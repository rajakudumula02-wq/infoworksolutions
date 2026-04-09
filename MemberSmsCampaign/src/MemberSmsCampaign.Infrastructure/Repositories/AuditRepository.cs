using Microsoft.Data.SqlClient;
using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Data;

namespace MemberSmsCampaign.Infrastructure.Repositories;

public class AuditRepository : IAuditRepository
{
    private readonly SqlConnectionFactory _factory;
    public AuditRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task LogAsync(string entityType, string entityId, string action, string? details = null, string performedBy = "system", CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO audit_logs (id, entity_type, entity_id, action, details, performed_by, created_at)
            VALUES (@id, @type, @eid, @action, @details, @by, @created)";
        cmd.Parameters.AddWithValue("@id", Guid.NewGuid());
        cmd.Parameters.AddWithValue("@type", entityType);
        cmd.Parameters.AddWithValue("@eid", entityId);
        cmd.Parameters.AddWithValue("@action", action);
        cmd.Parameters.AddWithValue("@details", (object?)details ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@by", performedBy);
        cmd.Parameters.AddWithValue("@created", DateTimeOffset.UtcNow);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<AuditLog>> GetByEntityAsync(string entityType, string entityId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM audit_logs WHERE entity_type=@type AND entity_id=@eid ORDER BY created_at DESC";
        cmd.Parameters.AddWithValue("@type", entityType);
        cmd.Parameters.AddWithValue("@eid", entityId);
        return await ReadAll(cmd, ct);
    }

    public async Task<List<AuditLog>> GetRecentAsync(int count = 50, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT TOP ({count}) * FROM audit_logs ORDER BY created_at DESC";
        return await ReadAll(cmd, ct);
    }

    public async Task<List<AuditLog>> GetByActionAsync(string action, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM audit_logs WHERE action=@action ORDER BY created_at DESC";
        cmd.Parameters.AddWithValue("@action", action);
        return await ReadAll(cmd, ct);
    }

    private static async Task<List<AuditLog>> ReadAll(SqlCommand cmd, CancellationToken ct)
    {
        using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AuditLog>();
        while (await r.ReadAsync(ct)) list.Add(new AuditLog
        {
            Id = r.GetGuid(r.GetOrdinal("id")),
            EntityType = r.GetString(r.GetOrdinal("entity_type")),
            EntityId = r.GetString(r.GetOrdinal("entity_id")),
            Action = r.GetString(r.GetOrdinal("action")),
            Details = r.IsDBNull(r.GetOrdinal("details")) ? null : r.GetString(r.GetOrdinal("details")),
            PerformedBy = r.GetString(r.GetOrdinal("performed_by")),
            CreatedAt = r.GetDateTimeOffset(r.GetOrdinal("created_at")),
        });
        return list;
    }
}
