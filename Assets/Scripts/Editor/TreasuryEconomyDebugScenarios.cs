#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class TreasuryEconomyDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Economy/Verify Treasury Economy")]
    public static void VerifyTreasuryEconomy()
    {
        if (!EditorApplication.isPlaying)
        {
            throw new InvalidOperationException(
                "GameplayScene을 실행한 상태에서 검증해야 합니다.");
        }

        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "런타임 컨테이너가 없습니다.");
        Require(
            HasUiObject("TreasuryResourceHud"),
            "우측 자원·금고 HUD가 생성되지 않았습니다.");
        Require(
            HasUiObject("TreasuryFinanceWindow"),
            "금고 상세 창이 생성되지 않았습니다.");

        IGameMoneyRuntime money =
            scope.Container.Resolve<IGameMoneyRuntime>();
        IEconomyTransactionLedger ledger =
            scope.Container.Resolve<IEconomyTransactionLedger>();
        IAutoProcurementRuntime procurement =
            scope.Container.Resolve<IAutoProcurementRuntime>();
        IEmploymentContractRuntime employment =
            scope.Container.Resolve<IEmploymentContractRuntime>();
        IPaidFacilityContractRuntime paid =
            scope.Container.Resolve<IPaidFacilityContractRuntime>();
        IEquipmentOverclockRuntime overclock =
            scope.Container.Resolve<IEquipmentOverclockRuntime>();
        ITreasuryDefenseRuntime defense =
            scope.Container.Resolve<ITreasuryDefenseRuntime>();
        IReforgePrecisionService precision =
            scope.Container.Resolve<IReforgePrecisionService>();
        Require(
            new object[]
            {
                money,
                ledger,
                procurement,
                employment,
                paid,
                overclock,
                defense,
                precision
            }.All(service => service != null),
            "금고 경제 서비스 조립이 완전하지 않습니다.");

        int beforeBalance = money.Balance;
        EconomyTransactionLedgerSaveData ledgerSave = ledger.Capture();
        money.Add(
            17,
            new EconomyTransactionContext(
                EconomyTransactionKind.DebugAdjustment,
                "treasury-qa",
                description: "금고 검증 입금"));
        Require(
            money.TrySpend(
                17,
                new EconomyTransactionContext(
                    EconomyTransactionKind.DebugAdjustment,
                    "treasury-qa",
                    description: "금고 검증 원상복구"),
                out string spendFailure),
            $"거래 원상복구 실패: {spendFailure}");
        Require(
            money.Balance == beforeBalance,
            "검증 후 금고 잔액이 원래 값으로 돌아오지 않았습니다.");
        Require(
            ledger.Records
                .Reverse()
                .Take(2)
                .All(record =>
                    record.kind == EconomyTransactionKind.DebugAdjustment),
            "거래 출처가 장부에 기록되지 않았습니다.");
        ledger.Restore(ledgerSave);

        BuildingSO treasuryBuilding = Resources
            .LoadAll<BuildingSO>("SO/Building/P1")
            .FirstOrDefault(building =>
                building != null
                && building.GetAbility<
                    BuildingTreasuryPoweredDefenseAbility>() != null);
        Require(
            treasuryBuilding != null,
            "금고 연동 방어시설 콘텐츠가 없습니다.");
        Require(
            treasuryBuilding.GetAbility<
                BuildingOverclockableAbility>() != null,
            "금고 연동 방어시설에 오버클럭 지원 모듈이 없습니다.");
        Require(
            treasuryBuilding.GetConstructionMaterials()
                .Any(pair => pair.Value > 0),
            "금고 연동 방어시설의 물리 건설 재료가 없습니다.");

        BuildingSO[] modularBuildings =
            Resources.LoadAll<BuildingSO>("SO/Building/Modular");
        BuildingSO tavern = modularBuildings.FirstOrDefault(building =>
            string.Equals(
                building?.GetAbility<BuildingFacilityPartAbility>()?.code,
                "D12",
                StringComparison.Ordinal));
        Require(tavern != null, "D12 주점 시설을 찾을 수 없습니다.");
        Require(
            tavern.GetAbility<BuildingMercenaryHiringAbility>() != null,
            "D12 주점에 용병 고용 능력이 연결되지 않았습니다.");

        BuildingSO lootRack = modularBuildings.FirstOrDefault(building =>
            string.Equals(
                building?.GetAbility<BuildingFacilityPartAbility>()?.code,
                "G06",
                StringComparison.Ordinal));
        Require(lootRack != null, "G06 전리품거치대를 찾을 수 없습니다.");
        Require(
            lootRack.GetSemanticTags().Contains(
                "loot-appraisal",
                StringComparer.Ordinal),
            "G06 전리품거치대에 감정 시설 태그가 없습니다.");
        Require(
            lootRack.Facility != null
            && lootRack.Facility.SupportsWork(BuiltInWorkTypeIds.Craft),
            "G06 전리품거치대가 감정 제작 작업을 지원하지 않습니다.");

        ResourceItemDefinitionSO[] economyItems =
            Resources.LoadAll<ResourceItemDefinitionSO>(
                ResourceItemDefinitionSO.ResourcePath);
        ResourceItemDefinitionSO unappraisedLoot =
            economyItems.FirstOrDefault(item =>
                string.Equals(
                    item?.ItemId,
                    "offense:unappraised-loot",
                    StringComparison.Ordinal));
        ResourceItemDefinitionSO appraisedValuables =
            economyItems.FirstOrDefault(item =>
                string.Equals(
                    item?.ItemId,
                    "offense:appraised-valuables",
                    StringComparison.Ordinal));
        Require(
            unappraisedLoot != null && !unappraisedLoot.CanSellToMarket,
            "미감정 전리품은 즉시 판매할 수 없어야 합니다.");
        Require(
            appraisedValuables != null
            && appraisedValuables.CanSellToMarket
            && Mathf.Approximately(appraisedValuables.MarketSaleRate, 1f),
            "감정된 가치품은 평가액 전액으로 판매 가능해야 합니다.");

        ProductionRecipeSO appraisalRecipe =
            Resources.LoadAll<ProductionRecipeSO>(
                    ProductionRecipeSO.ResourcePath)
                .FirstOrDefault(recipe =>
                    string.Equals(
                        recipe?.RecipeId,
                        "recipe:loot-appraisal",
                        StringComparison.Ordinal));
        Require(appraisalRecipe != null, "전리품 감정 조합식이 없습니다.");
        Require(
            string.Equals(
                appraisalRecipe.FacilityTag,
                "loot-appraisal",
                StringComparison.Ordinal)
            && appraisalRecipe.Inputs.Any(input =>
                string.Equals(
                    input?.ItemId,
                    "offense:unappraised-loot",
                    StringComparison.Ordinal))
            && appraisalRecipe.Outputs.Any(output =>
                string.Equals(
                    output?.ItemId,
                    "offense:appraised-valuables",
                    StringComparison.Ordinal)),
            "전리품 감정 조합식의 시설·입출력 연결이 올바르지 않습니다.");

        Debug.Log(
            "[TreasuryEconomy] PASS"
            + $" balance={beforeBalance}"
            + $" protected={procurement.ProtectedFunds}"
            + $" wages3d={employment.ForecastCost(3)}"
            + $" contracts3d={paid.ForecastCost(3)}"
            + $" defense={treasuryBuilding.objectName}"
            + $" tavern={tavern.objectName}"
            + $" appraisal={appraisalRecipe.DisplayName}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool HasUiObject(string objectName)
    {
        return UnityEngine.Object
            .FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Any(rect => rect != null
                && string.Equals(rect.name, objectName, StringComparison.Ordinal));
    }
}
#endif
