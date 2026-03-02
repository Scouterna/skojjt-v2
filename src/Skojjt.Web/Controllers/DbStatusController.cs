using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Web.Controllers;

/// <summary>
/// Database status endpoint for monitoring and maintenance.
/// Requires Admin role for authenticated access.
/// </summary>
[ApiController]
[Route("api/v1/db-status")]
[Authorize(Roles = "Admin")]
public class DbStatusController : ControllerBase
{
    private readonly SkojjtDbContext _db;
    private readonly ILogger<DbStatusController> _logger;

    public DbStatusController(SkojjtDbContext db, ILogger<DbStatusController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get database connectivity, migration status, and table statistics.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        var result = new DbStatusResult();

        // Test connectivity
        try
        {
            result.CanConnect = await _db.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            result.CanConnect = false;
            result.ConnectionError = ex.Message;
            return Ok(result);
        }

        // Get applied migrations
        try
        {
            var appliedMigrations = await _db.Database.GetAppliedMigrationsAsync(cancellationToken);
            result.AppliedMigrations = appliedMigrations.ToList();
        }
        catch (Exception ex)
        {
            result.MigrationError = ex.Message;
        }

        // Get pending migrations
        try
        {
            var pendingMigrations = await _db.Database.GetPendingMigrationsAsync(cancellationToken);
            result.PendingMigrations = pendingMigrations.ToList();
        }
        catch (Exception ex)
        {
            result.MigrationError ??= ex.Message;
        }

        // Get table row counts
        try
        {
            var tables = new Dictionary<string, long>();
            var connection = _db.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT schemaname || '.' || relname AS table_name,
                           n_live_tup AS row_count
                    FROM pg_stat_user_tables
                    ORDER BY n_live_tup DESC;
                    """;
                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    tables[reader.GetString(0)] = reader.GetInt64(1);
                }
            }
            finally
            {
                await connection.CloseAsync();
            }
            result.Tables = tables;
        }
        catch (Exception ex)
        {
            result.TableError = ex.Message;
        }

        // Get database server info
        try
        {
            var connection = _db.Database.GetDbConnection();
            result.ServerVersion = _db.Database.GetDbConnection().ServerVersion;
            result.Database = connection.Database;
            result.DataSource = connection.DataSource;
        }
        catch
        {
            // Ignore - optional info
        }

        return Ok(result);
    }

    /// <summary>
    /// Apply pending migrations.
    /// </summary>
    [HttpPost("migrate")]
    public async Task<IActionResult> ApplyMigrations(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Admin triggered database migration via /api/v1/db-status/migrate");
            var pendingBefore = (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            if (pendingBefore.Count == 0)
            {
                return Ok(new { message = "No pending migrations." });
            }

            await _db.Database.MigrateAsync(cancellationToken);

            return Ok(new
            {
                message = $"Successfully applied {pendingBefore.Count} migration(s).",
                applied = pendingBefore
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration triggered via admin endpoint failed");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Execute a read-only SQL query (SELECT only).
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> ExecuteQuery([FromBody] QueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Sql))
        {
            return BadRequest(new { error = "SQL query is required." });
        }

        // Only allow SELECT statements for safety
        var trimmed = request.Sql.TrimStart();
        if (!trimmed.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Only SELECT queries are allowed." });
        }

        try
        {
            var connection = _db.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = request.Sql;
                command.CommandTimeout = 30;
                using var reader = await command.ExecuteReaderAsync(cancellationToken);

                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                var rows = new List<Dictionary<string, object?>>();
                var maxRows = 1000;
                while (await reader.ReadAsync(cancellationToken) && rows.Count < maxRows)
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        row[columns[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    rows.Add(row);
                }

                return Ok(new { columns, rows, rowCount = rows.Count, truncated = rows.Count >= maxRows });
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

public record DbStatusResult
{
    public bool CanConnect { get; set; }
    public string? ConnectionError { get; set; }
    public string? ServerVersion { get; set; }
    public string? Database { get; set; }
    public string? DataSource { get; set; }
    public List<string> AppliedMigrations { get; set; } = [];
    public List<string> PendingMigrations { get; set; } = [];
    public string? MigrationError { get; set; }
    public Dictionary<string, long> Tables { get; set; } = [];
    public string? TableError { get; set; }
}

public record QueryRequest(string Sql);
