// Feature: healthcare-fhir-api
using HealthcareFhirApi.Infrastructure.Entities;

namespace HealthcareFhirApi.Infrastructure.Data;

public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options) { }

    public DbSet<TenantEntity> Tenants { get; set; } = default!;
    public DbSet<ApiKeyEntity> ApiKeys { get; set; } = default!;
    public DbSet<BulkExportJobEntity> BulkExportJobs { get; set; } = default!;
    public DbSet<MetricsRequestEntity> MetricsRequests { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OrganizationName).IsUnique();
            e.Property(x => x.OrganizationName).HasMaxLength(256);
            e.Property(x => x.ContactEmail).HasMaxLength(256);
            e.Property(x => x.PlanTier).HasMaxLength(50);
        });

        modelBuilder.Entity<ApiKeyEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.HasIndex(x => x.TenantId);
            e.Property(x => x.KeyHash).HasMaxLength(128);
            e.Property(x => x.KeyPrefix).HasMaxLength(16);
            e.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId);
        });

        modelBuilder.Entity<BulkExportJobEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TenantId);
            e.Property(x => x.Status).HasMaxLength(50);
            e.Property(x => x.Level).HasMaxLength(50);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<MetricsRequestEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.HasIndex(x => new { x.TenantId, x.Timestamp });
            e.Property(x => x.Endpoint).HasMaxLength(512);
        });
    }
}
