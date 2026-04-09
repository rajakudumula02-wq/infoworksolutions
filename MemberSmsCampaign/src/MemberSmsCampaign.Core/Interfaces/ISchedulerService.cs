namespace MemberSmsCampaign.Core.Interfaces;

public interface ISchedulerService
{
    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
}
