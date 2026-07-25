using System;

public interface IStaffDiscontentRuntimeProvider
{
    bool TryGetRuntime(out StaffDiscontentRuntime runtime);
}

public interface IStaffDiscontentRuntimeService
{
    float GetWorkEfficiencyMultiplier(CharacterActor staff);
    bool ShouldBlockWork(CharacterActor staff, out string reason);
    bool IsRebellionTarget(CharacterActor target);
    bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender);
}

public sealed class StaffDiscontentRuntimeProvider :
    IStaffDiscontentRuntimeProvider
{
    private readonly CharacterSceneRuntimeReferences runtimeReferences;

    public StaffDiscontentRuntimeProvider(
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out StaffDiscontentRuntime resolvedRuntime)
    {
        resolvedRuntime = runtimeReferences.StaffDiscontent;
        return resolvedRuntime != null;
    }
}

public sealed class StaffDiscontentRuntimeService : IStaffDiscontentRuntimeService
{
    private readonly IStaffDiscontentRuntimeProvider provider;

    public StaffDiscontentRuntimeService(IStaffDiscontentRuntimeProvider provider)
    {
        this.provider = provider
            ?? throw new ArgumentNullException(nameof(provider));
    }

    public float GetWorkEfficiencyMultiplier(CharacterActor staff)
    {
        return staff != null && provider.TryGetRuntime(out StaffDiscontentRuntime runtime)
            ? runtime.GetWorkEfficiencyMultiplier(staff)
            : 1f;
    }

    public bool ShouldBlockWork(CharacterActor staff, out string reason)
    {
        reason = string.Empty;
        return staff != null
            && provider.TryGetRuntime(out StaffDiscontentRuntime runtime)
            && runtime.ShouldBlockWork(staff, out reason);
    }

    public bool IsRebellionTarget(CharacterActor target)
    {
        return target != null
            && provider.TryGetRuntime(out StaffDiscontentRuntime runtime)
            && runtime.IsRebellionTarget(target);
    }

    public bool ResolveSuppressedRebel(CharacterActor rebel, CharacterActor defender)
    {
        return rebel != null
            && defender != null
            && provider.TryGetRuntime(out StaffDiscontentRuntime runtime)
            && runtime.ResolveSuppressedRebel(rebel, defender);
    }
}
