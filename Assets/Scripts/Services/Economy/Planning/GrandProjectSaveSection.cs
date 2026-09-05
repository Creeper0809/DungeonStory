using System;
using System.Collections.Generic;
using System.Linq;

// V18 required section: validation succeeds before the candidate Aggregate root is replaced.
public sealed class GrandProjectSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonGrandProjectSaveData,
        GrandProjectRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.grand-projects";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        ProductionBillsSaveSection.Id
    };

    private readonly IGrandProjectRuntime runtime;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IEconomyProjectInputOwnerRestoreRuntime inputOwners;
    private readonly IRestoreWorldCandidateQuery worldCandidates;

    public GrandProjectSaveSection(
        IGrandProjectRuntime runtime,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IEconomyProjectInputOwnerRestoreRuntime inputOwners,
        IRestoreWorldCandidateQuery worldCandidates)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        this.worldCandidates = worldCandidates
            ?? throw new ArgumentNullException(nameof(worldCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonGrandProjectSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonGrandProjectSaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override void ValidateParsedPayload(
        DungeonGrandProjectSaveData payload)
    {
        ValidateLocalPayload(payload);
        _ = runtime.BuildRestore(payload);
    }

    protected override GrandProjectRestoreCandidate BuildRestoreCandidate(
        DungeonGrandProjectSaveData payload)
    {
        ValidateLocalPayload(payload);
        ValidatePhysicalRestoreCandidate(payload, physicalCandidates);
        GrandProjectRestoreCandidate candidate = runtime.BuildRestore(payload);
        ValidateDetachedInputOwner(candidate);
        if (!inputOwners.TryReplaceForRestore(
                EconomyProjectInputOwnerAuthority.GrandProjectDomain,
                BuildInputOwnerDescriptors(payload),
                out string ownerFailure))
            throw new InvalidOperationException(
                "Grand-project exact input-owner restore join failed: "
                + ownerFailure);
        return candidate;
    }

    private IReadOnlyList<EconomyProjectInputOwnerDescriptor>
        BuildInputOwnerDescriptors(DungeonGrandProjectSaveData payload)
    {
        GrandProjectRuntimeState owner = payload?.state;
        if (owner == null || string.IsNullOrEmpty(owner.activeProjectId))
            return Array.Empty<EconomyProjectInputOwnerDescriptor>();
        GrandProjectDefinition definition = runtime.Definitions.Single(value =>
            string.Equals(value.ProjectId, owner.activeProjectId,
                StringComparison.Ordinal));
        return new[] { new EconomyProjectInputOwnerDescriptor(
            EconomyProjectInputOwnerAuthority.GrandProjectDomain,
            definition.ProjectId,
            owner.destinationId,
            new UnityEngine.Vector2Int(owner.inputDestinationX, owner.inputDestinationY),
            FacilityBufferDestinationAnchorKind.LiveFacility,
            owner.inputOwnerFacilityId,
            definition.Requirements.GroupBy(value => value.ItemId,
                    StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.Sum(value => value.Amount),
                    StringComparer.Ordinal),
            owner.inputCapacityGrams,
            owner.inputMassAuthorityRevision,
            owner.inputCapacityFingerprint) };
    }

    private void ValidateLocalPayload(DungeonGrandProjectSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        GrandProjectSaveValidation.Validate(
            payload,
            runtime.Definitions,
            report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Grand-project restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
    }

    protected override void PublishRestoreCandidate(
        GrandProjectRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);

    public static void ValidatePhysicalRestoreCandidate(
        DungeonGrandProjectSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        GrandProjectPhysicalCommitSaveData owner =
            payload?.state?.pendingPhysicalCommit;
        bool hasOwner = owner != null
            && owner.phase != GrandProjectPhysicalCommitPhase.None;
        if (query == null || !query.IsCandidateAvailable)
        {
            if (!hasOwner) return;
            throw new InvalidOperationException(
                "Grand-project physical restore requires the incoming item candidate.");
        }

        if (hasOwner
            && (!query.TryGetPendingBatchDisposition(
                    owner.operationId,
                    out PhysicalItemRestoreCandidateDispositionSnapshot ownerReceipt)
                || !Matches(owner, ownerReceipt)))
            throw new InvalidOperationException(
                "Grand-project physical owner has no exact incoming Sink receipt: "
                + owner.operationId);

        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    GrandProjectRuntime.PhysicalOperationPrefix,
                    StringComparison.Ordinal))
                continue;
            if (!hasOwner || !Matches(owner, receipt))
                throw new InvalidOperationException(
                    "Incoming grand-project physical Sink has no exact domain owner: "
                    + receipt.OperationId);
        }
    }

    private static bool Matches(
        GrandProjectPhysicalCommitSaveData owner,
        PhysicalItemRestoreCandidateDispositionSnapshot receipt) =>
        owner != null
        && receipt != null
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(owner.operationId, receipt.OperationId, StringComparison.Ordinal)
        && string.Equals(owner.reasonCode, receipt.ReasonCode, StringComparison.Ordinal)
        && string.Equals(owner.requestFingerprint, receipt.RequestFingerprint, StringComparison.Ordinal)
        && string.Equals(owner.commitId, receipt.CommitId, StringComparison.Ordinal)
        && owner.inputQuantity == receipt.Quantity
        && owner.inputMassGrams == receipt.InputMassGrams
        && receipt.SourceStackIds.SequenceEqual(
            owner.sourceStackIds,
            StringComparer.Ordinal);

    private void ValidateDetachedInputOwner(
        GrandProjectRestoreCandidate candidate)
    {
        GrandProjectRuntimeState state = candidate?.RuntimeState;
        if (state == null || string.IsNullOrEmpty(state.activeProjectId))
            return;
        if (!worldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> buildings)
            || buildings == null)
            throw new InvalidOperationException(
                "Grand-project input owner restore requires the detached facility world.");
        BuildableObject[] offices = buildings
            .Where(value => value != null
                && !value.IsBuildingDestroyed
                && value.SupportsWork(BuiltInWorkTypeIds.GrandProject)
                && value.HasSemanticTag("grand-project-office")
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    state.inputOwnerFacilityId,
                    StringComparison.Ordinal)
                && value.centerPos == new UnityEngine.Vector2Int(
                    state.inputDestinationX,
                    state.inputDestinationY))
            .ToArray();
        if (offices.Length != 1)
            throw new InvalidOperationException(
                "Grand-project detached LiveFacility input owner drifted: "
                + state.activeProjectId);
    }
}
