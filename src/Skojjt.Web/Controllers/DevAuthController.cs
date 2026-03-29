using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Core.Authentication;
using Skojjt.Infrastructure.Authentication;
using System.Security.Claims;
using System.Text.Json;

namespace Skojjt.Web.Controllers;

[Route("dev-auth")]
public class DevAuthController : Controller
{
    private readonly IScoutIdSimulator? _scoutIdSimulator;
    private readonly ILogger<DevAuthController> _logger;

    public DevAuthController(
        ILogger<DevAuthController> logger,
        IScoutIdSimulator? scoutIdSimulator = null)
    {
        _scoutIdSimulator = scoutIdSimulator;
        _logger = logger;
    }

    [HttpGet("login")]
    public IActionResult LoginPage([FromQuery] string? returnUrl)
    {
        return Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl ?? "/")}");
    }

    [HttpPost("login")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login(
        [FromForm] string name, 
        [FromForm] string email, 
        [FromForm] bool isAdmin = false,
        [FromForm] bool isMemberRegistrar = false,
        [FromForm] int groupId = 1001,
        [FromForm] string? accessibleGroups = null,
        [FromForm] string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name)) name = "Test User";
        if (string.IsNullOrWhiteSpace(email)) email = "test@test.se";

        var uid = email; // Use email as stable identifier for dev
        var groupNo = groupId.ToString();
        
        // Parse accessible groups
        var accessibleGroupIds = new List<int> { groupId };
        if (!string.IsNullOrEmpty(accessibleGroups))
        {
            accessibleGroupIds = accessibleGroups
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            
            if (!accessibleGroupIds.Contains(groupId))
                accessibleGroupIds.Insert(0, groupId);
        }

        // Build group roles
        var groupRoles = new Dictionary<string, List<string>>();
        if (isMemberRegistrar || isAdmin)
        {
            groupRoles[groupId.ToString()] = [ScoutIdRoles.MemberRegistrar];
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Email, email),
            new("sub", uid),
            
            // ScoutID claims
            new(ScoutIdClaimTypes.ScoutnetUid, uid),
            new(ScoutIdClaimTypes.DisplayName, name),
            //new(ScoutIdClaimTypes.GroupNo, groupNo),
            //new(ScoutIdClaimTypes.GroupId, groupId.ToString()),
            new(ScoutIdClaimTypes.AccessibleGroups, string.Join(",", accessibleGroupIds)),
            //new(ScoutIdClaimTypes.GroupRoles, JsonSerializer.Serialize(groupRoles)),
        };

        if (isMemberRegistrar || isAdmin)
        {
            claims.Add(new Claim(ScoutIdClaimTypes.MemberRegistrarGroups, groupId.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, "MemberRegistrar"));
        }

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Set persistent cookie that lasts 30 days
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        // User sync to database is handled by ScoutIdClaimsTransformation
        // on the first request after login.

        return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    [HttpGet("quick-login/{role}")]
    public async Task<IActionResult> QuickLogin(string role, [FromQuery] string? returnUrl)
    {
        // Try to use simulated ScoutID service if available
        if (_scoutIdSimulator != null)
        {
            var user = role.ToLower() switch
            {
                "admin" => _scoutIdSimulator.GetUserByEmail("admin@test.scout.se"),
                "registrar" => _scoutIdSimulator.GetUserByEmail("admin@test.scout.se"),
                "multi" => _scoutIdSimulator.GetUserByEmail("multi@test.scout.se"),
                "readonly" => _scoutIdSimulator.GetUserByEmail("readonly@test.scout.se"),
                _ => _scoutIdSimulator.GetUserByEmail("ledare@test.scout.se")
            };

            if (user != null)
            {
                return await LoginWithSimulatedUser(user, returnUrl);
            }
        }

        // Fallback to simple login
        var (name, email, isAdmin, isMemberRegistrar, groupId) = role.ToLower() switch
        {
            "admin" => ("Test Admin", "admin@test.se", true, true, 1001),
            "registrar" => ("Test Registrar", "registrar@test.se", false, true, 1001),
            _ => ("Test Ledare", "ledare@test.se", false, false, 1001)
        };

        return await Login(name, email, isAdmin, isMemberRegistrar, groupId, null, returnUrl);
    }

    [HttpPost("login-simulated")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> LoginSimulated([FromForm] string uid, [FromForm] string? returnUrl = null)
    {
        if (_scoutIdSimulator == null)
        {
            return BadRequest("Simulated ScoutID service not available");
        }

        var user = _scoutIdSimulator.GetUserByUid(uid);
        if (user == null)
        {
            return NotFound($"Simulated user with UID {uid} not found");
        }

        return await LoginWithSimulatedUser(user, returnUrl);
    }

    [HttpGet("users")]
    public IActionResult GetAvailableUsers()
    {
        if (_scoutIdSimulator == null)
        {
            return Ok(new[] 
            {
                new { Uid = "admin", Email = "admin@test.se", DisplayName = "Test Admin", IsMemberRegistrar = true },
                new { Uid = "ledare", Email = "ledare@test.se", DisplayName = "Test Ledare", IsMemberRegistrar = false }
            });
        }

        return Ok(_scoutIdSimulator.GetAvailableUsers().Select(u => new
        {
            u.Uid,
            u.Email,
            u.DisplayName,
            u.GroupId,
            u.IsMemberRegistrar,
            u.AccessibleGroupIds
        }));
    }

    private async Task<IActionResult> LoginWithSimulatedUser(SimulatedScoutIdUser user, string? returnUrl)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email),
            new("sub", user.Uid),
            
            // ScoutID claims
            new(ScoutIdClaimTypes.ScoutnetUid, user.Uid),
            new(ScoutIdClaimTypes.DisplayName, user.DisplayName),
            new(ScoutIdClaimTypes.AccessibleGroups, string.Join(",", user.AccessibleGroupIds)),
        };

        if (user.IsMemberRegistrar)
        {
            claims.Add(new Claim(ScoutIdClaimTypes.MemberRegistrarGroups, user.GroupId.ToString()));
            claims.Add(new Claim(ClaimTypes.Role, "MemberRegistrar"));
        }

        // Note: SkojjtAdmin claim will be added by ScoutIdClaimsTransformation based on User.IsAdmin in database

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);

        // User sync to database is handled by ScoutIdClaimsTransformation
        // on the first request after login.

        return LocalRedirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    }

    [HttpGet("logout")]
    public IActionResult Logout([FromQuery] string? returnUrl)
    {
        // Redirect to unified logout endpoint
        var redirectUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        return Redirect($"/auth/signout?returnUrl={Uri.EscapeDataString(redirectUrl)}");
    }
}

public record DevLoginRequest(string Name, string Email, bool IsAdmin, bool IsMemberRegistrar, int GroupId, string? AccessibleGroups, string? ReturnUrl);
