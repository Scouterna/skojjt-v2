# Skojjt → Sensus Närvarosynk

Ett Tampermonkey-userscript som synkroniserar närvarodata från Skojjt till Sensus e-tjänst. Scriptet matchar personer och sammankomster mellan systemen baserat på namn och datum, och uppdaterar närvarolistorna i Sensus automatiskt.

## Förutsättningar

- **Tampermonkey** installerat i webbläsaren ([Chrome](https://chrome.google.com/webstore/detail/tampermonkey/dhdgffkkebhmkfjojejmpbldmpobfkfo), [Firefox](https://addons.mozilla.org/en-US/firefox/addon/tampermonkey/))
- Inloggad i **Skojjt** (via ScoutID) — scriptet använder din befintliga session
- Inloggad i **Sensus e-tjänst** — scriptet använder sidans sessionscookies
- Arrangemang skapat i Sensus med deltagare och sammankomster (scheman) inlagda

## Installation

1. Öppna Tampermonkey-ikonen i webbläsaren och välj **Skapa nytt script**.
2. Ta bort allt förskrivna innehåll.
3. Klistra in hela innehållet från [`sensus-tamper.js`](../sensus-tamper.js).
4. Spara scriptet (**Ctrl+S**).
5. Gå till [https://e-tjanst.sensus.se](https://e-tjanst.sensus.se) — du bör se en panel i nedre högra hörnet.

## Konfiguration

Klicka på **⚙ Inställningar** i panelen (eller via Tampermonkey-menyn → *⚙ Skojjt-inställningar*).

| Fält | Beskrivning |
|---|---|
| **Scoutkår** | Din scoutkår i Skojjt. Hämtas automatiskt från din inloggning. |
| **Avdelning** | Vilken avdelning som ska synkas. Visar avdelningar för aktuell termin. |
| **Sensus personnummer** | *(Valfritt)* Personnummer för automatisk inloggning i Sensus. |
| **Sensus lösenord** | *(Valfritt)* Lösenord för automatisk inloggning i Sensus. |

Inställningarna sparas lokalt i Tampermonkey (per webbläsare).

### Skojjt-inloggning

Scriptet kontrollerar automatiskt om du är inloggad i Skojjt. Om du inte är inloggad visas en knapp **🔑 Logga in på Skojjt** som öppnar Skojjt i en ny flik. Logga in via ScoutID och kom tillbaka till Sensus-fliken.

## Användning

### Synka närvaro

1. Öppna [Sensus e-tjänst](https://e-tjanst.sensus.se) i webbläsaren.
2. Se till att du är inloggad i både Skojjt och Sensus.
3. Klicka **▶ Synka närvaro** i panelen (eller via Tampermonkey-menyn).

Scriptet gör följande:

1. Hämtar medlemmar och sammankomster från Skojjt för vald avdelning och termin.
2. Hämtar närvarodetaljer för varje sammankomst.
3. Söker upp det första matchande arrangemanget i Sensus (eller använder arrangemanget från aktuell URL).
4. Hämtar deltagare och scheman från Sensus-arrangemanget.
5. Matchar Skojjt-medlemmar mot Sensus-deltagare baserat på namn.
6. För varje datum som finns i båda systemen: uppdaterar närvarolistan i Sensus.

### Resultat

Efter synk visas en sammanfattning i loggen:

- **Synkade** — antal scheman som uppdaterades med närvaro
- **Hoppade över** — redan signerade eller icke-redigerbara scheman
- **Utan matchande datum** — Skojjt-sammankomster som inte har ett Sensus-schema samma datum
- **Fel** — scheman som inte gick att uppdatera

### Namnmatchning

Scriptet matchar personer med följande strategi (i ordning):

1. Exakt matchning av fullständigt namn
2. Exakt matchning av förnamn + efternamn
3. Omvänt namn (om Sensus visar "Efternamn Förnamn")
4. Partiell matchning (delsträngar)

Omatchade personer loggas som varningar — de ignoreras vid synk men påverkar inte övriga.

## Felsökning

| Problem | Lösning |
|---|---|
| Panelen visas inte | Kontrollera att scriptet är aktiverat i Tampermonkey. Verifiera att du är på `e-tjanst.sensus.se`. |
| "Inte inloggad i Skojjt" | Klicka 🔑-knappen i inställningarna. Logga in i Skojjt i den flik som öppnas. |
| "Inte inloggad i Sensus" | Logga in i Sensus manuellt, eller konfigurera automatisk inloggning i inställningarna. |
| "Inga arrangemang hittades" | Du saknar arrangemang med närvaroregistrering i Sensus, eller så är filtren för restriktiva. |
| Många omatchade personer | Kontrollera att namnen stämmer mellan Skojjt och Sensus. Scriptet matchar på fullständigt namn. |
| 404-fel på deltagare | Verifiera att arrangemanget har deltagare inlagda i Sensus. |

## Tekniska detaljer

- Scriptet körs på `https://e-tjanst.sensus.se/*` och injiceras efter att sidan laddats (`document-idle`).
- Skojjt API-anrop görs via `GM_xmlhttpRequest` (cross-origin med cookies).
- Sensus API-anrop görs via `unsafeWindow.fetch` för att dela sidans sessionscookies.
- Sensus SPA använder ett icke-standardiserat URL-format där query-parametrar skrivs med `&` direkt i sökvägen istället för `?`.
- Konfiguration sparas med `GM_getValue`/`GM_setValue` (Tampermonkey-lagring).
- Aktuell termin beräknas automatiskt (VT = jan–jul, HT = aug–dec).
