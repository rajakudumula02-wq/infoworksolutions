// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Infrastructure.Entities;

public class TenantEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string OrganizationName { get; set; } = default!;
    public string ContactEmail { get; set; } = default!;
    public string PlanTier { get; set; } = "standard";
    public bool IsActive { get; set; } = true;
    public string? SmartAuthority { get; set; }
    public string? DatabaseConnectionString { get; set; }
    public int RateLimitRequestsPerSecond { get; set; } = 100;
    public int RateLimitBurstSize { get; set; } = 200;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
