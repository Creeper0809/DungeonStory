# DungeonStory Current Findings

## 2026-07-26 V16 traversal-cache and wildlife timing findings

- `Grid.version` was serving two incompatible purposes: any content mutation invalidated structural path/room/facility caches. Moving wildlife and changing items therefore discarded otherwise valid routes.
- `Grid.StructuralVersion`/`TraversalVersion` now changes only for area, building, hallway, movement-blocking, or connection mutations. Full `version` still tracks every content change for consumers that need it.
- Wildlife hunt reachability previously depended on cached visitable-occupant positions. With dynamic occupancy excluded from traversal invalidation, the hunt query now checks the target actor's current Grid coordinate against the reachable-cell result.
- Wildlife arrival dwell mixed the caller-provided current time with a null-clock fallback of zero. Giving every actor a Unity game-clock fallback makes route start, route completion, threat interruption, and dwell expiry use one scaled time base.
- Focused Grid, Wildlife, and AI naturalness regressions pass after the changes.
- The 100-NPC EditMode stress scenario improved from roughly 353 seconds to 50.6 seconds. Broker path searches fell from 1,440 to 51 and budget deferrals from 16,461 to 50. Scheduler p95 is 0.73ms; the large max values are cold-path/test instrumentation spikes and require PlayMode profiling before final acceptance.

## 2026-07-21 Physical item and hauling implementation findings

- Current Editor Console baseline is clean through Unity MCP (`Error 0 / Warning 0`), but batchmode compilation cannot run while the interactive Unity Editor owns the project. MCP/Editor Console is the active compile source for this pass.
- `WarehouseInventory` currently lives inside `Buildings/SO/StockInfo.cs` and is consumed directly by stock delivery, shop restock, crafting, and expedition preparation. Physical items must be introduced as a runtime layer without mutating shared `BuildingSO` data.
- World info click selection currently checks characters before buildings and does not know item piles. Adding `GridLayer.Item` must not break `GridCell.GetBuilding()` callers that expect buildings even when another non-blocking occupant is on the same cell.
- Existing `AIActionSet.RequiresDestination=false` actions such as wait/look-around provide the right pattern for hauling: `AbilityHaul` should own pickup/dropoff pathing instead of forcing the generic AI destination contract to represent two legs.

- Final character-growth acceptance is green: combined EditMode regression, real-pointer P1/P2 (`18/18`), exclusive character/building selection, actual skill alert navigation, start-party generation, V3 save restore, and all three ultimate domains passed with Unity Console `Error 0 / Warning 0`.
- MCP `Camera_Capture` cannot render the live URP `Main Camera` directly in this editor/package combination, but a plain runtime camera copied from it renders the same transform and projection successfully. That capture is nonblank and provides independent world-only evidence alongside HUD `ScreenCapture` artifacts.
- Skill runtime audit found that `research`, `output`, `repair`, `stock`, `relationship`, and `revenue` modules only granted a generic mood factor. Their authored numeric variants now feed their actual subsystem paths, while management ultimates only join those contextual modifiers after their operating-day use limit has been marked.
- Defense automatic ultimates previously listened to the pre-target `InvasionStartedEvent`, so enemy-targeted effects had no intruder to affect. They now listen to `InvasionSpawnedEvent`, canonicalize actors by GameObject, and apply validated damage modules to the spawned intruder.
- Two inspection commands failed before code changes: one referenced the nonexistent `Assets/Scripts/FacilityShop/Shop.cs` instead of `Assets/Scripts/Buildings/Shop.cs`, and one PowerShell interpolation used `$i:` without braces. The follow-up reads used the resolved path and format operator.
- The actual skill-alert capture shows the prepared owner name as `유나 사장으로 시작`, confirming the duplicate-role notice fix. It also shows the event detail and `성장 탭 열기` command clearly above the world, while the character panel remains readable and no surfaces overlap incoherently.

## 2026-07-20 Confirmed character growth design

- The runtime character prefab contained both the empty legacy `Customer : CharacterActor` component and the canonical `CharacterActor`, so scene queries counted every spawned character twice. Start-party cleanup then destroyed the shared GameObject while trying to remove the apparent duplicate. Both character prefabs now keep only the canonical component, and start-party/save queries normalize actors by GameObject as a compatibility guard.
- The start-party pointer flow passed end to end with real LLM output, but the supposed mobile capture remained 1920x1080 because `Screen.SetResolution` does not resize this Editor Game View. Mobile bounds/capture evidence must use an Editor Game View size change or an equivalent actual render target before it can count as verified.
- Selecting fixed Game View sizes through `GameViewResolutionController` produces real 1600x900 and 900x1600 render targets. The portrait layout keeps all three member cards and the final action row in-bounds; no card or text overlaps were visible in the corrected capture.
- Growth-tab capture exposed a separate visible copy defect: authored owner names such as `슬라임 사장` were rendered as `슬라임 사장 사장으로 시작`. Owner selection now accepts the prepared identity name and avoids appending a duplicate role suffix.

- The legacy `CharacterProgression` model (`MaxLevel=20`, three equipped IDs, fixed unlock track, global `PowerMultiplier`) must be replaced rather than extended in parallel.
- Shared authored configuration belongs in one `CharacterSkillSystemSettingsSO` with managed-reference module rules. Character-specific skills, drafts, growth, ledgers, request state, and use limits belong to serializable runtime/save records.
- Normal active unlocks are at levels 1, 5, and 30; passives are automatic at level 1 and after level 25 plus narrative breadth; the narrative-derived ultimate arrives at level 50.
- Potential uses five display grades with 45/30/15/8/2 population weights and only modifies normal-active rarity rolls. A missed Rare-or-higher draft grants the next unlock a 1.5x upper-rarity weight modifier.
- Character preparation is a three-person roster (owner plus two same-species staff) with identity, aptitude, and skill reroll groups. World visitors require persistent individual profiles rather than respawning shared `CharacterSO` definitions.
- Save compatibility is deliberately broken for legacy progression data; save/load must preserve already-rolled rarity and candidates so reloading cannot reroll outcomes.
- `CharacterActor` already requires `CharacterProgression`, so the new per-character growth state can replace that component's legacy lists without adding another required prefab component.
- Combat applies the legacy level multiplier both in `CharacterActor.GetCombatPowerMultiplier` and `OffenseBattleFactory.CreatePlayerCombatant`; both applications must be removed so level growth comes only from allocated stat points.
- `CharacterStats.GetCharacterStat` is the narrow final-stat query used by battle and expedition power. It can compose identity profile stats with character-specific base-stat replacement, level growth, and conditional passive bonuses.
- Existing combat abilities are constructor-driven modules, so generated combat selections can be validated as string IDs and converted into `CharacterCombatAbilityDefinition` instances without storing polymorphic runtime effects in save data.
- The local LLM queue already supports prioritized request profiles and JSON mode. A dedicated skill profile can use the same queue while persistent retry keys and backoff live in the generation service.
- `OwnerSelectionPanel` is runtime-configured and pauses the simulation, making it the correct replacement surface for three-character preparation rather than introducing a disconnected scene-only mockup.
- Start preparation can remain instance-safe by generating skills on hidden `CharacterProgression` preview objects, then restoring their snapshots onto the real owner/staff actors only when the player confirms the party.
- Skill rerolls need request cancellation plus a per-growth revision because LLM callbacks can finish after identity or aptitude has changed. The generation service now ignores canceled requests and request keys include the revision.
- Existing customer `CharacterSO` assets can provide same-species staff visuals and authored species data without mutation: the spawned actor is converted to runtime `CharacterType.NPC`, receives `AbilityWork`, and owns its prepared growth snapshot.
- The first preparation PlayMode pass exposed bottom buttons parented directly under the modal surface; rebuilding member cards therefore left duplicate Start/Back controls. A dedicated preparation-action root fixes their lifecycle.
- Replacing base stats alone was insufficient: modifier queries still read `CharacterIdentity.Profile`, which is built from authored SO traits. The effective runtime profile must be rebuilt from the character's selected trait IDs and used by all modifier queries.
- Recruitment previously changed the live actor to NPC but never marked its persistent world profile as staff. Without promotion, a saved hired guest remained eligible for later visitor acquisition.

