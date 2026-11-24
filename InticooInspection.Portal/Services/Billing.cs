using InticooInspection.Portal.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Services;

public record CardInput(string Number, string Holder, string Expiry, string Cvc);
public record GatewayResult(bool Success, string TransactionId, string? Brand, string? Last4, string? Error);

public interface IPaymentGateway
{
    string Name { get; }
    bool Available { get; }
    Task<GatewayResult> ChargeAsync(CardInput card, decimal amount, string description);
}

/// <summary>
/// Cổng thanh toán GIẢ LẬP cho môi trường test (Payments:Mode=Test). Không gọi nhà cung cấp thật và không lưu
/// số thẻ/CVC — chỉ trả về brand + 4 số cuối. Khi lên production phải thay bằng Stripe (Checkout/Elements).
/// Thẻ test: 4242 4242 4242 4242 = thành công; 4000 0000 0000 0002 = bị từ chối.
/// </summary>
public class TestPaymentGateway : IPaymentGateway
{
    private readonly bool _enabled;
    public TestPaymentGateway(IConfiguration cfg) => _enabled = string.Equals(cfg["Payments:Mode"], "Test", StringComparison.OrdinalIgnoreCase);
    public string Name => "Test";
    public bool Available => _enabled;

    public Task<GatewayResult> ChargeAsync(CardInput card, decimal amount, string description)
    {
        var txn = "pi_test_" + Guid.NewGuid().ToString("N")[..16];
        if (!_enabled) return Task.FromResult(new GatewayResult(false, txn, null, null, "Online card payment is not enabled."));

        var digits = new string((card.Number ?? "").Where(char.IsDigit).ToArray());
        var brand = digits.StartsWith('4') ? "Visa" : digits.StartsWith('5') ? "Mastercard" : digits.StartsWith('3') ? "Amex" : "Card";
        var last4 = digits.Length >= 4 ? digits[^4..] : null;

        if (digits.Length < 13 || digits.Length > 19 || !Luhn(digits))
            return Task.FromResult(new GatewayResult(false, txn, brand, last4, "Invalid card number."));
        if (!ExpiryOk(card.Expiry))
            return Task.FromResult(new GatewayResult(false, txn, brand, last4, "Card expired or expiry date invalid."));
        if (!(card.Cvc?.All(char.IsDigit) ?? false) || card.Cvc.Length is < 3 or > 4)
            return Task.FromResult(new GatewayResult(false, txn, brand, last4, "Invalid security code."));
        if (digits == "4000000000000002")
            return Task.FromResult(new GatewayResult(false, txn, brand, last4, "Your card was declined."));

        return Task.FromResult(new GatewayResult(true, txn, brand, last4, null));
    }

    private static bool Luhn(string d)
    {
        int sum = 0; bool alt = false;
        for (int i = d.Length - 1; i >= 0; i--)
        {
            int n = d[i] - '0';
            if (alt) { n *= 2; if (n > 9) n -= 9; }
            sum += n; alt = !alt;
        }
        return sum % 10 == 0;
    }

    private static bool ExpiryOk(string? exp)
    {
        if (string.IsNullOrWhiteSpace(exp)) return false;
        var p = exp.Split('/', StringSplitOptions.TrimEntries);
        if (p.Length != 2 || !int.TryParse(p[0], out var m) || !int.TryParse(p[1], out var y)) return false;
        if (y < 100) y += 2000;
        if (m is < 1 or > 12) return false;
        var end = new DateTime(y, m, 1).AddMonths(1);
        return end > DateTime.UtcNow;
    }
}

/// <summary>Nghiệp vụ gói thuê bao / hóa đơn / thanh toán.</summary>
public class BillingService
{
    private readonly PortalDbContext _db;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly ProvisioningService _prov;
    private readonly PayPalClient _paypal;

    public BillingService(PortalDbContext db, SettingsService settings, AuditService audit, ProvisioningService prov, PayPalClient paypal)
    { _db = db; _settings = settings; _audit = audit; _prov = prov; _paypal = paypal; }

    public static DateTime AddCycle(DateTime from, string cycle) => cycle == "Annual" ? from.AddYears(1) : from.AddMonths(1);

    public async Task<string> NextInvoiceNumberAsync(DateTime? at = null)
    {
        var prefix = await _settings.GetAsync("invoice.prefix");
        var year = (at ?? DateTime.UtcNow).Year;
        var head = $"{prefix}-{year}-";
        var nums = await _db.Invoices.Where(i => i.Number.StartsWith(head)).Select(i => i.Number).ToListAsync();
        var max = nums.Select(n => int.TryParse(n[head.Length..], out var v) ? v : 0).DefaultIfEmpty(0).Max();
        return head + (max + 1).ToString("0000");
    }

