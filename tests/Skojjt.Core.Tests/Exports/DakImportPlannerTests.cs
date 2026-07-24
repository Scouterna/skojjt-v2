using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Core.Exports;

namespace Skojjt.Core.Tests.Exports;

[TestClass]
public class DakImportPlannerTests
{
    private static DakData BuildDak(
        string cardLokal,
        params DakSammankomst[] sammankomster)
    {
        var dak = new DakData();
        dak.Kort.Lokal = cardLokal;
        dak.Kort.Sammankomster.AddRange(sammankomster);
        return dak;
    }

    private static DakSammankomst Meeting(
        DateTime datum,
        int duration,
        string aktivitet,
        string lokal,
        params (string uid, bool leader)[] persons)
    {
        var s = new DakSammankomst("100-20251-0315", datum, duration, aktivitet) { Lokal = lokal };
        foreach (var (uid, leader) in persons)
        {
            var d = new DakDeltagare(uid, "F", "E", "200501010020", leader);
            if (leader) s.Ledare.Add(d); else s.Deltagare.Add(d);
        }
        return s;
    }

    private static DakExistingMeeting Existing(
        int id, DateOnly date, TimeOnly start, int duration, string name,
        string location, bool isHike, params int[] attending)
        => new()
        {
            MeetingId = id,
            Date = date,
            StartTime = start,
            DurationMinutes = duration,
            Name = name,
            Location = location,
            IsHike = isHike,
            AttendingPersonIds = attending
        };

