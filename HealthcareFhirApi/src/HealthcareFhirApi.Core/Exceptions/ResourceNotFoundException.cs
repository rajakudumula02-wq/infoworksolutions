namespace HealthcareFhirApi.Core.Exceptions;

public class ResourceNotFoundException(string resourceType, string id)
    : Exception($"{resourceType}/{id} not found");
