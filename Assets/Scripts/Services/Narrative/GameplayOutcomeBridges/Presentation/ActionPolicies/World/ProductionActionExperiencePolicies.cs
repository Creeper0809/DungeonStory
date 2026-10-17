using System;
using System.Linq;
using C = GameplayExperienceCategory;
using static WorldExperiencePolicyFacts;

public sealed class CharacterWorkIdentityAppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public CharacterWorkIdentityAppliedExperiencePolicy() : base("character.work-identity-applied", "작업 완료 신원 적용은 중복 방지 메타데이터. 작업 성취를 다시 가산하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Judge(C.NotExperience, "system-state-only", "작업 완료 신원 적용은 중복 방지 메타데이터. 작업 성취를 다시 가산하지 않음.");
}

public sealed class ProductionCommandAppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ProductionCommandAppliedExperiencePolicy() : base("production.command-applied", "생산 명령 처리. 아직 생산 성취가 아님.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Judge(C.NotExperience, "system-state-only", "생산 명령 처리. 아직 생산 성취가 아님.");
}

public sealed class ProductionCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ProductionCompletedExperiencePolicy() : base("production.completed", "일반 생산 완료. 품질·생존 영향 근거 없이 극적 성취로 확대하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "일반 생산 완료. 품질·생존 영향 근거 없이 극적 성취로 확대하지 않음.");
}

public sealed class WorkEmergencySuspensionRecordedExperiencePolicy : GameplayActionExperiencePolicy
{
    public WorkEmergencySuspensionRecordedExperiencePolicy() : base("work.emergency-suspension-recorded", "작업 중단/재개 신원 기록. 위협과 피해는 해당 사건 결과에서 판단.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Judge(C.NotExperience, "system-state-only", "작업 중단/재개 신원 기록. 위협과 피해는 해당 사건 결과에서 판단.");
}

