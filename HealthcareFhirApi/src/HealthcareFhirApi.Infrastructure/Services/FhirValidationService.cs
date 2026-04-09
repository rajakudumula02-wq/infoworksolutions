namespace HealthcareFhirApi.Infrastructure.Services;

public class FhirValidationService : IFhirValidationService
{
    // Profile URL constants
    private const string UsCorePatient       = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient";
    private const string UsCoreOrganization  = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-organization";
    private const string UsCorePractitioner  = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-practitioner";
    private const string UsCoreLocation      = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-location";
    private const string UsCoreCoverage      = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-coverage";
    private const string UsCoreEncounter     = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-encounter";
    private const string UsCoreRelatedPerson = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-relatedperson";
    private const string CarinBbEob          = "http://hl7.org/fhir/us/carin-bb/StructureDefinition/C4BB-ExplanationOfBenefit";
    private const string FhirR4Claim         = "http://hl7.org/fhir/StructureDefinition/Claim";
    private const string PdexPlanNetPrefix   = "http://hl7.org/fhir/us/davinci-pdex-plan-net/StructureDefinition/";

    public System.Threading.Tasks.Task<OperationOutcome> ValidateAsync(
        Resource resource,
        string profileUrl,
        CancellationToken ct = default)
    {
        var outcome = new OperationOutcome { Issue = new List<OperationOutcome.IssueComponent>() };

        // 1. Verify resourceType is not null/empty
        if (string.IsNullOrWhiteSpace(resource.TypeName))
        {
            outcome.Issue.Add(new OperationOutcome.IssueComponent
            {
                Severity    = OperationOutcome.IssueSeverity.Error,
                Code        = OperationOutcome.IssueType.Required,
                Diagnostics = "Resource type is missing or empty."
            });
            return System.Threading.Tasks.Task.FromResult(outcome);
        }

        // 2. Profile-specific structural checks
        ValidateByProfile(resource, profileUrl, outcome);

        return System.Threading.Tasks.Task.FromResult(outcome);
    }

    public bool IsValid(OperationOutcome outcome) =>
        !outcome.Issue.Any(i =>
            i.Severity == OperationOutcome.IssueSeverity.Error ||
            i.Severity == OperationOutcome.IssueSeverity.Fatal);

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static void ValidateByProfile(Resource resource, string profileUrl, OperationOutcome outcome)
    {
        switch (profileUrl)
        {
            case UsCorePatient:
                ValidatePatient(resource, outcome);
                break;

            case UsCoreOrganization:
                ValidateOrganization(resource, outcome);
                break;

            case UsCorePractitioner:
                ValidatePractitioner(resource, outcome);
                break;

            case UsCoreLocation:
                ValidateLocation(resource, outcome);
                break;

            case UsCoreCoverage:
                ValidateCoverage(resource, outcome);
                break;

            case UsCoreEncounter:
                ValidateEncounter(resource, outcome);
                break;

            case UsCoreRelatedPerson:
                ValidateRelatedPerson(resource, outcome);
                break;

            case CarinBbEob:
                ValidateExplanationOfBenefit(resource, outcome);
                break;

            case FhirR4Claim:
                ValidateClaim(resource, outcome);
                break;

            default:
                if (profileUrl.StartsWith(PdexPlanNetPrefix, StringComparison.OrdinalIgnoreCase))
                    ValidatePdexPlanNet(resource, profileUrl, outcome);
                else
                    ValidateByResourceType(resource, outcome);
                break;
        }
    }

