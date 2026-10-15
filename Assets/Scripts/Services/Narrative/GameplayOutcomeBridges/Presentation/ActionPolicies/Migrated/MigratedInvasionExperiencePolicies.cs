public sealed class DefenseFacilityStateChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public DefenseFacilityStateChangedExperiencePolicy() : base(
        "defense.facility-state-changed",
        "시설 단일 subject의 상태 처리 사실이며 개인 경험으로 확대하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class DefenseFacilityTriggeredExperiencePolicy : GameplayActionExperiencePolicy
{
    public DefenseFacilityTriggeredExperiencePolicy() : base(
        "defense.facility-triggered",
        "시설 단일 subject의 발동 사실이며 노출되지 않은 침입자 피해를 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class DefenseFrontCollapsedExperiencePolicy : GameplayActionExperiencePolicy
{
    public DefenseFrontCollapsedExperiencePolicy() : base(
        "defense.front-collapsed",
        "붕괴에 직접 참여한 intruder/defender 캐릭터는 강렬한 경험(.80), operation/facility는 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedInvasionExperienceFacts.Participation(
            facts, GameplayExperienceCategory.Intense, "front-collapse-experience");
}

public sealed class InvasionDungeonBreachedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InvasionDungeonBreachedExperiencePolicy() : base(
        "invasion.dungeon-breached",
        "돌파에 직접 참여한 intruder/defender 캐릭터는 의미 있는 경험(.55)이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedInvasionExperienceFacts.Participation(
            facts, GameplayExperienceCategory.Meaningful, "invasion-breach-experience");
}

public sealed class InvasionFacilityDamagedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InvasionFacilityDamagedExperiencePolicy() : base(
        "invasion.facility-damaged",
        "시설 습격의 intruder/defender 캐릭터만 의미 있는 경험(.55); 피해 시설은 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedInvasionExperienceFacts.Participation(
            facts, GameplayExperienceCategory.Meaningful, "invasion-damage-experience");
}

public sealed class InvasionResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InvasionResolvedExperiencePolicy() : base(
        "invasion.resolved",
        "성공 또는 실패로 종결된 침공의 직접 캐릭터 참가자는 의미 있는 경험(.55)이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedInvasionExperienceFacts.Participation(
            facts, GameplayExperienceCategory.Meaningful, "invasion-resolution-experience");
}

public sealed class InvasionStartedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InvasionStartedExperiencePolicy() : base(
        "invasion.started",
        "침공의 intruder/defender 캐릭터 참가자는 의미 있는 경험(.55); 예약 operation은 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedInvasionExperienceFacts.Participation(
            facts, GameplayExperienceCategory.Meaningful, "invasion-start-experience");
}

internal static class MigratedInvasionExperienceFacts
{
    internal static GameplayExperienceJudgment Participation(
        GameplayExperienceFacts facts,
        GameplayExperienceCategory category,
        string reason)
    {
        if (facts.Binding.status == (int)GameplayOutcomeStatus.Blocked
            || facts.Binding.status == (int)GameplayOutcomeStatus.Cancelled)
            return facts.Technical("invasion-not-realized");
        if (!facts.Character)
            return facts.Technical("invasion-system-subject");
        if (!facts.HasRole("intruder", "defender", "actor", "target"))
            GameplayExperienceFacts.Fail("invasion-participant-role");
        return facts.Judge(category, reason,
            category == GameplayExperienceCategory.Intense
                ? "실제 방어선 붕괴에 직접 참여한 캐릭터의 강렬한 경험."
                : "실제로 진행된 침공 단계에 직접 참여한 캐릭터의 의미 있는 경험.");
    }
}
