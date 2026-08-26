using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct BuildingSummaryPresentation
{
    public BuildingSummaryPresentation(string objectName, IReadOnlyList<string> detailLines)
    {
        ObjectName = objectName ?? string.Empty;
        DetailLines = detailLines ?? Array.Empty<string>();
        StockText = string.Join("\n", DetailLines);
    }

    public string ObjectName { get; }
    public string StockText { get; }
    public IReadOnlyList<string> DetailLines { get; }
}

public interface IBuildingSummaryFormatter
{
    BuildingSummaryPresentation Format(BuildableObject building);
}

public sealed class BuildingSummaryFormatter : IBuildingSummaryFormatter
{
    private readonly IBuildingDefinitionLookup buildingDefinitionLookup;
    private readonly IBuildingCharacterDisplayQuery characterDisplayQuery;
    private readonly IBuildingWorkOrderSummaryQuery workOrderSummaryQuery;
    private readonly IWorldFilthQuery worldFilthQuery;
    private readonly IStockCategoryDefinitionCatalog stockCategoryCatalog;
    private readonly IBuildingCategoryDefinitionCatalog buildingCategoryCatalog;

    public BuildingSummaryFormatter(
        IBuildingDefinitionLookup buildingDefinitionLookup,
        IBuildingCharacterDisplayQuery characterDisplayQuery,
        IBuildingWorkOrderSummaryQuery workOrderSummaryQuery,
        IWorldFilthQuery worldFilthQuery,
        IStockCategoryDefinitionCatalog stockCategoryCatalog,
        IBuildingCategoryDefinitionCatalog buildingCategoryCatalog)
    {
        this.buildingDefinitionLookup = buildingDefinitionLookup
            ?? throw new ArgumentNullException(nameof(buildingDefinitionLookup));
        this.characterDisplayQuery = characterDisplayQuery
            ?? throw new ArgumentNullException(nameof(characterDisplayQuery));
        this.workOrderSummaryQuery = workOrderSummaryQuery
            ?? throw new ArgumentNullException(nameof(workOrderSummaryQuery));
        this.worldFilthQuery = worldFilthQuery
            ?? throw new ArgumentNullException(nameof(worldFilthQuery));
        this.stockCategoryCatalog = stockCategoryCatalog
            ?? throw new ArgumentNullException(nameof(stockCategoryCatalog));
        this.buildingCategoryCatalog = buildingCategoryCatalog
            ?? throw new ArgumentNullException(nameof(buildingCategoryCatalog));
    }

    public BuildingSummaryPresentation Format(BuildableObject building)
    {
        if (building == null)
        {
            throw new ArgumentNullException(nameof(building));
        }

        BuildingSO data = building is WorldFilthWorkTarget
            ? building.BuildingData
            : buildingDefinitionLookup.GetBuilding(building.id) ?? building.BuildingData;
        string objectName = !string.IsNullOrWhiteSpace(data != null ? data.objectName : null)
            ? data.objectName
            : building.name;
        return new BuildingSummaryPresentation(objectName, BuildDetailLines(building));
    }

