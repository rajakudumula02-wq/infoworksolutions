namespace MemberSmsCampaign.Core.Models;

public class CampaignRun
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public string Status { get; set; } = "running";
    public int TotalEligible { get; set; }
    public int TotalSent { get; set; }
    public int TotalFailed { get; set; }
    public int TotalSkipped { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
