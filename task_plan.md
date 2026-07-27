# DungeonStory Active Plan

## Goal

Complete V16 by removing isolated or duplicate gameplay authorities and proving that the connected
equipment, offense rewards, arrivals, exterior incidents, nutrition, circus resources, and AI
performance paths work together:

```text
physical production and meals -> persistent characters/items/equipment
-> offense and exterior outcomes -> physical arrivals and regional pressure
-> captivity/circus/survival follow-up work
-> V16 save round trip, pointer UI, visual evidence, and performance closure
```

## Phases

| Phase | Scope | Status |
|---|---|---|
| 1 | Audit offense, facilities, rooms, stock, staff, rewards, and save contracts | Completed |
| 2 | Implement route nodes, supplies, stress, formation, retreat, and expedition state | Completed |
| 3 | Connect dungeon rooms/facilities/stock to preparation, recovery, and expedition modifiers | Completed |
| 4 | Replace offense UI with preparation, route, node, and formation-aware battle surfaces | Completed |
| 5 | Persist and restore active multi-node expeditions with migration | Completed |
| 6 | Verify formulas and state transitions in EditMode | Completed |
| 7 | Verify pointer-driven recruitment, journey, battle, save/restore, and `truth_core` completion with MCP captures | Completed |
| 8 | Audit character identity, stats, training, battle abilities, UI, and save ownership for per-character progression | Completed |
| 9 | Implement per-character level, experience, learned skills, and equipped skill slots | Completed |
| 10 | Connect training and offense outcomes to experience and skill unlocks | Completed |
| 11 | Surface level, experience, learned/equipped skills in character and offense UI | Completed |
| 12 | Persist progression and migrate existing characters and saves | Completed |
| 13 | Verify progression formulas, combat skill legality, UI input, and save round trip | Completed |
| 14 | Replace legacy progression with level-50 potential, stat growth, narrative ledger, modular skills, passives, and ultimates | Completed |
| 15 | Add constrained LLM skill generation, validation, persistent retry, and hidden request state | Completed |
| 16 | Replace owner selection with three-character start preparation and persistent world population | Completed |
| 17 | Integrate growth/event UI, save V3 incompatibility handling, and combat/operation triggers | Completed |
| 18 | Verify growth generation, save restore, pointer workflows, world population, ultimates, captures, and regressions | Completed |
| 19 | Audit weak links between completed gameplay systems and prioritize missing feedback loops | Completed |
| 20 | Unify persistent character identity, social memory, and V4 save validation | Completed |
| 21 | Rebalance level-50 progression and connect generated skill modules to runtime events and formations | Completed |
| 22 | Share cached room environment queries with AI, mood, guest, and work duration systems | Completed |
| 23 | Add equipment catalog, crafting queue, expedition loadout, death loss, and facility recovery | Completed |
| 24 | Surface stat breakdowns, crafting, equipment, stress, and readiness in product UI | Completed |
| 25 | Add deterministic EditMode and pointer-driven PlayMode coverage for the new closed loop | Completed |
| 26 | Direct-play the campaign through `truth_core`, capture desktop/mobile/world evidence, and clear the Console | Completed |
| 27 | Add physical item catalog, world stack runtime, pile marker, and V5 save payloads | Completed |
| 28 | Connect delivery, rewards, warehouse aggregation, carried inventory, and hauling limits | Completed |
| 29 | Add Haul work type, AI hauling action, pickup/dropoff pathing, and overburden movement penalty | Completed |
| 30 | Add item pile UX with marker badges, list/detail panel, Alt-click override, and character-first selection | Completed |
| 31 | Convert shop restock, purchases/theft, crafting input/output, and expedition packing to physical stack flows | Completed |
| 32 | Add item/hauling EditMode coverage for stack merging, reservation, weight, save restore, and pile UX sorting | Completed |
| 33 | Add pointer-driven PlayMode coverage for item piles, hauling, warehouse/shop/craft/expedition flows | Completed |
| 34 | Capture stack marker, pile list/detail, carry UI, and clear Console Error/Warning 0 | Completed |
| 35 | Split new-run owner and start-party preparation into a dedicated preparation scene | Completed |
| 36 | Add owner fixed skill slots and reserve staff roster preparation | Completed |
| 37 | Build owner-select and RimWorld-style party preparation UI | Completed |
| 38 | Verify preparation scene navigation, selection, reroll, start handoff, and compile state | Completed |
| 39 | Fix start-preparation roster drag swap, RimWorld-style detail layout, dice reroll placement, and start-button gate | Completed |
| 40 | Add unified work-order runtime, construction sites, work units, and V9 save payloads | Completed |
| 41 | Route placement, AI work, materials, crafting, research, cooking, butchering, water, treatment, and refuel through work units | Completed |
| 42 | Surface construction/work progress in UI and character labels | Completed |
| 43 | Verify compile, focused contracts, pointer gameplay, save/restore, and visual captures for work progress | Completed |
| 44 | Diagnose and fix world nameplate occlusion and readability across dungeon layers | Completed |
| 45 | Replace wildlife horizontal oscillation with habitat-aware varied path movement and stable intent timing | Completed |
| 46 | Restore player camera zoom input with unscaled controls and verify nameplates, wildlife motion, and zoom in PlayMode | Completed |
| 47 | Audit staffed checkout waiting, customer patience, mood, memory, and alternate-shop handoff | Completed |
| 48 | Add patience-scaled checkout stages, service calls, complaints, abandonment, and alternate shopping | Completed |
| 49 | Surface checkout wait position, elapsed time, and reactions through character phases and event alerts | Completed |
| 50 | Verify patience rules, visit handoff, personal facility memory, PlayMode behavior, and Console state | Completed |
| 51 | Audit paused stair traversal visibility and multi-low-need AI triage | Completed |
| 52 | Make stair traversal visibility obey scaled simulation time | Completed |
| 53 | Fix survival-only emergency triage, fallback selection, and worker/owner self-care access | Completed |
| 54 | Add paused traversal and combined low-need regression coverage | Completed |
| 55 | Verify the fixes in PlayMode and clear Console errors/warnings | Completed |
| 56 | Audit repeated emergency wait and stationary-character fallback paths | Completed |
| 57 | Replace stationary wait fallback with contextual micro-actions and reachable roaming | Completed |
| 58 | Connect low mood to bounded autonomous impulses instead of passive waiting | Completed |
| 59 | Add anti-stall detection, retry/backoff, and regression coverage | Completed |
| 60 | Verify moving fallback and low-mood behavior in PlayMode | Completed |

