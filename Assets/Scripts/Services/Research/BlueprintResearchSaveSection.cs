using System;
using System.Collections.Generic;
using System.Linq;

public sealed class BlueprintResearchSaveSection :
    DungeonJsonSaveSection<DungeonResearchSaveData>
{
    public const string Id = "research.blueprints";

    private readonly IBlueprintResearchRuntimeProvider runtimeProvider;
    private readonly IFacilityShopCatalog facilityCatalog;

    public BlueprintResearchSaveSection(
        IBlueprintResearchRuntimeProvider runtimeProvider,
        IFacilityShopCatalog facilityCatalog)
    {
        this.runtimeProvider = runtimeProvider
            ?? throw new ArgumentNullException(nameof(runtimeProvider));
        this.facilityCatalog = facilityCatalog
            ?? throw new ArgumentNullException(nameof(facilityCatalog));
    }

    public override string SectionId => Id;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn =>
        new[] { WorkOrdersSaveSection.Id };

    protected override DungeonResearchSaveData CapturePayload()
    {
        DungeonResearchSaveData destination = new DungeonResearchSaveData();
        if (!runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            return destination;
        }

        destination.tasks = runtime.State.Tasks
            .Where(task => task?.Blueprint != null)
            .Select(task => new DungeonResearchTaskSaveData
            {
                blueprintId = task.Blueprint.id,
                progress = task.Progress
            })
            .ToList();
        destination.completedBlueprintIds =
            runtime.State.CompletedBlueprintIds.OrderBy(id => id).ToList();
        destination.unlockedBuildingIds =
            runtime.State.UnlockedBuildingIds.OrderBy(id => id).ToList();
        destination.unlockedRecipeIds = runtime.State.UnlockedRecipeIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
        return destination;
    }

    protected override void RestorePayload(
        DungeonResearchSaveData source,
        DungeonGameRestoreReport report)
    {
        if (!runtimeProvider.TryGetRuntime(out BlueprintResearchRuntime runtime))
        {
            report.AddWarning("Research runtime was not present; research state was skipped.");
            return;
        }

        runtime.State.ClearForRestore();
        Dictionary<int, FacilityBlueprintSO> blueprints = facilityCatalog.Blueprints
            .Where(blueprint => blueprint != null)
            .GroupBy(blueprint => blueprint.id)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (DungeonResearchTaskSaveData task in source.tasks
                     ?? new List<DungeonResearchTaskSaveData>())
        {
            if (task == null
                || !blueprints.TryGetValue(
                    task.blueprintId,
                    out FacilityBlueprintSO blueprint))
            {
                report.AddWarning(
                    $"Research blueprint {task?.blueprintId ?? -1} no longer exists.");
                continue;
            }

            runtime.State.RestoreTask(blueprint, task.progress);
        }

        foreach (int id in source.completedBlueprintIds ?? new List<int>())
        {
            runtime.State.RestoreCompletedBlueprintId(id);
            if (!blueprints.TryGetValue(id, out FacilityBlueprintSO blueprint))
            {
                continue;
            }

            foreach (BlueprintBuildingUnlock unlock in
                     blueprint.Unlocks.OfType<BlueprintBuildingUnlock>())
            {
                runtime.State.RestoreUnlockedBuildingId(unlock.buildingId);
            }

            foreach (BlueprintRecipeUnlock unlock in
                     blueprint.Unlocks.OfType<BlueprintRecipeUnlock>())
            {
                runtime.State.UnlockRecipe(unlock.recipeId);
            }
        }

        foreach (int id in source.unlockedBuildingIds ?? new List<int>())
        {
            runtime.State.RestoreUnlockedBuildingId(id);
        }

        foreach (string id in source.unlockedRecipeIds ?? new List<string>())
        {
            runtime.State.UnlockRecipe(id);
        }
    }
}
