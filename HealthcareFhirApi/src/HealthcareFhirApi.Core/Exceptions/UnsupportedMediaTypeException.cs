namespace HealthcareFhirApi.Core.Exceptions;

public class UnsupportedMediaTypeException(string contentType)
    : Exception($"Unsupported media type: {contentType}");
