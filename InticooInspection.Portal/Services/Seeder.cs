using InticooInspection.Portal.Data;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Services;

public static class Seeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<PortalDbContext>();
        var cfg = sp.GetRequiredService<IConfiguration>();
        var pw = sp.GetRequiredService<PasswordService>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Seeder");

        // ── Module ───────────────────────────────────────────────
        if (!await db.Modules.AnyAsync())
        {
            db.Modules.AddRange(
                new PlatformModule { Code = "inspection", Name = "Product Inspection", Description = "Create and manage inspection jobs, checklists, reports and defects.", Color = "#2563eb", SortOrder = 1 },
                new PlatformModule { Code = "capa", Name = "CAPA Management", Description = "Manage corrective and preventive actions for quality issues.", Color = "#10b981", SortOrder = 2 },
                new PlatformModule { Code = "analytics", Name = "Dashboard Analytics", Description = "Quality analytics, KPIs and performance reports.", Color = "#8b5cf6", SortOrder = 3 },
                new PlatformModule { Code = "clc", Name = "Loading Container Check (CLC)", Description = "Manage container loading inspection, photos, reports and compliance.", Color = "#f59e0b", SortOrder = 4 },
                new PlatformModule { Code = "api", Name = "API Integration", Description = "External system integration and data exchange.", Color = "#ef4444", SortOrder = 5 });
            await db.SaveChangesAsync();
        }

        // ── Gói ─────────────────────────────────────────────────
        if (!await db.Plans.AnyAsync())
        {
            // Số liệu lấy từ bản thiết kế (Super Admin › Settings): Basic 5 user/100GB, Business 15/200GB, Professional 50/500GB
            var basic = new Plan { Code = "basic", Name = "Basic", Tagline = "For small teams", MonthlyPrice = 149, AnnualPrice = 1490, MaxUsers = 5, StorageGb = 100, SortOrder = 1 };
            var biz   = new Plan { Code = "business", Name = "Business", Tagline = "For growing teams", MonthlyPrice = 349, AnnualPrice = 3490, MaxUsers = 15, StorageGb = 200, SortOrder = 2, IsPopular = true };
            var pro   = new Plan { Code = "professional", Name = "Professional", Tagline = "For larger teams", MonthlyPrice = 699, AnnualPrice = 6990, MaxUsers = 50, StorageGb = 500, SortOrder = 3 };
            db.Plans.AddRange(basic, biz, pro);
            await db.SaveChangesAsync();
            var mods = await db.Modules.ToDictionaryAsync(m => m.Code, m => m.Id);
            void Link(Plan p, params string[] codes) { foreach (var c in codes) db.PlanModules.Add(new PlanModule { PlanId = p.Id, ModuleId = mods[c] }); }
            Link(basic, "inspection");
            Link(biz, "inspection", "capa", "analytics");
            Link(pro, "inspection", "capa", "analytics", "clc", "api");
            await db.SaveChangesAsync();
        }

        // ── Super admin đầu tiên ─────────────────────────────────
        var email = cfg["SuperAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = cfg["SuperAdmin:Password"];
        if (!await db.SuperAdmins.AnyAsync())
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                log.LogWarning("Chưa có Super Admin và thiếu SuperAdmin:Email/Password trong cấu hình — không thể đăng nhập khu quản trị.");
            else
            {
                var err = PasswordService.Validate(password);
                if (err != null) throw new InvalidOperationException("SuperAdmin:Password không đạt chính sách: " + err);
                db.SuperAdmins.Add(new SuperAdmin
                {
                    Email = email, FullName = cfg["SuperAdmin:FullName"] ?? "Super Admin",
                    PasswordHash = pw.Hash(password), MustChangePassword = false
                });
                await db.SaveChangesAsync();
                log.LogInformation("Đã tạo Super Admin {Email}", email);
            }
        }

        if (cfg.GetValue<bool>("Seed:Demo")) await DemoSeeder.SeedAsync(db);
    }
}

