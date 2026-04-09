# Requirements Document

## Introduction

This feature delivers a multi-tenant, CMS-compliant HL7 FHIR R4 API platform designed as a sellable product for insurance companies (payers). The platform exposes healthcare data across twenty-one domains: Patient, Provider, Practitioner, Claim, Explanation of Benefits (EOB), Payer-to-Payer data exchange, Preauthorization, Location, Year-End Reporting, Patient Role (RelatedPerson), Encounter, Medical Knowledge (Terminology), Provider Directory, Coverage, Clinical Data (Condition, AllergyIntolerance, MedicationRequest, Immunization, Procedure, DiagnosticReport), Bulk Data Export, SMART App Launch Configuration, Consent Management, Multi-Tenant Architecture, Admin Portal, and CMS Compliance Reporting. All APIs conform to the HL7 FHIR R4 specification and support standard FHIR RESTful interactions (read, search, create, update). The platform supports CMS-9115-F (Patient Access API), CMS-0057-F (Payer-to-Payer), SMART App Launch Framework v2.0, and FHIR Bulk Data Access IG. Each payer tenant receives isolated data storage, tenant-specific configuration, and dedicated compliance reporting. The platform is intended for use by insurance companies, partner payers, third-party SMART apps, and authorized consumers.

## Glossary

- **FHIR_Server**: The in-house HL7 FHIR R4-compliant API server hosting all resource endpoints
- **Patient_API**: The FHIR endpoint managing Patient resources (FHIR resource type: `Patient`)
- **Provider_API**: The FHIR endpoint managing Organization resources representing healthcare providers (FHIR resource type: `Organization`)
- **Practitioner_API**: The FHIR endpoint managing Practitioner and PractitionerRole resources
- **Claim_API**: The FHIR endpoint managing Claim and ClaimResponse resources
- **EOB_API**: The FHIR endpoint managing ExplanationOfBenefit resources
- **Payer_to_Payer_API**: The FHIR endpoint supporting inter-payer data exchange per CMS interoperability rules
- **Preauth_API**: The FHIR endpoint managing prior authorization using the FHIR Prior Authorization Support IG
- **Client**: Any authorized application or system consuming the FHIR APIs
- **SMART_Auth**: The SMART on FHIR OAuth 2.0 authorization framework used to authenticate and authorize Clients
- **CapabilityStatement**: The FHIR metadata resource describing supported operations and resource types
- **Bundle**: A FHIR container resource used to return collections of resources in search results
- **OperationOutcome**: A FHIR resource used to convey error, warning, or informational messages
- **Location_API**: The FHIR endpoint managing Location resources representing physical sites where healthcare services are provided (FHIR resource type: `Location`)
- **Report_API**: The reporting endpoint that generates annual summary reports for patients, claims, and EOBs
- **PatientRole_API**: The FHIR endpoint managing RelatedPerson resources representing roles a person plays in relation to a patient (FHIR resource type: `RelatedPerson`)
- **Encounter_API**: The FHIR endpoint managing Encounter resources representing patient visits, admissions, or interactions with healthcare services (FHIR resource type: `Encounter`)
- **Terminology_API**: The FHIR terminology endpoint supporting CodeSystem and ValueSet operations including `$lookup`, `$validate-code`, `$expand`, and ConceptMap translations
- **ProviderDirectory_API**: The FHIR-based provider directory endpoint supporting search and retrieval of Practitioner, Organization, Location, and related resources per the Da Vinci PDEX Plan Net Provider Directory IG
- **Coverage_API**: The FHIR endpoint managing Coverage resources representing a patient's insurance coverage (FHIR resource type: `Coverage`)
- **ConceptMap**: A FHIR resource that defines mappings between code systems or value sets
- **SNOMED_CT**: Systematized Nomenclature of Medicine Clinical Terms, a clinical terminology code system
- **LOINC**: Logical Observation Identifiers Names and Codes, a standard for identifying medical laboratory observations
- **ICD-10**: International Classification of Diseases, 10th Revision, a medical coding system for diagnoses and procedures
- **CPT**: Current Procedural Terminology, a medical code set for procedures and services
- **RxNorm**: A normalized naming system for clinical drugs maintained by the National Library of Medicine
- **PDEX_Plan_Net**: The Da Vinci PDEX Plan Net Provider Directory Implementation Guide defining FHIR profiles for provider directory data
- **Condition_API**: The FHIR endpoint managing Condition resources representing patient diagnoses and health conditions (FHIR resource type: `Condition`)
- **AllergyIntolerance_API**: The FHIR endpoint managing AllergyIntolerance resources representing patient allergies and intolerances (FHIR resource type: `AllergyIntolerance`)
- **MedicationRequest_API**: The FHIR endpoint managing MedicationRequest resources representing medication orders and prescriptions (FHIR resource type: `MedicationRequest`)
- **Immunization_API**: The FHIR endpoint managing Immunization resources representing vaccination records (FHIR resource type: `Immunization`)
- **Procedure_API**: The FHIR endpoint managing Procedure resources representing clinical procedures performed on patients (FHIR resource type: `Procedure`)
- **DiagnosticReport_API**: The FHIR endpoint managing DiagnosticReport resources representing diagnostic test results (FHIR resource type: `DiagnosticReport`)
- **BulkExport_API**: The FHIR endpoint supporting the Bulk Data Access IG `$export` operation for large-scale data extraction in NDJSON format
- **SMART_Configuration_Endpoint**: The `/.well-known/smart-configuration` endpoint that publishes SMART App Launch metadata as a JSON document
- **Consent_API**: The FHIR endpoint managing Consent resources representing patient consent decisions for data sharing (FHIR resource type: `Consent`)
- **Tenant**: An individual insurance company (payer organization) onboarded to the platform, with isolated data storage and configuration
- **Tenant_Management_API**: The administrative API for provisioning, configuring, and managing Tenant organizations on the platform
- **Admin_Portal_API**: The administrative API providing tenant management, API usage metrics, rate limiting configuration, and API key management
- **Compliance_Report_API**: The reporting endpoint that generates CMS attestation and compliance reports for API availability, response times, and error rates
- **CMS-9115-F**: The CMS Interoperability and Patient Access Final Rule requiring payers to expose Patient Access APIs
- **CMS-0057-F**: The CMS Interoperability and Prior Authorization Final Rule requiring Payer-to-Payer data exchange with patient consent
- **NDJSON**: Newline Delimited JSON, the output format required by the FHIR Bulk Data Access IG
- **SMART_Backend_Services**: The SMART on FHIR authorization profile for server-to-server (backend) authentication using client credentials and signed JWTs