    private IReadOnlyList<string> BuildDetailLines(BuildableObject building)
    {
        if (building is ConstructionSite site)
        {
            return BuildConstructionDetailLines(site);
        }

        if (building is WorldFilthWorkTarget filthTarget)
        {
            return BuildFilthDetailLines(filthTarget);
        }

        List<string> lines = new List<string>
        {
            BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Status",
                building.IsDamaged
                    ? BuildingSummaryUiTextQuery.Get("BuildingSummary.State.Damaged")
                    : BuildingSummaryUiTextQuery.Get("BuildingSummary.State.Normal"),
                building.FacilityLevel),
            BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.LocationCategory",
                building.centerPos.x,
                building.centerPos.y,
                FormatCategory(building.category))
        };

        FacilityData facility = building.Facility;
        if (facility != null)
        {
            string capacity = facility.capacity > 0
                ? BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Facility.Usage",
                    building.CurrentUserCount,
                    facility.capacity,
                    building.ActiveVisitReservationCount)
                : BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Facility.NoVisitUsage");
            lines.Add(capacity);
            lines.Add(BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Facility.Roles",
                FormatRoles(facility.roles)));
            lines.Add(BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Facility.Work",
                FormatWorkTypes(facility.SupportedWorkTypeIds),
                facility.requiredWorkers));
        }

        string stock = FormatStockText(building);
        if (!string.IsNullOrWhiteSpace(stock))
        {
            lines.Add(stock);
        }

        string crafting = FormatEquipmentCraftingText(building);
        if (!string.IsNullOrWhiteSpace(crafting))
        {
            lines.Add(crafting);
        }

        return lines;
    }

    private IReadOnlyList<string> BuildFilthDetailLines(WorldFilthWorkTarget target)
    {
        IReadOnlyList<WorldFilthSnapshot> entries = worldFilthQuery.GetAt(target.centerPos)
            ?? Array.Empty<WorldFilthSnapshot>();
        float amount = entries.Sum(entry => entry.Amount);
        float infection = entries.Select(entry => entry.InfectionRisk).DefaultIfEmpty(0f).Max();
        float cleanlinessPenalty = worldFilthQuery.GetCleanlinessPenalty(target.centerPos);
        string types = entries.Count > 0
            ? string.Join(", ", entries.Select(entry => FormatFilthType(entry.Type)).Distinct())
            : BuildingSummaryUiTextQuery.Get("BuildingSummary.Filth.Removed");
        string source = entries.Select(entry => entry.SourceCharacterId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

        List<string> lines = new List<string>
        {
            BuildingSummaryUiTextQuery.Get("BuildingSummary.Filth.Type", types),
            BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Filth.Location",
                target.centerPos.x,
                target.centerPos.y,
                entries.Any(entry => entry.WallStain)
                    ? BuildingSummaryUiTextQuery.Get("BuildingSummary.Filth.SurfaceFloorAndWall")
                    : BuildingSummaryUiTextQuery.Get("BuildingSummary.Filth.SurfaceFloor")),
            BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Filth.Amount",
                amount,
                infection * 100f),
            BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Filth.Cleanliness",
                cleanlinessPenalty,
                target.RequiredCleaningWork),
            BuildingSummaryUiTextQuery.Get(
                target.IsPriorityCleaning
                    ? "BuildingSummary.Filth.CleaningPriority"
                    : "BuildingSummary.Filth.CleaningAutomatic")
        };
        if (!string.IsNullOrWhiteSpace(source))
        {
            characterDisplayQuery.TryGetDisplayName(source, out string sourceName);
            lines.Add(BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Filth.Source",
                string.IsNullOrWhiteSpace(sourceName)
                    ? BuildingSummaryUiTextQuery.Get("BuildingSummary.Common.Unknown")
                    : sourceName));
        }

        return lines;
    }

    private static string FormatFilthType(WorldFilthType type)
    {
        return type switch
        {
            WorldFilthType.Waste => BuildingSummaryUiTextQuery.Get("BuildingSummary.Filth.Type.Waste"),
            WorldFilthType.Blood => BuildingSummaryUiTextQuery.Get("BuildingSummary.Filth.Type.Blood"),
            WorldFilthType.Rot => BuildingSummaryUiTextQuery.Get("BuildingSummary.Filth.Type.Rot"),
            WorldFilthType.Stain => BuildingSummaryUiTextQuery.Get("BuildingSummary.Filth.Type.Stain"),
            _ => BuildingSummaryUiTextQuery.Get("BuildingSummary.Filth.Type.Unknown")
        };
    }

    private static string FormatEquipmentCraftingText(BuildableObject building)
    {
        BuildingEquipmentCraftingAbility crafting = building?.BuildingData
            ?.GetAbility<BuildingEquipmentCraftingAbility>();
        if (crafting == null)
        {
            return string.Empty;
        }

        if (!building.TryGetCombatEquipmentRuntime(
                out IBuildingEquipmentCraftingRuntimePort runtimePort)
            || runtimePort is not ICombatEquipmentRuntime runtime)
        {
            return BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Crafting.RuntimeUnavailable");
        }

        HashSet<string> craftableIds = new HashSet<string>(
            crafting.CraftableEquipmentIds
                .Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        string queue = string.Join(", ", runtime.CraftQueue
            .Where(order => order != null
                && craftableIds.Contains(order.definitionId))
            .Select(order =>
            {
                string name = runtime.TryGetDefinition(order.definitionId, out CombatEquipmentDefinitionSO definition)
                    ? definition.DisplayName
                    : order.definitionId;
                string materialState = order.materialsReady
                    ? string.Empty
                    : BuildingSummaryUiTextQuery.Get(
                        "BuildingSummary.Crafting.MaterialsMoving");
                return BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Crafting.Order",
                    name,
                    order.RemainingWork,
                    materialState);
            }));
        string craftable = string.Join(", ", runtime.Definitions
            .Where(definition => definition != null
                && craftableIds.Contains(definition.EquipmentId))
            .Select(definition => definition.DisplayName));
        return string.IsNullOrWhiteSpace(queue)
            ? BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Crafting.AvailableNoQueue",
                craftable)
            : BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Crafting.Queue",
                queue);
    }

    private static string FormatStockText(BuildableObject building)
    {
        if (building is IRestockableFacility restockable)
        {
            int maximum = building.GetInternalStockCapacity();
            string amount = maximum > 0
                ? $"{restockable.CurrentStock}/{maximum}"
                : restockable.CurrentStock.ToString();
            return restockable.NeedsRestock
                ? BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Stock.RestockNeeded",
                    amount)
                : BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Stock.Amount",
                    amount);
        }

        if (building is IWarehouseFacility warehouse && warehouse.HasWarehouseInventory)
        {
            if (warehouse.Inventory.HasMassCapacityAuthority)
            {
                return BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Warehouse.Capacity",
                    WarehouseMassUiFormatter.FormatKilograms(
                        warehouse.Inventory.StoredMassGrams),
                    WarehouseMassUiFormatter.FormatKilograms(
                        warehouse.Inventory.MaxMassGrams));
            }

            return warehouse.Inventory.HasCapacityLimit
                ? BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Warehouse.Capacity",
                    warehouse.Inventory.TotalStock,
                    warehouse.Inventory.MaxCapacity)
                : BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Warehouse.Amount",
                    warehouse.Inventory.TotalStock);
        }

        if (building is IStockedFacility stocked)
        {
            int maximum = building.GetInternalStockCapacity();
            return maximum > 0
                ? BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Stock.Capacity",
                    stocked.CurrentStock,
                    maximum)
                : BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Stock.Amount",
                    stocked.CurrentStock);
        }

        return string.Empty;
    }

    private IReadOnlyList<string> BuildConstructionDetailLines(ConstructionSite site)
    {
        List<string> lines = new List<string>
        {
            BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Construction.Target",
                site.TargetBuilding?.objectName ?? site.name),
            BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Construction.Location",
                site.centerPos.x,
                site.centerPos.y)
        };

        ConstructionSafetyResult safety = site.GetConstructionSafetyState(null, forced: false);
        lines.Add(BuildingSummaryUiTextQuery.Get(
            "BuildingSummary.Construction.Safety",
            safety.Message));

        if (!workOrderSummaryQuery.TryGetOrder(
                site,
                BuiltInWorkTypeIds.Construct,
                out BuildingWorkOrderSummarySnapshot order))
        {
            lines.Add(BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Construction.NoOrder"));
            return lines;
        }

        lines.Add(BuildingSummaryUiTextQuery.Get(
            "BuildingSummary.Construction.Status",
            FormatWorkOrderStatus(order.Status)));
        lines.Add(BuildingSummaryUiTextQuery.Get(
            "BuildingSummary.Construction.Progress",
            order.CompletedWork,
            order.RequiredWork,
            Mathf.RoundToInt(order.ProgressRatio * 100f)));
        IReadOnlyDictionary<StockCategory, int> legacyCategoryMaterials =
            ReadOnlyView.Dictionary(new Dictionary<StockCategory, int>());
        bool hasCategoryMaterials = false;
        bool hasItemMaterials =
            order.ItemMaterialRequirements != null
            && order.ItemMaterialRequirements.Count > 0;
        if (hasCategoryMaterials)
        {
            foreach (KeyValuePair<StockCategory, int> pair in legacyCategoryMaterials)
            {
                int delivered = legacyCategoryMaterials.TryGetValue(pair.Key, out int value)
                        ? value
                        : 0;
                lines.Add(BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Construction.Material",
                    stockCategoryCatalog.GetDisplayName(pair.Key),
                    delivered,
                    pair.Value));
            }
        }

        if (hasItemMaterials)
        {
            foreach (KeyValuePair<string, int> pair
                     in order.ItemMaterialRequirements.OrderBy(
                         pair => pair.Key,
                         StringComparer.Ordinal))
            {
                int delivered = order.DeliveredItemMaterials != null
                    && order.DeliveredItemMaterials.TryGetValue(
                        pair.Key,
                        out int value)
                        ? value
                        : 0;
                string label = pair.Key;
                if (FacilityInstallationKitItemIds.TryGetBuildingId(
                        pair.Key,
                        out int buildingId))
                {
                    BuildingSO kitBuilding =
                        buildingDefinitionLookup.GetBuilding(buildingId);
                    string buildingName = kitBuilding?.objectName
                        ?? BuildingSummaryUiTextQuery.Get(
                            "BuildingSummary.Construction.UnnamedFacility",
                            buildingId);
                    label = BuildingSummaryUiTextQuery.Get(
                        "BuildingSummary.Construction.InstallationKit",
                        buildingName);
                }

                lines.Add(BuildingSummaryUiTextQuery.Get(
                    "BuildingSummary.Construction.Material",
                    label,
                    delivered,
                    pair.Value));
            }
        }

        if (!hasCategoryMaterials && !hasItemMaterials)
        {
            lines.Add(BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Construction.NoMaterials"));
        }

        if (!string.IsNullOrWhiteSpace(order.ReservedWorkerPersistentId))
        {
            lines.Add(BuildingSummaryUiTextQuery.Get(
                "BuildingSummary.Construction.ReservedWorker",
                order.ReservedWorkerPersistentId));
        }

        return lines;
    }

    private static string FormatWorkOrderStatus(BuildingWorkOrderSummaryStatus status)
    {
        return status switch
        {
            BuildingWorkOrderSummaryStatus.WaitingForMaterials => BuildingSummaryUiTextQuery.Get("BuildingSummary.WorkOrder.WaitingForMaterials"),
            BuildingWorkOrderSummaryStatus.Ready => BuildingSummaryUiTextQuery.Get("BuildingSummary.WorkOrder.Ready"),
            BuildingWorkOrderSummaryStatus.InProgress => BuildingSummaryUiTextQuery.Get("BuildingSummary.WorkOrder.InProgress"),
            BuildingWorkOrderSummaryStatus.Blocked => BuildingSummaryUiTextQuery.Get("BuildingSummary.WorkOrder.Blocked"),
            BuildingWorkOrderSummaryStatus.Completed => BuildingSummaryUiTextQuery.Get("BuildingSummary.WorkOrder.Completed"),
            BuildingWorkOrderSummaryStatus.Cancelled => BuildingSummaryUiTextQuery.Get("BuildingSummary.WorkOrder.Cancelled"),
            _ => BuildingSummaryUiTextQuery.Get("BuildingSummary.WorkOrder.Unknown")
        };
    }

    private string FormatCategory(BuildingCategory category)
    {
        return buildingCategoryCatalog.GetDisplayName(category);
    }

    private static string FormatRoles(FacilityRole roles)
    {
        if (roles == FacilityRole.None)
        {
            return BuildingSummaryUiTextQuery.Get("BuildingSummary.Common.None");
        }

        return string.Join(", ", FacilityRoleCatalog
            .Enumerate(roles)
            .Select(definition => definition.RoomLabel));
    }

    private static string FormatWorkTypes(IEnumerable<WorkTypeId> workTypeIds)
    {
        string label = CodexDomainTextFormatter.FormatWorkTypes(workTypeIds);
        if (string.IsNullOrWhiteSpace(label))
        {
            return BuildingSummaryUiTextQuery.Get("BuildingSummary.Common.None");
        }

        return label;
    }
}
