#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RetailOperationProficiencyDebugScenarios
{
    private const string BuildingRoot = "Assets/Resources/SO/Building";
    private const string RetailCounterPath =
        "Assets/Resources/SO/Building/Modular/S01_판매카운터.asset";
    private const string RetailShelfPath =
        "Assets/Resources/SO/Building/Modular/S02_잡화진열선반.asset";
    private const string SecureDisplayPath =
        "Assets/Resources/SO/Building/Modular/S03_잠금진열장.asset";

    public static void RunAll(bool log = true)
    {
        BuildingSO retailCounter = LoadBuilding(RetailCounterPath);
        CharacterProficiencyRank expectedRecommendedRank =
            retailCounter.OperationProficiency.RecommendedRank;
        CharacterProficiencyRank expectedMinimumRiskRank =
            retailCounter.OperationProficiency.MinimumRiskRank;

        RequireCanonicalRetailOperation(
            retailCounter,
            expectedRecommendedRank,
            expectedMinimumRiskRank);
        RequireCanonicalRetailOperation(
            LoadBuilding(RetailShelfPath),
            expectedRecommendedRank,
            expectedMinimumRiskRank);
        RequireCanonicalRetailOperation(
            LoadBuilding(SecureDisplayPath),
            expectedRecommendedRank,
            expectedMinimumRiskRank);

        BuildingSO[] allBuildings = AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { BuildingRoot })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
        string[] invalidOperateProfiles = allBuildings
            .Where(building => building.Facility?.SupportsWork(
                BuiltInWorkTypeIds.Operate) == true
                && !building.OperationProficiency.IsValid)
            .Select(building => AssetDatabase.GetAssetPath(building)
                + " (" + building.objectName + ")")
            .ToArray();
        Require(invalidOperateProfiles.Length == 0,
            "Operate-supporting BuildingSO assets require a valid operation proficiency: "
            + string.Join(" | ", invalidOperateProfiles));

        if (log)
        {
            int operateCount = allBuildings.Count(building => building.Facility?.SupportsWork(
                BuiltInWorkTypeIds.Operate) == true);
            Debug.Log(
                "RETAIL_OPERATION_PROFICIENCY=PASS; retail=S01,S02,S03; "
                + "operateProfiles=" + operateCount);
        }
    }

    private static BuildingSO LoadBuilding(string path)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
        Require(building != null, "Missing retail BuildingSO: " + path);
        return building;
    }

    private static void RequireCanonicalRetailOperation(
        BuildingSO building,
        CharacterProficiencyRank expectedRecommendedRank,
        CharacterProficiencyRank expectedMinimumRiskRank)
    {
        ProficiencyWorkProfileAuthoring profile = building.OperationProficiency;
        bool valid = building.Facility?.SupportsWork(BuiltInWorkTypeIds.Operate) == true
            && profile.IsValid
            && string.Equals(
                profile.Primary.Value,
                BuiltInCharacterProficiencyIds.Social.Value,
                StringComparison.Ordinal)
            && !profile.Secondary.IsValid
            && Mathf.Approximately(profile.PrimaryWeight, 1f)
            && profile.CombinationMode == ProficiencyCombinationMode.PrimaryOnly
            && profile.RecommendedRank == expectedRecommendedRank
            && profile.MinimumRiskRank == expectedMinimumRiskRank;
        Require(valid,
            "Retail operate proficiency contract failed for "
            + AssetDatabase.GetAssetPath(building)
            + "; operate="
            + (building.Facility?.SupportsWork(BuiltInWorkTypeIds.Operate) == true)
            + "; primary=" + profile.Primary.Value
            + "; secondary=" + profile.Secondary.Value
            + "; weight=" + profile.PrimaryWeight
            + "; mode=" + profile.CombinationMode
            + "; rank=" + profile.RecommendedRank
            + "/" + profile.MinimumRiskRank);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
