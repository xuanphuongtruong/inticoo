namespace InticooInspection.Client.Shared
{
    /// <summary>Mã lỗi F01–F12 (khớp InticooInspection.API.Services.DefectCatalog).</summary>
    public static class DefectCodes
    {
        public static readonly (string Code, string Name)[] All =
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

        /// <summary>Tên lỗi theo ngôn ngữ đang chọn (key "Opt.{tên tiếng Anh}"); giá trị lưu DB vẫn là tên tiếng Anh.</summary>
        public static string LocalName(string name) => InticooInspection.Client.Services.LanguageService.Tr("Opt." + name, name);

        public static string Label(string? code)
        {
            var m = All.FirstOrDefault(c => c.Code == code);
            return m.Code == null ? "" : $"{m.Code} {LocalName(m.Name)}";
        }
    }
}
