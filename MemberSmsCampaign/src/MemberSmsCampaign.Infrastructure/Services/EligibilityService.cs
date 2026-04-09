using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Infrastructure.Services;

public class EligibilityService : IEligibilityService
{
    private readonly ICoverageRepository _coverageRepo;

    public EligibilityService(ICoverageRepository coverageRepo) => _coverageRepo = coverageRepo;

    public async Task<bool> CheckEligibilityAsync(Guid memberId, CancellationToken ct = default)
    {
        var result = await CheckEligibilityDetailedAsync(memberId, ct);
        return result.Eligible;
    }

    public async Task<EligibilityResult> CheckEligibilityDetailedAsync(Guid memberId, CancellationToken ct = default)
    {
        var coverages = await _coverageRepo.GetByMemberIdAsync(memberId, ct);

        if (coverages.Count == 0)
            return new EligibilityResult { Eligible = false, Reason = "No coverage records found." };

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // Find active coverages that haven't expired
        var activeCoverages = coverages.Where(c =>
            c.Status == CoverageStatus.Active &&
            c.PeriodStart <= today &&
            (c.PeriodEnd is null || c.PeriodEnd >= today)).ToList();

        if (activeCoverages.Count == 0)
        {
            // Check if all coverages are expired
            var allExpired = coverages.All(c => c.PeriodEnd.HasValue && c.PeriodEnd.Value < today);
            if (allExpired)
                return new EligibilityResult { Eligible = false, Reason = "All coverages have expired." };

            var allInactive = coverages.All(c => c.Status != CoverageStatus.Active);
            if (allInactive)
                return new EligibilityResult { Eligible = false, Reason = "No active coverage. Status: " + coverages.First().Status };

            return new EligibilityResult { Eligible = false, Reason = "No active coverage for current date." };
        }

        // Check if any active coverage is expiring within 24 hours
        var expiringWithin24h = activeCoverages.Any(c =>
        {
            if (c.PeriodEnd is null) return false;
            var expirationDateTime = c.PeriodEnd.Value.ToDateTime(new TimeOnly(23, 59, 59));
            var hoursLeft = (expirationDateTime - now).TotalHours;
            return hoursLeft > 0 && hoursLeft <= 24;
        });

        return new EligibilityResult
        {
            Eligible = true,
            ExpiringWithin24Hours = expiringWithin24h,
            Reason = expiringWithin24h ? "Coverage expiring within 24 hours." : null
        };
    }
}
