using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using InticooInspection.Portal.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Services;

/// <summary>
/// PayPal Checkout (Orders API v2) theo kiểu CHUYỂN HƯỚNG: server tạo order → khách sang trang PayPal duyệt
/// (đăng nhập PayPal HOẶC trả bằng thẻ Visa/Mastercard không cần tài khoản — landing_page GUEST_CHECKOUT)
/// → PayPal quay về /pay/paypal/return → server CAPTURE lấy tiền. Server không bao giờ nhận số thẻ.
/// Khách duyệt xong mà không quay về thì order chưa capture = chưa trừ tiền (không cần webhook để tránh mất tiền).
///
/// Cấu hình (đặt ClientId/Secret trong appsettings.Secrets.json, KHÔNG để trong appsettings.json):
///   PayPal:Mode = Sandbox | Live,  PayPal:ClientId,  PayPal:Secret,  PayPal:BrandName (tùy chọn)
/// Thiếu ClientId/Secret → PayPal tắt, form đăng ký không hiện lựa chọn này.
/// </summary>
public class PayPalClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<PayPalClient> _log;
    private static string? _token;
    private static DateTime _tokenExp;
    private static readonly SemaphoreSlim _tokenLock = new(1, 1);

    public PayPalClient(HttpClient http, IConfiguration cfg, ILogger<PayPalClient> log)
    {
        _http = http; _cfg = cfg; _log = log;
        // PayPal:ApiBase chỉ dùng khi kiểm thử với PayPal giả lập (mặc định theo PayPal:Mode)
        var apiBase = cfg["PayPal:ApiBase"];
        _http.BaseAddress ??= new Uri(!string.IsNullOrWhiteSpace(apiBase) ? apiBase
            : IsLive ? "https://api-m.paypal.com/" : "https://api-m.sandbox.paypal.com/");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public bool IsLive => string.Equals(_cfg["PayPal:Mode"], "Live", StringComparison.OrdinalIgnoreCase);
    public bool Available => !string.IsNullOrWhiteSpace(_cfg["PayPal:ClientId"]) && !string.IsNullOrWhiteSpace(_cfg["PayPal:Secret"]);

    private async Task<string> TokenAsync()
    {
        if (_token != null && DateTime.UtcNow < _tokenExp) return _token;
        await _tokenLock.WaitAsync();
        try
        {
            if (_token != null && DateTime.UtcNow < _tokenExp) return _token;
            var req = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" })
            };
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_cfg["PayPal:ClientId"]}:{_cfg["PayPal:Secret"]}"));
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            using var res = await _http.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) throw new PayPalException($"PayPal authentication failed ({(int)res.StatusCode}). Check PayPal:ClientId / PayPal:Secret / PayPal:Mode.", body);
            var j = JsonNode.Parse(body)!;
            _token = (string)j["access_token"]!;
            _tokenExp = DateTime.UtcNow.AddSeconds(((int?)j["expires_in"] ?? 300) - 120);
            return _token;
        }
        finally { _tokenLock.Release(); }
    }

    private async Task<JsonNode> SendAsync(HttpMethod method, string path, object? payload, string? requestId = null)
    {
        var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await TokenAsync());
        if (requestId != null) req.Headers.Add("PayPal-Request-Id", requestId);   // chống tạo/capture trùng khi gửi lại
        req.Headers.Add("Prefer", "return=representation");
        if (payload != null) req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            _log.LogWarning("PayPal {Method} {Path} → {Status}: {Body}", method, path, (int)res.StatusCode, body);
            string? issue = null;
            try { issue = (string?)JsonNode.Parse(body)?["details"]?[0]?["issue"]; } catch { }
            throw new PayPalException(issue ?? $"PayPal error {(int)res.StatusCode}", body);
        }
        return string.IsNullOrWhiteSpace(body) ? new JsonObject() : JsonNode.Parse(body)!;
    }

    public static string Money(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>Tạo order cho hóa đơn → (orderId, link duyệt của PayPal).</summary>
    public async Task<(string OrderId, string ApproveUrl)> CreateOrderAsync(Invoice inv, string currency, string returnUrl, string cancelUrl)
    {
        var total = inv.Amount + inv.Tax;
        var payload = new
        {
            intent = "CAPTURE",
            purchase_units = new[]
            {
                new
                {
                    reference_id = inv.Number,
                    invoice_id = inv.Number + "-" + DateTime.UtcNow.ToString("yyMMddHHmmss"),   // PayPal chặn invoice_id trùng → thêm hậu tố cho lần thử lại
                    description = $"Inticoo {inv.PlanName} ({inv.BillingCycle})",
                    custom_id = inv.Id.ToString(CultureInfo.InvariantCulture),
                    amount = new { currency_code = currency, value = Money(total) }
                }
            },
            payment_source = new
            {
                paypal = new
                {
                    experience_context = new
                    {
                        brand_name = _cfg["PayPal:BrandName"] ?? "Inticoo",
                        landing_page = "GUEST_CHECKOUT",          // mở thẳng form thẻ (Visa/Mastercard) không cần tài khoản PayPal
                        shipping_preference = "NO_SHIPPING",
                        user_action = "PAY_NOW",
                        return_url = returnUrl,
                        cancel_url = cancelUrl
                    }
                }
            }
        };
        var j = await SendAsync(HttpMethod.Post, "v2/checkout/orders", payload, "inv-" + inv.Id + "-" + Guid.NewGuid().ToString("N")[..12]);
        var id = (string)j["id"]!;
        var approve = j["links"]?.AsArray().FirstOrDefault(l => (string?)l?["rel"] is "payer-action" or "approve")?["href"]?.ToString();
        if (string.IsNullOrEmpty(approve)) throw new PayPalException("PayPal did not return an approval link.", j.ToJsonString());
        return (id, approve);
    }

    public record CaptureResult(bool Completed, string Status, string? CaptureId, decimal? Amount, string? Currency,
        string? CardBrand, string? CardLast4, string? PayerName);

    /// <summary>Lấy tiền cho order đã được khách duyệt. Gọi lại với cùng order → trả về kết quả cũ (idempotent).</summary>
    public async Task<CaptureResult> CaptureAsync(string orderId)
    {
        JsonNode j;
        try { j = await SendAsync(HttpMethod.Post, $"v2/checkout/orders/{Uri.EscapeDataString(orderId)}/capture", new { }, "cap-" + orderId); }
        catch (PayPalException ex) when (ex.Message == "ORDER_ALREADY_CAPTURED")
        { j = await SendAsync(HttpMethod.Get, $"v2/checkout/orders/{Uri.EscapeDataString(orderId)}", null); }
        return ParseCapture(j);
    }

    private static CaptureResult ParseCapture(JsonNode j)
    {
        var cap = j["purchase_units"]?[0]?["payments"]?["captures"]?[0];
        var capStatus = (string?)cap?["status"];
        decimal? amt = decimal.TryParse((string?)cap?["amount"]?["value"], NumberStyles.Number, CultureInfo.InvariantCulture, out var a) ? a : null;
        var card = j["payment_source"]?["card"];
        var name = j["payer"]?["name"];
        var payer = name == null ? (string?)card?["name"] : $"{name["given_name"]} {name["surname"]}".Trim();
        return new CaptureResult(capStatus == "COMPLETED", capStatus ?? (string?)j["status"] ?? "UNKNOWN", (string?)cap?["id"], amt,
            (string?)cap?["amount"]?["currency_code"], (string?)card?["brand"], (string?)card?["last_digits"], payer);
    }

    /// <summary>Hoàn tiền toàn bộ một capture.</summary>
    public async Task RefundAsync(string captureId)
        => await SendAsync(HttpMethod.Post, $"v2/payments/captures/{Uri.EscapeDataString(captureId)}/refund", new { }, "ref-" + captureId);
}

