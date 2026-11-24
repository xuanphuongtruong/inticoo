using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.SuperAdmin.Pages;

/// <summary>Tin nhắn từ form Contact Us trên website.</summary>
public class MessagesModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly AuditService _audit;
    public MessagesModel(PortalDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [BindProperty(SupportsGet = true)] public string? Filter { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNo { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int Size { get; set; } = 10;
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    public List<ContactMessage> Rows { get; set; } = new();
    public PagerInfo Pager { get; set; } = new(1, 10, 0);
    public ContactMessage? Sel { get; set; }
    public int UnreadN, TotalN;

    public async Task OnGetAsync()
    {
        PageNo = ClampPage(PageNo); Size = ClampSize(Size);
        TotalN = await _db.ContactMessages.CountAsync(); UnreadN = await _db.ContactMessages.CountAsync(m => !m.IsRead);
        var q = _db.ContactMessages.AsNoTracking().AsQueryable();
        if (Filter == "unread") q = q.Where(m => !m.IsRead);
        Pager = new PagerInfo(PageNo, Size, await q.CountAsync());
        Rows = await q.OrderByDescending(m => m.CreatedAt).Skip((PageNo - 1) * Size).Take(Size).ToListAsync();
        var id = Id ?? Rows.FirstOrDefault()?.Id;
        if (id != null)
        {
            var m = await _db.ContactMessages.FindAsync(id);
            if (m != null) { if (!m.IsRead) { m.IsRead = true; await _db.SaveChangesAsync(); UnreadN = Math.Max(0, UnreadN - 1); } Sel = m; }
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var m = await _db.ContactMessages.FindAsync(id);
        if (m != null) { _db.ContactMessages.Remove(m); await _db.SaveChangesAsync(); await _audit.LogAsync("Message.delete", "ContactMessage", id.ToString(), m.Email); SetFlash("Message deleted."); }
        return RedirectToPage();
    }
}
