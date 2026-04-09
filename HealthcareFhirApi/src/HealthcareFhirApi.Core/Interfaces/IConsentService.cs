// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Interfaces;

public interface IConsentService
{
    System.Threading.Tasks.Task<bool> HasActiveConsentAsync(string patientId, CancellationToken ct = default);

    SystemTask ValidateConsentRequiredFieldsAsync(Consent consent, CancellationToken ct = default);
}
