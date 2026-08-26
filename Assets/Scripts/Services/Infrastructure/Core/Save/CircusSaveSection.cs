using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CircusSaveSection :
    DungeonStrictJsonSaveSection<
        CircusSaveData,
        CircusRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "circus";

    private static readonly string[] Dependencies =
    {
        "items.physical",
        CaptivitySaveSection.Id,
        "wildlife.population",
        "characters.world",
        "world.facilities",
        "combat.body-health"
    };

    private readonly ICircusPersistence persistence;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;

    public CircusSaveSection(ICircusPersistence persistence, IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.physicalCandidates = physicalCandidates ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CircusSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override CircusSaveData CapturePayload() =>
        persistence.Capture();

    protected override void NormalizeRestorePayload(
        CircusSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload?.orders != null)
        {
            for (int orderIndex = 0; orderIndex < payload.orders.Count; orderIndex++)
            {
                CircusShowOrder order = payload.orders[orderIndex];
                NormalizeCharacterIds(
                    order?.performerIds,
                    report,
                    $"orders[{orderIndex}].performerIds");
                NormalizeCharacterIds(
                    order?.audienceIds,
                    report,
                    $"orders[{orderIndex}].audienceIds");
            }
        }

        if (payload?.capturedWildlife == null)
        {
            return;
        }

        for (int index = 0; index < payload.capturedWildlife.Count; index++)
        {
            CapturedWildlifeState animal = payload.capturedWildlife[index];
            if (animal != null)
            {
                animal.reservedCarrierId = NormalizeV18CharacterReference(
                    animal.reservedCarrierId,
                    report,
                    $"capturedWildlife[{index}].reservedCarrierId");
            }
        }
    }

    private void NormalizeCharacterIds(
        IList<string> values,
        DungeonGameRestoreReport report,
        string path)
    {
        if (values == null)
        {
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            values[index] = NormalizeV18CharacterReference(
                values[index],
                report,
                $"{path}[{index}]");
        }
    }

    protected override CircusRestoreCandidate BuildRestoreCandidate(CircusSaveData payload)
    {
        ValidatePhysicalRestoreCandidate(payload, physicalCandidates);
        return persistence.BuildRestore(payload);
    }

    protected override void ValidateParsedPayload(CircusSaveData payload)
    {
        _ = persistence.BuildRestore(payload)
            ?? throw new InvalidOperationException(
                "Circus restore candidate builder returned null.");
    }

    public static void ValidatePhysicalRestoreCandidate(CircusSaveData payload, IPhysicalItemRestoreCandidateQuery query)
    {
        const string prefix = "circus-show-supplies:";
        const string reason = "circus-performance-prop-consumed";
        if (payload?.orders == null || query == null || !query.IsCandidateAvailable)
            throw new InvalidOperationException("Circus restore requires orders and the incoming physical candidate.");
        Dictionary<string, CircusShowOrder> owners = payload.orders
            .Where(o => o != null && o.pendingSupplyPhase != CircusShowSupplyCommitPhase.None)
            .ToDictionary(o => o.pendingSupplyOperationId, StringComparer.Ordinal);
        foreach (KeyValuePair<string, CircusShowOrder> pair in owners)
        {
            if (!query.TryGetPendingBatchDisposition(pair.Key, out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(pair.Value, receipt, reason))
                throw new InvalidOperationException($"Circus supply '{pair.Key}' has no exact incoming physical Sink receipt.");
        }
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null || !receipt.OperationId.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!owners.TryGetValue(receipt.OperationId, out CircusShowOrder owner) || !Matches(owner, receipt, reason))
                throw new InvalidOperationException($"Incoming Circus Sink '{receipt.OperationId}' has no exact order owner.");
        }
    }

    private static bool Matches(CircusShowOrder owner, PhysicalItemRestoreCandidateDispositionSnapshot receipt, string reason) =>
        owner != null && receipt != null && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(receipt.OperationId, owner.pendingSupplyOperationId, StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode, reason, StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode, owner.pendingSupplyReasonCode, StringComparison.Ordinal)
        && string.Equals(receipt.CommitId, owner.pendingSupplyCommitId, StringComparison.Ordinal)
        && receipt.Quantity == owner.pendingSupplyQuantity
        && receipt.InputMassGrams == owner.pendingSupplyMassGrams
        && receipt.SourceStackIds.SequenceEqual(owner.pendingSupplySourceStackIds, StringComparer.Ordinal);

    protected override void PublishRestoreCandidate(
        CircusRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
