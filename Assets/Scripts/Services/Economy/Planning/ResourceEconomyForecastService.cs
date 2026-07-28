using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ResourceEconomyForecastService :
    IResourceEconomyForecastService
{
    private const string FoodSummaryId = "forecast:category:food";
    private const string WaterItemId = "stock-item:4";

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IWorldItemStackRuntime items;
    private readonly IProductionBillRuntime productionBills;
    private readonly IBuildingWorldQuery buildings;
    private readonly ICharacterWorldQuery characters;
    private readonly ICropPlotRuntime crops;
    private readonly IAnimalHusbandryRuntime husbandry;
    private readonly IWildlifeSpeciesCatalogProvider wildlifeSpecies;
    private readonly IRegionalSupplyContractRuntime contracts;
    private readonly IGrandProjectRuntime grandProjects;
    private readonly IResourceStockPolicyRuntime stockPolicies;

    public ResourceEconomyForecastService(
        IResourceEconomyContentCatalog catalog,
        IWorldItemStackRuntime items,
        IProductionBillRuntime productionBills,
        IBuildingWorldQuery buildings,
        ICharacterWorldQuery characters,
        ICropPlotRuntime crops = null,
        IAnimalHusbandryRuntime husbandry = null,
        IWildlifeSpeciesCatalogProvider wildlifeSpecies = null,
        IRegionalSupplyContractRuntime contracts = null,
        IGrandProjectRuntime grandProjects = null,
        IResourceStockPolicyRuntime stockPolicies = null)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.productionBills = productionBills
            ?? throw new ArgumentNullException(nameof(productionBills));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.crops = crops;
        this.husbandry = husbandry;
        this.wildlifeSpecies = wildlifeSpecies;
        this.contracts = contracts;
        this.grandProjects = grandProjects;
        this.stockPolicies = stockPolicies;
    }

    public ResourceEconomyForecast Capture(int horizonDays = 3)
    {
        int days = Mathf.Clamp(horizonDays, 1, 30);
        Dictionary<string, ResourceEconomyForecastRow> rows =
            catalog.Items
                .Where(item => item != null
                    && !string.IsNullOrWhiteSpace(item.ItemId))
                .ToDictionary(
                    item => item.ItemId,
                    item => NewRow(item.ItemId, item.DisplayName),
                    StringComparer.Ordinal);

        AddPhysicalStock(rows);
        AddProductionBills(rows, days);
        AddCropProduction(rows, days);
        AddHusbandryFlows(rows, days);
        AddResidentNeeds(rows, days);
        AddContractDemand(rows);
        AddGrandProjectDemand(rows);

        ResourceEconomyForecastRow[] ordered = rows.Values
            .Where(row => row.Available != 0
                || row.Reserved != 0
                || row.ExpectedProduction != 0
                || row.ExpectedDemand != 0)
            .OrderBy(row => row.ProjectedBalance)
            .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, ResourceStockPolicyData> policies =
            (stockPolicies?.Policies
                ?? Array.Empty<ResourceStockPolicyData>())
            .Where(policy => policy != null && policy.enabled)
            .ToDictionary(
                policy => policy.itemId,
                policy => policy,
                StringComparer.Ordinal);

        ResourceEconomyForecastRow[] shortages = ordered
            .Where(row => row.ProjectedBalance < ResolveMinimum(
                row.ItemId,
                policies))
            .ToArray();
        ResourceEconomyForecastRow[] surpluses = ordered
            .Where(row =>
            {
                int maximum = ResolveMaximum(row.ItemId, policies);
                return maximum < int.MaxValue
                    && row.ProjectedBalance > maximum;
            })
            .ToArray();

        return new ResourceEconomyForecast
        {
            HorizonDays = days,
            Rows = ordered,
            Shortages = shortages,
            Surpluses = surpluses
        };
    }

    private void AddPhysicalStock(
        IDictionary<string, ResourceEconomyForecastRow> rows)
    {
        int totalFood = 0;
        int reservedFood = 0;
        foreach (WorldItemStackSnapshot stack in items.GetAllStacks())
        {
            if (stack == null || stack.Quantity <= 0)
            {
                continue;
            }

            ResourceEconomyForecastRow row = GetOrCreate(
                rows,
                stack.ItemId,
                stack.DisplayName);
            bool routed = IsRouted(stack);
            if (routed)
            {
                row.Reserved += stack.Quantity;
            }
            else if (stack.State == WorldItemStackState.Stored)
            {
                row.Available += stack.Quantity;
            }

            if (stack.StockCategory == StockCategory.Food)
            {
                if (routed)
                {
                    reservedFood += stack.Quantity;
                }
                else if (stack.State == WorldItemStackState.Stored)
                {
                    totalFood += stack.Quantity;
                }
            }
        }

        ResourceEconomyForecastRow food = GetOrCreate(
            rows,
            FoodSummaryId,
            "식량 전체");
        food.Available = totalFood;
        food.Reserved = reservedFood;
    }

    private void AddProductionBills(
        IDictionary<string, ResourceEconomyForecastRow> rows,
        int days)
    {
        foreach (BuildableObject building in buildings.Buildings)
        {
            if (building == null || building.isDestroy)
            {
                continue;
            }

            foreach (ProductionBillSnapshot bill in
                     productionBills.GetBills(building))
            {
                if (bill == null
                    || bill.Status == ProductionBillStatus.Suspended
                    || bill.Status == ProductionBillStatus.Completed
                    || bill.Status == ProductionBillStatus.Cancelled)
                {
                    continue;
                }

                int cycles = bill.Mode == ProductionOrderMode.RepeatCount
                    ? Mathf.Clamp(bill.RemainingCycles, 0, days)
                    : days;
                foreach (ItemAmountDefinition input in bill.Inputs)
                {
                    GetOrCreate(rows, input.ItemId).ExpectedDemand +=
                        input.Amount * cycles;
                }

                foreach (ProductionOutputDefinition output in bill.Outputs)
                {
                    GetOrCreate(rows, output.ItemId).ExpectedProduction +=
                        Mathf.RoundToInt(
                            output.Amount * output.Probability * cycles);
                }
            }
        }
    }

    private void AddCropProduction(
        IDictionary<string, ResourceEconomyForecastRow> rows,
        int days)
    {
        foreach (CropPlotSnapshot plot in
                 crops?.Plots ?? Array.Empty<CropPlotSnapshot>())
        {
            if (plot == null
                || string.IsNullOrWhiteSpace(plot.CropId)
                || !catalog.TryGetCrop(
                    plot.CropId,
                    out CropDefinitionSO crop))
            {
                continue;
            }

            float readiness = plot.Phase switch
            {
                CropPlotPhase.ReadyToHarvest or CropPlotPhase.Harvesting => 1f,
                CropPlotPhase.Growing => Mathf.Clamp01(
                    plot.GrowthProgress + days * 0.34f),
                CropPlotPhase.Sowing or CropPlotPhase.ReadyToSow => days >= 3
                    ? 0.75f
                    : 0.35f,
                _ => 0f
            };
            GetOrCreate(rows, crop.HarvestItemId).ExpectedProduction +=
                Mathf.RoundToInt(crop.Yield * readiness);
        }
    }

    private void AddHusbandryFlows(
        IDictionary<string, ResourceEconomyForecastRow> rows,
        int days)
    {
        foreach (HusbandryAnimalState animal in
                 husbandry?.Animals ?? Array.Empty<HusbandryAnimalState>())
        {
            if (animal == null || !animal.tamed)
            {
                continue;
            }

            string feedItem = ResolveFeedItem(animal.speciesId);
            GetOrCreate(rows, feedItem).ExpectedDemand += days;
            foreach (AnimalProductProgressState product in
                     animal.products
                     ?? new List<AnimalProductProgressState>())
            {
                if (product == null
                    || string.IsNullOrWhiteSpace(product.itemId))
                {
                    continue;
                }

                GetOrCreate(rows, product.itemId).ExpectedProduction +=
                    product.readyCycles
                    + (product.progressDays + days >= 1f ? 1 : 0);
            }

            GetOrCreate(rows, "resource:manure").ExpectedProduction +=
                animal.readyManureCycles
                + (animal.manureProgressDays + days >= 1f ? 1 : 0);
        }
    }

    private void AddResidentNeeds(
        IDictionary<string, ResourceEconomyForecastRow> rows,
        int days)
    {
        int residents = characters.Characters.Count(IsResident);
        GetOrCreate(rows, FoodSummaryId, "식량 전체").ExpectedDemand +=
            residents * days;
        GetOrCreate(rows, WaterItemId, "식수").ExpectedDemand +=
            residents * days;
    }

    private void AddContractDemand(
        IDictionary<string, ResourceEconomyForecastRow> rows)
    {
        foreach (RegionalSupplyContractState contract in
                 contracts?.Contracts
                 ?? Array.Empty<RegionalSupplyContractState>())
        {
            if (contract == null
                || contract.status is not (
                    RegionalSupplyContractStatus.Accepted
                    or RegionalSupplyContractStatus.Delivering))
            {
                continue;
            }

            foreach (RegionalSupplyContractRequirement requirement in
                     contract.requirements
                     ?? new List<RegionalSupplyContractRequirement>())
            {
                int alreadyRouted = CountAtDestination(
                    requirement.itemId,
                    contract.destinationId);
                GetOrCreate(rows, requirement.itemId).ExpectedDemand +=
                    Mathf.Max(0, requirement.amount - alreadyRouted);
            }
        }
    }

    private void AddGrandProjectDemand(
        IDictionary<string, ResourceEconomyForecastRow> rows)
    {
        if (grandProjects == null
            || string.IsNullOrWhiteSpace(
                grandProjects.State?.activeProjectId))
        {
            return;
        }

        GrandProjectDefinition definition = grandProjects.Definitions
            .FirstOrDefault(candidate => string.Equals(
                candidate.ProjectId,
                grandProjects.State.activeProjectId,
                StringComparison.Ordinal));
        if (definition == null)
        {
            return;
        }

        foreach (ItemAmountDefinition requirement in definition.Requirements)
        {
            int alreadyRouted = CountAtDestination(
                requirement.ItemId,
                grandProjects.State.destinationId);
            GetOrCreate(rows, requirement.ItemId).ExpectedDemand +=
                Mathf.Max(0, requirement.Amount - alreadyRouted);
        }
    }

    private int CountAtDestination(string itemId, string destinationId)
    {
        if (string.IsNullOrWhiteSpace(destinationId))
        {
            return 0;
        }

        return items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    itemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
    }

    private string ResolveFeedItem(string speciesId)
    {
        if (wildlifeSpecies != null
            && wildlifeSpecies.TryGetSpecies(
                speciesId,
                out WildlifeSpeciesDefinition species)
            && species.Diet is WildlifeDietType.Carnivore
                or WildlifeDietType.Scavenger)
        {
            return "feed:dog-food";
        }

        return "feed:hay";
    }

    private ResourceEconomyForecastRow GetOrCreate(
        IDictionary<string, ResourceEconomyForecastRow> rows,
        string itemId,
        string fallbackName = "")
    {
        string id = itemId?.Trim() ?? string.Empty;
        if (rows.TryGetValue(id, out ResourceEconomyForecastRow existing))
        {
            return existing;
        }

        string displayName = fallbackName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(displayName)
            && catalog.TryGetItem(
                id,
                out ResourceItemDefinitionSO definition))
        {
            displayName = definition.DisplayName;
        }

        ResourceEconomyForecastRow created = NewRow(
            id,
            string.IsNullOrWhiteSpace(displayName) ? id : displayName);
        rows[id] = created;
        return created;
    }

    private static ResourceEconomyForecastRow NewRow(
        string itemId,
        string displayName)
    {
        return new ResourceEconomyForecastRow
        {
            ItemId = itemId ?? string.Empty,
            DisplayName = displayName ?? itemId ?? string.Empty
        };
    }

    private static bool IsRouted(WorldItemStackSnapshot stack)
    {
        return stack.IsReserved
            || (!string.IsNullOrWhiteSpace(stack.DestinationId)
                && !stack.DestinationId.StartsWith(
                    "warehouse:",
                    StringComparison.Ordinal));
    }

    private static bool IsResident(CharacterActor actor)
    {
        return actor != null
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            && (actor.IsOwner
                || StaffDiscontentService.IsTrackableStaff(actor));
    }

    private static int ResolveMinimum(
        string itemId,
        IReadOnlyDictionary<string, ResourceStockPolicyData> policies)
    {
        return policies.TryGetValue(
                itemId ?? string.Empty,
                out ResourceStockPolicyData policy)
            ? policy.minimumStock
            : 0;
    }

    private static int ResolveMaximum(
        string itemId,
        IReadOnlyDictionary<string, ResourceStockPolicyData> policies)
    {
        return policies.TryGetValue(
                itemId ?? string.Empty,
                out ResourceStockPolicyData policy)
            ? policy.maximumStock
            : int.MaxValue;
    }
}
