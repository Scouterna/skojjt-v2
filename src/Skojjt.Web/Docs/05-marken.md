# Märken

Skojjt har stöd för att spåra scoutmärken och individuella framsteg.

## Översikt

Märkeshanteringen låter dig:

- Se vilka **märken** som finns tillgängliga för avdelningen
- Spåra **individuella framsteg** för varje medlem
- Se en **sammanställning** av vem som uppnått vilka märken

## Skapa ett nytt märke

Det finns två sätt att skapa ett märke för din kår:

### Från mall

1. Gå till kårens **märkessida** (Märken i sidomenyn)
2. Klicka på **Från mall**
3. Välj en av de tillgängliga mallarna — märket skapas automatiskt med alla delar färdigkonfigurerade

### Manuellt

1. Gå till kårens **märkessida**
2. Klicka på **Nytt märke**
3. Fyll i namn, valfri beskrivning och bild-URL (se [Lägga till märkesbild](#lagga-till-markesbild) nedan)
4. Klicka **Skapa**
5. Lägg till scoutdelar och admindelar på märkets detaljsida

## Scoutdelar och admindelar

Varje märke består av **delar** som delas in i två typer:

### Scoutdelar

Scoutdelar är uppgifter och krav som **scouten själv** arbetar med. Det kan till exempel vara:

- Genomföra en aktivitet (t.ex. "Laga mat över öppen eld")
- Visa en färdighet (t.ex. "Slå tre olika knopar")
- Delta i ett moment (t.ex. "Sov ute en natt")

Scoutdelarna visas i framstegsmatrisen och bockas av allteftersom scouten genomför dem.

### Admindelar

Admindelar är steg som **ledare eller kåren** ansvarar för. Det kan till exempel vara:

- Beställning av märket
- Utdelning av märket till scouten
- Administrativ kontroll eller godkännande

Admindelarna hanteras separat i framstegsmatrisen. En scout som klarat alla scoutdelar flyttas automatiskt till "adminsteget" där ledarna kan bocka av de administrativa delarna.

### Flöde

1. Scouten arbetar med **scoutdelarna** — ledare bockar av framsteg i matrisen
2. När alla scoutdelar är klara visas scouten i **adminfasen**
3. Ledare bockar av **admindelarna** (beställning, utdelning etc.)
4. När alla delar är klara markeras märket som **klart** ✅

## Lägga till märkesbild

När du skapar eller redigerar ett märke kan du ange en **bild-URL** för att visa märkets bild i Skojjt.

### Kopiera rätt bildlänk

Märkesbilder finns ofta på [scouterna.se](https://www.scouterna.se). Så här kopierar du rätt länk:

1. Gå till märkets sida på scouterna.se
2. **Högerklicka** på märkesbilden
3. Välj **"Öppna bild i ny flik"** (eller liknande beroende på webbläsare)
4. Kontrollera URL:en i adressfältet:
   - ✅ **Rätt** — en direkt bild-URL som pekar på den faktiska bildfilen, t.ex.:
     `https://media.scoutcontent.se/uploads/2021/03/07a190a-4.png`
   - ❌ **Fel** — en omslagsadress som innehåller `/_next/image/` med den riktiga länken gömd i en `url`-parameter, t.ex.:
     `https://www.scouterna.se/_next/image/?url=https%3A%2F%2Fmedia.scoutcontent.se%2F...&w=3840&q=75`
5. Om du får en omslagsadress — klistra in den ändå! Skojjt upptäcker automatiskt detta och extraherar den riktiga bildlänken åt dig.

> **Tips:** Du kan också högerklicka på bilden och välja **"Kopiera bildadress"** direkt — Skojjt hanterar båda formaten.

## Märken per avdelning

Navigera till avdelningens **märkessida** för att se:

- Alla märken kopplade till avdelningen
- En matris med medlemmar och deras framsteg per märke
- Status för varje kombination av medlem och märke

## Hantera framsteg

Klicka på en cell i märkesmatrisen för att uppdatera en medlems framsteg för ett specifikt märke. Ändringarna sparas automatiskt och synkroniseras i realtid till andra ledare som har samma sida öppen.

## Märkesmallar

Registeransvariga och administratörer kan skapa och hantera **märkesmallar** via **Märkesmallar** i sidomenyn. Mallar definierar vilka delar (scout- och admindelar) ett märke ska ha och kan återanvändas av alla kårer.

Så här skapar du en mall:

1. Gå till **Märkesmallar** i sidomenyn
2. Klicka **Ny mall**
3. Fyll i namn, beskrivning och valfri bild-URL
4. Lägg till delar — ange typ (Scout eller Admin), kort beskrivning och valfri lång beskrivning för varje del
5. Klicka **Spara**

## Tips

- Uppdatera märkesframsteg löpande under terminen
- Använd märkesöversikten för att planera vilka aktiviteter som behövs
- Skapa mallar för märken som används av flera avdelningar så att du slipper konfigurera delarna varje gång
