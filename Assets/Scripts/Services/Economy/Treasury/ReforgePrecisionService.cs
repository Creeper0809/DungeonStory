using System;
using System.Linq;
using UnityEngine;

public interface IReforgePrecisionService
{
    bool TryQueuePrecisionReforge(
        string equipmentInstanceId,
        BuildableObject craftingFacility,
        string catalystItemId,
        string stabilizerItemId,
        ReforgePrecisionSelection selection,
        out EvolutionReforgeOrder order,
        out string failureReason);
}

public sealed class ReforgePrecisionService : IReforgePrecisionService
{
    private readonly IEquipmentEvolutionRuntime evolution;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IGameMoneyAccount money;

    public ReforgePrecisionService(
        IEquipmentEvolutionRuntime evolution,
        ICombatEquipmentRuntime equipment,
        IWorldItemStackRuntime worldItems,
        IGameMoneyAccount money)
    {
        this.evolution = evolution
            ?? throw new ArgumentNullException(nameof(evolution));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
    }

    public bool TryQueuePrecisionReforge(
        string equipmentInstanceId,
        BuildableObject craftingFacility,
        string catalystItemId,
        string stabilizerItemId,
        ReforgePrecisionSelection selection,
        out EvolutionReforgeOrder order,
        out string failureReason)
    {
        selection ??= new ReforgePrecisionSelection();
        order = null;
        if (selection.SelectedCount > 2)
        {
            failureReason = "유료 정밀 서비스는 최대 두 개까지 선택할 수 있습니다.";
            return false;
        }

        if (!equipment.TryGetInstance(
                equipmentInstanceId,
                out CombatEquipmentInstance instance)
            || !equipment.TryGetDefinition(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            failureReason = "재단조할 장비를 찾지 못했습니다.";
            return false;
        }

        int value = ResolveValue(instance, definition);
        int goldCost = Mathf.CeilToInt(value * (
            (selection.preciseCalibration ? 0.2f : 0f)
            + (selection.burdenSuppression ? 0.3f : 0f)
            + (selection.externalTechnicalSupport ? 0.15f : 0f)));
        if (goldCost > 0 && !money.CanSpend(goldCost))
        {
            failureReason = $"정밀 서비스 비용 {goldCost}골드가 필요합니다.";
            return false;
        }

        if (!evolution.TryQueueReforge(
                equipmentInstanceId,
                craftingFacility,
                catalystItemId,
                stabilizerItemId,
                out EvolutionReforgeOrder queued,
                out failureReason))
        {
            return false;
        }

        if (goldCost > 0
            && !money.TrySpend(
                goldCost,
                new EconomyTransactionContext(
                    EconomyTransactionKind.ReforgePrecision,
                    "precision-reforge",
                    equipmentInstanceId,
                    "정밀 재단조 서비스"),
                out failureReason))
        {
            evolution.CancelReforge(queued.orderId, out _);
            return false;
        }

        if (!evolution.TryConfigurePrecision(
                queued.orderId,
                selection,
                goldCost,
                out failureReason))
        {
            evolution.CancelReforge(queued.orderId, out _);
            if (goldCost > 0)
            {
                money.Add(
                    goldCost,
                    new EconomyTransactionContext(
                        EconomyTransactionKind.LegacyIncome,
                        "precision-reforge-refund",
                        equipmentInstanceId,
                        "정밀 재단조 환불"));
            }

            return false;
        }

        order = evolution.ReforgeOrders
            .First(entry => string.Equals(
                entry.orderId,
                queued.orderId,
                StringComparison.Ordinal))
            .Clone();
        failureReason = string.Empty;
        return true;
    }

    private int ResolveValue(
        CombatEquipmentInstance instance,
        CombatEquipmentDefinitionSO definition)
    {
        DungeonItemDefinition item =
            worldItems.CatalogProvider.GetDefinition(definition.ItemId);
        float materialValue = equipment.TryGetDerivedStats(
                instance.instanceId,
                out CombatEquipmentDerivedStats stats)
            ? stats.ValueMultiplier
            : 1f;
        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                Mathf.Max(1, item.UnitPrice)
                * CombatQualityRules.GetMultiplier(instance.quality)
                * materialValue));
    }
}
