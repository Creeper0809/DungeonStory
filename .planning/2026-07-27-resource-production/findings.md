# Findings

- Research already has a deterministic graph builder and UI; it was extended to 72 nodes.
- Physical items, reservations, hauling plans, facility buffers, work units, combat equipment,
  wildlife capture, spoilage, and V16 section saves already exist and should be extended.
- Exact item consumption is missing from facility buffers; current consumption is only by
  `StockCategory`, which is insufficient for recipes with distinct ingredients.
- Economy assets require each ScriptableObject to live in a matching source file. Recreated
  assets now have valid MonoScript bindings and load without Console warnings.
- Existing production abilities are category-oriented and cannot represent item-level recipe
  bills, material filters, target stock, or preserved reserves.
- Production stations are represented by 21 modular facilities with stable semantic tags. Recipes
  target those tags, while research projects own the corresponding unlocks.
- Unity MCP dynamic commands are compiled in a wrapper namespace and need fully-qualified Unity
  editor types when a short type name can resolve under `Unity.*`.
- The production content builder must run after P01-P21 specs change; code definitions alone do
  not update authored `.asset` files. The rebuilt modular catalog now contains 94 assets.
- `source:*` and `sink:*` recipe definitions describe system producers/consumers such as gathering,
  spoilage, animal pens, and fuel use. Only `recipe:*` definitions require a physical production
  station semantic tag.
- Wildlife habitat patches already own renewable resource quantities, and their decoration runtime
  reduces or hides flower sprites as animals consume grass/brush resources. Gathering should share
  or synchronize this state instead of creating a second decorative resource simulation.
- The current generic work target selector enumerates `BuildableObject` targets. Treating flowers,
  trees, and rocks as buildings would couple gathering to building click/room/placement behavior;
  resource nodes need a non-blocking grid layer and a registered world-work candidate path.
- VContainer selects the greediest public constructor unless one is marked `[Inject]`. Catalogs
  that also expose enumerable-only constructors for tests must explicitly mark their runtime
  asset-loader constructor or they silently resolve as empty catalogs.
- Crop plots can stay inside the existing registered work pipeline by exposing Sow/Harvest on the
  facility and keeping crop choice, delivered inputs, work progress, growth, and save state in a
  scoped `CropPlotRuntime`.
- The first physical crop contract now proves that an outdoor plot consumes delivered Water before
  sowing and produces a loose harvest stack only after sow, growth, and harvest work complete.
- A repair order needs destination coordinates on its unique equipment stack, not only a destination
  ID. Otherwise a loose unique item can be planned as an ordinary warehouse haul.
- Equipment in `Carried` or `MaintenanceBuffer` world state is already en route to repair and must
  suppress duplicate delivery-stack generation.
- `AbilityHaul` performed the atomic pickup before a cosmetic scaled-time delay. If scaled time is
  suspended at that exact yield, the stack has disappeared and the carrier remains stuck in the
  pickup phase; the state transition should continue on the next frame without an artificial delay.
- Facility-buffer deposits must preserve destination position metadata. Without it, the generic haul
  planner treats a just-delivered repair input as ordinary output and carries it back to a warehouse.
- Routed stored stock cannot be recognized by destination-name prefixes. Repair, production, combat
  loadout, and future handlers all use the same destination-position contract; pickup and planning
  must share that generic rule.
- World stacks disappear while inside `CharacterCarryInventory`, so delivery request idempotency must
  live on the owning repair order rather than be inferred only from current world stacks.
- Meal production is already item-exact, while meal use is still category-exact. Leaving both paths
  active would allow free recovery or double consumption, so meal recovery and ledger publication
  must move behind one physical meal-consumption service.
- The Models/Survival folder is an asmdef boundary. Shared meal enums must live in the Survival model
  assembly rather than Assembly-CSharp, while resource classification may depend on that model.
- Unity's bundled Mono `csc.exe` is older than the editor compiler and reports false errors for newer
  target-typed conditionals. Compile checkpoints use `Data/NetCoreRuntime/dotnet.exe` with
  `Data/DotNetSdkRoslyn/csc.dll` and the current Bee response files.
- Character work and combat multipliers already converge in `CharacterStats`; substance effects
  belong there so every work/combat consumer sees one authoritative multiplier without double application.
- Medical treatment previously consumed only `StockCategory.Medicine`, making every medicine
  interchangeable. A treatment order must persist the exact selected item and delivery-issued state
  because carried stacks temporarily disappear from the world-stack repository.
- Per-meal freshness authored on resource item definitions was not consumed by spoilage. The spoilage
  runtime now needs the economy catalog to distinguish short-lived meals from preserved rations.
- Captured wildlife already owns physical pen delivery, feeding, escape pressure, and save state.
  Husbandry should extend that runtime instead of introducing a second animal container.
- Livestock feed must be selected by diet and consumed as an exact physical item. Herbivores prefer
  hay, while carnivores and scavengers require dog food or another animal-compatible feed.
