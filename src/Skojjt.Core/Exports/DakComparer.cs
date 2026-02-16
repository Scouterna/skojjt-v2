using System.Globalization;

namespace Skojjt.Core.Exports;

/// <summary>
/// Compares two normalized <see cref="DakData"/> instances and produces a structured diff.
/// Both inputs should be normalized via <see cref="DakNormalizer"/> before comparison.
/// Sammankomster are matched by Kod (as int32).
/// </summary>
public static class DakComparer
{
    /// <summary>
    /// Compare two DakData instances. Both should be normalized first.
    /// </summary>
    /// <param name="old">The old/baseline version.</param>
    /// <param name="new">The new/updated version.</param>
    public static DakComparisonResult Compare(DakData old, DakData @new)
    {
        var result = new DakComparisonResult();

        // Compare metadata
        CompareField(result.MetadataChanges, "ForeningsId", old.ForeningsId, @new.ForeningsId);
        CompareField(result.MetadataChanges, "ForeningsNamn", old.ForeningsNamn, @new.ForeningsNamn);
        CompareField(result.MetadataChanges, "Organisationsnummer", old.Organisationsnummer, @new.Organisationsnummer);
        CompareField(result.MetadataChanges, "KommunId", old.KommunId, @new.KommunId);
        CompareField(result.MetadataChanges, "NarvarokortNummer", old.Kort.NarvarokortNummer, @new.Kort.NarvarokortNummer);
        CompareField(result.MetadataChanges, "NamnPaKort", old.Kort.NamnPaKort, @new.Kort.NamnPaKort);
        CompareField(result.MetadataChanges, "Lokal", old.Kort.Lokal, @new.Kort.Lokal);
        CompareField(result.MetadataChanges, "Aktivitet", old.Kort.Aktivitet, @new.Kort.Aktivitet);

        // Compare sammankomster (matched by Kod)
        var oldByKod = old.Kort.Sammankomster.ToDictionary(s => s.Kod, StringComparer.Ordinal);
        var newByKod = @new.Kort.Sammankomster.ToDictionary(s => s.Kod, StringComparer.Ordinal);

        foreach (var kvp in oldByKod)
        {
            if (!newByKod.TryGetValue(kvp.Key, out var newSammankomst))
            {
                result.RemovedSammankomster.Add(kvp.Value);
            }
            else
            {
                var diff = CompareSammankomst(kvp.Value, newSammankomst);
                if (diff is not null)
                    result.ModifiedSammankomster.Add(diff);
            }
        }

        foreach (var kvp in newByKod)
        {
            if (!oldByKod.ContainsKey(kvp.Key))
                result.AddedSammankomster.Add(kvp.Value);
        }

        // Compare register
        ComparePersonList(old.Kort.Deltagare, @new.Kort.Deltagare, result.AddedDeltagare, result.RemovedDeltagare);
        ComparePersonList(old.Kort.Ledare, @new.Kort.Ledare, result.AddedLedare, result.RemovedLedare);

        return result;
    }

    private static DakSammankomstDiff? CompareSammankomst(DakSammankomst old, DakSammankomst @new)
    {
        var diff = new DakSammankomstDiff { Kod = old.Kod };

        CompareField(diff.FieldChanges, "Datum",
            old.GetDateString(), @new.GetDateString());
        CompareField(diff.FieldChanges, "StartTid",
            old.GetStartTimeString(), @new.GetStartTimeString());
        CompareField(diff.FieldChanges, "DurationMinutes",
            old.DurationMinutes.ToString(CultureInfo.InvariantCulture),
            @new.DurationMinutes.ToString(CultureInfo.InvariantCulture));
        CompareField(diff.FieldChanges, "Aktivitet", old.Aktivitet, @new.Aktivitet);
        CompareField(diff.FieldChanges, "Typ", old.Typ, @new.Typ);

        ComparePersonList(old.Deltagare, @new.Deltagare, diff.AddedDeltagare, diff.RemovedDeltagare);
        ComparePersonList(old.Ledare, @new.Ledare, diff.AddedLedare, diff.RemovedLedare);

        var hasChanges =
            diff.FieldChanges.Count > 0 ||
            diff.AddedDeltagare.Count > 0 ||
            diff.RemovedDeltagare.Count > 0 ||
            diff.AddedLedare.Count > 0 ||
            diff.RemovedLedare.Count > 0;

        return hasChanges ? diff : null;
    }

    private static void CompareField(List<DakFieldChange> changes, string fieldName, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue ?? "", newValue ?? "", StringComparison.Ordinal))
            changes.Add(new DakFieldChange(fieldName, oldValue, newValue));
    }

    private static void ComparePersonList(List<DakDeltagare> oldList, List<DakDeltagare> newList,
        List<DakDeltagare> added, List<DakDeltagare> removed)
    {
        var oldUids = new HashSet<string>(oldList.Select(d => d.Uid), StringComparer.Ordinal);
        var newUids = new HashSet<string>(newList.Select(d => d.Uid), StringComparer.Ordinal);

        removed.AddRange(oldList.Where(d => !newUids.Contains(d.Uid)));
        added.AddRange(newList.Where(d => !oldUids.Contains(d.Uid)));
    }
}
