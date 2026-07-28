using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

internal sealed class CropPlotState
{
    public string PlotId = string.Empty;
    public BuildableObject Building;
    public BuildingCropPlotAbility Ability;
    public string CropId = string.Empty;
    public CropPlotPhase Phase;
    public float SowWork;
    public float GrowthHours;
    public float HarvestWork;
    public string MaterialDestinationId = string.Empty;
    public bool MaterialsConsumed;
    public string BlockedReason = string.Empty;
}

public sealed class CropPlotRuntime :
    ICropPlotRuntime,
    IInitializable,
    ITickable,
    IDisposable
{
    private const float SecondsPerGameHour = 7.5f;
    private const float MaterialRequestInterval = 0.5f;
    private const string CompostItemId = "material:compost";

    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionItemGateway items;
    private readonly IGameClock gameClock;
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly IGameDataProvider gameDataProvider;
    private readonly ISurvivalEnvironmentQuery environmentQuery;
    private readonly IFacilityCandidateCache facilityCandidates;
    private readonly IWorkforceReplanService workforce;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly Dictionary<string, CropPlotState> states =
        new Dictionary<string, CropPlotState>(StringComparer.Ordinal);
    private readonly Dictionary<BuildableObject, CropPlotState> statesByBuilding =
        new Dictionary<BuildableObject, CropPlotState>();
    private readonly List<CropPlotSnapshot> snapshots =
        new List<CropPlotSnapshot>();

    private DungeonCropPlotSaveData pendingRestore;
    private int observedBuildingVersion = -1;
    private float nextMaterialRequestTime;
    private bool snapshotsDirty = true;

    public CropPlotRuntime(
        IBuildingWorldQuery buildingWorld,
        IResourceEconomyContentCatalog catalog,
        IProductionItemGateway items,
        IGameClock gameClock,
        IFacilityCandidateCache facilityCandidates,
        IBlueprintResearchRuntimeProvider researchProvider = null,
        IGameDataProvider gameDataProvider = null,
        ISurvivalEnvironmentQuery environmentQuery = null,
        IWorkforceReplanService workforce = null,
        IGrandProjectBenefitQuery grandProjectBenefits = null)
    {
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.facilityCandidates = facilityCandidates
            ?? throw new ArgumentNullException(nameof(facilityCandidates));
        this.researchProvider = researchProvider;
        this.gameDataProvider = gameDataProvider;
        this.environmentQuery = environmentQuery;
        this.workforce = workforce;
        this.grandProjectBenefits = grandProjectBenefits;
    }

    public int Version { get; private set; }

    public IReadOnlyList<CropPlotSnapshot> Plots
    {
        get
        {
            RefreshSnapshots();
            return snapshots;
        }
    }

    public void CopyVisualStates(List<CropPlotVisualState> destination)
    {
        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        destination.Clear();
        foreach (CropPlotState state in states.Values)
        {
            if (state?.Building == null
                || state.Building.isDestroy
                || !catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
            {
                continue;
            }

            destination.Add(new CropPlotVisualState(
                state.PlotId,
                state.Building,
                state.CropId,
                state.Phase,
                crop.GrowthHours <= 0f
                    ? 0f
                    : state.GrowthHours / crop.GrowthHours));
        }
    }

    public void Initialize()
    {
        SynchronizePlots(force: true);
    }

    public void Tick()
    {
        SynchronizePlots(force: false);
        bool requestMaterials = gameClock.Time >= nextMaterialRequestTime;
        if (requestMaterials)
        {
            nextMaterialRequestTime =
                gameClock.Time + MaterialRequestInterval;
        }

        foreach (CropPlotState state in states.Values)
        {
            TickState(state, requestMaterials);
        }
    }

    public void Dispose()
    {
        foreach (CropPlotState state in states.Values)
        {
            ReleaseMaterialDestination(state);
        }

        states.Clear();
        statesByBuilding.Clear();
        snapshots.Clear();
    }

    public bool TrySetCrop(
        BuildableObject plot,
        string cropId,
        out string message)
    {
        message = string.Empty;
        if (!TryGetState(plot, out CropPlotState state))
        {
            message = "경작지가 아닙니다.";
            return false;
        }

        if (state.MaterialsConsumed
            || state.Phase is CropPlotPhase.Sowing
                or CropPlotPhase.Growing
                or CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting)
        {
            message = "현재 재배 주기가 끝난 뒤 작물을 바꿀 수 있습니다.";
            return false;
        }

        if (!catalog.TryGetCrop(cropId, out CropDefinitionSO crop))
        {
            message = "알 수 없는 작물입니다.";
            return false;
        }

        if (state.Ability.Indoor && !crop.IndoorAllowed)
        {
            message = "이 작물은 실내에서 재배할 수 없습니다.";
            return false;
        }

        if (!IsResearchUnlocked(crop, out message))
        {
            return false;
        }

        ReleaseMaterialDestination(state);
        state.CropId = crop.CropId;
        state.MaterialDestinationId = BuildDestinationId(state.PlotId);
        state.Phase = CropPlotPhase.Empty;
        state.SowWork = 0f;
        state.GrowthHours = 0f;
        state.HarvestWork = 0f;
        state.MaterialsConsumed = false;
        state.BlockedReason = string.Empty;
        MarkChanged();
        message = $"{crop.DisplayName} 재배를 지정했습니다.";
        return true;
    }

    public bool TryGetWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        out CropPlotWorkSnapshot snapshot)
    {
        snapshot = default;
        if (!TryGetState(plot, out CropPlotState state)
            || !catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
        {
            return false;
        }

        if (workTypeId == BuiltInWorkTypeIds.Sow)
        {
            bool available = state.Phase is CropPlotPhase.ReadyToSow
                or CropPlotPhase.Sowing;
            snapshot = new CropPlotWorkSnapshot(
                state.PlotId,
                workTypeId,
                $"{crop.DisplayName} 파종",
                crop.SowWork,
                state.SowWork,
                available,
                available ? string.Empty : ResolveUnavailableReason(state));
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Harvest)
        {
            bool available = state.Phase is CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting;
            snapshot = new CropPlotWorkSnapshot(
                state.PlotId,
                workTypeId,
                $"{crop.DisplayName} 수확",
                crop.HarvestWork,
                state.HarvestWork,
                available,
                available ? string.Empty : ResolveUnavailableReason(state));
            return true;
        }

        return false;
    }

    public bool ApplyWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        float amount,
        out bool cycleCompleted)
    {
        cycleCompleted = false;
        if (amount <= 0f
            || !TryGetState(plot, out CropPlotState state)
            || !catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
        {
            return false;
        }

        if (workTypeId == BuiltInWorkTypeIds.Sow
            && state.Phase is CropPlotPhase.ReadyToSow
                or CropPlotPhase.Sowing)
        {
            state.Phase = CropPlotPhase.Sowing;
            state.SowWork = Mathf.Min(
                crop.SowWork,
                state.SowWork + amount);
            if (state.SowWork + 0.001f >= crop.SowWork)
            {
                state.SowWork = crop.SowWork;
                state.Phase = CropPlotPhase.Growing;
                cycleCompleted = true;
            }

            MarkChanged();
            return true;
        }

        if (workTypeId == BuiltInWorkTypeIds.Harvest
            && state.Phase is CropPlotPhase.ReadyToHarvest
                or CropPlotPhase.Harvesting)
        {
            state.Phase = CropPlotPhase.Harvesting;
            state.HarvestWork = Mathf.Min(
                crop.HarvestWork,
                state.HarvestWork + amount);
            if (state.HarvestWork + 0.001f >= crop.HarvestWork)
            {
                float outputMultiplier = state.Ability != null
                    && state.Ability.Indoor
                        ? grandProjectBenefits?.GetProductionOutputMultiplier(
                            "crop-indoor") ?? 1f
                        : 1f;
                items.SpawnOutput(
                    crop.HarvestItemId,
                    Mathf.Max(
                        1,
                        Mathf.RoundToInt(crop.Yield * outputMultiplier)),
                    state.Building.centerPos);
                ResetForNextCycle(state);
                cycleCompleted = true;
            }

            MarkChanged();
            return true;
        }

        return false;
    }

    public DungeonCropPlotSaveData Capture()
    {
        DungeonCropPlotSaveData data = new DungeonCropPlotSaveData();
        foreach (CropPlotState state in states.Values
                     .OrderBy(entry => entry.PlotId, StringComparer.Ordinal))
        {
            data.plots.Add(new CropPlotSaveData
            {
                plotId = state.PlotId,
                buildingId = state.Building != null ? state.Building.id : 0,
                gridX = state.Building != null ? state.Building.centerPos.x : 0,
                gridY = state.Building != null ? state.Building.centerPos.y : 0,
                cropId = state.CropId,
                phase = state.Phase,
                sowWork = state.SowWork,
                growthHours = state.GrowthHours,
                harvestWork = state.HarvestWork,
                materialDestinationId = state.MaterialDestinationId,
                materialsConsumed = state.MaterialsConsumed
            });
        }

        return data;
    }

    public void Restore(DungeonCropPlotSaveData snapshot)
    {
        pendingRestore = snapshot ?? new DungeonCropPlotSaveData();
        SynchronizePlots(force: true);
        ApplyPendingRestore();
    }

    private void TickState(CropPlotState state, bool requestMaterials)
    {
        if (state?.Building == null
            || state.Building.isDestroy
            || !catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
        {
            return;
        }

        if (!IsResearchUnlocked(crop, out string researchReason))
        {
            SetBlocked(state, researchReason);
            return;
        }

        if (state.Phase == CropPlotPhase.Blocked
            && state.BlockedReason.StartsWith("연구 필요", StringComparison.Ordinal))
        {
            state.Phase = CropPlotPhase.Empty;
            state.BlockedReason = string.Empty;
            MarkChanged();
        }

        if (state.Phase is CropPlotPhase.Empty
            or CropPlotPhase.WaitingForMaterials)
        {
            EnsureSowingMaterials(state, crop, requestMaterials);
            return;
        }

        if (state.Phase != CropPlotPhase.Growing)
        {
            return;
        }

        float multiplier = ResolveGrowthMultiplier(state, crop, out string blockedReason);
        if (multiplier <= 0f)
        {
            if (!string.Equals(state.BlockedReason, blockedReason, StringComparison.Ordinal))
            {
                state.BlockedReason = blockedReason;
                MarkChanged(replan: false);
            }
            return;
        }

        state.BlockedReason = string.Empty;
        float gameHours = gameClock.DeltaTime / SecondsPerGameHour;
        state.GrowthHours = Mathf.Min(
            crop.GrowthHours,
            state.GrowthHours + gameHours * multiplier);
        snapshotsDirty = true;
        if (state.GrowthHours + 0.001f >= crop.GrowthHours)
        {
            state.GrowthHours = crop.GrowthHours;
            state.Phase = CropPlotPhase.ReadyToHarvest;
            MarkChanged();
        }
    }

    private void EnsureSowingMaterials(
        CropPlotState state,
        CropDefinitionSO crop,
        bool requestMaterials)
    {
        Dictionary<string, int> requirements = BuildMaterialRequirements(state, crop);
        if (requirements.Count == 0)
        {
            state.MaterialsConsumed = true;
            state.Phase = CropPlotPhase.ReadyToSow;
            MarkChanged();
            return;
        }

        if (!state.MaterialsConsumed
            && HasDelivered(requirements, state.MaterialDestinationId))
        {
            if (items.ConsumeDelivered(
                    state.MaterialDestinationId,
                    requirements,
                    out string failureReason))
            {
                state.MaterialsConsumed = true;
                state.Phase = CropPlotPhase.ReadyToSow;
                state.BlockedReason = string.Empty;
                MarkChanged();
                return;
            }

            SetBlocked(state, failureReason);
            return;
        }

        if (!requestMaterials)
        {
            state.Phase = CropPlotPhase.WaitingForMaterials;
            snapshotsDirty = true;
            return;
        }

        bool requestedAny = false;
        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            int pending = items.CountPending(
                requirement.Key,
                state.MaterialDestinationId);
            int missing = Mathf.Max(0, requirement.Value - pending);
            if (missing <= 0)
            {
                continue;
            }

            items.RequestDelivery(
                requirement.Key,
                missing,
                state.Building.centerPos,
                state.MaterialDestinationId,
                out int requested,
                out _);
            requestedAny |= requested > 0;
        }

        state.Phase = CropPlotPhase.WaitingForMaterials;
        state.BlockedReason = BuildMaterialWaitReason(
            requirements,
            state.MaterialDestinationId);
        snapshotsDirty = true;
        if (requestedAny)
        {
            items.PrioritizeDestination(state.MaterialDestinationId);
            workforce?.RequestOneHaulerToReplan(forceInterrupt: false);
            MarkChanged(replan: false);
        }
    }

    private Dictionary<string, int> BuildMaterialRequirements(
        CropPlotState state,
        CropDefinitionSO crop)
    {
        Dictionary<string, int> requirements =
            new Dictionary<string, int>(StringComparer.Ordinal);
        float waterRate = state.Ability.WaterMultiplier;
        if (!state.Ability.Indoor
            && environmentQuery?.GetEnvironmentSnapshot().Weather
                is SurvivalWeatherType.Rain or SurvivalWeatherType.Storm)
        {
            waterRate *= 0.5f;
        }

        int water = crop.DailyWater <= 0f
            ? 0
            : Mathf.Max(
                1,
                Mathf.CeilToInt(
                    crop.DailyWater
                    * (crop.GrowthHours / 24f)
                    * waterRate));
        if (water > 0)
        {
            requirements[DungeonItemCatalogSO.StockItemId(StockCategory.Water)] =
                water;
        }

        if (state.Ability.CompostPerCycle > 0)
        {
            requirements[CompostItemId] = state.Ability.CompostPerCycle;
        }

        if (state.Ability.FuelPerCycle > 0)
        {
            requirements[DungeonItemCatalogSO.StockItemId(StockCategory.Fuel)] =
                state.Ability.FuelPerCycle;
        }

        return requirements;
    }

    private float ResolveGrowthMultiplier(
        CropPlotState state,
        CropDefinitionSO crop,
        out string blockedReason)
    {
        blockedReason = string.Empty;
        if (state.Ability.Indoor)
        {
            return state.Ability.GrowthMultiplier;
        }

        SurvivalEnvironmentSnapshot environment =
            environmentQuery?.GetEnvironmentSnapshot()
            ?? new SurvivalEnvironmentSnapshot(
                SurvivalWeatherType.Clear,
                18f,
                0f,
                0f,
                0f);
        Vector2 range = crop.TemperatureRange;
        if (environment.OutdoorTemperature < range.x)
        {
            blockedReason = $"기온이 너무 낮음 ({environment.OutdoorTemperature:0.#}도)";
            return 0f;
        }

        if (environment.OutdoorTemperature > range.y)
        {
            blockedReason = $"기온이 너무 높음 ({environment.OutdoorTemperature:0.#}도)";
            return 0f;
        }

        float weatherMultiplier = environment.Weather switch
        {
            SurvivalWeatherType.Rain => 1.1f,
            SurvivalWeatherType.Fog => 0.85f,
            SurvivalWeatherType.Storm => 0.55f,
            SurvivalWeatherType.HeatWave => 0.9f,
            SurvivalWeatherType.ColdSnap => 0.9f,
            _ => 1f
        };
        float dayMultiplier = 1f;
        if (gameDataProvider != null
            && gameDataProvider.TryGetGameData(out GameData data)
            && data?.timeOfDay?.Value == TimeOfDay.Night)
        {
            dayMultiplier = 0.55f;
        }

        return state.Ability.GrowthMultiplier
            * weatherMultiplier
            * dayMultiplier;
    }

    private void SynchronizePlots(bool force)
    {
        if (!force && observedBuildingVersion == buildingWorld.BuildingVersion)
        {
            return;
        }

        observedBuildingVersion = buildingWorld.BuildingVersion;
        HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
        statesByBuilding.Clear();
        foreach (BuildableObject building in buildingWorld.Buildings)
        {
            BuildingCropPlotAbility ability =
                building?.BuildingData?.GetAbility<BuildingCropPlotAbility>();
            if (building == null
                || building.isDestroy
                || ability == null)
            {
                continue;
            }

            string plotId = BuildPlotId(building);
            seen.Add(plotId);
            if (!states.TryGetValue(plotId, out CropPlotState state))
            {
                state = CreateState(building, ability, plotId);
                states.Add(plotId, state);
            }
            else
            {
                state.Building = building;
                state.Ability = ability;
            }

            statesByBuilding[building] = state;
        }

        foreach (string removedId in states.Keys
                     .Where(id => !seen.Contains(id))
                     .ToArray())
        {
            ReleaseMaterialDestination(states[removedId]);
            states.Remove(removedId);
        }

        ApplyPendingRestore();
        MarkChanged();
    }

    private CropPlotState CreateState(
        BuildableObject building,
        BuildingCropPlotAbility ability,
        string plotId)
    {
        CropDefinitionSO crop = ResolveDefaultCrop(ability);
        return new CropPlotState
        {
            PlotId = plotId,
            Building = building,
            Ability = ability,
            CropId = crop?.CropId ?? string.Empty,
            Phase = crop != null
                ? CropPlotPhase.Empty
                : CropPlotPhase.Blocked,
            MaterialDestinationId = BuildDestinationId(plotId),
            BlockedReason = crop != null
                ? string.Empty
                : "연구가 완료된 재배 작물이 없습니다."
        };
    }

    private CropDefinitionSO ResolveDefaultCrop(BuildingCropPlotAbility ability)
    {
        string preferredId = ability.Indoor
            ? "crop:cave-mushroom"
            : "crop:twilight-grain";
        CropDefinitionSO preferred = catalog.Crops
            .FirstOrDefault(crop => crop != null
                && string.Equals(crop.CropId, preferredId, StringComparison.Ordinal)
                && (!ability.Indoor || crop.IndoorAllowed)
                && IsResearchUnlocked(crop, out _));
        return preferred ?? catalog.Crops
            .FirstOrDefault(crop => crop != null
                && (!ability.Indoor || crop.IndoorAllowed)
                && IsResearchUnlocked(crop, out _));
    }

    private bool TryGetState(
        BuildableObject plot,
        out CropPlotState state)
    {
        state = null;
        if (plot == null)
        {
            return false;
        }

        SynchronizePlots(force: false);
        return statesByBuilding.TryGetValue(plot, out state)
            || states.TryGetValue(BuildPlotId(plot), out state);
    }

    private bool IsResearchUnlocked(
        CropDefinitionSO crop,
        out string reason)
    {
        reason = string.Empty;
        if (crop == null || string.IsNullOrWhiteSpace(crop.RequiredResearchId))
        {
            return true;
        }

        if (researchProvider == null
            || !researchProvider.TryGetRuntime(out BlueprintResearchRuntime runtime)
            || !runtime.State.Projects.IsCompleted(
                new ResearchProjectId(crop.RequiredResearchId)))
        {
            reason = $"연구 필요: {crop.RequiredResearchId}";
            return false;
        }

        return true;
    }

    private void ApplyPendingRestore()
    {
        if (pendingRestore?.plots == null)
        {
            return;
        }

        foreach (CropPlotSaveData saved in pendingRestore.plots)
        {
            if (saved == null
                || !states.TryGetValue(
                    saved.plotId ?? string.Empty,
                    out CropPlotState state)
                || !catalog.TryGetCrop(saved.cropId, out CropDefinitionSO crop)
                || (state.Ability.Indoor && !crop.IndoorAllowed))
            {
                continue;
            }

            state.CropId = crop.CropId;
            state.Phase = saved.phase;
            state.SowWork = Mathf.Clamp(saved.sowWork, 0f, crop.SowWork);
            state.GrowthHours = Mathf.Clamp(
                saved.growthHours,
                0f,
                crop.GrowthHours);
            state.HarvestWork = Mathf.Clamp(
                saved.harvestWork,
                0f,
                crop.HarvestWork);
            state.MaterialDestinationId =
                string.IsNullOrWhiteSpace(saved.materialDestinationId)
                    ? BuildDestinationId(state.PlotId)
                    : saved.materialDestinationId;
            state.MaterialsConsumed = saved.materialsConsumed;
            state.BlockedReason = string.Empty;
        }

        pendingRestore = null;
        MarkChanged();
    }

    private void ResetForNextCycle(CropPlotState state)
    {
        items.RemoveDestination(state.MaterialDestinationId);
        state.Phase = CropPlotPhase.Empty;
        state.SowWork = 0f;
        state.GrowthHours = 0f;
        state.HarvestWork = 0f;
        state.MaterialsConsumed = false;
        state.BlockedReason = string.Empty;
    }

    private void ReleaseMaterialDestination(CropPlotState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.MaterialDestinationId))
        {
            return;
        }

        Vector2Int position = state.Building != null
            ? state.Building.centerPos
            : Vector2Int.zero;
        items.ReleaseDestination(state.MaterialDestinationId, position);
    }

    private void SetBlocked(CropPlotState state, string reason)
    {
        string normalized = string.IsNullOrWhiteSpace(reason)
            ? "작업이 막혔습니다."
            : reason.Trim();
        if (state.Phase == CropPlotPhase.Blocked
            && string.Equals(state.BlockedReason, normalized, StringComparison.Ordinal))
        {
            return;
        }

        state.Phase = CropPlotPhase.Blocked;
        state.BlockedReason = normalized;
        MarkChanged();
    }

    private void MarkChanged(bool replan = true)
    {
        Version++;
        snapshotsDirty = true;
        facilityCandidates.MarkDynamicStateDirty();
        if (replan)
        {
            workforce?.RequestIdleWorkersToReplan();
        }
    }

    private void RefreshSnapshots()
    {
        if (!snapshotsDirty)
        {
            return;
        }

        snapshots.Clear();
        foreach (CropPlotState state in states.Values
                     .OrderBy(entry => entry.PlotId, StringComparer.Ordinal))
        {
            if (!catalog.TryGetCrop(state.CropId, out CropDefinitionSO crop))
            {
                continue;
            }

            Dictionary<string, int> required =
                BuildMaterialRequirements(state, crop);
            Dictionary<string, int> delivered = required.Keys.ToDictionary(
                itemId => itemId,
                itemId => items.CountDelivered(
                    itemId,
                    state.MaterialDestinationId),
                StringComparer.Ordinal);
            snapshots.Add(new CropPlotSnapshot
            {
                PlotId = state.PlotId,
                BuildingId = state.Building != null ? state.Building.id : 0,
                Position = state.Building != null
                    ? state.Building.centerPos
                    : Vector2Int.zero,
                Indoor = state.Ability.Indoor,
                CropId = crop.CropId,
                CropName = crop.DisplayName,
                Phase = state.Phase,
                SowProgress = crop.SowWork <= 0f
                    ? 0f
                    : Mathf.Clamp01(state.SowWork / crop.SowWork),
                GrowthProgress = crop.GrowthHours <= 0f
                    ? 0f
                    : Mathf.Clamp01(state.GrowthHours / crop.GrowthHours),
                HarvestProgress = crop.HarvestWork <= 0f
                    ? 0f
                    : Mathf.Clamp01(state.HarvestWork / crop.HarvestWork),
                MaterialDestinationId = state.MaterialDestinationId,
                RequiredMaterials = required,
                DeliveredMaterials = delivered,
                BlockedReason = state.BlockedReason
            });
        }

        snapshotsDirty = false;
    }

    private bool HasDelivered(
        IReadOnlyDictionary<string, int> requirements,
        string destinationId)
    {
        return requirements.All(requirement =>
            items.CountDelivered(requirement.Key, destinationId)
            >= requirement.Value);
    }

    private string BuildMaterialWaitReason(
        IReadOnlyDictionary<string, int> requirements,
        string destinationId)
    {
        string missing = requirements
            .Where(requirement =>
                items.CountDelivered(requirement.Key, destinationId)
                < requirement.Value)
            .Select(requirement =>
                $"{requirement.Key} "
                + $"{items.CountDelivered(requirement.Key, destinationId)}"
                + $"/{requirement.Value}")
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(missing)
            ? "파종 재료 확인 중"
            : $"파종 재료 운반 대기: {missing}";
    }

    private static string ResolveUnavailableReason(CropPlotState state)
    {
        if (!string.IsNullOrWhiteSpace(state.BlockedReason))
        {
            return state.BlockedReason;
        }

        return state.Phase switch
        {
            CropPlotPhase.Empty => "재배 주기 준비 중",
            CropPlotPhase.WaitingForMaterials => "파종 재료 운반 대기",
            CropPlotPhase.Growing => "작물이 자라는 중",
            CropPlotPhase.ReadyToHarvest => "수확 작업 대기",
            CropPlotPhase.Harvesting => "수확 작업 진행 중",
            _ => "현재 수행할 수 없는 작업"
        };
    }

    private static string BuildPlotId(BuildableObject plot)
    {
        return plot == null
            ? string.Empty
            : $"crop-plot:{plot.id}:{plot.centerPos.x}:{plot.centerPos.y}";
    }

    private static string BuildDestinationId(string plotId)
    {
        return $"crop-materials:{plotId}";
    }
}
