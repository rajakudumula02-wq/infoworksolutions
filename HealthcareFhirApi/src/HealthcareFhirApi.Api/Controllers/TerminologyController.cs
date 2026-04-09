// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Authorize]
public class TerminologyController : FhirControllerBase
{
    private readonly ITerminologyService _terminology;

    public TerminologyController(ITerminologyService terminology)
    {
        _terminology = terminology;
    }

    [HttpGet("CodeSystem/$lookup")]
    public async System.Threading.Tasks.Task<IActionResult> Lookup(
        [FromQuery] string system,
        [FromQuery] string code,
        [FromQuery] string? version,
        CancellationToken ct)
    {
        try
        {
            var result = await _terminology.LookupAsync(system, code, version, ct);
            return FhirOk(result);
        }
        catch (UnsupportedCodeSystemException ex)
        {
            return StatusCode(400, BuildUnsupportedSystemOutcome(ex.Message));
        }
    }

    [HttpGet("ValueSet/$validate-code")]
    public async System.Threading.Tasks.Task<IActionResult> ValidateCode(
        [FromQuery] string url,
        [FromQuery] string system,
        [FromQuery] string code,
        [FromQuery] string? display,
        CancellationToken ct)
    {
        try
        {
            var result = await _terminology.ValidateCodeAsync(url, system, code, display, ct);
            return FhirOk(result);
        }
        catch (UnsupportedCodeSystemException ex)
        {
            return StatusCode(400, BuildUnsupportedSystemOutcome(ex.Message));
        }
    }

    [HttpGet("ValueSet/$expand")]
    public async System.Threading.Tasks.Task<IActionResult> Expand(
        [FromQuery] string url,
        [FromQuery] string? filter,
        [FromQuery] int? count,
        CancellationToken ct)
    {
        var result = await _terminology.ExpandAsync(url, filter, count, ct);
        return FhirOk(result);
    }

    [HttpGet("ConceptMap/$translate")]
    public async System.Threading.Tasks.Task<IActionResult> Translate(
        [FromQuery] string url,
        [FromQuery] string system,
        [FromQuery] string code,
        [FromQuery] string targetsystem,
        CancellationToken ct)
    {
        try
        {
            var result = await _terminology.TranslateAsync(url, system, code, targetsystem, ct);
            return FhirOk(result);
        }
        catch (UnsupportedCodeSystemException ex)
        {
            return StatusCode(400, BuildUnsupportedSystemOutcome(ex.Message));
        }
    }

    private static OperationOutcome BuildUnsupportedSystemOutcome(string message) => new()
    {
        Issue = new List<OperationOutcome.IssueComponent>
        {
            new()
            {
                Severity    = OperationOutcome.IssueSeverity.Error,
                Code        = OperationOutcome.IssueType.NotSupported,
                Diagnostics = message
            }
        }
    };
}
