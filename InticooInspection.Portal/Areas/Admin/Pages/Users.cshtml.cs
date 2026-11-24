using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class UsersModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly TenantAccess _tenant;
    private readonly AuditService _audit;
    public UsersModel(PortalDbContext db, TenantAccess tenant, AuditService audit) { _db = db; _tenant = tenant; _audit = audit; }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Role { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Company { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNo { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int Size { get; set; } = 10;
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Dt { get; set; }

    public List<CompanyUser> Rows { get; set; } = new();
    public PagerInfo Pager { get; set; } = new(1, 10, 0);
    public int TotalN, ActiveN, PendingN, InactiveN, NewThisMonth;
    public List<Company> Companies { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public CompanyUser? Sel { get; set; }
    public List<AuditLog> Activity { get; set; } = new();
    public List<PlatformModule> Modules { get; set; } = new();
    public HashSet<string> SelModules { get; set; } = new();

    private IQueryable<CompanyUser> Filter()
    {
        var q = _db.CompanyUsers.AsNoTracking().Include(u => u.Company).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) { var t = Q.Trim(); q = q.Where(u => u.FullName.Contains(t) || u.Email.Contains(t) || u.Company!.Name.Contains(t)); }
        if (!string.IsNullOrEmpty(Status)) q = q.Where(u => u.Status == Status);
        if (!string.IsNullOrEmpty(Role)) q = q.Where(u => u.Role == Role);
        if (Company != null) q = q.Where(u => u.CompanyId == Company);
        return q;
    }

    public async Task OnGetAsync() { PageNo = ClampPage(PageNo); Size = ClampSize(Size); await LoadAsync(); }

    private async Task LoadAsync()
    {
        var g = await _db.CompanyUsers.AsNoTracking().GroupBy(u => u.Status).Select(x => new { x.Key, N = x.Count() }).ToListAsync();
        TotalN = g.Sum(x => x.N); ActiveN = g.FirstOrDefault(x => x.Key == "Active")?.N ?? 0; PendingN = g.FirstOrDefault(x => x.Key == "Pending")?.N ?? 0; InactiveN = g.FirstOrDefault(x => x.Key == "Inactive")?.N ?? 0;
        var ms = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        NewThisMonth = await _db.CompanyUsers.CountAsync(u => u.CreatedAt >= ms);
        var q = Filter();
        Pager = new PagerInfo(PageNo, Size, await q.CountAsync());
        Rows = await q.OrderBy(u => u.Company!.Name).ThenBy(u => u.FullName).Skip((PageNo - 1) * Size).Take(Size).ToListAsync();
        Companies = await _db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        Roles = await _db.CompanyUsers.AsNoTracking().Select(u => u.Role).Distinct().OrderBy(x => x).ToListAsync();
        Modules = await _db.Modules.AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync();
        var selId = Id ?? Rows.FirstOrDefault()?.Id;
        if (selId != null)
        {
            Sel = await _db.CompanyUsers.AsNoTracking().Include(u => u.Company).FirstOrDefaultAsync(u => u.Id == selId);
            if (Sel != null)
            {
                Activity = await _db.AuditLogs.AsNoTracking().Where(a => a.Detail != null && a.Detail.Contains(Sel.Email)).OrderByDescending(a => a.At).Take(10).ToListAsync();
                SelModules = (await _db.CompanyModules.AsNoTracking().Where(m => m.CompanyId == Sel.CompanyId && m.Enabled).Select(m => m.ModuleCode).ToListAsync()).ToHashSet();
            }
        }
    }

    private async Task<(CompanyUser? u, Company? c)> Get(Guid id)
    {
        var u = await _db.CompanyUsers.Include(x => x.Company).FirstOrDefaultAsync(x => x.Id == id);
        return (u, u?.Company);
    }

    public async Task<IActionResult> OnPostToggleAsync(Guid id)
    {
        var (u, c) = await Get(id);
        if (u == null || c == null) return NotFound();
        var activate = u.Status != "Active";
        try { if (_tenant.HasDatabase(c)) await _tenant.SetActiveAsync(c, u.Email, activate); }
        catch (Exception ex) { SetError("Company database: " + ex.Message); return RedirectToPage(new { id }); }
        u.Status = activate ? "Active" : "Inactive";
        await _db.SaveChangesAsync();
        await _audit.LogAsync(activate ? "User.activate" : "User.deactivate", "CompanyUser", u.Id.ToString(), $"{u.Email} ({c.Name})");
        SetFlash($"{u.FullName} {(activate ? "activated" : "deactivated")}.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostResetAsync(Guid id)
    {
        var (u, c) = await Get(id);
        if (u == null || c == null) return NotFound();
        if (!_tenant.HasDatabase(c)) { SetError("This company has no database yet — nothing to reset."); return RedirectToPage(new { id }); }
        try
        {
            var temp = await _tenant.ResetPasswordAsync(c, u.Email);
            await _audit.LogAsync("User.resetPassword", "CompanyUser", u.Id.ToString(), $"{u.Email} ({c.Name})");
            // Mật khẩu tạm chỉ hiện một lần, không ghi vào log/audit
            SetFlash($"Temporary password for {u.Email}: {temp} — shown once. Ask the user to change it after signing in.");
        }
        catch (Exception ex) { SetError("Company database: " + ex.Message); }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostSyncAsync(Guid companyId)
    {
        var c = await _db.Companies.FindAsync(companyId);
        if (c == null) return NotFound();
        try { var n = await _tenant.SyncUsersAsync(c); SetFlash($"Synced {n} user(s) from {c.Name}."); }
        catch (Exception ex) { SetError("Sync failed: " + ex.Message); }
        return RedirectToPage(new { company = companyId });
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await Filter().OrderBy(u => u.Company!.Name).ThenBy(u => u.FullName).ToListAsync();
        await _audit.LogAsync("Export", "CompanyUser", null, $"{rows.Count} rows");
        return Export.Xlsx("Users", new[] { "Name", "Email", "Company", "Role", "Status", "Last Login", "Created" },
            rows.Select(u => new object?[] { u.FullName, u.Email, u.Company!.Name, u.Role, u.Status, u.LastLoginAt, u.CreatedAt }), $"users-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
