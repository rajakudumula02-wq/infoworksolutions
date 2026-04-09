using Azure.Communication.Sms;
using MemberSmsCampaign.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace MemberSmsCampaign.Infrastructure.Services;

public class SmsProviderClient : ISmsProviderClient
{
    private readonly string _connectionString;
    private readonly string _fromNumber;

    public SmsProviderClient(IConfiguration configuration)
    {
        _connectionString = configuration["AzureCommunicationServices:ConnectionString"] ?? string.Empty;
        _fromNumber = configuration["AzureCommunicationServices:FromNumber"] ?? string.Empty;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString) && !string.IsNullOrWhiteSpace(_fromNumber);

    public async Task<bool> SendSmsAsync(string toPhoneNumber, string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString) || string.IsNullOrWhiteSpace(_fromNumber))
            throw new InvalidOperationException("Azure Communication Services is not configured.");

        var retries = 0;
        const int maxRetries = 3;

        while (true)
        {
            try
            {
                var client = new SmsClient(_connectionString);
                var response = await client.SendAsync(_fromNumber, toPhoneNumber, message, cancellationToken: ct);
                var result = response.Value;
                return result.Successful;
            }
            catch (Exception) when (retries < maxRetries)
            {
                retries++;
                var delay = (int)Math.Pow(2, retries - 1) * 1000; // 1s, 2s, 4s
                await Task.Delay(delay, ct);
            }
        }
    }
}
