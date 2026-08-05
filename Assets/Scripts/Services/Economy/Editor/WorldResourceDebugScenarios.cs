using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class WorldResourceDebugScenarios
{
    private const string ReportPath =
        "docs/implementation-reports/world-resource-runtime-latest.txt";

    [MenuItem("Tools/DungeonStory/Economy/Verify World Resource Runtime")]
    public static void VerifyRuntimeFromMenu()
    {
        List<string> lines = new List<string>
        {
            "# World Resource Runtime Verification",
            $"utc={DateTime.UtcNow:O}",
            $"playMode={Application.isPlaying}"
        };

        try
        {
            Require(Application.isPlaying, "Play Mode is required.");
            DungeonRuntimeLifetimeScope scope =
                UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
            Require(scope != null && scope.Container != null, "Runtime scope is missing.");

            IWorldResourceRuntime resources =
                scope.Container.Resolve<IWorldResourceRuntime>();
            IWorldResourcePersistence persistence =
                scope.Container.Resolve<IWorldResourcePersistence>();
            IWorldItemStackRuntime items =
                scope.Container.Resolve<IWorldItemStackRuntime>();
            BlueprintResearchRuntime research = scope.Container
                .Resolve<ProgressionSceneRuntimeReferences>()
                .BlueprintResearch;
            Require(resources != null, "World resource runtime is missing.");
            Require(resources.NodeCount > 0, "No natural resource nodes were created.");

            int gatheringNodes = CountNodes(
                resources,
                BuiltInWorkTypeIds.Gather);
            int loggingNodes = CountNodes(
                resources,
                BuiltInWorkTypeIds.Logging);
            int quarryNodes = CountNodes(
                resources,
                BuiltInWorkTypeIds.Quarry);
            WildlifeEcosystemApplicationAdapter ecosystem =
                scope.Container.Resolve<WildlifeEcosystemApplicationAdapter>();
            IResourceEconomyContentCatalog catalog =
                scope.Container.Resolve<IResourceEconomyContentCatalog>();
            DungeonWorldResourceSaveData resourceSave = persistence.Capture();
            lines.Add($"nodes={resources.NodeCount}");
            lines.Add($"gather={gatheringNodes}");
            lines.Add($"logging={loggingNodes}");
            lines.Add($"quarry={quarryNodes}");
            lines.Add("nodeView=" + string.Join(
                ",",
                resources.Nodes.Select(node =>
                    node == null
                        ? "<null>"
                        : $"{node.NodeId}@{node.transform.position.x:0.##},{node.transform.position.y:0.##}")));
            lines.Add("nodeSources=" + string.Join(
                ";",
                resourceSave.nodes.Select(FormatNodeSources)));
            lines.Add("catalogSources=" + string.Join(
                ",",
                new[] { "source:grass", "source:logging", "source:saltstone", "source:quarry" }
                    .Select(recipeId =>
                        $"{recipeId}:{catalog.TryGetRecipe(recipeId, out ProductionRecipeSO recipe)}"
                        + (recipe != null ? $"({recipe.WorkTypeId.Value})" : string.Empty))));
            lines.Add($"catalogRecipeCount={catalog.Recipes.Count}");
            lines.Add("catalogSourceIds=" + string.Join(
                ",",
                catalog.Recipes
                    .Where(recipe => recipe.RecipeId.StartsWith(
                        "source:",
                        StringComparison.Ordinal))
                    .Select(recipe => recipe.RecipeId)));
            lines.Add("patches=" + string.Join(
                ",",
                ecosystem.Patches.Select(patch =>
                    $"{patch.HabitatType}:{patch.PatchId}@{patch.Center.x},{patch.Center.y}")));
            Require(gatheringNodes > 0, "No gathering node was created.");
            Require(loggingNodes > 0, "No logging node was created.");
            Require(quarryNodes > 0, "No quarry node was created.");

            BuildingSO deepQuarry = AssetDatabase
                .FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building/Modular" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
                .FirstOrDefault(asset => asset != null
                    && asset.GetAbility<BuildingFacilityPartAbility>()?.code == "P22");
            Require(deepQuarry != null, "P22 deep quarry asset is missing.");
            Require(
                deepQuarry.Facility?.SupportsWork(BuiltInWorkTypeIds.Quarry) == true,
                "P22 does not support quarry work.");
            Require(
                deepQuarry.HasSemanticTag("quarry"),
                "P22 does not expose the quarry production tag.");

            Require(
                catalog.TryGetRecipe("source:quarry", out ProductionRecipeSO quarryRecipe)
                && quarryRecipe.WorkTypeId == BuiltInWorkTypeIds.Quarry,
                "Deep quarry recipe is not connected to work:quarry.");

            Require(
                research != null,
                "Research runtime is missing.");
            research.State.Projects.Complete(
                new ResearchProjectId("research:forestry:logging"));

            WorldResourceNode loggingNode = resources.Nodes.First(node =>
                resources.TryGetWork(
                    node,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot snapshot)
                && snapshot.Available);
            Require(
                resources.TryGetWork(
                    loggingNode,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot before),
                "Logging work snapshot is missing.");
            int logsBefore = CountItem(items, "resource:log");
            Require(
                resources.ApplyWork(
                    loggingNode,
                    BuiltInWorkTypeIds.Logging,
                    before.RequiredWork,
                    out bool completed)
                && completed,
                "Logging did not complete.");
            int logsAfter = CountItem(items, "resource:log");
            Require(logsAfter >= logsBefore + 5, "Logging did not create physical logs.");
            Require(
                resources.TryGetWork(
                    loggingNode,
                    BuiltInWorkTypeIds.Logging,
                    out WorldResourceWorkSnapshot after)
                && !after.Available,
                "Depleted logging node remained available.");

            DungeonWorldResourceSaveData save = persistence.Capture();
            Require(
                save.nodes.Any(node =>
                    node.buildingInstanceId == loggingNode.NodeId
                    && node.sources.Any(source =>
                        source.workTypeId == BuiltInWorkTypeIds.Logging.Value
                        && source.remainingCycles == 0)),
                "Depleted resource state was not captured.");
            WorldResourceSaveSection saveSection =
                new WorldResourceSaveSection(persistence);
            VerifyStrictSaveIsolation(saveSection);

            lines.Add($"logsBefore={logsBefore}");
            lines.Add($"logsAfter={logsAfter}");
            lines.Add("valid=true");
            WriteReport(lines);
            Debug.Log(string.Join(Environment.NewLine, lines));
        }
        catch (Exception exception)
        {
            lines.Add("valid=false");
            lines.Add("error=" + exception);
            WriteReport(lines);
            Debug.LogException(exception);
        }
    }

    private static int CountNodes(
        IWorldResourceRuntime resources,
        WorkTypeId workTypeId)
    {
        return resources.Nodes.Count(node =>
            resources.TryGetWork(node, workTypeId, out _));
    }

    private static int CountItem(
        IWorldItemStackRuntime items,
        string itemId)
    {
        return items.GetAllStacks()
            .Where(stack => string.Equals(
                stack.ItemId,
                itemId,
                StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
    }

    private static string FormatNodeSources(WorldResourceNodeSaveData node)
    {
        string sources = string.Join(
            ",",
            node.sources.Select(source =>
                $"{source.workTypeId}->{source.recipeId}"));
        return $"{node.buildingInstanceId}@{node.gridX},{node.gridY}=[{sources}]";
    }

    private static void VerifyStrictSaveIsolation(
        WorldResourceSaveSection saveSection)
    {
        Require(
            saveSection is IDungeonSaveSectionPreflight
            && saveSection is IDungeonRollbackFreeSaveSection,
            "World-resource save section is not strict and rollback-free.");
        string before = saveSection.Capture();
        DungeonWorldResourceSaveData invalid =
            JsonUtility.FromJson<DungeonWorldResourceSaveData>(before);
        Require(
            invalid?.nodes?.Count > 0
            && invalid.nodes[0].sources?.Count > 0,
            "World-resource isolation fixture is empty.");
        invalid.nodes[0].sources[0].completedWork = -1f;
        bool rejected = false;
        try
        {
            ((IDungeonSaveSectionPreflight)saveSection).ValidatePayload(
                JsonUtility.ToJson(invalid),
                saveSection.SectionVersion,
                new DungeonGameRestoreReport());
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, "Negative world-resource progress was accepted.");
        Require(
            string.Equals(before, saveSection.Capture(), StringComparison.Ordinal),
            "Failed world-resource preflight mutated live state.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void WriteReport(IEnumerable<string> lines)
    {
        string absolutePath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ".");
        File.WriteAllLines(absolutePath, lines);
    }
}
