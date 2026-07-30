using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public enum OffenseUrgentMitigationOrderStatus
{
    WaitingForFacility = 0,
    WaitingForMaterials = 1,
    Ready = 2,
    InProgress = 3
}

[Serializable]
public sealed class OffenseUrgentMitigationOrderStateData
{
    public string orderId;
    public string siteId;
    public string definitionId;
    public string facilityPersistentId;
    public int facilityX;
    public int facilityY;
    public string destinationId;
    public float requiredWork;
    public float completedWork;
    public OffenseUrgentMitigationOrderStatus status;
    public string statusText;
}

public readonly struct OffenseUrgentMitigationWorkSnapshot
{
    public OffenseUrgentMitigationWorkSnapshot(
        bool available,
        string orderId,
        string displayName,
        float requiredWork,
        float completedWork,
        string unavailableReason)
    {
        Available = available;
        OrderId = orderId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        RequiredWork = Mathf.Max(0.01f, requiredWork);
        CompletedWork = Mathf.Clamp(completedWork, 0f, RequiredWork);
        UnavailableReason = unavailableReason ?? string.Empty;
    }

    public bool Available { get; }
    public string OrderId { get; }
    public string DisplayName { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public string UnavailableReason { get; }
    public float Progress01 => Mathf.Clamp01(CompletedWork / RequiredWork);
}

public interface IOffenseUrgentMitigationRuntime
{
    event Action Changed;
    int Version { get; }
    IReadOnlyList<OffenseUrgentMitigationOrderStateData> Orders { get; }
    bool TryStart(string siteId, out string message);
    bool TryCancel(string siteId, out string message);
    bool TryGetOrder(
        string siteId,
        out OffenseUrgentMitigationOrderStateData order);
    bool TryGetWork(
        BuildableObject facility,
        CharacterActor worker,
        out OffenseUrgentMitigationWorkSnapshot work);
    bool ApplyWork(
        BuildableObject facility,
        CharacterActor worker,
        float amount,
        out bool completed);
    IReadOnlyList<OffenseUrgentMitigationOrderStateData> Capture();
    void Restore(
        IReadOnlyList<OffenseUrgentMitigationOrderStateData> orders);
}

public sealed class OffenseUrgentMitigationRuntime :
    IOffenseUrgentMitigationRuntime,
    IInitializable,
    ITickable
{
    private const float EvaluationInterval = 0.5f;

    private readonly IOffenseWorldSimulation world;
    private readonly IOffenseV17ContentCatalog content;
    private readonly IBuildingWorldQuery buildings;
    private readonly IProductionItemGateway items;
    private readonly IGameClock gameClock;
    private readonly IWorkforceReplanService workforce;
    private readonly IFacilityCandidateCache facilityCandidates;
    private readonly List<OffenseUrgentMitigationOrderStateData> orders =
        new List<OffenseUrgentMitigationOrderStateData>();
    private readonly Dictionary<string, BuildableObject> boundFacilities =
        new Dictionary<string, BuildableObject>(StringComparer.Ordinal);
    private IReadOnlyList<OffenseUrgentMitigationOrderStateData> ordersView;
    private float nextEvaluationTime;
    private int nextOrderSequence;

    public OffenseUrgentMitigationRuntime(
        IOffenseWorldSimulation world,
        IOffenseV17ContentCatalog content,
        IBuildingWorldQuery buildings,
        IProductionItemGateway items,
        IGameClock gameClock,
        IWorkforceReplanService workforce = null,
        IFacilityCandidateCache facilityCandidates = null)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.workforce = workforce;
        this.facilityCandidates = facilityCandidates;
    }

    public event Action Changed;
    public int Version { get; private set; }
    public IReadOnlyList<OffenseUrgentMitigationOrderStateData> Orders =>
        ordersView ??= ReadOnlyView.List(orders);

    public void Initialize()
    {
        RebindOrders();
    }

    public void Tick()
    {
        if (gameClock.IsPaused || gameClock.Time < nextEvaluationTime)
        {
            return;
        }

        nextEvaluationTime = gameClock.Time + EvaluationInterval;
        foreach (OffenseUrgentMitigationOrderStateData order in orders.ToArray())
        {
            Evaluate(order);
        }
    }

    public bool TryStart(string siteId, out string message)
    {
        string normalizedSiteId = siteId?.Trim() ?? string.Empty;
        if (!world.TryGetUrgentSite(
                normalizedSiteId,
                out OffenseUrgentSiteStateData site)
            || site == null
            || !site.IsActive)
        {
            message = "완화할 활성 긴급 거점이 없습니다.";
            return false;
        }

        OffenseUrgentSiteDefinitionSO definition =
            FindDefinition(site.definitionId);
        if (definition == null)
        {
            message = "긴급 거점 완화 설정을 찾을 수 없습니다.";
            return false;
        }

        if (site.mitigation + 0.001f >= definition.maximumMitigation)
        {
            message = "이 거점의 임시 완화 한도에 도달했습니다.";
            return false;
        }

        if (TryGetOrder(normalizedSiteId, out _))
        {
            message = "이미 이 거점의 완화 작업이 진행 중입니다.";
            return false;
        }

        BuildableObject facility = FindAvailableFacility(definition);
        if (facility == null)
        {
            message = BuildFacilityRequirementMessage(definition);
            return false;
        }

        string orderId =
            $"threat-mitigation:{normalizedSiteId}:{nextOrderSequence++}";
        OffenseUrgentMitigationOrderStateData order =
            new OffenseUrgentMitigationOrderStateData
            {
                orderId = orderId,
                siteId = normalizedSiteId,
                definitionId = definition.urgentSiteId,
                facilityPersistentId = GetFacilityPersistentId(facility),
                facilityX = facility.centerPos.x,
                facilityY = facility.centerPos.y,
                destinationId = orderId,
                requiredWork = Mathf.Max(0.01f, definition.mitigationWork),
                status = OffenseUrgentMitigationOrderStatus.WaitingForMaterials,
                statusText = "완화 재료를 시설로 운반하는 중입니다."
            };
        orders.Add(order);
        BindFacility(order, facility);
        RequestMissingMaterials(order, definition, facility);
        Touch(rebuildFacilityIndex: true);
        message = $"{site.displayName} 완화 작업을 발행했습니다.";
        return true;
    }

    public bool TryCancel(string siteId, out string message)
    {
        if (!TryGetOrder(siteId, out OffenseUrgentMitigationOrderStateData order))
        {
            message = "취소할 완화 작업이 없습니다.";
            return false;
        }

        string displayName =
            FindDefinition(order.definitionId)?.displayName
            ?? order.siteId;
        CancelOrder(order, releaseMaterials: true);
        message = $"{displayName} 완화 작업을 취소했습니다.";
        return true;
    }

    public bool TryGetOrder(
        string siteId,
        out OffenseUrgentMitigationOrderStateData order)
    {
        string normalized = siteId?.Trim() ?? string.Empty;
        order = orders.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.siteId,
                normalized,
                StringComparison.Ordinal));
        return order != null;
    }

    public bool TryGetWork(
        BuildableObject facility,
        CharacterActor worker,
        out OffenseUrgentMitigationWorkSnapshot work)
    {
        work = default;
        OffenseUrgentMitigationOrderStateData order =
            FindOrderForFacility(facility);
        if (order == null)
        {
            return false;
        }

        OffenseUrgentSiteDefinitionSO definition =
            FindDefinition(order.definitionId);
        if (definition == null)
        {
            work = Unavailable(order, "완화 설정이 사라졌습니다.");
            return false;
        }

        if (!HasAllDelivered(order, definition))
        {
            work = Unavailable(
                order,
                BuildMaterialStatus(order, definition));
            return false;
        }

        work = new OffenseUrgentMitigationWorkSnapshot(
            true,
            order.orderId,
            $"{definition.displayName} 완화",
            definition.mitigationWork,
            order.completedWork,
            string.Empty);
        return true;
    }

    public bool ApplyWork(
        BuildableObject facility,
        CharacterActor worker,
        float amount,
        out bool completed)
    {
        completed = false;
        if (amount <= 0f
            || !TryGetWork(
                facility,
                worker,
                out OffenseUrgentMitigationWorkSnapshot work)
            || !work.Available)
        {
            return false;
        }

        OffenseUrgentMitigationOrderStateData order =
            FindOrderForFacility(facility);
        OffenseUrgentSiteDefinitionSO definition =
            FindDefinition(order?.definitionId);
        if (order == null || definition == null)
        {
            return false;
        }

        order.completedWork = Mathf.Min(
            work.RequiredWork,
            order.completedWork + amount);
        SetStatus(
            order,
            OffenseUrgentMitigationOrderStatus.InProgress,
            $"{definition.displayName} 완화 "
            + $"{Mathf.FloorToInt(order.completedWork / work.RequiredWork * 100f)}%");
        if (order.completedWork + 0.001f < work.RequiredWork)
        {
            Touch();
            return true;
        }

        Dictionary<string, int> cost = BuildCost(definition);
        if (cost.Count > 0
            && !items.ConsumeDelivered(
                order.destinationId,
                cost,
                out string failureReason))
        {
            order.completedWork = Mathf.Max(0f, work.RequiredWork - 0.01f);
            SetStatus(
                order,
                OffenseUrgentMitigationOrderStatus.WaitingForMaterials,
                $"완화 재료 확인 실패: {failureReason}");
            Touch();
            return false;
        }

        if (!world.TryMitigateUrgentSite(
                order.siteId,
                definition.maximumMitigation))
        {
            SetStatus(
                order,
                OffenseUrgentMitigationOrderStatus.Ready,
                "긴급 거점 상태가 바뀌어 완화를 적용하지 못했습니다.");
            Touch();
            return false;
        }

        completed = true;
        RemoveOrder(order, releaseMaterials: false);
        return true;
    }

    public IReadOnlyList<OffenseUrgentMitigationOrderStateData> Capture()
    {
        return orders
            .Where(order => order != null)
            .Select(Clone)
            .ToArray();
    }

    public void Restore(
        IReadOnlyList<OffenseUrgentMitigationOrderStateData> restoredOrders)
    {
        foreach (OffenseUrgentMitigationOrderStateData order in orders.ToArray())
        {
            UnbindFacility(order);
        }

        orders.Clear();
        boundFacilities.Clear();
        nextOrderSequence = 0;
        foreach (OffenseUrgentMitigationOrderStateData restored in
                 restoredOrders ?? Array.Empty<OffenseUrgentMitigationOrderStateData>())
        {
            if (restored == null
                || string.IsNullOrWhiteSpace(restored.orderId)
                || string.IsNullOrWhiteSpace(restored.siteId)
                || FindDefinition(restored.definitionId) == null)
            {
                continue;
            }

            OffenseUrgentMitigationOrderStateData order = Clone(restored);
            order.completedWork = Mathf.Max(0f, order.completedWork);
            order.requiredWork = Mathf.Max(
                0.01f,
                order.requiredWork > 0f
                    ? order.requiredWork
                    : FindDefinition(order.definitionId)?.mitigationWork ?? 1f);
            order.destinationId = string.IsNullOrWhiteSpace(order.destinationId)
                ? order.orderId
                : order.destinationId.Trim();
            orders.Add(order);
            nextOrderSequence++;
        }

        RebindOrders();
        Touch(rebuildFacilityIndex: true);
    }

    private void Evaluate(OffenseUrgentMitigationOrderStateData order)
    {
        if (order == null)
        {
            return;
        }

        if (!world.TryGetUrgentSite(
                order.siteId,
                out OffenseUrgentSiteStateData site)
            || site == null
            || !site.IsActive)
        {
            CancelOrder(order, releaseMaterials: true);
            return;
        }

        OffenseUrgentSiteDefinitionSO definition =
            FindDefinition(order.definitionId);
        if (definition == null
            || site.mitigation + 0.001f >= definition.maximumMitigation)
        {
            CancelOrder(order, releaseMaterials: true);
            return;
        }

        BuildableObject facility = EnsureFacility(order, definition);
        if (facility == null)
        {
            SetStatus(
                order,
                OffenseUrgentMitigationOrderStatus.WaitingForFacility,
                BuildFacilityRequirementMessage(definition));
            return;
        }

        RequestMissingMaterials(order, definition, facility);
        if (!HasAllDelivered(order, definition))
        {
            SetStatus(
                order,
                OffenseUrgentMitigationOrderStatus.WaitingForMaterials,
                BuildMaterialStatus(order, definition));
            return;
        }

        if (order.status != OffenseUrgentMitigationOrderStatus.InProgress)
        {
            SetStatus(
                order,
                OffenseUrgentMitigationOrderStatus.Ready,
                "재료 준비 완료 · 담당 직원 배정 대기");
        }
        workforce?.RequestOneWorkerToReplanFor(
            BuiltInWorkTypeIds.ThreatMitigation,
            forceInterrupt: false);
    }

    private BuildableObject EnsureFacility(
        OffenseUrgentMitigationOrderStateData order,
        OffenseUrgentSiteDefinitionSO definition)
    {
        if (boundFacilities.TryGetValue(
                order.orderId,
                out BuildableObject bound)
            && bound != null
            && !bound.isDestroy)
        {
            return bound;
        }

        BuildableObject facility = FindFacilityByPersistentId(
            order.facilityPersistentId);
        if (facility == null)
        {
            items.ReleaseDestination(
                order.destinationId,
                new Vector2Int(order.facilityX, order.facilityY));
            facility = FindAvailableFacility(definition);
            if (facility == null)
            {
                order.facilityPersistentId = string.Empty;
                UnbindFacility(order);
                Touch(rebuildFacilityIndex: true);
                return null;
            }

            order.facilityPersistentId = GetFacilityPersistentId(facility);
            order.facilityX = facility.centerPos.x;
            order.facilityY = facility.centerPos.y;
        }

        BindFacility(order, facility);
        Touch(rebuildFacilityIndex: true);
        return facility;
    }

    private BuildableObject FindAvailableFacility(
        OffenseUrgentSiteDefinitionSO definition)
    {
        if (definition == null
            || string.IsNullOrWhiteSpace(definition.mitigationWorkTypeId))
        {
            return null;
        }

        WorkTypeId semanticWorkType =
            new WorkTypeId(definition.mitigationWorkTypeId);
        HashSet<BuildableObject> occupied =
            new HashSet<BuildableObject>(boundFacilities.Values
                .Where(facility => facility != null && !facility.isDestroy));
        return buildings.Buildings
            .Where(facility =>
                facility != null
                && !facility.isDestroy
                && facility is IWorkableFacility
                && !occupied.Contains(facility)
                && facility.SupportsWork(semanticWorkType))
            .OrderBy(facility => facility.centerPos.y)
            .ThenBy(facility => facility.centerPos.x)
            .ThenBy(facility => GetFacilityPersistentId(facility),
                StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private BuildableObject FindFacilityByPersistentId(string persistentId)
    {
        string normalized = persistentId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return null;
        }

        return buildings.Buildings.FirstOrDefault(facility =>
            facility != null
            && !facility.isDestroy
            && string.Equals(
                GetFacilityPersistentId(facility),
                normalized,
                StringComparison.Ordinal));
    }

    private OffenseUrgentMitigationOrderStateData FindOrderForFacility(
        BuildableObject facility)
    {
        if (facility == null)
        {
            return null;
        }

        string facilityId = GetFacilityPersistentId(facility);
        return orders.FirstOrDefault(order =>
            order != null
            && string.Equals(
                order.facilityPersistentId,
                facilityId,
                StringComparison.Ordinal));
    }

    private void RequestMissingMaterials(
        OffenseUrgentMitigationOrderStateData order,
        OffenseUrgentSiteDefinitionSO definition,
        BuildableObject facility)
    {
        if (order == null || definition == null || facility == null)
        {
            return;
        }

        foreach (KeyValuePair<string, int> cost in BuildCost(definition))
        {
            int pending = items.CountPending(
                cost.Key,
                order.destinationId);
            int missing = Mathf.Max(0, cost.Value - pending);
            if (missing <= 0)
            {
                continue;
            }

            items.RequestDelivery(
                cost.Key,
                missing,
                facility.centerPos,
                order.destinationId,
                out int requested,
                out _);
            if (requested > 0)
            {
                workforce?.RequestOneHaulerToReplan(forceInterrupt: false);
            }
        }

        items.PrioritizeDestination(order.destinationId);
    }

    private bool HasAllDelivered(
        OffenseUrgentMitigationOrderStateData order,
        OffenseUrgentSiteDefinitionSO definition)
    {
        return BuildCost(definition).All(cost =>
            items.CountDelivered(cost.Key, order.destinationId) >= cost.Value);
    }

    private string BuildMaterialStatus(
        OffenseUrgentMitigationOrderStateData order,
        OffenseUrgentSiteDefinitionSO definition)
    {
        KeyValuePair<string, int>? missing = BuildCost(definition)
            .FirstOrDefault(cost =>
                items.CountDelivered(
                    cost.Key,
                    order.destinationId) < cost.Value);
        if (!missing.HasValue || string.IsNullOrWhiteSpace(missing.Value.Key))
        {
            return "완화 재료 준비 완료";
        }

        int delivered = items.CountDelivered(
            missing.Value.Key,
            order.destinationId);
        int pending = items.CountPending(
            missing.Value.Key,
            order.destinationId);
        return $"{missing.Value.Key} 납품 {delivered}/{missing.Value.Value}"
            + $" · 운반 포함 {pending}/{missing.Value.Value}";
    }

    private static Dictionary<string, int> BuildCost(
        OffenseUrgentSiteDefinitionSO definition)
    {
        Dictionary<string, int> cost =
            new Dictionary<string, int>(StringComparer.Ordinal);
        if (definition != null
            && definition.mitigationItemAmount > 0
            && !string.IsNullOrWhiteSpace(definition.mitigationItemId))
        {
            cost.Add(
                definition.mitigationItemId.Trim(),
                definition.mitigationItemAmount);
        }

        return cost;
    }

    private string BuildFacilityRequirementMessage(
        OffenseUrgentSiteDefinitionSO definition)
    {
        string workLabel = definition != null
            && WorkTypeCatalog.TryGet(
                new WorkTypeId(definition.mitigationWorkTypeId),
                out WorkTypeDefinition work)
                    ? work.DisplayName
                    : "대응";
        return $"{workLabel} 작업이 가능한 정상 시설이 필요합니다.";
    }

    private void RebindOrders()
    {
        foreach (OffenseUrgentMitigationOrderStateData order in orders)
        {
            OffenseUrgentSiteDefinitionSO definition =
                FindDefinition(order.definitionId);
            if (definition != null)
            {
                EnsureFacility(order, definition);
            }
        }
    }

    private void BindFacility(
        OffenseUrgentMitigationOrderStateData order,
        BuildableObject facility)
    {
        if (order == null || facility == null)
        {
            return;
        }

        if (boundFacilities.TryGetValue(
                order.orderId,
                out BuildableObject previous)
            && previous == facility)
        {
            return;
        }

        UnbindFacility(order);
        RuntimeWorkCapabilityMarker marker =
            facility.GetComponent<RuntimeWorkCapabilityMarker>();
        if (marker == null)
        {
            marker = facility.gameObject.AddComponent<RuntimeWorkCapabilityMarker>();
        }

        marker.Add(order.orderId, BuiltInWorkTypeIds.ThreatMitigation);
        boundFacilities[order.orderId] = facility;
    }

    private void UnbindFacility(
        OffenseUrgentMitigationOrderStateData order)
    {
        if (order == null
            || !boundFacilities.TryGetValue(
                order.orderId,
                out BuildableObject facility))
        {
            return;
        }

        if (facility != null)
        {
            facility.GetComponent<RuntimeWorkCapabilityMarker>()?
                .RemoveSource(order.orderId);
        }

        boundFacilities.Remove(order.orderId);
    }

    private void CancelOrder(
        OffenseUrgentMitigationOrderStateData order,
        bool releaseMaterials)
    {
        RemoveOrder(order, releaseMaterials);
    }

    private void RemoveOrder(
        OffenseUrgentMitigationOrderStateData order,
        bool releaseMaterials)
    {
        if (order == null)
        {
            return;
        }

        if (releaseMaterials)
        {
            items.ReleaseDestination(
                order.destinationId,
                new Vector2Int(order.facilityX, order.facilityY));
        }
        else
        {
            items.RemoveDestination(order.destinationId);
        }

        UnbindFacility(order);
        orders.Remove(order);
        Touch(rebuildFacilityIndex: true);
    }

    private void SetStatus(
        OffenseUrgentMitigationOrderStateData order,
        OffenseUrgentMitigationOrderStatus status,
        string text)
    {
        string normalized = text ?? string.Empty;
        if (order.status == status
            && string.Equals(
                order.statusText,
                normalized,
                StringComparison.Ordinal))
        {
            return;
        }

        order.status = status;
        order.statusText = normalized;
        Touch();
    }

    private OffenseUrgentSiteDefinitionSO FindDefinition(
        string definitionId)
    {
        return content.UrgentSites.FirstOrDefault(definition =>
            definition != null
            && string.Equals(
                definition.urgentSiteId,
                definitionId?.Trim(),
                StringComparison.Ordinal));
    }

    private static string GetFacilityPersistentId(BuildableObject facility)
    {
        FacilityEvolutionStateComponent state =
            facility != null
                ? facility.GetComponent<FacilityEvolutionStateComponent>()
                : null;
        return state?.FacilityPersistentId ?? string.Empty;
    }

    private OffenseUrgentMitigationWorkSnapshot Unavailable(
        OffenseUrgentMitigationOrderStateData order,
        string reason)
    {
        OffenseUrgentSiteDefinitionSO definition =
            FindDefinition(order?.definitionId);
        return new OffenseUrgentMitigationWorkSnapshot(
            false,
            order?.orderId,
            definition != null
                ? $"{definition.displayName} 완화"
                : "긴급 거점 완화",
            definition?.mitigationWork ?? 1f,
            order?.completedWork ?? 0f,
            reason);
    }

    private void Touch(bool rebuildFacilityIndex = false)
    {
        Version++;
        if (rebuildFacilityIndex)
        {
            facilityCandidates?.Clear();
        }
        else
        {
            facilityCandidates?.MarkDynamicStateDirty();
        }

        Changed?.Invoke();
    }

    private static OffenseUrgentMitigationOrderStateData Clone(
        OffenseUrgentMitigationOrderStateData source)
    {
        return new OffenseUrgentMitigationOrderStateData
        {
            orderId = source.orderId ?? string.Empty,
            siteId = source.siteId ?? string.Empty,
            definitionId = source.definitionId ?? string.Empty,
            facilityPersistentId = source.facilityPersistentId ?? string.Empty,
            facilityX = source.facilityX,
            facilityY = source.facilityY,
            destinationId = source.destinationId ?? string.Empty,
            requiredWork = Mathf.Max(0.01f, source.requiredWork),
            completedWork = source.completedWork,
            status = source.status,
            statusText = source.statusText ?? string.Empty
        };
    }
}

