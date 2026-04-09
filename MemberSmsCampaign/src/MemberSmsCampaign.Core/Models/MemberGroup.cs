namespace MemberSmsCampaign.Core.Models;

public class MemberGroup
{
    public Guid Id { get; set; }
    public int GroupNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int MemberCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
