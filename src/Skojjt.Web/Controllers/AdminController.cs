using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Infrastructure.Services;

namespace Skojjt.Web.Controllers;

/// <summary>
/// Admin controller for data migration and system administration.
/// Only available in development environment.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly DataMigrationService _migrationService;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AdminController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AdminController(
        DataMigrationService migrationService,
        IWebHostEnvironment environment,
        ILogger<AdminController> logger)
    {
        _migrationService = migrationService;
        _environment = environment;
        _logger = logger;
    }

    /// <summary>
    /// Import data from JSON files (development only).
    /// Streams progress as Server-Sent Events (text/event-stream).
    /// </summary>
    [HttpPost("migrate")]
    public async Task MigrateData([FromBody] MigrateRequest? request = null, CancellationToken cancellationToken = default)
    {
        // Only allow in development
        if (!_environment.IsDevelopment())
        {
            Response.StatusCode = 403;
            return;
        }

        var importDir = request?.ImportDirectory;
        if (string.IsNullOrEmpty(importDir))
        {
            // Default: scripts/migration/json_export relative to the solution root.
            // ContentRootPath points at src/Skojjt.Web/, so go up two levels.
            var solutionRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", ".."));
            importDir = Path.Combine(solutionRoot, "scripts", "migration", "json_export");
        }

        if (!Path.IsPathRooted(importDir))
        {
            var solutionRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", ".."));
            importDir = Path.GetFullPath(Path.Combine(solutionRoot, importDir));
        }

        if (!Directory.Exists(importDir))
        {
            Response.StatusCode = 400;
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(new { error = $"Directory not found: {importDir}" }, cancellationToken);
            return;
        }

        _logger.LogInformation("Starting migration from {Directory}", importDir);

        // Stream progress as Server-Sent Events
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        Func<MigrationProgress, Task> progress = async p =>
        {
            var json = JsonSerializer.Serialize(p, JsonOptions);
            await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        };

        try
        {
            await _migrationService.ImportAllAsync(importDir, cancellationToken, progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed");
            var errorJson = JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
            await Response.WriteAsync($"event: error\ndata: {errorJson}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Get migration status and database statistics.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        // Only allow in development
        if (!_environment.IsDevelopment())
        {
            return Forbid("Stats endpoint is only available in development environment");
        }

        // TODO: Return database statistics
        return Ok(new { status = "ok" });
    }
}

public record MigrateRequest(string? ImportDirectory = null);
