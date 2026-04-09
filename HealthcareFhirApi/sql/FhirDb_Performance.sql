-- ============================================================
-- FhirDb Performance Script
-- Run this in SSMS after the initial EF Core migration
-- Adds: computed columns, indexes, and table partitioning
-- for 100M+ record scale
-- ============================================================

USE FhirDb;
GO

-- ============================================================
-- 1. COMPUTED COLUMNS for common JSON search fields
-- These extract frequently searched values from the JSON Data
-- column and persist them as indexed columns
-- ============================================================

-- Patient: identifier, name, birthdate, gender
ALTER TABLE [dbo].[Resources]
    ADD [PatientIdentifier] AS JSON_VALUE([Data], '$.identifier[0].value') PERSISTED;

ALTER TABLE [dbo].[Resources]
    ADD [PatientFamilyName] AS JSON_VALUE([Data], '$.name[0].family') PERSISTED;

ALTER TABLE [dbo].[Resources]
    ADD [PatientBirthDate] AS JSON_VALUE([Data], '$.birthDate') PERSISTED;

ALTER TABLE [dbo].[Resources]
    ADD [PatientGender] AS JSON_VALUE([Data], '$.gender') PERSISTED;

-- Subject/Patient reference (used by Claim, EOB, Encounter, Coverage, RelatedPerson)
ALTER TABLE [dbo].[Resources]
    ADD [SubjectRef] AS JSON_VALUE([Data], '$.subject.reference') PERSISTED;

ALTER TABLE [dbo].[Resources]
    ADD [PatientRef] AS JSON_VALUE([Data], '$.patient.reference') PERSISTED;

-- Claim / EOB: status, created, provider
ALTER TABLE [dbo].[Resources]
    ADD [ResourceStatus] AS JSON_VALUE([Data], '$.status') PERSISTED;

ALTER TABLE [dbo].[Resources]
    ADD [CreatedDate] AS JSON_VALUE([Data], '$.created') PERSISTED;

ALTER TABLE [dbo].[Resources]
    ADD [ProviderRef] AS JSON_VALUE([Data], '$.provider.reference') PERSISTED;

-- Organization: name
ALTER TABLE [dbo].[Resources]
    ADD [OrgName] AS JSON_VALUE([Data], '$.name') PERSISTED;

-- Practitioner: NPI identifier
ALTER TABLE [dbo].[Resources]
    ADD [NpiIdentifier] AS JSON_VALUE([Data], '$.identifier[0].value') PERSISTED;

GO

-- ============================================================
-- 2. INDEXES on computed columns
-- ============================================================

-- Primary lookup: ResourceType + Id (already the PK/clustered index)

-- Patient searches
CREATE NONCLUSTERED INDEX [IX_Resources_PatientIdentifier]
    ON [dbo].[Resources] ([ResourceType], [PatientIdentifier])
    WHERE [ResourceType] = 'Patient';

CREATE NONCLUSTERED INDEX [IX_Resources_PatientFamilyName]
    ON [dbo].[Resources] ([ResourceType], [PatientFamilyName])
    WHERE [ResourceType] = 'Patient';

CREATE NONCLUSTERED INDEX [IX_Resources_PatientBirthDate]
    ON [dbo].[Resources] ([ResourceType], [PatientBirthDate])
    WHERE [ResourceType] = 'Patient';

CREATE NONCLUSTERED INDEX [IX_Resources_PatientGender]
    ON [dbo].[Resources] ([ResourceType], [PatientGender])
    WHERE [ResourceType] = 'Patient';

-- Cross-resource patient reference searches (Claim, EOB, Encounter, Coverage)
CREATE NONCLUSTERED INDEX [IX_Resources_PatientRef]
    ON [dbo].[Resources] ([ResourceType], [PatientRef])
    INCLUDE ([Id], [LastUpdated]);

CREATE NONCLUSTERED INDEX [IX_Resources_SubjectRef]
    ON [dbo].[Resources] ([ResourceType], [SubjectRef])
    INCLUDE ([Id], [LastUpdated]);

