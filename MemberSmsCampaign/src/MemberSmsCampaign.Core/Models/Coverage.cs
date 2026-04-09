namespace MemberSmsCampaign.Core.Models;

public enum CoverageStatus { Active, Inactive, Cancelled }

public class Coverage
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public CoverageStatus Status { get; set; } = CoverageStatus.Active;
    public DateOnly PeriodStart { get; set; }
    public DateOnly? PeriodEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
