using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface ICoverageRepository
{
    Task<Coverage> CreateAsync(Coverage coverage, CancellationToken ct = default);
    Task<Coverage?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Coverage>> GetByMemberIdAsync(Guid memberId, CancellationToken ct = default);
    Task<List<Coverage>> GetAllActiveAsync(CancellationToken ct = default);
    Task<Coverage> UpdateAsync(Coverage coverage, CancellationToken ct = default);
    Task<Coverage?> FindDuplicateAsync(Guid memberId, string planName, DateOnly periodStart, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
