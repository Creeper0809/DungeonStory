# AI Performance Hardening

## Applied

- Character AI decisions are driven through `CharacterAiScheduler` with per-frame decision and path-search budgets.
- The scheduler can adapt budgets downward when AI processing exceeds the configured target time.
- Offscreen movement updates can be spread across multiple frames.
- Facility candidates are cached by grid version and facility dynamic-state version.
- Visitor action destination selection can choose the best candidate without allocating an intermediate candidate list.
- Grid path search reuses temporary search buffers and avoids per-cell occupant list allocations in hot loops.
- Character feedback text views are pooled and limited to high-detail characters.
- The 500 NPC stress world uses multiple floors and stair traversal links.
- Play Mode AI profiling writes a JSON report to `docs/implementation-reports/ai-play-mode-profile-latest.json` and a raw profiler log under `Temp/`.

## Job/Burst Candidates

1. `Grid.SearchPath`
   Batch multiple path requests by start position and grid version. Keep Unity object access outside jobs; feed jobs compact cell/link arrays.

2. Facility scoring
   Convert static facility role/preference data into plain arrays and score candidates in batches. Keep dynamic checks such as stock and capacity on the main thread until data ownership is clearer.

3. Offscreen movement
   Move purely positional interpolation for low-detail NPCs into a batched updater. Preserve interaction/coroutine state changes on the main thread.

4. Crowd and queue pressure
   Track facility crowd pressure in compact counters so AI scoring does not repeatedly query MonoBehaviours.

## Profiling Rule

Use the Play Mode profile report for regression comparison, but use Unity Profiler or a Development Build capture before moving any system to Jobs/Burst.
