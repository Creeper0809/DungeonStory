# Findings

## Baseline

- The older UI surface audit predates several current product systems and cannot
  prove present-day P1 UI coverage.
- The repository has many focused debug scenarios, but current completion needs
  a fresh integrated player-path baseline.
- Existing dirty files belong to recent terrain, hierarchy, event-dismiss, and
  architecture work and must be preserved.
- Unity starts from `GameplayScene` with compilation idle and Console Error 0 /
  Warning 0.
- The existing 45-second release soak currently fails only
  `AI_PATH_BUDGET`: actors changed state and moved, but the verifier sampled
  only `LastPathSearchCount` and reported zero. The scheduler separately
  exposes broker searches, cache hits, and deferrals, which the verifier does
  not consume.
- The existing naturalness observer runs for 18 realtime seconds and detects
  large jumps, action churn, blocked phases, and suspicious labels. It does not
  prove the requested 10-second unexplained-stationary bound or repeated
  two-cell oscillation during a longer run.
- The standalone dungeon-player bridge is not currently available because no
  player automation `bridge.json` exists. Editor PlayMode remains the immediate
  verification path.
- The strengthened 45-second release soak passes with 815 broker searches,
  maximum 4 searches in a frame, 40 budget deferrals, 0 seconds unexplained
  stationary time, one two-cell reversal, and at most 9 reservation target
  changes. Save round-trip, frame time, AI p95, allocation, and captures also
  pass.
- The release verifier previously sampled only the legacy scheduler path
  counter. It now records scheduler searches, broker searches, cache hits, and
  budget deferrals, and guards long waits, two-cell oscillation, and reservation
  churn per actor.
- PlayMode teardown can unregister the scene camera before a door trigger
  finishes refreshing a nameplate. `WorldCharacterNameplate` must treat this as
  an expected teardown state instead of surfacing an exception.
- Work orders already expose enough structured state to explain most silent
  stalls, but that information previously stopped at runtime data. The new
  diagnostics layer converts it into player-facing cause and recovery text
  without moving execution logic into the UI.
- `Camera_Capture` correctly validates the 2D world camera but intentionally
  excludes Screen Space Overlay HUD. HUD evidence must come from an end-of-frame
  GameView screenshot after the target tab is opened.
- A 45-second X5 soak advances only about one operating day in the current
  gameplay clock. First-three-day evidence therefore needs a day-bound runner,
  not another fixed short timeout.
- The first day-bound run exposed a real fresh-run softlock: prepared parties
  received no physical food or water, two staff entered hunger breakdown, and
  `RunDesperateEat` repeatedly finished without relief and requested another
  decision. Game time stopped on Day 2 before the Day 3 target.
- A deprivation coroutine owns the character after it starts, but the brain
  previously remained decision-pending. The scheduler therefore kept treating
  the same breakdown as a new decision instead of waiting for the coroutine to
  finish and request its own replan.
- Prepared new-run warehouses still contained legacy abstract seeded stock,
  leaving no capacity for physical starter supplies. Clearing that seed only
  for prepared new runs lets the same warehouses receive real hauled stacks.
- The corrected run observed `Loose` and `Stored` states and moved the 60-unit
  starter supply through normal hauling. A temporary no-plan sample can still
  mean path-budget deferral or an already reserved stack, so it must not be
  shown as a hard blocked route.
- Runtime-created construction sites initially missed VContainer injection, so
  their work target and material delivery handlers were absent. Placement and
  restore paths now inject the created `BuildableObject` before work begins.
- Legacy buildings without an authored work-amount module now request one unit
  of General material. The fallback is deterministic and covered by the work
  amount scenarios.
- A natural pointer-driven construction run exposed a second logistics
  starvation case: all three prepared workers were performing Priority1
  `Operate`, while material hauling is Priority2. The original one-shot
  replan rejected all workers and never retried.
- Preserving a haul preference until the next work shift is necessary but not
  yet sufficient. A 45-second natural run still observed the construction
  destination stack and valid haul candidates but no reservation or delivery;
  the selected worker returned to the generic work action instead.
- The natural build gap had three separate causes rather than one scoring bug:
  immediate replans still used normal path-budget priority, prioritized stacks
  lost their flag when a reservation was cancelled, and a Ready work order
  requested only a generic AI replan instead of the requested work type.
- Construction now has verified product-path evidence through pointer input:
  warehouse stock remains physical, one worker carries it to the site, work
  units accumulate, and the final building replaces the site.
- Item and work reservations are runtime ownership, not durable save ownership.
  Persisting them without the corresponding active action produces invisible
  blockers after load, so restore now preserves quantities and progress while
  requiring AI to reacquire ownership.
- Construction cancellation previously ignored its refund flag and removed both
  reserved and delivered material. Destination-aware release now conserves the
  exact quantity and preserves whether the material returns to source storage
  or remains loose at the former work site.
