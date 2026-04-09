using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Infrastructure.Data;
using MemberSmsCampaign.Infrastructure.Repositories;
using MemberSmsCampaign.Infrastructure.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var connectionString = context.Configuration["SqlConnectionString"] ?? string.Empty;
        services.AddSingleton(new SqlConnectionFactory(connectionString));

        services.AddScoped<ICampaignRepository, CampaignRepository>();
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<ICoverageRepository, CoverageRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<ITargetingService, TargetingService>();
        services.AddScoped<IEligibilityService, EligibilityService>();
        services.AddScoped<ISmsProviderClient, SmsProviderClient>();
    })
    .Build();

host.Run();
