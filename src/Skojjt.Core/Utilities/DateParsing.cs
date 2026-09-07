namespace Skojjt.Core.Utilities;

/// <summary>
/// Lenient parsing of date and time strings coming from external systems
/// (legacy exports, Scoutnet and Sensus payloads) where the exact format varies.
/// </summary>
public static class DateParsing
{
    /// <summary>
    /// Parses a date string, accepting plain dates, full timestamps and
    /// ISO-8601 strings where only the leading 10 characters are the date.
    /// Returns null when the value is missing or unparseable.
    /// </summary>
    public static DateOnly? ParseDateOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateOnly.TryParse(value, out var date))
        {
            return date;
        }

        if (DateTime.TryParse(value, out var dateTime))
        {
            return DateOnly.FromDateTime(dateTime);
        }

        if (value.Length >= 10 && DateOnly.TryParse(value[..10], out date))
        {
            return date;
        }

        return null;
    }

    /// <summary>
    /// Parses a time string. Returns null when the value is missing or unparseable.
    /// </summary>
    public static TimeOnly? ParseTimeOnly(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return TimeOnly.TryParse(value, out var time) ? time : null;
    }
}
