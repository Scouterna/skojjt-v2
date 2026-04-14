# Kårinställningar

Kårinställningarna samlar alla konfigurerbara värden för din scoutkår. Sidan nås via kårens meny och är tillgänglig för **registeransvariga**.

## Så här gör du

1. Navigera till din scoutkår
2. Klicka på **Kårinställningar** (kugghjulsikon)

## Grundinformation

| Inställning | Beskrivning |
|---|---|
| **Kårnamn** | Kårens visningsnamn i Skojjt |
| **Organisationsnummer** | Kårens organisationsnummer (XXXXXX-XXXX). Används i DAK-exporten. |
| **Förenings-ID** | Förenings-ID för DAK-export. Hämtas från din kommun. |
| **Kommun** | Kårens kommun. Används för DAK-export och bidragsrapporter. |

## Kontaktuppgifter

| Inställning | Beskrivning |
|---|---|
| **E-post** | Kårens e-postadress |
| **Telefon** | Kårens telefonnummer |
| **Gatuadress** | Kårens besöksadress |
| **Postadress** | Postnummer och postort |
| **Standardplats för läger** | Förvalsplats som visas vid lägerrapporter |
| **Standardplats för möten** | Förvalsplats som visas vid nya möten |

## Ekonomi

| Inställning | Beskrivning |
|---|---|
| **Bankkontonummer** | IBAN eller clearingnummer + kontonummer |
| **Firmatecknare** | Namn på kårens firmatecknare |
| **Firmatecknare telefon** | Firmatecknarens telefonnummer |
| **Firmatecknare e-post** | Firmatecknarens e-postadress |

## Scoutnet API-nycklar

API-nycklar används för att kommunicera med Scoutnet. De hämtas från Scoutnet under **Kåradmin → Webbkopplingar**.

| Nyckel | Scoutnet-namn | Användning |
|---|---|---|
| **Medlemslista** | "Get a detailed list of all members" | Importera medlemmar och jämföra vid Scoutnet synk |
| **Väntelista** | "Register a group member on a waitinglist" | Lägga till nya medlemmar på väntelistan i Scoutnet |
| **Uppdatera medlemskap** | "Update membership" | Skicka tillbaka avdelnings- och patrulländringar till Scoutnet |
| **Projekt** | "View info about projects the group is registered to" | Visa en lista över kårens arrangemang vid lägerimport |

> **Viktigt:** API-nycklar är hemliga och får inte delas. Om en nyckel avslöjas kan du återgenerera den i Scoutnet – men tänk på att alla tjänster som använde den gamla nyckeln slutar fungera omedelbart.

Se [Medlemshantering](/hjalp/04-medlemshantering) för mer information om vilka nycklar som behövs för import och synk.

## Närvaroinställningar

Dessa inställningar påverkar hur Skojjt beräknar bidragsberättigade medlemmar i [Föreningsredovisningen](/hjalp/11-foreningsredovisning) och andra rapporter.

| Inställning | Beskrivning |
|---|---|
| **Minsta antal möten per år** | Antal sammankomster en medlem måste delta i under ett år för att räknas som bidragsberättigad |
| **Minsta antal möten per termin** | Antal sammankomster en medlem måste delta i under en termin för att räknas som bidragsberättigad |
| **Inkludera läger i närvarostatistik** | Om lägerdagar ska räknas in i antalet sammankomster vid bidragsberäkning |

