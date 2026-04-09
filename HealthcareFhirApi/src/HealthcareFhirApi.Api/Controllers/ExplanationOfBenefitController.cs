// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class ExplanationOfBenefitController : FhirControllerBase
{
    private const string EobProfileUrl = "http://hl7.org/fhir/us/carin-bb/StructureDefinition/C4BB-ExplanationOfBenefit";

    private readonly IFhirResourceRepository<ExplanationOfBenefit> _repo;
    private readonly IFhirValidationService _validator;
    private readonly IAuditService _audit;
    private readonly IPaginationService _pager;

    public ExplanationOfBenefitController(
        IFhirResourceRepository<ExplanationOfBenefit> repo,
        IFhirValidationService validator,
        IAuditService audit,
        IPaginationService pager)
    {
        _repo      = repo;
        _validator = validator;
        _audit     = audit;
        _pager     = pager;
    }

    [HttpGet("{id}")]
    public async System.Threading.Tasks.Task<IActionResult> Read(string id, CancellationToken ct)
    {
        var eob = await _repo.GetByIdAsync(id, ct);
        if (eob is null)
            return FhirNotFound("ExplanationOfBenefit", id);

        var requestingPatient = User.FindFirst("patient")?.Value;
        var eobPatient        = eob.Patient?.Reference;
        if (requestingPatient is not null && eobPatient is not null
            && !eobPatient.EndsWith(requestingPatient, StringComparison.OrdinalIgnoreCase))
        {
            return FhirForbidden("Access to this ExplanationOfBenefit is not permitted for the requesting patient.");
        }

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: eob.Patient?.Reference,
            ResourceType: "ExplanationOfBenefit",
            ResourceId: id,
            Action: "read",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(eob);
    }

    [HttpGet]
    public async System.Threading.Tasks.Task<IActionResult> Search(
        [FromQuery] string? _id,
        [FromQuery] string? patient,
        [FromQuery] string? provider,
        [FromQuery] string? created,
        [FromQuery] string? status,
        [FromQuery] int? _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var requestingPatient = User.FindFirst("patient")?.Value;
        if (requestingPatient is not null && patient is not null
            && !patient.EndsWith(requestingPatient, StringComparison.OrdinalIgnoreCase))
        {
            return FhirForbidden("Access to ExplanationOfBenefit resources for this patient is not permitted.");
        }

        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (_id is not null)      filters["_id"]      = _id;
        if (patient is not null)  filters["patient"]  = patient;
        if (provider is not null) filters["provider"] = provider;
        if (created is not null)  filters["created"]  = created;
        if (status is not null)   filters["status"]   = status;

        var parameters = new SearchParameters(
            Filters: filters,
            Skip: skip,
            Take: take,
            SortField: _sort,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result = await _repo.SearchAsync(parameters, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: patient,
            ResourceType: "ExplanationOfBenefit",
            ResourceId: null,
            Action: "search",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}/ExplanationOfBenefit";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }
}
