// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class MedicationRequestController : FhirControllerBase
{
    private const string ProfileUrl = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-medicationrequest";

    private readonly IFhirResourceRepository<MedicationRequest> _repo;
    private readonly IFhirValidationService _validator;
    private readonly IAuditService _audit;
    private readonly IPaginationService _pager;

    public MedicationRequestController(
        IFhirResourceRepository<MedicationRequest> repo,
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
            return FhirNotFound("MedicationRequest", id);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "MedicationRequest",
            ResourceId: id,
            Action: "read",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(resource);
    }

    [HttpGet]
    public async System.Threading.Tasks.Task<IActionResult> Search(
        [FromQuery] string? patient,
        [FromQuery] string? status,
        [FromQuery] string? intent,
        [FromQuery] int? _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (patient is not null) filters["patient"] = patient;
        if (status is not null)  filters["status"]  = status;
        if (intent is not null)  filters["intent"]  = intent;

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
            ResourceType: "MedicationRequest",
            ResourceId: null,
            Action: "search",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}/MedicationRequest";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }

    [HttpPost]
    public async System.Threading.Tasks.Task<IActionResult> Create([FromBody] MedicationRequest resource, CancellationToken ct)
    {
        var outcome = await _validator.ValidateAsync(resource, ProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        var created = await _repo.CreateAsync(resource, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "MedicationRequest",
            ResourceId: created.Id,
            Action: "create",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var location = $"{Request.Scheme}://{Request.Host}/MedicationRequest/{created.Id}";
        return FhirCreated(created, location);
    }

    [HttpPut("{id}")]
    public async System.Threading.Tasks.Task<IActionResult> Update(string id, [FromBody] MedicationRequest resource, CancellationToken ct)
    {
        var outcome = await _validator.ValidateAsync(resource, ProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        var updated = await _repo.UpdateAsync(id, resource, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "MedicationRequest",
            ResourceId: id,
            Action: "update",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(updated);
    }
}
