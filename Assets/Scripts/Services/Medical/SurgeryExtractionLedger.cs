using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SurgeryExtractionLedger : ISurgeryExtractionLedger
{
    private readonly Dictionary<string, HashSet<string>> extracted =
        new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

    public bool IsExtracted(string corpseStackId, string nodeId)
    {
        return !string.IsNullOrWhiteSpace(corpseStackId)
            && !string.IsNullOrWhiteSpace(nodeId)
            && extracted.TryGetValue(corpseStackId.Trim(), out HashSet<string> nodes)
            && nodes.Contains(nodeId.Trim());
    }

    public bool TryMarkExtracted(
        string corpseStackId,
        string nodeId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(corpseStackId)
            || string.IsNullOrWhiteSpace(nodeId))
        {
            failureReason = "사체 또는 적출 부위가 유효하지 않습니다.";
            return false;
        }

        string stack = corpseStackId.Trim();
        if (!extracted.TryGetValue(stack, out HashSet<string> nodes))
        {
            nodes = new HashSet<string>(StringComparer.Ordinal);
            extracted.Add(stack, nodes);
        }

        if (!nodes.Add(nodeId.Trim()))
        {
            failureReason = "이미 적출한 부위입니다.";
            return false;
        }

        return true;
    }

    public IReadOnlyList<CorpseSurgicalRecord> Capture()
    {
        return extracted
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new CorpseSurgicalRecord
            {
                stackId = pair.Key,
                extractedNodeIds = pair.Value
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList()
            })
            .ToArray();
    }

    public void Restore(
        IEnumerable<CorpseSurgicalRecord> records,
        IList<string> warnings)
    {
        extracted.Clear();
        foreach (CorpseSurgicalRecord record in
                 records ?? Array.Empty<CorpseSurgicalRecord>())
        {
            if (record == null || string.IsNullOrWhiteSpace(record.stackId))
            {
                warnings?.Add("사체 ID가 없는 적출 기록을 제외했습니다.");
                continue;
            }

            HashSet<string> nodes = new HashSet<string>(
                (record.extractedNodeIds ?? new List<string>())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(id => id.Trim()),
                StringComparer.Ordinal);
            extracted[record.stackId.Trim()] = nodes;
        }
    }
}
