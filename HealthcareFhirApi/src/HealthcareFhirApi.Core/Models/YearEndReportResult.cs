namespace HealthcareFhirApi.Core.Models;

public record YearEndReportResult(
    string MemberId,
    int Year,
    int TotalClaimsCount,
    decimal TotalPaidAmount,
    decimal TotalPatientResponsibility,
    IReadOnlyList<CoveredServiceSummary> CoveredServices
);

public record CoveredServiceSummary(
    string ServiceCategory,
    int ClaimCount,
    decimal PaidAmount
);
