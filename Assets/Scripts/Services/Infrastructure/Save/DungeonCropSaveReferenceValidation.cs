using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static DungeonSaveReferenceRules;

/// <summary>Validates detached crop and seed joins.</summary>
internal sealed class DungeonCropSaveReferenceValidation
{
    private readonly IResourceEconomyContentCatalog economyContent;

    internal DungeonCropSaveReferenceValidation(IResourceEconomyContentCatalog economyContent)
    {
        this.economyContent = economyContent ?? throw new ArgumentNullException(nameof(economyContent));
    }

    internal void ValidateCropPlots(
        DungeonCropPlotSaveData source,
        DungeonSaveBuildingReferenceIndex buildings,
        DungeonGameRestoreReport report)
    {
        foreach (CropPlotSaveData plot in source?.plots
                     ?? new List<CropPlotSaveData>())
        {
            if (plot == null)
            {
                report.AddError("Crop-plot aggregate contains a null plot.");
                continue;
            }
            RequireReference(
                plot.buildingInstanceId,
                buildings.InstanceIds,
                "Crop plot building",
                report);
            if (!buildings.InstanceDefinitions.TryGetValue(
                    plot.buildingInstanceId,
                    out BuildingSO building)
                || building?.GetAbility<BuildingCropPlotAbility>()
                    is not BuildingCropPlotAbility ability)
            {
                report.AddError(
                    $"Crop plot '{plot.buildingInstanceId}' is not backed by a crop-plot building definition.");
                continue;
            }
            if (!economyContent.TryGetCrop(
                    plot.cropId,
                    out CropDefinitionSO crop))
            {
                report.AddError(
                    $"Crop plot '{plot.buildingInstanceId}' references unknown crop '{plot.cropId}'.");
            }
            else if (ability.Indoor && !crop.IndoorAllowed)
            {
                report.AddError(
                    $"Crop '{plot.cropId}' is not allowed in indoor plot '{plot.buildingInstanceId}'.");
            }
        }
    }

