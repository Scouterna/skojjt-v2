using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Skojjt.Core.Authentication;

namespace Skojjt.Web.Services;

/// <summary>
/// Sets the Application Insights authenticated user ID from ScoutID claims
/// so the Users report correctly counts unique users in Blazor Server,
/// where most interactions happen over a single SignalR connection.
/// </summary>
public class UserTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize(ITelemetry telemetry)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity is not { IsAuthenticated: true })
            return;

        var uid = httpContext.User.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value;
        if (!string.IsNullOrEmpty(uid))
        {
            telemetry.Context.User.AuthenticatedUserId = uid;
        }
    }
}
