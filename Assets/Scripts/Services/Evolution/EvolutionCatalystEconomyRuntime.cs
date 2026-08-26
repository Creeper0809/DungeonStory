using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class EvolutionCatalystEconomyRules
{
    public const int RefinementResidueCost = 3;
    public const int ProgressionUpgradeResidueCost = 5;
    public const float MerchantExchangeValueMultiplier = 1.5f;
}

public static class EvolutionCatalystItemDefinitions
{
    public const string FacilityPackageItemId =
        "evolution:facility-package";

    public static int GetCatalystValue(int progressionLevel)
    {
        EvolutionCatalystProgression.RequireValid(progressionLevel);
        int exponent = progressionLevel - 1;
        long value = 120L << exponent;
        return (int)Math.Min(int.MaxValue, value);
    }

    public static string GetFamilyDisplayName(string family)
    {
        return family?.Trim().ToLowerInvariant() switch
        {
            "offense" => "공세",
            "defense" => "수호",
            "industry" => "산업",
            "survival" => "생존",
            "arcane" => "비전",
            "authority" => "권위",
            _ => "범용"
        };
    }
}

public interface IEvolutionCatalystEconomyRuntime
{
    bool TryDismantle(
        string catalystStackId,
        Vector2Int outputPosition,
        out string failureReason);
    bool TryRefine(
        int progressionLevel,
        string catalystFamily,
        Vector2Int outputPosition,
        out string failureReason);
    bool TryAdvanceResidue(
        int progressionLevel,
        Vector2Int outputPosition,
        out string failureReason);
    bool TryMerchantExchange(
        string sourceCatalystStackId,
        string targetFamily,
        Vector2Int outputPosition,
        out int goldSpent,
        out string failureReason);
}

