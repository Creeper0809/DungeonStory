#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Factions;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using VContainer;
using VContainer.Internal;
using VContainer.Unity;

public static class V20CampaignDebugScenarios
{
    public static string RunWimWinterContractFocused()
    {
        const string path = "Artifacts/QA/wim-implementation/wim-009-winter-contract-focused.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "result=RUNNING\n");
        try
        {
            VerifyWinterContractFocused();
            File.WriteAllText(path, "result=PASS\n"
                + "authored=18 ordinary + 6 seasonal; charcoal8; no remote seasonal debit\n"
                + "actual campaign occurrence/acceptance=exact faction+occurrence+deadline; ordinary/stale/wrong-target/duplicate reject\n"
                + "controlled receipt -> staged seasonal/faction candidate restore -> reward once; cleanup and same-occurrence replay reject\n"
                + "next-year same-faction authored occurrence: fresh destination/operation; old receipt rejected\n"
                + "accepted deadline expiry: grievance+7 once; expired action rejected\n"
                + "scope=isolated production campaign/catalog, controlled eligibility and receipt; real physical haul/UI/full-world coordinator NOT_RUN\n");
            return "PASS " + path;
        }
        catch (Exception error)
        {
            File.WriteAllText(path, "result=FAIL\n" + error);
            throw;
        }
    }

    private static void VerifyWinterContractFocused()
    {
        Require(Application.isPlaying, "Run inside the protected, paused main Play session.");
        var catalog = new V20StoryContentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        Require(catalog.Contracts.Count == 24
                && catalog.Contracts.Count(value => string.IsNullOrEmpty(value.seasonalEventId)) == 18
                && catalog.Contracts.Count(value => value.seasonalEventId == "seasonal:winter-fuel-demand") == 6,
            "Expected ordinary18 and authored seasonal6 contracts.");
        var winter = ((IWorldEventCatalog)catalog).Require("seasonal:winter-fuel-demand");
        Require(!winter.startEffects.Concat(winter.dailyEffects).Concat(winter.endEffects)
                .Any(value => value.kind == V20ContentEffectKind.ItemConsume), "Winter still deletes remote stock.");
        var campaign = PrepareWinterOccurrence(catalog, 91, 157181);
        var occurrence = campaign.ActiveSeasonalEvents.Single(value => value.definitionId == winter.StableId);
        var contract = catalog.Contracts.Single(value => value.seasonalEventId == winter.StableId
            && value.factionId == occurrence.contextFactionId);
        var materials = FactionContractMaterialRules.BuildMaterialRequirements(contract);
        Require(materials.Count == 1 && materials.TryGetValue("material:charcoal", out int amount) && amount == 8,
            "Winter requirement is not exactly eight charcoal.");
        string factionId = contract.factionId;
        string before = JsonUtility.ToJson(campaign.CaptureFactions());
        var target = new FactionContractDeliveryTarget(
            FactionContractDeliveryOutbox.FormatDestinationId(contract.StableId, occurrence.instanceId), new Vector2Int(17, 19));
        Require(!campaign.TryAcceptContract(factionId, contract.StableId, 91, target, out _)
                && !campaign.TryAcceptSeasonalContract("event:missing", contract.StableId, target, out _)
                && !campaign.TryAcceptSeasonalContract(occurrence.instanceId, contract.StableId,
                    new FactionContractDeliveryTarget(FactionContractDeliveryOutbox.FormatDestinationId(contract.StableId), target.Position), out _)
                && JsonUtility.ToJson(campaign.CaptureFactions()) == before,
            "An ordinary/stale/unscoped path accepted or mutated a seasonal request.");
        Require(campaign.TryAcceptSeasonalContract(occurrence.instanceId, contract.StableId, target, out string failure), failure);
        Require(campaign.TryGetFaction(factionId, out var accepted)
                && accepted.activeContractOccurrenceId == occurrence.instanceId
                && accepted.activeContractDeadlineAbsoluteDay == occurrence.deadlineAbsoluteDay
                && occurrence.deadlineAbsoluteDay - occurrence.startedAbsoluteDay >= 2
                && occurrence.deadlineAbsoluteDay - occurrence.startedAbsoluteDay <= 4,
            "Seasonal acceptance did not retain its occurrence's authored deadline.");
        int priorRapport = accepted.rapport;
        int priorObligation = accepted.obligationTokens;
        string acceptedJson = JsonUtility.ToJson(campaign.CaptureFactions());
        Require(!campaign.TryAcceptSeasonalContract(occurrence.instanceId, contract.StableId, target, out _)
                && JsonUtility.ToJson(campaign.CaptureFactions()) == acceptedJson, "Repeated acceptance mutated an active request.");
        Require(campaign.TrySetInputOwnerProjection(factionId, 5000L, 1L, "qa:winter-controlled-projection", out failure), failure);
        var receipt = new FactionContractDeliveryReceipt(PhysicalItemDispositionKind.Transfer,
            FactionContractDeliveryOutbox.FormatOperationId(contract.StableId, occurrence.instanceId),
            FactionContractDeliveryOutbox.TransferReason, "qa:winter-controlled-receipt",
            new[] { "stack:qa:winter-focused" }, 8, 5000L);
        Require(campaign.TryRecordDeliveryReceipt(factionId, receipt, out failure), failure);
        var savedFactions = JsonUtility.FromJson<FactionCampaignWorldSaveData>(JsonUtility.ToJson(campaign.CaptureFactions()));
        var savedSeason = JsonUtility.FromJson<SeasonalEventWorldSaveData>(JsonUtility.ToJson(campaign.CaptureSeasonal()));
        var restored = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        var stagedSeason = restored.PrepareSeasonalRestore(savedSeason);
        try
        {
            var stagedFactions = restored.PrepareFactionsRestore(savedFactions);
            Require(restored.CaptureSeasonal().activeEvents.Count == 0,
                "Preparing the join published the candidate into the live season.");
            restored.PublishSeasonalRestore(stagedSeason);
            restored.PublishFactions(stagedFactions);
        }
        finally { stagedSeason.Discard(); }
        Require(JsonUtility.ToJson(restored.CaptureFactions()) == JsonUtility.ToJson(savedFactions),
            "Pending occurrence-scoped faction state did not round-trip exactly.");
        Require(restored.TryResolveDeliveredContract(factionId, Satisfy(new[] { contract.completionRequirements }),
                out _, out failure), failure);
        Require(restored.TryGetFaction(factionId, out var rewarded)
                && rewarded.rapport == priorRapport + 8 && rewarded.obligationTokens == priorObligation + 1,
            "Existing supply rewards were not applied exactly once.");
        string rewardJson = JsonUtility.ToJson(restored.CaptureFactions());
        Require(!restored.TryResolveDeliveredContract(factionId, Satisfy(new[] { contract.completionRequirements }), out _, out _)
                && JsonUtility.ToJson(restored.CaptureFactions()) == rewardJson, "Duplicate reward changed state.");
        Require(restored.TryMarkInputOwnerRetired(factionId, out failure), failure);
        Require(restored.TryClearDeliveryOutbox(factionId, out failure), failure);
        string terminalJson = JsonUtility.ToJson(restored.CaptureFactions());
        Require(!restored.TryAcceptSeasonalContract(occurrence.instanceId, contract.StableId, target, out _)
                && JsonUtility.ToJson(restored.CaptureFactions()) == terminalJson, "Completed occurrence could be accepted again.");

        V20CampaignRuntime next = null;
        V20ActiveEventSaveData nextOccurrence = null;
        for (int seed = 157181; seed < 157245; seed++)
        {
            var candidate = PrepareWinterOccurrence(catalog, 91 + GameCalendarRules.DaysPerYear, seed);
            var nextEvent = candidate.ActiveSeasonalEvents.Single(value => value.definitionId == winter.StableId);
            if (nextEvent.contextFactionId != factionId) continue;
            next = candidate; nextOccurrence = nextEvent; break;
        }
        Require(next != null, "No same-faction authored next-year occurrence in the bounded witness seeds.");
        next.PublishFactions(next.PrepareFactions(restored.CaptureFactions()));
        var nextTarget = new FactionContractDeliveryTarget(
            FactionContractDeliveryOutbox.FormatDestinationId(contract.StableId, nextOccurrence.instanceId), target.Position);
        Require(nextOccurrence.instanceId != occurrence.instanceId && nextTarget.DestinationId != target.DestinationId
                && !next.TryAcceptSeasonalContract(occurrence.instanceId, contract.StableId, target, out _)
                && next.TryAcceptSeasonalContract(nextOccurrence.instanceId, contract.StableId, nextTarget, out failure),
            "Next occurrence did not reject the old action and accept a fresh destination.");
        Require(next.TrySetInputOwnerProjection(factionId, 5000L, 1L, "qa:winter-controlled-projection", out failure), failure);
        string beforeOldReceipt = JsonUtility.ToJson(next.CaptureFactions());
        Require(!next.TryRecordDeliveryReceipt(factionId, receipt, out _)
                && JsonUtility.ToJson(next.CaptureFactions()) == beforeOldReceipt,
            "New occurrence accepted the old physical operation/receipt.");
        Require(next.TryGetFaction(factionId, out var beforeExpiry), "Missing next-year faction state.");
        int priorGrievance = beforeExpiry.grievance;
        var expiryContext = new V20DailyEventContext
        { AbsoluteDay = nextOccurrence.deadlineAbsoluteDay + 1, RunSeed = 157181, Season = Season.Winter };
        next.EvaluateDaily(expiryContext);
        Require(next.TryGetFaction(factionId, out var expired) && expired.grievance == priorGrievance + 7
                && expired.activeContractId.Length == 0
                && !next.TryAcceptSeasonalContract(nextOccurrence.instanceId, contract.StableId, nextTarget, out _),
            "Accepted expiry lost the existing failure effect or allowed the expired offer.");
        int expiredGrievance = expired.grievance;
        next.EvaluateDaily(expiryContext);
        Require(next.TryGetFaction(factionId, out var expiryRepeat) && expiryRepeat.grievance == expiredGrievance,
            "Repeated expiry applied the failure effect twice.");
    }

    private static V20CampaignRuntime PrepareWinterOccurrence(V20StoryContentCatalog catalog, int day, int seed)
    {
        var campaign = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        var controlled = campaign.CaptureSeasonal();
        controlled.cycle = day / GameCalendarRules.DaysPerYear;
        controlled.completedEventIds.AddRange(catalog.SeasonalEvents
            .Where(value => value.StableId != "seasonal:winter-fuel-demand").Select(value => value.StableId));
        campaign.PublishSeasonal(campaign.PrepareSeasonal(controlled));
        campaign.EvaluateDaily(new V20DailyEventContext { AbsoluteDay = day, RunSeed = seed, Season = Season.Winter });
        return campaign;
    }

    private const string DryWellReportPath = "Artifacts/QA/wim-implementation/wim-009-summer-dry-well.txt";

