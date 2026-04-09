// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class RelatedPersonController : FhirControllerBase
{
    private const string ProfileUrl = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-relatedperson";

    private readonly IFhirResourceRepository<RelatedPerson> _repo;
    private readonly IFhirValidationService _validator;
    private readonly IAuditService _audit;
    private readonly IPaginationService _pager;

    public RelatedPersonController(
        IFhirResourceRepository<RelatedPerson> repo,
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
        var relatedPerson = await _repo.GetByIdAsync(id, ct);
        if (relatedPerson is null)
            return FhirNotFound("RelatedPerson", id);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "RelatedPerson",
            ResourceId: id,
            Action: "read",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(relatedPerson);
    }

    [HttpGet]
    public async System.Threading.Tasks.Task<IActionResult> Search(
        [FromQuery] string? patient,
        [FromQuery] string? relationship,
        [FromQuery] string? name,
        [FromQuery] string? _id,
        [FromQuery] int? _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (patient is not null)      filters["patient"]      = patient;
        if (relationship is not null) filters["relationship"] = relationship;
        if (name is not null)         filters["name"]         = name;
        if (_id is not null)          filters["_id"]          = _id;

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
            ResourceType: "RelatedPerson",
            ResourceId: null,
            Action: "search",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}/RelatedPerson";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }

    [HttpPost]
    public async System.Threading.Tasks.Task<IActionResult> Create([FromBody] RelatedPerson relatedPerson, CancellationToken ct)
    {
        var outcome = await _validator.ValidateAsync(relatedPerson, ProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        var created = await _repo.CreateAsync(relatedPerson, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "RelatedPerson",
            ResourceId: created.Id,
            Action: "create",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var location = $"{Request.Scheme}://{Request.Host}/RelatedPerson/{created.Id}";
        return FhirCreated(created, location);
    }

    [HttpPut("{id}")]
    public async System.Threading.Tasks.Task<IActionResult> Update(string id, [FromBody] RelatedPerson relatedPerson, CancellationToken ct)
    {
        var outcome = await _validator.ValidateAsync(relatedPerson, ProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        var updated = await _repo.UpdateAsync(id, relatedPerson, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "RelatedPerson",
            ResourceId: id,
            Action: "update",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(updated);
    }
}
