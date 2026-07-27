# Progress

## 2026-07-26

- Started explicit large-dungeon verification at 128x128 through 1024x1024.
- Recorded that the previous 500-character result did not cover large square maps.
- Began auditing Grid construction, cell storage, A* workspaces, and existing benchmark entry points.
- Added a deterministic large-grid benchmark and exact-search expanded-node telemetry.
- The first isolated Unity compile caught an Editor/runtime assembly boundary error in
  benchmark cleanup; replaced the internal pool access with a narrow Grid diagnostic API.
- Completed the unoptimized 128/256/512/1024 square baseline in isolated Unity.
- At 1024x1024, construction took 347.1 ms, walkable/link setup 384.2 ms, and managed
  memory increased by 188.8 MB.
- A repeated 40-node route averaged 10.77 ms and the floor-to-floor stress route took
  706.24 ms after expanding 917,248 nodes, establishing that the current implementation
  cannot be described as large-dungeon ready.
- Made empty Grid cells allocate no dictionary, traversal list, or read-only wrapper.
- Replaced million-entry exact-search array clearing with generation stamps and touched-node
  reference cleanup.
- Added a safe vertical heuristic for adjacent-floor traversal; arbitrary vertical links
  automatically disable it to preserve optimal paths.
- Re-ran all sizes. At 1024x1024, horizontal and vertical long paths now take 0.514 ms and
  1.471 ms, while repeated local routes average 0.018 ms.
- Extended the benchmark with unreachable, obstacle, cache invalidation, and 500-request
  burst/budget tests.
- Verified the expanded benchmark at 128x128 in an isolated Unity batch clone; all checks passed.
- Ran the expanded 1024x1024 benchmark. Weighted cost, obstacle detour, unreachable,
  cache hit, and invalidation checks all passed.
- Measured 500 raw paths at 152.1 ms total and the broker-budgeted form at 3.685 ms p95
  over 63 frames.
- Generalized the AI stress world to 1024x1024 while keeping 500 active characters on
  three representative walkable floors.
- Traced an initial 678 ms p95 failure to `ExitDungeon` performing an unbounded predicate
  search for an absent entrance across all 1,048,576 cells.
- Replaced exit and current-path rebuild searches with exact-target A*, bounded idle
  wandering, and added per-frame unbounded-search telemetry.
- Prevented failed exit paths from falling through to a straight world-space exit movement.
- Added a 1 ms path-search time slice and 15% scheduler headroom for a 4 ms AI budget.
- Final 1024x1024 + 500-character scheduler profile passed at 1.293 ms average,
  3.472 ms p95, 3.762 ms max, zero unbounded searches, and zero measured tick allocation.
- Re-ran the 100-character Grid/AI regression; `valid=True`.
- Stored final reports in `docs/implementation-reports/navigation-large-grid-profile-latest.json`
  and `docs/implementation-reports/navigation-large-ai-profile-latest.json`.
- Added actual dense-dungeon generation with `BuildingSO` facilities, interior doors, room
  detection, facility churn, and 500 forced-replan AI actors.
- Ran balanced, door-heavy, and facility-heavy layouts with 8,192 facilities each on the
  1024x1024 Grid. All three passed the 4 ms scheduler p95 target.
- Removed dense-map full scans from shopping/facility availability and kept facility
  selection to the nearest indexed shortlist.
- Re-ran the 1024 weighted-path regression after refreshing synthetic stair metadata:
  optimal cost 103,900, path length 1,025, and budgeted 500-request p95 3.8082 ms.
- Published the combined dense evidence and its explicit rendering/UI exclusions in
  `docs/implementation-reports/navigation-dense-dungeon-profile-latest.json`.
- Began the rendered standalone follow-up. Confirmed that direct Editor Play only enters the
  five-character GameplayScene QA fallback and is not a valid normal-run measurement.
- Launched the existing HumanPlaytest executable and rejected it because it was a stale
  July 17 build that still opened SampleScene rather than the current product flow.
- Added `DungeonGameplayPerformanceProbe` and rebuilt the current HumanPlaytest product
  scenes.
- Fixed the standalone-only VContainer failure by registering
  `IGridTraversalCostPolicy` in `DungeonFoundationRegistration`.
- Ran a normal rendered baseline: 8 actors, 146 buildings, p95 1.104 ms, Error/Warning 0.
- Ran the rendered 1024x1024 dense 100-character profile with 8,192 facilities and 4,028
  doors: p95 5.961 ms, p99 34.209 ms.
