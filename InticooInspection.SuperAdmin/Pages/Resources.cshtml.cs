using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.SuperAdmin.Pages;

/// <summary>
/// Quản lý nội dung trang Resources của website: Featured Resources, Standards &amp; References (PDF),
/// Learning Center (bài viết/video), FAQ. File lưu ở thư mục chung Content:Root (ContentStore).
/// </summary>
[RequestSizeLimit(230_000_000)]
[RequestFormLimits(MultipartBodyLengthLimit = 230_000_000)]
public class ResourcesModel : AdminPageModel
{
    public static readonly string[] Langs = { "vi", "zh", "ms" };
    public static readonly Dictionary<string, string> SectionNames = new()
    {
        [ResourceSection.Featured] = "Featured Resources", [ResourceSection.Reference] = "Standards & References",
        [ResourceSection.Learning] = "Learning Center", [ResourceSection.Faq] = "FAQ"
    };
    public static readonly Dictionary<string, string[]> KindsBySection = new()
    {
        [ResourceSection.Featured] = new[] { "Guide", "Checklist", "Article", "Video" },
        [ResourceSection.Reference] = new[] { "Pdf" },
        [ResourceSection.Learning] = new[] { "Article", "Video" },
        [ResourceSection.Faq] = new[] { "Faq" },
    };
    public static readonly string[] IconChoices = { "grid", "alert", "doc", "box", "ruler", "book", "clipboard", "checkcircle", "shield", "chart", "container", "folder" };

    private readonly PortalDbContext _db;
    private readonly ContentStore _store;
    private readonly AuditService _audit;
    private readonly IConfiguration _cfg;
    public ResourcesModel(PortalDbContext db, ContentStore store, AuditService audit, IConfiguration cfg) { _db = db; _store = store; _audit = audit; _cfg = cfg; }

    [BindProperty(SupportsGet = true)] public string? Section { get; set; }
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Mode { get; set; }
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    public List<ResourceItem> Rows { get; set; } = new();
    public Dictionary<string, int> Counts { get; set; } = new();
    public ResourceItem? Item { get; set; }
    public string PublicUrl => (_cfg["Portal:PublicUrl"] ?? "").TrimEnd('/');

    public async Task OnGetAsync()
    {
        Counts = await _db.ResourceItems.GroupBy(r => r.Section).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var q = _db.ResourceItems.AsNoTracking().AsQueryable();
        if (Section != null && ResourceSection.All.Contains(Section)) q = q.Where(r => r.Section == Section);
        if (!string.IsNullOrWhiteSpace(Q)) q = q.Where(r => r.Title.Contains(Q) || (r.Summary != null && r.Summary.Contains(Q)));
        // thứ tự mục giống trang website: Featured → References → Learning → FAQ
        Rows = (await q.ToListAsync()).OrderBy(r => Array.IndexOf(ResourceSection.All, r.Section)).ThenBy(r => r.SortOrder).ThenByDescending(r => r.PublishedAt).ToList();
        if (Mode == "edit" && Id != null)
            Item = await _db.ResourceItems.AsNoTracking().Include(r => r.Translations).FirstOrDefaultAsync(r => r.Id == Id);
        if (Mode == "add")
        {
            var sec = Section != null && ResourceSection.All.Contains(Section) ? Section : ResourceSection.Learning;
            Item = new ResourceItem { Section = sec, Kind = KindsBySection[sec][0], SortOrder = (Rows.Where(r => r.Section == sec).Select(r => r.SortOrder).DefaultIfEmpty(0).Max() + 10) };
        }
    }

