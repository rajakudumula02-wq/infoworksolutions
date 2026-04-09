using Hl7.Fhir.Model;
using HealthcareFhirApi.Core.Models;

namespace HealthcareFhirApi.Core.Interfaces;

public interface IFhirResourceRepository<TResource> where TResource : Resource
{
    Task<TResource?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<PagedResult<TResource>> SearchAsync(SearchParameters parameters, CancellationToken ct = default);
    Task<TResource> CreateAsync(TResource resource, CancellationToken ct = default);
    Task<TResource> UpdateAsync(string id, TResource resource, CancellationToken ct = default);
}
