using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
