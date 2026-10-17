using System;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using VContainer.Unity;

public sealed class CharacterActionActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable macro, expedition, abandoned, substance, shopping, mentorship;
    public CharacterActionActivityObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    public void Initialize()
    {
        macro ??= events.Subscribe<CharacterMacroActionTransitionEvent>(Observe);
        expedition ??= events.Subscribe<CharacterExpeditionPresenceChangedEvent>(Observe);
        abandoned ??= events.Subscribe<CharacterInterruptedWorkAbandonedEvent>(Observe);
        substance ??= events.Subscribe<SubstanceUseCompletedEvent>(Observe);
        shopping ??= events.Subscribe<ShoppingActionObservedEvent>(Observe);
        mentorship ??= events.Subscribe<CareerMentorshipCompletedEvent>(Observe);
    }
    public void Dispose()
    {
        macro?.Dispose(); expedition?.Dispose(); abandoned?.Dispose(); substance?.Dispose(); shopping?.Dispose(); mentorship?.Dispose();
        macro = expedition = abandoned = substance = shopping = mentorship = null;
    }
    public static CharacterActivityEvent Project(in CharacterMacroActionTransitionEvent value) => value.Transition switch
    {
        CharacterMacroActionTransition.ComplaintExpressed => CharacterActivityEvent.Create(
            CharacterActivityKinds.Social, CharacterActivityOutcomes.Responded,
            $"불만을 표현함: {value.Reason}", actionId: "macro:complain", reasonCode: value.Reason,
            sentiment: -0.65f, bubbleEligible: true),
        CharacterMacroActionTransition.DepartureSelected => CharacterActivityEvent.Create(
            CharacterActivityKinds.Lifecycle, CharacterActivityOutcomes.Departed,
            $"던전을 떠나기로 함: {value.Reason}", actionId: "macro:exit-dungeon", reasonCode: value.Reason,
            sentiment: -0.8f, bubbleEligible: true),
        CharacterMacroActionTransition.FacilityVandalized => CharacterActivityEvent.Facility(
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Damaged,
            $"{BuildingLabel(value.Facility)}을 파손했다", value.Facility,
            actionId: "macro:vandalize", reasonCode: value.Reason, value: 1f, bubbleEligible: true),
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
    public static CharacterActivityEvent Project(in CharacterExpeditionPresenceChangedEvent value) =>
        CharacterActivityEvent.Create(CharacterActivityKinds.Lifecycle,
            value.OnExpedition ? CharacterActivityOutcomes.Departed : CharacterActivityOutcomes.Returned,
            value.OnExpedition ? "원정 출발" : "원정 복귀", actionId: "expedition",
            sentiment: value.OnExpedition ? 0.15f : 0.55f);
    public static CharacterActivityEvent Project(in CharacterInterruptedWorkAbandonedEvent value) =>
        CharacterActivityEvent.Work(value.WorkType, CharacterActivityOutcomes.Cancelled,
            value.Target == null ? "비상 중단 중 원래 작업 대상이 사라져 작업을 폐기하고 다시 계획합니다."
                : $"비상 중단 작업을 복귀하지 못해 다시 계획합니다: {value.RestoreFailure}",
            value.Target, reasonCode: value.ReasonCode);

    private static string BuildingLabel(BuildableObject building) =>
        building == null ? "None"
            : building.BuildingData != null && !string.IsNullOrWhiteSpace(building.BuildingData.objectName)
                ? building.BuildingData.objectName : building.name;
    public static CharacterActivityEvent Project(in SubstanceUseCompletedEvent value) =>
        CharacterActivityEvent.FreeText($"{value.DisplayName}을 복용했다.");
    public static CharacterActivityEvent Project(in CareerMentorshipCompletedEvent value) =>
        CharacterActivityEvent.FreeText(
            value.IsMentor
                ? $"{value.CounterpartName}에게 {value.ProficiencyId} 숙련을 가르쳤다."
                : $"{value.CounterpartName}에게 {value.ProficiencyId} 숙련을 배웠다.");
    public static CharacterActivityEvent Project(in ShoppingActionObservedEvent value) =>
        value.Transition switch
        {
            ShoppingActionTransition.MissingDestination =>
                CharacterActivityEvent.Create(
                    CharacterActivityKinds.Shopping,
                    CharacterActivityOutcomes.Failed,
                    "쇼핑 실패: 목적지 없음",
                    actionId: "shopping:visit",
                    reasonCode: "missing-destination",
                    sentiment: -0.7f,
                    bubbleEligible: true),
            ShoppingActionTransition.MissingMovementContext =>
                CharacterActivityEvent.Facility(
                    CharacterActivityKinds.Shopping,
                    CharacterActivityOutcomes.Failed,
                    "쇼핑 실패: 이동 정보 없음",
                    value.Facility,
                    actionId: "shopping:visit",
                    reasonCode: "missing-movement-context",
                    bubbleEligible: true),
            ShoppingActionTransition.DestinationUnreachable =>
                CharacterActivityEvent.Facility(
                    CharacterActivityKinds.Shopping,
                    CharacterActivityOutcomes.Failed,
                    "쇼핑 실패: 목적지 도달 실패",
                    value.Facility,
                    actionId: "shopping:visit",
                    reasonCode: "destination-unreachable",
                    bubbleEligible: true),
            ShoppingActionTransition.LooseItemStolen =>
                CharacterActivityEvent.Create(
                    CharacterActivityKinds.Shopping,
                    CharacterActivityOutcomes.Damaged,
                    TheftDetail(value),
                    actionId: "crime:floor-theft",
                    targetId: value.ItemStackId,
                    targetName: ItemName(value),
                    reasonCode: "floor-theft",
                    value: value.ItemValue,
                    quantity: 1,
                    sentiment: -0.5f,
                    bubbleEligible: true),
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        };
    private static string ItemName(in ShoppingActionObservedEvent value) =>
        !string.IsNullOrWhiteSpace(value.ItemDisplayName)
            ? value.ItemDisplayName
            : "item";
    private static string TheftDetail(in ShoppingActionObservedEvent value) =>
        $"{value.ActorName} pocketed {ItemName(value)} from the floor.";
    private static void Observe(SubstanceUseCompletedEvent value) =>
        value.Actor.LogComponent?.AddActivity(Project(value));
    private static void Observe(CareerMentorshipCompletedEvent value) =>
        value.Actor.LogComponent?.AddActivity(Project(value));
    private static void Observe(CharacterMacroActionTransitionEvent value) => value.Actor.AddActivity(Project(value));
    // Lifecycle formerly wrote only the display log; do not introduce a second
    // narrative/trait reaction by routing this through CharacterActor.AddActivity.
    private static void Observe(CharacterExpeditionPresenceChangedEvent value) => value.Actor.LogComponent?.AddActivity(Project(value));
    private static void Observe(CharacterInterruptedWorkAbandonedEvent value) => value.Actor.AddActivity(Project(value));
    private void Observe(ShoppingActionObservedEvent value)
    {
        value.Actor.AddActivity(Project(value));
        if (value.Transition == ShoppingActionTransition.LooseItemStolen)
        {
            events.RaiseAlert(
                "바닥 물품 도난",
                TheftDetail(value),
                EventAlertImportance.Medium,
                "범죄");
        }
    }
}
