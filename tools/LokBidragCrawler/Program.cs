using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// LOK-bidragsinsamlare
// Hämtar Sveriges kommuner från SKR, letar upp sidor om aktivitetsbidrag/LOK-stöd
// på varje kommuns webbplats och sparar resultatet som markdown.
//
// Användning:
//   dotnet run --project tools/LokBidragCrawler -- [--out docs/lok-bidrag] [--only Jönköping,Göteborg] [--force] [--parallel 6]
//
// AI (valfritt, OpenAI-kompatibelt chat completions-API):
//   AI_API_KEY / OPENAI_API_KEY  - API-nyckel (krävs för AI-sammanfattning)
//   AI_ENDPOINT                  - default https://api.openai.com/v1/chat/completions
//                                  (GitHub Models: https://models.github.ai/inference/chat/completions)
//   AI_MODEL                     - default gpt-4o-mini
// Lokalt via Ollama (har företräde om satt):
//   OLLAMA_MODEL                 - t.ex. gemma4 eller qwen2.5-coder
//   OLLAMA_HOST                  - default http://localhost:11434
// Utan nyckel används nyckelordsbaserad utdragning av relevanta textstycken.

var options = Options.Parse(args);
Directory.CreateDirectory(options.KommunDir);

using var http = CreateHttpClient();
var ai = AiClient.FromEnvironment(http);
Console.WriteLine(ai is null
    ? "AI: ingen API-nyckel hittad, använder nyckelordsutdragning."
    : $"AI: använder {ai.Model} via {ai.Endpoint}");

if (options.CatalogOnly)
{
    await Catalog.UpdateAsync(options, ai, CancellationToken.None);
    return;
}

var kommuner = await SkrList.FetchAsync(http);
Console.WriteLine($"Hittade {kommuner.Count} kommuner hos SKR.");

if (options.Only.Count > 0)
    kommuner = kommuner.Where(k => options.Only.Contains(k.Namn, StringComparer.OrdinalIgnoreCase)).ToList();

var done = 0;
await Parallel.ForEachAsync(kommuner, new ParallelOptions { MaxDegreeOfParallelism = options.Parallel }, async (kommun, ct) =>
{
    var file = Path.Combine(options.KommunDir, Slug(kommun.Namn) + ".md");
    if (!options.Force && File.Exists(file))
    {
        Interlocked.Increment(ref done);
        return;
    }

    string markdown;
    try
    {
        var result = await Crawler.FindAsync(http, kommun, ct);
        markdown = await Report.BuildAsync(kommun, result, ai, ct);
    }
    catch (Exception ex)
    {
        markdown = $"## {kommun.Namn}\n\nWebbplats: {kommun.Url}\n\n**Fel:** {ex.Message}\n";
    }

    await File.WriteAllTextAsync(file, markdown, ct);
    var n = Interlocked.Increment(ref done);
    Console.WriteLine($"[{n}/{kommuner.Count}] {kommun.Namn}");
});

Report.WriteIndex(options);
Console.WriteLine($"Sammanställning: {Path.Combine(options.OutDir, "lok-bidrag.md")}");

if (!options.SkipCatalog)
    await Catalog.UpdateAsync(options, ai, CancellationToken.None);

Console.WriteLine("Klart.");

static HttpClient CreateHttpClient()
{
    var handler = new SocketsHttpHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 10,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    };
    var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(25) };
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SkojjtLokBidragCrawler/1.0; +https://github.com/Scouterna/skojjt-v2)");
    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("sv-SE,sv;q=0.9");
    return client;
}

static string Slug(string name)
{
    var map = new Dictionary<char, string> { ['å'] = "a", ['ä'] = "a", ['ö'] = "o", ['é'] = "e", ['ü'] = "u" };
    var sb = new StringBuilder();
    foreach (var c in name.ToLowerInvariant())
    {
        if (map.TryGetValue(c, out var r)) sb.Append(r);
        else if (char.IsLetterOrDigit(c)) sb.Append(c);
        else sb.Append('-');
    }
    return Regex.Replace(sb.ToString(), "-+", "-").Trim('-');
}

internal sealed record Kommun(string Namn, string Url);

internal sealed record PageHit(string Url, string Title, string Text, int Score);

