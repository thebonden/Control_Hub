# Installation og betjening

[English](setup.en.md) · [Forside](../README.md)

## Forudsætninger

- Windows 10/11 til PowerPoint COM; .NET 10 SDK til build.
- PowerPoint desktop installeret. PowerPoint i browseren understøttes ikke.
- OBS Studio med WebSocket v5 aktiveret. OBS og Control Hub kører på samme maskine.
- Stream Deck 7.4 eller nyere, som indeholder Node 24-runtime til pluginet.
- Node 24 til JavaScript-tests. Ingen npm-afhængigheder er nødvendige.
- PowerPoint, Stream Deck og Control Hub bør køre som samme Windows-bruger og på samme rettighedsniveau.

## Byg og test

Kør fra repositoryets rod:

```powershell
dotnet restore ControlHub.slnx
dotnet build ControlHub.slnx -c Release --no-restore
dotnet test ControlHub.slnx -c Release --no-build
node --test tests/streamdeck.test.cjs
node scripts/check-policy.cjs
```

`node tests/host-smoke.cjs` starter en kortvarig testhost på port 5187. Stop din normale host først. Den bruger et tilfældigt testtoken, kontakter ikke produktions-OBS og sender ingen slideshow-kommandoer, men læser PowerPoint-status.

## Lokal konfiguration

I PowerShell 7 kan et token dannes uden at gemme det i en fil:

```powershell
$env:CONTROLHUB_TOKEN = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$env:CONTROLHUB_OBS_URL = 'ws://127.0.0.1:4455'
$secret = Read-Host 'OBS WebSocket-adgangskode' -AsSecureString
$env:CONTROLHUB_OBS_PASSWORD = [System.Net.NetworkCredential]::new('', $secret).Password
dotnet run --project src/ControlHub.Host --configuration Release --no-build
```

OBS WebSocket aktiveres under OBS' indstillinger for WebSocket-serveren. Kopiér det konfigurerede portnummer til URL'en. Angiv den adgangskode, der er indstillet i OBS; brug ikke din streamingnøgle. Værten accepterer kun en lokal `ws://`-adresse uden sti eller brugeroplysninger.

API-adressen er fast `http://127.0.0.1:5187`. Hostens token skal kopieres privat til hver Stream Deck-knap. For at hente det kan du bruge `$env:CONTROLHUB_TOKEN` i det samme terminalvindue før start; del aldrig outputtet. Tokenet er ikke permanent gemt af denne vejledning. Ved nyt token skal knappernes indstillinger opdateres. Adgangskoder og tokens må ikke lægges i repositoryet.

## Installér Stream Deck-kildepluginet

1. Afslut Stream Deck.
2. Kopiér hele mappen `Plugins/StreamDeck/dk.thebonden.controlhub.sdPlugin` til `%APPDATA%\Elgato\StreamDeck\Plugins\`. Bevar mappenavnet og alle filer.
3. Start Stream Deck igen. Gruppen **Control Hub** skal nu være synlig.
4. Træk **Næste slide / Next slide**, **Forrige slide / Previous slide**, **Skift scene / Switch scene** og **Status / Status** til din profil.
5. Vælg Dansk eller English, indsæt det lokale token, udfyld eventuelle argumenter og tryk Gem/Save på hver knap.
6. OBS-scener og lydkilder skal angives med deres præcise navne. Gå til slide kræver et positivt heltal. Lyd fra/til angiver ønsket tilstand; det er ikke en blind toggle.

Dette er en manuel udviklingsinstallation fra kildekode. Der er ikke udgivet en Marketplace-pakke, installer, release eller tag.

## Klargør PowerPoint og OBS

Åbn selv præsentationen, og start præcis ét slideshow i den tilknyttede PowerPoint-instans. Control Hub åbner eller gemmer ikke filer. Kontrollér statusknappen, før du trykker på navigation. Prøv først på et kontrolleret testshow.

Start OBS, aktivér WebSocket-serveren, og kontrollér navnene på dine scener og lydkilder. Start udsendelse sender en reel StartStream-anmodning med OBS' eksisterende konfiguration; brug kun knappen, når du faktisk vil sende live. Optagelse og udsendelse har separate start- og stopknapper.

## Feedback og fejlfinding

| Observation | Betydning og handling |
|---|---|
| Grøn bekræftelse | Programmet har bekræftet kommandoen. Det er ikke en videobekræftelse af det synlige resultat. |
| Tjek status / Check status | Kommandoen fejlede, eller udfaldet er ukendt. Kontrollér programmet og status før nyt tryk. |
| PPT ? | PowerPoint er utilgængelig, eller der findes ikke præcis ét slideshow. |
| OPT/REC ? eller LIVE ? | OBS-status kunne ikke læses. Kontrollér server, port og adgangskode. |
| 401 | Token mangler eller er forkert. |
| 403 | Kaldet har en browser-Origin. Brug Stream Deck-pluginets Node-klient eller en direkte API-klient. |
| 429 | Hosten har otte aktive forespørgsler. Vent, og kontrollér status. Gentag ikke en handling ukritisk. |
| Pluginet mangler | Kontrollér mappe, manifest, Stream Deck-version og genstart. |
| COM optaget/afvist | Luk dialoger i PowerPoint, kontrollér brugersession og rettighedsniveau. En ny kommando kræver operatørens vurdering. |

Status opdateres cirka hvert tredje sekund, når forbindelserne svarer hurtigt. Ingen statusopdatering eller fejl udløser en ny produktionskommando automatisk.

## Stop

Stop Control Hub med Ctrl+C i dens terminal. Pluginet må ikke afslutte PowerPoint eller OBS. Fjern eventuelt det lokale token fra Stream Deck-indstillinger, før en profil eksporteres.