public class PayPalException : Exception
{
    public string? Body { get; }
    public PayPalException(string message, string? body) : base(message) => Body = body;
}

/// <summary>Nối PayPal với hóa đơn/thanh toán của Portal.</summary>
public class PayPalCheckoutService
{
    public const string Provider = "PayPal";
    private readonly PortalDbContext _db;
    private readonly PayPalClient _pp;
    private readonly BillingService _billing;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly ILogger<PayPalCheckoutService> _log;

    public PayPalCheckoutService(PortalDbContext db, PayPalClient pp, BillingService billing, SettingsService settings, AuditService audit, ILogger<PayPalCheckoutService> log)
    { _db = db; _pp = pp; _billing = billing; _settings = settings; _audit = audit; _log = log; }

    public bool Available => _pp.Available;

    /// <summary>Tạo order PayPal cho hóa đơn, ghi 1 Payment "Pending" (TransactionId = order id) → trả link chuyển sang PayPal.</summary>
    public async Task<(string? url, string? error)> StartAsync(Invoice inv, string baseUrl)
    {
        if (!_pp.Available) return (null, "PayPal payment is not available.");
        if (inv.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled or InvoiceStatus.Refunded) return (null, "This invoice cannot be paid.");
        var currency = await _settings.GetAsync("billing.currency");
        if (string.IsNullOrWhiteSpace(currency)) currency = "USD";
        try
        {
            var (orderId, approve) = await _pp.CreateOrderAsync(inv, currency,
                baseUrl.TrimEnd('/') + "/pay/paypal/return", baseUrl.TrimEnd('/') + "/pay/paypal/cancel");
            var p = await _billing.AddPaymentAsync(inv, "PayPal", Provider, PaymentStatus.Pending, txn: orderId);
            p.Events.Add(new PaymentEvent { Name = "Redirected to PayPal" });
            await _db.SaveChangesAsync();
            return (approve, null);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "PayPal create order failed for invoice {Invoice}", inv.Number);
            return (null, "Could not connect to PayPal. Please try again or choose Bank Transfer.");
        }
    }

    /// <summary>Khách duyệt xong trên PayPal → capture. Trả về CompanyId để quay lại trang đăng ký.</summary>
    public async Task<(Guid? companyId, string? error)> CompleteAsync(string orderId)
    {
        var p = await _db.Payments.Include(x => x.Events).FirstOrDefaultAsync(x => x.Provider == Provider && x.TransactionId == orderId);
        if (p == null)
        {
            // Đã capture trước đó (TransactionId đã đổi sang capture id) → chỉ cần quay lại trang
            var done = await _db.PaymentEvents.Include(e => e.Payment).FirstOrDefaultAsync(e => e.Name == "PayPal order " + orderId);
            return done?.Payment == null ? (null, "Payment not found.") : (done.Payment.CompanyId, null);
        }
        if (p.Status != PaymentStatus.Pending) return (p.CompanyId, null);
        var inv = await _db.Invoices.FirstAsync(i => i.Id == p.InvoiceId);
        try
        {
            var r = await _pp.CaptureAsync(orderId);
            var currency = await _settings.GetAsync("billing.currency");
            if (string.IsNullOrWhiteSpace(currency)) currency = "USD";
            if (!r.Completed)
            {
                await FailAsync(p, inv, $"PayPal payment not completed ({r.Status}).");
                return (p.CompanyId, null);
            }
            if (r.Amount != p.Amount || !string.Equals(r.Currency, currency, StringComparison.OrdinalIgnoreCase))
            {
                // Không xảy ra khi bình thường (số tiền do server đặt). Ghi nhận để admin kiểm tra, không tự kích hoạt.
                p.Events.Add(new PaymentEvent { Name = "PayPal order " + orderId });
                p.TransactionId = r.CaptureId ?? orderId;
                await FailAsync(p, inv, $"Amount mismatch: received {r.Amount} {r.Currency}, expected {p.Amount} {currency}. Please contact support.");
                return (p.CompanyId, null);
            }
            p.Events.Add(new PaymentEvent { Name = "PayPal order " + orderId });
            p.TransactionId = r.CaptureId ?? orderId;          // giữ capture id để hoàn tiền
            p.CardBrand = r.CardBrand ?? "PayPal";
            p.CardLast4 = r.CardLast4;
            p.CardHolder = r.PayerName?.Length > 150 ? r.PayerName[..150] : r.PayerName;
            await _db.SaveChangesAsync();
            await _billing.SettleInvoiceAsync(inv, p);
            return (p.CompanyId, null);
        }
        catch (PayPalException ex)
        {
            _log.LogWarning(ex, "PayPal capture failed for order {Order}", orderId);
            var msg = ex.Message switch
            {
                "INSTRUMENT_DECLINED" => "Your card was declined. Please try another card.",
                "ORDER_NOT_APPROVED" => "The payment was not approved on PayPal.",
                _ => "PayPal could not complete the payment. Please try again."
            };
            await FailAsync(p, inv, msg);
            return (p.CompanyId, null);
        }
    }

    /// <summary>Khách bấm hủy trên trang PayPal.</summary>
    public async Task<Guid?> CancelAsync(string orderId)
    {
        var p = await _db.Payments.Include(x => x.Events).FirstOrDefaultAsync(x => x.Provider == Provider && x.TransactionId == orderId);
        if (p == null) return null;
        if (p.Status == PaymentStatus.Pending)
        {
            var inv = await _db.Invoices.FirstAsync(i => i.Id == p.InvoiceId);
            await FailAsync(p, inv, "Payment was cancelled on PayPal.");
        }
        return p.CompanyId;
    }

    private async Task FailAsync(Payment p, Invoice inv, string reason)
    {
        p.Status = PaymentStatus.Failed;
        p.FailureReason = reason;
        p.Events.Add(new PaymentEvent { Name = "Payment Failed" });
        await _db.SaveChangesAsync();
        await _audit.LogAsync("PaymentFailed", "Invoice", inv.Number, reason);
    }
}