    private static void ValidatePatient(Resource resource, OperationOutcome outcome)
    {
        if (resource is not Patient patient)
        {
            AddTypeMismatch(outcome, "Patient", resource.TypeName);
            return;
        }

        // US Core: identifier 1..* (SHALL)
        if (patient.Identifier == null || patient.Identifier.Count == 0)
            AddMissingField(outcome, "Patient.identifier");

        // US Core: identifier.system 1..1 and identifier.value 1..1
        if (patient.Identifier != null)
        {
            foreach (var id in patient.Identifier)
            {
                if (string.IsNullOrWhiteSpace(id.System))
                    AddMissingField(outcome, "Patient.identifier.system");
                if (string.IsNullOrWhiteSpace(id.Value))
                    AddMissingField(outcome, "Patient.identifier.value");
            }
        }

        // US Core: name 1..* (SHALL) with given and/or family
        if (patient.Name == null || patient.Name.Count == 0)
            AddMissingField(outcome, "Patient.name");
        else
        {
            foreach (var name in patient.Name)
            {
                if (string.IsNullOrWhiteSpace(name.Family) &&
                    (name.Given == null || !name.Given.Any(g => !string.IsNullOrWhiteSpace(g))))
                    AddMissingField(outcome, "Patient.name (requires family and/or given per us-core-6)");
            }
        }

        // US Core Must Support: gender (FHIR base cardinality 0..1, but CMS requires)
        if (patient.Gender == null)
            AddMissingField(outcome, "Patient.gender");

        // US Core Must Support: birthDate
        if (string.IsNullOrWhiteSpace(patient.BirthDate))
            AddMissingField(outcome, "Patient.birthDate");
    }

    private static void ValidateOrganization(Resource resource, OperationOutcome outcome)
    {
        if (resource is not Organization org)
        {
            AddTypeMismatch(outcome, "Organization", resource.TypeName);
            return;
        }

        // US Core: name 1..1 (SHALL)
        if (string.IsNullOrWhiteSpace(org.Name))
            AddMissingField(outcome, "Organization.name");

        // US Core: active 1..1 (SHALL)
        if (org.Active == null)
            AddMissingField(outcome, "Organization.active");

        // US Core Must Support: address
        if (org.Address == null || org.Address.Count == 0)
            AddMissingField(outcome, "Organization.address");

        // US Core Must Support: identifier (NPI recommended)
        if (org.Identifier == null || org.Identifier.Count == 0)
            AddMissingField(outcome, "Organization.identifier");
    }

    private static void ValidatePractitioner(Resource resource, OperationOutcome outcome)
    {
        if (resource is not Practitioner practitioner)
        {
            AddTypeMismatch(outcome, "Practitioner", resource.TypeName);
            return;
        }

        // US Core: identifier 1..* (SHALL, NPI required for US)
        if (practitioner.Identifier == null || practitioner.Identifier.Count == 0)
            AddMissingField(outcome, "Practitioner.identifier");

        // US Core: name 1..* (SHALL) with family 1..1
        if (practitioner.Name == null || practitioner.Name.Count == 0)
            AddMissingField(outcome, "Practitioner.name");
        else
        {
            foreach (var name in practitioner.Name)
            {
                if (string.IsNullOrWhiteSpace(name.Family))
                    AddMissingField(outcome, "Practitioner.name.family");
            }
        }
    }

    private static void ValidateLocation(Resource resource, OperationOutcome outcome)
    {
        if (resource is not Location location)
        {
            AddTypeMismatch(outcome, "Location", resource.TypeName);
            return;
        }

        // US Core: name 1..1 (SHALL)
        if (string.IsNullOrWhiteSpace(location.Name))
            AddMissingField(outcome, "Location.name");

        // US Core Must Support: status
        if (location.Status == null)
            AddMissingField(outcome, "Location.status");

        // US Core Must Support: address
        if (location.Address == null)
            AddMissingField(outcome, "Location.address");
    }

