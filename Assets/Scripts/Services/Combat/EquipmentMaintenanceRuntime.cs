using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public enum CombatEquipmentRepairOrderState
{
    PendingCombatEnd = 0,
    WaitingForDelivery = 1,
    Ready = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}

[Serializable]
public sealed class EquipmentMaintenancePolicyData
{
    public string id = string.Empty;
    public string displayName = string.Empty;
    public bool automaticRepair = true;
    [Range(0f, 1f)] public float sendAtDurability = 0.35f;
    [Range(0f, 1f)] public float returnAtDurability = 0.9f;
    public bool allowUnequipDuringInvasion;
    public bool preferReplacement = true;

    public EquipmentMaintenancePolicyData Clone()
    {
        return (EquipmentMaintenancePolicyData)MemberwiseClone();
    }

    public void Normalize()
    {
        id = id?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        sendAtDurability = Mathf.Clamp01(sendAtDurability);
        returnAtDurability = Mathf.Clamp(returnAtDurability, sendAtDurability, 1f);
    }
}

[Serializable]
public sealed class CombatEquipmentRepairOrder
{
    public string orderId = string.Empty;
    public string equipmentInstanceId = string.Empty;
    public string originalOwnerCharacterId = string.Empty;
    public string facilityDestinationId = string.Empty;
    public int facilityX;
    public int facilityY;
    public int requiredGeneralMaterials;
    public float requiredWork;
    public float completedWork;
    public float targetDurability = 0.9f;
    public string reservedWorkerId = string.Empty;
    public CombatEquipmentRepairOrderState state;
    public bool manuallyRequested;

    public Vector2Int FacilityPosition
    {
        get => new Vector2Int(facilityX, facilityY);
        set
        {
            facilityX = value.x;
            facilityY = value.y;
        }
    }

    public float ProgressRatio => requiredWork <= 0f
        ? 1f
        : Mathf.Clamp01(completedWork / requiredWork);

    public CombatEquipmentRepairOrder Clone()
    {
        return (CombatEquipmentRepairOrder)MemberwiseClone();
    }
}

[Serializable]
public sealed class EquipmentMaintenanceAssignmentSaveData
{
    public string characterId = string.Empty;
    public string policyId = string.Empty;
}

[Serializable]
public sealed class CombatEquipmentMaintenanceSaveData
{
    public List<EquipmentMaintenancePolicyData> policies =
        new List<EquipmentMaintenancePolicyData>();
    public List<EquipmentMaintenanceAssignmentSaveData> assignments =
        new List<EquipmentMaintenanceAssignmentSaveData>();
    public List<CombatEquipmentRepairOrder> orders =
        new List<CombatEquipmentRepairOrder>();
}

public interface ICombatEquipmentMaintenanceRuntime
{
    IReadOnlyList<EquipmentMaintenancePolicyData> Policies { get; }
    IReadOnlyList<CombatEquipmentRepairOrder> Orders { get; }
    EquipmentMaintenancePolicyData GetPolicy(CharacterActor actor);
    string GetAssignedPolicyId(CharacterActor actor);
    bool AssignPolicy(CharacterActor actor, string policyId);
    bool TryCreatePolicy(string displayName, out EquipmentMaintenancePolicyData policy);
    bool TryDuplicatePolicy(
        string sourcePolicyId,
        string displayName,
        out EquipmentMaintenancePolicyData policy);
    bool TryUpdatePolicy(EquipmentMaintenancePolicyData policy);
    bool TryDeletePolicy(string policyId, bool reassignToStandard);
    bool TryRequestManualRepair(string equipmentInstanceId, out string message);
    bool HasRepairWorkFor(BuildableObject building);
    float GetRepairUrgency(BuildableObject building);
    bool TryApplyRepairWork(
        CharacterActor worker,
        BuildableObject building,
        float workAmount,
        out bool completed,
        out string message);
    CombatEquipmentMaintenanceSaveData Capture();
    void Restore(CombatEquipmentMaintenanceSaveData saveData, IList<string> warnings);
}

