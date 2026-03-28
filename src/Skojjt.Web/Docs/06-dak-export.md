# DAK-export

DAK (Digitalt Aktivitetskort) är det format som används för att rapportera aktiviteter till kommunen för att söka **aktivitetsbidrag**.

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
