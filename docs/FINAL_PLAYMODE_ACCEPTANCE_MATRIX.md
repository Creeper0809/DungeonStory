# Final PlayMode acceptance matrix

This gate is driven only through Unity MCP commands and Unity's own
`EventSystem`/automation test capability. Operating-system mouse or keyboard
automation is forbidden.

## MCP entry points

Invoke the public static request method through Unity MCP:

`DungeonFinalPlayModeAcceptanceRequestFacade.RequestRunFromMenu()`

Poll the public status method through Unity MCP until it is no longer queued or
running:

`DungeonFinalPlayModeAcceptanceRequestFacade.GetStatusForMcp()`

The equivalent Unity menu command is:

`DungeonStory/QA/Request Final PlayMode Acceptance`

The final report is written to:

`Artifacts/QA/final-playmode-acceptance-report.txt`

The run is accepted only when its first line contains
`FINAL_PLAYMODE_ACCEPTANCE RESULT=PASS`. A PNG file existing on disk never
counts as a pass by itself.

## Ordered matrix

| Order | Target | Required coverage | Verifier report |
|---:|---|---|---|
| 1 | Resolution matrix | Title, settings, and gameplay HUD. Includes `1600x900` and `900x1600` plus the existing intermediate resolutions. | `Temp/resolution-matrix-report.txt` |
| 2 | Full-world V18 round trip | Live 54-section registry/capture, actual full-game round trip, baseline restoration, Console Error/Warning 0. This is the first Gameplay target. | `Artifacts/QA/full-world-round-trip-playmode-report.txt` |
| 3 | Research tree | `1600x900` desktop and `900x1600` detail/queue pointer flows using Unity automation input and `EventSystem`. | `Artifacts/QA/research-tree-playmode-report.txt` |
| 4 | Production | `1600x900` and `900x1600` branch, stock-sensor, priority, and pointer matrix. | `Artifacts/QA/production-ui-pointer-matrix-report.txt` |
| 5 | Service room | `1600x900` and `900x1600` panel bounds, mode-button hit test and `EventSystem` dispatch, capture dimensions and visible pixels. | `Artifacts/QA/service-room-pointer-matrix-report.txt` |
| 6 | Character summary/medical | `1600x900` and `900x1600` summary, health tab, automatic-surgery toggle, surgery modal, close flows, bounds, and captures. | `Artifacts/QA/CharacterSummaryMedical/ui-matrix-report.txt` |

Each target report is deleted immediately before its request. The coordinator
records the target request time and accepts the result only when both conditions
hold:

1. The report's UTC write time is at or after the recorded target start time.
2. A report line explicitly declares `RESULT=PASS` or starts with
   `RESULT=PASS;`.

Stale reports, `RESULT=FAIL`, arbitrary text containing a pass substring, and
capture-only output all fail the gate. Target timeouts are also written as an
explicit final failure.

The coordinator evaluates its timeout while PlayMode is active as well as in
EditMode. On timeout or an orchestration exception it deletes every known child
request marker, stops the active verifier runner, records a persistent pending
failure, requests `ExitPlaymode`, and writes the final failure after returning to
EditMode. This prevents a crashed child request from repeatedly re-entering
PlayMode.

Resolution verification has its own persistent request marker. If an Editor
domain reload drops the queued PlayMode callback, the `InitializeOnLoad` update
loop sees the saved coordinator state and marker and requests PlayMode again.
The persistence baseline is refreshed before every target and restored on both
successful and failed finalization.

Full-world Console acceptance uses two mandatory gates. Its persistent early-log
buffer records warnings and errors from the request marker through runner `Awake`,
including logs emitted after the Editor domain reload has initialized the new
AppDomain. Unity MCP must then read the complete Unity Console after the matrix
finishes and independently confirm Error 0 / Warning 0. This second hard gate
covers the domain-reload interval in which managed `Application.logMessageReceived`
handlers cannot exist; neither gate may be omitted. Full-world completion and
coordinator failure cleanup both remove the request marker and its early-log
buffer.

## Evidence paths

- Progress: `Artifacts/QA/final-playmode-acceptance-progress.txt`
- Final report: `Artifacts/QA/final-playmode-acceptance-report.txt`
- Research captures: `Artifacts/QA/research-tree-1600x900.png`,
  `Artifacts/QA/research-tree-900x1600-detail.png`, and
  `Artifacts/QA/research-tree-900x1600-queue.png`
- Production captures: `Artifacts/QA/production-branches-1600x900.png` and
  `Artifacts/QA/production-branches-900x1600.png`
- Service-room captures: `Artifacts/QA/service-room-1600x900.png` and
  `Artifacts/QA/service-room-900x1600.png`
- Character summary/medical captures: the four resolution/surface images under
  `Artifacts/QA/CharacterSummaryMedical/`

Loaded execution must be performed by Unity MCP. Source compilation and the
offline freshness policy harness do not replace this final loaded-Unity run.
