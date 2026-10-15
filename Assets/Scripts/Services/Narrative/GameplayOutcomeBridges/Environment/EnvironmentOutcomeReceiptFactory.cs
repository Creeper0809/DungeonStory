using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;

public static class EnvironmentOutcomeReceiptFactory
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

    public static EnvironmentalFireFuelOutcomeReceipt CreateFireFuelLoss(
        in EnvironmentalFireFuelLossReceipt source,
        string itemInstanceId,
        string itemDisplayName,
        string physicalSourceDigest,
        CoreGridCell location,
        int absoluteDay,
        long ownerRevision)
    {
        if (!source.IsCommitted)
            throw new ArgumentException(
                "A committed fire fuel receipt is required.",
                nameof(source));
        string canonicalItemInstanceId = RequireStable(
            itemInstanceId,
            nameof(itemInstanceId));
        string sourceDigest = RequireStable(
            physicalSourceDigest,
            nameof(physicalSourceDigest));
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.FireFuelProducer,
            new GameplayOperationId(source.OperationId),
            ownerRevision,
            0);
        var item = new GameplayEntityId(
            EnvironmentOutcomeIds.ItemStackKind,
            canonicalItemInstanceId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.FireFuelLoss,
            absoluteDay,
            GameplayOutcomeStatus.Failed,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            item, EnvironmentOutcomeIds.ItemRole, GameplayParticipationKind.Direct,
            true, EnvironmentOutcomeSnapshots.Name(
                canonicalItemInstanceId,
                itemDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.ItemStackKind,
            canonicalItemInstanceId,
            0.84f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.QuantityMetric,
            source.Quantity,
            EnvironmentOutcomeIds.CountUnit,
            item)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.MassMetric,
            source.InputMassGrams,
            EnvironmentOutcomeIds.GramUnit,
            item)));
        AddFireCommon(
            ref builder,
            EnvironmentOutcomeIds.FireFuelProducer,
            source.OperationId,
            source.CommitId,
            sourceDigest,
            location,
            nameof(EnvironmentalFireFuelLossReceipt),
            $"{itemDisplayName} {source.Quantity}개가 불타 "
            + $"{source.InputMassGrams}g이 소실됐다.");
        return new EnvironmentalFireFuelOutcomeReceipt(builder.Build());
    }

    public static EnvironmentalFireWaterOutcomeReceipt CreateFireWaterConsumed(
        in EnvironmentalFireWaterReceipt source,
        CharacterId actorId,
        string actorDisplayName,
        string waterItemDefinitionId,
        string waterItemDisplayName,
        long waterMassGrams,
        string physicalSourceDigest,
        CoreGridCell location,
        int absoluteDay,
        long ownerRevision)
    {
        if (!source.IsCommitted)
            throw new ArgumentException(
                "A committed fire water receipt is required.",
                nameof(source));
        string itemDefinitionId = RequireStable(
            waterItemDefinitionId,
            nameof(waterItemDefinitionId));
        string sourceDigest = RequireStable(
            physicalSourceDigest,
            nameof(physicalSourceDigest));
        if (waterMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(waterMassGrams));
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.FireWaterProducer,
            new GameplayOperationId(source.OperationId),
            ownerRevision,
            0);
        var actor = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            actorId.Value);
        var item = new GameplayEntityId(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemDefinitionId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.FireWaterConsumed,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            actor, EnvironmentOutcomeIds.ActorRole, GameplayParticipationKind.Direct,
            true, EnvironmentOutcomeSnapshots.Name(actorId.Value, actorDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            item, EnvironmentOutcomeIds.ItemRole, GameplayParticipationKind.Direct,
            true, EnvironmentOutcomeSnapshots.Name(
                itemDefinitionId,
                waterItemDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            actorId.Value,
            0.74f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemDefinitionId,
            0.68f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.QuantityMetric,
            source.Quantity,
            EnvironmentOutcomeIds.CountUnit,
            item)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.MassMetric,
            waterMassGrams,
            EnvironmentOutcomeIds.GramUnit,
            item)));
        AddFireCommon(
            ref builder,
            EnvironmentOutcomeIds.FireWaterProducer,
            source.OperationId,
            source.CommitId,
            sourceDigest,
            location,
            nameof(EnvironmentalFireWaterReceipt),
            $"{waterItemDisplayName} {source.Quantity}개를 화재 진압에 사용했다.");
        return new EnvironmentalFireWaterOutcomeReceipt(builder.Build());
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

    public static SpeciesIncidentOutcomeReceipt CreateSpeciesIncident(
        GameplayResultKey resultKey,
        long ownerRevision,
        CharacterId characterId,
        string characterDisplayName,
        CharacterSpeciesId speciesId,
        string speciesDisplayName,
        string incidentId,
        string summary,
        CoreGridCell location,
        int absoluteDay)
    {
        var character = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value);
        var species = new GameplayEntityId(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.SpeciesIncidentTriggered,
            absoluteDay,
            GameplayOutcomeStatus.Failed,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            character,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(characterId.Value, characterDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            species,
            EnvironmentOutcomeIds.SpeciesRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(speciesId.Value, speciesDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value,
            0.82f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value,
            0.64f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision,
            EnvironmentOutcomeIds.RevisionUnit,
            character)));
        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "species-incident", incidentId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(SpeciesIncidentTriggeredEvent))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.IncidentFact,
            incidentId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact,
            summary)));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        return new SpeciesIncidentOutcomeReceipt(builder.Build());
    }

    public static GolemUpkeepOutcomeReceipt CreateGolemUpkeep(
        GameplayResultKey resultKey,
        long ownerRevision,
        CharacterId characterId,
        string characterDisplayName,
        CharacterSpeciesId speciesId,
        string speciesDisplayName,
        string episodeKind,
        string stateBefore,
        string stateAfter,
        string physicalChild,
        string workChild,
        string bodyChild,
        string summary,
        CoreGridCell location,
        int absoluteDay)
    {
        if (!resultKey.IsValid || ownerRevision <= 0L)
            throw new ArgumentException("A valid Golem episode identity is required.");
        string kind = RequireStable(episodeKind, nameof(episodeKind));
        string before = RequireFactValue(stateBefore, nameof(stateBefore));
        string after = RequireFactValue(stateAfter, nameof(stateAfter));
        string physical = RequireFactValue(physicalChild, nameof(physicalChild));
        string work = RequireFactValue(workChild, nameof(workChild));
        string body = RequireFactValue(bodyChild, nameof(bodyChild));
        string exactSummary = RequireFactValue(summary, nameof(summary));
        var character = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value);
        var species = new GameplayEntityId(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.GolemUpkeepEpisode,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            character,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(characterId.Value, characterDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            species,
            EnvironmentOutcomeIds.SpeciesRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(speciesId.Value, speciesDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value,
            0.90f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value,
            0.72f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision,
            EnvironmentOutcomeIds.RevisionUnit,
            character)));
        Require(builder.AddTag(EnvironmentOutcomeIds.SpeciesTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "golem-upkeep", resultKey.OperationId.Value)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(GolemUpkeepOutcomeReceipt))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.EpisodeKindFact, kind)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.StateBeforeFact, before)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.StateAfterFact, after)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.PhysicalChildFact, physical)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.WorkChildFact, work)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.BodyChildFact, body)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact, exactSummary)));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        return new GolemUpkeepOutcomeReceipt(builder.Build());
    }

    public static HarpyGaleRelocationOutcomeReceipt
        CreateHarpyGaleRelocation(
            GameplayResultKey resultKey,
            long ownerRevision,
            CharacterId characterId,
            string characterDisplayName,
            CharacterSpeciesId speciesId,
            string speciesDisplayName,
            in PreparedPhysicalItemRelocationPreview physical,
            int absoluteDay)
    {
        if (!physical.IsValid || ownerRevision <= 0L)
            throw new ArgumentException(
                "A valid prepared relocation preview is required.",
                nameof(physical));
        PhysicalItemRelocationReceipt relocation = physical.Receipt;
        var character = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value);
        var species = new GameplayEntityId(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value);
        GameplayEntityId item = physical.ItemInstanceId.Length > 0
            ? new GameplayEntityId(
                EnvironmentOutcomeIds.ItemInstanceKind,
                physical.ItemInstanceId)
            : new GameplayEntityId(
                EnvironmentOutcomeIds.ItemStackKind,
                relocation.SourceStackId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.HarpyGaleRelocation,
            absoluteDay,
            GameplayOutcomeStatus.Failed,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            character,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(
                characterId.Value,
                characterDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            species,
            EnvironmentOutcomeIds.SpeciesRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(speciesId.Value, speciesDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            item,
            EnvironmentOutcomeIds.ItemRole,
            GameplayParticipationKind.Direct,
            true,
            PhysicalItemFactDisplayName.Snapshot(relocation.ItemId, physical.DisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value,
            0.82f)));
        Require(builder.AddSubject(new GameplayOutcomeSubjectLink(
            item,
            0.62f,
            NarrativeMemoryTier.Recent,
            false,
            false,
            0)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId.Value,
            0.64f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision, EnvironmentOutcomeIds.RevisionUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.QuantityMetric,
            relocation.Quantity, EnvironmentOutcomeIds.CountUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.MassMetric,
            relocation.MassGrams, EnvironmentOutcomeIds.GramUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.SourceXMetric,
            relocation.SourcePosition.x, EnvironmentOutcomeIds.CellUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.SourceYMetric,
            relocation.SourcePosition.y, EnvironmentOutcomeIds.CellUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.DestinationXMetric,
            relocation.DestinationPosition.x, EnvironmentOutcomeIds.CellUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.DestinationYMetric,
            relocation.DestinationPosition.y, EnvironmentOutcomeIds.CellUnit, item);
        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "physical-relocation",
            relocation.OperationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(PhysicalItemRelocationReceipt));
        AddFact(ref builder, EnvironmentOutcomeIds.IncidentFact,
            CharacterSpeciesIncidentIds.HarpyGaleCommotion);
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact,
            relocation.OperationId);
        AddFact(ref builder, EnvironmentOutcomeIds.SourceStackFact,
            relocation.SourceStackId);
        AddFact(ref builder, EnvironmentOutcomeIds.DestinationStackFact,
            relocation.DestinationStackId);
        AddFact(ref builder, EnvironmentOutcomeIds.ItemDefinitionFact,
            relocation.ItemId);
        AddFact(ref builder, EnvironmentOutcomeIds.ItemInstanceFact,
            physical.ItemInstanceId.Length > 0 ? physical.ItemInstanceId : "none");
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{physical.DisplayName} {relocation.Quantity}개가 돌풍에 "
            + $"({relocation.SourcePosition.x},{relocation.SourcePosition.y})에서 "
            + $"({relocation.DestinationPosition.x},{relocation.DestinationPosition.y})로 흩어졌다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon",
            string.Empty,
            relocation.DestinationPosition.x,
            relocation.DestinationPosition.y));
        return new HarpyGaleRelocationOutcomeReceipt(builder.Build());
    }

    public static RoomConditionOutcomeReceipt CreateRoomCondition(
        long sequence,
        CharacterId observerId,
        string observerDisplayName,
        BuildingInstanceId facilityId,
        string facilityDisplayName,
        string roomId,
        string roomDisplayName,
        float previousCleanliness,
        float currentCleanliness,
        CoreGridCell location,
        int absoluteDay)
    {
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.RoomConditionProducer,
            new GameplayOperationId("room-condition:" + sequence),
            sequence,
            0);
        var observer = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            observerId.Value);
        var facility = new GameplayEntityId(
            EnvironmentOutcomeIds.FacilityKind,
            facilityId.Value);
        var room = new GameplayEntityId(EnvironmentOutcomeIds.RoomKind, roomId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.RoomConditionChanged,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            sequence);
        AddRoomParticipants(
            ref builder,
            observer,
            observerId.Value,
            observerDisplayName,
            facility,
            facilityId.Value,
            facilityDisplayName,
            room,
            roomId,
            roomDisplayName);
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            observerId.Value,
            0.55f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind,
            facilityId.Value,
            0.5f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.RoomKind,
            roomId,
            0.45f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.PreviousCleanlinessMetric,
            previousCleanliness,
            EnvironmentOutcomeIds.PointUnit,
            room)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.CurrentCleanlinessMetric,
            currentCleanliness,
            EnvironmentOutcomeIds.PointUnit,
            room)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.SequenceMetric,
            sequence,
            EnvironmentOutcomeIds.RevisionUnit,
            room)));
        AddRoomCommon(
            ref builder,
            facilityId,
            location,
            nameof(RoomConditionChangedEvent),
            $"{roomDisplayName} 청결도: {previousCleanliness:0.##} → {currentCleanliness:0.##}.");
        return new RoomConditionOutcomeReceipt(builder.Build());
    }

    public static RoomEnvironmentExperienceOutcomeReceipt CreateRoomExperience(
        long sequence,
        CharacterId actorId,
        string actorDisplayName,
        BuildingInstanceId facilityId,
        string facilityDisplayName,
        string roomId,
        string roomDisplayName,
        RoomExperienceActivity activity,
        WorkTypeId workTypeId,
        float impressionMood,
        float cleanlinessMood,
        float durationSeconds,
        CoreGridCell location,
        int absoluteDay)
    {
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.RoomExperienceProducer,
            new GameplayOperationId("room-experience:" + sequence),
            sequence,
            0);
        var actor = new GameplayEntityId(EnvironmentOutcomeIds.CharacterKind, actorId.Value);
        var facility = new GameplayEntityId(EnvironmentOutcomeIds.FacilityKind, facilityId.Value);
        var room = new GameplayEntityId(EnvironmentOutcomeIds.RoomKind, roomId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.RoomExperienceApplied,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            sequence);
        AddRoomParticipants(
            ref builder,
            actor,
            actorId.Value,
            actorDisplayName,
            facility,
            facilityId.Value,
            facilityDisplayName,
            room,
            roomId,
            roomDisplayName);
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            actorId.Value,
            Math.Clamp(0.45f + Math.Abs(impressionMood + cleanlinessMood) / 20f, 0.45f, 0.9f))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind,
            facilityId.Value,
            0.5f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.RoomKind,
            roomId,
            0.4f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.ImpressionMoodMetric,
            impressionMood,
            EnvironmentOutcomeIds.PointUnit,
            actor)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.CleanlinessMoodMetric,
            cleanlinessMood,
            EnvironmentOutcomeIds.PointUnit,
            actor)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.DurationMetric,
            durationSeconds,
            EnvironmentOutcomeIds.SecondUnit,
            actor)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.SequenceMetric,
            sequence,
            EnvironmentOutcomeIds.RevisionUnit,
            room)));
        AddRoomCommon(
            ref builder,
            facilityId,
            location,
            nameof(RoomEnvironmentExperienceEvent),
            $"{roomDisplayName}에서 인상 기분 {impressionMood:0.##}, 청결 기분 {cleanlinessMood:0.##}가 적용됐다.");
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ActivityFact,
            activity.ToString())));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.WorkTypeFact,
            workTypeId.IsValid ? workTypeId.Value : "none")));
        return new RoomEnvironmentExperienceOutcomeReceipt(builder.Build());
    }

    public static ProcessAccidentOutcomeReceipt CreateProcessAccident(
        string workOperationId,
        CharacterId workerId,
        string workerDisplayName,
        BuildingInstanceId facilityId,
        string facilityDisplayName,
        WorkTypeId workTypeId,
        string anatomyNodeId,
        float appliedDamage,
        string observedCause,
        CoreGridCell location,
        int absoluteDay)
    {
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.ProcessAccidentProducer,
            new GameplayOperationId(workOperationId),
            0L,
            0);
        var worker = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            workerId.Value);
        var facility = new GameplayEntityId(
            EnvironmentOutcomeIds.FacilityKind,
            facilityId.Value);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.ProcessAccident,
            absoluteDay,
            GameplayOutcomeStatus.Failed,
            0L);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            worker,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(workerId.Value, workerDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            facility,
            EnvironmentOutcomeIds.FacilityRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(facilityId.Value, facilityDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            workerId.Value,
            Math.Clamp(0.65f + appliedDamage / 20f, 0.65f, 0.98f))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind,
            facilityId.Value,
            0.45f)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.DamageMetric,
            appliedDamage,
            EnvironmentOutcomeIds.PointUnit,
            worker)));
        Require(builder.AddTag(EnvironmentOutcomeIds.DisasterTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "work-operation",
            workOperationId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(ProcessAccidentFireReceipt))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.WorkTypeFact,
            workTypeId.Value)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.AnatomyNodeFact,
            anatomyNodeId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReasonFact,
            observedCause)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact,
            $"{facilityDisplayName}에서 {workTypeId.Value} 작업 중 {anatomyNodeId}에 {appliedDamage:0.##} 피해를 입었다.")));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon",
            string.Empty,
            location.X,
            location.Y));
        return new ProcessAccidentOutcomeReceipt(builder.Build());
    }

    public static PopulationDiseaseExposureOutcomeReceipt CreateDiseaseExposure(
        CharacterId characterId,
        string characterDisplayName,
        DiseaseDefinition disease,
        DiseaseTransmissionRoute route,
        float exposureHours,
        float environmentCoefficient,
        int absoluteDay,
        long ownerRevision,
        string sourceKind,
        string sourceId)
    {
        string operationId = "population-exposure:"
            + characterId.Value + ":" + ownerRevision;
        var resultKey = new GameplayResultKey(
            EnvironmentOutcomeIds.DiseaseExposureProducer,
            new GameplayOperationId(operationId),
            ownerRevision,
            0);
        var character = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value);
        var diseaseEntity = new GameplayEntityId(
            EnvironmentOutcomeIds.DiseaseDefinitionKind,
            disease.Id);
        var builder = new EnvironmentOutcomePayloadBuilder(
            resultKey,
            EnvironmentOutcomeIds.DiseaseRouteExposure,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            character,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(characterId.Value, characterDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            diseaseEntity,
            EnvironmentOutcomeIds.DiseaseRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(disease.Id, disease.DisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            characterId.Value,
            Math.Clamp(0.45f + exposureHours / 48f, 0.45f, 0.9f))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.DiseaseDefinitionKind,
            disease.Id,
            Math.Clamp(0.4f + exposureHours / 72f, 0.4f, 0.75f))));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.ExposureHoursMetric,
            exposureHours,
            EnvironmentOutcomeIds.HourUnit,
            diseaseEntity)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.EnvironmentCoefficientMetric,
            environmentCoefficient,
            EnvironmentOutcomeIds.RatioUnit,
            diseaseEntity)));
        Require(builder.AddMetric(new GameplayOutcomeMetric(
            EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision,
            EnvironmentOutcomeIds.RevisionUnit,
            character)));
        Require(builder.AddTag(EnvironmentOutcomeIds.DiseaseTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            RequireStable(sourceKind, nameof(sourceKind)),
            RequireStable(sourceId, nameof(sourceId)))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(PopulationDiseaseRouteExposureEvent))));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.RouteFact,
            route.ToString())));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.DetailFact,
            sourceId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact,
            $"{disease.DisplayName}에 {exposureHours:0.##}시간 노출됐다.")));
        return new PopulationDiseaseExposureOutcomeReceipt(builder.Build());
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

    public static WildlifeFoodRaidOutcomeReceipt CreateWildlifeFoodRaid(
        WildlifeFoodRaidFactData source,
        string physicalChildResultKey,
        GameplayOutcomeId physicalChildOutcomeId)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        source.Validate();
        if (!physicalChildOutcomeId.IsValid)
            throw new ArgumentException(
                "A valid physical child outcome is required.",
                nameof(physicalChildOutcomeId));

        string operationId = RequireStable(
            source.dispositionOperationId,
            nameof(source.dispositionOperationId));
        string raidId = RequireStable(source.raidId, nameof(source.raidId));
        string wildlifeId = RequireStable(
            source.wildlifeId,
            nameof(source.wildlifeId));
        string speciesId = RequireStable(
            source.wildlifeSpeciesId,
            nameof(source.wildlifeSpeciesId));
        string stackId = RequireStable(
            source.targetStackId,
            nameof(source.targetStackId));
        string itemId = RequireStable(
            source.targetItemId,
            nameof(source.targetItemId));
        string wildlifeName = RequireDisplay(
            source.wildlifeDisplayName,
            nameof(source.wildlifeDisplayName));
        string itemName = RequireDisplay(
            source.targetItemDisplayName,
            nameof(source.targetItemDisplayName));
        string childResultKey = physicalChildResultKey?.Trim() ?? string.Empty;
        if (childResultKey.Length == 0
            || !string.Equals(
                childResultKey,
                physicalChildResultKey,
                StringComparison.Ordinal))
            throw new ArgumentException(
                "A physical child result key is required.",
                nameof(physicalChildResultKey));
        long ownerRevision = source.dispositionOwnerRevision;

        var wildlife = new GameplayEntityId(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId);
        var species = new GameplayEntityId(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId);
        var stack = new GameplayEntityId(
            EnvironmentOutcomeIds.ItemStackKind,
            stackId);
        var item = new GameplayEntityId(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.WildlifeFoodRaidProducer,
                new GameplayOperationId(operationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.WildlifeFoodRaidCompleted,
            source.completedAbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);

        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            wildlife,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(wildlifeId, wildlifeName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            species,
            EnvironmentOutcomeIds.SpeciesRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(speciesId, wildlifeName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            stack,
            EnvironmentOutcomeIds.TargetRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(stackId, itemName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            item,
            EnvironmentOutcomeIds.ItemRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(itemId, itemName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId,
            0.86f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.SpeciesDefinitionKind,
            speciesId,
            0.48f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.ItemStackKind,
            stackId,
            0.54f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.ItemDefinitionKind,
            itemId,
            0.62f)));

        AddMetric(ref builder, EnvironmentOutcomeIds.RequestedQuantityMetric,
            source.requestedQuantity, EnvironmentOutcomeIds.CountUnit, stack);
        AddMetric(ref builder, EnvironmentOutcomeIds.StolenQuantityMetric,
            source.stolenQuantity, EnvironmentOutcomeIds.CountUnit, stack);
        AddMetric(ref builder, EnvironmentOutcomeIds.MassMetric,
            source.dispositionInputMassGrams, EnvironmentOutcomeIds.GramUnit, item);
        AddMetric(ref builder, EnvironmentOutcomeIds.SourceXMetric,
            source.startX, EnvironmentOutcomeIds.CellUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.SourceYMetric,
            source.startY, EnvironmentOutcomeIds.CellUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.DestinationXMetric,
            source.endX, EnvironmentOutcomeIds.CellUnit, stack);
        AddMetric(ref builder, EnvironmentOutcomeIds.DestinationYMetric,
            source.endY, EnvironmentOutcomeIds.CellUnit, stack);
        AddMetric(ref builder, EnvironmentOutcomeIds.StartedTimeMetric,
            source.startedGameTime, EnvironmentOutcomeIds.SecondUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.CompletedTimeMetric,
            source.completedGameTime, EnvironmentOutcomeIds.SecondUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.StartedDayMetric,
            source.startedAbsoluteDay, EnvironmentOutcomeIds.CountUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.CompletedDayMetric,
            source.completedAbsoluteDay, EnvironmentOutcomeIds.CountUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            ownerRevision, EnvironmentOutcomeIds.RevisionUnit, wildlife);

        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.WildlifeFoodRaidProducer,
            operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(WildlifeFoodRaidOrderSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.RaidIdFact, raidId);
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.CommitFact,
            source.dispositionCommitId);
        AddFact(ref builder, EnvironmentOutcomeIds.SourceStackFact, stackId);
        AddFact(ref builder, EnvironmentOutcomeIds.ItemDefinitionFact, itemId);
        AddFact(ref builder, EnvironmentOutcomeIds.TerminalStateFact,
            WildlifeFoodRaidOrderState.Stolen.ToString());
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact,
            RequireDisplay(source.outcomeReason, nameof(source.outcomeReason)));
        AddFact(ref builder, EnvironmentOutcomeIds.ChildResultKeyFact,
            childResultKey);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{wildlifeName}이 {itemName} {source.stolenQuantity}개를 훔쳤다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, source.endX, source.endY));
        builder.SetCausation(new GameplayOutcomeCausation(
            physicalChildOutcomeId,
            new GameplayOperationId(operationId),
            "physical-theft-consumption"));
        return new WildlifeFoodRaidOutcomeReceipt(builder.Build());
    }

    public static EnvironmentalFireIgnitionOutcomeReceipt CreateFireIgnition(
        EnvironmentalFireIgnitionRequest request,
        in EnvironmentalFireIgnitionResult source,
        string targetDisplayName,
        CoreGridCell location,
        int absoluteDay,
        long ownerRevision,
        float appliedIntensity,
        bool hasLocation = true)
    {
        if (request?.IsValid != true
            || source.Disposition is EnvironmentalFireIgnitionDisposition.InvalidRequest
                or EnvironmentalFireIgnitionDisposition.CauseConflict)
            throw new ArgumentException(
                "A processed canonical ignition result is required.", nameof(source));
        var target = new GameplayEntityId(
            EnvironmentOutcomeIds.FacilityKind,
            request.Target.TargetId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.FireIgnitionProducer,
                new GameplayOperationId(request.CauseId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.FireIgnition,
            absoluteDay,
            source.Created
                ? GameplayOutcomeStatus.Succeeded
                : GameplayOutcomeStatus.Failed,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            target,
            EnvironmentOutcomeIds.FacilityRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(
                request.Target.TargetId,
                targetDisplayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind,
            request.Target.TargetId,
            source.Created ? 0.9f : 0.56f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.StatusMetric,
            (int)source.Disposition, EnvironmentOutcomeIds.EnumUnit, target);
        AddMetric(ref builder, EnvironmentOutcomeIds.IntensityAfterMetric,
            source.Created ? appliedIntensity : 0d,
            EnvironmentOutcomeIds.RatioUnit, target);
        Require(builder.AddTag(EnvironmentOutcomeIds.DisasterTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            request.ProducerId,
            request.EvidenceId)));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "fire-ignition-request", NarrativeInferenceHash.ComputeSha256Utf8(request.Fingerprint))));
        if (!string.IsNullOrEmpty(source.FireId))
            Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
                "fire-ignition-id", source.FireId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(EnvironmentalFireIgnitionResult));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact,
            request.CauseId);
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact,
            source.Disposition.ToString());
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            source.Created
                ? $"{targetDisplayName}에서 화재가 발생했다."
                : $"{targetDisplayName}의 화재 발화 판정이 {source.Disposition}로 끝났다.");
        if (hasLocation)
            builder.SetLocation(new GameplayLocationReference(
                "dungeon", string.Empty, location.X, location.Y));
        return new EnvironmentalFireIgnitionOutcomeReceipt(builder.Build());
    }

    public static EnvironmentalFireSuppressionOutcomeReceipt CreateFireSuppression(
        EnvironmentalFireSuppressionCommand command,
        in EnvironmentalFireSuppressionResult source,
        string workerDisplayName,
        CoreGridCell location,
        int absoluteDay,
        long ownerRevision,
        string targetId)
    {
        if (command?.IsValid != true
            || !source.Applied
            || !float.IsFinite(source.IntensityBefore)
            || !float.IsFinite(source.IntensityAfter))
            throw new ArgumentException(
                "An applied canonical suppression result is required.", nameof(source));
        var worker = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            command.WorkerId);
        _ = new GameplayEntityId(EnvironmentOutcomeIds.FireKind, targetId);
        var fire = new GameplayEntityId(
            EnvironmentOutcomeIds.FireKind,
            command.FireId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.FireSuppressionProducer,
                new GameplayOperationId(command.OperationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.FireSuppression,
            absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            worker, EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(command.WorkerId, workerDisplayName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            fire, EnvironmentOutcomeIds.FireRole,
            GameplayParticipationKind.Direct, true,
            EnvironmentOutcomeSnapshots.Name(command.FireId, "화재"))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind, command.WorkerId, 0.78f)));
        Require(builder.AddSubject(new GameplayOutcomeSubjectLink(
            fire,
            0.82f,
            NarrativeMemoryTier.Episodic,
            false,
            false,
            0)));
        AddMetric(ref builder, EnvironmentOutcomeIds.IntensityBeforeMetric,
            source.IntensityBefore, EnvironmentOutcomeIds.RatioUnit, fire);
        AddMetric(ref builder, EnvironmentOutcomeIds.IntensityAfterMetric,
            source.IntensityAfter, EnvironmentOutcomeIds.RatioUnit, fire);
        AddMetric(ref builder, EnvironmentOutcomeIds.QuantityMetric,
            source.WaterConsumed, EnvironmentOutcomeIds.CountUnit, fire);
        AddMetric(ref builder, EnvironmentOutcomeIds.StatusMetric,
            (int)source.Disposition, EnvironmentOutcomeIds.EnumUnit, fire);
        Require(builder.AddTag(EnvironmentOutcomeIds.DisasterTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.FireSuppressionProducer,
            command.OperationId)));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "fire-suppression-request", NarrativeInferenceHash.ComputeSha256Utf8(command.Fingerprint))));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(EnvironmentalFireSuppressionResult));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact,
            command.OperationId);
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact,
            source.Disposition.ToString());
        AddFact(ref builder, new GameplayOutcomeFactId("fire-completion-target"), targetId);
        AddFact(ref builder, new GameplayOutcomeFactId("fire-completion-origin"),
            WorkCompletionIdentityOutcomeIds.StableOrigin(command.CommandOrigin));
        AddFact(ref builder, new GameplayOutcomeFactId("fire-completion-work"),
            command.ApprovedWork.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        AddFact(ref builder, new GameplayOutcomeFactId("fire-completion-required"),
            command.Mode == EnvironmentalFireSuppressionMode.Water || source.IntensityAfter <= 0.0001f
                ? "true" : "false");
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            source.IntensityAfter <= 0.0001f
                ? $"{workerDisplayName}이 화재를 완전히 진압했다."
                : $"{workerDisplayName}이 화재 강도를 {source.IntensityBefore:0.###}에서 {source.IntensityAfter:0.###}로 낮췄다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
        return new EnvironmentalFireSuppressionOutcomeReceipt(builder.Build());
    }

    public static EnvironmentalFireDamageOutcomeReceipt CreateFireDamage(
        EnvironmentalFireDamageCommand command,
        in EnvironmentalFireDamageResult source,
        string targetDisplayName,
        int absoluteDay,
        long ownerRevision)
    {
        if (string.IsNullOrWhiteSpace(command.OperationId)
            || string.IsNullOrWhiteSpace(command.FireId)
            || !command.Target.IsValid
            || !source.Committed
            || !float.IsFinite(source.AppliedDamage)
            || source.AppliedDamage <= 0f
            || !float.IsFinite(command.Intensity)
            || command.Intensity <= 0f
            || command.Intensity > 1f
            || string.IsNullOrWhiteSpace(targetDisplayName)
            || ownerRevision <= 0L)
        {
            throw new ArgumentException(
                "A committed canonical fire-damage result is required.",
                nameof(source));
        }

        GameplayEntityKindId targetKind = command.Target.Kind ==
                EnvironmentalFireTargetKind.Character
            ? EnvironmentOutcomeIds.CharacterKind
            : EnvironmentOutcomeIds.FacilityKind;
        var fire = new GameplayEntityId(
            EnvironmentOutcomeIds.FireKind,
            command.FireId);
        var target = new GameplayEntityId(
            targetKind,
            command.Target.TargetId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.FireDamageProducer,
                new GameplayOperationId(command.OperationId),
                ownerRevision,
                0),
            EnvironmentOutcomeIds.FireDamage,
            absoluteDay,
            GameplayOutcomeStatus.Failed,
            ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            fire,
            EnvironmentOutcomeIds.FireRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(command.FireId, "화재"))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            target,
            EnvironmentOutcomeIds.TargetRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(
                command.Target.TargetId,
                targetDisplayName))));
        Require(builder.AddSubject(new GameplayOutcomeSubjectLink(
            fire,
            0.76f,
            NarrativeMemoryTier.Episodic,
            false,
            false,
            0)));
        Require(builder.AddSubject(new GameplayOutcomeSubjectLink(
            target,
            source.TargetRemainsCombustible ? 0.84f : 0.94f,
            source.TargetRemainsCombustible
                ? NarrativeMemoryTier.Episodic
                : NarrativeMemoryTier.Core,
            false,
            false,
            0)));
        AddMetric(ref builder, EnvironmentOutcomeIds.DamageMetric,
            source.AppliedDamage, EnvironmentOutcomeIds.PointUnit, target);
        AddMetric(ref builder, EnvironmentOutcomeIds.IntensityBeforeMetric,
            command.Intensity, EnvironmentOutcomeIds.RatioUnit, fire);
        AddMetric(ref builder, EnvironmentOutcomeIds.StatusMetric,
            source.TargetRemainsCombustible ? 1d : 0d,
            EnvironmentOutcomeIds.EnumUnit,
            target);
        Require(builder.AddTag(EnvironmentOutcomeIds.DisasterTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.FireDamageProducer,
            command.OperationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(EnvironmentalFireDamageResult));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact,
            command.OperationId);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            source.TargetRemainsCombustible
                ? $"{targetDisplayName}이 화재로 {source.AppliedDamage:0.###} 피해를 입었다."
                : $"{targetDisplayName}이 화재 피해로 파괴되거나 쓰러졌다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, command.Position.x, command.Position.y));
        return new EnvironmentalFireDamageOutcomeReceipt(builder.Build());
    }

    public static EnvironmentalControlCommandReceipt CreateEnvironmentalControl(
        EnvironmentalControlOutcomeSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string buildingId = RequireStable(
            source.buildingInstanceId,
            nameof(source.buildingInstanceId));
        string displayName = RequireDisplay(
            source.buildingDisplayName,
            nameof(source.buildingDisplayName));
        string commandSource = RequireStable(
            source.commandSource,
            nameof(source.commandSource));
        var facility = new GameplayEntityId(
            EnvironmentOutcomeIds.FacilityKind,
            buildingId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.EnvironmentalControlProducer,
                new GameplayOperationId(operationId),
                source.ownerRevision,
                0),
            EnvironmentOutcomeIds.EnvironmentalControlChanged,
            source.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            source.ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            facility,
            EnvironmentOutcomeIds.FacilityRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(buildingId, displayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.FacilityKind,
            buildingId,
            0.62f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.RequestedTargetMetric,
            PersistedFloat(source.requestedTargetTemperatureC),
            EnvironmentOutcomeIds.PointUnit, facility);
        AddMetric(ref builder, EnvironmentOutcomeIds.AppliedTargetMetric,
            PersistedFloat(source.appliedTargetTemperatureC),
            EnvironmentOutcomeIds.PointUnit, facility);
        AddMetric(ref builder, EnvironmentOutcomeIds.MinimumTargetMetric,
            PersistedFloat(source.minimumTargetTemperatureC),
            EnvironmentOutcomeIds.PointUnit, facility);
        AddMetric(ref builder, EnvironmentOutcomeIds.MaximumTargetMetric,
            PersistedFloat(source.maximumTargetTemperatureC),
            EnvironmentOutcomeIds.PointUnit, facility);
        AddMetric(ref builder, EnvironmentOutcomeIds.TargetBeforeMetric,
            PersistedFloat(source.targetBeforeTemperatureC),
            EnvironmentOutcomeIds.PointUnit, facility);
        AddMetric(ref builder, EnvironmentOutcomeIds.TargetAfterMetric,
            PersistedFloat(source.targetAfterTemperatureC),
            EnvironmentOutcomeIds.PointUnit, facility);
        Require(builder.AddTag(EnvironmentOutcomeIds.EnvironmentTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.EnvironmentalControlProducer,
            operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(EnvironmentalControlOutcomeSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.CommandSourceFact, commandSource);
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact, "target-temperature-changed");
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            $"{displayName}의 목표 온도를 {source.targetBeforeTemperatureC:0.###}도에서 "
            + $"{source.targetAfterTemperatureC:0.###}도로 조정했다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, source.x, source.y));
        return new EnvironmentalControlCommandReceipt(builder.Build());
    }

    public static EnvironmentalExposureThresholdReceipt
        CreateEnvironmentalExposureThreshold(
            EnvironmentalExposureOutcomeSaveData source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string characterId = RequireStable(source.characterId, nameof(source.characterId));
        string displayName = RequireDisplay(
            source.characterDisplayName,
            nameof(source.characterDisplayName));
        string eventKind = RequireStable(source.eventKind, nameof(source.eventKind));
        string reasonCode = RequireExactText(source.reasonCode, nameof(source.reasonCode));
        var character = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            characterId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.EnvironmentalExposureProducer,
                new GameplayOperationId(operationId),
                source.ownerRevision,
                0),
            EnvironmentOutcomeIds.EnvironmentalExposureThreshold,
            source.absoluteDay,
            source.damage > 0f
                ? GameplayOutcomeStatus.Failed
                : GameplayOutcomeStatus.Succeeded,
            source.ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            character,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(characterId, displayName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            characterId,
            source.damage > 0f ? 0.88f : 0.72f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.TemperatureMetric,
            PersistedFloat(source.temperatureC), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.AirQualityMetric,
            PersistedFloat(source.airQuality), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.LightLevelMetric,
            PersistedFloat(source.lightLevel), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.ColdBeforeMetric,
            PersistedFloat(source.coldBefore), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.ColdAfterMetric,
            PersistedFloat(source.coldAfter), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.HeatBeforeMetric,
            PersistedFloat(source.heatBefore), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.HeatAfterMetric,
            PersistedFloat(source.heatAfter), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.AirborneBeforeMetric,
            PersistedFloat(source.airborneBefore), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.AirborneAfterMetric,
            PersistedFloat(source.airborneAfter), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.BandBeforeMetric,
            (int)source.bandBefore, EnvironmentOutcomeIds.EnumUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.BandAfterMetric,
            (int)source.bandAfter, EnvironmentOutcomeIds.EnumUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.DamageMetric,
            PersistedFloat(source.damage), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.HealthBeforeMetric,
            PersistedFloat(source.healthBefore), EnvironmentOutcomeIds.PointUnit, character);
        AddMetric(ref builder, EnvironmentOutcomeIds.HealthAfterMetric,
            PersistedFloat(source.healthAfter), EnvironmentOutcomeIds.PointUnit, character);
        Require(builder.AddTag(EnvironmentOutcomeIds.EnvironmentTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.EnvironmentalExposureProducer,
            operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(EnvironmentalExposureOutcomeSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.EventKindFact, eventKind);
        AddFact(ref builder, EnvironmentOutcomeIds.BodyPartFact, "aggregate-vitals");
        AddFact(ref builder, EnvironmentOutcomeIds.EnvironmentSourceFact,
            "environment-field");
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact, reasonCode);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            source.damage > 0f
                ? $"{displayName}이 환경 노출로 {source.damage:0.###} 피해를 입었다."
                : $"{displayName}의 환경 노출 단계가 {source.bandBefore}에서 {source.bandAfter} 단계로 변했다.");
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, source.x, source.y));
        return new EnvironmentalExposureThresholdReceipt(builder.Build());
    }

    public static WildlifeCommandOutcomeReceipt CreateWildlifeCommand(
        WildlifeGameplayOutcomeSaveData source)
    {
        ValidateWildlifeOutcomeBase(
            source,
            WildlifeGameplayOutcomeProfile.Command);
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string wildlifeId = RequireStable(source.wildlifeId, nameof(source.wildlifeId));
        string wildlifeName = RequireDisplay(
            source.wildlifeDisplayName,
            nameof(source.wildlifeDisplayName));
        string eventKind = RequireStable(source.eventKind, nameof(source.eventKind));
        string reason = RequireStable(source.reasonCode, nameof(source.reasonCode));
        var wildlife = new GameplayEntityId(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.WildlifeCommandProducer,
                new GameplayOperationId(operationId),
                source.ownerRevision,
                0),
            EnvironmentOutcomeIds.WildlifeCommand,
            source.absoluteDay,
            GameplayOutcomeStatus.Succeeded,
            source.ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            wildlife,
            EnvironmentOutcomeIds.WildlifeRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(wildlifeId, wildlifeName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId,
            0.62f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.StateBeforeMetric,
            source.stateBefore, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.StateAfterMetric,
            source.stateAfter, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.FlagBeforeMetric,
            source.flagBefore ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.FlagAfterMetric,
            source.flagAfter ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.PriorityBeforeMetric,
            source.priorityBefore ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.PriorityAfterMetric,
            source.priorityAfter ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            source.ownerRevision, EnvironmentOutcomeIds.RevisionUnit, wildlife);
        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.WildlifeCommandProducer,
            operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(WildlifeGameplayOutcomeSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.EventKindFact, eventKind);
        AddFact(ref builder, EnvironmentOutcomeIds.FacilityFact,
            OptionalFact(source.facilityId));
        AddFact(ref builder, EnvironmentOutcomeIds.RelatedActorFact,
            OptionalFact(source.actorId));
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact, reason);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            BuildWildlifeCommandSummary(source, wildlifeName));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, source.x, source.y));
        return new WildlifeCommandOutcomeReceipt(builder.Build());
    }

    public static WildlifeActionOutcomeReceipt CreateWildlifeAction(
        WildlifeGameplayOutcomeSaveData source)
    {
        ValidateWildlifeOutcomeBase(
            source,
            WildlifeGameplayOutcomeProfile.Action);
        string operationId = RequireStable(source.operationId, nameof(source.operationId));
        string wildlifeId = RequireStable(source.wildlifeId, nameof(source.wildlifeId));
        string actorId = RequireStable(source.actorId, nameof(source.actorId));
        string wildlifeName = RequireDisplay(
            source.wildlifeDisplayName,
            nameof(source.wildlifeDisplayName));
        string actorName = RequireDisplay(
            source.actorDisplayName,
            nameof(source.actorDisplayName));
        string eventKind = RequireStable(source.eventKind, nameof(source.eventKind));
        string reason = RequireStable(source.reasonCode, nameof(source.reasonCode));
        var actor = new GameplayEntityId(
            EnvironmentOutcomeIds.CharacterKind,
            actorId);
        var wildlife = new GameplayEntityId(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId);
        var builder = new EnvironmentOutcomePayloadBuilder(
            new GameplayResultKey(
                EnvironmentOutcomeIds.WildlifeActionProducer,
                new GameplayOperationId(operationId),
                source.ownerRevision,
                0),
            EnvironmentOutcomeIds.WildlifeAction,
            source.absoluteDay,
            string.Equals(eventKind, "hunt-attack", StringComparison.Ordinal)
                && !source.hit
                ? GameplayOutcomeStatus.Failed
                : GameplayOutcomeStatus.Succeeded,
            source.ownerRevision);
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            actor,
            EnvironmentOutcomeIds.ActorRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(actorId, actorName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            wildlife,
            EnvironmentOutcomeIds.WildlifeRole,
            GameplayParticipationKind.Direct,
            true,
            EnvironmentOutcomeSnapshots.Name(wildlifeId, wildlifeName))));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.CharacterKind,
            actorId,
            0.62f)));
        Require(builder.AddSubject(EnvironmentOutcomeSnapshots.Subject(
            EnvironmentOutcomeIds.WildlifeKind,
            wildlifeId,
            source.killed ? 0.96f : source.hit ? 0.78f : 0.52f)));
        AddMetric(ref builder, EnvironmentOutcomeIds.StateBeforeMetric,
            source.stateBefore, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.StateAfterMetric,
            source.stateAfter, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.HealthBeforeMetric,
            source.healthBefore, EnvironmentOutcomeIds.PointUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.HealthAfterMetric,
            source.healthAfter, EnvironmentOutcomeIds.PointUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.DamageMetric,
            source.damage, EnvironmentOutcomeIds.PointUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.AmmunitionBeforeMetric,
            source.ammunitionBefore, EnvironmentOutcomeIds.CountUnit, actor);
        AddMetric(ref builder, EnvironmentOutcomeIds.AmmunitionConsumedMetric,
            source.ammunitionConsumed, EnvironmentOutcomeIds.CountUnit, actor);
        AddMetric(ref builder, EnvironmentOutcomeIds.AmmunitionAfterMetric,
            source.ammunitionAfter, EnvironmentOutcomeIds.CountUnit, actor);
        AddMetric(ref builder, EnvironmentOutcomeIds.HitMetric,
            source.hit ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.KilledMetric,
            source.killed ? 1d : 0d, EnvironmentOutcomeIds.EnumUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.OutputQuantityMetric,
            source.outputQuantity, EnvironmentOutcomeIds.CountUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.OutputMassMetric,
            source.outputMassGrams, EnvironmentOutcomeIds.GramUnit, wildlife);
        AddMetric(ref builder, EnvironmentOutcomeIds.SequenceMetric,
            source.ownerRevision, EnvironmentOutcomeIds.RevisionUnit, wildlife);
        Require(builder.AddTag(EnvironmentOutcomeIds.WildlifeTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            EnvironmentOutcomeIds.WildlifeActionProducer,
            operationId)));
        AddFact(ref builder, EnvironmentOutcomeIds.ReceiptKindFact,
            nameof(WildlifeGameplayOutcomeSaveData));
        AddFact(ref builder, EnvironmentOutcomeIds.CorrelationFact, operationId);
        AddFact(ref builder, EnvironmentOutcomeIds.EventKindFact, eventKind);
        AddFact(ref builder, EnvironmentOutcomeIds.WeaponDefinitionFact,
            OptionalFact(source.weaponDefinitionId));
        AddFact(ref builder, EnvironmentOutcomeIds.WeaponInstanceFact,
            OptionalFact(source.weaponInstanceId));
        AddFact(ref builder, EnvironmentOutcomeIds.AmmunitionItemFact,
            OptionalFact(source.ammunitionItemId));
        AddFact(ref builder, EnvironmentOutcomeIds.BodyPartFact,
            OptionalFact(source.bodyPart));
        AddFact(ref builder, EnvironmentOutcomeIds.CoverSourceFact,
            OptionalFact(source.coverSourceId));
        AddFact(ref builder, EnvironmentOutcomeIds.CarcassStackFact,
            OptionalFact(source.carcassStackId));
        AddFact(ref builder, EnvironmentOutcomeIds.FacilityFact,
            OptionalFact(source.facilityId));
        AddFact(ref builder, EnvironmentOutcomeIds.ChildResultKeyFact,
            OptionalFact(source.physicalChildResultKey));
        AddFact(ref builder, EnvironmentOutcomeIds.ReasonFact, reason);
        AddFact(ref builder, EnvironmentOutcomeIds.SummaryFact,
            BuildWildlifeActionSummary(source, actorName, wildlifeName));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, source.x, source.y));
        return new WildlifeActionOutcomeReceipt(builder.Build());
    }

    private static void ValidateWildlifeOutcomeBase(
        WildlifeGameplayOutcomeSaveData source,
        WildlifeGameplayOutcomeProfile profile)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (source.schemaVersion != WildlifeGameplayOutcomeSaveData.CurrentSchemaVersion
            || source.profile != profile
            || source.absoluteDay < 0
            || source.ownerRevision <= 0L
            || source.stateBefore < 0
            || source.stateAfter < 0
            || source.healthBefore < 0
            || source.healthAfter < 0
            || source.damage < 0
            || source.ammunitionBefore < 0
            || source.ammunitionConsumed < 0
            || source.ammunitionAfter < 0
            || source.outputQuantity < 0
            || source.outputMassGrams < 0L)
        {
            throw new InvalidOperationException(
                "A valid current wildlife gameplay outcome is required.");
        }
    }

    private static string OptionalFact(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value.Trim();

    private static string BuildWildlifeCommandSummary(
        WildlifeGameplayOutcomeSaveData source,
        string wildlifeName) => source.eventKind switch
        {
            "capture-release" => $"{wildlifeName}을 야생으로 방생했다.",
            "companion-assigned" =>
                $"{wildlifeName}을 {RequireDisplay(source.actorDisplayName, nameof(source.actorDisplayName))}의 동료로 배정했다.",
            "companion-cleared" => $"{wildlifeName}의 동료 배정을 해제했다.",
            "haul-assigned" => $"{wildlifeName}에게 운반 역할을 배정했다.",
            "haul-cleared" => $"{wildlifeName}의 운반 역할을 해제했다.",
            "haul-clear-requested" =>
                $"{wildlifeName}의 보유 화물을 배송한 뒤 운반 역할을 해제하도록 명령했다.",
            _ => source.flagAfter
                ? source.priorityAfter
                    ? $"{wildlifeName}을 우선 사냥감으로 지정했다."
                    : $"{wildlifeName}을 사냥감으로 지정했다."
                : $"{wildlifeName}의 사냥 지정을 해제했다."
        };

    private static string BuildWildlifeActionSummary(
        WildlifeGameplayOutcomeSaveData source,
        string actorName,
        string wildlifeName) => source.eventKind switch
        {
            "capture-order" =>
                $"{actorName}이 {wildlifeName}의 생포 운반을 시작했다.",
            "capture-carry-started" =>
                $"{actorName}이 {wildlifeName}을 들어 운반하기 시작했다.",
            "capture-penned" =>
                $"{actorName}이 {wildlifeName}을 우리로 운반했다.",
            "carcass-butchered" =>
                $"{actorName}이 {wildlifeName} 사체를 도축해 산출물을 얻었다.",
            _ => source.killed
                ? $"{actorName}이 {wildlifeName}을 처치했다."
                : source.hit
                    ? $"{actorName}이 {wildlifeName}에게 {source.damage} 피해를 입혔다."
                    : $"{actorName}의 공격이 {wildlifeName}에게 적중하지 않았다."
        };

    private static string RequireStable(string value, string parameterName) =>
        GameplayOutcomeStableIdSyntax.Require(value, parameterName);

    private static string RequireDisplay(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(
                "A non-empty immutable display snapshot is required.",
                parameterName)
            : value.Trim();

    private static string RequireFactValue(string value, string parameterName)
    {
        string canonical = value?.Trim() ?? string.Empty;
        if (canonical.Length == 0
            || canonical.Length
                > GameplayOutcomeBufferLimits.MaximumFactValueUtf16Length)
        {
            throw new ArgumentException(
                "A non-empty bounded gameplay outcome fact value is required.",
                parameterName);
        }
        return canonical;
    }

    private static string RequireExactText(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
        || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
        || value.IndexOf('\r') >= 0
        || value.IndexOf('\n') >= 0
            ? throw new ArgumentException(
                "A non-empty canonical text value is required.",
                parameterName)
            : value;

    private static double PersistedFloat(float value) =>
        double.Parse(
            value.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture);

    private static void AddMetric(
        ref EnvironmentOutcomePayloadBuilder builder,
        GameplayMetricId id,
        double value,
        GameplayMetricUnitId unit,
        GameplayEntityId reference) => Require(builder.AddMetric(
        new GameplayOutcomeMetric(id, value, unit, reference)));

    private static void AddFact(
        ref EnvironmentOutcomePayloadBuilder builder,
        GameplayOutcomeFactId id,
        string value) => Require(builder.AddFact(
        new GameplayOutcomeFact(id, value)));

    private static void AddRoomParticipants(
        ref EnvironmentOutcomePayloadBuilder builder,
        GameplayEntityId actor,
        string actorId,
        string actorName,
        GameplayEntityId facility,
        string facilityId,
        string facilityName,
        GameplayEntityId room,
        string roomId,
        string roomName)
    {
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            actor, EnvironmentOutcomeIds.ActorRole, GameplayParticipationKind.Direct,
            true, EnvironmentOutcomeSnapshots.Name(actorId, actorName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            facility, EnvironmentOutcomeIds.FacilityRole, GameplayParticipationKind.Direct,
            true, EnvironmentOutcomeSnapshots.Name(facilityId, facilityName))));
        Require(builder.AddParticipant(new GameplayOutcomeParticipant(
            room, EnvironmentOutcomeIds.RoomRole, GameplayParticipationKind.Direct,
            true, EnvironmentOutcomeSnapshots.Name(roomId, roomName))));
    }

    private static void AddRoomCommon(
        ref EnvironmentOutcomePayloadBuilder builder,
        BuildingInstanceId facilityId,
        CoreGridCell location,
        string receiptKind,
        string summary)
    {
        Require(builder.AddTag(EnvironmentOutcomeIds.EnvironmentTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            "room-facility", facilityId.Value)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact, receiptKind)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact, summary)));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
    }

    private static void AddFireCommon(
        ref EnvironmentOutcomePayloadBuilder builder,
        string provenanceKind,
        string operationId,
        string commitId,
        string sourceDigest,
        CoreGridCell location,
        string receiptKind,
        string summary)
    {
        Require(builder.AddTag(EnvironmentOutcomeIds.DisasterTag));
        Require(builder.AddProvenance(new GameplayOutcomeProvenanceReference(
            provenanceKind,
            operationId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.ReceiptKindFact,
            receiptKind)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SourceDigestFact,
            sourceDigest)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.CommitFact,
            commitId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.CorrelationFact,
            operationId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact,
            summary)));
        builder.SetLocation(new GameplayLocationReference(
            "dungeon", string.Empty, location.X, location.Y));
    }

    private static void Require(bool accepted)
    {
        if (!accepted)
            throw new InvalidOperationException("Environment outcome fixed buffer capacity was exceeded.");
    }
}
