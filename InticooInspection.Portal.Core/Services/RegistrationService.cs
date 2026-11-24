using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using InticooInspection.Portal.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Services;

public class RegisterInput
{
    [Required(ErrorMessage = "Please enter your company name."), StringLength(200, ErrorMessage = "Company name is too long.")] public string CompanyName { get; set; } = "";
    [Required(ErrorMessage = "Please select a valid country / region."), StringLength(2, ErrorMessage = "Please select a valid country / region.")] public string CountryCode { get; set; } = "";
    [Required(ErrorMessage = "Please enter your company address."), StringLength(300, ErrorMessage = "Company address is too long.")] public string Address { get; set; } = "";
    [Required(ErrorMessage = "Please enter the contact person."), StringLength(150, ErrorMessage = "Contact person is too long.")] public string ContactPerson { get; set; } = "";
    [Required(ErrorMessage = "Please enter your work email."), EmailAddress(ErrorMessage = "Please enter a valid work email."), StringLength(200, ErrorMessage = "Work email is too long.")] public string WorkEmail { get; set; } = "";
    [Required(ErrorMessage = "Please enter your phone number."), StringLength(40, ErrorMessage = "Phone number is too long.")] public string Phone { get; set; } = "";
    [StringLength(200, ErrorMessage = "Website is too long.")] public string? Website { get; set; }
    [Required(ErrorMessage = "Please select a subscription plan.")] public string PlanCode { get; set; } = "business";
    [Required(ErrorMessage = "Invalid billing cycle.")] public string BillingCycle { get; set; } = "Monthly";
    public List<string> Languages { get; set; } = new() { "en", "zh", "vi", "ms" };
    public List<string> Modules { get; set; } = new();
    [Required(ErrorMessage = "Please enter the administrator name."), StringLength(150, ErrorMessage = "Administrator name is too long.")] public string AdminName { get; set; } = "";
    [Required(ErrorMessage = "Please enter the administrator email."), EmailAddress(ErrorMessage = "Please enter a valid administrator email."), StringLength(200, ErrorMessage = "Administrator email is too long.")] public string AdminEmail { get; set; } = "";
    [Required(ErrorMessage = "Please create a password.")] public string Password { get; set; } = "";
    [Required(ErrorMessage = "Invalid payment method.")] public string PaymentMethod { get; set; } = "Card";    // Card / PayPal / BankTransfer
    public string? CardNumber { get; set; }
    public string? CardExpiry { get; set; }
    public string? CardCvc { get; set; }
    public string? CardHolder { get; set; }
    public bool AcceptTerms { get; set; }
}

public record RegisterResult(bool Ok, string? Error, Registration? Registration, Invoice? Invoice, bool Paid, string? PaymentError);

public class RegistrationService
{
    private readonly PortalDbContext _db;
    private readonly PasswordService _pw;
    private readonly BillingService _billing;
    private readonly IPaymentGateway _gateway;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly IHttpContextAccessor _http;

    public RegistrationService(PortalDbContext db, PasswordService pw, BillingService billing, IPaymentGateway gateway,
        SettingsService settings, AuditService audit, IHttpContextAccessor http)
    { _db = db; _pw = pw; _billing = billing; _gateway = gateway; _settings = settings; _audit = audit; _http = http; }

