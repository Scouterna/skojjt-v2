using Skojjt.Core.Data;
using Skojjt.Core.Exports;
using System.Text;
using System.Xml;

namespace Skojjt.Infrastructure.Exports;

/// <summary>
/// Exports attendance data to DAK (Digitalt Aktivitetskort) XML format.
/// See: http://www.sverigesforeningssystem.se/dak-formatet/vad-ar-dak/
/// </summary>
public class DakXmlExporter : IAttendanceExporter
{
    public string ExporterId => "dak";
    public string DisplayName => "DAK XML";

    public Task<ExportResult> ExportAsync(AttendanceReportData data, CancellationToken cancellationToken = default)
    {
        // Validate required fields for DAK export
        if (string.IsNullOrWhiteSpace(data.ScoutGroup.AssociationId))
        {
            throw new InvalidOperationException(
                $"Förenings-ID saknas för scoutkåren '{data.ScoutGroup.Name}'. " +
                "Konfigurera förenings-ID i kårinställningar innan du exporterar DAK.");
        }

        if (string.IsNullOrWhiteSpace(data.ScoutGroup.MunicipalityId))
        {
            throw new InvalidOperationException(
                $"Kommun-ID saknas för scoutkåren '{data.ScoutGroup.Name}'. " +
                "Konfigurera kommun-ID i kårinställningar innan du exporterar DAK.");
        }

        // Validate that the municipality code is valid
        if (!MunicipalityCodes.IsValidCode(data.ScoutGroup.MunicipalityId))
        {
            throw new InvalidOperationException(
                $"Ogiltigt kommun-ID '{data.ScoutGroup.MunicipalityId}' för scoutkåren '{data.ScoutGroup.Name}'. " +
                "Kontrollera kommun-ID i kårinställningar.");
        }

        var dakData = BuildDakData(data);
        var xml = GenerateXml(dakData);
        var bytes = Encoding.UTF8.GetBytes(xml);
        var fileName = $"{dakData.Kort.NamnPaKort}-{data.Semester.DisplayName}.xml";

        return Task.FromResult(new ExportResult(bytes, fileName, "application/xml"));
    }

    private static DakData BuildDakData(AttendanceReportData data)
    {
        var dak = new DakData
        {
            ForeningsNamn = data.ScoutGroup.Name,
            ForeningsId = data.ScoutGroup.AssociationId ?? string.Empty,
            Organisationsnummer = data.ScoutGroup.OrganisationNumber ?? string.Empty,
            KommunId = data.ScoutGroup.MunicipalityId!, // This reference is validated earlier
			Kort =
            {
                NamnPaKort = data.Troop.Name,
                NarvarokortNummer = GetNarvarokortNummer(data.Troop, data.Semester).ToString(),
                Lokal = data.DefaultLocation
            }
        };

        // Build person lookup
        var personsDict = data.TroopPersons.ToDictionary(tp => tp.Person.Id);

        // Add leaders and participants to the card
        foreach (var tp in data.TroopPersons)
        {
            var deltagare = CreateDakDeltagare(tp.Person, tp.IsLeader);
            if (tp.IsLeader)
                dak.Kort.Ledare.Add(deltagare);
            else
                dak.Kort.Deltagare.Add(deltagare);
        }

        // Add meetings
        foreach (var meetingInfo in data.Meetings)
        {
            var meeting = meetingInfo.Meeting;
            
            // Skip hike meetings if not included
            if (!data.IncludeHikeMeetings && meeting.IsHike)
                continue;

            var sammankomst = new DakSammankomst(
                GetMeetingCode(meeting, data.Troop),
                meeting.MeetingDate.ToDateTime(meeting.StartTime),
                meeting.DurationMinutes,
                meeting.Name);

            // Add attending persons to meeting
            foreach (var personId in meetingInfo.AttendingPersonIds)
            {
                if (!personsDict.TryGetValue(personId, out var tp))
                    continue;

                var deltagare = CreateDakDeltagare(tp.Person, tp.IsLeader);
                if (tp.IsLeader)
                    sammankomst.Ledare.Add(deltagare);
                else
                    sammankomst.Deltagare.Add(deltagare);
            }

            dak.Kort.Sammankomster.Add(sammankomst);
        }

        return dak;
    }

