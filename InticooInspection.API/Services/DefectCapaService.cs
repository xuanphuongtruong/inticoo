using System.Text.Json;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Services
{
    /// <summary>
    /// Tự mở CAPA khi inspection hoàn tất (QC bấm Done).
    /// Gom lỗi theo (mã lỗi F01..F12, mức độ) trong cùng inspection; mỗi ảnh lỗi = 1 pc.
    /// Ngưỡng mở CAPA: Critical ≥ 1 pc · Major ≥ 3 pcs · Minor ≥ 10 pcs.
    /// </summary>
    public static class DefectCapaService
    {
        public const string StatusOpen       = "Open";
        public const string StatusInProgress = "In Progress";
        public const string StatusClosed     = "Closed";

        public static int Threshold(string severity) => severity switch
        {
            "Critical" => 1,
            "Major"    => 3,
            _          => 10
        };

        /// <summary>Số ngày mặc định từ lúc mở tới hạn xử lý.</summary>
        public static int DueDays(string severity) => severity switch
        {
            "Critical" => 7,
            "Major"    => 10,
            _          => 14
        };

        public static string CategoryOf(string? code) => code switch
        {
            "F04"                => "Appearance",
            "F06"                => "Material",
            "F07" or "F08"       => "Hardware / Components",
            "F10"                => "Function",
            "F11"                => "Dimension",
            "F12"                => "Packaging",
            _                    => "Workmanship"
        };

        public record Candidate(string Code, string Severity, int Quantity, List<string> Photos, List<string> Remarks);

        /// <summary>Tính các nhóm lỗi vượt ngưỡng từ QcResultJson.</summary>
        public static List<Candidate> Evaluate(string? qcResultJson, SettingsStore.CapaRules? rules = null)
        {
            var rows = new List<(string Code, string Sev, string Photo, string? Remark)>();
            if (string.IsNullOrWhiteSpace(qcResultJson)) return new();
            try
            {
                using var doc = JsonDocument.Parse(qcResultJson);
                if (!TryGet(doc.RootElement, "defects", out var defs) || defs.ValueKind != JsonValueKind.Array) return new();
                foreach (var d in defs.EnumerateArray())
                {
                    var sev  = TryGet(d, "type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                    var code = TryGet(d, "defectCode", out var dc) && dc.ValueKind == JsonValueKind.String ? dc.GetString() : null;
                    var photos  = StrList(d, "photos");
                    var remarks = StrList(d, "remarks");
                    for (int k = 0; k < photos.Count; k++)
                    {
                        if (string.IsNullOrEmpty(photos[k])) continue;
                        var rmk = k < remarks.Count ? remarks[k] : null;
                        rows.Add((DefectCatalog.Resolve(code, rmk), sev, photos[k]!, rmk));
                    }
                }
            }
            catch { return new(); }

            return rows.Where(r => r.Sev is "Critical" or "Major" or "Minor")
                .GroupBy(r => (r.Code, r.Sev))
                .Where(g => g.Count() >= (rules?.For(g.Key.Sev) ?? Threshold(g.Key.Sev)))
                .Select(g => new Candidate(g.Key.Code, g.Key.Sev, g.Count(),
                    g.Select(x => x.Photo).ToList(),
                    g.Select(x => x.Remark).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();
        }

        /// <summary>Mở CAPA còn thiếu cho 1 inspection. dryRun = chỉ trả danh sách, không ghi DB.</summary>
        public static async Task<List<DefectCapa>> AutoOpenAsync(AppDbContext db, int inspectionId, bool dryRun = false)
        {
            var insp = await db.Inspections.AsNoTracking().FirstOrDefaultAsync(i => i.Id == inspectionId);
            if (insp == null || insp.Status != InspectionStatus.Completed) return new();

            // Ngưỡng + bật/tắt lấy từ Settings › Inspection & Defect Management
            var rules = await SettingsStore.GetCapaRulesAsync(db);
            if (!rules.Enabled) return new();
            var candidates = Evaluate(insp.QcResultJson, rules);
            if (!candidates.Any()) return new();

            var existing = await db.DefectCapas.AsNoTracking()
                .Where(c => c.InspectionId == inspectionId)
                .Select(c => new { c.DefectCode, c.Severity }).ToListAsync();

            var sample = AqlSample(insp.QcResultJson);
            var opened = DateTime.UtcNow;
            var created = new List<DefectCapa>();
            foreach (var c in candidates)
            {
                if (existing.Any(e => e.DefectCode == c.Code && e.Severity == c.Severity)) continue;
                var due = opened.Date.AddDays(DueDays(c.Severity));
                created.Add(new DefectCapa
                {
                    InspectionId   = insp.Id,
                    JobNumber      = insp.JobNumber,
                    PoNumber       = insp.PoNumber,
                    InspectionDate = insp.InspectionDate,
                    InspectorName  = insp.InspectorName,
                    VendorId       = insp.VendorId,
                    VendorName     = insp.VendorName,
                    CustomerId     = insp.CustomerId,
                    CustomerName   = insp.CustomerName,
                    ProductName    = insp.ProductName,
                    ItemNumber     = insp.ItemNumber,
                    DefectCode     = c.Code,
                    DefectType     = c.Code == DefectCatalog.OtherCode ? "Other" : DefectCatalog.Name(c.Code),
                    DefectCategory = CategoryOf(c.Code),
                    Severity       = c.Severity,
                    AqlClassification = c.Severity,
                    Quantity       = c.Quantity,
                    ProductQtyChecked = sample > 0 ? sample : null,
                    Description    = c.Remarks.Any()
                        ? $"{c.Quantity} pcs found during {StageName(insp.InspectionType)}: {string.Join(", ", c.Remarks)}."
                        : $"{c.Quantity} pcs found during {StageName(insp.InspectionType)}.",
                    PhotosJson     = JsonSerializer.Serialize(c.Photos),
                    Priority       = c.Severity == "Critical" ? "High" : c.Severity == "Major" ? "Medium" : "Low",
                    Status         = StatusOpen,
                    OpenDate       = opened,
                    DueDate        = due,
                    TargetDate     = due,
                    AutoCreated    = true,
                    CreatedAt      = opened,
                });
            }

            if (!dryRun && created.Any())
            {
                foreach (var c in created)
                {
                    await AssignNumbersAsync(db, c);
                    db.DefectCapas.Add(c);   // vào Local để số kế tiếp không trùng
                }
                await db.SaveChangesAsync();
            }
            return created;
        }

        /// <summary>Gán DEF-xxx (chạy liên tục) và CAR-yyyy-xxx (đánh lại theo năm).</summary>
        public static async Task AssignNumbersAsync(AppDbContext db, DefectCapa c)
        {
            var defNos = await db.DefectCapas.Select(x => x.DefectNo).ToListAsync();
            defNos.AddRange(db.DefectCapas.Local.Select(x => x.DefectNo));
            var carNos = await db.DefectCapas.Select(x => x.CarNo).ToListAsync();
            carNos.AddRange(db.DefectCapas.Local.Select(x => x.CarNo));

            int maxDef = defNos.Select(n => int.TryParse(n?.Replace("DEF-", ""), out var v) ? v : 0).DefaultIfEmpty(0).Max();
            var year   = c.OpenDate.Year;
            var prefix = $"CAR-{year}-";
            int maxCar = carNos.Where(n => n != null && n.StartsWith(prefix))
                               .Select(n => int.TryParse(n![prefix.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
            c.DefectNo = $"DEF-{maxDef + 1:D3}";
            c.CarNo    = $"{prefix}{maxCar + 1:D3}";
        }

        static string StageName(InspectionType t) => t switch
        {
            InspectionType.PPT => "pre-production inspection",
            InspectionType.DPI => "during production inspection",
            _                  => "pre-shipment inspection"
        };

        static int AqlSample(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return 0;
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (!TryGet(doc.RootElement, "aql", out var aql)) return 0;
                int Get(string n) => TryGet(aql, n, out var v) && v.TryGetInt32(out var i) ? i : 0;
                return new[] { Get("criticalSampleSize"), Get("majorSampleSize"), Get("minorSampleSize") }.Max();
            }
            catch { return 0; }
        }

        static List<string?> StrList(JsonElement el, string name) =>
            TryGet(el, name, out var a) && a.ValueKind == JsonValueKind.Array
                ? a.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null).ToList()
                : new List<string?>();

        static bool TryGet(JsonElement el, string name, out JsonElement value)
        {
            if (el.ValueKind == JsonValueKind.Object)
                foreach (var p in el.EnumerateObject())
                    if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
            value = default;
            return false;
        }
    }
}
