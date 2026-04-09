using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace HealthcareFhirApi.Api.Formatters;

public class FhirJsonOutputFormatter : TextOutputFormatter
{
    private readonly FhirJsonSerializer _serializer = new();

    public FhirJsonOutputFormatter()
    {
        SupportedMediaTypes.Add("application/fhir+json");
        SupportedEncodings.Add(Encoding.UTF8);
    }

    protected override bool CanWriteType(Type? type)
        => type is not null && typeof(Resource).IsAssignableFrom(type);

    public override async System.Threading.Tasks.Task WriteResponseBodyAsync(
        OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        if (context.Object is Resource resource)
        {
            var json = _serializer.SerializeToString(resource);
            await context.HttpContext.Response.WriteAsync(json, selectedEncoding);
        }
    }
}
