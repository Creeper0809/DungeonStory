#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class EnvironmentalProtectionAssetBuilder
{
    private const string WorkwearRoot =
        "Assets/Resources/SO/Environment/Workwear";
    private const string TraitRoot =
        "Assets/Resources/SO/Character/Traits";

    [MenuItem(
        "Tools/DungeonStory/Environment/Rebuild Protection Assets")]
    public static void Rebuild()
    {
        EnsureFolder("Assets/Resources/SO/Environment");
        EnsureFolder(WorkwearRoot);
        EnsureWorkwear(
            "SlimeWarmingPad",
            "workwear:slime-warming-pad",
            "equipment:slime-warming-pad",
            "보온 점액 패드",
            "슬라임의 짧은 냉장 운반을 돕는 탈착식 보온 패드.",
            new[] { "Slime" },
            Protection(-4f, -4f, 0.6f),
            "research:environment:cold-work");
        EnsureWorkwear(
            "ColdWorkSuit",
            "workwear:cold-work-suit",
            "equipment:cold-work-suit",
            "방한 작업복",
            "전투 방어력 없이 8°C 냉장실 상시 근무를 지원한다.",
            Array.Empty<string>(),
            Protection(-8f, -8f, 0.35f),
            "research:environment:cold-work");
        EnsureWorkwear(
            "RuneColdSuit",
            "workwear:rune-cold-suit",
            "equipment:rune-cold-suit",
            "룬 방한복",
            "후기 2°C 냉장 근무용 룬 단열 작업복. 치명선은 바꾸지 않는다.",
            Array.Empty<string>(),
            Protection(-10f, -10f, 0.2f),
            "research:environment:rune-insulation");
        EnsureWorkwear(
            "HaulingHarness",
            "workwear:hauling-harness",
            DurableToolItemRules.HaulingHarness,
            "운반 멜빵",
            "운반 작업 중 적재 한도를 25% 늘리고 완료 시 내구도를 소모하는 물리 작업 장비.",
            Array.Empty<string>(),
            Protection(0f, 0f, 1f),
            "research:commerce:logistics");
        EnsureColdResistantSlimeTrait();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Environmental protection assets rebuilt.");
    }

    private static ThermalProtectionProfile Protection(
        float comfortMinimumOffset,
        float safeMinimumOffset,
        float coldMultiplier)
    {
        return new ThermalProtectionProfile
        {
            comfortMinimumOffset = comfortMinimumOffset,
            safeMinimumOffset = safeMinimumOffset,
            coldExposureMultiplier = coldMultiplier,
            heatExposureMultiplier = 1f
        };
    }

    private static void EnsureWorkwear(
        string fileName,
        string stableId,
        string physicalItemId,
        string displayName,
        string description,
        string[] allowedSpecies,
        ThermalProtectionProfile protection,
        string researchId)
    {
        string path = $"{WorkwearRoot}/{fileName}.asset";
        EnvironmentalWorkwearSO asset =
            AssetDatabase.LoadAssetAtPath<EnvironmentalWorkwearSO>(path);
        if (asset == null)
        {
            asset = ScriptableObject
                .CreateInstance<EnvironmentalWorkwearSO>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.id = StableNumericId(stableId);
        asset.Configure(
            stableId,
            physicalItemId,
            displayName,
            description,
            allowedSpecies,
            protection,
            researchId);
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureColdResistantSlimeTrait()
    {
        const string path =
            TraitRoot + "/Trait_ColdResistantSlime.asset";
        CharacterTraitSO trait =
            AssetDatabase.LoadAssetAtPath<CharacterTraitSO>(path);
        if (trait == null)
        {
            trait = ScriptableObject.CreateInstance<CharacterTraitSO>();
            AssetDatabase.CreateAsset(trait, path);
        }

        trait.id = 109;
        trait.traitName = "내한성 점액";
        trait.description =
            "슬라임의 최저 쾌적선 -4°C, 최저 안전선 -2°C, 냉기 노출 ×0.6. 치명 온도는 바꾸지 않는다.";
        trait.environmentalProtection = Protection(-4f, -2f, 0.6f);
        EditorUtility.SetDirty(trait);
    }

    private static int StableNumericId(string stableId)
    {
        unchecked
        {
            uint hash = 2166136261;
            for (int index = 0; index < stableId.Length; index++)
            {
                hash ^= stableId[index];
                hash *= 16777619;
            }

            return 800000 + (int)(hash % 100000);
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        int slash = folder.LastIndexOf('/');
        if (slash <= 0)
        {
            throw new InvalidOperationException(
                $"Invalid asset folder '{folder}'.");
        }

        string parent = folder.Substring(0, slash);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(
            parent,
            folder.Substring(slash + 1));
    }
}
#endif
