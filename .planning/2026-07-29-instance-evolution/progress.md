# Progress

## 2026-07-29

- Confirmed the worktree was clean before implementation.
- Read the active resource-production completion record and reused its established physical item,
  material, work, equipment, room, save, and performance contracts.
- Started Phase 1 audit.
- Confirmed existing facility replacement preserves state snapshots and records, while equipment
  instances and craft queues already persist through one runtime/save section.
- Confirmed Unity Editor baseline is idle with Console Error 0 / Warning 0.
- Identified equipment maintenance and code-provided item definitions as the reusable physical-work
  and catalyst authoring patterns.
- Confirmed facility evolution can use the existing building state-module persistence instead of a
  second global save section.
- Confirmed equipment evolution belongs directly on each `CombatEquipmentInstance`.
- Added bounded structured usage ledgers, deterministic hierarchical compaction, stable history
  hashing, evolution nodes, room activation rules, and attunement records.
- Added registered facility/equipment effect modules and catalyst families without creating one SO
  asset per generated grade.
- Facility instances now keep persistent IDs, mastery, generations, deterministic three-candidate
  drafts, modification orders, room-conditioned activation, and recalibration orders in their
  existing building state module.
- Facility production, research, and work duration now consume active evolution modifiers while
  burdens remain queryable independently of room compatibility.
- Combat equipment instances now keep mastery, generations, locked reforge snapshots, evolution
  nodes, attunement records, and unfinished reforge state while preserving the original instance ID.
- Reforging and facility modification use physical catalyst/material reservations, hauling to a
  facility buffer, and the existing persistent Craft work loop instead of instant completion.
- Added catalyst dismantling, same-grade refinement, potency upgrade, and 150% gold exchange.
- Combat attacks, shield blocks, and armor hits now feed equipment usage and attunement records.
- Raised the equipment save section independently and added an equipment-evolution order section;
  legacy V16 equipment instances restore at generation zero.
- Recompiled after the combat integration fix; Unity Console reports Error 0 / Warning 0.
- Connected deterministic offense/defense catalyst drops and the physical catalyst item catalog.
- Added immutable, evidence-locked narrative requests with correlated retries, late-response
  rejection, target destruction cancellation, and player-hidden pending state.
- Added facility relocation as dismantle, package hauling, and reinstall work while preserving the
  persistent facility ID and evolution state.
- Upgraded modular facility world payload to v3 so packed facilities restore on
  `GridLayer.Construction` with their package presentation instead of becoming completed buildings.
- Fixed packed facility destruction so construction occupancy cannot remain stale.
- Added the facility/equipment evolution surface to building details, including catalyst selection,
  three deterministic candidates, active/dormant nodes, recalibration, reforge, reattunement, and
  exact-cell relocation targeting.
- Fixed VContainer construction of `EvolutionModuleRegistry`; runtime gameplay now receives all
  built-in effect modules and no longer exposes internal `facility:*` IDs in candidate UI.
- Removed Unity-created empty order objects during state cloning so save/load cannot create ghost
  modification, recalibration, or relocation work.
- Added focused contracts for 128-event caps, 10,000-generation deterministic compaction,
  deterministic candidates, room gating, catalyst rules, narrative locks, packed save layers,
  packed destruction cleanup, and v2-to-v3 migration.
- Verified the exact relocation target surface activates and right-click cancellation works.
- Captured the building evolution HUD and main world camera in PlayMode; both were non-empty and the
  evolution controls were visible without intercepting world rendering.
- Passed instance evolution, authored facility evolution, combat, work amount, room environment,
  V16 integration, save section, combat material, physical item, building state, and modular
  facility save/load regressions.
- Final Unity Console check reports Error 0 / Warning 0.
- Re-ran the final focused suite through Unity MCP: instance evolution, authored facility
  evolution, combat, work amount, room environment, V16 integration, and save sections all passed.
- Confirmed the Editor is stopped and idle, and the final Console still reports
  Error 0 / Warning 0.
