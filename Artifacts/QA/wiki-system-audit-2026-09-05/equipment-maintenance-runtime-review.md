# Equipment maintenance runtime review

## Audit scope

- Authored facility: `building:8830` / `RF30_정비_부품함.asset`
- Ability: `BuildingEquipmentMaintenanceAbility`
- Public wiki surfaces: `data/entities/facility/building-8830.json`, `content/guides/combat-and-equipment.md`
- Runtime surfaces: maintenance policy, repair-order creation, physical delivery, repair work, terminal publication, save/restore, and UI exposure

## Confirmed authored values

- `workSpeedMultiplier`: `1.2`
- `simultaneousRepairSlots`: `2`
- `repairSupplyItemId`: `tool:maintenance-kit`
- `repairSupplyPerQuarterDurability`: `1`

## Confirmed runtime behavior

- Automatic and manual repair requests accept armor and shields only; weapon targets are rejected.
- Automatic creation requires an assigned automatic policy, no existing order for the target equipment, and durability at or below the policy send threshold.
- Supply demand is one maintenance kit per started quarter of missing durability, yielding `1` to `4` kits for RF30.
- Base work demand is `12 + 28 * lostDurabilityRatio`; contributed repair work is multiplied by RF30's `1.2` work-speed multiplier.
- Unless the selected policy permits unequipping during an invasion, the order waits for combat to end. Otherwise it proceeds through physical equipment and supply delivery to the facility buffer.
- Completion coordinates supply outbox commit/resume, durability publication and acknowledgement, equipment release, and terminal close with replay/conflict handling.
- Policy, assignment, active-order, terminal-effect, and sequence state participates in save/restore.
- The building UI exposes the facility repair queue and progress; the Operations surface exposes maintenance policy and assignment controls.

## Built-in policies and UI entry points

- `Standard` / “표준”: automatic, send at `35%`, return at `90%`, prefer replacement.
- `Preventive` / “예방 정비”: automatic, send at `60%`, return at `100%`, prefer replacement.
- `Manual` / “수동”: automatic repair disabled, send threshold `0%`, return at `100%`.
- The Operations surface displays send/return thresholds and invasion-unequip permission, and supports stepping those values, toggling invasion unequip/replacement preference, creating, duplicating, deleting, and assigning policies.
- A character-summary combat presenter invokes `TryRequestManualRepair`, so manual repair has a non-editor player UI entry point.

## Unresolved before registration

- `simultaneousRepairSlots` appears only in the ability definition, RF30 asset, editor asset builder, and an editor verifier fixture. No production consumer was found. It is therefore an implementation orphan and must not be documented as an effective two-slot runtime limit.

## Implementation follow-up, excluded from the documentation gap

- `BuildingEquipmentMaintenanceAbility.simultaneousRepairSlots = 2` is authored but does not constrain order creation, delivery, queue selection, or work execution in production code.
- This is retained as implementation follow-up evidence only. The public wiki does not promise two simultaneous repairs, so it is not a wiki-to-implementation `DIFF`/`WIM` item.

## Provisional documentation gap

The public facility entry currently summarizes construction and “장비 정비, 생산 작업대”, while the combat/equipment guide discusses durability effects and historical repair only. The policy thresholds, eligible equipment types, material/work formula, physical delivery, invasion gating, transactional completion, save fields, and queue/policy UI therefore constitute one owner gap. The authored but unconsumed two-slot field is implementation follow-up evidence, not an effective behavior to publish.
