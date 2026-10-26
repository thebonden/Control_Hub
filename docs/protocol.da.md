# Kommando- og statusprotokol v1

[English](protocol.en.md)

Transport: lokal HTTP/JSON på `127.0.0.1:5187`. Alle kald kræver `Authorization: Bearer <lokalt-token>`. Ingen CORS, fjernadgang eller broadcast-discovery.

## Endpoints

| Metode og sti | Indhold |
|---|---|
| GET /api/v1/plugins | Registrerede C#-plugins med id, navn, modenhed og handlinger |
| GET /api/v1/status | Protokolversion, produktversion, observationstid i UTC og pluginstatus |
| POST /api/v1/commands | Én eksplicit kommando; returnerer et korreleret resultat |

Stream Deck er en ekstern klient og står derfor ikke i C#-pluginkataloget. Planlagte plugins annoncerer tomme handlingslister og `not_implemented`.

## Kommando

```json
{
  "protocolVersion": "1",
  "id": "73c876dd-9f55-4e02-b314-dad15a6ac386",
  "plugin": "obs",
  "action": "scene.switch",
  "arguments": { "sceneName": "Camera" }
}
```

Id skal være UUID i D-format. Feltnavne bruger camelCase. Plugin- og handlingsnavne er case-sensitive. Argumenter er altid et objekt; ukendte og gentagne argumentfelter afvises. Ukendte felter i konvolutten afvises. Grænser: 16 KiB request-body og JSON-dybde 16.

| Plugin | Handling | Argumenter |
|---|---|---|
| powerpoint | next, previous, black, resume | {} |
| powerpoint | goto | {"slide": 3} — positivt heltal inden for præsentationen |
| obs | scene.switch | {"sceneName": "Camera"} |
| obs | record.start, record.stop, stream.start, stream.stop | {} |
| obs | input.mute | {"inputName": "Mic", "muted": true} |

Navne på scener og lydkilder skal være ikke-tomme strenge på højst 256 tegn. Ingen filstier, generelle COM-kald eller vilkårlige OBS-requesttyper accepteres.

## Resultat

```json
{
  "protocolVersion": "1",
  "id": "73c876dd-9f55-4e02-b314-dad15a6ac386",
  "plugin": "obs",
  "success": true,
  "code": "ok",
  "message": {
    "da": "OBS bekræftede handlingen.",
    "en": "OBS acknowledged the action."
  },
  "data": null
}
```

Et læsbart kommandosvar returneres som HTTP 200, også når `success` er false. Klienten skal kontrollere version, id og success, ikke alene HTTP-status. Dårlig JSON giver 400; adgang giver 401/403; bodygrænse giver 413; kapacitetsgrænse giver 429. Transportfejl kan mangle JSON-body.

Fejlkoder: `unsupported_protocol`, `invalid_command`, `unknown_plugin`, `unsupported_action`, `invalid_arguments`, `id_conflict`, `cancelled_before_dispatch`, `outcome_unknown`, `plugin_error`, `unsupported_platform`, `slideshow_unavailable`, `obs_rejected`.

## Dubletter og timeout

De seneste 1.024 afsendte UUID'er gemmes i hukommelsen. Identisk genafsendelse returnerer samme resultat. Ændret indhold giver id_conflict. Fingeraftrykket afhænger også af argumenternes JSON-repræsentation og rækkefølge; behold derfor den oprindelige konvolut ved et kontrolleret status-/retryforløb.

Kommandoer har et samlet deadline-budget på 10 sekunder inklusive ventetid i dispatcher-køen. Annullering før dispatcheradgang giver cancelled_before_dispatch. Annullering under adapterarbejdet giver outcome_unknown. Adaptere skal respektere cancellation; allerede påbegyndte COM-kald kan fortsætte. Fejlresultater gemmes også for at undgå utilsigtet genudførelse.

Historikken er hverken permanent eller en sikkerhedsgrænse mod genafspilning. Ingen automatisk kommando-retry efter timeout, genstart eller netværksfejl. OBS har sin egen requestId pr. WebSocket-forespørgsel.

## Status

Status indeholder `protocolVersion`, `version`, `observedAt` og `plugins`. Hvert plugin har `id`, `state`, tosproget `message` og eventuelt `data`.

- PowerPoint ready-data: `position`, `slideCount`, `state` (PowerPoints numeriske visningstilstand).
- OBS ready-data: `scene`, `recording`, `streaming`, med de relevante OBS-responseData-objekter.
- Tilstande: `ready`, `unavailable`, `unsupported`, `not_implemented`.

Hvert plugins statusopslag har et budget på 10 sekunder. Status samles sekventielt, og observedAt markerer slutningen af indsamlingen; det er ikke en atomisk måling. Manglende eller utilgængelig status må aldrig fortolkes som stoppet optagelse eller stoppet udsendelse.
