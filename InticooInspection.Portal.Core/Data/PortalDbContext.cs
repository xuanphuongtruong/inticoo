using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Portal.Data;

/// <summary>
/// DB quản trị trung tâm (InticooInspectionSupper). Toàn bộ bảng nằm trong schema "sup" để không đụng
/// các bảng cũ có sẵn trong DB này; dữ liệu nghiệp vụ của từng khách nằm ở DB riêng của khách đó.
/// </summary>
public class PortalDbContext : DbContext
{
    public const string Schema = "sup";

    public PortalDbContext(DbContextOptions<PortalDbContext> options) : base(options) { }

    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlatformModule> Modules => Set<PlatformModule>();
    public DbSet<PlanModule> PlanModules => Set<PlanModule>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<CompanyModule> CompanyModules => Set<CompanyModule>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentEvent> PaymentEvents => Set<PaymentEvent>();
    public DbSet<CompanyUser> CompanyUsers => Set<CompanyUser>();
    public DbSet<SuperAdmin> SuperAdmins => Set<SuperAdmin>();
    public DbSet<Registration> Registrations => Set<Registration>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ResourceItem> ResourceItems => Set<ResourceItem>();
    public DbSet<ResourceTranslation> ResourceTranslations => Set<ResourceTranslation>();
    public DbSet<CompanyUsageSnapshot> UsageSnapshots => Set<CompanyUsageSnapshot>();
    public DbSet<PlatformDiskSnapshot> DiskSnapshots => Set<PlatformDiskSnapshot>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<Plan>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.MonthlyPrice).HasPrecision(12, 2);
            e.Property(x => x.AnnualPrice).HasPrecision(12, 2);
        });
        b.Entity<PlatformModule>(e => { e.ToTable("Modules"); e.HasIndex(x => x.Code).IsUnique(); });
        b.Entity<PlanModule>(e =>
        {
            e.HasKey(x => new { x.PlanId, x.ModuleId });
            e.HasOne(x => x.Plan).WithMany(p => p.Modules).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Module).WithMany().HasForeignKey(x => x.ModuleId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Company>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Status);
        });
        b.Entity<CompanyModule>(e =>
        {
            e.HasKey(x => new { x.CompanyId, x.ModuleCode });
            e.HasOne(x => x.Company).WithMany(c => c.Modules).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Subscription>(e =>
        {
            e.Property(x => x.Price).HasPrecision(12, 2);
            e.HasOne(x => x.Company).WithMany(c => c.Subscriptions).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.Status);
        });
        b.Entity<Invoice>(e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.Tax).HasPrecision(12, 2);
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.InvoiceDate);
        });
        b.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.TransactionId).IsUnique();
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.HasOne(x => x.Invoice).WithMany(i => i.Payments).HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            // NoAction để tránh "multiple cascade paths" (Company → Invoice → Payment đã cascade)
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x => x.CreatedAt);
        });
        b.Entity<PaymentEvent>(e =>
            e.HasOne(x => x.Payment).WithMany(p => p.Events).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Cascade));
        b.Entity<CompanyUser>(e =>
        {
            e.HasOne(x => x.Company).WithMany(c => c.Users).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.UserName);
        });
        b.Entity<SuperAdmin>(e => e.HasIndex(x => x.Email).IsUnique());
        b.Entity<Registration>(e =>
        {
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.VerifyToken);
        });
        b.Entity<AuditLog>(e => e.HasIndex(x => x.At));
        b.Entity<Setting>(e => e.HasKey(x => x.Key));
        b.Entity<ResourceItem>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.Section, x.IsPublished, x.SortOrder });
            e.HasMany(x => x.Translations).WithOne(x => x.ResourceItem).HasForeignKey(x => x.ResourceItemId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<ResourceTranslation>(e => e.HasIndex(x => new { x.ResourceItemId, x.Lang }).IsUnique());
        b.Entity<CompanyUsageSnapshot>(e =>
        {
            e.ToTable("CompanyUsageSnapshots");
            e.HasIndex(x => new { x.CompanyId, x.At });
            e.HasIndex(x => x.At);
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PlatformDiskSnapshot>(e => { e.ToTable("PlatformDiskSnapshots"); e.HasIndex(x => x.At); });
    }
}

public class PortalDesignTimeFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<PortalDbContext>
{
    public PortalDbContext CreateDbContext(string[] args)
    {
        // Chỉ dùng cho `dotnet ef`: lấy chuỗi kết nối từ biến môi trường, KHÔNG hard-code secret vào repo
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Super")
                 ?? throw new InvalidOperationException("Đặt biến môi trường ConnectionStrings__Super trước khi chạy dotnet ef.");
        var o = new DbContextOptionsBuilder<PortalDbContext>()
            .UseSqlServer(cs, s => s.MigrationsHistoryTable("__EFMigrationsHistory", PortalDbContext.Schema))
            .Options;
        return new PortalDbContext(o);
    }
}
