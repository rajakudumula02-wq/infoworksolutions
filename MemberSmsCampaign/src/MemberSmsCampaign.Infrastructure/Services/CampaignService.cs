using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Infrastructure.Services;

public class CampaignService : ICampaignService
{
    private readonly ICampaignRepository _repository;
    private readonly IAuditRepository _audit;
    private readonly ITargetingService _targeting;
    private readonly IEligibilityService _eligibility;
    private readonly IMemberRepository _memberRepo;
    private readonly ISmsProviderClient _smsClient;

    public CampaignService(
        ICampaignRepository repository, IAuditRepository audit,
        ITargetingService targeting, IEligibilityService eligibility,
        IMemberRepository memberRepo, ISmsProviderClient smsClient)
    {
        _repository = repository;
        _audit = audit;
        _targeting = targeting;
        _eligibility = eligibility;
        _memberRepo = memberRepo;
        _smsClient = smsClient;
    }

    public async Task<Campaign> CreateCampaignAsync(string name, CampaignType type, string messageTemplate, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (!Enum.IsDefined(typeof(CampaignType), type))
            throw new ArgumentException($"Invalid campaign type '{type}'.", nameof(type));
        if (string.IsNullOrWhiteSpace(messageTemplate))
            throw new ArgumentException("Message template is required.", nameof(messageTemplate));
        if (messageTemplate.Length > 160)
            throw new ArgumentException("Message template must not exceed 160 characters.", nameof(messageTemplate));

        var now = DateTimeOffset.UtcNow;
        var campaign = new Campaign
        {
            Id = Guid.NewGuid(), Name = name, Type = type,
            MessageTemplate = messageTemplate, Status = CampaignStatus.Draft,
            CreatedAt = now, UpdatedAt = now
        };
        var created = await _repository.CreateAsync(campaign, ct);
        await _audit.LogAsync("Campaign", created.Id.ToString(), "created",
            $"Campaign '{name}' ({type}) created", ct: ct);
        return created;
    }

    public async Task<Campaign> ScheduleCampaignAsync(Guid campaignId, DateTimeOffset scheduledAt, CancellationToken ct = default)
    {
        if (scheduledAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Scheduled date must be in the future.", nameof(scheduledAt));
        var campaign = await _repository.GetByIdAsync(campaignId, ct)
            ?? throw new KeyNotFoundException($"Campaign '{campaignId}' not found.");
        if (campaign.Status != CampaignStatus.Draft && campaign.Status != CampaignStatus.Scheduled)
            throw new InvalidOperationException($"Cannot schedule a campaign in '{campaign.Status}' status.");
        if (campaign.Type == CampaignType.Holiday && scheduledAt.Month != 11 && scheduledAt.Month != 12)
            throw new ArgumentException("Holiday campaigns can only be scheduled for November or December.", nameof(scheduledAt));

        var prev = campaign.Status;
        campaign.Status = CampaignStatus.Scheduled;
        campaign.ScheduledAt = scheduledAt;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        var updated = await _repository.UpdateAsync(campaign, ct);
        await _audit.LogAsync("Campaign", campaignId.ToString(), "scheduled",
            $"Campaign '{campaign.Name}' scheduled for {scheduledAt:yyyy-MM-dd HH:mm} (was {prev})", ct: ct);
        return updated;
    }

    public async Task<Campaign> CancelCampaignAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await _repository.GetByIdAsync(campaignId, ct)
            ?? throw new KeyNotFoundException($"Campaign '{campaignId}' not found.");
        if (campaign.Status == CampaignStatus.Running || campaign.Status == CampaignStatus.Completed)
            throw new InvalidOperationException($"Cannot cancel a campaign in '{campaign.Status}' status.");
        if (campaign.Status == CampaignStatus.Cancelled)
            throw new InvalidOperationException("Campaign is already cancelled.");

        var prev = campaign.Status;
        campaign.Status = CampaignStatus.Cancelled;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        var updated = await _repository.UpdateAsync(campaign, ct);
        await _audit.LogAsync("Campaign", campaignId.ToString(), "cancelled",
            $"Campaign '{campaign.Name}' cancelled (was {prev})", ct: ct);
        return updated;
    }

    public Task<Campaign?> GetCampaignAsync(Guid id, CancellationToken ct = default)
        => _repository.GetByIdAsync(id, ct);