- Character progression must not be mutable state on `CharacterSO`; those assets are shared by every character using the same definition. Per-character level, XP, learned skills, and equipped slots need runtime/save ownership.

- The offense loop now has the missing expedition layer: preparation in the dungeon, route pressure, attrition, tactical formation combat, retreat, return, and reinvestment are separate decisions rather than one launch button followed by one boss battle.
- Ordinary battle victory no longer finalizes a target or heals the party. It returns to route choice with damage and stress intact; only the boss resolves the expedition and advances the campaign.
- The dungeon link is capability-based. Formal usable rooms and modular expedition-support abilities contribute preparation values, while supplies are withdrawn from and returned to the real warehouse inventory.
- A complete UI-event campaign passed all targets in order: `food_farm`, `merchant_road`, `old_armory`, `mana_ruins`, `rival_dungeon`, `truth_core`; final state was `truth=True` with six result records.
- Product-shell coverage separately proves pointer-driven owner selection, customer recruitment, map/composition, journey entry, first battle, and exact active-battle save/load without captured errors or warnings.
- Global button-text lookup is unsafe once event alerts repeat prior action labels. Offense verification now scopes clicks to the active map, expedition, or battle panel, matching the visible interaction surface.
- Immediate `CaptureScreenshotAsTexture` after a synchronous full campaign can capture before the next rendered frame. Scheduling `ScreenCapture.CaptureScreenshot` for the following frame produced valid visual evidence.

- The old offense lifecycle was the main reason it felt flat: launch opened one full encounter, every battle completion deleted the expedition, and victory fully healed survivors.
- Formation previously had no tactical effect. Source and target position constraints plus forward compaction are required before party order becomes a real decision.
- Dungeon/offense coupling now has an existing-compatible path: explicit `BuildingExpeditionSupportAbility` values override or extend role-based fallback contributions, so old content works before every asset is migrated.
- Warehouse inventory can safely back expedition supplies because aggregate availability, deterministic withdrawal, rollback, and return can be implemented without introducing a second resource ledger.

- The requested target is now a dungeon-linked multi-node expedition, not continued balancing of the existing one-target/one-battle flow.
- The existing offense lacks route decisions, supplies, formation constraints, persistent expedition attrition, camping, and room/facility recovery. These are product gaps, not presentation polish.
- The recently added campaign-order `+50%` stat multiplier closes a numerical test but works against the requested design; it must be removed as the real growth and preparation loop is introduced.
- The worktree is heavily dirty across scenes, prefabs, data assets, and gameplay scripts. Offense changes must remain tightly scoped and must not revert unrelated user changes.
- Stock is not a standalone subsystem folder. Runtime inventory is `WarehouseInventory` on warehouse buildables, with `SceneFacilityEvolutionWarehouseInventoryQuery` already providing aggregate query/withdraw/rollback patterns suitable for expedition supplies.
- Building functionality is already modular through `BuildingAbilityCollection`; expedition preparation/recovery should be added as ability modules and queried through capability interfaces instead of adding more fixed fields to `BuildingSO`.
- `OffenseExpeditionRuntime` currently owns the right lifecycle boundary but removes the active run on every `BattleCompleted`, fully heals victory survivors, grants target rewards immediately, and advances the world map. This method must become node-aware: ordinary victories return to the route, only boss victory finalizes the target, and survivors retain attrition.
- `OffenseSaveService` already pairs one active expedition with an exact battle snapshot. Its run payload can be extended in place with route, formation, stress, supplies, loot, and current-node fields while old saves default to a legacy boss-battle run.
- Offense panels are created through injected factories and `IOffensePanelService`; adding a dedicated route panel preserves the existing ownership pattern and avoids scene-authored UI dependencies.
- `OffenseExpeditionRun` can absorb the new journey state without changing its identity, target, or actor ownership. This lets battle, reward, campaign, and save systems migrate incrementally instead of creating a parallel offense runtime.

