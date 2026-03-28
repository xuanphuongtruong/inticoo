using InticooInspection.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Vendor>         Vendors         { get; set; }
        public DbSet<Customer>       Customers       { get; set; }
        public DbSet<Product>        Products        { get; set; }
        public DbSet<CustomerFile>   CustomerFiles   => Set<CustomerFile>();
        public DbSet<Inspection>                    Inspections                    => Set<Inspection>();
        public DbSet<InspectionStep>                InspectionSteps                => Set<InspectionStep>();
        public DbSet<InspectionPackaging>           InspectionPackagings           => Set<InspectionPackaging>();
        public DbSet<InspectionProductSpec>         InspectionProductSpecs         => Set<InspectionProductSpec>();
        public DbSet<InspectionColourSwatch>        InspectionColourSwatches       => Set<InspectionColourSwatch>();
        public DbSet<InspectionPerformanceTest>     InspectionPerformanceTests     => Set<InspectionPerformanceTest>();
        public DbSet<InspectionReference>           InspectionReferences           => Set<InspectionReference>();
        public DbSet<InspectionOverallConclusion>   InspectionOverallConclusions   => Set<InspectionOverallConclusion>();
        public DbSet<InspectionQcQuantityConformity> InspectionQcQuantityConformities => Set<InspectionQcQuantityConformity>();
        public DbSet<InspectionQcAqlResult>          InspectionQcAqlResults           => Set<InspectionQcAqlResult>();
        public DbSet<InspectionQcDefect>             InspectionQcDefects              => Set<InspectionQcDefect>();
        public DbSet<ProductCategory>               ProductCategories               => Set<ProductCategory>();
        public DbSet<Country>                       Countries                       => Set<Country>();
        public DbSet<City>                          Cities                          => Set<City>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Inspection>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).IsRequired().HasMaxLength(200);
                e.HasOne(x => x.CreatedBy)
                 .WithMany(u => u.Inspections)
                 .HasForeignKey(x => x.CreatedById)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<InspectionStep>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).IsRequired().HasMaxLength(200);
                e.HasOne(x => x.Inspection)
                 .WithMany(i => i.Steps)
                 .HasForeignKey(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CustomerFile>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Customer)
                 .WithMany()
                 .HasForeignKey(x => x.CustomerId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Product>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.ProductName).IsRequired().HasMaxLength(200);
                e.Property(x => x.Category).HasMaxLength(100);
                e.Property(x => x.ProductType).HasMaxLength(100);
                e.Property(x => x.ProductCode).HasMaxLength(100);
                e.Property(x => x.ProductColor).HasMaxLength(100);
                e.Property(x => x.ProductSize).HasMaxLength(100);
                e.Property(x => x.PhotoUrl).HasMaxLength(500);
                e.Property(x => x.Remark).HasMaxLength(500);

                e.HasOne(x => x.Customer)
                 .WithMany()
                 .HasForeignKey(x => x.CustomerId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Vendor)
                 .WithMany()
                 .HasForeignKey(x => x.VendorId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // ── Inspection child entities ─────────────────────────
            builder.Entity<InspectionPackaging>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Inspection)
                 .WithOne(i => i.Packaging)
                 .HasForeignKey<InspectionPackaging>(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<InspectionProductSpec>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Inspection)
                 .WithOne(i => i.ProductSpec)
                 .HasForeignKey<InspectionProductSpec>(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<InspectionColourSwatch>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Material).HasMaxLength(200);
                e.HasOne(x => x.Inspection)
                 .WithMany(i => i.ColourSwatches)
                 .HasForeignKey(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<InspectionPerformanceTest>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Category).HasMaxLength(200);
                e.Property(x => x.TestItem).HasMaxLength(200);
                e.Property(x => x.TestRequirement).HasMaxLength(500);
                e.Property(x => x.Remark).HasMaxLength(500);
                e.HasOne(x => x.Inspection)
                 .WithMany(i => i.PerformanceTests)
                 .HasForeignKey(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<InspectionReference>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.ReferenceName).HasMaxLength(300);
                e.Property(x => x.FileName).HasMaxLength(300);
                e.Property(x => x.FileUrl).HasMaxLength(500);
                e.Property(x => x.Remark).HasMaxLength(500);
                e.HasOne(x => x.Inspection)
                 .WithMany(i => i.References)
                 .HasForeignKey(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<InspectionOverallConclusion>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Letter).IsRequired().HasMaxLength(5);
                e.Property(x => x.Label).IsRequired().HasMaxLength(200);
                e.Property(x => x.Remark).HasMaxLength(500);
                e.HasOne(x => x.Inspection)
                 .WithMany(i => i.OverallConclusions)
                 .HasForeignKey(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ── QC Result entities ───────────────────────────────
            builder.Entity<InspectionQcQuantityConformity>(e =>
            {
                e.HasKey(x => x.Id);
                e.HasOne(x => x.Inspection)
                 .WithOne(i => i.QcQuantityConformity)
                 .HasForeignKey<InspectionQcQuantityConformity>(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<InspectionQcAqlResult>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.InspectionLevel).HasMaxLength(5);
                e.Property(x => x.CriticalAql).HasMaxLength(20);
                e.Property(x => x.MajorAql).HasMaxLength(20);
                e.Property(x => x.MinorAql).HasMaxLength(20);
                e.HasOne(x => x.Inspection)
                 .WithOne(i => i.QcAqlResult)
                 .HasForeignKey<InspectionQcAqlResult>(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<InspectionQcDefect>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.DefectType).HasMaxLength(20);
                e.Property(x => x.PhotoUrl).HasMaxLength(500);
                e.Property(x => x.Remark).HasMaxLength(1000);
                e.HasOne(x => x.Inspection)
                 .WithMany(i => i.QcDefects)
                 .HasForeignKey(x => x.InspectionId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // Title nullable (vì field cũ IsRequired nhưng entity mới dùng ProductName)
            builder.Entity<Inspection>()
                   .Property(x => x.Title).IsRequired(false).HasMaxLength(200);

            builder.Entity<ProductCategory>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).IsRequired().HasMaxLength(100);
                e.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<Country>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).IsRequired().HasMaxLength(2);
                e.Property(x => x.Name).IsRequired().HasMaxLength(100);
                e.Property(x => x.Region).HasMaxLength(50);
                e.HasIndex(x => x.Code).IsUnique();
            });

            builder.Entity<City>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).IsRequired().HasMaxLength(100);
                e.HasOne(x => x.Country)
                 .WithMany(c => c.Cities)
                 .HasForeignKey(x => x.CountryId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(x => new { x.CountryId, x.Name }).IsUnique();
            });
        }
    }
}
