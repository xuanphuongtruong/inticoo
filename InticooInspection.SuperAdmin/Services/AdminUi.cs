using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using InticooInspection.Portal.Data;

namespace InticooInspection.SuperAdmin.Services;

public record PagerInfo(int Page, int PageSize, int Total)
{
    public int Pages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
    public int From => Total == 0 ? 0 : (Page - 1) * PageSize + 1;
    public int To => Math.Min(Total, Page * PageSize);
}

public static class AdminUi
{
    private static readonly string[] Palette = { "#2563eb", "#7c3aed", "#0891b2", "#ea580c", "#059669", "#db2777", "#4f46e5", "#ca8a04" };
    public static string Color(string seed) => Palette[Math.Abs(seed.GetHashCode(StringComparison.Ordinal) % Palette.Length)];
    /// <summary>Mũi tên ▲/▼ % so với kỳ trước (KPI). Kỳ trước = 0 → không hiện (không chia được).</summary>
    public static IHtmlContent Delta(decimal cur, decimal prev)
    {
        if (prev == 0) return HtmlString.Empty;
        var pct = (double)((cur - prev) / prev * 100);
        return new HtmlString($"<span class='d {(pct >= 0 ? "up" : "down")}'>{(pct >= 0 ? "▲" : "▼")} {Math.Abs(pct).ToString("0", CultureInfo.InvariantCulture)}%</span>");
    }
    /// <summary>Đầu tháng hiện tại (UTC) và đầu tháng trước — cho KPI "this month / last month".</summary>
    public static (DateTime ThisMonth, DateTime LastMonth) MonthStarts()
    {
        var n = DateTime.UtcNow; var ms = new DateTime(n.Year, n.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (ms, ms.AddMonths(-1));
    }
    public static string Initial(string name) => string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[0].ToString().ToUpperInvariant();
    public static string Initials(string name)
    {
        var p = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return p.Length == 0 ? "?" : p.Length == 1 ? p[0][..1].ToUpperInvariant() : (p[0][..1] + p[^1][..1]).ToUpperInvariant();
    }

    public static string PillClass(string status) => status switch
    {
        "Active" or "Paid" or "Succeeded" or "Provisioned" => "green",
        "Pending" or "PendingPayment" or "Pending Payment" => "amber",
        "PendingReview" or "Pending Review" => "blue",
        "Overdue" or "Failed" => "red",
        "Cancelled" or "Paused" or "Inactive" or "None" => "grey",
        "Refunded" => "purple",
        _ => "grey"
    };

    public static string StatusLabel(string status) => status switch
    {
        "PendingPayment" => "Pending Payment",
        "PendingReview" => "Pending Review",
        _ => status
    };

    public static IHtmlContent Pill(string status) =>
        new HtmlString($"<span class='pill {PillClass(status)}'>{System.Net.WebUtility.HtmlEncode(StatusLabel(status))}</span>");

    /// <summary>Cờ quốc gia (wwwroot/img/flags/{mã ISO2}.svg; mã lạ → xx.svg).</summary>
    public static IHtmlContent Flag(string? code)
    {
        var c = (code ?? "").Trim().ToLowerInvariant();
        if (c.Length != 2 || !c.All(char.IsAsciiLetterLower) || !Known.Contains(c)) c = "xx";
        return new HtmlString($"<img class='flag' src='/img/flags/{c}.svg' alt='' />");
    }
    private static readonly HashSet<string> Known = new(Portal.Services.CountryList.All.Select(x => x.Code.ToLowerInvariant()).Append("gb").Append("eu"));

    public static string Money(decimal v) => "$" + v.ToString("N2", CultureInfo.InvariantCulture);
    public static string Money0(decimal v) => "$" + v.ToString("N0", CultureInfo.InvariantCulture);
    public static string D(DateTime? d) => d?.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "-";
    public static string DT(DateTime? d) => d?.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) ?? "-";
    public static string Num(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    public static string Pct(double v) => v.ToString("0.#", CultureInfo.InvariantCulture) + "%";

    /// <summary>Giữ nguyên query hiện tại, ghi đè/xóa các khóa chỉ định (giá trị null/rỗng = xóa).</summary>
    public static string Qs(HttpRequest req, params (string Key, string? Val)[] set)
    {
        var d = req.Query.ToDictionary(k => k.Key, k => k.Value.ToString());
        foreach (var (k, v) in set) { if (string.IsNullOrEmpty(v)) d.Remove(k); else d[k] = v; }
        return d.Count == 0 ? "" : "?" + string.Join("&", d.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public static string Slice(IEnumerable<(double Value, string Color)> parts)
    {
        var list = parts.ToList();
        var total = list.Sum(p => p.Value);
        if (total <= 0) return "background:#e5e7eb";
        double acc = 0; var segs = new List<string>();
        foreach (var (v, c) in list)
        {
            var a = acc / total * 360; acc += v; var b = acc / total * 360;
            segs.Add($"{c} {a.ToString("0.##", CultureInfo.InvariantCulture)}deg {b.ToString("0.##", CultureInfo.InvariantCulture)}deg");
        }
        return "background:conic-gradient(" + string.Join(",", segs) + ")";
    }

    public static string LinePoints(IList<decimal> vals, int w = 400, int h = 150, int pad = 12)
    {
        if (vals.Count == 0) return "";
        var max = Math.Max(1m, vals.Max());
        var pts = vals.Select((v, i) =>
        {
            var x = pad + (vals.Count == 1 ? 0 : i * (w - 2 * pad) / (double)(vals.Count - 1));
            var y = h - pad - (double)(v / max) * (h - 2 * pad);
            return $"{x.ToString("0.#", CultureInfo.InvariantCulture)},{y.ToString("0.#", CultureInfo.InvariantCulture)}";
        });
        return string.Join(" ", pts);
    }
}

public abstract class AdminPageModel : PageModel
{
    public static readonly int[] PageSizes = { 10, 25, 50 };
    protected void SetFlash(string msg) => TempData["flash"] = msg;
    protected void SetError(string msg) => TempData["flashErr"] = msg;
    public string ActorEmail => User.Identity?.Name ?? "";
    public static int ClampPage(int p) => Math.Max(1, p);
    public static int ClampSize(int s) => PageSizes.Contains(s) ? s : 10;
}
