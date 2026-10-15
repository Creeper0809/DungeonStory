using System;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class IdleSocialInteractionOutcomeIds
{
    public const string ProducerId = "social.idle-interaction";
    public static readonly GameplayOutcomeTypeId OutcomeType =
        new("social.idle-interaction-resolved");
    public static readonly GameplayEntityKindId CharacterKind = new("character");
    public static readonly GameplayEntityKindId BehaviorKind =
        new("social-behavior");
    public static readonly GameplayEntityKindId ReasonKind =
        new("social-reason");
    public static readonly GameplayRoleId ActorRole = new("actor");
    public static readonly GameplayRoleId TargetRole = new("target");
    public static readonly GameplayMetricId ActorRelationshipBefore =
        new("relationship.actor-to-target-before");
    public static readonly GameplayMetricId ActorRelationshipAfter =
        new("relationship.actor-to-target-after");
    public static readonly GameplayMetricId TargetRelationshipBefore =
        new("relationship.target-to-actor-before");
    public static readonly GameplayMetricId TargetRelationshipAfter =
        new("relationship.target-to-actor-after");
    public static readonly GameplayMetricId MoodBefore = new("mood.before");
    public static readonly GameplayMetricId MoodAfter = new("mood.after");
    public static readonly GameplayMetricId Behavior = new("social.behavior");
    public static readonly GameplayMetricId Reason = new("social.reason");
    public static readonly GameplayMetricUnitId PointUnit = new("point");
    public static readonly GameplayMetricUnitId BooleanUnit = new("boolean");
    public static readonly GameplayOutcomeTagId RelationshipTag =
        new("relationship");
    public static readonly GameplayOutcomeTagId MoodTag = new("mood");
    public static readonly GameplayOutcomeProvenanceReference Provenance =
        new("source-receipt", "idle-social-interaction");
}

public readonly struct IdleSocialInteractionOutcomeReceipt
{
    public IdleSocialInteractionOutcomeReceipt(
        string operationId,
        CharacterId actor,
        KoreanNameSnapshot actorName,
        CharacterId target,
        KoreanNameSnapshot targetName,
        string behaviorId,
        string reasonId,
        float actorRelationshipBefore,
        float actorRelationshipAfter,
        float targetRelationshipBefore,
        float targetRelationshipAfter,
        float moodBefore,
        float moodAfter,
        int absoluteDay)
    {
        OperationId = new GameplayOperationId(operationId);
        if (!actor.IsValid || !target.IsValid || actor.Equals(target))
            throw new ArgumentException("Distinct social participants are required.");
        if (!GameplayOutcomeLedger.IsValidDisplayNameSnapshot(actorName)
            || !GameplayOutcomeLedger.IsValidDisplayNameSnapshot(targetName))
        {
            throw new ArgumentException("Historical social participant names are required.");
        }
        BehaviorId = GameplayOutcomeStableIdSyntax.Require(
            behaviorId,
            nameof(behaviorId));
        ReasonId = GameplayOutcomeStableIdSyntax.Require(
            reasonId,
            nameof(reasonId));
        if (!float.IsFinite(actorRelationshipBefore)
            || !float.IsFinite(actorRelationshipAfter)
            || !float.IsFinite(targetRelationshipBefore)
            || !float.IsFinite(targetRelationshipAfter)
            || !float.IsFinite(moodBefore)
            || !float.IsFinite(moodAfter)
            || absoluteDay < 0)
        {
            throw new ArgumentException("Idle social result values are invalid.");
        }
        Actor = actor;
        ActorName = actorName;
        Target = target;
        TargetName = targetName;
        ActorRelationshipBefore = actorRelationshipBefore;
        ActorRelationshipAfter = actorRelationshipAfter;
        TargetRelationshipBefore = targetRelationshipBefore;
        TargetRelationshipAfter = targetRelationshipAfter;
        MoodBefore = moodBefore;
        MoodAfter = moodAfter;
        AbsoluteDay = absoluteDay;
    }

    public GameplayOperationId OperationId { get; }
    public CharacterId Actor { get; }
    public KoreanNameSnapshot ActorName { get; }
    public CharacterId Target { get; }
    public KoreanNameSnapshot TargetName { get; }
    public string BehaviorId { get; }
    public string ReasonId { get; }
    public float ActorRelationshipBefore { get; }
    public float ActorRelationshipAfter { get; }
    public float TargetRelationshipBefore { get; }
    public float TargetRelationshipAfter { get; }
    public float MoodBefore { get; }
    public float MoodAfter { get; }
    public int AbsoluteDay { get; }
    public GameplayResultKey ResultKey => new(
        IdleSocialInteractionOutcomeIds.ProducerId,
        OperationId,
        0L,
        0);
}

