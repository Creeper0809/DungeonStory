#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

// Small controlled query fixture plus the actual daily application owner.
// It does not claim natural AI, full-world restore, or every authored choice execution.
public static class CampaignPresentationObserverDebugScenarios
{
    public static void RunForBatch()
    {
        Require(Application.isBatchMode, "B128 requires the isolated batch runner.");
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            GuestDeliveryPresentationDebugScenarios.RunForBatch();
            Debug.Log("B128_GUEST_DELIVERY_PROJECTION_LIFETIME=PASS");
            WorkAccidentPresentationDebugScenarios.RunForBatch();
            Debug.Log("B128_WORK_ACCIDENT_PRESENTATION=PASS");
            using var context = new Context();
            VerifyCampaignLifetime(context);
            VerifyFrozenCrisis(context);
            VerifyActualDailyOwner(context);
            Require(context.UnexpectedCalls.Count == 0,
                "Unexpected query/command calls: " + string.Join(", ", context.UnexpectedCalls));
            Debug.Log("B128_CAMPAIGN_PRESENTATION_OBSERVERS=PASS");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static void VerifyCampaignLifetime(Context context)
    {
        context.Observer.Initialize();
        context.Observer.Initialize();
        context.Events.Publish(new CampaignDailyEvaluatedFact(3, true));
        Require(context.Alerts.Count == 1, "Duplicate campaign subscriptions.");
        EventAlertRequest alert = context.Alerts.Single();
        ServiceIncidentDefinitionSO definition = context.Definition;
        Require(alert.SourceId == context.Active.instanceId
            && alert.Title == definition.DisplayName
            && alert.Category == "V21 사회 사건"
            && alert.Importance == EventAlertImportance.Medium
            && alert.Detail.Contains(definition.Description)
            && alert.Detail.Contains("기한: 9일")
            && alert.Detail.Contains("위험 근거: b128-known-risk")
            && alert.Choices.Select(value => value.ActionId).SequenceEqual(
                definition.responses.Select(value =>
                    V21ContentAlertActionIds.Society(context.Active.instanceId, value.choiceId))),
            "Society text, risk or command routing changed.");
        var incident = new CampaignIncidentCapturedFact(new ObservedMealIncidentCaptureResult(
            ObservedMealIncidentCaptureDisposition.Created, definition.kind,
            context.Active.instanceId, default), 3);
        context.Events.Publish(incident);
        Require(context.Alerts.Count == 2, "Captured incident did not reach observer.");
        context.Observer.Dispose(); context.Observer.Dispose();
        context.Events.Publish(incident);
        context.Events.Publish(new CampaignDailyEvaluatedFact(3, false));
        Require(context.Alerts.Count == 2, "Disposed campaign subscriptions emitted.");
        context.Observer.Initialize();
        context.Events.Publish(incident);
        Require(context.Alerts.Count == 3, "Reinitialized observer did not receive incident.");
        Debug.Log("B128_CAMPAIGN_ACTIONS_LIFETIME=PASS");
    }

    private static void VerifyFrozenCrisis(Context context)
    {
        var axes = new List<EndlessCrisisAxisModifierSnapshot>
        {
            new(EndlessCrisisAxis.Climate, "검증 압력", "axis:b128", 1.25f)
        };
        var fact = new EndlessCrisisEvaluatedFact(7, Crisis(EndlessCrisisLifecyclePhase.Active, axes));
        axes.Clear();
        Require(fact.Crisis.Axes.Count == 1, "Crisis axes were not frozen.");
        context.Events.Publish(fact);
        EventAlertRequest active = context.Alerts.Last();
        Require(active.SourceId == "crisis:b128" && !active.IsResolved
            && active.Importance == EventAlertImportance.High
            && active.Detail.Contains("검증 압력 125%")
            && active.Detail.Contains("압력 종료까지 3일"),
            "Frozen crisis identity, multiplier or observed day changed.");
        foreach (EndlessCrisisLifecyclePhase phase in new[]
        {
            EndlessCrisisLifecyclePhase.AwaitingDirectResponse,
            EndlessCrisisLifecyclePhase.Recovery, EndlessCrisisLifecyclePhase.None
        })
        {
            context.Events.Publish(new EndlessCrisisEvaluatedFact(12, Crisis(phase, fact.Crisis.Axes)));
            EventAlertRequest output = context.Alerts.Last();
            Require(output.IsResolved == (phase == EndlessCrisisLifecyclePhase.None),
                "Crisis lifecycle resolution changed.");
            if (phase == EndlessCrisisLifecyclePhase.Recovery)
                Require(output.Detail.Contains("남은 수복 창 3일"), "Recovery observed day changed.");
            if (phase == EndlessCrisisLifecyclePhase.None)
                Require(output.ResultSummary.Contains("마지막 실제 종료일: 11"), "Terminal evidence changed.");
        }
        context.Observer.Dispose();
        int count = context.Alerts.Count;
        context.Events.Publish(fact);
        Require(context.Alerts.Count == count, "Disposed crisis subscription emitted.");
        context.Observer.Initialize();
        Debug.Log("B128_ENDLESS_FROZEN_PHASES=PASS");
    }

    private static EndlessCrisisSnapshot Crisis(EndlessCrisisLifecyclePhase phase,
        IReadOnlyList<EndlessCrisisAxisModifierSnapshot> axes) =>
        new(phase, "crisis:b128", axes, 2, 10, 11, 15, 12, 4, "", "");

    private static void VerifyActualDailyOwner(Context context)
    {
        var runtime = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), context.Catalog);
        var resolution = new FailingDailyResolution();
        int retirements = 0;
        ICareerService careers = context.Port<ICareerService>((method, args) =>
        {
            if (method.Name != "CompleteDueRetirements") return context.Unexpected(method);
            retirements++;
            Require((int)args[0] is 3 or 4, "Daily owner passed the wrong date.");
            return 0;
        });
        using var owner = new V20CampaignApplicationAdapter(resolution,
            runtime, runtime, runtime, runtime,
            context.Port<IGameCalendar>(),
            context.Port<IClimateQuery>((method, _) =>
                method.Name == "get_WeatherFrontId" ? "weather:b128" : context.Unexpected(method)),
            context.Port<IRunSeedProvider>((method, _) =>
                method.Name == "get_RunSeed" ? 128 : context.Unexpected(method)),
            context.Events, context.Milestones, context.Narratives, context.NarrativeCatalog,
            context.Port<ICharacterCultureGameplayQuery>(), context.Society, runtime,
            context.Catalog, context.Life, careers, context.Facilities,
            context.Port<ICharacterBodyHealthQuery>(), context.Port<ICharacterConsumablesApplication>(),
            context.Port<ICharacterMedicalCommand>(), context.Port<ICharacterMedicalQuery>(),
            context.Port<ICharacterLifetimeQuery>(), () => throw new InvalidOperationException("Unexpected exterior command."),
            new SocietyResponseMovementProvenanceRuntime());
        var daily = new List<CampaignDailyEvaluatedFact>();
        var crisis = new List<EndlessCrisisEvaluatedFact>();
        using var dailyCapture = context.Events.Subscribe<CampaignDailyEvaluatedFact>(daily.Add);
        using var crisisCapture = context.Events.Subscribe<EndlessCrisisEvaluatedFact>(crisis.Add);
        int faultCalls = 0;
        using (context.Events.Subscribe<EventAlertRequestedEvent>(_ =>
        {
            faultCalls++;
            throw new InvalidOperationException("b128-deliberate-campaign-alert-subscriber-failure");
        }))
        {
            owner.Start(); owner.Start();
            context.Events.Publish(new OperatingDayStartedEvent(3));
        }
        Require(faultCalls == 1 && retirements == 1 && resolution.Calls == 1
            && daily.Count == 1 && !daily[0].Succeeded && daily[0].AbsoluteDay == 3
            && crisis.Count == 1 && crisis[0].AbsoluteDay == 3
            && !owner.LastDailyEvaluationSucceeded && owner.LastDailyEvaluationFailure.IsFailure,
            "Presentation failure changed or interrupted actual daily orchestration.");
        context.Observer.Dispose();
        int alertsBefore = context.Alerts.Count;
        context.Events.Publish(new OperatingDayStartedEvent(4));
        Require(retirements == 2 && resolution.Calls == 2
            && daily.Count == 2 && crisis.Count == 2
            && context.Alerts.Count == alertsBefore,
            "Daily owner depended on the presentation observer.");
        owner.Dispose();
        context.Events.Publish(new OperatingDayStartedEvent(4));
        Require(resolution.Calls == 2, "Disposed daily application retained its subscription.");
        Debug.Log("B128_ACTUAL_DAILY_OWNER_INDEPENDENCE=PASS");
    }

    private sealed class FailingDailyResolution : IContentResolutionService
    {
        public int Calls { get; private set; }
        public bool TryExecute(ContentResolutionRequest request,
            out ContentResolutionResult result, out DomainFailure failure)
        {
            Require(request.Kind == ContentResolutionRequestKind.DailyEvaluation, "Unexpected content command.");
            Calls++;
            result = null;
            failure = new DomainFailure(FailureCode.ExternalInfluenceUnavailable, "qa:b128:daily-failure");
            return false;
        }
    }

    private sealed class Context : IDisposable
    {
        public readonly GameEventBus Events = new();
        public readonly List<EventAlertRequest> Alerts = new();
        public readonly List<string> UnexpectedCalls = new();
        public readonly V20StoryContentCatalog Catalog =
            new(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        public readonly V20ActiveEventSaveData Active;
        public readonly ServiceIncidentDefinitionSO Definition;
        public readonly IV20MilestoneWorldSnapshotQuery Milestones;
        public readonly ICharacterNarrativeQuery Narratives;
        public readonly ICharacterNarrativeCatalog NarrativeCatalog;
        public readonly ISocietyEventQuery Society;
        public readonly ICharacterLifeQuery Life;
        public readonly IFacilityCapabilityQuery Facilities;
        public readonly CampaignPresentationObserver Observer;
        private readonly IDisposable capture;

        public Context()
        {
            Definition = Catalog.ServiceIncidents.First();
            Active = new V20ActiveEventSaveData
            {
                instanceId = "incident:b128", definitionId = Definition.StableId,
                riskTier = ExperienceEventRiskTier.Recoverable, riskReason = "b128-known-risk",
                deadlineAbsoluteDay = 9
            };
            Milestones = Port<IV20MilestoneWorldSnapshotQuery>((method, args) => method.Name switch
            {
                "get_LivingCharacters" => Array.Empty<CharacterActor>(),
                "Build" => new RunMilestoneEvaluationSnapshot { AbsoluteDay = (int)args[0] },
                _ => Unexpected(method)
            });
            Narratives = Port<ICharacterNarrativeQuery>();
            NarrativeCatalog = EmptyQuery<ICharacterNarrativeCatalog>("get_Practices");
            Society = Port<ISocietyEventQuery>((method, _) =>
                method.Name == "get_ActiveSocietyEvents" ? new[] { Active } : Unexpected(method));
            Life = EmptyQuery<ICharacterLifeQuery>("get_Records");
            Facilities = EmptyQuery<IFacilityCapabilityQuery>("FindOperational");
            Observer = new CampaignPresentationObserver(Events, Milestones, Narratives, NarrativeCatalog,
                EmptyQuery<ISeasonalEventQuery>("get_ActiveSeasonalEvents"), Society,
                EmptyQuery<IFactionCampaignQuery>("get_Factions"), Catalog,
                EmptyQuery<IReproductionService>("get_Processes"), Life, Facilities,
                EmptyQuery<IKinshipQuery>("get_Tombstones"), Port<IGriefTraumaService>(),
                Port<IPopulationHealthQuery>(), Port<IDiseaseDefinitionCatalog>());
            capture = Events.Subscribe<EventAlertRequestedEvent>(value => Alerts.Add(value.request));
        }

        public T EmptyQuery<T>(params string[] allowedMethods) where T : class => Port<T>((method, _) =>
        {
            if (!allowedMethods.Contains(method.Name) || !method.ReturnType.IsGenericType)
                return Unexpected(method);
            return Array.CreateInstance(method.ReturnType.GetGenericArguments().Single(), 0);
        });

        public T Port<T>(Func<MethodInfo, object[], object> invoke = null) where T : class
        {
            T value = DispatchProxy.Create<T, StrictPort>();
            ((StrictPort)(object)value).InvokeMethod = invoke ?? ((method, _) => Unexpected(method));
            return value;
        }

        public object Unexpected(MethodInfo method)
        {
            string name = method.DeclaringType?.Name + "." + method.Name;
            UnexpectedCalls.Add(name);
            throw new InvalidOperationException("Unexpected fixture call: " + name);
        }

        public void Dispose() { Observer.Dispose(); capture.Dispose(); }
    }

    public class StrictPort : DispatchProxy
    {
        public Func<MethodInfo, object[], object> InvokeMethod;
        protected override object Invoke(MethodInfo method, object[] args) => InvokeMethod(method, args);
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition) throw new InvalidOperationException(failure);
    }
}
#endif
