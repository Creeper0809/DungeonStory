using System;
using DungeonStory.Foundation;
using VContainer.Unity;

public sealed class EvacuationActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable residentSubscription;
    private IDisposable ownerSubscription;

    public EvacuationActivityObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize()
    {
        residentSubscription ??=
            events.Subscribe<ResidentEvacuationArrivedEvent>(ObserveResident);
        ownerSubscription ??=
            events.Subscribe<OwnerEvacuationArrivedEvent>(ObserveOwner);
    }

    public void Dispose()
    {
        residentSubscription?.Dispose();
        ownerSubscription?.Dispose();
        residentSubscription = null;
        ownerSubscription = null;
    }

    public static CharacterActivityEvent Project(
        in ResidentEvacuationArrivedEvent value) =>
        CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Completed,
            "주민 대피 구역 도착",
            actionId: "invasion:resident-evacuation",
            sentiment: -0.1f,
            bubbleEligible: true);

    public static CharacterActivityEvent Project(
        in OwnerEvacuationArrivedEvent value) =>
        CharacterActivityEvent.Create(
            CharacterActivityKinds.Combat,
            CharacterActivityOutcomes.Completed,
            value.UsedAdministrationRoom
                ? "사장실 대피 완료"
                : "임시 대피 완료",
            actionId: "invasion:owner-evacuation",
            sentiment: -0.15f,
            bubbleEligible: true);

    private static void ObserveResident(
        ResidentEvacuationArrivedEvent value) =>
        value.Actor.AddActivity(Project(value));

    private static void ObserveOwner(
        OwnerEvacuationArrivedEvent value) =>
        value.Actor.AddActivity(Project(value));
}
