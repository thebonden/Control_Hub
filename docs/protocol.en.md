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

## Specific connection failures

Known failures include `data.reason` in status and command results. These optional fields do not change protocol version 1. Clients must use a general fallback for unknown reasons.

| Plugin | Reasons |
|---|---|
| PowerPoint | `powerpoint_not_found`, `no_slideshow`, `multiple_slideshows`, `powerpoint_unavailable` |
| OBS | `connection_unavailable`, `password_missing`, `authentication_failed`, `protocol_error`, `connection_lost`, `request_rejected` |

An OBS disconnect or invalid reply after starting request transmission produces `outcome_unknown`; an explicit OBS rejection produces `obs_rejected`. No automatic retry. Status remains `unavailable` when a cause is known; it must not imply recording/streaming is stopped. Fixed Danish/English messages never include raw server errors, passwords or OBS error comments. WebSocket close code 4009 identifies authentication failure according to the official OBS protocol.

## AndroidRemote library

AndroidRemote now advertises `development` maturity, no actions and `unavailable` with `data.reason = transport_not_configured`. The phone transport has no HTTP routes yet. Local owner routes are described below. Internally it uses protocol v1 and may return `access_denied` before allowing plugin actions or cached results. Revocation is rechecked after the command queue; responses preserve the client command ID. See [pairing and access](androidremote.en.md).

## Local AndroidRemote administration

All routes under `/api/v1/androidremote/owner/` require the normal bearer key **and** `X-ControlHub-Owner`. The control window generates a separate 32-byte random hex key for each host launch. It is not saved in settings, keys or the clipboard. Without a distinct valid `CONTROLHUB_OWNER_KEY`, the host does not register owner routes. Browser Origin is rejected; owner routes accept loopback clients only. Processes running with equivalent access under the same Windows user are not isolated from one another.

| Method and relative path | Result |
|---|---|
| GET (root) | `transportReady: false`, `devices` and `pendingPairings`; no secrets |
| POST pairings | `{"name":"My phone","permissions":["status.read"]}`. 201: ID, secret and expiry. The list cannot retrieve the secret. |
| DELETE pairings/{id} | Cancel invitation; 204 or 404 if already gone |
| DELETE devices/{id} | Revoke device access; 204 or 404 |
| DELETE access | Remove all invitations and devices; 204 |

Invalid, unknown or duplicate fields/permissions return 400; capacity returns 409. Missing the regular key returns 401; an incorrect owner key returns 403. Owner responses must not be cached. After an interrupted creation request an invitation may already exist: refresh and cancel it if needed. Do not automatically retry. None of these routes redeem invitations or execute production commands.
