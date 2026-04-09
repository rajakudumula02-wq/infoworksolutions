// Feature: healthcare-fhir-api
using HealthcareFhirApi.Core.Exceptions;
using HealthcareFhirApi.Core.Interfaces;
using HealthcareFhirApi.Core.Models;

namespace HealthcareFhirApi.Api.Middleware;

public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async System.Threading.Tasks.Task InvokeAsync(HttpContext context, ITenantService tenantService, TenantContext tenantContext)
    {
        var path = context.Request.Path;

        // Skip tenant resolution for public/anonymous endpoints
        if (path.StartsWithSegments("/swagger")
            || path.StartsWithSegments("/metadata")
            || path.StartsWithSegments("/.well-known")
            || path.Value == "/")
        {
            await next(context);
            return;
        }

        TenantContext? resolved = null;

        // 1. Try X-Api-Key header
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader))
        {
            var apiKey = apiKeyHeader.ToString();
            if (!string.IsNullOrWhiteSpace(apiKey))
                resolved = await tenantService.ResolveFromApiKeyAsync(apiKey, context.RequestAborted);
        }

        // 2. Try JWT tenant_id claim
        if (resolved is null && context.User.Identity?.IsAuthenticated == true)
        {
            var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrWhiteSpace(tenantClaim))
                resolved = await tenantService.GetByIdAsync(tenantClaim, context.RequestAborted);
        }

        // 3. Try subdomain
        if (resolved is null)
        {
            var host = context.Request.Host.Host;
            var parts = host.Split('.');
            if (parts.Length > 2)
                resolved = await tenantService.ResolveFromSubdomainAsync(parts[0], context.RequestAborted);
        }

        if (resolved is null)
            throw new TenantResolutionException();

        if (!resolved.IsActive)
            throw new TenantDeactivatedException(resolved.TenantId);

        // Populate scoped TenantContext
        tenantContext.TenantId = resolved.TenantId;
        tenantContext.OrganizationName = resolved.OrganizationName;
        tenantContext.IsActive = resolved.IsActive;
        tenantContext.SmartAuthority = resolved.SmartAuthority;
        tenantContext.DatabaseConnectionString = resolved.DatabaseConnectionString;
        tenantContext.RateLimitRequestsPerSecond = resolved.RateLimitRequestsPerSecond;
        tenantContext.RateLimitBurstSize = resolved.RateLimitBurstSize;

        await next(context);
    }
}
