using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace InticooInspection.Portal.Services;

/// <summary>
/// Đa ngôn ngữ cho website công khai (en/vi/zh/ms). Khóa = chính câu tiếng Anh; en không cần file,
/// các ngôn ngữ khác tra trong Localization/{lang}.json, thiếu thì rơi về tiếng Anh.
/// </summary>
public class LocalizationStore
{
    public static readonly string[] Supported = { "en", "zh", "vi", "ms" };
    public static readonly Dictionary<string, (string Native, string English, string Flag)> Meta = new()
    {
        ["en"] = ("English", "English", "EN"), ["zh"] = ("中文", "Chinese", "ZH"),
        ["vi"] = ("Tiếng Việt", "Vietnamese", "VI"), ["ms"] = ("Bahasa Melayu", "Malay", "MS")
    };
    private readonly Dictionary<string, Dictionary<string, string>> _data = new();

    public LocalizationStore(IWebHostEnvironment env, ILogger<LocalizationStore> log)
    {
        foreach (var l in Supported.Where(x => x != "en"))
        {
            var path = Path.Combine(env.ContentRootPath, "Localization", l + ".json");
            if (!File.Exists(path)) { log.LogWarning("Thiếu file dịch {Path}", path); continue; }
            _data[l] = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new();
        }
    }

    public string Get(string lang, string key) =>
        lang != "en" && _data.TryGetValue(lang, out var d) && d.TryGetValue(key, out var v) ? v : key;
}

public class Loc
{
    private readonly LocalizationStore _store;
    public string Lang { get; }

    public Loc(LocalizationStore store, IHttpContextAccessor http)
    {
        _store = store;
        var ctx = http.HttpContext;
        var l = ctx?.Request.Query["lang"].ToString();
        if (string.IsNullOrEmpty(l)) l = ctx?.Request.Cookies["lang"];
        if (string.IsNullOrEmpty(l))
        {
            var al = ctx?.Request.Headers.AcceptLanguage.ToString().ToLowerInvariant() ?? "";
            l = al.StartsWith("vi") ? "vi" : al.StartsWith("zh") ? "zh" : al.StartsWith("ms") ? "ms" : "en";
        }
        Lang = LocalizationStore.Supported.Contains(l) ? l : "en";
    }

    public string this[string key] => _store.Get(Lang, key);
    public string F(string key, params object[] args) => string.Format(_store.Get(Lang, key), args);

    /// <summary>Ngày theo ngôn ngữ đang xem (vd. 22 Nov 2026 / 22 thg 11, 2026 / 2026年11月22日).</summary>
    public string Date(DateTime d)
    {
        var ci = System.Globalization.CultureInfo.GetCultureInfo(Lang switch { "vi" => "vi-VN", "zh" => "zh-CN", "ms" => "ms-MY", _ => "en-GB" });
        return d.ToString(Lang == "zh" ? "yyyy年M月d日" : "d MMM yyyy", ci);
    }
}

public static class ModelStateLocalization
{
    /// <summary>Dịch thông báo lỗi (khóa = câu tiếng Anh trong ErrorMessage). Lỗi ép kiểu mặc định của ASP.NET
    /// ("The value 'x' is not valid...") có chứa tên thuộc tính → thay bằng câu chung.</summary>
    public static void Localize(this ModelStateDictionary ms, Loc l)
    {
        foreach (var (key, entry) in ms.ToList())
        {
            if (entry.Errors.Count == 0) continue;
            var msgs = entry.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) || e.ErrorMessage.StartsWith("The ") ? "Please check the highlighted information." : e.ErrorMessage)
                                   .Take(1).ToList();   // 1 lỗi/ô là đủ (ô trống vừa Required vừa EmailAddress)
            entry.Errors.Clear();
            foreach (var m in msgs) entry.Errors.Add(l[m]);
        }
    }
}
