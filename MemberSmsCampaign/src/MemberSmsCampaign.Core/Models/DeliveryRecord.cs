namespace MemberSmsCampaign.Core.Models;

public class DeliveryRecord
{
    public Guid Id { get; set; }
    public Guid? CampaignRunId { get; set; }
    public Guid? ManualSmsId { get; set; }
    public Guid? CampaignId { get; set; }
    public string? CampaignName { get; set; }
    public string? CampaignType { get; set; }
    public string MemberId { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? MessageContent { get; set; }
    public DeliveryStatus Status { get; set; }
    public string? Reason { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
