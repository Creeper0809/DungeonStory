# Findings

## Initial State
- Worktree contains extensive uncommitted V16/V17 surgery, offense, economy, and content work.
- New infrastructure must integrate without reverting or rewriting unrelated modified assets.

## Architecture Notes
- Unity Editor 6000.3.8f1 is connected, idle, and reports Console Error/Warning 0 at baseline.
- Grid primitives live under Models/World and are shared by scoped runtime services.
- Gameplay time already uses `IGameClock`, suitable for pause-safe power/fluid/conveyor ticks.
- Save infrastructure is `IDungeonSaveSection`-based and topologically ordered.
- Runtime composition is split across VContainer registration extension modules.
- Existing item repository, physical item save section, production bill runtime, water/filth runtimes, and research project assets are the integration points.
- Current worktree is heavily dirty; all implementation must be additive and narrowly edit existing registrations/contracts.
- `GridLayer` currently ends at `DownedCharacter = 10`; Utility and Conveyor can be appended without renumbering serialized values.
- Research fields currently cover 15 domains through `SurgeryAndTransplant`; industry and water fields can be appended safely.
- Work dispatch already uses stable `WorkTypeId`; add `work:plumbing` there and keep the legacy flag only as an asset adapter.
- World-simulation registration is the correct scoped composition point for power, fluid, conveyor, and automation entry points.
- `IBuildingWorldQuery.BuildingVersion` provides cheap topology invalidation without scene scans.
- `ProductionBillRuntime` supports the same begin/apply-work path with a null worker, allowing automation to preserve material consumption, work progress, output handlers, and deterministic RNG.
- Production bill snapshots expose work type, status, work totals, and destination IDs needed for automation/status UI.
- `BuildingSO.layer` already drives placement and construction, so utility/conveyor segments can use appended layers without a second placement system.
- Grid cells store occupants by `GridLayer`, but exterior area rules and renderer sorting must explicitly recognize the new layers.
- Conveyor transport can temporarily remove an internal world-item record, preserve its stable stack ID/metadata in the conveyor payload, then reinsert it on output/overflow.

## Completed Architecture
- `IndustrialInfrastructureTopology` owns versioned connected-component snapshots and caches descriptors by network.
- Power simulation uses priority shedding, fuel, storage, breaker, overload, and on-demand presentation snapshots.
- Fluid simulation keeps clean, unsafe, foul water and wastewater distinct while preserving physical water-stack fallback.
- Conveyor routing preserves stack IDs and metadata, detects SCC deadlocks after powered stalls, and uses overflow gates without deleting payloads.
- Overflow prefers configured reserve storage and otherwise restores the payload as an ordinary loose world stack.
- Automation delegates material consumption, work progress, deterministic completion, and output generation to the existing production bill runtime.
- Save ownership remains domain-scoped and the V17 global version is unchanged.

## Validation Notes
- The synthetic large-network probe covers topology construction and 2,000 route requests, not a rendered scene with 2,000 simultaneously animated payload markers.
- Focused PlayMode verification confirms scoped runtime resolution, save-section registration, Industry UI creation, and pause-safe execution.
- The QA fallback scene contains no preplaced industrial machines, so it validates empty-state presentation rather than a visually populated factory.
- Final asset rebuild produced 118 research projects and retained Console Error 0 / Warning 0.
- The first populated PlayMode run exposed a real content integration bug: production facilities use role tags such as `mill`, `forge`, and `cookbench`, while the industrial patcher searched for a nonexistent `Production` semantic tag. Automation modules therefore were not attached to any production asset.
- The populated fluid scenario exposed stale read behavior: `TryGetNetwork` read the last materialized snapshot and did not refresh it after fluid ticks, so produced water remained invisible to gameplay queries until another caller accessed the full `Networks` property.
- The live conveyor loop exposed that deadlock detection required every payload to have its own 30-second stall timer. A fully occupied cyclic network could have no movement for more than 30 seconds yet remain merely `Stalled` because one payload became blocked later. The rule now follows network-wide last movement.
- The generated mana generator referenced `material:mana-crystal`, while the authoritative resource catalog uses `resource:mana-crystal`. The wrong ID prevented physical refueling and has been corrected in both the asset builder and generated asset.
- A single 10-output water generator powered the basic fluid loop but shed power from part of the connected conveyor network. The final live fixture uses one physically fueled 32-output mana generator so deadlock timing excludes power outages as specified.
- Final populated `GameplayScene` verification passed with 35 placed buildings, power `32/10`, piped shower wastewater `0.45`, a ten-node physical payload trip, automatic work rate `1`, and a 28/28 cyclic network entering `Deadlocked` at `30.02` game seconds.
- Manual overflow approval restored `qa:overflow-stack:0` as a loose stack while preserving the unique humanoid corpse source ID, display name, species, death reason, and emergency-butcher flag.
- The verification screenshot used the active 900x1600 Game view and showed the scenario without HUD overlap. It is functional evidence rather than a close-up industrial marketing capture.
