namespace Skojjt.Core.Exports;

/// <summary>
/// Pure (no I/O) logic for classifying the meetings in a parsed DAK file against the
/// meetings already stored in Skojjt for a troop + semester. Kept free of database
/// dependencies so it can be unit tested directly, including the export → import
/// round-trip no-op invariant.
/// </summary>
public static class DakImportPlanner
{
    /// <summary>
    /// Build an incremental-import plan by comparing parsed DAK data against existing meetings.
    /// </summary>
    /// <param name="dak">Parsed DAK data.</param>
    /// <param name="existingMeetings">Meetings already stored in Skojjt for the target troop.</param>
    /// <param name="memberIds">Person IDs (member numbers) that exist as Skojjt members.</param>
    /// <param name="semesterRange">
    /// Optional inclusive date range of the target semester. Meetings whose date falls
    /// outside this range are excluded from import and reported as out-of-range.
    /// </param>
    public static DakImportPreview Plan(
        DakData dak,
        IReadOnlyList<DakExistingMeeting> existingMeetings,
        ISet<int> memberIds,
        (DateOnly Start, DateOnly End)? semesterRange = null)
    {
        var cardLokal = dak.Kort.Lokal?.Trim() ?? string.Empty;

        var existingByDate = existingMeetings
            .GroupBy(m => m.Date)
            .ToDictionary(g => g.Key, g => g.First());

        var newMeetings = new List<DakImportMeeting>();
        var unchanged = new List<DakImportMeeting>();
        var conflicts = new List<DakImportConflict>();
        var skipped = new Dictionary<string, DakSkippedPerson>(StringComparer.Ordinal);
        var outOfRange = new List<DakOutOfRangeMeeting>();

        foreach (var sammankomst in dak.Kort.Sammankomster)
        {
            var imported = BuildImportedMeeting(sammankomst, memberIds, skipped);

            if (semesterRange is { } range &&
                (imported.Date < range.Start || imported.Date > range.End))
            {
                outOfRange.Add(new DakOutOfRangeMeeting(imported.Date, imported.Name));
                continue;
            }

            if (!existingByDate.TryGetValue(imported.Date, out var existing))
            {
                newMeetings.Add(imported);
                continue;
            }

            var differences = DescribeDifferences(imported, existing, cardLokal);
            if (differences.Count == 0)
                unchanged.Add(imported);
            else
                conflicts.Add(new DakImportConflict
                {
                    Imported = imported,
                    Existing = existing,
                    Differences = differences
                });
        }

        return new DakImportPreview
        {
            CanImport = true,
            NewMeetings = newMeetings,
            UnchangedMeetings = unchanged,
            Conflicts = conflicts,
            SkippedPersons = skipped.Values.OrderBy(p => p.Name).ToList(),
            OutOfRangeMeetings = outOfRange.OrderBy(m => m.Date).ToList()
        };
    }

    /// <summary>
    /// Convert a DAK sammankomst into a normalized imported meeting, resolving persons
    /// against Skojjt members and recording any unknown persons as skipped.
    /// </summary>
    public static DakImportMeeting BuildImportedMeeting(
        DakSammankomst sammankomst,
        ISet<int> memberIds,
        IDictionary<string, DakSkippedPerson> skipped)
    {
        var attending = new List<int>();

        foreach (var person in sammankomst.GetAllPersons())
        {
            if (int.TryParse(person.Uid, out var id) && memberIds.Contains(id))
            {
                if (!attending.Contains(id))
                    attending.Add(id);
            }
            else if (!string.IsNullOrWhiteSpace(person.Uid))
            {
                var name = $"{person.Fornamn} {person.Efternamn}".Trim();
                skipped[person.Uid] = new DakSkippedPerson(person.Uid, name.Length == 0 ? person.Uid : name);
            }
        }

        return new DakImportMeeting
        {
            Date = DateOnly.FromDateTime(sammankomst.Datum.Date),
            StartTime = TimeOnly.FromDateTime(sammankomst.Datum),
            DurationMinutes = sammankomst.DurationMinutes,
            Name = DakActivityTags.StripTag(sammankomst.Aktivitet),
            IsHike = DakActivityTags.DecodeIsHike(sammankomst.Aktivitet),
            Location = sammankomst.Lokal?.Trim() ?? string.Empty,
            AttendingPersonIds = attending
        };
    }

    /// <summary>
    /// Describe the round-trippable field differences between an imported meeting and the
    /// existing one. An empty list means the meetings are considered equal (no conflict).
    /// </summary>
    public static List<string> DescribeDifferences(
        DakImportMeeting imported,
        DakExistingMeeting existing,
        string cardLokal)
    {
        var diffs = new List<string>();

        if (imported.StartTime != existing.StartTime)
            diffs.Add($"Starttid: {existing.StartTime:HH\\:mm} → {imported.StartTime:HH\\:mm}");

        if (imported.DurationMinutes != existing.DurationMinutes)
            diffs.Add($"Längd: {existing.DurationMinutes} min → {imported.DurationMinutes} min");

        if (!string.Equals(imported.Name, existing.Name, StringComparison.Ordinal))
            diffs.Add($"Namn: \"{existing.Name}\" → \"{imported.Name}\"");

        if (imported.IsHike != existing.IsHike)
            diffs.Add($"Utflykt: {(existing.IsHike ? "ja" : "nej")} → {(imported.IsHike ? "ja" : "nej")}");

        if (!LocationMatches(existing.Location, imported.Location, cardLokal))
            diffs.Add($"Lokal: \"{existing.Location}\" → \"{imported.Location}\"");

        if (!AttendanceMatches(imported.AttendingPersonIds, existing.AttendingPersonIds))
            diffs.Add($"Närvaro: {existing.AttendingPersonIds.Count} → {imported.AttendingPersonIds.Count} deltagare");

        return diffs;
    }

    /// <summary>
    /// Location equality that tolerates the DAK card-level fallback: an empty stored
    /// location matches an imported location equal to the card default (which the exporter
    /// emits when the meeting has no specific location), keeping round-trips a no-op.
    /// </summary>
    private static bool LocationMatches(string existingLocation, string importedLocation, string cardLokal)
    {
        var ex = existingLocation?.Trim() ?? string.Empty;
        var imp = importedLocation?.Trim() ?? string.Empty;

        if (string.Equals(ex, imp, StringComparison.Ordinal))
            return true;

        return ex.Length == 0 && string.Equals(imp, cardLokal, StringComparison.Ordinal);
    }

    private static bool AttendanceMatches(IReadOnlyList<int> a, IReadOnlyList<int> b)
        => a.Count == b.Count && a.ToHashSet().SetEquals(b);
}
