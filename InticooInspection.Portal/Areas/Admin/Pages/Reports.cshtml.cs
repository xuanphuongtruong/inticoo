using System.Globalization;
using System.Text;
using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Areas.Admin.Pages;

public class ReportsModel : AdminPageModel
{
    public static readonly (string Key, string Title, string Icon)[] TabList =
    {
        ("overview", "Overview", "chart"), ("revenue", "Revenue & Payments", "dollar"), ("subscriptions", "Subscriptions", "card"), ("companies", "Companies", "building"),
        ("users", "Users", "users"), ("modules", "Module Usage", "grid"), ("storage", "Storage Usage", "cloud"), ("invoices", "Invoices", "receipt")
    };

    private readonly PortalDbContext _db;
    private readonly ReportService _reports;
    private readonly AuditService _audit;
    public ReportsModel(PortalDbContext db, ReportService reports, AuditService audit) { _db = db; _reports = reports; _audit = audit; }

    [BindProperty(SupportsGet = true)] public string Tab { get; set; } = "overview";
    [BindProperty(SupportsGet = true)] public DateTime? From { get; set; }
    [BindProperty(SupportsGet = true)] public DateTime? To { get; set; }
    [BindProperty(SupportsGet = true)] public string? Country { get; set; }
    [BindProperty(SupportsGet = true)] public string? Plan { get; set; }
    [BindProperty(SupportsGet = true)] public string? Status { get; set; }

    public DashboardData D { get; set; } = new();
    public string[] Headers { get; set; } = Array.Empty<string>();
    public List<object?[]> Rows { get; set; } = new();
    public string Summary { get; set; } = "";
    public List<string> Countries { get; set; } = new();
    public List<Plan> Plans { get; set; } = new();
    public List<(string Name, decimal Mrr, int Count)> TopRevenue { get; set; } = new();
    public Dictionary<string, int> ByCountry { get; set; } = new();
    public int TotalInvoices, PaidInvoices;
    public DateTime RangeFrom, RangeTo;

    private void NormalizeRange()
    {
        var now = DateTime.UtcNow.Date;
        RangeFrom = From?.Date ?? new DateTime(now.Year, now.Month, 1);
        RangeTo = To?.Date ?? now;
        if (RangeTo < RangeFrom) (RangeFrom, RangeTo) = (RangeTo, RangeFrom);
    }

    public async Task OnGetAsync()
    {
        if (!TabList.Any(t => t.Key == Tab)) Tab = "overview";
        NormalizeRange();
        Countries = await _db.Companies.AsNoTracking().Select(c => c.Country).Distinct().OrderBy(x => x).ToListAsync();
        Plans = await _db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        if (Tab == "overview") await LoadOverviewAsync();
        else await BuildTableAsync();
    }

    private async Task LoadOverviewAsync()
    {
        {
            D = await _reports.DashboardAsync();
            ByCountry = (await _db.Companies.AsNoTracking().GroupBy(c => c.Country).Select(g => new { g.Key, N = g.Count() }).OrderByDescending(x => x.N).Take(6).ToListAsync()).ToDictionary(x => x.Key, x => x.N);
            TotalInvoices = await _db.Invoices.CountAsync(); PaidInvoices = await _db.Invoices.CountAsync(i => i.Status == InvoiceStatus.Paid);
            var subs = await _db.Subscriptions.AsNoTracking().Where(s => s.Status == SubscriptionStatus.Active).Select(s => new { s.Company!.Name, s.Price, s.BillingCycle }).ToListAsync();
            TopRevenue = subs.GroupBy(s => s.Name).Select(g => (g.Key, g.Sum(x => x.BillingCycle == "Annual" ? x.Price / 12 : x.Price), g.Count())).OrderByDescending(x => x.Item2).Take(10).ToList();
        }
    }

    // Bảng tóm tắt cho tab Overview (chỉ số + Top 10 doanh thu + phân bố gói/quốc gia/thanh toán) để xuất Excel/CSV
    private void BuildOverviewTable()
    {
        Headers = new[] { "Section", "Item", "Value" };
        Rows = new List<object?[]>
        {
            new object?[] { "KPI", "Total Companies", D.TotalCompanies },
            new object?[] { "KPI", "New Companies This Month", D.NewCompaniesThisMonth },
            new object?[] { "KPI", "Active Subscriptions", D.ActiveSubscriptions },
            new object?[] { "KPI", "Total Revenue (MRR, USD)", D.Mrr },
            new object?[] { "KPI", "MRR Last Month (USD)", D.MrrLastMonth },
            new object?[] { "KPI", "Total Invoices", TotalInvoices },
            new object?[] { "KPI", "Paid Invoices", PaidInvoices },
            new object?[] { "KPI", "Storage Used (GB)", D.StorageUsedGb },
        };
        foreach (var m in D.MonthlyRevenue) Rows.Add(new object?[] { "Monthly Revenue (USD)", m.Label, m.Value });
        foreach (var p in D.PlanDistribution) Rows.Add(new object?[] { "Subscriptions by Plan", p.Name, p.Count });
        foreach (var kv in ByCountry) Rows.Add(new object?[] { "Companies by Country", kv.Key, kv.Value });
        foreach (var p in D.PaymentStatus) Rows.Add(new object?[] { "Payment Status (this month)", p.Name, p.Count });
        foreach (var t in TopRevenue) Rows.Add(new object?[] { "Top Companies by Revenue (MRR, USD)", t.Name, t.Mrr });
    }

