using MemberSmsCampaign.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MemberSmsCampaign.Infrastructure.Services;

public class CampaignSchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CampaignSchedulerService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public CampaignSchedulerService(IServiceScopeFactory scopeFactory, ILogger<CampaignSchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for app to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        _logger.LogInformation("Campaign scheduler started. Checking every {Interval}s.", _checkInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var campaignService = scope.ServiceProvider.GetRequiredService<ICampaignService>();
                var dueCampaigns = await campaignService.GetDueCampaignsAsync(stoppingToken);

                foreach (var campaign in dueCampaigns)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    _logger.LogInformation("Executing campaign: {Name} ({Id})", campaign.Name, campaign.Id);
                    try
                    {
                        await campaignService.ExecuteCampaignRunAsync(campaign.Id, stoppingToken);
                        _logger.LogInformation("Campaign completed: {Name}", campaign.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Campaign failed: {Name}", campaign.Name);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Scheduler tick error: {Message}", ex.Message);
            }
        }
    }
}
