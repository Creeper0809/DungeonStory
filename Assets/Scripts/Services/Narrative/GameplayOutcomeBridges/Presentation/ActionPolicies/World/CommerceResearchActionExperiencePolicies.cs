using System;
using System.Linq;
using C = GameplayExperienceCategory;
using static WorldExperiencePolicyFacts;

public sealed class TradeInventoryResultExperiencePolicy : GameplayActionExperiencePolicy
{
    public TradeInventoryResultExperiencePolicy() : base("trade-inventory.result", "23개 거래·재고 결과를 명시 구분. 실제 거래는 일상, 범죄는 경미, 메타 성취는 유의미, 내부 물리 이전은 처리.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        double code = f.Number("result.kind", "enum", anyReference: true);
        if (code != Math.Truncate(code) || !Enum.IsDefined(typeof(TradeInventoryOutcomeKind), (int)code)) GameplayExperienceFacts.Fail("trade kind enum");
        var kind = (TradeInventoryOutcomeKind)(int)code;
        if (!f.Success && !f.Partial) return f.Technical("trade-not-committed");
        return kind switch
        {
            TradeInventoryOutcomeKind.FacilityCrime => f.Judge(C.Mild, "trade-crime-committed", "실제 시설 범죄. 손실 대비 재산 규모가 없어 파산으로 추정하지 않음."),
            TradeInventoryOutcomeKind.MetaUpgradePurchased => f.Judge(C.Meaningful, "persistent-upgrade-purchased", "영구 업그레이드 구매 성취."),
            TradeInventoryOutcomeKind.FacilityRevenue or TradeInventoryOutcomeKind.FacilityShopPurchased or TradeInventoryOutcomeKind.FacilityStockConsumed or TradeInventoryOutcomeKind.FacilityVisit or TradeInventoryOutcomeKind.GuestRequestDelivery or TradeInventoryOutcomeKind.StockSupply => f.Judge(C.Routine, "routine-commerce", "실제 일상 거래·이용·공급. 금액만으로 인생 변화를 추정하지 않음."),
            TradeInventoryOutcomeKind.GrandProjectPhysicalInput or TradeInventoryOutcomeKind.ItemExtraction or TradeInventoryOutcomeKind.PackagedLotTareOutput or TradeInventoryOutcomeKind.PhysicalItemBatchDisposition or TradeInventoryOutcomeKind.PhysicalItemDisposition or TradeInventoryOutcomeKind.PhysicalItemExactSourcePublication or TradeInventoryOutcomeKind.PhysicalItemRelocation or TradeInventoryOutcomeKind.PhysicalItemSourcePublication or TradeInventoryOutcomeKind.PhysicalItemTransform or TradeInventoryOutcomeKind.RegionalSupplyDeliveryTransfer or TradeInventoryOutcomeKind.ReservedRetailStockTransfer or TradeInventoryOutcomeKind.ResourceStockPolicySaleTransfer or TradeInventoryOutcomeKind.WarehouseMassAdmission or TradeInventoryOutcomeKind.WorldResourceRenewableDebit or TradeInventoryOutcomeKind.WastePolicyCommand => f.Technical("physical-or-policy-transaction-not-personal-experience"),
            _ => throw new InvalidOperationException("Uncovered trade enum")
        };
    }
}

public sealed class ResearchKnowledgeCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ResearchKnowledgeCompletedExperiencePolicy() : base("research.knowledge-completed", "지식 해금 완료. 개인의 인생 변화까지 추정하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Meaningful, "지식 해금 완료. 개인의 인생 변화까지 추정하지 않음.");
}

public sealed class ResearchWorkAppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ResearchWorkAppliedExperiencePolicy() : base("research.work-applied", "연구 진척 일상, 완료 유의미, 진행 없는 시도는 처리. 실패 이름으로 부상 추정 금지.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        string complete = f.Fact("research.completed");
        if (complete != "true" && complete != "false") GameplayExperienceFacts.Fail("research completed flag");
        double delta = Nonnegative(f.Number("research.work-delta", "work-unit", "research-subject"));
        return complete == "true" ? f.Action(C.Meaningful, "연구 완료로 지식이 해금됨.")
            : delta > 0 ? f.Judge(C.Routine, "research-progress", "일상 연구 진척. 아직 완료되지 않음.") : f.Technical("zero-research-progress");
    }
}

public sealed class FacilitySynthesisCompletedExperiencePolicy : GameplayActionExperiencePolicy
{
    public FacilitySynthesisCompletedExperiencePolicy() : base("facility.synthesis-completed", "실제 시설 합성 완료. 시설 성취이며 개인 생명 변화가 아님.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Meaningful, "실제 시설 합성 완료. 시설 성취이며 개인 생명 변화가 아님.");
}

public sealed class OffenseTruthRevealedExperiencePolicy : GameplayActionExperiencePolicy
{
    public OffenseTruthRevealedExperiencePolicy() : base("offense.truth-revealed", "새 진실 공개. 실제 내용/개인 관계 영향 근거 없이 정신적 충격을 추정하지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    => f.Action(C.Meaningful, "새 진실 공개. 실제 내용/개인 관계 영향 근거 없이 정신적 충격을 추정하지 않음.");
}

public sealed class ServiceSessionTerminalExperiencePolicy : GameplayActionExperiencePolicy
{
    public ServiceSessionTerminalExperiencePolicy() : base("service.session-terminal", "실제 제공 시간 양수면 일상 이용. 취소/실패만은 경험 아님. 만족도/결제를 생명 위기로 바꾸지 않음.") { }
    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts f)
    {
        double provided = Nonnegative(f.Number("provided-service-seconds", "second", "session"));
        return provided > 0 ? f.Judge(C.Routine, "service-time-provided", "실제 시설 서비스를 제공받음. 회복 효과는 캐릭터 이용 결과에서 별도 평가.") : f.Technical("no-service-provided");
    }
}

