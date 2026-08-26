#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V27ProductionReachabilityApplyApproved
{
    private readonly struct ApprovedUnlock
    {
        public ApprovedUnlock(string path, string projectId, int buildingId)
        {
            Path = path;
            ProjectId = projectId;
            BuildingId = buildingId;
        }

        public string Path { get; }
        public string ProjectId { get; }
        public int BuildingId { get; }
    }

    private static readonly ApprovedUnlock[] ApprovedUnlocks =
    {
        new(
            "Assets/Resources/SO/Research/Projects/cuisine_crops.asset",
            "research:cuisine:crops",
            1607),
        new(
            "Assets/Resources/SO/Research/Projects/industry_assisted_processing.asset",
            "research:industry:assisted-processing",
            1609)
    };

    [MenuItem("DungeonStory/V27/Production/Apply Approved Reachability Fixes")]
    public static void ApplyFromMenu()
    {
        List<string> changedPaths = new();
        foreach (ApprovedUnlock approved in ApprovedUnlocks)
        {
            ResearchProjectSO project =
                AssetDatabase.LoadAssetAtPath<ResearchProjectSO>(approved.Path);
            if (project == null
                || project.ProjectId.Value != approved.ProjectId)
            {
                throw new InvalidOperationException(
                    "Research project authority mismatch: " + approved.Path + ".");
            }

            IBlueprintBuildingUnlock[] matching = project.Unlocks
                .OfType<IBlueprintBuildingUnlock>()
                .Where(value => value.BuildingId == approved.BuildingId)
                .ToArray();
            if (matching.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Research project {approved.ProjectId} contains duplicate "
                    + $"building unlock {approved.BuildingId}.");
            }
            if (matching.Length == 1)
                continue;

            project.UnlockCollection.Add(new BlueprintBuildingUnlock
            {
                buildingId = approved.BuildingId
            });
            EditorUtility.SetDirty(project);
            changedPaths.Add(approved.Path);
        }

        if (changedPaths.Count > 0)
        {
            string[] stablePaths = changedPaths
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            AssetDatabase.SaveAssets();
            AssetDatabase.ForceReserializeAssets(
                stablePaths,
                ForceReserializeAssetsOptions.ReserializeAssets);
            AssetDatabase.Refresh();
        }

        foreach (ApprovedUnlock approved in ApprovedUnlocks)
        {
            ResearchProjectSO project =
                AssetDatabase.LoadAssetAtPath<ResearchProjectSO>(approved.Path);
            int count = project.Unlocks.OfType<IBlueprintBuildingUnlock>()
                .Count(value => value.BuildingId == approved.BuildingId);
            if (count != 1)
            {
                throw new InvalidOperationException(
                    $"Approved research unlock join failed: {approved.ProjectId} "
                    + $"-> {approved.BuildingId}; count={count}.");
            }
        }

        Debug.Log(
            "V27 approved reachability fixes applied: "
            + $"changedProjects={changedPaths.Count}; canonicalUnlocks=2.");
    }
}
#endif
