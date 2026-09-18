# WIM-021/022 medical effects and WIM-028 toxicity implementation evidence

Status: WIM-021/022 and WIM-028 code, targeted asset/localization publication, shared compile, and focused PlayMode verification are complete.

## Approved bounded contract

| Item | Completed-surgery effect | Preserved state | Eligibility |
|---|---|---|---|
| WIM-021 blood transfusion | Existing selected-node HP `+14`, infection `-2`; existing character body command reduces current `bloodLoss` by `25`, clamped at zero. | Does not stop any wound/node bleeding. No new body/save state. | Live compatible character with positive current blood loss; wildlife and constructs rejected. |
| WIM-022 emergency suture | Existing selected-node HP `+8`, infection `-8`; selected node `bleedingPerSecond = 0`. | Other nodes and already accumulated body blood loss are unchanged. | Existing living character/wildlife exact-node rules. |

The root baseline now records `balance:wim:021-022:transfusion-targeted-hemostasis`. Research, facility, physical BOM, work units, procedure risk, RNG/outcome rates, and medicine definitions are unchanged.

## Authority and execution

- `SurgeryOrderPlanningService` resolves the live character/wildlife species and runtime anatomy profile instead of trusting order text. It preflights typed effects during planning; `SurgeryRuntime` repeats the same eligibility at reservation and immediately before `resultRolled`.
- `RecoverBloodLossEffectHandler` is character-only, requires positive blood loss and a finite positive authored amount, and calls `ICharacterBodyHealthCommand.ApplyTreatment(character, 0f, 25f)`. That existing command changes blood loss only when the health argument is zero; it neither invokes `Stabilize` nor edits node bleeding.
- `StopSurgicalNodeBleedingEffectHandler` calls exact-node `TryStopBleeding` on `IAnatomyHealthRuntime` or its wildlife counterpart. Character authority zeros only the selected anatomy node and synchronizes that node's mapped legacy surface projection.
- A valid no-op selected-node heal is accepted by the surgery effect handler. This lets a full-health, zero-infection transfusion target proceed to the blood-loss effect and lets a bleed-only suture complete.
- The existing `SurgeryOrder.resultRolled` aggregate/save field remains the sole replay fence. No save schema, duplicate state, content-ID dispatch, or whole-body hemostasis path was added.

## Authored delta and publication

Current assets were compared to `SurgeryContentAssetBuilder`: IDs, labels/descriptions, family/urgency, research, facility tags, WU, BOM, difficulty/infection/bleeding risk, anesthesia/restraint, living/corpse flags, and existing heal values match exactly.

Only these deltas are intended:

- `Assets/Resources/SO/Medical/Procedures/procedure_emergency-suture.asset`: append one `StopSurgicalNodeBleedingEffect`; keep wildlife enabled.
- `Assets/Resources/SO/Medical/Procedures/procedure_blood-transfusion.asset`: append `RecoverBloodLossEffect(amount=25)` and set `allowsWildlife=false`.

Sole operator command:

- Menu: `DungeonStory/Content/WIM/Apply WIM-021-022 Medical Effects`
- Static method: `SurgeryContentAssetBuilder.ApplyWim021022MedicalEffects()`

The targeted builder loads/builds/validates exactly those two paths and force-reserializes no other asset.

## Focused production verification

Invocation:

- Menu: `DungeonStory/Debug/Medical/Request WIM-021-022 Verification`
- Static method: `WimTransfusionTargetedHemostasisDebugScenarios.RequestRunFromMenu()`
- Report: `Artifacts/QA/wim-implementation/wim-021-022-medical-effects.txt`

The single gameplay session uses the registered production container, `ISurgeryCommandService.TrySchedule`, `ISurgeryWorkCommand.TryReserveWork`/`ApplyWork`, the real surgery tick/recovery path, physical facility-buffer inputs, and `IDungeonGameSaveService`—not direct effect-handler calls. The fixture marks the patient admitted, seeds exact facility buffers, and forces the final success risk after reaching the pre-completion boundary; it does not claim natural surgeon-AI or haul-delivery coverage.

It verifies:

1. Pre-completion transfusion cancellation changes neither blood loss nor wound bleeding.
2. Completed suture zeros only the target node; another node and existing blood loss remain invariant; HP/infection remain `+8/-8`. The live Slime fixture uses the real unmapped `sensory-gel`/`pseudopods` nodes, so legacy projection is explicitly reported not applicable rather than fabricated.
3. Completed transfusion at full target-node HP still reduces blood loss `40 -> 15`, preserves wound bleeding, and preserves the valid no-op HP/infection state.
4. A saved `Recovering` order with `resultRolled=true` restores and reaches `Completed` without repeating the 25-point effect.
5. Low blood loss clamps `10 -> 0`; zero-blood, wildlife, construct, and spoofed-species schedules are rejected without creating an active order.

