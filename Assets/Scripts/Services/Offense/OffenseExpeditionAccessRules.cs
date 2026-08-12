using System;

public static class OffenseExpeditionAccessRules
{
    public const string RequiredResearchId = "research:survival:field-rations";
    public const string BlockerMessage =
        "야전 식량학 연구를 완료해야 실제 원정을 편성하고 출발할 수 있습니다.";

    public static bool IsUnlocked(BlueprintResearchState researchState)
    {
        return researchState != null
            && researchState.Projects.IsCompleted(
                new ResearchProjectId(RequiredResearchId));
    }

    public static BlueprintResearchState RequireState(
        ProgressionSceneRuntimeReferences progressionRuntimes,
        string ownerName)
    {
        if (progressionRuntimes == null)
        {
            throw new ArgumentNullException(nameof(progressionRuntimes));
        }

        BlueprintResearchRuntime research = progressionRuntimes.BlueprintResearch;
        if (research == null)
        {
            throw new InvalidOperationException(
                $"{ownerName} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        }

        return research.State;
    }
}
