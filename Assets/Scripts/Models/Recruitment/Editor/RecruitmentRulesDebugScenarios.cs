using System;

public static class RecruitmentRulesDebugScenarios
{
    public static void Validate()
    {
        RegularCustomerRules rules = RegularCustomerRules.CreateDefault();
        if (!RegularCustomerProgressionRules.MeetsRegularCondition(2, 65f, rules)
            || !RegularCustomerProgressionRules.MeetsRecruitCandidateCondition(
                2, 65f, rules)
            || RegularCustomerProgressionRules.ResolveStatus(
                true, true, false) != RegularCustomerStatus.RecruitCandidate)
        {
            throw new InvalidOperationException(
                "Recruitment progression pure rules regression.");
        }

        RegularCustomerProgressState state = new(
            "customer:pure-fixture",
            "Pure Fixture",
            "Slime",
            0,
            0f,
            false,
            false,
            false,
            RecruitCapability.All);
        state.RecordVisit(65f, rules);
        state.RecordVisit(65f, rules);
        if (!state.IsRegular
            || !state.IsRecruitCandidate
            || state.Status != RegularCustomerStatus.RecruitCandidate
            || !state.MarkRecruited()
            || state.Status != RegularCustomerStatus.Recruited)
        {
            throw new InvalidOperationException(
                "Recruitment pure state progression regression.");
        }
    }
}
