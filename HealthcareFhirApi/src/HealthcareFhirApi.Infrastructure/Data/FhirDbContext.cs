namespace HealthcareFhirApi.Infrastructure.Data;

public class FhirDbContext : DbContext
{
    public FhirDbContext(DbContextOptions<FhirDbContext> options) : base(options) { }

    public DbSet<FhirResourceEntity> Resources { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FhirResourceEntity>(e =>
        {
            e.HasKey(x => new { x.TenantId, x.ResourceType, x.Id });
            e.Property(x => x.Data).HasColumnType("nvarchar(max)");
            e.HasIndex(x => x.ResourceType);
            e.HasIndex(x => x.LastUpdated);
            e.HasIndex(x => x.TenantId);
        });
    }
}
