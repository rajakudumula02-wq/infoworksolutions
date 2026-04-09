using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface IGroupRepository
{
    Task<MemberGroup> CreateAsync(MemberGroup group, CancellationToken ct = default);
    Task<MemberGroup?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<MemberGroup>> GetAllAsync(CancellationToken ct = default);
    Task<MemberGroup> UpdateAsync(MemberGroup group, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task AddMemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default);
    Task<List<Member>> GetMembersAsync(Guid groupId, CancellationToken ct = default);
    Task<List<Guid>> GetMemberIdsAsync(Guid groupId, CancellationToken ct = default);
}
