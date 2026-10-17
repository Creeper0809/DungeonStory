# Unseen naming evaluation export — frozen structure contract

Date: 2026-09-15  
Batch: `UNSEEN-NAMING-EVAL-100-V1`  
Authority base: current v26 catalog and adapter3 public meaning contract

## Scope and completion line

Create one new immutable export with 100 controlled positive evaluation scenarios:
CharacterSkill 20, AcquiredTrait 20, FacilityEvolution 15, EquipmentChoice 15,
EvolutionHistory 15, Persona 15. The existing calibration positives remain the
default source. All 15 negative rejection contracts remain regressions: 14 remain
byte-semantic exact, while the one Persona extra-key rejection may gain only the
same six authoritative current-need public facts required by the V2 production
projection. No model inference, response generation, answer key, human approval,
training eligibility, gameplay asset value, runtime effect, or NarrativeAI write is
part of this batch.

## Structure contract

| Contract | Frozen decision |
|---|---|
| Content definition | Editor-only typed scenario plans/rows in the existing six C# scenario sources. Each new row owns a stable scenario ID and immutable evaluation metadata. Authored gameplay ScriptableObjects remain the final source for legal skills, traits, recipes, effects, species, and mechanics. |
| Runtime state | Existing controlled fixture aggregates remain the only mutable state: `CharacterProgression`/`CharacterNarrativeLedger`, acquired-trait aggregate, `RoomProfile`/facility evolution state, `UsageLedger`, evolution request snapshots, and `CustomerPersonaRuntime`. No new gameplay state is introduced. |
| Command/producer | Existing production producers only: CharacterSkill draft/catalog/prompt; acquired-trait packet authority; facility candidate/prompt formatter; equipment/history rule/request factories; Persona runtime/prompt builder. Scenario code may author fixture inputs but never fabricate a legal packet. |
| Query/consumer | Existing public material builders, legal candidate enumerators, response validators, and scenario serializer. The evaluation provider replaces only the positive bundle supplied to the exporter. |
| Identifiers | New IDs use an `unseen-eval-v1` token. `scenarioFamilyId` is stable. EquipmentChoice and EvolutionHistory views of the same ledger row share one family. Every row uses split `unseen-evaluation`; related variants may not cross splits. |
| Persistence | No gameplay save change. Scenario evaluation metadata and `novelty_review.json` are delivery-only, excluded from catalog JSON, catalogHash semantics, model input, and training input. They are protected by delivery hashes. |
| Dependencies | New evaluation provider -> existing catalog provider and six scenario sources -> existing production authorities. Exporter may serialize an optional snapshot-owned novelty review and request-owned raw test results. NarrativeAI is read-only external evidence. |
| Failure policy | Fail loudly on missing/duplicate profile, wrong counts, missing metadata, duplicate scenario/family misuse, unknown nearest calibration row, exact public-story collision, missing legal candidates, validation rejection, participant resolution failure, invalid history generation, or acquired-trait lifecycle inconsistency. No fallback or packet synthesis. |
| Transition | Default `new NarrativeMechanicCatalogUnityAssetSource()` remains v26 calibration behavior. A separate explicit evaluation provider creates the new set. Prior exports and the AI pin are not changed. |
| Validation | Two independent C# captures; all 100 production packets and validators; exact profile and candidate gates; public participant/history/trait checks; exact ID/input/public-story hash comparison against current v26 and the supplied exclusion ledger; 14 exact negative semantics plus one Persona negative with exact rejection mechanics and only the six-need public delta; create-new export; independent file/source/delivery hash audit. |

## Public/internal boundary

- `scenarios_100.json` keeps schemaVersion 2 and the adapter3 allowlist:
  `profileId`, `publicFacts`, and `publicNarrativeContext`, with legal mechanics
  carried by the existing candidate/semantics contract.
- Requests, response fixtures, authority state, original source IDs, validators,
  expected results, evaluation metadata, overlap judgments, and hashes are not
  model input.
- Evaluation metadata is retained separately from scenario semantic JSON so adding
  the audit cannot silently change the v26 public schema.
- `novelty_review.json` is delivery-only. `raw_test_results.json` remains
  delivery-only. Both are hashed by `delivery_manifest.json` and included in the
  independent byte comparison.

## Novelty rubric

Every positive row must provide:

1. `scenarioFamilyId` and split `unseen-evaluation`.
2. One same-profile nearest calibration scenario ID.
3. Decision `distinct-context`.
4. A row-specific reason naming at least two substantive changes among event
   sequence, domain combination, outcome, participant role, generation structure,
   facility operational history, need/species/background interaction, or prior
   active/erased state.

Renaming, reordering identical facts, moving only the date, changing only fixture
IDs, or selecting a different legal candidate from the same public story is not
novel. Exact scenario-ID, inputHash, and public-story hash overlaps must all be zero.
Semantic judgment remains an authored review claim, not a hash inference.

## Per-profile fixture invariants

- CharacterSkill: 8 Active, 7 Passive, 5 Ultimate. Evaluation rows use curated
  multi-event or multi-domain histories; no row is the old single generic ledger
  fact under a new ID. Production draft and full legal combination semantics remain
  authoritative.
- AcquiredTrait: milestones 3/8/20 remain covered. Curated evidence varies event
  sequence/outcomes/domains and, where applicable, prior active/erased state.
  Production score, packet, state, and response validation all run.
