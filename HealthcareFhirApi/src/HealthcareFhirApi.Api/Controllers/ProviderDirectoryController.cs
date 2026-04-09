// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("directory")]
[Authorize]
public class ProviderDirectoryController : FhirControllerBase
{
    private readonly IFhirResourceRepository<Practitioner>     _practitionerRepo;
    private readonly IFhirResourceRepository<Organization>     _organizationRepo;
    private readonly IFhirResourceRepository<Location>         _locationRepo;
    private readonly IFhirResourceRepository<PractitionerRole> _practitionerRoleRepo;
    private readonly IPaginationService                        _pager;
    private readonly IFhirValidationService                    _validator;

    public ProviderDirectoryController(
        IFhirResourceRepository<Practitioner>     practitionerRepo,
        IFhirResourceRepository<Organization>     organizationRepo,
        IFhirResourceRepository<Location>         locationRepo,
        IFhirResourceRepository<PractitionerRole> practitionerRoleRepo,
        IPaginationService                        pager,
        IFhirValidationService                    validator)
    {
        _practitionerRepo     = practitionerRepo;
        _organizationRepo     = organizationRepo;
        _locationRepo         = locationRepo;
        _practitionerRoleRepo = practitionerRoleRepo;
        _pager                = pager;
        _validator            = validator;
    }

    // ── Practitioner ────────────────────────────────────────────────────────

    [HttpGet("Practitioner/{id}")]
    public async System.Threading.Tasks.Task<IActionResult> ReadPractitioner(string id, CancellationToken ct)
    {
        var resource = await _practitionerRepo.GetByIdAsync(id, ct);
        if (resource is null)
            return FhirNotFound("Practitioner", id);

        return FhirOk(resource);
    }

    [HttpGet("Practitioner")]
    public async System.Threading.Tasks.Task<IActionResult> SearchPractitioner(
        [FromQuery] string? specialty,
        [FromQuery] string? name,
        [FromQuery] string? identifier,
        [FromQuery] string? _id,
        [FromQuery] int?    _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (specialty   is not null) filters["specialty"]   = specialty;
        if (name        is not null) filters["name"]        = name;
        if (identifier  is not null) filters["identifier"]  = identifier;
        if (_id         is not null) filters["_id"]         = _id;

        var parameters = new SearchParameters(
            Filters: filters,
            Skip: skip,
            Take: take,
            SortField: _sort,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result  = await _practitionerRepo.SearchAsync(parameters, ct);
        var baseUrl = $"{Request.Scheme}://{Request.Host}/Practitioner";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }

    // ── Organization ────────────────────────────────────────────────────────

    [HttpGet("Organization/{id}")]
    public async System.Threading.Tasks.Task<IActionResult> ReadOrganization(string id, CancellationToken ct)
    {
        var resource = await _organizationRepo.GetByIdAsync(id, ct);
        if (resource is null)
            return FhirNotFound("Organization", id);

        return FhirOk(resource);
    }

    [HttpGet("Organization")]
    public async System.Threading.Tasks.Task<IActionResult> SearchOrganization(
        [FromQuery] string? name,
        [FromQuery] string? type,
        [FromQuery] string? identifier,
        [FromQuery] string? _id,
        [FromQuery] int?    _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (name       is not null) filters["name"]       = name;
        if (type       is not null) filters["type"]       = type;
        if (identifier is not null) filters["identifier"] = identifier;
        if (_id        is not null) filters["_id"]        = _id;

        var parameters = new SearchParameters(
            Filters: filters,
            Skip: skip,
            Take: take,
            SortField: _sort,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result  = await _organizationRepo.SearchAsync(parameters, ct);
        var baseUrl = $"{Request.Scheme}://{Request.Host}/Organization";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }

    // ── Location ─────────────────────────────────────────────────────────────

    [HttpGet("Location/{id}")]
    public async System.Threading.Tasks.Task<IActionResult> ReadLocation(string id, CancellationToken ct)
    {
        var resource = await _locationRepo.GetByIdAsync(id, ct);
        if (resource is null)
            return FhirNotFound("Location", id);

        return FhirOk(resource);
    }

    [HttpGet("Location")]
    public async System.Threading.Tasks.Task<IActionResult> SearchLocation(
        [FromQuery] string? near,
        [FromQuery] string? address,
        [FromQuery(Name = "address-state")] string? addressState,
        [FromQuery] string? type,
        [FromQuery] string? organization,
        [FromQuery] string? _id,
        [FromQuery] int?    _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (near         is not null) filters["near"]          = near;
        if (address      is not null) filters["address"]       = address;
        if (addressState is not null) filters["address-state"] = addressState;
        if (type         is not null) filters["type"]          = type;
        if (organization is not null) filters["organization"]  = organization;
        if (_id          is not null) filters["_id"]           = _id;

        var parameters = new SearchParameters(
            Filters: filters,
            Skip: skip,
            Take: take,
            SortField: _sort,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result  = await _locationRepo.SearchAsync(parameters, ct);
        var baseUrl = $"{Request.Scheme}://{Request.Host}/Location";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);

        // When "near" is provided, note that proximity ordering is applied
        if (near is not null)
        {
            bundle.Meta ??= new Meta();
            bundle.Meta.Tag ??= new List<Coding>();
            bundle.Meta.Tag.Add(new Coding
            {
                System  = "http://terminology.hl7.org/CodeSystem/v3-ObservationValue",
                Code    = "PROXIMITY",
                Display = "Proximity ordering applied based on 'near' parameter"
            });
        }

        return FhirOk(bundle);
    }

    // ── PractitionerRole ─────────────────────────────────────────────────────

    [HttpGet("PractitionerRole/{id}")]
    public async System.Threading.Tasks.Task<IActionResult> ReadPractitionerRole(string id, CancellationToken ct)
    {
        var resource = await _practitionerRoleRepo.GetByIdAsync(id, ct);
        if (resource is null)
            return FhirNotFound("PractitionerRole", id);

        return FhirOk(resource);
    }

    [HttpGet("PractitionerRole")]
    public async System.Threading.Tasks.Task<IActionResult> SearchPractitionerRole(
        [FromQuery] string? specialty,
        [FromQuery] string? organization,
        [FromQuery] string? network,
        [FromQuery] string? location,
        [FromQuery(Name = "accepting-patients")] string? acceptingPatients,
        [FromQuery(Name = "insurance-plan")]     string? insurancePlan,
        [FromQuery] string? _id,
        [FromQuery] int?    _count,
        [FromQuery] string? _sort,
        CancellationToken ct)
    {
        var (skip, take) = _pager.ResolvePage(_count, null);

        var filters = new Dictionary<string, string?>();
        if (specialty        is not null) filters["specialty"]         = specialty;
        if (organization     is not null) filters["organization"]      = organization;
        if (network          is not null) filters["network"]           = network;
        if (location         is not null) filters["location"]          = location;
        if (acceptingPatients is not null) filters["accepting-patients"] = acceptingPatients;
        if (insurancePlan    is not null) filters["insurance-plan"]    = insurancePlan;
        if (_id              is not null) filters["_id"]               = _id;

        var parameters = new SearchParameters(
            Filters: filters,
            Skip: skip,
            Take: take,
            SortField: _sort,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result  = await _practitionerRoleRepo.SearchAsync(parameters, ct);
        var baseUrl = $"{Request.Scheme}://{Request.Host}/PractitionerRole";
        var bundle  = _pager.BuildSearchBundle(result.Items, parameters, result.TotalCount, baseUrl);
        return FhirOk(bundle);
    }
}
