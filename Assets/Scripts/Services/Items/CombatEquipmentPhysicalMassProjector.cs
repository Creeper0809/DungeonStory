using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Adapts validated runtime physical components into the immutable subject consumed by
/// the mass authority. The mass query never reads save DTOs directly.
/// </summary>
public static class PhysicalItemMassSubjectAdapter
{
    private const string StateJsonKey = "state-json";

    public static PhysicalItemMassSubject Create(
        IPhysicalItemMassQuery massQuery,
        ItemDefinitionId itemId,
        string itemInstanceId,
        IReadOnlyList<ItemInstanceComponentSaveData> components)
    {
        if (massQuery == null)
        {
            throw new ArgumentNullException(nameof(massQuery));
        }

        ItemInstanceComponentSaveData[] equipmentComponents = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null
                && string.Equals(
                    component.componentTypeId,
                    ItemInstanceComponentIds.Equipment,
                    StringComparison.Ordinal))
            .ToArray();
        ItemInstanceComponentSaveData[] apparelComponents = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(component => component != null
                && string.Equals(
                    component.componentTypeId,
                    ItemInstanceComponentIds.Apparel,
                    StringComparison.Ordinal))
            .ToArray();

        bool equipmentItem = PhysicalItemIds.TryGetEquipmentDefinitionId(
            itemId.Value,
            out _);
        if (equipmentComponents.Length > 0 && apparelComponents.Length > 0)
        {
            throw new InvalidOperationException(
                $"Physical item '{itemId.Value}' cannot be equipment and apparel simultaneously.");
        }
        if (equipmentComponents.Length == 0)
        {
            if (equipmentItem)
            {
                throw new InvalidOperationException(
                    $"Combat-equipment physical item '{itemId.Value}' has no equipment component.");
            }
            if (apparelComponents.Length > 0)
            {
                return CreateApparelSubject(
                    massQuery,
                    itemId,
                    itemInstanceId,
                    components,
                    apparelComponents);
            }
            if (WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
                    itemId.Value,
                    out string carcassSpeciesId))
            {
                return CreateWildlifeCarcassSubject(
                    massQuery,
                    itemId,
                    carcassSpeciesId);
            }
            if (massQuery is IPackagedLotDefinitionQuery packagedLots
                && packagedLots.TryGetPackagedLot(
                    itemId,
                    out PackagedLotDefinitionSnapshot packagedLot))
            {
                return CreatePackagedLotSubject(itemId, packagedLot);
            }
            return string.IsNullOrEmpty(itemInstanceId)
                ? PhysicalItemMassSubject.ForDefinition(itemId)
                : new PhysicalItemMassSubject(
                    itemId,
                    itemInstanceId,
                    PhysicalItemMassSubjectKind.GenericDefinition,
                    Array.Empty<PhysicalItemComponentSnapshot>(),
                    string.Empty);
        }
        if (equipmentComponents.Length != 1 || !equipmentItem)
        {
            throw new InvalidOperationException(
                $"Physical item '{itemId.Value}' has an invalid equipment component cardinality.");
        }

        ItemInstanceComponentSaveData component = equipmentComponents[0];
        if (!EquipmentItemStateCodec.TryDecodeFull(
                component,
                out EquipmentPhysicalStatePayload payload,
                out string error))
        {
            throw new InvalidOperationException(
                $"Combat-equipment mass payload is invalid: {error}");
        }
        ValidatePayload(itemId, itemInstanceId, payload);

        string canonicalPayload = component.values?
            .SingleOrDefault(value => value != null
                && string.Equals(value.key, StateJsonKey, StringComparison.Ordinal)
                && value.kind == ItemStateValueKind.String)?
            .stringValue;
        if (string.IsNullOrWhiteSpace(canonicalPayload))
        {
            throw new InvalidOperationException(
                "Combat-equipment mass payload has no canonical state JSON.");
        }

        string lotFingerprint = ItemReservationSignature.Create(
            itemId.Value,
            components);
        List<PhysicalItemMassContribution> massContributions = new(2);
        int attachedModuleCount = payload.attachedModules?.Count ?? 0;
        if (attachedModuleCount > 0)
        {
            massContributions.Add(new PhysicalItemMassContribution(
                (ItemDefinitionId)PhysicalItemIds.ForEquipmentModule(),
                attachedModuleCount));
        }

        LoadedAmmunitionBatch ammunition = payload.equipment.loadedAmmunition;
        if (ammunition != null && ammunition.remaining > 0)
        {
            massContributions.Add(new PhysicalItemMassContribution(
                (ItemDefinitionId)ammunition.ammunitionItemId,
                ammunition.remaining));
        }

        PhysicalMassGrams preparedUnitMass =
            massQuery.GetDefinitionUnitMass(itemId);
        for (int index = 0; index < massContributions.Count; index++)
        {
            PhysicalItemMassContribution contribution = massContributions[index];
            preparedUnitMass = preparedUnitMass.Add(
                massQuery.GetDefinitionUnitMass(contribution.ItemId)
                    .Multiply(contribution.Quantity));
        }

        PhysicalItemComponentSnapshot snapshot = new(
            component.componentTypeId,
            component.schemaVersion,
            canonicalPayload,
            component.ToCanonicalString(),
            massContributions,
            preparedUnitMass);
        return new PhysicalItemMassSubject(
            itemId,
            itemInstanceId,
            PhysicalItemMassSubjectKind.CombatEquipment,
            new[] { snapshot },
            lotFingerprint);
    }

    private static PhysicalItemMassSubject CreatePackagedLotSubject(
        ItemDefinitionId itemId,
        PackagedLotDefinitionSnapshot packagedLot)
    {
        string payload = $"{packagedLot.TareMass.Value}:"
            + $"{(int)packagedLot.TareDisposition}:"
            + packagedLot.ContainerItemId.Value;
        PhysicalItemComponentSnapshot snapshot = new(
            PackagedLotPhysicalItemMassProjector.ComponentTypeId,
            PackagedLotPhysicalItemMassProjector.SchemaVersion,
            payload,
            $"{PackagedLotPhysicalItemMassProjector.ComponentTypeId}:{payload}",
            preparedUnitMass: packagedLot.TotalUnitMass);
        return new PhysicalItemMassSubject(
            itemId,
            string.Empty,
            PhysicalItemMassSubjectKind.PackagedLot,
            new[] { snapshot },
            snapshot.Fingerprint);
    }

    private static PhysicalItemMassSubject CreateWildlifeCarcassSubject(
        IPhysicalItemMassQuery massQuery,
        ItemDefinitionId itemId,
        string speciesId)
    {
        if (string.IsNullOrWhiteSpace(speciesId)
            || !string.Equals(speciesId, speciesId.Trim(), StringComparison.Ordinal)
            || !string.Equals(
                itemId.Value,
                WildlifeItemDefinitions.GetCarcassItemId(speciesId),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Wildlife carcass '{itemId.Value}' has a non-canonical species identity.");
        }

        PhysicalMassGrams preparedUnitMass =
            massQuery.GetDefinitionUnitMass(itemId);
        string fingerprint =
            WildlifeCarcassPhysicalItemMassProjector.ComponentTypeId
            + ":"
            + speciesId;
        PhysicalItemComponentSnapshot snapshot = new(
            WildlifeCarcassPhysicalItemMassProjector.ComponentTypeId,
            WildlifeCarcassPhysicalItemMassProjector.SchemaVersion,
            speciesId,
            fingerprint,
            preparedUnitMass: preparedUnitMass);
        return new PhysicalItemMassSubject(
            itemId,
            string.Empty,
            PhysicalItemMassSubjectKind.WildlifeCarcass,
            new[] { snapshot },
            fingerprint);
    }

    private static PhysicalItemMassSubject CreateApparelSubject(
        IPhysicalItemMassQuery massQuery,
        ItemDefinitionId itemId,
        string itemInstanceId,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        IReadOnlyList<ItemInstanceComponentSaveData> apparelComponents)
    {
        string instanceId = itemInstanceId ?? string.Empty;
        if (apparelComponents.Count != 1
            || instanceId.Length == 0
            || !string.Equals(instanceId, instanceId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Apparel physical item '{itemId.Value}' has invalid component identity.");
        }

        ItemInstanceComponentSaveData component = apparelComponents[0];
        if (component.schemaVersion != ApparelItemStateCodec.SchemaVersion
            || !component.affectsStacking
            || !ApparelItemStateCodec.TryRead(
                new[] { component },
                out ApparelInstanceState state)
            || state == null
            || string.IsNullOrWhiteSpace(state.apparelDefinitionId)
            || !string.Equals(
                state.apparelDefinitionId,
                state.apparelDefinitionId.Trim(),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Apparel physical item '{itemId.Value}' has invalid canonical state.");
        }

        string canonicalPayload = component.ToCanonicalString();
        PhysicalMassGrams preparedUnitMass =
            massQuery.GetDefinitionUnitMass(itemId);
        PhysicalItemComponentSnapshot snapshot = new(
            component.componentTypeId,
            component.schemaVersion,
            canonicalPayload,
            canonicalPayload,
            preparedUnitMass: preparedUnitMass);
        return new PhysicalItemMassSubject(
            itemId,
            instanceId,
            PhysicalItemMassSubjectKind.Apparel,
            new[] { snapshot },
            ItemReservationSignature.Create(itemId.Value, components));
    }

    internal static void ValidatePayload(
        ItemDefinitionId itemId,
        string itemInstanceId,
        EquipmentPhysicalStatePayload payload)
    {
        CombatEquipmentInstance equipment = payload?.equipment
            ?? throw new InvalidOperationException(
                "Combat-equipment mass payload has no equipment instance.");
        string instanceId = itemInstanceId ?? string.Empty;
        if (instanceId.Length == 0
            || !string.Equals(instanceId, instanceId.Trim(), StringComparison.Ordinal)
            || !string.Equals(equipment.instanceId, instanceId, StringComparison.Ordinal)
            || !string.Equals(
                PhysicalItemIds.ForEquipment(equipment.definitionId),
                itemId.Value,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Combat-equipment mass identity mismatch for '{itemId.Value}'.");
        }

        Dictionary<string, EquipmentModuleInstance> attached = new(
            StringComparer.Ordinal);
        foreach (EquipmentModuleInstance module in payload.attachedModules
                     ?? new List<EquipmentModuleInstance>())
        {
            if (module == null
                || string.IsNullOrWhiteSpace(module.instanceId)
                || !string.Equals(
                    module.instanceId,
                    module.instanceId.Trim(),
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(module.definitionId)
                || !string.Equals(
                    module.definitionId,
                    module.definitionId.Trim(),
                    StringComparison.Ordinal)
                || !string.IsNullOrWhiteSpace(module.sourceStackId)
                || !string.Equals(
                    module.attachedEquipmentInstanceId,
                    equipment.instanceId,
                    StringComparison.Ordinal)
                || module.state != EquipmentModuleProcessState.Installed
                || !attached.TryAdd(module.instanceId, module))
            {
                throw new InvalidOperationException(
                    $"Combat-equipment '{equipment.instanceId}' has invalid attached-module mass state.");
            }
        }

        string[] slottedModuleIds = (equipment.moduleSlots
                ?? new List<EquipmentModuleSlotState>())
            .Where(slot => slot != null
                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId))
            .Select(slot => slot.moduleInstanceId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] attachedModuleIds = attached.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!slottedModuleIds.SequenceEqual(
                attachedModuleIds,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Combat-equipment '{equipment.instanceId}' module slots do not match its physical payload.");
        }

        LoadedAmmunitionBatch ammunition = equipment.loadedAmmunition;
        if (ammunition != null
            && ammunition.remaining > 0
            && (string.IsNullOrWhiteSpace(ammunition.ammunitionItemId)
                || !string.Equals(
                    ammunition.ammunitionItemId,
                    ammunition.ammunitionItemId.Trim(),
                    StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Combat-equipment '{equipment.instanceId}' has non-canonical loaded ammunition.");
        }
    }
}

public sealed class CombatEquipmentPhysicalItemMassProjector :
    IPhysicalItemMassProjector
{
    public CombatEquipmentPhysicalItemMassProjector(
        IPhysicalItemDefinitionMassProjector definitions)
    {
        _ = definitions ?? throw new ArgumentNullException(nameof(definitions));
    }

    public PhysicalItemMassSubjectKind SubjectKind =>
        PhysicalItemMassSubjectKind.CombatEquipment;

    public PhysicalMassGrams GetUnitMass(PhysicalItemMassSubject subject)
    {
        if (subject == null
            || subject.Kind != SubjectKind
            || subject.Components.Count != 1)
        {
            throw new ArgumentException(
                "Combat-equipment mass requires one validated equipment component.",
                nameof(subject));
        }

        PhysicalItemComponentSnapshot component = subject.Components[0];
        if (!string.Equals(
                component.ComponentTypeId,
                ItemInstanceComponentIds.Equipment,
                StringComparison.Ordinal)
            || component.SchemaVersion != EquipmentItemStateCodec.CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                "Combat-equipment mass component type or schema is invalid.");
        }

        if (!component.PreparedUnitMass.HasValue)
        {
            throw new InvalidOperationException(
                "Combat-equipment mass subject has no prepared unit-mass authority.");
        }

        return component.PreparedUnitMass.Value;
    }
}