    public static string RunWimSummerDryWellFocused()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DryWellReportPath));
        File.WriteAllText(DryWellReportPath, "result=RUNNING\n");
        try { return VerifyWimSummerDryWellFocused(); }
        catch (Exception error)
        {
            File.WriteAllText(DryWellReportPath, "result=FAIL\n" + error);
            throw;
        }
    }

    private static string VerifyWimSummerDryWellFocused()
    {
        const string path = DryWellReportPath;
        Require(Application.isPlaying, "Run only in a protected, paused main Play session.");
        var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        var catalog = new V20StoryContentCatalog(content);
        var dry = ((IWorldEventCatalog)catalog).Require("seasonal:summer-dry-well");
        Require(catalog.SeasonalEvents.Count == 28
                && catalog.SeasonalEvents.Count(value => value != dry) == 27
                && dry.worldWaterRegenerationMultiplier == .95f
                && dry.minimumDurationDays == 3 && dry.maximumDurationDays == 5
                && catalog.SeasonalEvents.Where(value => value != dry)
                    .All(value => value.worldWaterRegenerationMultiplier == 1f),
            "Authored dry-well/neutral seasonal values changed.");
        Require(!dry.startEffects.Concat(dry.dailyEffects).Concat(dry.endEffects)
                .Any(effect => effect.kind == V20ContentEffectKind.ItemConsume),
            "Dry well still carries an outward remote-consumption effect.");
        var root = new DungeonRuntimeAggregateRootStore();
        var campaign = new V20CampaignRuntime(root, catalog);
        var seedState = campaign.CaptureSeasonal();
        // Control eligibility, not the event's effects or occurrence creation.
        seedState.completedEventIds.AddRange(catalog.SeasonalEvents
            .Where(value => value != dry).Select(value => value.StableId));
        campaign.PublishSeasonal(campaign.PrepareSeasonal(seedState));
        Require(campaign.GetWorldWaterRegenerationMultiplier() == 1f, "Inactive source multiplier is not neutral.");
        var clock = new DryWellClock();
        using var water = new WorldWaterRuntime(new DryWellGridProvider(), clock,
            campaign, content, root, new ResourceDiseaseDefinitionCatalog(content));
        water.RestoreWaterSources(new[] { new WorldWaterSourceSaveData
        {
            sourceId = "water:qa:dry-well", gridX = 2, gridY = 1,
            capacity = 100f, remaining = 10f, regenerationPerSecond = .2f
        } }, 2);
        string beforeStart = JsonUtility.ToJson(water.CaptureWaterSources().Single());
        var results = campaign.EvaluateDaily(new V20DailyEventContext
        {
            AbsoluteDay = 32, RunSeed = 157181, Season = Season.Summer
        });
        Require(results.Any(value => value.DefinitionId == dry.StableId && value.ResolutionId == "started")
                && campaign.ActiveSeasonalEvents.Count == 1
                && campaign.GetWorldWaterRegenerationMultiplier() == .95f
                && JsonUtility.ToJson(water.CaptureWaterSources().Single()) == beforeStart,
            "Actual seasonal start did not activate the factor without deleting existing source water.");
        clock.Advance(10f);
        water.Tick();
        Require(Mathf.Abs(water.CaptureWaterSources().Single().remaining - 11.9f) < .0001f,
            "Actual WorldWaterRuntime.Tick did not produce 1.9 from base .2 over ten game seconds.");
        string pausedState = JsonUtility.ToJson(water.CaptureWaterSources().Single());
        clock.Advance(0f);
        water.Tick();
        for (int i = 0; i < 128; i++) campaign.GetWorldWaterRegenerationMultiplier();
        long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
        float observed = 0f;
        for (int i = 0; i < 10000; i++) observed = campaign.GetWorldWaterRegenerationMultiplier();
        long allocation = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;
        Require(allocation == 0 && observed == .95f
                && JsonUtility.ToJson(water.CaptureWaterSources().Single()) == pausedState,
            "Paused/query-only path mutated water or the warmed multiplier query allocated.");
        SeasonalEventWorldSaveData saved = JsonUtility.FromJson<SeasonalEventWorldSaveData>(
            JsonUtility.ToJson(campaign.CaptureSeasonal()));
        WorldWaterSourceSaveData savedSource = JsonUtility.FromJson<WorldWaterSourceSaveData>(pausedState);
        campaign.PublishSeasonal(campaign.PrepareSeasonal(saved));
        water.RestoreWaterSources(new[] { savedSource }, 2);
        Require(JsonUtility.ToJson(water.CaptureWaterSources().Single()) == pausedState
                && campaign.GetWorldWaterRegenerationMultiplier() == .95f,
            "Current occurrence/source round-trip changed base, remaining, or the derived factor.");
        clock.Advance(10f);
        water.Tick();
        Require(Mathf.Abs(water.CaptureWaterSources().Single().remaining - 13.8f) < .0001f,
            "Restoration compounded or lost the source reduction.");
        string beforeEnd = JsonUtility.ToJson(water.CaptureWaterSources().Single());
        campaign.EvaluateDaily(new V20DailyEventContext
        {
            AbsoluteDay = saved.activeEvents.Single().deadlineAbsoluteDay + 1,
            RunSeed = 157181, Season = Season.Summer
        });
        Require(campaign.ActiveSeasonalEvents.Count == 0
                && campaign.GetWorldWaterRegenerationMultiplier() == 1f
                && JsonUtility.ToJson(water.CaptureWaterSources().Single()) == beforeEnd,
            "Actual event end did not remove its reduction without refunding source water.");
        clock.Advance(10f);
        water.Tick();
        WorldWaterSourceSaveData final = water.CaptureWaterSources().Single();
        Require(Mathf.Abs(final.remaining - 15.8f) < .0001f
                && final.regenerationPerSecond == .2f && final.capacity == 100f,
            "Neutral supply after expiry or saved base/capacity changed.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, "result=PASS\n"
            + "authored28: dry=.95/duration3-5, other27=1; remote-consumption effects=0\n"
            + "actual seasonal start/end; actual water Tick: 10->11.9->13.8->15.8\n"
            + "pause/query unchanged; warmed10000-query allocation=0B\n"
            + "current seasonal+source JSON restore: no compounded reduction; base/capacity preserved; expiry refund=0\n"
            + "scope=isolated production campaign/water/real authored catalog, controlled eligibility/grid/game clock; natural main-day dispatcher/6adult NOT_RUN\n");
        return "PASS " + path;
    }

    private sealed class DryWellGridProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid { get; } = new Grid(5, 3);
        public bool TryGetManager(out GridSystemManager manager) { manager = null; return false; }
        public bool TryGetGrid(out Grid grid) { grid = Grid; return true; }
    }

    private sealed class DryWellClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => DeltaTime == 0f;
        public void Advance(float delta) { DeltaTime = delta; Time += delta; FrameCount++; }
    }

    public const string Wim032033ReportPath =
        "Artifacts/QA/wim-implementation/"
        + "wim-032-033-faction-contracts.txt";
    internal const string Wim032033RequestPath =
        "Temp/wim-032-033-faction-contracts.request";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    public const string Wim044ReportPath =
        "Artifacts/QA/wim-implementation/"
        + "wim-044-retirement-choice-schedule.txt";
    internal const string Wim044RequestPath =
        "Temp/wim-044-retirement-choice-schedule.request";

    [MenuItem("DungeonStory/QA/Run WIM-032-033 Faction Contracts Focused")]
    public static void RunWim032033FactionContractsFromMenu() =>
        RunWim032033FactionContractsFocused();

    public static string RunWim032033FactionContractsFocused()
    {
        if (Application.isPlaying)
        {
            StartWim032033Runner(exitPlayMode: false);
            return "STARTED: WIM-032/033 live PlayMode verifier";
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText(
            Wim032033RequestPath,
            DateTime.UtcNow.ToString("O"));
        if (!string.Equals(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                GameplayScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
        return "STARTED: entering PlayMode for WIM-032/033 live verifier";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapWim032033Runner()
    {
        if (File.Exists(Wim032033RequestPath))
            StartWim032033Runner(exitPlayMode: true);
    }

    private static void StartWim032033Runner(bool exitPlayMode)
    {
        V20FactionContractLivePlayModeRunner existing =
            UnityEngine.Object.FindFirstObjectByType<
                V20FactionContractLivePlayModeRunner>();
        if (existing != null)
        {
            existing.ExitPlayModeOnCompletion |= exitPlayMode;
            return;
        }
        GameObject host = new("WIM-032-033 Faction Contract Live Verifier");
        UnityEngine.Object.DontDestroyOnLoad(host);
        V20FactionContractLivePlayModeRunner runner =
            host.AddComponent<V20FactionContractLivePlayModeRunner>();
        runner.ExitPlayModeOnCompletion = exitPlayMode;
    }

    [MenuItem("DungeonStory/QA/Run WIM-044 Retirement Choice Schedule Focused")]
    public static void RunWim044RetirementChoiceScheduleFromMenu() =>
        RunWim044RetirementChoiceScheduleFocused();

    public static string RunWim044RetirementChoiceScheduleFocused()
    {
        if (Application.isPlaying)
        {
            StartWim044Runner(exitPlayMode: false);
            return "STARTED: WIM-044 live PlayMode verifier";
        }
        Directory.CreateDirectory("Temp");
        File.WriteAllText(
            Wim044RequestPath,
            DateTime.UtcNow.ToString("O"));
        if (!string.Equals(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                GameplayScenePath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);
        }
        EditorApplication.EnterPlaymode();
        return "STARTED: entering PlayMode for WIM-044 live verifier";
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapWim044Runner()
    {
        if (File.Exists(Wim044RequestPath))
            StartWim044Runner(exitPlayMode: true);
    }

    [MenuItem("DungeonStory/QA/Run WIM-039 Actionable Alert Details Focused")]
    public static void RunWim039ActionableAlertDetailsFromMenu() =>
        Debug.Log(RunWim039ActionableAlertDetailsFocused());

    public static string RunWim039ActionableAlertDetailsFocused()
    {
        if (!Application.isPlaying)
            return "FROZEN: enter a ready Gameplay PlayMode scope first.";
        return VerifyWim039ActionableAlertDetails();
    }

    private static void StartWim044Runner(bool exitPlayMode)
    {
        V20RetirementChoiceScheduleLivePlayModeRunner existing =
            UnityEngine.Object.FindFirstObjectByType<
                V20RetirementChoiceScheduleLivePlayModeRunner>();
        if (existing != null)
        {
            existing.ExitPlayModeOnCompletion |= exitPlayMode;
            return;
        }
        GameObject host = new("WIM-044 Retirement Choice Schedule Live Verifier");
        UnityEngine.Object.DontDestroyOnLoad(host);
        V20RetirementChoiceScheduleLivePlayModeRunner runner =
            host.AddComponent<V20RetirementChoiceScheduleLivePlayModeRunner>();
        runner.ExitPlayModeOnCompletion = exitPlayMode;
    }


    private static string VerifyWim039ActionableAlertDetails()
    {
        try
        {
            DungeonRuntimeLifetimeScope scope = Resources
                .FindObjectsOfTypeAll<DungeonRuntimeLifetimeScope>()
                .FirstOrDefault(value => value != null
                    && value.gameObject.scene.IsValid()
                    && value.Container != null);
            if (scope == null)
                return "FROZEN: ready Gameplay scope is unavailable.";

            IV20DailyEvaluationDiagnostic dailyEvaluation = scope.Container
                .Resolve<IV20DailyEvaluationDiagnostic>();
            if (dailyEvaluation is not V20CampaignApplicationAdapter adapter)
                return "FROZEN: production daily adapter is unavailable.";
            V20MilestoneWorldSnapshotProjector projector = scope.Container
                .Resolve<V20MilestoneWorldSnapshotProjector>();
            IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
            projector.Build(calendar.Day);
            V20StoryContentCatalog story = scope.Container
                .Resolve<V20StoryContentCatalog>();
            CharacterActor[] participants = projector.LivingCharacters
                .Where(value => value != null
                    && value.Identity != null)
                .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
                .Take(2)
                .ToArray();
            if (participants.Length != 2)
                return "FROZEN: two ready living participants are required.";

            System.Reflection.MethodInfo participantNames =
                typeof(V20CampaignApplicationAdapter).GetMethod(
                    "ParticipantNames",
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic);
            System.Reflection.MethodInfo choiceOutcome =
                typeof(V20CampaignApplicationAdapter).GetMethod(
                    "SocietyChoiceOutcome",
                    System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.NonPublic);
            if (participantNames == null || choiceOutcome == null)
                return "FROZEN: actionable-alert formatter contract changed.";

            string zero = (string)participantNames.Invoke(
                adapter,
                new object[] { new V20ActiveEventSaveData() });
            string one = (string)participantNames.Invoke(
                adapter,
                new object[]
                {
                    new V20ActiveEventSaveData
                    {
                        participantCharacterIds = new List<string>
                        {
                            participants[0].Identity.PersistentId
                        }
                    }
                });
            string two = (string)participantNames.Invoke(
                adapter,
                new object[]
                {
                    new V20ActiveEventSaveData
                    {
                        participantCharacterIds = new List<string>
                        {
                            participants[1].Identity.PersistentId,
                            participants[0].Identity.PersistentId
                        }
                    }
                });
            string firstName = DisplayNameOrId(participants[0]);
            string secondName = DisplayNameOrId(participants[1]);
            Require(string.IsNullOrEmpty(zero)
                    && string.Equals(one, "\n대상: " + firstName,
                        StringComparison.Ordinal)
                    && string.Equals(two,
                        "\n대상: " + firstName + ", " + secondName,
                        StringComparison.Ordinal),
                "Participant formatter did not preserve zero/one/two target policy.");

            V20ChoiceDefinition consume = PlainItemChoice(
                "qa:actionable-alert-consume",
                2,
                consume: true);
            V20ChoiceDefinition hold = PlainItemChoice(
                "qa:actionable-alert-hold",
                3,
                consume: false);
            string consumeText = (string)choiceOutcome.Invoke(
                null,
                new object[] { null, consume, false });
            string holdText = (string)choiceOutcome.Invoke(
                null,
                new object[] { null, hold, false });
            Require(consumeText.Contains(
                        "요구 물품: qa:actionable-alert-consume x2 (소비)",
                        StringComparison.Ordinal)
                    && holdText.Contains(
                        "요구 물품: qa:actionable-alert-hold x3 (보유 필요)",
                        StringComparison.Ordinal),
                "Choice formatter did not preserve consume/hold text.");

            VerifyWim040RelationshipArity(
                story,
                participants[0].Identity.PersistentId,
                participants[1].Identity.PersistentId);
            return "WIM039_ACTIONABLE_ALERT_DETAILS=PASS; "
                + "scope=ready; targets=0/1/2; choice=consume/hold; "
                + "wim040=one-skip/two-distinct; "
                + "scene/clock/save/autosave/SO=unchanged";
        }
        catch (Exception exception)
        {
            return "FROZEN: " + exception.GetType().Name + ": "
                + exception.Message;
        }
    }

    private static V20ChoiceDefinition PlainItemChoice(
        string itemId,
        int amount,
        bool consume) => new()
    {
        outcomeText = "qa:plain-choice",
        requirements = new V20ContentRequirementSet
        {
            items = new List<V20ItemAmountRequirement>
            {
                new()
                {
                    itemDefinitionId = itemId,
                    amount = amount,
                    consume = consume
                }
            }
        }
    };

    private static string DisplayNameOrId(CharacterActor actor) =>
        string.IsNullOrWhiteSpace(actor?.Identity?.DisplayName)
            ? actor?.Identity?.PersistentId ?? string.Empty
            : actor.Identity.DisplayName;

    private static void VerifyWim040RelationshipArity(
        V20StoryContentCatalog story,
        string firstParticipantId,
        string secondParticipantId)
    {
        LifeEventDefinitionSO relationship = story.LifeEvents.Single(value =>
            value != null
            && string.Equals(
                value.StableId,
                "life-event:shared-lullaby",
                StringComparison.Ordinal));
        Require(relationship.automatic
                && (relationship.automaticEffects
                        ?? new List<V20ContentEffect>())
                    .Any(effect => effect != null
                        && effect.kind == V20ContentEffectKind.Relationship),
            "WIM040 relationship fixture changed.");
        System.Reflection.MethodInfo stateFactory =
            typeof(V20RetirementChoiceScheduleLivePlayModeRunner).GetMethod(
                "CreateRetirementProducerFixtureState",
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic);
        System.Reflection.MethodInfo contextFactory =
            typeof(V20RetirementChoiceScheduleLivePlayModeRunner).GetMethod(
                "CreateRetirementProducerContext",
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic);
        Require(stateFactory != null && contextFactory != null,
            "WIM040 fixture helpers are unavailable.");

        string[] one = { firstParticipantId };
        V20CampaignRuntime skipped = new(
            new DungeonRuntimeAggregateRootStore(),
            story);
        skipped.PublishSociety(skipped.PrepareSociety(
            (SocietyEventWorldSaveData)stateFactory.Invoke(
                null,
                new object[] { story, one, relationship.StableId })));
        skipped.EvaluateDaily((V20DailyEventContext)contextFactory.Invoke(
            null,
            new object[]
            {
                1,
                one,
                new CharacterId(firstParticipantId),
                false
            }));
        Require(skipped.RecentResolvedSocietyEvents.All(value => !string.Equals(
                    value.definitionId,
                    relationship.StableId,
                    StringComparison.Ordinal)),
            "One participant started the authored two-person relationship event.");

        string[] two = { secondParticipantId, firstParticipantId };
        V20CampaignRuntime selected = new(
            new DungeonRuntimeAggregateRootStore(),
            story);
        selected.PublishSociety(selected.PrepareSociety(
            (SocietyEventWorldSaveData)stateFactory.Invoke(
                null,
                new object[] { story, two, relationship.StableId })));
        selected.EvaluateDaily((V20DailyEventContext)contextFactory.Invoke(
            null,
            new object[]
            {
                1,
                two,
                new CharacterId(firstParticipantId),
                false
            }));
        V20ActiveEventSaveData resolved = selected.RecentResolvedSocietyEvents
            .SingleOrDefault(value => string.Equals(
                value.definitionId,
                relationship.StableId,
                StringComparison.Ordinal));
        Require(resolved != null
                && resolved.participantCharacterIds.Count == 2
                && resolved.participantCharacterIds.Distinct(
                    StringComparer.Ordinal).Count() == 2,
            "Two distinct participants did not resolve the authored relationship event.");
    }

    private static string VerifyWim032033FactionContracts()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        V20StoryContentCatalog catalog = new(content);
        FactionContractDefinitionSO contract = catalog.Contracts.Single(value =>
            string.Equals(
                value.StableId,
                "faction-contract:beastkin:supply",
                StringComparison.Ordinal));
        Require(FactionContractMaterialRules.HasMaterialRequirements(contract),
            "The focused beastkin supply canary lost its material requirement.");
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        string factionId = contract.factionId;
        FactionContractDeliveryTarget target = new(
            EconomyProjectInputOwnerAuthority
                .BuildFactionContractDestinationId(contract.StableId),
            new Vector2Int(17, 19));

        Require(!campaign.TryAcceptContract(
                factionId,
                contract.StableId,
                20,
                out _),
            "Material contract bypassed the physical target command.");
        Require(((IFactionCampaignDeliveryCommand)campaign)
                .TryAcceptContract(
                    factionId,
                    contract.StableId,
                    20,
                    target,
                    out string acceptFailure),
            acceptFailure);
        string accepted = JsonUtility.ToJson(campaign.CaptureFactions());
        bool duplicateAccepted = ((IFactionCampaignDeliveryCommand)campaign)
            .TryAcceptContract(
                    factionId,
                    contract.StableId,
                    20,
                    target,
                    out _);
        Require(!duplicateAccepted
                && string.Equals(
                    accepted,
                    JsonUtility.ToJson(campaign.CaptureFactions()),
                    StringComparison.Ordinal),
            "Duplicate faction contract acceptance mutated state.");
        Require(((IFactionCampaignDeliveryCommand)campaign)
                .TrySetInputOwnerProjection(
                    factionId,
                    5_000L,
                    1L,
                    "focused-owner-fingerprint",
                    out string projectionFailure),
            projectionFailure);

        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        int quantity = material.Values.Sum();
        string beforeReceipt = JsonUtility.ToJson(campaign.CaptureFactions());
        FactionContractDeliveryReceipt mismatchedReceipt = new(
            PhysicalItemDispositionKind.Transfer,
            FactionContractDeliveryOutbox.FormatOperationId(contract.StableId),
            FactionContractDeliveryOutbox.TransferReason,
            "focused-request-mismatch",
            new[] { "stack:faction-contract-focused-mismatch" },
            quantity,
            5_001L);
        bool mismatchRecorded = ((IFactionCampaignDeliveryCommand)campaign)
            .TryRecordDeliveryReceipt(
                factionId,
                mismatchedReceipt,
                out _);
        Require(!mismatchRecorded
            && string.Equals(
                beforeReceipt,
                JsonUtility.ToJson(campaign.CaptureFactions()),
                StringComparison.Ordinal),
            "Faction delivery accepted a receipt outside its exact projection.");
        FactionContractDeliveryReceipt receipt = new(
            PhysicalItemDispositionKind.Transfer,
            FactionContractDeliveryOutbox.FormatOperationId(contract.StableId),
            FactionContractDeliveryOutbox.TransferReason,
            "focused-request",
            new[] { "stack:faction-contract-focused" },
            quantity,
            5_000L);
        Require(((IFactionCampaignDeliveryCommand)campaign)
                .TryRecordDeliveryReceipt(
                    factionId,
                    receipt,
                    out string receiptFailure),
            receiptFailure);
        string recorded = JsonUtility.ToJson(campaign.CaptureFactions());
        bool duplicateRecorded = ((IFactionCampaignDeliveryCommand)campaign)
            .TryRecordDeliveryReceipt(factionId, receipt, out _);
        Require(!duplicateRecorded
            && string.Equals(
                recorded,
                JsonUtility.ToJson(campaign.CaptureFactions()),
                StringComparison.Ordinal),
            "Duplicate faction delivery receipt mutated campaign state.");
        Require(!((IFactionCampaignDeliveryCommand)campaign)
                .TryMarkInputOwnerRetired(factionId, out _),
            "An active faction delivery input owner retired before terminal state.");

        V20DailyEventContext expiredWhilePending = new()
        {
            AbsoluteDay = 20 + contract.deadlineDays + 1,
            RunSeed = 321,
            Season = Season.Spring
        };
        campaign.EvaluateDaily(expiredWhilePending);
        Require(campaign.TryGetFaction(
                factionId,
                out FactionCampaignStateSaveData pending)
            && string.Equals(
                pending.activeContractId,
                contract.StableId,
                StringComparison.Ordinal)
            && !pending.failedContractIds.Contains(
                contract.StableId,
                StringComparer.Ordinal),
            "A pending physical receipt was expired as a failed contract.");

        FactionCampaignWorldSaveData activeSave = JsonUtility.FromJson<
            FactionCampaignWorldSaveData>(
            JsonUtility.ToJson(campaign.CaptureFactions()));
        V20CampaignRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        restored.PublishFactions(restored.PrepareFactions(activeSave));
        Require(string.Equals(
                JsonUtility.ToJson(restored.CaptureFactions()),
                JsonUtility.ToJson(activeSave),
                StringComparison.Ordinal),
            "Faction delivery candidate did not round-trip exactly.");
        PhysicalItemRestoreCandidateDispositionSnapshot physical = new(
            PhysicalItemDispositionKind.Transfer,
            receipt.OperationId,
            receipt.ReasonCode,
            receipt.RequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        FactionCampaignSaveSection.ValidatePhysicalRestoreJoin(
            activeSave,
            new FocusedPhysicalCandidate(physical));
        bool mismatchedRestoreRejected = false;
        try
        {
            PhysicalItemRestoreCandidateDispositionSnapshot mismatched = new(
                PhysicalItemDispositionKind.Transfer,
                receipt.OperationId,
                receipt.ReasonCode,
                receipt.RequestFingerprint,
                receipt.SourceStackIds,
                receipt.Quantity,
                receipt.InputMassGrams + 1L,
                receipt.CommitId);
            FactionCampaignSaveSection.ValidatePhysicalRestoreJoin(
                activeSave,
                new FocusedPhysicalCandidate(mismatched));
        }
        catch (InvalidOperationException)
        {
            mismatchedRestoreRejected = true;
        }
        Require(mismatchedRestoreRejected,
            "Faction delivery restore admitted a mismatched physical receipt.");

        RunMilestoneEvaluationSnapshot completion = Satisfy(
            new[] { contract.completionRequirements });
        Require(((IFactionCampaignDeliveryCommand)restored)
                .TryResolveDeliveredContract(
                    factionId,
                    completion,
                    out V20ResolvedEventResult resolved,
                    out string resolveFailure),
            resolveFailure);
        Require(resolved.Effects.All(value =>
                value.kind != V20ContentEffectKind.ItemConsume),
            "Receipt-gated completion retained global material consumption.");
        string completed = JsonUtility.ToJson(restored.CaptureFactions());
        bool duplicateResolved = ((IFactionCampaignDeliveryCommand)restored)
            .TryResolveDeliveredContract(
                    factionId,
                    completion,
                    out _,
                    out _);
        Require(!duplicateResolved
                && string.Equals(
                    completed,
                    JsonUtility.ToJson(restored.CaptureFactions()),
                    StringComparison.Ordinal),
            "Duplicate completion republished faction rewards.");
        Require(((IFactionCampaignDeliveryCommand)restored)
                .TryMarkInputOwnerRetired(
                    factionId,
                    out string retireFailure),
            retireFailure);
        Require(((IFactionCampaignDeliveryCommand)restored)
                .TryClearDeliveryOutbox(
                    factionId,
                    out string clearFailure),
            clearFailure);

        VerifyFactionContractExpiryOnce(catalog, contract);
        VerifyNonMaterialRequirementGuard();
        return "DETERMINISTIC_STATE_CONTRACT=PASS; "
            + "materialBypass=blocked; duplicateAccept=0; receiptDuplicate=0; "
            + "pendingExpiry=blocked; physicalRestoreJoin=exact+reject-mismatch; "
            + "globalConsume=0; duplicateReward=0; "
            + "expiryFailure=once; nonMaterialDelivery=0; "
            + "liveUi=NOT_RUN; liveHaul=NOT_RUN; wholeSave=NOT_RUN; "
            + "rejectStateMutation=0";
    }

    [MenuItem("DungeonStory/QA/V20 Campaign And Endless Rules")]
    public static void Run()
    {
        AccordSignalRestoreJoinFixture.Run();
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        V20StoryContentCatalog catalog = new(content);
        Require(catalog.All.Count == 9, "Milestone catalog count changed.");
        Require(catalog.Arcs.Count == 6
                && catalog.Chapters.Count == 36
                && catalog.Contracts.Count == 24
                && catalog.Contracts.Count(value => string.IsNullOrEmpty(value.seasonalEventId)) == 18
                && catalog.Contracts.Count(value => value.seasonalEventId == "seasonal:winter-fuel-demand") == 6,
            "Faction-story catalog counts changed.");

        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        BuildingSO[] landmarkBuildings = content.GetAll<BuildingSO>()
            .Where(value => value != null && campaign.IsLandmarkBuilding(value.ContentDefinitionId))
            .OrderBy(value => value.id)
            .ToArray();
        Require(landmarkBuildings.Length == 9,
            $"Expected 9 physical landmark buildings, found {landmarkBuildings.Length}.");
        Require(landmarkBuildings.All(value =>
                !campaign.IsLandmarkUnlocked(value.ContentDefinitionId)
                && !FacilityProgression.IsUnlocked(
                    value,
                    new GameSessionState(),
                    null,
                    DisabledDungeonDebugRuleQuery.Instance,
                    campaign)),
            "An unearned milestone landmark was constructible before completion.");
        List<string> completed = new();
        RunMilestoneEvaluationSnapshot allRequirements = null;
        for (int day = 1; day <= 120; day++)
        {
            allRequirements = Satisfy(
                catalog.All.Select(value => value.completionRequirements));
            allRequirements.AbsoluteDay = day;
            allRequirements.WorldFlags.Add("ecology:self-sufficient-today");
            completed.AddRange(campaign.Evaluate(allRequirements));
        }
        Require(completed.Count == 9,
            $"All authored milestone conditions completed {completed.Count}/9 milestones after the required 120-day self-sufficiency streak.");
        Require(campaign.Phase == RunProgressionPhase.EndlessAge,
            "The first grand milestone did not unlock EndlessAge.");
        Require(campaign.Evaluate(allRequirements).Count == 0,
            "A one-time milestone completed twice.");
        Require(campaign.CompletedMilestoneIds.Count == 9,
            "Milestone completion history is incomplete.");
        Require(catalog.All.All(value =>
                campaign.IsLandmarkUnlocked(value.landmarkBuildingId)),
            "A completed milestone did not unlock its physical landmark.");
        Require(landmarkBuildings.All(value => FacilityProgression.IsUnlocked(
                value,
                new GameSessionState(),
                null,
                DisabledDungeonDebugRuleQuery.Instance,
                campaign)),
            "A completed milestone did not unlock construction of its physical landmark.");
        VerifyMilestoneGameplayModifiers(campaign);

        IReadOnlyList<string> firstCrisis =
            campaign.ComposeNextEndlessCrisis(1_000, 94721);
        Require(firstCrisis.Count is 1 or 2
                && firstCrisis.Distinct(StringComparer.Ordinal).Count()
                    == firstCrisis.Count,
            "Endless crisis composition did not select one or two distinct authored axes.");

        VerifyFactionBranches(catalog);
        VerifyPersistedWorkDelay(catalog);
        VerifySelfSufficiencyStreak(catalog);
        VerifyTenYearBounds(catalog);
        VerifyCulturalPracticeOutcomePersistence(content);
        VerifyTraitAnalysisPersistence(content);
        VerifyTenThousandGeneralTraitDeterminism(content);
        VerifyTenThousandInheritanceDeterminism();
        VerifyThreeGenerationNarrativeCompression(content);
        VerifyRoundTrip(campaign);

        Debug.Log(
            "V20_CAMPAIGN_RULES=PASS; milestones=9; factions=6x6; "
            + $"endlessAxes={firstCrisis.Count}; selfSufficiencyDays=120; "
            + "workDelayPersisted=true; tenYearHistoryBounded=true; "
            + "practiceNeglectPersisted=true; traitAnalysisPersisted=true; "
            + "generalTraitDeterminism=10000; inheritanceDeterminism=10000; "
            + "narrativePopulation=2000x3generations; saveRoundTrip=true");
    }

    private static void VerifyTenThousandGeneralTraitDeterminism(
        ResourceGameContentCatalog content)
    {
        CharacterTraitSO[] traits = content.GetAll<CharacterTraitSO>()
            .Where(value => value != null)
            .OrderBy(value => value.id)
            .ToArray();
        CharacterSkillSystemSettingsSO settings = content
            .GetAll<CharacterSkillSystemSettingsSO>()
            .Single();
        Require(traits.Length == 100,
            $"Expected 100 general traits, found {traits.Length}.");

        for (int index = 0; index < 10_000; index++)
        {
            int seed = CharacterGrowthRules.StableHash($"qa:trait:{index}");
            IReadOnlyList<int> first = CharacterTraitSelectionRules.Select(
                traits,
                settings.traitConflicts,
                new DeterministicRandomSequence(seed),
                "Slime");
            IReadOnlyList<int> second = CharacterTraitSelectionRules.Select(
                traits.Reverse(),
                settings.traitConflicts,
                new DeterministicRandomSequence(seed),
                "Slime");
            Require(first.Count >= 1 && first.Count <= 4
                    && first.SequenceEqual(second),
                $"General trait selection lost deterministic ordering at sample {index}.");
            Require(!settings.traitConflicts.Any(rule => rule != null
                    && first.Contains(rule.firstTraitId)
                    && first.Contains(rule.secondTraitId)),
                $"General trait selection admitted a conflicting pair at sample {index}.");
        }
    }

    private static void VerifyFactionContractExpiryOnce(
        V20StoryContentCatalog catalog,
        FactionContractDefinitionSO completedContract)
    {
        FactionContractDefinitionSO expiring = catalog.Contracts
            .Where(value => string.Equals(
                value.factionId,
                completedContract.factionId,
                StringComparison.Ordinal)
                && FactionContractMaterialRules.HasMaterialRequirements(value)
                && !string.Equals(
                    value.StableId,
                    completedContract.StableId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .First();
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        FactionContractDeliveryTarget target = new(
            EconomyProjectInputOwnerAuthority
                .BuildFactionContractDestinationId(expiring.StableId),
            new Vector2Int(23, 29));
        Require(((IFactionCampaignDeliveryCommand)campaign)
                .TryAcceptContract(
                    expiring.factionId,
                    expiring.StableId,
                    40,
                    target,
                    out string acceptFailure),
            acceptFailure);
        Require(((IFactionCampaignDeliveryCommand)campaign)
                .TrySetInputOwnerProjection(
                    expiring.factionId,
                    4_000L,
                    1L,
                    "expiry-owner-fingerprint",
                    out string projectionFailure),
            projectionFailure);
        V20DailyEventContext expiry = new()
        {
            AbsoluteDay = 40 + expiring.deadlineDays + 1,
            RunSeed = 654,
            Season = Season.Spring
        };
        campaign.EvaluateDaily(expiry);
        Require(campaign.TryGetFaction(
                expiring.factionId,
                out FactionCampaignStateSaveData failed)
            && failed.failedContractIds.Count(value => string.Equals(
                value,
                expiring.StableId,
                StringComparison.Ordinal)) == 1
            && failed.deliveryTerminalCleanupPending,
            "Expired material contract did not retain one cleanup-owned failure.");
        int rapportAfterFailure = failed.rapport;
        int grievanceAfterFailure = failed.grievance;
        int obligationAfterFailure = failed.obligationTokens;
        campaign.EvaluateDaily(new V20DailyEventContext
        {
            AbsoluteDay = expiry.AbsoluteDay + 1,
            RunSeed = 655,
            Season = Season.Spring
        });
        Require(campaign.TryGetFaction(
                expiring.factionId,
                out failed)
            && failed.failedContractIds.Count(value => string.Equals(
                value,
                expiring.StableId,
                StringComparison.Ordinal)) == 1
            && failed.rapport == rapportAfterFailure
            && failed.grievance == grievanceAfterFailure
            && failed.obligationTokens == obligationAfterFailure,
            "Expired faction contract applied its failure twice.");
    }

    private static void VerifyNonMaterialRequirementGuard()
    {
        FactionContractDefinitionSO fixture = ScriptableObject.CreateInstance<
            FactionContractDefinitionSO>();
        try
        {
            fixture.completionRequirements.items.Add(
                new V20ItemAmountRequirement
                {
                    itemDefinitionId = "qa:non-consumed-evidence",
                    amount = 2,
                    consume = false
                });
            Require(!FactionContractMaterialRules.HasMaterialRequirements(fixture)
                    && FactionContractMaterialRules
                        .BuildNonMaterialRequirements(fixture)
                        .items.Count == 1,
                "A consume=false requirement became physical delivery.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    private sealed class FocusedPhysicalCandidate :
        IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateDispositionSnapshot> receipts;

        public FocusedPhysicalCandidate(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
        {
            this.receipts = receipts ?? Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>();
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => receipts;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = receipts.FirstOrDefault(value => string.Equals(
                value.OperationId,
                operationId,
                StringComparison.Ordinal));
            return disposition != null;
        }
    }

    private static void VerifyTenThousandInheritanceDeterminism()
    {
        string[] firstParent =
        {
            "heritable:reinforced-joints",
            "heritable:dense-bone",
            "heritable:regrowing-tissue"
        };
        string[] secondParent =
        {
            "heritable:expanded-lung",
            "heritable:efficient-digestion",
            "heritable:mana-grounding"
        };
        for (int index = 0; index < 10_000; index++)
        {
            string seed = $"qa:inheritance:{index}";
            ReproductionRules.SelectInheritedTraits(
                firstParent,
                secondParent,
                seed,
                out IReadOnlyList<string> firstExpressed,
                out IReadOnlyList<string> firstLatent);
            ReproductionRules.SelectInheritedTraits(
                firstParent.Reverse(),
                secondParent.Reverse(),
                seed,
                out IReadOnlyList<string> secondExpressed,
                out IReadOnlyList<string> secondLatent);
            Require(firstExpressed.Count <= 4
                    && firstLatent.Count <= 2
                    && firstExpressed.SequenceEqual(
                        secondExpressed,
                        StringComparer.Ordinal)
                    && firstLatent.SequenceEqual(
                        secondLatent,
                        StringComparer.Ordinal),
                $"Heritable selection lost deterministic ordering at sample {index}.");
        }
    }

    private static void VerifyThreeGenerationNarrativeCompression(
        ResourceGameContentCatalog content)
    {
        CharacterNarrativeCatalog catalog = new(content);
        CharacterNarrativeRuntime narrative = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        LifeEventDefinitionSO lifeEvent = catalog.LifeEvents.First(value =>
            value.choices != null && value.choices.Count > 0);
        string choiceId = lifeEvent.choices[0].choiceId;
        CharacterSpeciesId speciesId = new(
            catalog.Cultures[0].defaultSpeciesId);

        const int population = 2_000;
        const int recordedEvents = 15;
        for (int index = 0; index < population; index++)
        {
            int generation = index % 3;
            CharacterId characterId = CharacterId.FromStableSuffix(
                $"qa:narrative:g{generation}:{index}");
            narrative.Register(
                characterId,
                speciesId,
                Array.Empty<string>(),
                Array.Empty<string>());
            for (int eventIndex = 0; eventIndex < recordedEvents; eventIndex++)
            {
                narrative.RecordResolvedEvent(
                    characterId,
                    new NarrativeEventId(lifeEvent.StableId),
                    choiceId,
                    eventIndex + 1);
            }
        }

        CharacterNarrativeWorldSaveData captured = narrative.Capture();
        Require(captured.characters.Count == population
                && captured.characters.All(value =>
                    value.recentEvents.Count == 12
                    && value.eventSummaries.Sum(summary => summary.count)
                        == recordedEvents - 12),
            "Three-generation narrative history did not compact to 12 recent events plus summaries.");

        CharacterNarrativeRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        restored.PublishRestore(restored.PrepareRestore(captured));
        Require(restored.All.Count == population
                && restored.All.All(value =>
                    value.RecentEvents.Count == 12
                    && value.EventSummaries.Sum(summary => summary.count)
                        == recordedEvents - 12),
            "Compressed three-generation narrative state did not survive restoration.");
    }

    private static void VerifyCulturalPracticeOutcomePersistence(
        ResourceGameContentCatalog content)
    {
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime source = new(
            new DungeonRuntimeAggregateRootStore(),
            narrativeCatalog);
        SpeciesCultureDefinitionSO culture = narrativeCatalog.Cultures[0];
        CulturalPracticeDefinitionSO practice = narrativeCatalog.Practices
            .First(value => string.Equals(
                value.cultureId,
                culture.StableId,
                StringComparison.Ordinal));
        CharacterId characterId = new("character:qa:practice-neglect");
        source.Register(
            characterId,
            new CharacterSpeciesId(culture.defaultSpeciesId),
            Array.Empty<string>(),
            Array.Empty<string>());
        source.RecordPracticeNeglect(characterId, practice.StableId, 10);

        CharacterNarrativeWorldSaveData save = source.Capture();
        CharacterNarrativeRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            narrativeCatalog);
        restored.PublishRestore(restored.PrepareRestore(save));
        Require(restored.TryGet(characterId, out CharacterNarrativeSnapshot snapshot)
                && snapshot.PracticeParticipations.Count == 1
                && !snapshot.PracticeParticipations[0].performed
                && snapshot.PracticeParticipations[0].lastAbsoluteDay == 10,
            "Cultural-practice neglect outcome did not round-trip.");
        Require(!restored.CanPerformPractice(
                    characterId,
                    practice.StableId,
                    19,
                    out int nextAllowed)
                && nextAllowed == 20
                && restored.CanPerformPractice(
                    characterId,
                    practice.StableId,
                    20,
                    out _),
            "Cultural-practice neglect did not persist the authored cooldown.");
    }

    private static void VerifyTraitAnalysisPersistence(
        ResourceGameContentCatalog content)
    {
        CharacterNarrativeCatalog catalog = new(content);
        CharacterNarrativeRuntime source = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        SpeciesCultureDefinitionSO culture = catalog.Cultures[0];
        CharacterId characterId = new("character:qa:trait-analysis");
        string latentTraitId = catalog.HeritableTraits[0].traitId;
        CharacterNarrativeSnapshot before = source.Register(
            characterId,
            new CharacterSpeciesId(culture.defaultSpeciesId),
            Array.Empty<string>(),
            new[] { latentTraitId });
        Require(!before.HeritableTraitsAnalyzed
                && before.VisibleLatentHeritableTraitIds.Count == 0,
            "Latent hereditary traits were visible before physical analysis.");

        source.MarkHeritableTraitsAnalyzed(characterId);
        CharacterNarrativeWorldSaveData save = source.Capture();
        CharacterNarrativeRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        restored.PublishRestore(restored.PrepareRestore(save));
        Require(restored.TryGet(characterId, out CharacterNarrativeSnapshot after)
                && after.HeritableTraitsAnalyzed
                && after.VisibleLatentHeritableTraitIds.SequenceEqual(
                    new[] { latentTraitId },
                    StringComparer.Ordinal),
            "Trait-analysis reveal state did not round-trip.");
    }

    private static void VerifyMilestoneGameplayModifiers(
        V20CampaignRuntime campaign)
    {
        Require(campaign.GrantedRewardIds.Count == 9
                && campaign.ActivePressureIds.Count == 9,
            "Completed milestones did not retain all reward and pressure IDs.");
        Require(campaign.EnemyCounterIntelVisible
                && Math.Abs(campaign.ExpeditionTravelTimeMultiplier - 0.9f)
                    < 0.0001f
                && Math.Abs(campaign.FacilityMaintenanceGoldMultiplier - 0.9f)
                    < 0.0001f
                && Math.Abs(
                    campaign.WaterAndFertilizerConsumptionMultiplier - 0.9f)
                    < 0.0001f
                && campaign.MentorshipDailyXpCap == 15
                && campaign.TemporalStasisWarningDays == 3
                && Math.Abs(campaign.ManaTransferLossMultiplier - 0.9f)
                    < 0.0001f
                && Math.Abs(campaign.AutomaticMaintenanceWorkMultiplier - 0.85f)
                    < 0.0001f
                && campaign.IsAccordSignalSupportDay(
                    GameCalendarRules.DaysPerSeason),
            "A completed milestone remained a record-only reward.");
        Require(campaign.HasPressure("ending:truth-revealed")
                && campaign.HasPressure("ending:steel-apotheosis"),
            "Typed milestone pressure projection is incomplete.");
    }

    private static void VerifyPersistedWorkDelay(
        V20StoryContentCatalog catalog)
    {
        ServiceIncidentDefinitionSO incident = catalog.ServiceIncidents.Single(value =>
            value.kind == ServiceIncidentKind.MedicalCollapse);
        V20ChoiceDefinition response = incident.responses.First(value =>
            value.effects.Any(effect =>
                effect.kind == V20ContentEffectKind.WorkDelayDays
                && effect.amount > 0));
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        SocietyEventWorldSaveData state = new()
        {
            lastEvaluationAbsoluteDay = 10
        };
        campaign.PublishSociety(campaign.PrepareSociety(state));
        ObservedMealIncidentCaptureResult observed = campaign.CaptureObservedMealIncident(
            new ObservedMealIncidentSnapshot(
                new ConsumableOperationId("consumable-operation:qa:work-delay"),
                new CharacterId("character:qa:work-delay"), new ItemDefinitionId("food:lavish-meat"),
                new ItemStackId("stack:qa:work-delay"), default, true,
                new CoreGridCell(0, 0), 10, false, true, false),
            new V20DailyEventContext { AbsoluteDay = 10, RunSeed = 157181, Season = Season.Spring });
        Require(observed.Created, "Controlled observed work-delay incident was not created.");
        Require(campaign.TryResolveSocietyEvent(
                observed.OccurrenceInstanceId,
                response.choiceId,
                new RunMilestoneEvaluationSnapshot(),
                out _,
                out string failure),
            $"A work-delay response could not resolve: {failure}");
        Require(campaign.GetRemainingDays() > 0
                && campaign.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research) < 1f,
            "The authored work-delay effect did not affect real work speed.");

        SocietyEventWorldSaveData captured = campaign.CaptureSociety();
        V20CampaignRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        restored.PublishSociety(restored.PrepareSociety(captured));
        Require(restored.GetRemainingDays() == campaign.GetRemainingDays()
                && Math.Abs(restored.GetWorkSpeedMultiplier(
                    BuiltInWorkTypeIds.Research)
                    - campaign.GetWorkSpeedMultiplier(
                        BuiltInWorkTypeIds.Research)) < 0.0001f,
            "The work-delay state did not survive society save restoration.");

        V20CampaignRuntime flood = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        const int floodStartDay = 10;
        flood.ApplyWorkDelay("flood", 2, floodStartDay);
        WorkTypeId[] affected =
        {
            BuiltInWorkTypeIds.Sow,
            BuiltInWorkTypeIds.Harvest,
            BuiltInWorkTypeIds.Haul,
            BuiltInWorkTypeIds.Restock
        };
        Require(affected.All(workType => Math.Abs(
                    flood.GetWorkSpeedMultiplier(workType)
                    - ContentWorkDelaySpeedAuthority.PerActiveDelayMultiplier)
                < 0.0001f)
                && Math.Abs(flood.GetWorkSpeedMultiplier(
                    BuiltInWorkTypeIds.AnimalCare) - 1f) < 0.0001f
                && Math.Abs(flood.GetWorkSpeedMultiplier(
                    BuiltInWorkTypeIds.Research) - 1f) < 0.0001f,
            "Flood work delay did not follow exact agriculture/logistics capabilities.");
        flood.EvaluateDaily(new V20DailyEventContext
        {
            AbsoluteDay = floodStartDay + 2,
            RunSeed = 43043,
            Season = Season.Spring
        });
        Require(affected.All(workType => Math.Abs(
                    flood.GetWorkSpeedMultiplier(workType) - 1f)
                < 0.0001f),
            "Flood work delay did not expire on its authored day boundary.");
    }

    private static void VerifyFactionBranches(V20StoryContentCatalog catalog)
    {
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        foreach (FactionArcDefinitionSO arc in catalog.Arcs)
        {
            for (int chapterNumber = 1; chapterNumber <= 6; chapterNumber++)
            {
                FactionChapterDefinitionSO chapter = catalog.Chapters.Single(
                    value => value.factionId == arc.factionId
                        && value.chapterNumber == chapterNumber);
                V20ChoiceDefinition choice = chapter.choices[0];
                V20ChoiceDefinition bargain = chapter.choices.Single(value =>
                    string.Equals(value.choiceId, "bargain", StringComparison.Ordinal));
                int supportCost = choice.requirements.items
                    .Where(value => value.consume)
                    .Sum(value => value.amount);
                int bargainCost = bargain.requirements.items
                    .Where(value => value.consume)
                    .Sum(value => value.amount);
                Require(supportCost > bargainCost && bargainCost > 0,
                    $"Faction chapter costs are not mechanically distinct: {chapter.StableId}");
                int priorCrossRapport = 0;
                int priorCrossGrievance = 0;
                if (!string.IsNullOrWhiteSpace(chapter.crossFactionId)
                    && campaign.TryGetFaction(chapter.crossFactionId, out FactionCampaignStateSaveData beforeCross))
                {
                    priorCrossRapport = beforeCross.rapport;
                    priorCrossGrievance = beforeCross.grievance;
                }
                RunMilestoneEvaluationSnapshot requirements = Satisfy(
                    new[] { chapter.triggerRequirements, choice.requirements });
                Require(campaign.TryResolveChapter(
                        arc.factionId,
                        choice.choiceId,
                        requirements,
                        out V20ResolvedEventResult resolved,
                        out string failure),
                    $"Faction chapter was unreachable: {chapter.StableId}; {failure}");
                Require(resolved.Effects.Any(value =>
                        value.kind == V20ContentEffectKind.ItemConsume
                        && Mathf.RoundToInt(value.amount) == supportCost),
                    $"Faction chapter did not return its physical item consumption: {chapter.StableId}");
                if (!string.IsNullOrWhiteSpace(chapter.crossFactionId))
                {
                    Require(campaign.TryGetFaction(
                            chapter.crossFactionId,
                            out FactionCampaignStateSaveData afterCross)
                        && (afterCross.rapport != priorCrossRapport
                            || afterCross.grievance != priorCrossGrievance),
                        $"Cross-faction chapter did not change its counterpart: {chapter.StableId}");
                }
            }

            FactionContractDefinitionSO contract = catalog.Contracts.First(
                value => value.factionId == arc.factionId);
            FactionContractDeliveryTarget target = new(
                EconomyProjectInputOwnerAuthority
                    .BuildFactionContractDestinationId(contract.StableId),
                new Vector2Int(7, 9));
            Require(((IFactionCampaignDeliveryCommand)campaign)
                    .TryAcceptContract(
                        arc.factionId,
                        contract.StableId,
                        100,
                        target,
                        out string acceptFailure),
                $"Faction contract could not be accepted: {acceptFailure}");
            Require(((IFactionCampaignDeliveryCommand)campaign)
                    .TrySetInputOwnerProjection(
                        arc.factionId,
                        1_000L,
                        1L,
                        "qa-capacity-fingerprint",
                        out string projectionFailure),
                projectionFailure);
            IReadOnlyDictionary<string, int> material =
                FactionContractMaterialRules.BuildMaterialRequirements(contract);
            FactionContractDeliveryReceipt receipt = new(
                PhysicalItemDispositionKind.Transfer,
                FactionContractDeliveryOutbox.FormatOperationId(
                    contract.StableId),
                FactionContractDeliveryOutbox.TransferReason,
                "qa-request",
                new[] { $"stack:{contract.StableId}" },
                material.Values.Sum(),
                1_000L);
            Require(((IFactionCampaignDeliveryCommand)campaign)
                    .TryRecordDeliveryReceipt(
                        arc.factionId,
                        receipt,
                        out string receiptFailure),
                receiptFailure);
            Require(((IFactionCampaignDeliveryCommand)campaign)
                    .TryResolveDeliveredContract(
                        arc.factionId,
                        Satisfy(new[] { contract.completionRequirements }),
                        out V20ResolvedEventResult contractResult,
                        out string resolveFailure),
                $"Faction contract could not be completed: {resolveFailure}");
            Require(contractResult.Effects.All(value =>
                    value.kind != V20ContentEffectKind.ItemConsume),
                "Delivered faction material was also returned as a global item cost.");
            Require(((IFactionCampaignDeliveryCommand)campaign)
                    .TryMarkInputOwnerRetired(
                        arc.factionId,
                        out string retireFailure),
                retireFailure);
            Require(((IFactionCampaignDeliveryCommand)campaign)
                    .TryClearDeliveryOutbox(
                        arc.factionId,
                        out string clearFailure),
                clearFailure);
        }
    }

    private static void VerifySelfSufficiencyStreak(
        V20StoryContentCatalog catalog)
    {
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        List<string> completed = new();
        for (int day = 1; day <= 120; day++)
        {
            RunMilestoneEvaluationSnapshot snapshot = new()
            {
                AbsoluteDay = day,
                EligibleCharacterCount = 20
            };
            snapshot.CompletedResearchIds.Add(7255);
            snapshot.WorldFlags.Add("ecology:self-sufficient-today");
            completed.AddRange(campaign.Evaluate(snapshot));
        }
        Require(completed.Contains(
                "ending:sealed-paradise",
                StringComparer.Ordinal),
            "A continuous 120-day self-sufficient run did not unlock the sealed paradise milestone.");
    }

    private static void VerifyTenYearBounds(V20StoryContentCatalog catalog)
    {
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        for (int day = 1; day <= GameCalendarRules.DaysPerYear * 10; day++)
        {
            V20DailyEventContext context = new()
            {
                AbsoluteDay = day,
                RunSeed = 7711,
                Season = GameCalendarRules.Project(day, 0).Season,
                Generation = day / GameCalendarRules.DaysPerYear
            };
            context.ParticipantCharacterIds.Add("character:qa:one");
            context.ParticipantCharacterIds.Add("character:qa:two");
            context.ParticipantCharacterIds.Add("character:qa:three");
            campaign.EvaluateDaily(context);
            Require(campaign.ActiveSocietyEvents.Count <= (day <= 30 ? 2 : 3),
                "Society event queue exceeded its ordinary/emergency cap.");
        }
        Require(campaign.RecentResolvedSocietyEvents.Count <= 256,
            "Ten-year society history exceeded the bounded recent-history cap.");
    }

    private static void VerifyRoundTrip(V20CampaignRuntime source)
    {
        SeasonalEventWorldSaveData seasonal = source.CaptureSeasonal();
        SocietyEventWorldSaveData society = source.CaptureSociety();
        FactionCampaignWorldSaveData factions = source.CaptureFactions();
        RunMilestoneWorldSaveData milestones = source.CaptureMilestones();
        V20CampaignRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            new V20StoryContentCatalog(new ResourceGameContentCatalog(
                new UnityGameContentRootLoader())));
        restored.PublishSeasonal(restored.PrepareSeasonal(seasonal));
        restored.PublishSociety(restored.PrepareSociety(society));
        restored.PublishFactions(restored.PrepareFactions(factions));
        restored.PublishMilestones(restored.PrepareMilestones(milestones));
        Require(string.Equals(
                JsonUtility.ToJson(restored.CaptureMilestones()),
                JsonUtility.ToJson(milestones),
                StringComparison.Ordinal),
            "Milestone aggregate did not round-trip exactly.");
    }

    private static RunMilestoneEvaluationSnapshot Satisfy(
        IEnumerable<V20ContentRequirementSet> requirementSets)
    {
        RunMilestoneEvaluationSnapshot snapshot = new()
        {
            EligibleCharacterCount = 10_000
        };
        foreach (V20ContentRequirementSet requirements in
            requirementSets.Where(value => value != null))
        {
            foreach (V20ResearchRequirement value in requirements.research)
                snapshot.CompletedResearchIds.Add(value.researchNumericId);
            foreach (string value in requirements.requiredFlags)
                snapshot.WorldFlags.Add(value);
            foreach (V20WorldMetricRequirement value in requirements.worldMetrics)
                snapshot.WorldMetrics[value.kind] = Math.Max(
                    value.minimumValue,
                    snapshot.WorldMetrics.TryGetValue(value.kind, out float current)
                        ? current
                        : 0f);
            foreach (V20ItemAmountRequirement value in requirements.items)
                snapshot.ItemQuantities[value.itemDefinitionId] = Math.Max(
                    value.amount,
                    snapshot.ItemQuantities.TryGetValue(
                        value.itemDefinitionId,
                        out int current) ? current : 0);
            foreach (V20FacilityRequirement value in requirements.facilities)
            {
                string id = !string.IsNullOrWhiteSpace(value.buildingDefinitionId)
                    ? value.buildingDefinitionId
                    : "capability:" + value.capabilityId;
                snapshot.FacilityCounts[id] = Math.Max(
                    value.minimumCount,
                    snapshot.FacilityCounts.TryGetValue(id, out int current)
                        ? current
                        : 0);
            }
            foreach (V20FactionRequirement value in requirements.factions)
                snapshot.Factions[value.factionId] = new FactionCampaignStateSaveData
                {
                    factionId = value.factionId,
                    rapport = value.minimumRapport,
                    grievance = value.maximumGrievance,
                    obligationTokens = value.minimumObligationTokens,
                    currentChapter = 1
                };
        }
        return snapshot;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

public sealed class V20FactionContractLivePlayModeRunner : MonoBehaviour
{
    private const float RuntimeReadyTimeoutSeconds = 45f;
    private readonly List<string> report = new();
    private DungeonGameSaveData baseline;
    private IDungeonGameSaveService saves;
    private DungeonAutosaveService autosave;
    private IGameSpeedController gameSpeed;
    private float originalTimeScale;
    private bool originalGamePause;
    private bool verificationCompleted;
    private bool autosaveIsolated;
    private bool gameClockCaptured;
    private bool committedTemporaryRun;

    public bool ExitPlayModeOnCompletion { get; set; }

    private IEnumerator Start()
    {
        if (File.Exists(V20CampaignDebugScenarios.Wim032033RequestPath))
            File.Delete(V20CampaignDebugScenarios.Wim032033RequestPath);
        originalTimeScale = Time.timeScale;
        report.Add("WIM-032/033 live faction-contract verification");
        report.Add($"utc={DateTime.UtcNow:O}");

        Exception verificationFailure = null;
        IEnumerator verification = VerifyLiveFlow();
        while (true)
        {
            object current = null;
            bool moved;
            try
            {
                moved = verification.MoveNext();
                if (moved)
                    current = verification.Current;
            }
            catch (Exception exception)
            {
                verificationFailure = exception;
                break;
            }
            if (!moved)
                break;
            yield return current;
        }
        (verification as IDisposable)?.Dispose();

        if (baseline != null && saves != null)
        {
            try
            {
                DungeonGameSaveData cleanup = saves.FromJson(
                    saves.ToJson(baseline));
                if (!saves.TryRestore(
                        cleanup,
                        out DungeonGameRestoreReport cleanupReport)
                    || !cleanupReport.Success)
                {
                    throw new InvalidOperationException(
                        "Baseline cleanup restore failed: "
                        + string.Join(" | ", cleanupReport.Errors));
                }
                report.Add("[PASS] CLEANUP baseline whole-save restored");
            }
            catch (Exception exception)
            {
                verificationFailure ??= exception;
                report.Add("[FAIL] CLEANUP " + exception.Message);
            }
        }
        if (gameClockCaptured)
        {
            try
            {
                gameSpeed.SetPaused(originalGamePause);
                Time.timeScale = originalTimeScale;
                report.Add("[PASS] CLEANUP clock restored; paused="
                    + originalGamePause + "; timeScale="
                    + originalTimeScale);
            }
            catch (Exception exception)
            {
                verificationFailure ??= exception;
                report.Add("[FAIL] CLEANUP clock restore: "
                    + exception.Message);
            }
        }
        else
        {
            Time.timeScale = originalTimeScale;
        }
        if (autosaveIsolated && !ExitPlayModeOnCompletion)
        {
            try
            {
                autosave.Start();
                report.Add("[PASS] CLEANUP autosave subscription restored");
            }
            catch (Exception exception)
            {
                verificationFailure ??= exception;
                report.Add("[FAIL] CLEANUP autosave restart: "
                    + exception.Message);
            }
        }
        else if (autosaveIsolated)
        {
            report.Add("[PASS] CLEANUP autosave remained isolated through PlayMode exit");
        }

        bool passed = verificationCompleted && verificationFailure == null;
        if (verificationFailure != null)
        {
            report.Add("[FAIL] " + verificationFailure.GetType().Name
                + ": " + verificationFailure.Message);
        }
        report.Insert(0, passed
            ? "WIM032_033_FACTION_CONTRACTS_LIVE=PASS"
            : "WIM032_033_FACTION_CONTRACTS_LIVE=FROZEN");
        report.Add("failures=" + (passed ? 0 : 1));
        Directory.CreateDirectory(Path.GetDirectoryName(
            V20CampaignDebugScenarios.Wim032033ReportPath));
        File.WriteAllLines(
            V20CampaignDebugScenarios.Wim032033ReportPath,
            report);
        if (passed)
            Debug.Log(string.Join("\n", report));
        else
            Debug.LogError(string.Join("\n", report));

        if (ExitPlayModeOnCompletion)
            EditorApplication.ExitPlaymode();
        Destroy(gameObject);
    }

    private IEnumerator VerifyLiveFlow()
    {
        DungeonRuntimeLifetimeScope scope = null;
        float scopeDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < scopeDeadline)
        {
            scope = Resources
                .FindObjectsOfTypeAll<DungeonRuntimeLifetimeScope>()
                .FirstOrDefault(value => value != null
                    && value.gameObject.scene.IsValid()
                    && value.Container != null);
            if (scope != null)
                break;
            yield return null;
        }
        Require(scope != null, "Live gameplay DI scope is unavailable.");

        gameSpeed = scope.Container.Resolve<IGameSpeedController>();
        originalGamePause = gameSpeed.IsPaused;
        gameClockCaptured = true;
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        autosave = scope.Container.Resolve<IDungeonSaveCommandService>()
            as DungeonAutosaveService;
        Require(autosave != null,
            "Live faction verification requires the production autosave service.");
        autosave.Dispose();
        autosaveIsolated = true;

        IOwnerRunManagerProvider ownerManagers = scope.Container
            .Resolve<IOwnerRunManagerProvider>();
        Require(ownerManagers.TryGetManager(out OwnerRunManager ownerManager)
                && ownerManager != null,
            "Gameplay scene has no production owner-run manager.");
        float ownerCandidateDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (ownerManager.CurrentOwnerActor == null
               && (ownerManager.OwnerCandidates == null
                   || ownerManager.OwnerCandidates.Count == 0)
               && Time.realtimeSinceStartup < ownerCandidateDeadline)
        {
            if (ownerManagers.TryGetManager(
                    out OwnerRunManager refreshedOwnerManager)
                && refreshedOwnerManager != null)
            {
                ownerManager = refreshedOwnerManager;
            }
            yield return null;
        }
        if (ownerManager.CurrentOwnerActor == null)
        {
            Require(ownerManager.OwnerCandidates?.Count > 0,
                "Production owner candidates were not ready before fixture bootstrap.");
            committedTemporaryRun = true;
            // RunFastCommitForDebug can mutate the prepared-party run before
            // returning or throwing. Mark the run disposable first so every
            // failure after this boundary still exits PlayMode and discards it.
            ExitPlayModeOnCompletion = true;
            string bootstrap = StartPartyPreparationPlayModeVerifier
                .RunFastCommitForDebug();
            report.Add("[INFO] FIXTURE_BOOTSTRAP production prepared-party commit; "
                + bootstrap);
        }

        gameSpeed.SetPaused(false);
        float runTimeScale = Mathf.Max(1f, gameSpeed.Speed);
        Require(!gameSpeed.IsPaused && Time.timeScale > 0f,
            "Production clock did not resume after prepared-party bootstrap.");
        report.Add("[PASS] FIXTURE_CLOCK verification=" + runTimeScale
            + "; originalTimeScale=" + originalTimeScale
            + "; originalPaused=" + originalGamePause
            + "; cleanup=restore-original-authority");

        IOffensePanelService panelService =
            scope.Container.Resolve<IOffensePanelService>();
        IFactionContractDeliveryQuery contracts =
            scope.Container.Resolve<IFactionContractDeliveryQuery>();
        V20CampaignRuntime campaign =
            scope.Container.Resolve<V20CampaignRuntime>();
        V20StoryContentCatalog catalog =
            scope.Container.Resolve<V20StoryContentCatalog>();
        IWorldItemStackRuntime items =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        IWorldDropZoneQuery dropZones =
            scope.Container.Resolve<IWorldDropZoneQuery>();
        IPhysicalFacilityItemBatchTransferGateway transfers = scope.Container
            .Resolve<IPhysicalFacilityItemBatchTransferGateway>();
        IGameMoneyAccount money = scope.Container.Resolve<IGameMoneyAccount>();
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        IGameEventBus gameEvents = scope.Container.Resolve<IGameEventBus>();
        IV20DailyEvaluationDiagnostic dailyEvaluation = scope.Container
            .Resolve<IV20DailyEvaluationDiagnostic>();
        IGridSystemProvider gridSystems =
            scope.Container.Resolve<IGridSystemProvider>();
        IFacilityBufferDestinationClaimAuthorityQuery inputClaims = scope.Container
            .Resolve<IFacilityBufferDestinationClaimAuthorityQuery>();
        IFactionRuntime factions = scope.Container.Resolve<IFactionRuntime>();
        ICharacterWorldQuery characterWorld = scope.Container
            .Resolve<ICharacterWorldQuery>();
        IFacilityCapabilityQuery facilities = scope.Container
            .Resolve<IFacilityCapabilityQuery>();
        IRoomLayoutCache roomLayouts = scope.Container
            .Resolve<IRoomLayoutCache>();
        IGridBuildingObjectFactory buildingFactory = scope.Container
            .Resolve<IGridBuildingObjectFactory>();
        IDurableFacilityEquipmentSlotQuery equipmentSlots = scope.Container
            .Resolve<IDurableFacilityEquipmentSlotQuery>();
        IDomainFailureLocalizer failureLocalizer = scope.Container
            .Resolve<IDomainFailureLocalizer>();
        IEnvironmentalFieldQuery environmentalField = scope.Container
            .Resolve<IEnvironmentalFieldQuery>();

        Grid readyGrid = null;
        Vector2Int readyDropoff = default;
        int readyWorkers = 0;
        float runtimeDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < runtimeDeadline)
        {
            if (ownerManagers.TryGetManager(
                    out OwnerRunManager refreshedOwnerManager)
                && refreshedOwnerManager != null)
            {
                ownerManager = refreshedOwnerManager;
            }
            readyWorkers = characterWorld.Characters.Count(actor =>
                actor != null
                && !actor.IsDead
                && actor.gameObject.activeInHierarchy
                && actor.Brain != null
                && actor.TryGetAbility(out AbilityWork _));
            bool gridReady = gridSystems.TryGetGrid(out readyGrid)
                && readyGrid != null;
            bool dropoffReady = dropZones.TryGetDeliveryDropoff(
                out readyDropoff);
            bool uiReady = EventSystem.current != null;
            bool campaignReady = campaign.CaptureFactions().factions.Count > 0;
            if (ownerManager?.CurrentOwnerActor != null
                && readyWorkers > 0
                && gridReady
                && dropoffReady
                && uiReady
                && campaignReady
                && environmentalField.IsInitialized)
            {
                break;
            }
            yield return null;
        }
        Require(ownerManager?.CurrentOwnerActor != null,
            "Production prepared-party commit did not publish a run owner.");
        Require(readyWorkers > 0,
            "Production prepared-party commit did not publish a live AI worker.");
        Require(readyGrid != null,
            "Production prepared-party commit did not publish the gameplay grid.");
        Require(dropZones.TryGetDeliveryDropoff(out readyDropoff),
            "Production prepared-party commit did not publish a delivery dropoff.");
        Require(EventSystem.current != null,
            "Production run did not publish the UI EventSystem.");
        Require(campaign.CaptureFactions().factions.Count > 0,
            "Production run start did not initialize faction campaign rows.");
        Require(environmentalField.IsInitialized,
            "Production environmental field did not initialize before save capture.");

        // The production offense surface is intentionally lazy. Open it only
        // after its run, campaign and input prerequisites are ready; waiting
        // for the panel before this command can never progress on a fresh run.
        OffenseWorldMapPanel worldMapPanel = panelService.ShowWorldMap();
        Require(worldMapPanel != null
                && worldMapPanel.gameObject.activeInHierarchy
                && UnityEngine.Object.FindObjectsByType<OffenseWorldMapPanel>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Any(value => ReferenceEquals(value, worldMapPanel)),
            "Production ShowWorldMap did not publish the strategic UI.");

        // This is the cleanup authority for a manually started verifier. Keep
        // it free of any verifier-authored facility mutation.
        baseline = saves.Capture();
        IReadOnlyList<BuildableObject> administration = facilities
            .FindOperational(FacilityCapabilityKind.Administration);
        string administrationSource = administration.Count > 0
            ? "prepared-run:" + administration[0].PersistentInstanceId.Value
            : string.Empty;
        if (administration.Count == 0)
        {
            BuildableObject fixture = PlaceAdministrationFixture(
                scope,
                readyGrid,
                roomLayouts,
                buildingFactory,
                out Vector2Int administrationAnchor);
            yield return null;
            administration = facilities.FindOperational(
                FacilityCapabilityKind.Administration);
            Require(administration.Any(value => ReferenceEquals(value, fixture)),
                "Authored administration fixture was not operational after production placement.");
            administrationSource = "authored-fixture:R07:"
                + fixture.PersistentInstanceId.Value + "@"
                + administrationAnchor;
        }
        Require(administration.Count > 0,
            "Prepared live scene needs an operational administration facility.");
        string sealSource = "prepared-run";
        if (!items.GetAllStacks().Any(value => value != null
                && value.Quantity > 0
                && string.Equals(
                    value.ItemId,
                    DurableToolItemRules.AdministrativeSeal,
                    StringComparison.Ordinal)))
        {
            Require(items.SpawnUniqueItemAt(
                    DurableToolItemRules.AdministrativeSeal,
                    readyDropoff,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out string sealStackId),
                "Could not seed the authored administrative-seal fixture.");
            sealSource = "authored-fixture:" + sealStackId;
        }

        // Internal restore checks must retain the optional authored fixture;
        // final cleanup deliberately restores the earlier mutation-free baseline.
        DungeonGameSaveData verificationBaseline = saves.Capture();

        WorldItemStackSnapshot[] sourceStacks = items.GetAllStacks()
            .Where(value => value != null && value.Quantity > 0)
            .ToArray();
        FactionContractDefinitionSO contract = catalog.Contracts
            .Where(value => value != null
                && FactionContractMaterialRules.HasMaterialRequirements(value))
            .Where(value => contracts.GetContracts(value.factionId)
                .Any(view => string.Equals(
                        view.ContractId,
                        value.StableId,
                        StringComparison.Ordinal)
                    && view.CanAccept))
            .Where(value => FactionContractMaterialRules
                .BuildMaterialRequirements(value)
                .Any(requirement => requirement.Value > 1))
            .Where(value => FactionContractMaterialRules
                .BuildMaterialRequirements(value)
                .All(requirement => sourceStacks
                    .Where(stack => string.Equals(
                        stack.ItemId,
                        requirement.Key,
                        StringComparison.Ordinal))
                    .Where(stack => stack.State is WorldItemStackState.Loose
                        or WorldItemStackState.Stored
                        or WorldItemStackState.FacilityOutputBuffer)
                    .Sum(stack => stack.Quantity) == 0))
            .OrderBy(value => value.StableId, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(contract != null,
            "Prepared live scene needs an eligible material contract with zero source stock.");
        Vector2Int? blockedPickup = readyGrid.GetCells()
            .Where(value => value != null)
            .Select(value => value.Position)
            .Where(position => new[]
            {
                position,
                position + Vector2Int.left,
                position + Vector2Int.right
            }.All(candidate => !readyGrid.IsValidGridPos(candidate)
                || !readyGrid.IsWalkable(candidate)))
            .Select(position => (Vector2Int?)position)
            .FirstOrDefault();
        Require(blockedPickup.HasValue,
            "Prepared live grid has no pickup cell with a genuinely blocked route.");
        report.Add("[PASS] FIXTURE_READY owner=1; "
            + $"workers={readyWorkers}; grid=1; dropoff={readyDropoff}; "
            + "eventSystem=1; worldMap=1; factionCampaign=1; "
            + $"administration={administrationSource}; "
            + $"administrativeSeal={sealSource}; "
            + $"contract={contract.StableId}; blockedPickup={blockedPickup.Value}; "
            + $"temporaryRun={(committedTemporaryRun ? 1 : 0)}; "
            + "autosave=isolated");

        string baselineCampaign = JsonUtility.ToJson(
            campaign.CaptureFactions());
        string baselinePhysical = JsonUtility.ToJson(items.Capture());
        DungeonGameSaveData malformedFactionSave = RoundTrip(
            verificationBaseline);
        FactionCampaignWorldSaveData malformedFaction =
            DungeonSaveSectionPayload.ReadOrNew<FactionCampaignWorldSaveData>(
                malformedFactionSave,
                FactionCampaignSaveSection.Id);
        Require(malformedFaction.factions.Count > 0,
            "Captured whole save has no faction campaign rows.");
        malformedFaction.factions.RemoveAt(malformedFaction.factions.Count - 1);
        DungeonSaveSectionPayload.Write(
            malformedFactionSave,
            FactionCampaignSaveSection.Id,
            FactionCampaignWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            malformedFaction);
        malformedFactionSave.manifest = DungeonSaveManifest.Capture(
            malformedFactionSave.sections);
        bool malformedAccepted = saves.TryRestore(
            malformedFactionSave,
            out DungeonGameRestoreReport malformedReport);
        Require(!malformedAccepted
            && !malformedReport.Success
            && malformedReport.Errors.Any(value => value.Contains(
                $"Section '{FactionCampaignSaveSection.Id}' failed preflight",
                StringComparison.Ordinal))
            && string.Equals(
                baselineCampaign,
                JsonUtility.ToJson(campaign.CaptureFactions()),
                StringComparison.Ordinal)
            && string.Equals(
                baselinePhysical,
                JsonUtility.ToJson(items.Capture()),
                StringComparison.Ordinal),
            "Malformed local faction payload bypassed preflight or mutated live state.");
        Restore(
            verificationBaseline,
            "baseline whole-registry preflight/restore");
        Require(string.Equals(
                baselineCampaign,
                JsonUtility.ToJson(campaign.CaptureFactions()),
                StringComparison.Ordinal)
            && string.Equals(
                baselinePhysical,
                JsonUtility.ToJson(items.Capture()),
                StringComparison.Ordinal),
            "Valid whole-registry faction restore drifted campaign or physical state.");
        report.Add("[PASS] FACTION_WHOLE_REGISTRY local-malformed=preflight-reject; "
            + "valid=preflight+staged-restore");
        Require(catalog.Contracts.Count == 24
            && catalog.Contracts.Count(value => string.IsNullOrEmpty(value.seasonalEventId)) == 18
            && catalog.Contracts.Count(value => value.seasonalEventId == "seasonal:winter-fuel-demand") == 6
            && catalog.Contracts.All(
                FactionContractMaterialRules.HasMaterialRequirements),
            "Authored faction-contract count/material routing drifted from 18 normal + 6 winter contracts.");
        report.Add("[PASS] LIVE_AUTHORITIES container+whole-save resolved");

        IReadOnlyDictionary<string, int> material =
            FactionContractMaterialRules.BuildMaterialRequirements(contract);
        FactionContractView proposal = contracts.GetContracts(contract.factionId)
            .Single(value => string.Equals(
                value.ContractId,
                contract.StableId,
                StringComparison.Ordinal));
        FactionDefinitionSnapshot factionDefinition = factions.Definitions
            .FirstOrDefault(value => value != null && string.Equals(
                value.StableId,
                contract.factionId,
                StringComparison.Ordinal));
        Require(factionDefinition != null,
            "The contract faction is absent from the strategic faction catalog.");
        Require(dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff),
            "A live material contract requires a delivery dropoff.");
        ClickExact("세력");
        yield return null;
        ClickContaining(factionDefinition.DisplayName);
        yield return null;
        string proposalText = ActiveWorldMapText();
        Require(proposalText.Contains(
                $"수락 후 {proposal.DeadlineDays}일",
                StringComparison.Ordinal)
            && proposalText.Contains("완료 조건:", StringComparison.Ordinal)
            && proposalText.Contains("보상:", StringComparison.Ordinal)
            && proposalText.Contains("실패 비용:", StringComparison.Ordinal)
            && proposalText.Contains("수락 판정:", StringComparison.Ordinal),
            "Preaccept UI omitted deadline, conditions, rewards, failure cost, or eligibility.");

        string campaignBeforeDecline = JsonUtility.ToJson(
            campaign.CaptureFactions());
        ClickExact("제안 닫기 · " + contract.DisplayName);
        yield return null;
        Require(string.Equals(
                campaignBeforeDecline,
                JsonUtility.ToJson(campaign.CaptureFactions()),
                StringComparison.Ordinal),
            "Closing a proposal mutated campaign state.");
        ClickExact("닫은 계약 제안 다시 보기");
        yield return null;
        Require(!equipmentSlots.CaptureAll().Any(value => value != null
                && value.SupplyReady
                && string.Equals(
                    value.PolicyId,
                    RunAdministrativeSealDurableEquipmentPolicySource.PolicyId,
                    StringComparison.Ordinal)),
            "Focused fresh-run acceptance requires an unprovisioned administrative seal slot.");
        string campaignBeforeSealProvision = JsonUtility.ToJson(
            campaign.CaptureFactions());
        string materialBeforeSealProvision = MaterialQuantitySignature(
            items,
            material);
        string sealDurabilityBeforeProvision =
            AdministrativeSealDurabilitySignature(items);
        int moneyBeforeSealProvision = money.Balance;
        ClickExact("계약 수락 · " + contract.DisplayName);
        yield return null;
        yield return null;
        DurableFacilityEquipmentSlotSnapshot sealSlot = equipmentSlots
            .CaptureAll()
            .Where(value => value != null
                && string.Equals(
                    value.PolicyId,
                    RunAdministrativeSealDurableEquipmentPolicySource.PolicyId,
                    StringComparison.Ordinal))
            .OrderByDescending(value => value.AssignmentSequence)
            .FirstOrDefault();
        FactionContractView pendingSealView = contracts
            .GetContracts(contract.factionId)
            .Single(value => string.Equals(
                value.ContractId,
                contract.StableId,
                StringComparison.Ordinal));
        string expectedSealPending = failureLocalizer.Localize(
            new DomainFailure(
                FailureCode.ServiceFeatureMissing,
                DurableToolItemRules.AdministrativeSeal));
        Require(campaign.TryGetFaction(
                contract.factionId,
                out FactionCampaignStateSaveData accepted)
            && string.IsNullOrEmpty(accepted.activeContractId)
            && !accepted.completedContractIds.Contains(
                contract.StableId,
                StringComparer.Ordinal)
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.None
            && string.Equals(
                campaignBeforeSealProvision,
                JsonUtility.ToJson(campaign.CaptureFactions()),
                StringComparison.Ordinal)
            && money.Balance == moneyBeforeSealProvision
            && pendingSealView.Items.All(value => value.Assigned == 0
                && value.Hauling == 0
                && value.Arrived == 0
                && value.Delivered == 0)
            && string.Equals(
                materialBeforeSealProvision,
                MaterialQuantitySignature(items, material),
                StringComparison.Ordinal)
            && string.Equals(
                sealDurabilityBeforeProvision,
                AdministrativeSealDurabilitySignature(items),
                StringComparison.Ordinal)
            && sealSlot != null
            && !sealSlot.SupplyReady
            && sealSlot.Requirements.Single().PendingQuantity == 1
            && items.GetAllStacks().Any(value => value != null
                && value.Quantity == 1
                && string.Equals(
                    value.ItemId,
                    DurableToolItemRules.AdministrativeSeal,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.DestinationId,
                    sealSlot.DestinationId,
                    StringComparison.Ordinal))
            && ActiveWorldMapText().Contains(
                expectedSealPending,
                StringComparison.Ordinal),
            "First UI acceptance did not remain pending with observable physical seal custody.");

        Time.timeScale = runTimeScale;
        float sealDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < sealDeadline)
        {
            sealSlot = equipmentSlots.CaptureAll()
                .Where(value => value != null
                    && string.Equals(
                        value.PolicyId,
                        RunAdministrativeSealDurableEquipmentPolicySource
                            .PolicyId,
                        StringComparison.Ordinal))
                .OrderByDescending(value => value.AssignmentSequence)
                .FirstOrDefault();
            if (sealSlot?.SupplyReady == true)
                break;
            yield return null;
        }
        Require(sealSlot?.SupplyReady == true,
            "First live UI acceptance did not provision the physical administrative seal.");
        WorldItemStackSnapshot sealBeforeUse = RequireAdministrativeSealStack(
            items,
            sealSlot);
        float sealDurabilityBeforeUse = DurableToolItemRules
            .ReadCurrentDurability(
                sealBeforeUse.ItemId,
                sealBeforeUse.Components);
        Require(string.Equals(
                sealDurabilityBeforeProvision,
                AdministrativeSealDurabilitySignature(items),
                StringComparison.Ordinal),
            "Administrative-seal provisioning changed durability before use.");
        ClickExact("계약 수락 · " + contract.DisplayName);
        yield return null;
        yield return null;
        Require(campaign.TryGetFaction(
                contract.factionId,
                out accepted)
            && string.Equals(
                accepted.activeContractId,
                contract.StableId,
                StringComparison.Ordinal),
            "World-map pointer acceptance did not reach the campaign command.");
        WorldItemStackSnapshot sealAfterUse = items.GetAllStacks()
            .SingleOrDefault(value => value != null
                && value.Quantity == 1
                && string.Equals(
                    value.ItemInstanceId,
                    sealBeforeUse.ItemInstanceId,
                    StringComparison.Ordinal));
        float sealDurabilityAfterUse = sealAfterUse == null
            ? -1f
            : DurableToolItemRules.ReadCurrentDurability(
                sealAfterUse.ItemId,
                sealAfterUse.Components);
        Require(sealAfterUse != null
                && string.Equals(
                    sealAfterUse.ItemId,
                    DurableToolItemRules.AdministrativeSeal,
                    StringComparison.Ordinal)
                && string.Equals(
                    sealAfterUse.DestinationId,
                    sealSlot.DestinationId,
                    StringComparison.Ordinal)
                && (sealAfterUse.Components
                        ?? Array.Empty<ItemInstanceComponentSaveData>())
                    .Count(value => value != null
                        && string.Equals(
                            value.componentTypeId,
                            ItemInstanceComponentIds.Durability,
                            StringComparison.Ordinal)) == 1
                && Mathf.Approximately(
                    sealDurabilityAfterUse,
                    sealDurabilityBeforeUse
                        - (float)RunAdministrativeSealDurableEquipmentRuntime
                            .WearPerResolution),
            "Accepted contract consumed the seal or applied noncanonical durability wear.");
        report.Add("[PASS] ADMINISTRATION_EQUIPMENT first-ui=pending/no-credit; "
            + "natural-haul=SupplyReady; second-ui=accept; sealQuantity=1; "
            + $"durability={sealDurabilityBeforeUse}->{sealDurabilityAfterUse}");
        int rapportBeforeDelivery = accepted.rapport;
        int grievanceBeforeDelivery = accepted.grievance;
        int obligationBeforeDelivery = accepted.obligationTokens;
        int moneyBeforeDelivery = money.Balance;
        string deliveryDestinationId = accepted.activeContractDestinationId;
        string operationId = FactionContractDeliveryOutbox.FormatOperationId(
            contract.StableId);
        report.Add("[PASS] UI_ACCEPT_DECLINE_REOPEN pointer path; declineMutation=0");

        float shortageDeadline = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < shortageDeadline)
            yield return null;
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.None
            && string.Equals(
                accepted.activeContractId,
                contract.StableId,
                StringComparison.Ordinal),
            "Zero-stock shortage did not remain blocked before physical commit.");
        FactionContractView shortageView = contracts
            .GetContracts(contract.factionId)
            .Single(value => string.Equals(
                value.ContractId,
                contract.StableId,
                StringComparison.Ordinal));
        Require(shortageView.Items.All(value => value.Assigned == 0
                && value.Hauling == 0
                && value.Arrived == 0
                && value.Delivered == 0)
            && accepted.rapport == rapportBeforeDelivery
            && accepted.grievance == grievanceBeforeDelivery
            && accepted.obligationTokens == obligationBeforeDelivery
            && money.Balance == moneyBeforeDelivery,
            "Zero-stock shortage moved contract material or published a reward.");
        DungeonGameSaveData shortageSave = RoundTrip(saves.Capture());
        string shortageMaterial = MaterialQuantitySignature(items, material);
        Restore(shortageSave, "shortage whole-save");
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.None,
            "Shortage whole-save restore changed delivery phase.");
        report.Add("[PASS] SHORTAGE_BLOCKED whole-save active/no-receipt");

        KeyValuePair<string, int> partialRequirement = material
            .Where(value => value.Value > 1)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .First();
        Require(items.SpawnItemAt(
                partialRequirement.Key,
                1,
                blockedPickup.Value,
                WorldItemStackState.Loose,
                string.Empty,
                out int blockedSpawned)
            && blockedSpawned == 1,
            "Could not seed the blocked-path material stack.");
        Require(items.TryRequestItemDelivery(
                partialRequirement.Key,
                1,
                dropoff,
                deliveryDestinationId,
                out int blockedRequested,
                out string blockedRequestFailure)
            && blockedRequested == 1,
            "Could not route the blocked-path material: "
            + blockedRequestFailure);
        string blockedMaterial = MaterialQuantitySignature(items, material);
        float blockedDeadline = Time.realtimeSinceStartup + 2f;
        while (Time.realtimeSinceStartup < blockedDeadline)
            yield return null;
        FactionContractView blockedView = contracts
            .GetContracts(contract.factionId)
            .Single(value => string.Equals(
                value.ContractId,
                contract.StableId,
                StringComparison.Ordinal));
        FactionContractItemProgressView blockedProgress = blockedView.Items
            .Single(value => string.Equals(
                value.ItemId,
                partialRequirement.Key,
                StringComparison.Ordinal));
        WorldItemStackSnapshot[] blockedContractStacks = items.GetAllStacks()
            .Where(value => value != null && value.Quantity > 0)
            .Where(value => string.Equals(
                value.ItemId,
                partialRequirement.Key,
                StringComparison.Ordinal))
            .Where(value => string.Equals(
                value.DestinationId,
                deliveryDestinationId,
                StringComparison.Ordinal))
            .ToArray();
        Require(blockedProgress.Assigned == 1
            && blockedProgress.Hauling == 0
            && blockedProgress.Arrived == 0
            && blockedProgress.Delivered == 0
            && blockedContractStacks.Sum(value => value.Quantity) == 1
            && blockedContractStacks.All(value => value.State is
                WorldItemStackState.Loose
                or WorldItemStackState.Stored
                or WorldItemStackState.FacilityOutputBuffer)
            && string.Equals(
                blockedMaterial,
                MaterialQuantitySignature(items, material),
                StringComparison.Ordinal)
            && campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.None
            && accepted.rapport == rapportBeforeDelivery
            && accepted.grievance == grievanceBeforeDelivery
            && accepted.obligationTokens == obligationBeforeDelivery
            && money.Balance == moneyBeforeDelivery,
            "Blocked haul path lost material, advanced delivery, or published a reward.");
        report.Add("[PASS] BLOCKED_PATH invariant material=exact; reward=unchanged; "
            + "observed=Assigned:1,Carried:0,InTransit:0,Arrived:0");

        Restore(shortageSave, "blocked-path rollback whole-save");
        Require(string.Equals(
                shortageMaterial,
                MaterialQuantitySignature(items, material),
                StringComparison.Ordinal)
            && campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.None,
            "Blocked-path rollback did not restore the accepted shortage state.");

        Require(items.SpawnItemAt(
                partialRequirement.Key,
                1,
                dropoff,
                WorldItemStackState.Loose,
                string.Empty,
                out int partialSpawned)
            && partialSpawned == 1,
            "Could not seed the partial-arrival material stack.");
        Require(items.TryRequestItemDelivery(
                partialRequirement.Key,
                1,
                dropoff,
                deliveryDestinationId,
                out int partialRequested,
                out string partialRequestFailure)
            && partialRequested == 1,
            "Could not route the partial-arrival material: "
            + partialRequestFailure);

        int observedAssigned = 0;
        int observedCarried = 0;
        int observedInTransit = 0;
        int observedArrived = 0;
        bool observedPartialArrival = false;
        float partialDeadline = Time.realtimeSinceStartup + 45f;
        while (Time.realtimeSinceStartup < partialDeadline)
        {
            FactionContractView current = contracts
                .GetContracts(contract.factionId)
                .Single(value => string.Equals(
                    value.ContractId,
                    contract.StableId,
                    StringComparison.Ordinal));
            FactionContractItemProgressView progress = current.Items
                .Single(value => string.Equals(
                    value.ItemId,
                    partialRequirement.Key,
                    StringComparison.Ordinal));
            WorldItemStackSnapshot[] progressStacks = items.GetAllStacks()
                .Where(value => value != null && value.Quantity > 0)
                .Where(value => string.Equals(
                    value.ItemId,
                    partialRequirement.Key,
                    StringComparison.Ordinal))
                .Where(value => string.Equals(
                    value.DestinationId,
                    deliveryDestinationId,
                    StringComparison.Ordinal))
                .ToArray();
            observedAssigned = Math.Max(
                observedAssigned,
                progressStacks
                    .Where(value => value.State is WorldItemStackState.Loose
                        or WorldItemStackState.Stored
                        or WorldItemStackState.FacilityOutputBuffer)
                    .Sum(value => value.Quantity));
            observedCarried = Math.Max(
                observedCarried,
                progressStacks
                    .Where(value => value.State == WorldItemStackState.Carried)
                    .Sum(value => value.Quantity));
            observedInTransit = Math.Max(
                observedInTransit,
                progressStacks
                    .Where(value => value.State == WorldItemStackState.InTransit)
                    .Sum(value => value.Quantity));
            observedArrived = Math.Max(
                observedArrived,
                progressStacks
                    .Where(value => value.State ==
                        WorldItemStackState.FacilityBuffer)
                    .Sum(value => value.Quantity));
            if (campaign.TryGetFaction(contract.factionId, out accepted)
                && accepted.deliveryCommitPhase ==
                    FactionContractDeliveryCommitPhase.None
                && progress.Arrived > 0
                && progress.Arrived < progress.Required)
            {
                observedPartialArrival = true;
                Time.timeScale = 0f;
                break;
            }
            yield return null;
        }
        Require(observedPartialArrival,
            "Natural haul did not physically arrive a strict partial quantity.");
        string partialMaterial = MaterialQuantitySignature(items, material);
        DungeonGameSaveData partialSave = RoundTrip(saves.Capture());
        Restore(partialSave, "partial-haul whole-save");
        FactionContractItemProgressView restoredPartial = contracts
            .GetContracts(contract.factionId)
            .Single(value => string.Equals(
                value.ContractId,
                contract.StableId,
                StringComparison.Ordinal))
            .Items.Single(value => string.Equals(
                value.ItemId,
                partialRequirement.Key,
                StringComparison.Ordinal));
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.None
            && restoredPartial.Arrived > 0
            && restoredPartial.Arrived < restoredPartial.Required,
            "Partial-arrival restore lost physical progress or fabricated a receipt.");

        DungeonGameSaveData expirySave = RoundTrip(partialSave);
        FactionCampaignWorldSaveData expiryCampaign =
            DungeonSaveSectionPayload.ReadOrNew<FactionCampaignWorldSaveData>(
                expirySave,
                FactionCampaignSaveSection.Id);
        FactionCampaignStateSaveData expiryOwner = expiryCampaign.factions
            .Single(value => string.Equals(
                value.factionId,
                contract.factionId,
                StringComparison.Ordinal));
        string expiringDestinationId = expiryOwner.activeContractDestinationId;
        int expiryDay = Math.Max(
            calendar.Day + 1,
            campaign.CaptureSociety().lastEvaluationAbsoluteDay + 1);
        expiryOwner.activeContractDeadlineAbsoluteDay = expiryDay - 1;
        DungeonSaveSectionPayload.Write(
            expirySave,
            FactionCampaignSaveSection.Id,
            FactionCampaignWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            expiryCampaign);
        expirySave.manifest = DungeonSaveManifest.Capture(expirySave.sections);
        Restore(expirySave, "partial-arrival expiry whole-save");
        int expiryHour = calendar.Hour;
        calendar.SetDateTime(expiryDay, expiryHour);
        gameEvents.Publish(new OperatingDayStartedEvent(expiryDay));
        string dailyFailure = dailyEvaluation.LastDailyEvaluationFailure
            .IsFailure
                ? dailyEvaluation.LastDailyEvaluationFailure.Code + ":"
                    + string.Join(
                        ",",
                        dailyEvaluation.LastDailyEvaluationFailure
                            .Parameters.ToArray())
                : "none";
        report.Add("[INFO] PARTIAL_EXPIRY_DAY calendar=" + calendar.Day
            + "; event=" + expiryDay
            + "; evaluated="
            + dailyEvaluation.LastDailyEvaluationAbsoluteDay
            + "; success="
            + dailyEvaluation.LastDailyEvaluationSucceeded
            + "; failure=" + dailyFailure);
        Require(dailyEvaluation.LastDailyEvaluationAbsoluteDay == expiryDay
                && dailyEvaluation.LastDailyEvaluationSucceeded,
            "Production day evaluation rejected the aligned expiry day: "
            + dailyFailure);
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.failedContractIds.Contains(
                contract.StableId,
                StringComparer.Ordinal)
            && accepted.deliveryTerminalCleanupPending,
            "Production day evaluation did not expire the partial-arrival contract.");

        Time.timeScale = runTimeScale;
        bool ownerRetired = false;
        float cleanupDeadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < cleanupDeadline)
        {
            if (campaign.TryGetFaction(contract.factionId, out accepted)
                && string.IsNullOrEmpty(accepted.activeContractId)
                && !accepted.activeContractInputOwnerActive
                && !accepted.deliveryTerminalCleanupPending
                && accepted.deliveryCommitPhase ==
                    FactionContractDeliveryCommitPhase.None
                && !inputClaims.TryGetAuthorityClaim(
                    expiringDestinationId,
                    dropoff,
                    out _))
            {
                ownerRetired = true;
                Time.timeScale = 0f;
                break;
            }
            yield return null;
        }
        Require(ownerRetired,
            "Expired partial-arrival contract did not retire its input owner.");
        Require(string.Equals(
                partialMaterial,
                MaterialQuantitySignature(items, material),
                StringComparison.Ordinal)
            && items.GetAllStacks()
                .Where(value => value != null && value.Quantity > 0)
                .Where(value => material.ContainsKey(value.ItemId))
                .All(value => !string.Equals(
                    value.DestinationId,
                    expiringDestinationId,
                    StringComparison.Ordinal))
            && items.GetAllStacks().Any(value => value != null
                && value.Quantity > 0
                && string.Equals(
                    value.ItemId,
                    partialRequirement.Key,
                    StringComparison.Ordinal)
                && value.State == WorldItemStackState.Loose
                && string.IsNullOrEmpty(value.DestinationId))
            && items.CaptureHaulDeliveryIntentsByDestination(
                expiringDestinationId).Count == 0
            && !transfers.TryGetPending(operationId, out _),
            "Expiry owner retirement lost material or left delivery custody behind.");
        report.Add("[PASS] PARTIAL_EXPIRY_RECOVERY production-day owner=retired; "
            + "physical=Loose; quantity=exact; observed="
            + $"Assigned:{observedAssigned},"
            + $"Carried:{observedCarried},"
            + $"InTransit:{observedInTransit},"
            + $"Arrived:{observedArrived}");

        Restore(partialSave, "post-expiry partial recovery whole-save");
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.activeContractInputOwnerActive
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.None,
            "Post-expiry branch restore did not recover the live partial contract.");
        FactionContractView resumedPartial = contracts
            .GetContracts(contract.factionId)
            .Single(value => string.Equals(
                value.ContractId,
                contract.StableId,
                StringComparison.Ordinal));
        foreach (KeyValuePair<string, int> requirement in material)
        {
            int arrived = resumedPartial.Items
                .Single(value => string.Equals(
                    value.ItemId,
                    requirement.Key,
                    StringComparison.Ordinal))
                .Arrived;
            int remaining = Math.Max(0, requirement.Value - arrived);
            if (remaining == 0)
                continue;
            Require(items.SpawnItemAt(
                    requirement.Key,
                    remaining,
                    dropoff,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                && spawned == remaining,
                "Could not seed remaining live source stock for "
                + requirement.Key);
        }

        Time.timeScale = runTimeScale;
        bool physicalCommitted = false;
        float physicalDeadline = Time.realtimeSinceStartup + 90f;
        while (Time.realtimeSinceStartup < physicalDeadline)
        {
            if (campaign.TryGetFaction(contract.factionId, out accepted)
                && accepted.deliveryCommitPhase ==
                    FactionContractDeliveryCommitPhase.PhysicalCommitted)
            {
                physicalCommitted = true;
                Time.timeScale = 0f;
                break;
            }
            yield return null;
        }
        Require(physicalCommitted,
            "Natural haul did not reach the physical Transfer receipt phase.");
        Require(transfers.TryGetPending(operationId, out _),
            "Campaign physical receipt has no live Transfer outbox.");
        DungeonGameSaveData pendingSave = RoundTrip(saves.Capture());
        string livePendingCampaign = JsonUtility.ToJson(
            campaign.CaptureFactions());
        string livePendingPhysical = JsonUtility.ToJson(items.Capture());
        DungeonGameSaveData stagedMismatchSave = RoundTrip(pendingSave);
        FactionCampaignWorldSaveData stagedMismatchCampaign =
            DungeonSaveSectionPayload.ReadOrNew<FactionCampaignWorldSaveData>(
                stagedMismatchSave,
                FactionCampaignSaveSection.Id);
        FactionCampaignStateSaveData stagedMismatchOwner =
            stagedMismatchCampaign.factions.Single(value => string.Equals(
                value.factionId,
                contract.factionId,
                StringComparison.Ordinal));
        stagedMismatchOwner.deliveryMassGrams += 1L;
        stagedMismatchOwner.deliveryCommitId =
            $"physical-batch-disposition:"
            + $"{(int)PhysicalItemDispositionKind.Transfer}:"
            + $"{stagedMismatchOwner.deliveryOperationId}:"
            + $"{stagedMismatchOwner.deliveryQuantity}:"
            + stagedMismatchOwner.deliveryMassGrams;
        DungeonSaveSectionPayload.Write(
            stagedMismatchSave,
            FactionCampaignSaveSection.Id,
            FactionCampaignWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            stagedMismatchCampaign);
        stagedMismatchSave.manifest = DungeonSaveManifest.Capture(
            stagedMismatchSave.sections);
        bool stagedMismatchAccepted = saves.TryRestore(
            stagedMismatchSave,
            out DungeonGameRestoreReport stagedMismatchReport);
        Require(!stagedMismatchAccepted
            && !stagedMismatchReport.Success
            && stagedMismatchReport.Errors.Any(value => value.Contains(
                $"Failed to stage section '{FactionCampaignSaveSection.Id}'",
                StringComparison.Ordinal))
            && stagedMismatchReport.Errors.Any(value => value.Contains(
                "no exact incoming physical Transfer receipt",
                StringComparison.Ordinal))
            && string.Equals(
                livePendingCampaign,
                JsonUtility.ToJson(campaign.CaptureFactions()),
                StringComparison.Ordinal)
            && string.Equals(
                livePendingPhysical,
                JsonUtility.ToJson(items.Capture()),
                StringComparison.Ordinal)
            && campaign.TryGetFaction(contract.factionId, out accepted)
            && transfers.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt livePendingReceipt)
            && FactionContractDeliveryOutbox.ReceiptMatchesSaved(
                accepted,
                livePendingReceipt),
            "Staged faction/physical join mismatch was accepted or mutated live state.");
        Restore(pendingSave, "exact pending whole-registry restore");
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && transfers.TryGetPending(
                operationId,
                out PhysicalItemBatchDispositionReceipt restoredPendingReceipt)
            && FactionContractDeliveryOutbox.ReceiptMatchesSaved(
                accepted,
                restoredPendingReceipt),
            "Exact pending whole-registry restore lost its campaign/physical join.");
        report.Add("[PASS] FACTION_STAGED_JOIN mismatch=reject/no-mutation; "
            + "exact=whole-registry-restore");

        DungeonGameSaveData overduePendingSave = RoundTrip(pendingSave);
        FactionCampaignWorldSaveData pendingCampaign =
            DungeonSaveSectionPayload.ReadOrNew<FactionCampaignWorldSaveData>(
                overduePendingSave,
                FactionCampaignSaveSection.Id);
        FactionCampaignStateSaveData pendingOwner = pendingCampaign.factions
            .Single(value => string.Equals(
                value.factionId,
                contract.factionId,
                StringComparison.Ordinal));
        pendingOwner.activeContractDeadlineAbsoluteDay = calendar.Day - 1;
        DungeonSaveSectionPayload.Write(
            overduePendingSave,
            FactionCampaignSaveSection.Id,
            FactionCampaignWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            pendingCampaign);
        overduePendingSave.manifest = DungeonSaveManifest.Capture(
            overduePendingSave.sections);
        Restore(overduePendingSave, "overdue physical-receipt whole-save");
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.PhysicalCommitted
            && accepted.activeContractDeadlineAbsoluteDay < calendar.Day,
            "Overdue committed receipt did not restore exactly.");
        report.Add("[PASS] PENDING_OVERDUE_RESTORE physical receipt preserved past deadline");

        FactionCampaignWorldSaveData rewardBaselineCampaign =
            campaign.CaptureFactions();
        FactionCampaignStateSaveData rewardBaseline =
            rewardBaselineCampaign.factions.Single(value => string.Equals(
                value.factionId,
                contract.factionId,
                StringComparison.Ordinal));
        int rapportBeforeReward = rewardBaseline.rapport;
        int grievanceBeforeReward = rewardBaseline.grievance;
        int obligationBeforeReward = rewardBaseline.obligationTokens;
        int moneyBeforeReward = money.Balance;
        string rewardFactionBefore = JsonUtility.ToJson(
            rewardBaselineCampaign);

        Time.timeScale = runTimeScale;
        bool rewardPublished = false;
        float rewardDeadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < rewardDeadline)
        {
            if (campaign.TryGetFaction(contract.factionId, out accepted)
                && accepted.deliveryCommitPhase ==
                    FactionContractDeliveryCommitPhase.RewardPublished)
            {
                rewardPublished = true;
                Time.timeScale = 0f;
                break;
            }
            yield return null;
        }
        Require(rewardPublished,
            "Committed receipt did not resume campaign reward after deadline.");
        int expectedRapport = Math.Clamp(
            rapportBeforeReward + EffectAmount(
                contract,
                V20ContentEffectKind.FactionRapport),
            -100,
            100);
        int expectedGrievance = Math.Clamp(
            grievanceBeforeReward + EffectAmount(
                contract,
                V20ContentEffectKind.FactionGrievance),
            0,
            100);
        int expectedObligation = Math.Clamp(
            obligationBeforeReward + EffectAmount(
                contract,
                V20ContentEffectKind.FactionObligation),
            0,
            5);
        int expectedMoney = moneyBeforeReward + EffectAmount(
            contract,
            V20ContentEffectKind.Money);
        Require(accepted.rapport == expectedRapport
            && accepted.grievance == expectedGrievance
            && accepted.obligationTokens == expectedObligation
            && money.Balance == expectedMoney,
            "Campaign reward did not apply the authored faction effects once. "
            + $"rapport={rapportBeforeReward}->{accepted.rapport}"
            + $" (expected {expectedRapport}); "
            + $"grievance={grievanceBeforeReward}->{accepted.grievance}"
            + $" (expected {expectedGrievance}); "
            + $"obligation={obligationBeforeReward}->{accepted.obligationTokens}"
            + $" (expected {expectedObligation}); "
            + $"money={moneyBeforeReward}->{money.Balance}"
            + $" (expected {expectedMoney}); "
            + $"factionBefore={rewardFactionBefore}; "
            + "factionAfter=" + JsonUtility.ToJson(
                campaign.CaptureFactions()));
        DungeonGameSaveData rewardSave = RoundTrip(saves.Capture());
        Restore(rewardSave, "reward-published whole-save");
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.RewardPublished
            && accepted.rapport == expectedRapport
            && accepted.grievance == expectedGrievance
            && accepted.obligationTokens == expectedObligation
            && money.Balance == expectedMoney,
            "Reward-published restore duplicated or lost campaign effects.");
        report.Add("[PASS] REWARD_WHOLE_SAVE authored campaign effects exactly once");

        Time.timeScale = runTimeScale;
        bool acknowledged = false;
        float acknowledgementDeadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < acknowledgementDeadline)
        {
            if (campaign.TryGetFaction(contract.factionId, out accepted)
                && accepted.deliveryCommitPhase ==
                    FactionContractDeliveryCommitPhase.None
                && accepted.completedContractIds.Contains(
                    contract.StableId,
                    StringComparer.Ordinal)
                && !transfers.TryGetPending(operationId, out _))
            {
                acknowledged = true;
                break;
            }
            yield return null;
        }
        Require(acknowledged,
            "Reward outbox did not ACK and clear its physical receipt.");
        float duplicateWindow = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < duplicateWindow)
            yield return null;
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.rapport == expectedRapport
            && accepted.grievance == expectedGrievance
            && accepted.obligationTokens == expectedObligation
            && money.Balance == expectedMoney
            && !transfers.TryGetPending(operationId, out _),
            "Post-ACK replay duplicated a reward or recreated physical pending state.");
        DungeonGameSaveData finalSave = RoundTrip(saves.Capture());
        Restore(finalSave, "terminal whole-save");
        Require(campaign.TryGetFaction(contract.factionId, out accepted)
            && accepted.deliveryCommitPhase ==
                FactionContractDeliveryCommitPhase.None
            && accepted.completedContractIds.Contains(
                contract.StableId,
                StringComparer.Ordinal)
            && accepted.rapport == expectedRapport
            && accepted.grievance == expectedGrievance
            && accepted.obligationTokens == expectedObligation
            && money.Balance == expectedMoney,
            "Terminal whole-save restore drifted reward/ACK state.");
        report.Add("[PASS] ACK_EXACT_ONCE pending=0; rewardDuplicate=0; terminalRestore=exact");
        verificationCompleted = true;
    }

    internal static BuildableObject PlaceAdministrationFixture(
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        IRoomLayoutCache roomLayouts,
        IGridBuildingObjectFactory buildingFactory,
        out Vector2Int anchor)
    {
        const string definitionPath =
            "Assets/Resources/SO/Building/Modular/R07_영주집무책상.asset";
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            definitionPath);
        Require(definition != null,
            "Authored R07 administration definition is unavailable.");

        RoomLayout layout = roomLayouts.GetLayout(grid);
        Vector2Int? selected = layout.Rooms
            .Where(room => room != null && room.IsUsable)
            .OrderBy(room => room.Bounds.yMin)
            .ThenBy(room => room.Bounds.xMin)
            .SelectMany(room => room.Cells
                .OrderBy(position => position.y)
                .ThenBy(position => position.x)
                .Where(position => definition.GetGridPosList(position)
                    .All(footprint => room.ContainsCell(footprint)
                        && grid.GetGridCell(footprint) is GridCell cell
                        && cell.CanBuildInArea(definition)
                        && cell.CanOccupy(definition.Placement.Layer))))
            .Select(position => (Vector2Int?)position)
            .FirstOrDefault();
        Require(selected.HasValue,
            "No usable room has a free authored R07 production footprint.");
        anchor = selected.Value;

        BuildableObject fixture = buildingFactory.Create(
            grid,
            definition,
            anchor);
        Require(fixture != null,
            "Production building factory did not create the authored R07 fixture.");
        try
        {
            foreach (MonoBehaviour component in fixture
                         .GetComponentsInChildren<MonoBehaviour>(true))
            {
                scope.Container.Inject(component);
            }
            fixture.SetGrid(grid);
            fixture.Initialization(definition, anchor);
            Require(grid.RegisterOccupant(
                    fixture,
                    definition.Placement.Layer,
                    definition.GetGridPosList(anchor),
                    definition.Placement.IsMovement),
                "Gameplay grid rejected the authored R07 fixture footprint.");
            return fixture;
        }
        catch
        {
            fixture.DestroySelf();
            throw;
        }
    }

    private DungeonGameSaveData RoundTrip(DungeonGameSaveData save) =>
        saves.FromJson(saves.ToJson(save));

    private void Restore(DungeonGameSaveData save, string label)
    {
        Require(saves.TryRestore(
                RoundTrip(save),
                out DungeonGameRestoreReport restoreReport)
            && restoreReport.Success,
            label + " failed: " + string.Join(" | ", restoreReport.Errors));
    }

    private static int EffectAmount(
        FactionContractDefinitionSO contract,
        V20ContentEffectKind kind) => Mathf.RoundToInt(
        (contract.successEffects ?? new List<V20ContentEffect>())
        .Where(value => value != null && value.kind == kind)
        .Sum(value => value.amount));

    private static string MaterialQuantitySignature(
        IWorldItemStackRuntime items,
        IReadOnlyDictionary<string, int> material) => string.Join(
        "|",
        material.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(itemId => itemId + ":" + items.GetAllStacks()
                .Where(value => value != null && value.Quantity > 0)
                .Where(value => string.Equals(
                    value.ItemId,
                    itemId,
                    StringComparison.Ordinal))
                .Sum(value => value.Quantity)));

    private static string AdministrativeSealDurabilitySignature(
        IWorldItemStackRuntime items) => string.Join(
        "|",
        items.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && string.Equals(
                    value.ItemId,
                    DurableToolItemRules.AdministrativeSeal,
                    StringComparison.Ordinal))
            .OrderBy(value => value.ItemInstanceId, StringComparer.Ordinal)
            .ThenBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => value.ItemInstanceId + ":"
                + value.Quantity + ":"
                + DurableToolItemRules.ReadCurrentDurability(
                        value.ItemId,
                        value.Components)
                    .ToString(
                        "R",
                        System.Globalization.CultureInfo.InvariantCulture)));

    private static WorldItemStackSnapshot RequireAdministrativeSealStack(
        IWorldItemStackRuntime items,
        DurableFacilityEquipmentSlotSnapshot slot)
    {
        WorldItemStackSnapshot[] stacks = items.GetAllStacks()
            .Where(value => value != null
                && value.Quantity > 0
                && string.Equals(
                    value.ItemId,
                    DurableToolItemRules.AdministrativeSeal,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.DestinationId,
                    slot.DestinationId,
                    StringComparison.Ordinal))
            .ToArray();
        Require(stacks.Length == 1
                && stacks[0].Quantity == 1
                && !string.IsNullOrWhiteSpace(stacks[0].ItemInstanceId)
                && (stacks[0].Components
                        ?? Array.Empty<ItemInstanceComponentSaveData>())
                    .Count(value => value != null
                        && string.Equals(
                            value.componentTypeId,
                            ItemInstanceComponentIds.Durability,
                            StringComparison.Ordinal)) == 1,
            "SupplyReady administrative slot lacks one exact physical seal instance.");
        return stacks[0];
    }

    private static void ClickExact(string label)
    {
        Button button = ActiveWorldMapButtons().FirstOrDefault(value =>
            string.Equals(ButtonLabel(value), label, StringComparison.Ordinal));
        Require(ExecutePointerClick(button),
            "Live UI button is unavailable: " + label);
    }

    private static void ClickContaining(string label)
    {
        Button button = ActiveWorldMapButtons().FirstOrDefault(value =>
            ButtonLabel(value).Contains(label, StringComparison.Ordinal));
        Require(ExecutePointerClick(button),
            "Live UI button is unavailable: " + label);
    }

    private static IReadOnlyList<Button> ActiveWorldMapButtons() =>
        UnityEngine.Object.FindObjectsByType<OffenseWorldMapPanel>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(value => value != null && value.gameObject.activeInHierarchy)
            .SelectMany(value => value.GetComponentsInChildren<Button>(false))
            .Where(value => value != null
                && value.gameObject.activeInHierarchy
                && value.interactable)
            .ToArray();

    private static string ActiveWorldMapText() => string.Join(
        "\n",
        UnityEngine.Object.FindObjectsByType<OffenseWorldMapPanel>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(value => value != null && value.gameObject.activeInHierarchy)
            .SelectMany(value => value.GetComponentsInChildren<TMP_Text>(false))
            .Where(value => value != null && value.gameObject.activeInHierarchy)
            .Select(value => value.text));

    private static string ButtonLabel(Button button) => button == null
        ? string.Empty
        : button.GetComponentInChildren<TMP_Text>(true)?.text ?? string.Empty;

    private static bool ExecutePointerClick(Button button)
    {
        EventSystem eventSystem = EventSystem.current;
        if (button == null || eventSystem == null)
            return false;
        RectTransform rect = button.transform as RectTransform;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            null,
            rect != null
                ? rect.TransformPoint(rect.rect.center)
                : button.transform.position);
        PointerEventData pointer = new(eventSystem)
        {
            button = PointerEventData.InputButton.Left,
            position = screenPoint,
            pointerPress = button.gameObject,
            pointerEnter = button.gameObject
        };
        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerEnterHandler);
        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(
            button.gameObject,
            pointer,
            ExecuteEvents.pointerClickHandler);
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

