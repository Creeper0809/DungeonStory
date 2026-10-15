#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;

public static class ApparelRepairOutboxDebugScenarios
{
    private const string RepairFacilityPath =
        "Assets/Resources/SO/Building/V22Apparel/V22_9308_수선_접수대.asset";
    private const string ApparelId = "apparel:hauling-harness";
    private const string TextileId = "textile:common-wool";
    private const string ThreadItemId = "material:sewing-thread";
    private const string ScrapItemId = "material:mending-scrap";

    [MenuItem("DungeonStory/Debug/Items/Run Apparel Repair Outbox Focused")]
    public static void RunFocused()
    {
        string details = VerifyRepairPendingOutboxAndRestore();
        Debug.Log("Apparel repair pending outbox PASS. " + details);
    }

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            _ = VerifyRepairPendingOutboxAndRestore();
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    internal static string VerifyRepairPendingOutboxAndRestore()
    {
        WorldItemStackRuntime items = null;
        DomainFactDispatcher facts = null;
        GameObject facilityObject = null;
        try
        {
            items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService quantityReservations,
                out _);
            IGameContentDefinitionSource content =
                CharacterAiEditorTestDependencies.ContentDefinitions;
            IApparelDefinitionCatalog apparel =
                new ResourceApparelDefinitionCatalog(content);
            ITextileMaterialCatalog textiles =
                new ResourceTextileMaterialCatalog(content);
            Require(apparel.TryGet(ApparelId, out ApparelDefinitionSO definition),
                $"authored apparel missing: {ApparelId}");
            Require(textiles.TryGet(TextileId, out _),
                $"authored textile missing: {TextileId}");

            BuildableObject facility = CreateRepairFacility(out facilityObject);
            MutableGameClock clock = new MutableGameClock();
            PhysicalItemBatchDispositionService innerDisposition = new(
                repository,
                new PhysicalItemMassQuery(items.CatalogProvider),
                EditorNullItemMarkerPresenter.Instance);
            FailOnceAcknowledgementDisposition disposition = new(innerDisposition);
            var outcomeFixture = new MigratedProducerOutcomeEditorFixture(
                "run:apparel-repair-outbox");
            var outcomeProbe = new MigratedProducerOutcomeProbeTransaction(
                outcomeFixture.Transaction,
                outcomeFixture.Recorder)
            {
                TargetKind = MigratedProducerOutcomeKind
                    .ApparelWorkOrderTerminalResult
            };
            facts = outcomeFixture.CreateApparelWorkFacts(outcomeProbe);
            ApparelWorkOrderRuntime runtime = new(
                apparel,
                textiles,
                items,
                new LeasedItemReservationService(items, quantityReservations, clock),
                new FixtureFacilityCapabilityQuery(facility),
                clock,
                new EditorFixedGameCalendar(137, 9),
                disposition,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IApparelPhysicalTransaction>(),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IProductionOutputMaximumMassRegistry>(),
                new ProductionFacilityMutationEpochRuntime(),
                performance: BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<ICharacterPerformanceQuery>(),
                facts: facts);

            const string instanceId = "apparel-instance:qa-repair-outbox";
            string targetStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                definition.PhysicalItemId,
                1,
                WorldItemStackState.Loose,
                position: new Vector2Int(4, 2),
                itemInstanceId: instanceId,
                components: new[] { ApparelItemStateCodec.Create(CreateDamagedState()) });
            Require(targetStackId.Length > 0, "repair target was not created");

            SeedRepairMaterials(repository, 1);
            int initialThread = Quantity(items, ThreadItemId);
            int initialScrap = Quantity(items, ScrapItemId);
            disposition.FailNextAcknowledgement = true;
            Require(runtime.CreateRepair(
                    (ItemInstanceId)instanceId,
                out string firstOrderId,
                out DomainFailure createFailure),
                $"first repair order failed: {Describe(createFailure)}");
            Require(!runtime.ApplyWork(firstOrderId, 18f, out DomainFailure pendingFailure),
                "forced acknowledgement failure unexpectedly completed repair");
            ApparelWorkOrderSaveData firstPending = runtime.CaptureOrders().Single();
            Require(firstPending.state
                    == ApparelWorkOrderState.WaitingForDispositionFinalization
                    && firstPending.repairCommitPhase
                    == ApparelRepairCommitPhase.RepairApplied,
                $"repair did not persist applied pending phase: {firstPending.state}/"
                + firstPending.repairCommitPhase);
            Require(pendingFailure.Code == FailureCode.ApparelTransferFailed,
                $"pending failure was not typed: {pendingFailure}");
            Require(Quantity(items, ThreadItemId) == initialThread - 1
                    && Quantity(items, ScrapItemId) == initialScrap - 1,
                "repair material debit was not exact");
            Require(ReadDurability(items, targetStackId) == 70f,
                "repair result was not applied before pending acknowledgement");
            Require(disposition.TryGetPending(
                    firstPending.repairOperationId,
                    out PhysicalItemBatchDispositionReceipt firstReceipt)
                    && firstReceipt.CommitId == firstPending.repairCommitId,
                "pending disposition receipt was not retained");

