# WIM-036 source packet — substance-specific effects

Status: read-only source investigation (2026-09-06). This packet is not an implementation approval, numeric balance decision, or runtime test result.

## Scope and evidence boundary

- Requirement: `docs/game-design/wim-implementation-plan.md` WIM-036.
- Balance gate read: `docs/game-design/whole-game-balance-baseline.md`. No WIM-036-specific numeric approval record was found; this packet does not invent one.
- Historical finding: `Artifacts/QA/wiki-system-audit-2026-09-05/substance-consumption-review.md` and DIFF-036 classify the three descriptions as more specific than the old generic implementation. It is historical evidence only: its consumables-save version is now stale (current direct source is V9).
- KB/catalog output was deliberately not queried or rebuilt. The known KB is stale (`source 0d48b5eb`, KB `d04e6bea`, 117 failures); every fact below is from the current original source.

## Current authored definitions and actual generic result

| Item / authoring source | Intended wording | Current `SubstanceItemFeature` | Actual direct-use result |
| --- | --- | --- | --- |
| `drug:vitality-tonic` — `Assets/Resources/SO/Economy/Items/drug_vitality_tonic.asset` | fatigue reduction | NonAddictive; addiction `0`, overdose `0.006`, tolerance `0`, withdrawal `0`, mood `+2`, work `+0.12`, combat `0`, duration `150s`; gate `research:pharmacology:distillation` | mood plus the all-work aggregate only; no SLEEP/fatigue state is changed. |
| `drug:mana-awakener` — `Assets/Resources/SO/Economy/Items/drug_mana_awakener.asset` | research and arcane amplification | Addictive; addiction `0.11`, overdose `0.05`, tolerance `0.16`, withdrawal/hour `0.025`, mood `+2`, work `+0.18`, combat `+0.08`, duration `180s`; gate `research:pharmacology:stimulants` | `+0.18` reaches every work type, including research; `+0.08` is the generic combat context, not an arcane-only effect. |
| `drug:dreamleaf-analgesic` — `Assets/Resources/SO/Economy/Items/drug_dreamleaf_analgesic.asset` | pain reduction | Addictive; addiction `0.08`, overdose `0.02`, tolerance `0.12`, withdrawal/hour `0.02`, mood `+4`, work `+0.05`, combat `-0.03`, duration `240s`; gate `research:pharmacology:anesthesia` | mood plus generic all-work/combat aggregation; no temporary body-pain or pain-penalty state changes. |

The current narrative source is `docs/game-design/content/item-in-game-descriptions.ko.json` (`drug:*` rows), and gives the same three intended meanings. Those meanings must not be represented as already live while the typed consumer is absent.

## Reusable consumables contract (do not duplicate it)

`ItemDefinitionSO.SubstanceItemFeature` contains only the generic mood, work, combat and duration fields (`Assets/Scripts/Models/Economy/Content/ItemDefinitionSO.cs`). `CharacterConsumablesApplicationAdapters.ToSubstanceSnapshot` maps exactly those fields into `SubstanceDefinitionView`; no typed fatigue/research/arcane/pain field crosses that boundary today.

The following existing contract is sufficient to own one timed use, policy, physical receipt, tolerance, addiction, withdrawal, and retry fence for each item; it should be reused rather than recreated.

- `CharacterConsumablesRuntime.TryConsumeSubstance` creates a physical pending consumption receipt and a `CharacterSubstanceUsePlan`.
- `TryFinalizeSubstanceUsePlan` writes resolved tolerance/addiction/withdrawal/`activeSeconds`, publishes the current effect exactly once, then acknowledges the physical receipt.
- `CharacterConsumablesRuntime.Tick` expires `activeSeconds`, decays tolerance, and accumulates withdrawal after the existing one-game-hour condition.
- `CharacterConsumablesStateRules` clones, validates, captures and restores `CharacterSubstanceState` and active use plans. Current `DungeonCharacterConsumablesSaveData.CurrentVersion` is `9`; the strict section is `CharacterConsumablesSaveSection` (`survival.character-consumables`).

`CharacterSubstanceState` already has `activeSeconds` keyed by character and item definition. A derived typed projection can therefore be active only while that value is positive, without a second timer or a second save field. Adding new persisted effect state is not justified by the current evidence.

## Existing target channels and current gaps

### Vitality tonic — fatigue

The authoritative fatigue state is `CharacterCondition.SLEEP`. `CharacterStats.ApplyWorkNeedDepletion` evaluates `performance:survival:fatigue-rate`; its authored gameplay-effect target is `GameplayEffectTargetIds.FatigueRate`. `CharacterStats.RecoverNeed` is the existing immediate SLEEP recovery command, with recovery source semantics.