    internal void ValidateCertifiedSeeds(
        CertifiedSeedWorldSaveData source,
        ISet<string> buildingIds,
        DungeonSavePhysicalReferenceIndex physical,
        DungeonGameRestoreReport report)
    {
        if (source == null
            || source.version != CertifiedSeedWorldSaveData.CurrentVersion
            || source.nextOrderSequence < 0
            || source.orders == null)
        {
            report.AddError("Certified-seed aggregate is missing or invalid.");
            return;
        }
        HashSet<string> orderIds = new(StringComparer.Ordinal);
        foreach (CertifiedSeedOrderSaveData order in source.orders)
        {
            if (order == null
                || string.IsNullOrWhiteSpace(order.orderId)
                || !string.Equals(
                    order.orderId,
                    order.orderId.Trim(),
                    StringComparison.Ordinal)
                || !orderIds.Add(order.orderId)
                || order.orderSequence < 0
                || order.orderSequence >= source.nextOrderSequence)
            {
                report.AddError(
                    $"Certified-seed aggregate contains an invalid or duplicate order '{order?.orderId ?? string.Empty}'.");
                continue;
            }
            if (order.phase !=
                CertifiedSeedOrderPhase.FacilityDestroyedLossPending)
            {
                RequireReference(
                    order.facilityInstanceId,
                    buildingIds,
                    "Certified-seed facility",
                    report);
            }
            if (!economyContent.TryGetCrop(
                    order.cropId,
                    out CropDefinitionSO crop)
                || crop == null)
            {
                report.AddError(
                    $"Certified-seed order '{order.orderId}' references unknown crop '{order.cropId}'.");
                continue;
            }
            if (order.phase == CertifiedSeedOrderPhase.Planned)
                continue;
            CropPhysicalCommitSaveData owner = order.pendingInput;
            if (owner == null
                || !physical.PendingBatchByOperation.TryGetValue(
                    owner.operationId ?? string.Empty,
                    out PhysicalItemBatchDispositionSaveData pending)
                || pending == null
                || pending.kind != (int)PhysicalItemDispositionKind.Transfer
                || !string.Equals(
                    pending.reasonCode,
                    owner.reasonCode,
                    StringComparison.Ordinal)
                || !string.Equals(
                    pending.commitId,
                    owner.commitId,
                    StringComparison.Ordinal)
                || pending.quantity != owner.inputQuantity
                || pending.inputMassGrams != owner.inputMassGrams)
            {
                report.AddError(
                    $"Certified-seed order '{order.orderId}' has no exact pending input receipt.");
                continue;
            }
            if (order.phase ==
                CertifiedSeedOrderPhase.FacilityDestroyedLossPending)
                continue;
            bool hasPhysicalOutput = order.phase is
                CertifiedSeedOrderPhase.OutputPublished
                or CertifiedSeedOrderPhase
                    .OutputRestoredAwaitingInputAcknowledgement;
            if (!hasPhysicalOutput)
                continue;
            ProductionDomainOutputPublicationSaveData publication =
                order.outputPublication;
            ProductionDomainPublishedStackSaveData publishedStack =
                publication?.stacks?.SingleOrDefault();
            if (!ProductionDomainOutputPublicationService
                    .TryValidateCommittedOwner(publication, out _)
                || publication.outputAcknowledged !=
                    (order.phase == CertifiedSeedOrderPhase
                        .OutputRestoredAwaitingInputAcknowledgement)
                || publishedStack == null
                || !physical.StacksById.TryGetValue(
                    publishedStack.stackId ?? string.Empty,
                    out WorldItemStackSaveData output)
                || output == null
                || output.quantity != 1
                || output.quantity != publishedStack.quantity
                || !string.Equals(
                    output.itemId,
                    crop.SeedItemId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    output.itemId,
                    publishedStack.itemId,
                    StringComparison.Ordinal)
                || output.state != WorldItemStackState.FacilityOutputBuffer
                || !string.Equals(
                    output.destinationId,
                    publication.destinationId,
                    StringComparison.Ordinal)
                || output.gridX != publication.destinationX
                || output.gridY != publication.destinationY
                || order.certifiedSeedLot == null)
            {
                report.AddError(
                    $"Certified-seed order '{order.orderId}' has no exact physical output.");
                continue;
            }
            try
            {
                SeedLotState outputState = SeedLotItemStateCodec.Decode(
                    output.components);
                if (!SeedLotEquals(outputState, order.certifiedSeedLot))
                    report.AddError(
                        $"Certified-seed order '{order.orderId}' output state does not match its owner.");
            }
            catch (Exception exception)
            {
                report.AddError(
                    $"Certified-seed order '{order.orderId}' output is invalid: {exception.Message}");
            }
        }
    }

    internal static bool SeedLotEquals(SeedLotState left, SeedLotState right) =>
        left != null
        && right != null
        && string.Equals(left.cropId, right.cropId, StringComparison.Ordinal)
        && string.Equals(
            left.cultivarGenomeId,
            right.cultivarGenomeId,
            StringComparison.Ordinal)
        && left.generation == right.generation
        && left.pathogenLoad.Equals(right.pathogenLoad);

