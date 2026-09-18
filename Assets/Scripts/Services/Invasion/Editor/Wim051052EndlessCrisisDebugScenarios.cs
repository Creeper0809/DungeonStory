using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEditor;
using UnityEngine;

public static partial class InvasionThreatDebugScenarios
{
    public const string Wim051052ReportPath =
        "Artifacts/QA/wim-implementation/wim-051-052-endless-crisis-focused.txt";

    [MenuItem("DungeonStory/Debug/Invasion/Run WIM-051-052 Endless Crisis Focused")]
    public static void RunWim051052FromMenu()
    {
        string result = RunWim051052Focused();
        if (result.StartsWith("result=PASS", StringComparison.Ordinal))
            Debug.Log(result);
        else
            Debug.LogError(result);
    }

    public static string RunWim051052Focused()
    {
        List<string> lines = new();
        try
        {
            V20StoryContentCatalog catalog = new(
                new ResourceGameContentCatalog(
                    new UnityGameContentRootLoader()));
            Dictionary<EndlessCrisisAxis, CrisisSample> singles =
                FindSingleAxisCoverage(catalog, out CrisisSample compound);

            VerifyAuthoredComposition(catalog, singles, compound, lines);
            VerifyActualNonCombatConsumers(catalog, singles, lines);
            VerifyCombatOwnershipAndRecovery(catalog, singles, lines);
            VerifyAwaitingResponseAndRestore(catalog, singles, lines);
            lines.Insert(0, "result=PASS");
        }
        catch (Exception error)
        {
            lines.Insert(0, "result=FAIL");
            lines.Add(error.ToString());
        }

        lines.Add(
            "scope=production V20CampaignRuntime policy/lifecycle/persistence plus actual campaign climate/logistics/faction and InvasionThreatRuntime combat consumers; controlled EditMode fixture; not a natural multi-day colony recovery burden or UI click witness");
        string directory = Path.GetDirectoryName(Wim051052ReportPath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllLines(Wim051052ReportPath, lines);
        return string.Join("\n", lines);
    }

    private static Dictionary<EndlessCrisisAxis, CrisisSample>
        FindSingleAxisCoverage(
            V20StoryContentCatalog catalog,
            out CrisisSample compound)
    {
        Dictionary<EndlessCrisisAxis, CrisisSample> singles = new();
        compound = null;
        for (int seed = 1; seed <= 4096; seed++)
        {
            V20CampaignRuntime campaign = CreateEndlessCampaign(catalog);
            campaign.ComposeNextEndlessCrisis(10, seed);
            EndlessCrisisSnapshot snapshot = campaign.CurrentEndlessCrisis;
            if (snapshot.Axes.Count == 1)
                singles.TryAdd(
                    snapshot.Axes[0].Axis,
                    new CrisisSample(seed, campaign));
            else if (snapshot.Axes.Count == 2 && compound == null)
                compound = new CrisisSample(seed, campaign);

            if (singles.Count == 5 && compound != null) break;
        }

        Require(singles.Count == 5,
            $"Single-axis seed coverage incomplete: {singles.Count}/5.");
        Require(compound != null,
            "No deterministic compound crisis sample was found.");
        return singles;
    }

    private static void VerifyAuthoredComposition(
        V20StoryContentCatalog catalog,
        IReadOnlyDictionary<EndlessCrisisAxis, CrisisSample> singles,
        CrisisSample compound,
        List<string> lines)
    {
        Require(catalog.EndlessCrisisPolicy.singleAxisProbabilityPercent == 70,
            "Single-axis authored probability is not 70 percent.");
        Require(catalog.EndlessCrisisPolicy.pressureDurationDays == 3
            && catalog.EndlessCrisisPolicy.singleAxisRecoveryDays == 7
            && catalog.EndlessCrisisPolicy.compoundAxisRecoveryDays == 8,
            "Authored pressure/recovery durations drifted from 3/7/8 days.");

        foreach ((EndlessCrisisAxis axis, CrisisSample sample) in singles)
        {
            EndlessCrisisSnapshot snapshot = sample.Campaign.CurrentEndlessCrisis;
            EndlessCrisisAxisPolicy policy =
                catalog.EndlessCrisisPolicy.Require(axis);
            Require(snapshot.Phase == EndlessCrisisLifecyclePhase.Active
                && snapshot.Axes.Count == 1
                && snapshot.Axes[0].Axis == axis
                && Approximately(
                    snapshot.Axes[0].Multiplier,
                    policy.singleAxisMultiplier),
                $"Single-axis crisis projection mismatch for {axis}.");
            string before = JsonUtility.ToJson(
                sample.Campaign.CaptureMilestones());
            sample.Campaign.ComposeNextEndlessCrisis(10, sample.Seed);
            Require(string.Equals(
                    before,
                    JsonUtility.ToJson(sample.Campaign.CaptureMilestones()),
                    StringComparison.Ordinal),
                $"Same-day composition rerolled {axis}.");
        }

        EndlessCrisisSnapshot compoundSnapshot =
            compound.Campaign.CurrentEndlessCrisis;
        Require(compoundSnapshot.IsCompound
            && compoundSnapshot.Axes.Select(value => value.Axis)
                .Distinct().Count() == 2,
            "Compound crisis did not contain two distinct axes.");
        foreach (EndlessCrisisAxisModifierSnapshot modifier in
                 compoundSnapshot.Axes)
        {
            EndlessCrisisAxisPolicy policy =
                catalog.EndlessCrisisPolicy.Require(modifier.Axis);
            Require(Approximately(
                    modifier.Multiplier,
                    policy.compoundAxisMultiplier),
                $"Compound authored multiplier mismatch for {modifier.Axis}.");
            Require(EndlessCrisisAxisPolicy.IsEfficiencyAxis(modifier.Axis)
                    ? modifier.Multiplier > policy.singleAxisMultiplier
                    : modifier.Multiplier < policy.singleAxisMultiplier,
                $"Compound pressure was not weaker than single pressure for {modifier.Axis}.");
        }

        compound.Campaign.AdvanceEndlessCrisis(
            compoundSnapshot.PressureEndAbsoluteDay);
        EndlessCrisisSnapshot compoundRecovery =
            compound.Campaign.CurrentEndlessCrisis;
        Require(compoundRecovery.Phase
                == EndlessCrisisLifecyclePhase.Recovery
            && compoundRecovery.AuthoredRecoveryDays
                == catalog.EndlessCrisisPolicy.compoundAxisRecoveryDays
            && compoundRecovery.RecoveryEndAbsoluteDay
                == compoundSnapshot.PressureEndAbsoluteDay
                    + catalog.EndlessCrisisPolicy.compoundAxisRecoveryDays,
            "Compound crisis did not enter its authored eight-day recovery.");

        lines.Add(
            "PASS authored 70/30 policy; all five deterministic single axes and one distinct two-axis compound sampled; exact 3/7/8 lifetime including compound recovery state; same-day no reroll; compound per-axis pressure weaker than single");
    }

    private static void VerifyActualNonCombatConsumers(
        V20StoryContentCatalog catalog,
        IReadOnlyDictionary<EndlessCrisisAxis, CrisisSample> singles,
        List<string> lines)
    {
        CrisisSample climate = singles[EndlessCrisisAxis.Climate];
        float climateExpected = catalog.EndlessCrisisPolicy
            .Require(EndlessCrisisAxis.Climate).singleAxisMultiplier;
        Require(Approximately(
                climate.Campaign.GetWorldWaterRegenerationMultiplier(),
                climateExpected),
            "Climate axis did not reach world water regeneration.");

        CrisisSample logistics = singles[EndlessCrisisAxis.Logistics];
        float logisticsExpected = catalog.EndlessCrisisPolicy
            .Require(EndlessCrisisAxis.Logistics).singleAxisMultiplier;
        Require(Approximately(
                logistics.Campaign.GetWorkSpeedMultiplier(
                    BuiltInWorkTypeIds.Haul),
                logisticsExpected),
            "Logistics axis did not reach haul work speed.");

        CrisisSample faction = singles[EndlessCrisisAxis.Faction];
        IFactionCampaignQuery factions = faction.Campaign;
        FactionCampaignStateSaveData target = factions.Factions.First();
        int before = target.rapport;
        float factionMultiplier = catalog.EndlessCrisisPolicy
            .Require(EndlessCrisisAxis.Faction).singleAxisMultiplier;
        faction.Campaign.ApplyFactionChange(
            target.factionId,
            10,
            0,
            0);
        Require(factions.TryGetFaction(
                target.factionId,
                out FactionCampaignStateSaveData after)
            && after.rapport == before + Math.Max(
                1,
                (int)Math.Round(
                    10 * factionMultiplier,
                    MidpointRounding.AwayFromZero)),
            "Faction axis did not reach positive rapport changes.");

        lines.Add(
            "PASS actual campaign consumers: climate water regeneration, logistics haul work speed, and positive faction rapport consume their owned single-axis modifiers");
    }

    private static void VerifyCombatOwnershipAndRecovery(
        V20StoryContentCatalog catalog,
        IReadOnlyDictionary<EndlessCrisisAxis, CrisisSample> singles,
        List<string> lines)
    {
        CrisisSample sample = singles[EndlessCrisisAxis.Combat];
        V20CampaignRuntime campaign = sample.Campaign;
        EndlessCrisisSnapshot crisis = campaign.CurrentEndlessCrisis;
        string combatOwner = crisis.Axes.Single().EffectOwnerId;
        string beforeInvalid = JsonUtility.ToJson(campaign.CaptureMilestones());
        Require(!campaign.TryOwnInvasionCandidate(string.Empty)
            && !campaign.TryOwnInvasionCandidate(" wrong-owner ")
            && string.Equals(
                beforeInvalid,
                JsonUtility.ToJson(campaign.CaptureMilestones()),
                StringComparison.Ordinal),
            "Invalid combat ownership mutated campaign state.");

        FixedCalendar calendar = new();
        calendar.SetDateTime(crisis.StartedAbsoluteDay, 0);
        GameObject root = new("InvasionThreatRuntime_WIM051052");
        try
        {
            IGameEventBus bus = new GameEventBus();
            InvasionThreatRuntime runtime =
                root.AddComponent<InvasionThreatRuntime>();
            runtime.Construct(
                new FixedWorldSampler(),
                new NeutralRunVariableReader(),
                new NeutralMetaProgressionReader(),
                new UnityGameClock(),
                bus,
                new RandomStreamProvider(51052),
                worldThreatModifiers: null,
                experiencePacing: null,
                aggregateStateStore: new InvasionAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                endlessCrisis: campaign,
                endlessCrisisCommands: campaign,
                calendar: calendar);
            ConfigureCrisisRise(runtime);
            runtime.Tick(1f);
            float expectedRise = 10f * catalog.EndlessCrisisPolicy
                .Require(EndlessCrisisAxis.Combat).singleAxisMultiplier;
            Require(Approximately(runtime.CurrentThreat, expectedRise),
                "Combat axis did not reach actual invasion threat rise.");

            Require(runtime.ForceCandidateNow(),
                "Actual threat runtime did not publish a forced candidate.");
            InvasionThreatEndlessCrisisOwnershipState candidate =
                runtime.CaptureEndlessCrisisOwnership();
            Require(string.Equals(
                    candidate.CandidateEffectOwnerId,
                    combatOwner,
                    StringComparison.Ordinal),
                "Threat candidate did not own the combat effect.");

            const string runtimeId = "invasion-runtime:wim051052:owned";
            runtime.OnTriggerEvent(new InvasionStartedEvent(
                runtimeId,
                runtime.LatestSnapshot));
            InvasionThreatEndlessCrisisOwnershipState active =
                runtime.CaptureEndlessCrisisOwnership();
            Require(active.CandidateEffectOwnerId.Length == 0
                && active.DirectResponseOwnerId == combatOwner + ":invasion"
                && active.DirectResponseRuntimeId == runtimeId,
                "Started invasion did not transfer exact owner/runtime identity.");

            runtime.OnTriggerEvent(new InvasionResolvedEvent(
                "invasion-runtime:wim051052:unrelated",
                true,
                3f));
            Require(runtime.CaptureEndlessCrisisOwnership()
                    .DirectResponseRuntimeId == runtimeId
                && campaign.CurrentEndlessCrisis.DirectResponseOwnerId
                    == combatOwner + ":invasion",
                "Unrelated invasion resolution cleared direct response ownership.");

            calendar.SetDateTime(crisis.StartedAbsoluteDay + 1, 0);
            runtime.OnTriggerEvent(new InvasionResolvedEvent(
                runtimeId,
                false,
                2f));
            Require(runtime.CaptureEndlessCrisisOwnership()
                    .DirectResponseRuntimeId.Length == 0
                && campaign.CurrentEndlessCrisis.Phase
                    == EndlessCrisisLifecyclePhase.Active
                && campaign.CurrentEndlessCrisis.LastActualEndAbsoluteDay
                    == crisis.StartedAbsoluteDay + 1
                && Approximately(
                    runtime.CapturePersistentState().ResidualRisk,
                    4f),
                "Matching early invasion result did not persist without prematurely ending pressure.");

            campaign.AdvanceEndlessCrisis(crisis.PressureEndAbsoluteDay);
            EndlessCrisisSnapshot recovery = campaign.CurrentEndlessCrisis;
            Require(recovery.Phase == EndlessCrisisLifecyclePhase.Recovery
                && recovery.RecoveryEndAbsoluteDay
                    == crisis.PressureEndAbsoluteDay
                        + catalog.EndlessCrisisPolicy.singleAxisRecoveryDays
                && Approximately(
                    runtime.CapturePersistentState().ResidualRisk,
                    4f),
                "Pressure end did not enter authored recovery or erased the invasion result.");
            float recoveryThreatBefore = runtime.CurrentThreat;
            runtime.Tick(1f);
            Require(Approximately(
                    runtime.CurrentThreat - recoveryThreatBefore,
                    10f)
                && Approximately(
                    campaign.GetEndlessCrisisMultiplier(
                        EndlessCrisisAxis.Combat),
                    1f),
                "Recovery retained the ended combat pressure modifier.");
            int cycle = campaign.EndlessCycle;
            campaign.ComposeNextEndlessCrisis(
                recovery.RecoveryEndAbsoluteDay - 1,
                sample.Seed + 9000);
            Require(campaign.EndlessCycle == cycle
                && campaign.CurrentEndlessCrisis.Phase
                    == EndlessCrisisLifecyclePhase.Recovery,
                "Recovery allowed an overlapping or overdue crisis.");

            campaign.AdvanceEndlessCrisis(recovery.RecoveryEndAbsoluteDay);
            campaign.ComposeNextEndlessCrisis(
                recovery.RecoveryEndAbsoluteDay,
                sample.Seed + 9000);
            string next = JsonUtility.ToJson(campaign.CaptureMilestones());
            campaign.ComposeNextEndlessCrisis(
                recovery.RecoveryEndAbsoluteDay,
                sample.Seed + 9000);
            Require(campaign.EndlessCycle == cycle + 1
                && campaign.CurrentEndlessCrisis.Phase
                    == EndlessCrisisLifecyclePhase.Active
                && string.Equals(
                    next,
                    JsonUtility.ToJson(campaign.CaptureMilestones()),
                    StringComparison.Ordinal),
                "Recovery completion did not permit exactly one new crisis.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            CleanupRuntimeUi();
        }

        lines.Add(
            "PASS actual combat threat multiplier and candidate/start/resolve runtime-ID ownership; unrelated resolution retained owner; early result persisted; pressure then authored recovery; overlap blocked; recovery end allowed exactly one next crisis");
    }

    private static void VerifyAwaitingResponseAndRestore(
        V20StoryContentCatalog catalog,
        IReadOnlyDictionary<EndlessCrisisAxis, CrisisSample> singles,
        List<string> lines)
    {
        CrisisSample source = singles[EndlessCrisisAxis.Combat];
        V20CampaignRuntime campaign = CreateEndlessCampaign(catalog);
        campaign.ComposeNextEndlessCrisis(10, source.Seed);
        EndlessCrisisSnapshot active = campaign.CurrentEndlessCrisis;
        string effectOwner = active.Axes.Single(value =>
            value.Axis == EndlessCrisisAxis.Combat).EffectOwnerId;
        Require(campaign.TryOwnInvasionCandidate(effectOwner),
            "Controlled direct response candidate could not acquire ownership.");
        Require(campaign.TryBeginOwnedInvasion(
                effectOwner,
                active.StartedAbsoluteDay,
                out string responseOwner),
            "Controlled direct response could not begin.");
        campaign.AdvanceEndlessCrisis(active.PressureEndAbsoluteDay);
        Require(campaign.CurrentEndlessCrisis.Phase
                == EndlessCrisisLifecyclePhase.AwaitingDirectResponse
            && campaign.CurrentEndlessCrisis.RecoveryEndAbsoluteDay == -1,
            "Recovery time was consumed while the direct response was active.");

        RunMilestoneWorldSaveData captured = campaign.CaptureMilestones();
        string capturedJson = JsonUtility.ToJson(captured);
        V20CampaignRuntime restored = CreateCampaign(catalog);
        restored.PublishMilestones(restored.PrepareMilestones(
            JsonUtility.FromJson<RunMilestoneWorldSaveData>(capturedJson)));
        Require(string.Equals(
                capturedJson,
                JsonUtility.ToJson(restored.CaptureMilestones()),
                StringComparison.Ordinal),
            "Awaiting direct response did not round-trip exactly.");
        VerifyThreatPersistenceJoin(
            restored,
            responseOwner,
            "invasion-runtime:wim051052:restore");

        string stableBefore = JsonUtility.ToJson(
            restored.CaptureMilestones());
        RunMilestoneWorldSaveData malformed =
            JsonUtility.FromJson<RunMilestoneWorldSaveData>(capturedJson);
        malformed.endlessCrisisDirectResponseOwnerId =
            "endless-crisis:wrong:invasion";
        MustReject(
            () => restored.PrepareMilestones(malformed),
            "Malformed response ownership was accepted.");
        Require(string.Equals(
                stableBefore,
                JsonUtility.ToJson(restored.CaptureMilestones()),
                StringComparison.Ordinal),
            "Rejected milestone restore mutated live state.");

        int resolutionDay = active.PressureEndAbsoluteDay + 2;
        Require(!restored.TryResolveOwnedInvasion(
                "endless-crisis:unrelated:invasion",
                resolutionDay)
            && restored.CurrentEndlessCrisis.Phase
                == EndlessCrisisLifecyclePhase.AwaitingDirectResponse,
            "Unrelated direct response resolved the crisis.");
        Require(restored.TryResolveOwnedInvasion(
                responseOwner,
                resolutionDay),
            "Matching direct response did not resolve after restore.");
        EndlessCrisisSnapshot recovery = restored.CurrentEndlessCrisis;
        Require(recovery.Phase == EndlessCrisisLifecyclePhase.Recovery
            && recovery.LastActualEndAbsoluteDay == resolutionDay
            && recovery.RecoveryEndAbsoluteDay
                == resolutionDay
                    + catalog.EndlessCrisisPolicy.singleAxisRecoveryDays,
            "Post-response recovery was not measured from the actual end day.");

        lines.Add(
            "PASS pressure-end active response enters awaiting with no recovery burn; current-format milestone JSON and threat owner/runtime persistence join round-trip exact; malformed ownership rejected atomically; unrelated owner no-op; matching owner starts recovery from actual resolution day");
    }

    private static void VerifyThreatPersistenceJoin(
        V20CampaignRuntime restoredCampaign,
        string responseOwner,
        string runtimeId)
    {
        FixedCalendar calendar = new();
        calendar.SetDateTime(
            restoredCampaign.CurrentEndlessCrisis.PressureEndAbsoluteDay,
            0);
        GameObject sourceRoot = new("InvasionThreatRuntime_WIM051052_SaveSource");
        GameObject targetRoot = new("InvasionThreatRuntime_WIM051052_SaveTarget");
        try
        {
            InvasionThreatRuntime source = sourceRoot
                .AddComponent<InvasionThreatRuntime>();
            ConstructCrisisThreatRuntime(
                source,
                restoredCampaign,
                calendar,
                51053);
            source.RestoreEndlessCrisisOwnership(
                string.Empty,
                responseOwner,
                runtimeId);
            source.ValidateEndlessCrisisOwnershipJoin();
            InvasionThreatPersistenceState captured =
                source.CapturePersistentState();

            InvasionThreatRuntime target = targetRoot
                .AddComponent<InvasionThreatRuntime>();
            ConstructCrisisThreatRuntime(
                target,
                restoredCampaign,
                calendar,
                51054);
            target.RestorePersistentState(captured);
            target.ValidateEndlessCrisisOwnershipJoin();
            InvasionThreatEndlessCrisisOwnershipState ownership =
                target.CaptureEndlessCrisisOwnership();
            Require(ownership.CandidateEffectOwnerId.Length == 0
                && ownership.DirectResponseOwnerId == responseOwner
                && ownership.DirectResponseRuntimeId == runtimeId,
                "Threat persistence did not preserve the exact response owner/runtime join.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceRoot);
            UnityEngine.Object.DestroyImmediate(targetRoot);
        }
    }

    private static void ConstructCrisisThreatRuntime(
        InvasionThreatRuntime runtime,
        V20CampaignRuntime campaign,
        IGameCalendar calendar,
        int randomSeed)
    {
        runtime.Construct(
            new FixedWorldSampler(),
            new NeutralRunVariableReader(),
            new NeutralMetaProgressionReader(),
            new UnityGameClock(),
            new GameEventBus(),
            new RandomStreamProvider(randomSeed),
            worldThreatModifiers: null,
            experiencePacing: null,
            aggregateStateStore: new InvasionAggregateStateStore(
                new DungeonRuntimeAggregateRootStore()),
            endlessCrisis: campaign,
            endlessCrisisCommands: campaign,
            calendar: calendar);
    }

    private static V20CampaignRuntime CreateEndlessCampaign(
        V20StoryContentCatalog catalog)
    {
        V20CampaignRuntime campaign = CreateCampaign(catalog);
        RunMilestoneWorldSaveData milestones = campaign.CaptureMilestones();
        milestones.phase = RunProgressionPhase.EndlessAge;
        campaign.PublishMilestones(campaign.PrepareMilestones(milestones));
        return campaign;
    }

    private static V20CampaignRuntime CreateCampaign(
        V20StoryContentCatalog catalog) =>
        new(new DungeonRuntimeAggregateRootStore(), catalog);

    private static void ConfigureCrisisRise(InvasionThreatRuntime runtime)
    {
        runtime.Settings.difficulty = InvasionThreatDifficulty.Normal;
        runtime.Settings.normalMultiplier = 1f;
        runtime.Settings.warningThreshold = 70f;
        runtime.Settings.candidateThreshold = 100f;
        runtime.Settings.warningCooldownSeconds = 0f;
        runtime.Settings.initialSafetyDurationSeconds = 0f;
        runtime.Settings.safetyDurationSeconds = 0f;
        runtime.Settings.minCandidateDelaySeconds = 0f;
        runtime.Settings.maxCandidateDelaySeconds = 0f;
        runtime.Settings.baseRisePerSecond = 10f;
        runtime.Settings.dungeonValueRiseWeight = 0f;
        runtime.Settings.reputationRiseWeight = 0f;
        runtime.Settings.timeRiseWeight = 0f;
        runtime.Settings.riskRiseWeight = 0f;
    }

    private static bool Approximately(float left, float right) =>
        Math.Abs(left - right) <= 0.000001f;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void MustReject(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private sealed class CrisisSample
    {
        public CrisisSample(int seed, V20CampaignRuntime campaign)
        {
            Seed = seed;
            Campaign = campaign;
        }

        public int Seed { get; }
        public V20CampaignRuntime Campaign { get; }
    }
}
