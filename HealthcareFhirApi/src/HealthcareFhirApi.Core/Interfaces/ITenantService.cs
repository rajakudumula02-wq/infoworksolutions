// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Interfaces;

public interface ITenantService
{
    System.Threading.Tasks.Task<TenantContext?> ResolveFromApiKeyAsync(string apiKey, CancellationToken ct = default);
    System.Threading.Tasks.Task<TenantContext?> ResolveFromSubdomainAsync(string subdomain, CancellationToken ct = default);
    System.Threading.Tasks.Task<TenantContext?> GetByIdAsync(string tenantId, CancellationToken ct = default);
}