public sealed class V20RetirementChoiceScheduleLivePlayModeRunner : MonoBehaviour
{
    private const float RuntimeReadyTimeoutSeconds = 45f;
    private const string RetirementEventId = "life-event:retirement-request";
    private const string MentorAcademyPath =
        "Assets/Resources/SO/Building/ResearchOverhaul/RF96_멘토_학원.asset";
    private readonly List<string> report = new();
    private DungeonGameSaveData baseline;
    private IDungeonGameSaveService saves;
    private DungeonAutosaveService autosave;
    private IGameSpeedController gameSpeed;
    private float originalTimeScale;
    private bool originalGamePause;
    private bool autosaveIsolated;
    private bool gameClockCaptured;
    private bool verificationCompleted;

    public bool ExitPlayModeOnCompletion { get; set; }

    private IEnumerator Start()
    {
        if (File.Exists(V20CampaignDebugScenarios.Wim044RequestPath))
            File.Delete(V20CampaignDebugScenarios.Wim044RequestPath);
        originalTimeScale = Time.timeScale;
        report.Add("WIM-044 live retirement choice schedule verification");
        report.Add($"utc={DateTime.UtcNow:O}");

        Exception verificationFailure = null;
        IEnumerator verification = VerifyLiveFlow();
        while (true)
        {
            object current = null;
            bool moved;
            try
            {
                moved = verification.MoveNext();
                if (moved)
                    current = verification.Current;
            }
            catch (Exception exception)
            {
                verificationFailure = exception;
                break;
            }
            if (!moved)
                break;
            yield return current;
        }
        (verification as IDisposable)?.Dispose();

        if (baseline != null && saves != null)
        {
            try
            {
                Restore(baseline, "baseline cleanup whole-save");
                report.Add("[PASS] CLEANUP baseline whole-save restored");
            }
            catch (Exception exception)
            {
                verificationFailure ??= exception;
                report.Add("[FAIL] CLEANUP " + exception.Message);
            }
        }
        if (gameClockCaptured)
        {
            try
            {
                gameSpeed.SetPaused(originalGamePause);
                Time.timeScale = originalTimeScale;
                report.Add("[PASS] CLEANUP clock restored; paused="
                    + originalGamePause + "; timeScale=" + originalTimeScale);
            }
            catch (Exception exception)
            {
                verificationFailure ??= exception;
                report.Add("[FAIL] CLEANUP clock restore: "
                    + exception.Message);
            }
        }
        else
        {
            Time.timeScale = originalTimeScale;
        }
        if (autosaveIsolated && !ExitPlayModeOnCompletion)
        {
            try
            {
                autosave.Start();
                report.Add("[PASS] CLEANUP autosave subscription restored");
            }
            catch (Exception exception)
            {
                verificationFailure ??= exception;
                report.Add("[FAIL] CLEANUP autosave restart: "
                    + exception.Message);
            }
        }
        else if (autosaveIsolated)
        {
            report.Add("[PASS] CLEANUP autosave remained isolated through PlayMode exit");
        }

        bool passed = verificationCompleted && verificationFailure == null;
        if (verificationFailure != null)
        {
            report.Add("[FAIL] " + verificationFailure);
        }
        report.Insert(0, passed
            ? "WIM044_RETIREMENT_CHOICE_SCHEDULE_LIVE=PASS"
            : "WIM044_RETIREMENT_CHOICE_SCHEDULE_LIVE=FROZEN");
        report.Add("failures=" + (passed ? 0 : 1));
        Directory.CreateDirectory(Path.GetDirectoryName(
            V20CampaignDebugScenarios.Wim044ReportPath));
        File.WriteAllLines(
            V20CampaignDebugScenarios.Wim044ReportPath,
            report);
        if (passed)
            Debug.Log(string.Join("\n", report));
        else
            Debug.LogError(string.Join("\n", report));

        if (ExitPlayModeOnCompletion)
            EditorApplication.ExitPlaymode();
        Destroy(gameObject);
    }

