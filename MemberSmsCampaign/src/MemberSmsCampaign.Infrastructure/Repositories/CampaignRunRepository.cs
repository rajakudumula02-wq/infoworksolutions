using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace MemberSmsCampaign.Infrastructure.Repositories;

public class CampaignRunRepository : ICampaignRunRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public CampaignRunRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CampaignRun> CreateAsync(CampaignRun run, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO campaign_runs (id, campaign_id, status, total_eligible, total_sent, total_failed, total_skipped, started_at, completed_at)
            VALUES (@Id, @CampaignId, @Status, @TotalEligible, @TotalSent, @TotalFailed, @TotalSkipped, @StartedAt, @CompletedAt)";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", run.Id);
        cmd.Parameters.AddWithValue("@CampaignId", run.CampaignId);
        cmd.Parameters.AddWithValue("@Status", run.Status);
        cmd.Parameters.AddWithValue("@TotalEligible", run.TotalEligible);
        cmd.Parameters.AddWithValue("@TotalSent", run.TotalSent);
        cmd.Parameters.AddWithValue("@TotalFailed", run.TotalFailed);
        cmd.Parameters.AddWithValue("@TotalSkipped", run.TotalSkipped);
        cmd.Parameters.AddWithValue("@StartedAt", run.StartedAt);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)run.CompletedAt ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
        return run;
    }

    public async Task<CampaignRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "SELECT id, campaign_id, status, total_eligible, total_sent, total_failed, total_skipped, started_at, completed_at FROM campaign_runs WHERE id = @Id";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return MapCampaignRun(reader);

        return null;
    }

    public async Task<List<CampaignRun>> GetByCampaignIdAsync(Guid campaignId, CancellationToken ct = default)
    {
        const string sql = "SELECT id, campaign_id, status, total_eligible, total_sent, total_failed, total_skipped, started_at, completed_at FROM campaign_runs WHERE campaign_id = @CampaignId";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@CampaignId", campaignId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var runs = new List<CampaignRun>();
        while (await reader.ReadAsync(ct))
            runs.Add(MapCampaignRun(reader));

        return runs;
    }

    public async Task<CampaignRun> UpdateAsync(CampaignRun run, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE campaign_runs
            SET status = @Status, total_eligible = @TotalEligible, total_sent = @TotalSent,
                total_failed = @TotalFailed, total_skipped = @TotalSkipped, completed_at = @CompletedAt
            WHERE id = @Id";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", run.Id);
        cmd.Parameters.AddWithValue("@Status", run.Status);
        cmd.Parameters.AddWithValue("@TotalEligible", run.TotalEligible);
        cmd.Parameters.AddWithValue("@TotalSent", run.TotalSent);
        cmd.Parameters.AddWithValue("@TotalFailed", run.TotalFailed);
        cmd.Parameters.AddWithValue("@TotalSkipped", run.TotalSkipped);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)run.CompletedAt ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
        return run;
    }

    private static CampaignRun MapCampaignRun(SqlDataReader reader)
    {
        return new CampaignRun
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            CampaignId = reader.GetGuid(reader.GetOrdinal("campaign_id")),
            Status = reader.GetString(reader.GetOrdinal("status")),
            TotalEligible = reader.GetInt32(reader.GetOrdinal("total_eligible")),
            TotalSent = reader.GetInt32(reader.GetOrdinal("total_sent")),
            TotalFailed = reader.GetInt32(reader.GetOrdinal("total_failed")),
            TotalSkipped = reader.GetInt32(reader.GetOrdinal("total_skipped")),
            StartedAt = reader.GetDateTimeOffset(reader.GetOrdinal("started_at")),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at"))
                ? null
                : reader.GetDateTimeOffset(reader.GetOrdinal("completed_at"))
        };
    }
}
