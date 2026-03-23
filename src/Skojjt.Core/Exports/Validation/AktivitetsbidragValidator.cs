namespace Skojjt.Core.Exports.Validation;

/// <summary>
/// Validates a <see cref="DakData"/> against <see cref="AktivitetsbidragSettings"/>
/// and calculates the expected grant amount.
/// Generic engine  all municipality/year-specific values come from settings.
/// </summary>
public static class AktivitetsbidragValidator
{
    /// <summary>
    /// Validate and calculate aktivitetsbidrag for the given data.
    /// </summary>
    /// <param name="dak">The DAK data to validate.</param>
    /// <param name="settings">Municipality/year-specific settings.</param>
    /// <param name="terminsAr">The semester year, used for age calculations.</param>
    public static AktivitetsbidragResult Validate(DakData dak, AktivitetsbidragSettings settings, int terminsAr)
    {
        var issues = new List<DakParseIssue>();

        // Build lookup from register for personnummer-based checks
        var allDeltagare = BuildPersonLookup(dak);

        // Validate kommun
        if (settings.KommunId is not null && dak.KommunId != settings.KommunId)
        {
            issues.Add(new DakParseIssue
            {
                Severity = DakIssueSeverity.Error,
                Message = $"KommunId '{dak.KommunId}' matchar inte inställningen '{settings.KommunId}' ({settings.Namn}).",
                XmlPath = "Kommun",
                ActualValue = dak.KommunId,
                ExpectedValue = settings.KommunId,
            });
        }

        // Validate and calculate per sammankomst
        var sammankomstBerakningar = new List<SammankomstBidrag>();

        foreach (var sammankomst in dak.Kort.Sammankomster)
        {
            var bidrag = ValidateAndCalculateSammankomst(sammankomst, settings, terminsAr, allDeltagare, issues);
            sammankomstBerakningar.Add(bidrag);
        }

        // Check for duplicate UIDs across sammankomster
        ValidateSammankomstKoder(dak, issues);

        var summering = new BidragsSummering
        {
            SammankomstBerakningar = sammankomstBerakningar,
            Settings = settings,
            TerminsAr = terminsAr,
        };

        return new AktivitetsbidragResult
        {
            Issues = issues,
            Summering = summering,
        };
    }

    private static SammankomstBidrag ValidateAndCalculateSammankomst(
        DakSammankomst sammankomst,
        AktivitetsbidragSettings settings,
        int terminsAr,
        Dictionary<string, DakDeltagare> allPersons,
        List<DakParseIssue> issues)
    {
        var path = $"Sammankomst[@kod='{sammankomst.Kod}']";
        // Check duration
        if (sammankomst.DurationMinutes < settings.MinSammankomstMinuter)
        {
            issues.Add(new DakParseIssue
            {
                Severity = DakIssueSeverity.Warning,
                Message = $"Sammankomsten är {sammankomst.DurationMinutes} min, " +
                          $"kräver minst {settings.MinSammankomstMinuter} min.",
                XmlPath = path,
                ActualValue = $"{sammankomst.DurationMinutes} min",
                ExpectedValue = $"minst {settings.MinSammankomstMinuter} min",
            });

            return NonQualifying(sammankomst,
                $"Sammankomsten är för kort ({sammankomst.DurationMinutes} min, kräver {settings.MinSammankomstMinuter}).");
        }

        // Validate leaders
        var qualifiedLeaders = CountQualifiedLeaders(sammankomst, settings, terminsAr, allPersons, issues, path);
        if (qualifiedLeaders < settings.MinAntalLedare)
        {
            return NonQualifying(sammankomst,
                $"För få ledare ({qualifiedLeaders}, kräver minst {settings.MinAntalLedare}).");
        }

        // Count eligible participants
        int flickor = 0, pojkar = 0;
        foreach (var deltagare in sammankomst.Deltagare)
        {
            var person = ResolvePersonFromRegister(deltagare, allPersons);
            if (!IsEligibleParticipant(person, settings, terminsAr, issues, path))
                continue;

            if (person.IsFemale())
                flickor++;
            else
                pojkar++;
        }

        var totalEligible = flickor + pojkar;
        if (totalEligible < settings.MinAntalDeltagare)
        {
            issues.Add(new DakParseIssue
            {
                Severity = DakIssueSeverity.Warning,
                Message = $"Sammankomsten har {totalEligible} bidragsberättigade deltagare, " +
                          $"kräver minst {settings.MinAntalDeltagare}.",
                XmlPath = path,
                ActualValue = totalEligible.ToString(),
                ExpectedValue = $"minst {settings.MinAntalDeltagare}",
            });

            return NonQualifying(sammankomst,
                $"För få bidragsberättigade deltagare ({totalEligible}, kräver minst {settings.MinAntalDeltagare}).");
        }

        return new SammankomstBidrag
        {
            Kod = sammankomst.Kod,
            Datum = sammankomst.Datum,
            ArBidragsberattigad = true,
            AntalFlickor = flickor,
            AntalPojkar = pojkar,
            BeloppFlickor = flickor * settings.BeloppPerFlicka,
            BeloppPojkar = pojkar * settings.BeloppPerPojke,
        };
    }

