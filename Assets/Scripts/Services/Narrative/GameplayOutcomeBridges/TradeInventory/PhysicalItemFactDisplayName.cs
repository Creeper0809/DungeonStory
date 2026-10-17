using System;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;

/// <summary>Presentation belongs to observers, never the physical mutation or its saved facts.</summary>
public static class PhysicalItemFactDisplayName
{
    public static KoreanNameSnapshot Snapshot(string itemDefinitionId, string displayName)
    {
        if (!DomainFactValueSyntax.IsIdentity(itemDefinitionId)
            || !DomainFactValueSyntax.IsText(displayName, DomainFactValueSyntax.MaximumNameCharacters, false))
            throw new ArgumentException("An item observer requires a valid frozen definition name.");
        return new KoreanNameSnapshot(displayName, "item-definition:" + itemDefinitionId + ":v1",
            KoreanPronunciationHint.AutoHangulDisplay("item-definition-pronunciation-v1"), "ko-KR");
    }
}
