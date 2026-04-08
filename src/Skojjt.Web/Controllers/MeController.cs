using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Core.Authentication;
using Skojjt.Core.Interfaces;
using Skojjt.Shared.DTOs;

namespace Skojjt.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IScoutGroupRepository _scoutGroupRepository;

    public MeController(
        ICurrentUserService currentUserService,
        IScoutGroupRepository scoutGroupRepository)
    {
        _currentUserService = currentUserService;
        _scoutGroupRepository = scoutGroupRepository;
    }

    /// <summary>
    /// Returns the current user's identity and accessible resources.
    /// Used by external tools (e.g., Tampermonkey script) to discover what the user can access.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<MeResponseDto>> GetMe(CancellationToken ct)
    {
        var user = _currentUserService.GetCurrentUser();
        if (user == null)
            return Unauthorized();

        var groupIds = _currentUserService.GetAccessibleGroupIds();
        var troopScoutnetIds = _currentUserService.GetAccessibleTroopIds();

        // Fetch accessible scout groups in a single query
        var scoutGroups = await _scoutGroupRepository.FindAsync(g => groupIds.Contains(g.Id), ct);
        var groups = scoutGroups
            .Select(g => new ScoutGroupDto(g.Id, g.Name, g.OrganisationNumber, g.Email, g.Phone))
            .ToList();

        return Ok(new MeResponseDto(
            user.Uid,
            user.DisplayName,
            user.Email,
            _currentUserService.IsAnyMemberRegistrar,
            groups,
            [.. troopScoutnetIds]
        ));
    }
}
