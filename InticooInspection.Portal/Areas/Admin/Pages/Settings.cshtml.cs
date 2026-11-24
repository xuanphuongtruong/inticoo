using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public record Field(string Key, string Label, string Type = "text", string? Hint = null, string[]? Options = null, int Min = 0, int Max = 100000);
public record Card(string Title, string Sub, Field[] Fields);

public class SettingsModel : AdminPageModel
{
    public static readonly (string Key, string Title)[] TabList =
    {
        ("general", "General"), ("plans", "Subscription Plans"), ("billing", "Payment & Billing"), ("invoice", "Invoice Settings"),
        ("email", "Email Templates"), ("security", "Security"), ("integrations", "Integrations"), ("system", "System")
    };

    private static readonly string[] Langs = { "en", "zh", "vi", "ms" };
    public static readonly Dictionary<string, Card[]> Tabs = new()
    {
        ["general"] = new[]
        {
            new Card("Platform Information", "Basic information about your Inticoo platform.", new[]
            {
                new Field("platform.name", "Company Name"), new Field("platform.website", "Website", "url"),
                new Field("platform.supportEmail", "Support Email", "email"), new Field("platform.supportPhone", "Support Phone")
            }),
            new Card("Company Registration Settings", "Configure customer self-registration options.", new[]
            {
                new Field("reg.allowSelf", "Allow Self-Registration", "bool", "Allow customers to register their own company accounts."),
                new Field("reg.requireAdminApproval", "Require Admin Approval", "bool", "Paid accounts wait in 'Pending Review' until a super admin approves them."),
            }),
            new Card("Default Settings", "Default configurations for new companies.", new[]
            {
                new Field("default.language", "Default Language", "select", null, Langs), new Field("default.plan", "Default Plan (registration form)", "select", null, new[] { "basic", "business", "professional" }),
                new Field("default.storageGb", "Default Storage Quota (GB)", "number", null, null, 1, 100000)
            }),
            new Card("System Maintenance", "Manage platform maintenance.", new[]
            {
                new Field("system.maintenance", "Maintenance Mode", "bool", "Temporarily disable the public website. This console stays available.")
            }),
        },
        ["billing"] = new[]
        {
            new Card("Payment & Billing", "How invoices are issued and paid.", new[]
            {
                new Field("billing.currency", "Currency", "select", "Prices are defined in USD.", new[] { "USD" }),
                new Field("billing.taxPercent", "Tax (%) added to new invoices", "number", "0 = no tax.", null, 0, 100),
                new Field("billing.dueDays", "Invoice due after (days)", "number", null, null, 1, 365),
                new Field("billing.bankInfo", "Bank transfer instructions", "textarea", "Shown to customers who choose Bank Transfer and on invoices.")
            })
        },
        ["invoice"] = new[]
        {
            new Card("Invoice Settings", "Numbering and footer of invoices.", new[]
            {
                new Field("invoice.prefix", "Invoice number prefix", "text", "Numbers look like PREFIX-2026-0001."), new Field("invoice.footer", "Invoice footer text", "textarea")
            })
        },
        ["email"] = new[]
        {
            new Card("Email Templates", "Templates for customer emails. Placeholders: {name} {company} {invoice} {amount}. Outgoing mail is not connected yet — templates are stored for when SMTP is configured.", new[]
            {
                new Field("email.welcomeSubject", "Welcome email — subject"), new Field("email.welcomeBody", "Welcome email — body", "textarea"),
                new Field("email.invoiceSubject", "Invoice email — subject"), new Field("email.invoiceBody", "Invoice email — body", "textarea")
            })
        },
        ["security"] = new[]
        {
            new Card("Security", "Sign-in policy for this console.", new[]
            {
                new Field("security.minPasswordLength", "Minimum password length", "number", "Applies to new passwords (min 8).", null, 8, 64),
                new Field("security.maxFailedLogins", "Failed sign-ins before lockout", "number", null, null, 3, 20),
                new Field("security.lockoutMinutes", "Lockout duration (minutes)", "number", null, null, 1, 1440),
                new Field("security.sessionMinutes", "Session length (minutes)", "number", "Applies to new sign-ins.", null, 5, 1440)
            })
        },
    };

