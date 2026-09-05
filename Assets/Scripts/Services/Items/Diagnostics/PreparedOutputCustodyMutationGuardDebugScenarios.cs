#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class PreparedOutputCustodyMutationGuardDebugScenarios
{
    private const string ItemId = "material:lumber";
    private const string FoodItemId = "food:grain-porridge";
    private const string TextileItemId = "resource:wool";

    [MenuItem("DungeonStory/Debug/Items/Run Prepared Output Custody Mutation Guards")]
    public static void RunAll()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(catalog);
        VerifyBatchDispositionGuards(mass);
        VerifyRelocationAndSpawnerGuards(catalog, mass);
        VerifyTextileCompactionGuard(catalog);
        VerifyTheftGuard(catalog, mass);
        Debug.Log("Prepared-output custody mutation guards PASS.");
    }

    public static void RunFreshnessMutationGuard(
        WorldItemStackRuntime runtime,
        WorldItemRepository repository)
    {
        if (runtime == null)
            throw new ArgumentNullException(nameof(runtime));
        if (repository == null)
            throw new ArgumentNullException(nameof(repository));

        WorldItemStackRecord aged = AddFreshnessCustodyStack(
            repository,
            "aged",
            new[] { FoodFreshnessComponentCodec.Create(300d, false) });
        string custodyBefore = CaptureCustodyComponent(aged);
        string routeSignatureBefore = CaptureRouteBusinessSignature(aged);
        string physicalIdentityBefore = CapturePhysicalIdentity(aged);
        int recordCountBefore = repository.Records.Count;
        Require(runtime.TrySetFoodFreshness(
                aged.stackId,
                119.375d,
                false,
                out string agingFailure)
            && agingFailure.Length == 0
            && FoodFreshnessComponentCodec.TryRead(
                aged.components,
                out double remainingSeconds,
                out bool preserved)
            && remainingSeconds == 119.375d
            && !preserved
            && repository.Records.Count == recordCountBefore
            && CapturePhysicalIdentity(aged) == physicalIdentityBefore
            && CaptureCustodyComponent(aged) == custodyBefore
            && CaptureRouteBusinessSignature(aged) == routeSignatureBefore,
            "Strict freshness aging changed custody or identity: "
                + agingFailure);

        RequireRejectedFreshnessMutation(runtime, aged, 120d, false,
            "food-freshness-custody-mutation-widened");
        RequireRejectedFreshnessMutation(runtime, aged, 119.375d, true,
            "food-freshness-custody-mutation-widened");

        string genericMutationBefore = CaptureFreshnessRecord(aged);
        Require(!runtime.TrySetInstanceComponent(
                aged.stackId,
                new ItemInstanceComponentSaveData
                {
                    componentTypeId = "item-state:qa-custody-drift",
                    schemaVersion = 1,
                    affectsStacking = true,
                    values = new List<ItemStateValueSaveData>()
                })
            && CaptureFreshnessRecord(aged) == genericMutationBefore,
            "Generic component mutation changed protected freshness custody.");

        RequireRejectedFreshnessMutation(runtime,
            AddFreshnessCustodyStack(
                repository,
                "missing",
                Array.Empty<ItemInstanceComponentSaveData>()),
            100d,
            false,
            "food-freshness-custody-requires-initial-component");

        ItemInstanceComponentSaveData malformed =
            FoodFreshnessComponentCodec.Create(300d, false);
        malformed.values.Add(new ItemStateValueSaveData
        {
            key = "unexpected",
            kind = ItemStateValueKind.Integer,
            integerValue = 1L
        });
        RequireRejectedFreshnessMutation(runtime,
            AddFreshnessCustodyStack(repository, "malformed", new[] { malformed }),
            100d,
            false,
            "food-freshness-component-invalid");
        RequireRejectedFreshnessMutation(runtime,
            AddFreshnessCustodyStack(
                repository,
                "duplicate",
                new[]
                {
                    FoodFreshnessComponentCodec.Create(300d, false),
                    FoodFreshnessComponentCodec.Create(300d, false)
                }),
            100d,
            false,
            "food-freshness-component-invalid");
    }

    private static WorldItemStackRecord AddFreshnessCustodyStack(
        WorldItemRepository repository,
        string suffix,
        IReadOnlyList<ItemInstanceComponentSaveData> freshness)
    {
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            FoodItemId,
            2,
            WorldItemStackState.Loose,
            destinationId: "warehouse:qa:food",
            position: new Vector2Int(4, 5),
            components: freshness);
        WorldItemStackRecord record = repository.RecordsById[stackId];
        string routeSignature = FacilityBufferPlannedOutputPublicationService
            .CreateRuntimeComponentSignature(record.components);
        record.components.Add(
            FacilityOutputExactRouteEditorTestFactory.CreateRoutableCustody(
                "batch:qa:freshness:" + suffix,
                new string('1', 64),
                new string('2', 64),
                "output:qa:freshness",
                "line:qa:freshness:" + suffix,
                0,
                1,
                2,
                1_200L,
                FoodItemId,
                routeSignature,
                new string('a', 64),
                "production:qa:freshness-output",
                "warehouse:qa:food",
                stackId,
                stackId,
                new Vector2Int(4, 5),
                0,
                2,
                1_200L,
                "route:qa:freshness:" + suffix,
                new string('c', 64),
                new string('d', 64),
                0L,
                new string('e', 64),
                "warehouse:qa:food",
                new Vector2Int(9, 9)));
        repository.MarkChanged();
        Require(FacilityOutputExactRouteCustodyCodec.TryRead(
                record.components,
                out _),
            "Freshness mutation fixture did not create canonical route custody.");
        return record;
    }

    private static void RequireRejectedFreshnessMutation(
        WorldItemStackRuntime runtime,
        WorldItemStackRecord record,
        double remainingSeconds,
        bool preserved,
        string expectedFailure)
    {
        string before = CaptureFreshnessRecord(record);
        Require(!runtime.TrySetFoodFreshness(
                record.stackId,
                remainingSeconds,
                preserved,
                out string failureReason)
            && failureReason == expectedFailure
            && CaptureFreshnessRecord(record) == before,
            "Freshness rejection drifted. expected=" + expectedFailure
                + ";actual=" + failureReason);
    }

    private static string CaptureRouteBusinessSignature(
        WorldItemStackRecord record) =>
        FacilityBufferPlannedOutputPublicationService
            .CreateRuntimeComponentSignature(record.components.Where(component =>
                !FacilityOutputExactRouteCustodyCodec.IsCustody(component)));

    private static string CaptureCustodyComponent(
        WorldItemStackRecord record) => record.components
        .Single(FacilityOutputExactRouteCustodyCodec.IsCustody)
        .ToCanonicalString();

    private static string CapturePhysicalIdentity(WorldItemStackRecord record) =>
        string.Join("|", record.stackId, record.itemInstanceId, record.itemId,
            record.quantity, record.state, record.position.x, record.position.y,
            record.destinationId, record.hasDestinationPosition,
            record.destinationPosition.x, record.destinationPosition.y);

    private static string CaptureFreshnessRecord(WorldItemStackRecord record) =>
        CapturePhysicalIdentity(record) + "|" + string.Join(";",
            record.components.Where(component => component != null)
                .Select(component => component.ToCanonicalString())
                .OrderBy(value => value, StringComparer.Ordinal));

    private static void VerifyBatchDispositionGuards(
        IPhysicalItemMassQuery mass)
    {
        WorldItemRepository repository = CreateRepository();
        MutableClock clock = new();
        ItemQuantityReservationService reservations = new(
            repository,
            EditorNullItemMarkerPresenter.Instance,
            clock);
        PhysicalItemBatchDispositionService service = new(
            repository,
            mass,
            EditorNullItemMarkerPresenter.Instance,
            reservations);
        IReadOnlyList<ItemInstanceComponentSaveData> custody = CreateCustody();

        string reservedStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            2,
            WorldItemStackState.FacilityBuffer,
            destinationId: "production:qa:input",
            components: custody);
        WorldItemStackRecord reservedRecord = repository.RecordsById[reservedStackId];
        string signatureBefore = ItemStackSignature.Create(
            reservedRecord.itemId,
            reservedRecord.components);
        Require(reservations.TryReserve(
                "qa:custody:reserved",
                "character:qa",
                ItemReservationPurpose.ProductionInput,
                "production:qa:input",
                new ItemQuantityReservationRequest(
                    (ItemStackId)reservedStackId,
                    1,
                    ItemReservationSignature.Create(
                        reservedRecord.itemId,
                        reservedRecord.components)),
                out ItemQuantityLease lease,
                out DomainFailure reserveFailure),
            "Custody reservation fixture failed: " + reserveFailure);
        Require(!service.TryCommitReservedSinkPending(
                lease.leaseId,
                1,
                "qa:custody:reserved",
                "qa-custody-sink",
                out PhysicalItemBatchDispositionReceipt reservedReceipt,
                out string reservedFailure)
            && !reservedReceipt.IsCommitted
            && reservedFailure.Contains("prepared-output-route-protected",
                StringComparison.Ordinal)
            && repository.GetEditorTestQuantity(reservedStackId) == 2
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && reservations.Revalidate(lease.leaseId, out ItemQuantityLease preserved,
                out _)
            && preserved.remainingQuantity == 1
            && ItemStackSignature.Create(
                reservedRecord.itemId,
                reservedRecord.components) == signatureBefore,
            "Reserved Sink mutated custody quantity, components, lease, or receipt.");

        string pendingStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            2,
            WorldItemStackState.Loose,
            components: custody);
        Require(!service.TryCommitPending(
                new[] { new PhysicalItemTransformInput(pendingStackId, 1) },
                PhysicalItemDispositionKind.Transfer,
                "qa:custody:pending",
                "qa-custody-transfer",
                out PhysicalItemBatchDispositionReceipt pendingReceipt,
                out string pendingFailure)
            && !pendingReceipt.IsCommitted
            && pendingFailure.Contains("prepared-output-route-protected",
                StringComparison.Ordinal)
            && repository.GetEditorTestQuantity(pendingStackId) == 2
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "Pending Transfer mutated protected custody.");

        string carriedStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            1,
            WorldItemStackState.Carried,
            destinationId: "character:qa",
            components: custody);
        Require(!service.TryCommitCarriedSinkPending(
                carriedStackId,
                1,
                "qa:custody:carried",
                "qa-custody-carried-sink",
                out PhysicalItemBatchDispositionReceipt carriedReceipt,
                out string carriedFailure)
            && !carriedReceipt.IsCommitted
            && carriedFailure.Contains("prepared-output-route-protected",
                StringComparison.Ordinal)
            && repository.GetEditorTestQuantity(carriedStackId) == 1,
            "Carried Sink mutated protected custody.");
    }

    private static void VerifyRelocationAndSpawnerGuards(
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery mass)
    {
        WorldItemRepository repository = CreateRepository();
        WorldItemSpawner spawner = new(
            catalog,
            repository,
            EditorNullItemMarkerPresenter.Instance);
        IReadOnlyList<ItemInstanceComponentSaveData> custody = CreateCustody();
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            2,
            WorldItemStackState.Loose,
            position: new Vector2Int(2, 3),
            components: custody);
        WorldItemStackRecord source = repository.RecordsById[stackId];
        string signature = ItemStackSignature.Create(source.itemId, source.components);
        PhysicalItemRelocationService relocation = new(
            repository,
            spawner,
            mass,
            EditorNullItemMarkerPresenter.Instance);

        Require(!relocation.TryRelocateQuantity(
                stackId,
                2,
                new Vector2Int(9, 9),
                WorldItemStackState.Stored,
                "warehouse:qa",
                "qa:custody:relocate:whole",
                "qa-custody-relocation",
                out _,
                out string wholeFailure)
            && wholeFailure.Contains("prepared-output-route-protected",
                StringComparison.Ordinal)
            && source.position == new Vector2Int(2, 3)
            && source.quantity == 2
            && ItemStackSignature.Create(source.itemId, source.components) == signature,
            "Whole-stack relocation mutated protected custody.");
        Require(!relocation.TryRelocateQuantity(
                stackId,
                1,
                new Vector2Int(8, 8),
                WorldItemStackState.Loose,
                string.Empty,
                "qa:custody:relocate:partial",
                "qa-custody-relocation",
                out _,
                out string partialFailure)
            && partialFailure.Contains("prepared-output-route-protected",
                StringComparison.Ordinal)
            && source.quantity == 2,
            "Partial relocation did not fail through the custody boundary.");
        int recordCount = repository.Records.Count;
        Require(spawner.Spawn(
                ItemId,
                1,
                Vector2Int.zero,
                WorldItemStackState.Loose,
                string.Empty,
                components: custody) == 0
            && repository.Records.Count == recordCount,
            "Generic spawner accepted authoritative custody components.");
    }

    private static void VerifyTextileCompactionGuard(
        IDungeonItemCatalogProvider catalog)
    {
        WorldItemRepository repository = CreateRepository();
        IReadOnlyList<ItemInstanceComponentSaveData> custody = CreateCustody();
        const string destination = "warehouse:qa:textiles";
        string protectedId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            TextileItemId,
            2,
            WorldItemStackState.Stored,
            destinationId: destination,
            components: custody);
        string ordinaryId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            TextileItemId,
            2,
            WorldItemStackState.Stored,
            destinationId: destination);
        TextileBatchCompactionService compaction = new(
            repository,
            catalog,
            new ResourceTextileMaterialCatalog(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader())));
        RequireThrows<FacilityOutputExactRouteBypassException>(
            () => compaction.CompactDestination(destination),
            "Textile compaction accepted protected custody.");
        Require(repository.GetEditorTestQuantity(protectedId) == 2
            && repository.GetEditorTestQuantity(ordinaryId) == 2
            && repository.Records.Count == 2,
            "Textile compaction changed quantity before failing loud.");
    }

    private static void VerifyTheftGuard(
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery mass)
    {
        WorldItemRepository repository = CreateRepository();
        IReadOnlyList<ItemInstanceComponentSaveData> custody = CreateCustody();
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            2,
            WorldItemStackState.Loose,
            position: Vector2Int.zero,
            components: custody);
        WorldItemQueryService queries = new(
            catalog,
            mass,
            repository,
            EditorNullItemMarkerPresenter.Instance);
        FixedHaulingSettings hauling = new();
        WorldItemTheftService theft = new(
            new NoGridSystemProvider(),
            catalog,
            hauling,
            new MutableClock(),
            repository,
            queries,
            EditorNullItemMarkerPresenter.Instance);
        GameObject customerObject = new("PreparedOutputCustodyTheftFixture");
        try
        {
            CharacterActor customer = InitializeActor(customerObject);
            customer.characterType = CharacterType.Customer;
            CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(customer)
                ?? customerObject.AddComponent<CharacterCarryInventory>();
            inventory.Configure(
                catalog,
                mass,
                hauling,
                new CharacterCarryInventoryRegistry());
            string signature = ItemStackSignature.Create(
                repository.RecordsById[stackId].itemId,
                repository.RecordsById[stackId].components);
            Require(!theft.TryStealLooseItem(
                    customer,
                    0,
                    out WorldItemStackSnapshot stolen,
                    out _)
                && stolen == null
                && inventory.Items.Count == 0
                && repository.GetEditorTestQuantity(stackId) == 2
                && ItemStackSignature.Create(
                    repository.RecordsById[stackId].itemId,
                    repository.RecordsById[stackId].components) == signature,
                "Theft changed protected custody or carry inventory.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(customerObject);
        }
    }

    private static CharacterActor InitializeActor(GameObject target)
    {
        CharacterActor actor = target.AddComponent<CharacterActor>();
        actor.EnsureRuntimeState();
        return actor;
    }

    private static WorldItemRepository CreateRepository() => new(
        new GuidPersistentIdGenerator(),
        new DungeonRuntimeAggregateRootStore());

    private static IReadOnlyList<ItemInstanceComponentSaveData> CreateCustody() =>
        new[]
        {
            new ItemInstanceComponentSaveData
            {
                componentTypeId = FacilityOutputExactRouteCustodyCodec.ComponentTypeId,
                schemaVersion = FacilityOutputExactRouteCustodyCodec.SchemaVersion,
                affectsStacking = true,
                values = new List<ItemStateValueSaveData>()
            }
        };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
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

    private sealed class MutableClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
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
}
#endif
