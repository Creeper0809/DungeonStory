# Full Game Manual Verification

## Purpose

This document tracks the new full-game verification goal: prove, in PlayMode, that every implemented gameplay feature can be exercised and observed in game.

As of 2026-07-17, the implemented feature catalog has direct PlayMode evidence, public UI regression coverage, clean Development/Release builds, and standalone player smoke evidence. Earlier probe logs remain as historical supporting detail; the current completion claim uses the latest reports under `Temp/`.

The public UI caveat is closed by [UI Feature Surface Gap Audit](ui-feature-surface-gap-audit.md): P0 and P1/P2 player-facing surfaces, room inspection, click priority, product shell, save UI, and unified UI all have direct pointer and visible-state evidence. Human pacing and fun remain outside automated proof.

## Completion Rule

The goal can be called complete only when every feature row below has one of these final states:

- `Verified in game`: exercised in PlayMode through a game-facing scene/UI/runtime path, with console state and visual evidence where UI is involved.
- `Verified by PlayMode debug world`: exercised in PlayMode through a debug scenario because the feature has no current scene hookup; this must be called out as a limitation.
- `Accepted limitation`: intentionally not reachable in the current game scene, with source evidence and user-accepted scope.
- `Failed then fixed`: failed in PlayMode, fixed, and re-tested with evidence.

`Automatic scenario pass` alone is not enough to claim `Verified in game`.

## Evidence Levels

| Level | Meaning |
|---|---|
| `Not started` | No direct PlayMode pass for this feature in the new full-game QA goal. |
| `Automatic scenario coverage` | A `*DebugScenarios` suite exists and can seed checks, but manual/in-game evidence is still needed. |
| `Subset verified` | Previous DI/refactor pass touched part of this feature, but not the full gameplay feature. |
| `Verified in game` | Direct PlayMode evidence exists through scene/UI/runtime behavior. |
| `Missing scene hookup` | Logic exists, but the current scene lacks runtime objects/UI to exercise it as a game feature. |
| `Failed` | A real issue was found and needs fixing/re-test. |

## Scene Scope

| Scene | Role | Current Evidence |
|---|---|---|
| `Assets/Scenes/SampleScene.unity` | Only enabled build scene | PlayMode verifies composition, public UI, runtime paths, save/load, and run completion. Current Development and Release players build this scene and remain alive through separate 25-second standalone smokes with zero critical log matches. |
| `Assets/Scenes/CharacterAiTestScene.unity` | AI/test verification scene | PlayMode startup/core UI baseline plus injected character-AI feature probe verified several AI/persona/visual subpaths. The 2026-07-13 Core/Grid/UI completion probe also verified the active scene composition root, grid reachability, camera clamp, touch guard, and building-info panel open/close with visual evidence. Many non-AI feature-family runtimes are absent by scene design. |

## Automatic Scenario Inventory

Current scan found 31 `*DebugScenarios.cs` files with 248 named scenarios, plus `RefactorFollowupDebugScenarios`. These seeded the QA pass; the matrix below now records direct PlayMode evidence for the implemented feature catalog.

## Manual QA Matrix

