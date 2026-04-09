using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace MemberSmsCampaign.Infrastructure.Repositories;

public class ManualSmsLogRepository : IManualSmsLogRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public ManualSmsLogRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ManualSmsLog> CreateAsync(ManualSmsLog log, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO manual_sms_logs (id, type, message, total_sent, total_failed, total_skipped, created_at, completed_at)
            VALUES (@Id, @Type, @Message, @TotalSent, @TotalFailed, @TotalSkipped, @CreatedAt, @CompletedAt)";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", log.Id);
        cmd.Parameters.AddWithValue("@Type", log.Type);
        cmd.Parameters.AddWithValue("@Message", log.Message);
        cmd.Parameters.AddWithValue("@TotalSent", log.TotalSent);
        cmd.Parameters.AddWithValue("@TotalFailed", log.TotalFailed);
        cmd.Parameters.AddWithValue("@TotalSkipped", log.TotalSkipped);
        cmd.Parameters.AddWithValue("@CreatedAt", log.CreatedAt);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)log.CompletedAt ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
        return log;
    }

    public async Task<ManualSmsLog?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        const string sql = "SELECT id, type, message, total_sent, total_failed, total_skipped, created_at, completed_at FROM manual_sms_logs WHERE id = @Id";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", id);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
            return MapManualSmsLog(reader);

        return null;
    }

    public async Task<ManualSmsLog> UpdateAsync(ManualSmsLog log, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE manual_sms_logs
            SET total_sent = @TotalSent, total_failed = @TotalFailed, total_skipped = @TotalSkipped, completed_at = @CompletedAt
            WHERE id = @Id";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", log.Id);
        cmd.Parameters.AddWithValue("@TotalSent", log.TotalSent);
        cmd.Parameters.AddWithValue("@TotalFailed", log.TotalFailed);
        cmd.Parameters.AddWithValue("@TotalSkipped", log.TotalSkipped);
        cmd.Parameters.AddWithValue("@CompletedAt", (object?)log.CompletedAt ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
        return log;
    }

    private static ManualSmsLog MapManualSmsLog(SqlDataReader reader)
    {
        return new ManualSmsLog
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            Type = reader.GetString(reader.GetOrdinal("type")),
            Message = reader.GetString(reader.GetOrdinal("message")),
            TotalSent = reader.GetInt32(reader.GetOrdinal("total_sent")),
            TotalFailed = reader.GetInt32(reader.GetOrdinal("total_failed")),
            TotalSkipped = reader.GetInt32(reader.GetOrdinal("total_skipped")),
            CreatedAt = reader.GetDateTimeOffset(reader.GetOrdinal("created_at")),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at"))
                ? null
                : reader.GetDateTimeOffset(reader.GetOrdinal("completed_at"))
        };
    }
}
