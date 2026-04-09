using Hl7.Fhir.Model;

namespace HealthcareFhirApi.Core.Interfaces;

public interface ICapabilityStatementBuilder
{
    CapabilityStatement Build();
}
