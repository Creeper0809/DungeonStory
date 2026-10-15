using System;
using System.Globalization;
using DungeonStory.Narrative.Korean;

public static class CharacterConsumablesPolicyFactMapping
{
    public static PolicyValueChangeReceipt Capture(CharacterConsumablesPolicyChangedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        const string NameRevision = "consumables-policy-name-v1";
        return new PolicyValueChangeReceipt(
            new GameplayEntityId(new GameplayEntityKindId("character"), fact.CharacterId.Value),
            new KoreanNameSnapshot(fact.DisplayName, NameRevision,
                KoreanPronunciationHint.AutoHangulDisplay(NameRevision), "ko-KR"),
            fact.PolicyId, fact.ScopeId, fact.BeforeValue, fact.AfterValue, fact.Source,
            "policy-value:" + fact.CharacterId.Value + ":" + fact.AfterRevision.ToString(CultureInfo.InvariantCulture),
            fact.BeforeRevision, fact.AfterRevision, fact.AbsoluteDay,
            new GameplayLocationReference("dungeon", string.Empty, fact.Position.x, fact.Position.y));
    }
}
