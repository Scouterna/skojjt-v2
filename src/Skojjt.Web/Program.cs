using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using MudBlazor;
using MudBlazor.Services;
using Skojjt.Core.Authentication;
using Skojjt.Core.Interfaces;
using Skojjt.Core.Services;
using Skojjt.Infrastructure;
using Skojjt.Infrastructure.Authentication;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Repositories;
using Skojjt.Infrastructure.Services;
using Skojjt.Web.Components;
using Skojjt.Web.Hubs;
using Skojjt.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Log startup diagnostics
var startupLogger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("Startup");
startupLogger.LogInformation("Starting Skojjt.Web, Environment: {Env}", builder.Environment.EnvironmentName);
startupLogger.LogInformation("ConnectionString configured: {HasCs}", !string.IsNullOrEmpty(builder.Configuration.GetConnectionString("DefaultConnection")));
startupLogger.LogInformation("ScoutId Authority: {Authority}", builder.Configuration["ScoutId:Authority"]);

// Add MudBlazor services with Swedish localization
builder.Services.AddMudServices();
builder.Services.AddTransient<MudLocalizer, SwedishMudLocalizer>();

// Configure request localization to prevent CultureNotFoundException from malformed
// Accept-Language headers (e.g. bots/scanners sending binary garbage).
// This ensures all requests use a known-good culture before reaching Blazor's
// ServerComponentSerializer, which would otherwise crash on invalid culture names.
var supportedCultures = new[] { new CultureInfo("sv-SE"), new CultureInfo("en") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("sv-SE");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});

// Add Razor components with interactive server rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add API controllers
builder.Services.AddControllers();

// Add SignalR for real-time updates
builder.Services.AddSignalR();

// Add response compression for SignalR
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(["application/octet-stream"]);
});

// Add HttpContextAccessor for authentication in Blazor components
builder.Services.AddHttpContextAccessor();

// Register CurrentUserService for accessing authenticated user's ScoutID information
builder.Services.AddScoped<IAdminModeService, AdminModeService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register state services as singletons for real-time sync between Blazor circuits
builder.Services.AddSingleton<AttendanceStateService>();
builder.Services.AddSingleton<BadgeStateService>();

// Register ThemeService as scoped for theme management per user session
builder.Services.AddScoped<ThemeService>();

// Register notification services for broadcasting changes
builder.Services.AddScoped<AttendanceNotificationService>();
builder.Services.AddScoped<BadgeNotificationService>();

// Configure PostgreSQL with Entity Framework Core
// AddDbContextFactory registers both IDbContextFactory<T> AND DbContext for direct injection
builder.Services.AddDbContextFactory<SkojjtDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
	npgsqlOptions =>
	{
		npgsqlOptions.EnableRetryOnFailure(
			maxRetryCount: 5,
			maxRetryDelay: TimeSpan.FromSeconds(30),
			errorCodesToAdd: null);
		npgsqlOptions.CommandTimeout(180);  // 3 minutes for long import operations
	}
	));

// Register repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISemesterRepository, SemesterRepository>();
builder.Services.AddScoped<IScoutGroupRepository, ScoutGroupRepository>();
builder.Services.AddScoped<IPersonRepository, PersonRepository>();
builder.Services.AddScoped<ITroopRepository, TroopRepository>();
builder.Services.AddScoped<IMeetingRepository, MeetingRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IBadgeRepository, BadgeRepository>();
builder.Services.AddScoped<IBadgeTemplateRepository, BadgeTemplateRepository>();

// Register services
builder.Services.AddScoped<DataMigrationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IBadgeService, BadgeService>();
builder.Services.AddScoped<IMyProfileService, MyProfileService>();
builder.Services.AddScoutnetServices(builder.Configuration);
builder.Services.AddExportServices();

// Register user sync service for syncing ScoutID users to database on login
builder.Services.AddScoped<IUserSyncService, UserSyncService>();

// Configure authentication based on environment and configuration
var useDevAuth = builder.Environment.IsDevelopment() && 
    string.IsNullOrEmpty(builder.Configuration["ScoutId:ClientId"]) &&
    !builder.Configuration.GetValue<bool>("ScoutIdSaml:Enabled");
var useSaml = builder.Configuration.GetValue<bool>("ScoutIdSaml:Enabled");

// Enable detailed identity errors in development for debugging OIDC/SAML issues
if (builder.Environment.IsDevelopment())
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

