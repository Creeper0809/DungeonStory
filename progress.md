# DungeonStory Progress

## 2026-07-22 - Responsive HUD and final verification closure

- Fixed the remaining portrait-HUD polish gap by clamping the upper-right control strip to the live canvas width and forcing top/bottom navigation buttons to share flexible width instead of preserving template widths.
- Extended `DungeonResolutionPlayModeVerifier` to cover actual `1600x900` and `900x1600` Game View sizes and to load `TitleScene` before title checks, removing the previous false failure when the verifier was launched from gameplay.
- Re-ran the resolution matrix in PlayMode. `Temp/resolution-matrix-report.txt` now ends `RESULT=PASS; failures=0` with `capturedErrors=0; capturedWarnings=0`; the 900x1600 gameplay capture keeps upper-right controls and bottom tabs in-bounds.

## 2026-07-21 - Physical item and hauling implementation started

- Added phases 27-34 to the active plan for the requested RimWorld-style physical item, hauling, pile UX, integration, save, and verification work.
- Confirmed the current Unity Editor Console reports `Error 0 / Warning 0`; standalone batchmode compile is blocked because the interactive Editor is already running.
- Audited the stock, warehouse, click-selection, grid-layer, AI action, and lifetime-scope integration points before editing.

## 2026-07-20 - Character growth and skill redesign

- Completed the final acceptance pass. All implemented EditMode suites plus progression, population, facility evolution, room system/environment, and AI-plan regressions passed together.
- Re-ran the P1/P2 UI surface verifier with real pointer input (`18/18`) and the exclusive character/building click verifier; both captured zero errors and warnings. Visual review confirmed character-only, building-only, and skill-alert states are distinct and readable.
- Recovered MCP `Camera_Capture` with a plain runtime camera copied from `Main Camera`. The resulting nonblank 1920x1080 world capture showed all three floors, room boundaries, doors, facilities, characters, and lighting without HUD interference.
- Final Unity Console state is `Error 0 / Warning 0`; character-growth phases 14-18 are complete.
- Replaced the placeholder mood-only handling for management skill modules with real domain integrations: production output, flat stock, research work, cleaning and repair duration, staffed shop revenue, positive relationship sentiment, and spawned-intruder damage for defense ultimates. Added deterministic progression scenarios for each numeric path and management day-use activation.
- Strengthened the Unified UI PlayMode verifier with a real `CharacterProgression.OnDraftReady` alert and Input System press/release events. The alert button opened its detail, the single choice closed it and selected Growth, the player copy hid all LLM/request terminology, the screenshot was nonblank, and the complete UI regression still passed with zero errors/warnings.
- Added and ran a repeatable skill-runtime PlayMode probe. Management fired once on day 7001, ignored a duplicate day event, reset on day 7002, and activated a real 1.3x output modifier; defense reduced a spawned intruder from 120 to 115.5 HP; a direct offense ultimate dealt damage, set cooldown 999, and rejected same-battle reuse. Console remained Error 0 / Warning 0.

- Diagnosed the start-party `staff=4`/inactive failure as two `CharacterActor`-derived components on each character prefab, not duplicate spawning. Removed the obsolete `Customer` component from both character prefabs and added GameObject-based actor canonicalization to start-party, offense, and world-save paths.
- The full real-LLM pointer verifier now passes rerolls, nine tabs, three candidate confirmations, passive readiness, party commit, same-species staff, and UI close with `errors=0; warnings=0`. Visual inspection rejected its mobile artifact because the Editor ignored `Screen.SetResolution` and wrote another 1920x1080 frame.
- Replaced the false mobile resize with the shared Editor Game View resolution controller. A second real-LLM pointer run passed at actual `1600x900` and `900x1600`, produced nonblank exact-size captures, kept all three cards inside the portrait viewport, committed exactly two active staff, and ended with zero errors/warnings.
- Updated the full-game progression save verifier for V3 growth/narrative payloads and explicit V2 rejection; the real game-save JSON round trip passed at Lv.4/XP 77 with active/passive skills intact. Unified UI then passed growth, mood, records, notices, staff, and building preview after replacing its stale section-label expectation.

- Accepted the finalized level-50 character growth, modular skill generation, three-character preparation, and persistent world-character specification.
- Restored the previous planning files and appended phases 14-18 without changing completed offense work.
- Confirmed Unity MCP tool availability; editor-state and compilation checks are next.
- Unity Editor state query passed while idle; Console baseline is Error 0 / Warning 0.
- Audited actor component ownership, final stat queries, combat snapshot creation, local LLM profiles, owner selection, VContainer registration, and character save capture/restore.
- Implemented the level-50 per-character growth records, constrained modular skill generation, hidden retry state, combat/management runtime effects, population profiles, save V3 payloads, and growth-tab presentation.
- Added `StartPartyPreparationService`: it prepares an owner plus two same-species staff, rolls identity/aptitude separately, preserves three partial reroll charges, pre-generates the next skill roll, requires a selected first active plus validated passive, and only creates world actors on final confirmation.
- Fresh world profiles now resume their missing level-one active/passive drafts after restore; replacing or restoring a prepared roll cancels stale generation callbacks.
- Unity rebuilt `Assembly-CSharp.dll` after the start-party service changes with no C# compiler errors. The MCP bridge revoked approval during that domain reload and must be retried before PlayMode verification.
- Replaced the owner-choice callback with a real three-card preparation UI. Each card has Identity/Aptitude/Skill tabs, unlimited full rerolls, three charged partial rerolls, double-click confirmation for the permanent first active, and a gated final start action.
- A PlayMode pointer event selected the Slime owner and produced all nine per-character tabs. Runtime inspection confirmed six isolated current/prefetch growth objects and two hidden pending drafts per object; no Console errors or warnings were emitted.
- Fixed a discovered rerender defect where bottom preparation buttons accumulated outside the preparation root.
- Prepared traits now drive the effective per-character runtime profile, so generated traits affect consumption, crowd sensitivity, work/facility preference, accidents, spending, movement, and combat modifiers in addition to final stats.
- Visitor profiles now use readable names/origins and are promoted to permanent staff profiles on recruitment, preventing the same hired person from returning as a guest.

## 2026-07-20 - Character progression follow-up started

- Began the per-character level and skill progression increment requested after the expedition loop was completed.
- Chose instance-owned progression with shared skill definitions so characters of the same species can level and build different loadouts.

## 2026-07-20 - Darkest-Dungeon-style offense completed

- Replaced launch-to-one-battle offense with deterministic branching expeditions containing battle, event, camp, cache, and boss nodes.
- Added expedition light, supplies, persistent health/stress, front/middle/rear formation, ability position rules, camping, loot, retreat, and ordinary-battle return to route choice.
- Connected formal dungeon rooms, modular facility support abilities, and real warehouse stock to preparation capacity, scouting, starting light, medicine, camp recovery, loadout withdrawal, rollback, and return deposits.
- Extended active-expedition saves with route node, phase, completed nodes, supplies, loot, formation, stress, damage, and preparation data plus legacy migration.
- Restyled route and battle surfaces with compact charcoal, burgundy, brass, deep-green states and removed the oversized mint action slabs.
- Updated reward, Product Shell, and P1/P2 regressions so they traverse the new route UI instead of assuming immediate combat.
- Pointer-driven Product Shell verification passed owner selection, recruitment, map/composition, journey start, first battle, and exact manual save/restore with `capturedErrors=0; capturedWarnings=0`.
- A PlayMode UI-event run selected the owner, clicked recon/target/party/route/node/action/target controls, completed all six regions through `truth_core`, and reported `truth=True; history=6`.
- Visually inspected route, battle, and final truth captures. Final domain pass and Unity Console report `Error 0 / Warning 0`.

## 2026-07-20 - Multi-node offense implementation

- Connected front/middle/rear formation to battle persistence, ability source/target positions, enemy AI, and survivor compaction.
- Stress now weakens combat performance without reducing maximum health; regular nodes use smaller encounters and the final node uses the boss formation.
- Removed the immediate boss battle and victory full-heal lifecycle. Expeditions now return to route choice after ordinary victories and finalize only on retreat, defeat, or boss victory.
- Added `BuildingExpeditionSupportAbility` and `DungeonOffensePreparationService`.
- Usable dungeon rooms and Meal/Rest/Research/Mana/Logistics/Hygiene facilities improve camp recovery, scouting, light, medicine, and supply capacity.
- Expedition supplies are withdrawn atomically from real warehouse inventory, rolled back on failure, and unused supplies plus carried loot are deposited on return.
- Reimported scripts through Unity MCP and confirmed Console Error 0 / Warning 0.

## 2026-07-20

