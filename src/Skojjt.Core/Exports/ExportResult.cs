namespace Skojjt.Core.Exports;

/// <summary>
/// Result of an attendance export operation.
/// </summary>
/// <param name="Data">The exported data as a byte array.</param>
/// <param name="FileName">Suggested filename for the export.</param>
/// <param name="ContentType">MIME type for the export.</param>
public record ExportResult(byte[] Data, string FileName, string ContentType);
