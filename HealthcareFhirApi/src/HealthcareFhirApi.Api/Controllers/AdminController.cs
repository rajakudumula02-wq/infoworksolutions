// Feature: healthcare-fhir-api
using HealthcareFhirApi.Infrastructure.Services;

namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Policy = "admin")]
public class AdminController : ControllerBase
{
    private readonly TenantService _tenantService;
    private readonly IMetricsService _metricsService;

    public AdminController(ITenantService tenantService, IMetricsService metricsService)
    {
        _tenantService = (TenantService)tenantService;
        _metricsService = metricsService;
    }

    // POST /admin/tenants
    [HttpPost("tenants")]
    public async System.Threading.Tasks.Task<IActionResult> CreateTenant(
        [FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _tenantService.ProvisionAsync(
            request.OrganizationName, request.ContactEmail, request.PlanTier ?? "standard", ct);
        return Created($"/admin/tenants/{tenant.Id}", tenant);
    }

    // PUT /admin/tenants/{id}
    [HttpPut("tenants/{id}")]
    public async System.Threading.Tasks.Task<IActionResult> UpdateTenant(
        string id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _tenantService.UpdateAsync(id, request.ContactEmail, request.PlanTier, ct);
        return Ok(tenant);
    }

    // PATCH /admin/tenants/{id}/deactivate
    [HttpPatch("tenants/{id}/deactivate")]
    public async System.Threading.Tasks.Task<IActionResult> DeactivateTenant(string id, CancellationToken ct)
    {
        await _tenantService.DeactivateAsync(id, ct);
        return NoContent();
    }

    // GET /admin/tenants/{id}/metrics
    [HttpGet("tenants/{id}/metrics")]
    public async System.Threading.Tasks.Task<IActionResult> GetMetrics(
        string id, [FromQuery] DateTimeOffset? start, [FromQuery] DateTimeOffset? end, CancellationToken ct)
    {
        var s = start ?? DateTimeOffset.UtcNow.AddDays(-30);
        var e = end ?? DateTimeOffset.UtcNow;
        var metrics = await _metricsService.GetMetricsAsync(id, s, e, ct);
        return Ok(metrics);
    }

    // PUT /admin/tenants/{id}/rate-limit
    [HttpPut("tenants/{id}/rate-limit")]
    public async System.Threading.Tasks.Task<IActionResult> SetRateLimit(
        string id, [FromBody] RateLimitRequest request, CancellationToken ct)
    {
        await _tenantService.SetRateLimitAsync(id, request.RequestsPerSecond, request.BurstSize, ct);
        return NoContent();
    }

    // POST /admin/tenants/{id}/api-keys
    [HttpPost("tenants/{id}/api-keys")]
    public async System.Threading.Tasks.Task<IActionResult> CreateApiKey(string id, CancellationToken ct)
    {
        var (keyId, plaintext) = await _tenantService.CreateApiKeyAsync(id, ct);
        return Created($"/admin/tenants/{id}/api-keys/{keyId}", new { keyId, apiKey = plaintext });
    }

    // DELETE /admin/tenants/{id}/api-keys/{keyId}
    [HttpDelete("tenants/{id}/api-keys/{keyId}")]
    public async System.Threading.Tasks.Task<IActionResult> RevokeApiKey(
        string id, string keyId, CancellationToken ct)
    {
        await _tenantService.RevokeApiKeyAsync(id, keyId, ct);
        return NoContent();
    }
}

public record CreateTenantRequest(string OrganizationName, string ContactEmail, string? PlanTier);
public record UpdateTenantRequest(string? ContactEmail, string? PlanTier);
public record RateLimitRequest(int RequestsPerSecond, int BurstSize);
