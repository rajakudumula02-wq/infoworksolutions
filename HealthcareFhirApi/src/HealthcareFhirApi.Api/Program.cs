using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Microsoft.OpenApi.Models;
using HealthcareFhirApi.Infrastructure.Services;
using HealthcareFhirApi.Api.Formatters;
using HealthcareFhirApi.Api.Middleware;
using HealthcareFhirApi.Infrastructure.Data;
using HealthcareFhirApi.Infrastructure.Repositories;
using HealthcareFhirApi.Infrastructure.Services;
using HealthcareFhirApi.Core.Models;
using HealthcareFhirApi.Api.Auth;
using Hl7.Fhir.Model;

var builder = WebApplication.CreateBuilder(args);

// ── FHIR JSON formatters ──────────────────────────────────────────────────────
builder.Services.AddControllers(options =>
{
    options.InputFormatters.Insert(0, new FhirJsonInputFormatter());
    options.OutputFormatters.Insert(0, new FhirJsonOutputFormatter());
});

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Healthcare FHIR API",
        Version     = "v1",
        Description = "HL7 FHIR R4 compliant API for Patient, Provider, Claim, EOB, Coverage, Encounter, Location, Terminology, and more."
    });

    // Resolve conflicting routes between resource controllers and ProviderDirectory
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your SMART on FHIR JWT token"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ── EF Core + SQL Server ──────────────────────────────────────────────────────
builder.Services.AddDbContext<FhirDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FhirDb")));
builder.Services.AddDbContext<TenantDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FhirDb")));

// ── Redis ─────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    try
    {
        return ConnectionMultiplexer.Connect(
            builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false");
    }
    catch
    {
        return ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false");
    }
});
builder.Services.AddScoped<IDatabase>(sp =>
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());

// ── FHIR Repositories ─────────────────────────────────────────────────────────
builder.Services.AddScoped(typeof(IFhirResourceRepository<>), typeof(FhirResourceRepository<>));

// ── Domain Services ───────────────────────────────────────────────────────────
builder.Services.AddScoped<IFhirValidationService, FhirValidationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IPaginationService, PaginationService>();
builder.Services.AddScoped<ITerminologyService, TerminologyService>();
builder.Services.AddSingleton<ICapabilityStatementBuilder, CapabilityStatementBuilder>();
builder.Services.AddScoped<IBulkExportService, BulkExportService>();
builder.Services.AddScoped<IConsentService, ConsentService>();
builder.Services.AddScoped<CrdParserService>();
builder.Services.AddScoped<PasClaimParserService>();

// ── Multi-Tenant ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IMetricsService, MetricsService>();

// ── SMART on FHIR JWT authentication ─────────────────────────────────────────
builder.Services.AddAuthentication("ApiKey")
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.Authority = builder.Configuration["SmartAuth:Authority"];
        options.Audience  = builder.Configuration["SmartAuth:Audience"];
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero
        };
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthHandler>(
        "ApiKey", _ => { });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("ApiKey", JwtBearerDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
    options.AddPolicy("patient.read",  p => p.AddAuthenticationSchemes("ApiKey", JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser());
    options.AddPolicy("system.read",   p => p.AddAuthenticationSchemes("ApiKey", JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser());
    options.AddPolicy("user.read",     p => p.AddAuthenticationSchemes("ApiKey", JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser());
    options.AddPolicy("admin",         p => p.AddAuthenticationSchemes("ApiKey", JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser());
});

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Healthcare FHIR API v1");
    c.RoutePrefix = "swagger";
});
app.UseMiddleware<FhirExceptionMiddleware>();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseMiddleware<RateLimitingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<FhirContentNegotiationMiddleware>();
app.UseMiddleware<AuditLoggingMiddleware>();
app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