public sealed class IdleSocialInteractionOutcomeAdapter :
    GameplayOutcomeAdapter<IdleSocialInteractionOutcomeReceipt>
{
    public override GameplayOutcomeTypeId OutcomeTypeId =>
        IdleSocialInteractionOutcomeIds.OutcomeType;

    public override OutcomePrepareResult TryGetRequirements(
        in IdleSocialInteractionOutcomeReceipt receipt,
        long currentWorldEpoch,
        out OutcomeWriteRequirements requirements)
    {
        requirements = CreateRequirements(
            receipt.ResultKey,
            receipt.AbsoluteDay,
            currentWorldEpoch);
        return receipt.OperationId.IsValid
            && receipt.Actor.IsValid
            && receipt.Target.IsValid
            && !receipt.Actor.Equals(receipt.Target)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.BehaviorId)
            && GameplayOutcomeStableIdSyntax.IsValid(receipt.ReasonId)
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.InvalidReceipt,
                "idle-social-interaction-receipt-invalid");
    }

    internal static OutcomeWriteRequirements CreateRequirements(
        GameplayResultKey key,
        int day,
        long epoch) => new(
        key,
        IdleSocialInteractionOutcomeIds.OutcomeType,
        day,
        GameplayOutcomeStatus.Succeeded,
        epoch,
        0L,
        participantCount: 2,
        metricCount: 8,
        subjectCount: 2,
        tagCount: 2,
        anchorCount: 0,
        provenanceCount: 1);

    public override OutcomePrepareResult TryWrite(
        in IdleSocialInteractionOutcomeReceipt receipt,
        ref OutcomeWriteBuilder builder)
    {
        GameplayEntityId actor = new(
            IdleSocialInteractionOutcomeIds.CharacterKind,
            receipt.Actor.Value);
        GameplayEntityId target = new(
            IdleSocialInteractionOutcomeIds.CharacterKind,
            receipt.Target.Value);
        GameplayEntityId behavior = new(
            IdleSocialInteractionOutcomeIds.BehaviorKind,
            receipt.BehaviorId);
        GameplayEntityId reason = new(
            IdleSocialInteractionOutcomeIds.ReasonKind,
            receipt.ReasonId);
        bool written = builder.AddParticipant(new GameplayOutcomeParticipant(
                actor,
                IdleSocialInteractionOutcomeIds.ActorRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.ActorName))
            && builder.AddParticipant(new GameplayOutcomeParticipant(
                target,
                IdleSocialInteractionOutcomeIds.TargetRole,
                GameplayParticipationKind.Direct,
                true,
                receipt.TargetName))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                actor, 0.52f, NarrativeMemoryTier.Recent, false, false, 0))
            && builder.AddSubject(new GameplayOutcomeSubjectLink(
                target, 0.58f, NarrativeMemoryTier.Recent, false, false, 0))
            && AddMetric(ref builder, IdleSocialInteractionOutcomeIds.ActorRelationshipBefore, receipt.ActorRelationshipBefore, actor)
            && AddMetric(ref builder, IdleSocialInteractionOutcomeIds.ActorRelationshipAfter, receipt.ActorRelationshipAfter, actor)
            && AddMetric(ref builder, IdleSocialInteractionOutcomeIds.TargetRelationshipBefore, receipt.TargetRelationshipBefore, target)
            && AddMetric(ref builder, IdleSocialInteractionOutcomeIds.TargetRelationshipAfter, receipt.TargetRelationshipAfter, target)
            && AddMetric(ref builder, IdleSocialInteractionOutcomeIds.MoodBefore, receipt.MoodBefore, target)
            && AddMetric(ref builder, IdleSocialInteractionOutcomeIds.MoodAfter, receipt.MoodAfter, target)
            && builder.AddMetric(new GameplayOutcomeMetric(
                IdleSocialInteractionOutcomeIds.Behavior,
                1d,
                IdleSocialInteractionOutcomeIds.BooleanUnit,
                behavior))
            && builder.AddMetric(new GameplayOutcomeMetric(
                IdleSocialInteractionOutcomeIds.Reason,
                1d,
                IdleSocialInteractionOutcomeIds.BooleanUnit,
                reason))
            && builder.AddTag(IdleSocialInteractionOutcomeIds.RelationshipTag)
            && builder.AddTag(IdleSocialInteractionOutcomeIds.MoodTag)
            && builder.AddProvenance(
                IdleSocialInteractionOutcomeIds.Provenance);
        return written
            ? OutcomePrepareResult.Prepared()
            : new OutcomePrepareResult(
                OutcomePrepareCode.AdapterWriteFailed,
                "idle-social-interaction-write-failed");
    }

    private static bool AddMetric(
        ref OutcomeWriteBuilder builder,
        GameplayMetricId id,
        float value,
        GameplayEntityId entity) => builder.AddMetric(
        new GameplayOutcomeMetric(
            id,
            value,
            IdleSocialInteractionOutcomeIds.PointUnit,
            entity));
}

