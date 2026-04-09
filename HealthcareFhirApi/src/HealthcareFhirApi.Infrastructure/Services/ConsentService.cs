// Feature: healthcare-fhir-api
namespace HealthcareFhirApi.Infrastructure.Services;

public class ConsentService : IConsentService
{
    private readonly IFhirResourceRepository<Consent> _repo;

    public ConsentService(IFhirResourceRepository<Consent> repo)
    {
        _repo = repo;
    }

    public async Task<bool> HasActiveConsentAsync(string patientId, CancellationToken ct = default)
    {
        var result = await _repo.SearchAsync(new SearchParameters(
            Filters: new Dictionary<string, string?> { ["patient"] = patientId, ["status"] = "active" },
            Skip: 0, Take: 1, SortField: null, SortDescending: false,
            Include: Array.Empty<string>(), RevInclude: Array.Empty<string>()), ct);

        return result.TotalCount > 0;
    }

    public SystemTask ValidateConsentRequiredFieldsAsync(Consent consent, CancellationToken ct = default)
    {
        var missing = new List<string>();
        if (consent.Scope is null) missing.Add("scope");
        if (consent.Patient is null) missing.Add("patient");
        if (consent.Provision?.Period is null) missing.Add("period");

        if (missing.Count > 0)
        {
            var outcome = new OperationOutcome
            {
                Issue = missing.Select(f => new OperationOutcome.IssueComponent
                {
                    Severity    = OperationOutcome.IssueSeverity.Error,
                    Code        = OperationOutcome.IssueType.Required,
                    Diagnostics = $"Required element '{f}' is missing"
                }).ToList()
            };
            throw new FhirValidationException(outcome);
        }

        return SystemTask.CompletedTask;
    }
}
