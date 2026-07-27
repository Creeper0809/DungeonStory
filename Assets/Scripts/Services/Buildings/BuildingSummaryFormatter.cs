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
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IWorkOrderRuntime workOrderRuntime;
    private readonly IWorldFilthQuery worldFilthQuery;

    public BuildingSummaryFormatter(
        IBuildingDefinitionLookup buildingDefinitionLookup,
        ICharacterAiWorldRegistry worldRegistry,
        IWorkOrderRuntime workOrderRuntime,
        IWorldFilthQuery worldFilthQuery)
    {
        this.buildingDefinitionLookup = buildingDefinitionLookup
            ?? throw new ArgumentNullException(nameof(buildingDefinitionLookup));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.workOrderRuntime = workOrderRuntime
            ?? throw new ArgumentNullException(nameof(workOrderRuntime));
        this.worldFilthQuery = worldFilthQuery
            ?? throw new ArgumentNullException(nameof(worldFilthQuery));
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
            $"상태  {(building.IsDamaged ? "손상" : "정상")}  ·  시설 Lv.{building.FacilityLevel}",
            $"위치  ({building.centerPos.x}, {building.centerPos.y})  ·  {FormatCategory(building.category)}"
        };

        FacilityData facility = building.Facility;
        if (facility != null)
        {
            string capacity = facility.capacity > 0
                ? $"이용 {building.CurrentUserCount}/{facility.capacity}  ·  예약 {building.ActiveVisitReservationCount}"
                : "방문 이용 없음";
            lines.Add(capacity);
            lines.Add($"용도  {FormatRoles(facility.roles)}");
            lines.Add($"업무  {FormatWorkTypes(facility.SupportedWorkTypeIds)}  ·  필요 직원 {facility.requiredWorkers}");
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
            : "제거됨";
        string source = entries.Select(entry => entry.SourceCharacterId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));

        List<string> lines = new List<string>
        {
            $"종류  {types}",
            $"위치  ({target.centerPos.x}, {target.centerPos.y})  ·  {(entries.Any(entry => entry.WallStain) ? "바닥과 벽" : "바닥")}",
            $"오염량  {amount:0.#}  ·  감염도 {infection * 100f:0.#}%",
            $"청결 영향  -{cleanlinessPenalty:0.#}  ·  청소 작업량 {target.RequiredCleaningWork:0.#}",
            target.IsPriorityCleaning ? "청소 명령  최우선 지정됨" : "청소 명령  자동 우선순위"
        };
        if (!string.IsNullOrWhiteSpace(source))
        {
            CharacterActor sourceActor = worldRegistry.Characters.FirstOrDefault(actor =>
                actor != null
                && string.Equals(actor.Identity?.PersistentId, source, StringComparison.Ordinal));
            string sourceName = sourceActor?.Identity?.DisplayName;
            lines.Add($"원인 인물  {(string.IsNullOrWhiteSpace(sourceName) ? "알 수 없음" : sourceName)}");
        }

        return lines;
    }

    private static string FormatFilthType(WorldFilthType type)
    {
        return type switch
        {
            WorldFilthType.Waste => "배설 오염",
            WorldFilthType.Blood => "핏자국",
            WorldFilthType.Rot => "부패 오염",
            WorldFilthType.Stain => "벽 얼룩",
            _ => "오염"
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

        if (!building.TryGetCombatEquipmentRuntime(out ICombatEquipmentRuntime runtime))
        {
            return "제작  장비 런타임 없음";
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
                string materialState = order.materialsReady ? string.Empty : " / 재료 이동 중";
                return $"{name} 작업량 {order.RemainingWork:0.#}{materialState}";
            }));
        string craftable = string.Join(", ", runtime.Definitions
            .Where(definition => definition != null
                && craftableIds.Contains(definition.EquipmentId))
            .Select(definition => definition.DisplayName));
        return string.IsNullOrWhiteSpace(queue)
            ? $"제작 가능  {craftable}  ·  대기 없음"
            : $"제작 대기  {queue}";
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
                ? $"재고  {amount}  ·  보충 필요"
                : $"재고  {amount}";
        }

        if (building is IWarehouseFacility warehouse && warehouse.HasWarehouseInventory)
        {
            return warehouse.Inventory.HasCapacityLimit
                ? $"창고  {warehouse.Inventory.TotalStock}/{warehouse.Inventory.MaxCapacity}"
                : $"창고  {warehouse.Inventory.TotalStock}";
        }

        if (building is IStockedFacility stocked)
        {
            int maximum = building.GetInternalStockCapacity();
            return maximum > 0 ? $"재고  {stocked.CurrentStock}/{maximum}" : $"재고  {stocked.CurrentStock}";
        }

        return string.Empty;
    }

    private IReadOnlyList<string> BuildConstructionDetailLines(ConstructionSite site)
    {
        List<string> lines = new List<string>
        {
            $"대상  {site.TargetBuilding?.objectName ?? site.name}",
            $"위치  ({site.centerPos.x}, {site.centerPos.y})  ·  공사 현장"
        };

        ConstructionSafetyResult safety = site.GetConstructionSafetyState(null, forced: false);
        lines.Add($"안전  {safety.Message}");

        if (!workOrderRuntime.TryGetOrderFor(site, BuiltInWorkTypeIds.Construct, out WorkOrderProgressState order))
        {
            lines.Add("상태  공사 주문 없음");
            return lines;
        }

        lines.Add($"상태  {FormatWorkOrderStatus(order.Status)}");
        lines.Add($"작업  {order.CompletedWork:0.#}/{order.RequiredWork:0.#}  ·  {Mathf.RoundToInt(order.ProgressRatio * 100f)}%");
        if (order.MaterialRequirements != null && order.MaterialRequirements.Count > 0)
        {
            foreach (KeyValuePair<StockCategory, int> pair in order.MaterialRequirements.OrderBy(pair => (int)pair.Key))
            {
                int delivered = order.DeliveredMaterials != null
                    && order.DeliveredMaterials.TryGetValue(pair.Key, out int value)
                        ? value
                        : 0;
                lines.Add($"재료  {StockCategoryCatalog.GetDisplayName(pair.Key)} {delivered}/{pair.Value}");
            }
        }
        else
        {
            lines.Add("재료  필요 없음");
        }

        if (!string.IsNullOrWhiteSpace(order.ReservedWorkerPersistentId))
        {
            lines.Add($"예약 직원  {order.ReservedWorkerPersistentId}");
        }

        return lines;
    }

    private static string FormatWorkOrderStatus(WorkOrderStatus status)
    {
        return status switch
        {
            WorkOrderStatus.WaitingForMaterials => "재료 대기",
            WorkOrderStatus.Ready => "작업 가능",
            WorkOrderStatus.InProgress => "공사 중",
            WorkOrderStatus.Blocked => "막힘",
            WorkOrderStatus.Completed => "완료",
            WorkOrderStatus.Cancelled => "취소됨",
            _ => "알 수 없음"
        };
    }

    private static string FormatCategory(BuildingCategory category)
    {
        return BuildingCategoryCatalog.GetDisplayName(category);
    }

    private static string FormatRoles(FacilityRole roles)
    {
        if (roles == FacilityRole.None) return "없음";

        return string.Join(", ", FacilityRoleCatalog
            .Enumerate(roles)
            .Select(definition => definition.RoomLabel));
    }

    private static string FormatWorkTypes(IEnumerable<WorkTypeId> workTypeIds)
    {
        string label = CodexTextFormatter.FormatWorkTypes(workTypeIds);
        if (string.IsNullOrWhiteSpace(label)) return "없음";

        return label;
    }
}
