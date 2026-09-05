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

public enum OffenseUrgentMitigationCommitPhase
{
    None = 0,
    MaterialsCommitted = 1,
    OutcomePublished = 2
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
    public long inputBufferCapacityGrams;
    public long inputMassAuthorityRevision;
    public string inputCapacityFingerprint = string.Empty;
    public float requiredWork;
    public float completedWork;
    public OffenseUrgentMitigationOrderStatus status;
    public string statusText;
    public int physicalCommitPhase;
    public string physicalOperationId = string.Empty;
    public string physicalCommitId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public bool physicalReceiptAcknowledged;
    public float mitigationBefore;
    public float mitigationAfter;
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
}

internal sealed class OffenseUrgentMitigationRestoreCandidate
{
    internal OffenseUrgentMitigationRestoreCandidate(
        List<OffenseUrgentMitigationOrderStateData> orders,
        Dictionary<string, BuildableObject> boundFacilities,
        int nextOrderSequence,
        float nextEvaluationTime,
        int version)
    {
        Orders = orders;
        BoundFacilities = boundFacilities;
        NextOrderSequence = nextOrderSequence;
        NextEvaluationTime = nextEvaluationTime;
        Version = version;
    }

    internal List<OffenseUrgentMitigationOrderStateData> Orders { get; }
    internal Dictionary<string, BuildableObject> BoundFacilities { get; }
    internal int NextOrderSequence { get; }
    internal float NextEvaluationTime { get; }
    internal int Version { get; }
}

