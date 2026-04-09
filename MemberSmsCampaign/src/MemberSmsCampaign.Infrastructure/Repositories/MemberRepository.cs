using Microsoft.Data.SqlClient;
using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Data;

namespace MemberSmsCampaign.Infrastructure.Repositories;

public class MemberRepository : IMemberRepository
{
    private readonly SqlConnectionFactory _factory;

    public MemberRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<Member> CreateAsync(Member member, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        member.Id = member.Id == Guid.Empty ? Guid.NewGuid() : member.Id;
        var now = DateTimeOffset.UtcNow;
        member.CreatedAt = now;
        member.UpdatedAt = now;

        using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"
            INSERT INTO members (id, first_name, last_name, date_of_birth, phone_number, created_at, updated_at)
            VALUES (@id, @fn, @ln, @dob, @phone, @created, @updated)";
        insertCmd.Parameters.AddWithValue("@id", member.Id);
        insertCmd.Parameters.AddWithValue("@fn", member.FirstName);
        insertCmd.Parameters.AddWithValue("@ln", member.LastName);
        insertCmd.Parameters.AddWithValue("@dob", (object?)member.DateOfBirth?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@phone", (object?)member.PhoneNumber ?? DBNull.Value);
        insertCmd.Parameters.AddWithValue("@created", member.CreatedAt);
        insertCmd.Parameters.AddWithValue("@updated", member.UpdatedAt);
        await insertCmd.ExecuteNonQueryAsync(ct);

        // Read back the full record including IDENTITY columns
        var created = await GetByIdAsync(member.Id, ct);
        return created ?? member;
    }

    public async Task<Member?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM members WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public async Task<List<Member>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM members ORDER BY last_name, first_name";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var list = new List<Member>();
        while (await reader.ReadAsync(ct)) list.Add(MapRow(reader));
        return list;
    }

    public async Task<Member> UpdateAsync(Member member, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        member.UpdatedAt = DateTimeOffset.UtcNow;
        cmd.CommandText = @"
            UPDATE members SET first_name=@fn, last_name=@ln, date_of_birth=@dob,
            phone_number=@phone, phone_status=@pstatus, phone_status_updated_at=@pdate,
            sms_failure_count=@failcount, sms_opt_out=@optout, sms_opt_out_date=@optdate,
            updated_at=@updated WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", member.Id);
        cmd.Parameters.AddWithValue("@fn", member.FirstName);
        cmd.Parameters.AddWithValue("@ln", member.LastName);
        cmd.Parameters.AddWithValue("@dob", (object?)member.DateOfBirth?.ToDateTime(TimeOnly.MinValue) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@phone", (object?)member.PhoneNumber ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pstatus", member.PhoneStatus);
        cmd.Parameters.AddWithValue("@pdate", (object?)member.PhoneStatusUpdatedAt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@failcount", member.SmsFailureCount);
        cmd.Parameters.AddWithValue("@optout", member.SmsOptOut);
        cmd.Parameters.AddWithValue("@optdate", (object?)member.SmsOptOutDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@updated", member.UpdatedAt);
        await cmd.ExecuteNonQueryAsync(ct);
        return member;
    }

    public async Task<Member?> FindDuplicateAsync(string firstName, string lastName, string? phoneNumber, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();

        // Match by name OR phone number
        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            cmd.CommandText = @"SELECT TOP 1 * FROM members
                WHERE (LOWER(first_name)=LOWER(@fn) AND LOWER(last_name)=LOWER(@ln))
                   OR phone_number=@phone";
            cmd.Parameters.AddWithValue("@fn", firstName);
            cmd.Parameters.AddWithValue("@ln", lastName);
            cmd.Parameters.AddWithValue("@phone", phoneNumber);
        }
        else
        {
            cmd.CommandText = @"SELECT TOP 1 * FROM members
                WHERE LOWER(first_name)=LOWER(@fn) AND LOWER(last_name)=LOWER(@ln)";
            cmd.Parameters.AddWithValue("@fn", firstName);
            cmd.Parameters.AddWithValue("@ln", lastName);
        }

        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapRow(reader) : null;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        await conn.OpenAsync(ct);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM members WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Member MapRow(SqlDataReader r) => new()
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
    };
}
