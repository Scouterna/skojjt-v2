# Changelog

## [2.0.1]
* Användardocumentation tillagd. Se [Hjälp](/hjalp/01-komma-igang) för mer information.

## [2.0.0]
* Första releasen av v2. Detta är en omskrivning av skojjt v1. 
  Skojjt v1 var skriven i Python som kördes på Google App Engine. 
  Det här projektet är i C# med Blazor-sidor. Databasen är Postgres.
  Hosting är på Azure.
  Många begränsningar i v1 är borttagna, t.ex. att en ledare bara kunde föra närvaro i en scoutkår.
  Inloggning sker via ScoutID, du direkt får tillgång till dina scoutkårer där du är ledare.
  Flera av de gamla funktionerna är inte implementerade ännu.
