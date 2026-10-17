using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;
using static ProductionBillIdentity;

internal sealed class ProductionBillTerminalRuntime
{
    private readonly ProductionAggregateStateStore stateStore;
    private readonly IProductionAssemblyBridge items;
    private readonly IProductionInputDestinationClaimRuntime inputDestinationClaims;
    private readonly IProductionPreparedOutputExecutionPort preparedOutputExecution;
    private readonly IProductionPreparedOutputRoutingAuthority preparedOutputRouting;
    private readonly IProductionFacilityMutationEpochQuery facilityMutationEpoch;
    private readonly ProductionBillRuntimeQuery billQueries;
    private readonly ProductionBillInputRuntime inputRuntime;
    private readonly ProductionBillCommandRuntime commandRuntime;

    private WipTerminalCheckpointGcCandidate activeWipCheckpointGcCandidate;
    private IReadOnlyList<ProductionBillRecord> bills => stateStore.Bills;

    private sealed class WipTerminalCheckpointGcCandidate :
        IProductionGenericBillWipTerminalCheckpointGcCandidate
    {
        internal WipTerminalCheckpointGcCandidate(
            int expectedVersion,
            IReadOnlyList<ProductionWipTerminalReceiptSaveData> rows)
        {
            ExpectedVersion = expectedVersion;
            PublishedVersion = expectedVersion;
            Rows = (rows ?? Array.Empty<ProductionWipTerminalReceiptSaveData>())
                .Select(value => value.Clone())
                .OrderBy(value => value.commitId, StringComparer.Ordinal)
                .ToArray();
        }

        internal int ExpectedVersion { get; }
        internal int PublishedVersion { get; set; }
        internal IReadOnlyList<ProductionWipTerminalReceiptSaveData> Rows
            { get; }
        internal bool Published { get; set; }
    }

    internal ProductionBillTerminalRuntime(
        ProductionBillOrderDependencies order,
        ProductionBillExecutionDependencies execution,
        ProductionBillRuntimeQuery billQueries,
        ProductionBillInputRuntime inputRuntime,
        ProductionBillCommandRuntime commandRuntime)
    {
        this.stateStore = order.StateStore;
        this.items = order.Bridge;
        this.inputDestinationClaims = order.InputDestinationClaims;
        this.preparedOutputExecution = execution.PreparedOutputExecution;
        this.preparedOutputRouting = execution.PreparedOutputRouting;
        this.facilityMutationEpoch = order.FacilityMutationEpoch;
        this.billQueries = billQueries;
        this.inputRuntime = inputRuntime;
        this.commandRuntime = commandRuntime;
    }

    public bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionGenericBillTerminalDrainSaveData> producers,
        out IProductionGenericBillWipTerminalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (activeWipCheckpointGcCandidate != null)
        {
            failureReason = "production-wip-checkpoint-gc-already-active";
            return false;
        }
        ProductionGenericBillTerminalDrainSaveData[] ordered = (producers
                ?? Array.Empty<ProductionGenericBillTerminalDrainSaveData>())
            .OrderBy(value => value?.billId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.billId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason = "production-wip-checkpoint-gc-producer-invalid";
            return false;
        }