| ID | Feature | Primary Sources | Existing Automatic Coverage | Current Manual Status | Required In-Game Evidence |
|---|---|---|---|---|---|
| CORE-01 | Build scene startup and composition root | `SampleScene.unity`, `DungeonRuntimeLifetimeScope` | None as a full build-scene pass | Verified in PlayMode and standalone players | The completion audit verified one active lifetime scope, the grid, generated facilities, and UI with captured errors 0. Current Development and Release executables additionally start this scene and survive 25-second standalone smoke runs with zero critical matches. |
| CORE-02 | Test scene startup and active gameplay objects | `CharacterAiTestScene.unity` | Refactor follow-up smoke | Verified in game for scene startup; feature runtimes absent | Re-enter PlayMode, confirm scene role, root runtimes, console clean. |
| CORE-03 | Camera movement, pointer input, UI touch guard | `CameraManager`, `PlayerInputServices`, `UIManager` | Refactor follow-up smoke partially | Verified in game runtime path | Strict public pointer placement verified Input System pointer coordinates through `UnityPlayerInputReader`: pointer resolved `(21, 0)`, `placed=True`. The 2026-07-13 Core/Grid/UI probe verified active `CameraManager.ClampToCurrentBounds()` on the injected scene camera (`clampChangedExtreme=True`) and `UIManager` touch guard block/release (`blocked=True`, `released=True`) with 0 captured errors. |
| CORE-04 | Pause and game speed | `GameManager` | None found as direct scenario | Verified in game | Toggle pause/speed in PlayMode and verify `Time.timeScale` and visible runtime behavior. |
| GRID-01 | Grid foundation, pathing, stairs, hallways, rooms | Grid/Rooms scripts and debug suites | Automatic scenario coverage | Verified in game runtime path plus PlayMode debug-world foundation coverage | The 2026-07-13 Core/Grid/UI probe ran in `CharacterAiTestScene` PlayMode and reported `GridFoundationDebugScenarios.RunAll(false)=True`, covering walk path, stair path, entry/exit path, unreachable exclusion, tileless building rendering, repeated ghost sprites, and initial room footprint hallway layering. The same active-scene probe resolved grid `32x3`, found reachable pair `(1, 0)->(30, 2)`, `reachableCount=84`, `destinationWalkable=True`, with 0 captured errors and console 0. |
| GRID-02 | Build mode, category buttons, building select buttons, ghost | Grid UI/building scripts | Automatic scenario coverage partially | Failed then fixed / verified public pointer placement | Closing-gap probe used the active `SampleScene` grid and placement service to place and destroy `P1_SpikeTrap` at `(21, 0)`. Public build UI retest fixed generated-button listener, close-state, and unlock-refresh bugs; category buttons, generated building selection, Build mode, ghost visibility, buildable position `(21, 0)`, and close reset to None/ghost hidden are verified. The strict pointer retest fixed `UnityPlayerInputReader` to prefer `Mouse.current.position` so InputSystem click and pointer coordinates use the same backend; public UI selection plus `TriggerPlaceBuilding()` then resolved `(21, 0)` and placed successfully: `before=97`, `after=98`, `placed=True`, console 0. |
| GRID-03 | Building info panel open/close | `UIBuildingInfo` | Refactor follow-up smoke | Verified in game | The 2026-07-13 Core/Grid/UI probe used the active scene `GridUIManager.buildingInfoUI` and a live `BuildableObject`, opened the panel (`opened=True`, non-empty `displayedName`, `touchBlocked=True`), captured `Temp/full-game-qa-core-grid-ui-completion.png`, then closed it (`closed=True`, `touchReleased=True`) with 0 captured errors. |
| ECON-01 | Money, stock cost, and basic economic flow | `GameManager`, facility shop, run variables | Automatic scenario coverage | Verified in game runtime path | `SampleScene` feature-family probe purchased a daily facility offer and confirmed money delta `50000 -> 49880`; remaining-feature pass also verified shop/warehouse stock movement `shop 16 -> 5`, `warehouse 80 -> 75`. Visible HUD captures were taken during the passes. |
| ECON-02 | Operating day settlement | `OperatingDaySettlementRuntime` | Automatic scenario coverage | Verified in game | `SampleScene` feature-family probe triggered day 7 settlement: report present, revenue 123, 1 facility revenue row, 1 event row. |
| ECON-03 | Event alerts, choices, notice feed | `EventAlertRuntime`, alert UI factories | Automatic scenario coverage | Failed then fixed | Probe created alerts, opened detail, executed a choice callback, and captured visible detail UI. Visual pass found alert detail behind offense canvases; fixed runtime alert sorting canvas and re-captured. |
| AI-01 | Customer AI action selection and facility visits | Customer AI scripts | Automatic scenario coverage | Verified in game runtime path | `CharacterAiTestScene` injected probe confirmed a VContainer-injected customer is scheduler-registered and processed by BehaviorTree without direct fallback: `schedulerRegistered=5`, `schedulerProcessedMax=1`, `behaviorTicksMax=1`, `treeTicks=0->1`, `scheduledDecided=True`, `directFallback=False`. The 2026-07-13 long observation probe then completed real movement/visit evidence: `planned=True`, destination `훈련장`, `moved=True`, `visited=True`, `visitCount=1->0`, `visitedBuildings=0->1`, 0 captured errors. |
| AI-02 | Staff AI work selection and ongoing work stability | Work priority and AI scripts | Automatic scenario coverage | Verified in game runtime path | Remaining-feature pass had 4 actors/4 brains, 2 `CanRunAi` staff, and 2 direct work decisions. Character-AI probe confirmed a priority work target can be set and assigned. The 2026-07-13 long observation probe confirmed real ongoing work stability: work candidate `훈련장`, `workType=Operate`, `workStarted=True`, and fatigue tick changed `sleep=100->97`, `mood=100->99`, 0 captured errors. |
| AI-03 | Owner selection, owner AI, priority command, death/run end | Owner scripts and UI | Automatic scenario coverage | Verified in game runtime/UI path | Completion audit verified the real `OwnerSelectionPanel` and `OwnerRunManager`: `panel=True`, `panelBuilt=True`, `panelButtons=7`, `candidates=3`, `selectedByIndex=True`, `ownerSpawned=True`, `ownerDeathApplied=True`, `runEnded=True`, `metaResult=True`, and `runResultPanels=1`. It also verified `OwnerCommandController` after adding the missing scene object: `controller=True`, `selectedByController=True`, `commandInvoked=True`, `commandSet=True`, and `destinationResolved=True`. |
| AI-04 | Duty/off-duty, fatigue, wait/wander behavior | Staff duty scripts | Automatic scenario coverage | Verified in game runtime path | Character-AI probe confirmed low condition blocks work start, triggers off-duty, starts the staff visitor path, and returns to work after recovered conditions with QA-aged elapsed time: `offDutyVisitorDuringOffDuty=True`, `returnReadyAfterRecover=True`, `canStartAfterRecover=True`, `returnedToWorkAfterRecover=True`. The 2026-07-13 long observation probe confirmed idle wander and real-time work fatigue: `wanderPathFound=True`, `wanderStarted=True`, `wanderMoved=True`, `fatigueChanged=True`, `sleep=100->97`, `mood=100->99`, 0 captured errors. |
| AI-05 | Local LLM queue, persona, dialogue, mood impulse | AI/LLM runtime scripts | Automatic scenario coverage | Failed then fixed / verified real endpoint response | Character-AI probe confirmed queue presence and fake-success application for persona trait, mood impulse, and macro intent. Closing-gap rerun after the `CharacterDialogueRuntime` fix confirmed direct dialogue bubble display: `dialogueShown=True`, `lastDialogue=QA visible line`, active text count `16 -> 18`. The 2026-07-13 isolated endpoint retest used the real `LocalLlmRequestQueue` in `SampleScene` PlayMode after `AbortAllForDebug()`, sent a high-priority persona request to the configured Ollama-compatible endpoint, and received `status=Succeeded`, `content={\"qa\":\"pong\"}`, with 0 captured errors and 0 console warnings/errors. |
| CHAR-01 | Character model, species, traits, runtime profile | Character model scripts/assets | Automatic scenario coverage | Verified in game runtime path | Completion audit verified the selected owner model/profile path in `SampleScene`: `profileConnected=True`, `species=Slime`, `speciesPresent=True`, `traitCount=1`, `traitsPresent=True`, and `statsFromProfile=True`. Earlier Character-AI probes also verified visual renderer and feedback components. |
| CHAR-02 | Character feedback bubble and social memory | Feedback/social scripts | Automatic scenario coverage | Verified in game runtime path, visual capture inspected | Earlier pass verified social memory and feedback-bubble public APIs. Closing-gap rerun confirmed `CharacterFeedbackBubble.Show(Joy)` and `CharacterDialogueRuntime.ShowLine()` in `SampleScene`: `feedbackShown=True`, `dialogueShown=True`, active text count `16 -> 18`. The capture confirms HUD/UI readability; the runtime text-count/report is the direct dialogue evidence because `ScreenCapture` is asynchronous. |
| INV-01 | Inventory logistics, warehouse stock, early supply | Facility/stock/operation scripts | Automatic scenario coverage | Verified in game runtime path | Remaining-feature pass cleared a scene shop stock for QA and restocked 5 items from a scene warehouse: shop `16 -> 5`, warehouse `80 -> 75`, message `5개 보충`, 0 errors. |
| SHOP-01 | Daily facility shop and basic purchase unlocks | Facility shop scripts/UI | Automatic scenario coverage | Verified in game runtime path | Daily shop runtime refreshed 4 offers and purchased `P1_ResearchLab`, verifying cost/unlock event path; a dedicated shop UI surface is still not present. |
| RESEARCH-01 | Blueprint research queue/progress/completion | Research scripts | Automatic scenario coverage | Verified in game | Queued `BP_ArcaneBasics`, applied work through the scene `연구실`, completed research, and triggered downstream codex/event paths with 0 errors. |
| SYN-01 | Facility synthesis and synthesis UI | Synthesis scripts/panel | Automatic scenario coverage | Verified in game | Matched 2 scene materials to `RS_BattleDining`, executed synthesis, and produced `2성 전투 식당` in `SampleScene` PlayMode. |
| EVO-01 | Facility lineage/context evolution | Facility evolution scripts/panel | Automatic scenario coverage | Verified in game with QA-prepared active-scene candidate | `SampleScene` timed-gap probe prepared the scene `고기 식당` candidate through runtime record/room-profile evidence, then rechecked the real `FacilityEvolutionRuntime` path: `approvedAfterPrepare=True`, `evolved=True`, result `2성 전투 식당`, 0 captured errors. |
| CODEX-01 | Codex unlocks and codex panel | Codex scripts/panel | Automatic scenario coverage | Verified in game runtime path | Codex imported/updated after feature events: 37 facility, 3 monster, and 1 invasion entries; tab visibility was previously verified. |
| INVASION-01 | Invasion threat rise, warning, candidate delay | Invasion threat scripts | Automatic scenario coverage | Verified in game runtime path | Remaining-feature pass raised threat above candidate threshold, ticked the delay, and drove director/alert flow with alerts `1 -> 6`; stage reset to `Safety` after invasion start as expected. |
| INVASION-02 | Intruder behavior, target choice, breakthrough result | Invasion intruder scripts | Automatic scenario coverage | Verified timed coroutine path, owner damage direct path verified separately | Evidence covers candidate-triggered spawn, entry/movement DI, a direct reachable path with 31 steps to the owner, final combat damage `1260 -> 1250`, and the timed coroutine probe reaching `state=Finished`, `spawned=True`, `assigned=True`, `finishedOrDestroyed=True`, 0 captured errors. The timed scene had no owner actor, so owner HP stayed `-1 -> -1`; owner damage remains covered by the direct final-combat probe. |
| DEF-01 | Defense facilities and combat effects | Defense scripts/assets | Automatic scenario coverage | Verified in game with injected PlayMode placement | Closing-gap probe placed `P1_SpikeTrap` into the active `SampleScene`, resolved it through `DefenseFacilityResolver.TriggerAt`, produced 1 activation report, dealt 14 damage, then destroyed the placed defense. Base scene still lacks a pre-placed triggerable defense. |
| DEF-02 | Invasion combat report UI/feedback | Combat report runtime | Automatic scenario coverage | Verified in game runtime path | Completion audit generated a combat report after runtime defense placement/activation and final-combat events: `runtime=True`, `defenseStatus=True`, `placed=True`, `triggeredReports=1`, `report=True`, `resolved=True`, `defenseContributions=1`, `topDamage=14`, `damagedFacilities=1`, `finalCombat=True`, and `detailLength=217`, with captured errors 0. |
| RECRUIT-01 | Regular customers and recruitment | Recruitment runtime | Automatic scenario coverage | Verified in game runtime path | Remaining-feature pass drove 4 visits through `RegularCustomerRuntime`, produced a recruit candidate, recruited successfully, and recorded 1 recruited character. |
| DISCONTENT-01 | Staff discontent and rebellion stages | Staff discontent scripts | Automatic scenario coverage | Verified in game runtime path | Remaining-feature pass used injected temporary staff in active PlayMode, produced `LocalRebellion`, and kept console clean. |
| REBELLION-01 | Rebellion response commands | Rebellion response scripts | Automatic scenario coverage | Verified in game runtime/controller path | Completion audit verified discontent plus owner command controller flow: `runtime=True`, `controller=True`, rebel outcome `LocalRebellion`, `localRebellion=True`, `selectedByController=True`, `commandInvoked=True`, `commandSet=True`, `destinationResolved=True`, and `suppressed=True`, with captured errors 0. |
| RUN-01 | Run variables and temporary events | Run variable runtime | Automatic scenario coverage | Verified in game runtime path | `SampleScene` runtime started a run, activated operation variable `SlimeCrowdVisit`, selected invasion variable `ScoutTraces`, and raised alerts with 0 errors. |
| META-01 | Run end, run result panel, meta currency | Meta progression scripts/panel | Automatic scenario coverage | Verified in game | Triggered run end, displayed run result panel, gained legacy currency, and purchased `StartingFacilityCandidatePlusOne` upgrade. |
| OFF-01 | Offense world map and scouting | Offense world map runtime/panel | Automatic scenario coverage | Verified in game | Opened world map panel, revealed 2 targets, selected `외곽 식재료 농장`, and captured the panel. |
| OFF-02 | Offense expedition assignment and auto result | Offense expedition runtime/panel | Automatic scenario coverage | Verified in game with injected PlayMode member | Base `SampleScene` still has 0 available members, but remaining-feature pass created a VContainer-injected temporary NPC member, started `외곽 식재료 농장`, completed it successfully, and returned the member. |
| OFF-03 | Offense rewards | Offense reward runtime | Automatic scenario coverage | Verified in game with injected PlayMode member | Same pass applied expedition rewards: reward money `0 -> 80`, 2 reward summaries, 0 errors. |
| UI-01 | Summary top tabs and generated panels | `UITabManager`, summary services | Refactor follow-up smoke | Verified in game for `SampleScene` core tab pass | Verify all tabs in active game scene with visible text and no overlaps after each feature-family flow. Current `SampleScene` pass opened tabs 1-9 with text. |
| UI-02 | UI bounds and visual readability | Canvas/UI scripts | Refactor follow-up smoke | Failed then fixed / verified in game | Feature-family pass had 94 active rects, 0 invalid, 0 oversized. Remaining-feature pass had 73 active rects, 0 invalid, 0 oversized. Closing-gap pass had 44 active rects, 0 invalid, 0 oversized. Public build UI clean-open capture `Temp/full-game-qa-samplescene-public-build-ui-clean-open.png` showed readable HUD, construct categories, and generated buttons. Final Character-AI visual rerun had 38 active rects, 0 invalid, 0 oversized, 0 active yellow `Quest` graphics. Core/Grid/UI completion added building-info overlay evidence: `UI activeRects=55`, `invalid=0`, `oversized=0`. Final completion audit added owner/run-result/command visual evidence: `UI activeRects=69`, `invalid=0`, `oversized=0`, `activeTexts=29`, `activeButtons=19`, screen capture `Temp/full-game-qa-completion-audit-probe.png`, and Unity MCP camera capture succeeded. |

