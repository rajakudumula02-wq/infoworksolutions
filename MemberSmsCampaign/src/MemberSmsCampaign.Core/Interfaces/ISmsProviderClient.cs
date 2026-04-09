namespace MemberSmsCampaign.Core.Interfaces;

public interface ISmsProviderClient
{
    bool IsConfigured { get; }
    Task<bool> SendSmsAsync(string toPhoneNumber, string message, CancellationToken ct = default);
}
