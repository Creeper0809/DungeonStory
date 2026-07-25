using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class RunVariableIds
{
    public const string SlimeCrowdVisit = "run:operation:slime-crowd-visit";
    public const string OrcFeast = "run:operation:orc-feast";
    public const string VampireNightVisit = "run:operation:vampire-night-visit";
    public const string FoodDeliveryDelay = "run:operation:food-delivery-delay";
    public const string GeneralGoodsDiscount = "run:operation:general-goods-discount";
    public const string ManaStockSurplus = "run:operation:mana-stock-surplus";
    public const string VisitingMerchant = "run:operation:visiting-merchant";
    public const string DefenseFacilityDiscount = "run:operation:defense-facility-discount";
    public const string BlueprintRumor = "run:operation:blueprint-rumor";
    public const string ScoutTraces = "run:invasion:scout-traces";
    public const string Ambush = "run:invasion:ambush";
    public const string ArmedIntruder = "run:invasion:armed-intruder";
    public const string LootPriority = "run:invasion:loot-priority";
    public const string TiredIntruder = "run:invasion:tired-intruder";
}

public static class RunVariableCatalog
{
    private static Dictionary<string, RunVariableDefinition> definitions = BuildDefinitions();

    public static IReadOnlyCollection<RunVariableDefinition> All => definitions.Values;

    public static RunVariableDefinition Get(string id)
    {
        return !string.IsNullOrWhiteSpace(id)
            && definitions.TryGetValue(id, out RunVariableDefinition definition)
                ? definition
                : null;
    }

    public static IReadOnlyList<RunVariableDefinition> GetByCategory(RunVariableCategory category)
    {
        return definitions.Values
            .Where(definition => definition.category == category)
            .OrderBy(definition => definition.id, StringComparer.Ordinal)
            .ToList();
    }

