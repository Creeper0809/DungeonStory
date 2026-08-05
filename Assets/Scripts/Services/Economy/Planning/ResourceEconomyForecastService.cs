using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ResourceEconomyForecastInventoryDependencies
{
    public ResourceEconomyForecastInventoryDependencies(
        IResourceEconomyContentCatalog catalog,
        IWorldItemStackRuntime items,
        IProductionBillQuery productionBills,
        IBuildingWorldQuery buildings)
    {
        Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Items = items ?? throw new ArgumentNullException(nameof(items));
        ProductionBills = productionBills
            ?? throw new ArgumentNullException(nameof(productionBills));
        Buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
    }

    public IResourceEconomyContentCatalog Catalog { get; }
    public IWorldItemStackRuntime Items { get; }
    public IProductionBillQuery ProductionBills { get; }
    public IBuildingWorldQuery Buildings { get; }
}

public sealed class ResourceEconomyForecastPopulationDependencies
{
    public ResourceEconomyForecastPopulationDependencies(
        ICharacterWorldQuery characters,
        ICropPlotRuntime crops,
        IAnimalHusbandryQuery husbandry,
        IWildlifeSpeciesCatalogProvider wildlifeSpecies)
    {
        Characters = characters ?? throw new ArgumentNullException(nameof(characters));
        Crops = crops ?? throw new ArgumentNullException(nameof(crops));
        Husbandry = husbandry ?? throw new ArgumentNullException(nameof(husbandry));
        WildlifeSpecies = wildlifeSpecies
            ?? throw new ArgumentNullException(nameof(wildlifeSpecies));
    }

    public ICharacterWorldQuery Characters { get; }
    public ICropPlotRuntime Crops { get; }
    public IAnimalHusbandryQuery Husbandry { get; }
    public IWildlifeSpeciesCatalogProvider WildlifeSpecies { get; }
}

public sealed class ResourceEconomyForecastPlanningDependencies
{
    public ResourceEconomyForecastPlanningDependencies(
        IRegionalSupplyContractRuntime contracts,
        IGrandProjectRuntime grandProjects,
        IResourceStockPolicyRuntime stockPolicies)
    {
        Contracts = contracts ?? throw new ArgumentNullException(nameof(contracts));
        GrandProjects = grandProjects
            ?? throw new ArgumentNullException(nameof(grandProjects));
        StockPolicies = stockPolicies
            ?? throw new ArgumentNullException(nameof(stockPolicies));
    }

    public IRegionalSupplyContractRuntime Contracts { get; }
    public IGrandProjectRuntime GrandProjects { get; }
    public IResourceStockPolicyRuntime StockPolicies { get; }
}

public sealed class ResourceEconomyForecastService :
    IResourceEconomyForecastService
{
    private const string FoodSummaryId = "forecast:category:food";
    private const string WaterItemId = "resource:clean-water";

    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IWorldItemStackRuntime items;
    private readonly IProductionBillQuery productionBills;
    private readonly IBuildingWorldQuery buildings;
    private readonly ICharacterWorldQuery characters;
    private readonly ICropPlotRuntime crops;
    private readonly IAnimalHusbandryQuery husbandry;
    private readonly IWildlifeSpeciesCatalogProvider wildlifeSpecies;
    private readonly IRegionalSupplyContractRuntime contracts;
    private readonly IGrandProjectRuntime grandProjects;
    private readonly IResourceStockPolicyRuntime stockPolicies;

    public ResourceEconomyForecastService(
        ResourceEconomyForecastInventoryDependencies inventory,
        ResourceEconomyForecastPopulationDependencies population,
        ResourceEconomyForecastPlanningDependencies planning)
    {
        inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        population = population ?? throw new ArgumentNullException(nameof(population));
        planning = planning ?? throw new ArgumentNullException(nameof(planning));
        catalog = inventory.Catalog;
        items = inventory.Items;
        productionBills = inventory.ProductionBills;
        buildings = inventory.Buildings;
        characters = population.Characters;
        crops = population.Crops;
        husbandry = population.Husbandry;
        wildlifeSpecies = population.WildlifeSpecies;
        contracts = planning.Contracts;
        grandProjects = planning.GrandProjects;
        stockPolicies = planning.StockPolicies;
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
            stockPolicies.Policies
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
                 crops.Plots)
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
                 husbandry.Animals)
        {
            if (animal == null || !animal.Tamed)
            {
                continue;
            }

            string feedItem = ResolveFeedItem(animal.SpeciesId.Value);
            GetOrCreate(rows, feedItem).ExpectedDemand += days;
            foreach (AnimalProductProgressState product in
                     animal.Products
                     ?? new List<AnimalProductProgressState>())
            {
                if (product == null
                    || !product.ItemId.IsValid)
                {
                    continue;
                }

                GetOrCreate(rows, product.ItemId.Value).ExpectedProduction +=
                    product.ReadyCycles
                    + (product.ProgressDays + days >= 1f ? 1 : 0);
            }

            GetOrCreate(rows, "resource:manure").ExpectedProduction +=
                animal.ReadyManureCycles
                + (animal.ManureProgressDays + days >= 1f ? 1 : 0);
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
                 contracts.Contracts)
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
        if (string.IsNullOrWhiteSpace(
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
        if (wildlifeSpecies.TryGetSpecies(
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
