using InticooInspection.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Vendor>              Vendors              { get; set; }
        public DbSet<VendorFactoryEvalFile> VendorFactoryEvalFiles { get; set; }
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
        public DbSet<ProductReference>              ProductReferences               => Set<ProductReference>();
        public DbSet<Country>                       Countries                       => Set<Country>();
        public DbSet<City>                          Cities                          => Set<City>();
        public DbSet<PerformanceTestMaster>         PerformanceTestMasters          => Set<PerformanceTestMaster>();

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
                e.Property(x => x.SizeL).HasColumnType("decimal(10,2)");
                e.Property(x => x.SizeW).HasColumnType("decimal(10,2)");
                e.Property(x => x.SizeH).HasColumnType("decimal(10,2)");
                e.Property(x => x.Weight).HasColumnType("decimal(10,3)");
                e.Property(x => x.PhotoUrl).HasMaxLength(500);
                e.Property(x => x.Remark).HasMaxLength(500);
                e.Property(x => x.IsActive).HasDefaultValue(true);

                e.HasOne(x => x.Customer)
                 .WithMany()
                 .HasForeignKey(x => x.CustomerId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Vendor)
                 .WithMany()
                 .HasForeignKey(x => x.VendorId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasMany(x => x.References)
                 .WithOne(r => r.Product)
                 .HasForeignKey(r => r.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ProductReference>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(300);
                e.Property(x => x.FileUrl).HasMaxLength(500);
                e.Property(x => x.FileName).HasMaxLength(300);
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

            builder.Entity<PerformanceTestMaster>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Category).IsRequired().HasMaxLength(200);
                e.Property(x => x.StandardCode).IsRequired().HasMaxLength(300);
                e.Property(x => x.StandardName).HasMaxLength(500);
                e.Property(x => x.ProductType).IsRequired().HasMaxLength(200);
                e.Property(x => x.Market).HasMaxLength(100);
                e.Property(x => x.ProtocolName).IsRequired().HasMaxLength(500);
                e.Property(x => x.Requirements).HasColumnType("nvarchar(max)");
                e.HasIndex(x => new { x.Category, x.IsActive });
                e.HasIndex(x => new { x.ProductType, x.IsActive });
            });

            // ── AppUser extra fields ──────────────────────────────
            builder.Entity<AppUser>(e =>
            {
                e.Property(x => x.FullName).HasMaxLength(200);
                e.Property(x => x.ShortName).HasMaxLength(100);
                e.Property(x => x.Gender).HasMaxLength(20);
                e.Property(x => x.Nationality).HasMaxLength(100);
                e.Property(x => x.IdType).HasMaxLength(20);
                e.Property(x => x.IdNumber).HasMaxLength(100);
                e.Property(x => x.Category).HasMaxLength(100);
                e.Property(x => x.Language).HasMaxLength(500);
                e.Property(x => x.InspectorId).HasMaxLength(50);
                e.Property(x => x.Address).HasMaxLength(300);
                e.Property(x => x.Address1).HasMaxLength(300);
                e.Property(x => x.Address2).HasMaxLength(300);
                e.Property(x => x.City).HasMaxLength(100);
                e.Property(x => x.State).HasMaxLength(100);
                e.Property(x => x.Country).HasMaxLength(100);
                e.Property(x => x.PostalCode).HasMaxLength(20);
                e.Property(x => x.Mobile).HasMaxLength(50);
                e.Property(x => x.CvUrl).HasMaxLength(500);
                e.Property(x => x.AvatarUrl).HasMaxLength(500);
            });

            builder.Entity<Vendor>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Code).IsRequired().HasMaxLength(50);
                e.Property(x => x.Name).IsRequired().HasMaxLength(200);
                e.Property(x => x.ShortName).HasMaxLength(100);
                e.Property(x => x.Category).HasMaxLength(500);  // multi-category comma-separated
                e.Property(x => x.FactoryEvaluationNotes).HasColumnType("nvarchar(max)");
                e.Property(x => x.TaxCode).HasMaxLength(50);
                e.Property(x => x.BusinessRegNo).HasMaxLength(100);
                e.Property(x => x.Phone).HasMaxLength(50);
                e.Property(x => x.Website).HasMaxLength(300);
                e.Property(x => x.Address1).HasMaxLength(300);
                e.Property(x => x.Address2).HasMaxLength(300);
                e.Property(x => x.City).HasMaxLength(100);
                e.Property(x => x.State).HasMaxLength(100);
                e.Property(x => x.Country).HasMaxLength(100);
                e.Property(x => x.PostalCode).HasMaxLength(20);
                e.Property(x => x.ContactName).HasMaxLength(150);
                e.Property(x => x.ContactTitle).HasMaxLength(100);
                e.Property(x => x.ContactPhone).HasMaxLength(50);
                e.Property(x => x.ContactEmail).HasMaxLength(200);
                e.Property(x => x.Notes).HasColumnType("nvarchar(max)");
                e.HasMany(x => x.Attachments)
                 .WithOne(a => a.Vendor)
                 .HasForeignKey(a => a.VendorId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.FactoryEvalFiles)
                 .WithOne(f => f.Vendor)
                 .HasForeignKey(f => f.VendorId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<VendorFactoryEvalFile>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.StoredName).IsRequired().HasMaxLength(200);
                e.Property(x => x.OriginalName).IsRequired().HasMaxLength(300);
                e.Property(x => x.ContentType).HasMaxLength(100);
            });
        }
    }
}
