using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace MemberSmsCampaign.Api.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class MemberController : ControllerBase
{
    private readonly IMemberService _memberService;
    private readonly ICoverageService _coverageService;

    public MemberController(IMemberService memberService, ICoverageService coverageService)
    {
        _memberService = memberService;
        _coverageService = coverageService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMemberRequest request, CancellationToken ct)
    {
        try
        {
            var member = new Member
            {
                FirstName = request.FirstName ?? string.Empty,
                LastName = request.LastName ?? string.Empty,
                DateOfBirth = request.DateOfBirth is not null ? DateOnly.Parse(request.DateOfBirth) : null,
                PhoneNumber = request.PhoneNumber,
            };
            var created = await _memberService.CreateMemberAsync(member, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "VALIDATION_ERROR", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = "DUPLICATE_MEMBER", message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "INTERNAL_ERROR", message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var members = await _memberService.ListMembersAsync(ct);
        return Ok(members);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var member = await _memberService.GetMemberAsync(id, ct);
        if (member is null) return NotFound();

        var coverages = await _coverageService.GetCoveragesByMemberAsync(id, ct);
        return Ok(new { member, coverages });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMemberRequest request, CancellationToken ct)
    {
        try
        {
            var existing = await _memberService.GetMemberAsync(id, ct);
            if (existing is null) return NotFound();

            existing.FirstName = request.FirstName ?? existing.FirstName;
            existing.LastName = request.LastName ?? existing.LastName;
            existing.PhoneNumber = request.PhoneNumber ?? existing.PhoneNumber;
            if (request.DateOfBirth is not null)
                existing.DateOfBirth = DateOnly.Parse(request.DateOfBirth);

            var updated = await _memberService.UpdateMemberAsync(existing, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
    [HttpPost("{id:guid}/opt-out")]
    public async Task<IActionResult> OptOut(Guid id, CancellationToken ct)
    {
        try
        {
            var member = await _memberService.OptOutAsync(id, ct);
            return Ok(new { message = "Member opted out of SMS.", member });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/opt-in")]
    public async Task<IActionResult> OptIn(Guid id, CancellationToken ct)
    {
        try
        {
            var member = await _memberService.OptInAsync(id, ct);
            return Ok(new { message = "Member opted back in to SMS.", member });
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            var member = await _memberService.GetMemberAsync(id, ct);
            if (member is null) return NotFound();
            await _memberService.DeleteMemberAsync(id, ct);
            return Ok(new { message = $"Member #{member.MemberNumber} {member.FirstName} {member.LastName} deleted." });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (Exception ex) { return StatusCode(500, new { error = "INTERNAL_ERROR", message = ex.Message }); }
    }
}

public record CreateMemberRequest(string? FirstName, string? LastName, string? DateOfBirth, string? PhoneNumber);
public record UpdateMemberRequest(string? FirstName, string? LastName, string? DateOfBirth, string? PhoneNumber);
