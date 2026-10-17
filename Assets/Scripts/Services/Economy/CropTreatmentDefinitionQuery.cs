using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;

internal sealed class CropTreatmentDefinitionQuery
{
    private readonly IResourceEconomyContentCatalog catalog;

    internal CropTreatmentDefinitionQuery(IResourceEconomyContentCatalog catalog)
    {
        this.catalog = catalog;
    }

    internal string ResolveTreatmentDisplayName(string treatmentItemId) =>
        catalog.TryGetItem(
            treatmentItemId,
            out ResourceItemDefinitionSO item)
            ? item.DisplayName
            : treatmentItemId;

    internal bool TryResolveTreatment(
        string treatmentItemId,
        out ResourceItemDefinitionSO item,
        out CropTreatmentPolicy policy,
        out string failureReason)
    {
        item = null;
        policy = default;
        failureReason = string.Empty;
        string itemId = treatmentItemId ?? string.Empty;
        if (itemId.Length == 0
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || !catalog.TryGetItem(itemId, out item)
            || item == null
            || !item.TryGetCropTreatment(out policy)
            || !policy.IsValid)
        {
            failureReason = "알 수 없거나 잘못 작성된 작물 처리제입니다.";
            return false;
        }
        return true;
    }
}