    private async Task BuildTableAsync()
    {
        var to = RangeTo.AddDays(1);
        switch (Tab)
        {
            case "revenue":
            {
                var q = _db.Payments.AsNoTracking().Include(p => p.Company).Include(p => p.Invoice).Where(p => p.CreatedAt >= RangeFrom && p.CreatedAt < to);
                if (!string.IsNullOrEmpty(Country)) q = q.Where(p => p.Company!.Country == Country);
                if (!string.IsNullOrEmpty(Plan)) q = q.Where(p => p.Invoice!.PlanName == Plan);
                if (!string.IsNullOrEmpty(Status)) q = q.Where(p => p.Status == Status);
                var list = await q.OrderByDescending(p => p.CreatedAt).ToListAsync();
                Headers = new[] { "Date", "Transaction ID", "Company", "Invoice", "Plan", "Method", "Amount (USD)", "Status" };
                Rows = list.Select(p => new object?[] { p.CreatedAt, p.TransactionId, p.Company!.Name, p.Invoice!.Number, p.Invoice.PlanName, p.Method, p.Amount, p.Status }).ToList();
                Summary = $"{list.Count} payments · collected {AdminUi.Money(list.Where(p => p.Status == PaymentStatus.Paid).Sum(p => p.Amount))} · refunded {AdminUi.Money(list.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.Amount))}";
                break;
            }
            case "subscriptions":
            {
                var q = _db.Subscriptions.AsNoTracking().Include(s => s.Company).Include(s => s.Plan).Where(s => s.StartDate >= RangeFrom && s.StartDate < to);
                if (!string.IsNullOrEmpty(Country)) q = q.Where(s => s.Company!.Country == Country);
                if (!string.IsNullOrEmpty(Plan)) q = q.Where(s => s.Plan!.Name == Plan);
                if (!string.IsNullOrEmpty(Status)) q = q.Where(s => s.Status == Status);
                var list = await q.OrderByDescending(s => s.StartDate).ToListAsync();
                Headers = new[] { "Company", "Country", "Plan", "Billing Cycle", "Price (USD)", "Start Date", "Next Billing", "Status" };
                Rows = list.Select(s => new object?[] { s.Company!.Name, s.Company.Country, s.Plan!.Name, s.BillingCycle, s.Price, s.StartDate, s.NextBillingDate, s.Status }).ToList();
                Summary = $"{list.Count} subscriptions started in range · MRR of these {AdminUi.Money(list.Where(s => s.Status == SubscriptionStatus.Active).Sum(ReportService.Monthly))}";
                break;
            }
            case "companies":
            {
                var q = _db.Companies.AsNoTracking().Where(c => c.RegisteredAt >= RangeFrom && c.RegisteredAt < to);
                if (!string.IsNullOrEmpty(Country)) q = q.Where(c => c.Country == Country);
                if (!string.IsNullOrEmpty(Status)) q = q.Where(c => c.Status == Status);
                if (!string.IsNullOrEmpty(Plan)) q = q.Where(c => c.Subscriptions.Any(s => s.Plan!.Name == Plan));
                var list = await q.OrderByDescending(c => c.RegisteredAt).Select(c => new { c.Name, c.Country, c.ContactPerson, c.Email, c.Status, c.RegisteredAt, Users = c.Users.Count, c.StorageUsedGb }).ToListAsync();
                Headers = new[] { "Company", "Country", "Contact", "Email", "Status", "Registered", "Users", "Storage (GB)" };
                Rows = list.Select(c => new object?[] { c.Name, c.Country, c.ContactPerson, c.Email, c.Status, c.RegisteredAt, c.Users, c.StorageUsedGb }).ToList();
                Summary = $"{list.Count} companies registered in range";
                break;
            }
            case "users":
            {
                var q = _db.Companies.AsNoTracking().AsQueryable();
                if (!string.IsNullOrEmpty(Country)) q = q.Where(c => c.Country == Country);
                var list = await q.Select(c => new { c.Name, c.Country, Total = c.Users.Count, Active = c.Users.Count(u => u.Status == "Active"), Pending = c.Users.Count(u => u.Status == "Pending"), Inactive = c.Users.Count(u => u.Status == "Inactive"),
                    Max = c.Subscriptions.OrderByDescending(s => s.Id).Select(s => s.Plan!.MaxUsers).FirstOrDefault() }).OrderByDescending(x => x.Total).ToListAsync();
                Headers = new[] { "Company", "Country", "Users", "Active", "Pending", "Inactive", "Plan limit" };
                Rows = list.Select(c => new object?[] { c.Name, c.Country, c.Total, c.Active, c.Pending, c.Inactive, c.Max }).ToList();
                Summary = $"{list.Sum(x => x.Total)} users in {list.Count} companies";
                break;
            }
            case "modules":
            {
                var mods = await _db.Modules.AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync();
                var active = await _db.Companies.AsNoTracking().Where(c => c.Status == CompanyStatus.Active).Select(c => c.Id).ToListAsync();
                var cm = await _db.CompanyModules.AsNoTracking().Where(x => x.Enabled && active.Contains(x.CompanyId)).ToListAsync();
                Headers = new[] { "Module", "Active companies using it", "Share of active companies" };
                Rows = mods.Select(m => { var n = cm.Count(x => x.ModuleCode == m.Code); return new object?[] { m.Name, n, active.Count == 0 ? 0d : Math.Round(100.0 * n / active.Count, 1) }; }).ToList();
                Summary = $"{active.Count} active companies";
                break;
            }
            case "storage":
            {
                var q = _db.Companies.AsNoTracking().AsQueryable();
                if (!string.IsNullOrEmpty(Country)) q = q.Where(c => c.Country == Country);
                var list = await q.Select(c => new { c.Name, c.Country, c.StorageUsedGb, Quota = c.Subscriptions.OrderByDescending(s => s.Id).Select(s => s.Plan!.StorageGb).FirstOrDefault(), c.DatabaseName }).OrderByDescending(x => x.StorageUsedGb).ToListAsync();
                Headers = new[] { "Company", "Country", "Database", "Used (GB)", "Quota (GB)", "Usage %" };
                Rows = list.Select(c => new object?[] { c.Name, c.Country, c.DatabaseName, c.StorageUsedGb, c.Quota, c.Quota == 0 ? 0d : Math.Round(c.StorageUsedGb / c.Quota * 100, 1) }).ToList();
                Summary = $"{AdminUi.Num(list.Sum(x => x.StorageUsedGb))} GB used in total";
                break;
            }
            case "invoices":
            {
                var q = _db.Invoices.AsNoTracking().Include(i => i.Company).Where(i => i.InvoiceDate >= RangeFrom && i.InvoiceDate < to);
                if (!string.IsNullOrEmpty(Country)) q = q.Where(i => i.Company!.Country == Country);
                if (!string.IsNullOrEmpty(Plan)) q = q.Where(i => i.PlanName == Plan);
                if (!string.IsNullOrEmpty(Status)) q = q.Where(i => i.Status == Status);
                var list = await q.OrderByDescending(i => i.InvoiceDate).ToListAsync();
                Headers = new[] { "Invoice No.", "Company", "Plan", "Cycle", "Amount (USD)", "Invoice Date", "Due Date", "Status" };
                Rows = list.Select(i => new object?[] { i.Number, i.Company!.Name, i.PlanName, i.BillingCycle, i.Amount + i.Tax, i.InvoiceDate, i.DueDate, i.Status }).ToList();
                Summary = $"{list.Count} invoices · total {AdminUi.Money(list.Sum(i => i.Amount + i.Tax))} · paid {AdminUi.Money(list.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Amount + i.Tax))}";
                break;
            }
        }
    }