    public static void Register(RunVariableDefinition definition, bool replace = false)
    {
        Validate(definition);
        if (!replace && definitions.ContainsKey(definition.id))
        {
            throw new InvalidOperationException($"Run variable '{definition.id}' is already registered.");
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

    private static Dictionary<string, RunVariableDefinition> BuildDefinitions()
    {
        RunVariableDefinition[] list =
        {
            Definition(
                RunVariableIds.SlimeCrowdVisit,
                RunVariableCategory.Operation,
                "슬라임 단체 방문",
                "슬라임 손님 수요가 일시적으로 증가합니다.",
                EventAlertImportance.Low,
                new RunGuestDemandEffect("Slime", 1.7f)),
            Definition(
                RunVariableIds.OrcFeast,
                RunVariableCategory.Operation,
                "오크 회식",
                "오크 손님 수요가 증가하고 고소비 시설 우선순위가 올라갑니다.",
                EventAlertImportance.Low,
                new RunGuestDemandEffect("Orc", 1.6f)),
            Definition(
                RunVariableIds.VampireNightVisit,
                RunVariableCategory.Operation,
                "뱀파이어 야간 방문",
                "뱀파이어 손님 수요가 증가합니다.",
                EventAlertImportance.Low,
                new RunGuestDemandEffect("Vampire", 1.55f)),
            Definition(
                RunVariableIds.FoodDeliveryDelay,
                RunVariableCategory.Operation,
                "식자재 배송 지연",
                "식자재 상품 가격이 올라갑니다.",
                EventAlertImportance.Medium,
                new RunStockCostEffect(StockCategory.Food, 1.5f)),
            Definition(
                RunVariableIds.GeneralGoodsDiscount,
                RunVariableCategory.Operation,
                "잡화 할인",
                "잡화 상품 가격이 내려갑니다.",
                EventAlertImportance.Low,
                new RunStockCostEffect(StockCategory.General, 0.75f)),
            Definition(
                RunVariableIds.ManaStockSurplus,
                RunVariableCategory.Operation,
                "마력 재고 과잉",
                "마력 상품 가격이 크게 내려갑니다.",
                EventAlertImportance.Low,
                new RunStockCostEffect(StockCategory.Mana, 0.65f)),
            Definition(
                RunVariableIds.VisitingMerchant,
                RunVariableCategory.Operation,
                "방문 상인",
                "시설과 설계도 가격이 조금 내려갑니다.",
                EventAlertImportance.Medium,
                new RunFacilityShopCostEffect(0.85f),
                new RunBlueprintCostEffect(0.9f)),
            Definition(
                RunVariableIds.DefenseFacilityDiscount,
                RunVariableCategory.Operation,
                "방어 시설 할인",
                "방어 시설 가격이 내려갑니다.",
                EventAlertImportance.Low,
                new RunFacilityShopCostEffect(0.7f, defenseOnly: true)),
            Definition(
                RunVariableIds.BlueprintRumor,
                RunVariableCategory.Operation,
                "설계도 소문",
                "설계도 가격이 내려가고 연구 목표를 뽑기 쉬워집니다.",
                EventAlertImportance.Medium,
                new RunBlueprintCostEffect(0.8f)),
            Definition(
                RunVariableIds.ScoutTraces,
                RunVariableCategory.Invasion,
                "정찰 흔적",
                "추적자가 탐색을 짧게 끝내고 사장 위치를 빠르게 좁혀 옵니다.",
                EventAlertImportance.Medium,
                new RunIntruderPatternEffect(InvasionIntruderPatternIds.Hunter),
                new RunFocusTimeEffect(0.8f)),
            Definition(
                RunVariableIds.Ambush,
                RunVariableCategory.Invasion,
                "급습",
                "급습자가 긴 탐색 없이 사장에게 향하고 경로 재탐색도 빨라집니다.",
                EventAlertImportance.High,
                new RunIntruderPatternEffect(InvasionIntruderPatternIds.Ambusher),
                new RunRepathIntervalEffect(0.75f),
                new RunFinalCombatDamageEffect(0.9f)),
            Definition(
                RunVariableIds.ArmedIntruder,
                RunVariableCategory.Invasion,
                "무장한 침입자",
                "파괴자가 방어 시설부터 노리며 최종 교전 피해도 증가합니다.",
                EventAlertImportance.High,
                new RunIntruderPatternEffect(InvasionIntruderPatternIds.Breaker),
                new RunFinalCombatDamageEffect(1.35f)),
            Definition(
                RunVariableIds.LootPriority,
                RunVariableCategory.Invasion,
                "약탈 우선",
                "약탈자가 값비싼 운영 시설을 찾아 더 자주 훼손합니다.",
                EventAlertImportance.Medium,
                new RunIntruderPatternEffect(InvasionIntruderPatternIds.Plunderer),
                new RunFacilityDamageIntervalEffect(0.55f)),
            Definition(
                RunVariableIds.TiredIntruder,
                RunVariableCategory.Invasion,
                "지친 침입자",
                "낙오자가 오래 헤매며 최종 교전 피해도 낮습니다.",
                EventAlertImportance.Low,
                new RunIntruderPatternEffect(InvasionIntruderPatternIds.Straggler),
                new RunFocusTimeEffect(1.15f),
                new RunFinalCombatDamageEffect(0.7f))
        };

        Dictionary<string, RunVariableDefinition> result = new Dictionary<string, RunVariableDefinition>(
            StringComparer.Ordinal);
        foreach (RunVariableDefinition definition in list)
        {
            Validate(definition);
            result.Add(definition.id, definition);
        }

        return result;
    }

    private static RunVariableDefinition Definition(
        string id,
        RunVariableCategory category,
        string title,
        string detail,
        EventAlertImportance importance,
        params IRunVariableEffect[] effects)
    {
        return new RunVariableDefinition(
            id,
            category,
            title,
            detail,
            importance,
            1,
            effects ?? Array.Empty<IRunVariableEffect>());
    }

    private static void Validate(RunVariableDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (string.IsNullOrWhiteSpace(definition.id))
        {
            throw new InvalidOperationException("Run variable definitions require a stable id.");
        }

        if (definition.effects == null || definition.effects.Count == 0)
        {
            throw new InvalidOperationException(
                $"Run variable '{definition.id}' must declare at least one effect.");
        }

        if (definition.effects.Any(effect => effect == null))
        {
            throw new InvalidOperationException($"Run variable '{definition.id}' contains a null effect.");
        }
    }
}
