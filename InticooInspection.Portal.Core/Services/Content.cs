using System.Text;
using System.Text.RegularExpressions;
using Ganss.Xss;
using InticooInspection.Portal.Data;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace InticooInspection.Portal.Services;

/// <summary>
/// File nội dung trang Resources (ảnh bìa, PDF, video mp4, ảnh chèn trong bài) — lưu ở THƯ MỤC CHUNG
/// <c>Content:Root</c> mà cả Website lẫn Super Admin cùng đọc (Super Admin ghi, Website phục vụ tại <c>/content/...</c>).
/// Bỏ trống <c>Content:Root</c> → thư mục <c>InticooContent</c> cạnh thư mục project/site (máy dev: chung cho 2 app).
/// </summary>
public class ContentStore
{
    public const string UrlPrefix = "/content/";
    public enum Kind { Image, Pdf, Video }

    private static readonly Dictionary<Kind, (string[] Ext, long Max)> Rules = new()
    {
        [Kind.Image] = (new[] { ".jpg", ".jpeg", ".png", ".webp" }, 5L * 1024 * 1024),
        [Kind.Pdf] = (new[] { ".pdf" }, 30L * 1024 * 1024),
        [Kind.Video] = (new[] { ".mp4" }, 200L * 1024 * 1024),
    };
    public static long MaxBytes(Kind k) => Rules[k].Max;
    public static string Accept(Kind k) => string.Join(",", Rules[k].Ext);

    public string Root { get; }

    public ContentStore(IConfiguration cfg, IWebHostEnvironment env)
    {
        var r = cfg["Content:Root"];
        Root = Path.GetFullPath(string.IsNullOrWhiteSpace(r) ? Path.Combine(env.ContentRootPath, "..", "InticooContent") : r);
        Directory.CreateDirectory(Root);
    }

    /// <summary>Lưu file upload sau khi kiểm tra đuôi, dung lượng và chữ ký đầu file. Trả về (url, lỗi).</summary>
    public async Task<(string? Url, string? Error)> SaveAsync(IFormFile? file, Kind kind)
    {
        if (file == null || file.Length == 0) return (null, "No file selected.");
        var (exts, max) = Rules[kind];
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!exts.Contains(ext)) return (null, $"Only {string.Join(", ", exts)} files are allowed.");
        if (file.Length > max) return (null, $"File is too large (max {max / 1024 / 1024} MB).");

        var head = new byte[12];
        await using (var s = file.OpenReadStream()) { _ = await s.ReadAsync(head); }
        if (!MagicOk(ext, head)) return (null, "The file content does not match its type.");

        var sub = DateTime.UtcNow.ToString("yyyy/MM");
        var name = Guid.NewGuid().ToString("N") + (ext == ".jpeg" ? ".jpg" : ext);
        var dir = Path.Combine(Root, sub.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(dir);
        await using (var fs = File.Create(Path.Combine(dir, name))) await file.CopyToAsync(fs);
        return (UrlPrefix + sub + "/" + name, null);
    }

    /// <summary>Xóa file đã upload (chỉ đường dẫn /content/... nằm trong Root; ảnh mặc định /img/... bỏ qua).</summary>
    public void Delete(string? url)
    {
        var p = PathOf(url);
        if (p != null && File.Exists(p)) File.Delete(p);
    }

    public string? PathOf(string? url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith(UrlPrefix, StringComparison.Ordinal)) return null;
        var full = Path.GetFullPath(Path.Combine(Root, url[UrlPrefix.Length..].Replace('/', Path.DirectorySeparatorChar)));
        return full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ? full : null;
    }

    private static bool MagicOk(string ext, byte[] h) => ext switch
    {
        ".jpg" or ".jpeg" => h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF,
        ".png" => h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47,
        ".webp" => Encoding.ASCII.GetString(h, 0, 4) == "RIFF" && Encoding.ASCII.GetString(h, 8, 4) == "WEBP",
        ".pdf" => Encoding.ASCII.GetString(h, 0, 4) == "%PDF",
        ".mp4" => Encoding.ASCII.GetString(h, 4, 4) == "ftyp",
        _ => false
    };

    /// <summary>Phục vụ file đã upload tại /content/... (chỉ các loại cho phép, cache 7 ngày vì tên file là GUID).</summary>
    public static void UseContentFiles(WebApplication app)
    {
        var store = app.Services.GetRequiredService<ContentStore>();
        var types = new FileExtensionContentTypeProvider(new Dictionary<string, string>
        {
            [".jpg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp", [".pdf"] = "application/pdf", [".mp4"] = "video/mp4"
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(store.Root),
            RequestPath = UrlPrefix.TrimEnd('/'),
            ContentTypeProvider = types,
            ServeUnknownFileTypes = false,
            OnPrepareResponse = c => c.Context.Response.Headers.CacheControl = "public,max-age=604800"
        });
    }
}

