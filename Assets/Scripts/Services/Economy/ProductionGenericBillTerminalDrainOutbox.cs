using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Production-owned terminal producer for one generic bill. The producer does
/// not release physical destination custody. It accepts only the durable child
/// receipt published by the Items-owned input-destination drain, then retires
/// the exact frozen bill through replay-safe terminal effects.
/// </summary>
public sealed class ProductionGenericBillTerminalDrainOutbox :
    IProductionGenericBillTerminalDrainQuery,
    IProductionGenericBillTerminalDrainCommand
{
    private sealed class State
    {
        internal Dictionary<string, ProductionGenericBillTerminalDrainSaveData>
            ByStepOperationId { get; } = new(StringComparer.Ordinal);
    }

    private readonly DungeonRuntimeAggregateRootStore rootStore;
    private readonly IProductionBillPersistence billPersistence;
    private readonly IProductionInputDestinationClaimRuntime inputClaims;
    private readonly IProductionInputDestinationCustodyDrainOutbox inputDrain;

    public ProductionGenericBillTerminalDrainOutbox(
        DungeonRuntimeAggregateRootStore rootStore,
        IProductionBillPersistence billPersistence,
        IProductionInputDestinationClaimRuntime inputClaims,
        IProductionInputDestinationCustodyDrainOutbox inputDrain)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
        this.billPersistence = billPersistence
            ?? throw new ArgumentNullException(nameof(billPersistence));
        this.inputClaims = inputClaims
            ?? throw new ArgumentNullException(nameof(inputClaims));
        this.inputDrain = inputDrain
            ?? throw new ArgumentNullException(nameof(inputDrain));
    }

    private State Current => rootStore.GetOrCreate(() => new State());

    public bool TryCaptureLiveBill(
        ProductionBillId billId,
        out ProductionBillSaveData sourceBill,
        out string sourceBillFingerprint,
        out string failureReason)
    {
        sourceBill = null;
        sourceBillFingerprint = string.Empty;
        failureReason = string.Empty;
        if (!billId.IsValid)
        {
            failureReason = "production-generic-terminal-bill-id-invalid";
            return false;
        }

        ProductionBillSaveData[] matches = (billPersistence.Capture()?.bills
                ?? new List<ProductionBillSaveData>())
            .Where(value => value != null && string.Equals(
                value.billId,
                billId.Value,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            failureReason = matches.Length == 0
                ? "production-generic-terminal-bill-missing"
                : "production-generic-terminal-bill-duplicate";
            return false;
        }

        sourceBill = ProductionGenericBillTerminalDrainCanonical.CloneBill(
            matches[0]);
        sourceBillFingerprint = ProductionGenericBillTerminalDrainCanonical
            .CreateSourceBillFingerprint(sourceBill);
        return true;
    }

    [GameplayInternalOnly(
        "Persists one frozen generic bill only after the upper destructive-drain journal owner exists.",
        "Generic production destructive-drain participant only")]
    public ProductionGenericBillTerminalDrainResult TryPrepare(
        ProductionGenericBillTerminalDrainRequest request)
    {
        if (!TryValidateRequest(request, out string failureReason))
            return Conflict(failureReason);

        if (Current.ByStepOperationId.TryGetValue(
                request.StepOperationId,
                out ProductionGenericBillTerminalDrainSaveData existing))
        {
            return string.Equals(existing.requestFingerprint,
                    request.RequestFingerprint,
                    StringComparison.Ordinal)
                ? Result(existing, ProductionGenericBillTerminalDrainStatus.Replay)
                : Conflict("production-generic-terminal-request-conflict");
        }
        if (Current.ByStepOperationId.Values.Any(value => value != null
                && (string.Equals(value.billId, request.SourceBill.billId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        value.inputDestinationDrainStepOperationId,
                        request.InputDestinationDrainStepOperationId,
                        StringComparison.Ordinal))))
        {
            return Conflict("production-generic-terminal-source-already-owned");
        }

        ProductionGenericBillTerminalDrainSaveData prepared = new()
        {
            parentOperationId = request.ParentOperationId,
            stepOperationId = request.StepOperationId,
            ownerStableId = request.OwnerStableId,
            billId = request.SourceBill.billId,
            facilityId = request.SourceBill.buildingInstanceId,
            inputDestinationId = request.SourceBill.materialDestinationId,
            sourceBill = ProductionGenericBillTerminalDrainCanonical.CloneBill(
                request.SourceBill),
            sourceBillFingerprint = ProductionGenericBillTerminalDrainCanonical
                .CreateSourceBillFingerprint(request.SourceBill),
            inputDestinationDrainStepOperationId =
                request.InputDestinationDrainStepOperationId,
            inputDestinationDrainRequestFingerprint =
                request.InputDestinationDrainRequestFingerprint,
            requestFingerprint = request.RequestFingerprint,
            phase = ProductionGenericBillTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt
        };
        Current.ByStepOperationId.Add(
            prepared.stepOperationId,
            prepared.Clone());
        return Result(prepared, ProductionGenericBillTerminalDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Advances one replay-safe generic-bill terminal effect after its Items child receipt is durable.",
        "Generic production destructive-drain participant only")]
    public ProductionGenericBillTerminalDrainResult TryProgress(
        string stepOperationId)
    {
        if (!TryGet(stepOperationId,
                out ProductionGenericBillTerminalDrainSaveData value))
            return Conflict("production-generic-terminal-producer-missing");

        return value.phase switch
        {
            ProductionGenericBillTerminalDrainPhase
                .PreparedAwaitingInputDestinationReceipt =>
                TryRecordInputDestinationReceipt(value),
            ProductionGenericBillTerminalDrainPhase
                .InputDestinationReceiptRecordedAwaitingAcknowledgement =>
                TryAcknowledgeInputDestination(value),
            ProductionGenericBillTerminalDrainPhase
                .InputDestinationAcknowledgedAwaitingBillTerminal =>
                TryCommitBillTerminal(value),
            ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement or
            ProductionGenericBillTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc =>
                Result(value, ProductionGenericBillTerminalDrainStatus.Replay),
            _ => Conflict("production-generic-terminal-phase-invalid")
        };
    }

    [GameplayInternalOnly(
        "Acknowledges the terminal receipt only after the upper journal records the exact receipt.",
        "Generic production destructive-drain participant only")]
    public ProductionGenericBillTerminalDrainResult TryAcknowledge(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (!TryGet(stepOperationId,
                out ProductionGenericBillTerminalDrainSaveData value))
            return Conflict("production-generic-terminal-producer-missing");
        if (!string.Equals(value.receiptFingerprint, receiptFingerprint,
                StringComparison.Ordinal))
            return Conflict("production-generic-terminal-receipt-conflict");
        if (value.phase == ProductionGenericBillTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
            return Result(value, ProductionGenericBillTerminalDrainStatus.Replay);
        if (value.phase != ProductionGenericBillTerminalDrainPhase
                .BillTerminalCommittedAwaitingOwnerAcknowledgement)
            return Deferred(value, "production-generic-terminal-effect-not-committed");

        value.phase = ProductionGenericBillTerminalDrainPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;
        Store(value);
        return Result(value, ProductionGenericBillTerminalDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Garbage-collects child then producer authority after a durable owner acknowledgement.",
        "Destructive-drain checkpoint GC only")]
    public ProductionGenericBillTerminalDrainResult TryGarbageCollect(
        string stepOperationId,
        string receiptFingerprint)
    {
        if (!TryGet(stepOperationId,
                out ProductionGenericBillTerminalDrainSaveData value))
        {
            return new ProductionGenericBillTerminalDrainResult(
                ProductionGenericBillTerminalDrainStatus.Replay,
                ProductionGenericBillTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
                string.Empty,
                receiptFingerprint,
                string.Empty);
        }
        if (value.phase != ProductionGenericBillTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc)
            return Deferred(value, "production-generic-terminal-not-acknowledged");
        if (!string.Equals(value.receiptFingerprint, receiptFingerprint,
                StringComparison.Ordinal))
            return Conflict("production-generic-terminal-receipt-conflict");

        ProductionInputDestinationCustodyDrainResult childGc = inputDrain
            .TryGarbageCollect(
                value.inputDestinationDrainStepOperationId,
                value.inputDestinationDrainReceiptFingerprint);
        if (childGc.Status == ProductionInputDestinationCustodyDrainStatus.Conflict)
            return Conflict("production-generic-terminal-child-gc-conflict:"
                + childGc.FailureReason);
        if (childGc.Status == ProductionInputDestinationCustodyDrainStatus.Deferred)
            return Deferred(value, "production-generic-terminal-child-gc-deferred:"
                + childGc.FailureReason);

        Current.ByStepOperationId.Remove(stepOperationId);
        return Result(value, ProductionGenericBillTerminalDrainStatus.Applied);
    }

    [GameplayInternalOnly(
        "Recovers exactly one generic-bill terminal producer phase from current-format authority.",
        "Destructive-drain recovery runner only")]
    public ProductionGenericBillTerminalDrainResult TryRecover(
        string stepOperationId) => TryProgress(stepOperationId);

    public bool TryCapture(
        string stepOperationId,
        out ProductionGenericBillTerminalDrainSaveData record)
    {
        record = null;
        if (!TryGet(stepOperationId,
                out ProductionGenericBillTerminalDrainSaveData value))
            return false;
        record = value.Clone();
        return true;
    }

    public IReadOnlyList<ProductionGenericBillTerminalDrainSaveData>
        CaptureCurrentFormat() => Current.ByStepOperationId.Values
        .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
        .Select(value => value.Clone())
        .ToArray();

    [GameplayInternalOnly(
        "Atomically replaces the unregistered producer state after current-format save validation.",
        "Production save restore coordinator only")]
    public bool TryRestoreCurrentFormat(
        IEnumerable<ProductionGenericBillTerminalDrainSaveData> records,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionGenericBillTerminalDrainSaveData[] ordered = (records
                ?? Array.Empty<ProductionGenericBillTerminalDrainSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.stepOperationId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value =>
                !ProductionGenericBillTerminalDrainCanonical.IsValidSave(value))
            || HasDuplicates(ordered.Select(value => value.stepOperationId))
            || HasDuplicates(ordered.Select(value => value.billId))
            || HasDuplicates(ordered.Select(value => value.inputDestinationId))
            || HasDuplicates(ordered.Select(value =>
                value.inputDestinationDrainStepOperationId)))
        {
            failureReason = "production-generic-terminal-restore-invalid";
            return false;
        }

        State restored = new();
        foreach (ProductionGenericBillTerminalDrainSaveData value in ordered)
            restored.ByStepOperationId.Add(value.stepOperationId, value.Clone());
        rootStore.Replace(restored);
        return true;
    }

    private ProductionGenericBillTerminalDrainResult
        TryRecordInputDestinationReceipt(
            ProductionGenericBillTerminalDrainSaveData value)
    {
        if (!inputDrain.TryCapture(
                value.inputDestinationDrainStepOperationId,
                out ProductionInputDestinationCustodyDrainSaveData child))
        {
            return Deferred(value,
                "production-generic-terminal-child-receipt-missing");
        }
        if (!ProductionInputDestinationCustodyDrainContract.IsValidSave(child)
            || child.phase < ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck
            || !string.Equals(child.parentOperationId, value.parentOperationId,
                StringComparison.Ordinal)
            || !string.Equals(child.stepOperationId,
                value.inputDestinationDrainStepOperationId,
                StringComparison.Ordinal)
            || !string.Equals(child.ownerStableId, value.ownerStableId,
                StringComparison.Ordinal)
            || !string.Equals(child.billId, value.billId,
                StringComparison.Ordinal)
            || !string.Equals(child.facilityId, value.facilityId,
                StringComparison.Ordinal)
            || !string.Equals(child.sourceDestinationId,
                value.inputDestinationId, StringComparison.Ordinal)
            || !string.Equals(child.requestFingerprint,
                value.inputDestinationDrainRequestFingerprint,
                StringComparison.Ordinal))
        {
            return Conflict("production-generic-terminal-child-receipt-conflict");
        }

        value.inputDestinationDrainCommitId = child.commitId;
        value.inputDestinationDrainReceiptFingerprint = child.receiptFingerprint;
        value.releasedInputQuantity = child.releasedQuantity;
        value.releasedInputMassGrams = child.releasedMassGrams;
        value.phase = ProductionGenericBillTerminalDrainPhase
            .InputDestinationReceiptRecordedAwaitingAcknowledgement;
        Store(value);
        return Result(value, ProductionGenericBillTerminalDrainStatus.Applied);
    }

    private ProductionGenericBillTerminalDrainResult
        TryAcknowledgeInputDestination(
            ProductionGenericBillTerminalDrainSaveData value)
    {
        ProductionInputDestinationCustodyDrainResult child = inputDrain
            .TryAcknowledge(
                value.inputDestinationDrainStepOperationId,
                value.inputDestinationDrainReceiptFingerprint);
        if (child.Status == ProductionInputDestinationCustodyDrainStatus.Conflict)
            return Conflict("production-generic-terminal-child-ack-conflict:"
                + child.FailureReason);
        if (child.Status == ProductionInputDestinationCustodyDrainStatus.Deferred)
            return Deferred(value, "production-generic-terminal-child-ack-deferred:"
                + child.FailureReason);

        value.phase = ProductionGenericBillTerminalDrainPhase
            .InputDestinationAcknowledgedAwaitingBillTerminal;
        Store(value);
        return Result(value, ProductionGenericBillTerminalDrainStatus.Applied);
    }

    private ProductionGenericBillTerminalDrainResult TryCommitBillTerminal(
        ProductionGenericBillTerminalDrainSaveData value)
    {
        ProductionAggregateStateSession production =
            new(rootStore);
        ProductionBillRecord[] live = production.Bills
            .Where(record => record != null && string.Equals(
                record.billId.Value,
                value.billId,
                StringComparison.Ordinal))
            .ToArray();
        if (live.Length > 1)
            return Conflict("production-generic-terminal-live-bill-duplicate");

        ProductionBillRecord record = live.SingleOrDefault();
        if (record != null)
        {
            if (!TryCaptureLiveBill(
                    record.billId,
                    out _,
                    out string currentFingerprint,
                    out string captureFailure)
                || !string.Equals(
                    currentFingerprint,
                    value.sourceBillFingerprint,
                    StringComparison.Ordinal))
            {
                return Conflict("production-generic-terminal-live-source-drift:"
                    + captureFailure);
            }
            if (!TryPublishWipTerminalReceipt(
                    production,
                    value.sourceBill,
                    out string wipCommitId,
                    out string wipFailure))
                return Conflict(wipFailure);
            if (!inputClaims.TryRevokeIfPresent(record, out string claimFailure))
                return Deferred(value,
                    "production-generic-terminal-claim-revoke-deferred:"
                    + claimFailure);
            if (!production.RemoveBill(record))
                return Deferred(value,
                    "production-generic-terminal-bill-remove-deferred");
            production.IncrementBillVersion();
            value.wipTerminalCommitId = wipCommitId;
        }
        else
        {
            ProductionBillRecord recoveryRecord = ProductionBillRecord.Create(
                (ProductionBillId)value.billId,
                value.sourceBill.recipeId,
                (BuildingInstanceId)value.facilityId,
                value.sourceBill.mode,
                value.sourceBill.remainingCycles,
                value.sourceBill.targetStock,
                value.sourceBill.batchStage,
                value.inputDestinationId);
            if (!inputClaims.TryRevokeIfPresent(
                    recoveryRecord,
                    out string recoveryClaimFailure))
            {
                return Deferred(value,
                    "production-generic-terminal-recovery-claim-deferred:"
                    + recoveryClaimFailure);
            }
            if (!TryVerifyWipTerminalReceipt(
                    production,
                    value.sourceBill,
                    out string recoveredWipCommitId,
                    out string receiptFailure))
                return Conflict(receiptFailure);
            value.wipTerminalCommitId = recoveredWipCommitId;
        }

        if ((billPersistence.Capture()?.bills
                ?? new List<ProductionBillSaveData>()).Any(candidate =>
                candidate != null && string.Equals(
                    candidate.billId,
                    value.billId,
                    StringComparison.Ordinal)))
        {
            return Deferred(value,
                "production-generic-terminal-bill-still-live");
        }

        value.billTerminalEffectFingerprint =
            ProductionGenericBillTerminalDrainCanonical
                .CreateBillTerminalEffectFingerprint(
                    value.requestFingerprint,
                    value.inputDestinationDrainReceiptFingerprint,
                    value.wipTerminalCommitId);
        value.commitId = ProductionGenericBillTerminalDrainCanonical
            .CreateCommitId(value.stepOperationId, value.requestFingerprint);
        value.receiptFingerprint = ProductionGenericBillTerminalDrainCanonical
            .CreateReceiptFingerprint(
                value.requestFingerprint,
                value.inputDestinationDrainReceiptFingerprint,
                value.billTerminalEffectFingerprint,
                value.commitId);
        value.phase = ProductionGenericBillTerminalDrainPhase
            .BillTerminalCommittedAwaitingOwnerAcknowledgement;
        Store(value);
        return Result(value, ProductionGenericBillTerminalDrainStatus.Applied);
    }

    private bool TryValidateRequest(
        ProductionGenericBillTerminalDrainRequest request,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (request == null
            || request.SourceBill == null
            || !Token(request.ParentOperationId)
            || !Token(request.StepOperationId)
            || !Token(request.OwnerStableId)
            || !Token(request.InputDestinationDrainStepOperationId)
            || !ProductionGenericBillTerminalDrainCanonical.IsDigest(
                request.InputDestinationDrainRequestFingerprint)
            || !ProductionGenericBillTerminalDrainCanonical.IsDigest(
                request.RequestFingerprint))
        {
            failureReason = "production-generic-terminal-request-invalid";
            return false;
        }
        if (!string.Equals(
                request.OwnerStableId,
                ProductionFacilityDestructiveDrainOwnerStableIds.GenericBill(
                    request.SourceBill.billId),
                StringComparison.Ordinal)
            || !string.Equals(
                request.SourceBill.materialDestinationId,
                ProductionBillRuntime.DestinationPrefix
                    + request.SourceBill.billId,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-generic-terminal-request-owner-or-destination-invalid";
            return false;
        }

        string expected = ProductionGenericBillTerminalDrainCanonical
            .CreateRequestFingerprint(
                request.ParentOperationId,
                request.StepOperationId,
                request.OwnerStableId,
                request.SourceBill,
                request.InputDestinationDrainStepOperationId,
                request.InputDestinationDrainRequestFingerprint);
        if (!string.Equals(expected, request.RequestFingerprint,
                StringComparison.Ordinal))
        {
            failureReason = "production-generic-terminal-request-fingerprint-invalid";
            return false;
        }
        if (!TryCaptureLiveBill(
                (ProductionBillId)request.SourceBill.billId,
                out ProductionBillSaveData live,
                out string liveFingerprint,
                out failureReason)
            || !string.Equals(liveFingerprint,
                ProductionGenericBillTerminalDrainCanonical
                    .CreateSourceBillFingerprint(request.SourceBill),
                StringComparison.Ordinal)
            || !string.Equals(live.buildingInstanceId,
                request.SourceBill.buildingInstanceId,
                StringComparison.Ordinal)
            || !string.Equals(live.materialDestinationId,
                request.SourceBill.materialDestinationId,
                StringComparison.Ordinal))
        {
            failureReason = string.IsNullOrEmpty(failureReason)
                ? "production-generic-terminal-live-source-conflict"
                : failureReason;
            return false;
        }
        return true;
    }

    private static bool TryPublishWipTerminalReceipt(
        ProductionAggregateStateSession production,
        ProductionBillSaveData source,
        out string commitId,
        out string failureReason)
    {
        commitId = string.Empty;
        failureReason = string.Empty;
        if (!ProductionGenericBillTerminalDrainCanonical
                .RequiresWipTerminalReceipt(source))
            return true;
        if (!TryCreateWipTerminalReceipt(
                source,
                out ProductionWipTerminalReceiptSaveData receipt,
                out failureReason))
            return false;
        if (!production.AddWipTerminalReceipt(receipt))
        {
            failureReason = "production-generic-terminal-wip-receipt-conflict";
            return false;
        }
        commitId = receipt.commitId;
        return true;
    }

    private static bool TryVerifyWipTerminalReceipt(
        ProductionAggregateStateSession production,
        ProductionBillSaveData source,
        out string commitId,
        out string failureReason)
    {
        commitId = string.Empty;
        failureReason = string.Empty;
        if (!ProductionGenericBillTerminalDrainCanonical
                .RequiresWipTerminalReceipt(source))
            return true;
        if (!TryCreateWipTerminalReceipt(
                source,
                out ProductionWipTerminalReceiptSaveData expected,
                out failureReason))
            return false;
        ProductionWipTerminalReceiptSaveData[] matches = production
            .WipTerminalReceipts
            .Where(value => value != null && string.Equals(
                value.commitId,
                expected.commitId,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 || !WipReceiptEquals(matches[0], expected))
        {
            failureReason =
                "production-generic-terminal-recovery-wip-receipt-missing-or-conflicting";
            return false;
        }
        commitId = expected.commitId;
        return true;
    }

    private static bool TryCreateWipTerminalReceipt(
        ProductionBillSaveData source,
        out ProductionWipTerminalReceiptSaveData receipt,
        out string failureReason)
    {
        receipt = null;
        failureReason = string.Empty;
        try
        {
            long committedOutputMass = (source.resolvedOutputs
                    ?? new List<ProductionResolvedOutputSaveData>())
                .Where(value => value != null)
                .Aggregate(0L, (total, value) => checked(
                    total + value.committedMassGrams));
            long availableMass = checked(
                source.wipInputMassGrams + source.processCleanWaterMassGrams);
            long accountedMass = checked(
                committedOutputMass + source.processWastewaterMassGrams);
            long declaredLoss = checked(availableMass - accountedMass);
            if (declaredLoss < 0L)
            {
                failureReason =
                    "production-generic-terminal-wip-negative-declared-loss";
                return false;
            }
            receipt = new ProductionWipTerminalReceiptSaveData
            {
                commitId = ProductionGenericBillTerminalDrainCanonical
                    .CreateWipTerminalCommitId(
                        source.billId,
                        source.cycleSequence),
                billId = source.billId,
                recipeId = source.recipeId,
                buildingInstanceId = source.buildingInstanceId,
                cycleSequence = source.cycleSequence,
                inputCommitId = source.wipInputCommitId,
                inputQuantity = source.wipInputQuantity,
                inputMassGrams = source.wipInputMassGrams,
                processCleanWaterMassGrams = source.processCleanWaterMassGrams,
                processWastewaterMassGrams = source.processWastewaterMassGrams,
                wastewaterComponents = (source.processWastewaterComponents
                        ?? new List<ProductionWastewaterComponentSaveData>())
                    .OrderBy(value => (int)value.composition)
                    .ThenBy(value => (int)value.sourceKind)
                    .ThenBy(value => value.sourceStableId,
                        StringComparer.Ordinal)
                    .Select(value => value.Clone())
                    .ToList(),
                committedOutputMassGrams = committedOutputMass,
                reason = ProductionWipTerminalReason.FacilityDestroyed,
                lossKind = ProductionWipTerminalLossKind
                    .ExplicitIrrecoverableProcessLoss,
                declaredLossMassGrams = declaredLoss
            };
            return true;
        }
        catch (OverflowException)
        {
            failureReason = "production-generic-terminal-wip-mass-overflow";
            return false;
        }
    }

    private static bool WipReceiptEquals(
        ProductionWipTerminalReceiptSaveData left,
        ProductionWipTerminalReceiptSaveData right) =>
        left != null && right != null
        && string.Equals(left.commitId, right.commitId, StringComparison.Ordinal)
        && string.Equals(left.billId, right.billId, StringComparison.Ordinal)
        && string.Equals(left.recipeId, right.recipeId, StringComparison.Ordinal)
        && string.Equals(left.buildingInstanceId, right.buildingInstanceId,
            StringComparison.Ordinal)
        && left.cycleSequence == right.cycleSequence
        && string.Equals(left.inputCommitId, right.inputCommitId,
            StringComparison.Ordinal)
        && left.inputQuantity == right.inputQuantity
        && left.inputMassGrams == right.inputMassGrams
        && left.processCleanWaterMassGrams == right.processCleanWaterMassGrams
        && left.processWastewaterMassGrams == right.processWastewaterMassGrams
        && left.committedOutputMassGrams == right.committedOutputMassGrams
        && left.reason == right.reason
        && left.lossKind == right.lossKind
        && left.declaredLossMassGrams == right.declaredLossMassGrams
        && string.Equals(
            UnityEngine.JsonUtility.ToJson(left.wastewaterComponents),
            UnityEngine.JsonUtility.ToJson(right.wastewaterComponents),
            StringComparison.Ordinal);

    private bool TryGet(
        string stepOperationId,
        out ProductionGenericBillTerminalDrainSaveData value)
    {
        value = null;
        if (!Token(stepOperationId)
            || !Current.ByStepOperationId.TryGetValue(
                stepOperationId,
                out ProductionGenericBillTerminalDrainSaveData stored))
            return false;
        value = stored.Clone();
        return true;
    }

    private void Store(ProductionGenericBillTerminalDrainSaveData value)
    {
        if (!ProductionGenericBillTerminalDrainCanonical.IsValidSave(value))
            throw new InvalidOperationException(
                "Generic production terminal outbox refused an invalid state.");
        Current.ByStepOperationId[value.stepOperationId] = value.Clone();
    }

    private static bool HasDuplicates(IEnumerable<string> source)
    {
        string[] values = (source ?? Array.Empty<string>()).ToArray();
        return values.Any(value => !Token(value))
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length;
    }

    private static bool Token(string value) => !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static ProductionGenericBillTerminalDrainResult Result(
        ProductionGenericBillTerminalDrainSaveData value,
        ProductionGenericBillTerminalDrainStatus status) => new(
        status,
        value.phase,
        value.commitId,
        value.receiptFingerprint,
        string.Empty);

    private static ProductionGenericBillTerminalDrainResult Deferred(
        ProductionGenericBillTerminalDrainSaveData value,
        string reason) => new(
        ProductionGenericBillTerminalDrainStatus.Deferred,
        value.phase,
        value.commitId,
        value.receiptFingerprint,
        reason);

    private static ProductionGenericBillTerminalDrainResult Conflict(
        string reason) => new(
        ProductionGenericBillTerminalDrainStatus.Conflict,
        default,
        string.Empty,
        string.Empty,
        reason);
}
