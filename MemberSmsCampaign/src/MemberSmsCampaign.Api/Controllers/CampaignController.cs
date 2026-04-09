using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using MemberSmsCampaign.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace MemberSmsCampaign.Api.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class CampaignController : ControllerBase
{
    private readonly ICampaignService _campaignService;
    private readonly ICampaignRepository _campaignRepo;

    public CampaignController(ICampaignService campaignService, ICampaignRepository campaignRepo)
    {
        _campaignService = campaignService;
        _campaignRepo = campaignRepo;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCampaignRequest request, CancellationToken ct)
    {
        var errors = ValidationService.ValidateCampaignInput(request.Name, request.Type, request.MessageTemplate);
        if (errors.Count > 0)
            return BadRequest(new { error = "VALIDATION_ERROR", details = errors });

        var campaignType = Enum.Parse<CampaignType>(request.Type!, true);
        var campaign = await _campaignService.CreateCampaignAsync(request.Name!, campaignType, request.MessageTemplate!, ct);
        return CreatedAtAction(nameof(GetById), new { id = campaign.Id }, campaign);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var campaigns = await _campaignService.ListCampaignsAsync(ct);
        return Ok(campaigns);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var campaign = await _campaignService.GetCampaignAsync(id, ct);
        if (campaign is null)
            return NotFound();
        return Ok(campaign);
    }

    [HttpPut("{id:guid}/schedule")]
    public async Task<IActionResult> Schedule(Guid id, [FromBody] ScheduleCampaignRequest request, CancellationToken ct)
    {
        try
        {
            var campaign = await _campaignService.ScheduleCampaignAsync(id, request.ScheduledAt, ct);
            return Ok(campaign);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "INVALID_STATE_TRANSITION", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        try
        {
            var campaign = await _campaignService.CancelCampaignAsync(id, ct);
            return Ok(campaign);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "INVALID_STATE_TRANSITION", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMembers(Guid id, [FromBody] AddCampaignMembersRequest request, CancellationToken ct)
    {
        var campaign = await _campaignService.GetCampaignAsync(id, ct);
        if (campaign is null) return NotFound();
        if (request.MemberIds is null || request.MemberIds.Count == 0)
            return BadRequest(new { error = "VALIDATION_ERROR", message = "At least one member ID is required." });

        campaign.TargetingMode = "manual";
        campaign.UpdatedAt = DateTimeOffset.UtcNow;
        await _campaignRepo.UpdateAsync(campaign, ct);
        await _campaignRepo.AddMembersAsync(id, request.MemberIds, ct);
        return Ok(new { message = $"{request.MemberIds.Count} member(s) added to campaign.", targetingMode = "manual" });
    }

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken ct)
    {
        var campaign = await _campaignService.GetCampaignAsync(id, ct);
        if (campaign is null) return NotFound();
        var memberIds = await _campaignRepo.GetCampaignMemberIdsAsync(id, ct);
        return Ok(new { campaignId = id, targetingMode = campaign.TargetingMode, memberIds, count = memberIds.Count });
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        await _campaignRepo.RemoveMemberAsync(id, memberId, ct);
        return Ok(new { message = "Member removed from campaign." });
    }

    [HttpPost("{id:guid}/run")]
    public async Task<IActionResult> RunNow(Guid id, CancellationToken ct)
    {
        try
        {
            await _campaignService.ExecuteCampaignRunAsync(id, ct);
            return Ok(new { message = "Campaign executed successfully." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) { return StatusCode(500, new { error = "EXECUTION_ERROR", message = ex.Message }); }
    }
}

public record CreateCampaignRequest(string? Name, string? Type, string? MessageTemplate);
public record ScheduleCampaignRequest(DateTimeOffset ScheduledAt);
public record AddCampaignMembersRequest(List<Guid>? MemberIds);
