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
