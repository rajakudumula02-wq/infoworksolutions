using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface IDeliveryRecordRepository
{
    Task<DeliveryRecord> CreateAsync(DeliveryRecord record, CancellationToken ct = default);
    Task<List<DeliveryRecord>> GetByRunIdAsync(Guid campaignRunId, CancellationToken ct = default);
    Task<List<DeliveryRecord>> GetByManualSmsIdAsync(Guid manualSmsId, CancellationToken ct = default);
}