    [TestMethod]
    public void Plan_NewMeeting_WhenNoExistingOnDate()
    {
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 3, 15, 18, 30, 0), 90, "Möte", "Scouthuset", ("1", false)));

        var preview = DakImportPlanner.Plan(dak, new List<DakExistingMeeting>(), new HashSet<int> { 1 });

        Assert.HasCount(1, preview.NewMeetings);
        Assert.IsEmpty(preview.Conflicts);
        Assert.IsEmpty(preview.UnchangedMeetings);
    }

    [TestMethod]
    public void Plan_Unchanged_WhenIdentical()
    {
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 3, 15, 18, 30, 0), 90, "Möte", "Scouthuset", ("1", false)));

        var existing = new List<DakExistingMeeting>
        {
            Existing(1, new DateOnly(2025, 3, 15), new TimeOnly(18, 30), 90, "Möte", "Scouthuset", false, 1)
        };

        var preview = DakImportPlanner.Plan(dak, existing, new HashSet<int> { 1 });

        Assert.HasCount(1, preview.UnchangedMeetings);
        Assert.IsEmpty(preview.Conflicts);
        Assert.IsEmpty(preview.NewMeetings);
    }

    [TestMethod]
    public void Plan_Conflict_WhenDurationDiffers()
    {
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 3, 15, 18, 30, 0), 90, "Möte", "Scouthuset", ("1", false)));

        var existing = new List<DakExistingMeeting>
        {
            Existing(1, new DateOnly(2025, 3, 15), new TimeOnly(18, 30), 60, "Möte", "Scouthuset", false, 1)
        };

        var preview = DakImportPlanner.Plan(dak, existing, new HashSet<int> { 1 });

        Assert.HasCount(1, preview.Conflicts);
        Assert.IsNotEmpty(preview.Conflicts[0].Differences);
    }

    [TestMethod]
    public void Plan_SkipsUnknownPersons()
    {
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 3, 15, 18, 30, 0), 90, "Möte", "Scouthuset", ("1", false), ("999", false)));

        var existing = new List<DakExistingMeeting>
        {
            Existing(1, new DateOnly(2025, 3, 15), new TimeOnly(18, 30), 90, "Möte", "Scouthuset", false, 1)
        };

        var preview = DakImportPlanner.Plan(dak, existing, new HashSet<int> { 1 });

        Assert.HasCount(1, preview.SkippedPersons);
        Assert.AreEqual("999", preview.SkippedPersons[0].Uid);
        // The known person still matches, so the meeting is unchanged.
        Assert.HasCount(1, preview.UnchangedMeetings);
    }

    [TestMethod]
    public void Plan_HikeTag_RoundTripsAsUnchanged()
    {
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 3, 15, 10, 0, 0), 480,
                DakActivityTags.Encode("Vandring", true), "Scouthuset", ("1", false)));

        var existing = new List<DakExistingMeeting>
        {
            Existing(1, new DateOnly(2025, 3, 15), new TimeOnly(10, 0), 480, "Vandring", "Scouthuset", true, 1)
        };

        var preview = DakImportPlanner.Plan(dak, existing, new HashSet<int> { 1 });

        Assert.IsEmpty(preview.Conflicts);
        Assert.HasCount(1, preview.UnchangedMeetings);
        Assert.IsTrue(preview.UnchangedMeetings[0].IsHike);
        Assert.AreEqual("Vandring", preview.UnchangedMeetings[0].Name);
    }

    [TestMethod]
    public void Plan_EmptyLocation_MatchesCardDefault()
    {
        // Exporter emits card-level Lokal when meeting has no location; empty existing
        // location must not be reported as a conflict.
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 3, 15, 18, 30, 0), 90, "Möte", "Scouthuset", ("1", false)));

        var existing = new List<DakExistingMeeting>
        {
            Existing(1, new DateOnly(2025, 3, 15), new TimeOnly(18, 30), 90, "Möte", "", false, 1)
        };

        var preview = DakImportPlanner.Plan(dak, existing, new HashSet<int> { 1 });

        Assert.IsEmpty(preview.Conflicts);
        Assert.HasCount(1, preview.UnchangedMeetings);
    }

    [TestMethod]
    public void Plan_OutOfRangeMeeting_IsExcludedAndReported()
    {
        // Spring semester 2025 = 2025-01-01 .. 2025-06-30. A December meeting is out of range.
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 12, 10, 18, 30, 0), 90, "Höstmöte", "Scouthuset", ("1", false)));

        var range = (new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30));

        var preview = DakImportPlanner.Plan(dak, new List<DakExistingMeeting>(), new HashSet<int> { 1 }, range);

        Assert.IsEmpty(preview.NewMeetings);
        Assert.IsEmpty(preview.Conflicts);
        Assert.HasCount(1, preview.OutOfRangeMeetings);
        Assert.AreEqual(new DateOnly(2025, 12, 10), preview.OutOfRangeMeetings[0].Date);
        Assert.AreEqual("Höstmöte", preview.OutOfRangeMeetings[0].Name);
    }

    [TestMethod]
    public void Plan_InRangeMeeting_IsImported_WhenRangeGiven()
    {
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 3, 15, 18, 30, 0), 90, "Möte", "Scouthuset", ("1", false)));

        var range = (new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30));

        var preview = DakImportPlanner.Plan(dak, new List<DakExistingMeeting>(), new HashSet<int> { 1 }, range);

        Assert.HasCount(1, preview.NewMeetings);
        Assert.IsEmpty(preview.OutOfRangeMeetings);
    }

    [TestMethod]
    public void Plan_RangeBoundaries_AreInclusive()
    {
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 1, 1, 18, 30, 0), 90, "Första", "Scouthuset", ("1", false)),
            Meeting(new DateTime(2025, 6, 30, 18, 30, 0), 90, "Sista", "Scouthuset", ("1", false)));

        var range = (new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30));

        var preview = DakImportPlanner.Plan(dak, new List<DakExistingMeeting>(), new HashSet<int> { 1 }, range);

        Assert.HasCount(2, preview.NewMeetings);
        Assert.IsEmpty(preview.OutOfRangeMeetings);
    }

    [TestMethod]
    public void Plan_NoRange_ImportsAllDates()
    {
        var dak = BuildDak("Scouthuset",
            Meeting(new DateTime(2025, 12, 10, 18, 30, 0), 90, "Höstmöte", "Scouthuset", ("1", false)));

        var preview = DakImportPlanner.Plan(dak, new List<DakExistingMeeting>(), new HashSet<int> { 1 });

        Assert.HasCount(1, preview.NewMeetings);
        Assert.IsEmpty(preview.OutOfRangeMeetings);
    }
}

[TestClass]
public class DakActivityTagsTests
{
    [TestMethod]
    public void Encode_AppendsHikeTag_WhenHike()
    {
        Assert.AreEqual("Vandring #hike", DakActivityTags.Encode("Vandring", true));
    }

    [TestMethod]
    public void Encode_Idempotent_WhenAlreadyTagged()
    {
        var once = DakActivityTags.Encode("Vandring", true);
        Assert.AreEqual(once, DakActivityTags.Encode(once, true));
    }

    [TestMethod]
    public void Encode_LeavesUntouched_WhenNotHike()
    {
        Assert.AreEqual("Möte", DakActivityTags.Encode("Möte", false));
    }

    [TestMethod]
    public void DecodeAndStrip_RecoverOriginal()
    {
        var encoded = DakActivityTags.Encode("Vandring", true);
        Assert.IsTrue(DakActivityTags.DecodeIsHike(encoded));
        Assert.AreEqual("Vandring", DakActivityTags.StripTag(encoded));
    }

    [TestMethod]
    public void DecodeIsHike_False_ForPlainActivity()
    {
        Assert.IsFalse(DakActivityTags.DecodeIsHike("Möte"));
        Assert.AreEqual("Möte", DakActivityTags.StripTag("Möte"));
    }
}
