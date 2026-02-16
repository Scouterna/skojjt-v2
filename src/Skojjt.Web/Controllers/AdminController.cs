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
    /// </summary>
    [HttpPost("migrate")]
    public async Task<IActionResult> MigrateData([FromBody] MigrateRequest request, CancellationToken cancellationToken)
    {
        // Only allow in development
        if (!_environment.IsDevelopment())
        {
            return Forbid("Migration is only available in development environment");
        }

        if (string.IsNullOrEmpty(request.ImportDirectory))
        {
            return BadRequest("ImportDirectory is required");
        }

        if (!Directory.Exists(request.ImportDirectory))
        {
            return BadRequest($"Directory not found: {request.ImportDirectory}");
        }

        _logger.LogInformation("Starting migration from {Directory}", request.ImportDirectory);

        try
        {
            await _migrationService.ImportAllAsync(request.ImportDirectory, cancellationToken);
            return Ok(new { message = "Migration completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration failed");
            return StatusCode(500, new { error = ex.Message });
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

public record MigrateRequest(string ImportDirectory);
