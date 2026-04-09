// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("Report")]
[Authorize]
public class ReportController : FhirControllerBase
{
    private readonly IFhirResourceRepository<Patient> _patientRepo;
    private readonly IFhirResourceRepository<Claim> _claimRepo;
    private readonly IFhirResourceRepository<ExplanationOfBenefit> _eobRepo;
    private readonly IAuditService _audit;

    public ReportController(
        IFhirResourceRepository<Patient> patientRepo,
        IFhirResourceRepository<Claim> claimRepo,
        IFhirResourceRepository<ExplanationOfBenefit> eobRepo,
        IAuditService audit)
    {
        _patientRepo = patientRepo;
        _claimRepo   = claimRepo;
        _eobRepo     = eobRepo;
        _audit       = audit;
    }

    [HttpGet("year-end")]
    public async System.Threading.Tasks.Task<IActionResult> YearEnd(
        [FromQuery] string member,
        [FromQuery] int year,
        [FromQuery] string? format,
        CancellationToken ct)
    {
        // 1. Find patient by identifier
        var patientSearch = new SearchParameters(
            Filters: new Dictionary<string, string?> { ["identifier"] = member },
            Skip: 0,
            Take: 1,
            SortField: null,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var patientResult = await _patientRepo.SearchAsync(patientSearch, ct);
        var patient = patientResult.Items.FirstOrDefault();

        // 2. If not found, return 404 OperationOutcome
        if (patient is null)
            return FhirNotFound("Patient", member);

        // 3. Enforce SMART scope: requesting user's patient claim must match
        var userPatientClaim = User.FindFirst("patient")?.Value;
        if (userPatientClaim is not null && userPatientClaim != patient.Id)
            return FhirForbidden($"Access to patient {patient.Id} is not authorized for this token.");

        var patientRef = $"Patient/{patient.Id}";

        // 4. Search Claims for this patient where created year matches
        var claimSearch = new SearchParameters(
            Filters: new Dictionary<string, string?> { ["patient"] = patientRef, ["created"] = year.ToString() },
            Skip: 0,
            Take: 1000,
            SortField: null,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var claimResult = await _claimRepo.SearchAsync(claimSearch, ct);
        var claims = claimResult.Items
            .Where(c => c.Created != null && DateTime.TryParse(c.Created, out var cd) && cd.Year == year)
            .ToList();

        // 5. Search EOBs for this patient where created year matches
        var eobSearch = new SearchParameters(
            Filters: new Dictionary<string, string?> { ["patient"] = patientRef },
            Skip: 0,
            Take: 1000,
            SortField: null,
            SortDescending: false,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var eobResult = await _eobRepo.SearchAsync(eobSearch, ct);
        var eobs = eobResult.Items
            .Where(e => e.Created != null && DateTime.TryParse(e.Created, out var ed) && ed.Year == year)
            .ToList();

        // 6. Calculate totals from EOB adjudication
        decimal totalPaidAmount = 0m;
        decimal totalPatientResponsibility = 0m;

        foreach (var eob in eobs)
        {
            foreach (var item in eob.Item)
            {
                foreach (var adj in item.Adjudication)
                {
                    var code = adj.Category?.Coding?.FirstOrDefault()?.Code;
                    var amount = (decimal)(adj.Amount?.Value ?? 0m);
                    if (code == "benefit")
                        totalPaidAmount += amount;
                    else if (code == "patientpay")
                        totalPatientResponsibility += amount;
                }
            }
        }

        // 7. Build CoveredServiceSummary list grouped by service category
        var coveredServices = claims
            .GroupBy(c => c.SubType?.Coding?.FirstOrDefault()?.Code
                       ?? c.Type?.Coding?.FirstOrDefault()?.Code
                       ?? "unknown")
            .Select(g => new CoveredServiceSummary(
                ServiceCategory: g.Key,
                ClaimCount: g.Count(),
                PaidAmount: eobs
                    .Where(e => e.Claim?.Reference != null &&
                                g.Any(c => $"Claim/{c.Id}" == e.Claim.Reference))
                    .SelectMany(e => e.Item)
                    .SelectMany(i => i.Adjudication)
                    .Where(a => a.Category?.Coding?.FirstOrDefault()?.Code == "benefit")
                    .Sum(a => (decimal)(a.Amount?.Value ?? 0m))
            ))
            .ToList();

        // 8. Create AuditEvent
        await _audit.RecordAsync(new AuditContext(
            ClientId: User.FindFirst("client_id")?.Value ?? "unknown",
            PatientId: patient.Id,
            ResourceType: "Report",
            ResourceId: $"year-end/{member}/{year}",
            Action: "search",
            Timestamp: DateTimeOffset.UtcNow), ct);

        var result = new YearEndReportResult(
            MemberId: member,
            Year: year,
            TotalClaimsCount: claims.Count,
            TotalPaidAmount: totalPaidAmount,
            TotalPatientResponsibility: totalPatientResponsibility,
            CoveredServices: coveredServices);

        // Return based on format
        if (string.Equals(format, "fhir-bundle", StringComparison.OrdinalIgnoreCase))
        {
            var bundle = BuildYearEndBundle(patient, result, year);
            return FhirOk(bundle);
        }

        // Default: return JSON result
        return Ok(result);
    }

    private static Bundle BuildYearEndBundle(Patient patient, YearEndReportResult report, int year)
    {
        var composition = new Composition
        {
            Status = CompositionStatus.Final,
            Type = new CodeableConcept("http://loinc.org", "34133-9", "Summary of episode note"),
            Subject = new ResourceReference($"Patient/{patient.Id}"),
            Date = new DateTimeOffset(year, 12, 31, 0, 0, 0, TimeSpan.Zero).ToString("yyyy-MM-dd"),
            Title = $"Year-End Summary Report {year}",
            Author = new List<ResourceReference>
            {
                new ResourceReference("Organization/payer")
            },
            Section = new List<Composition.SectionComponent>
            {
                new()
                {
                    Title = "Claims Summary",
                    Text = new Narrative
                    {
                        Status = Narrative.NarrativeStatus.Generated,
                        Div = $"<div xmlns=\"http://www.w3.org/1999/xhtml\">" +
                              $"Total Claims: {report.TotalClaimsCount}, " +
                              $"Total Paid: {report.TotalPaidAmount:C}, " +
                              $"Patient Responsibility: {report.TotalPatientResponsibility:C}" +
                              $"</div>"
                    }
                }
            }
        };

        return new Bundle
        {
            Type = Bundle.BundleType.Document,
            Entry = new List<Bundle.EntryComponent>
            {
                new() { Resource = composition },
                new() { Resource = patient }
            }
        };
    }
}
