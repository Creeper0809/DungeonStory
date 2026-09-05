using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
                "Facility evolution V4 state requires its record aggregate.");
        }

        ValidatePendingMaterialCommit(snapshot);
        ValidateModificationMaterialTransfer(
            snapshot.instanceEvolution?.modificationOrder);
        ValidateRelocationPackageTransfer(snapshot.instanceEvolution?.relocationOrder);
        ValidateRecalibrationMaterialTransfer(snapshot.instanceEvolution?.recalibrationOrder);
        ValidateInputOwnerProjection(snapshot.instanceEvolution);

        Domain.FacilityEvolutionAggregateSnapshot domain = ToDomain(snapshot);
        return new FacilityEvolutionPreparedState(
            snapshot,
            Domain.FacilityEvolutionRestoreRules.Prepare(domain));
    }

    public static void ValidateRecalibrationMaterialTransfer(FacilityRecalibrationOrder order)
    {
        if(order==null)return;bool pending=!string.IsNullOrEmpty(order.materialTransferOperationId);
        if(!pending){Require(string.IsNullOrEmpty(order.materialTransferCommitId)&&string.IsNullOrEmpty(order.materialTransferSourceStackId)&&order.materialTransferMassGrams==0&&!order.materialTransferOutcomePublished,"Facility recalibration has orphan material-transfer provenance.");return;}
        string operation=FacilityRecalibrationMaterialOutbox.FormatOperationId(order.orderId);
        string commit=$"physical-batch-disposition:1:{operation}:1:{order.materialTransferMassGrams}";
        Require(!string.IsNullOrWhiteSpace(order.materialTransferSourceStackId)&&order.materialTransferMassGrams>0,"Facility recalibration material transfer lacks source or mass.");
        Require(string.Equals(order.materialTransferOperationId,operation,StringComparison.Ordinal)&&string.Equals(order.materialTransferCommitId,commit,StringComparison.Ordinal),"Facility recalibration material receipt is not canonical.");
        Require(order.materialTransferOutcomePublished==order.materialsConsumed,"Facility recalibration material outcome mismatch.");
        Require(order.materialTransferOutcomePublished ? order.state==EvolutionReforgeOrderState.Ready : order.state==EvolutionReforgeOrderState.WaitingForMaterials,"Facility recalibration material phase mismatch.");
    }

    public static void ValidateModificationMaterialTransfer(
        FacilityModificationOrder order)
    {
        if (order == null)
        {
            return;
        }

        bool pending = !string.IsNullOrEmpty(
            order.materialTransferOperationId);
        if (!pending)
        {
            Require(
                string.IsNullOrEmpty(order.materialTransferCommitId)
                && string.IsNullOrEmpty(
                    order.materialTransferRequestFingerprint)
                && order.materialTransferMassGrams == 0L
                && !order.materialTransferOutcomePublished
                && (order.materialTransferInputs?.Count ?? 0) == 0,
                "Facility modification has orphan material-transfer provenance.");
            return;
        }

        Require(
            FacilityModificationMaterialOutbox.TryValidateInputs(
                order,
                order.materialTransferInputs,
                out _),
            "Facility modification material inputs are not canonical.");
        string operation =
            FacilityModificationMaterialOutbox.FormatOperationId(order.orderId);
        string requestFingerprint =
            FacilityModificationMaterialOutbox.CreateRequestFingerprint(
                order.materialTransferInputs);
        int quantity = checked(order.materialTransferInputs.Sum(
            input => input.quantity));
        string commit =
            $"physical-batch-disposition:1:{operation}:{quantity}:{order.materialTransferMassGrams}";
        Require(
            order.materialTransferMassGrams > 0L,
            "Facility modification material transfer has no input mass.");
        Require(
            string.Equals(
                order.materialTransferOperationId,
                operation,
                StringComparison.Ordinal)
            && string.Equals(
                order.materialTransferCommitId,
                commit,
                StringComparison.Ordinal)
            && string.Equals(
                order.materialTransferRequestFingerprint,
                requestFingerprint,
                StringComparison.Ordinal),
            "Facility modification material receipt is not canonical.");
        Require(
            order.materialTransferOutcomePublished == order.materialsConsumed,
            "Facility modification material outcome mismatch.");
        Require(
            order.materialTransferOutcomePublished
                ? order.state == EvolutionReforgeOrderState.Ready
                : order.state == EvolutionReforgeOrderState.WaitingForMaterials,
            "Facility modification material phase mismatch.");
    }

    public static void ValidateRelocationPackageTransfer(FacilityRelocationOrder order)
    {
        if(order==null)return;
        bool pending=!string.IsNullOrEmpty(order.packageTransferOperationId);
        if(!pending)
        {
            Require(string.IsNullOrEmpty(order.packageTransferCommitId)&&order.packageTransferMassGrams==0&&!order.packageTransferOutcomePublished,
                "Facility relocation has orphan package-transfer provenance.");
            return;
        }
        string operation=FacilityRelocationPackageOutbox.FormatOperationId(order.orderId);
        string commit=$"physical-batch-disposition:1:{operation}:1:{order.packageTransferMassGrams}";
        Require(order.phase==FacilityRelocationPhase.WaitingForPackage,
            "Facility relocation package transfer is not waiting for publication.");
        Require(!string.IsNullOrWhiteSpace(order.packageStackId)&&order.packageTransferMassGrams>0,
            "Facility relocation package transfer lacks stack or mass provenance.");
        Require(string.Equals(order.packageTransferOperationId,operation,StringComparison.Ordinal)
            &&string.Equals(order.packageTransferCommitId,commit,StringComparison.Ordinal),
            "Facility relocation package transfer receipt is not canonical.");
        Require(order.packageTransferOutcomePublished==order.packageConsumed,
            "Facility relocation package outcome does not match its terminal state.");
    }

    public static void ValidatePendingMaterialCommit(
        FacilityEvolutionStateSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new InvalidOperationException(
                "Facility evolution state payload was empty.");
        }

        FacilityEvolutionPendingMaterialCommitSnapshot pending =
            snapshot.pendingMaterialCommit;
        bool hasOperation = !string.IsNullOrWhiteSpace(pending?.operationId);
        if (!hasOperation)
        {
            if (pending != null
                && (!string.IsNullOrWhiteSpace(pending.commitId)
                    || pending.quantity != 0
                    || pending.inputMassGrams != 0L
                    || pending.phase != FacilityEvolutionMaterialCommitPhase.None))
            {
                throw new InvalidOperationException(
                    "Facility evolution pending material payload has no operation identity.");
            }
            return;
        }

        Require(!string.IsNullOrWhiteSpace(pending.reasonCode),
            "Facility evolution pending material reason is missing.");
        Require(!string.IsNullOrWhiteSpace(pending.commitId),
            "Facility evolution pending physical commit is missing.");
        Require(!string.IsNullOrWhiteSpace(pending.recipeId),
            "Facility evolution pending recipe is missing.");
        Require(!string.IsNullOrWhiteSpace(pending.sourceFacilityPersistentId),
            "Facility evolution pending source identity is missing.");
        Require(!string.IsNullOrWhiteSpace(pending.sourceFacilityDefinitionId),
            "Facility evolution pending source definition is missing.");
        Require(!string.IsNullOrWhiteSpace(pending.resultFacilityDefinitionId),
            "Facility evolution pending result definition is missing.");
        Require(pending.quantity > 0,
            "Facility evolution pending quantity must be positive.");
        Require(pending.inputMassGrams > 0L,
            "Facility evolution pending input mass must be positive.");
        Require(pending.historySequence > 0,
            "Facility evolution pending history sequence must be positive.");
        Require(
            pending.phase == FacilityEvolutionMaterialCommitPhase.MaterialCommitted
            || pending.phase == FacilityEvolutionMaterialCommitPhase.DomainApplied,
            "Facility evolution pending phase is invalid.");

        string[] sourceStackIds = pending.sourceStackIds ?? Array.Empty<string>();
        Require(sourceStackIds.Length > 0
            && sourceStackIds.All(id => !string.IsNullOrWhiteSpace(id)),
            "Facility evolution pending source-stack provenance is missing.");
        Require(sourceStackIds
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .SequenceEqual(sourceStackIds, StringComparer.Ordinal),
            "Facility evolution pending source-stack provenance is not canonical.");
        Require(string.Equals(
                pending.reasonCode,
                "facility-evolution-material-incorporated:" + pending.recipeId,
                StringComparison.Ordinal),
            "Facility evolution pending material reason does not match its recipe.");
        Require(string.Equals(
                pending.sourceFacilityPersistentId,
                snapshot.instanceEvolution?.facilityPersistentId,
                StringComparison.Ordinal),
            "Facility evolution pending source identity does not match its aggregate.");
        Require(string.Equals(
                pending.operationId,
                "facility-evolution-material:"
                    + pending.sourceFacilityPersistentId
                    + ":sequence:"
                    + pending.historySequence.ToString("D8"),
                StringComparison.Ordinal),
            "Facility evolution pending operation identity is not canonical.");

        FacilityEvolutionStateSnapshot resolved =
            pending.ReadResolvedResultState();
        Require(resolved.hasRecordSnapshot,
            "Facility evolution pending resolved result has no record aggregate.");
        Require(string.IsNullOrWhiteSpace(
                resolved?.pendingMaterialCommit?.operationId),
            "Facility evolution resolved result recursively owns another pending commit.");
        Require(string.Equals(
                resolved?.currentFacilityId,
                pending.resultFacilityDefinitionId,
                StringComparison.Ordinal),
            "Facility evolution pending result definition does not match its resolved state.");
        Require(string.Equals(
                resolved?.instanceEvolution?.facilityPersistentId,
                pending.sourceFacilityPersistentId,
                StringComparison.Ordinal),
            "Facility evolution pending result changed the facility persistent identity.");
        Require(resolved?.evolutionHistory != null
            && resolved.evolutionHistory.Count == pending.historySequence
            && string.Equals(
                resolved.evolutionHistory[resolved.evolutionHistory.Count - 1]?.evolutionId,
                pending.recipeId,
                StringComparison.Ordinal),
            "Facility evolution pending resolved history is not exact.");

        Domain.FacilityEvolutionRestoreRules.Prepare(ToDomain(resolved));
        int outerHistoryCount = snapshot.evolutionHistory?.Count ?? 0;
        if (pending.phase == FacilityEvolutionMaterialCommitPhase.MaterialCommitted)
        {
            Require(string.Equals(
                    snapshot.currentFacilityId,
                    pending.sourceFacilityDefinitionId,
                    StringComparison.Ordinal),
                "Facility evolution material-committed source definition changed before publication.");
            Require(outerHistoryCount + 1 == pending.historySequence,
                "Facility evolution material-committed history sequence is not the next sequence.");
        }
        else
        {
            Require(string.Equals(
                    snapshot.currentFacilityId,
                    pending.resultFacilityDefinitionId,
                    StringComparison.Ordinal),
                "Facility evolution domain-applied result definition is not published.");
            Require(outerHistoryCount == pending.historySequence,
                "Facility evolution domain-applied history sequence is not published.");
            FacilityEvolutionStateSnapshot outerWithoutPending =
                FacilityEvolutionStateComponent.CloneSnapshot(
                    snapshot,
                    includePendingMaterialCommit: false);
            FacilityEvolutionStateSnapshot resolvedWithoutPending =
                FacilityEvolutionStateComponent.CloneSnapshot(
                    resolved,
                    includePendingMaterialCommit: false);
            Require(string.Equals(
                    JsonUtility.ToJson(outerWithoutPending),
                    JsonUtility.ToJson(resolvedWithoutPending),
                    StringComparison.Ordinal),
                "Facility evolution domain-applied aggregate differs from its resolved result.");
        }
    }

    private static void ValidateInputOwnerProjection(
        FacilityEvolutionState state)
    {
        if (state?.modificationOrder != null
            && HasOrderId(state.modificationOrder.orderId))
        {
            FacilityModificationOrder order = state.modificationOrder;
            ValidateProjection(order.orderId, order.destinationId,
                FacilityEvolutionInputKind.Modification,
                order.inputCapacityGrams, order.inputMassAuthorityRevision,
                order.inputCapacityFingerprint);
        }
        if (state?.recalibrationOrder != null
            && HasOrderId(state.recalibrationOrder.orderId))
        {
            FacilityRecalibrationOrder order = state.recalibrationOrder;
            ValidateProjection(order.orderId, order.destinationId,
                FacilityEvolutionInputKind.Recalibration,
                order.inputCapacityGrams, order.inputMassAuthorityRevision,
                order.inputCapacityFingerprint);
        }
        if (state?.relocationOrder != null
            && HasOrderId(state.relocationOrder.orderId))
        {
            FacilityRelocationOrder order = state.relocationOrder;
            ValidateProjection(order.orderId, order.destinationId,
                FacilityEvolutionInputKind.Relocation,
                order.inputCapacityGrams, order.inputMassAuthorityRevision,
                order.inputCapacityFingerprint);
        }
    }

    private static void ValidateProjection(string orderId,
        string destinationId, FacilityEvolutionInputKind kind,
        long capacityGrams, long massRevision, string fingerprint)
    {
        Require(string.Equals(destinationId,
                FacilityEvolutionInputOwnerAuthority.DestinationFor(kind, orderId),
                StringComparison.Ordinal),
            "Facility evolution input destination is not canonical.");
        Require(capacityGrams > 0L && massRevision > 0L
                && !string.IsNullOrWhiteSpace(fingerprint)
                && string.Equals(fingerprint, fingerprint.Trim(),
                    StringComparison.Ordinal),
            "Facility evolution input projection is not positive and canonical.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
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
            || order.inputCapacityGrams != 0L
            || order.inputMassAuthorityRevision != 0L
            || !string.IsNullOrWhiteSpace(order.inputCapacityFingerprint)
            || order.materialsConsumed
            || !string.IsNullOrWhiteSpace(order.materialTransferOperationId)
            || !string.IsNullOrWhiteSpace(order.materialTransferCommitId)
            || !string.IsNullOrWhiteSpace(
                order.materialTransferRequestFingerprint)
            || order.materialTransferMassGrams != 0L
            || order.materialTransferOutcomePublished
            || (order.materialTransferInputs?.Count ?? 0) != 0
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
            || order.inputCapacityGrams != 0L
            || order.inputMassAuthorityRevision != 0L
            || !string.IsNullOrWhiteSpace(order.inputCapacityFingerprint)
            || order.materialsConsumed
            || !string.IsNullOrWhiteSpace(order.materialTransferOperationId)
            || !string.IsNullOrWhiteSpace(order.materialTransferCommitId)
            || !string.IsNullOrWhiteSpace(order.materialTransferSourceStackId)
            || order.materialTransferMassGrams != 0
            || order.materialTransferOutcomePublished;
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
            || order.inputCapacityGrams != 0L
            || order.inputMassAuthorityRevision != 0L
            || !string.IsNullOrWhiteSpace(order.inputCapacityFingerprint)
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
