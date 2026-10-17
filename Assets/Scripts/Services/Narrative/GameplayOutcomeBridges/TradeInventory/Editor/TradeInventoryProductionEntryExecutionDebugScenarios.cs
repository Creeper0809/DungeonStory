#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class TradeInventoryProductionEntryExecutionDebugScenarios
{
    private const string OutcomeTypeId = "trade-inventory.result";
    private const string BatchProducerId =
        "trade-inventory.physical-item-batch-disposition-receipt";
    private const string RelocationProducerId =
        "trade-inventory.physical-item-relocation-receipt";
    private const string WastePolicyProducerId =
        "trade-inventory.waste-policy-command-result";

    public static bool RunAll(bool log = false)
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new TradeInventoryOutcomeDescriptor(new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new TradeInventoryOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId(
                "run:trade-inventory-production-entry-editor"),
            1L);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        using DomainFactDispatcher facts = new();
        facts.Subscribe(
            "physical-relocation",
            new GameplayDomainFactObserver<
                PhysicalItemRelocatedFact,
                TradeInventoryOutcomeReceipt>(
                PhysicalItemRelocationFactObservation.Capture,
                registry,
                ledger,
                recorder,
                new GameplayDomainFactCommitGroup(recorder, ledger)));
        facts.Subscribe(
            "physical-disposition",
            new GameplayDomainFactObserver<
                PhysicalItemDisposedFact,
                TradeInventoryOutcomeReceipt>(
                PhysicalItemDispositionFactObservation.Capture,
                registry,
                ledger,
                recorder,
                new GameplayDomainFactCommitGroup(recorder, ledger)));
        facts.Subscribe(
            "waste-policy",
            new GameplayDomainFactObserver<
                WastePolicyChangedFact,
                TradeInventoryOutcomeReceipt>(
                WastePolicyFactObservation.Capture,
                registry,
                ledger,
                recorder,
                new GameplayDomainFactCommitGroup(recorder, ledger)));

        WorldItemStackRuntime items =
            PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService quantityReservations,
                out _);
        try
        {
            VerifyBatchDisposition(
                items,
                repository,
                quantityReservations,
                facts,
                EditorFixedGameCalendar.Instance,
                recorder,
                ledger);
            VerifyRelocation(
                items,
                repository,
                facts,
                EditorFixedGameCalendar.Instance,
                ledger);
            VerifyWastePolicy(facts, facts, ledger);

            GameplayOutcomeQueryPage global = ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All);
            string[] producers = global.Items
                .Select(item => item.Exact?.producerId ?? string.Empty)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            Require(
                global.Items.Count == 3
                && producers.SequenceEqual(
                    new[]
                    {
                        BatchProducerId,
                        RelocationProducerId,
                        WastePolicyProducerId
                    }.OrderBy(value => value, StringComparer.Ordinal)),
                "The shared ledger did not retain exactly one result from each production producer.");

            if (log)
            {
                Debug.Log(
                    "Trade/inventory production-entry outcomes PASS: "
                    + string.Join(", ", producers));
            }
            return true;
        }
        finally
        {
            items.Dispose();
        }
    }

    private static void VerifyBatchDisposition(
        WorldItemStackRuntime items,
        WorldItemRepository repository,
        ItemQuantityReservationService quantityReservations,
        DomainFactDispatcher facts,
        IGameCalendar calendar,
        IGameplayOutcomeRecorder recorder,
        GameplayOutcomeLedger ledger)
    {
        const string OperationId =
            "qa:trade-inventory:batch-disposition:production-entry";
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            "item:buffer",
            2,
            WorldItemStackState.Loose,
            position: new Vector2Int(2, 3));
        PhysicalItemBatchDispositionService service = new(
            repository,
            items.MassQuery,
            EditorNullItemMarkerPresenter.Instance,
            quantityReservations,
            items.CatalogProvider,
            facts,
            facts,
            new GameplayDomainFactDeliveryQuery(recorder, ledger),
            calendar);

        bool succeeded = service.TryCommit(
            new[] { new PhysicalItemTransformInput(stackId, 1) },
            PhysicalItemDispositionKind.Sink,
            OperationId,
            "qa-production-entry-batch-sink",
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason);
        Require(
            succeeded
            && failureReason.Length == 0
            && receipt.IsCommitted
            && receipt.Quantity == 1
            && receipt.SourceStackIds.SequenceEqual(new[] { stackId })
            && repository.GetEditorTestQuantity(stackId) == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0,
            "PhysicalItemBatchDispositionService.TryCommit failed or lost its exact 2-to-1 quantity mutation: "
            + failureReason);

        GameplayResultKey key = new(
            BatchProducerId,
            new GameplayOperationId(OperationId),
            receipt.OwnerRevision,
            0);
        RequireAcknowledged(
            ledger,
            key,
            BatchProducerId,
            OperationId,
            receipt.OwnerRevision);
    }

    private static void VerifyRelocation(
        WorldItemStackRuntime items,
        WorldItemRepository repository,
        DomainFactDispatcher facts,
        IGameCalendar calendar,
        GameplayOutcomeLedger ledger)
    {
        const string OperationId =
            "qa:trade-inventory:relocation:production-entry";
        Vector2Int sourcePosition = new(5, 6);
        Vector2Int destinationPosition = new(8, 9);
        string sourceStackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            "item:buffer",
            2,
            WorldItemStackState.Loose,
            position: sourcePosition);
        long expectedOwnerRevision = checked(
            (long)repository.ItemStackVersion + 2L);
        PreparedPhysicalItemRelocationService preparedRelocations = new(
            repository,
            items.MassQuery,
            items.CatalogProvider,
            EditorNullItemMarkerPresenter.Instance);
        PhysicalItemRelocationService service = new(
            repository,
            preparedRelocations,
            facts,
            facts,
            calendar);

        bool succeeded = service.TryRelocateQuantity(
            sourceStackId,
            1,
            destinationPosition,
            WorldItemStackState.Loose,
            string.Empty,
            OperationId,
            "qa-production-entry-relocation",
            out PhysicalItemRelocationReceipt receipt,
            out string failureReason);
        WorldItemStackSnapshot source = items.GetAllStacks().SingleOrDefault(
            stack => stack.StackId == sourceStackId);
        WorldItemStackSnapshot destination = items.GetAllStacks().SingleOrDefault(
            stack => stack.StackId == receipt.DestinationStackId);
        Require(
            succeeded
            && failureReason.Length == 0
            && receipt.IsCommitted
            && receipt.DestinationStackId != sourceStackId
            && source != null
            && source.Quantity == 1
            && source.Position == sourcePosition
            && destination != null
            && destination.Quantity == 1
            && destination.Position == destinationPosition
            && destination.State == WorldItemStackState.Loose,
            "PhysicalItemRelocationService.TryRelocateQuantity failed or lost its exact 2-to-1+1 split: "
            + failureReason);

        GameplayResultKey key = new(
            RelocationProducerId,
            new GameplayOperationId(OperationId),
            0L,
            0);
        RequireAcknowledged(
            ledger,
            key,
            RelocationProducerId,
            OperationId,
            expectedOwnerRevision);
    }

    private static void VerifyWastePolicy(
        IDomainFactStagingPublisher observations,
        IDomainFactRecoveryPublisher recovery,
        GameplayOutcomeLedger ledger)
    {
        WasteProcessingRulesDefinition rules = new(
            5f,
            80f,
            new[]
            {
                new WasteOriginRuleRecord
                {
                    origin = WasteOriginKind.Plant,
                    defaultDisposition = WasteDispositionKind.Store,
                    defaultMaximumFeedContamination = 79f,
                    supportedDispositions = new List<WasteDispositionKind>
                    {
                        WasteDispositionKind.Store,
                        WasteDispositionKind.Compost
                    }
                }
            },
            Array.Empty<WasteRecipeBindingRecord>(),
            Array.Empty<WasteFeedRuleRecord>(),
            Array.Empty<LegacyWasteItemRuleRecord>());
        WasteProcessingRuntime runtime = new(
            new WasteProcessingMaterialDependencies(
                EmptyWasteInventoryPort.Instance,
                EmptyResourceEconomyContentCatalog.Instance),
            new WasteProcessingOperationDependencies(
                EmptyWasteProductionPort.Instance,
                new UnityGameClock(),
                rules),
            new DungeonRuntimeAggregateRootStore(),
            observations,
            recovery,
            EditorFixedGameCalendar.Instance);
        WastePolicyData before = runtime.GetPolicy(WasteOriginKind.Plant);
        int beforeVersion = runtime.Version;
        WastePolicyCommandResult result = runtime.SetPolicy(new WastePolicyData
        {
            origin = WasteOriginKind.Plant,
            disposition = WasteDispositionKind.Compost,
            enabled = false,
            maximumFeedContamination = 40f
        });
        WastePolicyData after = runtime.GetPolicy(WasteOriginKind.Plant);
        DungeonWasteProcessingSaveData saved = runtime.Capture();
        bool exactPostState = result.Succeeded
            && !result.Failure.IsFailure
            && result.OperationId == "waste-policy:1:command:1"
            && result.OwnerRevision == 1L
            && result.BeforePolicy != null
            && result.BeforePolicy.origin == WasteOriginKind.Plant
            && result.BeforePolicy.disposition == WasteDispositionKind.Store
            && result.BeforePolicy.enabled
            && Mathf.Approximately(
                result.BeforePolicy.maximumFeedContamination,
                79f)
            && result.AfterPolicy != null
            && result.AfterPolicy.origin == WasteOriginKind.Plant
            && result.AfterPolicy.disposition == WasteDispositionKind.Compost
            && !result.AfterPolicy.enabled
            && Mathf.Approximately(
                result.AfterPolicy.maximumFeedContamination,
                40f)
            && before.disposition == WasteDispositionKind.Store
            && before.enabled
            && Mathf.Approximately(before.maximumFeedContamination, 79f)
            && after.disposition == WasteDispositionKind.Compost
            && !after.enabled
            && Mathf.Approximately(after.maximumFeedContamination, 40f)
            && beforeVersion == 0
            && runtime.Version == beforeVersion + 1
            && saved.version == DungeonWasteProcessingSaveData.CurrentVersion
            && saved.nextPolicySequence == 2L
            && saved.pendingPolicyChanges.Count == 0;
        Require(
            exactPostState,
            "WasteProcessingRuntime.SetPolicy did not commit the exact policy, version, sequence, and acknowledged outbox state. Observed: "
            + "succeeded=" + result.Succeeded
            + "; failure=" + DescribeFailure(result.Failure)
            + "; operationId=" + result.OperationId
            + "; ownerRevision=" + result.OwnerRevision
            + "; resultBefore=" + DescribePolicy(result.BeforePolicy)
            + "; resultAfter=" + DescribePolicy(result.AfterPolicy)
            + "; runtimeBefore=" + DescribePolicy(before)
            + "; runtimeAfter=" + DescribePolicy(after)
            + "; runtimeVersion=" + beforeVersion + "->" + runtime.Version
            + "; saveSchemaVersion=" + saved.version
            + "; nextPolicySequence=" + saved.nextPolicySequence
            + "; pending=" + DescribePending(saved.pendingPolicyChanges)
            + "; ledgerExactCount="
            + ledger.CaptureGameplayOutcomes().exactOutcomes.Count + ".");

        GameplayResultKey key = new(
            WastePolicyProducerId,
            new GameplayOperationId(result.OperationId),
            result.OwnerRevision,
            0);
        RequireAcknowledged(
            ledger,
            key,
            WastePolicyProducerId,
            result.OperationId,
            result.OwnerRevision);
    }

    private static GameplayOutcomeSnapshot RequireAcknowledged(
        GameplayOutcomeLedger ledger,
        GameplayResultKey key,
        string expectedProducerId,
        string expectedOperationId,
        long expectedOwnerRevision)
    {
        Require(
            ledger.TryGetResultIdentity(
                key,
                out GameplayOutcomeReplayIdentity identity)
            && identity.ResultKey.Equals(key)
            && identity.State ==
                GameplayOutcomeReplayState.PublishedAcknowledged
            && identity.HasCanonicalPayloadHash,
            "The production owner result was not PublishedAcknowledged: " + key);
        Require(
            ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact)
            && exact != null
            && exact.producerId == expectedProducerId
            && exact.operationId == expectedOperationId
            && exact.commitRevision == key.CommitRevision
            && exact.localResultIndex == key.LocalResultIndex
            && exact.outcomeTypeId == OutcomeTypeId
            && exact.ownerRevision == expectedOwnerRevision
            && exact.status == GameplayOutcomeStatus.Succeeded,
            "The exact ledger row lost its producer, type, operation, revision, or owner state: "
            + key);
        return exact;
    }

    private static string DescribePolicy(WastePolicyData policy) => policy == null
        ? "null"
        : policy.origin + "/" + policy.disposition
            + "/enabled=" + policy.enabled
            + "/maximumFeedContamination="
            + policy.maximumFeedContamination.ToString(
                "0.###",
                System.Globalization.CultureInfo.InvariantCulture);

    private static string DescribeFailure(in DomainFailure failure) =>
        failure.IsFailure
            ? failure.Code + ":" + string.Join(",", failure.Parameters.ToArray())
            : FailureCode.None.ToString();

    private static string DescribePending(
        IReadOnlyList<WastePolicyObservationSaveData> pending) =>
        pending == null ? "null" : string.Join(",", pending.Select(value =>
            value == null
                ? "null-row"
                : value.fact?.operationId + "/key=" + value.publicationKey
                    + "/proof=" + (value.publicationId?.Length > 0)));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class EmptyWasteInventoryPort : IWasteProcessingInventoryPort
    {
        internal static EmptyWasteInventoryPort Instance { get; } = new();

        public bool HasExactWildlifeCareDestinationAuthority(
            string destinationId,
            Vector2Int destinationPosition) => false;

        public bool TryGetExactWildlifeCareDestinationPosition(
            string destinationId,
            out Vector2Int destinationPosition)
        {
            destinationPosition = default;
            return false;
        }

        public IReadOnlyList<WasteProcessingStackSnapshot> GetAllStacks() =>
            Array.Empty<WasteProcessingStackSnapshot>();

        public bool TryRequestStackDelivery(
            ItemStackId stackId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out DomainFailure failure)
        {
            requested = 0;
            failure = new DomainFailure(FailureCode.WasteFeedUnavailable);
            return false;
        }
    }

    private sealed class EmptyWasteProductionPort : IWasteProcessingProductionPort
    {
        internal static EmptyWasteProductionPort Instance { get; } = new();

        public int CountBillsMatching(Func<string, bool> recipePredicate) => 0;

        public void EnsureSingleBill(ProductionRecipeSO recipe)
        {
        }
    }
}
#endif
