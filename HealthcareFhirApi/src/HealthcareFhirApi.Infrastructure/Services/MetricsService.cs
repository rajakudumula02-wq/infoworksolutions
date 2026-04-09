// Feature: healthcare-fhir-api
using HealthcareFhirApi.Infrastructure.Data;

namespace HealthcareFhirApi.Infrastructure.Services;

public class MetricsService : IMetricsService
{
    private readonly TenantDbContext _db;

    public MetricsService(TenantDbContext db) => _db = db;

    public async SystemTask RecordRequestAsync(
        string tenantId, string endpoint, int statusCode, double latencyMs, CancellationToken ct = default)
    {
        _db.MetricsRequests.Add(new MetricsRequestEntity
        {
            TenantId = tenantId,
            Endpoint = endpoint,
            StatusCode = statusCode,
            LatencyMs = latencyMs
        });
        await _db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task<TenantMetrics> GetMetricsAsync(
        string tenantId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var rows = await _db.MetricsRequests
            .Where(m => m.TenantId == tenantId && m.Timestamp >= start && m.Timestamp <= end)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new TenantMetrics(0, 0, 0);

        var errorCount = rows.Count(r => r.StatusCode >= 400);
        return new TenantMetrics(
            rows.Count,
            Math.Round(100.0 * errorCount / rows.Count, 2),
            Math.Round(rows.Average(r => r.LatencyMs), 2));
    }

    public async System.Threading.Tasks.Task<ComplianceReportResult> GenerateComplianceReportAsync(
        string tenantId, DateTimeOffset start, DateTimeOffset end, CancellationToken ct = default)
    {
        var rows = await _db.MetricsRequests
            .Where(m => m.TenantId == tenantId && m.Timestamp >= start && m.Timestamp <= end)
            .ToListAsync(ct);

        if (rows.Count == 0)
            return new ComplianceReportResult(100.0, new List<EndpointMetric>(), 0, 0);

        var totalErrors = rows.Count(r => r.StatusCode >= 500);
        var uptime = Math.Round(100.0 * (rows.Count - totalErrors) / rows.Count, 2);

        var byEndpoint = rows.GroupBy(r => r.Endpoint).Select(g =>
        {
            var errs = g.Count(r => r.StatusCode >= 400);
            return new EndpointMetric(
                g.Key,
                g.Count(),
                Math.Round(g.Average(r => r.LatencyMs), 2),
                Math.Round(100.0 * errs / g.Count(), 2));
        }).ToList();

        return new ComplianceReportResult(
            uptime, byEndpoint, rows.Count,
            Math.Round(100.0 * rows.Count(r => r.StatusCode >= 400) / rows.Count, 2));
    }
}
