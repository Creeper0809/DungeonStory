# Progress

- Added focused diagnostics for reserved loose stacks, deferred path searches,
  and genuine blocked routes; all eight flow-diagnostic scenarios pass.
- Converted the build placement verifier from direct `ApplyWork` state
  mutation to a pointer-driven, physical-material, natural-AI construction
  check.
- Injected runtime and restored construction sites through the scene resolver,
  and added a legacy one-General-material fallback. Work amount scenarios pass.
- Added a construction material replan signal and protected Priority1 work
  from forced interruption. The first natural rerun still failed because the
  queued haul preference did not become the next committed action.

- Created an isolated P0-P1 closure plan while preserving completed historical
  plans and the current dirty worktree.
- Began Phase 1 by inventorying current compilation, test, scene, and runtime
  verification entry points.
- Ran the existing release soak baseline. Runtime/save/performance/reservation
  checks passed, but the path-budget assertion failed because it observes the
  legacy scheduler counter instead of the path broker.
- Strengthened the release soak with broker path counters and per-actor
  unexplained-stationary, two-cell oscillation, and reservation churn bounds.
- Re-ran the 45-second soak successfully: all runtime, AI, path, reservation,
  save, performance, and capture checks passed.
- Added a teardown guard so nameplate camera clamping does not throw after the
  registered scene camera has already been released.
- Started Phase 2 by tracing work orders, item stacks, hauling priorities, and
  the Operations UI surface for player-readable blocker diagnostics.
- Added `GameplayFlowDiagnosticsQuery` and an Operations `작업·물류` section.
  It reports missing materials, missing haulers, delivery waits, missing work
  roles, blocked routes, work progress, and loose-stack backlog with explicit
  recovery guidance.
- Added five focused diagnostics scenarios. Missing material, no hauler,
  delivery wait, no work role, and in-progress formatting all pass in Unity
  with Console Error 0 / Warning 0.
- Extended the P0 UI verifier to require the generated `작업·물류` section and
  at least one live flow-state card on the Operations surface.
- Extended the release soak from a fixed 45 seconds to an observed two-day
  advance (up to 150 realtime seconds at X5), so a Day 1 run must reach Day 3.
  Work-order states, item-stack states, and flow-diagnostic summaries are now
  sampled alongside AI, path, save, and performance metrics.
- The first day-bound soak failed on Day 2 because a fresh prepared run had no
  food or water. Noa and Sion remained in hunger-breakdown replanning for over
  113 seconds, giving a concrete P0 starter-supply and AI ownership failure.
- Added transactional starter physical supplies at the world dropoff: food,
  water, general material, fuel, and medicine. They remain loose world stacks
  so normal hauling and storage rules still apply.
- Marked coroutine-driven deprivation breakdowns as externally driven brain
  actions. The brain now stops issuing duplicate decisions until the breakdown
  coroutine completes and explicitly requests a replan.
- Re-ran the day-bound soak: Day 1 to Day 3, save/load, AI bounds, and Console
  capture passed, but the new starter-haul assertion failed because every
  starter stack remained loose.
- Cleared legacy seeded warehouse inventory for prepared new runs and expanded
  flow diagnostics to distinguish no hauler, no warehouse, full/incompatible
  storage, and blocked dropoff-to-warehouse routes.
- The first UI rerun was blocked by a compile error in the new warehouse label
  fallback. The invalid helper call was replaced with `BuildingSO.objectName`
  plus the GameObject name fallback.
- The next compile found the P0 verifier's pattern-bound `flowSection` was not
  definitely assigned when reused for text capture. It now resolves the object
  first and computes visibility separately.
- A stale P0 request entered `SampleScene` after compilation and produced
  uninjected debug-scene component errors and TMP fallback warnings. That run
  was stopped and is not accepted as product evidence.
- Added the exact flow diagnostic titles and recovery text to the product
  release-soak report so the remaining loose-stack blocker can be identified
  without another ambiguous state-only failure.
