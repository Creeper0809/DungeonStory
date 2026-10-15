# Unused C# type/file audit (read-only)

Date: 2026-10-15  
Scope: first-party `Assets/Scripts` runtime and Editor C# only. No Unity run, source/asset change, index rebuild, or plan-file change was performed.

## Coverage and evidence

- Read all 3,717 first-party C# files under `Assets/Scripts` (2,716 outside `Editor`, 1,001 under `Editor`) plus the 18 files under `Assets/Tests` for identifier/reference checks.
- Parsed 15,159 class/struct/interface/enum/record declarations. 312 type names occurred only at their declaration (72 runtime-path, 240 Editor-path); extension containers, attribute short names, Unity callbacks, NUnit methods, and Editor menu methods were then excluded from dead-type conclusions.
- Reused `assembly-migration-plan.json`: 1,949 default-assembly files, 16,480 semantic edges, 77 files with no semantic ingress/boundary ingress. The report is useful for default-assembly edges only; it does not cover every asmdef assembly and is not by itself a dead-code proof.
- The required knowledge-base queries for `ObsoleteAttribute`, `NotImplementedException`, `empty MonoBehaviour`, and `legacy` returned no rows, so freshness was unconfirmed and no generated index was used as proof.
- Checked exact C# names, VContainer registrations, reflection/string occurrences, `-executeMethod` tooling/workflow references, Unity entry attributes, and script-meta GUID references in repository assets for the candidates below.

## High-confidence deletion candidates

| Candidate | Location | Evidence |
|---|---|---|
| Empty file/type `WarehousePhysicalStockOperations` | `Assets/Scripts/Services/Items/WarehousePhysicalStockOperations.cs:1-8` (+ `.meta`) | Empty static shell whose comment says the old count/category path was removed. Exact name occurs only at declaration. Semantic graph: zero incoming, zero boundary ingress, zero outgoing, zero boundary egress. Script GUID `5c5cb8e0db4553c46b96f08e88e3cadc` has zero non-meta asset references. |
| Empty component `Shopkeeper` | `Assets/Scripts/Services/Character/Core/Shopkeeper.cs:1-15` (+ `.meta`) | Empty `CharacterActor` subclass. No C# caller, `AddComponent`, type string, DI registration, or exact-name reference. Semantic graph has zero ingress. Script GUID `1a96ce4f5cd02ac439c0ee732edc1b56` has zero non-meta asset references. |
| Unused identity-pressure abstraction and wrapper | `Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionIdentity.cs:6-9`, `:132-138` | `IFacilityIdentityPressureProvider` occurs only at its declaration and the one implementation; `DefaultFacilityIdentityPressureProvider` occurs only at its declaration. Live code calls `FacilityIdentityPressureUtility` directly (`RoomProfile`, `FacilityEvolutionService`, runtime/mutations). No DI registration or reflection/string reference. Remove both interface and wrapper, retain utility. |
| Superseded record provider | `Assets/Scripts/Services/FacilityEvolution/FacilityEvolutionRecord.cs:237-250` | `ComponentFacilityEvolutionRecordProvider` occurs only at declaration. Production composition registers `FacilityEvolutionRecordComponentService` as both `IFacilityEvolutionRecordComponentService` and `IFacilityEvolutionRecordProvider` (`DungeonFacilityRegistration.cs:46-49`); tests also construct that service. No DI/reflection/string reference to the old provider. |
| Unused save-codec type | `Assets/Scripts/Services/Items/PhysicalItemGameplayOutcomeTransaction.cs:129-187` | `PhysicalGameplayOutcomeSaveCodec` occurs only at declaration. Its four methods have no qualified or static-import call path; current save handling uses other codecs and `WorldItemRepository` cloning. This removes only the codec container, not `PhysicalGameplayOutcomeAttachment` or its DTO. |
| Unused private fixture | `Assets/Scripts/Services/Economy/Diagnostics/ProductionOutputDestinationLifecycleDebugScenarios.cs:1618-1702` | `RecordingMutationFence` occurs only at declaration; no construction or reflection/string reference. |
| Unused private fixture | `Assets/Scripts/Services/Infrastructure/Exterior/Editor/ExteriorActivityDebugScenarios.cs:1893-1943` | `FailOnceAfterExteriorSaveSection` occurs only at declaration; no construction or reflection/string reference. |
| Unused private fixture | `Assets/Scripts/Services/Character/AI/Editor/V25NarrativeInferenceDebugScenarios.cs:5288-5474` | `StableCatalogSnapshotProvider` occurs only at declaration; no construction or reflection/string reference. |
| Unused private fixture | `Assets/Scripts/Services/Offense/Editor/OffenseRewardDebugScenarios.cs:2529-2533` | Empty `EmptyRunResultPanelService`; exact name occurs only at declaration. |

## Deletion candidates needing one policy decision

