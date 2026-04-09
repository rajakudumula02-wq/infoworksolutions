// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Exceptions;

public class ConsentRequiredException(string patientId)
    : Exception($"No active consent found for patient '{patientId}'");
