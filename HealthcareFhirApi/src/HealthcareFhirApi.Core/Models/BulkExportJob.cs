// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Models;

public record BulkExportJob(
    string JobId,
    BulkExportStatus Status,
    BulkExportLevel Level,
    string? GroupId,
    DateTimeOffset? Since,
    IReadOnlyList<string>? Types,
    string OutputFormat,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    int? ProgressPercent,
    IReadOnlyList<BulkExportOutputFile>? OutputFiles
);

public record BulkExportOutputFile(string Type, string Url);

public enum BulkExportStatus { Accepted, InProgress, Complete, Error }

public enum BulkExportLevel { System, Patient, Group }
