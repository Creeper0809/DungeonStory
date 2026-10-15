using System;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects applied survival work and deprivation breakdowns into activity feeds.
/// It does not own work, needs, mood, or breakdown state.
/// </summary>
public sealed class SurvivalStateActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable workSubscription;
    private IDisposable breakdownSubscription;

    public SurvivalStateActivityObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize()
    {
        if (workSubscription != null)
        {
            return;
        }

        workSubscription =
            events.Subscribe<SurvivalFacilityWorkResultEvent>(Observe);
        breakdownSubscription =
            events.Subscribe<DeprivationBreakdownStartedEvent>(Observe);
    }

    public void Dispose()
    {
        workSubscription?.Dispose();
        workSubscription = null;
        breakdownSubscription?.Dispose();
        breakdownSubscription = null;
    }

    public static BuildingActivitySnapshot Project(
        in SurvivalFacilityWorkResultEvent value)
    {
        string buildingName =
            SurvivalFacilityWorkRules.GetBuildingName(value.Facility);
        return value.Kind switch
        {
            SurvivalFacilityWorkResultKind.WaterDrawing =>
                CreateWork(
                    BuiltInWorkTypeIds.DrawWater,
                    value.Quantity > 0
                        ? BuildingActivityOutcomes.Completed
                        : BuildingActivityOutcomes.Failed,
                    value.Quantity > 0
                        ? $"{buildingName}에서 물 {value.Quantity}개를 길었다."
                        : "물을 담을 곳을 찾지 못했다.",
                    value.Quantity > 0
                        ? "water-drawn"
                        : "water-output-failed",
                    value.Quantity,
                    value.Quantity <= 0),
            SurvivalFacilityWorkResultKind.Cooking =>
                CreateWork(
                    BuiltInWorkTypeIds.Cook,
                    BuildingActivityOutcomes.Completed,
                    value.PreservedFood
                        ? $"{buildingName}에서 오래 둘 수 있는 보존 식량을 만들었다."
                        : $"{buildingName}에서 따뜻한 식사를 만들었다.",
                    value.PreservedFood ? "food-preserved" : "food-cooked",
                    value.Quantity,
                    false),
            SurvivalFacilityWorkResultKind.Refueling =>
                CreateWork(
                    BuiltInWorkTypeIds.Refuel,
                    BuildingActivityOutcomes.Completed,
                    $"{buildingName} refueled.",
                    "survival-refueled",
                    value.Quantity,
                    false),
            SurvivalFacilityWorkResultKind.DetoxTreatment =>
                CreateWork(
                    BuiltInWorkTypeIds.Treat,
                    BuildingActivityOutcomes.Completed,
                    $"{buildingName}에서 {SurvivalFacilityWorkRules.GetActorName(value.Patient, value.PatientId)}의 독성 부담을 치료했다.",
                    "survival-detox-treated",
                    1,
                    false),
            SurvivalFacilityWorkResultKind.Treatment =>
                CreateWork(
                    BuiltInWorkTypeIds.Treat,
                    BuildingActivityOutcomes.Completed,
                    $"{buildingName}에서 {SurvivalFacilityWorkRules.GetActorName(value.Patient, value.PatientId)}의 상태를 살폈다.",
                    "survival-treated",
                    0,
                    false),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value.Kind,
                "Unknown survival work result kind.")
        };
    }

    public static CharacterActivityEvent Project(
        in DeprivationBreakdownStartedEvent value) =>
        CharacterActivityEvent.Create(
            CharacterActivityKinds.Health,
            CharacterActivityOutcomes.Started,
            GetBreakdownLabel(value.Kind),
            actionId: "survival/breakdown",
            reasonCode: value.Cause.ToString(),
            sentiment: -1f,
            bubbleEligible: true);

    private void Observe(SurvivalFacilityWorkResultEvent value) =>
        value.Worker?.RecordActivity(value.Facility, Project(value));

    private void Observe(DeprivationBreakdownStartedEvent value) =>
        value.Actor.AddActivity(Project(value));

    private static BuildingActivitySnapshot CreateWork(
        WorkTypeId workTypeId,
        string outcomeId,
        string factText,
        string reasonCode,
        int quantity,
        bool bubbleEligible) =>
        new BuildingActivitySnapshot(
            BuildingActivityKinds.Work,
            outcomeId,
            factText,
            workTypeId.Value,
            string.Empty,
            reasonCode,
            0f,
            quantity,
            bubbleEligible);

    private static string GetBreakdownLabel(CharacterBreakdownKind kind) =>
        kind switch
        {
            CharacterBreakdownKind.DesperateRelief => "배변 붕괴",
            CharacterBreakdownKind.DesperateDrink => "갈증 붕괴",
            CharacterBreakdownKind.DesperateEat => "굶주림 붕괴",
            CharacterBreakdownKind.Collapse => "탈진 실신",
            CharacterBreakdownKind.ViolentImpulse => "정신 붕괴",
            _ => "붕괴"
        };
}