- Six campaign targets and truth-reveal victory already exist.
- `OffenseBattleSession`, inline combat abilities, six fixed encounters, direct command runtime, and a dedicated battle panel now exist.
- Product expedition start now creates a turn battle. Product UI, debug completion, reward probes, and PlayMode verification no longer resolve expeditions by timer or combat-power comparison.
- Staff identities now persist from run seed plus creation sequence, with owner fixed to `owner`.
- Save V2 captures the active battle and V1 active expeditions migrate to a first-turn battle.
- Title new-game now selects `DungeonDifficulty`; start/result persistence is explicit and still needs an end-to-end runtime audit.
- Runtime defense uses recurring `EndlessDefense` cycles, but two PlayMode verifiers still expected obsolete `FinalChallenge/TruthHunt`; those expectations were replaced.
- Scene runtimes are found through cached providers, so the turn engine can be a DI singleton without adding another scene-authored component.
- The standalone `OffenseBattlePanelFactory.cs` was visible to `AssetDatabase` but absent from Unity Bee's source list. Merging its factory/controller types into the already imported `OffenseBattlePanel.cs` resolved the actual Unity compile failure.
- Unity MCP is connected. The Editor is idle and the current Console count is `Error 0 / Warning 0`.
- `DungeonProductShellPlayModeVerifier` now drives difficulty selection, expedition target/start, guard, attack/target selection, dungeon switching, manual save/load, and exact battle-state comparison through pointer input.
- First current-build product-shell run reached PlayMode with no Console errors/warnings, but every synthetic Input System button click failed to invoke its callback. The report proves targets were active/interactable while Settings, Difficulty, and Owner state did not change. The later gameplay transition came from the verifier's direct duplicate-transition assertion, so this run is not product-path evidence.
- Captures confirm the failure boundary: the title capture is valid, while the alleged battle capture still shows the owner-selection modal.
- The queue-plus-`InputState.Change` fallback fixed current Editor pointer delivery. The next run passed title Settings, audio/accessibility tabs, Hard difficulty, owner selection, save/settings/title return, Continue, and load-failure handoff with `capturedErrors=0; capturedWarnings=0`.
- The remaining product-shell failure moved to offense navigation: `P1Action_OffenseTarget_0` had a screen center at y=0.71 and was active/interactable but clipped/covered by the bottom HUD. The verifier skipped the visible `월드맵 열기` and party-composition workflow, so its click did not select a target and no Start button appeared.
- The visible map flow now selects `food_farm` and opens composition correctly. The current capture shows `선택 인원 0/3` and `필요 인력 부족 0/1`: a clean new run has no eligible employee, so the verifier cannot start combat until it exercises or prepares the recruitment path.
- Recruitment currently marks only `RegularCustomerRecord.IsRecruited`. No listener or conversion changes the live actor from `CharacterType.Customer` to `CharacterType.NPC` or grants `AbilityWork`; therefore a normally recruited customer still fails offense eligibility (`NPC` + active `AbilityWork`). This is a real gameplay-loop gap, not just a verifier fixture issue.
- Recruitment also had no runtime component in `SampleScene`, and the scene spawner referenced only `TestCharacter` (`NPC`). The real customer asset (`Resources/SO/Character/New Character SO.asset`) was never included in the spawn list, so visits and recruitment could not occur naturally. The lifetime scope now owns the runtime, and the spawner merges customer catalog entries once after DI.
- Customer data IDs identify a customer definition, not a unique live visitor. Keeping only the data ID caused recruitment to convert an arbitrary matching actor. `RegularCustomerRecord.ActiveActor` now preserves the exact last visitor in memory, while restored records safely retain the scene-query fallback.
- Current product-shell report is `PRODUCT_SHELL PASS`. It proves recruitment, offense input, independent dungeon time, view switching, direct guard/attack targeting, and exact active-battle restore with `capturedErrors=0; capturedWarnings=0`.
- The room MCP capture originally lost its overlay between tool calls because normal hover polling cleared it on the next frame. Preparing the room and pausing PlayMode in the same editor command preserves the real renderer state; the capture then shows 4 active fill cells and 10 active outline segments on the intended sorting layers.
- `DungeonRunFlowPlayModeVerifier` now opens `SampleScene` before PlayMode, so it remains valid after introducing the separate title scene. It proves recurring day-10/day-20 bosses never grant Victory and only stage 6 `truth_core` does.
- The final independent domain pass reports `RoomSystem`, `RoomEnvironment`, `OffenseBattle`, `OffenseWorldMap`, and `OffenseReward` all successful with no console errors or warnings.
- The complete regression set is green: product shell, run flow, save UI, Unified UI, P1/P2 surfaces, character click priority, room inspection, and 29 implemented debug suites.
- A clean player-style run proved natural visits stopped at 15 because a visitor with no remaining visits was still forced through an optional look-around before exit. `AbilityShopping` now exits immediately when the visit cycle is complete, and its focused AI regression passes.
- The recruited prefab carries both legacy `Customer` and canonical `CharacterActor` components. Reference-only distinctness therefore rendered one employee twice and made the second lifecycle transition fail. Expedition discovery and launch now canonicalize actors by GameObject; the clean composition shows one row for one employee.
- World map and expedition composition canvases could stay open together. Because composition sorts above the map, a visible stage button could actually hit composition's Close button. `OffensePanelService` now hides the opposite panel before binding either surface.
- Stage 1 left the only employee at `13/120`, while no building ability or player action restores combat health. The campaign exposed a hard lock rather than attrition. Successful surviving members now receive full return treatment; retreat/failure health and permanent death remain consequential.
- The resource catalog contained only one customer definition even though campaign stages require two and then three non-owner members. Added natural Orc and Vampire customer definitions, bringing the recruitable pool to IDs `1,2,3` without bypassing visits or recruitment UI.
- Focused customer, staff, world-map, battle, and reward suites pass after these direct-play fixes.
- `SampleScene` had a serialized `RegularCustomerRuntime` while the lifetime scope also created one. Both listened to every visit, but UI recruitment and generic runtime lookup could observe different states. DI now reuses the scene runtime and only creates a fallback when a scene has none; a clean run reports exactly one instance.
- A second clean seed ran at X5 for almost two real minutes and settled at average satisfaction `62.0 / 65.1 / 71.7`, below the old recruit threshold `75`. Since the unmodified starter dungeon offers no clear way to correct that early random average, recruitment and therefore the campaign were seed-locked. The default/product candidate threshold is now `65`: three visits establish a regular and the fourth qualifying visit unlocks recruitment.
- The direct run won stages 1 and 2, then lost both selected employees at stage 3. Training facilities only add mood factors; they never improve combat stats, while the fixed encounter roster escalates far beyond the unchanged recruit stats.
- Required power was informational and the original encounter curve had no corresponding growth path. Campaign stages now grant a deterministic preparation multiplier derived from prior victories (`+50%` combat stats per completed stage, health unchanged). The map and composition UI expose the bonus and effective party power.
- A deterministic battle regression now runs the starter Orc/Vampire/Slime roster through all six encounters on Easy, Normal, and Hard and requires victory with every member alive. This is regression coverage only; final acceptance still requires the clean pointer-driven playthrough.

## 2026-07-20 Weak-link audit

- P0: World profiles use unique string IDs, but regular-customer, rumor, staff-discontent, and some evolution records still key characters by shared `CharacterSO.id`; distinct people using the same template can share visits, recruitment, trust, and complaints.
- P0: The six-target winning campaign previously ended with the party at levels 6/5/3, while the new curve requires 63,700 XP for level 50. Level-25/30/50 content is outside the current run loop.
- P0: Skill generation has no executable fallback. Start-party confirmation and the ready guest pool both wait on validated LLM results, so an unavailable local LLM can stop both new-game start and the customer economy.
- P0: Passive validation permits combat modules for DamageTaken/InvasionStarted/BattleCompleted, but the outside-combat executor implements only a subset. Valid generated passives can therefore have no mechanical effect.
- P1: Room quality has two definitions. UI/mood use `RoomEnvironmentSnapshot`, while AI utility uses the older area/door/furniture `RoomInstance.GetQualityScore`; a room can look excellent in the overlay without becoming equally desirable to AI.
- P1: Offense preparation checks usable rooms, then adds fixed bonuses per facility role/ability. It does not consume room environment scores, staffing quality, recent operation, or character mastery, so duplicate fixtures can replace good operation until caps are reached.
- P1: Generated actives are converted with source and target formation masks set to `Any`; authored species skills respect positions, but personalized skills bypass the Darkest-Dungeon-style formation layer.
- P1: World profiles persist growth and narrative, but actor binding/release does not persist social memory, mood history, or the profile's relationship score.
- P2: Unlimited full reroll also rerolls potential and restores all partial reroll charges, making rare potential primarily a patience check instead of a durable roster tradeoff.
- P2: Growth UI shows only total allocated points and skill descriptions. Per-stat growth causes and stored narrative reasons disappear after the acquisition alert, weakening player understanding of history-shaped growth.

## 2026-07-20 Closed-loop integration findings

- Building info craft buttons were visible but ignored when clicked in the first fade-in frame because the parent `CanvasGroup.interactable` stayed false until the 0.1s tween completed. The panel now enables interaction immediately, leaving the tween as visual-only feedback.
- JsonUtility can deserialize a null nested class field as a blank object; `DungeonOffenseSaveData.activeBattle` therefore came back non-null with empty IDs. Offense restore now treats blank battle IDs as no saved battle, and capture only persists active battles that match an active `InBattle` expedition.
- Equipment crafting PlayMode now proves the full non-scenario UI path: queried warehouse material is visible to the runtime, the building craft button creates a queue order, materials are withdrawn, `Craft` work completes the order, and the expedition loadout reserves the crafted item.
- The character growth tab previously displayed only total growth, so players could not tell whether a stat came from base rolls, species/traits, level growth, equipment, or a conditional passive. `CharacterProgression` now exposes the breakdown directly and the UI consumes that source.
- Runtime-generated UI can survive script changes inside the dirty scene. The first stat-breakdown test passed by text value while the screenshot still showed the old 30px summary region; `CharacterSummaryRuntimeLogFactory` now rebuilds the generated view when the expected `GrowthList` structure is missing.
- The first no-injection direct-play verifier did not fail because offense launch was too strict; it failed because the real AI never consumed recovery facilities after a stage-1 victory. Prior coverage only checked `AIRest.CanStart` and `FacilityCandidateScorer.GetNeedScore`, so `AIAction.CalculateScore` could still return 0 and prevent selection.
- The concrete recovery scoring gap was that `Rest.asset` still referenced the old `Sleep` stat consideration instead of `NeedRest`, and `ConsiderationFacilityNeed` rejected staff self-care when `AbilityShopping.visitCount` was 0. On-duty Hygiene also rejected workers despite stress recovery being part of the hygiene need score.
- Unity can keep running the previous assembly when a compile error exists elsewhere. The missing `restResolved` diagnostics were not an MCP truncation problem; `NaturalRunRuntimeDebugProbe` still called the removed `IGameDataProvider.GetGameData()` API, so StaffDuty was executing stale code.
- `CharacterAiActionCandidate.Action.destination` is intentionally null during scoring. Destination proof belongs at `AIActionSet.TryResolveDestinationWithFailure` or after `AIBrain.TryCommitActionCandidate`; tests that read candidate destination before commit can produce false failures even though the AI can select and use the facility.
- The next no-injection direct-play failure was within the expedition itself: a two-person stage-1 party could reach the boss after an elite node but lacked enough remaining health to finish it. The verifier now behaves like an actually cautious player by filling the three-person party, carrying available supplies, using medicine before routing, and visiting camps when attrition is visible.
- Debug and focused scenarios can leave event-listener MonoBehaviours alive without DI-owned UI/recorder dependencies. Throwing from those listeners pollutes the Console even when the domain scenario itself passes. Event-alert and facility-evolution listeners now keep runtime state but skip only their dependent side effects until injection exists.
- The direct run proved the old default recruitment cadence was too slow for the current 3-person stage-1 offense requirement: by Day 10 a customer had 3 visits and high satisfaction, but no 4th visit arrived during the recruitment wait. Default recruitment now promotes a satisfied 3-visit regular to a recruit candidate; custom rule tests still cover the stricter 4-visit path.
- Recruitment is now explicitly template-safe rather than template-consuming. `CharacterSO` remains a shared species/source template, while `RegularCustomerState` and the promoted world profile own the persistent recruited person. Tests should assert the persistent ID is recruited, not that the shared template can never spawn another person.
- Unity scene serialization can override constructor/default rule changes. `SampleScene` carried the old `RegularCustomerRuntime.rules.recruitCandidateVisitThreshold=4`, so the direct run still behaved like the old cadence until the scene value was changed to 3.

