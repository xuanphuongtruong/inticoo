using System.ComponentModel.DataAnnotations;
using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.SuperAdmin.Pages;

public class CompaniesModel : AdminPageModel
{
    private readonly PortalDbContext _db;
    private readonly BillingService _billing;
    private readonly ProvisioningService _prov;
    private readonly AuditService _audit;
    private readonly PasswordService _pw;
    private readonly IConfiguration _cfg;
    private readonly UsageReportService _usage;
    private readonly SettingsService _settings;
    public CompaniesModel(PortalDbContext db, BillingService billing, ProvisioningService prov, AuditService audit, PasswordService pw, IConfiguration cfg, UsageReportService usage, SettingsService settings)
    { _db = db; _billing = billing; _prov = prov; _audit = audit; _pw = pw; _cfg = cfg; _usage = usage; _settings = settings; }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }
    [BindProperty(SupportsGet = true)] public string? Plan { get; set; }
    [BindProperty(SupportsGet = true)] public string? Country { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNo { get; set; } = 1;
    [BindProperty(SupportsGet = true)] public int Size { get; set; } = 10;
    [BindProperty(SupportsGet = true)] public Guid? Id { get; set; }
    [BindProperty(SupportsGet = true)] public string? Dt { get; set; }      // tab chi tiết
    [BindProperty(SupportsGet = true)] public string? Mode { get; set; }    // add / edit

    public class Row { public Company C { get; set; } = null!; public string? PlanName { get; set; } public DateTime? Next { get; set; } public int Users { get; set; } }
    public List<Row> Rows { get; set; } = new();
    public PagerInfo Pager { get; set; } = new(1, 10, 0);
    public int All, ActiveN, PendingN, OverdueN, PausedN, CancelledN, NewThisMonth;
    public List<Plan> Plans { get; set; } = new();
    public List<string> Countries { get; set; } = new();
    public Company? Sel { get; set; }
    public Subscription? SelSub { get; set; }
    public List<Invoice> SelInvoices { get; set; } = new();
    public List<PlatformModule> AllModules { get; set; } = new();
    public UsageRow? SelUsage { get; set; }
    public CompanyUsageSnapshot? SelSnap { get; set; }
    public List<(DateTime Day, double Gb)> SelTrend { get; set; } = new();
    public UsageReportService U => _usage;
    public string PaymentLink => Sel == null ? "" : $"{Request.Scheme}://{Request.Host}/register/complete/";

    [BindProperty] public CompanyForm Form { get; set; } = new();
    public class CompanyForm
    {
        [Required, StringLength(200)] public string Name { get; set; } = "";
        [Required, StringLength(2)] public string CountryCode { get; set; } = "";
        [StringLength(300)] public string Address { get; set; } = "";
        [Required, StringLength(150)] public string ContactPerson { get; set; } = "";
        [Required, EmailAddress, StringLength(200)] public string Email { get; set; } = "";
        [StringLength(40)] public string Phone { get; set; } = "";
        [StringLength(200)] public string? Website { get; set; }
        [StringLength(1000)] public string? Notes { get; set; }
        public string PlanCode { get; set; } = "";   // trống → gói mặc định (Settings › default.plan)
        public string BillingCycle { get; set; } = "Monthly";
        public string? AdminPassword { get; set; }
        public bool MarkPaid { get; set; }
    }

    public async Task<HashSet<string>> PlanModuleCodes(int planId) =>
        (await _db.PlanModules.AsNoTracking().Where(pm => pm.PlanId == planId).Select(pm => pm.Module!.Code).ToListAsync()).ToHashSet();

    private static IQueryable<Company> ApplyStatus(IQueryable<Company> q, string? s) => s switch
    {
        null or "" => q,
        "Pending" => q.Where(c => c.Status == CompanyStatus.PendingPayment || c.Status == CompanyStatus.PendingReview),
        _ => q.Where(c => c.Status == s)
    };

    private IQueryable<Company> Filter()
    {
        var q = _db.Companies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(Q)) { var t = Q.Trim(); q = q.Where(c => c.Name.Contains(t) || c.Email.Contains(t) || c.ContactPerson.Contains(t) || c.Country.Contains(t)); }
        if (!string.IsNullOrEmpty(Plan)) q = q.Where(c => c.Subscriptions.Any(s => s.Plan!.Code == Plan && s.Status != SubscriptionStatus.Cancelled));
        if (!string.IsNullOrEmpty(Country)) q = q.Where(c => c.Country == Country);
        return ApplyStatus(q, Status);
    }

    public async Task OnGetAsync()
    {
        PageNo = ClampPage(PageNo); Size = ClampSize(Size);
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var baseQ = _db.Companies.AsNoTracking();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var counts = await baseQ.GroupBy(c => c.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        int N(string k) => counts.FirstOrDefault(x => x.Key == k)?.N ?? 0;
        All = counts.Sum(x => x.N); ActiveN = N(CompanyStatus.Active); OverdueN = N(CompanyStatus.Overdue);
        PendingN = N(CompanyStatus.PendingPayment) + N(CompanyStatus.PendingReview); PausedN = N(CompanyStatus.Paused); CancelledN = N(CompanyStatus.Cancelled);
        NewThisMonth = await baseQ.CountAsync(c => c.RegisteredAt >= monthStart);

        var q = Filter();
        var total = await q.CountAsync();
        Pager = new PagerInfo(PageNo, Size, total);
        Rows = await q.OrderByDescending(c => c.RegisteredAt).Skip((PageNo - 1) * Size).Take(Size).Select(c => new Row
        {
            C = c,
            PlanName = c.Subscriptions.OrderByDescending(s => s.Id).Select(s => s.Plan!.Name).FirstOrDefault(),
            Next = c.Subscriptions.OrderByDescending(s => s.Id).Select(s => s.NextBillingDate).FirstOrDefault(),
            Users = c.Users.Count
        }).ToListAsync();
        Plans = await _db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        if (string.IsNullOrEmpty(Form.PlanCode))
        {
            var def = await _settings.GetAsync("default.plan");
            Form.PlanCode = Plans.FirstOrDefault(p => p.IsActive && p.Code == def)?.Code ?? Plans.FirstOrDefault(p => p.IsActive)?.Code ?? "";
        }
        Countries = await _db.Companies.AsNoTracking().Select(c => c.Country).Distinct().OrderBy(x => x).ToListAsync();
        AllModules = await _db.Modules.AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync();

        var selId = Id ?? Rows.FirstOrDefault()?.C.Id;
        if (selId != null && Mode != "add")
        {
            Sel = await _db.Companies.AsNoTracking().Include(c => c.Modules).Include(c => c.Users)
                .Include(c => c.Subscriptions).ThenInclude(s => s.Plan).FirstOrDefaultAsync(c => c.Id == selId);
            SelSub = Sel?.Subscriptions.OrderByDescending(s => s.Id).FirstOrDefault();
            if (Sel != null)
            {
                SelInvoices = await _db.Invoices.AsNoTracking().Where(i => i.CompanyId == Sel.Id).OrderByDescending(i => i.InvoiceDate).Take(10).ToListAsync();
                if (Dt == "usage")
                {
                    SelUsage = (await _usage.RowsAsync(includeCancelled: true, includeLegacy: false)).FirstOrDefault(x => x.CompanyId == Sel.Id);
                    SelSnap = await _db.UsageSnapshots.AsNoTracking().Where(s => s.CompanyId == Sel.Id).OrderByDescending(s => s.At).FirstOrDefaultAsync();
                    SelTrend = await _usage.TrendAsync(90, Sel.Code);
                }
                if (Mode == "edit") Form = new CompanyForm { Name = Sel.Name, CountryCode = Sel.CountryCode, Address = Sel.Address, ContactPerson = Sel.ContactPerson, Email = Sel.Email, Phone = Sel.Phone, Website = Sel.Website, Notes = Sel.Notes };
            }
        }
    }

    /// <summary>Tạm bỏ chặn hạn mức gói (người dùng / dung lượng) đến hết ngày chọn; bỏ trống = áp hạn mức lại.</summary>
    public async Task<IActionResult> OnPostOverrideAsync(Guid id, DateTime? until)
    {
        var c = await _db.Companies.FindAsync(id);
        if (c == null) return NotFound();
        if (until != null && until.Value.Date < DateTime.UtcNow.Date) { SetError("Choose a date from today onwards."); return RedirectToPage(new { id, dt = "usage" }); }
        c.LimitOverrideUntil = until?.Date.AddDays(1).AddSeconds(-1);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Company.limitOverride", "Company", c.Code, until == null ? "cleared" : $"until {until:yyyy-MM-dd}");
        SetFlash(until == null ? "Plan limits are enforced again for this company." : $"Plan limits are not enforced for this company until {until:dd MMM yyyy} (takes effect within 2 minutes).");
        return RedirectToPage(new { id, dt = "usage" });
    }

    public async Task<IActionResult> OnPostMeasureAsync(Guid id)
    {
        await _usage.RequestMeasureAsync();
        SetFlash("Measurement requested — new figures appear within about a minute.");
        return RedirectToPage(new { id, dt = "usage" });
    }

    public async Task<IActionResult> OnPostActionAsync(Guid id, string act)
    {
        var c = await _db.Companies.Include(x => x.Subscriptions).FirstOrDefaultAsync(x => x.Id == id);
        if (c == null) return NotFound();
        var sub = c.Subscriptions.OrderByDescending(s => s.Id).FirstOrDefault();
        switch (act)
        {
            case "pause":
                c.Status = CompanyStatus.Paused; if (sub != null) sub.Status = SubscriptionStatus.Paused; SetFlash($"{c.Name} paused."); break;
            case "resume":
            case "approve":
                c.Status = CompanyStatus.Active; if (sub != null) { sub.Status = SubscriptionStatus.Active; sub.NextBillingDate ??= BillingService.AddCycle(DateTime.UtcNow, sub.BillingCycle); }
                SetFlash($"{c.Name} activated."); break;
            case "cancel":
                if (sub != null) { sub.Status = SubscriptionStatus.Cancelled; sub.CancelledAt = DateTime.UtcNow; sub.AutoRenew = false; }
                c.Status = CompanyStatus.Cancelled; SetFlash($"{c.Name} cancelled."); break;
            case "paymentlink":
            {
                var reg = await _db.Registrations.Where(r => r.CompanyId == id).OrderByDescending(r => r.CreatedAt).FirstOrDefaultAsync();
                SetFlash(reg == null ? "No registration found for this company." : $"Payment link: {Request.Scheme}://{Request.Host}/register/complete/{reg.Id}");
                return RedirectToPage(new { id });
            }
            default: return BadRequest();
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Company." + act, "Company", c.Id.ToString(), c.Name);
        // Kích hoạt công ty chưa có DB → cấp hạ tầng riêng
        if (act is "resume" or "approve" && c.ProvisionStatus != ProvisionStatus.Provisioned && _prov.Enabled)
        {
            var ok = await _prov.ProvisionAsync(c);
            if (!ok) SetError("Company activated but database provisioning failed: " + c.ProvisionError);
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostProvisionAsync(Guid id)
    {
        var c = await _db.Companies.FindAsync(id);
        if (c == null) return NotFound();
        if (!_prov.Enabled) { SetError("Provisioning is disabled (Provisioning:Mode)."); return RedirectToPage(new { id }); }
        var ok = await _prov.ProvisionAsync(c);
        if (ok) SetFlash($"Database {c.DatabaseName} is ready."); else SetError("Provisioning failed: " + c.ProvisionError);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, bool dropDatabase)
    {
        var c = await _db.Companies.FindAsync(id);
        if (c == null) return NotFound();
        // Chỉ tính thanh toán thật: thanh toán qua cổng giả lập (Provider "Test", Payments:Mode=Test) không phải chứng từ tài chính
        var paid = await _db.Payments.AnyAsync(p => p.CompanyId == id && p.Status == PaymentStatus.Paid && p.Provider != "Test");
        if (paid && !c.IsDemo) { SetError("This company has paid payments. Cancel the subscription instead of deleting (financial records must be kept)."); return RedirectToPage(new { id }); }
        if (c.DatabaseName != null)
        {
            if (!dropDatabase) { SetError("This company has its own database. Tick 'also delete its database' to confirm."); return RedirectToPage(new { id }); }
            try { await _prov.DropTenantAsync(c); } catch (Exception ex) { SetError("Could not delete database: " + ex.Message); return RedirectToPage(new { id }); }
        }
        _db.Companies.Remove(c);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Company.delete", "Company", id.ToString(), c.Name);
        SetFlash($"{c.Name} deleted.");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid? id)
    {
        if (!CountryList.ByCode.TryGetValue(Form.CountryCode ?? "", out var country)) ModelState.AddModelError("Form.CountryCode", "Select a country.");
        if (!ModelState.IsValid) { Mode = id == null ? "add" : "edit"; Id = id; PageNo = 1; Size = 10; await LoadAsync(); return Page(); }
        var email = Form.Email.Trim().ToLowerInvariant();

        if (id != null)
        {
            var c = await _db.Companies.FindAsync(id);
            if (c == null) return NotFound();
            c.Name = Form.Name.Trim(); c.CountryCode = Form.CountryCode!; c.Country = country!; c.Address = Form.Address?.Trim() ?? "";
            c.ContactPerson = Form.ContactPerson.Trim(); c.Email = email; c.Phone = Form.Phone?.Trim() ?? ""; c.Website = string.IsNullOrWhiteSpace(Form.Website) ? null : Form.Website.Trim(); c.Notes = Form.Notes;
            await _db.SaveChangesAsync();
            await _audit.LogAsync("Company.update", "Company", c.Id.ToString(), c.Name);
            SetFlash("Company updated.");
            return RedirectToPage(new { id });
        }

        // Thêm công ty thủ công (sale tạo hộ khách)
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Code == Form.PlanCode && p.IsActive);
        if (plan == null || Form.BillingCycle is not ("Monthly" or "Annual")) { SetError("Invalid plan."); return RedirectToPage(); }
        var pwErr = PasswordService.Validate(Form.AdminPassword);
        if (pwErr != null) { SetError("Admin password: " + pwErr); return RedirectToPage(new { mode = "add" }); }
        if (await _db.Companies.AnyAsync(x => x.Email == email && x.Status != CompanyStatus.Cancelled)) { SetError("A company with this email already exists."); return RedirectToPage(new { mode = "add" }); }
        var baseCode = Slug.From(Form.Name); var code = baseCode;
        for (var n = 2; await _db.Companies.AnyAsync(x => x.Code == code); n++) code = $"{baseCode}_{n}";
        var co = new Company { Code = code, Name = Form.Name.Trim(), CountryCode = Form.CountryCode!, Country = country!, Address = Form.Address?.Trim() ?? "", ContactPerson = Form.ContactPerson.Trim(), Email = email, Phone = Form.Phone?.Trim() ?? "", Website = string.IsNullOrWhiteSpace(Form.Website) ? null : Form.Website.Trim(), Notes = Form.Notes };
        _db.Companies.Add(co);
        _db.Registrations.Add(new Registration { Company = co, AdminName = co.ContactPerson, AdminEmail = email, AdminPasswordHash = _pw.Hash(Form.AdminPassword!), EmailVerified = true });
        _db.CompanyUsers.Add(new CompanyUser { Company = co, FullName = co.ContactPerson, Email = email, Role = "Administrator", Status = "Pending", Phone = co.Phone });
        await _db.SaveChangesAsync();
        await _billing.SyncCompanyModulesAsync(co.Id, plan.Id);
        var (_, inv) = await _billing.CreateSubscriptionWithInvoiceAsync(co, plan, Form.BillingCycle);
        if (Form.MarkPaid)
        {
            var p = await _billing.AddPaymentAsync(inv, "BankTransfer", "Manual", PaymentStatus.Pending);
            await _billing.SettleInvoiceAsync(inv, p);
        }
        await _audit.LogAsync("Company.create", "Company", co.Id.ToString(), co.Name);
        SetFlash("Company created" + (Form.MarkPaid ? " and activated." : ". An invoice is waiting for payment."));
        return RedirectToPage(new { id = co.Id });
    }

    public async Task<IActionResult> OnPostModulesAsync(Guid id, List<string>? modules)
    {
        var c = await _db.Companies.Include(x => x.Subscriptions).FirstOrDefaultAsync(x => x.Id == id);
        var sub = c?.Subscriptions.OrderByDescending(s => s.Id).FirstOrDefault();
        if (c == null || sub == null) return NotFound();
        await _billing.SyncCompanyModulesAsync(id, sub.PlanId, modules ?? new());
        await _audit.LogAsync("Company.modules", "Company", id.ToString(), string.Join(',', modules ?? new()));
        SetFlash("Modules updated.");
        return RedirectToPage(new { id, dt = "modules" });
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var rows = await Filter().OrderByDescending(c => c.RegisteredAt).Select(c => new
        {
            c.Name, c.Country, c.ContactPerson, c.Email, c.Phone, c.Status, c.RegisteredAt, c.StorageUsedGb,
            Plan = c.Subscriptions.OrderByDescending(s => s.Id).Select(s => s.Plan!.Name).FirstOrDefault(),
            Next = c.Subscriptions.OrderByDescending(s => s.Id).Select(s => s.NextBillingDate).FirstOrDefault(),
            Users = c.Users.Count
        }).ToListAsync();
        await _audit.LogAsync("Export", "Company", null, $"{rows.Count} rows");
        return Export.Xlsx("Companies", new[] { "Company", "Country", "Contact", "Email", "Phone", "Plan", "Status", "Registered", "Next Billing", "Users", "Storage GB" },
            rows.Select(r => new object?[] { r.Name, r.Country, r.ContactPerson, r.Email, r.Phone, r.Plan, r.Status, r.RegisteredAt, r.Next, r.Users, r.StorageUsedGb }),
            $"companies-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }
}
