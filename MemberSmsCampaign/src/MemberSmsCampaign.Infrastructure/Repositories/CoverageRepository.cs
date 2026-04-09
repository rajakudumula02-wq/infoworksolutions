using Microsoft.Data.SqlClient;
using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Data;

namespace MemberSmsCampaign.Infrastructure.Repositories;

public class CoverageRepository : ICoverageRepository
{
    private readonly SqlConnectionFactory _factory;

    public CoverageRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<Coverage> CreateAsync(Coverage coverage, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        coverage.Id = coverage.Id == Guid.Empty ? Guid.NewGuid() : coverage.Id;
        var now = DateTimeOffset.UtcNow;
        coverage.CreatedAt = now;
        coverage.UpdatedAt = now;

        cmd.CommandText = @"
            INSERT INTO coverages (id, member_id, plan_name, status, period_start, period_end, created_at, updated_at)
            VALUES (@id, @mid, @plan, @status, @start, @end, @created, @updated)";
        cmd.Parameters.AddWithValue("@id", coverage.Id);
        cmd.Parameters.AddWithValue("@mid", coverage.MemberId);
        cmd.Parameters.AddWithValue("@plan", coverage.PlanName);
        cmd.Parameters.AddWithValue("@status", coverage.Status.ToString().ToLower());
        cmd.Parameters.AddWithValue("@start", coverage.PeriodStart.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@end", (object?)coverage.PeriodEnd?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", coverage.CreatedAt);
        cmd.Parameters.AddWithValue("@updated", coverage.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return coverage;
    }

    public async Task<Coverage?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM coverages WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public async Task<List<Coverage>> GetByMemberIdAsync(Guid memberId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM coverages WHERE member_id = @mid ORDER BY period_start DESC";
        cmd.Parameters.AddWithValue("@mid", memberId);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Coverage>();
        while (await reader.ReadAsync(ct)) list.Add(MapRow(reader));
        return list;
    }

    public async Task<List<Coverage>> GetAllActiveAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM coverages
            WHERE status = 'active'
              AND period_start <= CAST(GETUTCDATE() AS DATE)
              AND (period_end IS NULL OR period_end >= CAST(GETUTCDATE() AS DATE))
            ORDER BY period_start DESC";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Coverage>();
        while (await reader.ReadAsync(ct)) list.Add(MapRow(reader));
        return list;
    }

    public async Task<Coverage> UpdateAsync(Coverage coverage, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        coverage.UpdatedAt = DateTimeOffset.UtcNow;
        cmd.CommandText = @"
            UPDATE coverages SET plan_name=@plan, status=@status, period_start=@start,
            period_end=@end, updated_at=@updated WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", coverage.Id);
        cmd.Parameters.AddWithValue("@plan", coverage.PlanName);
        cmd.Parameters.AddWithValue("@status", coverage.Status.ToString().ToLower());
        cmd.Parameters.AddWithValue("@start", coverage.PeriodStart.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@end", (object?)coverage.PeriodEnd?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated", coverage.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return coverage;
    }

    public async Task<Coverage?> FindDuplicateAsync(Guid memberId, string planName, DateOnly periodStart, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT TOP 1 * FROM coverages
            WHERE member_id=@mid AND LOWER(plan_name)=LOWER(@plan) AND period_start=@start";
        cmd.Parameters.AddWithValue("@mid", memberId);
        cmd.Parameters.AddWithValue("@plan", planName);
        cmd.Parameters.AddWithValue("@start", periodStart.ToDateTime(TimeOnly.MinValue));
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM coverages WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static CoverageStatus ParseStatus(string s) => s.ToLower() switch
    {
        "active" => CoverageStatus.Active,
        "inactive" => CoverageStatus.Inactive,
        "cancelled" => CoverageStatus.Cancelled,
        _ => CoverageStatus.Active,
    };

    private static Coverage MapRow(SqlDataReader r) => new()
    {
        Id = r.GetGuid(r.GetOrdinal("id")),
        MemberId = r.GetGuid(r.GetOrdinal("member_id")),
        PlanName = r.GetString(r.GetOrdinal("plan_name")),
        Status = ParseStatus(r.GetString(r.GetOrdinal("status"))),
        PeriodStart = DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("period_start"))),
        PeriodEnd = r.IsDBNull(r.GetOrdinal("period_end")) ? null : DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("period_end"))),
        CreatedAt = r.GetDateTimeOffset(r.GetOrdinal("created_at")),
        UpdatedAt = r.GetDateTimeOffset(r.GetOrdinal("updated_at")),
    };
}
