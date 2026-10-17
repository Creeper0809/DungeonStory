using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DurableFacilityEquipmentCrossAggregateSaveValidation :
    IDungeonSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator,
    IDungeonCapturedSavePreflightValidator
{
    public void Validate(
        DungeonGameSaveData saveData,
        DungeonGameRestoreReport report)
    {
        if (saveData == null)
            throw new ArgumentNullException(nameof(saveData));
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        try
        {
            ValidatePayloads(
                ReadRequired<DungeonDurableFacilityEquipmentSaveData>(
                    saveData,
                    DurableFacilityEquipmentPersistenceContract.SectionId),
                ReadRequired<DungeonPhysicalItemSaveData>(
                    saveData,
                    PhysicalItemsSaveSection.Id),
                ReadRequired<ModularFacilityWorldSaveData>(
                    saveData,
                    ModularFacilityWorldSaveSection.Id));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Durable facility-equipment cross-aggregate preflight failed: "
                + exception.Message);
        }
    }

    public void Validate(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        DungeonGameRestoreReport report)
    {
        if (envelopes == null)
            throw new ArgumentNullException(nameof(envelopes));
        if (report == null)
            throw new ArgumentNullException(nameof(report));
        try
        {
            ValidatePayloads(
                ParseRequired<DungeonDurableFacilityEquipmentSaveData>(
                    envelopes,
                    DurableFacilityEquipmentPersistenceContract.SectionId,
                    DungeonDurableFacilityEquipmentSaveData.CurrentVersion),
                ParseRequired<DungeonPhysicalItemSaveData>(
                    envelopes,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion),
                ParseRequired<ModularFacilityWorldSaveData>(
                    envelopes,
                    ModularFacilityWorldSaveSection.Id,
                    ModularFacilityWorldSaveSection.CurrentSectionVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Durable facility-equipment registry preflight failed: "
                + exception.Message);
        }
    }

    internal static void ValidatePayloads(
        DungeonDurableFacilityEquipmentSaveData upper,
        DungeonPhysicalItemSaveData physical,
        ModularFacilityWorldSaveData facilities)
    {
        if (upper?.slots == null
            || physical?.pendingProductionInputDestinationDrains == null
            || facilities?.buildings == null)
        {
            throw new InvalidOperationException(
                "Durable facility-equipment join collections are missing.");
        }
        FacilityBufferDestinationCustodyDrainSnapshot[] children = physical
            .pendingProductionInputDestinationDrains
            .Where(value => value != null)
            .Select(FacilityBufferDestinationCustodyDrainProjection
                .ProjectValidated)
            .ToArray();
        DurableFacilityEquipmentCrossAggregateJoin.Validate(
            upper.slots,
            children,
            facilities.buildings);
    }

    private static TPayload ReadRequired<TPayload>(
        DungeonGameSaveData saveData,
        string sectionId)
        where TPayload : class, new()
    {
        if (!DungeonSaveSectionPayload.TryRead(
                saveData,
                sectionId,
                out TPayload payload))
        {
            throw new InvalidOperationException(
                "Required save section is missing: " + sectionId);
        }
        return payload;
    }

    private static TPayload ParseRequired<TPayload>(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        int currentVersion)
        where TPayload : class
    {
        if (!envelopes.TryGetValue(
                sectionId,
                out DungeonSaveSectionEnvelope envelope)
            || envelope == null
            || !string.Equals(
                envelope.sectionId,
                sectionId,
                StringComparison.Ordinal)
            || envelope.sectionVersion != currentVersion
            || string.IsNullOrWhiteSpace(envelope.payloadJson))
        {
            throw new InvalidOperationException(
                "Save section envelope is not exact current format: "
                + sectionId);
        }
        return JsonUtility.FromJson<TPayload>(envelope.payloadJson)
            ?? throw new InvalidOperationException(
                "Save section payload deserialized to null: " + sectionId);
    }
}