        List<ProductionWipTerminalReceiptSaveData> rows = new();
        foreach (ProductionGenericBillTerminalDrainSaveData producer in ordered)
        {
            if (producer.phase != ProductionGenericBillTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                || !ProductionGenericBillTerminalDrainCanonical.IsValidSave(
                    producer))
            {
                failureReason =
                    "production-wip-checkpoint-gc-producer-not-terminal";
                return false;
            }
            bool required = ProductionGenericBillTerminalDrainCanonical
                .RequiresWipTerminalReceipt(producer.sourceBill);
            if (!required)
            {
                if (!string.IsNullOrEmpty(producer.wipTerminalCommitId))
                {
                    failureReason =
                        "production-wip-checkpoint-gc-unexpected-owner";
                    return false;
                }
                continue;
            }
            if (!ProductionGenericBillTerminalDrainCanonical
                    .TryCreateWipTerminalReceipt(
                        producer.sourceBill,
                        out ProductionWipTerminalReceiptSaveData expected,
                        out failureReason)
                || !string.Equals(
                    producer.wipTerminalCommitId,
                    expected.commitId,
                    StringComparison.Ordinal))
            {
                failureReason = string.IsNullOrEmpty(failureReason)
                    ? "production-wip-checkpoint-gc-owner-mismatch"
                    : failureReason;
                return false;
            }
            ProductionWipTerminalReceiptSaveData[] matches = stateStore
                .WipTerminalReceipts
                .Where(value => value != null && string.Equals(
                    value.commitId,
                    expected.commitId,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1
                || !ProductionGenericBillTerminalDrainCanonical.WipReceiptEquals(
                    matches[0], expected))
            {
                failureReason =
                    "production-wip-checkpoint-gc-lower-row-missing-or-conflicting";
                return false;
            }
            rows.Add(expected);
        }
        activeWipCheckpointGcCandidate = new WipTerminalCheckpointGcCandidate(
            stateStore.BillVersion,
            rows);
        candidate = activeWipCheckpointGcCandidate;
        return true;
    }

    public bool TryPublishCheckpointGarbageCollection(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        WipTerminalCheckpointGcCandidate exact = RequireWipCheckpointGcCandidate(
            candidate);
        if (exact.Published)
            return true;
        if (stateStore.BillVersion != exact.ExpectedVersion
            || exact.Rows.Any(expected => stateStore.WipTerminalReceipts
                .Count(value => value != null && string.Equals(
                    value.commitId,
                    expected.commitId,
                    StringComparison.Ordinal)) != 1
                || !stateStore.WipTerminalReceipts.Any(value =>
                    ProductionGenericBillTerminalDrainCanonical.WipReceiptEquals(
                        value, expected))))
        {
            failureReason = "production-wip-checkpoint-gc-live-authority-changed";
            return false;
        }
        foreach (ProductionWipTerminalReceiptSaveData expected in exact.Rows)
        {
            if (!stateStore.TryRemoveWipTerminalReceiptExact(expected))
                throw new InvalidOperationException(
                    "WIP checkpoint-GC exact row vanished during publish.");
        }
        if (exact.Rows.Count > 0)
            stateStore.IncrementBillVersion();
        exact.Published = true;
        exact.PublishedVersion = stateStore.BillVersion;
        return true;
    }

