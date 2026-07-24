using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Core.Entities;
using Skojjt.Core.Exports;
using Skojjt.Core.Utilities;
using Skojjt.Infrastructure.Exports;

namespace Skojjt.Infrastructure.Tests.Exports;

/// <summary>
/// Verifies the invariant that exporting a troop's DAK and then re-importing the same
/// file produces no changes (a no-op).
/// </summary>
[TestClass]
public class DakImportRoundTripTests
{
    [TestMethod]
    public async Task ExportThenImport_IsNoOp()
    {
        var data = CreateTestData();

        // Export to DAK XML.
        var exporter = new DakXmlExporter();
        var export = await exporter.ExportAsync(data);

        // Parse the exported file back.
        var parse = DakXmlReader.Parse(export.Data, "roundtrip.xml");
        var issues = string.Join("\n", parse.Issues.Select(i => i.ToString()));
        Assert.IsNotNull(parse.Data, issues);
        Assert.IsFalse(parse.HasErrors, issues);

        // Build the "existing" Skojjt state from the same source meetings.
        var existing = data.Meetings
            .Select(mi => new DakExistingMeeting
            {
                MeetingId = mi.Meeting.Id,
                Date = mi.Meeting.MeetingDate,
                StartTime = mi.Meeting.StartTime,
                DurationMinutes = mi.Meeting.DurationMinutes,
                Name = mi.Meeting.Name,
                Location = mi.Meeting.Location,
                IsHike = mi.Meeting.IsHike,
                AttendingPersonIds = mi.AttendingPersonIds.ToList()
            })
            .ToList();

        var memberIds = data.TroopPersons.Select(tp => tp.Person.Id).ToHashSet();

        var preview = DakImportPlanner.Plan(parse.Data, existing, memberIds);

        Assert.IsEmpty(preview.NewMeetings, "No new meetings expected on round-trip.");
        Assert.IsEmpty(preview.Conflicts,
            "No conflicts expected on round-trip. Differences: " +
            string.Join(" | ", preview.Conflicts.SelectMany(c => c.Differences)));
        Assert.IsEmpty(preview.SkippedPersons, "No skipped persons expected on round-trip.");
        Assert.HasCount(data.Meetings.Count, preview.UnchangedMeetings);
    }

    private static AttendanceReportData CreateTestData()
    {
        var scoutGroup = new ScoutGroup
        {
            Id = 1,
            Name = "Test Scout Group",
            MunicipalityId = "1480",
            AssociationId = "12345",
            OrganisationNumber = "123456-7890"
        };

        var semester = new Semester(20251, 2025, true);

        var troop = new Troop
        {
            Id = 1,
            ScoutnetId = 100,
            Name = "Test Troop",
            SemesterId = semester.Id
        };

        var person1 = new Person
        {
            Id = 1,
            FirstName = "Anna",
            LastName = "Andersson",
            PersonalNumber = "200501010020".GetNullablePersonnummer()
        };

        var person2 = new Person
        {
            Id = 2,
            FirstName = "Erik",
            LastName = "Eriksson",
            PersonalNumber = "198001010019".GetNullablePersonnummer()
        };

        var regularMeeting = new Meeting
        {
            Id = 1,
            Name = "Vanligt möte",
            MeetingDate = new DateOnly(2025, 3, 15),
            StartTime = new TimeOnly(18, 30),
            DurationMinutes = 90,
            Location = "Klubbstugan",
            IsHike = false
        };

        var hikeMeeting = new Meeting
        {
            Id = 2,
            Name = "Vandring",
            MeetingDate = new DateOnly(2025, 4, 12),
            StartTime = new TimeOnly(9, 0),
            DurationMinutes = 480,
            Location = "Skogen",
            IsHike = true
        };

        return new AttendanceReportData
        {
            ScoutGroup = scoutGroup,
            Troop = troop,
            Semester = semester,
            DefaultLocation = "Scouthuset",
            IncludeHikeMeetings = true,
            TroopPersons =
            [
                new TroopPersonInfo { Person = person1, IsLeader = false, Patrol = "Örn" },
                new TroopPersonInfo { Person = person2, IsLeader = true }
            ],
            Meetings =
            [
                new MeetingInfo { Meeting = regularMeeting, AttendingPersonIds = [1, 2] },
                new MeetingInfo { Meeting = hikeMeeting, AttendingPersonIds = [1] }
            ]
        };
    }
}
