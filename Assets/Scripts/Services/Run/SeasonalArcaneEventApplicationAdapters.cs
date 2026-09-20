using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SeasonalManaLightningApplicationAdapter : ITickable
{
    public const string RiskKeyPrefix = "seasonal:mana-lightning:electrical-risk:";
    private const float Epsilon = 0.001f;

    private readonly IGameClock clock;
    private readonly IGameCalendar calendar;
    private readonly ISeasonalEventQuery seasonal;
    private readonly IPowerInfrastructureQuery power;
    private readonly IBuildingWorldQuery buildings;
    private readonly IEnvironmentalFireCommand fire;

    public SeasonalManaLightningApplicationAdapter(
        IGameClock clock,
        IGameCalendar calendar,
        ISeasonalEventQuery seasonal,
        IPowerInfrastructureQuery power,
        IBuildingWorldQuery buildings,
        IEnvironmentalFireCommand fire)
    {
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.seasonal = seasonal
            ?? throw new ArgumentNullException(nameof(seasonal));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.fire = fire ?? throw new ArgumentNullException(nameof(fire));
    }

    public void Tick()
    {
        if (clock.IsPaused || !calendar.IsRunning)
            return;
        float delta = clock.DeltaTime;
        if (!FiniteNonNegative(delta)
            || calendar.Day < 1
            || !FiniteNonNegative(calendar.ElapsedSeconds))
            throw new InvalidOperationException(
                "Mana-lightning scheduling requires finite nonnegative game time.");
        if (delta <= 0f)
            return;

        double absoluteNow = Math.Max(
            0d,
            (calendar.Day - 1d) * GameSimulationTimeRules.SecondsPerDay
                + calendar.ElapsedSeconds);
        double absoluteBefore = Math.Max(0d, absoluteNow - delta);
        Dictionary<string, BuildableObject> liveById = buildings.Buildings
            .Where(value => value != null
                && value.isActiveAndEnabled
                && !value.isDestroy
                && value.PersistentInstanceId.IsValid)
            .ToDictionary(
                value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal);

        foreach (V20ActiveEventSaveData occurrence in seasonal
                     .ActiveSeasonalEvents
                     .Where(value => value?.seasonalManaLightning?.configured == true
                         && value.deadlineAbsoluteDay >= calendar.Day)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            SeasonalManaLightningSaveData profile =
                occurrence.seasonalManaLightning;
            if (!TryGetCompletedWindowRange(
                    occurrence.startedAbsoluteDay,
                    profile.riskWindowSeconds,
                    absoluteBefore,
                    absoluteNow,
                    out long firstWindow,
                    out long lastWindow))
            {
                continue;
            }
            foreach (PowerNetworkSnapshot network in power.Networks
                         .Where(value => value != null)
                         .OrderBy(value => value.NetworkId, StringComparer.Ordinal))
            {
                if (network.Tripped
                    || !FiniteNonNegative(network.AvailableSourcePerSecond)
                    || network.AvailableSourcePerSecond <= Epsilon)
                    continue;

                (PowerNodeSnapshot Node, BuildableObject Building) chosen =
                    default;
                bool hasChosen = false;
                foreach (PowerNodeSnapshot node in network.Nodes
                             .Where(value => value != null
                                 && value.Powered
                                 && value.ConnectionEnabled
                                 && !value.BreakerTripped
                                 && FiniteNonNegative(value.DemandPerSecond)
                                 && value.DemandPerSecond > Epsilon
                                 && FiniteNonNegative(value.SuppliedFraction)
                                 && value.SuppliedFraction > Epsilon
                                 && FiniteNonNegative(value.Heat)
                                 && FiniteNonNegative(value.Fault)
                                 && value.Heat + Epsilon >= profile.minimumHeat
                                 && value.Fault + Epsilon >= profile.minimumFault)
                             .OrderBy(
                                 value => value.BuildingId.Value,
                                 StringComparer.Ordinal))
                {
                    if (!liveById.TryGetValue(
                            node.BuildingId.Value,
                            out BuildableObject building)
                        || building.BuildingData == null
                        || building.CurrentUserCount <= 0
                        || !profile.eligibleBuildingDefinitionIds.Contains(
                            building.BuildingData.id))
                        continue;
                    BuildingEnvironmentalFireAbility ability =
                        building.BuildingData.GetAbility<
                            BuildingEnvironmentalFireAbility>();
                    if (ability == null
                        || !ability.Accepts(
                            EnvironmentalFireIgnitionKind.ElectricalFault))
                        continue;
                    ability.CreateProfileOrThrow();
                    chosen = (node, building);
                    hasChosen = true;
                    break;
                }
                if (!hasChosen)
                    continue;

                for (long window = firstWindow;
                     window <= lastWindow;
                     window++)
                {
                    TryContributeRisk(
                        occurrence,
                        profile,
                        network,
                        chosen.Node,
                        chosen.Building,
                        window);
                    if (window == long.MaxValue)
                        break;
                }
            }
        }
    }

    internal static bool TryGetCompletedWindowRange(
        int startedAbsoluteDay,
        float riskWindowSeconds,
        double absoluteBefore,
        double absoluteNow,
        out long firstWindow,
        out long lastWindow)
    {
        firstWindow = 0L;
        lastWindow = -1L;
        if (startedAbsoluteDay < 1
            || !FinitePositive(riskWindowSeconds)
            || !FiniteNonNegative(absoluteBefore)
            || !FiniteNonNegative(absoluteNow)
            || absoluteBefore > absoluteNow)
        {
            throw new InvalidOperationException(
                "Mana-lightning window scheduling state is invalid.");
        }

        double occurrenceStartedAt =
            (startedAbsoluteDay - 1d)
            * GameSimulationTimeRules.SecondsPerDay;
        double elapsedBefore = Math.Max(
            0d,
            absoluteBefore - occurrenceStartedAt);
        double elapsedNow = Math.Max(
            0d,
            absoluteNow - occurrenceStartedAt);
        long previousWindow = (long)Math.Floor(
            elapsedBefore / riskWindowSeconds);
        lastWindow = (long)Math.Floor(
            elapsedNow / riskWindowSeconds);
        firstWindow = Math.Max(1L, previousWindow + 1L);
        return firstWindow <= lastWindow;
    }

    private void TryContributeRisk(
        V20ActiveEventSaveData occurrence,
        SeasonalManaLightningSaveData profile,
        PowerNetworkSnapshot network,
        PowerNodeSnapshot node,
        BuildableObject building,
        long window)
    {
        string causeId = string.Concat(
            "seasonal-mana-lightning:",
            occurrence.instanceId,
            ":",
            network.NetworkId,
            ":",
            window);
        float sample = (PersistentEntityId.GetStableHash32(
                RiskKeyPrefix + causeId) & 0x00ffffffu)
            / 16777216f;
        if (sample >= profile.additionalIgnitionChancePerWindow)
            return;
        string evidenceId = string.Concat(
            "active-running-energized-arcane-facility:",
            building.BuildingData.id,
            ":heat=",
            node.Heat.ToString("R", CultureInfo.InvariantCulture),
            ":fault=",
            node.Fault.ToString("R", CultureInfo.InvariantCulture),
            ":chance=",
            profile.additionalIgnitionChancePerWindow.ToString(
                "R",
                CultureInfo.InvariantCulture),
            ":sample=",
            sample.ToString("R", CultureInfo.InvariantCulture));
        EnvironmentalFireIgnitionResult result = fire.TryIgnite(
            new EnvironmentalFireIgnitionRequest(
                causeId,
                EnvironmentalFireIgnitionKind.ElectricalFault,
                occurrence.instanceId,
                new EnvironmentalFireTargetRef(
                    EnvironmentalFireTargetKind.Building,
                    building.PersistentInstanceId.Value),
                profile.ignitionIntensity,
                evidenceId,
                targetDisplayName: FacilityShopService.GetBuildingName(
                    building.BuildingData)));
        if (result.Disposition ==
            EnvironmentalFireIgnitionDisposition.CauseConflict)
            throw new InvalidOperationException(
                $"Mana-lightning window '{causeId}' produced conflicting evidence.");
    }

    private static bool FiniteNonNegative(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

    private static bool FinitePositive(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

    private static bool FiniteNonNegative(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0d;
}

public sealed class SeasonalSpecialExpeditionApplicationAdapter :
    IStartable,
    IDisposable,
    IDungeonSaveRestoreCompletedHook
{
    private const string SitePrefix = "seasonal-expedition:";
    private readonly ISeasonalEventQuery seasonal;
    private readonly IOffenseWorldSimulation world;
    private readonly IOffenseContentCatalog offenseContent;
    private readonly IEncounterCatalog encounters;
    private readonly IItemDefinitionCatalog items;
    private readonly IGameCalendar calendar;
    private readonly IGameEventBus events;
    private IDisposable contentResolvedSubscription;

    public SeasonalSpecialExpeditionApplicationAdapter(
        ISeasonalEventQuery seasonal,
        IOffenseWorldSimulation world,
        IOffenseContentCatalog offenseContent,
        IEncounterCatalog encounters,
        IItemDefinitionCatalog items,
        IGameCalendar calendar,
        IGameEventBus events)
    {
        this.seasonal = seasonal
            ?? throw new ArgumentNullException(nameof(seasonal));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.offenseContent = offenseContent
            ?? throw new ArgumentNullException(nameof(offenseContent));
        this.encounters = encounters
            ?? throw new ArgumentNullException(nameof(encounters));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        contentResolvedSubscription ??=
            events.Subscribe<V20ContentEffectsResolvedEvent>(
                _ => SynchronizeOffers());
        SynchronizeOffers();
    }

    public void Dispose()
    {
        contentResolvedSubscription?.Dispose();
        contentResolvedSubscription = null;
    }

    public void OnRestoreCompleted() => SynchronizeOffers();

    internal void SynchronizeOffers()
    {
        foreach (V20ActiveEventSaveData occurrence in seasonal
                     .ActiveSeasonalEvents
                     .Where(value =>
                         value?.seasonalSpecialExpedition?.configured == true
                         && value.deadlineAbsoluteDay >= calendar.Day)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            string siteId = SitePrefix + occurrence.instanceId;
            SeasonalSpecialExpeditionSaveData profile =
                occurrence.seasonalSpecialExpedition;
            ValidateAuthoredReferences(profile);
            if (world.TryGetSite(siteId, out OffenseWorldSiteStateData existing))
            {
                SeasonalArcaneEventRules.RequireMatchingOffer(
                    existing,
                    occurrence);
                continue;
            }
            OffenseHexTileState tile = SelectTile(occurrence, profile);
            if (tile == null)
                throw new InvalidOperationException(
                    $"Seasonal occurrence '{occurrence.instanceId}' has no legal expedition site.");
            OffenseWorldSiteStateData site = new()
            {
                siteId = siteId,
                archetypeId = profile.siteArchetypeId,
                displayName = profile.title,
                q = tile.q,
                r = tile.r,
                regionId = tile.regionId,
                factionId = profile.factionId,
                state = OffenseWorldSiteState.Revealed,
                fixedBoss = false,
                strength = profile.campaignOrder,
                createdDay = Math.Max(1, occurrence.startedAbsoluteDay),
                expiresDay = checked(occurrence.deadlineAbsoluteDay + 1),
                pressureAxis = StrategicPressureAxis.None,
                pressureAmount = 0f,
                seasonalOffer = CreateOffer(occurrence)
            };
            if (!world.TryRegisterStrategicSite(site))
                throw new InvalidOperationException(
                    $"Seasonal expedition site '{siteId}' could not be registered.");
        }
    }

    private void ValidateAuthoredReferences(
        SeasonalSpecialExpeditionSaveData profile)
    {
        if (!offenseContent.SiteArchetypes.Any(value => value != null
                && string.Equals(
                    value.siteTypeId,
                    profile.siteArchetypeId,
                    StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Unknown seasonal expedition site archetype '{profile.siteArchetypeId}'.");
        OffenseEncounterSO encounter = encounters.Require(
            profile.authoredEncounterId);
        int campaign = EncounterCampaign(encounter.encounterId);
        if (campaign != profile.campaignOrder)
            throw new InvalidOperationException(
                $"Seasonal encounter '{encounter.encounterId}' is not campaign {profile.campaignOrder}.");
        foreach (SeasonalExpeditionPhysicalRewardSaveData reward in
                 profile.physicalRewards)
        {
            ItemDefinitionId itemId = new(reward.itemId);
            if (!itemId.IsValid || !items.TryGet(itemId, out _))
                throw new InvalidOperationException(
                    $"Unknown seasonal expedition reward item '{reward.itemId}'.");
        }
    }

    private OffenseHexTileState SelectTile(
        V20ActiveEventSaveData occurrence,
        SeasonalSpecialExpeditionSaveData profile)
    {
        HashSet<OffenseHexCoord> occupied = world.Sites
            .Where(value => value != null)
            .Select(value => value.Coord)
            .ToHashSet();
        return world.Tiles
            .Where(value => value != null && !value.blocked)
            .Where(value =>
            {
                int distance = world.GetMinimumStepDistance(
                    world.DungeonCoord,
                    value.Coord);
                return distance >= profile.minimumDistanceSteps
                    && distance <= profile.maximumDistanceSteps
                    && !occupied.Contains(value.Coord);
            })
            .OrderBy(value => PersistentEntityId.GetStableHash32(
                $"{occurrence.instanceId}:{value.q}:{value.r}"))
            .ThenBy(value => value.q)
            .ThenBy(value => value.r)
            .FirstOrDefault();
    }

    internal static OffenseSeasonalExpeditionOfferData CreateOffer(
        V20ActiveEventSaveData occurrence)
    {
        SeasonalSpecialExpeditionSaveData profile =
            occurrence.seasonalSpecialExpedition;
        return new OffenseSeasonalExpeditionOfferData
        {
            configured = true,
            occurrenceInstanceId = occurrence.instanceId,
            definitionId = occurrence.definitionId,
            offerDeadlineAbsoluteDay = occurrence.deadlineAbsoluteDay,
            description = profile.description,
            recommendedDanger = profile.recommendedDanger,
            durationSeconds = profile.durationSeconds,
            requiredMembers = profile.requiredMembers,
            recommendedPower = profile.recommendedPower,
            campaignOrder = profile.campaignOrder,
            authoredEncounterId = profile.authoredEncounterId,
            encounterPreviewText = profile.encounterPreviewText,
            encounterRewardPreviewText = profile.encounterRewardPreviewText,
            physicalRewards = profile.physicalRewards.Select(value =>
                    new OffenseSeasonalPhysicalRewardData
                    {
                        itemId = value.itemId,
                        displayLabel = value.displayLabel,
                        exactQuantity = value.exactQuantity
                    })
                .ToList()
        };
    }

    private static int EncounterCampaign(string encounterId)
    {
        string suffix = encounterId?.Trim().Replace(
            "encounter:",
            string.Empty) ?? string.Empty;
        return int.TryParse(suffix, out int number)
            ? Mathf.Clamp((number - 1) / 6 + 1, 1, 6)
            : 0;
    }
}
