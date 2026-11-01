# Ændringslog / Changelog

## 0.0.001 — udviklingsversion / development version

### Dansk
- AndroidRemote-bibliotek med enkeltbrugsparring, enhedsrettigheder, udløb og tilbagekaldelse. Ingen telefonapp eller transport endnu.
- Adgang kontrolleres igen efter kommandokøen; kommandosvar holdes adskilt pr. parret enhed.
- Host-smoketest bruger en separat lokal port, så den åbne Control Hub kan fortsætte.
- Konkrete forbindelsesårsager for PowerPoint og OBS i status og Stream Deck, med sikre dansk/engelske tekster.
- Afbrudte OBS-kommandoer efter påbegyndt afsendelse rapporteres som ukendt udfald; aldrig automatisk genafsendelse.
- Rettet forbindelsessvar, når Stream Decks indstillingsvindue og knap har forskellige id'er; dækket af en regressionstest.
- Windows x64-installation med inkluderet .NET og officiel Stream Deck-installationspakke.
- Dansk/engelsk kontrolvindue med start/stop, forbindelsesstatus, kopiering af vedvarende token og DPAPI-krypterede OBS-indstillinger.
- Pakketest af konfiguration, lokal host og stop; Windows-artefakter i CI uden release/tag.
- Forbindelsestest i Stream Deck-indstillingerne med separat status for Control Hub, PowerPoint og OBS.
- Beskyttelse mod forsinket knapfeedback efter profil-/indstillingsskift.
- Hovedikon i krævet PNG-format og officiel Elgato-validering i CI.
- C#/.NET 10-kontrakter, dispatcher og lokal autentificeret HTTP-host.
- Eksplicit pluginsystem med ærlig status for ikke-implementerede integrationer.
- PowerPoint COM-navigation på STA-tråd, uden filredigering eller tastaturgenveje.
- OBS WebSocket v5-klient med godkendelse og konkrete handlinger.
- Stream Deck-kildeplugin med 12 handlinger, dansk/engelsk opsætning og statusfeedback.
- Forberedte integrationsplaner for AndroidRemote, lyd, Logitech, Android Live, fysisk fjernkontrol og Flux Keyboard.
- Automatiske tests, CI på Windows/Linux og projektbeskrivelse på begge sprog.
- Fysisk test i det aktive miljø afventer. Ingen release eller tag.

### English
- AndroidRemote library with single-use pairing, device permissions, expiry and revocation. No phone app or transport yet.
- Authorization is rechecked after queueing; command results are isolated per paired device.
- Host smoke tests use a separate local port so the open Control Hub can keep running.
- Specific PowerPoint/OBS connection reasons in status and Stream Deck, with safe Danish/English messages.
- OBS commands interrupted after transmission starts report an unknown outcome; no automatic retries.
- Fixed connection replies when the Stream Deck inspector and key have different IDs; covered by a regression test.
- Windows x64 installer with included .NET and an official Stream Deck installation package.
- Danish/English control window with start/stop, connection status, persistent token copying and DPAPI-encrypted OBS settings.
- Package checks for configuration, local host and shutdown; Windows CI artifacts without a release/tag.
- Connection check in Stream Deck settings with separate Control Hub, PowerPoint and OBS status.
- Protection against late key feedback after profile/settings changes.
- Main icon in the required PNG format and official Elgato validation in CI.
- C#/.NET 10 contracts, dispatcher and authenticated local HTTP host.
- Explicit plugin system with truthful status for unimplemented integrations.
- PowerPoint COM navigation on an STA thread without file editing or keyboard shortcuts.
- OBS WebSocket v5 client with authentication and concrete actions.
- Stream Deck source plugin with 12 actions, Danish/English settings and status feedback.
- Prepared integration plans for AndroidRemote, audio, Logitech, Android Live, physical remote and Flux Keyboard.
- Automated tests, Windows/Linux CI and project description in both languages.
- Physical testing in the active environment remains pending. No release or tag.
