// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Infrastructure.Services;

public class BulkExportService : IBulkExportService
{
    private static readonly HashSet<string> SupportedTypes = new()
    {
        "Patient", "Condition", "AllergyIntolerance", "MedicationRequest",
        "Immunization", "Procedure", "DiagnosticReport", "Coverage",
        "ExplanationOfBenefit", "Claim", "Encounter", "Organization",
        "Practitioner", "Location"
    };

    public async Task<BulkExportJob> StartExportAsync(
        BulkExportLevel level, string? groupId,
        DateTimeOffset? since, IReadOnlyList<string>? types,
        string outputFormat, CancellationToken ct = default)
    {
        if (types is not null)
        {
            var unsupported = types.Except(SupportedTypes).ToList();
            if (unsupported.Count > 0)
                throw new UnsupportedResourceTypeException(unsupported.First());
        }

        var job = new BulkExportJob(
            JobId: Guid.NewGuid().ToString("N"),
            Status: BulkExportStatus.Accepted,
            Level: level,
            GroupId: groupId,
            Since: since,
            Types: types,
            OutputFormat: outputFormat,
            RequestedAt: DateTimeOffset.UtcNow,
            CompletedAt: null,
            ProgressPercent: 0,
            OutputFiles: null);

        await SystemTask.CompletedTask;
        return job;
    }

    public async Task<BulkExportJob> GetJobStatusAsync(string jobId, CancellationToken ct = default)
    {
        await SystemTask.CompletedTask;
        throw new ResourceNotFoundException("BulkExportJob", jobId);
    }

    public async Task<Stream> DownloadFileAsync(string jobId, string fileName, CancellationToken ct = default)
    {
        await SystemTask.CompletedTask;
        throw new ResourceNotFoundException("BulkExportFile", $"{jobId}/{fileName}");
    }
}
