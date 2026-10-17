# Phase 80 producer closure review — groups 1 and 2

Verdict: **BLOCKED**

This review applies the authoritative-transaction requirement from
`Tools/Documentation/gameplay-outcome-narrative-ledger-plan.md` to every
`Record` row in inventory groups 1 and 2. An event publication or a receipt
object is not counted as ledger integration unless capacity is reserved before
the authoritative mutation, the exact immutable result is prepared, domain
state and the ledger outbox are committed together, rollback is available up
to the joint commit, and replay/save ownership is durable.

## Counts

| Group | Record rows | Integrated | Exact higher-result substitute | Blocked |
|---|---:|---:|---:|---:|
| 1 — production/resources/world | 21 | 0 | 0 | 21 |
| 2 — combat/invasion/medical/captivity | 21 | 0 | 0 | 21 |
| Total | 42 | 0 | 0 | 42 |

Two partial bridges exist, but neither qualifies for closure:

- `ProductionCompletedOutcomeBridge` is invoked only after prepared/exact
  physical output has already committed. Its current receipt is an aggregate
  quantity/mass/loss summary rather than the exact
  `ProductionRecipeExecutionReceipt` output lines, stack slices, commit IDs,
  WIP input and fingerprints. The receipt authority itself is not a persisted
  owner/outbox. It therefore cannot substitute for the lower physical receipts
  and cannot roll production back if outcome preparation fails.
- `CombatDamageOutcomeBridge` reserves before direct-command resolution and
  has a rollback-capable path only for positive character damage. Misses,
  cover blocks, zero-damage hits and wildlife targets deliberately cancel the
  reservation and apply through `TryApplyUntrackedMechanical`; the canonical
  receipt also omits ammunition, mana/power, armor/cover durability, status,
  downed/death/threshold and visitor-facility facts. It therefore cannot
  substitute for the group-2 combat result rows.

## Safe hardening completed in this pass

- Both partial adapters now write immutable Korean display-name snapshots
  instead of the obsolete ID-as-display constructor.
- Production rows retain bill and recipe non-retention provenance; positive
  character-damage rows retain the attack operation plus damage type/body-part
  facts in the frozen replay hash.
- The canonical attack operation ID is propagated into the derived
  `SocialConflictEvent`, allowing downstream social publication to reconcile
  with the attack authority instead of inventing a second identity.
- Descriptors now reject rows that omit those provenance/fact contracts.

These changes make the already-partial paths fail closed and historically
displayable. They do **not** change the 0/0/42 closure result.

## Exact owner seams still required

1. Production must reserve an outcome page after deterministic output
   resolution but before any physical batch/unit publication. The prepared
   output owner, exact-route owner, WIP input owner and gameplay-outcome outbox
   must publish or roll back as one transaction. The exact execution receipt
   and pending delivery identity must be saved before completed output is
   cleared.
2. Direct combat needs one exact attack receipt for executed failure, miss,
   evade, shield/cover block, character/wildlife damage, all resource and
   durability mutations, status effects and lifecycle transitions. Cover
   destructive loss and wildlife death/haul invalidation require detached
   prepare/rollback owners before this can be joint-committed.
3. Body-health, medical, captivity, defense, invasion, blueprint research,
   facility synthesis, manual water, quality and work-completion owners each
   need their own persisted pending-outcome identity and rollback/staged
   publication boundary. Subscribing to their current post-state events would
   be observer-only capture and is explicitly rejected.

`group12-closure.csv` records the per-row blocker. `verify-source-contract.ps1`
is a read-only source audit which passes only while the manifest honestly
matches the known partial implementation and the attack-operation propagation
hardening remains present.