- Replaced the active plan with the requested dungeon-linked multi-node offense redesign. The previous single-battle campaign and temporary campaign-order combat multiplier are explicitly superseded.
- Removed the temporary campaign-order combat stat multiplier and its misleading full-campaign balance regression.
- Added the first multi-node expedition domain: deterministic branching route graph, entrance/battle/event/camp/cache/boss nodes, supplies mapped to dungeon stock categories, light, persistent member stress, front/middle/rear formation, camp/event choices, carried stock loot, and retreat/defeat/completion phases.
- Extended `OffenseExpeditionRun` to own that journey state while retaining the legacy constructors needed for save migration. Unity recompiles with no Console errors.

- Read the approved offense-victory implementation plan.
- Re-audited offense, run flow, character persistence, save ordering, DI registrations, and title ownership.
- Confirmed the current runtime still uses timed automatic expeditions.
- Confirmed baseline runtime compilation succeeds.
- Reduced planning notes to the active turn-combat implementation only.
- Added `DungeonDifficulty` selection and launch propagation.
- Added the pure turn battle model, inline ability modules, six encounters, enemy AI, and command idempotence.
- Replaced product expedition completion with `OffenseBattleRuntime` and added the full-screen battle UI plus dungeon switching.
- Added stable character IDs and V2 active-battle save/restore with V1 migration.
- Replaced one-time final defense runtime with recurring 10-day `EndlessDefense` boss cycles.
- Updated old run-flow PlayMode expectations from `FinalChallenge/TruthHunt` to two recurring boss cycles.
- Editor compile attempt failed because `Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll` is missing; runtime compilation is the next recovery step.
- Removed remaining product/debug timer-completion and combat-power comparison paths from offense expeditions.
- Reworked reward, manual QA, P1/P2, and product-shell probes to submit real turn commands.
- Added explicit run difficulty persistence to start snapshots, save data, and run results.
- Fixed battle start ordering, stale restore clearing, first-turn migration, and exact command-wait restoration.
- Added product-shell pointer coverage for difficulty, battle actions, dungeon switching, and exact V2 manual save/load.
- Unity omitted the standalone battle factory source from Bee; merged factory/controller types into `OffenseBattlePanel.cs` and removed the omitted source file.
- Confirmed through Unity MCP that the Editor is idle, compilation completed, and Console reports `Error 0 / Warning 0`.
- Ran the current product-shell verifier. It failed at the first pointer callback (`StartupSettingsButton`) and all later synthetic Input System clicks; report and captures were inspected and rejected as completion evidence.
- Retried without forced `InputSystem.Update`; diagnostics proved the virtual mouse remained at `(0,0)` despite valid button screen coordinates. Switched to the existing queue-plus-`InputState.Change` fallback used by the modular-facility verifier.
- Pointer-state fallback succeeded through title, difficulty, owner selection, Settings, Save, return-to-title, Continue, and missing-save handoff with zero captured errors/warnings.
- Current failure is now the verifier's offense navigation order: it tries to click an off-screen target before opening the world map and composition surfaces.
- Added Close controls to the generated world-map and expedition overlays, and changed product verification to follow the visible map-target-close-composition-member-start path.
- Runtime inspection commands first failed because `OffenseBattleRuntime` is not a `UnityEngine.Object` and the dynamic command assembly does not reference VContainer; a panel-only diagnostic then showed the map remained active after target selection.
- Found and fixed the actual interruption: target selection re-rendered and destroyed its button, then the verifier dereferenced that destroyed button for pointer diagnostics. The interrupted request was removed and PlayMode stopped cleanly.
- Re-ran the visible offense flow. Map selection and composition passed, but the clean run had zero eligible staff; the capture and report were rejected as battle evidence.
- Added a recruitment activation service: recruitment now resolves or creates the live character, converts it to an active NPC employee with `AbilityWork`, and only then commits the recruited record.
- Extended product verification to accelerate four valid customer visits, pointer-click the recruitment card in Operations, assert live staff conversion/expedition eligibility, then continue through the visible offense composition path.
- Explicit verifier import exposed `CS0165` on a short-circuited `out` variable; initialized the recruitment record before the candidate predicate. The stale request file that was repeatedly retrying PlayMode was removed.
- The next product-shell run still found no recruitment candidate. Auditing the product scene exposed two real loop gaps: `RegularCustomerRuntime` was never present, and `CharacterSpawner` serialized only an NPC test asset while the actual customer existed only in the Resources catalog.
- Registered and eagerly created `RegularCustomerRuntime` under the gameplay lifetime scope, then made `CharacterSpawner` merge catalog customer definitions once after injection. Unity compiles these changes with `Error 0 / Warning 0`.
- Corrected the character catalog dependency to the existing `IRunCharacterCatalog`, and reordered the lifetime-scope build callback so scene injection always completes before the generated recruitment runtime is resolved.
- Bound each regular-customer record to the exact live visitor that produced the record. Recruitment now converts that actor first, falling back to an ID lookup only after save restoration or actor loss.
- The full product-shell verifier now passes through real pointer input: Hard difficulty, customer recruitment into an eligible NPC worker, map/composition, battle start, guard, dungeon switch, manual save, attack/target, exact V2 reload, title autosave, Continue, and missing-save handoff. Captured errors/warnings are both zero.
- Visually inspected `offense-turn-battle.png` and `offense-turn-battle-changed.png`: round, health, command log, and enemy health changed visibly without UI overlap.
- Fixed the AI macro-goal debug fixture so it creates its own grid instead of depending on unrelated scene state; all 29 implemented scenario suites now pass.
- Updated RunFlow verification to enter `SampleScene` from the title architecture and confirmed two recurring boss cycles, non-terminal defense, stages 1-5 without Victory, and `truth_core` truth-reveal Victory.
- Re-ran Save UI, Unified UI, P1/P2 feature surfaces, character click priority, and room inspection PlayMode verification; every report passes with zero captured errors and warnings.
- Updated P1/P2 offense coverage to use the visible map, target, composition, member, and launch controls, while accepting either an ongoing battle or a legitimate one-hit victory without allowing early truth reveal.
- Added a stable room-overlay MCP capture helper and verified 4 fill cells plus 10 outline segments. Pausing in the same command prevents ordinary hover polling from clearing the overlay between MCP calls.
- Captured and visually inspected the full dungeon room overlay, direct turn battle before/after state change, and final truth-result screen.
- Re-ran independent RoomSystem, RoomEnvironment, OffenseBattle, OffenseWorldMap, and OffenseReward scenarios; all passed and the final Console count is `Error 0 / Warning 0`.
- Rejected scenario-state completion as final evidence and began a clean, pointer-driven player run from `TitleScene`.
- Bought the visible commercial blueprint, waited at X5 for natural visitors, recruited the first real candidate from Operations, selected stage 1 on the world map, composed the party, and won by issuing visible barrier/attack/target commands.
- Fixed the visitor exit stall discovered during that run and added a focused AI scenario.
- Fixed duplicate expedition rows caused by legacy and canonical actor components sharing one GameObject.
- Fixed world-map/composition overlay overlap and added victory return treatment so surviving staff can continue the campaign.
- Added Orc and Vampire customer assets to make the natural recruitment pool large enough for the 2- and 3-member campaign gates.
- Re-ran customer, staff, world-map, battle, and reward regression suites; all five pass. Resource loading confirms three recruitable customer definitions.
- Direct replay exposed duplicate scene/generated recruitment runtimes; changed the lifetime scope to reuse the scene-authored runtime and verified the next clean run had exactly one.
- Direct replay also exposed seed-dependent recruitment starvation at the old 75 satisfaction threshold. Lowered the product/default threshold to 65 while retaining the separate visit-count gate.
- Recruited two employees through the visible Operations UI and directly won stages 1 and 2. Stage 3 then killed both employees despite using species abilities and focused targets, proving the current campaign was not naturally completable.
- Confirmed building training only affects mood and there is no persistent combat-stat growth path.
- Added stage-derived offense preparation, surfaced its bonus/effective power in the UI, and applied the same deterministic multiplier on battle start and exact save restoration.
- Added and passed a full six-stage, all-difficulty campaign-balance regression plus the existing battle, reward, and world-map suites.

## Next

- Tune content breadth: more authored route events, enemy intents, curios, diseases/quirks, and region-specific audiovisual treatment can now build on the completed expedition framework.

## 2026-07-20 - Character progression implementation

