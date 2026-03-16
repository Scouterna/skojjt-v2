using System.Text;
using Skojjt.Core.Entities;
using Skojjt.Core.Exports;
using Skojjt.Core.Interfaces;
using Skojjt.Core.Services;

namespace Skojjt.Infrastructure.Exports;

/// <summary>
/// Generates a members CSV file for Gothenburg municipality attendance grant (aktivitetsbidrag).
/// Format: "Förnamn;Efternamn;Personnummer;Har funktionsnedsättning"
/// UTF-8 with BOM, semicolon-delimited.
/// 
/// See documentation at:
/// https://goteborg.se/wps/portal/start/foretag-och-organisationer/foreningar/kulturstod-och-bidrag-till-foreningar/ansok-om-bidrag-till-foreningar-inom-idrott-och-fritid/aktivitetsbidrag
/// </summary>
public class MembersCsvExporter : IMembersCsvExporter
{
    private readonly IScoutGroupRepository _scoutGroupRepository;
    private readonly ISemesterRepository _semesterRepository;
    private readonly ITroopRepository _troopRepository;
    private readonly IPersonRepository _personRepository;
    private readonly IMeetingRepository _meetingRepository;

    public MembersCsvExporter(
        IScoutGroupRepository scoutGroupRepository,
        ISemesterRepository semesterRepository,
        ITroopRepository troopRepository,
        IPersonRepository personRepository,
        IMeetingRepository meetingRepository)
    {
        _scoutGroupRepository = scoutGroupRepository;
        _semesterRepository = semesterRepository;
        _troopRepository = troopRepository;
        _personRepository = personRepository;
        _meetingRepository = meetingRepository;
    }

    public async Task<ExportResult> ExportAsync(GothenburgCsvInput input, CancellationToken cancellationToken = default)
    {
        var scoutGroup = await _scoutGroupRepository.GetByIdAsync(input.ScoutGroupId, cancellationToken);
        if (scoutGroup == null)
            throw new InvalidOperationException($"Scoutkåren kunde inte hittas: {input.ScoutGroupId}");

        var semester = await _semesterRepository.GetByIdAsync(input.SemesterId, cancellationToken);
        if (semester == null)
            throw new InvalidOperationException($"Terminen kunde inte hittas: {input.SemesterId}");

        // Determine date range based on whether we're using semester or year minimum
        DateOnly fromDate, toDate;
        int minimumMeetings;
        string period;

        if (input.UseSemesterMinimum)
        {
            (fromDate, toDate) = semester.GetStartAndEndDates();
            minimumMeetings = scoutGroup.AttendanceMinSemester;
            period = semester.DisplayName;
        }
        else
        {
            // Year-based: use full year
            fromDate = new DateOnly(semester.Year, 1, 1);
            toDate = new DateOnly(semester.Year, 12, 31);
            minimumMeetings = scoutGroup.AttendanceMinYear;
            period = semester.Year.ToString();
        }

        // Collect attendance data for all persons across all troops
        var personMeetingCounts = new Dictionary<int, int>();
        var personsDict = new Dictionary<int, Person>();

        var numberOfSemesters = input.UseSemesterMinimum ? 1 : 2;
        for (int i = 0; i < numberOfSemesters; i++)
        {
            // Get all troops for this scout group and semester
            var troops = await _troopRepository.GetByScoutGroupAndSemesterAsync(input.ScoutGroupId, semester.Id, cancellationToken);

            foreach (var troop in troops)
            {
                // Get meetings with attendance for this troop within the date range
                var meetings = await _meetingRepository.GetByTroopAndDateRangeAsync(troop.Id, fromDate, toDate, cancellationToken);

                foreach (var meeting in meetings)
                {
                    // Skip hike meetings if not included in attendance stats
                    if (!scoutGroup.AttendanceInclHike && meeting.IsHike)
                        continue;

                    foreach (var attendance in meeting.Attendances)
                    {
                        var personId = attendance.PersonId;

                        // Count meeting attendance
                        personMeetingCounts.TryGetValue(personId, out var count);
                        personMeetingCounts[personId] = count + 1;

                        // Cache person if not already cached
                        if (!personsDict.ContainsKey(personId) && attendance.Person != null)
                        {
                            personsDict[personId] = attendance.Person;
                        }
                    }
                }
            }
            semester = semester.GetOtherSemesterSameYear();
        }
        // Load persons that weren't included in the attendance navigation property
        var missingPersonIds = personMeetingCounts.Keys.Except(personsDict.Keys).ToList();
        if (missingPersonIds.Any())
        {
            var allPersons = await _personRepository.GetByScoutGroupAsync(input.ScoutGroupId, cancellationToken);
            foreach (var person in allPersons.Where(p => missingPersonIds.Contains(p.Id)))
            {
                personsDict[person.Id] = person;
            }
        }

        // Generate CSV with UTF-8 BOM
        var sb = new StringBuilder();
        sb.Append('\ufeff'); // BOM for Excel
        sb.AppendLine("Förnamn;Efternamn;Personnummer;Har funktionsnedsättning");

        // Filter and sort persons
        var eligiblePersons = personMeetingCounts
            .Where(kvp => kvp.Value >= minimumMeetings)
            .Where(kvp => personsDict.ContainsKey(kvp.Key))
            .Select(kvp => personsDict[kvp.Key])
            .Where(p => (!(p.PersonalNumber is null) && p.PersonalNumber.IsValid)) // Must have valid 12-digit personal number
            .OrderBy(p => p.FirstName)
            .ThenBy(p => p.LastName)
            .ToList();

        foreach (var person in eligiblePersons)
        {
            // Format personal number with dash: YYYYMMDD-XXXX
            var personnummer = person.PersonalNumber!;
            var formattedPersonnummer = personnummer.ToFormattedString();

            sb.Append(person.FirstName);
            sb.Append(';');
            sb.Append(person.LastName);
            sb.Append(';');
            sb.Append(formattedPersonnummer);
            sb.Append(';');
            sb.AppendLine("Nej");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fileName = $"{scoutGroup.Name.Replace(" ", "_")}-{period}.csv";

        return new ExportResult(bytes, fileName, "text/csv; charset=utf-8");
    }

    private static int GetYearFromMemberYears(Person person, int targetYear)
    {
        return person.MemberYears.Contains(targetYear) ? targetYear : 0;
    }
}