## 2026-07-21 Feature verification findings

- Unity can keep QA request files alive while compilation is broken, so a shell-side "waiting for report" loop is not evidence that a verifier is actually running. Compile errors must be cleared before accepting any PlayMode result.
- ProductShell and CharacterClick both exposed stale Input System mouse devices after scene/UI transitions. The reliable verifier pattern is to use a dedicated verification mouse, make it current, apply `InputState.Change`, queue the state event, and recreate the device if the position still does not move.
- RoomInspection was still closing only the legacy owner-selection flow. The current start-party preparation panel must be completed as part of any gameplay-scene verifier before testing top-right HUD toggles.
- Expedition equipment UI searches must be scoped to the active expedition panel. A global "contains Iron Edge" button search can hit the building crafting button instead of the expedition equip button after crafting leaves the building info surface open.
- Feature batch verification now runs without MCP by writing request markers and letting the Editor open `SampleScene`, enter PlayMode, attach each runner, and aggregate the report. MCP Camera_Capture evidence is still separate and cannot be substituted by this batch.
## 2026-07-20 Character progression audit

- `CharacterSO` and `CharacterRuntimeProfile` currently describe shared authored identity, species, traits, base stats, and derived multipliers. They do not own mutable per-character growth.
- `CharacterActor` requires split runtime components (`CharacterIdentity`, `CharacterStats`, lifecycle, log, etc.) but has no progression component yet.
- Per-character level, XP, learned skill IDs, and equipped skill IDs should therefore live in a new actor-owned runtime component and be serialized by the character world save service.
- Existing offense ability definitions should be reused as the shared skill catalog so progression does not create a second combat-skill system.
- `CharacterCombatAbilityCatalog.GetAbilities` currently grants every species/trait ability directly to the battle combatant. This is the single integration point to filter by a per-character equipped loadout.
- Deterministic XP award points are `BuildingTrainingAbility.ApplyUseCompleted`, successful completion in `WorkTaskExecutor`, and `OffenseExpeditionSystem.OnBattleCompleted`; each fires once per completed activity/result.
- The generated character summary already owns Status, Mood, and Records tabs. A fourth Growth tab can expose level/XP and learned/equipped skills without adding another popup.
- Existing species assets do not currently serialize authored combat ability collections, so the catalog fallback abilities are the effective species skills. Shared unlockable techniques are needed for meaningful level milestones beyond the single innate skill.
- `OffenseExpeditionService.CalculateMemberPower` originally read the base stats multiplier directly; it now goes through `CharacterActor.GetCombatPowerMultiplier` so composition power and actual battle scaling agree on level growth.
- The full-game save debug scenario had retained a legacy single-battle expectation. It now verifies the current route-choice state, and orphan battle snapshots no longer discard an otherwise valid saved journey; both focused progression and full-game round trips pass.

## 2026-07-21 Physical item and hauling findings

- `RegisterComponentOnNewGameObject<ItemPileInfoPanel>` only created the panel when something resolved it, so item pile click events had no listener until the lifetime-scope build callback explicitly resolved the singleton.
- The item marker fallback sprite used a tiny white texture with too high a pixels-per-unit value, making default stock markers effectively invisible in SceneView capture. Lowering fallback PPU gives a visible one-cell marker.
- Delivery/reward physicalization can be introduced safely before removing legacy warehouse deposits by making `WorldItemStackRuntime.TrySpawnStockDelivery` the first path and preserving direct deposit only when the physical runtime is unavailable.
- Shop restock already used a character movement route, while the older instant `RestockFrom` API remains for legacy/debug callers. Gameplay work execution should continue using the physical route.
- Purchase and shoplifting previously only changed money/events and shop stock. Adding carried items at purchase/theft time creates the downstream hook for exit, confiscation, recovery, and theft consequences.
- Restored hauling multipliers must not permanently override runtime option changes; the settings provider now reads the current user setting first.
- The first actual `AIHaul` PlayMode pass found that `AbilityHaul.StartHauling()` reserved a stack, then immediately called `StopHauling("restart")`, releasing and clearing that same job. The worker walked toward the default `(0,0)` route instead of the selected item; reserving after stopping fixes the runtime, not just the verifier.
- A second logistics pass showed loose items could be carried toward a far existing warehouse because warehouse selection used scene-query order. `WorldItemStackRuntime.TryFindWarehouseForStack` now chooses the nearest reachable delivery cell, which also prevents long, confusing haul routes in normal play.
- The physical logistics verifier now proves the full no-injection movement path: loose stack to warehouse, warehouse stock to facility buffer, craft input buffer consumption, crafted equipment output stack to equipment inventory, expedition packed stacks, and carried-weight UI all pass with Console `Error 0 / Warning 0`.
- The item-pile PlayMode verifier still carried a legacy owner-option click path, so the current start-party preparation UI could leave owner selection active and make pile UX verification fail before item interaction. Reusing `StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug()` fixes the verifier entry without changing gameplay item logic.
- File-request PlayMode runners are useful when MCP loses approval during PlayMode/domain reloads. The pile verifier now mirrors the logistics verifier's request-file pattern, which lets the Editor run the test from EditMode even when direct MCP command execution is flaky.
- The remaining warehouse link was conceptual: stock could be deposited into the aggregate `WarehouseInventory` while the player could not inspect a corresponding stored physical stack. Stored warehouse stacks now mirror aggregate stock and are hidden unless the `물품` view toggle is on, keeping ordinary play uncluttered while preserving physical inspection.
- Stored physical stacks must be the restore authority when present. V5 restore now makes warehouse aggregate stock follow stored stack totals, so save/load cannot silently resurrect an old direct-inventory value after items have entered the physical hauling system.
- `CharacterWorldSaveService` already captured and restored `CharacterCarryInventory`; the contract test now verifies that carried items also survive the full `DungeonGameSaveData` JSON boundary.
- Direct-play recruitment exposed a profile/actor authority gap: recruited visitors kept a customer `CharacterSO` template while their world profile had become staff. If later code reset only `CharacterIdentity.CharacterType`, the actor could be released like a visitor and disappear from expedition candidates. `CharacterPopulationService` now treats `WorldCharacterProfile.isStaff` as authoritative and `CharacterSpawner.Interact` refuses to return staff profiles to the visitor pool.
- Offense reward regressions had a stale stock expectation after physicalization. Rewards may now appear as loose dropoff stacks instead of immediate warehouse stock, so tests measure warehouse delta plus physical-stack delta. The recruit-candidate reward handler also intentionally grants at least two candidates.
- `CharacterCarryInventory` could be duplicated in editor/runtime fixtures, splitting carried items between two components and making theft/hauling assertions read the empty one. The carry inventory is now `DisallowMultipleComponent`, and item tests resolve it through `CharacterCarryInventory.Ensure`.