- Added actor-owned `CharacterProgression` with levels 1-20, rollover XP, learned skills, three equipped slots, exact loadout restore, and legacy-save defaults.
- Added species/trait skill tracks plus level 2/4/6 shared techniques; offense combat now receives equipped skills only.
- Character levels increase offense combat statistics by 4% per level and appear in expedition, battle, and character UI.
- Training use, completed work, battle outcomes, and successful expedition returns award XP at their single completion points.
- Character world saves now capture and restore level, XP, learned skill IDs, and equipped skill IDs.
- Added a generated `성장` tab with an XP meter and pointer-clickable skill loadout controls.
- Added and passed progression curve, actor isolation, unlock/loadout, persistence, legacy default, and training reward scenarios.
- Extended Unified UI PlayMode verification to click the Growth tab and toggle a skill through UI pointer events.
- Focused PlayMode save verification restored `Lv.6 / XP 77 / equipped 2` through the real game save service and JSON boundary.
- The pointer-driven six-region campaign still reaches `truth_core`; the three recurring party members finished at levels 6, 5, and 3 with 4, 3, and 2 learned skills.
- Final progression plus offense regression passed and Unity Console finished at `Error 0 / Warning 0`.
- Updated the stale full-save offense expectation and made orphan battle snapshots fall back to the valid saved journey; the complete game save round trip now passes too.

## 2026-07-20 - Weak-link audit

- Audited cross-system runtime paths and prioritized ten weak links across identity, progression pacing, LLM dependency, passive execution, room quality, offense preparation, formation, persistence, rerolls, and growth feedback. No gameplay code changed in this audit.
## 2026-07-20 Closed-loop integration implementation started

- Preserved the existing dirty worktree and scoped the approved follow-up to identity/save, skills/combat, room/work, equipment/recovery, UI, and direct-play verification.
- Added active phases 20-26 to `task_plan.md`; Phase 20 starts with the string identity registry and V4 persistence boundary.
- Converted regular-customer records, staff-discontent records, social rumors, and world profiles to string persistent IDs at their save/runtime boundaries.
- Added V4 save fields for per-actor social memory, global facility reputation, and staff discontent; staff discontent now captures/restores by persistent staff ID instead of shared templates.
- Strengthened focused EditMode coverage: regular customer, staff discontent, character progression/population restore, and duplicate world-profile ID rejection all pass. Unity Console remains `Error 0 / Warning 0`.
- Changed the level curve to the approved `20 + floor((level-1)/10)*5` formula; level 1->50 now requires 1,460 XP and is covered by progression scenarios.
- Connected offense node XP to real expedition resolution: event/camp/cache, normal battle, elite battle, boss battle, and successful return now award the approved stage-scaled XP values.
- Added offense pacing coverage showing the combat-heavy route reaches level 50 by stage 3 while the safer route reaches it by stage 4, with both avoiding earlier max-level drift.
- Generated active skills now store authored formation constraints. Damage skills convert to front/middle attacks, control skills to middle/rear, and support skills to middle/rear without LLM override.
- Added a guarded `CharacterSkillExecutionContext` path and connected battle-start, damage-taken, and enemy-defeated passive triggers through the real offense battle runtime. Focused progression/offense scenarios pass with Unity Console `Error 0 / Warning 0`.
- Fixed the equipment crafting UI path discovered by PlayMode: building info now becomes interactable immediately when opened, so visible craft buttons are not ignored during the fade-in frame.
- Strengthened `ExpeditionEquipmentPlayModeVerifier` to seed and verify the actual queried warehouse inventory, pointer-click the craft button, assert craft queue creation, material withdrawal, craft work completion, equipment equip, nonblank screen capture, and zero captured errors/warnings.
- The pointer-driven equipment loop now passes: `queue=0->1`, weapon stock `35->33`, Iron Edge inventory `0->1`, equip state visible in the expedition detail, and Unity Console `Error 0 / Warning 0`.
- Hardened offense save capture/restore against JsonUtility blank `activeBattle` objects: only nonempty battle snapshots are restored, and capture only keeps a battle that matches an active `InBattle` expedition.
- Extended the full-game V4 save round trip to include expedition equipment inventory, reserved loadout, craft queue, and expedition recovery stress. It now passes with `warnings=0`, one active expedition, one intruder, and exact equipment/recovery restoration.
- Re-ran focused closed-loop EditMode scenarios after the fixes: character progression, offense battle, modular facility, and room environment all pass. Final Unity Console check remains `Error 0 / Warning 0`.
- Added saved level-growth allocation records and a public stat-breakdown API so the growth UI can show base, species/trait, level, equipment, conditional passive, and final values without duplicating calculation logic.
- Rebuilt the generated character growth tab as a scrollable surface and added visible stat breakdowns, combat-only equipment notes, and recent growth-allocation reasons.
- Updated Unified UI PlayMode verification to open the Growth tab by pointer input and require the stat breakdown headings. It passes after fast-committing a real start party, with updated capture `Temp/phase67-character-growth-tab.png` and `capturedErrors=0; capturedWarnings=0`.
- Strengthened progression EditMode coverage so level 50 creates 59 allocation records, records match `levelGrowthStats`, and stat breakdowns match final stat calculation. Focused progression, offense battle, modular facility, and room environment scenarios pass with Unity Console `Error 0 / Warning 0`.
- Extended the V4 full-game save round trip again so the expedition test member levels up once and the new allocation record must survive JSON capture and restore. The PlayMode round trip passes with `jsonBytes=102875`, `warnings=0`, and Console `Error 0 / Warning 0`.
- Added direct-play recovery and recruitment gates to `NaturalRunPlayModeVerifier`: after each real offense stage it now waits for actual staff health/stress recovery and recruits through the visible Operations UI when the next region requires more members. No scenario-state injection is used for these gates.
- A clean commerce-strategy replay drove the UI through new run, blueprint purchase, research priority, day-7 defense placement, and stage-1 offense victory. Stage 2 then correctly blocked on readiness instead of launching, exposing that injured/stressed staff were not actually selecting recovery facilities (`ready=0/1`, hp `79%/41%`, stress `62/66`).
- Fixed that recovery connection gap: `Rest.asset` now uses the facility-aware `NeedRest` consideration, staff self-care facility considerations no longer require a guest visit count, and on-duty stressed staff can select Hygiene. The StaffDuty recovery scenario now verifies actual Rest/Hygiene action scores, destinations, and job-giver selection against P1 recovery facilities.
- Re-ran recovery-linked scenarios after the patch: staff duty, offense journey, and modular facility all pass with Unity Console `Error 0 / Warning 0`.
- Fixed a stale compile blocker in `NaturalRunRuntimeDebugProbe` (`IGameDataProvider.TryGetGameData` is the real API) so Unity no longer executed old assemblies while reporting outdated StaffDuty diagnostics.
- Corrected the StaffDuty recovery test contract: `TryFindBestScoredAction` is only a scored candidate and does not populate `AIAction.destination` until commit; the scenario now validates `TryResolveDestinationWithFailure` for Rest/Hygiene destinations. Focused closed-loop regressions pass again: StaffDuty, OffenseJourney, CharacterProgression, OffenseBattle, ModularFacility, and RoomEnvironment with Console `Error 0 / Warning 0`.
- Direct commerce replay after those fixes reached a real stage-1 expedition through natural UI/time flow, including day-7 defense placement. It then failed at the stage-1 boss: the two starting staff survived the first battle/cache/elite path, but one entered the boss route at roughly 6 HP and both died with the boss still at 50/82 HP. This is now an intra-expedition attrition/balance issue, not a recovery-facility selection issue.
- Updated the direct-play verifier to recruit and use the full three-person main party from stage 1, load actual available supplies from the preparation snapshot, spend medicine before dangerous route choices, and prefer camp nodes when health/stress justify it. This keeps the fix in the player automation path rather than weakening battle resolution.
- Fixed two headless listener regressions exposed by the focused suite: `EventAlertRuntime` now preserves records while skipping UI rendering when no presenter factory is injected, and `FacilityEvolutionRecordRuntime` ignores events until its recorder is injected. Focused closed-loop regressions pass again: OffenseJourney, OffenseBattle, OffenseReward, StaffDuty, CharacterProgression, ModularFacility, and RoomEnvironment with Console `Error 0 / Warning 0`.
- The next direct commerce replay passed the Day 7 pointer-driven defense build and Day 10 boss-defense trigger, then failed before stage 1 launch because the strongest candidate had 3 visits and 83.2 satisfaction but default recruitment still required a 4th visit. Lowered only the default recruit-candidate visit threshold to 3 and updated the recruitment regression to match the V4 persistent-person model where shared `CharacterSO` templates remain spawnable while the recruited persistent ID is promoted. Recruitment, StaffDuty, OffenseJourney, OffenseReward, and OffenseBattle regressions pass with Console `Error 0 / Warning 0`.
- A follow-up natural replay showed the code default was not enough because `SampleScene` had serialized the old recruitment threshold. Updated `SampleScene`'s `RegularCustomerRuntime` rule from 4 visits to 3 visits and re-ran recruitment plus offense journey/reward regressions with Console `Error 0 / Warning 0`.

