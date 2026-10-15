using System;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects applied carcass-processing facts into the existing activity and
/// taboo narrative presentation without owning gameplay or storage effects.
/// </summary>
public sealed class WildlifeCarcassActivityObserver : IStartable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public WildlifeCarcassActivityObserver(IGameEventBus events)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start() => subscription ??=
        events.Subscribe<WildlifeCarcassProcessedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private static void Observe(WildlifeCarcassProcessedEvent value)
    {
        WildlifeCarcassProcessingData fact = (value.Fact
            ?? throw new InvalidOperationException(
                "wildlife-carcass-processed-event-invalid"))
            .Snapshot();
        if (fact.taboo)
        {
            CharacterActor actor = value.Actor;
            if (actor == null)
            {
                throw new InvalidOperationException(
                    "wildlife-carcass-taboo-actor-missing");
            }
            actor.Progression?.RecordNarrative(
                CharacterNarrativeDomain.Survival,
                "survival/taboo-butchery",
                fact.sourceCharacterId,
                fact.sameSpecies ? "same-species" : "humanoid",
                fact.outputQuantity,
                fact.absoluteDay);
            return;
        }

        value.Visitor?.RecordActivity(
            value.Building,
            new BuildingActivitySnapshot(
                BuildingActivityKinds.Work,
                BuildingActivityOutcomes.Completed,
                $"{fact.speciesDisplayName} 사체 손질을 마쳤다.",
                BuiltInWorkTypeIds.Butcher.Value,
                string.Empty,
                "wildlife-butchered",
                0f,
                fact.outputQuantity,
                false));
    }
}