## Product Decisions

- Party size remains 3 and the owner cannot join.
- Party positions are front, middle, and rear; skills declare usable and target positions.
- An expedition contains multiple connected nodes, not one battle.
- Supplies come from dungeon stock and are consumed during the expedition.
- Health and stress persist between nodes. Retreat preserves survivors and collected loot but forfeits unsecured rewards.
- Dungeon rooms and facility abilities provide preparation capacity, recovery, scouting, and supply efficiency.
- Death remains permanent. Returning survivors recover through dungeon services rather than automatic full healing.
- Campaign regions end in bosses; only the final `truth_core` boss reveals the truth and wins the run.
- The temporary campaign-order combat-stat multiplier is not part of the target design and must be removed.
- Character level, experience, learned skills, and equipped skills are per-character runtime/save state; `CharacterSO` remains immutable species/archetype authoring data.
- Skill definitions are shared data, while unlock and loadout state belong to each character instance.
- Character skill slots are fixed at one species active, three normal actives, two passives, and one ultimate.
- Potential affects only normal-active rarity odds; traits remain identity modifiers and passives remain event-triggered learned abilities.
- Generated skill state, drafts, narrative facts, retry keys, and use limits are per-world-character save data, never mutable shared ScriptableObject state.
- Skill rules choose rarity, budget, allowed module IDs, and variants before the LLM; invalid output retries under the same hidden request key with no player-facing fallback or generation status.
- The run begins with an owner and two same-species employees after all three have a selected level-one active and validated first passive.
- Old progression saves are intentionally incompatible with the new growth schema and must start a new game.
- `CharacterIdentity.PersistentId` is the sole runtime identity; template IDs never key per-person state.
- Room quality affects facility choice, mood, guests, and eligible work duration only; it never modifies offense stats directly.
- Facilities affect expeditions through crafted equipment and completed recovery use, not ambient combat bonuses.
- Generated skills have rule-authored formation masks and every accepted module must execute in an allowed runtime context.
- V4 rejects duplicate persistent IDs and V3-or-older saves instead of silently merging or migrating person state.
- Gold remains abstract money; non-gold delivery, reward, loot, crafting, shop, and expedition supplies become physical world stacks.
- `DungeonItemCatalogSO` owns item authoring; runtime stack/carry/save state must not be stored on shared ScriptableObjects.
- Item click priority is Character > Item > Building, with Alt-click forcing item pile selection on occupied cells.
- Warehouse inventory becomes an aggregate view of stored physical stacks while loose/carried/reserved items remain visible as separate states.
- Hauling capacity is character-owned and overburden affects movement speed, not global time scale.
- Stored warehouse stacks are hidden in normal play and become visible only through the `물품` view toggle; when stored stacks exist in V5 saves, they resynchronize the warehouse aggregate on restore.
- New runs use `StartPreparationScene` between title and gameplay. Gameplay-scene owner selection remains only as a direct-scene QA fallback.
- Owners have four fixed owner-skill slots in addition to normal generated growth slots. Fixed owner skills are authored static identity data, not LLM-generated or rerolled state.
- Start preparation contains one locked owner, two selected same-species staff, and four reserve staff candidates. Only selected staff enter the run.
- Start preparation treats the owner as ready through fixed owner skills; only selected staff must complete first active/passive start choices.
- Selected and reserve staff can be swapped by dragging roster cards onto each other.
- Player-placed buildings become construction sites with material delivery and work-unit progress; default/new-run seed buildings remain completed.
- Shared SOs may define static work requirements, but delivered materials, reservations, completed work, and queues are runtime/save state.

