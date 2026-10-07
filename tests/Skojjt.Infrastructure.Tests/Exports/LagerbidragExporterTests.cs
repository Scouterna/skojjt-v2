using Microsoft.VisualStudio.TestTools.UnitTesting;
using Skojjt.Core.Entities;
using Skojjt.Core.Exports;
using Skojjt.Infrastructure.Exports;

namespace Skojjt.Infrastructure.Tests.Exports;

[TestClass]
public class LagerbidragExporterTests
{
    private static readonly DateOnly CampStart = new(2025, 6, 10);
    private static readonly DateOnly CampEnd = new(2025, 6, 12);

    [TestMethod]
    public async Task ExportAsync_Stockholm_ListsAllScoutsInAgeRangeAndNoOneOverMaxAge()
    {
        // Adult leaders sort first by birth year, so they must not push scouts out of the list
        var data = CreateCampData(
            Leader(1, "Lisa", "Vuxen", 1980),
            Leader(2, "Lars", "Vuxen", 1985),
            Scout(3, "Anna", "Scout", 2012),
            Scout(4, "Bo", "Scout", 2013),
            Scout(5, "Cia", "Scout", 2014));

        var html = await ExportStockholmHtml(data);

        Assert.Contains("Anna Scout", html);
        Assert.Contains("Bo Scout", html);
        Assert.Contains("Cia Scout", html);
        Assert.DoesNotContain("Lisa Vuxen", html);
        Assert.DoesNotContain("Lars Vuxen", html);
    }

    [TestMethod]
    public async Task ExportAsync_Stockholm_ListsLeadersWithinAgeRange()
    {
        // Scoutlägerstöd counts members aged 7-20 regardless of role
        var data = CreateCampData(
            Leader(1, "Ung", "Ledare", 2005),
            Scout(2, "Anna", "Scout", 2012));

        var html = await ExportStockholmHtml(data);

        Assert.Contains("Ung Ledare", html);
        Assert.Contains("Anna Scout", html);
    }

    [TestMethod]
    public async Task ExportAsync_Stockholm_ExcludesScoutsUnderMinAge()
    {
        var data = CreateCampData(
            Scout(1, "Anna", "Scout", 2012),
            Scout(2, "Lilla", "Bäver", 2020));

        var html = await ExportStockholmHtml(data);

        Assert.Contains("Anna Scout", html);
        Assert.DoesNotContain("Lilla Bäver", html);
    }

    private static async Task<string> ExportStockholmHtml(AttendanceReportData data)
    {
        var exporter = new LagerbidragExporter();
        var result = await exporter.ExportAsync(new LagerbidragInput
        {
            AttendanceData = data,
            ContactPerson = "Kontakt",
            Site = "Lägerplatsen",
            DateFrom = CampStart,
            DateTo = CampEnd,
            Region = "sthlm"
        });
        return System.Text.Encoding.UTF8.GetString(result.Data);
    }

    private static TroopPersonInfo Scout(int id, string firstName, string lastName, int birthYear) =>
        CreateMember(id, firstName, lastName, birthYear, isLeader: false);

    private static TroopPersonInfo Leader(int id, string firstName, string lastName, int birthYear) =>
        CreateMember(id, firstName, lastName, birthYear, isLeader: true);

    private static TroopPersonInfo CreateMember(int id, string firstName, string lastName, int birthYear, bool isLeader) => new()
    {
        Person = new Person
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            BirthDate = new DateOnly(birthYear, 1, 1)
        },
        IsLeader = isLeader
    };

    private static AttendanceReportData CreateCampData(params TroopPersonInfo[] members)
    {
        var scoutGroup = new ScoutGroup { Id = 1, Name = "Test Scout Group" };
        var semester = new Semester(20251, 2025, true);
        var troop = new Troop { Id = 1, ScoutGroupId = scoutGroup.Id, ScoutnetId = 100, Name = "Test Troop", SemesterId = semester.Id };
        var attendingIds = members.Select(m => m.Person.Id).ToList();

        var meetings = new List<MeetingInfo>();
        for (var date = CampStart; date <= CampEnd; date = date.AddDays(1))
        {
            meetings.Add(new MeetingInfo
            {
                Meeting = new Meeting { Id = meetings.Count + 1, Name = "Läger", MeetingDate = date, IsHike = true },
                AttendingPersonIds = attendingIds
            });
        }

        return new AttendanceReportData
        {
            ScoutGroup = scoutGroup,
            Troop = troop,
            Semester = semester,
            DefaultLocation = "Scouthuset",
            IncludeHikeMeetings = true,
            TroopPersons = members.ToList(),
            Meetings = meetings
        };
    }
}
