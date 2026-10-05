using System.Diagnostics;
using OpenTelemetry;
using Skojjt.Core.Authentication;

namespace Skojjt.Web.Services;

/// <summary>
/// Sets the Application Insights authenticated user ID from ScoutID claims
/// so the Users report correctly counts unique users in Blazor Server,
/// where most interactions happen over a single SignalR connection.
/// The Azure Monitor exporter maps the <c>enduser.id</c> tag to the authenticated user ID.
/// </summary>
public class UserTelemetryInitializer : BaseProcessor<Activity>
{
    private const string EndUserIdTag = "enduser.id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override void OnEnd(Activity activity)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User?.Identity is not { IsAuthenticated: true })
            return;

        var uid = httpContext.User.FindFirst(ScoutIdClaimTypes.ScoutnetUid)?.Value;
        if (!string.IsNullOrEmpty(uid))
        {
            activity.SetTag(EndUserIdTag, uid);
        }
    }
}
