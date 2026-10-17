using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

public enum RunVariableFactKind { Activated = 1, Expired = 2 }

[Serializable]
public sealed class RunVariableFactEntry
{
    public long instanceSequence;
    public string definitionId = string.Empty;
    public string title = string.Empty;
    public int startDay;
    public int remainingDays;
    public RunVariableFactEntry Clone() => (RunVariableFactEntry)MemberwiseClone();
}

// Frozen domain facts, independent of any log identity, importance or summary format.
[Serializable]
public sealed class RunVariableFactData
{
    public RunVariableFactKind kind;
    public int runSeed;
    public int absoluteDay;
    public List<RunVariableFactEntry> entries = new();

    public void Validate()
    {
        if (!Enum.IsDefined(typeof(RunVariableFactKind), kind) || runSeed == 0 || absoluteDay < 1
            || entries == null || entries.Count == 0 || kind == RunVariableFactKind.Activated && entries.Count != 1)
            throw new InvalidOperationException("run-variable-fact-invalid");
        long previous = 0;
        var definitions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in entries)
        {
            if (row == null || row.instanceSequence <= previous || row.startDay < 1 || row.startDay > absoluteDay
                || !DomainFactValueSyntax.IsText(row.definitionId, 512, false) || row.definitionId != row.definitionId.Trim()
                || !definitions.Add(row.definitionId) || !DomainFactValueSyntax.IsText(row.title, 4096, true)
                || (kind == RunVariableFactKind.Activated ? row.remainingDays <= 0 : row.remainingDays != 0))
                throw new InvalidOperationException("run-variable-fact-entry-invalid");
            previous = row.instanceSequence;
        }
    }
    public RunVariableFactData Clone() => new()
    { kind = kind, runSeed = runSeed, absoluteDay = absoluteDay, entries = entries.Select(v => v.Clone()).ToList() };
}

public readonly struct RunVariableFact
{
    private readonly RunVariableFactData data;
    public RunVariableFact(RunVariableFactData source)
    { if (source == null) throw new ArgumentNullException(nameof(source)); source.Validate(); data = source.Clone(); }
    public RunVariableFactData Snapshot() => data?.Clone()
        ?? throw new InvalidOperationException("run-variable-fact-empty");
}

[Serializable]
public sealed class RunVariablePendingFactData
{
    public string publicationKey = string.Empty;
    public RunVariableFactData fact;
    public RunVariablePendingFactData Clone() => new() { publicationKey = publicationKey, fact = fact.Clone() };
    public void Validate()
    {
        if (!DomainFactValueSyntax.IsText(publicationKey, 2048, false) || fact == null)
            throw new InvalidOperationException("run-variable-pending-fact-invalid");
        fact.Validate();
    }
}
