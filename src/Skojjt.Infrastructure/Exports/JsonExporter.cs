using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Skojjt.Core.Exports;

namespace Skojjt.Infrastructure.Exports;

/// <summary>
/// Exports attendance data to JSON format.
/// </summary>
public class JsonExporter : IAttendanceExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ExporterId => "json";
    public string DisplayName => "JSON";

    public Task<ExportResult> ExportAsync(AttendanceReportData data, CancellationToken cancellationToken = default)
    {
        var exportData = BuildExportData(data);
        var json = JsonSerializer.Serialize(exportData, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"{data.Troop.Name}-{data.Semester.DisplayName}.json";

        return Task.FromResult(new ExportResult(bytes, fileName, "application/json"));
    }

    private static JsonExportData BuildExportData(AttendanceReportData data)
    {
        var personsDict = data.TroopPersons.ToDictionary(tp => tp.Person.Id);

        return new JsonExportData
        {
            ScoutGroup = new JsonScoutGroup
            {
                Id = data.ScoutGroup.Id,
                Name = data.ScoutGroup.Name,
                OrganisationNumber = data.ScoutGroup.OrganisationNumber,
                AssociationId = data.ScoutGroup.AssociationId,
                MunicipalityId = data.ScoutGroup.MunicipalityId
            },
            Troop = new JsonTroop
            {
                Id = data.Troop.Id,
                ScoutnetId = data.Troop.ScoutnetId,
                Name = data.Troop.Name
            },
            Semester = new JsonSemester
            {
                Id = data.Semester.Id,
                Year = data.Semester.Year,
                IsAutumn = data.Semester.IsAutumn,
                DisplayName = data.Semester.DisplayName
            },
            Participants = data.TroopPersons
                .Where(tp => !tp.IsLeader)
                .Select(tp => MapPerson(tp.Person, tp))
                .ToList(),
            Leaders = data.TroopPersons
                .Where(tp => tp.IsLeader)
                .Select(tp => MapPerson(tp.Person, tp))
                .ToList(),
            Meetings = data.Meetings
                .Where(m => data.IncludeHikeMeetings || !m.Meeting.IsHike)
                .Select(m => MapMeeting(m, personsDict))
                .ToList()
        };
    }

    private static JsonPerson MapPerson(Core.Entities.Person person, TroopPersonInfo troopPerson)
    {
        return new JsonPerson
        {
            Id = person.Id,
            FirstName = person.FirstName,
            LastName = person.LastName,
            PersonalNumber = person.PersonalNumber is null ? "?" : person.PersonalNumber.ToString(),
            Email = person.Email,
            Mobile = person.Mobile,
            BirthDate = person.BirthDate?.ToString("yyyy-MM-dd"),
            ZipCode = person.ZipCode,
            ZipName = person.ZipName,
            Patrol = troopPerson.Patrol
        };
    }

    private static JsonMeeting MapMeeting(MeetingInfo meetingInfo, Dictionary<int, TroopPersonInfo> personsDict)
    {
        var meeting = meetingInfo.Meeting;
        
        var participants = new List<int>();
        var leaders = new List<int>();

        foreach (var personId in meetingInfo.AttendingPersonIds)
        {
            if (personsDict.TryGetValue(personId, out var tp))
            {
                if (tp.IsLeader)
                    leaders.Add(personId);
                else
                    participants.Add(personId);
            }
        }

        return new JsonMeeting
        {
            Id = meeting.Id,
            Name = meeting.Name,
            Date = meeting.MeetingDate.ToString("yyyy-MM-dd"),
            StartTime = meeting.StartTime.ToString("HH:mm"),
            DurationMinutes = meeting.DurationMinutes,
            IsHike = meeting.IsHike,
            Location = meeting.Location,
            AttendingParticipantIds = participants,
            AttendingLeaderIds = leaders
        };
    }
}

#region JSON Data Models

internal class JsonExportData
{
    public required JsonScoutGroup ScoutGroup { get; init; }
    public required JsonTroop Troop { get; init; }
    public required JsonSemester Semester { get; init; }
    public required List<JsonPerson> Participants { get; init; }
    public required List<JsonPerson> Leaders { get; init; }
    public required List<JsonMeeting> Meetings { get; init; }
}

internal class JsonScoutGroup
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public string? OrganisationNumber { get; init; }
    public string? AssociationId { get; init; }
    public string? MunicipalityId { get; init; }
}

internal class JsonTroop
{
    public int Id { get; init; }
    public int ScoutnetId { get; init; }
    public required string Name { get; init; }
}

internal class JsonSemester
{
    public int Id { get; init; }
    public int Year { get; init; }
    public bool IsAutumn { get; init; }
    public required string DisplayName { get; init; }
}

internal class JsonPerson
{
    public int Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? PersonalNumber { get; init; }
    public string? Email { get; init; }
    public string? Mobile { get; init; }
    public string? BirthDate { get; init; }
    public string? ZipCode { get; init; }
    public string? ZipName { get; init; }
    public string? Patrol { get; init; }
}

internal class JsonMeeting
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required string Date { get; init; }
    public required string StartTime { get; init; }
    public int DurationMinutes { get; init; }
    public bool IsHike { get; init; }
    public string? Location { get; init; }
    public required List<int> AttendingParticipantIds { get; init; }
    public required List<int> AttendingLeaderIds { get; init; }
}

#endregion
