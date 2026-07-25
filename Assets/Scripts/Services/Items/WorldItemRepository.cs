using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public sealed class WorldItemRepository
{
    internal List<WorldItemStackRecord> Records { get; } =
        new List<WorldItemStackRecord>();

    internal Dictionary<string, WorldItemStackRecord> RecordsById { get; } =
        new Dictionary<string, WorldItemStackRecord>(StringComparer.Ordinal);

    internal Dictionary<Vector2Int, List<WorldItemStackRecord>> RecordsByPosition { get; } =
        new Dictionary<Vector2Int, List<WorldItemStackRecord>>();

    internal List<WorldItemStackRecord> HaulableCache { get; } =
        new List<WorldItemStackRecord>();

    internal HashSet<string> PrioritizedHaulStackIds { get; } =
        new HashSet<string>(StringComparer.Ordinal);

    internal int NextStackSequence { get; set; } = 1;
    public int ItemStackVersion { get; private set; }
    public int HaulJobVersion { get; private set; }
    internal bool HaulableCacheDirty { get; set; } = true;

    internal string AllocateStackId()
    {
        return "stack:" + NextStackSequence++.ToString("D8", CultureInfo.InvariantCulture);
    }

    internal void Add(WorldItemStackRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.stackId))
        {
            return;
        }

        if (RecordsById.ContainsKey(record.stackId))
        {
            record.stackId = AllocateStackId();
        }

        Records.Add(record);
        RecordsById[record.stackId] = record;
        if (!RecordsByPosition.TryGetValue(
                record.position,
                out List<WorldItemStackRecord> positionRecords))
        {
            positionRecords = new List<WorldItemStackRecord>();
            RecordsByPosition[record.position] = positionRecords;
        }

        positionRecords.Add(record);
        MarkChanged();
    }

    internal void Remove(WorldItemStackRecord record)
    {
        if (record == null)
        {
            return;
        }

        PrioritizedHaulStackIds.Remove(record.stackId);
        Records.Remove(record);
        RecordsById.Remove(record.stackId);
        if (RecordsByPosition.TryGetValue(
                record.position,
                out List<WorldItemStackRecord> positionRecords))
        {
            positionRecords.Remove(record);
            if (positionRecords.Count == 0)
            {
                RecordsByPosition.Remove(record.position);
            }
        }

        MarkChanged();
    }

    internal void Clear()
    {
        Records.Clear();
        RecordsById.Clear();
        RecordsByPosition.Clear();
        HaulableCache.Clear();
        PrioritizedHaulStackIds.Clear();
        MarkChanged();
    }

    internal void MarkChanged()
    {
        unchecked
        {
            ItemStackVersion++;
            HaulJobVersion++;
        }

        HaulableCacheDirty = true;
    }
}
