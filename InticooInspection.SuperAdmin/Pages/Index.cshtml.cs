using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InticooInspection.SuperAdmin.Pages;

public class IndexModel : PageModel
{
    private readonly ReportService _reports;
    private readonly BillingService _billing;
    private readonly UsageReportService _usage;
    public IndexModel(ReportService reports, BillingService billing, UsageReportService usage) { _reports = reports; _billing = billing; _usage = usage; }
    public DashboardData D { get; set; } = new();

    public async Task OnGetAsync()
    {
        await _billing.RefreshOverdueAsync();
        D = await _reports.DashboardAsync();
        var u = await _usage.PlatformAsync();
        D.Usage = u; D.StorageUsedGb = u.UsedGb; D.StorageCapacityGb = u.CapacityGb;
    }
}