## 2026-07-22 Direct-play completion findings

- The final `truth_core` failure was not a hidden compile or MCP issue; it was a bad natural-play decision path. The verifier accepted "three legally deployable members" even when the final party was not the trained Lv50 core.
- Camp routing had an off-by-one supply assumption. Entering any node consumes one ration, so choosing a camp with exactly two rations left means there is no longer enough food to actually rest at the camp.
- Generated/offense skill verification must treat control and setup modules as real combat actions. Looking only for `OffenseDamageEffect` made valid vulnerability, delay, multi-target, and conditional-amplify skills invisible to the direct-play battle driver.
- Late-stage direct-play proof needs a stronger gate than `CanJoinExpedition`. The product rule correctly blocks only below 25% health or 80 stress, but a player-style final boss run should wait for a healthy trained party and spend camp/medicine before engaging.
- The previous 900x1600 HUD width gap is fixed. Upper-right controls are clamped to the canvas width, top/bottom tab buttons no longer retain template widths, and `Temp/resolution-matrix-report.txt` now verifies 900x1600 gameplay bounds with `RESULT=PASS; failures=0`.

## 2026-07-22 Work amount and construction findings

- GameplayScene PlayMode verifiers must first complete the current start-party fallback before testing world input. Otherwise the owner-selection overlay can remain visible while tests interact with UI behind it, producing false world-click failures.
- `Grid.CountBuilding(BuildingSO)` counts any occupant with the same `GridId`, including a construction site using the target building ID. Tests that need to prove "not instantly built" should inspect the target building layer at the footprint, not the aggregate count.
- Work target scoring needs an explicit facility-role fit term. Without it, a generally supported work type such as Guard can compete on unrelated facilities and flatten species/work specialization. Guard now favors Training/Security facilities, while Research favors Research facilities.
## 2026-07-23 Nameplate, wildlife motion, and zoom findings

- `WorldCharacterNameplate` inherited the actor sprite sorting layer and added only `+36` order. Dungeon walls, floors, and furniture can use later sorting layers or much higher orders, so the text can be visibly covered even while the character sprite itself remains readable.
- `CameraManager` already reads unscaled wheel and keyboard zoom input, but `blockWheelZoomOverUi` calls the broad `IUiPointerBlocker.IsPointerOverUi()`. Full-screen HUD graphics count as UI hits, which suppresses wheel zoom over most or all of the visible world.
- Wildlife ecology produces meaningful intents, but `ChooseReachablePosition` samples only `origin.x +/- distance`, picks an almost deterministic best score, and immediately becomes eligible for another decision after a route finishes. On a side-view exterior surface this presents as repeated left/right pacing.
- Natural wildlife motion needs to preserve the one-dimensional walkable surface while varying cadence and intent: weighted target choice, direction momentum, arrival dwell, sprite facing, and eased/bobbed locomotion are the appropriate fixes; allowing arbitrary vertical cells would reintroduce air/wall traversal bugs.
- The gameplay camera uses the URP `UnityEngine.Rendering.Universal.PixelPerfectCamera`, not the legacy `UnityEngine.U2D` component. Zoom must enter the URP component's Cinemachine-compatible mode before assigning a continuous orthographic size, or `OnPreCull` restores the baseline every frame.
- Runtime sampling after the wildlife changes showed intent-driven cadence rather than synchronized pacing: most animals remained at forage/drink/rest targets while only one moved at a time, and individual positions changed by only zero to two cells over the next sample window.

## 2026-07-23 Customer checkout patience audit

- Staffed shops already keep customers in `WaitForServingWorker` and raise `Operate` urgency, but the wait has no timeout or personality stage. A shop without a worker therefore holds every customer forever.
- `CharacterAiPersonality.patience` and trait/species `waitPatienceMultiplier` exist, but no runtime checkout path consumes the latter.
- `AbilityShopping` always counts a finished interaction as a completed visit. An abandoned checkout needs a separate outcome so the customer avoids that shop without consuming the remaining visit, allowing Utility AI to choose an alternative.
- The action-phase/nameplate path already exposes current AI phases. Checkout feedback can remain lightweight by updating that phase and using one-shot event alerts instead of adding a new always-visible panel.
- A queue reaction must not consume the customer's remaining visit. Marking only the abandoned facility as visited lets the existing shopping Utility AI select another reachable shop and naturally fall back to looking around or exiting when none remain.
- Runtime verification showed the staged path is deterministic at accelerated game speed: an impatient customer reached abandonment, released the queue, lost mood, retained a negative personal facility memory, emitted both service and abandonment alerts, and exposed `구매 포기` as the visible action phase.
# 2026-07-23 Paused stair and low-needs AI audit

- `CharacterVisual.HideForTraversal` uses `Time.realtimeSinceStartup`, `WaitForSecondsRealtime`, and an unscaled expiry check. Therefore the stair coroutine correctly pauses on scaled waits, but its visibility fail-safe restores the actor while the game remains paused.
- `CharacterAiDecisionContext.Capture` chooses its strongest need from every registered need. `FUN` can therefore drive `EmergencyScore`, while `BuildEmergencyJobGivers` has no fun response and falls through to work/wait.
- Emergency job construction currently adds only the single strongest need action. If that facility/action is unavailable, another simultaneously critical survival need is not tried before wait.
- `AIBrain.UseOwnerWorkActions` omits Eat, Rest, Toilet, and Hygiene. Owners with depleted needs have no self-care action to select.
- `AIEat.CanStart` rejects every on-duty worker. Hunger is not part of `WorkDutyController.ShouldTakeOffDuty`, so a starving on-duty worker can remain unable to eat indefinitely.
- Stair traversal itself already uses scaled `WaitForSeconds` and scaled movement. The only pause leak was `CharacterVisual`'s realtime fail-safe, so the correct fix is to scale the fail-safe rather than alter stair timing or DOTween globally.
- Low hunger should interrupt current work without switching the worker off duty. Sending hunger through the off-duty state breaks return-to-work semantics and makes ordinary meals look like schedule changes.
- Emergency selection must retain every urgent survival candidate in weighted order. Choosing only one need makes a missing facility turn a solvable hunger/rest/hygiene combination into a generic wait.
- The focused naturalness regression suite passes after excluding leisure from emergency selection, adding survival fallbacks, and exposing owner self-care actions. Two broader `StaffDutyDebugScenarios` cases around emergency-priority and expedition-return wake-up remain separate pre-existing failures and were not counted as proof for this fix.

# 2026-07-23 Stationary AI fallback audit

