using System;

public sealed class EconomyAutoProcurementResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EconomyAutoProcurementResolvedExperiencePolicy() : base(
        "economy.auto-procurement-resolved",
        "operation subject의 조달 처리 결과이며 지출액으로 개인 재정 위기를 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class EconomyRetailPurchaseCommittedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EconomyRetailPurchaseCommittedExperiencePolicy() : base(
        "economy.retail-purchase-committed",
        "성공한 character customer 구매는 Routine; 금액으로 파산을 추정하지 않고 facility/item은 기술 참조다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (facts.Character && facts.HasRole("customer"))
            return MigratedExperiencePolicyFacts.CompletedAction(
                facts, GameplayExperienceCategory.Routine,
                "retail-purchase", "실제 소매 구매를 완료한 고객의 일상 경험.");
        return facts.Technical("retail-reference-subject");
    }
}

public sealed class ServiceFacilityCrimeExperiencePolicy : GameplayActionExperiencePolicy
{
    public ServiceFacilityCrimeExperiencePolicy() : base(
        "service.facility-crime",
        "facility 단일 subject만 있어 summary 속 행위자·손실을 개인 경험으로 해석하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class EconomyFacilityPurchasedExperiencePolicy : GameplayActionExperiencePolicy
{
    public EconomyFacilityPurchasedExperiencePolicy() : base(
        "economy.facility-purchased",
        "treasury owner/offer의 거래 사실이며 개인 subject가 없어 경험으로 확대하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class ServiceFacilityVisitedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ServiceFacilityVisitedExperiencePolicy() : base(
        "service.facility-visited",
        "actor의 typed health/mood delta를 상태와 무관하게 평가한다. 실제 delta 없는 실패/취소만 Technical이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.Character || !facts.HasRole("actor"))
        {
            if (!facts.Character)
                return facts.Technical("facility-visit-reference-subject");
            GameplayExperienceFacts.Fail("facility-visit-actor-role");
        }
        double healthBefore = facts.Number("health-before", MigratedExperiencePolicyFacts.Point);
        double healthAfter = facts.Number("health-after", MigratedExperiencePolicyFacts.Point);
        double moodBefore = facts.Number("mood-before", MigratedExperiencePolicyFacts.Point);
        double moodAfter = facts.Number("mood-after", MigratedExperiencePolicyFacts.Point);
        if (healthBefore < 0d || healthAfter < 0d)
            GameplayExperienceFacts.Fail("facility-visit-health");
        if (healthAfter < healthBefore)
            return facts.Damage(healthBefore, healthAfter, healthBefore - healthAfter,
                "시설 이용 중 발생한");
        double healthRecovery = healthAfter - healthBefore;
        double moodChange = Math.Abs(moodAfter - moodBefore);
        if (healthRecovery >= 20d || moodChange >= 20d)
        {
            return facts.Judge(GameplayExperienceCategory.Meaningful,
                "facility-major-applied-change",
                "상태와 무관하게 typed health/mood의 큰 실제 변화가 확인되어 Meaningful.");
        }
        if (healthRecovery > 0d || moodChange > 0d)
        {
            return facts.Judge(GameplayExperienceCategory.Mild,
                "facility-minor-applied-change",
                "상태와 무관하게 typed health/mood의 작은 실제 변화가 확인되어 Mild.");
        }
        if (facts.Success || facts.Partial)
            return facts.Judge(GameplayExperienceCategory.Routine,
                "routine-facility-visit", "실제 변화가 작거나 없는 정상 시설 이용이므로 Routine.");
        return facts.Technical("facility-processing-failure-without-effect");
    }
}

public sealed class MetaUpgradePurchasedExperiencePolicy : GameplayActionExperiencePolicy
{
    public MetaUpgradePurchasedExperiencePolicy() : base(
        "meta.upgrade-purchased",
        "meta operation의 구매 확인이며 개인 경험으로 확대하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class ServiceOperatingDayResolvedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ServiceOperatingDayResolvedExperiencePolicy() : base(
        "service.operating-day-resolved",
        "영업일 operation 정산 사실이며 개인 경험으로 확대하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class InventoryItemTransformedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InventoryItemTransformedExperiencePolicy() : base(
        "inventory.item-transformed",
        "변환 operation의 기술 receipt이며 물리 자산을 개인 경험으로 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class ServiceRegularCustomerRecruitedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ServiceRegularCustomerRecruitedExperiencePolicy() : base(
        "service.regular-customer-recruited",
        "성공한 customer 캐릭터의 실제 영입은 Meaningful(.55)이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Meaningful,
            "customer-recruited", "단골로 실제 영입된 당사자의 의미 있는 관계 변화.", "customer");
}

public sealed class ServiceRegularCustomerVisitedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ServiceRegularCustomerVisitedExperiencePolicy() : base(
        "service.regular-customer-visited",
        "성공한 customer 캐릭터의 방문은 Routine(.10)이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Routine,
            "customer-visit", "단골의 실제 방문이라는 일상 경험.", "customer");
}

public sealed class ServiceModeChangedExperiencePolicy : GameplayActionExperiencePolicy
{
    public ServiceModeChangedExperiencePolicy() : base(
        "service.mode-changed",
        "facility mode 변경 사실이며 개인 경험으로 확대하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class InventoryStockSuppliedExperiencePolicy : GameplayActionExperiencePolicy
{
    public InventoryStockSuppliedExperiencePolicy() : base(
        "inventory.stock-supplied",
        "stock operation 결과이며 공급량으로 개인 성취를 추정하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        facts.Technical();
}

public sealed class WorkOrderTerminalExperiencePolicy : GameplayActionExperiencePolicy
{
    public WorkOrderTerminalExperiencePolicy() : base(
        "work.order-terminal",
        "ledger status가 아니라 typed terminal-kind를 필수 확인한다. completed는 Routine, cancelled는 Technical이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        string terminal = facts.Fact("terminal-kind");
        return terminal switch
        {
            "completed" => facts.Judge(GameplayExperienceCategory.Routine,
                "work-order-completed", "typed terminal-kind가 completed인 실제 공사 완료 사실."),
            "cancelled" => facts.Technical("work-order-cancelled"),
            _ => throw new GameplayExperienceEvidenceException("terminal-kind")
        };
    }
}

public sealed class EconomyRegionalSupplyContractExperiencePolicy : GameplayActionExperiencePolicy
{
    public EconomyRegionalSupplyContractExperiencePolicy() : base(
        "economy.regional-supply-contract",
        "typed 계약 status-after가 Completed인 faction은 Meaningful, 다른 성공 상태는 Mild; operation은 기술 사실이다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        if (!facts.HasRole("faction"))
            return facts.Technical("regional-contract-system-subject");
        string after = facts.Fact("regional-contract-status-after");
        if (!facts.Success)
            return facts.Technical("regional-contract-not-completed");
        return facts.Judge(after == "Completed"
                ? GameplayExperienceCategory.Meaningful
                : GameplayExperienceCategory.Mild,
            after == "Completed" ? "regional-contract-completed" : "regional-contract-state-changed",
            after == "Completed"
                ? "typed 계약 상태가 Completed인 세력의 의미 있는 거래 성취."
                : "typed 계약 상태 변화는 확인되나 완료가 아니므로 Mild.");
    }
}

public sealed class ServiceRegularCustomerRecruitDeliveredExperiencePolicy : GameplayActionExperiencePolicy
{
    public ServiceRegularCustomerRecruitDeliveredExperiencePolicy() : base(
        "service.regular-customer-recruit-delivered",
        "성공한 actor 캐릭터의 실제 영입 배치는 Meaningful(.55); premium은 typed 근거가 없어 사용하지 않는다.") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts) =>
        MigratedExperiencePolicyFacts.CharacterRole(facts, GameplayExperienceCategory.Meaningful,
            "recruit-delivery-applied", "영입 뒤 실제 배치된 당사자의 의미 있는 상태 변화.", "actor");
}
