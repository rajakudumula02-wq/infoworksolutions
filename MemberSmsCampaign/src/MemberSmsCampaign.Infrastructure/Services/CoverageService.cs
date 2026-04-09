using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Infrastructure.Services;

public class CoverageService : ICoverageService
{
    private readonly ICoverageRepository _repo;
    private readonly IMemberRepository _memberRepo;

    public CoverageService(ICoverageRepository repo, IMemberRepository memberRepo)
    {
        _repo = repo;
        _memberRepo = memberRepo;
    }

    public async Task<Coverage> CreateCoverageAsync(Coverage coverage, CancellationToken ct = default)
    {
        var member = await _memberRepo.GetByIdAsync(coverage.MemberId, ct)
            ?? throw new KeyNotFoundException($"Member '{coverage.MemberId}' not found.");
        return await _repo.CreateAsync(coverage, ct);
    }

    public Task<List<Coverage>> GetCoveragesByMemberAsync(Guid memberId, CancellationToken ct = default)
        => _repo.GetByMemberIdAsync(memberId, ct);

    public Task<List<Coverage>> ListActiveCoveragesAsync(CancellationToken ct = default)
        => _repo.GetAllActiveAsync(ct);

    public async Task<Coverage> UpdateCoverageAsync(Coverage coverage, CancellationToken ct = default)
    {
        var existing = await _repo.GetByIdAsync(coverage.Id, ct)
            ?? throw new KeyNotFoundException($"Coverage '{coverage.Id}' not found.");
        return await _repo.UpdateAsync(coverage, ct);
    }
}
