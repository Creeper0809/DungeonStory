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

            string outputCapacityEvidence = VerifyOutputContainmentAdmission(
                resources,
                persistence,
                items,
                scope.Container.Resolve<IProductionItemGateway>(),
                catalog);
            lines.Add(outputCapacityEvidence);

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

    private static string VerifyOutputContainmentAdmission(
        IWorldResourceRuntime resources,
        IWorldResourcePersistence persistence,
        IWorldItemStackRuntime items,
        IProductionItemGateway gateway,
        IResourceEconomyContentCatalog catalog)
    {
        WorkTypeId[] workTypes =
        {
            BuiltInWorkTypeIds.Gather,
            BuiltInWorkTypeIds.Logging,
            BuiltInWorkTypeIds.Quarry
        };
        WorldResourceNode selectedNode = null;
        WorldResourceWorkSnapshot selectedWork = default;
        ProductionOutputDefinition selectedOutput = null;
        Vector2Int selectedPosition = default;
        DungeonWorldResourceSaveData currentState = persistence.Capture();
        foreach (WorldResourceNode node in resources.Nodes.Where(value => value != null))
        foreach (WorkTypeId workType in workTypes)
        {
            if (!resources.TryGetWork(node, workType, out WorldResourceWorkSnapshot work)
                || !work.Available
                || !catalog.TryGetRecipe(work.RecipeId, out ProductionRecipeSO recipe))
            {
                continue;
            }

            ProductionOutputDefinition output = recipe.Outputs
                .FirstOrDefault(value => value != null
                    && value.Probability > 0f
                    && !string.IsNullOrWhiteSpace(value.ItemId));
            if (output == null)
                continue;
            WorldResourceNodeSaveData savedNode = currentState.nodes
                .FirstOrDefault(value => string.Equals(
                    value.buildingInstanceId,
                    node.NodeId,
                    StringComparison.Ordinal));
            if (savedNode == null)
                continue;
            Vector2Int position = new Vector2Int(savedNode.gridX, savedNode.gridY);
            bool occupied = items.GetAllStacks().Any(stack => stack != null
                && stack.Quantity > 0
                && stack.State == WorldItemStackState.Loose
                && string.IsNullOrWhiteSpace(stack.DestinationId)
                && stack.Position == position
                && string.Equals(stack.ItemId, output.ItemId, StringComparison.Ordinal));
            if (occupied)
                continue;
            selectedNode = node;
            selectedWork = work;
            selectedOutput = output;
            selectedPosition = position;
            break;
        }

        Require(selectedNode != null && selectedOutput != null,
            "No available resource output-containment fixture was found.");
        DungeonWorldResourceSaveData before = persistence.Capture();
        WorldResourceSourceSaveData beforeSource = FindSavedSource(
            before,
            selectedNode.NodeId,
            selectedWork.WorkTypeId);
        HashSet<string> existingIds = items.GetAllStacks()
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        List<WorldItemStackSnapshot> created = new();
        try
        {
            Require(gateway.CanSpawnOutput(
                    selectedOutput.ItemId,
                    1,
                    selectedPosition,
                    out DomainFailure initialFailure)
                && !initialFailure.IsFailure,
                "An empty authorized source containment was rejected.");
            Require(gateway.SpawnOutput(selectedOutput.ItemId, 1, selectedPosition),
                "The first authorized source output batch was not materialized.");
            created.AddRange(items.GetAllStacks().Where(stack => stack != null
                && !existingIds.Contains(stack.StackId)
                && stack.State == WorldItemStackState.Loose
                && stack.Position == selectedPosition
                && string.Equals(
                    stack.ItemId,
                    selectedOutput.ItemId,
                    StringComparison.Ordinal)));
            Require(created.Sum(stack => stack.Quantity) == 1,
                "The containment fixture did not create exactly one physical unit.");
            Require(!gateway.CanSpawnOutput(
                    selectedOutput.ItemId,
                    1,
                    selectedPosition,
                    out DomainFailure saturatedFailure)
                && saturatedFailure.Code == FailureCode.ProductionOutputSpaceUnavailable,
                "An occupied source containment did not fail with the typed capacity reason.");
            Require(resources.TryGetWork(
                    selectedNode,
                    selectedWork.WorkTypeId,
                    out WorldResourceWorkSnapshot blocked)
                && !blocked.Available
                && string.Equals(
                    blocked.UnavailableReason,
                    FailureCode.ProductionOutputSpaceUnavailable.ToString(),
                    StringComparison.Ordinal),
                "Resource work remained available while its source containment was occupied.");
            Require(!resources.ApplyWork(
                    selectedNode,
                    selectedWork.WorkTypeId,
                    selectedWork.RequiredWork,
                    out bool completedWhileBlocked)
                && !completedWhileBlocked,
                "Blocked resource work consumed a cycle or completed output.");
            WorldResourceSourceSaveData blockedSource = FindSavedSource(
                persistence.Capture(),
                selectedNode.NodeId,
                selectedWork.WorkTypeId);
            Require(blockedSource.remainingCycles == beforeSource.remainingCycles
                    && Math.Abs(blockedSource.completedWork - beforeSource.completedWork) < 0.0001f,
                "Output saturation mutated resource quantity or work progress.");
        }
        finally
        {
            foreach (WorldItemStackSnapshot stack in created)
            {
                if (stack != null && stack.Quantity > 0)
                    items.TryConsumeStackQuantity(stack.StackId, stack.Quantity, out _);
            }
        }

        Require(resources.TryGetWork(
                selectedNode,
                selectedWork.WorkTypeId,
                out WorldResourceWorkSnapshot recovered)
            && recovered.Available,
            "Resource work did not recover after source containment was cleared.");
        return "PASS OUTPUT_CONTAINMENT_TYPED_BLOCK_RECOVERY "
            + $"node={selectedNode.NodeId};recipe={selectedWork.RecipeId};"
            + $"item={selectedOutput.ItemId};quantity=1;conserved=true";
    }

    private static WorldResourceSourceSaveData FindSavedSource(
        DungeonWorldResourceSaveData data,
        string nodeId,
        WorkTypeId workTypeId)
    {
        WorldResourceSourceSaveData source = data.nodes
            .FirstOrDefault(node => string.Equals(
                node.buildingInstanceId,
                nodeId,
                StringComparison.Ordinal))?
            .sources
            .FirstOrDefault(value => string.Equals(
                value.workTypeId,
                workTypeId.Value,
                StringComparison.Ordinal));
        return source ?? throw new InvalidOperationException(
            $"World-resource save source is missing: {nodeId}/{workTypeId.Value}.");
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
