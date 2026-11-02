# AndroidRemote: pairing and authorization

[Dansk](androidremote.da.md)

The first implementation is a transport-independent .NET 10 library under `Plugins/AndroidRemote/`, tested with the real dispatcher and simulated devices. It is **not an installable Android app or a working phone connection**.

## Pairing flow

1. The control window’s **Manage phone access** dialog calls `BeginPairing` with a 1–80 character name without control characters and an explicit permission list.
2. The library returns a pairing ID, random secret and expiry time. The secret is 32 cryptographically random bytes represented by 64 hexadecimal characters, not a short PIN. It lasts two minutes.
3. The future phone transport submits the ID and secret to `CompletePairing`. At most one concurrent attempt succeeds. Five incorrect attempts cancel the pairing; malformed secrets count too.
4. Successful pairing issues a new device ID and independent token lasting eight hours. The name and permissions come from the owner's selection; the phone cannot expand them.
5. Commands and status pass through `RemoteCommandGateway`. Incorrect tokens, missing permissions, revoked and expired devices are denied.

The owner can cancel a pairing, revoke one device or revoke all devices and pending pairings. Secrets are returned only at creation and must never be logged. The registry stores SHA-256 digests and compares them using `CryptographicOperations.FixedTimeEquals`. Device lists contain no credentials. This applies to high-entropy random tokens, not user passwords.

At most eight pairings may be pending, with a combined limit of 16 paired/reserved device slots. Expiry uses monotonic time; changing the Windows clock does not extend access. Displayed UTC times are advisory if the system clock changes. Everything is memory-only. Restart requires pairing again.

## Permissions

| Permission | Allows |
|---|---|
| `status.read` | The complete Control Hub status object, including OBS scene name |
| `powerpoint.control` | `next`, `previous`, `goto`, `black`, `resume` |
| `obs.scenes` | `scene.switch` |
| `obs.recording` | `record.start`, `record.stop` |
| `obs.streaming` | `stream.start`, `stream.stop` |
| `obs.audio` | `input.mute` |

No permissions are added automatically. There is no wildcard permission. Unknown and duplicate permissions are rejected. Permissions apply to action types, not individual scenes/inputs. File editing, arbitrary COM calls and focus-dependent keystrokes are not allowed.

## Queueing, revocation and duplicates

Authorization is checked on receipt and after waiting for the dispatcher, before reading cached results or executing. Revocation or expiry while queued therefore rejects still-pending commands. The post-queue check is the admission boundary; revocation cannot undo already admitted work, including COM calls. Status also rechecks authorization after collection.

The client's command UUID is deterministically mapped to an internal UUID in the pairing's own namespace. Responses restore the original client ID. An identical ID from two pairings or Stream Deck cannot retrieve the same cached result. History is still the shared in-memory cache of the latest 1,024 dispatched IDs, not persistent replay protection. Pairing again creates a new namespace. Phones must never automatically replay old commands after reconnecting or restarting.

`access_denied` uses fixed Danish/English messages and executes no plugin action. A timeout after adapter work starts preserves `outcome_unknown`. The operator must check status before another action.

## Before phone use

Remaining work includes a Danish/English Android client and separate TLS transport with verified server identity, secure pairing-secret/token transfer and secure storage on the phone. The transport must limit size, concurrency and request rate; library pairing limits do not replace these. Transport tests and physical Android testing must follow.

The control window uses [local owner routes](protocol.en.md). They require both the host connection key and a separate random owner key for this launch. The Stream Deck key alone cannot manage access. There is no route for a phone to redeem an invitation yet. The local Control Hub token must not be copied to the phone. The host still listens only on loopback; no firewall or LAN access is opened. Plugin status is `unavailable` / `transport_not_configured` with an empty action list.

## Tests

Tests cover concurrent single-use pairing, expiry, system-clock changes, incorrect secrets, capacity limits, immutable permissions, action permissions, forbidden file actions, isolated duplicate history, revocation/expiry while queued, revocation during status reads, correlation, timeouts and truthful transport status. No presentation is modified and no real recording or stream is started.
