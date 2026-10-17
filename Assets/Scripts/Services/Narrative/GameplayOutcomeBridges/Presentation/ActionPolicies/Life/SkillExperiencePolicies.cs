using static LifeExperiencePolicyFacts;

public sealed class CharacterSkillAcquiredExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterSkillAcquiredExperiencePolicy()
        : base(CharacterSkillAcquiredOutcomeIds.Acquired.Value,
            "캐릭터의 실제 스킬 획득") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, CharacterSkillAcquiredOutcomeIds.OwnerRole.Value);
        double before = facts.Number(CharacterSkillAcquiredOutcomeIds.BeforeRevision.Value,
            CharacterSkillAcquiredOutcomeIds.RevisionUnit.Value);
        double after = facts.Number(CharacterSkillAcquiredOutcomeIds.AfterRevision.Value,
            CharacterSkillAcquiredOutcomeIds.RevisionUnit.Value);
        ConsecutiveRevisions(before, after, "skill acquisition revision");
        CharacterSkillKind kind = facts.EnumFact<CharacterSkillKind>(
            CharacterSkillAcquiredOutcomeIds.SkillKind.Value);
        facts.Fact(CharacterSkillAcquiredOutcomeIds.SkillId.Value);
        GameplayExperienceCategory category = kind switch
        {
            CharacterSkillKind.Active or CharacterSkillKind.Passive =>
                GameplayExperienceCategory.Meaningful,
            CharacterSkillKind.Ultimate => GameplayExperienceCategory.Intense,
            _ => throw new GameplayExperienceEvidenceException("unsupported acquired skill kind")
        };
        return facts.Action(category,
            kind == CharacterSkillKind.Ultimate
                ? "궁극 스킬을 실제 획득해 강렬한 성장 경험으로 평가함."
                : "능동 또는 지속 스킬을 실제 획득해 의미 있는 성장 경험으로 평가함.");
    }
}

public sealed class CharacterSkillActivatedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterSkillActivatedExperiencePolicy()
        : base(CharacterSkillActivationOutcomeIds.Activated.Value,
            "스킬 사용과 별도 수신 결과의 존재") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        bool source = facts.HasRole(CharacterSkillActivationOutcomeIds.SourceRole.Value,
            CharacterSkillActivationOutcomeIds.SourceRecipientRole.Value);
        bool recipient = facts.HasRole(CharacterSkillActivationOutcomeIds.RecipientRole.Value,
            CharacterSkillActivationOutcomeIds.SourceRecipientRole.Value);
        if (!source && !recipient)
            GameplayExperienceFacts.Fail("skill activation subject role");
        facts.EnumFact<CharacterSkillActivationKind>(
            CharacterSkillActivationOutcomeIds.ActivationKind.Value);
        facts.Fact(CharacterSkillActivationOutcomeIds.SkillId.Value);
        facts.Fact(CharacterSkillActivationOutcomeIds.ChildResultKeys.Value);
        int recipientCount = WholeNonnegative(facts.FactNumber(
            CharacterSkillActivationOutcomeIds.RecipientCount.Value), "skill recipient count");
        int chunkCount = WholeNonnegative(facts.FactNumber(
            CharacterSkillActivationOutcomeIds.RecipientStateChunkCount.Value),
            "skill recipient chunk count");
        if (recipientCount == 0 || chunkCount == 0
            || chunkCount > (facts.Binding.facts?.Count ?? 0))
        {
            GameplayExperienceFacts.Fail("skill recipient evidence");
        }
        for (int index = 0; index < chunkCount; index++)
            facts.Fact(CharacterSkillActivationOutcomeIds.RecipientStateChunk(index).Value);
        string metricSourceRole = HasParticipantRole(facts,
                CharacterSkillActivationOutcomeIds.SourceRole.Value)
            ? CharacterSkillActivationOutcomeIds.SourceRole.Value
            : HasParticipantRole(facts, CharacterSkillActivationOutcomeIds.SourceRecipientRole.Value)
                ? CharacterSkillActivationOutcomeIds.SourceRecipientRole.Value
                : throw new GameplayExperienceEvidenceException("skill source role");
        ConsecutiveRevisions(
            facts.Number(CharacterSkillActivationOutcomeIds.OwnerRevisionBefore.Value,
                CharacterSkillActivationOutcomeIds.RevisionUnit.Value, metricSourceRole),
            facts.Number(CharacterSkillActivationOutcomeIds.OwnerRevisionAfter.Value,
                CharacterSkillActivationOutcomeIds.RevisionUnit.Value, metricSourceRole),
            "skill activation revision");
        return facts.Action(GameplayExperienceCategory.Mild,
            recipient
                ? "스킬 수신 사실은 확인되지만 typed 효과량이 없어 가벼운 경험으로만 평가함."
                : "스킬 사용은 확인되지만 별도 typed 효과량이 없어 가벼운 경험으로만 평가함.");
    }
}
