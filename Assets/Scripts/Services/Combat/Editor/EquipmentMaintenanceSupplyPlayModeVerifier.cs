#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class EquipmentMaintenanceSupplyPlayModeVerifier
{
    public static string Run()
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException("Play Mode is required.");
        }

        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope == null || scope.Container == null)
        {
            throw new InvalidOperationException("Runtime scope is missing.");
        }

        ICombatEquipmentRuntime equipment =
            scope.Container.Resolve<ICombatEquipmentRuntime>();
        ICombatEquipmentMaintenanceRuntime maintenance =
            scope.Container.Resolve<ICombatEquipmentMaintenanceRuntime>();
        IWorldItemStackRuntime items =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        BuildingSO building = AssetDatabase.FindAssets("t:BuildingSO")
            .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
            .Select(path => AssetDatabase.LoadAssetAtPath<BuildingSO>(path))
            .FirstOrDefault(candidate => string.Equals(
                candidate
                    ?.GetAbility<BuildingEquipmentMaintenanceAbility>()
                    ?.RepairSupplyItemId,
                "tool:maintenance-kit",
                StringComparison.Ordinal));
        if (building == null)
        {
            throw new InvalidOperationException(
                "Authored maintenance facility is missing.");
        }

        GameObject verifierObject =
            new GameObject("MaintenanceKit_FocusedVerifier");
        try
        {
            BuildableObject facility =
                verifierObject.AddComponent<BuildableObject>();
            scope.Container.Inject(facility);
            facility.Initialization(building, Vector2Int.zero);

            CombatEquipmentInstance instance = equipment.CreateInstance(
                "armor:cloth-hood",
                CombatEquipmentQuality.Normal,
                CombatEquipmentWorldState.Loose);
            if (instance == null
                || !equipment.TryApplyDurabilityDamage(instance.instanceId, 60f))
            {
                throw new InvalidOperationException(
                    "Could not create damaged armor.");
            }

            if (!items.SpawnItemAt(
                    "tool:maintenance-kit",
                    8,
                    facility.centerPos,
                    WorldItemStackState.Loose,
                    string.Empty,
                    out int spawned)
                || spawned < 1)
            {
                throw new InvalidOperationException(
                    "Could not spawn maintenance kits.");
            }

            if (!maintenance.TryRequestManualRepair(
                    instance.instanceId,
                    out string message))
            {
                throw new InvalidOperationException(
                    "Repair request failed: " + message);
            }

            CombatEquipmentRepairOrder order = maintenance.Orders.Single(value =>
                string.Equals(
                    value.equipmentInstanceId,
                    instance.instanceId,
                    StringComparison.Ordinal));
            if (!string.Equals(
                    order.materialItemId,
                    "tool:maintenance-kit",
                    StringComparison.Ordinal)
                || order.requiredMaterialAmount < 1
                || !order.materialDeliveryRequested)
            {
                throw new InvalidOperationException(
                    $"Wrong maintenance supply order: {order.materialItemId} "
                    + $"x{order.requiredMaterialAmount}, "
                    + $"requested={order.materialDeliveryRequested}");
            }

            return $"maintenance-kit x{order.requiredMaterialAmount} "
                + "physical delivery requested";
        }
        finally
        {
            UnityEngine.Object.Destroy(verifierObject);
        }
    }
}
#endif
