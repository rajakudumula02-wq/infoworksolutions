namespace MemberSmsCampaign.Core.Interfaces;

public interface IManualSmsService
{
    Task SendSingleAsync(Guid memberId, string message, CancellationToken ct = default);
    Task SendBulkAsync(List<Guid> memberIds, string message, CancellationToken ct = default);
}
