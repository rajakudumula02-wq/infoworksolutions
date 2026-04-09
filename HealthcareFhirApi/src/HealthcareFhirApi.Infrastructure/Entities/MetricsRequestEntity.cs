// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Infrastructure.Entities;

public class MetricsRequestEntity
{
    public long Id { get; set; }
    public string TenantId { get; set; } = default!;
    public string Endpoint { get; set; } = default!;
    public int StatusCode { get; set; }
    public double LatencyMs { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
