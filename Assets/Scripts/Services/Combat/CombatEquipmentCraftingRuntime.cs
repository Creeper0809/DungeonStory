using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns equipment crafting orders, concrete material policies, unlock checks,
/// and creation of repository-owned equipment instances.
/// </summary>
public sealed class CombatEquipmentCraftingRuntime
{
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IItemInstanceRepository itemInstances;
    private readonly IResourceEconomyContentCatalog materialCatalog;
    private readonly BlueprintResearchRuntime research;
    private readonly IEquipmentPhysicalItemGateway physicalItems;
    private readonly CombatEquipmentStatProjector statProjector;
    private readonly CombatEquipmentRuntimeStateStore stateStore;

    private List<CombatEquipmentCraftOrderSaveData> orders =>
        stateStore.Current.CraftOrders;
    private Dictionary<string, CombatEquipmentCraftMaterialPolicySaveData>
        materialPolicies => stateStore.Current.CraftMaterialPolicies;

    private IDictionary<string, CombatEquipmentInstance> Instances =>
        itemInstances.EquipmentInstances;

    public CombatEquipmentCraftingRuntime(
        ICombatEquipmentCatalog catalog,
        IItemInstanceRepository itemInstances,
        IResourceEconomyContentCatalog materialCatalog,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IEquipmentPhysicalItemGateway physicalItems,
        CombatEquipmentStatProjector statProjector,
        CombatEquipmentRuntimeStateStore stateStore)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.materialCatalog = materialCatalog
            ?? throw new ArgumentNullException(nameof(materialCatalog));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(CombatEquipmentCraftingRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.statProjector = statProjector
            ?? throw new ArgumentNullException(nameof(statProjector));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public IReadOnlyList<CombatEquipmentCraftOrderSaveData> Queue =>
        orders.AsReadOnly();

    public bool IsDefinitionUnlocked(string definitionId, out string failureReason)
    {
        failureReason = string.Empty;
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        if (string.IsNullOrWhiteSpace(definition.RequiredResearchId))
        {
            return true;
        }
        if (research.State.Projects.IsCompleted(
                new ResearchProjectId(definition.RequiredResearchId)))
        {
            return true;
        }
        failureReason = $"equipment.research.required:{definition.RequiredResearchId}";
        return false;
    }

    public IReadOnlyList<CraftMaterialDefinitionSO> GetAllowedMaterials(
        string definitionId)
    {
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition))
        {
            return Array.Empty<CraftMaterialDefinitionSO>();
        }

