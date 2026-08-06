using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

internal static class EquipmentMaintenanceSaveValidation
{
    internal const int MaximumPolicies = 64;
    internal const int MaximumAssignments = 512;
    internal const int MaximumOrders = 512;
    private const int MaximumDisplayNameLength = 80;
    private const int MaximumMaterialAmount = 1000;
    private const string CustomPolicyPrefix = "equipment-maintenance:custom:";
    private const string RepairOrderPrefix = "equipment-repair:";

    internal static void Validate(
        CombatEquipmentMaintenanceSaveData payload,
        DungeonGameRestoreReport report,
        EquipmentMaintenanceItemServices itemServices,
        EquipmentMaintenanceWorldServices worldServices)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (payload == null)
        {
            report.AddError("Equipment-maintenance payload is null.");
            return;
        }
        if (itemServices == null || worldServices == null)
        {
            report.AddError("Equipment-maintenance restore services are unavailable.");
            return;
        }
        if (payload.policySequence < 0
            || payload.orderSequence < 0
            || payload.policies == null
            || payload.assignments == null
            || payload.orders == null)
        {
            report.AddError("Equipment-maintenance payload has missing collections or a negative sequence.");
            return;
        }
        if (payload.policies.Count > MaximumPolicies
            || payload.assignments.Count > MaximumAssignments
            || payload.orders.Count > MaximumOrders)
        {
            report.AddError("Equipment-maintenance payload exceeds its bounded collection limits.");
        }

