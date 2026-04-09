namespace MemberSmsCampaign.Core.Models;

public class Member
{
    public Guid Id { get; set; }
    public int MemberNumber { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string? PhoneNumber { get; set; }
    public string PhoneStatus { get; set; } = "unknown";
    public DateTimeOffset? PhoneStatusUpdatedAt { get; set; }
    public int SmsFailureCount { get; set; }
    public bool SmsOptOut { get; set; }
    public DateTimeOffset? SmsOptOutDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
