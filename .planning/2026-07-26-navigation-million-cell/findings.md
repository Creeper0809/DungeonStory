# Findings

## Initial Scope

- The previous 500-character pass used a 60x3 gameplay Grid and a 96x3 stress Grid.
- Current `Grid` creates a `GridCell` reference object for every coordinate in a rectangular
  `GridCell[,]`, so a 1024x1024 map creates 1,048,576 cell objects before search workspaces.
- Exact A* uses pooled flat arrays and a binary heap, which should scale better than the old
  BFS, but its million-cell memory and worst-case expansion have not been measured.
- Per-request Job dispatch was correctly rejected for tiny maps; million-cell or batched
  large-map requests must be re-profiled instead of inheriting that decision.

## Baseline Results

| Size | Construct | Walkable/link setup | Managed delta | Local 31-step | Horizontal | Vertical |
|---|---:|---:|---:|---:|---:|---:|
| 128x128 | 5.5 ms | 6.5 ms | 0.1 MB | 6.38 ms | 0.23 ms | 8.67 ms |
| 256x256 | 16.7 ms | 24.4 ms | 3.3 MB | 0.77 ms | 0.69 ms | 45.16 ms |
| 512x512 | 112.5 ms | 98.0 ms | 34.9 MB | 11.05 ms | 2.94 ms | 186.74 ms |
| 1024x1024 | 347.1 ms | 384.2 ms | 188.8 MB | 21.12 ms | 11.41 ms | 706.24 ms |

- The 1024x1024 baseline is not acceptable. Even a repeated 40-node local route averaged
  10.77 ms because `GridSearchWorkspace.Prepare` clears five million-entry arrays before
  examining the first node.
- A vertical route through one stair link per floor expanded 917,248 cells at 1024x1024.
  The exact A* heuristic only measures horizontal distance because arbitrary traversal links
  make a naive vertical Manhattan term inadmissible; the search degrades to Dijkstra.
- `GridCell` eagerly creates a dictionary, a list, and a read-only list wrapper per cell.
  This is the dominant reason a mostly empty million-cell Grid consumes about 188.8 MB.
- Large-map support needs lazy cell payloads, generation-stamped search arrays, and a
  structural floor/portal heuristic or hierarchical route before any 500-request claim.

## First Optimization Results

- Lazy `GridCell` occupant/traversal payloads, generation-stamped exact-search workspaces,
  and an admissible adjacent-floor heuristic removed the three baseline bottlenecks.
- At 1024x1024, a 1,023-step horizontal route took 0.514 ms and expanded 1,024 nodes.
- The 1,023-floor adjacent-stair route fell from 706.24 ms and 917,248 expansions to
  1.471 ms and 1,024 expansions.
- Repeated 40-node local routes became map-size independent at about 0.018 ms each.
- The measured managed delta for the 1024x1024 Grid fell from about 188.8 MB to 65.9 MB.
  The benchmark's 512 MB delta became negative after cross-size GC compaction, so final
  memory evidence must run each size in a fresh process.
- The remaining large-map risk is destination-less `SearchPathWeighted`: it still creates
  dense million-cell retained result arrays. Most routine AI now selects candidates without
  it and resolves the winner by exact A*, but legacy reachability/manual/work paths remain.

## Expanded Correctness And Burst Benchmark

- Added deterministic coverage for blocked/unreachable routes, weighted detours, obstacle
  detours, path-cache hits, traversal-version invalidation, a raw 500-request burst, and
  broker-budgeted 500 requests.
- The 128x128 validation passed:
  - blocked path: unreachable after 64 expanded nodes
  - cache: first search 1, repeat cache hit 1, terrain mutation triggered one fresh search
    and increased cost from 12,700 to 12,754
  - raw 500-request burst: 29.17 ms total, 0.104 ms p95 per request
  - broker-budgeted 500 requests: 0.650 ms p95 frame, 1.003 ms max frame
- The 128 map has repeated deterministic request keys, so cache hits reduce the budgeted run
  to 27 frames. The 1024 result is the meaningful large-map pressure test.

## 1024x1024 Final Results

- The final isolated 1024x1024 run passed weighted cost, obstacle detour, unreachable,
  cache hit, and terrain-version invalidation checks.
- Edge-to-edge horizontal A* took 0.471 ms and expanded 1,024 nodes. The adjacent-floor
  vertical case took 1.598 ms and expanded 1,024 nodes.
- Weighted and obstacle detours took 1.136 ms and 1.142 ms. Repeated 40-node local paths
  averaged 0.020 ms.
- A raw burst of 500 unique paths cost 152.1 ms and is intentionally not allowed in one
  gameplay frame. Broker scheduling spread those requests across 63 frames at 2.471 ms
  average and 3.685 ms p95 per frame.
