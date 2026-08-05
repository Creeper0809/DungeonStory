using System;
using System.Collections.Generic;
using System.Linq;

public sealed class SurgeryExtractionLedger : ISurgeryExtractionLedger
{
    private readonly SurgeryAggregateStateStore stateStore;

    public SurgeryExtractionLedger(SurgeryAggregateStateStore stateStore)
    {
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    private Dictionary<string, HashSet<string>> extracted =>
        stateStore.State.ExtractedNodesByCorpse;

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
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (string.IsNullOrWhiteSpace(corpseStackId)
            || string.IsNullOrWhiteSpace(nodeId))
        {
            failure = new DomainFailure(
                FailureCode.SurgerySubjectInvalid,
                corpseStackId,
                nodeId);
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
            failure = new DomainFailure(
                FailureCode.SurgeryExtractionAlreadyRecorded,
                stack,
                nodeId.Trim());
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

}
