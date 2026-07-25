using System;
using VContainer;

public interface IBlueprintResearchRuntimeProvider
{
    bool TryGetRuntime(out BlueprintResearchRuntime runtime);
}

public interface IBlueprintResearchWorkService
{
    bool HasResearchWorkFor(BuildableObject facility);
    BlueprintResearchWorkResult ApplyResearchWork(
        CharacterActor researcher,
        BuildableObject researchFacility,
        float seconds);
}

public interface IBlueprintResearchStateService
{
    BlueprintResearchState GetState();
}

public sealed class BlueprintResearchRuntimeProvider :
    IBlueprintResearchRuntimeProvider
{
    private readonly ProgressionSceneRuntimeReferences runtimeReferences;

    public BlueprintResearchRuntimeProvider(
        ProgressionSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public bool TryGetRuntime(out BlueprintResearchRuntime runtime)
    {
        runtime = runtimeReferences.BlueprintResearch;
        return runtime != null;
    }
}

public sealed class BlueprintResearchWorkService : IBlueprintResearchWorkService
{
    private readonly IBlueprintResearchRuntimeProvider runtimeProvider;
    private readonly IMetaProgressionRuntimeReader metaProgressionReader;

    public BlueprintResearchWorkService(IBlueprintResearchRuntimeProvider runtimeProvider)
        : this(runtimeProvider, null)
    {
    }

    [Inject]
    public BlueprintResearchWorkService(
        IBlueprintResearchRuntimeProvider runtimeProvider,
        IMetaProgressionRuntimeReader metaProgressionReader)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.metaProgressionReader = metaProgressionReader;
    }

    public bool HasResearchWorkFor(BuildableObject facility)
    {
        return runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime)
            && runtime.HasActiveResearch
            && facility != null
            && facility.SupportsWork(BuiltInWorkTypeIds.Research);
    }

    public BlueprintResearchWorkResult ApplyResearchWork(
        CharacterActor researcher,
        BuildableObject researchFacility,
        float seconds)
    {
        if (!runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            return new BlueprintResearchWorkResult(
                false,
                null,
                0f,
                0f,
                1f,
                false,
                "Research runtime is not available.");
        }

        float multiplier = metaProgressionReader?.GetArcaneResearchWorkMultiplier() ?? 1f;
        return runtime.ApplyResearchWork(researcher, researchFacility, seconds * Math.Max(0.05f, multiplier));
    }
}

public sealed class BlueprintResearchStateService : IBlueprintResearchStateService
{
    private readonly IBlueprintResearchRuntimeProvider runtimeProvider;

    public BlueprintResearchStateService(IBlueprintResearchRuntimeProvider runtimeProvider)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
    }

    public BlueprintResearchState GetState()
    {
        if (!runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            throw new InvalidOperationException($"{nameof(BlueprintResearchStateService)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        }

        return runtime.State;
    }
}
