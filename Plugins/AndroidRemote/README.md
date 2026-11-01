# AndroidRemote

## Dansk

**Parring og adgangskontrol er implementeret som C#-bibliotek. Telefonapp og transport mangler.** Alt integrationskode ligger under denne mappe. RemoteDeviceRegistry håndterer enkeltbrugsparring, enhedsnøgler, rettigheder, udløb og tilbagekaldelse. RemoteCommandGateway kontrollerer adgang til den fælles dispatcher og status. RemotePermissions accepterer kun kendte PowerPoint- og OBS-handlinger.

Hosten registrerer AndroidRemotePlugin med modenhed development, tom handlingsliste og unavailable / transport_not_configured. Ingen nye HTTP-ruter eller netværksporte. Tests bruger simulerede enheder. Læs [design, test og næste trin](../../docs/androidremote.da.md). AndroidRemote er ejerens valgte nye Control Hub-klient til Android; tredjepartskompatibilitet er ikke implementeret.

## English

**Pairing and authorization are implemented as a C# library. The phone app and transport remain unimplemented.** All integration code lives in this directory. RemoteDeviceRegistry handles single-use pairing, device credentials, permissions, expiry and revocation. RemoteCommandGateway authorizes access to the shared dispatcher and status. RemotePermissions accepts only known PowerPoint and OBS actions.

The host registers AndroidRemotePlugin with development maturity, no actions and unavailable / transport_not_configured. No new HTTP routes or listening ports. Tests use simulated devices. Read the [design, tests and next steps](../../docs/androidremote.en.md). AndroidRemote is the owner-selected new Control Hub client for Android; third-party compatibility is not implemented.
