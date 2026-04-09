// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("[controller]")]
[Authorize]
public class ClaimController : FhirControllerBase
{
    private const string ClaimProfileUrl = "http://hl7.org/fhir/StructureDefinition/Claim";

    private readonly IFhirResourceRepository<Claim> _claimRepo;
    private readonly IFhirResourceRepository<ClaimResponse> _claimResponseRepo;
    private readonly IFhirValidationService _validator;
    private readonly IAuditService _audit;
    private readonly IPaginationService _pager;

    public ClaimController(
        IFhirResourceRepository<Claim> claimRepo,
        IFhirResourceRepository<ClaimResponse> claimResponseRepo,
        IFhirValidationService validator,
        IAuditService audit,
        IPaginationService pager)
    {
        _claimRepo         = claimRepo;
        _claimResponseRepo = claimResponseRepo;
        _validator         = validator;
        _audit             = audit;
        _pager             = pager;
    }

    [HttpGet("{id}")]
    public async System.Threading.Tasks.Task<IActionResult> Read(string id, CancellationToken ct)
    {
        var claim = await _claimRepo.GetByIdAsync(id, ct);
        if (claim is null)
            return FhirNotFound("Claim", id);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: claim.Patient?.Reference,
            ResourceType: "Claim",
            ResourceId: id,
            Action: "read",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(claim);
    }

    [HttpGet]
    public async System.Threading.Tasks.Task<IActionResult> Search(
        [FromQuery] string? _id,
        [FromQuery] string? patient,
        [FromQuery] string? provider,
        [FromQuery] string? status,
        [FromQuery] string? created,
        [FromQuery] int? _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (_id is not null)      filters["_id"]      = _id;
        if (patient is not null)  filters["patient"]  = patient;
        if (provider is not null) filters["provider"] = provider;
        if (status is not null)   filters["status"]   = status;
        if (created is not null)  filters["created"]  = created;

        var parameters = new SearchParameters(
            Filters: filters,
            Skip: skip,
            Take: take,
            SortField: _sort,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result = await _claimRepo.SearchAsync(parameters, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "Claim",
            ResourceId: null,
            Action: "search",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}/Claim";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }

    [HttpPost]
    public async System.Threading.Tasks.Task<IActionResult> Create([FromBody] Claim claim, CancellationToken ct)
    {
        var outcome = await _validator.ValidateAsync(claim, ClaimProfileUrl, ct);
        if (!_validator.IsValid(outcome))
            return FhirValidationError(outcome);

        var created = await _claimRepo.CreateAsync(claim, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: created.Patient?.Reference,
            ResourceType: "Claim",
            ResourceId: created.Id,
            Action: "create",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var location = $"{Request.Scheme}://{Request.Host}/Claim/{created.Id}";
        return FhirCreated(created, location);
    }

    [HttpGet("ClaimResponse/{id}")]
    public async System.Threading.Tasks.Task<IActionResult> ReadClaimResponse(string id, CancellationToken ct)
    {
        var claimResponse = await _claimResponseRepo.GetByIdAsync(id, ct);
        if (claimResponse is null)
            return FhirNotFound("ClaimResponse", id);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: claimResponse.Patient?.Reference,
            ResourceType: "ClaimResponse",
            ResourceId: id,
            Action: "read",
            Timestamp: DateTimeOffset.UtcNow), ct);

        return FhirOk(claimResponse);
    }

    [HttpGet("ClaimResponse")]
    public async System.Threading.Tasks.Task<IActionResult> SearchClaimResponse(
        [FromQuery] string? _id,
        [FromQuery] string? patient,
        [FromQuery] string? request,
        [FromQuery] string? outcome,
        [FromQuery] int? _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (_id is not null)      filters["_id"]      = _id;
        if (patient is not null)  filters["patient"]  = patient;
        if (request is not null)  filters["request"]  = request;
        if (outcome is not null)  filters["outcome"]  = outcome;

        var parameters = new SearchParameters(
            Filters: filters,
            Skip: skip,
            Take: take,
            SortField: _sort,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result = await _claimResponseRepo.SearchAsync(parameters, ct);

        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: null,
            ResourceType: "ClaimResponse",
            ResourceId: null,
            Action: "search",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}/Claim/ClaimResponse";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }
}
