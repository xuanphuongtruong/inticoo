using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class ModulesModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly BillingService _billing;
    private readonly AuditService _audit;
    public ModulesModel(PortalDbContext db, BillingService billing, AuditService audit) { _db = db; _billing = billing; _audit = audit; }

    public List<PlatformModule> Modules { get; set; } = new();
    public List<Plan> Plans { get; set; } = new();
    public HashSet<(int plan, int module)> Matrix { get; set; } = new();
    public Dictionary<string, int> Usage { get; set; } = new();
    public int ActiveCompanies, CompaniesWithModules, ProCompanies;
    public double AvgUsage;
    public string? EditCode { get; set; }

    public async Task OnGetAsync(string? edit)
    {
        EditCode = edit;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Modules = await _db.Modules.AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync();
        Plans = await _db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        Matrix = (await _db.PlanModules.AsNoTracking().Select(pm => new { pm.PlanId, pm.ModuleId }).ToListAsync()).Select(x => (x.PlanId, x.ModuleId)).ToHashSet();
        var active = await _db.Companies.AsNoTracking().Where(c => c.Status == CompanyStatus.Active).Select(c => c.Id).ToListAsync();
        ActiveCompanies = active.Count;
        var cm = await _db.CompanyModules.AsNoTracking().Where(x => x.Enabled && active.Contains(x.CompanyId)).ToListAsync();
        Usage = Modules.ToDictionary(m => m.Code, m => cm.Count(x => x.ModuleCode == m.Code));
        CompaniesWithModules = cm.Select(x => x.CompanyId).Distinct().Count();
        AvgUsage = Modules.Count == 0 || ActiveCompanies == 0 ? 0 : Math.Round(Usage.Values.Average() * 100.0 / ActiveCompanies, 0);
        var proId = Plans.LastOrDefault()?.Id;
        ProCompanies = proId == null ? 0 : await _db.Subscriptions.CountAsync(s => s.PlanId == proId && s.Status != SubscriptionStatus.Cancelled);
    }

    public async Task<IActionResult> OnPostMatrixAsync(List<string>? cells)
    {
        var want = (cells ?? new()).Select(c => c.Split(':')).Where(p => p.Length == 2 && int.TryParse(p[0], out _) && int.TryParse(p[1], out _))
            .Select(p => (int.Parse(p[0]), int.Parse(p[1]))).ToHashSet();
        var plans = await _db.Plans.Select(p => p.Id).ToListAsync();
        var mods = await _db.Modules.Select(m => m.Id).ToListAsync();
        var have = await _db.PlanModules.ToListAsync();
        foreach (var pm in have.Where(h => !want.Contains((h.PlanId, h.ModuleId)))) _db.PlanModules.Remove(pm);
        foreach (var w in want.Where(w => plans.Contains(w.Item1) && mods.Contains(w.Item2) && !have.Any(h => h.PlanId == w.Item1 && h.ModuleId == w.Item2)))
            _db.PlanModules.Add(new PlanModule { PlanId = w.Item1, ModuleId = w.Item2 });
        await _db.SaveChangesAsync();
        // Công ty đang dùng gói nào thì đồng bộ lại module cho khớp gói
        foreach (var s in await _db.Subscriptions.Where(s => s.Status != SubscriptionStatus.Cancelled).ToListAsync())
            await _billing.SyncCompanyModulesAsync(s.CompanyId, s.PlanId);
        await _audit.LogAsync("Modules.matrix", "Plan", null, $"{want.Count} cells");
        SetFlash("Module access by plan saved. Company modules were re-synced.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync(string code, string name, string description)
    {
        var m = await _db.Modules.FirstOrDefaultAsync(x => x.Code == code);
        if (m == null || string.IsNullOrWhiteSpace(name)) return RedirectToPage();
        m.Name = name.Trim()[..Math.Min(100, name.Trim().Length)]; m.Description = (description ?? "").Trim()[..Math.Min(300, (description ?? "").Trim().Length)];
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Module.update", "Module", code, m.Name);
        SetFlash("Module updated.");
        return RedirectToPage();
    }
}
