// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Models;

public record TenantMetrics(
    long RequestCount,
    double ErrorRatePercent,
    double AverageLatencyMs);