## PlayMode Pass Log

### 2026-07-12 - `CharacterAiTestScene` Baseline

- Entered PlayMode in `Assets/Scenes/CharacterAiTestScene.unity`.
- Confirmed active scene startup: 1 camera, 1 canvas, 1 `DungeonRuntimeLifetimeScope`, 1 `UIManager`, 1 `GameManager`, 1 `GridSystemManager`, 1 `DungeonStoryGridBuildingController`, 1 `GridUIManager`, 1 `UITabManager`.
- Confirmed current scene lacks many feature runtimes: facility shop, blueprint research, synthesis, evolution, codex, invasion, defense status, operating-day settlement, event alerts, recruitment, discontent, run variables, meta progression, and offense runtimes were all `0` in this scene.
- Static scan of `SampleScene` found scene entries for most feature-family runtimes: facility shop, blueprint research, synthesis, evolution, codex, invasion threat/director/report, operating day settlement, event alerts, recruitment, discontent, run variables, meta progression, offense world map/expedition/rewards, AI director, local LLM queue, social reputation, and runtime lifetime scope.
- Confirmed visible HUD text: money, day/time, speed, top/bottom tab labels.
- Confirmed pause/speed core behavior: `timeScale` went `1 -> 0 -> 1 -> 2`.
- Confirmed construct panel toggles open/closed in PlayMode.
- Confirmed `UITabManager.ToggleSelectButton(1..9)` opened one active tab at a time without console errors.
- Captured MCP Scene View camera output.
- Captured real PlayMode screen output at `Temp/full-game-qa-research-tab.png`.
- Found visual issue: active empty `UI/Quest` image covered the upper-right world view.
- Fixed scene data by setting `Quest` inactive in both `Assets/Scenes/CharacterAiTestScene.unity` and `Assets/Scenes/SampleScene.unity`.
- Re-tested in current PlayMode by disabling the loaded `Quest` object and captured `Temp/full-game-qa-research-tab-after-quest-fix.png`; the empty overlay was gone.
- Final console check for this pass: 0 errors, 0 warnings.
- Exited PlayMode and confirmed `isPlaying=False`.

