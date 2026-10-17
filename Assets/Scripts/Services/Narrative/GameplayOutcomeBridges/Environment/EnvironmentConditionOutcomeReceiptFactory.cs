using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using static EnvironmentOutcomeReceiptFactorySupport;

public static class EnvironmentConditionOutcomeReceiptFactory
{
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

}
