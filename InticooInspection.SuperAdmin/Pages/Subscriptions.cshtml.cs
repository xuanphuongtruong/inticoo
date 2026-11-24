using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.SuperAdmin.Pages;

public class SubscriptionsModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly BillingService _billing;
    private readonly AuditService _audit;
    public SubscriptionsModel(PortalDbContext db, BillingService billing, AuditService audit) { _db = db; _billing = billing; _audit = audit; }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Plan { get; set; }
    [BindProperty(SupportsGet = true)] public string? Cycle { get; set; }
    [BindProperty(SupportsGet = true)] public string? Country { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNo { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int Size { get; set; } = 10;
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Dt { get; set; }
    [BindProperty(SupportsGet = true)] public string? Mode { get; set; }

    public List<Subscription> Rows { get; set; } = new();
    public PagerInfo Pager { get; set; } = new(1, 10, 0);
    public int All, ActiveN, PendingN, OverdueN, CancelledN;
    public List<Plan> Plans { get; set; } = new();
    public List<string> Countries { get; set; } = new();
    public Subscription? Sel { get; set; }
    public List<Invoice> SelInvoices { get; set; } = new();
    public List<string> SelModules { get; set; } = new();

    private IQueryable<Subscription> Filter()
    {
        var q = _db.Subscriptions.AsNoTracking().Include(s => s.Company).Include(s => s.Plan).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) { var t = Q.Trim(); q = q.Where(s => s.Company!.Name.Contains(t) || s.Company.Email.Contains(t) || s.Plan!.Name.Contains(t)); }
        if (!string.IsNullOrEmpty(Plan)) q = q.Where(s => s.Plan!.Code == Plan);
        if (!string.IsNullOrEmpty(Cycle)) q = q.Where(s => s.BillingCycle == Cycle);
        if (!string.IsNullOrEmpty(Country)) q = q.Where(s => s.Company!.Country == Country);
        if (Status == "Pending") q = q.Where(s => s.Status == SubscriptionStatus.PendingPayment || s.Status == SubscriptionStatus.PendingReview);
        else if (!string.IsNullOrEmpty(Status)) q = q.Where(s => s.Status == Status);
        return q;
    }

    public async Task OnGetAsync() { PageNo = ClampPage(PageNo); Size = ClampSize(Size); await LoadAsync(); }

    private async Task LoadAsync()
    {
        var counts = await _db.Subscriptions.AsNoTracking().GroupBy(s => s.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        int N(string k) => counts.FirstOrDefault(x => x.Key == k)?.N ?? 0;
        All = counts.Sum(x => x.N); ActiveN = N("Active"); OverdueN = N("Overdue"); CancelledN = N("Cancelled"); PendingN = N("PendingPayment") + N("PendingReview");
        var q = Filter();
        Pager = new PagerInfo(PageNo, Size, await q.CountAsync());
        Rows = await q.OrderByDescending(s => s.StartDate).ThenByDescending(s => s.Id).Skip((PageNo - 1) * Size).Take(Size).ToListAsync();
        Plans = await _db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        Countries = await _db.Companies.AsNoTracking().Select(c => c.Country).Distinct().OrderBy(x => x).ToListAsync();
        var selId = Id ?? Rows.FirstOrDefault()?.Id;
        if (selId != null)
        {
            Sel = await _db.Subscriptions.AsNoTracking().Include(s => s.Company).Include(s => s.Plan).FirstOrDefaultAsync(s => s.Id == selId);
            if (Sel != null)
            {
                SelInvoices = await _db.Invoices.AsNoTracking().Where(i => i.SubscriptionId == Sel.Id).OrderByDescending(i => i.InvoiceDate).Take(10).ToListAsync();
                SelModules = await _db.CompanyModules.AsNoTracking().Where(m => m.CompanyId == Sel.CompanyId && m.Enabled).Select(m => m.ModuleCode).ToListAsync();
            }
        }
    }

    public async Task<IActionResult> OnPostAutoRenewAsync(int id)
    {
        var s = await _db.Subscriptions.FindAsync(id);
        if (s == null) return NotFound();
        s.AutoRenew = !s.AutoRenew;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Subscription.autorenew", "Subscription", id.ToString(), s.AutoRenew ? "on" : "off");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostChangePlanAsync(int id, string planCode, string cycle)
    {
        var s = await _db.Subscriptions.FindAsync(id);
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive);
        if (s == null || plan == null || cycle is not ("Monthly" or "Annual")) { SetError("Invalid plan or cycle."); return RedirectToPage(new { id }); }
        await _billing.ChangePlanAsync(s, plan, cycle);
        SetFlash($"Plan changed to {plan.Name} ({cycle}). The new price applies from the next invoice.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostInvoiceAsync(int id)
    {
        var s = await _db.Subscriptions.Include(x => x.Plan).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return NotFound();
        if (s.Status == SubscriptionStatus.Cancelled) { SetError("Cannot invoice a cancelled subscription."); return RedirectToPage(new { id }); }
        var company = await _db.Companies.FindAsync(s.CompanyId);
        var open = await _db.Invoices.AnyAsync(i => i.SubscriptionId == id && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Overdue));
        if (open) { SetError("There is already an unpaid invoice for this subscription."); return RedirectToPage(new { id }); }
        var inv = new Invoice
        {
            CompanyId = s.CompanyId, SubscriptionId = s.Id, PlanName = s.Plan!.Name, BillingCycle = s.BillingCycle, Amount = s.Price,
            InvoiceDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30), Number = await _billing.NextInvoiceNumberAsync()
        };
        _db.Invoices.Add(inv);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Invoice.create", "Invoice", inv.Number, company?.Name);
        SetFlash($"Invoice {inv.Number} created.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var s = await _db.Subscriptions.FindAsync(id);
        if (s == null) return NotFound();
        await _billing.CancelSubscriptionAsync(s);
        SetFlash("Subscription cancelled.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await Filter().OrderByDescending(s => s.StartDate).ToListAsync();
        await _audit.LogAsync("Export", "Subscription", null, $"{rows.Count} rows");
        return Export.Xlsx("Subscriptions", new[] { "Company", "Plan", "Billing Cycle", "Price (USD)", "Start Date", "Next Billing", "Status", "Auto Renew" },
            rows.Select(s => new object?[] { s.Company!.Name, s.Plan!.Name, s.BillingCycle, s.Price, s.StartDate, s.NextBillingDate, s.Status, s.AutoRenew ? "Yes" : "No" }),
            $"subscriptions-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
