namespace HealthcareFhirApi.Core.Exceptions;

public class ScopeViolationException(string requiredScope)
    : Exception($"Required scope '{requiredScope}' not granted");
