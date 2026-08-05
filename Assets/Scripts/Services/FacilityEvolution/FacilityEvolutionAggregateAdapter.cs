using System;
using System.Collections.Generic;
using System.Linq;
using Domain = DungeonStory.FacilityEvolution;

public sealed class FacilityEvolutionPreparedState
{
    public FacilityEvolutionPreparedState(
        FacilityEvolutionStateSnapshot serializableSnapshot,
        Domain.FacilityEvolutionRestoreCandidate candidate)
    {
        SerializableSnapshot = serializableSnapshot
            ?? throw new ArgumentNullException(nameof(serializableSnapshot));
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    public FacilityEvolutionStateSnapshot SerializableSnapshot { get; }
    public Domain.FacilityEvolutionRestoreCandidate Candidate { get; }
}

public static class FacilityEvolutionAggregateAdapter
{
    public static FacilityEvolutionPreparedState Prepare(
        FacilityEvolutionStateSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new InvalidOperationException("Facility evolution state payload was empty.");
        }
        if (!snapshot.hasRecordSnapshot)
        {
            throw new InvalidOperationException(
                "Facility evolution V3 state requires its record aggregate.");
        }

        Domain.FacilityEvolutionAggregateSnapshot domain = ToDomain(snapshot);
        return new FacilityEvolutionPreparedState(
            snapshot,
            Domain.FacilityEvolutionRestoreRules.Prepare(domain));
    }

    public static Domain.FacilityEvolutionAggregateSnapshot ToDomain(
        FacilityEvolutionStateSnapshot snapshot)
    {
        FacilityEvolutionState instance = snapshot.instanceEvolution
            ?? throw new InvalidOperationException("Facility instance evolution state is missing.");
        FacilityEvolutionRecordSnapshotBuilder record = new FacilityEvolutionRecordSnapshotBuilder(snapshot);

        return new Domain.FacilityEvolutionAggregateSnapshot(
            new Domain.FacilityDefinitionId(snapshot.baseFacilityId),
            new Domain.FacilityDefinitionId(snapshot.currentFacilityId),
            snapshot.starGrade,
            snapshot.lineageTags,
            snapshot.mutationTags,
            ToFloatMap(snapshot.lastIdentityPressures),
            (snapshot.evolutionHistory ?? new List<FacilityEvolutionHistoryEntry>())
                .Where(entry => entry != null)
                .Select(entry => $"{entry.sequence}:{entry.evolutionId}"),
            record.Build(),
            ToInstance(instance));
    }

    private static Domain.FacilityInstanceEvolutionSnapshot ToInstance(
        FacilityEvolutionState state)
    {
        UsageLedger ledger = state.usageLedger ?? new UsageLedger();
        return new Domain.FacilityInstanceEvolutionSnapshot(
            new BuildingInstanceId(state.facilityPersistentId),
            state.generation,
            state.mastery,
            ledger.nextSequence,
            (ledger.currentGenerationEvents ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null)
                .Select(ToUsageEvent),
            (ledger.compactedSegments ?? new List<CompactedHistorySegment>())
                .Where(segment => segment != null)
                .Select(ToHistorySegment),
            (state.evolutionNodes ?? new List<EvolutionNode>())
                .Where(node => node != null)
                .Select(ToNode),
            (state.pendingCandidates ?? new List<FacilityGenerationCandidate>())
                .Where(candidate => candidate != null)
                .Select(ToCandidate),
            state.activeNodeIds,
            state.dormantNodeIds,
            (state.narrativeRequests ?? new List<EvolutionNarrativeRequestSnapshot>())
                .Where(request => request != null)
                .Select(request => request.requestKey),
            ToPendingWork(state));
    }

    private static Domain.FacilityUsageEventSnapshot ToUsageEvent(UsageLedgerEvent entry) =>
        new Domain.FacilityUsageEventSnapshot(
            entry.evidenceId,
            entry.eventId,
            entry.actorId,
            entry.targetId,
            entry.amount,
            entry.sequence,
            entry.sourceTags);

    private static Domain.FacilityHistorySegmentSnapshot ToHistorySegment(
        CompactedHistorySegment segment) =>
        new Domain.FacilityHistorySegmentSnapshot(
            segment.level,
            segment.firstGeneration,
            segment.lastGeneration,
            segment.eventCount,
            segment.totalMagnitude,
            segment.historyHash,
            (segment.metrics ?? new List<UsageLedgerMetric>())
                .Where(metric => metric != null)
                .ToDictionary(metric => metric.metricId, metric => metric.value, StringComparer.Ordinal),
            (segment.keyEvents ?? new List<UsageLedgerEvent>())
                .Where(entry => entry != null)
                .Select(ToUsageEvent),
            segment.participantIds,
            segment.sourceTags);

    private static Domain.FacilityEvolutionNodeSnapshot ToNode(EvolutionNode node) =>
        new Domain.FacilityEvolutionNodeSnapshot(
            node.nodeId,
            node.parentNodeId,
            node.effectId,
            node.burdenEffectId,
            node.generation,
            node.active,
            node.historical,
            node.evidenceIds);

