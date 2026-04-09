using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Infrastructure.Services;

public class TargetingService : ITargetingService
{
    private readonly ICoverageRepository _coverageRepo;

    public TargetingService(ICoverageRepository coverageRepo) => _coverageRepo = coverageRepo;

    public async Task<List<Guid>> ResolveTargetMembersAsync(CampaignType campaignType, CancellationToken ct = default)
    {
        var activeCoverages = await _coverageRepo.GetAllActiveAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thirtyDaysAgo = today.AddDays(-30);

        return campaignType switch
        {
            // Welcome: coverage started within last 30 days
            CampaignType.Welcome => activeCoverages
                .Where(c => c.PeriodStart >= thirtyDaysAgo)
                .Select(c => c.MemberId)
                .Distinct()
                .ToList(),

            // Referral: coverage started more than 30 days ago
            CampaignType.Referral => activeCoverages
                .Where(c => c.PeriodStart < thirtyDaysAgo)
                .Select(c => c.MemberId)
                .Distinct()
                .ToList(),

            // Utilization & Holiday: all members with active coverage
            CampaignType.Utilization or CampaignType.Holiday => activeCoverages
                .Select(c => c.MemberId)
                .Distinct()
                .ToList(),

            _ => new List<Guid>(),
        };
    }
}
