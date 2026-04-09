// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Exceptions;

public class UnsupportedResourceTypeException(string resourceType)
    : Exception($"Unsupported resource type for bulk export: {resourceType}");
