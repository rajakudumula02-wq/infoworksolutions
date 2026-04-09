using Hl7.Fhir.Model;
using HealthcareFhirApi.Core.Models;

namespace HealthcareFhirApi.Core.Interfaces;

public interface IPaginationService
{
    Bundle BuildSearchBundle(IEnumerable<Resource> resources, SearchParameters parameters, int totalCount, string baseUrl);
    (int skip, int take) ResolvePage(int? count, string? pageToken);
}