    private static Domain.FacilityEvolutionCandidateSnapshot ToCandidate(
        FacilityGenerationCandidate candidate) =>
        new Domain.FacilityEvolutionCandidateSnapshot(
            candidate.candidateId,
            candidate.targetGeneration,
            candidate.benefitModuleId,
            candidate.burdenModuleId,
            candidate.catalystFamily,
            candidate.historyHash);

    private static Domain.FacilityEvolutionWorkSnapshot ToPendingWork(
        FacilityEvolutionState state)
    {
        // JsonUtility materializes null inline serializable classes as empty objects
        // during a JSON round trip.  Order identity, not CLR reference presence, is
        // therefore the persisted discriminator for this optional union.
        bool hasModification = HasOrderId(state.modificationOrder?.orderId);
        bool hasRecalibration = HasOrderId(state.recalibrationOrder?.orderId);
        bool hasRelocation = HasOrderId(state.relocationOrder?.orderId);
        RejectIncompleteOrder(state.modificationOrder, hasModification);
        RejectIncompleteOrder(state.recalibrationOrder, hasRecalibration);
        RejectIncompleteOrder(state.relocationOrder, hasRelocation);

        int count = (hasModification ? 1 : 0)
            + (hasRecalibration ? 1 : 0)
            + (hasRelocation ? 1 : 0);
        if (count > 1)
        {
            throw new InvalidOperationException(
                "A facility may have only one pending evolution work order.");
        }

        if (hasModification)
        {
            FacilityModificationOrder order = state.modificationOrder;
            return new Domain.FacilityEvolutionWorkSnapshot(
                new Domain.FacilityEvolutionOrderId(order.orderId),
                Domain.FacilityEvolutionWorkKind.Modification,
                ToPhase(order.state),
                new Domain.FacilityEvolutionItemId(
                    !string.IsNullOrWhiteSpace(order.catalystItemId)
                        ? order.catalystItemId
                        : order.bindingItemId),
                default,
                order.requiredWork,
                order.completedWork,
                default,
                new Domain.FacilityGridAddress(order.destinationX, order.destinationY),
                order.materialsConsumed);
        }

        if (hasRecalibration)
        {
            FacilityRecalibrationOrder order = state.recalibrationOrder;
            return new Domain.FacilityEvolutionWorkSnapshot(
                new Domain.FacilityEvolutionOrderId(order.orderId),
                Domain.FacilityEvolutionWorkKind.Recalibration,
                ToPhase(order.state),
                new Domain.FacilityEvolutionItemId(order.catalystItemId),
                default,
                order.requiredWork,
                order.completedWork,
                default,
                new Domain.FacilityGridAddress(order.destinationX, order.destinationY),
                order.materialsConsumed);
        }

        if (!hasRelocation)
        {
            return null;
        }

        FacilityRelocationOrder relocation = state.relocationOrder;
        bool dismantling = relocation.phase == FacilityRelocationPhase.Dismantling;
        return new Domain.FacilityEvolutionWorkSnapshot(
            new Domain.FacilityEvolutionOrderId(relocation.orderId),
            Domain.FacilityEvolutionWorkKind.Relocation,
            ToPhase(relocation.phase),
            new Domain.FacilityEvolutionItemId(relocation.packageItemId),
            new ItemStackId(relocation.packageStackId),
            dismantling ? relocation.dismantleRequiredWork : relocation.reinstallRequiredWork,
            dismantling ? relocation.dismantleCompletedWork : relocation.reinstallCompletedWork,
            new Domain.FacilityGridAddress(relocation.sourceX, relocation.sourceY),
            new Domain.FacilityGridAddress(relocation.destinationX, relocation.destinationY),
            relocation.packageConsumed);
    }

    private static bool HasOrderId(string orderId) =>
        !string.IsNullOrWhiteSpace(orderId);

    private static void RejectIncompleteOrder(
        FacilityModificationOrder order,
        bool hasOrderId)
    {
        if (order == null || hasOrderId)
        {
            return;
        }

        bool hasPayload = !string.IsNullOrWhiteSpace(order.facilityPersistentId)
            || !string.IsNullOrWhiteSpace(order.bindingItemId)
            || order.bindingAmount != 0
            || !string.IsNullOrWhiteSpace(order.catalystItemId)
            || order.catalystAmount != 0
            || order.requiredWork != 0f
            || order.completedWork != 0f
            || order.state != EvolutionReforgeOrderState.WaitingForMaterials
            || !string.IsNullOrWhiteSpace(order.destinationId)
            || order.destinationX != 0
            || order.destinationY != 0
            || order.materialsConsumed
            || HasCandidatePayload(order.candidate);
        if (hasPayload)
        {
            throw new InvalidOperationException(
                "Facility modification work has payload but no order ID.");
        }
    }