## Verification Gate

1. Runtime and Editor assemblies compile with Console `Error 0 / Warning 0`.
2. Route generation, node transitions, supplies, stress, formation legality, retreat, death, loot, and boss completion have deterministic tests.
3. Dungeon stock and eligible room/facility effects visibly change expedition preparation and outcomes.
4. UI pointer input can prepare a party, buy/load supplies, choose route branches, resolve nodes, issue formation-valid combat commands, camp, and retreat.
5. Save/load restores the exact route node, party order, health, stress, supplies, loot, battle turn, cooldowns, and statuses without duplication.
6. A clean direct-player run completes all regions and reveals the truth at `truth_core`; no scenario-state injection counts as completion evidence.
7. MCP captures prove readable preparation, route, combat, return, and truth-result screens without overlap or input leakage.
8. Physical item work compiles cleanly before verification; no PlayMode result counts while Unity is running stale assemblies.
9. Stack pile list/detail, carried weight, hauling, warehouse/shop/crafting/expedition item flows, and Alt-click priority are verified by actual pointer/UI tests.
10. V5 save checks include world stacks, stored-warehouse mirrors, hauling settings, and per-character carried inventory.
11. Start preparation checks include title-to-preparation routing, owner fixed skill display, selected/reserve staff swap, prepared snapshot handoff, and no gameplay owner-selection panel in the product flow.
12. V9 work-order checks include construction-site placement, material delivery, partial progress, save/restore, and final building replacement without instant completion.

## Errors Encountered

