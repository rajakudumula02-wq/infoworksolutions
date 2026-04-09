namespace HealthcareFhirApi.Api.Middleware;

public class FhirExceptionMiddleware(RequestDelegate next)
{
    public async SystemTask InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ResourceNotFoundException ex)
        {
            await WriteOperationOutcome(context, 404, OperationOutcome.IssueType.NotFound, ex.Message);
        }
        catch (FhirValidationException ex)
        {
            context.Response.StatusCode  = 422;
            context.Response.ContentType = "application/fhir+json";
            await context.Response.WriteAsync(new FhirJsonSerializer().SerializeToString(ex.Outcome));
        }
        catch (ScopeViolationException ex)
        {
            await WriteOperationOutcome(context, 403, OperationOutcome.IssueType.Forbidden, ex.Message);
        }
        catch (UnsupportedMediaTypeException ex)
        {
            await WriteOperationOutcome(context, 415, OperationOutcome.IssueType.NotSupported, ex.Message);
        }
        catch (UnsupportedCodeSystemException ex)
        {
            await WriteOperationOutcome(context, 400, OperationOutcome.IssueType.NotSupported, ex.Message);
        }
        catch (ResourceTypeMismatchException ex)
        {
            await WriteOperationOutcome(context, 400, OperationOutcome.IssueType.Invalid, ex.Message);
        }
        catch (UnsupportedResourceTypeException ex)
        {
            await WriteOperationOutcome(context, 400, OperationOutcome.IssueType.NotSupported, ex.Message);
        }
        catch (ConsentRequiredException ex)
        {
            await WriteOperationOutcome(context, 403, OperationOutcome.IssueType.Forbidden, ex.Message);
        }
        catch (TenantResolutionException ex)
        {
            await WriteOperationOutcome(context, 401, OperationOutcome.IssueType.Login, ex.Message);
        }
        catch (TenantDeactivatedException ex)
        {
            await WriteOperationOutcome(context, 403, OperationOutcome.IssueType.Forbidden, ex.Message);
        }
        catch (RateLimitExceededException ex)
        {
            context.Response.Headers["Retry-After"] = "1";
            await WriteOperationOutcome(context, 429, OperationOutcome.IssueType.Throttled, ex.Message);
        }
        catch (Exception ex)
        {
            // Temporarily show actual error for debugging
            await WriteOperationOutcome(context, 500, OperationOutcome.IssueType.Exception,
                $"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static async SystemTask WriteOperationOutcome(
        HttpContext context, int status, OperationOutcome.IssueType issueType, string diagnostics)
    {
        var outcome = new OperationOutcome
        {
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity    = OperationOutcome.IssueSeverity.Error,
                    Code        = issueType,
                    Diagnostics = diagnostics
                }
            ]
        };
        context.Response.StatusCode  = status;
        context.Response.ContentType = "application/fhir+json";
        await context.Response.WriteAsync(new FhirJsonSerializer().SerializeToString(outcome));
    }
}
