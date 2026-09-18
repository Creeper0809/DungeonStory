#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class Wim009ArcaneSeasonalDebugScenarios
{
    private const string ManaPath =
        "Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_summer-mana-lightning.asset";
    private const string EchoPath =
        "Assets/Resources/SO/V20/World/SeasonalEvents/seasonal_winter-deep-echo.asset";

    [MenuItem("DungeonStory/Debug/WIM-009 Arcane Seasonal Contracts")]
    public static void Run()
    {
        SeasonalWorldEventDefinitionSO mana = RequireAsset(ManaPath);
        SeasonalWorldEventDefinitionSO echo = RequireAsset(EchoPath);
        Require(!mana.ValidateDefinition().Any(),
            string.Join(" | ", mana.ValidateDefinition()));
        Require(!echo.ValidateDefinition().Any(),
            string.Join(" | ", echo.ValidateDefinition()));
        Require(mana.minimumDurationDays == 1
                && mana.maximumDurationDays == 3
                && mana.startEffects.Count == 0
                && mana.dailyEffects.Count == 0
                && mana.endEffects.Count == 0
                && mana.manaLightningProfile.eligibleBuildingDefinitionIds
                    .SequenceEqual(new[] { 9825 })
                && mana.manaLightningProfile.minimumHeat == 80f
                && mana.manaLightningProfile.minimumFault == 4f
                && mana.specialExpeditionProfile.authoredEncounterId
                    == "encounter:09"
                && mana.specialExpeditionProfile.physicalRewards.Count == 1
                && mana.specialExpeditionProfile.physicalRewards[0].itemId
                    == "resource:mana-crystal"
                && mana.specialExpeditionProfile.physicalRewards[0]
                    .exactQuantity == 3,
            "Mana lightning authoring drifted from its facility/expedition contract.");
        Require(echo.minimumDurationDays == 1
                && echo.maximumDurationDays == 2
                && echo.startEffects.Count == 0
                && echo.dailyEffects.Count == 0
                && echo.endEffects.Count == 0
                && !echo.affectedDomainIds.Contains("wildlife")
                && echo.wildlifeArrivalProfile.IsConfigured == false
                && echo.specialExpeditionProfile.authoredEncounterId
                    == "encounter:31"
                && echo.specialExpeditionProfile.encounterPreviewText.Contains(
                    "진실 봉인수호자")
                && echo.specialExpeditionProfile.encounterPreviewText.Contains(
                    "진실 무효감시자")
                && !echo.specialExpeditionProfile.encounterPreviewText.Contains(
                    "기록집행자"),
            "Deep echo authoring drifted from its opt-in truth-guardian contract.");

        V20ActiveEventSaveData occurrence = new()
        {
            instanceId = "event:120:seasonal:winter-deep-echo:1234ABCD",
            definitionId = echo.StableId,
            startedAbsoluteDay = 120,
            deadlineAbsoluteDay = 122,
            seasonalManaLightning =
                SeasonalManaLightningSaveData.FromProfile(
                    echo.manaLightningProfile),
            seasonalSpecialExpedition =
                SeasonalSpecialExpeditionSaveData.FromProfile(
                    echo.specialExpeditionProfile)
        };
        V20ActiveEventSaveData restored = JsonUtility.FromJson<
            V20ActiveEventSaveData>(JsonUtility.ToJson(occurrence));
        SeasonalArcaneEventRules.RequireValidFrozenState(
            restored.seasonalManaLightning,
            echo.manaLightningProfile,
            restored.instanceId);
        SeasonalArcaneEventRules.RequireValidFrozenState(
            restored.seasonalSpecialExpedition,
            echo.specialExpeditionProfile,
            restored.instanceId);
        Require(restored.seasonalSpecialExpedition.physicalRewards.Count == 2,
            "Frozen deep-echo rewards did not survive current-format JSON round-trip.");
        VerifyOccurrenceRelativeManaWindows();
        VerifyRevealedOfferGuards(restored);

        RecordingItemSink sink = new();
        OffenseRewardPreview reward = new(
            "마나 결정",
            3,
            new OffensePhysicalItemRewardSpec("resource:mana-crystal"));
        OffenseRewardGrantResult grant =
            new OffensePhysicalItemRewardGrantHandler(sink).Grant(
                reward,
                new OffenseRewardContext(),
                selector: null);
        Require(grant.success
                && sink.ItemId == "resource:mana-crystal"
                && sink.Amount == 3
                && grant.physicalItems.Count == 1
                && grant.physicalItems[0].quantity == 3,
            "Exact physical expedition reward did not use the item sink once.");

        string adapterSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Scripts/Services/Run/SeasonalArcaneEventApplicationAdapters.cs"));
        Require(adapterSource.Contains("IEnvironmentalFireCommand")
                && adapterSource.Contains("EnvironmentalFireIgnitionKind.ElectricalFault")
                && adapterSource.Contains("value.Powered")
                && adapterSource.Contains("value.ConnectionEnabled")
                && adapterSource.Contains("!value.BreakerTripped")
                && adapterSource.Contains("value.DemandPerSecond > Epsilon")
                && adapterSource.Contains("value.SuppliedFraction > Epsilon")
                && adapterSource.Contains("network.AvailableSourcePerSecond <= Epsilon")
                && adapterSource.Contains("building.BuildingData.id")
                && adapterSource.Contains("building.CurrentUserCount <= 0")
                && adapterSource.Contains("TryGetCompletedWindowRange")
                && adapterSource.Contains("Math.Max(1L, previousWindow + 1L)")
                && !adapterSource.Contains("BuildingData.name")
                && !adapterSource.Contains("gameObject.name"),
            "Mana-lightning source no longer proves common FIRE routing and ID/state eligibility.");
        string registrationSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Scripts/Services/Infrastructure/Registration/DungeonProgressionOffenseRegistration.cs"));
        Require(CountOccurrences(
                    registrationSource,
                    "Register<OffensePhysicalItemRewardGrantHandler>") == 1
                && CountOccurrences(
                    registrationSource,
                    "RegisterEntryPoint<SeasonalManaLightningApplicationAdapter>") == 1
                && CountOccurrences(
                    registrationSource,
                    "RegisterEntryPoint<SeasonalSpecialExpeditionApplicationAdapter>") == 1,
            "WIM-009 production entry points or physical reward handler are not registered exactly once.");
        string aggregateSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Scripts/Services/Offense/OffenseAggregateSaveValidation.cs"));
        Require(aggregateSource.Contains("SeasonalTargetMatchesOffer")
                && aggregateSource.Contains("OffenseWorldSiteState.Engaged")
                && aggregateSource.Contains("OffenseWorldSiteState.Resolved")
                && aggregateSource.Contains(
                    "invalid active-expedition join count"),
            "Current-format save preflight no longer proves the active seasonal run/site join.");
        string preflightSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Scripts/Services/Infrastructure/Save/DungeonAggregateReferencePreflight.cs"));
        string strategicTargetSource = File.ReadAllText(Path.Combine(
            Application.dataPath,
            "Scripts/Services/Offense/OffenseStrategicExpeditionServices.cs"));
        Require(preflightSource.Contains("ValidateSeasonalOffenseOfferJoins")
                && preflightSource.Contains("SeasonalWorldEventsSaveSection.Id")
                && preflightSource.Contains(
                    "site.expiresDay == occurrence.deadlineAbsoluteDay + 1")
                && strategicTargetSource.Contains("SeasonalLaunchIsCurrent")
                && strategicTargetSource.Contains(
                    "<= site.seasonalOffer.offerDeadlineAbsoluteDay"),
            "Revealed seasonal offer restore/launch guards are missing.");
        Require(SeasonalEventWorldSaveData.CurrentVersion == 5
                && OffenseWorldSaveData.CurrentVersion == 10
                && DungeonOffenseSaveData.CurrentVersion == 5
                && DungeonOffenseAggregateSaveData.CurrentVersion == 6,
            "WIM-009 current-format schema versions drifted.");

        Debug.Log(
            "WIM009_ARCANE_SEASONAL=PASS; mana=running+energized+Heat/Fault+common-fire; "
            + "offers=occurrence-frozen+opt-in+physical-reward; deep-echo=no-wildlife+encounter31; "
            + "current-format=seasonal5/world10/expedition5/aggregate6");
    }

    private static SeasonalWorldEventDefinitionSO RequireAsset(string path) =>
        AssetDatabase.LoadAssetAtPath<SeasonalWorldEventDefinitionSO>(path)
        ?? throw new InvalidOperationException(
            $"Required seasonal asset '{path}' is missing.");

    private static void VerifyOccurrenceRelativeManaWindows()
    {
        const int StartedDay = 120;
        const float WindowSeconds = 30f;
        double startedAt = (StartedDay - 1d)
            * GameSimulationTimeRules.SecondsPerDay;
        Require(!TryGetCompletedWindowRange(
                    StartedDay,
                    WindowSeconds,
                    startedAt - 1d,
                    startedAt,
                    out _,
                    out _)
            && !TryGetCompletedWindowRange(
                    StartedDay,
                    WindowSeconds,
                    startedAt,
                    startedAt + WindowSeconds - 0.001d,
                    out _,
                    out _),
            "Mana lightning sampled on occurrence creation or before one full window.");
        Require(TryGetCompletedWindowRange(
                    StartedDay,
                    WindowSeconds,
                    startedAt + WindowSeconds - 0.001d,
                    startedAt + WindowSeconds,
                    out long first,
                    out long last)
                && first == 1L
                && last == 1L,
            "Mana lightning did not expose its first sample after one full authored window.");
        Require(!TryGetCompletedWindowRange(
                    StartedDay,
                    WindowSeconds,
                    startedAt + WindowSeconds,
                    startedAt + WindowSeconds + 0.001d,
                    out _,
                    out _)
            && TryGetCompletedWindowRange(
                    StartedDay,
                    WindowSeconds,
                    startedAt + WindowSeconds * 2d - 0.001d,
                    startedAt + WindowSeconds * 2d,
                    out first,
                    out last)
            && first == 2L
            && last == 2L,
            "Mana lightning restore/re-entry duplicated or renumbered a completed window.");
    }

    private static void VerifyRevealedOfferGuards(
        V20ActiveEventSaveData occurrence)
    {
        OffenseWorldSiteStateData site = new()
        {
            siteId = "seasonal-expedition:" + occurrence.instanceId,
            state = OffenseWorldSiteState.Revealed,
            createdDay = occurrence.startedAbsoluteDay,
            expiresDay = occurrence.deadlineAbsoluteDay + 1,
            factionId = occurrence.seasonalSpecialExpedition.factionId,
            seasonalOffer = (OffenseSeasonalExpeditionOfferData)
                InvokeRuntimeRule(
                    typeof(SeasonalSpecialExpeditionApplicationAdapter),
                    "CreateOffer",
                    occurrence)
        };
        SeasonalEventWorldSaveData seasonal = new();
        seasonal.activeEvents.Add(occurrence);
        DungeonOffenseAggregateSaveData offense = new();
        offense.world.sites.Add(site);
        GameSessionSaveData session = new()
        {
            absoluteDay = occurrence.deadlineAbsoluteDay
        };
        DungeonGameRestoreReport valid = new();
        ValidateSeasonalOffenseOfferJoins(
            session,
            seasonal,
            offense,
            valid);
        Require(valid.Success,
            string.Join(" | ", valid.Errors));

        session.absoluteDay = occurrence.deadlineAbsoluteDay + 1;
        DungeonGameRestoreReport expired = new();
        ValidateSeasonalOffenseOfferJoins(
            session,
            seasonal,
            offense,
            expired);
        Require(!expired.Success,
            "Expired Revealed seasonal offer passed aggregate preflight.");
        session.absoluteDay = occurrence.deadlineAbsoluteDay;

        site.expiresDay++;
        DungeonGameRestoreReport tampered = new();
        ValidateSeasonalOffenseOfferJoins(
            session,
            seasonal,
            offense,
            tampered);
        Require(!tampered.Success,
            "Tampered seasonal offer expiry passed aggregate preflight.");
        site.expiresDay = occurrence.deadlineAbsoluteDay + 1;

        site.seasonalOffer.recommendedDanger++;
        DungeonGameRestoreReport alteredOffer = new();
        ValidateSeasonalOffenseOfferJoins(
            session,
            seasonal,
            offense,
            alteredOffer);
        Require(!alteredOffer.Success,
            "Tampered seasonal offer payload passed aggregate preflight.");
        site.seasonalOffer.recommendedDanger--;

        site.factionId = "tampered:faction";
        DungeonGameRestoreReport alteredFaction = new();
        ValidateSeasonalOffenseOfferJoins(
            session,
            seasonal,
            offense,
            alteredFaction);
        Require(!alteredFaction.Success,
            "Tampered seasonal offer faction passed aggregate preflight.");
        site.factionId = occurrence.seasonalSpecialExpedition.factionId;

        seasonal.activeEvents.Clear();
        DungeonGameRestoreReport stale = new();
        ValidateSeasonalOffenseOfferJoins(
            session,
            seasonal,
            offense,
            stale);
        Require(!stale.Success,
            "Orphan Revealed seasonal offer passed aggregate preflight.");
        seasonal.activeEvents.Add(occurrence);

        Require(SeasonalLaunchIsCurrent(
                    site,
                    occurrence.deadlineAbsoluteDay)
                && !SeasonalLaunchIsCurrent(
                    site,
                    occurrence.deadlineAbsoluteDay + 1),
            "Seasonal launch freshness did not enforce the inclusive offer deadline.");
    }

    private static bool TryGetCompletedWindowRange(
        int startedAbsoluteDay,
        float riskWindowSeconds,
        double absoluteBefore,
        double absoluteNow,
        out long firstWindow,
        out long lastWindow)
    {
        object[] arguments =
        {
            startedAbsoluteDay,
            riskWindowSeconds,
            absoluteBefore,
            absoluteNow,
            0L,
            -1L
        };
        bool result = (bool)InvokeRuntimeRule(
            typeof(SeasonalManaLightningApplicationAdapter),
            "TryGetCompletedWindowRange",
            arguments);
        firstWindow = (long)arguments[4];
        lastWindow = (long)arguments[5];
        return result;
    }

    private static void ValidateSeasonalOffenseOfferJoins(
        GameSessionSaveData session,
        SeasonalEventWorldSaveData seasonal,
        DungeonOffenseAggregateSaveData offense,
        DungeonGameRestoreReport report) =>
        InvokeRuntimeRule(
            typeof(DungeonAggregateReferencePreflight),
            "ValidateSeasonalOffenseOfferJoins",
            session,
            seasonal,
            offense,
            report);

    private static bool SeasonalLaunchIsCurrent(
        OffenseWorldSiteStateData site,
        int currentAbsoluteDay) =>
        (bool)InvokeRuntimeRule(
            typeof(OffenseStrategicTargetService),
            "SeasonalLaunchIsCurrent",
            site,
            currentAbsoluteDay);

    private static object InvokeRuntimeRule(
        Type owner,
        string methodName,
        params object[] arguments)
    {
        MethodInfo method = owner.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(owner.FullName, methodName);
        return method.Invoke(null, arguments);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static int CountOccurrences(string source, string value) =>
        string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value)
            ? 0
            : source.Split(
                new[] { value },
                StringSplitOptions.None).Length - 1;

    private sealed class RecordingItemSink : IExpeditionRewardItemSink
    {
        public string ItemId { get; private set; } = string.Empty;
        public int Amount { get; private set; }

        public bool SpawnLoot(string itemId, int amount, out int spawned)
        {
            ItemId = itemId;
            Amount += amount;
            spawned = amount;
            return true;
        }

        public bool SpawnStock(
            StockCategory category,
            int amount,
            string sourceLabel,
            out string itemId,
            out int spawned)
        {
            itemId = string.Empty;
            spawned = 0;
            return false;
        }
    }
}
#endif
