using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        IEquipmentPhysicalItemGateway itemStackRuntime,
        ICombatRejectedRecoveryProjector rejectedRecoveryProjector = null,
        ICharacterWorldQuery characterWorld = null,
        ICraftQualityResolver qualityResolver = null,
        IProductionFacilityMutationEpochQuery facilityMutations = null,
        ICombatEquipmentCraftInputDestinationRuntime inputDestinations = null)
    {
        catalog = Require(catalog, nameof(catalog));
        itemInstances = Require(itemInstances, nameof(itemInstances));
        carryInventories = Require(carryInventories, nameof(carryInventories));
        materialCatalog = Require(materialCatalog, nameof(materialCatalog));
        evolutionModules = Require(evolutionModules, nameof(evolutionModules));
        researchProvider = Require(researchProvider, nameof(researchProvider));
        moduleCatalog = Require(moduleCatalog, nameof(moduleCatalog));
        itemStackRuntime = Require(itemStackRuntime, nameof(itemStackRuntime));

        IProductionOutputCapabilityRegistry outputCapabilities =
            CreateOutputCapabilities(
                catalog,
                CreateItemDefinitionCatalog());
        CombatEquipmentPhysicalStateWriter physicalState =
            new CombatEquipmentPhysicalStateWriter(
                itemInstances,
                itemStackRuntime,
                outputCapabilities);
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
                    AllFacilityCapabilityQuery.Instance,
                    new EditorEquipmentModuleExactSourcePublicationProxy(
                        itemStackRuntime)),
                new EquipmentHistoryTransferRuntime(
                    itemInstances,
                    catalog,
                    researchProvider,
                    itemStackRuntime,
                    physicalState,
                    loadouts,
                    stateStore),
                stateStore);
        inputDestinations ??= CreateCraftInputDestinationRuntime(
            itemStackRuntime);
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
                stateStore,
                facilityMutations ?? new ProductionFacilityMutationEpochRuntime(),
                qualityResolver: qualityResolver,
                rejectedRecoveryProjector: rejectedRecoveryProjector,
                characterWorld: characterWorld,
                inputDestinations: inputDestinations);
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

    internal static IProductionOutputCapabilityRegistry CreateOutputCapabilities(
        ICombatEquipmentCatalog catalog,
        IItemDefinitionCatalog itemDefinitions)
    {
        catalog = Require(catalog, nameof(catalog));
        itemDefinitions = Require(itemDefinitions, nameof(itemDefinitions));
        return new ProductionOutputHandlerRegistry(
            new IProductionOutputCapability[]
            {
                new FixtureStandardOutputCapability(),
                new CombatEquipmentCraftOutputCapability(catalog),
                new CombatAmmunitionCraftOutputCapability(itemDefinitions)
            });
    }

    private static IItemDefinitionCatalog CreateItemDefinitionCatalog() =>
        new ResourceItemDefinitionCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));

    private static ICombatEquipmentCraftInputDestinationRuntime
        CreateCraftInputDestinationRuntime(
            IEquipmentPhysicalItemGateway physicalItems)
    {
        EditorCraftInputAuthority authority = new();
        return new CombatEquipmentCraftInputDestinationRuntime(
            new PhysicalItemMassQuery(EditorItemCatalogFactory.Create()),
            physicalItems,
            authority,
            authority,
            new FacilityBufferDestinationLifecycleService(
                authority,
                authority,
                authority,
                authority),
            new EditorCraftInputRelease(physicalItems));
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

    private sealed class FixtureStandardOutputCapability :
        IProductionOutputCapability
    {
        public string CapabilityId =>
            ProductionOutputCapabilityIds.StandardDefinition;
        public int ContractVersion =>
            ProductionOutputCapabilityIds.StandardDefinitionVersion;
        public string ComponentCodecId =>
            ProductionOutputCapabilityIds.DefinitionOnlyCodec;
        public int ComponentCodecVersion =>
            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion;
        public bool SupportsAutomaticSelection => true;
        public bool CanHandle(string itemId) => false;

    }

    /// <summary>
    /// Isolated Editor authority with the same owner-scoped replacement
    /// semantics as production. Each factory aggregate receives a fresh
    /// instance, so save/restore tests cannot leak claims between runtimes.
    /// </summary>
    private sealed class EditorCraftInputAuthority :
        IFacilityBufferDestinationClaimAuthorityQuery,
        IFacilityBufferDestinationClaimCommand,
        IFacilityBufferMassCapacityAuthorityQuery,
        IFacilityBufferMassCapacityCommand
    {
        private readonly List<FacilityBufferDestinationClaim> claims = new();
        private readonly List<FacilityBufferCapacityProfile> profiles = new();

        public bool TryGetAuthorityClaim(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferDestinationClaim claim)
        {
            claim = claims.SingleOrDefault(value =>
                string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && value.DropPosition == dropPosition);
            return claim != null;
        }

        public IReadOnlyList<FacilityBufferDestinationClaim>
            CaptureAuthorityClaims() => claims
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();

        public bool TryClaim(
            FacilityBufferDestinationClaim claim,
            out FacilityBufferDestinationClaimFailureCode failureCode,
            out string failureReason) => TryReplaceOwnedClaims(
            claim.OwnerDomain,
            new[] { claim },
            out failureCode,
            out failureReason);

        public bool TryRevoke(
            FacilityBufferDestinationClaim expectedClaim,
            out FacilityBufferDestinationClaimFailureCode failureCode,
            out string failureReason)
        {
            claims.Remove(expectedClaim);
            failureCode = FacilityBufferDestinationClaimFailureCode.None;
            failureReason = string.Empty;
            return true;
        }

        public bool TryReplaceOwnedClaims(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            out FacilityBufferDestinationClaimFailureCode failureCode,
            out string failureReason)
        {
            claims.RemoveAll(value => string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal));
            claims.AddRange(desiredClaims
                ?? Array.Empty<FacilityBufferDestinationClaim>());
            failureCode = FacilityBufferDestinationClaimFailureCode.None;
            failureReason = string.Empty;
            return true;
        }

        public IReadOnlyList<FacilityBufferCapacityProfile>
            CaptureAuthorityProfiles() => profiles
            .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
            .ToArray();

        public bool TryReplaceOwnedProfiles(
            string ownerDomain,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out FacilityBufferMassAdmissionFailureCode failureCode,
            out string failureReason)
        {
            profiles.RemoveAll(value => string.Equals(
                value.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal));
            profiles.AddRange(desiredProfiles
                ?? Array.Empty<FacilityBufferCapacityProfile>());
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Editor-only terminal release. It refuses to emulate carried recovery,
    /// while deterministically releasing only non-carried destination stacks.
    /// Production continues to require the carried-aware shared service.
    /// </summary>
    private sealed class EditorCraftInputRelease :
        IFacilityBufferDestinationReleaseService
    {
        private readonly IEquipmentPhysicalItemGateway physicalItems;

        internal EditorCraftInputRelease(
            IEquipmentPhysicalItemGateway physicalItems) =>
            this.physicalItems = Require(
                physicalItems,
                nameof(physicalItems));

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            if (physicalItems.GetAllStacks().Any(stack =>
                    stack != null
                    && stack.State == WorldItemStackState.Carried
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal)))
            {
                releasedQuantity = 0;
                failureReason =
                    "editor-combat-craft-carried-release-not-supported";
                return false;
            }

            releasedQuantity = physicalItems.ReleaseStacksByDestination(
                destinationId,
                ownerPosition);
            failureReason = string.Empty;
            return true;
        }
    }
}
