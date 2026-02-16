namespace Skojjt.Core.Exports;

/// <summary>
/// Normalizes a <see cref="DakData"/> instance for deterministic comparison.
/// Sorts sammankomster by Datum then Kod (as int), and deltagare/ledare by Uid.
/// </summary>
public static class DakNormalizer
{
    /// <summary>
    /// Normalize a DakData instance in-place. Returns the same instance for chaining.
    /// </summary>
    public static DakData Normalize(DakData dak)
    {
        // Sort register lists
        SortByUid(dak.Kort.Deltagare);
        SortByUid(dak.Kort.Ledare);

        // Sort sammankomster by datum, then kod as integer
        dak.Kort.Sammankomster.Sort((a, b) =>
        {
            var dateCmp = a.Datum.CompareTo(b.Datum);
            if (dateCmp != 0) return dateCmp;

            var aKod = int.TryParse(a.Kod, out var ai) ? ai : int.MaxValue;
            var bKod = int.TryParse(b.Kod, out var bi) ? bi : int.MaxValue;
            return aKod.CompareTo(bKod);
        });

        // Sort deltagare/ledare within each sammankomst
        foreach (var sammankomst in dak.Kort.Sammankomster)
        {
            SortByUid(sammankomst.Deltagare);
            SortByUid(sammankomst.Ledare);
        }

        return dak;
    }

    private static void SortByUid(List<DakDeltagare> list) =>
        list.Sort((a, b) => string.Compare(a.Uid, b.Uid, StringComparison.Ordinal));
}
