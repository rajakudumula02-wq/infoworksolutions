using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface IManualSmsLogRepository
{
    Task<ManualSmsLog> CreateAsync(ManualSmsLog log, CancellationToken ct = default);
    Task<ManualSmsLog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ManualSmsLog> UpdateAsync(ManualSmsLog log, CancellationToken ct = default);
}