---

## Requirements

### Requirement 1: FHIR Server Foundation

**User Story:** As a platform engineer, I want a standards-compliant FHIR R4 server base, so that all APIs share consistent behavior, versioning, and metadata discovery.

#### Acceptance Criteria

1. THE FHIR_Server SHALL expose a `/metadata` endpoint returning a valid HL7 FHIR R4 CapabilityStatement
2. THE FHIR_Server SHALL support `application/fhir+json` as the default content type for all requests and responses
3. THE FHIR_Server SHALL support `application/fhir+xml` as an alternative content type negotiated via the `Accept` header
4. WHEN a Client sends a request with an unsupported content type, THE FHIR_Server SHALL return an HTTP 415 status and an OperationOutcome resource
5. THE FHIR_Server SHALL include a `fhirVersion` field of `4.0.1` in all CapabilityStatement responses
6. WHEN a Client sends a request to an unknown endpoint, THE FHIR_Server SHALL return an HTTP 404 status and an OperationOutcome resource
7. THE FHIR_Server SHALL assign a unique logical `id` to every resource upon creation

---

### Requirement 2: Authentication and Authorization

**User Story:** As a security engineer, I want all API access controlled via SMART on FHIR, so that only authorized Clients can read or modify protected health information.

#### Acceptance Criteria

1. THE FHIR_Server SHALL require SMART_Auth OAuth 2.0 bearer tokens for all resource endpoints
2. WHEN a Client presents an expired or invalid bearer token, THE FHIR_Server SHALL return an HTTP 401 status and an OperationOutcome resource
3. WHEN a Client requests a resource outside its granted SMART_Auth scopes, THE FHIR_Server SHALL return an HTTP 403 status and an OperationOutcome resource
4. THE FHIR_Server SHALL support SMART on FHIR launch context scopes including `patient/*.read`, `user/*.read`, and `system/*.read`
5. THE FHIR_Server SHALL support PKCE (Proof Key for Code Exchange) in the SMART_Auth authorization code flow
6. WHEN a Client authenticates successfully, THE FHIR_Server SHALL return an access token with an expiry of no more than 3600 seconds

---

### Requirement 3: Patient API

**User Story:** As a healthcare application developer, I want to retrieve and manage Patient resources in FHIR R4 format, so that I can display and update patient demographic data.

#### Acceptance Criteria

1. THE Patient_API SHALL support the FHIR `read` interaction: `GET /Patient/{id}`
2. THE Patient_API SHALL support the FHIR `search` interaction: `GET /Patient` with search parameters `_id`, `identifier`, `name`, `birthdate`, `gender`, and `address-postalcode`
3. THE Patient_API SHALL support the FHIR `create` interaction: `POST /Patient`
4. THE Patient_API SHALL support the FHIR `update` interaction: `PUT /Patient/{id}`
5. WHEN a search returns multiple results, THE Patient_API SHALL return a FHIR Bundle of type `searchset`
6. WHEN a Patient resource is created or updated, THE Patient_API SHALL validate the resource against the US Core Patient profile
7. IF a Patient resource fails US Core profile validation, THEN THE Patient_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
8. WHEN a Client requests a Patient resource that does not exist, THE Patient_API SHALL return an HTTP 404 status and an OperationOutcome resource
9. THE Patient_API SHALL include at least one `identifier` element with a system URI in every Patient resource response

---

### Requirement 4: Provider API

**User Story:** As a healthcare application developer, I want to retrieve and manage Organization resources representing healthcare providers, so that I can associate patients and claims with the correct provider organizations.

#### Acceptance Criteria

1. THE Provider_API SHALL support the FHIR `read` interaction: `GET /Organization/{id}`
2. THE Provider_API SHALL support the FHIR `search` interaction: `GET /Organization` with search parameters `_id`, `identifier`, `name`, `type`, and `address-state`
3. THE Provider_API SHALL support the FHIR `create` interaction: `POST /Organization`
4. THE Provider_API SHALL support the FHIR `update` interaction: `PUT /Organization/{id}`
5. WHEN a search returns multiple results, THE Provider_API SHALL return a FHIR Bundle of type `searchset`
6. WHEN an Organization resource is created or updated, THE Provider_API SHALL validate the resource against the US Core Organization profile
7. IF an Organization resource fails US Core profile validation, THEN THE Provider_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
8. THE Provider_API SHALL include an NPI (National Provider Identifier) `identifier` element in every Organization resource response where the NPI is known

---

### Requirement 5: Practitioner API

