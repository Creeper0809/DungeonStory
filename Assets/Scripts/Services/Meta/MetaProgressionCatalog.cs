using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class MetaUpgradeIds
{
    public const string StartingFacilityCandidatePlusOne = "meta:starting-facility-candidate";
    public const string StartingOwnerTraitCandidatePlusOne = "meta:starting-owner-trait-candidate";
    public const string BasicPurchaseListExpansion = "meta:basic-purchase-list-expansion";
    public const string SpecialRecipeRecordSlot = "meta:special-recipe-record-slot";
    public const string OwnerSurvivalBonus = "meta:owner-survival-bonus";
    public const string InvasionWarningAccuracy = "meta:invasion-warning-accuracy";
    public const string CommerceSupplyNetwork = "meta:commerce-supply-network";
    public const string FortressEngineering = "meta:fortress-engineering";
    public const string ArcaneResearchMethod = "meta:arcane-research-method";
}

public static class MetaProgressionCatalog
{
    private static Dictionary<string, MetaUpgradeDefinition> definitions = BuildDefinitions();

    public static IReadOnlyCollection<MetaUpgradeDefinition> All => definitions.Values;

    public static MetaUpgradeDefinition Get(string id)
    {
        return !string.IsNullOrWhiteSpace(id)
            && definitions.TryGetValue(id, out MetaUpgradeDefinition definition)
                ? definition
                : null;
    }

    public static void Register(MetaUpgradeDefinition definition, bool replace = false)
    {
        Validate(definition);
        if (!replace && definitions.ContainsKey(definition.id))
        {
            throw new InvalidOperationException($"Meta upgrade '{definition.id}' is already registered.");
        }

        definitions[definition.id] = definition;
    }

    public static void ResetToBuiltIns()
    {
        definitions = BuildDefinitions();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeCatalog()
    {
        ResetToBuiltIns();
    }

    private static Dictionary<string, MetaUpgradeDefinition> BuildDefinitions()
    {
        MetaUpgradeDefinition[] list =
        {
            Definition(
                MetaUpgradeIds.StartingFacilityCandidatePlusOne,
                MetaProgressionBranch.OperationKnowledge,
                "시작 시설 후보 +1",
                "런 시작 시설 후보가 1개 늘어납니다.",
                80,
                2,
                new MetaIntegerBonusEffect(MetaUpgradeEffectIds.StartingFacilityCandidates, 1)),
            Definition(
                MetaUpgradeIds.StartingOwnerTraitCandidatePlusOne,
                MetaProgressionBranch.OwnerSurvival,
                "시작 사장 특성 후보 +1",
                "사장 특성 선택 후보를 늘리는 메타 보상입니다.",
                90,
                1,
                new MetaIntegerBonusEffect(MetaUpgradeEffectIds.StartingOwnerTraitCandidates, 1)),
            Definition(
                MetaUpgradeIds.BasicPurchaseListExpansion,
                MetaProgressionBranch.OperationKnowledge,
                "1일차 기본 구매 목록 확장",
                "일부 1성 시설이 기본 구매 후보에 미리 들어옵니다.",
                100,
                3,
                new MetaIntegerBonusEffect(MetaUpgradeEffectIds.BasicPurchaseEntries, 1)),
            Definition(
                MetaUpgradeIds.SpecialRecipeRecordSlot,
                MetaProgressionBranch.DesignPreservation,
                "특수 조합식 기록 보존",
                "런에서 해금한 특수 조합식 일부를 다음 런에 기억합니다.",
                120,
                3,
                new MetaIntegerBonusEffect(MetaUpgradeEffectIds.PreservedRecipeSlots, 1)),
            Definition(
                MetaUpgradeIds.OwnerSurvivalBonus,
                MetaProgressionBranch.OwnerSurvival,
                "사장 생존력 보너스 증가",
                "사장 최대 체력이 조금 오릅니다.",
                110,
                3,
                new MetaMultiplierDeltaEffect(MetaUpgradeEffectIds.OwnerMaxHealth, 0.08f)),
            Definition(
                MetaUpgradeIds.InvasionWarningAccuracy,
                MetaProgressionBranch.OwnerSurvival,
                "침입 전 경고 정확도 증가",
                "침입 경고를 조금 더 일찍 받을 수 있습니다.",
                90,
                2,
                new MetaMultiplierDeltaEffect(MetaUpgradeEffectIds.InvasionWarningThreshold, -0.08f)),
            Definition(
                MetaUpgradeIds.CommerceSupplyNetwork,
                MetaProgressionBranch.CommerceLogistics,
                "상단 보급망",
                "식량·잡화 배송비가 레벨당 4% 감소합니다.",
                100,
                3,
                new MetaMultiplierDeltaEffect(MetaUpgradeEffectIds.CommerceStockCost, -0.04f)),
            Definition(
                MetaUpgradeIds.FortressEngineering,
                MetaProgressionBranch.FortressDefense,
                "요새 공학",
                "방어 시설 구매가가 레벨당 5% 감소합니다.",
                110,
                3,
                new MetaMultiplierDeltaEffect(MetaUpgradeEffectIds.FortressDefenseFacilityCost, -0.05f)),
            Definition(
                MetaUpgradeIds.ArcaneResearchMethod,
                MetaProgressionBranch.ArcaneResearch,
                "비전 연구법",
                "연구 작업량이 레벨당 8% 증가합니다.",
                110,
                3,
                new MetaMultiplierDeltaEffect(MetaUpgradeEffectIds.ArcaneResearchWork, 0.08f))
        };

        Dictionary<string, MetaUpgradeDefinition> result = new Dictionary<string, MetaUpgradeDefinition>(
            StringComparer.Ordinal);
        foreach (MetaUpgradeDefinition definition in list)
        {
            Validate(definition);
            result.Add(definition.id, definition);
        }

        return result;
    }

    private static MetaUpgradeDefinition Definition(
        string id,
        MetaProgressionBranch branch,
        string title,
        string detail,
        int cost,
        int maxLevel,
        params IMetaUpgradeEffect[] effects)
    {
        return new MetaUpgradeDefinition(
            id,
            branch,
            title,
            detail,
            cost,
            maxLevel,
            effects ?? Array.Empty<IMetaUpgradeEffect>());
    }

    private static void Validate(MetaUpgradeDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.id))
        {
            throw new InvalidOperationException("Meta upgrade definitions require a stable id.");
        }

        if (definition.effects == null || definition.effects.Count == 0)
        {
            throw new InvalidOperationException(
                $"Meta upgrade '{definition.id}' must declare at least one effect.");
        }

        if (definition.effects.Any(effect => effect == null || string.IsNullOrWhiteSpace(effect.EffectId)))
        {
            throw new InvalidOperationException($"Meta upgrade '{definition.id}' contains an invalid effect.");
        }
    }
}
