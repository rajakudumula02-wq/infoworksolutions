// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Exceptions;

public class TenantDeactivatedException(string tenantId)
    : Exception($"Tenant '{tenantId}' has been deactivated.");
