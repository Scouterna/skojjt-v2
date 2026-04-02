# Changelog

## [2.0.10] - 2026-04-01
* Fixat changelog parsing

## [2.0.9] - 2026-04-01
* Fix för avdelningar med negativt ID (skapade i skojjt v1), url fungerade inte pga svenskt minus tecken, vad det nu är bra för.
* PWA-stöd: manifest, service worker och ikoner för installation som app.
* Fäst genvägar till avdelningar på startsidan.

## [2.0.8] - 2026-03-31
* Närvarostatistik och personflödesgrafer tillagda.
* Personflödesprojektion sprider jämnt över avdelningar.

## [2.0.7] - 2026-03-31
* Ny domän: skojjt.scouterna.net. Omdirigering från gamla domännamn.
* Uppgraderat till MudBlazor 9.2.0 med anpassning till breaking changes.
* CONTRIBUTING.md tillagd.
* Omarbetad versionsvisning med AppVersionHelper.

## [2.0.5] - 2026-03-29
* Borttagen legacy User-entitet och stöd för users-tabellen.

## [2.0.4] - 2026-03-29
* Automatisk versionering via MinVer från git-taggar.
* Centraliserad och uppdaterad versionshantering.

## [2.0.3] - 2026-03-29
* Uppdaterad dokumentation: terminstider, patruller, inloggningshjälp, GDPR-referenser.

## [2.0.2] - 2026-03-28
* Inbyggt dokumentationssystem (hjälpsidor i appen).
* Avdelningsbaserad behörighet - användare ser bara avdelningar de har roll i via ScoutID.
* Stöd för troop-baserade roller i ScoutID claims-transformation.
* Utskrivbar Sensus-närvarorapport och exportknapp.
* Registrering på Scoutnets väntelista via API vid tillägg av ny medlem.
* Datumväljaren startar nu alltid på måndag (sv-SE kultur på alla trådar).
* Märkesframsteg visas på personsidan, förbättrad märkes-UX.
* Patrullstöd vid Scoutnet-import.
* Stöd för flerdagarshajker och lägernattsberäkning i närvarosammanställning.
* Klickbara märkesnamn som länkar till märkesdetaljer.
* Admin-UI och API för import av legacy-data från ZIP-fil.
* API-nyckel-autentisering för admin-endpoints.
* Förbättrad Blazor-återanslutning och felhantering.
* Application Insights-integration och användarspårning.
* Uppdaterade beroenden (EF Core, Npgsql m.fl.).

## [2.0.1]
* Användardokumentation tillagd. Se [Hjälp](/hjalp/01-komma-igang) för mer information.

## [2.0.0] - 2026-02-16
* Första releasen av v2. Detta är en omskrivning av skojjt v1.
  Skojjt v1 var skriven i Python som kördes på Google App Engine.
  Det här projektet är i C# med Blazor-sidor. Databasen är Postgres.
  Hosting är på Azure.
  Många begränsningar i v1 är borttagna, t.ex. att en ledare bara kunde föra närvaro i en scoutkår.
  Inloggning sker via ScoutID, du direkt får tillgång till dina scoutkårer där du är ledare.
  Flera av de gamla funktionerna är inte implementerade ännu.
* SAML 2.0-autentisering via ScoutID.
* DAK-analys (Digitalt Aktivitetskort) för exportering till kommun.
* Mörkt läge med beständig inställning.
* Admin-gränssnitt för databasstatus och hantering.
* Scoutkårs- och avdelningshantering med Scoutnet-import.
* Närvarokort med ledare/deltagare-uppdelning och bulk-åtgärder.
* Märkessystem med bildstöd.
* CSV-export av medlemmar.
* Personnummervalidering med kontrollsiffra.
