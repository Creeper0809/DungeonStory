using System;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class ResourceStockPolicySaveValidation
{
    public static void Validate(
        DungeonResourceStockPolicySaveData data,
        IResourceEconomyContentCatalog catalog,
        DungeonGameRestoreReport report)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }
        if (data == null || data.policies == null)
        {
            report.AddError("Stock-policy payload or policy list is null.");
            return;
        }
        if (catalog?.Items == null)
        {
            report.AddError("Stock-policy validation has no item catalog.");
            return;
        }
        if (data.version != DungeonResourceStockPolicySaveData.CurrentVersion)
        {
            report.AddError(
                $"Stock-policy payload version {data.version} is unsupported.");
        }

        ResourceItemDefinitionSO[] expectedItems = catalog.Items
            .Where(item => item != null)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();
        if (data.policies.Count != expectedItems.Length)
        {
            report.AddError(
                "Stock-policy payload does not contain every authored item exactly once.");
            return;
        }

        for (int index = 0; index < expectedItems.Length; index++)
        {
            ResourceStockPolicyData policy = data.policies[index];
            string expectedId = expectedItems[index].ItemId;
            if (policy == null
                || !IsCanonical(policy.itemId)
                || !string.Equals(policy.itemId, expectedId, StringComparison.Ordinal))
            {
                report.AddError(
                    $"Stock-policy entry {index} does not match authored item '{expectedId}'.");
                continue;
            }
            if (!catalog.TryGetItem(policy.itemId, out _))
            {
                report.AddError(
                    $"Stock-policy '{policy.itemId}' is not a concrete authored item.");
            }
            if (!Enum.IsDefined(
                    typeof(StockSurplusDisposition),
                    policy.surplusDisposition))
            {
                report.AddError(
                    $"Stock-policy '{policy.itemId}' has an invalid surplus disposition.");
            }
            if (policy.minimumStock < 0
                || policy.targetStock < policy.minimumStock
                || policy.maximumStock < policy.targetStock)
            {
                report.AddError(
                    $"Stock-policy '{policy.itemId}' has invalid thresholds.");
            }
            if (policy.isEmergencyReserve && !policy.enabled)
            {
                report.AddError(
                    $"Emergency stock-policy '{policy.itemId}' is disabled.");
            }
            if (policy.lastStatus == null
                || !string.Equals(
                    policy.lastStatus,
                    policy.lastStatus.Trim(),
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Stock-policy '{policy.itemId}' has a non-canonical status.");
            }
        }
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);
}