**User Story:** As a healthcare application developer, I want to retrieve Practitioner and PractitionerRole resources, so that I can display individual clinician credentials and their organizational affiliations.

#### Acceptance Criteria

1. THE Practitioner_API SHALL support the FHIR `read` interaction: `GET /Practitioner/{id}`
2. THE Practitioner_API SHALL support the FHIR `search` interaction: `GET /Practitioner` with search parameters `_id`, `identifier`, `name`, and `specialty`
3. THE Practitioner_API SHALL support the FHIR `read` interaction: `GET /PractitionerRole/{id}`
4. THE Practitioner_API SHALL support the FHIR `search` interaction: `GET /PractitionerRole` with search parameters `practitioner`, `organization`, `role`, and `specialty`
5. WHEN a Practitioner resource is created or updated, THE Practitioner_API SHALL validate the resource against the US Core Practitioner profile
6. WHEN a PractitionerRole resource is created or updated, THE Practitioner_API SHALL validate the resource against the US Core PractitionerRole profile
7. IF a Practitioner or PractitionerRole resource fails US Core profile validation, THEN THE Practitioner_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
8. THE Practitioner_API SHALL include an NPI `identifier` element in every Practitioner resource response where the NPI is known

---

### Requirement 6: Claim API

**User Story:** As a billing system, I want to submit and retrieve FHIR Claim resources, so that I can manage healthcare claims in a standards-based format.

#### Acceptance Criteria

1. THE Claim_API SHALL support the FHIR `read` interaction: `GET /Claim/{id}`
2. THE Claim_API SHALL support the FHIR `search` interaction: `GET /Claim` with search parameters `_id`, `patient`, `provider`, `status`, and `created`
3. THE Claim_API SHALL support the FHIR `create` interaction: `POST /Claim`
4. THE Claim_API SHALL support the FHIR `read` interaction: `GET /ClaimResponse/{id}`
5. THE Claim_API SHALL support the FHIR `search` interaction: `GET /ClaimResponse` with search parameters `_id`, `patient`, `request`, and `outcome`
6. WHEN a Claim resource is submitted, THE Claim_API SHALL validate the resource against the FHIR R4 Claim base profile
7. IF a Claim resource fails validation, THEN THE Claim_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
8. WHEN a Claim is successfully created, THE Claim_API SHALL return an HTTP 201 status with a `Location` header referencing the new resource
9. THE Claim_API SHALL support Claim `status` values of `active`, `cancelled`, `draft`, and `entered-in-error`

---

### Requirement 7: Explanation of Benefits (EOB) API

**User Story:** As a patient portal application, I want to retrieve ExplanationOfBenefit resources, so that I can display cost and coverage information to members.

#### Acceptance Criteria

1. THE EOB_API SHALL support the FHIR `read` interaction: `GET /ExplanationOfBenefit/{id}`
2. THE EOB_API SHALL support the FHIR `search` interaction: `GET /ExplanationOfBenefit` with search parameters `_id`, `patient`, `provider`, `created`, and `status`
3. WHEN a search returns multiple results, THE EOB_API SHALL return a FHIR Bundle of type `searchset` with pagination links (`self`, `next`, `previous`)
4. THE EOB_API SHALL validate ExplanationOfBenefit resources against the CARIN Blue Button EOB profile
5. IF an ExplanationOfBenefit resource fails CARIN Blue Button profile validation, THEN THE EOB_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
6. THE EOB_API SHALL include `item.adjudication` elements detailing allowed amount, paid amount, and patient responsibility in every ExplanationOfBenefit response
7. WHEN a Client requests an ExplanationOfBenefit for a patient outside the Client's authorized scope, THE EOB_API SHALL return an HTTP 403 status and an OperationOutcome resource

---

### Requirement 8: Payer-to-Payer API

**User Story:** As a payer organization, I want to exchange member clinical and claims data with other payers via FHIR, so that I comply with CMS interoperability rules and support continuity of care.

#### Acceptance Criteria

1. THE Payer_to_Payer_API SHALL support the FHIR `$member-match` operation on the Patient resource to identify members across payers
2. THE Payer_to_Payer_API SHALL support the Da Vinci Payer Data Exchange (PDex) `$everything` operation to retrieve a member's complete data set
3. WHEN a `$member-match` request is received, THE Payer_to_Payer_API SHALL return a matched Patient resource or an HTTP 422 OperationOutcome if no unique match is found
4. THE Payer_to_Payer_API SHALL require mutual TLS (mTLS) in addition to SMART_Auth bearer tokens for all inter-payer requests
5. WHEN a `$everything` operation is invoked, THE Payer_to_Payer_API SHALL return a FHIR Bundle containing Patient, Coverage, ExplanationOfBenefit, and clinical resources for the matched member
6. THE Payer_to_Payer_API SHALL support the `_since` parameter on the `$everything` operation to filter resources by last updated date
7. IF a requesting payer is not registered as an authorized partner, THEN THE Payer_to_Payer_API SHALL return an HTTP 403 status and an OperationOutcome resource

---

### Requirement 9: Preauthorization API

**User Story:** As a provider system, I want to submit and track prior authorization requests using FHIR, so that I can reduce administrative burden and get faster coverage decisions.

#### Acceptance Criteria