internal sealed record CrawlResult(IReadOnlyList<PageHit> Hits, int PagesVisited);

internal sealed class Options
{
    public string OutDir { get; private set; } = Path.Combine("docs", "lok-bidrag");
    public string KommunDir => Path.Combine(OutDir, "kommuner");
    public HashSet<string> Only { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool Force { get; private set; }
    public int Parallel { get; private set; } = 6;
    public bool SkipCatalog { get; private set; }
    public bool CatalogOnly { get; private set; }
    public string CatalogPath { get; private set; } = Path.Combine("src", "Skojjt.Web", "wwwroot", "data", "aktivitetsbidrag.json");

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out": o.OutDir = args[++i]; break;
                case "--force": o.Force = true; break;
                case "--skip-catalog": o.SkipCatalog = true; break;
                case "--catalog-only": o.CatalogOnly = true; break;
                case "--catalog": o.CatalogPath = args[++i]; break;
                case "--parallel": o.Parallel = int.Parse(args[++i]); break;
                case "--only":
                    foreach (var n in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        o.Only.Add(n);
                    break;
            }
        }
        return o;
    }
}

internal static class Html
{
    private static readonly Regex StripBlocks = new(@"<(script|style|noscript|svg|head|nav|footer)[^>]*>.*?</\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BlockTags = new(@"</?(p|div|li|h[1-6]|tr|br|section|article|ul|ol|table)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Tags = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Links = new(@"<a\s[^>]*href\s*=\s*[""']([^""'#]+)[""'][^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TitleRx = new(@"<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ToText(string html)
    {
        var s = StripBlocks.Replace(html, " ");
        s = BlockTags.Replace(s, "\n");
        s = Tags.Replace(s, " ");
        s = WebUtility.HtmlDecode(s);
        var lines = s.Split('\n')
            .Select(l => Regex.Replace(l, @"[ \t\u00a0]+", " ").Trim())
            .Where(l => l.Length > 0);
        return string.Join('\n', lines);
    }

    public static string Title(string html)
    {
        var m = TitleRx.Match(html);
        return m.Success ? WebUtility.HtmlDecode(Regex.Replace(m.Groups[1].Value, @"\s+", " ").Trim()) : string.Empty;
    }

    public static IEnumerable<(Uri Url, string Text)> GetLinks(string html, Uri baseUri)
    {
        foreach (Match m in Links.Matches(html))
        {
            var href = WebUtility.HtmlDecode(m.Groups[1].Value.Trim());
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!Uri.TryCreate(baseUri, href, out var uri)) continue;
            if (uri.Scheme is not ("http" or "https")) continue;
            var text = WebUtility.HtmlDecode(Regex.Replace(Tags.Replace(m.Groups[2].Value, " "), @"\s+", " ").Trim());
            yield return (uri, text);
        }
    }
}

internal static class SkrList
{
    private const string Url = "https://skr.se/kommunerochregioner/kommunerlista.8288.html";

    public static async Task<List<Kommun>> FetchAsync(HttpClient http)
    {
        var html = await http.GetStringAsync(Url);
        var result = new List<Kommun>();
        foreach (Match list in Regex.Matches(html, @"<ul class=""\s*lp-link-list[^""]*"">(.*?)</ul>", RegexOptions.Singleline))
        {
            foreach (Match a in Regex.Matches(list.Groups[1].Value, @"<a href=""(https?://[^""]+)""[^>]*>([^<]+)<", RegexOptions.Singleline))
            {
                var name = WebUtility.HtmlDecode(a.Groups[2].Value).Trim();
                if (name.Length > 0 && !result.Any(k => k.Namn == name))
                    result.Add(new Kommun(name, a.Groups[1].Value));
            }
        }
        return result;
    }
}

internal static class Crawler
{
    private const int MaxPages = 60;

    // Ord i länktext/URL som leder mot föreningsbidrag.
    private static readonly (string Word, int Weight)[] LinkWords =
    [
        ("aktivitetsbidrag", 60), ("aktivitetsstöd", 60), ("lok-stöd", 60), ("lokstöd", 60), ("lok", 25),
        ("föreningsbidrag", 35), ("foreningsbidrag", 35), ("föreningsstöd", 35), ("foreningsstod", 35),
        ("bidrag", 20), ("stöd", 8), ("förening", 15), ("forening", 15), ("fritid", 10), ("idrott", 6),
    ];