    internal void ValidateCropEcology(
        CropEcologyWorldSaveData source,
        DungeonCropPlotSaveData cropPlots,
        CharacterNarrativeWorldSaveData characterNarrative,
        GameplayOutcomeLedgerSaveData outcomeLedger,
        ISet<string> buildingIds,
        IReadOnlyList<(string itemDefinitionId, SeedLotState state)> physicalSeedLots,
        DungeonGameRestoreReport report)
    {
        HashSet<string> cropPlotIds = (cropPlots?.plots ?? new List<CropPlotSaveData>())
            .Where(value => value != null)
            .Select(value => value.buildingInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> genomes = new(StringComparer.Ordinal);
        foreach (CultivarGenomeSaveData genome in (source?.activeCultivars
                     ?? new List<CultivarGenomeSaveData>())
                 .Concat(source?.frozenCultivars ?? new List<CultivarGenomeSaveData>()))
        {
            if (genome == null || string.IsNullOrWhiteSpace(genome.genomeId)
                || !genomes.TryAdd(genome.genomeId, genome.cropId))
            {
                report.AddError("Crop-ecology aggregate contains an invalid or duplicate cultivar genome.");
                continue;
            }
            RequireCropDefinition(genome.cropId, $"Cultivar genome '{genome.genomeId}'", report);
        }
        foreach (CropEcologyPlotSaveData plot in source?.plots
                     ?? new List<CropEcologyPlotSaveData>())
        {
            if (plot == null)
            {
                report.AddError("Crop-ecology aggregate contains a null plot.");
                continue;
            }
            RequireReference(plot.plotId, buildingIds, "Crop-ecology plot building", report);
            if (!cropPlotIds.Contains(plot.plotId))
            {
                report.AddError(
                    $"Crop-ecology plot '{plot.plotId}' has no matching crop-plot aggregate record.");
            }
            if (!string.IsNullOrWhiteSpace(plot.cropId))
                RequireCropDefinition(plot.cropId, $"Crop-ecology plot '{plot.plotId}'", report);
            if (!string.IsNullOrWhiteSpace(plot.cultivarGenomeId)
                && (!genomes.TryGetValue(plot.cultivarGenomeId, out string genomeCropId)
                    || !string.Equals(genomeCropId, plot.cropId, StringComparison.Ordinal)))
            {
                report.AddError(
                    $"Crop-ecology plot '{plot.plotId}' references missing or mismatched genome '{plot.cultivarGenomeId}'.");
            }
        }
        foreach ((string itemDefinitionId, SeedLotState seedLot) in physicalSeedLots)
        {
            RequireCropDefinition(seedLot.cropId, $"Physical seed lot '{itemDefinitionId}'", report);
            if (!genomes.TryGetValue(seedLot.cultivarGenomeId, out string genomeCropId)
                || !string.Equals(genomeCropId, seedLot.cropId, StringComparison.Ordinal))
            {
                report.AddError(
                    $"Physical seed lot '{itemDefinitionId}' references missing or mismatched genome '{seedLot.cultivarGenomeId}'.");
            }
        }

        try
        {
            CropEcologyAggregateState ecologyCandidate =
                CropEcologyAggregateState.Restore(source);
            CropHarvestOwnerValidationSnapshot[] owners = (
                    cropPlots?.plots ?? new List<CropPlotSaveData>())
                .Where(value => value?.pendingHarvest != null
                    && value.pendingHarvest.phase
                        != CropHarvestOutputPhase.None)
                .Select(value => new CropHarvestOwnerValidationSnapshot
                {
                    PlotId = value.buildingInstanceId,
                    Owner = value.pendingHarvest
                })
                .ToArray();
            CropPhysicalRestoreGuard.ValidatePreparedHarvestOwnerSnapshots(
                owners,
                ecologyCandidate.CapturePreparedHarvests(),
                ExtremeTraitRuntime.CapturePreparedGoldenHarvests(
                    characterNarrative?.identityStates));
            CropHarvestCompletionDeliveryRestoreJoin.Validate(
                cropPlots?.plots,
                characterNarrative?.workCompletionDeliveries,
                outcomeLedger);
        }
        catch (Exception exception)
        {
            report.AddError(
                "Crop harvest prepared restore join is invalid: "
                + exception.Message);
        }
    }

    internal void RequireCropDefinition(
        string cropId,
        string label,
        DungeonGameRestoreReport report)
    {
        if (!economyContent.TryGetCrop(cropId?.Trim() ?? string.Empty, out _))
            report.AddError($"{label} references unknown crop '{cropId ?? string.Empty}'.");
    }
}