public sealed class EvolutionCatalystEconomyRuntime :
    IEvolutionCatalystEconomyRuntime
{
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemTransformService physicalTransforms;
    private readonly IGameMoneyAccount money;

    public EvolutionCatalystEconomyRuntime(
        IWorldItemStackRuntime items,
        IPhysicalItemTransformService physicalTransforms,
        IGameMoneyAccount money)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.physicalTransforms = physicalTransforms
            ?? throw new ArgumentNullException(nameof(physicalTransforms));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
    }

    public bool TryDismantle(
        string catalystStackId,
        Vector2Int outputPosition,
        out string failureReason)
    {
        failureReason = string.Empty;
        WorldItemStackSnapshot source = FindStack(catalystStackId);
        if (source == null
            || !EvolutionCatalystItemId.TryParseCatalyst(
                source.ItemId,
                out EquipmentCatalystDefinition catalyst))
        {
            failureReason = "분해할 촉매 스택을 찾을 수 없습니다.";
            return false;
        }

        string operationId =
            $"evolution-catalyst-dismantle:{source.StackId}:{source.Quantity:D8}";
        if (physicalTransforms.TryTransformQuantity(
                source.StackId,
                1,
                new[]
                {
                    new PhysicalItemTransformOutput(
                        EvolutionCatalystItemId.BuildResidue(
                            catalyst.progressionLevel),
                        1,
                        outputPosition)
                },
                operationId,
                "evolution-catalyst-dismantle",
                out PhysicalItemTransformReceipt receipt,
                out _,
                out failureReason)
            && receipt.IsCommitted)
        {
            return true;
        }

        failureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "촉매 분해의 물리 질량 변환을 커밋할 수 없습니다."
            : failureReason;
        return false;
    }

    public bool TryRefine(
        int progressionLevel,
        string catalystFamily,
        Vector2Int outputPosition,
        out string failureReason)
    {
        if (!EvolutionCatalystProgression.IsValid(progressionLevel))
        {
            failureReason = "촉매 진행 단계가 유효하지 않습니다.";
            return false;
        }

        string outputId = EvolutionCatalystItemId.BuildCatalyst(
            catalystFamily,
            progressionLevel);
        if (!EvolutionCatalystItemId.TryParseCatalyst(outputId, out _))
        {
            failureReason = "정제할 촉매 계열이 올바르지 않습니다.";
            return false;
        }

        return TryTransform(
            EvolutionCatalystItemId.BuildResidue(progressionLevel),
            EvolutionCatalystEconomyRules.RefinementResidueCost,
            outputId,
            outputPosition,
            out failureReason);
    }

    public bool TryAdvanceResidue(
        int progressionLevel,
        Vector2Int outputPosition,
        out string failureReason)
    {
        if (!EvolutionCatalystProgression.IsValid(progressionLevel)
            || progressionLevel >= EvolutionCatalystProgression.MaximumLevel)
        {
            failureReason = "더 높은 촉매 진행 단계가 존재하지 않습니다.";
            return false;
        }

        return TryTransform(
            EvolutionCatalystItemId.BuildResidue(progressionLevel),
            EvolutionCatalystEconomyRules.ProgressionUpgradeResidueCost,
            EvolutionCatalystItemId.BuildResidue(progressionLevel + 1),
            outputPosition,
            out failureReason);
    }

    public bool TryMerchantExchange(
        string sourceCatalystStackId,
        string targetFamily,
        Vector2Int outputPosition,
        out int goldSpent,
        out string failureReason)
    {
        goldSpent = 0;
        failureReason = string.Empty;
        WorldItemStackSnapshot source = FindStack(sourceCatalystStackId);
        if (source == null
            || !EvolutionCatalystItemId.TryParseCatalyst(
                source.ItemId,
                out EquipmentCatalystDefinition catalyst))
        {
            failureReason = "교환할 촉매 스택을 찾을 수 없습니다.";
            return false;
        }

        string outputId = EvolutionCatalystItemId.BuildCatalyst(
            targetFamily,
            catalyst.progressionLevel);
        if (!EvolutionCatalystItemId.TryParseCatalyst(outputId, out _))
        {
            failureReason = "교환할 촉매 계열이 올바르지 않습니다.";
            return false;
        }

        int price = Mathf.CeilToInt(
            EvolutionCatalystItemDefinitions.GetCatalystValue(
                catalyst.progressionLevel)
            * EvolutionCatalystEconomyRules.MerchantExchangeValueMultiplier);
        if (!money.CanSpend(price))
        {
            failureReason = $"촉매 교환에 {price} 골드가 필요합니다.";
            return false;
        }

        EconomyTransactionContext exchangeContext = new(
            EconomyTransactionKind.CatalystExchange,
            "evolution-catalyst-exchange",
            sourceCatalystStackId,
            "촉매 계열 교환");
        if (!money.TrySpend(
                price,
                exchangeContext,
                out failureReason))
        {
            return false;
        }

        string operationId = $"evolution-catalyst-exchange:{source.StackId}:{outputId}";
        if (!physicalTransforms.TryTransformQuantity(
                source.StackId,
                1,
                new[]
                {
                    new PhysicalItemTransformOutput(
                        outputId,
                        1,
                        outputPosition)
                },
                operationId,
                "evolution-catalyst-exchange",
                out PhysicalItemTransformReceipt receipt,
                out _,
                out failureReason)
            || !receipt.IsCommitted)
        {
            money.Add(
                price,
                new EconomyTransactionContext(
                    EconomyTransactionKind.CatalystExchangeRefund,
                    "evolution-catalyst-exchange",
                    sourceCatalystStackId,
                    "촉매 계열 교환 실패 환급"));
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "교환한 촉매의 물리 변환을 커밋할 수 없습니다."
                : failureReason;
            return false;
        }

        goldSpent = price;
        return true;
    }

    private bool TryTransform(
        string inputItemId,
        int inputAmount,
        string outputItemId,
        Vector2Int outputPosition,
        out string failureReason)
    {
        failureReason = string.Empty;
        List<WorldItemStackSnapshot> sources = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    inputItemId,
                    StringComparison.Ordinal)
                && stack.AvailableQuantity > 0
                && !stack.Forbidden
                && stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored)
            .OrderByDescending(stack => stack.State == WorldItemStackState.Stored)
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToList();
        if (sources.Sum(stack => stack.Quantity) < inputAmount)
        {
            failureReason = $"재료가 부족합니다: {inputItemId} x{inputAmount}";
            return false;
        }

        int remaining = inputAmount;
        List<PhysicalItemTransformInput> inputs = new();
        foreach (WorldItemStackSnapshot source in sources)
        {
            int consume = Mathf.Min(remaining, source.Quantity);
            inputs.Add(new PhysicalItemTransformInput(source.StackId, consume));
            remaining -= consume;
            if (remaining <= 0)
            {
                break;
            }
        }
        string operationId = $"evolution-catalyst-transform:{outputItemId}:{outputPosition.x}:{outputPosition.y}";
        return physicalTransforms.TryTransformQuantities(
            inputs,
            new[]
            {
                new PhysicalItemTransformOutput(outputItemId, 1, outputPosition)
            },
            operationId,
            "evolution-catalyst-transform",
            out PhysicalItemTransformReceipt receipt,
            out _,
            out failureReason)
            && receipt.IsCommitted;
    }

    private WorldItemStackSnapshot FindStack(string stackId)
    {
        return items.GetAllStacks().FirstOrDefault(stack =>
            stack != null
            && string.Equals(
                stack.StackId,
                stackId?.Trim(),
                StringComparison.Ordinal));
    }

}
