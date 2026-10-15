using System;
using System.Linq;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class DefenseActivationActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private readonly ICharacterLifetimeQuery characters;
    private IDisposable subscription;
    public DefenseActivationActivityObserver(IGameEventBus events, ICharacterLifetimeQuery characters)
    { this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters)); }
    public void Initialize() => subscription ??= events.Subscribe<DefenseFacilityTriggeredEvent>(Observe);
    public void Dispose() { subscription?.Dispose(); subscription = null; }
    public static CharacterActivityEvent Project(DefenseActivationSnapshot report) =>
        CharacterActivityEvent.Facility(CharacterActivityKinds.Combat, CharacterActivityOutcomes.Damaged,
            report.FormatSummary(), report.SourceFacility, actionId: $"defense:{report.Concept}",
            reasonCode: report.Timing.ToString(), value: report.TotalDamage, quantity: report.EffectTags.Count,
            bubbleEligible: true);
    private void Observe(DefenseFacilityTriggeredEvent value)
    {
        var report = value.report ?? throw new InvalidOperationException("defense-activity-report-missing");
        CharacterActor actor = characters.AllCharacters?.FirstOrDefault(candidate => candidate != null
            && CharacterPersistentIdentity.TryGet(candidate, out CharacterId id) && id.Value == report.TargetPersistentId);
        if (actor == null) throw new InvalidOperationException("defense-activity-character-missing:" + report.TargetPersistentId);
        actor.AddActivity(Project(report));
    }
}
