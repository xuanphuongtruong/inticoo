using System.Text.RegularExpressions;

namespace InticooInspection.Client.Shared
{
    /// <summary>Người liên hệ của Customer/Vendor (khớp PartyProfileController.ContactDto).</summary>
    public class PartyContactVm
    {
        public int     Id             { get; set; }
        public string  Name           { get; set; } = "";
        public string? Position       { get; set; }
        public string? Email          { get; set; }
        public string? Phone          { get; set; }
        public bool    ReceiveReports { get; set; } = true;
        public bool    ScheduleEmail  { get; set; }
        public bool    IsPrimary      { get; set; }
        public bool    IsActive       { get; set; } = true;
    }

    /// <summary>Cấu hình báo cáo (khớp PartyProfileController.ReportSettingDto).</summary>
    public class PartyReportSettingVm
    {
        public string       Kind         { get; set; } = "PPI";   // PPI | DPI | PSI | Schedule
        public bool         Enabled      { get; set; } = true;
        public string       DeliveryMode { get; set; } = "Immediate"; // Immediate | ServerOnly | Scheduled | Manual
        public string       Language     { get; set; } = "en";
        public string       Subject      { get; set; } = "";
        public string       Body         { get; set; } = "";
        public int          PeriodDays   { get; set; } = 7;
        public List<string> SendDays     { get; set; } = new() { "Mon" };
        public string       SendTime     { get; set; } = "09:00";
        public string       TimeZone     { get; set; } = "Asia/Ho_Chi_Minh";
    }

    /// <summary>Mẫu email mặc định theo loại + ngôn ngữ (en / vi / zh / ms). Chỉ là nội dung khởi tạo, người dùng sửa tự do.</summary>
    public static class PartyTemplates
    {
        public static readonly string[] Langs = { "en", "vi", "zh", "ms" };

        private static readonly Dictionary<string, string> Names = new()
        {
            ["en|PPI"] = "pre-production inspection (PPI)", ["vi|PPI"] = "kiểm hàng trước sản xuất (PPI)",
            ["zh|PPI"] = "产前检验 (PPI)",                  ["ms|PPI"] = "pemeriksaan pra-pengeluaran (PPI)",
            ["en|DPI"] = "during production inspection (DPI)", ["vi|DPI"] = "kiểm hàng trong sản xuất (DPI)",
            ["zh|DPI"] = "生产中检验 (DPI)",                  ["ms|DPI"] = "pemeriksaan semasa pengeluaran (DPI)",
            ["en|PSI"] = "pre-shipment inspection (PSI)",    ["vi|PSI"] = "kiểm hàng trước giao (PSI)",
            ["zh|PSI"] = "出货前检验 (PSI)",                  ["ms|PSI"] = "pemeriksaan pra-penghantaran (PSI)",
            ["en|Schedule"] = "inspection schedule",         ["vi|Schedule"] = "lịch kiểm hàng",
            ["zh|Schedule"] = "检验计划",                      ["ms|Schedule"] = "jadual pemeriksaan",
        };

        private static string Name(string lang, string kind) =>
            Names.TryGetValue($"{lang}|{kind}", out var n) ? n : Names[$"en|{kind}"];

        public static string Subject(string lang, string kind, bool vendor)
        {
            var who = vendor ? "[Vendor Name]" : "[Customer Name]";
            return kind switch
            {
                "Schedule" => lang switch
                {
                    "vi" => $"Lịch kiểm hàng sắp tới – {who}",
                    "zh" => $"即将进行的检验计划 – {who}",
                    "ms" => $"Jadual Pemeriksaan Akan Datang – {who}",
                    _    => $"Upcoming Inspection Schedule – {who}",
                },
                _ => lang switch
                {
                    "vi" => $"Lịch {Name(lang, kind)} sắp tới – {who}",
                    "zh" => $"即将进行的{Name(lang, kind)} – {who}",
                    "ms" => $"{Cap(Name(lang, kind))} Akan Datang – {who}",
                    _    => $"Upcoming {Cap(Name(lang, kind))} – {who}",
                }
            };
        }

        public static string Body(string lang, string kind, bool vendor)
        {
            var name  = Name(lang, kind);
            var party = vendor ? "[Vendor Name]" : "[Customer Name]";
            var line  = vendor ? "" : lang switch
            {
                "vi" => $"<p>Khách hàng: {party}<br>Từ: [Start Date] đến [End Date]</p>",
                "zh" => $"<p>客户：{party}<br>从：[Start Date] 至 [End Date]</p>",
                "ms" => $"<p>Pelanggan: {party}<br>Dari: [Start Date] hingga [End Date]</p>",
                _    => $"<p>Customer: {party}<br>From: [Start Date] to [End Date]</p>",
            };
            return lang switch
            {
                "vi" => $"<p>Kính gửi [Contact Name],</p><p>Vui lòng xem {name} trong [Period] ngày tới bên dưới.</p>{line}<p>[Inspection Schedule Table]</p><p>Nếu có thắc mắc, vui lòng liên hệ chúng tôi.</p><p>Trân trọng,<br>Inticoo Team</p>",
                "zh" => $"<p>尊敬的 [Contact Name]，</p><p>请查看未来 [Period] 天的{name}如下。</p>{line}<p>[Inspection Schedule Table]</p><p>如有任何疑问，请随时联系我们。</p><p>此致，<br>Inticoo Team</p>",
                "ms" => $"<p>Kepada [Contact Name],</p><p>Sila lihat {name} untuk [Period] hari akan datang di bawah.</p>{line}<p>[Inspection Schedule Table]</p><p>Sila hubungi kami jika anda mempunyai sebarang pertanyaan.</p><p>Salam hormat,<br>Inticoo Team</p>",
                _    => $"<p>Dear [Contact Name],</p><p>Please find below the upcoming {name} for the next [Period] days.</p>{line}<p>[Inspection Schedule Table]</p><p>Please let us know if you have any questions.</p><p>Kind regards,<br>Inticoo Team</p>",
            };
        }

        /// <summary>true nếu nội dung đang là mẫu mặc định của một ngôn ngữ nào đó (→ được phép tự đổi khi người dùng đổi ngôn ngữ).</summary>
        public static bool IsDefault(PartyReportSettingVm s, bool vendor)
        {
            foreach (var l in Langs)
                if (string.IsNullOrWhiteSpace(s.Body) && string.IsNullOrWhiteSpace(s.Subject)
                    || (s.Subject == Subject(l, s.Kind, vendor) && s.Body == Body(l, s.Kind, vendor)))
                    return true;
            return false;
        }

        public static void ApplyDefault(PartyReportSettingVm s, string lang, bool vendor)
        {
            s.Language = lang;
            s.Subject  = Subject(lang, s.Kind, vendor);
            s.Body     = Body(lang, s.Kind, vendor);
        }

        private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

        private static readonly Regex Script = new(@"<\s*(script|style|iframe|object|embed)[^>]*>.*?<\s*/\s*\1\s*>|<\s*(script|style|iframe|object|embed)[^>]*/?\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex OnAttr = new(@"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex JsHref = new(@"(href|src)\s*=\s*(""|')?\s*javascript:[^""'>\s]*(""|')?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Bỏ script/iframe/on*=/javascript: khỏi HTML do người dùng soạn trước khi hiển thị preview.</summary>
        public static string Sanitize(string? html)
            => JsHref.Replace(OnAttr.Replace(Script.Replace(html ?? "", ""), ""), "");
    }
}
