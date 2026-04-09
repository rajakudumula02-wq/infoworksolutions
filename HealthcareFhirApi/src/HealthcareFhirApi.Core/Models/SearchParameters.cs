namespace HealthcareFhirApi.Core.Models;

public record SearchParameters(
    Dictionary<string, string?> Filters,
    int Skip,
    int Take,
    string? SortField,
    bool SortDescending,
    IReadOnlyList<string> Include,
    IReadOnlyList<string> RevInclude
);
