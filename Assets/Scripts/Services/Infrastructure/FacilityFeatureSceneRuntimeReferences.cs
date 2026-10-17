public sealed class FacilityFeatureSceneRuntimeReferences
{
    public FacilityFeatureSceneRuntimeReferences(
        FacilityEvolutionRuntime evolution,
        FacilitySynthesisRuntime synthesis,
        CodexRuntime codex)
    {
        Evolution = evolution;
        Synthesis = synthesis;
        Codex = codex;
    }

    public FacilityEvolutionRuntime Evolution { get; }
    public FacilitySynthesisRuntime Synthesis { get; }
    public CodexRuntime Codex { get; }
}
