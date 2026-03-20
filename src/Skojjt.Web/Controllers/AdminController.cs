using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Skojjt.Infrastructure.Services;
using Skojjt.Web.Authentication;

namespace Skojjt.Web.Controllers;

/// <summary>
/// Admin controller for data migration and system administration.
/// Requires Admin policy — authenticated via cookie (logged-in admin) or API key.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Policy = "Admin", AuthenticationSchemes = $"Cookies,{ApiKeyAuthenticationHandler.SchemeName}")]
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
    /// Import data from JSON files.
    /// Streams progress as Server-Sent Events (text/event-stream).
    /// In development, reads from the local filesystem.
    /// In production, an import directory must be specified in the request body.
    /// </summary>
    [HttpPost("migrate")]
    public async Task MigrateData([FromBody] MigrateRequest? request = null, CancellationToken cancellationToken = default)
    {
        var importDir = request?.ImportDirectory;
        if (string.IsNullOrEmpty(importDir))
        {
            if (!_environment.IsDevelopment())
            {
                Response.StatusCode = 400;
                Response.ContentType = "application/json";
                await Response.WriteAsJsonAsync(
                    new { error = "importDirectory is required in non-development environments." },
                    cancellationToken);
                return;
            }

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

        _logger.LogInformation("Starting migration from {Directory}, authenticated as {User}",
            importDir, User.Identity?.Name ?? "unknown");

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
    public IActionResult GetStats()
    {
        // TODO: Return database statistics
        return Ok(new { status = "ok" });
    }

    /// <summary>
    /// Upload a ZIP file containing JSON export files and run the data import.
    /// The ZIP should contain the JSON files at the root level (e.g., semesters.json, persons.json, etc.).
    /// Streams progress as Server-Sent Events (text/event-stream).
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(512 * 1024 * 1024)] // 512 MB
    [RequestFormLimits(MultipartBodyLengthLimit = 512 * 1024 * 1024)]
    public async Task ImportFromZip(IFormFile file, CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            Response.StatusCode = 400;
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(new { error = "No file uploaded." }, cancellationToken);
            return;
        }

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            Response.StatusCode = 400;
            Response.ContentType = "application/json";
            await Response.WriteAsJsonAsync(new { error = "File must be a .zip archive." }, cancellationToken);
            return;
        }

        // Extract to a temporary directory
        var tempDir = Path.Combine(Path.GetTempPath(), $"skojjt-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            _logger.LogInformation(
                "Receiving import ZIP ({Size:N0} bytes) from {User}, extracting to {TempDir}",
                file.Length, User.Identity?.Name ?? "unknown", tempDir);

            // Extract ZIP to temp directory
            await using (var stream = file.OpenReadStream())
            {
                ZipFile.ExtractToDirectory(stream, tempDir);
            }

            // Check if files were extracted inside a subfolder (common when zipping a directory)
            var jsonFiles = Directory.GetFiles(tempDir, "*.json");
            if (jsonFiles.Length == 0)
            {
                var subdirs = Directory.GetDirectories(tempDir);
                if (subdirs.Length == 1 && Directory.GetFiles(subdirs[0], "*.json").Length > 0)
                {
                    // Files are in a single subfolder — use that as the import directory
                    tempDir = subdirs[0];
                }
                else
                {
                    Response.StatusCode = 400;
                    Response.ContentType = "application/json";
                    await Response.WriteAsJsonAsync(
                        new { error = "ZIP archive does not contain any JSON files." }, cancellationToken);
                    return;
                }
            }

            _logger.LogInformation("Extracted {Count} JSON files, starting import...",
                Directory.GetFiles(tempDir, "*.json").Length);

            // Stream progress as Server-Sent Events
            Response.Headers.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";

            Func<MigrationProgress, Task> progress = async p =>
            {
                var json = JsonSerializer.Serialize(p, JsonOptions);
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            };

            await _migrationService.ImportAllAsync(tempDir, cancellationToken, progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import from ZIP failed");

            if (!Response.HasStarted)
            {
                Response.StatusCode = 500;
                Response.ContentType = "application/json";
                await Response.WriteAsJsonAsync(new { error = ex.Message }, cancellationToken);
            }
            else
            {
                var errorJson = JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions);
                await Response.WriteAsync($"event: error\ndata: {errorJson}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        finally
        {
            // Clean up temp directory
            try
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temp directory {TempDir}", tempDir);
            }
        }
    }
}

public record MigrateRequest(string? ImportDirectory = null);