    private static void ValidateCoverage(Resource resource, OperationOutcome outcome)
    {
        if (resource is not Coverage coverage)
        {
            AddTypeMismatch(outcome, "Coverage", resource.TypeName);
            return;
        }

        // US Core: status 1..1 (SHALL)
        if (coverage.Status == null)
            AddMissingField(outcome, "Coverage.status");

        // US Core: beneficiary 1..1 (SHALL)
        if (coverage.Beneficiary == null)
            AddMissingField(outcome, "Coverage.beneficiary");

        // US Core: payor 1..* (SHALL)
        if (coverage.Payor == null || coverage.Payor.Count == 0)
            AddMissingField(outcome, "Coverage.payor");

        // US Core Must Support: type
        if (coverage.Type == null)
            AddMissingField(outcome, "Coverage.type");

        // US Core Must Support: subscriber
        if (coverage.Subscriber == null)
            AddMissingField(outcome, "Coverage.subscriber");

        // US Core Must Support: subscriberId
        if (string.IsNullOrWhiteSpace(coverage.SubscriberId))
            AddMissingField(outcome, "Coverage.subscriberId");

        // US Core Must Support: relationship
        if (coverage.Relationship == null)
            AddMissingField(outcome, "Coverage.relationship");

        // US Core Must Support: period
        if (coverage.Period == null)
            AddMissingField(outcome, "Coverage.period");
    }

    private static void ValidateEncounter(Resource resource, OperationOutcome outcome)
    {
        if (resource is not Encounter encounter)
        {
            AddTypeMismatch(outcome, "Encounter", resource.TypeName);
            return;
        }

        // US Core: status 1..1 (SHALL)
        if (encounter.Status == null)
            AddMissingField(outcome, "Encounter.status");

        // US Core: class 1..1 (SHALL)
        if (encounter.Class == null)
            AddMissingField(outcome, "Encounter.class");

        // US Core: type 1..* (SHALL)
        if (encounter.Type == null || encounter.Type.Count == 0)
            AddMissingField(outcome, "Encounter.type");

        // US Core: subject 1..1 (SHALL, reference to Patient)
        if (encounter.Subject == null)
            AddMissingField(outcome, "Encounter.subject");

        // US Core Must Support: participant
        if (encounter.Participant == null || encounter.Participant.Count == 0)
            AddMissingField(outcome, "Encounter.participant");

        // US Core Must Support: period
        if (encounter.Period == null)
            AddMissingField(outcome, "Encounter.period");

        // US Core Must Support: location
        if (encounter.Location == null || encounter.Location.Count == 0)
            AddMissingField(outcome, "Encounter.location");
    }

    private static void ValidateRelatedPerson(Resource resource, OperationOutcome outcome)
    {
        if (resource is not RelatedPerson relatedPerson)
        {
            AddTypeMismatch(outcome, "RelatedPerson", resource.TypeName);
            return;
        }

        // US Core: patient 1..1 (SHALL)
        if (relatedPerson.Patient == null)
            AddMissingField(outcome, "RelatedPerson.patient");

        // US Core: active 1..1 (SHALL)
        if (relatedPerson.Active == null)
            AddMissingField(outcome, "RelatedPerson.active");

        // US Core Must Support: relationship
        if (relatedPerson.Relationship == null || relatedPerson.Relationship.Count == 0)
            AddMissingField(outcome, "RelatedPerson.relationship");

        // US Core Must Support: name
        if (relatedPerson.Name == null || relatedPerson.Name.Count == 0)
            AddMissingField(outcome, "RelatedPerson.name");
    }

