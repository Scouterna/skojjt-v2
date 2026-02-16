using Skojjt.Core.Exports;
using Skojjt.Core.Services;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Service for managing and executing attendance exports.
/// Acts as a registry/factory for all available exporters.
/// </summary>
public class AttendanceExportService : IAttendanceExportService
{
    private readonly Dictionary<string, IAttendanceExporter> _exporters;

    public AttendanceExportService(IEnumerable<IAttendanceExporter> exporters)
    {
        _exporters = exporters.ToDictionary(e => e.ExporterId, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<IAttendanceExporter> GetExporters() => _exporters.Values;

    public IAttendanceExporter? GetExporter(string exporterId)
    {
        _exporters.TryGetValue(exporterId, out var exporter);
        return exporter;
    }

    public async Task<ExportResult> ExportAsync(string exporterId, AttendanceReportData data, CancellationToken cancellationToken = default)
    {
        var exporter = GetExporter(exporterId);
        if (exporter == null)
        {
            throw new ArgumentException($"Unknown exporter: {exporterId}. Available exporters: {string.Join(", ", _exporters.Keys)}");
        }

        return await exporter.ExportAsync(data, cancellationToken);
    }
}