    // Ord i sidtext som tyder på att sidan beskriver aktivitetsbidraget.
    private static readonly (string Word, int Weight)[] PageWords =
    [
        ("aktivitetsbidrag", 20), ("lokalt aktivitetsstöd", 25), ("aktivitetsstöd", 15), ("lok-stöd", 20),
        ("sammankomst", 10), ("deltagartillfälle", 10), ("per deltagare", 15), ("kr/deltagare", 20),
        ("kronor/deltagare", 20), ("kr per deltagare", 20), ("kronor per deltagare", 20), ("7-25 år", 8), ("7–25 år", 8),
    ];

    private static readonly string[] SearchPaths =
    [
        "/sok.html?query=aktivitetsbidrag",
        "/sok?q=aktivitetsbidrag",
        "/sok?query=aktivitetsbidrag",
        "/search?q=aktivitetsbidrag",
        "/sok.4.html?query=aktivitetsbidrag",
    ];

    public static async Task<CrawlResult> FindAsync(HttpClient http, Kommun kommun, CancellationToken ct)
    {
        var start = new Uri(kommun.Url);
        var visited = new HashSet<string>();
        var queue = new PriorityQueue<(Uri Url, int Depth), int>();
        var hits = new List<PageHit>();
        string? rootHost = null;

        queue.Enqueue((start, 0), 0);
        foreach (var path in SearchPaths)
            queue.Enqueue((new Uri(start, path), 1), -40);

        while (queue.Count > 0 && visited.Count < MaxPages)
        {
            var (url, depth) = queue.Dequeue();
            var key = url.GetLeftPart(UriPartial.Query).TrimEnd('/').ToLowerInvariant();
            if (!visited.Add(key)) continue;

            string html;
            Uri finalUri;
            try
            {
                using var resp = await http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode) continue;
                var media = resp.Content.Headers.ContentType?.MediaType ?? "text/html";
                if (!media.Contains("html", StringComparison.OrdinalIgnoreCase)) continue;
                html = await resp.Content.ReadAsStringAsync(ct);
                finalUri = resp.RequestMessage?.RequestUri ?? url;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                continue;
            }

            rootHost ??= BaseHost(finalUri.Host);
            if (Environment.GetEnvironmentVariable("LOK_VERBOSE") == "1")
                Console.WriteLine($"  [{kommun.Namn}] d{depth} {finalUri}");
            var text = Html.ToText(html);
            var score = ScorePage(text);
            if (score >= 40 && !finalUri.Query.Contains("aktivitetsbidrag", StringComparison.OrdinalIgnoreCase) && !hits.Any(h => h.Url == finalUri.ToString()))
                hits.Add(new PageHit(finalUri.ToString(), Html.Title(html), text, score));

            // Bra träff: sluta leta efter fler.
            if (hits.Count(h => h.Score >= 90) >= 2) break;
            if (depth >= 7) continue;

            foreach (var (link, linkText) in Html.GetLinks(html, finalUri))
            {
                if (BaseHost(link.Host) != rootHost) continue;
                if (Regex.IsMatch(link.AbsolutePath, @"\.(pdf|docx?|xlsx?|jpg|png|zip)$", RegexOptions.IgnoreCase)) continue;
                var linkScore = ScoreLink(link, linkText);
                if (linkScore <= 0) continue;
                queue.Enqueue((link, depth + 1), -linkScore + depth * 2);
            }
        }

        return new CrawlResult(hits.OrderByDescending(h => h.Score).Take(3).ToList(), visited.Count);
    }

    private static string BaseHost(string host)
    {
        var parts = host.ToLowerInvariant().Split('.');
        return parts.Length >= 2 ? string.Join('.', parts[^2..]) : host;
    }

    private static int ScoreLink(Uri url, string text)
    {
        var hay = (Uri.UnescapeDataString(url.AbsolutePath) + " " + text).ToLowerInvariant();
        return LinkWords.Where(w => hay.Contains(w.Word)).Sum(w => w.Weight);
    }

