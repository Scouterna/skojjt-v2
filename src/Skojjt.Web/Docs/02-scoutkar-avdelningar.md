# Scoutkårer & avdelningar

Skojjt organiserar data i tre nivåer: **scoutkår**, **termin** och **avdelning**.

## Scoutkårer

En scoutkår motsvarar en kår i Scoutnet. När du loggar in ser du de kårer du har tillgång till baserat på dina Scoutnet-roller.

Från kårens översiktssida kan du:

- Se vilka terminer som finns
- Navigera till avdelningar
- Gå till kårinställningar (om du är registeransvarig)

## Lägga till en ny scoutkår

För att lägga till en ny scoutkår i Skojjt behöver du vara **Medlemsregistrerare** i kåren i Scoutnet. Sedan behöver du hämta en API-nyckel från Scoutnet.

### Hämta Kår-ID och API-nyckel från Scoutnet

1. Logga in i **Scoutnet** (vanliga Scoutnet, inte ScoutID)
2. Gå till kårens administrationssida
3. Klicka på fliken **Webbkoppling**
4. Om API-åtkomst inte redan är aktiverad behöver du **slå på den** först. Följ instruktionerna på sidan i Scoutnet för att aktivera API-åtkomst för kåren.
5. Längst upp på sidan hittar du **Kår-ID för webbtjänster** – notera detta ID
6. Scrolla ned till sektionen **API-nycklar och endpoints**
7. Leta efter raden med titeln **"Get a detailed csv/xls/json list of all members"**
8. Om det inte redan finns en nyckel, klicka på **Generera nyckel**
9. **Kopiera nyckeln** – du behöver den i nästa steg

> **Viktigt:** API-nycklar är hemliga och får **inte delas, länkas eller läggas till i publik kod**. Om en nyckel avslöjas av misstag kan du återgenerera den på Webbkopplings-sidan i Scoutnet – men tänk på att alla tjänster som använde den gamla nyckeln slutar fungera omedelbart.

### Skapa kåren i Skojjt

1. Logga in i Skojjt
2. Gå till **Scoutkårer** och klicka på **Lägg till ny scoutkår**
3. Ange kårens **Kår-ID** (från Webbkopplings-sidan i Scoutnet) och den **API-nyckel** du kopierade
4. Skojjt hämtar kårens information och importerar medlemsdata

### Valfritt: API-nyckel för väntelista

Det finns även en API-nyckel för att lägga till nya medlemmar på väntelistan i Scoutnet från Skojjt. Denna heter **"Register a group member on a waitinglist"** i Scoutnet. Generera nyckeln på samma sätt och lägg in den i kårens inställningar i Skojjt.

> **Tips:** API-nycklarna kan också läggas till eller ändras i efterhand under **Kårinställningar** i Skojjt.

## Terminer

Varje termin (VT = vårtermin, HT = hösttermin) hanterar sin egen uppsättning av närvarodata. Terminer skapas automatiskt men du kan även skapa nya vid behov.

- **VT** – vårtermin, januari–juni
- **HT** – hösttermin, juli–december

Välj termin för att se avdelningarna och deras sammankomster.

## Avdelningar

Avdelningar importeras normalt från Scoutnet. Varje avdelning har:

- **Namn** – t.ex. "Spårarna", "Upptäckarna"
- **Medlemmar** – deltagare och ledare
- **Sammankomster** – de möten som registreras

### Skapa en lokal avdelning

Ibland behöver du en avdelning som **inte finns i Scoutnet**, till exempel en tillfällig grupp eller en lokal indelning som bara används i Skojjt. **Registeransvariga** kan skapa sådana avdelningar direkt i Skojjt.

1. Gå till terminens översikt för kåren (`/sk/{kår-id}/t/{termin-id}`)
2. Öppna panelen **Skapa avdelning**
3. Klicka på knappen **Skapa avdelning**
4. Fyll i:
   - **Namn** (obligatoriskt)
   - **Avdelningstyp** (åldersgrupp, valfritt – används för sortering i personflödesgrafen)
   - **Mötesplats** (valfritt)
   - **Standardtid** och **Längd (minuter)** som förval för nya sammankomster
5. Klicka på **Skapa avdelning**

Avdelningen får ett **lokalt ID** som är unikt inom kåren och kan sedan hanteras precis som en avdelning importerad från Scoutnet – du lägger till medlemmar, registrerar närvaro och exporterar rapporter på vanligt sätt.

> **Notera:** Lokala avdelningar skapas bara i Skojjt och synkas inte tillbaka till Scoutnet. Medlemmar läggs till manuellt eller flyttas in från andra avdelningar.

> **Notera:** Du kan importera läger och aktiviteter från Scoutnet som en lokal avdelning i Skojjt. Se [Importera läger som avdelning](/hjalp/07-lager) för mer information.

### Avdelningsinställningar

Varje avdelning har en inställningssida som nås via **kugghjulsikonen** på avdelningens närvarosida.

#### Standardvärden för möten

Här ställer du in förvalen som används när nya sammankomster skapas:

| Inställning | Beskrivning |
|---|---|
| **Starttid** | Standardstarttid för nya möten |
| **Längd (minuter)** | Standardlängd i minuter (15–480) |
| **Mötesplats** | Standardplats som fylls i automatiskt |

Dessa värden sparar tid vid skapande av nya sammankomster — du behöver inte fylla i samma information varje gång.

#### Åldersgrupp

Välj avdelningens **avdelningstyp** (åldersgrupp) i listan. Tillgängliga typer:

- Bäverscouter, Spårarscouter, Upptäckarscouter, Äventyrarscouter, Utmanarscouter, Roverscouter, Familjescouter, Annat

Åldersgruppen används för att sortera avdelningar i [personflödesgrafen](/hjalp/10-grafer-statistik). Om din avdelning saknar åldersgrupp visas den inte i personflödet.

> **Tips:** Åldersgruppen importeras normalt från Scoutnet. Om den saknas kan du ställa in den manuellt här.

#### Ta bort avdelning (Farozon)

**Registeransvariga** kan ta bort en avdelning permanent. Detta raderar alla möten, närvarodata och medlemskopplingar och går inte att ångra.

## Navigering

Webbadressen följer mönstret:

- `/sk/{kår-id}` – kårens sida
- `/sk/{kår-id}/t/{termin-id}/{avdelning-id}` – avdelningens närvarosida

Du kan alltid använda brödsmulorna (breadcrumbs) högst upp på sidan för att navigera tillbaka.
