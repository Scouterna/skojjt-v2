# DAK-export och -import

DAK (Digitalt Aktivitetskort) är det format som används för att rapportera aktiviteter till kommunen för att söka **aktivitetsbidrag**. Skojjt kan både **exportera** DAK-filer och **importera** dem för att föra in möten och närvaro från ett annat system.

## Vad är aktivitetsbidrag?

Kommuner i Sverige ger ekonomiskt stöd till föreningar baserat på deras aktiviteter med barn och ungdomar. För att få bidraget behöver ni rapportera era sammankomster i ett standardiserat format – DAK.

## Exportera DAK-fil

1. Gå till kårens sida
2. Välj **termin**
3. Klicka på **Exportera DAK** (tillgänglig för registeransvariga)
4. En XML-fil genereras med all närvarodata för terminen

DAK-filen innehåller:

- Information om föreningen (kårens namn och organisationsnummer)
- Varje sammankomst med datum och närvarande deltagare
- Deltagarnas personnummer och kön

## Bidragsberäkning

Aktivitetsbidraget beräknas baserat på antal deltagare per sammankomst, uppdelat på kön:

| Kön | Belopp (2026, Göteborg) |
|---|---|
| Flickor/kvinnor | 9,89 kr per deltagare och sammankomst |
| Pojkar/män | 8,02 kr per deltagare och sammankomst |

> **OBS:** Bidragsnivåerna varierar mellan kommuner och kan ändras mellan år.

## DAK-analys

Skojjt har ett inbyggt verktyg för att **analysera DAK-filer**. Nås via menyn **DAK-analys**. Här kan du:

- Ladda upp och **validera** en DAK XML-fil
- Jämföra **två filer** för att se skillnader
- Beräkna **förväntat bidrag** baserat på aktuella bidragsnivåer

## Krav för DAK

För att DAK-exporten ska fungera korrekt behöver ni:

- Ha registrerat **närvaro** för alla sammankomster
- Ha **personnummer** registrerade för alla deltagare
- Ha angett kårens **organisationsnummer** i kårinställningarna

## DAK-import

Med DAK-import kan du föra över **möten och närvaro** från en DAK-fil till en avdelning och termin i Skojjt. Det är användbart om ni har data i gamla Skojjt eller i ett annat närvarosystem som kan exportera DAK.

Importen är **inkrementell** – den lägger till möten som saknas och rör inte befintliga möten om de är identiska. Skiljer sig ett möte får du välja vilken version som gäller.

### Var hittar jag importen?

1. Gå till kårens sida och välj **termin**
2. Under **Snabbåtgärder** finns panelen **DAK-import**
3. Klicka på **Importera DAK-fil**

Importen är kopplad till den valda terminen. Endast **registeransvariga** (medlemsregistrerare) har åtkomst.

### Så här importerar du

1. Välj **avdelning** som mötena ska importeras till (terminen är redan vald)
2. Ladda upp DAK-filen (XML)
3. Granska **förhandsgranskningen**:
   - **Nya möten** – möten som läggs till
   - **Oförändrade** – möten som redan finns och är identiska (ingen åtgärd)
   - **Konflikter** – möten som finns men skiljer sig; välj *Behåll data i Skojjt* eller *Använd data från DAK-filen* per möte
   - **Överhoppade personer** – deltagare i filen som inte finns som medlemmar i Skojjt (hoppas över)
4. Klicka på **Importera**

### Kontroll av datum mot termin

Mötesdatum kontrolleras mot den valda terminens datumintervall (VT = 1 januari–30 juni, HT = 1 juli–31 december). Möten som ligger **utanför terminen** importeras inte och listas separat under *Möten utanför terminen*. Ser du sådana möten har du troligen valt fel termin – kontrollera och välj rätt termin.

### Bra att veta

- **Export → import är en no-op:** exporterar du en avdelnings DAK-fil och importerar tillbaka den till samma avdelning och termin sker ingen förändring.
- **Deltagare matchas** på medlemsnummer. Personer som inte finns som medlemmar i kåren hoppas över (importera medlemmar från Scoutnet först vid behov).
- **Lokal** och **utflykt/hajk** bevaras i importen. Fältet *Lokal* läses per sammankomst, och utflyktsmöten känns igen via en `#hike`-markering i aktivitetsnamnet.
- Importen **skriver aldrig över** befintliga möten utan att du valt det i en konflikt.
