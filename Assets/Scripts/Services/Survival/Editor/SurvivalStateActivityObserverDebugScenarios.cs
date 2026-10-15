#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class SurvivalStateActivityObserverDebugScenarios
{
    public static void RunOrThrow()
    {
        GameObject actorObject = null;
        CharacterSO actorData = null;
        try
        {
            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "Survival State Activity Observer Actor");
            CharacterActor actor = actorObject.GetComponent<CharacterActor>();
            actorData = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    "생존 활동 관찰자",
                    "Orc");
            actor.EnsureRuntimeState();
            actor.Initialization(actorData);
            actor.Identity.SetPersistentId(
                "character:survival-state-activity-observer");
            actor.name = "생존 활동 관찰자";

            VerifyWorkProjections(actor);
            VerifyBreakdownProjections(actor);
            VerifyObserverLifecycle(actor);
        }
        finally
        {
            if (actorData != null)
                Object.DestroyImmediate(actorData);
            if (actorObject != null)
                Object.DestroyImmediate(actorObject);
        }
    }

    private static void VerifyWorkProjections(CharacterActor patient)
    {
        RequireWork(
            SurvivalStateActivityObserver.Project(new
                SurvivalFacilityWorkResultEvent(
                    SurvivalFacilityWorkResultKind.WaterDrawing,
                    null,
                    null,
                    2)),
            BuiltInWorkTypeIds.DrawWater,
            BuildingActivityOutcomes.Completed,
            "시설에서 물 2개를 길었다.",
            "water-drawn",
            2,
            bubbleEligible: false);
        RequireWork(
            SurvivalStateActivityObserver.Project(new
                SurvivalFacilityWorkResultEvent(
                    SurvivalFacilityWorkResultKind.WaterDrawing,
                    null,
                    null,
                    0)),
            BuiltInWorkTypeIds.DrawWater,
            BuildingActivityOutcomes.Failed,
            "물을 담을 곳을 찾지 못했다.",
            "water-output-failed",
            0,
            bubbleEligible: true);
        RequireWork(
            SurvivalStateActivityObserver.Project(new
                SurvivalFacilityWorkResultEvent(
                    SurvivalFacilityWorkResultKind.Cooking,
                    null,
                    null,
                    3)),
            BuiltInWorkTypeIds.Cook,
            BuildingActivityOutcomes.Completed,
            "시설에서 따뜻한 식사를 만들었다.",
            "food-cooked",
            3,
            bubbleEligible: false);
        RequireWork(
            SurvivalStateActivityObserver.Project(new
                SurvivalFacilityWorkResultEvent(
                    SurvivalFacilityWorkResultKind.Cooking,
                    null,
                    null,
                    4,
                    preservedFood: true)),
            BuiltInWorkTypeIds.Cook,
            BuildingActivityOutcomes.Completed,
            "시설에서 오래 둘 수 있는 보존 식량을 만들었다.",
            "food-preserved",
            4,
            bubbleEligible: false);
        RequireWork(
            SurvivalStateActivityObserver.Project(new
                SurvivalFacilityWorkResultEvent(
                    SurvivalFacilityWorkResultKind.Refueling,
                    null,
                    null,
                    5)),
            BuiltInWorkTypeIds.Refuel,
            BuildingActivityOutcomes.Completed,
            "시설 refueled.",
            "survival-refueled",
            5,
            bubbleEligible: false);
        RequireWork(
            SurvivalStateActivityObserver.Project(new
                SurvivalFacilityWorkResultEvent(
                    SurvivalFacilityWorkResultKind.DetoxTreatment,
                    null,
                    null,
                    9,
                    patient: patient,
                    patientId: "ignored-patient-id")),
            BuiltInWorkTypeIds.Treat,
            BuildingActivityOutcomes.Completed,
            "시설에서 생존 활동 관찰자의 독성 부담을 치료했다.",
            "survival-detox-treated",
            1,
            bubbleEligible: false);
        RequireWork(
            SurvivalStateActivityObserver.Project(new
                SurvivalFacilityWorkResultEvent(
                    SurvivalFacilityWorkResultKind.Treatment,
                    null,
                    null,
                    9,
                    patient: null,
                    patientId: "character:survival-patient-fallback")),
            BuiltInWorkTypeIds.Treat,
            BuildingActivityOutcomes.Completed,
            "시설에서 character:survival-patient-fallback의 상태를 살폈다.",
            "survival-treated",
            0,
            bubbleEligible: false);
    }

    private static void VerifyBreakdownProjections(CharacterActor actor)
    {
        (CharacterBreakdownKind Kind, string Label)[] cases =
        {
            (CharacterBreakdownKind.DesperateRelief, "배변 붕괴"),
            (CharacterBreakdownKind.DesperateDrink, "갈증 붕괴"),
            (CharacterBreakdownKind.DesperateEat, "굶주림 붕괴"),
            (CharacterBreakdownKind.Collapse, "탈진 실신"),
            (CharacterBreakdownKind.ViolentImpulse, "정신 붕괴"),
            (CharacterBreakdownKind.None, "붕괴")
        };
        foreach ((CharacterBreakdownKind kind, string label) in cases)
        {
            CharacterActivityEvent projected =
                SurvivalStateActivityObserver.Project(new
                    DeprivationBreakdownStartedEvent(
                        actor,
                        kind,
                        DeprivationKind.Hunger));
            Require(
                projected != null
                && projected.KindId == CharacterActivityKinds.Health
                && projected.OutcomeId == CharacterActivityOutcomes.Started
                && projected.FactText == label
                && projected.ActionId == "survival/breakdown"
                && projected.ReasonCode == DeprivationKind.Hunger.ToString()
                && projected.Value == 0f
                && projected.Quantity == 0
                && projected.Sentiment == -1f
                && projected.NarrativeEligible
                && projected.BubbleEligible
                && projected.VisibleToPlayer,
                "A deprivation breakdown activity projection changed: "
                + kind);
        }
    }

    private static void VerifyObserverLifecycle(CharacterActor actor)
    {
        var events = new GameEventBus();
        IBuildingVisitorPort visitor = VisitorProbe.Create(out VisitorProbe probe);
        var added = new List<CharacterLogEntry>();
        void Capture(CharacterLogEntry entry) => added.Add(entry);
        actor.OnLogAdded += Capture;
        var observer = new SurvivalStateActivityObserver(events);
        try
        {
            observer.Initialize();
            observer.Initialize();
            events.Publish(new SurvivalFacilityWorkResultEvent(
                SurvivalFacilityWorkResultKind.WaterDrawing,
                visitor,
                null,
                2));
            Require(
                probe.RecordCount == 1
                && probe.LastFacility == null,
                "Repeated initialization duplicated a survival work activity.");
            RequireWork(
                probe.LastActivity,
                BuiltInWorkTypeIds.DrawWater,
                BuildingActivityOutcomes.Completed,
                "시설에서 물 2개를 길었다.",
                "water-drawn",
                2,
                bubbleEligible: false);

            observer.Dispose();
            events.Publish(new SurvivalFacilityWorkResultEvent(
                SurvivalFacilityWorkResultKind.Cooking,
                visitor,
                null,
                3));
            events.Publish(new DeprivationBreakdownStartedEvent(
                actor,
                CharacterBreakdownKind.Collapse,
                DeprivationKind.Exhaustion));
            Require(
                probe.RecordCount == 1
                && added.Count == 0,
                "A disposed survival activity observer remained subscribed.");

            observer.Initialize();
            events.Publish(new SurvivalFacilityWorkResultEvent(
                SurvivalFacilityWorkResultKind.Cooking,
                visitor,
                null,
                4,
                preservedFood: true));
            events.Publish(new DeprivationBreakdownStartedEvent(
                actor,
                CharacterBreakdownKind.Collapse,
                DeprivationKind.Exhaustion));
            Require(
                probe.RecordCount == 2
                && added.Count == 1,
                "A reinitialized survival activity observer did not subscribe once.");
            RequireWork(
                probe.LastActivity,
                BuiltInWorkTypeIds.Cook,
                BuildingActivityOutcomes.Completed,
                "시설에서 오래 둘 수 있는 보존 식량을 만들었다.",
                "food-preserved",
                4,
                bubbleEligible: false);
            CharacterActivityEvent actual = added[0].Activity;
            Require(
                actual != null
                && actual.KindId == CharacterActivityKinds.Health
                && actual.OutcomeId == CharacterActivityOutcomes.Started
                && actual.FactText == "탈진 실신"
                && actual.ActionId == "survival/breakdown"
                && actual.ReasonCode == DeprivationKind.Exhaustion.ToString()
                && actual.Sentiment == -1f
                && actual.BubbleEligible,
                "The real event-bus breakdown activity changed.");
        }
        finally
        {
            observer.Dispose();
            actor.OnLogAdded -= Capture;
        }
    }

    private static void RequireWork(
        BuildingActivitySnapshot activity,
        WorkTypeId workType,
        string outcome,
        string text,
        string reason,
        int quantity,
        bool bubbleEligible)
    {
        Require(
            activity.KindId == BuildingActivityKinds.Work
            && activity.OutcomeId == outcome
            && activity.FactText == text
            && activity.WorkTypeId == workType.Value
            && activity.ActionId.Length == 0
            && activity.ReasonCode == reason
            && activity.Value == 0f
            && activity.Quantity == quantity
            && activity.Sentiment == 0f
            && activity.BubbleEligible == bubbleEligible
            && activity.AbsoluteDay == 0,
            "A survival facility-work activity projection changed: "
            + reason);
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException(failure);
    }

    public class VisitorProbe : DispatchProxy
    {
        public VisitorProbe()
        {
        }

        internal int RecordCount { get; private set; }
        internal IBuildingWorldEntryPort LastFacility { get; private set; }
        internal BuildingActivitySnapshot LastActivity { get; private set; }

        internal static IBuildingVisitorPort Create(out VisitorProbe probe)
        {
            IBuildingVisitorPort value =
                DispatchProxy.Create<IBuildingVisitorPort, VisitorProbe>();
            probe = (VisitorProbe)(object)value;
            return value;
        }

        protected override object Invoke(
            MethodInfo targetMethod,
            object[] args)
        {
            if (targetMethod?.Name == nameof(IBuildingVisitorPort.RecordActivity)
                && args?.Length == 2
                && args[1] is BuildingActivitySnapshot activity)
            {
                RecordCount++;
                LastFacility = args[0] as IBuildingWorldEntryPort;
                LastActivity = activity;
                return null;
            }

            throw new InvalidOperationException(
                "Unexpected survival activity visitor call: "
                + (targetMethod?.Name ?? "<null>"));
        }
    }
}
#endif
