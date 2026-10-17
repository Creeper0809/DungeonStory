using static LifeExperiencePolicyFacts;

public sealed class ItemFloorTheftCommittedExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public ItemFloorTheftCommittedExperiencePolicy()
        : base(VisitorExitOutcomeIds.TheftCommitted.Value,
            "방문자의 실제 바닥 물품 절도") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, VisitorExitOutcomeIds.ThiefRole.Value);
        int quantity = WholePositive(facts.Number(VisitorExitOutcomeIds.QuantityMetric.Value,
            VisitorExitOutcomeIds.CountUnit.Value), "theft quantity");
        Nonnegative(facts.Number(VisitorExitOutcomeIds.MassMetric.Value,
            VisitorExitOutcomeIds.GramsUnit.Value), "theft mass");
        double sourceBefore = facts.FactNumber(VisitorExitOutcomeIds.SourceQuantityBefore.Value);
        double sourceAfter = facts.FactNumber(VisitorExitOutcomeIds.SourceQuantityAfter.Value);
        double carryBefore = facts.FactNumber(VisitorExitOutcomeIds.CarryQuantityBefore.Value);
        double carryAfter = facts.FactNumber(VisitorExitOutcomeIds.CarryQuantityAfter.Value);
        if (sourceBefore - sourceAfter != quantity || carryAfter - carryBefore != quantity)
            GameplayExperienceFacts.Fail("theft quantity transfer");
        facts.Fact(VisitorExitOutcomeIds.ItemInstanceId.Value);
        return facts.Action(GameplayExperienceCategory.Meaningful,
            "물품을 실제 절취해 행위자에게 의미 있는 범죄 경험으로 평가함.");
    }
}

public sealed class CharacterVisitorDepartureTerminalExperiencePolicy
    : GameplayActionExperiencePolicy
{
    public CharacterVisitorDepartureTerminalExperiencePolicy()
        : base(VisitorExitOutcomeIds.DepartureTerminal.Value,
            "방문자 퇴장의 정상·차단·기술 실패 종결") { }

    protected override GameplayExperienceJudgment Evaluate(GameplayExperienceFacts facts)
    {
        RequireRole(facts, VisitorExitOutcomeIds.DepartingVisitorRole.Value);
        VisitorDepartureTerminalKind terminal = facts.EnumFact<VisitorDepartureTerminalKind>(
            VisitorExitOutcomeIds.TerminalKind.Value);
        string theftChildren = facts.Fact(VisitorExitOutcomeIds.TheftChildResultKeys.Value);
        GameplayOutcomeStatus expectedStatus = terminal == VisitorDepartureTerminalKind.Departed
            ? GameplayOutcomeStatus.Succeeded
            : theftChildren == "none"
                ? terminal is VisitorDepartureTerminalKind.CaptiveBlocked
                    or VisitorDepartureTerminalKind.IncidentBlocked
                        ? GameplayOutcomeStatus.Blocked
                        : GameplayOutcomeStatus.Failed
                : GameplayOutcomeStatus.PartiallySucceeded;
        if (facts.Binding.status != (int)expectedStatus)
            GameplayExperienceFacts.Fail("visitor departure status");
        if (terminal == VisitorDepartureTerminalKind.Departed)
        {
            return facts.Judge(GameplayExperienceCategory.Routine,
                "visitor-departed-normally",
                "방문자가 정상 퇴장하여 일상 이동 경험으로 평가함.");
        }
        if (terminal is VisitorDepartureTerminalKind.CaptiveBlocked
            or VisitorDepartureTerminalKind.IncidentBlocked)
        {
            return facts.Judge(GameplayExperienceCategory.Mild,
                "visitor-departure-blocked",
                "포로 또는 사건 상태 때문에 퇴장이 실제 차단되어 가벼운 경험으로 평가함.");
        }
        if (terminal is VisitorDepartureTerminalKind.GridUnavailable
            or VisitorDepartureTerminalKind.DestinationUnavailable
            or VisitorDepartureTerminalKind.PathUnavailable
            or VisitorDepartureTerminalKind.DoorPassageFailed
            or VisitorDepartureTerminalKind.HandoffFailed)
        {
            return facts.Technical("visitor-departure-technical-failure");
        }
        GameplayExperienceFacts.Fail("visitor departure terminal kind");
        return default;
    }
}
