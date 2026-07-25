using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class OffenseWorldMapDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Offense/Run P3 World Map Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P3 offense world map scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();

        RunScenario("초기 가까운 원정 대상 공개", VerifyInitialNearbyTargets, errors);
        RunScenario("정찰 강화로 원정 대상 추가 공개", VerifyReconUpgradeRevealsMoreTargets, errors);
        RunScenario("원정 대상 정보 필드", VerifyTargetRequirementFields, errors);
        RunScenario("세 전략 원정 설계도 고정 연결", VerifyStrategyBlueprintRoutes, errors);
        RunScenario("원정 대상 선택 이벤트", VerifyTargetSelectionEvent, errors);
        RunScenario("오펜스 캠페인 선행 목표 잠금", VerifyCampaignPrerequisiteChain, errors);
        RunScenario("최종 오펜스 진실 공개", VerifyTerminalTruthReveal, errors);
        RunScenario("월드맵 읽기 경계", VerifyReadOnlyWorldMapBoundary, errors);
        RunScenario("월드맵 패널 생성", VerifyPanelCreation, errors);

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
            Debug.Log("P3 offense world map scenarios passed.");
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

    private static bool VerifyInitialNearbyTargets()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        IReadOnlyList<OffenseTargetSnapshot> visible = scenario.Runtime.VisibleTargets;

        return scenario.Runtime.State.ReconLevel == 0
            && visible.Count >= 2
            && visible.All((target) => target.distance <= OffenseWorldMapService.GetScanRange(0))
            && visible.Any((target) => target.id == "food_farm")
            && visible.Any((target) => target.id == "merchant_road")
            && visible.All((target) => target.id != "mana_ruins");
    }

    private static bool VerifyReconUpgradeRevealsMoreTargets()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        int initialCount = scenario.Runtime.VisibleTargets.Count;
        bool upgraded = scenario.Runtime.TryUpgradeRecon(out string message);
        int afterFirstUpgrade = scenario.Runtime.VisibleTargets.Count;

        scenario.Runtime.TryUpgradeRecon(out _);
        int afterSecondUpgrade = scenario.Runtime.VisibleTargets.Count;

        return upgraded
            && message.Contains("정찰 Lv.1")
            && afterFirstUpgrade > initialCount
            && afterSecondUpgrade > afterFirstUpgrade
            && scenario.Runtime.VisibleTargets.Any((target) => target.id == "mana_ruins");
    }

    private static bool VerifyTargetRequirementFields()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        scenario.Runtime.TryUpgradeRecon(out _);

        OffenseTargetSnapshot target = scenario.Runtime.VisibleTargets.FirstOrDefault((candidate) => candidate.id == "old_armory");
        return target != null
            && target.danger > 0f
            && target.durationSeconds > 0f
            && target.requiredMembers >= 1
            && target.requiredPower > 0f
            && target.rewards.Count > 0
            && target.ToDetailText().Contains("필요 인력");
    }

    private static bool VerifyTargetSelectionEvent()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        using CountingSelectionListener selections = new CountingSelectionListener(scenario.Runtime);

        bool selected = scenario.Runtime.TrySelectTarget("food_farm", out OffenseTargetSnapshot snapshot, out string message);
        return selected
            && snapshot != null
            && message.Contains(snapshot.title)
            && scenario.Runtime.State.SelectedTargetId == "food_farm"
            && selections.Count == 1
            && selections.LastTargetId == "food_farm";
    }

    private static bool VerifyStrategyBlueprintRoutes()
    {
        IReadOnlyList<OffenseTargetDefinition> targets = OffenseWorldMapService.CreateDefaultTargets();
        return HasSpecificBlueprint(targets, "merchant_road", OffenseStrategyBlueprintIds.CommerceLogistics)
            && HasSpecificBlueprint(targets, "old_armory", OffenseStrategyBlueprintIds.FortressDefense)
            && HasSpecificBlueprint(targets, "mana_ruins", OffenseStrategyBlueprintIds.ArcaneResearch);
    }

    private static bool VerifyCampaignPrerequisiteChain()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        OffenseTargetSnapshot first = scenario.Runtime.VisibleTargets
            .FirstOrDefault(target => target.id == "food_farm");
        OffenseTargetSnapshot second = scenario.Runtime.VisibleTargets
            .FirstOrDefault(target => target.id == "merchant_road");
        bool skipped = scenario.Runtime.TrySelectTarget(
            "merchant_road",
            out _,
            out string lockedMessage);
        bool completed = scenario.Runtime.TryRecordSuccessfulExpedition(
            "food_farm",
            out OffenseTargetSnapshot completedFirst,
            out _);
        second = scenario.Runtime.VisibleTargets.FirstOrDefault(target => target.id == "merchant_road");
        bool duplicate = scenario.Runtime.TryRecordSuccessfulExpedition(
            "food_farm",
            out _,
            out string duplicateMessage);

        return first != null
            && first.isAvailable
            && second != null
            && !skipped
            && lockedMessage.Contains("앞선")
            && completed
            && completedFirst.isCompleted
            && scenario.Runtime.State.CompletedTargetCount == 1
            && second.isAvailable
            && !duplicate
            && duplicateMessage.Contains("이미 완료");
    }

    private static bool VerifyTerminalTruthReveal()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        using CountingTruthListener truthListener = new CountingTruthListener(scenario.GameEvents);
        while (scenario.Runtime.State.ReconLevel < OffenseWorldMapService.MaxReconLevel)
        {
            scenario.Runtime.TryUpgradeRecon(out _);
        }

        IReadOnlyList<OffenseTargetDefinition> route = scenario.Runtime.TargetDefinitions
            .OrderBy(target => target.campaignOrder)
            .ToList();
        bool allCompleted = true;
        for (int i = 0; i < route.Count; i++)
        {
            allCompleted &= scenario.Runtime.TryRecordSuccessfulExpedition(
                route[i].id,
                out OffenseTargetSnapshot completed,
                out _);
            if (i < route.Count - 1
                && (truthListener.Count != 0 || scenario.Runtime.State.TruthRevealed))
            {
                return false;
            }

            allCompleted &= completed != null && completed.isCompleted;
        }

        return allCompleted
            && route.Count == 6
            && route[route.Count - 1].id == OffenseWorldMapService.TruthTargetId
            && scenario.Runtime.State.TruthRevealed
            && scenario.Runtime.State.RevealedTruthTargetId == OffenseWorldMapService.TruthTargetId
            && scenario.Runtime.State.CompletedTargetCount == route.Count
            && truthListener.Count == 1
            && truthListener.TruthText.Contains("지상의 왕국");
    }

    private static bool HasSpecificBlueprint(
        IEnumerable<OffenseTargetDefinition> targets,
        string targetId,
        int blueprintId)
    {
        return targets.FirstOrDefault(target => target.id == targetId)?.rewards
            .Select(reward => reward?.GrantSpec)
            .OfType<OffenseSpecificBlueprintRewardSpec>()
            .Any(spec => spec.BlueprintId == blueprintId) == true;
    }

    private static bool VerifyReadOnlyWorldMapBoundary()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        OffenseTargetDefinition source = new OffenseTargetDefinition
        {
            id = "boundary_target",
            title = "원본 제목",
            description = "원본 설명",
            kind = OffenseTargetKind.ResourceSite,
            distance = 1f,
            danger = 3f,
            durationSeconds = 20f,
            requiredMembers = 1,
            requiredPower = 1f,
            rewards = new[]
            {
                new OffenseRewardPreview("원본 보상", 1, new OffenseMoneyRewardSpec())
            }
        };
        scenario.Runtime.SetTargetsForDebug(new[] { source });
        source.title = "외부에서 변경한 제목";
        source.rewards[0] = new OffenseRewardPreview("외부 변경", 99, new OffenseMoneyRewardSpec());

        OffenseTargetSnapshot visible = scenario.Runtime.VisibleTargets.Single();
        bool rewardMutationRejected = false;
        if (visible.rewards is IList<string> rewards)
        {
            try
            {
                rewards[0] = "침범";
            }
            catch (NotSupportedException)
            {
                rewardMutationRejected = true;
            }
        }

        return visible.title == "원본 제목"
            && visible.rewards.Single() == "원본 보상 x1"
            && rewardMutationRejected
            && scenario.Runtime.State.KnownTargetIds is not HashSet<string>;
    }

    private static bool VerifyPanelCreation()
    {
        using ScenarioRuntime scenario = new ScenarioRuntime();
        OffenseWorldMapPanel panel = scenario.Runtime.ShowWorldMap();

        bool valid = panel != null
            && panel.gameObject.activeSelf
            && Object.FindFirstObjectByType<Canvas>() != null;

        if (panel != null)
        {
            Object.DestroyImmediate(panel.transform.root.gameObject);
        }

        return valid;
    }

    private sealed class ScenarioRuntime : IDisposable
    {
        private readonly GameObject runtimeObject;

        public OffenseWorldMapRuntime Runtime { get; }
        public DungeonStory.Foundation.IGameEventBus GameEvents { get; }

        public ScenarioRuntime()
        {
            GameEvents = new DungeonStory.Foundation.GameEventBus();
            runtimeObject = new GameObject("Offense World Map Scenario Runtime");
            Runtime = runtimeObject.AddComponent<OffenseWorldMapRuntime>();
            Runtime.Construct(new TestPanelService(), GameEvents);
            Runtime.StartWorldMap();
        }

        public void Dispose()
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }

    private sealed class TestPanelService : IOffensePanelService
    {
        public OffenseWorldMapPanel ShowWorldMap(OffenseWorldMapRuntime runtime)
        {
            GameObject canvasObject = new GameObject("World Map Test Canvas", typeof(Canvas));
            GameObject panelObject = new GameObject("World Map Test Panel");
            panelObject.transform.SetParent(canvasObject.transform, false);
            return panelObject.AddComponent<OffenseWorldMapPanel>();
        }

        public OffenseExpeditionPanel ShowExpedition(OffenseExpeditionRuntime runtime)
        {
            return null;
        }
    }

    private sealed class CountingSelectionListener : IDisposable
    {
        private OffenseWorldMapRuntime runtime;
        public int Count { get; private set; }
        public string LastTargetId { get; private set; }

        public CountingSelectionListener(OffenseWorldMapRuntime runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.runtime.TargetSelected += OnTargetSelected;
        }

        private void OnTargetSelected(OffenseTargetSnapshot target)
        {
            if (target == null) return;

            Count++;
            LastTargetId = target.id;
        }

        public void Dispose()
        {
            if (runtime == null) return;

            runtime.TargetSelected -= OnTargetSelected;
            runtime = null;
        }
    }

    private sealed class CountingTruthListener : IDisposable
    {
        private readonly IDisposable subscription;
        public int Count { get; private set; }
        public string TruthText { get; private set; }

        public CountingTruthListener(DungeonStory.Foundation.IGameEventBus gameEventBus)
        {
            subscription = gameEventBus.Subscribe<OffenseTruthRevealedEvent>(OnTriggerEvent);
        }

        public void OnTriggerEvent(OffenseTruthRevealedEvent eventType)
        {
            Count++;
            TruthText = eventType.truthText;
        }

        public void Dispose()
        {
            subscription.Dispose();
        }
    }
}
