using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CombatEquipmentCraftTransactionFixture
{
    public static string LastFailureReason { get; private set; } = string.Empty;

    private static readonly IReadOnlyDictionary<string, int> Requirements =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["material:lumber"] = 2,
            ["material:iron-ingot"] = 1
        };

    public static bool Run()
    {
        LastFailureReason = string.Empty;
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        if (!VerifyMissingInputAtomic(catalog))
        {
            return Fail("missing-input atomicity");
        }
        if (!VerifyRejectedDismantleReplay(catalog))
        {
            return Fail("rejected-dismantle replay");
        }

        FixtureGateway gateway = new(catalog) { FailNextAcknowledgement = true };
        CombatEquipmentCraftOrderSaveData order = CreateOrder("qa");
        string lumberA = gateway.Add("material:lumber", 1, order.materialDestinationId);
        string lumberB = gateway.Add("material:lumber", 1, order.materialDestinationId);
        string iron = gateway.Add("material:iron-ingot", 1, order.materialDestinationId);
        if (!CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                order,
                Requirements,
                gateway.GetAllStacks(),
                gateway,
                out _)
            || !order.materialsReady
            || order.materialTransferInputs.Count != 3
            || gateway.Quantity(lumberA) != 0
            || gateway.Quantity(lumberB) != 0
            || gateway.Quantity(iron) != 0
            || !gateway.TryGetPendingBatchPhysicalDisposition(
                order.materialTransferOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            return Fail("material Transfer commit");
        }

        order.attemptOutcomeResolved = true;
        order.resolvedQuality = CombatEquipmentQuality.Normal;
        order.completionEffectsPublished = true;
        order.outputOperationId = CombatEquipmentCraftOutputOutbox
            .FormatOperationId(order.orderId, order.qualityAttemptIndex);
        order.outputItemId = CombatItemDefinitions.ArrowItemId;
        order.outputQuantity = 20;
        const string outputDestination = "production-output:building:qa";
        bool firstOutput = CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                order,
                gateway,
                Vector2Int.zero,
                outputDestination,
                out string firstOutputFailure);
        int firstOutputCount = CountCommittedOutput(
            gateway,
            order.outputCommitId);
        bool replayOutput = CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                order,
                gateway,
                Vector2Int.zero,
                outputDestination,
                out string replayOutputFailure);
        int replayOutputCount = CountCommittedOutput(
            gateway,
            order.outputCommitId);
        if (!firstOutput
            || !order.outputPublished
            || firstOutputCount != 20
            || !replayOutput
            || replayOutputCount != 20)
        {
            return Fail(
                "generic output exact-once publication"
                + $":first={firstOutput}/{firstOutputFailure}"
                + $",published={order.outputPublished}"
                + $",firstCount={firstOutputCount}"
                + $",replay={replayOutput}/{replayOutputFailure}"
                + $",replayCount={replayOutputCount}"
                + $",commit={order.outputCommitId}");
        }

        if (CombatEquipmentCraftMaterialOutbox.TryAcknowledgeOutcome(
                order,
                Requirements,
                gateway,
                out _)
            || order.materialTransferAcknowledged)
        {
            return Fail("injected material acknowledgement fault");
        }

        PhysicalItemRestoreCandidateDispositionSnapshot candidate = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            order.materialTransferRequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        CombatEquipmentCraftOrderSaveData restored =
            JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                JsonUtility.ToJson(order));
        if (!CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                restored,
                Requirements,
                gateway.GetAllStacks(),
                gateway,
                out _)
            || !CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                restored,
                gateway,
                Vector2Int.zero,
                outputDestination,
                out _)
            || !CombatEquipmentCraftMaterialOutbox.TryAcknowledgeOutcome(
                restored,
                Requirements,
                gateway,
                out _)
            || !restored.materialTransferAcknowledged
            || CountCommittedOutput(gateway, restored.outputCommitId) != 20
            || gateway.Quantity(lumberA) != 0
            || gateway.Quantity(lumberB) != 0
            || gateway.Quantity(iron) != 0)
        {
            return Fail("JSON replay acknowledgement/output recovery");
        }

        Validate(new[] { order }, candidate);
        if (!Reject(new[] { order })
            || !Reject(Array.Empty<CombatEquipmentCraftOrderSaveData>(), candidate)
            || !Reject(new[] { order }, Copy(candidate, mass: candidate.InputMassGrams + 1))
            || !Reject(new[] { order }, Copy(candidate, fingerprint: candidate.RequestFingerprint + ":bad")))
        {
            return Fail("incoming material receipt join rejection");
        }
        Validate(new[] { restored });
        return Reject(new[] { restored }, candidate)
            || Fail("terminal owner accepted stale incoming receipt");
    }

    private static bool Fail(string reason)
    {
        LastFailureReason = reason ?? "unknown";
        return false;
    }

    private static bool VerifyMissingInputAtomic(IDungeonItemCatalogProvider catalog)
    {
        FixtureGateway gateway = new(catalog);
        CombatEquipmentCraftOrderSaveData order = CreateOrder("missing");
        string lumber = gateway.Add("material:lumber", 2, order.materialDestinationId);
        return !CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                order,
                Requirements,
                gateway.GetAllStacks(),
                gateway,
                out _)
            && gateway.Quantity(lumber) == 2
            && string.IsNullOrEmpty(order.materialTransferOperationId);
    }

    private static bool VerifyRejectedDismantleReplay(
        IDungeonItemCatalogProvider catalog)
    {
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        CombatEquipmentCraftOrderSaveData order = CreateOrder("dismantle");
        order.dismantlingRejectedOutput = true;
        order.materialsReady = true;
        order.rejectedInstanceId = "equipment:rejected:qa";
        order.rejectedStackId = gateway.AddUnique(
            "material:iron-ingot",
            order.rejectedInstanceId,
            WorldItemStackState.FacilityOutputBuffer,
            "production-output:building:qa");
        order.recoveryOutputs.Add(new CombatCraftRecoveryOutputSaveData
        {
            itemId = "material:lumber",
            amount = 1
        });
        order.spawnedRecoveryAmounts.Add(0);
        if (!CombatEquipmentRejectedDismantleOutbox.TryCommitOrResume(
                order,
                gateway,
                out _)
            || gateway.Quantity(order.rejectedStackId) != 0
            || !gateway.TryGetPendingBatchPhysicalDisposition(
                order.rejectedDismantleOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            return false;
        }
        string recoveryOperation = CombatEquipmentRejectedDismantleOutbox
            .FormatRecoveryOperationId(order.orderId, 0, 0);
        if (!CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                "material:lumber",
                1,
                recoveryOperation,
                gateway,
                Vector2Int.zero,
                "production-output:building:qa",
                out string recoveryCommit,
                out _))
        {
            return false;
        }
        order.spawnedRecoveryAmounts[0] = 1;
        order.rejectedRecoveryPublished = true;
        if (CombatEquipmentRejectedDismantleOutbox.TryAcknowledgeRecovery(
                order,
                gateway,
                out _))
        {
            return false;
        }
        PhysicalItemRestoreCandidateDispositionSnapshot candidate = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            order.rejectedDismantleRequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        CombatEquipmentCraftOrderSaveData restored =
            JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                JsonUtility.ToJson(order));
        if (!CombatEquipmentRejectedDismantleOutbox.TryCommitOrResume(
                restored,
                gateway,
                out _)
            || !CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                "material:lumber",
                1,
                recoveryOperation,
                gateway,
                Vector2Int.zero,
                "production-output:building:qa",
                out string replayCommit,
                out _)
            || !string.Equals(
                recoveryCommit,
                replayCommit,
                StringComparison.Ordinal)
            || !CombatEquipmentRejectedDismantleOutbox.TryAcknowledgeRecovery(
                restored,
                gateway,
                out _)
            || !restored.rejectedDismantleAcknowledged
            || CountCommittedOutput(gateway, recoveryCommit) != 1)
        {
            return false;
        }
        Validate(new[] { order }, candidate);
        Validate(new[] { restored });
        return Reject(new[] { order })
            && Reject(Array.Empty<CombatEquipmentCraftOrderSaveData>(), candidate)
            && Reject(new[] { restored }, candidate);
    }

    private static CombatEquipmentCraftOrderSaveData CreateOrder(string suffix) =>
        new()
        {
            orderId = "combat-craft:" + suffix,
            definitionId = CombatItemDefinitions.ArrowBundleRecipeId,
            materialDestinationId = "facility-input:combat-craft:" + suffix,
            facilityPersistentId = "building:qa",
            requiredWork = 4f,
            craftWorkPerAttempt = 4f,
            qualityAttemptIndex = 0
        };

    private static int CountCommittedOutput(
        FixtureGateway gateway,
        string commitId) => gateway.GetAllStacks()
        .Where(stack => ProductionOutputCommitComponentCodec.Matches(
            stack.Components,
            commitId))
        .Sum(stack => stack.Quantity);

    private static void Validate(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts) =>
        CombatEquipmentCraftMaterialRestoreGuard.ValidateOwnerSet(
            orders,
            GetRequirements,
            new CandidateQuery(receipts));

    private static bool Reject(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
    {
        try
        {
            Validate(orders, receipts);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool GetRequirements(
        CombatEquipmentCraftOrderSaveData order,
        out IReadOnlyDictionary<string, int> requirements)
    {
        requirements = Requirements;
        return order != null;
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot Copy(
        PhysicalItemRestoreCandidateDispositionSnapshot value,
        string fingerprint = null,
        long? mass = null) => new(
        value.Kind,
        value.OperationId,
        value.ReasonCode,
        fingerprint ?? value.RequestFingerprint,
        value.SourceStackIds,
        value.Quantity,
        mass ?? value.InputMassGrams,
        value.CommitId);

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            values;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values) =>
            this.values = values ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => values;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot value)
        {
            value = values.FirstOrDefault(candidate => string.Equals(
                candidate.OperationId,
                operationId,
                StringComparison.Ordinal));
            return value != null;
        }
    }

    private sealed class FixtureGateway : IEquipmentPhysicalItemGateway
    {
        private readonly WorldItemRepository repository;
        private readonly WorldItemQueryService query;
        private readonly IPhysicalItemBatchDispositionService dispositions;

        internal FixtureGateway(IDungeonItemCatalogProvider catalog)
        {
            repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            PhysicalItemMassQuery mass = new(catalog);
            query = new WorldItemQueryService(
                catalog,
                mass,
                repository,
                EditorNullItemMarkerPresenter.Instance);
            dispositions = new PhysicalItemBatchDispositionService(
                repository,
                mass,
                EditorNullItemMarkerPresenter.Instance);
        }

        internal bool FailNextAcknowledgement { get; set; }

        internal string Add(string itemId, int quantity, string destinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                quantity,
                WorldItemStackState.FacilityBuffer,
                destinationId: destinationId);

        internal string AddUnique(
            string itemId,
            string itemInstanceId,
            WorldItemStackState state,
            string destinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                1,
                state,
                destinationId: destinationId,
                itemInstanceId: itemInstanceId);

        internal int Quantity(string stackId) =>
            repository.GetEditorTestQuantity(stackId);

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            query.GetAllStacks();

        public bool SpawnItemAtWithComponents(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components,
            out int spawned)
        {
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                amount,
                state,
                destinationId: destinationId,
                position: position,
                components: components);
            spawned = amount;
            return true;
        }

        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => dispositions.TryCommitPending(
            inputs, kind, operationId, reasonCode, out receipt, out failureReason);

        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            dispositions.TryGetPending(operationId, out receipt);

        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-acknowledgement-failure";
                return false;
            }
            return dispositions.Acknowledge(commitId, out failureReason);
        }

        public bool SpawnItemAt(string itemId, int amount, Vector2Int position,
            WorldItemStackState state, string destinationId, out int spawned) =>
            SpawnItemAtWithComponents(itemId, amount, position, state,
                destinationId, Array.Empty<ItemInstanceComponentSaveData>(), out spawned);
        public bool SpawnExistingUniqueItemAt(string itemId, ItemInstanceId itemInstanceId,
            Vector2Int position, WorldItemStackState state, string destinationId,
            out string stackId)
        {
            stackId = WorldItemRepositoryEditorAccess.AddStack(repository, itemId, 1,
                state, destinationId: destinationId, position: position,
                itemInstanceId: itemInstanceId.Value);
            return true;
        }
        public bool TryAbsorbUniqueItemStack(string stackId, ItemInstanceId expectedInstanceId) => false;
        public bool TryRequestItemDelivery(string itemId, int amount,
            Vector2Int destinationPosition, string destinationId, out int requested,
            out string failureReason) { requested = 0; failureReason = "not-used"; return false; }
        public bool TryConsumeFacilityItemBuffer(string destinationId,
            IReadOnlyDictionary<string, int> costs, out string failureReason)
        { failureReason = "forbidden"; return false; }
        public bool DeleteStack(string stackId) =>
            WorldItemRepositoryEditorAccess.TryRemoveStack(repository, stackId);
        public bool TryConsumeStackQuantity(string stackId, int quantity,
            out WorldItemStackSnapshot consumed) { consumed = null; return false; }
        public bool TryCommitBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind, string operationId, string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt, out string failureReason) =>
            dispositions.TryCommit(inputs, kind, operationId, reasonCode,
                out receipt, out failureReason);
        public bool TrySetInstanceComponent(string stackId,
            ItemInstanceComponentSaveData component) => false;
        public bool TryRemoveInstanceComponent(string stackId, string componentTypeId) => false;
        public int ReleaseStacksByDestination(string destinationId,
            Vector2Int releasePosition) => 0;
    }
}
