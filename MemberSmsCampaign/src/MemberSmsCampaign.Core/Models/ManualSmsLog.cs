namespace MemberSmsCampaign.Core.Models;

public class ManualSmsLog
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // "single" or "bulk"
    public string Message { get; set; } = string.Empty;
    public Guid? CampaignId { get; set; }
    public int TotalSent { get; set; }
    public int TotalFailed { get; set; }
    public int TotalSkipped { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
