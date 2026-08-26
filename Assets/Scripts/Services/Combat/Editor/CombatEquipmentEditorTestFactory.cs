using System;
using System.Collections.Generic;

/// <summary>
/// Explicit Editor-test composition for the equipment aggregate. Production
/// composition remains owned by DungeonCombatRegistration.
/// </summary>
public static class CombatEquipmentEditorTestFactory
{
    public static CombatEquipmentRuntime Create(
        ICombatEquipmentCatalog catalog,
        IItemInstanceRepository itemInstances,
        ICharacterCarryInventoryRegistry carryInventories,
        IResourceEconomyContentCatalog materialCatalog,
        IEvolutionModuleRegistry evolutionModules,
        ProgressionSceneRuntimeReferences researchProvider,
        IEquipmentModuleCatalog moduleCatalog,
        IEquipmentPhysicalItemGateway itemStackRuntime)
    {
        catalog = Require(catalog, nameof(catalog));
        itemInstances = Require(itemInstances, nameof(itemInstances));
        carryInventories = Require(carryInventories, nameof(carryInventories));
        materialCatalog = Require(materialCatalog, nameof(materialCatalog));
        evolutionModules = Require(evolutionModules, nameof(evolutionModules));
        researchProvider = Require(researchProvider, nameof(researchProvider));
        moduleCatalog = Require(moduleCatalog, nameof(moduleCatalog));
        itemStackRuntime = Require(itemStackRuntime, nameof(itemStackRuntime));

        CombatEquipmentPhysicalStateWriter physicalState =
            new CombatEquipmentPhysicalStateWriter(itemInstances, itemStackRuntime);
        CombatEquipmentRuntimeStateStore stateStore =
            new CombatEquipmentRuntimeStateStore(
                new DungeonRuntimeAggregateRootStore());
        CombatEquipmentLoadoutStore loadouts =
            new CombatEquipmentLoadoutStore(stateStore);
        CombatEquipmentRuntimeCollaborators collaborators =
            new CombatEquipmentRuntimeCollaborators(
                new CombatEquipmentStatProjector(
                    itemInstances,
                    evolutionModules,
                    moduleCatalog),
                physicalState,
                loadouts,
                new EquipmentModuleRuntime(
                    itemInstances,
                    catalog,
                    moduleCatalog,
                    researchProvider,
                    physicalState,
                    itemStackRuntime,
                    AllFacilityCapabilityQuery.Instance),
                new EquipmentHistoryTransferRuntime(
                    itemInstances,
                    catalog,
                    researchProvider,
                    itemStackRuntime,
                    physicalState,
                    loadouts,
                    stateStore),
                stateStore);
        CombatEquipmentCraftingRuntime crafting =
            new CombatEquipmentCraftingRuntime(
                catalog,
                itemInstances,
                materialCatalog,
                researchProvider,
                itemStackRuntime,
                collaborators.StatProjector,
                physicalState,
                AllFacilityCapabilityQuery.Instance,
                stateStore);
        CombatEquipmentLoadoutRuntime loadoutRuntime =
            new CombatEquipmentLoadoutRuntime(
                catalog,
                itemInstances,
                loadouts,
                collaborators.StatProjector,
                crafting);
        return new CombatEquipmentRuntime(
            catalog,
            itemInstances,
            carryInventories,
            moduleCatalog,
            itemStackRuntime,
            collaborators,
            crafting,
            loadoutRuntime);
    }

    private sealed class AllFacilityCapabilityQuery : IFacilityCapabilityQuery
    {
        public static readonly AllFacilityCapabilityQuery Instance = new();
        private static readonly IReadOnlyList<BuildableObject> Available =
            new BuildableObject[1];

        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") => Available;

        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) => Available;
    }

    private static T Require<T>(T value, string parameterName)
        where T : class
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }
}
