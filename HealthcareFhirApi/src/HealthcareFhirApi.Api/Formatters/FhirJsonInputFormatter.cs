using System.Text;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace HealthcareFhirApi.Api.Formatters;

public class FhirJsonInputFormatter : TextInputFormatter
{
    private readonly FhirJsonParser _parser = new();

    public FhirJsonInputFormatter()
    {
        SupportedMediaTypes.Add("application/fhir+json");
        SupportedEncodings.Add(Encoding.UTF8);
    }

    protected override bool CanReadType(Type type)
        => typeof(Resource).IsAssignableFrom(type);

    public override async System.Threading.Tasks.Task<InputFormatterResult> ReadRequestBodyAsync(
        InputFormatterContext context, Encoding encoding)
    {
        using var reader = new StreamReader(context.HttpContext.Request.Body, encoding);
        var body = await reader.ReadToEndAsync();
        var resource = _parser.Parse<Resource>(body);
        return InputFormatterResult.Success(resource);
    }
}