- The reported actor is not movement-locked. Its debug panel shows `Emergency -> WaitJobGiver`, no target, and every self-care action rejected by `CanStart`, so the pipeline intentionally commits a stationary wait.
- A wait action is currently allowed to be the terminal fallback even when the actor is healthy enough to move. Repeating that fallback makes a living actor look frozen despite the BT and scheduler continuing to tick.
- Low mood already has a mood-impulse model, but several impulse types map back to `Wait`; without a guaranteed locomotion/micro-action fallback, bad mood can still present as standing in place.
- `AIWait` does request a moving idle behavior, but high recent movement pressure selects `InspectFacilityIdleBehavior`, whose implementation is only `StartWait`. The unresolved emergency then selects the same branch again.
- Queue waiting is the only stationary wait that should remain intentionally static. Ambient inspection, weather shelter, complaint, and unresolved-need fallbacks need a bounded pause followed by movement or a fresh decision.
- Routine/job-giver mood bias alone was insufficient because the final routine-group multiplier could make ordinary work win again. Low-mood autonomy therefore has to be enforced once more at the final cross-group candidate comparison.
- The fixed idle path keeps deliberate queue/chat waits bounded, converts inspection and generic wait into reachable roaming, and ends a failed no-path wait quickly so the next decision can retry instead of holding an infinite action.
- A real GameplayScene probe with no LLM mood impulse held the actor at mood 17-20, selected `RoutineUtility -> Wait -> 기분 내키는 대로 배회`, and visited six distinct grid cells during the observation window.

# 2026-07-23 Dark survival V11 findings

- Runtime-only filth work targets cannot be resolved through the static `BuildingSO` catalog. Their information panel must format the runtime `WorldFilthWorkTarget` directly.
- Runtime work targets created outside prefab injection must receive `ConstructBuildableObject(...)` before initialization so grid and work services are available deterministically.
- Water Tilemap world X is mirrored from logical grid X in this project; verification must query the runtime grid-to-tile conversion rather than assume identical coordinates.
- `Tilemap.GetUsedTilesCount()` reports distinct tile assets, not occupied cells. Visual verification therefore counts source-cell tiles explicitly.
- Filth priority is most reliable as target-owned runtime state: it raises Clean urgency and wakes eligible workers without mutating shared SO data.
- Desperate drinking needs two distinct contracts: stored/clean facility water wins first, while disabling those sources must expose the external unsafe-water fallback and its infection cost.
- A permanent social-memory entry uses `validUntil = 0`; restore must retain zero-duration snapshots because zero means indefinite, not already expired.
- Mood modifies breakdown probability, while `selfCare` and `patience` provide a bounded personality adjustment. Even the most stable personality cannot suppress a forced 100-burden breakdown.

# 2026-07-23 Exterior habitat decoration findings

- The ecosystem already consumed `Grass` and `Brush` resources, but had no world visual bound to those values; the missing link was presentation, not another food simulation.
- The authored flower PNGs are imported as six full cluster sprites. Layering several clusters at stable offsets produces a readable dense patch and lets resource thresholds remove whole clusters without modifying source art.
- Trees and rocks must remain nonblocking visual decoration. They use `OutsideObject`, while wildlife remains on `Default`, so actors pass in front without changing pathfinding or grid occupancy.
- `Grass` and `Brush` intent filtering is required when habitat radii overlap; a foraging animal now consumes only forage patches and a drinking animal only water patches.
- Pure EditMode ecosystem contracts must not create scene decoration roots. Automatic decoration creation is PlayMode-only, while the focused visual contract explicitly creates and disposes its runtime.

# 2026-07-23 Exterior pond visibility findings

- The original default water selector admitted DropZone cells and picked the lowest X cells. That put two tiny sources at Grid `(0,0)` and `(1,0)`, visually buried beside the entrance instead of reading as an exterior water feature.
- A runtime Tile uses `TileFlags.LockColor` by default, so `Tilemap.SetColor` did not tint the generated white/gray sprite. Setting `TileFlags.None` is required for clean/unsafe/foul source colors to appear.
- The logical grid world position is the floor of a three-world-unit cell, while a Tilemap sprite is centered in that cell. A half-height water sprite therefore needs a `0.25 - CellWorldHeight/2` local Y offset to sit on the ground.
- The longest exterior surface run is Grid X `31..59`. Placing shallow water at `56..58` and deep water only at the outer boundary `59` keeps the pond reachable without partitioning the exterior route.

# 2026-07-23 Zoom sky and centered dungeon findings

- The solid sky previously followed only the physical world width. At maximum zoom-out the camera viewed Y `-6..15`, while the sky covered only about `-0.29..14.29`, exposing the camera clear color at the frame edges.
- Background coverage must be the union of padded physical-world bounds and the current orthographic viewport. Recomputing on camera position, size, or aspect changes keeps every zoom level covered without stretching decorative foreground sprites.
- The 27-column dungeon interior was authored at Grid X `4..30` inside a 60-column world. A centered start is X `17`, so both area tags and all 93 authored placements require the same `+13` translation.
- After centering, the entrance resolves to `(17,0)`, the drop zone to X `14..16`, and the interior to X `17..43`. The camera world X is `-29.5`, exactly matching the dungeon interior center.
- Runtime wall-tile inspection confirms outer-wall tiles at both centered boundaries X `17` and X `43`; the maximum zoom-out camera capture shows both edges in frame.

# 2026-07-23 Entrance outer-wall gap finding

- The entrance door correctly occupied Grid X `14..16`, but three invisible `ExteriorZoneMarker` instances shared X `13` through fixture/overlay layers.
- `GridWallTileCalculator` used `GridCell.HasOccupant()` for automatic side walls, so those nonstructural markers made X `13` look like a building and pushed the visible wall outward to X `12`.
- Automatic side-wall topology now reads only `Building` and `Hallway` structural content. Dynamic actors, items, wildlife, filth, construction overlays, and exterior markers no longer move outer walls.
- Fresh PlayMode verification reports rendered wall `X12=false`, `X13=true`; the entrance arch and wall are visually adjacent.

# 2026-07-23 Exact world click finding

- `WorldInfoClickSelectionService` first used exact `Physics2D.OverlapPointAll` hits, but then fell back to `GridCell.GetBuilding()` whenever no collider was hit.
- `GridCell.GetBuilding()` intentionally searches every occupant layer and ranks hallway last, so a bare floor click still returned the cell's `Hallway` object and opened corridor information.
- Ordinary facilities already own runtime colliders. Their selection now requires the pointer world point to overlap that collider; sharing a grid cell or being nearby is not sufficient.
- Structural wall and interior-door visuals are tile-based and do not always own colliders, so they retain a strictly same-cell `GridLayer.Building` fallback. Hallways, dungeon doors, and normal facilities are excluded from that fallback.
- The actual pointer regression clicked a facility collider and opened only that facility, clicked a collider-free hallway cell `(28, 0)` and opened nothing, then verified character-over-building priority. The report finished with zero failures, errors, or warnings.

# 2026-07-23 Consecutive wildlife click finding

- `WildlifeInfoPanel.OnTriggerEvent` assigned `current = wildlife` before calling `popupService.CloseAll()`.
- On the first click the panel was not yet in the popup stack, so the assignment survived. On a consecutive click, `CloseAll()` closed the already-open wildlife panel and `OnClose()` reset `current` to null after the new assignment.
- Clicking a building between wildlife clicks removed the wildlife panel from the popup stack, which explains why the next wildlife click appeared to work again.
- The panel now closes the prior popup stack first and assigns the clicked wildlife afterward. Repeated clicks therefore refresh and retain the same target instead of clearing it.
- The Input System regression performs two consecutive clicks at the same wildlife collider and verifies `CurrentWildlife` and the visible panel after both clicks.

# 2026-07-23 Wildlife horizontal-facing finding

- Wildlife facing used `step.To.x - step.From.x`, but this project's `Grid.GetWorldPos` maps logical X with `origin.x - gridX`; increasing Grid X therefore moves left on screen.
- The source animal sheets face right, so rightward world movement must keep `flipX=false` and leftward world movement must set `flipX=true`.
- Facing now uses the actual world-space X delta between movement-step endpoints, which remains correct if the Grid origin or coordinate mapping changes again.
- The focused natural-motion contract and live GameplayScene checks pass in both directions for every currently spawned species.

# 2026-07-23 Defense interception audit

