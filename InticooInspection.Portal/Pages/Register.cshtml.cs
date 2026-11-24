using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Pages;

[EnableRateLimiting("register")]
public class RegisterModel : PageModel
{
    private readonly PortalDbContext _db;
    private readonly RegistrationService _svc;
    private readonly IPaymentGateway _gateway;
    private readonly SettingsService _settings;
    private readonly Loc _l;
    private readonly PayPalCheckoutService _paypal;
    public RegisterModel(PortalDbContext db, RegistrationService svc, IPaymentGateway gateway, SettingsService settings, Loc l, PayPalCheckoutService paypal)
    { _db = db; _svc = svc; _gateway = gateway; _settings = settings; _l = l; _paypal = paypal; }

    [BindProperty] public RegisterInput Input { get; set; } = new();
    public List<Plan> Plans { get; set; } = new();
    public List<PlatformModule> Modules { get; set; } = new();
    public bool CardEnabled => _gateway.Available;
    public bool PayPalEnabled => _paypal.Available;
    public bool Closed { get; set; }
    public string? Error { get; set; }
    public List<string> EnabledLanguages { get; set; } = new();

    private async Task LoadAsync()
    {
        Plans = await _db.Plans.AsNoTracking().Include(p => p.Modules).ThenInclude(m => m.Module).Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
        Modules = await _db.Modules.AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync();
        Closed = !await _settings.GetBoolAsync("reg.allowSelf");
        var all = await _settings.AllAsync();
        EnabledLanguages = LocalizationStore.Supported.Where(l => all.GetValueOrDefault("lang." + l, "true") == "true").ToList();
        if (EnabledLanguages.Count == 0) EnabledLanguages.Add("en");
        Input.Languages = Input.Languages.Where(EnabledLanguages.Contains).ToList();
        if (string.IsNullOrEmpty(Input.PlanCode) || !Plans.Any(p => p.Code == Input.PlanCode))
            Input.PlanCode = (Plans.FirstOrDefault(p => p.Code == all.GetValueOrDefault("default.plan")) ?? Plans.FirstOrDefault(p => p.IsPopular) ?? Plans.FirstOrDefault())?.Code ?? "";
    }

    public async Task OnGetAsync(string? plan, string? cycle)
    {
        if (!string.IsNullOrEmpty(plan)) Input.PlanCode = plan;
        Input.BillingCycle = cycle == "Annual" ? "Annual" : "Monthly";
        Input.PaymentMethod = PayPalEnabled ? "PayPal" : CardEnabled ? "Card" : "BankTransfer";
        await LoadAsync();
        Input.Modules = Plans.First(p => p.Code == Input.PlanCode).Modules.Select(m => m.Module!.Code).ToList();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        // Mật khẩu/số thẻ KHÔNG được trả lại form khi lỗi
        ModelState.Remove("Input.Password"); ModelState.Remove("Input.CardNumber"); ModelState.Remove("Input.CardCvc");

        if (Input.PaymentMethod == "Card" && !CardEnabled)
            ModelState.AddModelError("", "Online card payment is not available yet. Please choose Bank Transfer.");
        if (Input.PaymentMethod == "PayPal" && !PayPalEnabled)
            ModelState.AddModelError("", "PayPal payment is not available. Please choose another payment method.");
        if (Input.PaymentMethod == "Card" && string.IsNullOrWhiteSpace(Input.CardNumber))
            ModelState.AddModelError("", "Please enter your card details.");
        if (!ModelState.IsValid) { ModelState.Localize(_l); Input.Password = ""; Input.CardNumber = ""; Input.CardCvc = ""; return Page(); }

        var r = await _svc.RegisterAsync(Input);
        Input.Password = ""; Input.CardNumber = ""; Input.CardCvc = "";
        if (!r.Ok) { Error = r.Error; return Page(); }
        if (Input.PaymentMethod == "PayPal" && r.Invoice != null)
        {
            var (url, _) = await _paypal.StartAsync(r.Invoice, $"{Request.Scheme}://{Request.Host}{Request.PathBase}");
            if (url != null) return Redirect(url);   // lỗi kết nối PayPal → trang hoàn tất cho thử lại / chọn chuyển khoản
        }
        return RedirectToPage("RegisterComplete", new { id = r.Registration!.Id });
    }
}
