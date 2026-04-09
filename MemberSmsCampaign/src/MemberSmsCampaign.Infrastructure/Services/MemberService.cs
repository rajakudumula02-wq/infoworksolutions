using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Infrastructure.Services;

public class MemberService : IMemberService
{
    private readonly IMemberRepository _repo;

    public MemberService(IMemberRepository repo) => _repo = repo;

    public async Task<Member> CreateMemberAsync(Member member, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(member.FirstName))
            throw new ArgumentException("First name is required.", nameof(member));
        if (string.IsNullOrWhiteSpace(member.LastName))
            throw new ArgumentException("Last name is required.", nameof(member));

        // Check for duplicate by name or phone number
        var existing = await _repo.FindDuplicateAsync(member.FirstName, member.LastName, member.PhoneNumber, ct);
        if (existing is not null)
        {
            var reason = existing.FirstName.Equals(member.FirstName, StringComparison.OrdinalIgnoreCase)
                      && existing.LastName.Equals(member.LastName, StringComparison.OrdinalIgnoreCase)
                ? $"A member with name '{member.FirstName} {member.LastName}' already exists (#{existing.MemberNumber})."
                : $"A member with phone number '{member.PhoneNumber}' already exists (#{existing.MemberNumber}).";
            throw new InvalidOperationException(reason);
        }

        return await _repo.CreateAsync(member, ct);
    }

    public Task<Member?> GetMemberAsync(Guid id, CancellationToken ct = default)
        => _repo.GetByIdAsync(id, ct);

    public Task<List<Member>> ListMembersAsync(CancellationToken ct = default)
        => _repo.GetAllAsync(ct);

    public async Task<Member> UpdateMemberAsync(Member member, CancellationToken ct = default)
    {
        var existing = await _repo.GetByIdAsync(member.Id, ct)
            ?? throw new KeyNotFoundException($"Member '{member.Id}' not found.");
        return await _repo.UpdateAsync(member, ct);
    }

    public async Task<Member> OptOutAsync(Guid memberId, CancellationToken ct = default)
    {
        var member = await _repo.GetByIdAsync(memberId, ct)
            ?? throw new KeyNotFoundException($"Member '{memberId}' not found.");
        member.SmsOptOut = true;
        member.SmsOptOutDate = DateTimeOffset.UtcNow;
        return await _repo.UpdateAsync(member, ct);
    }

    public async Task<Member> OptInAsync(Guid memberId, CancellationToken ct = default)
    {
        var member = await _repo.GetByIdAsync(memberId, ct)
            ?? throw new KeyNotFoundException($"Member '{memberId}' not found.");
        member.SmsOptOut = false;
        member.SmsOptOutDate = null;
        return await _repo.UpdateAsync(member, ct);
    }

    public async Task DeleteMemberAsync(Guid memberId, CancellationToken ct = default)
    {
        var member = await _repo.GetByIdAsync(memberId, ct)
            ?? throw new KeyNotFoundException($"Member '{memberId}' not found.");
        await _repo.DeleteAsync(memberId, ct);
    }
}
