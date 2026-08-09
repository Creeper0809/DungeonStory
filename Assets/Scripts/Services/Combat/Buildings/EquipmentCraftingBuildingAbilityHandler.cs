using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EquipmentCraftingBuildingAbilityHandler :
    IBuildingAbilityWorkCompletedHandler
{
    private static readonly Type[] Types =
    {
        typeof(BuildingEquipmentCraftingAbility)
    };

    private readonly ICombatEquipmentRuntime combatRuntime;
    private readonly ICombatEquipmentCatalog combatCatalog;
    private readonly ICharacterEnvironmentStatusQuery environmentStatus;

    public EquipmentCraftingBuildingAbilityHandler(
        ICombatEquipmentRuntime combatRuntime,
        ICombatEquipmentCatalog combatCatalog,
        ICharacterEnvironmentStatusQuery environmentStatus)
    {
        this.combatRuntime = combatRuntime
            ?? throw new ArgumentNullException(nameof(combatRuntime));
        this.combatCatalog = combatCatalog
            ?? throw new ArgumentNullException(nameof(combatCatalog));
        this.environmentStatus = environmentStatus
            ?? throw new ArgumentNullException(nameof(environmentStatus));
    }

    public IReadOnlyCollection<Type> AbilityTypes => Types;

    public int Apply(BuildingAbility ability, BuildingAbilityWorkContext context)
    {
        return ability is BuildingEquipmentCraftingAbility crafting
            ? ApplyCrafting(context.Actor, context.Building, crafting, context.WorkTypeId)
            : 0;
    }

    private int ApplyCrafting(
        IBuildingVisitorPort actor,
        BuildableObject building,
        BuildingEquipmentCraftingAbility ability,
        WorkTypeId workTypeId)
    {
        if (building == null
            || ability == null
            || workTypeId != BuiltInWorkTypeIds.Craft
            || !building.TryGetCombatEquipmentRuntime(
                out IBuildingEquipmentCraftingRuntimePort runtimePort)
            || runtimePort is not ICombatEquipmentRuntime equipmentRuntime)
        {
            return 0;
        }

        CharacterBuildingVisitorAdapter.TryGetActor(actor, out CharacterActor worker);
        BuildingVisitorSnapshot workerSnapshot = actor?.VisitorSnapshot ?? default;
        // Craft quality uses the shared 0..100 craftsmanship scale while
        // character stats are authored on 0..10. Weight the two relevant
        // attributes equally, then project the mean to the shared scale.
        float relevantSkill = Mathf.Clamp(
            (workerSnapshot.Dexterity + workerSnapshot.Research) * 5f,
            0f,
            100f);
        int completed = equipmentRuntime.ApplyCraftWork(
            ability.CraftableEquipmentIds,
            Mathf.Max(0.1f, ability.workUnitsPerCycle),
            worker,
            relevantSkill,
            out string completedEquipmentId,
            out string completedMaterialId,
            out CombatEquipmentQuality completedQuality);
        if (completed > 0)
        {
            SpawnCraftedOutput(
                actor,
                building,
                completedEquipmentId,
                completedMaterialId,
                completedQuality,
                completed);
        }

        string targetName = string.IsNullOrWhiteSpace(completedEquipmentId)
            ? "장비"
            : equipmentRuntime.TryGetDefinition(
                completedEquipmentId,
                out CombatEquipmentDefinitionSO definition)
                ? definition.DisplayName
                : completedEquipmentId;
        actor?.RecordActivity(
            building,
            new BuildingActivitySnapshot(
                BuildingActivityKinds.Work,
                completed > 0
                    ? BuildingActivityOutcomes.Completed
                    : BuildingActivityOutcomes.Progress,
                completed > 0
                    ? $"{targetName} 제작을 마쳤다."
                    : $"{GetBuildingName(building)}에서 제작을 진행했다.",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                completed > 0
                    ? "equipment-crafted"
                    : "equipment-crafting-progress",
                0f,
                completed,
                false));
        return completed;
    }

    private bool SpawnCraftedOutput(
        IBuildingVisitorPort actor,
        BuildableObject building,
        string completedEquipmentId,
        string completedMaterialId,
        CombatEquipmentQuality completedQuality,
        int completed)
    {
        IBuildingItemStackPort itemRuntime = building.WorldItemStackRuntime;
        if (completedEquipmentId == CombatItemDefinitions.ArrowBundleRecipeId
            || completedEquipmentId == CombatItemDefinitions.BoltBundleRecipeId)
        {
            string ammunitionItemId =
                completedEquipmentId == CombatItemDefinitions.ArrowBundleRecipeId
                    ? CombatItemDefinitions.ArrowItemId
                    : CombatItemDefinitions.BoltItemId;
            int outputAmount =
                completedEquipmentId == CombatItemDefinitions.ArrowBundleRecipeId
                    ? 20
                    : 12;
            return itemRuntime != null
                && itemRuntime.SpawnFacilityBufferItem(
                    ammunitionItemId,
                    outputAmount,
                    building.centerPos,
                    $"craft:{building.RequirePersistentInstanceId().Value}",
                    out int spawned)
                && spawned == outputAmount;
        }

        if (combatCatalog.TryGet(completedEquipmentId, out _)
            && completed == 1
            && itemRuntime != null)
        {
            CombatEquipmentInstance instance = combatRuntime.CreateInstance(
                completedEquipmentId,
                completedQuality,
                CombatEquipmentWorldState.Loose,
                completedMaterialId);
            if (!itemRuntime.SpawnExistingFacilityBufferUniqueItem(
                    PhysicalItemIds.ForEquipment(completedEquipmentId),
                    (ItemInstanceId)instance.instanceId,
                    building.centerPos,
                    $"craft:{building.RequirePersistentInstanceId().Value}",
                    out string outputStackId))
            {
                throw new InvalidOperationException(
                    $"Failed to materialize crafted equipment '{instance.instanceId}'.");
            }
            combatRuntime.TryLinkToWorldStack(
                instance.instanceId,
                outputStackId,
                CombatEquipmentWorldState.Loose);
            return true;
        }

        return false;
    }

    private static string GetBuildingName(BuildableObject building)
    {
        return building?.BuildingData != null
            ? building.BuildingData.objectName
            : "시설";
    }
}