    private static int ScorePage(string text)
    {
        var lower = text.ToLowerInvariant();
        var score = 0;
        foreach (var (word, weight) in PageWords)
            score += Math.Min(3, CountOf(lower, word)) * weight;
        if (Regex.IsMatch(lower, @"\d+[,.]?\d*\s*(kr|kronor)\b[^\n]{0,40}(deltagare|sammankomst|ledare)")) score += 40;
        return score;
    }

    private static int CountOf(string s, string word)
    {
        var count = 0;
        for (var i = s.IndexOf(word, StringComparison.Ordinal); i >= 0; i = s.IndexOf(word, i + word.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}

internal static class Report
{
    private static readonly string[] SnippetWords =
    [
        "aktivitetsbidrag", "aktivitetsstöd", "sammankomst", "deltagare", "ledare", "kr", "kronor", "år", "minuter", "ansök", "redovis",
    ];

    public static async Task<string> BuildAsync(Kommun kommun, CrawlResult result, AiClient? ai, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"## {kommun.Namn}");
        sb.AppendLine();
        sb.AppendLine($"Webbplats: {kommun.Url}  ");
        sb.AppendLine($"Insamlat: {DateTime.Now:yyyy-MM-dd}, {result.PagesVisited} sidor besökta");
        sb.AppendLine();

        if (result.Hits.Count == 0)
        {
            sb.AppendLine("_Ingen sida om aktivitetsbidrag/LOK-stöd hittades automatiskt._");
            return sb.ToString();
        }

        sb.AppendLine("Källor:");
        foreach (var h in result.Hits)
            sb.AppendLine($"- [{(string.IsNullOrWhiteSpace(h.Title) ? h.Url : h.Title)}]({h.Url}) (poäng {h.Score})");
        sb.AppendLine();

        string? summary = null;
        if (ai is not null)
        {
            try
            {
                summary = await ai.SummarizeAsync(kommun.Namn, result.Hits, ct);
            }
            catch (Exception ex)
            {
                sb.AppendLine($"_AI-sammanfattning misslyckades: {ex.Message}_");
                sb.AppendLine();
            }
        }

        if (summary is not null)
        {
            sb.AppendLine(summary.Trim());
        }
        else
        {
            sb.AppendLine("### Utdrag");
            sb.AppendLine();
            foreach (var line in Snippets(result.Hits[0].Text))
                sb.AppendLine($"> {line}  ");
        }

        return sb.ToString();
    }

    private static IEnumerable<string> Snippets(string text)
    {
        var lines = text.Split('\n');
        var keep = new SortedSet<int>();
        for (var i = 0; i < lines.Length; i++)
        {
            var l = lines[i].ToLowerInvariant();
            var isAmount = Regex.IsMatch(l, @"\d\s*(kr|kronor)\b");
            var isTopic = l.Contains("aktivitetsbidrag") || l.Contains("aktivitetsstöd") || l.Contains("sammankomst");
            if (!isAmount && !isTopic) continue;
            for (var j = Math.Max(0, i - 1); j <= Math.Min(lines.Length - 1, i + 2); j++)
                keep.Add(j);
        }
        return keep.Take(150).Select(i => lines[i]).Where(l => SnippetWords.Any(w => l.Contains(w, StringComparison.OrdinalIgnoreCase)) || l.Length < 200);
    }

    public static void WriteIndex(Options options)
    {
        var files = Directory.GetFiles(options.KommunDir, "*.md").OrderBy(f => f, StringComparer.Create(new System.Globalization.CultureInfo("sv-SE"), true));
        var sb = new StringBuilder();
        sb.AppendLine("# LOK-stöd / aktivitetsbidrag i Sveriges kommuner");
        sb.AppendLine();
        sb.AppendLine("Automatiskt insamlat av `tools/LokBidragCrawler`. Kontrollera alltid uppgifterna mot kommunens egen webbplats.");
        sb.AppendLine();
        foreach (var f in files)
        {
            sb.AppendLine(File.ReadAllText(f).TrimEnd());
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
        }
        File.WriteAllText(Path.Combine(options.OutDir, "lok-bidrag.md"), sb.ToString());
    }
}

internal sealed class AiClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;

    private AiClient(HttpClient http, string apiKey, string endpoint, string model)
    {
        _http = http;
        _apiKey = apiKey;
        Endpoint = endpoint;
        Model = model;
    }

    public string Endpoint { get; }
    public string Model { get; }

    private bool IsOllama { get; init; }
    private readonly SemaphoreSlim _gate = new(1, 1);

    public static AiClient? FromEnvironment(HttpClient http)
    {
        var ollamaModel = Environment.GetEnvironmentVariable("OLLAMA_MODEL");
        if (!string.IsNullOrWhiteSpace(ollamaModel))
        {
            var host = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434";
            var ollamaHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
            return new AiClient(ollamaHttp, string.Empty, host.TrimEnd('/') + "/api/chat", ollamaModel) { IsOllama = true };
        }

        var key = Environment.GetEnvironmentVariable("AI_API_KEY") ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(key)) return null;
        var endpoint = Environment.GetEnvironmentVariable("AI_ENDPOINT") ?? "https://api.openai.com/v1/chat/completions";
        var model = Environment.GetEnvironmentVariable("AI_MODEL") ?? "gpt-4o-mini";
        return new AiClient(http, key, endpoint, model);
    }

