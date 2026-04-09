using System.Text.Json;
using HealthcareFhirApi.Core.Models;

namespace HealthcareFhirApi.Infrastructure.Services;

public class CrdParserService
{
    public CrdExtractedData Parse(JsonElement root)
    {
        var result = new CrdExtractedData();

        // MemberId from context.patientId
        if (root.TryGetProperty("context", out var ctx))
        {
            result.MemberId = ctx.GetStringOrEmpty("patientId");
            result.ProviderId = ctx.GetStringOrEmpty("userId");
        }

        // Prefetch: coverage, patient, locations
        if (root.TryGetProperty("prefetch", out var prefetch))
        {
            // MemberCoverageId from prefetch.coverage
            result.MemberCoverageId = ExtractCoverageId(prefetch);

            // OfficeId from prefetch.locations
            result.OfficeId = ExtractLocationId(prefetch);

            // ProviderId fallback from prefetch.practitioners
            if (string.IsNullOrEmpty(result.ProviderId) || !result.ProviderId.Contains("/"))
                result.ProviderId = ExtractPractitionerId(prefetch);
        }

        // Procedures from context.draftOrders
        if (root.TryGetProperty("context", out var ctx2) && ctx2.TryGetProperty("draftOrders", out var orders))
        {
            result.Procedures = ExtractProcedures(orders);
        }

        return result;
    }

    private static string ExtractCoverageId(JsonElement prefetch)
    {
        // Try prefetch.coverage as Bundle
        if (prefetch.TryGetProperty("coverage", out var cov))
        {
            var entry = GetFirstEntry(cov);
            if (entry.HasValue && entry.Value.TryGetProperty("resource", out var res))
                return res.GetStringOrEmpty("id");
            // Direct resource
            return cov.GetStringOrEmpty("id");
        }
        return string.Empty;
    }

    private static string ExtractLocationId(JsonElement prefetch)
    {
        if (prefetch.TryGetProperty("locations", out var locs))
        {
            var entry = GetFirstEntry(locs);
            if (entry.HasValue && entry.Value.TryGetProperty("resource", out var res))
                return res.GetStringOrEmpty("id");
        }
        return string.Empty;
    }

    private static string ExtractPractitionerId(JsonElement prefetch)
    {
        if (prefetch.TryGetProperty("practitioners", out var pracs))
        {
            var entry = GetFirstEntry(pracs);
            if (entry.HasValue && entry.Value.TryGetProperty("resource", out var res))
            {
                var id = res.GetStringOrEmpty("id");
                return string.IsNullOrEmpty(id) ? string.Empty : $"Practitioner/{id}";
            }
        }
        return string.Empty;
    }

    private static List<CrdProcedure> ExtractProcedures(JsonElement draftOrders)
    {
        var procedures = new List<CrdProcedure>();

        if (!draftOrders.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
            return procedures;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("resource", out var resource)) continue;
            if (resource.GetStringOrEmpty("resourceType") != "ServiceRequest") continue;

            var proc = new CrdProcedure
            {
                ServiceRequestId = resource.GetStringOrEmpty("id"),
            };

            // Primary procedure code
            if (resource.TryGetProperty("code", out var code))
            {
                if (code.TryGetProperty("coding", out var codings) && codings.ValueKind == JsonValueKind.Array)
                {
                    foreach (var coding in codings.EnumerateArray())
                    {
                        proc.ProcedureCode = coding.GetStringOrEmpty("code");
                        proc.ProcedureSystem = coding.GetStringOrEmpty("system");
                        proc.ProcedureDisplay = coding.GetStringOrEmpty("display");
                        break; // first coding
                    }
                }

                // Billing options from extensions
                if (code.TryGetProperty("extension", out var exts) && exts.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ext in exts.EnumerateArray())
                    {
                        if (ext.GetStringOrEmpty("url").Contains("ext-billing-options"))
                        {
                            if (ext.TryGetProperty("valueCodeableConcept", out var vcc) &&
                                vcc.TryGetProperty("coding", out var bCodings) &&
                                bCodings.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var bc in bCodings.EnumerateArray())
                                    proc.BillingCodes.Add(bc.GetStringOrEmpty("code"));
                            }
                        }
                    }
                }
            }

            // Tooth numbers from bodySite (dental)
            if (resource.TryGetProperty("bodySite", out var bodySites) && bodySites.ValueKind == JsonValueKind.Array)
            {
                foreach (var site in bodySites.EnumerateArray())
                {
                    if (site.TryGetProperty("coding", out var siteCodings) && siteCodings.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var sc in siteCodings.EnumerateArray())
                        {
                            var system = sc.GetStringOrEmpty("system");
                            if (system.Contains("tooth") || system.Contains("ada") || system.Contains("fdi"))
                                proc.ToothNumbers.Add(sc.GetStringOrEmpty("code"));
                        }
                    }
                }
            }

            procedures.Add(proc);
        }

        return procedures;
    }

    private static JsonElement? GetFirstEntry(JsonElement bundle)
    {
        if (bundle.TryGetProperty("entry", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in entries.EnumerateArray()) return e;
        }
        return null;
    }
}

internal static class JsonElementExtensions
{
    public static string GetStringOrEmpty(this JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString() ?? string.Empty;
        return string.Empty;
    }
}