    private IEnumerator VerifyLiveFlow()
    {
        DungeonRuntimeLifetimeScope scope = null;
        float scopeDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < scopeDeadline)
        {
            scope = Resources.FindObjectsOfTypeAll<DungeonRuntimeLifetimeScope>()
                .FirstOrDefault(value => value != null
                    && value.gameObject.scene.IsValid()
                    && value.Container != null);
            if (scope != null)
                break;
            yield return null;
        }
        Require(scope != null, "Live gameplay DI scope is unavailable.");

        gameSpeed = scope.Container.Resolve<IGameSpeedController>();
        originalGamePause = gameSpeed.IsPaused;
        gameClockCaptured = true;
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        autosave = scope.Container.Resolve<IDungeonSaveCommandService>()
            as DungeonAutosaveService;
        Require(autosave != null,
            "Live retirement verification requires the production autosave service.");
        autosave.Dispose();
        autosaveIsolated = true;

        IOwnerRunManagerProvider ownerManagers = scope.Container
            .Resolve<IOwnerRunManagerProvider>();
        Require(ownerManagers.TryGetManager(out OwnerRunManager ownerManager)
                && ownerManager != null,
            "Gameplay scene has no production owner-run manager.");
        float ownerDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (ownerManager.CurrentOwnerActor == null
               && (ownerManager.OwnerCandidates == null
                   || ownerManager.OwnerCandidates.Count == 0)
               && Time.realtimeSinceStartup < ownerDeadline)
        {
            if (ownerManagers.TryGetManager(
                    out OwnerRunManager refreshedOwnerManager)
                && refreshedOwnerManager != null)
            {
                ownerManager = refreshedOwnerManager;
            }
            yield return null;
        }
        if (ownerManager.CurrentOwnerActor == null)
        {
            Require(ownerManager.OwnerCandidates?.Count > 0,
                "Production owner candidates were not ready before fixture bootstrap.");
            ExitPlayModeOnCompletion = true;
            report.Add("[INFO] FIXTURE_BOOTSTRAP "
                + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
        }

        gameSpeed.SetPaused(false);
        Require(!gameSpeed.IsPaused && Time.timeScale > 0f,
            "Production clock did not resume after prepared-party bootstrap.");

        ICharacterWorldQuery world = scope.Container
            .Resolve<ICharacterWorldQuery>();
        ICharacterLifeQuery life = scope.Container
            .Resolve<ICharacterLifeQuery>();
        ICharacterLifeDefinitionCatalog lifeDefinitions = scope.Container
            .Resolve<ICharacterLifeDefinitionCatalog>();
        ICareerService careers = scope.Container.Resolve<ICareerService>();
        ICareerPersistence careerPersistence = scope.Container
            .Resolve<ICareerPersistence>();
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        IGameEventBus gameEvents = scope.Container.Resolve<IGameEventBus>();
        IEventAlertChoiceActionDispatcher dispatcher = scope.Container
            .Resolve<IEventAlertChoiceActionDispatcher>();
        DungeonRuntimeAggregateRootStore rootStore = scope.Container
            .Resolve<DungeonRuntimeAggregateRootStore>();
        ICommittedRunResultQuery committedRunResults = scope.Container
            .Resolve<ICommittedRunResultQuery>();
        V20StoryContentCatalog story = scope.Container
            .Resolve<V20StoryContentCatalog>();
        IFacilityCapabilityQuery facilities = scope.Container
            .Resolve<IFacilityCapabilityQuery>();
        IGridSystemProvider gridSystems = scope.Container
            .Resolve<IGridSystemProvider>();
        IRoomLayoutCache roomLayouts = scope.Container
            .Resolve<IRoomLayoutCache>();
        IGridBuildingObjectFactory buildingFactory = scope.Container
            .Resolve<IGridBuildingObjectFactory>();
        IWorkPolicyRegistry workPolicy = scope.Container
            .Resolve<IWorkPolicyRegistry>();
        ICharacterBodyHealthCommand bodyHealth = scope.Container
            .Resolve<ICharacterBodyHealthCommand>();
        IKinshipQuery kinship = scope.Container.Resolve<IKinshipQuery>();
        IEnvironmentalFieldQuery environmentalField = scope.Container
            .Resolve<IEnvironmentalFieldQuery>();

        Grid grid = null;
        EventAlertRuntime alerts = null;
        float readyDeadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        while (Time.realtimeSinceStartup < readyDeadline)
        {
            if (ownerManagers.TryGetManager(
                    out OwnerRunManager refreshedOwnerManager)
                && refreshedOwnerManager != null)
            {
                ownerManager = refreshedOwnerManager;
            }
            gridSystems.TryGetGrid(out grid);
            alerts = UnityEngine.Object.FindObjectsByType<EventAlertRuntime>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault();
            if (ownerManager?.CurrentOwnerActor != null
                && world.Characters.Count(value => value != null
                    && !value.IsDead
                    && value.gameObject.activeInHierarchy) >= 3
                && grid != null
                && alerts != null
                && EventSystem.current != null
                && environmentalField.IsInitialized)
            {
                break;
            }
            yield return null;
        }
        Require(ownerManager?.CurrentOwnerActor != null,
            "Prepared run did not publish an owner.");
        Require(grid != null, "Prepared run did not publish a gameplay grid.");
        Require(alerts != null && EventSystem.current != null,
            "Prepared run did not publish the production alert UI.");
        Require(environmentalField.IsInitialized,
            "Production environmental field did not initialize before save capture.");

        CharacterActor[] candidates = world.Characters
            .Where(value => value != null
                && !value.IsDead
                && value.gameObject.activeInHierarchy
                && !ReferenceEquals(value, ownerManager.CurrentOwnerActor))
            .Where(value => !careers.TryGet(
                    CharacterPersistentIdentity.Require(value),
                    out CharacterCareerSnapshot career)
                || !career.Retired)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .ToArray();
        Require(candidates.Length >= 2,
            "Retirement fixture needs a non-owner target and mentorship peer.");
        CharacterId targetId = CharacterPersistentIdentity.Require(candidates[0]);
        CharacterId studentId = CharacterPersistentIdentity.Require(candidates[1]);

        int producerCooldownDelayDays =
            VerifyAuthoredRetirementAndProducer(story, targetId);
        report.Add("[PASS] TYPED_PRODUCER weighted-selection=sole-eligible; "
            + "ineligible=rejected; constructed-category-cooldown-delay-days="
            + producerCooldownDelayDays);
        baseline = saves.Capture();

        BuildableObject academy = facilities
            .FindOperational(ResearchFacilityCommandKind.MentorAcademy)
            .FirstOrDefault();
        string academySource = academy != null
            ? "prepared-run"
            : "authored-fixture";
        if (academy == null)
        {
            academy = PlaceMentorAcademyFixture(
                scope,
                grid,
                roomLayouts,
                buildingFactory);
            yield return null;
        }
        Require(academy != null
                && !academy.IsBuildingDestroyed
                && academy.BuildingData?.ResearchFacilityCommand ==
                    ResearchFacilityCommandKind.MentorAcademy,
            "Production MentorAcademy prerequisite is unavailable.");
        BuildingInstanceId academyId = academy.PersistentInstanceId;
        Require(academyId.IsValid && dispatcher != null,
            "Production MentorAcademy or choice dispatcher is invalid.");
        DungeonGameSaveData verificationBaseline = saves.Capture();
        int choiceDay = Math.Max(1, calendar.Day);
        int choiceHour = calendar.Hour;
        VerifyMalformedScheduleRejected(
            verificationBaseline,
            targetId,
            careerPersistence,
            rootStore);
        report.Add("[PASS] SAVE_PREFLIGHT v3/malformed schedule rejected; live=unchanged");

        DungeonGameSaveData deadBeforeChoiceScenario = PrepareScenario(
            verificationBaseline,
            targetId,
            studentId,
            academyId,
            choiceDay,
            "dead-before-choice",
            lifeDefinitions,
            story,
            out string deadBeforeChoiceInstanceId);
        Restore(deadBeforeChoiceScenario, "dead-before-choice fixture restore");
        calendar.SetDateTime(choiceDay, choiceHour);
        CharacterActor deadBeforeChoiceTarget = RequireActor(world, targetId);
        EventAlertRecord deadBeforeChoiceAlert = PublishAndRequireAlert(
            gameEvents,
            alerts,
            choiceDay,
            deadBeforeChoiceInstanceId,
            deadBeforeChoiceTarget.Identity.DisplayName);
        alerts.Open(deadBeforeChoiceAlert);
        bodyHealth.Kill(
            deadBeforeChoiceTarget,
            CharacterDeathCauseCode.Unknown,
            "qa:wim044:death-before-choice");
        Require(kinship.TryGetTombstone(targetId, out _)
                && !alerts.ExecuteChoice(1)
                && careers.TryGet(
                    targetId,
                    out CharacterCareerSnapshot deadBeforeChoiceCareer)
                && !deadBeforeChoiceCareer.Retired
                && deadBeforeChoiceCareer.RetirementScheduleStatus ==
                    RetirementScheduleStatus.None
                && committedRunResults.CaptureCommittedRunResult()
                    .CommittedChoices.Count == 0
                && CountRetiredHistory(
                    careerPersistence.Capture(),
                    targetId) == 0,
            "A tombstoned elder accepted retirement after the actual death path.");
        report.Add("[PASS] DEAD_BEFORE_CHOICE actual-death/tombstone; choice=rejected; schedule=none");

        DungeonGameSaveData immediateScenario = PrepareScenario(
            verificationBaseline,
            targetId,
            studentId,
            academyId,
            choiceDay,
            "immediate",
            lifeDefinitions,
            story,
            out string immediateInstanceId);
        Restore(immediateScenario, "immediate-choice fixture restore");
        calendar.SetDateTime(choiceDay, choiceHour);
        CharacterActor target = RequireActor(world, targetId);
        float moodBefore = target.Mood.Value;
        EventAlertRecord immediateAlert = PublishAndRequireAlert(
            gameEvents,
            alerts,
            choiceDay,
            immediateInstanceId,
            target.Identity.DisplayName);
        Require(immediateAlert.Choices.Count == 2
                && immediateAlert.Choices[0].Description.Contains(
                    "선택 당일 은퇴",
                    StringComparison.Ordinal)
                && immediateAlert.Choices[1].Description.Contains(
                    $"선택 후 {GameCalendarRules.DaysPerSeason}일",
                    StringComparison.Ordinal),
            "Production retirement UI did not expose both authored deadlines.");
        alerts.Open(immediateAlert);
        string immediateOperationId = immediateAlert.Choices[0].ActionId;
        Require(alerts.ExecuteChoice(0),
            "Production alert UI rejected the immediate retirement choice.");
        Require(careers.TryGet(targetId, out CharacterCareerSnapshot immediate)
                && immediate.Retired
                && immediate.RetirementScheduleStatus ==
                    RetirementScheduleStatus.Completed
                && immediate.RetirementDecisionAbsoluteDay == choiceDay
                && immediate.RetirementDueAbsoluteDay == choiceDay
                && immediate.RetirementTerminalAbsoluteDay == choiceDay,
            "Immediate retirement did not complete for the selected actor/day.");
        CommittedRunResultSnapshot immediateHistory =
            committedRunResults.CaptureCommittedRunResult();
        Require(immediateHistory.CommittedChoices.Count == 1
                && immediateHistory.CommittedChoices[0].Kind ==
                    CommittedRunChoiceKind.SocietyEventChoice
                && string.Equals(
                    immediateHistory.CommittedChoices[0].InstanceId,
                    immediateInstanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    immediateHistory.CommittedChoices[0].OperationId,
                    immediateOperationId,
                    StringComparison.Ordinal),
            "Successful production choice did not publish one structured committed-history record.");
        target = RequireActor(world, targetId);
        Require(target.Mood.Factors.Any(value =>
                string.Equals(
                    value.Id,
                    $"content:{RetirementEventId}:{RetirementEventId}",
                    StringComparison.Ordinal)
                && Mathf.Approximately(value.Value, 6f))
            && target.Mood.Value >= Mathf.Min(100f, moodBefore + 6f) - 0.01f,
            "Immediate retirement did not preserve the authored Mood +6 effect.");
        report.Add("[PASS] IMMEDIATE actual-ui/dispatcher; sameActor="
            + targetId.Value + "; decision=due=terminal=" + choiceDay
            + "; mood=+6");

        DungeonGameSaveData scheduledScenario = PrepareScenario(
            verificationBaseline,
            targetId,
            studentId,
            academyId,
            choiceDay,
            "scheduled",
            lifeDefinitions,
            story,
            out string scheduledInstanceId);
        Restore(scheduledScenario, "scheduled-choice fixture restore");
        calendar.SetDateTime(choiceDay, choiceHour);
        target = RequireActor(world, targetId);
        int revisionBeforeChoice = rootStore.PublishedRestoreRevision;
        EventAlertRecord scheduledAlert = PublishAndRequireAlert(
            gameEvents,
            alerts,
            choiceDay,
            scheduledInstanceId,
            target.Identity.DisplayName);
        alerts.Open(scheduledAlert);
        string scheduledOperationId = scheduledAlert.Choices[1].ActionId;
        Require(target.BeginExpedition()
                && target.CurrentLifecycleState ==
                    CharacterLifecycleState.OnExpedition,
            "Production away lifecycle could not retain the selected participant.");
        Require(alerts.ExecuteChoice(1),
            "Production alert UI rejected the away participant's one-season retirement choice.");
        target.EndExpedition(alive: true);
        Require(target.CurrentLifecycleState == CharacterLifecycleState.Active,
            "Retirement participant did not return from the focused away fixture.");
        int expectedDueDay = checked(
            choiceDay + GameCalendarRules.DaysPerSeason);
        Require(careers.TryGet(targetId, out CharacterCareerSnapshot pending)
                && !pending.Retired
                && pending.RetirementScheduleStatus ==
                    RetirementScheduleStatus.Pending
                && string.Equals(
                    pending.RetirementEventId,
                    RetirementEventId,
                    StringComparison.Ordinal)
                && string.Equals(
                    pending.RetirementChoiceId,
                    "second",
                    StringComparison.Ordinal)
                && pending.RetirementDecisionAbsoluteDay == choiceDay
                && pending.RetirementDueAbsoluteDay == expectedDueDay
                && pending.RetirementTerminalAbsoluteDay == 0,
            "Scheduled retirement did not preserve selected actor and exact +30 day due date.");
        CommittedRunResultSnapshot scheduledHistory =
            committedRunResults.CaptureCommittedRunResult();
        Require(scheduledHistory.CommittedChoices.Count == 1
                && string.Equals(
                    scheduledHistory.CommittedChoices[0].InstanceId,
                    scheduledInstanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    scheduledHistory.CommittedChoices[0].OperationId,
                    scheduledOperationId,
                    StringComparison.Ordinal),
            "Scheduled production choice lost its structured committed-history identity.");
        Require(rootStore.PublishedRestoreRevision == revisionBeforeChoice,
            "Gameplay choice publication falsely advanced the restore revision.");
        DungeonGameSaveData pendingSave = saves.Capture();
        int revisionBeforeRestore = rootStore.PublishedRestoreRevision;
        Restore(pendingSave, "pending schedule current-save round trip");
        Require(rootStore.PublishedRestoreRevision > revisionBeforeRestore
                && careers.TryGet(targetId, out pending)
                && pending.RetirementDueAbsoluteDay == expectedDueDay,
            "Real restore did not advance its revision or preserve the pending due date.");
        report.Add("[PASS] SCHEDULE actual-ui/dispatcher; sameActor="
            + targetId.Value + "; decision=" + choiceDay + "; due="
            + expectedDueDay + "; awayParticipant=accepted; restoreRevision=isolated");

        VerifyObservedLastLessonActualTick(
            scope.Container,
            pendingSave,
            targetId,
            studentId,
            academyId,
            choiceDay,
            expectedDueDay);
        report.Add("[PASS] LAST_LESSON actual CareerApplicationAdapter.Tick; "
            + "physical-lesson=controlled prerequisite; durable-ledger=wear-once; "
            + "quiet-promotion=actual proficiency rank crossing; "
            + "Society receipts/occurrences=exact; whole-save replay=idempotent");

        VerifyDueBeforeUnrelatedDailyFailure(
            scope.Container,
            pendingSave,
            targetId,
            expectedDueDay,
            careerPersistence,
            rootStore);
        report.Add("[PASS] DUE_ORDER actual-adapter; unrelatedDaily=failure; "
            + "retirement=completed-at-stored-due");

        Restore(pendingSave, "pending unsafe-work branch restore");
        calendar.SetDateTime(expectedDueDay - 1, choiceHour);
        target = RequireActor(world, targetId);
        academy = facilities
            .FindOperational(ResearchFacilityCommandKind.MentorAcademy)
            .First(value => value.PersistentInstanceId.Equals(academyId));
        AbilityWork activeWork = target.GetComponent<AbilityWork>();
        Require(activeWork != null,
            "Retirement target has no production work ability.");
        activeWork.SetWorkPriority(
            BuiltInWorkTypeIds.Operate,
            WorkPriorityLevel.Priority1,
            null);
        Require(activeWork.TryAssignWorkTarget(
                    academy,
                    BuiltInWorkTypeIds.Operate)
                && activeWork.AssignedWorkTypeId == BuiltInWorkTypeIds.Operate,
            "Focused fixture could not establish real active MentorAcademy work.");
        careers.ClearMentorship(studentId);
        gameEvents.Publish(new OperatingDayStartedEvent(expectedDueDay));
        Require(!activeWork.CanContinueCurrentWork(out string activeStopReason)
                && activeStopReason.Contains("retiree", StringComparison.Ordinal),
            "Production active-work recheck did not interrupt newly unsafe retiree work.");
        report.Add("[PASS] ACTIVE_WORK actual-assignment/recheck; unsafe=blocked; reason="
            + activeStopReason);

        Restore(pendingSave, "pending exact-due branch restore");

        int revisionBeforeDue = rootStore.PublishedRestoreRevision;
        calendar.SetDateTime(expectedDueDay, choiceHour);
        gameEvents.Publish(new OperatingDayStartedEvent(expectedDueDay));
        Require(careers.TryGet(targetId, out CharacterCareerSnapshot completed)
                && completed.Retired
                && completed.RetirementScheduleStatus ==
                    RetirementScheduleStatus.Completed
                && completed.RetirementTerminalAbsoluteDay == expectedDueDay,
            "Production daily adapter did not retire the actor on the exact due day.");
        int retiredHistoryCount = CountRetiredHistory(
            careerPersistence.Capture(),
            targetId);
        gameEvents.Publish(new OperatingDayStartedEvent(expectedDueDay));
        gameEvents.Publish(new OperatingDayStartedEvent(expectedDueDay + 1));
        Require(rootStore.PublishedRestoreRevision == revisionBeforeDue
                && CountRetiredHistory(
                    careerPersistence.Capture(),
                    targetId) == retiredHistoryCount
                && retiredHistoryCount == 1,
            "Due-day replay duplicated retirement or advanced restore-only state.");

        target = RequireActor(world, targetId);
        academy = facilities
            .FindOperational(ResearchFacilityCommandKind.MentorAcademy)
            .First(value => value.PersistentInstanceId.Equals(academyId));
        Require(!workPolicy.IsAvailable(
                    BuiltInWorkTypeIds.Haul,
                    target,
                    academy,
                    out string unsafeReason)
                && unsafeReason.Contains("retiree", StringComparison.Ordinal),
            "Retirement did not stop an unsafe active-work policy recheck.");
        bool qualifiedOperate = CareerWorkEligibilityRules.IsSafeRetireeWork(
            BuiltInWorkTypeIds.Operate,
            targetId,
            academy,
            careers.Mentorships);
        Require(qualifiedOperate
                && careers.CanPerformRetiredWork(
                    targetId,
                    expectedDueDay,
                    qualifiedOperate,
                    out _),
            "Assigned retired mentor could not operate the same MentorAcademy.");
        careers.RecordRetiredWork(
            targetId,
            expectedDueDay,
            CareerRules.RetireeMaximumSafeWorkSeconds);
        Require(!careers.CanPerformRetiredWork(
                    targetId,
                    expectedDueDay,
                    qualifiedOperate,
                    out string capReason)
                && capReason.Contains("daily-limit", StringComparison.Ordinal),
            "Qualified MentorAcademy work bypassed the retained four-hour cap.");
        report.Add("[PASS] WORK unsafe=blocked; MentorAcademyOperate=assigned; cap=4h; "
            + "academy=" + academySource);

        Restore(pendingSave, "pending missed-day branch restore");
        calendar.SetDateTime(expectedDueDay + 3, choiceHour);
        gameEvents.Publish(new OperatingDayStartedEvent(expectedDueDay + 3));
        Require(careers.TryGet(targetId, out CharacterCareerSnapshot missed)
                && missed.Retired
                && missed.RetirementScheduleStatus ==
                    RetirementScheduleStatus.Completed
                && missed.RetirementDueAbsoluteDay == expectedDueDay
                && missed.RetirementTerminalAbsoluteDay == expectedDueDay
                && CountRetiredHistory(
                    careerPersistence.Capture(),
                    targetId) == 1,
            "Missed-day processing extended or failed to complete the persisted schedule.");
        report.Add("[PASS] MISSED_DAY due=" + expectedDueDay
            + "; observed=" + (expectedDueDay + 3)
            + "; retiredHistory=1");

        Restore(pendingSave, "pending death-branch restore");
        calendar.SetDateTime(choiceDay + 1, choiceHour);
        target = RequireActor(world, targetId);
        bodyHealth.Kill(
            target,
            CharacterDeathCauseCode.Unknown,
            "qa:wim044:pending-retirement-death");
        Require(target.IsDead
                || target.CurrentLifecycleState == CharacterLifecycleState.Despawned,
            "Production body-health death command did not terminalize the actor lifecycle.");
        Require(careers.TryGet(targetId, out CharacterCareerSnapshot cancelled)
                && !cancelled.Retired
                && cancelled.RetirementScheduleStatus ==
                    RetirementScheduleStatus.CancelledByDeath
                && cancelled.RetirementTerminalAbsoluteDay == choiceDay + 1
                && CountRetiredHistory(
                    careerPersistence.Capture(),
                    targetId) == 0,
            "Death did not terminalize the pending schedule without retired history.");
        calendar.SetDateTime(expectedDueDay + 1, choiceHour);
        gameEvents.Publish(new OperatingDayStartedEvent(expectedDueDay + 1));
        Require(careers.TryGet(targetId, out cancelled)
                && !cancelled.Retired
                && cancelled.RetirementScheduleStatus ==
                    RetirementScheduleStatus.CancelledByDeath,
            "Missed-day processing retired a death-cancelled schedule.");
        DungeonGameSaveData cancelledSave = saves.Capture();
        int revisionBeforeCancelledRestore = rootStore.PublishedRestoreRevision;
        Restore(cancelledSave, "death-cancelled current-save round trip");
        Require(rootStore.PublishedRestoreRevision >
                    revisionBeforeCancelledRestore
                && careers.TryGet(targetId, out cancelled)
                && !cancelled.Retired
                && cancelled.RetirementScheduleStatus ==
                    RetirementScheduleStatus.CancelledByDeath
                && cancelled.RetirementTerminalAbsoluteDay == choiceDay + 1,
            "Death-cancelled current save did not restore its terminal state.");
        report.Add("[PASS] DEATH_CANCELLED actual-body-health/death-event; terminal="
            + (choiceDay + 1)
            + "; retiredHistory=0; dueReplay=no-op; save=exact");
        verificationCompleted = true;
    }

