using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public sealed class ProductionRoutePanelPresenter
{
    private readonly IProductionDistributionPolicyCommand billCommands;
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionDependencyCatalog dependencies;
    private readonly IProductionUiTextQuery text;

    public ProductionRoutePanelPresenter(
        IProductionDistributionPolicyCommand billCommands,
        IResourceEconomyContentCatalog catalog,
        IProductionDependencyCatalog dependencies,
        IProductionUiTextQuery text)
    {
        this.billCommands = billCommands
            ?? throw new ArgumentNullException(nameof(billCommands));
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
        this.dependencies = dependencies;
        this.text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public IReadOnlyList<ProductionConsumerRoutePolicy> BuildRoutePolicies(
        ProductionBillSnapshot bill)
    {
        if (bill == null)
        {
            return Array.Empty<ProductionConsumerRoutePolicy>();
        }
        if (bill.RouteStates.Count > 0)
        {
            return bill.RouteStates
                .Where(state => state?.policy != null)
                .Select(state => state.policy.Clone())
                .ToArray();
        }
        if (bill.RoutePolicies.Count > 0 || dependencies == null)
        {
            return bill.RoutePolicies;
        }
        if (!catalog.TryGetRecipe(bill.RecipeId, out ProductionRecipeSO recipe))
        {
            return Array.Empty<ProductionConsumerRoutePolicy>();
        }
        return recipe.Outputs
            .Where(output => output != null)
            .SelectMany(output => dependencies.GetConsumers(output.ItemId))
            .Where(link => link != null && link.IsRealConsumer)
            .GroupBy(link => link.consumerId, StringComparer.Ordinal)
            .Select(group => new ProductionConsumerRoutePolicy
            {
                consumerId = group.Key,
                enabled = true,
                priority = 50,
                weight = 1
            })
            .ToArray();
    }

    public void Render(
        Transform parent,
        ProductionBillSnapshot bill,
        int billIndex,
        TMP_FontAsset font,
        ICollection<GameObject> created,
        Action<ProductionBillCommandResult> applyResult)
    {
        ProductionConsumerRoutePolicy[] policies = BuildRoutePolicies(bill)
            .Where(policy => policy != null)
            .Select(policy => policy.Clone())
            .ToArray();
        if (policies.Length == 0)
        {
            return;
        }

        float routeRowHeight = policies.Length > 6 ? 24f : 52f;

        ProductionBuildingViewFactory.AddText(
            parent,
            text.Get(ProductionUiTextId.Header),
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            22f,
            created);
        for (int routeIndex = 0; routeIndex < policies.Length; routeIndex++)
        {
            int capturedIndex = routeIndex;
            ProductionConsumerRoutePolicy policy = policies[routeIndex];
            GameObject controls = ProductionBuildingViewFactory.CreateRouteEditor(
                parent,
                $"ProductionRoute_{billIndex}_{routeIndex}",
                ResolveConsumerLabel(bill, policy.consumerId),
                font,
                routeRowHeight);
            created.Add(controls);

            AddAdjustment(
                controls.transform,
                text.Get(
                    ProductionUiTextId.PriorityIncrease,
                    policy.priority),
                $"ProductionRoutePriorityPlus_{billIndex}_{routeIndex}",
                font,
                bill,
                policies,
                capturedIndex,
                route => route.priority = Mathf.Clamp(route.priority + 1, 0, 100),
                applyResult);
            AddAdjustment(
                controls.transform,
                text.Get(
                    ProductionUiTextId.WeightIncrease,
                    policy.weight),
                $"ProductionRouteWeightPlus_{billIndex}_{routeIndex}",
                font,
                bill,
                policies,
                capturedIndex,
                route => route.weight = Mathf.Clamp(route.weight + 1, 1, 10),
                applyResult);
            AddAdjustment(
                controls.transform,
                text.Get(
                    ProductionUiTextId.MinimumReserveIncrease,
                    policy.minimumReserve),
                $"ProductionRouteReservePlus_{billIndex}_{routeIndex}",
                font,
                bill,
                policies,
                capturedIndex,
                route =>
                {
                    route.minimumReserve++;
                    route.targetStock = Mathf.Max(
                        route.minimumReserve,
                        route.targetStock);
                },
                applyResult);
        }
    }

    private void AddAdjustment(
        Transform parent,
        string label,
        string objectName,
        TMP_FontAsset font,
        ProductionBillSnapshot bill,
        IReadOnlyList<ProductionConsumerRoutePolicy> source,
        int routeIndex,
        Action<ProductionConsumerRoutePolicy> mutate,
        Action<ProductionBillCommandResult> applyResult)
    {
        ProductionBuildingViewFactory.AddButton(
            parent,
            label,
            font,
            false,
            () =>
            {
                ProductionConsumerRoutePolicy[] updated = source
                    .Select(policy => policy.Clone())
                    .ToArray();
                mutate(updated[routeIndex]);
                applyResult?.Invoke(
                    billCommands.SetDistributionPolicy(
                        bill.BillId,
                        bill.DistributionMode,
                        updated));
            },
            objectName,
            76f);
    }

    private string ResolveConsumerLabel(
        ProductionBillSnapshot bill,
        string consumerId)
    {
        ProductionConsumerRouteState live = bill?.RouteStates
            .FirstOrDefault(state => state?.policy != null
                && string.Equals(
                    state.policy.consumerId,
                    consumerId,
                    StringComparison.Ordinal));
        if (live != null)
        {
            string label = string.IsNullOrWhiteSpace(live.displayName)
                ? consumerId
                : live.displayName;
            string status = string.IsNullOrWhiteSpace(live.blockedReason)
                ? text.Get(
                    ProductionUiTextId.StatusDemandReserved,
                    live.stage,
                    live.currentDemand,
                    live.reservedQuantity)
                : text.Get(
                    ProductionUiTextId.StatusBlocked,
                    live.blockedReason);
            return $"{label} [{consumerId}] | {status}";
        }
        if (string.IsNullOrWhiteSpace(consumerId)
            || dependencies == null
            || bill == null
            || !catalog.TryGetRecipe(bill.RecipeId, out ProductionRecipeSO recipe))
        {
            return consumerId ?? string.Empty;
        }

        ProductionConsumerLink link = recipe.Outputs
            .Where(output => output != null)
            .SelectMany(output => dependencies.GetConsumers(output.ItemId))
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.consumerId,
                    consumerId,
                    StringComparison.Ordinal));
        string resolved = link == null || string.IsNullOrWhiteSpace(link.displayName)
            ? consumerId
            : link.displayName;
        string inactive = text.Get(ProductionUiTextId.StatusInactiveConsumer);
        return $"{resolved} [{consumerId}] | {inactive}";
    }
}
