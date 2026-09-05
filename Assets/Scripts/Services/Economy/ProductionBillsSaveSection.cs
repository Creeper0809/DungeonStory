using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public sealed class ProductionBillsSaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.production-bills";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };

    private readonly IProductionBillDetachedFacilityPersistence persistence;
    private readonly IPhysicalItemRestoreCandidateQuery physicalCandidates;
    private readonly IPhysicalItemRestoreCandidateOutputQuery outputCandidates;
    private readonly IProductionPreparedOutputRestoreJoin preparedOutputJoin;
    private readonly IProductionExactCapabilityOutputRestoreJoin exactOutputJoin;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;
    private readonly IRestoreWorldCandidateQuery restoreWorldCandidates;
    private readonly IProductionFacilityHandleQuery facilityHandles;

    public ProductionBillsSaveSection(
        IProductionBillDetachedFacilityPersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IPhysicalItemRestoreCandidateOutputQuery outputCandidates,
        IProductionPreparedOutputRestoreJoin preparedOutputJoin,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IProductionFacilityHandleQuery facilityHandles)
        : this(
            persistence,
            physicalCandidates,
            outputCandidates,
            preparedOutputJoin,
            lifecycleRestoreCandidates,
            EmptyProductionExactCapabilityOutputRestoreJoin.Instance,
            restoreWorldCandidates,
            facilityHandles)
    {
    }

    [Inject]
    public ProductionBillsSaveSection(
        IProductionBillDetachedFacilityPersistence persistence,
        IPhysicalItemRestoreCandidateQuery physicalCandidates,
        IPhysicalItemRestoreCandidateOutputQuery outputCandidates,
        IProductionPreparedOutputRestoreJoin preparedOutputJoin,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates,
        IProductionExactCapabilityOutputRestoreJoin exactOutputJoin,
        IRestoreWorldCandidateQuery restoreWorldCandidates,
        IProductionFacilityHandleQuery facilityHandles)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.physicalCandidates = physicalCandidates
            ?? throw new ArgumentNullException(nameof(physicalCandidates));
        this.outputCandidates = outputCandidates
            ?? throw new ArgumentNullException(nameof(outputCandidates));
        this.preparedOutputJoin = preparedOutputJoin
            ?? throw new ArgumentNullException(nameof(preparedOutputJoin));
        this.exactOutputJoin = exactOutputJoin
            ?? throw new ArgumentNullException(nameof(exactOutputJoin));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
        this.restoreWorldCandidates = restoreWorldCandidates
            ?? throw new ArgumentNullException(nameof(restoreWorldCandidates));
        this.facilityHandles = facilityHandles
            ?? throw new ArgumentNullException(nameof(facilityHandles));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonProductionBillSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;

    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        // Registry preflight runs before any dependency is staged. Validate
        // this section's own current-format payload here; the Physical ->
        // Production cross-section joins remain fail-loud in StageRestore,
        // after the physical dependency has published its detached candidate.
        DungeonProductionBillSaveData payload = Parse(payloadJson, report);
        _ = persistence.BuildRestore(payload);
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IDungeonSaveRestoreStage stage = StageRestore(
            payloadJson,
            sectionVersion,
            report);
        if (report.Success)
        {
            stage.Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        PreparedOutputRestoreStageCandidate candidate =
            BuildCandidate(payloadJson, report);
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ =>
            {
                lifecycleRestoreCandidates.SetProduction(
                    candidate.PreparedOutput.NormalizedPayload);
                persistence.Restore(
                    candidate.Bills,
                    candidate.DetachedFacilities);
                preparedOutputJoin.Acknowledge(candidate.PreparedOutput);
            });
    }

    private void RequireVersion(int sectionVersion)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}; expected {SectionVersion}.");
        }
    }

    private DungeonProductionBillSaveData Parse(
        string payloadJson,
        DungeonGameRestoreReport report)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"{SectionId} payload is empty.");
        }
        try
        {
            DungeonProductionBillSaveData payload =
                JsonUtility.FromJson<DungeonProductionBillSaveData>(payloadJson)
                ?? throw new InvalidOperationException(
                    $"{SectionId} payload deserialized to null.");
            V18WorkProductionCharacterReferenceRestoreNormalizer.Normalize(
                payload,
                (value, path) =>
                    V18TypedCharacterReferenceRestoreNormalizer
                        .RewriteLegacyReference(
                            value,
                            report,
                            SectionId,
                            path));
            return payload;
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{SectionId} payload JSON is invalid: {exception.Message}",
                exception);
        }
    }

    private PreparedOutputRestoreStageCandidate BuildCandidate(
        string payloadJson,
        DungeonGameRestoreReport report)
    {
        DungeonProductionBillSaveData payload = Parse(payloadJson, report);
        ValidateStockSensorPhysicalRestoreCandidate(
            payload,
            physicalCandidates);
        ValidateStockSensorRemovalOutputCandidate(payload, outputCandidates);
        ProductionPreparedOutputRestoreJoinPlan prepared =
            preparedOutputJoin.Build(payload);
        exactOutputJoin.Validate(prepared.NormalizedPayload);
        ProductionFacilityHandle[] detachedFacilities =
            CaptureDetachedProductionFacilities(prepared.NormalizedPayload);
        return new PreparedOutputRestoreStageCandidate(
            persistence.BuildRestore(prepared.NormalizedPayload),
            prepared,
            detachedFacilities);
    }

    private ProductionFacilityHandle[] CaptureDetachedProductionFacilities(
        DungeonProductionBillSaveData payload)
    {
        if (!restoreWorldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> candidateBuildings)
            || candidateBuildings == null)
        {
            throw new InvalidOperationException(
                "Production restore requires the detached facility-world candidate.");
        }

        ProductionFacilityHandle[] facilities = candidateBuildings
            .Where(ProductionFacilityDefinitionIdentity.IsProductionWorkstation)
            .Select(value => facilityHandles.CaptureFacility(value)
                ?? throw new InvalidOperationException(
                    "Production restore projected a null facility handle."))
            .OrderBy(value => value.InstanceId.Value, StringComparer.Ordinal)
            .ToArray();
        if (facilities.Select(value => value.InstanceId.Value)
            .Distinct(StringComparer.Ordinal).Count() != facilities.Length)
        {
            throw new InvalidOperationException(
                "Production restore contains duplicate detached facility identities.");
        }

        HashSet<string> facilityIds = facilities
            .Select(value => value.InstanceId.Value)
            .ToHashSet(StringComparer.Ordinal);
        ProductionBillSaveData orphan = (payload?.bills
                ?? new List<ProductionBillSaveData>())
            .Where(value => value != null)
            .OrderBy(value => value.billId, StringComparer.Ordinal)
            .FirstOrDefault(value => !facilityIds.Contains(
                value.buildingInstanceId));
        if (orphan != null)
        {
            throw new InvalidOperationException(
                $"Production bill '{orphan.billId}' has no detached facility '{orphan.buildingInstanceId}'.");
        }
        return facilities;
    }

    private sealed class PreparedOutputRestoreStageCandidate
    {
        internal PreparedOutputRestoreStageCandidate(
            ProductionBillRestoreCandidate bills,
            ProductionPreparedOutputRestoreJoinPlan preparedOutput,
            IReadOnlyList<ProductionFacilityHandle> detachedFacilities)
        {
            Bills = bills ?? throw new ArgumentNullException(nameof(bills));
            PreparedOutput = preparedOutput
                ?? throw new ArgumentNullException(nameof(preparedOutput));
            DetachedFacilities = (detachedFacilities
                    ?? throw new ArgumentNullException(nameof(detachedFacilities)))
                .ToArray();
        }

        internal ProductionBillRestoreCandidate Bills { get; }
        internal ProductionPreparedOutputRestoreJoinPlan PreparedOutput { get; }
        internal IReadOnlyList<ProductionFacilityHandle> DetachedFacilities
            { get; }
    }

    public static void ValidateStockSensorRemovalOutputCandidate(
        DungeonProductionBillSaveData payload,
        IPhysicalItemRestoreCandidateOutputQuery query)
    {
        if (payload?.pendingStockSensorRemovals == null)
            throw new InvalidOperationException(
                "Production stock-sensor restore has no removal owner collection.");
        if (query == null || !query.IsCandidateAvailable)
            throw new InvalidOperationException(
                "Production stock-sensor removal restore requires the incoming output candidate.");

        foreach (ProductionStockSensorRemovalSaveData owner in
                 payload.pendingStockSensorRemovals)
        {
            string prefix = "physical-source:" + owner.operationId + ":";
            PhysicalItemRestoreCandidateOutputSnapshot[] candidates =
                (query.CommittedOutputs
                    ?? Array.Empty<
                        PhysicalItemRestoreCandidateOutputSnapshot>())
                .Where(output => output != null
                    && output.CommitId.StartsWith(
                        prefix,
                        StringComparison.Ordinal))
                .OrderBy(output => output.CommitId, StringComparer.Ordinal)
                .ThenBy(output => output.StackId, StringComparer.Ordinal)
                .ToArray();
            if (owner.phase == ProductionStockSensorRemovalPhase.Prepared)
            {
                if (candidates.Length != 0)
                    throw new InvalidOperationException(
                        "Prepared stock-sensor removal already has an incoming physical output: "
                        + owner.operationId);
                continue;
            }

            string expectedCommit =
                ProductionStockSensorRuntime.BuildRemovalOutputCommitId(owner);
            if (owner.phase is not
                    ProductionStockSensorRemovalPhase.OutputPublished
                    and not ProductionStockSensorRemovalPhase
                        .OwnerAcknowledgedAwaitingCheckpointGc
                || owner.outputCommitIds == null
                || owner.outputCommitIds.Count != 1
                || !string.Equals(
                    owner.outputCommitIds[0],
                    expectedCommit,
                    StringComparison.Ordinal)
                || !query.TryGetCommittedOutput(
                    expectedCommit,
                    out IReadOnlyList<
                        PhysicalItemRestoreCandidateOutputSnapshot> exact)
                || exact == null
                || exact.Count == 0
                || candidates.Length != exact.Count
                || exact.Any(output => output == null
                    || !string.Equals(
                        output.ItemId,
                        owner.itemId,
                        StringComparison.Ordinal)
                    || output.State != WorldItemStackState.Loose
                    || output.Position.x != owner.outputPositionX
                    || output.Position.y != owner.outputPositionY
                    || !string.IsNullOrEmpty(output.DestinationId))
                || exact.Sum(output => (long)output.Quantity)
                    != owner.outputQuantity
                || exact.Sum(output => output.MassGrams)
                    != owner.outputMassGrams)
            {
                throw new InvalidOperationException(
                    "Published stock-sensor removal has no exact incoming physical output: "
                    + owner.operationId);
            }
        }
    }

    public static void ValidateStockSensorPhysicalRestoreCandidate(
        DungeonProductionBillSaveData payload,
        IPhysicalItemRestoreCandidateQuery query)
    {
        if (payload?.pendingStockSensorInstalls == null)
            throw new InvalidOperationException(
                "Production stock-sensor restore has no physical owner collection.");
        if (query == null || !query.IsCandidateAvailable)
            throw new InvalidOperationException(
                "Production stock-sensor restore requires the incoming item candidate.");
        Dictionary<string, ProductionStockSensorPhysicalCommitSaveData> owners =
            payload.pendingStockSensorInstalls.ToDictionary(
                owner => owner.operationId,
                StringComparer.Ordinal);
        foreach (KeyValuePair<string, ProductionStockSensorPhysicalCommitSaveData>
                 pair in owners)
        {
            if (!query.TryGetPendingBatchDisposition(
                    pair.Key,
                    out PhysicalItemRestoreCandidateDispositionSnapshot receipt)
                || !Matches(pair.Value, receipt))
                throw new InvalidOperationException(
                    "Production stock-sensor owner has no exact incoming Sink receipt: "
                    + pair.Key);
        }
        foreach (PhysicalItemRestoreCandidateDispositionSnapshot receipt in
                 query.PendingBatchDispositions
                 ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>())
        {
            if (receipt?.OperationId == null
                || !receipt.OperationId.StartsWith(
                    ProductionStockSensorRuntime.PhysicalOperationPrefix,
                    StringComparison.Ordinal))
                continue;
            if (!owners.TryGetValue(receipt.OperationId, out var owner)
                || !Matches(owner, receipt))
                throw new InvalidOperationException(
                    "Incoming production stock-sensor Sink has no exact owner: "
                    + receipt.OperationId);
        }
    }

    private static bool Matches(
        ProductionStockSensorPhysicalCommitSaveData owner,
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
}
