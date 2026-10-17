using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects applied character vital changes into the existing activity log.
/// Vital state, mood, lifecycle, and death effects remain with the publisher.
/// </summary>
public sealed class CharacterVitalsActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterAiWorldRegistry world;
    private IDisposable subscription;

    public CharacterVitalsActivityObserver(
        IGameEventBus events,
        ICharacterAiWorldRegistry world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<CharacterVitalsChangedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    public static CharacterActivityEvent Project(
        in CharacterVitalsChangedEvent value) => value.Transition switch
    {
        CharacterVitalTransition.Damaged => CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Damaged,
            string.IsNullOrWhiteSpace(value.Reason)
                ? $"피해 {value.Amount:0.#}"
                : $"피해 {value.Amount:0.#}: {value.Reason}",
            actionId: "health:damage",
            reasonCode: value.Reason,
            value: value.Amount,
            sentiment: -0.8f,
            bubbleEligible: true),
        CharacterVitalTransition.Healed => CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Completed,
            $"회복 {value.Amount:0.#}",
            actionId: "health:heal",
            value: value.Amount,
            sentiment: 0.55f),
        CharacterVitalTransition.InjurySeverityChanged => ProjectInjurySeverity(
            value.Amount),
        CharacterVitalTransition.Died => CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Defeated,
            string.IsNullOrWhiteSpace(value.Reason)
                ? "사망"
                : $"사망: {value.Reason}",
            actionId: "health:death",
            reasonCode: value.Reason,
            value: 1f,
            sentiment: -1f,
            bubbleEligible: true),
        _ => throw new ArgumentOutOfRangeException(
            nameof(value),
            value.Transition,
            "Unknown character vital transition.")
    };

    private void Observe(CharacterVitalsChangedEvent value)
    {
        CharacterActor actor = world.AllCharacters.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(
                candidate,
                out CharacterId id)
            && id.Equals(value.Character));
        if (actor == null)
        {
            throw new InvalidOperationException(
                "character-vitals-activity-character-missing:"
                + value.Character.Value);
        }

        actor.LogComponent?.AddActivity(Project(value));
    }

    private static CharacterActivityEvent ProjectInjurySeverity(float amount)
    {
        float injurySeverity = Math.Clamp(amount, 0f, 1f);
        int percentage = (int)Math.Round(
            injurySeverity * 100f,
            MidpointRounding.ToEven);
        return CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Changed,
            $"부상도 변경: {percentage}%",
            actionId: "health:injury-severity",
            value: injurySeverity,
            sentiment: -injurySeverity);
    }
}