- FacilityEvolution: use existing authored recipes and legal mutation tags only.
  Each row supplies a concrete multi-event room history, not a renamed single
  `Controlled scenario signal` token.
- EquipmentChoice: 15 three-event current-generation ledgers with public actor and
  target references; legal candidates stay in the production 2–3 range.
- EvolutionHistory: shares the 15 equipment families, adds at least one strictly
  earlier compacted generation, and keeps all current events in the request
  generation.
- Persona (V1 wording superseded by the V2 delta below): 15 Customer-only
  requests vary more than name alone through species-authored background plus
  six current-needs facts. Locked C# mechanics and facility preferences remain
  host-only, and narrative parsing must not modify mechanics.

## Audit envelope

`novelty_review.json` schemaVersion 1 records the evaluation set ID, supplied
exclusion ledger SHA-256, exclusion source-pilot hash, current calibration count,
profile/split/family counts, exact overlap counts, semantic review totals, and one
row per scenario with family/split/nearest calibration/reason/public-story hash.
Expected family count is 85: 20 + 20 + 15 + 15 shared equipment/history + 15.

## Balance and extension classification

`밸런스 영향 없음`: reviewed `whole-game-balance-baseline.md` section 8 and its
type-specific comparison table. This batch changes only Editor-controlled evaluation
inputs and delivery evidence. It changes no authored BOM, WU, EWU, kg, price,
effect, probability, cooldown, facility capacity, research, combat result, save
schema, or gameplay producer/consumer. New narrative events never grant effects or
resources. Extension type is evaluation `ParameterContent`; gameplay core
content-specific branch count remains 0. Synthetic gameplay canary is N/A because
no gameplay capability is added; the explicit alternate provider is the closed
evaluation-set seam.

## Evidence tiers and explicit NOT_RUN claims

Controlled fixture validation and Unity Editor C# execution do not establish
natural-play distribution, PlayMode UI/AI occurrence, save-roundtrip behavior,
model quality, local-model load, inference, human approval, or training readiness.
Those remain `NOT_RUN`/false in the delivery and completion report.

## Follow-up delta — `UNSEEN-NAMING-EVAL-100-V2`

The independent AI audit of v1 passed delivery/source/mechanical/schema/negative
gates but reopened only 55 model-visible narratives: CharacterSkill 20,
AcquiredTrait 20, and Persona 15. FacilityEvolution, EquipmentChoice, and
EvolutionHistory remain frozen impact-check rows.

### Public contract delta

- Public-context schemaVersion remains 1, scenario schema remains 2, and adapter3
  roots remain exactly `profileId`, `publicFacts`, `publicNarrativeContext`, plus
  each profile's existing legal request/candidate meaning. No new JSON key is
  required.
- CharacterSkill/AcquiredTrait controlled evaluation records gain immutable C#
  publication descriptors keyed by the exact ledger
  `(domain, originalFactId, sourceSubjectId)` tuple. Each descriptor supplies a
  human-readable event sentence, a semantic non-fixture event type, and a public
  target entity where the recorded subject identifies one. The production public
  material builder must reject missing, extra, duplicate, domain-mismatched, or
  target-mismatched descriptors. The ledger remains the authority for actor,
  target ID, outcome, day, count, milestone, and value.
- Persona public material gains six current-need facts derived directly from
  `CharacterActor.Stats`: hunger, sleep, fun, mood, excretion, and hygiene. Missing
  stats are represented as unavailable rather than numeric zero. These facts
  replace the raw-prompt-only representation so live prompt, export, and adapter3
  use one public projection.
- Persona response remains effect-free with exactly `personaName` and
  `flavorText`. Locked multipliers, preferred facility tags, raw prompt, fixture
  input, audit reasons, answers, and validation witnesses remain host-only.

### State, save, failure, and transition

- No serialized gameplay field, save DTO, asset, mechanic value, candidate rule,
  or content effect changes. Ledger-publication descriptors are immutable request
  definitions used only while building the 40 controlled evaluation materials;
  they are not saved and normal runtime calls keep their existing behavior.
- Persona needs are existing authoritative runtime state already consumed by the
  production prompt. The change removes the split representation; it does not add
  a second state authority.
- v1, v26, NarrativeAI, and the user-approved calibration100 stay byte-immutable.
  The corrected package is create-only; scenario/family IDs stay stable because
  these are corrected revisions of the same cases, not renamed substitutes.

### Focused completion evidence

1. Mask scenario/fact/entity IDs, Name, Potential, fixture input, audit metadata,
   and responses from adapter3 input. A name/ID/potential-only Persona mutation
   must produce the same semantic signature and be rejected; every corrected
   Persona must differ from v1/v26/exclusion data through visible needs or other
   authoritative public facts.
2. For all 40 character-history rows, every selected ledger event has a readable
   fact sentence and non-fixture semantic event type after fixture IDs are hidden.
   Actor, target, outcome, day, and count must match ledger evidence, and the exact
   public material instance must bind both live prompt and export.
3. Re-run the 55 affected production captures/validators, compare the other 45
   scenario semantics to v1, preserve counts 20/20/15/15/15/15 and all 15
   rejection contracts (14 exact semantic rows; one Persona row with only the
   authoritative six-need public projection delta), then run
   package/source/schema/delivery/hash gates without model inference.

`밸런스 영향 없음`: the follow-up changes only answer-free narrative observation
of already-authored event/current-need state. It changes no gameplay cost, effect,
probability, schedule, resource, progression, save authority, or AI mechanic.