### 2026-07-12 - Additive-Isolated `SampleScene` Core/Intruder Retest

- Kept dirty `CharacterAiTestScene` loaded but disabled its 10 roots, then used active `Assets/Scenes/SampleScene.unity` for PlayMode verification.
- Found a real runtime failure during the first `SampleScene` pass: dynamically-created `InvasionIntruderRuntime` characters reached `CharacterActor`/`AbilityMove` without VContainer injection, causing `ICharacterAiSchedulingService` exceptions.
- Fixed `InvasionIntruderRuntimeFactory` to reuse `ICharacterSpawnObjectFactory.Create/Inject`, matching the existing dynamic character DI path.
- Found a second additive-verification failure: `IDungeonSceneComponentQuery.First(... includeInactive: true)` could return inactive components from the non-active loaded scene before active `SampleScene` components.
- Fixed `DungeonSceneComponentQuery` to search the active scene first and active roots before inactive roots.
- Added `FullGameManualQaRuntimeProbe` as a PlayMode QA helper for the `SampleScene` intruder path.
- Re-tested `SampleScene` after both fixes:
  - scene query resolved active `SampleScene` `GridSystemManager`, grid size `32x3`, walkable cells `84`.
  - intruder probe reported `spawned=True`, `intruderAssigned=True`, `entryResolved=True`, `entryGrid=(4, 0)`, `capturedErrors=0`.
  - after 5 seconds at `Time.timeScale=8`, probe report still had `capturedErrors=0`, `errors=<none>`.
  - core UI probe confirmed pause/speed `1 -> 0 -> 1 -> 2`, construct panel `False -> True -> False`, tabs 1-9 each opened one active tab with text.
  - UI bounds check: 86 active rects, 0 invalid, 0 oversized, 39 active texts, 33 active buttons.
- Captured Unity MCP Scene View camera output; dungeon/facility labels were visible.
- Captured overlay UI screenshot at `Temp/full-game-qa-samplescene-after-intruder-fix.png`; HUD, bottom tabs, codex panel, and notice feed were visible.
- Restored `CharacterAiTestScene` roots to their saved active states and closed additive `SampleScene`; final editor state returned to one loaded scene with 10/10 active roots.
- During restore, Unity emitted transient URP duplicate global-light errors while both scenes' roots were briefly active; the scene was then restored to one loaded scene.