    /// <summary>Tạo subscription (chờ thanh toán) + hóa đơn đầu tiên cho công ty.</summary>
    public async Task<(Subscription sub, Invoice inv)> CreateSubscriptionWithInvoiceAsync(Company company, Plan plan, string cycle)
    {
        var price = cycle == "Annual" ? plan.AnnualPrice : plan.MonthlyPrice;
        var taxPct = decimal.TryParse(await _settings.GetAsync("billing.taxPercent"), out var t) ? t : 0;
        var dueDays = await _settings.GetIntAsync("billing.dueDays", 30);
        var sub = new Subscription
        {
            Company = company, PlanId = plan.Id, BillingCycle = cycle, Price = price,
            StartDate = DateTime.UtcNow, Status = SubscriptionStatus.PendingPayment
        };
        var inv = new Invoice
        {
            Company = company, Subscription = sub, PlanName = plan.Name, BillingCycle = cycle,
            Amount = price, Tax = Math.Round(price * taxPct / 100m, 2),
            InvoiceDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(dueDays),
            Number = await NextInvoiceNumberAsync()
        };
        _db.Subscriptions.Add(sub);
        _db.Invoices.Add(inv);
        await _db.SaveChangesAsync();
        return (sub, inv);
    }

    public static string NewTransactionId(string provider) =>
        (provider == "Manual" ? "mn_" : "pi_") + Guid.NewGuid().ToString("N")[..20];

    public async Task<Payment> AddPaymentAsync(Invoice inv, string method, string provider, string status,
        string? txn = null, string? brand = null, string? last4 = null, string? holder = null, string? failure = null)
    {
        var p = new Payment
        {
            InvoiceId = inv.Id, CompanyId = inv.CompanyId, Amount = inv.Amount + inv.Tax,
            Method = method, Provider = provider, Status = status,
            TransactionId = txn ?? NewTransactionId(provider),
            CardBrand = brand, CardLast4 = last4, CardHolder = holder, FailureReason = failure
        };
        p.Events.Add(new PaymentEvent { Name = "Payment Initiated" });
        if (status == PaymentStatus.Paid)
        {
            p.Events.Add(new PaymentEvent { Name = "Payment Processing" });
            p.Events.Add(new PaymentEvent { Name = "Payment Successful" });
        }
        else if (status == PaymentStatus.Failed) p.Events.Add(new PaymentEvent { Name = "Payment Failed" });
        _db.Payments.Add(p);
        await _db.SaveChangesAsync();
        return p;
    }

    /// <summary>Đánh dấu hóa đơn đã thanh toán → kích hoạt thuê bao/công ty và cấp database nếu cần.</summary>
    public async Task SettleInvoiceAsync(Invoice inv, Payment payment)
    {
        inv.Status = InvoiceStatus.Paid;
        inv.PaidAt = DateTime.UtcNow;
        payment.Status = PaymentStatus.Paid;
        if (!payment.Events.Any(e => e.Name == "Payment Successful"))
            payment.Events.Add(new PaymentEvent { Name = "Payment Successful" });
        payment.Events.Add(new PaymentEvent { Name = "Invoice Generated" });

        var sub = inv.SubscriptionId == null ? null : await _db.Subscriptions.FindAsync(inv.SubscriptionId);
        var company = await _db.Companies.FirstAsync(c => c.Id == inv.CompanyId);
        if (sub != null)
        {
            var start = sub.NextBillingDate is { } nb && nb > DateTime.UtcNow && sub.Status == SubscriptionStatus.Active ? nb : DateTime.UtcNow;
            if (sub.Status != SubscriptionStatus.Active) sub.StartDate = DateTime.UtcNow;
            sub.NextBillingDate = AddCycle(start, sub.BillingCycle);
            sub.Status = SubscriptionStatus.Active;
            sub.CancelledAt = null;
            payment.Events.Add(new PaymentEvent { Name = "Subscription Activated" });
        }

        var needApproval = await _settings.GetBoolAsync("reg.requireAdminApproval") && company.ProvisionStatus != ProvisionStatus.Provisioned;
        if (needApproval) { company.Status = CompanyStatus.PendingReview; if (sub != null) sub.Status = SubscriptionStatus.PendingReview; }
        else if (company.Status is CompanyStatus.PendingPayment or CompanyStatus.Overdue or CompanyStatus.PendingReview)
            company.Status = CompanyStatus.Active;
        await _db.SaveChangesAsync();

        if (!needApproval && company.ProvisionStatus != ProvisionStatus.Provisioned)
            await _prov.ProvisionAsync(company);
        await _audit.LogAsync("InvoicePaid", "Invoice", inv.Number, $"{company.Name} {inv.Amount:0.00}");
    }

