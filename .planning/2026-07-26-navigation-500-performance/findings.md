# Findings

## Baseline

- `Grid.SearchPath` is a full reachable-cell BFS with equal edge cost.
- Every search creates result dictionaries/lists and the result creates a reachable-position
  hash set.
- Horizontal neighbors and explicit traversal links are the only graph edges. The graph is
  therefore floor/row oriented and is a strong candidate for a region/portal hierarchy.
- `GridCell.TerrainMoveSpeedMultiplier` reports `0.65` for shallow water, but no runtime
  consumer currently uses it for path selection or movement duration.
- `GridPathSearchBroker` caches only within the current frame and clears all entries at the
  next frame.
- The latest 100-character profile recorded 43 broker searches, 0 cache hits, frame p95
  3.423 ms, scheduler p95 0.497 ms, and Editor-wide GC around 182 KB/frame.
- Runtime code has 64 direct broker/search path calls outside Editor verification code, so
  migration must classify query intent instead of performing a blind signature replacement.
- The worktree already contains extensive V16 gameplay changes. Navigation edits must remain
  localized and must not revert or reserialize unrelated assets/scenes.
- Burst, Collections, and Jobs are not direct project manifest dependencies. The first
  checkpoint should provide a pure-data search core and stable API before selecting a concrete
  parallel backend.
- Current scheduler budgets are 16 decisions and 8 path searches per frame, with visible
  decisions at 0.35 seconds and offscreen decisions at 1.5 seconds.
- The current graph expands left/right neighbors plus explicit stair/elevator/teleport links.
  Region spans separated by structural blockers can substantially reduce high-level search.
- GameplayScene and SampleScene currently use a 60 by 3 Grid. At this size, repeated result
  construction and duplicate searches are at least as important as raw node expansion count.

## Implementation Findings

- The 500-character lock was dominated by eager per-actor Behavior Designer graph setup, not
  the 60x3 navigation graph. A staged actor creation profile and code-root BT execution are
  required before pathfinding numbers are meaningful.
- A 60x3 production Grid and 96x3 stress Grid are too small for one Job/Burst dispatch per path
  to be a reliable win. Main-thread A* with an eight-search budget is the correct first backend.
- Safe future parallel candidates are immutable-snapshot batch scoring and offscreen path
  batches. Unity objects, door access, reservations, and route commit must stay on the main
  thread and reject results whose navigation version changed.
- Exact destination searches do not need occupant discovery or full reachability collections.
  Compact cell-index arrays remove hash probes and neighbor `GridMoveStep` allocations while
  preserving full Dijkstra fields for facility/work candidate selection.
- A dirty/due min-heap is necessary for 500 logical actors. Polling every actor each frame just
  to discover that no decision is due defeats tick dilation.
- Presentation LOD matters even when AI is budgeted: per-character `LateUpdate` calls that
  perform camera projection or instantiate bubbles can become the next bottleneck after AI.
- `FacilityWorkType` remains a valid serialized migration enum. The structural rule is to stop
  extending gameplay dispatch through it, not to remove every enum indiscriminately.
- A 60 FPS claim must be attached to a recorded machine and standalone Player scenario. The
  profile now separates behavior validity from performance validity and records both.
- Exact A* working arrays are temporary implementation state, not result state. Retaining them
  in every cached result cost about 6.7 KB per query on a 96x3 grid; renting the workspace and
  retaining only the route reduced full path materialization allocation by roughly 72%.
- On the current 96x3 side-view world, optimized weighted A* is about 11 microseconds per
  request in the standalone managed benchmark. A job per request would be finer-grained than
  its scheduling cost; parallel work should batch many immutable decision/scoring snapshots.
- The apparent final 500-character bottleneck was in the Editor stress fixture, not the
  production registry. `EditorGridSystemProvider` called `FindFirstObjectByType` for every
  facility candidate-source lookup. Caching the fixture's `GridSystemManager` reduced the
  120-frame diagnostic from 13.61 ms average to 2.60 ms average and removed all 16.67 ms
  misses.
- The final 600-frame 500-character profile recorded 3.39 ms average frame time, 4.37 ms p95,
  15.40 ms maximum, and zero frames over the 60 FPS budget. All 500 actors were registered,
  had actions, and ticked their behavior trees.
- Scheduler average/p95/max were 1.228/1.809/2.580 ms. The broker served 527 searches and
  8,674 cross-frame cache hits while respecting the seven-search observed maximum and bounded
  deferral policy.
- Editor-wide allocation includes import, profiler, and GUI noise. The profile therefore
  measures the same 500-actor world with scheduling disabled and subtracts that baseline;
  the resulting incremental average was 36.0 KB/frame.
- Broad worker-thread conversion is not justified by the measured workload. Unity object
  access, path authorization, reservations, and route commit remain on the main thread.
  Future parallel candidates are immutable-snapshot batches for offscreen utility scoring or
  many paths requested together on substantially larger maps.