| Error | Attempt | Resolution |
|---|---|---|
| Single-battle campaign became unwinnable at stage 3 | Direct Normal playthrough | Rejected the thin-loop balance patch; redesign offense around persistent multi-node expedition progression and dungeon support. |
| Initial audit searched a nonexistent `Assets/Scripts/Stock` folder | 1 | Located stock runtime under `Buildings/SO/StockInfo.cs` and warehouse query services. |
| Reward regression still expected launch-to-boss and victory full heal | 6 | Reworked it to traverse route nodes, resolve encounters, defeat the boss, grant rewards, and retain survivor injuries. |
| Product-shell verification clicked a recruit card behind the bottom HUD | 7 | Scrolled the card into a 140px bottom-safe region; the pointer-driven product shell then passed. |
| Full-campaign UI verifier selected old alert buttons with matching labels | 7 | Scoped pointer lookup to the active offense map, expedition, or battle panel. |
| QA batch runner introduced `CS1626` by yielding inside `try/catch` | 25 | Moved coroutine yields outside the exception-handled block before rerunning feature tests. |
| Physical item pile PlayMode verifier still used the legacy owner-option flow | 30 | Replaced it with the current start-party fast commit path and added a request-file runner; the pile verifier then passed. |
| Physical item batch wait script looked for `PhysicalItemPile: PASS` while the report writes `[PASS] PhysicalItemPile` | 33 | Treated the shell wait exit code as a harness-string mismatch, then verified the actual batch report, target reports, and Console directly. |
| Recruited staff disappeared from later direct-play expedition candidates | 26 | Made `WorldCharacterProfile.isStaff` authoritative during population bind/refresh/promote/release and prevented the spawner from returning staff profiles to the visitor pool. |
| Offense reward regression still expected instant warehouse stock after physicalization | 26 | Updated reward tests to accept warehouse delta plus physical dropoff stack delta, and aligned recruit-candidate expectations with the handler's minimum-two rule. |
| Physical item theft test could read a duplicate empty carry inventory | 32 | Marked `CharacterCarryInventory` as single-instance and updated fixtures to resolve inventories through `CharacterCarryInventory.Ensure`. |
| Runtime visual-inspection command referenced TMP and wildlife properties that do not exist | 46 | Read the concrete APIs, then queried the TMP `MeshRenderer` sorting data and `WildlifeActor.DisplayName`; the corrected command passed. |
| First zoom persistence fix targeted the legacy `UnityEngine.U2D.PixelPerfectCamera` type | 46 | Runtime component inspection found `UnityEngine.Rendering.Universal.PixelPerfectCamera`; switched the alias to the URP type and reran the pointer test successfully. |
| Pond route probe called a nonexistent two-argument `Grid.SearchPath` overload | 47 | Read the concrete Grid API and used `GetMovePath(start, endPredicate)` instead. |
| Pond route probe started on the occupied entrance-door cell and reported no generic move path | 47 | Tested continuity from the first exterior surface cell; the exterior route and all shallow pond cells are reachable while only the boundary deep-water cell blocks movement. |
| Unity MCP approval was revoked during exact world-click verification | 48 | Used the compiled in-project UI regression request runner to execute the same Input System pointer path and collect the final report without bypassing gameplay input. |
| Physical delivery worker reached the source but could not pick up | Construction material delivery PlayMode attempt 1 | Found warehouse storage IDs were based on shared building definition `GridId`; replace them with a unique building instance key before rerunning. |
| Physical logistics rerun request did not auto-enter PlayMode | Construction material delivery PlayMode attempt 2 | Request file remained pending with Editor idle; enter PlayMode explicitly so the registered runner consumes the same request. |
| PlayMode could not start after warehouse-key migration edit | Construction material delivery compile attempt 2 | Preserved `IWarehouseFacility` type while matching a `BuildableObject` instead of passing the narrowed base type to the storage-key helper. |
| Combined regional-pressure patch did not match mojibake reward strings | V16 regional pressure attempt 1 | Split the change into focused structural patches and replace handler bodies using ASCII-only method boundaries. |
| Assumed `InvasionIntruderRuntime.cs` was a standalone file | V16 invasion pressure audit | Located the runtime in `InvasionIntruderSystem.cs` and switched subsequent reads to the concrete file. |
| MCP checkpoint command referenced the wrong `CompilationPipeline` namespace | V16 regional pressure compile attempt 1 | Use the MCP command's own pre-execution project compilation with a simple `AssetDatabase.Refresh` command. |
| Assumed character body-health models were in a standalone file | V16 return-arrival audit | Locate the concrete interface by symbol before reading; the captivity eligibility check itself was confirmed in `CaptivityRuntime.cs`. |

## Dark Survival V11 Completion

- [x] Add per-character deprivation burdens, health damage, probabilistic/forced breakdowns, and BT priority handling.
- [x] Add desperate relief, unsafe-water drinking, starvation violence/cannibalism, collapse, and nonlethal suppression paths.
- [x] Add physical exterior water, floor filth, wall stains, clean work targets, humanoid corpse metadata, and emergency butchery.
- [x] Add the character health tab, world breakdown warning, filth information/priority command, and V11 persistence.
- [x] Verify focused EditMode contracts, pointer-driven PlayMode behavior, camera/screen captures, and Console `Error 0 / Warning 0`.

## Exterior Habitat Decoration Completion

- [x] Build one static wildlife decoration palette from the authored TINY FOREST flower, tree, and rock sprites.
- [x] Place deterministic, nonblocking flowers, trees, and rocks only on walkable exterior surface cells.
- [x] Bind Grass/Brush flower density to habitat resource so grazing removes flowers and regeneration restores them progressively.
- [x] Keep decoration runtime state derived from habitat patches; do not add duplicate save data or per-decoration SO assets.
- [x] Verify EditMode contracts, the live herbivore grazing loop, hierarchy cleanup, PlayMode snapshot, camera capture, and Console `Error 0 / Warning 0`.

