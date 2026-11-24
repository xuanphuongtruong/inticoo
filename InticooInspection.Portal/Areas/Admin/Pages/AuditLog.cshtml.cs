using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class AuditLogModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    public AuditLogModel(PortalDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Entity { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNo { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int Size { get; set; } = 25;
    public List<AuditLog> Rows { get; set; } = new();
    public PagerInfo Pager { get; set; } = new(1, 25, 0);
    public List<string> Entities { get; set; } = new();

    private IQueryable<AuditLog> Filter()
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) { var t = Q.Trim(); q = q.Where(a => a.Actor.Contains(t) || a.Action.Contains(t) || (a.Detail != null && a.Detail.Contains(t)) || (a.EntityId != null && a.EntityId.Contains(t))); }
        if (!string.IsNullOrEmpty(Entity)) q = q.Where(a => a.Entity == Entity);
        if (From != null) q = q.Where(a => a.At >= From);
        if (To != null) q = q.Where(a => a.At < To.Value.Date.AddDays(1));
        return q;
    }

    public async Task OnGetAsync()
    {
        PageNo = ClampPage(PageNo); Size = ClampSize(Size);
        var q = Filter();
        Pager = new PagerInfo(PageNo, Size, await q.CountAsync());
        Rows = await q.OrderByDescending(a => a.Id).Skip((PageNo - 1) * Size).Take(Size).ToListAsync();
        Entities = await _db.AuditLogs.AsNoTracking().Select(a => a.Entity).Distinct().OrderBy(x => x).ToListAsync();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await Filter().OrderByDescending(a => a.Id).Take(20000).ToListAsync();
        return Export.Xlsx("Audit Log", new[] { "Time (UTC)", "Actor", "Action", "Entity", "Entity ID", "Detail", "IP" },
            rows.Select(a => new object?[] { a.At, a.Actor, a.Action, a.Entity, a.EntityId, a.Detail, a.IpAddress }), $"audit-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
