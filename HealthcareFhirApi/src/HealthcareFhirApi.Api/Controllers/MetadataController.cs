// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class MetadataController : ControllerBase
{
    private readonly ICapabilityStatementBuilder _builder;

    public MetadataController(ICapabilityStatementBuilder builder)
    {
        _builder = builder;
    }

    [HttpGet("metadata")]
    public IActionResult GetCapabilityStatement()
    {
        var statement = _builder.Build();
        return Ok(statement);
    }
}
