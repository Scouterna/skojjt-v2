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

> **Viktigt:** Skojjt skickar aldrig data tillbaka till Scoutnet. Ändringar i patruller och avdelningstillhörighet bör göras i Scoutnet och sedan importeras till Skojjt.

### API-nyckel för import

För att Scoutnet-importen ska fungera behöver kåren ha en giltig API-nyckel konfigurerad. Nyckeln hämtas från Scoutnet:

1. Logga in i **Scoutnet** och gå till kårens administrationssida
2. Klicka på fliken **Webbkoppling**
3. Under **API-nycklar och endpoints**, leta efter **"Get a detailed csv/xls/json list of all members"**
4. Klicka på **Generera nyckel** om det saknas en nyckel
5. Kopiera nyckeln och klistra in den i **Kårinställningar** → **Scoutnet API-nycklar** i Skojjt

Se [Scoutkårer & avdelningar](/hjalp/02-scoutkar-avdelningar) för mer detaljer om hur du sätter upp en ny kår.

## Manuellt tillagda medlemmar

Om en person inte finns i Scoutnet kan du lägga till dem manuellt via **Lägg till medlem** (tillgänglig för registeransvariga).

## Borttagna medlemmar

När en medlem tas bort från Scoutnet och ni gör en ny import:

- Medlemmen **tas inte bort** från Skojjt automatiskt
- Medlemmen markeras som **borttagen/inaktiv**
- Historisk närvarodata bevaras för rapportering

Du kan manuellt ta bort en medlem från en avdelning om personen inte längre ska vara där.

## Alla medlemmar

Registeransvariga kan se en lista över **alla medlemmar** i kåren, oavsett avdelningstillhörighet. Denna vy nås från kårens meny.

## Personnummer

Personnummer används för att identifiera medlemmar och för att bestämma kön vid DAK-rapportering:


Detta påverkar bidragsberäkningen i DAK-exporten.
