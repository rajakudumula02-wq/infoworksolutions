namespace MemberSmsCampaign.Core.Models;

public enum CampaignType { Welcome, Referral, Utilization, Holiday }
public enum CampaignStatus { Draft, Scheduled, Running, Completed, Cancelled }
public enum DeliveryStatus { Sent, Failed, Skipped, Excluded }

public class Campaign
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CampaignType Type { get; set; }
    public string MessageTemplate { get; set; } = string.Empty;
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public string TargetingMode { get; set; } = "auto"; // "auto" or "manual"
    public DateTimeOffset? ScheduledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
