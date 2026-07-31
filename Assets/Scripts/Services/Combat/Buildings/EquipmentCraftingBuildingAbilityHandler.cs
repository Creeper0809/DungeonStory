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
        ICharacterEnvironmentStatusQuery environmentStatus = null)
    {
        this.combatRuntime = combatRuntime
            ?? throw new ArgumentNullException(nameof(combatRuntime));
        this.combatCatalog = combatCatalog
            ?? throw new ArgumentNullException(nameof(combatCatalog));
        this.environmentStatus = environmentStatus;
    }

    public IReadOnlyCollection<Type> AbilityTypes => Types;

    public int Apply(BuildingAbility ability, BuildingAbilityWorkContext context)
    {
        return ability is BuildingEquipmentCraftingAbility crafting
            ? ApplyCrafting(context.Actor, context.Building, crafting, context.WorkTypeId)
            : 0;
    }

    private int ApplyCrafting(
        CharacterActor actor,
        BuildableObject building,
        BuildingEquipmentCraftingAbility ability,
        WorkTypeId workTypeId)
    {
        if (building == null
            || ability == null
            || workTypeId != BuiltInWorkTypeIds.Craft
            || !building.TryGetCombatEquipmentRuntime(
                out ICombatEquipmentRuntime equipmentRuntime))
        {
            return 0;
        }

        int completed = equipmentRuntime.ApplyCraftWork(
            ability.CraftableEquipmentIds,
            Mathf.Max(0.1f, ability.workUnitsPerCycle),
            out string completedEquipmentId,
            out string completedMaterialId);
        if (completed > 0)
        {
            SpawnCraftedOutput(
                actor,
                building,
                completedEquipmentId,
                completedMaterialId,
                completed);
        }

        string targetName = string.IsNullOrWhiteSpace(completedEquipmentId)
            ? "장비"
            : equipmentRuntime.TryGetDefinition(
                completedEquipmentId,
                out CombatEquipmentDefinitionSO definition)
                ? definition.DisplayName
                : completedEquipmentId;
        actor?.AddActivity(CharacterActivityEvent.Work(
            FacilityWorkType.Craft,
            completed > 0
                ? CharacterActivityOutcomes.Completed
                : CharacterActivityOutcomes.Progress,
            completed > 0
                ? $"{targetName} 제작을 마쳤다."
                : $"{GetBuildingName(building)}에서 제작을 진행했다.",
            building,
            reasonCode: completed > 0
                ? "equipment-crafted"
                : "equipment-crafting-progress",
            quantity: completed));
        return completed;
    }

    private bool SpawnCraftedOutput(
        CharacterActor actor,
        BuildableObject building,
        string completedEquipmentId,
        string completedMaterialId,
        int completed)
    {
        IWorldItemStackRuntime itemRuntime = building.WorldItemStackRuntime;
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
                && itemRuntime.SpawnItemAt(
                    ammunitionItemId,
                    outputAmount,
                    building.centerPos,
                    WorldItemStackState.FacilityBuffer,
                    $"craft:{building.GetInstanceID()}",
                    out int spawned)
                && spawned == outputAmount;
        }

        if (combatCatalog.TryGet(completedEquipmentId, out _)
            && completed == 1
            && itemRuntime != null
            && itemRuntime.SpawnUniqueItemAt(
                DungeonItemCatalogSO.EquipmentItemId(completedEquipmentId),
                building.centerPos,
                WorldItemStackState.FacilityBuffer,
                $"craft:{building.GetInstanceID()}",
                out string outputStackId))
        {
            CombatEquipmentInstance instance = combatRuntime.CreateInstance(
                completedEquipmentId,
                ResolveCraftedQuality(actor, building),
                CombatEquipmentWorldState.Loose,
                completedMaterialId);
            combatRuntime.TryLinkToWorldStack(
                instance.instanceId,
                outputStackId,
                CombatEquipmentWorldState.Loose);
            return true;
        }

        return false;
    }

    private CombatEquipmentQuality ResolveCraftedQuality(
        CharacterActor actor,
        BuildableObject building)
    {
        int dexterity =
            actor?.Stats?.GetCharacterStat(CharacterStatType.Dexterity) ?? 5;
        int research =
            actor?.Stats?.GetCharacterStat(CharacterStatType.Research) ?? 5;
        int score = dexterity
            + research
            + Mathf.Max(1, building?.FacilityLevel ?? 1) * 2;
        CombatEquipmentQuality quality = score switch
        {
            <= 8 => CombatEquipmentQuality.Awful,
            <= 12 => CombatEquipmentQuality.Poor,
            <= 18 => CombatEquipmentQuality.Normal,
            <= 23 => CombatEquipmentQuality.Good,
            <= 28 => CombatEquipmentQuality.Excellent,
            <= 34 => CombatEquipmentQuality.Masterwork,
            _ => CombatEquipmentQuality.Legendary
        };
        string characterId = actor?.Identity?.PersistentId;
        EnvironmentalExposureBand band =
            (EnvironmentalExposureBand)Mathf.Max(
                (int)(environmentStatus?.GetPhysiologicalBand(characterId)
                    ?? EnvironmentalExposureBand.Stable),
                (int)(environmentStatus?.GetVisualBand(characterId)
                    ?? EnvironmentalExposureBand.Stable));
        return band >= EnvironmentalExposureBand.Impaired
            ? (CombatEquipmentQuality)Mathf.Max(
                (int)CombatEquipmentQuality.Awful,
                (int)quality - 1)
            : quality;
    }

    private static string GetBuildingName(BuildableObject building)
    {
        return building?.BuildingData != null
            ? building.BuildingData.objectName
            : "시설";
    }
}
