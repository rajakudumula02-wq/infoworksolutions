using Hl7.Fhir.Model;

namespace HealthcareFhirApi.Core.Interfaces;

public interface IFhirValidationService
{
    Task<OperationOutcome> ValidateAsync(Resource resource, string profileUrl, CancellationToken ct = default);
    bool IsValid(OperationOutcome outcome);
}
