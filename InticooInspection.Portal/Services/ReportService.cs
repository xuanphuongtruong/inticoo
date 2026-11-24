using InticooInspection.Portal.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Services;

public record MonthPoint(string Label, decimal Value);
public record NameCount(string Name, int Count, string Color);

public class DashboardData
{
    public int TotalCompanies, NewCompaniesThisMonth, ActiveSubscriptions, PendingRegistrations;
    public decimal Mrr, MrrLastMonth;
    public double ActivePercent;
    public List<MonthPoint> MonthlyRevenue = new();
    public List<NameCount> CompaniesByPlan = new();
    public List<MonthPoint> NewCompaniesByMonth = new();
    public List<NameCount> PaymentStatus = new();
    public int InvoicesThisMonth;
    public List<(string Name, decimal Amount, string Color)> InvoiceAmounts = new();
    public double StorageUsedGb, StorageCapacityGb;
    public List<Company> RecentCompanies = new();
    public List<Payment> RecentPayments = new();
    public List<(string Name, decimal Mrr)> TopCompanies = new();
    public List<(string Name, int Count, double Pct, string Color)> ModuleUsage = new();
    public List<(string Name, int Count, double Pct, string Color)> PlanDistribution = new();
    public Dictionary<string, int> CompaniesByCountry = new();
}

/// <summary>Số liệu tổng hợp cho Dashboard và Reports. Doanh thu = thanh toán Paid (không tính Refunded).</summary>
public class ReportService
{
    public const string BlueC = "#2563eb", GreenC = "#10b981", PurpleC = "#8b5cf6", AmberC = "#f59e0b", RedC = "#ef4444", GreyC = "#94a3b8";
    public static string PlanColor(string code) => code switch { "basic" => BlueC, "business" => GreenC, "professional" => PurpleC, _ => AmberC };

    private readonly PortalDbContext _db;
    public ReportService(PortalDbContext db) => _db = db;

    public static decimal Monthly(Subscription s) => s.BillingCycle == "Annual" ? s.Price / 12m : s.Price;

    public async Task<DashboardData> DashboardAsync(int storageCapacityTb = 10)
    {
        var d = new DashboardData();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var companies = await _db.Companies.AsNoTracking().ToListAsync();
        d.TotalCompanies = companies.Count;
        d.NewCompaniesThisMonth = companies.Count(c => c.RegisteredAt >= monthStart);
        d.PendingRegistrations = companies.Count(c => c.Status is CompanyStatus.PendingPayment or CompanyStatus.PendingReview);

        var subs = await _db.Subscriptions.AsNoTracking().Include(s => s.Plan).Include(s => s.Company).ToListAsync();
        var active = subs.Where(s => s.Status == SubscriptionStatus.Active).ToList();
        d.ActiveSubscriptions = active.Count;
        d.ActivePercent = d.TotalCompanies == 0 ? 0 : Math.Round(100.0 * active.Count / d.TotalCompanies, 1);
        d.Mrr = active.Sum(Monthly);
        d.MrrLastMonth = active.Where(s => s.StartDate < monthStart).Sum(Monthly);

        // Doanh thu 6 tháng gần nhất
        var from6 = monthStart.AddMonths(-5);
        var paid = await _db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Paid && p.CreatedAt >= from6)
            .Select(p => new { p.CreatedAt, p.Amount }).ToListAsync();
        for (int i = 5; i >= 0; i--)
        {
            var m = monthStart.AddMonths(-i);
            d.MonthlyRevenue.Add(new MonthPoint(m.ToString("MMM"), paid.Where(p => p.CreatedAt.Year == m.Year && p.CreatedAt.Month == m.Month).Sum(p => p.Amount)));
            d.NewCompaniesByMonth.Add(new MonthPoint(m.ToString("MMM"), companies.Count(c => c.RegisteredAt.Year == m.Year && c.RegisteredAt.Month == m.Month)));
        }

        var plans = await _db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ToListAsync();
        var live = subs.Where(s => s.Status is not (SubscriptionStatus.Cancelled)).ToList();
        foreach (var p in plans)
        {
            var n = live.Count(s => s.PlanId == p.Id);
            d.CompaniesByPlan.Add(new NameCount(p.Name, n, PlanColor(p.Code)));
            d.PlanDistribution.Add((p.Name, n, live.Count == 0 ? 0 : Math.Round(100.0 * n / live.Count, 0), PlanColor(p.Code)));
        }

        // Thanh toán / hóa đơn trong tháng hiện tại (theo ngày hóa đơn)
        var inv = await _db.Invoices.AsNoTracking().Where(i => i.InvoiceDate >= monthStart).ToListAsync();
        d.InvoicesThisMonth = inv.Count;
        (string, string)[] sts = { (InvoiceStatus.Paid, GreenC), (InvoiceStatus.Pending, AmberC), (InvoiceStatus.Overdue, RedC), (InvoiceStatus.Cancelled, GreyC) };
        foreach (var (s, col) in sts)
        {
            d.PaymentStatus.Add(new NameCount(s, inv.Count(i => i.Status == s), col));
            d.InvoiceAmounts.Add((s, inv.Where(i => i.Status == s).Sum(i => i.Amount), col));
        }

        d.StorageUsedGb = companies.Sum(c => c.StorageUsedGb);
        d.StorageCapacityGb = storageCapacityTb * 1024.0;

        d.RecentCompanies = await _db.Companies.AsNoTracking().Include(c => c.Subscriptions).ThenInclude(s => s.Plan)
            .OrderByDescending(c => c.RegisteredAt).Take(5).ToListAsync();
        d.RecentPayments = await _db.Payments.AsNoTracking().Include(p => p.Invoice).Include(p => p.Company)
            .OrderByDescending(p => p.CreatedAt).Take(5).ToListAsync();

        d.TopCompanies = active.GroupBy(s => s.Company!.Name).Select(g => (g.Key, g.Sum(Monthly)))
            .OrderByDescending(x => x.Item2).Take(5).ToList();

        var activeCompanyIds = companies.Where(c => c.Status == CompanyStatus.Active).Select(c => c.Id).ToHashSet();
        var mods = await _db.Modules.AsNoTracking().OrderBy(m => m.SortOrder).ToListAsync();
        var cm = await _db.CompanyModules.AsNoTracking().Where(x => x.Enabled).ToListAsync();
        foreach (var m in mods)
        {
            var n = cm.Count(x => x.ModuleCode == m.Code && activeCompanyIds.Contains(x.CompanyId));
            d.ModuleUsage.Add((m.Name, n, activeCompanyIds.Count == 0 ? 0 : Math.Round(100.0 * n / activeCompanyIds.Count, 0), m.Color));
        }

        d.CompaniesByCountry = companies.GroupBy(c => c.Country).OrderByDescending(g => g.Count()).Take(5).ToDictionary(g => g.Key, g => g.Count());
        return d;
    }
}
