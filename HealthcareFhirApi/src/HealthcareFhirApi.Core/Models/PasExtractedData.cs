namespace HealthcareFhirApi.Core.Models;

public class PasExtractedData
{
    public string MemberId { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string MemberCoverageId { get; set; } = string.Empty;
    public string CoverageType { get; set; } = string.Empty;
    public string CoveragePayer { get; set; } = string.Empty;
    public string SubscriberId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string OfficeId { get; set; } = string.Empty;
    public string InsurerId { get; set; } = string.Empty;
    public List<PasProcedureItem> Procedures { get; set; } = new();
}

public class PasProcedureItem
{
    public int Sequence { get; set; }
    public string ProcedureCode { get; set; } = string.Empty;
    public string ProcedureSystem { get; set; } = string.Empty;
    public string ProcedureDisplay { get; set; } = string.Empty;
    public string DiagnosisCode { get; set; } = string.Empty;
    public string DiagnosisDisplay { get; set; } = string.Empty;
    public string ServicedDate { get; set; } = string.Empty;
    public string PlaceOfService { get; set; } = string.Empty;
    public List<string> ToothNumbers { get; set; } = new();
}