## Exterior Pond Visibility Completion

- [x] Exclude the entrance and drop zone from default water generation.
- [x] Place one bounded four-cell pond at the outer edge of the longest exterior surface run.
- [x] Ground-align the water visual, unlock per-cell tint, and render a readable pixel-water strip above terrain.
- [x] Keep three shallow cells walkable, the outer deep cell blocked, and the exterior route connected.
- [x] Verify runtime positions, tile occupancy, camera capture, focused contracts, and Console `Error 0 / Warning 0`.

## Zoom Sky / Centered Dungeon Completion

- [x] Resize and reposition the solid sky from the live orthographic camera viewport whenever zoom, aspect, or camera position changes.
- [x] Center the 27-column dungeon interior inside the 60-column physical world and shift every authored GameplayScene placement by the same offset.
- [x] Center the gameplay camera on the resolved dungeon interior at scene start.
- [x] Verify left and right outer-wall tiles after the shift and visually inspect the maximum zoom-out frame.
- [x] Run physical-world, background-lighting, and grid-foundation regressions with Console `Error 0 / Warning 0`.

### Verification Notes

- Minimum zoom: camera Y `1.25..7.75`, sky Y `-2..11`, coverage `true`.
- Maximum zoom-out: camera Y `-6..15`, sky Y `-8..17`, coverage `true`.
- Runtime dungeon interior is Grid X `17..43`, authored placement shift is `+13`, and camera X matches the dungeon world center at `-29.5`.
- A broader runtime-composition policy scan still reports unrelated pre-existing direct-access violations in other systems; the focused changed-surface regressions pass.

## Entrance Outer-Wall Adjacency Fix

- [x] Reproduce the one-cell gap beside the centered dungeon entrance.
- [x] Exclude characters, wildlife, items, and nonstructural exterior markers from automatic side-wall structure detection.
- [x] Confirm the outer wall moves from Grid X `12` to the correct adjacent cell X `13` beside the three-cell dungeon door.
- [x] Add a marker-overlap regression and verify the repaired entrance with Unity MCP Camera Capture.
- [x] Run grid visual, foundation, and physical-world regressions with Console `Error 0 / Warning 0`.

## Exact Facility World Click Completion

- [x] Remove the arbitrary `GridCell.GetBuilding()` fallback from ordinary facility selection.
- [x] Require an actual `Physics2D.OverlapPointAll` collider hit for facilities and construction sites.
- [x] Keep exact-cell fallback only for structural walls and interior doors that are rendered without normal colliders.
- [x] Reject hallway/floor definitions even if a hallway collider is present.
- [x] Verify actual facility click, bare hallway click, character-over-building priority, and exclusive info panels through Input System pointer events.
- [x] Finish with the UI regression batch at `RESULT=PASS`, captured `Error 0 / Warning 0`.

## Consecutive Wildlife Click Completion

- [x] Reproduce the same-animal consecutive click failure in the popup lifecycle.
- [x] Close the previously registered popup before assigning the newly clicked wildlife target.
- [x] Add current-target/open-state diagnostics and a repeated-event regression.
- [x] Add two consecutive Input System pointer clicks to the world-info PlayMode verifier.
- [x] Verify wildlife contracts, UI regression batch, and Console `Error 0 / Warning 0`.

## Wildlife World-Facing Completion

- [x] Trace wildlife facing against the project's mirrored Grid-to-world X mapping.
- [x] Derive horizontal facing from world-space movement instead of logical Grid X delta.
- [x] Update the natural-motion regression to assert left/right in world space.
- [x] Verify every wildlife species present in GameplayScene in both horizontal directions.
- [x] Finish with wildlife contracts and Console `Error 0 / Warning 0`.

## Defense Interception And Engagement V12

- [x] Audit current invasion movement, guard commands, defense UI, DI, persistence, and compile state.
- [x] Add adjacent-cell interception, reciprocal combat, one lead guard, and one replacement guard.
- [x] Add RimWorld-style defense policies assigned per guard and owner evacuation to an administration room.
- [x] Connect manual suppression, skill events, combat presentation, player-facing status, and defense UI.
- [x] Persist policies, assignments, owner evacuation, and active engagements in V12 saves.
- [x] Verify focused contracts, pointer-driven PlayMode combat, captures, and Console gameplay `Error 0 / Warning 0` after the known Unity 6000.3.8 startup warning.

