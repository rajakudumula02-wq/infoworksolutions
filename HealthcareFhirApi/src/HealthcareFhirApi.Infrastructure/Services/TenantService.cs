// Feature: healthcare-fhir-api
using System.Security.Cryptography;
using System.Text;
using HealthcareFhirApi.Infrastructure.Data;
using HealthcareFhirApi.Infrastructure.Entities;

namespace HealthcareFhirApi.Infrastructure.Services;

public class TenantService : ITenantService
{
    private readonly TenantDbContext _db;

    public TenantService(TenantDbContext db) => _db = db;

    public async System.Threading.Tasks.Task<TenantContext?> ResolveFromApiKeyAsync(
        string apiKey, CancellationToken ct = default)
    {
        var hash = HashKey(apiKey);
        var key = await _db.ApiKeys
            .Include(k => k.Tenant)
            .FirstOrDefaultAsync(k => k.KeyHash == hash && !k.IsRevoked, ct);

        return key is null ? null : MapToContext(key.Tenant);
    }

    public async System.Threading.Tasks.Task<TenantContext?> ResolveFromSubdomainAsync(
        string subdomain, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants
            .FirstOrDefaultAsync(t => t.OrganizationName.ToLower() == subdomain.ToLower(), ct);

        return tenant is null ? null : MapToContext(tenant);
    }

    public async System.Threading.Tasks.Task<TenantContext?> GetByIdAsync(
        string tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct);
        return tenant is null ? null : MapToContext(tenant);
    }

    // ── Admin operations (used by AdminController) ──

    public async System.Threading.Tasks.Task<TenantEntity> ProvisionAsync(
        string organizationName, string contactEmail, string planTier, CancellationToken ct = default)
    {
        var entity = new TenantEntity
        {
            OrganizationName = organizationName,
            ContactEmail = contactEmail,
            PlanTier = planTier
        };
        _db.Tenants.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async SystemTask DeactivateAsync(string tenantId, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct)
            ?? throw new ResourceNotFoundException("Tenant", tenantId);
        tenant.IsActive = false;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task<TenantEntity> UpdateAsync(
        string tenantId, string? contactEmail, string? planTier, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct)
            ?? throw new ResourceNotFoundException("Tenant", tenantId);
        if (contactEmail is not null) tenant.ContactEmail = contactEmail;
        if (planTier is not null) tenant.PlanTier = planTier;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return tenant;
    }

    public async SystemTask SetRateLimitAsync(
        string tenantId, int requestsPerSecond, int burstSize, CancellationToken ct = default)
    {
        var tenant = await _db.Tenants.FindAsync(new object[] { tenantId }, ct)
            ?? throw new ResourceNotFoundException("Tenant", tenantId);
        tenant.RateLimitRequestsPerSecond = requestsPerSecond;
        tenant.RateLimitBurstSize = burstSize;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task<(string KeyId, string PlaintextKey)> CreateApiKeyAsync(
        string tenantId, CancellationToken ct = default)
    {
        _ = await _db.Tenants.FindAsync(new object[] { tenantId }, ct)
            ?? throw new ResourceNotFoundException("Tenant", tenantId);

        var plaintext = $"fhir_{Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLower()}";
        var entity = new ApiKeyEntity
        {
            TenantId = tenantId,
            KeyHash = HashKey(plaintext),
            KeyPrefix = plaintext[..8]
        };
        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);
        return (entity.Id, plaintext);
    }

    public async SystemTask RevokeApiKeyAsync(
        string tenantId, string keyId, CancellationToken ct = default)
    {
        var key = await _db.ApiKeys
            .FirstOrDefaultAsync(k => k.Id == keyId && k.TenantId == tenantId, ct)
            ?? throw new ResourceNotFoundException("ApiKey", keyId);
        key.IsRevoked = true;
        key.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    // ── Helpers ──

    private static TenantContext MapToContext(TenantEntity t) => new()
    {
        TenantId = t.Id,
        OrganizationName = t.OrganizationName,
        IsActive = t.IsActive,
        SmartAuthority = t.SmartAuthority,
        DatabaseConnectionString = t.DatabaseConnectionString,
        RateLimitRequestsPerSecond = t.RateLimitRequestsPerSecond,
        RateLimitBurstSize = t.RateLimitBurstSize
    };

    private static string HashKey(string key)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLower();
}
