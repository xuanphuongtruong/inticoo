using InticooInspection.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InticooInspection.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Inspection> Inspections => Set<Inspection>();
        public DbSet<InspectionStep> InspectionSteps => Set<InspectionStep>();

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
        }
    }
}
