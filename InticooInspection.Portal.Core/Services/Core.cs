using System.Security.Cryptography;
using System.Text;
using InticooInspection.Portal.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InticooInspection.Infrastructure.Tenancy;

namespace InticooInspection.Portal.Services;

public class AuditService
{
    private readonly PortalDbContext _db;
    private readonly IHttpContextAccessor _http;
    public AuditService(PortalDbContext db, IHttpContextAccessor http) { _db = db; _http = http; }

    public async Task LogAsync(string action, string entity, string? entityId = null, string? detail = null, string? actor = null)
    {
        var ctx = _http.HttpContext;
        _db.AuditLogs.Add(new AuditLog
        {
            Actor = actor ?? ctx?.User?.Identity?.Name ?? "system",
            Action = action, Entity = entity, EntityId = entityId,
            Detail = detail is { Length: > 1000 } ? detail[..1000] : detail,
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();
    }
}

/// <summary>Cấu hình nền tảng lưu trong bảng Settings, có giá trị mặc định.</summary>
public class SettingsService
{
    public static readonly Dictionary<string, string> Defaults = new()
    {
        ["platform.name"] = "Inticoo Global Services",
        ["platform.website"] = "https://inticoo.com",
        ["platform.supportEmail"] = "support@inticoo.com",
        ["platform.supportPhone"] = "+60 3 1234 5678",
        ["reg.allowSelf"] = "true",
        ["reg.requireEmailVerification"] = "true",
        ["reg.requireAdminApproval"] = "false",
        ["reg.allowFreeTrial"] = "true",
        ["reg.trialDays"] = "7",
        ["notify.newCompany"] = "true",
        ["notify.paymentReceived"] = "true",
        ["notify.paymentFailed"] = "true",
        ["notify.subscriptionExpiry"] = "true",
        ["system.maintenance"] = "false",
        ["system.backup"] = "true",
        ["system.backupRetention"] = "30",
        ["default.language"] = "en",
        ["default.plan"] = "business",
        ["default.storageGb"] = "200",
        ["format.timezone"] = "(UTC+08:00) Kuala Lumpur, Singapore",
        ["format.date"] = "dd MMM yyyy",
        ["format.time"] = "24h",
        ["billing.currency"] = "USD",
        ["billing.taxPercent"] = "0",
        ["billing.dueDays"] = "30",
        ["billing.bankInfo"] = "Bank: (to be configured)\nAccount name: Inticoo Global Services\nAccount no: -\nSWIFT: -",
        ["invoice.prefix"] = "INV",
        ["invoice.footer"] = "Thank you for choosing Inticoo.",
        ["email.welcomeSubject"] = "Welcome to Inticoo",
        ["email.welcomeBody"] = "Hello {name}, your Inticoo account for {company} is ready.",
        ["email.invoiceSubject"] = "Your Inticoo invoice {invoice}",
        ["email.invoiceBody"] = "Hello {name}, invoice {invoice} for {amount} is attached.",
        ["lang.en"] = "true", ["lang.zh"] = "true", ["lang.vi"] = "true", ["lang.ms"] = "true",
        ["security.minPasswordLength"] = "8",
        ["security.maxFailedLogins"] = "5",
        ["security.lockoutMinutes"] = "15",
        ["security.sessionMinutes"] = "60",
        ["integration.stripeEnabled"] = "false",
        // Đo sử dụng + hạn mức gói (API đọc các khóa usage.* trực tiếp từ sup.Settings)
        ["usage.intervalHours"] = "6",          // chu kỳ API tự đo dung lượng / người dùng
        ["usage.warnPercent"] = "80",           // ngưỡng cảnh báo sắp hết hạn mức
        ["usage.inactiveDays"] = "30",          // không đăng nhập quá N ngày = công ty không hoạt động
        ["usage.externalCapacityGb"] = "0",     // dung lượng thuê ngoài cộng thêm vào ổ đĩa server
        ["usage.enforce"] = "true",             // chặn thêm người dùng / upload khi vượt hạn mức gói
        ["usage.measureRequestedAt"] = "",      // Super Admin bấm "Measure now" → API đo ngay
        ["usage.lastRunAt"] = "",
    };

    private readonly PortalDbContext _db;
    public SettingsService(PortalDbContext db) => _db = db;

    public async Task<Dictionary<string, string>> AllAsync()
    {
        var d = new Dictionary<string, string>(Defaults);
        foreach (var s in await _db.Settings.AsNoTracking().ToListAsync()) d[s.Key] = s.Value;
        return d;
    }

    public async Task<string> GetAsync(string key)
    {
        var s = await _db.Settings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key);
        return s?.Value ?? Defaults.GetValueOrDefault(key, "");
    }

    public async Task<bool> GetBoolAsync(string key) => (await GetAsync(key)).Equals("true", StringComparison.OrdinalIgnoreCase);
    public async Task<int> GetIntAsync(string key, int fallback = 0) => int.TryParse(await GetAsync(key), out var v) ? v : fallback;

    public async Task SetManyAsync(IDictionary<string, string> values)
    {
        foreach (var (k, v) in values)
        {
            if (!Defaults.ContainsKey(k)) continue;       // chỉ nhận khóa đã biết
            var row = await _db.Settings.FindAsync(k);
            if (row == null) _db.Settings.Add(new Setting { Key = k, Value = v ?? "" });
            else row.Value = v ?? "";
        }
        await _db.SaveChangesAsync();
    }
}

public class PasswordService
{
    private readonly PasswordHasher<object> _hasher = new();
    public string Hash(string password) => _hasher.HashPassword(new object(), password);
    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(new object(), hash, password) != PasswordVerificationResult.Failed;

    /// <summary>Tối thiểu 8 ký tự gồm chữ hoa, chữ thường, số và ký hiệu.</summary>
    public static string? Validate(string? pw, int minLen = 8)
    {
        if (string.IsNullOrEmpty(pw) || pw.Length < minLen) return $"Password must be at least {minLen} characters.";
        // Khớp chính sách mật khẩu của app chính (cần hoa + số + ký hiệu) để admin đăng nhập app được ngay
        if (!pw.Any(char.IsUpper) || !pw.Any(char.IsLower) || !pw.Any(char.IsDigit) || pw.All(char.IsLetterOrDigit))
            return "Password must contain upper and lower case letters, a number and a symbol.";
        return null;
    }
}

public static class Slug
{
    public static string From(string name)
    {
        var norm = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in norm)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (c is 'đ' or 'Đ') { sb.Append('d'); continue; }
            if (char.IsAsciiLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        var s = sb.ToString().Trim('_');
        if (s.Length > 24) s = s[..24].TrimEnd('_');
        return s.Length == 0 ? "company" : s;
    }
}
