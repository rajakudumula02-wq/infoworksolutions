// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("Patient")]
[Authorize(Policy = "system.read")]
public class PayerToPayerController : FhirControllerBase
{
    private readonly IFhirResourceRepository<Patient> _patientRepo;
    private readonly IFhirResourceRepository<Coverage> _coverageRepo;
    private readonly IFhirResourceRepository<ExplanationOfBenefit> _eobRepo;
    private readonly IPaginationService _pager;
    private readonly IConsentService _consentService;

    public PayerToPayerController(
        IFhirResourceRepository<Patient> patientRepo,
        IFhirResourceRepository<Coverage> coverageRepo,
        IFhirResourceRepository<ExplanationOfBenefit> eobRepo,
        IPaginationService pager,
        IConsentService consentService)
    {
        _patientRepo    = patientRepo;
        _coverageRepo   = coverageRepo;
        _eobRepo        = eobRepo;
        _pager          = pager;
        _consentService = consentService;
    }

    [HttpPost("$member-match")]
    public async System.Threading.Tasks.Task<IActionResult> MemberMatch(
        [FromBody] Parameters parameters, CancellationToken ct)
    {
        var memberPatientParam = parameters.GetSingle("memberPatient");
        var coverageParam      = parameters.GetSingle("coverageToMatch");

        if (memberPatientParam?.Resource is not Patient memberPatient)
        {
            return FhirValidationError(new OperationOutcome
            {
                Issue = new List<OperationOutcome.IssueComponent>
                {
                    new()
                    {
                        Severity    = OperationOutcome.IssueSeverity.Error,
                        Code        = OperationOutcome.IssueType.Required,
                        Diagnostics = "memberPatient parameter is required"
                    }
                }
            });
        }

        // Search for matching patient by identifier
        var identifierFilters = new Dictionary<string, string?>();
        if (memberPatient.Identifier?.Count > 0)
        {
            var id = memberPatient.Identifier[0];
            identifierFilters["identifier"] = $"{id.System}|{id.Value}";
        }

        var searchParams = new SearchParameters(
            Filters: identifierFilters,
            Skip: 0,
            Take: 2,
            SortField: null,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result = await _patientRepo.SearchAsync(searchParams, ct);

        if (result.TotalCount == 1)
        {
            var matchedPatient = result.Items[0];
            var hasConsent = await _consentService.HasActiveConsentAsync(matchedPatient.Id, ct);
            if (!hasConsent)
                throw new ConsentRequiredException(matchedPatient.Id);

            return FhirOk(matchedPatient);
        }

        // No unique match found
        var noMatchOutcome = new OperationOutcome
        {
            Issue = new List<OperationOutcome.IssueComponent>
            {
                new()
                {
                    Severity    = OperationOutcome.IssueSeverity.Error,
                    Code        = OperationOutcome.IssueType.NotFound,
                    Diagnostics = "No unique member match found"
                }
            }
        };
        return StatusCode(422, noMatchOutcome);
    }

    [HttpGet("{id}/$everything")]
    public async System.Threading.Tasks.Task<IActionResult> Everything(
        string id,
        [FromQuery] DateTimeOffset? _since,
        CancellationToken ct)
    {
        var patient = await _patientRepo.GetByIdAsync(id, ct);
        if (patient is null)
            return FhirNotFound("Patient", id);

        var hasConsent = await _consentService.HasActiveConsentAsync(id, ct);
        if (!hasConsent)
            throw new ConsentRequiredException(id);

        var coverageParams = new SearchParameters(
            Filters: new Dictionary<string, string?> { ["patient"] = id },
            Skip: 0,
            Take: 100,
            SortField: null,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var eobParams = new SearchParameters(
            Filters: new Dictionary<string, string?> { ["patient"] = id },
            Skip: 0,
            Take: 100,
            SortField: null,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var coverageResult = await _coverageRepo.SearchAsync(coverageParams, ct);
        var eobResult      = await _eobRepo.SearchAsync(eobParams, ct);

        var resources = new List<Resource> { patient };

        foreach (var coverage in coverageResult.Items)
        {
            if (_since is null || (coverage.Meta?.LastUpdated >= _since))
                resources.Add(coverage);
        }

        foreach (var eob in eobResult.Items)
        {
            if (_since is null || (eob.Meta?.LastUpdated >= _since))
                resources.Add(eob);
        }

        var bundle = new Bundle
        {
            Type  = Bundle.BundleType.Collection,
            Entry = resources.Select(r => new Bundle.EntryComponent { Resource = r }).ToList()
        };

        return FhirOk(bundle);
    }
}