/// <summary>Xử lý nội dung bài viết: lọc HTML từ trình soạn thảo, tạo slug, nhúng video.</summary>
public static class ResourceContent
{
    private static readonly HtmlSanitizer San = Build();

    private static HtmlSanitizer Build()
    {
        var s = new HtmlSanitizer();
        s.AllowedTags.Clear();
        foreach (var t in new[] { "p", "br", "h2", "h3", "h4", "strong", "b", "em", "i", "u", "s", "ul", "ol", "li", "a", "img", "blockquote", "hr",
                                   "table", "thead", "tbody", "tr", "th", "td", "figure", "figcaption", "span", "div" })
            s.AllowedTags.Add(t);
        s.AllowedAttributes.Clear();
        foreach (var a in new[] { "href", "src", "alt", "title", "colspan", "rowspan" }) s.AllowedAttributes.Add(a);
        s.AllowedCssProperties.Clear();
        s.AllowedSchemes.Clear();
        foreach (var sc in new[] { "http", "https", "mailto" }) s.AllowedSchemes.Add(sc);
        s.AllowDataAttributes = false;
        // Ảnh trong bài chỉ được lấy từ file đã upload (/content/...) hoặc ảnh của site (/img/...)
        s.PostProcessNode += (_, e) =>
        {
            if (e.Node is AngleSharp.Dom.IElement el)
            {
                if (el.LocalName == "img")
                {
                    var src = el.GetAttribute("src") ?? "";
                    if (!(src.StartsWith(ContentStore.UrlPrefix) || src.StartsWith("/img/"))) el.Remove();
                    else el.SetAttribute("loading", "lazy");
                }
                if (el.LocalName == "a" && (el.GetAttribute("href") ?? "").StartsWith("http")) el.SetAttribute("rel", "noopener nofollow");
            }
        };
        s.AllowedAttributes.Add("loading"); s.AllowedAttributes.Add("rel");
        return s;
    }

    public static string? Clean(string? html) =>
        string.IsNullOrWhiteSpace(html) ? null : San.Sanitize(html).Trim() is { Length: > 0 } c && c != "<p><br></p>" ? c : null;

    public static string Slugify(string title)
    {
        var norm = title.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in norm)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (c is 'đ' or 'Đ') { sb.Append('d'); continue; }
            if (char.IsAsciiLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[^1] != '-') sb.Append('-');
        }
        var s = sb.ToString().Trim('-');
        if (s.Length > 120) s = s[..120].TrimEnd('-');
        return s.Length == 0 ? "resource" : s;
    }

    /// <summary>Link YouTube/Vimeo → địa chỉ nhúng (iframe). Không nhận ra → null.</summary>
    public static string? Embed(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var m = Regex.Match(url, @"(?:youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/)|youtu\.be/)([A-Za-z0-9_-]{11})");
        if (m.Success) return "https://www.youtube-nocookie.com/embed/" + m.Groups[1].Value;
        m = Regex.Match(url, @"vimeo\.com/(?:video/)?(\d+)");
        return m.Success ? "https://player.vimeo.com/video/" + m.Groups[1].Value : null;
    }

    public static bool IsUploadedVideo(string? url) => url != null && url.StartsWith(ContentStore.UrlPrefix) && url.EndsWith(".mp4");

    /// <summary>Lấy bản dịch theo ngôn ngữ, ô nào trống thì dùng tiếng Anh.</summary>
    public static (string Title, string? Summary, string? Body, string? Button) Localized(ResourceItem r, string lang)
    {
        var t = lang == "en" ? null : r.Translations.FirstOrDefault(x => x.Lang == lang);
        return (Pick(t?.Title) ?? r.Title, Pick(t?.Summary) ?? r.Summary, Pick(t?.Body) ?? r.Body, Pick(t?.ButtonText) ?? r.ButtonText);
        static string? Pick(string? v) => string.IsNullOrWhiteSpace(v) ? null : v;
    }
}
