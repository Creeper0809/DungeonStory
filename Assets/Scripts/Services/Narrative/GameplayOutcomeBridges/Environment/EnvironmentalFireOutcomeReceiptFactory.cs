using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using static EnvironmentOutcomeReceiptFactorySupport;

public static class EnvironmentalFireOutcomeReceiptFactory
{
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

}
