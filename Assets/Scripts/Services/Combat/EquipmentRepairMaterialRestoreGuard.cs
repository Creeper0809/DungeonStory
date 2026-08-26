using System;
using System.Collections.Generic;
using System.Linq;

public sealed class EquipmentRepairMaterialRestoreGuard :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "225.world.equipment-maintenance-materials";
    private readonly ICombatEquipmentMaintenanceRuntime maintenance;
    private readonly ICombatEquipmentRepairTerminalEffectQuery terminalEffects;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private bool active;
    private bool published;

    public EquipmentRepairMaterialRestoreGuard(
        ICombatEquipmentMaintenanceRuntime maintenance,
        ICombatEquipmentRepairTerminalEffectQuery terminalEffects,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.maintenance = maintenance
            ?? throw new ArgumentNullException(nameof(maintenance));
        this.terminalEffects = terminalEffects
            ?? throw new ArgumentNullException(nameof(terminalEffects));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
        {
            throw new InvalidOperationException(
                "Equipment repair material restore validation is already active.");
        }
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
        {
            throw new InvalidOperationException(
                "Equipment repair material restore validation is not ready to publish.");
        }
        ValidateOwnerSet(
            maintenance.Orders,
            terminalEffects.TerminalEffects,
            physicalCandidates);
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
                "Equipment repair material restore validation cannot complete.");
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
        IReadOnlyList<CombatEquipmentRepairOrder> orders,
        IPhysicalItemRestoreCandidateQuery query) => ValidateOwnerSet(
        orders,
        Array.Empty<CombatEquipmentRepairTerminalEffectSaveData>(),
        query);

    public static void ValidateOwnerSet(
        IReadOnlyList<CombatEquipmentRepairOrder> orders,
        IReadOnlyList<CombatEquipmentRepairTerminalEffectSaveData>
            terminalEffects,
        IPhysicalItemRestoreCandidateQuery query)
    {
        Dictionary<string, CombatEquipmentRepairOrder> pending =
            new(StringComparer.Ordinal);
        HashSet<string> acknowledged = new(StringComparer.Ordinal);
        Dictionary<string, CombatEquipmentRepairTerminalEffectSaveData>
            terminalBySource = new(StringComparer.Ordinal);
        Dictionary<string, CombatEquipmentRepairTerminalEffectSaveData>
            terminalOperations = new(StringComparer.Ordinal);
        foreach (CombatEquipmentRepairTerminalEffectSaveData terminal in
                 terminalEffects
                     ?? Array.Empty<CombatEquipmentRepairTerminalEffectSaveData>())
        {
            if (terminal == null
                || !terminalBySource.TryAdd(terminal.sourceId, terminal))
            {
                throw new InvalidOperationException(
                    "Duplicate or null equipment repair terminal restore row.");
            }
            CombatEquipmentRepairOrder frozen;
            try
            {
                frozen = UnityEngine.JsonUtility.FromJson<
                    CombatEquipmentRepairOrder>(terminal.frozenSourcePayload);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    "Equipment repair terminal restore payload is invalid.",
                    exception);
            }
            if (frozen == null
                || !string.Equals(
                    frozen.orderId,
                    terminal.sourceId,
                    StringComparison.Ordinal)
                || !string.IsNullOrEmpty(frozen.materialTransferOperationId)
                    && !terminalOperations.TryAdd(
                        frozen.materialTransferOperationId,
                        terminal))
            {
                throw new InvalidOperationException(
                    "Equipment repair terminal restore source drifted or duplicated.");
            }
        }
        foreach (CombatEquipmentRepairOrder order in
                 orders ?? Array.Empty<CombatEquipmentRepairOrder>())
        {
            if (order == null
                || string.IsNullOrEmpty(order.materialTransferOperationId))
            {
                continue;
            }
            if (!EquipmentRepairMaterialOutbox.ValidateProvenance(
                    order,
                    out string failureReason))
            {
                throw new InvalidOperationException(
                    "Invalid equipment repair material owner: "
                    + failureReason);
            }
            if (order.materialTransferAcknowledged)
            {
                if (!acknowledged.Add(order.materialTransferOperationId))
                {
                    throw new InvalidOperationException(
                        "Duplicate acknowledged equipment repair operation: "
                        + order.materialTransferOperationId);
                }
            }
            else if (!pending.TryAdd(
                         order.materialTransferOperationId,
                         order))
            {
                throw new InvalidOperationException(
                    "Duplicate pending equipment repair operation: "
                    + order.materialTransferOperationId);
            }
        }

        if (acknowledged.Overlaps(pending.Keys))
        {
            throw new InvalidOperationException(
                "Equipment repair operation is both pending and acknowledged.");
        }

        foreach (CombatEquipmentRepairTerminalEffectSaveData terminal in
                 terminalBySource.Values)
        {
            CombatEquipmentRepairOrder live = (orders
                    ?? Array.Empty<CombatEquipmentRepairOrder>())
                .SingleOrDefault(value => value != null && string.Equals(
                    value.orderId,
                    terminal.sourceId,
                    StringComparison.Ordinal));
            bool removed = terminal.phase ==
                CombatEquipmentRepairTerminalEffectPhase.SourceRemoved;
            if (removed == (live != null)
                || live != null && !string.Equals(
                    UnityEngine.JsonUtility.ToJson(live),
                    terminal.frozenSourcePayload,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Equipment repair terminal/live source restore join is invalid.");
            }
        }

        if (query == null || !query.IsCandidateAvailable)
        {
            if (pending.Count == 0 && terminalOperations.Count == 0)
            {
                return;
            }
            throw new InvalidOperationException(
                "Equipment repair restore requires the incoming physical candidate.");
        }

        foreach (CombatEquipmentRepairOrder order in pending.Values)
        {
            bool takeover = terminalBySource.TryGetValue(
                order.orderId,
                out CombatEquipmentRepairTerminalEffectSaveData terminal);
            bool hasReceipt = query.TryGetPendingBatchDisposition(
                order.materialTransferOperationId,
                out PhysicalItemRestoreCandidateDispositionSnapshot receipt);
            if (takeover && !hasReceipt)
            {
                // The same-aggregate terminal row is durable before physical
                // acknowledgement. Absence is the one legal crash-ahead state.
                continue;
            }
            if (takeover && terminal.phase >=
                CombatEquipmentRepairTerminalEffectPhase
                    .OwnerDispositionAcknowledgedAwaitingDestinationClose
                && hasReceipt)
            {
                throw new InvalidOperationException(
                    "Acknowledged terminal repair WIP still has a pending receipt: "
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
                    EquipmentRepairMaterialOutbox.ReasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.CommitId,
                    order.materialTransferCommitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.RequestFingerprint,
                    order.materialTransferRequestFingerprint,
                    StringComparison.Ordinal)
                || receipt.Quantity != order.requiredMaterialAmount
                || receipt.InputMassGrams
                    != order.materialTransferMassGrams
                || !receipt.SourceStackIds.SequenceEqual(
                    sourceIds,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Equipment repair order has no exact incoming material Transfer receipt: "
                    + order.materialTransferOperationId);
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    "equipment-repair-material:",
                    StringComparison.Ordinal))
            {
                continue;
            }
            if (acknowledged.Contains(receipt.OperationId))
            {
                throw new InvalidOperationException(
                    "Acknowledged equipment repair owner still has an incoming physical receipt: "
                    + receipt.OperationId);
            }
            if (terminalOperations.TryGetValue(
                    receipt.OperationId,
                    out CombatEquipmentRepairTerminalEffectSaveData terminal)
                && terminal.phase >=
                    CombatEquipmentRepairTerminalEffectPhase
                        .OwnerDispositionAcknowledgedAwaitingDestinationClose)
            {
                throw new InvalidOperationException(
                    "Terminal equipment repair WIP receipt survived owner acknowledgement: "
                    + receipt.OperationId);
            }
            if (!pending.ContainsKey(receipt.OperationId))
            {
                throw new InvalidOperationException(
                    "Incoming equipment repair material Transfer has no order owner: "
                    + receipt.OperationId);
            }
        }
    }
}