- Body-part combat damage must own downing and lethal head/torso/blood-loss
  outcomes. Applying the same hit through the legacy aggregate-health death
  path killed guards before the medical lifecycle could begin.
- Defense pause ownership was not explicit. Releasing an engagement could
  either leave guards paused forever or incorrectly clear a pause owned by
  another system; the runtime now records and restores each guard's prior pause
  state.
- Rescue actions can legitimately outlive the exact `AIActionSet` instance that
  selected them. Treating action-object replacement as cancellation stopped
  stabilization and transport midway through an otherwise valid rescue.
- A single naturally tuned invasion is not deterministic evidence for both
  combat and medical recovery: body-part rolls may end with no downed guard.
  The integrated verifier now proves real rally, breach, adjacency, reciprocal
  damage, and three exchanges first, then reports whether medical recovery came
  from a natural combat down or an explicit controlled post-combat injury.
- The completed integrated pass observed stabilization, physical parented carry,
  bed treatment, recovery hysteresis, and both patient and rescuer resuming AI.
  Focused Combat, Defense, and standalone V14 PlayMode regressions also pass
  with Console Error 0 / Warning 0.
- The post-defense Day 1 to Day 3 product soak remained within every AI bound:
  maximum unexplained stationary time 1.01 seconds, two-cell reversals 2,
  reservation target changes 7, and pending time 5.67 seconds.
- The same run used at most three broker path searches in a frame, kept
  scheduler p95 at 0.188 ms, preserved starter physical hauling and save/load,
  and captured zero warnings or errors.
- Several report and AI status strings are still visibly mojibaked even though
  runtime behavior is valid. These are presentation defects and belong in the
  current pointer/UI audit rather than being accepted as readable blocker text.
- The current P1/P2 surface verifier passed 15 of 18 checks with Console 0/0.
  Its three failures are stale contracts: it still searches for removed
  monolithic run/threat objects and economy-history buttons instead of the
  registered Operations and Defense presenters.
- The legacy resolution-matrix runner cannot be started by loading TitleScene
  from an already running GameplayScene. The surviving gameplay scope causes a
  missing `DungeonSceneRuntimeReferences` registration in the title scope. It
  must enter PlayMode from the title scene, or the gameplay-only responsive
  audit must remain scene-local.
- Existing feature-surface helpers invoke `Button.onClick` directly for many
  state fixtures. That is valid for model regressions but not evidence for
  raycast order, UI input blocking, or portrait reachability. A separate
  pointer/raycast responsive pass is required.
- The pointer-driven 900x1600 capture exposed a real collision: the center-top
  developer button covered the wildlife toggle even though both controls were
  otherwise inside the screen. Portrait layout must reserve the narrow gap
  between the time block and upper-right control strip.
- Event alerts can remain active in the hierarchy while clipped outside the
  visible alert stack. A right-click regression must choose the first button
  that actually wins the EventSystem raycast, not merely the newest active
  record.
- After the responsive fix, the same pointer suite passes 21/21 at both
  1600x900 and 900x1600. Wildlife, Operations, Defense, and Save all receive
  real EventSystem clicks; right-click dismissal removes only the HUD entry and
  preserves its event-history record.
- Both final responsive captures are nonblank and keep the upper-right controls
  reachable. No visible label matched the mojibake scan, and the run captured
  Console Error 0 / Warning 0.
- The ProductShell failure revealed a real composition leak: the shared
  Settings controller depended on gameplay-wide
  `DungeonSceneRuntimeReferences`, so the title scope could resolve its own DI
  container but failed while dispatching entry points. Settings needs only an
  optional `GameManager`, which belongs on its existing settings runtime-target
  bundle instead of the gameplay scene aggregate.
- Once title composition was repaired, ProductShell reached every scene with
  Console 0/0. Its only two remaining failures were stale verifier fixtures:
  the recruit candidate bypassed `CharacterSpawner` and therefore had no
  persistent ID, and the combat check skipped the current attack-preview
  confirmation step.
- ProductShell now passes the full pointer path from Title settings and
  difficulty through StartPreparation and Gameplay. Recruitment uses the real
  spawn/population path, the recruited actor is both staff and expedition
  eligible, offense damage is applied through attack confirmation, and the
  exact battle state survives save/restore.
- The dedicated StartPreparation regression now performs a real pointer drag
  between a selected employee and a reserve. The promoted reserve occupies the
  original party slot, contextual dice rerolls remain reachable, and both
  desktop and portrait captures stay inside the viewport.
- The owner intentionally starts with four fixed owner skills instead of a
  generated level-one active/passive pair. Both selected staff retain one
  generated active and first passive after the gameplay scene commit.
- The final changed-surface regression set passes SaveUi, UnifiedUi,
  BuildPlacement, CharacterClick, RoomInspection, ExpeditionEquipment,
  PhysicalItemPile, PhysicalItemLogistics, ProductShell, StartParty, and
  SkillRuntime. Every accepted verifier report records Error 0 / Warning 0.
