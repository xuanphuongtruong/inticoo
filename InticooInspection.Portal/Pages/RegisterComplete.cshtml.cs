using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Pages;

[EnableRateLimiting("register")]
public class RegisterCompleteModel : PageModel
{
    private readonly PortalDbContext _db;
    private readonly RegistrationService _svc;
    private readonly SettingsService _settings;
    private readonly IPaymentGateway _gateway;
    private readonly PayPalCheckoutService _paypal;
    public RegisterCompleteModel(PortalDbContext db, RegistrationService svc, SettingsService settings, IPaymentGateway gateway, PayPalCheckoutService paypal)
    { _db = db; _svc = svc; _settings = settings; _gateway = gateway; _paypal = paypal; }

    public Registration? Reg { get; set; }
    public Company? Company { get; set; }
    public Invoice? Invoice { get; set; }
    public Payment? LastPayment { get; set; }
    public string BankInfo { get; set; } = "";
    public bool CardEnabled => _gateway.Available;
    public bool PayPalEnabled => _paypal.Available;
    public string? Error { get; set; }

    private async Task<bool> LoadAsync(Guid id)
    {
        // Id đăng ký (GUID ngẫu nhiên) đóng vai trò mã truy cập; chỉ hiển thị thông tin tối thiểu, không có dữ liệu nhạy cảm
        Reg = await _db.Registrations.AsNoTracking().Include(r => r.Company).FirstOrDefaultAsync(r => r.Id == id);
        if (Reg == null) return false;
        Company = Reg.Company;
        Invoice = await _db.Invoices.Include(i => i.Payments).Where(i => i.CompanyId == Reg.CompanyId).OrderByDescending(i => i.Id).FirstOrDefaultAsync();
        LastPayment = Invoice?.Payments.OrderByDescending(p => p.Id).FirstOrDefault();
        BankInfo = await _settings.GetAsync("billing.bankInfo");
        return true;
    }

    public async Task<IActionResult> OnGetAsync(Guid id) => await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostRetryAsync(Guid id, string? number, string? expiry, string? cvc, string? holder)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Invoice == null || Invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled) return RedirectToPage(new { id });
        var (paid, err) = await _svc.ChargeAsync(Invoice, new CardInput(number ?? "", holder ?? "", (expiry ?? "").Replace(" ", ""), cvc ?? ""));
        if (!paid) { Error = err; await LoadAsync(id); return Page(); }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPayPalAsync(Guid id)
    {
        if (!await LoadAsync(id)) return NotFound();
        if (Invoice == null || Invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled) return RedirectToPage(new { id });
        var (url, err) = await _paypal.StartAsync(Invoice, $"{Request.Scheme}://{Request.Host}{Request.PathBase}");
        if (url != null) return Redirect(url);
        Error = err; await LoadAsync(id); return Page();
    }
}
