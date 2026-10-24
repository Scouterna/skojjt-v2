using Skojjt.Core.Exports;
using Skojjt.Core.Exports.Validation;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Exports;

// Needed for DakParseResult, DakComparisonResult, DakData
using DakParseResult = Skojjt.Core.Exports.DakParseResult;
using DakComparisonResult = Skojjt.Core.Exports.DakComparisonResult;
using DakData = Skojjt.Core.Exports.DakData;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Service for analyzing DAK XML files: parse, normalize, compare, and validate.
/// </summary>
public class DakAnalysisService : IDakAnalysisService
{
    public DakParseResult Parse(Stream stream, string fileName = "") =>
        DakXmlReader.Parse(stream, fileName);

    public DakParseResult Parse(byte[] data, string fileName = "") =>
        DakXmlReader.Parse(data, fileName);

    public DakComparisonResult Compare(DakData old, DakData @new)
    {
        DakNormalizer.Normalize(old);
        DakNormalizer.Normalize(@new);
        return DakComparer.Compare(old, @new);
    }
    public AktivitetsbidragResult Validate(DakData dak, AktivitetsbidragSettings? settings, int terminsAr)
    {
        if (settings is null)
        {
            return new AktivitetsbidragResult
            {
                Issues =
                [
                    new DakParseIssue
                    {
                        Severity = DakIssueSeverity.Error,
                        Message = $"Inga bidragsinställningar hittades för kommun '{dak.KommunId}', år {terminsAr}. " +
                                  "Välj inställningar manuellt.",
                        XmlPath = "Kommun",
                        ActualValue = dak.KommunId,
                    }
                ],
                Summering = new BidragsSummering
                {
                    SammankomstBerakningar = [],
                    Settings = new AktivitetsbidragSettings
                    {
                        Namn = "Inga inställningar",
                        BeloppPerFlicka = 0m,
                        BeloppPerPojke = 0m,
                        MinAlderDeltagare = 0,
                        MaxAlderDeltagare = 0,
                        MinAlderLedare = 0,
                        MinAntalDeltagare = 0,
                        MinAntalLedare = 0,
                        MinSammankomstMinuter = 0,
                    },
                    TerminsAr = terminsAr,
                },
            };
        }

        return AktivitetsbidragValidator.Validate(dak, settings, terminsAr);
    }
}
