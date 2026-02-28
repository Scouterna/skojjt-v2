using System.Globalization;
using System.Xml;
using Skojjt.Core.Exports;

namespace Skojjt.Infrastructure.Exports;

/// <summary>
/// Reads and parses DAK (Digitalt Aktivitetskort) XML files into <see cref="DakData"/>.
/// Provides detailed diagnostics with line numbers since Softadmin gives no error messages.
/// </summary>
public static class DakXmlReader
{
    /// <summary>
    /// Parse a DAK XML file from a stream.
    /// </summary>
    public static DakParseResult Parse(Stream stream, string fileName = "")
    {
        var issues = new List<DakParseIssue>();
        var pathStack = new Stack<string>();

        try
        {
            var settings = new XmlReaderSettings
            {
                IgnoreWhitespace = true,
                IgnoreComments = true,
            };

            using var reader = XmlReader.Create(stream, settings);
            var lineInfo = (IXmlLineInfo)reader;

            var dak = ReadAktivitetskort(reader, lineInfo, issues, pathStack);

            return new DakParseResult
            {
                Data = dak,
                Issues = issues.OrderBy(i => i.LineNumber).ThenBy(i => i.LinePosition).ToList(),
                FileName = fileName,
            };
        }
        catch (XmlException ex)
        {
            issues.Add(new DakParseIssue
            {
                Severity = DakIssueSeverity.Error,
                Message = $"XML-filen är ogiltig: {ex.Message}",
                LineNumber = ex.LineNumber,
                LinePosition = ex.LinePosition,
                XmlPath = GetCurrentPath(pathStack),
            });

            return new DakParseResult
            {
                Data = null,
                Issues = issues,
                FileName = fileName,
            };
        }
        catch (Exception ex)
        {
            issues.Add(new DakParseIssue
            {
                Severity = DakIssueSeverity.Error,
                Message = $"Oväntat fel vid inläsning: {ex.Message}",
                XmlPath = GetCurrentPath(pathStack),
            });

            return new DakParseResult
            {
                Data = null,
                Issues = issues,
                FileName = fileName,
            };
        }
    }

    /// <summary>
    /// Parse a DAK XML file from a byte array.
    /// </summary>
    public static DakParseResult Parse(byte[] data, string fileName = "")
    {
        using var stream = new MemoryStream(data);
        return Parse(stream, fileName);
    }

