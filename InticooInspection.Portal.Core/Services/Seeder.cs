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
                new PlatformModule { Code = "inspection", Name = "Product Inspection", Description = "Create and manage inspection jobs, checklists, reports and defects.", Color = "#2563eb", Icon = "clipboard", SortOrder = 1 },
                new PlatformModule { Code = "capa", Name = "CAPA Management", Description = "Manage corrective and preventive actions for quality issues.", Color = "#10b981", Icon = "shield", SortOrder = 2 },
                new PlatformModule { Code = "analytics", Name = "Dashboard Analytics", Description = "Quality analytics, KPIs and performance reports.", Color = "#8b5cf6", Icon = "chart", SortOrder = 3 },
                new PlatformModule { Code = "clc", Name = "Loading Container Check (CLC)", Description = "Manage container loading inspection, photos, reports and compliance.", Color = "#f59e0b", Icon = "container", SortOrder = 4 },
                new PlatformModule { Code = "api", Name = "API Integration", Description = "External system integration and data exchange.", Color = "#ef4444", Icon = "link", SortOrder = 5 });
            await db.SaveChangesAsync();
        }

        // ── Gói ─────────────────────────────────────────────────
        if (!await db.Plans.AnyAsync())
        {
            // Số liệu lấy từ bản thiết kế (Super Admin › Settings): Basic 5 user/100GB, Business 15/200GB, Professional 50/500GB
            var basic = new Plan { Code = "basic", Name = "Basic", Tagline = "For small teams", MonthlyPrice = 149, AnnualPrice = 1490, MaxUsers = 5, StorageGb = 100, SortOrder = 1, Color = "#2563eb" };
            var biz   = new Plan { Code = "business", Name = "Business", Tagline = "For growing teams", MonthlyPrice = 349, AnnualPrice = 3490, MaxUsers = 15, StorageGb = 200, SortOrder = 2, IsPopular = true, Color = "#10b981" };
            var pro   = new Plan { Code = "professional", Name = "Professional", Tagline = "For larger teams", MonthlyPrice = 699, AnnualPrice = 6990, MaxUsers = 50, StorageGb = 500, SortOrder = 3, Color = "#8b5cf6" };
            // Nội dung thẻ gói trên website theo Website.docx ({users}/{storage} = hạn mức thật; * = nổi bật) — giống migration AddUsageTracking
            basic.Features = "Digital inspection forms (PPI, DPI, PST)\nInspection scheduling\nPhoto and PDF reports\nBasic dashboard and analytics\nUp to {users} users\n{storage} GB cloud storage\nMulti-language (EN/CN/VI/MS)\nEmail support";
            biz.Features = "Everything in Basic\nCAPA management\nSupplier management\nAdvanced analytics and reports\nUp to {users} users\n{storage} GB cloud storage\nMulti-language (EN/CN/VI/MS)\nPriority email support";
            pro.Features = "Everything in Business\n*Loading Container Check (CLC)\nAdvanced reporting\nCustom inspection templates\nAPI integration (upon request)\nUp to {users} users\n{storage} GB cloud storage\nMulti-language (EN/CN/VI/MS)\nDedicated account manager";
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

        // ── Nội dung trang Resources (chỉ lần đầu khi bảng trống) — đúng nội dung đang có trên thiết kế ──
        if (!await db.ResourceItems.AnyAsync())
        {
            var n = 0;
            ResourceItem R(string sec, string kind, string title, string summary, string? cover = null, string? icon = null, string? button = null) => new()
            {
                Section = sec, Kind = kind, Title = title, Slug = ResourceContent.Slugify(title), Summary = summary,
                CoverPath = cover, Icon = icon, ButtonText = button, SortOrder = ++n * 10
            };
            db.ResourceItems.AddRange(
                R(ResourceSection.Featured, "Guide", "AQL Standards Guide", "Understand AQL levels, sampling plans and defect classifications.", "/img/photos/res-aql.jpg", button: "View Guide"),
                R(ResourceSection.Featured, "Checklist", "Inspection Checklists", "Sample checklists for PPI, DPI, PST and Loading Container Check (CLC).", "/img/photos/res-checklists.jpg", button: "View Checklists"),
                R(ResourceSection.Featured, "Guide", "Container Loading Check (CLC) Guide", "How to conduct container loading inspections and common check points.", "/img/photos/res-clc.jpg", button: "View Guide"),
                R(ResourceSection.Featured, "Article", "Quality Management Articles", "Practical insights on quality, suppliers, CAPA and continuous improvement.", "/img/photos/res-articles.jpg", button: "Read Articles"),
                R(ResourceSection.Reference, "Pdf", "AQL Tables", "Sampling plans for normal, tightened and reduced inspection.", icon: "grid"),
                R(ResourceSection.Reference, "Pdf", "Defect Classification", "Definition and examples of critical, major and minor defects.", icon: "alert"),
                R(ResourceSection.Reference, "Pdf", "Inspection Terms", "Common quality inspection terms and abbreviations.", icon: "doc"),
                R(ResourceSection.Reference, "Pdf", "Packaging Guidelines", "Guidelines for carton marking, labelling and packaging checks.", icon: "box"),
                R(ResourceSection.Reference, "Pdf", "Measurement Guide", "How to measure product dimensions, tolerance and weight.", icon: "ruler"),
                R(ResourceSection.Learning, "Article", "How to Plan an Effective Product Inspection", "Step-by-step guide for PPI, DPI and PST.", "/img/photos/learn-1.jpg"),
                R(ResourceSection.Learning, "Video", "Conducting a Container Loading Check (CLC)", "Key check points and best practices.", "/img/photos/learn-2.jpg"),
                R(ResourceSection.Learning, "Article", "Common Defects in Furniture and Hardline Products", "Examples and how to prevent them.", "/img/photos/learn-3.jpg"),
                R(ResourceSection.Learning, "Video", "Using Quality Analytics to Improve Supplier Performance", "Turn inspection data into action.", "/img/photos/learn-4.jpg"),
                R(ResourceSection.Faq, "Faq", "Which plan should I choose?", "Basic fits small teams that run product inspections. Business adds CAPA, supplier management and analytics. Professional adds Loading Container Check and API integration."),
                R(ResourceSection.Faq, "Faq", "Is my data separated from other companies?", "Yes. Every company gets its own independent database and access credentials, so data is never mixed with other customers."),
                R(ResourceSection.Faq, "Faq", "Which languages are supported?", "English, Chinese, Vietnamese and Bahasa Melayu. Each user can pick their own language."),
                R(ResourceSection.Faq, "Faq", "How do I pay?", "By credit or debit card, or by bank transfer — an invoice with bank details is generated for you."),
                R(ResourceSection.Faq, "Faq", "Can I change or cancel my plan?", "Yes. Contact our team to upgrade, downgrade or cancel at the end of your billing period."));
            await db.SaveChangesAsync();
            log.LogInformation("Đã tạo nội dung mặc định trang Resources");
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

    /// <summary>Xóa toàn bộ dữ liệu mẫu (công ty IsDemo + thuê bao, hóa đơn, thanh toán, user, module, số đo của chúng
    /// và 2 tin nhắn liên hệ mẫu do SeedAsync tạo). KHÔNG đụng công ty thật. Chạy trong 1 giao dịch: lỗi → không xóa gì.
    /// Xóa tường minh theo thứ tự con → cha, không dựa vào xóa dây chuyền (Payment→Company, Invoice→Subscription là NoAction).</summary>
    public static async Task<DemoPurgeResult> PurgeAsync(PortalDbContext db)
    {
        var ids = await db.Companies.Where(c => c.IsDemo).Select(c => c.Id).ToListAsync();
        var codes = await db.Companies.Where(c => c.IsDemo).Select(c => c.Code).ToListAsync();
        var r = new DemoPurgeResult();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (ids.Count > 0)
        {
            r.Payments = await db.Payments.Where(x => ids.Contains(x.CompanyId)).CountAsync();
            await db.PaymentEvents.Where(x => ids.Contains(x.Payment!.CompanyId)).ExecuteDeleteAsync();
            await db.Payments.Where(x => ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            r.Invoices = await db.Invoices.Where(x => ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            r.Subscriptions = await db.Subscriptions.Where(x => ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            r.Users = await db.CompanyUsers.Where(x => ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await db.CompanyModules.Where(x => ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await db.Registrations.Where(x => ids.Contains(x.CompanyId)).ExecuteDeleteAsync();
            await db.UsageSnapshots.Where(x => (x.CompanyId != null && ids.Contains(x.CompanyId.Value)) || codes.Contains(x.TenantCode)).ExecuteDeleteAsync();
            r.Companies = await db.Companies.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
        }
        // 2 tin nhắn mẫu: khớp đúng email + chủ đề do SeedAsync tạo (tin khách thật không trùng cả hai)
        r.Messages = await db.ContactMessages.Where(m =>
            (m.Email == "hannah@becker-imports.de" && m.Subject == "Book a Demo") ||
            (m.Email == "rafael@costahome.com" && m.Subject == "Request a Quote")).ExecuteDeleteAsync();
        await tx.CommitAsync();
        return r;
    }
}

public class DemoPurgeResult
{
    public int Companies, Subscriptions, Invoices, Payments, Users, Messages;
    public override string ToString() => $"{Companies} companies, {Subscriptions} subscriptions, {Invoices} invoices, {Payments} payments, {Users} users, {Messages} messages";
}
