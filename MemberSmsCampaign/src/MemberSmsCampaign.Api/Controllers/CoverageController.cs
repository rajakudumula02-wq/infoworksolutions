using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace MemberSmsCampaign.Api.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class CoverageController : ControllerBase
{
    private readonly ICoverageService _coverageService;

    public CoverageController(ICoverageService coverageService) => _coverageService = coverageService;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCoverageRequest request, CancellationToken ct)
    {
        try
        {
            var coverage = new Coverage
            {
                MemberId = request.MemberId,
                PlanName = request.PlanName ?? string.Empty,
                Status = Enum.TryParse<CoverageStatus>(request.Status, true, out var s) ? s : CoverageStatus.Active,
                PeriodStart = DateOnly.Parse(request.PeriodStart ?? throw new ArgumentException("PeriodStart is required.")),
                PeriodEnd = request.PeriodEnd is not null ? DateOnly.Parse(request.PeriodEnd) : null,
            };
            var created = await _coverageService.CreateCoverageAsync(coverage, ct);
            return StatusCode(201, created);
        }
        catch (KeyNotFoundException ex)
        {
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid? memberId, [FromQuery] string? status, CancellationToken ct)
    {
        if (memberId.HasValue)
        {
            var coverages = await _coverageService.GetCoveragesByMemberAsync(memberId.Value, ct);
            return Ok(coverages);
        }
        if (status?.ToLower() == "active")
        {
            var active = await _coverageService.ListActiveCoveragesAsync(ct);
            return Ok(active);
        }
        var all = await _coverageService.ListActiveCoveragesAsync(ct);
        return Ok(all);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCoverageRequest request, CancellationToken ct)
    {
        try
        {
            var coverage = new Coverage { Id = id };
            if (request.PlanName is not null) coverage.PlanName = request.PlanName;
            if (request.Status is not null && Enum.TryParse<CoverageStatus>(request.Status, true, out var s))
                coverage.Status = s;
            if (request.PeriodStart is not null) coverage.PeriodStart = DateOnly.Parse(request.PeriodStart);
            coverage.PeriodEnd = request.PeriodEnd is not null ? DateOnly.Parse(request.PeriodEnd) : null;

            var updated = await _coverageService.UpdateCoverageAsync(coverage, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}

public record CreateCoverageRequest(Guid MemberId, string? PlanName, string? Status, string? PeriodStart, string? PeriodEnd);
public record UpdateCoverageRequest(string? PlanName, string? Status, string? PeriodStart, string? PeriodEnd);
