using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Infrastructure.Services;

namespace Skojjt.Web.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Roles = "Admin")]
public class MigrationController : ControllerBase
{
    private readonly DataMigrationService _migrationService;
    private readonly ILogger<MigrationController> _logger;

    public MigrationController(DataMigrationService migrationService, ILogger<MigrationController> logger)
    {
        _migrationService = migrationService;
        _logger = logger;
    }

    /// <summary>
    /// Import data from JSON files exported from the old system.
    /// </summary>
    /// <param name="importDirectory">Path to the directory containing JSON export files</param>
    [HttpPost("import")]
    public async Task<IActionResult> ImportData([FromQuery] string importDirectory)
    {
        if (string.IsNullOrEmpty(importDirectory))
        {
            return BadRequest("Import directory path is required");
        }

        if (!Directory.Exists(importDirectory))
        {
            return BadRequest($"Directory not found: {importDirectory}");
        }

        _logger.LogInformation("Starting data import from {Directory}", importDirectory);

        try
        {
            await _migrationService.ImportAllAsync(importDirectory);
            return Ok(new { message = "Import completed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
