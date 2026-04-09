// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Interfaces;

public interface IBulkExportService
{
    System.Threading.Tasks.Task<BulkExportJob> StartExportAsync(
        BulkExportLevel level, string? groupId,
        DateTimeOffset? since, IReadOnlyList<string>? types,
        string outputFormat, CancellationToken ct = default);

    System.Threading.Tasks.Task<BulkExportJob> GetJobStatusAsync(string jobId, CancellationToken ct = default);

    System.Threading.Tasks.Task<Stream> DownloadFileAsync(string jobId, string fileName, CancellationToken ct = default);
}
