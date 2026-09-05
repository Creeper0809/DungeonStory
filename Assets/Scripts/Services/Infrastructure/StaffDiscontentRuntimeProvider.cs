using System;

public interface IStaffDiscontentRuntimeService
{
    float GetWorkEfficiencyMultiplier(CharacterActor staff);
    bool ShouldBlockWork(CharacterActor staff, out string reason);
    bool IsRebellionTarget(CharacterActor target);
    bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender);
}

public sealed class StaffDiscontentRuntimeService : IStaffDiscontentRuntimeService
{
    private readonly StaffDiscontentRuntime runtime;
    private readonly ICharacterSettlementStandingQuery settlementStandings;

    public StaffDiscontentRuntimeService(
        CharacterSceneRuntimeReferences runtimeReferences,
        ICharacterSettlementStandingQuery settlementStandings)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .StaffDiscontent
            ?? throw new InvalidOperationException(
                $"{nameof(StaffDiscontentRuntimeService)} requires a loaded {nameof(StaffDiscontentRuntime)}.");
        this.settlementStandings = settlementStandings
            ?? throw new ArgumentNullException(nameof(settlementStandings));
    }

    public float GetWorkEfficiencyMultiplier(CharacterActor staff)
    {
        if (settlementStandings.IsMinion(staff))
        {
            return 1f;
        }

        return staff != null
            ? runtime.GetWorkEfficiencyMultiplier(staff)
            : 1f;
    }

    public bool ShouldBlockWork(CharacterActor staff, out string reason)
    {
        reason = string.Empty;
        if (settlementStandings.IsMinion(staff))
        {
            return false;
        }

        return staff != null
            && runtime.ShouldBlockWork(staff, out reason);
    }

    public bool IsRebellionTarget(CharacterActor target)
    {
        if (settlementStandings.IsMinion(target))
        {
            return false;
        }

        return target != null
            && runtime.IsRebellionTarget(target);
    }

    public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)
    {
        return rebel != null
            && defender != null
            && runtime.ResolveSuppressedRebel(rebel, defender);
    }
}