- Invasion intruders currently run an independent movement coroutine and never enter an engaged state. Defense facilities can delay them, but guards do not stop that coroutine.
- `SuppressPriorityTarget` moves the guard onto the intruder's exact cell and applies one-way damage every `0.55s`; the intruder neither retaliates nor stops advancing.
- Guard work currently behaves like ordinary facility work. `InvasionSpawnedEvent` has no runtime listener that assigns on-duty Guard workers to an intruder.
- Character zero health immediately triggers death and despawn, so retreat and replacement policy directly controls guard survival.
- The existing boss-only owner rally chooses a shared hallway target rather than an Administration room. It must be replaced by evacuation for every invasion.
- The defense feature panel already owns threat, intruder, facility, and report sections, making it the correct home for policy editing and live engagement status. Several strings in that section are mojibake and need replacement while it is changed.
- The current top-level save version is V11 and invasion state already has a dedicated snapshot, so V12 can extend that boundary without mixing policy state into character or shared SO assets.

# 2026-07-23 Defense interception completion findings

- The live intruder coroutine must consult the engagement runtime before every Grid step. Merely pausing facility damage is insufficient because a previously started movement path can otherwise cross the frontline.
- Combat presentation must animate only the actor's visual child. Moving the actor root for a lunge corrupts logical Grid occupancy and can let the intruder or guard appear to cross the line.
- Policy switching keeps the same engagement and intruder reservation while swapping lead and reserve positions. Verification must follow the intruder runtime rather than assume a replacement engagement ID.
- Owner final defense can be planned while the intruder is still several cells away. `InterceptPlanned` is expected until the intruder reaches the reserved stop cell; only then does reciprocal combat begin.
- A real PlayMode run held the intruder at `(1,0)` against a lead at `(2,0)` for at least three exchanges, with reciprocal damage and no additional facility damage. Policy switching changed the lead without moving the intruder.
- The owner reached fallback evacuation cell `(41,2)`. After the non-owner frontline collapsed, final combat held the intruder at `(40,2)` against the owner at `(41,2)` for 20 exchanges with no reserve.
- Unity 6000.3.8 emits one editor-startup `The referenced script (Unknown) on this Behaviour is missing!` warning despite project-wide loaded-scene, prefab, ScriptableObject, animator, renderer-feature, and volume-profile scans finding no missing project scripts. Unity issue UUM-133323 lists the fix in 6000.3.12f1. After clearing that startup-only engine warning, the complete defense probe produced `Error 0 / Warning 0`.

# 2026-07-23 Developer mode findings

- Commands remain maintainable when each provider declares category, exact target contract, mutation status, and execution; the registry validates 112 unique IDs.
- Exact targeting uses pointer colliders for actors/items/facilities and only the resolved cursor cell for GridCell commands. There is no nearest-target fallback.
- Pure overlays do not mark a run modified. Stateful commands do, while palette visibility, targeting, cheats, and overlays reset when developer mode is disabled or a save is loaded.
- The palette remains non-modal at `1600x900` and becomes a bounded scrollable bottom sheet at `900x1600`.
- Camera Capture comparison confirmed the Grid overlay appears only while enabled and leaves no lines, labels, or pooled renderer residue after disable.

# 2026-07-23 Construction material delivery audit

- `WorkOrderRuntime.TryCreateConstructionOrder` immediately requests every missing material through `WorldItemStackRuntime.TryRequestFacilityDelivery`.
- Construction readiness correctly consumes only `FacilityBuffer` stock at the construction destination, so the expected final step is a worker deposit.
- The suspicious path is the delivery-request implementation: it may be representing demand by creating a visible `Loose` stack at the construction cell instead of reserving physical stock at its warehouse/source location.
- Correct behavior is source-preserving: order creation creates demand/reservations, pickup removes quantity from the warehouse stack, and only worker deposit creates the construction-site buffer.
- `TryRequestFacilityDelivery` delegates part of its work to a dedicated `RequestLooseStockDelivery` method. The construction bug is therefore localized around request-time stock conversion and the haul-plan candidate rules.
- Root cause confirmed: `TryRequestFacilityDelivery` calls `warehouse.Inventory.Withdraw(...)` immediately, removes the physical `Stored` quantity, then respawns it at the warehouse cell as a destination-tagged visible `Loose` stack.
- The stack is not teleported to the construction cell, but it is incorrectly dropped onto the warehouse floor before any worker pickup. That is the yellow pile visible immediately after placement.
- The fix must let destination-tagged warehouse stock remain `Stored` and hidden, make it haulable only as outbound reserved stock, and withdraw aggregate warehouse inventory only when the worker actually picks it up.
- Both pickup APIs currently only decrement the selected world stack and add it to `CharacterCarryInventory`; they do not touch `WarehouseInventory`. This confirms the aggregate withdrawal was intentionally front-loaded and must move into pickup for outbound stored stacks.
- Facility deposit already has the correct endpoint: carried items become `FacilityBuffer` only after the worker reaches the destination.
- The haul planner already understands destination-tagged stacks and routes them to `FacilityBuffer`, including multi-pickup plans and partial carry quantities.
- A focused extension is sufficient: allow only destination-tagged `Stored` stacks as outbound haul candidates, then perform the matching warehouse aggregate withdrawal atomically during pickup.
- Stored-stack save restoration currently rebuilds each warehouse aggregate from `destinationId` values prefixed with `warehouse:`. Overwriting that field with a construction destination would lose warehouse ownership on reload.
- Outbound stock therefore needs separate source-storage metadata. Cancellation must clear the delivery destination and merge the reserved quantity back into normal stored stock rather than deleting it.
- `DungeonPhysicalItemSaveData` currently enforces nested version 1 exactly. Source-storage ownership can be added as an optional serialized field while retaining version 1 compatibility; older V12 saves deserialize the new field as empty.
- `WarehouseInventory` exposes bounded `Withdraw` and `Deposit` operations but no reservation model. Reservation should remain physical-stack metadata, with pickup performing `Withdraw` and rolling back via `Deposit` if carry insertion fails.
- Existing item regressions explicitly expect warehouse stock to drop at request time, so they currently preserve the defect. They must assert stock remains unchanged and no visible loose stack appears until pickup, then assert the aggregate drops at pickup.
- `BuildPlacementUxPlayModeVerifier` bypasses hauling by spawning a `FacilityBuffer` directly at the site. It cannot be final proof for this fix and needs either a real-haul path or a separate focused pointer/play verifier.
- The first live haul exposed a second root cause: warehouse storage IDs use `BuildableObject.GridId`, which is the shared building-definition ID. Two warehouses of the same type both became `warehouse:1050`.
- The worker reached the reserved stack's physical warehouse, but pickup resolved the other same-type warehouse by the colliding ID and could not withdraw its stock. Warehouse ownership must use a per-building persistent/runtime instance key.
- Warehouse keys now use `building definition ID + center grid position`, which is stable across saves and unique for same-type warehouses. Legacy two-part IDs are normalized by matching the saved stack position during restore.

## 2026-07-23 Medieval dark fantasy combat V13

- The defense and offense loops previously owned separate damage assumptions. Both now route attacks through `ICombatResolutionService`, with adapters supplying real Grid distance/LOS or formation distance/cover.
- The active equipment instance, not a character template, is the authoritative source for range profiles, attack verb, quality, loaded ammunition, fire modes, recoverable throws, armor layers, shield state, and durability.
- Wildlife hunting still used a bespoke random hit roll after defense and offense had moved to the shared core. It now uses the same line-of-sight, friendly-fire, cover, range, evasion, body-part, and presentation rules.
- Wildlife uses a deliberately smaller body profile: head, torso, and combined limbs. Limb damage lowers mobility/evasion; vital-part destruction kills; the profile is persisted in wildlife save data.
- Ranged hunters now seek a valid firing cell instead of always pathing adjacent, refuse unsafe friendly-fire lines, reload from their physical carry inventory over scaled game time, and stop cleanly when ammo or a firing position is unavailable.
- A PlayMode command probe exposed a manual-move lock leak: owner evacuation could cancel the movement coroutine without clearing `AIBrain.manualCommandActive`. `AbilityMove.CancelActiveMovement` now completes cancelled manual commands and releases the lock.
- Live defense verification retained the intended phase order: 12-second external rally with guards waiting, dispatch only after breach, then four held reciprocal exchanges on adjacent cells.
- Unity Console finished at `Error 0 / Warning 0`. The MCP camera preview renderer failed twice for the live camera, while Unity's direct Game View screenshot succeeded.
## 2026-07-23 Construction material delivery

