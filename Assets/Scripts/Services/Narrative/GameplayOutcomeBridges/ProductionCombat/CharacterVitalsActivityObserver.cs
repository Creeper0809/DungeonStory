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
            value.Amount > 0f && value.Amount < 0.05f
                ? string.IsNullOrWhiteSpace(value.Reason)
                    ? "소량의 피해"
                    : $"소량의 피해: {value.Reason}"
                : string.IsNullOrWhiteSpace(value.Reason)
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

#if UNITY_EDITOR
    public static void VerifySmallDamageProjectionForBatch()
    {
        var character = new CharacterId("character:small-damage-projection");
        CharacterActivityEvent small = Project(new CharacterVitalsChangedEvent(
            character, CharacterVitalTransition.Damaged, 0.01f, "출혈"));
        CharacterActivityEvent withoutReason = Project(new CharacterVitalsChangedEvent(
            character, CharacterVitalTransition.Damaged, 0.01f));
        CharacterActivityEvent normal = Project(new CharacterVitalsChangedEvent(
            character, CharacterVitalTransition.Damaged, 3.5f, "출혈"));
        CharacterActivityEvent zero = Project(new CharacterVitalsChangedEvent(
            character, CharacterVitalTransition.Damaged, 0f, "출혈"));
        CharacterActivityEvent threshold = Project(new CharacterVitalsChangedEvent(
            character, CharacterVitalTransition.Damaged, 0.05f, "출혈"));
        if (small.FactText != "소량의 피해: 출혈" || small.Value != 0.01f
            || small.ReasonCode != "출혈" || small.OutcomeId != CharacterActivityOutcomes.Damaged
            || withoutReason.FactText != "소량의 피해" || withoutReason.Value != 0.01f
            || normal.FactText != $"피해 {3.5f:0.#}: 출혈" || normal.Value != 3.5f
            || zero.FactText != $"피해 {0f:0.#}: 출혈" || zero.Value != 0f
            || threshold.FactText != $"피해 {0.05f:0.#}: 출혈" || threshold.Value != 0.05f)
        {
            throw new InvalidOperationException("Small damage projection changed its value or display contract.");
        }
    }
#endif

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