    public async Task<IActionResult> OnGetExportAsync(string format = "xlsx")
    {
        if (!TabList.Any(t => t.Key == Tab)) Tab = "overview";   // giống OnGet: tab lạ → Overview
        NormalizeRange();
        if (Tab == "overview") { await LoadOverviewAsync(); BuildOverviewTable(); }
        else await BuildTableAsync();
        await _audit.LogAsync("Export", "Report", Tab, $"{Rows.Count} rows, {format}");
        var name = $"report-{Tab}-{DateTime.UtcNow:yyyyMMdd}";
        if (format == "csv")
        {
            var sb = new StringBuilder("﻿");
            sb.AppendLine(string.Join(",", Headers.Select(Csv)));
            foreach (var r in Rows) sb.AppendLine(string.Join(",", r.Select(c => Csv(c switch { DateTime d => d.ToString("yyyy-MM-dd HH:mm"), decimal m => m.ToString("0.00", CultureInfo.InvariantCulture), double d => d.ToString(CultureInfo.InvariantCulture), null => "", var o => o.ToString() }))));
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", name + ".csv");
        }
        return Export.Xlsx(Tab, Headers, Rows, name + ".xlsx");
    }

    // Chống CSV injection (ô bắt đầu bằng = + - @) và escape dấu phẩy/nháy
    private static string Csv(string? s)
    {
        s ??= "";
        if (s.Length > 0 && "=+-@\t\r".Contains(s[0])) s = "'" + s;
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
