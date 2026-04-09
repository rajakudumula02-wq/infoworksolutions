// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Infrastructure.Entities;

public class BulkExportJobEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = default!;
    public string Status { get; set; } = "Accepted";
    public string Level { get; set; } = "System";
    public string? GroupId { get; set; }
    public DateTimeOffset? Since { get; set; }
    public string? Types { get; set; }           // comma-separated resource types
    public string OutputFormat { get; set; } = "application/fhir+ndjson";
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int ProgressPercent { get; set; }
    public string? OutputFilesJson { get; set; } // JSON array of output files
}
