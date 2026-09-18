# Golem recharge runtime review

## Scope and authored facilities

- Active modular facility: `building:1037`, M02 마력저장조.
- Deprecated compatibility facility: P1 ManaStorage, authored id `17`, `deprecatedCompatibilityAsset: 1`, no public facility page. Initial-placement migration replaces it with M02 + M03 + E05.
- Both assets author the same `BuildingGolemRechargeAbility`: `resource:mana-crystal` ×1, `requiredWork` 100, `restoredCharge` 50.
- The public M02 page lists need recovery, production, storage, and construction only. It does not expose the Golem recharge capability.

## Runtime lifecycle

- The Refuel work handler routes a Golem actor and recharge-capable, non-destroyed facility to `SpeciesRuntime`.
- A new recharge job is available at charge `<= 35`; an already-started job remains available. New-job urgency is `clamp(100 - charge, 65, 95)`, while an in-progress job has urgency 95.
- Start selects the first stable-ID, non-forbidden mana-crystal stack with at least one available unit and reserves one unit under the Golem recharge owner. It records worker, facility, material stack, and a nonzero progress sentinel.
- Work persists up to 100 WU. On completion, the reserved material is consumed atomically, charge increases by 50 and clamps to 100, then the recharge order fields are cleared.
- If material consumption fails, progress is retained immediately below completion for retry. Explicit cancellation releases the reservation and clears the order.
- The implementation reserves and consumes a world stack but does not create a separate delivery order or prove that the crystal reached the facility buffer. Documentation must not claim a physical delivery step that the runtime does not execute.

## Golem charge consequences

- Authored Golem `chargeRateMultiplier` is 1. Runtime charge decreases by `0.035 * multiplier * elapsed` and clamps to 0–100.
- Below charge 25, the actor receives the mood factor “동력핵 충전 부족” at -10 for 5 seconds, one stack.
- At charge 0, the actor receives nonlethal damage at `max(0.1, maxHealth * 0.0025 * elapsed)` from “동력핵 방전”.
- Charge, integrity, wear counters, incident state, and recharge worker/facility/material/progress fields are captured in the version-3 character-species save payload and validated/restored by canonical sorted character ID.

## Public documentation comparison

- `facility/building-1037.json` does not mention Golem recharge.
- `character/characterspecies-9.json` exposes only an aggregate “효과 14개” fact and appearance summary, not charge metabolism, thresholds, or recharge work.
- `species-culture-and-life.md` only states that species affect life needs in general.
- `item/resource-mana-crystal.json` describes crafting, medicine, and arcane research, but not Golem recharge consumption.

## Audit boundary

- The legacy P1 facility is not counted as a second public-page gap because it is explicitly a deprecated compatibility asset and has no public entity.
- No direct non-editor UI presentation of charge or recharge progress was found in the initial exact caller search. The actual Refuel work entry point is documented; dedicated UI visibility remains an implementation follow-up unless a later consumer is found.
- The remote reservation/consumption behavior is implementation follow-up evidence. It is not a WIM item unless a public physical-logistics promise is proven to cover this operation.
