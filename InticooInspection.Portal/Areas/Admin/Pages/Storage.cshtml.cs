using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class StorageModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly ProvisioningService _prov;
    public StorageModel(PortalDbContext db, ProvisioningService prov) { _db = db; _prov = prov; }

    public record Row(Company C, string PlanName, int QuotaGb);
    public List<Row> Rows { get; set; } = new();
    public double TotalUsed, TotalQuota;
    public int Over80, Provisioned;

    public async Task OnGetAsync()
    {
        var list = await _db.Companies.AsNoTracking().Where(c => c.Status != CompanyStatus.Cancelled)
            .Select(c => new { C = c, Sub = c.Subscriptions.OrderByDescending(s => s.Id).Select(s => new { s.Plan!.Name, s.Plan.StorageGb }).FirstOrDefault() })
            .OrderByDescending(x => x.C.StorageUsedGb).ToListAsync();
        Rows = list.Select(x => new Row(x.C, x.Sub?.Name ?? "-", x.Sub?.StorageGb ?? 0)).ToList();
        TotalUsed = Rows.Sum(r => r.C.StorageUsedGb); TotalQuota = Rows.Sum(r => r.QuotaGb);
        Over80 = Rows.Count(r => r.QuotaGb > 0 && r.C.StorageUsedGb / r.QuotaGb >= .8);
        Provisioned = Rows.Count(r => r.C.ProvisionStatus == ProvisionStatus.Provisioned);
    }

    public async Task<IActionResult> OnPostRefreshAsync()
    {
        var n = 0;
        foreach (var c in await _db.Companies.Where(c => c.ProvisionStatus == ProvisionStatus.Provisioned).ToListAsync())
            if (await _prov.RefreshStorageAsync(c) != null) n++;
        SetFlash($"Measured storage for {n} company database(s). Companies without their own database keep the last known value.");
        return RedirectToPage();
    }
}