    private void VerifyObservedLastLessonActualTick(
        IObjectResolver container,
        DungeonGameSaveData pendingSave,
        CharacterId mentorId,
        CharacterId studentId,
        BuildingInstanceId academyId,
        int choiceDay,
        int expectedDueDay)
    {
        Restore(pendingSave, "last-lesson actual-tick branch restore");
        IGameCalendar calendar = container.Resolve<IGameCalendar>();
        IGameClock clock = container.Resolve<IGameClock>();
        ICareerService careers = container.Resolve<ICareerService>();
        ICharacterProficiencyQuery proficiencyQuery = container
            .Resolve<ICharacterProficiencyQuery>();
        ICharacterProficiencyCommand proficiencyCommands = container
            .Resolve<ICharacterProficiencyCommand>();
        ICombatEquipmentRuntime equipment = container
            .Resolve<ICombatEquipmentRuntime>();
        IDurableFacilityEquipmentPolicyQuery equipmentPolicies = container
            .Resolve<IDurableFacilityEquipmentPolicyQuery>();
        IDurableFacilityEquipmentSlotCommand equipmentSlots = container
            .Resolve<IDurableFacilityEquipmentSlotCommand>();
        IDurableFacilityEquipmentSlotQuery equipmentSlotQuery = container
            .Resolve<IDurableFacilityEquipmentSlotQuery>();
        IWorldItemStackRuntime items = container.Resolve<IWorldItemStackRuntime>();
        WorldItemRepository repository = container.Resolve<WorldItemRepository>();
        V20CampaignRuntime campaign = container.Resolve<V20CampaignRuntime>();
        IReadOnlyList<ITickable> tickables = container
            .Resolve<ContainerLocal<IReadOnlyList<ITickable>>>()
            .Value;
        CareerApplicationAdapter adapter = tickables
            .OfType<CareerApplicationAdapter>()
            .Single();

        calendar.SetDateTime(choiceDay, calendar.Hour);
        Require(clock.DeltaTime > 0f,
            "Actual last-lesson producer requires an advancing production clock.");
        Require(careers.TryGet(mentorId, out CharacterCareerSnapshot pending)
                && !pending.Retired
                && pending.RetirementScheduleStatus ==
                    RetirementScheduleStatus.Pending
                && pending.RetirementDecisionAbsoluteDay == choiceDay
                && pending.RetirementDueAbsoluteDay == expectedDueDay
                && pending.RetirementTerminalAbsoluteDay == 0,
            "Last-lesson fixture lost the selected mentor's pending retirement.");

        SocietyEventWorldSaveData promotionSocietyBefore =
            campaign.CaptureSociety();
        int promotionReceiptCountBefore = promotionSocietyBefore
            .successfulLifeEventOperations.Count(value => value != null
                && value.sourceKind ==
                    ObservedLifeEventSourceKind.ProficiencyPromotion);
        careers.ClearMentorship(studentId);
        proficiencyCommands.AddDirectExperience(
            mentorId,
            BuiltInCharacterProficiencyIds.Social,
            3060f,
            calendar.AbsoluteHour,
            applyLearningMultiplier: false);
        Require(proficiencyQuery.TryGetProficiency(
                    mentorId,
                    BuiltInCharacterProficiencyIds.Social,
                    calendar.AbsoluteHour,
                    out CharacterProficiencySnapshot mentorSkill)
                && proficiencyQuery.TryGetProficiency(
                    studentId,
                    BuiltInCharacterProficiencyIds.Social,
                    calendar.AbsoluteHour,
                    out CharacterProficiencySnapshot studentSkill)
                && mentorSkill.Rank >= CharacterProficiencyRank.Expert
                && mentorSkill.Rank > studentSkill.Rank
                && mentorSkill.CurrentExperience - studentSkill.CurrentExperience
                    >= 200,
            "Last-lesson fixture could not establish the production mentorship proficiency contract.");
        SocietyEventWorldSaveData promotionSocietyAfter =
            campaign.CaptureSociety();
        ObservedLifeEventReceiptSaveData promotionReceipt =
            promotionSocietyAfter.successfulLifeEventOperations
                .Where(value => value != null
                    && value.sourceKind ==
                        ObservedLifeEventSourceKind.ProficiencyPromotion)
                .OrderBy(value => value.sourceOperationId, StringComparer.Ordinal)
                .Last();
        V20ActiveEventSaveData promotionOccurrence = promotionSocietyAfter
            .recentResolvedEvents.Single(value => value != null
                && string.Equals(
                    value.instanceId,
                    promotionReceipt.occurrenceInstanceId,
                    StringComparison.Ordinal));
        Require(promotionSocietyAfter.successfulLifeEventOperations.Count(value =>
                    value != null
                    && value.sourceKind ==
                        ObservedLifeEventSourceKind.ProficiencyPromotion)
                    == promotionReceiptCountBefore + 1
                && promotionReceipt.sourceOperationId.StartsWith(
                    "proficiency-promotion:",
                    StringComparison.Ordinal)
                && promotionReceipt.canonicalPayload.StartsWith(
                    "quiet-promotion@1|",
                    StringComparison.Ordinal)
                && promotionOccurrence.resolved
                && string.Equals(
                    promotionOccurrence.definitionId,
                    "life-event:quiet-promotion",
                    StringComparison.Ordinal)
                && promotionOccurrence.participantCharacterIds.Count == 1
                && string.Equals(
                    promotionOccurrence.participantCharacterIds[0],
                    mentorId.Value,
                    StringComparison.Ordinal),
            "Actual proficiency award did not publish one exact quiet-promotion receipt and resolved occurrence.");
        string promotionCanonicalPayload = promotionReceipt.canonicalPayload;
        careers.AssignMentorship(
            mentorId,
            studentId,
            academyId,
            BuiltInCharacterProficiencyIds.Social);
        careers.RecordMentorshipWork(
            studentId,
            choiceDay,
            mentorContribution: true,
            approvedWork: CareerRules.MentoringWorkAmountPerParticipant);
        CareerMentorshipSnapshot completedLesson = careers.RecordMentorshipWork(
            studentId,
            choiceDay,
            mentorContribution: false,
            approvedWork: CareerRules.MentoringWorkAmountPerParticipant);
        Require(completedLesson.HasCompletedPhysicalLesson
                && completedLesson.LastAwardAbsoluteDay < choiceDay,
            "Controlled physical-lesson prerequisite was not complete before the actual producer tick.");

        equipment.TryUnassignSlot(
            mentorId.Value,
            CombatEquipmentLoadoutSlot.Armor,
            out _);
        BlueprintResearchRuntime research = container
            .Resolve<ProgressionSceneRuntimeReferences>()
            .BlueprintResearch;
        Require(research.TryCompleteProjectImmediatelyForVerification(
                new ResearchProjectId(
                    "research:equipment:pressure-barrels"),
                out string researchFailure),
            "Last-lesson fixture could not unlock authored protective armor: "
            + researchFailure);
        CombatEquipmentInstance protectiveEquipment = equipment.CreateInstance(
            "armor:powder-cuirass",
            CombatEquipmentQuality.Normal);
        Require(protectiveEquipment != null,
            "Last-lesson fixture could not create authored protective armor.");
        Require(equipment.TryAssignToCharacter(
                mentorId.Value,
                protectiveEquipment.instanceId,
                out string equipmentFailure),
            "Last-lesson fixture could not equip authored protective armor: "
            + equipmentFailure);
        Require(equipment.Instances.Any(value => value != null
                    && string.Equals(
                        value.instanceId,
                        protectiveEquipment.instanceId,
                        StringComparison.Ordinal)
                    && value.worldState == CombatEquipmentWorldState.Equipped
                    && string.Equals(
                        value.ownerCharacterId,
                        mentorId.Value,
                        StringComparison.Ordinal)
                    && (CombatEquipmentRoleRules.For(value.definitionId)
                        & CombatEquipmentRoleFlags.BlastAndSmokeProtection) != 0),
            "Assigned last-lesson armor did not expose the production blast/smoke PPE role.");

        Require(equipmentPolicies.TryGetPolicy(
                    CareerDurableEquipmentPolicySource.PolicyId,
                    out DurableFacilityEquipmentPolicy policy),
            "Production career-ledger policy is unavailable.");
        BuildableObject academy = container.Resolve<IFacilityCapabilityQuery>()
            .FindOperational(ResearchFacilityCommandKind.MentorAcademy)
            .Single(value => value.PersistentInstanceId.Equals(academyId));
        DurableFacilityEquipmentAssignment assignment = policy.CreateAssignment(
            academyId.Value,
            academyId,
            academy.centerPos);
        DurableFacilityEquipmentSlotResult reconciled = equipmentSlots
            .TryReconcile(assignment);
        Require(reconciled.Succeeded,
            "Production career-ledger slot did not reconcile: "
            + reconciled.FailureReason);
        string destinationId = reconciled.Snapshot.DestinationId;
        WorldItemStackSnapshot[] ledgerCandidates = items.GetAllStacks()
            .Where(value => value.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ItemId,
                    DurableToolItemRules.CareerLedger,
                    StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        if (ledgerCandidates.Length == 0)
        {
            string ledgerStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                DurableToolItemRules.CareerLedger,
                1,
                WorldItemStackState.FacilityBuffer,
                destinationId: destinationId,
                position: academy.centerPos,
                itemInstanceId:
                    "item-instance:wim044:last-lesson:career-ledger",
                components: new[]
                {
                    DurableToolItemRules.CreateDurability(
                        DurableToolItemRules.CareerLedger)
                });
            ledgerCandidates = items.GetAllStacks()
                .Where(value => string.Equals(
                    value.StackId,
                    ledgerStackId,
                    StringComparison.Ordinal))
                .ToArray();
        }
        Require(ledgerCandidates.Length == 1
                && ledgerCandidates[0].Quantity == 1
                && equipmentSlotQuery.TryCapture(
                    assignment.Key,
                    out DurableFacilityEquipmentSlotSnapshot suppliedSlot)
                && suppliedSlot.SupplyReady,
            "Production career-ledger slot is not backed by one usable physical ledger.");
        string expectedLedgerStackId = ledgerCandidates[0].StackId;
        long ledgerRevisionBefore = ledgerCandidates[0].ContentRevision;
        float ledgerDurabilityBefore = DurableToolItemRules
            .ReadCurrentDurability(
                ledgerCandidates[0].ItemId,
                ledgerCandidates[0].Components);

        SocietyEventWorldSaveData societyBefore = campaign.CaptureSociety();
        int receiptCountBefore = societyBefore.successfulLifeEventOperations
            .Count(value => value != null
                && value.sourceKind ==
                    ObservedLifeEventSourceKind.LastMentorshipLesson);
        int occurrenceCountBefore = societyBefore.activeEvents.Count(value =>
            value != null
            && string.Equals(
                value.definitionId,
                "life-event:last-lesson",
                StringComparison.Ordinal));

        adapter.Tick();

        WorldItemStackSnapshot ledgerAfter = items.GetAllStacks().Single(value =>
            string.Equals(
                value.StackId,
                expectedLedgerStackId,
                StringComparison.Ordinal));
        float ledgerDurabilityAfter = DurableToolItemRules
            .ReadCurrentDurability(
                ledgerAfter.ItemId,
                ledgerAfter.Components);
        Require(ledgerAfter.ContentRevision == ledgerRevisionBefore + 1L,
            "Actual mentorship award did not wear the exact physical ledger once.");
        Require(Mathf.Approximately(
                ledgerDurabilityAfter,
                ledgerDurabilityBefore
                    - (float)CareerDurableEquipmentAwardRuntime
                        .LedgerWearPerAward),
            "Actual mentorship award did not consume the authored ledger wear amount.");
        Require(careers.Mentorships.Single(value =>
                    value.StudentCharacterId.Equals(studentId))
                .LastAwardAbsoluteDay == choiceDay,
            "Actual mentorship adapter did not commit the last-lesson award day.");
        Require(careers.TryGet(mentorId, out CharacterCareerSnapshot afterCareer)
                && afterCareer.RetirementScheduleStatus ==
                    RetirementScheduleStatus.Pending
                && afterCareer.RetirementDecisionAbsoluteDay == choiceDay
                && afterCareer.RetirementDueAbsoluteDay == expectedDueDay
                && afterCareer.RetirementTerminalAbsoluteDay == 0,
            "Last-lesson publication mutated the pending retirement contract.");

        SocietyEventWorldSaveData societyAfter = campaign.CaptureSociety();
        V20ActiveEventSaveData occurrence = societyAfter.activeEvents.Single(value =>
            string.Equals(
                value.definitionId,
                "life-event:last-lesson",
                StringComparison.Ordinal));
        ObservedLifeEventReceiptSaveData receipt = societyAfter
            .successfulLifeEventOperations.Single(value => value != null
                && value.sourceKind ==
                    ObservedLifeEventSourceKind.LastMentorshipLesson);
        string expectedOperationId = "career-mentorship-award:"
            + expectedLedgerStackId + ":" + ledgerAfter.ContentRevision;
        Require(societyAfter.activeEvents.Count(value => value != null
                    && string.Equals(
                        value.definitionId,
                        "life-event:last-lesson",
                        StringComparison.Ordinal))
                    == occurrenceCountBefore + 1
                && societyAfter.successfulLifeEventOperations.Count(value =>
                    value != null
                    && value.sourceKind ==
                        ObservedLifeEventSourceKind.LastMentorshipLesson)
                    == receiptCountBefore + 1
                && !occurrence.resolved
                && occurrence.participantCharacterIds.Count == 1
                && string.Equals(
                    occurrence.participantCharacterIds[0],
                    mentorId.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    receipt.occurrenceInstanceId,
                    occurrence.instanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    receipt.sourceOperationId,
                    expectedOperationId,
                    StringComparison.Ordinal)
                && receipt.canonicalPayload.StartsWith(
                    "last-lesson@1|",
                    StringComparison.Ordinal),
            "Actual last-lesson occurrence did not preserve its exact source/participant/receipt join.");

        string canonicalPayload = receipt.canonicalPayload;
        DungeonGameSaveData observedSave = saves.Capture();
        Restore(observedSave, "last-lesson observed whole-save round trip");
        WorldItemStackSnapshot restoredLedger = container
            .Resolve<IWorldItemStackRuntime>()
            .GetAllStacks()
            .Single(value => string.Equals(
                value.StackId,
                expectedLedgerStackId,
                StringComparison.Ordinal));
        SocietyEventWorldSaveData restoredSociety = container
            .Resolve<V20CampaignRuntime>()
            .CaptureSociety();
        ObservedLifeEventReceiptSaveData restoredReceipt = restoredSociety
            .successfulLifeEventOperations.Single(value => value != null
                && value.sourceKind ==
                    ObservedLifeEventSourceKind.LastMentorshipLesson);
        Require(Mathf.Approximately(
                DurableToolItemRules.ReadCurrentDurability(
                    restoredLedger.ItemId,
                    restoredLedger.Components),
                ledgerDurabilityAfter),
            "Whole-save restore changed the last-lesson ledger durability.");
        Require(careers.Mentorships.Single(value =>
                value.StudentCharacterId.Equals(studentId))
                .LastAwardAbsoluteDay == choiceDay,
            "Whole-save restore lost the last-lesson mentorship award day.");
        Require(careers.TryGet(
                    mentorId,
                    out CharacterCareerSnapshot restoredCareer)
                && restoredCareer.RetirementScheduleStatus ==
                    RetirementScheduleStatus.Pending
                && restoredCareer.RetirementDueAbsoluteDay == expectedDueDay,
            "Whole-save restore changed the pending retirement schedule.");
        Require(equipment.Instances.Any(value => value != null
                && string.Equals(
                    value.instanceId,
                    protectiveEquipment.instanceId,
                    StringComparison.Ordinal)
                && value.worldState == CombatEquipmentWorldState.Equipped
                && string.Equals(
                    value.ownerCharacterId,
                    mentorId.Value,
                    StringComparison.Ordinal)),
            "Whole-save restore lost the equipped last-lesson PPE instance.");
        Require(restoredSociety.activeEvents.Any(value => value != null
                && string.Equals(
                    value.instanceId,
                    occurrence.instanceId,
                    StringComparison.Ordinal)),
            "Whole-save restore lost the pending last-lesson occurrence.");
        Require(string.Equals(
                restoredReceipt.canonicalPayload,
                canonicalPayload,
                StringComparison.Ordinal),
            "Whole-save restore changed the last-lesson receipt payload.");
        Require(restoredSociety.successfulLifeEventOperations.Any(value =>
                value != null
                && value.sourceKind ==
                    ObservedLifeEventSourceKind.ProficiencyPromotion
                && string.Equals(
                    value.canonicalPayload,
                    promotionCanonicalPayload,
                    StringComparison.Ordinal)),
            "Whole-save restore lost the quiet-promotion receipt payload.");

        adapter.Tick();
        SocietyEventWorldSaveData replay = campaign.CaptureSociety();
        WorldItemStackSnapshot replayLedger = items.GetAllStacks().Single(value =>
            string.Equals(
                value.StackId,
                expectedLedgerStackId,
                StringComparison.Ordinal));
        Require(Mathf.Approximately(
                    DurableToolItemRules.ReadCurrentDurability(
                        replayLedger.ItemId,
                        replayLedger.Components),
                    ledgerDurabilityAfter)
                && careers.Mentorships.Single(value =>
                    value.StudentCharacterId.Equals(studentId))
                    .LastAwardAbsoluteDay == choiceDay
                && replay.successfulLifeEventOperations.Count(value =>
                    value != null
                    && value.sourceKind ==
                        ObservedLifeEventSourceKind.LastMentorshipLesson)
                    == receiptCountBefore + 1
                && replay.activeEvents.Count(value => value != null
                    && string.Equals(
                        value.definitionId,
                        "life-event:last-lesson",
                        StringComparison.Ordinal))
                    == occurrenceCountBefore + 1
                && string.Equals(
                    replay.successfulLifeEventOperations.Single(value =>
                        value != null
                        && value.sourceKind ==
                            ObservedLifeEventSourceKind.LastMentorshipLesson)
                        .canonicalPayload,
                    canonicalPayload,
                    StringComparison.Ordinal),
            "Same-day actual adapter replay duplicated or mutated the last lesson.");

        Restore(pendingSave, "post-last-lesson branch cleanup restore");
    }

