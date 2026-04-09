// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Models;

/// <summary>Scoped per-request tenant context populated by TenantResolutionMiddleware.</summary>
public class TenantContext
{
    public string TenantId { get; set; } = default!;
    public string OrganizationName { get; set; } = default!;
    public bool IsActive { get; set; }
    public string? SmartAuthority { get; set; }
    public string? DatabaseConnectionString { get; set; }
    public int RateLimitRequestsPerSecond { get; set; } = 100;
    public int RateLimitBurstSize { get; set; } = 200;
}
