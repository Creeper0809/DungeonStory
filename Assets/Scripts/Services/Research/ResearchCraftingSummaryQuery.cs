using System;

public readonly struct ResearchCraftingSummary
{
    public ResearchCraftingSummary(
        int researchQueueCount,
        int completedProjectCount,
        bool hasActiveProject,
        string activeProjectName,
        float activeProgressRatio,
        int selectedSynthesisMaterials,
        int visibleSynthesisRecipes)
    {
        ResearchQueueCount = researchQueueCount;
        CompletedProjectCount = completedProjectCount;
        HasActiveProject = hasActiveProject;
        ActiveProjectName = activeProjectName ?? string.Empty;
        ActiveProgressRatio = activeProgressRatio;
        SelectedSynthesisMaterials = selectedSynthesisMaterials;
        VisibleSynthesisRecipes = visibleSynthesisRecipes;
    }

    public int ResearchQueueCount { get; }
    public int CompletedProjectCount { get; }
    public bool HasActiveProject { get; }
    public string ActiveProjectName { get; }
    public float ActiveProgressRatio { get; }
    public int SelectedSynthesisMaterials { get; }
    public int VisibleSynthesisRecipes { get; }
}

public interface IResearchCraftingSummaryService
{
    ResearchCraftingSummary Capture();
}

public sealed class ResearchCraftingSummaryService : IResearchCraftingSummaryService
{
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly IFacilitySynthesisRuntimeProvider synthesisProvider;

    public ResearchCraftingSummaryService(
        IBlueprintResearchRuntimeProvider researchProvider,
        IFacilitySynthesisRuntimeProvider synthesisProvider)
    {
        this.researchProvider = researchProvider
            ?? throw new ArgumentNullException(nameof(researchProvider));
        this.synthesisProvider = synthesisProvider
            ?? throw new ArgumentNullException(nameof(synthesisProvider));
    }

    public ResearchCraftingSummary Capture()
    {
        researchProvider.TryGetRuntime(out BlueprintResearchRuntime research);
        synthesisProvider.TryGetRuntime(out FacilitySynthesisRuntime synthesis);

        ResearchProjectSO activeProject = null;
        if (research != null)
        {
            ResearchProjectId activeProjectId = research.State.Projects.ActiveProjectId;
            if (activeProjectId.IsValid)
            {
                research.ProjectCatalog.TryGet(activeProjectId, out activeProject);
            }
        }

        return new ResearchCraftingSummary(
            research != null ? research.State.Projects.Queue.Count : 0,
            research != null ? research.State.Projects.CompletedProjectIds.Count : 0,
            activeProject != null,
            activeProject != null ? activeProject.DisplayName : string.Empty,
            activeProject != null
                ? research.State.Projects.GetProgress(activeProject.ProjectId).GetRatio(activeProject)
                : 0f,
            synthesis != null ? synthesis.SelectedMaterials.Count : 0,
            synthesis != null ? synthesis.VisibleRecipes.Count : 0);
    }
}