### 2026-07-12 - Additive-Isolated `SampleScene` Feature-Family Probe

- Reused the additive-isolated `SampleScene` setup while preserving dirty `CharacterAiTestScene`; final restore returned to one loaded `CharacterAiTestScene` with 10/10 active roots.
- Added `FullGameManualQaRuntimeProbe.RunSampleSceneFeatureFamilyProbe()` to exercise active scene runtimes in PlayMode.
- Final feature-family probe report:
  - active scene `Assets/Scenes/SampleScene.unity`, 97 buildable objects, 4 character actors.
  - event alerts: alert log increased, detail opened, choice executed, callback fired, detail visible.
  - daily facility shop: 4 daily offers, purchased `P1_ResearchLab`, money `50000 -> 49880`.
  - blueprint research: queued `BP_ArcaneBasics`, used scene `연구실`, completed work with progress 1.
  - facility synthesis: 8 visible recipes, matched 2 scene materials, synthesized `RS_BattleDining` into `2성 전투 식당`.
  - facility evolution: runtime present, but 0 candidates in the current scene state; remains missing-scene-candidate.
  - codex: 37 facility entries, 3 monster entries, 1 invasion entry.
  - operating day: day 7 report generated with revenue 123, 1 facility revenue row, 1 event row.
  - run variables: start variables present, operation `SlimeCrowdVisit`, invasion `ScoutTraces`.
  - offense: world map and expedition panels opened, 2 visible targets, selected `외곽 식재료 농장`; expedition start blocked by 0 available members.
  - meta: run result generated, legacy currency gained, upgrade purchase succeeded.
  - UI: 94 active rects, 0 invalid, 0 oversized, 40 active texts, 32 active buttons.
- Visual evidence:
  - `Temp/full-game-qa-samplescene-feature-family.png` captured run result and feature event state.
  - `Temp/full-game-qa-samplescene-feature-family-alert-sorting-fix.png` captured offense panel plus alert detail after the alert stacking fix.
  - Unity MCP camera capture also showed the `SampleScene` dungeon/facility view.
- Found and fixed a real UI stacking issue: alert detail was under offense canvases (`sortingOrder` 420/430). `EventAlertRuntimeUI` now creates an override-sorting nested canvas at order 480, below the run-result canvas at 500.
- Final console check after the feature-family pass and scene restore: 0 errors, 0 warnings.

### 2026-07-12 - Additive-Isolated `SampleScene` Remaining-Feature Probe

- Added `FullGameManualQaRuntimeProbe.RunSampleSceneRemainingFeatureProbe()` to cover feature families that were still unverified after the first feature-family pass.
- First attempt mixed broad legacy Editor debug-world suites into the PlayMode pass; those suites create uninjected temporary objects and produced false fixture errors, so the probe was narrowed to direct active-`SampleScene` evidence plus the already-stable facility evolution supplement.
- Final remaining-feature probe report:
  - active scene `Assets/Scenes/SampleScene.unity`, 97 buildable objects, 4 character actors.
  - AI: 4 actors/4 brains, 2 actors could run AI, 2 direct decisions selected `일하기`.
  - inventory/logistics: scene shop restocked 5 items from a scene warehouse; shop stock `16 -> 5`, warehouse stock `80 -> 75`.
  - invasion/threat/report: threat raised and candidate/director flow triggered; active intruders `0 -> 1`; combat report created; 1 damaged facility recorded; alerts `1 -> 6`.
  - defense: `IDefenseStatusRuntimeService` resolved, but no pre-placed triggerable scene defense facility was found in this pass; the later closing-gap probe below covers injected runtime defense placement/activation.
  - recruitment: 4 visits through `RegularCustomerRuntime` produced a recruit candidate and successful recruitment.
  - staff discontent/rebellion: injected temporary staff produced `LocalRebellion`; isolate and suppress response paths succeeded.
  - offense: injected temporary expedition member started and completed `외곽 식재료 농장`; reward money `0 -> 80`, 2 reward summaries.
  - evolution: active scene produced 2 candidates but 0 approved; `FacilityEvolutionDebugScenarios.RunAll(false)` passed as debug-world supplement.
  - UI: 73 active rects, 0 invalid, 0 oversized, 31 active texts, 29 active buttons.
  - captured errors: 0; Unity console after probe/capture/restore: 0 errors, 0 warnings.
- Visual evidence:
  - Unity MCP camera capture showed the `SampleScene` dungeon/facility view.
  - `Temp/full-game-qa-samplescene-remaining-feature-families.png` showed HUD, top controls, bottom tabs, and notice feed after the remaining-feature pass.
- Restored editor state: exited PlayMode, closed additive `SampleScene`, restored `CharacterAiTestScene` roots; final active scene `Assets/Scenes/CharacterAiTestScene.unity`, one loaded scene, 10 roots active, console clean.

### 2026-07-12 - `CharacterAiTestScene` Injected Character-AI Feature Probe

- First tried broad legacy character-AI debug-world suites in PlayMode. They produced fixture-only DI errors from uninjected temporary objects, so they were separated from direct active-scene evidence.
- Added and ran an injected `CharacterAiTestScene` probe using VContainer-injected temporary characters.
- Fixed a compile error in the new QA helpers caused by referencing a nonexistent `AIBrain.CurrentDestinationDebugLabel`; the probe now reads the reserved destination from `bestAction.ReservedDestination`.
- Final probe evidence:
  - active scene `Assets/Scenes/CharacterAiTestScene.unity`, 97 buildings, 4 actors.
  - customer AI: injected customer, `canRun=True`, scheduler-registered and BehaviorTree-processed with `schedulerProcessedMax=1`, `behaviorTicksMax=1`, `treeTicks=0->1`, `scheduledDecided=True`, `directFallback=False`, action `휴식`, destination `휴식방`.
  - staff duty/priority: low condition blocked work and triggered off-duty; visitor state was true while off-duty; QA-aged recovery returned the staff to work; priority work target assignment succeeded.
  - owner priority: owner actor decision and priority work target assignment succeeded.
  - local LLM/persona: queue present; fake-success persona trait, mood impulse, and macro intent applied; real endpoint and visible dialogue bubble were still pending in this historical pass, then closed by later 2026-07-13 probes.
  - character visual feedback: visual renderer and bubble component present; persistent Fatigue state evaluated; temporary visible bubble state did not apply.
  - UI: 39 active rects, 0 invalid, 0 oversized, 15 active texts, 13 active buttons.