- Ran the rendered dense 500-character profile: p95 40.189 ms, p99 364.437 ms.
- Repeated 500 characters after 1,800 warmup frames and sampled for 30 seconds: average
  17.871 ms, p95 60.535 ms, p99 410.953 ms, so the steady product scene does not meet
  the 60 FPS target.
- Copied JSON, standalone logs, and screenshots to
  `Artifacts/QA/GameplayPerformance` and published
  `docs/implementation-reports/gameplay-rendered-1024-profile-latest.md`.
- Started the population-agnostic dynamic work-slicing pass after the rendered profile
  showed a 46.564 ms scheduler slice.
- Confirmed that the existing adaptive scheduler changes fixed actor/path counts but cannot
  preempt one expensive synchronous decision.
- Added command-line-only detailed AI instrumentation to the standalone gameplay probe.
- Profiled the rendered 1024x1024 dense 500-character scene and traced the largest
  synchronous slices to survival environment capture, work target selection, and path
  destination resolution.
- Replaced synchronous facility-role scans with a shared, spatial-bucketed incremental
  shortlist and made facility actions resume after a deferred result instead of skipping.
- Moved work-target candidate scans and construction safety fast paths under the dynamic
  frame-work budget.
- Fixed editor profiling so direct `GameplayScene` PlayMode creates a real prepared
  owner/staff party instead of merely hiding the QA selection panel.
- Ran the 1024x1024, 500-actor, 4,096-facility profile entirely in Unity Editor PlayMode:
  frame p95 17.164 ms, AI scheduler p95 1.31 ms, path p95 0.345 ms, Error/Warning 0.
- Stored the report and game capture at
  `Artifacts/QA/GameplayPerformance/editor-playable-dynamic-500.json` and `.png`.
- Added a scene-scoped `CharacterPresentationScheduler` and registered it through VContainer.
- Removed independent character, nameplate, and feedback-bubble frame callbacks. Camera-visible
  presentation now updates every frame while offscreen visibility probes are staggered under
  dynamic frame headroom.
- Added presentation registered/visible counts to the gameplay performance report.
- Re-ran the 1024x1024 dense Editor PlayMode profile:
  - 500 actors: 11.253 ms average, 16.555 ms p95, 1.247 ms scheduler p95.
  - 1,000 actors: 10.909 ms average, 15.375 ms p95, 1.341 ms scheduler p95.
  - 1,000 actors registered, 59 visible/presented.
- Fixed the nameplate regression fixture for required clock/runtime dependencies and restored
  the intended rule that injured characters show a health bar outside active combat.
- Character nameplate and feedback-bubble regression scenarios both pass.
- Stored new reports and captures at
  `Artifacts/QA/GameplayPerformance/editor-presentation-scheduler-500.*` and
  `Artifacts/QA/GameplayPerformance/editor-presentation-scheduler-1000.*`.
- Added named profiler markers for the scene-scoped VContainer tickables and delayed raw
  profiler-frame capture so the reported sample matches the measured slow frame.
- Traced recurring 33 ms exit spikes to `CharacterSpawner` scanning all 1,048,576 cells for
  an absent entry door. Replaced the scan with the building registry plus the cached entrance
  grid position.
- Limited character-skill generation to two concurrent requests and one submission per tick,
  then cached each prepared prompt across queue rejection and transport retries.
- Removed per-character deprivation hot-path LINQ, enum arrays, temporary damage arrays, and
  four-actor minimum slices. Burdens are normalized once and accessed by stable enum index.
- Final real `GameplayScene` profile at 1024x1024, 900 dense facilities, 100 actors, and X5:
  7.475 ms average, 11.768 ms p95, 14.881 ms p99, 67.20 FPS 1% low, scheduler 1.660 ms p95,
  Console Error 0 / Warning 0.
- A follow-up raw trace confirmed the remaining >33 ms samples came from editor/probe work:
  the largest game tick was below 1 ms in that sampled frame and `WorkTargetSelector` maxed
  at 1.331 ms during the trace.
- Fixed a focused progression regression discovered after the performance pass. Regular
  employees were receiving the four fallback owner-fixed skills because
  `CharacterOwnerFixedSkillUtility` padded every character profile. The utility now returns
  no owner skills unless `CharacterSO.IsOwnerCandidate` is true, and the progression fixture
  explicitly guards that boundary.
- Re-ran progression, AI naturalness, grid foundation, owner, and dark-survival focused
  regressions successfully.
- Re-profiled the corrected build in real `GameplayScene` at 1024x1024, 900 dense facilities,
  100 actors, and X5: 7.317 ms average, 11.213 ms p95, 14.529 ms p99, 68.83 FPS 1% low,
  scheduler 1.803 ms p95, path broker 0.333 ms p95, Console Error 0 / Warning 0.