    private static int VerifyAuthoredRetirementAndProducer(
        V20StoryContentCatalog story,
        CharacterId targetId)
    {
        LifeEventDefinitionSO definition = story.LifeEvents.Single(value =>
            string.Equals(
                value.StableId,
                RetirementEventId,
                StringComparison.Ordinal));
        Require(!definition.automatic
                && definition.choices.Count == 2,
            "Retirement request must remain a two-choice authored event.");
        V20ChoiceDefinition first = definition.choices.Single(value =>
            string.Equals(value.choiceId, "first", StringComparison.Ordinal));
        V20ChoiceDefinition second = definition.choices.Single(value =>
            string.Equals(value.choiceId, "second", StringComparison.Ordinal));
        Require(first.effects.Any(value => value != null
                    && value.kind == V20ContentEffectKind.Mood
                    && Mathf.Approximately(value.amount, 6f))
                && first.requirements.characters.Count == 0
                && first.effects.Count(value => value != null
                    && value.kind == V20ContentEffectKind.RetirementSchedule
                    && value.durationDays == 0) == 1
                && second.effects.Count(value => value != null
                    && value.kind == V20ContentEffectKind.RetirementSchedule
                    && value.durationDays == GameCalendarRules.DaysPerSeason) == 1
                && second.requirements.characters.Count == 0
                && second.effects.All(value => value == null
                    || value.kind != V20ContentEffectKind.WorkDelayDays),
            "Authored retirement choices lost typed eligibility, Mood +6, immediate, or +30 day semantics.");

        string[] participants = Enumerable.Range(0, 8)
            .Select(index => index == 0
                ? targetId.Value
                : $"character:wim044-producer-ineligible-peer-{index}")
            .ToArray();
        V20CampaignRuntime isolatedCampaign = new(
            new DungeonRuntimeAggregateRootStore(),
            story);
        isolatedCampaign.PublishSociety(isolatedCampaign.PrepareSociety(
            CreateRetirementProducerFixtureState(
                story,
                participants,
                unsuppressedAutomaticDefinitionId: string.Empty)));
        V20DailyEventContext isolated = CreateRetirementProducerContext(
            1,
            participants,
            targetId,
            retirementEligible: true);
        isolatedCampaign.EvaluateDaily(isolated);
        V20ActiveEventSaveData selected = isolatedCampaign.ActiveSocietyEvents
            .SingleOrDefault(value => string.Equals(
                value.definitionId,
                RetirementEventId,
                StringComparison.Ordinal));
        Require(selected != null
                && selected.participantCharacterIds.Count == 1
                && string.Equals(
                    selected.participantCharacterIds[0],
                    targetId.Value,
                    StringComparison.Ordinal),
            "Typed retirement producer did not select its sole eligible elder through the weighted scheduler.");

        V20CampaignRuntime ineligibleCampaign = new(
            new DungeonRuntimeAggregateRootStore(),
            story);
        ineligibleCampaign.PublishSociety(ineligibleCampaign.PrepareSociety(
            CreateRetirementProducerFixtureState(
                story,
                participants,
                unsuppressedAutomaticDefinitionId: string.Empty)));
        V20DailyEventContext ineligible = CreateRetirementProducerContext(
            1,
            participants,
            targetId,
            retirementEligible: false);
        ineligibleCampaign.EvaluateDaily(ineligible);
        Require(ineligibleCampaign.ActiveSocietyEvents.All(value =>
                !string.Equals(
                    value.definitionId,
                    RetirementEventId,
                    StringComparison.Ordinal)),
            "Typed retirement producer ignored its elder/career eligibility set.");

        V20CampaignRuntime sequentialCampaign = new(
            new DungeonRuntimeAggregateRootStore(),
            story);
        string[] sequentialParticipants = { targetId.Value };
        const int cooldownStartedAbsoluteDay = 1;
        const int cooldownAvailableAbsoluteDay = 4;
        SocietyEventWorldSaveData cooldownState =
            CreateRetirementProducerFixtureState(
                story,
                sequentialParticipants,
                unsuppressedAutomaticDefinitionId: string.Empty);
        string categoryKey = $"event-category:life:{definition.category}";
        cooldownState.cooldowns.Add(new V20EventCooldownSaveData
        {
            definitionId = categoryKey,
            availableAbsoluteDay = cooldownAvailableAbsoluteDay
        });
        sequentialCampaign.PublishSociety(
            sequentialCampaign.PrepareSociety(cooldownState));
        int selectedAfterCooldownAbsoluteDay = 0;
        for (int day = cooldownStartedAbsoluteDay;
             day <= cooldownAvailableAbsoluteDay;
             day++)
        {
            V20DailyEventContext sequential =
                CreateRetirementProducerContext(
                    day,
                    sequentialParticipants,
                    targetId,
                    retirementEligible: true);
            sequentialCampaign.EvaluateDaily(sequential);
            V20ActiveEventSaveData retirement =
                sequentialCampaign.ActiveSocietyEvents.SingleOrDefault(value =>
                    string.Equals(
                        value.definitionId,
                        RetirementEventId,
                        StringComparison.Ordinal));
            if (retirement == null) continue;
            selectedAfterCooldownAbsoluteDay = day;
            break;
        }
        Require(selectedAfterCooldownAbsoluteDay
                == cooldownAvailableAbsoluteDay,
            "Retirement producer did not resume when the constructed category cooldown expired on day 4.");
        return selectedAfterCooldownAbsoluteDay
            - cooldownStartedAbsoluteDay;
    }

