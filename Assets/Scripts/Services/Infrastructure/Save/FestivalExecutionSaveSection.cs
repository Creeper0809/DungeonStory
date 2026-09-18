using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class FestivalExecutionRestoreCandidate
{
    public FestivalExecutionRestoreCandidate(
        FestivalExecutionAggregateState executions,
        IReadOnlyList<EconomyProjectInputOwnerDescriptor> inputOwners)
    {
        Executions = executions ?? throw new ArgumentNullException(nameof(executions));
        InputOwners = inputOwners ?? throw new ArgumentNullException(nameof(inputOwners));
    }

    public FestivalExecutionAggregateState Executions { get; }
    public IReadOnlyList<EconomyProjectInputOwnerDescriptor> InputOwners { get; }
}

public sealed class FestivalExecutionSaveSection :
    DungeonStrictJsonSaveSection<
        FestivalExecutionWorldSaveData,
        FestivalExecutionRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "festival.execution";

    private readonly IFestivalExecutionPersistence persistence;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IEconomyProjectInputOwnerRestoreRuntime inputOwners;

    public FestivalExecutionSaveSection(
        IFestivalExecutionPersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IEconomyProjectInputOwnerRestoreRuntime inputOwners)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
    }

    public override string SectionId => Id;
    public override int SectionVersion => FestivalExecutionWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        CalendarClimateSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id,
        CharacterNarrativeSaveSection.Id,
        CharacterPsychosocialSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        FactionCampaignSaveSection.Id
    };

    protected override FestivalExecutionWorldSaveData CapturePayload() =>
        persistence.CaptureFestivalExecutions();

    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(payloadJson, "occurrences");

    protected override FestivalExecutionRestoreCandidate BuildRestoreCandidate(
        FestivalExecutionWorldSaveData payload)
    {
        ValidatePhysicalRestoreJoin(payload, physicalCandidates);
        FestivalExecutionAggregateState executions = persistence
            .PrepareFestivalExecutionRestore(payload);
        IReadOnlyList<EconomyProjectInputOwnerDescriptor> descriptors =
            BuildInputOwnerDescriptors(payload);
        if (!inputOwners.TryValidateForRestore(
                EconomyProjectInputOwnerAuthority.FestivalDomain,
                descriptors,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Festival input-owner restore join failed: " + ownerFailure);
        }
        return new FestivalExecutionRestoreCandidate(executions, descriptors);
    }

    protected override void ValidateParsedPayload(
        FestivalExecutionWorldSaveData payload)
    {
        _ = persistence.PrepareFestivalExecutionRestore(payload)
            ?? throw new InvalidOperationException(
                "Festival execution restore candidate builder returned null.");
    }

    protected override void PublishRestoreCandidate(
        FestivalExecutionRestoreCandidate candidate)
    {
        FestivalExecutionRestoreCandidate required = candidate
            ?? throw new ArgumentNullException(nameof(candidate));
        if (!inputOwners.TryReplaceForRestore(
                EconomyProjectInputOwnerAuthority.FestivalDomain,
                required.InputOwners,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Festival input-owner restore publication failed: "
                + ownerFailure);
        }
        persistence.PublishFestivalExecutionRestore(required.Executions);
    }

    private static IReadOnlyList<EconomyProjectInputOwnerDescriptor>
        BuildInputOwnerDescriptors(FestivalExecutionWorldSaveData payload) =>
        (payload?.occurrences ?? new List<FestivalExecutionOccurrenceSaveData>())
            .Where(value => value?.inputOwnerActive == true)
            .OrderBy(value => value.materialDestinationId, StringComparer.Ordinal)
            .Select(value => new EconomyProjectInputOwnerDescriptor(
                EconomyProjectInputOwnerAuthority.FestivalDomain,
                value.occurrenceId,
                value.materialDestinationId,
                new Vector2Int(value.venueAnchorX, value.venueAnchorY),
                FacilityBufferDestinationAnchorKind.LiveFacility,
                value.venueFacilityInstanceId,
                FestivalExecutionRuntime.MaterialRequirements(value),
                value.inputCapacityGrams,
                value.inputMassAuthorityRevision,
                value.inputCapacityFingerprint))
            .ToArray();

    public static void ValidatePhysicalRestoreJoin(
        FestivalExecutionWorldSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        if (payload?.occurrences == null
            || query == null
            || !query.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "Festival execution restore requires execution and physical candidates.");
        }
        Dictionary<string, FestivalExecutionOccurrenceSaveData> owners = payload
            .occurrences
            .Where(value => value?.materialReceiptPending == true)
            .ToDictionary(
                value => value.materialOperationId,
                value => value,
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, FestivalExecutionOccurrenceSaveData> owner in
                 owners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    owner.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(owner.Value, receipt))
            {
                throw new InvalidOperationException(
                    $"Festival material sink '{owner.Key}' has no exact physical receipt.");
            }
        }
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions)
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    FestivalExecutionRules.MaterialOperationPrefix,
                    StringComparison.Ordinal))
                continue;
            if (!owners.TryGetValue(
                    receipt.OperationId,
                    out FestivalExecutionOccurrenceSaveData owner)
                || !Matches(owner, receipt))
            {
                throw new InvalidOperationException(
                    $"Physical festival sink '{receipt.OperationId}' has no exact execution owner.");
            }
        }
    }

    private static bool Matches(
        FestivalExecutionOccurrenceSaveData owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(receipt.OperationId, owner.materialOperationId,
            StringComparison.Ordinal)
        && string.Equals(receipt.ReasonCode, owner.materialReasonCode,
            StringComparison.Ordinal)
        && string.Equals(receipt.RequestFingerprint,
            owner.materialRequestFingerprint, StringComparison.Ordinal)
        && string.Equals(receipt.CommitId, owner.materialCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == owner.materialQuantity
        && receipt.InputMassGrams == owner.materialMassGrams
        && (receipt.SourceStackIds ?? Array.Empty<string>())
            .SequenceEqual(owner.materialSourceStackIds, StringComparer.Ordinal);
}
