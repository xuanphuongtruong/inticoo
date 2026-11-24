using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class PaymentsModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly BillingService _billing;
    private readonly AuditService _audit;
    public PaymentsModel(PortalDbContext db, BillingService billing, AuditService audit) { _db = db; _billing = billing; _audit = audit; }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Method { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNo { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int Size { get; set; } = 10;
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }

    public List<Payment> Rows { get; set; } = new();
    public PagerInfo Pager { get; set; } = new(1, 10, 0);
    public Dictionary<string, int> Counts { get; set; } = new();
    public decimal TotalPaid, LastMonthPaid;
    public Payment? Sel { get; set; }
    public int TotalN => Counts.Values.Sum();
    public int N(string s) => Counts.GetValueOrDefault(s);

    private IQueryable<Payment> Filter()
    {
        var q = _db.Payments.AsNoTracking().Include(p => p.Company).Include(p => p.Invoice).AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) { var t = Q.Trim(); q = q.Where(p => p.TransactionId.Contains(t) || p.Company!.Name.Contains(t) || p.Invoice!.Number.Contains(t)); }
        if (!string.IsNullOrEmpty(Status)) q = q.Where(p => p.Status == Status);
        if (!string.IsNullOrEmpty(Method)) q = q.Where(p => p.Method == Method);
        if (From != null) q = q.Where(p => p.CreatedAt >= From);
        if (To != null) q = q.Where(p => p.CreatedAt < To.Value.Date.AddDays(1));
        return q;
    }

    public async Task OnGetAsync() { PageNo = ClampPage(PageNo); Size = ClampSize(Size); await LoadAsync(); }

    private async Task LoadAsync()
    {
        Counts = (await _db.Payments.AsNoTracking().GroupBy(p => p.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).ToDictionary(x => x.Key, x => x.N);
        var ms = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        TotalPaid = await _db.Payments.Where(p => p.Status == PaymentStatus.Paid).SumAsync(p => (decimal?)p.Amount) ?? 0;
        LastMonthPaid = await _db.Payments.Where(p => p.Status == PaymentStatus.Paid && p.CreatedAt >= ms).SumAsync(p => (decimal?)p.Amount) ?? 0;
        var q = Filter();
        Pager = new PagerInfo(PageNo, Size, await q.CountAsync());
        Rows = await q.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).Skip((PageNo - 1) * Size).Take(Size).ToListAsync();
        var selId = Id ?? Rows.FirstOrDefault()?.Id;
        if (selId != null)
            Sel = await _db.Payments.AsNoTracking().Include(p => p.Company).Include(p => p.Invoice).Include(p => p.Events).FirstOrDefaultAsync(p => p.Id == selId);
    }

    public async Task<IActionResult> OnPostRefundAsync(int id)
    {
        var p = await _db.Payments.Include(x => x.Events).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        try
        {
            // Lưu ý: cổng test không có API hoàn tiền; với Stripe thật cần gọi refund ở đây trước khi đổi trạng thái.
            await _billing.RefundAsync(p);
            SetFlash(p.Provider == PayPalCheckoutService.Provider
                ? $"Payment {p.TransactionId} refunded via PayPal."
                : $"Payment {p.TransactionId} marked as refunded. If it was a real card payment, issue the refund in the payment provider as well.");
        }
        catch (InvalidOperationException ex) { SetError(ex.Message); }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostConfirmAsync(int id)
    {
        var p = await _db.Payments.Include(x => x.Events).Include(x => x.Invoice).FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return NotFound();
        if (p.Status != PaymentStatus.Pending || p.Invoice == null) { SetError("Only pending payments can be confirmed."); return RedirectToPage(new { id }); }
        await _billing.SettleInvoiceAsync(p.Invoice, p);
        SetFlash("Payment confirmed and subscription activated.");
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await Filter().OrderByDescending(p => p.CreatedAt).ToListAsync();
        await _audit.LogAsync("Export", "Payment", null, $"{rows.Count} rows");
        return Export.Xlsx("Payments", new[] { "Date & Time", "Transaction ID", "Company", "Invoice No.", "Amount (USD)", "Method", "Provider", "Status" },
            rows.Select(p => new object?[] { p.CreatedAt, p.TransactionId, p.Company!.Name, p.Invoice!.Number, p.Amount, p.Method, p.Provider, p.Status }),
            $"payments-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
