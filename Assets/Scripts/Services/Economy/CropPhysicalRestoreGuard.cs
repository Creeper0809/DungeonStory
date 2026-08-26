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
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private bool active;
    private bool published;

    public CropPhysicalRestoreGuard(
        CropPlotRuntime plots,
        CertifiedSeedRuntime certifiedSeeds,
        ICropEcologyService ecology,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.plots = plots ?? throw new ArgumentNullException(nameof(plots));
        this.certifiedSeeds = certifiedSeeds
            ?? throw new ArgumentNullException(nameof(certifiedSeeds));
        this.ecology = ecology ?? throw new ArgumentNullException(nameof(ecology));
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
        published = true;
    }

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
