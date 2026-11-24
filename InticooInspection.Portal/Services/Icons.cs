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
    };

    public static IHtmlContent Svg(string name, int size = 24) =>
        new HtmlString($"<svg viewBox='0 0 24 24' width='{size}' height='{size}' fill='none' stroke='currentColor' stroke-width='1.8' stroke-linecap='round' stroke-linejoin='round' aria-hidden='true'>{(P.TryGetValue(name, out var v) ? v : "")}</svg>");
}
