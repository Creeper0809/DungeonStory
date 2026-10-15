using System;
using System.Linq;
using DungeonStory.Foundation.Facts;

/// <summary>A frozen terminal domain observation, independent of output identity and presentation.</summary>
public readonly struct MemoryErasureTerminalFact
{
    private readonly MemoryErasureTerminalFactData data;
    public MemoryErasureTerminalFact(MemoryErasureTerminalFactData data)
    { this.data = (data ?? throw new ArgumentNullException(nameof(data))).Clone(); }
    public MemoryErasureTerminalFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("memory-erasure-fact-uninitialized")).Clone();

    public static MemoryErasureTerminalFact Capture(MemoryErasureSealUseResult result,
        CharacterAcquiredTraitInstanceState selected, string name, int day, long terminalRevision)
    {
        if (selected == null || selected.instanceId != result.TraitInstanceId)
            throw new ArgumentException("memory-erasure-trait-snapshot-conflict");
        string[] modules = (selected.moduleIds ?? throw new ArgumentException("memory-erasure-modules-missing"))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return new(new MemoryErasureTerminalFactData
        {
            terminalRevision = terminalRevision, status = result.Status, operationId = result.OperationId,
            auditId = result.AuditId, characterId = result.TargetCharacterId, characterDisplayName = name,
            traitInstanceId = result.TraitInstanceId, traitDisplayName = selected.displayName,
            combinationId = string.IsNullOrWhiteSpace(selected.combinationId)
                ? CharacterAcquiredTraitCombinationIdentity.Build(modules) : selected.combinationId,
            moduleIds = modules, physicalCommitId = result.PhysicalCommitId,
            previousRevision = result.PreviousRevision, committedRevision = result.CommittedRevision,
            absoluteHour = result.AbsoluteHour, absoluteDay = day, detail = result.Detail
        });
    }
}

[Serializable]
public sealed class MemoryErasureTerminalFactData
{
    public long terminalRevision, absoluteHour;
    public int absoluteDay, previousRevision, committedRevision;
    public MemoryErasureSealUseStatus status;
    public string operationId = string.Empty, auditId = string.Empty, characterId = string.Empty,
        characterDisplayName = string.Empty, traitInstanceId = string.Empty, traitDisplayName = string.Empty,
        combinationId = string.Empty, physicalCommitId = string.Empty, detail = string.Empty;
    public string[] moduleIds = Array.Empty<string>();

    public void Validate()
    {
        if (terminalRevision <= 0 || terminalRevision == long.MaxValue || absoluteHour < 0 || absoluteDay < 0
            || previousRevision < 0 || committedRevision < 0
            || !Enum.IsDefined(typeof(MemoryErasureSealUseStatus), status)
            || status == MemoryErasureSealUseStatus.AlreadyCompleted
            || !DomainFactValueSyntax.IsIdentity(operationId) || !DomainFactValueSyntax.IsIdentity(auditId)
            || !DomainFactValueSyntax.IsIdentity(characterId) || !DomainFactValueSyntax.IsIdentity(traitInstanceId)
            || !DomainFactValueSyntax.IsIdentity(combinationId)
            || !DomainFactValueSyntax.IsText(characterDisplayName, 4096, false)
            || !DomainFactValueSyntax.IsText(traitDisplayName, 4096, false)
            || !DomainFactValueSyntax.IsText(detail, 16384, true)
            || physicalCommitId == null || physicalCommitId.Length != 0 && !DomainFactValueSyntax.IsIdentity(physicalCommitId)
            || moduleIds == null || moduleIds.Length == 0
            || moduleIds.Any(value => !DomainFactValueSyntax.IsIdentity(value))
            || !moduleIds.SequenceEqual(moduleIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)))
            throw new ArgumentException("memory-erasure-terminal-fact-invalid");
        if (status == MemoryErasureSealUseStatus.Succeeded
            && (physicalCommitId.Length == 0 || (long)committedRevision != (long)previousRevision + 1))
            throw new ArgumentException("memory-erasure-success-transition-invalid");
    }

    public MemoryErasureTerminalFactData Clone()
    {
        Validate(); var result = (MemoryErasureTerminalFactData)MemberwiseClone();
        result.moduleIds = (string[])moduleIds.Clone(); return result;
    }
}
