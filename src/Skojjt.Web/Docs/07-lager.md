# Läger

Skojjt stödjer lägerhantering genom att importera arrangemang från Scoutnet. Ett läger fungerar som en specialavdelning med en lägerdag per dag i datumintervallet, och deltagare importerade från Scoutnet-projektet.

## Vad är ett läger i Skojjt?

Ett läger är en avdelning med typ **Läger** (till skillnad från vanliga avdelningar). I terminsvyn visas läger med en 🏕️-ikon bredvid namnet och datumintervallet syns direkt på kortet.

Skillnader mot en vanlig avdelning:

| | Vanlig avdelning | Läger |
|---|---|---|
| **Skapas via** | Scoutnet-import | Importera från Scoutnet-arrangemang |
| **Sammankomster** | Skapas manuellt, en i taget | Skapas automatiskt — en per dag |
| **Längd** | Standardlängd (t.ex. 90 min) | Heldag |
| **Personflöde** | Visas i grafen | Exkluderas |
| **Checkin-synk** | Ej tillgängligt | Kan synka tillbaka till Scoutnet |

## Förberedelser i Scoutnet

Innan du kan importera ett läger i Skojjt behöver du förbereda arrangemanget i Scoutnet:

1. **Skapa ett arrangemang** (projekt) i Scoutnet om det inte redan finns
2. **Registrera deltagare** på arrangemanget
3. **Hitta projekt-ID** — det syns i URL:en när du tittar på arrangemanget, t.ex. `/activities/view/1190` → projekt-ID = **1190**
4. **Generera API-nycklar** på projektets API-sida i Scoutnet:
   - **"Get a list of members who are registered on the project"** — krävs för import
   - **"Update the check-in state"** (checkin) — valfritt, behövs bara om du vill synka närvarostatus tillbaka till Scoutnet

> **Tips:** Varje arrangemang i Scoutnet har egna API-nycklar, separata från kårens nycklar. Du hittar dem på arrangemangets API-sida.

### Varför kan inte Skojjt visa en lista över kårens läger?

Scoutnets API har en begränsning som gör att det **inte är möjligt att automatiskt lista kårens arrangemang**.

Därför behöver du **ange projekt-ID och API-nyckel manuellt** vid varje import. Du hittar projekt-ID i URL:en på arrangemangets sida i Scoutnet, och API-nycklarna genereras på arrangemangets API-sida.

## Importera läger från Scoutnet

Du behöver vara **Medlemsregistrerare** för att importera läger.

1. Navigera till din scoutkår och välj rätt **termin**
2. Scrolla ned och öppna panelen **Läger**
3. Klicka på **Importera från Scoutnet**
4. I dialogen som öppnas:

### Steg 1: Ange projekt-ID och API-nyckel

- Ange **Scoutnet projekt-ID** (numret från arrangemangets URL)
- Ange **API-nyckel (get/participants)** — nyckeln för att hämta deltagarlistan
- Ange **API-nyckel (checkin)** *(valfritt)* — om du vill kunna synka närvarostatus tillbaka till Scoutnet
- Klicka på **Hämta deltagare**

### Steg 2: Förhandsgranska och fyll i lägeruppgifter

Skojjt hämtar deltagarlistan och visar en förhandsgranskning:

- Deltagarnas namn visas i listan
- **Avbokade** deltagare markeras med en gul etikett
- Deltagare som **saknas i databasen** markeras med en röd etikett — dessa kan inte importeras förrän de lagts till via [Scoutnet-import](/hjalp/04-scoutnet-import)

Fyll i lägeruppgifterna:

- **Lägernamn** — t.ex. "Sommarläger 2025". Scoutnets API ger inte tillgång till arrangemangets namn, så du behöver fylla i detta själv.
- **Plats** — t.ex. "Vässarö"
- **Startdatum** och **Slutdatum**

Klicka på **Importera** för att skapa lägret.

### Vad händer vid import?

1. En ny **lägeravdelning** skapas för den valda terminen
2. En **lägerdag** (sammankomst) skapas automatiskt för varje dag i datumintervallet
3. Alla deltagare som finns i databasen **kopplas till lägret**
4. Deltagare som saknas i databasen hoppas över — en varning visas med namnen

> **OBS:** Om du importerar samma Scoutnet-arrangemang en andra gång blockeras det för att undvika dubbletter.

## Närvarohantering för läger

När lägret är skapat navigerar du till det precis som en vanlig avdelning. Närvarokortet visar:

- **En kolumn per lägerdag** — varje dag i datumintervallet
- **En rad per deltagare** — alla importerade deltagare och ledare

Registrera närvaro genom att klicka på cellerna, precis som för vanliga sammankomster. Alla dagar visas direkt utan att du behöver skapa dem manuellt.

## Synka checkin till Scoutnet

Om du angav en **checkin-API-nyckel** vid importen kan du synka närvarostatus tillbaka till Scoutnet:

1. Navigera till lägrets närvarosida
2. Öppna panelen **Rapporter & Verktyg**
3. Klicka på **Synka checkin till Scoutnet**
4. Dialogen visar hur många deltagare som har minst en närvarodag
5. Klicka på **Synka** för att skicka statusen

Deltagare med minst en närvarande dag markeras som **incheckade** i Scoutnet. Övriga markeras som **ej incheckade**.

## Läger i rapporter

Lägerdata integreras med Skojjts rapporter:

- **Lägerbidrag** (Göteborg/Stockholm) — lägret kan väljas i lägerbidragsdialogen på terminsvyn
- **DAK-export** — lägerdagar räknas som sammankomster och ingår i DAK-filen
- **Aktivitetsbidrag CSV** — lägerdeltagare räknas in i närvarostatistiken
- **Personflödesgrafen** — läger exkluderas medvetet, eftersom lägerdeltagande inte representerar en avdelningstillhörighet

## Ta bort ett läger

Om du behöver ta bort ett läger:

1. Navigera till lägrets närvarosida
2. Öppna panelen **Rapporter & Verktyg**
3. Klicka på **Standardvärden** (inställningar)
4. Scrolla ned till **Farozon**
5. Klicka på **Ta bort läger**
6. Bekräfta i dialogen

> **Varning:** Att ta bort ett läger raderar alla lägerdagar, närvarodata och deltagarkopplingar permanent. Detta går inte att ångra.
