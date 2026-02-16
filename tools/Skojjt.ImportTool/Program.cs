using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Skojjt.Infrastructure.Data;
using Skojjt.Infrastructure.Services;

var defaultConnectionString = "Host=localhost;Port=5433;Database=skojjt;Username=skojjt;Password=dev_password_123;Keepalive=30;Command Timeout=180;GSS Encryption Mode=Disable;Include Error Detail=true";
// Parse command line arguments
if (args.Length < 1)
{
    Console.WriteLine("Usage: Skojjt.ImportTool <import-directory> [connection-string]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  import-directory   Path to JSON export files (e.g., ./scripts/migration/json_export)");
    Console.WriteLine("  connection-string  Optional PostgreSQL connection string");
    Console.WriteLine($"                     Default: {defaultConnectionString}");
    return 1;
}

var importDirectory = args[0];
var connectionString = args.Length > 1 
    ? args[1] 
    : defaultConnectionString;

if (!Directory.Exists(importDirectory))
{
    Console.WriteLine($"ERROR: Directory not found: {importDirectory}");
    return 1;
}

Console.WriteLine("===========================================");
Console.WriteLine("  Skojjt Data Import Tool");
Console.WriteLine("===========================================");
Console.WriteLine($"Import from: {Path.GetFullPath(importDirectory)}");
Console.WriteLine($"Database:    {connectionString.Split(';').FirstOrDefault(s => s.StartsWith("Database="))}");
Console.WriteLine();

// Build host with services
var host = Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddDbContext<SkojjtDbContext>(options =>
            options.UseNpgsql(connectionString));
        
        services.AddScoped<DataMigrationService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

// Run migration
using var scope = host.Services.CreateScope();
var migrationService = scope.ServiceProvider.GetRequiredService<DataMigrationService>();
var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

try
{
    Console.WriteLine("Starting import...");
    Console.WriteLine();
    
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    
    await migrationService.ImportAllAsync(importDirectory);
    
    stopwatch.Stop();
    
    Console.WriteLine();
    Console.WriteLine("===========================================");
    Console.WriteLine($"  Import completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds");
    Console.WriteLine("===========================================");
    
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Import failed");
    Console.WriteLine();
    Console.WriteLine($"ERROR: {ex.Message}");
    return 1;
}
