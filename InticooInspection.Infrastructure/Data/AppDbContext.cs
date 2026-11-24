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
        public DbSet<ProductCarton>                 ProductCartons                  => Set<ProductCarton>();
        public DbSet<InspectionCarton>              InspectionCartons               => Set<InspectionCarton>();
        public DbSet<DefectCapa>                    DefectCapas                     => Set<DefectCapa>();
        public DbSet<Country>                       Countries                       => Set<Country>();
        public DbSet<City>                          Cities                          => Set<City>();
        public DbSet<PerformanceTestMaster>         PerformanceTestMasters          => Set<PerformanceTestMaster>();
        public DbSet<PerformanceTestReference>      PerformanceTestReferences       => Set<PerformanceTestReference>();
        public DbSet<MailLog>                       MailLogs                        => Set<MailLog>();
        public DbSet<MailConfig>                    MailConfigs                     => Set<MailConfig>();
        public DbSet<AppSetting>                    AppSettings                     => Set<AppSetting>();
        public DbSet<ContainerCheck>                ContainerChecks                 => Set<ContainerCheck>();
        public DbSet<ContainerCheckPoint>           ContainerCheckPoints            => Set<ContainerCheckPoint>();
        public DbSet<ContainerCheckProduct>         ContainerCheckProducts          => Set<ContainerCheckProduct>();
        public DbSet<ContainerCheckPhoto>           ContainerCheckPhotos            => Set<ContainerCheckPhoto>();
        public DbSet<PartyContact>                  PartyContacts                   => Set<PartyContact>();
        public DbSet<PartyReportSetting>            PartyReportSettings             => Set<PartyReportSetting>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Inspection>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Title).IsRequired().HasMaxLength(200);
                e.Property(x => x.TinhTrangGuiMail).HasDefaultValue(0);
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

            builder.Entity<DefectCapa>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.DefectNo).HasMaxLength(30);
                e.Property(x => x.CarNo).HasMaxLength(30);
                e.Property(x => x.Severity).HasMaxLength(20);
                e.Property(x => x.Status).HasMaxLength(20);
                e.Property(x => x.DefectCode).HasMaxLength(10);
                e.HasIndex(x => x.InspectionId);
                e.HasIndex(x => x.DefectNo);
            });

            builder.Entity<ProductCarton>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.CartonVariant).HasMaxLength(100);
                e.Property(x => x.Description).HasMaxLength(300);
                e.Property(x => x.PackagingQuantity).HasMaxLength(100);
                e.Property(x => x.SizeL).HasColumnType("decimal(10,2)");
                e.Property(x => x.SizeW).HasColumnType("decimal(10,2)");
                e.Property(x => x.SizeH).HasColumnType("decimal(10,2)");
                e.Property(x => x.Weight).HasColumnType("decimal(10,3)");
                e.HasOne(x => x.Product)
                 .WithMany(p => p.Cartons)
                 .HasForeignKey(x => x.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<InspectionCarton>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.CartonVariant).HasMaxLength(100);
                e.Property(x => x.Description).HasMaxLength(300);
                e.Property(x => x.PackingQuantity).HasMaxLength(100);
                e.HasOne(x => x.Inspection)
                 .WithMany(i => i.Cartons)
                 .HasForeignKey(x => x.InspectionId)
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
                e.Property(x => x.ProtocolName).IsRequired().HasMaxLength(2000);
                e.Property(x => x.TestProtocol).HasColumnType("nvarchar(max)");
                e.Property(x => x.Procedure).HasColumnType("nvarchar(max)");
                e.Property(x => x.Requirements).HasColumnType("nvarchar(max)");
                e.HasIndex(x => new { x.Category, x.IsActive });

                // 1 master ─ N reference files (cascade delete)
                e.HasMany(x => x.References)
                 .WithOne(r => r.Master)
                 .HasForeignKey(r => r.PerformanceTestMasterId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<PerformanceTestReference>(e =>
            {
                e.ToTable("PerformanceTestReferences");
                e.HasKey(x => x.Id);

                e.Property(x => x.FileName).IsRequired().HasMaxLength(500);
                e.Property(x => x.ContentType).IsRequired().HasMaxLength(200)
                    .HasDefaultValue("application/octet-stream");
                e.Property(x => x.FileData).IsRequired();
                e.Property(x => x.FileSize).IsRequired();
                e.Property(x => x.SortOrder).HasDefaultValue(0);
                e.Property(x => x.UploadedAt).HasDefaultValueSql("SYSUTCDATETIME()");

                e.HasIndex(x => x.PerformanceTestMasterId)
                 .HasDatabaseName("IX_PerfTestRef_MasterId");
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
                e.Property(x => x.ShowInLoginAccount).HasDefaultValue(true);
                e.Property(x => x.UiLanguage).HasMaxLength(10);
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

            // ── MailLog (lưu lịch sử gửi mail vendor hàng tuần) ──
            builder.Entity<MailLog>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.VendorCode).HasMaxLength(50);
                e.Property(x => x.ToEmail).IsRequired().HasMaxLength(200);
                e.Property(x => x.Subject).IsRequired().HasMaxLength(300);
                e.Property(x => x.ErrorMessage).HasMaxLength(2000);
                e.HasIndex(x => x.SentAt);
                e.HasIndex(x => x.VendorId);
            });
            
            builder.Entity<ContainerCheck>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.JobNumber).IsRequired().HasMaxLength(30);
                e.HasIndex(x => x.JobNumber).IsUnique();
                e.Property(x => x.Status).IsRequired().HasMaxLength(20);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.InspectionDate);
                e.Property(x => x.CustomerCode).HasMaxLength(50);
                e.Property(x => x.CustomerName).HasMaxLength(200);
                e.Property(x => x.CustomerContact).HasMaxLength(150);
                e.Property(x => x.CustomerPhone).HasMaxLength(50);
                e.Property(x => x.CustomerEmail).HasMaxLength(200);
                e.Property(x => x.CustomerAddress).HasMaxLength(500);
                e.Property(x => x.VendorCode).HasMaxLength(50);
                e.Property(x => x.VendorName).HasMaxLength(200);
                e.Property(x => x.VendorContact).HasMaxLength(150);
                e.Property(x => x.VendorPhone).HasMaxLength(50);
                e.Property(x => x.VendorEmail).HasMaxLength(200);
                e.Property(x => x.VendorAddress).HasMaxLength(500);
                e.Property(x => x.ContainerNo).HasMaxLength(30);
                e.HasIndex(x => x.ContainerNo);
                e.Property(x => x.ContainerType).HasMaxLength(20);
                e.Property(x => x.PoNumber).HasMaxLength(100);
                e.Property(x => x.ShipmentReference).HasMaxLength(100);
                e.Property(x => x.Destination).HasMaxLength(200);
                e.Property(x => x.PlannedStartTime).HasMaxLength(10);
                e.Property(x => x.PlannedEndTime).HasMaxLength(10);
                e.Property(x => x.LoadingMethod).HasMaxLength(60);
                e.Property(x => x.TruckNo).HasMaxLength(50);
                e.Property(x => x.LoadingLocation).HasMaxLength(60);
                e.Property(x => x.LoadingAddress).HasMaxLength(500);
                e.Property(x => x.CargoDescription).HasMaxLength(500);
                e.Property(x => x.InspectorId).HasMaxLength(450);
                e.Property(x => x.InspectorName).HasMaxLength(150);
                e.Property(x => x.InspectionStartTime).HasMaxLength(10);
                e.Property(x => x.InspectionEndTime).HasMaxLength(10);
                e.Property(x => x.JobRemarks).HasMaxLength(500);
                e.Property(x => x.SevenPointResult).HasMaxLength(10);
                e.Property(x => x.AdditionalResult).HasMaxLength(10);
                e.Property(x => x.LoadingResult).HasMaxLength(10);
                e.Property(x => x.SealResult).HasMaxLength(10);
                e.Property(x => x.OverallResult).HasMaxLength(10);
                e.Property(x => x.AdditionalRemarks).HasMaxLength(500);
                e.Property(x => x.SealNumber).HasMaxLength(60);
                e.Property(x => x.SealCondition).HasMaxLength(20);
                e.Property(x => x.SealAppliedBy).HasMaxLength(30);
                e.Property(x => x.SealPhotoUrl).HasMaxLength(500);
                e.Property(x => x.SealRemarks).HasMaxLength(500);
                e.Property(x => x.LoadingStartTime).HasMaxLength(10);
                e.Property(x => x.LoadingEndTime).HasMaxLength(10);
                e.Property(x => x.LoadingMethodActual).HasMaxLength(60);
                e.Property(x => x.CartonCondition).HasMaxLength(10);
                e.Property(x => x.MarkingLabel).HasMaxLength(10);
                e.Property(x => x.PackagingProtection).HasMaxLength(10);
                e.Property(x => x.PalletCondition).HasMaxLength(10);
                e.Property(x => x.LoadingRemarks).HasMaxLength(500);
                e.Property(x => x.FinalRemarks).HasMaxLength(500);
                e.Property(x => x.InspectorSignatureUrl).HasMaxLength(500);
                e.Property(x => x.InspectorSignName).HasMaxLength(150);
                e.Property(x => x.InspectorSignPosition).HasMaxLength(100);
                e.Property(x => x.ReviewerSignatureUrl).HasMaxLength(500);
                e.Property(x => x.ReviewerSignName).HasMaxLength(150);
                e.Property(x => x.ReviewerSignPosition).HasMaxLength(100);
                e.Property(x => x.CreatedById).HasMaxLength(450);
                e.HasMany(x => x.Points).WithOne(p => p.Check).HasForeignKey(p => p.CheckId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Products).WithOne(p => p.Check).HasForeignKey(p => p.CheckId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Photos).WithOne(p => p.Check).HasForeignKey(p => p.CheckId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ContainerCheckPoint>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Result).HasMaxLength(10);
                e.Property(x => x.Findings).HasMaxLength(300);
                e.Property(x => x.Remarks).HasMaxLength(500);
                e.Property(x => x.PhotosJson).HasColumnType("nvarchar(max)");
                e.HasIndex(x => new { x.CheckId, x.PointNo }).IsUnique();
            });

            builder.Entity<ContainerCheckProduct>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.ProductCode).IsRequired().HasMaxLength(100);
                e.Property(x => x.Description).HasMaxLength(300);
                e.Property(x => x.Unit).HasMaxLength(20);
                e.Property(x => x.Result).HasMaxLength(10);
            });

            builder.Entity<ContainerCheckPhoto>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Category).IsRequired().HasMaxLength(20);
                e.Property(x => x.Url).IsRequired().HasMaxLength(500);
                e.Property(x => x.Caption).HasMaxLength(200);
            });

            builder.Entity<PartyContact>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.PartyType).IsRequired().HasMaxLength(20);
                e.Property(x => x.Name).IsRequired().HasMaxLength(150);
                e.Property(x => x.Position).HasMaxLength(100);
                e.Property(x => x.Email).HasMaxLength(200);
                e.Property(x => x.Phone).HasMaxLength(50);
                e.HasIndex(x => new { x.PartyType, x.PartyId });
            });

            builder.Entity<PartyReportSetting>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.PartyType).IsRequired().HasMaxLength(20);
                e.Property(x => x.Kind).IsRequired().HasMaxLength(20);
                e.Property(x => x.DeliveryMode).IsRequired().HasMaxLength(20);
                e.Property(x => x.Language).IsRequired().HasMaxLength(10);
                e.Property(x => x.Subject).HasMaxLength(300);
                e.Property(x => x.Body).HasColumnType("nvarchar(max)");
                e.Property(x => x.SendDays).HasMaxLength(60);
                e.Property(x => x.SendTime).HasMaxLength(10);
                e.Property(x => x.TimeZone).HasMaxLength(100);
                e.HasIndex(x => new { x.PartyType, x.PartyId, x.Kind }).IsUnique();
            });

            // ── AppSetting (cài đặt toàn hệ thống theo nhóm, JSON) ──
            builder.Entity<AppSetting>(e =>
            {
                e.HasKey(x => x.Section);
                e.Property(x => x.Section).HasMaxLength(64);
                e.Property(x => x.UpdatedBy).HasMaxLength(200);
            });

            // ── MailConfig (cấu hình mail singleton, Id luôn = 1) ──
            builder.Entity<MailConfig>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.SmtpHost).IsRequired().HasMaxLength(200);
                e.Property(x => x.SenderEmail).IsRequired().HasMaxLength(200);
                e.Property(x => x.SenderName).HasMaxLength(100);
                e.Property(x => x.Username).IsRequired().HasMaxLength(200);
                e.Property(x => x.UpdatedBy).HasMaxLength(200);
            });
        }
    }
}
