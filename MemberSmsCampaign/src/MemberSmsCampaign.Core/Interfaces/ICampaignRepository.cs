using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface ICampaignRepository
{
    Task<Campaign> CreateAsync(Campaign campaign, CancellationToken ct = default);
    Task<Campaign?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Campaign>> GetAllAsync(CancellationToken ct = default);
    Task<Campaign> UpdateAsync(Campaign campaign, CancellationToken ct = default);
    Task<Campaign?> FindByNameAsync(string name, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task AddMembersAsync(Guid campaignId, List<Guid> memberIds, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid campaignId, Guid memberId, CancellationToken ct = default);
    Task<List<Guid>> GetCampaignMemberIdsAsync(Guid campaignId, CancellationToken ct = default);
}