    public async Task RefundAsync(Payment p)
    {
        if (p.Status != PaymentStatus.Paid) throw new InvalidOperationException("Only paid payments can be refunded.");
        // PayPal: hoàn tiền thật về thẻ/tài khoản PayPal của khách (TransactionId = capture id). Lỗi → không đổi trạng thái.
        if (p.Provider == PayPalCheckoutService.Provider)
        {
            try { await _paypal.RefundAsync(p.TransactionId); }
            catch (PayPalException ex) { throw new InvalidOperationException("PayPal refund failed: " + ex.Message); }
        }
        p.Status = PaymentStatus.Refunded;
        p.Events.Add(new PaymentEvent { Name = "Payment Refunded" });
        var inv = await _db.Invoices.FindAsync(p.InvoiceId);
        if (inv != null) inv.Status = InvoiceStatus.Refunded;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("PaymentRefunded", "Payment", p.TransactionId, $"{p.Amount:0.00}");
    }

    public async Task CancelInvoiceAsync(Invoice inv)
    {
        if (inv.Status == InvoiceStatus.Paid) throw new InvalidOperationException("A paid invoice cannot be cancelled — refund the payment instead.");
        inv.Status = InvoiceStatus.Cancelled;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("InvoiceCancelled", "Invoice", inv.Number);
    }

    public async Task CancelSubscriptionAsync(Subscription s)
    {
        s.Status = SubscriptionStatus.Cancelled; s.CancelledAt = DateTime.UtcNow; s.AutoRenew = false;
        var c = await _db.Companies.FindAsync(s.CompanyId);
        if (c != null) c.Status = CompanyStatus.Cancelled;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("SubscriptionCancelled", "Subscription", s.Id.ToString(), c?.Name);
    }

    public async Task ChangePlanAsync(Subscription s, Plan plan, string cycle)
    {
        s.PlanId = plan.Id; s.BillingCycle = cycle; s.Price = cycle == "Annual" ? plan.AnnualPrice : plan.MonthlyPrice;
        if (s.NextBillingDate != null) s.NextBillingDate = AddCycle(s.StartDate, cycle);
        // Module bật cho công ty theo gói mới (chỉ thêm/bớt theo gói, giữ nguyên lựa chọn tắt thủ công của module vẫn thuộc gói)
        await SyncCompanyModulesAsync(s.CompanyId, plan.Id);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("PlanChanged", "Subscription", s.Id.ToString(), $"{plan.Name}/{cycle}");
    }

    public async Task SyncCompanyModulesAsync(Guid companyId, int planId, IEnumerable<string>? selected = null)
    {
        var allowed = await _db.PlanModules.Where(pm => pm.PlanId == planId).Select(pm => pm.Module!.Code).ToListAsync();
        var existing = await _db.CompanyModules.Where(m => m.CompanyId == companyId).ToListAsync();
        var sel = selected?.ToHashSet();
        foreach (var code in allowed)
        {
            var row = existing.FirstOrDefault(e => e.ModuleCode == code);
            var enabled = sel == null || sel.Contains(code);
            if (row == null) _db.CompanyModules.Add(new CompanyModule { CompanyId = companyId, ModuleCode = code, Enabled = enabled });
            else if (sel != null) row.Enabled = enabled;
        }
        _db.CompanyModules.RemoveRange(existing.Where(e => !allowed.Contains(e.ModuleCode)));
        await _db.SaveChangesAsync();
    }

    /// <summary>Hóa đơn quá hạn → Overdue; công ty Active có hóa đơn quá hạn → Overdue.</summary>
    public async Task<int> RefreshOverdueAsync()
    {
        var now = DateTime.UtcNow;
        var late = await _db.Invoices.Where(i => i.Status == InvoiceStatus.Pending && i.DueDate < now).ToListAsync();
        foreach (var i in late)
        {
            i.Status = InvoiceStatus.Overdue;
            var sub = i.SubscriptionId == null ? null : await _db.Subscriptions.FindAsync(i.SubscriptionId);
            var comp = await _db.Companies.FindAsync(i.CompanyId);
            if (comp is { Status: CompanyStatus.Active }) comp.Status = CompanyStatus.Overdue;
            if (sub is { Status: SubscriptionStatus.Active }) sub.Status = SubscriptionStatus.Overdue;
        }
        if (late.Count > 0) await _db.SaveChangesAsync();
        return late.Count;
    }
}

public class OverdueWorker : BackgroundService
{
    private readonly IServiceScopeFactory _sf;
    private readonly ILogger<OverdueWorker> _log;
    public OverdueWorker(IServiceScopeFactory sf, ILogger<OverdueWorker> log) { _sf = sf; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _sf.CreateScope();
                var n = await scope.ServiceProvider.GetRequiredService<BillingService>().RefreshOverdueAsync();
                if (n > 0) _log.LogInformation("Đánh dấu {N} hóa đơn quá hạn", n);
            }
            catch (Exception ex) { _log.LogWarning(ex, "OverdueWorker lỗi"); }
            try { await Task.Delay(TimeSpan.FromHours(1), ct); } catch (TaskCanceledException) { }
        }
    }
}
