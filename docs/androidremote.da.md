# AndroidRemote i Control Hub 0.0.001

[English](androidremote.en.md)

AndroidRemote giver operatøren fire handlinger på telefonen. Næste og forrige slide styrer det åbne slideshow. Skift OBS-scene bruger det scenenavn, operatøren indtaster. Status viser slideposition og den aktive OBS-scene eller forklarer, hvis en integration er utilgængelig.

## Installér og par

1. Installér den tilhørende Windows-udviklingspakke og `ControlHub-AndroidRemote-0.0.001.apk` på en telefon med Android 8 eller nyere. Brug Windows og telefon på samme lokale netværk.
2. Før hoststart vælger du **Tillad AndroidRemote på lokalt netværk** og computerens private IPv4-adresse på det netværk, telefonen kan nå. Start Control Hub. Telefonserveren bruger TCP-port 5188. Den lokale desktop- og Stream Deck-host bruger fortsat loopback på port 5187.
3. Åbn **Administrér telefonadgang**. Navngiv telefonen og vælg **Læs status**, **Styr slides** og **Skift OBS-scene** efter behov. Tryk **Opret parringsbevis**. Windows viser det som *Venter* i op til to minutter.
4. Vælg **Gem parringsfil** og overfør filen privat, for eksempel via USB. Vælg **Åbn parringsfil** på telefonen og derefter **Par telefon**. Du kan også indsætte det kopierede bevis. Beviset indeholder en hemmelighed og serverens certifikataftryk. Brug en overførsel, hvor du har kontrol over afsender og modtager. Slet filen efter brug.
5. Start et slideshow manuelt på Windows, og vælg en eksisterende scene i OBS. Telefonen viser knapper efter de valgte rettigheder. Et ukendt udfald kræver, at operatøren kontrollerer status før næste tryk.

**Fjern valgt adgang** i Windows tilbagekalder telefonens adgang. **Glem parring** på telefonen fjerner dens lokale legitimationsoplysninger. Ved hostgenstart oprettes nyt certifikat og nye parringer. Hvis certifikatkontrollen fejler efter genstart, vælg **Glem parring** og brug et nyt bevis.

## Forbindelse og adgang

Telefonserveren har sin egen HTTPS-lytter på én privat adresse. Telefonen kontrollerer serverens IP-navn, certifikatets gyldighed og SHA-256-aftrykket fra beviset før indløsning. Appen bruger direkte forbindelse og afviser omdirigeringer. Der oprettes ingen automatisk firewallregel. Kontrollér adgang på det private netværk, hvis telefonen ikke kan nå computeren.

Beviset har 256 tilfældige bits og kan bruges én gang. Fem fejlforsøg annullerer det. Hosten reserverer højst otte ventende beviser og i alt 16 enhedspladser. Udløb måles med monoton tid. Enhedsnøglen gælder i otte timer. Hosten gemmer kun dens hash i hukommelsen. På telefonen krypteres sessionen med AES-GCM og en nøgle i Android Keystore.

Rettigheder tildeles særskilt til status, slides, scener, optagelse, streaming og OBS-lyd. Den første app har knapper til de første tre kategorier. Statusrettigheden giver adgang til det samlede statusobjekt. Hosten kontrollerer rettigheder før kommandoen og efter ventekøen. En igangsat handling kan stadig afsluttes efter tilbagekaldelse. Kommandosvar knyttes til klientens id og den enkelte parring. Appen genafsender ikke en kommando efter afbrydelse.

Telefonserveren begrænser en forespørgsel til 16 KiB, otte samtidige forespørgsler og 32 forbindelser. De samlede grænser pr. minut er 24 indløsningsforsøg og 240 status-/kommandokald. Ejeradministration foregår via den separate lokale host. [Protokol](protocol.da.md)

## Byg og afprøv

Kildekode til appen og APK-bygning ligger under `Plugins/AndroidRemote/Android/`. Kør `build.py --help` med Python 3. Brug JDK 17 eller nyere, Android SDK Platform 35 og Build Tools 36.0.0. Angiv en udviklingsnøgle uden for repositoryet. Scriptet kompilerer, pakker og verificerer APK-signaturen. Brug samme udviklingsnøgle ved lokale opdateringer. En pakke signeret med en anden nøgle kræver afinstallation og ny parring.

Den automatiske Android 15-prøve installerer APK’en og bruger appens knapper gennem den rigtige TLS-server og PowerPoint-/OBS-adapterne. Kontrollerede testsessioner tæller handlinger. Forkert certifikat, parring, næste/forrige, sceneskift, status, afbrudt forbindelse, genforbindelse og tilbagekaldelse indgår. Prøven på en fysisk telefon med Windows, Office, OBS og det konkrete Wi-Fi-netværk dokumenteres særskilt. [Testvejledning](testing.da.md)
