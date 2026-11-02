# AndroidRemote

## Dansk

**AndroidRemote består af Android-appen under `Android/`, HTTPS-serveren under `Host/` og Windows-vinduet under `Desktop/`.** Alt integrationskode ligger under denne mappe. RemoteDeviceRegistry håndterer enkeltbrugsparring, enhedsnøgler, rettigheder, udløb og tilbagekaldelse. RemoteCommandGateway kontrollerer adgang til den fælles dispatcher og status. RemotePermissions accepterer kun kendte PowerPoint- og OBS-handlinger.

Vælg telefonnetværket i Windows før hoststart. Når HTTPS-serveren lytter, viser pluginstatus `ready` og antal parrede telefoner. Tests bruger simulerede enheder. Læs [design, test og næste trin](../../docs/androidremote.da.md). AndroidRemote er ejerens valgte nye Control Hub-klient til Android; tredjepartskompatibilitet er ikke implementeret.

## English

**AndroidRemote includes the Android app under `Android/`, the HTTPS server under `Host/` and the Windows dialog under `Desktop/`.** All integration code lives in this directory. RemoteDeviceRegistry handles single-use pairing, device credentials, permissions, expiry and revocation. RemoteCommandGateway authorizes access to the shared dispatcher and status. RemotePermissions accepts only known PowerPoint and OBS actions.

Select the phone network in Windows before starting the host. When HTTPS is listening, the plugin reports `ready` and the paired device count. Tests use simulated devices. Read the [design, tests and next steps](../../docs/androidremote.en.md). AndroidRemote is the owner-selected new Control Hub client for Android; third-party compatibility is not implemented.
