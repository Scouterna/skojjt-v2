# Personuppgifter & GDPR

Skojjt hanterar personuppgifter om scoutmedlemmar, inklusive barn och ungdomar. Här beskriver vi vilka uppgifter som lagras, varifrån de kommer och hur åtkomsten styrs.

> **OBS:** Denna sida beskriver den tekniska hanteringen av personuppgifter i Skojjt. Den ersätter inte er kårs eller Scouternas personuppgiftspolicy. Kontakta er kårs personuppgiftsansvarig eller Scouternas rikskansli vid juridiska frågor om GDPR.

## Vilka personuppgifter lagrar Skojjt?

Skojjt lagrar följande uppgifter om medlemmar:

| Kategori | Uppgifter |
|---|---|
| **Grunduppgifter** | Förnamn, efternamn, födelsedatum |
| **Personnummer** | Svenskt personnummer (används för DAK-export och könsbestämning) |
| **Kontaktuppgifter** | E-post, telefon, mobilnummer |
| **Adress** | Gatuadress, postnummer, postort |
| **Vårdnadshavare** | Namn, e-post och mobilnummer till mamma och pappa |
| **Scoutuppgifter** | Scoutnet-medlemsnummer, avdelningstillhörighet, roll (ledare/deltagare), patrull |
| **Närvaro** | Datum för sammankomster och vilka som var närvarande |
| **Märken** | Framsteg och uppnådda märken per person |

## Varifrån kommer uppgifterna?

Personuppgifterna importeras från **Scoutnet**, Scouternas centrala medlemsregister. Det innebär att:

- Data som finns i Skojjt har sitt ursprung i Scoutnet
- Registeransvariga styr importen – data hämtas bara vid manuell import
- Skojjt kan skicka uppgifter till Scoutnets väntelista om den funktionen är konfigurerad – i övrigt skickas inga personuppgifter tillbaka till Scoutnet

Närvarodata och märkesframsteg skapas direkt i Skojjt av ledare.

## Vem kan se personuppgifterna?

Åtkomsten till personuppgifter styrs av roller i Scoutnet:

- **Avdelningsledare** ser enbart medlemmar i de avdelningar de är funktionärer på
- **Medlemsregistrerare** ser alla medlemmar i kåren
- **Administratörer** har utökad åtkomst men bara när administratörsläget är aktivt

Ingen kan se uppgifter för kårer eller avdelningar de inte har behörighet till. Se [Roller & behörighet](/hjalp/07-roller-behorighet) för mer information.

## Borttagna medlemmar

När en medlem tas bort från Scoutnet och en ny import görs:

- Medlemmen markeras som **borttagen** i Skojjt
- Historisk **närvarodata bevaras** för att kunna rapportera korrekt (t.ex. DAK-export)
- Registeransvariga kan manuellt ta bort en medlem från en avdelning

## Vad används uppgifterna till?

Personuppgifterna i Skojjt används för:

- **Närvarohantering** – föra närvarokort för sammankomster
- **DAK-export** – rapportera till kommunen för aktivitetsbidrag (kräver personnummer och kön)
- **Märkeshantering** – spåra individuella framsteg
- **Kontakt** – kontaktuppgifter och vårdnadshavare visas för ledare

## Var lagras data?

Skojjts server och databas driftas i **Sweden Central** (Azure-region i Sverige). Det innebär att alla personuppgifter lagras inom **EU/EES**, vilket uppfyller GDPR:s krav på datalagring utan behov av särskilda avtal för tredjelandsöverföring.

## Tips för kårer

- **Begränsa behörigheter** – ge bara rollen Medlemsregistrerare till de som behöver det
- **Kontakta Scouterna** vid frågor om den övergripande personuppgiftshanteringen
