namespace MemberSmsCampaign.Core.Models;

public class AuditLog
{
    public Guid Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string PerformedBy { get; set; } = "system";
    public DateTimeOffset CreatedAt { get; set; }
}
