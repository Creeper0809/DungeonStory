# Million-Cell Navigation Verification

## Goal

Verify and, where necessary, redesign weighted navigation and runtime work
scheduling so maps up to 1024x1024 remain memory-safe and scale with arbitrary
population through frame-headroom-driven, resumable work slicing.

## Phases

- [x] Measure current Grid construction, memory, and A* scaling at 128/256/512/1024 square.
- [x] Add a deterministic large-map benchmark covering open, weighted, obstacle, and invalidation cases.
- [x] Remove million-cell memory or initialization blockers discovered by the baseline.
- [x] Add large-map query controls such as bounded search, hierarchical regions, or shared fields only where measured.
- [x] Run 500-request burst and sustained scheduling profiles on the largest viable map.
- [x] Run focused Unity regressions and publish the exact supported envelope.
- [x] Replace the sparse-world assumption with three 8,192-facility dense-dungeon scenarios.
- [x] Measure the fully rendered standalone GameplayScene at baseline, 100, and 500 characters.
- [x] Replace fixed per-frame actor limits with workload-adaptive, resumable AI work slicing.
- [x] Re-run rendered Editor PlayMode profiles and verify that p95 presentation load scales.
- [x] Verify the product scene with 100 actors at X5 and keep frame p99 below 16.67 ms.
- [ ] Remove residual p99/GC spikes and cap worst-case AI decision deferral at 1,000+ actors.

## Decisions

- Do not extrapolate the prior 60x3/96x3 result to large maps.
- Test progression stops at the first unsafe size, fixes the blocker, then resumes.
- Correct weighted routes and door/traversal authorization remain mandatory.
- Unity objects and route commit stay on the main thread. Worker execution is allowed only
  for immutable flattened snapshots and must reject stale version results.
- A 1024x1024 claim requires successful construction, search correctness, bounded memory,
  and 500-agent demand tests, not only one empty-map path.
- Dynamic scheduling must react to measured frame headroom and queued work cost. It must
  not introduce a separate 500-character mode or hard-code population tiers.

## Verification Matrix

- Sizes: 128x128, 256x256, 512x512, 1024x1024.
- Layouts: open, weighted terrain bands, deterministic maze/obstacles, sparse portals.
- Queries: short local, edge-to-edge, unreachable, repeated cache hit, version invalidation.
- Demand: one request, 500 burst requests, sustained budgeted requests.
- Metrics: construction ms, managed-memory delta, search avg/p95/max, expanded cells,
  route cost/length, cache hit rate, and GC.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---|---|
| Editor benchmark could not access internal `GridSearchScratch` | 1 | Exposed a narrow `Grid.ReleaseRetainedSearchMemoryForDiagnostics()` wrapper instead of making the pool public. |
| 1024-grid 500-NPC profile reached 678 ms p95 | 1 | Traced `ExitDungeon` to an absent entrance predicate that scanned all 1,048,576 cells; replaced it with exact-position A*. |
| Forced profile narrowly exceeded the 4 ms scheduler target | 2 | Added a 1 ms path-time slice and 15% scheduler headroom so the final decision cannot consume the whole frame budget. |
| Dense 8,192-facility profile exceeded 7 ms p95 | 1 | Removed full facility scans from shopping availability and bounded facility scoring to the nearest 20 indexed candidates. |
| Weighted-detour fixture selected the direct wet route after portal heuristic work | 1 | Refreshed traversal heuristic metadata after the test directly mutated stair links; the 1024 route returned to the optimal 103,900 cost. |
| Unity RunCommand could not reference `CharacterActor` because its dynamic assembly lacked Sirenix references | 1 | Inspected live actors through `MonoBehaviour.GetType()` and moved permanent measurement code into the project assembly. |
| Existing HumanPlaytest executable opened the obsolete SampleScene flow | 1 | Rejected the stale July 17 build and scheduled a fresh product-scene build before measurement. |
| Editor profile produced a black capture after hiding the QA owner panel | 1 | Start a real prepared owner/staff party through `IStartPartyPreparationService` and `IPreparedStartPartyGameplayApplier`; fail the profile if owner selection remains visible. |
| Progression regression produced a 1.20 work-speed snapshot instead of 1.10 | 1 | Restricted fallback owner-fixed skills to `IsOwnerCandidate` profiles and added a regular-employee boundary assertion. |
