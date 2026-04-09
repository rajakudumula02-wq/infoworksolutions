// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class PractitionerController : FhirControllerBase
{
    private const string ProfileUrl = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-practitioner";

    private readonly IFhirResourceRepository<Practitioner> _repo;
    private readonly IFhirValidationService _validator;
    private readonly IAuditService _audit;
    private readonly IPaginationService _pager;

    public PractitionerController(
        IFhirResourceRepository<Practitioner> repo,
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
        var practitioner = await _repo.GetByIdAsync(id, ct);
        if (practitioner is null)
            return FhirNotFound("Practitioner", id);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "Practitioner",
            ResourceId: id,
            Action: "read",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(practitioner);
    }

    [HttpGet]
    public async System.Threading.Tasks.Task<IActionResult> Search(
        [FromQuery] string? _id,
        [FromQuery] string? identifier,
        [FromQuery] string? name,
        [FromQuery] string? specialty,
        [FromQuery] int? _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (_id is not null)        filters["_id"]        = _id;
        if (identifier is not null) filters["identifier"] = identifier;
        if (name is not null)       filters["name"]       = name;
        if (specialty is not null)  filters["specialty"]  = specialty;

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
            PatientId: null,
            ResourceType: "Practitioner",
            ResourceId: null,
            Action: "search",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}/Practitioner";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }

    [HttpPost]
    public async System.Threading.Tasks.Task<IActionResult> Create([FromBody] Practitioner practitioner, CancellationToken ct)
    {
        var outcome = await _validator.ValidateAsync(practitioner, ProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        var created = await _repo.CreateAsync(practitioner, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "Practitioner",
            ResourceId: created.Id,
            Action: "create",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var location = $"{Request.Scheme}://{Request.Host}/Practitioner/{created.Id}";
        return FhirCreated(created, location);
    }

    [HttpPut("{id}")]
    public async System.Threading.Tasks.Task<IActionResult> Update(string id, [FromBody] Practitioner practitioner, CancellationToken ct)
    {
        var outcome = await _validator.ValidateAsync(practitioner, ProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        var updated = await _repo.UpdateAsync(id, practitioner, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "Practitioner",
            ResourceId: id,
            Action: "update",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(updated);
    }
}
