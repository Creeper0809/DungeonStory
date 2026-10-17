using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

using static CropPlotOperationIdentity;

internal sealed class CropPlotIrrigationRuntime
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly ICropIrrigationRuntime irrigation;
    private readonly ICropIrrigationSupplyTransaction irrigationTransactions;
    private readonly IGameCalendar calendar;
    private readonly CropPlotMutationRuntime mutation;
    private readonly CropPlotFactPublication factPublication;

    internal CropPlotIrrigationRuntime(ICropIrrigationRuntime irrigation,
        ICropIrrigationSupplyTransaction irrigationTransactions,
        IGameCalendar calendar,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CropPlotMutationRuntime mutation,
        CropPlotFactPublication factPublication)
    {
        this.irrigation = irrigation;
        this.irrigationTransactions = irrigationTransactions;
        this.calendar = calendar;
        this.aggregateRootStore = aggregateRootStore;
        this.mutation = mutation;
        this.factPublication = factPublication;
    }

    private int CurrentAbsoluteDay => calendar.Day;
    private bool snapshotsDirty
    {
        set => aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState()).SnapshotsDirty = value;
    }

    internal CropIrrigationSupplyResult TryCommitAutomaticIrrigation(
        CropPlotState state, CropIrrigationRequest request)
    {
        if (state.IrrigationPublication.pending)
            throw new InvalidOperationException("A previous irrigation fact is still pending.");
        CropIrrigationAssessment assessment = irrigation.Assess(request);
        if (!assessment.CanSupply)
            return new CropIrrigationSupplyResult(assessment, false, 0f, WorldWaterQuality.Clean);

        int sequence = state.NextWaterRefillOperationSequence;
        int nextSequence = checked(sequence + 1);
        float waterBefore = state.CurrentWater;
        CropCycleExecutionReceiptSaveData cycleBefore = state.CycleExecutionReceipt;
        if (cycleBefore == null || cycleBefore.IsEmpty)
            throw new InvalidOperationException("Crop irrigation requires its active physical cycle.");
        PreparedCropIrrigationSupply prepared = null;
        DomainFactLease lease = default;
        IDomainFactPublisher publisher = factPublication.RequireDomainFacts();
        bool ownerChanged = false;
        bool publicationMayBeCommitted = false;
        try
        {
            // Fluid preparation is reversible. No observer failure may leave a
            // debit unless an authoritative post-supply fact could be committed.
            if (!irrigationTransactions.TryPrepareSupply(request, out prepared))
                return new CropIrrigationSupplyResult(irrigation.Assess(request), false, 0f, WorldWaterQuality.Clean);
            CropIrrigationSupplyResult supplied = prepared.Result;
            if (supplied.SuppliedWaterUnits != CropWaterRules.RefillQuantity
                || waterBefore + supplied.SuppliedWaterUnits > state.WaterCapacity)
                throw new InvalidOperationException("Prepared crop irrigation drifted from its preflight quantity.");
            var pending = new CropIrrigationPublicationSaveData
            {
                pending = true, operationSequence = sequence,
                operationId = IrrigationOperationPrefix + state.PlotId.Value + ":"
                    + sequence.ToString("D6", System.Globalization.CultureInfo.InvariantCulture),
                plotId = state.PlotId.Value, irrigatorId = supplied.Assessment.IrrigatorId.Value,
                plotDisplayName = prepared.PlotDisplayName, irrigatorDisplayName = prepared.IrrigatorDisplayName,
                absoluteDay = CurrentAbsoluteDay, locationX = state.LastKnownPosition.x, locationY = state.LastKnownPosition.y,
                quantity = supplied.SuppliedWaterUnits, consumedQuality = supplied.ConsumedQuality,
                waterBefore = waterBefore, waterAfter = waterBefore + supplied.SuppliedWaterUnits,
                networkId = prepared.NetworkId, networkWaterBefore = prepared.NetworkWaterBefore,
                networkWaterAfter = prepared.NetworkWaterAfter
            };
            CropIrrigationSuppliedFact fact = new(pending);
            if (!publisher.TryReserve(fact, out lease, out string failure))
                throw new InvalidOperationException("Crop irrigation observation reservation failed: " + failure);
            pending.publicationKey = lease.PublicationKey;
            CropCycleExecutionReceiptSaveData cycleAfter = CropPlanExecutionReceiptAuthority.RecordWaterSupply(
                cycleBefore, pending.waterBefore, pending.waterAfter, pending.publicationKey);

            // Persist the applied state, exact opaque identity and frozen fact
            // before dispatch. An unknown ACK is forward recovery, never a
            // second network debit or an inferred rollback of a committed fact.
            state.CurrentWater = pending.waterAfter;
            state.NextWaterRefillOperationSequence = nextSequence;
            state.CycleExecutionReceipt = cycleAfter;
            state.IrrigationPublication = pending;
            ownerChanged = true;
            DomainFactDelivery delivery = factPublication.DeliverCropCommandFact(lease, pending.publicationKey, fact);
            publicationMayBeCommitted = delivery.MayBeCommitted;
            if (!publicationMayBeCommitted)
                throw new InvalidOperationException("Crop irrigation observation rejected: " + delivery.Detail);
            irrigationTransactions.CommitPreparedSupply(prepared);
            ApplyIrrigationAcknowledgement(state, delivery);
            mutation.MarkChanged();
            return supplied;
        }
        catch
        {
            if (!publicationMayBeCommitted)
            {
                if (ownerChanged)
                {
                    state.CurrentWater = waterBefore;
                    state.NextWaterRefillOperationSequence = sequence;
                    state.CycleExecutionReceipt = cycleBefore;
                    state.IrrigationPublication = new CropIrrigationPublicationSaveData();
                }
                if (prepared?.IsPending == true)
                    irrigationTransactions.RollbackPreparedSupply(prepared);
                if (lease.IsValid) publisher.Cancel(lease);
            }
            else if (prepared?.IsPending == true)
                irrigationTransactions.CommitPreparedSupply(prepared);
            throw;
        }
    }

    internal bool TryAdvanceIrrigationPublication(CropPlotState state)
    {
        if (state == null || !state.IrrigationPublication.pending) return true;
        CropIrrigationPublicationSaveData owner = state.IrrigationPublication;
        DomainFactDelivery delivery = factPublication.DeliverCropCommandFact(default, owner.publicationKey,
            new CropIrrigationSuppliedFact(owner));
        return ApplyIrrigationAcknowledgement(state, delivery);
    }

    private bool ApplyIrrigationAcknowledgement(CropPlotState state, DomainFactDelivery delivery)
    {
        CropIrrigationPublicationSaveData owner = state.IrrigationPublication;
        if (!CropPlotFactPublication.ConfirmCropCommandPublication(delivery, owner.publicationKey,
                owner.publicationId, owner.fingerprint, out string failure))
        {
            owner.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        owner.publicationId = delivery.PublicationId;
        owner.fingerprint = delivery.Fingerprint;
        if (delivery.Phase != DomainFactDeliveryPhase.Delivered)
        {
            owner.failureReason = delivery.Detail;
            snapshotsDirty = true;
            return false;
        }
        state.IrrigationPublication = new CropIrrigationPublicationSaveData();
        mutation.MarkChanged(replan: false);
        return true;
    }
}
