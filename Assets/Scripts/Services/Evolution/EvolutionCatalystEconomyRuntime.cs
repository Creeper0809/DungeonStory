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
    private readonly IGameMoneyAccount money;

    public EvolutionCatalystEconomyRuntime(
        IWorldItemStackRuntime items,
        IGameMoneyAccount money)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
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

        if (!items.TryConsumeStackQuantity(source.StackId, 1, out _))
        {
            failureReason = "촉매를 분해할 수 없습니다.";
            return false;
        }

        if (items.SpawnItemAt(
                EvolutionCatalystItemId.BuildResidue(
                    catalyst.progressionLevel),
                1,
                outputPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            && spawned == 1)
        {
            return true;
        }

        RestoreItem(source.ItemId, outputPosition);
        failureReason = "촉매 잔재를 놓을 수 없습니다.";
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

        if (!items.TryConsumeStackQuantity(source.StackId, 1, out _))
        {
            failureReason = "교환할 촉매를 회수할 수 없습니다.";
            return false;
        }

        if (!items.SpawnItemAt(
                outputId,
                1,
                outputPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned != 1)
        {
            RestoreItem(source.ItemId, outputPosition);
            failureReason = "교환한 촉매를 놓을 수 없습니다.";
            return false;
        }

        if (!money.TrySpend(
                price,
                new EconomyTransactionContext(
                    EconomyTransactionKind.ShopPurchase,
                    "evolution-catalyst-exchange",
                    sourceCatalystStackId,
                    "촉매 계열 교환"),
                out failureReason))
        {
            RestoreItem(source.ItemId, outputPosition);
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
        foreach (WorldItemStackSnapshot source in sources)
        {
            int consume = Mathf.Min(remaining, source.Quantity);
            if (!items.TryConsumeStackQuantity(source.StackId, consume, out _))
            {
                failureReason = "촉매 재료를 소비할 수 없습니다.";
                return false;
            }

            remaining -= consume;
            if (remaining <= 0)
            {
                break;
            }
        }

        if (items.SpawnItemAt(
                outputItemId,
                1,
                outputPosition,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            && spawned == 1)
        {
            return true;
        }

        items.SpawnItemAt(
            inputItemId,
            inputAmount,
            outputPosition,
            WorldItemStackState.Loose,
            string.Empty,
            out _);
        failureReason = "정제 결과물을 놓을 수 없습니다.";
        return false;
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

    private void RestoreItem(string itemId, Vector2Int position)
    {
        items.SpawnItemAt(
            itemId,
            1,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            out _);
    }
}
