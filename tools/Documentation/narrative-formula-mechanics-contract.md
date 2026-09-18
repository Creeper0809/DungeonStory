# Narrative Formula Mechanics Contract

Status: approved design, implementation in progress (2026-09-16)

## Scope and authority

This contract applies to newly generated character active/passive/ultimate skills, acquired traits, equipment evolution nodes, and facility lineage modifiers. It does not rewrite previously committed mechanics.

| Contract item | Authority |
|---|---|
| Content definition | Immutable system policy and capability-formula definitions in ScriptableObjects plus an explicit fail-closed capability registry |
| Runtime state | The owning character progression, equipment instance, or facility evolution aggregate; shared definitions are never mutated |
| Command | Each domain's existing generation/selection/commit service; presentation completion is a required stage of the same atomic operation |
| Query | Existing growth/equipment/facility queries read committed instance parameters and `mechanicalDescription`; UI never computes mechanics |
| Identifiers | Stable definition/capability IDs from the authored catalog; runtime instance/node IDs from the owning aggregate; `presentationId` is issued by C# from the pending operation |
| Save | Original facts, per-fact influence use count, formula version, calculated parameters, pending-presentation state and committed presentation are saved; candidate scores and caches are recomputed |
| Dependencies | Formula core has no LLM or Unity-object dependency; domain adapters translate authoritative ledger records; presentation providers consume a closed presentation request |
| Failure | Missing formula metadata, illegal/duplicate capability, non-finite value, budget overflow, unknown evidence, forbidden LLM field or stale presentation ID fails closed |
| Migration | Existing committed fixed values load as `formulaVersion=0`; legacy numeric variants remain read-only load support and are not used for new generation |
| Verification | Formula monotonicity/soft-cap/decay, quantized maximality, deterministic ranking, distinct active cores, failure/cancel/save atomicity, forbidden LLM fields and legacy round-trip |

## Narrative strength and influence use

Only records already committed by C# may enter a formula request. Every evidence record exposes stable diversity keys, attained milestone count, an authored importance score and its committed `influenceUseCount`.

```text
reuseMultiplier = 1 / (1 + influenceUseCount)

D = sum of the reuse-adjusted first contribution for distinct event-group,
    action, relationship and domain keys
M = sum of reuse-adjusted attained milestone weights
I = sum of reuse-adjusted C#-authored importance points

R = 0.40 * D + 0.35 * M + 0.25 * I
P = 1 - exp(-R / softCapK)
budget = baseBudget + floor(powerScale * P)
```

`baseBudget`, `powerScale`, `softCapK`, milestone weights and the allowed importance range are owned by the system policy ScriptableObject. All inputs and intermediate values must be finite and non-negative. `softCapK` must be positive. Formula results are deterministic and culture-independent.

The original narrative ledger is never deleted or reduced. Candidate generation reads but does not mutate influence counts. Exactly the evidence IDs frozen into the final committed result increment their use counts once. Failed generation, cancelled choice, rejected restore, failed presentation and `AwaitingNarrativeRetry` do not increment them.

## Capability formulas and optimization

### Inseparable drawback credit

An authored drawback may increase only the spendable positive-cost envelope of
the same generated instance. It never changes narrative strength and never
creates a global, persistent, or transferable budget.

```text
netCost = positiveCost - acceptedDrawbackCredit
netCost <= narrativeBudget
```

The system policy owns a player-choice fraction cap, a stricter automatic
fraction cap, an absolute cap, and the automatic negative-evidence gate.
Unreachable, optional, separately removable, toggleable, or direct
benefit-cancelling drawbacks fail closed. Automatic credit is zero without a
C#-recorded negative event; the drawback may still exist but cannot finance a
larger benefit. The committed instance persists positive cost, accepted credit,
net cost, drawback ID, original narrative budget, and evidence qualification.
Removal or deactivation always removes the paired benefit and drawback in the
same aggregate operation.

Each generatable capability descriptor must register all of the following:

