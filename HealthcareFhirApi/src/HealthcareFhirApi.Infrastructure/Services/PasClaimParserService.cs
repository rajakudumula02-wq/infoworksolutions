using Hl7.Fhir.Model;
using HealthcareFhirApi.Core.Models;

namespace HealthcareFhirApi.Infrastructure.Services;

public class PasClaimParserService
{
    public PasExtractedData Extract(Claim claim, Bundle? fullBundle = null)
    {
        var data = new PasExtractedData();

        // MemberId from claim.patient
        data.MemberId = ExtractId(claim.Patient?.Reference);
        data.MemberName = claim.Patient?.Display ?? string.Empty;

        // ProviderId from claim.provider
        data.ProviderId = claim.Provider?.Reference ?? string.Empty;
        data.ProviderName = claim.Provider?.Display ?? string.Empty;

        // InsurerId from claim.insurer
        data.InsurerId = claim.Insurer?.Reference ?? string.Empty;

        // MemberCoverageId from claim.insurance[0].coverage
        if (claim.Insurance?.Count > 0)
        {
            var ins = claim.Insurance[0];
            data.MemberCoverageId = ExtractId(ins.Coverage?.Reference);
            data.CoveragePayer = ins.Coverage?.Display ?? string.Empty;
        }

        // SubscriberId — look in prefetch coverage if bundle provided
        if (fullBundle != null)
        {
            ExtractFromBundle(fullBundle, data);
        }

        // Procedures from claim.item
        if (claim.Item != null)
        {
            foreach (var item in claim.Item)
            {
                var proc = new PasProcedureItem
                {
                    Sequence = item.Sequence ?? 0,
                };

                // Procedure code
                if (item.ProductOrService?.Coding?.Count > 0)
                {
                    var coding = item.ProductOrService.Coding[0];
                    proc.ProcedureCode = coding.Code ?? string.Empty;
                    proc.ProcedureSystem = coding.System ?? string.Empty;
                    proc.ProcedureDisplay = coding.Display ?? string.Empty;
                }

                // Serviced date
                if (item.Serviced is FhirDateTime dt)
                    proc.ServicedDate = dt.Value ?? string.Empty;
                else if (item.Serviced is Period period)
                    proc.ServicedDate = $"{period.Start} to {period.End}";

                // Place of service
                if (item.Location is CodeableConcept loc && loc.Coding?.Count > 0)
                    proc.PlaceOfService = $"{loc.Coding[0].Code} — {loc.Coding[0].Display}";

                // Tooth numbers from bodySite
                if (item.BodySite?.Coding != null)
                {
                    foreach (var bs in item.BodySite.Coding)
                    {
                        if (!string.IsNullOrEmpty(bs.Code))
                            proc.ToothNumbers.Add(bs.Code);
                    }
                }

                data.Procedures.Add(proc);
            }
        }

        // Match diagnosis codes to items by sequence
        if (claim.Diagnosis != null)
        {
            foreach (var diag in claim.Diagnosis)
            {
                var seq = diag.Sequence ?? 0;
                var matchingProc = data.Procedures.FirstOrDefault(p => p.Sequence == seq);
                if (matchingProc != null && diag.Diagnosis is CodeableConcept dx && dx.Coding?.Count > 0)
                {
                    matchingProc.DiagnosisCode = dx.Coding[0].Code ?? string.Empty;
                    matchingProc.DiagnosisDisplay = dx.Coding[0].Display ?? string.Empty;
                }
            }
        }

        return data;
    }

    private void ExtractFromBundle(Bundle bundle, PasExtractedData data)
    {
        foreach (var entry in bundle.Entry ?? Enumerable.Empty<Bundle.EntryComponent>())
        {
            var res = entry.Resource;
            if (res is Coverage cov)
            {
                if (string.IsNullOrEmpty(data.MemberCoverageId))
                    data.MemberCoverageId = cov.Id ?? string.Empty;
                data.SubscriberId = cov.SubscriberId ?? string.Empty;
                if (cov.Type?.Text != null)
                    data.CoverageType = cov.Type.Text;
                if (cov.Payor?.Count > 0)
                    data.CoveragePayer = cov.Payor[0].Display ?? string.Empty;
            }
            else if (res is Patient pat)
            {
                if (string.IsNullOrEmpty(data.MemberId))
                    data.MemberId = pat.Id ?? string.Empty;
                if (pat.Name?.Count > 0)
                    data.MemberName = $"{string.Join(" ", pat.Name[0].Given ?? Enumerable.Empty<string>())} {pat.Name[0].Family}".Trim();
            }
            else if (res is Location loc)
            {
                if (string.IsNullOrEmpty(data.OfficeId))
                    data.OfficeId = loc.Id ?? string.Empty;
            }
            else if (res is Practitioner prac)
            {
                if (string.IsNullOrEmpty(data.ProviderId))
                    data.ProviderId = $"Practitioner/{prac.Id}";
                if (prac.Name?.Count > 0)
                    data.ProviderName = $"{string.Join(" ", prac.Name[0].Prefix ?? Enumerable.Empty<string>())} {string.Join(" ", prac.Name[0].Given ?? Enumerable.Empty<string>())} {prac.Name[0].Family}".Trim();
            }
        }
    }

    private static string ExtractId(string? reference)
    {
        if (string.IsNullOrEmpty(reference)) return string.Empty;
        var parts = reference.Split('/');
        return parts.Length > 1 ? parts[^1] : reference;
    }
}
