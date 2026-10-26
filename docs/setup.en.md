# Setup and operation

[Dansk](setup.da.md) · [Home](../README.en.md)

## Prerequisites

- Windows 10/11 for PowerPoint COM; .NET 10 SDK for building.
- Installed desktop PowerPoint. Browser-based PowerPoint is not supported.
- OBS Studio with WebSocket v5 enabled. OBS and Control Hub run on the same machine.
- Stream Deck 7.4 or later, including the plugin's Node 24 runtime.
- Node 24 for JavaScript tests. No npm dependencies are required.
- Run PowerPoint, Stream Deck and Control Hub as the same Windows user at the same privilege level.

## Build and test

From the repository root:

```powershell
dotnet restore ControlHub.slnx
dotnet build ControlHub.slnx -c Release --no-restore
dotnet test ControlHub.slnx -c Release --no-build
node --test tests/streamdeck.test.cjs tests/inspector.test.cjs
node scripts/check-policy.cjs
```

`node tests/host-smoke.cjs` starts a temporary test host on port 5187. Stop your normal host first. It uses a random test token, does not contact production OBS and sends no slideshow commands, but reads PowerPoint status.

## Local configuration

PowerShell 7 can generate a token without saving it to a file:

```powershell
$env:CONTROLHUB_TOKEN = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:CONTROLHUB_OBS_URL = 'ws://127.0.0.1:4455'
$secret = Read-Host 'OBS WebSocket password' -AsSecureString
$env:CONTROLHUB_OBS_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
dotnet run --project src/ControlHub.Host --configuration Release --no-build
```

Enable the OBS WebSocket server in OBS' WebSocket server settings. Copy the configured port into the URL. Supply the password configured in OBS, not your streaming key. The host accepts only a local `ws://` endpoint without a path or user information.

The API address is fixed at `http://127.0.0.1:5187`. Privately copy the host token to each Stream Deck key. You can retrieve it with `$env:CONTROLHUB_TOKEN` in the same terminal before starting the host; never share the output. This guide does not persist the token. When it changes, update the keys. Do not commit passwords or tokens.

## Install the Stream Deck source plugin

1. Quit Stream Deck.
2. Copy the entire `Plugins/StreamDeck/dk.thebonden.controlhub.sdPlugin` folder into `%APPDATA%\Elgato\StreamDeck\Plugins\`. Preserve its folder name and all files.
3. Restart Stream Deck. The **Control Hub** category should appear.
4. Drag **Næste slide / Next slide**, **Forrige slide / Previous slide**, **Skift scene / Switch scene** and **Status / Status** onto your profile.
5. Choose Dansk or English, enter the local token and any required arguments, and click Gem/Save for every key.
6. Click **Test connection**. It uses the entered token without saving it and only reads status. It does not change slides, scenes, recording or streaming. It reports Control Hub, PowerPoint and OBS separately. Click **Save** to retain the token once configured.
7. Enter exact OBS scene and input names. Go-to requires a positive slide number. Mute specifies a desired state rather than blindly toggling.

This is a manual development installation from source. No Marketplace package, installer, release or tag has been published.

## Prepare PowerPoint and OBS

Open the presentation yourself and start exactly one slideshow in the attached PowerPoint instance. Control Hub does not open or save files. Check the status key before navigating. Begin with a controlled test show.

Start OBS, enable its WebSocket server and verify scene and input names. Start streaming sends a real StartStream request using the existing OBS configuration; use it only when you intend to broadcast. Recording and streaming have separate start and stop keys.

## Feedback and troubleshooting

| Observation | Meaning and action |
|---|---|
| Green confirmation | The application acknowledged the command. This is not visual verification of its on-screen result. |
| Tjek status / Check status | The command failed or its outcome is unknown. Check the application and status before pressing again. |
| PPT ? | PowerPoint is unavailable or there is not exactly one slideshow. |
| OPT/REC ? or LIVE ? | OBS status could not be read. Check server, port and password. |
| 401 | Missing or incorrect token. |
| 403 | The request contains a browser Origin. Use the Stream Deck Node client or a direct API client. |
| 429 | Eight requests are active. Wait and check status. Do not blindly replay an action. |
| Missing plugin | Check the folder, manifest and Stream Deck version, then restart. |
| COM busy/rejected | Close PowerPoint dialogs and check the user session and privilege level. Another command requires operator judgment. |

Status normally refreshes approximately every three seconds when connections respond promptly. Status refreshes and errors never automatically issue another production command.

## Stop

Stop Control Hub with Ctrl+C in its terminal. The plugin must not quit PowerPoint or OBS. Remove the local token from Stream Deck settings before exporting a profile.
