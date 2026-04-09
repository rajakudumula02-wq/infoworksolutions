using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface IMemberService
{
    Task<Member> CreateMemberAsync(Member member, CancellationToken ct = default);
    Task<Member?> GetMemberAsync(Guid id, CancellationToken ct = default);
    Task<List<Member>> ListMembersAsync(CancellationToken ct = default);
    Task<Member> UpdateMemberAsync(Member member, CancellationToken ct = default);
    Task<Member> OptOutAsync(Guid memberId, CancellationToken ct = default);
    Task<Member> OptInAsync(Guid memberId, CancellationToken ct = default);
    Task DeleteMemberAsync(Guid memberId, CancellationToken ct = default);
}
