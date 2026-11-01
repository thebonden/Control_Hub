# AndroidRemote: parring og adgangskontrol

[English](androidremote.en.md)

Første kodetrin er et transportuafhængigt .NET 10-bibliotek under `Plugins/AndroidRemote/`, testet mod den rigtige dispatcher og simulerede enheder. Det er **ikke en installerbar Android-app eller en fungerende telefonforbindelse**.

## Parringsforløb

1. En fremtidig lokal ejergrænseflade kalder `BeginPairing` med et navn på 1–80 tegn uden kontroltegn og en eksplicit rettighedsliste.
2. Biblioteket returnerer et parrings-id, et tilfældigt bevis og udløbstidspunkt. Beviset er 32 kryptografisk tilfældige bytes gengivet som 64 hextegn; det er ikke en kort PIN-kode. Det gælder i to minutter.
3. Den fremtidige telefontransport afleverer id og bevis til `CompletePairing`. Højst ét samtidigt forsøg kan lykkes. Fem forkerte forsøg annullerer parringen; ugyldigt formaterede beviser tæller også.
4. Vellykket parring giver et nyt enheds-id og en uafhængig nøgle, som gælder i otte timer. Navn og rettigheder kommer fra ejerens valg; telefonen kan ikke udvide dem.
5. Kommandoer og status går gennem `RemoteCommandGateway`. Forkerte nøgler, manglende rettigheder, tilbagekaldte og udløbne enheder afvises.

Ejeren kan annullere en parring, tilbagekalde én enhed eller tilbagekalde alle enheder og ventende parringer. Hemmeligheder returneres kun ved oprettelse og må ikke logges. Registeret gemmer SHA-256-hashværdier og sammenligner dem med `CryptographicOperations.FixedTimeEquals`. Enhedslister indeholder ingen nøgler. Dette gælder tilfældige nøgler med stor entropi, ikke brugeradgangskoder.

Der er højst otte ventende parringer og i alt 16 parrede/reserverede enhedspladser. Udløb bruger monoton tid; ændring af Windows-uret forlænger ikke adgangen. Viste UTC-tidspunkter er vejledende, hvis systemuret ændres. Alt findes kun i hukommelsen. Genstart kræver ny parring.

## Rettigheder

| Rettighed | Tillader |
|---|---|
| `status.read` | Hele Control Hub-statusobjektet, inklusive OBS-scenenavn |
| `powerpoint.control` | `next`, `previous`, `goto`, `black`, `resume` |
| `obs.scenes` | `scene.switch` |
| `obs.recording` | `record.start`, `record.stop` |
| `obs.streaming` | `stream.start`, `stream.stop` |
| `obs.audio` | `input.mute` |

Ingen rettigheder tilføjes automatisk. Der findes ingen wildcard-rettighed. Ukendte og dublerede rettigheder afvises. Rettigheder gælder handlingstyper, ikke individuelle scener/lydkilder. Filredigering, vilkårlige COM-kald og fokusafhængige tastetryk er ikke tilladt.

## Kø, tilbagekaldelse og dubletter

Adgang kontrolleres ved modtagelse og efter dispatcherens ventekø, før cachelæsning eller udførelse. Tilbagekaldelse eller udløb under køen afviser dermed stadig ventende kommandoer. Kontrollen efter køen er grænsen for igangsættelse; tilbagekaldelse kan ikke fortryde allerede igangsat arbejde, herunder COM-kald. Status kontrollerer også adgang igen efter indsamling.

Klientens kommando-UUID mappes deterministisk til et internt UUID i parringens eget navnerum. Svaret får klientens oprindelige id tilbage. Samme id fra to parringer eller fra Stream Deck giver ikke adgang til det samme cachesvar. Historikken er stadig den fælles hukommelsescache med de seneste 1.024 afsendte id'er; den er ikke vedvarende replay-beskyttelse. Ny parring giver et nyt navnerum. Telefonen må aldrig automatisk genafspille gamle kommandoer efter genforbindelse eller genstart.

`access_denied` har faste danske/engelske tekster og udfører ingen pluginhandling. Timeout efter igangsat adapterarbejde bevarer `outcome_unknown`. Operatøren skal kontrollere status før en ny handling.

## Før telefonbrug

Der mangler lokal parringsdialog/enhedsliste, en dansk/engelsk Android-klient og særskilt TLS-transport med verificeret serveridentitet, sikker overførsel af parringsbevis/nøgle og sikker lagring på telefonen. Transporten skal begrænse størrelse, samtidighed og forespørgselshastighed; bibliotekets parringsgrænser erstatter ikke dette. Derefter følger transporttests og fysisk Android-test.

Der er ingen nye HTTP-endpoints. Parring og tilbagekaldelse er ejerfunktioner i biblioteket, ikke anonyme telefon-API'er. Det lokale Control Hub-token skal ikke kopieres til telefonen. Hosten lytter fortsat kun på loopback; der åbnes ingen firewall eller LAN-adgang. Pluginstatus er `unavailable` / `transport_not_configured` med tom handlingsliste.

## Test

Tests dækker samtidig enkeltbrugsparring, udløb, ændring af systemur, forkerte beviser, kapacitetsgrænser, uforanderlige rettigheder, handlingsrettigheder, filhandlingsafvisning, adskilt dublethistorik, tilbagekaldelse/udløb under kø, tilbagekaldelse under statuslæsning, korrelation, timeout og ærlig transportstatus. Ingen præsentation ændres, og ingen virkelig optagelse eller streaming startes.
