namespace HealthcareFhirApi.Infrastructure.Services;

public class CapabilityStatementBuilder : ICapabilityStatementBuilder
{
    private static readonly string[] SupportedFormats =
        ["application/fhir+json", "application/fhir+xml"];

    private static readonly string[] ResourceTypes =
    [
        "Patient", "Organization", "Practitioner", "PractitionerRole",
        "Claim", "ClaimResponse", "ExplanationOfBenefit", "Coverage",
        "Encounter", "Location", "RelatedPerson", "AuditEvent",
        "Bundle", "Parameters"
    ];

    public CapabilityStatement Build() => new()
    {
        Status = PublicationStatus.Active,
        Date = "2024-01-01",
        Kind = CapabilityStatementKind.Instance,
        FhirVersion = FHIRVersion.N4_0_1,
        Format = SupportedFormats,
        Rest = BuildRestComponents()
    };

    private static List<CapabilityStatement.RestComponent> BuildRestComponents() =>
    [
        new CapabilityStatement.RestComponent
        {
            Mode = CapabilityStatement.RestfulCapabilityMode.Server,
            Resource = ResourceTypes
                .Select(BuildResourceComponent)
                .ToList()
        }
    ];

    private static CapabilityStatement.ResourceComponent BuildResourceComponent(string resourceType) =>
        new()
        {
            TypeElement = new Code(resourceType),
            Interaction =
            [
                new CapabilityStatement.ResourceInteractionComponent
                    { Code = CapabilityStatement.TypeRestfulInteraction.Read },
                new CapabilityStatement.ResourceInteractionComponent
                    { Code = CapabilityStatement.TypeRestfulInteraction.SearchType },
                new CapabilityStatement.ResourceInteractionComponent
                    { Code = CapabilityStatement.TypeRestfulInteraction.Create },
                new CapabilityStatement.ResourceInteractionComponent
                    { Code = CapabilityStatement.TypeRestfulInteraction.Update }
            ]
        };
}
