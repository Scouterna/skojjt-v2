using Skojjt.Core.Exports;

namespace Skojjt.Infrastructure.Exports;

/// <summary>
/// Lagerbidrag (camp subsidy) region limits.
/// </summary>
public record RegionLimits(int MinDays, int MaxDays, int MinAge, int MaxAge, bool CountOverMaxAge);

/// <summary>
/// Data for a person in a lagerbidrag report.
/// </summary>
public class LagerPerson
{
    public int PersonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BirthYear { get; set; }
    public int Age { get; set; }
    public string PostalAddress { get; set; } = string.Empty;
    public int Days { get; set; }
}

/// <summary>
/// Container for lagerbidrag report data.
/// </summary>
public class LagerbidragData
{
    public string ScoutGroupName { get; set; } = string.Empty;
    public string ForeningsId { get; set; } = string.Empty;
    public string Firmatecknare { get; set; } = string.Empty;
    public string FirmatecknarTelefon { get; set; } = string.Empty;
    public string FirmatecknarEmail { get; set; } = string.Empty;
    public string Organisationsnummer { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string BankAccount { get; set; } = string.Empty;
    public string Contact { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Site { get; set; } = string.Empty;
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public bool HikeDuringBreak { get; set; }
    
    public int UpToMaxAge { get; set; }
    public int OverMaxAge { get; set; }
    public int NightsUpToMaxAge { get; set; }
    public int NightsOverMaxAge { get; set; }
    public int TotalNights { get; set; }
    public int TotalDays { get; set; }
    
    public List<LagerPerson> Persons { get; set; } = [];
    public int YoungPersonsCount { get; set; }
    public int OlderPersonsCount { get; set; }
    public int UnderMinAgeCount { get; set; }
    public int TotalPersonsCount { get; set; }
}

/// <summary>
/// Input data for generating a lagerbidrag report.
/// </summary>
public class LagerbidragInput
{
    /// <summary>
    /// Attendance report data containing scout group, troop, and meeting info.
    /// </summary>
    public required AttendanceReportData AttendanceData { get; init; }
    
    /// <summary>
    /// Contact person name.
    /// </summary>
    public required string ContactPerson { get; init; }
    
    /// <summary>
    /// Contact email.
    /// </summary>
    public string ContactEmail { get; init; } = string.Empty;
    
    /// <summary>
    /// Contact phone.
    /// </summary>
    public string ContactPhone { get; init; } = string.Empty;
    
    /// <summary>
    /// Camp site name.
    /// </summary>
    public required string Site { get; init; }
    
    /// <summary>
    /// Start date of the camp.
    /// </summary>
    public required DateOnly DateFrom { get; init; }
    
    /// <summary>
    /// End date of the camp.
    /// </summary>
    public required DateOnly DateTo { get; init; }
    
    /// <summary>
    /// Whether the hike/camp is during a school break.
    /// </summary>
    public bool HikeDuringBreak { get; init; }
    
    /// <summary>
    /// Region for the report (e.g., "gbg" for Gothenburg, "sthlm" for Stockholm).
    /// </summary>
    public required string Region { get; init; }
}

/// <summary>
/// Interface for lagerbidrag export.
/// </summary>
public interface ILagerbidragExporter
{
    /// <summary>
    /// Generate a lagerbidrag report.
    /// </summary>
    Task<ExportResult> ExportAsync(LagerbidragInput input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Exports camp subsidy (lagerbidrag) reports for Gothenburg and Stockholm formats.
/// </summary>
public class LagerbidragExporter : ILagerbidragExporter
{
    private static readonly Dictionary<string, RegionLimits> Limits = new()
    {
        // Gothenburg: min 2 days, max 14 days, 7-25 years + some older
        ["gbg"] = new RegionLimits(2, 15, 7, 25, true),
        // Stockholm: min 2 days, max 7 days, 7-20 years
        ["sthlm"] = new RegionLimits(2, 7, 7, 20, false)
    };

    public Task<ExportResult> ExportAsync(LagerbidragInput input, CancellationToken cancellationToken = default)
    {
        if (!Limits.TryGetValue(input.Region.ToLower(), out var limits))
        {
            throw new ArgumentException($"Unknown region: {input.Region}. Supported regions: gbg, sthlm");
        }

        var data = BuildLagerbidragData(input, limits);
        
        return input.Region.ToLower() switch
        {
            "gbg" => ExportGothenburgAsync(data, limits, cancellationToken),
            "sthlm" => ExportStockholmAsync(data, limits, cancellationToken),
            _ => throw new ArgumentException($"Unknown region: {input.Region}")
        };
    }

    private static LagerbidragData BuildLagerbidragData(LagerbidragInput input, RegionLimits limits)
    {
        ValidateDates(input.DateFrom, input.DateTo, limits);

        var data = input.AttendanceData;
        var scoutGroup = data.ScoutGroup;
        var year = input.DateTo.Year;

        var bidrag = new LagerbidragData
        {
            ScoutGroupName = scoutGroup.Name,
            ForeningsId = scoutGroup.AssociationId ?? string.Empty,
            Firmatecknare = scoutGroup.Signatory ?? string.Empty,
            FirmatecknarTelefon = scoutGroup.SignatoryPhone ?? string.Empty,
            FirmatecknarEmail = scoutGroup.SignatoryEmail ?? string.Empty,
            Organisationsnummer = scoutGroup.OrganisationNumber ?? string.Empty,
            Email = scoutGroup.Email ?? string.Empty,
            Phone = scoutGroup.Phone ?? string.Empty,
            Address = scoutGroup.Address ?? string.Empty,
            ZipCode = scoutGroup.PostalAddress ?? string.Empty,
            BankAccount = scoutGroup.BankAccount ?? string.Empty,
            Contact = input.ContactPerson,
            ContactEmail = input.ContactEmail,
            ContactPhone = input.ContactPhone,
            Site = input.Site,
            DateFrom = input.DateFrom,
            DateTo = input.DateTo,
            HikeDuringBreak = input.HikeDuringBreak
        };

        // Get meetings within the date range (hike meetings only)
        var meetings = data.Meetings
            .Where(m => m.Meeting.IsHike)
            .Where(m => m.Meeting.MeetingDate >= input.DateFrom && m.Meeting.MeetingDate <= input.DateTo)
            .ToList();

        // Build person-days dictionary
        var personDays = new Dictionary<int, int>();
        foreach (var meeting in meetings)
        {
            foreach (var personId in meeting.AttendingPersonIds)
            {
                personDays.TryGetValue(personId, out var days);
                personDays[personId] = days + 1;
            }
        }

        // Create LagerPerson list
        var personsDict = data.TroopPersons.ToDictionary(tp => tp.Person.Id, tp => tp.Person);
        var lagerPersons = new List<LagerPerson>();

        foreach (var (personId, days) in personDays)
        {
            if (!personsDict.TryGetValue(personId, out var person))
                continue;

            var birthYear = person.BirthDate?.Year ?? 0;
            var age = birthYear > 0 ? year - birthYear : 0;
            var postalAddress = $"{person.ZipCode ?? ""} {person.ZipName ?? ""}".Trim();

            lagerPersons.Add(new LagerPerson
            {
                PersonId = personId,
                Name = person.FullName,
                BirthYear = birthYear,
                Age = age,
                PostalAddress = postalAddress,
                Days = days
            });
        }

        // Filter out persons with less than minimum days
        lagerPersons = lagerPersons.Where(p => p.Days >= limits.MinDays).ToList();

        // Sort by days descending to get persons over max age with most days first
        lagerPersons = lagerPersons.OrderByDescending(p => p.Days).ToList();

        // Calculate statistics
        foreach (var person in lagerPersons)
        {
            if (person.Age > limits.MaxAge)
            {
                bidrag.OverMaxAge++;
                bidrag.OlderPersonsCount++;
                bidrag.NightsOverMaxAge += person.Days - 1;
            }
            else if (person.Age >= limits.MinAge)
            {
                bidrag.UpToMaxAge++;
                bidrag.NightsUpToMaxAge += person.Days - 1;
                bidrag.TotalNights += person.Days - 1;
                bidrag.TotalDays += person.Days;
                bidrag.YoungPersonsCount++;
            }
            else
            {
                bidrag.UnderMinAgeCount++;
            }
        }

        // Count older persons if allowed by region
        if (limits.CountOverMaxAge)
        {
            var allowedOverMaxAge = bidrag.UpToMaxAge / 3;
            var countOverMaxAge = 0;
            
            foreach (var person in lagerPersons.Where(p => p.Age > limits.MaxAge))
            {
                if (countOverMaxAge >= allowedOverMaxAge)
                    break;
                    
                countOverMaxAge++;
                bidrag.TotalNights += person.Days - 1;
                bidrag.TotalDays += person.Days;
            }
        }

        // Sort by birth year then name for final list
        lagerPersons = lagerPersons
            .OrderBy(p => p.BirthYear)
            .ThenBy(p => p.Name)
            .ToList();

        bidrag.Persons = lagerPersons;
        bidrag.TotalPersonsCount = lagerPersons.Count;

        return bidrag;
    }

    private static void ValidateDates(DateOnly from, DateOnly to, RegionLimits limits)
    {
        var days = (to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue)).Days + 1;
        
        if (days > limits.MaxDays)
            throw new InvalidOperationException($"Lägret får max vara {limits.MaxDays} dagar");
        
        if (days < limits.MinDays)
            throw new InvalidOperationException($"Lägret måste vara minst {limits.MinDays} dagar");
    }

    private static Task<ExportResult> ExportGothenburgAsync(LagerbidragData data, RegionLimits limits, CancellationToken cancellationToken)
    {
        // For Gothenburg, we generate an HTML report that can be printed
        var html = GenerateGothenburgHtml(data);
        var bytes = System.Text.Encoding.UTF8.GetBytes(html);
        var fileName = $"Lagerbidrag_{data.Site}_{data.DateFrom:yyyy-MM-dd}_{data.DateTo:yyyy-MM-dd}.html";

        return Task.FromResult(new ExportResult(bytes, fileName, "text/html"));
    }

    private static Task<ExportResult> ExportStockholmAsync(LagerbidragData data, RegionLimits limits, CancellationToken cancellationToken)
    {
        // For Stockholm, we also generate an HTML report (original used docx template)
        var html = GenerateStockholmHtml(data);
        var bytes = System.Text.Encoding.UTF8.GetBytes(html);
        var fileName = $"Lagerbidrag_{data.Site}_{data.DateFrom:yyyy-MM-dd}_{data.DateTo:yyyy-MM-dd}.html";

        return Task.FromResult(new ExportResult(bytes, fileName, "text/html"));
    }

    private static string GenerateGothenburgHtml(LagerbidragData data)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'><title>Lägerbidrag Göteborg</title>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;margin:20px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:8px;text-align:left}th{background-color:#4CAF50;color:white}.summary{margin:20px 0}h1,h2{color:#333}</style>");
        sb.AppendLine("</head><body>");
        
        sb.AppendLine($"<h1>Lägerbidragsansökan - Göteborg</h1>");
        sb.AppendLine($"<h2>{data.ScoutGroupName}</h2>");
        
        sb.AppendLine("<div class='summary'>");
        sb.AppendLine($"<p><strong>Lägerplats:</strong> {data.Site}</p>");
        sb.AppendLine($"<p><strong>Period:</strong> {data.DateFrom:yyyy-MM-dd} - {data.DateTo:yyyy-MM-dd}</p>");
        sb.AppendLine($"<p><strong>Kontaktperson:</strong> {data.Contact}</p>");
        sb.AppendLine($"<p><strong>E-post:</strong> {data.ContactEmail}</p>");
        sb.AppendLine($"<p><strong>Telefon:</strong> {data.ContactPhone}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<h3>Sammanfattning</h3>");
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><td>Antal deltagare 7-25 år</td><td>{data.YoungPersonsCount}</td></tr>");
        sb.AppendLine($"<tr><td>Antal deltagare över 25 år</td><td>{data.OlderPersonsCount}</td></tr>");
        sb.AppendLine($"<tr><td>Antal övernattningar 7-25 år</td><td>{data.NightsUpToMaxAge}</td></tr>");
        sb.AppendLine($"<tr><td>Antal övernattningar över 25 år</td><td>{data.NightsOverMaxAge}</td></tr>");
        sb.AppendLine($"<tr><td><strong>Totalt antal bidragsgrundande övernattningar</strong></td><td><strong>{data.TotalNights}</strong></td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h3>Deltagarförteckning</h3>");
        sb.AppendLine("<table><thead><tr><th>Nr</th><th>Namn</th><th>Födelseår</th><th>Ålder</th><th>Antal dagar</th></tr></thead><tbody>");
        
        var i = 1;
        foreach (var person in data.Persons)
        {
            sb.AppendLine($"<tr><td>{i++}</td><td>{person.Name}</td><td>{person.BirthYear}</td><td>{person.Age}</td><td>{person.Days}</td></tr>");
        }
        
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("</body></html>");
        
        return sb.ToString();
    }

    private static string GenerateStockholmHtml(LagerbidragData data)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html><head><meta charset='utf-8'><title>Lägerbidrag Stockholm</title>");
        sb.AppendLine("<style>body{font-family:Arial,sans-serif;margin:20px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:8px;text-align:left}th{background-color:#0066cc;color:white}.summary{margin:20px 0}h1,h2{color:#333}</style>");
        sb.AppendLine("</head><body>");
        
        sb.AppendLine($"<h1>Lägerbidragsansökan - Stockholm</h1>");
        sb.AppendLine($"<h2>{data.ScoutGroupName}</h2>");
        
        sb.AppendLine("<div class='summary'>");
        sb.AppendLine($"<p><strong>Organisationsnummer:</strong> {data.Organisationsnummer}</p>");
        sb.AppendLine($"<p><strong>Lägerplats:</strong> {data.Site}</p>");
        sb.AppendLine($"<p><strong>Period:</strong> {data.DateFrom:yyyy-MM-dd} - {data.DateTo:yyyy-MM-dd}</p>");
        sb.AppendLine($"<p><strong>Ledare:</strong> {data.Contact}</p>");
        sb.AppendLine($"<p><strong>E-post:</strong> {data.ContactEmail}</p>");
        sb.AppendLine($"<p><strong>Telefon:</strong> {data.ContactPhone}</p>");
        sb.AppendLine($"<p><strong>Läger under lov:</strong> {(data.HikeDuringBreak ? "Ja" : "Nej")}</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<h3>Sammanfattning</h3>");
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><td>Antal medlemmar (7-20 år)</td><td>{data.YoungPersonsCount}</td></tr>");
        var days = (data.DateTo.ToDateTime(TimeOnly.MinValue) - data.DateFrom.ToDateTime(TimeOnly.MinValue)).Days + 1;
        sb.AppendLine($"<tr><td>Antal dagar</td><td>{days}</td></tr>");
        sb.AppendLine($"<tr><td><strong>Summa bidragsgrundande dagar</strong></td><td><strong>{data.TotalDays}</strong></td></tr>");
        sb.AppendLine("</table>");

        sb.AppendLine("<h3>Deltagarförteckning</h3>");
        sb.AppendLine("<table><thead><tr><th>Nr</th><th>Namn</th><th>Postadress</th><th>Födelseår</th><th>Antal dagar</th></tr></thead><tbody>");
        
        var i = 1;
        foreach (var person in data.Persons.Take(data.YoungPersonsCount))
        {
            sb.AppendLine($"<tr><td>{i++}</td><td>{person.Name}</td><td>{person.PostalAddress}</td><td>{person.BirthYear}</td><td>{person.Days}</td></tr>");
        }
        
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("</body></html>");
        
        return sb.ToString();
    }
}
