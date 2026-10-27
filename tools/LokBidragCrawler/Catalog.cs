using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Skojjt.Core.Data;

// Sista steget: läser lok-bidrag.md och uppdaterar aktivitetsbidrag.json.
// Befintliga värden skrivs aldrig över; endast saknade fält fylls i och nya kommuner läggs till.
internal static class Catalog
{
    private static readonly string[] NumberFields =
    [
        "beloppPerFlicka", "beloppPerPojke", "beloppPerLedare", "beloppPerSammankomst",
        "maxAntalLedareMedBidrag", "maxDeltagarePerLedare", "minAlderDeltagare", "maxAlderDeltagare",
        "minAlderLedare", "minAntalDeltagare", "minAntalLedare", "minSammankomstMinuter", "maxAntalDeltagare",
    ];

    private static readonly string[] Berakningstyper = ["Fast", "Pott", "PottKvartal", "PottTimmar", "Poang"];

    public static async Task UpdateAsync(Options options, AiClient? ai, CancellationToken ct)
    {
        var mdPath = Path.Combine(options.OutDir, "lok-bidrag.md");
        if (!File.Exists(mdPath))
        {
            Console.WriteLine($"Katalog: {mdPath} saknas, hoppar över uppdatering av aktivitetsbidrag.json.");
            return;
        }
        if (!File.Exists(options.CatalogPath))
        {
            Console.WriteLine($"Katalog: {options.CatalogPath} saknas, hoppar över.");
            return;
        }
        if (ai is null)
        {
            Console.WriteLine("Katalog: kräver AI (OLLAMA_MODEL eller AI_API_KEY) för att tolka lok-bidrag.md, hoppar över.");
            return;
        }

        var root = JsonNode.Parse(await File.ReadAllTextAsync(options.CatalogPath, ct))!.AsObject();
        var list = root["kommuner"]!.AsArray();

        var sections = ParseSections(await File.ReadAllTextAsync(mdPath, ct))
            .Where(s => options.Only.Count == 0 || options.Only.Contains(s.Namn))
            .ToList();

        int added = 0, updated = 0, skipped = 0;
        foreach (var (namn, text) in sections)
        {
            if (text.Contains("Ingen sida om aktivitetsbidrag", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Ingen information om aktivitetsbidrag", StringComparison.OrdinalIgnoreCase)
                || text.Contains("**Fel:**", StringComparison.Ordinal))
            {
                skipped++;
                continue;
            }

            var kod = FindCode(namn);
            if (kod is null)
            {
                Console.WriteLine($"Katalog: okänd kommunkod för {namn}, hoppar över.");
                skipped++;
                continue;
            }

            JsonObject? extracted;
            try
            {
                extracted = await ExtractAsync(ai, namn, text, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Katalog: AI-tolkning misslyckades för {namn}: {ex.Message}");
                skipped++;
                continue;
            }
            if (extracted is null || !HasAmounts(extracted))
            {
                skipped++;
                continue;
            }

            var year = ReadInt(extracted["ar"]) ?? CollectedYear(text) ?? DateTime.Now.Year;
            var existing = list.OfType<JsonObject>()
                .Where(e => (string?)e["kommunId"] == kod)
                .OrderByDescending(e => (int?)e["ar"] == year)
                .ThenByDescending(e => (int?)e["ar"] ?? 0)
                .FirstOrDefault();

            if (existing is not null && (int?)existing["ar"] == year)
            {
                if (Merge(existing, extracted))
                {
                    updated++;
                    Console.WriteLine($"Katalog: kompletterade {existing["namn"]}");
                }
                continue;
            }

            var entry = new JsonObject
            {
                ["namn"] = $"{namn} {year}",
                ["kommunId"] = kod,
                ["ar"] = year,
                ["berakningstyp"] = Berakningstyper.Contains((string?)extracted["berakningstyp"]) ? (string?)extracted["berakningstyp"] : "Fast",
                ["beloppPerFlicka"] = 0,
                ["beloppPerPojke"] = 0,
            };
            Merge(entry, extracted);
            entry["anmarkning"] = $"Automatiskt insamlat {DateTime.Now:yyyy-MM-dd} av LokBidragCrawler – verifiera mot kommunens webbplats. {(string?)extracted["anmarkning"]}".Trim();
            list.Add(entry);
            added++;
            Console.WriteLine($"Katalog: lade till {entry["namn"]}");
        }

        var json = root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        var tmp = options.CatalogPath + ".tmp";
        await File.WriteAllTextAsync(tmp, json + Environment.NewLine, new UTF8Encoding(false), ct);
        File.Move(tmp, options.CatalogPath, overwrite: true);

        Console.WriteLine($"Katalog: {added} nya, {updated} kompletterade, {skipped} hoppade över. Granska ändringarna i {options.CatalogPath} innan pull request.");
    }

    private static IEnumerable<(string Namn, string Text)> ParseSections(string markdown)
    {
        foreach (var part in Regex.Split(markdown, @"^## (?=[^\r\n]+\r?\n\s*Webbplats:)", RegexOptions.Multiline).Skip(1))
        {
            var nl = part.IndexOf('\n');
            if (nl < 0) continue;
            var namn = part[..nl].Trim();
            var text = Regex.Replace(part[(nl + 1)..], @"^---\s*$", string.Empty, RegexOptions.Multiline).Trim();
            yield return (namn, text);
        }
    }

    private static string? FindCode(string namn)
    {
        static string Norm(string s) => Regex.Replace(s, @"\s+(kommun|stad)$", string.Empty, RegexOptions.IgnoreCase).Trim();
        var n = Norm(namn);
        return MunicipalityCodes.All.FirstOrDefault(m => string.Equals(Norm(m.Name), n, StringComparison.OrdinalIgnoreCase))?.Code;
    }

    private static int? CollectedYear(string text)
    {
        var m = Regex.Match(text, @"Insamlat:\s*(\d{4})");
        return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    private static bool HasAmounts(JsonObject o) =>
        new[] { "beloppPerFlicka", "beloppPerPojke", "beloppPerLedare", "beloppPerSammankomst" }.Any(f => (ReadDecimal(o[f]) ?? 0) > 0)
        || (o["aldersBelopp"] is JsonArray a && a.Count > 0);

    private static bool Merge(JsonObject target, JsonObject source)
    {
        var changed = false;
        foreach (var f in NumberFields)
        {
            var value = ReadDecimal(source[f]);
            if (value is null || value <= 0) continue;
            var current = ReadDecimal(target[f]);
            if (current is not null && current != 0) continue;
            target[f] = value == decimal.Truncate(value.Value) && !f.StartsWith("belopp", StringComparison.Ordinal)
                ? (int)value.Value
                : value.Value;
            changed = true;
        }

        if (target["aldersBelopp"] is not JsonArray && source["aldersBelopp"] is JsonArray ages && ages.Count > 0)
        {
            var arr = new JsonArray();
            foreach (var a in ages.OfType<JsonObject>())
            {
                var belopp = ReadDecimal(a["belopp"]);
                if (belopp is null || belopp <= 0) continue;
                arr.Add(new JsonObject
                {
                    ["minAlder"] = ReadInt(a["minAlder"]),
                    ["maxAlder"] = ReadInt(a["maxAlder"]),
                    ["belopp"] = belopp,
                });
            }
            if (arr.Count > 0)
            {
                target["aldersBelopp"] = arr;
                changed = true;
            }
        }

        if (target["systemMottagare"] is null && source["systemMottagare"] is JsonValue sys && !string.IsNullOrWhiteSpace((string?)sys))
        {
            target["systemMottagare"] = (string?)sys;
            changed = true;
        }
        return changed;
    }

    private static decimal? ReadDecimal(JsonNode? node)
    {
        if (node is not JsonValue v) return null;
        if (v.TryGetValue<decimal>(out var d)) return d;
        if (v.TryGetValue<double>(out var dbl)) return (decimal)dbl;
        if (v.TryGetValue<string>(out var s)
            && decimal.TryParse(s.Replace(',', '.').Replace(" ", string.Empty), NumberStyles.Number, CultureInfo.InvariantCulture, out var p))
            return p;
        return null;
    }

    private static int? ReadInt(JsonNode? node) => ReadDecimal(node) is { } d ? (int)d : null;

    private static async Task<JsonObject?> ExtractAsync(AiClient ai, string namn, string text, CancellationToken ct)
    {
        var prompt = $$"""
            Nedan finns en sammanfattning av {{namn}}s regler för aktivitetsbidrag/LOK-stöd.
            Extrahera reglerna för vanliga ideella barn- och ungdomsföreningar (inte funktionshinder- eller pensionärsföreningar)
            och svara ENDAST med ett JSON-objekt enligt detta schema. Använd null för allt som inte uttryckligen står i texten.
            Hitta inte på värden. Belopp i kronor som decimaltal med punkt.
            {
              "ar": heltal (år reglerna gäller) eller null,
              "berakningstyp": "Fast" (fasta belopp), "Pott" (rörlig pott per termin), "PottKvartal", "PottTimmar" eller "Poang",
              "beloppPerFlicka": belopp per flicka/deltagare och sammankomst (samma som pojke om ej könsuppdelat),
              "beloppPerPojke": belopp per pojke/deltagare och sammankomst,
              "aldersBelopp": [ { "minAlder": int, "maxAlder": int, "belopp": decimal } ] om beloppet beror på ålder, annars null,
              "beloppPerLedare": belopp per ledare och sammankomst,
              "beloppPerSammankomst": belopp per godkänd sammankomst (oberoende av antal deltagare),
              "maxAntalLedareMedBidrag": int,
              "maxDeltagarePerLedare": int,
              "minAlderDeltagare": int,
              "maxAlderDeltagare": int,
              "minAlderLedare": int,
              "minAntalDeltagare": int,
              "minAntalLedare": int,
              "minSammankomstMinuter": int,
              "maxAntalDeltagare": int,
              "systemMottagare": redovisningssystem (t.ex. "Interbook Go", "Rbok") eller null,
              "anmarkning": kort svensk kommentar (max 200 tecken) eller null
            }

            {{(text.Length > 12000 ? text[..12000] : text)}}
            """;

        var answer = await ai.ChatAsync(prompt, ct);
        var m = Regex.Match(answer, @"\{.*\}", RegexOptions.Singleline);
        if (!m.Success) return null;
        try
        {
            return JsonNode.Parse(m.Value) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
