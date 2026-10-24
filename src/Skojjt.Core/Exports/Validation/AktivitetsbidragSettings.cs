namespace Skojjt.Core.Exports.Validation;

/// <summary>
/// Configurable settings for aktivitetsbidrag validation and calculation.
/// Municipality-specific and changes yearly  keep settings separate from logic.
/// </summary>
public record AktivitetsbidragSettings
{
    /// <summary>
    /// Find the best matching settings for a given municipality and year.
    /// Returns null if no match is found.
    /// </summary>
    public static AktivitetsbidragSettings? Find(IEnumerable<AktivitetsbidragSettings> settings, string? kommunId, int? year = null)
    {
        var candidates = settings;

        if (!string.IsNullOrWhiteSpace(kommunId))
            candidates = candidates.Where(s => s.KommunId == null || s.KommunId == kommunId);

        if (year.HasValue)
            candidates = candidates.Where(s => s.Ar == null || s.Ar == year.Value);

        return candidates.FirstOrDefault();
    }

    /// <summary>
    /// Display name, e.g. "Göteborgs kommun 2026".
    /// </summary>
    public required string Namn { get; init; }

    /// <summary>
    /// Municipality code, e.g. "1480". Null means the settings apply to any municipality.
    /// </summary>
    public string? KommunId { get; init; }

    /// <summary>
    /// Year these settings apply to. Null means default/current.
    /// </summary>
    public int? Ar { get; init; }

    // --- Bidragsbelopp ---
    /// <summary>
    /// Amount per eligible female participant per meeting (kr).
    /// </summary>
    public required decimal BeloppPerFlicka { get; init; }

    /// <summary>
    /// Amount per eligible male participant per meeting (kr).
    /// </summary>
    public required decimal BeloppPerPojke { get; init; }

    /// <summary>
    /// Optional age-based amounts. When non-empty, these replace the gender-based amounts.
    /// </summary>
    public IReadOnlyList<AldersBelopp> AldersBelopp { get; init; } = [];

    /// <summary>
    /// Maximum number of eligible participants per meeting that generate a grant. Null means no limit.
    /// </summary>
    public int? MaxAntalDeltagare { get; init; }

    /// <summary>
    /// Maximum number of eligible participants per qualified leader. Null means no limit.
    /// </summary>
    public int? MaxDeltagarePerLedare { get; init; }

    /// <summary>
    /// Fixed amount per qualified leader per meeting (kr).
    /// </summary>
    public decimal BeloppPerLedare { get; init; }

    /// <summary>
    /// Fixed amount per qualifying meeting (kr), independent of the number of participants.
    /// </summary>
    public decimal BeloppPerSammankomst { get; init; }

    /// <summary>
    /// Maximum number of leaders per meeting that generate
    /// </summary>
    public int? MaxAntalLedareMedBidrag { get; init; }

    /// <summary>
    /// How the municipality calculates the grant.
    /// </summary>
    public BidragsBerakningstyp Berakningstyp { get; init; } = BidragsBerakningstyp.Fast;

    /// <summary>
    /// Receiving system, e.g. "Interbook / ApN".
    /// </summary>
    public string? SystemMottagare { get; init; }

    /// <summary>
    /// Free-text notes about municipality-specific rules not modelled in code.
    /// </summary>
    public string? Anmarkning { get; init; }

    /// <summary>
    /// True when the settings contain fixed amounts so a grant amount can be calculated.
    /// </summary>
    public bool HarKandaBelopp =>
        Berakningstyp == BidragsBerakningstyp.Fast &&
        (BeloppPerFlicka > 0 || BeloppPerPojke > 0 || AldersBelopp.Count > 0 || BeloppPerLedare > 0 || BeloppPerSammankomst > 0);

    /// <summary>
    /// Gets the amount for one eligible participant.
    /// </summary>
    public decimal BeloppFor(bool isFemale, int age)
    {
        if (AldersBelopp.Count > 0)
        {
            return AldersBelopp.FirstOrDefault(b => age >= b.MinAlder && age <= b.MaxAlder)?.Belopp ?? 0m;
        }

        return isFemale ? BeloppPerFlicka : BeloppPerPojke;
    }

    // --- Deltagarkrav ---

    /// <summary>
    /// Minimum age for eligible participants (inclusive).
    /// </summary>
    public required int MinAlderDeltagare { get; init; }

    /// <summary>
    /// Maximum age for eligible participants (inclusive).
    /// </summary>
    public required int MaxAlderDeltagare { get; init; }

    /// <summary>
    /// Minimum age for leaders (inclusive).
    /// </summary>
    /// Defaults to 13 when not specified in the catalog.
    public int MinAlderLedare { get; init; } = 13;

    /// <summary>
    /// Minimum number of eligible participants per meeting for the meeting to qualify.
    /// </summary>
    public required int MinAntalDeltagare { get; init; }

    /// <summary>
    /// Minimum number of leaders per meeting for the meeting to qualify.
    /// </summary>
    /// Defaults to 1 when not specified in the catalog.
    public int MinAntalLedare { get; init; } = 1;

    // --- Sammankomstkrav ---

    /// <summary>
    /// Minimum meeting duration in minutes for the meeting to qualify.
    /// </summary>
    public required int MinSammankomstMinuter { get; init; }

}

/// <summary>
/// Amount per participant and meeting for an age range (inclusive).
/// </summary>
public record AldersBelopp(int MinAlder, int MaxAlder, decimal Belopp)
{
    /// <summary>
    /// Formats age-based amounts as "7-12:3.50;13-20:13.80".
    /// </summary>
    public static string Format(IEnumerable<AldersBelopp> belopp) =>
        string.Join(';', belopp.Select(b =>
            $"{b.MinAlder}-{b.MaxAlder}:{b.Belopp.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

    /// <summary>
    /// Parses age-based amounts in the format "7-12:3.50;13-20:13.80". Comma is accepted as decimal separator.
    /// </summary>
    public static bool TryParse(string? text, out IReadOnlyList<AldersBelopp> result)
    {
        var list = new List<AldersBelopp>();
        result = list;
        if (string.IsNullOrWhiteSpace(text))
            return true;

        foreach (var part in text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var rangeAndAmount = part.Split(':', StringSplitOptions.TrimEntries);
            if (rangeAndAmount.Length != 2)
                return false;

            var range = rangeAndAmount[0].Split('-', StringSplitOptions.TrimEntries);
            if (range.Length != 2 ||
                !int.TryParse(range[0], out var min) ||
                !int.TryParse(range[1], out var max) ||
                min > max ||
                !decimal.TryParse(rangeAndAmount[1].Replace(',', '.'),
                    System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out var amount) ||
                amount < 0)
            {
                return false;
            }

            list.Add(new AldersBelopp(min, max, amount));
        }

        return true;
    }
}

/// <summary>
/// Grant calculation model used by a municipality.
/// </summary>
public enum BidragsBerakningstyp
{
    /// <summary>Fixed amounts per participant and meeting.</summary>
    Fast,
    /// <summary>Variable pool distributed per semester.</summary>
    Pott,
    /// <summary>Variable pool distributed per quarter.</summary>
    PottKvartal,
    /// <summary>Variable pool based on participant hours.</summary>
    PottTimmar,
    /// <summary>Points-based pool (e.g. Uppsala).</summary>
    Poang,
}

