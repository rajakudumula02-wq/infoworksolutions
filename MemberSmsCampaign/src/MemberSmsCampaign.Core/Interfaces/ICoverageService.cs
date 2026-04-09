using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface ICoverageService
{
    Task<Coverage> CreateCoverageAsync(Coverage coverage, CancellationToken ct = default);
    Task<List<Coverage>> GetCoveragesByMemberAsync(Guid memberId, CancellationToken ct = default);
    Task<List<Coverage>> ListActiveCoveragesAsync(CancellationToken ct = default);
    Task<Coverage> UpdateCoverageAsync(Coverage coverage, CancellationToken ct = default);
}
