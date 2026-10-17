using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects primitive-survival notifications into the existing character
/// activity and narrative presentation. Gameplay effects remain owner-side.
/// </summary>
public sealed class PrimitiveSurvivalActivityObserver : IStartable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public PrimitiveSurvivalActivityObserver(IGameEventBus events)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start() => subscription ??=
        events.Subscribe<PrimitiveSurvivalChangedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    public static CharacterActivityEvent Project(
        in PrimitiveSurvivalChangedEvent value)
    {
        value.Validate();
        return value.Transition switch
        {
            PrimitiveSurvivalTransition.Started =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Started,
                    "primitive-survival-start",
                    $"Primitive survival started: kind={value.Kind}; intent={value.IntentKind}; epoch={value.Epoch}; position={value.Position}"),
            PrimitiveSurvivalTransition.Failed =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Failed,
                    value.ReasonCode,
                    $"Primitive survival failed: kind={value.Kind}; {value.Detail}"),
            PrimitiveSurvivalTransition.MealSourceChanged =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Changed,
                    "field-meal-source-replan",
                    $"Field meal source changed: phase={value.Phase}; revision={value.SourceRevision}; expected={value.SourceStackId}@{value.SourcePosition}."),
            PrimitiveSurvivalTransition.PathSearchPending =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Changed,
                    "primitive-path-search-pending",
                    $"Running primitive retained while path search is pending: kind={value.Kind}; phase={value.Phase}; role={value.FacilityRole}."),
            PrimitiveSurvivalTransition.YieldedToFacility =>
                CharacterActivityEvent.InternalAi(
                    CharacterActivityOutcomes.Cancelled,
                    "primitive-yielded-to-authored-facility",
                    $"Primitive survival yielded to an authored facility: kind={value.Kind}; phase={value.Phase}; role={value.FacilityRole}."),
            PrimitiveSurvivalTransition.FieldMealCompleted =>
                CharacterActivityEvent.Create(
                    CharacterActivityKinds.Health,
                    CharacterActivityOutcomes.Completed,
                    $"{value.DisplayName}을(를) 야전식으로 먹음",
                    actionId: "survival:field-meal",
                    targetId: value.SourceStackId,
                    reasonCode: "no-meal-facility",
                    value: value.Recovery,
                    bubbleEligible: true),
            PrimitiveSurvivalTransition.Completed =>
                ProjectCompletion(value),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value.Transition,
                "Unknown primitive-survival transition.")
        };
    }

    private void Observe(PrimitiveSurvivalChangedEvent value)
    {
        value.Validate();
        CharacterActor actor = value.Actor;
        CharacterActivityEvent activity = Project(value);
        if (value.Transition != PrimitiveSurvivalTransition.Completed
            || string.Equals(
                value.Pending.actionId,
                "survival:bucket-wash",
                StringComparison.Ordinal))
        {
            actor.AddActivity(activity);
            return;
        }

        List<Exception> failures = null;
        TryPublish(
            () => EnsureCompletedNarrative(actor, value),
            ref failures);
        TryPublish(
            () => actor.AddActivity(activity),
            ref failures);
        if (failures != null)
        {
            throw new AggregateException(
                "Committed primitive completion publication failed.",
                failures);
        }
    }

    private static CharacterActivityEvent ProjectCompletion(
        in PrimitiveSurvivalChangedEvent value)
    {
        PrimitiveSurvivalPendingSaveData pending = value.Pending;
        if (string.Equals(
                pending.actionId,
                "survival:bucket-wash",
                StringComparison.Ordinal))
        {
            return CharacterActivityEvent.Create(
                CharacterActivityKinds.Health,
                CharacterActivityOutcomes.Completed,
                "깨끗한 물 한 단위로 몸을 씻음",
                actionId: "survival:bucket-wash",
                targetId: pending.waterStackId,
                reasonCode: "no-hygiene-facility",
                value: pending.recovery,
                bubbleEligible: true);
        }

        bool floorRest = string.Equals(
            pending.actionId,
            "survival:floor-rest",
            StringComparison.Ordinal);
        return CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Completed,
            floorRest
                ? "맨바닥에서 잠을 청함"
                : "지정된 임시 변소를 사용함",
            actionId: pending.actionId,
            reasonCode: floorRest
                ? "no-rest-facility"
                : "no-toilet-facility",
            value: pending.recovery,
            narrativeEligible: false,
            bubbleEligible: true);
    }

    private static void EnsureCompletedNarrative(
        CharacterActor actor,
        in PrimitiveSurvivalChangedEvent value)
    {
        PrimitiveSurvivalPendingSaveData pending = value.Pending;
        if (actor?.Progression == null)
            return;

        var eventContext = new GameplayNarrativeEventContext
        {
            eventInstanceId = value.PublicationId,
            chainId = value.PublicationId,
            actorId = pending.characterId,
            actorDisplayName = pending.characterName,
            resultDetail = "생존 행동 완료",
            occurredDay = pending.absoluteDay
        };
        bool alreadyRecorded = actor.Progression.NarrativeLedger.Facts.Any(item =>
            item?.lastEventContext != null
            && string.Equals(
                item.lastEventContext.eventInstanceId,
                eventContext.eventInstanceId,
                StringComparison.Ordinal));
        if (alreadyRecorded)
            return;

        actor.Progression.RecordNarrative(
            CharacterNarrativeDomain.Survival,
            pending.actionId + ":completed",
            pending.characterId,
            CharacterActivityOutcomes.Completed,
            pending.recovery,
            pending.absoluteDay,
            new CharacterNarrativeEvidenceMetadata(
                "primitive-survival",
                pending.actionId,
                string.Empty,
                1f),
            triggerPassives: true,
            eventContext: eventContext);
    }

    private static void TryPublish(
        Action callback,
        ref List<Exception> failures)
    {
        try
        {
            callback();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            (failures ??= new List<Exception>()).Add(exception);
        }
    }
}
