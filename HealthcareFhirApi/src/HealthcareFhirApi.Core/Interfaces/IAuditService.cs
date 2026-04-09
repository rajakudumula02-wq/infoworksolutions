using Hl7.Fhir.Model;
using HealthcareFhirApi.Core.Models;

namespace HealthcareFhirApi.Core.Interfaces;

public interface IAuditService
{
    System.Threading.Tasks.Task RecordAsync(AuditContext context, CancellationToken ct = default);
    System.Threading.Tasks.Task<Bundle> QueryAsync(string clientId, string patientId, CancellationToken ct = default);
}
