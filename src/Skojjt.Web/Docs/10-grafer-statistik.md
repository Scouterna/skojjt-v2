# Grafer & statistik

Skojjt erbjuder grafiska sammanställningar av kårens närvarodata över tid. Dessa funktioner nås från kårens meny och är tillgängliga för **registeransvariga**.

## Närvarohistorik

Grafersidan visar ett linjediagram med **närvarohistorik per termin**. Diagrammet visar tre dataserier:

- **Medlemmar** – totalt antal medlemmar per termin
- **Sammankomster** – antal sammankomster per termin
- **Snitt närvaro/möte** – genomsnittligt antal närvarande per sammankomst

> **OBS:** Minst två terminer med data behövs för att diagrammet ska visas.

### Sammanfattningstabell

Under diagrammet finns en tabell med detaljerad statistik per termin:

| Kolumn | Beskrivning |
|---|---|
| Termin | Terminens namn (t.ex. HT 2025) |
| Medlemmar | Totalt antal medlemmar under terminen |
| Sammankomster | Antal sammankomster under terminen |
| Total närvaro | Summan av alla närvaroregistreringar |
| Snitt/möte | Genomsnittligt antal närvarande per sammankomst |

### Så här gör du

1. Navigera till din scoutkår
2. Klicka på **Grafer** i menyn
3. Diagrammet och tabellen visas automatiskt med data från alla tillgängliga terminer

## Personflöde

Personflödet visualiserar hur **medlemmar rör sig mellan avdelningar** från termin till termin med hjälp av ett Sankey-diagram. Det är ett kraftfullt verktyg för att förstå tillväxt, avhopp och övergångar mellan avdelningar.

### Så här gör du

1. Navigera till din scoutkår
2. Klicka på **Grafer** och sedan **Visa personflöde**, eller navigera direkt till **Personflöde** i menyn
3. Välj **minst två terminer** i terminsväljaren
4. Klicka på **Visa flöde**

### Vad visas i diagrammet?

Diagrammet har en kolumn per vald termin. Varje kolumn visar avdelningar som noder, och flöden (band) mellan dem som visar hur medlemmar rört sig:

| Färg | Betydelse |
|---|---|
| **Blå** | Avdelning – nod med antal medlemmar |
| **Grön** | Ny medlem – person som inte fanns föregående termin |
| **Röd** | Slutat – person som inte finns kvar nästkommande termin |
| **Lila** | Prognos – projicerat flöde för nästa termin (valfritt) |

### Sammanfattning

Under diagrammet visas en tabell med per-termin-statistik:

- **Avdelningar** – antal avdelningar med data
- **Medlemmar** – totalt antal aktiva medlemmar
- **Nya** – antal nya medlemmar jämfört med föregående termin
- **Slutat** – antal medlemmar som inte längre är aktiva

### Prognos

Aktivera **"Visa prognos för nästa termin"** för att se en projicering av hur medlemmar förväntas röra sig baserat på ålder och avdelningens åldersgrupp. Prognosen visas i lila i diagrammet.

### Förutsättningar

- Avdelningar måste ha en **åldersgrupp** (avdelningstyp) inställd för att visas i personflödet. Saknas din avdelning? Ställ in åldersgrupp under **Avdelningsinställningar → Åldersgrupp**, eller importera från Scoutnet.
- **Läger** (avdelningar med typ Läger) exkluderas automatiskt från personflödet, eftersom lägerdeltagande inte representerar en avdelningstillhörighet.

