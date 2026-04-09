using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Infrastructure.Data;
using MemberSmsCampaign.Infrastructure.Repositories;
using MemberSmsCampaign.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Add controllers
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Member SMS Campaign API",
        Version = "v1",
        Description = "Healthcare member SMS campaign management — campaigns, members, coverages, groups, manual SMS, and audit trail.",
    });
});

// Infrastructure
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));

// Repositories
builder.Services.AddScoped<ICampaignRepository, CampaignRepository>();
builder.Services.AddScoped<IMemberRepository, MemberRepository>();
builder.Services.AddScoped<ICoverageRepository, CoverageRepository>();
builder.Services.AddScoped<IGroupRepository, GroupRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<ICampaignRunRepository, CampaignRunRepository>();
builder.Services.AddScoped<IDeliveryRecordRepository, DeliveryRecordRepository>();
builder.Services.AddScoped<IManualSmsLogRepository, ManualSmsLogRepository>();

// Services
builder.Services.AddScoped<ICampaignService, CampaignService>();
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddScoped<ICoverageService, CoverageService>();
builder.Services.AddScoped<IEligibilityService, EligibilityService>();
builder.Services.AddScoped<ITargetingService, TargetingService>();
builder.Services.AddScoped<IManualSmsService, ManualSmsService>();
builder.Services.AddScoped<ISmsProviderClient, SmsProviderClient>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapFallbackToFile("index.html");

app.Run();
