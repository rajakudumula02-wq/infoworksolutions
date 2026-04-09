using MemberSmsCampaign.Core.Interfaces;
using MemberSmsCampaign.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace MemberSmsCampaign.Api.Controllers;

[ApiController]
[Route("api/[controller]s")]
public class GroupController : ControllerBase
{
    private readonly IGroupRepository _repo;

    public GroupController(IGroupRepository repo) => _repo = repo;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "VALIDATION_ERROR", message = "Name is required." });

        var group = new MemberGroup { Name = request.Name, Description = request.Description };
        var created = await _repo.CreateAsync(group, ct);
        return StatusCode(201, created);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _repo.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var group = await _repo.GetByIdAsync(id, ct);
        if (group is null) return NotFound();
        var members = await _repo.GetMembersAsync(id, ct);
        return Ok(new { group, members });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateGroupRequest request, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null) return NotFound();
        existing.Name = request.Name ?? existing.Name;
        existing.Description = request.Description ?? existing.Description;
        return Ok(await _repo.UpdateAsync(existing, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _repo.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> AddMember(Guid id, Guid memberId, CancellationToken ct)
    {
        await _repo.AddMemberAsync(id, memberId, ct);
        return Ok(new { message = "Member added to group." });
    }

    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken ct)
    {
        await _repo.RemoveMemberAsync(id, memberId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/memberIds")]
    public async Task<IActionResult> GetMemberIds(Guid id, CancellationToken ct)
        => Ok(await _repo.GetMemberIdsAsync(id, ct));
}

public record CreateGroupRequest(string? Name, string? Description);
