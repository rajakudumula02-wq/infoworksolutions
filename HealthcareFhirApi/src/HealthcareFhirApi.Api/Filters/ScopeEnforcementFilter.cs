using Microsoft.AspNetCore.Mvc.Filters;

namespace HealthcareFhirApi.Api.Filters;

/// <summary>
/// Action filter that enforces a required SMART on FHIR scope claim.
/// Apply via [ServiceFilter(typeof(ScopeEnforcementFilter))] or a derived attribute.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireScopeAttribute(string requiredScope) : Attribute, IAsyncActionFilter
{
    public async System.Threading.Tasks.Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;

        if (!user.Identity?.IsAuthenticated ?? true)
            throw new ScopeViolationException(requiredScope);

        // SMART scopes may be space-separated in a single claim or multiple claims
        var scopeClaims = user.Claims
            .Where(c => c.Type == "scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet();

        if (!scopeClaims.Contains(requiredScope) && !scopeClaims.Contains("system/*.read"))
            throw new ScopeViolationException(requiredScope);

        await next();
    }
}