/// <summary>Dữ liệu mẫu để xem giao diện quản trị khi chưa có khách thật. Mọi dòng gắn IsDemo=true và xóa được bằng 1 nút.</summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(PortalDbContext db)
    {
        if (await db.Companies.AnyAsync(c => c.IsDemo)) return;
        var rnd = new Random(42);
        var plans = await db.Plans.Include(p => p.Modules).ThenInclude(m => m.Module).OrderBy(p => p.SortOrder).ToListAsync();
        string[] names = { "Alpha Furniture","Global Sourcing","Coastal Living","Verde Home","Nordic Outdoor","Pacific Trading","Everest Furniture",
            "Home & Garden Co.","Urban Living","Sunrise Outdoor Ltd","Alpine Furniture","European Imports","Green Home Living","Summit Trading","Lakeside Decor",
            "Orchid Homeware","Redwood Supply","Blue Harbor Goods","Maple Leaf Imports","Kangaroo Sourcing","Tiger Craft","Delta Hardline","Aurora Interiors",
            "Cedar & Pine","Silverline Trading","Harbor Point Co.","Oakridge Living","Zenith Home" };
        string[] cc = { "DE","GB","FR","SE","CA","AU","SG","IT","ES","US","DE","DE","CA","MY","NL","VN","US","DK","CA","AU","TH","PL","US","SE","HK","GB","US","JP" };
        string[] first = { "John","Sarah","David","Lisa","Michael","Emma","Thomas","Sophie","Andrew","Natalia" };
        string[] last = { "Miller","Johnson","Chen","Wang","Kim","Roberts","Klein","Laurent","Green","Petrova" };
        string[] roles = { "Administrator","Inspector","Quality Manager","Viewer","Purchaser" };
        var today = DateTime.UtcNow.Date;
        int invSeq = 0;

        for (int n = 0; n < names.Length; n++)
        {
            var reg = today.AddDays(-rnd.Next(1, 190)).AddHours(rnd.Next(8, 18));
            var plan = plans[n % 10 < 3 ? 0 : n % 10 < 8 ? 1 : 2];
            var annual = n % 4 == 0;
            var code = "demo_" + Slug.From(names[n]);
            var st = n == 9 ? CompanyStatus.PendingPayment : n == 4 ? CompanyStatus.PendingReview : n == 6 ? CompanyStatus.Overdue
                   : n == 8 ? CompanyStatus.Paused : n == 7 ? CompanyStatus.Cancelled : CompanyStatus.Active;
            var cname = CountryList.ByCode.GetValueOrDefault(cc[n], cc[n]);
            var contact = $"{first[n % 10]} {last[(n * 3) % 10]}";
            var dom = Slug.From(names[n]).Replace("_", "") + ".com";
            var c = new Company
            {
                Code = code, Name = names[n], CountryCode = cc[n], Country = cname, Address = $"{100 + n * 7} Main Street, {cname}",
                ContactPerson = contact, Email = $"contact@{dom}", Phone = $"+1 312 555 {1000 + n:0000}", Website = $"https://www.{dom}",
                Status = st, RegisteredAt = reg, IsDemo = true,
                StorageUsedGb = st is CompanyStatus.PendingPayment or CompanyStatus.PendingReview ? 0 : Math.Round(rnd.NextDouble() * plan.StorageGb * 0.8, 1)
            };
            db.Companies.Add(c);
            var priceBase = annual ? plan.AnnualPrice : plan.MonthlyPrice;
            var sub = new Subscription
            {
                Company = c, PlanId = plan.Id, BillingCycle = annual ? "Annual" : "Monthly", Price = priceBase, StartDate = reg,
                AutoRenew = st == CompanyStatus.Active,
                Status = st switch { CompanyStatus.Active => SubscriptionStatus.Active, CompanyStatus.PendingPayment => SubscriptionStatus.PendingPayment,
                    CompanyStatus.PendingReview => SubscriptionStatus.PendingReview, CompanyStatus.Overdue => SubscriptionStatus.Overdue,
                    CompanyStatus.Paused => SubscriptionStatus.Paused, _ => SubscriptionStatus.Cancelled },
                NextBillingDate = st is CompanyStatus.PendingPayment or CompanyStatus.PendingReview ? null : BillingService.AddCycle(reg, annual ? "Annual" : "Monthly")
            };
            db.Subscriptions.Add(sub);
            foreach (var m in plan.Modules) db.CompanyModules.Add(new CompanyModule { Company = c, ModuleCode = m.Module!.Code, Enabled = true });

            // người dùng
            var userCount = Math.Min(plan.MaxUsers, 2 + rnd.Next(0, 6));
            for (int u = 0; u < userCount; u++)
            {
                var fn = $"{first[(n + u) % 10]} {last[(n * 2 + u) % 10]}";
                db.CompanyUsers.Add(new CompanyUser
                {
                    Company = c, FullName = fn, Email = $"{fn.ToLower().Replace(' ', '.')}@{dom}", Role = u == 0 ? "Administrator" : roles[rnd.Next(roles.Length)],
                    Status = st is CompanyStatus.PendingPayment ? "Pending" : (rnd.Next(12) == 0 ? "Inactive" : rnd.Next(10) == 0 ? "Pending" : "Active"),
                    CreatedAt = reg, LastLoginAt = today.AddHours(-rnd.Next(1, 400)), JobTitle = u == 0 ? "Factory Manager" : "QA", Department = "Quality Assurance",
                    Phone = c.Phone, Language = "en"
                });
            }

            // hóa đơn theo từng kỳ đã qua
            var periods = new List<DateTime>();
            for (var d = reg; d <= today && periods.Count < 8; d = BillingService.AddCycle(d, sub.BillingCycle)) periods.Add(d);
            if (periods.Count == 0) periods.Add(reg);
            foreach (var d in periods)
            {
                var isLast = d == periods[^1];
                string istat = InvoiceStatus.Paid;
                if (isLast) istat = st switch
                {
                    CompanyStatus.PendingPayment or CompanyStatus.PendingReview => InvoiceStatus.Pending,
                    CompanyStatus.Overdue => InvoiceStatus.Overdue, CompanyStatus.Cancelled => InvoiceStatus.Cancelled, _ => InvoiceStatus.Paid
                };
                var inv = new Invoice
                {
                    Company = c, Subscription = sub, PlanName = plan.Name, BillingCycle = sub.BillingCycle, Amount = priceBase,
                    InvoiceDate = d, DueDate = d.AddDays(30), Status = istat, PaidAt = istat == InvoiceStatus.Paid ? d.AddMinutes(20) : null,
                    Number = $"INV-DEMO-{++invSeq:00000}"
                };
                db.Invoices.Add(inv);
                if (istat is InvoiceStatus.Paid or InvoiceStatus.Overdue or InvoiceStatus.Pending)
                {
                    var pst = istat == InvoiceStatus.Paid ? PaymentStatus.Paid : istat == InvoiceStatus.Overdue ? PaymentStatus.Failed : PaymentStatus.Pending;
                    var method = n % 5 == 3 ? "BankTransfer" : n % 7 == 0 ? "PayPal" : "Card";
                    var pay = new Payment
                    {
                        Invoice = inv, Company = c, Amount = priceBase, Method = method, Provider = method == "Card" ? "Stripe" : method == "PayPal" ? "PayPal" : "Manual",
                        Status = pst, CreatedAt = d.AddMinutes(10), TransactionId = "pi_demo_" + Guid.NewGuid().ToString("N")[..14],
                        CardBrand = method == "Card" ? "Visa" : null, CardLast4 = method == "Card" ? "4242" : null, CardHolder = contact
                    };
                    pay.Events.Add(new PaymentEvent { Name = "Payment Initiated", At = pay.CreatedAt });
                    if (pst == PaymentStatus.Paid) { pay.Events.Add(new PaymentEvent { Name = "Payment Successful", At = pay.CreatedAt.AddMinutes(1) }); pay.Events.Add(new PaymentEvent { Name = "Invoice Generated", At = pay.CreatedAt.AddMinutes(2) }); }
                    db.Payments.Add(pay);
                }
            }
        }
        // một ít refund + cancelled cho bộ lọc
        await db.SaveChangesAsync();
        var refundable = await db.Payments.Where(p => p.Company!.IsDemo && p.Status == PaymentStatus.Paid).OrderBy(p => p.Id).Take(3).ToListAsync();
        foreach (var p in refundable) { p.Status = PaymentStatus.Refunded; var i = await db.Invoices.FindAsync(p.InvoiceId); if (i != null) i.Status = InvoiceStatus.Refunded; }

        db.ContactMessages.AddRange(
            new ContactMessage { FullName = "Hannah Becker", Company = "Becker Imports", Email = "hannah@becker-imports.de", Country = "Germany", Subject = "Book a Demo", Message = "We would like a demo for our QC team." },
            new ContactMessage { FullName = "Rafael Costa", Company = "Costa Home", Email = "rafael@costahome.com", Country = "Brazil", Subject = "Request a Quote", Message = "Pricing for 30 users please." });
        await db.SaveChangesAsync();
    }

    public static async Task PurgeAsync(PortalDbContext db)
    {
        db.Companies.RemoveRange(await db.Companies.Where(c => c.IsDemo).ToListAsync());
        await db.SaveChangesAsync();
    }
}
