// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Infrastructure.Entities;

public class ApiKeyEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = default!;
    public string KeyHash { get; set; } = default!;   // SHA-256 hash
    public string KeyPrefix { get; set; } = default!;  // first 8 chars for identification
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }

    public TenantEntity Tenant { get; set; } = default!;
}
