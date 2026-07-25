using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class InvasionThreatDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Invasion/Run P1 Threat Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P1 invasion threat scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("위협도 상승 요인 계산", VerifyThreatRiseFactors, errors);
        RunScenario("경고 이벤트와 알림", VerifyWarningAlert, errors);
        RunScenario("침입 후보 지연 이벤트", VerifyCandidateDelay, errors);
        RunScenario("침입 시작 후 초기화와 안전 시간", VerifyResetAndSafety, errors);
        RunScenario("Preparation days refresh initial safety", VerifyInitialSafety, errors);
        RunScenario("Structural pieces do not inflate dungeon value", VerifyStructuralValueExclusion, errors);

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
            Debug.Log("P1 invasion threat scenarios passed.");
        }

        return true;
    }

    public static string DescribeActiveSceneValue()
    {
        BuildableObject[] buildings = Object.FindObjectsByType<BuildableObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        int structures = buildings.Count(building => building != null
            && building.BuildingData != null
            && (building.BuildingData.IsWall
                || building.BuildingData.IsDoor
                || building.BuildingData.IsGridMovement));
        int valuables = buildings.Count(building => building != null
            && InvasionThreatValueCalculator.CalculateBuildingValue(building.BuildingData) > 0f);
        float value = InvasionThreatValueCalculator.CalculateDungeonValue(buildings);
        return $"buildings={buildings.Length}; structuresExcluded={structures}; "
            + $"valuables={valuables}; dungeonValue={value:0.##}; "
            + $"riseContribution={value * 0.012f:0.###}/s";
    }

    private static void RunScenario(string name, System.Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario()) return;
        }
        finally
        {
            CleanupLeakedScenarioObjects();
        }

        errors.Add(name);
    }

    private static void CleanupLeakedScenarioObjects()
    {
        foreach (InvasionThreatRuntime runtime in Object.FindObjectsByType<InvasionThreatRuntime>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (runtime != null && runtime.gameObject.name == "InvasionThreatRuntime_Test")
            {
                Object.DestroyImmediate(runtime.gameObject);
            }
        }
    }

    private static bool VerifyThreatRiseFactors()
    {
        InvasionThreatSettings settings = new InvasionThreatSettings
        {
            difficulty = InvasionThreatDifficulty.Normal,
            baseRisePerSecond = 1f,
            dungeonValueRiseWeight = 1f,
            reputationRiseWeight = 1f,
            timeRiseWeight = 1f,
            riskRiseWeight = 1f
        };

        InvasionThreatFactors low = new InvasionThreatFactors(0f, 0f, 0f, 0f);
        InvasionThreatFactors high = new InvasionThreatFactors(2f, 1f, 1f, 1f);
        float lowRise = InvasionThreatCalculator.CalculateRisePerSecond(settings, low);
        float highRise = InvasionThreatCalculator.CalculateRisePerSecond(settings, high);

        settings.difficulty = InvasionThreatDifficulty.Hard;
        float hardRise = InvasionThreatCalculator.CalculateRisePerSecond(settings, low);

        return highRise > lowRise && hardRise > lowRise;
    }

    private static bool VerifyWarningAlert()
    {
        InvasionThreatRuntime runtime = CreateRuntime(
            out GameObject threatRoot,
            out IGameEventBus gameEventBus);
        ConfigureFastSettings(runtime);
        CountingThreatWarningListener listener = new CountingThreatWarningListener(gameEventBus);
        CountingEventAlertRequestListener alerts =
            new CountingEventAlertRequestListener(gameEventBus);

        runtime.AddThreat(71f);

        bool valid = listener.Count == 1
            && runtime.CurrentStage == InvasionThreatStage.Warning
            && alerts.Count == 1
            && alerts.LastTitle == "침입 경고"
            && alerts.LastImportance == EventAlertImportance.Medium;

        listener.Dispose();
        alerts.Dispose();
        Object.DestroyImmediate(threatRoot);
        CleanupRuntimeUi();
        return valid;
    }

    private static bool VerifyCandidateDelay()
    {
        InvasionThreatRuntime runtime = CreateRuntime(
            out GameObject threatRoot,
            out IGameEventBus gameEventBus);
        ConfigureFastSettings(runtime);
        CountingInvasionCandidateListener listener =
            new CountingInvasionCandidateListener(gameEventBus);
        CountingEventAlertRequestListener alerts =
            new CountingEventAlertRequestListener(gameEventBus);

        runtime.AddThreat(100f);
        runtime.Tick(0.1f);

        bool valid = listener.Count == 1
            && runtime.CurrentStage == InvasionThreatStage.Candidate
            && alerts.Requests.Any((request) => request.Title == "침입 임박" && request.Importance == EventAlertImportance.High);

        listener.Dispose();
        alerts.Dispose();
        Object.DestroyImmediate(threatRoot);
        CleanupRuntimeUi();
        return valid;
    }

    private static bool VerifyResetAndSafety()
    {
        InvasionThreatRuntime runtime = CreateRuntime(out GameObject root, out _);
        ConfigureFastSettings(runtime);
        runtime.Settings.safetyDurationSeconds = 10f;
        runtime.AddThreat(100f);
        runtime.Tick(0.1f);

        runtime.OnTriggerEvent(new InvasionStartedEvent(runtime.LatestSnapshot));

        bool reset = Mathf.Approximately(runtime.CurrentThreat, 0f)
            && runtime.CurrentStage == InvasionThreatStage.Safety
            && runtime.SafetyRemaining > 0f;

        runtime.Tick(11f);
        bool safetyEnded = runtime.CurrentStage != InvasionThreatStage.Safety;

        Object.DestroyImmediate(root);
        CleanupRuntimeUi();
        return reset && safetyEnded;
    }

    private static bool VerifyInitialSafety()
    {
        InvasionThreatRuntime runtime = CreateRuntime(out GameObject root, out _);
        runtime.Settings.initialSafetyDurationSeconds = 180f;

        runtime.OnTriggerEvent(new OperatingDayStartedEvent(1));
        bool active = runtime.CurrentStage == InvasionThreatStage.Safety
            && Mathf.Approximately(runtime.SafetyRemaining, 180f);
        runtime.Tick(179f);
        runtime.OnTriggerEvent(new OperatingDayStartedEvent(2));
        bool dayTwoRefreshed = Mathf.Approximately(runtime.SafetyRemaining, 180f);
        runtime.Tick(179f);
        runtime.OnTriggerEvent(new OperatingDayStartedEvent(3));
        bool dayThreeRefreshed = Mathf.Approximately(runtime.SafetyRemaining, 180f);
        runtime.Tick(181f);
        runtime.OnTriggerEvent(new OperatingDayStartedEvent(4));
        bool growthStartsThreat = runtime.CurrentStage != InvasionThreatStage.Safety
            && runtime.CurrentThreat > 0f;

        Object.DestroyImmediate(root);
        return active && dayTwoRefreshed && dayThreeRefreshed && growthStartsThreat;
    }

    private static bool VerifyStructuralValueExclusion()
    {
        BuildingSO wall = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO door = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO facility = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            wall.category = BuildingCategory.Wall;
            door.type = typeof(Door);
            facility.category = BuildingCategory.Production;
            facility.Facility = new FacilityData { roles = FacilityRole.Research };

            return Mathf.Approximately(InvasionThreatValueCalculator.CalculateBuildingValue(wall), 0f)
                && Mathf.Approximately(InvasionThreatValueCalculator.CalculateBuildingValue(door), 0f)
                && InvasionThreatValueCalculator.CalculateBuildingValue(facility) >= 0.5f;
        }
        finally
        {
            Object.DestroyImmediate(wall);
            Object.DestroyImmediate(door);
            Object.DestroyImmediate(facility);
        }
    }

    private static InvasionThreatRuntime CreateRuntime(
        out GameObject root,
        out IGameEventBus gameEventBus)
    {
        root = new GameObject("InvasionThreatRuntime_Test");
        gameEventBus = new GameEventBus();
        InvasionThreatRuntime runtime = root.AddComponent<InvasionThreatRuntime>();
        runtime.Construct(
            new FixedWorldSampler(),
            new NeutralRunVariableReader(),
            new NeutralMetaProgressionReader(),
            new DungeonStory.Foundation.UnityGameClock(),
            gameEventBus,
            new DungeonStory.Foundation.RandomStreamProvider(41));
        return runtime;
    }

    private static void ConfigureFastSettings(InvasionThreatRuntime runtime)
    {
        runtime.Settings.warningThreshold = 70f;
        runtime.Settings.candidateThreshold = 100f;
        runtime.Settings.warningCooldownSeconds = 0f;
        runtime.Settings.minCandidateDelaySeconds = 0f;
        runtime.Settings.maxCandidateDelaySeconds = 0f;
        runtime.Settings.baseRisePerSecond = 0f;
        runtime.Settings.dungeonValueRiseWeight = 0f;
        runtime.Settings.reputationRiseWeight = 0f;
        runtime.Settings.timeRiseWeight = 0f;
        runtime.Settings.riskRiseWeight = 0f;
    }

    private static void CleanupRuntimeUi()
    {
        string[] names =
        {
            "EventAlertRuntimeUI",
            "EventAlertButtonRoot",
            "EventAlertDetailPanel",
            "RuntimeUICanvas"
        };

        foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj != null
                && !EditorUtility.IsPersistent(obj)
                && System.Array.IndexOf(names, obj.name) >= 0)
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    private sealed class FixedWorldSampler : IInvasionThreatWorldSampler
    {
        public InvasionThreatFactors Sample(float secondsSinceLastInvasion)
        {
            return new InvasionThreatFactors(0f, 0f, 0f, 0f);
        }
    }

    private sealed class NeutralRunVariableReader : IRunVariableRuntimeReader
    {
        public int GetInitialShopSeed() => 0;
        public IReadOnlyList<int> GetStartingBlueprintCandidateIds() => System.Array.Empty<int>();
        public float GetGuestDemandMultiplier(string speciesTag) => 1f;
        public float GetStockCostMultiplier(StockCategory category) => 1f;
        public float GetFacilityShopCostMultiplier(BuildingSO building) => 1f;
        public float GetBlueprintCostMultiplier(FacilityBlueprintSO blueprint) => 1f;
        public float GetThreatRiseMultiplier() => 1f;
        public float GetWarningThresholdMultiplier() => 1f;
        public InvasionIntruderSettings ApplyInvasionSettings(InvasionIntruderSettings source) => source;
    }

    private sealed class NeutralMetaProgressionReader : IMetaProgressionRuntimeReader
    {
        public int GetStartingFacilityCandidateBonus() => 0;
        public int GetStartingOwnerTraitCandidateBonus() => 0;
        public float GetOwnerMaxHealthMultiplier() => 1f;
        public float GetInvasionWarningThresholdMultiplier() => 1f;
        public float GetCommerceStockCostMultiplier(StockCategory category) => 1f;
        public float GetFortressFacilityCostMultiplier(BuildingSO building) => 1f;
        public float GetArcaneResearchWorkMultiplier() => 1f;
        public bool IsRecipePreserved(string recipeId) => false;

        public IReadOnlyCollection<int> GetExpandedBasicPurchaseBuildingIds(
            IEnumerable<BuildingSO> buildings)
        {
            return System.Array.Empty<int>();
        }
    }

    private sealed class CountingThreatWarningListener : System.IDisposable
    {
        private readonly System.IDisposable subscription;

        public int Count { get; private set; }

        public CountingThreatWarningListener(IGameEventBus gameEventBus)
        {
            subscription = gameEventBus.Subscribe<InvasionThreatWarningEvent>(OnThreatWarning);
        }

        private void OnThreatWarning(InvasionThreatWarningEvent eventType)
        {
            Count++;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }

    private sealed class CountingInvasionCandidateListener : System.IDisposable
    {
        private readonly System.IDisposable subscription;

        public int Count { get; private set; }

        public CountingInvasionCandidateListener(IGameEventBus gameEventBus)
        {
            subscription = gameEventBus.Subscribe<InvasionCandidateEvent>(OnInvasionCandidate);
        }

        private void OnInvasionCandidate(InvasionCandidateEvent eventType)
        {
            Count++;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }

    private sealed class CountingEventAlertRequestListener : System.IDisposable
    {
        private readonly List<EventAlertRequest> requests = new List<EventAlertRequest>();
        private readonly System.IDisposable subscription;

        public IReadOnlyList<EventAlertRequest> Requests => requests;
        public int Count => requests.Count;
        public string LastTitle => requests.Count > 0 ? requests[requests.Count - 1].Title : string.Empty;
        public EventAlertImportance LastImportance => requests.Count > 0 ? requests[requests.Count - 1].Importance : EventAlertImportance.Low;

        public CountingEventAlertRequestListener(IGameEventBus gameEventBus)
        {
            subscription =
                gameEventBus.Subscribe<EventAlertRequestedEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(EventAlertRequestedEvent eventType)
        {
            if (eventType.request != null)
            {
                requests.Add(eventType.request);
            }
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}
