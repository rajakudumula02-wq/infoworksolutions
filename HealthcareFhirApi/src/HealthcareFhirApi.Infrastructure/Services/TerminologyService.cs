// Feature: healthcare-fhir-api
using StackExchange.Redis;

namespace HealthcareFhirApi.Infrastructure.Services;

public class TerminologyService : ITerminologyService
{
    private static readonly HashSet<string> SupportedSystems = new()
    {
        "http://snomed.info/sct",
        "http://loinc.org",
        "http://hl7.org/fhir/sid/icd-10",
        "http://www.ama-assn.org/go/cpt",
        "http://www.nlm.nih.gov/research/umls/rxnorm"
    };

    private readonly IDatabase _redis;
    private readonly IFhirResourceRepository<CodeSystem> _codeSystemRepo;
    private readonly IFhirResourceRepository<ValueSet> _valueSetRepo;
    private readonly FhirJsonParser _parser = new();
    private readonly FhirJsonSerializer _serializer = new();

    public TerminologyService(
        IDatabase redis,
        IFhirResourceRepository<CodeSystem> codeSystemRepo,
        IFhirResourceRepository<ValueSet> valueSetRepo)
    {
        _redis          = redis;
        _codeSystemRepo = codeSystemRepo;
        _valueSetRepo   = valueSetRepo;
    }

    public async System.Threading.Tasks.Task<Parameters> LookupAsync(
        string system, string code, string? version, CancellationToken ct = default)
    {
        if (!SupportedSystems.Contains(system))
            throw new UnsupportedCodeSystemException(system);

        var cacheKey = $"terminology:lookup:{system}:{code}";
        var cached   = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return _parser.Parse<Parameters>(cached!);

        var result = new Parameters
        {
            Parameter = new List<Parameters.ParameterComponent>
            {
                new() { Name = "display",     Value = new FhirString($"Display for {code}") },
                new() { Name = "definition",  Value = new FhirString($"Definition for {code} in {system}") },
                new() { Name = "designation", Value = new FhirString(code) }
            }
        };

        await _redis.StringSetAsync(cacheKey, _serializer.SerializeToString(result), TimeSpan.FromHours(24));
        return result;
    }

    public async System.Threading.Tasks.Task<Parameters> ValidateCodeAsync(
        string url, string system, string code, string? display, CancellationToken ct = default)
    {
        if (!SupportedSystems.Contains(system))
            throw new UnsupportedCodeSystemException(system);

        var cacheKey = $"terminology:validate:{system}:{code}";
        var cached   = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return _parser.Parse<Parameters>(cached!);

        var result = new Parameters
        {
            Parameter = new List<Parameters.ParameterComponent>
            {
                new() { Name = "result", Value = new FhirBoolean(true) }
            }
        };

        await _redis.StringSetAsync(cacheKey, _serializer.SerializeToString(result), TimeSpan.FromHours(24));
        return result;
    }

    public async System.Threading.Tasks.Task<ValueSet> ExpandAsync(
        string url, string? filter, int? count, CancellationToken ct = default)
    {
        var cacheKey = $"terminology:expand:{url}:{filter}";
        var cached   = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return _parser.Parse<ValueSet>(cached!);

        var result = new ValueSet
        {
            Url    = url,
            Status = PublicationStatus.Active,
            Expansion = new ValueSet.ExpansionComponent
            {
                Timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Total     = 0,
                Contains  = new List<ValueSet.ContainsComponent>()
            }
        };

        await _redis.StringSetAsync(cacheKey, _serializer.SerializeToString(result), TimeSpan.FromHours(24));
        return result;
    }

    public async System.Threading.Tasks.Task<Parameters> TranslateAsync(
        string url, string system, string code, string targetSystem, CancellationToken ct = default)
    {
        if (!SupportedSystems.Contains(system))
            throw new UnsupportedCodeSystemException(system);

        var cacheKey = $"terminology:translate:{system}:{code}";
        var cached   = await _redis.StringGetAsync(cacheKey);
        if (cached.HasValue)
            return _parser.Parse<Parameters>(cached!);

        var matchPart = new Parameters.ParameterComponent
        {
            Name = "match",
            Part = new List<Parameters.ParameterComponent>
            {
                new() { Name = "equivalence", Value = new FhirString("equivalent") },
                new() { Name = "concept",     Value = new Coding(targetSystem, code) }
            }
        };

        var result = new Parameters
        {
            Parameter = new List<Parameters.ParameterComponent>
            {
                new() { Name = "result", Value = new FhirBoolean(true) },
                matchPart
            }
        };

        await _redis.StringSetAsync(cacheKey, _serializer.SerializeToString(result), TimeSpan.FromHours(24));
        return result;
    }
}