    private static void ValidateExplanationOfBenefit(Resource resource, OperationOutcome outcome)
    {
        if (resource is not ExplanationOfBenefit eob)
        {
            AddTypeMismatch(outcome, "ExplanationOfBenefit", resource.TypeName);
            return;
        }

        // CARIN BB: status 1..1 (SHALL)
        if (eob.Status == null)
            AddMissingField(outcome, "ExplanationOfBenefit.status");

        // CARIN BB: type 1..1 (SHALL)
        if (eob.Type == null)
            AddMissingField(outcome, "ExplanationOfBenefit.type");

        // CARIN BB: use 1..1 (SHALL)
        if (eob.Use == null)
            AddMissingField(outcome, "ExplanationOfBenefit.use");

        // CARIN BB: patient 1..1 (SHALL)
        if (eob.Patient == null)
            AddMissingField(outcome, "ExplanationOfBenefit.patient");

        // CARIN BB: billablePeriod 1..1 (SHALL)
        if (eob.BillablePeriod == null)
            AddMissingField(outcome, "ExplanationOfBenefit.billablePeriod");

        // CARIN BB: insurer 1..1 (SHALL)
        if (eob.Insurer == null)
            AddMissingField(outcome, "ExplanationOfBenefit.insurer");

        // CARIN BB: provider 1..1 (SHALL)
        if (eob.Provider == null)
            AddMissingField(outcome, "ExplanationOfBenefit.provider");

        // CARIN BB: outcome 1..1 (SHALL)
        if (eob.Outcome == null)
            AddMissingField(outcome, "ExplanationOfBenefit.outcome");

        // CARIN BB: insurance 1..* (SHALL)
        if (eob.Insurance == null || eob.Insurance.Count == 0)
            AddMissingField(outcome, "ExplanationOfBenefit.insurance");
    }

    private static void ValidateClaim(Resource resource, OperationOutcome outcome)
    {
        if (resource is not Claim claim)
        {
            AddTypeMismatch(outcome, "Claim", resource.TypeName);
            return;
        }

        // FHIR R4: status 1..1 (SHALL)
        if (claim.Status == null)
            AddMissingField(outcome, "Claim.status");

        // FHIR R4: type 1..1 (SHALL)
        if (claim.Type == null)
            AddMissingField(outcome, "Claim.type");

        // FHIR R4: use 1..1 (SHALL)
        if (claim.Use == null)
            AddMissingField(outcome, "Claim.use");

        // FHIR R4: patient 1..1 (SHALL)
        if (claim.Patient == null)
            AddMissingField(outcome, "Claim.patient");

        // FHIR R4: created 1..1 (SHALL)
        if (string.IsNullOrWhiteSpace(claim.Created))
            AddMissingField(outcome, "Claim.created");

        // FHIR R4: provider 1..1 (SHALL)
        if (claim.Provider == null)
            AddMissingField(outcome, "Claim.provider");

        // FHIR R4: priority 1..1 (SHALL)
        if (claim.Priority == null)
            AddMissingField(outcome, "Claim.priority");

        // FHIR R4: insurance 1..* (SHALL)
        if (claim.Insurance == null || claim.Insurance.Count == 0)
            AddMissingField(outcome, "Claim.insurance");
    }

    private static void ValidatePdexPlanNet(Resource resource, string profileUrl, OperationOutcome outcome)
    {
        // Basic structural check: ensure the resource type is not null (already checked above).
        // Profile-specific rules for PDEX Plan Net are not enforced without a full profile validator.
        if (string.IsNullOrWhiteSpace(resource.TypeName))
            AddMissingField(outcome, "resourceType");
    }

    /// <summary>
    /// Fallback validator for resources not matched by a specific profile URL.
    /// Validates mandatory fields based on FHIR R4 base spec and US Core profiles.
    /// </summary>
    private static void ValidateByResourceType(Resource resource, OperationOutcome outcome)
    {
        switch (resource)
        {
            case AllergyIntolerance ai:
                ValidateAllergyIntolerance(ai, outcome);
                break;
            case Condition cond:
                ValidateCondition(cond, outcome);
                break;
            case Consent consent:
                ValidateConsent(consent, outcome);
                break;
            case DiagnosticReport dr:
                ValidateDiagnosticReport(dr, outcome);
                break;
            case Immunization imm:
                ValidateImmunization(imm, outcome);
                break;
            case MedicationRequest mr:
                ValidateMedicationRequest(mr, outcome);
                break;
            case Procedure proc:
                ValidateProcedure(proc, outcome);
                break;
            case PractitionerRole pr:
                ValidatePractitionerRole(pr, outcome);
                break;
        }
    }

    // ── AllergyIntolerance (US Core) ─────────────────────────────────────────

