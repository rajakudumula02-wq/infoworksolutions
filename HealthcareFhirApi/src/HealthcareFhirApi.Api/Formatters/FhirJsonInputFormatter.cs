using System.Text;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace HealthcareFhirApi.Api.Formatters;

public class FhirJsonInputFormatter : TextInputFormatter
{
    public FhirJsonInputFormatter()
    {
        SupportedMediaTypes.Add("application/fhir+json");
        SupportedMediaTypes.Add("application/json");
        SupportedEncodings.Add(Encoding.UTF8);
    }

    protected override bool CanReadType(Type type)
        => typeof(Resource).IsAssignableFrom(type);

    public override async System.Threading.Tasks.Task<InputFormatterResult> ReadRequestBodyAsync(
        InputFormatterContext context, Encoding encoding)
    {
        using var reader = new StreamReader(context.HttpContext.Request.Body, encoding);
        var body = await reader.ReadToEndAsync();
        var node = FhirJsonNode.Parse(body);
        var resource = node.ToPoco<Resource>();
        return InputFormatterResult.Success(resource);
    }
}
