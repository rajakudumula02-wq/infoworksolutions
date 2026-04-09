using MemberSmsCampaign.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MemberSmsCampaign.Functions;

public class CampaignSchedulerFunction
{
    private readonly ICampaignService _campaignService;
    private readonly ILogger<CampaignSchedulerFunction> _logger;

    public CampaignSchedulerFunction(ICampaignService campaignService, ILogger<CampaignSchedulerFunction> logger)
    {
        _campaignService = campaignService;
        _logger = logger;
    }

    /// <summary>
    /// Runs every minute to check for due campaigns and execute them.
    /// Cron: "0 */1 * * * *" = every minute
    /// </summary>
    [Function("CampaignScheduler")]
    public async Task Run([TimerTrigger("0 */1 * * * *")] TimerInfo timer, CancellationToken ct)
    {
        _logger.LogInformation("Campaign scheduler triggered at {Time}", DateTimeOffset.UtcNow);

        try
        {
            var dueCampaigns = await _campaignService.GetDueCampaignsAsync(ct);

            if (dueCampaigns.Count == 0)
            {
                _logger.LogInformation("No due campaigns found.");
                return;
            }

            _logger.LogInformation("Found {Count} due campaign(s).", dueCampaigns.Count);

            foreach (var campaign in dueCampaigns)
            {
                _logger.LogInformation("Executing campaign: {Name} ({Id}, type={Type})", campaign.Name, campaign.Id, campaign.Type);
                try
                {
                    await _campaignService.ExecuteCampaignRunAsync(campaign.Id, ct);
                    _logger.LogInformation("Campaign completed: {Name} ({Id})", campaign.Name, campaign.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Campaign execution failed: {Name} ({Id})", campaign.Name, campaign.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Campaign scheduler failed.");
        }
    }
}
