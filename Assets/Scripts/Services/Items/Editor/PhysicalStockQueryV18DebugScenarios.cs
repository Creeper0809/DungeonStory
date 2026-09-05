#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class PhysicalStockQueryV18DebugScenarios
{
    public const string WarehouseMassAuthorityReportPath =
        "Artifacts/QA/v27-storage-mass-authority.txt";
    private const string LumberItemId = "material:lumber";
    private const string InoculatedLogItemId = "supply:inoculated-log";

    [MenuItem("DungeonStory/Debug/Items/Run V18 Physical Stock Query Contracts")]
    public static void RunAll()
    {
        PreparedOutputLegacyBypassStaticDiagnostics.RunAll();
        PhysicalItemExactSourcePublicationDebugScenarios.RunAll();
        FacilityBufferMassAdmissionDebugScenarios.RunAll();
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        IPhysicalItemMassQuery massQuery = CreateMassQuery(catalog);
        VerifyProjectorComposition(catalog);
        PhysicalStockQuery query = new(repository, catalog, massQuery);
        VerifyAuthoredWarehouseMassAuthorities();
        BuildingSO l02 = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/L02_상자더미.asset");
        Require(
            string.Equals(
                typeof(ItemDefinitionId).Assembly.GetName().Name,
                "DungeonStory.Foundation",
                StringComparison.Ordinal),
            "ItemDefinitionId is not owned by DungeonStory.Foundation.");
        Require(
            string.Equals(
                typeof(PhysicalMassGrams).Assembly.GetName().Name,
                "DungeonStory.Items",
                StringComparison.Ordinal),
            "PhysicalMassGrams is not owned by DungeonStory.Items.");
        Require(
            massQuery.GetDefinitionUnitMass((ItemDefinitionId)LumberItemId).Value
                == 1200L,
            "Canonical lumber mass did not capture as exact integer grams.");
        Require(
            massQuery.GetQuantityMass(
                (ItemDefinitionId)LumberItemId,
                PhysicalItemMassSubject.ForDefinition(
                    (ItemDefinitionId)LumberItemId),
                7).Value == 8400L,
            "Definition quantity mass was not a checked exact-gram product.");
        RequireThrows<System.Collections.Generic.KeyNotFoundException>(
            () => massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)"item:missing-mass-authority"),
            "Unknown item mass did not fail loud.");
        RequireThrows<InvalidOperationException>(
            () => PhysicalMassGrams.FromCanonicalKilograms(0.11000001f),
            "Non-canonical float mass did not fail loud.");
        Require(
            massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)"material:cave-silk").Value == 110L,
            "Canonical cave-silk asset did not project to exact 110g.");
        RequireThrows<OverflowException>(
            () => new PhysicalMassGrams(long.MaxValue).Multiply(2),
            "Mass multiplication overflow did not fail loud.");
        Require(
            typeof(IPhysicalItemMassQuery)
                .GetMethods()
                .SelectMany(method => method.GetParameters())
                .All(parameter => parameter.ParameterType.Name
                    .IndexOf("SaveData", StringComparison.Ordinal) < 0),
            "Runtime mass query accepts a save DTO input.");
        VerifyMassQueryPerformance(massQuery);
        BuildingInstanceId firstWarehouse =
            (BuildingInstanceId)"building:test-stock-query-a";
        BuildingInstanceId secondWarehouse =
            (BuildingInstanceId)"building:test-stock-query-b";
        string firstDestination =
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + firstWarehouse.Value;
        string secondDestination =
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + secondWarehouse.Value;
        string itemId = LumberItemId;

        WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            7,
            WorldItemStackState.Stored,
            firstDestination);
        WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            5,
            WorldItemStackState.Stored,
            secondDestination);
        string outboundId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            3,
            WorldItemStackState.Stored,
            "facility-input:test",
            firstDestination);
        WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            11,
            WorldItemStackState.Loose);

        Require(query.GetWarehouseQuantity(firstWarehouse, itemId) == 10,
            "Warehouse query did not derive stored and outbound physical quantities.");
        Require(query.GetWarehouseQuantity(secondWarehouse, itemId) == 5,
            "Warehouse identities leaked stock across buildings.");
        Require(query.GetWarehouseTotal(firstWarehouse) == 10,
            "Warehouse total was not derived from physical stacks.");
        WarehouseInventory massInventory = new(
            25_000L,
            StockCategory.General,
            restrictCategory: false);
        massInventory.BindPhysicalStock(
            query,
            firstWarehouse,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        Require(massInventory.StoredMassGrams == 12_000L
                && massInventory.RemainingMassGrams == 13_000L,
            $"Mass warehouse index mismatch: stored={massInventory.StoredMassGrams}; "
            + $"remaining={massInventory.RemainingMassGrams}.");
        Require(
            string.Equals(
                WarehouseMassUiFormatter.FormatCapacity(massInventory),
                "12kg/25kg",
                StringComparison.Ordinal)
            && string.Equals(
                WarehouseMassUiFormatter.FormatKilograms(39_300L),
                "39.3kg",
                StringComparison.Ordinal),
            "Warehouse mass UI did not project canonical grams exactly.");
        WarehouseManagementSummary massSummary =
            BuildingManagementSummaryQuery.FromWarehouses(new[]
            {
                new WarehouseManagementSnapshot(
                    totalStock: 10,
                    stock: massInventory.EnumerateStock().ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value),
                    storedMassGrams: 12_000L,
                    maxMassGrams: 25_000L),
                new WarehouseManagementSnapshot(
                    totalStock: 2,
                    stock: new Dictionary<StockCategory, int>(),
                    storedMassGrams: 2_000L,
                    maxMassGrams: 8_000L)
            });
        Require(massSummary.WarehouseCount == 2
                && massSummary.TotalStoredMassGrams == 14_000L
                && massSummary.TotalMaxMassGrams == 33_000L,
            "Warehouse management summary did not aggregate gram capacities.");
        Require(massInventory.GetAcceptableQuantity(LumberItemId, 20) == 10
                && massInventory.CanStoreItem(LumberItemId, 10)
                && !massInventory.CanStoreItem(LumberItemId, 11),
            "Generic partial admission did not floor by remaining canonical grams.");
        WarehouseInventorySnapshot policySnapshot = massInventory.CreateSnapshot();
        Require(policySnapshot.version == 4
                && typeof(WarehouseInventorySnapshot).GetField("maxCapacity") == null,
            "V4 warehouse policy snapshot duplicates immutable capacity.");
        massInventory.ApplySnapshot(policySnapshot);
        Require(massInventory.MaxMassGrams == 25_000L,
            "Policy restore overwrote immutable authored gram capacity.");
        VerifyWarehouseMassQueryPerformance(massInventory);
        VerifyL02InoculatedLogMassAdmission(catalog, massQuery, l02);
        Require(query.GetGlobalQuantity(itemId) == 26,
            "Global physical stock quantity is inconsistent.");
        Require(
            query.GetAllStacks().All(stack =>
                Mathf.Approximately(stack.UnitWeight, 1.2f)),
            "Physical stock snapshots bypassed the integer-gram mass authority.");

        WorldItemRepositoryEditorAccess.RemoveStack(repository, outboundId);
        Require(query.GetWarehouseTotal(firstWarehouse) == 7,
            "The derived stock index retained removed physical state.");
        string overCapacityId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            15,
            WorldItemStackState.Stored,
            firstDestination);
        Require(massInventory.StoredMassGrams == 26_400L
                && massInventory.RemainingMassGrams == 0L
                && massInventory.GetAcceptableQuantity(LumberItemId, 1) == 0,
            "Valid over-capacity stock was not preserved while new admission was blocked.");
        WorldItemRepositoryEditorAccess.RemoveStack(repository, overCapacityId);
        Require(massInventory.StoredMassGrams == 8_400L,
            "Warehouse mass index did not invalidate after physical stock removal.");
        VerifyWarehouseMassAdmissionLifecycle(
            repository,
            catalog,
            massQuery,
            query,
            firstWarehouse,
            secondWarehouse,
            massInventory);
        VerifyConveyorWarehouseMassAdmission(
            repository,
            catalog,
            massQuery,
            query);
        VerifyCombatEquipmentDynamicMass(
            repository,
            catalog,
            massQuery,
            query,
            secondWarehouse);
        VerifyApparelPhysicalMass(massQuery);
        VerifyPackagedLotPhysicalMass();
        VerifyPackagedLotTareDisposition();
        VerifyWildlifeCarcassPhysicalMass(
            massQuery,
            CreateWildlifeSpeciesCatalog());
        VerifyAtomicWildlifeCarcassTransform(
            repository,
            catalog,
            massQuery,
            query,
            CreateWildlifeSpeciesCatalog());
        VerifyPartialPhysicalTransform(
            repository,
            catalog,
            massQuery,
            query);
        VerifyExactPhysicalRelocation(
            repository,
            catalog,
            massQuery,
            query);
        VerifyAtomicBatchDisposition(
            repository,
            massQuery,
            query);
        VerifyPendingBatchDispositionSaveRestore(catalog, massQuery);
        VerifyReservedPendingSinkAtomicity(catalog, massQuery);
        VerifyReservedPendingTransferAtomicity(catalog, massQuery);
        VerifyProductionRawConsumeManifest();
        VerifyProductionCountOnlyWarehouseCandidateManifest();

        Debug.Log(
            "V18 PHYSICAL STOCK QUERY PASS: warehouse totals are rebuildable views "
            + "over physical stacks and have no independent save state. "
            + "V27_MASS_AUTHORITY_ASSEMBLY_EXACT=PASS; "
            + "V27_GENERIC_MASS_QUERY_EXACT_GRAMS=PASS; "
            + "V27_MASS_QUERY_FAILS_LOUD=PASS; "
            + "V27_MASS_QUERY_SAVE_DTO_FREE=PASS; "
            + "V27_MASS_PROJECTOR_COMPOSITION_EXACT=PASS; "
            + "V27_MASS_QUERY_10000_OP_P95=PASS; "
            + "V27_MASS_QUERY_STEADY_ALLOC_0B=PASS; "
            + "V27_PRODUCTION_RAW_CONSUME_CALLS_ZERO=PASS; "
            + "V27_PRODUCTION_COUNT_ONLY_WAREHOUSE_CANDIDATES_ZERO=PASS; "
            + "V27_L01_MASS_CAPACITY_25000G=PASS; "
            + "V27_L02_MASS_CAPACITY_12500G=PASS; "
            + "V27_L02_INOCULATED_LOG_COUNT_FALLBACK_BYPASSED=PASS; "
            + "V27_L02_INOCULATED_LOG_ADMISSION_17X700G_EXACT=PASS; "
            + "V27_L02_INOCULATED_LOG_OVERFILL_REJECTED=PASS; "
            + "V27_L02_CURRENT_FORMAT_RESTORE_EXACT=PASS; "
            + "V27_WAREHOUSE_MASS_INDEX_EXACT=PASS; "
            + "V27_WAREHOUSE_MASS_UI_EXACT_KG=PASS; "
            + "V27_WAREHOUSE_MASS_SUMMARY_DIMENSIONS_SEPARATED=PASS; "
            + "V27_WAREHOUSE_GENERIC_PARTIAL_ADMISSION=PASS; "
            + "V27_WAREHOUSE_POLICY_V4_DEFINITION_OWNED_CAPACITY=PASS; "
            + "V27_WAREHOUSE_VALID_OVERCAPACITY_BLOCKS_INGRESS=PASS; "
            + "V27_WAREHOUSE_LOCAL_REVISION_ISOLATED=PASS; "
            + "V27_WAREHOUSE_ADMISSION_PARTIAL_RESERVE_EXACT=PASS; "
            + "V27_WAREHOUSE_ADMISSION_COMMIT_RECEIPT_IDEMPOTENT=PASS; "
            + "V27_WAREHOUSE_ADMISSION_RELEASE_TOMBSTONE=PASS; "
            + "V27_WAREHOUSE_ADMISSION_EXPIRED_TOMBSTONE=PASS; "
            + "V27_WAREHOUSE_ADMISSION_EXTERNAL_MUTATION_INVALIDATED=PASS; "
            + "V27_CONVEYOR_WAREHOUSE_MASS_ADMISSION_EXACT=PASS; "
            + "V27_CONVEYOR_PARTIAL_MASS_REJECT_PRESERVES_TRANSIT=PASS; "
            + "V27_COMBAT_EQUIPMENT_DYNAMIC_MASS_EXACT=PASS; "
            + "V27_COMBAT_EQUIPMENT_NON_MASS_STATE_INVARIANT=PASS; "
            + "V27_COMBAT_EQUIPMENT_WAREHOUSE_ADMISSION_EXACT=PASS; "
            + "V27_COMBAT_EQUIPMENT_MASS_QUERY_10000_OP_P95=PASS; "
            + "V27_COMBAT_EQUIPMENT_MASS_QUERY_STEADY_ALLOC_0B=PASS; "
            + "V27_APPAREL_PHYSICAL_MASS_EXACT=PASS; "
            + "V27_APPAREL_NON_MASS_STATE_INVARIANT=PASS; "
            + "V27_APPAREL_MATERIAL_WEIGHT_PROJECTS_PHYSICAL_AUTHORITY=PASS; "
             + "V27_WILDLIFE_CARCASS_SPECIES_ITEM_MASS_EXACT=PASS; "
             + "V27_WILDLIFE_CARCASS_PREPARED_SUBJECT_EXACT=PASS; "
             + "V27_WILDLIFE_CARCASS_TRANSFORM_ATOMIC=PASS; "
             + "V27_WILDLIFE_CARCASS_TRANSFORM_MASS_RECEIPT_EXACT=PASS; "
              + "V27_WILDLIFE_CARCASS_TRANSFORM_FAILURE_PRESERVES_SOURCE=PASS; "
              + "V27_PARTIAL_TRANSFORM_QUANTITY_AND_MASS_EXACT=PASS; "
              + "V27_INVALID_TRANSFORM_OUTPUT_FAILS_ATOMICALLY=PASS; "
              + "V27_MULTI_INPUT_TRANSFORM_EXACT=PASS; "
              + "V27_POST_COMMIT_EXCEPTION_RESTORES_EXACT_SOURCES=PASS; "
              + "V27_PHYSICAL_RELOCATION_IDENTITY_AND_MASS_EXACT=PASS; "
              + "V27_BATCH_DISPOSITION_MULTI_STACK_ATOMIC=PASS; "
              + "V27_BATCH_DISPOSITION_EXCEPTION_RESTORES_SOURCES=PASS; "
              + "V27_PENDING_DISPOSITION_RETRY_IDEMPOTENT=PASS; "
              + "V27_PENDING_DISPOSITION_SAVE_RESTORE_EXACT=PASS; "
              + "V27_PENDING_DISPOSITION_CONFLICT_FAILS_LOUD=PASS; "
              + "V27_PENDING_DISPOSITION_ACK_IDEMPOTENT=PASS; "
              + "V27_PENDING_DISPOSITION_TAMPER_REJECTED=PASS; "
            + "V27_WAREHOUSE_MASS_QUERY_10000_OP_P95=PASS; "
            + "V27_WAREHOUSE_MASS_QUERY_STEADY_ALLOC_0B=PASS.");
    }

    private static void VerifyProductionRawConsumeManifest()
    {
        HashSet<string> allowedBoundaryFiles = new(StringComparer.Ordinal)
        {
            "Assets/Scripts/Services/Items/ItemTransferService.cs",
            "Assets/Scripts/Services/Items/PhysicalItemDisposition.cs",
            "Assets/Scripts/Services/Items/WorldItemModels.cs",
            "Assets/Scripts/Services/Items/WorldItemStackRuntime.cs"
        };
        List<string> violations = new();
        foreach (string rawPath in Directory.GetFiles(
                     "Assets/Scripts",
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            string path = rawPath.Replace('\\', '/');
            if (path.Contains("/Editor/", StringComparison.Ordinal)
                || allowedBoundaryFiles.Contains(path))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains(
                        ".TryConsumeStackQuantity(",
                        StringComparison.Ordinal))
                {
                    violations.Add($"{path}:{index + 1}");
                }
            }
        }

        Require(
            violations.Count == 0,
            "Production raw TryConsumeStackQuantity callsites remain outside "
            + "the typed Items boundary: "
            + string.Join(", ", violations));
    }

    private static void VerifyProductionCountOnlyWarehouseCandidateManifest()
    {
        List<string> violations = new();
        foreach (string rawPath in Directory.GetFiles(
                     "Assets/Scripts",
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            string path = rawPath.Replace('\\', '/');
            if (path.Contains("/Editor/", StringComparison.Ordinal)
                || string.Equals(
                    path,
                    "Assets/Scripts/Models/Buildings/Core/WarehouseInventory.cs",
                    StringComparison.Ordinal))
            {
                continue;
            }

            string[] forbiddenMembers =
            {
                ".CanStore(",
                ".MaxCapacity",
                ".RemainingCapacity",
                ".HasCapacityLimit"
            };
            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                if (forbiddenMembers.Any(member => lines[index].Contains(
                        member,
                        StringComparison.Ordinal)))
                {
                    violations.Add($"{path}:{index + 1}");
                }
            }
        }

        Require(
            violations.Count == 0,
            "Production count-only warehouse candidate callsites remain; "
            + "use exact item identity and gram-capacity authority instead: "
            + string.Join(", ", violations));
    }

    private static void VerifyPartialPhysicalTransform(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        PhysicalStockQuery stockQuery)
    {
        EditorNullItemMarkerPresenter markers =
            EditorNullItemMarkerPresenter.Instance;
        PhysicalItemTransformService transforms = new(
            repository,
            new WorldItemSpawner(catalog, repository, markers),
            massQuery,
            catalog,
            markers);
        long lumberMass = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)LumberItemId).Value;
        DungeonItemDefinition output = catalog.All
            .Where(value => value != null
                && value.MaxStack > 1
                && !string.Equals(value.ItemId, LumberItemId,
                    StringComparison.Ordinal)
                && !PhysicalItemIds.TryGetEquipmentDefinitionId(
                    value.ItemId,
                    out _)
                && !PhysicalItemIds.IsEquipmentModule(value.ItemId)
                && massQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)value.ItemId).Value <= lumberMass)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No non-unique output fits the partial transform mass fixture.");
        Vector2Int position = new(39, 4);
        string sourceId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            3,
            WorldItemStackState.Loose,
            position: position);
        Require(
            transforms.TryTransformQuantity(
                sourceId,
                1,
                new[]
                {
                    new PhysicalItemTransformOutput(
                        output.ItemId,
                        1,
                        position)
                },
                $"qa:partial-transform:{sourceId}",
                "qa-partial-transform",
                out PhysicalItemTransformReceipt receipt,
                out PhysicalItemTransformFailureCode failureCode,
                out string failureReason)
            && failureCode == PhysicalItemTransformFailureCode.None
            && receipt.IsCommitted
            && receipt.InputQuantity == 1
            && receipt.InputMassGrams == lumberMass
            && stockQuery.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                sourceId,
                StringComparison.Ordinal)).Quantity == 2,
            $"Partial transform did not preserve its source remainder: {failureCode}:{failureReason}");

        int sourceQuantityBeforeInvalid = stockQuery.GetAllStacks()
            .Single(value => string.Equals(
                value.StackId,
                sourceId,
                StringComparison.Ordinal))
            .Quantity;
        int outputQuantityBeforeInvalid = stockQuery.GetAllStacks()
            .Where(value => value.Position == position
                && string.Equals(value.ItemId, output.ItemId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        Require(
            !transforms.TryTransformQuantity(
                sourceId,
                1,
                new[]
                {
                    new PhysicalItemTransformOutput(
                        output.ItemId,
                        1,
                        position),
                    new PhysicalItemTransformOutput(string.Empty, 1, position)
                },
                $"qa:invalid-partial-transform:{sourceId}",
                "qa-invalid-partial-transform",
                out _,
                out PhysicalItemTransformFailureCode invalidCode,
                out _)
            && invalidCode == PhysicalItemTransformFailureCode.InvalidRequest
            && stockQuery.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                sourceId,
                StringComparison.Ordinal)).Quantity == sourceQuantityBeforeInvalid
            && stockQuery.GetAllStacks()
                .Where(value => value.Position == position
                    && string.Equals(value.ItemId, output.ItemId,
                        StringComparison.Ordinal))
                .Sum(value => value.Quantity) == outputQuantityBeforeInvalid,
            "Invalid transform output was silently filtered or mutated physical state.");

        Vector2Int firstSourcePosition = new(40, 4);
        Vector2Int secondSourcePosition = new(41, 4);
        Vector2Int multiOutputPosition = new(42, 4);
        string firstSourceId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.Loose,
            position: firstSourcePosition);
        string secondSourceId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.Loose,
            position: secondSourcePosition);
        bool multiCommitted = transforms.TryTransformQuantities(
                new[]
                {
                    new PhysicalItemTransformInput(firstSourceId, 2),
                    new PhysicalItemTransformInput(secondSourceId, 1)
                },
                new[]
                {
                    new PhysicalItemTransformOutput(
                        output.ItemId,
                        1,
                        multiOutputPosition)
                },
                $"qa:multi-transform:{firstSourceId}:{secondSourceId}",
                "qa-multi-transform",
                out PhysicalItemTransformReceipt multiReceipt,
                out PhysicalItemTransformFailureCode multiFailure,
                out string multiReason);
        int firstAfterMulti = stockQuery.GetAllStacks()
            .Where(value => string.Equals(value.StackId, firstSourceId,
                StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        int secondAfterMulti = stockQuery.GetAllStacks()
            .Where(value => string.Equals(value.StackId, secondSourceId,
                StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        Require(
            multiCommitted
            && multiReceipt.IsCommitted
            && multiReceipt.SourceStackIds.SequenceEqual(
                new[] { firstSourceId, secondSourceId }
                    .OrderBy(value => value, StringComparer.Ordinal))
            && multiReceipt.InputQuantity == 3
            && multiReceipt.InputMassGrams == lumberMass * 3L
            && firstAfterMulti == 0
            && secondAfterMulti == 1,
            $"Multi-input transform was not exact: committed={multiCommitted}; "
            + $"receipt={multiReceipt.IsCommitted}; sources={string.Join(",", multiReceipt.SourceStackIds ?? Array.Empty<string>())}; "
            + $"input={multiReceipt.InputQuantity}/{multiReceipt.InputMassGrams}; "
            + $"remaining={firstAfterMulti}/{secondAfterMulti}; failure={multiFailure}:{multiReason}");

        string rollbackFirstId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            1,
            WorldItemStackState.Loose,
            position: firstSourcePosition);
        string rollbackSecondId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.Loose,
            position: secondSourcePosition);
        PhysicalItemTransformService throwingTransforms = new(
            repository,
            new WorldItemSpawner(catalog, repository, markers),
            massQuery,
            catalog,
            new ThrowOnceItemMarkerPresenter());
        int outputBeforeRollback = stockQuery.GetAllStacks()
            .Where(value => value.Position == multiOutputPosition
                && string.Equals(value.ItemId, output.ItemId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        Require(
            !throwingTransforms.TryTransformQuantities(
                new[]
                {
                    new PhysicalItemTransformInput(rollbackFirstId, 1),
                    new PhysicalItemTransformInput(rollbackSecondId, 1)
                },
                new[]
                {
                    new PhysicalItemTransformOutput(
                        output.ItemId,
                        1,
                        multiOutputPosition)
                },
                $"qa:rollback-transform:{rollbackFirstId}:{rollbackSecondId}",
                "qa-rollback-transform",
                out _,
                out PhysicalItemTransformFailureCode rollbackCode,
                out _)
            && rollbackCode == PhysicalItemTransformFailureCode.OutputCommitFailed
            && stockQuery.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                rollbackFirstId,
                StringComparison.Ordinal)).Quantity == 1
            && stockQuery.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                rollbackSecondId,
                StringComparison.Ordinal)).Quantity == 2
            && stockQuery.GetAllStacks()
                .Where(value => value.Position == multiOutputPosition
                    && string.Equals(value.ItemId, output.ItemId,
                        StringComparison.Ordinal))
                .Sum(value => value.Quantity) == outputBeforeRollback,
            "Post-commit transform exception did not restore exact source identities and quantities.");

        foreach (WorldItemStackSnapshot stack in stockQuery.GetAllStacks()
                     .Where(value => value.Position == position
                         || value.Position == firstSourcePosition
                         || value.Position == secondSourcePosition
                         || value.Position == multiOutputPosition)
                     .ToArray())
        {
            WorldItemRepositoryEditorAccess.RemoveStack(
                repository,
                stack.StackId);
        }
    }

    private sealed class ThrowOnceItemMarkerPresenter : IItemMarkerPresenter
    {
        private bool throwNext = true;

        public void Initialize(IWorldItemMarkerDataSource dataSource)
        {
        }

        public void RefreshAll(IEnumerable<Vector2Int> positions)
        {
        }

        public void RefreshAt(Vector2Int position)
        {
            if (!throwNext)
            {
                return;
            }

            throwNext = false;
            throw new InvalidOperationException("Injected marker refresh failure.");
        }

        public bool TryGetMarkerAt(Vector2Int position, out UnityEngine.Object marker)
        {
            marker = null;
            return false;
        }

        public void Clear()
        {
        }
    }

    private static void VerifyExactPhysicalRelocation(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        PhysicalStockQuery stockQuery)
    {
        IItemMarkerPresenter markers = EditorNullItemMarkerPresenter.Instance;
        PhysicalItemRelocationService relocations = new(
            repository,
            new WorldItemSpawner(catalog, repository, markers),
            massQuery,
            markers);
        Vector2Int sourcePosition = new(43, 4);
        Vector2Int destinationPosition = new(44, 4);
        string sourceId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            3,
            WorldItemStackState.Stored,
            destinationId: "warehouse-storage:qa-relocation",
            position: sourcePosition);
        Require(
            relocations.TryRelocateQuantity(
                sourceId,
                1,
                destinationPosition,
                WorldItemStackState.Loose,
                string.Empty,
                $"qa:physical-relocation:{sourceId}",
                "qa-identity-preserving-relocation",
                out PhysicalItemRelocationReceipt receipt,
                out string failureReason)
            && receipt.IsCommitted
            && receipt.SourceStackId == sourceId
            && receipt.DestinationStackId != sourceId
            && receipt.ItemId == LumberItemId
            && receipt.Quantity == 1
            && receipt.MassGrams == massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)LumberItemId).Value
            && stockQuery.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                sourceId,
                StringComparison.Ordinal)).Quantity == 2
            && stockQuery.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                receipt.DestinationStackId,
                StringComparison.Ordinal)).Position == destinationPosition,
            $"Exact physical relocation failed: {failureReason}");

        foreach (WorldItemStackSnapshot stack in stockQuery.GetAllStacks()
                     .Where(value => value.Position == sourcePosition
                         || value.Position == destinationPosition)
                     .ToArray())
        {
            WorldItemRepositoryEditorAccess.RemoveStack(repository, stack.StackId);
        }
    }

    private static void VerifyAtomicBatchDisposition(
        WorldItemRepository repository,
        IPhysicalItemMassQuery massQuery,
        PhysicalStockQuery stockQuery)
    {
        Vector2Int firstPosition = new(45, 4);
        Vector2Int secondPosition = new(46, 4);
        string firstId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.Loose,
            position: firstPosition);
        string secondId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.Loose,
            position: secondPosition);
        PhysicalItemBatchDispositionService batch = new(
            repository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance);
        Require(
            batch.TryCommit(
                new[]
                {
                    new PhysicalItemTransformInput(firstId, 2),
                    new PhysicalItemTransformInput(secondId, 1)
                },
                PhysicalItemDispositionKind.Transfer,
                $"qa:batch-disposition:{firstId}:{secondId}",
                "qa-batch-custody-transfer",
                out PhysicalItemBatchDispositionReceipt receipt,
                out string failureReason)
            && receipt.IsCommitted
            && receipt.Quantity == 3
            && receipt.InputMassGrams == massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)LumberItemId).Value * 3L
            && stockQuery.GetAllStacks().All(value => !string.Equals(
                value.StackId,
                firstId,
                StringComparison.Ordinal))
            && stockQuery.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                secondId,
                StringComparison.Ordinal)).Quantity == 1,
            $"Atomic batch disposition failed: {failureReason}");

        string rollbackFirstId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            1,
            WorldItemStackState.Loose,
            position: firstPosition);
        string rollbackSecondId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.Loose,
            position: secondPosition);
        PhysicalItemBatchDispositionService throwingBatch = new(
            repository,
            massQuery,
            new ThrowOnceItemMarkerPresenter());
        Require(
            !throwingBatch.TryCommit(
                new[]
                {
                    new PhysicalItemTransformInput(rollbackFirstId, 1),
                    new PhysicalItemTransformInput(rollbackSecondId, 1)
                },
                PhysicalItemDispositionKind.Sink,
                $"qa:batch-rollback:{rollbackFirstId}:{rollbackSecondId}",
                "qa-batch-rollback",
                out _,
                out string rollbackFailure)
            && rollbackFailure.StartsWith(
                "physical-batch-disposition-rollback:",
                StringComparison.Ordinal)
            && stockQuery.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                rollbackFirstId,
                StringComparison.Ordinal)).Quantity == 1
            && stockQuery.GetAllStacks().Single(value => string.Equals(
                value.StackId,
                rollbackSecondId,
                StringComparison.Ordinal)).Quantity == 2,
            "Batch disposition did not restore exact sources after publication failure.");

        foreach (WorldItemStackSnapshot stack in stockQuery.GetAllStacks()
                     .Where(value => value.Position == firstPosition
                         || value.Position == secondPosition)
                     .ToArray())
        {
            WorldItemRepositoryEditorAccess.RemoveStack(repository, stack.StackId);
        }
    }

    private static void VerifyPendingBatchDispositionSaveRestore(
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery)
    {
        WorldItemRepository sourceRepository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            sourceRepository,
            LumberItemId,
            2,
            WorldItemStackState.Loose,
            position: new Vector2Int(48, 4));
        PhysicalItemBatchDispositionService sourceBatch = new(
            sourceRepository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance);
        string operationId = "production-wip:qa-pending-receipt:00000001";
        PhysicalItemTransformInput[] inputs =
        {
            new(stackId, 1)
        };
        Require(
            sourceBatch.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                "production.inputs-to-wip",
                out PhysicalItemBatchDispositionReceipt firstReceipt,
                out string firstFailure)
            && firstReceipt.IsCommitted
            && sourceRepository.GetEditorPendingBatchDispositionCount() == 1
            && sourceRepository.GetEditorTestQuantity(stackId) == 1,
            "Pending batch disposition did not commit exactly once: "
            + firstFailure);
        Require(
            sourceBatch.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                "production.inputs-to-wip",
                out PhysicalItemBatchDispositionReceipt replayReceipt,
                out string replayFailure)
            && replayReceipt.CommitId == firstReceipt.CommitId
            && sourceRepository.GetEditorTestQuantity(stackId) == 1,
            "Pending batch disposition retry was not idempotent: "
            + replayFailure);
        Require(
            !sourceBatch.TryCommitPending(
                new[] { new PhysicalItemTransformInput(stackId, 2) },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                "production.inputs-to-wip",
                out _,
                out string conflictFailure)
            && conflictFailure.StartsWith(
                "physical-batch-disposition-operation-conflict:",
                StringComparison.Ordinal),
            "Pending operation accepted a mismatched retry.");

        WorldItemPersistenceService sourcePersistence = new(
            catalog,
            new FixedHaulingSettings(),
            sourceRepository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        DungeonPhysicalItemSaveData saved = sourcePersistence.Capture();
        Require(
            saved.version == DungeonPhysicalItemSaveData.CurrentVersion
            && saved.pendingBatchDispositions.Count == 1,
            "Pending disposition was not captured in the current physical payload.");

        WorldItemRepository restoredRepository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        WorldItemPersistenceService restoredPersistence = new(
            catalog,
            new FixedHaulingSettings(),
            restoredRepository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        restoredPersistence.RestoreForEditorTest(saved);
        PhysicalItemBatchDispositionService restoredBatch = new(
            restoredRepository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance);
        Require(
            restoredBatch.TryCommitPending(
                inputs,
                PhysicalItemDispositionKind.Transfer,
                operationId,
                "production.inputs-to-wip",
                out PhysicalItemBatchDispositionReceipt restoredReceipt,
                out string restoredFailure)
            && restoredReceipt.CommitId == firstReceipt.CommitId
            && restoredRepository.GetEditorTestQuantity(stackId) == 1,
            "Restored pending disposition consumed physical input twice: "
            + restoredFailure);
        Require(
            restoredBatch.Acknowledge(
                restoredReceipt.CommitId,
                out string acknowledgeFailure)
            && restoredRepository.GetEditorPendingBatchDispositionCount() == 0
            && restoredBatch.Acknowledge(
                restoredReceipt.CommitId,
                out acknowledgeFailure),
            "Pending disposition acknowledgement was not idempotent: "
            + acknowledgeFailure);

        DungeonPhysicalItemSaveData tampered = restoredPersistence.Capture();
        tampered.pendingBatchDispositions.Add(new PhysicalItemBatchDispositionSaveData
        {
            kind = (int)PhysicalItemDispositionKind.Transfer,
            operationId = operationId,
            reasonCode = "production.inputs-to-wip",
            requestFingerprint = "tampered",
            sourceStackIds = new List<string> { stackId },
            quantity = 1,
            inputMassGrams = 1200L,
            commitId = "tampered"
        });
        RequireThrows<InvalidOperationException>(
            () => restoredPersistence.RestoreForEditorTest(tampered),
            "Mismatched pending receipt identity was accepted by restore.");
    }

    private static void VerifyReservedPendingSinkAtomicity(
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery)
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalStockQuery stock = new(repository, catalog, massQuery);
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new MutableAdmissionClock());
        string operationId = "meal:qa:reserved-pending:00000001";
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.FacilityBuffer,
            position: new Vector2Int(49, 4));
        WorldItemStackSnapshot stack = stock.GetAllStacks().Single(value =>
            string.Equals(value.StackId, stackId, StringComparison.Ordinal));
        Require(
            reservations.TryReserve(
                operationId,
                "character:qa:meal",
                ItemReservationPurpose.Meal,
                "meal:qa:facility:material:lumber",
                new ItemQuantityReservationRequest(
                    (ItemStackId)stackId,
                    1,
                    stack.ReservationSignature),
                out ItemQuantityLease lease,
                out DomainFailure reserveFailure),
            "Reserved pending Sink fixture could not reserve its source: "
            + reserveFailure.Code);
        PhysicalItemBatchDispositionService batch = new(
            repository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance,
            reservations);
        Require(
            batch.TryCommitReservedSinkPending(
                lease.leaseId,
                1,
                operationId,
                "character-meal-consumed",
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure)
            && receipt.IsCommitted
            && receipt.Kind == PhysicalItemDispositionKind.Sink
            && repository.GetEditorTestQuantity(stackId) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1
            && !reservations.Revalidate(lease.leaseId, out _, out _),
            "Reserved pending Sink did not atomically debit its lease and source: "
            + commitFailure);
        Require(
            batch.TryCommitReservedSinkPending(
                lease.leaseId,
                1,
                operationId,
                "character-meal-consumed",
                out PhysicalItemBatchDispositionReceipt replay,
                out string replayFailure)
            && replay.CommitId == receipt.CommitId
            && repository.GetEditorTestQuantity(stackId) == 1,
            "Reserved pending Sink replay consumed the source twice: "
            + replayFailure);
        Require(
            batch.Acknowledge(receipt.CommitId, out string acknowledgeFailure)
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "Reserved pending Sink acknowledgement failed: "
            + acknowledgeFailure);

        string rollbackOperation =
            "meal:qa:reserved-pending:rollback:00000002";
        string rollbackStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.FacilityBuffer,
            position: new Vector2Int(50, 4));
        WorldItemStackSnapshot rollbackStack = stock.GetAllStacks().Single(value =>
            string.Equals(
                value.StackId,
                rollbackStackId,
                StringComparison.Ordinal));
        Require(
            reservations.TryReserve(
                rollbackOperation,
                "character:qa:meal",
                ItemReservationPurpose.Meal,
                "meal:qa:facility:material:lumber",
                new ItemQuantityReservationRequest(
                    (ItemStackId)rollbackStackId,
                    1,
                    rollbackStack.ReservationSignature),
                out ItemQuantityLease rollbackLease,
                out reserveFailure),
            "Reserved pending Sink rollback fixture could not reserve its source: "
            + reserveFailure.Code);
        PhysicalItemBatchDispositionService throwingBatch = new(
            repository,
            massQuery,
            new ThrowOnceItemMarkerPresenter(),
            reservations);
        Require(
            !throwingBatch.TryCommitReservedSinkPending(
                rollbackLease.leaseId,
                1,
                rollbackOperation,
                "character-meal-consumed",
                out _,
                out string rollbackFailure)
            && rollbackFailure.StartsWith(
                "physical-reserved-disposition-rollback:",
                StringComparison.Ordinal)
            && repository.GetEditorTestQuantity(rollbackStackId) == 2
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && reservations.Revalidate(
                rollbackLease.leaseId,
                out ItemQuantityLease restoredLease,
                out _)
            && restoredLease.remainingQuantity == 1
            && reservations.GetReservedQuantity(
                (ItemStackId)rollbackStackId) == 1,
            "Reserved pending Sink failure did not restore exact source and lease ownership.");
        reservations.Release(
            rollbackLease.leaseId,
            ItemReservationReleaseReason.Cancelled);
    }

    private static void VerifyReservedPendingTransferAtomicity(
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery)
    {
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalStockQuery stock = new(repository, catalog, massQuery);
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new MutableAdmissionClock());
        PhysicalItemBatchDispositionService batch = new(
            repository,
            massQuery,
            EditorNullItemMarkerPresenter.Instance,
            reservations);

        string operationId =
            "apparel:qa:reserved-transfer-pending:00000001";
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.FacilityBuffer,
            position: new Vector2Int(51, 4));
        WorldItemStackSnapshot stack = stock.GetAllStacks().Single(value =>
            string.Equals(value.StackId, stackId, StringComparison.Ordinal));
        Require(
            reservations.TryReserve(
                operationId,
                "apparel:qa:craft-input",
                ItemReservationPurpose.ProductionInput,
                "production:qa:apparel:material:lumber",
                new ItemQuantityReservationRequest(
                    (ItemStackId)stackId,
                    1,
                    stack.ReservationSignature),
                out ItemQuantityLease lease,
                out DomainFailure reserveFailure),
            "Reserved pending Transfer fixture could not reserve its source: "
            + reserveFailure.Code);
        Require(
            batch.TryCommitReservedTransferPending(
                lease.leaseId,
                1,
                operationId,
                "apparel-inputs-to-wip",
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure)
            && receipt.IsCommitted
            && receipt.Kind == PhysicalItemDispositionKind.Transfer
            && repository.GetEditorTestQuantity(stackId) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1
            && !reservations.Revalidate(lease.leaseId, out _, out _)
            && batch.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt persisted)
            && persisted.Kind == PhysicalItemDispositionKind.Transfer
            && persisted.CommitId == receipt.CommitId,
            "Reserved pending Transfer did not atomically debit its lease/source "
            + "and persist an exact Transfer receipt: " + commitFailure);
        Require(
            batch.TryCommitReservedTransferPending(
                lease.leaseId,
                1,
                operationId,
                "apparel-inputs-to-wip",
                out PhysicalItemBatchDispositionReceipt replay,
                out string replayFailure)
            && replay.CommitId == receipt.CommitId
            && repository.GetEditorTestQuantity(stackId) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Reserved pending Transfer replay depended on its consumed lease or "
            + "consumed the source twice: " + replayFailure);
        Require(
            !batch.TryCommitReservedSinkPending(
                lease.leaseId,
                1,
                operationId,
                "apparel-inputs-to-wip",
                out _,
                out string kindConflictFailure)
            && kindConflictFailure.StartsWith(
                "physical-reserved-disposition-operation-conflict:",
                StringComparison.Ordinal)
            && repository.GetEditorTestQuantity(stackId) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Reserved pending disposition accepted a different kind for the same "
            + "operation or mutated committed state.");
        Require(
            batch.Acknowledge(receipt.CommitId, out string acknowledgeFailure)
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "Reserved pending Transfer acknowledgement failed: "
            + acknowledgeFailure);

        string ownedOperation =
            "apparel:qa:reserved-transfer-owner:00000002";
        string wrongOperation =
            "apparel:qa:reserved-transfer-wrong:00000002";
        string ownerStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.FacilityBuffer,
            position: new Vector2Int(52, 4));
        WorldItemStackSnapshot ownerStack = stock.GetAllStacks().Single(value =>
            string.Equals(
                value.StackId,
                ownerStackId,
                StringComparison.Ordinal));
        Require(
            reservations.TryReserve(
                ownedOperation,
                "apparel:qa:craft-input",
                ItemReservationPurpose.ProductionInput,
                "production:qa:apparel:material:lumber",
                new ItemQuantityReservationRequest(
                    (ItemStackId)ownerStackId,
                    1,
                    ownerStack.ReservationSignature),
                out ItemQuantityLease ownerLease,
                out reserveFailure),
            "Reserved pending Transfer operation-conflict fixture could not reserve: "
            + reserveFailure.Code);
        Require(
            !batch.TryCommitReservedTransferPending(
                ownerLease.leaseId,
                1,
                wrongOperation,
                "apparel-inputs-to-wip",
                out _,
                out string operationConflictFailure)
            && operationConflictFailure.StartsWith(
                "physical-reserved-disposition-lease-invalid:",
                StringComparison.Ordinal)
            && repository.GetEditorTestQuantity(ownerStackId) == 2
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && reservations.Revalidate(
                ownerLease.leaseId,
                out ItemQuantityLease retainedOwnerLease,
                out _)
            && retainedOwnerLease.remainingQuantity == 1,
            "Reserved pending Transfer accepted a lease owned by another operation "
            + "or changed its source/lease/pending state.");
        reservations.Release(
            ownerLease.leaseId,
            ItemReservationReleaseReason.Cancelled);

        string rollbackOperation =
            "apparel:qa:reserved-transfer-rollback:00000003";
        string rollbackStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.FacilityBuffer,
            position: new Vector2Int(53, 4));
        WorldItemStackSnapshot rollbackStack = stock.GetAllStacks().Single(value =>
            string.Equals(
                value.StackId,
                rollbackStackId,
                StringComparison.Ordinal));
        Require(
            reservations.TryReserve(
                rollbackOperation,
                "apparel:qa:craft-input",
                ItemReservationPurpose.ProductionInput,
                "production:qa:apparel:material:lumber",
                new ItemQuantityReservationRequest(
                    (ItemStackId)rollbackStackId,
                    1,
                    rollbackStack.ReservationSignature),
                out ItemQuantityLease rollbackLease,
                out reserveFailure),
            "Reserved pending Transfer rollback fixture could not reserve its source: "
            + reserveFailure.Code);
        PhysicalItemBatchDispositionService throwingBatch = new(
            repository,
            massQuery,
            new ThrowOnceItemMarkerPresenter(),
            reservations);
        Require(
            !throwingBatch.TryCommitReservedTransferPending(
                rollbackLease.leaseId,
                1,
                rollbackOperation,
                "apparel-inputs-to-wip",
                out _,
                out string rollbackFailure)
            && rollbackFailure.StartsWith(
                "physical-reserved-disposition-rollback:",
                StringComparison.Ordinal)
            && repository.GetEditorTestQuantity(rollbackStackId) == 2
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && reservations.Revalidate(
                rollbackLease.leaseId,
                out ItemQuantityLease restoredLease,
                out _)
            && restoredLease.remainingQuantity == 1
            && reservations.GetReservedQuantity(
                (ItemStackId)rollbackStackId) == 1,
            "Reserved pending Transfer publication failure did not restore exact "
            + "source/lease state and remove its pending receipt.");
        reservations.Release(
            rollbackLease.leaseId,
            ItemReservationReleaseReason.Cancelled);
    }


    private static void VerifyWildlifeCarcassPhysicalMass(
        IPhysicalItemMassQuery massQuery,
        IWildlifeSpeciesCatalogProvider speciesCatalog)
    {
        Require(speciesCatalog.All.Count > 0,
            "No authored wildlife carcass authorities were captured.");
        foreach (WildlifeSpeciesDefinition species in speciesCatalog.All)
        {
            ItemDefinitionId itemId = (ItemDefinitionId)species.CarcassItemId;
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                itemId,
                string.Empty,
                Array.Empty<ItemInstanceComponentSaveData>());
            long speciesGrams = PhysicalMassGrams
                .FromCanonicalKilograms(species.CarcassWeight)
                .Value;
            Require(
                subject.Kind == PhysicalItemMassSubjectKind.WildlifeCarcass
                && subject.HasPreparedUnitMass
                && subject.PreparedUnitMass.Value == speciesGrams
                && massQuery.GetPreparedStackUnitMass(subject).Value == speciesGrams,
                $"Wildlife carcass mass diverged for '{species.SpeciesId}'.");
        }
    }

    private static void VerifyAtomicWildlifeCarcassTransform(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        PhysicalStockQuery stockQuery,
        IWildlifeSpeciesCatalogProvider speciesCatalog)
    {
        WildlifeSpeciesDefinition species = speciesCatalog.All
            .Where(candidate => candidate != null
                && candidate.ButcherYields != null
                && candidate.ButcherYields.Any(yieldItem =>
                    yieldItem != null && yieldItem.amount > 0))
            .OrderBy(candidate => candidate.SpeciesId, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                "No wildlife species has an authored butcher yield.");
        EditorNullItemMarkerPresenter markers =
            EditorNullItemMarkerPresenter.Instance;
        WorldItemSpawner spawner = new(catalog, repository, markers);
        PhysicalItemTransformService transforms = new(
            repository,
            spawner,
            massQuery,
            catalog,
            markers);
        Vector2Int position = new(37, 4);
        string sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            species.CarcassItemId,
            1,
            WorldItemStackState.Loose,
            position: position);
        PhysicalItemTransformOutput[] outputs = species.ButcherYields
            .Where(yieldItem => yieldItem != null && yieldItem.amount > 0)
            .Select(yieldItem => new PhysicalItemTransformOutput(
                yieldItem.itemId,
                yieldItem.amount,
                position))
            .ToArray();
        long expectedInput = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)species.CarcassItemId).Value;
        long expectedOutput = outputs.Sum(output => checked(
            massQuery.GetDefinitionUnitMass((ItemDefinitionId)output.ItemId).Value
            * output.Quantity));
        Require(
            transforms.TryTransformWholeStack(
                sourceStackId,
                outputs,
                $"qa:wildlife-butcher:{sourceStackId}",
                "qa-wildlife-butcher-loss",
                out PhysicalItemTransformReceipt receipt,
                out PhysicalItemTransformFailureCode failureCode,
                out string failureReason)
            && failureCode == PhysicalItemTransformFailureCode.None
            && receipt.IsCommitted
            && receipt.InputMassGrams == expectedInput
            && receipt.OutputMassGrams == expectedOutput
            && receipt.LossMassGrams == expectedInput - expectedOutput
            && stockQuery.GetAllStacks().All(stack => !string.Equals(
                stack.StackId,
                sourceStackId,
                StringComparison.Ordinal)),
            $"Atomic wildlife transform failed: {failureCode}:{failureReason}");

        string rejectedSourceId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            species.CarcassItemId,
            1,
            WorldItemStackState.Loose,
            position: position);
        int stackCountBefore = stockQuery.GetAllStacks().Count;
        Require(
            !transforms.TryTransformWholeStack(
                rejectedSourceId,
                new[]
                {
                    new PhysicalItemTransformOutput(
                        LumberItemId,
                        100,
                        position)
                },
                $"qa:wildlife-overweight:{rejectedSourceId}",
                "qa-overweight-rejected",
                out _,
                out PhysicalItemTransformFailureCode rejectedCode,
                out _)
            && rejectedCode
                == PhysicalItemTransformFailureCode.OutputMassExceedsInput
            && stockQuery.GetAllStacks().Count == stackCountBefore
            && stockQuery.GetAllStacks().Any(stack => string.Equals(
                stack.StackId,
                rejectedSourceId,
                StringComparison.Ordinal)),
            "Rejected wildlife transform mutated its source or output state.");
        WorldItemRepositoryEditorAccess.RemoveStack(repository, rejectedSourceId);
        foreach (PhysicalItemTransformOutput output in outputs)
        {
            foreach (WorldItemStackSnapshot stack in stockQuery.GetAllStacks()
                         .Where(stack => stack.Position == position
                             && string.Equals(
                                 stack.ItemId,
                                 output.ItemId,
                                 StringComparison.Ordinal))
                         .ToArray())
            {
                WorldItemRepositoryEditorAccess.RemoveStack(
                    repository,
                    stack.StackId);
            }
        }
    }

    private static void VerifyApparelPhysicalMass(
        IPhysicalItemMassQuery massQuery)
    {
        const string itemId = DurableToolItemRules.HaulingHarness;
        const string instanceId = "apparel-instance:qa-hauling-harness";
        ApparelInstanceState state = new()
        {
            apparelDefinitionId = "apparel:hauling-harness",
            primaryMaterialId = "textile:common-wool",
            craftsmanshipQuality = CraftsmanshipQualityTier.Masterwork,
            sourceKind = TextileSourceKind.Animal,
            sourceDefinitionId = "textile:common-wool",
            size = ApparelSizeClass.Medium,
            durability = 37f,
            moisture = 91f,
            contamination = 76f,
            craftedAbsoluteDay = 11,
            deterministicBatchHash = 0xA11CEUL
        };
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)itemId,
            instanceId,
            new[] { ApparelItemStateCodec.Create(state) });
        Require(
            subject.Kind == PhysicalItemMassSubjectKind.Apparel
            && subject.HasPreparedUnitMass
            && subject.PreparedUnitMass.Value == 1150L
            && massQuery.GetPreparedStackUnitMass(subject).Value == 1150L,
            "Hauling harness did not project the physical item's exact 1,150g authority.");

        state.primaryMaterialId = "textile:cave-silk";
        state.craftsmanshipQuality = CraftsmanshipQualityTier.Awful;
        state.durability = 1f;
        state.moisture = 0f;
        state.contamination = 0f;
        PhysicalItemMassSubject changed = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)itemId,
            instanceId,
            new[] { ApparelItemStateCodec.Create(state) });
        Require(
            massQuery.GetPreparedStackUnitMass(changed).Value == 1150L,
            "Apparel material, quality, durability, moisture, or contamination changed mass.");

        IGameContentDefinitionSource content =
            CharacterAiEditorTestDependencies.ContentDefinitions;
        IApparelDefinitionCatalog apparel =
            new ResourceApparelDefinitionCatalog(content);
        ITextileMaterialCatalog materials =
            new ResourceTextileMaterialCatalog(content);
        ApparelMaterialProjector presentation = new(
            apparel,
            materials,
            massQuery);
        int apparelIndex = apparel.GetIndex("apparel:hauling-harness");
        ApparelDerivedStats wool = presentation.GetOrCreate(
            new ApparelProjectionKey(
                apparelIndex,
                materials.GetIndex("textile:common-wool"),
                CraftsmanshipQualityTier.Masterwork,
                4,
                TextileConditionBand.Ready,
                ApparelModificationKind.None,
                adjacentSize: false));
        ApparelDerivedStats silk = presentation.GetOrCreate(
            new ApparelProjectionKey(
                apparelIndex,
                materials.GetIndex("textile:cave-silk"),
                CraftsmanshipQualityTier.Awful,
                0,
                TextileConditionBand.Wet,
                ApparelModificationKind.None,
                adjacentSize: false));
        Require(
            Mathf.Approximately(wool.Weight, 1.15f)
            && Mathf.Approximately(silk.Weight, 1.15f),
            $"Apparel presentation retained a duplicate material-derived mass: "
            + $"wool={wool.Weight:0.###}, silk={silk.Weight:0.###}.");
    }

    private static void VerifyCombatEquipmentDynamicMass(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        PhysicalStockQuery stockQuery,
        BuildingInstanceId warehouseId)
    {
        const string equipmentDefinitionId = "weapon:crossbow";
        string itemId = PhysicalItemIds.ForEquipment(equipmentDefinitionId);
        const string instanceId = "equipment-instance:qa-dynamic-mass";
        const string moduleInstanceId = "item-instance:qa-dynamic-mass-module";
        CombatEquipmentInstance equipment = new()
        {
            instanceId = instanceId,
            definitionId = equipmentDefinitionId,
            durabilityRatio = 0.61f,
            quality = CombatEquipmentQuality.Masterwork,
            powerCharge = 17f,
            worldState = CombatEquipmentWorldState.Stored,
            loadedAmmunition = new LoadedAmmunitionBatch
            {
                ammunitionItemId = CombatItemDefinitions.BoltItemId,
                remaining = 3
            },
            moduleSlots = new List<EquipmentModuleSlotState>
            {
                new()
                {
                    slotIndex = 0,
                    moduleInstanceId = moduleInstanceId
                }
            }
        };
        EquipmentModuleInstance module = new()
        {
            instanceId = moduleInstanceId,
            definitionId = "module:weapon:balanced-core",
            grade = 4,
            condition = 0.42f,
            state = EquipmentModuleProcessState.Installed,
            attachedEquipmentInstanceId = instanceId
        };
        ItemInstanceComponentSaveData component =
            EquipmentItemStateCodec.Encode(equipment, new[] { module });
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)itemId,
            instanceId,
            new[] { component });
        long baseMass = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)itemId).Value;
        long moduleMass = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)PhysicalItemIds.ForEquipmentModule()).Value;
        long ammunitionMass = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)CombatItemDefinitions.BoltItemId).Value;
        long expectedMass = checked(baseMass + moduleMass + ammunitionMass * 3L);
        Require(
            subject.HasPreparedUnitMass
            && subject.PreparedUnitMass.Value == expectedMass,
            "Combat-equipment adapter did not freeze its prepared unit mass.");
        Require(
            massQuery.GetStackUnitMass((ItemDefinitionId)itemId, subject).Value
                == expectedMass,
            "Combat-equipment mass did not add base, attached module, and loaded ammunition exactly.");
        VerifyCombatEquipmentMassQueryPerformance(
            massQuery,
            subject);

        CombatEquipmentInstance presentationChanged = equipment.Clone();
        presentationChanged.durabilityRatio = 0.05f;
        presentationChanged.quality = CombatEquipmentQuality.Poor;
        presentationChanged.powerCharge = 99f;
        presentationChanged.worldState = CombatEquipmentWorldState.Carried;
        EquipmentModuleInstance modulePresentationChanged = module.Clone();
        modulePresentationChanged.grade = 1;
        modulePresentationChanged.condition = 0.01f;
        PhysicalItemMassSubject presentationSubject =
            PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                (ItemDefinitionId)itemId,
                instanceId,
                new[]
                {
                    EquipmentItemStateCodec.Encode(
                        presentationChanged,
                        new[] { modulePresentationChanged })
                });
        Require(
            massQuery.GetStackUnitMass(
                    (ItemDefinitionId)itemId,
                    presentationSubject).Value == expectedMass,
            "Durability, quality, world state, power charge, grade, or condition changed physical mass.");

        string destinationId =
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + warehouseId.Value;
        WarehouseInventory inventory = new(
            25_000L,
            StockCategory.General,
            restrictCategory: false);
        inventory.BindPhysicalStock(
            stockQuery,
            warehouseId,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        TestWarehouseFacility facility = new(warehouseId, inventory);
        WarehouseMassAdmissionService admission = new(
            catalog,
            massQuery,
            stockQuery,
            new TestWarehouseWorldQuery(facility),
            new MutableAdmissionClock(),
            repository);
        long beforeMass = stockQuery.GetWarehouseStoredMassGrams(warehouseId);
        WarehouseMassAdmissionRequest request = new(
            warehouseId,
            "qa:combat-equipment-dynamic-mass",
            (ItemDefinitionId)itemId,
            instanceId,
            subject.ComponentFingerprint,
            1,
            admission.GetWarehouseCapacityRevision(warehouseId),
            admission.CatalogRevision,
            expectedSourceRevision: repository.ItemStackVersion,
            massSubject: subject);
        Require(
            admission.TryReserve(
                request,
                out WarehouseMassAdmissionToken token,
                out DomainFailure reserveFailure)
            && !reserveFailure.IsFailure
            && token.AcceptedQuantity == 1
            && token.ReservedMassGrams == expectedMass,
            $"Combat-equipment dynamic gram admission failed: {reserveFailure.Code}.");
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            itemId,
            1,
            WorldItemStackState.Stored,
            destinationId,
            position: default,
            itemInstanceId: instanceId,
            components: new[] { component });
        long storedMassAfter = stockQuery.GetWarehouseStoredMassGrams(warehouseId);
        bool committed = admission.TryCommit(
                token.TokenId,
                "qa:combat-equipment-dynamic-mass:commit",
                out WarehouseMassAdmissionReceipt receipt,
                out DomainFailure commitFailure);
        Require(
            storedMassAfter == checked(beforeMass + expectedMass)
            && committed
            && !commitFailure.IsFailure
            && receipt.CommittedMassGrams == expectedMass
            && inventory.ReservedInboundMassGrams == 0L,
            $"Combat-equipment stored mass and admission receipt diverged: {commitFailure.Code}.");
        WorldItemRepositoryEditorAccess.RemoveStack(repository, stackId);
    }

    private static IPhysicalItemMassQuery CreateMassQuery(
        IDungeonItemCatalogProvider catalog)
    {
        GenericDefinitionPhysicalItemMassProjector definitions = new(catalog);
        return new PhysicalItemMassQuery(new IPhysicalItemMassProjector[]
        {
            definitions,
            new CombatEquipmentPhysicalItemMassProjector(definitions),
            new ApparelPhysicalItemMassProjector(),
            new PackagedLotPhysicalItemMassProjector(),
            new WildlifeCarcassPhysicalItemMassProjector(
                CreateWildlifeSpeciesCatalog(),
                catalog)
        });
    }

    private static void VerifyAuthoredWarehouseMassAuthorities()
    {
        (string Path, long ExpectedGrams)[] expected =
        {
            ("Assets/Resources/SO/Building/Modular/D10_식재료선반.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/H06_청소도구함.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/L01_대형보관선반.asset", 25_000L),
            ("Assets/Resources/SO/Building/Modular/L02_상자더미.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/L03_통더미.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/L04_자루더미.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/L05_식재료저장함.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/L06_무기로커.asset", 25_000L),
            ("Assets/Resources/SO/Building/Modular/L07_마력보관함.asset", 15_000L),
            ("Assets/Resources/SO/Building/Modular/M01_마력수정선반.asset", 13_500L),
            ("Assets/Resources/SO/Building/Modular/M02_마력저장조.asset", 27_000L),
            ("Assets/Resources/SO/Building/Modular/Q03_연구용책장.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/Q04_시약선반.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/Q05_표본보관장.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/R06_옷장.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/R09_개인보관함.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/S04_잡화상자.asset", 12_500L),
            ("Assets/Resources/SO/Building/Modular/S07_무기보관함.asset", 25_000L),
            ("Assets/Resources/SO/Building/Medical/M08_장기보관함.asset", 12_500L),
            ("Assets/Resources/SO/Building/P1/DefenseSupplyDepot.asset", 25_000L),
            ("Assets/Resources/SO/Building/P1/DefenseMaintenanceBench.asset", 25_000L)
        };

        Require(expected.Select(entry => entry.Path)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == expected.Length,
            "Authored warehouse mass table contains a duplicate asset path.");
        List<string> reportRows = new();
        foreach ((string path, long expectedGrams) in expected)
        {
            BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            BuildingStorageAbility storage = building?
                .Abilities
                .OfType<BuildingStorageAbility>()
                .SingleOrDefault();
            Require(building != null
                    && storage != null
                    && storage.capacity > 0
                    && storage.maxStoredMassGrams == expectedGrams
                    && building.GetStorageMassCapacityGrams() == expectedGrams,
                $"Warehouse mass authority mismatch at '{path}': "
                + $"expected={expectedGrams}; actual={storage?.maxStoredMassGrams ?? -1L}.");
            if (path.IndexOf("/Modular/", StringComparison.Ordinal) >= 0)
            {
                Require(
                    ModularFacilityAssetBuilder.GetStorageMassCapacityGrams(
                        building.GetFacilityCode()) == expectedGrams,
                    $"Modular builder mass authority diverged at '{path}'.");
            }

            string writer = path.IndexOf("/Modular/", StringComparison.Ordinal) >= 0
                ? nameof(ModularFacilityAssetBuilder)
                : path.IndexOf("/Medical/", StringComparison.Ordinal) >= 0
                    ? nameof(SurgeryContentAssetBuilder)
                    : nameof(P1DefenseFacilityAssetBuilder);
            reportRows.Add(
                "ROW\tcode=" + building.GetFacilityCode()
                + ";path=" + path
                + ";category=" + storage.category
                + ";count=" + storage.capacity
                + ";grams=" + storage.maxStoredMassGrams
                + ";allCategories=" + storage.allCategories
                + ";writer=" + writer);
        }

        var positiveCountStorage = AssetDatabase
            .FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .SelectMany(building => building.Abilities
                .OfType<BuildingStorageAbility>()
                .Where(storage => storage.capacity > 0)
                .Select(storage => new { Building = building, Storage = storage }))
            .ToArray();
        Require(positiveCountStorage.Length == expected.Length,
            $"Expected {expected.Length} positive-count storage authorities, "
            + $"found {positiveCountStorage.Length}.");
        Require(positiveCountStorage.All(entry =>
                entry.Storage.maxStoredMassGrams > 0L),
            "A positive-count storage authority still lacks positive gram capacity.");
        reportRows.Insert(
            0,
            "RESULT=PASS; authorities=" + expected.Length
            + "; positiveCount=" + positiveCountStorage.Length
            + "; positiveGram=" + positiveCountStorage.Count(entry =>
                entry.Storage.maxStoredMassGrams > 0L));
        Directory.CreateDirectory(Path.GetDirectoryName(
            WarehouseMassAuthorityReportPath) ?? "Artifacts/QA");
        File.WriteAllLines(
            WarehouseMassAuthorityReportPath,
            reportRows,
            new System.Text.UTF8Encoding(false));
    }

    private static void VerifyPackagedLotPhysicalMass()
    {
        const string medicineId = "medicine:test-packaged-dose";
        const string containerId = "container:test-medical-vial";
        IDungeonItemCatalogProvider catalog = new FixedItemCatalogProvider(
            new DungeonItemDefinition(
                medicineId,
                "Packaged medicine",
                "Test packaged dose",
                StockCategory.Medicine,
                1,
                null,
                0.16f,
                10,
                resourceKind: ResourceItemKind.Medicine,
                packageTareGrams: 30,
                packageTareDisposition:
                    PackageTareDisposition.ReusableContainerReturn,
                packageContainerItemId: containerId),
            new DungeonItemDefinition(
                containerId,
                "Empty medical vial",
                "Test reusable tare",
                StockCategory.General,
                1,
                null,
                0.03f,
                50));
        GenericDefinitionPhysicalItemMassProjector definitions = new(catalog);
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(
            new IPhysicalItemMassProjector[]
            {
                definitions,
                new PackagedLotPhysicalItemMassProjector()
            });
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)medicineId,
            string.Empty,
            Array.Empty<ItemInstanceComponentSaveData>());
        Require(
            subject.Kind == PhysicalItemMassSubjectKind.PackagedLot
            && massQuery.GetStackUnitMass(
                (ItemDefinitionId)medicineId,
                subject).Value == 160L
            && massQuery is IPackagedLotDefinitionQuery packagedLots
            && packagedLots.TryGetPackagedLot(
                (ItemDefinitionId)medicineId,
                out PackagedLotDefinitionSnapshot packagedLot)
            && packagedLot.ContentMass.Value == 130L
            && packagedLot.TareMass.Value == 30L
            && packagedLot.TotalUnitMass.Value == 160L
            && packagedLot.TareDisposition
                == PackageTareDisposition.ReusableContainerReturn
            && string.Equals(
                packagedLot.ContainerItemId.Value,
                containerId,
                StringComparison.Ordinal),
            "Packaged-lot immutable content/tare projection is not exact.");

        RequireThrows<InvalidOperationException>(
            () => new GenericDefinitionPhysicalItemMassProjector(
                new FixedItemCatalogProvider(
                    new DungeonItemDefinition(
                        medicineId,
                        "Packaged medicine",
                        "Invalid tare fixture",
                        StockCategory.Medicine,
                        1,
                        null,
                        0.16f,
                        10,
                        resourceKind: ResourceItemKind.Medicine,
                        packageTareGrams: 30,
                        packageTareDisposition:
                            PackageTareDisposition.ReusableContainerReturn,
                        packageContainerItemId: containerId),
                    new DungeonItemDefinition(
                        containerId,
                        "Wrong vial",
                        "Mismatched tare fixture",
                        StockCategory.General,
                        1,
                        null,
                        0.04f,
                        50))),
            "Packaged-lot container mass mismatch did not fail loud.");
    }

    private static void VerifyPackagedLotTareDisposition()
    {
        const string medicineId = "medicine:test-packaged-sink";
        const string destroyedMedicineId = "medicine:test-destroyed-package";
        const string containerId = "container:test-sink-vial";
        const string parentCommitId = "qa:packaged-lot-sink:0001";
        Vector2Int outputPosition = new(7, 3);
        GenericDefinitionPhysicalItemMassProjector definitions = new(
            new FixedItemCatalogProvider(
                new DungeonItemDefinition(
                    medicineId,
                    "Packaged medicine",
                    "Reusable tare fixture",
                    StockCategory.Medicine,
                    1,
                    null,
                    0.16f,
                    10,
                    resourceKind: ResourceItemKind.Medicine,
                    packageTareGrams: 30,
                    packageTareDisposition:
                        PackageTareDisposition.ReusableContainerReturn,
                    packageContainerItemId: containerId),
                new DungeonItemDefinition(
                    destroyedMedicineId,
                    "Disposable packaged medicine",
                    "Explicit loss receipt fixture",
                    StockCategory.Medicine,
                    1,
                    null,
                    0.12f,
                    10,
                    resourceKind: ResourceItemKind.Medicine,
                    packageTareGrams: 20,
                    packageTareDisposition:
                        PackageTareDisposition.DestroyedDuringUse),
                new DungeonItemDefinition(
                    containerId,
                    "Empty medical vial",
                    "Reusable tare output",
                    StockCategory.General,
                    1,
                    null,
                    0.03f,
                    50)));
        RecordingPackagedLotTareOutputGateway outputGateway = new();
        PackagedLotTareDispositionService service = new(
            definitions,
            outputGateway);

        bool first = service.EnsureTerminalSinkOutputs(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [medicineId] = 2
            },
            outputPosition,
            parentCommitId,
            out PackagedLotTareOutputReceipt firstReceipt,
            out string firstFailure);
        Require(
            first
            && string.IsNullOrEmpty(firstFailure)
            && firstReceipt.OutputQuantity == 2
            && firstReceipt.OutputMassGrams == 60L
            && firstReceipt.DestroyedTareMassGrams == 0L
            && firstReceipt.AccountedTareMassGrams == 60L
            && firstReceipt.OutputCommitIds.Count == 1
            && outputGateway.SpawnCalls == 1
            && outputGateway.GetAllStacks().Single().Quantity == 2
            && outputGateway.GetAllStacks().Single().Position == outputPosition,
            "Packaged-lot terminal Sink did not return exact physical tare.");

        bool replay = service.EnsureTerminalSinkOutputs(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [medicineId] = 2
            },
            outputPosition,
            parentCommitId,
            out PackagedLotTareOutputReceipt replayReceipt,
            out string replayFailure);
        Require(
            replay
            && string.IsNullOrEmpty(replayFailure)
            && replayReceipt.OutputMassGrams == firstReceipt.OutputMassGrams
            && outputGateway.SpawnCalls == 1
            && outputGateway.GetAllStacks().Count == 1,
            "Packaged-lot tare outbox replay duplicated its physical output.");

        outputGateway.GetAllStacks().Single().Position = outputPosition + Vector2Int.right;
        bool conflictingReplay = service.EnsureTerminalSinkOutputs(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [medicineId] = 2
            },
            outputPosition,
            parentCommitId,
            out _,
            out string conflictFailure);
        Require(
            !conflictingReplay
            && conflictFailure.StartsWith(
                "packaged-lot-tare-output-conflict:",
                StringComparison.Ordinal)
            && outputGateway.SpawnCalls == 1,
            "Conflicting packaged-lot tare output did not fail loud.");

        bool destroyed = service.EnsureTerminalSinkOutputs(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [destroyedMedicineId] = 3
            },
            outputPosition,
            "qa:packaged-lot-sink:destroyed",
            out PackagedLotTareOutputReceipt destroyedReceipt,
            out string destroyedFailure);
        Require(
            destroyed
            && string.IsNullOrEmpty(destroyedFailure)
            && destroyedReceipt.OutputQuantity == 0
            && destroyedReceipt.OutputMassGrams == 0L
            && destroyedReceipt.DestroyedTareMassGrams == 60L
            && destroyedReceipt.AccountedTareMassGrams == 60L
            && destroyedReceipt.OutputCommitIds.Count == 0
            && outputGateway.SpawnCalls == 1,
            "Destroyed packaged tare did not produce an exact parent-bound loss receipt.");

        bool destroyedReplay = service.EnsureTerminalSinkOutputs(
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [destroyedMedicineId] = 3
            },
            outputPosition,
            "qa:packaged-lot-sink:destroyed",
            out PackagedLotTareOutputReceipt destroyedReplayReceipt,
            out string destroyedReplayFailure);
        Require(
            destroyedReplay
            && string.IsNullOrEmpty(destroyedReplayFailure)
            && destroyedReplayReceipt.DestroyedTareMassGrams
                == destroyedReceipt.DestroyedTareMassGrams
            && destroyedReplayReceipt.AccountedTareMassGrams
                == destroyedReceipt.AccountedTareMassGrams
            && outputGateway.SpawnCalls == 1,
            "Destroyed packaged tare replay changed its declared loss or spawned output.");
    }

    private static IWildlifeSpeciesCatalogProvider CreateWildlifeSpeciesCatalog()
    {
        IGameContentCatalog content =
            CharacterAiEditorTestDependencies.ContentDefinitions
                as IGameContentCatalog
            ?? throw new InvalidOperationException(
                "Editor content authority does not expose the game-content catalog.");
        return new ResourceWildlifeSpeciesCatalogProvider(
            content,
            new ResourceItemDefinitionCatalog(content));
    }

    private static void VerifyL02InoculatedLogMassAdmission(
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        BuildingSO l02)
    {
        BuildingStorageAbility storage = l02?.GetAbility<BuildingStorageAbility>();
        Require(storage != null
                && storage.maxStoredMassGrams == 12_500L
                && storage.capacity == 16
                && !storage.allCategories
                && storage.category == StockCategory.General,
            "L02 storage projection does not preserve the reviewed 12,500g General contract.");
        Require(
            massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)InoculatedLogItemId).Value == 700L,
            "L02 admission fixture requires the exact 700g inoculated-log authority.");

        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalStockQuery stockQuery = new(repository, catalog, massQuery);
        BuildingInstanceId warehouseId =
            (BuildingInstanceId)"building:qa-l02-inoculated-log";
        string destinationId =
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
            + warehouseId.Value;
        WarehouseInventory inventory = new(
            storage.maxStoredMassGrams,
            storage.category,
            restrictCategory: !storage.allCategories);
        inventory.BindPhysicalStock(
            stockQuery,
            warehouseId,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        TestWarehouseFacility facility = new(warehouseId, inventory);
        WarehouseMassAdmissionService admission = new(
            catalog,
            massQuery,
            stockQuery,
            new TestWarehouseWorldQuery(facility),
            new MutableAdmissionClock(),
            repository);

        Require(inventory.GetAcceptableQuantity(InoculatedLogItemId, 18) == 17,
            "L02 positive gram authority was incorrectly clamped by legacy count capacity 16.");
        WarehouseMassAdmissionRequest request = new(
            warehouseId,
            "qa:l02-inoculated-log:admission",
            (ItemDefinitionId)InoculatedLogItemId,
            string.Empty,
            "generic:supply:inoculated-log",
            18,
            admission.GetWarehouseCapacityRevision(warehouseId),
            massQuery.AuthorityRevision);
        Require(
            admission.TryReserve(
                request,
                out WarehouseMassAdmissionToken token,
                out DomainFailure reserveFailure)
            && !reserveFailure.IsFailure
            && token.AcceptedQuantity == 17
            && token.ReservedMassGrams == 11_900L
            && inventory.ReservedInboundMassGrams == 11_900L,
            "L02 did not reserve the exact 17×700g partial admission: "
            + reserveFailure.Code);

        string committedStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            InoculatedLogItemId,
            token.AcceptedQuantity,
            WorldItemStackState.Stored,
            destinationId);
        Require(
            admission.TryCommit(
                token.TokenId,
                "qa:l02-inoculated-log:commit",
                out WarehouseMassAdmissionReceipt receipt,
                out DomainFailure commitFailure)
            && !commitFailure.IsFailure
            && receipt.CommittedQuantity == 17
            && receipt.CommittedMassGrams == 11_900L
            && inventory.StoredMassGrams == 11_900L
            && inventory.ReservedInboundMassGrams == 0L
            && inventory.RemainingMassGrams == 600L,
            "L02 did not commit the exact 17×700g physical stack: "
            + commitFailure.Code);

        WarehouseMassAdmissionRequest overfill = new(
            warehouseId,
            "qa:l02-inoculated-log:overfill",
            (ItemDefinitionId)InoculatedLogItemId,
            string.Empty,
            "generic:supply:inoculated-log",
            1,
            admission.GetWarehouseCapacityRevision(warehouseId),
            massQuery.AuthorityRevision);
        Require(
            !admission.TryReserve(
                overfill,
                out _,
                out DomainFailure overfillFailure)
            && overfillFailure.Code
                == FailureCode.WarehouseMassCapacityUnavailable
            && inventory.StoredMassGrams == 11_900L
            && inventory.RemainingMassGrams == 600L,
            "L02 accepted a 700g unit into its final 600g or mutated stock on rejection: "
            + overfillFailure.Code);

        WorldItemPersistenceService sourcePersistence = new(
            catalog,
            new FixedHaulingSettings(),
            repository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        DungeonPhysicalItemSaveData saved = sourcePersistence.Capture();
        WorldItemRepository restoredRepository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        WorldItemPersistenceService restoredPersistence = new(
            catalog,
            new FixedHaulingSettings(),
            restoredRepository,
            EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
        restoredPersistence.RestoreForEditorTest(saved);
        PhysicalStockQuery restoredStock = new(
            restoredRepository,
            catalog,
            massQuery);
        WarehouseInventory restoredInventory = new(
            storage.maxStoredMassGrams,
            storage.category,
            restrictCategory: !storage.allCategories);
        restoredInventory.BindPhysicalStock(
            restoredStock,
            warehouseId,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        WorldItemStackSnapshot restoredStack = restoredStock.GetAllStacks()
            .Single(value => string.Equals(
                value.StackId,
                committedStackId,
                StringComparison.Ordinal));
        Require(restoredStack.State == WorldItemStackState.Stored
                && string.Equals(
                    restoredStack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && restoredStack.Quantity == 17
                && restoredInventory.StoredMassGrams == 11_900L
                && restoredInventory.RemainingMassGrams == 600L
                && restoredInventory.GetAcceptableQuantity(
                    InoculatedLogItemId,
                    1) == 0,
            "L02 current-format restore changed stack identity, quantity or derived grams.");
    }

    private static void VerifyWarehouseMassAdmissionLifecycle(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        PhysicalStockQuery stockQuery,
        BuildingInstanceId firstWarehouseId,
        BuildingInstanceId secondWarehouseId,
        WarehouseInventory firstInventory)
    {
        WarehouseInventory secondInventory = new(
            25_000L,
            StockCategory.General,
            restrictCategory: false);
        secondInventory.BindPhysicalStock(
            stockQuery,
            secondWarehouseId,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        TestWarehouseFacility firstFacility = new(
            firstWarehouseId,
            firstInventory);
        TestWarehouseFacility secondFacility = new(
            secondWarehouseId,
            secondInventory);
        TestWarehouseWorldQuery world = new(firstFacility, secondFacility);
        MutableAdmissionClock clock = new();
        WarehouseMassAdmissionService admission = new(
            catalog,
            massQuery,
            stockQuery,
            world,
            clock,
            repository);

        long initialRevision = admission.GetWarehouseCapacityRevision(
            firstWarehouseId);
        WarehouseMassAdmissionRequest partialRequest = CreateAdmissionRequest(
            admission,
            massQuery,
            firstWarehouseId,
            "qa:warehouse-partial",
            requestedQuantity: 20);
        Require(
            admission.TryReserve(
                partialRequest,
                out WarehouseMassAdmissionToken partialToken,
                out DomainFailure partialFailure)
            && partialToken.AcceptedQuantity == 13
            && partialToken.ReservedMassGrams == 15_600L
            && !partialFailure.IsFailure,
            $"Partial gram reservation failed: {partialFailure.Code}; "
            + $"accepted={partialToken.AcceptedQuantity}; "
            + $"mass={partialToken.ReservedMassGrams}.");
        Require(firstInventory.ReservedInboundMassGrams == 15_600L,
            "Warehouse inventory did not project the reserved gram ledger.");
        Require(
            admission.TryRelease(
                partialToken.TokenId,
                WarehouseMassAdmissionReleaseReason.CancelledBeforePickup,
                out DomainFailure partialReleaseFailure)
            && admission.TryRelease(
                partialToken.TokenId,
                WarehouseMassAdmissionReleaseReason.CancelledBeforePickup,
                out DomainFailure repeatedReleaseFailure)
            && !partialReleaseFailure.IsFailure
            && !repeatedReleaseFailure.IsFailure
            && admission.TryGetStatus(
                partialToken.TokenId,
                out WarehouseMassAdmissionStatusSnapshot releasedStatus)
            && releasedStatus.Status == WarehouseMassAdmissionTokenStatus.Released
            && admission.HasOwnerOperationHistory(
                partialRequest.OwnerOperationId)
            && firstInventory.ReservedInboundMassGrams == 0L,
            "Released admission did not retain an idempotent terminal tombstone.");

        WarehouseMassAdmissionRequest commitRequest = CreateAdmissionRequest(
            admission,
            massQuery,
            firstWarehouseId,
            "qa:warehouse-commit",
            requestedQuantity: 2);
        Require(
            admission.TryReserve(
                commitRequest,
                out WarehouseMassAdmissionToken commitToken,
                out DomainFailure commitReserveFailure)
            && !commitReserveFailure.IsFailure,
            $"Commit reservation failed: {commitReserveFailure.Code}.");
        long revisionBeforeUnrelatedMutation =
            admission.GetWarehouseCapacityRevision(firstWarehouseId);
        string unrelatedStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            1,
            WorldItemStackState.Stored,
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
                + secondWarehouseId.Value);
        Require(
            admission.GetWarehouseCapacityRevision(firstWarehouseId)
                == revisionBeforeUnrelatedMutation
            && admission.TryGetStatus(
                commitToken.TokenId,
                out WarehouseMassAdmissionStatusSnapshot stillReserved)
            && stillReserved.Status == WarehouseMassAdmissionTokenStatus.Reserved,
            "An unrelated warehouse mutation invalidated the L01 admission token.");

        string committedStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            commitToken.AcceptedQuantity,
            WorldItemStackState.Stored,
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
                + firstWarehouseId.Value);
        Require(
            admission.TryCommit(
                commitToken.TokenId,
                "qa:warehouse-commit:receipt",
                out WarehouseMassAdmissionReceipt firstReceipt,
                out DomainFailure firstCommitFailure)
            && admission.TryCommit(
                commitToken.TokenId,
                "qa:warehouse-commit:receipt",
                out WarehouseMassAdmissionReceipt repeatedReceipt,
                out DomainFailure repeatedCommitFailure)
            && !firstCommitFailure.IsFailure
            && !repeatedCommitFailure.IsFailure
            && firstReceipt.CommittedQuantity == 2
            && firstReceipt.CommittedMassGrams == 2_400L
            && repeatedReceipt.CommitId == firstReceipt.CommitId
            && admission.HasOwnerOperationHistory(
                commitRequest.OwnerOperationId)
            && firstInventory.ReservedInboundMassGrams == 0L,
            "Admission commit was not exact and idempotent.");

        WarehouseMassAdmissionRequest expiryRequest = CreateAdmissionRequest(
            admission,
            massQuery,
            firstWarehouseId,
            "qa:warehouse-expiry",
            requestedQuantity: 1);
        Require(
            admission.TryReserve(
                expiryRequest,
                out WarehouseMassAdmissionToken expiryToken,
                out DomainFailure expiryReserveFailure)
            && !expiryReserveFailure.IsFailure,
            $"Expiry reservation failed: {expiryReserveFailure.Code}.");
        clock.CurrentTime = 20f;
        _ = admission.GetReservedInboundMassGrams(firstWarehouseId);
        Require(
            admission.TryGetStatus(
                expiryToken.TokenId,
                out WarehouseMassAdmissionStatusSnapshot expiredStatus)
            && expiredStatus.Status == WarehouseMassAdmissionTokenStatus.Expired
            && !admission.TryRenew(
                expiryToken.TokenId,
                admission.GetWarehouseCapacityRevision(firstWarehouseId),
                out _,
                out DomainFailure expiredRenewFailure)
            && expiredRenewFailure.Code
                == FailureCode.WarehouseMassAdmissionTokenExpired,
            "Expired admission did not retain a typed terminal tombstone.");

        clock.CurrentTime = 21f;
        WarehouseMassAdmissionRequest haulRenewalRequest = CreateAdmissionRequest(
            admission,
            massQuery,
            firstWarehouseId,
            "haul:qa:warehouse-renewal-window",
            requestedQuantity: 1);
        Require(
            admission.TryReserve(
                haulRenewalRequest,
                out WarehouseMassAdmissionToken haulRenewalToken,
                out DomainFailure haulReserveFailure)
            && !haulReserveFailure.IsFailure,
            $"Haul renewal reservation failed: {haulReserveFailure.Code}.");
        clock.CurrentTime = 30f;
        Require(
            admission.TryRenew(
                haulRenewalToken.TokenId,
                admission.GetWarehouseCapacityRevision(firstWarehouseId),
                out WarehouseMassAdmissionToken renewedHaulToken,
                out DomainFailure haulRenewFailure)
            && !haulRenewFailure.IsFailure
            && renewedHaulToken.ExpiresAtGameSeconds >= 75d,
            "Haul admission did not renew to the active-haul lease window.");
        clock.CurrentTime = 50f;
        _ = admission.GetReservedInboundMassGrams(firstWarehouseId);
        Require(
            admission.TryGetStatus(
                haulRenewalToken.TokenId,
                out WarehouseMassAdmissionStatusSnapshot liveHaulStatus)
            && liveHaulStatus.Status
                == WarehouseMassAdmissionTokenStatus.Reserved
            && admission.TryRelease(
                haulRenewalToken.TokenId,
                WarehouseMassAdmissionReleaseReason.CancelledBeforePickup,
                out DomainFailure haulReleaseFailure)
            && !haulReleaseFailure.IsFailure,
            "Renewed haul admission expired inside its active-haul window.");

        clock.CurrentTime = 51f;
        WarehouseMassAdmissionRequest invalidatedRequest = CreateAdmissionRequest(
            admission,
            massQuery,
            firstWarehouseId,
            "qa:warehouse-invalidated",
            requestedQuantity: 1);
        Require(
            admission.TryReserve(
                invalidatedRequest,
                out WarehouseMassAdmissionToken invalidatedToken,
                out DomainFailure invalidatedReserveFailure)
            && !invalidatedReserveFailure.IsFailure,
            $"Invalidation reservation failed: {invalidatedReserveFailure.Code}.");
        string externalStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            1,
            WorldItemStackState.Stored,
            WorldItemStackRuntime.WarehouseStorageDestinationPrefix
                + firstWarehouseId.Value);
        _ = admission.GetWarehouseCapacityRevision(firstWarehouseId);
        Require(
            admission.TryGetStatus(
                invalidatedToken.TokenId,
                out WarehouseMassAdmissionStatusSnapshot invalidatedStatus)
            && invalidatedStatus.Status
                == WarehouseMassAdmissionTokenStatus.Invalidated
            && firstInventory.ReservedInboundMassGrams == 0L,
            "External L01 stock mutation did not invalidate its active token.");

        WorldItemRepositoryEditorAccess.RemoveStack(repository, unrelatedStackId);
        WorldItemRepositoryEditorAccess.RemoveStack(repository, committedStackId);
        WorldItemRepositoryEditorAccess.RemoveStack(repository, externalStackId);
        admission.BeginRestoreCandidate();
        admission.PublishRestoreCandidate();
        WarehouseMassAdmissionRequest restoredRequest = CreateAdmissionRequest(
            admission,
            massQuery,
            firstWarehouseId,
            "qa:warehouse-exact-token-restore",
            requestedQuantity: 1);
        const string restoredTokenId = "warehouse-mass:0000000000000042";
        Require(
            admission.TryRestoreReserved(
                restoredTokenId,
                restoredRequest,
                expectedReservedMassGrams: 1_200L,
                out WarehouseMassAdmissionToken restoredToken,
                out DomainFailure restoredFailure)
            && !restoredFailure.IsFailure
            && string.Equals(
                restoredToken.TokenId,
                restoredTokenId,
                StringComparison.Ordinal)
            && restoredToken.ReservedMassGrams == 1_200L
            && admission.TryGetStatus(
                restoredTokenId,
                out WarehouseMassAdmissionStatusSnapshot restoredStatus)
            && restoredStatus.Status == WarehouseMassAdmissionTokenStatus.Reserved,
            "Current-format restore did not preserve the exact warehouse admission token.");
        admission.CompleteRestoreCandidate();
        Require(admission.TryRelease(
                restoredTokenId,
                WarehouseMassAdmissionReleaseReason.CancelledBeforePickup,
                out DomainFailure restoredReleaseFailure)
            && !restoredReleaseFailure.IsFailure,
            "Exact restored admission token could not be released.");
        Require(initialRevision > 0L,
            "Warehouse-local capacity revision was not initialized.");
    }

    private static void VerifyConveyorWarehouseMassAdmission(
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery massQuery,
        PhysicalStockQuery stockQuery)
    {
        BuildingInstanceId firstWarehouseId =
            (BuildingInstanceId)"building:qa-conveyor-mass-a";
        BuildingInstanceId secondWarehouseId =
            (BuildingInstanceId)"building:qa-conveyor-mass-b";
        WarehouseInventory firstInventory = new(
            25_000L,
            StockCategory.General,
            restrictCategory: false);
        firstInventory.BindPhysicalStock(
            stockQuery,
            firstWarehouseId,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        WarehouseInventory secondInventory = new(
            25_000L,
            StockCategory.General,
            restrictCategory: false);
        secondInventory.BindPhysicalStock(
            stockQuery,
            secondWarehouseId,
            CharacterAiEditorTestDependencies.AuthoredGameplay);
        TestWarehouseFacility firstFacility = new(
            firstWarehouseId,
            firstInventory);
        TestWarehouseFacility secondFacility = new(
            secondWarehouseId,
            secondInventory);
        TestWarehouseWorldQuery warehouseWorld = new(
            firstFacility,
            secondFacility);
        WarehouseMassAdmissionService admission = new(
            catalog,
            massQuery,
            stockQuery,
            warehouseWorld,
            new MutableAdmissionClock(),
            repository);
        WorldItemQueryService itemQueries = new(
            catalog,
            massQuery,
            repository,
            EditorNullItemMarkerPresenter.Instance);
        IWorldItemSpawner spawner = new WorldItemSpawner(
            catalog,
            repository,
            EditorNullItemMarkerPresenter.Instance);
        ItemQuantityReservationService quantityReservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            new UnityGameClock());
        FacilityBufferDestinationClaimRegistry destinationClaims = new();
        FacilityBufferPhysicalOccupancyQuery facilityOccupancy = new(
            repository,
            massQuery,
            quantityReservations);
        FacilityBufferMassAdmissionService facilityAdmission = new(
            destinationClaims,
            facilityOccupancy);
        IItemReservationService reservations = new ItemReservationService(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            quantityReservations);
        ICharacterAiWorldRegistry characterWorld =
            CharacterAiEditorTestDependencies.WorldRegistry;
        NoGridSystemProvider gridProvider = new();
        NoCharacterIdRegistry characterIds = new();
        WorldItemWarehouseService warehouseService = new(
            catalog,
            repository,
            characterWorld,
            spawner,
            EditorNullItemMarkerPresenter.Instance,
            gridProvider,
            characterIds,
            reservations,
            quantityReservations,
            admission);
        IBufferStackAggregationService aggregation =
            new BufferStackAggregationService(
                catalog,
                repository,
                EditorNullItemMarkerPresenter.Instance,
                quantityReservations,
                quantityReservations);
        IGameContentCatalog content =
            CharacterAiEditorTestDependencies.ContentDefinitions
                as IGameContentCatalog
            ?? throw new InvalidOperationException(
                "Editor content authority does not expose the game-content catalog.");
        ItemTransferService transfers = new(
            new WorldItemReadServices(
                catalog,
                massQuery,
                new FixedHaulingSettings(),
                itemQueries,
                EditorNullItemMarkerPresenter.Instance,
                new EditorCharacterAiPerformanceRecorder(),
                DisabledDungeonDebugRuleQuery.Instance,
                new FacilityOutputClearanceTelemetryRuntime()),
            characterIds,
            gridProvider,
            characterWorld,
            destinationClaims,
            new ResourceCombatEquipmentCatalog(content),
            new GameEventBus(),
            repository,
            spawner,
            warehouseService,
            quantityReservations,
            quantityReservations,
            aggregation,
            admission,
            facilityBufferMassAdmission: facilityAdmission);

        const string rejectedOwner = "conveyor-payload:qa-mass-rejected";
        string rejectedStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            21,
            WorldItemStackState.Loose,
            position: new Vector2Int(2, 2));
        long firstMassBefore = firstInventory.StoredMassGrams;
        Require(
            transfers.TryBeginTransit(
                (ItemStackId)rejectedStackId,
                new Vector2Int(2, 2),
                rejectedOwner,
                out _,
                out DomainFailure rejectedBeginFailure),
            $"Conveyor rejection fixture did not enter transit: {rejectedBeginFailure.Code}.");
        Require(
            !transfers.TryCompleteTransitToWarehouse(
                (ItemStackId)rejectedStackId,
                rejectedOwner,
                firstFacility,
                out _,
                out DomainFailure rejectedFailure)
            && rejectedFailure.Code == FailureCode.WarehouseMassCapacityUnavailable
            && transfers.TryGetTransitStack(
                (ItemStackId)rejectedStackId,
                rejectedOwner,
                out ItemTransitStackSnapshot rejectedTransit)
            && rejectedTransit.Quantity == 21
            && firstInventory.StoredMassGrams == firstMassBefore
            && firstInventory.ReservedInboundMassGrams == 0L,
            "A count-compatible but gram-incompatible conveyor payload was not "
            + $"rejected atomically: {rejectedFailure.Code}.");

        const string acceptedOwner = "conveyor-payload:qa-mass-accepted";
        string acceptedStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            2,
            WorldItemStackState.Loose,
            position: new Vector2Int(3, 2));
        long secondMassBefore = secondInventory.StoredMassGrams;
        Require(
            transfers.TryBeginTransit(
                (ItemStackId)acceptedStackId,
                new Vector2Int(3, 2),
                acceptedOwner,
                out _,
                out DomainFailure acceptedBeginFailure),
            $"Conveyor acceptance fixture did not enter transit: {acceptedBeginFailure.Code}.");
        Require(
            transfers.TryCompleteTransitToWarehouse(
                (ItemStackId)acceptedStackId,
                acceptedOwner,
                secondFacility,
                out WarehouseMassAdmissionReceipt receipt,
                out DomainFailure acceptedFailure)
            && !acceptedFailure.IsFailure
            && receipt.CommittedQuantity == 2
            && receipt.CommittedMassGrams == 2_400L
            && secondInventory.StoredMassGrams == secondMassBefore + 2_400L
            && secondInventory.ReservedInboundMassGrams == 0L
            && stockQuery.GetAllStacks().Any(stack =>
                string.Equals(
                    stack.StackId,
                    acceptedStackId,
                    StringComparison.Ordinal)
                && stack.State == WorldItemStackState.Stored
                && string.Equals(
                    stack.DestinationId,
                    WorldItemStackRuntime.WarehouseStorageDestinationPrefix
                        + secondWarehouseId.Value,
                    StringComparison.Ordinal)),
            "A gram-compatible conveyor payload did not commit exact physical "
            + $"warehouse mass: {acceptedFailure.Code}.");

        const string FacilityDestination =
            "conveyor-facility-buffer:qa-mass";
        const string FacilityOwnerDomain = "infrastructure.conveyor";
        const string FacilityOwnerOperation =
            "conveyor-facility-owner:qa-mass";
        const string FacilityOwnerId = "building:qa-conveyor-port";
        Vector2Int facilityPosition = new(8, 3);
        FacilityBufferDestinationClaim facilityClaim = new(
            FacilityDestination,
            facilityPosition,
            FacilityOwnerDomain,
            FacilityOwnerOperation,
            FacilityOwnerId,
            FacilityBufferDestinationAnchorKind.LiveBuilding);
        FacilityBufferCapacityProfile facilityProfile = new(
            FacilityDestination,
            facilityPosition,
            FacilityOwnerDomain,
            FacilityOwnerOperation,
            FacilityOwnerId,
            maxMass: new PhysicalMassGrams(3_600L),
            capacityRevision: 1L);
        Require(
            destinationClaims.TryClaim(facilityClaim, out _, out _)
            && facilityAdmission.TryReplaceOwnedProfiles(
                FacilityOwnerDomain,
                new[] { facilityProfile },
                out _,
                out _),
            "Conveyor facility-buffer claim/profile fixture did not publish.");

        const string MissingProfileOwner =
            "conveyor-payload:qa-facility-missing-profile";
        string missingProfileStackId =
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                LumberItemId,
                1,
                WorldItemStackState.Loose,
                position: new Vector2Int(4, 3));
        Require(
            transfers.TryBeginTransit(
                (ItemStackId)missingProfileStackId,
                new Vector2Int(4, 3),
                MissingProfileOwner,
                out _,
                out _)
            && !transfers.TryCompleteTransitToFacilityBuffer(
                (ItemStackId)missingProfileStackId,
                MissingProfileOwner,
                new Vector2Int(9, 3),
                "conveyor-facility-buffer:qa-missing",
                out _,
                out DomainFailure missingProfileFailure)
            && missingProfileFailure.Code
                == FailureCode.ConveyorDestinationUnavailable
            && transfers.TryGetTransitStack(
                (ItemStackId)missingProfileStackId,
                MissingProfileOwner,
                out _),
            "A conveyor payload entered a facility buffer without an exact "
            + "claim/profile admission authority.");

        const string OvermassOwner =
            "conveyor-payload:qa-facility-overmass";
        string overmassStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            4,
            WorldItemStackState.Loose,
            position: new Vector2Int(5, 3));
        Require(
            transfers.TryBeginTransit(
                (ItemStackId)overmassStackId,
                new Vector2Int(5, 3),
                OvermassOwner,
                out _,
                out _)
            && !transfers.TryCompleteTransitToFacilityBuffer(
                (ItemStackId)overmassStackId,
                OvermassOwner,
                facilityPosition,
                FacilityDestination,
                out _,
                out DomainFailure overmassFailure)
            && overmassFailure.Code == FailureCode.ConveyorPortFull
            && transfers.TryGetTransitStack(
                (ItemStackId)overmassStackId,
                OvermassOwner,
                out _)
            && facilityAdmission.TryGetCapacity(
                FacilityDestination,
                facilityPosition,
                out FacilityBufferMassCapacitySnapshot afterOvermass)
            && afterOvermass.ReservedMassGrams == 0L,
            "An overmass conveyor payload was not rejected atomically by the "
            + "facility-buffer admission authority.");

        const string BypassOwner =
            "conveyor-payload:qa-facility-bypass";
        string bypassStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            1,
            WorldItemStackState.Loose,
            position: new Vector2Int(6, 3));
        Require(
            transfers.TryBeginTransit(
                (ItemStackId)bypassStackId,
                new Vector2Int(6, 3),
                BypassOwner,
                out _,
                out _)
            && !transfers.TryCompleteTransit(
                (ItemStackId)bypassStackId,
                BypassOwner,
                WorldItemStackState.FacilityBuffer,
                facilityPosition,
                FacilityDestination,
                out DomainFailure bypassFailure)
            && bypassFailure.Code
                == FailureCode.ConveyorDestinationUnavailable
            && transfers.TryGetTransitStack(
                (ItemStackId)bypassStackId,
                BypassOwner,
                out _),
            "The generic transit completion API still bypassed exact "
            + "facility-buffer admission.");

        const string FaultOwner = "conveyor-payload:qa-facility-fault";
        string faultStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            LumberItemId,
            1,
            WorldItemStackState.Loose,
            position: new Vector2Int(7, 3));
        Require(
            transfers.TryBeginTransit(
                (ItemStackId)faultStackId,
                new Vector2Int(7, 3),
                FaultOwner,
                out _,
                out _),
            "The facility-buffer commit-fault payload did not enter transit.");
        WorldItemStackSnapshot faultBefore = stockQuery.GetAllStacks().Single(
            stack => string.Equals(
                stack.StackId,
                faultStackId,
                StringComparison.Ordinal));
        long faultMassBefore = massQuery.GetQuantityMass(
            (ItemDefinitionId)faultBefore.ItemId,
            PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                (ItemDefinitionId)faultBefore.ItemId,
                faultBefore.ItemInstanceId,
                faultBefore.Components),
            faultBefore.Quantity).Value;
        transfers.DebugFailBeforeFacilityTransitAdmissionCommit = () => true;
        bool faultRejected =
            !transfers.TryCompleteTransitToFacilityBuffer(
                (ItemStackId)faultStackId,
                FaultOwner,
                facilityPosition,
                FacilityDestination,
                out _,
                out DomainFailure faultFailure);
        transfers.DebugFailBeforeFacilityTransitAdmissionCommit = null;
        WorldItemStackSnapshot faultAfter = stockQuery.GetAllStacks().Single(
            stack => string.Equals(
                stack.StackId,
                faultStackId,
                StringComparison.Ordinal));
        long faultMassAfter = massQuery.GetQuantityMass(
            (ItemDefinitionId)faultAfter.ItemId,
            PhysicalItemMassSubjectAdapter.Create(
                massQuery,
                (ItemDefinitionId)faultAfter.ItemId,
                faultAfter.ItemInstanceId,
                faultAfter.Components),
            faultAfter.Quantity).Value;
        Require(
            faultRejected
            && faultFailure.Code == FailureCode.ConveyorDestinationUnavailable
            && faultAfter.State == faultBefore.State
            && faultAfter.Position == faultBefore.Position
            && string.Equals(
                faultAfter.DestinationId,
                faultBefore.DestinationId,
                StringComparison.Ordinal)
            && string.Equals(
                faultAfter.SourceStorageDestinationId,
                faultBefore.SourceStorageDestinationId,
                StringComparison.Ordinal)
            && faultAfter.HasDestinationPosition
                == faultBefore.HasDestinationPosition
            && faultAfter.DestinationPosition
                == faultBefore.DestinationPosition
            && string.Equals(
                faultAfter.ReservedByPersistentId,
                faultBefore.ReservedByPersistentId,
                StringComparison.Ordinal)
            && faultAfter.ReservedQuantity == faultBefore.ReservedQuantity
            && faultAfter.ReservationRevision
                == faultBefore.ReservationRevision
            && faultAfter.Quantity == faultBefore.Quantity
            && string.Equals(
                faultAfter.ItemInstanceId,
                faultBefore.ItemInstanceId,
                StringComparison.Ordinal)
            && string.Equals(
                faultAfter.ReservationSignature,
                faultBefore.ReservationSignature,
                StringComparison.Ordinal)
            && faultMassAfter == faultMassBefore
            && facilityAdmission.TryGetCapacity(
                FacilityDestination,
                facilityPosition,
                out FacilityBufferMassCapacitySnapshot afterFault)
            && afterFault.ReservedMassGrams == 0L,
            "A failed conveyor facility-buffer commit leaked mass admission "
            + "or mutated the in-transit lot.");

        const string FacilityAcceptedOwner =
            "conveyor-payload:qa-facility-accepted";
        string facilityAcceptedStackId =
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                LumberItemId,
                2,
                WorldItemStackState.Loose,
                position: new Vector2Int(7, 4));
        Require(
            transfers.TryBeginTransit(
                (ItemStackId)facilityAcceptedStackId,
                new Vector2Int(7, 4),
                FacilityAcceptedOwner,
                out _,
                out _)
            && transfers.TryCompleteTransitToFacilityBuffer(
                (ItemStackId)facilityAcceptedStackId,
                FacilityAcceptedOwner,
                facilityPosition,
                FacilityDestination,
                out FacilityBufferMassAdmissionReceipt facilityReceipt,
                out DomainFailure facilityFailure)
            && !facilityFailure.IsFailure
            && facilityReceipt.CommittedMassGrams == 2_400L
            && stockQuery.GetAllStacks().Any(stack =>
                string.Equals(
                    stack.StackId,
                    facilityAcceptedStackId,
                    StringComparison.Ordinal)
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    FacilityDestination,
                    StringComparison.Ordinal))
            && facilityAdmission.TryGetCapacity(
                FacilityDestination,
                facilityPosition,
                out FacilityBufferMassCapacitySnapshot afterAccepted)
            && afterAccepted.ReservedMassGrams == 0L,
            "A valid conveyor facility-buffer arrival did not commit its exact "
            + "physical lot and gram receipt.");
        Require(
            facilityOccupancy.Capture(FacilityDestination).TotalMassGrams
                == 2_400L
            && !transfers.TryCompleteTransitToFacilityBuffer(
                (ItemStackId)facilityAcceptedStackId,
                FacilityAcceptedOwner,
                facilityPosition,
                FacilityDestination,
                out _,
                out DomainFailure replayFailure)
            && replayFailure.Code == FailureCode.ConveyorDestinationUnavailable
            && stockQuery.GetAllStacks().Count(stack =>
                string.Equals(
                    stack.StackId,
                    facilityAcceptedStackId,
                    StringComparison.Ordinal)) == 1,
            "Conveyor facility-buffer completion was not exact-once.");

        WorldItemRepositoryEditorAccess.RemoveStack(repository, rejectedStackId);
        WorldItemRepositoryEditorAccess.RemoveStack(repository, acceptedStackId);
        WorldItemRepositoryEditorAccess.RemoveStack(
            repository,
            missingProfileStackId);
        WorldItemRepositoryEditorAccess.RemoveStack(repository, overmassStackId);
        WorldItemRepositoryEditorAccess.RemoveStack(repository, bypassStackId);
        WorldItemRepositoryEditorAccess.RemoveStack(repository, faultStackId);
        WorldItemRepositoryEditorAccess.RemoveStack(
            repository,
            facilityAcceptedStackId);
    }

    private static WarehouseMassAdmissionRequest CreateAdmissionRequest(
        IWarehouseMassAdmissionService admission,
        IPhysicalItemMassQuery massQuery,
        BuildingInstanceId warehouseId,
        string operationId,
        int requestedQuantity) =>
        new(
            warehouseId,
            operationId,
            (ItemDefinitionId)LumberItemId,
            string.Empty,
            "generic:material:lumber",
            requestedQuantity,
            admission.GetWarehouseCapacityRevision(warehouseId),
            massQuery.AuthorityRevision,
            expectedSourceRevision: 0L);

    private sealed class TestWarehouseFacility : IWarehouseFacility
    {
        internal TestWarehouseFacility(
            BuildingInstanceId id,
            WarehouseInventory inventory)
        {
            PersistentInstanceId = id;
            Inventory = inventory;
        }

        public BuildingInstanceId PersistentInstanceId { get; }
        public WarehouseInventory Inventory { get; }
        public bool HasWarehouseInventory => true;
    }

    private sealed class TestWarehouseWorldQuery : IWarehouseWorldQuery
    {
        private readonly IReadOnlyList<IWarehouseFacility> warehouses;

        internal TestWarehouseWorldQuery(params IWarehouseFacility[] warehouses)
        {
            this.warehouses = warehouses;
        }

        public int WarehouseVersion => 1;
        public IReadOnlyList<IWarehouseFacility> Warehouses => warehouses;
    }

    private sealed class MutableAdmissionClock : IGameClock
    {
        public float CurrentTime { get; set; }
        public float DeltaTime => 0f;
        public float Time => CurrentTime;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class NoGridSystemProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
    }

    private sealed class NoCharacterIdRegistry : ICharacterIdRegistry
    {
        public bool TryGetPersistentId(
            CharacterActor actor,
            out string persistentId)
        {
            persistentId = string.Empty;
            return false;
        }

        public string GetOrAssignPersistentId(CharacterActor actor) =>
            throw new InvalidOperationException(
                "The conveyor mass fixture does not own character identity.");
    }

    private sealed class FixedHaulingSettings : IItemHaulingSettingsProvider
    {
        public float MaxCarryMultiplier =>
            CharacterCarryTuning.DefaultMaxCarryMultiplier;

        public ItemHaulingSettingsSnapshot Capture() => new()
        {
            maxCarryMultiplier = MaxCarryMultiplier
        };

        public void Restore(ItemHaulingSettingsSnapshot snapshot)
        {
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void RequireThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void VerifyMassQueryPerformance(
        IPhysicalItemMassQuery massQuery)
    {
        const int OperationsPerSample = 10_000;
        const int WarmupSamples = 10;
        const int MeasuredSamples = 100;
        ItemDefinitionId itemId = (ItemDefinitionId)LumberItemId;
        long checksum = 0L;

        for (int sample = 0; sample < WarmupSamples; sample++)
        {
            for (int operation = 0; operation < OperationsPerSample; operation++)
            {
                checksum ^= massQuery.GetDefinitionUnitMass(itemId).Value;
            }
        }

        long[] elapsedTicks = new long[MeasuredSamples];
        long peakAllocatedBytes = 0L;
        for (int sample = 0; sample < MeasuredSamples; sample++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int operation = 0; operation < OperationsPerSample; operation++)
            {
                checksum ^= massQuery.GetDefinitionUnitMass(itemId).Value;
            }

            elapsedTicks[sample] =
                System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            peakAllocatedBytes = Math.Max(peakAllocatedBytes, allocated);
        }

        Array.Sort(elapsedTicks);
        int p95Index = (int)Math.Ceiling(MeasuredSamples * 0.95d) - 1;
        double p95Milliseconds = elapsedTicks[p95Index]
            * 1000d
            / System.Diagnostics.Stopwatch.Frequency;
        Require(
            p95Milliseconds <= 2d,
            $"Mass query p95 exceeded 2ms: {p95Milliseconds:F4}ms/10,000 ops.");
        Require(
            peakAllocatedBytes == 0L,
            $"Mass query allocated {peakAllocatedBytes}B in a measured sample.");
        GC.KeepAlive(checksum);
    }

    private static void VerifyWarehouseMassQueryPerformance(
        WarehouseInventory inventory)
    {
        const int OperationsPerSample = 10_000;
        const int WarmupSamples = 10;
        const int MeasuredSamples = 100;
        long checksum = 0L;
        _ = inventory.StoredMassGrams;

        for (int sample = 0; sample < WarmupSamples; sample++)
        {
            for (int operation = 0; operation < OperationsPerSample; operation++)
            {
                checksum ^= inventory.StoredMassGrams;
            }
        }

        long[] elapsedTicks = new long[MeasuredSamples];
        long peakAllocatedBytes = 0L;
        for (int sample = 0; sample < MeasuredSamples; sample++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int operation = 0; operation < OperationsPerSample; operation++)
            {
                checksum ^= inventory.StoredMassGrams;
            }

            elapsedTicks[sample] =
                System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;
            peakAllocatedBytes = Math.Max(
                peakAllocatedBytes,
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
        }

        Array.Sort(elapsedTicks);
        int p95Index = (int)Math.Ceiling(MeasuredSamples * 0.95d) - 1;
        double p95Milliseconds = elapsedTicks[p95Index]
            * 1000d
            / System.Diagnostics.Stopwatch.Frequency;
        Require(p95Milliseconds <= 2d,
            $"Warehouse mass query p95 exceeded 2ms: "
            + $"{p95Milliseconds:F4}ms/10,000 ops.");
        Require(peakAllocatedBytes == 0L,
            $"Warehouse mass query allocated {peakAllocatedBytes}B in a measured sample.");
        GC.KeepAlive(checksum);
    }

    private static void VerifyCombatEquipmentMassQueryPerformance(
        IPhysicalItemMassQuery massQuery,
        PhysicalItemMassSubject subject)
    {
        const int OperationsPerSample = 10_000;
        const int WarmupSamples = 10;
        const int MeasuredSamples = 100;
        long checksum = 0L;

        for (int sample = 0; sample < WarmupSamples; sample++)
        {
            for (int operation = 0; operation < OperationsPerSample; operation++)
            {
                checksum ^= massQuery.GetPreparedStackUnitMass(subject).Value;
            }
        }

        long[] elapsedTicks = new long[MeasuredSamples];
        long peakAllocatedBytes = 0L;
        for (int sample = 0; sample < MeasuredSamples; sample++)
        {
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int operation = 0; operation < OperationsPerSample; operation++)
            {
                checksum ^= massQuery.GetPreparedStackUnitMass(subject).Value;
            }

            elapsedTicks[sample] =
                System.Diagnostics.Stopwatch.GetTimestamp() - startedAt;
            peakAllocatedBytes = Math.Max(
                peakAllocatedBytes,
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
        }

        Array.Sort(elapsedTicks);
        int p95Index = (int)Math.Ceiling(MeasuredSamples * 0.95d) - 1;
        double p95Milliseconds = elapsedTicks[p95Index]
            * 1000d
            / System.Diagnostics.Stopwatch.Frequency;
        Require(
            p95Milliseconds <= 2d,
            $"Combat-equipment mass query p95 exceeded 2ms: "
            + $"{p95Milliseconds:F4}ms/10,000 ops.");
        Require(
            peakAllocatedBytes == 0L,
            $"Combat-equipment mass query allocated {peakAllocatedBytes}B "
            + "in a measured sample.");
        GC.KeepAlive(checksum);
    }

    private static void VerifyProjectorComposition(
        IDungeonItemCatalogProvider catalog)
    {
        ContainerBuilder builder = new ContainerBuilder();
        builder.RegisterInstance(catalog).As<IDungeonItemCatalogProvider>();
        builder.Register<GenericDefinitionPhysicalItemMassProjector>(
                Lifetime.Singleton)
            .As<IPhysicalItemMassProjector>()
            .As<IPhysicalItemDefinitionMassProjector>();
        builder.Register<CombatEquipmentPhysicalItemMassProjector>(
                Lifetime.Singleton)
            .As<IPhysicalItemMassProjector>();
        builder.Register<ApparelPhysicalItemMassProjector>(Lifetime.Singleton)
            .As<IPhysicalItemMassProjector>();
        builder.RegisterInstance(CreateWildlifeSpeciesCatalog())
            .As<IWildlifeSpeciesCatalogProvider>();
        builder.Register<WildlifeCarcassPhysicalItemMassProjector>(
                Lifetime.Singleton)
            .As<IPhysicalItemMassProjector>();
        builder.Register<PackagedLotPhysicalItemMassProjector>(Lifetime.Singleton)
            .As<IPhysicalItemMassProjector>();
        builder.Register<PhysicalItemMassQuery>(Lifetime.Singleton)
            .As<IPhysicalItemMassQuery>();
        IObjectResolver resolver = builder.Build();
        try
        {
            IPhysicalItemMassQuery resolved =
                resolver.Resolve<IPhysicalItemMassQuery>();
            Require(
                resolved.GetDefinitionUnitMass(
                    (ItemDefinitionId)LumberItemId).Value == 1200L,
                "VContainer projector composition did not preserve exact grams.");
        }
        finally
        {
            (resolver as IDisposable)?.Dispose();
        }
    }

    private sealed class FixedItemCatalogProvider : IDungeonItemCatalogProvider
    {
        private readonly Dictionary<string, DungeonItemDefinition> definitions;

        internal FixedItemCatalogProvider(params DungeonItemDefinition[] definitions)
        {
            this.definitions = definitions.ToDictionary(
                definition => definition.ItemId,
                StringComparer.Ordinal);
        }

        public IReadOnlyList<DungeonItemDefinition> All =>
            definitions.Values.OrderBy(
                definition => definition.ItemId,
                StringComparer.Ordinal).ToArray();

        public DungeonItemDefinition GetDefinition(string itemId) =>
            TryGetDefinition(itemId, out DungeonItemDefinition definition)
                ? definition
                : throw new KeyNotFoundException(itemId);

        public bool TryGetDefinition(
            string itemId,
            out DungeonItemDefinition definition) =>
            definitions.TryGetValue(itemId ?? string.Empty, out definition);
    }

    private sealed class RecordingPackagedLotTareOutputGateway :
        IPackagedLotTareOutputGateway
    {
        private readonly List<WorldItemStackSnapshot> stacks = new();
        private int nextStackId = 1;

        internal int SpawnCalls { get; private set; }

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => stacks;

        public bool SpawnOutput(
            string itemId,
            int quantity,
            Vector2Int position,
            IReadOnlyList<ItemInstanceComponentSaveData> components,
            out int spawned)
        {
            SpawnCalls++;
            stacks.Add(new WorldItemStackSnapshot
            {
                StackId = $"qa:tare:{nextStackId++:D4}",
                ItemId = itemId,
                Quantity = quantity,
                State = WorldItemStackState.Loose,
                Position = position,
                Components = components?.ToArray()
                    ?? Array.Empty<ItemInstanceComponentSaveData>()
            });
            spawned = quantity;
            return true;
        }
    }
}
#endif