- The fresh-process managed-memory delta was negative because Unity compacted pre-existing
  editor allocations during the measurement. The prior post-optimization isolated run
  measured about 65.9 MB for the million-cell Grid, so the signed delta in the final JSON
  must not be interpreted as the Grid using negative memory.

## 500-Character Large-Grid Trace

- The first 1024x1024 + 500-character forced profile exposed one remaining unbounded hot
  path: `AbilityMove.ExitDungeon` searched for a building ID that the stress map did not
  contain, expanding the whole million-cell map repeatedly. The profile reached 678 ms p95.
- Exit movement now resolves the known spawner entry coordinate and requests exact-position
  A*. `AIBrain` path rebuild also uses its known destination instead of predicate search.
- Exit movement now aborts after its bounded retries unless the actor actually reached the
  entry cell; it no longer falls through to a direct world-space move that could cross walls.
- Idle wandering is bounded to local same-floor samples and at most two exact A* attempts.
- Broker diagnostics now count unbounded searches. The final profile recorded zero.
- The final scheduler-only profile registered and ticked all 500 characters for 600 sample
  ticks with 16 forced replans per tick: 1.293 ms average, 3.472 ms p95, 3.762 ms max,
  and 0 KB measured per-tick allocation.
- This profile excludes character movement, animation, rendering, nameplates, and other
  presentation work. It proves the scheduler/path envelope, not full rendered 60 FPS.

## Dense Dungeon Follow-up

- The first large-map benchmark was still structurally sparse, so it was not evidence that
  a furnished dungeon would preserve the same AI cost.
- Added a dense batch profile that creates real `GridBuildingFactory` objects backed by
  `BuildingSO`, real interior doors, room membership, facility destruction, and 500 active
  behavior trees on a 1024x1024 Grid.
- Three 8,192-facility layouts passed:
  - balanced: 4,096 rooms and 4,032 doors, scheduler p95 3.6073 ms
  - door-heavy: 8,192 rooms and 8,128 doors, scheduler p95 3.5764 ms
  - facility-heavy: 2,048 rooms and 1,984 doors, scheduler p95 3.5883 ms
- Each scenario destroyed 512 facilities, invalidated room/facility caches, and rescanned
  the rooms without losing room counts or retaining removed furniture.
- An initial 8,192-facility run exceeded 7 ms p95 because shopping availability scanned all
  facilities. The final path uses the indexed nearest-candidate shortlist.
- The final weighted A* regression on 1024x1024 selected the optimal 1,025-step detour at
  cost 103,900 and budgeted 500 requests at 3.8082 ms p95.
- The dense profile still excludes rendering, movement animation, nameplates, physics
  presentation, and HUD. It validates domain/AI/path scaling, not end-to-end rendered FPS.

## Rendered Standalone Follow-up

- Added a command-line standalone performance probe that starts a normal new run and uses
  real `CharacterActor`, `BuildableObject`, renderer, collider, nameplate, physics, HUD,
  and AI paths.
- The first product build exposed a missing `IGridTraversalCostPolicy` VContainer
  registration. The data-only editor profiles did not exercise this composition failure.
- The normal 60x3 run with 8 actors and 146 buildings reached 1.104 ms p95 and had no
  frame over 16.67 ms.
- The 1024x1024 run with 100 actors, 8,192 modular facilities, 4,028 doors, and 12,366
  buildings reached 5.961 ms p95 but 34.209 ms p99. It meets p95 60 FPS, not p99.
- The same dense world with 502 actors reached 40.189 ms p95 in the initial 12-second
  sample and 60.535 ms p95 in a longer 30-second steady sample.
- The steady 500-character run averaged 55.96 FPS, had a 2.43 FPS 1% low, and sampled
  a 46.564 ms scheduler slice. A rendered 500-character 60 FPS guarantee is false today.
- Bulk character creation also saturated the persona request queue. Those rejections are
  emitted as normal logs rather than Unity warnings, so the structural report still has
  Error 0 / Warning 0.

## Dynamic Work Slicing Follow-up

- The current scheduler adapts `maxDecisionsPerFrame` between fixed integer limits, but a
  single `RunDecisionTreeDirect()` remains synchronous and cannot yield.
- The budget check runs only after a complete character decision. A costly candidate
  evaluation can therefore exceed the entire frame budget before the scheduler can stop.
- Immediate replans all use due time zero. The heap limits how many are processed, but
  synchronized world signals still produce an overdue burst rather than a smoothed queue.
- A correct solution is population-agnostic: estimate and measure work-unit cost, admit
  only work that fits current frame headroom, preserve resumable decision state, and age
  deferred urgent work so it cannot starve.
- Command-line detailed instrumentation on the real dense 500-character scene identified
  synchronous work that cannot be fixed by actor-count throttling alone:
  - survival environment signal: about 6.51 ms average and 7.18 ms p95
  - work target selection: 127.75 ms average across seven expensive samples, 266.77 ms max
  - path search: 4.01 ms p95 and 115.90 ms max
  - action destination resolution: 266.77 ms max
