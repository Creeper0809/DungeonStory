public sealed class SocietyFestivalCelebratedExperiencePolicy : GameplayActionExperiencePolicy
{
    public SocietyFestivalCelebratedExperiencePolicy() : base(
        "society.festival-celebrated",
        "operation 단일 subject만 기록되어 참가자 개인 경험을 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class OffenseBattleCommandResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public OffenseBattleCommandResolvedExperiencePolicy() : base(
        "offense.battle-command-resolved",
        "target은 실제 health delta로 판정하고 actor는 성공한 전투 행동 Mild; 장비/능력은 기술 참조다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Success)
            return facts.Technical("battle-command-not-completed");
        if (!facts.Character)
            return facts.Technical("battle-reference-subject");
        if (facts.HasRole("target"))
        {
            double before = facts.Number("health-before", MigratedExperiencePolicyFacts.Point);
            double after = facts.Number("health-after", MigratedExperiencePolicyFacts.Point);
            double damage = facts.Number("damage", MigratedExperiencePolicyFacts.Point);
            return facts.Damage(before, after, damage, "원정 전투 target의");
        }
        if (facts.HasRole("actor"))
        {
            return facts.Judge(GameplayExperienceCategory.Mild,
                "battle-action-completed",
                "원정 전투 명령을 실제 수행했으나 actor 자신의 피해·성과 중증도 근거는 없어 Mild.");
        }
        GameplayExperienceFacts.Fail("battle-character-role");
        return default;
    }
}

public sealed class OffenseExpeditionArrivedExperiencePolicy : GameplayActionExperiencePolicy
{
    public OffenseExpeditionArrivedExperiencePolicy() : base(
        "offense.expedition-arrived",
        "성공·부분성공·실패로 실제 도착한 character arrival/member는 Meaningful; 시스템 주체는 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedOffenseExperienceFacts.Expedition(facts);
}

public sealed class OffenseExpeditionNodeResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public OffenseExpeditionNodeResolvedExperiencePolicy() : base(
        "offense.expedition-node-resolved",
        "해결된 원정 지점의 character member/actor는 Meaningful; node/expedition 자체는 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedOffenseExperienceFacts.Expedition(facts);
}

public sealed class OffenseExpeditionResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public OffenseExpeditionResolvedExperiencePolicy() : base(
        "offense.expedition-resolved",
        "실제 종결된 원정의 character member/arrival/actor는 Meaningful; 보상·operation은 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedOffenseExperienceFacts.Expedition(facts);
}

public sealed class RunResultReadyExperiencePolicy : GameplayActionExperiencePolicy
{
    public RunResultReadyExperiencePolicy() : base(
        "run.result-ready",
        "run result operation만 기록되어 개인 경험을 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class RunVariableActivatedExperiencePolicy : GameplayActionExperiencePolicy
{
    public RunVariableActivatedExperiencePolicy() : base(
        "run.variable-activated",
        "run variable operation의 발현 사실만 기록하며 개인 경험으로 확대하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class RunVariableExpiredExperiencePolicy : GameplayActionExperiencePolicy
{
    public RunVariableExpiredExperiencePolicy() : base(
        "run.variable-expired",
        "만료된 run variable operation들의 기술 사실만 보존한다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class RunCampaignEventResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public RunCampaignEventResolvedExperiencePolicy() : base(
        "run.campaign-event-resolved",
        "성공한 campaign event의 character actor는 Meaningful, faction은 Mild, effect 대상/operation은 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Success && !facts.Partial)
            return facts.Technical("campaign-event-not-applied");
        if (facts.Character && facts.HasRole("actor"))
            return facts.Judge(GameplayExperienceCategory.Meaningful,
                "campaign-event-actor", "실제 캠페인 사건 해결에 직접 참여한 캐릭터의 의미 있는 경험.");
        if (facts.HasRole("faction"))
            return facts.Judge(GameplayExperienceCategory.Mild,
                "campaign-event-faction", "캠페인 사건에 직접 연결된 세력의 가벼운 상태 변화.");
        return facts.Technical("campaign-effect-reference");
    }
}

public sealed class RunOwnerEndedExperiencePolicy : GameplayActionExperiencePolicy
{
    public RunOwnerEndedExperiencePolicy() : base(
        "run.owner-ended",
        "성공/실패로 실제 종료된 owner actor의 run은 결정적 경험(.95)이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Character || !facts.HasRole("actor"))
        {
            if (!facts.Character)
                return facts.Technical("run-system-subject");
            GameplayExperienceFacts.Fail("owner-run-actor-role");
        }
        if (facts.Binding.status == (int)GameplayOutcomeStatus.Succeeded
            || facts.Binding.status == (int)GameplayOutcomeStatus.Failed)
        {
            return facts.Judge(GameplayExperienceCategory.Transformative,
                "owner-run-ended", "성공 여부와 별개로 owner의 run이 실제 종결된 결정적 경험.");
        }
        return facts.Technical("owner-run-not-ended");
    }
}

internal static class MigratedOffenseExperienceFacts
{
    internal static GameplayExperienceJudgment Expedition(GameplayExperienceFacts facts)
    {
        if (facts.Binding.status == (int)GameplayOutcomeStatus.Blocked
            || facts.Binding.status == (int)GameplayOutcomeStatus.Cancelled)
            return facts.Technical("expedition-not-realized");
        if (!facts.Character)
            return facts.Technical("expedition-system-subject");
        if (!facts.HasRole("member", "arrival", "actor", "target"))
            GameplayExperienceFacts.Fail("expedition-character-role");
        return facts.Judge(GameplayExperienceCategory.Meaningful,
            "expedition-participation",
            "성공 여부와 별개로 실제 원정 단계·귀환·종결에 직접 참여한 캐릭터의 의미 있는 경험.");
    }
}
