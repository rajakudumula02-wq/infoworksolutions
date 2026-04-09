using Microsoft.Data.SqlClient;
using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Data;

namespace MemberSmsCampaign.Infrastructure.Repositories;

public class CampaignRepository : ICampaignRepository
{
    private readonly SqlConnectionFactory _factory;

    public CampaignRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<Campaign> CreateAsync(Campaign campaign, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO campaigns (id, name, type, message_template, status, scheduled_at, created_at, updated_at)
            VALUES (@id, @name, @type, @msg, @status, @sched, @created, @updated)";
        cmd.Parameters.AddWithValue("@id", campaign.Id);
        cmd.Parameters.AddWithValue("@name", campaign.Name);
        cmd.Parameters.AddWithValue("@type", campaign.Type.ToString().ToLower());
        cmd.Parameters.AddWithValue("@msg", campaign.MessageTemplate);
        cmd.Parameters.AddWithValue("@status", campaign.Status.ToString().ToLower());
        cmd.Parameters.AddWithValue("@sched", (object?)campaign.ScheduledAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@created", campaign.CreatedAt);
        cmd.Parameters.AddWithValue("@updated", campaign.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return campaign;
    }

    public async Task<Campaign?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM campaigns WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public async Task<List<Campaign>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM campaigns ORDER BY created_at DESC";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Campaign>();
        while (await reader.ReadAsync(ct)) list.Add(MapRow(reader));
        return list;
    }

    public async Task<Campaign> UpdateAsync(Campaign campaign, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE campaigns SET name=@name, type=@type, message_template=@msg,
            status=@status, scheduled_at=@sched, updated_at=@updated WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", campaign.Id);
        cmd.Parameters.AddWithValue("@name", campaign.Name);
        cmd.Parameters.AddWithValue("@type", campaign.Type.ToString().ToLower());
        cmd.Parameters.AddWithValue("@msg", campaign.MessageTemplate);
        cmd.Parameters.AddWithValue("@status", campaign.Status.ToString().ToLower());
        cmd.Parameters.AddWithValue("@sched", (object?)campaign.ScheduledAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated", campaign.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return campaign;
    }

    public async Task<Campaign?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 * FROM campaigns WHERE LOWER(name)=LOWER(@name)";
        cmd.Parameters.AddWithValue("@name", name);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM campaigns WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static CampaignType ParseType(string s) => s.ToLower() switch
    {
        "welcome" => CampaignType.Welcome,
        "referral" => CampaignType.Referral,
        "utilization" => CampaignType.Utilization,
        "holiday" => CampaignType.Holiday,
        _ => CampaignType.Welcome,
    };

    private static CampaignStatus ParseStatus(string s) => s.ToLower() switch
    {
        "draft" => CampaignStatus.Draft,
        "scheduled" => CampaignStatus.Scheduled,
        "running" => CampaignStatus.Running,
        "completed" => CampaignStatus.Completed,
        "cancelled" => CampaignStatus.Cancelled,
        _ => CampaignStatus.Draft,
    };

    private static Campaign MapRow(SqlDataReader r)
    {
        var campaign = new Campaign
        {
            Id = r.GetGuid(r.GetOrdinal("id")),
            Name = r.GetString(r.GetOrdinal("name")),
            Type = ParseType(r.GetString(r.GetOrdinal("type"))),
            MessageTemplate = r.GetString(r.GetOrdinal("message_template")),
            Status = ParseStatus(r.GetString(r.GetOrdinal("status"))),
            ScheduledAt = r.IsDBNull(r.GetOrdinal("scheduled_at")) ? null : r.GetDateTimeOffset(r.GetOrdinal("scheduled_at")),
            CreatedAt = r.GetDateTimeOffset(r.GetOrdinal("created_at")),
            UpdatedAt = r.GetDateTimeOffset(r.GetOrdinal("updated_at")),
        };
        try { campaign.TargetingMode = r.GetString(r.GetOrdinal("targeting_mode")); } catch { }
        return campaign;
    }

    public async Task AddMembersAsync(Guid campaignId, List<Guid> memberIds, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        foreach (var memberId in memberIds)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"IF NOT EXISTS (SELECT 1 FROM campaign_members WHERE campaign_id=@cid AND member_id=@mid)
                INSERT INTO campaign_members (campaign_id, member_id) VALUES (@cid, @mid)";
            cmd.Parameters.AddWithValue("@cid", campaignId);
            cmd.Parameters.AddWithValue("@mid", memberId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task RemoveMemberAsync(Guid campaignId, Guid memberId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM campaign_members WHERE campaign_id=@cid AND member_id=@mid";
        cmd.Parameters.AddWithValue("@cid", campaignId);
        cmd.Parameters.AddWithValue("@mid", memberId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<Guid>> GetCampaignMemberIdsAsync(Guid campaignId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT member_id FROM campaign_members WHERE campaign_id=@cid";
        cmd.Parameters.AddWithValue("@cid", campaignId);
        using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Guid>();
        while (await r.ReadAsync(ct)) list.Add(r.GetGuid(0));
        return list;
    }
}
