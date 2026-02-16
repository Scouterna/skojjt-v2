namespace Skojjt.Core.Exports;

/// <summary>
/// Interface for attendance report exporters.
/// Implementations provide export functionality for specific formats (DAK XML, JSON, Excel, etc.).
/// </summary>
public interface IAttendanceExporter
{
    /// <summary>
    /// Unique identifier for this exporter (e.g., "dak", "json", "excel-gbg").
    /// Used for routing and registration.
    /// </summary>
    string ExporterId { get; }

    /// <summary>
    /// Display name for the export format.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Export attendance data for a troop.
    /// </summary>
    /// <param name="data">The attendance report data to export.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Export result containing the file data, filename, and content type.</returns>
    Task<ExportResult> ExportAsync(AttendanceReportData data, CancellationToken cancellationToken = default);
}
