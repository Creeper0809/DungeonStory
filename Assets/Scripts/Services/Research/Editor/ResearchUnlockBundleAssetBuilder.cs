#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ResearchUnlockBundleAssetBuilder
{
    private const string Root = "Assets/Resources/SO/Research/UnlockBundles";

    public static void EnsureAssets(IEnumerable<ResearchProjectSO> projectSource)
    {
        EnsureFolder(Root);
        ResearchProjectSO[] projects = (projectSource
                ?? Array.Empty<ResearchProjectSO>())
            .Where(project => project != null)
            .OrderBy(project => project.id)
            .ToArray();
        HashSet<string> retainedPaths = new(StringComparer.Ordinal);
        foreach (ResearchProjectSO project in projects)
        {
            string path = $"{Root}/{Sanitize(project.ProjectId.Value)}.asset";
            retainedPaths.Add(path);
            ResearchUnlockBundleDefinitionSO asset =
                AssetDatabase.LoadAssetAtPath<ResearchUnlockBundleDefinitionSO>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<ResearchUnlockBundleDefinitionSO>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.id = project.id;
            ResearchUnlockBundleRole role = ResolveRole(project);
            asset.Configure(
                project.ProjectId.Value,
                role,
                project.Description,
                CreateRewardGroups(),
                role is ResearchUnlockBundleRole.Foundation
                    or ResearchUnlockBundleRole.SystemFacility
                    or ResearchUnlockBundleRole.Capstone
                        ? ResolveSingletonReason(role)
                        : string.Empty);
            EditorUtility.SetDirty(asset);
        }

        foreach (string path in AssetDatabase.FindAssets(
                     "t:ResearchUnlockBundleDefinitionSO",
                     new[] { Root }).Select(AssetDatabase.GUIDToAssetPath))
        {
            if (!retainedPaths.Contains(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static ResearchUnlockBundleRole ResolveRole(ResearchProjectSO project)
    {
        string id = project.ProjectId.Value;
        if (project.RequiredWork >= 4800f)
        {
            return ResearchUnlockBundleRole.Capstone;
        }
        if (id.StartsWith("research:equipment:", StringComparison.Ordinal)
            || id.StartsWith("research:metallurgy:", StringComparison.Ordinal)
            || id.StartsWith("research:textile:", StringComparison.Ordinal))
        {
            return ResearchUnlockBundleRole.EquipmentFamily;
        }
        if (id.StartsWith("research:service:", StringComparison.Ordinal)
            || id.StartsWith("research:medical:", StringComparison.Ordinal)
            || id.StartsWith("research:health:", StringComparison.Ordinal)
            || id.StartsWith("research:society:", StringComparison.Ordinal)
            || id.StartsWith("research:housing:", StringComparison.Ordinal))
        {
            return ResearchUnlockBundleRole.ServicePackage;
        }
        if (project.RequiredWork <= 60f || project.Prerequisites.Count == 0)
        {
            return ResearchUnlockBundleRole.Foundation;
        }
        if (project.Unlocks.OfType<BlueprintBuildingUnlock>().Any()
            && !project.Unlocks.OfType<BlueprintRecipeUnlock>().Any())
        {
            return ResearchUnlockBundleRole.SystemFacility;
        }
        return ResearchUnlockBundleRole.ProductionChain;
    }

    private static IEnumerable<ResearchUnlockRewardGroup> CreateRewardGroups()
    {
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.Facility, 0, "핵심 시설");
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.CraftMaterial, 10, "신규 재료");
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.Crop, 15, "작물과 종자");
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.ProductionRecipe, 20, "생산 조합식");
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.ProductionItem, 30, "제작 아이템");
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.InstallationComponent, 35, "설치 부품");
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.CombatEquipment, 40, "무기·방어구·방패");
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.EnvironmentalWorkwear, 45, "환경 작업복");
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.Ammunition, 50, "탄약과 전투 소모품");
        yield return new ResearchUnlockRewardGroup(ResearchRewardKind.MedicalProcedure, 60, "의료 시술");
    }

    private static string ResolveSingletonReason(ResearchUnlockBundleRole role) => role switch
    {
        ResearchUnlockBundleRole.Foundation => "여러 후속 연구와 기존 제작 계통을 여는 기반 연구다.",
        ResearchUnlockBundleRole.Capstone => "게임 규칙을 바꾸는 최종 기술이므로 핵심 보상 하나만으로도 완결된다.",
        _ => "독립된 작업 규칙과 입력·출력을 가진 시스템 시설을 연다."
    };

    private static void EnsureFolder(string path)
    {
        string current = "Assets";
        foreach (string segment in path.Substring("Assets/".Length).Split('/'))
        {
            string next = $"{current}/{segment}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segment);
            }
            current = next;
        }
    }

    private static string Sanitize(string id) => id
        .Replace("research:", string.Empty)
        .Replace(':', '_')
        .Replace('-', '_');
}
#endif
