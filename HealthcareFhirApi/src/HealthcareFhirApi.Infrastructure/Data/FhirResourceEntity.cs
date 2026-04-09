namespace HealthcareFhirApi.Infrastructure.Data;

public class FhirResourceEntity
{
    public string Id { get; set; } = default!;
    public string ResourceType { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public string Data { get; set; } = default!;
    public DateTimeOffset LastUpdated { get; set; }
    public bool IsDeleted { get; set; }
    public long VersionId { get; set; }
}
