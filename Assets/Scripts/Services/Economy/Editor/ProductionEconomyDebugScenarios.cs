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
        ValidateSawmillPreparedOutputRealAdapter();
        ValidateSubstanceSingleAuthority();
        ValidatePhysicalStockSensorInstallation();
        ValidateProductionInputBufferMassAdmission();
        ValidateProductionInputClaimIdempotentRevoke();
        ValidateTerminalPreparedOutputBillRetirement();
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

    private static void ValidateCanonicalProductionOutputLines()
    {
        (string AssetPath, string OutputLineId)[] migratedPreparedOutputAssets =
        {
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_charcoal.asset",
                "output:recipe:charcoal/000/main/material:charcoal"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_dog_food.asset",
                "output:main"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_dog_food_fresh.asset",
                "output:main"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_hay_feed.asset",
                "output:main"),
            (
                "Assets/Resources/SO/Economy/Recipes/Workshop/recipe_malt.asset",
                "output:recipe:malt/000/main/material:malt"),
            (
                "Assets/Resources/SO/Economy/Recipes/recipe_milling_flour.asset",
                "output:recipe:milling-flour/000/main/material:flour"),
            (
                "Assets/Resources/SO/Economy/Recipes/Workshop/recipe_silage.asset",
                "output:main"),
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

    private static void ValidateSawmillPreparedOutputRealAdapter()
    {
        const string recipeId = "recipe:sawmill-lumber";
        const string lumberId = "material:lumber";
        const string facilityId = "building:qa:sawmill-prepared-output";
        const string workerId = "character:qa:sawmill-prepared-output";
        const string billId = "production-bill:1";
        const long expectedUnitMassGrams = 1_200L;
        const long expectedBatchMassGrams = 3_600L;
        const long expectedCapacityGrams = 14_400L;

        ResourceEconomyContentCatalog catalog = LoadCatalog();
        Require(
            catalog.TryGetRecipe(recipeId, out ProductionRecipeSO recipe),
            "sawmill prepared-output recipe is missing");
        Require(
            catalog.TryGetItem(lumberId, out ResourceItemDefinitionSO lumber),
            "sawmill prepared-output lumber definition is missing");
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/P03_제재소.asset");
        Require(building != null, "P03 sawmill definition is missing");

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
                    Array.Empty<IProductionOutputHandler>(),
                    narrativeQualification: null,
                    performance:
                        CharacterAiEditorTestDependencies.NeutralPerformance);
            ProductionFacilityHandle facilityHandle =
                bridge.CaptureFacility(facility);
            ProductionWorkerHandle workerHandle = bridge.CaptureWorker(worker);
            Require(
                string.Equals(
                    facilityHandle.DefinitionId,
                    "building:1075",
                    StringComparison.Ordinal)
                && string.Equals(
                    facilityHandle.WorkstationTag,
                    "workstation:sawmill",
                    StringComparison.Ordinal)
                && facilityHandle.OutputBufferCycleCapacity == 4,
                "P03 sawmill semantic capacity authority is invalid");

            IProductionOutputPlanningService outputPlanning =
                new ProductionOutputPlanningService(catalog, bridge);
            ProductionPreparedOutputComponentCodec componentCodec = new();
            IDungeonItemCatalogProvider itemCatalog =
                EditorItemCatalogFactory.Create();
            IPhysicalItemMassQuery massQuery =
                new PhysicalItemMassQuery(itemCatalog);
            Require(
                massQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)lumberId).Value ==
                expectedUnitMassGrams,
                "lumber physical mass authority is not 1,200g");

            ProductionMaximumOutputFactorCatalog maximumFactors = new(
                LoadAll<BuildingSO>("Assets/Resources/SO/Building"));
            ProductionOutputBufferCapacityProjector capacityProjector = new(
                catalog,
                bridge,
                maximumFactors,
                componentCodec,
                massQuery);
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
                componentCodec,
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
            long inputMassGrams = massQuery.GetQuantityMass(
                (ItemDefinitionId)"resource:log",
                PhysicalItemMassSubject.ForDefinition(
                    (ItemDefinitionId)"resource:log"),
                2).Value;
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
                $"sawmill prepared-output execution failed: {result.Failure}");

            ProductionPreparedOutputBatchSaveData completed =
                record.preparedOutput;
            Require(
                completed.schemaVersion ==
                    ProductionPreparedOutputBatchSaveData.CurrentSchemaVersion
                && completed.schemaVersion == 3
                && completed.totalPhysicalMassGrams ==
                    expectedBatchMassGrams
                && completed.outputBufferCycleCapacity == 4
                && completed.projectedPortfolioCapacityGrams ==
                    expectedCapacityGrams
                && completed.requiredMinimumCapacityGrams ==
                    expectedCapacityGrams,
                "sawmill prepared-output v3 mass/capacity contract drifted");
            ProductionPreparedOutputLineSaveData line =
                completed.lines.Single();
            ProductionPreparedOutputComponentProjection decoded =
                componentCodec.ValidateAndDecode(
                    lumber,
                    line.componentPayload,
                    line.componentFingerprint);
            Require(
                string.Equals(line.itemId, lumberId, StringComparison.Ordinal)
                && line.quantity == 3
                && line.exactMassGrams == expectedBatchMassGrams
                && decoded.RuntimeComponents.Count == 0
                && massQuery.GetQuantityMass(
                        (ItemDefinitionId)lumberId,
                        decoded.MassSubject,
                        line.quantity).Value == expectedBatchMassGrams,
                "sawmill component projection did not preserve exact lumber mass");

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
                "sawmill output buffer did not commit exact physical capacity");
            FacilityBufferPlannedOutputPublicationEditorSnapshot physical =
                publication.CaptureEditorTestSnapshot();
            Require(
                physical.Stacks.Count == 1
                && physical.Stacks[0].Quantity == 3
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
                "sawmill output publication did not leave one durable physical stack");
            ProductionPreparedOutputRoutingLineSnapshot routed =
                routing.CaptureBill(record.billId).Single();
            Require(
                string.Equals(routed.ItemId, lumberId, StringComparison.Ordinal)
                && routed.OriginalQuantity == 3
                && routed.OriginalMassGrams == expectedBatchMassGrams
                && routed.RemainingQuantity == 3
                && routed.RemainingMassGrams == expectedBatchMassGrams,
                "sawmill routing authority did not capture exact output");

            string json = JsonUtility.ToJson(completed);
            ProductionPreparedOutputBatchSaveData roundTripped =
                JsonUtility.FromJson<ProductionPreparedOutputBatchSaveData>(json);
            Require(
                roundTripped != null
                && roundTripped.schemaVersion == 3
                && string.Equals(
                    JsonUtility.ToJson(roundTripped),
                    json,
                    StringComparison.Ordinal),
                "sawmill prepared-output batch did not round-trip deterministically");
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
            staleCycle.projectedPortfolioCapacityGrams = 10_800L;
            staleCycle.requiredMinimumCapacityGrams = 10_800L;
            RequireStaleRestoreRejected(
                staleCycle,
                "three-cycle 10,800g capacity mutation");

            // OPEN: a later fixture must rebuild and restore the complete
            // Production/Physical/Routing persistence graph. This focused slice
            // deliberately proves only the real adapter and its capacity-source
            // restore gate.
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(facilityObject);
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
                "items.TryReleaseDestinationAtomically(") == 3,
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
            NoOpWorkforceReplanService workforce =
                NoOpWorkforceReplanService.Instance;
            IProductionWorkshopRuntime workshops =
                EmptyProductionWorkshopRuntime.Instance;
            IProductionInputLogisticsService inputLogistics =
                new ProductionInputLogisticsService(
                    LoadCatalog(),
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
                Array.Empty<IProductionOutputHandler>(),
                narrativeQualification: null,
                performance: CharacterAiEditorTestDependencies.NeutralPerformance);
            ProductionFacilityHandle facilityHandle =
                bridge.CaptureFacility(facility);
            ProductionStockSensorRuntime runtime = new(
                bridge,
                new ProductionAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                items);

            runtime.RequestInstallation(facilityHandle);
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
                workstationTag = "workstation:mill"
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
            ProductionRuntimeFixture runtime = CreateRuntime(
                catalog,
                items,
                seed: 771,
                buildingWorld: new FixedBuildingWorldQuery(facility));
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
                        .IsolatedSectionFixtureOnly);
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
                seed: 772);
            ProductionBillsSaveSection terminalRestoredSection =
                new(
                    terminalRestored.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly);
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

            ProductionRuntimeFixture restored = CreateRuntime(
                catalog,
                items,
                seed: 771,
                buildingWorld: new FixedBuildingWorldQuery(facility));
            ProductionBillsSaveSection restoredSection =
                new ProductionBillsSaveSection(
                    restored.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly);
            DungeonGameRestoreReport restoreReport =
                new DungeonGameRestoreReport();
            restoredSection.Restore(
                partialSave,
                saveSection.SectionVersion,
                restoreReport);
            Require(
                restoreReport.Success,
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
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0].amount == 2
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .committedAmount == 1
                    && resolvedOutputPayload.bills[0].resolvedOutputs[0]
                        .committedMassGrams == 1000L,
                "blocked production output did not persist its exact resolved outcome");

            ProductionRuntimeFixture partialTerminalRuntime = CreateRuntime(
                catalog,
                items,
                seed: 999990,
                buildingWorld: new FixedBuildingWorldQuery(facility));
            ProductionBillsSaveSection partialTerminalSection =
                new(
                    partialTerminalRuntime.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly);
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
                seed: 999989);
            ProductionBillsSaveSection partialTerminalRoundTripSection =
                new(
                    partialTerminalRoundTrip.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly);
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
                clock: partialDestroyedClock);
            ProductionBillsSaveSection partialDestroyedSection =
                new(
                    partialDestroyedRuntime.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly);
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
                buildingWorld: new FixedBuildingWorldQuery(facility));
            ProductionBillsSaveSection outputRestoredSection =
                new ProductionBillsSaveSection(
                    outputRestored.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly);
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
                    && items.HasBufferedCommit(pendingOutput.pendingCommitId),
                "post-commit output crash did not persist its pending commit identity");

            ProductionRuntimeFixture crashRestored = CreateRuntime(
                catalog,
                items,
                seed: 1234567,
                buildingWorld: new FixedBuildingWorldQuery(facility));
            ProductionBillsSaveSection crashRestoredSection =
                new(
                    crashRestored.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly);
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
                clock: destroyedClock);
            ProductionBillsSaveSection destroyedSection =
                new(
                    destroyedRuntime.Core,
                    EmptyPhysicalItemRestoreCandidateQuery.Instance,
                    EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                    EmptyProductionPreparedOutputRestoreJoin.Instance,
                    ProductionOutputLifecycleRestoreCandidatePublisher
                        .IsolatedSectionFixtureOnly);
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
                    workstationTag = "workstation:test-brewery"
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
                clock: clock);
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
                    .IsolatedSectionFixtureOnly).Capture();
            ProductionRuntimeFixture restored = CreateRuntime(
                catalog,
                items,
                seed: 772,
                workshops: workshop,
                buildingWorld: new FixedBuildingWorldQuery(
                    workstation,
                    support),
                power: power,
                clock: clock);
            DungeonGameRestoreReport report =
                new DungeonGameRestoreReport();
            new ProductionBillsSaveSection(
                restored.Core,
                EmptyPhysicalItemRestoreCandidateQuery.Instance,
                EmptyPhysicalItemRestoreCandidateOutputQuery.Instance,
                EmptyProductionPreparedOutputRestoreJoin.Instance,
                ProductionOutputLifecycleRestoreCandidatePublisher
                    .IsolatedSectionFixtureOnly).Restore(
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
            Require(
                restored.GetBills(workstation)
                    .All(bill => bill.BillId != ruined.BillId)
                && items.GetAvailable("test:rot") == 2
                && items.GetAvailable("test:beer") == 3
                && items.GetRequested("test:fuel") == 3,
                "zero-integrity batch did not become matching physical rot");
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
        GrandProjectRuntime runtime = new GrandProjectRuntime(
            grandProjectAdapter,
            grandProjectAdapter,
            new FixedGameClock(),
            aggregateRootStore: new DungeonRuntimeAggregateRootStore());
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
            EmptyPhysicalItemRestoreCandidateQuery.Instance);
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
        GrandProjectRuntime restored = new GrandProjectRuntime(
            restoredGrandProjectAdapter,
            restoredGrandProjectAdapter,
            new FixedGameClock(),
            aggregateRootStore: new DungeonRuntimeAggregateRootStore());
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        GrandProjectSaveSection restoredSection =
            new GrandProjectSaveSection(
                restored,
                EmptyPhysicalItemRestoreCandidateQuery.Instance);
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
        GrandProjectRuntime source = new(
            sourcePort,
            sourcePort,
            new FixedGameClock(),
            new DungeonRuntimeAggregateRootStore());
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
        GrandProjectRuntime restored = new(
            restoredPort,
            restoredPort,
            new FixedGameClock(),
            new DungeonRuntimeAggregateRootStore());
        GrandProjectSaveSection restoredSection = new(restored, matching);
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
        FakeResourceStockPolicyRuntime source =
            new FakeResourceStockPolicyRuntime(catalog);
        ResourceStockPolicySaveSection sourceSection =
            new ResourceStockPolicySaveSection(
                source,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance);
        string canonicalJson = sourceSection.Capture();

        FakeResourceStockPolicyRuntime target =
            new FakeResourceStockPolicyRuntime(catalog);
        ResourceStockPolicySaveSection targetSection =
            new ResourceStockPolicySaveSection(
                target,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance);
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
                EmptyPhysicalItemRestoreCandidateQuery.Instance);
        string canonicalJson = sourceSection.Capture();

        FakeRegionalSupplyContractRuntime target =
            new FakeRegionalSupplyContractRuntime(canonical);
        RegionalSupplyContractSaveSection targetSection =
            new RegionalSupplyContractSaveSection(
                target,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance);
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
            aggregateRootStore: aggregateRootStore);
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
                EmptyPhysicalItemRestoreCandidateQuery.Instance);
        ResourceStockPolicySaveSection stockPolicySection =
            new ResourceStockPolicySaveSection(
                stockPolicies,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance);
        RegionalSupplyContractSaveSection regionalSection =
            new RegionalSupplyContractSaveSection(
                regionalContracts,
                catalog,
                EmptyPhysicalItemRestoreCandidateQuery.Instance);

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
            aggregateRootStore);

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
                workstationTag = "workstation:feedbench"
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

    private static ProductionRuntimeFixture CreateRuntime(
        IResourceEconomyContentCatalog catalog,
        IProductionItemGateway items,
        int seed,
        IProductionWorkshopRuntime workshops = null,
        IBuildingWorldQuery buildingWorld = null,
        IPowerInfrastructureQuery power = null,
        IGameClock clock = null,
        IProductionPreparedOutputExecutionPort preparedOutputExecution = null,
        IProductionPreparedOutputRoutingAuthority preparedOutputRouting = null)
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
            Array.Empty<IProductionOutputHandler>(),
            narrativeQualification: null,
            performance: CharacterAiEditorTestDependencies.NeutralPerformance);
        IProductionOutputPlanningService outputPlanning =
            new ProductionOutputPlanningService(catalog, bridge);
        IProductionOutputExecutionService outputExecution =
            new ProductionOutputExecutionService(
                bridge,
                EmptyGrandProjectBenefitQuery.Instance,
                outputPlanning,
                new RandomStreamProvider(seed));
        ProductionAggregateStateStore stateStore =
            new ProductionAggregateStateStore(
                new DungeonRuntimeAggregateRootStore());
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
                        "Production test item gateway must expose stock-sensor removal outputs."));
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
            new ExtremeTraitRuntime(new CharacterIdentityStateStore()),
            clock,
            new CharacterIdentityEventPublisher(new GameEventBus()));
        return new ProductionRuntimeFixture(
            core,
            scene,
            destinationClaims,
            destinationCapacities);
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
            retirable.Contains(ownerBillId.Value);

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
                    record != null
                    && ProductionPreparedOutputMigrationScope.Contains(
                        record.recipeId)))
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
            IFacilityBufferDestinationClaimQuery destinationClaims,
            IFacilityBufferMassCapacityQuery destinationCapacities)
        {
            Core = core ?? throw new ArgumentNullException(nameof(core));
            this.scene = scene ?? throw new ArgumentNullException(nameof(scene));
            DestinationClaims = destinationClaims
                ?? throw new ArgumentNullException(nameof(destinationClaims));
            DestinationCapacities = destinationCapacities
                ?? throw new ArgumentNullException(nameof(destinationCapacities));
        }

        public ProductionBillRuntime Core { get; }
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
