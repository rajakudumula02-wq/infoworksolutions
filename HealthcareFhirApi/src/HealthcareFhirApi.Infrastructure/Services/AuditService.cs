namespace HealthcareFhirApi.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly IFhirResourceRepository<AuditEvent> _repo;

    public AuditService(IFhirResourceRepository<AuditEvent> repo)
    {
        _repo = repo;
    }

    public async SystemTask RecordAsync(AuditContext context, CancellationToken ct = default)
    {
        var auditEvent = new AuditEvent
        {
            Type     = new Coding("http://terminology.hl7.org/CodeSystem/audit-event-type", "rest"),
            Action   = MapAction(context.Action),
            Recorded = context.Timestamp,
            Agent    = new List<AuditEvent.AgentComponent>
            {
                new()
                {
                    Who       = new ResourceReference($"Client/{context.ClientId}"),
                    Requestor = true
                }
            },
            Entity = new List<AuditEvent.EntityComponent>
            {
                new()
                {
                    What = new ResourceReference(
                        context.ResourceId is not null
                            ? $"{context.ResourceType}/{context.ResourceId}"
                            : context.ResourceType),
                    Type = new Coding("http://terminology.hl7.org/CodeSystem/audit-entity-type", "2")
                }
            }
        };

        await _repo.CreateAsync(auditEvent, ct);
    }

    public async System.Threading.Tasks.Task<Bundle> QueryAsync(
        string clientId, string patientId, CancellationToken ct = default)
    {
        var parameters = new SearchParameters(
            Filters: new Dictionary<string, string?>
            {
                ["patient"] = patientId,
                ["agent"]   = clientId
            },
            Skip: 0,
            Take: 100,
            SortField: "date",
            SortDescending: true,
            Include: Array.Empty<string>(),
            RevInclude: Array.Empty<string>());

        var result = await _repo.SearchAsync(parameters, ct);
        return BuildBundle(result.Items);
    }

    private static AuditEvent.AuditEventAction MapAction(string action) => action switch
    {
        "read"   => AuditEvent.AuditEventAction.R,
        "search" => AuditEvent.AuditEventAction.E,
        "create" => AuditEvent.AuditEventAction.C,
        "update" => AuditEvent.AuditEventAction.U,
        _        => AuditEvent.AuditEventAction.E
    };

    private static Bundle BuildBundle(IReadOnlyList<AuditEvent> items) => new()
    {
        Type  = Bundle.BundleType.Searchset,
        Total = items.Count,
        Entry = items.Select(e => new Bundle.EntryComponent { Resource = e }).ToList()
    };
}
