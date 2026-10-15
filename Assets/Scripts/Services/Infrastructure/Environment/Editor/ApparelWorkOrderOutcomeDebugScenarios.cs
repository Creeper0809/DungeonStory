#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;

public static class ApparelWorkOrderOutcomeDebugScenarios
{
    private const string ApparelId = "apparel:hauling-harness";
    private const string TextileId = "textile:common-wool";
    private const string InstanceId = "item-instance:eec05-terminal";

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds
        { get; } = new[]
        {
            MigratedProducerOutcomeKind.ApparelWorkOrderTerminalResult
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyFourProductionTerminalsAndCancel();
            Require(ApparelRepairOutboxDebugScenarios.RunAll(false),
                "Repair material/outbox recovery scenario failed.");
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static void RunBatch()
    {
        _ = RunAll();
        Debug.Log("APPAREL_WORK_ORDER_OUTCOME=PASS");
    }

    public static void RunObserversFocusedBatch()
    {
        try
        {
            _ = RunAll();
            Debug.Log("APPAREL_WORK_OBSERVERS_FOCUSED_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void VerifyFourProductionTerminalsAndCancel()
    {
        WorldItemStackRuntime items = null;
        DomainFactDispatcher facts = null;
        var objects = new List<GameObject>();
        try
        {
            items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService quantityReservations,
                out _);
            IGameContentDefinitionSource content =
                CharacterAiEditorTestDependencies.ContentDefinitions;
            var apparel = new ResourceApparelDefinitionCatalog(content);
            var textiles = new ResourceTextileMaterialCatalog(content);
            Require(apparel.TryGet(ApparelId, out ApparelDefinitionSO definition),
                "Authored apparel definition is missing.");
            var facilities = new List<BuildableObject>
            {
                CreateFacility(
                    "V22_9303_손세탁_수조.asset",
                    ResearchFacilityCommandKind.HandLaundry,
                    objects),
                CreateFacility(
                    "V22_9304_실내_건조대.asset",
                    ResearchFacilityCommandKind.IndoorDrying,
                    objects),
                CreateFacility(
                    "V22_9308_수선_접수대.asset",
                    ResearchFacilityCommandKind.ApparelRepair,
                    objects),
                CreateFacility(
                    "V22_9301_재단_재봉_작업대.asset",
                    ResearchFacilityCommandKind.ApparelTailoring,
                    objects)
            };
            var clock = new FixtureClock();
            var dispositions = new PhysicalItemBatchDispositionService(
                repository,
                new PhysicalItemMassQuery(items.CatalogProvider),
                EditorNullItemMarkerPresenter.Instance);
            var outcomes = new MigratedProducerOutcomeEditorFixture(
                "run:apparel-work-order-terminal");
            var probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder)
            {
                TargetKind = MigratedProducerOutcomeKind
                    .ApparelWorkOrderTerminalResult
            };
            facts = outcomes.CreateApparelWorkFacts(probe);
            ApparelWorkOrderRuntime CreateRuntime(IDomainFactPublisher publisher, int day) => new ApparelWorkOrderRuntime(
                apparel,
                textiles,
                items,
                new LeasedItemReservationService(
                    items,
                    quantityReservations,
                    clock),
                new FacilityQuery(facilities),
                clock,
                new EditorFixedGameCalendar(day, 9),
                dispositions,
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IApparelPhysicalTransaction>(),
                BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy
                    .Create<IProductionOutputMaximumMassRegistry>(),
                new ProductionFacilityMutationEpochRuntime(),
                performance: BatchACoreSessionSaveDebugScenarios
                    .DefaultInterfaceProxy
                    .Create<ICharacterPerformanceQuery>(),
                facts: publisher);
            var runtime = CreateRuntime(facts, 137);

            string stackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                definition.PhysicalItemId,
                1,
                WorldItemStackState.Loose,
                position: new Vector2Int(4, 2),
                itemInstanceId: InstanceId,
                components: new[]
                {
                    ApparelItemStateCodec.Create(new ApparelInstanceState
                    {
                        apparelDefinitionId = ApparelId,
                        primaryMaterialId = TextileId,
                        craftsmanshipQuality =
                            CraftsmanshipQualityTier.Normal,
                        sourceDefinitionId = TextileId,
                        size = ApparelSizeClass.Medium,
                        durability = 70f,
                        moisture = 20f,
                        contamination = 60f,
                        deterministicBatchHash = 0xEEC05UL
                    })
                });
            Require(stackId.Length > 0, "Apparel target was not created.");
            string original = StateJson(items, stackId);
            Require(runtime.CreateLaundry(
                    new[] { (ItemInstanceId)InstanceId },
                    false,
                    out string cancelled,
                    out DomainFailure failure),
                "Cancelled laundry creation failed: " + Describe(failure));
            Require(runtime.ApplyWork(cancelled, 6f, out failure),
                "Partial laundry work failed: " + Describe(failure));
            Require(runtime.Cancel(cancelled, out failure),
                "Partial laundry cancellation failed: " + Describe(failure));
            string afterCancel = StateJson(items, stackId);
            Require(string.Equals(original, afterCancel, StringComparison.Ordinal),
                "Partial/cancelled laundry changed apparel: before="
                + original + "; after=" + afterCancel);
            Require(probe.CommitCount(MigratedProducerOutcomeKind
                    .ApparelWorkOrderTerminalResult) == 0,
                "Partial/cancelled laundry emitted a terminal result.");

            Require(runtime.CreateLaundry(
                    new[] { (ItemInstanceId)InstanceId },
                    false,
                    out string laundry,
                    out failure)
                && runtime.ApplyWork(laundry, 12f, out failure),
                "Laundry production entry failed: " + Describe(failure));
            ApparelInstanceState laundered = Read(items, stackId);
            Require(laundered.contamination == 0f
                    && laundered.moisture == 100f
                    && probe.CommitCount(MigratedProducerOutcomeKind
                        .ApparelWorkOrderTerminalResult) == 1,
                "Laundry effect/result mismatch.");

            Require(runtime.CreateDrying(
                    new[] { (ItemInstanceId)InstanceId },
                    out string drying,
                    out failure)
                && runtime.ApplyWork(drying, 24f, out failure),
                "Drying production entry failed: " + Describe(failure));
            Require(Read(items, stackId).moisture == 0f
                    && probe.CommitCount(MigratedProducerOutcomeKind
                        .ApparelWorkOrderTerminalResult) == 2,
                "Drying effect/result mismatch.");

            Require(runtime.CreateRepair(
                    (ItemInstanceId)InstanceId,
                    out string repair,
                    out failure)
                && runtime.ApplyWork(repair, 8f, out failure),
                "Repair production entry failed: " + Describe(failure));
            Require(Read(items, stackId).durability == 95f
                    && probe.CommitCount(MigratedProducerOutcomeKind
                        .ApparelWorkOrderTerminalResult) == 3,
                "Repair effect/result mismatch.");

            Require(runtime.CreateAlteration(
                    (ItemInstanceId)InstanceId,
                    ApparelSizeClass.Large,
                    ApparelModificationKind.None,
                    false,
                    out string alteration,
                    out failure)
                && runtime.ApplyWork(alteration, 14f, out failure),
                "Alteration production entry failed: " + Describe(failure));
            Require(Read(items, stackId).size == ApparelSizeClass.Large
                    && probe.CommitCount(MigratedProducerOutcomeKind
                        .ApparelWorkOrderTerminalResult) == 4
                    && runtime.CaptureOrders().Length == 0,
                "Alteration effect/result mismatch.");

            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    outcomes,
                    MigratedProducerOutcomeKind
                        .ApparelWorkOrderTerminalResult,
                    probe,
                    original,
                    StateJson(items, stackId));
            Require(outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                    && exact.participants.Any(value =>
                        value.entityKindId
                            == MigratedProducerOutcomeIds.ItemKind.Value
                        && value.entityId == InstanceId)
                    && exact.facts.Any(value =>
                        value.factId == "apparel-order-kind"
                        && value.value == ApparelWorkOrderKind.Alteration
                            .ToString())
                    && exact.facts.Any(value =>
                        value.factId.StartsWith(
                            "apparel-state-before-",
                            StringComparison.Ordinal))
                    && exact.facts.Any(value =>
                        value.factId.StartsWith(
                            "apparel-state-after-",
                            StringComparison.Ordinal))
                    && exact.absoluteDay == 137,
                "Apparel terminal result omitted exact item or before/after state.");

            // Real owner effects and real observer commit, with only the acknowledgement lost.
            var lostAck = new LoseFirstAckPublisher(facts);
            ApparelWorkOrderRuntime pendingOwner = CreateRuntime(lostAck, 138);
            Require(pendingOwner.CreateLaundry(new[] { (ItemInstanceId)InstanceId }, false,
                    out string pendingId, out failure)
                && !pendingOwner.ApplyWork(pendingId, 12f, out failure),
                "Lost ACK did not leave an applied pending garment.");
            ApparelWorkOrderSaveData pending = pendingOwner.CaptureOrders().Single();
            Require(pending.terminalOutcomeReady && !pending.terminalPublicationConfirmed
                && pending.terminalPublicationKey.Length > 0 && pending.terminalAbsoluteDay == 138,
                "Lost ACK discarded the frozen fact or opaque identity.");
            string appliedState = StateJson(items, stackId);
            long committedSequence = outcomes.Ledger.CaptureGameplayOutcomes().nextSequence;
            var saved = JsonUtility.FromJson<ApparelWorkOrderSaveData>(JsonUtility.ToJson(pending));
            ApparelWorkCompletedFactPreflight.Validate(new[] { saved },
                outcomes.Ledger.CaptureGameplayOutcomes(), outcomes.Transaction.Capture());
            string frozenName = saved.terminalOutcomeItemStates.Single().apparelDisplayName;
            // Retire the old fixture authority through its restore path, releasing its transient leases.
            pendingOwner.PublishRestoreOrders(pendingOwner.PrepareRestoreOrders(Array.Empty<ApparelWorkOrderSaveData>()));
            using var restoredFacts = outcomes.CreateApparelWorkFacts();
            ApparelWorkOrderRuntime restored = CreateRuntime(restoredFacts, 200);
            restored.PublishRestoreOrders(restored.PrepareRestoreOrders(new[] { saved }));
            Require(restored.ApplyWork(pendingId, 1f, out failure)
                && restored.CaptureOrders().Length == 0
                && outcomes.Ledger.CaptureGameplayOutcomes().nextSequence == committedSequence
                && StateJson(items, stackId) == appliedState
                && !restored.ApplyWork(pendingId, 1f, out _),
                "Saved ACK-loss recovery repeated garment effects or emitted a second result: " + Describe(failure));
            var delivered = outcomes.Ledger.CaptureGameplayOutcomes();
            var recovered = delivered.outbox.Concat(delivered.exactOutcomes).Single(row =>
                row.facts.Any(value => value.factId == MigratedProducerOutcomeIds.WorkOrderFact.Value
                    && value.value == pendingId));
            Require(recovered.absoluteDay == 138 && recovered.participants.Any(value =>
                    value.entityId == InstanceId && value.displayText == frozenName),
                "Recovery recomputed the completion date or item name.");

            // No active orders survive a completed save. Their business IDs must not be recycled.
            ApparelWorkOrderRuntime afterEmptySave = CreateRuntime(restoredFacts, 201);
            afterEmptySave.PublishRestoreOrders(afterEmptySave.PrepareRestoreOrders(restored.CaptureOrders()));
            Require(afterEmptySave.CreateDrying(new[] { (ItemInstanceId)InstanceId }, out string newId, out failure)
                && newId != pendingId && newId != laundry && newId != drying
                && afterEmptySave.ApplyWork(newId, 24f, out failure)
                && outcomes.Ledger.CaptureGameplayOutcomes().nextSequence == committedSequence + 1,
                "Empty save restore reused a completed order identity.");
            Debug.Log("APPAREL_OBSERVER_RECOVERY=PASS; JsonUtility=true; actualOwnerAckLoss=true; frozenDay=true; duplicateEffects=0; newOrderIdentity=true");

        }
        finally
        {
            facts?.Dispose();
            items?.Dispose();
            foreach (GameObject value in objects)
                UnityEngine.Object.DestroyImmediate(value);
        }
    }

