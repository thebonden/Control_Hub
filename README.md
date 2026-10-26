# Control Hub · 0.0.001

**Dansk** · [English](README.en.md)

Control Hub er en lokal kontrolcentral til præsentationer, produktion og liveafvikling. Første prioritet er **Stream Deck → PowerPoint og OBS Studio**. Derefter følger **AndroidRemote**.

Formålet er at give operatøren tydelige knapper, forudsigelige kommandoer og ærlig status, også når et program ikke er tilsluttet. Kontrol sker via programmernes grænseflader; ingen funktion afhænger af, hvilket vindue der har fokus.

> **Udviklingsversion, ikke en release.** 0.0.001 indeholder en kørbar .NET 10-kerne, konkrete PowerPoint- og OBS-adaptere og et Stream Deck-plugin. Automatiske tests erstatter ikke afprøvning med fysisk Stream Deck, Office og OBS. Se [testplanen](docs/testing.da.md) og PR'ens buildresultater.

## Dokumentation

- [Dybdegående projektbeskrivelse](docs/project.da.md) · [English](docs/project.en.md)
- [Installation og betjening](docs/setup.da.md) · [English](docs/setup.en.md)
- [Kommando- og statusprotokol](docs/protocol.da.md) · [English](docs/protocol.en.md)
- [Testplan og acceptkriterier](docs/testing.da.md) · [English](docs/testing.en.md)
- [Versionsregler / Version policy](docs/versioning.md)
- [Bidrag / Contributing](CONTRIBUTING.md)
- [Ændringslog / Changelog](CHANGELOG.md)

## Hvad findes faktisk?

| Område | Implementeret i 0.0.001 | Begrænsning |
|---|---|---|
| C#/.NET 10 | Lokal HTTP-host, validering, pluginregistrering, status, timeouts og begrænset dubletbeskyttelse | Ingen desktop-GUI, serviceinstallation eller vedvarende kø |
| PowerPoint COM | Næste, forrige, gå til slide, sort skærm, genoptag og status via en STA-tråd | Windows; præcis ét eksisterende slideshow; endnu ikke livegodkendt |
| OBS WebSocket v5 | Godkendelse, scenevalg, start/stop optagelse og udsendelse, eksplicit lyd fra/til, status | Kun lokal OBS; ny forbindelse pr. forespørgsel; endnu ikke livegodkendt |
| Stream Deck | 12 handlinger, danske/engelske indstillinger, resultatfeedback og statusknap | Kildeplugin til manuel udviklingsinstallation; endnu ikke hardwaregodkendt |
| AndroidRemote | Plugin-id, statuspladsholder og integrationsplan | Ingen Android-app eller parring endnu |
| Lyd, Logitech, Android Live, fysisk fjernkontrol, Flux Keyboard | Mapper, integrationsplaner og statuspladsholdere | Ingen fungerende enhedsdrivere endnu |

OBS-lydstyring gælder navngivne OBS-lydkilder. Det separate lydplugin til operativsystemets enheder er fortsat planlagt. Stream Deck kører som en ekstern klient under `Plugins/StreamDeck/`; det optræder derfor ikke som en hardwareforbundet C#-pluginstatus.

## Hurtig start

Krav: .NET 10 SDK; til den prioriterede kæde Windows, PowerPoint desktop, OBS med WebSocket v5 og Stream Deck 7.4+.

```powershell
dotnet restore ControlHub.slnx
dotnet build ControlHub.slnx --configuration Release --no-restore
dotnet test ControlHub.slnx --configuration Release --no-build
node --test tests/streamdeck.test.cjs tests/inspector.test.cjs
```

Opret et tilfældigt lokalt token og start værten som vist i [installationsvejledningen](docs/setup.da.md). Åbn selv præsentationen og start slideshowet. Aktivér OBS WebSocket og angiv adgangskoden lokalt. Installér derefter Stream Deck-pluginet og konfigurér knapperne.

## Grundregler

- Alle pluginimplementationer og pluginaktiver ligger under `Plugins/`.
- Control Hub må aldrig ændre præsentationsfiler. Ingen åbning, gemning, indholdsredigering eller lukning af filer via PowerPoint-adapteren.
- Ingen tastatursimulering eller fokusafhængige genveje.
- Kommandoer sendes kun efter en konkret handling; fejl eller timeout udløser ikke automatisk gentagelse.
- UI-tekster og dokumentation findes på dansk og engelsk. Protokol-id'er og kodeidentifikatorer er stabile engelske navne.
- Udvikling sker på arbejdsbranch med PR mod `main`. Ingen release eller tag uden ejerens udtrykkelige godkendelse.

## Kildestruktur

```text
src/ControlHub.Contracts/  fælles protokol og plugininterface
src/ControlHub.Core/       routing, validering og dubletbeskyttelse
src/ControlHub.Host/       lokal HTTP-host og eksplicit pluginregistrering
Plugins/                  alle integrationer og Stream Deck-kildeaktiver
tests/                    C#-, WebSocket-, Stream Deck- og hosttests
docs/                     dansk og engelsk dokumentation
scripts/                  kontrol af projektregler
```
