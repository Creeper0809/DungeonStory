using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CombatEquipmentCraftMaterialRestoreGuard :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "215.world.combat-equipment-craft-materials";
    private readonly CombatEquipmentCraftingRuntime crafting;
    private readonly CombatEquipmentRuntimeStateStore stateStore;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IPhysicalItemRestoreCandidateOutputQuery outputCandidates;
    private bool active;
    private bool published;

    public CombatEquipmentCraftMaterialRestoreGuard(
        CombatEquipmentCraftingRuntime crafting,
        CombatEquipmentRuntimeStateStore stateStore,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IPhysicalItemRestoreCandidateOutputQuery outputCandidates)
    {
        this.crafting = crafting
            ?? throw new ArgumentNullException(nameof(crafting));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.outputCandidates = outputCandidates
            ?? throw new ArgumentNullException(nameof(outputCandidates));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
        {
            throw new InvalidOperationException(
                "Combat craft material restore validation is already active.");
        }
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
        {
            throw new InvalidOperationException(
                "Combat craft material restore validation is not ready to publish.");
        }
        ValidateOwnerSet(
            crafting.Queue,
            stateStore.Current.CraftTerminalEffects.Values.ToArray(),
            (CombatEquipmentCraftOrderSaveData order,
                out IReadOnlyDictionary<string, int> requirements) =>
                crafting.TryGetConcreteMaterials(order, out requirements),
            physicalCandidates,
            outputCandidates);
        published = true;
    }

    public void RollbackPublishedRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public void CompleteRestoreCandidate()
    {
        if (!active || !published)
        {
            throw new InvalidOperationException(
                "Combat craft material restore validation cannot complete.");
        }
        active = false;
        published = false;
    }

    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public static void ValidateOwnerSet(
        CombatEquipmentCraftingRuntime crafting,
        IPhysicalItemRestoreCandidateQuery query,
        IPhysicalItemRestoreCandidateOutputQuery outputQuery = null)
    {
        if (crafting == null)
        {
            throw new ArgumentNullException(nameof(crafting));
        }
        ValidateOwnerSet(
            crafting.Queue,
            Array.Empty<CombatEquipmentCraftTerminalEffectSaveData>(),
            (CombatEquipmentCraftOrderSaveData order,
                out IReadOnlyDictionary<string, int> requirements) =>
                crafting.TryGetConcreteMaterials(order, out requirements),
            query,
            outputQuery ?? EmptyPhysicalItemRestoreCandidateOutputQuery.Instance);
    }

    public delegate bool TryGetCraftRequirements(
        CombatEquipmentCraftOrderSaveData order,
        out IReadOnlyDictionary<string, int> requirements);

    public static void ValidateOwnerSet(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        TryGetCraftRequirements getRequirements,
        IPhysicalItemRestoreCandidateQuery query) => ValidateOwnerSet(
        orders,
        Array.Empty<CombatEquipmentCraftTerminalEffectSaveData>(),
        getRequirements,
        query,
        EmptyPhysicalItemRestoreCandidateOutputQuery.Instance);

    public static void ValidateOwnerSet(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        TryGetCraftRequirements getRequirements,
        IPhysicalItemRestoreCandidateQuery query,
        IPhysicalItemRestoreCandidateOutputQuery outputQuery) => ValidateOwnerSet(
        orders,
        Array.Empty<CombatEquipmentCraftTerminalEffectSaveData>(),
        getRequirements,
        query,
        outputQuery);

    public static void ValidateOwnerSet(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        IReadOnlyList<CombatEquipmentCraftTerminalEffectSaveData> terminalEffects,
        TryGetCraftRequirements getRequirements,
        IPhysicalItemRestoreCandidateQuery query) => ValidateOwnerSet(
        orders,
        terminalEffects,
        getRequirements,
        query,
        EmptyPhysicalItemRestoreCandidateOutputQuery.Instance);

    public static void ValidateOwnerSet(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        IReadOnlyList<CombatEquipmentCraftTerminalEffectSaveData> terminalEffects,
        TryGetCraftRequirements getRequirements,
        IPhysicalItemRestoreCandidateQuery query,
        IPhysicalItemRestoreCandidateOutputQuery outputQuery)
    {
        if (getRequirements == null)
        {
            throw new ArgumentNullException(nameof(getRequirements));
        }
        Dictionary<string, CombatEquipmentCraftOrderSaveData> pending =
            new(StringComparer.Ordinal);
        HashSet<string> acknowledged = new(StringComparer.Ordinal);
        Dictionary<string, CombatEquipmentCraftOrderSaveData> pendingDismantles =
            new(StringComparer.Ordinal);
        HashSet<string> acknowledgedDismantles = new(StringComparer.Ordinal);
        Dictionary<string, CombatEquipmentCraftTerminalEffectSaveData>
            terminalBySource = new(StringComparer.Ordinal);
        Dictionary<string, CombatEquipmentCraftTerminalEffectSaveData>
            terminalMaterialOperations = new(StringComparer.Ordinal);
        Dictionary<string, CombatEquipmentCraftTerminalEffectSaveData>
            terminalDismantleOperations = new(StringComparer.Ordinal);
        Dictionary<string, CombatEquipmentCraftOrderSaveData>
            terminalFrozenBySource = new(StringComparer.Ordinal);
        foreach (CombatEquipmentCraftTerminalEffectSaveData terminal in
                 terminalEffects
                     ?? Array.Empty<CombatEquipmentCraftTerminalEffectSaveData>())
        {
            if (terminal == null
                || !terminalBySource.TryAdd(terminal.sourceId, terminal))
            {
                throw new InvalidOperationException(
                    "Duplicate or null combat craft terminal restore row.");
            }
            CombatEquipmentCraftOrderSaveData frozen;
            try
            {
                frozen = UnityEngine.JsonUtility.FromJson<
                    CombatEquipmentCraftOrderSaveData>(
                    terminal.frozenSourcePayload);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Combat craft terminal restore payload is invalid.",
                    exception);
            }
            if (frozen == null
                || !string.Equals(
                    frozen.orderId,
                    terminal.sourceId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat craft terminal restore source drifted.");
            }
            if (!terminalFrozenBySource.TryAdd(frozen.orderId, frozen))
            {
                throw new InvalidOperationException(
                    "Duplicate terminal combat craft frozen source.");
            }
            if (!string.IsNullOrEmpty(frozen.materialTransferOperationId)
                && !terminalMaterialOperations.TryAdd(
                    frozen.materialTransferOperationId,
                    terminal))
            {
                throw new InvalidOperationException(
                    "Duplicate terminal combat craft material operation.");
            }
            if (!string.IsNullOrEmpty(frozen.rejectedDismantleOperationId)
                && !terminalDismantleOperations.TryAdd(
                    frozen.rejectedDismantleOperationId,
                    terminal))
            {
                throw new InvalidOperationException(
                    "Duplicate terminal rejected dismantle operation.");
            }
        }
        foreach (CombatEquipmentCraftOrderSaveData order in
                 orders ?? Array.Empty<CombatEquipmentCraftOrderSaveData>())
        {
            if (order == null
                || string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                continue;
            }
            string failureReason = string.Empty;
            if (!getRequirements(order, out var requirements)
                || !CombatEquipmentCraftMaterialOutbox.ValidateProvenance(
                    order,
                    requirements,
                    out failureReason))
            {
                throw new InvalidOperationException(
                    "Invalid combat craft material owner: " + failureReason);
            }
            if (order.materialTransferAcknowledged)
            {
                if (!acknowledged.Add(order.materialTransferOperationId))
                {
                    throw new InvalidOperationException(
                        "Duplicate acknowledged combat craft operation: "
                        + order.materialTransferOperationId);
                }
            }
            else if (!pending.TryAdd(order.materialTransferOperationId, order))
            {
                throw new InvalidOperationException(
                    "Duplicate pending combat craft operation: "
                    + order.materialTransferOperationId);
            }
        }

        foreach (CombatEquipmentCraftOrderSaveData order in
                 orders ?? Array.Empty<CombatEquipmentCraftOrderSaveData>())
        {
            if (order == null
                || string.IsNullOrEmpty(order.rejectedDismantleOperationId))
            {
                continue;
            }
            if (!CombatEquipmentRejectedDismantleOutbox.ValidateProvenance(
                    order,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Invalid rejected-equipment dismantle owner: "
                    + failureReason);
            }
            if (order.rejectedDismantleAcknowledged)
            {
                if (!acknowledgedDismantles.Add(
                        order.rejectedDismantleOperationId))
                {
                    throw new InvalidOperationException(
                        "Duplicate acknowledged rejected dismantle operation: "
                        + order.rejectedDismantleOperationId);
                }
            }
            else if (!pendingDismantles.TryAdd(
                         order.rejectedDismantleOperationId,
                         order))
            {
                throw new InvalidOperationException(
                    "Duplicate pending rejected dismantle operation: "
                    + order.rejectedDismantleOperationId);
            }
        }

        if (acknowledged.Overlaps(pending.Keys))
        {
            throw new InvalidOperationException(
                "Combat craft operation is both pending and acknowledged.");
        }
        if (acknowledgedDismantles.Overlaps(pendingDismantles.Keys))
        {
            throw new InvalidOperationException(
                "Rejected dismantle operation is both pending and acknowledged.");
        }
        foreach (CombatEquipmentCraftTerminalEffectSaveData terminal in
                 terminalBySource.Values)
        {
            CombatEquipmentCraftOrderSaveData live = (orders
                    ?? Array.Empty<CombatEquipmentCraftOrderSaveData>())
                .SingleOrDefault(value => value != null && string.Equals(
                    value.orderId,
                    terminal.sourceId,
                    StringComparison.Ordinal));
            bool removed = terminal.phase ==
                CombatEquipmentCraftTerminalEffectPhase.SourceRemoved;
            if (removed == (live != null)
                || live != null && !string.Equals(
                    UnityEngine.JsonUtility.ToJson(live),
                    terminal.frozenSourcePayload,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat craft terminal/live source restore join is invalid.");
            }
        }
        if (query == null || !query.IsCandidateAvailable)
        {
            if (pending.Count == 0 && pendingDismantles.Count == 0
                && terminalMaterialOperations.Count == 0
                && terminalDismantleOperations.Count == 0)
            {
                return;
            }
            throw new InvalidOperationException(
                "Combat craft restore requires the incoming physical candidate.");
        }

        foreach (CombatEquipmentCraftOrderSaveData order in pending.Values)
        {
            bool takeover = terminalBySource.TryGetValue(
                order.orderId,
                out CombatEquipmentCraftTerminalEffectSaveData terminal);
            bool hasReceipt = query.TryGetPendingBatchDisposition(
                order.materialTransferOperationId,
                out PhysicalItemRestoreCandidateDispositionSnapshot receipt);
            if (takeover && !hasReceipt)
            {
                // The same-aggregate WIP row is durable before acknowledgement.
                // Missing physical authority is therefore the exact crash-ahead
                // state recovered by the terminal authority.
                continue;
            }
            if (takeover && terminal.phase >=
                CombatEquipmentCraftTerminalEffectPhase
                    .InputDispositionAcknowledgedAwaitingDestinationClose
                && hasReceipt)
            {
                throw new InvalidOperationException(
                    "Acknowledged terminal craft WIP still has a pending receipt: "
                    + order.materialTransferOperationId);
            }
            string[] sourceIds = order.materialTransferInputs
                .Select(input => input.sourceStackId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!hasReceipt
                || receipt.Kind != PhysicalItemDispositionKind.Transfer
                || !string.Equals(
                    receipt.ReasonCode,
                    CombatEquipmentCraftMaterialOutbox.ReasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.CommitId,
                    order.materialTransferCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.RequestFingerprint,
                    order.materialTransferRequestFingerprint,
                    StringComparison.Ordinal)
                || receipt.Quantity != order.materialTransferInputs.Sum(input => input.quantity)
                || receipt.InputMassGrams != order.materialTransferMassGrams
                || !receipt.SourceStackIds.SequenceEqual(
                    sourceIds,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat craft order has no exact incoming material Transfer receipt: "
                    + order.materialTransferOperationId);
            }
        }

        foreach (CombatEquipmentCraftOrderSaveData order in
                 pendingDismantles.Values)
        {
            bool takeover = terminalBySource.TryGetValue(
                order.orderId,
                out CombatEquipmentCraftTerminalEffectSaveData terminal);
            bool hasReceipt = query.TryGetPendingBatchDisposition(
                order.rejectedDismantleOperationId,
                out PhysicalItemRestoreCandidateDispositionSnapshot receipt);
            if (takeover && !hasReceipt)
                continue;
            if (takeover && terminal.phase >=
                CombatEquipmentCraftTerminalEffectPhase
                    .InputDispositionAcknowledgedAwaitingDestinationClose
                && hasReceipt)
            {
                throw new InvalidOperationException(
                    "Acknowledged terminal rejected WIP still has a pending receipt: "
                    + order.rejectedDismantleOperationId);
            }
            if (!hasReceipt
                || receipt.Kind != PhysicalItemDispositionKind.Transfer
                || !string.Equals(
                    receipt.ReasonCode,
                    CombatEquipmentRejectedDismantleOutbox.ReasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.CommitId,
                    order.rejectedDismantleCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.RequestFingerprint,
                    order.rejectedDismantleRequestFingerprint,
                    StringComparison.Ordinal)
                || receipt.Quantity != 1
                || receipt.InputMassGrams
                    != order.rejectedDismantleInputMassGrams
                || !receipt.SourceStackIds.SequenceEqual(
                    new[] { order.rejectedStackId },
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Rejected dismantle order has no exact incoming Transfer receipt: "
                    + order.rejectedDismantleOperationId);
            }
        }

        ValidateRecoveryOutputs(
            orders,
            terminalFrozenBySource.Values,
            outputQuery);

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || (!receipt.OperationId.StartsWith(
                        CombatEquipmentCraftMaterialOutbox.OperationPrefix,
                        StringComparison.Ordinal)
                    && !receipt.OperationId.StartsWith(
                        CombatEquipmentRejectedDismantleOutbox.OperationPrefix,
                        StringComparison.Ordinal)))
            {
                continue;
            }
            if (acknowledged.Contains(receipt.OperationId)
                || acknowledgedDismantles.Contains(receipt.OperationId))
            {
                throw new InvalidOperationException(
                    "Acknowledged combat craft owner still has an incoming receipt: "
                    + receipt.OperationId);
            }
            if (terminalMaterialOperations.TryGetValue(
                    receipt.OperationId,
                    out CombatEquipmentCraftTerminalEffectSaveData
                        terminalMaterial)
                && terminalMaterial.phase >=
                    CombatEquipmentCraftTerminalEffectPhase
                        .InputDispositionAcknowledgedAwaitingDestinationClose
                || terminalDismantleOperations.TryGetValue(
                    receipt.OperationId,
                    out CombatEquipmentCraftTerminalEffectSaveData
                        terminalDismantle)
                && terminalDismantle.phase >=
                    CombatEquipmentCraftTerminalEffectPhase
                        .InputDispositionAcknowledgedAwaitingDestinationClose)
            {
                throw new InvalidOperationException(
                    "Terminal combat WIP receipt survived owner acknowledgement: "
                    + receipt.OperationId);
            }
            if (!pending.ContainsKey(receipt.OperationId)
                && !pendingDismantles.ContainsKey(receipt.OperationId))
            {
                throw new InvalidOperationException(
                    "Incoming combat craft material Transfer has no order owner: "
                    + receipt.OperationId);
            }
        }
    }

    private static void ValidateRecoveryOutputs(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> liveOrders,
        IEnumerable<CombatEquipmentCraftOrderSaveData> terminalOrders,
        IPhysicalItemRestoreCandidateOutputQuery query)
    {
        if (query == null || !query.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Combat rejected-recovery output restore candidate is unavailable.");
        }
        Dictionary<string, CombatEquipmentCraftOrderSaveData> owners = new(
            StringComparer.Ordinal);
        foreach (CombatEquipmentCraftOrderSaveData order in liveOrders
                     ?? Array.Empty<CombatEquipmentCraftOrderSaveData>())
        {
            if (order?.dismantlingRejectedOutput == true)
                owners.Add(order.orderId, order);
        }
        foreach (CombatEquipmentCraftOrderSaveData order in terminalOrders
                     ?? Array.Empty<CombatEquipmentCraftOrderSaveData>())
        {
            if (order?.dismantlingRejectedOutput == true
                && !owners.ContainsKey(order.orderId))
            {
                owners.Add(order.orderId, order);
            }
        }

        HashSet<string> expectedCommits = new(StringComparer.Ordinal);
        foreach (CombatEquipmentCraftOrderSaveData order in owners.Values
                     .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            long committedMass = 0L;
            bool allCommitted = true;
            for (int index = 0; index < order.recoveryOutputs.Count; index++)
            {
                CombatCraftRecoveryOutputSaveData output =
                    order.recoveryOutputs[index];
                string operation = CombatEquipmentRejectedDismantleOutbox
                    .FormatRecoveryOperationId(
                        order.orderId,
                        order.qualityAttemptIndex,
                        index);
                string commit = CombatEquipmentCraftOutputOutbox.FormatCommitId(
                    operation,
                    output.itemId,
                    output.amount);
                if (!expectedCommits.Add(commit))
                {
                    throw new InvalidOperationException(
                        "Duplicate combat rejected-recovery output commit: "
                        + commit);
                }
                bool hasOutput = query.TryGetCommittedOutput(
                    commit,
                    out IReadOnlyList<
                        PhysicalItemRestoreCandidateOutputSnapshot> rows);
                PhysicalItemRestoreCandidateOutputSnapshot[] snapshots = (rows
                        ?? Array.Empty<
                            PhysicalItemRestoreCandidateOutputSnapshot>())
                    .Where(value => value != null)
                    .ToArray();
                int recorded = index < order.spawnedRecoveryAmounts.Count
                    ? order.spawnedRecoveryAmounts[index]
                    : 0;
                bool exact = hasOutput
                    && snapshots.Length > 0
                    && snapshots.All(value => string.Equals(
                            value.CommitId,
                            commit,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.ItemId,
                            output.itemId,
                            StringComparison.Ordinal)
                        && value.Quantity > 0
                        && value.MassGrams > 0L
                        && value.State
                            == WorldItemStackState.FacilityOutputBuffer
                        && value.Position == new UnityEngine.Vector2Int(
                            order.destinationX,
                            order.destinationY)
                        && string.Equals(
                            value.DestinationId,
                            ProductionBillRuntime.OutputDestinationPrefix
                            + order.facilityPersistentId,
                            StringComparison.Ordinal))
                    && snapshots.Sum(value => (long)value.Quantity)
                        == output.amount;
                if (hasOutput != exact
                    || recorded != 0 && recorded != output.amount
                    || recorded == output.amount && !exact)
                {
                    throw new InvalidOperationException(
                        "Combat rejected-recovery output conflicts with its owner: "
                        + commit);
                }
                allCommitted &= exact;
                if (exact)
                {
                    foreach (PhysicalItemRestoreCandidateOutputSnapshot row in
                             snapshots)
                    {
                        committedMass = checked(committedMass + row.MassGrams);
                    }
                }
            }
            if (committedMass > order.rejectedRecoveryOutputMassGrams
                || order.rejectedRecoveryPublished
                && (!allCommitted
                    || committedMass
                        != order.rejectedRecoveryOutputMassGrams))
            {
                throw new InvalidOperationException(
                    "Combat rejected-recovery committed mass conflicts with its frozen projection: "
                    + order.orderId);
            }
        }

        string recoveryCommitPrefix = "physical-source:"
            + CombatEquipmentRejectedDismantleOutbox.RecoveryOperationPrefix;
        foreach (PhysicalItemRestoreCandidateOutputSnapshot output in
                 query.CommittedOutputs
                     ?? Array.Empty<PhysicalItemRestoreCandidateOutputSnapshot>())
        {
            if (output != null
                && output.CommitId.StartsWith(
                    recoveryCommitPrefix,
                    StringComparison.Ordinal)
                && !expectedCommits.Contains(output.CommitId))
            {
                throw new InvalidOperationException(
                    "Orphan combat rejected-recovery output: "
                    + output.CommitId);
            }
        }
    }
}
