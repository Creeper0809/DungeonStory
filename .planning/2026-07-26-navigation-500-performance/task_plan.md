# Navigation 500 Performance

## Goal

Replace the single unweighted full-grid BFS hot path with cost-aware, purpose-specific
navigation while preserving current gameplay behavior, then prove a 500-character
standalone Player performance envelope.

## Phases

- [x] Capture the current Grid, broker, movement, AI, and performance contracts.
- [x] Add deterministic traversal-cost policies and weighted path result primitives.
- [x] Add point-to-point A*, multi-target Dijkstra cost fields, and reusable versioned caches.
- [x] Route movement and AI target selection through the appropriate query type.
- [x] Remove navigation hot-path allocations and add invalidation/fairness coverage.
- [x] Run focused Unity regressions and 100/500-character performance verification.

## Decisions

- Keep legacy `Grid.SearchPath` as a compatibility reachability facade until all runtime
  call sites have explicit query semantics.
- Use integer fixed costs. Dry horizontal movement is 100 cost units.
- Use A* for a fixed destination, Dijkstra for weighted multi-candidate queries, and
  reverse/shared fields for common destinations.
- Never access Unity objects from worker jobs. A future Burst path operates on immutable
  flattened navigation snapshots and commits results on the main thread after version checks.
- Performance claims apply to a documented minimum-spec standalone Player scenario, not
  arbitrary hardware or Editor timings.
- Do not dispatch one Unity Job per path on the current 60x3/96x3 worlds. The measured
  weighted A* query is substantially cheaper than Job scheduling; future parallel work must
  batch immutable offscreen scoring or route requests and version-check results on commit.

## Verification Gate

- Existing Grid, door access, movement, AI, work, wildlife, defense, and save regressions pass.
- Weighted paths prefer lower travel time over fewer cells and honor door/traversal rules.
- Structural, terrain-cost, door-access, hazard, and congestion changes invalidate only
  affected cached results.
- Navigation and AI scheduling allocate no managed memory in steady-state Player profiling.
- The 500-character benchmark maintains the approved 60 FPS frame budget without path
  starvation or unbounded urgent work.

## Final Verification

- Focused Grid plus 100-character regression: passed.
- 500-character staged PlayMode profile: behavior and performance gates passed.
- 600 sampled frames: average 3.39 ms, p95 4.37 ms, maximum 15.40 ms, with 0 frames
  above 16.67 ms.
- Scheduler: average 1.228 ms, p95 1.809 ms, maximum 2.580 ms.
- Path broker: 527 searches, 8,674 cache hits, 247 bounded deferrals, at most 7 searches
  and 8 deferrals in one frame.
- Incremental stress-world GC after subtracting the same-scene Editor baseline averaged
  36.0 KB/frame.
- Gameplay/compile verification finished with no C# errors and no game Console warnings.
  Batch logs contain Unity licensing-service handshake noise from the isolated verifier,
  which is not a gameplay Console failure.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---|---|
| Parallel inspection returned exit 1 when one `rg` query had no match | 1 | Split optional searches from required file reads so an expected no-match does not hide other output. |
| PowerShell rejected a wildcard path passed directly to `rg` | 1 | Query concrete scene files instead of using a Windows path wildcard. |
| `dotnet build` could not run because no .NET SDK is installed | 1 | Use Unity Editor compilation, which is the authoritative compiler for this project. |
| Combined planning patch had an invalid hunk boundary | 1 | Reapplied the task-plan and progress updates as separate patches. |
| Looked for a moved Grid debug scenario at its old path | 1 | Located it with `rg --files` under `Controllers/Grid/System/Editor`. |
| One inspection command used a nonexistent legacy `Assets/Scripts/Character` path | 1 | Restricted subsequent searches to current `Services/Character` paths. |
| 500-character synchronous MCP stress exceeded the 300-second tool timeout | 1 | Identified eager BT construction as the setup spike and deferred configuration through the scheduler budget. |
| Process inspection used an invalid temporary workdir | 1 | Re-ran from the project root. |
| A multi-file exact-path patch failed on mojibake message context in `AbilityMove` | 1 | Split the patch and migrated ASCII-stable fixed-destination callers first. |
| The synchronous 500-character editor scenario blocked Unity's main thread for more than five minutes | 1 | Capped synchronous scenarios at 100 and converted 300/500 validation to staged PlayMode creation and sampling. |
| Escape/PostMessage attempts did not interrupt the synchronous editor command | 2 | Left the unsaved editor process intact; it eventually recovered without destructive termination. |
| External Roslyn compilation exposed `FacilityWorkType` as inaccessible across the new Buildings assembly boundary | 1 | Kept the required enum and made the asset migration contract public instead of deleting it. |
| Unity MCP remained occupied by the stale long-running direct connection after the editor recovered | 2 | Continued with external Unity Roslyn compilation; Unity MCP validation remains pending until the bridge reconnects. |
