using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using static EnvironmentOutcomeReceiptFactorySupport;

public static class CropProductionOutcomeReceiptFactory
{
    public static CertifiedSeedCompletionOutcomeReceipt
        CreateCertifiedSeedCompletion(
            CertifiedSeedPlanExecutionReceipt source,
            string facilityDisplayName,
            string cropDisplayName,
            string outputItemDefinitionId,
            string outputItemDisplayName,
            int outputQuantity,
            long outputMassGrams,
            string outputOutcomeFingerprint,
            int absoluteDay,
            long ownerRevision)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (outputQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(outputQuantity));
        if (outputMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(outputMassGrams));
        string itemDefinitionId = RequireStable(
            outputItemDefinitionId,
            nameof(outputItemDefinitionId));
        string fingerprint = RequireStable(
            outputOutcomeFingerprint,
            nameof(outputOutcomeFingerprint));
        if (ownerRevision <= 0L)
            throw new ArgumentOutOfRangeException(nameof(ownerRevision));
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.CertifiedSeedProducer,
            new GameplayOperationId(source.ActionId),
            ownerRevision,
            0);
        var facility = new GameplayEntityId(
            EnvironmentOutcomeIds.FacilityKind,
            source.FacilityInstanceId);
        var crop = new GameplayEntityId(
            EnvironmentOutcomeIds.CropDefinitionKind,
            source.CropId);
        var item = new GameplayEntityId(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemDefinitionId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.CertifiedSeedCompleted,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            facility,
            EnvironmentOutcomeIds.FacilityRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(
                source.FacilityInstanceId,
                facilityDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            crop,
            EnvironmentOutcomeIds.CropRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(source.CropId, cropDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            item,
            EnvironmentOutcomeIds.ItemRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(
                itemDefinitionId,
                outputItemDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind,
            source.FacilityInstanceId,
            0.72f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CropDefinitionKind,
            source.CropId,
            0.68f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemDefinitionId,
            0.64f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.QuantityMetric,
            outputQuantity,
            EnvironmentOutcomeIds.CountUnit,
            item)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.MassMetric,
            outputMassGrams,
            EnvironmentOutcomeIds.GramUnit,
            item)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision,
            EnvironmentOutcomeIds.RevisionUnit,
            facility)));
        Require(builder.AddTag(EnvironmentOutcomeIds.AgricultureTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "certified-seed-plan",
            source.ActionId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(CertifiedSeedPlanExecutionReceipt))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SourceDigestFact,
            source.SourceDigest)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.CommitFact,
            source.OutputBatchCommitId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.FingerprintFact,
            fingerprint)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.CorrelationFact,
            source.ActionId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact,
            $"{facilityDisplayName}에서 {cropDisplayName} 인증 종자 "
            + $"{outputQuantity}개를 생산했다.")));
        return new CertifiedSeedCompletionOutcomeReceipt(builder.Build());
    }

    public static CropPlanGameplayOutcomeReceipt CreateCropPlan(
        CropPlanExecutionReceipt source,
        BuildingInstanceId plotId,
        string plotDisplayName,
        string cropDisplayName,
        int absoluteDay,
        long ownerRevision)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        int outputQuantity = 0;
        foreach (CropPlanExecutionOutputWitness output in source.Outputs)
            outputQuantity = checked(outputQuantity + output.Quantity);
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.CropPlanProducer,
            new GameplayOperationId(source.ActionId),
            ownerRevision,
            0);
        var plot = new GameplayEntityId(EnvironmentOutcomeIds.FacilityKind, plotId.Value);
        var crop = new GameplayEntityId(EnvironmentOutcomeIds.CropDefinitionKind, source.CropId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.CropPlanTerminal,
            absoluteDay,
            source.Succeeded ? GameplayOutcomeStatus.Succeeded : GameplayOutcomeStatus.Failed,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            plot, EnvironmentOutcomeIds.FacilityRole, GameplayParticipationKind.Direct,
            true, EnvironmentOutcomeSnapshots.Name(plotId.Value, plotDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            crop, EnvironmentOutcomeIds.CropRole, GameplayParticipationKind.Direct,
            true, EnvironmentOutcomeSnapshots.Name(source.CropId, cropDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind, plotId.Value, 0.72f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CropDefinitionKind, source.CropId, 0.68f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.InputQuantityMetric,
            source.InputQuantity,
            EnvironmentOutcomeIds.CountUnit,
            crop)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.InputMassMetric,
            source.InputMassGrams,
            EnvironmentOutcomeIds.GramUnit,
            crop)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.OutputQuantityMetric,
            outputQuantity,
            EnvironmentOutcomeIds.CountUnit,
            crop)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.OutputMassMetric,
            source.OutputMassGrams,
            EnvironmentOutcomeIds.GramUnit,
            crop)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.StatusMetric,
            (int)source.Status,
            EnvironmentOutcomeIds.EnumUnit,
            plot)));
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterBeforeMetric,
            PersistedFloat(source.WaterBefore),
            EnvironmentOutcomeIds.PointUnit,
            plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterAfterMetric,
            PersistedFloat(source.WaterAfter),
            EnvironmentOutcomeIds.PointUnit,
            plot);
        Require(builder.AddTag(EnvironmentOutcomeIds.AgricultureTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "crop-plan", source.ActionId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(CropPlanExecutionReceipt))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SourceDigestFact,
            source.SourceDigest)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.InputDigestFact,
            string.IsNullOrEmpty(source.InputVectorDigest)
                ? "not-applicable"
                : source.InputVectorDigest)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.OutputDigestFact,
            string.IsNullOrEmpty(source.OutputVectorDigest)
                ? "not-applicable"
                : source.OutputVectorDigest)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.CommitFact,
            string.IsNullOrEmpty(source.OutputBatchCommitId)
                ? "not-applicable"
                : source.OutputBatchCommitId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.FingerprintFact,
            string.IsNullOrEmpty(source.OutputOutcomeFingerprint)
                ? "not-applicable"
                : source.OutputOutcomeFingerprint)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.IndoorFact,
            source.Indoor ? "true" : "false")));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.CorrelationFact,
            source.ActionId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SowOperationFact,
            string.IsNullOrEmpty(source.SowOperationId)
                ? "not-applicable"
                : source.SowOperationId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.HarvestOperationFact,
            string.IsNullOrEmpty(source.HarvestOperationId)
                ? "not-applicable"
                : source.HarvestOperationId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SowContributorsFact,
            EncodeContributors(source.SowContributions))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.HarvestContributorsFact,
            EncodeContributors(source.HarvestContributions))));
        AddFact(ref builder, EnvironmentOutcomeIds.HarvesterFact,
            string.IsNullOrEmpty(source.HarvesterId)
                ? "not-applicable"
                : source.HarvesterId);
        AddFact(ref builder, EnvironmentOutcomeIds.EcologyBeforeFact,
            string.IsNullOrEmpty(source.EcologyBeforeFingerprint)
                ? "not-applicable"
                : source.EcologyBeforeFingerprint);
        AddFact(ref builder, EnvironmentOutcomeIds.EcologyAfterFact,
            string.IsNullOrEmpty(source.EcologyAfterFingerprint)
                ? "not-applicable"
                : source.EcologyAfterFingerprint);
        AddFact(ref builder, EnvironmentOutcomeIds.GoldenTraitFact,
            source.GoldenPrepared
                ? source.GoldenTraitDefinitionId
                : "not-applicable");
        AddFact(ref builder, EnvironmentOutcomeIds.GoldenOutcomeFact,
            source.GoldenPrepared
                ? source.GoldenOutcome.ToString()
                : "not-applicable");
        AddFact(ref builder, EnvironmentOutcomeIds.GoldenMultipliersFact,
            source.GoldenPrepared
                ? source.GoldenPrimaryMultiplier.ToString(
                    "R", System.Globalization.CultureInfo.InvariantCulture)
                    + ","
                    + source.GoldenSecondaryMultiplier.ToString(
                        "R", System.Globalization.CultureInfo.InvariantCulture)
                : "not-applicable");
        AddFact(ref builder, EnvironmentOutcomeIds.GoldenRollHashFact,
            source.GoldenPrepared
                ? source.GoldenRollHash.ToString(
                    System.Globalization.CultureInfo.InvariantCulture)
                : "not-applicable");
        AddFact(ref builder, EnvironmentOutcomeIds.IrrigationChildrenFact,
            source.IrrigationChildResultKeys.Count == 0
                ? "none"
                : string.Join(",", source.IrrigationChildResultKeys));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact,
            source.Succeeded
                ? $"{cropDisplayName} 재배를 마쳐 산출물 {outputQuantity}개를 확보했다."
                : $"{cropDisplayName} 재배가 {source.FailureReasonCode} 사유로 종료됐다.")));
        return new CropPlanGameplayOutcomeReceipt(builder.Build());
    }

    private static string EncodeContributors(
        IEnumerable<CraftContributionSaveData> source)
    {
        string encoded = string.Join(",", (source
                ?? Array.Empty<CraftContributionSaveData>())
            .Where(value => value != null && value.contributedWork > 0f)
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .Select(value => value.characterId + "="
                + value.contributedWork.ToString(
                    "R",
                    System.Globalization.CultureInfo.InvariantCulture)));
        return encoded.Length == 0 ? "none" : encoded;
    }

    public static CropIrrigationSupplyOutcomeReceipt CreateCropIrrigationSupply(
        in CropIrrigationSupplyResult source,
        string operationId,
        long ownerRevision,
        string plotDisplayName,
        string irrigatorDisplayName,
        CoreGridCell location,
        int absoluteDay,
        float waterBefore,
        float waterAfter,
        string networkId,
        float networkWaterBefore,
        float networkWaterAfter)
    {
        if (!source.Succeeded
            || !source.Assessment.PlotId.IsValid
            || !source.Assessment.IrrigatorId.IsValid
            || !float.IsFinite(source.SuppliedWaterUnits)
            || source.SuppliedWaterUnits <= 0f
            || !float.IsFinite(waterBefore)
            || !float.IsFinite(waterAfter)
            || waterBefore < 0f
            || Math.Abs(waterBefore + source.SuppliedWaterUnits - waterAfter)
                > 0.0001f
            || !float.IsFinite(networkWaterBefore)
            || !float.IsFinite(networkWaterAfter)
            || networkWaterBefore < 0f
            || Math.Abs(
                networkWaterBefore
                - source.SuppliedWaterUnits
                - networkWaterAfter) > 0.0001f)
            throw new ArgumentException(
                "A successful exact irrigation result is required.",
                nameof(source));
        string canonicalOperationId = RequireStable(operationId, nameof(operationId));
        string canonicalNetworkId = RequireStable(networkId, nameof(networkId));
        var plot = new GameplayEntityId(
            EnvironmentOutcomeIds.FacilityKind,
            source.Assessment.PlotId.Value);
        var irrigator = new GameplayEntityId(
            EnvironmentOutcomeIds.FacilityKind,
            source.Assessment.IrrigatorId.Value);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.CropIrrigationProducer,
                new GameplayOperationId(canonicalOperationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.CropIrrigationSupplied,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            plot,
            EnvironmentOutcomeIds.FacilityRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(
                source.Assessment.PlotId.Value,
                plotDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            irrigator,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(
                source.Assessment.IrrigatorId.Value,
                irrigatorDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind,
            source.Assessment.PlotId.Value,
            0.62f)));
        Require(builder.AddSubject(new GameplayOutcomeSubjectLink(
            irrigator,
            0.48f,
            NarrativeMemoryTier.Episodic,
            false,
            false,
            0)));
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterUnitsMetric,
            source.SuppliedWaterUnits, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterQualityMetric,
            (int)source.ConsumedQuality, EnvironmentOutcomeIds.EnumUnit, irrigator);
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterBeforeMetric,
            waterBefore, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterAfterMetric,
            waterAfter, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.NetworkWaterBeforeMetric,
            networkWaterBefore, EnvironmentOutcomeIds.PointUnit, irrigator);
        AddMetric(ref builder, EnvironmentOutcomeIds.NetworkWaterAfterMetric,
            networkWaterAfter, EnvironmentOutcomeIds.PointUnit, irrigator);
        Require(builder.AddTag(EnvironmentOutcomeIds.AgricultureTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.CropIrrigationProducer,
            canonicalOperationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(CropIrrigationSupplyResult));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact,
            canonicalOperationId);
        AddFact(ref builder, EnvironmentOutcomeIds.NetworkIdFact,
            canonicalNetworkId);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{irrigatorDisplayName}에서 {plotDisplayName}에 깨끗한 물 {source.SuppliedWaterUnits:0.##}단위를 공급했다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        return new CropIrrigationSupplyOutcomeReceipt(builder.Build());
    }

    public static CropSelectionCommandReceipt CreateCropSelection(
        CropSelectionCommandSaveData source,
        BuildingInstanceId plotId,
        string plotDisplayName,
        string cropDisplayName,
        CoreGridCell location)
    {
        if (source == null
            || source.phase is not (CropCommandOutcomePhase.MutationApplied
                or CropCommandOutcomePhase.LedgerCommitted))
            throw new InvalidOperationException(
                "A mutation-applied crop selection owner is required.");
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string selectedCrop = RequireStable(source.cropAfter, nameof(source.cropAfter));
        long ownerRevision = checked((long)source.operationSequence + 1L);
        var plot = new GameplayEntityId(EnvironmentOutcomeIds.FacilityKind, plotId.Value);
        var crop = new GameplayEntityId(EnvironmentOutcomeIds.CropDefinitionKind, selectedCrop);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.CropSelectionProducer,
                new GameplayOperationId(operationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.CropSelectionChanged,
            source.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            plot, EnvironmentOutcomeIds.FacilityRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(plotId.Value, plotDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            crop, EnvironmentOutcomeIds.CropRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(selectedCrop, cropDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind, plotId.Value, 0.64f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CropDefinitionKind, selectedCrop, 0.58f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision, EnvironmentOutcomeIds.RevisionUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterBeforeMetric,
            source.waterBefore, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterAfterMetric,
            source.waterAfter, EnvironmentOutcomeIds.PointUnit, plot);
        Require(builder.AddTag(EnvironmentOutcomeIds.AgricultureTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.CropSelectionProducer, operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(CropSelectionCommandSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.PreviousCropFact,
            string.IsNullOrEmpty(source.cropBefore) ? "none" : source.cropBefore);
        AddFact(ref builder, EnvironmentOutcomeIds.SelectedCropFact, selectedCrop);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{plotDisplayName}의 재배 작물을 {cropDisplayName}(으)로 지정했다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        return new CropSelectionCommandReceipt(builder.Build());
    }

    public static GoldenHarvestScheduleReceipt CreateGoldenHarvestSchedule(
        CropGoldenHarvestScheduleSaveData source,
        BuildingInstanceId plotId,
        string plotDisplayName,
        string cropDisplayName,
        CoreGridCell location)
    {
        if (source == null
            || source.phase is not (CropCommandOutcomePhase.MutationApplied
                or CropCommandOutcomePhase.LedgerCommitted))
            throw new InvalidOperationException(
                "A mutation-applied golden harvest schedule owner is required.");
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string workerId = RequireStable(source.harvesterId, nameof(source.harvesterId));
        string cropId = RequireStable(source.cropId, nameof(source.cropId));
        long ownerRevision = checked((long)source.operationSequence + 1L);
        var worker = new GameplayEntityId(EnvironmentOutcomeIds.CharacterKind, workerId);
        var plot = new GameplayEntityId(EnvironmentOutcomeIds.FacilityKind, plotId.Value);
        var crop = new GameplayEntityId(EnvironmentOutcomeIds.CropDefinitionKind, cropId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.GoldenHarvestScheduleProducer,
                new GameplayOperationId(operationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.GoldenHarvestScheduled,
            source.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            worker, EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(workerId, source.harvesterDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            plot, EnvironmentOutcomeIds.FacilityRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(plotId.Value, plotDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            crop, EnvironmentOutcomeIds.CropRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(cropId, cropDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind, workerId, 0.7f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind, plotId.Value, 0.62f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CropDefinitionKind, cropId, 0.54f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision, EnvironmentOutcomeIds.RevisionUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.AttemptBeforeMetric,
            source.attemptBefore, EnvironmentOutcomeIds.CountUnit, worker);
        AddMetric(ref builder, EnvironmentOutcomeIds.AttemptAfterMetric,
            source.attemptAfter, EnvironmentOutcomeIds.CountUnit, worker);
        Require(builder.AddTag(EnvironmentOutcomeIds.AgricultureTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.GoldenHarvestScheduleProducer, operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(CropGoldenHarvestScheduleSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.PreviousHarvesterFact,
            string.IsNullOrEmpty(source.harvesterBefore)
                ? "none"
                : source.harvesterBefore);
        AddFact(ref builder, EnvironmentOutcomeIds.HarvesterFact,
            source.harvesterAfter);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{source.harvesterDisplayName}에게 {cropDisplayName} 황금 수확을 예약했다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        return new GoldenHarvestScheduleReceipt(builder.Build());
    }

    public static CropManualWaterRefillOutcomeReceipt CreateManualWaterRefill(
        CropWaterRefillSaveData source,
        BuildingInstanceId plotId,
        string plotDisplayName,
        string itemDisplayName,
        string physicalChildResultKey,
        GameplayOutcomeId physicalChildOutcomeId,
        CoreGridCell location)
    {
        if (source == null
            || source.outcomeSchemaVersion
                != CropWaterRefillSaveData.CurrentOutcomeSchemaVersion
            || source.phase is not (CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted))
            throw new InvalidOperationException(
                "A current water-applied refill owner is required.");
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string workerId = RequireStable(
            source.responsibleWorkerId, nameof(source.responsibleWorkerId));
        string itemId = RequireStable(source.itemId, nameof(source.itemId));
        string childResultKey = RequireExactText(
            physicalChildResultKey, nameof(physicalChildResultKey));
        long ownerRevision = checked((long)source.operationSequence + 1L);
        var worker = new GameplayEntityId(EnvironmentOutcomeIds.CharacterKind, workerId);
        var plot = new GameplayEntityId(EnvironmentOutcomeIds.FacilityKind, plotId.Value);
        var item = new GameplayEntityId(EnvironmentOutcomeIds.ItemDefinitionKind, itemId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.CropManualWaterProducer,
                new GameplayOperationId(operationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.CropManualWaterRefilled,
            source.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            worker, EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(
                workerId, source.responsibleWorkerDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            plot, EnvironmentOutcomeIds.FacilityRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(plotId.Value, plotDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            item, EnvironmentOutcomeIds.ItemRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(itemId, itemDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind, workerId, 0.68f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind, plotId.Value, 0.7f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.ItemDefinitionKind, itemId, 0.5f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.InputQuantityMetric,
            source.inputQuantity, EnvironmentOutcomeIds.CountUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.InputMassMetric,
            source.inputMassGrams, EnvironmentOutcomeIds.GramUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterBeforeMetric,
            source.waterBefore, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.WaterAfterMetric,
            source.waterAfter, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision, EnvironmentOutcomeIds.RevisionUnit, plot);
        Require(builder.AddTag(EnvironmentOutcomeIds.AgricultureTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.CropManualWaterProducer, operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(CropWaterRefillSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.CommitFact, source.commitId);
        AddFact(ref builder, EnvironmentOutcomeIds.SourceStackFact,
            string.Join(",", (source.sourceStackIds ?? new List<string>())
                .OrderBy(value => value, StringComparer.Ordinal)));
        AddFact(ref builder, EnvironmentOutcomeIds.ChildResultKeyFact,
            childResultKey);
        AddFact(ref builder, EnvironmentOutcomeIds.WorkContributorsFact,
            EncodeContributors(source.workContributions));
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{source.responsibleWorkerDisplayName}이 {plotDisplayName}에 {itemDisplayName} {source.inputQuantity}개를 보충했다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        if (physicalChildOutcomeId.IsValid)
        {
            builder.SetCausation(new GameplayOutcomeCausation(
                physicalChildOutcomeId,
                new GameplayOperationId(operationId),
                "physical-water-consumption"));
        }
        return new CropManualWaterRefillOutcomeReceipt(builder.Build());
    }

    public static CropTreatmentOutcomeReceipt CreateCropTreatment(
        CropTreatmentOrderSaveData source,
        string plotId,
        string plotDisplayName,
        string itemDisplayName,
        CoreGridCell location)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source.outcomeSchemaVersion
                != CropTreatmentOrderSaveData.CurrentOutcomeSchemaVersion
            || source.phase is not (CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted))
        {
            throw new InvalidOperationException(
                "A current ecology-applied crop-treatment owner is required.");
        }
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string canonicalPlotId = RequireStable(plotId, nameof(plotId));
        string workerId = RequireStable(
            source.responsibleWorkerId,
            nameof(source.responsibleWorkerId));
        string itemId = RequireStable(source.itemId, nameof(source.itemId));
        string workerName = RequireDisplay(
            source.responsibleWorkerDisplayName,
            nameof(source.responsibleWorkerDisplayName));
        string canonicalPlotName = RequireDisplay(
            plotDisplayName,
            nameof(plotDisplayName));
        string canonicalItemName = RequireDisplay(
            itemDisplayName,
            nameof(itemDisplayName));
        long ownerRevision = checked((long)source.operationSequence + 1L);
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.CropTreatmentProducer,
            new GameplayOperationId(operationId),
            ownerRevision,
            0);
        var worker = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            workerId);
        var plot = new GameplayEntityId(
            EnvironmentOutcomeIds.FacilityKind,
            canonicalPlotId);
        var item = new GameplayEntityId(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.CropTreatmentApplied,
            source.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            worker,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(workerId, workerName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            plot,
            EnvironmentOutcomeIds.FacilityRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(canonicalPlotId, canonicalPlotName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            item,
            EnvironmentOutcomeIds.ItemRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(itemId, canonicalItemName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            workerId,
            0.56f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind,
            canonicalPlotId,
            0.74f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemId,
            0.42f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.InputQuantityMetric,
            source.quantity, EnvironmentOutcomeIds.CountUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.InputMassMetric,
            source.inputMassGrams, EnvironmentOutcomeIds.GramUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.EffectAmountMetric,
            source.effectAmount, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.PestPressureBeforeMetric,
            source.pestPressureBefore, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.PestPressureAfterMetric,
            source.pestPressureAfter, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.DiseasePressureBeforeMetric,
            source.diseasePressureBefore, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.DiseasePressureAfterMetric,
            source.diseasePressureAfter, EnvironmentOutcomeIds.PointUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.CooldownBeforeDayMetric,
            source.cooldownBeforeDay, EnvironmentOutcomeIds.CountUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.CooldownAfterDayMetric,
            source.cooldownAfterDay, EnvironmentOutcomeIds.CountUnit, plot);
        AddMetric(ref builder, EnvironmentOutcomeIds.OutputQuantityMetric,
            source.tareOutputQuantity, EnvironmentOutcomeIds.CountUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.OutputMassMetric,
            source.tareOutputMassGrams, EnvironmentOutcomeIds.GramUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision, EnvironmentOutcomeIds.RevisionUnit, plot);
        Require(builder.AddTag(EnvironmentOutcomeIds.AgricultureTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.CropTreatmentProducer,
            operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(CropTreatmentOrderSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.TreatmentKindFact,
            source.treatmentKind.ToString());
        AddFact(ref builder, EnvironmentOutcomeIds.CommitFact, source.commitId);
        AddFact(ref builder, EnvironmentOutcomeIds.SourceStackFact,
            string.Join(",", source.sourceStackIds ?? new List<string>()));
        AddFact(ref builder, EnvironmentOutcomeIds.EcologyBeforeFact,
            source.ecologyBeforeFingerprint);
        AddFact(ref builder, EnvironmentOutcomeIds.EcologyAfterFact,
            source.ecologyAfterFingerprint);
        AddFact(ref builder, EnvironmentOutcomeIds.TareCommitFact,
            source.tareOutputCommitIds != null
                && source.tareOutputCommitIds.Count > 0
                ? string.Join(",", source.tareOutputCommitIds)
                : "none");
        AddFact(ref builder, EnvironmentOutcomeIds.TreatmentContributorsFact,
            string.Join(",", (source.workContributions
                    ?? new List<CraftContributionSaveData>())
                .Where(value => value != null)
                .OrderBy(value => value.characterId, StringComparer.Ordinal)
                .Select(value => value.characterId + "="
                    + value.contributedWork.ToString(
                        "R",
                        System.Globalization.CultureInfo.InvariantCulture))));
        AddFact(ref builder, EnvironmentOutcomeIds.ItemDefinitionFact, itemId);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{workerName}이 {canonicalPlotName}에 {canonicalItemName} 처리를 완료했다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        return new CropTreatmentOutcomeReceipt(builder.Build());
    }

}
