using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface IMemberRepository
{
    Task<Member> CreateAsync(Member member, CancellationToken ct = default);
    Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<Member>> GetAllAsync(CancellationToken ct = default);
    Task<Member> UpdateAsync(Member member, CancellationToken ct = default);
    Task<Member?> FindDuplicateAsync(string firstName, string lastName, string? phoneNumber, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
