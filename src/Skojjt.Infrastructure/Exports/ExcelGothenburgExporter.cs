using ClosedXML.Excel;
using Skojjt.Core.Exports;

namespace Skojjt.Infrastructure.Exports;

/// <summary>
/// Exports attendance data to Excel format (Gothenburg format).
/// Based on the narvarokort.xlsx template format.
/// </summary>
public class ExcelGothenburgExporter : IAttendanceExporter
{
    public string ExporterId => "excel-gbg";
    public string DisplayName => "Excel (Göteborg)";

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
        const int startRowPersons = 13;
        const int firstMeetingColumn = 11;

        // Header information
        ws.Cell("A1").Value = "Närvarokort Nr:";
        ws.Cell("E1").Value = GetUniqueId(data.Troop);
        ws.Cell("H1").Value = "År:";
        ws.Cell("I1").Value = data.Semester.Year;

        ws.Cell("A2").Value = "Grupp/Avdelning:";
        ws.Cell("D2").Value = data.Troop.Name;

        ws.Cell("A3").Value = "Huvudsaklig verksamhet:";
        ws.Cell("D3").Value = "Scouting";

        ws.Cell("A4").Value = "Lokal:";
        ws.Cell("D4").Value = data.DefaultLocation;

        // Semester selection (VT/HT)
        ws.Cell("A6").Value = "VT";
        ws.Cell("A7").Value = "HT";
        if (data.Semester.IsAutumn)
            ws.Cell("C7").Value = "X";
        else
            ws.Cell("C6").Value = "X";

        // Column headers for persons
        ws.Cell("B12").Value = "Namn";
        ws.Cell("H12").Value = "K/M";
        ws.Cell("I12").Value = "Postnr";
        ws.Cell("J12").Value = "Föd.datum";

        // Time labels
        ws.Cell("A7").Value = "Starttid";
        ws.Cell("A9").Value = "Sluttid";
        ws.Cell("A10").Value = "Månad";
        ws.Cell("A11").Value = "Dag";

        // Separate participants and leaders
        var participants = data.TroopPersons.Where(tp => !tp.IsLeader).ToList();
        var leaders = data.TroopPersons.Where(tp => tp.IsLeader).ToList();
        var personsDict = data.TroopPersons.ToDictionary(tp => tp.Person.Id);

        // Filter meetings based on IncludeHikeMeetings setting
        var meetings = data.Meetings
            .Where(m => data.IncludeHikeMeetings || !m.Meeting.IsHike)
            .ToList();

        // Write meeting headers
        for (int i = 0; i < meetings.Count; i++)
        {
            var meeting = meetings[i].Meeting;
            var col = firstMeetingColumn + i;

            ws.Cell(2, col).Value = meeting.Name;
            ws.Cell(7, col).Value = meeting.StartTime.Hour;
            ws.Cell(9, col).Value = meeting.StartTime.AddMinutes(meeting.DurationMinutes).Hour;
            ws.Cell(10, col).Value = meeting.MeetingDate.Month;
            ws.Cell(11, col).Value = meeting.MeetingDate.Day;
        }

        // Write participants
        for (int i = 0; i < participants.Count; i++)
        {
            var tp = participants[i];
            var person = tp.Person;

			var row = startRowPersons + i;

            ws.Cell(row, 2).Value = $"{person.FirstName} {person.LastName}";
			ws.Cell(row, 8).Value = person.PersonalNumber is null ? '?' : person.PersonalNumber.IsFemale ? 'K' : 'M';
            ws.Cell(row, 9).Value = person.ZipCode ?? "";
            ws.Cell(row, 10).Value = person.PersonalNumber is null ? "?" : person.PersonalNumber.BirthDayString;

			// Mark attendance for each meeting
			for (int j = 0; j < meetings.Count; j++)
            {
                if (meetings[j].AttendingPersonIds.Contains(person.Id))
                {
                    ws.Cell(row, firstMeetingColumn + j).Value = 1;
                }
            }
        }

        // Write leaders
        for (int i = 0; i < leaders.Count; i++)
        {
            var tp = leaders[i];
            var person = tp.Person;
            var row = startRowPersons + participants.Count + i;

            ws.Cell(row, 2).Value = "Ledare:";
            ws.Cell(row, 3).Value = $"{person.FirstName} {person.LastName}";
            ws.Cell(row, 8).Value = person.PersonalNumber is null ? "K" : person.PersonalNumber.IsFemale ? "K" : "M";
            ws.Cell(row, 9).Value = person.ZipCode ?? "";
            ws.Cell(row, 10).Value = person.PersonalNumber is null ? "?" : person.PersonalNumber.BirthDayString;

			// Mark attendance for each meeting
			for (int j = 0; j < meetings.Count; j++)
            {
                if (meetings[j].AttendingPersonIds.Contains(person.Id))
                {
                    ws.Cell(row, firstMeetingColumn + j).Value = 1;
                }
            }
        }

        // Auto-fit columns
        ws.Columns().AdjustToContents();
    }

    private static string GetUniqueId(Core.Entities.Troop troop)
    {
        return $"{troop.ScoutnetId}-{troop.SemesterId}";
    }

    private static bool IsFemale(string? personnummer)
    {
        if (string.IsNullOrEmpty(personnummer) || personnummer.Length < 11)
            return false;
        
        return int.TryParse(personnummer[^2].ToString(), out var digit) && (digit & 1) == 0;
    }

    private static string GetBirthDateString(string? personnummer)
    {
        if (string.IsNullOrEmpty(personnummer) || personnummer.Length < 8)
            return "";
        
        return personnummer[..8];
    }
}