- Pen policy limits and the physical `BuildingBeastPenAbility` capacity must share one effective
  maximum; otherwise births can complete logically but never acquire a valid containment slot.
- Late-game contracts must size offers from population and research progression, not current
  warehouse quantity, or hoarding perversely scales the requested amount.
- Stock policies, regional contracts, and grand projects can share physical delivery primitives
  without sharing runtime ownership: each uses a stable destination ID and consumes only delivered
  facility-buffer stacks.
- Unity's console summary can omit VContainer's dependency path; `Editor.log` preserves it. The
  PlayMode cycle was `CharacterDeprivationRuntime -> WorldFilthRuntime -> ExteriorActivityRuntime
  -> CharacterMedicalRuntime -> CharacterDeprivationRuntime`.
- Medical treatment only needs to notify infection reduction. Publishing that result through the
  scoped game event bus removes the survival/medical constructor cycle without lazy service lookup.
- Stock policies must accept both authored economy item IDs and legacy physical category IDs such as
  `stock-item:4`; rejecting the latter makes the visible warehouse policy controls silently no-op.
- Feature-surface control cards must let their vertical layout own child heights. With
  `childControlHeight=false`, buttons visually remained in one section while their raycast region
  overlapped later section headers, so exact pointer clicks selected the wrong UI object.
- Forecast cards must prioritize enabled stock policies before applying the visible-card limit.
  Otherwise a policy can be configured correctly but disappear behind synthetic aggregate rows.
- The first real `100 staff + 100 livestock + x5` gameplay profile proves the simulation and
  rendering are viable at p95: frame `14.326ms`, scheduler `1.258ms`, wildlife tick `0.384ms`.
  Its failure is allocation spikes rather than sustained heap growth: average GC was `2.38MB/frame`,
  maximum `163.7MB`, while first-to-last-quarter managed memory changed by `-11.76MB`.
- The mixed profile uses the normal new-run path, 256x8 actual grid, 128 dense facilities,
  100 pooled staff actors, 100 captured/tamed wildlife actors, 338 total buildings, and all UI and
  presentation enabled. It is not the earlier isolated `StressWorld`.
- Seeding five days of real physical Food and Water avoids a synchronized deprivation storm while
  preserving the normal AI and hauling rules. Under this normal-operation load the latest profile
  reaches frame p95 `12.981ms`, scheduler p95 `1.970ms`, and retained managed growth `9.03MB`.
- The remaining profile failure is allocation throughput: baseline-adjusted average GC is
  `215,297B/frame` versus the `64KB/frame` gate. A raw profiler trace attributes the largest
  sampled allocation (`251,590B`) directly below `CharacterAiScheduler.ProcessAiBudget`.
- Earlier hot allocations from emergency stock lookup and first-visible nameplate component creation
  were removed with indexed nonalloc item queries and spawn-time nameplate prewarming.
- `AIWork.GetDestinationCandidates` allocates a new list for zero or one destination on every work
  action evaluation. The runtime can bypass this compatibility API by resolving its single work
  candidate directly in `TryResolveDestinationWithFailure`.
- The latest production-like `100 staff + 100 livestock + x5` profile already satisfies CPU and
  frame pacing (`10.2ms` frame p95, `1.192ms` scheduler p95, `69 FPS` 1% low) with Console
  Error 0 / Warning 0. The only remaining mixed-population gate is baseline-adjusted allocation
  throughput at `110,392B/frame`.
- Detailed Utility diagnostics were accidentally enabled for every visible actor. Restricting them
  to the selected actor removed the large per-decision candidate string/breakdown allocation while
  preserving the player-facing AI inspector.
- `WorldResourceRuntime` rebuilt a sorted string signature from every decoration on every frame.
  `WildlifeHabitatDecorationRuntime.StructureVersion` now invalidates the resource-node mapping only
  when decoration structure actually changes.
- `CropPlotVisualPresenter` still allocates a LINQ dictionary, hash set, key filter, and array every
  quarter second. Its scene-to-runtime synchronization should use reusable collections and indexed
  loops.
- `CharacterPresentationScheduler` calls every visible nameplate and feedback presenter each
  `LateTick`. Presentation should be split into immediate dirty-value updates and low-frequency
  ambient text refreshes so 100 visible characters do not rebuild labels every rendered frame.
- The former single-drop-zone supply setup was not a valid 100-resident steady-state scenario:
  one 500-unit water stack exposed only three relief approach cells and synchronized deprivation
  breakdowns. The measured dungeon now includes storage, water, crop, production, meal, and
  butchery facilities instead of repeating the first twelve catalog entries.
- Added physical warehouse seeding and verified the mixed profile distributes five days of food
  and water across nine real warehouse inventories/stored stacks. Stored-stack consumption now
  decrements the matching warehouse aggregate, preventing later mirror regeneration.