    public async Task<IActionResult> OnPostSaveAsync(int? id, string section, string kind, string title, string? slug, string? summary, string? body,
        string? buttonText, string? icon, int sortOrder, bool published, DateTime? publishedAt, string? videoUrl,
        IFormFile? cover, IFormFile? file, IFormFile? video, bool removeCover, bool removeFile, bool removeVideo)
    {
        section = ResourceSection.All.Contains(section) ? section : ResourceSection.Learning;
        if (!KindsBySection[section].Contains(kind)) kind = KindsBySection[section][0];
        title = (title ?? "").Trim();
        object back = id == null ? new { mode = "add", section } : new { mode = "edit", id };
        if (title.Length == 0 || title.Length > 200) { SetError("Title is required (max 200 characters)."); return RedirectToPage(back); }

        var r = id == null ? new ResourceItem { CreatedAt = DateTime.UtcNow }
                           : await _db.ResourceItems.Include(x => x.Translations).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();

        // slug: tự sinh từ tiêu đề nếu để trống; bảo đảm không trùng
        var baseSlug = ResourceContent.Slugify(string.IsNullOrWhiteSpace(slug) ? title : slug);
        if (baseSlug == "articles") baseSlug = "articles-1";   // /resources/articles là trang danh sách bài viết
        var s = baseSlug; var n = 2;
        while (await _db.ResourceItems.AnyAsync(x => x.Slug == s && x.Id != r.Id)) s = $"{baseSlug}-{n++}";

        videoUrl = string.IsNullOrWhiteSpace(videoUrl) ? null : videoUrl.Trim();
        if (videoUrl != null && !videoUrl.StartsWith(ContentStore.UrlPrefix) && ResourceContent.Embed(videoUrl) == null)
        { SetError("Video link must be a YouTube or Vimeo address (or upload an .mp4 file)."); return RedirectToPage(back); }

        // file mới: lưu trước, lỗi thì dừng (không đổi dữ liệu)
        string? newCover = null, newFile = null, newVideo = null;
        if (cover is { Length: > 0 }) { var (u, e) = await _store.SaveAsync(cover, ContentStore.Kind.Image); if (e != null) { SetError("Cover image: " + e); return RedirectToPage(back); } newCover = u; }
        if (file is { Length: > 0 }) { var (u, e) = await _store.SaveAsync(file, ContentStore.Kind.Pdf); if (e != null) { _store.Delete(newCover); SetError("PDF: " + e); return RedirectToPage(back); } newFile = u; }
        if (video is { Length: > 0 }) { var (u, e) = await _store.SaveAsync(video, ContentStore.Kind.Video); if (e != null) { _store.Delete(newCover); _store.Delete(newFile); SetError("Video: " + e); return RedirectToPage(back); } newVideo = u; }

        if (newCover != null || removeCover) { _store.Delete(r.CoverPath); r.CoverPath = newCover; }
        if (newFile != null || removeFile)
        {
            _store.Delete(r.FilePath);
            r.FilePath = newFile;
            r.FileName = newFile != null ? SafeName(file!.FileName) : null;
        }
        // Video: file .mp4 mới > bỏ video > link YouTube/Vimeo nhập vào > giữ file .mp4 cũ (ô link chỉ chứa link ngoài)
        var oldVideo = r.VideoUrl;
        var target = newVideo ?? (removeVideo ? null : videoUrl ?? (ResourceContent.IsUploadedVideo(oldVideo) ? oldVideo : null));
        if (ResourceContent.IsUploadedVideo(oldVideo) && oldVideo != target) _store.Delete(oldVideo);
        r.VideoUrl = target;

        r.Section = section; r.Kind = kind; r.Title = title; r.Slug = s;
        r.Summary = Trim(summary, 500); r.Body = ResourceContent.Clean(body); r.ButtonText = Trim(buttonText, 60);
        r.Icon = IconChoices.Contains(icon) ? icon : null; r.SortOrder = sortOrder; r.IsPublished = published;
        // Ô ngày chỉ có ngày: giữ nguyên giờ cũ nếu không đổi ngày; bài mới / chọn hôm nay → giờ hiện tại (để bài mới lên đầu)
        if (publishedAt is { } pd)
        {
            var cur = id == null ? DateTime.UtcNow : r.PublishedAt;
            if (pd.Date != cur.ToLocalTime().Date)
                r.PublishedAt = pd.Date == DateTime.Now.Date ? DateTime.UtcNow : pd.Date.ToUniversalTime();
            else r.PublishedAt = cur;
        }
        r.UpdatedAt = DateTime.UtcNow;

        // bản dịch tùy chọn: tr.{lang}.title / summary / button / body
        foreach (var l in Langs)
        {
            string? F(string k) => Request.Form[$"tr.{l}.{k}"].FirstOrDefault();
            var tt = Trim(F("title"), 200); var ts = Trim(F("summary"), 500); var tb = ResourceContent.Clean(F("body")); var tbt = Trim(F("button"), 60);
            var tr = r.Translations.FirstOrDefault(x => x.Lang == l);
            if (tt == null && ts == null && tb == null && tbt == null) { if (tr != null) _db.ResourceTranslations.Remove(tr); continue; }
            if (tr == null) { tr = new ResourceTranslation { Lang = l }; r.Translations.Add(tr); }
            tr.Title = tt; tr.Summary = ts; tr.Body = tb; tr.ButtonText = tbt;
        }

        if (id == null) _db.ResourceItems.Add(r);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(id == null ? "Resource.create" : "Resource.update", "ResourceItem", r.Id.ToString(), $"{r.Section}: {r.Title}");
        SetFlash(id == null ? "Resource created." : "Resource saved.");
        return RedirectToPage(new { mode = "edit", id = r.Id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var r = await _db.ResourceItems.Include(x => x.Translations).FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return RedirectToPage();
        foreach (var p in new[] { r.CoverPath, r.FilePath, r.VideoUrl }) _store.Delete(p);
        foreach (var img in System.Text.RegularExpressions.Regex.Matches((r.Body ?? "") + string.Join("", r.Translations.Select(t => t.Body)), "src=\"(/content/[^\"]+)\"").Select(m => m.Groups[1].Value))
            _store.Delete(img);
        _db.ResourceItems.Remove(r);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Resource.delete", "ResourceItem", id.ToString(), r.Title);
        SetFlash("Resource deleted.");
        return RedirectToPage(new { section = r.Section });
    }

    public async Task<IActionResult> OnPostPublishAsync(int id, bool on)
    {
        var r = await _db.ResourceItems.FindAsync(id);
        if (r != null) { r.IsPublished = on; r.UpdatedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); await _audit.LogAsync(on ? "Resource.publish" : "Resource.unpublish", "ResourceItem", id.ToString(), r.Title); }
        return RedirectToPage(new { section = Section, q = Q });
    }

    /// <summary>Ảnh chèn trong trình soạn thảo (AJAX) → {url} hoặc {error}.</summary>
    public async Task<IActionResult> OnPostImageAsync(IFormFile? image)
    {
        var (u, e) = await _store.SaveAsync(image, ContentStore.Kind.Image);
        if (e != null) return new JsonResult(new { error = e }) { StatusCode = 400 };
        await _audit.LogAsync("Resource.image", "ResourceItem", null, u);
        return new JsonResult(new { url = u });
    }

    private static string? Trim(string? v, int max) => string.IsNullOrWhiteSpace(v) ? null : (v.Trim().Length > max ? v.Trim()[..max] : v.Trim());
    private static string SafeName(string n)
    {
        var name = Path.GetFileName(n);
        name = new string(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c) && c != '"').ToArray());
        return name.Length > 150 ? name[^150..] : name;
    }
}