1. THE Preauth_API SHALL support submission of prior authorization requests via `POST /Claim` with `use` set to `preauthorization`
2. THE Preauth_API SHALL support the FHIR `$submit` operation on the Claim resource per the Da Vinci Prior Authorization Support IG
3. WHEN a preauthorization Claim is submitted, THE Preauth_API SHALL return a ClaimResponse resource with an `outcome` of `queued`, `complete`, or `error`
4. THE Preauth_API SHALL support the FHIR `read` interaction `GET /ClaimResponse/{id}` to retrieve the status of a submitted preauthorization
5. WHEN a preauthorization decision is made, THE Preauth_API SHALL update the ClaimResponse `disposition` field with the approval or denial reason
6. THE Preauth_API SHALL support the `$inquire` operation to query the status of an existing preauthorization request
7. IF a preauthorization Claim is missing required clinical documentation references, THEN THE Preauth_API SHALL return an HTTP 422 status and an OperationOutcome identifying the missing elements
8. THE Preauth_API SHALL respond to `$submit` operations within 5 seconds for synchronous decisions

---

### Requirement 10: Pagination and Search Result Consistency

**User Story:** As an API consumer, I want consistent pagination across all search endpoints, so that I can reliably retrieve large result sets without missing or duplicating records.

#### Acceptance Criteria

1. THE FHIR_Server SHALL support the `_count` search parameter on all search interactions to control page size, with a default of 20 and a maximum of 100
2. WHEN a search result set exceeds the page size, THE FHIR_Server SHALL include `next` and `self` Bundle links for navigation
3. THE FHIR_Server SHALL support the `_sort` search parameter on all search interactions
4. THE FHIR_Server SHALL support the `_include` and `_revinclude` search parameters to allow Clients to retrieve related resources in a single Bundle
5. WHEN a search returns zero results, THE FHIR_Server SHALL return an HTTP 200 status with an empty Bundle of type `searchset`

---

### Requirement 11: Audit Logging

**User Story:** As a compliance officer, I want all PHI access events logged, so that I can meet HIPAA audit trail requirements.

#### Acceptance Criteria

1. THE FHIR_Server SHALL create an AuditEvent resource for every read, search, create, and update interaction on Patient, EOB, Claim, and ClaimResponse resources
2. WHEN an AuditEvent is created, THE FHIR_Server SHALL record the Client identity, resource type, resource id, action type, and timestamp
3. THE FHIR_Server SHALL retain AuditEvent records for a minimum of 6 years
4. WHEN a Client queries `GET /AuditEvent`, THE FHIR_Server SHALL return AuditEvent resources scoped to the Client's authorized patient population only

---

### Requirement 12: Error Handling and Validation

**User Story:** As an API consumer, I want consistent, descriptive error responses, so that I can diagnose and correct integration issues quickly.

#### Acceptance Criteria

1. THE FHIR_Server SHALL return an OperationOutcome resource as the body of every 4xx and 5xx HTTP response
2. WHEN a required field is missing from a submitted resource, THE FHIR_Server SHALL return an HTTP 422 status and an OperationOutcome with `issue.severity` of `error` and a `diagnostics` message identifying the missing field
3. WHEN an internal server error occurs, THE FHIR_Server SHALL return an HTTP 500 status and an OperationOutcome with `issue.code` of `exception`, without exposing internal stack traces
4. THE FHIR_Server SHALL validate all incoming resource `resourceType` fields and return an HTTP 400 status and an OperationOutcome if the `resourceType` does not match the target endpoint

---

### Requirement 13: Location API

**User Story:** As a healthcare application developer, I want to retrieve and manage Location resources in FHIR R4 format, so that I can display and associate physical care sites with patients, practitioners, and organizations.

#### Acceptance Criteria

1. THE Location_API SHALL support the FHIR `read` interaction: `GET /Location/{id}`
2. THE Location_API SHALL support the FHIR `search` interaction: `GET /Location` with search parameters `_id`, `name`, `address`, `address-city`, `address-state`, `address-postalcode`, `type`, and `organization`
3. THE Location_API SHALL support the FHIR `create` interaction: `POST /Location`
4. THE Location_API SHALL support the FHIR `update` interaction: `PUT /Location/{id}`
5. WHEN a search returns multiple results, THE Location_API SHALL return a FHIR Bundle of type `searchset`
6. WHEN a Location resource is created or updated, THE Location_API SHALL validate the resource against the US Core Location profile
7. IF a Location resource fails US Core profile validation, THEN THE Location_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
8. WHEN a Client requests a Location resource that does not exist, THE Location_API SHALL return an HTTP 404 status and an OperationOutcome resource
9. THE Location_API SHALL include a `managingOrganization` reference element in every Location resource response where the managing organization is known

---

### Requirement 14: Year-End Reports API

**User Story:** As a member services application, I want to generate annual summary reports per member, so that I can provide year-end cost and coverage summaries for patients, claims, and EOBs.

#### Acceptance Criteria

1. THE Report_API SHALL support a `GET /Report/year-end` operation accepting `member` (patient identifier) and `year` (four-digit calendar year) as required query parameters
2. WHEN a valid `member` and `year` are provided, THE Report_API SHALL return a year-end summary including total claims count, total paid amount, total patient responsibility amount, and a covered services breakdown for the specified calendar year
3. WHEN the `format` query parameter is set to `fhir-bundle`, THE Report_API SHALL return the year-end summary as a FHIR Bundle of type `document` containing the relevant Patient, ExplanationOfBenefit, and summary Composition resources
4. WHEN the `format` query parameter is set to `json` or is omitted, THE Report_API SHALL return the year-end summary as a structured JSON object
5. IF no claims or EOB data exists for the specified `member` and `year`, THEN THE Report_API SHALL return an HTTP 200 status with a summary indicating zero claims and zero amounts
6. IF the specified `member` identifier does not match a known Patient resource, THEN THE Report_API SHALL return an HTTP 404 status and an OperationOutcome resource
7. WHEN a Client requests a year-end report for a member outside the Client's authorized SMART_Auth scope, THE Report_API SHALL return an HTTP 403 status and an OperationOutcome resource
8. THE Report_API SHALL create an AuditEvent resource for every year-end report generation request, recording the Client identity, member identifier, requested year, and timestamp