## 2026-07-21 Closed-loop feature verification pass

- Switched from slow day-10/direct campaign play to focused feature tests as requested.
- Fixed stale QA contracts after the V4/string-identity and Craft-work additions: WorkPriority now expects `Craft`, staff rebellion/facility evolution/AI plan/P1P2 fixtures assign persistent IDs, and Save UI/ProductShell pointer helpers recreate or force the verification mouse when Unity input state sticks after scene changes.
- Re-ran focused EditMode failures (`WorkPriority`, `FacilityEvolution`, `StaffRebellionResponse`, `CharacterAiPlan`) and the broad implemented-scenario runner; all passed with no recent Console errors/warnings.
- PlayMode feature tests passed: camera movement stays unscaled by game speed, Save UI pointer save/load/delete works, Unified UI start/growth/event surfaces pass, ProductShell passes, and P1/P2 feature surface rows pass `18/18`.
- Added a file-request driven feature batch for MCP-independent PlayMode verification and fixed the QA-only `CS1626` compile error caused by yielding inside a `try/catch` coroutine.
- Final feature batch passed in one chain: BuildPlacement, CharacterClick, RoomInspection, ExpeditionEquipment, and SkillRuntime. Reports show `capturedErrors=0` and `capturedWarnings=0` where applicable.
- Current proof artifacts include `Temp/build-placement-ux-report.txt`, `Temp/phase47-exclusive-world-info-report.txt`, `Temp/room-inspection-playmode-report.txt`, `Artifacts/QA/expedition-equipment-playmode-report.txt`, and `Temp/character-skill-runtime-playmode-report.txt`.
- MCP direct Camera_Capture remains unavailable/revoked in this continuation, so Phase 26 direct-play/camera-capture evidence is intentionally still pending rather than claimed complete.

## 2026-07-21 Physical item and hauling implementation

- Added `DungeonItemCatalogSO`, `ItemHaulingSettingsSO`, `WorldItemStackRuntime`, `CharacterCarryInventory`, `AbilityHaul`, `AIHaul`, and `ItemPileInfoPanel`.
- Save data is now V5 and captures physical world stacks, hauling settings, and per-character carried items.
- Added `GridLayer.Item` and changed click priority to `Character > Item > Building`; `Alt` click forces item pile selection on a character-occupied cell.
- Delivery purchases and stock rewards now spawn loose world stacks at the entrance dropoff when the physical item runtime is active; gold remains abstract.
- Staff/owner hauling can reserve loose stacks, walk to the pickup cell, carry within character weight limits, suffer overburden speed penalties, and deposit stock categories into warehouse inventory.
- Shop restock uses the already-present physical restock route from warehouse to shop; successful purchases and shoplifting now add carried items to the customer.
- Options UI has a `최대 운반 배율` slider, and character info shows carried weight, base limit, max allowed weight, overburden penalty, and carried item names.
- The item pile panel opens as a singleton runtime listener, shows pile rows, switches the same panel to stack detail on row click, and returns to the list with a Back button.
- Created default `Assets/Resources/SO/Items/DungeonItemCatalog.asset` and `Assets/Resources/SO/Items/ItemHaulingSettings.asset` so item authoring and hauling tuning are visible in Unity.
- Added `PhysicalItemDebugScenarios`; the focused contract report passes for stock fallback, carry weight penalty, pile sorting/detail, stack delete fallback, warehouse aggregation, and V5 JSON save payloads.
- PlayMode MCP smoke verified `WorldItemStackRuntime.Active`, dropoff stack spawn, pile lookup, one-cell fallback marker bounds, item pile panel opening, row detail view, and Back navigation.
- Current verification state: Unity compile succeeds and Console is `Error 0 / Warning 0`.
- Expedition return supplies and loot now prefer entrance dropoff stacks when the physical item runtime is active, with direct warehouse deposit kept only as a fallback.
- Converted equipment craft materials/output and outbound expedition packing to physical stack flows, then added dedicated PlayMode coverage for actual `AIHaul` movement across loose-stack warehouse deposit, facility input delivery, craft material delivery, crafted-equipment output hauling, expedition supply packing/consume, and character carried-weight UI.
- Fixed two logistics bugs exposed by that verifier: `AbilityHaul.StartHauling()` no longer clears the freshly reserved job before starting its coroutine, and warehouse delivery now selects the nearest reachable warehouse delivery cell instead of whichever warehouse the scene query returns first.
- New proof artifact: `Artifacts/QA/physical-item-logistics-playmode-report.txt` reports `RESULT=PASS; failures=0`, with `capturedErrors=0` and `capturedWarnings=0`; carry UI capture is `Artifacts/QA/physical-item-carry-ui.png`.
- Re-ran the physical item pass for the current request. EditMode contracts pass in `Temp/physical-item-contracts.tsv` and the current Console is `Error 0 / Warning 0`.
- Fixed the item-pile PlayMode verifier entry path after it failed against the new start-party flow (`RUN_READY owner selection is still active`). It now uses the same fast start-party commit path as the logistics verifier and supports a request-file runner at `Temp/physical-item-pile-playmode.request`.
- Re-ran both physical PlayMode suites from the current assemblies: `Artifacts/QA/physical-item-pile-playmode-report.txt` and `Artifacts/QA/physical-item-logistics-playmode-report.txt` both report `RESULT=PASS; failures=0`, `capturedErrors=0`, and `capturedWarnings=0`.
- Added the final warehouse-storage polish: stored physical stacks mirror warehouse aggregate stock, stay hidden by default, and appear only while the new top-right `물품` toggle is enabled. The toggle turns itself off during build/grid modes.
- Restoring V5 physical item data now resynchronizes warehouse aggregate inventory from stored physical stacks when those stored stacks exist, preventing the old direct-inventory view from drifting away from the physical source of truth.
- Extended the V5 save contract so it explicitly round-trips a character `carryInventory` item. The updated `Temp/physical-item-contracts.tsv` reports `save_v5_contract PASS version=5; stacks=4; carried=3`.
- Re-ran the current Unity physical item verification after the save-contract change: EditMode physical contracts all pass, the UI feature batch reports `UI_REGRESSION_BATCH PASS` for `PhysicalItemPile` and `PhysicalItemLogistics`, both PlayMode reports end with `RESULT=PASS; failures=0`, and Unity Console is `Error 0 / Warning 0`.
- Resumed Phase 26 direct-play verification and found the stage-3 party was losing its recruited staff member because staff promotion was not authoritative across population binding/release. `WorldCharacterProfile.isStaff` now reasserts NPC runtime identity on bind/refresh/promote/release, and the spawner keeps staff profiles active instead of sending them back to the visitor pool.
- Added a regression to simulate a promoted staff actor whose type was reset to `Customer`; `CharacterPopulationDebugScenarios`, `StaffDutyDebugScenarios`, `CharacterProgressionDebugScenarios`, `OffenseJourneyDebugScenarios`, and `OffenseRewardDebugScenarios` all pass after the fix.
- Updated stale reward tests for physical rewards and the minimum-two recruit-candidate rule, then made `CharacterCarryInventory` single-instance and fixed physical-item fixtures to use `Ensure`. The refreshed `Temp/physical-item-contracts.tsv` reports every physical item case as `PASS`, and the current Console has `Error 0 / Warning 0`.

## 2026-07-22 Phase 26 direct-play completion

- Resumed the pending no-injection commerce direct-play campaign after the physical item work and reproduced the remaining failure at stage 6 `truth_core`: the verifier skipped usable camp recovery and entered the final boss with an under-recovered/under-leveled party, causing a real defeat.
- Fixed the verifier's natural-play strategy instead of weakening encounter stats: late-stage loadouts now reserve enough rations to enter and use camp, cautious camp selection accounts for the travel ration cost, enemy-directed non-damage combat modules are treated as usable attack/control skills, and the final stage waits for a Lv50 party with safe health/stress.
- Re-ran focused regressions after the patch: `CharacterPopulationDebugScenarios`, `StaffDutyDebugScenarios`, `CharacterProgressionDebugScenarios`, `OffenseBattleDebugScenarios`, `OffenseJourneyDebugScenarios`, `OffenseRewardDebugScenarios`, and `PhysicalItemDebugScenarios` pass.
- Clean `commerce` natural run now reports `NATURAL_RUN PASS strategy=commerce`. Stage 6 logs show `final:maxLevel=3/3`, camp `useSupply=True`, medicine before boss, `OFFENSE_STAGE_6_BATTLE_COMPLETE outcome=Victory`, and `OFFENSE_CAMPAIGN_FINISH completed=6/6; truth=True; outcome=Victory`.
- Evidence artifacts: `Temp/natural-run-commerce-report.txt`, final-result HUD `Temp/natural-run-commerce.png`, 1600x900 HUD `Temp/natural-run-commerce-1600x900.png`, 900x1600 HUD `Temp/natural-run-commerce-900x1600.png`, and MCP `Unity_Camera_Capture` from Main Camera instance `65154`.
- Final Unity Console check after captures and stopping PlayMode is `Error 0 / Warning 0`.

