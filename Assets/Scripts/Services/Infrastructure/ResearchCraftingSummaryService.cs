using System;

public sealed class ResearchCraftingSummaryService : IResearchCraftingSummaryService
{
    private readonly BlueprintResearchRuntime research;
    private readonly FacilitySynthesisRuntime synthesis;

    public ResearchCraftingSummaryService(
        ProgressionSceneRuntimeReferences progressionRuntimes,
        FacilityFeatureSceneRuntimeReferences facilityRuntimes)
    {
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(ResearchCraftingSummaryService)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        synthesis = (facilityRuntimes
                ?? throw new ArgumentNullException(nameof(facilityRuntimes)))
            .Synthesis
            ?? throw new InvalidOperationException(
                $"{nameof(ResearchCraftingSummaryService)} requires a loaded {nameof(FacilitySynthesisRuntime)}.");
    }

    public ResearchCraftingSummary Capture()
    {
        BlueprintResearchState state = research?.State;
        ResearchProjectSO activeProject = null;
        if (research != null)
        {
            ResearchProjectId activeProjectId = state.Projects.ActiveProjectId;
            if (activeProjectId.IsValid)
            {
                research.ProjectCatalog.TryGet(activeProjectId, out activeProject);
            }
        }

        return new ResearchCraftingSummary(
            state != null ? state.Projects.Queue.Count : 0,
            state != null ? state.Projects.CompletedProjectIds.Count : 0,
            activeProject != null,
            activeProject != null ? activeProject.DisplayName : string.Empty,
            activeProject != null
                ? state.Projects.GetProgressSnapshot(activeProject.ProjectId).GetRatio(activeProject)
                : 0f,
            synthesis != null ? synthesis.SelectedMaterials.Count : 0,
            synthesis != null ? synthesis.VisibleRecipes.Count : 0);
    }
}
