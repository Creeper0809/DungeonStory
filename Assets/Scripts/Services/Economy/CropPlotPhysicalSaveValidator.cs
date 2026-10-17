using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

using static CropPlotSaveContract;
using static CropPlotOperationIdentity;
internal sealed class CropPlotPhysicalSaveValidator
{
    private readonly IProductionItemGateway items;
    private readonly IGameplayEffectResultBoundsQuery effectBounds;
    private readonly IProductionOutputCapabilityRegistry outputCapabilities;
    private readonly IProductionDomainOutputPublicationService outputPublication;
    private readonly ICharacterPerformanceDefinitionMaximumQuery performanceMaximum;
    private readonly CropTreatmentDefinitionQuery treatmentDefinitions;

    internal CropPlotPhysicalSaveValidator(CropPlotWorldDependencies world,
        CropTreatmentDefinitionQuery treatmentDefinitions)
    {
        items = world.Items;
        effectBounds = world.EffectBounds;
        outputCapabilities = world.OutputCapabilities;
        outputPublication = world.OutputPublication;
        performanceMaximum = world.PerformanceMaximum;
        this.treatmentDefinitions = treatmentDefinitions;
    }

    internal void ValidatePendingHarvest(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId)
    {
        CropHarvestOutputSaveData owner = saved.pendingHarvest;
        if (saved.nextHarvestOperationSequence < 0 || owner == null)
            throw new InvalidOperationException(
                "Crop harvest output owner or sequence is invalid.");
        if (owner.phase == CropHarvestOutputPhase.None)
        {
            bool empty = owner.operationSequence == 0
                && string.IsNullOrEmpty(owner.operationId)
                && string.IsNullOrEmpty(owner.cropId)
                && !owner.indoor
                && string.IsNullOrEmpty(owner.harvesterId)
                && string.IsNullOrEmpty(owner.outcomeId)
                && string.IsNullOrEmpty(owner.ecologyOutcomeFingerprint)
                && string.IsNullOrEmpty(owner.ecologyBeforeFingerprint)
                && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                && !owner.ecologyCommitted
                && !owner.ecologyAcknowledged
                && !owner.goldenPrepared
                && string.IsNullOrEmpty(owner.goldenTraitDefinitionId)
                && string.IsNullOrEmpty(owner.goldenOutcomeFingerprint)
                && owner.goldenOutcome == default
                && owner.goldenPrimaryMultiplier == 0f
                && owner.goldenSecondaryMultiplier == 0f
                && owner.goldenRollHash == 0UL
                && !owner.goldenCommitted
                && !owner.goldenAcknowledged
                && !owner.completionEventPublished
                && string.IsNullOrEmpty(owner.completionPublicationKey)
                && string.IsNullOrEmpty(owner.completionDeliveryId)
                && string.IsNullOrEmpty(owner.completionDeliveryFingerprint)
                && owner.completionAbsoluteDay == 0
                && string.IsNullOrEmpty(owner.harvestItemId)
                && owner.harvestQuantity == 0
                && string.IsNullOrEmpty(owner.seedItemId)
                && owner.seedQuantity == 0
                && IsSerializedEmptySeedLot(owner.returnedSeedLot)
                && owner.maximumHarvestQuantity == 0
                && owner.maximumSeedQuantity == 0
                && string.IsNullOrEmpty(
                    owner.seasonalDamageSourceEventInstanceId)
                && string.IsNullOrEmpty(
                    owner.seasonalDamageSourceDefinitionId)
                && owner.seasonalPrimaryBatchLossPercent == 0
                && owner.primaryQuantityBeforeSeasonalLoss == 0
                && owner.seasonalPrimaryLossQuantity == 0
                && owner.harvestCapability is { IsEmpty: true }
                && owner.seedCapability is { IsEmpty: true }
                && owner.outputPublication is { IsEmpty: true };
            if (!empty)
                throw new InvalidOperationException(
                    "Empty crop harvest owner contains frozen provenance: "
                    + DescribeEmptyHarvestProvenance(owner));
            return;
        }

        CropSeasonalYieldDamageSaveData seasonalDamage =
            saved.seasonalYieldDamage;
        bool seasonalLossValid;
        if (seasonalDamage.IsEmpty)
        {
            seasonalLossValid = string.IsNullOrEmpty(
                    owner.seasonalDamageSourceEventInstanceId)
                && string.IsNullOrEmpty(
                    owner.seasonalDamageSourceDefinitionId)
                && owner.seasonalPrimaryBatchLossPercent == 0
                && owner.primaryQuantityBeforeSeasonalLoss == 0
                && owner.seasonalPrimaryLossQuantity == 0;
        }
        else
        {
            seasonalLossValid = string.Equals(
                    owner.seasonalDamageSourceEventInstanceId,
                    seasonalDamage.sourceEventInstanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    owner.seasonalDamageSourceDefinitionId,
                    seasonalDamage.sourceDefinitionId,
                    StringComparison.Ordinal)
                && owner.seasonalPrimaryBatchLossPercent
                    == seasonalDamage.primaryBatchLossPercent
                && owner.primaryQuantityBeforeSeasonalLoss > 0
                && owner.seasonalPrimaryLossQuantity
                    == CropHarvestOutputRules.ResolvePrimaryBatchLoss(
                        owner.primaryQuantityBeforeSeasonalLoss,
                        owner.seasonalPrimaryBatchLossPercent)
                && owner.harvestQuantity
                    == CropHarvestOutputRules.ApplyPrimaryBatchLoss(
                        owner.primaryQuantityBeforeSeasonalLoss,
                        owner.seasonalPrimaryBatchLossPercent);
        }

        if (!Enum.IsDefined(typeof(CropHarvestOutputPhase), owner.phase)
            || saved.phase != CropPlotPhase.Harvesting
            || saved.harvestWork + 0.001f < crop.HarvestWork
            || owner.operationSequence != saved.nextHarvestOperationSequence
            || !string.Equals(
                owner.operationId,
                FormatHarvestOperationId(plotId, owner.operationSequence),
                StringComparison.Ordinal)
            || !string.Equals(owner.cropId, crop.CropId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(owner.ecologyOutcomeFingerprint)
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                owner.ecologyBeforeFingerprint)
            || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                owner.ecologyAfterFingerprint)
            || owner.harvestQuantity <= 0
            || owner.seedQuantity <= 0
            || owner.returnedSeedLot == null
            || !string.Equals(
                owner.returnedSeedLot.cropId,
                crop.CropId,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.harvestItemId,
                crop.HarvestItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.seedItemId,
                crop.SeedItemId,
                StringComparison.Ordinal)
            || owner.maximumHarvestQuantity !=
                CropHarvestOutputMaximumAuthority.ResolveMaximumHarvestQuantity(
                    crop,
                    owner.indoor,
                    performanceMaximum)
            || owner.maximumSeedQuantity !=
                CropHarvestOutputMaximumAuthority
                    .ResolveMaximumReturnedSeedQuantity(effectBounds)
            || owner.harvestQuantity > owner.maximumHarvestQuantity
            || owner.seedQuantity > owner.maximumSeedQuantity
            || !seasonalLossValid
            || owner.ecologyAcknowledged && !owner.ecologyCommitted
            || owner.goldenCommitted && !owner.goldenPrepared
            || owner.goldenAcknowledged && !owner.goldenCommitted
            || owner.goldenCommitted && !owner.ecologyCommitted
            || owner.ecologyAcknowledged && !owner.completionEventPublished
            || owner.goldenAcknowledged
                && (!owner.ecologyAcknowledged
                    || !owner.completionEventPublished)
            || owner.completionEventPublished && !owner.cropOutcomeAcknowledged
            || owner.cropOutcomeAcknowledged
                && (owner.phase != CropHarvestOutputPhase
                        .OutputRestoredAwaitingFinalization
                    || !owner.ecologyCommitted
                    || !owner.outputPublication.outputAcknowledged
                    || owner.goldenPrepared && !owner.goldenCommitted)
            || owner.completionAbsoluteDay < 0
            || !IsCanonicalToken(owner.harvesterId)
            || !IsCanonicalToken(owner.outcomeId)
            || !Enum.IsDefined(typeof(ExtremeRiskOutcome), owner.goldenOutcome)
            || !IsFinitePositive(owner.goldenPrimaryMultiplier)
            || !IsFinitePositive(owner.goldenSecondaryMultiplier))
        {
            throw new InvalidOperationException(
                "Crop harvest frozen owner contradicts plot state or authored maximums.");
        }

