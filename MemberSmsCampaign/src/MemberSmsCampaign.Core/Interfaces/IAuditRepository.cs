using MemberSmsCampaign.Core.Models;

namespace MemberSmsCampaign.Core.Interfaces;

public interface IAuditRepository
{
    Task LogAsync(string entityType, string entityId, string action, string? details = null, string performedBy = "system", CancellationToken ct = default);
    Task<List<AuditLog>> GetByEntityAsync(string entityType, string entityId, CancellationToken ct = default);
    Task<List<AuditLog>> GetRecentAsync(int count = 50, CancellationToken ct = default);
    Task<List<AuditLog>> GetByActionAsync(string action, CancellationToken ct = default);
}
