using Microsoft.Data.SqlClient;
using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Data;

namespace MemberSmsCampaign.Infrastructure.Repositories;

public class GroupRepository : IGroupRepository
{
    private readonly SqlConnectionFactory _factory;
    public GroupRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<MemberGroup> CreateAsync(MemberGroup group, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        group.Id = group.Id == Guid.Empty ? Guid.NewGuid() : group.Id;
        var now = DateTimeOffset.UtcNow;
        group.CreatedAt = now; group.UpdatedAt = now;
        cmd.CommandText = @"INSERT INTO member_groups (id,name,description,created_at,updated_at) VALUES (@id,@name,@desc,@c,@u);
            SELECT group_number FROM member_groups WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", group.Id);
        cmd.Parameters.AddWithValue("@name", group.Name);
        cmd.Parameters.AddWithValue("@desc", (object?)group.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@c", group.CreatedAt);
        cmd.Parameters.AddWithValue("@u", group.UpdatedAt);
        var groupNumber = await cmd.ExecuteScalarAsync(ct);
        group.GroupNumber = Convert.ToInt32(groupNumber);
        return group;
    }

    public async Task<MemberGroup?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT g.*, (SELECT COUNT(*) FROM group_members gm WHERE gm.group_id=g.id) AS member_count FROM member_groups g WHERE g.id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? MapRow(r) : null;
    }

    public async Task<List<MemberGroup>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT g.*, (SELECT COUNT(*) FROM group_members gm WHERE gm.group_id=g.id) AS member_count FROM member_groups g ORDER BY g.name";
        using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<MemberGroup>();
        while (await r.ReadAsync(ct)) list.Add(MapRow(r));
        return list;
    }

    public async Task<MemberGroup> UpdateAsync(MemberGroup group, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        group.UpdatedAt = DateTimeOffset.UtcNow;
        cmd.CommandText = "UPDATE member_groups SET name=@name, description=@desc, updated_at=@u WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", group.Id);
        cmd.Parameters.AddWithValue("@name", group.Name);
        cmd.Parameters.AddWithValue("@desc", (object?)group.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@u", group.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return group;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM member_groups WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task AddMemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"IF NOT EXISTS (SELECT 1 FROM group_members WHERE group_id=@gid AND member_id=@mid)
            INSERT INTO group_members (group_id, member_id) VALUES (@gid, @mid)";
        cmd.Parameters.AddWithValue("@gid", groupId);
        cmd.Parameters.AddWithValue("@mid", memberId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid groupId, Guid memberId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM group_members WHERE group_id=@gid AND member_id=@mid";
        cmd.Parameters.AddWithValue("@gid", groupId);
        cmd.Parameters.AddWithValue("@mid", memberId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<List<Member>> GetMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT m.* FROM members m
            INNER JOIN group_members gm ON gm.member_id = m.id
            WHERE gm.group_id = @gid ORDER BY m.last_name, m.first_name";
        cmd.Parameters.AddWithValue("@gid", groupId);
        using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Member>();
        while (await r.ReadAsync(ct)) list.Add(new Member
        {
            Id = r.GetGuid(r.GetOrdinal("id")),
            MemberNumber = r.GetInt32(r.GetOrdinal("member_number")),
            FirstName = r.GetString(r.GetOrdinal("first_name")),
            LastName = r.GetString(r.GetOrdinal("last_name")),
            DateOfBirth = r.IsDBNull(r.GetOrdinal("date_of_birth")) ? null : DateOnly.FromDateTime(r.GetDateTime(r.GetOrdinal("date_of_birth"))),
            PhoneNumber = r.IsDBNull(r.GetOrdinal("phone_number")) ? null : r.GetString(r.GetOrdinal("phone_number")),
            PhoneStatus = r.GetString(r.GetOrdinal("phone_status")),
            PhoneStatusUpdatedAt = r.IsDBNull(r.GetOrdinal("phone_status_updated_at")) ? null : r.GetDateTimeOffset(r.GetOrdinal("phone_status_updated_at")),
            SmsFailureCount = r.GetInt32(r.GetOrdinal("sms_failure_count")),
            SmsOptOut = r.GetBoolean(r.GetOrdinal("sms_opt_out")),
            SmsOptOutDate = r.IsDBNull(r.GetOrdinal("sms_opt_out_date")) ? null : r.GetDateTimeOffset(r.GetOrdinal("sms_opt_out_date")),
            CreatedAt = r.GetDateTimeOffset(r.GetOrdinal("created_at")),
            UpdatedAt = r.GetDateTimeOffset(r.GetOrdinal("updated_at")),
        });
        return list;
    }

    public async Task<List<Guid>> GetMemberIdsAsync(Guid groupId, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT member_id FROM group_members WHERE group_id=@gid";
        cmd.Parameters.AddWithValue("@gid", groupId);
        using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Guid>();
        while (await r.ReadAsync(ct)) list.Add(r.GetGuid(0));
        return list;
    }

    private static MemberGroup MapRow(SqlDataReader r) => new()
    {
        Id = r.GetGuid(r.GetOrdinal("id")),
        GroupNumber = r.GetInt32(r.GetOrdinal("group_number")),
        Name = r.GetString(r.GetOrdinal("name")),
        Description = r.IsDBNull(r.GetOrdinal("description")) ? null : r.GetString(r.GetOrdinal("description")),
        MemberCount = r.GetInt32(r.GetOrdinal("member_count")),
        CreatedAt = r.GetDateTimeOffset(r.GetOrdinal("created_at")),
        UpdatedAt = r.GetDateTimeOffset(r.GetOrdinal("updated_at")),
    };
}
