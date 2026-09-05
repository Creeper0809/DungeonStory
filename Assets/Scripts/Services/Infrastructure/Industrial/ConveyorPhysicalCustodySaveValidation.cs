using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Joins durable conveyor payload intent to the exact physical InTransit lot
/// in both directions before any aggregate is published. Conveyor routes are
/// deliberately rebuilt from the current topology after restore; this
/// validator owns custody cardinality, not a frozen route reservation.
/// </summary>
public sealed class ConveyorPhysicalCustodySaveValidation :
    IDungeonSavePreflightValidator,
    IDungeonSaveRegistryPreflightValidator
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
            ValidateCore(
                ReadRequired<DungeonPhysicalItemSaveData>(
                    saveData.sections,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion),
                ReadRequired<DungeonConveyorInfrastructureSaveData>(
                    saveData.sections,
                    ConveyorInfrastructureSaveSection.Id,
                    DungeonConveyorInfrastructureSaveData.CurrentVersion),
                ReadRequired<ModularFacilityWorldSaveData>(
                    saveData.sections,
                    ModularFacilityWorldSaveSection.Id,
                    ModularFacilityWorldSaveSection.CurrentSectionVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Conveyor physical-custody save preflight failed: "
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
            ValidateCore(
                ReadRequired<DungeonPhysicalItemSaveData>(
                    envelopes,
                    PhysicalItemsSaveSection.Id,
                    DungeonPhysicalItemSaveData.CurrentVersion),
                ReadRequired<DungeonConveyorInfrastructureSaveData>(
                    envelopes,
                    ConveyorInfrastructureSaveSection.Id,
                    DungeonConveyorInfrastructureSaveData.CurrentVersion),
                ReadRequired<ModularFacilityWorldSaveData>(
                    envelopes,
                    ModularFacilityWorldSaveSection.Id,
                    ModularFacilityWorldSaveSection.CurrentSectionVersion));
        }
        catch (Exception exception)
        {
            report.AddError(
                "Conveyor physical-custody registry preflight failed: "
                + exception.Message);
        }
    }

    public static void ValidateCore(
        DungeonPhysicalItemSaveData physical,
        DungeonConveyorInfrastructureSaveData conveyor,
        ModularFacilityWorldSaveData facilities)
    {
        if (physical?.stacks == null
            || conveyor?.payloads == null
            || facilities?.buildings == null)
        {
            throw new InvalidOperationException(
                "Conveyor custody requires physical, conveyor and facility collections.");
        }

        Dictionary<string, WorldItemStackSaveData[]> stacksById = physical
            .stacks
            .Where(value => value != null)
            .GroupBy(value => value.stackId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        HashSet<string> buildingIds = facilities.buildings
            .Where(value => value != null)
            .Select(value => value.persistentInstanceId)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, ConveyorPayloadSaveData> payloadsById =
            new(StringComparer.Ordinal);
        HashSet<string> claimedStackIds = new(StringComparer.Ordinal);

        foreach (ConveyorPayloadSaveData payload in conveyor.payloads)
        {
            if (payload == null
                || string.IsNullOrEmpty(payload.payloadId)
                || !payloadsById.TryAdd(payload.payloadId, payload))
            {
                throw new InvalidOperationException(
                    "Conveyor payload identity is missing or duplicated.");
            }
            if (string.IsNullOrEmpty(payload.itemStackId)
                || !claimedStackIds.Add(payload.itemStackId))
            {
                throw new InvalidOperationException(
                    $"Conveyor payload '{payload.payloadId}' does not own a unique physical stack.");
            }
            if (!stacksById.TryGetValue(
                    payload.itemStackId,
                    out WorldItemStackSaveData[] matchingStacks)
                || matchingStacks.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Conveyor payload '{payload.payloadId}' has no exact physical stack '{payload.itemStackId}'.");
            }

            WorldItemStackSaveData stack = matchingStacks[0];
            if (stack.state != WorldItemStackState.InTransit
                || !string.Equals(
                    stack.destinationId,
                    payload.payloadId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Conveyor payload '{payload.payloadId}' does not own its exact InTransit lot.");
            }
            if (!buildingIds.Contains(payload.segmentBuildingInstanceId))
            {
                throw new InvalidOperationException(
                    $"Conveyor payload '{payload.payloadId}' references missing current segment '{payload.segmentBuildingInstanceId}'.");
            }
        }

        foreach (WorldItemStackSaveData stack in physical.stacks.Where(
                     value => value?.state == WorldItemStackState.InTransit))
        {
            if (!payloadsById.TryGetValue(
                    stack.destinationId,
                    out ConveyorPayloadSaveData payload)
                || !string.Equals(
                    payload.itemStackId,
                    stack.stackId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"InTransit physical stack '{stack.stackId}' has no exact conveyor payload owner.");
            }
        }
    }

    private static T ReadRequired<T>(
        IReadOnlyList<DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        int expectedVersion)
        where T : class
    {
        DungeonSaveSectionEnvelope envelope = (envelopes
                ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.sectionId,
                    sectionId,
                    StringComparison.Ordinal));
        return ParseRequired<T>(envelope, sectionId, expectedVersion);
    }

    private static T ReadRequired<T>(
        IReadOnlyDictionary<string, DungeonSaveSectionEnvelope> envelopes,
        string sectionId,
        int expectedVersion)
        where T : class
    {
        if (!envelopes.TryGetValue(sectionId, out DungeonSaveSectionEnvelope envelope))
        {
            throw new InvalidOperationException(
                $"Required save section '{sectionId}' is missing.");
        }
        return ParseRequired<T>(envelope, sectionId, expectedVersion);
    }

    private static T ParseRequired<T>(
        DungeonSaveSectionEnvelope envelope,
        string sectionId,
        int expectedVersion)
        where T : class
    {
        if (envelope == null
            || envelope.sectionVersion != expectedVersion
            || string.IsNullOrEmpty(envelope.payloadJson))
        {
            throw new InvalidOperationException(
                $"Save section '{sectionId}' is missing or has an invalid version.");
        }
        T result = JsonUtility.FromJson<T>(envelope.payloadJson);
        return result ?? throw new InvalidOperationException(
            $"Save section '{sectionId}' payload is invalid.");
    }
}
