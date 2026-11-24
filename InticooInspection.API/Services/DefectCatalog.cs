namespace InticooInspection.API.Services
{
    /// <summary>
    /// Danh mục mã lỗi F01–F12 dùng cho thống kê Top Defect / Annual Defect Breakdown.
    /// Defect mới chọn mã ngay trên màn QC; defect cũ (chỉ có remark tự do) được suy ra theo từ khóa.
    /// Danh sách phải khớp với InticooInspection.Client.Shared.DefectCodes.
    /// </summary>
    public static class DefectCatalog
    {
        public const string OtherCode = "OTH";

        public static readonly (string Code, string Name)[] Codes =
        {
            ("F01", "Scratch / Surface Damage"),
            ("F02", "Dent / Deformation"),
            ("F03", "Crack / Breakage"),
            ("F04", "Colour Variation / Appearance Defect"),
            ("F05", "Poor Finish / Coating Defect"),
            ("F06", "Material Defect"),
            ("F07", "Loose Hardware / Component"),
            ("F08", "Missing Parts / Accessories"),
            ("F09", "Assembly Error"),
            ("F10", "Instability / Functional Defect"),
            ("F11", "Dimension Out of Specification"),
            ("F12", "Packaging Damage"),
        };

        // Thứ tự quan trọng: từ khóa cụ thể đứng trước từ khóa chung ("damage")
        static readonly (string Code, string[] Keywords)[] Rules =
        {
            ("F12", new[] { "packag", "carton", "box", "label", "shipping mark" }),
            ("F01", new[] { "scratch", "scuff", "abrasion" }),
            ("F02", new[] { "dent", "deform", "warp", "bent", "bend" }),
            ("F03", new[] { "crack", "break", "broken", "split", "chip" }),
            ("F04", new[] { "colour", "color", "shade", "discolo" }),
            ("F05", new[] { "finish", "coating", "paint", "rough", "glue", "stain", "dirty", "spot", "bubble", "lacquer", "sanding" }),
            ("F06", new[] { "material", "fingerjoint", "finger joint", "knot", "wood", "fabric", "leather", "mould", "mold", "rust" }),
            ("F07", new[] { "loose", "screw", "bolt", "hardware", "nut" }),
            ("F08", new[] { "missing" }),
            ("F09", new[] { "assembl", "gap", "align", "misalign", "uneven" }),
            ("F10", new[] { "function", "wobbl", "unstable", "drawer", "hinge", "mechanism" }),
            ("F11", new[] { "dimension", "size", "oos", "out of spec" }),
            ("F01", new[] { "damage", "surface" }),
        };

        public static string Name(string? code) =>
            Codes.FirstOrDefault(c => c.Code == code).Name ?? "Other";

        public static string Label(string? code) =>
            code == null || code == OtherCode ? "Other" : $"{code} {Name(code)}";

        /// <summary>Mã lỗi của 1 defect: ưu tiên mã đã chọn, nếu không suy từ remark.</summary>
        public static string Resolve(string? defectCode, string? remark)
        {
            if (!string.IsNullOrWhiteSpace(defectCode) && Codes.Any(c => c.Code == defectCode)) return defectCode!;
            var r = (remark ?? "").ToLowerInvariant();
            if (r.Length > 0)
                foreach (var (code, kws) in Rules)
                    if (kws.Any(k => r.Contains(k))) return code;
            return OtherCode;
        }
    }
}
