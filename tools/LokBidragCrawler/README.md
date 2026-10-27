# LokBidragCrawler

Verktyg som samlar in information om lokalt aktivitetsstöd (LOK-stöd / aktivitetsbidrag) från Sveriges alla kommuner. Resultatet används som underlag när `src/Skojjt.Web/wwwroot/data/aktivitetsbidrag.json` uppdateras.

## Hur det fungerar

1. Hämtar kommunlistan från SKR (https://skr.se/kommunerochregioner/kommunerlista.8288.html).
2. Söker igenom varje kommuns webbplats efter sidor om aktivitetsbidrag/LOK-stöd.
3. Sammanfattar (valfritt) informationen med AI till strukturerad markdown på svenska: belopp, åldersgränser, ledarkrav, sammankomstlängd, rapporteringssystem m.m.
4. Skriver resultatet till:
   - `docs/lok-bidrag/lok-bidrag.md` — sammanställning för alla kommuner.
   - `docs/lok-bidrag/kommuner/*.md` — en fil per kommun med källhänvisningar.

Redan hämtade kommuner hoppas över vid nästa körning om inte `--force` anges.

## Användning

Kör från repots rot:

```pwsh
dotnet run --project tools/LokBidragCrawler -- [flaggor]
```

| Flagga | Beskrivning |
|---|---|
| `--out <katalog>` | Utdatakatalog (standard `docs/lok-bidrag`). |
| `--only <namn,namn>` | Kör bara angivna kommuner, t.ex. `--only Jönköping,Göteborg`. |
| `--force` | Hämta om kommuner som redan har en resultatfil. |
| `--parallel <n>` | Antal kommuner som hämtas parallellt. |
| `--skip-catalog` | Hoppa över sista steget som uppdaterar `aktivitetsbidrag.json`. |
| `--catalog-only` | Kör bara sista steget (från befintlig `lok-bidrag.md`), ingen crawling. |
| `--catalog <fil>` | Sökväg till katalogen. Standard `src/Skojjt.Web/wwwroot/data/aktivitetsbidrag.json`. |

## AI-sammanfattning (valfritt)

Utan AI-konfiguration skrivs endast hittade sidor och textutdrag. AI väljs via miljövariabler; Ollama har företräde om `OLLAMA_MODEL` är satt.

### Lokalt via Ollama

| Variabel | Beskrivning |
|---|---|
| `OLLAMA_MODEL` | Modell, t.ex. `gemma4` eller `qwen2.5-coder`. |
| `OLLAMA_HOST` | Standard `http://localhost:11434`. |

```pwsh
$env:OLLAMA_MODEL = "gemma4"
dotnet run --project tools/LokBidragCrawler -- --only Jönköping --force
```

Ollama-anrop görs ett i taget, oavsett `--parallel`.

### OpenAI-kompatibelt API

| Variabel | Beskrivning |
|---|---|
| `AI_API_KEY` / `OPENAI_API_KEY` | API-nyckel. |
| `AI_ENDPOINT` | Standard `https://api.openai.com/v1/chat/completions`. För GitHub Models: `https://models.github.ai/inference/chat/completions`. |
| `AI_MODEL` | Standard `gpt-4o-mini`. |

### Övrigt

| Variabel | Beskrivning |
|---|---|
| `LOK_VERBOSE=1` | Skriver ut varje besökt sida. |

## Att uppdatera aktivitetsbidrag.json

Som sista steg läser verktyget `lok-bidrag.md` och låter AI:n extrahera strukturerade värden per kommun (kräver AI-konfiguration). Kommunkod hämtas från `MunicipalityCodes`. Regler:

- Finns redan en post för samma kommunkod och år fylls endast saknade fält (eller fält med värdet 0) i – befintliga värden skrivs aldrig över.
- Annars läggs en ny post till med en anmärkning om att den är automatiskt insamlad.
- Kommuner utan belopp, utan hittad sida eller med fel hoppas över.

```pwsh
dotnet run --project tools/LokBidragCrawler -- --catalog-only
```

Resultatet är AI-genererat och kan innehålla fel. Verifiera alltid belopp och regler mot kommunens egen webbplats (källänkar finns i varje kommunfil) innan `src/Skojjt.Web/wwwroot/data/aktivitetsbidrag.json` uppdateras via pull request.