---

### Requirement 15: Patient Role API

**User Story:** As a healthcare application developer, I want to retrieve and manage RelatedPerson resources representing roles a person plays in relation to a patient, so that I can track guardians, emergency contacts, and caregivers associated with a patient.

#### Acceptance Criteria

1. THE PatientRole_API SHALL support the FHIR `read` interaction: `GET /RelatedPerson/{id}`
2. THE PatientRole_API SHALL support the FHIR `search` interaction: `GET /RelatedPerson` with search parameters `patient`, `relationship`, `name`, and `_id`
3. THE PatientRole_API SHALL support the FHIR `create` interaction: `POST /RelatedPerson`
4. THE PatientRole_API SHALL support the FHIR `update` interaction: `PUT /RelatedPerson/{id}`
5. WHEN a search returns multiple results, THE PatientRole_API SHALL return a FHIR Bundle of type `searchset`
6. WHEN a RelatedPerson resource is created or updated, THE PatientRole_API SHALL validate the resource against the US Core RelatedPerson profile
7. IF a RelatedPerson resource fails US Core profile validation, THEN THE PatientRole_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
8. WHEN a Client requests a RelatedPerson resource that does not exist, THE PatientRole_API SHALL return an HTTP 404 status and an OperationOutcome resource
9. THE PatientRole_API SHALL include a `patient` reference element and at least one `relationship` coding in every RelatedPerson resource response

---

### Requirement 16: Encounter API

**User Story:** As a healthcare application developer, I want to retrieve and manage Encounter resources representing patient visits and interactions with healthcare services, so that I can track and display the history of care events for a patient.

#### Acceptance Criteria

1. THE Encounter_API SHALL support the FHIR `read` interaction: `GET /Encounter/{id}`
2. THE Encounter_API SHALL support the FHIR `search` interaction: `GET /Encounter` with search parameters `patient`, `status`, `class`, `date`, `practitioner`, `location`, and `_id`
3. THE Encounter_API SHALL support the FHIR `create` interaction: `POST /Encounter`
4. THE Encounter_API SHALL support the FHIR `update` interaction: `PUT /Encounter/{id}`
5. WHEN a search returns multiple results, THE Encounter_API SHALL return a FHIR Bundle of type `searchset`
6. WHEN an Encounter resource is created or updated, THE Encounter_API SHALL validate the resource against the US Core Encounter profile
7. IF an Encounter resource fails US Core profile validation, THEN THE Encounter_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
8. WHEN a Client requests an Encounter resource that does not exist, THE Encounter_API SHALL return an HTTP 404 status and an OperationOutcome resource
9. THE Encounter_API SHALL include a `subject` reference to a Patient resource and a `status` element in every Encounter resource response

---

### Requirement 17: Medical Knowledge API

**User Story:** As a clinical application developer, I want to perform terminology lookups and value set expansions via FHIR operations, so that I can validate and translate clinical codes using standard code systems.

#### Acceptance Criteria

1. THE Terminology_API SHALL support the FHIR `$lookup` operation on CodeSystem: `GET /CodeSystem/$lookup` with parameters `system`, `code`, and `version`
2. THE Terminology_API SHALL support the FHIR `$validate-code` operation on ValueSet: `GET /ValueSet/$validate-code` with parameters `url`, `system`, `code`, and `display`
3. THE Terminology_API SHALL support the FHIR `$expand` operation on ValueSet: `GET /ValueSet/$expand` with parameters `url`, `filter`, and `count`
4. THE Terminology_API SHALL support code lookups and validations for SNOMED_CT, LOINC, ICD-10, CPT, and RxNorm code systems
5. WHEN a `$lookup` request is received for a valid code, THE Terminology_API SHALL return a FHIR Parameters resource containing the display name, definition, and designations for the code
6. WHEN a `$validate-code` request is received, THE Terminology_API SHALL return a FHIR Parameters resource with a `result` parameter of `true` or `false` indicating whether the code is valid in the specified ValueSet
7. WHEN a `$expand` request is received for a valid ValueSet URL, THE Terminology_API SHALL return a ValueSet resource with an `expansion` element containing the matching concepts
8. IF a `$lookup` or `$validate-code` request references an unsupported code system, THEN THE Terminology_API SHALL return an HTTP 400 status and an OperationOutcome identifying the unsupported system
9. WHEN a ConceptMap translation is requested via `GET /ConceptMap/$translate` with parameters `url`, `system`, `code`, and `targetsystem`, THE Terminology_API SHALL return a FHIR Parameters resource containing the translated code and equivalence

---

### Requirement 18: Provider Directory API

**User Story:** As a health plan application, I want to search and retrieve practitioner, organization, and location directory data per the Da Vinci PDEX Plan Net IG, so that I can display accurate in-network provider information to members.

#### Acceptance Criteria

