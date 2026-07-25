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

    public static void ApplyUseCompleted(CharacterActor actor, BuildableObject building)
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
        CharacterActor actor,
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
        CharacterActor actor,
        BuildableObject building,
        BuildingProductionAbility ability,
        WorkTypeId workTypeId)
    {
        if (building == null
            || ability == null
            || !ability.IsValid
            || (workTypeId != BuiltInWorkTypeIds.Operate
                && workTypeId != BuiltInWorkTypeIds.Research))
        {
            return 0;
        }

        float outputMultiplier = CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(actor);
        int requested = Mathf.CeilToInt(Mathf.Max(0, ability.amount) * outputMultiplier)
            + CharacterSkillRuntimeEffects.GetStockProductionBonus(actor);
        int amount = Produce(building, ability.outputCategory, requested);
        string moduleId = BuildingStateModuleIds.ForAbility("production", ability.AbilityId);
        building.RequireStateModule<BuildingProductionStateModule>(moduleId).AddProducedStock(amount);
        actor?.AddActivity(CharacterActivityEvent.Facility(
            CharacterActivityKinds.Stock,
            CharacterActivityOutcomes.Completed,
            $"{GetName(building)}에서 {StockCategoryCatalog.GetDisplayName(ability.outputCategory)} {amount}개를 생산했다.",
            building,
            actionId: "stock:produce",
            quantity: amount));
        return amount;
    }

    public static int ApplyCleaning(
        CharacterActor actor,
        BuildableObject building,
        BuildingCleaningAbility ability,
        WorkTypeId workTypeId)
    {
        if (building == null
            || ability == null
            || workTypeId != BuiltInWorkTypeIds.Clean)
        {
            return 0;
        }

        foreach (BuildableObject part in building.GetRoomOperationalProfile().Parts)
        {
            if (part != null)
            {
                part.SetCleanliness(ability.restoredCleanliness);
            }
        }

        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Clean,
            CharacterActivityOutcomes.Completed,
            $"{GetName(building)} 청소를 마쳐 방이 말끔해졌다.",
            building,
            value: ability.restoredCleanliness));
        return 0;
    }

    public static int ApplySecurity(
        CharacterActor actor,
        BuildableObject building,
        BuildingSecurityAbility ability,
        WorkTypeId workTypeId)
    {
        if (building == null
            || ability == null
            || workTypeId != BuiltInWorkTypeIds.Guard)
        {
            return 0;
        }

        string moduleId = BuildingStateModuleIds.ForAbility("security", ability.AbilityId);
        BuildingSecurityStateModule state = building.RequireStateModule<BuildingSecurityStateModule>(moduleId);
        state.AddAlarmCharges(ability.chargesPerGuardWork, ability.maxAlarmCharges);
        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Guard,
            CharacterActivityOutcomes.Completed,
            $"{GetName(building)} 경계 태세를 갖췄다. ({state.AlarmCharges}/{Mathf.Max(1, ability.maxAlarmCharges)})",
            building,
            reasonCode: "alarm-charged",
            quantity: state.AlarmCharges));
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
        int produced = Deposit(roomWarehouses, category, remaining, out remaining);

        if (remaining > 0 && source.Grid != null)
        {
            IEnumerable<IWarehouseFacility> allWarehouses = source.Grid
                .FindAllOccupants(null)
                .OfType<IWarehouseFacility>()
                .Where(IsUsableWarehouse);
            produced += Deposit(allWarehouses, category, remaining, out remaining);
        }

        return produced;
    }

    private static int Deposit(
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
            int amount = warehouse.Inventory.Deposit(category, remaining);
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
