using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InticooInspection.Domain.Security
{
    // ─────────────────────────────────────────────────────────────────────────
    // Quyền theo module của 1 tài khoản (lưu JSON ở cột AppUser.ModulePermissions).
    // File này DÙNG CHUNG cho API và Client (Client link file vào csproj) — chỉ dùng
    // thư viện chuẩn, không phụ thuộc EF/Identity.
    //
    // Mỗi module có mức: None (ẩn) / View (chỉ xem) / Edit (dùng module) và các cờ
    // hành động. Khoá module trùng với khoá PageAccess cũ để menu/guard cũ vẫn chạy.
    // ─────────────────────────────────────────────────────────────────────────

    public enum PermLevel { None = 0, View = 1, Edit = 2 }

    public static class PermAction
    {
        public const string Create   = "Create";
        public const string Edit     = "Edit";
        public const string Delete   = "Delete";
        public const string Export   = "Export";
        public const string Download = "Download";
        /// <summary>Dùng module (mức Edit) — cho module không có cờ Create/Edit/Delete, vd Inspection Schedule.</summary>
        public const string Use      = "Use";
    }

    public static class ModuleKeys
    {
        public const string Dashboard          = "Dashboard";
        public const string JobRequest         = "JobRequest";
        public const string InspectionSchedule = "InspectionSchedule";
        public const string DefectManagement   = "DefectManagement";
        public const string InspectionSummary  = "SummaryInspections";
        public const string PerformanceTest    = "PerformanceTest";
        public const string PerformanceReview  = "PerformanceReview";
        public const string Directory          = "Directory";
        public const string ContainerLoadingCheck = "ContainerLoadingCheck";
        public const string ReportsExport      = "ReportsExport";
    }

    /// <summary>Định nghĩa 1 module: mức tối đa và các cờ hành động.</summary>
    /// <param name="FlagsNeedEdit">true: cờ chỉ bật được khi mức = Edit (Create/Edit/Delete); false: bật được từ mức View (Export/Download).</param>
    public sealed record ModuleDef(string Key, string Label, PermLevel MaxLevel, string[] Flags, bool FlagsNeedEdit);

    public sealed class ModulePerm
    {
        [JsonPropertyName("level")]    public PermLevel Level    { get; set; }
        [JsonPropertyName("create")]   public bool      Create   { get; set; }
        [JsonPropertyName("edit")]     public bool      Edit     { get; set; }
        [JsonPropertyName("delete")]   public bool      Delete   { get; set; }
        [JsonPropertyName("export")]   public bool      Export   { get; set; }
        [JsonPropertyName("download")] public bool      Download { get; set; }

        public bool Flag(string action) => action switch
        {
            PermAction.Create   => Create,
            PermAction.Edit     => Edit,
            PermAction.Delete   => Delete,
            PermAction.Export   => Export,
            PermAction.Download => Download,
            _ => false
        };

        public void SetFlag(string action, bool value)
        {
            switch (action)
            {
                case PermAction.Create:   Create   = value; break;
                case PermAction.Edit:     Edit     = value; break;
                case PermAction.Delete:   Delete   = value; break;
                case PermAction.Export:   Export   = value; break;
                case PermAction.Download: Download = value; break;
            }
        }

        public ModulePerm Clone() => (ModulePerm)MemberwiseClone();
    }

    public sealed class ModulePermissionSet
    {
        static readonly string[] Cud = { PermAction.Create, PermAction.Edit, PermAction.Delete };

        /// <summary>Danh mục module hiển thị trên màn Create/Edit User (đúng thứ tự).</summary>
        public static readonly IReadOnlyList<ModuleDef> Catalog = new[]
        {
            new ModuleDef(ModuleKeys.Dashboard,          "Dashboard",           PermLevel.View, Array.Empty<string>(), true),
            new ModuleDef(ModuleKeys.JobRequest,         "Job Request",         PermLevel.Edit, Cud, true),
            new ModuleDef(ModuleKeys.InspectionSchedule, "Inspection Schedule", PermLevel.Edit, Array.Empty<string>(), true),
            new ModuleDef(ModuleKeys.ContainerLoadingCheck, "Container Loading Check", PermLevel.Edit, Cud, true),
            new ModuleDef(ModuleKeys.DefectManagement,   "Defect Management",   PermLevel.Edit, Cud, true),
            new ModuleDef(ModuleKeys.InspectionSummary,  "Inspection Summary",  PermLevel.Edit, Cud, true),
            new ModuleDef(ModuleKeys.PerformanceTest,    "Performance Test",    PermLevel.Edit, Cud, true),
            new ModuleDef(ModuleKeys.PerformanceReview,  "Performance Review",  PermLevel.View, Array.Empty<string>(), true),
            new ModuleDef(ModuleKeys.Directory,          "Directory",           PermLevel.Edit, Cud, true),   // Customer/Vendor/Product
            new ModuleDef(ModuleKeys.ReportsExport,      "Reports & Export",    PermLevel.Edit, new[] { PermAction.Export, PermAction.Download }, false),
        };

        public static ModuleDef? Def(string key) => Catalog.FirstOrDefault(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        static readonly JsonSerializerOptions _json = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        [JsonPropertyName("modules")]
        public Dictionary<string, ModulePerm> Modules { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public ModulePerm Get(string key)
        {
            if (!Modules.TryGetValue(key, out var p)) { p = new ModulePerm(); Modules[key] = p; }
            return p;
        }

        public PermLevel Level(string key) => Modules.TryGetValue(key, out var p) ? p.Level : PermLevel.None;
        public bool CanView(string key) => Level(key) >= PermLevel.View;

        /// <summary>Có được làm hành động <paramref name="action"/> trên module không (đã tính mức + quy tắc).</summary>
        public bool Can(string key, string action)
        {
            var def = Def(key);
            if (def == null || !Modules.TryGetValue(key, out var p)) return false;
            if (action == PermAction.Use) return p.Level >= PermLevel.Edit;
            if (!def.Flags.Contains(action)) return false;
            var need = def.FlagsNeedEdit ? PermLevel.Edit : PermLevel.View;
            return p.Level >= need && p.Flag(action);
        }

        /// <summary>Chuẩn hoá theo quy tắc: mức không vượt MaxLevel; cờ chỉ giữ khi đủ mức.</summary>
        public ModulePermissionSet Normalize()
        {
            var clean = new Dictionary<string, ModulePerm>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in Catalog)
            {
                var src = Modules.TryGetValue(def.Key, out var x) ? x : new ModulePerm();
                var p = new ModulePerm { Level = src.Level > def.MaxLevel ? def.MaxLevel : src.Level };
                var need = def.FlagsNeedEdit ? PermLevel.Edit : PermLevel.View;
                if (p.Level >= need)
                    foreach (var f in def.Flags) p.SetFlag(f, src.Flag(f));
                clean[def.Key] = p;
            }
            Modules = clean;
            return this;
        }

        /// <summary>CSV PageAccess tương ứng (các module có mức ≥ View) — giữ tương thích menu/guard cũ.</summary>
        public string ToPageAccessCsv() =>
            string.Join(",", Catalog.Where(d => CanView(d.Key)).Select(d => d.Key));

        public string ToJson() => JsonSerializer.Serialize(this, _json);

        public static ModulePermissionSet? TryParse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var set = JsonSerializer.Deserialize<ModulePermissionSet>(json, _json);
                if (set != null && !set.Modules.ContainsKey(ModuleKeys.ContainerLoadingCheck)
                    && set.Modules.TryGetValue(ModuleKeys.InspectionSummary, out var sum))
                    set.Modules[ModuleKeys.ContainerLoadingCheck] = sum.Clone();   // trước đây CLC đi theo Summary/Job Request
                return set?.Normalize();
            }
            catch { return null; }
        }

        /// <summary>
        /// Quyền hiệu lực: dùng JSON nếu có; nếu chưa có (tài khoản cũ) thì suy ra từ PageAccess CSV
        /// theo đúng hành vi trước đây — trang được thấy = dùng đầy đủ (Edit + mọi cờ).
        /// </summary>
        public static ModulePermissionSet Effective(string? modulePermissionsJson, string? pageAccessCsv)
            => TryParse(modulePermissionsJson) ?? FromLegacy(pageAccessCsv);

        public static ModulePermissionSet FromLegacy(string? pageAccessCsv)
        {
            var pages = new HashSet<string>((pageAccessCsv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()),
                                            StringComparer.OrdinalIgnoreCase);
            var set = new ModulePermissionSet();
            foreach (var def in Catalog)
            {
                var p = new ModulePerm();
                // Reports & Export trước đây ai cũng dùng được → mặc định View + Export + Download
                if (def.Key == ModuleKeys.ReportsExport) { p.Level = PermLevel.View; p.Export = p.Download = true; }
                else if (pages.Contains(def.Key) || (def.Key == ModuleKeys.ContainerLoadingCheck && pages.Contains(ModuleKeys.InspectionSummary)))
                {
                    p.Level = def.MaxLevel;
                    foreach (var f in def.Flags) p.SetFlag(f, true);
                    // Directory trước đây chỉ có View (ghi chỉ Admin) → tài khoản cũ giữ View, Admin tự cấp Edit
                    if (def.Key == ModuleKeys.Directory) { p.Level = PermLevel.View; p.Create = p.Edit = p.Delete = false; }
                }
                set.Modules[def.Key] = p;
            }
            return set;
        }

        /// <summary>Mẫu quyền mặc định khi tạo tài khoản mới theo role.</summary>
        public static ModulePermissionSet DefaultFor(string? role)
        {
            var csv = role switch
            {
                "Customer"  => "InspectionSchedule,SummaryInspections",
                "Inspector" => "JobRequest,InspectionSchedule,SummaryInspections,PerformanceReview",
                "Admin"       => "Dashboard,JobRequest,InspectionSchedule,DefectManagement,SummaryInspections,PerformanceTest,PerformanceReview,Directory",
                "QAQCManager" => "Dashboard,JobRequest,InspectionSchedule,DefectManagement,SummaryInspections,PerformanceTest,PerformanceReview,Directory",
                "Vendor"      => "InspectionSchedule,DefectManagement,SummaryInspections",
                "Management"  => "Dashboard,InspectionSchedule,DefectManagement,SummaryInspections,PerformanceReview",
                _           => ""   // CustomRole: Admin tự cấu hình
            };
            var set = FromLegacy(csv);
            if (role == "Management" || role == "Vendor")
            {
                // Quản lý / Nhà cung cấp: chỉ xem, không tạo/sửa/xoá
                foreach (var def in Catalog)
                {
                    var m = set.Get(def.Key);
                    if (m.Level > PermLevel.View) m.Level = PermLevel.View;
                    m.Create = m.Edit = m.Delete = false;
                }
            }
            if (role == "Customer")
            {
                // Khách hàng chỉ xem lịch, không bắt đầu kiểm hàng
                var sch = set.Get(ModuleKeys.InspectionSchedule);
                if (sch.Level > PermLevel.View) sch.Level = PermLevel.View;
                // Khách hàng: chỉ xem báo cáo, không tạo/sửa/xoá
                var s = set.Get(ModuleKeys.InspectionSummary);
                s.Level = PermLevel.View; s.Create = s.Edit = s.Delete = false;
                var clc = set.Get(ModuleKeys.ContainerLoadingCheck);
                clc.Level = PermLevel.View; clc.Create = clc.Edit = clc.Delete = false;
            }
            return set.Normalize();
        }

        public ModulePermissionSet Clone() => new()
        {
            Modules = Modules.ToDictionary(kv => kv.Key, kv => kv.Value.Clone(), StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>Danh sách khách hàng được truy cập (cột AppUser.CustomerIds): CSV mã KH, "*" = tất cả.</summary>
    public static class CustomerAccess
    {
        public const string All = "*";

        public static bool IsAll(string? csv) => (csv ?? "").Trim() == All;

        public static List<string> Parse(string? csv) =>
            IsAll(csv) ? new List<string>()
                       : (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim())
                                    .Where(s => s != "").Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        /// <summary>
        /// Phạm vi khách hàng hiệu lực (null = không giới hạn).
        /// - CustomerIds = "*"  → không giới hạn
        /// - CustomerIds có mã  → chỉ các mã đó
        /// - CustomerIds trống  → tài khoản cũ: role Customer giới hạn theo CustomerId; role khác không giới hạn
        /// </summary>
        public static HashSet<string>? Scope(string? customerIdsCsv, string? legacyCustomerId, bool isCustomerRole)
        {
            if (IsAll(customerIdsCsv)) return null;
            var list = Parse(customerIdsCsv);
            if (list.Count > 0) return new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
            if (!isCustomerRole) return null;
            return new HashSet<string>(string.IsNullOrWhiteSpace(legacyCustomerId) ? new[] { "__none__" } : new[] { legacyCustomerId.Trim() },
                                       StringComparer.OrdinalIgnoreCase);
        }
    }
}
