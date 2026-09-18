using System;
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
            source.InputVectorDigest)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.OutputDigestFact,
            source.OutputVectorDigest)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.CommitFact,
            source.OutputBatchCommitId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.FingerprintFact,
            source.OutputOutcomeFingerprint)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.IndoorFact,
            source.Indoor ? "true" : "false")));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.CorrelationFact,
            source.ActionId)));
        Require(builder.AddFact(new GameplayOutcomeFact(
            EnvironmentOutcomeIds.SummaryFact,
            source.Succeeded
                ? $"{cropDisplayName} 재배를 마쳐 산출물 {outputQuantity}개를 확보했다."
                : $"{cropDisplayName} 재배가 {source.FailureReasonCode} 사유로 종료됐다.")));
        return new CropPlanGameplayOutcomeReceipt(builder.Build());
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
            physical.DisplayName)));
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
            $"{physical.DisplayName.DisplayText} {relocation.Quantity}개가 돌풍에 "
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

    private static string RequireStable(string value, string parameterName) =>
        GameplayOutcomeStableIdSyntax.Require(value, parameterName);

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
