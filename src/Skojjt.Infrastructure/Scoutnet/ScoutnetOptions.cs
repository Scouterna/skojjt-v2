namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Configuration options for the Scoutnet integration.
/// </summary>
public class ScoutnetOptions
{
    /// <summary>
    /// Configuration section name in appsettings.
    /// </summary>
    public const string SectionName = "Scoutnet";

    /// <summary>
    /// Base URL of the Scoutnet server.
    /// Production: https://www.scoutnet.se
    /// Test: https://demo2.custard.no
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.scoutnet.se";
}