- The yellow pile was not a harmless preview. `TryRequestFacilityDelivery` withdrew aggregate warehouse stock and respawned it as a visible `Loose` stack at the warehouse cell as soon as the construction order was created.
- Construction readiness already consumed only `FacilityBuffer` stock, so the defect was isolated to the request/pickup boundary.
- Warehouse building-definition IDs are shared by every instance. Using only `GridId` as storage identity caused two same-type warehouses to collide; storage IDs now include the warehouse grid position.
- The correct three-stage ownership model is now explicit: ordinary hidden `Stored` stock, destination-reserved hidden `Stored` stock, then carried stock and destination `FacilityBuffer`.
- A delivery request does not alter aggregate warehouse inventory. Pickup atomically withdraws it, and failed carry insertion deposits it back.
- The pointer-driven build verifier previously spawned `FacilityBuffer` stacks directly. That shortcut was removed so it cannot hide a future request-time drop regression.
- The work-amount save contract had a stale `save.version == 9` assertion despite the product using V12; the assertion now follows `DungeonGameSaveData.CurrentVersion`.

## 2026-07-26 V16 integration audit

- `GameplayScene` contained both the production owner command controller and a priority-command duplicate, plus production and `_Test` regular-customer runtimes. Exact-one composition validation is required because first-match lookup silently accepts this corruption.
- `ExpeditionEquipmentRuntime` and `ICombatEquipmentRuntime` both authored inventory, loadouts, crafting, offense modifiers, and save data. Offense applied both bonus paths, so consolidation must remove the legacy stat block rather than adapt both indefinitely.
- The common combat runtime already owns persistent equipment instance IDs, quality, durability, ammunition, and active loadouts, making it the correct authority. Its missing piece was work-unit crafting and physical material/output integration.
- Offense weakening, prisoner rewards, special-monster rewards, and recruit rewards are counters only. They must become regional pressure or pending physical/persistent arrivals before the old reward-state fields can be removed.
- `SurvivalFoodRuntime` both withdraws food at daily settlement and allows real meal completion to consume food. Daily withdrawal must become forecast/reporting only to avoid double consumption.
- Exterior incidents currently advance through text and timers without persistent actors, inventories, theft stacks, rescue patients, or handler-owned stages.
- Circus fame and injury history are recorded but do not gate treatment, contracts, release, or performer availability.
- Blood and memory extraction currently collapse into generic Mana-style stock. They need Biological and Knowledge categories plus physical, work-based consumers.
- `CharacterAiPerfSettingsSO` and report types exist without a runtime recorder, so the current performance surface cannot provide trustworthy rolling avg/p95/max/GC/path-cache evidence.
- Offense targets currently carry no region or faction identity. The human/rival reward handlers only increment `OffenseRewardState` counters, and the terminal truth target still grants a meaningless rival weakening reward.
- `ExteriorActivityRuntime` already owns visible departure/return movement, a physical entry point, body-health checks, and medical-order creation. V16 return rewards should attach to this completed return boundary rather than create a parallel arrival animation service.
- Exterior incident persistence currently stores only kind, zone, text, and remaining seconds on a zone marker. There is no handler-owned actor, inventory, stage, or outcome state.
- V15 offense persistence serializes all abstract reward counters inside `DungeonOffenseRewardSaveData`; regional pressure and pending arrivals should be independent domain sections so the offense service no longer owns their restore order.
- Save restoration is already dependency-sorted by section and phase, so V16 can express the required equipment/items → characters/wildlife → captivity → arrivals → incidents → regions order without central orchestration.
- `InvasionIntruderRuntimeFactory` is the single constructor boundary for runtime intruder actors and is a suitable point for applying a captured regional pressure snapshot once per spawn.
- Offense enemy templates are materialized in `OffenseEncounterCatalog.CreateEnemies`; applying regional armament/manpower factors there avoids a second post-construction stat mutation path.
- Invasion intruder health and attack configuration is finalized in `InvasionIntruderRuntime.Initialize` inside `InvasionIntruderSystem.cs`, so regional modifiers should be supplied with the spawn settings before actor health is scaled.
- `CharacterPopulationService` already owns deterministic persistent IDs and full generated growth profiles, but exposes no API for adding a reward candidate. Extending that boundary avoids a second profile generator.
- Recruitment activation can bind an actor back to an existing population profile by matching `CharacterIdentity.PersistentId`; a reward candidate therefore needs a population profile and a matching `RegularCustomerRecord`, not a counter.
- Wildlife spawning is private to `WildlifeRuntime`. A narrow `TrySpawnArrival` method on `IWildlifeRuntime` can reuse the catalog, grid validation, hierarchy, actor initialization, and registry path without exposing general mutation internals.

## 2026-07-26 AI profile boundary and allocation findings

- `CharacterNeedCatalog.All` rebuilt a sorted array on every access. Survival scoring calls it
  for each AI candidate, making the catalog a measurable allocation hotspot at population scale.
- Offscreen/nonselected actors do not need full utility strings or breakdown objects. Retaining
  compact numeric scoring while collecting details only for selected diagnostics preserves
  decisions and substantially lowers garbage.
- `WorldCharacterNameplate` previously captured a complete deprivation snapshot only to display
  the highest burden and breakdown state. A narrow display-state query avoids grouping,
  dictionaries, and arrays on every visible nameplate update.
- The first PlayMode profile implementation sampled immediately after forced GC and reused the
  last warmup scheduler timing. That made a 2.6-second sample window report impossible 17-second
  scheduler frames. Discarding two transition frames produces coherent wall, frame, and
  scheduler timing.
- Unity 6000.3.8's Mono runtime returns zero from
  `GC.GetAllocatedBytesForCurrentThread()` even around a known 4KB allocation. Scheduler-only
  allocation is therefore explicitly reported as unsupported; `GC Allocated In Frame` remains
  the authoritative Editor-wide counter.
- The stabilized 100-character result is frame `2.77ms average / 3.42ms p95`, scheduler
  `0.370ms average / 0.497ms p95 / 0.632ms max`, all 100 trees ticked, and zero decision/path
  budget overflow.

## 2026-07-26 Weighted navigation and 500-character profile

- Unweighted BFS could not represent shallow-water speed, door policy, traversal penalties,
  or cost-aware target choice. Fixed destinations now use A*, while multi-target candidate
  scoring retains a weighted Dijkstra field.
- The current 60x3 gameplay and 96x3 stress grids are small enough that one Job per route
  would add more scheduling overhead than search work. The optimized weighted A* benchmark is
  about 11.3 microseconds per query.
- The largest apparent 500-character hotspot was an Editor test provider repeatedly calling
  `FindFirstObjectByType<GridSystemManager>`. Caching the fixture manager reduced the
  diagnostic average from 13.61 ms to 2.60 ms.
- The final staged 500-character profile passed: frame average/p95/max
  `3.39/4.37/15.40ms`, scheduler `1.228/1.809/2.580ms`, and no sampled frame exceeded
  16.67 ms.
- Broad multithreading is therefore deferred. Only immutable, batched offscreen scoring or
  route requests are safe future candidates; Unity objects, door access, reservations, and
  route commit remain main-thread responsibilities.
