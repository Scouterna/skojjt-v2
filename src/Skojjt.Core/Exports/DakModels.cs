using System.Globalization;

namespace Skojjt.Core.Exports;

/// <summary>
/// DAK (Digitalt Aktivitetskort) data model.
/// Used for exporting attendance data to the Swedish DAK format.
/// See: http://www.sverigesforeningssystem.se/dak-formatet/vad-ar-dak/
/// </summary>
public class DakData
{
    /// <summary>
    /// The attendance card (närvarokort).
    /// </summary>
    public DakNarvarokort Kort { get; set; } = new();

    /// <summary>
    /// Municipality ID (kommun-ID).
    /// </summary>
    public string KommunId { get; set; } = string.Empty;

    /// <summary>
    /// Association ID (förenings-ID).
    /// </summary>
    public string ForeningsId { get; set; } = string.Empty;

    /// <summary>
    /// Association name (föreningsnamn).
    /// </summary>
    public string ForeningsNamn { get; set; } = string.Empty;

    /// <summary>
    /// Organization number (organisationsnummer).
    /// </summary>
    public string Organisationsnummer { get; set; } = string.Empty;
}

/// <summary>
/// Attendance card (närvarokort) in DAK format.
/// </summary>
public class DakNarvarokort
{
    /// <summary>
    /// Participants (deltagare) - non-leaders.
    /// </summary>
    public List<DakDeltagare> Deltagare { get; set; } = [];

    /// <summary>
    /// Leaders (ledare).
    /// </summary>
    public List<DakDeltagare> Ledare { get; set; } = [];

    /// <summary>
    /// Meetings (sammankomster).
    /// </summary>
    public List<DakSammankomst> Sammankomster { get; set; } = [];

    /// <summary>
    /// Attendance card number (närvarokort-nummer).
    /// </summary>
    public string NarvarokortNummer { get; set; } = string.Empty;

    /// <summary>
    /// Location (lokal).
    /// </summary>
    public string Lokal { get; set; } = "Scouthuset"; // TODO: Meeting.Location

	/// <summary>
	/// Name on card (namn på kort) - usually troop name.
	/// </summary>
	public string NamnPaKort { get; set; } = string.Empty;

    /// <summary>
    /// Main activity for the attendance card.
    /// </summary>
    public string Aktivitet { get; set; } = "Scouting";
}

/// <summary>
/// Participant (deltagare) in DAK format.
/// </summary>
public class DakDeltagare
{
    public DakDeltagare() { }

    public DakDeltagare(string uid, string fornamn, string efternamn, string personnummer, 
        bool ledare, string epost = "", string mobilNr = "", string postnummer = "")
    {
        Uid = uid;
        Fornamn = fornamn;
        Efternamn = efternamn;
        Personnummer = personnummer;
        Ledare = ledare;
        Epost = epost;
        MobilNr = mobilNr;
        Postnummer = postnummer;
    }

    /// <summary>
    /// Unique ID (member number).
    /// </summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>
    /// First name (förnamn).
    /// </summary>
    public string Fornamn { get; set; } = string.Empty;

    /// <summary>
    /// Last name (efternamn).
    /// </summary>
    public string Efternamn { get; set; } = string.Empty;

    /// <summary>
    /// Personal number (personnummer) in format YYYYMMDDNNNN.
    /// </summary>
    public string Personnummer { get; set; } = string.Empty;

    /// <summary>
    /// Whether this person is a leader.
    /// </summary>
    public bool Ledare { get; set; }

    /// <summary>
    /// Email address.
    /// </summary>
    public string Epost { get; set; } = string.Empty;

    /// <summary>
    /// Mobile phone number.
    /// </summary>
    public string MobilNr { get; set; } = string.Empty;

    /// <summary>
    /// Postal code (postnummer).
    /// </summary>
    public string Postnummer { get; set; } = string.Empty;

    /// <summary>
    /// Returns true if the person is female based on personnummer.
    /// The second to last digit is odd for males, even for females.
    /// </summary>
    public bool IsFemale()
    {
        if (string.IsNullOrEmpty(Personnummer) || Personnummer.Length < 11)
            return false;
        
        return int.TryParse(Personnummer[^2].ToString(), out var digit) && (digit & 1) == 0;
    }

    /// <summary>
    /// Calculate age for a given semester year.
    /// </summary>
    public int AgeThisSemester(int semesterYear)
    {
        if (string.IsNullOrEmpty(Personnummer) || Personnummer.Length < 4)
            return 0;
        
        if (int.TryParse(Personnummer[..4], out var birthYear))
            return semesterYear - birthYear;
        
        return 0;
    }
}

/// <summary>
/// Meeting (sammankomst) in DAK format.
/// </summary>
public class DakSammankomst
{
    public DakSammankomst(string kod, DateTime datum, int durationMinutes, string aktivitet)
    {
		if (kod.Length > 50)
			throw new ArgumentException("Kod cannot be longer than 50 characters.", nameof(kod));

		Kod = kod;
        Datum = datum;
        DurationMinutes = durationMinutes;
        Aktivitet = aktivitet;
    }

    /// <summary>
    /// Meeting code/ID.
    /// </summary>
    public string Kod { get; private set; } = string.Empty;

    /// <summary>
    /// Meeting date and start time.
    /// </summary>
    public DateTime Datum { get; set; }

    /// <summary>
    /// Duration in minutes.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    /// Activity name.
    /// </summary>
    public string Aktivitet { get; set; } = string.Empty;

    /// <summary>
    /// Meeting type. One of: Traening, Match, Moete, Oevrigt
    /// </summary>
    public string Typ { get; set; } = "Moete";

    /// <summary>
    /// Attending participants (non-leaders).
    /// </summary>
    public List<DakDeltagare> Deltagare { get; set; } = [];

    /// <summary>
    /// Attending leaders.
    /// </summary>
    public List<DakDeltagare> Ledare { get; set; } = [];

    /// <summary>
    /// Get date string in specified format.
    /// </summary>
    public string GetDateString(string format = "yyyy-MM-dd") => Datum.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>
    /// Get start time string in specified format.
    /// </summary>
    public string GetStartTimeString(string format = "HH:mm:ss") => Datum.ToString(format, CultureInfo.InvariantCulture);

    /// <summary>
    /// Get stop time string in specified format.
    /// The end time is limited to 23:59:59 of the same day.
    /// </summary>
    public string GetStopTimeString(string format = "HH:mm:ss")
    {
        var maxEndTime = Datum.Date.AddDays(1).AddSeconds(-1);
        var endTime = Datum.AddMinutes(DurationMinutes);
        
        if (endTime > maxEndTime)
            endTime = maxEndTime;
        
        return endTime.ToString(format, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Get all persons attending this meeting.
    /// </summary>
    public IEnumerable<DakDeltagare> GetAllPersons() => Ledare.Concat(Deltagare);
}
