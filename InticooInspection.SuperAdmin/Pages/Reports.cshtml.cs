using System.Globalization;
using System.Text;
using InticooInspection.Portal.Data;
using InticooInspection.Portal.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.SuperAdmin.Pages;

public class ReportsModel : AdminPageModel
{
    public static readonly (string Key, string Title, string Icon)[] TabList =
    {
        ("overview", "Overview", "chart"), ("revenue", "Revenue & Payments", "dollar"), ("subscriptions", "Subscriptions", "card"), ("companies", "Companies", "building"),
        ("tenants", "Tenants", "building"), ("users", "Users & Activity", "users"), ("modules", "Module Usage", "grid"), ("storage", "Storage Usage", "cloud"),
        ("limits", "Plan Limits", "alert"), ("usage", "App Usage", "trend"), ("invoices", "Invoices", "receipt")
    };

    private readonly PortalDbContext _db;
    private readonly ReportService _reports;
    private readonly AuditService _audit;
    private readonly UsageReportService _usage;
    public ReportsModel(PortalDbContext db, ReportService reports, AuditService audit, UsageReportService usage) { _db = db; _reports = reports; _audit = audit; _usage = usage; }

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
    public record TopRow(Guid Id, string Name, string CountryCode, string Country, string Plan, decimal Mrr, string Status);
    public List<TopRow> TopRevenue { get; set; } = new();
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
            var u = await _usage.PlatformAsync(); D.Usage = u; D.StorageUsedGb = u.UsedGb; D.StorageCapacityGb = u.CapacityGb;
            ByCountry = (await _db.Companies.AsNoTracking().GroupBy(c => c.Country).Select(g => new { g.Key, N = g.Count() }).OrderByDescending(x => x.N).Take(6).ToListAsync()).ToDictionary(x => x.Key, x => x.N);
            TotalInvoices = await _db.Invoices.CountAsync(); PaidInvoices = await _db.Invoices.CountAsync(i => i.Status == InvoiceStatus.Paid);
            var subs = await _db.Subscriptions.AsNoTracking().Where(s => s.Status == SubscriptionStatus.Active)
                .Select(s => new { s.CompanyId, s.Company!.Name, s.Company.CountryCode, s.Company.Country, CoStatus = s.Company.Status, Plan = s.Plan!.Name, s.Price, s.BillingCycle }).ToListAsync();
            TopRevenue = subs.GroupBy(s => s.CompanyId).Select(g => new TopRow(g.Key, g.First().Name, g.First().CountryCode, g.First().Country, string.Join(", ", g.Select(x => x.Plan).Distinct()),
                    g.Sum(x => x.BillingCycle == "Annual" ? x.Price / 12 : x.Price), g.First().CoStatus))
                .OrderByDescending(x => x.Mrr).Take(10).ToList();
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

