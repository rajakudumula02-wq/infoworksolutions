// Feature: healthcare-fhir-api
using System.Text;
using System.Text.Json;

namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("admin/compliance")]
[Authorize(Policy = "admin")]
public class ComplianceReportController : ControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly ITenantService _tenantService;

    public ComplianceReportController(IMetricsService metricsService, ITenantService tenantService)
    {
        _metricsService = metricsService;
        _tenantService = tenantService;
    }

    // GET /admin/compliance/report?tenant=xxx&start-date=xxx&end-date=xxx&format=json|pdf|csv
    [HttpGet("report")]
    public async System.Threading.Tasks.Task<IActionResult> GetReport(
        [FromQuery] string tenant,
        [FromQuery(Name = "start-date")] DateTimeOffset startDate,
        [FromQuery(Name = "end-date")] DateTimeOffset endDate,
        [FromQuery] string? format,
        CancellationToken ct)
    {
        var resolved = await _tenantService.GetByIdAsync(tenant, ct);
        if (resolved is null)
        {
            var outcome = new OperationOutcome
            {
                Issue = [new OperationOutcome.IssueComponent
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = OperationOutcome.IssueType.NotFound,
                    Diagnostics = $"Tenant '{tenant}' not found"
                }]
            };
            return NotFound(new FhirJsonSerializer().SerializeToString(outcome));
        }

        var report = await _metricsService.GenerateComplianceReportAsync(tenant, startDate, endDate, ct);

        return (format?.ToLower()) switch
        {
            "csv" => File(Encoding.UTF8.GetBytes(ToCsv(report)), "text/csv", "compliance-report.csv"),
            "pdf" => File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(report)), "application/pdf", "compliance-report.pdf"),
            _ => Ok(report)
        };
    }

    private static string ToCsv(ComplianceReportResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Endpoint,RequestCount,AverageLatencyMs,ErrorRatePercent");
        foreach (var m in r.EndpointMetrics)
            sb.AppendLine($"{m.Endpoint},{m.RequestCount},{m.AverageLatencyMs},{m.ErrorRatePercent}");
        sb.AppendLine();
        sb.AppendLine($"UptimePercentage,{r.UptimePercentage}");
        sb.AppendLine($"TotalRequests,{r.TotalRequests}");
        sb.AppendLine($"OverallErrorRate,{r.OverallErrorRate}");
        return sb.ToString();
    }
}