- Visual evidence:
  - Unity MCP camera capture showed the AI test dungeon/facility view.
  - Initial `Temp/full-game-qa-character-ai-feature-probe.png` showed HUD/top/bottom UI, but also a large translucent yellow right-side panel. This was later classified and fixed in the overlay pass below.
- Final console after this probe/capture/exit: 0 errors, 0 warnings.

### 2026-07-12 - Additive-Isolated `SampleScene` Closing-Gap Probe

- Added and ran `FullGameManualQaRuntimeProbe.RunSampleSceneClosingGapProbe()` to close several remaining runtime-evidence gaps.
- First PlayMode attempt exited immediately because the new helper had a compile error in `TryFindReachablePair`: an `out` parameter was captured by LINQ lambdas. Fixed by copying `start` into a local `startPosition`, recompiled, and re-entered PlayMode successfully.
- Final closing-gap probe report:
  - active scene `Assets/Scenes/SampleScene.unity`, grid `32x3`, 97 buildable objects.
  - grid/build/defense: loaded `P1_SpikeTrap`, found position `(21, 0)`, placed a `DefenseFacility`, resolved it through `DefenseFacilityResolver.TriggerAt`, produced 1 trigger report, dealt 14 damage (`1500 -> 1486`), and destroyed the placed defense.
  - invasion breakthrough/final combat: found a direct path with 31 steps, moved the intruder to the owner position, initialized `InvasionIntruderRuntime`, and applied final combat damage (`1260 -> 1250`).
  - visible feedback: scheduler allowed feedback, feedback bubble existed, `Show(Joy)` succeeded, and active text count changed `16 -> 17`; dialogue runtime existed but `dialogueShown=False`.
  - UI: 44 active rects, 0 invalid, 0 oversized, 17 active texts, 14 active buttons.
  - captured errors: 0; Unity console after probe/capture/restore: 0 errors, 0 warnings.
- Visual evidence:
  - Unity MCP camera capture showed the `SampleScene` dungeon/facility view.
  - `Temp/full-game-qa-samplescene-closing-gap-probe.png` showed readable HUD, top speed controls, bottom tabs, and facility labels, with no large right-side yellow overlay in this scene.
- Restored editor state after the pass: exited PlayMode, closed additive `SampleScene`, restored `CharacterAiTestScene` roots; final active scene `Assets/Scenes/CharacterAiTestScene.unity`, one loaded scene, 10 roots active.
- During restore Unity again emitted transient URP duplicate global-light logs while both scenes' roots were briefly active; after closing `SampleScene` and clearing the transient logs, final console was clean.

### 2026-07-12 - `CharacterAiTestScene` Quest Overlay Classification/Fix

- Re-ran `CharacterAiFeatureQaRuntimeProbe.Run()` to reproduce the yellow right-side overlay.
- Active UI graphic scan identified the overlay as `UI/Quest`: `Image` color `(1, 0.876, 0, 0.357)`, rect `(1488,454.6)-(1910,940)`, `raycastTarget=True`.
- Confirmed both scene files serialize `Quest` as inactive, but the currently open dirty `CharacterAiTestScene` editor state had `UI/Quest activeSelf=True`; set the open scene object inactive without saving unrelated dirty scene state.
- Re-ran the Character-AI probe after disabling `UI/Quest`:
  - UI: 38 active rects, 0 invalid, 0 oversized, 15 active texts, 13 active buttons.
  - active yellow `Quest` graphics: 0.
  - `Temp/full-game-qa-character-ai-feature-probe.png` showed the HUD/top/bottom UI and facility labels with the yellow right-side overlay removed.
  - Unity MCP camera capture also showed the AI test dungeon/facility view.
  - Final console after the rerun and PlayMode exit: 0 errors, 0 warnings.
- During this pass a QA-fixture DOTween warning appeared when temporary characters were destroyed. Updated both QA helpers to kill DOTween tweens on temporary objects/components before destruction, recompiled cleanly, and reran with no warning.

### 2026-07-12 - `SampleScene` Public Build UI Retest/Fix

- Ran a public build UI PlayMode flow in active `SampleScene` after a feature-family purchase/unlock path.
- Initial public-flow failures:
  - Generated building select buttons did not invoke `UIBuildingSelectButton.OnClick`, so clicking a generated button did not reliably select/build.
  - Closing the construct tab hid the panel but left build mode active and the placement ghost visible.
  - Reopening the construct tab after a building became unlocked did not regenerate the unlocked building buttons.
- Runtime fixes:
  - `GridConstructButtonFactory.CreateBuildingSelectButton()` now wires each generated Unity `Button` to `UIBuildingSelectButton.OnClick`.
  - `GridUIManager.CloseConstructTab()` now resolves runtime dependencies, fires `OnUiClose`, and resets `DungeonStoryGridBuildingController` to `GridMode.None`.
  - `GridConstructTab.OnOpen()` now rebuilds missing select buttons while avoiding duplicates.
- Retest evidence:
  - `constructOpen=True`, 8 category buttons, 24 building buttons, 23 generated building buttons.
  - Category click succeeded, generated building button was selected, selected building id `1`, grid mode became `Build`, ghost became visible.
  - Found buildable position `(21, 0)`.
  - Closing the construct tab produced `constructAfterClose=False`, `modeAfterClose=None`, and `ghostHiddenAfterClose=True`.
  - Clean visual capture: `Temp/full-game-qa-samplescene-public-build-ui-clean-open.png`.
