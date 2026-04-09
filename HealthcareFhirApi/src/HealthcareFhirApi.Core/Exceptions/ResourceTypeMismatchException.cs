namespace HealthcareFhirApi.Core.Exceptions;

public class ResourceTypeMismatchException(string expected, string actual)
    : Exception($"Expected resourceType '{expected}', got '{actual}'");