### Defense Decisions

- Automatic interception is limited to on-duty non-owner staff with Guard priority enabled.
- Melee combat is one blocker versus one intruder on separate adjacent cells; a second guard may wait behind for replacement but cannot attack through the lead guard.
- Defense behavior is configured through named policies and each guard is assigned one policy.
- The owner never auto-dispatches. Every invasion cancels the owner's current action and evacuates them to an Administration room, or the farthest reachable interior safe cell when no valid room exists.
- Empty frontline means the intruder resumes advancing immediately; zero health continues to use the existing permanent-death flow.

## Developer Mode And Debug Palette

- [x] Add settings schema V2 with developer mode disabled by default and a dedicated Development tab.
- [x] Add a center-top Debug button, responsive non-modal palette, search, numeric input, eight command tabs, and exact world targeting.
- [x] Register 112 modular commands across cheats, spawning, characters, building/work, survival/wildlife, defense/events, overlays, and history.
- [x] Connect persistent `debugModified` metadata and a 50-entry command history while resetting transient cheats and overlays after load.
- [x] Verify pointer targeting, Shift repeat, right-click/Escape cancellation, commands, invasions, save behavior, overlays, and both supported aspect ratios.
- [x] Finish with EditMode PASS, PlayMode `RESULT=PASS`, Camera Capture comparison, and Console `Error 0 / Warning 0`.

## Construction Material Physical Delivery

- [x] Trace construction placement, delivery request, warehouse reservation, pickup, and site deposit.
- [x] Remove any construction-site material spawning or teleporting at placement time.
- [x] Keep materials physically stored until a worker picks up the reserved quantity and deposits it into the site buffer.
- [x] Add regressions for no-stock waiting, partial reservation, pickup, deposit, and construction readiness.
- [x] Verify the live placement-to-haul flow and Console `Error 0 / Warning 0`.

## Medieval Dark Fantasy Combat V13

- [x] Add shared melee, ranged, and recoverable-throw resolution with range bands, fire modes, evasion, directional cover, friendly-fire gating, armor penetration, body parts, bleeding, suppression, and pause-safe presentation.
- [x] Add individual weapon, armor, shield, and ammunition data plus persistent equipment instances, quality, armor durability, loadouts, reloads, crafting recipes, and V13 save state.
- [x] Connect defense to rally-time physical loadout pickup, post-breach melee interception, ranged line-of-sight combat, reciprocal damage, owner evacuation, and recoverable thrown equipment.
- [x] Connect offense to the same resolver, formation distance, cover, weapon switching, ammunition, body-part injuries, suppression turn loss, and persistent return-state wounds.
- [x] Add combat UI, cover buildings, exact multi-select/direct movement commands, fire-mode/hold-fire controls, and player-facing combat status.
- [x] Connect wildlife hunting and retaliation to the shared combat resolver, ranged firing positions, scaled-time reloads, simplified persistent body profiles, and real armor/body damage on hunters.
- [x] Verify static combat/offense/defense/priority/wildlife contracts, PlayMode wildlife loop, defense rally and engagement, direct movement and cancellation, visual capture, and Console `Error 0 / Warning 0`.

### V13 Verification Notes

- Defense PlayMode: rally held outside, four reciprocal exchanges on distinct adjacent cells, both sides damaged, intruder movement and facility attacks locked, owner evacuation and save snapshot valid.
- Player command PlayMode: `Cain (19,0) -> (17,0)` completed with manual lock released; a second move cancelled immediately also released its lock.
- Wildlife PlayMode: runtime snapshot and hunt/carcass/butcher loop passed; limb injury lowers mobility and survives capture/restore.
- `ScreenCapture`: `Artifacts/QA/combat-v13-defense-final.png`.
- Unity MCP `Camera_Capture` was attempted twice against the live Main Camera but the connector returned `Failed to render scene preview`; the direct Game View capture rendered correctly.

## V16 Isolated Feature Integration

