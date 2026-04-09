using MemberSmsCampaign.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MemberSmsCampaign.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditRepository _audit;
    public AuditController(IAuditRepository audit) => _audit = audit;

    [HttpGet("recent")]
    public async Task<IActionResult> Recent([FromQuery] int count = 50, CancellationToken ct = default)
        => Ok(await _audit.GetRecentAsync(count, ct));

    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<IActionResult> ByEntity(string entityType, string entityId, CancellationToken ct = default)
        => Ok(await _audit.GetByEntityAsync(entityType, entityId, ct));

    [HttpGet("action/{action}")]
    public async Task<IActionResult> ByAction(string action, CancellationToken ct = default)
        => Ok(await _audit.GetByActionAsync(action, ct));
}