- The detailed recorder itself adds large overhead, so its frame-rate result is diagnostic
  only. Category attribution, not the 407 ms frame p95, is the useful output.
- Dynamic slicing must therefore include shared environment snapshots, resumable work
  candidate evaluation, and brokered path continuations. Merely changing a per-frame
  character count cannot enforce a frame budget.

## Editor-Only Playable Profile

- The performance workflow no longer requires a player build. The editor probe now creates
  a real prepared owner and two starting employees before stress actors and facilities are
  added to `GameplayScene`.
- Facility shortlisting is shared and resumable by role/spatial bucket, work-target scans
  consume dynamic frame headroom, and deferred actions resume from the same action index.
- The valid 1024x1024 editor PlayMode run contained 500 active `CharacterActor` objects,
  4,096 dense facilities, 1,004 doors, and 5,246 total buildings.
- The captured game frame confirms the normal HUD and playable run are active; the former
  black capture was an invalid QA fallback caused by hiding owner selection without applying
  an owner.
- Results: frame 12.406 ms average, 17.164 ms p95, 72.848 ms p99; scheduler 1.31 ms p95;
  path search 0.345 ms p95; Console Error 0 / Warning 0.
- Dynamic AI scheduling now stays within its p95 budget. The remaining 60 FPS miss is whole
  frame presentation/GC pressure: about 0.82 MB average allocation per sampled frame, plus
  500 independent actor/nameplate/presentation callbacks. This must be solved by
  visibility/spatial-chunk driven presentation scheduling, not a 500-character mode.

## Character Presentation Scheduling

- Replaced per-character `CharacterActor.Update`, `WorldCharacterNameplate.LateUpdate`, and
  `CharacterFeedbackBubble.LateUpdate` callbacks with one scene-scoped
  `CharacterPresentationScheduler`.
- Every actor remains simulated and registered, but only actors inside the camera viewport
  receive per-frame presentation maintenance. Offscreen actors are reclassified incrementally
  under the shared dynamic frame-work budget.
- The 1024x1024, 500-character Editor PlayMode profile improved from 17.164 ms to 16.555 ms
  frame p95 and from roughly 0.82 MB to 0.70 MB average sampled allocation per frame.
- The 1024x1024, 1,000-character profile registered all 1,000 actors while presenting only
  59 visible actors. It reached 10.909 ms average, 15.375 ms p95, 76.861 ms p99, 1.341 ms
  scheduler p95, and 0.332 ms path p95 with Console Error 0 / Warning 0.
- Population-scaled presentation callbacks are no longer the p95 bottleneck. Remaining work
  is tail latency and allocation: p99 is still about 76 ms, average sampled allocation is
  about 0.72 MB/frame, and the 1,000-character maximum AI decision deferral reached 11.908 s.

## 100 Actors At X5

- The 100-actor target is materially different from the prior 500/1,000 stress envelope:
  after removing the million-cell entrance lookup, the normal rendered 30-second run stays
  below the 16.67 ms frame budget at p99.
- The largest repeatable gameplay spikes were not A* searches. They were an absent-entrance
  full-grid lookup, per-actor deprivation allocations, and repeated LLM prompt construction.
- Final normal measurement:
  - frame average 7.475 ms, p95 11.768 ms, p99 14.881 ms
  - 1% low 67.20 FPS
  - scheduler p95 1.660 ms, max 3.228 ms
  - path broker p95 0.324 ms, max 0.469 ms
  - 21 of 4,018 frames exceeded 16.67 ms; one exceeded 33.33 ms
  - Console Error 0 / Warning 0
- The raw trace's two >33 ms samples were attributable to the profiling coroutine and the
  Unity Editor loop, not a runtime domain tick. This supports the 100/X5 gameplay target on
  the current editor machine, but it is not an absolute guarantee for every hardware target.
- Sampled GC remains about 274 KB/frame with a one-time 34 MB maximum sample. This is below
  the current p99 frame target but remains the next optimization target for longer sessions.

## Owner Skill Boundary Regression

- The failed work-speed snapshot assertion was a valid gameplay regression, not a flaky
  performance test. The runtime snapshot was 1.20 instead of 1.10 because a regular employee
  received both its generated passive and the fallback owner skill `창업 본능`.
- `CharacterOwnerFixedSkillUtility.GetSkills` formerly padded any `CharacterSO` to four owner
  skills. Owner fallback generation must be guarded by `IsOwnerCandidate`; callers cannot
  infer role from the returned list after fallback generation.
- The corrected 100-actor X5 profile is slightly faster than the prior baseline and remains
  under the frame budget at p99. The fix therefore restores behavior without compromising
  the optimized scheduling target.
