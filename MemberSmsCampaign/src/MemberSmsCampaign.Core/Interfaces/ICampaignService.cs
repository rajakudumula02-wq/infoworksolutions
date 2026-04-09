using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface ICampaignService
{
    Task<Campaign> CreateCampaignAsync(string name, CampaignType type, string messageTemplate, CancellationToken ct = default);
    Task<Campaign> ScheduleCampaignAsync(Guid campaignId, DateTimeOffset scheduledAt, CancellationToken ct = default);
    Task<Campaign> CancelCampaignAsync(Guid campaignId, CancellationToken ct = default);
    Task<Campaign?> GetCampaignAsync(Guid id, CancellationToken ct = default);
    Task<List<Campaign>> ListCampaignsAsync(CancellationToken ct = default);
    Task<List<Campaign>> GetDueCampaignsAsync(CancellationToken ct = default);
    Task ExecuteCampaignRunAsync(Guid campaignId, CancellationToken ct = default);
}
