using System;

public readonly struct ResearchCraftingSummary
{
    public ResearchCraftingSummary(
        int researchTaskCount,
        int completedBlueprintCount,
        bool hasActiveTask,
        string activeBlueprintName,
        float activeProgressRatio,
        int selectedSynthesisMaterials,
        int visibleSynthesisRecipes)
    {
        ResearchTaskCount = researchTaskCount;
        CompletedBlueprintCount = completedBlueprintCount;
        HasActiveTask = hasActiveTask;
        ActiveBlueprintName = activeBlueprintName ?? string.Empty;
        ActiveProgressRatio = activeProgressRatio;
        SelectedSynthesisMaterials = selectedSynthesisMaterials;
        VisibleSynthesisRecipes = visibleSynthesisRecipes;
    }

    public int ResearchTaskCount { get; }
    public int CompletedBlueprintCount { get; }
    public bool HasActiveTask { get; }
    public string ActiveBlueprintName { get; }
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

        BlueprintResearchTask activeTask = null;
        bool hasActiveTask = research != null && research.State.TryGetActiveTask(out activeTask);

        return new ResearchCraftingSummary(
            research != null ? research.State.Tasks.Count : 0,
            research != null ? research.State.CompletedBlueprintIds.Count : 0,
            hasActiveTask,
            activeTask != null && activeTask.Blueprint != null ? activeTask.Blueprint.DisplayName : string.Empty,
            activeTask != null ? activeTask.ProgressRatio : 0f,
            synthesis != null ? synthesis.SelectedMaterials.Count : 0,
            synthesis != null ? synthesis.VisibleRecipes.Count : 0);
    }
}
