using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct InvasionIntruderEntry
{
    public InvasionIntruderEntry(
        Vector2Int gridPosition,
        Vector3 outsidePosition,
        Vector3 doorPosition)
    {
        GridPosition = gridPosition;
        OutsidePosition = outsidePosition;
        DoorPosition = doorPosition;
    }

    public Vector2Int GridPosition { get; }
    public Vector3 OutsidePosition { get; }
    public Vector3 DoorPosition { get; }
}

public enum InvasionIntruderEntryFailureCode
{
    None = 0,
    NoEntryCandidate = 1
}

public readonly struct InvasionIntruderEntryResolution
{
    private InvasionIntruderEntryResolution(
        bool success,
        InvasionIntruderEntry entry,
        InvasionIntruderEntryFailureCode failureCode)
    {
        Success = success;
        Entry = entry;
        FailureCode = failureCode;
    }

    public bool Success { get; }
    public InvasionIntruderEntry Entry { get; }
    public InvasionIntruderEntryFailureCode FailureCode { get; }

    public static InvasionIntruderEntryResolution Succeeded(
        InvasionIntruderEntry entry) =>
        new(true, entry, InvasionIntruderEntryFailureCode.None);

    public static InvasionIntruderEntryResolution Failed() =>
        new(false, default, InvasionIntruderEntryFailureCode.NoEntryCandidate);
}

public static class InvasionIntruderEntryResolver
{
    public static InvasionIntruderEntryResolution Resolve(
        bool hasPreferredEntry,
        InvasionIntruderEntry preferredEntry,
        IEnumerable<InvasionIntruderEntry> entranceEntries,
        bool hasFallbackEntry,
        InvasionIntruderEntry fallbackEntry)
    {
        if (hasPreferredEntry)
        {
            return InvasionIntruderEntryResolution.Succeeded(preferredEntry);
        }

        InvasionIntruderEntry[] orderedEntrances =
            (entranceEntries ?? Array.Empty<InvasionIntruderEntry>())
            .OrderBy(candidate => candidate.GridPosition.y)
            .ThenBy(candidate => candidate.GridPosition.x)
            .ToArray();
        if (orderedEntrances.Length > 0)
        {
            return InvasionIntruderEntryResolution.Succeeded(
                orderedEntrances[0]);
        }

        return hasFallbackEntry
            ? InvasionIntruderEntryResolution.Succeeded(fallbackEntry)
            : InvasionIntruderEntryResolution.Failed();
    }

    public static bool TryResolve(
        bool hasPreferredEntry,
        InvasionIntruderEntry preferredEntry,
        IEnumerable<InvasionIntruderEntry> entranceEntries,
        bool hasFallbackEntry,
        InvasionIntruderEntry fallbackEntry,
        out InvasionIntruderEntry entry)
    {
        InvasionIntruderEntryResolution resolution = Resolve(
            hasPreferredEntry,
            preferredEntry,
            entranceEntries,
            hasFallbackEntry,
            fallbackEntry);
        entry = resolution.Entry;
        return resolution.Success;
    }
}
