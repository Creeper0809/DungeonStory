using System;
using System.Globalization;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;

public static class FamilyPlanningPolicyFactMapping
{
    private const string PolicyId = "family-planning";
    private const string ScopeId = "dungeon";
    private const string NameRevision = "family-planning-policy-name-v1";

    public static PolicyValueChangeReceipt Capture(
        FamilyPlanningPolicyChangedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        return new PolicyValueChangeReceipt(
            new GameplayEntityId(
                new GameplayEntityKindId("dungeon"),
                "dungeon:family-planning"),
            new KoreanNameSnapshot(
                "가족계획 정책",
                NameRevision,
                KoreanPronunciationHint.AutoHangulDisplay(NameRevision),
                "ko-KR"),
            PolicyId,
            ScopeId,
            StableValue(fact.Before),
            StableValue(fact.After),
            fact.Source,
            "policy-value:family-planning:"
                + fact.AfterRevision.ToString(CultureInfo.InvariantCulture),
            fact.BeforeRevision,
            fact.AfterRevision,
            fact.AbsoluteDay,
            new GameplayLocationReference("dungeon", string.Empty, 0, 0));
    }

    private static string StableValue(FamilyPlanningPolicy policy) =>
        policy switch
        {
            FamilyPlanningPolicy.Off => "off",
            FamilyPlanningPolicy.Planned => "planned",
            FamilyPlanningPolicy.Allowed => "allowed",
            _ => throw new ArgumentOutOfRangeException(nameof(policy))
        };
}
