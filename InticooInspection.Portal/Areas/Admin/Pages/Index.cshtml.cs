using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class IndexModel : PageModel
{
    private readonly ReportService _reports;
    private readonly BillingService _billing;
    public IndexModel(ReportService reports, BillingService billing) { _reports = reports; _billing = billing; }
    public DashboardData D { get; set; } = new();

    public async Task OnGetAsync()
    {
        await _billing.RefreshOverdueAsync();
        D = await _reports.DashboardAsync();
    }
}
