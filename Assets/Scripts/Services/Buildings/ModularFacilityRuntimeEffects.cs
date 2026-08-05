using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class ModularFacilityRuntimeEffects
{
    private const string RuntimeLightObjectName = "RoomClippedLight";
    private const float MountedLightLocalY = 2f;
    private const float BuildingLightLocalY = 1.4f;

    public static void ConfigureVisual(BuildableObject building)
    {
        if (building?.BuildingData == null)
        {
            return;
        }

        if (building is ConstructionSite)
        {
            return;
        }

        foreach (IBuildingVisualRuntimeAbility ability in building.BuildingData.Abilities
                     .OfType<IBuildingVisualRuntimeAbility>())
        {
            ability.ConfigureVisual(building);
        }
    }

    public static void ConfigureLighting(BuildableObject building, BuildingLightingAbility lighting)
    {
        if (building == null || lighting == null || !lighting.IsValid)
        {
            return;
        }

        LightingRuntimeConfig config = new LightingRuntimeConfig(
            lighting.intensity,
            lighting.radius,
            lighting.InnerRadiusRatio,
            lighting.Color,
            lighting.FalloffIntensity,
            lighting.GetTargetSortingLayerIds());

        RemoveRootLightIfPresent(building);
        Transform lightTransform = GetOrCreateLightTransform(building);
        lightTransform.localPosition = GetLightLocalPosition(building);
        Light2D light = lightTransform.GetComponent<Light2D>();
        if (light == null)
        {
            light = lightTransform.gameObject.AddComponent<Light2D>();
        }

        light.intensity = config.Intensity;
        light.pointLightInnerRadius = Mathf.Max(0.1f, config.Radius * config.InnerRadiusRatio);
        light.pointLightOuterRadius = Mathf.Max(light.pointLightInnerRadius + 0.1f, config.Radius);
        light.color = config.Color;
        light.falloffIntensity = config.FalloffIntensity;
        light.targetSortingLayers = config.TargetSortingLayers;

        RoomClippedLight2D clippedLight = lightTransform.GetComponent<RoomClippedLight2D>();
        if (clippedLight == null)
        {
            clippedLight = lightTransform.gameObject.AddComponent<RoomClippedLight2D>();
        }

        clippedLight.Configure(building, light, config.Radius);
    }

    private static void RemoveRootLightIfPresent(BuildableObject building)
    {
        Light2D rootLight = building != null ? building.GetComponent<Light2D>() : null;
        if (rootLight == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Object.Destroy(rootLight);
        }
        else
        {
            Object.DestroyImmediate(rootLight);
        }
    }

    private static Transform GetOrCreateLightTransform(BuildableObject building)
    {
        Transform existing = building.transform.Find(RuntimeLightObjectName);
        if (existing != null)
        {
            return existing;
        }

        GameObject lightObject = new GameObject(RuntimeLightObjectName);
        lightObject.transform.SetParent(building.transform, false);
        return lightObject.transform;
    }

    private static Vector3 GetLightLocalPosition(BuildableObject building)
    {
        GridLayer layer = building?.BuildingData != null
            ? building.BuildingData.layer
            : GridLayer.Building;
        float y = layer == GridLayer.WallFixture || layer == GridLayer.CeilingFixture
            ? MountedLightLocalY
            : BuildingLightLocalY;
        return new Vector3(0f, y, 0f);
    }

    private static int[] GetRuntimeLightTargetSortingLayers(BuildingLightingAbility lighting)
    {
        return lighting != null
            ? lighting.GetTargetSortingLayerIds()
            : BuildingLightingSettingsSO.DefaultTargetSortingLayers
                .Select(SortingLayer.NameToID)
                .Where(SortingLayer.IsValid)
                .ToArray();
    }

    private readonly struct LightingRuntimeConfig
    {
        public LightingRuntimeConfig(
            float intensity,
            float radius,
            float innerRadiusRatio,
            Color color,
            float falloffIntensity,
            int[] targetSortingLayers)
        {
            Intensity = Mathf.Max(0f, intensity);
            Radius = Mathf.Max(0f, radius);
            InnerRadiusRatio = Mathf.Clamp(innerRadiusRatio, 0.05f, 0.95f);
            Color = color;
            FalloffIntensity = Mathf.Clamp01(falloffIntensity);
            TargetSortingLayers = targetSortingLayers != null && targetSortingLayers.Length > 0
                ? targetSortingLayers
                : GetRuntimeLightTargetSortingLayers(null);
        }

        public float Intensity { get; }
        public float Radius { get; }
        public float InnerRadiusRatio { get; }
        public Color Color { get; }
        public float FalloffIntensity { get; }
        public int[] TargetSortingLayers { get; }
    }

    public static void ApplyUseCompleted(IBuildingVisitorPort actor, BuildableObject building)
    {
        if (building?.BuildingData == null)
        {
            return;
        }

        foreach (IBuildingUseCompletedRuntimeAbility ability in building.BuildingData.Abilities
                     .OfType<IBuildingUseCompletedRuntimeAbility>())
        {
            ability.ApplyUseCompleted(actor, building);
        }
    }

    public static int ApplyWorkCompleted(
        IBuildingVisitorPort actor,
        BuildableObject building,
        WorkTypeId workTypeId)
    {
        if (building?.BuildingData == null || !workTypeId.IsValid)
        {
            return 0;
        }

        if (building.AbilityRuntimeDispatcher == null)
        {
            throw new System.InvalidOperationException(
                $"{building.name} requires {nameof(IBuildingAbilityRuntimeDispatcher)} injection.");
        }

        return building.AbilityRuntimeDispatcher.ApplyWorkCompleted(
            actor,
            building,
            workTypeId);
    }

    public static int ApplyProduction(
        IBuildingVisitorPort actor,
        BuildableObject building,
        BuildingProductionAbility ability,
        WorkTypeId workTypeId,
        float evolutionOutputMultiplier = 1f)
    {
        return ability == null
            ? 0
            : ApplyProduction(
                actor,
                building,
                ability.AbilityId,
                ability.outputCategory,
                ability.amount,
                workTypeId,
                evolutionOutputMultiplier);
    }

    public static int ApplyProduction(
        IBuildingVisitorPort actor,
        BuildableObject building,
        string abilityId,
        StockCategory outputCategory,
        int configuredAmount,
        WorkTypeId workTypeId,
        float evolutionOutputMultiplier = 1f)
    {
        if (building == null
            || string.IsNullOrWhiteSpace(abilityId)
            || configuredAmount <= 0
            || (workTypeId != BuiltInWorkTypeIds.Operate
                && workTypeId != BuiltInWorkTypeIds.Research))
        {
            return 0;
        }

        float outputMultiplier = actor?.VisitorSnapshot.ProductionOutputMultiplier ?? 1f;
        int requested = Mathf.CeilToInt(
                Mathf.Max(0, configuredAmount)
                * outputMultiplier
                * Mathf.Max(0.05f, evolutionOutputMultiplier))
            + (actor?.VisitorSnapshot.StockProductionBonus ?? 0);
        int amount = Produce(building, outputCategory, requested);
        string moduleId = BuildingStateModuleIds.ForAbility("production", abilityId);
        building.RequireStateModule<BuildingProductionStateModule>(moduleId).AddProducedStock(amount);
        actor?.RecordActivity(building, new BuildingActivitySnapshot(
            BuildingActivityKinds.Stock,
            BuildingActivityOutcomes.Completed,
            $"{GetName(building)}에서 {StockCategoryPersistenceId.ToId(outputCategory)} {amount}개를 생산했다.",
            actionId: "stock:produce",
            quantity: amount));
        return amount;
    }

    public static int ApplyCleaning(
        IBuildingVisitorPort actor,
        BuildableObject building,
        BuildingCleaningAbility ability,
        WorkTypeId workTypeId)
    {
        return ability == null
            ? 0
            : ApplyCleaning(
                actor,
                building,
                ability.restoredCleanliness,
                workTypeId);
    }

    public static int ApplyCleaning(
        IBuildingVisitorPort actor,
        BuildableObject building,
        float restoredCleanliness,
        WorkTypeId workTypeId)
    {
        if (building == null
            || workTypeId != BuiltInWorkTypeIds.Clean)
        {
            return 0;
        }

        foreach (BuildableObject part in building.GetRoomOperationalProfile()
                     .Parts
                     .OfType<BuildableObject>())
        {
            if (part != null)
            {
                part.SetCleanliness(restoredCleanliness);
            }
        }

        actor?.RecordActivity(building, new BuildingActivitySnapshot(
            BuildingActivityKinds.Work,
            BuildingActivityOutcomes.Completed,
            $"{GetName(building)} 청소를 마쳐 방이 말끔해졌다.",
            BuiltInWorkTypeIds.Clean.Value,
            string.Empty,
            string.Empty,
            restoredCleanliness,
            0,
            false));
        return 0;
    }

    public static int ApplySecurity(
        IBuildingVisitorPort actor,
        BuildableObject building,
        BuildingSecurityAbility ability,
        WorkTypeId workTypeId)
    {
        return ability == null
            ? 0
            : ApplySecurity(
                actor,
                building,
                ability.AbilityId,
                ability.maxAlarmCharges,
                ability.chargesPerGuardWork,
                workTypeId);
    }

    public static int ApplySecurity(
        IBuildingVisitorPort actor,
        BuildableObject building,
        string abilityId,
        int maxAlarmCharges,
        int chargesPerGuardWork,
        WorkTypeId workTypeId)
    {
        if (building == null
            || string.IsNullOrWhiteSpace(abilityId)
            || workTypeId != BuiltInWorkTypeIds.Guard)
        {
            return 0;
        }

        string moduleId = BuildingStateModuleIds.ForAbility("security", abilityId);
        BuildingSecurityStateModule state = building.RequireStateModule<BuildingSecurityStateModule>(moduleId);
        state.AddAlarmCharges(chargesPerGuardWork, maxAlarmCharges);
        actor?.RecordActivity(building, new BuildingActivitySnapshot(
            BuildingActivityKinds.Work,
            BuildingActivityOutcomes.Completed,
            $"{GetName(building)} 경계 태세를 갖췄다. ({state.AlarmCharges}/{Mathf.Max(1, maxAlarmCharges)})",
            BuiltInWorkTypeIds.Guard.Value,
            string.Empty,
            "alarm-charged",
            0f,
            state.AlarmCharges,
            false));
        return 0;
    }

    public static int Produce(BuildableObject source, StockCategory category, int requested)
    {
        int remaining = Mathf.Max(0, requested);
        if (remaining <= 0 || source == null)
        {
            return 0;
        }

        IEnumerable<IWarehouseFacility> roomWarehouses = source
            .GetRoomOperationalProfile()
            .Parts
            .OfType<IWarehouseFacility>()
            .Where(IsUsableWarehouse);
        int produced = Deposit(source.WorldItemStackRuntime, roomWarehouses, category, remaining, out remaining);

        if (remaining > 0 && source.Grid != null)
        {
            IEnumerable<IWarehouseFacility> allWarehouses = source.Grid
                .FindAllOccupants(null)
                .OfType<IWarehouseFacility>()
                .Where(IsUsableWarehouse);
            produced += Deposit(source.WorldItemStackRuntime, allWarehouses, category, remaining, out remaining);
        }

        return produced;
    }

    private static int Deposit(
        IBuildingItemStackPort items,
        IEnumerable<IWarehouseFacility> warehouses,
        StockCategory category,
        int requested,
        out int remaining)
    {
        remaining = Mathf.Max(0, requested);
        int deposited = 0;
        foreach (IWarehouseFacility warehouse in warehouses ?? Enumerable.Empty<IWarehouseFacility>())
        {
            if (remaining <= 0) break;
            if (items == null)
            {
                throw new System.InvalidOperationException(
                    "Facility production requires physical item runtime.");
            }
            items.SpawnStockInWarehouse(
                warehouse as IBuildingWorldEntryPort,
                category,
                remaining,
                out int amount);
            deposited += amount;
            remaining -= amount;
        }

        return deposited;
    }

    private static bool IsUsableWarehouse(IWarehouseFacility warehouse)
    {
        return warehouse != null && warehouse.HasWarehouseInventory && warehouse.Inventory != null;
    }

    private static string GetName(BuildableObject building)
    {
        return building?.BuildingData != null
            ? building.BuildingData.objectName
            : "시설";
    }
}
