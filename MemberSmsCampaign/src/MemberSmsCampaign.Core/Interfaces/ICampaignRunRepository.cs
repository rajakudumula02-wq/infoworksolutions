using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface ICampaignRunRepository
{
    Task<CampaignRun> CreateAsync(CampaignRun run, CancellationToken ct = default);
    Task<CampaignRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<CampaignRun>> GetByCampaignIdAsync(Guid campaignId, CancellationToken ct = default);
    Task<CampaignRun> UpdateAsync(CampaignRun run, CancellationToken ct = default);
}
