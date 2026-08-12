#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V23MarketValueCalibrator
{
    private const string AppraisedValuablesId = "offense:appraised-valuables";

    private static readonly HashSet<string> AutomaticSaleExclusions =
        new(StringComparer.Ordinal)
        {
            PhysicalItemIds.EquipmentModule,
            EquipmentProgressionItemIds.LineageSeal,
            "seed-lot:bloodleaf",
            "seed-lot:cave-mushroom",
            "seed-lot:dreamleaf",
            "seed-lot:ember-cotton",
            "seed-lot:ember-root",
            "seed-lot:frost-flax",
            "seed-lot:mire-reed",
            "seed-lot:moonflower",
            "seed-lot:night-grape",
            "seed-lot:shade-fiber",
            "seed-lot:spore-hemp",
            "seed-lot:twilight-grain"
        };

    [MenuItem("DungeonStory/V23/Calibrate Item Market Values")]
    public static void Apply()
    {
        GameContentCatalogSO root = Resources.Load<GameContentCatalogSO>(
            GameContentCatalogSO.ResourcePath)
            ?? throw new InvalidOperationException(
                "The required root GameContentCatalogSO could not be loaded.");
        GameDomainContentCatalogSO domain = root.DomainCatalogs
            .OfType<GameDomainContentCatalogSO>()
            .Single();
        ItemDefinitionCatalogSO itemCatalog =
            root.GetItemDefinitions<ItemDefinitionCatalogSO>();
        ContentSource source = new(domain, itemCatalog);
        ResourceMaterialEconomicProfileCatalog materialProfiles = new(source);
        V23BalanceWorkCalculator work = new(materialProfiles);
        ProductionRecipeSO[] recipes = domain.GetAll<ProductionRecipeSO>()
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        foreach (ProductionRecipeSO recipe in recipes)
        {
            recipe.ConfigureBalanceWork(work.CalculateRecipe(recipe));
            EditorUtility.SetDirty(recipe);
        }
        EmbeddedWorkValueSnapshot embeddedWork =
            new V23EmbeddedWorkValueCalculator(
                    recipes,
                    itemCatalog.Definitions,
                    domain.GetAll<CombatEquipmentDefinitionSO>(),
                    domain.GetAll<CraftMaterialDefinitionSO>(),
                    work)
                .Calculate();

        int calibrated = 0;
        int excluded = 0;
        foreach (ItemDefinitionSO item in itemCatalog.Definitions
                     .Where(value => value != null)
                     .OrderBy(value => value.ItemId, StringComparer.Ordinal))
        {
            if (item is ResourceItemDefinitionSO excludedResource
                && AutomaticSaleExclusions.Contains(item.ItemId))
            {
                excludedResource.ConfigureMarketSaleRate(0f);
                EditorUtility.SetDirty(item);
                excluded++;
                continue;
            }

            if (!embeddedWork.TryGetItemWork(item.ItemId, out float ewu)
                || ewu <= 0f)
            {
                continue;
            }

            int unitPrice = Mathf.Max(1, Mathf.RoundToInt(
                ewu * GoldEconomyBalanceRules.GoldPerEmbeddedWorkUnit));
            item.ConfigureUnitPrice(unitPrice);
            if (item is ResourceItemDefinitionSO resource)
            {
                float targetSaleGold = ewu
                    * GoldEconomyBalanceRules.GoldPerEmbeddedWorkUnit
                    * GoldEconomyBalanceRules.TargetExternalSaleRecovery;
                if (string.Equals(item.ItemId, AppraisedValuablesId, StringComparison.Ordinal))
                {
                    resource.ConfigureUnitPrice(Mathf.Max(1, Mathf.RoundToInt(targetSaleGold)));
                    resource.ConfigureMarketSaleRate(1f);
                }
                else if (resource.CanSellToMarket)
                    resource.ConfigureMarketSaleRate(Mathf.Clamp01(targetSaleGold / unitPrice));
            }

            EditorUtility.SetDirty(item);
            calibrated++;
        }

        Dictionary<string, ItemDefinitionSO> itemById = itemCatalog.Definitions
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        int guestRequests = CalibrateGuestRequestRewards(domain, itemById);
        int retailOffers = 0;
        foreach (SaleItem saleItem in Resources.LoadAll<SaleItem>("SO/Stock/Item")
                     .Where(value => value != null)
                     .OrderBy(value => value.id))
        {
            if (!itemById.TryGetValue(saleItem.ItemDefinitionId.Value, out ItemDefinitionSO item))
                continue;
            saleItem.ConfigureCost(GoldEconomyBalanceRules.CalculateRetailBasePrice(item.UnitPrice));
            EditorUtility.SetDirty(saleItem);
            retailOffers++;
        }

        foreach (AuthoredStockCategoryRecord stock in domain.StockCategories
                     .Where(value => value != null
                         && value.dailyBaseAmount > 0
                         && !string.IsNullOrWhiteSpace(value.deliveryItemId)))
        {
            if (embeddedWork.TryGetItemWork(stock.deliveryItemId, out float ewu)
                && ewu > 0f)
            {
                stock.dailyUnitCost = ewu
                    * GoldEconomyBalanceRules.TargetPurchaseGoldPerEmbeddedWorkUnit;
            }
        }

        EditorUtility.SetDirty(domain);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"V23_MARKET_VALUE_CALIBRATION=PASS; calibrated={calibrated}; "
            + $"automatic_sale_exclusions={excluded}; retail_offers={retailOffers}; "
            + $"guest_requests={guestRequests}");
    }

    private static int CalibrateGuestRequestRewards(
        GameDomainContentCatalogSO domain,
        IReadOnlyDictionary<string, ItemDefinitionSO> itemById)
    {
        int calibrated = 0;
        foreach (GuestRequestDefinitionSO request in domain
                     .GetAll<GuestRequestDefinitionSO>()
                     .Where(value => value != null)
                     .OrderBy(value => value.StableId, StringComparer.Ordinal))
        {
            int internalValue = (request.serviceRequirements?.items
                    ?? new List<V20ItemAmountRequirement>())
                .Where(value => value != null && value.consume)
                .Sum(value => itemById.TryGetValue(
                        value.itemDefinitionId?.Trim() ?? string.Empty,
                        out ItemDefinitionSO item)
                    ? item.UnitPrice * value.amount
                    : 0);
            if (internalValue <= 0)
            {
                throw new InvalidOperationException(
                    $"Guest request '{request.StableId}' has no priced consumed item requirement.");
            }

            V20ContentEffect[] rewards = (request.successEffects
                    ?? new List<V20ContentEffect>())
                .Where(value => value != null
                    && value.kind == V20ContentEffectKind.Money)
                .ToArray();
            if (rewards.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Guest request '{request.StableId}' must have exactly one money reward.");
            }

            rewards[0].amount = GoldEconomyBalanceRules
                .CalculatePremiumServiceReward(internalValue);
            EditorUtility.SetDirty(request);
            calibrated++;
        }
        return calibrated;
    }

    private sealed class ContentSource : IGameContentDefinitionSource
    {
        private readonly GameDomainContentCatalogSO domain;
        private readonly ItemDefinitionCatalogSO items;

        public ContentSource(
            GameDomainContentCatalogSO domain,
            ItemDefinitionCatalogSO items)
        {
            this.domain = domain;
            this.items = items;
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            if (typeof(T) == typeof(ItemDefinitionSO))
                return items.Definitions.Cast<T>().ToArray();
            return domain.GetAll<T>();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> definitions = GetAll<T>();
            return definitions.Count == 1
                ? definitions[0]
                : throw new InvalidOperationException(
                    $"Expected one {typeof(T).Name}, found {definitions.Count}.");
        }
    }
}
#endif
