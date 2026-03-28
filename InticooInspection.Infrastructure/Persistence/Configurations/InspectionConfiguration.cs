using InticooInspection.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InticooInspection.Infrastructure.Persistence.Configurations
{
    // ── Inspection ───────────────────────────────────────────
    public class InspectionConfiguration : IEntityTypeConfiguration<Inspection>
    {
        public void Configure(EntityTypeBuilder<Inspection> b)
        {
            b.ToTable("Inspections");
            b.HasKey(x => x.Id);

            // General Info
            b.Property(x => x.CustomerName).HasMaxLength(200).IsRequired();
            b.Property(x => x.CustomerId).HasMaxLength(50);
            b.Property(x => x.VendorName).HasMaxLength(200).IsRequired();
            b.Property(x => x.VendorId).HasMaxLength(50);
            b.Property(x => x.InspectionLocation).HasMaxLength(300);
            b.Property(x => x.PoNumber).HasMaxLength(100);
            b.Property(x => x.ItemNumber).HasMaxLength(100);
            b.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            b.Property(x => x.ProductCategory).HasMaxLength(100);
            b.Property(x => x.Photo1Url).HasMaxLength(500);
            b.Property(x => x.Photo2Url).HasMaxLength(500);

            // AQL
            b.Property(x => x.AqlInspectionLevel).HasConversion<int>();
            b.Property(x => x.CriticalAql).HasConversion<int>();
            b.Property(x => x.MajorAql).HasConversion<int>();
            b.Property(x => x.MinorAql).HasConversion<int>();

            // Status & Audit
            b.Property(x => x.Status).HasConversion<int>();
            b.Property(x => x.InspectionType).HasConversion<int>();
            b.Property(x => x.CreatedById).HasMaxLength(450);

            // Office Use
            b.Property(x => x.InspectorName).HasMaxLength(200);
            b.Property(x => x.InspectorId).HasMaxLength(100);
            b.Property(x => x.JobNumber).HasMaxLength(50);

            // Legacy
            b.Property(x => x.Title).HasMaxLength(300);
            b.Property(x => x.Description).HasMaxLength(2000);

            // Relationships
            b.HasOne(x => x.CreatedBy)
             .WithMany()
             .HasForeignKey(x => x.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Packaging)
             .WithOne(x => x.Inspection)
             .HasForeignKey<InspectionPackaging>(x => x.InspectionId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(x => x.ProductSpec)
             .WithOne(x => x.Inspection)
             .HasForeignKey<InspectionProductSpec>(x => x.InspectionId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.ColourSwatches)
             .WithOne(x => x.Inspection)
             .HasForeignKey(x => x.InspectionId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.PerformanceTests)
             .WithOne(x => x.Inspection)
             .HasForeignKey(x => x.InspectionId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.References)
             .WithOne(x => x.Inspection)
             .HasForeignKey(x => x.InspectionId)
             .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(x => x.Steps)
             .WithOne(x => x.Inspection)
             .HasForeignKey(x => x.InspectionId)
             .OnDelete(DeleteBehavior.Cascade);
        }
    }

    // ── InspectionPackaging ──────────────────────────────────
    public class InspectionPackagingConfiguration : IEntityTypeConfiguration<InspectionPackaging>
    {
        public void Configure(EntityTypeBuilder<InspectionPackaging> b)
        {
            b.ToTable("InspectionPackagings");
            b.HasKey(x => x.Id);

            b.Property(x => x.ItemNumber).HasMaxLength(100);
            b.Property(x => x.CartonNumber).HasMaxLength(100);
            b.Property(x => x.PackagingType).HasConversion<int>();
            b.Property(x => x.CartonColor).HasConversion<int>();
            b.Property(x => x.CardboardType).HasConversion<int>();
            b.Property(x => x.ShippingMark).HasConversion<int>();
            b.Property(x => x.InnerPackingRemark).HasMaxLength(500);
            b.Property(x => x.OuterSizeL).HasColumnType("decimal(10,2)");
            b.Property(x => x.OuterSizeW).HasColumnType("decimal(10,2)");
            b.Property(x => x.OuterSizeH).HasColumnType("decimal(10,2)");
            b.Property(x => x.OuterWeight).HasColumnType("decimal(10,3)");
        }
    }

    // ── InspectionProductSpec ────────────────────────────────
    public class InspectionProductSpecConfiguration : IEntityTypeConfiguration<InspectionProductSpec>
    {
        public void Configure(EntityTypeBuilder<InspectionProductSpec> b)
        {
            b.ToTable("InspectionProductSpecs");
            b.HasKey(x => x.Id);

            b.Property(x => x.SizeL).HasColumnType("decimal(10,2)");
            b.Property(x => x.SizeW).HasColumnType("decimal(10,2)");
            b.Property(x => x.SizeH).HasColumnType("decimal(10,2)");
            b.Property(x => x.Weight).HasColumnType("decimal(10,3)");
        }
    }

    // ── InspectionColourSwatch ───────────────────────────────
    public class InspectionColourSwatchConfiguration : IEntityTypeConfiguration<InspectionColourSwatch>
    {
        public void Configure(EntityTypeBuilder<InspectionColourSwatch> b)
        {
            b.ToTable("InspectionColourSwatches");
            b.HasKey(x => x.Id);
            b.Property(x => x.Material).HasMaxLength(300).IsRequired();
        }
    }

    // ── InspectionPerformanceTest ────────────────────────────
    public class InspectionPerformanceTestConfiguration : IEntityTypeConfiguration<InspectionPerformanceTest>
    {
        public void Configure(EntityTypeBuilder<InspectionPerformanceTest> b)
        {
            b.ToTable("InspectionPerformanceTests");
            b.HasKey(x => x.Id);

            b.Property(x => x.Category).HasMaxLength(200);
            b.Property(x => x.TestItem).HasMaxLength(300);
            b.Property(x => x.TestRequirement).HasMaxLength(500);
            b.Property(x => x.Remark).HasMaxLength(500);
        }
    }

    // ── InspectionReference ──────────────────────────────────
    public class InspectionReferenceConfiguration : IEntityTypeConfiguration<InspectionReference>
    {
        public void Configure(EntityTypeBuilder<InspectionReference> b)
        {
            b.ToTable("InspectionReferences");
            b.HasKey(x => x.Id);

            b.Property(x => x.ReferenceName).HasMaxLength(300);
            b.Property(x => x.FileUrl).HasMaxLength(500);
            b.Property(x => x.FileName).HasMaxLength(255);
            b.Property(x => x.Remark).HasMaxLength(500);
        }
    }

    // ── InspectionStep (legacy) ──────────────────────────────
    public class InspectionStepConfiguration : IEntityTypeConfiguration<InspectionStep>
    {
        public void Configure(EntityTypeBuilder<InspectionStep> b)
        {
            b.ToTable("InspectionSteps");
            b.HasKey(x => x.Id);

            b.Property(x => x.Title).HasMaxLength(300).IsRequired();
            b.Property(x => x.Description).HasMaxLength(1000);
            b.Property(x => x.Note).HasMaxLength(1000);
            b.Property(x => x.Status).HasConversion<int>();
        }
    }
}