            int beforeCancelVersion = runtime.Version;
            string beforeCancelOrder = JsonUtility.ToJson(firstPending);
            Require(!runtime.Cancel(firstOrderId, out DomainFailure cancelFailure)
                    && cancelFailure.Code == FailureCode.ApparelRecoveryDeferred,
                "pending repair cancellation did not fail with typed recovery deferral");
            ApparelWorkOrderSaveData afterCancel = runtime.CaptureOrders().Single();
            Require(runtime.Version == beforeCancelVersion
                    && string.Equals(
                        JsonUtility.ToJson(afterCancel),
                        beforeCancelOrder,
                        StringComparison.Ordinal)
                    && disposition.TryGetPending(
                        firstPending.repairOperationId,
                        out PhysicalItemBatchDispositionReceipt retainedReceipt)
                    && retainedReceipt.CommitId == firstPending.repairCommitId
                    && Quantity(items, ThreadItemId) == initialThread - 1
                    && Quantity(items, ScrapItemId) == initialScrap - 1
                    && ReadDurability(items, targetStackId) == 70f,
                "rejected pending repair cancellation mutated its order, receipt, materials, or target");

            outcomeProbe.FaultMode = MigratedProducerOutcomeFaultMode
                .RejectReservation;
            Require(!runtime.ApplyWork(
                    firstOrderId,
                    1f,
                    out DomainFailure reservationFailure)
                    && reservationFailure.Code
                        == FailureCode.ApparelTransferFailed,
                "injected apparel parent reservation rejection succeeded");
            ApparelWorkOrderSaveData reservedPending = runtime.CaptureOrders()
                .Single();
            Require(reservedPending.terminalOutcomeReady
                    && reservedPending.repairCommitPhase
                        == ApparelRepairCommitPhase.None
                    && !reservedPending.terminalPublicationConfirmed
                    && string.IsNullOrEmpty(
                        reservedPending.terminalPublicationKey)
                    && Quantity(items, ThreadItemId) == initialThread - 1
                    && Quantity(items, ScrapItemId) == initialScrap - 1,
                "parent reservation rejection repeated or lost repair effects");
            outcomeProbe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            Require(runtime.ApplyWork(firstOrderId, 1f, out DomainFailure retryFailure),
                $"repair retry did not finalize: {retryFailure}");
            Require(runtime.CaptureOrders().Length == 0,
                "completed repair remained in save payload");
            Require(Quantity(items, ThreadItemId) == initialThread - 1
                    && Quantity(items, ScrapItemId) == initialScrap - 1
                    && !disposition.TryGetPending(firstPending.repairOperationId, out _),
                "repair retry debited twice or retained the receipt");

            Require(items.TrySetInstanceComponent(
                    targetStackId,
                    ApparelItemStateCodec.Create(CreateDamagedState())),
                "repair target could not be reset for restore scenario");
            SeedRepairMaterials(repository, 2);
            int restoreThreadBefore = Quantity(items, ThreadItemId);
            int restoreScrapBefore = Quantity(items, ScrapItemId);
            disposition.FailNextAcknowledgement = true;
            Require(runtime.CreateRepair(
                    (ItemInstanceId)instanceId,
                out string restoreOrderId,
                out DomainFailure restoreCreateFailure),
                $"restore repair order failed: {Describe(restoreCreateFailure)}");
            Require(!runtime.ApplyWork(
                    restoreOrderId,
                    18f,
                    out DomainFailure restorePendingFailure)
                    && restorePendingFailure.Code == FailureCode.ApparelTransferFailed,
                "restore fixture did not stop at pending acknowledgement");
            ApparelWorkOrderSaveData livePending = runtime.CaptureOrders().Single();
            Require(Quantity(items, ThreadItemId) == restoreThreadBefore - 1
                    && Quantity(items, ScrapItemId) == restoreScrapBefore - 1,
                "restore fixture material debit was not exact");