Unity result: **PASS**. Shared compile and final Console were `0 errors / 0 warnings`; the focused report ended `RESULT=PASS; failures=0; capturedErrors=0`. The fixture used the authored E01 fuel torch through physical warehouse stock/refuel and measured the actual surgery cell at `20°C`, air `100`, light `75`; this is test setup, not natural clinic or balance certification.

Report: `Artifacts/QA/wim-implementation/wim-021-022-medical-effects.txt` (SHA-256 `ABBDB4F2F2807C2BB3055E672C124AE7D575F6B6C9DE276C2BDD7FEF2A72B8CA`).

## WIM-028 implemented and verified contract

- `CharacterToxicityPolicy` is the single numeric authority: confirmed overdose `+30`, natural recovery `30` per operating day, burden clamp `0..100`, and one linear work/combat penalty contribution up to `20%`. Existing overdose damage, mood `-12/300s`, substance state, and antiparasitic/ordinary Treat behavior remain in their existing routes; no DOT or free treatment HP was added.
- `CharacterConsumablesRuntime` owns one toxicity state per character. Its strict current-format V9 payload validates finite bounded toxicity, day watermark, completed operation, active detox plan, exact authored medicine magnitude, and exact physical pending receipt. Invalid candidates do not mutate live state; there is no migration or parallel save authority.
- Detox selects only a physically stored item with `MedicineItemFeature.DetoxReduction > 0`. The authored `medicine:antidote` remains unchanged at `30`; Biological stock and ordinary injury medicines are not fallback detox inputs.
- `SurvivalWorkExecutionHandler` completes actual Treat through the typed SurvivalFood bridge and marks completion effects already applied, so the generic facility dispatcher cannot apply it again. Health/disease candidates retain priority; toxicity candidates are ordered by burden then character ID.
- Durable detox identity is `consumable-operation:detox:<ServiceSessionSnapshot.SessionId>`. The active service session and actor/facility detox plan are the save authority across receipt retry/restore. `AbilityWork.activeWorkRunId` is only an in-memory work-run counter; `SurvivalFoodRuntime.detoxOperationByWorkOwner` correlates same-live-work replay to the durable service operation and is cleared only after successful SurvivalFood restore publication. It is never serialized or used as a durable operation key.
- Physical commit precedes the toxicity delta. The delta is recorded once in the completed-operation ledger, then the exact receipt and service session are acknowledged. A pending actor cannot acquire a second facility plan; committed retries do not cancel/switch the patient; the same live work replay consumes zero; restore resumes the saved service operation without overwriting concurrent toxicity changes.
- Character health UI projects `toxicity / 100`, the actual post-clamp penalty contribution, and localized available/pending/not-needed/unauthored/unavailable treatment state. `CharacterConsumablesPolicyRules.DefaultDietPolicy` exposes the unchanged canonical `Free` default for preparation UI.

The sole operator synchronized the existing Character Summary UI tables with `CharacterSummaryUiLocalizationAssetBuilder.Synchronize()` (menu `Tools/DungeonStory/Content/Update Character Summary UI Localization`). No medicine or procedure asset publication was required; the authored antidote remained unchanged at `DetoxReduction=30`.

Focused invocation: `SurvivalDebugScenarios.RunWim028ToxicityFocused()` (menu `DungeonStory/Debug/Survival/Run WIM-028 Toxicity Verification`). The live-panel observation used the existing fast-start party fixture to establish an actual registered production character before opening the main Character Summary.

The focused verifier covers deterministic confirmed/duplicate/normal substance outcomes; cap/zero/day recovery; one penalty application for both work and combat; the actual live main `CharacterSummaryInfo` health panel rendering toxicity `100`, the post-clamp `20%` contribution, and localized `detox-medicine-unavailable`; pre-completion cancellation; exact one-antidote physical commit; cross-facility rejection; saved pending service/plan resume; delta-once receipt acknowledgement; same-live-work replay; restore cache invalidation; and unchanged HP. It is a focused production-bridge fixture, not evidence of natural AI scheduling, hauling, or treatment risk/balance.

Unity result: **PASS**. Shared compile and final Console were `0 errors / 0 warnings`; the focused report ended `failures=0`. Report: `Artifacts/QA/wim-implementation/wim-028-toxicity-antidote.txt` (SHA-256 `767B510902DDF11953696FDE50EC6719AC4B0AC55EB222616D57D848F0621D09`).
