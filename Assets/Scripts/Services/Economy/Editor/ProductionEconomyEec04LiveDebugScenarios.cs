#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static partial class ProductionEconomyDebugScenarios
{
    private const string Eec04InputItemId = "resource:grass-straw";
    private const string Eec04OutputItemId = "waste:plant-rot";
    private const string Eec04WorkstationTag =
        "workstation:feedbench";
    private const string Eec04SupportTag =
        "support:qa-eec04-fermenter";
    private const int Eec04InputQuantity = 30;
    private const long Eec04InputMassGrams = 2_400L;
    private const long Eec04WaterMassGrams = 600L;

    [MenuItem(
        "DungeonStory/Audit/EEC04/Verify Passive Production Live Outcomes")]
    public static void RunEec04PassiveProductionStatusFocused()
    {
        List<string> warningsAndErrors = new();
        void CaptureLog(string message, string stackTrace, LogType type)
        {
            if (type is LogType.Warning or LogType.Error or LogType.Assert
                or LogType.Exception)
            {
                warningsAndErrors.Add(type + ":" + (message ?? string.Empty));
            }
        }

        Application.logMessageReceived += CaptureLog;
        Eec04PassiveProductionReport report;
        try
        {
            report = VerifyEec04PassiveProductionStatus();
            Require(
                warningsAndErrors.Count == 0,
                "EEC04 passive production emitted warnings/errors: "
                + string.Join(" | ", warningsAndErrors));
            report.warningCount = 0;
            report.errorCount = 0;
        }
        finally
        {
            Application.logMessageReceived -= CaptureLog;
        }

        WriteEec04ReportCreateOnly(report);
        Debug.Log(
            "EEC04_PASSIVE_PRODUCTION_LIVE=PASS "
            + "success=Succeeded; partial=PartiallySucceeded; ruined=Failed; "
            + "outcomes=3; batches=3; duplicateOutcomes=0; duplicateBatches=0");
    }

    private static Eec04PassiveProductionReport
        VerifyEec04PassiveProductionStatus()
    {
        ResourceEconomyContentCatalog authored = LoadCatalog();
        ProductionRecipeSO successRecipe = CreateEec04Recipe(
            "recipe:qa-eec04-passive-success",
            "output:qa-eec04-passive-success/000/main/waste:plant-rot",
            declareProcessLoss: false);
        ProductionRecipeSO partialRecipe = CreateEec04Recipe(
            "recipe:qa-eec04-passive-partial",
            "output:qa-eec04-passive-partial/000/main/waste:plant-rot",
            declareProcessLoss: true);
        ProductionRecipeSO ruinedRecipe = CreateEec04Recipe(
            "recipe:qa-eec04-passive-ruined",
            "output:qa-eec04-passive-ruined/000/main/waste:plant-rot",
            declareProcessLoss: false);
        BuildingSO workstationData = LoadEec04WorkstationDefinition();
        BuildingSO supportData = CreateEec04SupportDefinition();
        GameObject workstationObject = new("EEC04 Passive Production Workstation");
        GameObject supportObject = new("EEC04 Passive Production Support");
        GameObject workerObject = new("EEC04 Passive Production Worker");

        try
        {
            ResourceEconomyContentCatalog catalog = new(
                authored.Items,
                new[] { successRecipe, partialRecipe, ruinedRecipe },
                Array.Empty<CropDefinitionSO>(),
                Array.Empty<CraftMaterialDefinitionSO>());

            BuildableObject workstation = workstationObject
                .AddComponent<BuildableObject>();
            workstation.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:eec04-passive-workstation");
            CharacterAiEditorTestDependencies.Inject(workstation);
            workstation.Initialization(workstationData, new Vector2Int(14, 4));

            BuildableObject support = supportObject
                .AddComponent<BuildableObject>();
            support.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:eec04-passive-support");
            CharacterAiEditorTestDependencies.Inject(support);
            support.Initialization(supportData, new Vector2Int(15, 4));

            CharacterActor worker = workerObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            worker.EnsureRuntimeState();
            worker.Identity.SetPersistentId(
                "character:qa:eec04-passive-production-worker");

            FakeProductionItemGateway items = new();
            items.SetDefinitionMass(Eec04InputItemId, 80L);
            MutableGameClock clock = new();
            MutablePowerRuntime power = new();
            FakeProductionWorkshop workshop = new(workstation, support);
            using Eec04PhysicalOutputFixture physical = new(
                catalog,
                supportData,
                clock);
            Eec04ObservedCareerLifeEvents observedLifeEvents = new();
            ProductionRuntimeFixture runtime = CreateRuntime(
                catalog,
                items,
                seed: 90404,
                workshops: workshop,
                buildingWorld: new FixedBuildingWorldQuery(
                    workstation,
                    support),
                power: power,
                clock: clock,
                preparedOutputRouting: physical.Routing,
                preparedOutputFactory: physical.CreateAdapter,
                commandObservations: physical.CommandFacts,
                commandRecovery: physical.CommandFacts,
                calendar: physical.Calendar,
                recipeExecutionReceiptsFactory: stateStore =>
                    new ProductionRecipeExecutionReceiptAuthority(
                        observedLifeEvents,
                        physical.CompletionFacts,
                        physical.CompletionFacts,
                        stateStore));

            List<Eec04PassiveProductionCaseReport> cases = new()
            {
                RunEec04Case(
                    runtime,
                    physical,
                    items,
                    power,
                    clock,
                    workstation,
                    worker,
                    successRecipe,
                    Eec04CaseMode.Success,
                    GameplayOutcomeStatus.Succeeded,
                    expectedQuantity: 4,
                    expectedOutputMassGrams: 2_400L,
                    expectedLossMassGrams: 0L),
                RunEec04Case(
                    runtime,
                    physical,
                    items,
                    power,
                    clock,
                    workstation,
                    worker,
                    partialRecipe,
                    Eec04CaseMode.Partial,
                    GameplayOutcomeStatus.PartiallySucceeded,
                    expectedQuantity: 2,
                    expectedOutputMassGrams: 1_200L,
                    expectedLossMassGrams: 1_200L),
                RunEec04Case(
                    runtime,
                    physical,
                    items,
                    power,
                    clock,
                    workstation,
                    worker,
                    ruinedRecipe,
                    Eec04CaseMode.Ruined,
                    GameplayOutcomeStatus.Failed,
                    expectedQuantity: 4,
                    expectedOutputMassGrams: 2_400L,
                    expectedLossMassGrams: 0L)
            };

            Require(
                observedLifeEvents.DeclaredLossCount == 1,
                "EEC04 partial production did not publish one declared-loss life event.");
            GameplayOutcomeSnapshot[] exactOutcomes = physical.Ledger
                .GetGlobal(
                    OutcomeCursor.FirstPage(16),
                    OutcomeFilter.All)
                .Items
                .Where(value => value.Exact != null)
                .Select(value => value.Exact)
                .ToArray();
            Require(
                exactOutcomes.Count(value => string.Equals(
                    value.outcomeTypeId,
                    ProductionCombatOutcomeIds.ProductionCompleted.Value,
                    StringComparison.Ordinal)) == 3
                && exactOutcomes.Count(value => string.Equals(
                    value.outcomeTypeId,
                    ProductionCommandOutcomeIds.Applied.Value,
                    StringComparison.Ordinal)) == 3,
                "EEC04 actual passive production did not publish exactly three command and three completion outcomes.");
            Require(
                physical.Publication
                    .CaptureAcknowledgedRestoreBatchesForEditorTest().Count == 3,
                "EEC04 actual passive production did not retain exactly three physical batches.");

            runtime.Tick();
            Require(
                physical.Ledger.GetDiagnostics().PublishedCount == 6,
                "EEC04 replay tick duplicated a gameplay outcome.");
            Require(
                physical.Publication
                    .CaptureAcknowledgedRestoreBatchesForEditorTest().Count == 3,
                "EEC04 replay tick duplicated or removed a physical output batch.");

            return new Eec04PassiveProductionReport
            {
                result = "PASS",
                source =
                    "ProductionBillRuntime->ProductionPreparedOutputExecutionAdapter"
                    + "->FacilityBufferPlannedOutputPublicationService"
                    + "->ProductionCycleCompletedFact"
                    + "->GameplayDomainFactObserver",
                inputItemId = Eec04InputItemId,
                inputQuantityPerCase = Eec04InputQuantity,
                inputMassGramsPerCase = Eec04InputMassGrams,
                cleanWaterMassGramsPerCase = Eec04WaterMassGrams,
                wastewaterMassGramsPerCase = Eec04WaterMassGrams,
                publishedOutcomeCount = 3,
                publishedCommandOutcomeCount = 3,
                acknowledgedPhysicalBatchCount = 3,
                declaredLossObservationCount =
                    observedLifeEvents.DeclaredLossCount,
                duplicateOutcomeCount = 0,
                duplicatePhysicalBatchCount = 0,
                cases = cases
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(supportObject);
            UnityEngine.Object.DestroyImmediate(workstationObject);
            UnityEngine.Object.DestroyImmediate(supportData);
            UnityEngine.Object.DestroyImmediate(ruinedRecipe);
            UnityEngine.Object.DestroyImmediate(partialRecipe);
            UnityEngine.Object.DestroyImmediate(successRecipe);
        }
    }

    private static Eec04PassiveProductionCaseReport RunEec04Case(
        ProductionRuntimeFixture runtime,
        Eec04PhysicalOutputFixture physical,
        FakeProductionItemGateway items,
        MutablePowerRuntime power,
        MutableGameClock clock,
        BuildableObject workstation,
        CharacterActor worker,
        ProductionRecipeSO recipe,
        Eec04CaseMode mode,
        GameplayOutcomeStatus expectedStatus,
        int expectedQuantity,
        long expectedOutputMassGrams,
        long expectedLossMassGrams)
    {
        power.Powered = true;
        clock.DeltaTimeValue = 0f;
        ProductionBillCommandResult added = runtime.AddBill(
            workstation,
            recipe.RecipeId,
            ProductionOrderMode.RepeatCount,
            1);
        Require(
            added.Succeeded,
            $"EEC04 {mode} bill add failed: {added.Failure.Code}");
        ProductionBillSaveData savedBill = runtime.Core.Capture().bills.Single(
            value => string.Equals(
                value.billId,
                added.BillId.Value,
                StringComparison.Ordinal));
        int cycleSequence = savedBill.cycleSequence;
        Require(
            cycleSequence > 0,
            $"EEC04 {mode} bill has no cycle sequence.");

        string destination = ProductionBillRuntime.DestinationPrefix
            + added.BillId.Value;
        items.Deliver(Eec04InputItemId, Eec04InputQuantity, destination);
        ProductionWorkBeginResult begin = runtime.BeginWork(
            worker,
            workstation,
            BuiltInWorkTypeIds.Craft);
        Require(
            begin.Succeeded && begin.Bill.BillId == added.BillId,
            $"EEC04 {mode} passive preparation did not begin: "
            + begin.Failure.Code + ":"
            + string.Join(",", begin.Failure.Parameters.ToArray()));
        ProductionWorkExecutionResult preparation = runtime.ExecuteWork(
            worker,
            workstation,
            added.BillId,
            recipe.RequiredWork);
        Require(
            preparation.Succeeded && !preparation.CycleCompleted,
            $"EEC04 {mode} preparation did not enter passive processing: "
            + preparation.Failure.Code);
        ProductionBillSnapshot processing = runtime.GetBills(workstation)
            .Single(value => value.BillId == added.BillId);
        Require(
            processing.BatchStage == ProductionBatchStage.Processing
            && processing.MaterialsConsumed
            && processing.ProcessFluidConsumed,
            $"EEC04 {mode} did not consume inputs/fluids into a processing batch: "
            + $"stage={processing.BatchStage}; "
            + $"materials={processing.MaterialsConsumed}; "
            + $"fluid={processing.ProcessFluidConsumed}; "
            + $"work={processing.CompletedWork}/{processing.RequiredWork}.");

        switch (mode)
        {
            case Eec04CaseMode.Success:
                clock.DeltaTimeValue =
                    GameSimulationTimeRules.SecondsPerGameHour * 12f;
                runtime.Tick();
                CompleteEec04FinishingWork(
                    runtime,
                    workstation,
                    worker,
                    added.BillId,
                    mode);
                break;
            case Eec04CaseMode.Partial:
                power.Powered = false;
                clock.DeltaTimeValue =
                    GameSimulationTimeRules.SecondsPerGameHour * 16.2f;
                runtime.Tick();
                ProductionBillSnapshot degraded = runtime.GetBills(workstation)
                    .Single(value => value.BillId == added.BillId);
                Require(
                    degraded.BatchIntegrity > 48f
                    && degraded.BatchIntegrity < 50f,
                    "EEC04 partial batch did not reach the deterministic half-yield band: "
                    + degraded.BatchIntegrity);
                power.Powered = true;
                clock.DeltaTimeValue =
                    GameSimulationTimeRules.SecondsPerGameHour * 12f;
                runtime.Tick();
                CompleteEec04FinishingWork(
                    runtime,
                    workstation,
                    worker,
                    added.BillId,
                    mode);
                break;
            case Eec04CaseMode.Ruined:
                power.Powered = false;
                clock.DeltaTimeValue =
                    GameSimulationTimeRules.SecondsPerGameHour * 26f;
                runtime.Tick();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
        power.Powered = true;
        clock.DeltaTimeValue = 0f;

        GameplayResultKey key = new(
            ProductionCombatOutcomeIds.ProductionProducerId,
            new GameplayOperationId(
                $"production-cycle:{added.BillId.Value}:{cycleSequence}"),
            cycleSequence,
            0);
        Require(
            physical.Ledger.TryGetResultIdentity(
                key,
                out GameplayOutcomeReplayIdentity identity)
            && identity.State
                == GameplayOutcomeReplayState.PublishedAcknowledged,
            $"EEC04 {mode} has no exact acknowledged completion identity.");
        Require(
            physical.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot outcome),
            $"EEC04 {mode} exact outcome is not queryable.");
        Require(
            outcome.status == expectedStatus,
            $"EEC04 {mode} status was {outcome.status}, expected {expectedStatus}.");

        string batchCommitId = outcome.provenance.Single(value =>
            string.Equals(
                value.kindId,
                "production-batch",
                StringComparison.Ordinal)).value;
        FacilityBufferPlannedOutputRestoreBatchSnapshot physicalBatch =
            physical.Publication
                .CaptureAcknowledgedRestoreBatchesForEditorTest()
                .Single(value => string.Equals(
                    value.BatchCommitId,
                    batchCommitId,
                    StringComparison.Ordinal));
        Require(
            physicalBatch.TotalQuantity == expectedQuantity
            && physicalBatch.TotalMassGrams == expectedOutputMassGrams
            && physicalBatch.Stacks.All(value => string.Equals(
                value.ItemId,
                Eec04OutputItemId,
                StringComparison.Ordinal))
            && physicalBatch.Stacks.Sum(value => value.Quantity)
                == expectedQuantity
            && physicalBatch.Stacks.Sum(value => value.MassGrams)
                == expectedOutputMassGrams,
            $"EEC04 {mode} physical output does not match ledger quantities.");

        AssertEec04Metric(
            outcome,
            ProductionCombatOutcomeIds.ProducedQuantityMetric.Value,
            ProductionCombatOutcomeIds.CountUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipe.RecipeId,
            expectedQuantity);
        AssertEec04Metric(
            outcome,
            ProductionCombatOutcomeIds.ProducedMassMetric.Value,
            ProductionCombatOutcomeIds.GramUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipe.RecipeId,
            expectedOutputMassGrams);
        AssertEec04Metric(
            outcome,
            ProductionCombatOutcomeIds.DeclaredLossMetric.Value,
            ProductionCombatOutcomeIds.GramUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipe.RecipeId,
            expectedLossMassGrams);
        AssertEec04Metric(
            outcome,
            ProductionCombatOutcomeIds.WipInputQuantityMetric.Value,
            ProductionCombatOutcomeIds.CountUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipe.RecipeId,
            Eec04InputQuantity);
        AssertEec04Metric(
            outcome,
            ProductionCombatOutcomeIds.WipInputMassMetric.Value,
            ProductionCombatOutcomeIds.GramUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipe.RecipeId,
            Eec04InputMassGrams);
        AssertEec04Metric(
            outcome,
            ProductionCombatOutcomeIds.CleanWaterMetric.Value,
            ProductionCombatOutcomeIds.GramUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipe.RecipeId,
            Eec04WaterMassGrams);
        AssertEec04Metric(
            outcome,
            ProductionCombatOutcomeIds.WastewaterMetric.Value,
            ProductionCombatOutcomeIds.GramUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipe.RecipeId,
            Eec04WaterMassGrams);
        AssertEec04Metric(
            outcome,
            ProductionCombatOutcomeIds.ProducedQuantityMetric.Value,
            ProductionCombatOutcomeIds.CountUnit.Value,
            ProductionCombatOutcomeIds.ItemKind.Value,
            Eec04OutputItemId,
            expectedQuantity);
        AssertEec04Metric(
            outcome,
            ProductionCombatOutcomeIds.ProducedMassMetric.Value,
            ProductionCombatOutcomeIds.GramUnit.Value,
            ProductionCombatOutcomeIds.ItemKind.Value,
            Eec04OutputItemId,
            expectedOutputMassGrams);

        long inputSideMass = checked(
            Eec04InputMassGrams + Eec04WaterMassGrams);
        long outputSideMass = checked(
            expectedOutputMassGrams
            + Eec04WaterMassGrams
            + expectedLossMassGrams);
        Require(
            inputSideMass == outputSideMass,
            $"EEC04 {mode} mass conservation failed: "
            + $"input={inputSideMass}; output={outputSideMass}.");

        return new Eec04PassiveProductionCaseReport
        {
            caseId = mode.ToString().ToLowerInvariant(),
            billId = added.BillId.Value,
            cycleSequence = cycleSequence,
            resultState = identity.State.ToString(),
            status = outcome.status.ToString(),
            batchCommitId = batchCommitId,
            physicalItemId = Eec04OutputItemId,
            producedQuantity = physicalBatch.TotalQuantity,
            producedMassGrams = physicalBatch.TotalMassGrams,
            declaredLossMassGrams = expectedLossMassGrams,
            wipInputQuantity = Eec04InputQuantity,
            wipInputMassGrams = Eec04InputMassGrams,
            cleanWaterMassGrams = Eec04WaterMassGrams,
            wastewaterMassGrams = Eec04WaterMassGrams,
            conservedInputMassGrams = inputSideMass,
            conservedOutputMassGrams = outputSideMass
        };
    }

    private static void CompleteEec04FinishingWork(
        ProductionRuntimeFixture runtime,
        BuildableObject workstation,
        CharacterActor worker,
        ProductionBillId billId,
        Eec04CaseMode mode)
    {
        ProductionBillSnapshot finishing = runtime.GetBills(workstation)
            .Single(value => value.BillId == billId);
        Require(
            finishing.BatchStage == ProductionBatchStage.Finishing,
            $"EEC04 {mode} processing did not reach finishing.");
        ProductionWorkBeginResult finishBegin = runtime.BeginWork(
            worker,
            workstation,
            BuiltInWorkTypeIds.Craft);
        Require(
            finishBegin.Succeeded && finishBegin.Bill.BillId == billId,
            $"EEC04 {mode} finishing did not begin: "
            + finishBegin.Failure.Code);
        ProductionWorkExecutionResult completed = runtime.ExecuteWork(
            worker,
            workstation,
            billId,
            1f);
        Require(
            completed.Succeeded && completed.CycleCompleted,
            $"EEC04 {mode} finishing did not complete: "
            + completed.Failure.Code);
    }

    private static void AssertEec04Metric(
        GameplayOutcomeSnapshot outcome,
        string metricId,
        string unitId,
        string referenceKindId,
        string referenceId,
        double expected)
    {
        GameplayOutcomeMetricSnapshot metric = outcome.metrics.Single(value =>
            string.Equals(value.metricId, metricId, StringComparison.Ordinal)
            && string.Equals(value.unitId, unitId, StringComparison.Ordinal)
            && string.Equals(
                value.referenceKindId,
                referenceKindId,
                StringComparison.Ordinal)
            && string.Equals(
                value.referenceId,
                referenceId,
                StringComparison.Ordinal));
        Require(
            Math.Abs(metric.value - expected) < 0.0001d,
            $"EEC04 metric {metricId}/{referenceId} was {metric.value}, "
            + $"expected {expected}.");
    }

    private static ProductionRecipeSO CreateEec04Recipe(
        string recipeId,
        string outputLineId,
        bool declareProcessLoss)
    {
        ProductionRecipeSO recipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        recipe.Configure(
            recipeId,
            "EEC04 수동 생산 " + recipeId,
            "수동 생산 상태와 실제 질량 원장 검증용 레시피.",
            "qa-eec04-feedbench",
            BuiltInWorkTypeIds.Craft.Value,
            string.Empty,
            3f,
            new[]
            {
                new ItemAmountDefinition(
                    Eec04InputItemId,
                    Eec04InputQuantity)
            },
            new[]
            {
                new ProductionOutputDefinition(
                    outputLineId,
                    ProductionOutputRole.Main,
                    Eec04OutputItemId,
                    4)
            });
        recipe.ConfigureWorkshop(
            Eec04WorkstationTag,
            new[] { Eec04SupportTag },
            ProductionProcessKind.PassiveBatch,
            Eec04SupportTag,
            prepareWork: 2f,
            finishWork: 1f,
            processGameHours: 12f,
            cleanWater: 1.2f,
            wastewater: 1.2f,
            allowManualWater: true,
            failedBatchItemId: Eec04OutputItemId,
            wastewaterKind: ProcessWastewaterComposition.FermentationEffluent);
        recipe.ConfigureProficiency(
            BuiltInCharacterProficiencyIds.FoodProduction);
        recipe.ConfigureProcessClass(
            ProductionProcessClass.CookingSimpleMixing);
        if (declareProcessLoss)
        {
            recipe.ConfigureMassExplanation(
                ProcessLossProductionMassExplanationCapability.Id,
                ProcessLossProductionMassExplanationCapability.Version,
                ProcessLossProductionMassExplanationCapability.BuildPayload(
                    PhysicalMassLossKind.FermentationGasLoss,
                    "qa-eec04-passive-integrity-loss"));
        }
        return recipe;
    }

    private static BuildingSO LoadEec04WorkstationDefinition() =>
        LoadAll<BuildingSO>("Assets/Resources/SO/Building/Modular")
            .Single(asset => asset != null
                && asset.id == 1089
                && string.Equals(
                    asset.GetProductionWorkstationAbility()?.WorkstationTag,
                    Eec04WorkstationTag,
                    StringComparison.Ordinal)
                && asset.GetProductionBufferAbility()
                    ?.physicalOutputBufferCycleCapacity == 4);

    private static BuildingSO CreateEec04SupportDefinition()
    {
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingProductionSupportAbility
        {
            supportId = "support:qa-eec04-fermenter-instance",
            featureTags = new[] { Eec04SupportTag },
            compatibleWorkstationTags = new[] { Eec04WorkstationTag },
            kind = ProductionSupportKind.BatchProcessor,
            batchCapacity = 1,
            maximumLinkedInstancesPerWorkstation = 1,
            requiresPower = true,
            requiresFuel = false,
            workSpeedMultiplier = 1f,
            outputMultiplier = 1f
        });
        building.id = 99405;
        building.objectName = "EEC04 수동 생산 발효조";
        building.ReplaceAbilities(abilities);
        return building;
    }

    private static void WriteEec04ReportCreateOnly(
        Eec04PassiveProductionReport report)
    {
        string outputRoot = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(
            !string.IsNullOrWhiteSpace(outputRoot),
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT is required.");
        Directory.CreateDirectory(outputRoot);
        string reportPath = Path.Combine(
            outputRoot,
            "eec04-passive-production-live.json");
        using FileStream stream = new(
            reportPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(JsonUtility.ToJson(report, prettyPrint: true));
        writer.WriteLine();
    }

    private enum Eec04CaseMode
    {
        Success,
        Partial,
        Ruined
    }

    [Serializable]
    private sealed class Eec04PassiveProductionReport
    {
        public string result = string.Empty;
        public string source = string.Empty;
        public string inputItemId = string.Empty;
        public int inputQuantityPerCase;
        public long inputMassGramsPerCase;
        public long cleanWaterMassGramsPerCase;
        public long wastewaterMassGramsPerCase;
        public int publishedOutcomeCount;
        public int publishedCommandOutcomeCount;
        public int acknowledgedPhysicalBatchCount;
        public int declaredLossObservationCount;
        public int duplicateOutcomeCount;
        public int duplicatePhysicalBatchCount;
        public int warningCount;
        public int errorCount;
        public List<Eec04PassiveProductionCaseReport> cases = new();
    }

    [Serializable]
    private sealed class Eec04PassiveProductionCaseReport
    {
        public string caseId = string.Empty;
        public string billId = string.Empty;
        public int cycleSequence;
        public string resultState = string.Empty;
        public string status = string.Empty;
        public string batchCommitId = string.Empty;
        public string physicalItemId = string.Empty;
        public int producedQuantity;
        public long producedMassGrams;
        public long declaredLossMassGrams;
        public int wipInputQuantity;
        public long wipInputMassGrams;
        public long cleanWaterMassGrams;
        public long wastewaterMassGrams;
        public long conservedInputMassGrams;
        public long conservedOutputMassGrams;
    }

    private sealed class Eec04PhysicalOutputFixture : IDisposable
    {
        private readonly IResourceEconomyContentCatalog catalog;
        private readonly BuildingSO supportDefinition;
        private readonly IPhysicalItemMassQuery mass;
        private readonly FacilityBufferDestinationClaimRegistry claims;
        private readonly FacilityBufferPhysicalOccupancyQuery occupancy;
        private readonly FacilityBufferMassAdmissionService admission;
        private readonly ProductionOutputDestinationAuthorityRuntime
            destinations;
        private readonly ProductionPreparedOutputComponentCodec componentCodec;

        internal Eec04PhysicalOutputFixture(
            IResourceEconomyContentCatalog catalog,
            BuildingSO supportDefinition,
            IGameClock clock)
        {
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
            this.supportDefinition = supportDefinition
                ?? throw new ArgumentNullException(nameof(supportDefinition));
            IDungeonItemCatalogProvider itemCatalog =
                EditorItemCatalogFactory.Create();
            mass = new PhysicalItemMassQuery(itemCatalog);
            DungeonRuntimeAggregateRootStore roots = new();
            WorldItemRepository repository = new(
                new GuidPersistentIdGenerator(),
                roots);
            ItemQuantityReservationService reservations = new(
                repository,
                EditorNullItemMarkerPresenter.Instance,
                clock ?? throw new ArgumentNullException(nameof(clock)));
            occupancy = new FacilityBufferPhysicalOccupancyQuery(
                repository,
                mass,
                reservations,
                roots);
            claims = new FacilityBufferDestinationClaimRegistry();
            admission = new FacilityBufferMassAdmissionService(
                claims,
                occupancy,
                mass);
            FacilityBufferDestinationLifecycleService lifecycle = new(
                claims,
                claims,
                admission,
                admission);
            destinations = new ProductionOutputDestinationAuthorityRuntime(
                claims,
                admission,
                claims,
                admission,
                lifecycle);
            Publication = new FacilityBufferPlannedOutputPublicationService(
                repository,
                itemCatalog,
                mass,
                admission);
            Routing = new ProductionPreparedOutputRoutingAuthority();
            componentCodec = new ProductionPreparedOutputComponentCodec();

            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new ProductionCompletedOutcomeDescriptor(),
                    new ProductionCommandOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new ProductionCompletedOutcomeAdapter(),
                    new ProductionCommandOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId(
                    "run:eec04-passive-production-live"),
                1L);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                registry,
                new GameEventBus());
            Calendar = new EditorFixedGameCalendar(137, 9);
            GameplayDomainFactCommitGroup commits = new(recorder, Ledger);
            CommandFacts = new DomainFactDispatcher();
            CommandFacts.Subscribe(
                "production.command-observer",
                new GameplayDomainFactObserver<
                    ProductionCommandAppliedFact,
                    ProductionCommandOutcomeReceipt>(
                    ProductionCommandFactObservation.Capture,
                    registry,
                    Ledger,
                    recorder,
                    commits));
            CommandFacts.Subscribe(
                "production.completion-observer",
                new GameplayDomainFactObserver<
                    ProductionCycleCompletedFact,
                    ProductionCompletedOutcomeReceipt>(
                    ProductionCompletionFactObservation.Capture,
                    registry,
                    Ledger,
                    recorder,
                    commits));
            CompletionFacts = CommandFacts;
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal DomainFactDispatcher CommandFacts { get; }
        internal DomainFactDispatcher CompletionFacts { get; }
        internal EditorFixedGameCalendar Calendar { get; }
        internal FacilityBufferPlannedOutputPublicationService Publication
            { get; }
        internal ProductionPreparedOutputRoutingAuthority Routing { get; }

        internal IProductionPreparedOutputExecutionPort CreateAdapter(
            IProductionAssemblyBridge bridge)
        {
            StandardDefinitionProductionOutputCapability standard = new(
                catalog,
                componentCodec);
            PerishableFoodOutputCapability perishable = new(
                new ResourceItemDefinitionCatalog(catalog.Items));
            ProductionOutputHandlerRegistry handlers = new(
                new IProductionOutputCapability[]
                {
                    standard,
                    perishable
                });
            ProductionOutputMaximumMassRegistry maximumMass = new(
                new IProductionOutputMaximumMassCapability[]
                {
                    standard,
                    perishable
                },
                mass);
            IProductionOutputPlanningService outputPlanning =
                new ProductionOutputPlanningService(catalog, bridge);
            ProductionOutputBufferCapacityProjector capacity = new(
                catalog,
                bridge,
                new ProductionMaximumOutputFactorCatalog(
                    new[] { supportDefinition }),
                componentCodec,
                mass,
                maximumMass);
            return new ProductionPreparedOutputExecutionAdapter(
                catalog,
                outputPlanning,
                bridge,
                EmptyGrandProjectBenefitQuery.Instance,
                new CanonicalProductionOutputResolver(
                    new RandomStreamProvider(90404)),
                new ProductionPreparedOutputMaterializerRegistry(
                    new IProductionPreparedOutputMaterializer[]
                    {
                        componentCodec,
                        new PerishableFoodPreparedOutputMaterializer()
                    },
                    handlers),
                mass,
                capacity,
                destinations,
                admission,
                occupancy,
                admission,
                Publication,
                Routing,
                massExplanations: null,
                completionObservations: CompletionFacts,
                completionRecovery: CompletionFacts,
                calendar: Calendar);
        }

        public void Dispose() => CommandFacts.Dispose();
    }

    private sealed class Eec04ObservedCareerLifeEvents :
        IObservedCareerLifeEventCommand
    {
        internal int DeclaredLossCount { get; private set; }

        public bool TryCaptureProductionDeclaredLoss(
            ProductionDeclaredLossCycleReceipt source,
            out bool stateChanged,
            out string failureReason)
        {
            DeclaredLossCount++;
            stateChanged = true;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCaptureLastLesson(
            CareerMentorshipSnapshot mentorship,
            CharacterCareerSnapshot retirement,
            CombatEquipmentInstance protectiveEquipment,
            CareerMentorshipAwardCommitReceipt award,
            int absoluteDay,
            out bool stateChanged,
            out string failureReason)
        {
            stateChanged = false;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCaptureQuietPromotion(
            CharacterProficiencyAwardCommitReceipt source,
            out bool stateChanged,
            out string failureReason)
        {
            stateChanged = false;
            failureReason = string.Empty;
            return true;
        }
    }
}
#endif