            ApparelWorkOrderSaveData tampered = Clone(livePending);
            tampered.repairCommitId += ":tampered";
            bool rejected = false;
            runtime.BeginRestoreCandidate();
            try
            {
                runtime.PublishRestoreOrders(runtime.PrepareRestoreOrders(new[] { tampered }));
                runtime.PublishRestoreCandidate();
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            finally
            {
                runtime.DiscardRestoreCandidate();
            }
            Require(rejected, "tampered repair commit joined a restore candidate");
            ApparelWorkOrderSaveData afterRejected = runtime.CaptureOrders().Single();
            Require(afterRejected.orderId == livePending.orderId
                    && afterRejected.repairCommitId == livePending.repairCommitId
                    && afterRejected.repairCommitPhase
                    == ApparelRepairCommitPhase.RepairApplied
                    && disposition.TryGetPending(livePending.repairOperationId, out _)
                    && ReadDurability(items, targetStackId) == 70f,
                "rejected restore changed live order, item, or receipt authority");

            outcomeProbe.FaultMode = MigratedProducerOutcomeFaultMode
                .RejectCommit;
            Require(!runtime.ApplyWork(
                    restoreOrderId,
                    1f,
                    out DomainFailure parentFailure)
                    && parentFailure.Code == FailureCode.ApparelTransferFailed,
                "injected apparel parent rejection unexpectedly succeeded");
            ApparelWorkOrderSaveData parentPending = runtime.CaptureOrders()
                .Single();
            Require(parentPending.terminalOutcomeReady
                    && !parentPending.terminalPublicationConfirmed
                    && string.IsNullOrEmpty(
                        parentPending.terminalPublicationKey)
                    && parentPending.repairCommitPhase
                        == ApparelRepairCommitPhase.None
                    && parentPending.terminalMaterialCommitId
                        == livePending.repairCommitId
                    && !disposition.TryGetPending(
                        livePending.repairOperationId,
                        out _)
                    && Quantity(items, ThreadItemId)
                        == restoreThreadBefore - 1
                    && Quantity(items, ScrapItemId)
                        == restoreScrapBefore - 1
                    && ReadDurability(items, targetStackId) == 70f,
                "parent rejection did not retain the exact applied repair receipt");
            Require(!runtime.Cancel(
                    restoreOrderId,
                    out DomainFailure parentCancelFailure)
                    && parentCancelFailure.Code
                        == FailureCode.ApparelRecoveryDeferred,
                "pending apparel parent could be cancelled after physical commit");

            ApparelWorkOrderRuntime restored = new(
                apparel,
                textiles,
                items,
                new LeasedItemReservationService(
                    items,
                    quantityReservations,
                    clock),
                new FixtureFacilityCapabilityQuery(facility),
                clock,
                new EditorFixedGameCalendar(137, 9),
                disposition,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IApparelPhysicalTransaction>(),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IProductionOutputMaximumMassRegistry>(),
                new ProductionFacilityMutationEpochRuntime(),
                performance: BatchACoreSessionSaveDebugScenarios
                    .DefaultInterfaceProxy
                    .Create<ICharacterPerformanceQuery>(),
                facts: facts);
            restored.PublishRestoreOrders(
                restored.PrepareRestoreOrders(
                    new[] { Clone(parentPending) }));
            outcomeProbe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            string frozenTerminal = JsonUtility.ToJson(parentPending);
            Require(restored.ApplyWork(
                    restoreOrderId,
                    1f,
                    out DomainFailure retryParentFailure),
                "saved apparel parent did not retry: "
                + Describe(retryParentFailure));
            Require(restored.ParticipantId ==
                    "226.world.apparel-work-orders",
                $"unexpected restore participant id: {restored.ParticipantId}");
            Require(restored.CaptureOrders().Length == 0
                    && Quantity(items, ThreadItemId)
                        == restoreThreadBefore - 1
                    && Quantity(items, ScrapItemId)
                        == restoreScrapBefore - 1
                    && ReadDurability(items, targetStackId) == 70f,
                "saved parent retry repeated or lost the physical repair");
            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomeFixture,
                    MigratedProducerOutcomeKind
                        .ApparelWorkOrderTerminalResult,
                    outcomeProbe,
                    frozenTerminal,
                    "order=" + restoreOrderId + "|outcome=durable");
            Require(outcomeFixture.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                    && exact.participants.Any(value =>
                        value.entityKindId
                            == MigratedProducerOutcomeIds.ItemKind.Value
                        && value.entityId == instanceId)
                    && Metric(exact, "count") == 1d
                    && Metric(exact, "quantity") == 2d
                    && Metric(exact, "mass") > 0d,
                "apparel parent omitted the exact garment or repair inputs");
            int reservations = outcomeProbe.ReservationCount(
                MigratedProducerOutcomeKind.ApparelWorkOrderTerminalResult);
            int commits = outcomeProbe.CommitCount(
                MigratedProducerOutcomeKind.ApparelWorkOrderTerminalResult);
            Require(!restored.ApplyWork(
                    restoreOrderId,
                    1f,
                    out _)
                    && outcomeProbe.ReservationCount(
                        MigratedProducerOutcomeKind
                            .ApparelWorkOrderTerminalResult) == reservations
                    && outcomeProbe.CommitCount(
                        MigratedProducerOutcomeKind
                            .ApparelWorkOrderTerminalResult) == commits,
                "completed apparel parent replay created a duplicate");

