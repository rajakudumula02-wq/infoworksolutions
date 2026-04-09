using System.Security.Claims;

namespace HealthcareFhirApi.Api.Middleware;

public class AuditLoggingMiddleware(RequestDelegate next, IAuditService auditService)
{
    private static readonly HashSet<string> AuditedResources = new(StringComparer.OrdinalIgnoreCase)
    {
        "Patient", "ExplanationOfBenefit", "Claim", "ClaimResponse"
    };

    public async SystemTask InvokeAsync(HttpContext context)
    {
        await next(context);

        // Fire audit asynchronously after response completes — don't block
        _ = FireAuditAsync(context);
    }

    private async SystemTask FireAuditAsync(HttpContext context)
    {
        try
        {
            var path     = context.Request.Path.Value ?? string.Empty;
            var segments = path.Trim('/').Split('/');

            if (segments.Length == 0) return;

            var resourceType = segments[0];
            if (!AuditedResources.Contains(resourceType)) return;

            var resourceId = segments.Length >= 2 ? segments[1] : null;
            // Ignore operation segments like $everything
            if (resourceId?.StartsWith('$') == true) resourceId = null;

            var clientId = context.User.FindFirst("sub")?.Value
                        ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? "anonymous";

            var patientId = context.User.FindFirst("patient")?.Value;

            var action = MapAction(context.Request.Method);

            var auditContext = new AuditContext(
                ClientId:     clientId,
                PatientId:    patientId,
                ResourceType: resourceType,
                ResourceId:   resourceId,
                Action:       action,
                Timestamp:    DateTimeOffset.UtcNow);

            await auditService.RecordAsync(auditContext);
        }
        catch
        {
            // Audit failures must never affect the response
        }
    }

    private static string MapAction(string httpMethod) => httpMethod.ToUpperInvariant() switch
    {
        "GET"    => "read",
        "POST"   => "create",
        "PUT"    => "update",
        "PATCH"  => "update",
        _        => "read"
    };
}