    // Số đo sử dụng (UsageReportService) + bộ lọc quốc gia / gói / trạng thái của trang Reports
    private async Task<List<UsageRow>> UsageRowsAsync()
    {
        var rows = (await _usage.RowsAsync(includeCancelled: Status == CompanyStatus.Cancelled)).Where(r => !r.IsDemo);
        if (!string.IsNullOrEmpty(Country)) rows = rows.Where(r => r.Country == Country);
        if (!string.IsNullOrEmpty(Plan)) rows = rows.Where(r => r.Plan == Plan);
        if (!string.IsNullOrEmpty(Status)) rows = rows.Where(r => r.Status == Status);
        return rows.ToList();
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
                var list = await UsageRowsAsync();
                Headers = new[] { "Company", "Country", "Plan", "Users", "Active users", "Plan limit", "% of limit", "Signed in 7 days", "Signed in 30 days", "Last activity", "Inactive" };
                Rows = list.OrderByDescending(r => r.ActiveUsers).Select(r => new object?[] { r.Name, r.Country, r.Plan, r.Users, r.ActiveUsers, r.MaxUsers == 0 ? null : r.MaxUsers,
                    r.MaxUsers == 0 ? null : Math.Round(r.UserPct, 1), r.ActiveUsers7d, r.ActiveUsers30d, r.LastActivityAt, r.IsLegacy ? null : _usage.IsInactive(r) ? "Yes" : "No" }).ToList();
                Summary = $"{list.Sum(x => x.Users)} users ({list.Sum(x => x.ActiveUsers)} active) in {list.Count(x => !x.IsLegacy)} companies · {list.Count(x => !x.IsLegacy && _usage.IsInactive(x))} inactive for {_usage.InactiveDays}+ days";
                break;
            }
            case "tenants":
            {
                // Theo tháng trong khoảng ngày: công ty mới, thuê bao hủy, số công ty đang thuê cuối tháng, tỷ lệ rời bỏ
                var comps = await _db.Companies.AsNoTracking().Where(c => !c.IsDemo).Select(c => new { c.RegisteredAt, c.Country, c.Status }).ToListAsync();
                var subs = await _db.Subscriptions.AsNoTracking().Where(s => !s.Company!.IsDemo)
                    .Select(s => new { s.CompanyId, s.StartDate, s.CancelledAt, s.Status, Plan = s.Plan!.Name, s.Company!.Country }).ToListAsync();
                if (!string.IsNullOrEmpty(Country)) { comps = comps.Where(c => c.Country == Country).ToList(); subs = subs.Where(s => s.Country == Country).ToList(); }
                if (!string.IsNullOrEmpty(Plan)) subs = subs.Where(s => s.Plan == Plan).ToList();
                static bool Live(string st, DateTime start, DateTime? canc, DateTime at) => st != SubscriptionStatus.PendingPayment && start <= at && (canc == null || canc > at);
                Headers = new[] { "Month", "New companies", "New subscriptions", "Cancelled", "Renting at month end", "Churn %" };
                Rows = new();
                for (var m = new DateTime(RangeFrom.Year, RangeFrom.Month, 1); m <= RangeTo; m = m.AddMonths(1))
                {
                    var end = m.AddMonths(1);
                    var startLive = subs.Where(s => Live(s.Status, s.StartDate, s.CancelledAt, m)).Select(s => s.CompanyId).Distinct().Count();
                    var endLive = subs.Where(s => Live(s.Status, s.StartDate, s.CancelledAt, end.AddTicks(-1))).Select(s => s.CompanyId).Distinct().Count();
                    var cancelled = subs.Count(s => s.CancelledAt >= m && s.CancelledAt < end);
                    Rows.Add(new object?[] { m.ToString("MMM yyyy", CultureInfo.InvariantCulture), comps.Count(c => c.RegisteredAt >= m && c.RegisteredAt < end),
                        subs.Count(s => s.StartDate >= m && s.StartDate < end), cancelled, endLive, startLive == 0 ? 0d : Math.Round(100.0 * cancelled / startLive, 1) });
                }
                var now = DateTime.UtcNow;
                Summary = $"{subs.Where(s => Live(s.Status, s.StartDate, s.CancelledAt, now)).Select(s => s.CompanyId).Distinct().Count()} companies renting now · {comps.Count} registered in total";
                break;
            }
            case "limits":
            {
                var list = await UsageRowsAsync();
                var plans = await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.MonthlyPrice).ToListAsync();
                var hit = list.Where(r => !r.IsLegacy && (_usage.IsOverStorage(r) || _usage.IsOverUsers(r) || _usage.IsNear(r))).ToList();
                Headers = new[] { "Company", "Plan", "Storage used (GB)", "Storage quota (GB)", "Storage %", "Active users", "User limit", "Users %", "State", "Limits paused until", "Suggested plan" };
                Rows = hit.OrderByDescending(r => Math.Max(r.StoragePct, r.UserPct)).Select(r =>
                {
                    var state = _usage.IsOverStorage(r) && _usage.IsOverUsers(r) ? "Over storage + users" : _usage.IsOverStorage(r) ? "Over storage" : _usage.IsOverUsers(r) ? "Over users" : $"Above {_usage.WarnPercent}%";
                    var fit = plans.FirstOrDefault(p => p.StorageGb > r.UsedGb && p.MaxUsers > r.ActiveUsers && p.Name != r.Plan);
                    return new object?[] { r.Name, r.Plan, Math.Round(r.UsedGb, 3), r.QuotaGb, Math.Round(r.StoragePct, 1), r.ActiveUsers, r.MaxUsers, Math.Round(r.UserPct, 1), state,
                        r.Overridden ? r.LimitOverrideUntil : null, fit?.Name ?? "Custom plan" };
                }).ToList();
                Summary = $"{hit.Count(r => _usage.IsOverStorage(r) || _usage.IsOverUsers(r))} over limit · {hit.Count(_usage.IsNear)} above {_usage.WarnPercent}% (warning threshold in Settings)";
                break;
            }
            case "usage":
            {
                var list = await UsageRowsAsync();
                Headers = new[] { "Company", "Plan", "Inspections", "Inspections this month", "Open CAPA", "Active users", "Signed in 30 days", "Last activity" };
                Rows = list.OrderByDescending(r => r.InspectionsThisMonth).ThenByDescending(r => r.Inspections)
                    .Select(r => new object?[] { r.Name, r.Plan, r.Inspections, r.InspectionsThisMonth, r.OpenCapa, r.ActiveUsers, r.ActiveUsers30d, r.LastActivityAt }).ToList();
                Summary = $"{list.Sum(r => r.Inspections)} inspections in total · {list.Sum(r => r.InspectionsThisMonth)} this month";
                break;
            }            case "modules":
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
                var list = await UsageRowsAsync();
                Headers = new[] { "Company", "Country", "Plan", "Database (GB)", "Files (GB)", "Files", "Total (GB)", "Quota (GB)", "Usage %", "Growth 30 days (GB)", "Days to full", "Measured" };
                Rows = list.OrderByDescending(r => r.UsedGb).Select(r => new object?[] { r.Name, r.Country, r.Plan, Math.Round(r.DbGb, 3), Math.Round(r.FilesGb, 3), r.FileCount, Math.Round(r.UsedGb, 3),
                    r.QuotaGb == 0 ? null : r.QuotaGb, r.QuotaGb == 0 ? null : Math.Round(r.StoragePct, 1), Math.Round(r.Growth30Gb, 3), r.DaysToFull, r.MeasuredAt }).ToList();
                var p = await _usage.PlatformAsync();
                Summary = $"{UsageReportService.Size(list.Sum(x => x.UsedGb))} used · {UsageReportService.Size(p.AllocatedGb)} allocated in active plans" + (p.CapacityGb > 0 ? $" · capacity {UsageReportService.Size(p.CapacityGb)}" : "");
                break;
            }            case "invoices":
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