    private static void ValidateAllergyIntolerance(AllergyIntolerance ai, OperationOutcome outcome)
    {
        // US Core: clinicalStatus 1..1 (SHALL, if verificationStatus is not entered-in-error)
        if (ai.ClinicalStatus == null)
            AddMissingField(outcome, "AllergyIntolerance.clinicalStatus");

        // US Core: code 1..1 (SHALL)
        if (ai.Code == null)
            AddMissingField(outcome, "AllergyIntolerance.code");

        // US Core: patient 1..1 (SHALL)
        if (ai.Patient == null)
            AddMissingField(outcome, "AllergyIntolerance.patient");
    }

    // ── Condition (US Core Encounter Diagnosis / Problems) ───────────────────

    private static void ValidateCondition(Condition cond, OperationOutcome outcome)
    {
        // US Core: clinicalStatus 1..1 (SHALL, if verificationStatus is not entered-in-error)
        if (cond.ClinicalStatus == null && cond.VerificationStatus?.Coding?.Any(c => c.Code == "entered-in-error") != true)
            AddMissingField(outcome, "Condition.clinicalStatus");

        // US Core: category 1..* (SHALL)
        if (cond.Category == null || cond.Category.Count == 0)
            AddMissingField(outcome, "Condition.category");

        // US Core: code 1..1 (SHALL)
        if (cond.Code == null)
            AddMissingField(outcome, "Condition.code");

        // US Core: subject 1..1 (SHALL)
        if (cond.Subject == null)
            AddMissingField(outcome, "Condition.subject");
    }

    // ── Consent ──────────────────────────────────────────────────────────────

    private static void ValidateConsent(Consent consent, OperationOutcome outcome)
    {
        // FHIR R4: status 1..1 (SHALL)
        if (consent.Status == null)
            AddMissingField(outcome, "Consent.status");

        // FHIR R4: scope 1..1 (SHALL)
        if (consent.Scope == null)
            AddMissingField(outcome, "Consent.scope");

        // FHIR R4: category 1..* (SHALL)
        if (consent.Category == null || consent.Category.Count == 0)
            AddMissingField(outcome, "Consent.category");

        // FHIR R4: patient (SHALL for healthcare consent)
        if (consent.Patient == null)
            AddMissingField(outcome, "Consent.patient");
    }

    // ── DiagnosticReport (US Core) ───────────────────────────────────────────

    private static void ValidateDiagnosticReport(DiagnosticReport dr, OperationOutcome outcome)
    {
        // US Core: status 1..1 (SHALL)
        if (dr.Status == null)
            AddMissingField(outcome, "DiagnosticReport.status");

        // US Core: category 1..* (SHALL)
        if (dr.Category == null || dr.Category.Count == 0)
            AddMissingField(outcome, "DiagnosticReport.category");

        // US Core: code 1..1 (SHALL)
        if (dr.Code == null)
            AddMissingField(outcome, "DiagnosticReport.code");

        // US Core: subject 1..1 (SHALL)
        if (dr.Subject == null)
            AddMissingField(outcome, "DiagnosticReport.subject");

        // US Core Must Support: effective[x]
        if (dr.Effective == null)
            AddMissingField(outcome, "DiagnosticReport.effective[x]");
    }

    // ── Immunization (US Core) ───────────────────────────────────────────────

    private static void ValidateImmunization(Immunization imm, OperationOutcome outcome)
    {
        // US Core: status 1..1 (SHALL)
        if (imm.Status == null)
            AddMissingField(outcome, "Immunization.status");

        // US Core: vaccineCode 1..1 (SHALL)
        if (imm.VaccineCode == null)
            AddMissingField(outcome, "Immunization.vaccineCode");

        // US Core: patient 1..1 (SHALL)
        if (imm.Patient == null)
            AddMissingField(outcome, "Immunization.patient");

        // US Core: occurrence[x] 1..1 (SHALL)
        if (imm.Occurrence == null)
            AddMissingField(outcome, "Immunization.occurrence[x]");

        // US Core Must Support: primarySource
        if (imm.PrimarySource == null)
            AddMissingField(outcome, "Immunization.primarySource");
    }

