using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects applied social reactions into the existing activity feed. Social
/// memory, mood, combat, and publication state remain with gameplay owners.
/// </summary>
public sealed class SocialInteractionActivityObserver :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterLifetimeQuery world;
    private IDisposable insultSubscription;
    private IDisposable minionSubscription;

    public SocialInteractionActivityObserver(
        IGameEventBus events,
        ICharacterLifetimeQuery world)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public void Initialize()
    {
        if (insultSubscription != null || minionSubscription != null)
            return;
        IDisposable insult = events.Subscribe<SocialInsultAnsweredEvent>(
            ObserveInsult);
        try
        {
            IDisposable minion =
                events.Subscribe<MinionSocialConflictAppliedEvent>(
                    ObserveMinionConflict);
            insultSubscription = insult;
            minionSubscription = minion;
        }
        catch
        {
            insult.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        insultSubscription?.Dispose();
        minionSubscription?.Dispose();
        insultSubscription = null;
        minionSubscription = null;
    }

    public static CharacterActivityEvent Project(
        in SocialInsultAnsweredEvent value) =>
        CharacterActivityEvent.Create(
            CharacterActivityKinds.Social,
            CharacterActivityOutcomes.Responded,
            $"{value.InstigatorName ?? "상대"}의 모욕에 즉시 맞섰다.",
            actionId: "social:answer-insult",
            reasonCode: "trait:hot-blooded",
            sentiment: 0.35f,
            bubbleEligible: true);

    public static CharacterActivityEvent Project(
        in MinionSocialConflictAppliedEvent value) =>
        CharacterActivityEvent.Create(
            CharacterActivityKinds.Social,
            CharacterActivityOutcomes.Changed,
            value.ResidentName + (value.Brawl ? "와 몸싸움" : "와 말다툼"),
            actionId: "minion-social-conflict",
            reasonCode: value.Brawl ? "brawl" : "argument",
            sentiment: -0.6f,
            bubbleEligible: true);

    private void ObserveInsult(SocialInsultAnsweredEvent value) =>
        Find(value.TargetId, "social-insult-answer-activity-character-missing")
            .AddActivity(Project(value));

    private void ObserveMinionConflict(MinionSocialConflictAppliedEvent value)
    {
        CharacterActivityEvent activity = Project(value);
        CharacterActor minion = Find(
            value.MinionId,
            "minion-social-activity-minion-missing");
        CharacterActor resident = Find(
            value.ResidentId,
            "minion-social-activity-resident-missing");
        minion.AddActivity(activity);
        resident.AddActivity(activity);
    }

    private CharacterActor Find(string characterId, string error)
    {
        CharacterActor actor = world.AllCharacters?.FirstOrDefault(candidate =>
            candidate != null
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id)
            && string.Equals(id.Value, characterId, StringComparison.Ordinal));
        return actor ?? throw new InvalidOperationException(
            error + ":" + characterId);
    }
}
