// Feature: healthcare-fhir-api
using System.Data;
using Microsoft.Data.SqlClient;
using HealthcareFhirApi.Infrastructure.Data;

namespace HealthcareFhirApi.Api.Controllers;

[ApiController]
[Route("$import")]
[Authorize]
public class BulkImportController : FhirControllerBase
{
    private readonly FhirDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly IFhirValidationService _validator;
    private readonly string _connectionString;

    // Map resource types to their US Core / FHIR profile URLs
    private static readonly Dictionary<string, string> ProfileMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Patient"]              = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-patient",
        ["Organization"]         = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-organization",
        ["Practitioner"]         = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-practitioner",
        ["Location"]             = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-location",
        ["Coverage"]             = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-coverage",
        ["Encounter"]            = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-encounter",
        ["RelatedPerson"]        = "http://hl7.org/fhir/us/core/StructureDefinition/us-core-relatedperson",
        ["ExplanationOfBenefit"] = "http://hl7.org/fhir/us/carin-bb/StructureDefinition/C4BB-ExplanationOfBenefit",
        ["Claim"]                = "http://hl7.org/fhir/StructureDefinition/Claim",
    };

    public BulkImportController(FhirDbContext db, TenantContext tenantContext, IFhirValidationService validator, IConfiguration config)
    {
        _db = db;
        _tenantContext = tenantContext;
        _validator = validator;
        _connectionString = config.GetConnectionString("FhirDb")!;
    }

    /// <summary>
    /// POST /$import?validate=true — accepts NDJSON body (one FHIR resource per line).
    /// validate=true enables profile validation (slower but catches missing fields).
    /// validate=false (default) skips validation for maximum throughput.
    /// </summary>
    [HttpPost]
    [Consumes("application/fhir+ndjson", "application/x-ndjson", "text/plain", "application/json")]
    public async System.Threading.Tasks.Task<IActionResult> Import(
        [FromQuery] bool validate = false, CancellationToken ct = default)
    {
        using var reader = new StreamReader(Request.Body);
        var parser = new FhirJsonParser();
        var serializer = new FhirJsonSerializer();
        var table = CreateDataTable();

        int imported = 0, skipped = 0, lineNum = 0;
        var validationErrors = new List<object>();
        var now = DateTimeOffset.UtcNow;

        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNum++;
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var resource = parser.Parse<Resource>(line);

                // Validate if requested
                if (validate)
                {
                    var issues = await ValidateResourceAsync(resource, ct);
                    if (issues is not null)
                    {
                        skipped++;
                        if (validationErrors.Count < 50)
                            validationErrors.Add(new { line = lineNum, resourceType = resource.TypeName, errors = issues });
                        continue;
                    }
                }

                AddResourceToTable(table, resource, serializer, now);
                imported++;

                if (table.Rows.Count >= 5000)
                {
                    await BulkInsertAsync(table, ct);
                    table.Clear();
                }
            }
            catch (Exception ex)
            {
                skipped++;
                if (validationErrors.Count < 50)
                    validationErrors.Add(new { line = lineNum, error = ex.Message });
            }
        }

        if (table.Rows.Count > 0)
            await BulkInsertAsync(table, ct);

        return Ok(new { imported, skipped, validationErrors = validationErrors.Count > 0 ? validationErrors : null });
    }

    /// <summary>
    /// POST /$import/bundle?validate=true — accepts a FHIR Bundle and imports all entries.
    /// </summary>
    [HttpPost("bundle")]
    public async System.Threading.Tasks.Task<IActionResult> ImportBundle(
        [FromBody] Bundle bundle, [FromQuery] bool validate = false, CancellationToken ct = default)
    {
        if (bundle?.Entry is null || bundle.Entry.Count == 0)
            return BadRequest(new { error = "Bundle contains no entries" });

        var serializer = new FhirJsonSerializer();
        var table = CreateDataTable();

        int imported = 0, skipped = 0;
        var validationErrors = new List<object>();
        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < bundle.Entry.Count; i++)
        {
            var resource = bundle.Entry[i].Resource;
            if (resource is null) { skipped++; continue; }

            try
            {
                if (validate)
                {
                    var issues = await ValidateResourceAsync(resource, ct);
                    if (issues is not null)
                    {
                        skipped++;
                        if (validationErrors.Count < 50)
                            validationErrors.Add(new { entry = i, resourceType = resource.TypeName, errors = issues });
                        continue;
                    }
                }

                AddResourceToTable(table, resource, serializer, now);
                imported++;

                if (table.Rows.Count >= 5000)
                {
                    await BulkInsertAsync(table, ct);
                    table.Clear();
                }
            }
            catch (Exception ex)
            {
                skipped++;
                if (validationErrors.Count < 50)
                    validationErrors.Add(new { entry = i, error = ex.Message });
            }
        }

        if (table.Rows.Count > 0)
            await BulkInsertAsync(table, ct);

        return Ok(new { imported, skipped, validationErrors = validationErrors.Count > 0 ? validationErrors : null });
    }

    // ── Helpers ──

    private async System.Threading.Tasks.Task<List<string>?> ValidateResourceAsync(Resource resource, CancellationToken ct)
    {
        var typeName = ModelInfo.GetFhirTypeNameForType(resource.GetType()) ?? resource.TypeName;
        if (!ProfileMap.TryGetValue(typeName, out var profileUrl))
            return null; // No profile registered — skip validation, allow import

        var outcome = await _validator.ValidateAsync(resource, profileUrl, ct);
        if (_validator.IsValid(outcome))
            return null;

        return outcome.Issue
            .Where(i => i.Severity == OperationOutcome.IssueSeverity.Error || i.Severity == OperationOutcome.IssueSeverity.Fatal)
            .Select(i => i.Diagnostics)
            .ToList();
    }

    private void AddResourceToTable(DataTable table, Resource resource, FhirJsonSerializer serializer, DateTimeOffset now)
    {
        resource.Id = Guid.NewGuid().ToString("N");
        resource.Meta = new Meta { LastUpdated = now, VersionId = "1" };
        var resourceType = ModelInfo.GetFhirTypeNameForType(resource.GetType()) ?? resource.TypeName;

        table.Rows.Add(
            _tenantContext.TenantId,
            resourceType,
            resource.Id,
            serializer.SerializeToString(resource),
            now,
            false,
            1L);
    }

    private static DataTable CreateDataTable()
    {
        var table = new DataTable();
        table.Columns.Add("TenantId", typeof(string));
        table.Columns.Add("ResourceType", typeof(string));
        table.Columns.Add("Id", typeof(string));
        table.Columns.Add("Data", typeof(string));
        table.Columns.Add("LastUpdated", typeof(DateTimeOffset));
        table.Columns.Add("IsDeleted", typeof(bool));
        table.Columns.Add("VersionId", typeof(long));
        return table;
    }

    private async SystemTask BulkInsertAsync(DataTable table, CancellationToken ct)
    {
        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var bulk = new SqlBulkCopy(conn)
        {
            DestinationTableName = "Resources",
            BatchSize = 5000,
            BulkCopyTimeout = 600
        };
        bulk.ColumnMappings.Add("TenantId", "TenantId");
        bulk.ColumnMappings.Add("ResourceType", "ResourceType");
        bulk.ColumnMappings.Add("Id", "Id");
        bulk.ColumnMappings.Add("Data", "Data");
        bulk.ColumnMappings.Add("LastUpdated", "LastUpdated");
        bulk.ColumnMappings.Add("IsDeleted", "IsDeleted");
        bulk.ColumnMappings.Add("VersionId", "VersionId");

        await bulk.WriteToServerAsync(table, ct);
    }
}
