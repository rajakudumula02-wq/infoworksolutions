namespace HealthcareFhirApi.Api.Middleware;

public class FhirContentNegotiationMiddleware(RequestDelegate next)
{
    private const string FhirJson = "application/fhir+json";
    private const string FhirXml  = "application/fhir+xml";

    public async SystemTask InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip content negotiation for Swagger and non-FHIR routes
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var accept = context.Request.Headers.Accept.ToString();

        // Determine the desired response format from the Accept header
        if (string.IsNullOrEmpty(accept) || accept == "*/*" || accept.Contains(FhirJson))
        {
            context.Response.ContentType = FhirJson;
        }
        else if (accept.Contains(FhirXml))
        {
            context.Response.ContentType = FhirXml;
        }
        else
        {
            // Unsupported Accept header on any request
            throw new UnsupportedMediaTypeException(accept);
        }

        await next(context);
    }
}
