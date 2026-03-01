using ClosedXML.Excel;
using Skojjt.Core.Exports;

namespace Skojjt.Infrastructure.Exports;

/// <summary>
/// Exports attendance data to Excel format (Stockholm format).
/// Based on the narvarokort_sthlm.xlsx template format.
/// </summary>
public class ExcelStockholmExporter : IAttendanceExporter
{
    private const int MaxMeetingsSmall = 24;
    private const int MaxMeetingsLarge = 36;
    private const int MaxPersonsSmall = 36;
    private const int MaxPersonsLarge = 48;

    public string ExporterId => "excel-sthlm";
    public string DisplayName => "Excel (Stockholm)";

    public Task<ExportResult> ExportAsync(AttendanceReportData data, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Närvarokort");

        BuildWorksheet(worksheet, data);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var bytes = stream.ToArray();
        var fileName = $"{data.Troop.Name}-{data.Semester.DisplayName}.xlsx";

        return Task.FromResult(new ExportResult(
            bytes, 
            fileName, 
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));
    }

    private static void BuildWorksheet(IXLWorksheet ws, AttendanceReportData data)
    {
        const int startRowPersons = 15;
        const int firstMeetingColumn = 7;

        var participants = data.TroopPersons.Where(tp => !tp.IsLeader).ToList();
        var leaders = data.TroopPersons.Where(tp => tp.IsLeader).ToList();
        var totalPersons = participants.Count + leaders.Count;

        // Filter meetings based on IncludeHikeMeetings setting
        var meetings = data.Meetings
            .Where(m => data.IncludeHikeMeetings || !m.Meeting.IsHike)
            .ToList();

        // Validate limits
        var (maxPersons, maxMeetings) = GetLimits(totalPersons, meetings.Count);
        if (totalPersons > maxPersons || meetings.Count > maxMeetings)
        {
            throw new InvalidOperationException(
                $"För många personer ({totalPersons}) eller sammankomster ({meetings.Count}). " +
                $"Max är {maxPersons} personer och {maxMeetings} sammankomster.");
        }

        // Header information - Stockholm format
        ws.Cell("A1").Value = $"Närvarokort Nr {GetUniqueId(data.Troop)}";
        ws.Cell("AJ1").Value = data.Semester.Year;
        
        // Semester indicator
        ws.Cell("D1").Value = data.Semester.IsAutumn 
            ? $"HT-{data.Semester.Year % 100:D2}" 
            : $"VT-{data.Semester.Year % 100:D2}";

        ws.Cell("A3").Value = data.ScoutGroup.Name;
        ws.Cell("A5").Value = "Scouting";
        ws.Cell("C5").Value = data.Troop.Name;
        ws.Cell("A7").Value = data.DefaultLocation;

        // Column headers
        ws.Cell(10, 1).Value = "Starttid";
        ws.Cell(11, 1).Value = "Sluttid";
        ws.Cell(12, 1).Value = "Månad";
        ws.Cell(13, 1).Value = "Dag";

        ws.Cell(14, 2).Value = "Förnamn";
        ws.Cell(14, 3).Value = "Efternamn";
        ws.Cell(14, 4).Value = "K/M";
        ws.Cell(14, 5).Value = "Postnr";
        ws.Cell(14, 6).Value = "Föd.år";

        // Write meeting headers
        for (int i = 0; i < meetings.Count; i++)
        {
            var meeting = meetings[i].Meeting;
            var col = firstMeetingColumn + i;

            ws.Cell(4, col).Value = meeting.Name;
            ws.Cell(10, col).Value = meeting.StartTime.Hour;
            ws.Cell(11, col).Value = meeting.StartTime.AddMinutes(meeting.DurationMinutes).Hour;
            ws.Cell(12, col).Value = meeting.MeetingDate.Month;
            ws.Cell(13, col).Value = meeting.MeetingDate.Day;
        }

        // Write participants
        for (int i = 0; i < participants.Count; i++)
        {
            var tp = participants[i];
            var person = tp.Person;
            var row = startRowPersons + i;

            ws.Cell(row, 2).Value = person.FirstName;
            ws.Cell(row, 3).Value = person.LastName;
            ws.Cell(row, 4).Value = person.PersonalNumber is null ? "?" : person.PersonalNumber.IsFemale ? "k" : "m";
            ws.Cell(row, 5).Value = FormatPostnummer(person.ZipCode);
            ws.Cell(row, 6).Value = person.PersonalNumber is null ? "?" : person.PersonalNumber.BirthDayString;

            // Mark attendance for each meeting
            for (int j = 0; j < meetings.Count; j++)
            {
                if (meetings[j].AttendingPersonIds.Contains(person.Id))
                {
                    ws.Cell(row, firstMeetingColumn + j).Value = "x";
                }
            }
        }

        // Write leaders
        for (int i = 0; i < leaders.Count; i++)
        {
            var tp = leaders[i];
            var person = tp.Person;
            var row = startRowPersons + participants.Count + i;

            ws.Cell(row, 2).Value = person.FirstName;
            ws.Cell(row, 3).Value = person.LastName;
            ws.Cell(row, 4).Value = person.PersonalNumber is null ? "?" : person.PersonalNumber.IsFemale ? "k" : "m";
            ws.Cell(row, 5).Value = FormatPostnummer(person.ZipCode);
            ws.Cell(row, 6).Value = person.PersonalNumber is null ? "?" : person.PersonalNumber.BirthDayString;

            // Mark attendance for each meeting
            for (int j = 0; j < meetings.Count; j++)
            {
                if (meetings[j].AttendingPersonIds.Contains(person.Id))
                {
                    ws.Cell(row, firstMeetingColumn + j).Value = "x";
                }
            }
        }

        // Auto-fit columns
        ws.Columns().AdjustToContents();
    }

    private static (int maxPersons, int maxMeetings) GetLimits(int totalPersons, int meetingCount)
    {
        // Use larger template limits if needed
        if (totalPersons <= MaxPersonsSmall && meetingCount <= MaxMeetingsSmall)
            return (MaxPersonsSmall, MaxMeetingsSmall);
        
        return (MaxPersonsLarge, MaxMeetingsLarge);
    }

    private static string GetUniqueId(Core.Entities.Troop troop)
    {
        return $"{troop.ScoutnetId}-{troop.SemesterId}";
    }

    private static string FormatPostnummer(string? postnummer)
    {
        if (string.IsNullOrEmpty(postnummer))
            return "";
        
        return postnummer.Replace(" ", "");
    }
}
