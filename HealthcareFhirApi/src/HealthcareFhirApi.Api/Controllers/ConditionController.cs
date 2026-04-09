// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class ConditionController : FhirControllerBase
{
    private const string ProfileUrl = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-condition-problems-health-concerns";

    private readonly IFhirResourceRepository<Condition> _repo;
    private readonly IFhirValidationService _validator;
    private readonly IAuditService _audit;
    private readonly IPaginationService _pager;

    public ConditionController(
        IFhirResourceRepository<Condition> repo,
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
        var resource = await _repo.GetByIdAsync(id, ct);
        if (resource is null)
            return FhirNotFound("Condition", id);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "Condition",
            ResourceId: id,
            Action: "read",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(resource);
    }

    [HttpGet]
    public async System.Threading.Tasks.Task<IActionResult> Search(
        [FromQuery] string? patient,
        [FromQuery] string? category,
        [FromQuery(Name = "clinical-status")] string? clinicalStatus,
        [FromQuery(Name = "onset-date")] string? onsetDate,
        [FromQuery] int? _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (patient is not null)        filters["patient"]         = patient;
        if (category is not null)       filters["category"]        = category;
        if (clinicalStatus is not null) filters["clinical-status"] = clinicalStatus;
        if (onsetDate is not null)      filters["onset-date"]      = onsetDate;

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
            ResourceType: "Condition",
            ResourceId: null,
            Action: "search",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}/Condition";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }

    [HttpPost]
    public async System.Threading.Tasks.Task<IActionResult> Create([FromBody] Condition condition, CancellationToken ct)
    {
        var outcome = await _validator.ValidateAsync(condition, ProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        var created = await _repo.CreateAsync(condition, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "Condition",
            ResourceId: created.Id,
            Action: "create",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var location = $"{Request.Scheme}://{Request.Host}/Condition/{created.Id}";
        return FhirCreated(created, location);
    }

    [HttpPut("{id}")]
    public async System.Threading.Tasks.Task<IActionResult> Update(string id, [FromBody] Condition condition, CancellationToken ct)
    {
        var outcome = await _validator.ValidateAsync(condition, ProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        var updated = await _repo.UpdateAsync(id, condition, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "Condition",
            ResourceId: id,
            Action: "update",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(updated);
    }
}
