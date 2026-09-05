using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class
    CharacterMedicalSupplyDestinationDrainCrossAggregateSaveValidation :
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
                ReadRequired<DungeonCharacterMedicalSaveData>(
                    saveData,
                    CharacterMedicalSaveSection.Id),
                ReadRequired<DungeonPhysicalItemSaveData>(
                    saveData,
                    PhysicalItemsSaveSection.Id));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Character medical destination-drain preflight failed: "
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
                ParseRequired<DungeonCharacterMedicalSaveData>(
                    envelopes,
                    CharacterMedicalSaveSection.Id,
                    DungeonCharacterMedicalSaveData.CurrentVersion),
                ParseRequired<DungeonPhysicalItemSaveData>(
                    envelopes,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Character medical destination-drain registry preflight failed: "
                + exception.Message);
        }
    }

    internal static void ValidatePayloads(
        DungeonCharacterMedicalSaveData medical,
        DungeonPhysicalItemSaveData physical)
    {
        if (medical?.orders == null
            || physical?.pendingProductionInputDestinationDrains == null)
        {
            throw new InvalidOperationException(
                "Character medical destination-drain join collections are missing.");
        }
        FacilityBufferDestinationCustodyDrainSnapshot[] children = physical
            .pendingProductionInputDestinationDrains
            .Where(value => value != null)
            .Select(FacilityBufferDestinationCustodyDrainProjection
                .ProjectValidated)
            .ToArray();
        CharacterMedicalSupplyDestinationDrainCrossAggregateJoin.Validate(
            medical.orders,
            children);
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
            || !string.Equals(envelope.sectionId, sectionId,
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
