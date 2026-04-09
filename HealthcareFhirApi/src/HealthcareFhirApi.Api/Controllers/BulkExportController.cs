// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Authorize(Policy = "system.read")]
public class BulkExportController : FhirControllerBase
{
    private readonly IBulkExportService _exportService;

    public BulkExportController(IBulkExportService exportService)
    {
        _exportService = exportService;
    }

    /// <summary>System-level export: POST /$export</summary>
    [HttpPost("/$export")]
    public async System.Threading.Tasks.Task<IActionResult> SystemExport(
        [FromQuery] DateTimeOffset? _since,
        [FromQuery] string? _type,
        [FromQuery] string? _outputFormat,
        CancellationToken ct)
    {
        var types = ParseTypes(_type);
        var format = _outputFormat ?? "application/fhir+ndjson";

        var job = await _exportService.StartExportAsync(
            BulkExportLevel.System, null, _since, types, format, ct);

        return AcceptedExport(job);
    }

    /// <summary>Patient-level export: POST /Patient/$export</summary>
    [HttpPost("Patient/$export")]
    public async System.Threading.Tasks.Task<IActionResult> PatientExport(
        [FromQuery] DateTimeOffset? _since,
        [FromQuery] string? _type,
        [FromQuery] string? _outputFormat,
        CancellationToken ct)
    {
        var types = ParseTypes(_type);
        var format = _outputFormat ?? "application/fhir+ndjson";

        var job = await _exportService.StartExportAsync(
            BulkExportLevel.Patient, null, _since, types, format, ct);

        return AcceptedExport(job);
    }

    /// <summary>Group-level export: POST /Group/{id}/$export</summary>
    [HttpPost("Group/{id}/$export")]
    public async System.Threading.Tasks.Task<IActionResult> GroupExport(
        string id,
        [FromQuery] DateTimeOffset? _since,
        [FromQuery] string? _type,
        [FromQuery] string? _outputFormat,
        CancellationToken ct)
    {
        var types = ParseTypes(_type);
        var format = _outputFormat ?? "application/fhir+ndjson";

        var job = await _exportService.StartExportAsync(
            BulkExportLevel.Group, id, _since, types, format, ct);

        return AcceptedExport(job);
    }

    /// <summary>Poll export job status: GET /$export-poll/{jobId}</summary>
    [HttpGet("/$export-poll/{jobId}")]
    public async System.Threading.Tasks.Task<IActionResult> PollExportStatus(string jobId, CancellationToken ct)
    {
        var job = await _exportService.GetJobStatusAsync(jobId, ct);

        if (job.Status is BulkExportStatus.Accepted or BulkExportStatus.InProgress)
        {
            if (job.ProgressPercent.HasValue)
                Response.Headers["X-Progress"] = $"{job.ProgressPercent}%";

            return StatusCode(202);
        }

        if (job.Status == BulkExportStatus.Error)
        {
            return StatusCode(500, new OperationOutcome
            {
                Issue = new List<OperationOutcome.IssueComponent>
                {
                    new()
                    {
                        Severity    = OperationOutcome.IssueSeverity.Error,
                        Code        = OperationOutcome.IssueType.Exception,
                        Diagnostics = "Bulk export job failed"
                    }
                }
            });
        }

        // Complete
        var result = new
        {
            transactionTime = job.CompletedAt?.ToString("o"),
            request = $"{Request.Scheme}://{Request.Host}/$export",
            requiresAccessToken = true,
            output = job.OutputFiles?.Select(f => new { type = f.Type, url = f.Url }) ?? Enumerable.Empty<object>()
        };

        return Ok(result);
    }

    private static IReadOnlyList<string>? ParseTypes(string? typeParam)
    {
        if (string.IsNullOrWhiteSpace(typeParam))
            return null;

        return typeParam.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private IActionResult AcceptedExport(BulkExportJob job)
    {
        var pollUrl = $"{Request.Scheme}://{Request.Host}/$export-poll/{job.JobId}";
        Response.Headers["Content-Location"] = pollUrl;
        return StatusCode(202);
    }
}
