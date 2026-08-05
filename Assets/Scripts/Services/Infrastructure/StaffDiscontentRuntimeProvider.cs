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

    public StaffDiscontentRuntimeService(
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .StaffDiscontent
            ?? throw new InvalidOperationException(
                $"{nameof(StaffDiscontentRuntimeService)} requires a loaded {nameof(StaffDiscontentRuntime)}.");
    }

    public float GetWorkEfficiencyMultiplier(CharacterActor staff)
    {
        return staff != null
            ? runtime.GetWorkEfficiencyMultiplier(staff)
            : 1f;
    }

    public bool ShouldBlockWork(CharacterActor staff, out string reason)
    {
        reason = string.Empty;
        return staff != null
            && runtime.ShouldBlockWork(staff, out reason);
    }

    public bool IsRebellionTarget(CharacterActor target)
    {
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
