namespace HealthcareFhirApi.Core.Models;

public record AuditContext(
    string ClientId,
    string? PatientId,
    string ResourceType,
    string? ResourceId,
    string Action,
    DateTimeOffset Timestamp
);
