using Microsoft.AspNetCore.Html;

namespace InticooInspection.Portal.Services;

/// <summary>Bộ icon SVG nội tuyến (nét đơn giản) dùng cho website và khu quản trị — không phụ thuộc CDN.</summary>
public static class Icons
{
    private static readonly Dictionary<string, string> P = new()
    {
        ["doc"] = "<path d='M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z'/><path d='M14 3v5h5M9 13h6M9 17h6'/>",
        ["clipboard"] = "<rect x='6' y='4' width='12' height='17' rx='2'/><path d='M9 4h6v3H9zM9 12l2 2 4-4'/>",
        ["box"] = "<path d='M21 8l-9-5-9 5v8l9 5 9-5z'/><path d='M3 8l9 5 9-5M12 13v8'/>",
        ["container"] = "<rect x='3' y='5' width='18' height='14' rx='1'/><path d='M7 5v14M11 5v14M15 5v14M19 5v14'/>",
        ["users"] = "<circle cx='9' cy='8' r='3.2'/><path d='M3 20a6 6 0 0 1 12 0'/><circle cx='17' cy='9' r='2.4'/><path d='M16 14.2A5 5 0 0 1 21 19'/>",
        ["user"] = "<circle cx='12' cy='8' r='4'/><path d='M4 21a8 8 0 0 1 16 0'/>",
        ["alert"] = "<path d='M12 3l10 18H2z'/><path d='M12 10v5M12 18h.01'/>",
        ["shield"] = "<path d='M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z'/><path d='M9 12l2 2 4-4'/>",
        ["lock"] = "<rect x='5' y='11' width='14' height='9' rx='2'/><path d='M8 11V8a4 4 0 0 1 8 0v3'/>",
        ["chart"] = "<path d='M4 20V10M10 20V4M16 20v-8M22 20H2'/>",
        ["trend"] = "<path d='M3 17l6-6 4 4 8-8'/><path d='M15 7h6v6'/>",
        ["cloud"] = "<path d='M7 18a4 4 0 0 1-.5-8A6 6 0 0 1 18 9a4.5 4.5 0 0 1-.5 9z'/>",
        ["globe"] = "<circle cx='12' cy='12' r='9'/><path d='M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18'/>",
        ["headset"] = "<path d='M4 14v-2a8 8 0 0 1 16 0v2'/><rect x='3' y='14' width='4' height='6' rx='1.5'/><rect x='17' y='14' width='4' height='6' rx='1.5'/><path d='M19 20a4 4 0 0 1-4 2h-2'/>",
        ["mail"] = "<rect x='3' y='5' width='18' height='14' rx='2'/><path d='M3 7l9 6 9-6'/>",
        ["clock"] = "<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/>",
        ["phone"] = "<rect x='7' y='2' width='10' height='20' rx='2'/><path d='M11 18h2'/>",
        ["building"] = "<rect x='5' y='3' width='9' height='18' rx='1'/><path d='M14 9h5v12h-5M8 7h3M8 11h3M8 15h3'/>",
        ["settings"] = "<circle cx='12' cy='12' r='3'/><path d='M12 2v3M12 19v3M4.2 4.2l2.1 2.1M17.7 17.7l2.1 2.1M2 12h3M19 12h3M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1'/>",
        ["calendar"] = "<rect x='3' y='5' width='18' height='16' rx='2'/><path d='M3 10h18M8 3v4M16 3v4'/>",
        ["heart"] = "<path d='M12 20s-8-5-8-11a4.5 4.5 0 0 1 8-2.5A4.5 4.5 0 0 1 20 9c0 6-8 11-8 11z'/>",
        ["arrow"] = "<path d='M5 12h14M13 6l6 6-6 6'/>",
        ["check"] = "<path d='M5 12.5l4.5 4.5L19 7.5'/>",
        ["card"] = "<rect x='2' y='5' width='20' height='14' rx='2'/><path d='M2 10h20M6 15h4'/>",
        ["bank"] = "<path d='M3 10l9-6 9 6M5 10v8M9 10v8M15 10v8M19 10v8M3 20h18'/>",
        ["dollar"] = "<circle cx='12' cy='12' r='9'/><path d='M14.5 9a2.5 2.5 0 0 0-2.5-1.5c-1.4 0-2.5.8-2.5 2s1 1.7 2.5 2 2.5.8 2.5 2-1.1 2-2.5 2A2.5 2.5 0 0 1 9.5 15M12 6v1.5M12 16.5V18'/>",
        ["receipt"] = "<path d='M6 3h12v18l-3-2-3 2-3-2-3 2z'/><path d='M9 8h6M9 12h6'/>",
        ["grid"] = "<rect x='4' y='4' width='7' height='7' rx='1.5'/><rect x='13' y='4' width='7' height='7' rx='1.5'/><rect x='4' y='13' width='7' height='7' rx='1.5'/><rect x='13' y='13' width='7' height='7' rx='1.5'/>",
        ["home"] = "<path d='M3 11l9-8 9 8M5 10v10h14V10'/>",
        ["crown"] = "<path d='M3 18h18l-2-10-5 4-2-6-2 6-5-4z'/>",
        ["log"] = "<path d='M6 3h9l4 4v14H6z'/><path d='M9 12h7M9 16h7M9 8h3'/>",
        ["search"] = "<circle cx='11' cy='11' r='7'/><path d='M20 20l-4-4'/>",
        ["download"] = "<path d='M12 4v11M7 11l5 5 5-5M5 20h14'/>",
        ["plus"] = "<path d='M12 5v14M5 12h14'/>",
        ["eye"] = "<path d='M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z'/><circle cx='12' cy='12' r='3'/>",
        ["edit"] = "<path d='M4 20h4L19 9l-4-4L4 16z'/>",
        ["trash"] = "<path d='M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13'/>",
        ["bell"] = "<path d='M6 16V11a6 6 0 0 1 12 0v5l2 2H4z'/><path d='M10 21h4'/>",
        ["logout"] = "<path d='M9 4H5v16h4M16 8l4 4-4 4M20 12H9'/>",
        ["key"] = "<circle cx='8' cy='15' r='4'/><path d='M11 12l9-9M16 7l3 3'/>",
        ["book"] = "<path d='M4 5a2 2 0 0 1 2-2h13v16H6a2 2 0 0 0-2 2zM4 19V5'/>",
        ["cap"] = "<path d='M2 9l10-5 10 5-10 5zM6 11v5c0 1.5 3 3 6 3s6-1.5 6-3v-5'/>",
        ["help"] = "<circle cx='12' cy='12' r='9'/><path d='M9.5 9.5a2.5 2.5 0 1 1 3.5 2.3c-.7.4-1 .9-1 1.7M12 17h.01'/>",
        ["chat"] = "<path d='M4 5h16v11H9l-5 4z'/>",
        ["bulb"] = "<path d='M9 18h6M10 21h4M12 3a6 6 0 0 0-3.5 10.8c.6.5 1 1.2 1 2V16h5v-.2c0-.8.4-1.5 1-2A6 6 0 0 0 12 3z'/>",
        ["ruler"] = "<path d='M3 17L17 3l4 4L7 21z'/><path d='M7 13l2 2M10 10l2 2M13 7l2 2'/>",
        ["pause"] = "<circle cx='12' cy='12' r='9'/><path d='M10 9v6M14 9v6'/>",
        ["x"] = "<circle cx='12' cy='12' r='9'/><path d='M9 9l6 6M15 9l-6 6'/>",
        ["wallet"] = "<path d='M3 7a2 2 0 0 1 2-2h13v4'/><rect x='3' y='7' width='18' height='13' rx='2'/><path d='M16 13.5h2'/>",
        ["layers"] = "<path d='M12 3l9 5-9 5-9-5z'/><path d='M3 13l9 5 9-5'/>",
        ["link"] = "<path d='M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1'/>",
        ["play"] = "<circle cx='12' cy='12' r='9'/><path d='M10 8.5l5 3.5-5 3.5z'/>",
        ["pin"] = "<path d='M12 21s-7-6.2-7-11.5a7 7 0 0 1 14 0C19 14.8 12 21 12 21z'/><circle cx='12' cy='9.5' r='2.5'/>",
        ["linkedin"] = "<rect x='3' y='3' width='18' height='18' rx='3'/><path d='M8 10v7M8 7v.01M12 17v-4a2 2 0 0 1 4 0v4M12 10v7'/>",
        ["youtube"] = "<rect x='2.5' y='5' width='19' height='14' rx='4'/><path d='M10 9l5 3-5 3z'/>",
        ["diamond"] = "<path d='M6 3h12l4 6-10 12L2 9z'/><path d='M2 9h20M9 3l3 18 3-18'/>",
        ["mountain"] = "<path d='M3 20l6-10 4 6 3-4 5 8z'/><path d='M9 10V3l5 2-5 2'/>",
        ["target"] = "<circle cx='12' cy='12' r='9'/><circle cx='12' cy='12' r='5'/><circle cx='12' cy='12' r='1.5'/>",
        ["handshake"] = "<path d='M2 12l4-4 4 2 3-2 3 1 6 3'/><path d='M6 8l-4 4 5 5 2-1 2 2 2-1 2 1 3-3 2-3'/><path d='M10 14l2 2M12 12l2 2'/>",
        ["chevron"] = "<path d='M6 9l6 6 6-6'/>",
        ["checkcircle"] = "<circle cx='12' cy='12' r='9'/><path d='M8 12.5l2.7 2.7L16 10'/>",
        ["star"] = "<path d='M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1-4.4-4.3 6.1-.9z'/>",
        ["folder"] = "<path d='M3 6a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z'/>",
        ["api"] = "<circle cx='12' cy='5' r='2.5'/><circle cx='5' cy='19' r='2.5'/><circle cx='19' cy='19' r='2.5'/><path d='M11 7.2l-4.8 9.6M13 7.2l4.8 9.6M7.5 19h9'/>",
        ["barchart"] = "<path d='M5 20V12M10 20V6M15 20v-9M20 20V4'/>",
        ["usershield"] = "<circle cx='9' cy='8' r='3.2'/><path d='M3 20a6 6 0 0 1 9.5-4.9'/><path d='M17 12l4 1.5v3c0 2.5-1.8 4-4 4.8-2.2-.8-4-2.3-4-4.8v-3z'/>",
        ["translate"] = "<rect x='2' y='4' width='10' height='14' rx='1.5'/><rect x='12' y='6' width='10' height='14' rx='1.5'/><path d='M5 14l2-6 2 6M5.7 12h2.6M14.5 10h5M17 9v1M15 13c1.5 2.5 3 3.5 5 4M19.5 10c-.5 3-2 5.5-4.5 7'/>",
        ["ship"] = "<path d='M3 15l2 5h14l2-5z'/><path d='M6 15V9h12v6M9 9V5h6v4M12 5V3'/>",
        // biểu tượng đặc (thiết kế dùng icon đặc ở Key Features / bảng giá)
        ["users-s"] = "<g fill='currentColor' stroke='none'><circle cx='12' cy='6.5' r='3.3'/><circle cx='5.2' cy='8.2' r='2.5'/><circle cx='18.8' cy='8.2' r='2.5'/><path d='M6.5 20v-2.2a5.5 5.5 0 0 1 11 0V20z'/><path d='M1 19v-1.4A4 4 0 0 1 5.2 13.7c.9 0 1.7.2 2.4.7A7 7 0 0 0 5.2 19z'/><path d='M23 19v-1.4a4 4 0 0 0-4.2-3.9c-.9 0-1.7.2-2.4.7a7 7 0 0 1 2.4 4.6z'/></g>",
        ["bar-s"] = "<g fill='currentColor' stroke='none'><rect x='3' y='13' width='4.6' height='8' rx='1.4'/><rect x='9.7' y='8' width='4.6' height='13' rx='1.4'/><rect x='16.4' y='3' width='4.6' height='18' rx='1.4'/></g>",
        ["bolt"] ="<path d='M13 2L4 14h7l-1 8 9-12h-7z'/>",
    };

    /// <summary>Tên mọi icon (cho ô chọn icon ở Super Admin).</summary>
    public static IEnumerable<string> Names => P.Keys.Order();
    public static bool Has(string? name) => name != null && P.ContainsKey(name);

    public static IHtmlContent Svg(string name, int size = 24) =>
        new HtmlString($"<svg viewBox='0 0 24 24' width='{size}' height='{size}' fill='none' stroke='currentColor' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round' aria-hidden='true'>{(P.TryGetValue(name, out var v) ? v : "")}</svg>");
}
