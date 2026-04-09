using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface ITargetingService
{
    Task<List<Guid>> ResolveTargetMembersAsync(CampaignType campaignType, CancellationToken ct = default);
}
