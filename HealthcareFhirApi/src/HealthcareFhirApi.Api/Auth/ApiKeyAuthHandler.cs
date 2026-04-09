// Feature: healthcare-fhir-api
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using HealthcareFhirApi.Core.Models;
using SecurityClaim = System.Security.Claims.Claim;
using System.Security.Claims;

namespace HealthcareFhirApi.Api.Auth;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly TenantContext _tenantContext;

    public ApiKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TenantContext tenantContext)
        : base(options, logger, encoder)
    {
        _tenantContext = tenantContext;
    }

    protected override System.Threading.Tasks.Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!string.IsNullOrEmpty(_tenantContext.TenantId))
        {
            var claims = new List<SecurityClaim>
            {
                new(ClaimTypes.Name, _tenantContext.OrganizationName ?? "api-key-user"),
                new("tenant_id", _tenantContext.TenantId),
                new("client_id", $"apikey:{_tenantContext.TenantId}"),
                new("scope", "patient/*.read"),
                new("scope", "user/*.read"),
                new("scope", "system/*.read"),
                new("role", "admin")
            };
            var identity = new ClaimsIdentity(claims, "ApiKey");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "ApiKey");
            return System.Threading.Tasks.Task.FromResult(AuthenticateResult.Success(ticket));
        }

        return System.Threading.Tasks.Task.FromResult(AuthenticateResult.NoResult());
    }
}
