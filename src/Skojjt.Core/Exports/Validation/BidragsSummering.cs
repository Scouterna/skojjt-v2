namespace Skojjt.Core.Exports.Validation;

/// <summary>
/// Summary of calculated aktivitetsbidrag for an entire närvarokort.
/// </summary>
public class BidragsSummering
{
    /// <summary>
    /// Per-meeting breakdown.
    /// </summary>
    public required IReadOnlyList<SammankomstBidrag> SammankomstBerakningar { get; init; }

    /// <summary>
    /// Total grant amount for all qualifying meetings.
    /// </summary>
    public decimal TotaltBelopp => TotaltBeloppFlickor + TotaltBeloppPojkar;

    /// <summary>
    /// Total eligible female participant attendances across all qualifying meetings.
    /// </summary>
    public int TotaltAntalFlickor => SammankomstBerakningar
        .Where(s => s.ArBidragsberattigad).Sum(s => s.AntalFlickor);

    /// <summary>
    /// Total eligible male participant attendances across all qualifying meetings.
    /// </summary>
    public int TotaltAntalPojkar => SammankomstBerakningar
        .Where(s => s.ArBidragsberattigad).Sum(s => s.AntalPojkar);

    /// <summary>
    /// Total grant amount for female participants.
    /// </summary>
    public decimal TotaltBeloppFlickor => SammankomstBerakningar
        .Where(s => s.ArBidragsberattigad).Sum(s => s.BeloppFlickor);

    /// <summary>
    /// Total grant amount for male participants.
    /// </summary>
    public decimal TotaltBeloppPojkar => SammankomstBerakningar
        .Where(s => s.ArBidragsberattigad).Sum(s => s.BeloppPojkar);

    /// <summary>
    /// Number of meetings that qualify for aktivitetsbidrag.
    /// </summary>
    public int AntalBidragsberattigadeSammankomster => SammankomstBerakningar
        .Count(s => s.ArBidragsberattigad);

    /// <summary>
    /// Number of meetings that do not qualify.
    /// </summary>
    public int AntalEjBidragsberattigadeSammankomster => SammankomstBerakningar
        .Count(s => !s.ArBidragsberattigad);

    /// <summary>
    /// The settings used for this calculation.
    /// </summary>
    public required AktivitetsbidragSettings Settings { get; init; }

    /// <summary>
    /// The semester year used for age calculations.
    /// </summary>
    public required int TerminsAr { get; init; }
}
