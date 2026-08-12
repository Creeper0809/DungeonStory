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
    private readonly IGameCalendar calendar;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly ICharacterPerformanceQuery performance;

    public EquipmentCraftingBuildingAbilityHandler(
        ICombatEquipmentRuntime combatRuntime,
        ICombatEquipmentCatalog combatCatalog,
        ICharacterEnvironmentStatusQuery environmentStatus,
        IGameCalendar calendar = null,
        CharacterIdentityEventPublisher identityEvents = null,
        ICharacterPerformanceQuery performance = null)
    {
        this.combatRuntime = combatRuntime
            ?? throw new ArgumentNullException(nameof(combatRuntime));
        this.combatCatalog = combatCatalog
            ?? throw new ArgumentNullException(nameof(combatCatalog));
        this.environmentStatus = environmentStatus
            ?? throw new ArgumentNullException(nameof(environmentStatus));
        this.calendar = calendar;
        this.identityEvents = identityEvents;
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
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
        if (worker == null || performance == null)
            throw new InvalidOperationException(
                "Equipment craft quality requires a live worker and the character performance query.");
        float relevantSkill = Mathf.Clamp(
            performance.Evaluate(
                worker,
                "performance:work:craft:quality").Value * 58f,
            0f,
            100f);
        int completed = equipmentRuntime.ApplyCraftWork(
            ability.CraftableEquipmentIds,
            Mathf.Max(0.1f, ability.workUnitsPerCycle),
            worker,
            relevantSkill,
            out string completedEquipmentId,
            out string completedMaterialId,
            out CombatEquipmentQuality completedQuality,
            out MythicProvenanceSaveData mythicProvenance);
        if (completed > 0)
        {
            SpawnCraftedOutput(
                actor,
                building,
                completedEquipmentId,
                completedMaterialId,
                completedQuality,
                mythicProvenance,
                completed);
            PublishMaterialOutcome(
                worker,
                completedEquipmentId,
                completedMaterialId);
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

    private void PublishMaterialOutcome(
        CharacterActor worker,
        string definitionId,
        string materialId)
    {
        if (identityEvents == null
            || worker == null
            || !CharacterPersistentIdentity.TryGet(worker, out CharacterId characterId)
            || !combatCatalog.TryGet(definitionId, out CombatEquipmentDefinitionSO definition))
        {
            return;
        }

        string resolvedMaterial = string.IsNullOrWhiteSpace(materialId)
            ? definition.DefaultMaterialId
            : materialId.Trim();
        bool substitute = !string.Equals(
            resolvedMaterial,
            definition.DefaultMaterialId,
            StringComparison.Ordinal);
        identityEvents.Publish(new WorkCompletedIdentityEvent(
            characterId,
            substitute
                ? "work:substitute-success"
                : "work:strict-procedure",
            definitionId,
            CharacterCommandOrigin.Autonomous,
            Mathf.Max(0, calendar?.Day ?? 0)));
    }

    private bool SpawnCraftedOutput(
        IBuildingVisitorPort actor,
        BuildableObject building,
        string completedEquipmentId,
        string completedMaterialId,
        CombatEquipmentQuality completedQuality,
        MythicProvenanceSaveData mythicProvenance,
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
            if (mythicProvenance != null)
            {
                mythicProvenance.createdDay = Mathf.Max(0, calendar?.Day ?? 0);
                mythicProvenance.createdFacilityId =
                    building.RequirePersistentInstanceId().Value;
            }
            CombatEquipmentInstance instance = combatRuntime.CreateInstance(
                completedEquipmentId,
                completedQuality,
                CombatEquipmentWorldState.Loose,
                completedMaterialId,
                mythicProvenance);
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
