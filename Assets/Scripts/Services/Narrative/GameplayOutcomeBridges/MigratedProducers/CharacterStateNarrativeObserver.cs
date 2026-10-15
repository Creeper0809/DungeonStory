using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

public readonly struct CharacterStateNarrativeProjection
{
    private readonly GameplayNarrativeEventContext context;

    public CharacterStateNarrativeProjection(
        CharacterId character,
        CharacterNarrativeDomain domain,
        string factId,
        string subjectId,
        string outcome,
        float value,
        int day,
        bool triggerPassives,
        GameplayNarrativeEventContext context)
    {
        Character = character;
        Domain = domain;
        FactId = factId ?? string.Empty;
        SubjectId = subjectId ?? string.Empty;
        Outcome = outcome ?? string.Empty;
        Value = value;
        Day = day;
        TriggerPassives = triggerPassives;
        this.context = CharacterStateNarrativeObserver.CopyContext(context);
    }

    public CharacterId Character { get; }
    public CharacterNarrativeDomain Domain { get; }
    public string FactId { get; }
    public string SubjectId { get; }
    public string Outcome { get; }
    public float Value { get; }
    public int Day { get; }
    public bool TriggerPassives { get; }
    public GameplayNarrativeEventContext Context =>
        CharacterStateNarrativeObserver.CopyContext(context);
}

/// <summary>
/// Projects applied condition and mood notifications into the existing
/// character narrative ledger. State ownership remains with the publisher.
/// </summary>
public sealed class CharacterStateNarrativeObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterAiWorldRegistry world;
    private IDisposable conditionSubscription;
    private IDisposable moodSubscription;

    public CharacterStateNarrativeObserver(
        IGameEventBus events,
        ICharacterAiWorldRegistry world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize()
    {
        if (conditionSubscription != null || moodSubscription != null)
            return;

        IDisposable condition = events.Subscribe<CharacterConditionChangedEvent>(
            ObserveCondition);
        try
        {
            IDisposable mood = events.Subscribe<CharacterMoodFactorAppliedEvent>(
                ObserveMood);
            conditionSubscription = condition;
            moodSubscription = mood;
        }
        catch
        {
            condition.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        IDisposable condition = conditionSubscription;
        IDisposable mood = moodSubscription;
        conditionSubscription = null;
        moodSubscription = null;
        try
        {
            mood?.Dispose();
        }
        finally
        {
            condition?.Dispose();
        }
    }

    public static bool TryProject(
        in CharacterConditionChangedEvent value,
        out CharacterStateNarrativeProjection projection)
    {
        bool critical = value.Before >= 20f && value.After < 20f;
        bool satisfied = value.Before <= 80f && value.After > 80f;
        if (!critical && !satisfied)
        {
            projection = default;
            return false;
        }

        projection = new CharacterStateNarrativeProjection(
            value.Character,
            CharacterNarrativeDomain.Need,
            "need:" + value.Condition.ToString().ToLowerInvariant(),
            string.Empty,
            critical ? "critical" : "satisfied",
            value.After,
            value.Day,
            triggerPassives: true,
            context: ProjectContext(value.Context));
        return true;
    }

    public static bool TryProject(
        in CharacterMoodFactorAppliedEvent value,
        out CharacterStateNarrativeProjection projection)
    {
        if (value.FactorId.StartsWith("skill:", StringComparison.Ordinal))
        {
            projection = default;
            return false;
        }

        projection = new CharacterStateNarrativeProjection(
            value.Character,
            CharacterNarrativeDomain.Mood,
            value.FactorId,
            string.Empty,
            value.Value >= 0f ? "positive" : "negative",
            value.Value,
            value.Day,
            triggerPassives: true,
            context: ProjectContext(value.Context));
        return true;
    }

    public static GameplayNarrativeEventContext ProjectContext(
        CharacterEventContext value) => value == null
        ? null
        : new GameplayNarrativeEventContext
        {
            eventInstanceId = value.eventInstanceId,
            chainId = value.chainId,
            locationId = value.locationId,
            locationDisplayName = value.locationDisplayName,
            actorId = value.actorId,
            actorDisplayName = value.actorDisplayName,
            counterpartyId = value.counterpartyId,
            counterpartyDisplayName = value.counterpartyDisplayName,
            usedObjectId = value.usedObjectId,
            usedObjectDisplayName = value.usedObjectDisplayName,
            resultDetail = value.resultDetail,
            occurredDay = value.occurredDay,
            sequence = value.sequence
        };

    internal static GameplayNarrativeEventContext CopyContext(
        GameplayNarrativeEventContext value) => value == null
        ? null
        : new GameplayNarrativeEventContext
        {
            eventInstanceId = value.eventInstanceId,
            chainId = value.chainId,
            locationId = value.locationId,
            locationDisplayName = value.locationDisplayName,
            actorId = value.actorId,
            actorDisplayName = value.actorDisplayName,
            counterpartyId = value.counterpartyId,
            counterpartyDisplayName = value.counterpartyDisplayName,
            usedObjectId = value.usedObjectId,
            usedObjectDisplayName = value.usedObjectDisplayName,
            resultDetail = value.resultDetail,
            occurredDay = value.occurredDay,
            sequence = value.sequence
        };

    private void ObserveCondition(CharacterConditionChangedEvent value)
    {
        if (TryProject(value, out CharacterStateNarrativeProjection projection))
            Apply(projection);
    }

    private void ObserveMood(CharacterMoodFactorAppliedEvent value)
    {
        if (TryProject(value, out CharacterStateNarrativeProjection projection))
            Apply(projection);
    }

    private void Apply(in CharacterStateNarrativeProjection projection)
    {
        CharacterId character = projection.Character;
        CharacterActor actor = world.AllCharacters.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(
                candidate,
                out CharacterId id)
            && id.Equals(character));
        if (actor == null)
        {
            throw new InvalidOperationException(
                "character-state-narrative-character-missing:"
                + projection.Character.Value);
        }

        actor.Progression?.RecordNarrative(
            projection.Domain,
            projection.FactId,
            projection.SubjectId,
            projection.Outcome,
            projection.Value,
            projection.Day,
            projection.TriggerPassives,
            projection.Context);
    }
}
