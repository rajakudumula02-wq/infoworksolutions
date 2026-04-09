using Hl7.Fhir.Model;

namespace HealthcareFhirApi.Core.Exceptions;

public class FhirValidationException(OperationOutcome outcome)
    : Exception("FHIR profile validation failed")
{
    public OperationOutcome Outcome { get; } = outcome;
}