    private static DakDeltagare CreateDakDeltagare(Core.Entities.Person person, bool isLeader)
    {
        return new DakDeltagare(
            FormatPersonId(person.Id),
            person.FirstName,
            person.LastName,
            person.PersonalNumber?.ToString() ?? string.Empty,
            isLeader,
            person.Email ?? string.Empty,
            person.Mobile ?? string.Empty,
            person.ZipCode ?? string.Empty);
    }

    /// <summary>
    /// Format person ID to meet DAK schema requirements (minLength=2, maxLength=50).
    /// Pads short IDs with leading zeros to ensure minimum length.
    /// </summary>
    private static string FormatPersonId(int personId)
    {
        // DAK schema requires id to be at least 2 characters
        // Use zero-padded format to ensure minimum length
        return personId.ToString("D2");
    }

    /// <summary>
    /// Generate NarvarokortNummer as a unique integer.
    /// The DAK schema requires this to be xs:int (Int32).
    /// Uses a combination of troop ScoutnetId and semester year to create uniqueness.
    /// </summary>
    private static int GetNarvarokortNummer(Core.Entities.Troop troop, Core.Entities.Semester semester)
    {
        return troop.ScoutnetId * 1000 + semester.Id;
    }

    /// <summary>
    /// Generate a meeting code that meets DAK schema requirements.
    /// The schema requires kod to be a string with minLength=3 and maxLength=50.
    /// </summary>
    private static string GetMeetingCode(Core.Entities.Meeting meeting, Core.Entities.Troop troop)
    {
		//
		// get_short_key returns a unique string for this meeting.
		//
		// It does not have to be an signed int as before (bug fixed in Gothenburg kommun)
		// It is a string with the max length of 50 chars(see DAK 2.2 specification) and should be unique for each meeting.
		//
		// It should also be deterministic, so the same meeting will always get the same short key, even if the meeting is deleted and recreated.
		var toopid = troop.ScoutnetId;
		var semesterid = troop.SemesterId;
		var datestr = meeting.MeetingDate.ToString("MMdd");
		return $"{toopid}-{semesterid}-{datestr}";
    }

    private static string GenerateXml(DakData dak)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using var stringWriter = new StringWriterWithEncoding(Encoding.UTF8);
        using var writer = XmlWriter.Create(stringWriter, settings);

        writer.WriteStartDocument();
        
        // Root element with namespaces
        writer.WriteStartElement("Aktivitetskort", "http://sverigesforeningssystem.se/importSchema.xsd");
        writer.WriteAttributeString("xmlns", "xsd", null, "http://www.w3.org/2001/XMLSchema");
        writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");

        // Kommun
        writer.WriteStartElement("Kommun");
        writer.WriteAttributeString("kommunID", dak.KommunId);
        writer.WriteAttributeString("version", "2.2");

        // Förening
        writer.WriteStartElement("Foerening");
        writer.WriteAttributeString("foereningsID", dak.ForeningsId);
        writer.WriteAttributeString("foereningsNamn", dak.ForeningsNamn);
        writer.WriteAttributeString("organisationsnummer", dak.Organisationsnummer);

        // Närvarokort
        writer.WriteStartElement("Naervarokort");
        writer.WriteAttributeString("NaervarokortNummer", dak.Kort.NarvarokortNummer);
        
        writer.WriteElementString("Aktivitet", dak.Kort.Aktivitet);
        writer.WriteElementString("Lokal", dak.Kort.Lokal);
        writer.WriteElementString("NamnPaaKort", dak.Kort.NamnPaKort);

        // Sammankomster
        writer.WriteStartElement("Sammankomster");
        foreach (var sammankomst in dak.Kort.Sammankomster)
        {
            WriteSammankomst(writer, sammankomst, dak.Kort.Lokal);
        }
        writer.WriteEndElement(); // Sammankomster