    private static SocietyEventWorldSaveData
        CreateRetirementProducerFixtureState(
            V20StoryContentCatalog story,
            IReadOnlyList<string> participants,
            string unsuppressedAutomaticDefinitionId)
    {
        const int blockedUntilAbsoluteDay = 100;
        SocietyEventWorldSaveData state = new();
        foreach (LifeEventDefinitionSO automatic in story.LifeEvents
                     .Where(value => value.automatic))
        {
            if (string.Equals(
                    automatic.StableId,
                    unsuppressedAutomaticDefinitionId,
                    StringComparison.Ordinal))
                continue;
            switch (automatic.frequencyRule)
            {
                case LifeEventFrequencyRule.OncePerCharacter:
                    foreach (string participant in participants)
                        state.recurrenceKeys.Add(
                            $"{automatic.StableId}:character:{participant}");
                    break;
                case LifeEventFrequencyRule.OncePerGeneration:
                    state.recurrenceKeys.Add(
                        $"{automatic.StableId}:generation:1");
                    break;
                case LifeEventFrequencyRule.OncePerRun:
                    state.completedOnceEventIds.Add(automatic.StableId);
                    break;
                case LifeEventFrequencyRule.Repeatable:
                    state.cooldowns.Add(new V20EventCooldownSaveData
                    {
                        definitionId = automatic.StableId,
                        availableAbsoluteDay = blockedUntilAbsoluteDay
                    });
                    break;
            }
        }
        foreach (LifeEventDefinitionSO lifeEvent in story.LifeEvents
                     .Where(value => !value.automatic
                         && !string.Equals(
                             value.StableId,
                             RetirementEventId,
                             StringComparison.Ordinal)))
            AddRetirementProducerFixtureCooldown(
                state,
                lifeEvent.StableId,
                blockedUntilAbsoluteDay);
        foreach (GuestRequestDefinitionSO guest in story.GuestRequests)
            AddRetirementProducerFixtureCooldown(
                state,
                guest.StableId,
                blockedUntilAbsoluteDay);
        foreach (ServiceIncidentDefinitionSO incident in story.ServiceIncidents)
            AddRetirementProducerFixtureCooldown(
                state,
                incident.StableId,
                blockedUntilAbsoluteDay);
        return state;
    }

    private static void AddRetirementProducerFixtureCooldown(
        SocietyEventWorldSaveData state,
        string definitionId,
        int availableAbsoluteDay) => state.cooldowns.Add(
            new V20EventCooldownSaveData
            {
                definitionId = definitionId,
                availableAbsoluteDay = availableAbsoluteDay
            });

    private static V20DailyEventContext CreateRetirementProducerContext(
        int absoluteDay,
        IReadOnlyList<string> participants,
        CharacterId targetId,
        bool retirementEligible)
    {
        V20DailyEventContext context = new()
        {
            AbsoluteDay = absoluteDay,
            RunSeed = 44044,
            Season = GameCalendarRules.Project(absoluteDay, 0).Season,
            Generation = 1
        };
        context.Requirements.AbsoluteDay = absoluteDay;
        context.ParticipantCharacterIds.AddRange(participants);
        if (retirementEligible)
            context.RetirementEligibleParticipantIds.Add(targetId.Value);
        return context;
    }

    private DungeonGameSaveData PrepareScenario(
        DungeonGameSaveData source,
        CharacterId targetId,
        CharacterId studentId,
        BuildingInstanceId academyId,
        int choiceDay,
        string suffix,
        ICharacterLifeDefinitionCatalog lifeDefinitions,
        V20StoryContentCatalog story,
        out string instanceId)
    {
        DungeonGameSaveData candidate = RoundTrip(source);
        CharacterLifeWorldSaveData life =
            DungeonSaveSectionPayload.ReadOrNew<CharacterLifeWorldSaveData>(
                candidate,
                CharacterLifeSaveSection.Id);
        CharacterLifeRecordSaveData lifeRecord = life.characters.Single(value =>
            string.Equals(
                value.characterId,
                targetId.Value,
                StringComparison.Ordinal));
        SpeciesLifeHistoryDefinition history = lifeDefinitions.RequireLifeHistory(
            new CharacterSpeciesId(lifeRecord.phenotypeSpeciesId));
        int elderDays = Mathf.CeilToInt((float)history.ElderAgeDayUnits);
        lifeRecord.chronologicalAgeDays = Math.Max(
            lifeRecord.chronologicalAgeDays,
            elderDays);
        lifeRecord.biologicalAgeDayUnits = Math.Max(
            lifeRecord.biologicalAgeDayUnits,
            history.ElderAgeDayUnits);
        lifeRecord.lifeStage = CharacterLifeStage.Elder;

        CharacterCareerWorldSaveData career = DungeonSaveSectionPayload
            .ReadOrNew<CharacterCareerWorldSaveData>(
                candidate,
                CharacterCareerSaveSection.Id);
        CharacterCareerSaveData targetCareer = career.characters.FirstOrDefault(
            value => string.Equals(
                value.characterId,
                targetId.Value,
                StringComparison.Ordinal));
        if (targetCareer == null)
        {
            targetCareer = new CharacterCareerSaveData
            {
                characterId = targetId.Value
            };
            career.characters.Add(targetCareer);
        }
        Require(!targetCareer.retired,
            "Retirement fixture target is already retired.");
        ResetRetirementSchedule(targetCareer);
        if (!career.characters.Any(value => string.Equals(
                value.characterId,
                studentId.Value,
                StringComparison.Ordinal)))
        {
            career.characters.Add(new CharacterCareerSaveData
            {
                characterId = studentId.Value
            });
        }
        career.mentorships.RemoveAll(value => value == null
            || string.Equals(
                value.mentorCharacterId,
                targetId.Value,
                StringComparison.Ordinal)
            || string.Equals(
                value.studentCharacterId,
                studentId.Value,
                StringComparison.Ordinal));
        career.mentorships.Add(new CareerMentorshipSaveData
        {
            mentorCharacterId = targetId.Value,
            studentCharacterId = studentId.Value,
            academyBuildingId = academyId.Value,
            proficiencyId = "proficiency:social"
        });

        SocietyEventWorldSaveData society =
            DungeonSaveSectionPayload.ReadOrNew<SocietyEventWorldSaveData>(
                candidate,
                SocietyEventsSaveSection.Id);
        society.activeEvents.Clear();
        instanceId = $"event:{choiceDay}:{RetirementEventId}:wim044-{suffix}";
        society.activeEvents.Add(new V20ActiveEventSaveData
        {
            instanceId = instanceId,
            definitionId = RetirementEventId,
            riskTier = story.LifeEvents.Single(value => value.StableId == RetirementEventId).riskTier,
            riskReason = story.LifeEvents.Single(value => value.StableId == RetirementEventId).riskReason,
            startedAbsoluteDay = choiceDay,
            deadlineAbsoluteDay = choiceDay + 3,
            generation = 1,
            deterministicRoll = 44044,
            contextFactionId = story.Arcs
                .OrderBy(value => value.factionId, StringComparer.Ordinal)
                .First().factionId,
            participantCharacterIds = new List<string> { targetId.Value }
        });
        society.lastEvaluationAbsoluteDay = Math.Min(
            society.lastEvaluationAbsoluteDay,
            choiceDay - 1);

        DungeonSaveSectionPayload.Write(
            candidate,
            CharacterLifeSaveSection.Id,
            CharacterLifeWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.Characters,
            life);
        DungeonSaveSectionPayload.Write(
            candidate,
            CharacterCareerSaveSection.Id,
            CharacterCareerWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            career);
        DungeonSaveSectionPayload.Write(
            candidate,
            SocietyEventsSaveSection.Id,
            SocietyEventWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            society);
        candidate.manifest = DungeonSaveManifest.Capture(candidate.sections);
        return candidate;
    }

    private static void ResetRetirementSchedule(CharacterCareerSaveData career)
    {
        career.retirementScheduleStatus = RetirementScheduleStatus.None;
        career.retirementEventId = string.Empty;
        career.retirementChoiceId = string.Empty;
        career.retirementDecisionAbsoluteDay = 0;
        career.retirementDueAbsoluteDay = 0;
        career.retirementTerminalAbsoluteDay = 0;
    }

    private void VerifyMalformedScheduleRejected(
        DungeonGameSaveData source,
        CharacterId targetId,
        ICareerPersistence careerPersistence,
        DungeonRuntimeAggregateRootStore rootStore)
    {
        string before = JsonUtility.ToJson(careerPersistence.Capture());
        int revisionBefore = rootStore.PublishedRestoreRevision;
        DungeonGameSaveData legacy = RoundTrip(source);
        DungeonSaveSectionEnvelope legacyCareer = legacy.sections.Single(value =>
            string.Equals(
                value.sectionId,
                CharacterCareerSaveSection.Id,
                StringComparison.Ordinal));
        CharacterCareerWorldSaveData legacyPayload =
            JsonUtility.FromJson<CharacterCareerWorldSaveData>(
                legacyCareer.payloadJson);
        legacyPayload.version = 3;
        legacyCareer.sectionVersion = 3;
        legacyCareer.payloadJson = JsonUtility.ToJson(legacyPayload);
        legacy.manifest = DungeonSaveManifest.Capture(legacy.sections);
        bool restoredLegacy = saves.TryRestore(
            RoundTrip(legacy),
            out DungeonGameRestoreReport legacyReport);
        Require(!restoredLegacy
                && !legacyReport.Success
                && rootStore.PublishedRestoreRevision == revisionBefore
                && string.Equals(
                    before,
                    JsonUtility.ToJson(careerPersistence.Capture()),
                    StringComparison.Ordinal),
            "Legacy v3 career payload passed the v4 retirement schedule boundary.");

        DungeonGameSaveData malformed = RoundTrip(source);
        CharacterCareerWorldSaveData career = DungeonSaveSectionPayload
            .ReadOrNew<CharacterCareerWorldSaveData>(
                malformed,
                CharacterCareerSaveSection.Id);
        CharacterCareerSaveData target = career.characters.FirstOrDefault(value =>
            string.Equals(
                value.characterId,
                targetId.Value,
                StringComparison.Ordinal));
        if (target == null)
        {
            target = new CharacterCareerSaveData
            {
                characterId = targetId.Value
            };
            career.characters.Add(target);
        }
        target.retirementScheduleStatus = RetirementScheduleStatus.Pending;
        target.retirementEventId = string.Empty;
        target.retirementChoiceId = "second";
        target.retirementDecisionAbsoluteDay = 10;
        target.retirementDueAbsoluteDay = 40;
        target.retirementTerminalAbsoluteDay = 0;
        DungeonSaveSectionPayload.Write(
            malformed,
            CharacterCareerSaveSection.Id,
            CharacterCareerWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            career);
        malformed.manifest = DungeonSaveManifest.Capture(malformed.sections);

        bool restored = saves.TryRestore(
            RoundTrip(malformed),
            out DungeonGameRestoreReport restoreReport);
        Require(!restored
                && !restoreReport.Success
                && rootStore.PublishedRestoreRevision == revisionBefore
                && string.Equals(
                    before,
                    JsonUtility.ToJson(careerPersistence.Capture()),
                    StringComparison.Ordinal),
            "Malformed retirement schedule mutated live state or passed preflight.");
    }

