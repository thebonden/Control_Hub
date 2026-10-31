# AndroidRemote / AndroidRemote

## Dansk

Status i 0.0.001: **planlagt, ikke implementeret**. Plugin-id: `androidremote`. Hosten registrerer en fælles `PlannedPlugin` med tom handlingsliste og `not_implemented`-status. Der er ingen driver, transport eller app i denne mappe endnu.

Næste prioritet efter Stream Deck/PowerPoint/OBS. Afklar om navnet betyder en eksisterende app eller en ny klient. Design derefter en mobil knapflade, parring, TLS, enhedsgodkendelse, genkaldelse og status. Genbrug protokol v1; åbn ikke den eksisterende loopback-host på LAN. Ingen automatisk replay efter mobil genforbindelse.

Accept kræver en dokumenteret protokol, fejl- og forbindelsestests, hardwaretest og danske/engelske brugertekster. Implementationskode skal placeres i denne mappe under `Plugins/`.

## English

0.0.001 status: **planned, not implemented**. Plugin ID: `androidremote`. The host registers a shared `PlannedPlugin` with an empty action list and `not_implemented` status. No driver, transport or app exists in this directory yet.

Next priority after Stream Deck/PowerPoint/OBS. Establish whether this means an existing app or a new client. Then design mobile buttons, pairing, TLS, device authorization, revocation and status. Reuse protocol v1; do not expose the existing loopback host on the LAN. Never automatically replay commands after mobile reconnection.

Acceptance requires a documented protocol, error and connection tests, hardware testing and Danish/English user text. Implementation code must be placed in this directory under `Plugins/`.