public sealed class ThreatMitigationWorkExecutionHandler :
    IWorkExecutionHandler,
    IWorkCandidateProvider,
    IWorkUrgencyProvider
{
    private static readonly WorkTypeId[] Supported =
    {
        BuiltInWorkTypeIds.ThreatMitigation
    };

    private readonly IOffenseUrgentMitigationRuntime runtime;

    public ThreatMitigationWorkExecutionHandler(
        IOffenseUrgentMitigationRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public IReadOnlyCollection<WorkTypeId> WorkTypeIds => Supported;

    public bool IsAvailable(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target,
        out string reason)
    {
        OffenseUrgentMitigationWorkSnapshot work = default;
        bool available = workTypeId == BuiltInWorkTypeIds.ThreatMitigation
            && runtime.TryGetWork(target, actor, out work)
            && work.Available;
        reason = available ? string.Empty : work.UnavailableReason;
        return available;
    }

    public float GetUrgency(
        WorkTypeId workTypeId,
        CharacterActor actor,
        BuildableObject target)
    {
        return workTypeId == BuiltInWorkTypeIds.ThreatMitigation
            && runtime.TryGetWork(
                target,
                actor,
                out OffenseUrgentMitigationWorkSnapshot work)
            && work.Available
                ? 88f
                : 0f;
    }

    public IEnumerator Execute(
        WorkExecutionContext context,
        WorkExecutionResult result)
    {
        if (!runtime.TryGetWork(
                context.Target,
                context.Actor,
                out OffenseUrgentMitigationWorkSnapshot work)
            || !work.Available)
        {
            result.CompletedSuccessfully = false;
            yield break;
        }

        bool progressApplied = true;
        bool completed = false;
        yield return context.ExecutePersistentWorkAmount(
            work.RequiredWork,
            work.CompletedWork,
            work.DisplayName,
            delta =>
            {
                bool succeeded = runtime.ApplyWork(
                    context.Target,
                    context.Actor,
                    delta,
                    out bool workCompleted);
                progressApplied &= succeeded;
                completed |= workCompleted;
                return succeeded;
            });
        result.CompletedSuccessfully = progressApplied && completed;
        result.CompletionEffectsAlreadyApplied = completed;
    }
}