    public Task<List<Campaign>> ListCampaignsAsync(CancellationToken ct = default)
        => _repository.GetAllAsync(ct);

    public async Task<List<Campaign>> GetDueCampaignsAsync(CancellationToken ct = default)
    {
        var all = await _repository.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return all.Where(c => c.Status == CampaignStatus.Scheduled
            && c.ScheduledAt.HasValue && c.ScheduledAt.Value <= now).ToList();
    }

    public async Task ExecuteCampaignRunAsync(Guid campaignId, CancellationToken ct = default)
    {
        var campaign = await _repository.GetByIdAsync(campaignId, ct)
            ?? throw new KeyNotFoundException($"Campaign '{campaignId}' not found.");

        // 1. Set status to Running
        campaign.Status = CampaignStatus.Running;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.UpdateAsync(campaign, ct);
        await _audit.LogAsync("Campaign", campaignId.ToString(), "run_started",
            $"Campaign '{campaign.Name}' ({campaign.Type}) execution started", ct: ct);

        int sent = 0, failed = 0, skipped = 0;

        try
        {
            // 2. Get target members based on targeting mode
            List<Guid> targetMemberIds;
            if (campaign.TargetingMode == "manual")
            {
                targetMemberIds = await _repository.GetCampaignMemberIdsAsync(campaignId, ct);
                await _audit.LogAsync("Campaign", campaignId.ToString(), "targeting_complete",
                    $"Manual targeting: {targetMemberIds.Count} members assigned to campaign", ct: ct);
            }
            else
            {
                targetMemberIds = await _targeting.ResolveTargetMembersAsync(campaign.Type, ct);
                await _audit.LogAsync("Campaign", campaignId.ToString(), "targeting_complete",
                    $"Auto targeting resolved {targetMemberIds.Count} members for {campaign.Type} campaign", ct: ct);
            }

            // 3. For each target member: check eligibility, check phone, send SMS
            foreach (var memberId in targetMemberIds)
            {
                try
                {
                    var eligible = await _eligibility.CheckEligibilityAsync(memberId, ct);
                    if (!eligible) { skipped++; continue; }

                    var member = await _memberRepo.GetByIdAsync(memberId, ct);
                    if (member is null) { skipped++; continue; }
                    if (member.SmsOptOut) { skipped++; continue; }
                    if (string.IsNullOrWhiteSpace(member.PhoneNumber)) { skipped++; continue; }
                    if (member.PhoneStatus is "not_in_service" or "disconnected" or "landline") { skipped++; continue; }
                    if (member.SmsFailureCount >= 3) { skipped++; continue; }

                    // 4. Send SMS
                    if (_smsClient.IsConfigured)
                    {
                        var success = await _smsClient.SendSmsAsync(member.PhoneNumber, campaign.MessageTemplate, ct);
                        if (success)
                        {
                            sent++;
                            if (member.PhoneStatus != "valid" || member.SmsFailureCount > 0)
                            {
                                member.PhoneStatus = "valid";
                                member.SmsFailureCount = 0;
                                member.PhoneStatusUpdatedAt = DateTimeOffset.UtcNow;
                                await _memberRepo.UpdateAsync(member, ct);
                            }
                        }
                        else
                        {
                            failed++;
                            member.SmsFailureCount++;
                            if (member.SmsFailureCount >= 3) member.PhoneStatus = "not_in_service";
                            member.PhoneStatusUpdatedAt = DateTimeOffset.UtcNow;
                            await _memberRepo.UpdateAsync(member, ct);
                        }
                    }
                    else
                    {
                        // SMS provider not configured — simulate success for testing
                        sent++;
                        await _audit.LogAsync("Campaign", campaignId.ToString(), "sms_simulated",
                            $"SMS simulated to {member.FirstName} {member.LastName} ({member.PhoneNumber})", ct: ct);
                    }
                }
                catch
                {
                    failed++;
                }
            }
        }
        catch (Exception ex)
        {
            await _audit.LogAsync("Campaign", campaignId.ToString(), "run_error",
                $"Campaign execution error: {ex.Message}", ct: ct);
        }

        // 5. Set status to Completed
        campaign.Status = CampaignStatus.Completed;
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await _repository.UpdateAsync(campaign, ct);

        await _audit.LogAsync("Campaign", campaignId.ToString(), "run_completed",
            $"Campaign '{campaign.Name}' completed: sent={sent}, failed={failed}, skipped={skipped}", ct: ct);
    }
}
