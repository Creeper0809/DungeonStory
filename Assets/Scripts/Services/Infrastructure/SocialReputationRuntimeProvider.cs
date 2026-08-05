using System;

public interface ISocialReputationBiasService
{
    float GetFacilityUtilityBias(CharacterActor actor, BuildableObject building);
}

public sealed class SocialReputationBiasService : ISocialReputationBiasService
{
    private readonly SocialReputationRuntime runtime;

    public SocialReputationBiasService(
        CharacterSceneRuntimeReferences runtimeReferences)
    {
        runtime = (runtimeReferences
                ?? throw new ArgumentNullException(nameof(runtimeReferences)))
            .SocialReputation
            ?? throw new InvalidOperationException(
                $"{nameof(SocialReputationBiasService)} requires a loaded {nameof(SocialReputationRuntime)}.");
    }

    public float GetFacilityUtilityBias(CharacterActor actor, BuildableObject building)
    {
        if (actor == null || building == null)
        {
            return 0f;
        }

        return runtime.GetFacilityUtilityBias(actor, building);
    }
}
