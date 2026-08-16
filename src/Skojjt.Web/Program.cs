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
using Microsoft.AspNetCore.HttpOverrides;
using Skojjt.Web.Components;
using Skojjt.Web.Hubs;
using Skojjt.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Raise Kestrel's request-header size limit as defense-in-depth against HTTP 431
// (Request Header Fields Too Large). The primary fix is the server-side auth ticket
// store configured below, which keeps the auth cookie tiny; this headroom also covers
// large cookies from other sources and the extra forwarded headers added by the
// Azure App Service reverse proxy.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestHeadersTotalSize = 64 * 1024; // 64 KB (default 32 KB)
});

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
// Override the Swedish negative sign (U+2212 '−') with ASCII hyphen-minus (U+002D '-')
// as a safety net. URL interpolations also use FormattableString.Invariant() explicitly,
// but this ensures any missed site still produces valid URLs for negative ScoutnetIds.
svSE.NumberFormat.NegativeSign = "-";
CultureInfo.DefaultThreadCurrentCulture = svSE;
CultureInfo.DefaultThreadCurrentUICulture = svSE;

// Add MudBlazor services with Swedish localization
builder.Services.AddMudServices();
builder.Services.AddTransient<MudLocalizer, SwedishMudLocalizer>();

// Configure request localization to prevent CultureNotFoundException from malformed
// Accept-Language headers (e.g. bots/scanners sending binary garbage).
// This ensures all requests use a known-good culture before reaching Blazor's
// ServerComponentSerializer, which would otherwise crash on invalid culture names.
// The English fallback culture must also use Monday as first day of week so that
// MudDatePicker always starts on Monday, even when the browser sends Accept-Language: en.
var enCulture = new CultureInfo("en");
enCulture.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Monday;
var supportedCultures = new[] { svSE, enCulture };
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

// Register interest badge (intressemärken) catalog service
builder.Services.AddSingleton<InterestBadgeCatalogService>();

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
        ScoutIdCookieEvents.Configure(options);
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
        ScoutIdCookieEvents.Configure(options);
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
        ScoutIdCookieEvents.Configure(options);
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = builder.Configuration["ScoutId:Authority"];
        options.ClientId = builder.Configuration["ScoutId:ClientId"];
        options.ClientSecret = builder.Configuration["ScoutId:ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code;
        // Do not persist ID/access/refresh tokens in the auth ticket. They are not used
        // server-side and previously bloated the cookie, contributing to HTTP 431.
        options.SaveTokens = false;
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

// Store authentication tickets server-side so the browser cookie only holds a small
// session key instead of every ScoutID claim and saved token. This is the robust fix
// for HTTP 431: the cookie stays tiny no matter how many groups/troops/roles a user has.
// NOTE: AddDistributedMemoryCache is per-instance and cleared on restart. Blazor Server
// already relies on sticky sessions, so a single instance serves a given user; swap in
// AddStackExchangeRedisCache for shared, restart-durable storage across instances.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSingleton<ITicketStore, DistributedCacheTicketStore>();
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<ITicketStore>((options, ticketStore) => options.SessionStore = ticketStore);

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

// Configure forwarded headers for Azure App Service reverse proxy.
// Without this, the SAML library constructs ACS URLs using the internal
// Azure hostname instead of the custom domain (e.g. skojjt.scouterna.net).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    // Azure App Service proxy IPs are not known in advance
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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

// UseForwardedHeaders must run before UseHsts and UseHttpsRedirection.
// Azure App Service terminates TLS and forwards requests internally over HTTP.
// Without processing X-Forwarded-Proto first, UseHsts sees HTTP and never
// emits the Strict-Transport-Security header, causing Chrome to show "Not secure".
app.UseForwardedHeaders();

// Defensive self-heal for users still carrying a large auth cookie issued before the
// server-side ticket store was introduced (e.g. leaders in many troops). If the inbound
// auth cookie approaches the request-header limit, clear it and redirect to sign-out so
// the browser stops resending it, preventing a hard HTTP 431. Runs after UseForwardedHeaders
// (so IsHttps is correct for cookie deletion) and before auth / redirects.
app.UseStaleAuthCookieGuard();

// Redirect non-canonical hostnames (e.g. skojjt.azurewebsites.net) to the
// canonical domain (skojjt.scouterna.net). This runs early so SAML/auth
// callbacks always use the domain registered in ScoutID.
// Health check and SAML callback paths are excluded — a 301 on a POST would
// cause browsers to drop the SAML response body, silently breaking login.
var canonicalHostname = app.Configuration["CanonicalHostname"];
if (!string.IsNullOrEmpty(canonicalHostname))
{
    app.Use(async (context, next) =>
    {
        var host = context.Request.Host.Host;
        if (!string.Equals(host, canonicalHostname, StringComparison.OrdinalIgnoreCase)
            && !context.Request.Path.StartsWithSegments("/healthz")
            && !context.Request.Path.StartsWithSegments("/Saml2"))
        {
            var url = $"{context.Request.Scheme}://{canonicalHostname}{context.Request.Path}{context.Request.QueryString}";
            context.Response.Redirect(url, permanent: true);
            return;
        }
        await next();
    });
}

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

// Static wwwroot assets are served by MapStaticAssets() (see below), which applies
// content-based fingerprinting and immutable, long-lived cache headers for assets
// referenced via the @Assets["..."] helper. UseStaticFiles remains as a fallback for
// any files not captured by the build-time asset manifest.
//
// Images referenced by plain paths (e.g. /img/interest-badges/...) bypass the asset
// manifest, so give them an explicit 24h Cache-Control. Without it the browser
// revalidates every re-rendered <img>, causing the badge map to visibly reload
// images when the filter changes.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        if (name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.CacheControl = "public,max-age=86400";
        }
    }
});

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

// Dynamic sitemap.xml listing only publicly accessible pages (no login required).
// Helps search engines index the public landing/about/help pages and reduces 404s.
app.MapGet("/sitemap.xml", (HttpContext ctx, DocumentationService docs) =>
{
    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    var today = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    var urls = new List<string>
    {
        $"{baseUrl}/",
        $"{baseUrl}/about",
        $"{baseUrl}/hjalp",
    };
    urls.AddRange(docs.GetPages().Select(p => $"{baseUrl}/hjalp/{Uri.EscapeDataString(p.Slug)}"));

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
    foreach (var url in urls)
    {
        sb.AppendLine("  <url>");
        sb.AppendLine($"    <loc>{System.Net.WebUtility.HtmlEncode(url)}</loc>");
        sb.AppendLine($"    <lastmod>{today}</lastmod>");
        sb.AppendLine("  </url>");
    }
    sb.AppendLine("</urlset>");

    return Results.Content(sb.ToString(), "application/xml; charset=utf-8");
}).AllowAnonymous();

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
