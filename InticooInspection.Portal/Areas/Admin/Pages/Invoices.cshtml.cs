using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class InvoicesModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly BillingService _billing;
    private readonly AuditService _audit;
    public InvoicesModel(PortalDbContext db, BillingService billing, AuditService audit) { _db = db; _billing = billing; _audit = audit; }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Plan { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Company { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNo { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int Size { get; set; } = 10;
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Dt { get; set; }
    [BindProperty(SupportsGet = true)] public string? Mode { get; set; }

    public List<Invoice> Rows { get; set; } = new();
    public PagerInfo Pager { get; set; } = new(1, 10, 0);
    public Dictionary<string, int> Counts { get; set; } = new();
    public int TotalN, NewLastMonthN;
    public decimal TotalAmount, LastMonthAmount;
    public List<Plan> Plans { get; set; } = new();
    public List<Company> Companies { get; set; } = new();
    public Invoice? Sel { get; set; }

    private IQueryable<Invoice> Filter()
    {
        var q = _db.Invoices.AsNoTracking().Include(i => i.Company).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) { var t = Q.Trim(); q = q.Where(i => i.Number.Contains(t) || i.Company!.Name.Contains(t) || i.Company.Email.Contains(t)); }
        if (!string.IsNullOrEmpty(Status)) q = q.Where(i => i.Status == Status);
        if (!string.IsNullOrEmpty(Plan)) q = q.Where(i => i.PlanName == Plan);
        if (Company != null) q = q.Where(i => i.CompanyId == Company);
        if (From != null) q = q.Where(i => i.InvoiceDate >= From);
        if (To != null) q = q.Where(i => i.InvoiceDate < To.Value.Date.AddDays(1));
        return q;
    }

    public async Task OnGetAsync() { PageNo = ClampPage(PageNo); Size = ClampSize(Size); await LoadAsync(); }

    private async Task LoadAsync()
    {
        var grp = await _db.Invoices.AsNoTracking().GroupBy(i => i.Status).Select(g => new { g.Key, N = g.Count(), Sum = g.Sum(x => x.Amount + x.Tax) }).ToListAsync();
        Counts = grp.ToDictionary(g => g.Key, g => g.N);
        TotalN = grp.Sum(g => g.N); TotalAmount = grp.Where(g => g.Key is InvoiceStatus.Paid).Sum(g => g.Sum);
        var lm = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        NewLastMonthN = await _db.Invoices.CountAsync(i => i.InvoiceDate >= lm);
        LastMonthAmount = await _db.Invoices.Where(i => i.InvoiceDate >= lm && i.Status == InvoiceStatus.Paid).SumAsync(i => (decimal?)(i.Amount + i.Tax)) ?? 0;

        var q = Filter();
        Pager = new PagerInfo(PageNo, Size, await q.CountAsync());
        Rows = await q.OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).Skip((PageNo - 1) * Size).Take(Size).ToListAsync();
        Plans = await _db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        Companies = await _db.Companies.AsNoTracking().Where(c => c.Subscriptions.Any()).OrderBy(c => c.Name).ToListAsync();
        var selId = Id ?? Rows.FirstOrDefault()?.Id;
        if (selId != null)
            Sel = await _db.Invoices.AsNoTracking().Include(i => i.Company).Include(i => i.Payments).FirstOrDefaultAsync(i => i.Id == selId);
    }

    public int N(string s) => Counts.GetValueOrDefault(s);

    public async Task<IActionResult> OnPostCreateAsync(Guid companyId)
    {
        var sub = await _db.Subscriptions.Include(s => s.Plan).Where(s => s.CompanyId == companyId).OrderByDescending(s => s.Id).FirstOrDefaultAsync();
        if (sub == null) { SetError("Company has no subscription."); return RedirectToPage(); }
        if (sub.Status == SubscriptionStatus.Cancelled) { SetError("Subscription is cancelled."); return RedirectToPage(); }
        var inv = new Invoice
        {
            CompanyId = companyId, SubscriptionId = sub.Id, PlanName = sub.Plan!.Name, BillingCycle = sub.BillingCycle, Amount = sub.Price,
            InvoiceDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(30), Number = await _billing.NextInvoiceNumberAsync()
        };
        _db.Invoices.Add(inv);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Invoice.create", "Invoice", inv.Number);
        SetFlash($"Invoice {inv.Number} created.");
        return RedirectToPage(new { id = inv.Id });
    }

    /// <summary>Ghi nhận đã nhận tiền chuyển khoản/ngoài hệ thống → hóa đơn Paid, kích hoạt thuê bao.</summary>
    public async Task<IActionResult> OnPostMarkPaidAsync(int id)
    {
        var inv = await _db.Invoices.Include(i => i.Payments).ThenInclude(p => p.Events).FirstOrDefaultAsync(i => i.Id == id);
        if (inv == null) return NotFound();
        if (inv.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled or InvoiceStatus.Refunded) { SetError("Invoice cannot be marked as paid."); return RedirectToPage(new { id }); }
        var p = inv.Payments.FirstOrDefault(x => x.Status == PaymentStatus.Pending && x.Method == "BankTransfer")
                ?? await _billing.AddPaymentAsync(inv, "BankTransfer", "Manual", PaymentStatus.Pending);
        await _billing.SettleInvoiceAsync(inv, p);
        SetFlash($"Invoice {inv.Number} marked as paid.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var inv = await _db.Invoices.FindAsync(id);
        if (inv == null) return NotFound();
        try { await _billing.CancelInvoiceAsync(inv); SetFlash("Invoice cancelled."); } catch (InvalidOperationException ex) { SetError(ex.Message); }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await Filter().OrderByDescending(i => i.InvoiceDate).ToListAsync();
        await _audit.LogAsync("Export", "Invoice", null, $"{rows.Count} rows");
        return Export.Xlsx("Invoices", new[] { "Invoice No.", "Company", "Plan", "Billing Cycle", "Amount (USD)", "Tax", "Invoice Date", "Due Date", "Status", "Paid At" },
            rows.Select(i => new object?[] { i.Number, i.Company!.Name, i.PlanName, i.BillingCycle, i.Amount, i.Tax, i.InvoiceDate, i.DueDate, i.Status, i.PaidAt }),
            $"invoices-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