- Re-ran the product soak successfully. Day 1 reached Day 3 in 71.4 seconds,
  starter stacks transitioned from `Loose` to `Stored`, all three actors
  progressed, save/load succeeded without warnings, and captured runtime
  errors/warnings remained zero.
- Refined logistics diagnostics so path-budget deferral is shown as
  `경로 계산 중` and already reserved loose stacks are shown as `운반 중`
  instead of a false hard route blocker.
- Made prioritized haul stacks outrank ordinary high-value stock and retain
  their priority across a cancelled reservation. The focused haul-plan
  contracts pass, including the cancellation retry.
- Added an urgent first path lookup for explicit immediate AI replans so a
  blocking player order cannot be starved by the normal per-frame path budget.
- Split ordinary hauling from blocking work-order hauling: a newly requested
  construction delivery may interrupt one worker, while ambient hauling still
  respects the configured work-priority order.
- Added targeted work-action preference by `WorkTypeId`. When the final
  material reaches a construction site, one worker now replans specifically
  into `Construct` instead of recommitting to unrelated work.
- The pointer-driven build verifier now passes the complete natural product
  path: site creation, physical pickup, facility-buffer delivery, accumulated
  work, final wall replacement, site removal, progress capture, and Console
  Error 0 / Warning 0.
- Added a work-type hint for the generic `AIWork` action so targeted replans
  choose Construct even when the character has no separate Construct action
  asset.
- Registered `WorkOrderRuntime` as a low-frequency tick source. A Ready
  construction order without a live site reservation retries one targeted
  worker replan per game second and stops retrying as soon as a worker attaches.
- Made partial work-order restoration distinguish durable state from runtime
  ownership: progress and delivered materials survive, while stale Ready or
  InProgress worker reservations are cleared and reacquired naturally.
- Extended the pointer build verifier with a partial-save checkpoint. It saved
  at 3%, restored the same progress and delivered material with zero warnings,
  resumed through normal AI, and completed the final wall replacement.
- Re-ran Work Amount, Haul Plan and Construction Safety, and Gameplay Flow
  Diagnostics focused regressions. All passed with Console Error 0 / Warning 0.
- Made haul reservations explicitly runtime-owned. Loading a save now clears
  transient stack reservations, and disabling an active haul action releases
  its plan instead of leaving the item permanently unavailable.
- Added destination-level construction material recovery. Cancelling a site
  returns undelivered stored stock to its source warehouse and releases
  delivered buffers as loose material at the site without changing quantity.
- Added low-frequency orphan detection for construction orders. A destroyed or
  missing site now cancels its work order and releases materials instead of
  leaving a progression-blocking reservation.
- Added and passed physical-item contracts for transient reservation restore
  and five-unit cancellation conservation. Work Amount scenarios also cover
  explicit cancellation and orphan-site recovery.
- Re-ran the full pointer build path after the recovery changes. It saved at 2%
  progress, restored the exact progress and delivered material, resumed through
  normal AI, completed the building, and captured zero errors or warnings.
- Corrected body-part combat ownership so ordinary limb damage is nonlethal at
  the aggregate-health layer while head, torso, and blood-loss rules remain
  authoritative in `CharacterBodyHealthRuntime`.
- Excluded intruders from medical rescue orders and made downing request
  immediate replans from eligible Rescue workers.
- Fixed defense guard pause ownership and orphan release so a completed
  invasion restores each guard's pre-defense pause state instead of leaving
  lifestyle AI disabled.
- Fixed `AbilityRescue` continuation across equivalent action replacement,
  allowing stabilization, physical carrying, and bed treatment to continue
  without a false cancellation.
- Extended the integrated defense verifier through real rally, exterior
  approach without dispatch, interior breach, three reciprocal exchanges,
  locked intruder position, and post-combat medical recovery.
- Isolated the verifier from survival deaths and naturally scheduled follow-up
  invasions. When combat ends without a downed guard, it now applies and reports
  a controlled nonfatal post-combat injury only after the real combat contract
  has passed.
- Integrated defense-medical verification passed: stabilization, physical
  carry, bed treatment, recovery, patient AI resume, rescuer AI resume, and
  Console Error 0 / Warning 0.