    /// <summary>
    /// Parse a DAK XML file from a string.
    /// </summary>
    public static DakParseResult ParseXml(string xml, string fileName = "")
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        return Parse(stream, fileName);
    }

    private static DakData ReadAktivitetskort(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack)
    {
        var dak = new DakData();

        reader.MoveToContent();
        var rootName = reader.LocalName;
        pathStack.Push(rootName);

        if (rootName != "Aktivitetskort")
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                $"Rotelement är '{rootName}', förväntade 'Aktivitetskort'.",
                actualValue: rootName, expectedValue: "Aktivitetskort"));
        }

        if (reader.IsEmptyElement)
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                "Aktivitetskort-elementet är tomt."));
            pathStack.Pop();
            return dak;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            switch (reader.LocalName)
            {
                case "Kommun":
                    ReadKommun(reader, lineInfo, issues, pathStack, dak);
                    break;
                case "DeltagarRegister":
                    ReadDeltagarRegister(reader, lineInfo, issues, pathStack, dak);
                    break;
                case "LedarRegister":
                    ReadLedarRegister(reader, lineInfo, issues, pathStack, dak);
                    break;
                default:
                    issues.Add(Issue(DakIssueSeverity.Info, lineInfo, pathStack,
                        $"Okänt element '{reader.LocalName}' ignoreras.", actualValue: reader.LocalName));
                    reader.Skip();
                    break;
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        pathStack.Pop();
        return dak;
    }

    private static void ReadKommun(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, DakData dak)
    {
        pathStack.Push("Kommun");

        dak.KommunId = ReadRequiredAttribute(reader, lineInfo, issues, pathStack, "kommunID");

        var version = reader.GetAttribute("version");
        if (version is not null && version != "2.2")
        {
            issues.Add(Issue(DakIssueSeverity.Warning, lineInfo, pathStack,
                $"DAK-version '{version}'  denna läsare är testad mot version 2.2.",
                actualValue: version, expectedValue: "2.2"));
        }

        if (reader.IsEmptyElement)
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                "Kommun-elementet är tomt, förväntade Foerening."));
            reader.Read();
            pathStack.Pop();
            return;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            if (reader.LocalName == "Foerening")
            {
                ReadForening(reader, lineInfo, issues, pathStack, dak);
            }
            else
            {
                issues.Add(Issue(DakIssueSeverity.Info, lineInfo, pathStack,
                    $"Okänt element '{reader.LocalName}' i Kommun ignoreras.", actualValue: reader.LocalName));
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        pathStack.Pop();
    }

    private static void ReadForening(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, DakData dak)
    {
        pathStack.Push("Foerening");

        dak.ForeningsId = ReadRequiredAttribute(reader, lineInfo, issues, pathStack, "foereningsID");
        dak.ForeningsNamn = reader.GetAttribute("foereningsNamn") ?? string.Empty;
        dak.Organisationsnummer = reader.GetAttribute("organisationsnummer") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(dak.ForeningsNamn))
        {
            issues.Add(Issue(DakIssueSeverity.Warning, lineInfo, pathStack,
                "Attribut 'foereningsNamn' saknas eller är tomt."));
        }

        if (reader.IsEmptyElement)
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                "Foerening-elementet är tomt, förväntade Naervarokort."));
            reader.Read();
            pathStack.Pop();
            return;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            if (reader.LocalName == "Naervarokort")
            {
                ReadNarvarokort(reader, lineInfo, issues, pathStack, dak);
            }
            else
            {
                issues.Add(Issue(DakIssueSeverity.Info, lineInfo, pathStack,
                    $"Okänt element '{reader.LocalName}' i Foerening ignoreras.", actualValue: reader.LocalName));
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        pathStack.Pop();
    }

    private static void ReadNarvarokort(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, DakData dak)
    {
        pathStack.Push("Naervarokort");

        var narvarokortNummer = ReadRequiredAttribute(reader, lineInfo, issues, pathStack, "NaervarokortNummer");
        dak.Kort.NarvarokortNummer = narvarokortNummer;

        if (!string.IsNullOrEmpty(narvarokortNummer) && !int.TryParse(narvarokortNummer, out _))
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                $"NaervarokortNummer '{narvarokortNummer}' är inte ett giltigt heltal (int32). Softadmin kräver ett heltalsvärde.",
                actualValue: narvarokortNummer, expectedValue: "heltal (max 2 147 483 647)"));
        }

        if (reader.IsEmptyElement)
        {
            reader.Read();
            pathStack.Pop();
            return;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            switch (reader.LocalName)
            {
                case "Aktivitet":
                    dak.Kort.Aktivitet = reader.ReadElementContentAsString();
                    break;
                case "Lokal":
                    dak.Kort.Lokal = reader.ReadElementContentAsString();
                    break;
                case "NamnPaaKort":
                    dak.Kort.NamnPaKort = reader.ReadElementContentAsString();
                    break;
                case "Sammankomster":
                    ReadSammankomster(reader, lineInfo, issues, pathStack, dak);
                    break;
                default:
                    issues.Add(Issue(DakIssueSeverity.Info, lineInfo, pathStack,
                        $"Okänt element '{reader.LocalName}' i Naervarokort ignoreras.", actualValue: reader.LocalName));
                    reader.Skip();
                    break;
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        pathStack.Pop();
    }

    private static void ReadSammankomster(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, DakData dak)
    {
        pathStack.Push("Sammankomster");

        if (reader.IsEmptyElement)
        {
            issues.Add(Issue(DakIssueSeverity.Warning, lineInfo, pathStack,
                "Inga sammankomster i närvarokortet."));
            reader.Read();
            pathStack.Pop();
            return;
        }

        var seenKoder = new HashSet<string>(StringComparer.Ordinal);
        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            if (reader.LocalName == "Sammankomst")
            {
                var sammankomst = ReadSammankomst(reader, lineInfo, issues, pathStack, seenKoder);
                if (sammankomst is not null)
                    dak.Kort.Sammankomster.Add(sammankomst);
            }
            else
            {
                issues.Add(Issue(DakIssueSeverity.Info, lineInfo, pathStack,
                    $"Okänt element '{reader.LocalName}' i Sammankomster ignoreras.", actualValue: reader.LocalName));
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        pathStack.Pop();
    }

    private static DakSammankomst? ReadSammankomst(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, HashSet<string> seenKoder)
    {
        var kod = reader.GetAttribute("kod") ?? string.Empty;
        var datumStr = reader.GetAttribute("Datum") ?? string.Empty;
        var pathLabel = $"Sammankomst[@kod='{kod}', Datum='{datumStr}']";
        pathStack.Push(pathLabel);

        if (string.IsNullOrWhiteSpace(kod))
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                "Attribut 'kod' saknas på Sammankomst."));
        }
        else
        {
            if (kod.Length < 3)
            {
                issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                    $"Kod '{kod}' är för kort (minst 3 tecken krävs).",
                    actualValue: kod, expectedValue: "minst 3 tecken"));
            }

            if (!int.TryParse(kod, out _))
            {
                issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                    $"Kod '{kod}' är inte ett giltigt heltal (int32). Softadmin kräver att kod är ett heltal.",
                    actualValue: kod, expectedValue: "heltal (max 2 147 483 647)"));
            }

            if (!seenKoder.Add(kod))
            {
                issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                    $"Duplicerad sammankomstkod '{kod}'. Kod måste vara unik inom närvarokortet.",
                    actualValue: kod));
            }
        }

        DateTime datum = default;
        if (string.IsNullOrWhiteSpace(datumStr))
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                "Attribut 'Datum' saknas på Sammankomst."));
        }
        else if (!DateTime.TryParseExact(datumStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out datum))
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                $"Ogiltigt datumformat '{datumStr}'.",
                actualValue: datumStr, expectedValue: "yyyy-MM-dd"));
        }

        var sammankomst = new DakSammankomst(kod, datum, 90, "Aktivitet");
        DateTime? stoppDatum = null;
        TimeSpan? parsedStoppTid = null;

        if (reader.IsEmptyElement)
        {
            issues.Add(Issue(DakIssueSeverity.Warning, lineInfo, pathStack,
                "Sammankomst-elementet är tomt (saknar tider, deltagare)."));
            reader.Read();
            pathStack.Pop();
            return sammankomst;
        }

        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            switch (reader.LocalName)
            {
                case "StartTid":
                    var startTidStr = reader.ReadElementContentAsString();
                    if (TimeSpan.TryParseExact(startTidStr, ["hh\\:mm\\:ss", "hh\\:mm"], CultureInfo.InvariantCulture, out var startTid)
                       || TimeSpan.TryParse(startTidStr, CultureInfo.InvariantCulture, out startTid))
                    {
                        sammankomst.Datum = datum.Date + startTid;
                    }
                    else
                    {
                        issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                            $"Ogiltig StartTid '{startTidStr}'.",
                            actualValue: startTidStr, expectedValue: "HH:mm:ss"));
                    }
                    break;

                case "StoppTid":
                    var stoppTidStr = reader.ReadElementContentAsString();
                    if (TimeSpan.TryParseExact(stoppTidStr, ["hh\\:mm\\:ss", "hh\\:mm"], CultureInfo.InvariantCulture, out var stoppTid)
                        || TimeSpan.TryParse(stoppTidStr, CultureInfo.InvariantCulture, out stoppTid))
                    {
                        parsedStoppTid = stoppTid;
                    }
                    else
                    {
                        issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                            $"Ogiltig StoppTid '{stoppTidStr}'.",
                            actualValue: stoppTidStr, expectedValue: "HH:mm:ss"));
                    }
                    break;

                case "StoppDatum":
                    var stoppDatumStr = reader.ReadElementContentAsString();
                    if (DateTime.TryParseExact(stoppDatumStr, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedStoppDatum))
                    {
                        stoppDatum = parsedStoppDatum;
                    }
                    else
                    {
                        issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                            $"Ogiltigt StoppDatum '{stoppDatumStr}'.",
                            actualValue: stoppDatumStr, expectedValue: "yyyy-MM-dd"));
                    }
                    break;

                case "Aktivitet":
                    sammankomst.Aktivitet = reader.ReadElementContentAsString();
                    break;

                case "Lokal":
                    reader.ReadElementContentAsString();
                    break;

                case "Typ":
                    var typ = reader.ReadElementContentAsString();
                    string[] validTyper = ["Traening", "Match", "Moete", "Oevrigt"];
                    if (!validTyper.Contains(typ, StringComparer.Ordinal))
                    {
                        issues.Add(Issue(DakIssueSeverity.Warning, lineInfo, pathStack,
                            $"Okänd sammankomsttyp '{typ}'.",
                            actualValue: typ, expectedValue: string.Join(", ", validTyper)));
                    }
                    sammankomst.Typ = typ;
                    break;

                case "Metod":
                    reader.ReadElementContentAsString();
                    break;

                case "DeltagarLista":
                    ReadPersonLista(reader, lineInfo, issues, pathStack, "DeltagarLista", "Deltagare", sammankomst.Deltagare);
                    break;

                case "LedarLista":
                    ReadPersonLista(reader, lineInfo, issues, pathStack, "LedarLista", "Ledare", sammankomst.Ledare);
                    break;

                default:
                    issues.Add(Issue(DakIssueSeverity.Info, lineInfo, pathStack,
                        $"Okänt element '{reader.LocalName}' i Sammankomst ignoreras.", actualValue: reader.LocalName));
                    reader.Skip();
                    break;
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        // Calculate duration from StartTid, StoppTid, and optional StoppDatum
        if (parsedStoppTid.HasValue)
        {
            var startTime = sammankomst.Datum; // Date + StartTid
            var endDate = stoppDatum ?? sammankomst.Datum.Date;
            var endTime = endDate + parsedStoppTid.Value;

            if (endTime > startTime)
            {
                sammankomst.DurationMinutes = (int)(endTime - startTime).TotalMinutes;
            }
            else if (endTime == startTime)
            {
                issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                    "StoppTid är samma som StartTid. Sammankomsten har 0 minuters varaktighet."));
                sammankomst.DurationMinutes = 0;
            }
            else
            {
                issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                    $"StoppTid ({parsedStoppTid.Value:hh\\:mm\\:ss}) är före StartTid ({sammankomst.Datum:HH:mm:ss})"
                    + (stoppDatum.HasValue ? $" även med StoppDatum ({stoppDatum.Value:yyyy-MM-dd})." : ". Ange StoppDatum om sammankomsten sträcker sig över midnatt."),
                    actualValue: $"StartTid: {sammankomst.Datum:HH:mm:ss}, StoppTid: {parsedStoppTid.Value:hh\\:mm\\:ss}"));
                sammankomst.DurationMinutes = 0;
            }
        }
        pathStack.Pop();
        return sammankomst;
    }

    private static void ReadPersonLista(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, string listaName, string personElement, List<DakDeltagare> target)
    {
        pathStack.Push(listaName);

        if (reader.IsEmptyElement)
        {
            reader.Read();
            pathStack.Pop();
            return;
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            if (reader.LocalName == personElement)
            {
                var id = reader.GetAttribute("id") ?? string.Empty;
                pathStack.Push($"{personElement}[@id='{id}']");

                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                        $"Attribut 'id' saknas på {personElement} i {listaName}."));
                }
                else if (!seenIds.Add(id))
                {
                    issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                        $"Duplicerat id '{id}' i {listaName}. Samma person kan inte förekomma två gånger i samma sammankomst.",
                        actualValue: id));
                }

                var deltagare = new DakDeltagare { Uid = id, Ledare = personElement == "Ledare" };

                if (!reader.IsEmptyElement)
                {
                    reader.ReadStartElement();
                    while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
                    {
                        if (reader.NodeType == XmlNodeType.Element)
                            reader.Skip();
                        else
                            reader.Read();
                    }
                    if (reader.NodeType == XmlNodeType.EndElement)
                        reader.ReadEndElement();
                }
                else
                {
                    reader.Read();
                }

                target.Add(deltagare);
                pathStack.Pop();
            }
            else
            {
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        pathStack.Pop();
    }
    private static void ReadDeltagarRegister(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, DakData dak)
    {
        pathStack.Push("DeltagarRegister");

        if (reader.IsEmptyElement)
        {
            reader.Read();
            pathStack.Pop();
            return;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            if (reader.LocalName == "Deltagare")
            {
                var deltagare = ReadRegisterPerson(reader, lineInfo, issues, pathStack, "Deltagare", isLeader: false);
                if (deltagare is not null)
                    MergeRegisterPerson(dak.Kort.Deltagare, deltagare);
            }
            else
            {
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        pathStack.Pop();
    }

    private static void ReadLedarRegister(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, DakData dak)
    {
        pathStack.Push("LedarRegister");

        if (reader.IsEmptyElement)
        {
            reader.Read();
            pathStack.Pop();
            return;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            if (reader.LocalName == "Ledare")
            {
                var ledare = ReadRegisterPerson(reader, lineInfo, issues, pathStack, "Ledare", isLeader: true);
                if (ledare is not null)
                    MergeRegisterPerson(dak.Kort.Ledare, ledare);
            }
            else
            {
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        pathStack.Pop();
    }

    private static DakDeltagare ReadRegisterPerson(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, string elementName, bool isLeader)
    {
        var id = reader.GetAttribute("id") ?? string.Empty;
        pathStack.Push($"{elementName}[@id='{id}']");

        if (string.IsNullOrWhiteSpace(id))
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                $"Attribut 'id' saknas på {elementName} i registret."));
        }
        else if (id.Length < 2)
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                $"Id '{id}' är för kort (minst 2 tecken krävs).",
                actualValue: id, expectedValue: "minst 2 tecken"));
        }

        var person = new DakDeltagare { Uid = id, Ledare = isLeader };

        if (reader.IsEmptyElement)
        {
            issues.Add(Issue(DakIssueSeverity.Warning, lineInfo, pathStack,
                $"{elementName} med id '{id}' har inga underliggande element (namn, personnummer saknas)."));
            reader.Read();
            pathStack.Pop();
            return person;
        }

        reader.ReadStartElement();

        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType != XmlNodeType.Element) { reader.Read(); continue; }

            switch (reader.LocalName)
            {
                case "Foernamn":
                    person.Fornamn = reader.ReadElementContentAsString();
                    break;
                case "Efternamn":
                    person.Efternamn = reader.ReadElementContentAsString();
                    break;
                case "Personnummer":
                    var pnr = reader.ReadElementContentAsString();
                    person.Personnummer = pnr;
                    if (!string.IsNullOrEmpty(pnr) && pnr.Length != 12)
                    {
                        issues.Add(Issue(DakIssueSeverity.Warning, lineInfo, pathStack,
                            $"Personnummer '{MaskPersonnummer(pnr)}' har oväntad längd ({pnr.Length} tecken, förväntade 12).",
                            actualValue: $"{pnr.Length} tecken", expectedValue: "12 tecken (YYYYMMDDNNNN)"));
                    }
                    break;
                case "Epost":
                    person.Epost = reader.ReadElementContentAsString();
                    break;
                case "MobilNr":
                    person.MobilNr = reader.ReadElementContentAsString();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
            reader.ReadEndElement();

        pathStack.Pop();
        return person;
    }

    /// <summary>
    /// Merge register person data into existing list (enriches stubs from sammankomst references).
    /// </summary>
    private static void MergeRegisterPerson(List<DakDeltagare> existing, DakDeltagare person)
    {
        var match = existing.FirstOrDefault(d => d.Uid == person.Uid);
        if (match is not null)
        {
            if (!string.IsNullOrEmpty(person.Fornamn)) match.Fornamn = person.Fornamn;
            if (!string.IsNullOrEmpty(person.Efternamn)) match.Efternamn = person.Efternamn;
            if (!string.IsNullOrEmpty(person.Personnummer)) match.Personnummer = person.Personnummer;
            if (!string.IsNullOrEmpty(person.Epost)) match.Epost = person.Epost;
            if (!string.IsNullOrEmpty(person.MobilNr)) match.MobilNr = person.MobilNr;
        }
        else
        {
            existing.Add(person);
        }
    }

    private static string ReadRequiredAttribute(XmlReader reader, IXmlLineInfo lineInfo, List<DakParseIssue> issues, Stack<string> pathStack, string attributeName)
    {
        var value = reader.GetAttribute(attributeName);
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Issue(DakIssueSeverity.Error, lineInfo, pathStack,
                $"Obligatoriskt attribut '{attributeName}' saknas eller är tomt."));
            return string.Empty;
        }
        return value;
    }

    private static DakParseIssue Issue(DakIssueSeverity severity, IXmlLineInfo lineInfo, Stack<string> pathStack,
        string message, string? actualValue = null, string? expectedValue = null)
    {
        return new DakParseIssue
        {
            Severity = severity,
            Message = message,
            LineNumber = lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0,
            LinePosition = lineInfo.HasLineInfo() ? lineInfo.LinePosition : 0,
            XmlPath = GetCurrentPath(pathStack),
            ActualValue = actualValue,
            ExpectedValue = expectedValue,
        };
    }

    private static string GetCurrentPath(Stack<string> pathStack) =>
        string.Join("/", pathStack.Reverse());

    /// <summary>
    /// Mask personnummer for display in error messages (GDPR).
    /// Shows first 8 digits (birthdate) and masks the last 4.
    /// </summary>
    private static string MaskPersonnummer(string personnummer)
    {
        if (personnummer.Length <= 8)
            return personnummer;
        return personnummer[..8] + "****";
    }
}

