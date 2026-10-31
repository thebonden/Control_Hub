# Command and status protocol v1

[Dansk](protocol.da.md)

Transport: local HTTP/JSON at `127.0.0.1:5187`. Every request requires `Authorization: Bearer <local-token>`. No CORS, remote access or broadcast discovery.

## Endpoints

| Method and path | Content |
|---|---|
| GET /api/v1/plugins | Registered C# plugins with ID, name, maturity and actions |
| GET /api/v1/status | Protocol version, product version, UTC observation time and plugin status |
| POST /api/v1/commands | One explicit command; returns a correlated result |

Stream Deck is an external client and is not in the C# plugin catalog. Planned plugins advertise empty action lists and `not_implemented`.

## Command

```json
{
  "protocolVersion": "1",
  "id": "73c876dd-9f55-4e02-b314-dad15a6ac386",
  "plugin": "obs",
  "action": "scene.switch",
  "arguments": { "sceneName": "Camera" }
}
```

IDs must be D-format UUIDs. Fields use camelCase. Plugin and action names are case-sensitive. Arguments must be objects; unknown and duplicate argument fields are rejected. Unknown envelope fields are rejected. Limits: 16 KiB request body and JSON depth 16.

| Plugin | Action | Arguments |
|---|---|---|
| powerpoint | next, previous, black, resume | {} |
| powerpoint | goto | {"slide": 3} — positive integer within the presentation |
| obs | scene.switch | {"sceneName": "Camera"} |
| obs | record.start, record.stop, stream.start, stream.stop | {} |
| obs | input.mute | {"inputName": "Mic", "muted": true} |

Scene and input names must be nonblank strings of at most 256 characters. File paths, generic COM operations and arbitrary OBS request types are not accepted.

## Result

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

A readable command result uses HTTP 200 even when `success` is false. Clients must validate version, ID and success, not HTTP status alone. Invalid JSON produces 400; access failures 401/403; body limit 413; capacity limit 429. Transport errors may not have a JSON body.

Error codes: `unsupported_protocol`, `invalid_command`, `unknown_plugin`, `unsupported_action`, `invalid_arguments`, `id_conflict`, `cancelled_before_dispatch`, `outcome_unknown`, `plugin_error`, `unsupported_platform`, `slideshow_unavailable`, `obs_rejected`.

## Duplicates and timeouts

The latest 1,024 dispatched UUIDs are retained in memory. An identical resend returns the same result. Changed content produces id_conflict. Fingerprints also depend on argument JSON representation and ordering; preserve the original envelope for any controlled reconciliation/retry workflow.

Commands have a total deadline budget of 10 seconds including dispatcher queue time. Cancellation before acquiring dispatcher access produces cancelled_before_dispatch. Cancellation during adapter work produces outcome_unknown. Adapters must honour cancellation; already running COM calls may continue. Failure results are cached too, to prevent unintended re-execution.

History is neither persistent nor an anti-replay security boundary. Do not automatically retry commands after timeout, restart or network failure. OBS uses a separate requestId for each WebSocket request.

## Status

Status contains `protocolVersion`, `version`, `observedAt` and `plugins`. Each plugin has `id`, `state`, a bilingual `message` and optional `data`.

- PowerPoint ready data: `position`, `slideCount`, `state` (PowerPoint's numeric display state).
- OBS ready data: `scene`, `recording`, `streaming`, containing the relevant OBS responseData objects.
- States: `ready`, `unavailable`, `unsupported`, `not_implemented`.

Each plugin status lookup has a 10-second budget. Status is collected sequentially and observedAt marks the end of collection; it is not an atomic snapshot. Missing or unavailable status must never be interpreted as recording or streaming being stopped.
