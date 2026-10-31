# Testplan og accept

[English](testing.en.md)

## Automatiske kontroller

- Windows-pakketest: inkluderet runtime, DPAPI-konfiguration, ødelagt konfiguration, lokal adressevalidering, start af den pakkede host, autentificeret status, optaget port og stop via lukket inputforbindelse. Kør `scripts/verify-package.ps1` efter pakning; den afprøver ikke fysisk styring.

- Release-build af alle C#-projekter med advarsler som fejl.
- Routing, UUID, protokolversion, ukendte handlinger og ugyldige argumenter.
- Parallelle dubletter udføres én gang inden for cachevinduet; konflikt-id'er afvises.
- Timeout og annullering skelner mellem ikke afsendt og ukendt udfald.
- PowerPoint-adapterens kald afprøves mod en falsk session. Åbning, gemning, redigering og lukning er ikke handlinger.
- OBS testes mod en lokal simuleret WebSocket-server: håndtryk, password challenge, fragmenterede svar, mellemliggende events, requestfejl og forkert korrelation.
- Elgatos officielle CLI 1.10.3 validerer pluginmanifest og aktiver, herunder hovedikonets PNG-format. Validering er ikke en fysisk installationstest.
- Forbindelsestesten kontrolleres for kun at sende GET-status og aldrig gemme eller sende tokenet tilbage i et svar. UI-tests dækker korrelation, sprog, timeout og ændring af token under et kald.
- Forsinket succes eller fejl må ikke vises på en nykonfigureret knap eller en genbrugt kontekst.
- Stream Deck testes med simuleret HTTP: knaptryk, argumentmapping, overlap, fejlfeedback, manglende token og status.
- Host-smoketest: adgang, Origin-afvisning, ugyldig JSON, version, handlinger og utilgængelige integrationer.
- Kildekoderegler: ingen præsentationsfiler, ingen kendte filskrivnings- eller fokusoperationer i PowerPoint-adapteren.

CI kører på Windows og Linux. Testene ændrer ikke præsentationsfiler og sender ikke produktionskommandoer til brugerens OBS. En grøn CI-kørsel beviser ikke, at COM, Stream Deck-installation og den konkrete hardware er afprøvet.

## Fysisk accept — endnu ikke gennemført

| Trin | Forventet resultat |
|---|---|
| Installér kildepluginet i Stream Deck | Alle 12 handlinger vises; begge sprog kan vælges |
| Start host med forkert/manglende token i knappen | Ingen handling; tydelig fejl |
| Start ét slideshow og en testopsætning i OBS | Status viser korrekt position og outputtilstand |
| Flyt fokus til et andet program | Næste/forrige og OBS-scenevalg virker stadig |
| Næste/forrige med animationer | PowerPoints native adfærd, én handling pr. bevidst tryk |
| Gå til gyldig/ugyldig slide | Gyldig navigation; ugyldig afvises uden ændring |
| Sort skærm og genoptag | Kun slideshowvisning ændres |
| Første/sidste slide og brugerdefineret show | Adfærd dokumenteret og accepteret |
| To slideshowvinduer | Adapteren afviser tvetydig styring |
| Forkert OBS-adgangskode eller scenenavn | Ingen falsk succes; brugbar fejlfeedback |
| Start/stop testoptagelse og mute/unmute | OBS bekræfter den ønskede tilstand |
| Start/stop udsendelse til godkendt testdestination | Kun ved eksplicit, aftalt live-test |
| Luk/genstart OBS eller PowerPoint | Status bliver utilgængelig og kan genfindes; ingen automatisk genafspilning |
| Afbryd under kommando | Ukendt udfald behandles ved statuskontrol |
| Sammenlign præsentationsfilens SHA-256 før/efter | Identisk filindhold |
| Stop host | PowerPoint og OBS forbliver åbne |

Beregn filhash med `Get-FileHash -Algorithm SHA256 -LiteralPath '<eksisterende fil>'` før og efter. Testen må ikke gemme præsentationen eller bruge redigering som del af testen. Brug ikke produktionsudsendelser til en uvarslet test.

## Registrering

For hvert testforløb registreres dato, Windows/Office/OBS/Stream Deck-versioner, hardwaremodel, plugincommit, testtrin, forventet og faktisk resultat, filhash og operatørens godkendelse. Del ikke tokens, OBS-password eller streamingnøgler.

0.0.001 kan først betegnes hardwareverificeret, når denne protokol er gennemført og resultaterne er vedlagt. 0.1.000 kræver derudover ejerens godkendelse af det fulde scope og dokumentation fra det aktive miljø.

## Lokal integrationsstatus — 31. oktober 2026

Stream Deck har tidligere indlæst alle 12 handlinger. En konfigureret statusknap modtog autentificeret status fra Control Hub, mens PowerPoint og OBS blev vist som utilgængelige. Dette bekræfter statusforbindelsen, men ikke fysisk knaptryk eller styring af programmerne.

Afprøvningen afslørede, at indstillingsvinduets registrerings-id og knappens id er forskellige. Forbindelsestesten sendte korrekt, men ignorerede svaret til knappens id. Rettelsen bevarer afsender-id’et og accepterer svaret til den tilknyttede knap. En regressionstest dækker hele forløbet gennem pluginets bro med simuleret Stream Deck-routing. 25 JavaScript-tests består. Den rettede dialog skal stadig afprøves i Stream Deck.

Ved genoptagelse den 31. oktober var Stream Deck, OBS, Control Hub og PowerPoint ikke åbne. Automatisk åbning via appstyringen fejlede. Der er derfor ikke registreret en ny praktisk PowerPoint-/OBS-test eller en gennemført hardwareaccept. Ingen præsentationsfiler er ændret, og ingen udsendelse eller optagelse er startet.
