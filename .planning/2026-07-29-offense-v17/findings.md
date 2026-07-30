# Offense V17 Findings

## Baseline

- Unity 6000.3.8f1 is connected through MCP.
- Editor is not playing or compiling.
- Console baseline is Error 0 / Warning 0.
- Surgery V16 implementation and verification are complete but uncommitted.

## Existing Offense

- `OffenseWorldMapService` exposes six hard-coded, linearly gated targets.
- `OffenseRouteGenerator` creates one fixed eight-node route per target.
- `OffenseExpeditionRun` currently creates at most three formation members.
- Event resolution is primarily a binary supply-use decision.
- `OffenseBattleRuntime` already integrates shared combat resolution, equipment,
  anatomy health, strategic pressure, skills, and ultimate execution.
- Physical departure, return arrivals, loot delivery, regional pressure, and
  section-based persistence already exist and should be adapted.
- Player-facing strings in older offense files contain extensive mojibake.

## Architecture Direction

- Add V17 domain services beside the legacy runtime, then bridge them through
  existing scene runtimes and presenters.
- Keep `rival_dungeon` and `truth_core` as fixed boss sites.
- Move extensible content to individual ScriptableObject definitions.
- Keep pure deterministic map, path, return-safety, and chain rules testable
  without scene objects.

## Implementation Findings

- The legacy offense save restored active expeditions only through the static
  campaign target catalog. Generated V17 targets therefore needed an embedded
  target snapshot and explicit V17 phase flags.
- Redirecting travel directly through `IOffenseTravelRuntime` was insufficient:
  reaching a newly selected site did not retarget the expedition objective.
  Routing now goes through `OffenseExpeditionRuntime`.
- Enemy intents originally executed once per intercepting command and ignored
  unopposed intents. Each intent now executes exactly once; later attacks on the
  same intent are one-sided follow-ups, and intents refresh every command turn.
- Urgent-site definitions already contain mitigation materials and work values,
  but the physical delivery and worker-driven mitigation order is still missing.
- The original V17 surface kept the desktop `72% map + 28% sidebar` split in
  portrait, shrinking controls into an unusable strip. The surface now switches
  to a full-width map with a scrollable bottom command sheet.
- The post-fix map capture passes bounds, overflow, and content checks at both
  1600x900 and 900x1600. Portrait controls are now full width and legible; the
  hex map remains independently pannable and zoomable.
- A live V17 battle also opened the legacy offense battle canvas above the
  command-card surface. `OffenseBattleUiController` now routes V17 expeditions
  exclusively to the command surface and retains the legacy panel only for
  legacy expeditions.
- Changing from a panned world map to battle could retain the old content
  offset and clip the battle title. V17 surface transitions now reset only the
  map viewport state, while ordinary card selection keeps the current view.
