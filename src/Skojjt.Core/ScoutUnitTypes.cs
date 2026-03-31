namespace Skojjt.Core;

/// <summary>
/// Centralised Scoutnet unit type definitions used across the application.
/// Maps unit_type_id (raw_value) to display names, age ranges, and sort order.
/// </summary>
public static class ScoutUnitTypes
{
    /// <summary>
    /// All known unit types with their ID and Swedish display name,
    /// ordered by age (oldest first).
    /// </summary>
    public static readonly IReadOnlyList<(int Id, string Name)> All =
    [
        (6, "Roverscouter"),
        (5, "Utmanarscouter"),
        (4, "Äventyrarscouter"),
        (3, "Upptäckarscouter"),
        (2, "Spårarscouter"),
        (1, "Bäverscouter"),
        (8, "Familjescouter"),
        (7, "Annat"),
    ];

    /// <summary>
    /// All valid unit_type_id values (including "Annat").
    /// Troops without one of these are excluded from the person flow chart.
    /// </summary>
    public static readonly HashSet<int> ValidIds = [1, 2, 3, 4, 5, 6, 7, 8];

    /// <summary>
    /// Default set of unit types shown in the person flow chart.
    /// Excludes "Annat" (7) since it has no meaningful age range.
    /// </summary>
    public static readonly HashSet<int> DefaultFlowIds = [1, 2, 3, 4, 5, 6, 8];

    /// <summary>
    /// Sort order for unit types by age, oldest first.
    /// Lower value = displayed higher in the Sankey chart.
    /// </summary>
    public static readonly Dictionary<int, int> AgeSortOrder = new()
    {
        [6] = 0,  // Roverscouter (18–25 år)
        [5] = 1,  // Utmanarscouter (15–17 år)
        [4] = 2,  // Äventyrarscouter (12–14 år)
        [3] = 3,  // Upptäckarscouter (10–11 år)
        [2] = 4,  // Spårarscouter (8–9 år)
        [1] = 5,  // Bäverscouter (7 år)
        [8] = 6,  // Familjescouter (2–7 år)
        [7] = 7,  // Annat (unknown)
    };

    /// <summary>
    /// Age ranges per unit type (minAge, maxAge) inclusive.
    /// Used to project which troop a member will belong to next semester.
    /// In Swedish scouting, age = year - birth year (birth month irrelevant).
    /// </summary>
    public static readonly Dictionary<int, (int MinAge, int MaxAge)> AgeRanges = new()
    {
        [8] = (2, 7),   // Familjescouter
        [1] = (7, 7),   // Bäverscouter
        [2] = (8, 9),   // Spårarscouter
        [3] = (10, 11), // Upptäckarscouter
        [4] = (12, 14), // Äventyrarscouter
        [5] = (15, 17), // Utmanarscouter
        [6] = (18, 25), // Roverscouter
        [7] = (0, 99),  // Annat
    };

    /// <summary>
    /// Gets the display name for a unit type ID, or null if unknown.
    /// </summary>
    public static string? GetName(int? unitTypeId)
    {
        if (!unitTypeId.HasValue) return null;
        foreach (var (id, name) in All)
        {
            if (id == unitTypeId.Value) return name;
        }
        return null;
    }
}
