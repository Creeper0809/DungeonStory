using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CropPhysicalOwnerValidationSnapshot
{
    public string ExpectedOperationId { get; set; } = string.Empty;
    public CropPhysicalCommitSaveData Owner { get; set; }
}

public sealed class CropTreatmentOwnerValidationSnapshot
{
    public string PlotId { get; set; } = string.Empty;
    public int NextOperationSequence { get; set; }
    public CropTreatmentOrderSaveData Owner { get; set; }
}

public sealed class CropHarvestOwnerValidationSnapshot
{
    public string PlotId { get; set; } = string.Empty;
    public CropHarvestOutputSaveData Owner { get; set; }
}

public static class CropHarvestCompletionDeliveryRestoreJoin
{
    public static void Validate(
        IEnumerable<CropPlotSaveData> plots,
        IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> deliveries)
    {
        WorkCompletionIdentityDeliveryCursorSaveData[] cursors =
            WorkCompletionIdentityDeliveryLedger.ValidateAndClone(deliveries);
        Dictionary<string, WorkCompletionIdentityDeliveryCursorSaveData>
            byStream = cursors.ToDictionary(
                value => value.producerStreamId,
                StringComparer.Ordinal);
        HashSet<string> cropStreams = new(StringComparer.Ordinal);
        foreach (CropPlotSaveData plot in plots ?? Array.Empty<CropPlotSaveData>())
        {
            if (plot == null
                || string.IsNullOrWhiteSpace(plot.buildingInstanceId)
                || !string.Equals(
                    plot.buildingInstanceId,
                    plot.buildingInstanceId.Trim(),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop completion delivery join contains an invalid plot.");
            string streamId = CropPlotRuntime.HarvestCompletionStreamPrefix
                + plot.buildingInstanceId;
            if (!cropStreams.Add(streamId))
                throw new InvalidOperationException(
                    "Crop completion delivery join contains duplicate plot streams.");
            byStream.TryGetValue(streamId, out WorkCompletionIdentityDeliveryCursorSaveData cursor);
            CropHarvestOutputSaveData pending = plot.pendingHarvest;
            if (pending == null || pending.phase == CropHarvestOutputPhase.None)
            {
                if (cursor != null
                    && (plot.nextHarvestOperationSequence <= 0
                        || cursor.operationSequence >=
                            plot.nextHarvestOperationSequence
                        || !string.Equals(
                            cursor.deliveryId,
                            CropPlotRuntime.HarvestCompletionDeliveryPrefix
                                + CropPlotRuntime.FormatHarvestOperationId(
                                    new BuildingInstanceId(
                                        plot.buildingInstanceId),
                                    cursor.operationSequence),
                            StringComparison.Ordinal)))
                    throw new InvalidOperationException(
                        "Crop completion delivery cursor is ahead of its plot sequence.");
                continue;
            }
            if (string.IsNullOrEmpty(pending.harvesterId))
            {
                if (!string.IsNullOrEmpty(pending.completionDeliveryId)
                    || !string.IsNullOrEmpty(
                        pending.completionDeliveryFingerprint))
                    throw new InvalidOperationException(
                        "Workerless crop owner has completion delivery provenance.");
                if (cursor != null
                    && (cursor.operationSequence >= pending.operationSequence
                        || !MatchesHistoricalDeliveryId(
                            plot.buildingInstanceId,
                            cursor)))
                    throw new InvalidOperationException(
                        "Workerless crop owner has a current or invalid completion cursor.");
                continue;
            }

            WorkCompletionIdentityDeliveryRequest request =
                CropPlotRuntime.CreateHarvestCompletionDelivery(
                    new BuildingInstanceId(plot.buildingInstanceId),
                    pending);
            bool exactCurrent = cursor != null
                && cursor.operationSequence == request.OperationSequence
                && string.Equals(
                    cursor.deliveryId,
                    request.DeliveryId,
                    StringComparison.Ordinal)
                && string.Equals(
                    cursor.payloadFingerprint,
                    request.PayloadFingerprint,
                    StringComparison.Ordinal);
            bool previous = cursor == null && request.OperationSequence == 0
                || cursor != null
                && cursor.operationSequence == request.OperationSequence - 1
                && MatchesHistoricalDeliveryId(
                    plot.buildingInstanceId,
                    cursor);
            if (pending.completionEventPublished ? !exactCurrent
                : !exactCurrent && !previous)
                throw new InvalidOperationException(
                    "Crop completion owner and identity delivery cursor do not join.");
        }

        WorkCompletionIdentityDeliveryCursorSaveData orphan = cursors
            .FirstOrDefault(value =>
                value.producerStreamId.StartsWith(
                    CropPlotRuntime.HarvestCompletionStreamPrefix,
                    StringComparison.Ordinal)
                && !cropStreams.Contains(value.producerStreamId));
        if (orphan != null)
            throw new InvalidOperationException(
                "Crop completion delivery cursor has no owning crop plot: "
                + orphan.producerStreamId);
    }

    private static bool MatchesHistoricalDeliveryId(
        string plotId,
        WorkCompletionIdentityDeliveryCursorSaveData cursor) =>
        cursor != null
        && string.Equals(
            cursor.deliveryId,
            CropPlotRuntime.HarvestCompletionDeliveryPrefix
                + CropPlotRuntime.FormatHarvestOperationId(
                    new BuildingInstanceId(plotId),
                    cursor.operationSequence),
            StringComparison.Ordinal);
}

/// <summary>
/// Enforces the bidirectional restore join between crop-domain WIP owners and
/// incoming pending physical disposition receipts.
/// </summary>
public sealed class CropPhysicalRestoreGuard :
    IDungeonRestoreTransactionParticipant
{
    private const string RestoreParticipantId =
        "175.world.crop-physical-transactions";
    private readonly CropPlotRuntime plots;
    private readonly CertifiedSeedRuntime certifiedSeeds;
    private readonly ICropEcologyService ecology;
    private readonly ICropEcologyHarvestTransactionService ecologyHarvests;
    private readonly IGoldenHarvestPreparedResolutionQuery goldenHarvests;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private bool active;
    private bool published;

    public CropPhysicalRestoreGuard(
        CropPlotRuntime plots,
        CertifiedSeedRuntime certifiedSeeds,
        ICropEcologyService ecology,
        ICropEcologyHarvestTransactionService ecologyHarvests,
        IGoldenHarvestPreparedResolutionQuery goldenHarvests,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.plots = plots ?? throw new ArgumentNullException(nameof(plots));
        this.certifiedSeeds = certifiedSeeds
            ?? throw new ArgumentNullException(nameof(certifiedSeeds));
        this.ecology = ecology ?? throw new ArgumentNullException(nameof(ecology));
        this.ecologyHarvests = ecologyHarvests
            ?? throw new ArgumentNullException(nameof(ecologyHarvests));
        this.goldenHarvests = goldenHarvests
            ?? throw new ArgumentNullException(nameof(goldenHarvests));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }

    public string ParticipantId => RestoreParticipantId;

    public void BeginRestoreCandidate()
    {
        if (active)
            throw new InvalidOperationException(
                "Crop physical restore validation is already active.");
        active = true;
        published = false;
    }

    public void PublishRestoreCandidate()
    {
        if (!active || published)
            throw new InvalidOperationException(
                "Crop physical restore validation is not ready to publish.");
        ValidateOwnerSet(
            plots.PhysicalTransactionStates,
            certifiedSeeds.PhysicalOrders,
            physicalCandidates);
        ValidateTreatmentOwnerSet(
            plots.PhysicalTransactionStates,
            physicalCandidates);
        ValidateEcologyEnvelopes(
            plots.PhysicalTransactionStates,
            ecology.Plots);
        ValidatePreparedHarvestOwnerSet(
            plots.PhysicalTransactionStates,
            ecologyHarvests.CapturePreparedHarvests(),
            goldenHarvests.CapturePreparedGoldenHarvests());
        published = true;
    }

    internal static void ValidatePreparedHarvestOwnerSet(
        IReadOnlyCollection<CropPlotState> plots,
        IReadOnlyList<CropEcologyPreparedHarvestSnapshot> ecologyReceipts,
        IReadOnlyList<GoldenHarvestPreparedResolution> goldenReceipts)
    {
        CropHarvestOwnerValidationSnapshot[] snapshots = (
                plots ?? Array.Empty<CropPlotState>())
            .Where(plot => plot?.PendingHarvest != null
                && plot.PendingHarvest.phase != CropHarvestOutputPhase.None)
            .Select(plot => new CropHarvestOwnerValidationSnapshot
            {
                PlotId = plot.PlotId.Value,
                Owner = plot.PendingHarvest
            })
            .ToArray();
        ValidatePreparedHarvestOwnerSnapshots(
            snapshots,
            ecologyReceipts,
            goldenReceipts);
    }

    public static void ValidatePreparedHarvestOwnerSnapshots(
        IReadOnlyCollection<CropHarvestOwnerValidationSnapshot> snapshots,
        IReadOnlyList<CropEcologyPreparedHarvestSnapshot> ecologyReceipts,
        IReadOnlyList<GoldenHarvestPreparedResolution> goldenReceipts)
    {
        Dictionary<string, CropHarvestOwnerValidationSnapshot> owners = new(
            StringComparer.Ordinal);
        foreach (CropHarvestOwnerValidationSnapshot snapshot in
                 snapshots ?? Array.Empty<CropHarvestOwnerValidationSnapshot>())
        {
            CropHarvestOutputSaveData owner = snapshot?.Owner;
            if (owner == null
                || owner.phase == CropHarvestOutputPhase.None
                || string.IsNullOrWhiteSpace(snapshot.PlotId)
                || !IsValidCropHarvestOperation(owner.operationId)
                || !string.Equals(
                    ExtractHarvestPlotId(owner.operationId),
                    snapshot.PlotId,
                    StringComparison.Ordinal)
                || !owners.TryAdd(owner.operationId, snapshot))
                throw new InvalidOperationException(
                    "Crop harvest prepared owner is invalid or duplicated.");
        }

        Dictionary<string, CropEcologyPreparedHarvestSnapshot> ecologyByOperation =
            BuildEcologyReceiptMap(ecologyReceipts);
        Dictionary<string, GoldenHarvestPreparedResolution> goldenByOperation =
            BuildGoldenReceiptMap(goldenReceipts);

        foreach ((string operationId, CropHarvestOwnerValidationSnapshot snapshot)
                 in owners.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            CropHarvestOutputSaveData owner = snapshot.Owner;
            bool hasEcology = ecologyByOperation.TryGetValue(
                operationId,
                out CropEcologyPreparedHarvestSnapshot ecologyReceipt);
            if (owner.ecologyAcknowledged == hasEcology)
                throw new InvalidOperationException(
                    "Crop harvest ecology acknowledgement contradicts its prepared receipt: "
                    + operationId);
            if (hasEcology
                && (!string.Equals(
                        ecologyReceipt.PlotId,
                        snapshot.PlotId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ecologyReceipt.OutcomeFingerprint,
                        owner.ecologyOutcomeFingerprint,
                        StringComparison.Ordinal)
                    || ecologyReceipt.Committed != owner.ecologyCommitted
                    || !SeedLotsEqual(
                        ecologyReceipt.Result.ReturnedSeedLot,
                        owner.returnedSeedLot)))
                throw new InvalidOperationException(
                    "Crop harvest owner does not match its ecology prepared receipt: "
                    + operationId);

            bool hasGolden = goldenByOperation.TryGetValue(
                operationId,
                out GoldenHarvestPreparedResolution goldenReceipt);
            if (!owner.goldenPrepared)
            {
                if (hasGolden)
                    throw new InvalidOperationException(
                        "Normal crop harvest has an orphan Golden Harvest receipt: "
                        + operationId);
                continue;
            }
            if (owner.goldenAcknowledged == hasGolden)
                throw new InvalidOperationException(
                    "Crop harvest Golden acknowledgement contradicts its prepared receipt: "
                    + operationId);
            if (hasGolden
                && (!string.Equals(
                        goldenReceipt.FieldId,
                        snapshot.PlotId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        goldenReceipt.CharacterId,
                        owner.harvesterId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        goldenReceipt.TraitDefinitionId,
                        owner.goldenTraitDefinitionId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        goldenReceipt.Fingerprint,
                        owner.goldenOutcomeFingerprint,
                        StringComparison.Ordinal)
                    || goldenReceipt.Committed != owner.goldenCommitted
                    || goldenReceipt.Resolution.Outcome != owner.goldenOutcome
                    || goldenReceipt.Resolution.PrimaryMultiplier
                        != owner.goldenPrimaryMultiplier
                    || goldenReceipt.Resolution.SecondaryMultiplier
                        != owner.goldenSecondaryMultiplier
                    || goldenReceipt.Resolution.FixedRollHash
                        != owner.goldenRollHash))
                throw new InvalidOperationException(
                    "Crop harvest owner does not match its Golden Harvest receipt: "
                    + operationId);
        }

        foreach (string operationId in ecologyByOperation.Keys)
            if (!owners.ContainsKey(operationId))
                throw new InvalidOperationException(
                    "Prepared crop ecology receipt has no crop harvest owner: "
                    + operationId);
        foreach (string operationId in goldenByOperation.Keys)
            if (!owners.ContainsKey(operationId))
                throw new InvalidOperationException(
                    "Prepared Golden Harvest receipt has no crop harvest owner: "
                    + operationId);
    }

    private static Dictionary<string, CropEcologyPreparedHarvestSnapshot>
        BuildEcologyReceiptMap(
            IReadOnlyList<CropEcologyPreparedHarvestSnapshot> receipts)
    {
        Dictionary<string, CropEcologyPreparedHarvestSnapshot> result = new(
            StringComparer.Ordinal);
        foreach (CropEcologyPreparedHarvestSnapshot receipt in
                 receipts ?? Array.Empty<CropEcologyPreparedHarvestSnapshot>())
            if (!IsValidCropHarvestOperation(receipt.OperationId)
                || !result.TryAdd(receipt.OperationId, receipt))
                throw new InvalidOperationException(
                    "Prepared crop ecology receipt is invalid or duplicated.");
        return result;
    }

    private static Dictionary<string, GoldenHarvestPreparedResolution>
        BuildGoldenReceiptMap(
            IReadOnlyList<GoldenHarvestPreparedResolution> receipts)
    {
        Dictionary<string, GoldenHarvestPreparedResolution> result = new(
            StringComparer.Ordinal);
        foreach (GoldenHarvestPreparedResolution receipt in
                 receipts ?? Array.Empty<GoldenHarvestPreparedResolution>())
            if (!IsValidCropHarvestOperation(receipt.OperationId)
                || !result.TryAdd(receipt.OperationId, receipt))
                throw new InvalidOperationException(
                    "Prepared Golden Harvest receipt is invalid or duplicated.");
        return result;
    }

    private static bool IsCropHarvestOperation(string operationId) =>
        operationId?.StartsWith("crop-harvest:", StringComparison.Ordinal)
        == true;

    private static bool IsValidCropHarvestOperation(string operationId)
    {
        if (!IsCropHarvestOperation(operationId)
            || !string.Equals(
                operationId,
                operationId.Trim(),
                StringComparison.Ordinal))
            return false;
        int separator = operationId.LastIndexOf(':');
        if (separator <= "crop-harvest:".Length
            || operationId.Length - separator - 1 < 6)
            return false;
        ReadOnlySpan<char> suffix = operationId.AsSpan(separator + 1);
        return int.TryParse(
            suffix,
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out int sequence)
            && sequence >= 0
            && string.Equals(
                sequence.ToString(
                    "D6",
                    System.Globalization.CultureInfo.InvariantCulture),
                suffix.ToString(),
                StringComparison.Ordinal);
    }

    private static string ExtractHarvestPlotId(string operationId)
    {
        const string prefix = "crop-harvest:";
        if (!IsValidCropHarvestOperation(operationId))
            throw new InvalidOperationException(
                "Crop harvest operation prefix is invalid.");
        int separator = operationId.LastIndexOf(':');
        if (separator <= prefix.Length || separator == operationId.Length - 1)
            throw new InvalidOperationException(
                "Crop harvest operation identity is malformed.");
        return operationId.Substring(prefix.Length, separator - prefix.Length);
    }

    private static bool SeedLotsEqual(SeedLotState left, SeedLotState right) =>
        left != null
        && right != null
        && string.Equals(
            SeedLotItemStateCodec.Encode(left).ToCanonicalString(),
            SeedLotItemStateCodec.Encode(right).ToCanonicalString(),
            StringComparison.Ordinal);

    internal static void ValidateEcologyEnvelopes(
        IReadOnlyCollection<CropPlotState> plots,
        IReadOnlyList<CropEcologyPlotSaveData> ecologyPlots)
    {
        foreach (CropPlotState plot in plots ?? Array.Empty<CropPlotState>())
        {
            CropPhysicalCommitSaveData owner = plot?.PendingSow;
            if (owner != null && owner.phase != CropPhysicalCommitPhase.None)
            {
                string expected = owner.phase
                    == CropPhysicalCommitPhase.InputCommitted
                    ? owner.ecologyBeforeFingerprint
                    : owner.ecologyAfterFingerprint;
                string actual =
                    CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
                        ecologyPlots,
                        plot.PlotId.Value);
                if (string.IsNullOrWhiteSpace(expected)
                    || !string.Equals(expected, actual, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Crop sow ecology envelope does not match its restored aggregate: "
                        + plot.PlotId.Value);
            }

            CropTreatmentOrderSaveData treatment = plot.Treatment;
            ValidateTreatmentEcologyEnvelope(
                plot.PlotId.Value,
                treatment,
                ecologyPlots);
        }
    }

    public static void ValidateTreatmentEcologyEnvelope(
        string plotId,
        CropTreatmentOrderSaveData treatment,
        IReadOnlyList<CropEcologyPlotSaveData> ecologyPlots)
    {
        if (treatment == null
            || treatment.phase is CropTreatmentOrderPhase.None
                or CropTreatmentOrderPhase.WaitingForDelivery
                or CropTreatmentOrderPhase.ReadyForWork
                or CropTreatmentOrderPhase.Working
                or CropTreatmentOrderPhase.OutcomePublished)
            return;
        string treatmentActual =
            CropPhysicalTransactionOutbox.CreateEcologyFingerprint(
                ecologyPlots,
                plotId);
        if (string.IsNullOrWhiteSpace(treatment.ecologyBeforeFingerprint)
            || !string.Equals(
                treatment.ecologyBeforeFingerprint,
                treatmentActual,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Crop treatment ecology envelope does not match its restored aggregate: "
                + plotId);
    }

    public void RollbackPublishedRestoreCandidate()
    {
        active = false;
        published = false;
    }

    public void CompleteRestoreCandidate()
    {
        if (!active || !published)
            throw new InvalidOperationException(
                "Crop physical restore validation cannot complete.");
        active = false;
        published = false;
    }

    public void DiscardRestoreCandidate()
    {
        active = false;
        published = false;
    }

    internal static void ValidateOwnerSet(
        IReadOnlyCollection<CropPlotState> plots,
        IReadOnlyCollection<CertifiedSeedOrderSaveData> certifiedOrders,
        IPhysicalItemRestoreCandidateQuery query)
    {
        List<CropPhysicalOwnerValidationSnapshot> snapshots = new();
        foreach (CropPlotState plot in plots ?? Array.Empty<CropPlotState>())
        {
            CropPhysicalCommitSaveData owner = plot?.PendingSow;
            if (owner == null || owner.phase == CropPhysicalCommitPhase.None)
                continue;
            snapshots.Add(new CropPhysicalOwnerValidationSnapshot
            {
                ExpectedOperationId =
                    CropPhysicalTransactionOutbox.FormatSowOperationId(
                        plot.PlotId.Value,
                        plot.NextSowOperationSequence),
                Owner = owner
            });
        }
        foreach (CertifiedSeedOrderSaveData order in
                 certifiedOrders ?? Array.Empty<CertifiedSeedOrderSaveData>())
        {
            CropPhysicalCommitSaveData owner = order?.pendingInput;
            if (owner == null || owner.phase == CropPhysicalCommitPhase.None)
                continue;
            snapshots.Add(new CropPhysicalOwnerValidationSnapshot
            {
                ExpectedOperationId =
                    CropPhysicalTransactionOutbox.FormatCertifiedOperationId(
                        order.orderId),
                Owner = owner
            });
        }

        ValidateOwnerSnapshots(snapshots, query);
    }

    internal static void ValidateTreatmentOwnerSet(
        IReadOnlyCollection<CropPlotState> plots,
        IPhysicalItemRestoreCandidateQuery query)
    {
        List<CropTreatmentOwnerValidationSnapshot> snapshots = new();
        foreach (CropPlotState plot in plots ?? Array.Empty<CropPlotState>())
        {
            if (plot == null) continue;
            snapshots.Add(new CropTreatmentOwnerValidationSnapshot
            {
                PlotId = plot.PlotId.Value,
                NextOperationSequence = plot.NextTreatmentOperationSequence,
                Owner = plot.Treatment
            });
        }
        ValidateTreatmentOwnerSnapshots(snapshots, query);
    }

    public static void ValidateTreatmentOwnerSnapshots(
        IReadOnlyCollection<CropTreatmentOwnerValidationSnapshot> snapshots,
        IPhysicalItemRestoreCandidateQuery query)
    {
        Dictionary<string, CropTreatmentOrderSaveData> owners =
            new(StringComparer.Ordinal);
        foreach (CropTreatmentOwnerValidationSnapshot snapshot in
                 snapshots ?? Array.Empty<CropTreatmentOwnerValidationSnapshot>())
        {
            CropTreatmentOrderSaveData owner = snapshot?.Owner;
            if (owner == null
                || owner.phase is CropTreatmentOrderPhase.None
                    or CropTreatmentOrderPhase.WaitingForDelivery
                    or CropTreatmentOrderPhase.ReadyForWork
                    or CropTreatmentOrderPhase.Working)
                continue;
            string expected = CropTreatmentPhysicalOutbox.FormatOperationId(
                snapshot.PlotId,
                snapshot.NextOperationSequence);
            if (!string.Equals(owner.operationId, expected, StringComparison.Ordinal)
                || !owners.TryAdd(owner.operationId, owner))
                throw new InvalidOperationException(
                    "Crop treatment owner is invalid or duplicated: "
                    + owner.operationId);
        }

        if (query == null || !query.IsCandidateAvailable)
        {
            if (owners.Count == 0) return;
            throw new InvalidOperationException(
                "Crop treatment restore requires the incoming item candidate.");
        }
        foreach (CropTreatmentOrderSaveData owner in owners.Values)
        {
            string[] sources = owner.sourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!query.TryGetPendingBatchDisposition(
                    owner.operationId,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || receipt.Kind != PhysicalItemDispositionKind.Sink
                || !string.Equals(
                    receipt.ReasonCode,
                    owner.reasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.CommitId,
                    owner.commitId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    receipt.RequestFingerprint,
                    owner.requestFingerprint,
                    StringComparison.Ordinal)
                || receipt.Quantity != owner.quantity
                || receipt.InputMassGrams != owner.inputMassGrams
                || !receipt.SourceStackIds.SequenceEqual(
                    sources,
                    StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "Crop treatment owner has no exact incoming receipt: "
                    + owner.operationId);
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    CropTreatmentPhysicalOutbox.OperationPrefix,
                    StringComparison.Ordinal))
                continue;
            if (!owners.ContainsKey(receipt.OperationId))
                throw new InvalidOperationException(
                    "Incoming crop treatment receipt has no domain owner: "
                    + receipt.OperationId);
        }
    }

    public static void ValidateOwnerSnapshots(
        IReadOnlyCollection<CropPhysicalOwnerValidationSnapshot> snapshots,
        IPhysicalItemRestoreCandidateQuery query)
    {
        Dictionary<string, CropPhysicalCommitSaveData> owners =
            new(StringComparer.Ordinal);
        foreach (CropPhysicalOwnerValidationSnapshot snapshot in
                 snapshots ?? Array.Empty<CropPhysicalOwnerValidationSnapshot>())
        {
            CropPhysicalCommitSaveData owner = snapshot?.Owner;
            if (owner == null
                || owner.phase == CropPhysicalCommitPhase.None
                || !string.Equals(
                    owner.operationId,
                    snapshot.ExpectedOperationId,
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Crop physical owner operation is invalid.");
            AddOwner(owner, owners);
        }

        if (query == null || !query.IsCandidateAvailable)
        {
            if (owners.Count == 0) return;
            throw new InvalidOperationException(
                "Crop physical restore requires the incoming item candidate.");
        }
        foreach (CropPhysicalCommitSaveData owner in owners.Values)
        {
            string[] sourceIds = owner.inputs
                .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
                .Select(value => value.sourceStackId)
                .ToArray();
            if (!query.TryGetPendingBatchDisposition(
                    owner.operationId,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || receipt.Kind != PhysicalItemDispositionKind.Transfer
                || !string.Equals(receipt.ReasonCode, owner.reasonCode, StringComparison.Ordinal)
                || !string.Equals(receipt.CommitId, owner.commitId, StringComparison.Ordinal)
                || !string.Equals(
                    receipt.RequestFingerprint,
                    CreateItemLayerRequestFingerprint(owner),
                    StringComparison.Ordinal)
                || receipt.Quantity != owner.inputQuantity
                || receipt.InputMassGrams != owner.inputMassGrams
                || !receipt.SourceStackIds.SequenceEqual(
                    sourceIds,
                    StringComparer.Ordinal))
                throw new InvalidOperationException(
                    "Crop physical owner has no exact incoming receipt: "
                    + owner.operationId);
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt?.OperationId == null
                || (!receipt.OperationId.StartsWith(
                        CropPhysicalTransactionOutbox.SowOperationPrefix,
                        StringComparison.Ordinal)
                    && !receipt.OperationId.StartsWith(
                        CropPhysicalTransactionOutbox.CertifiedOperationPrefix,
                        StringComparison.Ordinal)))
                continue;
            if (!owners.ContainsKey(receipt.OperationId))
                throw new InvalidOperationException(
                    "Incoming crop physical receipt has no domain owner: "
                    + receipt.OperationId);
        }
    }

    private static void AddOwner(
        CropPhysicalCommitSaveData owner,
        IDictionary<string, CropPhysicalCommitSaveData> owners)
    {
        if (owner.inputs == null
            || owner.inputs.Count == 0
            || !owners.TryAdd(owner.operationId, owner))
            throw new InvalidOperationException(
                "Crop physical owner is invalid or duplicated: "
                + owner.operationId);
    }

    private static string CreateItemLayerRequestFingerprint(
        CropPhysicalCommitSaveData owner) =>
        $"{(int)PhysicalItemDispositionKind.Transfer}:{owner.reasonCode}:"
        + string.Join(",", owner.inputs
            .OrderBy(value => value.sourceStackId, StringComparer.Ordinal)
            .Select(value => $"{value.sourceStackId}={value.quantity}"));
}
