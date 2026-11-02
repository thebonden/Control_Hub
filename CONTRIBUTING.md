# Bidrag / Contributing

## Dansk

Arbejd på en branch fra `main`, og åbn en PR mod `main`. Hold alle pluginimplementationer og deres aktiver under `Plugins/`. Bevar danske og engelske dokumenter samt UI-tekster i samme ændring; protokol- og kodeidentifikatorer forbliver engelske.

Kør build, C#-tests, Stream Deck-tests, host-smoketest og kildekoderegler som beskrevet i installationsvejledningen. Beskriv præcist, om en integration er implementeret, simuleret, planlagt eller fysisk afprøvet. Brug ikke grønne enhedstests som bevis for hardwarefunktion.

Ændr aldrig præsentationsfiler, og tilføj ingen fokusafhængige genveje. Gem aldrig tokens eller adgangskoder i kode, eksempler, testlogs eller profiler. Følg [versionsreglerne](docs/versioning.md). Release, tags og .100-grænser kræver ejerens eksplicitte godkendelse.

## English

Work on a branch from `main` and open a PR targeting `main`. Keep all plugin implementations and assets under `Plugins/`. Update Danish and English documentation and UI text together; protocol and code identifiers remain English.

Run the build, C# tests, Stream Deck tests, host smoke test and source policy checks described in setup. State precisely whether an integration is implemented, simulated, planned or physically tested. Green unit tests do not prove hardware functionality.

Never modify presentation files or add focus-dependent shortcuts. Never commit tokens or passwords in code, examples, test logs or profiles. Follow the [version policy](docs/versioning.md). Releases, tags and .100 boundaries require explicit owner approval.