    public async Task<string> SummarizeAsync(string kommun, IReadOnlyList<PageHit> hits, CancellationToken ct)
    {
        var context = new StringBuilder();
        foreach (var h in hits)
        {
            context.AppendLine($"=== Källa: {h.Url}");
            context.AppendLine(h.Text.Length > 12000 ? h.Text[..12000] : h.Text);
        }

        var prompt = $"""
            Nedan finns text från {kommun}s webbplats. Sammanfatta reglerna för kommunens aktivitetsbidrag/LOK-stöd
            (lokalt aktivitetsstöd) till ideella föreningar på svenska i markdown, med rubriker på nivå 3.
            Ta med om det finns: belopp per deltagare/sammankomst (per ålder och kön), belopp per ledare, grundbidrag,
            åldersgränser, krav på sammankomst (min/max antal deltagare, minsta längd), antal ledare, ansökningsperioder,
            redovisningssystem (t.ex. DAK, Interbook Go, RBOK) och sista ansökningsdag.
            Skriv exakta belopp som de står. Hitta inte på något; skriv "Uppgift saknas" om något inte framgår.
            Om texten inte handlar om aktivitetsbidrag, svara endast "Ingen information om aktivitetsbidrag hittades."

            {context}
            """;

        return await ChatAsync(prompt, ct);
    }

    public async Task<string> ChatAsync(string prompt, CancellationToken ct)
    {
        if (IsOllama)
            return await OllamaChatAsync(prompt, ct);

        using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = Model,
                temperature = 0,
                messages = new object[]
                {
                    new { role = "system", content = "Du är en noggrann assistent som extraherar fakta om kommunala föreningsbidrag." },
                    new { role = "user", content = prompt },
                },
            }),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        for (var attempt = 0; ; attempt++)
        {
            using var clone = new HttpRequestMessage(req.Method, req.RequestUri) { Content = new StringContent(await req.Content!.ReadAsStringAsync(ct), Encoding.UTF8, "application/json") };
            clone.Headers.Authorization = req.Headers.Authorization;
            using var resp = await _http.SendAsync(clone, ct);
            if ((int)resp.StatusCode == 429 && attempt < 5)
            {
                await Task.Delay(TimeSpan.FromSeconds(10 * (attempt + 1)), ct);
                continue;
            }
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
    }

    // Ollama körs lokalt: en förfrågan i taget och större kontextfönster än standard.
    private async Task<string> OllamaChatAsync(string prompt, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var body = new
            {
                model = Model,
                stream = false,
                think = false,
                options = new { temperature = 0, num_ctx = 16384 },
                messages = new object[]
                {
                    new { role = "system", content = "Du är en noggrann assistent som extraherar fakta om kommunala föreningsbidrag." },
                    new { role = "user", content = prompt },
                },
            };
            using var resp = await _http.PostAsJsonAsync(Endpoint, body, ct);
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var content = doc.RootElement.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
            return Regex.Replace(content, @"<think>.*?</think>", string.Empty, RegexOptions.Singleline).Trim();
        }
        finally
        {
            _gate.Release();
        }
    }
}
