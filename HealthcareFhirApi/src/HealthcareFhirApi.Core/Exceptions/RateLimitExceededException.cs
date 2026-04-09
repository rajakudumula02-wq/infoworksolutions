// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Core.Exceptions;

public class RateLimitExceededException()
    : Exception("Rate limit exceeded. Please retry after a short delay.");