These are different effects. The current `+0.12` generic work aggregate is not a fatigue-rate multiplier or a SLEEP restoration amount. Therefore an immediate SLEEP recovery value cannot be inferred from it. A temporary active `FatigueRate` projection is feasible through the existing performance channel, but its authored magnitude and whether the tonic should instead restore SLEEP require a decision.

### Mana awakener — research and arcane

`CharacterStatsProjectionService.GetWorkContextMultiplier` applies `GameplayEffectTargetIds.ResearchSpeed` for `BuiltInWorkTypeIds.Research`, then also multiplies the generic substance work aggregate. The current `+0.18` generic field consequently boosts research **and every other work type**; it is not a research-only result.

The direct arcane consumer is `CombatResolutionService` / `OffenseBattleRuntime`, which evaluate `CharacterPerformanceFormulaIds.ArcanePower`. The current live formula asset `Assets/Resources/SO/V27/CharacterPerformance/Formulas/performance_combat_arcane-power.asset` uses gameplay-effect target `character:combat-power`. The historical `arcane:power` effect definition exists, but this current formula does not use it. Consequently, an active `character:combat-power` source would affect non-arcane combat too; it cannot be called arcane-only without an approved consumer correction.

`CharacterTransientGameplayEffectSourceQuery.GetStatusSources` is the existing runtime-status projection seam. It currently supplies only sedation, and `CharacterDerivedStatsSnapshotProjector` already combines those status sources with traits, species, equipment and completed research. It can supply a derived active-substance status without a separate timer, but needs an explicit projection contract and typed authoring values.

### Dreamleaf analgesic — pain, not healing

Body injury is authoritative. `CharacterStatsVitalsService.GetVitals` reads the body-health projection, and `CharacterStatsProjectionService` adds `state:pain` when `InjurySeverity > 0.001`. There is no independent current transient pain-suppression value. Mutating `InjurySeverity` would change the body state and is injury healing, not temporary analgesia.

`MedicineItemFeature.PainReduction` is a different existing metadata channel: `CharacterMedicalRuntime` turns a selected treatment's value into only a mood factor for `180s`, with strength `clamp(painReduction * 0.15, 1, 6)`. It does not reduce injury, body pain state, or a work penalty.

Important direct-source distinction: dreamleaf has **no** `MedicineItemFeature` or `PainReduction` value. `medicine:anesthetic` has `PainReduction: 35` and `SupportsInjuryTreatment: 0`; `CharacterMedicalSupplyCoordinator.TryRequestMedicine` filters candidates to `Kind == Medicine && SupportsInjuryTreatment`, so that standalone anesthetic is not selected by that injury-treatment route. Dreamleaf also has the separate `response:dreamless-sedative` disease-field response (`DiseaseFieldResponseRuntime`: physical item 1, severity reduction 22), which is disease treatment, not its general-use pain effect.

## Minimal next implementation scope after a decision

1. **Authoring/model and consumables projection (one grouped change):** extend `SubstanceItemFeature` and `SubstanceDefinitionView`/adapter snapshot with explicit typed values only for the approved three effects; project an active substance from existing `CharacterSubstanceState.activeSeconds`. Retire the corresponding generic `workSpeedEffect`/`combatEffect` values for these three items so a typed effect and broad aggregate cannot apply together. Preserve the existing physical receipt, duration, tolerance, addiction, withdrawal, overdose, policy and V9 save contract.
2. **Existing downstream channel consumers:** use the existing fatigue-rate, research-speed and approved arcane consumer seam; do not add a new generic multiplier. Implement analgesia as temporary derived suppression only if its exact body-pain/work/combat semantic is approved; do not heal injury merely to clear `state:pain`.
3. **Focused verification:** exercise one physical direct use per item; active-duration expiry; cancellation before receipt (effect/consumption zero); receipt acknowledgement retry (one effect); save/restore while active; and proof that the old all-work/all-combat aggregate is absent for these three items while their approved typed consumer changes once.

## Essential decisions still required

1. **Vitality tonic:** temporary fatigue accumulation reduction, or immediate SLEEP recovery? If recovery, supply its amount; if rate, supply its multiplier. `+0.12` work has different units and is not authority for either.
2. **Mana awakener:** approval is required before reusing the existing generic magnitudes as typed research/arcane values (for example, `+0.18` research and `+0.08` arcane). The present formula's `character:combat-power` target would also affect non-arcane combat; choose whether that is intended or authorize an arcane-only consumer correction. Do not silently reinterpret the old broad bonuses.
3. **Dreamleaf:** choose a temporary suppression semantic and magnitude. The separate `medicine:anesthetic` value `35` is neither authored on dreamleaf nor a general-use body-pain reduction, so it must not be copied without an explicit approval. This effect must leave HP, body parts, injury severity, disease severity and healing unchanged unless another approved system says otherwise.

No gameplay, asset, wiki, generated-content, KB, Unity, or shared-consumables file was modified for this packet.
