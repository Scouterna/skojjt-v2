namespace Skojjt.Core.Data;

/// <summary>
/// Swedish municipality (kommun) codes.
/// Source: https://skr.se/kommunerochregioner/kommunkoder.8282.html
/// </summary>
public static class MunicipalityCodes
{
    /// <summary>
    /// A municipality entry with name and code.
    /// </summary>
    public record Municipality(string Name, string Code);

    /// <summary>
    /// All Swedish municipalities with their codes.
    /// </summary>
    public static readonly IReadOnlyList<Municipality> All = new List<Municipality>
    {
        // A
        new("Ale", "1440"),
        new("Alingsås", "1489"),
        new("Alvesta", "0764"),
        new("Aneby", "0604"),
        new("Arboga", "1984"),
        new("Arjeplog", "2506"),
        new("Arvidsjaur", "2505"),
        new("Arvika", "1784"),
        new("Askersund", "1882"),
        new("Avesta", "2084"),
        
        // B
        new("Bengtsfors", "1460"),
        new("Berg", "2326"),
        new("Bjurholm", "2403"),
        new("Bjuv", "1260"),
        new("Boden", "2582"),
        new("Bollebygd", "1443"),
        new("Bollnäs", "2183"),
        new("Borgholm", "0885"),
        new("Borlänge", "2081"),
        new("Borås", "1490"),
        new("Botkyrka", "0127"),
        new("Boxholm", "0560"),
        new("Bromölla", "1272"),
        new("Bräcke", "2305"),
        new("Burlöv", "1231"),
        new("Båstad", "1278"),
        
        // D
        new("Dals-Ed", "1438"),
        new("Danderyd", "0162"),
        new("Degerfors", "1862"),
        new("Dorotea", "2425"),
        
        // E
        new("Eda", "1730"),
        new("Ekerö", "0125"),
        new("Eksjö", "0686"),
        new("Emmaboda", "0862"),
        new("Enköping", "0381"),
        new("Eskilstuna", "0484"),
        new("Eslöv", "1285"),
        new("Essunga", "1445"),
        
        // F
        new("Fagersta", "1982"),
        new("Falkenberg", "1382"),
        new("Falköping", "1499"),
        new("Falun", "2080"),
        new("Filipstad", "1782"),
        new("Finspång", "0562"),
        new("Flen", "0482"),
        new("Forshaga", "1763"),
        new("Färgelanda", "1439"),
        
        // G
        new("Gagnef", "2026"),
        new("Gislaved", "0662"),
        new("Gnesta", "0461"),
        new("Gnosjö", "0617"),
        new("Gotland", "0980"),
        new("Grums", "1764"),
        new("Grästorp", "1444"),
        new("Gullspång", "1447"),
        new("Gällivare", "2523"),
        new("Gävle", "2180"),
        new("Göteborg", "1480"),
        new("Götene", "1471"),
        
        // H
        new("Habo", "0643"),
        new("Hagfors", "1783"),
        new("Hallsberg", "1861"),
        new("Hallstahammar", "1961"),
        new("Halmstad", "1380"),
        new("Hammarö", "1761"),
        new("Haninge", "0136"),
        new("Haparanda", "2583"),
        new("Heby", "0331"),
        new("Hedemora", "2083"),
        new("Helsingborg", "1283"),
        new("Herrljunga", "1466"),
        new("Hjo", "1497"),
        new("Hofors", "2104"),
        new("Huddinge", "0126"),
        new("Hudiksvall", "2184"),
        new("Hultsfred", "0860"),
        new("Hylte", "1315"),
        new("Håbo", "0305"),
        new("Hällefors", "1863"),
        new("Härjedalen", "2361"),
        new("Härnösand", "2280"),
        new("Härryda", "1401"),
        new("Hässleholm", "1293"),
        new("Höganäs", "1284"),
        new("Högsby", "0821"),
        new("Hörby", "1266"),
        new("Höör", "1267"),
        
        // J
        new("Jokkmokk", "2510"),
        new("Järfälla", "0123"),
        new("Jönköping", "0680"),
        
        // K
        new("Kalix", "2514"),
        new("Kalmar", "0880"),
        new("Karlsborg", "1446"),
        new("Karlshamn", "1082"),
        new("Karlskoga", "1883"),
        new("Karlskrona", "1080"),
        new("Karlstad", "1780"),
        new("Katrineholm", "0483"),
        new("Kil", "1715"),
        new("Kinda", "0513"),
        new("Kiruna", "2584"),
        new("Klippan", "1276"),
        new("Knivsta", "0330"),
        new("Kramfors", "2282"),
        new("Kristianstad", "1290"),
        new("Kristinehamn", "1781"),
        new("Krokom", "2309"),
        new("Kumla", "1881"),
        new("Kungsbacka", "1384"),
        new("Kungsör", "1960"),
        new("Kungälv", "1482"),
        new("Kävlinge", "1261"),
        new("Köping", "1983"),
        
        // L
        new("Laholm", "1381"),
        new("Landskrona", "1282"),
        new("Laxå", "1860"),
        new("Lekeberg", "1814"),
        new("Leksand", "2029"),
        new("Lerum", "1441"),
        new("Lessebo", "0761"),
        new("Lidingö", "0186"),
        new("Lidköping", "1494"),
        new("Lilla Edet", "1462"),
        new("Lindesberg", "1885"),
        new("Linköping", "0580"),
        new("Ljungby", "0781"),
        new("Ljusdal", "2161"),
        new("Ljusnarsberg", "1864"),
        new("Lomma", "1262"),
        new("Ludvika", "2085"),
        new("Luleå", "2580"),
        new("Lund", "1281"),
        new("Lycksele", "2481"),
        new("Lysekil", "1484"),
        
        // M
        new("Malmö", "1280"),
        new("Malung–Sälen", "2023"),
        new("Malå", "2418"),
        new("Mariestad", "1493"),
        new("Mark", "1463"),
        new("Markaryd", "0767"),
        new("Mellerud", "1461"),
        new("Mjölby", "0586"),
        new("Mora", "2062"),
        new("Motala", "0583"),
        new("Mullsjö", "0642"),
        new("Munkedal", "1430"),
        new("Munkfors", "1762"),
        new("Mölndal", "1481"),
        new("Mönsterås", "0861"),
        new("Mörbylånga", "0840"),
        
        // N
        new("Nacka", "0182"),
        new("Norberg", "1962"),
        new("Nora", "1884"),
        new("Nordanstig", "2132"),
        new("Nordmaling", "2401"),
        new("Norrköping", "0581"),
        new("Norrtälje", "0188"),
        new("Norsjö", "2417"),
        new("Nybro", "0881"),
        new("Nykvarn", "0140"),
        new("Nyköping", "0480"),
        new("Nynäshamn", "0192"),
        new("Nässjö", "0682"),
        
        // O
        new("Ockelbo", "2101"),
        new("Olofström", "1060"),
        new("Orsa", "2034"),
        new("Orust", "1421"),
        new("Osby", "1273"),
        new("Oskarshamn", "0882"),
        new("Ovanåker", "2121"),
        new("Oxelösund", "0481"),
        
        // P
        new("Pajala", "2521"),
        new("Partille", "1402"),
        new("Perstorp", "1275"),
        new("Piteå", "2581"),
        
        // R
        new("Ragunda", "2303"),
        new("Robertsfors", "2409"),
        new("Ronneby", "1081"),
        new("Rättvik", "2031"),
        
        // S
        new("Sala", "1981"),
        new("Salem", "0128"),
        new("Sandviken", "2181"),
        new("Sigtuna", "0191"),
        new("Simrishamn", "1291"),
        new("Sjöbo", "1265"),
        new("Skara", "1495"),
        new("Skellefteå", "2482"),
        new("Skinnskatteberg", "1904"),
        new("Skurup", "1264"),
        new("Skövde", "1496"),
        new("Smedjebacken", "2061"),
        new("Sollefteå", "2283"),
        new("Sollentuna", "0163"),
        new("Solna", "0184"),
        new("Sorsele", "2422"),
        new("Sotenäs", "1427"),
        new("Staffanstorp", "1230"),
        new("Stenungsund", "1415"),
        new("Stockholm", "0180"),
        new("Storfors", "1760"),
        new("Storuman", "2421"),
        new("Strängnäs", "0486"),
        new("Strömstad", "1486"),
        new("Strömsund", "2313"),
        new("Sundbyberg", "0183"),
        new("Sundsvall", "2281"),
        new("Sunne", "1766"),
        new("Surahammar", "1907"),
        new("Svalöv", "1214"),
        new("Svedala", "1263"),
        new("Svenljunga", "1465"),
        new("Säffle", "1785"),
        new("Säter", "2082"),
        new("Sävsjö", "0684"),
        new("Söderhamn", "2182"),
        new("Söderköping", "0582"),
        new("Södertälje", "0181"),
        new("Sölvesborg", "1083"),
        
        // T
        new("Tanum", "1435"),
        new("Tibro", "1472"),
        new("Tidaholm", "1498"),
        new("Tierp", "0360"),
        new("Timrå", "2262"),
        new("Tingsryd", "0763"),
        new("Tjörn", "1419"),
        new("Tomelilla", "1270"),
        new("Torsby", "1737"),
        new("Torsås", "0834"),
        new("Tranemo", "1452"),
        new("Tranås", "0687"),
        new("Trelleborg", "1287"),
        new("Trollhättan", "1488"),
        new("Trosa", "0488"),
        new("Tyresö", "0138"),
        new("Täby", "0160"),
        new("Töreboda", "1473"),
        
        // U
        new("Uddevalla", "1485"),
        new("Ulricehamn", "1491"),
        new("Umeå", "2480"),
        new("Upplands-Bro", "0139"),
        new("Upplands Väsby", "0114"),
        new("Uppsala", "0380"),
        new("Uppvidinge", "0760"),
        
        // V
        new("Vadstena", "0584"),
        new("Vaggeryd", "0665"),
        new("Valdemarsvik", "0563"),
        new("Vallentuna", "0115"),
        new("Vansbro", "2021"),
        new("Vara", "1470"),
        new("Varberg", "1383"),
        new("Vaxholm", "0187"),
        new("Vellinge", "1233"),
        new("Vetlanda", "0685"),
        new("Vilhelmina", "2462"),
        new("Vimmerby", "0884"),
        new("Vindeln", "2404"),
        new("Vingåker", "0428"),
        new("Vårgårda", "1442"),
        new("Vänersborg", "1487"),
        new("Vännäs", "2460"),
        new("Värmdö", "0120"),
        new("Värnamo", "0683"),
        new("Västervik", "0883"),
        new("Västerås", "1980"),
        new("Växjö", "0780"),
        
        // Y
        new("Ydre", "0512"),
        new("Ystad", "1286"),
        
        // Å
        new("Åmål", "1492"),
        new("Ånge", "2260"),
        new("Åre", "2321"),
        new("Årjäng", "1765"),
        new("Åsele", "2463"),
        new("Åstorp", "1277"),
        new("Åtvidaberg", "0561"),
        
        // Ä
        new("Älmhult", "0765"),
        new("Älvdalen", "2039"),
        new("Älvkarleby", "0319"),
        new("Älvsbyn", "2560"),
        new("Ängelholm", "1292"),
        
        // Ö
        new("Öckerö", "1407"),
        new("Ödeshög", "0509"),
        new("Örebro", "1880"),
        new("Örkelljunga", "1257"),
        new("Örnsköldsvik", "2284"),
        new("Östersund", "2380"),
        new("Österåker", "0117"),
        new("Östhammar", "0382"),
        new("Östra Göinge", "1256"),
        new("Överkalix", "2513"),
        new("Övertorneå", "2518"),
    }.AsReadOnly();

    /// <summary>
    /// Dictionary for quick lookup by municipality code.
    /// </summary>
    private static readonly Dictionary<string, Municipality> ByCode = 
        All.ToDictionary(m => m.Code, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a municipality by its code.
    /// </summary>
    public static Municipality? GetByCode(string code) =>
        ByCode.TryGetValue(code, out var municipality) ? municipality : null;

    /// <summary>
    /// Checks if a municipality code is valid.
    /// </summary>
    public static bool IsValidCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ByCode.ContainsKey(code);

    /// <summary>
    /// Gets the display name for a municipality code (e.g., "Göteborg (1480)").
    /// </summary>
    public static string? GetDisplayName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var municipality = GetByCode(code);
        return municipality != null ? $"{municipality.Name} ({municipality.Code})" : null;
    }

    /// <summary>
    /// Searches municipalities by name or code (case-insensitive, partial match).
    /// </summary>
    public static IEnumerable<Municipality> Search(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return All;

        return All.Where(m => 
            m.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            m.Code.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }
}
