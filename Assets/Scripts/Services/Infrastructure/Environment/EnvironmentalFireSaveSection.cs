using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using UnityEngine;

public sealed class EnvironmentalFireSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "environment.fire";
    private static readonly string[] Dependencies =
    {
        FoundationSessionSaveSection.Id,
        RandomStreamSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        PowerInfrastructureSaveSection.Id
    };

    private readonly IEnvironmentalFirePersistence persistence;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;

    public EnvironmentalFireSaveSection(
        IEnvironmentalFirePersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonEnvironmentalFireSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;

    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireHeader(payloadJson, sectionVersion);
        persistence.PrepareRestore(Parse(payloadJson));
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidatePayload(payloadJson, sectionVersion, report);
        if (report.Success)
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireHeader(payloadJson, sectionVersion);
        DungeonEnvironmentalFireSaveData payload = Parse(payloadJson);
        EnvironmentalFireRestoreCandidate candidate =
            persistence.PrepareRestore(payload);
        ValidatePhysicalRestoreCandidate(payload, physicalCandidates);
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }

    public static void ValidatePhysicalRestoreCandidate(
        DungeonEnvironmentalFireSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        if (payload?.suppressionOperations == null
            || payload.activeFires == null
            || payload.history == null
            || query == null
            || !query.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Environmental-fire restore requires the incoming physical candidate.");
        }

        Dictionary<string, EnvironmentalFireSuppressionSaveRecord> owners =
            payload.suppressionOperations
                .Where(value => value != null
                    && value.mode == (int)EnvironmentalFireSuppressionMode.Water
                    && value.waterCommittedQuantity > 0
                    && !value.waterAcknowledged)
                .ToDictionary(value => value.operationId, StringComparer.Ordinal);
        Dictionary<string, EnvironmentalFireSaveRecord> fuelOwners =
            EnumerateFires(payload)
                .Where(value => value.fuelLossQuantity > 0
                    && !value.fuelLossAcknowledged)
                .ToDictionary(
                    value => value.fuelLossOperationId,
                    StringComparer.Ordinal);

        foreach (KeyValuePair<string, EnvironmentalFireSuppressionSaveRecord> pair
                 in owners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(pair.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Environmental-fire water owner '{pair.Key}' has no exact incoming physical Sink receipt.");
            }
        }

        foreach (KeyValuePair<string, EnvironmentalFireSaveRecord> pair
                 in fuelOwners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !MatchesFuelLoss(pair.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Environmental-fire fuel owner '{pair.Key}' has no exact incoming physical Sink receipt.");
            }
        }

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt == null
                || (!string.Equals(
                        receipt.ReasonCode,
                        EnvironmentalFireWorldAdapter.WaterSinkReasonCode,
                        StringComparison.Ordinal)
                    && !string.Equals(
                        receipt.ReasonCode,
                        EnvironmentalFireWorldAdapter.FuelLossSinkReasonCode,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            bool isWater = string.Equals(
                receipt.ReasonCode,
                EnvironmentalFireWorldAdapter.WaterSinkReasonCode,
                StringComparison.Ordinal);
            bool matched;
            if (isWater)
            {
                matched = owners.TryGetValue(
                        receipt.OperationId,
                        out EnvironmentalFireSuppressionSaveRecord owner)
                    && Matches(owner, receipt);
            }
            else
            {
                matched = fuelOwners.TryGetValue(
                        receipt.OperationId,
                        out EnvironmentalFireSaveRecord fuelOwner)
                    && MatchesFuelLoss(fuelOwner, receipt);
            }
            if (!matched)
            {
                throw new InvalidOperationException(
                    $"Incoming environmental-fire Sink '{receipt.OperationId}' has no exact unacknowledged fire owner.");
            }
        }
    }

    private static IEnumerable<EnvironmentalFireSaveRecord> EnumerateFires(
        DungeonEnvironmentalFireSaveData payload)
    {
        foreach (EnvironmentalFireSaveRecord fire in payload.activeFires)
            yield return fire;
        foreach (EnvironmentalFireHistorySaveRecord entry in payload.history)
        {
            if (entry?.fire != null)
                yield return entry.fire;
        }
    }

    private static bool Matches(
        EnvironmentalFireSuppressionSaveRecord owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            owner.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            EnvironmentalFireWorldAdapter.WaterSinkReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            owner.waterCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.waterCommittedQuantity;

    private static bool MatchesFuelLoss(
        EnvironmentalFireSaveRecord owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            owner.fuelLossOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            EnvironmentalFireWorldAdapter.FuelLossSinkReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            owner.fuelLossCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.fuelLossQuantity
        && receipt.InputMassGrams == owner.fuelLossMassGrams
        && receipt.SourceStackIds != null
        && receipt.SourceStackIds.Count == 1
        && string.Equals(
            receipt.SourceStackIds[0],
            owner.fuelLossTargetId,
            StringComparison.Ordinal);

    private static DungeonEnvironmentalFireSaveData Parse(string payloadJson)
    {
        try
        {
            return JsonUtility.FromJson<DungeonEnvironmentalFireSaveData>(
                payloadJson)
                ?? throw new InvalidOperationException(
                    "Environmental-fire payload deserialized to null.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Environmental-fire payload JSON is invalid: "
                + exception.Message,
                exception);
        }
    }

    private void RequireHeader(string payloadJson, int sectionVersion)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new InvalidOperationException(
                "Environmental-fire payload is empty.");
        if (sectionVersion != SectionVersion)
            throw new InvalidOperationException(
                $"Environmental-fire section version {sectionVersion} is unsupported; expected {SectionVersion}.");
        DungeonStrictJsonShape.RequireTopLevelArrays(
            SectionId,
            payloadJson,
            new[]
            {
                "activeFires",
                "history",
                "processedCauses",
                "suppressionOperations"
            });
    }
}