internal sealed class IdleSocialInteractionPerspectiveProjector :
    INarrativePerspectiveProjector
{
    public NarrativeView Project(
        in GameplayOutcomeReadView outcome,
        NarrativePerspectiveContext perspective)
    {
        KoreanNameSnapshot actor = default;
        KoreanNameSnapshot target = default;
        string behavior = "social:interaction";
        for (int index = 0; index < outcome.ParticipantCount; index++)
        {
            GameplayOutcomeParticipant participant = outcome.GetParticipant(index);
            if (participant.RoleId.Equals(IdleSocialInteractionOutcomeIds.ActorRole))
                actor = participant.DisplayName;
            if (participant.RoleId.Equals(IdleSocialInteractionOutcomeIds.TargetRole))
                target = participant.DisplayName;
        }
        for (int index = 0; index < outcome.MetricCount; index++)
        {
            GameplayOutcomeMetric metric = outcome.GetMetric(index);
            if (metric.MetricId.Equals(IdleSocialInteractionOutcomeIds.Behavior))
                behavior = metric.DefinitionOrInstanceId.Value;
        }
        string action = behavior switch
        {
            "social:reconcile" => "화해했다",
            "social:encourage" => "격려했다",
            "social:formal-etiquette" => "예를 갖춰 인사했다",
            _ => "대화를 나눴다"
        };
        return new NarrativeView(
            outcome.OutcomeId,
            perspective.Kind,
            $"{actor.DisplayText}이(가) {target.DisplayText}과(와) {action}.",
            "idle-social-interaction-v1",
            true);
    }
}

public sealed class IdleSocialInteractionOutcomeDescriptor :
    IGameplayOutcomeDescriptor
{
    private static readonly INarrativePerspectiveProjector Projector =
        new IdleSocialInteractionPerspectiveProjector();
    private static readonly IOutcomeMemoryPolicy Memory =
        new SocialLifeOutcomeMemoryPolicy();
    private static readonly IOutcomePerceptionPolicy Perception =
        new SocialLifeOutcomePerceptionPolicy();
    private static readonly IOutcomeMemoryConsolidator Consolidator =
        new SocialConflictOutcomeConsolidator();

    public GameplayOutcomeTypeId OutcomeTypeId =>
        IdleSocialInteractionOutcomeIds.OutcomeType;
    public INarrativePerspectiveProjector PerspectiveProjector => Projector;
    public IOutcomeMemoryPolicy MemoryPolicy => Memory;
    public IOutcomePerceptionPolicy PerceptionPolicy => Perception;
    public IOutcomeMemoryConsolidator MemoryConsolidator => Consolidator;
    public bool IsKnownRole(GameplayRoleId roleId) =>
        roleId.Equals(IdleSocialInteractionOutcomeIds.ActorRole)
        || roleId.Equals(IdleSocialInteractionOutcomeIds.TargetRole);
    public bool IsKnownMetric(
        GameplayMetricId metricId,
        GameplayMetricUnitId unitId) =>
        (metricId.Equals(IdleSocialInteractionOutcomeIds.Behavior)
            || metricId.Equals(IdleSocialInteractionOutcomeIds.Reason))
            && unitId.Equals(IdleSocialInteractionOutcomeIds.BooleanUnit)
        || (metricId.Equals(IdleSocialInteractionOutcomeIds.ActorRelationshipBefore)
            || metricId.Equals(IdleSocialInteractionOutcomeIds.ActorRelationshipAfter)
            || metricId.Equals(IdleSocialInteractionOutcomeIds.TargetRelationshipBefore)
            || metricId.Equals(IdleSocialInteractionOutcomeIds.TargetRelationshipAfter)
            || metricId.Equals(IdleSocialInteractionOutcomeIds.MoodBefore)
            || metricId.Equals(IdleSocialInteractionOutcomeIds.MoodAfter))
            && unitId.Equals(IdleSocialInteractionOutcomeIds.PointUnit);
    public OutcomeValidationResult Validate(in GameplayOutcomeReadView outcome) =>
        outcome.OutcomeTypeId == OutcomeTypeId
        && outcome.ParticipantCount == 2
        && outcome.MetricCount == 8
        && outcome.SubjectCount == 2
        && outcome.TagCount == 2
        && outcome.AnchorCount == 0
        && outcome.ProvenanceCount == 1
            ? OutcomeValidationResult.Accepted
            : OutcomeValidationResult.Reject(
                "idle-social-interaction-shape-invalid");
}