    public async Task<RegisterResult> RegisterAsync(RegisterInput i)
    {
        if (!await _settings.GetBoolAsync("reg.allowSelf"))
            return Fail("Self-registration is currently disabled. Please contact us.");
        if (!i.AcceptTerms) return Fail("You must accept the Terms of Service and Privacy Policy.");

        var minLen = await _settings.GetIntAsync("security.minPasswordLength", 8);
        var pwErr = PasswordService.Validate(i.Password, minLen);
        if (pwErr != null) return Fail(pwErr);

        if (!CountryList.ByCode.TryGetValue(i.CountryCode.ToUpperInvariant(), out var country)) return Fail("Please select a valid country / region.");
        var plan = await _db.Plans.Include(p => p.Modules).ThenInclude(m => m.Module)
            .FirstOrDefaultAsync(p => p.Code == i.PlanCode && p.IsActive);
        if (plan == null) return Fail("Selected plan is not available.");
        if (i.BillingCycle is not ("Monthly" or "Annual")) return Fail("Invalid billing cycle.");
        if (i.PaymentMethod is not ("Card" or "PayPal" or "BankTransfer")) return Fail("Invalid payment method.");
        if (!string.IsNullOrWhiteSpace(i.Website) && !Regex.IsMatch(i.Website, @"^https?://[^\s]+$"))
            return Fail("Website must start with http:// or https://");

        var email = i.WorkEmail.Trim().ToLowerInvariant();
        var adminEmail = i.AdminEmail.Trim().ToLowerInvariant();
        if (await _db.Companies.AnyAsync(c => c.Email == email && c.Status != CompanyStatus.Cancelled) ||
            await _db.CompanyUsers.AnyAsync(u => u.Email == adminEmail && u.Company!.Status != CompanyStatus.Cancelled))
            return Fail("An account with this email already exists. Please sign in or contact support.");

        // Mã công ty duy nhất (dùng đặt tên database)
        var baseCode = Slug.From(i.CompanyName);
        var code = baseCode;
        for (int n = 2; await _db.Companies.AnyAsync(c => c.Code == code); n++) code = $"{baseCode}_{n}";

        var langs = i.Languages.Where(l => l is "en" or "zh" or "vi" or "ms").Distinct().ToList();
        if (langs.Count == 0) langs.Add("en");

        var company = new Company
        {
            Code = code, Name = i.CompanyName.Trim(), Country = country, CountryCode = i.CountryCode.ToUpperInvariant(),
            Address = i.Address.Trim(), ContactPerson = i.ContactPerson.Trim(), Email = email, Phone = i.Phone.Trim(),
            Website = string.IsNullOrWhiteSpace(i.Website) ? null : i.Website.Trim(),
            Language = langs[0], Status = CompanyStatus.PendingPayment
        };
        _db.Companies.Add(company);
        var reg = new Registration
        {
            Company = company, AdminName = i.AdminName.Trim(), AdminEmail = adminEmail,
            AdminPasswordHash = _pw.Hash(i.Password), Languages = string.Join(',', langs),
            PaymentMethod = i.PaymentMethod, EmailVerified = true,
            IpAddress = _http.HttpContext?.Connection.RemoteIpAddress?.ToString()
        };
        _db.Registrations.Add(reg);
        _db.CompanyUsers.Add(new CompanyUser
        {
            Company = company, FullName = reg.AdminName, Email = adminEmail, Role = "Administrator",
            Status = "Pending", Language = langs[0], Phone = company.Phone
        });
        await _db.SaveChangesAsync();

        var allowedModules = plan.Modules.Select(m => m.Module!.Code).ToHashSet();
        var chosen = i.Modules.Count == 0 ? allowedModules : i.Modules.Where(allowedModules.Contains).ToHashSet();
        await _billing.SyncCompanyModulesAsync(company.Id, plan.Id, chosen);

        var (_, inv) = await _billing.CreateSubscriptionWithInvoiceAsync(company, plan, i.BillingCycle);
        await _audit.LogAsync("Registered", "Company", company.Id.ToString(), $"{company.Name} / {plan.Name} {i.BillingCycle}", actor: adminEmail);

        if (i.PaymentMethod == "BankTransfer")
        {
            await _billing.AddPaymentAsync(inv, "BankTransfer", "Manual", PaymentStatus.Pending);
            return new RegisterResult(true, null, reg, inv, false, null);
        }
        // PayPal: trang đăng ký tạo order và chuyển khách sang PayPal (PayPalCheckoutService.StartAsync)
        if (i.PaymentMethod == "PayPal") return new RegisterResult(true, null, reg, inv, false, null);

        var (paid, err) = await ChargeAsync(inv, new CardInput(i.CardNumber ?? "", i.CardHolder ?? "", i.CardExpiry ?? "", i.CardCvc ?? ""));
        return new RegisterResult(true, null, reg, inv, paid, err);
    }

    /// <summary>Thanh toán thẻ cho một hóa đơn (dùng cả lúc đăng ký và khi thử lại sau thất bại).</summary>
    public async Task<(bool paid, string? error)> ChargeAsync(Invoice inv, CardInput card)
    {
        var total = inv.Amount + inv.Tax;
        var r = await _gateway.ChargeAsync(card, total, $"Inticoo {inv.PlanName} ({inv.BillingCycle})");
        if (!r.Success)
        {
            await _billing.AddPaymentAsync(inv, "Card", _gateway.Name, PaymentStatus.Failed, r.TransactionId, r.Brand, r.Last4, card.Holder, r.Error);
            await _audit.LogAsync("PaymentFailed", "Invoice", inv.Number, r.Error);
            return (false, r.Error);
        }
        var p = await _billing.AddPaymentAsync(inv, "Card", _gateway.Name, PaymentStatus.Pending, r.TransactionId, r.Brand, r.Last4, card.Holder);
        await _billing.SettleInvoiceAsync(inv, p);
        return (true, null);

        // Lưu ý: số thẻ/CVC chỉ nằm trong biến cục bộ — không được ghi log/DB.
    }

    private static RegisterResult Fail(string msg) => new(false, msg, null, null, false, null);
}

public static class CountryList
{
    // (ISO2, tên) — danh sách rút gọn các thị trường chính; có thể bổ sung
    public static readonly (string Code, string Name)[] All =
    {
        ("MY","Malaysia"),("SG","Singapore"),("VN","Vietnam"),("CN","China"),("US","United States"),("GB","United Kingdom"),
        ("DE","Germany"),("FR","France"),("IT","Italy"),("ES","Spain"),("NL","Netherlands"),("SE","Sweden"),("DK","Denmark"),
        ("PL","Poland"),("CA","Canada"),("AU","Australia"),("NZ","New Zealand"),("JP","Japan"),("KR","South Korea"),
        ("TH","Thailand"),("ID","Indonesia"),("PH","Philippines"),("IN","India"),("HK","Hong Kong"),("TW","Taiwan"),
        ("AE","United Arab Emirates"),("TR","Turkey"),("MX","Mexico"),("BR","Brazil"),("ZA","South Africa"),("XX","Other")
    };
    public static readonly Dictionary<string, string> ByCode = All.ToDictionary(x => x.Code, x => x.Name);
}
