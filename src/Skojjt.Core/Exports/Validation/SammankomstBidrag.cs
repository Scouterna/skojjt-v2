namespace Skojjt.Core.Exports.Validation;

/// <summary>
/// Bidragsberäkning for a single sammankomst.
/// </summary>
public class SammankomstBidrag
{
    /// <summary>
    /// Meeting code.
    /// </summary>
    public required string Kod { get; init; }

    /// <summary>
    /// Meeting date.
    /// </summary>
    public required DateTime Datum { get; init; }

    /// <summary>
    /// Whether this meeting qualifies for aktivitetsbidrag.
    /// </summary>
    public required bool ArBidragsberattigad { get; init; }

    /// <summary>
    /// Reason for rejection, if not qualifying. Null if qualifying.
    /// </summary>
    public string? AvslagsOrsak { get; init; }

    /// <summary>
    /// Number of eligible female participants in this meeting.
    /// </summary>
    public int AntalFlickor { get; init; }

    /// <summary>
    /// Number of eligible male participants in this meeting.
    /// </summary>
    public int AntalPojkar { get; init; }

    /// <summary>
    /// Grant amount for female participants (AntalFlickor × rate).
    /// </summary>
    public decimal BeloppFlickor { get; init; }

    /// <summary>
    /// Grant amount for male participants (AntalPojkar × rate).
    /// </summary>
    public decimal BeloppPojkar { get; init; }

    /// <summary>
    /// Total grant amount for this meeting.
    /// </summary>
    public decimal Belopp => BeloppFlickor + BeloppPojkar;

    /// <summary>
    /// Total number of eligible participants.
    /// </summary>
    public int AntalBidragsberattigade => AntalFlickor + AntalPojkar;
}