    private void VerifyDueBeforeUnrelatedDailyFailure(
        IObjectResolver container,
        DungeonGameSaveData pendingSave,
        CharacterId targetId,
        int dueAbsoluteDay,
        ICareerPersistence careerPersistence,
        DungeonRuntimeAggregateRootStore rootStore)
    {
        Restore(pendingSave, "pending failing-daily branch restore");
        AlwaysFailContentResolutionService failingContent = new();
        GameEventBus isolatedEvents = new();
        V20CampaignApplicationAdapter adapter = new(
            failingContent,
            container.Resolve<IRunMilestoneCommand>(),
            container.Resolve<IRunMilestoneQuery>(),
            container.Resolve<IEndlessCrisisCommand>(),
            container.Resolve<IEndlessCrisisQuery>(),
            container.Resolve<IGameCalendar>(),
            container.Resolve<IClimateQuery>(),
            container.Resolve<IRunSeedProvider>(),
            isolatedEvents,
            container.Resolve<V20MilestoneWorldSnapshotProjector>(),
            container.Resolve<ICharacterNarrativeQuery>(),
            container.Resolve<ICharacterNarrativeCatalog>(),
            container.Resolve<ICharacterCultureGameplayQuery>(),
            container.Resolve<ISeasonalEventQuery>(),
            container.Resolve<ISocietyEventQuery>(),
            container.Resolve<ISocietyObservedMealIncidentCommand>(),
            container.Resolve<IFactionCampaignQuery>(),
            container.Resolve<V20StoryContentCatalog>(),
            container.Resolve<IReproductionService>(),
            container.Resolve<ICharacterLifeQuery>(),
            container.Resolve<ICareerService>(),
            container.Resolve<IFacilityCapabilityQuery>(),
            container.Resolve<IKinshipQuery>(),
            container.Resolve<IGriefTraumaService>(),
            container.Resolve<IPopulationHealthQuery>(),
            container.Resolve<IDiseaseDefinitionCatalog>(),
            container.Resolve<ICharacterBodyHealthQuery>(),
            container.Resolve<ICharacterConsumablesApplication>(),
            container.Resolve<ICharacterMedicalCommand>(),
            container.Resolve<ICharacterMedicalQuery>(),
            container.Resolve<ICharacterLifetimeQuery>());
        int revisionBefore = rootStore.PublishedRestoreRevision;
        try
        {
            adapter.Start();
            isolatedEvents.Publish(new OperatingDayStartedEvent(dueAbsoluteDay));
        }
        finally
        {
            adapter.Dispose();
        }
        Require(failingContent.CallCount == 1
                && !adapter.LastDailyEvaluationSucceeded
                && adapter.LastDailyEvaluationFailure.IsFailure
                && careerPersistence.PrepareRestore(careerPersistence.Capture())
                    .TryGet(targetId, out CharacterCareerSnapshot completed)
                && completed.Retired
                && completed.RetirementScheduleStatus ==
                    RetirementScheduleStatus.Completed
                && completed.RetirementTerminalAbsoluteDay == dueAbsoluteDay
                && CountRetiredHistory(
                    careerPersistence.Capture(),
                    targetId) == 1
                && rootStore.PublishedRestoreRevision == revisionBefore,
            "Unrelated daily failure blocked or rewrote due retirement progression.");
    }

    private sealed class AlwaysFailContentResolutionService :
        IContentResolutionService
    {
        public int CallCount { get; private set; }

        public bool TryExecute(
            ContentResolutionRequest request,
            out ContentResolutionResult result,
            out DomainFailure failure)
        {
            CallCount++;
            result = null;
            failure = new DomainFailure(
                FailureCode.ExternalInfluenceUnavailable,
                "qa:wim044:unrelated-daily-failure");
            return false;
        }
    }

    private static EventAlertRecord PublishAndRequireAlert(
        IGameEventBus gameEvents,
        EventAlertRuntime alerts,
        int absoluteDay,
        string instanceId,
        string participantName)
    {
        gameEvents.Publish(new OperatingDayStartedEvent(absoluteDay));
        EventAlertRecord record = alerts.EventLog.LastOrDefault(value =>
            value != null
            && string.Equals(
                value.SourceId,
                instanceId,
                StringComparison.Ordinal));
        Require(record != null
                && record.Detail.Contains(
                    participantName,
                    StringComparison.Ordinal)
                && record.Choices.Count == 2
                && record.Choices.All(value =>
                    !string.IsNullOrWhiteSpace(value.ActionId)),
            "Production alert UI did not expose the selected retirement participant.");
        return record;
    }

    private static CharacterActor RequireActor(
        ICharacterWorldQuery world,
        CharacterId characterId) => world.Characters.Single(value =>
        value != null
        && !value.IsDead
        && CharacterPersistentIdentity.TryGet(value, out CharacterId candidate)
        && candidate.Equals(characterId));

    private static int CountRetiredHistory(
        CharacterCareerWorldSaveData career,
        CharacterId characterId) => career.characters
        .Single(value => string.Equals(
            value.characterId,
            characterId.Value,
            StringComparison.Ordinal))
        .recentHistory.Count(value => value != null
            && value.kind == CareerHistoryEventKind.Retired);

    private static BuildableObject PlaceMentorAcademyFixture(
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        IRoomLayoutCache roomLayouts,
        IGridBuildingObjectFactory buildingFactory)
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            MentorAcademyPath);
        Require(definition != null
                && definition.ResearchFacilityCommand ==
                    ResearchFacilityCommandKind.MentorAcademy,
            "Authored RF96 MentorAcademy definition is unavailable.");
        RoomLayout layout = roomLayouts.GetLayout(grid);
        Vector2Int? selected = layout.Rooms
            .Where(room => room != null && room.IsUsable)
            .OrderBy(room => room.Bounds.yMin)
            .ThenBy(room => room.Bounds.xMin)
            .SelectMany(room => room.Cells
                .OrderBy(position => position.y)
                .ThenBy(position => position.x)
                .Where(position => definition.GetGridPosList(position)
                    .All(footprint => room.ContainsCell(footprint)
                        && grid.GetGridCell(footprint) is GridCell cell
                        && cell.CanBuildInArea(definition)
                        && cell.CanOccupy(definition.Placement.Layer))))
            .Select(position => (Vector2Int?)position)
            .FirstOrDefault();
        Require(selected.HasValue,
            "No usable room has a free authored RF96 production footprint.");

        BuildableObject fixture = buildingFactory.Create(
            grid,
            definition,
            selected.Value);
        Require(fixture != null,
            "Production building factory did not create RF96 MentorAcademy.");
        try
        {
            foreach (MonoBehaviour component in fixture
                         .GetComponentsInChildren<MonoBehaviour>(true))
            {
                scope.Container.Inject(component);
            }
            fixture.SetGrid(grid);
            fixture.Initialization(definition, selected.Value);
            Require(grid.RegisterOccupant(
                    fixture,
                    definition.Placement.Layer,
                    definition.GetGridPosList(selected.Value),
                    definition.Placement.IsMovement),
                "Gameplay grid rejected the authored RF96 footprint.");
            return fixture;
        }
        catch
        {
            fixture.DestroySelf();
            throw;
        }
    }

    private DungeonGameSaveData RoundTrip(DungeonGameSaveData save) =>
        saves.FromJson(saves.ToJson(save));

    private void Restore(DungeonGameSaveData save, string label)
    {
        Require(saves.TryRestore(
                RoundTrip(save),
                out DungeonGameRestoreReport restoreReport)
            && restoreReport.Success,
            label + " failed: " + string.Join(" | ", restoreReport.Errors));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
// One seasonal-contract witness. It does not rerun the ordinary WIM032/033 matrix.
public sealed class WimWinterContractLiveRunner : MonoBehaviour
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-009-winter-contract-live.txt";
    private readonly List<string> lines = new();
    private DungeonRuntimeLifetimeScope scope;
    private IGameTimeScaleController timeScale;
    private IGameClock clock;
    private ICharacterAiWorldRegistry world;
    private IWorldItemStackRuntime items;
    private V20CampaignRuntime campaign;
    private IDungeonGameSaveService saves;
    private string workerId;
    private string factionId;

    public static string StartFocused()
    {
        Require(Application.isPlaying && FindFirstObjectByType<WimWinterContractLiveRunner>() == null,
            "Start once inside a fresh protected main Play session.");
        var current = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(current?.Container != null && FindFirstObjectByType<OwnerRunManager>() != null,
            "Main scope/owner preparation is unavailable.");
        var persistence = current.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "User-save isolation is unavailable.");
        persistence.Dispose();
        current.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        FindFirstObjectByType<GameManager>().isPause = true;
        current.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        new GameObject("WIM009 Winter Physical Contract Witness").AddComponent<WimWinterContractLiveRunner>();
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Start()
    {
        // Flatten nested setup iterators so a preparation exception cannot leave
        // a RUNNING report or an unobserved Unity coroutine failure.
        var iterators = new Stack<IEnumerator>();
        iterators.Push(Run());
        Exception failure = null;
        while (iterators.Count > 0)
        {
            object current = null;
            bool moved;
            try { moved = iterators.Peek().MoveNext(); if (moved) current = iterators.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (iterators.Pop() as IDisposable)?.Dispose(); continue; }
            if (current is IEnumerator nested) iterators.Push(nested);
            else yield return current;
        }
        while (iterators.Count > 0) (iterators.Pop() as IDisposable)?.Dispose();
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add("cleanup=operator stops disposable protected Play; no scene/profile/save file writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        Destroy(gameObject);
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        clock = scope.Container.Resolve<IGameClock>();
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        campaign = scope.Container.Resolve<V20CampaignRuntime>();
        saves = scope.Container.Resolve<IDungeonGameSaveService>();
        var runFlowSubscriptions = scope.Container.Resolve<IDungeonRunFlowRuntime>() as IDisposable;
        Require(runFlowSubscriptions != null, "Run-flow subscription isolation is unavailable.");
        // The synthetic winter day must reach the actual seasonal subscriber,
        // not schedule six unrelated boss cycles. Disposal only detaches this
        // adapter's event subscriptions; its state/restore participants remain.
        runFlowSubscriptions.Dispose();
        var invasion = FindFirstObjectByType<InvasionDirectorRuntime>();
        Require(invasion != null && invasion.ActiveIntruders.Count == 0,
            "Winter witness requires a fresh world without an already-owned invasion.");
        // Stop the unrelated candidate producer, not its consumer: disabling
        // only the director would leave a raised-but-unacknowledged candidate
        // and invalidate whole-world save preflight.
        var threat = FindFirstObjectByType<InvasionThreatRuntime>();
        Require(threat != null && !threat.IsCandidatePending
            && !threat.CapturePersistentState().CandidateRaisedThisCycle,
            "Winter witness requires a fresh threat cycle before isolation.");
        threat.enabled = false;
        lines.Add("isolation=run-flow event subscriptions disposed and invasion threat producer disabled before synthetic winter in disposable Play; actual seasonal daily bus retained; director/state/restore participants unchanged");
        var owner = FindFirstObjectByType<OwnerRunManager>();
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(out var expansion, out string expansionFailure)
                    && expansion.CurrentInteriorColumns == 29, expansionFailure);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Normal party UI did not commit a run.");
        }
        Require(world.TryGetGrid(out Grid grid) && grid != null, "Main grid unavailable.");
        var worker = world.Characters.Where(value => value != null && !value.IsOwner && !value.IsDead
                && value.characterType == CharacterType.NPC && value.Brain != null
                && value.GetComponent<AbilityHaul>() != null)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Require(worker != null, "No real staff hauler in the prepared run.");
        workerId = worker.Identity.PersistentId;
        ConfigureHauler();
        var facilities = scope.Container.Resolve<IFacilityCapabilityQuery>();
        if (facilities.FindOperational(FacilityCapabilityKind.Administration).Count == 0)
        {
            var office = V20FactionContractLivePlayModeRunner.PlaceAdministrationFixture(scope, grid,
                scope.Container.Resolve<IRoomLayoutCache>(), scope.Container.Resolve<IGridBuildingObjectFactory>(), out var anchor);
            yield return null;
            Require(facilities.FindOperational(FacilityCapabilityKind.Administration).Any(value => value == office),
                "Authored R07 did not become operational.");
            lines.Add("administration=authored R07 via production building factory at " + anchor);
        }
        Require(scope.Container.Resolve<IWorldDropZoneQuery>().TryGetDeliveryDropoff(out var dropoff),
            "No production delivery dropoff.");
        if (!items.GetAllStacks().Any(value => value.Quantity > 0 && value.ItemId == DurableToolItemRules.AdministrativeSeal))
            Require(items.SpawnUniqueItemAt(DurableToolItemRules.AdministrativeSeal, dropoff,
                WorldItemStackState.Loose, string.Empty, out _), "Could not seed one authored physical seal.");
        var traversal = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(worker));
        var access = scope.Container.Resolve<IGridTraversalAccessQuery>();
        var costs = scope.Container.Resolve<IGridTraversalCostPolicy>();
        var source = grid.GetCells().Select(value => value.Position)
            .Where(position => grid.IsWalkable(position)
                && Mathf.Abs(position.x - dropoff.x) + Mathf.Abs(position.y - dropoff.y) >= 4)
            .OrderBy(position => Mathf.Abs(position.x - dropoff.x) + Mathf.Abs(position.y - dropoff.y))
            .ThenBy(position => position.y).ThenBy(position => position.x)
            .Where(position => grid.SearchPathTo(dropoff, position,
                    cell => access.CanTraverse(grid, cell, traversal, out _), costs, traversal)
                .GetMoveCostTo(position) != int.MaxValue)
            .Select(position => (Vector2Int?)position).FirstOrDefault();
        Require(source.HasValue, "No reachable separated charcoal pickup cell for the actual travel witness.");
        Require(items.SpawnItemAt("material:charcoal", 8, source.Value, WorldItemStackState.Loose,
                string.Empty, out int spawned) && spawned == 8, "Could not seed exactly eight real charcoal.");
        var sourceStackIds = items.GetAllStacks().Where(value => value.ItemId == "material:charcoal"
                && value.Position == source.Value && value.State == WorldItemStackState.Loose)
            .Select(value => value.StackId).ToArray();
        Require(sourceStackIds.Length > 0 && sourceStackIds.All(id => items.SetForbidden(id, true)),
            "Could not hold fixture charcoal against unrelated pre-contract warehouse hauling.");
        lines.Add("charcoal pickup=" + source.Value + "; destination=" + dropoff + "; real travel required");
        int charcoalBefore = Charcoal();

        var catalog = scope.Container.Resolve<V20StoryContentCatalog>();
        var seasonal = campaign.CaptureSeasonal();
        Require(seasonal.cycle == 0 && seasonal.lastEvaluationAbsoluteDay < 91
                && seasonal.activeEvents.All(value => value.deadlineAbsoluteDay < 91),
            "Fresh fixture is not eligible for its controlled first winter day.");
        seasonal.completedEventIds = catalog.SeasonalEvents
            .Where(value => value.StableId != "seasonal:winter-fuel-demand"
                && !seasonal.activeEvents.Any(active => active.definitionId == value.StableId))
            .Select(value => value.StableId).ToList();
        campaign.PublishSeasonal(campaign.PrepareSeasonal(seasonal));
        var calendar = scope.Container.Resolve<IGameCalendar>();
        calendar.SetDateTime(91, 8);
        scope.Container.Resolve<IGameEventBus>().Publish(new OperatingDayStartedEvent(91));
        var daily = scope.Container.Resolve<IV20DailyEvaluationDiagnostic>();
        Require(daily.LastDailyEvaluationAbsoluteDay == 91 && daily.LastDailyEvaluationSucceeded,
            "Main daily dispatcher rejected the controlled winter day: " + daily.LastDailyEvaluationFailure);
        var occurrence = campaign.ActiveSeasonalEvents.Single(value => value.definitionId == "seasonal:winter-fuel-demand");
        var contract = catalog.Contracts.Single(value => value.seasonalEventId == occurrence.definitionId
            && value.factionId == occurrence.contextFactionId);
        factionId = contract.factionId;
        int rapportBefore = State().rapport;
        int obligationBefore = State().obligationTokens;
        int grievanceBefore = State().grievance;
        Require(Charcoal() == charcoalBefore && string.IsNullOrEmpty(State().activeContractId),
            "Unaccepted seasonal offer consumed stock or accepted a contract.");
        var notices = FindFirstObjectByType<EventAlertRuntime>();
        var notice = notices.EventLog.Single(value => value.SourceId == occurrence.instanceId);
        Require(notice.Choices.Count == 1 && notice.Choices[0].ActionId ==
            V21ContentAlertActionIds.SeasonalFactionContractAccept(occurrence.instanceId, contract.StableId),
            "Actual daily producer did not publish the occurrence-bound acceptance action.");
        Click("EventAlertButton_" + notice.Id);
        Require(notices.IsDetailVisible && notices.SelectedRecord?.Id == notice.Id, "Notice pointer did not open detail.");
        notices.CloseDetail();
        Require(Charcoal() == charcoalBefore && State().rapport == rapportBefore && string.IsNullOrEmpty(State().activeContractId),
            "Closing an unaccepted offer incurred a cost.");
        Click("EventAlertButton_" + notice.Id);
        Click("EventChoice_1");
        yield return null;
        Require(string.IsNullOrEmpty(State().activeContractId) && Charcoal() == charcoalBefore,
            "Acceptance bypassed the physical administrative seal gate.");
        lines.Add("winter=controlled day91/other seasonal eligibility; actual main daily dispatch/notice/button/close; unaccepted debit0");

        var slots = scope.Container.Resolve<IDurableFacilityEquipmentSlotQuery>();
        Resume();
        float wallStart = Time.realtimeSinceStartup, gameStart = clock.Time;
        while (!slots.CaptureAll().Any(value => value.PolicyId == RunAdministrativeSealDurableEquipmentPolicySource.PolicyId
                   && value.SupplyReady) && Time.realtimeSinceStartup - wallStart < 60f && clock.Time - gameStart < 160f)
            yield return null;
        Pause();
        Require(slots.CaptureAll().Any(value => value.PolicyId == RunAdministrativeSealDurableEquipmentPolicySource.PolicyId
            && value.SupplyReady), "Actual hauler did not deliver the administrative seal in the bounded window.");
        Require(notices.IsDetailVisible, "Pending seal request lost its retry UI.");
        Click("EventChoice_1");
        yield return null;
        Require(State().activeContractId == contract.StableId && State().activeContractOccurrenceId == occurrence.instanceId
                && State().activeContractDeadlineAbsoluteDay == occurrence.deadlineAbsoluteDay
                && Charcoal() == charcoalBefore && State().rapport == rapportBefore && State().obligationTokens == obligationBefore,
            "Actual choice acceptance lost occurrence/deadline or rewarded before delivery.");
        lines.Add("accept=real seal AI delivery then actual choice retry; no charcoal/reward on acceptance; deadline=" + occurrence.deadlineAbsoluteDay);
        Require(sourceStackIds.All(id => items.GetAllStacks().Any(value => value.StackId == id
                && value.Position == source.Value && value.State == WorldItemStackState.Loose && value.Forbidden))
            && sourceStackIds.All(id => items.SetForbidden(id, false)),
            "Charcoal moved during seal preparation or failed the ordinary un-forbid command.");
        lines.Add("source release=ordinary forbidden toggle after acceptance; no pre-contract relocation/quantity mutation");

        var transfers = scope.Container.Resolve<IPhysicalFacilityItemBatchTransferGateway>();
        string operationId = FactionContractDeliveryOutbox.FormatOperationId(contract.StableId, occurrence.instanceId);
        ConfigureHauler();
        Resume();
        wallStart = Time.realtimeSinceStartup; gameStart = clock.Time;
        bool carried = false, arrived = false;
        while (State().deliveryCommitPhase != FactionContractDeliveryCommitPhase.PhysicalCommitted
            && Time.realtimeSinceStartup - wallStart < 90f && clock.Time - gameStart < 200f)
        {
            var activeWorker = world.Characters.Single(value => value != null && value.Identity.PersistentId == workerId);
            Require(activeWorker.CurrentLifecycleState == CharacterLifecycleState.Active,
                "Unrelated lifecycle transition invalidated winter haul timing: " + activeWorker.CurrentLifecycleState);
            var charcoal = items.GetAllStacks().Where(value => value.ItemId == "material:charcoal");
            // Physical carried custody belongs to actorId, while the delivery
            // intent retains the contract destination. Do not compare unlike
            // physical-owner and logical-destination identifiers.
            carried |= charcoal.Any(value => value.State == WorldItemStackState.Carried && value.DestinationId == workerId)
                && items.CaptureHaulDeliveryIntentsByDestination(State().activeContractDestinationId).Count > 0;
            arrived |= charcoal.Any(value => value.State == WorldItemStackState.FacilityBuffer
                && value.DestinationId == State().activeContractDestinationId);
            yield return null;
        }
        Pause();
        var observedWorker = world.Characters.Single(value => value != null && value.Identity.PersistentId == workerId);
        Require(invasion.ActiveIntruders.Count == 0, "Unrelated invasion bypassed fixture isolation; delivery timing is not attributable.");
        lines.Add("physical-boundary phase=" + State().deliveryCommitPhase + "; failure=" + State().deliveryFailureReason
            + "; charcoal=" + Charcoal() + "; before=" + charcoalBefore + "; rapport=" + State().rapport
            + "; gameSeconds=" + (clock.Time - gameStart) + "; wallSeconds=" + (Time.realtimeSinceStartup - wallStart)
            + "; day=" + calendar.Day + "; workerAction=" + observedWorker.Brain.bestAction?.actionset?.Branch
            + "; phase=" + observedWorker.Brain.CurrentActionPhase + "; failure=" + observedWorker.Brain.LastActionFailure
            + "; hauls=" + observedWorker.GetComponent<AbilityHaul>().IsHauling
            + "; stacks=" + string.Join("|", items.GetAllStacks().Where(value => value.ItemId == "material:charcoal")
                .Select(value => value.StackId + ":" + value.Quantity + ":" + value.State + ":" + value.DestinationId)));
        Require(State().deliveryCommitPhase == FactionContractDeliveryCommitPhase.PhysicalCommitted
                && transfers.TryGetPending(operationId, out var receipt)
                && receipt.Quantity == 8 && FactionContractDeliveryOutbox.ReceiptMatchesSaved(State(), receipt)
                && Charcoal() == charcoalBefore - 8 && State().rapport == rapportBefore,
            "Real haul did not reach exact eight-charcoal physical commit before reward.");
        Require(carried, "No actual carried charcoal was observed; physical AI delivery is unproven.");
        lines.Add("delivery=actual AI carried/physical Transfer8; buffer-observed=" + arrived
            + "; gameSeconds=" + (clock.Time - gameStart) + "; wallSeconds=" + (Time.realtimeSinceStartup - wallStart));

        var pending = saves.FromJson(saves.ToJson(saves.Capture()));
        var invalid = saves.FromJson(saves.ToJson(pending));
        var incomingSeason = DungeonSaveSectionPayload.ReadOrNew<SeasonalEventWorldSaveData>(invalid, SeasonalWorldEventsSaveSection.Id);
        incomingSeason.activeEvents.RemoveAll(value => value.instanceId == occurrence.instanceId);
        DungeonSaveSectionPayload.Write(invalid, SeasonalWorldEventsSaveSection.Id, SeasonalEventWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.RuntimeState, incomingSeason);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        string beforeFaction = JsonUtility.ToJson(campaign.CaptureFactions());
        string beforeSeason = JsonUtility.ToJson(campaign.CaptureSeasonal());
        string beforePhysical = JsonUtility.ToJson(items.Capture());
        Require(!saves.TryRestore(invalid, out var rejected) && !rejected.Success
                && rejected.Errors.Any(value => value.Contains("Faction active contract is invalid.", StringComparison.Ordinal))
                && beforeFaction == JsonUtility.ToJson(campaign.CaptureFactions())
                && beforeSeason == JsonUtility.ToJson(campaign.CaptureSeasonal())
                && beforePhysical == JsonUtility.ToJson(items.Capture()),
            "Missing incoming occurrence was accepted or mutated live faction/season/physical state: " + string.Join(" | ", rejected.Errors));
        Require(saves.TryRestore(pending, out var restored) && restored.Success,
            "Exact pending whole-world restore failed: " + string.Join(" | ", restored.Errors));
        yield return null;
        Require(State().deliveryCommitPhase == FactionContractDeliveryCommitPhase.PhysicalCommitted
            && transfers.TryGetPending(operationId, out var restoredReceipt)
            && FactionContractDeliveryOutbox.ReceiptMatchesSaved(State(), restoredReceipt)
            && Charcoal() == charcoalBefore - 8, "Whole-world restore lost pending physical/occurrence custody.");
        lines.Add("whole-world=JSON pending receipt + incoming seasonal/faction join; missing occurrence reject/no live mutation; exact restore PASS");
        ConfigureHauler();
        Resume();
        wallStart = Time.realtimeSinceStartup;
        while ((State().deliveryCommitPhase != FactionContractDeliveryCommitPhase.None || !string.IsNullOrEmpty(State().activeContractId))
               && Time.realtimeSinceStartup - wallStart < 15f) yield return null;
        Pause();
        Require(State().deliveryCommitPhase == FactionContractDeliveryCommitPhase.None && !transfers.TryGetPending(operationId, out _)
            && State().rapport == rapportBefore + 8 && State().obligationTokens == obligationBefore + 1
            && State().grievance == grievanceBefore && Charcoal() == charcoalBefore - 8,
            "Restored physical commit did not reward +8/+1 once and ACK its physical receipt.");
        var terminal = saves.FromJson(saves.ToJson(saves.Capture()));
        Require(saves.TryRestore(terminal, out var terminalRestore) && terminalRestore.Success,
            "Terminal whole-world restore failed: " + string.Join(" | ", terminalRestore.Errors));
        yield return null;
        Require(State().rapport == rapportBefore + 8 && State().obligationTokens == obligationBefore + 1
            && Charcoal() == charcoalBefore - 8 && !transfers.TryGetPending(operationId, out _),
            "Terminal restore duplicated reward or physical stock.");
        lines.Add("reward=rapport+8/obligation+1/grievance0 once; physical ACK empty; terminal current world restore exact");
        lines.Add("scope=controlled winter day/eligibility, authored R07+physical stock+healthy staff+typed next-haul preference, unrelated run-flow/invasion scheduling isolated; real main seasonal dispatcher/UI/haul/current full restore; general AI priority/performance/natural season progression/boss scheduling/combined defense/6adult NOT_RUN");
    }

    private FactionCampaignStateSaveData State()
    {
        Require(campaign.TryGetFaction(factionId, out var state), "Fixture faction disappeared.");
        return state;
    }
    private int Charcoal() => items.GetAllStacks().Where(value => value.ItemId == "material:charcoal").Sum(value => value.Quantity);
    private void ConfigureHauler()
    {
        var worker = world.Characters.Single(value => value != null && value.Identity.PersistentId == workerId);
        foreach (var actor in world.Characters.Where(value => value != null)) actor.SetAiPaused(actor != worker);
        foreach (var need in new[] { CharacterCondition.HUNGER, CharacterCondition.THIRST, CharacterCondition.SLEEP,
                     CharacterCondition.HYGIENE, CharacterCondition.EXCRETION, CharacterCondition.FUN })
            worker.Stats.ChangesStat(need, 100f - worker.Stats.GetConditionValue(need, 0f));
        var work = worker.GetComponent<AbilityWork>();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
        Require(worker.Brain.PreferActionOnNextDecision<AIHaul>(),
            "Prepared staff has no authored haul action for the focused physical witness.");
        worker.Brain.RequestImmediateReplan(clearFailures: true);
    }
    private void Pause()
    {
        var game = FindFirstObjectByType<GameManager>();
        if (game != null) game.isPause = true;
        if (timeScale != null) timeScale.Scale = 0f;
    }
    private void Resume() { FindFirstObjectByType<GameManager>().isPause = false; timeScale.Scale = 4f; }
    private static void Click(string name)
    {
        var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(value => value != null
            && value.gameObject.scene.isLoaded && value.gameObject.activeInHierarchy && value.name == name);
        Require(button != null && button.IsInteractable()
            && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual UI button unavailable: " + name);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
