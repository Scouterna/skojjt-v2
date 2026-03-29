using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Localization;
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
using Skojjt.Web.Authentication;
using Skojjt.Web.Components;
using Skojjt.Web.Hubs;
using Skojjt.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Application Insights for production telemetry (exceptions, requests, dependencies)
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddSingleton<Microsoft.ApplicationInsights.Extensibility.ITelemetryInitializer, UserTelemetryInitializer>();

// Log startup diagnostics
var startupLogger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("Startup");
startupLogger.LogInformation("Starting Skojjt.Web, Environment: {Env}", builder.Environment.EnvironmentName);
startupLogger.LogInformation("ConnectionString configured: {HasCs}", !string.IsNullOrEmpty(builder.Configuration.GetConnectionString("DefaultConnection")));
startupLogger.LogInformation("ScoutId Authority: {Authority}", builder.Configuration["ScoutId:Authority"]);

// Set default culture for all threads (including Blazor Server circuit threads).
// UseRequestLocalization only applies to the initial HTTP request; subsequent
// renders on SignalR threads would otherwise use the system default (en-US),
// causing MudDatePicker to show Sunday as first day of week instead of Monday.
var svSE = new CultureInfo("sv-SE");
CultureInfo.DefaultThreadCurrentCulture = svSE;
CultureInfo.DefaultThreadCurrentUICulture = svSE;

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
    .AddInteractiveServerComponents(options =>
    {
        // Allow circuits to stay alive longer while the client reconnects.
        // Default is 3 minutes; extend to 5 for unstable mobile/Wi-Fi connections.
        options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(5);

        // Show detailed errors in development for easier debugging.
        options.DetailedErrors = true; // TODO: set to: builder.Environment.IsDevelopment();
    });

// Add API controllers
builder.Services.AddControllers();

// Add OpenAPI / Swagger for API documentation
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Skojjt Admin API",
        Version = "v1",
        Description = "API för dataimport och administration av Skojjt."
    });

    // Add API key authentication support in Swagger UI
    options.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "X-Api-Key",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.ApiKey,
        Description = "API-nyckel genererad från admin-panelen."
    });
    options.AddSecurityRequirement(_ => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("ApiKey"),
            []
        }
    });
});

// Add SignalR for real-time updates
builder.Services.AddSignalR(options =>
{
    // Send keep-alive pings every 15 seconds (default) to detect dead connections.
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);

    // Allow clients 60 seconds (default 30) to respond to keep-alive before
    // the server considers them disconnected. Helps on flaky mobile networks.
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);

    // Allow larger payloads for batch attendance updates.
    options.MaximumReceiveMessageSize = 128 * 1024; // 128 KB
});

// NOTE: ResponseCompressionMiddleware is intentionally NOT used.
// MapStaticAssets() serves pre-compressed static files (gzip/brotli) at build time.
// Using ResponseCompressionMiddleware on top causes ArgumentOutOfRangeException in
// SendFileFallback when the compressed response wrapper's count doesn't match the
// asset manifest's recorded file size. Azure App Service / reverse proxies handle
// dynamic response compression at the infrastructure layer.

// Add HttpContextAccessor
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
		npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
	}
	));

// Register repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISemesterRepository, SemesterRepository>();
builder.Services.AddScoped<IScoutGroupRepository, ScoutGroupRepository>();
builder.Services.AddScoped<IPersonRepository, PersonRepository>();
builder.Services.AddScoped<ITroopRepository, TroopRepository>();
builder.Services.AddScoped<IMeetingRepository, MeetingRepository>();
builder.Services.AddScoped<IBadgeRepository, BadgeRepository>();
builder.Services.AddScoped<IBadgeTemplateRepository, BadgeTemplateRepository>();

// Register services
builder.Services.AddScoped<DataMigrationService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<IBadgeService, BadgeService>();
builder.Services.AddScoped<IMyProfileService, MyProfileService>();
builder.Services.AddScoutnetServices(builder.Configuration);
builder.Services.AddExportServices();

// Register documentation service
builder.Services.AddSingleton<DocumentationService>();

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
                }

                // Admin role assignment is handled by ScoutIdClaimsTransformation
                // which runs on every request via IClaimsTransformation.
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

// Add API key authentication scheme (works alongside cookie/OIDC/SAML auth)
builder.Services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, _ => { });

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

    // Policy for system administrators — accepts cookie auth or API key
    options.AddPolicy("Admin", policy =>
    {
        policy.AddAuthenticationSchemes(
            CookieAuthenticationDefaults.AuthenticationScheme,
            ApiKeyAuthenticationHandler.SchemeName);
        policy.RequireRole("Admin");
    });
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
    // Serve a static HTML error page instead of re-executing through the Blazor pipeline.
    // UseExceptionHandler("/Error") previously tried to render a Blazor component, which
    // fails when the pipeline itself is broken (e.g. CultureNotFoundException in
    // ServerComponentSerializer, BadImageFormatException from a corrupted deployment).
    // A static file handler bypasses Blazor entirely and always produces a valid response.
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        ExceptionHandler = async context =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Serving static error page for {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "text/html; charset=utf-8";

            var errorPagePath = Path.Combine(app.Environment.WebRootPath, "error.html");
            if (File.Exists(errorPagePath))
            {
                await context.Response.SendFileAsync(errorPagePath);
            }
        }
    });
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Normalize request culture before Blazor rendering. Malformed Accept-Language headers
// (from bots/scanners) would otherwise cause CultureNotFoundException in
// ServerComponentSerializer, crashing the entire request including the error handler.
app.UseRequestLocalization();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Swagger UI for API documentation (available at /swagger in development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Skojjt Admin API v1");
    });
}

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
