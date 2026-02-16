using Microsoft.AspNetCore.Authorization;
using Skojjt.Core.Authentication;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Authorization requirement that checks if the user has access to a specific scout group.
/// </summary>
public class GroupAccessRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The scout group ID to check access for. If null, checks if user has any group access.
    /// </summary>
    public int? ScoutGroupId { get; }

    public GroupAccessRequirement(int? scoutGroupId = null)
    {
        ScoutGroupId = scoutGroupId;
    }
}

/// <summary>
/// Authorization handler for GroupAccessRequirement.
/// </summary>
public class GroupAccessHandler : AuthorizationHandler<GroupAccessRequirement>
{
    private readonly ICurrentUserService _currentUserService;

    public GroupAccessHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GroupAccessRequirement requirement)
    {
        var user = _currentUserService.GetUserFromPrincipal(context.User);
        
        if (user == null)
        {
            return Task.CompletedTask;
        }

        if (requirement.ScoutGroupId == null)
        {
            // Just check if user has any group access
            if (user.AccessibleGroupIds.Count > 0)
            {
                context.Succeed(requirement);
            }
        }
        else if (user.AccessibleGroupIds.Contains(requirement.ScoutGroupId.Value))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Authorization requirement that checks if the user is a member registrar for a specific group.
/// </summary>
public class MemberRegistrarRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// The scout group ID to check member registrar status for.
    /// </summary>
    public int? ScoutGroupId { get; }

    public MemberRegistrarRequirement(int? scoutGroupId = null)
    {
        ScoutGroupId = scoutGroupId;
    }
}