1. THE ProviderDirectory_API SHALL support the FHIR `read` interaction for Practitioner, Organization, Location, OrganizationAffiliation, PractitionerRole, and HealthcareService resources
2. THE ProviderDirectory_API SHALL support the FHIR `search` interaction on Practitioner with parameters `specialty`, `name`, `identifier`, and `_id`
3. THE ProviderDirectory_API SHALL support the FHIR `search` interaction on Organization with parameters `name`, `type`, `identifier`, and `_id`
4. THE ProviderDirectory_API SHALL support the FHIR `search` interaction on Location with parameters `near` (proximity search), `address`, `address-state`, `type`, and `organization`
5. THE ProviderDirectory_API SHALL support the FHIR `search` interaction on PractitionerRole with parameters `specialty`, `organization`, `network`, `location`, and `accepting-patients`
6. WHEN a search returns multiple results, THE ProviderDirectory_API SHALL return a FHIR Bundle of type `searchset`
7. WHEN a `near` parameter is provided on a Location search, THE ProviderDirectory_API SHALL return Location resources within the specified distance in kilometers, ordered by proximity
8. THE ProviderDirectory_API SHALL validate all Practitioner, Organization, Location, and PractitionerRole resources against the corresponding PDEX_Plan_Net profiles
9. IF a resource fails PDEX_Plan_Net profile validation, THEN THE ProviderDirectory_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
10. THE ProviderDirectory_API SHALL support filtering PractitionerRole search results by insurance plan via the `insurance-plan` search parameter

---

### Requirement 19: Coverage API

**User Story:** As a healthcare application developer, I want to retrieve and manage Coverage resources representing a patient's insurance coverage, so that I can display and update coverage details including beneficiary, payor, and plan information.

#### Acceptance Criteria

1. THE Coverage_API SHALL support the FHIR `read` interaction: `GET /Coverage/{id}`
2. THE Coverage_API SHALL support the FHIR `search` interaction: `GET /Coverage` with search parameters `patient`, `subscriber`, `payor`, `status`, `period`, and `_id`
3. THE Coverage_API SHALL support the FHIR `create` interaction: `POST /Coverage`
4. THE Coverage_API SHALL support the FHIR `update` interaction: `PUT /Coverage/{id}`
5. WHEN a search returns multiple results, THE Coverage_API SHALL return a FHIR Bundle of type `searchset`
6. WHEN a Coverage resource is created or updated, THE Coverage_API SHALL validate the resource against the US Core Coverage profile
7. IF a Coverage resource fails US Core profile validation, THEN THE Coverage_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
8. WHEN a Client requests a Coverage resource that does not exist, THE Coverage_API SHALL return an HTTP 404 status and an OperationOutcome resource
9. THE Coverage_API SHALL include `beneficiary`, `payor`, and `class` (plan details) elements in every Coverage resource response


---

### Requirement 20: Clinical Data APIs (CMS-9115-F Patient Access)

**User Story:** As a patient-facing application developer, I want to retrieve clinical data resources including conditions, allergies, medications, immunizations, procedures, and diagnostic reports via FHIR R4 APIs, so that I can display a complete clinical picture to members as required by the CMS-9115-F Patient Access API rule.

#### Acceptance Criteria

1. THE Condition_API SHALL support the FHIR `read` interaction: `GET /Condition/{id}`
2. THE Condition_API SHALL support the FHIR `search` interaction: `GET /Condition` with search parameters `patient`, `category`, `clinical-status`, and `onset-date`
3. WHEN a Condition resource is created or updated, THE Condition_API SHALL validate the resource against the US Core Condition profile
4. IF a Condition resource fails US Core profile validation, THEN THE Condition_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
5. THE AllergyIntolerance_API SHALL support the FHIR `read` interaction: `GET /AllergyIntolerance/{id}`
6. THE AllergyIntolerance_API SHALL support the FHIR `search` interaction: `GET /AllergyIntolerance` with search parameters `patient` and `clinical-status`
7. WHEN an AllergyIntolerance resource is created or updated, THE AllergyIntolerance_API SHALL validate the resource against the US Core AllergyIntolerance profile
8. IF an AllergyIntolerance resource fails US Core profile validation, THEN THE AllergyIntolerance_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
9. THE MedicationRequest_API SHALL support the FHIR `read` interaction: `GET /MedicationRequest/{id}`
10. THE MedicationRequest_API SHALL support the FHIR `search` interaction: `GET /MedicationRequest` with search parameters `patient`, `status`, and `intent`
11. WHEN a MedicationRequest resource is created or updated, THE MedicationRequest_API SHALL validate the resource against the US Core MedicationRequest profile
12. IF a MedicationRequest resource fails US Core profile validation, THEN THE MedicationRequest_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
13. THE Immunization_API SHALL support the FHIR `read` interaction: `GET /Immunization/{id}`
14. THE Immunization_API SHALL support the FHIR `search` interaction: `GET /Immunization` with search parameters `patient`, `date`, and `status`
15. WHEN an Immunization resource is created or updated, THE Immunization_API SHALL validate the resource against the US Core Immunization profile
16. IF an Immunization resource fails US Core profile validation, THEN THE Immunization_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
17. THE Procedure_API SHALL support the FHIR `read` interaction: `GET /Procedure/{id}`
18. THE Procedure_API SHALL support the FHIR `search` interaction: `GET /Procedure` with search parameters `patient`, `date`, `status`, and `code`
19. WHEN a Procedure resource is created or updated, THE Procedure_API SHALL validate the resource against the US Core Procedure profile
20. IF a Procedure resource fails US Core profile validation, THEN THE Procedure_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
21. THE DiagnosticReport_API SHALL support the FHIR `read` interaction: `GET /DiagnosticReport/{id}`
22. THE DiagnosticReport_API SHALL support the FHIR `search` interaction: `GET /DiagnosticReport` with search parameters `patient`, `category`, `date`, and `code`
23. WHEN a DiagnosticReport resource is created or updated, THE DiagnosticReport_API SHALL validate the resource against the US Core DiagnosticReport profile
24. IF a DiagnosticReport resource fails US Core profile validation, THEN THE DiagnosticReport_API SHALL return an HTTP 422 status and an OperationOutcome listing all validation errors
25. WHEN a search returns multiple results, THE Condition_API, AllergyIntolerance_API, MedicationRequest_API, Immunization_API, Procedure_API, and DiagnosticReport_API SHALL each return a FHIR Bundle of type `searchset`
26. WHEN a Client requests a clinical resource that does not exist, THE respective Clinical Data API SHALL return an HTTP 404 status and an OperationOutcome resource

