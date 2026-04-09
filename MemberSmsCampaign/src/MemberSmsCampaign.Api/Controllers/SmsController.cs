using MemberSmsCampaign.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MemberSmsCampaign.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SmsController : ControllerBase
{
    private readonly IManualSmsService _smsService;
    private readonly IGroupRepository _groupRepo;

    public SmsController(IManualSmsService smsService, IGroupRepository groupRepo)
    {
        _smsService = smsService;
        _groupRepo = groupRepo;
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendSmsRequest request, CancellationToken ct)
    {
        try
        {
            await _smsService.SendSingleAsync(request.MemberId, request.Message ?? string.Empty, ct);
            return Ok(new { status = "sent", memberId = request.MemberId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = "NOT_FOUND", message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            return StatusCode(503, new { error = "SERVICE_UNAVAILABLE", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "ELIGIBILITY_ERROR", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
    }

    [HttpPost("bulk")]
    public async Task<IActionResult> Bulk([FromBody] BulkSmsRequest request, CancellationToken ct)
    {
        try
        {
            await _smsService.SendBulkAsync(request.MemberIds ?? new List<Guid>(), request.Message ?? string.Empty, ct);
            return Ok(new { status = "completed", totalTargeted = request.MemberIds?.Count ?? 0 });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            return StatusCode(503, new { error = "SERVICE_UNAVAILABLE", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
    }
}

public record SendSmsRequest(Guid MemberId, string? Message);
public record BulkSmsRequest(List<Guid>? MemberIds, Guid? GroupId, string? Message);
