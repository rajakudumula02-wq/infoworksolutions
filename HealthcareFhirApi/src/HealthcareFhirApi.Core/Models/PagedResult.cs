namespace HealthcareFhirApi.Core.Models;

public record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    SearchParameters Parameters
);
