# Narrative Formula LLM Module Selection Contract

## Decision

For formula-generated character skills, acquired traits, equipment evolution, and
facility evolution, C# remains the only authority for eligibility, legality, costs,
numeric ranges, drawback credit, quantization, persistence, and effect application.
The LLM selects a subset of the individually offered authored modules because matching
concrete life history to semantic mechanics is a language judgment. It never supplies
numeric values, costs, arbitrary module IDs, or effects.

The previous formula path, which selected and numerically froze a complete composition
before the LLM request, is superseded for newly created formula versions. Existing
committed saves remain load-only and retain their exact frozen mechanics.

## Authority and state contract

| Concern | Sole authority |
|---|---|
| Immutable module definitions, polarity, descriptions, conflicts, required companions, numeric formula/ranges and applicators | System-specific ScriptableObject catalog |
| Narrative evidence, influence-use count and event context | Existing system ledger/aggregate |
| Individually eligible offered modules | C# offer builder |
| Semantic module subset and cited evidence IDs | Validated LLM response |
| Combination legality and feasibility | C# selection validator |
| Positive magnitudes, drawback severity, accepted local credit and quantization | `NarrativeFormulaCore` |
| Pending and committed mutable state | Owning character/equipment/facility aggregate |
| Display name and narrative flavor | Validated LLM response |
| Mechanical description | C# formatter from frozen numeric allocation |

Shared ScriptableObject assets are never mutated at runtime. Pending offers, selections,
numeric allocations, and presentation text are instance-local runtime/save data.

## Command flow

1. The owning aggregate calculates narrative strength and base budget from its real ledger.
2. C# enumerates every **individually eligible** positive and drawback module for the
   current profile. Hard-ineligible modules are omitted for explicit reasons only:
   unavailable domain/target/trigger, active conflict, missing formatter/applicator or
   formula metadata, unreachable drawback, or unsupported system capability.
3. C# persists a hash-bound `ModuleSelectionPending` offer before dispatch. The offer
   includes module ID, polarity, player-facing semantic description, affinity metadata,
   conflict groups, required companion IDs/groups, minimum feasibility cost, and the
   exact offered evidence IDs. It does not contain a preselected combination or final
   numeric allocation.
4. The LLM returns exactly the pending selection ID, one to three distinct positive
   module IDs, zero to the profile limit of distinct drawback IDs, cited offered evidence
   IDs, display name, and narrative flavor. It returns no numeric/cost/effect fields.
5. C# rejects unknown or non-offered IDs, duplicates, negative-only results, unsupported
   counts, conflicts, missing required companions, uncited/forged evidence, or selections
   whose minimum quantized state is infeasible under the bounded budget policy.
6. After validation, C# jointly solves numeric positive allocations and selected drawback
   severity. Drawback credit is derived from the solved mandatory severity, is local to
   this instance, and remains capped by the authored profile policy.
7. C# creates the mechanical description and atomically commits the selected IDs, numeric
   envelopes, accepted credit, display text, evidence influence-use increments, cost and
   acquisition/evolution record.

## Closed response schema

```json
{
  "selectionId": "module-selection:<profile>:<sha256>",
  "positiveModuleIds": ["authored-positive-module-id"],
  "drawbackModuleIds": [],
  "evidenceFactIds": ["offered-fact-id"],
  "displayName": "한국어 이름",
  "narrativeFlavor": "제공된 사건이 선택 기능으로 이어진 설명"
}
```

All six keys are required and additional keys are rejected. `positiveModuleIds` must
contain at least one item. A drawback-only response is invalid and cannot be repaired by
silently adding a positive module. Numeric tokens or mechanical values in the response
are rejected rather than treated as authority.

## Numeric and drawback constraints

Let `B` be the C# narrative base budget, `P(x)` the cost of the selected positive
allocation, `D(y)` the authored cost/severity of selected mandatory drawbacks, and
`Credit(D(y))` the profile policy's bounded accepted credit.

```text
P(x) <= B + Credit(D(y))
Credit(D(y)) <= floor(B * profileCreditFraction)
Credit(D(y)) <= absoluteCreditCap
P(x) >= minimum cost of every selected positive module
D(y) >= minimum severity of every selected drawback module
```

The optimizer searches only the selected module identities. It maximizes legal budget
utilization and narrative affinity under quantization, and accepts the result only when
no allowed parameter can take one additional quantum. A drawback that is optional,
unreachable, separately removable, a direct cancellation of a selected benefit, or not
supported by a real consumer receives no offer. Automatic profiles still require
qualified negative ledger evidence when the authored policy says so.

## Persistence and atomicity

The pending state stores the selection ID, profile ID, owner/manifestation identity,
formula version, catalog SHA-256, base budget, offered module descriptors, offered fact
IDs, offer hash, failure count, and last failure reason. It does not apply effects or
increment influence-use counts.

The committed state stores the validated selected module IDs, exact numeric envelopes,
positive cost, requested/accepted drawback credit, net cost, evidence IDs, formula
version/catalog hash, display text, mechanical description, and audit receipt.

Invalid responses, five exhausted retries, cancellation, stale callbacks, catalog/hash
changes, or numeric infeasibility consume no item, cost, cooldown, progression state, or
influence-use count. They remain explicit retry/failure states; no first-module or
rule-ranked hidden fallback is permitted.

## Profile policies

- Character active skill: three independently selected/frozen results are offered to the
  player and must have distinct primary positive modules.
- Character passive/ultimate: one validated selection is automatically committed after
  numeric allocation; no C# semantic winner replaces the LLM selection.
- Acquired trait: one validated selection is automatically committed. At least one
  benefit is mandatory; selected drawbacks must have qualified evidence and be erased
  atomically with the trait.
- Equipment evolution: one validated selection is automatically committed from offered
  equipment modules. A model failure is recorded as failure, not first-candidate success.
- Facility evolution: the validated selected modules become the player-visible proposal;
  final affordability, current facility state, cost, and player confirmation remain C#.

## Compatibility and transition

- Existing committed `formulaVersion=0/1/2` instances keep their exact saved mechanics.
- Old pending presentation-only operations are not reinterpreted as module-selection
  responses. They remain retryable only through their original versioned contract or are
  explicitly superseded without consuming state.
- The new path uses a new formula/request schema version and catalog hash.
- No existing fixed variant definition is deleted; legacy loading and validation remain.

## Validation matrix

- Complete offered-module enumeration and stable hash/order.
- A newly authored compatible module appears without core ID-specific branching.
- Unknown, omitted, duplicate, non-offered, negative-only, over-count, conflict, missing
  companion and forged evidence selections are rejected.
- Minimum-cost infeasibility and optimizer state overflow fail closed.
- Drawback severity produces bounded credit and cannot be separated from benefits.
- Same offer and response produce the same numbers; different selected subsets can produce
  different allocations.
- Failure/cancel/stale callback/save round-trip never spends state before commit.
- Legacy committed saves preserve exact effects and values.
- Each of the four profiles reaches its real runtime consumer and exposes a C# mechanical
  description plus LLM display text.
- Export includes schema/formula version, game commit, catalog SHA-256, module definitions,
  offer/selection examples, and source closure hashes.

## Balance status

`밸런스 기준 배정`. This contract changes who makes the semantic module choice but does
not loosen the existing narrative budget, quantization, soft cap, reuse decay, drawback
credit caps, or applicator legality. Formula regression and representative gameplay
verification are required before claiming `밸런스 공식 검증`.