if (useDevAuth)
{
    // Use cookie-based fake authentication for development without ScoutID
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/login";
            options.LogoutPath = "/auth/signout";
        });

    // Register simulated ScoutID service for development
    builder.Services.AddSingleton<IScoutIdSimulator, FakeScoutIdService>();
}
else if (useSaml)
{
    // Configure SimpleSAML-based ScoutID authentication (SAML 2.0)
    // This is the current production ScoutID version based on SimpleSAMLphp.
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = SamlAuthenticationExtensions.Saml2Scheme;
    })
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
    .AddScoutIdSaml(builder.Configuration, builder.Environment.IsDevelopment());
}
else
{
    // Configure ScoutID authentication (OAuth 2.0 / OIDC) for production
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    })
	.AddOpenIdConnect(options =>
	{
		options.Authority = builder.Configuration["ScoutId:Authority"];
		options.ClientId = builder.Configuration["ScoutId:ClientId"];
		options.ClientSecret = builder.Configuration["ScoutId:ClientSecret"];
		options.ResponseType = OpenIdConnectResponseType.Code;
		options.SaveTokens = true;
		options.GetClaimsFromUserInfoEndpoint = true;

		// Disable Pushed Authorization Requests (PAR) - ScoutID advertises PAR
		// support but rejects the requests with 'invalid_request'
		options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.Disable;
      
        // Allow HTTP for local development (e.g., http://localhost:8080)
        // In production, Authority should always use HTTPS
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
        }
        
        
        // Configure OIDC scopes - clear defaults to avoid duplicates
        options.Scope.Clear();
        var scoutIdScope = builder.Configuration["ScoutId:Scope"];
		if (!string.IsNullOrEmpty(scoutIdScope))
		{
			foreach (var scope in scoutIdScope.Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				options.Scope.Add(scope);
			}
		}
		else
		{
			// Default OIDC scopes
			options.Scope.Add("openid");
			options.Scope.Add("profile");
			options.Scope.Add("email");
		}

		options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            NameClaimType = "name",
            // Use full URI to match mapped claim types (MapInboundClaims=true by default)
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };

        options.Events = new OpenIdConnectEvents
        {
            OnTokenValidated = async context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                var name = context.Principal?.Identity?.Name ?? "unknown";
                logger.LogInformation("User {Name} authenticated via ScoutID", name);
                
                // Log all claims for debugging
                if (context.Principal != null)
                {
                    logger.LogDebug("=== ScoutID Claims for {Name} ===", name);
                    foreach (var claim in context.Principal.Claims)
                    {
                        logger.LogDebug("  Claim: {Type} = {Value}", claim.Type, claim.Value);
                    }
                    logger.LogDebug("=== End Claims ===");
                    
                    // Add Admin role if user has the scoutid_admin claim
                    // This must be done here (during sign-in) so it persists in the cookie
                    const string scoutIdAdminClaim = "organisation:692:scoutid_admin"; // TODO: move to config
                    var identity = context.Principal.Identity as System.Security.Claims.ClaimsIdentity;
                    if (identity != null)
                    {
                        var isAdmin = context.Principal.Claims.Any(c => c.Value == scoutIdAdminClaim);
                        logger.LogInformation("Admin check for {Name}: looking for '{Claim}', found: {IsAdmin}", 
                            name, scoutIdAdminClaim, isAdmin);
                        
                        if (isAdmin)
                        {
                            identity.AddClaim(new System.Security.Claims.Claim(
                                System.Security.Claims.ClaimTypes.Role, "Admin"));
                            logger.LogInformation("Added Admin role to {Name}", name);
                        }
                    }
                }

                // Sync user to database
                try
                {
                    var userSyncService = context.HttpContext.RequestServices.GetService<IUserSyncService>();
                    if (userSyncService != null && context.Principal != null)
                    {
                        var currentUserService = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();
                        var claims = currentUserService.GetUserFromPrincipal(context.Principal);
                        if (claims != null)
                        {
                            await userSyncService.SyncUserAsync(claims);
                            logger.LogDebug("User {Name} synced to database", name);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to sync user {Name} to database", name);
                    // Don't fail authentication if sync fails
                }
            },
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "ScoutID authentication failed");
                return Task.CompletedTask;
            }
        };
    });
}

// Add claims transformation to convert ScoutID claims to application claims
builder.Services.AddTransient<IClaimsTransformation, ScoutIdClaimsTransformation>();

builder.Services.AddAuthorization(options =>
{
    // Policy for users who can manage members (member registrars)
    options.AddPolicy("MemberRegistrar", policy =>
        policy.RequireRole("MemberRegistrar"));
    
    // Policy for authenticated users with any group access
    options.AddPolicy("GroupAccess", policy =>
        policy.RequireAuthenticatedUser());

    // Policy for system administrators
    options.AddPolicy("Admin", policy =>
        policy.RequireRole("Admin"));
});

builder.Services.AddCascadingAuthenticationState();

// Add health checks for Azure monitoring
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SkojjtDbContext>("database");

var app = builder.Build();

// Apply pending EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SkojjtDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Applying pending database migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to apply database migrations. The application will start without migrations.");
    }
}

// Log that configuration and DI completed successfully
app.Logger.LogInformation("Application built successfully. Configuring middleware pipeline...");

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Enable response compression
app.UseResponseCompression();

app.UseHttpsRedirection();
app.UseStaticFiles();

// Normalize request culture before Blazor rendering. Malformed Accept-Language headers
// (from bots/scanners) would otherwise cause CultureNotFoundException in
// ServerComponentSerializer, crashing the entire request including the error handler.
app.UseRequestLocalization();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Map health check endpoint (unauthenticated, for Azure monitoring)
app.MapHealthChecks("/healthz");

// Map API controllers
app.MapControllers();

// Map SignalR hubs
app.MapHub<AttendanceHub>("/hubs/attendance");
app.MapHub<BadgeHub>("/hubs/badge");

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Logger.LogInformation("Middleware pipeline configured. Starting application...");

app.Run();
