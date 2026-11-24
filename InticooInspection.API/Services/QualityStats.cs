using System.Text.Json;
using InticooInspection.Domain.Entities;
using InticooInspection.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.API.Services
{
    /// <summary>
    /// 1 inspection đã "làm phẳng" phục vụ thống kê: số lỗi / cỡ mẫu AQL / mã lỗi lấy từ QcResultJson.
    /// </summary>
    public class InspStat
    {
        public int      Id              { get; init; }
        public string?  JobNumber       { get; init; }
        public InspectionType   Type    { get; init; }
        public InspectionStatus Status  { get; init; }
        public DateTime InspectionDate  { get; init; }
        public DateTime CreatedAt       { get; init; }
        public DateTime? CompletedAt    { get; init; }
        public string?  FinalResult     { get; init; }
        public string   CustomerName    { get; init; } = "";
        public string?  CustomerId      { get; init; }
        public string   VendorName      { get; init; } = "";
        public string?  VendorId        { get; init; }
        public string?  Category        { get; init; }
        public string   ProductName     { get; init; } = "";
        public string?  ItemNumber      { get; init; }
        public string?  InspectorId     { get; init; }
        public string?  InspectorName   { get; init; }
        public int      ShipmentQty     { get; init; }

        public int Critical   { get; set; }
        public int Major      { get; set; }
        public int Minor      { get; set; }
        public int SampleSize { get; set; }          // cỡ mẫu AQL đã kiểm
        public DateTime? StartTime { get; set; }     // giờ bắt đầu kiểm (UTC)
        public List<string> DefectCodes { get; } = new();   // mỗi phần tử = 1 lỗi (đã resolve F01..F12/OTH)

        public int  Defects     => Critical + Major + Minor;
        public bool IsCompleted => Status == InspectionStatus.Completed;
        public bool IsAwaiting  => Status is InspectionStatus.Pending or InspectionStatus.InProgress;
        public bool Passed      => IsCompleted && FinalResult == "PASSED";
        public bool Failed      => IsCompleted && FinalResult == "FAILED";
        /// <summary>Có kết luận Pass/Fail (loại NA) — mẫu số cho Pass Rate.</summary>
        public bool Judged      => Passed || Failed;

        /// <summary>
        /// On-time: báo cáo QC được hoàn tất (Done) trong vòng 24 giờ kể từ lúc bắt đầu kiểm.
        /// Null = không xác định được (job cũ không lưu giờ bắt đầu).
        /// </summary>
        public bool? OnTime => IsCompleted && CompletedAt.HasValue && StartTime.HasValue
            ? (CompletedAt.Value - StartTime.Value).TotalHours <= 24
            : null;

        public string ProductKey => !string.IsNullOrWhiteSpace(ItemNumber) ? ItemNumber!.Trim() : ProductName.Trim();
    }

    public static class QualityStats
    {
        public static readonly string[] MonthNames = { "Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec" };

        /// <summary>Nạp inspection (bỏ Cancelled) + parse QcResultJson.</summary>
        public static async Task<List<InspStat>> LoadAsync(AppDbContext db, Func<IQueryable<Inspection>, IQueryable<Inspection>>? filter = null)
        {
            var q = db.Inspections.AsNoTracking().Where(i => i.Status != InspectionStatus.Cancelled);
            if (filter != null) q = filter(q);

            var rows = await q.Select(i => new
            {
                i.Id, i.JobNumber, i.InspectionType, i.Status, i.InspectionDate, i.CreatedAt, i.CompletedAt,
                i.FinalResult, i.CustomerName, i.CustomerId, i.VendorName, i.VendorId, i.ProductCategory,
                i.ProductName, i.ItemNumber, i.InspectorId, i.InspectorName, i.TotalShipmentQty, i.QcResultJson
            }).ToListAsync();

            return rows.Select(r =>
            {
                var s = new InspStat
                {
                    Id = r.Id, JobNumber = r.JobNumber, Type = r.InspectionType, Status = r.Status,
                    InspectionDate = r.InspectionDate, CreatedAt = r.CreatedAt, CompletedAt = r.CompletedAt,
                    FinalResult = r.FinalResult, CustomerName = r.CustomerName ?? "", CustomerId = r.CustomerId,
                    VendorName = r.VendorName ?? "", VendorId = r.VendorId, Category = r.ProductCategory,
                    ProductName = r.ProductName ?? "", ItemNumber = r.ItemNumber, InspectorId = r.InspectorId,
                    InspectorName = r.InspectorName, ShipmentQty = r.TotalShipmentQty
                };
                ParseQc(s, r.QcResultJson);
                return s;
            }).ToList();
        }

        static void ParseQc(InspStat s, string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (TryGet(root, "inspectionStartTime", out var st) && st.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(st.GetString(), out var dto))
                    s.StartTime = dto.UtcDateTime;

                if (TryGet(root, "aql", out var aql) && aql.ValueKind == JsonValueKind.Object)
                {
                    s.Critical   = GetInt(aql, "criticalFound");
                    s.Major      = GetInt(aql, "majorFound");
                    s.Minor      = GetInt(aql, "minorFound");
                    s.SampleSize = new[] { GetInt(aql, "criticalSampleSize"), GetInt(aql, "majorSampleSize"), GetInt(aql, "minorSampleSize") }.Max();
                }

                if (TryGet(root, "defects", out var defs) && defs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in defs.EnumerateArray())
                    {
                        var code = TryGet(d, "defectCode", out var dc) && dc.ValueKind == JsonValueKind.String ? dc.GetString() : null;
                        var photos  = TryGet(d, "photos", out var ph) && ph.ValueKind == JsonValueKind.Array
                            ? ph.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null).ToList()
                            : new List<string?>();
                        var remarks = TryGet(d, "remarks", out var rm) && rm.ValueKind == JsonValueKind.Array
                            ? rm.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null).ToList()
                            : new List<string?>();
                        // Mỗi ảnh = 1 lỗi (UI mới 1 dòng = 1 ảnh); dòng không ảnh thì bỏ
                        for (int k = 0; k < photos.Count; k++)
                        {
                            if (string.IsNullOrEmpty(photos[k])) continue;
                            s.DefectCodes.Add(DefectCatalog.Resolve(code, k < remarks.Count ? remarks[k] : null));
                        }
                    }
                }
            }
            catch { /* JSON hỏng → coi như không có số liệu QC */ }
        }

        static bool TryGet(JsonElement el, string name, out JsonElement value)
        {
            if (el.ValueKind == JsonValueKind.Object)
                foreach (var p in el.EnumerateObject())
                    if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
            value = default;
            return false;
        }

        static int GetInt(JsonElement el, string name) =>
            TryGet(el, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;

        // ── Công thức dùng chung ─────────────────────────────────────────

        public static double Pct(double num, double den, int digits = 1) =>
            den > 0 ? Math.Round(num / den * 100, digits) : 0;

        /// <summary>Pass Rate = Passed ÷ (Passed + Failed) × 100 — inspection NA/chưa xong không tính.</summary>
        public static double PassRate(IEnumerable<InspStat> list)
        {
            var l = list.Where(i => i.Judged).ToList();
            return Pct(l.Count(i => i.Passed), l.Count);
        }

        /// <summary>Defect Rate (theo AQL) = tổng lỗi ÷ tổng cỡ mẫu AQL × 100.</summary>
        public static double DefectRateAql(IEnumerable<InspStat> list)
        {
            var l = list.Where(i => i.IsCompleted).ToList();
            return Pct(l.Sum(i => i.Defects), l.Sum(i => i.SampleSize), 2);
        }

        /// <summary>% On-time trên các inspection xác định được (null nếu không có dữ liệu).</summary>
        public static double? OnTimeRate(IEnumerable<InspStat> list)
        {
            var l = list.Where(i => i.OnTime.HasValue).ToList();
            return l.Count == 0 ? null : Pct(l.Count(i => i.OnTime == true), l.Count);
        }

        /// <summary>Top mã lỗi của danh sách (null nếu không có lỗi).</summary>
        public static string? TopDefect(IEnumerable<InspStat> list)
        {
            var top = list.SelectMany(i => i.DefectCodes).GroupBy(c => c)
                          .OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();
            return top == null ? null : DefectCatalog.Label(top.Key);
        }

        /// <summary>
        /// Overall Score theo KPI scoring (Pass 30% / Defect 40% / CAPA 30%).
        /// CAPA chưa có module → bỏ CAPA và chuẩn hoá lại trọng số (30/70, 40/70).
        /// Điểm từng KPI: đạt target = 100, chưa đạt thì giảm theo tỉ lệ.
        /// </summary>
        public static double? OverallScore(double? passRate, double? defectRate, double? capaRate = null)
        {
            if (passRate == null || defectRate == null) return null;
            double passScore   = Math.Min(100, passRate.Value / 95.0 * 100);
            double defectScore = defectRate.Value <= 0.5 ? 100 : Math.Max(0, 0.5 / defectRate.Value * 100);
            if (capaRate == null)
                return Math.Round((passScore * 30 + defectScore * 40) / 70.0, 1);
            double capaScore = Math.Min(100, capaRate.Value / 95.0 * 100);
            return Math.Round(passScore * 0.3 + defectScore * 0.4 + capaScore * 0.3, 1);
        }

        public static string Rating(double? score) => score switch
        {
            null    => "—",
            >= 95   => "Excellent",
            >= 90   => "Good",
            >= 80   => "Acceptable",
            _       => "Improvement Required"
        };

        /// <summary>Dải 5 năm cho bảng Annual: bắt đầu từ năm dữ liệu đầu tiên (không trước 2026).</summary>
        public static int[] AnnualYears(IEnumerable<InspStat> list)
        {
            var first = list.Any() ? list.Min(i => i.InspectionDate.Year) : DateTime.UtcNow.Year;
            var start = Math.Max(2026, Math.Min(first, DateTime.UtcNow.Year));
            return Enumerable.Range(start, 5).ToArray();
        }
    }
}