- stable capability ID and supported domain/target/trigger contracts;
- magnitude, duration, count and target-count ranges with positive quantization steps;
- cost curves for magnitude, duration, count, target, trigger frequency and certainty;
- area/multi-effect and declared pair-synergy costs;
- narrative affinities, conflict groups and forbidden synergies;
- mechanical formatter and runtime applicator for the generated parameter envelope.

Registration is explicit and deterministic. Runtime reflection discovery, content-name switches and nearest/default capability fallback are forbidden. A descriptor missing any required range, quantum, affinity, conflict or formatter/applicator fails authoring validation and cannot participate in generation.

The optimizer enumerates bounded quantized parameter states, rejects every state whose total calculated cost exceeds the narrative budget, and maximizes this score in order:

1. narrative affinity;
2. budget utilization;
3. novelty against already committed effects;
4. canonical capability/parameter signature.

Unused budget is valid only when no legal single quantum can be added to any selected axis. Candidates within `0.10` of the best normalized score are retained, at most three. The automatic winner uses a fixed seed derived from persistent owner ID, manifestation/evolution position, formula version and catalog SHA-256. Active skill generation exposes exactly three candidates with distinct core capability IDs; facility evolution exposes the rule ranking for player choice. Passive, ultimate, acquired trait and equipment evolution commit the deterministic automatic winner.

## Per-domain persistent values

| Domain | Mutable generated values |
|---|---|
| Character skill | Skill instance capability parameter envelopes, calculated cost, evidence IDs and formula version |
| Acquired trait | Acquired-trait instance effect overrides, calculated cost, evidence IDs and formula version |
| Equipment evolution | Equipment evolution node parameter envelopes, calculated cost, evidence IDs and formula version |
| Facility evolution | Facility instance lineage modifier parameter envelopes, calculated cost, evidence IDs and formula version |

The generated envelope is canonical data, not a pointer to a mutable ScriptableObject. Existing runtime consumers must use generated values when `formulaVersion>0` and legacy variant values only when `formulaVersion=0`. Save restore validates the exact capability, units, ranges, cost and catalog/formula version before publishing live state.

## Presentation-only LLM contract

C# freezes mechanics before an LLM request and supplies:

- `presentationId`;
- public narrative facts and the exact evidence IDs already chosen by C#;
- `mechanicalDescription`, generated entirely by C# from committed units and conditions;
- style/length constraints.

The response schema is closed and contains exactly:

```json
{
  "presentationId": "...",
  "displayName": "...",
  "narrativeFlavor": "..."
}
```

Any additional key, including a combination/candidate/recipe/effect/tag ID, selected index, number, cost or mechanical rule, rejects the response. The LLM never selects or changes mechanics. It may only name the frozen result and explain how the supplied evidence led to it without inventing a fact.

Generated mechanics first enter `PresentationPending`. After a valid matching response, one domain command atomically publishes effects, costs, influence-use increments, acquisition/evolution history and presentation text. After five failed presentation attempts the operation becomes `AwaitingNarrativeRetry`; no effect, cost or influence use is committed and no hidden display fallback is substituted. Retry resumes the same frozen mechanics and `presentationId`, so reopening or restoring cannot reroll the result.

## Compatibility and rollout

- Previously committed fixed mechanics keep their exact values and load with `formulaVersion=0`.
- Legacy variant assets and codecs remain only for old-save reads; new-generation writers must reject variant authority.
- Formula/catalog versions and source commit are included in the Unity export with a canonical SHA-256.
- The current AI corpus and rejected SFT are not promoted. A fresh export, fresh 100-row full review and explicit human approval are required before the 1,000-row reservoir or 50,000-row build resumes.

## Implementation order

1. Shared pure formula core and contract tests.
2. CharacterSkill vertical slice through save and player choice.
3. AcquiredTrait automatic manifestation and erasure interaction.
4. Equipment evolution automatic mechanic selection.
5. Facility evolution ranked player selection.
6. Presentation-only providers, catalog export, AI consumer and fresh pilot.

No Unity MCP or `dungeon-player` connection is part of this workflow. Verification uses the official Unity CLI only after all source writers are frozen.
