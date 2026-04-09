// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Models;

public record ComplianceReportResult(
    double UptimePercentage,
    List<EndpointMetric> EndpointMetrics,
    long TotalRequests,
    double OverallErrorRate);

public record EndpointMetric(
    string Endpoint,
    long RequestCount,
    double AverageLatencyMs,
    double ErrorRatePercent);