## 2026-07-22 Start preparation scene split

- Began implementing the approved `TitleScene -> StartPreparationScene -> SampleScene` preparation flow.
- Confirmed the current product flow still routes new games directly to `SampleScene`, where gameplay UI opens `OwnerSelectionPanel`.
- Confirmed `CharacterProgression` still hard-codes three normal active slots and two passive slots, so owner-only extra skill display/validation needs a role-based slot profile rather than a UI-only change.
- Added phases 35-38 and the new owner/preparation product decisions to `task_plan.md`.
- Added `PreparedStartPartySnapshot`, role-based `CharacterSkillSlotProfile`, and owner fixed skill support. Owners now expose four fixed owner-skill slots separate from generated active/passive/ultimate growth slots, and those fixed skills run through the existing skill effect path without becoming LLM-generated or rerollable.
- Split the scene flow so title new-game requests open `StartPreparationScene`; prepared runs then enter `SampleScene` with a handoff snapshot. Direct `SampleScene` opens still keep the old owner-selection fallback for QA.
- Added `DungeonPreparationLifetimeScope` and generated `StartPartyPreparationUiController`: owner selection shows owner portraits, large focus area, fixed skills, traits, and doctrine summary; party preparation shows one locked owner, two selected staff, four reserves, detail tabs, reroll buttons, reserve swaps, team summary, and gated start.
- Added `PreparedStartPartyGameplayApplier` so prepared starts spawn exactly the selected owner plus two staff, restore growth state/selected skills, assign persistent IDs, and suppress the gameplay owner-selection panel in the product path.
- Unity compile/refresh succeeded through MCP with Console `Error 0 / Warning 0`. Build settings now include `TitleScene`, `StartPreparationScene`, and `SampleScene`.
- Editor service verification passed: same-species staff roster, 7 total members, 3 selected, 4 reserves, owner locked, owner swap rejected, staff/reserve swap accepted, owner fixed skills 4/4, and prepared snapshot valid with two staff.
- PlayMode pointer verification passed from the title screen: clicked new game and normal difficulty, reached `StartPreparationScene`, advanced owner selection, selected a reserve, revealed swap buttons, prepared active skills through the runtime service, clicked start, and landed in `SampleScene` with one owner, two staff, three actors total, and zero active `OwnerSelectionPanel`s.
- Visual evidence captured: `Artifacts/QA/start-preparation-owner-select-endframe.png`, `Artifacts/QA/start-preparation-party-prepare.png`, `Artifacts/QA/start-preparation-party-ready.png`, and `Artifacts/QA/start-preparation-sample-scene.png`. PlayMode save-protection snapshot was restored after the run.

## 2026-07-22 Start preparation UX correction

- Fixed the reported start-button blocker by treating the owner as start-ready through the four fixed owner skills. Selected staff still require first active selection and first passive readiness.
- Added drag/drop roster swapping: selected staff and reserve staff cards now exchange through the existing `TrySwapWithReserve` service path, while the owner remains locked.
- Reworked the staff detail page away from one flat text block into a RimWorld-style structured panel: portrait area, readiness label, section tabs, identity rows, trait cards, potential summary, stat bars, skill slots, and active candidate cards.
- Removed the bottom reroll button row from the new preparation scene. Full/partial rerolls now appear as compact dot-dice buttons next to the relevant character or section.
- Localized visible preparation status messages that still leaked English text, including drag-swap and start handoff failures.
- Verified in PlayMode with a UI-event smoke runner: `members=3`, `reserves=4`, `drag_swapped=True`, selected staff readiness `3:True,2:True`, `owner_ready=True`, `start_interactable=True`, `final_scene=SampleScene`, and `owner_panels=0`.
- Visual proof artifact: `Artifacts/QA/start-preparation-final-party.png`. Final Unity compile succeeded and Console is `Error 0 / Warning 0`.

## 2026-07-22 RimWorld-style work amount and construction sites

- Added the V9 work-order pipeline: player placement creates a `ConstructionSite` on `GridLayer.Construction`, tracks `WorkOrderId`, required/completed work, delivered materials, reserved worker, and status, then swaps to the final building only on completed work.
- Routed work-unit execution through the shared work loop for construction, repair, research, equipment crafting, cooking/survival work, butchering, water, treatment, and refuel. Existing seed/new-run buildings still spawn completed.
- Building static work requirements live in ability modules; runtime progress, delivery state, worker reservation, and save payloads stay out of shared SOs.
- Updated world selection priority to include construction between item and building, and added construction-site info UI with target, status, material delivery, worker reservation, progress, and cancel action.
- Extended BuildPlacement PlayMode verification from ghost-only UX to actual pointer placement: build tab/category/item click, visible grid/ghost, world click creates a construction site, final building is not present instantly, work order exists, progress reaches 45%, completion swaps to final building, and the construction layer clears.
- Visual proof artifacts: `Temp/build-placement-ux.png`, `Temp/build-placement-construction-site-info.png`, and `Temp/build-placement-construction-progress.png`.
- Regression results: `WorkPriorityDebugScenarios=True`; `ImplementedScenarioDebugRunner` reports `Suites: 30`, `Passed: 30`, `Failed: 0`, including `P1 Work amount` and `P1 Work priority`; BuildPlacement PlayMode batch reports `UI_REGRESSION_BATCH PASS`; Unity Console is `Error 0 / Warning 0`.

## 2026-07-23 Wildlife ecosystem v1

- Added the V10 wildlife ecosystem layer: exterior habitat patches, diet/intent fields, hunger/thirst-driven target choice, territory return, predator/prey behavior, habitat-gated respawn pressure, and ecosystem save data.
- Wildlife now auto-generates grass, water, brush, burrow, and lair patches on usable exterior surface cells when no scene-authored `WildlifeHabitatMarker` exists.
- The wildlife info panel now shows Korean player-facing state, intent reason, hunger, thirst, danger, territory, expected yields, and hunt controls.
- The Operations survival section now includes wildlife abundance, food/water patch status, predator danger, respawn wait, and live wildlife rows.
- Expanded `GameplayScene` and `SampleScene` physical grid width to restore a real exterior surface band; PlayMode inspection reports 30 exterior surface cells instead of animals being packed into the entrance sliver.
- Verification: `WildlifeDebugScenarios.RunAll`, `RunPlayModeSnapshot`, and `RunPlayModeHuntLoop` all passed in the current PlayMode assemblies. Runtime inspection reports six active wildlife with `Drink`/`Rest` ecology intents and six habitat overlay renderers.
- Visual check: `Unity_SceneView_Capture2DScene` captured the exterior band with habitat overlay visible on exterior ground. `ScreenCaptureAsTexture` returned a black GameView frame in this editor state, so that artifact was rejected rather than counted.
- Current Unity Console after verification is `Error 0 / Warning 0`.
## 2026-07-23 Nameplate, wildlife motion, and camera zoom follow-up

- Started a focused follow-up for three player-reported regressions: world nameplates render behind dungeon art, wildlife visibly oscillates left/right, and mouse-wheel camera zoom has no effect.
- Preserving the heavily modified worktree and limiting edits to the involved runtime/UI/AI paths plus focused verification coverage.
- Moved world character name text and its backing line to the `UI` sorting layer while retaining actor-relative order. Runtime inspection reported every active nameplate at `UI:38`, and the gameplay capture shows names above dungeon floors, walls, furniture, and characters.
- Replaced deterministic wildlife left/right selection with near-best weighted targets, direction momentum, reversal penalty, arrival dwell by intent, immediate threat interruption, sprite facing, and eased/bobbed locomotion. Same-cell habitat decisions now fall back to natural roaming instead of repeated no-op routes.
- Restored zoom in `GameplayScene`, `SampleScene`, and `CharacterAiTestScene`, narrowed wheel blocking to actual `ScrollRect` UI, and made zoom cooperate with the URP Pixel Perfect Camera rather than being reset during rendering.
- Focused contracts passed: `WorldCharacterNameplateDebugScenarios.RunAll=True` and `WildlifeDebugScenarios.RunAll=True`, including the new movement dwell/facing case.
- Pointer-driven camera verification passed while paused, at 1x, and at 5x. Wheel zoom measured `8.438 -> 7.588 -> 8.438`; movement distances differed by only `0.0017`, and the verifier captured `Error 0 / Warning 0`.
- PlayMode wildlife samples showed nine animals spread across `Rest`, `Forage`, `Drink`, `Wander`, and `ReturnToTerritory`; most were stationary at meaningful targets while only one was moving in each sample window.
- Visual evidence: `Artifacts/QA/nameplate-wildlife-default.png`, `Artifacts/QA/nameplate-wildlife-zoomed-in.png`, and `Artifacts/QA/wildlife-natural-motion-exterior.png`. Final Unity state is idle, compiled, and Console `Error 0 / Warning 0`.
- Unity MCP `Camera_Capture` also succeeded at 1920x1080 using the Main Camera GameObject ID and confirmed the world nameplate remains visible above the dungeon render layers.