    // ── MedicationRequest (US Core) ──────────────────────────────────────────

    private static void ValidateMedicationRequest(MedicationRequest mr, OperationOutcome outcome)
    {
        // US Core: status 1..1 (SHALL)
        if (mr.Status == null)
            AddMissingField(outcome, "MedicationRequest.status");

        // US Core: intent 1..1 (SHALL)
        if (mr.Intent == null)
            AddMissingField(outcome, "MedicationRequest.intent");

        // US Core: medication[x] 1..1 (SHALL)
        if (mr.Medication == null)
            AddMissingField(outcome, "MedicationRequest.medication[x]");

        // US Core: subject 1..1 (SHALL)
        if (mr.Subject == null)
            AddMissingField(outcome, "MedicationRequest.subject");

        // US Core Must Support: authoredOn
        if (string.IsNullOrWhiteSpace(mr.AuthoredOn))
            AddMissingField(outcome, "MedicationRequest.authoredOn");

        // US Core Must Support: requester
        if (mr.Requester == null)
            AddMissingField(outcome, "MedicationRequest.requester");
    }

    // ── Procedure (US Core) ──────────────────────────────────────────────────

    private static void ValidateProcedure(Procedure proc, OperationOutcome outcome)
    {
        // US Core: status 1..1 (SHALL)
        if (proc.Status == null)
            AddMissingField(outcome, "Procedure.status");

        // US Core: code 1..1 (SHALL)
        if (proc.Code == null)
            AddMissingField(outcome, "Procedure.code");

        // US Core: subject 1..1 (SHALL)
        if (proc.Subject == null)
            AddMissingField(outcome, "Procedure.subject");

        // US Core Must Support: performed[x]
        if (proc.Performed == null)
            AddMissingField(outcome, "Procedure.performed[x]");
    }

    // ── PractitionerRole (US Core) ───────────────────────────────────────────

    private static void ValidatePractitionerRole(PractitionerRole pr, OperationOutcome outcome)
    {
        // US Core Must Support: practitioner
        if (pr.Practitioner == null)
            AddMissingField(outcome, "PractitionerRole.practitioner");

        // US Core Must Support: organization
        if (pr.Organization == null)
            AddMissingField(outcome, "PractitionerRole.organization");

        // US Core Must Support: code (role)
        if (pr.Code == null || pr.Code.Count == 0)
            AddMissingField(outcome, "PractitionerRole.code");

        // US Core Must Support: specialty
        if (pr.Specialty == null || pr.Specialty.Count == 0)
            AddMissingField(outcome, "PractitionerRole.specialty");

        // US Core Must Support: location
        if (pr.Location == null || pr.Location.Count == 0)
            AddMissingField(outcome, "PractitionerRole.location");

        // US Core Must Support: telecom
        if (pr.Telecom == null || pr.Telecom.Count == 0)
            AddMissingField(outcome, "PractitionerRole.telecom");
    }

    // -------------------------------------------------------------------------
    // Issue factory helpers
    // -------------------------------------------------------------------------

    private static void AddMissingField(OperationOutcome outcome, string expression)
    {
        outcome.Issue.Add(new OperationOutcome.IssueComponent
        {
            Severity    = OperationOutcome.IssueSeverity.Error,
            Code        = OperationOutcome.IssueType.Required,
            Diagnostics = $"Required field '{expression}' is missing or empty.",
            Expression  = new[] { expression }
        });
    }

    private static void AddTypeMismatch(OperationOutcome outcome, string expected, string actual)
    {
        outcome.Issue.Add(new OperationOutcome.IssueComponent
        {
            Severity    = OperationOutcome.IssueSeverity.Error,
            Code        = OperationOutcome.IssueType.Invalid,
            Diagnostics = $"Expected resource type '{expected}' but received '{actual}'."
        });
    }
}