    private sealed class LoseFirstAckPublisher : IDomainFactPublisher, IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        private bool lose = true;
        internal LoseFirstAckPublisher(DomainFactDispatcher inner) => this.inner = inner;
        public bool TryReserve<T>(in T fact, out DomainFactLease lease, out string failure) =>
            inner.TryReserve(fact, out lease, out failure);
        public DomainFactDelivery Publish<T>(in DomainFactLease lease, in T fact)
        {
            DomainFactDelivery delivery = inner.Publish(lease, fact);
            if (!lose || !delivery.Completed) return delivery;
            lose = false;
            return DomainFactDelivery.Uncertain("apparel-test-ack-lost-after-real-commit");
        }
        public bool PublishBatch(IReadOnlyList<DomainFactEmission> facts, DomainFactDelivery[] results, out string failure) =>
            inner.PublishBatch(facts, results, out failure);
        public DomainFactDelivery Replay<T>(in T fact) => inner.Replay(fact);
        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
        public bool ReplayBatch(IReadOnlyList<DomainFactReplay> facts, DomainFactDelivery[] results, out string failure) =>
            inner.ReplayBatch(facts, results, out failure);
    }

    private static BuildableObject CreateFacility(
        string filename,
        ResearchFacilityCommandKind expected,
        ICollection<GameObject> objects)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/V22Apparel/" + filename);
        Require(building != null
                && building.ResearchFacilityCommand == expected,
            "Authored apparel facility is missing: " + filename);
        var value = new GameObject("EEC05 " + expected);
        objects.Add(value);
        BuildableObject facility = value.AddComponent<BuildableObject>();
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

    private static ApparelInstanceState Read(
        IWorldItemStackRuntime items,
        string stackId)
    {
        WorldItemStackSnapshot stack = items.GetAllStacks()
            .Single(value => value.StackId == stackId);
        Require(ApparelItemStateCodec.TryRead(
                stack.Components,
                out ApparelInstanceState state),
            "Apparel state could not be read.");
        return state;
    }

    private static string StateJson(
        IWorldItemStackRuntime items,
        string stackId) => JsonUtility.ToJson(Read(items, stackId));

    private static string Describe(DomainFailure failure) =>
        failure.Code + "[" + string.Join(",", failure.Parameters.ToArray()) + "]";

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixtureClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => GameCalendarRules.SecondsPerDay * 77f;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }

    private sealed class FacilityQuery : IFacilityCapabilityQuery
    {
        private readonly IReadOnlyList<BuildableObject> facilities;

        public FacilityQuery(IReadOnlyList<BuildableObject> facilities) =>
            this.facilities = facilities;

        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") =>
            Array.Empty<BuildableObject>();

        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) => facilities
            .Where(value => value.BuildingData.ResearchFacilityCommand == command)
            .ToArray();
    }
}
#endif