- A later console pass showed a DOTween warning from destroyed character visuals after broader PlayMode cleanup. `CharacterVisual` now kills child `SpriteRenderer`, renderer GameObject/Transform, visual root, and owning object/transform DOTween targets in `OnDisable`/`OnDestroy`; the final Unity console check after compilation returned 0 errors and 0 warnings.

### 2026-07-13 - `CharacterDialogueRuntime` Display Fix and Closing-Gap Rerun

- The first rerun after QA helper reinjection still reported `dialogueShown=False` with no exception.
- Root cause: dynamically-created actors can receive required runtime components before `CharacterActor` is fully cached by `CharacterDialogueRuntime.Awake()`. If the cached actor stays null, `ShowLine()` is rejected by the scheduler visibility gate.
- Runtime fix: `CharacterDialogueRuntime` now lazily refreshes `CharacterActor`, `CharacterLog`, and `CharacterVisual` references in `Awake`, `OnEnable`, `LateUpdate`, `ShowLine`, `OnLogAdded`, and offset calculation.
- Recompiled cleanly with 0 console errors/warnings.
- Reran `FullGameManualQaRuntimeProbe.RunSampleSceneClosingGapProbe()` in additive-isolated `SampleScene`:
  - grid/build/defense still passed: `P1_SpikeTrap` placement at `(21, 0)`, resolver trigger count 1, damage `1500 -> 1486`, destroyed after test.
  - invasion final-combat still passed: direct path 31 steps, owner health `1260 -> 1250`.
  - visible bubble/dialogue now passed: `schedulerAllows=True`, `feedbackShown=True`, `dialogueShown=True`, `lastDialogue=QA visible line`, active text count `16 -> 18`.
  - UI: 45 active rects, 0 invalid, 0 oversized, 18 active texts, 14 active buttons.
  - captured errors: 0; PlayMode console after the rerun was 0 errors/warnings.
- Unity MCP camera capture showed the `SampleScene` dungeon/facility view. `Temp/full-game-qa-samplescene-closing-gap-probe.png` was refreshed at 2026-07-13 00:17:20 and showed readable HUD/top controls/bottom tabs/facility labels.
- Restored editor state after the pass: active scene `Assets/Scenes/CharacterAiTestScene.unity`, one loaded scene, 10 roots active, not playing/compiling/updating, final console 0 errors/warnings.

### 2026-07-13 - Public Pointer Placement Attempt

- Re-entered additive-isolated `SampleScene` to test the strictest public build input subcase.
- Probe path:
  - opened `GridConstructTab`.
  - clicked a category button through `ExecuteEvents.pointerClickHandler`.
  - clicked a generated `UIBuildingSelectButton` through `ExecuteEvents.pointerClickHandler`.
  - selected building id `1`.
  - found buildable position `(21, 0)`.
  - warped the Input System mouse to that world position's screen coordinate.
  - confirmed `EventSystem.current.IsPointerOverGameObject()` was false before placement.
  - called public `DungeonStoryGridBuildingController.TriggerPlaceBuilding()`.
- Initial result: `opened=True`, `categoryClicked=True`, `buildingClicked=True`, `selectedId=1`, `foundPosition=True`, `buildableBefore=True`, `mouseWarped=True`, `pointerOverUiBeforePlace=False`, but `placed=False`, `beforeCount=97`, `afterCount=97`, `mode=None`, `modeReset=True`, exception none.
- Root cause and fix: `DungeonStoryGridBuildingController` receives click input through Input System `InputAction`, while `SceneCameraWorldPointerPositionProvider` read legacy `Input.mousePosition` through `UnityPlayerInputReader`; under MCP/InputSystem injection, `Mouse.current.position` was correct while legacy `Input.mousePosition` was stale. `UnityPlayerInputReader.MousePosition` now prefers `Mouse.current.position` when available, preserving the DI adapter boundary.
- Retest result: public construct open, category button click, generated `UIBuildingSelectButton` click, InputSystem pointer warp, and public `TriggerPlaceBuilding()` placed at `(21, 0)`: `mouseCurrent=(928.00, 252.00)`, `legacyMouse=(-8359.78, 205.95, 0.00)`, `pointerOverUiBeforePlace=False`, `resolvedBefore=(21, 0)`, `before=97`, `after=98`, `placed=True`, `mode=None`. Unity MCP camera capture succeeded and final console was 0 errors/warnings after restore.
- A DOTween warning recurred after this run from a destroyed `SpriteRenderer` fade. `CharacterVisual` tween cleanup was broadened again, then Unity recompiled with final console 0 errors/warnings.

### 2026-07-13 - SampleScene Timed Gap Probe

- Added and ran `FullGameManualQaRuntimeProbe.StartSampleSceneTimedGapProbe()` in active `SampleScene` PlayMode.
- Facility evolution: `sceneCandidates=2`, `sceneApproved=0` initially; QA-prepared the active scene candidate, revalidated to `selectedApproved=True`, `approvedAfterPrepare=True`, and executed `TryEvolve()` successfully: result `2성 전투 식당`.
- Invasion coroutine: `settingsOverridden=True`, `spawned=True`, `assigned=True`, `state=Finished`, `finishedOrDestroyed=True`, `exception=<none>`. Owner damage was not part of this timed run because `owner=False`, but direct final combat damage was verified earlier.
- Initial Local LLM real endpoint attempt accepted a low-priority bubble request but dropped it from queue age pressure, so the probe was corrected to isolate the queue and use a high-priority persona request.
- Local LLM isolated endpoint retest: localhost Ollama `/api/tags` and `/v1/models` both reported `llama3.1`; a direct OpenAI-compatible POST returned `{"qa":"pong"}` outside Unity. The corrected PlayMode probe then reported `LOCAL-LLM-ENDPOINT queue=True; configured=True; accepted=True; completed=True; status=Succeeded; success=True; content={\"qa\":\"pong\"}; error=<none>; queued=11; running=0; timeouts=0; dropped=0`.
- UI/visual: `UI activeRects=48`, `invalid=0`, `oversized=0`; `Temp/full-game-qa-samplescene-timed-gap-probe.png` was written, and Unity MCP Scene View capture showed the evolved `전투 식당` label visible. Final console after PlayMode stop/restore was 0 errors/warnings.

