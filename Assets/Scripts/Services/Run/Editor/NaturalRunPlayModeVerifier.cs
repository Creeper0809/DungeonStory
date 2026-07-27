using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class NaturalRunPlayModeVerifier
{
    public const string RequestPath = "Temp/natural-run-verification.request";
    public const string ReportPath = "Temp/natural-run-verification-report.txt";
    public const string ScreenshotPath = "Temp/natural-run-verification.png";
    public const string ShopScreenshotPath = "Temp/natural-run-shop.png";
    public const string CommerceReportPath = "Temp/natural-run-commerce-report.txt";
    public const string FortressReportPath = "Temp/natural-run-fortress-report.txt";
    public const string ArcaneReportPath = "Temp/natural-run-arcane-report.txt";
    public const string WeakReportPath = "Temp/natural-run-weak-report.txt";
    public const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";

    private static bool runnerCreated;

    static NaturalRunPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Natural Run Verification")]
    public static void RequestRunFromMenu()
    {
        Request("baseline");
    }

    [MenuItem("DungeonStory/Debug/QA/Strategy Run/Commerce Logistics")]
    public static void RequestCommerceRunFromMenu()
    {
        Request("commerce");
    }

    [MenuItem("DungeonStory/Debug/QA/Strategy Run/Fortress Defense")]
    public static void RequestFortressRunFromMenu()
    {
        Request("fortress");
    }

    [MenuItem("DungeonStory/Debug/QA/Strategy Run/Arcane Research")]
    public static void RequestArcaneRunFromMenu()
    {
        Request("arcane");
    }

    [MenuItem("DungeonStory/Debug/QA/Strategy Run/Intentional Weak Layout")]
    public static void RequestWeakRunFromMenu()
    {
        Request("weak");
    }

    private static void Request(string token)
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        File.WriteAllText(RequestPath, token + "\n" + DateTime.UtcNow.ToString("O"));
    }

    private static void OnEditorUpdate()
    {
        if (File.Exists(RequestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (!string.Equals(
                    SceneManager.GetActiveScene().path,
                    NaturalRunPlayModeVerifier.GameplayScenePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            }

            EditorApplication.EnterPlaymode();
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            runnerCreated = false;
            return;
        }

        if (change == PlayModeStateChange.EnteredPlayMode
            && !runnerCreated
            && File.Exists(RequestPath))
        {
            runnerCreated = true;
            GameObject runnerObject = new GameObject("Natural Run Verification Runner");
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            runnerObject.AddComponent<NaturalRunVerificationRunner>();
        }
    }
}

public sealed class NaturalRunStrategySpec
{
    private static readonly IReadOnlyList<int> CommerceDefenses = Array.AsReadOnly(
        new[] { 31, 30, 30, 30, 30 });
    private static readonly IReadOnlyList<int> FortressDefenses = Array.AsReadOnly(
        new[] { 30, 30, 30, 30, 30 });
    private static readonly IReadOnlyList<int> ArcaneDefenses = Array.AsReadOnly(
        new[] { 32, 32, 32, 33, 33, 33 });
    private static readonly IReadOnlyList<int> NoDefenses = Array.AsReadOnly(Array.Empty<int>());

    private NaturalRunStrategySpec(
        string token,
        string speciesTag,
        string doctrineId,
        int blueprintId,
        string reportPath)
    {
        Token = token;
        SpeciesTag = speciesTag;
        DoctrineId = doctrineId;
        BlueprintId = blueprintId;
        ReportPath = reportPath;
        ScreenshotPath = $"Temp/natural-run-{token}.png";
        ShopScreenshotPath = $"Temp/natural-run-{token}-shop.png";
        DefenseScreenshotPath = $"Temp/natural-run-{token}-defense.png";
    }

    public string Token { get; }
    public string SpeciesTag { get; }
    public string DoctrineId { get; }
    public int BlueprintId { get; }
    public string ReportPath { get; }
    public string ScreenshotPath { get; }
    public string ShopScreenshotPath { get; }
    public string DefenseScreenshotPath { get; }
    public bool IsTargeted => BlueprintId >= 0 && !string.IsNullOrWhiteSpace(SpeciesTag);
    public bool ExpectsDefeat => string.Equals(Token, "weak", StringComparison.Ordinal);
    public IReadOnlyList<int> DefenseBuildingIds => Token switch
    {
        "commerce" => CommerceDefenses,
        "fortress" => FortressDefenses,
            "arcane" => ArcaneDefenses,
            "weak" => NoDefenses,
            _ => NoDefenses
    };

    public static NaturalRunStrategySpec ReadRequest(string path)
    {
        string token = File.Exists(path)
            ? File.ReadLines(path).FirstOrDefault()?.Trim().ToLowerInvariant()
            : string.Empty;
        return token switch
        {
            "commerce" => new NaturalRunStrategySpec(
                "commerce",
                "Slime",
                OwnerDoctrineIds.SlimeStewardship,
                RunStrategyBlueprintIds.CommerceBasics,
                NaturalRunPlayModeVerifier.CommerceReportPath),
            "fortress" => new NaturalRunStrategySpec(
                "fortress",
                "Orc",
                OwnerDoctrineIds.OrcWarCamp,
                RunStrategyBlueprintIds.FortressBasics,
                NaturalRunPlayModeVerifier.FortressReportPath),
            "arcane" => new NaturalRunStrategySpec(
                "arcane",
                "Vampire",
                OwnerDoctrineIds.VampireForbiddenStudy,
                RunStrategyBlueprintIds.ArcaneBasics,
                NaturalRunPlayModeVerifier.ArcaneReportPath),
            "weak" => new NaturalRunStrategySpec(
                "weak",
                "Slime",
                OwnerDoctrineIds.SlimeStewardship,
                RunStrategyBlueprintIds.CommerceBasics,
                NaturalRunPlayModeVerifier.WeakReportPath),
            _ => new NaturalRunStrategySpec(
                "baseline",
                string.Empty,
                string.Empty,
                -1,
                NaturalRunPlayModeVerifier.ReportPath)
        };
    }
}

public sealed class NaturalRunVerificationRunner : MonoBehaviour
{
    private const float MaximumRealtimeSeconds = 900f;
    private const float DirectPlayReadyHealthRatio = 0.25f;
    private const float DirectPlayReadyStressLimit = 79.99f;
    private const float DirectPlayRecoveryMaxRealtimeSeconds = 90f;
    private const float DirectPlayLogisticsMaxRealtimeSeconds = 75f;
    private const float DirectPlayRecruitmentMaxRealtimeSeconds = 150f;
    private const float DirectPlayHealAbilityThreshold = 0.74f;
    private const float DirectPlayGuardAbilityThreshold = 0.82f;
    private const float DirectPlayLateStageHealthyHealthRatio = 0.55f;
    private const float DirectPlayFinalStageHealthyHealthRatio = 0.70f;
    private const float DirectPlayFinalStageStressLimit = 70f;
    private const float DirectPlayMedicineHealthRatio = 0.70f;
    private const float DirectPlayBossMedicineHealthRatio = 0.92f;
    private const int DirectPlayWeakExtraLevelGap = 12;
    private const int DirectPlayCautiousRouteFromStage = 4;
    private const int DirectPlayEarlyPreferredMembers = 3;
    private const int DirectPlayFullPreferredMembers = 3;
    private const int DirectPlayFullPartyFromStage = 3;

    private sealed class FileBackup
    {
        public string Path;
        public byte[] Bytes;
    }

    private readonly List<string> report = new List<string>();
    private readonly List<string> errors = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<FileBackup> backups = new List<FileBackup>();
    private readonly HashSet<DungeonRunPhase> observedPhases = new HashSet<DungeonRunPhase>();

    private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private IDungeonGameSaveSlotService slotService;
    private string profilePath;
    private bool verificationCompleted;
    private IDisposable defenseFacilityTriggeredSubscription;
    private IDisposable bossInvasionStartedSubscription;
    private NaturalRunStrategySpec strategy;
    private int startingMoney;
    private int purchasedBlueprintCost;
    private int installedDefenseCount;
    private int installedDefenseCost;
    private int defenseTriggerCount;
    private float defenseDamage;
    private int bossDefenseTriggerCount;
    private float bossDefenseDamage;
    private bool bossFightObserved;
    private bool offenseCampaignAttempted;
    private bool lastDirectPlayLogisticsSettled = true;

    private sealed class DirectOffenseCampaignStats
    {
        public int RouteChoices;
        public int NodeResolutions;
        public int BattlesCompleted;
        public int BattleCommands;
        public int AbilityCommands;
        public int SupplyUses;
        public int FailedCommands;
        public string FailureDetail = string.Empty;
        public bool Failed => FailedCommands > 0 || !string.IsNullOrWhiteSpace(FailureDetail);
    }

    private void OnDisable()
    {
        defenseFacilityTriggeredSubscription?.Dispose();
        defenseFacilityTriggeredSubscription = null;
        bossInvasionStartedSubscription?.Dispose();
        bossInvasionStartedSubscription = null;
    }

    public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)
    {
        DefenseActivationSnapshot activation = eventType.report;
        if (activation == null)
        {
            return;
        }

        defenseTriggerCount++;
        defenseDamage += activation.TotalDamage;
        if (bossFightObserved)
        {
            bossDefenseTriggerCount++;
            bossDefenseDamage += activation.TotalDamage;
        }
    }

    public void OnTriggerEvent(BossInvasionStartedEvent eventType)
    {
        bossFightObserved = true;
        bossDefenseTriggerCount = 0;
        bossDefenseDamage = 0f;

        InvasionIntruderRuntime intruderRuntime = eventType.Intruder != null
            ? eventType.Intruder.GetComponent<InvasionIntruderRuntime>()
            : null;
        CharacterActor owner = FindFirstObjectByType<OwnerRunManager>()?.CurrentOwnerActor;
        report.Add(
            $"BOSS_RALLY active={intruderRuntime?.HasFinalDefenseTarget ?? false}; "
            + $"target={(intruderRuntime != null ? intruderRuntime.FinalDefenseTarget : default)}; "
            + $"owner={(owner != null ? owner.GetNowXY() : default)}; "
            + $"intruder={(eventType.Intruder != null ? eventType.Intruder.GetNowXY() : default)}");
        string defenseState = string.Join(
            " | ",
            FindObjectsByType<DefenseFacility>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(defense => defense != null && !defense.isDestroy)
                .OrderBy(defense => defense.centerPos.x)
                .Select(defense =>
                    $"{defense.BuildingData?.id ?? -1}@{defense.centerPos}:"
                    + $"damaged={defense.IsDamaged},cooldown={defense.CooldownRemaining:0.##}"));
        report.Add($"BOSS_DEFENSE_STATE {defenseState}");
        FlushProgress();
    }

    private IEnumerator Start()
    {
        yield return Run();
    }

    private IEnumerator Run()
    {
        Directory.CreateDirectory("Temp");
        strategy = NaturalRunStrategySpec.ReadRequest(NaturalRunPlayModeVerifier.RequestPath);
        File.WriteAllText(
            strategy.ReportPath,
            $"NATURAL_RUN IN_PROGRESS strategy={strategy.Token}" + Environment.NewLine);
        report.Add($"strategy={strategy.Token}; species={strategy.SpeciesTag}; "
            + $"doctrine={strategy.DoctrineId}; blueprint={strategy.BlueprintId}");
        Application.logMessageReceived += CaptureLog;
        ConfigureInput();

        try
        {
            yield return new WaitForSecondsRealtime(2f);
            DungeonRuntimeLifetimeScope scope = FindScope();
            Check(scope != null, "DI_SCOPE", "active game container resolved");
            Check(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path.EndsWith(
                    NaturalRunPlayModeVerifier.GameplayScenePath,
                    StringComparison.Ordinal),
                "SCENE", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            if (scope == null)
            {
                yield break;
            }

            DungeonStory.Foundation.IGameEventBus gameEventBus =
                scope.Container.Resolve<DungeonStory.Foundation.IGameEventBus>();
            defenseFacilityTriggeredSubscription =
                gameEventBus.Subscribe<DefenseFacilityTriggeredEvent>(OnTriggerEvent);
            bossInvasionStartedSubscription =
                gameEventBus.Subscribe<BossInvasionStartedEvent>(OnTriggerEvent);
            slotService = scope.Container.Resolve<IDungeonGameSaveSlotService>();
            IMetaProfileStore profileStore = scope.Container.Resolve<IMetaProfileStore>();
            profilePath = profileStore.ProfilePath;
            BackupFile(profilePath);
            foreach (DungeonSaveSlotInfo slot in slotService.GetSlots())
            {
                BackupFile(slot.Path);
            }

            yield return StartFreshRun();

            IGameDataProvider gameDataProvider = scope.Container.Resolve<IGameDataProvider>();
            IDungeonRunFlowRuntime flow = scope.Container.Resolve<IDungeonRunFlowRuntime>();
            IBlueprintResearchRuntimeProvider researchProvider =
                scope.Container.Resolve<IBlueprintResearchRuntimeProvider>();
            IOperatingDaySettlementRuntimeProvider settlementProvider =
                scope.Container.Resolve<IOperatingDaySettlementRuntimeProvider>();
            IInvasionDirectorRuntimeProvider invasionProvider =
                scope.Container.Resolve<IInvasionDirectorRuntimeProvider>();
            IDailyFacilityShopRuntimeProvider dailyShopProvider =
                scope.Container.Resolve<IDailyFacilityShopRuntimeProvider>();
            IRunVariableRuntimeProvider runVariableProvider =
                scope.Container.Resolve<IRunVariableRuntimeProvider>();
            IMetaProgressionRuntimeProvider metaProvider =
                scope.Container.Resolve<IMetaProgressionRuntimeProvider>();
            IWorldItemStackRuntime itemRuntime =
                scope.Container.Resolve<IWorldItemStackRuntime>();
            Check(gameDataProvider.TryGetGameData(out GameData gameData), "GAME_DATA", "runtime data resolved");
            Check(flow != null, "RUN_FLOW", "run flow resolved");
            if (gameData == null || flow == null)
            {
                yield break;
            }

            bool hasMetaRuntime = metaProvider.TryGetRuntime(out MetaProgressionRuntime metaRuntime);
            Check(hasMetaRuntime, "META_RUNTIME", "meta progression runtime resolved");
            int legacyCurrencyBefore = hasMetaRuntime
                ? metaRuntime.State.LifetimeEarnedCurrency
                : -1;
            int completedRunsBefore = hasMetaRuntime
                ? metaRuntime.State.CompletedRunCount
                : -1;

            startingMoney = gameData.holdingMoney.Value;
            if (strategy.IsTargeted)
            {
                bool hasRunVariables = runVariableProvider.TryGetRuntime(out RunVariableRuntime runVariables);
                RunStartVariableSnapshot startVariables = hasRunVariables
                    ? runVariables.State.StartVariables
                    : null;
                Check(startVariables != null
                        && startVariables.ownerDoctrineId == strategy.DoctrineId
                        && startVariables.startingBlueprintCandidateIds.Count > 0
                        && startVariables.startingBlueprintCandidateIds[0] == strategy.BlueprintId,
                    "STRATEGY_START",
                    $"doctrine={startVariables?.ownerDoctrineId ?? "missing"}; "
                    + $"candidates={string.Join(",", startVariables?.startingBlueprintCandidateIds ?? Array.Empty<int>())}");
            }

            observedPhases.Add(flow.Phase);
            Button speedButton = FindSpeedButton();
            int speedPointerClicks = 0;
            while (gameData.gameSpeed.Value < 5 && speedPointerClicks < 4)
            {
                yield return Click(speedButton, "speed");
                speedPointerClicks++;
            }

            Check(gameData.gameSpeed.Value == 5 && Mathf.Approximately(Time.timeScale, 5f),
                "PUBLIC_SPEED", $"pointerClicks={speedPointerClicks}; speed=x{gameData.gameSpeed.Value}; timeScale={Time.timeScale:0.##}");

            BlueprintResearchRuntime research = null;
            researchProvider.TryGetRuntime(out research);
            int researchQueueBefore = research != null ? research.State.Projects.Queue.Count : -1;
            int purchasedOffers = 0;
            Button shopTab = FindTopTabButton(TabId.Shop);
            yield return Click(shopTab, "shop tab");
            yield return new WaitForSecondsRealtime(0.25f);
            yield return CaptureEvidence(strategy.ShopScreenshotPath);

            int runtimeOfferCount = dailyShopProvider.TryGetRuntime(out DailyFacilityShopRuntime dailyShop)
                ? dailyShop.CurrentDailyOffers.Count
                : -1;
            Button[] dailyOffers = FindActiveButtons("P0Action_ShopDaily_")
                .OrderBy(button => button.name, StringComparer.Ordinal)
                .ToArray();
            Check(runtimeOfferCount > 0 && dailyOffers.Length > 0,
                "PUBLIC_DAILY_OFFERS",
                $"runtimeOffers={runtimeOfferCount}; visibleButtons={dailyOffers.Length}");
            int blueprintOfferIndex = -1;
            FacilityShopOffer selectedBlueprintOffer = null;
            for (int i = 0; dailyShop != null && i < dailyShop.CurrentDailyOffers.Count; i++)
            {
                FacilityShopOffer offer = dailyShop.CurrentDailyOffers[i];
                if (offer != null
                    && string.Equals(
                        offer.OfferTypeId,
                        FacilityShopOfferTypeIds.Blueprint,
                        StringComparison.Ordinal)
                    && (!strategy.IsTargeted || offer.DataId == strategy.BlueprintId))
                {
                    blueprintOfferIndex = i;
                    selectedBlueprintOffer = offer;
                    break;
                }
            }

            if (strategy.IsTargeted)
            {
                Check(blueprintOfferIndex >= 0,
                    "STRATEGY_DAY1_OFFER",
                    $"blueprint={strategy.BlueprintId}; offers={string.Join(",", dailyShop?.CurrentDailyOffers.Select((offer) => offer.DataId) ?? Array.Empty<int>())}");
            }

            if (blueprintOfferIndex >= 0)
            {
                Button blueprintButton = FindButton($"P0Action_ShopDaily_{blueprintOfferIndex}");
                purchasedOffers = blueprintButton != null ? 1 : 0;
                int moneyBeforePurchase = gameData.holdingMoney.Value;
                yield return Click(blueprintButton, $"P0Action_ShopDaily_{blueprintOfferIndex}");
                yield return new WaitForSecondsRealtime(0.25f);
                purchasedBlueprintCost = moneyBeforePurchase - gameData.holdingMoney.Value;
            }

            FacilityBlueprintSO purchasedBlueprint =
                (selectedBlueprintOffer as FacilityBlueprintOffer)?.Blueprint;
            int researchQueueAfter = research != null ? research.State.Projects.Queue.Count : -1;
            bool physicalBlueprintExists = purchasedBlueprint != null
                && itemRuntime.GetAllStacks().Any(stack => stack != null
                    && stack.Quantity > 0
                    && string.Equals(
                        stack.ItemId,
                        purchasedBlueprint.PhysicalItemId,
                        StringComparison.Ordinal));
            Check(purchasedOffers > 0
                    && physicalBlueprintExists
                    && researchQueueAfter == researchQueueBefore,
                "PUBLIC_RESEARCH_PURCHASE",
                $"pointerClicks={purchasedOffers}; blueprint={selectedBlueprintOffer?.DataId ?? -1}; "
                + $"cost={purchasedBlueprintCost}; physical={physicalBlueprintExists}; "
                + $"queue={researchQueueBefore}->{researchQueueAfter}; money={gameData.holdingMoney.Value}");
            if (strategy.IsTargeted)
            {
                bool targetAcquired = dailyShop != null
                    && dailyShop.UnlockState.AcquiredBlueprintIds.Contains(strategy.BlueprintId);
                bool targetPhysical = purchasedBlueprint != null
                    && purchasedBlueprint.id == strategy.BlueprintId
                    && physicalBlueprintExists;
                Check(targetAcquired && targetPhysical,
                    "STRATEGY_RESEARCH_PATH",
                    $"blueprint={strategy.BlueprintId}; acquired={targetAcquired}; physical={targetPhysical}; "
                    + $"offerCost={selectedBlueprintOffer?.Cost ?? -1}; paid={purchasedBlueprintCost}");
            }
            yield return Click(shopTab, "shop tab close");
            yield return PrioritizeStrategyResearch();

            int acceleratorCount = CountPublicAccelerators();
            Check(acceleratorCount == 0, "NO_PUBLIC_ACCELERATORS", $"buttons={acceleratorCount}");

            float researchProgressBefore = GetTotalResearchProgress(research);
            int completedResearchBefore = research != null
                ? research.State.Projects.CompletedProjectIds.Count
                : 0;
            int facilityUsesBefore = GetCompletedFacilityUses();
            int facilityWorkBefore = GetCompletedFacilityWork();
            CharacterActor owner = FindFirstObjectByType<OwnerRunManager>()?.CurrentOwnerActor;
            Check(owner != null && !owner.IsDead, "OWNER_ACTIVE", owner != null ? owner.name : "missing");

            float startedAt = Time.realtimeSinceStartup;
            int lastDay = gameData.day.Value;
            bool ownerDiedBeforeFinal = false;
            bool strategyDefensesInstalled = !strategy.IsTargeted;
            bool strategyDefenseAttempted = !strategy.IsTargeted;
            RecordDaySnapshot(lastDay, gameData, flow, research, settlementProvider, invasionProvider, owner);

            while (Time.realtimeSinceStartup - startedAt < MaximumRealtimeSeconds
                && gameData.day.Value < 10
                && flow.Outcome == DungeonRunOutcome.None)
            {
                yield return new WaitForSecondsRealtime(1f);
                observedPhases.Add(flow.Phase);
                if (owner != null && owner.IsDead && gameData.day.Value < 10)
                {
                    ownerDiedBeforeFinal = true;
                }

                if (!strategyDefenseAttempted && gameData.day.Value >= 7)
                {
                    strategyDefenseAttempted = true;
                    yield return InstallStrategyDefenses(scope, gameData, owner);
                    strategyDefensesInstalled = installedDefenseCount == strategy.DefenseBuildingIds.Count;
                }

                if (!strategy.ExpectsDefeat
                    && strategyDefensesInstalled
                    && !offenseCampaignAttempted
                    && IsStrategyResearchReady(research, strategy))
                {
                    yield return ReduceGameSpeedToOne(gameData);
                    offenseCampaignAttempted = true;
                    report.Add(
                        "OFFENSE_TRIGGER "
                        + "reason=functional-window; "
                        + $"day={gameData.day.Value}; "
                        + $"defenses={installedDefenseCount}; "
                        + $"researchReady=True");
                    FlushProgress();
                    yield return CompleteOffenseCampaignForVerification(flow, gameData);
                    if (flow.Outcome != DungeonRunOutcome.None)
                    {
                        break;
                    }
                }

                if (gameData.day.Value != lastDay)
                {
                    lastDay = gameData.day.Value;
                    RecordDaySnapshot(lastDay, gameData, flow, research, settlementProvider, invasionProvider, owner);
                }
            }

            if (!strategyDefenseAttempted && gameData.day.Value >= 7)
            {
                strategyDefenseAttempted = true;
                yield return InstallStrategyDefenses(scope, gameData, owner);
                strategyDefensesInstalled = installedDefenseCount == strategy.DefenseBuildingIds.Count;
            }

            while (Time.realtimeSinceStartup - startedAt < MaximumRealtimeSeconds
                && gameData.day.Value >= 10
                && flow.Outcome == DungeonRunOutcome.None)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                if (flow.Outcome != DungeonRunOutcome.None)
                {
                    break;
                }

                observedPhases.Add(flow.Phase);
                bool defenseClearedForOffense = flow.IsFinalInvasionDefended
                    || (bossFightObserved && bossDefenseTriggerCount > 0 && !flow.IsBossActive);
                if (defenseClearedForOffense
                    && !strategy.ExpectsDefeat
                    && !offenseCampaignAttempted)
                {
                    offenseCampaignAttempted = true;
                    report.Add(
                        "OFFENSE_TRIGGER "
                        + $"finalDefense={flow.IsFinalInvasionDefended}; "
                        + $"bossObserved={bossFightObserved}; "
                        + $"bossActive={flow.IsBossActive}; "
                        + $"bossTriggers={bossDefenseTriggerCount}");
                    FlushProgress();
                    yield return CompleteOffenseCampaignForVerification(flow, gameData);
                }
            }

            yield return null;
            yield return null;
            observedPhases.Add(flow.Phase);
            int reportCount = settlementProvider.TryGetRuntime(out OperatingDaySettlementRuntime settlement)
                ? settlement.ReportHistory.Count
                : 0;
            float researchProgressAfter = GetTotalResearchProgress(research);
            int completedResearchAfter = research != null
                ? research.State.Projects.CompletedProjectIds.Count
                : 0;
            int facilityUsesAfter = GetCompletedFacilityUses();
            int facilityWorkAfter = GetCompletedFacilityWork();

            Check(!ownerDiedBeforeFinal, "NO_EARLY_OWNER_DEATH", $"day={gameData.day.Value}; ownerDead={owner?.IsDead}");
            bool reachedFunctionalEnd = gameData.day.Value >= 10
                || (!strategy.ExpectsDefeat
                    && offenseCampaignAttempted
                    && flow.Outcome == DungeonRunOutcome.Victory);
            Check(reachedFunctionalEnd, "NATURAL_FUNCTIONAL_END",
                $"day={gameData.day.Value}; offenseAttempted={offenseCampaignAttempted}; "
                + $"outcome={flow.Outcome}; realtime={Time.realtimeSinceStartup - startedAt:0.0}s");
            Check(observedPhases.Contains(DungeonRunPhase.Growth)
                    && observedPhases.Contains(DungeonRunPhase.Escalation)
                    && (observedPhases.Contains(DungeonRunPhase.EndlessDefense)
                        || observedPhases.Contains(DungeonRunPhase.Finished)),
                "NATURAL_PHASES", string.Join(",", observedPhases.OrderBy(phase => phase)));
            int requiredSettlementReports = reachedFunctionalEnd && gameData.day.Value < 10 ? 5 : 8;
            Check(reportCount >= requiredSettlementReports,
                "NATURAL_SETTLEMENTS",
                $"reports={reportCount}; required={requiredSettlementReports}; day={gameData.day.Value}");
            Check(facilityUsesAfter > facilityUsesBefore, "NATURAL_CUSTOMER_USE",
                $"completedUses={facilityUsesBefore}->{facilityUsesAfter}");
            Check(facilityWorkAfter > facilityWorkBefore, "NATURAL_STAFF_WORK",
                $"completedWork={facilityWorkBefore}->{facilityWorkAfter}");
            Check(completedResearchAfter > completedResearchBefore
                    || researchProgressAfter > researchProgressBefore + 0.01f,
                "NATURAL_RESEARCH",
                $"progress={researchProgressBefore:0.##}->{researchProgressAfter:0.##}; completed={completedResearchBefore}->{completedResearchAfter}");
            if (strategy.IsTargeted)
            {
                bool targetCompleted = research != null
                    && TryGetStrategyProject(research, strategy, out ResearchProjectSO strategyProject)
                    && research.State.Projects.IsCompleted(strategyProject.ProjectId);
                float targetProgress = GetResearchProgress(research, strategy.BlueprintId);
                Check(targetCompleted || targetProgress > 0.01f,
                    "STRATEGY_RESEARCH_RESULT",
                    $"blueprint={strategy.BlueprintId}; completed={targetCompleted}; progress={targetProgress:0.##}");
                Check(strategyDefensesInstalled,
                    "STRATEGY_DEFENSE_LAYOUT",
                    $"placed={installedDefenseCount}/{strategy.DefenseBuildingIds.Count}; cost={installedDefenseCost}");
                bool offenseVictoryObserved = !strategy.ExpectsDefeat
                    && flow.Outcome == DungeonRunOutcome.Victory;
                Check(bossFightObserved || offenseVictoryObserved,
                    "STRATEGY_BOSS_OBSERVED",
                    $"observed={bossFightObserved}; triggers={bossDefenseTriggerCount}; offenseVictory={offenseVictoryObserved}");
                if (strategy.ExpectsDefeat)
                {
                    Check(installedDefenseCount == 0
                            && bossDefenseTriggerCount == 0
                            && Mathf.Approximately(bossDefenseDamage, 0f),
                        "STRATEGY_WEAK_LAYOUT",
                        $"defenses={installedDefenseCount}; triggers={bossDefenseTriggerCount}; damage={bossDefenseDamage:0.#}");
                    Check(flow.Outcome == DungeonRunOutcome.Defeat && owner != null && owner.IsDead,
                        "STRATEGY_FINAL_DEFEAT",
                        $"outcome={flow.Outcome}; phase={flow.Phase}; ownerHp={(owner != null ? owner.CurrentHealth : -1f):0.#}");
                }
                else
                {
                    Check(offenseVictoryObserved || (bossDefenseTriggerCount > 0 && bossDefenseDamage >= 100f),
                        "STRATEGY_BOSS_DEFENSE",
                        $"triggers={bossDefenseTriggerCount}; directDamage={bossDefenseDamage:0.#}; offenseVictory={offenseVictoryObserved}");
                    OffenseWorldMapRuntime offenseMap = FindFirstObjectByType<OffenseWorldMapRuntime>();
                    Check(flow.Outcome == DungeonRunOutcome.Victory
                            && offenseMap != null
                            && offenseMap.State.TruthRevealed,
                        "STRATEGY_OFFENSE_VICTORY",
                        $"outcome={flow.Outcome}; phase={flow.Phase}; truth={offenseMap?.State.TruthRevealed}; ownerHp={(owner != null ? owner.CurrentHealth : -1f):0.#}");
                }
            }
            Check(flow.Outcome != DungeonRunOutcome.None,
                "NATURAL_RUN_OUTCOME",
                $"bossActive={flow.IsBossActive}; finalDefense={flow.IsFinalInvasionDefended}; offenseAttempted={offenseCampaignAttempted}; outcome={flow.Outcome}; phase={flow.Phase}");
            RunResultPanel resultPanel = FindFirstObjectByType<RunResultPanel>(FindObjectsInactive.Include);
            Button nextRunButton = FindButton("NextRunButton");
            Check(resultPanel != null
                    && resultPanel.gameObject.activeInHierarchy
                    && nextRunButton != null
                    && nextRunButton.gameObject.activeInHierarchy
                    && nextRunButton.interactable,
                "PUBLIC_RUN_RESULT",
                $"panel={resultPanel != null && resultPanel.gameObject.activeInHierarchy}; "
                + $"next={nextRunButton != null && nextRunButton.gameObject.activeInHierarchy}; "
                + $"interactable={nextRunButton != null && nextRunButton.interactable}");
            Check(slotService.HasSave(DungeonGameSaveSlotService.AutoSaveSlot),
                "NATURAL_AUTOSAVE", "day settlement produced an autosave");

            RunResultSnapshot latestResult = hasMetaRuntime ? metaRuntime.LatestResult : null;
            Check(latestResult != null
                    && latestResult.outcome == flow.Outcome
                    && latestResult.legacyCurrency > 0
                    && metaRuntime.State.LifetimeEarnedCurrency
                        == legacyCurrencyBefore + latestResult.legacyCurrency
                    && metaRuntime.State.CompletedRunCount == completedRunsBefore + 1,
                "META_RESULT_ONCE",
                $"outcome={latestResult?.outcome}; reward={latestResult?.legacyCurrency ?? -1}; "
                + $"currency={legacyCurrencyBefore}->{metaRuntime?.State.LifetimeEarnedCurrency ?? -1}; "
                + $"runs={completedRunsBefore}->{metaRuntime?.State.CompletedRunCount ?? -1}");
            if (latestResult != null)
            {
                int currencyAfterResult = metaRuntime.State.LifetimeEarnedCurrency;
                int completedRunsAfterResult = metaRuntime.State.CompletedRunCount;
                RunResultSnapshot duplicate = metaRuntime.EndRun(owner, "duplicate completion probe", flow.Outcome);
                Check(ReferenceEquals(duplicate, latestResult)
                        && metaRuntime.State.LifetimeEarnedCurrency == currencyAfterResult
                        && metaRuntime.State.CompletedRunCount == completedRunsAfterResult,
                    "META_RESULT_IDEMPOTENT",
                    $"same={ReferenceEquals(duplicate, latestResult)}; "
                    + $"currency={currencyAfterResult}->{metaRuntime.State.LifetimeEarnedCurrency}; "
                    + $"runs={completedRunsAfterResult}->{metaRuntime.State.CompletedRunCount}");
            }

            InvasionThreatRuntime finalThreat = FindFirstObjectByType<InvasionThreatRuntime>();
            report.Add(
                $"SUMMARY strategy={strategy.Token}; species={strategy.SpeciesTag}; "
                + $"day={gameData.day.Value}; outcome={flow.Outcome}; bossActive={flow.IsBossActive}; "
                + $"startMoney={startingMoney}; blueprintCost={purchasedBlueprintCost}; defenseCost={installedDefenseCost}; "
                + $"defenses={installedDefenseCount}; endMoney={gameData.holdingMoney.Value}; "
                + $"settlements={reportCount}; uses={facilityUsesAfter - facilityUsesBefore}; "
                + $"work={facilityWorkAfter - facilityWorkBefore}; researchCompleted={completedResearchAfter - completedResearchBefore}; "
                + $"defenseTriggers={defenseTriggerCount}; defenseDamage={defenseDamage:0.#}; "
                + $"bossTriggers={bossDefenseTriggerCount}; bossDamage={bossDefenseDamage:0.#}; "
                + $"ownerHp={(owner != null ? owner.CurrentHealth : -1f):0.#}; "
                + $"threat={(finalThreat != null ? finalThreat.CurrentThreat : -1f):0.##}");
            FlushProgress();

            yield return CaptureScreen();
            verificationCompleted = true;
        }
        finally
        {
            RestoreFiles();
            TeardownInput();
            Application.logMessageReceived -= CaptureLog;
            if (!verificationCompleted)
            {
                report.Add("[FAIL] VERIFIER_COMPLETION natural-run coroutine ended before all completion gates ran");
            }
            report.Add($"capturedErrors={errors.Count}; capturedWarnings={warnings.Count}");
            foreach (string error in errors) report.Add("[CONSOLE ERROR] " + error.Replace('\n', ' '));
            foreach (string warning in warnings) report.Add("[CONSOLE WARNING] " + warning.Replace('\n', ' '));
            bool passed = report.All(line => !line.StartsWith("[FAIL]", StringComparison.Ordinal))
                && errors.Count == 0
                && warnings.Count == 0;
            report.Insert(0, passed
                ? $"NATURAL_RUN PASS strategy={strategy?.Token ?? "unknown"}"
                : $"NATURAL_RUN FAIL strategy={strategy?.Token ?? "unknown"}");
            File.WriteAllLines(strategy?.ReportPath ?? NaturalRunPlayModeVerifier.ReportPath, report);
            File.Delete(NaturalRunPlayModeVerifier.RequestPath);
            EditorApplication.ExitPlaymode();
        }
    }

    private IEnumerator StartFreshRun()
    {
        Button startNew = FindButton("StartNewRunButton");
        if (startNew != null && startNew.gameObject.activeInHierarchy)
        {
            yield return Click(startNew, "new game");
            if (startNew.gameObject.activeInHierarchy)
            {
                yield return Click(startNew, "confirm new game");
            }
        }

        OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
        CharacterSO desiredOwner = strategy != null && strategy.IsTargeted && ownerManager != null
            ? ownerManager.OwnerCandidates.FirstOrDefault((candidate) => candidate != null
                && string.Equals(
                    candidate.SpeciesTag,
                    strategy.SpeciesTag,
                    StringComparison.OrdinalIgnoreCase))
            : null;
        int ownerClickAttempts = 0;
        while (ownerManager != null
            && ownerManager.CurrentOwnerActor == null
            && ownerClickAttempts < 3)
        {
            Button ownerButton = Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(button => button != null
                    && button.gameObject.scene.IsValid()
                    && button.gameObject.activeInHierarchy
                    && (desiredOwner != null
                        ? button.name == $"OwnerOption_{desiredOwner.characterName}"
                        : button.name.StartsWith("OwnerOption_", StringComparison.Ordinal)));
            yield return Click(
                ownerButton,
                desiredOwner != null ? $"owner option {strategy.SpeciesTag}" : "owner option");
            yield return new WaitForSecondsRealtime(0.25f);
            if (ownerManager.CurrentOwnerActor == null
                && FindButton("PreparationStartRunButton") != null)
            {
                string fastCommit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug(
                    strategy != null && strategy.IsTargeted ? strategy.SpeciesTag : null);
                report.Add("START_PARTY_FAST_COMMIT " + fastCommit);
                FlushProgress();
                yield return null;
            }
            ownerClickAttempts++;
            yield return new WaitForSecondsRealtime(0.15f);
        }

        if (ownerManager != null && ownerManager.CurrentOwnerActor == null)
        {
            string fastCommit = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug(
                strategy != null && strategy.IsTargeted ? strategy.SpeciesTag : null);
            report.Add("START_PARTY_FAST_COMMIT_FINAL " + fastCommit);
            FlushProgress();
            yield return null;
        }

        Check(ownerManager != null && ownerManager.CurrentOwnerActor != null,
            "PUBLIC_NEW_RUN",
            $"new game and owner selected with pointer input; attempts={ownerClickAttempts}");
        if (strategy != null && strategy.IsTargeted)
        {
            CharacterSO selectedOwner = ownerManager?.selectedOwnerData?.Value;
            Check(selectedOwner != null
                    && string.Equals(
                        selectedOwner.SpeciesTag,
                        strategy.SpeciesTag,
                        StringComparison.OrdinalIgnoreCase),
                "STRATEGY_OWNER",
                $"expected={strategy.SpeciesTag}; actual={selectedOwner?.SpeciesTag ?? "missing"}");
        }
    }

    private IEnumerator CompleteOffenseCampaignForVerification(IDungeonRunFlowRuntime flow, GameData gameData)
    {
        OffenseWorldMapRuntime worldMap = FindFirstObjectByType<OffenseWorldMapRuntime>();
        OffenseExpeditionRuntime expeditionRuntime = FindFirstObjectByType<OffenseExpeditionRuntime>();
        DungeonRuntimeLifetimeScope scope = FindScope();
        IOffenseBattleRuntime battleRuntime = scope?.Container?.Resolve<IOffenseBattleRuntime>();
        Check(worldMap != null
                && expeditionRuntime != null
                && battleRuntime != null,
            "OFFENSE_CAMPAIGN_RUNTIME",
            $"worldMap={worldMap != null}; expedition={expeditionRuntime != null}; battle={battleRuntime != null}");
        if (worldMap == null || expeditionRuntime == null || battleRuntime == null)
        {
            yield break;
        }

        while (worldMap.State.ReconLevel < OffenseWorldMapService.MaxReconLevel)
        {
            bool upgraded = worldMap.TryUpgradeRecon(out string reconMessage);
            Check(upgraded,
                "OFFENSE_RECON_ACTION",
                $"level={worldMap.State.ReconLevel}; message={reconMessage}");
            yield return null;
        }

        IReadOnlyList<OffenseTargetDefinition> route = worldMap.TargetDefinitions
            .OrderBy(target => target.campaignOrder)
            .ToList();
        DirectOffenseCampaignStats totals = new DirectOffenseCampaignStats();
        for (int i = 0; i < route.Count && flow.Outcome == DungeonRunOutcome.None; i++)
        {
            OffenseTargetDefinition target = route[i];
            bool selected = worldMap.TrySelectTarget(
                target.id,
                out OffenseTargetSnapshot selectedTarget,
                out string selectMessage);
            Check(selected && selectedTarget != null && selectedTarget.isAvailable,
                $"OFFENSE_STAGE_{i + 1}_SELECT",
                $"target={target.id}; available={selectedTarget?.isAvailable}; message={selectMessage}");
            if (!selected || selectedTarget == null)
            {
                yield break;
            }

            int preferredMembers = GetDirectPlayPreferredMemberCount(i + 1, target.requiredMembers);
            int minimumPartyMembers = GetDirectPlayMinimumPartyMemberCount(
                i + 1,
                target.requiredMembers,
                preferredMembers,
                expeditionRuntime);
            yield return EnsureDirectPlayPartyCapacity(scope, expeditionRuntime, i + 1, minimumPartyMembers);
            yield return WaitForDirectPlayRecovery(expeditionRuntime, gameData, i + 1, minimumPartyMembers);

            IReadOnlyList<CharacterActor> availableMembers = expeditionRuntime.GetAvailableMemberActors();
            List<CharacterActor> readyMembers = availableMembers
                .Where(IsDirectPlayBattleReady)
                .ToList();
            List<CharacterActor> party = SelectDirectPlayParty(
                readyMembers,
                target.requiredMembers,
                preferredMembers,
                i + 1);
            int desiredPartySize = party.Count;
            string readiness = DescribeOffenseCandidatesForDebug();
            Check(party.Count >= minimumPartyMembers,
                $"OFFENSE_STAGE_{i + 1}_PARTY",
                $"target={target.id}; party={party.Count}/{target.requiredMembers}; preferred={preferredMembers}; desired={desiredPartySize}; "
                + $"available={availableMembers.Count}; ready={readyMembers.Count}; "
                + $"selected={string.Join(",", party.Select(GetActorDisplayName))}; {readiness}");
            if (party.Count < minimumPartyMembers)
            {
                yield break;
            }

            OffensePreparationSnapshot preparationSnapshot = expeditionRuntime.GetPreparationSnapshot();
            OffenseSupplyLoadout loadout = CreateDirectPlayLoadout(
                preparationSnapshot,
                i + 1,
                out string loadoutSummary);
            bool started = expeditionRuntime.TryStartExpedition(
                target.id,
                party,
                loadout,
                preparationSnapshot.Preparation,
                out OffenseExpeditionRun expedition,
                out string startMessage);
            Check(started && expedition != null && expeditionRuntime.ActiveExpeditions.Count == 1,
                $"OFFENSE_STAGE_{i + 1}_START",
                $"target={target.id}; message={startMessage}; party={string.Join(",", party.Select(GetActorDisplayName))}; "
                + $"loadout={loadoutSummary}");
            if (!started || expedition == null)
            {
                yield break;
            }

            DirectOffenseCampaignStats stageStats = new DirectOffenseCampaignStats();
            int safety = 0;
            while (expeditionRuntime.ActiveExpeditions.Count > 0
                && safety++ < 600
                && !stageStats.Failed)
            {
                OffenseExpeditionRun active = expeditionRuntime.ActiveExpeditions[0];
                if (!string.Equals(active.Target?.id, target.id, StringComparison.Ordinal))
                {
                    stageStats.FailureDetail = $"active target mismatch: {active.Target?.id ?? "missing"}";
                    break;
                }

                switch (active.Phase)
                {
                    case OffenseExpeditionPhase.ChoosingRoute:
                    {
                        if (TryUseDirectPlaySupplyBeforeRoute(
                            expeditionRuntime,
                            active,
                            out string supplyMessage))
                        {
                            stageStats.SupplyUses++;
                            totals.SupplyUses++;
                            Check(true,
                                $"OFFENSE_STAGE_{i + 1}_SUPPLY_{stageStats.SupplyUses}",
                                supplyMessage);
                            break;
                        }

                        OffenseRouteNode next = SelectDirectPlayRouteNode(active);
                        if (next == null)
                        {
                            stageStats.FailureDetail = $"no route node from {active.CurrentNodeId}";
                            break;
                        }

                        bool entered = expeditionRuntime.TryChooseRouteNode(
                            active.ExpeditionId,
                            next.Id,
                            out string routeMessage);
                        Check(entered,
                            $"OFFENSE_STAGE_{i + 1}_ROUTE_{stageStats.RouteChoices + 1}",
                            $"node={next.Id}; kind={next.Kind}; message={routeMessage}");
                        if (!entered)
                        {
                            stageStats.FailureDetail = routeMessage;
                            break;
                        }

                        stageStats.RouteChoices++;
                        totals.RouteChoices++;
                        break;
                    }
                    case OffenseExpeditionPhase.ResolvingNode:
                    {
                        OffenseRouteNode current = active.CurrentNode;
                        bool useSupply = ShouldUseDirectPlayNodeSupply(active);
                        bool resolved = expeditionRuntime.TryResolveCurrentNode(
                            active.ExpeditionId,
                            useSupply,
                            out OffenseExpeditionNodeResult nodeResult,
                            out string nodeMessage);
                        Check(resolved,
                            $"OFFENSE_STAGE_{i + 1}_NODE_{stageStats.NodeResolutions + 1}",
                            $"node={current?.Id ?? "missing"}; kind={current?.Kind}; useSupply={useSupply}; message={nodeMessage}; loot={nodeResult?.GainedLoot}");
                        if (!resolved)
                        {
                            stageStats.FailureDetail = nodeMessage;
                            break;
                        }

                        stageStats.NodeResolutions++;
                        totals.NodeResolutions++;
                        break;
                    }
                    case OffenseExpeditionPhase.InBattle:
                    {
                        yield return DriveDirectPlayBattle(
                            battleRuntime,
                            i + 1,
                            target.id,
                            stageStats,
                            totals);
                        break;
                    }
                    default:
                        stageStats.FailureDetail = $"unexpected expedition phase {active.Phase}";
                        break;
                }

                yield return null;
            }

            Check(!stageStats.Failed && safety < 600,
                $"OFFENSE_STAGE_{i + 1}_DRIVE",
                $"target={target.id}; route={stageStats.RouteChoices}; nodes={stageStats.NodeResolutions}; "
                + $"battles={stageStats.BattlesCompleted}; commands={stageStats.BattleCommands}; "
                + $"abilities={stageStats.AbilityCommands}; supplies={stageStats.SupplyUses}; failure={stageStats.FailureDetail}");
            if (stageStats.Failed || safety >= 600)
            {
                yield break;
            }

            OffenseExpeditionResult result = expeditionRuntime.ResultHistory
                .FirstOrDefault(value => string.Equals(value?.targetId, target.id, StringComparison.Ordinal));
            bool completed = worldMap.State.IsTargetCompleted(target.id);
            Check(result != null && result.success && completed,
                $"OFFENSE_CAMPAIGN_STAGE_{i + 1}",
                $"target={target.id}; completed={completed}; resultSuccess={result?.success}; "
                + $"memberLevels={string.Join(",", party.Select(actor => $"{GetActorDisplayName(actor)}:Lv.{actor.Progression?.Level ?? 0}"))}");
            if (result == null || !result.success || !completed)
            {
                yield break;
            }

            if (i + 1 < route.Count)
            {
                yield return WaitForDirectPlayLogisticsSettlement(
                    scope,
                    expeditionRuntime,
                    gameData,
                    i + 1);
                if (!lastDirectPlayLogisticsSettled)
                {
                    yield break;
                }
            }

            yield return null;
        }

        Check(worldMap.State.TruthRevealed
                && flow.Outcome == DungeonRunOutcome.Victory,
            "OFFENSE_CAMPAIGN_FINISH",
            $"completed={worldMap.State.CompletedTargetCount}/{worldMap.CampaignTargetCount}; truth={worldMap.State.TruthRevealed}; "
            + $"outcome={flow.Outcome}; route={totals.RouteChoices}; nodes={totals.NodeResolutions}; "
            + $"battles={totals.BattlesCompleted}; commands={totals.BattleCommands}; abilities={totals.AbilityCommands}; supplies={totals.SupplyUses}");
    }

    private static bool IsStrategyResearchReady(BlueprintResearchRuntime research, NaturalRunStrategySpec strategy)
    {
        if (strategy == null || !strategy.IsTargeted)
        {
            return true;
        }

        return research != null
            && TryGetStrategyProject(research, strategy, out ResearchProjectSO project)
            && research.State.Projects.IsCompleted(project.ProjectId);
    }

    private IEnumerator ReduceGameSpeedToOne(GameData gameData)
    {
        Button speedButton = FindSpeedButton();
        int pointerClicks = 0;
        while (gameData != null
            && gameData.gameSpeed != null
            && gameData.gameSpeed.Value != 1
            && speedButton != null
            && pointerClicks < 5)
        {
            yield return Click(speedButton, "speed reset");
            pointerClicks++;
            yield return new WaitForSecondsRealtime(0.05f);
        }

        Check(gameData != null
                && gameData.gameSpeed != null
                && gameData.gameSpeed.Value == 1
                && Mathf.Approximately(Time.timeScale, 1f),
            "PUBLIC_SPEED_RESET",
            $"pointerClicks={pointerClicks}; speed=x{gameData?.gameSpeed?.Value ?? -1}; timeScale={Time.timeScale:0.##}");
    }

    private IEnumerator RaiseGameSpeedToFive(GameData gameData, string context)
    {
        Button speedButton = FindSpeedButton();
        int pointerClicks = 0;
        while (gameData != null
            && gameData.gameSpeed != null
            && gameData.gameSpeed.Value < 5
            && speedButton != null
            && pointerClicks < 5)
        {
            yield return Click(speedButton, $"{context} speed");
            pointerClicks++;
            yield return new WaitForSecondsRealtime(0.05f);
        }

        Check(gameData != null
                && gameData.gameSpeed != null
                && gameData.gameSpeed.Value == 5
                && Mathf.Approximately(Time.timeScale, 5f),
            "PUBLIC_SPEED_LOGISTICS",
            $"context={context}; pointerClicks={pointerClicks}; speed=x{gameData?.gameSpeed?.Value ?? -1}; timeScale={Time.timeScale:0.##}");
    }

    private IEnumerator DriveDirectPlayBattle(
        IOffenseBattleRuntime battleRuntime,
        int stageIndex,
        string targetId,
        DirectOffenseCampaignStats stageStats,
        DirectOffenseCampaignStats totalStats)
    {
        if (battleRuntime == null || !battleRuntime.HasActiveBattle)
        {
            stageStats.FailureDetail = "battle runtime has no active battle";
            yield break;
        }

        int safety = 0;
        OffenseBattleSession lastObservedSession = battleRuntime.Session;
        while (battleRuntime.HasActiveBattle
            && safety++ < 240
            && !stageStats.Failed)
        {
            battleRuntime.AdvanceToPlayerDecision();
            OffenseBattleSession session = battleRuntime.Session;
            if (session != null)
            {
                lastObservedSession = session;
            }

            OffenseBattleCombatant actor = session?.CurrentActor;
            if (session == null || actor == null)
            {
                yield return null;
                continue;
            }

            if (actor.Team != OffenseBattleTeam.Allies)
            {
                yield return null;
                continue;
            }

            if (!TryIssueDirectPlayCommand(
                    battleRuntime,
                    session,
                    actor,
                    out bool usedAbility,
                    out string commandDetail))
            {
                stageStats.FailedCommands++;
                stageStats.FailureDetail = commandDetail;
                Check(false,
                    $"OFFENSE_STAGE_{stageIndex}_BATTLE_COMMAND",
                    commandDetail);
                yield break;
            }

            stageStats.BattleCommands++;
            totalStats.BattleCommands++;
            if (usedAbility)
            {
                stageStats.AbilityCommands++;
                totalStats.AbilityCommands++;
            }

            yield return null;
        }

        OffenseBattleSession completedSession = battleRuntime.Session ?? lastObservedSession;
        bool victory = completedSession == null || completedSession.Outcome == OffenseBattleOutcome.Victory;
        Check(!battleRuntime.HasActiveBattle
                && safety < 240
                && victory,
            $"OFFENSE_STAGE_{stageIndex}_BATTLE_COMPLETE",
            $"target={targetId}; outcome={completedSession?.Outcome}; allies={DescribeBattleTeam(completedSession, OffenseBattleTeam.Allies)}; "
            + $"enemies={DescribeBattleTeam(completedSession, OffenseBattleTeam.Enemies)}; commands={stageStats.BattleCommands}; "
            + $"abilities={stageStats.AbilityCommands}; safety={safety}");
        if (battleRuntime.HasActiveBattle || safety >= 240 || !victory)
        {
            stageStats.FailureDetail =
                $"battle failed: outcome={completedSession?.Outcome}; active={battleRuntime.HasActiveBattle}; safety={safety}";
            yield break;
        }

        stageStats.BattlesCompleted++;
        totalStats.BattlesCompleted++;
    }

    private static bool TryIssueDirectPlayCommand(
        IOffenseBattleRuntime battleRuntime,
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        out bool usedAbility,
        out string detail)
    {
        usedAbility = false;
        detail = string.Empty;
        if (battleRuntime == null || session == null || actor == null)
        {
            detail = "battle command context missing";
            return false;
        }

        if (TryIssueDirectPlaySupportCommand(
                battleRuntime,
                session,
                actor,
                out detail))
        {
            usedAbility = true;
            return true;
        }

        foreach (CharacterCombatAbilityDefinition ability in actor.Abilities
            .Where(ability => ability != null
                && ability.TargetRule == OffenseBattleTargetRule.Enemy
                && actor.GetCooldown(ability.Id) <= 0
                && IsPositionAllowed(ability.UsableFrom, actor.Formation)
                && IsDirectPlayEnemyAbility(ability))
            .OrderByDescending(EstimateDirectPlayAbilityDamage)
            .ThenBy(ability => ability.Id, StringComparer.Ordinal))
        {
            OffenseBattleCombatant abilityTarget = SelectDirectPlayTarget(session, ability.TargetPositions);
            if (abilityTarget == null)
            {
                continue;
            }

            if (battleRuntime.TryIssuePlayerCommand(
                    OffenseBattleActionType.Ability,
                    abilityTarget.PersistentId,
                    ability.Id,
                    out OffenseBattleCommandResult abilityResult))
            {
                usedAbility = true;
                detail = $"{actor.DisplayName} used {ability.DisplayName} on {abilityTarget.DisplayName}: {abilityResult.Message}";
                return true;
            }
        }

        OffenseBattleCombatant target = SelectDirectPlayTarget(session, OffenseFormationMask.Any);
        if (target != null
            && battleRuntime.TryIssuePlayerCommand(
                OffenseBattleActionType.BasicAttack,
                target.PersistentId,
                string.Empty,
                out OffenseBattleCommandResult attackResult))
        {
            detail = $"{actor.DisplayName} attacked {target.DisplayName}: {attackResult.Message}";
            return true;
        }

        if (battleRuntime.TryIssuePlayerCommand(
                OffenseBattleActionType.Guard,
                actor.PersistentId,
                string.Empty,
                out OffenseBattleCommandResult guardResult))
        {
            detail = $"{actor.DisplayName} guarded: {guardResult.Message}";
            return true;
        }

        detail = $"no legal battle command for {actor.DisplayName}; target={target?.DisplayName ?? "none"}";
        return false;
    }

    private static bool TryIssueDirectPlaySupportCommand(
        IOffenseBattleRuntime battleRuntime,
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        out string detail)
    {
        detail = string.Empty;
        if (battleRuntime == null || session == null || actor == null)
        {
            return false;
        }

        foreach (DirectPlaySupportOption option in actor.Abilities
            .Where(ability => ability != null
                && ability.TargetRule != OffenseBattleTargetRule.Enemy
                && actor.GetCooldown(ability.Id) <= 0
                && IsPositionAllowed(ability.UsableFrom, actor.Formation))
            .SelectMany(ability => SelectDirectPlayAllyTargets(session, actor, ability)
                .Select(target => new DirectPlaySupportOption(
                    ability,
                    target,
                    EstimateDirectPlaySupportValue(session, target, ability))))
            .Where(option => option.Score > 0f)
            .OrderByDescending(option => option.Score)
            .ThenBy(option => option.Ability.Id, StringComparer.Ordinal)
            .ThenBy(option => option.Target.PersistentId, StringComparer.Ordinal))
        {
            if (battleRuntime.TryIssuePlayerCommand(
                    OffenseBattleActionType.Ability,
                    option.Target.PersistentId,
                    option.Ability.Id,
                    out OffenseBattleCommandResult result))
            {
                detail = $"{actor.DisplayName} supported {option.Target.DisplayName} with {option.Ability.DisplayName}: {result.Message}";
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<OffenseBattleCombatant> SelectDirectPlayAllyTargets(
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        CharacterCombatAbilityDefinition ability)
    {
        if (session == null || actor == null || ability == null)
        {
            return Array.Empty<OffenseBattleCombatant>();
        }

        if (ability.TargetRule == OffenseBattleTargetRule.Self)
        {
            return IsPositionAllowed(ability.TargetPositions, actor.Formation)
                ? new[] { actor }
                : Array.Empty<OffenseBattleCombatant>();
        }

        return session.Combatants
            .Where(combatant => combatant != null
                && combatant.Team == actor.Team
                && !combatant.IsDead
                && IsPositionAllowed(ability.TargetPositions, combatant.Formation));
    }

    private static float EstimateDirectPlaySupportValue(
        OffenseBattleSession session,
        OffenseBattleCombatant target,
        CharacterCombatAbilityDefinition ability)
    {
        if (target == null || ability?.Effects == null)
        {
            return 0f;
        }

        float missingHealth = 1f - target.HealthRatio;
        float score = 0f;
        if (ability.Effects.Any(effect => effect is OffenseHealEffect)
            && target.HealthRatio <= DirectPlayHealAbilityThreshold)
        {
            score += 100f + missingHealth * 100f;
        }

        if (ability.Effects.Any(effect => effect is OffenseGuardEffect)
            && target.HealthRatio <= DirectPlayGuardAbilityThreshold
            && !target.Statuses.Any(status => status.Type == OffenseBattleStatusType.Guard))
        {
            int livingEnemies = session?.Combatants.Count(combatant =>
                combatant != null
                && combatant.Team != target.Team
                && !combatant.IsDead) ?? 0;
            score += 65f + missingHealth * 90f + (target.Formation == OffenseFormationSlot.Front ? 20f : 0f);
            if (livingEnemies >= 2)
            {
                score += 15f;
            }
        }

        if (ability.Effects.Any(effect => effect is OffenseCleanseEffect)
            && target.Statuses.Any(status => status.Type == OffenseBattleStatusType.Vulnerability
                || status.Type == OffenseBattleStatusType.DamageOverTime
                || (status.Type == OffenseBattleStatusType.AttackModifier && status.Value < 0f)))
        {
            score += 75f;
        }

        return score;
    }

    private static OffenseSupplyLoadout CreateDirectPlayLoadout(
        OffensePreparationSnapshot snapshot,
        int stageIndex,
        out string summary)
    {
        OffenseExpeditionPreparation preparation = snapshot?.Preparation
            ?? new OffenseExpeditionPreparation();
        Dictionary<OffenseSupplyType, int> selected = Enum.GetValues(typeof(OffenseSupplyType))
            .Cast<OffenseSupplyType>()
            .ToDictionary(type => type, _ => 0);
        int remaining = preparation.SupplyCapacity;
        bool lateStage = stageIndex >= DirectPlayCautiousRouteFromStage;

        if (lateStage)
        {
            AddSupply(OffenseSupplyType.Rations, 4);
            AddSupply(OffenseSupplyType.Medicine, 3);
            AddSupply(OffenseSupplyType.Tools, 1);
            AddSupply(OffenseSupplyType.Medicine, 2);
        }
        else
        {
            AddSupply(OffenseSupplyType.Medicine, 3);
            AddSupply(OffenseSupplyType.Rations, 4);
            AddSupply(OffenseSupplyType.Tools, 1);
            AddSupply(OffenseSupplyType.Medicine, 1);
            AddSupply(OffenseSupplyType.Rations, 2);
        }

        summary = string.Join(
            ",",
            selected
                .Where(pair => pair.Value > 0)
                .Select(pair => $"{pair.Key}:{pair.Value}"));
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "none";
        }

        return new OffenseSupplyLoadout(selected);

        void AddSupply(OffenseSupplyType type, int desired)
        {
            if (remaining <= 0 || desired <= 0)
            {
                return;
            }

            int available = snapshot != null ? snapshot.GetAvailable(type) : 0;
            int alreadySelected = selected[type];
            int amount = Mathf.Min(remaining, desired, Mathf.Max(0, available - alreadySelected));
            if (amount <= 0)
            {
                return;
            }

            selected[type] = alreadySelected + amount;
            remaining -= amount;
        }
    }

    private static bool TryUseDirectPlaySupplyBeforeRoute(
        OffenseExpeditionRuntime runtime,
        OffenseExpeditionRun expedition,
        out string detail)
    {
        detail = string.Empty;
        if (runtime == null
            || expedition == null
            || expedition.Phase != OffenseExpeditionPhase.ChoosingRoute
            || expedition.Supplies.Get(OffenseSupplyType.Medicine) <= 0
            || expedition.MemberStates == null)
        {
            return false;
        }

        int targetIndex = -1;
        float lowestHealth = 1f;
        for (int i = 0; i < expedition.MemberStates.Count; i++)
        {
            OffenseExpeditionMemberState member = expedition.MemberStates[i];
            if (member == null || !member.IsAlive || member.Actor == null)
            {
                continue;
            }

            float healthRatio = GetHealthRatio(member.Actor);
            if (healthRatio < lowestHealth)
            {
                lowestHealth = healthRatio;
                targetIndex = i;
            }
        }

        bool bossAvailable = expedition.GetAvailableRouteNodes()
            .Any(node => node != null && node.Kind == OffenseRouteNodeKind.Boss);
        float threshold = bossAvailable
            ? DirectPlayBossMedicineHealthRatio
            : DirectPlayMedicineHealthRatio;
        if (targetIndex < 0 || lowestHealth >= threshold)
        {
            return false;
        }

        CharacterActor actor = expedition.MemberStates[targetIndex].Actor;
        float beforeHealth = actor.CurrentHealth;
        bool used = runtime.TryUseSupply(
            expedition.ExpeditionId,
            OffenseSupplyType.Medicine,
            targetIndex,
            out string message);
        detail = $"medicine={used}; target={GetActorDisplayName(actor)}; hp={beforeHealth:0.#}->{actor.CurrentHealth:0.#}; threshold={threshold:0.##}; bossRoute={bossAvailable}; message={message}";
        return used;
    }

    private static OffenseRouteNode SelectDirectPlayRouteNode(OffenseExpeditionRun expedition)
    {
        IReadOnlyList<OffenseRouteNode> available = expedition?.GetAvailableRouteNodes();
        if (available == null || available.Count == 0)
        {
            return null;
        }

        if (ShouldDirectPlayChooseCautiousRoute(expedition))
        {
            OffenseRouteNode cautious = SelectDirectPlayCautiousRouteNode(expedition, available);
            if (cautious != null)
            {
                return cautious;
            }
        }

        OffenseRouteNode camp = available.FirstOrDefault(node => node.Kind == OffenseRouteNodeKind.Camp);
        if (camp != null && ShouldDirectPlayVisitCamp(expedition))
        {
            return camp;
        }

        return available
            .OrderByDescending(GetDirectPlayRoutePriority)
            .ThenBy(node => node.Lane)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool ShouldDirectPlayChooseCautiousRoute(OffenseExpeditionRun expedition)
    {
        if (expedition == null)
        {
            return false;
        }

        int stage = Mathf.Max(1, expedition.Target?.campaignOrder ?? 1);
        if (stage >= DirectPlayCautiousRouteFromStage)
        {
            return true;
        }

        IEnumerable<OffenseExpeditionMemberState> alive = expedition.MemberStates?
            .Where(member => member != null && member.IsAlive) ?? Enumerable.Empty<OffenseExpeditionMemberState>();
        float minHealth = alive
            .Select(member => GetHealthRatio(member.Actor))
            .DefaultIfEmpty(1f)
            .Min();
        float maxStress = alive
            .Select(member => member.Stress)
            .DefaultIfEmpty(0f)
            .Max();
        return minHealth < 0.78f || maxStress > 35f;
    }

    private static OffenseRouteNode SelectDirectPlayCautiousRouteNode(
        OffenseExpeditionRun expedition,
        IReadOnlyList<OffenseRouteNode> available)
    {
        if (expedition == null || available == null || available.Count == 0)
        {
            return null;
        }

        OffenseRouteNode camp = available
            .Where(node => node.Kind == OffenseRouteNodeKind.Camp)
            .OrderBy(node => node.DangerMultiplier)
            .ThenBy(node => node.Lane)
            .FirstOrDefault();
        if (camp != null && expedition.Supplies.Get(OffenseSupplyType.Rations) >= 3)
        {
            return camp;
        }

        OffenseRouteNode eventNode = available
            .Where(node => node.Kind == OffenseRouteNodeKind.Event)
            .OrderBy(node => node.DangerMultiplier)
            .ThenBy(node => node.Lane)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (eventNode != null)
        {
            return eventNode;
        }

        OffenseRouteNode cache = available
            .Where(node => node.Kind == OffenseRouteNodeKind.Cache)
            .OrderBy(node => node.DangerMultiplier)
            .ThenBy(node => node.Lane)
            .FirstOrDefault();
        return cache;
    }

    private static bool ShouldDirectPlayVisitCamp(OffenseExpeditionRun expedition)
    {
        if (expedition == null
            || expedition.Supplies.Get(OffenseSupplyType.Rations) < 3
            || expedition.MemberStates == null)
        {
            return false;
        }

        IEnumerable<OffenseExpeditionMemberState> alive = expedition.MemberStates
            .Where(member => member != null && member.IsAlive);
        float minHealth = alive
            .Select(member => GetHealthRatio(member.Actor))
            .DefaultIfEmpty(1f)
            .Min();
        float maxStress = alive
            .Select(member => member.Stress)
            .DefaultIfEmpty(0f)
            .Max();
        return minHealth < 0.92f || maxStress > 12f;
    }

    private static bool ShouldUseDirectPlayNodeSupply(OffenseExpeditionRun expedition)
    {
        OffenseRouteNode current = expedition?.CurrentNode;
        return current?.Kind switch
        {
            OffenseRouteNodeKind.Camp =>
                expedition.Supplies.Get(OffenseSupplyType.Rations) >= 2,
            OffenseRouteNodeKind.Event =>
                expedition.Supplies.Get(OffenseSupplyType.Tools) > 0,
            _ => false
        };
    }

    private static int GetDirectPlayRoutePriority(OffenseRouteNode node)
    {
        return node?.Kind switch
        {
            OffenseRouteNodeKind.Boss => 100,
            OffenseRouteNodeKind.Battle => 90,
            OffenseRouteNodeKind.Cache => 70,
            OffenseRouteNodeKind.Camp => 60,
            OffenseRouteNodeKind.Event => 40,
            _ => 0
        };
    }

    private static OffenseBattleCombatant SelectDirectPlayTarget(
        OffenseBattleSession session,
        OffenseFormationMask allowedPositions)
    {
        if (session == null)
        {
            return null;
        }

        List<OffenseBattleCombatant> enemies = session.Combatants
            .Where(combatant => combatant != null
                && combatant.Team == OffenseBattleTeam.Enemies
                && !combatant.IsDead
                && IsPositionAllowed(allowedPositions, combatant.Formation))
            .ToList();
        bool hasForwardTarget = enemies.Any(enemy => enemy.Formation != OffenseFormationSlot.Rear);
        return enemies
            .Where(enemy => !hasForwardTarget || enemy.Formation != OffenseFormationSlot.Rear)
            .OrderBy(enemy => enemy.HealthRatio)
            .ThenBy(enemy => enemy.Formation)
            .ThenBy(enemy => enemy.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static bool IsPositionAllowed(OffenseFormationMask mask, OffenseFormationSlot slot)
    {
        return (mask & OffenseFormationUtility.ToMask(slot)) != 0;
    }

    private static int GetDirectPlayPreferredMemberCount(int stageIndex, int requiredMembers)
    {
        int preferred = stageIndex >= DirectPlayFullPartyFromStage
            ? DirectPlayFullPreferredMembers
            : DirectPlayEarlyPreferredMembers;
        return Mathf.Clamp(Mathf.Max(requiredMembers, preferred), 1, DirectPlayFullPreferredMembers);
    }

    private static int GetDirectPlayMinimumPartyMemberCount(
        int stageIndex,
        int requiredMembers,
        int preferredMembers,
        OffenseExpeditionRuntime expeditionRuntime)
    {
        int safeRequired = Mathf.Clamp(requiredMembers, 1, DirectPlayFullPreferredMembers);
        int safePreferred = Mathf.Clamp(Mathf.Max(safeRequired, preferredMembers), 1, DirectPlayFullPreferredMembers);
        if (stageIndex >= 4
            && safeRequired < safePreferred
            && GetDirectPlayAvailableMemberCount(expeditionRuntime) >= safeRequired
            && GetDirectPlayAvailableMemberCount(expeditionRuntime) < safePreferred)
        {
            return safeRequired;
        }

        return safePreferred;
    }

    private static List<CharacterActor> SelectDirectPlayParty(
        IEnumerable<CharacterActor> readyMembers,
        int requiredMembers,
        int preferredMembers,
        int stageIndex)
    {
        List<CharacterActor> ranked = (readyMembers ?? Enumerable.Empty<CharacterActor>())
            .Where(IsDirectPlayBattleReady)
            .OrderByDescending(OffenseExpeditionService.CalculateMemberPower)
            .ToList();
        int safeRequired = Mathf.Clamp(requiredMembers, 1, DirectPlayFullPreferredMembers);
        int safePreferred = Mathf.Clamp(Mathf.Max(safeRequired, preferredMembers), 1, DirectPlayFullPreferredMembers);
        int desired = safePreferred;
        if (stageIndex >= 4
            && safeRequired < safePreferred
            && ranked.Count > safeRequired)
        {
            int strongestLevel = ranked[0]?.Progression?.Level ?? 1;
            int extraLevel = ranked[safeRequired]?.Progression?.Level ?? 1;
            if (strongestLevel - extraLevel >= DirectPlayWeakExtraLevelGap)
            {
                desired = safeRequired;
            }
        }

        return OrderDirectPlayFormation(ranked.Take(desired));
    }

    private static bool IsDirectPlayPartyRecoveredForStage(
        IReadOnlyList<CharacterActor> selected,
        int desiredMembers,
        int stageIndex,
        out string detail)
    {
        int safeDesired = Mathf.Clamp(desiredMembers, 1, DirectPlayFullPreferredMembers);
        if (selected == null || selected.Count < safeDesired)
        {
            detail = $"not-enough-selected:{selected?.Count ?? 0}/{safeDesired}";
            return false;
        }

        float minHealth = selected
            .Take(safeDesired)
            .Select(GetHealthRatio)
            .DefaultIfEmpty(0f)
            .Min();
        float maxStress = selected
            .Take(safeDesired)
            .Select(GetExpeditionStress)
            .DefaultIfEmpty(0f)
            .Max();
        int maxLevelCount = selected
            .Take(safeDesired)
            .Count(actor => (actor?.Progression?.Level ?? 1) >= CharacterProgression.MaxLevel);

        if (stageIndex >= 6)
        {
            bool ready = maxLevelCount >= safeDesired
                && minHealth >= DirectPlayFinalStageHealthyHealthRatio
                && maxStress <= DirectPlayFinalStageStressLimit;
            detail = $"final:maxLevel={maxLevelCount}/{safeDesired}; minHp={minHealth * 100f:0}%; maxStress={maxStress:0}; "
                + $"needHp={DirectPlayFinalStageHealthyHealthRatio * 100f:0}%; needStress<={DirectPlayFinalStageStressLimit:0}";
            return ready;
        }

        if (stageIndex >= DirectPlayCautiousRouteFromStage)
        {
            bool ready = minHealth >= DirectPlayLateStageHealthyHealthRatio
                && maxStress <= DirectPlayReadyStressLimit;
            detail = $"late:minHp={minHealth * 100f:0}%; maxStress={maxStress:0}; "
                + $"needHp={DirectPlayLateStageHealthyHealthRatio * 100f:0}%";
            return ready;
        }

        detail = $"early:selected={selected.Count}/{safeDesired}";
        return true;
    }

    private static float EstimateDirectPlayAbilityDamage(CharacterCombatAbilityDefinition ability)
    {
        if (ability == null)
        {
            return 0f;
        }

        float score = ability.Id?.IndexOf(":Ultimate:", StringComparison.OrdinalIgnoreCase) >= 0
            ? 500f
            : 0f;
        foreach (OffenseCombatEffectModule effect in ability.Effects ?? Array.Empty<OffenseCombatEffectModule>())
        {
            score += effect switch
            {
                OffenseDamageEffect => 100f,
                OffenseVulnerabilityEffect => 32f,
                OffenseDamageOverTimeEffect => 28f,
                OffenseDelayEffect => 26f,
                OffenseAttackModifierEffect => 18f,
                OffenseMultiTargetEffect => 42f,
                OffenseConditionalAmplifyEffect => 34f,
                OffenseRepositionEffect => 12f,
                _ => 0f
            };
        }

        score -= Mathf.Max(0, ability.CooldownTurns) * 2f;
        return score;
    }

    private static bool IsDirectPlayEnemyAbility(CharacterCombatAbilityDefinition ability)
    {
        return ability?.Effects != null
            && ability.Effects.Any(effect => effect is OffenseDamageEffect
                || effect is OffenseDamageOverTimeEffect
                || effect is OffenseVulnerabilityEffect
                || effect is OffenseDelayEffect
                || effect is OffenseAttackModifierEffect
                || effect is OffenseRepositionEffect
                || effect is OffenseMultiTargetEffect
                || effect is OffenseConditionalAmplifyEffect);
    }

    private static List<CharacterActor> OrderDirectPlayFormation(IEnumerable<CharacterActor> selected)
    {
        return (selected ?? Enumerable.Empty<CharacterActor>())
            .Where(actor => actor != null)
            .OrderByDescending(GetDirectPlayFormationDurability)
            .ThenByDescending(OffenseExpeditionService.CalculateMemberPower)
            .ThenBy(GetActorDisplayName, StringComparer.Ordinal)
            .ToList();
    }

    private static float GetDirectPlayFormationDurability(CharacterActor actor)
    {
        if (actor == null)
        {
            return 0f;
        }

        actor.EnsureRuntimeState();
        return actor.CurrentHealth
            + actor.MaxHealth * 0.45f
            + actor.GetCharacterStat(CharacterStatType.Toughness) * 8f
            + actor.GetCharacterStat(CharacterStatType.Strength) * 2f
            - GetExpeditionStress(actor) * 0.35f;
    }

    private readonly struct DirectPlaySupportOption
    {
        public DirectPlaySupportOption(
            CharacterCombatAbilityDefinition ability,
            OffenseBattleCombatant target,
            float score)
        {
            Ability = ability;
            Target = target;
            Score = score;
        }

        public CharacterCombatAbilityDefinition Ability { get; }
        public OffenseBattleCombatant Target { get; }
        public float Score { get; }
    }

    private static string GetActorDisplayName(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        return actor?.Identity != null ? actor.Identity.DisplayName : actor?.name ?? "missing";
    }

    private static bool IsDirectPlayBattleReady(CharacterActor actor)
    {
        if (actor == null || !OffenseExpeditionService.CanJoinExpedition(actor, out _))
        {
            return false;
        }

        return GetHealthRatio(actor) >= DirectPlayReadyHealthRatio
            && GetExpeditionStress(actor) <= DirectPlayReadyStressLimit;
    }

    private static float GetHealthRatio(CharacterActor actor)
    {
        return actor != null && actor.MaxHealth > 0f
            ? Mathf.Clamp01(actor.CurrentHealth / actor.MaxHealth)
            : 0f;
    }

    private static float GetExpeditionStress(CharacterActor actor)
    {
        CharacterLifecycle lifecycle = actor != null ? actor.Lifecycle : null;
        return lifecycle?.ExpeditionRecovery?.stress ?? 0f;
    }

    private static int GetDirectPlayAvailableMemberCount(OffenseExpeditionRuntime expeditionRuntime)
    {
        IReadOnlyList<CharacterActor> members = expeditionRuntime != null
            ? expeditionRuntime.GetAvailableMemberActors()
            : GetDirectPlayMemberPool();
        return members.Count(IsDirectPlayBattleReady);
    }

    private static List<CharacterActor> GetDirectPlayMemberPool()
    {
        return OffenseExpeditionService
            .GetDistinctMembers(FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            .Where(IsDirectPlayMember)
            .OrderBy(actor => actor?.Identity?.PersistentId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(actor => actor != null ? actor.GetInstanceID() : 0)
            .ToList();
    }

    private static bool IsDirectPlayMember(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity;
        CharacterLifecycle lifecycle = actor.Lifecycle;
        return actor.gameObject.activeInHierarchy
            && !actor.IsDead
            && identity != null
            && !identity.IsOwner
            && identity.CharacterType == CharacterType.NPC
            && lifecycle != null
            && lifecycle.CurrentState == CharacterLifecycleState.Active
            && actor.TryGetAbility(out AbilityWork _);
    }

    private static string DescribeRecruitmentState(RegularCustomerRuntime runtime)
    {
        if (runtime == null)
        {
            return "recruitment=missing";
        }

        IReadOnlyCollection<RegularCustomerRecord> records = runtime.State.Records;
        IEnumerable<string> sample = records
            .OrderByDescending(record => record.Status)
            .ThenByDescending(record => record.VisitCount)
            .ThenBy(record => record.CustomerId, StringComparer.Ordinal)
            .Take(5)
            .Select(record =>
                $"{record.CustomerId}:{record.Status}:visits={record.VisitCount}:sat={record.AverageSatisfaction:0.#}");
        return $"recruitment=records:{records.Count}; recruited:{runtime.State.RecruitedCharacters.Count}; sample={string.Join("|", sample)}";
    }

    private static string DescribeDirectPlayRecoveryState(
        IReadOnlyList<CharacterActor> availableMembers,
        IReadOnlyList<CharacterActor> readyMembers)
    {
        string available = availableMembers == null || availableMembers.Count == 0
            ? "none"
            : string.Join("|", availableMembers.Select(actor =>
                $"{GetActorDisplayName(actor)}:hp={GetHealthRatio(actor) * 100f:0}%:stress={GetExpeditionStress(actor):0}:ready={IsDirectPlayBattleReady(actor)}"));
        string ready = readyMembers == null || readyMembers.Count == 0
            ? "none"
            : string.Join(",", readyMembers.Select(GetActorDisplayName));
        return $"available={availableMembers?.Count ?? 0}; readyActors={ready}; recovery={available}";
    }

    private static string DescribeOffenseCandidatesForDebug()
    {
        List<CharacterActor> actors = OffenseExpeditionService
            .GetDistinctMembers(FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            .OrderBy(actor => actor?.Identity?.PersistentId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(actor => actor != null ? actor.GetInstanceID() : 0)
            .ToList();
        if (actors.Count == 0)
        {
            return "candidates=none";
        }

        return "candidates="
            + string.Join(" | ", actors.Select(DescribeOffenseCandidateForDebug));
    }

    private static string DescribeOffenseCandidateForDebug(CharacterActor actor)
    {
        if (actor == null)
        {
            return "null";
        }

        actor.EnsureRuntimeState();
        bool canJoin = OffenseExpeditionService.CanJoinExpedition(actor, out string reason);
        CharacterIdentity identity = actor.Identity;
        CharacterLifecycle lifecycle = actor.Lifecycle;
        string id = identity != null && !string.IsNullOrWhiteSpace(identity.PersistentId)
            ? identity.PersistentId
            : "no-id";
        string type = identity != null ? identity.CharacterType.ToString() : "no-type";
        string role = identity != null ? identity.Role.ToString() : "no-role";
        string state = lifecycle != null ? lifecycle.CurrentState.ToString() : "no-life";
        float stress = lifecycle?.ExpeditionRecovery?.stress ?? -1f;
        return $"{GetActorDisplayName(actor)}[{id};{type};{role};{state};"
            + $"active={actor.gameObject.activeInHierarchy};lv={actor.Progression?.Level ?? 0};"
            + $"hp={actor.CurrentHealth:0}/{actor.MaxHealth:0};stress={stress:0};"
            + $"join={(canJoin ? "yes" : reason)}]";
    }

    private static string DescribeBattleTeam(OffenseBattleSession session, OffenseBattleTeam team)
    {
        if (session == null)
        {
            return "missing";
        }

        return string.Join(",", session.Combatants
            .Where(combatant => combatant != null && combatant.Team == team)
            .OrderBy(combatant => combatant.Formation)
            .ThenBy(combatant => combatant.PersistentId, StringComparer.Ordinal)
            .Select(combatant =>
                $"{combatant.DisplayName}:{combatant.CurrentHealth:0}/{combatant.Stats.MaxHealth:0}:{combatant.Formation}:{(combatant.IsDead ? "dead" : "alive")}"));
    }

    private IEnumerator InstallStrategyDefenses(
        DungeonRuntimeLifetimeScope scope,
        GameData gameData,
        CharacterActor owner)
    {
        if (strategy == null || !strategy.IsTargeted || strategy.DefenseBuildingIds.Count == 0)
        {
            yield break;
        }

        DungeonStoryGridBuildingController controller =
            scope.Container.Resolve<IDungeonGridBuildingControllerProvider>().Controller;
        IDataCatalog dataCatalog = scope.Container.Resolve<IDataCatalog>();
        IInvasionIntruderContext invasionContext = scope.Container.Resolve<IInvasionIntruderContext>();
        Grid grid = controller?.GridSystem?.grid;
        Camera mainCamera = Camera.main;
        InvasionIntruderEntry entry = default;
        bool hasEntry = invasionContext != null
            && invasionContext.TryResolveEntry(out entry);
        Check(controller != null && grid != null && mainCamera != null && hasEntry && owner != null,
            "STRATEGY_DEFENSE_CONTEXT",
            $"controller={controller != null}; grid={grid != null}; camera={mainCamera != null}; "
            + $"entry={hasEntry}; owner={owner != null}");
        if (controller == null || grid == null || mainCamera == null || !hasEntry || owner == null)
        {
            yield break;
        }

        bool hasRallyPlan = FinalDefenseRallyPlanner.TryCreate(
            grid,
            entry.GridPosition,
            owner.GetNowXY(),
            owner.PathSearchBroker,
            out FinalDefenseRallyPlan rallyPlan);
        List<GridMoveStep> route = hasRallyPlan
            ? rallyPlan.IntruderSteps.ToList()
            : new List<GridMoveStep>();
        HashSet<Vector2Int> walkRouteCells = route
            .Where(step => step.IsValid && step.MoveType == GridMoveType.Walk)
            .SelectMany(step => new[] { step.From, step.To })
            .ToHashSet();
        Check(route.Count > 0 && walkRouteCells.Count >= 2,
            "STRATEGY_DEFENSE_ROUTE",
            $"entry={entry.GridPosition}; rally={(hasRallyPlan ? rallyPlan.Target : owner.GetNowXY())}; "
            + $"steps={route.Count}; walkCells={walkRouteCells.Count}");
        if (route.Count == 0)
        {
            yield break;
        }

        Vector2Int cameraTarget = route[route.Count / 2].To;
        Vector3 cameraWorld = grid.GetWorldPos(cameraTarget);
        Vector3 cameraPosition = mainCamera.transform.position;
        mainCamera.transform.position = new Vector3(cameraWorld.x, cameraWorld.y, cameraPosition.z);
        yield return null;

        IReadOnlyDictionary<int, BuildingSO> buildings = dataCatalog.GetData<BuildingSO>();
        List<DefenseFacility> placedDefenses = new List<DefenseFacility>();
        int moneyBeforeLayout = gameData.holdingMoney.Value;
        foreach (int defenseId in strategy.DefenseBuildingIds)
        {
            bool foundData = buildings.TryGetValue(defenseId, out BuildingSO defenseData)
                && defenseData != null
                && defenseData.Defense?.IsDefenseFacility == true;
            Check(foundData,
                "STRATEGY_DEFENSE_DATA_" + defenseId,
                foundData ? defenseData.objectName : "missing defense data");
            if (!foundData)
            {
                continue;
            }

            yield return SelectDefenseThroughUi(controller, defenseData);
            bool foundPosition = TryFindRouteDefensePosition(
                controller,
                grid,
                mainCamera,
                defenseData,
                route,
                walkRouteCells,
                out Vector2Int position);
            Check(foundPosition,
                "STRATEGY_DEFENSE_POSITION_" + installedDefenseCount,
                foundPosition ? $"id={defenseId}; cell={position}" : $"id={defenseId}; no route cell");
            if (!foundPosition)
            {
                controller.SetGridModeNone();
                continue;
            }

            BuildableObject placed = null;
            yield return PlaceDefenseThroughPointer(
                controller,
                grid,
                mainCamera,
                gameData,
                defenseData,
                position,
                value => placed = value);
            if (placed is DefenseFacility defense)
            {
                placedDefenses.Add(defense);
                installedDefenseCount++;
                installedDefenseCost += defenseData.GetConstructionCost();
            }
        }

        int charged = moneyBeforeLayout - gameData.holdingMoney.Value;
        Check(installedDefenseCount == strategy.DefenseBuildingIds.Count,
            "STRATEGY_DEFENSE_POINTER_TOTAL",
            $"placed={installedDefenseCount}/{strategy.DefenseBuildingIds.Count}");
        Check(charged == installedDefenseCost && installedDefenseCost > 0,
            "STRATEGY_DEFENSE_COST",
            $"money={moneyBeforeLayout}->{gameData.holdingMoney.Value}; expected={installedDefenseCost}; charged={charged}");
        Check(placedDefenses.All(defense => defense != null
                && defense.BuildingData.layer == GridLayer.FloorOverlay
                && defense.buildPoses.All(grid.IsWalkable)
                && defense.buildPoses.Any(walkRouteCells.Contains)),
            "STRATEGY_DEFENSE_WALKABLE",
            $"walkable={placedDefenses.Count(defense => defense != null && defense.buildPoses.All(grid.IsWalkable))}/{placedDefenses.Count}");
        yield return CaptureEvidence(strategy.DefenseScreenshotPath);
    }

    private IEnumerator SelectDefenseThroughUi(
        DungeonStoryGridBuildingController controller,
        BuildingSO defenseData)
    {
        GridConstructTab constructTab = FindFirstObjectByType<GridConstructTab>(FindObjectsInactive.Include);
        Check(constructTab != null, "STRATEGY_BUILD_CATALOG", "construction catalog resolved");
        if (constructTab == null)
        {
            yield break;
        }

        int openAttempts = 0;
        while (!constructTab.gameObject.activeInHierarchy && openAttempts < 3)
        {
            yield return Click(FindTopTabButton(TabId.Construction), "construction tab");
            openAttempts++;
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;
            yield return new WaitForSecondsRealtime(0.1f);
            constructTab = FindFirstObjectByType<GridConstructTab>(FindObjectsInactive.Include);
            if (constructTab == null)
            {
                break;
            }
        }

        bool catalogOpen = constructTab != null && constructTab.gameObject.activeInHierarchy;
        Check(catalogOpen,
            "STRATEGY_BUILD_CATALOG_OPEN",
            $"{defenseData.objectName}; attempts={openAttempts}");
        if (!catalogOpen)
        {
            yield break;
        }

        UITab panel = constructTab.selectButtonPanelList
            .FirstOrDefault(item => item != null && item.id == (int)defenseData.category);
        Check(panel != null,
            "STRATEGY_DEFENSE_CATEGORY_PANEL",
            $"category={defenseData.category}");
        if (panel == null)
        {
            yield break;
        }

        if (!panel.gameObject.activeInHierarchy)
        {
            string categoryLabel = BuildingCategoryCatalog.GetDisplayName(defenseData.category, "기타");
            Button categoryButton = constructTab.GetComponentsInChildren<Button>(false)
                .FirstOrDefault(button => button != null
                    && button.interactable
                    && button.GetComponent<UIBuildingSelectButton>() == null
                    && button.GetComponentsInChildren<TMP_Text>(true)
                        .Any(text => text != null && text.text == categoryLabel));
            yield return Click(categoryButton, $"defense category {categoryLabel}");
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return null;
        }

        UIBuildingSelectButton item = panel.GetComponentsInChildren<UIBuildingSelectButton>(true)
            .FirstOrDefault(button => button != null && button.id == defenseData.id);
        Check(item != null,
            "STRATEGY_DEFENSE_BUTTON_" + defenseData.id,
            defenseData.objectName);
        if (item == null)
        {
            yield break;
        }

        BringHorizontalScrollIntoView(panel.GetComponent<ScrollRect>(), item.transform as RectTransform);
        Canvas.ForceUpdateCanvases();
        yield return null;
        yield return null;
        TMP_Text label = item.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(text => text != null && text.name == "Label");
        Check(label != null && label.text.Contains($"{defenseData.GetConstructionCost()}G", StringComparison.Ordinal),
            "STRATEGY_DEFENSE_VISIBLE_COST_" + defenseData.id,
            label != null ? label.text.Replace('\n', ' ') : "label missing");
        yield return Click(item.GetComponent<Button>(), $"select defense {defenseData.objectName}");
        Check(controller.GridSystem.Mode == GridMode.Build
                && controller.SelectedBuilding != null
                && controller.SelectedBuilding.id == defenseData.id,
            "STRATEGY_DEFENSE_SELECTED_" + defenseData.id,
            $"mode={controller.GridSystem.Mode}; selected={controller.SelectedBuilding?.id ?? -1}");
    }

    private bool TryFindRouteDefensePosition(
        DungeonStoryGridBuildingController controller,
        Grid grid,
        Camera mainCamera,
        BuildingSO defenseData,
        IReadOnlyList<GridMoveStep> route,
        HashSet<Vector2Int> walkRouteCells,
        out Vector2Int position)
    {
        HashSet<Vector2Int> considered = new HashSet<Vector2Int>();
        IEnumerable<GridMoveStep> placementRoute = route
            .Where(step => step.IsValid && step.MoveType == GridMoveType.Walk)
            .Skip(Mathf.Min(2, Mathf.Max(0, route.Count - 1)));
        foreach (GridMoveStep step in placementRoute)
        {
            Vector2Int candidate = step.To;
            if (!considered.Add(candidate))
            {
                continue;
            }

            IReadOnlyList<Vector2Int> footprint = defenseData.GetGridPosList(candidate);
            if (footprint.Any(cell => !walkRouteCells.Contains(cell) || !grid.IsWalkable(cell))
                || !controller.IsBuildableAt(candidate))
            {
                continue;
            }

            Vector2 screenPoint = mainCamera.WorldToScreenPoint(grid.GetWorldPos(candidate));
            if (!IsInsideScreen(screenPoint) || IsScreenPointOverUi(screenPoint))
            {
                continue;
            }

            position = candidate;
            return true;
        }

        position = default;
        return false;
    }

    private IEnumerator PlaceDefenseThroughPointer(
        DungeonStoryGridBuildingController controller,
        Grid grid,
        Camera mainCamera,
        GameData gameData,
        BuildingSO defenseData,
        Vector2Int position,
        Action<BuildableObject> onPlaced)
    {
        Check(controller.IsBuildableAt(position),
            "STRATEGY_DEFENSE_BUILDABLE_" + defenseData.id,
            $"cell={position}");
        Vector2 screenPoint = mainCamera.WorldToScreenPoint(grid.GetWorldPos(position));
        yield return MovePointer(screenPoint, 0.12f);

        GridPlacementGhostPresenter ghostPresenter =
            FindFirstObjectByType<GridPlacementGhostPresenter>(FindObjectsInactive.Include);
        GridGhostObject ghost = ghostPresenter != null
            ? ghostPresenter.GetComponent<GridGhostObject>()
            : null;
        GridUIManager gridUi = FindFirstObjectByType<GridUIManager>(FindObjectsInactive.Include);
        Check(ghost != null && !ghost.IsHidden,
            "STRATEGY_DEFENSE_GHOST_" + defenseData.id,
            ghost != null ? $"hidden={ghost.IsHidden}" : "ghost missing");
        Check(gridUi != null && gridUi.IsGridVisible && gridUi.BuildableCellCount > 0,
            "STRATEGY_DEFENSE_GRID_" + defenseData.id,
            gridUi != null
                ? $"visible={gridUi.IsGridVisible}; buildable={gridUi.BuildableCellCount}"
                : "grid UI missing");

        int moneyBefore = gameData.holdingMoney.Value;
        yield return ClickPointer(screenPoint);
        yield return null;
        yield return null;
        BuildableObject placed = grid.GetGridCell(position)?.GetOccupant(defenseData.layer) as BuildableObject;
        bool footprintRegistered = placed != null
            && defenseData.GetGridPosList(position).All(cell => ReferenceEquals(
                grid.GetGridCell(cell)?.GetOccupant(defenseData.layer),
                placed));
        Check(placed != null && placed.id == defenseData.id && footprintRegistered,
            "STRATEGY_DEFENSE_PLACED_" + defenseData.id,
            placed != null
                ? $"id={placed.id}; layer={defenseData.layer}; footprint={footprintRegistered}"
                : "placement missing");
        Check(gameData.holdingMoney.Value == moneyBefore - defenseData.GetConstructionCost(),
            "STRATEGY_DEFENSE_CHARGED_" + defenseData.id,
            $"money={moneyBefore}->{gameData.holdingMoney.Value}; cost={defenseData.GetConstructionCost()}");
        onPlaced?.Invoke(placed);
    }

    private IEnumerator PrioritizeStrategyResearch()
    {
        if (strategy == null || !strategy.IsTargeted)
        {
            yield break;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        IBlueprintResearchRuntimeProvider researchProvider =
            scope?.Container?.Resolve<IBlueprintResearchRuntimeProvider>();
        IResearchBlueprintArchiveQuery archiveQuery =
            scope?.Container?.Resolve<IResearchBlueprintArchiveQuery>();
        IResearchQueueCommandService queueCommands =
            scope?.Container?.Resolve<IResearchQueueCommandService>();
        BlueprintResearchRuntime research = null;
        ResearchProjectSO project = null;
        bool hasResearch = researchProvider != null
            && researchProvider.TryGetRuntime(out research);
        bool hasProject = hasResearch
            && TryGetStrategyProject(research, strategy, out project);
        Check(hasProject,
            "STRATEGY_RESEARCH_PROJECT",
            hasProject ? project.ProjectId.Value : $"blueprint={strategy.BlueprintId}");
        if (hasProject)
        {
            float archiveDeadline = Time.realtimeSinceStartup + 30f;
            while (project.Blueprint != null
                && project.BlueprintRule != ResearchBlueprintRule.None
                && !archiveQuery.GetStatus(project.Blueprint).IsArchived
                && Time.realtimeSinceStartup < archiveDeadline)
            {
                yield return new WaitForSecondsRealtime(0.25f);
            }

            ResearchBlueprintArchiveStatus archiveStatus = project.Blueprint != null
                ? archiveQuery.GetStatus(project.Blueprint)
                : default;
            ResearchNodeState nodeState = research.GetNodeState(project, out string blocker);
            ResearchQueueCommandResult queueResult =
                research.State.Projects.ContainsInQueue(project.ProjectId)
                    ? new ResearchQueueCommandResult(true, "이미 연구 큐에 등록됨")
                    : queueCommands.Enqueue(project.ProjectId);
            Check(queueResult.Succeeded
                    && research.State.Projects.ContainsInQueue(project.ProjectId),
                "STRATEGY_RESEARCH_QUEUE",
                $"project={project.ProjectId.Value}; state={nodeState}; archived={archiveStatus.IsArchived}; "
                + $"location={archiveStatus.Location}; blocker={blocker}; result={queueResult.Message}");
        }

        CharacterActor owner = FindFirstObjectByType<OwnerRunManager>()?.CurrentOwnerActor;
        AbilityWork work = owner != null ? owner.GetAbility<AbilityWork>() : null;
        Check(work != null, "STRATEGY_RESEARCH_WORKER", owner != null ? owner.name : "owner missing");
        if (work == null)
        {
            yield break;
        }

        Button staffTab = FindTopTabButton(TabId.Staff);
        yield return Click(staffTab, "staff tab");
        yield return new WaitForSecondsRealtime(0.25f);

        Button priorityMode = FindButton("P1Action_StaffModePriorities");
        if (priorityMode != null && priorityMode.gameObject.activeInHierarchy && priorityMode.interactable)
        {
            yield return Click(priorityMode, "staff priorities mode");
            yield return new WaitForSecondsRealtime(0.15f);
        }

        int pointerClicks = 0;
        while (work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research) != WorkPriorityLevel.Priority1
            && pointerClicks < 4)
        {
            Button priorityCell = FindButton($"Cell_{owner.GetInstanceID()}_{FacilityWorkType.Research}");
            yield return Click(priorityCell, "owner research priority");
            pointerClicks++;
            yield return new WaitForSecondsRealtime(0.1f);
        }

        Check(work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research) == WorkPriorityLevel.Priority1,
            "STRATEGY_RESEARCH_PRIORITY",
            $"pointerClicks={pointerClicks}; priority={work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Research)}");
        yield return Click(staffTab, "staff tab close");
    }

    private IEnumerator Click(Button button, string label)
    {
        bool available = button != null && button.gameObject.activeInHierarchy && button.interactable;
        Check(available, "POINTER_TARGET", available ? label : label + " missing");
        if (!available)
        {
            yield break;
        }

        yield return ScrollIntoView(button, label);
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
        {
            Check(false, "POINTER_TARGET_AFTER_SCROLL", label + " unavailable after scroll");
            yield break;
        }

        if (!IsInsideScrollViewport(button))
        {
            Check(false, "POINTER_TARGET_AFTER_SCROLL", label + " remains outside viewport");
            yield break;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        QueueVerificationMouseState(new MouseState { position = point });
        yield return null;
        if (!IsButtonPointerReachable(button))
        {
            Check(false, "POINTER_TARGET_AFTER_SCROLL", label + " is covered by another raycast target");
            yield break;
        }

        DispatchButtonPointerClick(button, point);
        yield return null;
        yield return null;
    }

    private IEnumerator MovePointer(Vector2 screenPoint, float waitSeconds)
    {
        DungeonAutomationInputState.MovePointer(screenPoint);
        QueueVerificationMouseState(new MouseState { position = screenPoint });
        yield return null;
        yield return null;
        if (waitSeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(waitSeconds);
        }
    }

    private IEnumerator EnsureDirectPlayPartyCapacity(
        DungeonRuntimeLifetimeScope scope,
        OffenseExpeditionRuntime expeditionRuntime,
        int stageIndex,
        int desiredMembers)
    {
        int currentCapacity = GetDirectPlayAvailableMemberCount(expeditionRuntime);
        if (currentCapacity >= desiredMembers)
        {
            Check(true,
                $"OFFENSE_STAGE_{stageIndex}_MEMBER_CAPACITY",
                $"availableMembers={currentCapacity}/{desiredMembers}; no recruitment needed");
            yield break;
        }

        IRegularCustomerRuntimeProvider recruitmentProvider = null;
        try
        {
            recruitmentProvider = scope?.Container?.Resolve<IRegularCustomerRuntimeProvider>();
        }
        catch (Exception exception)
        {
            Check(false,
                $"OFFENSE_STAGE_{stageIndex}_RECRUIT_PROVIDER",
                exception.Message);
            yield break;
        }

            Check(recruitmentProvider != null
                && recruitmentProvider.TryGetRuntime(out RegularCustomerRuntime runtime)
                && runtime != null,
            $"OFFENSE_STAGE_{stageIndex}_RECRUIT_RUNTIME",
            $"desired={desiredMembers}; available={currentCapacity}; totalMembers={GetDirectPlayMemberPool().Count}");
        if (recruitmentProvider == null || !recruitmentProvider.TryGetRuntime(out runtime) || runtime == null)
        {
            yield break;
        }

        float startedAt = Time.realtimeSinceStartup;
        int recruitClicks = 0;
        int candidateSamples = 0;
        string lastCandidateState = string.Empty;
        while (GetDirectPlayAvailableMemberCount(expeditionRuntime) < desiredMembers
            && Time.realtimeSinceStartup - startedAt < DirectPlayRecruitmentMaxRealtimeSeconds)
        {
            List<RegularCustomerRecord> candidates = runtime.State.Records
                .Where(record => record != null
                    && record.Status == RegularCustomerStatus.RecruitCandidate
                    && !record.IsRecruited)
                .OrderByDescending(record => record.AverageSatisfaction)
                .ThenByDescending(record => record.VisitCount)
                .ThenBy(record => record.CustomerId, StringComparer.Ordinal)
                .ToList();
            lastCandidateState = DescribeRecruitmentState(runtime);
            candidateSamples++;

            if (candidates.Count > 0)
            {
                Button operationsTab = FindTopTabButton(TabId.Operations);
                yield return Click(operationsTab, "operations tab recruitment");
                yield return new WaitForSecondsRealtime(0.25f);

                foreach (RegularCustomerRecord candidate in candidates)
                {
                    if (GetDirectPlayAvailableMemberCount(expeditionRuntime) >= desiredMembers)
                    {
                        break;
                    }

                    Button recruitButton = FindButton($"P0Action_Recruit_{candidate.CustomerId}");
                    int beforeMembers = GetDirectPlayAvailableMemberCount(expeditionRuntime);
                    yield return Click(recruitButton, $"recruit {candidate.CustomerId}");
                    yield return new WaitForSecondsRealtime(0.35f);
                    int afterMembers = GetDirectPlayAvailableMemberCount(expeditionRuntime);
                    bool recruited = runtime.State.IsRecruited(candidate.CustomerId)
                        && afterMembers > beforeMembers;
                    Check(recruited,
                        $"OFFENSE_STAGE_{stageIndex}_RECRUIT_CLICK_{recruitClicks + 1}",
                        $"candidate={candidate.CustomerId}; availableMembers={beforeMembers}->{afterMembers}; totalMembers={GetDirectPlayMemberPool().Count}; state={DescribeRecruitmentState(runtime)}");
                    recruitClicks++;
                }

                Button closeTab = FindTopTabButton(TabId.Operations);
                if (closeTab != null && closeTab.gameObject.activeInHierarchy && closeTab.interactable)
                {
                    yield return Click(closeTab, "operations tab close after recruitment");
                    yield return new WaitForSecondsRealtime(0.1f);
                }
            }

            if (GetDirectPlayAvailableMemberCount(expeditionRuntime) < desiredMembers)
            {
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        currentCapacity = GetDirectPlayAvailableMemberCount(expeditionRuntime);
        Check(currentCapacity >= desiredMembers,
            $"OFFENSE_STAGE_{stageIndex}_MEMBER_CAPACITY_READY",
            $"availableMembers={currentCapacity}/{desiredMembers}; totalMembers={GetDirectPlayMemberPool().Count}; clicks={recruitClicks}; samples={candidateSamples}; {lastCandidateState}");
    }

    private IEnumerator WaitForDirectPlayRecovery(
        OffenseExpeditionRuntime expeditionRuntime,
        GameData gameData,
        int stageIndex,
        int desiredMembers)
    {
        bool fastForwarded = stageIndex >= DirectPlayCautiousRouteFromStage;
        if (fastForwarded)
        {
            yield return RaiseGameSpeedToFive(gameData, $"recovery stage {stageIndex}");
        }

        float startedAt = Time.realtimeSinceStartup;
        int samples = 0;
        string lastState = string.Empty;
        string lastGate = string.Empty;
        while (Time.realtimeSinceStartup - startedAt < DirectPlayRecoveryMaxRealtimeSeconds)
        {
            IReadOnlyList<CharacterActor> availableMembers = expeditionRuntime.GetAvailableMemberActors();
            List<CharacterActor> readyMembers = availableMembers
                .Where(IsDirectPlayBattleReady)
                .OrderByDescending(OffenseExpeditionService.CalculateMemberPower)
                .ToList();
            List<CharacterActor> selected = SelectDirectPlayParty(
                readyMembers,
                desiredMembers,
                desiredMembers,
                stageIndex);
            lastState = DescribeDirectPlayRecoveryState(availableMembers, readyMembers);
            if (IsDirectPlayPartyRecoveredForStage(selected, desiredMembers, stageIndex, out lastGate))
            {
                if (fastForwarded)
                {
                    yield return ReduceGameSpeedToOne(gameData);
                }

                Check(true,
                    $"OFFENSE_STAGE_{stageIndex}_RECOVERY",
                    $"ready={readyMembers.Count}/{desiredMembers}; selected={string.Join(",", selected.Select(GetActorDisplayName))}; "
                    + $"waited={samples:0}; gate={lastGate}; {lastState}");
                yield break;
            }

            samples++;
            yield return new WaitForSecondsRealtime(1f);
        }

        if (fastForwarded)
        {
            yield return ReduceGameSpeedToOne(gameData);
        }

        IReadOnlyList<CharacterActor> finalAvailable = expeditionRuntime.GetAvailableMemberActors();
        List<CharacterActor> finalReady = finalAvailable
            .Where(IsDirectPlayBattleReady)
            .OrderByDescending(OffenseExpeditionService.CalculateMemberPower)
            .ToList();
        List<CharacterActor> finalSelected = SelectDirectPlayParty(
            finalReady,
            desiredMembers,
            desiredMembers,
            stageIndex);
        IsDirectPlayPartyRecoveredForStage(finalSelected, desiredMembers, stageIndex, out lastGate);
        lastState = DescribeDirectPlayRecoveryState(finalAvailable, finalReady);
        Check(false,
            $"OFFENSE_STAGE_{stageIndex}_RECOVERY",
            $"ready={finalReady.Count}/{desiredMembers}; selected={string.Join(",", finalSelected.Select(GetActorDisplayName))}; "
            + $"timeout={DirectPlayRecoveryMaxRealtimeSeconds:0}s; gate={lastGate}; {lastState}");
    }

    private IEnumerator WaitForDirectPlayLogisticsSettlement(
        DungeonRuntimeLifetimeScope scope,
        OffenseExpeditionRuntime expeditionRuntime,
        GameData gameData,
        int stageIndex)
    {
        lastDirectPlayLogisticsSettled = false;
        IWorldItemStackRuntime itemRuntime = TryResolveOptional<IWorldItemStackRuntime>(scope);
        if (itemRuntime == null)
        {
            Check(false,
                $"OFFENSE_STAGE_{stageIndex}_LOGISTICS",
                "physical item runtime missing");
            yield break;
        }

        OffensePreparationSnapshot beforeSupply = expeditionRuntime?.GetPreparationSnapshot();
        int looseBefore = CountLooseLogisticsStacks(
            itemRuntime,
            out int looseQuantityBefore,
            out string looseSampleBefore);
        int carriedBefore = CountStaffCarriedLogisticsItems(
            out int carriedQuantityBefore,
            out string carriedSampleBefore);
        int pendingQuantityBefore = looseQuantityBefore + carriedQuantityBefore;
        if (looseBefore <= 0 && carriedBefore <= 0)
        {
            lastDirectPlayLogisticsSettled = true;
            Check(true,
                $"OFFENSE_STAGE_{stageIndex}_LOGISTICS",
                $"nothing pending; supply={DescribeDirectPlaySupplyAvailability(beforeSupply)}");
            yield break;
        }

        yield return RaiseGameSpeedToFive(gameData, $"logistics stage {stageIndex}");

        float startedAt = Time.realtimeSinceStartup;
        int samples = 0;
        int looseAfter = looseBefore;
        int looseQuantityAfter = looseQuantityBefore;
        int carriedAfter = carriedBefore;
        int carriedQuantityAfter = carriedQuantityBefore;
        string looseSampleAfter = looseSampleBefore;
        string carriedSampleAfter = carriedSampleBefore;
        while (Time.realtimeSinceStartup - startedAt < DirectPlayLogisticsMaxRealtimeSeconds)
        {
            looseAfter = CountLooseLogisticsStacks(
                itemRuntime,
                out looseQuantityAfter,
                out looseSampleAfter);
            carriedAfter = CountStaffCarriedLogisticsItems(
                out carriedQuantityAfter,
                out carriedSampleAfter);
            OffensePreparationSnapshot currentSupply = expeditionRuntime?.GetPreparationSnapshot();
            int pendingQuantityNow = looseQuantityAfter + carriedQuantityAfter;
            bool logisticsMovedStock = pendingQuantityNow < pendingQuantityBefore
                || DirectPlaySupplyImproved(beforeSupply, currentSupply);
            if ((looseAfter <= 0 && carriedAfter <= 0)
                || (logisticsMovedStock && HasDirectPlayStandardLoadout(currentSupply)))
            {
                break;
            }

            samples++;
            yield return new WaitForSecondsRealtime(0.5f);
        }

        yield return ReduceGameSpeedToOne(gameData);

        looseAfter = CountLooseLogisticsStacks(
            itemRuntime,
            out looseQuantityAfter,
            out looseSampleAfter);
        carriedAfter = CountStaffCarriedLogisticsItems(
            out carriedQuantityAfter,
            out carriedSampleAfter);
        OffensePreparationSnapshot afterSupply = expeditionRuntime?.GetPreparationSnapshot();
        int pendingQuantityAfter = looseQuantityAfter + carriedQuantityAfter;
        bool observedPhysicalProgress = pendingQuantityAfter < pendingQuantityBefore
            || DirectPlaySupplyImproved(beforeSupply, afterSupply);
        bool standardLoadoutReady = HasDirectPlayStandardLoadout(afterSupply);
        lastDirectPlayLogisticsSettled = (looseAfter <= 0 && carriedAfter <= 0)
            || (observedPhysicalProgress && standardLoadoutReady);
        Check(lastDirectPlayLogisticsSettled,
            $"OFFENSE_STAGE_{stageIndex}_LOGISTICS",
            $"looseStacks={looseBefore}->{looseAfter}; looseQty={looseQuantityBefore}->{looseQuantityAfter}; "
            + $"carriedStacks={carriedBefore}->{carriedAfter}; carriedQty={carriedQuantityBefore}->{carriedQuantityAfter}; "
            + $"waited={samples * 0.5f:0.0}s/{DirectPlayLogisticsMaxRealtimeSeconds:0}s; "
            + $"progress={observedPhysicalProgress}; standardLoadout={standardLoadoutReady}; "
            + $"supply={DescribeDirectPlaySupplyAvailability(beforeSupply)}->{DescribeDirectPlaySupplyAvailability(afterSupply)}; "
            + $"looseSample={looseSampleAfter}; carriedSample={carriedSampleAfter}");
    }

    private static int CountLooseLogisticsStacks(
        IWorldItemStackRuntime itemRuntime,
        out int quantity,
        out string sample)
    {
        WorldItemStackSnapshot[] stacks = itemRuntime?.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && !stack.Forbidden
                && IsDirectPlayLogisticsItem(stack.ItemId))
            .OrderBy(stack => stack.IsReserved ? 1 : 0)
            .ThenByDescending(stack => stack.TotalValue)
            .ThenBy(stack => stack.DisplayName, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<WorldItemStackSnapshot>();
        quantity = stacks.Sum(stack => stack.Quantity);
        sample = stacks.Length == 0
            ? "none"
            : string.Join("|", stacks.Take(5).Select(stack =>
                $"{stack.DisplayName}x{stack.Quantity}@{stack.Position}{(stack.IsReserved ? ":reserved" : string.Empty)}"));
        return stacks.Length;
    }

    private static int CountStaffCarriedLogisticsItems(out int quantity, out string sample)
    {
        CharacterCarryInventory[] inventories = FindObjectsByType<CharacterCarryInventory>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        List<string> samples = new List<string>();
        int stackCount = 0;
        int totalQuantity = 0;
        foreach (CharacterCarryInventory inventory in inventories)
        {
            if (inventory == null || !inventory.HasItems)
            {
                continue;
            }

            CharacterActor actor = inventory.GetComponent<CharacterActor>();
            if (!IsDirectPlayHauler(actor))
            {
                continue;
            }

            foreach (CharacterCarriedItemSaveData item in inventory.Items)
            {
                if (item == null
                    || item.quantity <= 0
                    || !IsDirectPlayLogisticsItem(item.itemId))
                {
                    continue;
                }

                stackCount++;
                totalQuantity += item.quantity;
                if (samples.Count < 5)
                {
                    samples.Add($"{GetActorDisplayName(actor)}:{item.itemId}x{item.quantity}");
                }
            }
        }

        quantity = totalQuantity;
        sample = samples.Count == 0 ? "none" : string.Join("|", samples);
        return stackCount;
    }

    private static bool IsDirectPlayHauler(CharacterActor actor)
    {
        if (actor == null || !actor.gameObject.activeInHierarchy || actor.IsDead)
        {
            return false;
        }

        actor.EnsureRuntimeState();
        CharacterIdentity identity = actor.Identity;
        return identity != null
            && (identity.IsOwner || identity.CharacterType == CharacterType.NPC);
    }

    private static bool IsDirectPlayLogisticsItem(string itemId)
    {
        return DungeonItemCatalogSO.TryGetStockCategoryFromItemId(itemId, out _)
            || DungeonItemCatalogSO.TryGetEquipmentIdFromItemId(itemId, out _);
    }

    private static string DescribeDirectPlaySupplyAvailability(OffensePreparationSnapshot snapshot)
    {
        if (snapshot == null)
        {
            return "missing";
        }

        return $"rations={snapshot.GetAvailable(OffenseSupplyType.Rations)}"
            + $",medicine={snapshot.GetAvailable(OffenseSupplyType.Medicine)}"
            + $",tools={snapshot.GetAvailable(OffenseSupplyType.Tools)}";
    }

    private static bool HasDirectPlayStandardLoadout(OffensePreparationSnapshot snapshot)
    {
        return snapshot != null
            && snapshot.GetAvailable(OffenseSupplyType.Rations) >= 4
            && snapshot.GetAvailable(OffenseSupplyType.Medicine) >= 3;
    }

    private static bool DirectPlaySupplyImproved(
        OffensePreparationSnapshot before,
        OffensePreparationSnapshot after)
    {
        if (before == null || after == null)
        {
            return false;
        }

        return after.GetAvailable(OffenseSupplyType.Rations)
                > before.GetAvailable(OffenseSupplyType.Rations)
            || after.GetAvailable(OffenseSupplyType.Medicine)
                > before.GetAvailable(OffenseSupplyType.Medicine)
            || after.GetAvailable(OffenseSupplyType.Tools)
                > before.GetAvailable(OffenseSupplyType.Tools);
    }

    private static T TryResolveOptional<T>(DungeonRuntimeLifetimeScope scope) where T : class
    {
        try
        {
            return scope?.Container?.Resolve<T>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private IEnumerator ClickPointer(Vector2 screenPoint)
    {
        DungeonAutomationInputState.MovePointer(screenPoint);
        DungeonAutomationInputState.ClickPointer(0);
        QueueVerificationMouseState(
            new MouseState { position = screenPoint }.WithButton(MouseButton.Left, true));
        yield return null;
        yield return null;
        QueueVerificationMouseState(new MouseState { position = screenPoint });
        yield return null;
        yield return null;
    }

    private static void DispatchButtonPointerClick(Button button, Vector2 screenPoint)
    {
        if (button == null || EventSystem.current == null)
        {
            return;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left,
            position = screenPoint,
            pressPosition = screenPoint,
            pointerPressRaycast = new RaycastResult { gameObject = button.gameObject },
            pointerCurrentRaycast = new RaycastResult { gameObject = button.gameObject }
        };
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler);
    }

    private void QueueVerificationMouseState(MouseState state)
    {
        if (verificationMouse == null || !verificationMouse.added)
        {
            return;
        }

        DungeonAutomationInputState.MovePointer(state.position);

        if (!verificationMouse.enabled)
        {
            InputSystem.EnableDevice(verificationMouse);
        }

        verificationMouse.MakeCurrent();
        InputSystem.QueueStateEvent(verificationMouse, state);
        InputSystem.Update();
        if (Vector2.Distance(verificationMouse.position.ReadValue(), state.position) > 0.1f)
        {
            InputState.Change(verificationMouse, state);
        }
    }

    private static void BringHorizontalScrollIntoView(ScrollRect scroll, RectTransform target)
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null || target == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        float overflow = Mathf.Max(0f, scroll.content.rect.width - scroll.viewport.rect.width);
        if (overflow <= 0.1f)
        {
            scroll.horizontalNormalizedPosition = 0f;
            return;
        }

        float targetCenter = -target.anchoredPosition.x;
        if (targetCenter < 0f)
        {
            targetCenter = target.anchoredPosition.x;
        }

        float desiredOffset = Mathf.Clamp(
            targetCenter - scroll.viewport.rect.width * 0.5f,
            0f,
            overflow);
        scroll.horizontalNormalizedPosition = desiredOffset / overflow;
    }

    private IEnumerator ScrollIntoView(Button button, string label)
    {
        ScrollRect scrollRect = button != null ? button.GetComponentInParent<ScrollRect>() : null;
        RectTransform viewport = scrollRect != null ? scrollRect.viewport : null;
        if (scrollRect == null || viewport == null || !scrollRect.vertical)
        {
            yield break;
        }

        RectTransform directButtonRect = button.GetComponent<RectTransform>();
        BringVerticalScrollIntoView(scrollRect, directButtonRect);
        yield return null;
        Canvas.ForceUpdateCanvases();

        bool visible = false;
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            Vector2 buttonPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                buttonRect.TransformPoint(buttonRect.rect.center));
            visible = RectTransformUtility.RectangleContainsScreenPoint(viewport, buttonPoint, null);
            if (visible)
            {
                break;
            }

            Vector2 viewportPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                viewport.TransformPoint(viewport.rect.center));
            float scrollDelta = buttonPoint.y < viewportPoint.y ? -120f : 120f;
            verificationMouse.MakeCurrent();
            InputSystem.QueueStateEvent(
                verificationMouse,
                new MouseState
                {
                    position = viewportPoint,
                    scroll = new Vector2(0f, scrollDelta)
                });
            yield return null;
            yield return null;
            verificationMouse.MakeCurrent();
            InputSystem.QueueStateEvent(
                verificationMouse,
                new MouseState { position = viewportPoint });
            yield return null;
        }

        for (int attempt = 0; !visible && attempt < 12; attempt++)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            Vector2 buttonPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                buttonRect.TransformPoint(buttonRect.rect.center));
            Vector2 viewportPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                viewport.TransformPoint(viewport.rect.center));
            float direction = buttonPoint.y < viewportPoint.y ? 1f : -1f;
            float dragDistance = Mathf.Max(80f, viewport.rect.height * 0.45f);
            Vector2 dragStart = viewportPoint - new Vector2(0f, direction * dragDistance * 0.35f);
            Vector2 dragEnd = viewportPoint + new Vector2(0f, direction * dragDistance * 0.65f);

            verificationMouse.MakeCurrent();
            InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = dragStart });
            yield return null;
            InputSystem.QueueStateEvent(
                verificationMouse,
                new MouseState { position = dragStart }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(
                verificationMouse,
                new MouseState { position = dragEnd }.WithButton(MouseButton.Left, true));
            yield return null;
            InputSystem.QueueStateEvent(verificationMouse, new MouseState { position = dragEnd });
            yield return null;
            yield return null;

            visible = IsInsideScrollViewport(button);
        }

        Check(visible, "POINTER_VISIBLE", label);
    }

    private static void BringVerticalScrollIntoView(ScrollRect scroll, RectTransform target)
    {
        if (scroll == null || scroll.content == null || scroll.viewport == null || target == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        float overflow = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
        if (overflow <= 0.1f)
        {
            scroll.verticalNormalizedPosition = 1f;
            return;
        }

        Vector3 worldCenter = target.TransformPoint(target.rect.center);
        Vector2 contentPoint = scroll.content.InverseTransformPoint(worldCenter);
        float distanceFromTop = scroll.content.rect.yMax - contentPoint.y;
        float desiredOffset = Mathf.Clamp(
            distanceFromTop - scroll.viewport.rect.height * 0.5f,
            0f,
            overflow);
        scroll.verticalNormalizedPosition = 1f - desiredOffset / overflow;
        Canvas.ForceUpdateCanvases();
    }

    private static bool IsInsideScrollViewport(Button button)
    {
        ScrollRect scrollRect = button != null ? button.GetComponentInParent<ScrollRect>() : null;
        RectTransform viewport = scrollRect != null ? scrollRect.viewport : null;
        RectTransform buttonRect = button != null ? button.GetComponent<RectTransform>() : null;
        if (scrollRect == null || viewport == null || !scrollRect.vertical)
        {
            return true;
        }

        if (buttonRect == null)
        {
            return false;
        }

        Vector2 buttonPoint = RectTransformUtility.WorldToScreenPoint(
            null,
            buttonRect.TransformPoint(buttonRect.rect.center));
        return RectTransformUtility.RectangleContainsScreenPoint(viewport, buttonPoint, null);
    }

    private static bool IsInsideScreen(Vector2 point)
    {
        return point.x > 1f && point.x < Screen.width - 1f
            && point.y > 1f && point.y < Screen.height - 1f;
    }

    private static bool IsScreenPointOverUi(Vector2 point)
    {
        if (EventSystem.current == null || !EventSystem.current.isActiveAndEnabled)
        {
            return false;
        }

        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = point
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        return results.Any(result => result.module is GraphicRaycaster);
    }

    private IEnumerator CaptureScreen()
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = capture != null ? capture.GetPixels32() : Array.Empty<Color32>();
        Check(pixels.Any(pixel => pixel.a > 0 && (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)),
            "CAPTURE", $"nonblank pixels={pixels.Length}");
        if (capture != null)
        {
            File.WriteAllBytes(
                strategy?.ScreenshotPath ?? NaturalRunPlayModeVerifier.ScreenshotPath,
                capture.EncodeToPNG());
            Destroy(capture);
        }
    }

    private static IEnumerator CaptureEvidence(string path)
    {
        yield return new WaitForEndOfFrame();
        Texture2D capture = ScreenCapture.CaptureScreenshotAsTexture();
        if (capture == null)
        {
            yield break;
        }

        File.WriteAllBytes(path, capture.EncodeToPNG());
        Destroy(capture);
    }

    private void RecordDaySnapshot(
        int day,
        GameData gameData,
        IDungeonRunFlowRuntime flow,
        BlueprintResearchRuntime research,
        IOperatingDaySettlementRuntimeProvider settlementProvider,
        IInvasionDirectorRuntimeProvider invasionProvider,
        CharacterActor owner)
    {
        int reports = settlementProvider.TryGetRuntime(out OperatingDaySettlementRuntime settlement)
            ? settlement.ReportHistory.Count
            : 0;
        int intruders = invasionProvider.TryGetRuntime(out InvasionDirectorRuntime invasion)
            ? invasion.ActiveIntruders.Count
            : 0;
        InvasionThreatRuntime threat = FindFirstObjectByType<InvasionThreatRuntime>();
        InvasionThreatSnapshot threatSnapshot = threat != null ? threat.LatestSnapshot : default;
        report.Add(
            $"DAY {day}; phase={flow.Phase}; money={gameData.holdingMoney.Value}; " +
            $"reports={reports}; uses={GetCompletedFacilityUses()}; work={GetCompletedFacilityWork()}; " +
            $"research={GetTotalResearchProgress(research):0.##}; completed={research?.State.Projects.CompletedProjectIds.Count ?? 0}; " +
            $"intruders={intruders}; ownerHp={(owner != null ? owner.CurrentHealth : -1f):0.#}; " +
            $"threat={(threat != null ? threat.CurrentThreat : -1f):0.##}; " +
            $"stage={(threat != null ? threat.CurrentStage : InvasionThreatStage.Peaceful)}; " +
            $"safety={(threat != null ? threat.SafetyRemaining : -1f):0.##}; " +
            $"dungeonValue={threatSnapshot.factors.dungeonValue:0.##}");
        string workBreakdown = GetCompletedFacilityWorkBreakdown();
        if (!string.IsNullOrWhiteSpace(workBreakdown))
        {
            report.Add($"WORK_BREAKDOWN day={day}; {workBreakdown}");
        }
        FlushProgress();
    }

    private static float GetTotalResearchProgress(BlueprintResearchRuntime research)
    {
        return research == null
            ? 0f
            : research.State.Projects.ProgressById.Values.Sum(progress => progress?.Progress ?? 0f);
    }

    private static float GetResearchProgress(BlueprintResearchRuntime research, int blueprintId)
    {
        if (research == null
            || !research.ProjectCatalog.TryGetForBlueprint(blueprintId, out ResearchProjectSO project))
        {
            return 0f;
        }

        return research.State.Projects.GetProgress(project.ProjectId).Progress;
    }

    private static bool TryGetStrategyProject(
        BlueprintResearchRuntime research,
        NaturalRunStrategySpec strategy,
        out ResearchProjectSO project)
    {
        project = null;
        return research != null
            && strategy != null
            && strategy.IsTargeted
            && research.ProjectCatalog.TryGetForBlueprint(strategy.BlueprintId, out project);
    }

    private static int GetCompletedFacilityUses()
    {
        return FindObjectsByType<BuildableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(building => building != null && !building.isDestroy)
            .Sum(building => building.FacilityState.completedUses);
    }

    private static int GetCompletedFacilityWork()
    {
        return FindObjectsByType<BuildableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .Where(building => building != null && !building.isDestroy)
            .Sum(building => building.FacilityState.completedWorkCycles);
    }

    private static string GetCompletedFacilityWorkBreakdown()
    {
        return string.Join(
            " | ",
            FindObjectsByType<BuildableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(building => building != null
                    && !building.isDestroy
                    && building.FacilityState.completedWorkCycles > 0)
                .OrderByDescending(building => building.FacilityState.completedWorkCycles)
                .ThenBy(building => building.BuildingData != null ? building.BuildingData.id : int.MaxValue)
                .Take(8)
                .Select(building =>
                    $"{building.BuildingData?.id ?? -1}:{building.BuildingData?.objectName ?? building.name}="
                    + $"{building.FacilityState.completedWorkCycles}"
                    + $"[{(building.Facility != null ? CodexTextFormatter.FormatWorkTypes(building.Facility.SupportedWorkTypeIds) : string.Empty)}]"));
    }

    private static Button FindSpeedButton()
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .Where(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && IsButtonCenterInsideScreen(button)
                && Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                    .Any(index => button.onClick.GetPersistentMethodName(index) == "ChangeGameSpeed"))
            .OrderByDescending(IsButtonPointerReachable)
            .FirstOrDefault();
    }

    private static Button FindTopTabButton(TabId tabId)
    {
        return Resources.FindObjectsOfTypeAll<UITabButtonBinding>()
            .Where(binding => binding != null
                && binding.Id == tabId
                && binding.gameObject.scene.IsValid()
                && binding.gameObject.activeInHierarchy)
            .Select(binding => binding.GetComponent<Button>())
            .Where(button => button != null
                && button.interactable
                && IsButtonCenterInsideScreen(button))
            .OrderByDescending(IsButtonPointerReachable)
            .FirstOrDefault(button => button != null);
    }

    private static bool IsButtonCenterInsideScreen(Button button)
    {
        RectTransform rect = button != null ? button.GetComponent<RectTransform>() : null;
        if (rect == null)
        {
            return false;
        }

        Vector2 point = RectTransformUtility.WorldToScreenPoint(
            null,
            rect.TransformPoint(rect.rect.center));
        return IsInsideScreen(point);
    }

    private static bool IsButtonPointerReachable(Button button)
    {
        if (button == null || EventSystem.current == null || !EventSystem.current.isActiveAndEnabled)
        {
            return false;
        }

        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
        {
            return false;
        }

        Vector2 point = RectTransformUtility.WorldToScreenPoint(
            null,
            rect.TransformPoint(rect.rect.center));
        PointerEventData pointer = new PointerEventData(EventSystem.current)
        {
            position = point
        };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, results);
        return results.Any(result => result.gameObject != null
            && result.gameObject.transform.IsChildOf(button.transform));
    }

    private static Button FindButton(string name)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .Where(button => button != null
                && button.gameObject.scene.IsValid()
                && button.name == name)
            .OrderByDescending(button => button.gameObject.activeInHierarchy && button.interactable)
            .ThenByDescending(button => button.gameObject.activeInHierarchy)
            .FirstOrDefault();
    }

    private static IEnumerable<Button> FindActiveButtons(string prefix)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .Where(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && button.name.StartsWith(prefix, StringComparison.Ordinal));
    }

    private static int CountPublicAccelerators()
    {
        string[] forbidden =
        {
            "ResearchProgress",
            "AdvanceDay",
            "SettleNow",
            "ThreatIncrease",
            "SpawnIntruder",
            "TriggerDefense",
            "RefreshDiscontent",
            "RequestPersona"
        };
        return Resources.FindObjectsOfTypeAll<Button>()
            .Count(button => button != null
                && button.gameObject.scene.IsValid()
                && forbidden.Any(token => button.name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0));
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private void ConfigureInput()
    {
        originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        verificationMouse = InputSystem.AddDevice<Mouse>("NaturalRunVerificationMouse");
        verificationMouse.MakeCurrent();
        DungeonAutomationInputState.Enable(
            new DungeonStory.Foundation.UnityGameClock(),
            new DungeonStory.Foundation.UnityUiClock());
    }

    private void TeardownInput()
    {
        DungeonAutomationInputState.Disable();

        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
        }

        InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
    }

    private void BackupFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || backups.Any(backup => string.Equals(backup.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        backups.Add(new FileBackup
        {
            Path = path,
            Bytes = File.Exists(path) ? File.ReadAllBytes(path) : null
        });
    }

    private void RestoreFiles()
    {
        if (slotService != null)
        {
            HashSet<string> originalPaths = backups
                .Where(backup => backup.Bytes != null)
                .Select(backup => backup.Path)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (DungeonSaveSlotInfo slot in slotService.GetSlots())
            {
                if (!originalPaths.Contains(slot.Path) && File.Exists(slot.Path))
                {
                    File.Delete(slot.Path);
                }
            }
        }

        foreach (FileBackup backup in backups)
        {
            if (backup.Bytes == null)
            {
                if (File.Exists(backup.Path)) File.Delete(backup.Path);
                continue;
            }

            string directory = Path.GetDirectoryName(backup.Path);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(backup.Path, backup.Bytes);
        }

        if (!string.IsNullOrWhiteSpace(profilePath)
            && backups.All(backup => !string.Equals(backup.Path, profilePath, StringComparison.OrdinalIgnoreCase))
            && File.Exists(profilePath))
        {
            File.Delete(profilePath);
        }
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors.Add(condition);
        }
        else if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
    }

    private void Check(bool condition, string id, string detail)
    {
        report.Add($"[{(condition ? "PASS" : "FAIL")}] {id} {detail}");
        FlushProgress();
    }

    private void FlushProgress()
    {
        File.WriteAllLines(
            strategy?.ReportPath ?? NaturalRunPlayModeVerifier.ReportPath,
            new[] { $"NATURAL_RUN IN_PROGRESS strategy={strategy?.Token ?? "unknown"}" }.Concat(report));
    }
}
