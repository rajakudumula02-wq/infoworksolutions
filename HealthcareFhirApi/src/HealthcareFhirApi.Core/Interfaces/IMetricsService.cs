// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Interfaces;

public interface IMetricsService
{
    SystemTask RecordRequestAsync(string tenantId, string endpoint, int statusCode, double latencyMs, CancellationToken ct = default);
    System.Threading.Tasks.Task<TenantMetrics> GetMetricsAsync(string tenantId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
    System.Threading.Tasks.Task<ComplianceReportResult> GenerateComplianceReportAsync(string tenantId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default);
}