- Corrected endpoint retest visual/final state: `UI activeRects=51`, `invalid=0`, `oversized=0`; Unity MCP Scene View capture showed the evolved `전투 식당` label visible. Final editor state after PlayMode stop/restore was active `CharacterAiTestScene`, one loaded scene, 10/10 active roots, and console 0 errors/warnings.

### 2026-07-13 - Character AI Long Observation Probe

- Added and ran `FullGameManualQaRuntimeProbe.StartCharacterAiLongObservationProbe()` in `CharacterAiTestScene` PlayMode after recompiling the QA helper and clearing the console.
- Customer visit: `planned=True`, destination `훈련장`, `moved=True`, `visited=True`, `visitCount=1->0`, `visitedBuildings=0->1`, start `(4, 0)`, end `(10, 2)`, failure `<none>`.
- Staff wander/fatigue: `wanderPathFound=True`, `wanderPathSteps=1`, `wanderStarted=True`, `wanderMoved=True`; work candidate `훈련장`, `workType=Operate`, `workStarted=True`, `fatigueChanged=True`, `sleep=100->97`, `mood=100->99`, message `<none>`.
- UI/visual: `UI activeRects=40`, `invalid=0`, `oversized=0`, `activeTexts=17`, `activeButtons=13`; screen capture `Temp/full-game-qa-character-ai-long-observation.png` showed readable HUD, top controls, bottom tabs, facility labels, and no blocking `Quest` overlay. Unity MCP camera capture also succeeded.
- Final state after PlayMode stop: editor not playing/compiling/updating; console Error/Warning count 0.

### 2026-07-13 - Core/Grid/UI Completion Probe

- Added and ran `FullGameManualQaRuntimeProbe.StartCoreGridUiCompletionProbe()` in `CharacterAiTestScene` PlayMode after clearing the console.
- Active scene composition evidence: `activeScene=Assets/Scenes/CharacterAiTestScene.unity`, `lifetimeScopes=1`, `gameManagers=1`, `gridManagers=1`, `gridUiManagers=1`, `uiManagers=1`, `cameraManagers=1`, `cameras=1`, `grid=32x3`, `buildings=97`.
- Grid evidence: `GridFoundationDebugScenarios.RunAll(false)` returned `True`; active-scene reachability found `(1, 0)->(30, 2)`, `reachableCount=84`, `destinationWalkable=True`.
- Camera/input/UI guard evidence: `CameraManager.ClampToCurrentBounds()` restored an extreme camera position to valid bounds (`clampChangedExtreme=True`), and `UIManager.MakeTouchFalse/MakeTouchTrue` toggled the guard (`blocked=True`, `released=True`).
- Building-info evidence: active scene `GridUIManager.buildingInfoUI` opened on a live building (`opened=True`, non-empty `displayedName`, `touchBlocked=True`), then closed cleanly (`closed=True`, `touchReleased=True`).
- UI/visual: `UI activeRects=55`, `invalid=0`, `oversized=0`, `activeTexts=22`, `activeButtons=17`; `Temp/full-game-qa-core-grid-ui-completion.png` showed readable HUD, top controls, bottom tabs, and the building-info panel. Unity MCP camera capture also showed the world view from `Main Camera`.
- Final console check after the probe and captures: 0 errors, 0 warnings.

### 2026-07-13 - Completion Audit / Owner Hookup Probe

- Added the missing `OwnerCommandController` scene object to `Assets/Scenes/SampleScene.unity`, then entered PlayMode with `SampleScene` as the active scene while preserving and disabling the dirty `CharacterAiTestScene` roots.
- Completion audit evidence:
  - startup/composition: active scene `Assets/Scenes/SampleScene.unity`, `lifetimeScopes=1`, grid `32x3`, 97 buildings.
  - owner/model/run end: `panel=True`, `panelBuilt=True`, `panelButtons=7`, `candidates=3`, `selectedByIndex=True`, `ownerSpawned=True`, `profileConnected=True`, `species=Slime`, `traitCount=1`, `statsFromProfile=True`, `ownerDeathApplied=True`, `runEnded=True`, `metaResult=True`, `runResultPanels=1`.
  - combat report: `defenseStatus=True`, `placed=True`, `triggeredReports=1`, `resolved=True`, `defenseContributions=1`, `topDamage=14`, `damagedFacilities=1`, `finalCombat=True`, `detailLength=217`.
  - rebellion command: `controller=True`, `outcome=LocalRebellion`, `selectedByController=True`, `commandInvoked=True`, `commandSet=True`, `destinationResolved=True`, `suppressed=True`.
  - UI: 69 active rects, 0 invalid, 0 oversized, 29 active texts, 19 active buttons.
- Visual evidence: `Temp/full-game-qa-completion-audit-probe.png` showed HUD, run-result panel, command feed text, bottom tabs, and right-side runtime buttons; Unity MCP camera capture also showed the scene/facility view.
- A DOTween warning from a delayed `SpriteRenderer` fade was fixed by storing/killing the active `CharacterVisual` fade tween directly. Recompiled, reran the completion audit, and final PlayMode console had 0 errors/warnings.
- Final restored editor state: PlayMode off, active scene `Assets/Scenes/CharacterAiTestScene.unity`, one loaded scene, 10/10 active roots, console 0 errors/warnings.

## Current Answer To "Are All Features Confirmed In Game?"

Yes for the implemented feature catalog and automated release criteria. The latest evidence covers public UI, state changes, camera/screen captures, save restoration, three natural victories, intentional defeat, soak/performance, supported resolutions, branded Development/Release builds, and standalone startup with clean critical logs. A few historical probes use injected actors to isolate subpaths, but outcome-level natural runs use the real runtime flow. The remaining gate is a human clean-profile first run for comprehension, pacing, reward cadence, and replay motivation.