    private readonly PortalDbContext _db;
    private readonly SettingsService _settings;
    private readonly AuditService _audit;
    private readonly IConfiguration _cfg;
    private readonly IWebHostEnvironment _env;
    private readonly IPaymentGateway _gateway;
    private readonly ProvisioningService _prov;
    public SettingsModel(PortalDbContext db, SettingsService settings, AuditService audit, IConfiguration cfg, IWebHostEnvironment env, IPaymentGateway gateway, ProvisioningService prov)
    { _db = db; _settings = settings; _audit = audit; _cfg = cfg; _env = env; _gateway = gateway; _prov = prov; }

    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "general";
    [BindProperty(SupportsGet = true)] public int? Edit { get; set; }
    public Dictionary<string, string> S { get; set; } = new();
    public List<Plan> Plans { get; set; } = new();
    public Dictionary<int, int> PlanUsage { get; set; } = new();
    public string DbServer = "", EnvName = "", PayMode = "", ProvMode = "";
    public int DemoCompanies;
    public bool Demo => DemoCompanies > 0;

    public async Task OnGetAsync()
    {
        if (!TabList.Any(t => t.Key == Tab)) Tab = "general";
        S = await _settings.AllAsync();
        if (Tab == "plans")
        {
            Plans = await _db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
            PlanUsage = (await _db.Subscriptions.Where(s => s.Status != SubscriptionStatus.Cancelled).GroupBy(s => s.PlanId).Select(g => new { g.Key, N = g.Count() }).ToListAsync()).ToDictionary(x => x.Key, x => x.N);
        }
        if (Tab is "system" or "integrations")
        {
            var b = new SqlConnectionStringBuilder(_cfg.GetConnectionString("Super"));
            DbServer = $"{b.DataSource} / {b.InitialCatalog}";   // không hiển thị user/mật khẩu
            EnvName = _env.EnvironmentName; PayMode = _gateway.Available ? "Test (simulated gateway)" : "Disabled"; ProvMode = _prov.Enabled ? "Database per company" : "Off";
            DemoCompanies = await _db.Companies.CountAsync(c => c.IsDemo);
        }
    }

    public async Task<IActionResult> OnPostSaveAsync(string tab)
    {
        if (!Tabs.TryGetValue(tab, out var cards)) return BadRequest();
        var form = Request.Form; var values = new Dictionary<string, string>();
        // Mỗi khối (card) là 1 form riêng → chỉ xử lý trường có trong form gửi lên, không đụng trường của khối khác
        // (trước đây duyệt cả tab: lưu khối dưới báo lỗi email trống, công tắc ở khối khác bị ghi thành false)
        foreach (var f in cards.SelectMany(c => c.Fields).Where(f => form.ContainsKey(f.Key)))
        {
            // checkbox: có hidden "false" đi kèm → lấy giá trị cuối
            var raw = form[f.Key].LastOrDefault()?.Trim() ?? "";
            switch (f.Type)
            {
                case "bool": values[f.Key] = form[f.Key].Contains("true") ? "true" : "false"; break;
                case "number":
                    if (!int.TryParse(raw, out var n) || n < f.Min || n > f.Max) { SetError($"{f.Label}: enter a number between {f.Min} and {f.Max}."); return RedirectToPage(new { tab }); }
                    values[f.Key] = n.ToString(); break;
                case "select":
                    if (f.Options != null && !f.Options.Contains(raw)) { SetError($"{f.Label}: invalid value."); return RedirectToPage(new { tab }); }
                    values[f.Key] = raw; break;
                case "email":
                    if (!System.Net.Mail.MailAddress.TryCreate(raw, out _)) { SetError($"{f.Label}: invalid email."); return RedirectToPage(new { tab }); }
                    values[f.Key] = raw; break;
                case "url":
                    if (raw != "" && !(raw.StartsWith("http://") || raw.StartsWith("https://"))) { SetError($"{f.Label}: must start with http(s)://"); return RedirectToPage(new { tab }); }
                    values[f.Key] = raw; break;
                default:
                    values[f.Key] = raw.Length > 1900 ? raw[..1900] : raw; break;
            }
        }
        if (values.Count == 0) { SetError("Nothing to save."); return RedirectToPage(new { tab }); }
        await _settings.SetManyAsync(values);
        await _audit.LogAsync("Settings.update", "Setting", tab, string.Join(',', values.Keys));
        SetFlash("Settings saved.");
        return RedirectToPage(new { tab });
    }