    private static int CountQualifiedLeaders(
        DakSammankomst sammankomst,
        AktivitetsbidragSettings settings,
        int terminsAr,
        Dictionary<string, DakDeltagare> allPersons,
        List<DakParseIssue> issues,
        string path)
    {
        int count = 0;
        foreach (var ledare in sammankomst.Ledare)
        {
            var person = ResolvePersonFromRegister(ledare, allPersons);
            var age = person.AgeThisSemester(terminsAr);

            if (string.IsNullOrEmpty(person.Personnummer))
            {
                issues.Add(new DakParseIssue
                {
                    Severity = DakIssueSeverity.Warning,
                    Message = $"Ledare '{person.Uid}' saknar personnummer  kan inte verifiera ålder.",
                    XmlPath = $"{path}/LedarLista/Ledare[@id='{person.Uid}']",
                });
                continue;
            }

            if (age < settings.MinAlderLedare)
            {
                issues.Add(new DakParseIssue
                {
                    Severity = DakIssueSeverity.Warning,
                    Message = $"Ledare '{person.Uid}' är {age} år, kräver minst {settings.MinAlderLedare} år.",
                    XmlPath = $"{path}/LedarLista/Ledare[@id='{person.Uid}']",
                    ActualValue = $"{age} år",
                    ExpectedValue = $"minst {settings.MinAlderLedare} år",
                });
                continue;
            }

            count++;
        }
        return count;
    }

    private static bool IsEligibleParticipant(
        DakDeltagare person,
        AktivitetsbidragSettings settings,
        int terminsAr,
        List<DakParseIssue> issues,
        string path)
    {
        if (string.IsNullOrEmpty(person.Personnummer))
        {
            issues.Add(new DakParseIssue
            {
                Severity = DakIssueSeverity.Warning,
                Message = $"Deltagare '{person.Uid}' saknar personnummer  räknas inte som bidragsberättigad.",
                XmlPath = $"{path}/DeltagarLista/Deltagare[@id='{person.Uid}']",
            });
            return false;
        }

        if (person.Personnummer.Length != 12)
        {
            issues.Add(new DakParseIssue
            {
                Severity = DakIssueSeverity.Warning,
                Message = $"Deltagare '{person.Uid}' har ogiltigt personnummer (längd {person.Personnummer.Length})  räknas inte.",
                XmlPath = $"{path}/DeltagarLista/Deltagare[@id='{person.Uid}']",
            });
            return false;
        }

        var age = person.AgeThisSemester(terminsAr);
        if (age < settings.MinAlderDeltagare || age > settings.MaxAlderDeltagare)
        {
            issues.Add(new DakParseIssue
            {
                Severity = DakIssueSeverity.Info,
                Message = $"Deltagare '{person.Uid}' är {age} år  utanför bidragsberättigad ålder ({settings.MinAlderDeltagare}{settings.MaxAlderDeltagare}).",
                XmlPath = $"{path}/DeltagarLista/Deltagare[@id='{person.Uid}']",
                ActualValue = $"{age} år",
                ExpectedValue = $"{settings.MinAlderDeltagare}{settings.MaxAlderDeltagare} år",
            });
            return false;
        }

        return true;
    }

    private static void ValidateSammankomstKoder(DakData dak, List<DakParseIssue> issues)
    {
        var seenKoder = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sammankomst in dak.Kort.Sammankomster)
        {
            // The bug in softadmin where you had to have Kod as a int32 is fixed.

            if (sammankomst.Kod.Length > 50)
            {
                issues.Add(new DakParseIssue
                {
                    Severity = DakIssueSeverity.Error,
                    Message = $"Sammankomstkod '{sammankomst.Kod}' längre än 50 tecken.",
                    XmlPath = $"Sammankomst[@kod='{sammankomst.Kod}']",
                    ActualValue = sammankomst.Kod,
                    ExpectedValue = "String (max length 50)",
                });
            }
            if (sammankomst.Kod.Length < 3)
            {
                issues.Add(new DakParseIssue
                {
                    Severity = DakIssueSeverity.Warning,
                    Message = $"Sammankomstkod '{sammankomst.Kod}' är för kort.",
                    XmlPath = $"Sammankomst[@kod='{sammankomst.Kod}']",
                    ActualValue = sammankomst.Kod,
                    ExpectedValue = "String (max length 50)",
                });
            }

            if (!seenKoder.Add(sammankomst.Kod))
            {
                issues.Add(new DakParseIssue
                {
                    Severity = DakIssueSeverity.Error,
                    Message = $"Duplicerad sammankomstkod '{sammankomst.Kod}'.",
                    XmlPath = $"Sammankomst[@kod='{sammankomst.Kod}']",
                    ActualValue = sammankomst.Kod,
                });
            }
        }
    }

    /// <summary>
    /// Build a lookup dictionary from both register lists, keyed by Uid.
    /// </summary>
    private static Dictionary<string, DakDeltagare> BuildPersonLookup(DakData dak)
    {
        var lookup = new Dictionary<string, DakDeltagare>(StringComparer.Ordinal);
        foreach (var d in dak.Kort.Deltagare)
            lookup.TryAdd(d.Uid, d);
        foreach (var l in dak.Kort.Ledare)
            lookup.TryAdd(l.Uid, l);
        return lookup;
    }

    /// <summary>
    /// Resolve full person data from the register (sammankomst attendance entries
    /// may be stubs with only Uid).
    /// </summary>
    private static DakDeltagare ResolvePersonFromRegister(DakDeltagare stub, Dictionary<string, DakDeltagare> allPersons) =>
        allPersons.TryGetValue(stub.Uid, out var full) ? full : stub;

    private static SammankomstBidrag NonQualifying(DakSammankomst sammankomst, string orsak) =>
        new()
        {
            Kod = sammankomst.Kod,
            Datum = sammankomst.Datum,
            ArBidragsberattigad = false,
            AvslagsOrsak = orsak,
        };
}
