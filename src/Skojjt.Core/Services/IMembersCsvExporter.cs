using Skojjt.Core.Exports;

namespace Skojjt.Core.Services;

/// <summary>
/// Interface for generating members CSV exports.
/// </summary>
public interface IMembersCsvExporter
{
    /// <summary>
    /// Generate a CSV file with members eligible for the Gothenburg attendance grant.
    /// </summary>
    /// <param name="input">Input parameters specifying scout group and semester.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export result containing the CSV file data.</returns>
    Task<ExportResult> ExportAsync(GothenburgCsvInput input, CancellationToken cancellationToken = default);
}
