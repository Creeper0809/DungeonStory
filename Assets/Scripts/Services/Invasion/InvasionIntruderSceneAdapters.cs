using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal static class InvasionIntruderDefenseObservationSceneAdapter
{
    public static void Observe(
        IDefenseRaidAwarenessRuntime raidAwareness,
        string raidId,
        Grid grid,
        Vector2Int center)
    {
        if (raidAwareness == null || grid == null)
        {
            return;
        }

        int visionRange = InvasionIntruderDefenseObservation.VisionRange;
        for (int y = -visionRange; y <= visionRange; y++)
        {
            for (int x = -visionRange; x <= visionRange; x++)
            {
                if (Mathf.Abs(x) + Mathf.Abs(y) > visionRange)
                {
                    continue;
                }

                GridCell cell = grid.GetGridCell(center + new Vector2Int(x, y));
                if (cell == null)
                {
                    continue;
                }

                foreach (DefenseFacility facility in cell.GetAllOccupants()
                             .OfType<DefenseFacility>()
                             .Distinct())
                {
                    DefenseFacilityData defense = facility.Defense;
                    if (defense == null)
                    {
                        continue;
                    }

                    InvasionDefenseObservationSnapshot snapshot = new(
                        facility.PersistentInstanceId,
                        defense.facilityFamilyId,
                        defense.range);
                    if (InvasionIntruderDefenseObservation.IsObservable(snapshot))
                    {
                        raidAwareness.RecordObservedFacility(raidId, facility);
                    }
                }
            }
        }
    }
}

public static class InvasionIntruderEntrySceneAdapter
{
    public static bool TryResolve(
        CharacterSpawner spawner,
        Grid grid,
        out InvasionIntruderEntry entry)
    {
        Vector2Int preferredPosition = default;
        bool hasPreferredEntry = spawner != null
            && spawner.TryGetEntryGridPosition(out preferredPosition);
        InvasionIntruderEntry preferredEntry = hasPreferredEntry
            ? new InvasionIntruderEntry(
                preferredPosition,
                spawner.GetOutsideSpawnWorldPosition(),
                spawner.GetEntryDoorWorldPosition())
            : default;

        List<InvasionIntruderEntry> entranceEntries = new();
        if (!hasPreferredEntry && grid != null)
        {
            foreach (GridCell cell in grid.GetCells().Where(candidate =>
                         candidate != null
                         && candidate.AreaType == GridCellAreaType.Entrance
                         && grid.IsWalkable(candidate.Position)))
            {
                Vector3 doorPosition = grid.GetWorldPos(cell.Position);
                entranceEntries.Add(new InvasionIntruderEntry(
                    cell.Position,
                    doorPosition + new Vector3(2f, 0f, 0f),
                    doorPosition));
            }
        }

        Vector2Int fallbackPosition = default;
        bool hasFallbackEntry = !hasPreferredEntry
            && entranceEntries.Count == 0
            && grid != null
            && grid.TryFindNearestWalkablePosition(
                Vector2Int.zero,
                out fallbackPosition);
        InvasionIntruderEntry fallbackEntry = default;
        if (hasFallbackEntry)
        {
            Vector3 doorPosition = grid.GetWorldPos(fallbackPosition);
            fallbackEntry = new InvasionIntruderEntry(
                fallbackPosition,
                doorPosition + new Vector3(2f, 0f, 0f),
                doorPosition);
        }

        return InvasionIntruderEntryResolver.TryResolve(
            hasPreferredEntry,
            preferredEntry,
            entranceEntries,
            hasFallbackEntry,
            fallbackEntry,
            out entry);
    }
}