---

### Requirement 21: Bulk Data Export (CMS-9115-F)

**User Story:** As a payer data analyst, I want to export large volumes of FHIR data in bulk using the FHIR Bulk Data Access IG, so that I can perform population-level analytics and comply with CMS-9115-F data availability requirements.

#### Acceptance Criteria

1. THE BulkExport_API SHALL support the `$export` operation at the system level: `POST /$export`
2. THE BulkExport_API SHALL support the `$export` operation at the Patient level: `POST /Patient/$export`
3. THE BulkExport_API SHALL support the `$export` operation at the Group level: `POST /Group/{id}/$export`
4. THE BulkExport_API SHALL support the `_since` parameter to filter exported resources by last updated date
5. THE BulkExport_API SHALL support the `_type` parameter to filter exported resources by resource type
6. THE BulkExport_API SHALL support the `_outputFormat` parameter with a default value of `application/fhir+ndjson`
7. THE BulkExport_API SHALL return all exported data in NDJSON format with one resource per line
8. WHEN a `$export` request is accepted, THE BulkExport_API SHALL return an HTTP 202 status with a `Content-Location` header containing the polling URL for the export job
9. WHEN a Client polls the export status URL and the export is in progress, THE BulkExport_API SHALL return an HTTP 202 status with an optional `X-Progress` header indicating completion percentage
10. WHEN a Client polls the export status URL and the export is complete, THE BulkExport_API SHALL return an HTTP 200 status with a JSON body containing `transactionTime`, `request`, `requiresAccessToken`, and an `output` array with `type` and `url` fields for each downloadable file
11. THE BulkExport_API SHALL require SMART_Backend_Services authentication using client credentials with a signed JWT assertion for all bulk export requests
12. IF a `$export` request specifies an unsupported `_type` parameter value, THEN THE BulkExport_API SHALL return an HTTP 400 status and an OperationOutcome identifying the unsupported resource type

---

### Requirement 22: SMART App Launch Configuration

**User Story:** As a third-party SMART app developer, I want to discover the authorization endpoints and capabilities of the FHIR server, so that I can integrate my application using the SMART App Launch Framework v2.0.

#### Acceptance Criteria

1. THE FHIR_Server SHALL expose a `/.well-known/smart-configuration` endpoint returning a JSON document
2. THE SMART_Configuration_Endpoint SHALL include the `authorization_endpoint` field containing the OAuth 2.0 authorization URL
3. THE SMART_Configuration_Endpoint SHALL include the `token_endpoint` field containing the OAuth 2.0 token URL
4. THE SMART_Configuration_Endpoint SHALL include the `scopes_supported` field listing all supported SMART on FHIR scopes
5. THE SMART_Configuration_Endpoint SHALL include the `code_challenge_methods_supported` field with at least `S256` to indicate PKCE support
6. THE SMART_Configuration_Endpoint SHALL include the `capabilities` field listing supported SMART App Launch capabilities including `launch-ehr`, `launch-standalone`, `client-public`, `client-confidential-symmetric`, and `sso-openid-connect`
7. THE FHIR_Server SHALL support the standalone launch sequence as defined in the SMART App Launch Framework IG v2.0
8. THE FHIR_Server SHALL support the EHR launch sequence as defined in the SMART App Launch Framework IG v2.0
9. WHEN a Client requests `/.well-known/smart-configuration`, THE FHIR_Server SHALL return the response with `Content-Type` of `application/json`
10. THE SMART_Configuration_Endpoint SHALL include the `grant_types_supported` field listing `authorization_code` and `client_credentials`

---

### Requirement 23: Consent Management (CMS-0057-F Payer-to-Payer)

**User Story:** As a payer compliance officer, I want to manage patient consent for Payer-to-Payer data exchange using FHIR Consent resources, so that the platform complies with CMS-0057-F requirements and respects patient opt-in and opt-out decisions.

#### Acceptance Criteria

1. THE Consent_API SHALL support the FHIR `read` interaction: `GET /Consent/{id}`
2. THE Consent_API SHALL support the FHIR `search` interaction: `GET /Consent` with search parameters `patient`, `status`, `category`, and `period`
3. THE Consent_API SHALL support the FHIR `create` interaction: `POST /Consent`
4. THE Consent_API SHALL support the FHIR `update` interaction: `PUT /Consent/{id}`
5. WHEN a Consent resource is created, THE Consent_API SHALL require the `scope`, `patient` reference, and `period` elements to be present
6. IF a Consent resource is missing the `scope`, `patient` reference, or `period` element, THEN THE Consent_API SHALL return an HTTP 422 status and an OperationOutcome identifying the missing elements
7. THE Consent_API SHALL support `status` values of `active`, `inactive`, `rejected`, and `entered-in-error` for opt-in and opt-out decisions
8. WHEN a Payer-to-Payer data exchange request is received, THE Payer_to_Payer_API SHALL query the Consent_API to verify that an `active` Consent resource exists for the specified patient before sharing data
9. IF no `active` Consent resource exists for the specified patient, THEN THE Payer_to_Payer_API SHALL return an HTTP 403 status and an OperationOutcome indicating that patient consent has not been granted
10. WHEN a search returns multiple results, THE Consent_API SHALL return a FHIR Bundle of type `searchset`
11. WHEN a Client requests a Consent resource that does not exist, THE Consent_API SHALL return an HTTP 404 status and an OperationOutcome resource

