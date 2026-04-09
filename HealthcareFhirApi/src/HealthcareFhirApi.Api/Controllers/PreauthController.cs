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
    public async System.Threading.Tasks.Task<IActionResult> Submit(
        [FromBody] Bundle bundle, CancellationToken ct)
    {
        // Extract the Claim with use="preauthorization" from the bundle
        var claim = bundle.Entry?
            .Select(e => e.Resource)
            .OfType<Claim>()
            .FirstOrDefault(c => c.UseElement?.Value.ToString()?.ToLower() == "preauthorization");

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