## 2026-07-23 Customer checkout patience

- Audited the staffed checkout coroutine, shop work urgency, shopping visit bookkeeping, personality modifiers, mood factors, personal facility memory, activity logs, and event alerts.
- Confirmed the root behavior gap: staffed checkout waits indefinitely and never branches by patience, while visit bookkeeping cannot distinguish purchase completion from queue abandonment.
- Added `CustomerCheckoutPatienceRules`: personality patience and species/trait wait modifiers determine restless, service-request, and abandonment thresholds, with modest visible-queue pressure.
- Staffed checkout now updates the character phase with queue position and elapsed seconds, applies a small restless mood factor, wakes idle workers again when called, and emits one-shot player alerts.
- On timeout the customer receives a stronger mood penalty and personal facility complaint memory, releases the checkout queue, avoids only that shop, preserves the remaining visit, and lets existing Utility AI choose an alternative or leave.
- EditMode customer AI contracts passed, including stage boundaries, alternate-shop handoff, memory persistence input, and the real checkout iterator reaching abandonment and releasing its queue.
- PlayMode probe passed with `outcome=Abandoned`, `waiting=0`, `mood=70->64.5`, `sentiment=-0.28`, `alerts=2`, and visible phase `구매 포기`. Final Unity Console is `Error 0 / Warning 0`.
# 2026-07-23 Paused stair and low-needs AI stabilization

- Traced the paused stair appearance to the traversal visibility fail-safe using unscaled realtime, not to a world DOTween.
- Traced combined low-need instability to leisure being treated as an emergency, single-need emergency fallback, missing owner self-care actions, and on-duty hunger being ineligible for eating.
- Changed traversal visibility deadlines and delayed restoration to scaled game time. A PlayMode probe held `Time.timeScale=0` for 0.45 seconds: the actor stayed hidden, then restored only after simulation resumed.
- Added survival-only strongest-need selection and emergency candidates for every urgent hunger, rest, toilet, and hygiene need. If the highest-scoring facility is unavailable, the next valid survival response can now run instead of falling through to wait.
- Added owner Eat/Rest/Toilet/Hygiene actions and allowed sufficiently urgent hunger/rest to start during duty. Hunger interrupts current work with `식사 필요` but does not flip the worker into off-duty state.
- Focused AI naturalness regressions passed, including leisure exclusion, combined low-need triage, owner self-care, and worker hunger interruption. The final PlayMode probe passed all 10 assertions for pause visibility and staff/owner low-needs behavior.
- Removed the temporary PlayMode probe and recompiled the Editor assembly. The recent compiler-error scan is empty and `git diff --check` passes for the touched files.
- Re-ran the complete staff-duty suite on the current assembly. The new low-needs scenarios pass; the suite remains red only on the separately tracked fixture failures `Emergency priority` (`Repair` candidate is rejected before assignment) and `Expedition return` (`AIWait` wins after return).

## 2026-07-23 Stationary AI fallback follow-up

- Started a focused audit after the runtime AI panel showed a character repeatedly selecting `Emergency -> WaitJobGiver` with every need action rejected and no target.
- The target behavior is now explicit: ordinary waiting must become a short contextual micro-action or reachable roam, while low mood should produce a bounded self-directed impulse and later return to normal utility decisions.
- Confirmed the repeat loop: urgent but currently unsatisfiable needs keep the BT in Emergency, while high recent movement pressure selects a nominal micro-action implemented as another static wait.
- Converted inspection and generic idle fallbacks to actual reachable roaming, added a stronger mood-driven wander, and left only purposeful queue/chat/shelter waits as short stationary actions.
- Low mood now suppresses ordinary work at both routine and final candidate selection. Critical mood also interrupts an active work coroutine with a visible player-facing reason.
- Added focused regressions for low-mood movement without an LLM impulse and critical-mood work interruption. `CharacterAiNaturalnessDebugScenarios.RunAll` reports `FINAL_NATURALNESS_REGRESSION PASS`.
- The actual GameplayScene PlayMode probe visited six cells while reporting `RoutineUtility: 대기 / 기분 내키는 대로 배회` at mood 17-20. Unity finished idle and compiled with Console `Error 0 / Warning 0`.

## 2026-07-23 Dark fantasy deprivation and breakdown survival

- Implemented V11 deprivation burdens for hunger, thirst, bladder damage, contamination, exhaustion, and mental instability, including health damage, breakdown probability, and guaranteed failure at sustained maximum burden.
- Connected the new `DeprivationBreakdown` BT branch to dedicated desperate relief/drink/eat/collapse action sets, violent breakdown behavior, nonlethal guard suppression, humanoid deaths, corpses, cannibalism, and emergency butchery.
- Added shared physical exterior water for humans and wildlife, water terrain/tile rendering, floor filth and wall stains, room/exterior cleanliness effects, and work-unit-based Clean targets with a player priority command.
- Added the character Health tab, overhead breakdown warning, filth detail panel, V11 save/restore snapshots, source-character metadata, taboo memories, and nonmergeable humanoid corpses.
- Verification passed: legacy survival scenarios `6/6`, dark-survival scenarios `9/9`, and pointer-driven PlayMode report `RESULT=PASS; failures=0` with captured `Error 0 / Warning 0`.
- Visual evidence: `Artifacts/QA/dark-survival-world-water-and-filth.png`, `Artifacts/QA/dark-survival-health-and-filth.png`, plus a successful Unity MCP `Camera_Capture` of the live gameplay world.
- Follow-up verification now proves clean-water facility priority, unsafe exterior-water fallback, personality-adjusted breakdown chance, permanent taboo relationship memory after restore, and nonlethal suppression (`118 -> 115.5 HP`, actor alive, breakdown ended).

## 2026-07-23 Exterior flowers, trees, rocks, and grazing visuals

- Added `WildlifeHabitatDecorationPaletteSO` and generated a single authored palette with 6 flower clusters, 3 summer trees, and 3 rock variants from the existing TINY FOREST pack.
- Added `WildlifeHabitatDecorationRuntime`: Grass/Brush patches receive consumable flower clusters, Brush receives trees, Burrow/Lair receives rocks, and extra trees/rocks are deterministically scattered over valid exterior ground.
- Flower visibility follows habitat resource in stages. Depleted patches hide every flower and regeneration restores clusters progressively; no decoration occupies the grid or alters movement.
- Added forage-intent patch filtering and immediate visual refresh after an animal consumes a patch.
- Wildlife contracts pass, including `full=5 -> depleted=0 -> regrown=3`; the clean PlayMode snapshot confirms one runtime root under `__Runtime/Exterior`, correct `OutsideObject` sorting, and populated flower/tree/rock visuals.
- A live GameplayScene probe moved a herbivore onto the flower patch and measured `resource 8.781223 -> 0`, `flowers 5 -> 0`, then `resource 10`, `flowers 5` after regrowth. Unity MCP Camera Capture confirmed grounded trees/rocks, visible flower beds, and actors rendered in front.

## 2026-07-23 Exterior pond visibility follow-up

- Traced the missing water to two default source cells at the entrance/drop-zone edge plus a locked gray runtime Tile rendered one cell above the floor.
- Default generation now uses only `ExteriorPath` surface cells and creates a four-cell pond at the outer end of the longest run: three walkable unsafe shallows and one blocked foul deep-water boundary cell.
- Reworked the runtime visual into a point-filtered 16x8 water strip, enabled per-cell tint, aligned it to the floor, and kept it above exterior ground but below actors and decoration.
- Live GameplayScene verification reports source cells `(56..59, 0)`, tile occupancy `4/4`, `Wall:2` sorting, a connected exterior path through the shallow edge, and `Error 0 / Warning 0`.
- Unity MCP Camera Capture at 1920x1080 shows the blue/teal pond grounded at the far exterior edge. Focused dark-survival contracts remain green.

