using HealthcareFhirApi.Infrastructure.Data;

namespace HealthcareFhirApi.Infrastructure.Repositories;

public class FhirResourceRepository<TResource> : IFhirResourceRepository<TResource>
    where TResource : Resource
{
    private readonly FhirDbContext _db;
    private readonly TenantContext _tenantContext;
    private readonly FhirJsonParser _parser;
    private readonly FhirJsonSerializer _serializer;
    private readonly string _resourceType;

    public FhirResourceRepository(FhirDbContext db, TenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
        _parser = new FhirJsonParser();
        _serializer = new FhirJsonSerializer();
        _resourceType = ModelInfo.GetFhirTypeNameForType(typeof(TResource))
            ?? typeof(TResource).Name;
    }

    public async System.Threading.Tasks.Task<TResource?> GetByIdAsync(
        string id, CancellationToken ct = default)
    {
        var entity = await _db.Resources
            .Where(r => r.TenantId == _tenantContext.TenantId
                     && r.ResourceType == _resourceType
                     && r.Id == id && !r.IsDeleted)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : _parser.Parse<TResource>(entity.Data);
    }

    public async System.Threading.Tasks.Task<PagedResult<TResource>> SearchAsync(
        SearchParameters parameters, CancellationToken ct = default)
    {
        var query = _db.Resources
            .Where(r => r.TenantId == _tenantContext.TenantId
                     && r.ResourceType == _resourceType && !r.IsDeleted);

        query = ApplySearchFilters(query, parameters);

        var total = await query.CountAsync(ct);
        var entities = await query
            .OrderBy(r => r.LastUpdated)
            .Skip(parameters.Skip)
            .Take(parameters.Take)
            .ToListAsync(ct);

        var resources = entities.Select(e => _parser.Parse<TResource>(e.Data)).ToList();
        return new PagedResult<TResource>(resources, total, parameters);
    }

    public async System.Threading.Tasks.Task<TResource> CreateAsync(
        TResource resource, CancellationToken ct = default)
    {
        resource.Id = Guid.NewGuid().ToString("N");
        resource.Meta = new Meta
        {
            LastUpdated = DateTimeOffset.UtcNow,
            VersionId = "1"
        };

        var entity = new FhirResourceEntity
        {
            Id = resource.Id,
            ResourceType = _resourceType,
            TenantId = _tenantContext.TenantId,
            Data = _serializer.SerializeToString(resource),
            LastUpdated = resource.Meta.LastUpdated!.Value,
            VersionId = 1
        };

        _db.Resources.Add(entity);
        await _db.SaveChangesAsync(ct);
        return resource;
    }

    public async System.Threading.Tasks.Task<TResource> UpdateAsync(
        string id, TResource resource, CancellationToken ct = default)
    {
        var entity = await _db.Resources
            .Where(r => r.TenantId == _tenantContext.TenantId
                     && r.ResourceType == _resourceType && r.Id == id)
            .FirstOrDefaultAsync(ct)
            ?? throw new ResourceNotFoundException(_resourceType, id);

        resource.Id = id;
        resource.Meta = new Meta
        {
            LastUpdated = DateTimeOffset.UtcNow,
            VersionId = (entity.VersionId + 1).ToString()
        };

        entity.Data = _serializer.SerializeToString(resource);
        entity.LastUpdated = resource.Meta.LastUpdated!.Value;
        entity.VersionId++;

        await _db.SaveChangesAsync(ct);
        return resource;
    }

    private IQueryable<FhirResourceEntity> ApplySearchFilters(
        IQueryable<FhirResourceEntity> query, SearchParameters parameters)
    {
        foreach (var (key, value) in parameters.Filters)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var searchValue = value;
            query = query.Where(r => EF.Functions.Like(r.Data, $"%\"{key}\":\"{searchValue}\"%")
                                  || EF.Functions.Like(r.Data, $"%\"{key}\": \"{searchValue}\"%"));
        }

        return query;
    }
}