- [x] Audit duplicate scene runtimes, legacy equipment, abstract rewards, food consumption, exterior incidents, circus milestones, extract resources, and AI performance diagnostics.
- [x] Remove duplicate GameplayScene command/customer runtimes and enforce exact-one composition lookup.
- [x] Remove the legacy expedition equipment stack and make common combat equipment authoritative for crafting, storage, loadout, defense, and offense.
- [x] Replace abstract offense weakening and reward counters with regional pressure and physical return arrivals.
- [x] Connect exterior incidents, reception, patrol readiness, weather, sanitation, and night danger to physical actors and outcomes.
- [x] Remove daily abstract food withdrawal and make completed character meals the sole nutrition consumption path.
- [x] Connect circus fame milestones, injury gating, Biological blood, and Knowledge memory residue to work-unit consumers.
- [x] Wire allocation-free AI performance recording and remove unused expedition support ability and mojibake on changed surfaces.
- [x] Finish broad regressions, pointer-driven PlayMode verification, captures, performance checks, and Console `Error 0 / Warning 0`.

### V16 Performance Closure

- [x] Split full Grid content changes from structural/traversal changes so items, wildlife, and filth do not invalidate route and room caches.
- [x] Preserve current wildlife target reachability without relying on stale cached occupant positions.
- [x] Repair wildlife arrival dwell to use one game-clock time base and pass Grid/Wildlife/AI focused regressions.
- [x] Re-run 100-NPC EditMode stress: elapsed `353s -> 50.6s`, broker searches `1440 -> 51`, deferrals `16461 -> 50`, Scheduler p95 `0.73ms`.
- [x] Run PlayMode profiling and the broad V16/domain regression matrix.
- [x] Perform current visual capture and final stopped-editor Console audit.

### V16 Verification Notes

- Broad domain matrix passed for V16 integration, save sections, survival, exterior activity,
  captivity/circus, offense reward/battle, combat, defense, work amount, Grid, AI naturalness,
  wildlife, and physical items.
- Pointer-driven UI verification passed `21/21` rows at `1600x900` and `900x1600`, including
  alert right-click dismissal, with captured `Error 0 / Warning 0`.
- The stabilized 100-character PlayMode profile recorded frame `2.77ms average / 3.42ms p95`
  and scheduler `0.370ms average / 0.497ms p95 / 0.632ms max`, with all 100 behavior trees
  ticked and no decision/path-budget overflow.
- Unity Editor-wide GC averaged `182KB/frame`; subtracting the measured one-character Editor
  baseline of about `120KB/frame` leaves about `62KB/frame` attributable to the stress world.
  The Mono backend does not support `GC.GetAllocatedBytesForCurrentThread`, so the report marks
  scheduler-only allocation as unsupported instead of falsely reporting zero.
- `Artifacts/QA/v16-gameplay-world.png` and
  `Temp/p1-p2-ui-surface-verification.png` provide current world and HUD evidence. Direct
  `Camera_Capture` still returns `Failed to render scene preview`; direct Game View capture works.

### V16 Decisions

- V16 is new-game only; V15 and older saves are rejected with a Korean explanation.
- Common combat equipment is the only authoritative equipment runtime.
- Food is consumed only when a character completes a real meal.
- Prisoners, special wildlife, and recruits return as physical or persistent world entities rather than counters.
- Strategic pressure is regional with a 25% same-faction spillover.
- Blood and memory residue remain physical resources with multiple work-based consumers.

## V17 Weighted Navigation and 500-Character Performance

- [x] Add deterministic terrain/traversal costs and weighted path results.
- [x] Use exact A* for fixed destinations and weighted Dijkstra for multi-target selection.
- [x] Add versioned broker caching, bounded search budgets, and reusable search workspaces.
- [x] Replace per-frame actor polling with due-time scheduling and immediate dirty wakeups.
- [x] Remove benchmark scene scans and hot-path decision/presentation allocations.
- [x] Pass focused Grid/100-character regressions and the staged 500-character profile.

### V17 Verification Notes

- 500 actors, 600 sampled frames: 3.39 ms average, 4.37 ms p95, 15.40 ms maximum,
  and 0 frames over 16.67 ms.
- Scheduler average/p95/max: 1.228/1.809/2.580 ms.
- Broker: 527 searches, 8,674 cache hits, bounded at 7 searches and 8 deferrals per frame.
- Incremental GC after the same-world Editor baseline: 36.0 KB/frame.
- Per-request Jobs/Burst are intentionally deferred: current weighted A* measures about
  11.3 microseconds/query, below practical scheduling overhead. Future parallelization must
  batch immutable offscreen work.