-- Status searches
CREATE NONCLUSTERED INDEX [IX_Resources_Status]
    ON [dbo].[Resources] ([ResourceType], [ResourceStatus])
    INCLUDE ([Id], [LastUpdated]);

-- Date searches
CREATE NONCLUSTERED INDEX [IX_Resources_CreatedDate]
    ON [dbo].[Resources] ([ResourceType], [CreatedDate])
    INCLUDE ([Id], [LastUpdated]);

-- Provider searches
CREATE NONCLUSTERED INDEX [IX_Resources_ProviderRef]
    ON [dbo].[Resources] ([ResourceType], [ProviderRef])
    INCLUDE ([Id], [LastUpdated]);

-- Organization name
CREATE NONCLUSTERED INDEX [IX_Resources_OrgName]
    ON [dbo].[Resources] ([ResourceType], [OrgName])
    WHERE [ResourceType] = 'Organization';

-- LastUpdated (for $everything _since queries)
CREATE NONCLUSTERED INDEX [IX_Resources_LastUpdated]
    ON [dbo].[Resources] ([ResourceType], [LastUpdated])
    INCLUDE ([Id]);

GO

-- ============================================================
-- 3. TABLE PARTITIONING by ResourceType
-- Requires SQL Server Enterprise Edition or Azure SQL
-- Splits the table into separate physical partitions per
-- resource type for maximum query isolation at 100M+ scale
-- ============================================================

-- Step 1: Create a filegroup per partition (optional but recommended)
-- ALTER DATABASE FhirDb ADD FILEGROUP FG_Patient;
-- ALTER DATABASE FhirDb ADD FILEGROUP FG_Claim;
-- (Add files to filegroups as needed for your storage layout)

-- Step 2: Create partition function
CREATE PARTITION FUNCTION [PF_ResourceType] (NVARCHAR(64))
AS RANGE LEFT FOR VALUES (
    'AuditEvent',
    'Claim',
    'ClaimResponse',
    'CodeSystem',
    'Coverage',
    'Encounter',
    'ExplanationOfBenefit',
    'Location',
    'Organization',
    'Patient',
    'Practitioner',
    'PractitionerRole',
    'RelatedPerson',
    'ValueSet'
);
GO

-- Step 3: Create partition scheme (maps partitions to filegroups)
-- Using PRIMARY for all partitions here; adjust for production
CREATE PARTITION SCHEME [PS_ResourceType]
AS PARTITION [PF_ResourceType]
ALL TO ([PRIMARY]);
GO

-- Step 4: Rebuild the clustered index on the partition scheme
-- NOTE: This recreates the PK as a partitioned index
-- Run during a maintenance window on production
ALTER TABLE [dbo].[Resources]
    DROP CONSTRAINT [PK_Resources];

ALTER TABLE [dbo].[Resources]
    ADD CONSTRAINT [PK_Resources]
    PRIMARY KEY CLUSTERED ([ResourceType], [Id])
    ON [PS_ResourceType] ([ResourceType]);
GO

-- ============================================================
-- 4. STATISTICS update (run periodically)
-- ============================================================
UPDATE STATISTICS [dbo].[Resources] WITH FULLSCAN;
GO

-- ============================================================
-- 5. VERIFY partition distribution
-- ============================================================
SELECT
    pf.name                         AS PartitionFunction,
    p.partition_number              AS PartitionNumber,
    rv.value                        AS BoundaryValue,
    p.rows                          AS RowCount
FROM sys.partitions p
JOIN sys.indexes i ON p.object_id = i.object_id AND p.index_id = i.index_id
JOIN sys.objects o ON p.object_id = o.object_id
JOIN sys.partition_schemes ps ON i.data_space_id = ps.data_space_id
JOIN sys.partition_functions pf ON ps.function_id = pf.function_id
LEFT JOIN sys.partition_range_values rv
    ON pf.function_id = rv.function_id AND p.partition_number = rv.boundary_id + 1
WHERE o.name = 'Resources'
ORDER BY p.partition_number;
GO
