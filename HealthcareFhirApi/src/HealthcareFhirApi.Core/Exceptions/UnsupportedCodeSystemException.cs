namespace HealthcareFhirApi.Core.Exceptions;

public class UnsupportedCodeSystemException(string system)
    : Exception($"Unsupported code system: {system}");
