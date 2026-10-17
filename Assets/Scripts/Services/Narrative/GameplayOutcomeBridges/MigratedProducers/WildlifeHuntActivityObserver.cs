using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

// Transient presentation only. Wildlife effects and its durable outcome owner are separate.
public sealed class WildlifeHuntActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterAiWorldRegistry world;
    private IDisposable attackSubscription, completionSubscription;
    public WildlifeHuntActivityObserver(IGameEventBus events, ICharacterAiWorldRegistry world)
    { this.events = events ?? throw new ArgumentNullException(nameof(events)); this.world = world ?? throw new ArgumentNullException(nameof(world)); }
    public void Initialize()
    {
        if (attackSubscription != null) return;
        var attack = events.Subscribe<WildlifeHuntAttackResolvedEvent>(ObserveAttack);
        try
        {
            completionSubscription = events.Subscribe<WildlifeHuntCompletedEvent>(ObserveCompletion);
            attackSubscription = attack;
        }
        catch { attack.Dispose(); throw; }
    }
    public void Dispose()
    {
        var attack = attackSubscription; var completion = completionSubscription;
        attackSubscription = null; completionSubscription = null;
        try { completion?.Dispose(); } finally { attack?.Dispose(); }
    }
    public static CharacterActivityEvent Project(in WildlifeHuntAttackResolvedEvent value) => CharacterActivityEvent.Create(
        CharacterActivityKinds.Combat,
        value.Killed ? CharacterActivityOutcomes.Completed : CharacterActivityOutcomes.Progress,
        value.Killed ? $"{value.HunterName}이(가) {value.WildlifeName} 사냥을 끝냈다."
            : value.Hit ? $"{value.HunterName}이(가) {value.WildlifeName}의 {BodyPartName(value.BodyPart)}에 {value.AppliedDamage} 피해를 입혔다."
            : value.CoverBlocked ? $"{value.HunterName}의 공격이 엄폐물에 막혔다."
            : $"{value.HunterName}의 공격을 {value.WildlifeName}이(가) 피했다.",
        actionId: "survival/hunt", targetId: "wildlife:" + value.WildlifeId, targetName: value.WildlifeName,
        value: value.AppliedDamage, sentiment: value.Killed ? 0.45f : value.Hit ? 0.1f : -0.1f, bubbleEligible: true);

    public static CharacterStateNarrativeProjection Project(in WildlifeHuntCompletedEvent value) => new(
        value.Hunter, CharacterNarrativeDomain.Survival, "survival/hunt", "wildlife:" + value.SpeciesId,
        value.Dangerous ? "dangerous-hunt" : "hunt", value.MaximumHealth, value.AbsoluteDay,
        triggerPassives: true, context: null);

    private void ObserveAttack(WildlifeHuntAttackResolvedEvent value) => Find(value.Hunter).AddActivity(Project(value));
    private void ObserveCompletion(WildlifeHuntCompletedEvent value)
    {
        var projection = Project(value);
        Find(value.Hunter).Progression?.RecordNarrative(projection.Domain, projection.FactId,
            projection.SubjectId, projection.Outcome, projection.Value, projection.Day);
    }
    private CharacterActor Find(CharacterId id) => world.AllCharacters.FirstOrDefault(actor => actor != null
        && CharacterPersistentIdentity.TryGet(actor, out var candidate) && candidate.Equals(id))
        ?? throw new InvalidOperationException("wildlife-hunt-observer-character-missing:" + id.Value);
    private static string BodyPartName(CombatBodyPart part) => part switch
    {
        CombatBodyPart.Head => "머리", CombatBodyPart.Torso => "몸통", CombatBodyPart.LeftArm => "왼앞다리",
        CombatBodyPart.RightArm => "오른앞다리", CombatBodyPart.LeftLeg => "왼뒷다리", CombatBodyPart.RightLeg => "오른뒷다리", _ => "몸"
    };
}
