# Control Hub · 0.0.001

[Dansk](README.md) · **English**

Control Hub is a local control centre for presentations, production and live operation. The first priority is **Stream Deck → PowerPoint and OBS Studio**, followed by **AndroidRemote**.

The goal is to give operators clear buttons, predictable commands and truthful status, including when an application is unavailable. Control uses application interfaces; no feature depends on the focused window.

> **Development version, not a release.** 0.0.001 includes an executable .NET 10 core, concrete PowerPoint and OBS adapters, and a Stream Deck plugin. Automated tests do not replace trials with physical Stream Deck hardware, Office and OBS. See the [test plan](docs/testing.en.md) and the PR's build results.

## Documentation

- [Detailed project description](docs/project.en.md) · [Dansk](docs/project.da.md)
- [Setup and operation](docs/setup.en.md) · [Dansk](docs/setup.da.md)
- [Command and status protocol](docs/protocol.en.md) · [Dansk](docs/protocol.da.md)
- [Testing and acceptance criteria](docs/testing.en.md) · [Dansk](docs/testing.da.md)
- [Version policy / Versionsregler](docs/versioning.md)
- [Contributing / Bidrag](CONTRIBUTING.md)
- [Changelog / Ændringslog](CHANGELOG.md)

## What is actually implemented?

| Area | Implemented in 0.0.001 | Limitation |
|---|---|---|
| C#/.NET 10 | Local HTTP host, validation, plugin registration, status, timeouts and bounded deduplication | No service installer or persistent queue |
| Windows installation | Installer including .NET; Danish/English control window, start/stop, status and encrypted settings | Windows x64; unsigned development package; no automatic updates |
| PowerPoint COM | Next, previous, go to slide, black screen, resume and status on an STA thread | Windows; exactly one existing slideshow; live acceptance pending |
| OBS WebSocket v5 | Authentication, scene selection, start/stop recording and streaming, explicit mute state, status | Local OBS only; new connection per request; live acceptance pending |
| Stream Deck | 12 actions, Danish/English settings, result feedback, status key and installation package | Loading verified locally; complete hardware acceptance pending |
| AndroidRemote | Plugin ID, status placeholder and integration plan | No Android app or pairing yet |
| Audio, Logitech, Android Live, physical remote, Flux Keyboard | Directories, integration plans and status placeholders | No working device drivers yet |

OBS audio control applies to named OBS inputs. The separate operating-system audio plugin remains planned. Stream Deck is an external client under `Plugins/StreamDeck/`; it is not reported as a hardware-connected C# plugin.

## Quick start

Use the Windows installer to start without a terminal or a separate .NET installation. GitHub Actions packages are development artifacts, not releases. Follow the [installation guide](docs/installation.en.html).

Building from source requires .NET 10 SDK; for the priority chain, Windows, desktop PowerPoint, OBS with WebSocket v5, and Stream Deck 7.4+.

```powershell
dotnet restore ControlHub.slnx
dotnet build ControlHub.slnx --configuration Release --no-restore
dotnet test ControlHub.slnx --configuration Release --no-build
node --test tests/streamdeck.test.cjs tests/inspector.test.cjs
```

Generate a random local token and start the host as described in [setup](docs/setup.en.md). Open the presentation and start its slideshow yourself. Enable OBS WebSocket and configure its password locally. Then install the Stream Deck plugin and configure the keys.

## Ground rules

- All plugin implementations and assets belong under `Plugins/`.
- Control Hub must never modify presentation files. The PowerPoint adapter cannot open, save, edit or close presentation files.
- No simulated keyboard input or focus-dependent shortcuts.
- Commands follow explicit actions; failures and timeouts never trigger automatic replay.
- UI text and documentation are available in Danish and English. Protocol IDs and code identifiers remain stable English names.
- Development uses a working branch and PR to `main`. No release or tag without the owner's explicit approval.

## Source layout

```text
src/ControlHub.Contracts/  shared protocol and plugin interface
src/ControlHub.Core/       routing, validation and deduplication
src/ControlHub.Host/       local HTTP host and explicit plugin registration
Plugins/                  all integrations and Stream Deck source assets
tests/                    C#, WebSocket, Stream Deck and host tests
docs/                     Danish and English documentation
scripts/                  project policy checks
```
