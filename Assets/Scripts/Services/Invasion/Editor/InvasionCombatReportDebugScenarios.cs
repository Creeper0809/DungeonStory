using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class InvasionCombatReportDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Invasion/Run P1 Combat Report Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 invasion combat report scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("전투 발동 피드와 결과 요약", VerifyCombatFeedbackAndSummary, errors);
        RunScenario("추천 대응 미표시", VerifySummaryHasNoRecommendation, errors);
        RunScenario("완료 보고서 씬 참조 격리", VerifyCompletedReportSceneIsolation, errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("P1 invasion combat report scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }

    private static bool VerifyCombatFeedbackAndSummary()
    {
        using CombatReportScenarioWorld world = new CombatReportScenarioWorld();
        CountingCombatReportListener reports =
            new CountingCombatReportListener(world.EventBus);
        CountingEventAlertRequestListener alerts =
            new CountingEventAlertRequestListener(world.EventBus);
        int feedbackCount = 0;
        void CountFeedback(string message, DefenseActivationSnapshot report)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                feedbackCount++;
            }
        }

        world.Runtime.Feedback += CountFeedback;

        world.StartInvasion();
        DefenseActivationReport spikeReport = world.CreateDefenseReport(
            "가시 함정",
            DefenseAttackConcept.Physical,
            18f,
            0f,
            "가시 피해");
        world.TriggerDefense(spikeReport);

        DefenseActivationReport iceReport = world.CreateDefenseReport(
            "냉기 분사구",
            DefenseAttackConcept.Ice,
            8f,
            1.5f,
            "감속");
        world.TriggerDefense(iceReport);

        BuildableObject damaged = world.CreateFacility("저가 음식점", DefenseAttackConcept.None);
        damaged.SetDamaged(true);
        world.TriggerFacilityDamaged(damaged);
        world.Resolve(true, 1f);

        InvasionCombatReportSnapshot report = reports.LastReport;
        EventAlertRequest alert = alerts.LastRequest;
        string detail = report != null ? report.ToDetailText() : string.Empty;

        bool valid = reports.Count == 1
            && feedbackCount == 2
            && alert != null
            && alert.Title == "침입 결과"
            && alert.Importance == EventAlertImportance.Medium
            && detail.Contains("가장 많은 피해를 준 시설: 가시 함정")
            && detail.Contains("가장 오래 지연시킨 시설: 냉기 분사구")
            && detail.Contains("피해를 입은 시설: 저가 음식점")
            && detail.Contains("파손 시설: 저가 음식점")
            && detail.Contains("획득한 관찰 정보")
            && detail.Contains("전투 중 발동");

        reports.Dispose();
        world.Runtime.Feedback -= CountFeedback;
        alerts.Dispose();
        return valid;
    }

    private static bool VerifySummaryHasNoRecommendation()
    {
        using CombatReportScenarioWorld world = new CombatReportScenarioWorld();
        CountingCombatReportListener reports =
            new CountingCombatReportListener(world.EventBus);

        world.StartInvasion();
        DefenseActivationReport guardReport = world.CreateDefenseReport(
            "경비실",
            DefenseAttackConcept.Guard,
            12f,
            0f,
            "경비 교전");
        world.TriggerDefense(guardReport);
        world.TriggerFinalCombat();
        world.Resolve(false, 5f);

        string detail = reports.LastReport != null ? reports.LastReport.ToDetailText() : string.Empty;
        bool valid = detail.Contains("방어 결과: 방어 실패")
            && detail.Contains("결정적 방어")
            && !detail.Contains("추천")
            && !detail.Contains("건설하세요")
            && !detail.Contains("연구하세요");

        reports.Dispose();
        return valid;
    }

    private static bool VerifyCompletedReportSceneIsolation()
    {
        InvasionCombatReportSnapshot report;
        using (CombatReportScenarioWorld world = new CombatReportScenarioWorld())
        {
            CountingCombatReportListener reports =
                new CountingCombatReportListener(world.EventBus);
            world.StartInvasion();
            DefenseActivationReport defense = world.CreateDefenseReport(
                "격리 함정",
                DefenseAttackConcept.Physical,
                5f,
                0f,
                "격리");
            world.TriggerDefense(defense);
            BuildableObject damaged = world.CreateFacility("격리 대상", DefenseAttackConcept.None);
            damaged.SetDamaged(true);
            world.TriggerFacilityDamaged(damaged);
            world.Resolve(true, 0f);
            report = reports.LastReport;
            reports.Dispose();
        }

        bool mutationRejected = false;
        if (report?.Observations is IList<string> observations && observations.Count > 0)
        {
            try
            {
                observations[0] = "mutated";
            }
            catch (NotSupportedException)
            {
                mutationRejected = true;
            }
        }

        return report != null
            && mutationRejected
            && report.DamagedFacilities.Count == 1
            && report.DamagedFacilities[0].Name == "격리 대상"
            && report.DefenseContributions.Count == 1
            && report.DefenseContributions[0].FacilityName == "격리 함정"
            && report.ToDetailText().Contains("격리 대상")
            && typeof(InvasionCombatReportSnapshot).GetProperty("Intruder") == null
            && typeof(InvasionFacilitySnapshot).GetProperty("Facility") == null;
    }

    private sealed class CombatReportScenarioWorld : IDisposable
    {
        private readonly List<Object> objects = new List<Object>();

        public CombatReportScenarioWorld()
        {
            EventBus = new DungeonStory.Foundation.GameEventBus();
            GameObject runtimeObject = new GameObject("CombatReportRuntime_Test");
            Runtime = runtimeObject.AddComponent<InvasionCombatReportRuntime>();
            Runtime.ConstructInvasionCombatReportRuntime(
                EventBus,
                new DungeonStory.Foundation.UnityGameClock());
            objects.Add(runtimeObject);

            Intruder = CreateCharacter("Test Intruder");
            Owner = CreateCharacter("Test Owner");
        }

        public InvasionCombatReportRuntime Runtime { get; }
        public DungeonStory.Foundation.IGameEventBus EventBus { get; }
        public CharacterActor Intruder { get; }
        public CharacterActor Owner { get; }

        public void StartInvasion()
        {
            InvasionThreatSnapshot snapshot = new InvasionThreatSnapshot(
                100f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(3f, 2f, 1f, 0f),
                0f,
                0f);
            Runtime.OnTriggerEvent(new InvasionStartedEvent(snapshot));
            Runtime.OnTriggerEvent(new InvasionSpawnedEvent(CharacterActor.From(Intruder), snapshot));
        }

        public void TriggerDefense(DefenseActivationReport report)
        {
            Runtime.OnTriggerEvent(new DefenseFacilityTriggeredEvent(report));
        }

        public void TriggerFacilityDamaged(BuildableObject facility)
        {
            Runtime.OnTriggerEvent(new InvasionFacilityDamagedEvent(CharacterActor.From(Intruder), facility));
        }

        public void TriggerFinalCombat()
        {
            Runtime.OnTriggerEvent(new InvasionFinalCombatStartedEvent(CharacterActor.From(Intruder), CharacterActor.From(Owner)));
        }

        public void Resolve(bool defended, float residualRisk)
        {
            Runtime.OnTriggerEvent(new InvasionResolvedEvent(defended, residualRisk));
        }

        public DefenseActivationReport CreateDefenseReport(
            string buildingName,
            DefenseAttackConcept concept,
            float damage,
            float delay,
            string effectTag)
        {
            DefenseFacility facility = CreateFacility(buildingName, concept) as DefenseFacility;
            DefenseActivationReport report = new DefenseActivationReport(
                facility,
                CharacterActor.From(Intruder),
                DefenseTriggerTiming.OnEnter);
            report.AddDamage(damage);
            report.AddMovementDelay(delay);
            report.AddEffectTag(effectTag);
            return report;
        }

        public BuildableObject CreateFacility(string buildingName, DefenseAttackConcept concept)
        {
            BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
            data.objectName = buildingName;
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = BuildingCategory.Special;
            data.type = typeof(DefenseFacility);
            data.Facility = new FacilityData
            {
                disabledWhenDamaged = true
            };
            data.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Repair });
            data.Defense = new DefenseFacilityData
            {
                enabled = concept != DefenseAttackConcept.None,
                concept = concept,
                triggerTimings = DefenseTriggerTiming.OnEnter,
                targetRule = DefenseTargetRule.EnteringIntruder,
                combatLogText = buildingName
            };
            objects.Add(data);

            GameObject buildingObject = new GameObject(buildingName);
            DefenseFacility facility = buildingObject.AddComponent<DefenseFacility>();
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(data, Vector2Int.zero);
            objects.Add(buildingObject);
            return facility;
        }

        public void Dispose()
        {
            for (int i = objects.Count - 1; i >= 0; i--)
            {
                if (objects[i] != null)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private CharacterActor CreateCharacter(string name)
        {
            GameObject characterObject = new GameObject(name);
            CharacterActor character = characterObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(characterObject);
            character.SetLifecycleState(CharacterLifecycleState.Active);
            objects.Add(characterObject);
            return character;
        }
    }

    private sealed class CountingCombatReportListener : IDisposable
    {
        private readonly IDisposable subscription;

        public int Count { get; private set; }
        public InvasionCombatReportSnapshot LastReport { get; private set; }

        public CountingCombatReportListener(
            DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription =
                gameEventBus.Subscribe<InvasionCombatReportReadyEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(InvasionCombatReportReadyEvent eventType)
        {
            Count++;
            LastReport = eventType.report;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }

    private sealed class CountingEventAlertRequestListener : IDisposable
    {
        private readonly IDisposable subscription;

        public EventAlertRequest LastRequest { get; private set; }

        public CountingEventAlertRequestListener(
            DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription =
                gameEventBus.Subscribe<EventAlertRequestedEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(EventAlertRequestedEvent eventType)
        {
            LastRequest = eventType.request;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}
