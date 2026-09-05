using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Shared deterministic identity contract for the stock-sensor composite
/// destructive owner. Runtime orchestration and raw-save validation must use the
/// same formulas so a current-format restore cannot reinterpret a lower receipt.
/// </summary>
public static class ProductionStockSensorDestructiveDrainCanonical
{
    internal const string ChildStepSuffix = ":input-destination-custody";

    public static string BuildChildStepOperationId(string upperStepOperationId)
    {
        if (!ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                upperStepOperationId))
        {
            throw new ArgumentException(
                "A canonical upper stock-sensor step operation is required.",
                nameof(upperStepOperationId));
        }
        return upperStepOperationId + ChildStepSuffix;
    }

    public static string BuildRequestFingerprint(
        string childRequestFingerprint,
        Provenance provenance)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-stock-sensor-destructive-request@2");
        digest.Append(childRequestFingerprint);
        provenance.AppendTo(digest);
        return digest.ComputeSha256();
    }

    internal static string BuildEmbeddedReceiptFingerprint(
        ProductionStockSensorRemovalSaveData removal)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-stock-sensor-destructive-embedded-receipt@2");
        digest.Append(removal?.facilityId);
        digest.Append(removal?.itemId);
        digest.Append(removal?.operationId);
        digest.Append(removal?.reasonCode);
        digest.Append(removal?.installationSourceStackId);
        digest.Append(removal?.expectedOutputMassGrams ?? 0L);
        digest.Append(removal?.outputQuantity ?? 0);
        digest.Append(removal?.outputMassGrams ?? 0L);
        string[] commits = (removal?.outputCommitIds ?? new List<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        digest.Append(commits.Length);
        foreach (string commit in commits)
            digest.Append(commit);
        return digest.ComputeSha256();
    }

    public static bool TryBuildCompositeTerminal(
        string upperRequestFingerprint,
        ProductionInputDestinationCustodyDrainSaveData child,
        ProductionStockSensorRemovalSaveData removal,
        out string commitId,
        out string receiptFingerprint)
    {
        commitId = string.Empty;
        receiptFingerprint = string.Empty;
        if (!ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                upperRequestFingerprint)
            || child == null
            || !ProductionInputDestinationCustodyDrainContract.IsValidSave(child)
            || !IsChildEffectCommitted(child.phase)
            || !HasCanonicalTerminalFields(
                child.commitId,
                child.receiptFingerprint)
            || removal != null
                && (!IsSensorEffectCommitted(removal.phase)
                    || removal.outputCommitIds == null
                    || removal.outputCommitIds.Count != 1))
        {
            return false;
        }

        string sensorCommit = removal == null
            ? "none"
            : removal.outputCommitIds[0];
        string sensorReceipt = removal == null
            ? ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "production-stock-sensor-destructive-embedded-absent@2")
            : BuildEmbeddedReceiptFingerprint(removal);
        CanonicalSemanticDigestBuilder commitDigest = new();
        commitDigest.Append("production-stock-sensor-destructive-commit@2");
        commitDigest.Append(upperRequestFingerprint);
        commitDigest.Append(child.commitId);
        commitDigest.Append(child.receiptFingerprint);
        commitDigest.Append(sensorCommit);
        commitDigest.Append(sensorReceipt);
        commitId = "production-stock-sensor-destructive-commit:"
            + commitDigest.ComputeSha256();

        CanonicalSemanticDigestBuilder receipt = new();
        receipt.Append("production-stock-sensor-destructive-receipt@2");
        receipt.Append(upperRequestFingerprint);
        receipt.Append(commitId);
        receipt.Append(child.commitId);
        receipt.Append(child.receiptFingerprint);
        receipt.Append(sensorCommit);
        receipt.Append(sensorReceipt);
        receiptFingerprint = receipt.ComputeSha256();
        return true;
    }

    internal static bool IsChildEffectCommitted(
        ProductionInputDestinationCustodyDrainPhase phase) =>
        phase is ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck
            or ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc;

    internal static bool IsChildAcknowledged(
        ProductionInputDestinationCustodyDrainPhase phase) =>
        phase == ProductionInputDestinationCustodyDrainPhase
            .BillAcknowledgedAwaitingCheckpointGc;

    internal static bool IsSensorEffectCommitted(
        ProductionStockSensorRemovalPhase phase) =>
        phase is ProductionStockSensorRemovalPhase.OutputPublished
            or ProductionStockSensorRemovalPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;

    internal static bool IsSensorAcknowledged(
        ProductionStockSensorRemovalPhase phase) =>
        phase == ProductionStockSensorRemovalPhase
            .OwnerAcknowledgedAwaitingCheckpointGc;

    internal static bool HasCanonicalTerminalFields(
        string commitId,
        string receiptFingerprint) =>
        ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(commitId)
        && ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
            receiptFingerprint);

    public readonly struct Provenance : IEquatable<Provenance>
    {
        internal static Provenance Absent => default;

        private Provenance(
            bool present,
            string facilityId,
            string itemId,
            string sourceStackId,
            long massGrams)
        {
            Present = present;
            FacilityId = facilityId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            SourceStackId = sourceStackId ?? string.Empty;
            MassGrams = massGrams;
        }

        internal bool Present { get; }
        internal string FacilityId { get; }
        internal string ItemId { get; }
        internal string SourceStackId { get; }
        internal long MassGrams { get; }

        public static bool TryCreate(
            BuildingInstanceId facilityId,
            ProductionStockSensorPhysicalCommitSaveData pending,
            ProductionInstalledStockSensorSaveData installed,
            ProductionStockSensorRemovalSaveData removal,
            out Provenance provenance)
        {
            provenance = Absent;
            List<Provenance> sources = new();
            if (pending != null)
            {
                if (pending.sourceStackIds == null
                    || pending.sourceStackIds.Count != 1
                    || pending.inputQuantity != 1)
                {
                    return false;
                }
                sources.Add(new Provenance(
                    true,
                    pending.facilityId,
                    pending.itemId,
                    pending.sourceStackIds[0],
                    pending.inputMassGrams));
            }
            if (installed != null)
            {
                sources.Add(new Provenance(
                    true,
                    installed.facilityId,
                    installed.itemId,
                    installed.inputSourceStackId,
                    installed.embeddedMassGrams));
            }
            if (removal != null)
            {
                sources.Add(new Provenance(
                    true,
                    removal.facilityId,
                    removal.itemId,
                    removal.installationSourceStackId,
                    removal.expectedOutputMassGrams));
            }
            if (sources.Count == 0)
                return true;
            provenance = sources[0];
            Provenance expected = provenance;
            return provenance.Present
                && string.Equals(provenance.FacilityId,
                    facilityId.Value, StringComparison.Ordinal)
                && ProductionFacilityDestructiveDrainCanonical
                    .IsCanonicalToken(provenance.ItemId)
                && ProductionFacilityDestructiveDrainCanonical
                    .IsCanonicalToken(provenance.SourceStackId)
                && provenance.MassGrams > 0L
                && sources.All(value => value.Equals(expected));
        }

        internal static bool Matches(
            Provenance provenance,
            ProductionStockSensorRemovalSaveData removal) =>
            removal != null
            && provenance.Present
            && string.Equals(provenance.FacilityId,
                removal.facilityId, StringComparison.Ordinal)
            && string.Equals(provenance.ItemId,
                removal.itemId, StringComparison.Ordinal)
            && string.Equals(provenance.SourceStackId,
                removal.installationSourceStackId, StringComparison.Ordinal)
            && provenance.MassGrams == removal.expectedOutputMassGrams;

        internal void AppendTo(CanonicalSemanticDigestBuilder digest)
        {
            digest.Append(Present);
            digest.Append(FacilityId);
            digest.Append(ItemId);
            digest.Append(SourceStackId);
            digest.Append(MassGrams);
        }

        public bool Equals(Provenance other) =>
            Present == other.Present
            && string.Equals(FacilityId, other.FacilityId,
                StringComparison.Ordinal)
            && string.Equals(ItemId, other.ItemId,
                StringComparison.Ordinal)
            && string.Equals(SourceStackId, other.SourceStackId,
                StringComparison.Ordinal)
            && MassGrams == other.MassGrams;

        public override bool Equals(object obj) =>
            obj is Provenance other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            Present,
            FacilityId,
            ItemId,
            SourceStackId,
            MassGrams);
    }
}