        return materialCatalog.Materials
            .Where(definition.AllowsMaterial)
            .OrderBy(material => material.RareMaterial ? 1 : 0)
            .ThenBy(material => material.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    public CombatEquipmentCraftMaterialPolicySaveData GetMaterialPolicy(
        string definitionId,
        BuildableObject craftingFacility)
    {
        return TryGetOrCreateMaterialPolicy(
                definitionId,
                craftingFacility,
                out CombatEquipmentCraftMaterialPolicySaveData policy,
                out _)
            ? policy.Clone()
            : new CombatEquipmentCraftMaterialPolicySaveData();
    }

    public bool SetMaterialAllowed(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        bool allowed,
        out string failureReason)
    {
        if (!TryGetOrCreateMaterialPolicy(
                definitionId,
                craftingFacility,
                out CombatEquipmentCraftMaterialPolicySaveData policy,
                out failureReason))
        {
            return false;
        }

        string normalizedMaterialId = materialId?.Trim() ?? string.Empty;
        if (!policy.priorityMaterialIds.Contains(
                normalizedMaterialId,
                StringComparer.Ordinal))
        {
            failureReason = "equipment.material.not_allowed";
            return false;
        }

        if (allowed)
        {
            if (!policy.allowedMaterialIds.Contains(
                    normalizedMaterialId,
                    StringComparer.Ordinal))
            {
                policy.allowedMaterialIds.Add(normalizedMaterialId);
            }
        }
        else
        {
            policy.allowedMaterialIds.RemoveAll(id =>
                string.Equals(id, normalizedMaterialId, StringComparison.Ordinal));
        }

        failureReason = string.Empty;
        return true;
    }

    public bool MoveMaterialPriority(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        int offset,
        out string failureReason)
    {
        if (!TryGetOrCreateMaterialPolicy(
                definitionId,
                craftingFacility,
                out CombatEquipmentCraftMaterialPolicySaveData policy,
                out failureReason))
        {
            return false;
        }

        string normalizedMaterialId = materialId?.Trim() ?? string.Empty;
        int currentIndex = policy.priorityMaterialIds.FindIndex(id =>
            string.Equals(id, normalizedMaterialId, StringComparison.Ordinal));
        if (currentIndex < 0)
        {
            failureReason = "equipment.material.not_allowed";
            return false;
        }

        int targetIndex = Mathf.Clamp(
            currentIndex + Math.Sign(offset),
            0,
            policy.priorityMaterialIds.Count - 1);
        if (targetIndex != currentIndex)
        {
            policy.priorityMaterialIds.RemoveAt(currentIndex);
            policy.priorityMaterialIds.Insert(targetIndex, normalizedMaterialId);
        }
        failureReason = string.Empty;
        return true;
    }

    public bool TryGetPreviewStats(
        string definitionId,
        string materialId,
        out CombatEquipmentDerivedStats stats)
    {
        stats = default;
        if (!catalog.TryGet(
                definitionId?.Trim() ?? string.Empty,
                out CombatEquipmentDefinitionSO definition)
            || !TryResolveMaterial(definition, materialId, out CraftMaterialDefinitionSO material, out _))
        {
            return false;
        }

        stats = statProjector.Build(definition, material);
        return true;
    }

    public bool TryQueue(
        string definitionId,
        BuildableObject craftingFacility,
        out string failureReason)
    {
        string normalizedId = definitionId?.Trim() ?? string.Empty;
        string defaultMaterialId = string.Empty;
        if (catalog.TryGet(normalizedId, out CombatEquipmentDefinitionSO definition))
        {
            if (TryGetOrCreateMaterialPolicy(
                    normalizedId,
                    craftingFacility,
                    out CombatEquipmentCraftMaterialPolicySaveData policy,
                    out failureReason))
            {
                defaultMaterialId = policy.priorityMaterialIds.FirstOrDefault(id =>
                    policy.allowedMaterialIds.Contains(id, StringComparer.Ordinal))
                    ?? string.Empty;
                if (string.IsNullOrWhiteSpace(defaultMaterialId))
                {
                    failureReason = "equipment.material.none_enabled";
                    return false;
                }
            }
            else if (materialCatalog.Materials.Count > 0)
            {
                return false;
            }
            else
            {
                defaultMaterialId = definition.DefaultMaterialId;
            }
        }

        return TryQueue(
            normalizedId,
            defaultMaterialId,
            craftingFacility,
            out failureReason);
    }

    public bool TryQueue(
        string definitionId,
        string materialId,
        BuildableObject craftingFacility,
        out string failureReason)
    {
        failureReason = string.Empty;
        string normalizedId = definitionId?.Trim() ?? string.Empty;
        bool ammunitionRecipe = IsAmmunitionRecipe(normalizedId);
        CombatEquipmentDefinitionSO definition = null;
        if (!ammunitionRecipe && !catalog.TryGet(normalizedId, out definition))
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        if (!ammunitionRecipe && !IsDefinitionUnlocked(normalizedId, out failureReason))
        {
            return false;
        }

        CraftMaterialDefinitionSO material = null;
        if (!ammunitionRecipe
            && !TryResolveMaterial(definition, materialId, out material, out failureReason))
        {
            return false;
        }
        if (craftingFacility == null)
        {
            failureReason = "equipment.craft.facility_required";
            return false;
        }
        if (!TryBuildConcreteMaterials(
                definition,
                normalizedId,
                material,
                out IReadOnlyDictionary<string, int> materials,
                out failureReason))
        {
            return false;
        }

        string orderId = $"combat-craft:{Guid.NewGuid():N}";
        string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + orderId;
        foreach (KeyValuePair<string, int> cost in materials)
        {
            if (!physicalItems.TryRequestItemDelivery(
                    cost.Key,
                    cost.Value,
                    craftingFacility.centerPos,
                    destinationId,
                    out int requested,
                    out string requestFailure)
                || requested < cost.Value)
            {
                physicalItems.ReleaseStacksByDestination(
                    destinationId,
                    craftingFacility.centerPos);
                failureReason = string.IsNullOrWhiteSpace(requestFailure)
                    ? "equipment.craft.materials_missing"
                    : requestFailure;
                return false;
            }
        }

        orders.Add(new CombatEquipmentCraftOrderSaveData
        {
            orderId = orderId,
            definitionId = normalizedId,
            materialId = material?.MaterialId
                ?? ResolveRequestedMaterialId(definition, materialId),
            requiredWork = ammunitionRecipe ? 4f : definition.RequiredCraftWork,
            completedWork = 0f,
            materialsReady = materials.Count == 0,
            materialDestinationId = destinationId,
            destinationX = craftingFacility.centerPos.x,
            destinationY = craftingFacility.centerPos.y
        });
        return true;
    }

    public bool HasPendingWork(IEnumerable<string> craftableDefinitionIds)
    {
        return orders.Any(order =>
            order != null
            && order.RemainingWork > 0f
            && IsCraftable(order.definitionId, craftableDefinitionIds)
            && EnsureMaterialsReady(order));
    }

    public int ApplyWork(
        IEnumerable<string> craftableDefinitionIds,
        float workUnits,
        out string completedDefinitionId,
        out string completedMaterialId)
    {
        completedDefinitionId = string.Empty;
        completedMaterialId = string.Empty;
        float safeWork = Mathf.Max(0f, workUnits);
        if (safeWork <= 0f)
        {
            return 0;
        }

        for (int index = 0; index < orders.Count; index++)
        {
            CombatEquipmentCraftOrderSaveData order = orders[index];
            if (order == null
                || !IsCraftable(order.definitionId, craftableDefinitionIds)
                || !EnsureMaterialsReady(order))
            {
                continue;
            }

            order.completedWork = Mathf.Min(
                Mathf.Max(0.1f, order.requiredWork),
                order.completedWork + safeWork);
            if (order.RemainingWork > 0.001f)
            {
                return 0;
            }

            completedDefinitionId = order.definitionId;
            completedMaterialId = order.materialId;
            orders.RemoveAt(index);
            return 1;
        }
        return 0;
    }

    public CombatEquipmentInstance CreateInstance(
        string definitionId,
        CombatEquipmentQuality quality,
        CombatEquipmentWorldState worldState,
        string materialId)
    {
        if (!catalog.TryGet(definitionId, out CombatEquipmentDefinitionSO definition))
        {
            throw new KeyNotFoundException(
                $"Unknown combat equipment definition '{definitionId}'.");
        }
        if (!IsDefinitionUnlocked(definitionId, out string lockedReason))
        {
            throw new InvalidOperationException(lockedReason);
        }
        if (!TryResolveMaterial(
                definition,
                materialId,
                out CraftMaterialDefinitionSO material,
                out string failureReason))
        {
            throw new ArgumentException(failureReason, nameof(materialId));
        }

        CombatEquipmentInstance instance = new CombatEquipmentInstance
        {
            instanceId = itemInstances.AllocateItemInstanceId().Value,
            definitionId = definition.EquipmentId,
            materialId = material?.MaterialId
                ?? ResolveRequestedMaterialId(definition, materialId),
            quality = quality,
            durabilityRatio = 1f,
            loadedAmmo = 0,
            worldState = worldState,
            moduleSlots = Enumerable.Range(0, definition.ModuleSlotCount)
                .Select(index => new EquipmentModuleSlotState { slotIndex = index })
                .ToList()
        };
        Instances.Add(instance.instanceId, instance);
        return instance.Clone();
    }

    public CraftMaterialDefinitionSO ResolveInstanceMaterial(
        CombatEquipmentInstance instance,
        CombatEquipmentDefinitionSO definition)
    {
        if (materialCatalog.Materials.Count == 0 || definition == null)
        {
            return null;
        }
        string materialId = ResolveRequestedMaterialId(definition, instance?.materialId);
        return materialCatalog.TryGetMaterial(materialId, out CraftMaterialDefinitionSO material)
            && definition.AllowsMaterial(material)
                ? material
                : null;
    }

    public string NormalizeRestoredMaterialId(
        CombatEquipmentDefinitionSO definition,
        string materialId)
    {
        string normalized = ResolveRequestedMaterialId(definition, materialId);
        if (materialCatalog.Materials.Count == 0)
        {
            return normalized;
        }
        return materialCatalog.TryGetMaterial(normalized, out CraftMaterialDefinitionSO material)
            && definition.AllowsMaterial(material)
                ? material.MaterialId
                : definition.DefaultMaterialId;
    }

    public IReadOnlyList<CombatEquipmentCraftOrderSaveData> CaptureOrders() =>
        orders
            .Where(order => order != null && order.RemainingWork > 0f)
            .Select(order => order.Clone())
            .ToArray();

    public IReadOnlyList<CombatEquipmentCraftMaterialPolicySaveData> CapturePolicies() =>
        materialPolicies.Values.Select(policy => policy.Clone()).ToArray();

    internal void PopulateRestoreState(
        CombatEquipmentRuntimeState target,
        IEnumerable<CombatEquipmentCraftOrderSaveData> savedOrders,
        IEnumerable<CombatEquipmentCraftMaterialPolicySaveData> savedPolicies)
    {
        CombatEquipmentRuntimeState requiredTarget = target
            ?? throw new ArgumentNullException(nameof(target));
        HashSet<string> orderIds = new(StringComparer.Ordinal);
        foreach (CombatEquipmentCraftOrderSaveData source in savedOrders
                     ?? Array.Empty<CombatEquipmentCraftOrderSaveData>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.orderId)
                || !orderIds.Add(source.orderId)
                || source.RemainingWork <= 0f
                || (!IsAmmunitionRecipe(source.definitionId)
                    && (!catalog.TryGet(source.definitionId, out _)
                        || !IsDefinitionUnlocked(source.definitionId, out _))))
            {
                continue;
            }

            CombatEquipmentCraftOrderSaveData restored = source.Clone();
            if (catalog.TryGet(restored.definitionId, out CombatEquipmentDefinitionSO definition))
            {
                restored.materialId = NormalizeRestoredMaterialId(
                    definition,
                    restored.materialId);
            }
            requiredTarget.CraftOrders.Add(restored);
        }