public static class CombatEquipmentMaintenanceFacilityUtility
{
    public static bool IsMaintenanceFacility(BuildableObject building)
    {
        return building?.BuildingData?.GetAbility<BuildingEquipmentMaintenanceAbility>() != null;
    }

    internal static FacilityWorkType AddFallbackWorkTypes(
        BuildableObject building,
        FacilityWorkType supported)
    {
        return IsMaintenanceFacility(building)
            ? supported | FacilityWorkType.Repair
            : supported;
    }
}

public sealed class EquipmentMaintenancePolicyRuntime :
    ICombatEquipmentMaintenanceRuntime,
    ITickable
{
    public const string StandardPolicyId = "equipment-maintenance:standard";
    public const string PreventivePolicyId = "equipment-maintenance:preventive";
    public const string ManualPolicyId = "equipment-maintenance:manual";
    private const string DestinationPrefix = "equipment-repair:";

    private readonly ICombatEquipmentRuntime equipment;
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IWorldItemStackRuntime items;
    private readonly ICombatEquipmentPickupRuntime equipmentPickup;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IDefenseEngagementRuntime defenseRuntime;
    private readonly IGameClock gameClock;
    private readonly Dictionary<string, EquipmentMaintenancePolicyData> policies =
        new Dictionary<string, EquipmentMaintenancePolicyData>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> assignments =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, CombatEquipmentRepairOrder> orders =
        new Dictionary<string, CombatEquipmentRepairOrder>(StringComparer.Ordinal);
    private float nextScanAt;
    private int policySequence;
    private int orderSequence;

    public EquipmentMaintenancePolicyRuntime(
        ICombatEquipmentRuntime equipment,
        ICombatEquipmentCatalog catalog,
        IWorldItemStackRuntime items,
        ICombatEquipmentPickupRuntime equipmentPickup,
        ICharacterAiWorldRegistry worldRegistry,
        IDefenseEngagementRuntime defenseRuntime,
        IGameClock gameClock)
    {
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.equipmentPickup = equipmentPickup
            ?? throw new ArgumentNullException(nameof(equipmentPickup));
        this.worldRegistry = worldRegistry
            ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.defenseRuntime = defenseRuntime
            ?? throw new ArgumentNullException(nameof(defenseRuntime));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        EnsureDefaults();
    }

    public IReadOnlyList<EquipmentMaintenancePolicyData> Policies =>
        policies.Values
            .OrderBy(item => item.displayName, StringComparer.Ordinal)
            .Select(item => item.Clone())
            .ToArray();
    public IReadOnlyList<CombatEquipmentRepairOrder> Orders =>
        orders.Values
            .Where(item => item.state is not CombatEquipmentRepairOrderState.Completed
                and not CombatEquipmentRepairOrderState.Cancelled)
            .OrderBy(item => item.orderId, StringComparer.Ordinal)
            .Select(item => item.Clone())
            .ToArray();

    public void Tick()
    {
        if (gameClock.Time < nextScanAt)
        {
            return;
        }

        nextScanAt = gameClock.Time + 1f;
        EnsureDefaults();
        RefreshOrders();
        CreateAutomaticOrders();
    }

    public EquipmentMaintenancePolicyData GetPolicy(CharacterActor actor)
    {
        string policyId = GetAssignedPolicyId(actor);
        return policies.TryGetValue(policyId, out EquipmentMaintenancePolicyData policy)
            ? policy.Clone()
            : policies[StandardPolicyId].Clone();
    }

    public string GetAssignedPolicyId(CharacterActor actor)
    {
        string characterId = GetCharacterId(actor);
        return !string.IsNullOrWhiteSpace(characterId)
            && assignments.TryGetValue(characterId, out string policyId)
            && policies.ContainsKey(policyId)
                ? policyId
                : StandardPolicyId;
    }

    public bool AssignPolicy(CharacterActor actor, string policyId)
    {
        string characterId = GetCharacterId(actor);
        if (string.IsNullOrWhiteSpace(characterId) || !policies.ContainsKey(policyId ?? string.Empty))
        {
            return false;
        }

        assignments[characterId] = policyId;
        return true;
    }

    public bool TryCreatePolicy(
        string displayName,
        out EquipmentMaintenancePolicyData policy)
    {
        policy = new EquipmentMaintenancePolicyData
        {
            id = $"equipment-maintenance:custom:{++policySequence}",
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? $"장비 정책 {policySequence}"
                : displayName.Trim(),
            automaticRepair = true,
            sendAtDurability = 0.35f,
            returnAtDurability = 0.9f,
            preferReplacement = true
        };
        policies[policy.id] = policy;
        policy = policy.Clone();
        return true;
    }

    public bool TryDuplicatePolicy(
        string sourcePolicyId,
        string displayName,
        out EquipmentMaintenancePolicyData policy)
    {
        policy = null;
        if (!policies.TryGetValue(
                sourcePolicyId?.Trim() ?? string.Empty,
                out EquipmentMaintenancePolicyData source))
        {
            return false;
        }

        policy = source.Clone();
        policy.id = $"equipment-maintenance:custom:{++policySequence}";
        policy.displayName = string.IsNullOrWhiteSpace(displayName)
            ? $"{source.displayName} 복사본"
            : displayName.Trim();
        policies[policy.id] = policy;
        policy = policy.Clone();
        return true;
    }

    public bool TryUpdatePolicy(EquipmentMaintenancePolicyData source)
    {
        if (source == null
            || string.IsNullOrWhiteSpace(source.id)
            || !policies.ContainsKey(source.id))
        {
            return false;
        }

        EquipmentMaintenancePolicyData normalized = source.Clone();
        normalized.Normalize();
        if (string.IsNullOrWhiteSpace(normalized.displayName))
        {
            return false;
        }

        policies[normalized.id] = normalized;
        return true;
    }

    public bool TryDeletePolicy(string policyId, bool reassignToStandard)
    {
        if (string.IsNullOrWhiteSpace(policyId)
            || policyId is StandardPolicyId or PreventivePolicyId or ManualPolicyId
            || !policies.ContainsKey(policyId))
        {
            return false;
        }

        string[] affected = assignments
            .Where(pair => string.Equals(pair.Value, policyId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray();
        if (affected.Length > 0 && !reassignToStandard)
        {
            return false;
        }

        foreach (string characterId in affected)
        {
            assignments[characterId] = StandardPolicyId;
        }

        return policies.Remove(policyId);
    }

    public bool TryRequestManualRepair(string equipmentInstanceId, out string message)
    {
        return TryCreateOrder(equipmentInstanceId, manuallyRequested: true, out message);
    }

    public bool HasRepairWorkFor(BuildableObject building)
    {
        if (!CombatEquipmentMaintenanceFacilityUtility.IsMaintenanceFacility(building))
        {
            return false;
        }

        RefreshOrders();
        return orders.Values.Any(order =>
            order.state is CombatEquipmentRepairOrderState.Ready
                or CombatEquipmentRepairOrderState.InProgress
            && order.FacilityPosition == building.centerPos);
    }

    public float GetRepairUrgency(BuildableObject building)
    {
        if (building == null)
        {
            return 0f;
        }

        return orders.Values
            .Where(order => order.FacilityPosition == building.centerPos
                && order.state is CombatEquipmentRepairOrderState.Ready
                    or CombatEquipmentRepairOrderState.InProgress)
            .Select(order => equipment.TryGetInstance(
                    order.equipmentInstanceId,
                    out CombatEquipmentInstance instance)
                ? Mathf.Lerp(35f, 95f, 1f - instance.durabilityRatio)
                : 0f)
            .DefaultIfEmpty(0f)
            .Max();
    }

    public bool TryApplyRepairWork(
        CharacterActor worker,
        BuildableObject building,
        float workAmount,
        out bool completed,
        out string message)
    {
        completed = false;
        message = string.Empty;
        CombatEquipmentRepairOrder order = orders.Values
            .Where(candidate => candidate.FacilityPosition == building?.centerPos
                && candidate.state is CombatEquipmentRepairOrderState.Ready
                    or CombatEquipmentRepairOrderState.InProgress)
            .OrderBy(candidate => candidate.orderId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (order == null)
        {
            message = "수리 대기 장비가 없습니다.";
            return false;
        }

        order.state = CombatEquipmentRepairOrderState.InProgress;
        order.reservedWorkerId = GetCharacterId(worker);
        float multiplier = building.BuildingData
            .GetAbility<BuildingEquipmentMaintenanceAbility>()?.workSpeedMultiplier ?? 1f;
        order.completedWork = Mathf.Min(
            order.requiredWork,
            order.completedWork + Mathf.Max(0f, workAmount) * Mathf.Max(0.1f, multiplier));
        if (order.completedWork + 0.001f < order.requiredWork)
        {
            message = $"장비 수리 {Mathf.RoundToInt(order.ProgressRatio * 100f)}%";
            return true;
        }

        completed = CompleteOrder(order, building, out message);
        return completed;
    }

    public CombatEquipmentMaintenanceSaveData Capture()
    {
        return new CombatEquipmentMaintenanceSaveData
        {
            policies = policies.Values.Select(item => item.Clone()).ToList(),
            assignments = assignments.Select(pair =>
                new EquipmentMaintenanceAssignmentSaveData
                {
                    characterId = pair.Key,
                    policyId = pair.Value
                }).ToList(),
            orders = orders.Values
                .Where(item => item.state is not CombatEquipmentRepairOrderState.Completed
                    and not CombatEquipmentRepairOrderState.Cancelled)
                .Select(item => item.Clone())
                .ToList()
        };
    }

    public void Restore(
        CombatEquipmentMaintenanceSaveData saveData,
        IList<string> warnings)
    {
        policies.Clear();
        assignments.Clear();
        orders.Clear();
        EnsureDefaults();
        if (saveData == null)
        {
            return;
        }

        foreach (EquipmentMaintenancePolicyData source in saveData.policies
            ?? new List<EquipmentMaintenancePolicyData>())
        {
            if (source == null || string.IsNullOrWhiteSpace(source.id))
            {
                continue;
            }

            EquipmentMaintenancePolicyData restored = source.Clone();
            restored.Normalize();
            policies[restored.id] = restored;
        }
        EnsureDefaults();

        foreach (EquipmentMaintenanceAssignmentSaveData assignment in saveData.assignments
            ?? new List<EquipmentMaintenanceAssignmentSaveData>())
        {
            if (assignment == null
                || string.IsNullOrWhiteSpace(assignment.characterId)
                || !policies.ContainsKey(assignment.policyId ?? string.Empty))
            {
                warnings?.Add("유효하지 않은 장비 정비 정책 배정을 표준으로 되돌렸습니다.");
                continue;
            }

            assignments[assignment.characterId] = assignment.policyId;
        }

        foreach (CombatEquipmentRepairOrder source in saveData.orders
            ?? new List<CombatEquipmentRepairOrder>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.orderId)
                || !equipment.TryGetInstance(source.equipmentInstanceId, out _)
                || orders.ContainsKey(source.orderId))
            {
                warnings?.Add("대상이 없거나 중복된 장비 수리 주문을 해제했습니다.");
                continue;
            }

            orders[source.orderId] = source.Clone();
        }
    }

    private void CreateAutomaticOrders()
    {
        foreach (CombatEquipmentInstance instance in equipment.Instances)
        {
            if (instance == null
                || orders.Values.Any(order =>
                    string.Equals(
                        order.equipmentInstanceId,
                        instance.instanceId,
                        StringComparison.Ordinal))
                || !catalog.TryGet(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition)
                || definition.Kind is not CombatEquipmentKind.Armor
                    and not CombatEquipmentKind.Shield)
            {
                continue;
            }

            EquipmentMaintenancePolicyData policy = GetPolicyByCharacterId(
                instance.ownerCharacterId);
            if (!policy.automaticRepair
                || instance.durabilityRatio > policy.sendAtDurability)
            {
                continue;
            }

            TryCreateOrder(instance.instanceId, manuallyRequested: false, out _);
        }
    }

    private bool TryCreateOrder(
        string instanceId,
        bool manuallyRequested,
        out string message)
    {
        message = string.Empty;
        if (!equipment.TryGetInstance(instanceId, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is not CombatEquipmentKind.Armor
                and not CombatEquipmentKind.Shield)
        {
            message = "수리 가능한 방어구나 방패가 아닙니다.";
            return false;
        }

        if (orders.Values.Any(order =>
            string.Equals(order.equipmentInstanceId, instanceId, StringComparison.Ordinal)))
        {
            message = "이미 수리 대기 중인 장비입니다.";
            return false;
        }

        BuildableObject facility = FindMaintenanceFacility();
        if (facility == null)
        {
            message = "장비를 수리할 대장작업대가 없습니다.";
            return false;
        }

        EquipmentMaintenancePolicyData policy = GetPolicyByCharacterId(
            instance.ownerCharacterId);
        float lost = 1f - instance.durabilityRatio;
        CombatEquipmentRepairOrder order = new CombatEquipmentRepairOrder
        {
            orderId = $"equipment-repair:{++orderSequence:D6}",
            equipmentInstanceId = instance.instanceId,
            originalOwnerCharacterId = instance.ownerCharacterId,
            FacilityPosition = facility.centerPos,
            facilityDestinationId = $"{DestinationPrefix}{instance.instanceId}",
            requiredGeneralMaterials = Mathf.Max(1, Mathf.CeilToInt(lost / 0.25f)),
            requiredWork = 12f + lost * 28f,
            completedWork = 0f,
            targetDurability = policy.returnAtDurability,
            state = IsDefenseActive() && !policy.allowUnequipDuringInvasion
                ? CombatEquipmentRepairOrderState.PendingCombatEnd
                : CombatEquipmentRepairOrderState.WaitingForDelivery,
            manuallyRequested = manuallyRequested
        };
        orders[order.orderId] = order;
        if (order.state == CombatEquipmentRepairOrderState.WaitingForDelivery)
        {
            PrepareDelivery(order);
        }

        message = order.state == CombatEquipmentRepairOrderState.PendingCombatEnd
            ? "침공 종료 후 장비 수리를 시작합니다."
            : "장비 수리 운반을 요청했습니다.";
        return true;
    }

    private void RefreshOrders()
    {
        foreach (string staleOrderId in orders
            .Where(pair => pair.Value == null
                || pair.Value.state is CombatEquipmentRepairOrderState.Completed
                    or CombatEquipmentRepairOrderState.Cancelled)
            .Select(pair => pair.Key)
            .ToArray())
        {
            orders.Remove(staleOrderId);
        }

        foreach (CombatEquipmentRepairOrder order in orders.Values.ToArray())
        {
            if (!equipment.TryGetInstance(order.equipmentInstanceId, out _))
            {
                order.state = CombatEquipmentRepairOrderState.Cancelled;
                continue;
            }

            if (order.state == CombatEquipmentRepairOrderState.PendingCombatEnd
                && !IsDefenseActive())
            {
                order.state = CombatEquipmentRepairOrderState.WaitingForDelivery;
                PrepareDelivery(order);
            }

            if (order.state == CombatEquipmentRepairOrderState.WaitingForDelivery)
            {
                PrepareDelivery(order);
                if (HasDeliveredEquipment(order) && HasDeliveredMaterials(order))
                {
                    order.state = CombatEquipmentRepairOrderState.Ready;
                }
            }
        }
    }

    private void PrepareDelivery(CombatEquipmentRepairOrder order)
    {
        if (order == null
            || !equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance instance))
        {
            return;
        }

        if (!HasEquipmentEnRoute(order, instance))
        {
            Vector2Int sourcePosition = ResolveEquipmentSourcePosition(instance);
            string previousStackId = instance.sourceStackId;
            if (catalog.TryGet(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition)
                && items.SpawnUniqueItemAt(
                    definition.ItemId,
                    sourcePosition,
                    WorldItemStackState.Loose,
                    order.facilityDestinationId,
                    out string stackId))
            {
                if (equipment.TryDetachForMaintenance(
                        instance.instanceId,
                        out CombatEquipmentInstance detached)
                    && equipment.TryLinkToWorldStack(
                        detached.instanceId,
                        stackId,
                        CombatEquipmentWorldState.Loose))
                {
                    if (!string.IsNullOrWhiteSpace(previousStackId)
                        && !string.Equals(
                            previousStackId,
                            stackId,
                            StringComparison.Ordinal))
                    {
                        items.DeleteStack(previousStackId);
                    }

                    TryRequestReplacement(order, detached.definitionId);
                }
                else
                {
                    items.DeleteStack(stackId);
                }
            }
        }

        int pendingGeneral = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    order.facilityDestinationId,
                    StringComparison.Ordinal)
                && stack.StockCategory == StockCategory.General)
            .Sum(stack => stack.Quantity);
        int missing = Mathf.Max(0, order.requiredGeneralMaterials - pendingGeneral);
        if (missing > 0)
        {
            items.TryRequestFacilityDelivery(
                StockCategory.General,
                missing,
                order.FacilityPosition,
                order.facilityDestinationId,
                out _,
                out _);
        }
    }

    private bool CompleteOrder(
        CombatEquipmentRepairOrder order,
        BuildableObject building,
        out string message)
    {
        message = string.Empty;
        if (!HasDeliveredEquipment(order) || !HasDeliveredMaterials(order))
        {
            order.state = CombatEquipmentRepairOrderState.WaitingForDelivery;
            message = "수리 재료가 부족합니다.";
            return false;
        }

        Dictionary<StockCategory, int> materialCost = new Dictionary<StockCategory, int>
        {
            [StockCategory.General] = order.requiredGeneralMaterials
        };
        if (!items.TryConsumeFacilityBuffer(
                order.facilityDestinationId,
                materialCost,
                out message))
        {
            order.state = CombatEquipmentRepairOrderState.WaitingForDelivery;
            return false;
        }

        WorldItemStackSnapshot equipmentStack = FindDeliveredEquipmentStack(order);
        if (equipmentStack == null
            || !items.DeleteStack(equipmentStack.StackId)
            || !equipment.TryRestoreDurability(
                order.equipmentInstanceId,
                order.targetDurability)
            || !equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance repaired)
            || !catalog.TryGet(
                repaired.definitionId,
                out CombatEquipmentDefinitionSO definition)
            || !items.SpawnUniqueItemAt(
                definition.ItemId,
                building.centerPos,
                WorldItemStackState.Loose,
                string.Empty,
                out string outputStackId))
        {
            message = "수리 완료품을 생성하지 못했습니다.";
            return false;
        }

        equipment.TryLinkToWorldStack(
            repaired.instanceId,
            outputStackId,
            CombatEquipmentWorldState.Loose);
        order.state = CombatEquipmentRepairOrderState.Completed;
        order.completedWork = order.requiredWork;
        message = $"{definition.DisplayName} 수리 완료";
        return true;
    }

    private bool HasEquipmentEnRoute(
        CombatEquipmentRepairOrder order,
        CombatEquipmentInstance instance)
    {
        return !string.IsNullOrWhiteSpace(instance.sourceStackId)
            && items.GetAllStacks().Any(stack =>
                stack != null
                && string.Equals(
                    stack.StackId,
                    instance.sourceStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    order.facilityDestinationId,
                    StringComparison.Ordinal));
    }

    private bool HasDeliveredEquipment(CombatEquipmentRepairOrder order)
    {
        return FindDeliveredEquipmentStack(order) != null;
    }

    private WorldItemStackSnapshot FindDeliveredEquipmentStack(
        CombatEquipmentRepairOrder order)
    {
        return items.GetAllStacks().FirstOrDefault(stack =>
            stack != null
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(
                stack.DestinationId,
                order.facilityDestinationId,
                StringComparison.Ordinal)
            && equipment.TryGetInstanceBySourceStack(
                stack.StackId,
                out CombatEquipmentInstance linked)
            && string.Equals(
                linked.instanceId,
                order.equipmentInstanceId,
                StringComparison.Ordinal));
    }

    private bool HasDeliveredMaterials(CombatEquipmentRepairOrder order)
    {
        return items.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    order.facilityDestinationId,
                    StringComparison.Ordinal)
                && stack.StockCategory == StockCategory.General)
            .Sum(stack => stack.Quantity) >= order.requiredGeneralMaterials;
    }

    private Vector2Int ResolveEquipmentSourcePosition(CombatEquipmentInstance instance)
    {
        CharacterActor owner = FindCharacter(instance.ownerCharacterId);
        if (owner != null)
        {
            return owner.GetNowXY();
        }

        WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.StackId,
                instance.sourceStackId,
                StringComparison.Ordinal));
        return stack?.Position ?? FindMaintenanceFacility()?.centerPos ?? Vector2Int.zero;
    }

    private BuildableObject FindMaintenanceFacility()
    {
        return worldRegistry.Buildings
            .Where(CombatEquipmentMaintenanceFacilityUtility.IsMaintenanceFacility)
            .OrderBy(building => building.IsDamaged ? 1 : 0)
            .ThenBy(building => building.centerPos.y)
            .ThenBy(building => building.centerPos.x)
            .FirstOrDefault();
    }

    private EquipmentMaintenancePolicyData GetPolicyByCharacterId(string characterId)
    {
        string policyId = !string.IsNullOrWhiteSpace(characterId)
            && assignments.TryGetValue(characterId, out string assigned)
                ? assigned
                : StandardPolicyId;
        return policies.TryGetValue(policyId, out EquipmentMaintenancePolicyData policy)
            ? policy
            : policies[StandardPolicyId];
    }

    private void TryRequestReplacement(
        CombatEquipmentRepairOrder order,
        string definitionId)
    {
        if (order == null
            || string.IsNullOrWhiteSpace(order.originalOwnerCharacterId)
            || !GetPolicyByCharacterId(order.originalOwnerCharacterId).preferReplacement)
        {
            return;
        }

        CharacterActor owner = FindCharacter(order.originalOwnerCharacterId);
        if (owner == null
            || owner.IsDead
            || owner.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            return;
        }

        bool hasStoredReplacement = equipment.Instances.Any(candidate =>
            candidate != null
            && !string.Equals(
                candidate.instanceId,
                order.equipmentInstanceId,
                StringComparison.Ordinal)
            && string.Equals(candidate.definitionId, definitionId, StringComparison.Ordinal)
            && candidate.worldState == CombatEquipmentWorldState.Stored
            && !string.IsNullOrWhiteSpace(candidate.sourceStackId)
            && items.GetAllStacks().Any(stack =>
                stack != null
                && string.Equals(
                    stack.StackId,
                    candidate.sourceStackId,
                    StringComparison.Ordinal)
                && stack.State == WorldItemStackState.Stored));
        if (hasStoredReplacement)
        {
            equipmentPickup.TryRequestEquipmentPickup(owner, definitionId, out _);
        }
    }

    private void EnsureDefaults()
    {
        policies.TryAdd(StandardPolicyId, new EquipmentMaintenancePolicyData
        {
            id = StandardPolicyId,
            displayName = "표준",
            automaticRepair = true,
            sendAtDurability = 0.35f,
            returnAtDurability = 0.9f,
            preferReplacement = true
        });
        policies.TryAdd(PreventivePolicyId, new EquipmentMaintenancePolicyData
        {
            id = PreventivePolicyId,
            displayName = "예방 정비",
            automaticRepair = true,
            sendAtDurability = 0.6f,
            returnAtDurability = 1f,
            preferReplacement = true
        });
        policies.TryAdd(ManualPolicyId, new EquipmentMaintenancePolicyData
        {
            id = ManualPolicyId,
            displayName = "수동",
            automaticRepair = false,
            sendAtDurability = 0f,
            returnAtDurability = 1f
        });
    }

    private bool IsDefenseActive()
    {
        return defenseRuntime.ActiveEngagements.Any(item =>
            item != null && item.IsActive) == true;
    }

    private CharacterActor FindCharacter(string characterId)
    {
        return worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                GetCharacterId(actor),
                characterId,
                StringComparison.Ordinal));
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        return actor?.Identity?.PersistentId
            ?? (actor != null ? $"character:{actor.GetInstanceID()}" : string.Empty);
    }
}
