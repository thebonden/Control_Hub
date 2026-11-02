# Fysisk fjernkontrol / Physical remote

## Dansk

Status i 0.0.001: **planlagt, ikke implementeret**. Plugin-id: `physical-remote`. Hosten registrerer en fælles `PlannedPlugin` med tom handlingsliste og `not_implemented`-status. Der er ingen driver, transport eller app i denne mappe endnu.

Afklar USB, HID, seriel, Bluetooth eller netværk og den konkrete model. Kræv eksplicit enhedsvalg, debounce, tryk/slip-semantik og kontrolleret reconnect. Ingen fokusbaserede tastetryk.

Accept kræver en dokumenteret protokol, fejl- og forbindelsestests, hardwaretest og danske/engelske brugertekster. Implementationskode skal placeres i denne mappe under `Plugins/`.

## English

0.0.001 status: **planned, not implemented**. Plugin ID: `physical-remote`. The host registers a shared `PlannedPlugin` with an empty action list and `not_implemented` status. No driver, transport or app exists in this directory yet.

Establish USB, HID, serial, Bluetooth or network transport and the exact device model. Require explicit device selection, debounce, press/release semantics and controlled reconnect. No focus-based key injection.

Acceptance requires a documented protocol, error and connection tests, hardware testing and Danish/English user text. Implementation code must be placed in this directory under `Plugins/`.
