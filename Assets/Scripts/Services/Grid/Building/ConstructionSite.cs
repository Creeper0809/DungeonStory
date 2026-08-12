using System;
using System.Collections;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class ConstructionSite : BuildableObject,
    IWorkableFacility,
    IParallelWorkerReservationFacility
{
    private const float WorkerStandOffsetY = 0.15f;
    private const float WorkerStandSpacingX = 0.18f;
    private Func<bool> completeConstruction;
    private Action removeSite;
    private readonly List<IBuildingVisitorPort> workers = new(4);
    private readonly Dictionary<IBuildingVisitorPort, Vector3> workerOffsets = new();
    private readonly Dictionary<IBuildingVisitorPort, int> workerSlots = new();
    private readonly Dictionary<CharacterId, ParallelWorkerReservation> workerReservations = new();
    private readonly List<CharacterId> expiredWorkerReservations = new(4);
    private string workOrderId = string.Empty;
    private ConstructionSafetyResult lastSafetyResult = ConstructionSafetyResult.Safe();
    private ConstructionSafetyResult cachedSafetyResult =
        ConstructionSafetyResult.Safe();
    private int cachedSafetyGridVersion = -1;
    private int cachedSafetyBuildingVersion = -1;
    private Vector2Int cachedSafetyWorkerPosition =
        new Vector2Int(int.MinValue, int.MinValue);
    private bool hasCachedSafetyResult;
    private IWorkOrderRuntime workOrderRuntime;
    private Sprite ownedSiteSprite;
    private Texture2D ownedSiteTexture;

    public string WorkOrderId => workOrderId;
    public BuildingSO TargetBuilding => BuildingData;
    public Vector2Int GridPosition => centerPos;
    public ConstructionSafetyResult LastSafetyResult => lastSafetyResult;
    public IBuildingVisitorPort ActiveWorker => workers.Count > 0 ? workers[0] : null;
    public int ActiveWorkerCount => workers.Count;
    public int OccupiedWorkerSlotCount
    {
        get
        {
            PruneParallelWorkerReservations();
            return workers.Count + workerReservations.Count;
        }
    }
    public int MaximumWorkers => TargetBuilding == null
        ? 0
        : SettlementLaborBalanceRules.GetMaximumWorkers(
            TargetBuilding.GetConstructionProjectScale());
    public IBuildingCharacterPort PrimaryWorkerReservation
    {
        get
        {
            PruneParallelWorkerReservations();
            foreach (ParallelWorkerReservation reservation in workerReservations.Values)
                return reservation.Worker;
            return null;
        }
    }

    private sealed class ParallelWorkerReservation
    {
        public ParallelWorkerReservation(
            IBuildingCharacterPort worker,
            float expiresAt,
            int slotIndex)
        {
            Worker = worker;
            ExpiresAt = expiresAt;
            SlotIndex = slotIndex;
        }

        public IBuildingCharacterPort Worker { get; }
        public float ExpiresAt { get; set; }
        public int SlotIndex { get; }
    }

    public void ConfigureWorkOrderRuntime(IWorkOrderRuntime runtime)
    {
        workOrderRuntime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public void ConfigureSite(
        string orderId,
        Func<bool> onCompleteConstruction,
        Action onRemoveSite)
    {
        workOrderId = orderId ?? string.Empty;
        completeConstruction = onCompleteConstruction;
        removeSite = onRemoveSite;
    }

    public override void Initialization(BuildingSO buildingSO, Vector2Int buildPos)
    {
        base.Initialization(buildingSO, buildPos);
        name = $"ConstructionSite_{buildingSO.objectName}_{buildPos.x}_{buildPos.y}";
        EnsureSiteVisual();
    }

    public override bool isVisitable()
    {
        return true;
    }

    internal override float GetLegacyWorkUrgency(FacilityWorkType workType)
    {
        if (workType != FacilityWorkType.Construct)
        {
            return 0f;
        }

        if (workOrderRuntime != null
            && workOrderRuntime.TryGetOrderFor(this, BuiltInWorkTypeIds.Construct, out WorkOrderProgressState order))
        {
            return order.Status switch
            {
                WorkOrderStatus.WaitingForMaterials => 35f,
                WorkOrderStatus.Ready => 80f,
                WorkOrderStatus.InProgress => 90f,
                WorkOrderStatus.Blocked => 15f,
                WorkOrderStatus.WaitingForEligibleWorker => 20f,
                WorkOrderStatus.TargetCurrentlyUnreachable => 5f,
                WorkOrderStatus.WaitingForOutputSpace => 10f,
                _ => 0f
            };
        }

        return 55f;
    }

    public FacilityAssignmentStatus GetConstructionWorkStatus()
    {
        if (isDestroy)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Destroyed,
                "공사 현장 파괴됨");
        }

        if (workOrderRuntime == null
            || !workOrderRuntime.TryGetOrderFor(this, BuiltInWorkTypeIds.Construct, out WorkOrderProgressState order))
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.WorkNotNeeded,
                "공사 주문 없음");
        }

        if (order.Status == WorkOrderStatus.WaitingForMaterials
            && workOrderRuntime.RefreshMaterialsReady(this))
        {
            workOrderRuntime.TryGetOrderFor(this, BuiltInWorkTypeIds.Construct, out order);
        }

        if (order.Status == WorkOrderStatus.Completed
            || order.Status == WorkOrderStatus.Cancelled)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.WorkNotNeeded,
                "이미 끝난 공사");
        }

        if (order.Status == WorkOrderStatus.WaitingForMaterials)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.WorkNotNeeded,
                "재료 도착 대기");
        }

        if (order.Status == WorkOrderStatus.Blocked)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Unknown,
                "공사 막힘");
        }

        if (order.Status == WorkOrderStatus.WaitingForEligibleWorker)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.MissingWorker,
                "조건을 만족하는 건설 작업자를 기다리는 중");
        }

        if (order.Status == WorkOrderStatus.TargetCurrentlyUnreachable)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.WorkNotNeeded,
                "현재 조건으로 목표 품질에 도달할 수 없음");
        }

        if (order.Status == WorkOrderStatus.WaitingForOutputSpace)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Occupied,
                "회수품 또는 출력 공간을 기다리는 중");
        }

        return FacilityAssignmentStatus.Allowed();
    }

    public FacilityAssignmentStatus GetWorkerAssignmentStatus(IBuildingVisitorPort actor)
    {
        if (actor == null)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.MissingWorker,
                "시공 작업자 없음");
        }
        FacilityAssignmentStatus status = GetConstructionWorkStatus();
        if (!status.IsAllowed)
        {
            return status;
        }

        if (actor != null && workers.Contains(actor))
        {
            return FacilityAssignmentStatus.Allowed();
        }

        if (workers.Count >= MaximumWorkers)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Occupied,
                $"시공 인원 상한 {MaximumWorkers}명에 도달함");
        }

        if (HasWorkerReservationForOther(actor))
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Reserved,
                "이미 작업 예약됨");
        }

        if (workOrderRuntime is IWorkOrderWorkerPolicyQuery workerPolicyQuery
            && CharacterBuildingVisitorAdapter.TryGetActor(actor, out CharacterActor character)
            && !workerPolicyQuery.IsWorkerEligible(
                workOrderId,
                character,
                out string policyFailure))
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.MissingWorker,
                policyFailure);
        }

        return FacilityAssignmentStatus.Allowed();
    }

    public bool CanAssignWorker(IBuildingVisitorPort actor, out string failureReason)
    {
        FacilityAssignmentStatus status = GetWorkerAssignmentStatus(actor);
        failureReason = status.Reason;
        return status.IsAllowed;
    }

    public ConstructionSafetyResult GetConstructionSafetyState(
        IBuildingVisitorPort actor,
        bool forced = false)
    {
        int gridVersion = Grid != null ? Grid.StructuralVersion : -1;
        int buildingVersion = WorldRegistry?.BuildingVersion ?? -1;
        Vector2Int workerPosition = actor != null && Grid != null
            ? Grid.GetXY(actor.VisitorSnapshot.Position)
            : new Vector2Int(int.MinValue, int.MinValue);
        if (!hasCachedSafetyResult
            || cachedSafetyGridVersion != gridVersion
            || cachedSafetyBuildingVersion != buildingVersion
            || cachedSafetyWorkerPosition != workerPosition)
        {
            cachedSafetyResult =
                ConstructionSafetyPlanner.Evaluate(this, actor, forced: false);
            cachedSafetyGridVersion = gridVersion;
            cachedSafetyBuildingVersion = buildingVersion;
            cachedSafetyWorkerPosition = workerPosition;
            hasCachedSafetyResult = true;
        }

        lastSafetyResult = forced && !cachedSafetyResult.IsSafe
            ? cachedSafetyResult.AsForcedWarning()
            : cachedSafetyResult;
        return lastSafetyResult;
    }

    public IEnumerator AllocateWorker(IBuildingVisitorPort actor)
    {
        if (!CanAssignWorker(actor, out _))
        {
            yield break;
        }

        int slotIndex = TryGetReservedSlot(actor.BuildingCharacterId, out int reservedSlot)
            ? reservedSlot
            : FindAvailableWorkerSlot();
        if (slotIndex < 0)
        {
            yield break;
        }

        workers.Add(actor);
        workerSlots[actor] = slotIndex;
        ReleaseWorkerReservation(actor);
        MarkFacilityDynamicStateDirty();
        if (actor == null || !actor.VisitorSnapshot.CanMove)
        {
            yield break;
        }

        object currentAction = actor.CurrentActionToken;
        Vector3 workPosition = GetMovementWorldPosition(centerPos);
        float centeredIndex = slotIndex - (MaximumWorkers - 1f) * 0.5f;
        Vector3 standOffset = new Vector3(
            centeredIndex * WorkerStandSpacingX,
            WorkerStandOffsetY,
            0f);
        workerOffsets[actor] = standOffset;
        actor.SetActionPhase("공사 현장 접근", this);
        yield return actor.MoveTo(workPosition, 1f, currentAction);
        actor.ChangeLayer("DungeonMiddleObject");
        yield return actor.MoveTo(
            workPosition + standOffset,
            3f,
            currentAction);
        actor.SetActionPhase("공사 중", this);
        actor.FaceRight();
    }

    public void DeallocateWorker(IBuildingVisitorPort actor)
    {
        if (actor == null || !workers.Remove(actor))
        {
            return;
        }

        Vector3 standOffset = workerOffsets.TryGetValue(actor, out Vector3 savedOffset)
            ? savedOffset
            : new Vector3(0f, WorkerStandOffsetY, 0f);
        workerOffsets.Remove(actor);
        workerSlots.Remove(actor);
        actor.SetActionPhase("공사 현장 이탈", this);
        actor.SetWorldPosition(
            actor.VisitorSnapshot.Position
                - standOffset);
        actor.ChangeLayer("Default");
        MarkFacilityDynamicStateDirty();
    }

    public bool TryReserveParallelWorker(
        IBuildingCharacterPort actor,
        out FacilityAssignmentStatus status,
        float seconds)
    {
        PruneParallelWorkerReservations();
        if (actor == null)
        {
            status = FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.MissingWorker,
                "시공 작업자 없음");
            return false;
        }

        CharacterId characterId = actor.BuildingCharacterId;
        if (FindActiveWorker(characterId) != null)
        {
            status = FacilityAssignmentStatus.Allowed();
            return true;
        }

        if (workerReservations.TryGetValue(
                characterId,
                out ParallelWorkerReservation existingReservation))
        {
            existingReservation.ExpiresAt =
                Time.unscaledTime + Mathf.Max(0.1f, seconds);
            status = FacilityAssignmentStatus.Allowed();
            return true;
        }

        if (workers.Count + workerReservations.Count >= MaximumWorkers)
        {
            status = FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Reserved,
                $"시공 예약 상한 {MaximumWorkers}명에 도달함");
            return false;
        }

        int slotIndex = FindAvailableWorkerSlot();
        if (slotIndex < 0)
        {
            status = FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Reserved,
                "공사 작업 접근 슬롯 없음");
            return false;
        }

        workerReservations.Add(characterId, new ParallelWorkerReservation(
            actor,
            Time.unscaledTime + Mathf.Max(0.1f, seconds),
            slotIndex));
        MarkFacilityDynamicStateDirty();
        status = FacilityAssignmentStatus.Allowed();
        return true;
    }

    public void RefreshParallelWorkerReservation(
        IBuildingCharacterPort actor,
        float seconds)
    {
        PruneParallelWorkerReservations();
        if (actor != null
            && workerReservations.TryGetValue(
                actor.BuildingCharacterId,
                out ParallelWorkerReservation reservation))
        {
            reservation.ExpiresAt = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }
    }

    public bool HasParallelWorkerReservationForOther(IBuildingCharacterPort actor)
    {
        PruneParallelWorkerReservations();
        if (actor != null
            && (workerReservations.ContainsKey(actor.BuildingCharacterId)
                || FindActiveWorker(actor.BuildingCharacterId) != null))
        {
            return false;
        }
        return workers.Count + workerReservations.Count >= MaximumWorkers;
    }

    public void ReleaseParallelWorkerReservation(IBuildingCharacterPort actor)
    {
        if (actor != null && workerReservations.Remove(actor.BuildingCharacterId))
        {
            MarkFacilityDynamicStateDirty();
        }
    }

    private void PruneParallelWorkerReservations()
    {
        if (workerReservations.Count == 0) return;
        float now = Time.unscaledTime;
        expiredWorkerReservations.Clear();
        foreach (KeyValuePair<CharacterId, ParallelWorkerReservation> pair in workerReservations)
        {
            if (pair.Value.Worker == null || pair.Value.ExpiresAt <= now)
                expiredWorkerReservations.Add(pair.Key);
        }
        for (int index = 0; index < expiredWorkerReservations.Count; index++)
            workerReservations.Remove(expiredWorkerReservations[index]);
        if (expiredWorkerReservations.Count > 0)
            MarkFacilityDynamicStateDirty();
    }

    private bool TryGetReservedSlot(CharacterId characterId, out int slotIndex)
    {
        PruneParallelWorkerReservations();
        if (workerReservations.TryGetValue(
                characterId,
                out ParallelWorkerReservation reservation))
        {
            slotIndex = reservation.SlotIndex;
            return true;
        }

        slotIndex = -1;
        return false;
    }

    private IBuildingVisitorPort FindActiveWorker(CharacterId characterId)
    {
        for (int index = 0; index < workers.Count; index++)
        {
            IBuildingVisitorPort worker = workers[index];
            if (worker != null && worker.BuildingCharacterId.Equals(characterId))
            {
                return worker;
            }
        }

        return null;
    }

    private int FindAvailableWorkerSlot()
    {
        int maximumWorkers = MaximumWorkers;
        for (int candidate = 0; candidate < maximumWorkers; candidate++)
        {
            bool used = false;
            foreach (int activeSlot in workerSlots.Values)
            {
                if (activeSlot == candidate)
                {
                    used = true;
                    break;
                }
            }

            if (used)
            {
                continue;
            }

            foreach (ParallelWorkerReservation reservation in workerReservations.Values)
            {
                if (reservation.SlotIndex == candidate)
                {
                    used = true;
                    break;
                }
            }

            if (!used)
            {
                return candidate;
            }
        }

        return -1;
    }

    public bool CompleteConstruction()
    {
        if (isDestroy)
        {
            return false;
        }

        if (completeConstruction != null && !completeConstruction.Invoke())
        {
            return false;
        }

        RemoveSiteOnly();
        return true;
    }

    public void CancelConstruction()
    {
        workOrderRuntime?.CancelOrder(workOrderId, refundDeliveredMaterials: true);
        RemoveSiteOnly();
    }

    public void RemoveSiteOnly()
    {
        if (isDestroy)
        {
            return;
        }

        isDestroy = true;
        workers.Clear();
        workerOffsets.Clear();
        workerSlots.Clear();
        workerReservations.Clear();
        removeSite?.Invoke();
    }

    private void EnsureSiteVisual()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        ownedSiteSprite = CreateSiteSprite();
        ownedSiteTexture = ownedSiteSprite.texture;
        renderer.sprite = ownedSiteSprite;
        renderer.color = new Color(0.92f, 0.80f, 0.38f, 0.62f);
        renderer.sortingLayerName = "DungeonMiddleObject";
        renderer.sortingOrder = 65;

        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }

        int width = Mathf.Max(1, BuildingData != null ? BuildingData.width : 1);
        int height = Mathf.Max(1, BuildingData != null ? BuildingData.height : 1);
        collider.size = new Vector2(width, Mathf.Max(0.1f, height * 3f));
        collider.offset = new Vector2(0f, collider.size.y * 0.5f);
    }

    protected override void OnDestroy()
    {
        Sprite sprite = ownedSiteSprite;
        Texture2D texture = ownedSiteTexture;
        ownedSiteSprite = null;
        ownedSiteTexture = null;
        if (sprite != null)
        {
            UnityEngine.Object.DestroyImmediate(sprite);
        }

        if (texture != null)
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }

        base.OnDestroy();
    }

    private static Sprite CreateSiteSprite()
    {
        Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color fill = Color.white;
        Color edge = new Color(1f, 1f, 1f, 0.95f);
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                bool border = x == 0 || y == 0 || x == 7 || y == 7;
                bool stripe = (x + y) % 4 == 0;
                texture.SetPixel(x, y, border ? edge : stripe ? fill : clear);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0f), 8f);
    }
}
