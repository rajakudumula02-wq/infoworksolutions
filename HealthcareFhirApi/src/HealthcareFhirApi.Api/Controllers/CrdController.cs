// Feature: healthcare-fhir-api — Da Vinci CRD (Coverage Requirements Discovery)
using System.Text.Json;
using HealthcareFhirApi.Core.Models;
using HealthcareFhirApi.Infrastructure.Services;

namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("cds-services")]
public class CrdController : ControllerBase
{
    private readonly CrdParserService _parser;

    public CrdController(CrdParserService parser) => _parser = parser;

    [HttpGet]
    public IActionResult Discovery() => Ok(new
    {
        services = new[]
        {
            new { hook = "order-sign", title = "CRD Order Sign", description = "Coverage Requirements Discovery", id = "crd-order-sign",
                prefetch = new { patient = "Patient/{{context.patientId}}", coverage = "Coverage?patient={{context.patientId}}&status=active", encounter = "Encounter/{{context.encounterId}}" } }
        }
    });

    [HttpPost("crd-order-sign")]
    public async Task<IActionResult> OrderSign(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        JsonElement root;
        try { root = JsonDocument.Parse(body).RootElement; }
        catch { return BadRequest(new { error = "Invalid JSON payload" }); }

        var data = _parser.Parse(root);

        // Build CDS Hooks cards — one per procedure
        var cards = data.Procedures.Select(proc =>
        {
            var needsAuth = NeedsPreauth(proc.ProcedureCode, proc.ProcedureSystem);
            return new
            {
                uuid = Guid.NewGuid().ToString(),
                summary = needsAuth
                    ? $"Prior authorization REQUIRED for {proc.ProcedureDisplay}"
                    : $"No prior authorization needed for {proc.ProcedureDisplay}",
                detail = BuildDetail(data, proc),
                indicator = needsAuth ? "warning" : "info",
                source = new { label = "Coverage Requirements Discovery", url = data.OfficeId },
                suggestions = needsAuth ? new[] { BuildPreauthSuggestion(data, proc) } : Array.Empty<object>(),
                links = needsAuth ? BuildLinks(proc) : Array.Empty<object>(),
            };
        }).ToList();

        return Ok(new
        {
            cards,
            extracted = new
            {
                data.MemberId,
                data.MemberCoverageId,
                data.ProviderId,
                data.OfficeId,
                procedures = data.Procedures.Select(p => new
                {
                    p.ServiceRequestId, p.ProcedureCode, p.ProcedureSystem,
                    p.ProcedureDisplay, p.BillingCodes, p.ToothNumbers,
                    requiresPreauth = NeedsPreauth(p.ProcedureCode, p.ProcedureSystem)
                })
            }
        });
    }

    private static string BuildDetail(CrdExtractedData data, CrdProcedure proc)
    {
        var lines = new List<string>
        {
            $"Member: {data.MemberId}, Coverage: {data.MemberCoverageId}",
            $"Provider: {data.ProviderId}, Office: {data.OfficeId}",
            $"Procedure: {proc.ProcedureCode} ({proc.ProcedureSystem}) — {proc.ProcedureDisplay}"
        };
        if (proc.BillingCodes.Count > 0) lines.Add($"Billing Options: {string.Join(", ", proc.BillingCodes)}");
        if (proc.ToothNumbers.Count > 0) lines.Add($"Tooth Numbers: {string.Join(", ", proc.ToothNumbers)}");
        return string.Join("\n", lines);
    }

    private static object BuildPreauthSuggestion(CrdExtractedData data, CrdProcedure proc) => new
    {
        label = $"Submit prior authorization for {proc.ProcedureDisplay}",
        uuid = Guid.NewGuid().ToString(),
        actions = new[]
        {
            new
            {
                type = "create",
                description = "Create prior authorization Claim",
                resource = new
                {
                    resourceType = "Claim",
                    status = "active",
                    type = new { coding = new[] { new { system = "http://terminology.hl7.org/CodeSystem/claim-type", code = "professional" } } },
                    use = "preauthorization",
                    patient = new { reference = $"Patient/{data.MemberId}" },
                    provider = new { reference = data.ProviderId },
                    insurance = new[] { new { sequence = 1, focal = true, coverage = new { reference = $"Coverage/{data.MemberCoverageId}" } } },
                    item = new[] { new { sequence = 1, productOrService = new { coding = new[] { new { system = proc.ProcedureSystem, code = proc.ProcedureCode, display = proc.ProcedureDisplay } } } } }
                }
            }
        }
    };

    private static object[] BuildLinks(CrdProcedure proc) => new object[]
    {
        new { label = "View prior auth requirements", url = $"http://example.org/prior-auth/{proc.ProcedureCode}", type = "absolute" }
    };

    private static bool NeedsPreauth(string code, string system)
    {
        if (system.Contains("HCPCS", StringComparison.OrdinalIgnoreCase) && code.StartsWith("E")) return true;
        if (code.StartsWith("770")) return true;
        return false;
    }
}
