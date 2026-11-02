# Test plan and acceptance

[Dansk](testing.da.md)

## Automated checks

- Windows package checks: included runtime, DPAPI configuration, corrupt configuration, local address validation, packaged host startup, authenticated status, occupied ports and shutdown through a closed input connection. Run `scripts/verify-package.ps1` after packaging; this does not test physical control.

- Release build of all C# projects with warnings treated as errors.
- Routing, UUIDs, protocol versions, unknown actions and invalid arguments.
- Concurrent duplicate commands execute once within the cache window; conflicting IDs are rejected.
- Timeouts and cancellation distinguish not dispatched from unknown outcome.
- PowerPoint adapter calls are tested against a fake session. Opening, saving, editing and closing are not actions.
- OBS uses a local simulated WebSocket server: handshake, password challenge, fragmented replies, intervening events, request failures and mismatched correlation.
- Elgato's official CLI 1.10.3 validates the plugin manifest and assets, including the main icon's PNG format. Validation is not a physical installation test.
- The connection check is verified to send only a status GET and never save or echo the token in its reply. UI tests cover correlation, language, timeout and editing the token during a request.
- Late success or failure must not appear on a reconfigured key or reused context.
- Stream Deck uses simulated HTTP: key presses, argument mapping, overlap, failure feedback, missing token and status.
- Host smoke test: authentication, Origin rejection, malformed JSON, version, actions and unavailable integrations.
- Source policy: no presentation files and no known file-writing or focus operations in the PowerPoint adapter.

CI runs on Windows and Linux. Tests do not modify presentation files or send production commands to the user's OBS. Green CI does not prove COM, Stream Deck installation or specific hardware has been tested.

## Physical acceptance — not yet completed

| Step | Expected outcome |
|---|---|
| Install source plugin in Stream Deck | All 12 actions appear; both languages selectable |
| Configure a missing/incorrect token | No action; clear error |
| Start one slideshow and a test OBS setup | Correct position and output status |
| Move focus to another application | Navigation and OBS scene selection still work |
| Next/previous with animations | Native PowerPoint behaviour; one action per intentional press |
| Go to valid/invalid slide | Valid navigation; invalid request rejected without a change |
| Black screen and resume | Only slideshow display state changes |
| First/last slide and custom show | Behaviour documented and accepted |
| Two slideshow windows | Ambiguous control rejected |
| Wrong OBS password or scene name | No false success; useful failure feedback |
| Start/stop test recording and mute/unmute | OBS confirms the desired state |
| Start/stop streaming to an approved test destination | Only during an explicitly agreed live test |
| Close/restart OBS or PowerPoint | Status becomes unavailable and recovers; no automatic replay |
| Disconnect during a command | Unknown outcome reconciled using status |
| Compare presentation SHA-256 before/after | Identical file content |
| Stop host | PowerPoint and OBS stay open |

Use `Get-FileHash -Algorithm SHA256 -LiteralPath '<existing file>'` before and after. The test must not save the presentation or include editing. Do not use production broadcasts for an unannounced test.

## Recording results

Record date, Windows/Office/OBS/Stream Deck versions, hardware model, plugin commit, steps, expected and observed outcomes, file hashes and operator approval. Never share tokens, OBS passwords or streaming keys.

0.0.001 may only be called hardware-verified after this procedure is completed and its evidence attached. 0.1.000 additionally requires owner approval of the full scope and evidence from the active environment.

## Local integration status — updated 1 November 2026

On 31 October the corrected connection dialog was exercised in the installed Stream Deck plugin: Control Hub, PowerPoint and OBS responded. The status key showed slide 1 with recording/streaming off.

A live check sent simulated key events through the production plugin bridge to the open local Control Hub 0.0.001 test build, PowerPoint COM and OBS 33.0.1. Next/previous, go to slide, invalid-slide rejection, black screen/resume and OBS scene selection passed. Slide 1 and the original scene were restored. The presentation SHA-256 was unchanged. No recording or streaming was started. The check used the already open control window from package-001-b; installation checks of the new package are separate.

The owner subsequently confirmed that the physical Next key on Stream Deck XL changes slides. The follow-up COM read did not find exactly one active slideshow, so the resulting slide number was not independently verified. Profile 2 has Status, Next, Previous, OBS SORT and OBS PPT. Physical presses on the other keys, recording, streaming, mute, animations and full hardware acceptance remain unconfirmed.

On 1 November, 51 C# tests and 27 JavaScript tests pass after adding specific connection reasons. Tests cover missing/rejected OBS passwords, a closed port, malformed handshake, disconnect after dispatch, PowerPoint failure reasons and safe bilingual status messages. Fault tests use a simulated OBS server; they do not change the user's server settings.

## AndroidRemote implementation stage — 1 November 2026

After adding the AndroidRemote library, 89 C# tests and 27 JavaScript tests pass. The 38 new cases cover concurrent single-use pairing, expiry, wrong tokens, permissions, device limits, revocation during queueing/status reads, isolated duplicate history and unknown outcomes without automatic replay. Host smoke tests pass on a separate local port while the regular Control Hub runs. Build: zero warnings/errors. These are library tests with simulated devices, not Android hardware tests.

## Phone dialog — 2 November 2026

The host smoke test creates, lists and cancels test invitations and checks the capacity limit. Missing owner proof, the Stream Deck key alone, Origin requests and invalid fields are rejected. The listing contains no secrets or tokens. The packaged desktop client has a separate create/read/cancel check. The actual Windows dialog was visually checked in both languages with simulated devices, including Danish creation of a read-only invitation. No phone was used.