- Safe drinking now starts before critical burden, limits coroutine starts per frame, skips
  saturated water stacks/facilities, and uses a caller-owned nonalloc stock-candidate buffer.
- The AI root returned a valid `LockedAction` before running emergency relief. Existing
  `ShouldStopCurrentActionForReplan` only observes action invalidation, mood impulses, and
  action-owned interruption; it did not treat severe thirst as an interrupt reason. This allowed
  ordinary work to continue until deprivation became a breakdown even with physical water present.
- Survival interruption must be a capability of the running action, not a blanket BT reorder.
  Work, hauling, and hunting can yield after their minimum persistence; rescue, substance use, and
  active deprivation actions remain hard locked.
- Stored-stack consumption is a two-repository transaction. A stored physical stack may only shrink
  after the matching warehouse aggregate successfully withdraws the exact amount; partial aggregate
  withdrawal must be rolled back.
- The first distributed-supply rerun still reports about 204,554 baseline-adjusted bytes/frame.
  CPU remains within target (`frame p95 15.02ms`, scheduler `p95 1.475ms`), so another raw
  allocation trace is required; the remaining GC is not explained by supply topology alone.
- Deprivation stage diagnostics showed that path selection, rather than item consumption, caused
  almost all failed emergency drinks. Reusing the path-aware reservable source selector reduced
  desperate-drink movement failures from `1,310` to `8` in the same mixed-population profile.
- The stress dungeon's functional-facility selector excluded `GridLayer.Building`, so its
  storage/water/crop/production priority clauses could never select ordinary freestanding
  facilities. The nine warehouses in the profile were only the small pre-existing fixtures.
- All-category warehouses initialize with legacy seeded stock and the profiler filled Food before
  Water. The resulting effective capacity was `74`, so `500` Water still fell back to one drop
  position. Profile-created warehouses must start empty and the dense catalog must include the
  actual Building layer before this is a valid normal-operation load.
- The mixed-population profile authored synthetic `Stair` traversal links with a null movement
  occupant. Production movement correctly requires a stair handler, so every cross-floor safe-drink
  path ended as `MissingMovementHandler`. The profile now supplies an interface-based stair handler,
  and `AbilityMove` accepts any live `IGridMovementHandler` instead of requiring a `BuildableObject`.
- After fixing the traversal contract, blocked safe-drink movement failures fell from `354` to `0`.
  The remaining deprivation load exposed a second scenario flaw: the 128 dense facilities were
  filled floor-major, concentrating storage and water on the first two of eight active floors.
  Dense profile facilities must be balanced by floor and horizontal room segment.
- Balancing facilities reduced incremental GC from `170,757B/frame` to `107,112B/frame` and kept
  frame p95 at `12.257ms`, but hydration still chose adjacent-floor stock before same-floor stock.
  Manhattan distance treats one floor as one horizontal cell even when the path must reach a distant
  stair. Safe resource selection needs a strong floor-transition cost and must begin before the
  remaining travel window becomes shorter than a normal same-floor trip.
- The final feature gap is verification cohesion rather than another missing catalog/runtime:
  production, crops, consumables, husbandry, waste, planning, and V16 save sections each have
  focused contracts, but there is no single GameplayScene verifier that proves their physical
  hand-offs and player-facing controls in one persisted run.
- The current production contract validates authored content, physical material delivery, partial
  work/save continuation, stock planning, contracts, and grand projects. Survival contracts cover
  diet/substance definitions and consumables payloads; crop and world-resource verifiers require
  PlayMode and exercise the actual scoped services.
- Focused AI regressions exposed two cache isolation defects: editor-created buildings were not
  registered with the same scoped world registry used by production, and nearest-facility scoring
  scanned the global building list instead of the queried grid's role index. The latter exhausted
  the scoring slice on unrelated scene/test buildings and surfaced as a false `PathSearchDeferred`.
- Grid fallback facility membership must track `Grid.version`, not only the world-registry version,
  because editor fixtures and non-registry integrations can add occupants after an empty fallback
  index has already been built.
- `BuildingSO.unlocked` is authored fallback data, not runtime state. Mutating it during a shop
  purchase or offense reward contaminates the shared asset for the rest of the editor session and
  makes later new runs order-dependent. `BlueprintResearchState.UnlockedBuildingIds` is now the
  sole runtime authority, including legacy shop-save restoration.
- Research graph centering cannot subtract authored graph coordinates directly from
  `RectTransform.anchoredPosition`; the viewport pivot, root anchors, and current scale make that
  formula resolution-dependent. Transforming the node to viewport-local coordinates and applying
  the measured delta centers it deterministically.
- The final mixed-population measurement passes CPU and frame pacing but still allocates roughly
  `143KB/frame` after baseline adjustment. Feature development is complete; reaching the separate
  `64KB/frame` target requires another profiler-led presentation/decision allocation pass.