- Re-ran V14 Combat and Defense EditMode scenarios and the standalone V14
  PlayMode rescue/treatment regression. All passed with zero Console errors or
  warnings.
- Re-ran the full Day 1 to Day 3 release soak after the defense fixes. It passed
  AI progress, pending bounds, unexplained stationary time, two-cell
  oscillation, reservation churn, path budget, save growth, physical starter
  hauling, save/load, performance, and Console checks.
- The current AI maxima were 1.01 seconds unexplained stationary, 2 consecutive
  two-cell reversals, 7 reservation target changes, 5.67 seconds pending, and
  0.188 ms scheduler p95. Phase 5 therefore required no additional runtime
  repair.
- Began the Phase 6 UI audit. The existing P1/P2 feature run completed with
  15/18 rows passing and Console Error 0 / Warning 0; room, facilities,
  defense effects/reports, offense battle, staff, shop, codex, and event
  history all passed.
- Identified three stale verifier expectations for the refactored Operations
  and Defense presenters. Also confirmed the older resolution runner is not a
  valid reverse Gameplay-to-Title transition test because the gameplay scope
  survives that launch path.
- The first responsive rerun stopped on an active TMP component whose text was
  temporarily null during a Defense panel rebuild. The readability scan now
  treats that transient state as non-text instead of dereferencing it.
- Reworked the developer-button portrait layout so its compact 78-unit control
  sits in the top-bar gap instead of covering the wildlife toggle.
- Reworked the alert-dismiss regression to right-click the newest alert that is
  both visible and the top EventSystem raycast target, while still requiring
  history retention after dismissal.
- Re-ran the complete P1/P2 feature surface suite. All 21 checks passed,
  including pointer-driven desktop and portrait controls and alert right-click
  dismissal with history retention.
- Visually inspected the 1600x900 and 900x1600 captures. The developer button
  no longer covers the wildlife toggle, all upper-right controls remain
  readable, and the verifier captured Console Error 0 / Warning 0.
- The first final-regression batch did not establish a clean ProductShell start:
  title DI resolved, but the fresh title UI was absent and the verifier carried
  on into preparation/gameplay. This run is rejected as shell evidence and will
  be repeated from a reset TitleScene entry.
- Removed the gameplay-scene aggregate dependency from
  `DungeonSettingsUiController`. The shared settings runtime targets now carry
  an optional `GameManager`: title settings build without gameplay references,
  while gameplay settings retain the existing pause-state behavior.
- Updated ProductShell verification to create recruit candidates through the
  real character-spawn/population path, preserving their permanent ID.
- Updated the offense pointer path to select a target, wait for the attack
  preview, and click `공격 확정` before asserting damage and command progress.
- The next ProductShell run confirmed the attack-preview path now passes with
  Console 0/0. The real character spawner still declined the recruit fixture
  without a warning, so the remaining failure is being traced through its
  customer/population/entrance eligibility gates.
- Completed the ProductShell pointer regression from a clean TitleScene.
  Settings, difficulty, preparation, gameplay, real recruitment, expedition
  eligibility, offense attack confirmation, and exact battle save/restore all
  pass with Console Error 0 / Warning 0.
- Updated the changed-UI batch so StartParty opens the dedicated
  `StartPreparationScene` instead of the obsolete gameplay fallback surface.
- Added actual pointer drag verification for selected/reserve roster exchange.
  The promoted reserve keeps the selected party slot and contextual reroll
  controls remain usable after the swap.
- Corrected the final preparation contract to verify four fixed owner skills
  separately from the generated active/passive pair retained by each selected
  employee.
- Re-ran StartParty and SkillRuntime together. Both passed, including desktop
  1600x900, portrait 900x1600, automatic starting skills, gameplay commit, and
  runtime management/defense/offense skill effects.
- Phase 7 and the P0-P1 gameplay closure are complete. All accepted focused,
  integrated, pointer, responsive, soak, save, defense, combat, logistics, and
  product-shell reports record zero errors and zero warnings.