            return $"retry={firstOrderId}; restore={restoreOrderId}; "
                + $"participant={restored.ParticipantId}; materials=1+1; durability=70";
        }
        finally
        {
            facts?.Dispose();
            items?.Dispose();
            if (facilityObject != null)
            {
                UnityEngine.Object.DestroyImmediate(facilityObject);
            }
        }
    }

    private static ApparelInstanceState CreateDamagedState() => new()
    {
        apparelDefinitionId = ApparelId,
        primaryMaterialId = TextileId,
        craftsmanshipQuality = CraftsmanshipQualityTier.Normal,
        sourceKind = TextileSourceKind.Unknown,
        sourceDefinitionId = TextileId,
        size = ApparelSizeClass.Medium,
        durability = 40f,
        deterministicBatchHash = 0xA22A11UL
    };

    private static void SeedRepairMaterials(WorldItemRepository repository, int ordinal)
    {
        WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ThreadItemId,
            1,
            WorldItemStackState.Loose,
            position: new Vector2Int(2 + ordinal, 2));
        WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ScrapItemId,
            1,
            WorldItemStackState.Loose,
            position: new Vector2Int(2 + ordinal, 3));
    }

    private static int Quantity(IWorldItemStackRuntime items, string itemId) =>
        items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
            .Sum(value => value.Quantity);

    private static float ReadDurability(
        IWorldItemStackRuntime items,
        string stackId)
    {
        WorldItemStackSnapshot stack = items.GetAllStacks()
            .Single(value => value.StackId == stackId);
        Require(ApparelItemStateCodec.TryRead(stack.Components, out ApparelInstanceState state),
            "apparel target component could not be read");
        return state.durability;
    }

    private static double Metric(GameplayOutcomeSnapshot exact, string id) =>
        exact.metrics.Single(value => value.metricId == id).value;

    private static ApparelWorkOrderSaveData Clone(ApparelWorkOrderSaveData source) =>
        JsonUtility.FromJson<ApparelWorkOrderSaveData>(JsonUtility.ToJson(source));

    private static string Describe(DomainFailure failure) =>
        $"{failure.Code}({string.Join(",", failure.Parameters.ToArray())})";

    private static BuildableObject CreateRepairFacility(out GameObject facilityObject)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(RepairFacilityPath);
        Require(building != null, $"repair facility asset missing: {RepairFacilityPath}");
        Require(building.ResearchFacilityCommand == ResearchFacilityCommandKind.ApparelRepair,
            "repair facility command contract drifted");
        facilityObject = new GameObject("ApparelRepairOutboxFacility");
        BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
        facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        facility.ConstructBuildableObject(
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingResearchWorkPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingFacilityStateChangePort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingRoomPolicyPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingEquipmentCraftingRuntimePort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingWorldRegistryPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingItemStackPort>(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingAbilityRuntimeDispatcher>(),
            new UnityGameClock(),
            BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                .Create<IBuildingPaidFacilityContractPort>(),
            new FacilityEvolutionStateComponentFactory());
        facility.Initialization(building, new Vector2Int(6, 2));
        return facility;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class MutableGameClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 0f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FixtureFacilityCapabilityQuery : IFacilityCapabilityQuery
    {
        private readonly BuildableObject facility;

        public FixtureFacilityCapabilityQuery(BuildableObject facility) =>
            this.facility = facility ?? throw new ArgumentNullException(nameof(facility));

        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") => Array.Empty<BuildableObject>();

        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) =>
            command == ResearchFacilityCommandKind.ApparelRepair
                ? new[] { facility }
                : Array.Empty<BuildableObject>();
    }

    private sealed class FailOnceAcknowledgementDisposition :
        IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;

        public FailOnceAcknowledgementDisposition(
            IPhysicalItemBatchDispositionService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool FailNextAcknowledgement { get; set; }

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommit(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommitPending(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "qa-forced-apparel-acknowledgement-failure";
                return false;
            }
            return inner.Acknowledge(commitId, out failureReason);
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);
    }
}
#endif
