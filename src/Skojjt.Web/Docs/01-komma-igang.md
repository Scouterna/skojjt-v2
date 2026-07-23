# Komma igång

Välkommen till Skojjt! Här beskriver vi hur du kommer igång med systemet.

## Vad är Skojjt?

Skojjt är ett närvarohanteringssystem för svenska scoutkårer. Med Skojjt kan du:

- Föra **närvarokort** för avdelningarnas sammankomster
- **Importera medlemsdata** direkt från Scoutnet
- Hantera **märken** och spåra individuella framsteg
- **Exportera DAK-filer** för kommunala aktivitetsbidrag
- Se **sammanställningar** och rapporter per termin

## Instruktionsvideo

Se vår instruktionsvideo på YouTube för en snabb genomgång av hur Skojjt fungerar:

[▶ Se instruktionsvideo på YouTube](https://youtu.be/lH4_G_nK4f8)

## Logga in

Skojjt använder **ScoutID** för inloggning – samma inloggning som du använder för Scoutnet och andra scoutverktyg. Du behöver alltså inte skapa ett separat konto.

1. Gå till Skojjts startsida
2. Klicka på **Logga in med ScoutID**
3. Logga in med ditt ScoutID-konto
4. Du skickas tillbaka till Skojjt och ser de scoutkårer du har tillgång till

> **Tips:** Din behörighet i Skojjt styrs av dina roller i Scoutnet. Om du saknar tillgång, kontrollera att du har rätt roll i Scoutnet.

## Vem kan använda Skojjt?

Du måste ha en roll i Scoutnet för att kunna använda Skojjt:

- **Avdelningsledare** – du måste vara tillagd som ledare (funktionär) på en avdelning i Scoutnet. Gå till avdelningen i Scoutnet och fliken **Funktionärer** för att lägga till ledare. Du ser då bara den avdelningen i Skojjt.
- **Medlemsregistrerare** – rollen "Medlemsregistrerare" i Scoutnet ger dig tillgång till **hela scoutkåren** i Skojjt. Då kan du importera från Scoutnet, hantera alla avdelningar och ändra kårens inställningar.

## Lägga till en ny scoutkår

För att lägga till en ny scoutkår i Skojjt behöver du vara **Medlemsregistrerare** i kåren i Scoutnet. Se [Scoutkårer & avdelningar](/hjalp/02-scoutkar-avdelningar) för en steg-för-steg-guide.

## Första stegen

När du har loggat in:

1. **Välj din scoutkår** i listan över scoutkårer
2. **Välj termin** – varje termin (VT/HT) hanteras separat
3. **Välj avdelning** – du ser de avdelningar du har behörighet till
4. Börja föra **närvaro** på sammankomster!

## Har du använt en egen instans av Skojjt v1?

En del avancerade användare har kört en **egen instans av Skojjt v1** på Google App Engine. Om du vill ta med dig din gamla data (terminer, avdelningar, medlemmar, sammankomster och närvaro) in i Skojjt v2 kan den migreras över. Migreringen görs i två steg: först **exporterar** du datan från den gamla App Engine-databasen (Google Cloud Datastore), sedan **importeras** den till Skojjt v2. Import kräver **Skojjt-adminbehörighet**.

> **Obs:** Detta är enbart för dig som driftat en egen Skojjt v1-instans. Har du bara använt en delad/gemensam Skojjt behöver du inte göra något – din data hämtas i stället från Scoutnet.

### 1. Exportera den gamla datan från Skojjt v1

Exporten hämtar datan ur Google Cloud Datastore och omvandlar den till JSON-filer som Skojjt v2 kan läsa in. Du behöver [Google Cloud SDK (`gcloud`)](https://cloud.google.com/sdk) och Python installerat, samt läsbehörighet till det gamla projektet.

Kör följande från katalogen `scripts/migration` i Skojjt v2-repot (byt ut `skojjt` mot ditt eget App Engine-projekt-id):

```bash
pip install google-cloud-datastore protobuf
gcloud auth application-default login
python export_live.py --project <ditt-projekt-id> --output-dir ./raw_export
python transform_data.py --input-dir ./raw_export --output-dir ./json_export
```

Resultatet blir en mapp `json_export/` med JSON-filer. En mer detaljerad beskrivning (inklusive alternativ export via Datastore-managed export) finns i `scripts/migration/README.md` i repot.

### 2. Importera datan till Skojjt v2

Importen körs mot ett Skojjt v2-API och kräver att du är inloggad som **Skojjt-admin**. Har du inte adminbehörighet själv kan du skicka `json_export/`-filerna till en annan Skojjt-admin som kör importen åt dig.

1. Se till att Skojjt v2 kör och att databasen är migrerad.
2. Lägg `json_export/`-mappen på en plats som servern kommer åt.
3. Anropa importendpointen som admin:

```bash
curl -N -X POST "http://localhost:5286/api/v1/admin/migrate" \
  -H "Content-Type: application/json" \
  -d '{"importDirectory":"C:/sökväg/till/json_export"}'
```

Endpointen strömmar förloppet steg för steg, så du ser varje importsteg allt eftersom det blir klart. Om `importDirectory` utelämnas används `scripts/migration/json_export` relativt lösningens rot.

> **Tips:** Vill du inte köra importen själv – kontakta en Skojjt-admin, bifoga dina exporterade JSON-filer och be dem köra importsteget ovan.

## Behöver du hjälp?

Om du har frågor eller stöter på problem kan du:

- Läsa vidare i de övriga hjälpsidorna
- Rapportera buggar på [GitHub](https://github.com/Scouterna/skojjt-v2/issues)