    private static void RejectIncompleteOrder(
        FacilityRecalibrationOrder order,
        bool hasOrderId)
    {
        if (order == null || hasOrderId)
        {
            return;
        }

        bool hasPayload = !string.IsNullOrWhiteSpace(order.facilityPersistentId)
            || !string.IsNullOrWhiteSpace(order.nodeId)
            || !string.IsNullOrWhiteSpace(order.catalystItemId)
            || order.catalystPotency != 0
            || order.requiredWork != 0f
            || order.completedWork != 0f
            || order.state != EvolutionReforgeOrderState.WaitingForMaterials
            || !string.IsNullOrWhiteSpace(order.destinationId)
            || order.destinationX != 0
            || order.destinationY != 0
            || order.materialsConsumed;
        if (hasPayload)
        {
            throw new InvalidOperationException(
                "Facility recalibration work has payload but no order ID.");
        }
    }

    private static void RejectIncompleteOrder(
        FacilityRelocationOrder order,
        bool hasOrderId)
    {
        if (order == null || hasOrderId)
        {
            return;
        }

        bool hasPayload = !string.IsNullOrWhiteSpace(order.facilityPersistentId)
            || !string.IsNullOrWhiteSpace(order.packageItemId)
            || !string.IsNullOrWhiteSpace(order.packageStackId)
            || !string.IsNullOrWhiteSpace(order.destinationId)
            || order.sourceX != 0
            || order.sourceY != 0
            || order.destinationX != 0
            || order.destinationY != 0
            || order.dismantleRequiredWork != 0f
            || order.dismantleCompletedWork != 0f
            || order.reinstallRequiredWork != 0f
            || order.reinstallCompletedWork != 0f
            || order.phase != FacilityRelocationPhase.Dismantling
            || order.packageConsumed;
        if (hasPayload)
        {
            throw new InvalidOperationException(
                "Facility relocation work has payload but no order ID.");
        }
    }

    private static bool HasCandidatePayload(FacilityGenerationCandidate candidate)
    {
        return candidate != null
            && (!string.IsNullOrWhiteSpace(candidate.candidateId)
                || candidate.targetGeneration != 0
                || !string.IsNullOrWhiteSpace(candidate.benefitModuleId)
                || !string.IsNullOrWhiteSpace(candidate.burdenModuleId)
                || !string.IsNullOrWhiteSpace(candidate.catalystFamily)
                || candidate.minimumCatalystProgressionLevel != 0
                || !string.IsNullOrWhiteSpace(candidate.historyHash));
    }

    private static Domain.FacilityEvolutionWorkPhase ToPhase(EvolutionReforgeOrderState state) =>
        state switch
        {
            EvolutionReforgeOrderState.WaitingForMaterials => Domain.FacilityEvolutionWorkPhase.WaitingForMaterials,
            EvolutionReforgeOrderState.Ready => Domain.FacilityEvolutionWorkPhase.Ready,
            EvolutionReforgeOrderState.InProgress => Domain.FacilityEvolutionWorkPhase.InProgress,
            EvolutionReforgeOrderState.Completed => Domain.FacilityEvolutionWorkPhase.Completed,
            EvolutionReforgeOrderState.Cancelled => Domain.FacilityEvolutionWorkPhase.Cancelled,
            _ => Domain.FacilityEvolutionWorkPhase.Blocked
        };

    private static Domain.FacilityEvolutionWorkPhase ToPhase(FacilityRelocationPhase phase) =>
        phase switch
        {
            FacilityRelocationPhase.Dismantling => Domain.FacilityEvolutionWorkPhase.Dismantling,
            FacilityRelocationPhase.WaitingForPackage => Domain.FacilityEvolutionWorkPhase.WaitingForPackage,
            FacilityRelocationPhase.Reinstalling => Domain.FacilityEvolutionWorkPhase.Reinstalling,
            _ => Domain.FacilityEvolutionWorkPhase.Blocked
        };

    private static Dictionary<string, float> ToFloatMap(
        IEnumerable<FacilityEvolutionValue> values) =>
        (values ?? Array.Empty<FacilityEvolutionValue>())
            .ToDictionary(entry => entry.key, entry => entry.value, StringComparer.Ordinal);

    private sealed class FacilityEvolutionRecordSnapshotBuilder
    {
        private readonly FacilityEvolutionStateSnapshot snapshot;
        public FacilityEvolutionRecordSnapshotBuilder(FacilityEvolutionStateSnapshot snapshot) =>
            this.snapshot = snapshot;

        public Domain.FacilityEvolutionRecordSnapshot Build() =>
            new Domain.FacilityEvolutionRecordSnapshot(
                ToFloatMap(snapshot.recordMetrics),
                (snapshot.recordTokens ?? Array.Empty<FacilityEvolutionTokenValue>())
                    .ToDictionary(entry => entry.key, entry => entry.count, StringComparer.Ordinal),
                snapshot.recordRecentEvents);
    }
}
