using Hl7.Fhir.Model;

namespace HealthcareFhirApi.Core.Interfaces;

public interface ITerminologyService
{
    Task<Parameters> LookupAsync(string system, string code, string? version, CancellationToken ct = default);
    Task<Parameters> ValidateCodeAsync(string url, string system, string code, string? display, CancellationToken ct = default);
    Task<ValueSet> ExpandAsync(string url, string? filter, int? count, CancellationToken ct = default);
    Task<Parameters> TranslateAsync(string url, string system, string code, string targetSystem, CancellationToken ct = default);
}
