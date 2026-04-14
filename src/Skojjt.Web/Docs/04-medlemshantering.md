# Medlemshantering

Skojjt hanterar medlemmar genom koppling till Scoutnet, Sveriges Scouternas centrala medlemsregister.

## Scoutnet-import

Det primära sättet att lägga till och uppdatera medlemmar är att **importera från Scoutnet**. Denna funktion är tillgänglig för **registeransvariga**.

### Så gör du en import

1. Gå till kårens sida
2. Klicka på **Scoutnet Import**
3. Välj vilka avdelningar du vill importera
4. Klicka på **Importera**

Importen hämtar:

- **Nya medlemmar** – läggs till i Skojjt
- **Uppdaterade uppgifter** – namn, personnummer och kontaktinfo synkas
- **Avdelningstillhörighet** – medlemmar placeras i rätt avdelning
- **Roller** – ledare och deltagare identifieras

> **Tips:** Ändringar i patruller och avdelningstillhörighet kan synkas tillbaka till Scoutnet med **Scoutnet synk** (se nedan).

### API-nyckel för import

För att Scoutnet-importen ska fungera behöver kåren ha en giltig API-nyckel konfigurerad. Nyckeln hämtas från Scoutnet:

1. Logga in i **Scoutnet** och gå till kårens administrationssida
2. Klicka på fliken **Webbkoppling**
3. Under **API-nycklar och endpoints**, leta efter **"Get a detailed csv/xls/json list of all members"**
4. Klicka på **Generera nyckel** om det saknas en nyckel
5. Kopiera nyckeln och klistra in den i **Kårinställningar** → **Scoutnet API-nycklar** i Skojjt

Se [Scoutkårer & avdelningar](/hjalp/02-scoutkar-avdelningar) för mer detaljer om hur du sätter upp en ny kår.

## Scoutnet synk – skicka tillbaka avdelning och patrull

Med **Scoutnet synk** kan du skicka ändringar i avdelningstillhörighet och patruller tillbaka till Scoutnet. Skojjt jämför den aktuella terminen med vad som finns i Scoutnet och visar vilka skillnader som kan synkas.

### Förutsättningar

Scoutnet synk kräver **två API-nycklar** konfigurerade under **Kårinställningar** → **Scoutnet API-nycklar**:

1. **"Get a detailed csv/xls/json list of all members"** – samma nyckel som används för import. Behövs för att hämta nuvarande data från Scoutnet och jämföra med Skojjt.
2. **"Update membership"** (`api/organisation/update/membership`) – en separat nyckel som ger Skojjt skrivrättigheter att uppdatera avdelning och patrull i Scoutnet.

Båda nycklarna hämtas från Scoutnet:

1. Logga in i **Scoutnet** och gå till kårens administrationssida
2. Klicka på fliken **Webbkoppling**
3. Under **API-nycklar och endpoints**, generera nycklar för respektive endpoint
4. Kopiera nycklarna och klistra in dem i **Kårinställningar** i Skojjt

> **OBS:** Utan API-nyckeln för "Update membership" kan Skojjt inte skicka ändringar till Scoutnet. Importnyckeln räcker inte – den ger bara läsrättigheter.

### Så här gör du

1. Gå till kårens terminsvy (avdelningsöversikten)
2. Klicka på **Synka med Scoutnet**
3. Skojjt hämtar aktuell data från Scoutnet och visar en **förhandsgranskning** av ändringarna:
   - **Avdelningsbyten** – medlemmar som tillhör en annan avdelning i Skojjt än i Scoutnet
   - **Patrullbyten** – medlemmar som tillhör en annan patrull i Skojjt än i Scoutnet
4. Granska ändringarna och klicka **Skicka till Scoutnet** för att genomföra synkningen

### Vad synkas?

- **Avdelningstillhörighet** – om en deltagare (ej ledare) har bytt avdelning i Skojjt skickas den nya avdelningen till Scoutnet
- **Patrulltillhörighet** – om en medlem har bytt patrull i Skojjt skickas den nya patrullen till Scoutnet

### Begränsningar

- **Ledare** synkas inte för avdelningsbyten, eftersom ledare kan ha roller i flera avdelningar
- **Lokalt skapade avdelningar** (som inte finns i Scoutnet) kan inte synkas – dessa medlemmar hoppas över
- Patruller som saknar Scoutnet-ID kan inte synkas – kör en **Scoutnet-import** först så att patruller får sina ID:n

## Manuellt tillagda medlemmar

Om en person inte finns i Scoutnet kan du lägga till dem manuellt via **Lägg till medlem** (tillgänglig för registeransvariga).

## Borttagna medlemmar

När en medlem tas bort från Scoutnet och ni gör en ny import:

- Medlemmen **tas inte bort** från Skojjt automatiskt
- Medlemmen markeras som **borttagen/inaktiv**
- Historisk närvarodata bevaras för rapportering

Du kan manuellt ta bort en medlem från en avdelning om personen inte längre ska vara där.

## Alla medlemmar

Registeransvariga kan se en lista över **alla medlemmar** i kåren, oavsett avdelningstillhörighet. Sidan nås från kårens meny.

Listan visar namn, ålder, e-post, mobil, adress och status. Du kan:

- **Söka** – filtrera på namn, e-post eller telefonnummer med sökfältet
- **Sortera** – klicka på kolumnrubrikerna för att sortera listan
- **Visa borttagna** – slå på reglaget för att inkludera borttagna medlemmar (markerade med röd etikett)
- **Exportera** – klicka **Exportera** för att ladda ner medlemslistan som CSV-fil

Klicka på en medlems namn för att öppna [personsidan](#personsida) med fullständig information.

## Personsida

Klicka på en medlems **namn** i en avdelning eller i listan med alla medlemmar för att öppna personsidan. Här visas detaljerad information om personen:

### Kontaktuppgifter

E-post, telefon, mobilnummer och adress. Alla kontaktuppgifter är klickbara – e-post öppnar ditt e-postprogram och telefonnummer ringer direkt.

### Vårdnadshavare

Namn, e-post och mobilnummer till vårdnadshavare. Dessa uppgifter importeras från Scoutnet.

### Medlemskap

Medlemsnummer, personnummer och medlemsår. Om personen har tagits bort från Scoutnet visas en röd **Borttagen**-markering.

### Avdelningar

En lista med alla avdelningar personen tillhör, med termin, patrull och roll (ledare/deltagare). Klicka på en avdelning för att navigera till närvarokortet.

### Märkesframsteg

Alla märken personen påbörjat eller klarat visas som kort med framstegsindikator. Klicka på ett märke för att se märkets detaljer.

## Personnummer

Personnummer används för att identifiera medlemmar och för att bestämma kön vid DAK-rapportering:


Detta påverkar bidragsberäkningen i DAK-exporten.
