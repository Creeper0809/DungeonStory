# Surgery System Findings

## Existing Foundation

- Human body health currently owns six external `CombatBodyPart` states plus blood loss,
  suppression, downed state, treatment, and save capture.
- `CharacterMedicalRuntime` already owns stabilization, rescue, physical carry, medicine
  delivery, treatment work, bed reservations, and recovery.
- Wildlife has separate whole-body and pooled head/torso/limb health state.
- `work:treat` is registered and uses the existing registered work execution architecture.
- Character stats are stable-ID backed despite the legacy enum adapter; current initial total is 55.
- Existing research includes Medical Recovery, Records, Anesthesia, Advanced Pharmacology,
  animal husbandry, steel, blacksteel, and arcane progression.
- Physical anesthetic and medicine items and recipes already exist.
- Beds R01-R03 carry `BuildingMedicalAbility`; no surgery-specific facility exists.
- Building abilities are serialized static configuration while active state belongs in runtime/save.
- Research currently contains 72 projects and uses deterministic auto-layout.

## Design Constraints

- Do not put mutable patients, orders, reservations, or stock in ScriptableObjects.
- Do not extend the old treatment state machine into a surgery mega-switch.
- Use stable IDs and handler registries for procedures, effects, anatomy nodes, and work.
- Use the existing item/haul/work-unit systems rather than instant material consumption.
- Preserve the current V16 global save version and version only affected save sections.

## Integration Findings

- Wildlife surgery runtime and physical transport existed, but the wildlife panel had no surgery entry point.
- Corpse extraction runtime and hauling existed, but item-pile detail had no anatomy/extraction command.
- Captive laborers may use the NPC character type, so character type alone cannot determine consent or surgeon eligibility.
- Organ storage originally changed freshness without enforcing its configured capacity or physical fuel supply.
- The surgery success formula must normalize Medical/Dexterity/Research to the 45% skill budget before facility and room bonuses.
- Medical room naming needs to inspect actual room fixtures; the shared `Medical` role alone cannot distinguish an infirmary, operating room, and transplant room.
- Dynamic work becoming available did not invalidate `WorkTargetSelector`'s incremental scan. A surgery order could become ready while workers kept the previous "no surgery work" result.
- `DungeonWorkforceReplanService` requested an immediate decision only after finding a candidate, so it also needs to invalidate dynamic facility state before its candidate probe.
- The public physical-capacity query must combine organ anatomy and the legacy external body surfaces by taking the stricter result; additive or weighted averaging can hide catastrophic head or torso injuries.
- A completed research project's save state is not sufficient by itself. Restore must replay the current authored building and recipe rewards so catalog evolution does not leave completed research disconnected.
- A single work tick may cross several surgery thresholds at high skill or game speed. Clinical state history therefore belongs to the surgery order rather than relying on frame-by-frame UI observation.
- PlayMode verification must control only the dedicated surgery outcome random stream; modifying production success formulas or accepting random failures would make the test flaky.
