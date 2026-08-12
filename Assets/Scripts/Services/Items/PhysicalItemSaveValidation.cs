using System;
using System.Collections.Generic;
using System.Linq;

internal static class PhysicalItemSaveValidation
{
    internal const int MaxSavedStacks = 262_144;
    internal const int MaxSavedUniqueItems = 65_536;
    private const int MaxComponentsPerItem = 64;
    private const int MaxValuesPerComponent = 256;

    internal static void Validate(
        DungeonPhysicalItemSaveData snapshot,
        DungeonGameRestoreReport report,
        IDungeonItemCatalogProvider catalog)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (catalog == null)
        {
            throw new ArgumentNullException(nameof(catalog));
        }
        if (snapshot == null)
        {
            report.AddError("Physical item payload is null.");
            return;
        }
        if (snapshot.version != DungeonPhysicalItemSaveData.CurrentVersion)
        {
            report.AddError(
                $"Unsupported physical item payload version {snapshot.version}; expected {DungeonPhysicalItemSaveData.CurrentVersion}.");
        }

        ValidateHaulingSettings(snapshot.haulingSettings, report);
        if (snapshot.stacks == null)
        {
            report.AddError("Physical item payload has no stack list.");
            return;
        }
        if (snapshot.uniqueItems == null)
        {
            report.AddError("Physical item payload has no unique-item list.");
            return;
        }
        if (snapshot.reservationIntents == null)
        {
            report.AddError("Physical item payload has no reservation-intent list.");
            return;
        }
        if (snapshot.stacks.Count > MaxSavedStacks)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxSavedStacks}-stack limit.");
        }
        if (snapshot.uniqueItems.Count > MaxSavedUniqueItems)
        {
            report.AddError(
                $"Physical item payload exceeds the {MaxSavedUniqueItems}-unique-item limit.");
        }

        Dictionary<string, UniqueItemInstanceSaveData> uniqueById =
            ValidateUniqueItems(snapshot.uniqueItems, report);
        ValidateStacks(snapshot.stacks, uniqueById, report, catalog);
        ValidateReservationIntents(snapshot, report);
    }

    private static void ValidateReservationIntents(
        DungeonPhysicalItemSaveData snapshot,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, WorldItemStackSaveData> stacks = snapshot.stacks
            .Where(stack => stack != null && !string.IsNullOrWhiteSpace(stack.stackId))
            .ToDictionary(stack => stack.stackId, StringComparer.Ordinal);
        Dictionary<string, int> totalsByStack = new(StringComparer.Ordinal);
        HashSet<string> owners = new(StringComparer.Ordinal);
        HashSet<string> claimIds = new(StringComparer.Ordinal);
        string previousOwner = string.Empty;
        foreach (ItemReservationIntentSaveData intent in snapshot.reservationIntents)
        {
            string owner = intent?.ownerOperationId ?? string.Empty;
            if (intent == null
                || !intent.hadActiveItemReservation
                || !IsCanonicalNonEmpty(owner)
                || !owners.Add(owner)
                || intent.reservationHints == null
                || intent.reservationHints.Count == 0)
            {
                report.AddError($"Invalid reservation intent '{owner}'.");
                continue;
            }
            if (previousOwner.Length > 0
                && string.CompareOrdinal(previousOwner, owner) >= 0)
            {
                report.AddError("Reservation intents are not in canonical owner order.");
            }
            previousOwner = owner;
            int expectedOrdinal = 0;
            foreach (ItemReservationClaimHintSaveData hint in intent.reservationHints)
            {
                string stackId = hint?.preferredPhysicalStackId ?? string.Empty;
                if (hint == null
                    || hint.claimOrdinal != expectedOrdinal++
                    || hint.quantity <= 0
                    || !IsCanonicalNonEmpty(hint.claimHintId)
                    || !IsCanonicalNonEmpty(hint.originStackId)
                    || !claimIds.Add(hint.claimHintId)
                    || !stacks.TryGetValue(stackId, out WorldItemStackSaveData stack)
                    || !string.Equals(stack.itemId, hint.itemId, StringComparison.Ordinal)
                    || !string.Equals(
                        stack.GetStackSignature(),
                        hint.expectedStackSignature,
                        StringComparison.Ordinal)
                    || !Enum.IsDefined(typeof(ItemReservationPurpose), hint.purpose))
                {
                    report.AddError(
                        $"Invalid reservation claim '{hint?.claimHintId}' for owner '{owner}'.");
                    continue;
                }
                totalsByStack[stackId] = totalsByStack.TryGetValue(
                    stackId,
                    out int total)
                    ? checked(total + hint.quantity)
                    : hint.quantity;
            }
        }
        foreach (KeyValuePair<string, int> total in totalsByStack)
        {
            if (stacks[total.Key].quantity < total.Value)
            {
                report.AddError(
                    $"Reservation hints exceed physical stack '{total.Key}': {total.Value}/{stacks[total.Key].quantity}.");
            }
        }
    }

    private static void ValidateHaulingSettings(
        ItemHaulingSettingsSnapshot settings,
        DungeonGameRestoreReport report)
    {
        if (settings == null)
        {
            report.AddError("Physical item payload has no hauling settings.");
            return;
        }

        float value = settings.maxCarryMultiplier;
        float steps = value / 0.05f;
        if (float.IsNaN(value)
            || float.IsInfinity(value)
            || value < 1f
            || value > 2.5f
            || Math.Abs(steps - Math.Round(steps)) > 0.0001d)
        {
            report.AddError(
                $"Physical item hauling multiplier {value} is not canonical.");
        }
    }

    private static Dictionary<string, UniqueItemInstanceSaveData> ValidateUniqueItems(
        IReadOnlyList<UniqueItemInstanceSaveData> savedItems,
        DungeonGameRestoreReport report)
    {
        Dictionary<string, UniqueItemInstanceSaveData> result =
            new(StringComparer.Ordinal);
        HashSet<string> moduleIds = new(StringComparer.Ordinal);
        string previousId = string.Empty;
        for (int index = 0; index < savedItems.Count; index++)
        {
            UniqueItemInstanceSaveData unique = savedItems[index];
            string instanceId = unique?.itemInstanceId ?? string.Empty;
            ItemInstanceId typedId = (ItemInstanceId)instanceId;
            if (unique == null
                || !typedId.IsValid
                || !string.Equals(instanceId, typedId.Value, StringComparison.Ordinal)
                || !result.TryAdd(instanceId, unique))
            {
                report.AddError(
                    $"Physical unique item {index} has invalid or duplicate ID '{instanceId}'.");
                continue;
            }
            if (previousId.Length > 0
                && string.CompareOrdinal(previousId, instanceId) >= 0)
            {
                report.AddError(
                    "Physical unique items must use canonical ascending instance-ID order.");
            }
            previousId = instanceId;

            string definitionId = unique.definitionId ?? string.Empty;
            if (!IsCanonicalNonEmpty(definitionId))
            {
                report.AddError(
                    $"Physical unique item '{instanceId}' has a non-canonical definition ID.");
            }
            ValidateComponents(unique.components, $"unique item '{instanceId}'", report);

            ItemInstanceComponentSaveData equipmentComponent = null;
            ItemInstanceComponentSaveData moduleComponent = null;
            if (unique.components != null)
            {
                foreach (ItemInstanceComponentSaveData component in unique.components)
                {
                    if (component != null
                        && string.Equals(
                            component.componentTypeId,
                            ItemInstanceComponentIds.Equipment,
                            StringComparison.Ordinal))
                    {
                        if (equipmentComponent != null)
                        {
                            report.AddError(
                                $"Physical unique item '{instanceId}' has duplicate equipment state.");
                        }
                        equipmentComponent = component;
                    }
                    if (component != null
                        && string.Equals(
                            component.componentTypeId,
                            ItemInstanceComponentIds.EquipmentModule,
                            StringComparison.Ordinal))
                    {
                        if (moduleComponent != null)
                        {
                            report.AddError(
                                $"Physical unique item '{instanceId}' has duplicate equipment-module state.");
                        }
                        moduleComponent = component;
                    }
                }
            }

            if (PhysicalItemIds.IsEquipmentModule(definitionId))
            {
                string moduleDecodeError = "missing equipment-module state";
                if (equipmentComponent != null
                    || moduleComponent == null
                    || !EquipmentModuleItemStateCodec.TryDecode(
                        moduleComponent,
                        out EquipmentModuleInstance module,
                        out moduleDecodeError))
                {
                    report.AddError(
                        $"Physical unique item '{instanceId}' has invalid equipment-module state: {moduleDecodeError}.");
                    continue;
                }
                if (!string.Equals(
                        module.instanceId,
                        instanceId,
                        StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(module.sourceStackId)
                    || !string.IsNullOrWhiteSpace(
                        module.attachedEquipmentInstanceId)
                    || !moduleIds.Add(module.instanceId))
                {
                    report.AddError(
                        $"Physical unique item '{instanceId}' does not match its independent equipment-module identity.");
                }
                continue;
            }

            if (moduleComponent != null)
            {
                report.AddError(
                    $"Physical unique item '{instanceId}' mixes equipment and independent module state.");
            }

            EquipmentPhysicalStatePayload payload = null;
            string decodeError = "missing equipment state";
            if (equipmentComponent == null
                || !EquipmentItemStateCodec.TryDecodeFull(
                    equipmentComponent,
                    out payload,
                    out decodeError))
            {
                report.AddError(
                    $"Physical unique item '{instanceId}' has invalid equipment state: {decodeError ?? "missing equipment state"}.");
                continue;
            }

            string expectedDefinition =
                PhysicalItemIds.ForEquipment(payload.equipment.definitionId);
            if (!string.Equals(
                    payload.equipment.instanceId,
                    instanceId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    definitionId,
                    expectedDefinition,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Physical unique item '{instanceId}' does not match its equipment identity or definition.");
            }

            foreach (EquipmentModuleInstance module in
                     payload.attachedModules ?? new List<EquipmentModuleInstance>())
            {
                string moduleId = module?.instanceId ?? string.Empty;
                if (module == null
                    || !IsCanonicalNonEmpty(moduleId)
                    || !string.IsNullOrWhiteSpace(module.sourceStackId)
                    || !string.Equals(
                        module.attachedEquipmentInstanceId,
                        payload.equipment.instanceId,
                        StringComparison.Ordinal)
                    || !moduleIds.Add(moduleId))
                {
                    report.AddError(
                        $"Physical unique item '{instanceId}' has an invalid or duplicate module '{moduleId}'.");
                }
            }
        }

        return result;
    }

    private static void ValidateStacks(
        IReadOnlyList<WorldItemStackSaveData> stacks,
        IReadOnlyDictionary<string, UniqueItemInstanceSaveData> uniqueById,
        DungeonGameRestoreReport report,
        IDungeonItemCatalogProvider catalog)
    {
        HashSet<string> stackIds = new(StringComparer.Ordinal);
        HashSet<string> stackedInstanceIds = new(StringComparer.Ordinal);
        WorldItemStackSaveData previous = null;
        for (int index = 0; index < stacks.Count; index++)
        {
            WorldItemStackSaveData stack = stacks[index];
            string stackId = stack?.stackId ?? string.Empty;
            ItemStackId typedStackId = (ItemStackId)stackId;
            if (stack == null
                || !typedStackId.IsValid
                || !string.Equals(stackId, typedStackId.Value, StringComparison.Ordinal)
                || !stackIds.Add(stackId))
            {
                report.AddError(
                    $"Physical stack {index} has invalid or duplicate ID '{stackId}'.");
                continue;
            }
            if (previous != null && CompareStackOrder(previous, stack) >= 0)
            {
                report.AddError(
                    "Physical stacks must use canonical y/x/item/stack-ID order.");
            }
            previous = stack;

            string itemId = stack.itemId ?? string.Empty;
            if (!IsCanonicalNonEmpty(itemId)
                || !catalog.TryGetDefinition(itemId, out DungeonItemDefinition definition))
            {
                report.AddError(
                    $"Physical stack '{stackId}' references unknown or non-canonical item '{itemId}'.");
                continue;
            }
            if (stack.quantity <= 0 || stack.quantity > definition.MaxStack)
            {
                report.AddError(
                    $"Physical stack '{stackId}' has invalid quantity {stack.quantity}/{definition.MaxStack}.");
            }
            if (!Enum.IsDefined(typeof(WorldItemStackState), stack.state))
            {
                report.AddError(
                    $"Physical stack '{stackId}' has invalid state {stack.state}.");
            }
            if (stack.reservedByPersistentId == null
                || stack.reservedByPersistentId.Length != 0)
            {
                report.AddError(
                    $"Physical stack '{stackId}' contains transient reservation state.");
            }
            if (!IsCanonicalText(stack.destinationId)
                || !IsCanonicalText(stack.aggregationCohortId)
                || !IsCanonicalText(stack.sourceStorageDestinationId)
                || !IsCanonicalText(stack.sourceCharacterId)
                || !IsCanonicalText(stack.sourceDisplayName)
                || !IsCanonicalText(stack.sourceSpeciesTag)
                || !IsCanonicalText(stack.sourceDeathReason))
            {
                report.AddError(
                    $"Physical stack '{stackId}' contains non-canonical text fields.");
            }
            if (!stack.hasDestinationPosition
                && (stack.destinationGridX != 0 || stack.destinationGridY != 0))
            {
                report.AddError(
                    $"Physical stack '{stackId}' has stale destination coordinates.");
            }
            if (!string.IsNullOrEmpty(stack.destinationId)
                && stack.destinationId.StartsWith(
                    WorldItemStackRuntime.CombatLoadoutDestinationPrefix,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Physical stack '{stackId}' contains transient combat-loadout routing.");
            }
            if (!Enum.IsDefined(typeof(WasteOriginKind), stack.wasteOrigin)
                || float.IsNaN(stack.contamination)
                || float.IsInfinity(stack.contamination)
                || stack.contamination < 0f
                || stack.contamination > 100f)
            {
                report.AddError(
                    $"Physical stack '{stackId}' has invalid waste or contamination state.");
            }
            ValidateComponents(stack.components, $"stack '{stackId}'", report);

            string instanceId = stack.itemInstanceId ?? string.Empty;
            ItemInstanceId typedInstanceId = (ItemInstanceId)instanceId;
            if (definition.MaxStack == 1 && !typedInstanceId.IsValid)
            {
                report.AddError(
                    $"Unique physical stack '{stackId}' has no item-instance ID.");
            }
            if (instanceId.Length == 0)
            {
                continue;
            }
            bool equipmentBacked = PhysicalItemIds.TryGetEquipmentDefinitionId(
                    itemId,
                    out _)
                || PhysicalItemIds.IsEquipmentModule(itemId);
            UniqueItemInstanceSaveData unique = null;
            if (!typedInstanceId.IsValid
                || !string.Equals(instanceId, typedInstanceId.Value, StringComparison.Ordinal)
                || !stackedInstanceIds.Add(instanceId))
            {
                report.AddError(
                    $"Physical stack '{stackId}' has an invalid or duplicate item-instance ID '{instanceId}'.");
                continue;
            }
            if (equipmentBacked
                && (!uniqueById.TryGetValue(instanceId, out unique)
                    || !string.Equals(unique.definitionId, itemId, StringComparison.Ordinal)))
            {
                report.AddError(
                    $"Physical equipment stack '{stackId}' has no matching authoritative item instance '{instanceId}'.");
            }
            else if (!equipmentBacked && uniqueById.ContainsKey(instanceId))
            {
                report.AddError(
                    $"Inline-authority unique stack '{stackId}' must not duplicate item instance '{instanceId}' in the equipment registry.");
            }
            if (PhysicalItemIds.IsEquipmentModule(itemId)
                && unique != null)
            {
                ItemInstanceComponentSaveData moduleComponent =
                    unique.components?.FirstOrDefault(component =>
                        component != null
                        && string.Equals(
                            component.componentTypeId,
                            ItemInstanceComponentIds.EquipmentModule,
                            StringComparison.Ordinal));
                if (!EquipmentModuleItemStateCodec.TryDecode(
                        moduleComponent,
                        out EquipmentModuleInstance module,
                        out _)
                    || !string.Equals(
                        module.sourceStackId,
                        stackId,
                        StringComparison.Ordinal))
                {
                    report.AddError(
                        $"Physical equipment-module stack '{stackId}' does not match its module source stack.");
                }
            }
        }

        foreach (UniqueItemInstanceSaveData unique in uniqueById.Values)
        {
            if (unique != null
                && PhysicalItemIds.IsEquipmentModule(unique.definitionId)
                && !stackedInstanceIds.Contains(unique.itemInstanceId))
            {
                report.AddError(
                    $"Physical equipment module '{unique.itemInstanceId}' has no authoritative stack.");
            }
        }
    }

    private static void ValidateComponents(
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        string owner,
        DungeonGameRestoreReport report)
    {
        if (components == null)
        {
            report.AddError($"Physical {owner} has no component list.");
            return;
        }
        if (components.Count > MaxComponentsPerItem)
        {
            report.AddError(
                $"Physical {owner} exceeds the {MaxComponentsPerItem}-component limit.");
        }

        HashSet<string> componentIds = new(StringComparer.Ordinal);
        foreach (ItemInstanceComponentSaveData component in components)
        {
            string componentId = component?.componentTypeId ?? string.Empty;
            if (component == null
                || !IsCanonicalNonEmpty(componentId)
                || component.schemaVersion < 1
                || !componentIds.Add(componentId))
            {
                report.AddError(
                    $"Physical {owner} has an invalid or duplicate component '{componentId}'.");
                continue;
            }
            if (component.values == null)
            {
                report.AddError(
                    $"Physical {owner} component '{componentId}' has no value list.");
                continue;
            }
            if (component.values.Count > MaxValuesPerComponent)
            {
                report.AddError(
                    $"Physical {owner} component '{componentId}' exceeds the {MaxValuesPerComponent}-value limit.");
            }

            HashSet<string> keys = new(StringComparer.Ordinal);
            foreach (ItemStateValueSaveData value in component.values)
            {
                string key = value?.key ?? string.Empty;
                if (value == null
                    || !IsCanonicalNonEmpty(key)
                    || !Enum.IsDefined(typeof(ItemStateValueKind), value.kind)
                    || !keys.Add(key)
                    || (value.kind == ItemStateValueKind.Decimal
                        && (double.IsNaN(value.decimalValue)
                            || double.IsInfinity(value.decimalValue))))
                {
                    report.AddError(
                        $"Physical {owner} component '{componentId}' has invalid or duplicate value '{key}'.");
                }
            }
        }
    }

    private static int CompareStackOrder(
        WorldItemStackSaveData left,
        WorldItemStackSaveData right)
    {
        int comparison = left.gridY.CompareTo(right.gridY);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = left.gridX.CompareTo(right.gridX);
        if (comparison != 0)
        {
            return comparison;
        }
        comparison = string.CompareOrdinal(left.itemId, right.itemId);
        return comparison != 0
            ? comparison
            : string.CompareOrdinal(left.stackId, right.stackId);
    }

    private static bool IsCanonicalNonEmpty(string value)
    {
        return !string.IsNullOrEmpty(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static bool IsCanonicalText(string value)
    {
        return value != null
            && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }
}
