using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Infrastructure.Services;

public class ManualSmsService : IManualSmsService
{
    private readonly IMemberRepository _memberRepo;
    private readonly IEligibilityService _eligibility;
    private readonly ISmsProviderClient _smsClient;
    private readonly IAuditRepository _audit;

    public ManualSmsService(IMemberRepository memberRepo, IEligibilityService eligibility,
        ISmsProviderClient smsClient, IAuditRepository audit)
    {
        _memberRepo = memberRepo;
        _eligibility = eligibility;
        _smsClient = smsClient;
        _audit = audit;
    }

    public async Task SendSingleAsync(Guid memberId, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 160)
            throw new ArgumentException("Message must be 1-160 characters.", nameof(message));
        if (!_smsClient.IsConfigured)
            throw new InvalidOperationException("Azure Communication Services is not configured.");

        var eligibility = await _eligibility.CheckEligibilityDetailedAsync(memberId, ct);
        if (!eligibility.Eligible)
            throw new InvalidOperationException($"Member is not eligible for SMS: {eligibility.Reason}");

        var member = await _memberRepo.GetByIdAsync(memberId, ct)
            ?? throw new KeyNotFoundException($"Member '{memberId}' not found.");
        if (member.SmsOptOut)
            throw new InvalidOperationException("Member has opted out of SMS communications.");
        if (string.IsNullOrWhiteSpace(member.PhoneNumber))
            throw new InvalidOperationException("No phone number on file for this member.");
        if (member.PhoneStatus == "not_in_service" || member.PhoneStatus == "disconnected" || member.PhoneStatus == "landline")
            throw new InvalidOperationException($"Member phone number is {member.PhoneStatus}. SMS cannot be delivered.");
        if (member.SmsFailureCount >= 3)
            throw new InvalidOperationException("Member phone has 3+ consecutive SMS failures. Verify the number before retrying.");

        var success = await _smsClient.SendSmsAsync(member.PhoneNumber, message, ct);

        if (!success)
        {
            member.SmsFailureCount++;
            if (member.SmsFailureCount >= 3)
                member.PhoneStatus = "not_in_service";
            member.PhoneStatusUpdatedAt = DateTimeOffset.UtcNow;
            await _memberRepo.UpdateAsync(member, ct);
        }
        else
        {
            if (member.SmsFailureCount > 0 || member.PhoneStatus != "valid")
            {
                member.SmsFailureCount = 0;
                member.PhoneStatus = "valid";
                member.PhoneStatusUpdatedAt = DateTimeOffset.UtcNow;
                await _memberRepo.UpdateAsync(member, ct);
            }
        }

        await _audit.LogAsync("SMS", memberId.ToString(), "single_sms_sent",
            $"{(eligibility.ExpiringWithin24Hours ? "[COVERAGE EXPIRING] " : "")}Single SMS to {member.FirstName} {member.LastName} ({member.PhoneNumber}): \"{message}\" — {(success ? "delivered" : "failed")}",
            ct: ct);
    }

    public async Task SendBulkAsync(List<Guid> memberIds, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 160)
            throw new ArgumentException("Message must be 1-160 characters.", nameof(message));
        if (!_smsClient.IsConfigured)
            throw new InvalidOperationException("Azure Communication Services is not configured.");
        if (memberIds.Count == 0)
            throw new ArgumentException("At least one member ID is required.", nameof(memberIds));

        int sent = 0, failed = 0, skipped = 0;

        foreach (var memberId in memberIds)
        {
            try
            {
                var eligible = await _eligibility.CheckEligibilityAsync(memberId, ct);
                if (!eligible) { skipped++; continue; }

                var member = await _memberRepo.GetByIdAsync(memberId, ct);
                if (member is null || string.IsNullOrWhiteSpace(member.PhoneNumber)) { skipped++; continue; }
                if (member.SmsOptOut) { skipped++; continue; }
                if (member.PhoneStatus is "not_in_service" or "disconnected" or "landline") { skipped++; continue; }
                if (member.SmsFailureCount >= 3) { skipped++; continue; }

                var success = await _smsClient.SendSmsAsync(member.PhoneNumber, message, ct);
                if (success) sent++; else failed++;
            }
            catch (InvalidOperationException) { throw; }
            catch { failed++; }
        }

        await _audit.LogAsync("SMS", Guid.NewGuid().ToString(), "bulk_sms_sent",
            $"Bulk SMS to {memberIds.Count} members: sent={sent}, failed={failed}, skipped={skipped}, message=\"{message}\"",
            ct: ct);
    }
}
