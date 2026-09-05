#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class ProductionEconomyDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Economy/Run Production Economy Contracts")]
    public static void RunFromMenu()
    {
        RunAll();
    }

    public static void RunAll()
    {
        ValidateAuthoredContent();
        ValidateCanonicalProductionOutputLines();
        ProductionPreparedOutputComponentCodecDebugScenarios.RunAll();
        ProductionAmmunitionPreparedOutputDebugScenarios.RunAll();
        ValidateProcessLossPreparedOutputRealAdapter();
        ValidateRuinedPreparedOutputMaximumProof();
        ValidateSubstanceSingleAuthority();
        ValidateProductionFacilityEnumerationBoundary();
        ValidatePhysicalStockSensorInstallation();
        ValidateProductionInputBufferMassAdmission();
        ValidateZeroInputSourceBillInputAuthority();
        ValidateProductionInputClaimIdempotentRevoke();
        ProductionActiveMultiFacilityRetargetDebugScenarios.VerifyFromMenu();
        ValidateTerminalPreparedOutputBillRetirement();
        ValidateTerminalExactOutputBillRetirement();
        ProductionPreparedOutputDeliveryCallerDebugScenarios.RunAll();
        ProductionPreparedOutputDeliveryCoordinatorDebugScenarios.RunAll();
        PreparedOutputExactDestinationAdmissionCandidateDebugScenarios.RunAll();
        ExactWarehouseHaulAdmissionJoinDebugScenarios.RunAll();
        V27FacilityBufferOwnerManifestDebugScenarios.RequireClassificationCoverage();
        ValidatePhysicalProductionBill();
        ValidateProcessFluidReceiptAggregation();
        ValidatePassiveBatchProduction();
        ValidateEconomyPlanning();
        ValidateGrandProjectPhysicalTransaction();
        ValidateEconomyPlanningLateFailureDiscard();
        Debug.Log("Production economy contracts passed.");
    }

    [MenuItem("DungeonStory/V27/Production/Verify Frozen Standard Output")]
    public static void RunFrozenStandardOutputFocused()
    {
        ValidatePhysicalProductionBill();
        Debug.Log("Frozen standard production output contracts passed.");
    }

    public static void RunPreparedOutputRealAdapterFocused()
    {
        ValidateProcessLossPreparedOutputRealAdapter();
        Debug.Log("Prepared-output real adapter contracts passed.");
    }

    public static void RunZeroInputSourceBillFocused()
    {
        ValidateZeroInputSourceBillInputAuthority();
        Debug.Log("V27_ZERO_INPUT_SOURCE_BILL=PASS");
    }

    private static void ValidateCanonicalProductionOutputLines()
    {
        (string AssetPath, string OutputLineId)[] migratedPreparedOutputAssets =
        {
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_charcoal.asset",
                "output:recipe:charcoal/000/main/material:charcoal"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_dog_food.asset",
                "output:recipe:dog-food/000/main/feed:dog-food"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_dog_food_fresh.asset",
                "output:recipe:dog-food-fresh/000/main/feed:dog-food-fresh"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_hay_feed.asset",
                "output:recipe:hay-feed/000/main/feed:hay"),
            (
                "Assets/Resources/SO/Economy/Recipes/Workshop/recipe_malt.asset",
                "output:recipe:malt/000/main/material:malt"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_milling_flour.asset",
                "output:recipe:milling-flour/000/main/material:flour"),
            (
                "Assets/Resources/SO/Economy/Recipes/Workshop/recipe_silage.asset",
                "output:recipe:silage/000/main/feed:silage"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_sawmill_lumber.asset",
                "output:recipe:sawmill-lumber/000/main/material:lumber"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_starch.asset",
                "output:recipe:starch/000/main/material:starch"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_steel_ingot.asset",
                "output:recipe:steel-ingot/000/main/material:steel-ingot"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_treated_lumber.asset",
                "output:recipe:treated-lumber/000/main/material:treated-lumber")
        };
        foreach ((string assetPath, string outputLineId) in
                 migratedPreparedOutputAssets)
        {
            ProductionRecipeSO migrated =
                AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(assetPath);
            Require(
                migrated != null
                && migrated.CaptureCanonicalOutputs().Count == 1
                && string.Equals(
                    migrated.Outputs[0].OutputLineId,
                    outputLineId,
                    StringComparison.Ordinal)
                && migrated.Outputs[0].Role == ProductionOutputRole.Main,
                $"migrated prepared-output line is not canonical: {assetPath}");
        }

        ProductionOutputDefinition valid = new(
            "output:resource:dark-resin",
            ProductionOutputRole.Byproduct,
            "resource:dark-resin",
            1,
            0.18f);
        Require(
            valid.HasCanonicalAuthoredValue
            && !ProductionOutputDefinition.IsCanonicalOutputLineId(
                "output:Main")
            && !ProductionOutputDefinition.IsCanonicalOutputLineId(
                "output:main|other"),
            "production output-line canonical grammar accepted invalid IDs");

        ProductionRecipeSO recipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        try
        {
            recipe.Configure(
                "recipe:qa:output-line",
                "QA output line",
                "QA",
                "workstation:qa",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                1f,
                Array.Empty<ItemAmountDefinition>(),
                new[] { valid });
            Require(
                recipe.CaptureCanonicalOutputs().Count == 1,
                "canonical production output capture failed");

            bool duplicateRejected = false;
            try
            {
                recipe.Configure(
                    "recipe:qa:output-line-duplicate",
                    "QA output line duplicate",
                    "QA",
                    "workstation:qa",
                    BuiltInWorkTypeIds.Craft.Value,
                    string.Empty,
                    1f,
                    Array.Empty<ItemAmountDefinition>(),
                    new[]
                    {
                        valid,
                        new ProductionOutputDefinition(
                            "output:resource:dark-resin",
                            ProductionOutputRole.Byproduct,
                            "resource:coal",
                            1)
                    });
            }
            catch (InvalidOperationException)
            {
                duplicateRejected = true;
            }

            Require(
                duplicateRejected,
                "duplicate production output-line ID was accepted");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void ValidateProcessLossPreparedOutputRealAdapter()
    {
        const string recipeId = "recipe:bowstring-fiber";
        const string outputItemId = "material:bowstring";
        const string facilityId = "building:qa:bowstring-prepared-output";
        const string workerId = "character:qa:bowstring-prepared-output";
        const string billId = "production-bill:1";
        const long expectedUnitMassGrams = 80L;
        const long expectedBatchMassGrams = 80L;
        const long expectedDeclaredLossMassGrams = 160L;
        const long expectedCapacityGrams = 4_000L;

        ResourceEconomyContentCatalog authoredCatalog = LoadCatalog();
        Require(
            authoredCatalog.TryGetRecipe(recipeId, out ProductionRecipeSO recipe),
            "bowstring prepared-output recipe is missing");
        Require(
            authoredCatalog.TryGetItem(
                outputItemId,
                out ResourceItemDefinitionSO outputItem),
            "bowstring prepared-output definition is missing");
        ProductionRecipeSO externalInputRecipe =
            UnityEngine.Object.Instantiate(recipe);
        externalInputRecipe.name = "QA External Input Prepared Output";
        externalInputRecipe.Configure(
            "recipe:qa:prepared-output-external-input",
            "QA prepared-output external input",
            "Prepared-output external-input receipt fixture.",
            recipe.FacilityTag,
            recipe.WorkTypeId.Value,
            string.Empty,
            recipe.RequiredWork,
            new[]
            {
                new ItemAmountDefinition("resource:shade-fiber", 1)
            },
            new[]
            {
                new ProductionOutputDefinition(
                    "output:qa:prepared-output-external-input/000/main/"
                    + outputItemId,
                    ProductionOutputRole.Main,
                    outputItemId,
                    5)
            });
        externalInputRecipe.ConfigureMassExplanation(
            ProcessAdditionProductionMassExplanationCapability.Id,
            ProcessAdditionProductionMassExplanationCapability.Version,
            ProcessAdditionProductionMassExplanationCapability.BuildPayload(
                PhysicalMassExternalInputKind.AbstractProcessAddition,
                "qa-external-input"));
        ResourceEconomyContentCatalog catalog = new(
            authoredCatalog.Items,
            authoredCatalog.Recipes.Concat(new[] { externalInputRecipe }),
            authoredCatalog.Crops,
            authoredCatalog.Materials);
        BuildingSO building = LoadAll<BuildingSO>(
                "Assets/Resources/SO/Building")
            .Single(value => value != null
                && string.Equals(
                    value.GetProductionWorkstationAbility()?.WorkstationTag,
                    recipe.WorkstationTag,
                    StringComparison.Ordinal));
        Require(building != null, "Loom definition is missing");

        GameObject facilityObject = new("Sawmill Prepared Output E2E Facility");
        GameObject workerObject = new("Sawmill Prepared Output E2E Worker");
        try
        {
            BuildableObject facility =
                facilityObject.AddComponent<BuildableObject>();
            facility.RestorePersistentIdentity(
                (BuildingInstanceId)facilityId);
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(building, new Vector2Int(11, 4));

            CharacterActor worker = workerObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            worker.EnsureRuntimeState();
            worker.Identity.SetPersistentId(workerId);

            FakeProductionItemGateway items = new();
            ProductionPreparedOutputComponentCodec componentCodec = new();
            ProductionOutputHandlerRegistry outputHandlers =
                CreateOutputHandlerRegistry(
                    catalog,
                    items,
                    componentCodec: componentCodec);
            IProductionWorkshopRuntime workshops =
                EmptyProductionWorkshopRuntime.Instance;
            IWorkforceReplanService workforce =
                NoOpWorkforceReplanService.Instance;
            IProductionInputLogisticsService inputLogistics =
                new ProductionInputLogisticsService(
                    catalog,
                    items,
                    EmptyResearchRuntimeReferences.Instance,
                    workforce,
                    workshops);
            IProductionAssemblyBridge bridge =
                new ProductionAssemblyBridgeAdapter(
                    items,
                    items,
                    items,
                    inputLogistics,
                    new TestProductionCycleUtilityService(
                        workshops,
                        new MutablePowerRuntime()),
                    workshops,
                    new FixedBuildingWorldQuery(facility),
                    EmptyWarehouseWorldQuery.Instance,
                    workforce,
                    outputHandlers,
                    narrativeQualification: null,
                    performance: () =>
                        CharacterAiEditorTestDependencies.NeutralPerformance);
            ProductionFacilityHandle facilityHandle =
                bridge.CaptureFacility(facility);
            ProductionWorkerHandle workerHandle = bridge.CaptureWorker(worker);
            Require(
                string.Equals(
                    facilityHandle.WorkstationTag,
                    recipe.WorkstationTag,
                    StringComparison.Ordinal)
                && facilityHandle.OutputBufferCycleCapacity == 4,
                "Loom bowstring semantic capacity authority is invalid");

            IProductionOutputPlanningService outputPlanning =
                new ProductionOutputPlanningService(catalog, bridge);
            IDungeonItemCatalogProvider itemCatalog =
                EditorItemCatalogFactory.Create();
            IPhysicalItemMassQuery massQuery =
                new PhysicalItemMassQuery(itemCatalog);
            Require(
                massQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)outputItemId).Value ==
                expectedUnitMassGrams,
                "bowstring physical mass authority is not 80g");

            ProductionMaximumOutputFactorCatalog maximumFactors = new(
                LoadAll<BuildingSO>("Assets/Resources/SO/Building"));
            StandardDefinitionProductionOutputCapability maximumCapability = new(
                catalog,
                componentCodec);
            ProductionOutputMaximumMassRegistry maximumMass = new(
                new IProductionOutputMaximumMassCapability[]
                {
                    new PerishableFoodOutputCapability(
                        new ResourceItemDefinitionCatalog(catalog.Items)),
                    maximumCapability
                },
                massQuery);
            ProductionOutputBufferCapacityProjector capacityProjector = new(
                catalog,
                bridge,
                maximumFactors,
                componentCodec,
                massQuery,
                maximumMass);
            DungeonRuntimeAggregateRootStore itemStore = new();
            WorldItemRepository repository = new(
                new GuidPersistentIdGenerator(),
                itemStore);
            ItemQuantityReservationService quantityReservations = new(
                repository,
                EditorNullItemMarkerPresenter.Instance,
                new UnityGameClock());
            FacilityBufferPhysicalOccupancyQuery occupancy = new(
                repository,
                massQuery,
                quantityReservations);
            FacilityBufferDestinationClaimRegistry claims = new();
            FacilityBufferMassAdmissionService admission = new(
                claims,
                occupancy,
                massQuery);
            FacilityBufferDestinationLifecycleService lifecycle = new(
                claims,
                claims,
                admission,
                admission);
            ProductionOutputDestinationAuthorityRuntime destinations = new(
                claims,
                admission,
                claims,
                admission,
                lifecycle);
            FacilityBufferPlannedOutputPublicationService publication = new(
                repository,
                itemCatalog,
                massQuery,
                admission);
            ProductionPreparedOutputRoutingAuthority routing = new();
            ProductionPreparedOutputExecutionAdapter adapter = new(
                catalog,
                outputPlanning,
                bridge,
                EmptyGrandProjectBenefitQuery.Instance,
                new CanonicalProductionOutputResolver(
                    new RandomStreamProvider(9024)),
                new ProductionPreparedOutputMaterializerRegistry(
                    new IProductionPreparedOutputMaterializer[]
                    {
                        new PerishableFoodPreparedOutputMaterializer(),
                        componentCodec
                    },
                    outputHandlers),
                massQuery,
                capacityProjector,
                destinations,
                admission,
                occupancy,
                admission,
                publication,
                routing);

            ProductionBillRecord record = CreatePreparedOutputRestoreRecord(
                billId,
                recipe,
                facilityHandle,
                resolvedBatch: null);
            string unresolvedBeforeCapacity = string.Join(
                "|",
                record.materialsConsumed,
                record.wipInputCommitId,
                record.wipInputQuantity,
                record.wipInputMassGrams,
                record.outputOutcomeResolved,
                record.resolvedOutputs.Count,
                JsonUtility.ToJson(record.preparedOutput));
            ProductionPreparedOutputCapacityResult cycleStartCapacity =
                adapter.AssessCycleStart(record, recipe, facilityHandle);
            Require(
                cycleStartCapacity.IsValid
                && cycleStartCapacity.CanBeginCycle
                && cycleStartCapacity.MaximumMassGrams == expectedCapacityGrams
                && record.preparedOutput.phase ==
                    ProductionPreparedOutputPhase.Unresolved
                && !record.materialsConsumed
                && record.wipInputMassGrams == 0L
                && string.Equals(
                    string.Join(
                        "|",
                        record.materialsConsumed,
                        record.wipInputCommitId,
                        record.wipInputQuantity,
                        record.wipInputMassGrams,
                        record.outputOutcomeResolved,
                        record.resolvedOutputs.Count,
                        JsonUtility.ToJson(record.preparedOutput)),
                    unresolvedBeforeCapacity,
                    StringComparison.Ordinal),
                "cycle-start capacity assessment resolved output or required pre-WIP input mass");
            long inputMassGrams = checked(
                massQuery.GetQuantityMass(
                    (ItemDefinitionId)"resource:shade-fiber",
                    PhysicalItemMassSubject.ForDefinition(
                        (ItemDefinitionId)"resource:shade-fiber"),
                    2).Value);
            record.SetMaterialsConsumed(true);
            record.SetWipInput(new ProductionWipInputReceipt(
                "production-wip-input:production-bill:1:00000001",
                2,
                inputMassGrams));

            ProductionPreparedOutputExecutionResult result = adapter.Execute(
                record,
                recipe,
                facilityHandle,
                workerHandle);
            Require(
                result.IsValid
                && result.CycleOutputCompleted
                && result.Phase == ProductionPreparedOutputPhase.Completed,
                "bowstring prepared-output execution failed: "
                + result.Failure.Code
                + "/"
                + string.Join(",", result.Failure.Parameters.ToArray()));

            ProductionPreparedOutputBatchSaveData completed =
                record.preparedOutput;
            Require(
                completed.schemaVersion ==
                    ProductionPreparedOutputBatchSaveData.CurrentSchemaVersion
                && completed.schemaVersion ==
                    ProductionPreparedOutputBatchSaveData.CurrentSchemaVersion
                && completed.totalPhysicalMassGrams ==
                    expectedBatchMassGrams
                && completed.totalDeclaredLossMassGrams ==
                    expectedDeclaredLossMassGrams
                && completed.outputBufferCycleCapacity == 4
                && completed.projectedPortfolioCapacityGrams ==
                    expectedCapacityGrams
                && completed.requiredMinimumCapacityGrams ==
                    expectedCapacityGrams,
                "bowstring prepared-output mass/capacity contract drifted: physical="
                + completed.totalPhysicalMassGrams
                + ", loss=" + completed.totalDeclaredLossMassGrams
                + ", cycles=" + completed.outputBufferCycleCapacity
                + ", projected=" + completed.projectedPortfolioCapacityGrams
                + ", required=" + completed.requiredMinimumCapacityGrams);
            ProductionPreparedOutputLineSaveData line =
                completed.lines.Single(value =>
                    ProductionOutputRoleRules.IsPhysical(value.role));
            ProductionPreparedOutputLineSaveData loss =
                completed.lines.Single(value =>
                    value.role == ProductionOutputRole.DeclaredLoss);
            ProductionPreparedOutputComponentProjection decoded =
                componentCodec.ValidateAndDecode(
                    outputItem,
                    line.componentPayload,
                    line.componentFingerprint);
            Require(
                string.Equals(line.itemId, outputItemId, StringComparison.Ordinal)
                && line.quantity == 1
                && line.exactMassGrams == expectedBatchMassGrams
                && decoded.RuntimeComponents.Count == 0
                && massQuery.GetQuantityMass(
                        (ItemDefinitionId)outputItemId,
                        decoded.MassSubject,
                        line.quantity).Value == expectedBatchMassGrams
                && loss.quantity == 0
                && loss.itemId.Length == 0
                && loss.exactMassGrams == expectedDeclaredLossMassGrams
                && loss.rollKind == "process-loss"
                && loss.componentPayload.Contains(
                    "lossKind=CuttingWaste",
                    StringComparison.Ordinal),
                "bowstring output or process-loss receipt drifted");

            string destinationId = record.outputDestinationId;
            Require(
                occupancy.Capture(destinationId).TotalMassGrams ==
                    expectedBatchMassGrams
                && admission.TryGetCapacity(
                    destinationId,
                    facilityHandle.Position,
                    out FacilityBufferMassCapacitySnapshot capacity)
                && capacity.Profile.MaxMassGrams == expectedCapacityGrams
                && capacity.ReservedMassGrams == 0L,
                "bowstring output buffer did not commit exact physical capacity");
            FacilityBufferPlannedOutputPublicationEditorSnapshot physical =
                publication.CaptureEditorTestSnapshot();
            Require(
                physical.Stacks.Count == 1
                && physical.Stacks[0].Quantity == 1
                && physical.Stacks[0].State ==
                    WorldItemStackState.FacilityOutputBuffer
                && string.Equals(
                    physical.Stacks[0].DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && physical.Stacks[0].MarkerCount == 1
                && !physical.Stacks[0].MarkerAffectsStacking
                && !publication.TryCapturePendingBatch(
                    completed.batchCommitId,
                    out _,
                    out _,
                    out _),
                "bowstring output publication did not leave one durable physical stack");
            ProductionPreparedOutputRoutingLineSnapshot routed =
                routing.CaptureBill(record.billId).Single();
            Require(
                string.Equals(routed.ItemId, outputItemId, StringComparison.Ordinal)
                && routed.OriginalQuantity == 1
                && routed.OriginalMassGrams == expectedBatchMassGrams
                && routed.RemainingQuantity == 1
                && routed.RemainingMassGrams == expectedBatchMassGrams,
                "bowstring routing authority did not capture exact output");

            string json = JsonUtility.ToJson(completed);
            ProductionPreparedOutputBatchSaveData roundTripped =
                JsonUtility.FromJson<ProductionPreparedOutputBatchSaveData>(json);
            Require(
                roundTripped != null
                && roundTripped.schemaVersion ==
                    ProductionPreparedOutputBatchSaveData.CurrentSchemaVersion
                && string.Equals(
                    JsonUtility.ToJson(roundTripped),
                    json,
                    StringComparison.Ordinal),
                "bowstring prepared-output batch did not round-trip deterministically");
            int stackCountBeforeReplay = publication.CaptureEditorTestSnapshot()
                .Stacks.Count;
            ProductionPreparedOutputExecutionResult completedReplay =
                adapter.Execute(
                    record,
                    recipe,
                    facilityHandle,
                    workerHandle);
            Require(
                completedReplay.IsValid
                && completedReplay.CycleOutputCompleted
                && completedReplay.Phase == ProductionPreparedOutputPhase.Completed
                && record.preparedOutput.totalDeclaredLossMassGrams
                    == expectedDeclaredLossMassGrams
                && publication.CaptureEditorTestSnapshot().Stacks.Count
                    == stackCountBeforeReplay,
                "Frozen process-loss replay duplicated or drifted output after save round-trip validation.");
            ProductionPreparedOutputBatchSaveData restoreBatch =
                PrepareWaitingRestoreBatch(roundTripped);
            ProductionBillRecord restoreRecord =
                CreatePreparedOutputRestoreRecord(
                    billId,
                    recipe,
                    facilityHandle,
                    restoreBatch);
            adapter.RestoreDestinationAuthorities(
                new[] { restoreRecord },
                new[] { facilityHandle });

            string baselineAuthorityState = CapturePreparedOutputAuthorityState(
                claims,
                admission,
                occupancy,
                publication,
                destinationId,
                facilityHandle.Position);

            void RequireStaleRestoreRejected(
                ProductionPreparedOutputBatchSaveData staleBatch,
                string scenario)
            {
                ProductionBillRecord staleRecord =
                    CreatePreparedOutputRestoreRecord(
                        billId,
                        recipe,
                        facilityHandle,
                        staleBatch);
                bool rejected = false;
                try
                {
                    adapter.RestoreDestinationAuthorities(
                        new[] { staleRecord },
                        new[] { facilityHandle });
                }
                catch (InvalidOperationException exception)
                {
                    rejected = exception.Message.Contains(
                        ProductionOutputBufferCapacitySourceGuard
                            .StaleFailureToken,
                        StringComparison.Ordinal);
                }
                Require(rejected, scenario + " was not rejected as stale");
                Require(
                    string.Equals(
                        CapturePreparedOutputAuthorityState(
                            claims,
                            admission,
                            occupancy,
                            publication,
                            destinationId,
                            facilityHandle.Position),
                        baselineAuthorityState,
                        StringComparison.Ordinal),
                    scenario + " mutated claim/profile/physical authority");
            }

            ProductionPreparedOutputBatchSaveData staleDigest =
                restoreBatch.Clone();
            staleDigest.capacitySourceDigest = string.Equals(
                    staleDigest.capacitySourceDigest,
                    new string('a', 64),
                    StringComparison.Ordinal)
                ? new string('b', 64)
                : new string('a', 64);
            RequireStaleRestoreRejected(
                staleDigest,
                "valid-format capacity digest mutation");

            ProductionPreparedOutputBatchSaveData staleCycle =
                restoreBatch.Clone();
            staleCycle.outputBufferCycleCapacity = 3;
            staleCycle.projectedPortfolioCapacityGrams = 4_000L;
            staleCycle.requiredMinimumCapacityGrams = 4_000L;
            RequireStaleRestoreRejected(
                staleCycle,
                "three-cycle capacity mutation");

            const string externalBillId = "production-bill:2";
            ProductionBillRecord externalRecord =
                CreatePreparedOutputRestoreRecord(
                    externalBillId,
                    externalInputRecipe,
                    facilityHandle,
                    resolvedBatch: null);
            externalRecord.SetMaterialsConsumed(true);
            externalRecord.SetWipInput(new ProductionWipInputReceipt(
                "production-wip-input:production-bill:2:00000001",
                1,
                200L));
            ProductionPreparedOutputExecutionResult externalResult =
                adapter.Execute(
                    externalRecord,
                    externalInputRecipe,
                    facilityHandle,
                    workerHandle);
            Require(
                externalResult.IsValid
                && externalResult.CycleOutputCompleted
                && externalResult.Phase ==
                    ProductionPreparedOutputPhase.Completed,
                "declared external-input prepared-output execution failed: "
                + externalResult.Failure.Code
                + "/"
                + string.Join(",", externalResult.Failure.Parameters.ToArray()));
            ProductionPreparedOutputBatchSaveData externalBatch =
                externalRecord.preparedOutput;
            ProductionPreparedOutputLineSaveData externalReceipt =
                externalBatch.lines.Single(value =>
                    value.role ==
                        ProductionOutputRole.DeclaredExternalInput);
            Require(
                externalBatch.totalPhysicalMassGrams == 400L
                && externalBatch.totalDeclaredLossMassGrams == 0L
                && externalBatch.totalDeclaredExternalInputMassGrams == 200L
                && externalReceipt.itemId.Length == 0
                && externalReceipt.quantity == 0
                && externalReceipt.exactMassGrams == 200L
                && externalReceipt.rollKind == "process-addition"
                && externalReceipt.componentPayload.Contains(
                    "externalInputKind=AbstractProcessAddition",
                    StringComparison.Ordinal)
                && routing.CaptureBill(externalRecord.billId).Count == 1
                && ((IProductionPreparedOutputRoutingBatchQuery)routing)
                    .TryCaptureBatch(
                        externalBatch.batchCommitId,
                        out ProductionPreparedOutputRoutingBatchSnapshot
                            externalRouting)
                && externalRouting.TotalDeclaredExternalInputMassGrams ==
                    200L
                && externalRouting.NonPhysicalDispositions.Single(value =>
                        value.Role ==
                            ProductionOutputRole.DeclaredExternalInput)
                    .DispositionFingerprint ==
                    externalReceipt.componentFingerprint,
                "declared external-input receipt was not persisted exactly or entered physical routing");

            string externalJson = JsonUtility.ToJson(externalBatch);
            ProductionPreparedOutputBatchSaveData externalRoundTrip =
                JsonUtility.FromJson<ProductionPreparedOutputBatchSaveData>(
                    externalJson);
            ProductionPreparedOutputContract.ValidateForBill(
                externalRoundTrip,
                externalRecord.billId,
                externalInputRecipe.RecipeId,
                externalRecord.cycleSequence,
                externalRecord.outputDestinationId);
            Require(
                string.Equals(
                    externalJson,
                    JsonUtility.ToJson(externalRoundTrip),
                    StringComparison.Ordinal),
                "declared external-input prepared-output receipt did not round-trip exactly");
            ProductionPreparedOutputBatchSaveData externalDrift =
                externalRoundTrip.Clone();
            externalDrift.lines.Single(value => value.role ==
                    ProductionOutputRole.DeclaredExternalInput)
                .exactMassGrams++;
            bool externalDriftRejected = false;
            try
            {
                ProductionPreparedOutputContract.ValidateForBill(
                    externalDrift,
                    externalRecord.billId,
                    externalInputRecipe.RecipeId,
                    externalRecord.cycleSequence,
                    externalRecord.outputDestinationId);
            }
            catch (InvalidOperationException)
            {
                externalDriftRejected = true;
            }
            Require(
                externalDriftRejected,
                "declared external-input receipt drift was accepted");

            // OPEN: a later fixture must rebuild and restore the complete
            // Production/Physical/Routing persistence graph. This focused slice
            // deliberately proves only the real adapter and its capacity-source
            // restore gate.
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(externalInputRecipe);
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private static void ValidateRuinedPreparedOutputMaximumProof()
    {
        ResourceEconomyContentCatalog authoredCatalog = LoadCatalog();
        ProductionRecipeSO authoredRecipe = authoredCatalog.Recipes.Single(value =>
            string.Equals(
                value.RecipeId,
                "recipe:silage",
                StringComparison.Ordinal));
        ProductionRecipeSO recipe =
            ProductionRuinedOutputCapacityQaFixtureFactory.CreateRecipe(
                authoredRecipe);
        ResourceEconomyContentCatalog catalog = new(
            authoredCatalog.Items,
            authoredCatalog.Recipes.Concat(new[] { recipe }),
            authoredCatalog.Crops,
            authoredCatalog.Materials);
        BuildingSO building = LoadAll<BuildingSO>(
                "Assets/Resources/SO/Building")
            .Single(value => value != null
                && string.Equals(
                    value.GetProductionWorkstationAbility()?.WorkstationTag,
                    recipe.WorkstationTag,
                    StringComparison.Ordinal));
        GameObject facilityObject = new(
            "Ruined Prepared Output Maximum Proof Facility");
        try
        {
            BuildableObject facility = facilityObject
                .AddComponent<BuildableObject>();
            facility.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:ruined-output-proof");
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(building, new Vector2Int(19, 7));

            FakeProductionItemGateway items = new();
            ProductionPreparedOutputComponentCodec componentCodec = new();
            ProductionOutputHandlerRegistry outputHandlers =
                CreateOutputHandlerRegistry(
                    catalog,
                    items,
                    componentCodec: componentCodec);
            IProductionWorkshopRuntime workshops =
                EmptyProductionWorkshopRuntime.Instance;
            IWorkforceReplanService workforce =
                NoOpWorkforceReplanService.Instance;
            IProductionInputLogisticsService inputLogistics =
                new ProductionInputLogisticsService(
                    catalog,
                    items,
                    EmptyResearchRuntimeReferences.Instance,
                    workforce,
                    workshops);
            IProductionAssemblyBridge bridge =
                new ProductionAssemblyBridgeAdapter(
                    items,
                    items,
                    items,
                    inputLogistics,
                    new TestProductionCycleUtilityService(
                        workshops,
                        new MutablePowerRuntime()),
                    workshops,
                    new FixedBuildingWorldQuery(facility),
                    EmptyWarehouseWorldQuery.Instance,
                    workforce,
                    outputHandlers,
                    narrativeQualification: null,
                    performance: () =>
                        CharacterAiEditorTestDependencies.NeutralPerformance);
            ProductionFacilityHandle facilityHandle =
                bridge.CaptureFacility(facility);
            IDungeonItemCatalogProvider itemCatalog =
                EditorItemCatalogFactory.Create();
            IPhysicalItemMassQuery massQuery =
                new PhysicalItemMassQuery(itemCatalog);
            ProductionMaximumOutputFactorCatalog maximumFactors = new(
                LoadAll<BuildingSO>("Assets/Resources/SO/Building"));
            ProductionOutputMaximumMassRegistry maximumMass = new(
                new IProductionOutputMaximumMassCapability[]
                {
                    new PerishableFoodOutputCapability(
                        new ResourceItemDefinitionCatalog(catalog.Items)),
                    new StandardDefinitionProductionOutputCapability(
                        catalog,
                        componentCodec)
                },
                massQuery);
            ProductionOutputBufferCapacityProjector capacityProjector = new(
                catalog,
                bridge,
                maximumFactors,
                componentCodec,
                massQuery,
                maximumMass);
            DungeonRuntimeAggregateRootStore itemStore = new();
            WorldItemRepository repository = new(
                new GuidPersistentIdGenerator(),
                itemStore);
            ItemQuantityReservationService quantityReservations = new(
                repository,
                EditorNullItemMarkerPresenter.Instance,
                new UnityGameClock());
            FacilityBufferPhysicalOccupancyQuery occupancy = new(
                repository,
                massQuery,
                quantityReservations);
            FacilityBufferDestinationClaimRegistry claims = new();
            FacilityBufferMassAdmissionService admission = new(
                claims,
                occupancy,
                massQuery);
            FacilityBufferDestinationLifecycleService lifecycle = new(
                claims,
                claims,
                admission,
                admission);
            ProductionOutputDestinationAuthorityRuntime destinations = new(
                claims,
                admission,
                claims,
                admission,
                lifecycle);
            FacilityBufferPlannedOutputPublicationService publication = new(
                repository,
                itemCatalog,
                massQuery,
                admission);
            ProductionPreparedOutputRoutingAuthority routing = new();
            ProductionPreparedOutputExecutionAdapter adapter = new(
                catalog,
                new ProductionOutputPlanningService(catalog, bridge),
                bridge,
                EmptyGrandProjectBenefitQuery.Instance,
                new CanonicalProductionOutputResolver(
                    new RandomStreamProvider(19473)),
                new ProductionPreparedOutputMaterializerRegistry(
                    new IProductionPreparedOutputMaterializer[]
                    {
                        new PerishableFoodPreparedOutputMaterializer(),
                        componentCodec
                    },
                    outputHandlers),
                massQuery,
                capacityProjector,
                destinations,
                admission,
                occupancy,
                admission,
                publication,
                routing);

            ProductionBillRecord record = CreatePreparedOutputRestoreRecord(
                "production-bill:ruined-output-proof",
                recipe,
                facilityHandle,
                resolvedBatch: null);
            ProductionRuinedOutputCapacityQaFixtureFactory.ApplyRuinedState(
                record,
                recipe,
                massQuery);
            ProductionRuinedBatchExecutionResult result = adapter
                .ExecuteRuinedBatch(record, recipe, facilityHandle);
            Require(
                result.IsValid
                && result.BatchDispositionCompleted
                && result.Phase == ProductionPreparedOutputPhase.Completed,
                "Ruined prepared-output proof execution failed: "
                + result.Failure.Code
                + "/"
                + string.Join(",", result.Failure.Parameters.ToArray()));

            ProductionPreparedOutputBatchSaveData completed =
                record.preparedOutput;
            Require(
                completed.totalPhysicalMassGrams ==
                    ProductionRuinedOutputCapacityQaExpectations
                        .RecoverableWasteMassGrams
                && completed.totalDeclaredLossMassGrams ==
                    ProductionRuinedOutputCapacityQaExpectations
                        .DeclaredLossMassGrams
                && completed.maximumBatchMassGrams ==
                    ProductionRuinedOutputCapacityQaExpectations
                        .RecoverableWasteMassGrams
                && completed.maximumMassProofDigest.Length == 64
                && completed.capacityClaimDigest.Length == 64
                && completed.requiredMinimumCapacityGrams ==
                    ProductionRuinedOutputCapacityQaExpectations
                        .RequiredMinimumCapacityGrams
                && completed.lines.Single(value => value.role ==
                    ProductionOutputRole.RecoverableWaste).quantity ==
                    ProductionRuinedOutputCapacityQaExpectations
                        .RecoverableWasteQuantity,
                "Ruined prepared output did not persist the QA-authored 2,400g WIP plus 600g process-water and 300g wastewater disposition as exact 4x600g waste and 300g declared loss: "
                + $"physical={completed.totalPhysicalMassGrams}, "
                + $"loss={completed.totalDeclaredLossMassGrams}, "
                + $"maximum={completed.maximumBatchMassGrams}, "
                + $"required={completed.requiredMinimumCapacityGrams}, "
                + $"wasteQuantity={completed.lines.Single(value => value.role == ProductionOutputRole.RecoverableWaste).quantity}.");
            Require(
                occupancy.Capture(completed.destinationId).TotalMassGrams ==
                    ProductionRuinedOutputCapacityQaExpectations
                        .RecoverableWasteMassGrams
                && admission.TryGetCapacity(
                    completed.destinationId,
                    facilityHandle.Position,
                    out FacilityBufferMassCapacitySnapshot liveCapacity)
                && liveCapacity.Profile.MaxMassGrams ==
                    ProductionRuinedOutputCapacityQaExpectations
                        .RequiredMinimumCapacityGrams,
                "Ruined prepared output publication did not use the proof-sized FacilityBuffer.");

            VerifyRetiredMultiUnitRuinedTerminalCapacity(
                record,
                completed,
                building,
                facilityHandle,
                capacityProjector,
                massQuery);

            ProductionPreparedOutputBatchSaveData waiting =
                PrepareWaitingRestoreBatch(completed);
            ProductionBillRecord restored = CreatePreparedOutputRestoreRecord(
                record.billId.Value,
                recipe,
                facilityHandle,
                waiting);
            ProductionRuinedOutputCapacityQaFixtureFactory.ApplyRuinedState(
                restored,
                recipe,
                massQuery);
            adapter.RestoreDestinationAuthorities(
                new[] { restored },
                new[] { facilityHandle });

            VerifyRuinedProofTamperRejected(
                adapter,
                recipe,
                facilityHandle,
                waiting,
                massQuery,
                candidate => candidate.maximumMassProofDigest =
                    new string('f', 64),
                "proof digest");
            VerifyRuinedProofTamperRejected(
                adapter,
                recipe,
                facilityHandle,
                waiting,
                massQuery,
                candidate => candidate.maximumBatchMassGrams += 600L,
                "proof mass");
            VerifyRuinedProofTamperRejected(
                adapter,
                recipe,
                facilityHandle,
                waiting,
                massQuery,
                candidate => candidate.capacityClaimDigest =
                    new string('e', 64),
                "claim digest");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyRuinedProofTamperRejected(
        ProductionPreparedOutputExecutionAdapter adapter,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionPreparedOutputBatchSaveData source,
        IPhysicalItemMassQuery massQuery,
        Action<ProductionPreparedOutputBatchSaveData> tamper,
        string label)
    {
        ProductionPreparedOutputBatchSaveData candidate = source.Clone();
        tamper(candidate);
        bool rejected = false;
        try
        {
            ProductionBillRecord restored = CreatePreparedOutputRestoreRecord(
                source.billId,
                recipe,
                facility,
                candidate);
            ProductionRuinedOutputCapacityQaFixtureFactory.ApplyRuinedState(
                restored,
                recipe,
                massQuery);
            adapter.RestoreDestinationAuthorities(
                new[] { restored },
                new[] { facility });
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected,
            "Ruined prepared-output " + label + " tamper was accepted.");
    }

    private static void VerifyRetiredMultiUnitRuinedTerminalCapacity(
        ProductionBillRecord record,
        ProductionPreparedOutputBatchSaveData completed,
        BuildingSO building,
        ProductionFacilityHandle facility,
        ProductionOutputBufferCapacityProjector capacityProjector,
        IPhysicalItemMassQuery massQuery)
    {
        ProductionBillSaveData sourceBill = new()
        {
            billId = record.billId.Value,
            recipeId = record.recipeId,
            buildingInstanceId = record.buildingInstanceId.Value,
            mode = record.mode,
            remainingCycles = record.remainingCycles,
            targetStock = record.targetStock,
            materialsConsumed = record.materialsConsumed,
            cycleSequence = record.cycleSequence,
            wipInputCommitId = record.wipInputCommitId,
            wipInputQuantity = record.wipInputQuantity,
            wipInputMassGrams = record.wipInputMassGrams,
            outputOutcomeResolved = record.outputOutcomeResolved,
            resolvedOutputs = record.resolvedOutputs
                .Select(value => value.Clone())
                .ToList(),
            preparedOutput = completed.Clone(),
            processFluidConsumed = record.processFluidConsumed,
            processCleanWaterMassGrams = record.processCleanWaterMassGrams,
            processWastewaterMassGrams = record.processWastewaterMassGrams,
            processWastewaterComponents = record.processWastewaterComponents
                .Select(value => value.Clone())
                .ToList(),
            processManualWaterTransfers = record.processManualWaterTransfers
                .Select(value => value.Clone())
                .ToList(),
            batchStage = record.batchStage,
            batchIntegrity = record.batchIntegrity,
            materialDestinationId = record.materialDestinationId,
            outputDestinationId = record.outputDestinationId
        };
        ProductionGenericBillTerminalDrainSaveData terminal =
            CreateCommittedTerminalSource(sourceBill);
        DungeonProductionGenericBillTerminalDrainSaveData terminalPayload =
            new()
            {
                version = DungeonProductionGenericBillTerminalDrainSaveData
                    .CurrentVersion,
                entries = new List<
                    ProductionGenericBillTerminalDrainSaveData>
                {
                    terminal
                }
            };
        ProductionPreparedOutputRoutingBatchSaveData routingBatch =
            CreateRoutingBatch(completed, sourceBill);
        ProductionPreparedOutputRoutingSaveData routing = new()
        {
            batches = new List<ProductionPreparedOutputRoutingBatchSaveData>
            {
                routingBatch
            }
        };
        ModularFacilityWorldSaveData world = new()
        {
            buildings = new List<ModularFacilityBuildingSaveData>
            {
                new()
                {
                    persistentInstanceId = facility.InstanceId.Value,
                    buildingId = building.id,
                    centerX = facility.Position.x,
                    centerY = facility.Position.y
                }
            }
        };

        ProductionOutputCapacityDurableProjection projection =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRoutingFromSave(
                    facility.InstanceId,
                    world,
                    new DungeonProductionBillSaveData(),
                    terminalPayload,
                    new DungeonPhysicalItemSaveData(),
                    new DungeonCharacterWorldSaveData(),
                    routing,
                    Array.Empty<FacilityOutputExactRouteOutboxSaveData>(),
                    new RuinedFixtureBuildingDefinitionLookup(building),
                    capacityProjector,
                    massQuery);
        Require(
            projection.Profile != null
            && projection.Profile.MaxMassGrams ==
                ProductionRuinedOutputCapacityQaExpectations
                    .RequiredMinimumCapacityGrams
            && routingBatch.maximumBatchMassGrams ==
                ProductionRuinedOutputCapacityQaExpectations
                    .RecoverableWasteMassGrams
            && routingBatch.outputBufferCycleCapacity ==
                ProductionRuinedOutputCapacityQaExpectations
                    .OutputBufferCycleCapacity,
            "Retired multi-unit ruined terminal did not reproject the exact 9,600g capacity from its frozen source bill.");

        DungeonProductionGenericBillTerminalDrainSaveData wipDrift =
            terminalPayload.Clone();
        wipDrift.entries[0].sourceBill.wipInputMassGrams += 600L;
        RebindTerminalFingerprints(wipDrift.entries[0]);
        RequireProjectorRejectsWithoutMutation(
            facility.InstanceId,
            world,
            wipDrift,
            routing,
            building,
            capacityProjector,
            massQuery,
            "detached-terminal-routing-proof-stale");

        DungeonProductionGenericBillTerminalDrainSaveData digestDrift =
            terminalPayload.Clone();
        ProductionPreparedOutputBatchSaveData driftPrepared =
            digestDrift.entries[0].sourceBill.preparedOutput;
        driftPrepared.maximumMassProofDigest = new string('d', 64);
        driftPrepared.capacityClaimDigest = new string('e', 64);
        driftPrepared.capacitySourceDigest = new string('f', 64);
        RebindTerminalFingerprints(digestDrift.entries[0]);
        ProductionPreparedOutputRoutingSaveData digestRouting = new()
        {
            batches = new List<ProductionPreparedOutputRoutingBatchSaveData>
            {
                routingBatch.Clone()
            }
        };
        digestRouting.batches[0].maximumMassProofDigest = new string('d', 64);
        digestRouting.batches[0].capacityClaimDigest = new string('e', 64);
        digestRouting.batches[0].capacitySourceDigest = new string('f', 64);
        RequireProjectorRejectsWithoutMutation(
            facility.InstanceId,
            world,
            digestDrift,
            digestRouting,
            building,
            capacityProjector,
            massQuery,
            "detached-terminal-routing-proof-stale");

        ProductionPreparedOutputRoutingSaveData minimumDrift = new()
        {
            batches = new List<ProductionPreparedOutputRoutingBatchSaveData>
            {
                routingBatch.Clone()
            }
        };
        minimumDrift.batches[0].requiredMinimumCapacityGrams += 1L;
        RequireProjectorRejectsWithoutMutation(
            facility.InstanceId,
            world,
            terminalPayload,
            minimumDrift,
            building,
            capacityProjector,
            massQuery,
            "detached-terminal-routing-capacity-source-stale");
    }

    private static ProductionGenericBillTerminalDrainSaveData
        CreateCommittedTerminalSource(ProductionBillSaveData sourceBill)
    {
        ProductionGenericBillTerminalDrainSaveData result = new()
        {
            parentOperationId =
                "production-facility-destructive-drain:qa:ruined-terminal",
            stepOperationId =
                "production-facility-destructive-drain:qa:ruined-terminal:generic",
            ownerStableId = "production-generic-bill:" + sourceBill.billId,
            billId = sourceBill.billId,
            facilityId = sourceBill.buildingInstanceId,
            inputDestinationId = sourceBill.materialDestinationId,
            sourceBill = ProductionGenericBillTerminalDrainCanonical
                .CloneBill(sourceBill),
            inputDestinationDrainStepOperationId =
                "production-facility-destructive-drain:qa:ruined-terminal:generic:input",
            inputDestinationDrainRequestFingerprint = new string('a', 64),
            phase = ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement,
            inputDestinationDrainCommitId =
                "production-input-drain:qa:ruined-terminal",
            inputDestinationDrainReceiptFingerprint = new string('b', 64)
        };
        RebindTerminalFingerprints(result);
        Require(
            ProductionGenericBillTerminalDrainCanonical.IsValidSave(result),
            "Retired ruined terminal fixture is not canonical.");
        return result;
    }

    private static void RebindTerminalFingerprints(
        ProductionGenericBillTerminalDrainSaveData value)
    {
        value.sourceBillFingerprint =
            ProductionGenericBillTerminalDrainCanonical
                .CreateSourceBillFingerprint(value.sourceBill);
        value.requestFingerprint = ProductionGenericBillTerminalDrainCanonical
            .CreateRequestFingerprint(
                value.parentOperationId,
                value.stepOperationId,
                value.ownerStableId,
                value.sourceBill,
                value.inputDestinationDrainStepOperationId,
                value.inputDestinationDrainRequestFingerprint);
        value.wipTerminalCommitId = ProductionGenericBillTerminalDrainCanonical
            .CreateWipTerminalCommitId(
                value.billId,
                value.sourceBill.cycleSequence);
        value.billTerminalEffectFingerprint =
            ProductionGenericBillTerminalDrainCanonical
                .CreateBillTerminalEffectFingerprint(
                    value.requestFingerprint,
                    value.inputDestinationDrainReceiptFingerprint,
                    value.wipTerminalCommitId);
        value.commitId = ProductionGenericBillTerminalDrainCanonical
            .CreateCommitId(value.stepOperationId, value.requestFingerprint);
        value.receiptFingerprint = ProductionGenericBillTerminalDrainCanonical
            .CreateReceiptFingerprint(
                value.requestFingerprint,
                value.inputDestinationDrainReceiptFingerprint,
                value.billTerminalEffectFingerprint,
                value.commitId);
    }

    private static ProductionPreparedOutputRoutingBatchSaveData
        CreateRoutingBatch(
            ProductionPreparedOutputBatchSaveData prepared,
            ProductionBillSaveData sourceBill) => new()
        {
            batchCommitId = prepared.batchCommitId,
            ownerBillId = sourceBill.billId,
            ownerRecipeId = sourceBill.recipeId,
            ownerFacilityId = sourceBill.buildingInstanceId,
            cycleSequence = sourceBill.cycleSequence,
            outcomeFingerprint = prepared.outcomeFingerprint,
            routingFingerprint = new string('c', 64),
            destinationId = prepared.destinationId,
            capacitySourceDigest = prepared.capacitySourceDigest,
            outputBufferCycleCapacity = prepared.outputBufferCycleCapacity,
            projectedPortfolioCapacityGrams =
                prepared.projectedPortfolioCapacityGrams,
            requiredMinimumCapacityGrams =
                prepared.requiredMinimumCapacityGrams,
            maximumMassProofDigest = prepared.maximumMassProofDigest,
            maximumBatchMassGrams = prepared.maximumBatchMassGrams,
            capacityClaimDigest = prepared.capacityClaimDigest,
            totalDeclaredLossMassGrams = prepared.totalDeclaredLossMassGrams,
            nonPhysicalDispositions = prepared.lines
                .Where(value => value != null
                    && value.rollSucceeded
                    && ProductionOutputRoleRules.IsNonPhysical(value.role))
                .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                .Select(value =>
                    new ProductionPreparedOutputNonPhysicalDispositionSaveData
                    {
                        batchCommitId = prepared.batchCommitId,
                        lineCommitId = value.lineCommitId,
                        outputLineId = value.outputLineId,
                        role = value.role,
                        canonicalPayload = value.componentPayload,
                        dispositionFingerprint = value.componentFingerprint,
                        exactMassGrams = value.exactMassGrams
                    })
                .ToList(),
            lines = prepared.lines
                .Where(value => value != null
                    && ProductionOutputRoleRules.IsPhysical(value.role)
                    && value.quantity > 0)
                .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                .Select(value =>
                    new ProductionPreparedOutputRoutingLineSaveData
                    {
                        batchCommitId = prepared.batchCommitId,
                        lineCommitId = value.lineCommitId,
                        outputLineId = value.outputLineId,
                        role = value.role,
                        itemId = value.itemId,
                        destinationId = prepared.destinationId,
                        componentFingerprint = value.componentFingerprint,
                        outputCapabilityId = value.outputCapabilityId,
                        outputCapabilityVersion =
                            value.outputCapabilityVersion,
                        outputComponentCodecId =
                            value.outputComponentCodecId,
                        outputComponentCodecVersion =
                            value.outputComponentCodecVersion,
                        outputCapabilityFingerprint =
                            value.outputCapabilityFingerprint,
                        originalQuantity = value.quantity,
                        remainingQuantity = value.quantity,
                        originalMassGrams = value.exactMassGrams,
                        remainingMassGrams = value.exactMassGrams
                    })
                .ToList()
        };

    private static void RequireProjectorRejectsWithoutMutation(
        BuildingInstanceId facilityId,
        ModularFacilityWorldSaveData world,
        DungeonProductionGenericBillTerminalDrainSaveData terminal,
        ProductionPreparedOutputRoutingSaveData routing,
        BuildingSO building,
        ProductionOutputBufferCapacityProjector capacityProjector,
        IPhysicalItemMassQuery massQuery,
        string expectedToken)
    {
        string beforeTerminal = JsonUtility.ToJson(terminal);
        string beforeRouting = JsonUtility.ToJson(routing);
        bool rejected = false;
        try
        {
            ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRoutingFromSave(
                    facilityId,
                    world,
                    new DungeonProductionBillSaveData(),
                    terminal,
                    new DungeonPhysicalItemSaveData(),
                    new DungeonCharacterWorldSaveData(),
                    routing,
                    Array.Empty<FacilityOutputExactRouteOutboxSaveData>(),
                    new RuinedFixtureBuildingDefinitionLookup(building),
                    capacityProjector,
                    massQuery);
        }
        catch (InvalidOperationException exception)
        {
            rejected = exception.Message.Contains(
                expectedToken,
                StringComparison.Ordinal);
        }
        Require(rejected,
            "Retired ruined terminal tamper did not fail with token '"
            + expectedToken + "'.");
        Require(
            string.Equals(beforeTerminal, JsonUtility.ToJson(terminal),
                StringComparison.Ordinal)
            && string.Equals(beforeRouting, JsonUtility.ToJson(routing),
                StringComparison.Ordinal),
            "Retired ruined terminal projection mutated its raw save input on failure.");
    }

    private sealed class RuinedFixtureBuildingDefinitionLookup :
        IBuildingDefinitionLookup
    {
        private readonly BuildingSO building;

        public RuinedFixtureBuildingDefinitionLookup(BuildingSO building) =>
            this.building = building
                ?? throw new ArgumentNullException(nameof(building));

        public BuildingSO GetBuilding(int id)
        {
            if (building.id != id)
            {
                throw new InvalidOperationException(
                    "Ruined terminal building definition fixture mismatch.");
            }
            return building;
        }
    }

    private static ProductionPreparedOutputBatchSaveData
        PrepareWaitingRestoreBatch(
            ProductionPreparedOutputBatchSaveData source)
    {
        ProductionPreparedOutputBatchSaveData candidate = source?.Clone()
            ?? throw new ArgumentNullException(nameof(source));
        candidate.phase =
            ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace;
        candidate.admissionFingerprint = string.Empty;
        candidate.physicalCandidates.Clear();
        return candidate;
    }

    private static ProductionBillRecord CreatePreparedOutputRestoreRecord(
        string billId,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionPreparedOutputBatchSaveData resolvedBatch)
    {
        ProductionBillId exactBillId = (ProductionBillId)billId;
        ProductionBillRecord record = ProductionBillRecord.Create(
            exactBillId,
            recipe.RecipeId,
            facility.InstanceId,
            ProductionOrderMode.RepeatCount,
            1,
            0,
            ProductionBatchStage.None,
            ProductionBillRuntime.DestinationPrefix + exactBillId.Value);
        record.SetOutputDestination(
            ProductionBillRuntime.OutputDestinationPrefix
            + facility.InstanceId.Value);
        if (resolvedBatch != null)
            record.ResolvePreparedOutput(resolvedBatch);
        return record;
    }

    private static string CapturePreparedOutputAuthorityState(
        FacilityBufferDestinationClaimRegistry claims,
        FacilityBufferMassAdmissionService admission,
        FacilityBufferPhysicalOccupancyQuery occupancy,
        FacilityBufferPlannedOutputPublicationService publication,
        string destinationId,
        Vector2Int position)
    {
        string claimState = string.Join(
            ";",
            claims.CaptureClaims()
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .Select(value => string.Join(
                    "|",
                    value.DestinationId,
                    value.DropPosition.x,
                    value.DropPosition.y,
                    value.OwnerDomain,
                    value.OwnerOperationId,
                    value.OwnerFacilityId,
                    (int)value.AnchorKind)));
        string profileState = string.Join(
            ";",
            admission.CaptureProfiles()
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .Select(value => string.Join(
                    "|",
                    value.DestinationId,
                    value.DropPosition.x,
                    value.DropPosition.y,
                    value.OwnerDomain,
                    value.OwnerOperationId,
                    value.OwnerFacilityId,
                    value.MaxMassGrams,
                    value.CapacityRevision)));
        FacilityBufferPhysicalOccupancySnapshot physical =
            occupancy.Capture(destinationId);
        FacilityBufferPlannedOutputPublicationEditorSnapshot published =
            publication.CaptureEditorTestSnapshot();
        string stackState = string.Join(
            ";",
            published.Stacks
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .Select(value => string.Join(
                    "|",
                    value.StackId,
                    value.Quantity,
                    (int)value.State,
                    value.DestinationId,
                    value.Position.x,
                    value.Position.y,
                    value.MarkerCount,
                    value.MarkerAffectsStacking)));
        Require(
            admission.TryGetCapacity(
                destinationId,
                position,
                out FacilityBufferMassCapacitySnapshot capacity),
            "prepared-output authority snapshot has no capacity");
        return string.Join(
            "#",
            claims.Revision,
            admission.Revision,
            claimState,
            profileState,
            capacity.Profile.MaxMassGrams,
            capacity.ReservedMassGrams,
            physical.NonCarriedMassGrams,
            physical.CommittedCarriedMassGrams,
            published.ItemStackVersion,
            stackState);
    }

    private static void ValidateProductionInputBufferMassAdmission()
    {
        string logisticsSourcePath = Path.Combine(
            Application.dataPath,
            "Scripts",
            "Services",
            "Economy",
            "ProductionInputLogisticsService.cs");
        string logisticsSource = File.ReadAllText(logisticsSourcePath);
        Require(
            CountOrdinalOccurrences(
                logisticsSource,
                "items.RequestDeliveryWithinMassCapacity(") == 0,
            "production input logistics retained a caller-authored mass precheck");
        Require(
            CountOrdinalOccurrences(
                logisticsSource,
                "items.RequestDelivery(") == 1,
            "production input logistics must use the common exact-admission delivery path");
        string warehouseSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Scripts",
            "Services",
            "Items",
            "WorldItemWarehouseService.cs"));
        Require(
            CountOrdinalOccurrences(
                warehouseSource,
                "private bool TryRetargetDeliveryAtomically(") == 1
            && CountOrdinalOccurrences(warehouseSource, "private int RequestLoose(") == 0
            && CountOrdinalOccurrences(warehouseSource, "private int RequestStored(") == 0
            && warehouseSource.Contains(
                "ReservedTargetDestinationIdentity.ProductionInputPrefix",
                StringComparison.Ordinal)
            && CountOrdinalOccurrences(
                warehouseSource,
                "facilityBufferMassAdmission.TryReserveExactLot(") == 1,
            "production item retargeting must use one preflighted exact transaction and retain no partial spawn path");
        string billRuntimeSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Scripts",
            "Models",
            "Economy",
            "Content",
            "ProductionBillRuntime.cs"));
        Require(
            CountOrdinalOccurrences(
                billRuntimeSource,
                "items.TryReleaseDestinationAtomically(") == 4,
            "every production-bill claim-revocation path must close carried delivery ownership atomically");

        ResourceEconomyContentCatalog catalog = LoadCatalog();
        Require(
            catalog.TryGetRecipe(
                "recipe:supply:inoculated-log",
                out ProductionRecipeSO recipe),
            "inoculated-log production recipe is missing");

        FakeProductionItemGateway items = new();
        items.SetDefinitionMass("material:treated-lumber", 1_150L);
        items.SetDefinitionMass("resource:cave-mushroom", 250L);
        ProductionInputLogisticsService logistics = new(
            catalog,
            items,
            EmptyResearchRuntimeReferences.Instance,
            NoOpWorkforceReplanService.Instance,
            EmptyProductionWorkshopRuntime.Instance);
        GameObject gameObject = new("ProductionInputBufferMass_DebugFacility");
        try
        {
            BuildableObject facility = gameObject.AddComponent<BuildableObject>();
            const string destination = "production:production-bill:buffer-mass";
            ProductionBillRecord bill = ProductionBillRecord.Create(
                (ProductionBillId)"production-bill:buffer-mass",
                recipe.RecipeId,
                (BuildingInstanceId)"building:debug-input-buffer-mass",
                ProductionOrderMode.RepeatForever,
                -1,
                0,
                ProductionBatchStage.None,
                destination);
            bill.SetPrefetchPlan(
                productionCycleSeconds: 1f,
                batchCount: 3,
                ProductionLogisticsStatus.None);

            logistics.RequestMissingInputs(bill, recipe, facility);
            Require(
                items.GetRequested("material:treated-lumber") == 3
                && items.GetRequested("resource:cave-mushroom") == 3,
                "production input-buffer prefetch did not request exact three-cycle inputs");
            Require(
                items.CountPendingMassGrams(destination) == 4_200L,
                "production input-buffer mass did not reach exact 4,200g capacity");
            Require(
                logistics.ResolveInputBufferMassCapacity(
                    bill,
                    recipe,
                    facility) == 4_200L,
                "production input-buffer did not derive its exact three-cycle profile mass");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static void ValidateProductionInputClaimIdempotentRevoke()
    {
        const string billId = "production-bill:qa-idempotent-revoke";
        const string facilityId = "building:qa-idempotent-revoke";
        string destinationId = ProductionBillRuntime.DestinationPrefix + billId;
        Vector2Int position = new(5, 2);
        ProductionBillRecord record = ProductionBillRecord.Create(
            (ProductionBillId)billId,
            "recipe:qa-idempotent-revoke",
            (BuildingInstanceId)facilityId,
            ProductionOrderMode.RepeatForever,
            -1,
            0,
            ProductionBatchStage.None,
            destinationId);
        FacilityBufferDestinationClaim claim = new(
            destinationId,
            position,
            ProductionInputDestinationClaimRuntime.OwnerDomain,
            billId,
            facilityId,
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile profile = new(
            destinationId,
            position,
            ProductionInputDestinationClaimRuntime.OwnerDomain,
            billId,
            facilityId,
            new PhysicalMassGrams(1_000L),
            ProductionInputDestinationClaimRuntime
                .InputBufferCapacitySchemaRevision);
        FacilityBufferDestinationClaimRegistry claims = new();
        EmptyFacilityBufferPhysicalOccupancyQuery occupancy = new();
        FacilityBufferMassAdmissionService capacities = new(claims, occupancy);
        FacilityBufferDestinationLifecycleService lifecycle = new(
            claims,
            claims,
            capacities,
            capacities);
        ProductionInputDestinationClaimRuntime runtime = new(
            claims,
            capacities,
            claims,
            capacities,
            lifecycle);

        Require(
            lifecycle.TryReplaceOwnedAuthorities(
                ProductionInputDestinationClaimRuntime.OwnerDomain,
                new[] { claim },
                new[] { profile },
                out string publishFailure),
            "could not publish idempotent-revoke fixture: " + publishFailure);
        Require(
            runtime.TryRevokeIfPresent(record, out string firstFailure)
                && claims.CaptureAuthorityClaims().Count == 0
                && capacities.CaptureAuthorityProfiles().Count == 0,
            "first idempotent input-authority revoke failed: " + firstFailure);
        long claimRevision = claims.Revision;
        long capacityRevision = capacities.Revision;
        Require(
            runtime.TryRevokeIfPresent(record, out string replayFailure)
                && claims.Revision == claimRevision
                && capacities.Revision == capacityRevision,
            "replayed input-authority revoke was not a semantic no-op: "
                + replayFailure);

        Require(
            claims.TryReplaceOwnedClaims(
                ProductionInputDestinationClaimRuntime.OwnerDomain,
                new[] { claim },
                out _,
                out string partialPublishFailure),
            "could not publish partial input-authority fixture: "
                + partialPublishFailure);
        Require(
            !runtime.TryRevokeIfPresent(record, out string partialFailure)
                && partialFailure.StartsWith(
                    "production-input-authority-pair-invalid:",
                    StringComparison.Ordinal)
                && claims.CaptureAuthorityClaims().Count == 1,
            "partial input-authority pair was silently treated as revoked");
    }

    private static void ValidateZeroInputSourceBillInputAuthority()
    {
        ResourceEconomyContentCatalog authoredCatalog = LoadCatalog();
        Require(
            authoredCatalog.TryGetRecipe(
                "source:quarry",
                out ProductionRecipeSO authoredQuarry),
            "zero-input source fixture requires source:quarry");
        BuildingSO deepQuarry = LoadAll<BuildingSO>(
                "Assets/Resources/SO/Building/Modular")
            .Single(asset => asset != null
                && asset.GetAbility<BuildingFacilityPartAbility>()?.code == "P22");

        const string zeroInputRecipeId = "recipe:qa:zero-input-source";
        ProductionRecipeSO zeroInputRecipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        ProductionOutputDefinition[] zeroInputOutputs = authoredQuarry.Outputs
            .Select((output, index) => new ProductionOutputDefinition(
                ProductionOutputLineAuthoring.BuildStableId(
                    zeroInputRecipeId,
                    index,
                    output.ItemId,
                    output.Role),
                output.Role,
                output.ItemId,
                output.Amount,
                output.Probability))
            .ToArray();
        zeroInputRecipe.Configure(
            zeroInputRecipeId,
            "QA zero-input source",
            "Verifies that a source recipe owns no physical input destination.",
            authoredQuarry.FacilityTag,
            authoredQuarry.WorkTypeId.Value,
            string.Empty,
            authoredQuarry.RequiredWork,
            Array.Empty<ItemAmountDefinition>(),
            zeroInputOutputs);
        zeroInputRecipe.ConfigureWorkshop(
            authoredQuarry.WorkstationTag,
            authoredQuarry.RequiredSupportTags,
            authoredQuarry.ProcessKind);

        const string invalidInputRecipeId = "recipe:qa:invalid-input-mass";
        const string invalidInputItemId = "resource:cave-mushroom";
        ProductionRecipeSO invalidInputRecipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        ProductionOutputDefinition[] invalidInputOutputs = authoredQuarry.Outputs
            .Take(1)
            .Select((output, index) => new ProductionOutputDefinition(
                ProductionOutputLineAuthoring.BuildStableId(
                    invalidInputRecipeId,
                    index,
                    output.ItemId,
                    output.Role),
                output.Role,
                output.ItemId,
                output.Amount,
                output.Probability))
            .ToArray();
        invalidInputRecipe.Configure(
            invalidInputRecipeId,
            "QA invalid input mass",
            "Verifies that a positive input with zero mass stays fail-loud.",
            authoredQuarry.FacilityTag,
            authoredQuarry.WorkTypeId.Value,
            string.Empty,
            authoredQuarry.RequiredWork,
            new[] { new ItemAmountDefinition(invalidInputItemId, 1) },
            invalidInputOutputs);
        invalidInputRecipe.ConfigureWorkshop(
            authoredQuarry.WorkstationTag,
            authoredQuarry.RequiredSupportTags,
            authoredQuarry.ProcessKind);

        ResourceEconomyContentCatalog catalog = new(
            authoredCatalog.Items,
            authoredCatalog.Recipes.Concat(new[]
            {
                zeroInputRecipe,
                invalidInputRecipe
            }),
            authoredCatalog.Crops,
            authoredCatalog.Materials);
        GameObject facilityObject = new("Zero Input Source Bill Facility");
        try
        {
            BuildableObject facility =
                facilityObject.AddComponent<BuildableObject>();
            facility.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:zero-input-source");
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(deepQuarry, new Vector2Int(17, 6));
            FixedBuildingWorldQuery world = new(facility);
            MutablePreparedOutputRoutingAuthority routing = new();
            TerminalPreparedOutputExecutionPort preparedOutputs = new(routing);

            FakeProductionItemGateway items = new();
            ProductionRuntimeFixture runtime = CreateRuntime(
                catalog,
                items,
                seed: 91_701,
                buildingWorld: world,
                preparedOutputExecution: preparedOutputs,
                preparedOutputRouting: routing);
            ProductionBillCommandResult added = runtime.AddBill(
                facility,
                zeroInputRecipeId,
                ProductionOrderMode.RepeatForever,
                0);
            string zeroInputDestination = ProductionBillRuntime
                .DestinationPrefix + added.BillId.Value;
            Require(
                added.Succeeded
                && runtime.GetBills(facility).Count == 1
                && !runtime.DestinationClaims.TryGetClaim(
                    zeroInputDestination,
                    facility.centerPos,
                    out _)
                && !runtime.DestinationCapacities.TryGetCapacity(
                    zeroInputDestination,
                    facility.centerPos,
                    out _),
                "zero-input source bill created a physical input destination");

            DungeonProductionBillSaveData saved = runtime.Core.Capture();
            ProductionBillRestoreCandidate candidate = runtime.Core.BuildRestore(saved);
            ProductionRuntimeFixture restored = CreateRuntime(
                catalog,
                new FakeProductionItemGateway(),
                seed: 91_701,
                buildingWorld: world,
                preparedOutputExecution: preparedOutputs,
                preparedOutputRouting: routing);
            restored.Core.Restore(candidate);
            Require(
                restored.GetBills(facility).Count == 1
                && !restored.DestinationClaims.TryGetClaim(
                    zeroInputDestination,
                    facility.centerPos,
                    out _)
                && !restored.DestinationCapacities.TryGetCapacity(
                    zeroInputDestination,
                    facility.centerPos,
                    out _),
                "zero-input source restore recreated an input destination");
            ProductionBillCommandResult removed = restored.Core.RemoveBill(
                added.BillId,
                returnMaterials: true);
            bool retainedInputClaim = restored.DestinationClaims.TryGetClaim(
                zeroInputDestination,
                facility.centerPos,
                out _);
            bool retainedInputCapacity = restored.DestinationCapacities
                .TryGetCapacity(
                    zeroInputDestination,
                    facility.centerPos,
                    out _);
            int retainedBills = restored.GetBills(facility).Count;
            Require(
                removed.Succeeded
                && retainedBills == 0
                && !retainedInputClaim
                && !retainedInputCapacity,
                "zero-input source bill could not retire without a claim: "
                + $"succeeded={removed.Succeeded};failure={removed.Failure.Code};"
                + $"parameters={string.Join(",", removed.Failure.Parameters.ToArray())};"
                + $"bills={retainedBills};claim={retainedInputClaim};"
                + $"capacity={retainedInputCapacity}");

            FakeProductionItemGateway invalidItems = new();
            invalidItems.SetInvalidDefinitionMassForFailureTest(
                invalidInputItemId,
                0L);
            ProductionRuntimeFixture invalidRuntime = CreateRuntime(
                catalog,
                invalidItems,
                seed: 91_702,
                buildingWorld: world,
                preparedOutputExecution: preparedOutputs,
                preparedOutputRouting: routing);
            RequireThrows(
                () => invalidRuntime.AddBill(
                    facility,
                    invalidInputRecipeId,
                    ProductionOrderMode.RepeatForever,
                    0),
                "positive-input recipe with zero input mass did not fail loudly");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(zeroInputRecipe);
            UnityEngine.Object.DestroyImmediate(invalidInputRecipe);
        }
    }

    private static int CountOrdinalOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static void ValidateSubstanceSingleAuthority()
    {
        const string legacyRoot = "Assets/Resources/SO/Economy/Substances";
        string[] legacyAssets = AssetDatabase.IsValidFolder(legacyRoot)
            ? AssetDatabase.FindAssets(string.Empty, new[] { legacyRoot })
            : Array.Empty<string>();
        Require(
            legacyAssets.Length == 0,
            $"legacy substance SO assets remain: {legacyAssets.Length}");

        ResourceEconomyContentCatalog catalog = LoadCatalog();
        foreach (SubstanceDefinitionView projection in catalog.Substances)
        {
            Require(
                catalog.TryGetItem(projection.ItemId, out ResourceItemDefinitionSO item),
                $"substance projection item is missing: {projection.ItemId}");
            Require(
                item.TryGetFeature(out SubstanceItemFeature feature),
                $"substance projection has no item feature: {projection.ItemId}");
            Require(
                string.Equals(
                    feature.substanceId?.Trim(),
                    projection.SubstanceId,
                    StringComparison.Ordinal),
                $"substance projection ID drift: {projection.ItemId}");
            Require(
                catalog.TryGetSubstance(projection.SubstanceId, out SubstanceDefinitionView indexed)
                    && ReferenceEquals(indexed, projection),
                $"substance projection index drift: {projection.SubstanceId}");
        }

        ResourceItemDefinitionSO first =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        ResourceItemDefinitionSO second =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        ItemDefinitionCatalogSO root =
            ScriptableObject.CreateInstance<ItemDefinitionCatalogSO>();
        try
        {
            first.name = "DuplicateSubstanceA";
            first.Configure(
                "debug:substance-a", "A", string.Empty, StockCategory.Medicine,
                ResourceItemKind.Substance, ResourceIngredientTag.None,
                1, 1f, 1, string.Empty);
            first.ConfigureSubstance(
                "substance:duplicate", SubstanceUseClass.NonAddictive,
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 10f);
            second.name = "DuplicateSubstanceB";
            second.Configure(
                "debug:substance-b", "B", string.Empty, StockCategory.Medicine,
                ResourceItemKind.Substance, ResourceIngredientTag.None,
                1, 1f, 1, string.Empty);
            second.ConfigureSubstance(
                "substance:duplicate", SubstanceUseClass.NonAddictive,
                0f, 0f, 0f, 0f, 0f, 0f, 0f, 10f);
            root.SetDefinitions(new ItemDefinitionSO[] { first, second });
            Require(
                root.ValidateCatalog().Any(error =>
                    error.Contains("Duplicate substance ID", StringComparison.Ordinal)),
                "root item catalog accepted a duplicate substance ID");

            second.ClearSubstance();
            Require(
                root.ValidateCatalog().Any(error =>
                    error.Contains("has no substance feature", StringComparison.Ordinal)),
                "root item catalog accepted a substance item without a feature");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(first);
        }
    }

    private static void ValidatePhysicalStockSensorInstallation()
    {
        BuildingSO building = AssetDatabase
            .FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(candidate =>
                !string.IsNullOrWhiteSpace(candidate
                    ?.GetProductionWorkstationAbility()
                    ?.StockSensorInstallationItemId));
        Require(building != null, "stock sensor workstation definition is missing");

        GameObject gameObject = new("ProductionStockSensor_DebugFacility");
        try
        {
            BuildableObject facility = gameObject.AddComponent<BuildableObject>();
            const string facilityId = "building:debug-production-stock-sensor";
            facility.RestorePersistentIdentity(new BuildingInstanceId(facilityId));
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(building, Vector2Int.zero);

            string itemId = building
                .GetProductionWorkstationAbility()
                .StockSensorInstallationItemId;
            string destinationId = "production-sensor:" + facilityId;
            FakeProductionItemGateway items = new();
            IResourceEconomyContentCatalog catalog = LoadCatalog();
            NoOpWorkforceReplanService workforce =
                NoOpWorkforceReplanService.Instance;
            IProductionWorkshopRuntime workshops =
                EmptyProductionWorkshopRuntime.Instance;
            IProductionInputLogisticsService inputLogistics =
                new ProductionInputLogisticsService(
                    catalog,
                    items,
                    EmptyResearchRuntimeReferences.Instance,
                    workforce,
                    workshops);
            IProductionAssemblyBridge bridge = new ProductionAssemblyBridgeAdapter(
                items,
                items,
                items,
                inputLogistics,
                new TestProductionCycleUtilityService(
                    workshops,
                    new MutablePowerRuntime()),
                workshops,
                new FixedBuildingWorldQuery(facility),
                EmptyWarehouseWorldQuery.Instance,
                workforce,
                CreateOutputHandlerRegistry(catalog, items),
                narrativeQualification: null,
                performance: () =>
                    CharacterAiEditorTestDependencies.NeutralPerformance);
            ProductionFacilityHandle facilityHandle =
                bridge.CaptureFacility(facility);
            FacilityBufferDestinationClaimRegistry sensorClaims = new();
            EmptyFacilityBufferPhysicalOccupancyQuery sensorOccupancy = new();
            FacilityBufferMassAdmissionService sensorCapacities = new(
                sensorClaims,
                sensorOccupancy);
            FacilityBufferDestinationLifecycleService sensorLifecycle = new(
                sensorClaims,
                sensorClaims,
                sensorCapacities,
                sensorCapacities);
            ProductionStockSensorDestinationAuthorityRuntime sensorAuthority = new(
                items,
                sensorClaims,
                sensorClaims,
                sensorCapacities,
                sensorCapacities,
                sensorOccupancy,
                sensorLifecycle);
            DungeonRuntimeAggregateRootStore sensorRoots = new();
            ProductionStockSensorRuntime runtime = new(
                bridge,
                new ProductionAggregateStateStore(sensorRoots),
                items,
                sensorAuthority,
                new ProductionFacilityDestructiveDrainOpenOperationQuery(
                    sensorRoots));

            runtime.RequestInstallation(facilityHandle);
            Require(
                sensorAuthority.TryValidate(
                    facilityHandle,
                    out long sensorCapacityMassGrams,
                    out string sensorAuthorityFailure)
                && sensorCapacityMassGrams
                    == items.GetDefinitionQuantityMassGrams(itemId, 1),
                "stock sensor did not publish an exact one-panel gram authority: "
                    + sensorAuthorityFailure);
            Require(
                items.GetRequested(itemId) == 1,
                $"stock sensor requested wrong physical item: {itemId}");
            items.Deliver(itemId, 1, destinationId);
            items.FailStockSensorAcknowledgeOnce();
            runtime.FinalizeDeliveredSensors();
            Require(runtime.Has(facilityHandle), "delivered stock sensor was not installed");
            Require(
                items.GetDelivered(itemId) == 0,
                "installed stock sensor was not physically consumed");
            ProductionStockSensorPhysicalCommitSaveData pendingOwner =
                runtime.PendingInstallations.Single();
            DungeonProductionBillSaveData pendingPayload = new()
            {
                installedStockSensorFacilityIds = new List<string> { facilityId },
                installedStockSensors = runtime.InstalledSensors
                    .Select(value => value.Clone())
                    .ToList(),
                pendingStockSensorInstalls = new List<
                    ProductionStockSensorPhysicalCommitSaveData>
                {
                    pendingOwner.Clone()
                }
            };
            PhysicalItemRestoreCandidateDispositionSnapshot pendingReceipt = new(
                PhysicalItemDispositionKind.Sink,
                pendingOwner.operationId,
                pendingOwner.reasonCode,
                pendingOwner.requestFingerprint,
                pendingOwner.sourceStackIds,
                pendingOwner.inputQuantity,
                pendingOwner.inputMassGrams,
                pendingOwner.commitId);
            SinglePhysicalItemRestoreCandidateQuery pendingQuery =
                new(pendingReceipt);
            ProductionBillsSaveSection.ValidateStockSensorPhysicalRestoreCandidate(
                pendingPayload,
                pendingQuery);
            bool missingRejected = false;
            try
            {
                ProductionBillsSaveSection.ValidateStockSensorPhysicalRestoreCandidate(
                    pendingPayload,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance);
            }
            catch (InvalidOperationException)
            {
                missingRejected = true;
            }
            bool orphanRejected = false;
            try
            {
                ProductionBillsSaveSection.ValidateStockSensorPhysicalRestoreCandidate(
                    new DungeonProductionBillSaveData(),
                    pendingQuery);
            }
            catch (InvalidOperationException)
            {
                orphanRejected = true;
            }
            Require(
                missingRejected && orphanRejected,
                "stock-sensor physical restore accepted a missing or orphan Sink receipt");
            runtime.FinalizeDeliveredSensors();
            Require(
                runtime.PendingInstallations.Count == 0
                && items.GetDelivered(itemId) == 0,
                "stock-sensor acknowledgement retry repeated the physical debit");

            items.FailStockSensorRemovalOnce();
            ProductionBillCommandResult firstRemoval =
                runtime.Remove(facilityHandle);
            Require(
                !firstRemoval.Succeeded
                && runtime.Has(facilityHandle)
                && runtime.PendingRemovals.Count == 1
                && items.GetAvailable(itemId) == 0,
                "failed stock sensor output did not preserve installed ownership");
            ProductionStockSensorRemovalSaveData preparedRemoval =
                runtime.PendingRemovals.Single().Clone();
            DungeonProductionBillSaveData preparedRemovalPayload = new()
            {
                installedStockSensorFacilityIds =
                    new List<string> { facilityId },
                installedStockSensors = runtime.InstalledSensors
                    .Select(value => value.Clone())
                    .ToList(),
                pendingStockSensorRemovals = new List<
                    ProductionStockSensorRemovalSaveData>
                {
                    preparedRemoval.Clone()
                }
            };
            ProductionBillsSaveSection.ValidateStockSensorRemovalOutputCandidate(
                preparedRemovalPayload,
                EmptyPhysicalItemRestoreCandidateOutputQuery.Instance);
            ProductionStockSensorRemovalSaveData publishedRemoval =
                preparedRemoval.Clone();
            publishedRemoval.phase =
                ProductionStockSensorRemovalPhase.OutputPublished;
            publishedRemoval.outputQuantity = 1;
            publishedRemoval.outputMassGrams =
                publishedRemoval.expectedOutputMassGrams;
            string removalCommit =
                ProductionStockSensorRuntime.BuildRemovalOutputCommitId(
                    publishedRemoval);
            publishedRemoval.outputCommitIds = new List<string>
            {
                removalCommit
            };
            DungeonProductionBillSaveData publishedRemovalPayload = new()
            {
                installedStockSensorFacilityIds =
                    new List<string> { facilityId },
                installedStockSensors = runtime.InstalledSensors
                    .Select(value => value.Clone())
                    .ToList(),
                pendingStockSensorRemovals = new List<
                    ProductionStockSensorRemovalSaveData>
                {
                    publishedRemoval
                }
            };
            SinglePhysicalItemRestoreCandidateOutputQuery removalQuery = new(
                new PhysicalItemRestoreCandidateOutputSnapshot(
                    removalCommit,
                    "stack:stock-sensor-removal-output",
                    itemId,
                    1,
                    publishedRemoval.expectedOutputMassGrams,
                    WorldItemStackState.Loose,
                    facilityHandle.Position,
                    string.Empty));
            ProductionBillsSaveSection.ValidateStockSensorRemovalOutputCandidate(
                publishedRemovalPayload,
                removalQuery);
            bool missingRemovalOutputRejected = false;
            try
            {
                ProductionBillsSaveSection
                    .ValidateStockSensorRemovalOutputCandidate(
                        publishedRemovalPayload,
                        EmptyPhysicalItemRestoreCandidateOutputQuery.Instance);
            }
            catch (InvalidOperationException)
            {
                missingRemovalOutputRejected = true;
            }
            Require(
                missingRemovalOutputRejected,
                "published stock-sensor removal accepted a missing physical output");
            runtime.Remove(facilityHandle);
            Require(!runtime.Has(facilityHandle), "removed stock sensor remains installed");
            Require(
                items.GetAvailable(itemId) == 1,
                "removed stock sensor was not returned as a physical item");
            Require(
                runtime.PendingRemovals.Count == 0
                && items.StockSensorRemovalPublicationCount == 1,
                "stock sensor removal did not finish one exact output commit");
            runtime.Remove(facilityHandle);
            Require(
                items.GetAvailable(itemId) == 1
                && items.StockSensorRemovalPublicationCount == 1,
                "stock sensor removal retry minted a second physical output");

            runtime.RequestInstallation(facilityHandle);
            items.Deliver(itemId, 1, destinationId);
            runtime.FinalizeDeliveredSensors();
            Require(
                runtime.Has(facilityHandle),
                "stock sensor could not be reinstalled for destructive-drain QA");

            IProductionStockSensorDestructiveDrainPort destructive = runtime;
            Require(
                destructive.TryPrepareDurable(
                    facilityHandle.InstanceId,
                    out ProductionStockSensorRemovalSaveData durablePrepared,
                    out string durablePrepareFailure)
                && durablePrepared.phase ==
                    ProductionStockSensorRemovalPhase.Prepared
                && runtime.Has(facilityHandle)
                && items.StockSensorRemovalPublicationCount == 1,
                "destructive sensor prepare published or detached ownership: "
                + durablePrepareFailure);
            Require(
                destructive.TryPrepareDurable(
                    facilityHandle.InstanceId,
                    out ProductionStockSensorRemovalSaveData durablePrepareReplay,
                    out string durablePrepareReplayFailure)
                && durablePrepareReplay.phase == durablePrepared.phase
                && string.Equals(
                    durablePrepareReplay.operationId,
                    durablePrepared.operationId,
                    StringComparison.Ordinal)
                && durablePrepareReplay.expectedOutputMassGrams
                    == durablePrepared.expectedOutputMassGrams
                && items.StockSensorRemovalPublicationCount == 1,
                "destructive sensor prepare replay drifted: "
                + durablePrepareReplayFailure);
            Require(
                destructive.TryPublish(
                    facilityHandle.InstanceId,
                    out ProductionStockSensorRemovalSaveData directPublished,
                    out string directPublishFailure)
                && directPublished.phase ==
                    ProductionStockSensorRemovalPhase.OutputPublished
                && directPublished.outputCommitIds.Count == 1
                && !runtime.Has(facilityHandle)
                && runtime.InstalledSensors.Count == 1
                && items.StockSensorRemovalPublicationCount == 2,
                "destructive sensor direct publish failed or duplicated output: "
                + directPublishFailure);
            string directCommitId = directPublished.outputCommitIds.Single();
            Require(
                !destructive.TryAcknowledge(
                    facilityHandle.InstanceId,
                    directCommitId + ":wrong",
                    out _,
                    out _)
                && runtime.InstalledSensors.Count == 1,
                "destructive sensor accepted a mismatched upper receipt");
            Require(
                destructive.TryAcknowledge(
                    facilityHandle.InstanceId,
                    directCommitId,
                    out ProductionStockSensorRemovalSaveData directAcknowledged,
                    out string directAckFailure)
                && directAcknowledged.phase ==
                    ProductionStockSensorRemovalPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc
                && runtime.InstalledSensors.Count == 0
                && runtime.PendingRemovals.Count == 1
                && items.StockSensorRemovalPublicationCount == 2,
                "destructive sensor direct acknowledgement failed: "
                + directAckFailure);
            Require(
                destructive.TryAcknowledge(
                    facilityHandle.InstanceId,
                    directCommitId,
                    out ProductionStockSensorRemovalSaveData directReplay,
                    out string directReplayFailure)
                && directReplay.phase ==
                    ProductionStockSensorRemovalPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc
                && items.StockSensorRemovalPublicationCount == 2,
                "destructive sensor acknowledgement replay duplicated output: "
                + directReplayFailure);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    private static void ValidateAuthoredContent()
    {
        BuildingSO[] buildings = AssetDatabase
            .FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
        BuildingSO[] stations = buildings
            .Where(building =>
                (building.GetAbility<BuildingFacilityPartAbility>()?.code
                    ?? string.Empty)
                .StartsWith("P", StringComparison.Ordinal))
            .ToArray();
        int expectedStationCount = ModularFacilityAssetBuilder
            .GetCatalogCodes()
            .Count(code => code.StartsWith("P", StringComparison.Ordinal));
        Require(
            stations.Length == expectedStationCount,
            $"production stations={stations.Length}/{expectedStationCount}");
        Require(stations.All(station => !station.unlocked),
            "research production stations must start locked");
        Require(stations.All(station =>
                station.Facility != null
                && (station.Facility.SupportsWork(BuiltInWorkTypeIds.Craft)
                    || station.Facility.SupportsWork(BuiltInWorkTypeIds.Cook)
                    || station.Facility.SupportsWork(BuiltInWorkTypeIds.Quarry)
                    || station.Facility.SupportsWork(BuiltInWorkTypeIds.Sow)
                    || station.Facility.SupportsWork(BuiltInWorkTypeIds.Harvest))),
            "a production station has no production work type");
        Require(stations.All(station =>
                !string.IsNullOrWhiteSpace(station.GetPrimarySemanticTag())),
            "a production station has no recipe facility tag");

        ResearchProjectSO[] projects = AssetDatabase
            .FindAssets(
                "t:ResearchProjectSO",
                new[] { "Assets/Resources/SO/Research/Projects" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null)
            .ToArray();
        Require(projects.Length >= 78, $"research projects={projects.Length}");

        HashSet<int> stationIds = stations.Select(station => station.id).ToHashSet();
        int unlockedStationCount = projects
            .SelectMany(project => project.Unlocks)
            .OfType<BlueprintBuildingUnlock>()
            .Select(unlock => unlock.buildingId)
            .Where(stationIds.Contains)
            .Distinct()
            .Count();
        Require(unlockedStationCount == stations.Length,
            $"research station unlocks={unlockedStationCount}/{stations.Length}");

        ResourceEconomyContentCatalog catalog = LoadCatalog();
        Require(
            catalog.Items.Count
                >= ResourceEconomyAssetBuilder.ExpectedItemCount
                    + ProductionWorkshopContentAssetBuilder
                        .ExpectedWorkshopItemCount,
            $"resource items={catalog.Items.Count}");
        Require(
            catalog.Recipes.Count
                >= ResourceEconomyAssetBuilder.ExpectedRecipeCount
                    + ProductionWorkshopContentAssetBuilder
                        .ExpectedWorkshopRecipeCount,
            $"production recipes={catalog.Recipes.Count}");
        Require(
            catalog.Crops.Count == ResourceEconomyAssetBuilder.ExpectedCropCount + 4,
            $"crops={catalog.Crops.Count}");
        Require(
            catalog.Materials.Count == ResourceEconomyAssetBuilder.ExpectedMaterialCount,
            $"materials={catalog.Materials.Count}");
        Require(
            catalog.Substances.Count == ResourceEconomyAssetBuilder.ExpectedSubstanceCount,
            $"substances={catalog.Substances.Count}");

        HashSet<string> stationTags = stations
            .SelectMany(station => station.GetSemanticTags())
            .Concat(buildings.SelectMany(building => building.GetSemanticTags()))
            .ToHashSet(StringComparer.Ordinal);
        string[] missingTags = catalog.Recipes
            .Where(recipe => recipe.RecipeId.StartsWith(
                "recipe:",
                StringComparison.Ordinal))
            .Select(recipe => recipe.FacilityTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag)
                && !stationTags.Contains(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        Require(missingTags.Length == 0,
            $"recipes without facilities: {string.Join(", ", missingTags)}");

        string[] wasteRecipeIds =
        {
            "recipe:compost-plant",
            "recipe:compost-animal",
            "recipe:compost-mixed",
            "recipe:low-fuel-plant",
            "recipe:low-fuel-animal",
            "recipe:low-fuel-rot",
            "recipe:rot-toxin",
            "recipe:incinerate-plant",
            "recipe:incinerate-animal",
            "recipe:incinerate-mixed",
            "recipe:incinerate-forbidden"
        };
        string[] missingWasteRecipes = wasteRecipeIds
            .Where(recipeId => !catalog.TryGetRecipe(recipeId, out _))
            .ToArray();
        Require(
            missingWasteRecipes.Length == 0,
            $"missing waste recipes: {string.Join(", ", missingWasteRecipes)}");
    }

    private static void ValidateProductionFacilityEnumerationBoundary()
    {
        ResourceEconomyContentCatalog catalog = LoadCatalog();
        BuildingSO nonProductionDefinition =
            ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO invalidWorkstationDefinition =
            ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO invalidTagDefinition =
            ScriptableObject.CreateInstance<BuildingSO>();
        GameObject nonProductionObject = new(
            "Production Boundary Non-Production Fixture");
        GameObject invalidWorkstationObject = new(
            "Production Boundary Invalid Workstation Fixture");
        GameObject invalidTagObject = new(
            "Production Boundary Invalid Workstation Tag Fixture");
        try
        {
            nonProductionDefinition.id = -1_950_010_000;
            nonProductionDefinition.objectName =
                "QA non-production mixed-world entry";
            nonProductionDefinition.category = BuildingCategory.Production;
            nonProductionDefinition.ConfigureAuthoredContentIdentity(
                string.Empty,
                1,
                "QA mixed-world production boundary fixture.");
            BuildingAbilityCollection nonProductionAbilities = new();
            nonProductionAbilities.Add(new BuildingFacilityPartAbility
            {
                code = "P25"
            });
            nonProductionAbilities.Add(new BuildingSemanticTagsAbility
            {
                tags = new[] { "incinerator" }
            });
            nonProductionDefinition.ReplaceAbilities(nonProductionAbilities);

            BuildingAbilityCollection workstationAbilities = new();
             workstationAbilities.Add(new BuildingProductionWorkstationAbility
             {
                 workstationTag = "workstation:qa:invalid-authority",
                 lanePolicy = ProductionWorkstationLanePolicy
                     .ManualWithDetachedBatchProcessors,
                 manualWorkLaneCount = 1,
                 automaticWorkLaneCount = 0
            });
            workstationAbilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 2,
                physicalOutputBufferCycleCapacity = 2,
                allowOverflowDump = false
            });
            invalidWorkstationDefinition.id = -1_950_010_001;
            invalidWorkstationDefinition.objectName =
                "QA production workstation missing definition authority";
            invalidWorkstationDefinition.ConfigureAuthoredContentIdentity(
                string.Empty,
                1,
                "QA fail-loud production identity fixture.");
            invalidWorkstationDefinition.ReplaceAbilities(workstationAbilities);

            BuildingAbilityCollection invalidTagAbilities = new();
             invalidTagAbilities.Add(new BuildingProductionWorkstationAbility
             {
                 workstationTag = " workstation:qa:noncanonical ",
                 lanePolicy = ProductionWorkstationLanePolicy
                     .ManualWithDetachedBatchProcessors,
                 manualWorkLaneCount = 1,
                 automaticWorkLaneCount = 0
            });
            invalidTagAbilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 2,
                physicalOutputBufferCycleCapacity = 2,
                allowOverflowDump = false
            });
            invalidTagDefinition.id = 1_950_010_002;
            invalidTagDefinition.objectName =
                "QA production workstation with noncanonical tag";
            invalidTagDefinition.ConfigureAuthoredContentIdentity(
                "building:qa:invalid-workstation-tag",
                1,
                "QA fail-loud workstation tag fixture.");
            invalidTagDefinition.ReplaceAbilities(invalidTagAbilities);

            BuildableObject nonProduction =
                nonProductionObject.AddComponent<BuildableObject>();
            nonProduction.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:non-production-boundary");
            CharacterAiEditorTestDependencies.Inject(nonProduction);
            nonProduction.Initialization(
                nonProductionDefinition,
                new Vector2Int(3, 5));

            BuildableObject invalidWorkstation =
                invalidWorkstationObject.AddComponent<BuildableObject>();
            invalidWorkstation.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:invalid-production-authority");
            CharacterAiEditorTestDependencies.Inject(invalidWorkstation);
            invalidWorkstation.Initialization(
                invalidWorkstationDefinition,
                new Vector2Int(5, 5));

            BuildableObject invalidTag =
                invalidTagObject.AddComponent<BuildableObject>();
            invalidTag.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:invalid-workstation-tag-instance");
            CharacterAiEditorTestDependencies.Inject(invalidTag);
            invalidTag.Initialization(
                invalidTagDefinition,
                new Vector2Int(7, 5));

            ProductionRuntimeFixture nonProductionRuntime = CreateRuntime(
                catalog,
                new FakeProductionItemGateway(),
                52_711,
                buildingWorld: new FixedBuildingWorldQuery(nonProduction));
            nonProductionRuntime.Tick();
            Require(
                nonProductionRuntime.Facilities.Count == 0,
                "mixed building world exposed a non-production entry as a production facility");
            Require(
                nonProductionRuntime.GetBills(nonProduction).Count == 0
                && !nonProductionRuntime.HasStockSensor(nonProduction),
                "read-only production queries did not safely ignore a non-production entry");

            ProductionRuntimeFixture invalidRuntime = CreateRuntime(
                catalog,
                new FakeProductionItemGateway(),
                52_712,
                buildingWorld: new FixedBuildingWorldQuery(invalidWorkstation));
            bool enumerationRejected = false;
            try
            {
                _ = invalidRuntime.Facilities;
            }
            catch (InvalidOperationException exception)
            {
                enumerationRejected = exception.Message.Contains(
                    "neither a definition ID nor numeric authority",
                    StringComparison.Ordinal);
            }
            Require(
                enumerationRejected,
                "a real production workstation with missing definition authority was silently filtered");

            bool queryRejected = false;
            try
            {
                _ = invalidRuntime.GetBills(invalidWorkstation);
            }
            catch (InvalidOperationException exception)
            {
                queryRejected = exception.Message.Contains(
                    "neither a definition ID nor numeric authority",
                    StringComparison.Ordinal);
            }
            Require(
                queryRejected,
                "production query facade hid invalid authority on a real workstation");

            bool invalidTagRejected = false;
            try
            {
                _ = ProductionFacilityDefinitionIdentity
                    .IsProductionWorkstation(invalidTag);
            }
            catch (InvalidOperationException exception)
            {
                invalidTagRejected = exception.Message.Contains(
                    "noncanonical workstation tag",
                    StringComparison.Ordinal);
            }
            Require(
                invalidTagRejected,
                "a raw production workstation ability with a noncanonical tag was silently filtered");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(nonProductionObject);
            UnityEngine.Object.DestroyImmediate(invalidWorkstationObject);
            UnityEngine.Object.DestroyImmediate(invalidTagObject);
            UnityEngine.Object.DestroyImmediate(nonProductionDefinition);
            UnityEngine.Object.DestroyImmediate(invalidWorkstationDefinition);
            UnityEngine.Object.DestroyImmediate(invalidTagDefinition);
        }
    }

    private static void ValidatePhysicalProductionBill()
    {
        ProductionRecipeSO recipe = ScriptableObject.CreateInstance<ProductionRecipeSO>();
        ResourceItemDefinitionSO grain =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        ResourceItemDefinitionSO flour =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject facilityObject = new GameObject("Production Bill Contract Facility");
        GameObject workerObject = new GameObject("Production Bill Contract Worker");
        try
        {
            CharacterActor worker = workerObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            worker.EnsureRuntimeState();
            worker.Identity.SetPersistentId("character:test-production-worker");
            recipe.Configure(
                "test:recipe:flour",
                "시험 제분",
                "정확한 재료와 누적 작업량을 검증한다.",
                "mill",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                10f,
                new[] { new ItemAmountDefinition("resource:test-grain", 3) },
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:main",
                        ProductionOutputRole.Main,
                        "material:test-flour",
                        2)
                });
            recipe.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.Crafting);
            recipe.ConfigureWorkshop(
                "workstation:mill",
                Array.Empty<string>(),
                ProductionProcessKind.WorkOnly,
                cleanWater: 0.2f,
                wastewater: 0.1f,
                wastewaterKind:
                    ProcessWastewaterComposition.FoodProcessWashwater);
            recipe.ConfigureProcessClass(
                ProductionProcessClass.CuttingGrindingWashing);
            grain.Configure(
                "resource:test-grain",
                "시험 곡물",
                "생산 주문 계약 검증용 원료",
                StockCategory.General,
                ResourceItemKind.Raw,
                ResourceIngredientTag.Plant,
                1,
                1f,
                100,
                string.Empty);
            flour.Configure(
                "material:test-flour",
                "시험 밀가루",
                "생산 주문 계약 검증용 중간재",
                StockCategory.General,
                ResourceItemKind.Intermediate,
                ResourceIngredientTag.Plant,
                1,
                1f,
                100,
                string.Empty);
            ResourceEconomyContentCatalog catalog =
                new ResourceEconomyContentCatalog(
                    new[] { grain, flour },
                    new[] { recipe },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>());

            BuildingAbilityCollection abilities = new BuildingAbilityCollection();
            abilities.Add(new BuildingFacilityAbility
            {
                settings = CreateCraftFacilityData()
            });
            abilities.Add(new BuildingSemanticTagsAbility
            {
                tags = new[] { "mill" }
            });
             abilities.Add(new BuildingProductionWorkstationAbility
             {
                 workstationTag = "workstation:mill",
                 lanePolicy = ProductionWorkstationLanePolicy
                     .ManualWithDetachedBatchProcessors,
                 manualWorkLaneCount = 1,
                 automaticWorkLaneCount = 0
            });
            abilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 4,
                physicalOutputBufferCycleCapacity = 4
            });
            building.id = 99101;
            building.objectName = "시험 제분소";
            building.ReplaceAbilities(abilities);

            BuildableObject facility =
                facilityObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(facility);
            Require(
                facility.PersistentInstanceId.IsValid,
                "production fixture building identity was not assigned");
            facility.Initialization(building, new Vector2Int(7, 3));

            FakeProductionItemGateway items = new FakeProductionItemGateway();
            BufferedExactOutputTestHandler testOutputHandler = new(
                items,
                "material:test-flour");
            ProductionRuntimeFixture runtime = CreateRuntime(
                catalog,
                items,
                seed: 771,
                buildingWorld: new FixedBuildingWorldQuery(facility),
                outputHandlers: new[] { testOutputHandler });
            ProductionBillCommandResult added = runtime.AddBill(
                facility,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                2);
            Require(added.Succeeded, added.Failure.Code.ToString());
            string inputDestination = ProductionBillRuntime.DestinationPrefix
                + added.BillId.Value;
            Require(
                runtime.DestinationClaims.TryGetClaim(
                    inputDestination,
                    facility.centerPos,
                    out FacilityBufferDestinationClaim addedClaim)
                && string.Equals(
                    addedClaim.OwnerDomain,
                    ProductionInputDestinationClaimRuntime.OwnerDomain,
                    StringComparison.Ordinal)
                && string.Equals(
                    addedClaim.OwnerOperationId,
                    added.BillId.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    addedClaim.OwnerFacilityId,
                    facility.PersistentInstanceId.Value,
                    StringComparison.Ordinal),
                "production bill did not publish its exact input destination claim");
            Require(
                runtime.DestinationCapacities.TryGetCapacity(
                    inputDestination,
                    facility.centerPos,
                    out FacilityBufferMassCapacitySnapshot inputCapacity)
                && inputCapacity.Profile.MaxMassGrams > 0L
                && string.Equals(
                    inputCapacity.Profile.OwnerDomain,
                    ProductionInputDestinationClaimRuntime.OwnerDomain,
                    StringComparison.Ordinal)
                && string.Equals(
                    inputCapacity.Profile.OwnerOperationId,
                    added.BillId.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    inputCapacity.Profile.OwnerFacilityId,
                    facility.PersistentInstanceId.Value,
                    StringComparison.Ordinal),
                "production bill did not publish its paired input-buffer mass profile");
            Require(items.GetRequested("resource:test-grain") == 3,
                "exact input delivery was not requested");
            Require(!runtime.CheckWorkAvailability(
                    facility,
                    BuiltInWorkTypeIds.Craft).Available,
                "production became runnable before delivery");

            items.Deliver(
                "resource:wrong-grain",
                3,
                ProductionBillRuntime.DestinationPrefix + added.BillId);
            Require(!runtime.CheckWorkAvailability(
                    facility,
                    BuiltInWorkTypeIds.Craft).Available,
                "a different item satisfied an exact recipe input");

            items.Deliver(
                "resource:test-grain",
                3,
                ProductionBillRuntime.DestinationPrefix + added.BillId);
            ProductionWorkAvailabilityResult availability =
                runtime.CheckWorkAvailability(
                    facility,
                    BuiltInWorkTypeIds.Craft);
            Require(availability.Available,
                $"delivered production did not become runnable: {availability.Failure.Code}");
            ProductionWorkBeginResult begin = runtime.BeginWork(
                worker,
                facility,
                BuiltInWorkTypeIds.Craft);
            Require(begin.Succeeded,
                $"could not begin production: {begin.Failure.Code}");
            ProductionBillSnapshot started = begin.Bill;
            Require(items.GetDelivered("resource:test-grain") == 0,
                "delivered materials were not consumed at work start");

            ProductionWorkExecutionResult partialWork = runtime.ExecuteWork(
                worker,
                facility,
                started.BillId,
                4f);
            Require(partialWork.Succeeded && !partialWork.CycleCompleted,
                "partial work incorrectly completed a cycle");
            ProductionBillsSaveSection saveSection =
                new ProductionBillsSaveSection(
                    runtime.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly,
                    new FixedRestoreWorldCandidateQuery(facility),
                    new ProductionFacilityHandleQueryAdapter());
            string partialSave = saveSection.Capture();
            DungeonProductionBillSaveData partialPayload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(partialSave);
            Require(partialPayload.version == DungeonProductionBillSaveData.CurrentVersion
                    && partialPayload.bills.Count == 1
                    && partialPayload.bills[0].materialsConsumed
                    && partialPayload.bills[0].wipInputCommitId
                        == "test-wip:production-wip:" + added.BillId.Value
                            + ":00000001"
                    && partialPayload.bills[0].wipInputQuantity == 3
                    && partialPayload.bills[0].wipInputMassGrams == 3000L
                    && partialPayload.bills[0].processFluidConsumed
                    && partialPayload.bills[0].processCleanWaterMassGrams == 100L
                    && partialPayload.bills[0].processWastewaterMassGrams == 50L
                    && partialPayload.bills[0].processWastewaterComponents.Count == 1
                    && partialPayload.bills[0].processWastewaterComponents[0]
                        .composition
                        == ProcessWastewaterComposition.FoodProcessWashwater
                    && partialPayload.bills[0].processWastewaterComponents[0]
                        .massGrams == 50L
                    && partialPayload.bills[0].processManualWaterTransfers.Count == 1
                    && partialPayload.bills[0].processManualWaterTransfers[0]
                        .transferredWaterUnits == 1
                    && partialPayload.bills[0].processManualWaterTransfers[0]
                        .inputMassGrams == 500L,
                "production WIP input receipt was not captured exactly");
            ProductionBillCommandResult activeCancel = runtime.RemoveBill(
                started.BillId,
                returnMaterials: true);
            DungeonProductionBillSaveData cancelledPayload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    saveSection.Capture());
            Require(activeCancel.Succeeded
                    && runtime.GetBills(facility).Count == 0
                    && !runtime.DestinationClaims.CaptureClaims().Any(claim =>
                        string.Equals(
                            claim.DestinationId,
                            inputDestination,
                            StringComparison.Ordinal))
                    && cancelledPayload.bills.Count == 0
                    && cancelledPayload.wipTerminalReceipts.Count == 1
                    && cancelledPayload.wipTerminalReceipts[0].reason
                        == ProductionWipTerminalReason.Cancelled
                    && cancelledPayload.wipTerminalReceipts[0].lossKind
                        == ProductionWipTerminalLossKind
                            .ExplicitIrrecoverableProcessLoss
                    && cancelledPayload.wipTerminalReceipts[0].inputCommitId
                        == partialPayload.bills[0].wipInputCommitId
                    && cancelledPayload.wipTerminalReceipts[0].inputQuantity == 3
                    && cancelledPayload.wipTerminalReceipts[0].inputMassGrams == 3000L
                    && cancelledPayload.wipTerminalReceipts[0]
                        .processCleanWaterMassGrams == 100L
                    && cancelledPayload.wipTerminalReceipts[0]
                        .processWastewaterMassGrams == 50L
                    && cancelledPayload.wipTerminalReceipts[0]
                        .wastewaterComponents.Count == 1
                    && cancelledPayload.wipTerminalReceipts[0]
                        .wastewaterComponents[0].massGrams == 50L
                    && cancelledPayload.wipTerminalReceipts[0]
                        .committedOutputMassGrams == 0L
                    && cancelledPayload.wipTerminalReceipts[0].declaredLossMassGrams
                        == 3050L,
                "active production WIP cancellation did not publish one exact terminal loss receipt");

            string cancelledSave = saveSection.Capture();
            ProductionRuntimeFixture terminalRestored = CreateRuntime(
                catalog,
                items,
                seed: 772,
                outputHandlers: new[] { testOutputHandler });
            ProductionBillsSaveSection terminalRestoredSection =
                new(
                    terminalRestored.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly,
                    new FixedRestoreWorldCandidateQuery(facility),
                    new ProductionFacilityHandleQueryAdapter());
            DungeonGameRestoreReport terminalRestoreReport = new();
            terminalRestoredSection.Restore(
                cancelledSave,
                terminalRestoredSection.SectionVersion,
                terminalRestoreReport);
            Require(terminalRestoreReport.Success
                    && string.Equals(
                        terminalRestoredSection.Capture(),
                        cancelledSave,
                        StringComparison.Ordinal),
                "production WIP terminal receipt did not round-trip exactly");
            DungeonProductionBillSaveData tamperedTerminal =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    cancelledSave);
            tamperedTerminal.wipTerminalReceipts[0].declaredLossMassGrams--;
            bool tamperedTerminalRejected = false;
            try
            {
                terminalRestoredSection.StageRestore(
                    JsonUtility.ToJson(tamperedTerminal),
                    terminalRestoredSection.SectionVersion,
                    new DungeonGameRestoreReport());
            }
            catch (InvalidOperationException)
            {
                tamperedTerminalRejected = true;
            }
            Require(tamperedTerminalRejected
                    && string.Equals(
                        terminalRestoredSection.Capture(),
                        cancelledSave,
                        StringComparison.Ordinal),
                "tampered production WIP terminal mass mutated live state");

            DungeonProductionBillSaveData tamperedWastewaterComposition =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    cancelledSave);
            tamperedWastewaterComposition.wipTerminalReceipts[0]
                .wastewaterComponents[0].massGrams++;
            bool tamperedWastewaterCompositionRejected = false;
            try
            {
                terminalRestoredSection.StageRestore(
                    JsonUtility.ToJson(tamperedWastewaterComposition),
                    terminalRestoredSection.SectionVersion,
                    new DungeonGameRestoreReport());
            }
            catch (InvalidOperationException)
            {
                tamperedWastewaterCompositionRejected = true;
            }
            Require(tamperedWastewaterCompositionRejected
                    && string.Equals(
                        terminalRestoredSection.Capture(),
                        cancelledSave,
                        StringComparison.Ordinal),
                "tampered wastewater composition mutated live production state");

            FixedBuildingWorldQuery emptyLiveRestoreWorld = new();
            ProductionRuntimeFixture restored = CreateRuntime(
                catalog,
                items,
                seed: 771,
                buildingWorld: emptyLiveRestoreWorld,
                outputHandlers: new[] { testOutputHandler });
            ProductionBillsSaveSection restoredSection =
                new ProductionBillsSaveSection(
                    restored.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly,
                    new FixedRestoreWorldCandidateQuery(facility),
                    new ProductionFacilityHandleQueryAdapter());
            DungeonGameRestoreReport restoreReport =
                new DungeonGameRestoreReport();
            restoredSection.Restore(
                partialSave,
                saveSection.SectionVersion,
                restoreReport);
            Require(
                restoreReport.Success
                    && emptyLiveRestoreWorld.Buildings.Count == 0,
                $"production save section restore failed: "
                + string.Join(" / ", restoreReport.Errors));
            Require(
                restored.DestinationClaims.TryGetClaim(
                    inputDestination,
                    facility.centerPos,
                    out FacilityBufferDestinationClaim restoredClaim)
                && string.Equals(
                    restoredClaim.OwnerOperationId,
                    added.BillId.Value,
                    StringComparison.Ordinal),
                "production restore did not republish the exact input destination claim");
            ProductionBillSnapshot restoredBill =
                restored.GetBills(facility).Single();
            Require(Mathf.Approximately(restoredBill.CompletedWork, 4f),
                $"partial progress was not restored: {restoredBill.CompletedWork}");
            DungeonProductionBillSaveData restoredPayload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    restoredSection.Capture());
            Require(restoredPayload.bills[0].wipInputCommitId
                        == partialPayload.bills[0].wipInputCommitId
                    && restoredPayload.bills[0].wipInputQuantity == 3
                    && restoredPayload.bills[0].wipInputMassGrams == 3000L,
                "production WIP input authority drifted across restore");

            string beforeRejectedRestore = restoredSection.Capture();
            int versionBeforeRejectedRestore = restored.Version;
            DungeonProductionBillSaveData invalidRestore =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    beforeRejectedRestore);
            invalidRestore.bills[0].buildingInstanceId = string.Empty;
            bool invalidRestoreRejected = false;
            try
            {
                restoredSection.StageRestore(
                    JsonUtility.ToJson(invalidRestore),
                    restoredSection.SectionVersion,
                    new DungeonGameRestoreReport());
            }
            catch (InvalidOperationException)
            {
                invalidRestoreRejected = true;
            }
            Require(
                invalidRestoreRejected
                && restored.Version == versionBeforeRejectedRestore
                && string.Equals(
                    restoredSection.Capture(),
                    beforeRejectedRestore,
                    StringComparison.Ordinal),
                "invalid production restore mutated live aggregate state");

            DungeonProductionBillSaveData invalidWip =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    beforeRejectedRestore);
            invalidWip.bills[0].wipInputMassGrams = 0L;
            bool invalidWipRejected = false;
            try
            {
                restoredSection.StageRestore(
                    JsonUtility.ToJson(invalidWip),
                    restoredSection.SectionVersion,
                    new DungeonGameRestoreReport());
            }
            catch (InvalidOperationException)
            {
                invalidWipRejected = true;
            }
            Require(invalidWipRejected
                    && string.Equals(
                        restoredSection.Capture(),
                        beforeRejectedRestore,
                        StringComparison.Ordinal),
                "invalid production WIP receipt was not rejected atomically");

            items.FailBufferedOutputAfterSuccesses(1);
            ProductionWorkExecutionResult blockedOutput = restored.ExecuteWork(
                worker,
                facility,
                restoredBill.BillId,
                6f);
            Require(!blockedOutput.Succeeded && !blockedOutput.CycleCompleted,
                "blocked production output unexpectedly completed");
            Require(items.GetAvailable("material:test-flour") == 1,
                "partial output fixture did not publish exactly one unit");
            string resolvedOutputSave = restoredSection.Capture();
            DungeonProductionBillSaveData resolvedOutputPayload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    resolvedOutputSave);
            Require(resolvedOutputPayload.bills[0].outputOutcomeResolved
                    && resolvedOutputPayload.bills[0].resolvedOutputs.Count == 1
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0].itemId
                        == "material:test-flour"
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .outputLineId == "output:main"
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .outputCapabilityId ==
                        BufferedExactOutputTestHandler.Capability
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .outputCapabilityVersion ==
                        BufferedExactOutputTestHandler.CapabilityVersion
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .outputComponentCodecId ==
                        BufferedExactOutputTestHandler.Codec
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .outputComponentCodecVersion ==
                        BufferedExactOutputTestHandler.CodecVersion
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .outputCapabilityFingerprint ==
                        ProductionOutputCapabilityDescriptorFingerprint.Capture(
                            "output:main",
                            "material:test-flour",
                            BufferedExactOutputTestHandler.Capability,
                            BufferedExactOutputTestHandler.CapabilityVersion,
                            BufferedExactOutputTestHandler.Codec,
                            BufferedExactOutputTestHandler.CodecVersion)
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0].amount == 2
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .committedAmount == 1
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .committedMassGrams == 1000L,
                "blocked production output did not persist its exact resolved outcome");

            DungeonProductionBillSaveData driftedCapability =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    resolvedOutputSave);
            driftedCapability.bills[0].resolvedOutputs[0]
                .outputCapabilityVersion++;
            bool driftedCapabilityRejected = false;
            try
            {
                restoredSection.StageRestore(
                    JsonUtility.ToJson(driftedCapability),
                    restoredSection.SectionVersion,
                    new DungeonGameRestoreReport());
            }
            catch (InvalidOperationException)
            {
                driftedCapabilityRejected = true;
            }
            Require(
                driftedCapabilityRejected
                && string.Equals(
                    restoredSection.Capture(),
                    resolvedOutputSave,
                    StringComparison.Ordinal),
                "drifted frozen output capability mutated the live production aggregate");

            ProductionRuntimeFixture partialTerminalRuntime = CreateRuntime(
                catalog,
                items,
                seed: 999990,
                buildingWorld: new FixedBuildingWorldQuery(facility),
                outputHandlers: new[] { testOutputHandler });
            ProductionBillsSaveSection partialTerminalSection =
                new(
                    partialTerminalRuntime.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly,
                    new FixedRestoreWorldCandidateQuery(facility),
                    new ProductionFacilityHandleQueryAdapter());
            DungeonGameRestoreReport partialTerminalRestore = new();
            partialTerminalSection.Restore(
                resolvedOutputSave,
                partialTerminalSection.SectionVersion,
                partialTerminalRestore);
            ProductionBillCommandResult partialTerminalCancel =
                partialTerminalRuntime.RemoveBill(
                    restoredBill.BillId,
                    returnMaterials: true);
            DungeonProductionBillSaveData partialTerminalPayload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    partialTerminalSection.Capture());
            ProductionWipTerminalReceiptSaveData partialTerminalReceipt =
                partialTerminalPayload.wipTerminalReceipts.Single();
            Require(partialTerminalRestore.Success
                    && partialTerminalCancel.Succeeded
                    && partialTerminalPayload.bills.Count == 0
                    && partialTerminalReceipt.inputMassGrams == 3000L
                    && partialTerminalReceipt.processCleanWaterMassGrams == 100L
                    && partialTerminalReceipt.processWastewaterMassGrams == 50L
                    && partialTerminalReceipt.committedOutputMassGrams == 1000L
                    && partialTerminalReceipt.declaredLossMassGrams == 2050L,
                "partial-output WIP cancellation did not close its exact mass equation");
            string partialTerminalSave = partialTerminalSection.Capture();
            ProductionRuntimeFixture partialTerminalRoundTrip = CreateRuntime(
                catalog,
                items,
                seed: 999989,
                outputHandlers: new[] { testOutputHandler });
            ProductionBillsSaveSection partialTerminalRoundTripSection =
                new(
                    partialTerminalRoundTrip.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly,
                    new FixedRestoreWorldCandidateQuery(facility),
                    new ProductionFacilityHandleQueryAdapter());
            DungeonGameRestoreReport partialTerminalRoundTripReport = new();
            partialTerminalRoundTripSection.Restore(
                partialTerminalSave,
                partialTerminalRoundTripSection.SectionVersion,
                partialTerminalRoundTripReport);
            Require(partialTerminalRoundTripReport.Success
                    && string.Equals(
                        partialTerminalRoundTripSection.Capture(),
                        partialTerminalSave,
                        StringComparison.Ordinal),
                "partial-output WIP terminal receipt did not round-trip exactly");
            DungeonProductionBillSaveData tamperedPartialTerminal =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    partialTerminalSave);
            tamperedPartialTerminal.wipTerminalReceipts[0]
                .committedOutputMassGrams++;
            bool tamperedPartialTerminalRejected = false;
            try
            {
                partialTerminalRoundTripSection.StageRestore(
                    JsonUtility.ToJson(tamperedPartialTerminal),
                    partialTerminalRoundTripSection.SectionVersion,
                    new DungeonGameRestoreReport());
            }
            catch (InvalidOperationException)
            {
                tamperedPartialTerminalRejected = true;
            }
            Require(tamperedPartialTerminalRejected
                    && string.Equals(
                        partialTerminalRoundTripSection.Capture(),
                        partialTerminalSave,
                        StringComparison.Ordinal),
                "tampered partial-output WIP mass mutated live state");

            MutableGameClock partialDestroyedClock = new()
            {
                DeltaTimeValue = 0.02f
            };
            FixedBuildingWorldQuery partialDestroyedWorld =
                new(facility);
            ProductionRuntimeFixture partialDestroyedRuntime = CreateRuntime(
                catalog,
                items,
                seed: 999988,
                buildingWorld: partialDestroyedWorld,
                clock: partialDestroyedClock,
                outputHandlers: new[] { testOutputHandler });
            ProductionBillsSaveSection partialDestroyedSection =
                new(
                    partialDestroyedRuntime.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly,
                    new FixedRestoreWorldCandidateQuery(facility),
                    new ProductionFacilityHandleQueryAdapter());
            DungeonGameRestoreReport partialDestroyedRestore = new();
            partialDestroyedSection.Restore(
                resolvedOutputSave,
                partialDestroyedSection.SectionVersion,
                partialDestroyedRestore);
            partialDestroyedWorld.Replace();
            partialDestroyedRuntime.Core.Tick();
            DungeonProductionBillSaveData partialDestroyedPayload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    partialDestroyedSection.Capture());
            ProductionWipTerminalReceiptSaveData partialDestroyedReceipt =
                partialDestroyedPayload.wipTerminalReceipts.Single();
            Require(partialDestroyedRestore.Success
                    && partialDestroyedPayload.bills.Count == 0
                    && partialDestroyedReceipt.reason
                        == ProductionWipTerminalReason.FacilityDestroyed
                    && partialDestroyedReceipt.inputMassGrams == 3000L
                    && partialDestroyedReceipt.processCleanWaterMassGrams == 100L
                    && partialDestroyedReceipt.processWastewaterMassGrams == 50L
                    && partialDestroyedReceipt.committedOutputMassGrams == 1000L
                    && partialDestroyedReceipt.declaredLossMassGrams == 2050L,
                "missing facility did not close partial-output WIP with the exact mass equation");

            ProductionRuntimeFixture outputRestored = CreateRuntime(
                catalog,
                items,
                seed: 999991,
                buildingWorld: new FixedBuildingWorldQuery(facility),
                outputHandlers: new[] { testOutputHandler });
            ProductionBillsSaveSection outputRestoredSection =
                new ProductionBillsSaveSection(
                    outputRestored.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly,
                    new FixedRestoreWorldCandidateQuery(facility),
                    new ProductionFacilityHandleQueryAdapter());
            DungeonGameRestoreReport outputRestoreReport =
                new DungeonGameRestoreReport();
            outputRestoredSection.Restore(
                resolvedOutputSave,
                outputRestoredSection.SectionVersion,
                outputRestoreReport);
            Require(outputRestoreReport.Success,
                "resolved production output could not be restored");
            ProductionWorkExecutionResult restoredWork = outputRestored.ExecuteWork(
                worker,
                facility,
                restoredBill.BillId,
                1f);
            Require(restoredWork.Succeeded && restoredWork.CycleCompleted,
                "restored resolved production output did not complete");
            Require(items.GetAvailable("material:test-flour") == 2,
                "production output was not spawned as a physical stack");
            ProductionBillSnapshot repeated = outputRestored
                .GetBills(facility).Single();
            Require(repeated.RemainingCycles == 1
                    && Mathf.Approximately(repeated.CompletedWork, 0f)
                    && !repeated.MaterialsConsumed,
                "repeat bill did not reset for its next cycle");
            Require(items.GetRequested("resource:test-grain") == 6,
                "repeat bill did not request the next exact material batch");

            string materialDestination = ProductionBillRuntime.DestinationPrefix
                + repeated.BillId.Value;
            items.Deliver("resource:test-grain", 3, materialDestination);
            ProductionWorkBeginResult secondBegin = outputRestored.BeginWork(
                worker,
                facility,
                BuiltInWorkTypeIds.Craft);
            Require(secondBegin.Succeeded,
                "second production cycle could not begin for crash recovery");
            items.ThrowAfterBufferedCommitOnce();
            bool injectedOutputCrash = false;
            try
            {
                outputRestored.ExecuteWork(
                    worker,
                    facility,
                    repeated.BillId,
                    10f);
            }
            catch (InvalidOperationException exception)
            {
                injectedOutputCrash = exception.Message.Contains(
                    "injected-output-post-commit",
                    StringComparison.Ordinal);
            }
            Require(injectedOutputCrash
                    && items.GetAvailable("material:test-flour") == 3,
                "post-commit output crash did not preserve exactly one physical unit");
            string outputCrashSave = outputRestoredSection.Capture();
            DungeonProductionBillSaveData outputCrashPayload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    outputCrashSave);
            ProductionResolvedOutputSaveData pendingOutput = outputCrashPayload
                .bills.Single().resolvedOutputs.Single();
            Require(pendingOutput.committedAmount == 0
                    && pendingOutput.committedMassGrams == 0L
                    && !pendingOutput.pendingCommitApplied
                    && !string.IsNullOrWhiteSpace(pendingOutput.pendingCommitId)
                    && pendingOutput.pendingCommitId.Contains(
                        ":output:main:material:test-flour:",
                        StringComparison.Ordinal)
                    && pendingOutput.outputCapabilityId ==
                        BufferedExactOutputTestHandler.Capability
                    && pendingOutput.outputCapabilityVersion ==
                        BufferedExactOutputTestHandler.CapabilityVersion
                    && items.HasBufferedCommit(pendingOutput.pendingCommitId),
                "post-commit output crash did not persist its pending commit identity");

            ProductionRuntimeFixture crashRestored = CreateRuntime(
                catalog,
                items,
                seed: 1234567,
                buildingWorld: new FixedBuildingWorldQuery(facility),
                outputHandlers: new[] { testOutputHandler });
            ProductionBillsSaveSection crashRestoredSection =
                new(
                    crashRestored.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly,
                    new BufferedExactOutputRestoreJoinForFixture(items),
                    new FixedRestoreWorldCandidateQuery(facility),
                    new ProductionFacilityHandleQueryAdapter());
            DungeonGameRestoreReport crashRestoreReport = new();
            crashRestoredSection.Restore(
                outputCrashSave,
                crashRestoredSection.SectionVersion,
                crashRestoreReport);
            Require(crashRestoreReport.Success,
                "post-commit output crash save could not be restored");
            ProductionWorkExecutionResult recoveredOutput =
                crashRestored.ExecuteWork(
                    worker,
                    facility,
                    repeated.BillId,
                    1f);
            Require(recoveredOutput.Succeeded
                    && recoveredOutput.CycleCompleted
                    && items.GetAvailable("material:test-flour") == 4
                    && !crashRestored.DestinationClaims.CaptureClaims().Any(
                        claim => string.Equals(
                            claim.DestinationId,
                            inputDestination,
                            StringComparison.Ordinal)),
                "restored output commit duplicated or lost a physical unit: "
                + $"succeeded={recoveredOutput.Succeeded}; "
                + $"cycle={recoveredOutput.CycleCompleted}; "
                + $"failure={recoveredOutput.Failure.Code}; "
                + $"available={items.GetAvailable("material:test-flour")}");

            GameObject destroyedFacilityObject =
                new("Production WIP Destroyed Facility Contract");
            BuildableObject destroyedFacility =
                destroyedFacilityObject.AddComponent<BuildableObject>();
            destroyedFacility.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(destroyedFacility);
            destroyedFacility.Initialization(building, new Vector2Int(9, 3));
            MutableGameClock destroyedClock = new()
            {
                DeltaTimeValue = 0.02f
            };
            ProductionRuntimeFixture destroyedRuntime = CreateRuntime(
                catalog,
                items,
                seed: 8881,
                buildingWorld: new FixedBuildingWorldQuery(destroyedFacility),
                clock: destroyedClock,
                outputHandlers: new[] { testOutputHandler });
            ProductionBillsSaveSection destroyedSection =
                new(
                    destroyedRuntime.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly,
                    new FixedRestoreWorldCandidateQuery(destroyedFacility),
                    new ProductionFacilityHandleQueryAdapter());
            ProductionBillCommandResult destroyedBill = destroyedRuntime.AddBill(
                destroyedFacility,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            Require(destroyedBill.Succeeded,
                "destroyed-facility WIP fixture bill was not created");
            string destroyedDestination = ProductionBillRuntime.DestinationPrefix
                + destroyedBill.BillId.Value;
            items.Deliver("resource:test-grain", 3, destroyedDestination);
            ProductionWorkBeginResult destroyedBegin = destroyedRuntime.BeginWork(
                worker,
                destroyedFacility,
                BuiltInWorkTypeIds.Craft);
            Require(destroyedBegin.Succeeded,
                "destroyed-facility WIP fixture did not consume its input");
            UnityEngine.Object.DestroyImmediate(destroyedFacilityObject);
            destroyedRuntime.Core.Tick();
            DungeonProductionBillSaveData destroyedPayload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    destroyedSection.Capture());
            ProductionWipTerminalReceiptSaveData destroyedReceipt =
                destroyedPayload.wipTerminalReceipts.Single(receipt =>
                    receipt.billId == destroyedBill.BillId.Value);
            Require(destroyedPayload.bills.All(saved =>
                        saved.billId != destroyedBill.BillId.Value)
                    && !destroyedRuntime.DestinationClaims.CaptureClaims().Any(
                        claim => string.Equals(
                            claim.DestinationId,
                            destroyedDestination,
                            StringComparison.Ordinal))
                    && destroyedReceipt.reason
                        == ProductionWipTerminalReason.FacilityDestroyed
                    && destroyedReceipt.inputQuantity == 3
                    && destroyedReceipt.inputMassGrams == 3000L
                    && destroyedReceipt.processCleanWaterMassGrams == 100L
                    && destroyedReceipt.processWastewaterMassGrams == 50L
                    && destroyedReceipt.committedOutputMassGrams == 0L
                    && destroyedReceipt.declaredLossMassGrams == 3050L,
                "facility destruction did not close active WIP with one exact terminal receipt");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(building);
            UnityEngine.Object.DestroyImmediate(grain);
            UnityEngine.Object.DestroyImmediate(flour);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void ValidateProcessFluidReceiptAggregation()
    {
        ProductionRecipeSO recipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject facilityObject =
            new("Production Fluid Receipt Contract Facility");
        try
        {
            recipe.Configure(
                "test:recipe:fluid-receipt",
                "시험 유체 영수증",
                "시설·레시피 유체 질량 합산을 검증한다.",
                "fluid-receipt",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                1f,
                Array.Empty<ItemAmountDefinition>(),
                Array.Empty<ProductionOutputDefinition>());
            recipe.ConfigureWorkshop(
                "workstation:fluid-receipt",
                Array.Empty<string>(),
                ProductionProcessKind.WorkOnly,
                cleanWater: 0.2f,
                wastewater: 0.1f,
                wastewaterKind:
                    ProcessWastewaterComposition.FoodProcessWashwater);

            BuildingAbilityCollection abilities = new();
            abilities.Add(new BuildingProcessFluidAbility
            {
                workTypeIds = new[] { BuiltInWorkTypeIds.Craft.Value },
                cleanWaterPerCycle = 0.3f,
                wastewaterPerCycle = 0.2f,
                wastewaterComposition =
                    ProcessWastewaterComposition.IndustrialEffluent,
                allowsManualWaterFallback = false
            });
            building.id = 99102;
            building.objectName = "시험 유체 영수증 시설";
            building.ReplaceAbilities(abilities);

            BuildableObject facility =
                facilityObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(building, new Vector2Int(8, 3));

            RecordingProcessFluidUseRuntime processFluids = new();
            ProductionCycleUtilityService utilities = new(
                processFluids,
                EmptyProductionWorkshopRuntime.Instance,
                new MutablePowerRuntime(),
                NoOpFluidInfrastructureTransaction.Instance,
                NoOpWastewaterTransaction.Instance,
                NoEnvironmentalFieldQuery.Instance);
            ProductionBillRecord fluidBill = ProductionBillRecord.Create(
                (ProductionBillId)"production-bill:99102",
                recipe.RecipeId,
                facility.RequirePersistentInstanceId(),
                ProductionOrderMode.RepeatCount,
                1,
                0,
                ProductionBatchStage.None,
                "production:production-bill:99102");
            bool consumed = utilities.TryConsumeCycleUtilities(
                fluidBill,
                recipe,
                facility,
                out ProductionProcessFluidReceipt receipt,
                out string failureReason);
            Require(consumed
                    && string.IsNullOrEmpty(failureReason)
                    && processFluids.BatchCallCount == 1
                    && processFluids.ExplicitCallCount == 1
                    && Mathf.Approximately(processFluids.LastCleanWater, 0.5f)
                    && Mathf.Approximately(processFluids.LastWastewater, 0.3f)
                    && receipt.CleanWaterMassGrams == 250L
                    && receipt.WastewaterMassGrams == 150L
                    && receipt.WastewaterComponents.Count == 2
                    && receipt.WastewaterComponents.Any(value =>
                        value.Composition
                            == ProcessWastewaterComposition.FoodProcessWashwater
                        && value.SourceKind == ProcessWastewaterSourceKind.Recipe
                        && value.MassGrams == 50L)
                    && receipt.WastewaterComponents.Any(value =>
                        value.Composition
                            == ProcessWastewaterComposition.IndustrialEffluent
                        && value.SourceKind == ProcessWastewaterSourceKind.Facility
                        && value.MassGrams == 100L),
                "production process-fluid receipt did not aggregate facility and recipe mass exactly");

            recipe.ConfigureWorkshop(
                "workstation:fluid-receipt",
                new[] { "support:missing-before-fluid" },
                ProductionProcessKind.WorkOnly,
                cleanWater: 0.2f,
                wastewater: 0.1f,
                wastewaterKind:
                    ProcessWastewaterComposition.FoodProcessWashwater);
            bool missingSupportRejected = utilities.TryConsumeCycleUtilities(
                fluidBill,
                recipe,
                facility,
                out ProductionProcessFluidReceipt rejectedReceipt,
                out string missingSupportFailure);
            Require(!missingSupportRejected
                    && rejectedReceipt.CleanWaterMassGrams == 0L
                    && rejectedReceipt.WastewaterMassGrams == 0L
                    && processFluids.ExplicitCallCount == 1
                    && processFluids.BatchCallCount == 1
                    && missingSupportFailure.Contains(
                        "linked-support-missing",
                        StringComparison.Ordinal),
                "missing linked support consumed facility fluid before rejection");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(building);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void ValidatePassiveBatchProduction()
    {
        ProductionRecipeSO recipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        BuildingSO workstationData =
            ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO supportData =
            ScriptableObject.CreateInstance<BuildingSO>();
        ResourceItemDefinitionSO[] fixtureItems =
        {
            CreateFixtureItem(
                "test:wort",
                ResourceItemKind.Intermediate,
                StockCategory.General),
            CreateFixtureItem(
                "test:beer",
                ResourceItemKind.FinishedGood,
                StockCategory.Food),
            CreateFixtureItem(
                "test:fuel",
                ResourceItemKind.Raw,
                StockCategory.Fuel),
            CreateFixtureItem(
                "test:rot",
                ResourceItemKind.Waste,
                StockCategory.General)
        };
        GameObject workstationObject = new GameObject(
            "Passive Batch Contract Workstation");
        GameObject supportObject = new GameObject(
            "Passive Batch Contract Support");
        GameObject workerObject = new GameObject(
            "Passive Batch Contract Worker");
        try
        {
            CharacterActor worker = workerObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            worker.EnsureRuntimeState();
            worker.Identity.SetPersistentId("character:test-passive-production-worker");
            recipe.Configure(
                "test:recipe:fermentation",
                "시험 발효",
                "시간 공정과 저장 복원을 검증한다.",
                "brewery",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                2f,
                new[] { new ItemAmountDefinition("test:wort", 2) },
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:main",
                        ProductionOutputRole.Main,
                        "test:beer",
                        2)
                });
            recipe.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.FoodProduction);
            recipe.ConfigureWorkshop(
                "workstation:test-brewery",
                new[] { "support:test-fermenter" },
                ProductionProcessKind.PassiveBatch,
                "support:test-fermenter",
                prepareWork: 2f,
                finishWork: 1f,
                processGameHours: 12f,
                failedBatchItemId: "test:rot");
            recipe.ConfigureProcessClass(
                ProductionProcessClass.CookingSimpleMixing);
            ResourceEconomyContentCatalog catalog =
                new ResourceEconomyContentCatalog(
                    fixtureItems,
                    new[] { recipe },
                    Array.Empty<CropDefinitionSO>(),
                    Array.Empty<CraftMaterialDefinitionSO>());

            BuildingAbilityCollection workstationAbilities =
                new BuildingAbilityCollection();
            workstationAbilities.Add(new BuildingFacilityAbility
            {
                settings = CreateCraftFacilityData()
            });
            workstationAbilities.Add(
                 new BuildingProductionWorkstationAbility
                 {
                     workstationTag = "workstation:test-brewery",
                     lanePolicy = ProductionWorkstationLanePolicy
                         .ManualWithDetachedBatchProcessors,
                     manualWorkLaneCount = 1,
                     automaticWorkLaneCount = 0
                });
            workstationAbilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 4,
                physicalOutputBufferCycleCapacity = 4
            });
            workstationData.id = 99201;
            workstationData.objectName = "시험 양조장";
            workstationData.ReplaceAbilities(workstationAbilities);

            BuildingAbilityCollection supportAbilities =
                new BuildingAbilityCollection();
            supportAbilities.Add(new BuildingProductionSupportAbility
            {
                supportId = "support:test-fermenter-instance",
                featureTags = new[] { "support:test-fermenter" },
                compatibleWorkstationTags =
                    new[] { "workstation:test-brewery" },
                 kind = ProductionSupportKind.BatchProcessor,
                 batchCapacity = 1,
                 maximumLinkedInstancesPerWorkstation = 1,
                requiresPower = true,
                requiresFuel = true,
                fuelItemId = "test:fuel",
                fuelPerCycle = 1
            });
            supportData.id = 99202;
            supportData.objectName = "시험 발효조";
            supportData.ReplaceAbilities(supportAbilities);

            BuildableObject workstation =
                workstationObject.AddComponent<BuildableObject>();
            workstation.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(workstation);
            workstation.Initialization(
                workstationData,
                new Vector2Int(4, 4));
            BuildableObject support =
                supportObject.AddComponent<BuildableObject>();
            support.ConstructPersistentIdentity(
                new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(support);
            support.Initialization(
                supportData,
                new Vector2Int(5, 4));

            FakeProductionItemGateway items =
                new FakeProductionItemGateway();
            BufferedExactOutputTestHandler passiveOutputHandler = new(
                items,
                "test:beer");
            MutableGameClock clock = new MutableGameClock();
            MutablePowerRuntime power = new MutablePowerRuntime();
            FakeProductionWorkshop workshop =
                new FakeProductionWorkshop(workstation, support);
            ProductionRuntimeFixture runtime = CreateRuntime(
                catalog,
                items,
                seed: 772,
                workshops: workshop,
                buildingWorld: new FixedBuildingWorldQuery(
                    workstation,
                    support),
                power: power,
                clock: clock,
                outputHandlers: new[] { passiveOutputHandler });
            ProductionBillCommandResult added = runtime.AddBill(
                workstation,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            Require(added.Succeeded, added.Failure.Code.ToString());
            string destination =
                ProductionBillRuntime.DestinationPrefix + added.BillId;
            items.Deliver("test:wort", 2, destination);
            items.Deliver("test:fuel", 1, destination);
            ProductionWorkBeginResult passiveBegin = runtime.BeginWork(
                worker,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(passiveBegin.Succeeded,
                $"passive batch did not begin: {passiveBegin.Failure.Code}");
            ProductionBillSnapshot prepared = passiveBegin.Bill;
            ProductionWorkExecutionResult preparation = runtime.ExecuteWork(
                worker,
                workstation,
                prepared.BillId,
                2f);
            Require(preparation.Succeeded && !preparation.CycleCompleted,
                "preparation incorrectly emitted the final product");
            ProductionBillSnapshot processing =
                runtime.GetBills(workstation).Single();
            Require(
                processing.BatchStage == ProductionBatchStage.Processing
                && Mathf.Approximately(
                    processing.RemainingProcessingHours,
                    12f)
                && items.GetAvailable("test:beer") == 0,
                "batch did not occupy its passive processing stage");

            clock.DeltaTimeValue = 45f;
            runtime.Tick();
            string saveJson = new ProductionBillsSaveSection(
                runtime.Core,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                EmptyProductionPreparedOutputRestoreJoin.Instance,
                ProductionOutputLifecycleRestoreCandidatePublisher
                    .IsolatedSectionFixtureOnly,
                new FixedRestoreWorldCandidateQuery(workstation, support),
                new ProductionFacilityHandleQueryAdapter()).Capture();
            ProductionRuntimeFixture restored = CreateRuntime(
                catalog,
                items,
                seed: 772,
                workshops: workshop,
                buildingWorld: new FixedBuildingWorldQuery(
                    workstation,
                    support),
                power: power,
                clock: clock,
                outputHandlers: new[] { passiveOutputHandler });
            DungeonGameRestoreReport report =
                new DungeonGameRestoreReport();
            new ProductionBillsSaveSection(
                restored.Core,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                EmptyProductionPreparedOutputRestoreJoin.Instance,
                ProductionOutputLifecycleRestoreCandidatePublisher
                    .IsolatedSectionFixtureOnly,
                new FixedRestoreWorldCandidateQuery(workstation, support),
                new ProductionFacilityHandleQueryAdapter()).Restore(
                saveJson,
                DungeonProductionBillSaveData.CurrentVersion,
                report);
            ProductionBillSnapshot halfProcessed =
                restored.GetBills(workstation).Single();
            Require(
                report.Success
                && Mathf.Approximately(
                    halfProcessed.RemainingProcessingHours,
                    6f),
                "passive processing time did not save and restore");

            power.Powered = false;
            restored.Tick();
            ProductionBillSnapshot gracePaused =
                restored.GetBills(workstation).Single();
            Require(
                Mathf.Approximately(
                    gracePaused.RemainingProcessingHours,
                    6f)
                && Mathf.Approximately(gracePaused.BatchIntegrity, 100f)
                && Mathf.Approximately(
                    gracePaused.UtilityOutageHours,
                    6f),
                "six-hour utility grace did not preserve progress and integrity");
            clock.DeltaTimeValue = 0.75f;
            restored.Tick();
            ProductionBillSnapshot decaying =
                restored.GetBills(workstation).Single();
            Require(
                decaying.BatchIntegrity < 100f
                && decaying.BatchIntegrity > 99f,
                "integrity did not start decaying after the utility grace");

            power.Powered = true;
            clock.DeltaTimeValue = 45f;
            restored.Tick();
            ProductionBillSnapshot finishing =
                restored.GetBills(workstation).Single();
            Require(
                finishing.BatchStage == ProductionBatchStage.Finishing
                && items.GetAvailable("test:beer") == 0,
                "processing completion did not wait for finishing work");
            ProductionWorkBeginResult finishBegin = restored.BeginWork(
                worker,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(finishBegin.Succeeded,
                $"finishing work did not become runnable: {finishBegin.Failure.Code}");
            ProductionBillSnapshot finishingWork = finishBegin.Bill;
            ProductionWorkExecutionResult finishWork = restored.ExecuteWork(
                worker,
                workstation,
                finishingWork.BillId,
                1f);
            Require(finishWork.Succeeded
                && finishWork.CycleCompleted
                && items.GetAvailable("test:beer") == 2,
                "finishing work did not emit a physical final product");
            Require(
                items.GetRequested("test:wort") == 2,
                "passive production generated an automatic downstream order");

            ProductionBillCommandResult degraded = restored.AddBill(
                workstation,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            Require(degraded.Succeeded, degraded.Failure.Code.ToString());
            string degradedDestination =
                ProductionBillRuntime.DestinationPrefix + degraded.BillId;
            items.Deliver("test:wort", 2, degradedDestination);
            items.Deliver("test:fuel", 1, degradedDestination);
            ProductionWorkBeginResult degradedBegin = restored.BeginWork(
                worker,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(degradedBegin.Succeeded,
                $"degraded batch did not begin: {degradedBegin.Failure.Code}");
            ProductionBillSnapshot degradedPreparation = degradedBegin.Bill;
            Require(restored.ExecuteWork(
                    worker,
                    workstation,
                    degradedPreparation.BillId,
                    2f).Succeeded,
                "degraded batch preparation failed");
            power.Powered = false;
            clock.DeltaTimeValue = 7.5f * 16.2f;
            restored.Tick();
            ProductionBillSnapshot degradedProcessing = restored
                .GetBills(workstation)
                .Single(bill => bill.BillId == degraded.BillId);
            Require(
                degradedProcessing.BatchIntegrity < 50f
                && degradedProcessing.BatchIntegrity > 48f,
                "utility outage did not reach the half-yield integrity band");
            power.Powered = true;
            clock.DeltaTimeValue = 7.5f * 12f;
            restored.Tick();
            ProductionWorkBeginResult degradedFinish = restored.BeginWork(
                worker,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(degradedFinish.Succeeded,
                $"degraded finishing did not begin: {degradedFinish.Failure.Code}");
            ProductionBillSnapshot degradedFinishing = degradedFinish.Bill;
            ProductionWorkExecutionResult degradedFinishWork =
                restored.ExecuteWork(
                    worker,
                    workstation,
                    degradedFinishing.BillId,
                    1f);
            Require(degradedFinishWork.Succeeded
                && degradedFinishWork.CycleCompleted
                && items.GetAvailable("test:beer") == 3,
                "integrity below 50 did not halve the physical output");

            ProductionBillCommandResult ruined = restored.AddBill(
                workstation,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            Require(ruined.Succeeded, ruined.Failure.Code.ToString());
            string ruinedDestination =
                ProductionBillRuntime.DestinationPrefix + ruined.BillId;
            items.Deliver("test:wort", 2, ruinedDestination);
            items.Deliver("test:fuel", 1, ruinedDestination);
            ProductionWorkBeginResult ruinedBegin = restored.BeginWork(
                worker,
                workstation,
                BuiltInWorkTypeIds.Craft);
            Require(ruinedBegin.Succeeded,
                $"ruined batch did not begin: {ruinedBegin.Failure.Code}");
            ProductionBillSnapshot ruinedPreparation = ruinedBegin.Bill;
            Require(restored.ExecuteWork(
                    worker,
                    workstation,
                    ruinedPreparation.BillId,
                    2f).Succeeded,
                "ruined batch preparation failed");
            power.Powered = false;
            clock.DeltaTimeValue = 7.5f * 26f;
            restored.Tick();
            ProductionBillSnapshot unsupportedRuin = restored
                .GetBills(workstation)
                .Single(bill => bill.BillId == ruined.BillId);
            Require(
                unsupportedRuin.MaterialsConsumed
                && unsupportedRuin.BlockedFailure.Code ==
                    FailureCode.ProductionOutputUnavailable
                && unsupportedRuin.BlockedFailure.Parameters.ToArray().Contains(
                    "ruined-output-capability-unsupported")
                && items.GetAvailable("test:rot") == 0
                && items.GetAvailable("test:beer") == 3
                && items.GetRequested("test:fuel") == 3,
                "special passive output without a ruined-batch capability did not preserve WIP and fail loudly");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(workstationObject);
            UnityEngine.Object.DestroyImmediate(supportObject);
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(workstationData);
            UnityEngine.Object.DestroyImmediate(supportData);
            foreach (ResourceItemDefinitionSO fixtureItem in fixtureItems)
            {
                UnityEngine.Object.DestroyImmediate(fixtureItem);
            }
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void ValidateEconomyPlanning()
    {
        ResourceStockPolicyData normalized = new ResourceStockPolicyData
        {
            itemId = " material:iron-ingot ",
            enabled = true,
            minimumStock = 30,
            targetStock = 10,
            maximumStock = 5,
            surplusDisposition = StockSurplusDisposition.Process
        };
        normalized.Normalize();
        Require(
            normalized.itemId == "material:iron-ingot"
            && normalized.targetStock == 30
            && normalized.maximumStock == 30,
            "stock policy thresholds were not normalized");
        ValidateStockPolicySaveBoundary();

        int rawSmall = RegionalSupplyContractSizing.ResolveAmount(
            ResourceItemKind.Raw,
            population: 3,
            completedResearchCount: 12,
            offerIndex: 0);
        int rawLarge = RegionalSupplyContractSizing.ResolveAmount(
            ResourceItemKind.Raw,
            population: 30,
            completedResearchCount: 72,
            offerIndex: 2);
        int finished = RegionalSupplyContractSizing.ResolveAmount(
            ResourceItemKind.FinishedGood,
            population: 30,
            completedResearchCount: 72,
            offerIndex: 2);
        Require(
            rawSmall is >= 20 and <= 80
            && rawLarge is >= 20 and <= 80
            && finished is >= 2 and <= 12,
            "regional contract sizing escaped its content bounds");
        ValidateRegionalSupplyContractSaveBoundary();

        FakeProductionItemGateway items = new FakeProductionItemGateway();
        GrandProjectApplicationAdapter grandProjectAdapter =
            new GrandProjectApplicationAdapter(
                items,
                UnavailableGrandProjectPhysicalSink.Instance,
                new EmptyBuildingWorldQuery(),
                new FixedDropZoneQuery(new Vector2Int(4, 1)),
                EmptyResearchRuntimeReferences.Instance,
                workforce: null,
                facilityCandidates: null);
        EconomyProjectInputOwnerFixtureAuthority inputOwners =
            CreateEconomyProjectInputOwnerFixture();
        GrandProjectRuntime runtime = new GrandProjectRuntime(
            grandProjectAdapter,
            grandProjectAdapter,
            new FixedGameClock(),
            aggregateRootStore: new DungeonRuntimeAggregateRootStore(),
            inputOwners: inputOwners.Runtime);
        DungeonGrandProjectSaveData completedProjects =
            new DungeonGrandProjectSaveData
        {
            state = new GrandProjectRuntimeState
            {
                completedProjectIds = new List<string>
                {
                    GrandProjectRuntime.DeepMiningNetworkId,
                    GrandProjectRuntime.DefenseDistrictId,
                    GrandProjectRuntime.ExpeditionSupplyBaseId,
                    GrandProjectRuntime.RegionalTradePostId
                }
            }
        };
        runtime.PublishRestoreCandidate(
            runtime.BuildRestore(completedProjects));
        Require(
            Mathf.Approximately(
                runtime.GetProductionOutputMultiplier("quarry"),
                1.25f)
            && Mathf.Approximately(runtime.ContractRewardMultiplier, 1.25f)
            && Mathf.Approximately(
                runtime.DefensePreparationMultiplier,
                1.2f)
            && runtime.ExpeditionSupplyCapacityBonus == 12,
            "completed grand-project benefits were not restored");

        GrandProjectSaveSection section = new GrandProjectSaveSection(
            runtime,
            EmptyPhysicalItemRestoreCandidateQuery.Instance,
            inputOwners.RestoreRuntime,
            new FixedRestoreWorldCandidateQuery());
        string json = section.Capture();
        GrandProjectApplicationAdapter restoredGrandProjectAdapter =
            new GrandProjectApplicationAdapter(
                items,
                UnavailableGrandProjectPhysicalSink.Instance,
                new EmptyBuildingWorldQuery(),
                new FixedDropZoneQuery(new Vector2Int(4, 1)),
                EmptyResearchRuntimeReferences.Instance,
                workforce: null,
                facilityCandidates: null);
        EconomyProjectInputOwnerFixtureAuthority restoredInputOwners =
            CreateEconomyProjectInputOwnerFixture();
        GrandProjectRuntime restored = new GrandProjectRuntime(
            restoredGrandProjectAdapter,
            restoredGrandProjectAdapter,
            new FixedGameClock(),
            aggregateRootStore: new DungeonRuntimeAggregateRootStore(),
            inputOwners: restoredInputOwners.Runtime);
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        GrandProjectSaveSection restoredSection =
            new GrandProjectSaveSection(
                restored,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                restoredInputOwners.RestoreRuntime,
                new FixedRestoreWorldCandidateQuery());
        restoredSection.Restore(
            json,
            section.SectionVersion,
            report);
        Require(
            report.Success
            && restored.IsCompleted(
                GrandProjectRuntime.DeepMiningNetworkId)
            && restored.IsCompleted(
                GrandProjectRuntime.RegionalTradePostId),
            "grand-project save section did not round-trip");
        object grandProjectSectionContract = section;
        Require(
            grandProjectSectionContract is IDungeonSaveSectionPreflight
            && grandProjectSectionContract is IDungeonRollbackFreeSaveSection
            && grandProjectSectionContract is not IOptionalDungeonSaveSection
            && grandProjectSectionContract is not IDungeonStagedOptionalSaveSection,
            "grand-project save section is not strict and rollback-free");

        DungeonGrandProjectSaveData invalid =
            JsonUtility.FromJson<DungeonGrandProjectSaveData>(json);
        invalid.state.completedProjectIds.Add(
            GrandProjectRuntime.DeepMiningNetworkId);
        invalid.state.completedWork = 1f;
        string beforeInvalid = restoredSection.Capture();
        RequireStrictRejectsWithoutMutation(
            restoredSection,
            JsonUtility.ToJson(invalid),
            beforeInvalid,
            "invalid grand-project payload mutated live state");
        RequireStrictRejectsWithoutMutation(
            restoredSection,
            json,
            restoredSection.SectionVersion - 1,
            beforeInvalid,
            "legacy grand-project section version was accepted");
        RequireStrictRejectsWithoutMutation(
            restoredSection,
            string.Empty,
            restoredSection.SectionVersion,
            beforeInvalid,
            "empty grand-project payload was accepted");
        DungeonGrandProjectSaveData legacyPayload =
            JsonUtility.FromJson<DungeonGrandProjectSaveData>(json);
        legacyPayload.version--;
        RequireStrictRejectsWithoutMutation(
            restoredSection,
            JsonUtility.ToJson(legacyPayload),
            restoredSection.SectionVersion,
            beforeInvalid,
            "legacy grand-project payload version was accepted");
    }

    private static void ValidateGrandProjectPhysicalTransaction()
    {
        GrandProjectPhysicalFixturePort sourcePort = new(
            failFirstAcknowledge: true);
        EconomyProjectInputOwnerFixtureAuthority sourceInputOwners =
            CreateEconomyProjectInputOwnerFixture();
        GrandProjectRuntime source = new(
            sourcePort,
            sourcePort,
            new FixedGameClock(),
            new DungeonRuntimeAggregateRootStore(),
            sourceInputOwners.Runtime);
        source.Initialize();
        Require(
            source.Start(GrandProjectRuntime.DeepMiningNetworkId, out _),
            "grand-project physical fixture could not start the project");
        GrandProjectDefinition definition = source.Definitions.Single(value =>
            value.ProjectId == GrandProjectRuntime.DeepMiningNetworkId);
        Require(
            source.ApplyWork(
                sourcePort.Office.InstanceId,
                definition.RequiredWork,
                out bool completed)
            && completed
            && source.IsCompleted(definition.ProjectId),
            "grand-project physical outcome was not published after its exact Sink");

        DungeonGrandProjectSaveData pending = source.Capture();
        GrandProjectPhysicalCommitSaveData owner = pending.state.pendingPhysicalCommit;
        Require(
            pending.version == DungeonGrandProjectSaveData.CurrentVersion
            && owner.phase == GrandProjectPhysicalCommitPhase.OutcomePublished
            && sourcePort.CommitCount == 1
            && sourcePort.AcknowledgeCount == 1,
            "grand-project acknowledgement fault did not preserve one durable owner");

        PhysicalItemRestoreCandidateDispositionSnapshot physical =
            sourcePort.CaptureRestoreReceipt();
        SinglePhysicalItemRestoreCandidateQuery matching = new(physical);
        GrandProjectSaveSection.ValidatePhysicalRestoreCandidate(
            pending,
            matching);
        bool missingRejected = false;
        try
        {
            GrandProjectSaveSection.ValidatePhysicalRestoreCandidate(
                pending,
                EmptyPhysicalItemRestoreCandidateQuery.Instance);
        }
        catch (InvalidOperationException)
        {
            missingRejected = true;
        }
        DungeonGrandProjectSaveData noOwner = JsonUtility.FromJson<
            DungeonGrandProjectSaveData>(JsonUtility.ToJson(pending));
        noOwner.state.pendingPhysicalCommit =
            new GrandProjectPhysicalCommitSaveData();
        bool orphanRejected = false;
        try
        {
            GrandProjectSaveSection.ValidatePhysicalRestoreCandidate(
                noOwner,
                matching);
        }
        catch (InvalidOperationException)
        {
            orphanRejected = true;
        }
        Require(
            missingRejected && orphanRejected,
            "grand-project physical restore accepted a missing or orphan Sink receipt");

        GrandProjectPhysicalFixturePort restoredPort = new(
            failFirstAcknowledge: false,
            restoredReceipt: sourcePort.PendingReceipt);
        EconomyProjectInputOwnerFixtureAuthority restoredInputOwners =
            CreateEconomyProjectInputOwnerFixture();
        GrandProjectRuntime restored = new(
            restoredPort,
            restoredPort,
            new FixedGameClock(),
            new DungeonRuntimeAggregateRootStore(),
            restoredInputOwners.Runtime);
        GrandProjectSaveSection restoredSection = new(
            restored,
            matching,
            restoredInputOwners.RestoreRuntime,
            new FixedRestoreWorldCandidateQuery());
        DungeonGameRestoreReport report = new();
        restoredSection.Restore(
            JsonUtility.ToJson(pending),
            DungeonGrandProjectSaveData.CurrentVersion,
            report);
        restored.Tick();
        DungeonGrandProjectSaveData terminal = restored.Capture();
        Require(
            report.Success
            && terminal.state.pendingPhysicalCommit.phase
                == GrandProjectPhysicalCommitPhase.None
            && restoredPort.CommitCount == 0
            && restoredPort.AcknowledgeCount == 1
            && restored.IsCompleted(definition.ProjectId),
            "grand-project acknowledgement-only restore replay duplicated input or outcome");
    }

    private static void ValidateStockPolicySaveBoundary()
    {
        ResourceEconomyContentCatalog catalog = LoadCatalog();
        EconomyProjectInputOwnerFixtureAuthority sourceInputOwners =
            CreateEconomyProjectInputOwnerFixture();
        FakeResourceStockPolicyRuntime source =
            new FakeResourceStockPolicyRuntime(catalog);
        ResourceStockPolicySaveSection sourceSection =
            new ResourceStockPolicySaveSection(
                source,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                sourceInputOwners.RestoreRuntime);
        string canonicalJson = sourceSection.Capture();

        FakeResourceStockPolicyRuntime target =
            new FakeResourceStockPolicyRuntime(catalog);
        EconomyProjectInputOwnerFixtureAuthority targetInputOwners =
            CreateEconomyProjectInputOwnerFixture();
        ResourceStockPolicySaveSection targetSection =
            new ResourceStockPolicySaveSection(
                target,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                targetInputOwners.RestoreRuntime);
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        targetSection.Restore(
            canonicalJson,
            targetSection.SectionVersion,
            validReport);
        object sectionContract = targetSection;
        Require(
            validReport.Success
            && target.RestoreCount == 1
            && string.Equals(
                targetSection.Capture(),
                canonicalJson,
                StringComparison.Ordinal)
            && sectionContract is IDungeonSaveSectionPreflight
            && sectionContract is IDungeonRollbackFreeSaveSection
            && sectionContract is not IOptionalDungeonSaveSection
            && sectionContract is not IDungeonStagedOptionalSaveSection,
            "stock-policy save section did not preserve its strict canonical contract");

        DungeonResourceStockPolicySaveData invalid =
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                canonicalJson);
        invalid.policies[0].minimumStock = -1;
        string beforeInvalid = targetSection.Capture();
        RequireStrictRejectsWithoutMutation(
            targetSection,
            JsonUtility.ToJson(invalid),
            beforeInvalid,
            "invalid stock-policy payload mutated live state");
        RequireStrictRejectsWithoutMutation(
            targetSection,
            canonicalJson,
            targetSection.SectionVersion - 1,
            beforeInvalid,
            "legacy stock-policy section version was accepted");
        RequireStrictRejectsWithoutMutation(
            targetSection,
            string.Empty,
            targetSection.SectionVersion,
            beforeInvalid,
            "empty stock-policy payload was accepted");
        DungeonResourceStockPolicySaveData legacyPayload =
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                canonicalJson);
        legacyPayload.version--;
        RequireStrictRejectsWithoutMutation(
            targetSection,
            JsonUtility.ToJson(legacyPayload),
            targetSection.SectionVersion,
            beforeInvalid,
            "legacy stock-policy payload version was accepted");
        Require(target.RestoreCount == 1,
            "invalid stock-policy payload reached publish");
    }

    private static void ValidateRegionalSupplyContractSaveBoundary()
    {
        ResourceEconomyContentCatalog catalog = LoadCatalog();
        EconomyProjectInputOwnerFixtureAuthority sourceInputOwners =
            CreateEconomyProjectInputOwnerFixture();
        string itemId = catalog.Items
            .Where(item => item != null)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .First().ItemId;
        DungeonRegionalSupplyContractSaveData canonical =
            new DungeonRegionalSupplyContractSaveData
            {
                currentDay = 1,
                nextOfferDay = 4,
                nextSequence = 2,
                contracts = new List<RegionalSupplyContractState>
                {
                    new RegionalSupplyContractState
                    {
                        contractId = "contract:1:1",
                        title = "Fixture supply contract",
                        regionName = "Fixture region",
                        offeredDay = 1,
                        deadlineDay = 4,
                        rewardGold = 25,
                        status = RegionalSupplyContractStatus.Offered,
                        destinationId = string.Empty,
                        lastStatus = string.Empty,
                        requirements = new List<RegionalSupplyContractRequirement>
                        {
                            new RegionalSupplyContractRequirement
                            {
                                itemId = itemId,
                                amount = 2
                            }
                        }
                    }
                }
            };
        FakeRegionalSupplyContractRuntime source =
            new FakeRegionalSupplyContractRuntime(canonical);
        RegionalSupplyContractSaveSection sourceSection =
            new RegionalSupplyContractSaveSection(
                source,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                sourceInputOwners.RestoreRuntime);
        string canonicalJson = sourceSection.Capture();

        FakeRegionalSupplyContractRuntime target =
            new FakeRegionalSupplyContractRuntime(canonical);
        EconomyProjectInputOwnerFixtureAuthority targetInputOwners =
            CreateEconomyProjectInputOwnerFixture();
        RegionalSupplyContractSaveSection targetSection =
            new RegionalSupplyContractSaveSection(
                target,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                targetInputOwners.RestoreRuntime);
        DungeonGameRestoreReport validReport = new DungeonGameRestoreReport();
        targetSection.Restore(
            canonicalJson,
            targetSection.SectionVersion,
            validReport);
        object sectionContract = targetSection;
        Require(
            validReport.Success
            && target.RestoreCount == 1
            && string.Equals(
                targetSection.Capture(),
                canonicalJson,
                StringComparison.Ordinal)
            && sectionContract is IDungeonSaveSectionPreflight
            && sectionContract is IDungeonRollbackFreeSaveSection
            && sectionContract is not IOptionalDungeonSaveSection
            && sectionContract is not IDungeonStagedOptionalSaveSection,
            "regional-contract save section did not preserve its strict canonical contract");

        DungeonRegionalSupplyContractSaveData invalid =
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                canonicalJson);
        invalid.contracts[0].destinationId = "regional-contract:wrong";
        string beforeInvalid = targetSection.Capture();
        RequireStrictRejectsWithoutMutation(
            targetSection,
            JsonUtility.ToJson(invalid),
            beforeInvalid,
            "invalid regional-contract payload mutated live state");
        RequireStrictRejectsWithoutMutation(
            targetSection,
            canonicalJson,
            targetSection.SectionVersion - 1,
            beforeInvalid,
            "legacy regional-contract section version was accepted");
        RequireStrictRejectsWithoutMutation(
            targetSection,
            string.Empty,
            targetSection.SectionVersion,
            beforeInvalid,
            "empty regional-contract payload was accepted");
        DungeonRegionalSupplyContractSaveData legacyPayload =
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                canonicalJson);
        legacyPayload.version--;
        RequireStrictRejectsWithoutMutation(
            targetSection,
            JsonUtility.ToJson(legacyPayload),
            targetSection.SectionVersion,
            beforeInvalid,
            "legacy regional-contract payload version was accepted");
        Require(target.RestoreCount == 1,
            "invalid regional-contract payload reached publish");
    }

    private static void ValidateEconomyPlanningLateFailureDiscard()
    {
        ResourceEconomyContentCatalog catalog = LoadCatalog();
        DungeonRuntimeAggregateRootStore aggregateRootStore = new();
        EconomyProjectInputOwnerFixtureAuthority inputOwners =
            CreateEconomyProjectInputOwnerFixture();
        FakeProductionItemGateway items = new();
        GrandProjectApplicationAdapter grandProjectAdapter =
            new GrandProjectApplicationAdapter(
                items,
                UnavailableGrandProjectPhysicalSink.Instance,
                new EmptyBuildingWorldQuery(),
                new FixedDropZoneQuery(new Vector2Int(4, 1)),
                EmptyResearchRuntimeReferences.Instance,
                workforce: null,
                facilityCandidates: null);
        GrandProjectRuntime grandProjects = new GrandProjectRuntime(
            grandProjectAdapter,
            grandProjectAdapter,
            new FixedGameClock(),
            aggregateRootStore: aggregateRootStore,
            inputOwners: inputOwners.Runtime);
        FakeResourceStockPolicyRuntime stockPolicies =
            new FakeResourceStockPolicyRuntime(catalog, aggregateRootStore);
        DungeonRegionalSupplyContractSaveData emptyContracts =
            new DungeonRegionalSupplyContractSaveData
            {
                currentDay = 1,
                nextOfferDay = 1,
                nextSequence = 1,
                contracts = new List<RegionalSupplyContractState>()
            };
        FakeRegionalSupplyContractRuntime regionalContracts =
            new FakeRegionalSupplyContractRuntime(
                emptyContracts,
                aggregateRootStore);

        GrandProjectSaveSection grandProjectSection =
            new GrandProjectSaveSection(
                grandProjects,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                inputOwners.RestoreRuntime,
                new FixedRestoreWorldCandidateQuery());
        ResourceStockPolicySaveSection stockPolicySection =
            new ResourceStockPolicySaveSection(
                stockPolicies,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                inputOwners.RestoreRuntime);
        RegionalSupplyContractSaveSection regionalSection =
            new RegionalSupplyContractSaveSection(
                regionalContracts,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                inputOwners.RestoreRuntime);

        RequiredDependencyStubSection physicalItems = new(
            PhysicalItemsSaveSection.Id,
            DungeonSaveRestorePhase.Items);
        RequiredDependencyStubSection modularFacilities = new(
            ModularFacilityWorldSaveSection.Id,
            DungeonSaveRestorePhase.World);
        RequiredDependencyStubSection productionBills = new(
            ProductionBillsSaveSection.Id,
            DungeonSaveRestorePhase.RuntimeState);
        FinalFailingSection finalFailure = new(new[]
        {
            GrandProjectSaveSection.Id,
            ResourceStockPolicySaveSection.Id,
            RegionalSupplyContractSaveSection.Id
        });
        IDungeonSaveSection[] sections =
        {
            modularFacilities,
            physicalItems,
            productionBills,
            grandProjectSection,
            stockPolicySection,
            regionalSection,
            finalFailure
        };
        DungeonSaveSectionRegistry registry = new(
            sections,
            aggregateRootStore,
            inputOwners.RestoreParticipants);

        string grandProjectBefore = grandProjectSection.Capture();
        string stockPolicyBefore = stockPolicySection.Capture();
        string regionalBefore = regionalSection.Capture();
        int revisionBefore = aggregateRootStore.PublishedRestoreRevision;

        DungeonGrandProjectSaveData incomingGrandProject =
            JsonUtility.FromJson<DungeonGrandProjectSaveData>(
                grandProjectBefore);
        incomingGrandProject.state.lastStatus = "incoming";
        DungeonResourceStockPolicySaveData incomingStockPolicy =
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                stockPolicyBefore);
        incomingStockPolicy.policies[0].lastStatus = "incoming";
        DungeonRegionalSupplyContractSaveData incomingRegional =
            CreateRegionalSupplyContractFixture(catalog);

        Dictionary<string, string> payloadById = new(StringComparer.Ordinal)
        {
            [grandProjectSection.SectionId] =
                JsonUtility.ToJson(incomingGrandProject),
            [stockPolicySection.SectionId] =
                JsonUtility.ToJson(incomingStockPolicy),
            [regionalSection.SectionId] = JsonUtility.ToJson(incomingRegional)
        };
        List<DungeonSaveSectionEnvelope> envelopes = sections
            .Select(section => new DungeonSaveSectionEnvelope
            {
                sectionId = section.SectionId,
                sectionVersion = section.SectionVersion,
                restorePhase = section.RestorePhase,
                optional = false,
                payloadJson = payloadById.TryGetValue(
                    section.SectionId,
                    out string payload)
                    ? payload
                    : section.Capture()
            })
            .ToList();
        DungeonGameRestoreReport report = new();
        bool restored = registry.RestoreAll(envelopes, report);

        Require(
            !restored
            && !report.Success
            && finalFailure.WasCommitted
            && aggregateRootStore.PublishedRestoreRevision == revisionBefore
            && string.Equals(
                grandProjectSection.Capture(),
                grandProjectBefore,
                StringComparison.Ordinal)
            && string.Equals(
                stockPolicySection.Capture(),
                stockPolicyBefore,
                StringComparison.Ordinal)
            && string.Equals(
                regionalSection.Capture(),
                regionalBefore,
                StringComparison.Ordinal),
            "economy planning late failure leaked a staged Aggregate state");
    }

    private static DungeonRegionalSupplyContractSaveData
        CreateRegionalSupplyContractFixture(
            IResourceEconomyContentCatalog catalog)
    {
        string itemId = catalog.Items
            .Where(item => item != null)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .First().ItemId;
        return new DungeonRegionalSupplyContractSaveData
        {
            currentDay = 1,
            nextOfferDay = 4,
            nextSequence = 2,
            contracts = new List<RegionalSupplyContractState>
            {
                new RegionalSupplyContractState
                {
                    contractId = "contract:1:1",
                    title = "Fixture supply contract",
                    regionName = "Fixture region",
                    offeredDay = 1,
                    deadlineDay = 4,
                    rewardGold = 25,
                    status = RegionalSupplyContractStatus.Offered,
                    destinationId = string.Empty,
                    lastStatus = string.Empty,
                    requirements = new List<RegionalSupplyContractRequirement>
                    {
                        new RegionalSupplyContractRequirement
                        {
                            itemId = itemId,
                            amount = 2
                        }
                    }
                }
            }
        };
    }

    private static FacilityData CreateCraftFacilityData()
    {
        FacilityData data = new FacilityData
        {
            requiredWorkers = 1
        };
        data.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Craft });
        return data;
    }

    private static ResourceItemDefinitionSO CreateFixtureItem(
        string itemId,
        ResourceItemKind kind,
        StockCategory category)
    {
        ResourceItemDefinitionSO item =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        item.Configure(
            itemId,
            itemId,
            "생산 계약 검증용 아이템",
            category,
            kind,
            ResourceIngredientTag.None,
            1,
            1f,
            100,
            string.Empty);
        return item;
    }

    private static ResourceEconomyContentCatalog LoadCatalog()
    {
        return new ResourceEconomyContentCatalog(
            LoadAll<ResourceItemDefinitionSO>("Assets/Resources/SO/Economy/Items"),
            LoadAll<ProductionRecipeSO>("Assets/Resources/SO/Economy/Recipes"),
            LoadAll<CropDefinitionSO>("Assets/Resources/SO/Economy/Crops"),
            LoadAll<CraftMaterialDefinitionSO>("Assets/Resources/SO/Economy/Materials"));
    }

    private static EconomyProjectInputOwnerFixtureAuthority
        CreateEconomyProjectInputOwnerFixture() => new(
            new PhysicalItemMassQuery(EditorItemCatalogFactory.Create()));

    private static T[] LoadAll<T>(string root)
        where T : UnityEngine.Object
    {
        return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(asset => asset != null)
            .ToArray();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void RequireStrictRejectsWithoutMutation(
        IDungeonSaveSection section,
        string payloadJson,
        string before,
        string message)
    {
        RequireStrictRejectsWithoutMutation(
            section,
            payloadJson,
            section.SectionVersion,
            before,
            message);
    }

    private static void RequireStrictRejectsWithoutMutation(
        IDungeonSaveSection section,
        string payloadJson,
        int sectionVersion,
        string before,
        string message)
    {
        bool rejected = false;
        try
        {
            ((IDungeonStagedSaveSection)section).StageRestore(
                payloadJson,
                sectionVersion,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(
            rejected
            && string.Equals(section.Capture(), before, StringComparison.Ordinal),
            message);
    }

    private static void ValidateTerminalPreparedOutputBillRetirement()
    {
        ProductionRecipeSO recipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        ResourceItemDefinitionSO input =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        ResourceItemDefinitionSO output =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject facilityObject =
            new("Terminal Prepared Output Retirement Facility");
        GameObject workerObject =
            new("Terminal Prepared Output Retirement Worker");
        try
        {
            const string inputItemId = "resource:terminal-route-input";
            const string outputItemId = "feed:terminal-route-output";
            input.Configure(
                inputItemId,
                "Terminal route input",
                "Prepared-output terminal retention fixture input.",
                StockCategory.General,
                ResourceItemKind.Raw,
                ResourceIngredientTag.Plant,
                1,
                1f,
                100,
                string.Empty);
            output.Configure(
                outputItemId,
                "Terminal route output",
                "Prepared-output terminal retention fixture output.",
                StockCategory.General,
                ResourceItemKind.Intermediate,
                ResourceIngredientTag.Plant,
                1,
                1f,
                100,
                string.Empty);
            recipe.Configure(
                "recipe:hay-feed",
                "Terminal route recipe",
                "Retains a terminal RepeatCount bill until routing drains.",
                "feedbench",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                1f,
                new[] { new ItemAmountDefinition(inputItemId, 1) },
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:main",
                        ProductionOutputRole.Main,
                        outputItemId,
                        1)
                });
            recipe.ConfigureWorkshop(
                "workstation:feedbench",
                Array.Empty<string>(),
                ProductionProcessKind.WorkOnly);
            recipe.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.FoodProduction);

            BuildingAbilityCollection abilities = new();
            abilities.Add(new BuildingFacilityAbility
            {
                settings = CreateCraftFacilityData()
            });
            abilities.Add(new BuildingSemanticTagsAbility
            {
                tags = new[] { "feedbench" }
            });
             abilities.Add(new BuildingProductionWorkstationAbility
             {
                 workstationTag = "workstation:feedbench",
                 lanePolicy = ProductionWorkstationLanePolicy
                     .ManualWithDetachedBatchProcessors,
                 manualWorkLaneCount = 1,
                 automaticWorkLaneCount = 0
            });
            abilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 4,
                physicalOutputBufferCycleCapacity = 4
            });
            building.id = 99107;
            building.objectName = "Terminal route feedbench";
            building.ReplaceAbilities(abilities);

            BuildableObject facility =
                facilityObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(building, new Vector2Int(11, 4));
            CharacterActor worker = workerObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            worker.EnsureRuntimeState();
            worker.Identity.SetPersistentId(
                "character:terminal-route-worker");

            ResourceEconomyContentCatalog catalog = new(
                new[] { input, output },
                new[] { recipe },
                Array.Empty<CropDefinitionSO>(),
                Array.Empty<CraftMaterialDefinitionSO>());
            FakeProductionItemGateway items = new();
            items.SetDefinitionMass(inputItemId, 1_000L);
            MutablePreparedOutputRoutingAuthority routing = new();
            TerminalPreparedOutputExecutionPort prepared = new(routing);
            MutableGameClock clock = new() { DeltaTimeValue = 0.02f };
            ProductionRuntimeFixture runtime = CreateRuntime(
                catalog,
                items,
                seed: 7107,
                buildingWorld: new FixedBuildingWorldQuery(facility),
                clock: clock,
                preparedOutputExecution: prepared,
                preparedOutputRouting: routing);

            ProductionBillCommandResult added = runtime.AddBill(
                facility,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            Require(added.Succeeded,
                "terminal prepared-output fixture bill was not added");
            string inputDestination = ProductionBillRuntime.DestinationPrefix
                + added.BillId.Value;
            items.Deliver(inputItemId, 1, inputDestination);
            ProductionWorkBeginResult begin = runtime.BeginWork(
                worker,
                facility,
                BuiltInWorkTypeIds.Craft);
            Require(begin.Succeeded,
                "terminal prepared-output fixture did not begin work");
            ProductionWorkExecutionResult completed = runtime.ExecuteWork(
                worker,
                facility,
                added.BillId,
                1f);
            Require(completed.Succeeded
                    && completed.CycleCompleted
                    && runtime.GetBills(facility).Single().RemainingCycles == 0
                    && routing.HasOutstandingForBill(added.BillId)
                    && runtime.DestinationClaims.TryGetClaim(
                        inputDestination,
                        facility.centerPos,
                        out _)
                    && items.GetRequested(inputItemId) == 1,
                "terminal bill or its input destination was retired before physical output routing drained");

            ProductionBillCommandResult earlyRemoval = runtime.RemoveBill(
                added.BillId,
                returnMaterials: true);
            runtime.Tick();
            Require(!earlyRemoval.Succeeded
                    && runtime.GetBills(facility).Count == 1
                    && items.GetRequested(inputItemId) == 1,
                "outstanding terminal routing allowed removal or requested another input cycle");

            routing.AllowRetirement(added.BillId);
            runtime.Tick();
            Require(runtime.GetBills(facility).Count == 0
                    && !runtime.DestinationClaims.TryGetClaim(
                        inputDestination,
                        facility.centerPos,
                        out _),
                "drained terminal routing did not retire the bill and its input destination exactly once");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(building);
            UnityEngine.Object.DestroyImmediate(input);
            UnityEngine.Object.DestroyImmediate(output);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void ValidateTerminalExactOutputBillRetirement()
    {
        ProductionRecipeSO recipe =
            ScriptableObject.CreateInstance<ProductionRecipeSO>();
        ResourceItemDefinitionSO input =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        ResourceItemDefinitionSO output =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject facilityObject = new("Terminal Exact Output Retirement Facility");
        GameObject workerObject = new("Terminal Exact Output Retirement Worker");
        try
        {
            const string inputItemId = "resource:terminal-exact-input";
            const string outputItemId = "component:terminal-exact-output";
            input.Configure(
                inputItemId,
                "Terminal exact input",
                "Exact-output terminal retention fixture input.",
                StockCategory.General,
                ResourceItemKind.Raw,
                ResourceIngredientTag.Plant,
                1,
                1f,
                100,
                string.Empty);
            output.Configure(
                outputItemId,
                "Terminal exact output",
                "Exact-output terminal retention fixture output.",
                StockCategory.General,
                ResourceItemKind.FinishedGood,
                ResourceIngredientTag.None,
                1,
                1f,
                1,
                string.Empty);
            recipe.Configure(
                "recipe:qa-terminal-exact-output",
                "Terminal exact route recipe",
                "Retains an exact-capability bill until its physical buffer drains.",
                "mill",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                1f,
                new[] { new ItemAmountDefinition(inputItemId, 1) },
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:main",
                        ProductionOutputRole.Main,
                        outputItemId,
                        1)
                });
            recipe.ConfigureWorkshop(
                "workstation:mill",
                Array.Empty<string>(),
                ProductionProcessKind.WorkOnly);
            recipe.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.Crafting);

            BuildingAbilityCollection abilities = new();
            abilities.Add(new BuildingFacilityAbility
            {
                settings = CreateCraftFacilityData()
            });
            abilities.Add(new BuildingSemanticTagsAbility
            {
                tags = new[] { "mill" }
            });
             abilities.Add(new BuildingProductionWorkstationAbility
             {
                 workstationTag = "workstation:mill",
                 lanePolicy = ProductionWorkstationLanePolicy
                     .ManualWithDetachedBatchProcessors,
                 manualWorkLaneCount = 1,
                 automaticWorkLaneCount = 0
             });
            abilities.Add(new BuildingProductionBufferAbility
            {
                defaultBatchCapacity = 3,
                physicalOutputBufferCycleCapacity = 3
            });
            building.id = 99108;
            building.objectName = "Terminal exact mill";
            building.ReplaceAbilities(abilities);

            BuildableObject facility =
                facilityObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(building, new Vector2Int(12, 4));
            CharacterActor worker = workerObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            worker.EnsureRuntimeState();
            worker.Identity.SetPersistentId("character:terminal-exact-worker");

            ResourceEconomyContentCatalog catalog = new(
                new[] { input, output },
                new[] { recipe },
                Array.Empty<CropDefinitionSO>(),
                Array.Empty<CraftMaterialDefinitionSO>());
            FakeProductionItemGateway items = new();
            items.SetDefinitionMass(inputItemId, 1_000L);
            items.SetDefinitionMass(outputItemId, 1_000L);
            BufferedExactOutputTestHandler handler = new(items, outputItemId);
            MutableGameClock clock = new() { DeltaTimeValue = 0.02f };
            ProductionRuntimeFixture runtime = CreateRuntime(
                catalog,
                items,
                seed: 7108,
                buildingWorld: new FixedBuildingWorldQuery(facility),
                clock: clock,
                outputHandlers: new[] { handler });

            ProductionBillCommandResult added = runtime.AddBill(
                facility,
                recipe.RecipeId,
                ProductionOrderMode.RepeatCount,
                1);
            Require(added.Succeeded,
                "terminal exact-output fixture bill was not added");
            string inputDestination = ProductionBillRuntime.DestinationPrefix
                + added.BillId.Value;
            string outputDestination = ProductionBillRuntime.OutputDestinationPrefix
                + facility.PersistentInstanceId.Value;
            items.Deliver(inputItemId, 1, inputDestination);
            ProductionWorkBeginResult begin = runtime.BeginWork(
                worker,
                facility,
                BuiltInWorkTypeIds.Craft);
            Require(begin.Succeeded,
                "terminal exact-output fixture did not begin work");
            ProductionWorkExecutionResult completed = runtime.ExecuteWork(
                worker,
                facility,
                added.BillId,
                1f);
            Require(completed.Succeeded
                    && completed.CycleCompleted
                     && runtime.GetBills(facility).Single().RemainingCycles == 0
                     && items.CountBufferedOutput(outputItemId, outputDestination) == 1
                     && !runtime.DestinationClaims.TryGetClaim(
                         inputDestination,
                         facility.centerPos,
                         out _),
                "terminal exact-capability bill did not separate drained input ownership from outstanding output routing");

            Require(items.TryRouteBufferedOutput(
                        outputDestination,
                        outputItemId,
                        1,
                        facility.centerPos,
                        string.Empty,
                        out int routed,
                        out DomainFailure routeFailure)
                    && routed == 1,
                "terminal exact-output fixture could not drain its physical lot: "
                + routeFailure.Code);
            runtime.Tick();
            Require(runtime.GetBills(facility).Count == 0
                    && !runtime.DestinationClaims.TryGetClaim(
                        inputDestination,
                        facility.centerPos,
                        out _),
                "drained terminal exact-output bill did not retire exactly once");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(building);
            UnityEngine.Object.DestroyImmediate(input);
            UnityEngine.Object.DestroyImmediate(output);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static ProductionOutputHandlerRegistry CreateOutputHandlerRegistry(
        IResourceEconomyContentCatalog catalog,
        IProductionOutputBufferGateway outputBuffer,
        IEnumerable<IProductionOutputHandler> additional = null,
        IProductionPreparedOutputComponentCodec componentCodec = null)
    {
        var capabilities = new List<IProductionOutputCapability>
        {
            new PerishableFoodOutputCapability(
                new ResourceItemDefinitionCatalog(catalog.Items)),
            new StandardDefinitionProductionOutputCapability(
                catalog,
                componentCodec
                    ?? new ProductionPreparedOutputComponentCodec())
        };
        if (additional != null)
            capabilities.AddRange(additional);
        return new ProductionOutputHandlerRegistry(capabilities);
    }

    private static ProductionRuntimeFixture CreateRuntime(
        IResourceEconomyContentCatalog catalog,
        IProductionItemGateway items,
        int seed,
        IProductionWorkshopRuntime workshops = null,
        IBuildingWorldQuery buildingWorld = null,
        IPowerInfrastructureQuery power = null,
        IGameClock clock = null,
        IProductionPreparedOutputExecutionPort preparedOutputExecution = null,
        IProductionPreparedOutputRoutingAuthority preparedOutputRouting = null,
        IEnumerable<IProductionOutputHandler> outputHandlers = null)
    {
        workshops ??= EmptyProductionWorkshopRuntime.Instance;
        buildingWorld ??= new FixedBuildingWorldQuery();
        power ??= new MutablePowerRuntime();
        clock ??= new MutableGameClock();
        preparedOutputExecution ??=
            FailLoudPreparedOutputExecutionPort.Instance;
        NoOpWorkforceReplanService workforce =
            NoOpWorkforceReplanService.Instance;
        IProductionInputLogisticsService inputLogistics =
            new ProductionInputLogisticsService(
                catalog,
                items,
                EmptyResearchRuntimeReferences.Instance,
                workforce,
                workshops);
        IProductionCycleUtilityService cycleUtilities =
            new TestProductionCycleUtilityService(workshops, power);
        IProductionAssemblyBridge bridge = new ProductionAssemblyBridgeAdapter(
            items,
            items as IProductionOutputBufferGateway
                ?? throw new InvalidOperationException(
                    "Production test item gateway must expose an output buffer."),
            items as IProductionStockSensorPhysicalGateway
                ?? throw new InvalidOperationException(
                    "Production test item gateway must expose stock-sensor physical transactions."),
            inputLogistics,
            cycleUtilities,
            workshops,
            buildingWorld,
            EmptyWarehouseWorldQuery.Instance,
            workforce,
            CreateOutputHandlerRegistry(
                catalog,
                items as IProductionOutputBufferGateway
                    ?? throw new InvalidOperationException(
                        "Production test item gateway must expose an output buffer."),
                outputHandlers),
            narrativeQualification: null,
            performance: () =>
                CharacterAiEditorTestDependencies.NeutralPerformance);
        IProductionOutputPlanningService outputPlanning =
            new ProductionOutputPlanningService(catalog, bridge);
        IProductionOutputExecutionService outputExecution =
            new ProductionOutputExecutionService(
                bridge,
                EmptyGrandProjectBenefitQuery.Instance,
                outputPlanning,
                new RandomStreamProvider(seed));
        DungeonRuntimeAggregateRootStore productionRoots = new();
        ProductionAggregateStateStore stateStore =
            new ProductionAggregateStateStore(productionRoots);
        FacilityBufferDestinationClaimRegistry destinationClaims = new();
        EmptyFacilityBufferPhysicalOccupancyQuery occupancy = new();
        FacilityBufferMassAdmissionService destinationCapacities = new(
            destinationClaims,
            occupancy);
        FacilityBufferDestinationLifecycleService destinationLifecycle = new(
            destinationClaims,
            destinationClaims,
            destinationCapacities,
            destinationCapacities);
        IProductionInputDestinationClaimRuntime inputDestinationClaims =
            new ProductionInputDestinationClaimRuntime(
                destinationClaims,
                destinationCapacities,
                destinationClaims,
                destinationCapacities,
                destinationLifecycle);
        IProductionStockSensorRuntime stockSensors =
            new ProductionStockSensorRuntime(
                bridge,
                stateStore,
                items as IProductionStockSensorRemovalOutputGateway
                    ?? throw new InvalidOperationException(
                        "Production test item gateway must expose stock-sensor removal outputs."),
                new ProductionStockSensorDestinationAuthorityRuntime(
                    items,
                    destinationClaims,
                    destinationClaims,
                    destinationCapacities,
                    destinationCapacities,
                    occupancy,
                    destinationLifecycle),
                new ProductionFacilityDestructiveDrainOpenOperationQuery(
                    productionRoots));
        IProductionBillSnapshotProjector snapshots =
            new ProductionBillSnapshotProjector(
                catalog,
                bridge,
                outputPlanning,
                preparedOutputExecution,
                stockSensors,
                EmptyProductionDistributionQuery.Instance);
        ProductionBillOrderDependencies order = new(
            catalog,
            bridge,
            stockSensors,
            stateStore,
            inputDestinationClaims,
            new ProductionFacilityMutationEpochRuntime());
        ProductionBillExecutionDependencies execution = new(
            outputPlanning,
            outputExecution,
            preparedOutputExecution,
            FailLoudPreparedOutputExecutionPort.Instance,
            snapshots,
            bridge,
            clock,
            preparedOutputRouting);
        ProductionBillRuntime core = new(order, execution);
        ProductionBillSceneFacade scene = new(
            core,
            core,
            core,
            bridge,
            ManualExecutionModeQuery.Instance,
            new ExtremeTraitRuntime(new CharacterIdentityStateStore()),
            clock,
            new CharacterIdentityEventPublisher(new GameEventBus()));
        return new ProductionRuntimeFixture(
            core,
            scene,
            bridge,
            destinationClaims,
            destinationCapacities);
    }

    private sealed class ManualExecutionModeQuery : IAutomationExecutionModeQuery
    {
        public static ManualExecutionModeQuery Instance { get; } = new();

        public AutomationMode GetMode(BuildingInstanceId facilityId)
        {
            if (!facilityId.IsValid)
            {
                throw new ArgumentException(
                    "Execution-mode fixture requires a valid facility ID.",
                    nameof(facilityId));
            }
            return AutomationMode.Manual;
        }
    }

    private sealed class TerminalPreparedOutputExecutionPort :
        IProductionPreparedOutputExecutionPort
    {
        private static readonly string DefinitionDigest = new('a', 64);
        private static readonly string OutcomeFingerprint = new('b', 64);
        private static readonly string AdmissionFingerprint = new('c', 64);
        private static readonly string ComponentFingerprint = new('d', 64);
        private readonly IProductionPreparedOutputRoutingAuthority routing;

        public TerminalPreparedOutputExecutionPort(
            IProductionPreparedOutputRoutingAuthority routing)
        {
            this.routing = routing
                ?? throw new ArgumentNullException(nameof(routing));
        }

        public void RestoreDestinationAuthorities(
            IReadOnlyList<ProductionBillRecord> records,
            IReadOnlyList<ProductionFacilityHandle> facilities)
        {
        }

        public ProductionPreparedOutputCapacityResult AssessCycleStart(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility) =>
            ProductionPreparedOutputCapacityResult.Available(
                4_000L,
                0L,
                0L);

        public ProductionPreparedOutputCapacityResult AssessCurrentCapacity(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility) =>
            AssessCycleStart(record, recipe, facility);

        public ProductionPreparedOutputExecutionResult Execute(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility,
            ProductionWorkerHandle worker)
        {
            string batchCommitId = ProductionPreparedOutputIdentity
                .BuildBatchCommitId(
                    record.billId,
                    record.cycleSequence,
                    OutcomeFingerprint);
            string lineCommitId = ProductionPreparedOutputIdentity
                .BuildLineCommitId(batchCommitId, "output:main");
            ProductionPreparedOutputBatchSaveData resolved = new()
            {
                phase = ProductionPreparedOutputPhase
                    .ResolvedWaitingForOutputSpace,
                billId = record.billId.Value,
                cycleSequence = record.cycleSequence,
                recipeId = record.recipeId,
                destinationId = record.outputDestinationId,
                recipeDefinitionDigest = DefinitionDigest,
                migrationProfileDigest = new string('f', 64),
                capacitySourceDigest = new string('e', 64),
                maximumMassProofDigest = new string('d', 64),
                maximumBatchMassGrams = 1_000L,
                capacityClaimDigest = new string('c', 64),
                outputBufferCycleCapacity = 4,
                projectedPortfolioCapacityGrams = 4_000L,
                requiredMinimumCapacityGrams = 4_000L,
                outcomeFingerprint = OutcomeFingerprint,
                batchCommitId = batchCommitId,
                totalPhysicalMassGrams = 1_000L,
                totalDeclaredLossMassGrams = 0L,
                lines = new List<ProductionPreparedOutputLineSaveData>
                {
                    new()
                    {
                        outputLineId = "output:main",
                        role = ProductionOutputRole.Main,
                        itemId = "feed:terminal-route-output",
                        outputCapabilityId =
                            ProductionOutputCapabilityIds.StandardDefinition,
                        outputCapabilityVersion =
                            ProductionOutputCapabilityIds.StandardDefinitionVersion,
                        outputComponentCodecId =
                            ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                        outputComponentCodecVersion =
                            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
                        outputCapabilityFingerprint =
                            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                                "output:main",
                                "feed:terminal-route-output",
                                ProductionOutputCapabilityIds.StandardDefinition,
                                ProductionOutputCapabilityIds.StandardDefinitionVersion,
                                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion),
                        quantity = 1,
                        componentPayload = string.Empty,
                        componentFingerprint = ComponentFingerprint,
                        qualityPermille = 1_000,
                        rollKind = "deterministic",
                        rollValue = 0L,
                        rollUpperExclusive = 1L,
                        rollSucceeded = true,
                        exactMassGrams = 1_000L,
                        lineCommitId = lineCommitId
                    }
                }
            };
            record.ResolvePreparedOutput(resolved);
            record.MarkPreparedOutputPublicationPrepared(AdmissionFingerprint);
            record.MarkPreparedOutputPhysicalBatchCommitted(new[]
            {
                new ProductionPreparedOutputPhysicalCandidateSaveData
                {
                    stackId = "stack:terminal-route-output",
                    batchCommitId = batchCommitId,
                    outputLineId = "output:main",
                    lineCommitId = lineCommitId,
                    itemId = "feed:terminal-route-output",
                    quantity = 1,
                    massGrams = 1_000L,
                    destinationId = record.outputDestinationId,
                    state = ProductionPreparedPhysicalCandidateState
                        .FacilityOutputBuffer
                }
            });
            record.MarkPreparedOutputCompleted();
            routing.PublishCommittedBatch(
                record.preparedOutput.Clone(),
                facility.InstanceId);
            return ProductionPreparedOutputExecutionResult.Completed();
        }

        public ProductionPreparedOutputReleaseResult Release(
            ProductionBillRecord record,
            ProductionWipTerminalReason reason)
        {
            ProductionPreparedOutputPhase phase = record.preparedOutput?.phase
                ?? ProductionPreparedOutputPhase.Unresolved;
            if (phase == ProductionPreparedOutputPhase.Unresolved)
            {
                return ProductionPreparedOutputReleaseResult
                    .ReleasedUnpublished();
            }
            if (phase ==
                ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace)
            {
                record.ReleaseUnpublishedPreparedOutput();
                return ProductionPreparedOutputReleaseResult
                    .ReleasedUnpublished();
            }
            return ProductionPreparedOutputReleaseResult.Blocked(
                true,
                new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    record.billId.Value,
                    "terminal-route-output-already-published"));
        }
    }

    private sealed class MutablePreparedOutputRoutingAuthority :
        IProductionPreparedOutputRoutingAuthority
    {
        private readonly HashSet<string> outstanding =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> retirable =
            new(StringComparer.Ordinal);

        public void PublishCommittedBatch(
            ProductionPreparedOutputBatchSaveData completedBatch,
            BuildingInstanceId ownerFacilityId)
        {
            ProductionPreparedOutputBatchSaveData exact = completedBatch
                ?? throw new ArgumentNullException(nameof(completedBatch));
            outstanding.Add(exact.billId);
            retirable.Remove(exact.billId);
        }

        public bool HasOutstandingForBill(ProductionBillId ownerBillId) =>
            outstanding.Contains(ownerBillId.Value);

        public bool CanRetireBill(ProductionBillId ownerBillId) =>
            !outstanding.Contains(ownerBillId.Value);

        public void AllowRetirement(ProductionBillId ownerBillId)
        {
            Require(outstanding.Remove(ownerBillId.Value),
                "terminal routing fixture had no outstanding bill to drain");
            retirable.Add(ownerBillId.Value);
        }

        public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
            CaptureAll() =>
            Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>();

        public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
            CaptureBill(ProductionBillId ownerBillId) =>
            Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>();

        public IReadOnlyList<ProductionPreparedOutputRoutingLineSnapshot>
            CaptureDestination(string destinationId) =>
            Array.Empty<ProductionPreparedOutputRoutingLineSnapshot>();

        public ProductionPreparedOutputRouteRequestSnapshot PrepareRoute(
            string batchCommitId,
            string lineCommitId,
            string targetDestinationId,
            int targetPositionX,
            int targetPositionY,
            int routedQuantity) => throw NotUsed();

        public IReadOnlyList<ProductionPreparedOutputRouteRequestSnapshot>
            CaptureRouteOperations() =>
            Array.Empty<ProductionPreparedOutputRouteRequestSnapshot>();

        public void CommitPhysicalRoute(
            ProductionPreparedOutputPhysicalRouteReceipt receipt) =>
            throw NotUsed();

        public void AcknowledgePhysicalRoute(
            string routeOperationId,
            string physicalReceiptFingerprint) => throw NotUsed();

        private static InvalidOperationException NotUsed() => new(
            "The terminal bill retirement fixture does not execute route mutations.");
    }

    private sealed class FailLoudPreparedOutputExecutionPort :
        IProductionPreparedOutputExecutionPort,
        IProductionRuinedBatchExecutionPort
    {
        internal static readonly FailLoudPreparedOutputExecutionPort Instance =
            new();

        private FailLoudPreparedOutputExecutionPort()
        {
        }

        public void RestoreDestinationAuthorities(
            IReadOnlyList<ProductionBillRecord> records,
            IReadOnlyList<ProductionFacilityHandle> facilities)
        {
            if ((records ?? Array.Empty<ProductionBillRecord>()).Any(record =>
                    record?.preparedOutput != null
                    && record.preparedOutput.phase
                        != ProductionPreparedOutputPhase.Unresolved))
            {
                throw MissingAdapter(null);
            }
        }

        public ProductionPreparedOutputCapacityResult AssessCycleStart(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility) => throw MissingAdapter(recipe);

        public ProductionPreparedOutputCapacityResult AssessCurrentCapacity(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility) => throw MissingAdapter(recipe);

        public ProductionPreparedOutputExecutionResult Execute(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility,
            ProductionWorkerHandle worker) => throw MissingAdapter(recipe);

        public ProductionPreparedOutputReleaseResult Release(
            ProductionBillRecord record,
            ProductionWipTerminalReason reason) => throw new InvalidOperationException(
            "The production Economy fixture cannot release a migrated prepared-output bill "
            + $"'{record?.recipeId ?? "null"}' without an explicit Items adapter.");

        public ProductionRuinedBatchExecutionResult ExecuteRuinedBatch(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility) => throw new InvalidOperationException(
            "The production Economy fixture cannot publish a ruined batch "
            + $"'{recipe?.RecipeId ?? "null"}' without an explicit Items adapter.");

        private static InvalidOperationException MissingAdapter(
            ProductionRecipeSO recipe) => new(
            "The production Economy fixture cannot execute migrated prepared-output recipe "
            + $"'{recipe?.RecipeId ?? "null"}' without an explicit Items adapter.");
    }

    private sealed class ProductionRuntimeFixture
    {
        private readonly ProductionBillSceneFacade scene;

        public ProductionRuntimeFixture(
            ProductionBillRuntime core,
            ProductionBillSceneFacade scene,
            IProductionAssemblyBridge bridge,
            IFacilityBufferDestinationClaimQuery destinationClaims,
            IFacilityBufferMassCapacityQuery destinationCapacities)
        {
            Core = core ?? throw new ArgumentNullException(nameof(core));
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
            Bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            DestinationClaims = destinationClaims
                ?? throw new ArgumentNullException(nameof(destinationClaims));
            DestinationCapacities = destinationCapacities
                ?? throw new ArgumentNullException(nameof(destinationCapacities));
        }

        public ProductionBillRuntime Core { get; }
        public IProductionAssemblyBridge Bridge { get; }
        public IFacilityBufferDestinationClaimQuery DestinationClaims { get; }
        public IFacilityBufferMassCapacityQuery DestinationCapacities { get; }
        public int Version => scene.Version;

        public ProductionBillCommandResult AddBill(
            BuildableObject facility,
            string recipeId,
            ProductionOrderMode mode,
            int amount) => scene.AddBill(facility, recipeId, mode, amount);

        public IReadOnlyList<ProductionBillSnapshot> GetBills(
            BuildableObject facility) => scene.GetBills(facility);

        public IReadOnlyList<ProductionFacilityHandle> Facilities =>
            Bridge.Facilities;

        public bool HasStockSensor(BuildableObject facility) =>
            scene.HasStockSensor(facility);

        public ProductionWorkAvailabilityResult CheckWorkAvailability(
            BuildableObject facility,
            WorkTypeId workTypeId) =>
            scene.CheckWorkAvailability(facility, workTypeId);

        public ProductionWorkBeginResult BeginWork(
            CharacterActor worker,
            BuildableObject facility,
            WorkTypeId workTypeId) =>
            scene.BeginWork(worker, facility, workTypeId);

        public ProductionWorkExecutionResult ExecuteWork(
            CharacterActor worker,
            BuildableObject facility,
            ProductionBillId billId,
            float amount) =>
            scene.ExecuteWork(worker, facility, billId, amount);

        public ProductionBillCommandResult RemoveBill(
            ProductionBillId billId,
            bool returnMaterials) => scene.RemoveBill(billId, returnMaterials);

        public void Tick() => Core.Tick();
    }

    private sealed class EmptyGrandProjectBenefitQuery :
        IGrandProjectBenefitQuery
    {
        public static readonly EmptyGrandProjectBenefitQuery Instance = new();

        public float ContractRewardMultiplier => 1f;
        public float DefensePreparationMultiplier => 1f;
        public int ExpeditionSupplyCapacityBonus => 0;

        public bool IsCompleted(string projectId) => false;
        public float GetProductionOutputMultiplier(string facilityTag) => 1f;
    }

    private sealed class EmptyFacilityBufferPhysicalOccupancyQuery :
        IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "production-debug-exact-lot-not-configured";
            return false;
        }
    }

    private sealed class EmptyProductionDistributionQuery :
        IProductionDistributionQuery
    {
        public static readonly EmptyProductionDistributionQuery Instance = new();

        public IReadOnlyList<ProductionConsumerRouteState> GetRouteStates(
            ProductionBillId billId) =>
            Array.Empty<ProductionConsumerRouteState>();
    }

    private static class EmptyResearchRuntimeReferences
    {
        private static BlueprintResearchRuntime runtime;

        public static ProgressionSceneRuntimeReferences Instance
        {
            get
            {
                if (runtime == null)
                {
                    GameObject host = new GameObject("EmptyResearchRuntime")
                    {
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    runtime = host.AddComponent<BlueprintResearchRuntime>();
                    runtime.enabled = false;
                }

                return new ProgressionSceneRuntimeReferences(null, runtime, null);
            }
        }
    }

    private sealed class NoOpWorkforceReplanService :
        IWorkforceReplanService
    {
        public static readonly NoOpWorkforceReplanService Instance = new();

        public void RequestIdleWorkersToReplan(bool clearFailures = true)
        {
        }

        public void RequestOneWorkerToReplanFor(
            WorkTypeId workTypeId,
            bool clearFailures = true,
            bool forceInterrupt = false)
        {
        }

        public void RequestOneHaulerToReplan(
            bool clearFailures = true,
            bool forceInterrupt = false,
            CharacterId protectedCharacterId = default,
            bool forcePriorityWakeFanout = false)
        {
        }
    }

    private sealed class EmptyProductionWorkshopRuntime :
        IProductionWorkshopRuntime
    {
        public static readonly EmptyProductionWorkshopRuntime Instance = new();

        public int Version => 0;

        public IReadOnlyList<ProductionSupportLinkSnapshot> GetLinks(
            BuildableObject workstation) =>
            Array.Empty<ProductionSupportLinkSnapshot>();

        public bool TryGetLinkForSupport(
            BuildableObject support,
            out ProductionSupportLinkSnapshot link)
        {
            link = null;
            return false;
        }

        public bool HasRequiredSupports(
            BuildableObject workstation,
            IReadOnlyList<string> requiredFeatureTags,
            out string failureReason)
        {
            bool valid = requiredFeatureTags == null
                || requiredFeatureTags.All(string.IsNullOrWhiteSpace);
            failureReason = valid ? string.Empty : "test-support-missing";
            return valid;
        }

        public bool TryResolveSupport(
            BuildableObject workstation,
            string featureTag,
            ProductionSupportKind? requiredKind,
            out BuildableObject support,
            out BuildingProductionSupportAbility ability)
        {
            support = null;
            ability = null;
            return false;
        }
    }

    private sealed class RecordingProcessFluidUseRuntime : IProcessFluidUseRuntime
    {
        public int ExplicitCallCount { get; private set; }
        public int BatchCallCount { get; private set; }
        public float LastCleanWater { get; private set; }
        public float LastWastewater { get; private set; }

        public bool EnsureCycleSupply(
            BuildableObject facility,
            WorkTypeId workTypeId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool TryConsumeCycle(
            BuildableObject facility,
            WorkTypeId workTypeId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool TryConsumeCycle(
            BuildableObject facility,
            WorkTypeId workTypeId,
            float cleanWater,
            float wastewater,
            bool allowsManualWaterFallback,
            out DomainFailure failure)
        {
            ExplicitCallCount++;
            LastCleanWater = cleanWater;
            LastWastewater = wastewater;
            failure = DomainFailure.None;
            return true;
        }

        public bool TryConsumeBatch(
            IReadOnlyList<ProcessFluidCycleDemand> demands,
            out DomainFailure failure)
        {
            BatchCallCount++;
            ExplicitCallCount += demands?.Count ?? 0;
            LastCleanWater = demands?.Sum(demand => demand.CleanWater) ?? 0f;
            LastWastewater = demands?.Sum(demand => demand.Wastewater) ?? 0f;
            failure = DomainFailure.None;
            return demands != null;
        }

        public bool TryConsumeBatch(
            IReadOnlyList<ProcessFluidCycleDemand> demands,
            string operationId,
            out IReadOnlyList<ManualWaterTransferReceipt> manualTransfers,
            out IReadOnlyList<ProcessWastewaterComponent> wastewaterComponents,
            out DomainFailure failure)
        {
            manualTransfers = Array.Empty<ManualWaterTransferReceipt>();
            wastewaterComponents = (demands ?? Array.Empty<ProcessFluidCycleDemand>())
                .SelectMany(value => value.WastewaterComponents)
                .OrderBy(value => (int)value.Composition)
                .ThenBy(value => (int)value.SourceKind)
                .ThenBy(value => value.SourceStableId, StringComparer.Ordinal)
                .ToArray();
            return TryConsumeBatch(demands, out failure);
        }

        public bool AcknowledgeManualTransfers(
            IReadOnlyList<string> operationIds,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }
    }

    private sealed class NoOpFluidInfrastructureTransaction :
        IFluidInfrastructureTransaction
    {
        public static readonly NoOpFluidInfrastructureTransaction Instance = new();

        public bool TryConsume(
            BuildableObject consumer,
            WorldWaterQuality minimumQuality,
            float amount,
            out WorldWaterQuality consumedQuality,
            out DomainFailure failure)
        {
            consumedQuality = minimumQuality;
            failure = DomainFailure.None;
            return true;
        }

        public bool CanConsume(
            BuildableObject consumer,
            WorldWaterQuality minimumQuality,
            float amount,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }

        public bool TryAdd(
            BuildableObject producer,
            WorldWaterQuality quality,
            float amount,
            out float accepted)
        {
            accepted = amount;
            return true;
        }

        public bool TryConsumeManualContainer(
            BuildableObject consumer,
            string destinationId,
            float amount,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }
    }

    private sealed class NoOpWastewaterTransaction : IFluidWastewaterTransaction
    {
        public static readonly NoOpWastewaterTransaction Instance = new();

        public bool TryAddWastewater(
            BuildableObject fixture,
            float amount,
            out float accepted,
            out DomainFailure failure)
        {
            accepted = amount;
            failure = DomainFailure.None;
            return true;
        }

        public bool TryConsumeWastewater(
            BuildableObject processor,
            float amount,
            out float consumed)
        {
            consumed = amount;
            return true;
        }

        public bool CanAcceptWastewater(
            BuildableObject fixture,
            float amount,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return true;
        }
    }

    private sealed class TestProductionCycleUtilityService :
        IProductionCycleUtilityService
    {
        private readonly IProductionWorkshopRuntime workshops;
        private readonly IPowerInfrastructureQuery power;

        public TestProductionCycleUtilityService(
            IProductionWorkshopRuntime workshops,
            IPowerInfrastructureQuery power)
        {
            this.workshops = workshops;
            this.power = power;
        }

        public bool ValidateCycleRequirements(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            IReadOnlyList<ProductionBillRecord> allBills,
            out string failureReason)
        {
            if (!workshops.HasRequiredSupports(
                    facility,
                    recipe.RequiredSupportTags,
                    out failureReason))
            {
                return false;
            }

            if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch)
            {
                return true;
            }

            return TryResolveBatchSupport(
                recipe,
                facility,
                out _,
                out failureReason);
        }

        public bool ValidateProcessingUtilities(
            string occupiedSupportNodeId,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out string failureReason)
        {
            BuildableObject support = ResolveOccupiedBatchSupport(
                occupiedSupportNodeId,
                facility);
            failureReason = support == null || power.IsPowered(support)
                ? string.Empty
                : "test-support-power-off";
            return string.IsNullOrEmpty(failureReason);
        }

        public bool TryConsumeCycleUtilities(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out ProductionProcessFluidReceipt receipt,
            out string failureReason)
        {
            float cleanWater = recipe?.CleanWaterPerCycle ?? 0f;
            ProductionManualWaterTransferSaveData[] manual = cleanWater > 0f
                ? new[]
                {
                    new ProductionManualWaterTransferSaveData
                    {
                        operationId =
                            $"production-process-fluid:{record.billId.Value}:{record.cycleSequence:D8}:manual-water:0000:{facility.RequirePersistentInstanceId().Value}",
                        physicalCommitId =
                            $"physical-batch-disposition:1:production-process-fluid:{record.billId.Value}:{record.cycleSequence:D8}:manual-water:0000:{facility.RequirePersistentInstanceId().Value}:1:500",
                        destinationId =
                            $"plumbing:process-water:{facility.RequirePersistentInstanceId().Value}:{recipe.WorkTypeId.Value}",
                        requestedWaterUnits = cleanWater,
                        transferredWaterUnits = 1,
                        inputMassGrams = 500L,
                        sourceStackIds = new List<string>
                        {
                            "item-stack:test-production-manual-water"
                        }
                    }
                }
                : Array.Empty<ProductionManualWaterTransferSaveData>();
            receipt = new ProductionProcessFluidReceipt(
                ProductionFluidMassRules.ToMassGrams(
                    cleanWater),
                ProductionFluidMassRules.ToMassGrams(
                    recipe?.WastewaterPerCycle ?? 0f),
                manual,
                recipe != null && recipe.WastewaterPerCycle > 0f
                    ? new[]
                    {
                        new ProcessWastewaterComponent(
                            recipe.WastewaterComposition,
                            ProcessWastewaterSourceKind.Recipe,
                            recipe.RecipeId,
                            recipe.WastewaterPerCycle)
                    }
                    : Array.Empty<ProcessWastewaterComponent>());
            failureReason = string.Empty;
            return true;
        }

        public bool AcknowledgeCycleUtilities(
            ProductionProcessFluidReceipt receipt,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryResolveBatchSupport(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            BuildableObject facility,
            IReadOnlyList<ProductionBillRecord> allBills,
            out string supportNodeId,
            out string failureReason)
        {
            supportNodeId = string.Empty;
            if (!TryResolveBatchSupport(
                    recipe,
                    facility,
                    out BuildableObject support,
                    out failureReason))
            {
                return false;
            }

            supportNodeId = support.RequirePersistentInstanceId().Value;
            return true;
        }

        public float ResolveTemperatureSpeed(
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out bool dangerous)
        {
            dangerous = false;
            return 1f;
        }

        public BuildableObject ResolveOccupiedBatchSupport(
            string occupiedSupportNodeId,
            BuildableObject facility)
        {
            return workshops.GetLinks(facility)
                .Select(link => link.Support)
                .FirstOrDefault(support => string.Equals(
                    support.RequirePersistentInstanceId().Value,
                    occupiedSupportNodeId,
                    StringComparison.Ordinal));
        }

        private bool TryResolveBatchSupport(
            ProductionRecipeSO recipe,
            BuildableObject facility,
            out BuildableObject support,
            out string failureReason)
        {
            support = workshops.GetLinks(facility)
                .Select(link => link.Support)
                .FirstOrDefault(candidate =>
                {
                    BuildingProductionSupportAbility ability = candidate?
                        .BuildingData.GetProductionSupportAbility();
                    return ability != null
                        && ability.kind == ProductionSupportKind.BatchProcessor
                        && ability.Provides(recipe.BatchSupportTag);
                });
            failureReason = support == null
                ? "test-batch-support-missing"
                : string.Empty;
            return support != null;
        }
    }

    /// <summary>
    /// Editor-only exact-capability fixture for the legacy stateful output
    /// lifecycle tests. Standard definition output is intentionally excluded:
    /// production code routes it through the common prepared batch owner.
    /// </summary>
    private sealed class BufferedExactOutputTestHandler :
        IProductionOutputHandler,
        IIdempotentProductionOutputHandler
    {
        public const string Capability =
            "production-output:qa-buffered-exact";
        public const int CapabilityVersion = 1;
        public const string Codec =
            "production-output-codec:qa-buffered-exact";
        public const int CodecVersion = 1;

        private readonly IProductionOutputBufferGateway outputBuffer;
        private readonly HashSet<string> itemIds;

        public BufferedExactOutputTestHandler(
            IProductionOutputBufferGateway outputBuffer,
            params string[] itemIds)
        {
            this.outputBuffer = outputBuffer
                ?? throw new ArgumentNullException(nameof(outputBuffer));
            this.itemIds = new HashSet<string>(
                itemIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (this.itemIds.Count == 0)
            {
                throw new ArgumentException(
                    "An exact output test handler requires at least one item.",
                    nameof(itemIds));
            }
        }

        public string CapabilityId => Capability;
        public int ContractVersion => CapabilityVersion;
        public string ComponentCodecId => Codec;
        public int ComponentCodecVersion => CodecVersion;
        public bool SupportsAutomaticSelection => true;

        public bool CanHandle(string itemId) => itemIds.Contains(
            itemId ?? string.Empty);

        public bool TryProduce(
            ProductionOutputContext context,
            out string failureReason)
        {
            bool succeeded = TryProduceIdempotent(
                context,
                out DomainFailure failure);
            failureReason = succeeded
                ? string.Empty
                : failure.Code + ":" + string.Join(",", failure.Parameters.ToArray());
            return succeeded;
        }

        public bool TryProduceIdempotent(
            ProductionOutputContext context,
            out DomainFailure failure)
        {
            if (context.Facility == null
                || !CanHandle(context.ItemId)
                || string.IsNullOrEmpty(context.CommitId))
            {
                failure = new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    context.ItemId,
                    "qa-buffered-exact-context-invalid");
                return false;
            }
            return outputBuffer.TryCommitBufferedOutput(
                context.CommitId,
                context.ItemId,
                context.Amount,
                context.Facility.centerPos,
                context.OutputDestinationId,
                out failure);
        }

        public bool TryAcknowledge(
            string commitId,
            out DomainFailure failure) =>
            outputBuffer.AcknowledgeBufferedOutput(commitId, out failure);

        public bool TryCaptureCommittedOutput(
            ProductionOutputContext context,
            out ProductionCommittedOutputSnapshot snapshot,
            out DomainFailure failure)
        {
            snapshot = null;
            if (!outputBuffer.TryGetBufferedOutputCommitMassGrams(
                    context.CommitId,
                    out long massGrams,
                    out failure))
            {
                return false;
            }
            string proof = HashSnapshot("proof", context.CommitId, massGrams);
            string capacity = HashSnapshot("capacity", context.CommitId, massGrams);
            string outcome = HashSnapshot("outcome", context.CommitId, massGrams);
            string planned = HashSnapshot("planned", context.CommitId, massGrams);
            string facilityId = context.Facility.PersistentInstanceId.Value;
            snapshot = new ProductionCommittedOutputSnapshot(
                context.CommitId,
                facilityId,
                Capability,
                CapabilityVersion,
                Codec,
                CodecVersion,
                proof,
                massGrams,
                capacity,
                massGrams,
                massGrams,
                outcome,
                planned,
                context.OutputDestinationId,
                context.Facility.centerPos.x,
                context.Facility.centerPos.y,
                "qa.production",
                "qa-buffered-exact:" + context.CommitId,
                facilityId,
                1L,
                false,
                new[]
                {
                    new ProductionCommittedOutputStackSnapshot(
                        context.OutputLineId,
                        "stack:qa-buffered-exact:" + context.CommitId,
                        context.ItemId,
                        context.Amount,
                        massGrams,
                        string.Empty,
                        string.Empty)
                });
            return true;
        }

        private static string HashSnapshot(
            string kind,
            string commitId,
            long massGrams)
        {
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append("qa-buffered-exact-snapshot@1");
            digest.Append(kind);
            digest.Append(commitId);
            digest.Append(massGrams);
            return digest.ComputeSha256();
        }
    }

    private sealed class BufferedExactOutputRestoreJoinForFixture :
        IProductionExactCapabilityOutputRestoreJoin
    {
        private readonly FakeProductionItemGateway items;

        internal BufferedExactOutputRestoreJoinForFixture(
            FakeProductionItemGateway items)
        {
            this.items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public void Validate(DungeonProductionBillSaveData payload)
        {
            ProductionResolvedOutputSaveData[] pending = (payload?.bills
                    ?? new List<ProductionBillSaveData>())
                .Where(value => value != null)
                .SelectMany(value => value.resolvedOutputs
                    ?? new List<ProductionResolvedOutputSaveData>())
                .Where(value => value != null
                    && !string.IsNullOrEmpty(value.pendingCommitId))
                .ToArray();
            foreach (ProductionResolvedOutputSaveData output in pending)
            {
                if (!ProductionOutputCommitIdentity.IsOwnedCommitId(
                        output.pendingCommitId)
                    || output.pendingCommitApplied
                    || output.pendingOutputPublication == null
                    || output.pendingOutputPublication.phase
                        != ProductionExactOutputPublicationPhase.None
                    || !string.Equals(
                        output.outputCapabilityId,
                        BufferedExactOutputTestHandler.Capability,
                        StringComparison.Ordinal)
                    || !items.HasBufferedCommit(output.pendingCommitId)
                    || !items.TryGetBufferedOutputCommitMassGrams(
                        output.pendingCommitId,
                        out long physicalMassGrams,
                        out _)
                    || physicalMassGrams <= 0L)
                {
                    throw new InvalidOperationException(
                        "QA buffered exact-output restore join rejected commit: "
                        + output.pendingCommitId);
                }
            }
        }
    }

    private sealed class FakeProductionItemGateway :
        IProductionItemGateway,
        IProductionOutputBufferGateway,
        IProductionStockSensorPhysicalGateway,
        IProductionStockSensorRemovalOutputGateway
    {
        private readonly Dictionary<string, int> requested =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> cumulativeRequested =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> delivered =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> available =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> buffered =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> definitionMassByItem =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionStockSensorPhysicalReceipt>
            pendingStockSensors = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ProductionStockSensorRemovalReceipt>
            publishedStockSensorRemovals = new(StringComparer.Ordinal);
        private int bufferedOutputSuccessesBeforeFailure = -1;
        private bool throwAfterBufferedCommitOnce;
        private bool failStockSensorAcknowledgeOnce;
        private bool failStockSensorRemovalOnce;
        public int StockSensorRemovalPublicationCount { get; private set; }

        public int CountDelivered(string itemId, string destinationId) =>
            Get(delivered, Key(itemId, destinationId));

        public bool TryGetStockCategory(
            string itemId,
            out StockCategory category)
        {
            category = StockCategory.General;
            return !string.IsNullOrWhiteSpace(itemId)
                && string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
                && definitionMassByItem.ContainsKey(itemId);
        }

        public int CountPending(string itemId, string destinationId) =>
            Get(requested, Key(itemId, destinationId))
            + Get(delivered, Key(itemId, destinationId));

        public long CountPendingMassGrams(string destinationId)
        {
            string suffix = "|" + destinationId;
            return requested
                .Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Sum(pair => (long)pair.Value * GetUnitMassFromKey(pair.Key))
                + delivered
                    .Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                    .Sum(pair => (long)pair.Value * GetUnitMassFromKey(pair.Key));
        }

        public long GetDefinitionQuantityMassGrams(
            string itemId,
            int quantity) => checked((long)quantity * GetUnitMass(itemId));

        public void SetDefinitionMass(string itemId, long unitMassGrams)
        {
            Require(
                !string.IsNullOrWhiteSpace(itemId)
                && string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
                && unitMassGrams > 0L,
                "fake production item mass authority is invalid");
            definitionMassByItem[itemId] = unitMassGrams;
        }

        public void SetInvalidDefinitionMassForFailureTest(
            string itemId,
            long unitMassGrams)
        {
            Require(
                !string.IsNullOrWhiteSpace(itemId)
                && string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
                && unitMassGrams == 0L,
                "invalid production item mass fixture must be canonical zero");
            definitionMassByItem[itemId] = unitMassGrams;
        }

        private long GetUnitMassFromKey(string key)
        {
            int separator = key.IndexOf('|');
            return GetUnitMass(separator >= 0 ? key.Substring(0, separator) : key);
        }

        private long GetUnitMass(string itemId) =>
            definitionMassByItem.TryGetValue(
                itemId ?? string.Empty,
                out long value)
                ? value
                : 1_000L;

        public int CountAvailableStock(
            string itemId,
            string excludedDestinationId) =>
            Get(available, itemId);

        public bool RequestDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requestedAmount,
            out string failureReason)
        {
            requestedAmount = Mathf.Max(0, amount);
            failureReason = string.Empty;
            string key = Key(itemId, destinationId);
            Add(requested, key, requestedAmount);
            Add(cumulativeRequested, key, requestedAmount);
            return requestedAmount > 0;
        }

        public bool RequestDeliveryWithinMassCapacity(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            long maxDestinationMassGrams,
            out int requestedAmount,
            out string failureReason)
        {
            long requestedMass = GetDefinitionQuantityMassGrams(itemId, amount);
            if (CountPendingMassGrams(destinationId) + requestedMass
                > maxDestinationMassGrams)
            {
                requestedAmount = 0;
                failureReason =
                    "production-input-buffer-mass-capacity-unavailable";
                return false;
            }
            return RequestDelivery(
                itemId,
                amount,
                destinationPosition,
                destinationId,
                out requestedAmount,
                out failureReason);
        }

        private bool ConsumeDeliveredForFixture(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            out string failureReason)
        {
            failureReason = string.Empty;
            foreach (KeyValuePair<string, int> cost in costs)
            {
                if (CountDelivered(cost.Key, destinationId) < cost.Value)
                {
                    failureReason = $"missing {cost.Key}";
                    return false;
                }
            }

            foreach (KeyValuePair<string, int> cost in costs)
            {
                Add(delivered, Key(cost.Key, destinationId), -cost.Value);
            }
            return true;
        }

        public bool ConsumeDeliveredToWip(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            string operationId,
            out ProductionWipInputReceipt receipt,
            out string failureReason)
        {
            if (!ConsumeDeliveredForFixture(destinationId, costs, out failureReason))
            {
                receipt = default;
                return false;
            }
            int quantity = costs.Values.Sum();
            receipt = new ProductionWipInputReceipt(
                "test-wip:" + operationId,
                quantity,
                quantity * 1000L);
            return true;
        }

        public bool AcknowledgeWipInput(
            string commitId,
            out string failureReason)
        {
            failureReason = string.Empty;
            return !string.IsNullOrWhiteSpace(commitId);
        }

        public bool CommitPending(
            string destinationId,
            string itemId,
            string operationId,
            string reasonCode,
            out ProductionStockSensorPhysicalReceipt receipt,
            out string failureReason)
        {
            if (pendingStockSensors.TryGetValue(operationId, out receipt))
            {
                failureReason = string.Empty;
                return true;
            }
            if (!ConsumeDeliveredForFixture(
                    destinationId,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [itemId] = 1
                    },
                    out failureReason))
            {
                receipt = default;
                return false;
            }
            const long mass = 1000L;
            string sourceId = "stack:stock-sensor-fixture:" + operationId;
            string fingerprint =
                $"{(int)PhysicalItemDispositionKind.Sink}:{reasonCode}:{sourceId}=1";
            receipt = new ProductionStockSensorPhysicalReceipt(
                operationId,
                reasonCode,
                fingerprint,
                $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Sink}:{operationId}:1:{mass}",
                1,
                mass,
                new[] { sourceId });
            pendingStockSensors.Add(operationId, receipt);
            return true;
        }

        public bool TryGetPending(
            string operationId,
            out ProductionStockSensorPhysicalReceipt receipt) =>
            pendingStockSensors.TryGetValue(operationId, out receipt);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            string operationId = pendingStockSensors
                .Where(pair => string.Equals(
                    pair.Value.CommitId,
                    commitId,
                    StringComparison.Ordinal))
                .Select(pair => pair.Key)
                .FirstOrDefault();
            if (operationId != null && failStockSensorAcknowledgeOnce)
            {
                failStockSensorAcknowledgeOnce = false;
                failureReason = "fixture stock-sensor acknowledgement fault";
                return false;
            }
            failureReason = operationId == null
                ? "fixture stock-sensor receipt missing"
                : string.Empty;
            return operationId != null && pendingStockSensors.Remove(operationId);
        }

        public void FailStockSensorAcknowledgeOnce() =>
            failStockSensorAcknowledgeOnce = true;

        public bool TryEnsureRemovalOutput(
            string itemId,
            Vector2Int outputPosition,
            string operationId,
            string reasonCode,
            out ProductionStockSensorRemovalReceipt receipt,
            out string failureReason)
        {
            if (failStockSensorRemovalOnce)
            {
                failStockSensorRemovalOnce = false;
                receipt = default;
                failureReason =
                    "fixture stock-sensor removal output-space fault";
                return false;
            }
            if (publishedStockSensorRemovals.TryGetValue(
                    operationId,
                    out receipt))
            {
                failureReason = string.Empty;
                return true;
            }
            const long mass = 1000L;
            string commitId =
                $"physical-source:{operationId}:{itemId}:1:{mass}";
            receipt = new ProductionStockSensorRemovalReceipt(
                operationId,
                reasonCode,
                new[] { commitId },
                1,
                mass);
            publishedStockSensorRemovals.Add(operationId, receipt);
            Add(available, itemId, 1);
            StockSensorRemovalPublicationCount++;
            failureReason = string.Empty;
            return true;
        }

        public void FailStockSensorRemovalOnce() =>
            failStockSensorRemovalOnce = true;

        public bool SpawnOutput(
            string itemId,
            int amount,
            Vector2Int position)
        {
            Add(available, itemId, amount);
            return true;
        }

        public bool CanSpawnOutput(
            string itemId,
            int amount,
            Vector2Int position,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            return amount > 0;
        }

        public int CountBufferedOutput(string itemId)
        {
            return buffered
                .Where(pair => pair.Key.StartsWith(
                    itemId + "|",
                    StringComparison.Ordinal))
                .Sum(pair => pair.Value);
        }

        public int CountBufferedOutput(string itemId, string destinationId) =>
            Get(buffered, Key(itemId, destinationId));

        public bool TryCommitBufferedOutput(
            string commitId,
            string itemId,
            int amount,
            Vector2Int position,
            string destinationId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            string key = Key(itemId, destinationId);
            string commitKey = "commit|" + commitId;
            if (Get(buffered, commitKey) > 0)
            {
                return Get(buffered, commitKey) == amount;
            }
            if (bufferedOutputSuccessesBeforeFailure == 0)
            {
                bufferedOutputSuccessesBeforeFailure = -1;
                failure = new DomainFailure(FailureCode.ProductionOutputUnavailable);
                return false;
            }
            if (bufferedOutputSuccessesBeforeFailure > 0)
            {
                bufferedOutputSuccessesBeforeFailure--;
            }
            Add(buffered, key, amount);
            Add(buffered, commitKey, amount);
            Add(available, itemId, amount);
            if (throwAfterBufferedCommitOnce)
            {
                throwAfterBufferedCommitOnce = false;
                throw new InvalidOperationException(
                    "injected-output-post-commit");
            }
            return true;
        }

        public bool AcknowledgeBufferedOutput(
            string commitId,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            buffered.Remove("commit|" + commitId);
            return true;
        }

        public bool TryGetBufferedOutputCommitMassGrams(
            string commitId,
            out long massGrams,
            out DomainFailure failure)
        {
            failure = DomainFailure.None;
            int quantity = Get(buffered, "commit|" + commitId);
            massGrams = quantity * 1000L;
            if (massGrams > 0L)
            {
                return true;
            }
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                commitId,
                "commit-mass-missing");
            return false;
        }

        public bool SpawnBufferedOutput(
            string itemId,
            int amount,
            Vector2Int position,
            string destinationId)
        {
            if (bufferedOutputSuccessesBeforeFailure == 0)
            {
                bufferedOutputSuccessesBeforeFailure = -1;
                return false;
            }
            if (bufferedOutputSuccessesBeforeFailure > 0)
            {
                bufferedOutputSuccessesBeforeFailure--;
            }
            Add(buffered, Key(itemId, destinationId), amount);
            Add(available, itemId, amount);
            return amount > 0;
        }

        public void FailBufferedOutputAfterSuccesses(int successfulUnits) =>
            bufferedOutputSuccessesBeforeFailure = Math.Max(0, successfulUnits);

        public void ThrowAfterBufferedCommitOnce() =>
            throwAfterBufferedCommitOnce = true;

        public bool HasBufferedCommit(string commitId) =>
            Get(buffered, "commit|" + commitId) > 0;

        public int ReleaseBufferedOutput(
            string destinationId,
            Vector2Int releasePosition)
        {
            int released = 0;
            foreach (string key in buffered.Keys
                         .Where(key => key.EndsWith(
                             "|" + destinationId,
                             StringComparison.Ordinal))
                         .ToArray())
            {
                released += buffered[key];
                buffered.Remove(key);
            }
            return released;
        }

        public bool TryRouteBufferedOutput(
            string sourceDestinationId,
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int routed,
            out DomainFailure failure)
        {
            string sourceKey = Key(itemId, sourceDestinationId);
            routed = Mathf.Min(Mathf.Max(0, amount), Get(buffered, sourceKey));
            failure = DomainFailure.None;
            Add(buffered, sourceKey, -routed);
            Add(buffered, Key(itemId, destinationId), routed);
            return routed == amount;
        }

        public void PrioritizeDestination(string destinationId)
        {
        }

        public int ReleaseDestination(
            string destinationId,
            Vector2Int releasePosition) => 0;

        public bool TryReleaseDestinationAtomically(
            string destinationId,
            Vector2Int releasePosition,
            out int released,
            out string failureReason)
        {
            released = ReleaseDestination(destinationId, releasePosition);
            failureReason = string.Empty;
            return true;
        }

        public int RemoveDestination(string destinationId) => 0;

        public void Deliver(string itemId, int amount, string destinationId)
        {
            string key = Key(itemId, destinationId);
            int moved = Mathf.Min(amount, Get(requested, key));
            Add(requested, key, -moved);
            Add(delivered, key, moved);
        }

        public int GetRequested(string itemId)
        {
            return cumulativeRequested
                .Where(pair => pair.Key.StartsWith(
                    itemId + "|",
                    StringComparison.Ordinal))
                .Sum(pair => pair.Value);
        }

        public int GetDelivered(string itemId)
        {
            return delivered
                .Where(pair => pair.Key.StartsWith(
                    itemId + "|",
                    StringComparison.Ordinal))
                .Sum(pair => pair.Value);
        }

        public int GetAvailable(string itemId) => Get(available, itemId);

        private static string Key(string itemId, string destinationId) =>
            $"{itemId}|{destinationId}";

        private static int Get(
            IReadOnlyDictionary<string, int> values,
            string key) =>
            values.TryGetValue(key, out int value) ? value : 0;

        private static void Add(
            IDictionary<string, int> values,
            string key,
            int amount)
        {
            values[key] = Mathf.Max(
                0,
                (values.TryGetValue(key, out int current) ? current : 0)
                + amount);
        }
    }

    private sealed class FakeProductionWorkshop :
        IProductionWorkshopRuntime
    {
        private readonly BuildableObject workstation;
        private readonly BuildableObject support;
        private readonly BuildingProductionSupportAbility ability;

        public FakeProductionWorkshop(
            BuildableObject workstation,
            BuildableObject support)
        {
            this.workstation = workstation;
            this.support = support;
            ability = support.BuildingData.GetProductionSupportAbility();
        }

        public int Version => 1;

        public IReadOnlyList<ProductionSupportLinkSnapshot> GetLinks(
            BuildableObject candidate)
        {
            return candidate == workstation
                ? new[]
                {
                    new ProductionSupportLinkSnapshot
                    {
                        Workstation = workstation,
                        Support = support,
                        WorkstationTag =
                            workstation.GetProductionWorkstationTag(),
                        SupportId = ability.SupportId,
                        FeatureTags = ability.featureTags
                    }
                }
                : Array.Empty<ProductionSupportLinkSnapshot>();
        }

        public bool TryGetLinkForSupport(
            BuildableObject candidate,
            out ProductionSupportLinkSnapshot link)
        {
            link = candidate == support
                ? GetLinks(workstation).Single()
                : null;
            return link != null;
        }

        public bool HasRequiredSupports(
            BuildableObject candidate,
            IReadOnlyList<string> requiredFeatureTags,
            out string failureReason)
        {
            failureReason = string.Empty;
            bool valid = candidate == workstation
                && (requiredFeatureTags ?? Array.Empty<string>())
                    .All(ability.Provides);
            if (!valid)
            {
                failureReason = "missing fake support";
            }
            return valid;
        }

        public bool TryResolveSupport(
            BuildableObject candidate,
            string featureTag,
            ProductionSupportKind? requiredKind,
            out BuildableObject resolvedSupport,
            out BuildingProductionSupportAbility resolvedAbility)
        {
            bool valid = candidate == workstation
                && ability.Provides(featureTag)
                && (!requiredKind.HasValue
                    || ability.kind == requiredKind.Value);
            resolvedSupport = valid ? support : null;
            resolvedAbility = valid ? ability : null;
            return valid;
        }
    }

    private sealed class FixedBuildingWorldQuery : IBuildingWorldQuery
    {
        public FixedBuildingWorldQuery(params BuildableObject[] buildings)
        {
            Replace(buildings);
        }

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; private set; }

        public void Replace(params BuildableObject[] buildings)
        {
            Buildings = buildings ?? Array.Empty<BuildableObject>();
        }
    }

    private sealed class FixedRestoreWorldCandidateQuery :
        IRestoreWorldCandidateQuery
    {
        private readonly IReadOnlyList<BuildableObject> buildings;

        internal FixedRestoreWorldCandidateQuery(
            params BuildableObject[] buildings)
        {
            this.buildings = (buildings ?? Array.Empty<BuildableObject>())
                .Where(value => value != null)
                .ToArray();
        }

        public int Revision => 1;

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }

        public bool TryGetBuildings(
            out IReadOnlyList<BuildableObject> candidateBuildings)
        {
            candidateBuildings = buildings;
            return true;
        }

        public bool TryGetCharacters(
            out IReadOnlyList<CharacterActor> characters)
        {
            characters = null;
            return false;
        }

        public bool TryGetWildlife(
            out IReadOnlyList<WildlifeActor> wildlife)
        {
            wildlife = null;
            return false;
        }

        public bool TryGetExteriorZones(
            out IReadOnlyList<ExteriorZoneMarker> zones)
        {
            zones = null;
            return false;
        }
    }

    private sealed class EmptyWarehouseWorldQuery : IWarehouseWorldQuery
    {
        public static readonly EmptyWarehouseWorldQuery Instance = new();

        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses { get; } =
            Array.Empty<IWarehouseFacility>();
    }

    private sealed class MutableGameClock : IGameClock
    {
        public float DeltaTimeValue { get; set; }
        public bool Paused { get; set; }

        public float DeltaTime => DeltaTimeValue;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => Paused;
    }

    private sealed class MutablePowerRuntime : IPowerInfrastructureQuery
    {
        public bool Powered { get; set; } = true;

        public int Version => 1;
        public IReadOnlyList<PowerNetworkSnapshot> Networks { get; } =
            Array.Empty<PowerNetworkSnapshot>();

        public bool IsPowered(BuildableObject building) => Powered;

        public bool TryGetNode(
            BuildableObject building,
            out PowerNodeSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public DungeonPowerInfrastructureSaveData Capture() =>
            new DungeonPowerInfrastructureSaveData();

        public void Restore(DungeonPowerInfrastructureSaveData snapshot)
        {
        }
    }

    private sealed class EmptyBuildingWorldQuery : IBuildingWorldQuery
    {
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings { get; } =
            Array.Empty<BuildableObject>();
    }

    private sealed class FixedDropZoneQuery : IWorldDropZoneQuery
    {
        private readonly Vector2Int position;

        public FixedDropZoneQuery(Vector2Int position)
        {
            this.position = position;
        }

        public bool TryGetDeliveryDropoff(out Vector2Int result)
        {
            result = position;
            return true;
        }

        public bool TryGetExpeditionLootDropoff(out Vector2Int result)
        {
            result = position;
            return true;
        }

        public bool TryGetVisitorEntryPoint(
            out WorldGridEntryPoint entryPoint)
        {
            entryPoint = default;
            return false;
        }
    }

    private sealed class FixedGameClock : IGameClock
    {
        public float DeltaTime => 0.02f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class RequiredDependencyStubSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public RequiredDependencyStubSection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase)
        {
            SectionId = sectionId
                ?? throw new ArgumentNullException(nameof(sectionId));
            RestorePhase = restorePhase;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError(
                    $"Invalid prerequisite payload for '{SectionId}'.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private sealed class FinalFailingSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;

        public FinalFailingSection(IReadOnlyList<string> dependencies)
        {
            this.dependencies = dependencies
                ?? throw new ArgumentNullException(nameof(dependencies));
        }

        public bool WasCommitted { get; private set; }
        public string SectionId => "economy.debug.late-failure";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Presentation;
        public IReadOnlyList<string> DependsOn => dependencies;
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError("Injected economy final payload is invalid.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                commitReport =>
                {
                    WasCommitted = true;
                    commitReport.AddError(
                        "Injected economy final-section failure.");
                });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private sealed class FakeResourceStockPolicyRuntime :
        IResourceStockPolicyRuntime
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private DungeonResourceStockPolicySaveData localState;

        public FakeResourceStockPolicyRuntime(
            IResourceEconomyContentCatalog catalog,
            DungeonRuntimeAggregateRootStore store = null)
        {
            this.store = store;
            State = new DungeonResourceStockPolicySaveData
            {
                nextSaleSequence = 1,
                policies = catalog.Items
                    .Where(item => item != null)
                    .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                    .Select(item => new ResourceStockPolicyData
                    {
                        itemId = item.ItemId,
                        minimumStock = 10,
                        targetStock = 20,
                        maximumStock = 40,
                        surplusDisposition = StockSurplusDisposition.Hold,
                        lastStatus = string.Empty
                    })
                    .ToList(),
                pendingSales = new List<ResourceStockPolicyPendingSale>()
            };
        }

        private DungeonResourceStockPolicySaveData State
        {
            get => store != null
                ? store.GetOrCreate(
                    () => new DungeonResourceStockPolicySaveData())
                : localState;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localState = value;
                }
            }
        }

        public int RestoreCount { get; private set; }
        public int Version => RestoreCount;
        public IReadOnlyList<ResourceStockPolicyData> Policies =>
            State.policies;

        public ResourceStockPolicyData GetOrCreate(string itemId) =>
            State.policies.Single(policy => string.Equals(
                policy.itemId,
                itemId,
                StringComparison.Ordinal)).Clone();

        public bool SetPolicy(
            ResourceStockPolicyData policy,
            out string failureReason)
        {
            failureReason = "Fixture runtime is read-only.";
            return false;
        }

        public int CountOwned(string itemId) => 0;

        public DungeonResourceStockPolicySaveData Capture() =>
            Clone(State);

        public ResourceStockPolicyRestoreCandidate PrepareRestoreCandidate(
            DungeonResourceStockPolicySaveData saveData)
        {
            DungeonResourceStockPolicySaveData payload = Clone(saveData);
            ResourceStockPolicyAggregateState candidateState = new();
            candidateState.NextSaleSequence = payload.nextSaleSequence;
            foreach (ResourceStockPolicyData policy in payload.policies)
            {
                candidateState.ByItemId.Add(policy.itemId, policy.Clone());
            }
            foreach (ResourceStockPolicyPendingSale pending in
                     payload.pendingSales)
            {
                candidateState.PendingSalesByItemId.Add(
                    pending.itemId,
                    pending.Clone());
            }
            candidateState.PolicyView = candidateState.ByItemId.Values
                .OrderBy(policy => policy.itemId, StringComparer.Ordinal)
                .ToArray();
            return new ResourceStockPolicyRestoreCandidate(
                candidateState,
                payload);
        }

        public void PublishRestoreCandidate(
            ResourceStockPolicyRestoreCandidate candidate)
        {
            State = candidate.Payload;
            if (store == null)
            {
                RestoreCount++;
            }
        }

        private static DungeonResourceStockPolicySaveData Clone(
            DungeonResourceStockPolicySaveData source) =>
            JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                JsonUtility.ToJson(source));
    }

    private sealed class FakeRegionalSupplyContractRuntime :
        IRegionalSupplyContractRuntime
    {
        private readonly DungeonRuntimeAggregateRootStore store;
        private DungeonRegionalSupplyContractSaveData localState;

        public FakeRegionalSupplyContractRuntime(
            DungeonRegionalSupplyContractSaveData initialState,
            DungeonRuntimeAggregateRootStore store = null)
        {
            this.store = store;
            State = Clone(initialState);
        }

        private DungeonRegionalSupplyContractSaveData State
        {
            get => store != null
                ? store.GetOrCreate(
                    () => new DungeonRegionalSupplyContractSaveData())
                : localState;
            set
            {
                if (store != null)
                {
                    store.Replace(value);
                }
                else
                {
                    localState = value;
                }
            }
        }

        public int RestoreCount { get; private set; }
        public int Version => RestoreCount;
        public bool IsUnlocked => true;
        public IReadOnlyList<RegionalSupplyContractState> Contracts =>
            State.contracts;

        public bool Accept(string contractId, out string message)
        {
            message = "Fixture runtime is read-only.";
            return false;
        }

        public bool Decline(string contractId, out string message)
        {
            message = "Fixture runtime is read-only.";
            return false;
        }

        public DungeonRegionalSupplyContractSaveData Capture() =>
            Clone(State);

        public RegionalSupplyContractRestoreCandidate PrepareRestoreCandidate(
            DungeonRegionalSupplyContractSaveData saveData) =>
            new RegionalSupplyContractRestoreCandidate(Clone(saveData));

        public void PublishRestoreCandidate(
            RegionalSupplyContractRestoreCandidate candidate)
        {
            State = candidate.Payload;
            if (store == null)
            {
                RestoreCount++;
            }
        }

        private static DungeonRegionalSupplyContractSaveData Clone(
            DungeonRegionalSupplyContractSaveData source) =>
            JsonUtility.FromJson<DungeonRegionalSupplyContractSaveData>(
                JsonUtility.ToJson(source));
    }

    private sealed class GrandProjectPhysicalFixturePort :
        IGrandProjectWorldPort,
        IGrandProjectOperationsPort
    {
        private readonly bool failFirstAcknowledge;
        private GrandProjectPhysicalInputReceipt pending;

        public GrandProjectPhysicalFixturePort(
            bool failFirstAcknowledge,
            GrandProjectPhysicalInputReceipt restoredReceipt = default)
        {
            this.failFirstAcknowledge = failFirstAcknowledge;
            pending = restoredReceipt;
            Office = new GrandProjectOfficeSnapshot(
                new BuildingInstanceId("building:grand-project-fixture"),
                new Vector2Int(3, 2));
        }

        public GrandProjectOfficeSnapshot Office { get; }
        public int CommitCount { get; private set; }
        public int AcknowledgeCount { get; private set; }
        public GrandProjectPhysicalInputReceipt PendingReceipt => pending;
        public GrandProjectOfficeSnapshot FindOffice() => Office;
        public bool IsResearchCompleted(string researchId) => true;
        public Vector2Int ResolveReleasePosition() => Office.Position;
        public int CountPending(string itemId, string destinationId) => 10_000;
        public int CountDelivered(string itemId, string destinationId) => 10_000;

        public bool RequestDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested)
        {
            requested = 0;
            return true;
        }

        public bool CommitDeliveredMaterialsPending(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            string operationId,
            string reasonCode,
            out GrandProjectPhysicalInputReceipt receipt,
            out string failureReason)
        {
            if (pending.IsCommitted)
            {
                receipt = pending;
                failureReason = string.Equals(
                    pending.OperationId,
                    operationId,
                    StringComparison.Ordinal)
                    ? string.Empty
                    : "fixture operation conflict";
                return failureReason.Length == 0;
            }
            int quantity = costs?.Values.Sum() ?? 0;
            string sourceId = "stack:grand-project-fixture";
            long mass = checked(quantity * 1000L);
            string fingerprint =
                $"{(int)PhysicalItemDispositionKind.Sink}:{reasonCode}:{sourceId}={quantity}";
            pending = new GrandProjectPhysicalInputReceipt(
                operationId,
                reasonCode,
                fingerprint,
                $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Sink}:{operationId}:{quantity}:{mass}",
                quantity,
                mass,
                new[] { sourceId });
            CommitCount++;
            receipt = pending;
            failureReason = string.Empty;
            return receipt.IsCommitted;
        }

        public bool TryGetPendingMaterials(
            string operationId,
            out GrandProjectPhysicalInputReceipt receipt)
        {
            receipt = pending;
            return pending.IsCommitted
                && string.Equals(
                    pending.OperationId,
                    operationId,
                    StringComparison.Ordinal);
        }

        public bool AcknowledgeMaterials(
            string commitId,
            out string failureReason)
        {
            AcknowledgeCount++;
            if (!pending.IsCommitted
                || !string.Equals(pending.CommitId, commitId, StringComparison.Ordinal))
            {
                failureReason = "fixture pending receipt missing";
                return false;
            }
            if (failFirstAcknowledge && AcknowledgeCount == 1)
            {
                failureReason = "fixture acknowledgement fault";
                return false;
            }
            pending = default;
            failureReason = string.Empty;
            return true;
        }

        public PhysicalItemRestoreCandidateDispositionSnapshot CaptureRestoreReceipt() =>
            new(
                PhysicalItemDispositionKind.Sink,
                pending.OperationId,
                pending.ReasonCode,
                pending.RequestFingerprint,
                pending.SourceStackIds,
                pending.InputQuantity,
                pending.InputMassGrams,
                pending.CommitId);

        public int ReleaseDestination(string destinationId, Vector2Int releasePosition) => 0;
        public void PrioritizeDestination(string destinationId) { }
        public void RequestGrandProjectWorker() { }
        public void RequestHauler() { }
        public void MarkDynamicStateDirty() { }
    }

    private sealed class SinglePhysicalItemRestoreCandidateQuery :
        IPhysicalItemRestoreCandidateQuery
    {
        private readonly PhysicalItemRestoreCandidateDispositionSnapshot value;

        public SinglePhysicalItemRestoreCandidateQuery(
            PhysicalItemRestoreCandidateDispositionSnapshot value)
        {
            this.value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => new[] { value };
        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = value;
            return string.Equals(value.OperationId, operationId, StringComparison.Ordinal);
        }
    }

    private sealed class SingleStockSensorLifecycleQuery :
        IProductionOutputDestinationLifecycleQuery
    {
        private readonly ProductionStockSensorLifecycleContributor contributor;

        internal SingleStockSensorLifecycleQuery(
            ProductionStockSensorLifecycleContributor contributor)
        {
            this.contributor = contributor
                ?? throw new ArgumentNullException(nameof(contributor));
        }

        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId facilityId)
        {
            ProductionOutputDestinationId destination =
                ProductionOutputDestinationId.FromFacility(facilityId);
            ProductionOutputDestinationLifecycleContribution value =
                contributor.Capture(facilityId, destination);
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                destination,
                new[] { value },
                ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                    "qa:stock-sensor-lifecycle:live:"
                    + value.SemanticFingerprint),
                ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                    "qa:stock-sensor-lifecycle:durable:"
                    + value.DurableSemanticFingerprint));
        }
    }

    private sealed class SinglePhysicalItemRestoreCandidateOutputQuery :
        IPhysicalItemRestoreCandidateOutputQuery
    {
        private readonly PhysicalItemRestoreCandidateOutputSnapshot value;

        public SinglePhysicalItemRestoreCandidateOutputQuery(
            PhysicalItemRestoreCandidateOutputSnapshot value)
        {
            this.value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>
            CommittedOutputs => new[] { value };

        public bool TryGetCommittedOutput(
            string commitId,
            out IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> outputs)
        {
            if (string.Equals(
                    commitId,
                    value.CommitId,
                    StringComparison.Ordinal))
            {
                outputs = new[] { value };
                return true;
            }
            outputs = Array.Empty<
                PhysicalItemRestoreCandidateOutputSnapshot>();
            return false;
        }
    }

    private sealed class UnavailableGrandProjectPhysicalSink :
        IPhysicalFacilityItemBatchSinkGateway
    {
        public static readonly UnavailableGrandProjectPhysicalSink Instance = new();

        public bool TryCommitSinkPending(
            string destinationId,
            IReadOnlyDictionary<string, int> itemQuantities,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = "fixture grand-project physical sink unavailable";
            return false;
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        {
            receipt = default;
            return false;
        }

        public bool Acknowledge(string commitId, out string failureReason)
        {
            failureReason = "fixture grand-project physical sink unavailable";
            return false;
        }
    }

    private sealed class EmptyPhysicalItemRestoreCandidateQuery :
        IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly EmptyPhysicalItemRestoreCandidateQuery Instance =
            new();

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions =>
                Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = null;
            return false;
        }
    }
}
#endif
