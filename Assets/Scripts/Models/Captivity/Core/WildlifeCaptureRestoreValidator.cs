using System;
using System.Collections.Generic;
using UnityEngine;

public interface IWildlifeCaptureRestoreWorld
{
    bool HasActiveGrid { get; }
    bool HasMatchingLiveWildlife(string wildlifeId, string speciesId);
    bool HasSpecies(string speciesId);
    bool HasItem(string itemId);
    bool IsValidGridPosition(Vector2Int position);
    bool IsCarrierAvailable(string carrierId);
    bool TryGetValidPenCapacity(
        string penId,
        Vector2Int penPosition,
        out int capacity);
}

public static class WildlifeCaptureRestoreValidator
{
    public static void Validate(
        CircusSaveData saveData,
        IWildlifeCaptureRestoreWorld world,
        DungeonGameRestoreReport report)
    {
        if (world == null)
        {
            throw new ArgumentNullException(nameof(world));
        }
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (saveData?.capturedWildlife == null)
        {
            report.AddError("Circus restore is missing captured wildlife data.");
            return;
        }
        if (!world.HasActiveGrid)
        {
            report.AddError("Captured wildlife restore requires an active grid.");
            return;
        }

        Dictionary<string, int> penOccupancy =
            new Dictionary<string, int>(StringComparer.Ordinal);
        Dictionary<string, int> penCapacities =
            new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (CapturedWildlifeState state in saveData.capturedWildlife)
        {
            if (state == null)
            {
                continue;
            }

            if (!world.HasMatchingLiveWildlife(
                    state.wildlifeId,
                    state.speciesId))
            {
                report.AddError(
                    $"Captured wildlife '{state.wildlifeId}' has no matching live actor.");
            }
            if (!world.HasSpecies(state.speciesId))
            {
                report.AddError(
                    $"Captured wildlife '{state.wildlifeId}' references unknown species '{state.speciesId}'.");
            }
            if (state.lastFeedItemId.Length > 0
                && !world.HasItem(state.lastFeedItemId))
            {
                report.AddError(
                    $"Captured wildlife '{state.wildlifeId}' references unknown feed '{state.lastFeedItemId}'.");
            }
            if (!world.IsValidGridPosition(state.capturePosition)
                || !world.IsValidGridPosition(state.penPosition)
                || state.escaped
                && !world.IsValidGridPosition(state.escapeDestination))
            {
                report.AddError(
                    $"Captured wildlife '{state.wildlifeId}' has a position outside the active grid.");
            }

            bool requiresCarrier = state.transportState is
                CapturedWildlifeTransportState.AwaitingTransport
                or CapturedWildlifeTransportState.Transporting;
            if (requiresCarrier
                && !world.IsCarrierAvailable(state.reservedCarrierId))
            {
                report.AddError(
                    $"Captured wildlife '{state.wildlifeId}' references unavailable carrier '{state.reservedCarrierId}'.");
            }

            if (state.escaped)
            {
                continue;
            }
            if (!world.TryGetValidPenCapacity(
                    state.penId,
                    state.penPosition,
                    out int capacity))
            {
                report.AddError(
                    $"Captured wildlife '{state.wildlifeId}' references invalid pen '{state.penId}'.");
                continue;
            }

            penOccupancy.TryGetValue(state.penId, out int count);
            penOccupancy[state.penId] = count + 1;
            penCapacities[state.penId] = capacity;
        }

        foreach (KeyValuePair<string, int> entry in penOccupancy)
        {
            int capacity = penCapacities[entry.Key];
            if (entry.Value > capacity)
            {
                report.AddError(
                    $"Beast pen '{entry.Key}' exceeds capacity {capacity} with {entry.Value} animals.");
            }
        }
    }
}