        foreach (CombatEquipmentCraftMaterialPolicySaveData source in savedPolicies
                     ?? Array.Empty<CombatEquipmentCraftMaterialPolicySaveData>())
        {
            CombatEquipmentCraftMaterialPolicySaveData restored =
                NormalizeMaterialPolicy(source);
            string key = BuildMaterialPolicyKey(
                restored.facilityKey,
                restored.definitionId);
            if (string.IsNullOrWhiteSpace(restored.facilityKey)
                || string.IsNullOrWhiteSpace(restored.definitionId)
                || requiredTarget.CraftMaterialPolicies.ContainsKey(key))
            {
                continue;
            }
            requiredTarget.CraftMaterialPolicies.Add(key, restored);
        }
    }

    private bool EnsureMaterialsReady(CombatEquipmentCraftOrderSaveData order)
    {
        if (order == null)
        {
            return false;
        }
        if (order.materialsReady)
        {
            return true;
        }

        CombatEquipmentDefinitionSO definition = null;
        if (!IsAmmunitionRecipe(order.definitionId)
            && !catalog.TryGet(order.definitionId, out definition))
        {
            return false;
        }
        CraftMaterialDefinitionSO material = null;
        if (definition != null
            && !TryResolveMaterial(definition, order.materialId, out material, out _))
        {
            return false;
        }
        if (!TryBuildConcreteMaterials(
                definition,
                order.definitionId,
                material,
                out IReadOnlyDictionary<string, int> materials,
                out _))
        {
            return false;
        }
        if (materials.Count == 0)
        {
            order.materialsReady = true;
            return true;
        }
        if (string.IsNullOrWhiteSpace(order.materialDestinationId)
            || !physicalItems.TryConsumeFacilityItemBuffer(
                order.materialDestinationId,
                materials,
                out _))
        {
            return false;
        }
        order.materialsReady = true;
        return true;
    }

    private static bool TryBuildConcreteMaterials(
        CombatEquipmentDefinitionSO definition,
        string definitionId,
        CraftMaterialDefinitionSO material,
        out IReadOnlyDictionary<string, int> result,
        out string failureReason)
    {
        Dictionary<string, int> materials = new(StringComparer.Ordinal);
        failureReason = string.Empty;
        if (string.Equals(
                definitionId,
                CombatItemDefinitions.ArrowBundleRecipeId,
                StringComparison.Ordinal))
        {
            materials["material:lumber"] = 1;
            materials["resource:feather"] = 1;
            result = materials;
            return true;
        }
        if (string.Equals(
                definitionId,
                CombatItemDefinitions.BoltBundleRecipeId,
                StringComparison.Ordinal))
        {
            materials["material:lumber"] = 1;
            materials["material:iron-ingot"] = 1;
            result = materials;
            return true;
        }
        if ((definition?.CraftMaterials?.Count ?? 0) > 0)
        {
            result = materials;
            failureReason = "equipment.craft.legacy_stock_category_input";
            return false;
        }
        if (material != null && !string.IsNullOrWhiteSpace(material.ItemId))
        {
            materials[material.ItemId] = Mathf.Max(1, definition.PrimaryMaterialAmount);
        }
        foreach (ItemAmountDefinition component in definition?.RequiredComponentInputs
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            if (component == null
                || string.IsNullOrWhiteSpace(component.ItemId)
                || component.Amount <= 0)
            {
                continue;
            }
            materials.TryGetValue(component.ItemId, out int current);
            materials[component.ItemId] = current + component.Amount;
        }
        result = materials;
        return true;
    }

    private bool TryResolveMaterial(
        CombatEquipmentDefinitionSO definition,
        string requestedMaterialId,
        out CraftMaterialDefinitionSO material,
        out string failureReason)
    {
        material = null;
        failureReason = string.Empty;
        if (definition == null)
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        string normalizedId = ResolveRequestedMaterialId(
            definition,
            requestedMaterialId);
        if (materialCatalog.Materials.Count == 0)
        {
            return true;
        }
        if (string.IsNullOrWhiteSpace(normalizedId)
            || !materialCatalog.TryGetMaterial(normalizedId, out material))
        {
            failureReason = "equipment.material.unknown";
            return false;
        }
        if (!definition.AllowsMaterial(material))
        {
            failureReason = "equipment.material.not_allowed";
            material = null;
            return false;
        }
        return true;
    }

    private bool TryGetOrCreateMaterialPolicy(
        string definitionId,
        BuildableObject craftingFacility,
        out CombatEquipmentCraftMaterialPolicySaveData policy,
        out string failureReason)
    {
        policy = null;
        failureReason = string.Empty;
        if (craftingFacility == null)
        {
            failureReason = "equipment.craft.facility_required";
            return false;
        }
        string normalizedDefinitionId = definitionId?.Trim() ?? string.Empty;
        if (!catalog.TryGet(normalizedDefinitionId, out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "equipment.definition.unknown";
            return false;
        }
        IReadOnlyList<CraftMaterialDefinitionSO> allowedMaterials =
            GetAllowedMaterials(normalizedDefinitionId);
        if (allowedMaterials.Count == 0)
        {
            failureReason = "equipment.material.none_available";
            return false;
        }

        string facilityKey = craftingFacility.RequirePersistentInstanceId().Value;
        string policyKey = BuildMaterialPolicyKey(facilityKey, normalizedDefinitionId);
        if (materialPolicies.TryGetValue(policyKey, out policy))
        {
            policy = NormalizeMaterialPolicy(policy);
            materialPolicies[policyKey] = policy;
            return true;
        }

        List<string> priority = allowedMaterials
            .OrderBy(material => string.Equals(
                    material.MaterialId,
                    definition.DefaultMaterialId,
                    StringComparison.Ordinal)
                ? 0
                : 1)
            .ThenBy(material => material.RareMaterial ? 1 : 0)
            .ThenBy(material => material.DisplayName, StringComparer.Ordinal)
            .Select(material => material.MaterialId)
            .ToList();
        List<string> allowed = allowedMaterials
            .Where(material => !material.RareMaterial)
            .Select(material => material.MaterialId)
            .ToList();
        if (allowed.Count == 0 && priority.Count > 0)
        {
            allowed.Add(priority[0]);
        }
        policy = new CombatEquipmentCraftMaterialPolicySaveData
        {
            facilityKey = facilityKey,
            definitionId = normalizedDefinitionId,
            priorityMaterialIds = priority,
            allowedMaterialIds = allowed
        };
        materialPolicies.Add(policyKey, policy);
        return true;
    }

    private CombatEquipmentCraftMaterialPolicySaveData NormalizeMaterialPolicy(
        CombatEquipmentCraftMaterialPolicySaveData source)
    {
        CombatEquipmentCraftMaterialPolicySaveData clone =
            source?.Clone() ?? new CombatEquipmentCraftMaterialPolicySaveData();
        if (!catalog.TryGet(clone.definitionId, out CombatEquipmentDefinitionSO definition))
        {
            return new CombatEquipmentCraftMaterialPolicySaveData();
        }
        Dictionary<string, CraftMaterialDefinitionSO> allowedById =
            GetAllowedMaterials(definition.EquipmentId)
                .ToDictionary(
                    material => material.MaterialId,
                    material => material,
                    StringComparer.Ordinal);
        List<string> priority = clone.priorityMaterialIds
            .Where(allowedById.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        foreach (CraftMaterialDefinitionSO material in allowedById.Values
                     .OrderBy(candidate => string.Equals(
                             candidate.MaterialId,
                             definition.DefaultMaterialId,
                             StringComparison.Ordinal)
                         ? 0
                         : 1)
                     .ThenBy(candidate => candidate.RareMaterial ? 1 : 0)
                     .ThenBy(candidate => candidate.DisplayName, StringComparer.Ordinal))
        {
            if (!priority.Contains(material.MaterialId, StringComparer.Ordinal))
            {
                priority.Add(material.MaterialId);
            }
        }
        return new CombatEquipmentCraftMaterialPolicySaveData
        {
            facilityKey = clone.facilityKey,
            definitionId = definition.EquipmentId,
            priorityMaterialIds = priority,
            allowedMaterialIds = clone.allowedMaterialIds
                .Where(allowedById.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
    }

    private static string ResolveRequestedMaterialId(
        CombatEquipmentDefinitionSO definition,
        string requestedMaterialId)
    {
        return string.IsNullOrWhiteSpace(requestedMaterialId)
            ? definition?.DefaultMaterialId ?? string.Empty
            : requestedMaterialId.Trim();
    }

    private static string BuildMaterialPolicyKey(string facilityKey, string definitionId)
    {
        return $"{facilityKey?.Trim() ?? string.Empty}|"
            + $"{definitionId?.Trim() ?? string.Empty}";
    }

    private static bool IsCraftable(
        string definitionId,
        IEnumerable<string> craftableDefinitionIds)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return false;
        }
        string[] allowed = craftableDefinitionIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<string>();
        return allowed.Length == 0
            || allowed.Contains(definitionId, StringComparer.Ordinal);
    }

    public static bool IsAmmunitionRecipe(string definitionId)
    {
        return string.Equals(
                definitionId,
                CombatItemDefinitions.ArrowBundleRecipeId,
                StringComparison.Ordinal)
            || string.Equals(
                definitionId,
                CombatItemDefinitions.BoltBundleRecipeId,
                StringComparison.Ordinal);
    }
}