    public void RollbackCheckpointGarbageCollection(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
    {
        WipTerminalCheckpointGcCandidate exact = RequireWipCheckpointGcCandidate(
            candidate);
        if (!exact.Published)
            return;
        if (stateStore.BillVersion != exact.PublishedVersion
            || exact.Rows.Any(expected => stateStore.WipTerminalReceipts.Any(
                value => string.Equals(value?.commitId, expected.commitId,
                    StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "WIP checkpoint-GC rollback encountered live authority drift.");
        }
        foreach (ProductionWipTerminalReceiptSaveData expected in exact.Rows)
        {
            if (!stateStore.AddWipTerminalReceipt(expected))
                throw new InvalidOperationException(
                    "WIP checkpoint-GC rollback could not restore exact row.");
        }
        if (exact.Rows.Count > 0
            && !stateStore.TryRestoreBillVersionForCheckpointGc(
                exact.PublishedVersion,
                exact.ExpectedVersion))
        {
            throw new InvalidOperationException(
                "WIP checkpoint-GC rollback could not restore version.");
        }
        exact.Published = false;
    }

    public void CompleteCheckpointGarbageCollection(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
    {
        RequireWipCheckpointGcCandidate(candidate);
        activeWipCheckpointGcCandidate = null;
    }

    public ProductionBillCommandResult RemoveBill(
        ProductionBillId billId,
        bool returnMaterials)
    {
        ProductionBillRecord record = billQueries.Find(billId);
        if (record == null)
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillMissing,
                    billId.Value));
        }
        if (commandRuntime.TryGetFrozenMutationFailure(record, out var frozen))
            return frozen;

        ProductionRecipeSO inputRecipe = billQueries.ResolveRecipe(record)
            ?? throw new InvalidOperationException(
                $"Production bill '{billId.Value}' recipe is missing during removal.");
        ProductionFacilityHandle inputFacility = billQueries.ResolveFacility(record);
        bool requiresInputDestination = inputRuntime.RequiresPhysicalInputDestination(
            record,
            inputRecipe,
            inputFacility);
        if (requiresInputDestination
            && !inputDestinationClaims.TryValidateClaim(
                record,
                out string claimFailure))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    billId.Value,
                    claimFailure));
        }

        bool usesPreparedOutput = billQueries.UsesPreparedOutput(record);
        if (usesPreparedOutput
            && !CanRetirePreparedOutputBill(record))
        {
            return ProductionBillCommandResult.Failed(
                new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    record.billId.Value,
                    "prepared-output-routing-pending"));
        }

        long ownerRevision = stateStore.NextCommandSequence;
        if (inputFacility == null
            || ownerRevision <= 0L
            || ownerRevision == long.MaxValue)
        {
            return ProductionBillCommandResult.Failed(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                billId.Value,
                inputFacility == null
                    ? "production-command-facility-missing"
                    : "production-command-owner-revision-exhausted"));
        }
        ProductionCommandAppliedFact outcomeSource = commandRuntime.CreateCommandFact(
            ProductionCommandKind.BillRemoved,
            record, inputRecipe, inputFacility, ProductionBillCommandRuntime.CaptureCommandState(record), new ProductionCommandStateData(), ownerRevision);
        if (!commandRuntime.TryStageCommand(
                outcomeSource,
                out IDomainFactStage preparedOutcome,
                out string outcomePrepareFailure))
        {
            return ProductionBillCommandRuntime.CommandObservationFailure(billId, outcomePrepareFailure);
        }

        bool domainCommitted = false;
        try
        {
            if (usesPreparedOutput)
            {
                if (ProductionBillRuntimeQuery.HasLegacyOutputAuthority(record))
                {
                    return ProductionBillCommandResult.Failed(
                        ProductionBillRuntimeQuery.PreparedOutputAuthorityConflict(record));
                }
                ProductionPreparedOutputReleaseResult release =
                    preparedOutputExecution.Release(
                        record,
                        ProductionWipTerminalReason.Cancelled);
                if (!release.IsValid
                    || !release.Released
                    || record.preparedOutput?.phase !=
                        ProductionPreparedOutputPhase.Unresolved)
                {
                    DomainFailure preparedReleaseFailure = release.IsValid
                        && release.Failure.IsFailure
                            ? release.Failure
                            : new DomainFailure(
                                FailureCode.ProductionOutputUnavailable,
                                record.billId.Value,
                                release.IsValid && release.PhysicalBatchCommitted
                                    ? "prepared-output-physical-batch-retained"
                                    : release.IsValid && release.Released
                                        ? "prepared-output-release-state-mismatch"
                                        : "prepared-output-release-invalid-result");
                    return ProductionBillCommandResult.Failed(
                        preparedReleaseFailure);
                }
            }

            if (record.materialsConsumed
                && !TryCommitWipTerminalDisposition(
                    record,
                    ProductionWipTerminalReason.Cancelled))
            {
                return ProductionBillCommandResult.Failed(
                    new DomainFailure(
                        FailureCode.ProductionBillUnavailable,
                        billId.Value,
                        "wip-terminal-receipt-conflict"));
            }

            if (requiresInputDestination
                && !items.TryReleaseDestinationAtomically(
                    record.materialDestinationId,
                    billQueries.ResolveFacility(record)?.Position ?? Vector2Int.zero,
                    out _,
                    out string releaseFailure))
            {
                return ProductionBillCommandResult.Failed(
                    new DomainFailure(
                        FailureCode.ProductionBillUnavailable,
                        billId.Value,
                        releaseFailure));
            }

            inputRuntime.RevokeInputDestinationClaimOrThrow(
                record,
                requiresInputDestination,
                "revoked during removal");
            commandRuntime.QueueCommittedCommandObservation(
                outcomeSource,
                ownerRevision,
                preparedOutcome,
                deliverImmediately: false);
            if (!stateStore.RemoveBill(record))
            {
                stateStore.RemovePendingCommandObservation(ownerRevision);
                stateStore.NextCommandSequence = ownerRevision;
                throw new InvalidOperationException(
                    $"Production bill '{billId.Value}' vanished during removal commit.");
            }
            domainCommitted = true;
            commandRuntime.DeliverPendingCommandObservation(ownerRevision, preparedOutcome);
            commandRuntime.Touch(default, requestWorker: false);
            return ProductionBillCommandResult.Success(
                billId,
                ProductionBillOutcomeCode.BillRemoved);
        }
        finally
        {
            if (!domainCommitted)
                commandRuntime.CancelCommandStage(preparedOutcome);
        }
    }

    internal void ReconcileMissingFacilities()
    {
        bool changed = false;
        foreach (ProductionBillRecord record in bills.ToArray())
        {
            if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
                continue;
            bool usesPreparedOutput = billQueries.UsesPreparedOutput(record);
            if (usesPreparedOutput
                && !CanRetirePreparedOutputBill(record))
            {
                // The durable routing authority still owns completed physical
                // output from at least one cycle. Facility loss must not turn
                // bill cleanup into remote output deletion or an owner orphan.
                continue;
            }
            if (billQueries.ResolveFacility(record) != null)
            {
                continue;
            }
            if (usesPreparedOutput)
            {
                if (ProductionBillRuntimeQuery.HasLegacyOutputAuthority(record))
                {
                    record.SetBlockedFailure(
                        ProductionBillRuntimeQuery.PreparedOutputAuthorityConflict(record));
                    continue;
                }
                ProductionPreparedOutputReleaseResult release =
                    preparedOutputExecution.Release(
                        record,
                        ProductionWipTerminalReason.FacilityDestroyed);
                if (!release.IsValid
                    || !release.Released
                    || record.preparedOutput?.phase !=
                        ProductionPreparedOutputPhase.Unresolved)
                {
                    record.SetBlockedFailure(release.IsValid
                        && release.Failure.IsFailure
                            ? release.Failure
                            : new DomainFailure(
                                FailureCode.ProductionOutputUnavailable,
                                record.billId.Value,
                                release.IsValid
                                    && release.PhysicalBatchCommitted
                                        ? "prepared-output-physical-batch-retained"
                                        : release.IsValid && release.Released
                                            ? "prepared-output-release-state-mismatch"
                                            : "prepared-output-release-invalid-result"));
                    continue;
                }
            }
            if (record.materialsConsumed
                && !TryCommitWipTerminalDisposition(
                    record,
                    ProductionWipTerminalReason.FacilityDestroyed))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    record.billId.Value,
                    "wip-terminal-receipt-conflict"));
                continue;
            }
            ProductionRecipeSO missingFacilityRecipe = billQueries.ResolveRecipe(record)
                ?? throw new InvalidOperationException(
                    $"Production bill '{record.billId.Value}' recipe is missing while its facility is reconciled.");
            bool hadInputDestination = inputRuntime.RequiresPhysicalInputDestination(
                    record,
                    missingFacilityRecipe,
                    facility: null)
                || inputDestinationClaims.TryValidateClaim(record, out _);
            if (hadInputDestination
                && !items.TryReleaseDestinationAtomically(
                    record.materialDestinationId,
                    Vector2Int.zero,
                    out _,
                    out string releaseFailure))
            {
                record.SetBlockedFailure(new DomainFailure(
                    FailureCode.ProductionBillUnavailable,
                    record.billId.Value,
                    releaseFailure));
                continue;
            }
            inputRuntime.RevokeInputDestinationClaimOrThrow(
                record,
                hadInputDestination,
                "revoked after facility loss");
            stateStore.RemoveBill(record);
            changed = true;
        }
        if (changed)
        {
            commandRuntime.Touch(default, requestWorker: false);
        }
    }

    internal void ReconcileFinishedOutputBills()
    {
        bool changed = false;
        foreach (ProductionBillRecord record in bills.ToArray())
        {
            if (facilityMutationEpoch.IsFrozen(record.buildingInstanceId))
                continue;
            if (!IsTerminalOutputRepeatCount(record))
            {
                continue;
            }

            ProductionFacilityHandle facility = billQueries.ResolveFacility(record);
            if (TryRetireFinishedRepeatCountBill(record, facility))
            {
                changed = true;
            }
        }

        if (changed)
        {
            commandRuntime.Touch(default, requestWorker: false);
        }
    }

    internal bool TryRetireFinishedRepeatCountBill(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        if (record == null
            || record.mode != ProductionOrderMode.RepeatCount
            || record.remainingCycles > 0)
        {
            return false;
        }
        if (billQueries.UsesPreparedOutput(record)
            && !CanRetirePreparedOutputBill(record))
        {
            return false;
        }
        if (!billQueries.UsesPreparedOutput(record) && HasBufferedExactOutput(record))
        {
            // Exact stateful capabilities publish a physical lot into the
            // facility output buffer before returning success. The bill is the
            // only routing policy owner until distribution releases that lot;
            // retiring it here would leave a permanent orphan in the buffer.
            // Its consumed input destination is a separate authority and must
            // not stay claimed while only output routing remains outstanding.
            TryReleaseTerminalExactInputAuthorities(record, facility);
            return false;
        }

        if (!billQueries.UsesPreparedOutput(record))
        {
            if (!TryReleaseTerminalExactInputAuthorities(record, facility))
                return false;

            stateStore.RemoveBill(record);
            return true;
        }

        ProductionRecipeSO terminalRecipe = billQueries.ResolveRecipe(record)
            ?? throw new InvalidOperationException(
                $"Production bill '{record.billId.Value}' recipe is missing during terminal retirement.");
        bool requiresInputDestination = inputRuntime.RequiresPhysicalInputDestination(
            record,
            terminalRecipe,
            facility);
        if (requiresInputDestination
            && !items.TryReleaseDestinationAtomically(
                record.materialDestinationId,
                facility?.Position ?? Vector2Int.zero,
                out _,
                out string releaseFailure))
        {
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                record.billId.Value,
                releaseFailure));
            return false;
        }

        inputRuntime.RevokeInputDestinationClaimOrThrow(
            record,
            requiresInputDestination,
            "revoked during terminal retirement");
        stateStore.RemoveBill(record);
        return true;
    }

    private bool TryReleaseTerminalExactInputAuthorities(
        ProductionBillRecord record,
        ProductionFacilityHandle facility)
    {
        bool hasInputDestination = inputDestinationClaims.TryValidateClaim(
            record,
            out _);
        if (hasInputDestination
            && !items.TryReleaseDestinationAtomically(
                record.materialDestinationId,
                facility?.Position ?? Vector2Int.zero,
                out _,
                out string releaseFailure))
        {
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                record.billId.Value,
                releaseFailure));
            return false;
        }

        if (!inputDestinationClaims.TryRevokeIfPresent(
                record,
                out string claimFailure))
        {
            record.SetBlockedFailure(new DomainFailure(
                FailureCode.ProductionBillUnavailable,
                record.billId.Value,
                claimFailure));
            return false;
        }

        return true;
    }

    private bool CanRetirePreparedOutputBill(ProductionBillRecord record)
    {
        bool outstanding = preparedOutputRouting.HasOutstandingForBill(
            record.billId);
        bool canRetire = preparedOutputRouting.CanRetireBill(record.billId);
        if (outstanding && canRetire)
        {
            throw new InvalidOperationException(
                $"Prepared-output routing authority returned conflicting retirement state for '{record.billId.Value}'.");
        }
        return !outstanding && canRetire;
    }

    private bool IsTerminalOutputRepeatCount(
        ProductionBillRecord record) => record != null
        && record.mode == ProductionOrderMode.RepeatCount
        && record.remainingCycles <= 0;

    private bool HasBufferedExactOutput(ProductionBillRecord record)
    {
        ProductionRecipeSO recipe = billQueries.ResolveRecipe(record);
        if (recipe == null)
            return false;
        return recipe.CaptureCanonicalOutputs()
            .Where(output => output != null
                && ProductionOutputRoleRules.IsPhysical(output.Role)
                && output.Amount > 0)
            .Select(output => output.ItemId)
            .Distinct(StringComparer.Ordinal)
            .Any(itemId => items.CountBufferedOutput(
                itemId,
                record.outputDestinationId) > 0);
    }

    private bool TryCommitWipTerminalDisposition(
        ProductionBillRecord record,
        ProductionWipTerminalReason reason)
    {
        if (record == null)
        {
            return true;
        }

        long committedOutputMassGrams;
        long availableMassGrams;
        long accountedMassGrams;
        long declaredLossMassGrams;
        try
        {
            committedOutputMassGrams = record.resolvedOutputs
                .Where(output => output != null)
                .Aggregate(
                    0L,
                    (total, output) => checked(
                        total + output.committedMassGrams));
            availableMassGrams = checked(
                record.wipInputMassGrams
                + record.processCleanWaterMassGrams);
            accountedMassGrams = checked(
                committedOutputMassGrams
                + record.processWastewaterMassGrams);
            declaredLossMassGrams = checked(
                availableMassGrams - accountedMassGrams);
        }
        catch (OverflowException)
        {
            return false;
        }
        if (availableMassGrams == 0L)
        {
            return true;
        }
        if (declaredLossMassGrams < 0L)
        {
            return false;
        }
        return stateStore.AddWipTerminalReceipt(
            new ProductionWipTerminalReceiptSaveData
            {
                commitId = ProductionBillStateCodec.BuildWipTerminalCommitId(
                    record.billId.Value,
                    record.cycleSequence,
                    reason),
                billId = record.billId.Value,
                recipeId = record.recipeId,
                buildingInstanceId = record.buildingInstanceId.Value,
                cycleSequence = record.cycleSequence,
                inputCommitId = record.wipInputCommitId,
                inputQuantity = record.wipInputQuantity,
                inputMassGrams = record.wipInputMassGrams,
                processCleanWaterMassGrams =
                    record.processCleanWaterMassGrams,
                processWastewaterMassGrams =
                    record.processWastewaterMassGrams,
                wastewaterComponents = record.processWastewaterComponents
                    .OrderBy(value => (int)value.composition)
                    .ThenBy(value => (int)value.sourceKind)
                    .ThenBy(value => value.sourceStableId, StringComparer.Ordinal)
                    .Select(value => value.Clone())
                    .ToList(),
                committedOutputMassGrams = committedOutputMassGrams,
                reason = reason,
                lossKind =
                    ProductionWipTerminalLossKind.ExplicitIrrecoverableProcessLoss,
                declaredLossMassGrams = declaredLossMassGrams
            });
    }

    private WipTerminalCheckpointGcCandidate RequireWipCheckpointGcCandidate(
        IProductionGenericBillWipTerminalCheckpointGcCandidate candidate)
    {
        if (candidate is not WipTerminalCheckpointGcCandidate exact
            || !ReferenceEquals(activeWipCheckpointGcCandidate, exact))
        {
            throw new InvalidOperationException(
                "WIP checkpoint-GC candidate is stale or foreign.");
        }
        return exact;
    }
}