        ValidateHarvestTerminalPublication(saved, plotId);
        if (string.IsNullOrEmpty(owner.harvesterId))
        {
            if (!string.IsNullOrEmpty(owner.completionPublicationKey)
                || !string.IsNullOrEmpty(owner.completionDeliveryId)
                || !string.IsNullOrEmpty(owner.completionDeliveryFingerprint))
                throw new InvalidOperationException(
                    "Workerless crop harvest contains completion delivery provenance.");
        }
        else
        {
            _ = new CropHarvestCompletedFact(plotId, owner, saved.cycleExecutionReceipt);
            ValidateCropTerminalPublicationFields(owner.completionPublicationKey, owner.completionDeliveryId,
                owner.completionDeliveryFingerprint, owner.completionEventPublished);
            if (!string.IsNullOrEmpty(owner.completionPublicationKey) && !owner.cropOutcomeAcknowledged)
                throw new InvalidOperationException("Crop completion publication precedes its terminal acknowledgement.");
        }

        if (owner.goldenPrepared)
        {
            if (string.IsNullOrWhiteSpace(owner.goldenTraitDefinitionId)
                || string.IsNullOrWhiteSpace(owner.goldenOutcomeFingerprint)
                || !string.Equals(
                    owner.harvesterId,
                    saved.goldenHarvestHarvesterId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outcomeId,
                    owner.goldenOutcome.ToString().ToLowerInvariant(),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Prepared Golden Harvest provenance is incomplete.");
            }
        }
        else if (!string.IsNullOrEmpty(owner.goldenTraitDefinitionId)
            || !string.IsNullOrEmpty(owner.goldenOutcomeFingerprint)
            || owner.goldenOutcome != ExtremeRiskOutcome.Normal
            || owner.goldenPrimaryMultiplier != 1f
            || owner.goldenSecondaryMultiplier != 1f
            || owner.goldenRollHash != 0UL
            || owner.goldenCommitted
            || owner.goldenAcknowledged)
        {
            throw new InvalidOperationException(
                "Normal crop harvest contains Golden Harvest provenance.");
        }

        ProductionOutputCapabilityDescriptor harvest = outputCapabilities
            .CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.HarvestOutputLineId(
                    crop.CropId),
                crop.HarvestItemId,
                ProductionOutputCapabilityIds.StandardDefinition);
        ProductionOutputCapabilityDescriptor seed = outputCapabilities
            .CaptureDeclaredDescriptor(
                CropHarvestOutputMaximumAuthority.SeedOutputLineId(crop.CropId),
                crop.SeedItemId,
                ProductionOutputCapabilityIds.CropHarvestSeedLot);
        string expectedOutcomeFingerprint =
            CaptureHarvestOutcomeFingerprint(plotId, owner);
        if (!CapabilityMatches(owner.harvestCapability, harvest)
            || !CapabilityMatches(owner.seedCapability, seed)
            || owner.phase != CropHarvestOutputPhase.Frozen
                && !string.Equals(
                    expectedOutcomeFingerprint,
                    owner.outputPublication.outcomeFingerprint,
                    StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop harvest output capability or outcome fingerprint drifted.");
        }

        if (owner.phase == CropHarvestOutputPhase.Frozen)
        {
            if (owner.ecologyCommitted
                || owner.ecologyAcknowledged
                || owner.goldenCommitted
                || owner.goldenAcknowledged
                || owner.completionEventPublished)
                throw new InvalidOperationException(
                    "Frozen crop harvest contains committed provenance.");
            if (owner.outputPublication.IsEmpty)
                return;
            if (!ProductionDomainOutputPublicationService
                    .TryValidateRestorableOwner(
                        owner.outputPublication,
                        out bool committed,
                        out string restorableFailure)
                || committed
                || !string.Equals(
                    expectedOutcomeFingerprint,
                    owner.outputPublication.outcomeFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.batchCommitId,
                    HarvestOutputBatchCommitPrefix + owner.operationId,
                    StringComparison.Ordinal)
                || !string.IsNullOrEmpty(
                        owner.outputPublication.publicationOperationId)
                    && !string.Equals(
                        owner.outputPublication.publicationOperationId,
                        HarvestOutputPublicationOperationPrefix
                            + owner.operationId + ":"
                            + owner.outputPublication.publicationAttempt.ToString(
                                "D4",
                                System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.ownerFacilityId,
                    plotId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.ownerDomain,
                    ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.destinationId,
                    ProductionBillIdentity.OutputDestinationPrefix + plotId.Value,
                    StringComparison.Ordinal)
                || !string.Equals(
                    owner.outputPublication.ownerOperationId,
                    owner.outputPublication.destinationId,
                    StringComparison.Ordinal)
                || owner.outputPublication.acknowledgementDisposition !=
                    ProductionDomainOutputAcknowledgementDisposition
                        .ReleaseLooseOrDestination)
                throw new InvalidOperationException(
                    "Frozen crop harvest publication owner is invalid: "
                    + restorableFailure);
            return;
        }

        if (!ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                owner.outputPublication,
                out string publicationFailure)
            || !string.Equals(
                owner.outputPublication.batchCommitId,
                HarvestOutputBatchCommitPrefix + owner.operationId,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.publicationOperationId,
                HarvestOutputPublicationOperationPrefix
                    + owner.operationId + ":"
                    + owner.outputPublication.publicationAttempt.ToString(
                        "D4",
                        System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.ownerFacilityId,
                plotId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.ownerDomain,
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.destinationId,
                ProductionBillIdentity.OutputDestinationPrefix + plotId.Value,
                StringComparison.Ordinal)
            || !string.Equals(
                owner.outputPublication.ownerOperationId,
                owner.outputPublication.destinationId,
                StringComparison.Ordinal)
            || owner.outputPublication.acknowledgementDisposition !=
                ProductionDomainOutputAcknowledgementDisposition
                    .ReleaseLooseOrDestination
            || owner.phase == CropHarvestOutputPhase.OutputCommitted
                && owner.outputPublication.outputAcknowledged
            || owner.phase == CropHarvestOutputPhase
                    .OutputRestoredAwaitingFinalization
                && !owner.outputPublication.outputAcknowledged)
        {
            throw new InvalidOperationException(
                "Crop harvest committed publication is invalid: "
                + publicationFailure);
        }
        ValidatePublishedHarvestVector(owner);
    }

    private static void ValidatePublishedHarvestVector(
        CropHarvestOutputSaveData owner)
    {
        ProductionDomainPublishedStackSaveData[] stacks =
            owner.outputPublication.stacks?.ToArray()
            ?? Array.Empty<ProductionDomainPublishedStackSaveData>();
        if (stacks.Length != 2
            || stacks.Select(value => value.outputLineId)
                    .Distinct(StringComparer.Ordinal).Count() != 2)
            throw new InvalidOperationException(
                "Crop harvest publication is not an exact two-line vector.");
        ProductionDomainPublishedStackSaveData harvest = stacks.SingleOrDefault(
            value => string.Equals(
                value.outputLineId,
                owner.harvestCapability.outputLineId,
                StringComparison.Ordinal));
        ProductionDomainPublishedStackSaveData seed = stacks.SingleOrDefault(
            value => string.Equals(
                value.outputLineId,
                owner.seedCapability.outputLineId,
                StringComparison.Ordinal));
        if (harvest == null
            || seed == null
            || !string.Equals(
                harvest.itemId,
                owner.harvestItemId,
                StringComparison.Ordinal)
            || harvest.quantity != owner.harvestQuantity
            || !string.Equals(
                seed.itemId,
                owner.seedItemId,
                StringComparison.Ordinal)
            || seed.quantity != owner.seedQuantity)
            throw new InvalidOperationException(
                "Crop harvest publication vector drifted from its frozen outcome.");
    }

    private static string DescribeEmptyHarvestProvenance(
        CropHarvestOutputSaveData owner)
    {
        if (owner == null)
            return "owner=null";

        List<string> fields = new();
        void Add(bool present, string field)
        {
            if (present)
                fields.Add(field);
        }

        Add(owner.operationSequence != 0, nameof(owner.operationSequence));
        Add(!string.IsNullOrEmpty(owner.operationId), nameof(owner.operationId));
        Add(!string.IsNullOrEmpty(owner.cropId), nameof(owner.cropId));
        Add(owner.indoor, nameof(owner.indoor));
        Add(!string.IsNullOrEmpty(owner.harvesterId), nameof(owner.harvesterId));
        Add(!string.IsNullOrEmpty(owner.outcomeId), nameof(owner.outcomeId));
        Add(!string.IsNullOrEmpty(owner.ecologyOutcomeFingerprint),
            nameof(owner.ecologyOutcomeFingerprint));
        Add(!string.IsNullOrEmpty(owner.ecologyBeforeFingerprint),
            nameof(owner.ecologyBeforeFingerprint));
        Add(!string.IsNullOrEmpty(owner.ecologyAfterFingerprint),
            nameof(owner.ecologyAfterFingerprint));
        Add(owner.ecologyCommitted, nameof(owner.ecologyCommitted));
        Add(owner.ecologyAcknowledged, nameof(owner.ecologyAcknowledged));
        Add(owner.goldenPrepared, nameof(owner.goldenPrepared));
        Add(!string.IsNullOrEmpty(owner.goldenTraitDefinitionId),
            nameof(owner.goldenTraitDefinitionId));
        Add(!string.IsNullOrEmpty(owner.goldenOutcomeFingerprint),
            nameof(owner.goldenOutcomeFingerprint));
        Add(owner.goldenOutcome != default, nameof(owner.goldenOutcome));
        Add(owner.goldenPrimaryMultiplier != 0f,
            nameof(owner.goldenPrimaryMultiplier));
        Add(owner.goldenSecondaryMultiplier != 0f,
            nameof(owner.goldenSecondaryMultiplier));
        Add(owner.goldenRollHash != 0UL, nameof(owner.goldenRollHash));
        Add(owner.goldenCommitted, nameof(owner.goldenCommitted));
        Add(owner.goldenAcknowledged, nameof(owner.goldenAcknowledged));
        Add(owner.cropOutcomeAcknowledged, nameof(owner.cropOutcomeAcknowledged));
        Add(owner.terminalReceipt == null || !owner.terminalReceipt.IsEmpty, nameof(owner.terminalReceipt));
        Add(!string.IsNullOrEmpty(owner.terminalPlotDisplayName), nameof(owner.terminalPlotDisplayName));
        Add(!string.IsNullOrEmpty(owner.terminalCropDisplayName), nameof(owner.terminalCropDisplayName));
        Add(!string.IsNullOrEmpty(owner.terminalPublicationKey), nameof(owner.terminalPublicationKey));
        Add(!string.IsNullOrEmpty(owner.terminalPublicationId), nameof(owner.terminalPublicationId));
        Add(!string.IsNullOrEmpty(owner.terminalPublicationFingerprint), nameof(owner.terminalPublicationFingerprint));
        Add(owner.completionEventPublished,
            nameof(owner.completionEventPublished));
        Add(!string.IsNullOrEmpty(owner.completionDeliveryId),
            nameof(owner.completionDeliveryId));
        Add(!string.IsNullOrEmpty(owner.completionPublicationKey), nameof(owner.completionPublicationKey));
        Add(!string.IsNullOrEmpty(owner.completionDeliveryFingerprint),
            nameof(owner.completionDeliveryFingerprint));
        Add(owner.completionAbsoluteDay != 0,
            nameof(owner.completionAbsoluteDay));
        Add(!string.IsNullOrEmpty(owner.harvestItemId),
            nameof(owner.harvestItemId));
        Add(owner.harvestQuantity != 0, nameof(owner.harvestQuantity));
        Add(!string.IsNullOrEmpty(owner.seedItemId), nameof(owner.seedItemId));
        Add(owner.seedQuantity != 0, nameof(owner.seedQuantity));
        Add(!IsSerializedEmptySeedLot(owner.returnedSeedLot),
            nameof(owner.returnedSeedLot));
        Add(owner.maximumHarvestQuantity != 0,
            nameof(owner.maximumHarvestQuantity));
        Add(owner.maximumSeedQuantity != 0,
            nameof(owner.maximumSeedQuantity));
        Add(!string.IsNullOrEmpty(owner.seasonalDamageSourceEventInstanceId),
            nameof(owner.seasonalDamageSourceEventInstanceId));
        Add(!string.IsNullOrEmpty(owner.seasonalDamageSourceDefinitionId),
            nameof(owner.seasonalDamageSourceDefinitionId));
        Add(owner.seasonalPrimaryBatchLossPercent != 0,
            nameof(owner.seasonalPrimaryBatchLossPercent));
        Add(owner.primaryQuantityBeforeSeasonalLoss != 0,
            nameof(owner.primaryQuantityBeforeSeasonalLoss));
        Add(owner.seasonalPrimaryLossQuantity != 0,
            nameof(owner.seasonalPrimaryLossQuantity));
        Add(owner.harvestCapability is not { IsEmpty: true },
            nameof(owner.harvestCapability));
        Add(owner.seedCapability is not { IsEmpty: true },
            nameof(owner.seedCapability));
        Add(owner.outputPublication == null,
            nameof(owner.outputPublication) + "=null");
        if (owner.outputPublication is { IsEmpty: false })
        {
            fields.Add(
                nameof(owner.outputPublication)
                + "="
                + JsonUtility.ToJson(owner.outputPublication));
        }
        return fields.Count == 0 ? "unknown" : string.Join(",", fields);
    }

    private static void ValidateHarvestTerminalPublication(CropPlotSaveData saved, BuildingInstanceId plotId)
    {
        CropHarvestOutputSaveData owner = saved.pendingHarvest;
        if (owner.terminalReceipt == null)
            throw new InvalidOperationException("Crop harvest terminal snapshot must be explicit.");
        ValidateCropTerminalPublicationFields(owner.terminalPublicationKey, owner.terminalPublicationId,
            owner.terminalPublicationFingerprint, owner.cropOutcomeAcknowledged);
        if (owner.terminalReceipt.IsEmpty)
        {
            if (!string.IsNullOrEmpty(owner.terminalPlotDisplayName)
                || !string.IsNullOrEmpty(owner.terminalCropDisplayName)
                || !string.IsNullOrEmpty(owner.terminalPublicationKey) || owner.cropOutcomeAcknowledged)
                throw new InvalidOperationException("Empty harvest terminal snapshot contains publication provenance.");
            return;
        }
        if (owner.phase != CropHarvestOutputPhase.OutputRestoredAwaitingFinalization
            || !owner.outputPublication.outputAcknowledged || !owner.ecologyCommitted
            || owner.goldenPrepared && !owner.goldenCommitted)
            throw new InvalidOperationException("Harvest terminal was frozen before its physical effects completed.");
        _ = new CropCycleTerminalFact(plotId, owner.terminalReceipt,
            owner.terminalPlotDisplayName, owner.terminalCropDisplayName,
            owner.completionAbsoluteDay, checked((long)owner.operationSequence + 1L));
        CropCycleExecutionReceiptSaveData expected = CropPlanExecutionReceiptAuthority.Complete(
            saved.cycleExecutionReceipt, owner, saved.currentWater);
        if (owner.terminalReceipt.sourceDigest != expected.sourceDigest)
            throw new InvalidOperationException("Harvest terminal snapshot drifted from its authoritative execution.");
    }

    internal void ValidatePendingWaterRefill(
        CropPlotSaveData saved,
        CropDefinitionSO crop,
        BuildingInstanceId plotId)
    {
        CropWaterRefillSaveData owner = saved.waterRefill;
        bool hasPublication = !string.IsNullOrEmpty(owner.publicationKey);
        if (hasPublication)
        {
            ValidateCropCommandPublicationKey(owner.publicationKey);
            _ = new CropWaterRefillFact(plotId, owner);
        }
        else if (!string.IsNullOrEmpty(owner.plotDisplayName)
            || !string.IsNullOrEmpty(owner.itemDisplayName)
            || owner.locationX != 0 || owner.locationY != 0
            || owner.phase is CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished or CropWaterRefillPhase.LedgerCommitted)
            throw new InvalidOperationException("Crop water-refill publication snapshot is missing its key.");
        if (owner.phase == CropWaterRefillPhase.None)
        {
            bool empty = !hasPublication && owner.operationSequence == 0
                && owner.outcomeSchemaVersion == 0
                && string.IsNullOrEmpty(owner.operationId)
                && string.IsNullOrEmpty(owner.reasonCode)
                && string.IsNullOrEmpty(owner.destinationId)
                && string.IsNullOrEmpty(owner.itemId)
                && owner.quantity == 0
                && owner.requiredWork == 0f
                && owner.completedWork == 0f
                && string.IsNullOrEmpty(owner.commitId)
                && owner.inputQuantity == 0
                && owner.inputMassGrams == 0L
                && string.IsNullOrEmpty(owner.requestFingerprint)
                && string.IsNullOrEmpty(owner.physicalRequestFingerprint)
                && owner.physicalOutcomeOwnerRevision == 0L
                && (owner.sourceStackIds?.Count ?? 0) == 0
                && string.IsNullOrEmpty(owner.responsibleWorkerId)
                && string.IsNullOrEmpty(owner.responsibleWorkerDisplayName)
                && (owner.workContributions?.Count ?? 0) == 0
                && owner.waterBefore == 0f
                && owner.waterAfter == 0f
                && owner.completedAbsoluteDay == 0
                && string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
                && string.IsNullOrEmpty(owner.failureReason);
            if (!empty)
                throw new InvalidOperationException(
                    "Empty crop water-refill owner contains provenance.");
            return;
        }

        if (!Enum.IsDefined(typeof(CropWaterRefillPhase), owner.phase)
            || owner.outcomeSchemaVersion
                != CropWaterRefillSaveData.CurrentOutcomeSchemaVersion
            || crop.DailyWater <= 0f
            || owner.operationSequence
                != saved.nextWaterRefillOperationSequence
            || !string.Equals(
                owner.operationId,
                FormatWaterRefillOperationId(
                    plotId,
                    owner.operationSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.destinationId,
                CropPlotInputOwnerAuthority.BuildWaterRefillDestinationId(
                    plotId.Value,
                    owner.operationSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.reasonCode,
                "crop-water-refill-input",
                StringComparison.Ordinal)
            || !string.Equals(
                owner.itemId,
                CropCycleInputRequirementAuthority.CleanWaterItemId,
                StringComparison.Ordinal)
            || owner.quantity != CropWaterRules.RefillQuantity
            || !float.IsFinite(owner.requiredWork)
            || owner.requiredWork < 0.1f
            || !float.IsFinite(owner.completedWork)
            || owner.completedWork < 0f
            || owner.completedWork > owner.requiredWork
            || !string.Equals(
                owner.requestFingerprint,
                CaptureWaterRefillRequestFingerprint(
                    plotId,
                    owner.operationSequence,
                    owner.destinationId,
                    owner.requiredWork),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Crop water-refill intent contradicts plot identity or current policy.");
        }

        bool committed = owner.phase is CropWaterRefillPhase.InputCommitted
            or CropWaterRefillPhase.OutcomePublished
            or CropWaterRefillPhase.LedgerCommitted;
        long expectedMass = items.GetDefinitionQuantityMassGrams(
            owner.itemId,
            owner.quantity);
        bool receiptExact = !string.IsNullOrWhiteSpace(owner.commitId)
            && string.Equals(
                owner.commitId,
                owner.commitId.Trim(),
                StringComparison.Ordinal)
            && owner.inputQuantity == owner.quantity
            && owner.inputMassGrams == expectedMass
            && IsCanonicalToken(owner.physicalRequestFingerprint)
            && owner.physicalOutcomeOwnerRevision > 0L
            && owner.sourceStackIds != null
            && owner.sourceStackIds.Count > 0
            && owner.sourceStackIds.All(IsCanonicalToken)
            && owner.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == owner.sourceStackIds.Count
            && owner.sourceStackIds.SequenceEqual(
                owner.sourceStackIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal),
                StringComparer.Ordinal);
        bool receiptEmpty = string.IsNullOrEmpty(owner.commitId)
            && owner.inputQuantity == 0
            && owner.inputMassGrams == 0L
            && string.IsNullOrEmpty(owner.physicalRequestFingerprint)
            && owner.physicalOutcomeOwnerRevision == 0L
            && (owner.sourceStackIds?.Count ?? 0) == 0;
        bool workerExact = owner.phase is CropWaterRefillPhase.Working
                or CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted
            ? IsCanonicalToken(owner.responsibleWorkerId)
                && !string.IsNullOrWhiteSpace(
                    owner.responsibleWorkerDisplayName)
                && owner.workContributions != null
                && owner.workContributions.Count > 0
            : string.IsNullOrEmpty(owner.responsibleWorkerId)
                && string.IsNullOrEmpty(owner.responsibleWorkerDisplayName)
                && (owner.workContributions?.Count ?? 0) == 0;
        bool outcomeExact = owner.phase == CropWaterRefillPhase.LedgerCommitted
            ? IsCanonicalToken(owner.gameplayOutcomeId)
                && IsCropCommandFingerprint(owner.gameplayOutcomeDigest)
            : string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest);
        if (committed != receiptExact
            || !committed && !receiptEmpty
            || committed
                && owner.completedWork + 0.001f < owner.requiredWork
            || !workerExact
            || !outcomeExact
            || committed && (!float.IsFinite(owner.waterBefore)
                || owner.waterBefore < 0f
                || owner.completedAbsoluteDay < 0)
            || owner.phase == CropWaterRefillPhase.InputCommitted
                && saved.currentWater + owner.quantity
                    > saved.waterCapacity + 0.001f
            || (owner.phase is CropWaterRefillPhase.OutcomePublished
                    or CropWaterRefillPhase.LedgerCommitted)
                && (Math.Abs(saved.currentWater - owner.waterAfter) > 0.0001f
                    || Math.Abs(
                        owner.waterBefore + owner.quantity - owner.waterAfter)
                        > 0.0001f)
            || owner.phase == CropWaterRefillPhase.WaitingForDelivery
                && owner.completedWork != 0f)
        {
            throw new InvalidOperationException(
                "Crop water-refill WIP receipt contradicts its phase.");
        }
    }

    internal void ValidatePendingTreatment(
        CropPlotSaveData saved,
        BuildingInstanceId plotId)
    {
        CropTreatmentOrderSaveData owner = saved.treatment;
        bool hasPublication = !string.IsNullOrEmpty(owner.publicationKey);
        if (hasPublication)
        {
            ValidateCropCommandPublicationKey(owner.publicationKey);
            if (owner.phase != CropTreatmentOrderPhase.PlotDestroyedLossPending)
                _ = new CropTreatmentFact(plotId, owner);
            else if (string.IsNullOrWhiteSpace(owner.plotDisplayName)
                || string.IsNullOrWhiteSpace(owner.itemDisplayName))
                throw new InvalidOperationException("Destroyed crop treatment lost its frozen presentation.");
        }
        else if (!string.IsNullOrEmpty(owner.plotDisplayName) || !string.IsNullOrEmpty(owner.itemDisplayName)
            || owner.locationX != 0 || owner.locationY != 0
            || !string.IsNullOrEmpty(owner.ecologyBeforeFingerprint)
            || owner.pestPressureBefore != 0f || owner.diseasePressureBefore != 0f
            || owner.cooldownBeforeDay != 0 || owner.completedAbsoluteDay != 0
            || owner.phase is CropTreatmentOrderPhase.InputCommitted or CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted or CropTreatmentOrderPhase.PlotDestroyedLossPending)
            throw new InvalidOperationException("Crop treatment publication snapshot is missing its key.");
        if (owner.phase == CropTreatmentOrderPhase.None)
        {
            bool empty = !hasPublication && owner.outcomeSchemaVersion == 0
                && owner.operationSequence == 0
                && string.IsNullOrEmpty(owner.operationId)
                && string.IsNullOrEmpty(owner.reasonCode)
                && string.IsNullOrEmpty(owner.destinationId)
                && string.IsNullOrEmpty(owner.itemId)
                && owner.quantity == 0
                && owner.requiredWork == 0f
                && owner.completedWork == 0f
                && owner.effectAmount == 0f
                && owner.cooldownDays == 0
                && owner.scheduledAbsoluteDay == 0
                && (owner.sourceStackIds?.Count ?? 0) == 0
                && owner.inputMassGrams == 0L
                && string.IsNullOrEmpty(owner.commitId)
                && string.IsNullOrEmpty(owner.requestFingerprint)
                && owner.tareOutputQuantity == 0
                && owner.tareOutputMassGrams == 0L
                && owner.destroyedTareMassGrams == 0L
                && (owner.tareOutputCommitIds?.Count ?? 0) == 0
                && string.IsNullOrEmpty(owner.ecologyBeforeFingerprint)
                && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                && string.IsNullOrEmpty(owner.responsibleWorkerId)
                && string.IsNullOrEmpty(owner.responsibleWorkerDisplayName)
                && (owner.workContributions?.Count ?? 0) == 0
                && owner.pestPressureBefore == 0f
                && owner.pestPressureAfter == 0f
                && owner.diseasePressureBefore == 0f
                && owner.diseasePressureAfter == 0f
                && owner.cooldownBeforeDay == 0
                && owner.cooldownAfterDay == 0
                && owner.completedAbsoluteDay == 0
                && string.IsNullOrEmpty(owner.gameplayOutcomeId)
                && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
                && owner.terminalDisposition
                    == CropTreatmentTerminalDisposition.None
                && string.IsNullOrEmpty(owner.terminalReasonCode)
                && owner.terminalLossQuantity == 0
                && owner.terminalLossMassGrams == 0L;
            if (!empty)
                throw new InvalidOperationException(
                    "Empty crop treatment owner contains provenance.");
            return;
        }

        if (!Enum.IsDefined(typeof(CropTreatmentOrderPhase), owner.phase)
            || owner.outcomeSchemaVersion != CropTreatmentOrderSaveData.CurrentOutcomeSchemaVersion
            || owner.operationSequence != saved.nextTreatmentOperationSequence
            || !string.Equals(
                owner.operationId,
                CropTreatmentPhysicalOutbox.FormatOperationId(
                    plotId.Value,
                    owner.operationSequence),
                StringComparison.Ordinal)
            || !string.Equals(
                owner.destinationId,
                BuildTreatmentDestinationId(
                    plotId,
                    owner.operationSequence),
                StringComparison.Ordinal)
            || !treatmentDefinitions.TryResolveTreatment(
                owner.itemId,
                out _,
                out CropTreatmentPolicy policy,
                out _)
            || owner.treatmentKind != policy.Kind
            || owner.quantity != policy.QuantityPerApplication
            || !owner.requiredWork.Equals(policy.RequiredWork)
            || !owner.effectAmount.Equals(policy.EffectAmount)
            || owner.cooldownDays != policy.CooldownDays
            || !CropTreatmentPhysicalOutbox.ValidateIntent(owner))
            throw new InvalidOperationException(
                "Crop treatment intent contradicts authored policy or plot identity.");

        bool hasWorker = !string.IsNullOrWhiteSpace(owner.responsibleWorkerId)
            && string.Equals(
                owner.responsibleWorkerId,
                owner.responsibleWorkerId.Trim(),
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(
                owner.responsibleWorkerDisplayName)
            && owner.workContributions != null
            && owner.workContributions.Count > 0
            && owner.workContributions.All(value => value != null
                && !string.IsNullOrWhiteSpace(value.characterId)
                && value.characterId == value.characterId.Trim()
                && float.IsFinite(value.contributedWork)
                && value.contributedWork > 0f)
            && owner.workContributions.Select(value => value.characterId)
                .Distinct(StringComparer.Ordinal).Count()
                == owner.workContributions.Count;
        bool hasBeforeSnapshot = !string.IsNullOrWhiteSpace(
                owner.ecologyBeforeFingerprint)
            && float.IsFinite(owner.pestPressureBefore)
            && owner.pestPressureBefore is >= 0f and <= 100f
            && float.IsFinite(owner.diseasePressureBefore)
            && owner.diseasePressureBefore is >= 0f and <= 100f
            && owner.cooldownBeforeDay >= 0
            && owner.completedAbsoluteDay >= 0;
        bool hasAfterSnapshot = hasBeforeSnapshot
            && !string.IsNullOrWhiteSpace(owner.ecologyAfterFingerprint)
            && float.IsFinite(owner.pestPressureAfter)
            && owner.pestPressureAfter is >= 0f and <= 100f
            && float.IsFinite(owner.diseasePressureAfter)
            && owner.diseasePressureAfter is >= 0f and <= 100f
            && owner.cooldownAfterDay
                == owner.completedAbsoluteDay + owner.cooldownDays;
        bool hasLedgerIdentity = IsCanonicalToken(owner.gameplayOutcomeId)
            && IsCropCommandFingerprint(owner.gameplayOutcomeDigest);

        bool hasReceipt = owner.sourceStackIds != null
            && owner.sourceStackIds.Count > 0
            && owner.sourceStackIds.All(value =>
                !string.IsNullOrWhiteSpace(value)
                && string.Equals(value, value.Trim(), StringComparison.Ordinal))
            && owner.sourceStackIds.Distinct(StringComparer.Ordinal).Count()
                == owner.sourceStackIds.Count
            && owner.inputMassGrams > 0L
            && !string.IsNullOrWhiteSpace(owner.commitId)
            && !string.IsNullOrWhiteSpace(owner.requestFingerprint)
            && !string.IsNullOrWhiteSpace(owner.ecologyBeforeFingerprint);
        bool emptyReceipt = (owner.sourceStackIds?.Count ?? 0) == 0
            && owner.inputMassGrams == 0L
            && string.IsNullOrEmpty(owner.commitId)
            && string.IsNullOrEmpty(owner.requestFingerprint)
            && owner.tareOutputQuantity == 0
            && owner.tareOutputMassGrams == 0L
            && owner.destroyedTareMassGrams == 0L
            && (owner.tareOutputCommitIds?.Count ?? 0) == 0
            && string.IsNullOrEmpty(owner.ecologyBeforeFingerprint)
            && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
            && string.IsNullOrEmpty(owner.gameplayOutcomeId)
            && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
            && owner.terminalDisposition
                == CropTreatmentTerminalDisposition.None
            && string.IsNullOrEmpty(owner.terminalReasonCode)
            && owner.terminalLossQuantity == 0
            && owner.terminalLossMassGrams == 0L;
        bool preCommit = owner.phase is
            CropTreatmentOrderPhase.WaitingForDelivery
            or CropTreatmentOrderPhase.ReadyForWork
            or CropTreatmentOrderPhase.Working;
        bool valid = preCommit
            ? emptyReceipt && (owner.completedWork <= 0f || hasWorker)
                    || owner.phase == CropTreatmentOrderPhase.Working
                        && hasWorker
                        && hasBeforeSnapshot
                        && (owner.sourceStackIds?.Count ?? 0) == 0
                        && owner.inputMassGrams == 0L
                        && string.IsNullOrEmpty(owner.commitId)
                        && string.IsNullOrEmpty(owner.requestFingerprint)
            : owner.phase == CropTreatmentOrderPhase.InputCommitted
                ? hasReceipt
                    && string.IsNullOrEmpty(owner.ecologyAfterFingerprint)
                    && hasWorker && hasBeforeSnapshot
                    && owner.terminalDisposition
                        == CropTreatmentTerminalDisposition.None
                : owner.phase == CropTreatmentOrderPhase.OutcomePublished
                    ? hasReceipt
                        && !string.IsNullOrWhiteSpace(
                            owner.ecologyAfterFingerprint)
                        && hasWorker && hasAfterSnapshot
                        && string.IsNullOrEmpty(owner.gameplayOutcomeId)
                        && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
                        && owner.terminalDisposition
                            == CropTreatmentTerminalDisposition.None
                    : owner.phase == CropTreatmentOrderPhase.LedgerCommitted
                        ? hasReceipt
                            && hasWorker
                            && hasAfterSnapshot
                            && hasLedgerIdentity
                            && owner.terminalDisposition
                                == CropTreatmentTerminalDisposition.None
                    : hasReceipt && hasWorker && hasBeforeSnapshot
                        && string.IsNullOrEmpty(owner.gameplayOutcomeId)
                        && string.IsNullOrEmpty(owner.gameplayOutcomeDigest)
                        && CropTreatmentPhysicalOutbox.ValidateDestroyedPlotLoss(
                        owner,
                        out _);
        if (!valid)
            throw new InvalidOperationException(
                "Crop treatment physical provenance contradicts its phase.");
    }
}