## 2026-07-23 Zoom-responsive sky and centered dungeon

- Extended `DungeonSceneBackdropFitter` to consume the injected main camera and fit the solid sky to the padded camera viewport in `LateUpdate`, including orthographic size and aspect changes.
- Centered the physical dungeon interior within the 60-column world, shifted authored GameplayScene placements by `+13`, and moved the entrance/drop-zone area tags with the layout.
- Added start-time dungeon centering to `CameraManager`; live verification reports camera X and dungeon center both at `-29.5`.
- Confirmed both outer-wall boundary tiles, captured the maximum zoom-out frame, and verified no uncovered sky band remains.
- Physical-world, background-lighting, and grid-foundation regression suites pass. Final Unity Console is `Error 0 / Warning 0`.

## 2026-07-23 Entrance outer-wall adjacency

- Traced the reported one-cell entrance gap to automatic wall generation treating three invisible exterior activity markers as structural occupants.
- Limited automatic wall content to actual Building/Hallway layers and added a regression proving overlay/fixture markers cannot displace the wall.
- Fresh GameplayScene verification changed the rendered wall from X `12` to the adjacent X `13`; Unity MCP Camera Capture confirms the arch and outer wall now touch.
- Grid visual, foundation, and physical-world regressions pass. Final Unity Console is `Error 0 / Warning 0`.

## 2026-07-23 Exact facility world click

- Removed ordinary facility selection through the approximate grid occupant fallback. Facilities and construction sites now require an actual collider hit at the pointer position.
- Limited collider-free grid selection to the exact `GridLayer.Building` cell for structural walls and interior doors, and explicitly excluded hallway/floor objects from physics-hit building selection.
- Added a static classification regression for hallway, wall, interior door, dungeon door, and a normal facility.
- Extended the Input System PlayMode verifier with an exact facility click and a collider-free bare hallway click. It also retains the character-over-building exclusivity checks.
- Repaired the QA gameplay fallback so the shared start-party driver recognizes `StartPartyConfirm` and confirms generated legacy candidate skills before entering the world.
- Final `CharacterClick` batch passed: `EXACT_BUILDING_CLICK=PASS`, `BARE_HALLWAY_NO_INFO=PASS`, overlap priority passed, `RESULT=PASS`, captured `Error 0 / Warning 0`.

## 2026-07-23 Consecutive wildlife world click

- Fixed the wildlife popup lifecycle order so `CloseAll()` cannot clear the newly clicked wildlife target when the same panel is already open.
- Exposed read-only wildlife panel diagnostics and extended the wildlife PlayMode contract to send the same target twice.
- Extended the actual world-info pointer verifier to click one wildlife collider twice consecutively without clicking another target between clicks.
- Final report passed `WILDLIFE_FIRST_CLICK` and `WILDLIFE_CONSECUTIVE_CLICK`, retained character/building/floor priority checks, and ended with `RESULT=PASS; failures=0`.
- `WildlifeDebugScenarios.RunAll` passed and Unity Console finished at `Error 0 / Warning 0`.

## 2026-07-23 Wildlife horizontal facing

- Fixed all wildlife reading logical Grid X as screen direction even though the world X axis is mirrored by `Grid.GetWorldPos`.
- `WildlifeActor` now computes facing from movement endpoints in world space while preserving the authored right-facing source sprites.
- Updated the natural-motion regression to cover world-left and world-right routes explicitly.
- A paused fresh GameplayScene probe forced both directions for all four species currently spawned and reported `LIVE_SPECIES_FACING=PASS`; the shared path also covers the fifth catalog species.

## 2026-07-23 Defense interception and engagement

- Started implementation of the approved real-time dungeon-defense engagement plan.
- Confirmed the current defect is structural: manual suppression overlaps cells and deals one-way damage while the intruder movement coroutine continues independently.
- Locked product decisions: on-duty Guard workers only, one lead plus one replacement per intruder, named policies assigned per guard, and immediate owner evacuation to an Administration room with farthest-safe-cell fallback.
- Preserving the existing dirty worktree and integrating with the current V11 invasion, AI, room, UI, and save systems without reverting prior work.

## 2026-07-23 Defense interception and engagement completion

- Added `DefenseEngagementRuntime`, adjacent-cell intercept planning, transient combat-cell reservations, reciprocal attack timing/damage, lead/reserve dispatch, policy retreat and replacement, owner final defense, and combat presentation tied to scaled game time.
- Automatic dispatch now accepts only on-duty non-owner workers with Guard priority. Manual intruder suppression enters the same engagement pipeline, while nonlethal rebellion/deprivation suppression remains separate.
- Added named defense policies with create, duplicate, edit, delete, and per-guard assignment; the defense UI shows live frontline state, lead/reserve guards, exchange count, and owner evacuation status.
- Added immediate owner evacuation to the farthest valid Administration-room cell with a farthest-reachable interior fallback, and kept the owner out of ordinary guard selection until the frontline fully collapses.
- Extended V12 persistence with policies, assignments, owner evacuation, active engagements, reserved cells, attack timers, and exchange counts. Active save round trip passed with no restore warnings.
- Static regressions passed: `DEFENSE_SCENARIOS=True` and `INVASION_REGRESSION=True`.
- Actual PlayMode combat passed: three reciprocal exchanges on distinct adjacent cells, intruder held, both sides damaged, facility damage locked, presentation visible, and save snapshot valid.
- Actual policy switch passed: `state=Engaged`, `leadChanged=True`, cells remained `(1,0)/(2,0)`, facility remained locked, and the old lead resumed AI.
- Actual UI pointer probe passed the Defense tab, policy creation, and guard assignment controls through `PointerDown/PointerUp/PointerClick` events.
- Owner evacuation passed at `(41,2)`, then owner final defense passed at `(40,2)/(41,2)` with 20 exchanges, reciprocal damage, no reserve, and the intruder held.
- Visual artifacts: `Temp/DefensePolicyAndEngagementScheduled.png`, `Temp/DefenseEngagementWorldFinal.png`, `Temp/DefenseGuardEngagementSpaced.png`, and `Temp/DefenseOwnerFinalVerified.png`.
- After clearing Unity 6000.3.8's known startup-only UUM-133323 warning, the complete PlayMode defense verification finished with Console `Error 0 / Warning 0`.

## 2026-07-23 Developer mode and debug palette

- Added settings V2, runtime mode/cheat rules, save metadata/history, 112 modular commands, exact targeting, responsive palette UI, and pooled world overlays.
- Connected cheat hooks to money/items, placement/unlocks, needs/damage, AI, breakdowns, work/construction/research, wildlife, survival, and defense services.
- Added EditMode contracts and a pointer-driven PlayMode verifier for settings, palette tabs, exact spawn, repeat/cancel input, overlays, domain commands, invasions, save metadata, and transient reset.
- Final report `Artifacts/QA/debug-mode-playmode-report.txt` is `RESULT=PASS`; desktop and portrait captures are `debug-palette-1600x900.png` and `debug-palette-900x1600.png`.
- Unity Camera Capture verified overlay on/off rendering and the final Console audit is `Error 0 / Warning 0`.

## 2026-07-23 Construction material physical delivery

