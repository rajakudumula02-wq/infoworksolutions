// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

public abstract class FhirControllerBase : ControllerBase
{
    protected IActionResult FhirNotFound(string resourceType, string id)
    {
        var outcome = new OperationOutcome
        {
            Issue = new List<OperationOutcome.IssueComponent>
            {
                new()
                {
                    Severity    = OperationOutcome.IssueSeverity.Error,
                    Code        = OperationOutcome.IssueType.NotFound,
                    Diagnostics = $"{resourceType}/{id} not found"
                }
            }
        };
        return StatusCode(404, outcome);
    }

    protected IActionResult FhirValidationError(OperationOutcome outcome)
        => StatusCode(422, outcome);

    protected IActionResult FhirCreated(Resource resource, string locationUrl)
    {
        Response.Headers.Location = locationUrl;
        return StatusCode(201, resource);
    }

    protected IActionResult FhirForbidden(string message)
    {
        var outcome = new OperationOutcome
        {
            Issue = new List<OperationOutcome.IssueComponent>
            {
                new()
                {
                    Severity    = OperationOutcome.IssueSeverity.Error,
                    Code        = OperationOutcome.IssueType.Forbidden,
                    Diagnostics = message
                }
            }
        };
        return StatusCode(403, outcome);
    }

    protected IActionResult FhirOk(Resource resource)
        => Ok(resource);

    protected IActionResult FhirAccepted(Resource resource)
        => StatusCode(202, resource);
}
