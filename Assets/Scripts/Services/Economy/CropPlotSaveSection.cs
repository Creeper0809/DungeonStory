using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

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

    protected override void ValidateRawPayload(string payloadJson)
    {
        RequireTopLevelArrayFields(payloadJson, "plots");
        JObject root = JObject.Parse(payloadJson, new JsonLoadSettings
        {
            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
        });
        foreach (JToken plot in (JArray)root["plots"])
        {
            if (plot["retirementPublicationKey"]?.Type != JTokenType.String)
                throw new InvalidOperationException("Crop retirement publication key must be explicit.");
            if (plot["failedOutcome"] is not JObject failed || plot["pendingHarvest"] is not JObject harvest
                || harvest["terminalReceipt"] is not JObject terminal
                || terminal["status"]?.Type != JTokenType.Integer || terminal["sourceDigest"]?.Type != JTokenType.String)
                throw new InvalidOperationException("Crop terminal snapshots must be explicit.");
            foreach (string name in new[] { "publicationKey", "publicationId", "publicationFingerprint" })
                if (failed[name]?.Type != JTokenType.String)
                    throw new InvalidOperationException("Crop failure requires an explicit publication field: " + name);
            foreach (string name in new[] { "terminalPublicationKey", "terminalPublicationId", "terminalPublicationFingerprint",
                "terminalPlotDisplayName", "terminalCropDisplayName", "completionPublicationKey",
                "completionDeliveryId", "completionDeliveryFingerprint" })
                if (harvest[name]?.Type != JTokenType.String)
                    throw new InvalidOperationException("Crop harvest requires an explicit publication field: " + name);
            if (plot["treatment"] is not JObject treatment
                || treatment["locationX"]?.Type != JTokenType.Integer
                || treatment["locationY"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("Crop treatment publication must be explicit.");
            foreach (string name in new[] { "publicationKey", "plotDisplayName", "itemDisplayName" })
                if (treatment[name]?.Type != JTokenType.String)
                    throw new InvalidOperationException("Crop treatment requires an explicit string: " + name);
            if (plot["waterRefill"] is not JObject water
                || water["locationX"]?.Type != JTokenType.Integer
                || water["locationY"]?.Type != JTokenType.Integer)
                throw new InvalidOperationException("Crop manual water publication must be explicit.");
            foreach (string name in new[] { "publicationKey", "plotDisplayName", "itemDisplayName" })
                if (water[name]?.Type != JTokenType.String)
                    throw new InvalidOperationException("Crop manual water requires an explicit string: " + name);
            if (plot["irrigationPublication"] is not JObject irrigation
                || irrigation["pending"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("Crop irrigation publication must be explicit.");
            foreach (string name in new[] { "operationId", "publicationKey", "publicationId", "fingerprint", "failureReason",
                "plotId", "irrigatorId", "plotDisplayName", "irrigatorDisplayName", "networkId" })
                if (irrigation[name]?.Type != JTokenType.String)
                    throw new InvalidOperationException("Crop irrigation requires an explicit string: " + name);
            foreach (string name in new[] { "operationSequence", "absoluteDay", "locationX", "locationY", "consumedQuality" })
                if (irrigation[name]?.Type != JTokenType.Integer)
                    throw new InvalidOperationException("Crop irrigation requires an explicit integer: " + name);
            foreach (string name in new[] { "quantity", "waterBefore", "waterAfter", "networkWaterBefore", "networkWaterAfter" })
                if (irrigation[name]?.Type is not (JTokenType.Float or JTokenType.Integer))
                    throw new InvalidOperationException("Crop irrigation requires an explicit number: " + name);
            foreach (string name in new[] { "cropSelectionOutcome", "goldenHarvestScheduleOutcome" })
            {
                if (plot[name] is not JObject command
                    || command["publicationKey"]?.Type != JTokenType.String
                    || command["plotDisplayName"]?.Type != JTokenType.String
                    || command["cropDisplayName"]?.Type != JTokenType.String
                    || command["locationX"]?.Type != JTokenType.Integer
                    || command["locationY"]?.Type != JTokenType.Integer)
                    throw new InvalidOperationException(
                        "Crop command requires explicit publication identity and frozen presentation fields: " + name);
            }
        }
    }

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
        ValidateFrozenSeasonalPrimaryLoss(owner);
        if (owner.phase == CropHarvestOutputPhase.OutputRestoredAwaitingFinalization)
            ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
                owner.outputPublication, batch);
        else
            ProductionDomainOutputRestoreGuard.ValidateIncoming(
                owner.outputPublication, batch);
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
            FacilityBufferPlannedOutputPublicationService
                .CreateRuntimeComponentSignature(new[] { expectedSeed });
        string expectedSeedFingerprint =
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
                StringComparison.Ordinal)
            && string.Equals(
                seed.PreparedComponentFingerprint,
                expectedSeedFingerprint,
                StringComparison.Ordinal);
        if (!valid)
            throw new InvalidOperationException(
                "Crop harvest physical vector or seed-lot component drifted: "
                + owner.operationId);
    }

    private static void ValidateFrozenSeasonalPrimaryLoss(
        CropHarvestOutputSaveData owner)
    {
        if (owner == null)
            throw new InvalidOperationException(
                "Crop harvest seasonal-loss owner is missing.");
        if (owner.seasonalPrimaryBatchLossPercent == 0)
        {
            if (!string.IsNullOrEmpty(
                    owner.seasonalDamageSourceEventInstanceId)
                || !string.IsNullOrEmpty(
                    owner.seasonalDamageSourceDefinitionId)
                || owner.primaryQuantityBeforeSeasonalLoss != 0
                || owner.seasonalPrimaryLossQuantity != 0)
                throw new InvalidOperationException(
                    "Crop harvest contains partial seasonal-loss provenance.");
            return;
        }
        if (string.IsNullOrWhiteSpace(
                owner.seasonalDamageSourceEventInstanceId)
            || string.IsNullOrWhiteSpace(
                owner.seasonalDamageSourceDefinitionId)
            || owner.primaryQuantityBeforeSeasonalLoss <= 0
            || owner.seasonalPrimaryLossQuantity
                != CropHarvestOutputRules.ResolvePrimaryBatchLoss(
                    owner.primaryQuantityBeforeSeasonalLoss,
                    owner.seasonalPrimaryBatchLossPercent)
            || owner.harvestQuantity
                != CropHarvestOutputRules.ApplyPrimaryBatchLoss(
                    owner.primaryQuantityBeforeSeasonalLoss,
                    owner.seasonalPrimaryBatchLossPercent))
        {
            throw new InvalidOperationException(
                "Crop harvest seasonal-loss arithmetic or provenance drifted.");
        }
    }

    private void ValidateDetachedPlot(CropPlotSaveData plot)
    {
        CropHarvestOutputSaveData owner = plot?.pendingHarvest;
        CropWaterRefillSaveData water = plot?.waterRefill;
        bool hasHarvest = owner != null
            && owner.phase != CropHarvestOutputPhase.None;
        bool hasWater = water != null
            && water.phase != CropWaterRefillPhase.None;
        if (!hasHarvest && !hasWater)
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
            || plotAbility == null)
            throw new InvalidOperationException(
                "Crop detached facility authority drifted: "
                + (plot.buildingInstanceId ?? string.Empty));
        if (hasWater
            && !water.requiredWork.Equals(
                CropWaterRules.RequireRefillWork(facility)))
            throw new InvalidOperationException(
                "Crop water-refill authored work drifted: "
                + plot.buildingInstanceId);
        if (!hasHarvest)
            return;
        if (plotAbility.Indoor != owner.indoor)
            throw new InvalidOperationException(
                "Crop harvest detached indoor authority drifted: "
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
