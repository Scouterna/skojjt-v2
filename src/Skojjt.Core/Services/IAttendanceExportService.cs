using Skojjt.Core.Exports;

namespace Skojjt.Core.Services;

/// <summary>
/// Interface for attendance export service.
/// Provides access to all registered exporters.
/// </summary>
public interface IAttendanceExportService
{
    /// <summary>
    /// Get all available exporters.
    /// </summary>
    IEnumerable<IAttendanceExporter> GetExporters();

    /// <summary>
    /// Get an exporter by its ID.
    /// </summary>
    /// <param name="exporterId">The exporter ID (e.g., "dak", "json", "excel-gbg").</param>
    /// <returns>The exporter, or null if not found.</returns>
    IAttendanceExporter? GetExporter(string exporterId);

    /// <summary>
    /// Export attendance data using the specified exporter.
    /// </summary>
    /// <param name="exporterId">The exporter ID.</param>
    /// <param name="data">The attendance report data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export result.</returns>
    Task<ExportResult> ExportAsync(string exporterId, AttendanceReportData data, CancellationToken cancellationToken = default);
}
