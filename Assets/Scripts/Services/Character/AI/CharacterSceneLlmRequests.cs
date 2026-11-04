using System;

// Bind transient work to the original scene object, never a reused persistent ID.
// A rolled-back restore keeps that object alive and therefore keeps its request.
internal static class CharacterSceneLlmRequests
{
    internal static bool Generate(ILocalLlmRuntime runtime, string profileId,
        string prompt, string originalText, UnityEngine.Object owner,
        Action<LocalLlmResult> callback, Func<bool> stillRelevant = null)
    {
        var component = owner as UnityEngine.Component;
        CharacterActor actor = component != null ? component.GetComponent<CharacterActor>() : null;
        long revision = actor != null ? actor.NarrativeRequestRevision : 0;
        bool hadActor = actor != null;
        bool IsAlive() => owner != null
            && (!hadActor || (actor != null && actor.NarrativeRequestRevision == revision))
            && (stillRelevant?.Invoke() ?? true);
        if (runtime is ILocalLlmTransientRequestRuntime transient)
            return transient.GenerateTransientAsync(profileId, prompt, originalText, IsAlive, callback);

        // Existing test/legacy runtimes do not own native cancellation. Preserve
        // their typed API and prevent a retired callback from publishing content.
        void Complete(LocalLlmResult result) => callback?.Invoke(IsAlive() ? result
            : new LocalLlmResult(LocalLlmRequestStatus.Cancelled, string.Empty,
                "Original scene request owner was retired.", originalText));
        return profileId switch
        {
            "Persona" => runtime.GeneratePersonaAsync(prompt, Complete),
            "MacroGoal" => runtime.GenerateMacroGoalAsync(prompt, Complete),
            "MoodImpulse" => runtime.GenerateMoodImpulseAsync(prompt, Complete),
            "SocialRumor" => runtime.GenerateSocialRumorAsync(prompt, Complete),
            "CharacterRecord" => runtime.GenerateCharacterRecordAsync(prompt, originalText, Complete),
            "BubbleLine" => runtime.GenerateBubbleLineAsync(prompt, originalText, Complete),
            _ => throw new ArgumentException("Unsupported scene-owned profile.", nameof(profileId))
        };
    }
}
