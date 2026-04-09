using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace MemberSmsCampaign.Infrastructure.Repositories;

public class DeliveryRecordRepository : IDeliveryRecordRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public DeliveryRecordRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DeliveryRecord> CreateAsync(DeliveryRecord record, CancellationToken ct = default)
    {
        const string sql = @"
            INSERT INTO delivery_records (id, campaign_run_id, manual_sms_id, member_id, phone_number, status, reason, sent_at, created_at)
            VALUES (@Id, @CampaignRunId, @ManualSmsId, @MemberId, @PhoneNumber, @Status, @Reason, @SentAt, @CreatedAt)";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", record.Id);
        cmd.Parameters.AddWithValue("@CampaignRunId", (object?)record.CampaignRunId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ManualSmsId", (object?)record.ManualSmsId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@MemberId", record.MemberId);
        cmd.Parameters.AddWithValue("@PhoneNumber", (object?)record.PhoneNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Status", record.Status.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("@Reason", (object?)record.Reason ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@SentAt", (object?)record.SentAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedAt", record.CreatedAt);

        await cmd.ExecuteNonQueryAsync(ct);
        return record;
    }

    public async Task<List<DeliveryRecord>> GetByRunIdAsync(Guid campaignRunId, CancellationToken ct = default)
    {
        const string sql = "SELECT id, campaign_run_id, manual_sms_id, member_id, phone_number, status, reason, sent_at, created_at FROM delivery_records WHERE campaign_run_id = @CampaignRunId";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@CampaignRunId", campaignRunId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var records = new List<DeliveryRecord>();
        while (await reader.ReadAsync(ct))
            records.Add(MapDeliveryRecord(reader));

        return records;
    }

    public async Task<List<DeliveryRecord>> GetByManualSmsIdAsync(Guid manualSmsId, CancellationToken ct = default)
    {
        const string sql = "SELECT id, campaign_run_id, manual_sms_id, member_id, phone_number, status, reason, sent_at, created_at FROM delivery_records WHERE manual_sms_id = @ManualSmsId";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(ct);

        await using var cmd = new SqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@ManualSmsId", manualSmsId);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var records = new List<DeliveryRecord>();
        while (await reader.ReadAsync(ct))
            records.Add(MapDeliveryRecord(reader));

        return records;
    }

    private static DeliveryRecord MapDeliveryRecord(SqlDataReader reader)
    {
        var statusStr = reader.GetString(reader.GetOrdinal("status"));
        var status = Enum.Parse<DeliveryStatus>(statusStr, ignoreCase: true);

        return new DeliveryRecord
        {
            Id = reader.GetGuid(reader.GetOrdinal("id")),
            CampaignRunId = reader.IsDBNull(reader.GetOrdinal("campaign_run_id"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("campaign_run_id")),
            ManualSmsId = reader.IsDBNull(reader.GetOrdinal("manual_sms_id"))
                ? null
                : reader.GetGuid(reader.GetOrdinal("manual_sms_id")),
            MemberId = reader.GetString(reader.GetOrdinal("member_id")),
            PhoneNumber = reader.IsDBNull(reader.GetOrdinal("phone_number"))
                ? null
                : reader.GetString(reader.GetOrdinal("phone_number")),
            Status = status,
            Reason = reader.IsDBNull(reader.GetOrdinal("reason"))
                ? null
                : reader.GetString(reader.GetOrdinal("reason")),
            SentAt = reader.IsDBNull(reader.GetOrdinal("sent_at"))
                ? null
                : reader.GetDateTimeOffset(reader.GetOrdinal("sent_at")),
            CreatedAt = reader.GetDateTimeOffset(reader.GetOrdinal("created_at"))
        };
    }
}
