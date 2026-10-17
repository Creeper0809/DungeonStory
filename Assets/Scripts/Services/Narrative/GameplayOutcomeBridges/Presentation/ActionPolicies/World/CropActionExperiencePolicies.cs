using System;
using System.Linq;
using C = GameplayExperienceCategory;
using static WorldExperiencePolicyFacts;

public sealed class EnvironmentCertifiedSeedCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentCertifiedSeedCompletedExperiencePolicy() : base("environment.certified-seed-completed", "인증 종자 생산 완료. 생존 구원이나 대성공을 추가 추정하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Mild, "인증 종자 생산 완료. 생존 구원이나 대성공을 추가 추정하지 않음.");
}

public sealed class EnvironmentCropIrrigationSuppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentCropIrrigationSuppliedExperiencePolicy() : base("environment.crop-irrigation-supplied", "관개 공급. 작물 구조·개인 생존을 추정하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "관개 공급. 작물 구조·개인 생존을 추정하지 않음.");
}

public sealed class EnvironmentCropManualWaterRefilledExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentCropManualWaterRefilledExperiencePolicy() : base("environment.crop-manual-water-refilled", "작물 물 보충. 일상 관리.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "작물 물 보충. 일상 관리.");
}

public sealed class EnvironmentCropPlanTerminalExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentCropPlanTerminalExperiencePolicy() : base("environment.crop-plan-terminal", "실제 산출량 양수 일상 수확. 처리 실패와 작물 전체 소실 구분.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
            double quantity = Nonnegative(f.Number("output-quantity", "count", "crop"));
            return quantity > 0 ? f.Judge(C.Routine, "harvest-output-confirmed", "실제 수확 산출물 확인. 규모 대비 생계 영향 근거는 없음.") : f.Technical("no-harvest-output");
        }
}

public sealed class EnvironmentCropSelectionChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentCropSelectionChangedExperiencePolicy() : base("environment.crop-selection-changed", "작물 선택 변경. 아직 생산 성취가 아님.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "작물 선택 변경. 아직 생산 성취가 아님.");
}

public sealed class EnvironmentCropTreatmentAppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentCropTreatmentAppliedExperiencePolicy() : base("environment.crop-treatment-applied", "작물 치료 적용. 실제 개인 건강 변화는 아님.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "작물 치료 적용. 실제 개인 건강 변화는 아님.");
}

public sealed class EnvironmentGoldenHarvestScheduledExperiencePolicy : GameplayActionExperiencePolicy
{
    public EnvironmentGoldenHarvestScheduledExperiencePolicy() : base("environment.golden-harvest-scheduled", "특수 수확 예약. 아직 수확 결과나 대성취가 아님.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Routine, "특수 수확 예약. 아직 수확 결과나 대성취가 아님.");
}

