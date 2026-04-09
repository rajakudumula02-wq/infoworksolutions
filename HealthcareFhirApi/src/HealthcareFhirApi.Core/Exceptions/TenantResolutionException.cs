// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Exceptions;

public class TenantResolutionException()
    : Exception("Unable to resolve tenant from the request.");
