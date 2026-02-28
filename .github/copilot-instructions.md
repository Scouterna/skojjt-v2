# Copilot Instructions

## General Guidelines
- Web app should be easy to use on mobile phone
- We should only display personal data that you are authenticated for.

## Code Style
- Use specific formatting rules
- Follow naming conventions

## Project-Specific Rules
- Custom requirement A
- Custom requirement B

## DAK Importer Specifics
- The Sammankomst `kod` attribute must be a unique key for the meeting that fits into DAK XML schema defining it as a string (minLength=3, maxLength=50). It does not have to be an int32 anymore.
- For 2026, Göteborgs kommun aktivitetsbidrag rates are: Flickor/kvinnor = 9,89 kr per deltagare och sammankomst, Pojkar/män = 8,02 kr per deltagare och sammankomst.