public sealed class OffenseUrgentMitigationRuntime :
    IOffenseUrgentMitigationRuntime,
    IInitializable,
    ITickable
{
    private const float EvaluationInterval = 0.5f;
    private const string PhysicalOperationPrefix =
        "offense-urgent-mitigation:";

    private readonly IOffenseWorldSimulation world;
    private readonly IOffenseContentCatalog content;
    private readonly IBuildingWorldQuery buildings;
    private readonly IProductionItemGateway items;
    private readonly IOffenseUrgentMitigationInputOwnerRuntime inputOwners;
    private readonly IGameClock gameClock;
    private readonly IWorkforceReplanService workforce;
    private readonly IFacilityCandidateCache facilityCandidates;
    private List<OffenseUrgentMitigationOrderStateData> orders =
        new List<OffenseUrgentMitigationOrderStateData>();
    private Dictionary<string, BuildableObject> boundFacilities =
        new Dictionary<string, BuildableObject>(StringComparer.Ordinal);
    private IReadOnlyList<OffenseUrgentMitigationOrderStateData> ordersView;
    private float nextEvaluationTime;
    private int nextOrderSequence;

    public OffenseUrgentMitigationRuntime(
        IOffenseWorldSimulation world,
        IOffenseContentCatalog content,
        IBuildingWorldQuery buildings,
        IProductionItemGateway items,
        IOffenseUrgentMitigationInputOwnerRuntime inputOwners,
        IGameClock gameClock,
        IWorkforceReplanService workforce,
        IFacilityCandidateCache facilityCandidates)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
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
            $"threat-mitigation:{normalizedSiteId}:{nextOrderSequence}";
        OffenseUrgentMitigationOrderStateData order =
            new OffenseUrgentMitigationOrderStateData
            {
                orderId = orderId,
                siteId = normalizedSiteId,
                definitionId = definition.urgentSiteId,
                facilityPersistentId = GetFacilityPersistentId(facility),
                facilityX = facility.centerPos.x,
                facilityY = facility.centerPos.y,
                destinationId = OffenseUrgentMitigationInputOwnerAuthority
                    .BuildDestinationId(orderId),
                requiredWork = Mathf.Max(0.01f, definition.mitigationWork),
                status = OffenseUrgentMitigationOrderStatus.WaitingForMaterials,
                statusText = "완화 재료를 시설로 운반하는 중입니다."
            };
        if (!inputOwners.TryEnsure(order, facility, out string ownerFailure))
        {
            message = "완화 재료 시설 소유권을 게시하지 못했습니다: "
                + ownerFailure;
            return false;
        }
        nextOrderSequence++;
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
        if ((OffenseUrgentMitigationCommitPhase)order.physicalCommitPhase
            != OffenseUrgentMitigationCommitPhase.None)
        {
            message = "재료가 이미 완화 작업에 귀속되어 취소할 수 없습니다.";
            return false;
        }
        if (!TryRemoveOrder(
                order,
                OffenseUrgentMitigationInputOwnerAuthority
                    .CancelledReleaseReasonCode,
                out string closeFailure))
        {
            message = "완화 작업 재료를 보존하지 못해 취소하지 않았습니다: "
                + closeFailure;
            return false;
        }
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

        if ((OffenseUrgentMitigationCommitPhase)order.physicalCommitPhase
            != OffenseUrgentMitigationCommitPhase.None)
        {
            work = Unavailable(order, "완화 결과를 원자적으로 확정하는 중입니다.");
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

        if (!TryFinalizeCompletedOrder(
                order,
                definition,
                out string failureReason))
        {
            SetStatus(
                order,
                OffenseUrgentMitigationOrderStatus.InProgress,
                "완화 결과 확정 대기: " + failureReason);
            Touch();
            return false;
        }

        completed = true;
        return true;
    }

    public IReadOnlyList<OffenseUrgentMitigationOrderStateData> Capture()
    {
        if (!inputOwners.TryValidateForCapture(
                orders,
                buildings.Buildings,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Urgent mitigation input ownership is not capture-safe: "
                + failureReason);
        }
        return orders
            .Where(order => order != null)
            .Select(Clone)
            .ToArray();
    }

    internal OffenseUrgentMitigationRestoreCandidate PrepareRestore(
        IReadOnlyList<OffenseUrgentMitigationOrderStateData> restoredOrders)
    {
        if (restoredOrders == null)
        {
            throw new ArgumentNullException(nameof(restoredOrders));
        }

        List<OffenseUrgentMitigationOrderStateData> candidate = new();
        HashSet<string> orderIds = new(StringComparer.Ordinal);
        HashSet<string> siteIds = new(StringComparer.Ordinal);
        int candidateNextSequence = 0;
        foreach (OffenseUrgentMitigationOrderStateData restored in
                 restoredOrders)
        {
            if (restored == null
                || string.IsNullOrWhiteSpace(restored.orderId)
                || string.IsNullOrWhiteSpace(restored.siteId)
                || string.IsNullOrWhiteSpace(restored.destinationId)
                || !string.Equals(
                    restored.destinationId,
                    OffenseUrgentMitigationInputOwnerAuthority
                        .BuildDestinationId(restored.orderId),
                    StringComparison.Ordinal)
                || !orderIds.Add(restored.orderId)
                || !siteIds.Add(restored.siteId)
                || !Enum.IsDefined(
                    typeof(OffenseUrgentMitigationOrderStatus),
                    restored.status)
                || restored.requiredWork <= 0f
                || restored.completedWork < 0f
                || restored.completedWork > restored.requiredWork
                || !IsFinite(restored.requiredWork)
                || !IsFinite(restored.completedWork)
                || FindDefinition(restored.definitionId) == null)
            {
                throw new InvalidOperationException(
                    $"Invalid or duplicate offense mitigation order '{restored?.orderId ?? "null"}'.");
            }

            OffenseUrgentMitigationOrderStateData order = Clone(restored);
            ValidatePhysicalCommitState(
                order,
                FindDefinition(order.definitionId));
            candidate.Add(order);
            int separator = order.orderId.LastIndexOf(':');
            if (separator >= 0
                && int.TryParse(order.orderId.Substring(separator + 1),
                    out int sequence)
                && sequence >= 0)
            {
                candidateNextSequence = Math.Max(
                    candidateNextSequence,
                    sequence + 1);
            }
        }

        if (!inputOwners.TryReplaceForRestore(
                candidate,
                out string ownerFailure))
        {
            throw new InvalidOperationException(
                "Urgent mitigation input owner restore join failed: "
                + ownerFailure);
        }

        int candidateVersion;
        unchecked
        {
            candidateVersion = Version + 1;
        }
        return new OffenseUrgentMitigationRestoreCandidate(
            candidate,
            new Dictionary<string, BuildableObject>(StringComparer.Ordinal),
            candidateNextSequence,
            gameClock.Time,
            candidateVersion);
    }

    internal void PublishRestore(
        OffenseUrgentMitigationRestoreCandidate candidate)
    {
        candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        orders = candidate.Orders;
        boundFacilities = candidate.BoundFacilities;
        ordersView = null;
        nextOrderSequence = candidate.NextOrderSequence;
        nextEvaluationTime = candidate.NextEvaluationTime;
        Version = candidate.Version;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private void ValidatePhysicalCommitState(
        OffenseUrgentMitigationOrderStateData order,
        OffenseUrgentSiteDefinitionSO definition)
    {
        OffenseUrgentMitigationCommitPhase phase =
            (OffenseUrgentMitigationCommitPhase)order.physicalCommitPhase;
        if (!Enum.IsDefined(typeof(OffenseUrgentMitigationCommitPhase), phase))
        {
            throw new InvalidOperationException(
                $"Mitigation order '{order.orderId}' has an unknown physical commit phase.");
        }
        if (phase == OffenseUrgentMitigationCommitPhase.None)
        {
            if (!string.IsNullOrEmpty(order.physicalOperationId)
                || !string.IsNullOrEmpty(order.physicalCommitId)
                || order.inputQuantity != 0
                || order.inputMassGrams != 0L
                || order.physicalReceiptAcknowledged
                || order.mitigationBefore != 0f
                || order.mitigationAfter != 0f)
            {
                throw new InvalidOperationException(
                    $"Mitigation order '{order.orderId}' has orphan physical provenance.");
            }
            return;
        }

        int expectedQuantity = BuildCost(definition).Values.Sum();
        string expectedOperation = FormatPhysicalOperationId(order.orderId);
        string expectedCommit =
            $"physical-batch-disposition:{(int)PhysicalItemDispositionKind.Transfer}:{expectedOperation}:{order.inputQuantity}:{order.inputMassGrams}";
        if (expectedQuantity <= 0
            || !string.Equals(
                order.physicalOperationId,
                expectedOperation,
                StringComparison.Ordinal)
            || !string.Equals(
                order.physicalCommitId,
                expectedCommit,
                StringComparison.Ordinal)
            || order.inputQuantity != expectedQuantity
            || order.inputMassGrams <= 0L
            || !IsFinite(order.mitigationBefore)
            || !IsFinite(order.mitigationAfter)
            || order.mitigationBefore < 0f
            || order.mitigationAfter <= order.mitigationBefore
            || order.mitigationAfter > 0.6001f
            || order.completedWork + 0.001f < order.requiredWork
            || (phase == OffenseUrgentMitigationCommitPhase.MaterialsCommitted
                && order.physicalReceiptAcknowledged))
        {
            throw new InvalidOperationException(
                $"Mitigation order '{order.orderId}' has invalid physical commit provenance.");
        }
    }

    private bool TryFinalizeCompletedOrder(
        OffenseUrgentMitigationOrderStateData order,
        OffenseUrgentSiteDefinitionSO definition,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null || definition == null)
        {
            failureReason = "완화 작업 권위가 없습니다.";
            return false;
        }

        OffenseUrgentMitigationCommitPhase phase =
            (OffenseUrgentMitigationCommitPhase)order.physicalCommitPhase;
        Dictionary<string, int> cost = BuildCost(definition);
        if (cost.Count == 0)
        {
            if (phase != OffenseUrgentMitigationCommitPhase.None
                || !world.TryMitigateUrgentSite(
                    order.siteId,
                    definition.maximumMitigation))
            {
                failureReason = "무재료 완화 결과를 게시하지 못했습니다.";
                return false;
            }
            return TryRemoveOrder(
                order,
                OffenseUrgentMitigationInputOwnerAuthority
                    .CompletedReleaseReasonCode,
                out failureReason);
        }

        if (phase == OffenseUrgentMitigationCommitPhase.None)
        {
            if (!world.TryGetUrgentSite(
                    order.siteId,
                    out OffenseUrgentSiteStateData site)
                || site == null
                || !site.IsActive)
            {
                failureReason = "완화 대상 거점이 더 이상 활성 상태가 아닙니다.";
                return false;
            }

            float maximum = Mathf.Clamp(
                definition.maximumMitigation,
                0f,
                0.6f);
            if (site.mitigation + 0.001f >= maximum)
            {
                failureReason = "완화 대상이 이미 authored 상한에 도달했습니다.";
                return false;
            }

            string operationId = FormatPhysicalOperationId(order.orderId);
            if (!items.ConsumeDeliveredToWip(
                    order.destinationId,
                    cost,
                    operationId,
                    out ProductionWipInputReceipt receipt,
                    out failureReason)
                || !receipt.IsCommitted)
            {
                failureReason = string.IsNullOrWhiteSpace(failureReason)
                    ? "완화 재료 Transfer receipt를 만들지 못했습니다."
                    : failureReason;
                return false;
            }

            order.physicalCommitPhase =
                (int)OffenseUrgentMitigationCommitPhase.MaterialsCommitted;
            order.physicalOperationId = operationId;
            order.physicalCommitId = receipt.CommitId;
            order.inputQuantity = receipt.Quantity;
            order.inputMassGrams = receipt.InputMassGrams;
            order.physicalReceiptAcknowledged = false;
            order.mitigationBefore = site.mitigation;
            order.mitigationAfter = Mathf.Clamp(
                site.mitigation + Mathf.Max(0f, definition.maximumMitigation),
                0f,
                maximum);
            phase = OffenseUrgentMitigationCommitPhase.MaterialsCommitted;
            Touch();
        }

        if (phase == OffenseUrgentMitigationCommitPhase.MaterialsCommitted)
        {
            if (!TryPublishMitigationOutcome(order, out failureReason))
            {
                return false;
            }
            order.physicalCommitPhase =
                (int)OffenseUrgentMitigationCommitPhase.OutcomePublished;
            phase = OffenseUrgentMitigationCommitPhase.OutcomePublished;
            Touch();
        }

        if (!order.physicalReceiptAcknowledged)
        {
            if (!items.AcknowledgeWipInput(
                    order.physicalCommitId,
                    out failureReason))
            {
                return false;
            }
            order.physicalReceiptAcknowledged = true;
            Touch();
        }

        if (phase != OffenseUrgentMitigationCommitPhase.OutcomePublished)
        {
            failureReason = "완화 작업 commit phase가 비정상입니다.";
            return false;
        }

        // Preserve any unexpected residual delivery as physical Loose stock.
        // Completion must never use count-only RemoveDestination deletion.
        return TryRemoveOrder(
            order,
            OffenseUrgentMitigationInputOwnerAuthority
                .CompletedReleaseReasonCode,
            out failureReason);
    }

    private bool TryPublishMitigationOutcome(
        OffenseUrgentMitigationOrderStateData order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!world.TryGetUrgentSite(
                order.siteId,
                out OffenseUrgentSiteStateData site)
            || site == null
            || !site.IsActive)
        {
            failureReason = "완화 결과 대상 거점이 사라졌습니다.";
            return false;
        }

        if (Approximately(site.mitigation, order.mitigationAfter))
        {
            return true;
        }
        if (!Approximately(site.mitigation, order.mitigationBefore))
        {
            failureReason =
                $"완화 결과 기준값 충돌: current={site.mitigation:R}, before={order.mitigationBefore:R}, after={order.mitigationAfter:R}";
            return false;
        }

        float delta = order.mitigationAfter - order.mitigationBefore;
        if (delta <= 0f
            || !world.TryMitigateUrgentSite(order.siteId, delta)
            || !world.TryGetUrgentSite(order.siteId, out site)
            || site == null
            || !Approximately(site.mitigation, order.mitigationAfter))
        {
            failureReason = "완화 결과 게시 뒤 exact 상태를 확인하지 못했습니다.";
            return false;
        }
        return true;
    }

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;

    public static string FormatPhysicalOperationId(string orderId) =>
        PhysicalOperationPrefix + (orderId ?? string.Empty);

    private void Evaluate(OffenseUrgentMitigationOrderStateData order)
    {
        if (order == null)
        {
            return;
        }

        OffenseUrgentMitigationCommitPhase phase =
            (OffenseUrgentMitigationCommitPhase)order.physicalCommitPhase;
        if (phase != OffenseUrgentMitigationCommitPhase.None)
        {
            OffenseUrgentSiteDefinitionSO pendingDefinition =
                FindDefinition(order.definitionId);
            string pendingFailure = string.Empty;
            if (pendingDefinition == null
                || !TryFinalizeCompletedOrder(
                    order,
                    pendingDefinition,
                    out pendingFailure))
            {
                SetStatus(
                    order,
                    OffenseUrgentMitigationOrderStatus.InProgress,
                    "완화 결과 복구 대기: "
                    + (pendingDefinition == null
                        ? "완화 설정 누락"
                        : pendingFailure));
            }
            return;
        }

        if (!world.TryGetUrgentSite(
                order.siteId,
                out OffenseUrgentSiteStateData site)
            || site == null
            || !site.IsActive)
        {
            if (!TryRemoveOrder(
                    order,
                    OffenseUrgentMitigationInputOwnerAuthority
                        .CancelledReleaseReasonCode,
                    out string cancelFailure))
            {
                SetStatus(
                    order,
                    OffenseUrgentMitigationOrderStatus.WaitingForFacility,
                    "종료 재료 보존 대기: " + cancelFailure);
            }
            return;
        }

        OffenseUrgentSiteDefinitionSO definition =
            FindDefinition(order.definitionId);
        if (definition == null
            || site.mitigation + 0.001f >= definition.maximumMitigation)
        {
            if (!TryRemoveOrder(
                    order,
                    OffenseUrgentMitigationInputOwnerAuthority
                        .CancelledReleaseReasonCode,
                    out string cancelFailure))
            {
                SetStatus(
                    order,
                    OffenseUrgentMitigationOrderStatus.WaitingForFacility,
                    "종료 재료 보존 대기: " + cancelFailure);
            }
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
            return inputOwners.TryEnsure(order, bound, out _)
                ? bound
                : null;
        }

        BuildableObject facility = FindFacilityByPersistentId(
            order.facilityPersistentId);
        if (facility == null)
        {
            if (OffenseUrgentMitigationInputOwnerAuthority
                    .HasStoredProjection(order)
                && !inputOwners.TryRetire(
                    order,
                    OffenseUrgentMitigationInputOwnerAuthority
                        .FacilityLostReleaseReasonCode,
                    out _))
            {
                return null;
            }
            order.facilityPersistentId = string.Empty;
            OffenseUrgentMitigationInputOwnerAuthority
                .ClearStoredProjection(order);
            UnbindFacility(order);
            facility = FindAvailableFacility(definition);
            if (facility == null)
            {
                Touch(rebuildFacilityIndex: true);
                return null;
            }

            order.facilityPersistentId = GetFacilityPersistentId(facility);
            order.facilityX = facility.centerPos.x;
            order.facilityY = facility.centerPos.y;
        }

        if (!inputOwners.TryEnsure(order, facility, out _))
        {
            return null;
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

    private bool TryRemoveOrder(
        OffenseUrgentMitigationOrderStateData order,
        string releaseReasonCode,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null)
        {
            failureReason = "urgent-mitigation-order-missing";
            return false;
        }
        if (!inputOwners.TryRetire(
                order,
                releaseReasonCode,
                out failureReason))
        {
            return false;
        }

        UnbindFacility(order);
        orders.Remove(order);
        Touch(rebuildFacilityIndex: true);
        return true;
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
        BuildingInstanceId value = facility != null
            ? facility.PersistentInstanceId
            : default;
        return value.IsValid ? value.Value : string.Empty;
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
            inputBufferCapacityGrams = source.inputBufferCapacityGrams,
            inputMassAuthorityRevision = source.inputMassAuthorityRevision,
            inputCapacityFingerprint = source.inputCapacityFingerprint
                ?? string.Empty,
            requiredWork = source.requiredWork,
            completedWork = source.completedWork,
            status = source.status,
            statusText = source.statusText ?? string.Empty,
            physicalCommitPhase = source.physicalCommitPhase,
            physicalOperationId = source.physicalOperationId ?? string.Empty,
            physicalCommitId = source.physicalCommitId ?? string.Empty,
            inputQuantity = source.inputQuantity,
            inputMassGrams = source.inputMassGrams,
            physicalReceiptAcknowledged = source.physicalReceiptAcknowledged,
            mitigationBefore = source.mitigationBefore,
            mitigationAfter = source.mitigationAfter
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
