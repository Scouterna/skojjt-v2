# Närvarohantering

Närvarohantering är kärnan i Skojjt. Här beskriver vi hur du skapar sammankomster och registrerar närvaro.

## Närvarovyn

När du navigerar till en avdelning ser du närvarokortet – en tabell med:

- **Rader** – en rad per medlem (deltagare och ledare)
- **Kolumner** – en kolumn per sammankomst (datum)
- **Celler** – kryss (✓) för närvaro

## Skapa en sammankomst

1. Navigera till avdelningens närvarosida
2. Klicka på **Lägg till sammankomst**
3. Välj **datum** för sammankomsten
4. Sammankomsten skapas och en ny kolumn dyker upp i närvarokortet

> **OBS:** Det kan bara finnas en sammankomst per datum och avdelning.

## Registrera närvaro

Klicka på en cell i närvarokortet för att markera eller avmarkera närvaro. Ändringarna sparas automatiskt.

- **Grön markering** = närvarande
- **Tom cell** = ej närvarande

## Redigera en sammankomst

Klicka på en sammankomsts **datum** i närvarokortet för att öppna mötesdetaljsidan. Här kan du redigera:

| Fält | Beskrivning |
|---|---|
| **Namn** | Sammankomstens namn |
| **Datum** | Datum för sammankomsten |
| **Plats** | Var sammankomsten hölls |
| **Starttid** | När sammankomsten startade |
| **Längd (minuter)** | Sammankomstens längd |
| **Sluttid** | Alternativt: ange sluttid för att beräkna längden automatiskt |
| **Läger/vandring** | Markera om sammankomsten är en lägerdag eller vandring |

Nedanför formuläret visas en lista med **närvarande personer** för sammankomsten.

> **OBS:** Om avdelningen är **låst** kan du inte redigera eller ta bort sammankomsten.

## Ta bort en sammankomst

Du kan ta bort en sammankomst på två sätt:

- Från **närvarokortet**: klicka på sammankomstens meny (tre punkter) och välj **Ta bort**
- Från **mötesdetaljsidan**: klicka på **Ta bort**-knappen

All närvarodata för den sammankomsten raderas.

## Sensus närvarolista

Från avdelningens meny kan du generera en **Sensus närvarolista** – en utskriftsvänlig sammanställning av terminens närvaro. Denna kan användas som underlag vid rapportering.

Använd patruller för att dela upp avdelningen i mindre listor och se närvaro per patrull. Varje patrull behöver minst en ledare.

## Skicka närvaro till Sensus e-tjänst

Skojjt kan synka närvarodata direkt till [Sensus e-tjänst](https://e-tjanst.sensus.se) så att du slipper föra in närvaron manuellt.

### Förutsättningar

- Du behöver ett **användarnamn** (person-, samordnings- eller LMA-nummer) och **lösenord** till Sensus e-tjänst.
- Det måste finnas ett **arrangemang** i Sensus som motsvarar avdelningens verksamhet.

### Så här gör du

1. Navigera till avdelningens närvarosida
2. Öppna menyn och välj **Synka till Sensus**
3. Ange ditt **person-/LMA-nummer** (bara siffror) och ditt **lösenord**
4. Klicka **Logga in** – Skojjt hämtar dina tillgängliga arrangemang från Sensus
5. Välj det **arrangemang** du vill synka närvaron till
6. Klicka **Synka närvaro** – Skojjt matchar sammankomster och deltagare och skickar närvaron

### Resultat

Efter synkningen visas en sammanfattning med:

- Antal **synkade** sammankomster
- Antal **överhoppade** (redan fanns i Sensus)
- Antal **utan matchande datum** i Sensus
- Antal **matchade personer** av totalt antal deltagare
- En detaljerad **logg** som du kan expandera för mer information

### Om inloggningsuppgifter och säkerhet

Skojjt sparar **inte** ditt Sensus-lösenord. Uppgifterna används bara under den pågående sessionen för att kommunicera med Sensus API. Din webbläsare kan erbjuda att spara användarnamn och lösenord – det är upp till dig om du vill använda webbläsarens lösenordshanterare för att slippa ange dem varje gång.

> **Tips:** Använd webbläsarens inbyggda lösenordshanterare (t.ex. i Chrome, Edge eller Firefox) för att smidigt spara dina Sensus-uppgifter mellan sessioner.

## Tips

- Registrera närvaron direkt under eller efter sammankomsten så glömmer ni inte
- Ledare som är närvarande räknas också – glöm inte att markera dem
- Närvarodata används som underlag för DAK-exporten
- För **läger** med automatiska lägerdagar, se [Läger](/hjalp/07-lager)
