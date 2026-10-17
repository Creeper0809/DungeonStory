using System;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;

/// <summary>A completed mechanical loadout change, with no log format or importance policy.</summary>
public readonly struct ApparelLoadoutChangedFact
{
    private readonly ApparelLoadoutChangedFactData data;
    public ApparelLoadoutChangedFact(ApparelLoadoutChangedFactData data) =>
        this.data = (data ?? throw new ArgumentNullException(nameof(data))).Clone();
    public ApparelLoadoutChangedFactData Snapshot() =>
        (data ?? throw new InvalidOperationException("apparel-loadout-fact-uninitialized")).Clone();
}

[Serializable]
public sealed class ApparelLoadoutChangedFactData
{
    public string characterId = string.Empty, characterDisplayName = string.Empty, apparelId = string.Empty;
    public bool equipped;
    public int origin, absoluteDay, changeIndex, changeCount;
    public long transitionRevision;

    public void Validate()
    {
        if (!DomainFactValueSyntax.IsIdentity(characterId)
            || !DomainFactValueSyntax.IsText(characterDisplayName, 4096, false)
            || !DomainFactValueSyntax.IsIdentity(apparelId)
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), origin)
            || absoluteDay < 0 || transitionRevision <= 0
            || changeCount <= 0 || changeIndex < 0 || changeIndex >= changeCount)
            throw new ArgumentException("apparel-loadout-fact-invalid");
    }
    public ApparelLoadoutChangedFactData Clone()
    { Validate(); return (ApparelLoadoutChangedFactData)MemberwiseClone(); }
}
