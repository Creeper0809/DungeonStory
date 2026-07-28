using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class InstanceEvolutionDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Evolution/Run Instance Evolution Contracts")]
    public static void RunFromMenu()
    {
        if (!RunAll(logSuccess: true))
        {
            Debug.LogError("Instance evolution contracts failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        Run("Raw usage ledger stays bounded", VerifyRawLedgerCapacity, errors);
        Run("Ten thousand generations compact deterministically", VerifyLongRunCompaction, errors);
        Run("Facility candidates are deterministic", VerifyFacilityCandidateDeterminism, errors);
        Run("Room conditions only gate benefits", VerifyRoomActivationContract, errors);
        Run("Catalyst economy and potency scaling stay explicit", VerifyCatalystRules, errors);
        Run("Narrative responses cannot alter locked facts", VerifyNarrativeLock, errors);
        Run("Packed relocation save state preserves construction occupancy", VerifyPackedRelocationSaveState, errors);
        Run("Packed relocation destruction releases construction occupancy", VerifyPackedRelocationDestruction, errors);
        Run("World save version two migrates without losing buildings", VerifyWorldSaveV2Migration, errors);

        if (errors.Count > 0)
        {
            Debug.LogError(
                $"InstanceEvolutionDebugScenarios failed:\n{string.Join("\n", errors)}");
            return false;
        }

        if (logSuccess)
        {
            Debug.Log("InstanceEvolutionDebugScenarios passed.");
        }

        return true;
    }

    private static void Run(
        string name,
        Action scenario,
        ICollection<string> errors)
    {
        try
        {
            scenario();
        }
        catch (Exception exception)
        {
            errors.Add(
                $"- {name}: {exception.GetType().Name} {exception.Message}");
        }
    }

    private static void VerifyRawLedgerCapacity()
    {
        UsageLedger ledger = new UsageLedger();
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();
        for (int index = 0; index < 256; index++)
        {
            compactor.Record(
                ledger,
                "work.completed",
                index + 1,
                "worker:test",
                "facility:test",
                new[] { "production" });
        }

        Require(
            ledger.currentGenerationEvents.Count == UsageLedger.RawEventCapacity,
            $"raw events={ledger.currentGenerationEvents.Count}");
        Require(
            ledger.currentGenerationEvents[0].sequence == 129,
            $"oldest sequence={ledger.currentGenerationEvents[0].sequence}");
        Require(
            ledger.currentGenerationEvents[^1].sequence == 256,
            $"newest sequence={ledger.currentGenerationEvents[^1].sequence}");
    }

    private static void VerifyLongRunCompaction()
    {
        UsageLedger first = BuildLongRunLedger();
        UsageLedger second = BuildLongRunLedger();
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();

        Require(
            first.currentGenerationEvents.Count == 0,
            "closed generations retained raw events");
        Require(
            first.compactedSegments.Count <= 64,
            $"segments={first.compactedSegments.Count}");
        Require(
            first.compactedSegments.All(segment =>
                segment != null && segment.keyEvents.Count <= 8),
            "a compacted segment exceeded eight key events");
        Require(
            first.compactedSegments.Sum(segment => segment.eventCount) == 10000,
            $"event total={first.compactedSegments.Sum(segment => segment.eventCount)}");
        Require(
            string.Equals(
                compactor.ComputeHistoryHash(first),
                compactor.ComputeHistoryHash(second),
                StringComparison.Ordinal),
            "identical ledgers produced different hashes");
    }

    private static UsageLedger BuildLongRunLedger()
    {
        UsageLedger ledger = new UsageLedger();
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();
        for (int generation = 0; generation < 10000; generation++)
        {
            compactor.Record(
                ledger,
                generation % 3 == 0
                    ? "combat.intercept"
                    : "work.completed",
                1f + generation % 7,
                $"worker:{generation % 11}",
                "instance:long-run",
                new[] { generation % 2 == 0 ? "defense" : "production" },
                $"evidence:{generation}");
            compactor.CloseGeneration(ledger, generation);
        }

        return ledger;
    }

    private static void VerifyFacilityCandidateDeterminism()
    {
        using FacilityFixture first = new FacilityFixture("Candidate A");
        using FacilityFixture second = new FacilityFixture("Candidate B");
        FacilityEvolutionState fixedState = new FacilityEvolutionState
        {
            facilityPersistentId = "facility:deterministic",
            generation = 0,
            mastery = 0f
        };
        first.State.ReplaceInstanceEvolution(fixedState);
        second.State.ReplaceInstanceEvolution(fixedState);

        FacilityInstanceEvolutionRuntime runtime = new FacilityInstanceEvolutionRuntime(
            new FacilityEvolutionStateComponentFactory(),
            new UsageLedgerCompactor(),
            new EvolutionModuleRegistry(),
            roomEnvironment: null,
            facilityCandidateCache: null,
            worldItems: null,
            relocationWorld: new NoopRelocationWorldService());
        runtime.RecordUsage(
            first.Building,
            "research.completed",
            FacilityEvolutionProgression.GetRequiredMastery(0),
            3f,
            "worker:researcher",
            new[] { "research", "arcane" });
        runtime.RecordUsage(
            second.Building,
            "research.completed",
            FacilityEvolutionProgression.GetRequiredMastery(0),
            3f,
            "worker:researcher",
            new[] { "research", "arcane" });

        IReadOnlyList<FacilityGenerationCandidate> firstCandidates =
            runtime.GetGenerationCandidates(first.Building);
        IReadOnlyList<FacilityGenerationCandidate> secondCandidates =
            runtime.GetGenerationCandidates(second.Building);
        Require(firstCandidates.Count == 3, $"candidate count={firstCandidates.Count}");
        Require(
            firstCandidates.Select(DescribeCandidate).SequenceEqual(
                secondCandidates.Select(DescribeCandidate),
                StringComparer.Ordinal),
            "same seed, ID, generation, and history produced different candidates");
        Require(
            firstCandidates[0].benefitModuleId == "facility:research",
            $"primary module={firstCandidates[0].benefitModuleId}");
    }

    private static string DescribeCandidate(FacilityGenerationCandidate candidate)
    {
        return string.Join(
            "|",
            candidate.candidateId,
            candidate.kind,
            candidate.targetGeneration,
            candidate.benefitModuleId,
            candidate.burdenModuleId,
            candidate.catalystFamily,
            candidate.minimumCatalystPotency,
            candidate.historyHash);
    }

    private static void VerifyRoomActivationContract()
    {
        EvolutionModuleActivationRule rule = new EvolutionModuleActivationRule
        {
            kind = EvolutionModuleActivationKind.RoomConditional,
            requiredRoomTags = new List<string> { "Research" },
            forbiddenRoomTags = new List<string> { "Prison" },
            minimumCleanliness = 60f,
            minimumBeauty = 40f,
            minimumTemperature = 15f,
            minimumSpace = 50f
        };
        EvolutionRoomConditionSnapshot matching =
            new EvolutionRoomConditionSnapshot(
                new[] { "Research" },
                80f,
                60f,
                20f,
                70f);
        EvolutionRoomConditionSnapshot mismatching =
            new EvolutionRoomConditionSnapshot(
                new[] { "Research", "Prison" },
                20f,
                60f,
                20f,
                70f);

        Require(
            EvolutionModuleActivation.IsBenefitActive(rule, matching),
            "matching room did not activate its benefit");
        Require(
            !EvolutionModuleActivation.IsBenefitActive(rule, mismatching),
            "forbidden or dirty room kept its benefit active");

        EvolutionModuleRegistry registry = new EvolutionModuleRegistry();
        Require(
            registry.TryGet(
                "facility:room-synergy",
                out EvolutionModuleDefinition module)
            && module.Burdens.Count > 0,
            "conditional module lost its persistent burden");
    }

    private static void VerifyCatalystRules()
    {
        Require(
            EvolutionCatalystEconomyRules.RefinementResidueCost == 3,
            "refinement ratio changed");
        Require(
            EvolutionCatalystEconomyRules.PotencyUpgradeResidueCost == 5,
            "potency upgrade ratio changed");
        Require(
            Mathf.Approximately(
                EvolutionCatalystEconomyRules.MerchantExchangeValueMultiplier,
                1.5f),
            "merchant exchange multiplier changed");
        Require(
            EquipmentEvolutionProgression.GetMinimumCatalystPotency(0) == 1
            && EquipmentEvolutionProgression.GetMinimumCatalystPotency(4) == 2
            && EquipmentEvolutionProgression.GetMinimumCatalystPotency(8) == 3,
            "generation potency gates are incorrect");

        string catalystId = EvolutionCatalystItemId.BuildCatalyst(
            "catalyst:arcane",
            7);
        Require(
            EvolutionCatalystItemId.TryParseCatalyst(
                catalystId,
                out EquipmentCatalystDefinition catalyst)
            && catalyst.family == "arcane"
            && catalyst.potency == 7,
            $"catalyst parse failed: {catalystId}");
        Require(
            EquipmentEvolutionRuntime.GetCatalystFamilyPotencyScale("arcane")
            > EquipmentEvolutionRuntime.GetCatalystFamilyPotencyScale("industry"),
            "catalyst families do not alter equipment outcomes");
        Require(
            EvolutionCatalystItemDefinitions.GetCatalystValue(4)
            > EvolutionCatalystItemDefinitions.GetCatalystValue(3),
            "higher potency did not increase catalyst value");
    }

    private static void VerifyNarrativeLock()
    {
        UsageLedger ledger = new UsageLedger();
        UsageLedgerCompactor compactor = new UsageLedgerCompactor();
        compactor.Record(
            ledger,
            "combat.boss-defeated",
            20f,
            "owner:test",
            "equipment:test",
            new[] { "boss", "combat" },
            "evidence:boss");
        EvolutionNode hiddenNode = new EvolutionNode
        {
            nodeId = "history:test",
            parentNodeId = "history:parent",
            effectId = "history:equipment",
            generation = 3,
            historical = true,
            playerVisible = false
        };
        EvolutionNarrativeRequestSnapshot request =
            EvolutionNarrativeRequestFactory.Create(
                EvolutionNarrativeTargetKind.Equipment,
                "equipment:test",
                hiddenNode,
                compactor.ComputeHistoryHash(ledger),
                ledger,
                effectBudget: 0);
        Require(!hiddenNode.playerVisible, "pending history became player-visible");

        EvolutionHistoryNarrativeResponseDto valid = new EvolutionHistoryNarrativeResponseDto
        {
            requestKey = request.requestKey,
            targetPersistentId = request.targetPersistentId,
            nodeId = request.nodeId,
            parentNodeId = request.parentNodeId,
            effectId = request.effectId,
            effectBudget = request.effectBudget,
            evidenceIds = request.evidenceIds.ToArray(),
            displayName = "남겨진 칼끝",
            description = "오래 버틴 흔적이 칼날에 남았다.",
            historyReason = "강적을 쓰러뜨린 기록에서 비롯되었다."
        };
        Require(
            EvolutionNarrativeResponseValidator.Validate(
                request,
                valid,
                out string validFailure),
            validFailure);

        EvolutionHistoryNarrativeResponseDto invalid = new EvolutionHistoryNarrativeResponseDto
        {
            requestKey = request.requestKey,
            targetPersistentId = request.targetPersistentId,
            nodeId = request.nodeId,
            parentNodeId = request.parentNodeId,
            effectId = "combat.damage",
            effectBudget = 999,
            evidenceIds = new[] { "evidence:invented" },
            displayName = valid.displayName,
            description = valid.description,
            historyReason = valid.historyReason
        };
        Require(
            !EvolutionNarrativeResponseValidator.Validate(
                request,
                invalid,
                out _),
            "an LLM response changed locked IDs, budget, or evidence");
    }

    private static void VerifyPackedRelocationSaveState()
    {
        using FacilityFixture fixture = new FacilityFixture("Packed Facility");
        Grid grid = new Grid(10, 4);
        fixture.Building.SetGrid(grid);
        fixture.Building.SetRuntimeGridPosition(new Vector2Int(4, 1));
        Require(
            grid.RegisterOccupant(
                fixture.Building,
                GridLayer.Construction,
                fixture.Building.buildPoses,
                false),
            "packed facility could not reserve the construction layer");

        FacilityEvolutionState state = fixture.State.InstanceEvolution;
        state.facilityPersistentId = "facility:packed";
        state.relocationOrder = new FacilityRelocationOrder
        {
            orderId = "relocation:test",
            facilityPersistentId = state.facilityPersistentId,
            sourceX = 1,
            sourceY = 1,
            destinationX = 4,
            destinationY = 1,
            dismantleRequiredWork = 25f,
            dismantleCompletedWork = 25f,
            reinstallRequiredWork = 50f,
            phase = FacilityRelocationPhase.WaitingForPackage
        };
        fixture.State.ReplaceInstanceEvolution(state);

        ModularFacilityBuildingSaveData save =
            ModularFacilityBuildingSaveData.From(fixture.Building);
        Require(save.hasRuntimeLayer, "runtime occupancy layer was not captured");
        Require(
            save.runtimeLayer == GridLayer.Construction,
            $"runtime layer={save.runtimeLayer}");
        Require(save.relocationPacked, "packed relocation flag was not captured");
        Require(
            save.layer == fixture.Data.Placement.Layer,
            "authored placement layer was overwritten");
    }

    private static void VerifyWorldSaveV2Migration()
    {
        ModularFacilityWorldSaveService service =
            new ModularFacilityWorldSaveService(
                _ => null,
                new GridBuildingFactory(_ => { }));
        ModularFacilityWorldSaveData legacy = new ModularFacilityWorldSaveData
        {
            version = 2,
            buildings = new List<ModularFacilityBuildingSaveData>
            {
                new ModularFacilityBuildingSaveData
                {
                    buildingId = 77,
                    layer = GridLayer.Building,
                    centerX = 3,
                    centerY = 1
                }
            }
        };
        ModularFacilityWorldSaveData migrated =
            service.FromJson(JsonUtility.ToJson(legacy));
        Require(
            migrated.version == ModularFacilityWorldSaveService.CurrentVersion,
            $"migrated version={migrated.version}");
        Require(migrated.migratedFromVersion == 2, "v2 migration was not recorded");
        Require(migrated.buildings.Count == 1, "v2 building was lost");
        Require(
            !migrated.buildings[0].hasRuntimeLayer,
            "legacy building invented a runtime layer");
    }

    private static void VerifyPackedRelocationDestruction()
    {
        FacilityFixture fixture = new FacilityFixture("Packed Facility Destruction");
        Grid grid = new Grid(10, 4);
        Vector2Int packedPosition = new Vector2Int(6, 1);
        fixture.Building.SetGrid(grid);
        fixture.Building.SetRuntimeGridPosition(packedPosition);
        Require(
            grid.RegisterOccupant(
                fixture.Building,
                GridLayer.Construction,
                fixture.Building.buildPoses,
                false),
            "packed facility could not reserve the construction layer");

        fixture.Building.DestroySelf();
        Require(
            grid.GetGridCell(packedPosition)?.GetOccupant(GridLayer.Construction) == null,
            "destroyed packed facility left stale construction occupancy");
        fixture.Dispose();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FacilityFixture : IDisposable
    {
        private readonly GameObject gameObject;

        public FacilityFixture(string name)
        {
            Data = ScriptableObject.CreateInstance<BuildingSO>();
            Data.id = 9801;
            Data.objectName = name;
            Data.width = 1;
            Data.height = 1;
            Data.layer = GridLayer.Building;
            Data.category = BuildingCategory.Special;
            Data.type = typeof(BuildableObject);
            Data.unlocked = true;
            Data.ReplaceAbilities(new BuildingAbilityCollection());

            gameObject = new GameObject(name);
            Building = gameObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(Building);
            Building.Initialization(Data, Vector2Int.zero);
            State = Building.GetComponent<FacilityEvolutionStateComponent>();
        }

        public BuildingSO Data { get; }
        public BuildableObject Building { get; }
        public FacilityEvolutionStateComponent State { get; }

        public void Dispose()
        {
            if (gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            if (Data != null)
            {
                UnityEngine.Object.DestroyImmediate(Data);
            }
        }
    }

    private sealed class NoopRelocationWorldService :
        IFacilityRelocationWorldService
    {
        public bool CanRelocate(
            BuildableObject source,
            Vector2Int destination,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryPackAtDestination(
            BuildableObject source,
            Vector2Int destination,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryCompleteRelocation(
            BuildableObject packedSource,
            out BuildableObject relocated,
            out string failureReason)
        {
            relocated = packedSource;
            failureReason = string.Empty;
            return true;
        }

        public void RestorePackedPresentation(BuildableObject packedSource)
        {
        }
    }
}