---

### Requirement 24: Multi-Tenant Architecture

**User Story:** As a platform operator, I want each insurance company (payer) to have isolated data storage and tenant-specific configuration, so that I can sell the platform as a multi-tenant SaaS product with strict data separation between payer organizations.

#### Acceptance Criteria

1. THE FHIR_Server SHALL isolate all FHIR resource data per Tenant so that one Tenant cannot access another Tenant's data
2. THE FHIR_Server SHALL identify the current Tenant from the incoming request using one of the following methods: subdomain, API key, or JWT claim
3. WHEN a request is received, THE FHIR_Server SHALL resolve the Tenant identity before processing any FHIR interaction
4. IF the Tenant identity cannot be resolved from the request, THEN THE FHIR_Server SHALL return an HTTP 401 status and an OperationOutcome indicating that tenant identification failed
5. THE FHIR_Server SHALL support tenant-specific configuration including branding, SMART_Auth endpoints, and database connection settings
6. THE FHIR_Server SHALL enforce cross-tenant data isolation at the repository layer so that all database queries are scoped to the resolved Tenant
7. THE Tenant_Management_API SHALL support a `POST /tenants` operation to provision a new payer organization with isolated data storage
8. WHEN a new Tenant is provisioned, THE Tenant_Management_API SHALL create the Tenant's isolated data store and return the Tenant configuration including the assigned tenant identifier
9. IF a FHIR resource query attempts to access data belonging to a different Tenant, THEN THE FHIR_Server SHALL exclude that data from the result set without returning an error

---

### Requirement 25: Admin Portal and Tenant Management

**User Story:** As a platform administrator, I want an administrative API for managing tenants, monitoring API usage, and configuring rate limits, so that I can operate the multi-tenant platform and provide visibility into each payer's usage.

#### Acceptance Criteria

1. THE Admin_Portal_API SHALL support creating a new Tenant via `POST /admin/tenants` with required fields: organization name, contact email, and plan tier
2. THE Admin_Portal_API SHALL support updating an existing Tenant via `PUT /admin/tenants/{id}` to modify configuration, contact information, and plan tier
3. THE Admin_Portal_API SHALL support deactivating a Tenant via `PATCH /admin/tenants/{id}/deactivate`, which disables all API access for that Tenant
4. WHEN a Tenant is deactivated, THE FHIR_Server SHALL reject all requests for that Tenant with an HTTP 403 status and an OperationOutcome indicating that the tenant account is inactive
5. THE Admin_Portal_API SHALL expose a `GET /admin/tenants/{id}/metrics` endpoint returning API usage metrics including request count, error rate, and average latency for the specified Tenant over a configurable time period
6. THE Admin_Portal_API SHALL support configuring tenant-level rate limits via `PUT /admin/tenants/{id}/rate-limit` with parameters for requests per second and burst size
7. WHEN a Tenant exceeds the configured rate limit, THE FHIR_Server SHALL return an HTTP 429 status and an OperationOutcome indicating that the rate limit has been exceeded
8. THE Admin_Portal_API SHALL support API key management via `POST /admin/tenants/{id}/api-keys` to create a new API key and `DELETE /admin/tenants/{id}/api-keys/{keyId}` to revoke an existing key
9. WHEN an API key is revoked, THE FHIR_Server SHALL reject all requests using that API key with an HTTP 401 status and an OperationOutcome resource

---

### Requirement 26: CMS Compliance Reporting

**User Story:** As a payer compliance officer, I want to generate CMS attestation reports showing API availability, response times, and error rates, so that I can submit compliance evidence to CMS and demonstrate adherence to interoperability rules.

#### Acceptance Criteria

1. THE Compliance_Report_API SHALL support a `GET /admin/compliance/report` operation accepting `tenant` (tenant identifier), `start-date`, and `end-date` as required query parameters
2. WHEN a valid compliance report request is received, THE Compliance_Report_API SHALL return a report containing API uptime percentage, average response time per endpoint, and error rate per endpoint for the specified period
3. THE Compliance_Report_API SHALL track and report the following CMS-required metrics: API uptime percentage, average response time in milliseconds, error rate as a percentage of total requests, and request volume per endpoint
4. THE Compliance_Report_API SHALL support exporting the compliance report as PDF via the `format=pdf` query parameter
5. THE Compliance_Report_API SHALL support exporting the compliance report as CSV via the `format=csv` query parameter
6. WHEN the `format` query parameter is omitted, THE Compliance_Report_API SHALL return the compliance report as a structured JSON object
7. IF no metrics data exists for the specified Tenant and date range, THEN THE Compliance_Report_API SHALL return an HTTP 200 status with a report indicating zero requests and no availability data
8. IF the specified Tenant identifier does not match a known Tenant, THEN THE Compliance_Report_API SHALL return an HTTP 404 status and an OperationOutcome resource
9. THE Compliance_Report_API SHALL restrict access to compliance reports to Clients with the `admin` or `compliance-officer` role