        writer.WriteEndElement(); // Naervarokort
        writer.WriteEndElement(); // Foerening
        writer.WriteEndElement(); // Kommun

        // DeltagarRegister
        writer.WriteStartElement("DeltagarRegister");
        foreach (var deltagare in dak.Kort.Deltagare)
        {
            WriteDeltagare(writer, deltagare, "Deltagare");
        }
        writer.WriteEndElement(); // DeltagarRegister

        // LedarRegister
        writer.WriteStartElement("LedarRegister");
        foreach (var ledare in dak.Kort.Ledare)
        {
            WriteLedare(writer, ledare);
        }
        writer.WriteEndElement(); // LedarRegister

        writer.WriteEndElement(); // Aktivitetskort
        writer.WriteEndDocument();

        writer.Flush();
        return stringWriter.ToString();
    }

    private static void WriteSammankomst(XmlWriter writer, DakSammankomst sammankomst, string lokal)
    {
        writer.WriteStartElement("Sammankomst");
        writer.WriteAttributeString("Datum", sammankomst.GetDateString());
        writer.WriteAttributeString("kod", sammankomst.Kod);

        writer.WriteElementString("StartTid", sammankomst.GetStartTimeString());
        writer.WriteElementString("StoppTid", sammankomst.GetStopTimeString());
        writer.WriteElementString("Aktivitet", sammankomst.Aktivitet);
        writer.WriteElementString("Lokal", lokal);
        writer.WriteElementString("Typ", sammankomst.Typ);
        writer.WriteElementString("Metod", "Add");

        // DeltagarLista
        writer.WriteStartElement("DeltagarLista");
        foreach (var deltagare in sammankomst.Deltagare)
        {
            writer.WriteStartElement("Deltagare");
            writer.WriteAttributeString("id", deltagare.Uid);
            writer.WriteElementString("Handikapp", "false");
            writer.WriteElementString("Naervarande", "true");
            writer.WriteEndElement();
        }
        writer.WriteEndElement(); // DeltagarLista

        // LedarLista
        writer.WriteStartElement("LedarLista");
        foreach (var ledare in sammankomst.Ledare)
        {
            writer.WriteStartElement("Ledare");
            writer.WriteAttributeString("id", ledare.Uid);
            writer.WriteElementString("Handikapp", "false");
            writer.WriteElementString("Naervarande", "true");
            writer.WriteEndElement();
        }
        writer.WriteEndElement(); // LedarLista

        writer.WriteEndElement(); // Sammankomst
    }

    private static void WriteDeltagare(XmlWriter writer, DakDeltagare deltagare, string elementName)
    {
        writer.WriteStartElement(elementName);
        writer.WriteAttributeString("id", deltagare.Uid);
        writer.WriteElementString("Foernamn", deltagare.Fornamn);
        writer.WriteElementString("Efternamn", deltagare.Efternamn);
        writer.WriteElementString("Personnummer", deltagare.Personnummer);
        writer.WriteEndElement();
    }

    private static void WriteLedare(XmlWriter writer, DakDeltagare ledare)
    {
        writer.WriteStartElement("Ledare");
        writer.WriteAttributeString("id", ledare.Uid);
        writer.WriteElementString("Foernamn", ledare.Fornamn);
        writer.WriteElementString("Efternamn", ledare.Efternamn);
        writer.WriteElementString("Personnummer", ledare.Personnummer);
        writer.WriteElementString("Epost", ledare.Epost);
        writer.WriteElementString("MobilNr", ledare.MobilNr);
        writer.WriteEndElement();
    }

    /// <summary>
    /// StringWriter that supports specifying encoding (default is UTF-16).
    /// </summary>
    private sealed class StringWriterWithEncoding : StringWriter
    {
        private readonly Encoding _encoding;

        public StringWriterWithEncoding(Encoding encoding)
        {
            _encoding = encoding;
        }

        public override Encoding Encoding => _encoding;
    }
}
