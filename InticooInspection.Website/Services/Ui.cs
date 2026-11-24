using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Html;

namespace InticooInspection.Website.Services;

/// <summary>Tiện ích hiển thị dùng chung cho các trang website.</summary>
public static class Ui
{
    private static readonly Dictionary<string, string> FlagFile = new() { ["en"] = "gb", ["zh"] = "cn", ["vi"] = "vn", ["ms"] = "my" };

    /// <summary>Ảnh cờ cho mã ngôn ngữ (en/zh/vi/ms) hoặc mã nước có sẵn trong img/flags (us, eu...).</summary>
    public static IHtmlContent Flag(string code, string cls = "flag") =>
        new HtmlString($"<img class=\"{cls}\" src=\"/img/flags/{(FlagFile.TryGetValue(code, out var f) ? f : code)}.svg\" alt=\"\" />");

    /// <summary>Mã ngắn hiển thị ở nút chọn ngôn ngữ (EN/中文/VI/BM).</summary>
    public static string Short(string lang) => lang switch { "zh" => "中文", "vi" => "VI", "ms" => "BM", _ => "EN" };

    /// <summary>Mã vùng điện thoại theo mã nước trong CountryList (ô Phone Number ở trang đăng ký).</summary>
    public static readonly Dictionary<string, string> Dial = new()
    {
        ["MY"] = "+60", ["SG"] = "+65", ["VN"] = "+84", ["CN"] = "+86", ["US"] = "+1", ["GB"] = "+44", ["DE"] = "+49", ["FR"] = "+33",
        ["IT"] = "+39", ["ES"] = "+34", ["NL"] = "+31", ["SE"] = "+46", ["DK"] = "+45", ["PL"] = "+48", ["CA"] = "+1", ["AU"] = "+61",
        ["NZ"] = "+64", ["JP"] = "+81", ["KR"] = "+82", ["TH"] = "+66", ["ID"] = "+62", ["PH"] = "+63", ["IN"] = "+91", ["HK"] = "+852",
        ["TW"] = "+886", ["AE"] = "+971", ["TR"] = "+90", ["MX"] = "+52", ["BR"] = "+55", ["ZA"] = "+27"
    };

    /// <summary>Icon gói theo mã gói (thiết kế: Basic = nhóm người, Business = cột, Professional = tòa nhà).</summary>
    public static string PlanIcon(string code) => code switch { "basic" => "users", "business" => "barchart", _ => "building" };

    /// <summary>Nội dung 1 mục Resources theo ngôn ngữ đang xem. Mục chưa có bản dịch nào cho ngôn ngữ này
    /// thì thử tra câu tiếng Anh trong file dịch của website (nội dung mặc định đã được dịch sẵn ở đó).</summary>
    public static (string Title, string? Summary, string? Body, string? Button) Res(ResourceItem r, Loc l)
    {
        var (t, s, b, bt) = ResourceContent.Localized(r, l.Lang);
        if (l.Lang != "en" && !r.Translations.Any(x => x.Lang == l.Lang))
        { t = l[t]; s = s == null ? null : l[s]; bt = bt == null ? null : l[bt]; }
        return (t, s, b, bt);
    }

    /// <summary>Chữ nút mặc định theo mục/loại khi Super Admin không nhập.</summary>
    public static string DefaultButton(ResourceItem r) => r.Section switch
    {
        ResourceSection.Reference => "Download PDF",
        _ when r.Kind == "Video" => "Watch Video",
        ResourceSection.Featured => r.Kind == "Checklist" ? "View Checklists" : r.Kind == "Article" ? "Read Articles" : "View Guide",
        _ => "Read More"
    };
}
