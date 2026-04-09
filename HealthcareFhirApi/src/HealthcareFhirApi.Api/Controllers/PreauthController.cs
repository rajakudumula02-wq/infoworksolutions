// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("Claim")]
[Authorize]
public class PreauthController : FhirControllerBase
{
    private const string ClaimProfileUrl = "http://hl7.org/fhir/StructureDefinition/Claim";

    private readonly IFhirResourceRepository<Claim> _claimRepo;
    private readonly IFhirResourceRepository<ClaimResponse> _claimResponseRepo;
    private readonly IFhirValidationService _validator;
    private readonly IAuditService _audit;
    private readonly PasClaimParserService _parser;

    public PreauthController(
        IFhirResourceRepository<Claim> claimRepo,
        IFhirResourceRepository<ClaimResponse> claimResponseRepo,
        IFhirValidationService validator,
        IAuditService audit,
        PasClaimParserService parser)
    {
        _claimRepo         = claimRepo;
        _claimResponseRepo = claimResponseRepo;
        _validator         = validator;
        _audit             = audit;
        _parser            = parser;
    }

    [HttpPost("$submit")]
    public async System.Threading.Tasks.Task<IActionResult> Submit(CancellationToken ct)
    {
        // Read raw body as JsonDocument (same approach as CRD controller)
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        System.Text.Json.JsonElement root;
        try { root = System.Text.Json.JsonDocument.Parse(body).RootElement; }
        catch { return BadRequest(new { error = "Invalid JSON payload" }); }

        // Verify it's a Bundle
        var resourceType = root.TryGetProperty("resourceType", out var rt) ? rt.GetString() : null;
        if (resourceType != "Bundle")
            return BadRequest(new { error = "Expected a FHIR Bundle resource" });

        // Find the Claim entry with use="preauthorization"
        System.Text.Json.JsonElement? claimElement = null;
        if (root.TryGetProperty("entry", out var entries))
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.TryGetProperty("resource", out var res) &&
                    res.TryGetProperty("resourceType", out var resType) && resType.GetString() == "Claim" &&
                    res.TryGetProperty("use", out var use) && use.GetString()?.ToLower() == "preauthorization")
                {
                    claimElement = res;
                    break;
                }
            }
        }

        // Also try top-level (if the Bundle IS the Claim — shouldn't happen but handle gracefully)
        if (claimElement == null && resourceType == "Claim" &&
            root.TryGetProperty("use", out var topUse) && topUse.GetString()?.ToLower() == "preauthorization")
        {
            claimElement = root;
        }

        // Parse the full body into FHIR models for downstream processing
        Bundle bundle;
        Claim? claim = null;
        try
        {
            #pragma warning disable CS0618 // Suppress obsolete warning for FhirJsonParser
            var fhirParser = new Hl7.Fhir.Serialization.FhirJsonParser(
                new Hl7.Fhir.Serialization.ParserSettings { PermissiveParsing = true });
            bundle = fhirParser.Parse<Bundle>(body);
            #pragma warning restore CS0618

            claim = bundle.Entry?
                .Select(e => e.Resource)
                .OfType<Claim>()
                .FirstOrDefault(c => c.UseElement?.Value.ToString()?.ToLower() == "preauthorization");
        }
        catch
        {
            // If FHIR parsing fails, return error with details from JsonDocument validation
            if (claimElement == null)
                return BadRequest(new { error = "Bundle must contain a Claim with use='preauthorization'" });
            return BadRequest(new { error = "Failed to parse FHIR Bundle" });
        }

        if (claim is null)
        {
            return FhirValidationError(new OperationOutcome
            {
                Issue = new List<OperationOutcome.IssueComponent>
                {
                    new()
                    {
                        Severity    = OperationOutcome.IssueSeverity.Error,
                        Code        = OperationOutcome.IssueType.Required,
                        Diagnostics = "Bundle must contain a Claim with use='preauthorization'"
                    }
                }
            });
        }

        // Validate the Claim against the profile
        var outcome = await _validator.ValidateAsync(claim, ClaimProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        // Persist the claim
        var createdClaim = await _claimRepo.CreateAsync(claim, ct);

        // Create a ClaimResponse with outcome="queued" per PAS IG
        var preAuthRef = $"PA-{DateTime.UtcNow:yyyy-MM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
        var claimResponse = new ClaimResponse
        {
            Status  = FinancialResourceStatusCodes.Active,
            Type    = createdClaim.Type,
            UseElement = new Code<ClaimUseCode>(ClaimUseCode.Preauthorization),
            Patient = createdClaim.Patient,
            Created = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Insurer = createdClaim.Insurer,
            Request = new ResourceReference($"Claim/{createdClaim.Id}"),
            Outcome = ClaimProcessingCodes.Queued,
            Disposition = "Prior authorization request received and queued for review.",
            PreAuthRef = preAuthRef,
            PreAuthPeriod = new Period
            {
                Start = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                End = DateTime.UtcNow.AddMonths(6).ToString("yyyy-MM-dd")
            }
        };

        // Add per-item adjudication with review action (pended)
        if (createdClaim.Item != null)
        {
            claimResponse.Item = createdClaim.Item.Select((item, idx) => new ClaimResponse.ItemComponent
            {
                ItemSequence = item.Sequence,
                Adjudication = new List<ClaimResponse.AdjudicationComponent>
                {
                    new()
                    {
                        Category = new CodeableConcept(
                            "http://hl7.org/fhir/us/davinci-pas/CodeSystem/PASSupportingInfoType",
                            "pended", "Pended")
                    }
                }
            }).ToList();
        }

        var createdResponse = await _claimResponseRepo.CreateAsync(claimResponse, ct);

        // Extract key fields from the claim
        var extracted = _parser.Extract(createdClaim, bundle);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: createdClaim.Patient?.Reference,
            ResourceType: "Claim",
            ResourceId: createdClaim.Id,
            Action: "create",
            Timestamp: DateTimeOffset.UtcNow), ct);

        // Return ClaimResponse with extracted data
        return Ok(new
        {
            claimResponse = createdResponse,
            extracted = new
            {
                extracted.MemberId,
                extracted.MemberName,
                extracted.MemberCoverageId,
                extracted.CoverageType,
                extracted.CoveragePayer,
                extracted.SubscriberId,
                extracted.ProviderId,
                extracted.ProviderName,
                extracted.OfficeId,
                extracted.InsurerId,
                preAuthRef,
                outcome = "queued",
                procedures = extracted.Procedures.Select(p => new
                {
                    p.Sequence,
                    p.ProcedureCode,
                    p.ProcedureSystem,
                    p.ProcedureDisplay,
                    p.DiagnosisCode,
                    p.DiagnosisDisplay,
                    p.ServicedDate,
                    p.PlaceOfService,
                    p.ToothNumbers
                })
            }
        });
    }

    [HttpPost("$inquire")]
    public async System.Threading.Tasks.Task<IActionResult> Inquire(
        [FromBody] Parameters parameters, CancellationToken ct)
    {
        var claimIdParam = parameters.GetSingle("claim-id");
        var claimId      = claimIdParam?.Value?.ToString();

        if (string.IsNullOrWhiteSpace(claimId))
        {
            return FhirValidationError(new OperationOutcome
            {
                Issue = new List<OperationOutcome.IssueComponent>
                {
                    new()
                    {
                        Severity    = OperationOutcome.IssueSeverity.Error,
                        Code        = OperationOutcome.IssueType.Required,
                        Diagnostics = "claim-id parameter is required"
                    }
                }
            });
        }

        // Look up ClaimResponse by the claim reference
        var searchParams = new SearchParameters(
            Filters: new Dictionary<string, string?> { ["request"] = $"Claim/{claimId}" },
            Skip: 0,
            Take: 1,
            SortField: null,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result = await _claimResponseRepo.SearchAsync(searchParams, ct);

        if (result.TotalCount == 0)
            return FhirNotFound("ClaimResponse", claimId);

        return FhirOk(result.Items[0]);
    }
}