    public async Task<IActionResult> OnPostPlanAsync(int? id, string code, string name, string tagline, decimal monthly, decimal annual, int maxUsers, int storageGb, bool popular, bool active, int sortOrder)
    {
        code = (code ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || monthly < 0 || annual < 0 || maxUsers < 1 || storageGb < 1) { SetError("Fill in name, prices, users and storage with valid values."); return RedirectToPage(new { tab = "plans" }); }
        Plan? p;
        if (id == null)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z0-9_-]{2,30}$")) { SetError("Plan code: 2-30 characters a-z, 0-9, - or _."); return RedirectToPage(new { tab = "plans" }); }
            if (await _db.Plans.AnyAsync(x => x.Code == code)) { SetError("Plan code already exists."); return RedirectToPage(new { tab = "plans" }); }
            p = new Plan { Code = code }; _db.Plans.Add(p);
        }
        else p = await _db.Plans.FindAsync(id);
        if (p == null) return NotFound();
        p.Name = name.Trim(); p.Tagline = (tagline ?? "").Trim(); p.MonthlyPrice = monthly; p.AnnualPrice = annual; p.MaxUsers = maxUsers; p.StorageGb = storageGb;
        // Checkbox có hidden "false" đứng trước → form gửi "false,true"; bind bool chỉ lấy giá trị đầu (luôn false) nên đọc trực tiếp
        popular = Request.Form["popular"].Contains("true"); active = Request.Form["active"].Contains("true");
        p.IsPopular = popular; p.IsActive = active; p.SortOrder = sortOrder;
        if (popular) foreach (var o in await _db.Plans.Where(x => x.IsPopular && x.Id != p.Id).ToListAsync()) o.IsPopular = false;   // chỉ 1 gói "Most Popular"
        await _db.SaveChangesAsync();
        await _audit.LogAsync(id == null ? "Plan.create" : "Plan.update", "Plan", p.Code, $"{p.Name} {p.MonthlyPrice}/{p.AnnualPrice}");
        SetFlash("Plan saved. Existing subscriptions keep their current price until changed.");
        return RedirectToPage(new { tab = "plans" });
    }

    public async Task<IActionResult> OnPostDeletePlanAsync(int id)
    {
        var p = await _db.Plans.FindAsync(id);
        if (p == null) return NotFound();
        if (await _db.Subscriptions.AnyAsync(s => s.PlanId == id)) { SetError("This plan is used by subscriptions — deactivate it instead of deleting."); return RedirectToPage(new { tab = "plans" }); }
        _db.Plans.Remove(p); await _db.SaveChangesAsync();
        await _audit.LogAsync("Plan.delete", "Plan", p.Code);
        SetFlash("Plan deleted.");
        return RedirectToPage(new { tab = "plans" });
    }

    public async Task<IActionResult> OnPostPurgeDemoAsync()
    {
        await DemoSeeder.PurgeAsync(_db);
        await _audit.LogAsync("Demo.purge", "Company");
        SetFlash("Demo data removed.");
        return RedirectToPage(new { tab = "system" });
    }
}