        Dictionary<string, EquipmentMaintenancePolicyData> policies =
            ValidatePolicies(payload, report);
        ValidateAssignments(payload, policies, report, worldServices.WorldRegistry);
        ValidateOrders(payload, report, itemServices, worldServices.WorldRegistry);
    }

    internal static EquipmentMaintenanceAggregateState CreateState(
        CombatEquipmentMaintenanceSaveData payload)
    {
        EquipmentMaintenanceAggregateState state = new()
        {
            PolicySequence = payload.policySequence,
            OrderSequence = payload.orderSequence
        };
        foreach (EquipmentMaintenancePolicyData policy in payload.policies)
        {
            state.Policies.Add(policy.id, policy.Clone());
        }
        foreach (EquipmentMaintenanceAssignmentSaveData assignment in payload.assignments)
        {
            state.Assignments.Add(assignment.characterId, assignment.policyId);
        }
        foreach (CombatEquipmentRepairOrder order in payload.orders)
        {
            state.Orders.Add(order.orderId, order.Clone());
        }

        return state;
    }

    internal static bool TryResolveRepairMaterial(
        CombatEquipmentInstance instance,
        ICombatEquipmentCatalog equipmentCatalog,
        IResourceEconomyContentCatalog resourceCatalog,
        out string materialItemId)
    {
        materialItemId = string.Empty;
        if (instance == null
            || equipmentCatalog == null
            || resourceCatalog == null
            || !equipmentCatalog.TryGet(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            return false;
        }

        string materialId = string.IsNullOrWhiteSpace(instance.materialId)
            ? definition.DefaultMaterialId
            : instance.materialId;
        if (!resourceCatalog.TryGetMaterial(
                materialId,
                out CraftMaterialDefinitionSO material)
            || !definition.AllowsMaterial(material)
            || string.IsNullOrWhiteSpace(material.ItemId))
        {
            return false;
        }

        materialItemId = material.ItemId;
        return true;
    }

    private static Dictionary<string, EquipmentMaintenancePolicyData> ValidatePolicies(
        CombatEquipmentMaintenanceSaveData payload,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, EquipmentMaintenancePolicyData> policies =
            new(StringComparer.Ordinal);
        int highestSequence = 0;
        foreach (EquipmentMaintenancePolicyData policy in payload.policies)
        {
            string id = policy?.id ?? string.Empty;
            bool builtIn = IsBuiltInPolicy(id);
            bool custom = TryParsePositiveSequence(id, CustomPolicyPrefix, out int sequence);
            if (policy == null
                || (!builtIn && !custom)
                || !policies.TryAdd(id, policy)
                || !IsCanonical(id)
                || !IsCanonical(policy.displayName)
                || policy.displayName.Length > MaximumDisplayNameLength
                || !IsFiniteRange(policy.sendAtDurability, 0f, 1f)
                || !IsFiniteRange(policy.returnAtDurability, policy.sendAtDurability, 1f))
            {
                report.AddError($"Equipment-maintenance policy '{id}' is invalid or duplicated.");
                continue;
            }

            highestSequence = Math.Max(highestSequence, sequence);
        }

        foreach (string required in new[]
                 {
                     EquipmentMaintenancePolicyRuntime.StandardPolicyId,
                     EquipmentMaintenancePolicyRuntime.PreventivePolicyId,
                     EquipmentMaintenancePolicyRuntime.ManualPolicyId
                 })
        {
            if (!policies.ContainsKey(required))
            {
                report.AddError($"Equipment-maintenance payload is missing built-in policy '{required}'.");
            }
        }
        if (payload.policySequence < highestSequence)
        {
            report.AddError(
                $"Equipment-maintenance policy sequence {payload.policySequence} is below saved policy {highestSequence}.");
        }

        return policies;
    }

    private static void ValidateAssignments(
        CombatEquipmentMaintenanceSaveData payload,
        IReadOnlyDictionary<string, EquipmentMaintenancePolicyData> policies,
        DungeonGameRestoreReport report,
        ICharacterAiWorldRegistry world)
    {
        HashSet<string> availableCharacters = new(
            (world?.Characters ?? Array.Empty<CharacterActor>())
                .Where(actor => actor != null)
                .Select(actor => CharacterPersistentIdentity.Require(actor).Value),
            StringComparer.Ordinal);
        HashSet<string> assignedCharacters = new(StringComparer.Ordinal);
        foreach (EquipmentMaintenanceAssignmentSaveData assignment in payload.assignments)
        {
            string characterId = assignment?.characterId ?? string.Empty;
            if (assignment == null
                || !((CharacterId)characterId).IsValid
                || !IsCanonical(characterId)
                || !assignedCharacters.Add(characterId)
                || !policies.ContainsKey(assignment.policyId ?? string.Empty)
                || !availableCharacters.Contains(characterId))
            {
                report.AddError(
                    $"Equipment-maintenance assignment for '{characterId}' is invalid or references missing state.");
            }
        }
    }

    private static void ValidateOrders(
        CombatEquipmentMaintenanceSaveData payload,
        DungeonGameRestoreReport report,
        EquipmentMaintenanceItemServices services,
        ICharacterAiWorldRegistry world)
    {
        Dictionary<string, BuildableObject> facilities = new(StringComparer.Ordinal);
        foreach (BuildableObject building in world.Buildings.Where(
                     CombatEquipmentMaintenanceFacilityUtility.IsMaintenanceFacility))
        {
            if (building == null)
            {
                continue;
            }
            string id = building.RequirePersistentInstanceId().Value;
            if (!facilities.TryAdd(id, building))
            {
                report.AddError($"Detached world duplicates maintenance facility '{id}'.");
            }
        }
        HashSet<string> characterIds = new(
            world.Characters
                .Where(actor => actor != null)
                .Select(actor => CharacterPersistentIdentity.Require(actor).Value),
            StringComparer.Ordinal);
        HashSet<string> orderIds = new(StringComparer.Ordinal);
        HashSet<string> equipmentIds = new(StringComparer.Ordinal);
        int highestSequence = 0;
        foreach (CombatEquipmentRepairOrder order in payload.orders)
        {
            string orderId = order?.orderId ?? string.Empty;
            bool validId = TryParseRepairOrderId(orderId, out int sequence);
            bool activeState = order != null
                && Enum.IsDefined(typeof(CombatEquipmentRepairOrderState), order.state)
                && order.state is not CombatEquipmentRepairOrderState.Completed
                    and not CombatEquipmentRepairOrderState.Cancelled;
            if (order == null
                || !validId
                || !orderIds.Add(orderId)
                || !((ItemInstanceId)(order.equipmentInstanceId ?? string.Empty)).IsValid
                || !equipmentIds.Add(order.equipmentInstanceId)
                || !((BuildingInstanceId)(order.facilityBuildingId ?? string.Empty)).IsValid
                || !facilities.ContainsKey(order.facilityBuildingId)
                || !IsOptionalCharacterId(order.originalOwnerCharacterId)
                || !IsOptionalCharacterId(order.reservedWorkerId)
                || order.reservedWorkerId.Length > 0
                    && !characterIds.Contains(order.reservedWorkerId)
                || order.requiredMaterialAmount < 1
                || order.requiredMaterialAmount > MaximumMaterialAmount
                || !IsCanonical(order.materialItemId)
                || !services.ResourceCatalog.TryGetItem(order.materialItemId, out _)
                || !IsFiniteGreaterThan(order.requiredWork, 0f)
                || !IsFiniteRange(order.completedWork, 0f, order.requiredWork)
                || !IsFiniteRange(order.targetDurability, 0f, 1f)
                || !activeState)
            {
                report.AddError($"Equipment-maintenance order '{orderId}' is structurally invalid.");
                continue;
            }

            highestSequence = Math.Max(highestSequence, sequence);
            if (!services.Equipment.TryGetInstance(
                    order.equipmentInstanceId,
                    out CombatEquipmentInstance instance))
            {
                report.AddError($"Equipment-maintenance order '{orderId}' references missing equipment.");
                continue;
            }
            if (!TryResolveRepairMaterial(
                    instance,
                    services.EquipmentCatalog,
                    services.ResourceCatalog,
                    out string expectedMaterial)
                || !string.Equals(
                    order.materialItemId,
                    expectedMaterial,
                    StringComparison.Ordinal))
            {
                report.AddError($"Equipment-maintenance order '{orderId}' has a mismatched repair material.");
            }
        }
        if (payload.orderSequence < highestSequence)
        {
            report.AddError(
                $"Equipment-maintenance order sequence {payload.orderSequence} is below saved order {highestSequence}.");
        }
    }

    private static bool IsBuiltInPolicy(string id) =>
        id is EquipmentMaintenancePolicyRuntime.StandardPolicyId
            or EquipmentMaintenancePolicyRuntime.PreventivePolicyId
            or EquipmentMaintenancePolicyRuntime.ManualPolicyId;

    private static bool TryParsePositiveSequence(
        string value,
        string prefix,
        out int sequence)
    {
        sequence = 0;
        if (value == null || !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        string suffix = value.Substring(prefix.Length);
        return int.TryParse(
                suffix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out sequence)
            && sequence > 0
            && string.Equals(
                suffix,
                sequence.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static bool TryParseRepairOrderId(
        string value,
        out int sequence)
    {
        sequence = 0;
        if (value == null
            || !value.StartsWith(RepairOrderPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string suffix = value.Substring(RepairOrderPrefix.Length);
        return int.TryParse(
                suffix,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out sequence)
            && sequence > 0
            && string.Equals(
                suffix,
                sequence.ToString("D6", CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsOptionalCharacterId(string value) =>
        value != null
        && (value.Length == 0
            || IsCanonical(value) && ((CharacterId)value).IsValid);

    private static bool IsFiniteRange(float value, float minimum, float maximum) =>
        !float.IsNaN(value)
        && !float.IsInfinity(value)
        && value >= minimum
        && value <= maximum;

    private static bool IsFiniteGreaterThan(float value, float minimum) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value > minimum;
}
