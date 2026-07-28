# Findings

- Existing facility evolution already owns recipes, state components, record tokens, mutation
  resolution, LLM proposals, room profiles, replacement, and a feature panel.
- Existing combat equipment instances already own unique IDs, definition/material/quality,
  durability, ammunition, owner, world state, crafting, repair, and V16 save integration.
- Resource production already provides exact physical recipes, material definitions, work units,
  hauling, and equipment material selection.
- `RoomInstance.Id` is transient, so permanent progression must remain on facility/equipment IDs.
- Existing V16 save sections can be versioned independently without changing the global save version.
- `FacilityEvolutionStateComponent` already transfers its snapshot across authored building
  replacement, so infinite-generation state should extend this snapshot instead of introducing a
  second facility authority.
- `FacilityEvolutionRecordComponent` stores aggregate metrics/tokens and only 12 recent text events;
  the new structured ledger can coexist during migration and eventually feed the same room profile.
- Combat equipment save authority is `DungeonCombatEquipmentSaveData` inside
  `CombatEquipmentRuntime`; extending `CombatEquipmentInstance` keeps crafting, ownership,
  durability, and save round trips under one runtime.
- Existing equipment maintenance already owns physical delivery and work-unit execution and is the
  closest implementation pattern for reforge orders.
- Physical stack save data has no generic metadata payload. Catalyst potency/provenance should be
  encoded by stable item definition IDs for stackable catalysts, while selected order state stores
  normalized family/grade/provenance fields.
- Generic construction work orders are category-based and target buildings. Equipment reforging
  should follow the specialized maintenance-order pattern rather than overloading construction
  orders with instance-target semantics.
- Combat registration already exposes one `CombatEquipmentRuntime`; the new equipment-evolution
  service should decorate/query that runtime instead of owning duplicate equipment instances.
- The item catalog already supports code-provided definition families, so catalysts can be exposed
  through a dedicated definition provider without mutating one giant catalog asset.
- `BuildableObject.GetStateModules()` automatically discovers attached
  `IBuildingStateModule` components. Making `FacilityEvolutionStateComponent` a state module
  preserves its persistent ID, ledger, and evolution nodes through the modular-facility save path.
- `CombatEquipmentInstance` is the authoritative unique-equipment DTO captured by
  `DungeonCombatEquipmentSaveData`; embedding `EquipmentEvolutionState` there avoids a second
  identity store.
- `CombatEquipmentInstance.Clone()` currently uses `MemberwiseClone`, so nested evolution state
  requires an explicit deep clone.
- The existing Craft handler can own facility modification, recalibration, and equipment reforge
  work without adding another global work-order authority.
- Catalyst grade and family can be represented by stable physical item IDs and code-provided
  catalog definitions, keeping integer potency unbounded without asset proliferation.
- Facility room benefits and burdens must be queried separately: room mismatch sleeps benefits but
  cannot disable fuel, heat, pollution, staffing, accident, or maintenance costs.
- Equipment evolution can be fed at the shared combat resolution boundary, which covers defense,
  offense, wildlife, and other consumers without duplicating result interpretation.
- Unity compilation after the current combat usage integration is clean at Error 0 / Warning 0.
- VContainer selected the registry's enumerable constructor even when no modules were registered,
  producing an empty runtime module registry. The parameterless built-in constructor must be the
  explicit injection constructor.
- A facility being relocated is registered on `GridLayer.Construction`, not its authored placement
  layer. Save, clear, restore, and destruction must all resolve the actual registered layer.
- Unity can materialize null serialized nested order classes as empty objects. An order is valid
  only when it has a stable non-empty order ID; otherwise cloning and persistence must treat it as
  absent.
- The existing building-detail scroll surface is a suitable shared presentation shell. Evolution
  controls remain scoped to workable facilities, with equipment controls added only when the
  selected facility owns equipment crafting.
- PlayMode validation confirmed deterministic candidates appear only at the mastery threshold and
  that catalyst-gated candidates remain visibly unavailable when no physical catalyst is stored.
