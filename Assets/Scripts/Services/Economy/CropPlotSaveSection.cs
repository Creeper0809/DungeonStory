using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CropPlotSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCropPlotSaveData,
        CropPlotRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.crop-plots";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly ICropPlotPersistence persistence;
    private readonly ICropPlotInputOwnerDescriptorSource inputOwnerSource;
    private readonly ICropPlotInputOwnerRuntime inputOwners;
    private readonly IProductionDomainOutputRestoreJoin outputRestoreJoin;
    private readonly IProductionOutputMaximumMassRegistry maximumMass;
    private readonly IProductionOutputDetachedFacilityCapacityRestoreGuard
        detachedCapacity;
    private readonly IRestoreWorldCandidateQuery worldCandidates;
    private readonly IFacilityBufferPlannedOutputRestoreCandidateQuery
        pendingOutputs;
    private readonly IFacilityBufferAcknowledgedOutputRestoreCandidateQuery
        acknowledgedOutputs;

    public CropPlotSaveSection(
        ICropPlotPersistence persistence,
        ICropPlotInputOwnerDescriptorSource inputOwnerSource,
        ICropPlotInputOwnerRuntime inputOwners,
        IProductionDomainOutputRestoreJoin outputRestoreJoin,
        IProductionOutputMaximumMassRegistry maximumMass,
        IProductionOutputDetachedFacilityCapacityRestoreGuard detachedCapacity,
        IRestoreWorldCandidateQuery worldCandidates,
        IFacilityBufferPlannedOutputRestoreCandidateQuery pendingOutputs,
        IFacilityBufferAcknowledgedOutputRestoreCandidateQuery
            acknowledgedOutputs)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.inputOwnerSource = inputOwnerSource
            ?? throw new ArgumentNullException(nameof(inputOwnerSource));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        this.outputRestoreJoin = outputRestoreJoin
            ?? throw new ArgumentNullException(nameof(outputRestoreJoin));
        this.maximumMass = maximumMass
            ?? throw new ArgumentNullException(nameof(maximumMass));
        this.detachedCapacity = detachedCapacity
            ?? throw new ArgumentNullException(nameof(detachedCapacity));
        this.worldCandidates = worldCandidates
            ?? throw new ArgumentNullException(nameof(worldCandidates));
        this.pendingOutputs = pendingOutputs
            ?? throw new ArgumentNullException(nameof(pendingOutputs));
        this.acknowledgedOutputs = acknowledgedOutputs
            ?? throw new ArgumentNullException(nameof(acknowledgedOutputs));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonCropPlotSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonCropPlotSaveData CapturePayload() =>
        persistence.Capture();

    protected override void ValidateParsedPayload(
        DungeonCropPlotSaveData payload) =>
        _ = persistence.BuildRestore(payload);

    protected override CropPlotRestoreCandidate BuildRestoreCandidate(
        DungeonCropPlotSaveData payload)
    {
        // First pass validates the authored/frozen owner before any incoming
        // physical marker is adopted. The second pass validates the normalized
        // post-adoption phase and becomes the only publishable aggregate.
        CropPlotRestoreCandidate validated = persistence.BuildRestore(payload);
        if (!worldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> detachedBuildings)
            || detachedBuildings == null)
            throw new InvalidOperationException(
                "Crop-plot input owner restore requires the detached facility world.");
        IReadOnlyList<CropPlotInputOwnerDescriptor> inputDescriptors =
            inputOwnerSource.BuildInputOwnerDescriptors(
                validated,
                detachedBuildings);
        if (!inputOwners.TryReplaceForRestore(
                inputDescriptors,
                out string inputOwnerFailure))
            throw new InvalidOperationException(
                "Crop-plot input owner restore join failed: "
                + inputOwnerFailure);
        List<ProductionDomainOutputRestoreAcknowledgement>
            acknowledgements = new();
        foreach (CropPlotSaveData plot in payload.plots
                     .OrderBy(
                         value => value.buildingInstanceId,
                         StringComparer.Ordinal))
        {
            ValidateDetachedPlot(plot);
            CropHarvestOutputSaveData owner = plot.pendingHarvest;
            switch (owner.phase)
            {
                case CropHarvestOutputPhase.OutputCommitted:
                    ValidatePhysicalVector(owner, pendingOutputs);
                    acknowledgements.Add(
                        outputRestoreJoin.AdoptPending(
                            owner.outputPublication));
                    owner.outputPublication.outputAcknowledged = true;
                    owner.outputPublication.restoredInCurrentTransaction = true;
                    owner.phase = CropHarvestOutputPhase
                        .OutputRestoredAwaitingFinalization;
                    break;

                case CropHarvestOutputPhase.None:
                case CropHarvestOutputPhase.Frozen:
                    outputRestoreJoin.RequireNoPending(
                        owner.outputPublication);
                    break;

                case CropHarvestOutputPhase
                        .OutputRestoredAwaitingFinalization:
                    owner.outputPublication.restoredInCurrentTransaction = false;
                    outputRestoreJoin.RequireNoPending(
                        owner.outputPublication);
                    ValidatePhysicalVector(owner, acknowledgedOutputs);
                    break;

                default:
                    throw new InvalidOperationException(
                        "Unknown crop harvest restore phase: "
                        + (int)owner.phase);
            }
        }

        CropPlotRestoreCandidate normalized = persistence.BuildRestore(payload);
        return new CropPlotRestoreCandidate(
            normalized.State,
            acknowledgements
                .OrderBy(value => value.BatchCommitId, StringComparer.Ordinal)
                .ToArray());
    }

    private static void ValidatePhysicalVector(
        CropHarvestOutputSaveData owner,
        IFacilityBufferPlannedOutputRestoreCandidateQuery query)
    {
        if (query == null
            || !query.IsCandidateAvailable
            || !query.TryGetBatch(
                owner.outputPublication.batchCommitId,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch))
            throw new InvalidOperationException(
                "Crop harvest pending physical output vector is missing: "
                + owner.operationId);
        ValidatePhysicalVector(owner, batch);
    }

    private static void ValidatePhysicalVector(
        CropHarvestOutputSaveData owner,
        IFacilityBufferAcknowledgedOutputRestoreCandidateQuery query)
    {
        if (query == null
            || !query.IsCandidateAvailable
            || !query.TryGetBatch(
                owner.outputPublication.batchCommitId,
                out FacilityBufferPlannedOutputRestoreBatchSnapshot batch))
            throw new InvalidOperationException(
                "Crop harvest acknowledged physical output vector is missing: "
                + owner.operationId);
        ValidatePhysicalVector(owner, batch);
    }

    private static void ValidatePhysicalVector(
        CropHarvestOutputSaveData owner,
        FacilityBufferPlannedOutputRestoreBatchSnapshot batch)
    {
        ProductionDomainOutputRestoreGuard.ValidateIncoming(
            owner.outputPublication,
            batch);
        if (batch.Stacks.Count != 2)
            throw new InvalidOperationException(
                "Crop harvest physical output is not an exact two-line batch: "
                + owner.operationId);
        FacilityBufferPlannedOutputRestoreStackSnapshot harvest = batch.Stacks
            .SingleOrDefault(value => string.Equals(
                value.OutputLineId,
                owner.harvestCapability.outputLineId,
                StringComparison.Ordinal));
        FacilityBufferPlannedOutputRestoreStackSnapshot seed = batch.Stacks
            .SingleOrDefault(value => string.Equals(
                value.OutputLineId,
                owner.seedCapability.outputLineId,
                StringComparison.Ordinal));
        ItemInstanceComponentSaveData expectedSeed =
            SeedLotItemStateCodec.Encode(owner.returnedSeedLot);
        string expectedSeedCanonical = expectedSeed.ToCanonicalString();
        string expectedSeedSignature =
            ProductionDomainOutputPublicationService
                .CaptureComponentFingerprint(new[] { expectedSeed });
        bool valid = harvest != null
            && harvest.Components.Count == 0
            && string.Equals(
                harvest.ItemId,
                owner.harvestItemId,
                StringComparison.Ordinal)
            && harvest.Quantity == owner.harvestQuantity
            && seed != null
            && string.Equals(
                seed.ItemId,
                owner.seedItemId,
                StringComparison.Ordinal)
            && seed.Quantity == owner.seedQuantity
            && seed.Components.Count == 1
            && string.Equals(
                seed.Components[0].ToCanonicalString(),
                expectedSeedCanonical,
                StringComparison.Ordinal)
            && string.Equals(
                seed.ComponentSignature,
                expectedSeedSignature,
                StringComparison.Ordinal);
        if (!valid)
            throw new InvalidOperationException(
                "Crop harvest physical vector or seed-lot component drifted: "
                + owner.operationId);
    }

    private void ValidateDetachedPlot(CropPlotSaveData plot)
    {
        CropHarvestOutputSaveData owner = plot?.pendingHarvest;
        if (owner == null || owner.phase == CropHarvestOutputPhase.None)
            return;
        if (!worldCandidates.TryGetBuildings(
                out IReadOnlyList<BuildableObject> buildings)
            || buildings == null)
            throw new InvalidOperationException(
                "Crop harvest restore requires the detached facility world.");
        BuildableObject facility = buildings.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                plot.buildingInstanceId,
                StringComparison.Ordinal));
        BuildingCropPlotAbility plotAbility = facility?.BuildingData?
            .GetAbility<BuildingCropPlotAbility>();
        if (facility == null
            || facility.IsBuildingDestroyed
            || plotAbility == null
            || plotAbility.Indoor != owner.indoor)
            throw new InvalidOperationException(
                "Crop harvest detached facility or indoor authority drifted: "
                + (plot.buildingInstanceId ?? string.Empty));

        ProductionOutputBatchMaximumMassProof proof = new(new[]
        {
            maximumMass.CaptureDeclared(
                owner.harvestCapability.ToDescriptor(),
                owner.maximumHarvestQuantity),
            maximumMass.CaptureDeclared(
                owner.seedCapability.ToDescriptor(),
                owner.maximumSeedQuantity)
        });
        if (owner.phase == CropHarvestOutputPhase.Frozen)
            return;
        ProductionDomainOutputPublicationSaveData publication =
            owner.outputPublication;
        if (!string.Equals(
                proof.SourceDigest,
                publication.maximumMassProofDigest,
                StringComparison.Ordinal)
            || proof.MaximumBatchMassGrams
                != publication.maximumBatchMassGrams)
            throw new InvalidOperationException(
                "Crop harvest detached maximum-mass proof drifted: "
                + owner.operationId);
        detachedCapacity.Validate(
            owner.operationId,
            plot.buildingInstanceId,
            proof,
            publication.capacitySourceDigest,
            publication.requiredMinimumCapacityGrams);
    }

    protected override void PublishRestoreCandidate(
        CropPlotRestoreCandidate candidate)
    {
        outputRestoreJoin.Acknowledge(candidate.OutputAcknowledgements);
        persistence.Restore(candidate);
    }
}
