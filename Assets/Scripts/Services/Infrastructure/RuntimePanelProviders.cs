using System;

public interface IFacilityEvolutionRuntimeProvider
{
    FacilityEvolutionRuntime Runtime { get; }
    bool TryGetRuntime(out FacilityEvolutionRuntime runtime);
}

public interface IFacilitySynthesisRuntimeProvider
{
    FacilitySynthesisRuntime Runtime { get; }
    bool TryGetRuntime(out FacilitySynthesisRuntime runtime);
}

public interface ICodexRuntimeProvider
{
    CodexRuntime Runtime { get; }
    bool TryGetRuntime(out CodexRuntime runtime);
}

public sealed class FacilityEvolutionRuntimeProvider :
    IFacilityEvolutionRuntimeProvider
{
    private readonly FacilityFeatureSceneRuntimeReferences runtimeReferences;

    public FacilityEvolutionRuntimeProvider(
        FacilityFeatureSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public FacilityEvolutionRuntime Runtime
    {
        get
        {
            return runtimeReferences.Evolution
                ?? throw new InvalidOperationException(
                    $"{nameof(IFacilityEvolutionRuntimeProvider)} requires a loaded {nameof(FacilityEvolutionRuntime)}.");
        }
    }

    public bool TryGetRuntime(out FacilityEvolutionRuntime runtime)
    {
        runtime = runtimeReferences.Evolution;
        return runtime != null;
    }
}

public sealed class FacilitySynthesisRuntimeProvider :
    IFacilitySynthesisRuntimeProvider
{
    private readonly FacilityFeatureSceneRuntimeReferences runtimeReferences;

    public FacilitySynthesisRuntimeProvider(
        FacilityFeatureSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public FacilitySynthesisRuntime Runtime
    {
        get
        {
            return runtimeReferences.Synthesis
                ?? throw new InvalidOperationException(
                    $"{nameof(IFacilitySynthesisRuntimeProvider)} requires a loaded {nameof(FacilitySynthesisRuntime)}.");
        }
    }

    public bool TryGetRuntime(out FacilitySynthesisRuntime runtime)
    {
        runtime = runtimeReferences.Synthesis;
        return runtime != null;
    }
}

public sealed class CodexRuntimeProvider :
    ICodexRuntimeProvider
{
    private readonly FacilityFeatureSceneRuntimeReferences runtimeReferences;

    public CodexRuntimeProvider(
        FacilityFeatureSceneRuntimeReferences runtimeReferences)
    {
        this.runtimeReferences = runtimeReferences
            ?? throw new ArgumentNullException(nameof(runtimeReferences));
    }

    public CodexRuntime Runtime
    {
        get
        {
            return runtimeReferences.Codex
                ?? throw new InvalidOperationException(
                    $"{nameof(ICodexRuntimeProvider)} requires a loaded {nameof(CodexRuntime)}.");
        }
    }

    public bool TryGetRuntime(out CodexRuntime runtime)
    {
        runtime = runtimeReferences.Codex;
        return runtime != null;
    }
}