| Candidate | Location | Why static evidence says unused | Why not unconditional |
|---|---|---|---|
| `UIGridTab` | `Assets/Scripts/Models/Grid/Core/UIGridTab.cs:6-41` (+ `.meta`) | Exact name only at declaration; no C# construction/call; script GUID `6d1c5bf520774d347b5364249fd5ee19` has zero non-meta asset references. | Has `[MovedFrom(sourceAssembly: "Assembly-CSharp")]` and `[FormerlySerializedAs]`. Safe for current repository assets, but retain if external AssetBundles or save-time type metadata outside the repository are supported. |
| `WimPowerFlowDebugScenarios` | `Assets/Scripts/Services/Infrastructure/Industrial/WimPowerFlowDebugScenarios.cs:9` (whole file) | `#if UNITY_EDITOR`; no MenuItem/NUnit/initializer, C# caller, tool/workflow `-executeMethod`, or exact-name string outside generated indexes. Semantic graph has zero ingress. | Removes an ad-hoc QA report generator; confirm old WIM verification artifacts are historical and repeatability is no longer required. |
| `WimWetVenueWaterDebugContract` | `Assets/Scripts/Services/Infrastructure/Industrial/WimWetVenueWaterDebugContract.cs:12` (whole file) | Same unregistered/manual-only pattern; zero semantic ingress and no tool/workflow reference. | Removes an ad-hoc focused witness and its documented report path. |
| `CharacterProficiencyDebugHarness` | `Assets/Scripts/Services/Character/CharacterProficiencyDebugHarness.cs:5` (whole file) | No caller, Unity/NUnit entry, DI registration, tool `-executeMethod`, or exact-name string in executable tooling; semantic graph has zero ingress. | `docs/game-design/whole-game-balance-baseline.md:2080` still cites it as validation evidence. Delete only with a replacement verifier and doc update, or move it to Editor tests. |

There are 52 additional Editor-path static scenario/exporter classes whose class names have no C# reference, no Unity/NUnit entry attribute, and no current tool or `docs/dev/unity-workflow.md` reference. They are not classified as unconditional deletions because public static methods can still be invoked manually through Unity `-executeMethod`, and many preserve past QA reproducibility. Representative files are `Wim019WildlifeMigrationDebugScenarios.cs:11`, `CampaignPresentationObserverDebugScenarios.cs:13`, `NaturalRunRuntimeDebugProbe.cs:7`, `TradeInventoryOutcomeBatchRunner.cs:6`, `MigratedProducerExecutionSnapshotExporter.cs:13`, and `OriginalSkillProseNativeBatchRunner.cs:19`. Treat this as a separate QA-retention cleanup: establish the supported execute-method allowlist, then delete the remainder as one Editor-only batch.

## False positives explicitly rejected / preserve

- `DefenseGuardAttackEffectSO` has no C# caller but is live serialized content: GUID `c2fad375c4cf1f64e86e86d432687ccd` is referenced by `P1_StormFireTrap_4_GuardAttack.asset` and `P1_AlarmCoil_3_GuardAttack.asset`; it also has `CreateAssetMenu`.
- `DungeonPlayerAutomationSceneBootstrap` is invoked by `[RuntimeInitializeOnLoadMethod]`; its name correctly has no ordinary caller.
- Runtime registration and extension containers whose type names occur once are live through their member calls. Examples include `GameplayDomainFactRegistration`, all gameplay-outcome `*Registration` classes, `GameEventBusInfoFeedExtensions`, `EventAlertPresentationText`, `RunVariablePresentationText`, `ProductionWorkshopAbilityAccessors`, `FacilityShopUnityUnlockAdapter`, and `BuildingCharacterFacilityAdapter`.
- Attribute types such as `RuntimeRebuildableCacheAttribute`, `ApplicationAdapterTransientStateAttribute`, and the `Balance*Attribute` types are live through C#'s shortened attribute spelling and analyzer lookup.
- `CombatDamageOutcomeBridge` (`ProductionCombatOutcomeBridges.cs:6`) currently has no C# caller/registration and zero semantic ingress, but `task_plan.md:434` and `HANDOFF.md:345` explicitly name it as the next active integration target. Preserve while that authorized work remains active; reassess only after the combat outcome migration is completed or cancelled.
- MenuItem, CreateAssetMenu, InitializeOnLoad/RuntimeInitializeOnLoad, NUnit/UnityTest, MonoBehaviour/ScriptableObject serialization, VContainer generic registrations, and explicit tool `-executeMethod` paths were not counted as dead merely because they lack ordinary callers.

## Limits

- No Unity import/compile/Play was run, so deletion candidates still require the main agent's source edit followed by current-source compile verification in the designated isolated Unity window.
- The repository may have external AssetBundles, user-authored scenes, or external automation not represented in versioned files. Only `UIGridTab` has an explicit migration/serialization compatibility marker among the candidates above.
- Static absence cannot disprove arbitrary reflection assembled from concatenated strings. The audit found no broad runtime assembly scan that would activate the high-confidence candidates; Editor reflection observed was targeted to named types/methods.

Status: READY/FROZEN (audit only; no source or asset edits).
