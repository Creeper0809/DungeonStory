# Progress

## 2026-07-26

- Started the weighted-navigation and 500-character performance pass.
- Read the existing Grid BFS, frame-local broker, terrain model, movement loop, AI scheduler,
  and latest 100-character profile.
- Confirmed that unweighted BFS cannot represent shallow-water, traversal, door, hazard, or
  congestion travel costs.
- Chose a compatibility-first migration with reachability, point path, multi-target cost
  field, and shared-destination query types.
- Audited the dirty worktree and recorded that all unrelated V16 changes must be preserved.
- Confirmed no direct Burst/Collections package dependency and deferred the execution backend
  until the pure navigation contract compiles and passes focused tests.
- Added integer traversal costs, weighted Dijkstra fields, exact-destination A*, navigation
  terrain versioning, shallow-water movement timing, and a bounded multi-frame broker cache.
- Added focused contracts for weighted detours, terrain-only invalidation, and cross-frame
  cache reuse.
- Unity refreshed and compiled the project successfully with the new navigation source.
- `GridFoundationDebugScenarios.RunAll(false)` passed, including the new weighted path,
  terrain invalidation, and broker reuse contracts.
- Cached arbitrary-horizontal traversal presence on `Grid`, removing a whole-grid scan from
  every A* heuristic evaluation.
- Added a hard four-search urgent overdraft above the normal per-frame path budget so
  simultaneous emergencies cannot create an unbounded navigation spike.
- Converted work-target distance scoring from path cell count to normalized traversal cost.
- Added a focused regression proving urgent requests stop at the normal budget plus the
  bounded overdraft.
- Found the 500-character setup spike: registration eagerly configured every Behavior
  Designer tree and the scheduler configured trees before checking their due time.
- Changed registration to O(1) bookkeeping and deferred BT configuration into the existing
  per-frame decision budget; due-time rejection now runs before BT configuration.
- Added allocation-free destination path extraction and stopped collecting visitable
  occupants during exact A* searches.
- Routed wildlife movement, captive/wildlife escort, rescue, and hauling fixed destinations
  through the exact A* broker API.
- Made reachable/visitable/distance lookup sets lazy so point paths no longer allocate four
  unused managed collections, and removed the obsolete BFS visit-mark grid.
- Replaced the scheduler's per-frame actor scan with a due-time min-heap and immediate dirty
  scheduling. Action completion now wakes dormant actors without polling all 500 actors.
- Added bounded heap compaction so repeated urgent replans cannot grow stale schedule entries
  without limit during long runs.
- Replaced per-character Behavior Designer graph deserialization in the hot path with the same
  ordered code-root BT pipeline. The selected actor can still materialize the serialized graph
  for editor visualization.
- Changed the 500-character profile to create eight actors per frame and made synchronous
  validation refuse counts above 100, preventing another editor main-thread lock.
- Converted Grid path storage from `Dictionary<Vector2Int, ...>` to compact cell-index arrays.
  Search expansion now uses value-type traversal descriptors and creates `GridMoveStep` objects
  only for the final returned route.
- Added explicit 500-character profile gates for frame p95 <= 16.67 ms, scheduler p95 <= 4 ms,
  and average frame allocation <= 64 KB, including machine information in the JSON report.
- Throttled offscreen nameplates, feedback/dialogue bubbles, and traversal visibility recovery
  to staggered eight-frame lanes while preserving immediate combat and visible feedback.
- Rebuilt both `Assembly-CSharp` and `Assembly-CSharp-Editor` with Unity's Roslyn compiler after
  the changes; both compile with zero errors.
- Unity Editor recovered from the accidental synchronous stress run without being killed, but
  the Unity MCP direct connection remains occupied and times out even for console reads.
- Converted `GridMoveStep` from a heap object into a readonly value carrying an explicit
  `IsValid` bit, then updated movement, invasion, rally, and editor contracts accordingly.
- Exact-destination A* now rents and reuses its parent/cost/search arrays and retains only the
  compact final route plus total cost in the cached result.
- Added an editor-independent Mono navigation benchmark for the production 96x3 grid shape.
  Ten thousand weighted A* searches with route materialization completed in 112.9 ms
  (11.3 microseconds/query); allocation fell from 10.8 KB to 3.0 KB/query, and the weighted
  shallow-water detour contract passed.
- Reused each job giver's already-built decision context while scoring actions and added
  context-aware action preparation, avoiding repeated need, schedule, and visitor-state
  queries.
- Cached composite facility role candidate sets and split performance recording into action
  start, destination resolution, candidate source, and candidate loop categories.
- Found and fixed an Editor-only benchmark defect: its grid provider searched the scene on
  every candidate lookup instead of retaining the fixture manager.
- Added a same-world scheduling-disabled baseline so Editor allocation noise is not falsely
  attributed to 500-character AI.
- Ran the focused Grid and 100-character navigation regression successfully.
- Ran the staged 500-character PlayMode profile for 600 sampled frames. It passed behavior,
  60 FPS, scheduler, path-budget, and allocation gates: frame 3.39/4.37/15.40 ms
  average/p95/max, scheduler 1.228/1.809/2.580 ms, and no frame above 16.67 ms.
- Kept pathfinding on the main thread for the current world size because measured A* work is
  below per-request Job scheduling overhead. The APIs and immutable cost model leave a clean
  boundary for future batched worker execution if larger maps make it profitable.
