using System;
using System.Linq;
using C = GameplayExperienceCategory;
using static WorldExperiencePolicyFacts;

public sealed class InfrastructureAutonomousTransitionExperiencePolicy : GameplayActionExperiencePolicy
{
    public InfrastructureAutonomousTransitionExperiencePolicy() : base("infrastructure.autonomous-transition", "설비 상태 자동 전이. 개인 경험의 증거가 아님.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Judge(C.NotExperience, "system-state-only", "설비 상태 자동 전이. 개인 경험의 증거가 아님.");
}

public sealed class InfrastructureCommandAppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InfrastructureCommandAppliedExperiencePolicy() : base("infrastructure.command-applied", "설비 명령 처리. 개인 피해·성취는 실제 결과에서 판단.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Judge(C.NotExperience, "system-state-only", "설비 명령 처리. 개인 피해·성취는 실제 결과에서 판단.");
}

public sealed class DefenseFacilityEffectResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public DefenseFacilityEffectResolvedExperiencePolicy() : base("defense.facility-effect-resolved", "대상 체력 손실률과 실제 상태 효과 판단. 고장/발동만으로 트라우마 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        if (!f.Character || !f.HasRole("target")) return f.Technical("defense-source-not-victim");
        double before = f.Number("defense-effect.health-before", "points");
        double after = f.Number("defense-effect.health-after", "points");
        double damage = f.Number("defense-effect.damage", "points");
        var health = f.Damage(before, after, damage, "방어시설 실제 피해.");
        if (damage > 0) return health;
        double oldCount = Nonnegative(f.Number("defense-effect.status-count-before", "count"));
        double newCount = Nonnegative(f.Number("defense-effect.status-count-after", "count"));
        double delay = Nonnegative(f.Number("defense-effect.movement-delay", "seconds"));
        return f.Judge(oldCount != newCount || delay > 0 ? C.Mild : C.Routine, "defense-status-effect", "실제 상태 효과/이동 지연. 피해 없는 발동을 생명 위기로 확대하지 않음.");
    }
}

public sealed class DefenseFacilityServiceResultExperiencePolicy : GameplayActionExperiencePolicy
{
    public DefenseFacilityServiceResultExperiencePolicy() : base("defense.facility-service-result", "방어설비 보급·정비 처리. 개인 구원 추정 없음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "방어설비 보급·정비 처리. 개인 구원 추정 없음.");
}

public sealed class InfrastructureBuildingDemolishedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InfrastructureBuildingDemolishedExperiencePolicy() : base("infrastructure.building-demolished", "의도된 철거 완료. 소유자의 재난 피해로 바꾸지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Mild, "의도된 철거 완료. 소유자의 재난 피해로 바꾸지 않음.");
}

public sealed class InfrastructureDungeonSpaceExpandedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InfrastructureDungeonSpaceExpandedExperiencePolicy() : base("infrastructure.dungeon-space-expanded", "실제 사용 가능 공간 확장. 시설 차원의 지속 성취.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Meaningful, "실제 사용 가능 공간 확장. 시설 차원의 지속 성취.");
}

public sealed class InfrastructureFacilityVandalizedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InfrastructureFacilityVandalizedExperiencePolicy() : base("infrastructure.facility-vandalized", "실제 구조 피해가 있어야 파괴 행동 평가. 완전 파괴와 경미 훼손 구분.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        double before = Nonnegative(f.FactNumber("vandalism.structural-hit-points-before"));
        double after = Nonnegative(f.FactNumber("vandalism.structural-hit-points-after"));
        if (after > before) GameplayExperienceFacts.Fail("vandalism structural delta");
        if (before == after) return f.Technical("no-structural-vandalism-change");
        return f.Judge(f.HasRole("vandal") ? C.Meaningful : after == 0 ? C.Meaningful : C.Mild,
            "structural-vandalism-confirmed", "실제 시설 훼손. 목격자의 감정 피해는 별도 결과이며 자동으로 전이하지 않음.");
    }
}

public sealed class InvasionResidentEvacuationTerminalExperiencePolicy : GameplayActionExperiencePolicy
{
    public InvasionResidentEvacuationTerminalExperiencePolicy() : base("invasion.resident-evacuation-terminal", "실제 침공/화재 피난은 유의미, 행정 대체/대상 불가 종료는 처리 메타데이터.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        if (!f.HasRole("evacuee")) return f.Technical();
        var threat = f.EnumFact<ResidentEvacuationThreatKind>("evacuation.threat-kind");
        var terminal = f.EnumFact<ResidentEvacuationTerminalKind>("evacuation.terminal-kind");
        if (terminal == ResidentEvacuationTerminalKind.ReplacedByRequest || terminal == ResidentEvacuationTerminalKind.ResidentUnavailable || terminal == ResidentEvacuationTerminalKind.None)
            return f.Technical("evacuation-administrative-terminal");
        return f.Judge(C.Meaningful, "evacuation-under-confirmed-threat", "확인된 위협에 대한 피난 종료: " + threat + "/" + terminal + ". 실제 부상이나 죽을 뻔함은 추정하지 않음.");
    }
}

public sealed class PolicyStatusChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public PolicyStatusChangedExperiencePolicy() : base("policy.status-changed", "정책 플래그 전이. 실제 적용 피해·성취는 후속 생산 결과에서 판단.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Judge(C.NotExperience, "system-state-only", "정책 플래그 전이. 실제 적용 피해·성취는 후속 생산 결과에서 판단.");
}

public sealed class PolicyValueChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public PolicyValueChangedExperiencePolicy() : base("policy.value-changed", "정책 설정값 전이. 캐릭터의 감정·경험을 추정하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Judge(C.NotExperience, "system-state-only", "정책 설정값 전이. 캐릭터의 감정·경험을 추정하지 않음.");
}

