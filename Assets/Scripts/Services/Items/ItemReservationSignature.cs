using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Stable identity for a quantity reservation. Physical stack compatibility
/// remains stricter and still includes exact freshness and the equipment's
/// current world location. A lease deliberately ignores freshness and an
/// equipment instance's worldState because both normally change while the
/// reservation owner walks, picks up and deposits the exact same item.
/// Every other equipment field and attached module remains exact.
/// </summary>
public static class ItemReservationSignature
{
    public static string Create(
        string definitionId,
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        return ItemStackSignature.Create(
            definitionId,
            NormalizeComponents(components));
    }

    private static IEnumerable<ItemInstanceComponentSaveData> NormalizeComponents(
        IEnumerable<ItemInstanceComponentSaveData> components)
    {
        foreach (ItemInstanceComponentSaveData component in
                 components ?? Array.Empty<ItemInstanceComponentSaveData>())
        {
            if (component == null
                || string.Equals(
                    component.componentTypeId,
                    ItemInstanceComponentIds.Freshness,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(
                    component.componentTypeId,
                    ItemInstanceComponentIds.Equipment,
                    StringComparison.Ordinal)
                && EquipmentItemStateCodec.TryDecodeFull(
                    component,
                    out EquipmentPhysicalStatePayload payload,
                    out _))
            {
                CombatEquipmentInstance normalized = payload.equipment.Clone();
                normalized.worldState = CombatEquipmentWorldState.Stored;
                yield return EquipmentItemStateCodec.Encode(
                    normalized,
                    payload.attachedModules);
                continue;
            }

            // Unknown or malformed components remain exact. Reservation identity
            // must never hide invalid state by silently removing it.
            yield return component;
        }
    }
}
