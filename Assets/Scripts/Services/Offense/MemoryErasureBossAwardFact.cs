using System;
using DungeonStory.Foundation.Facts;

/// <summary>The actual one-time reward, frozen independently of its output observer.</summary>
public readonly struct MemoryErasureBossAwardFact
{
    private readonly MemoryErasureBossAwardFactData data;
    public MemoryErasureBossAwardFact(MemoryErasureBossAwardFactData data)
    {
        (data ?? throw new ArgumentNullException(nameof(data))).Validate();
        this.data = data.Clone();
    }
    public MemoryErasureBossAwardFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("boss-award-fact-uninitialized")).Clone();
}

[Serializable]
public sealed class MemoryErasureBossAwardFactData
{
    public string regionId = string.Empty, regionDisplayName = string.Empty,
        operationId = string.Empty, physicalCommitId = string.Empty;
    public int absoluteDay;

    public void Validate(bool requirePhysical = true)
    {
        if (!DomainFactValueSyntax.IsIdentity(regionId)
            || !DomainFactValueSyntax.IsIdentity(operationId)
            || operationId != MemoryErasureSealBossAwardRules.BuildOperationId(regionId)
            || !DomainFactValueSyntax.IsText(regionDisplayName, 4096, false) || absoluteDay < 0
            || !DomainFactValueSyntax.IsText(physicalCommitId, 1024, !requirePhysical)
            || physicalCommitId != physicalCommitId.Trim())
            throw new ArgumentException("boss-award-fact-invalid");
    }
    public MemoryErasureBossAwardFactData Clone()
    { Validate(requirePhysical: false); return (MemoryErasureBossAwardFactData)MemberwiseClone(); }
}