- Began tracing the yellow construction material marker reported after placement.
- Confirmed construction-order creation immediately calls the facility-delivery request path and that work readiness later consumes only an actual facility buffer.
- Confirmed the concrete defect: delivery request time withdraws aggregate warehouse stock, removes the stored physical stack, and respawns a visible loose stack at the warehouse cell.
- Added source-storage ownership to physical stack save/runtime data without breaking nested V1 compatibility.
- Facility requests now split warehouse materials into hidden outbound `Stored` reservations while keeping aggregate warehouse stock unchanged.
- Outbound stored stock is haulable through the existing multi-haul planner; pickup now atomically withdraws warehouse stock and carry insertion failure rolls it back.
- Work orders count outbound stored reservations as pending and cancellation returns them to ordinary warehouse storage.
- Updated EditMode and PlayMode expectations so request-time loose piles and early warehouse withdrawal are regressions.
- Unity completed the first recompilation with Console compile errors at 0.
- `PhysicalItemDebugScenarios.RunAll` passed all 12 contracts. Request-time stock remained held, outbound storage reservations were hidden, and save/restore preserved total/reserved/available quantities.
- The existing logistics PlayMode runner now asserts the corrected three-stage transition: unchanged warehouse stock and no loose marker after request, stock withdrawal during AI pickup, then facility-buffer creation after delivery.
- First PlayMode attempt failed at pickup despite the worker reaching the source. The report showed both same-type warehouses shared `warehouse:1050`; the source warehouse lookup selected the wrong instance. This is now tracked as the next fix rather than extending the timeout.
- Replaced shared warehouse type keys with position-qualified persistent keys and added legacy storage-ID normalization during item restore.
- Recompiled and reran all 12 physical-item contracts after the warehouse-key change; all passed again.
- The second request-file PlayMode launch did not auto-enter play and produced no report; the request remains intact, so the next attempt explicitly starts PlayMode instead of recreating the request.
- Explicit PlayMode entry also produced no report, indicating the request callback did not create the verifier runner. The next diagnostic checks active scene and runner presence before invoking the component directly.
- Console inspection found the actual blocker was `CS1503` in the legacy warehouse-ID normalizer. Fixed the narrowed type so the matched object remains an `IWarehouseFacility`.
- Replaced request-time warehouse withdrawal and loose-stack spawning with destination-reserved hidden stored stacks.
- Added position-qualified warehouse storage IDs and legacy-ID normalization so same-type warehouses cannot resolve to the wrong source inventory.
- Added an actual `ConstructionSite + WorkOrderRuntime + AIHaul` PlayMode scenario. It passed with stock `18 -> 18` at request, no loose pile, hidden reservation quantity 2, stock `18 -> 16` at pickup, and the construction order becoming `Ready` after delivery.
- `PhysicalItemDebugScenarios` passed all 12 contracts, including save/restore of ordinary, reserved, and available warehouse quantities.
- `WorkAmountDebugScenarios` passed after correcting its stale V9 assertion to the current V12 save contract.
- Pointer-driven build placement passed: construction site created, final building not instant, partial progress retained, final replacement succeeded, captured errors 0, captured warnings 0.
- Visual inspection of `Temp/build-placement-construction-site-info.png` showed only the construction-site marker at the selected cell and no quantity-badged yellow item pile.
- Final Unity state was stopped and not compiling; after clearing the Console, the audit returned `Error 0 / Warning 0`.

## 2026-07-23 Medieval dark fantasy combat V13

- Added the shared combat model, weapon/armor/shield definitions, individual equipment runtime, loadout policies, ammunition, line-of-sight/cover services, body-health runtime, projectile/melee presentation, and V13 persistence.
- Added nine initial weapons, layered armor sets, two shields, arrow/bolt items, crafting recipes, and three destructible directional cover buildings.
- Defense now gathers physical equipment during invasion warning, waits while intruders rally outside, dispatches after the breach, and combines ranged cover fire with the existing one-on-one interception line.
- Offense now preserves body injuries, bleeding, suppression, ammunition, weapon switches, recoverable throws, and downed state through battle persistence and return to the dungeon.
- Character combat UI exposes body condition, equipment, load, ammunition, cover, hit/evasion calculations, loadout presets, weapon switching, reload, fire mode, and hold fire.
- Shift additive selection, drag selection, exact intruder interception, exact cover movement, and direct Grid movement are connected through `OwnerCommandController`.
- Wildlife hunting now resolves through the same combat service. Ranged hunters choose a safe firing cell, reload from carried ammunition, launch pause-safe projectiles, and apply persistent simplified body injuries.
- Fixed manual move cancellation so evacuation or another movement owner cannot leave AI permanently locked.
- Roslyn runtime/editor compilation passed.
- Static regression result: `combat=True; offense=True; defense=True; priority=True; wildlife=True`.
- Wildlife PlayMode result: `wildlifeSnapshot=True; huntLoop=True`.
- Defense PlayMode result: `exchanges=4; held=True; adjacent=True; bothDamaged=True; facilityLocked=True; save=True; presentation=True; rally=True; approachHeld=True; ownerEvac=True`.
- Player command PlayMode result: movement completed and released its lock; immediate cancellation also reported `cancelReleased=True`.
- Visual artifact: `Artifacts/QA/combat-v13-defense-final.png`; Game View inspection shows separate adjacent fighters, damage numbers, combat labels, and wounded-only health bars.
- Unity MCP `Camera_Capture` failed twice with `Failed to render scene preview`; direct `ScreenCapture` succeeded.
- Final Unity Console audit: `Error 0 / Warning 0`.

## 2026-07-26 V16 isolated feature integration

- Audited the V15 codebase against the approved V16 plan. Confirmed duplicate scene runtimes, two equipment authorities, abstract offense reward counters, daily food withdrawal in addition to real meals, text-only exterior incidents, unused circus milestone data, generic extract stock, and disconnected AI performance settings.
- Removed `Priority Command Controller` and `RegularCustomerRuntime_Test` from `GameplayScene`.
- Added `SingleRequired<T>` scene composition validation and applied it to owner commands and regular customers so missing or duplicate required runtimes fail immediately.
- Began equipment consolidation: common combat equipment now owns craft work orders, physical material requests, unique output instances, queue persistence, and offense loadout access. Removed legacy equipment registration and save-section registration, plus legacy combat-stat double application.
- Unity MCP compilation checkpoint passed with `isCompilationSuccessful=true`; Console currently reports `Error 0 / Warning 0`.
- Remaining legacy source and verifier references are intentionally retained only until their callers are migrated; deleting the types before that checkpoint would hide actionable compile errors.

## 2026-07-26 V16 traversal-cache performance closure

- Split Grid full-content versioning from structural/traversal versioning and moved path, movement, room, lighting, and facility caches to the structural signal.
- Kept current moving wildlife discoverable by checking the actor's live Grid coordinate rather than cached visitable-occupant coordinates.
- Fixed wildlife arrival dwell timing by retaining a scaled `IGameClock` fallback when no test clock is injected.
- Recompiled successfully and passed `GridFoundationDebugScenarios`, `WildlifeDebugScenarios`, and `CharacterAiNaturalnessDebugScenarios`.
- Re-ran the 100-NPC EditMode stress scenario: valid, 2,182 decisions, 51 broker searches, 1 cache hit, 50 deferrals, max 8 searches/frame, Scheduler p95 0.73ms, elapsed 50.6s.
- Previous comparison was 1,440 broker searches, 540 cache hits, 16,461 deferrals, and roughly 353 seconds.

## 2026-07-26 V16 integration and performance verification

- Removed remaining per-decision LINQ and diagnostic allocations from AI memory, need lookup,
  survival nameplates, utility breakdowns, and action predicate selection.
- Cached `CharacterNeedCatalog.All` so sorting and array creation occur only after registration
  changes instead of during every AI decision.
- Added allocation-free deprivation display queries for world nameplates.
- Added a stabilized PlayMode profile boundary that discards the warmup/forced-GC transition
  frames; this removed stale 17-second initialization samples from normal runtime statistics.
- Marked scheduler-only GC as unsupported on the current Mono runtime because
  `GC.GetAllocatedBytesForCurrentThread()` returns zero even after a known allocation.
- Final 100-character profile passed with all 100 BTs ticked, frame average/p95
  `2.77/3.42ms`, scheduler average/p95/max `0.370/0.497/0.632ms`, and no budget overflow.
- Editor-wide GC averaged `182KB/frame`; against the prior one-character Editor baseline of
  roughly `120KB/frame`, the 100-character stress-world increment is roughly `62KB/frame`.
- Broad V16/domain regressions passed. Pointer-driven UI verification passed `21/21` rows at
  desktop and portrait resolutions, including right-click alert dismissal.
- Final focused V16 integration and AI/survival regressions passed with Console
  `Error 0 / Warning 0`.

## 2026-07-26 Weighted navigation and 500-character closure

- Replaced equal-cost fixed-destination BFS use with cost-aware A*, while preserving weighted
  Dijkstra for reachability and multi-candidate work/facility selection.
- Added terrain/traversal versioning, cross-frame path caching, bounded urgent search
  overdraft, pooled workspaces, compact routes, and shallow-water movement costs.
- Reworked AI scheduling around a due-time heap, deferred BT materialization, immediate dirty
  wakeups, and presentation LOD.
- Reused decision contexts, cached facility candidate sources, and removed a repeated
  Editor fixture scene search exposed by fine-grained profiling.
- Focused Grid and 100-character regressions passed.
- The final 500-character, 600-frame staged PlayMode profile passed with 3.39 ms average,
  4.37 ms p95, 15.40 ms maximum, 0 frames over 16.67 ms, and scheduler p95 1.809 ms.
- Current route searches remain main-threaded because the measured 11.3 microsecond A* query
  is too small for profitable per-request Job dispatch. The architecture supports future
  immutable batch execution if map and request sizes grow.
