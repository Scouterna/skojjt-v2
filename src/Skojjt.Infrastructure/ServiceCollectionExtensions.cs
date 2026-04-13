using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Skojjt.Core.Exports;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Exports;
using Skojjt.Infrastructure.Scoutnet;
using Skojjt.Infrastructure.Sensus;
using Skojjt.Infrastructure.Services;

namespace Skojjt.Infrastructure;

/// <summary>
/// Extension methods for registering Infrastructure services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Scoutnet integration services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration (optional, for binding ScoutnetOptions).</param>
    public static IServiceCollection AddScoutnetServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        // Bind ScoutnetOptions from configuration if provided
        if (configuration != null)
        {
            services.Configure<ScoutnetOptions>(configuration.GetSection(ScoutnetOptions.SectionName));
        }
        else
        {
            // Use default options if no configuration provided
            services.Configure<ScoutnetOptions>(_ => { });
        }

        // Register HttpClient for Scoutnet API
        services.AddHttpClient<IScoutnetApiClient, ScoutnetApiClient>();

        // Register import service
        services.AddScoped<IScoutnetImportService, ScoutnetImportService>();

        // Register membership sync service (push troop/patrol changes to Scoutnet)
        services.AddScoped<IMembershipSyncService, ScoutnetMembershipSyncService>();

        // Register waiting list registration service
        services.AddScoped<IScoutnetRegistrationService, ScoutnetRegistrationService>();

        // Register camp service (create camps, import from Scoutnet projects)
        services.AddScoped<ICampService, CampService>();

        return services;
    }

    /// <summary>
    /// Adds attendance export services and all registered exporters.
    /// </summary>
    public static IServiceCollection AddExportServices(this IServiceCollection services)
    {
        // Register individual exporters
        services.AddScoped<IAttendanceExporter, DakXmlExporter>();
        services.AddScoped<IAttendanceExporter, JsonExporter>();
        services.AddScoped<IAttendanceExporter, ExcelGothenburgExporter>();
        services.AddScoped<IAttendanceExporter, ExcelStockholmExporter>();

        // Register export service (receives all IAttendanceExporter instances)
        services.AddScoped<IAttendanceExportService, AttendanceExportService>();

        // Register lagerbidrag exporter
        services.AddScoped<ILagerbidragExporter, LagerbidragExporter>();

        // Register members CSV exporter (aktivitetsbidrag)
        services.AddScoped<IMembersCsvExporter, MembersCsvExporter>();

        // Register group summary service
        services.AddScoped<IGroupSummaryService, GroupSummaryService>();

        // Register DAK analysis service
        services.AddScoped<IDakAnalysisService, DakAnalysisService>();

        // Register attendance stats service (charts)
        services.AddScoped<IAttendanceStatsService, AttendanceStatsService>();

        // Register person flow service (Sankey chart)
        services.AddScoped<IPersonFlowService, PersonFlowService>();

        // Register Sensus sync service with named HttpClient
        services.AddHttpClient(SensusSyncService.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://e-tjanst.sensus.se");
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            UseCookies = false,
        });
        services.AddScoped<ISensusSyncService, SensusSyncService>();

        return services;
    }
}
