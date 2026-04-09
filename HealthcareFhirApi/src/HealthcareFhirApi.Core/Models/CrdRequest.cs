namespace HealthcareFhirApi.Core.Models;

public class CrdHookRequest
{
    public string Hook { get; set; } = string.Empty;
    public string HookInstance { get; set; } = string.Empty;
    public string FhirServer { get; set; } = string.Empty;
    public CrdContext Context { get; set; } = new();
    public CrdPrefetch Prefetch { get; set; } = new();
}

public class CrdContext
{
    public string UserId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string EncounterId { get; set; } = string.Empty;
}

public class CrdPrefetch { }

public class CrdExtractedData
{
    public string MemberId { get; set; } = string.Empty;
    public string MemberCoverageId { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public string OfficeId { get; set; } = string.Empty;
    public List<CrdProcedure> Procedures { get; set; } = new();
}

public class CrdProcedure
{
    public string ServiceRequestId { get; set; } = string.Empty;
    public string ProcedureCode { get; set; } = string.Empty;
    public string ProcedureSystem { get; set; } = string.Empty;
    public string ProcedureDisplay { get; set; } = string.Empty;
    public List<string> BillingCodes { get; set; } = new();
    public List<string> ToothNumbers { get; set; } = new();
}
