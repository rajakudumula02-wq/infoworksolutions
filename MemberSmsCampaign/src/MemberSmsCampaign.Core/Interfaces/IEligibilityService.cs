namespace MemberSmsCampaign.Core.Interfaces;

public class EligibilityResult
{
    public bool Eligible { get; set; }
    public string? Reason { get; set; }
    public bool ExpiringWithin24Hours { get; set; }
}

public interface IEligibilityService
{
    Task<bool> CheckEligibilityAsync(Guid memberId, CancellationToken ct = default);
    Task<EligibilityResult> CheckEligibilityDetailedAsync(Guid memberId, CancellationToken ct = default);
}
