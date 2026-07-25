using System;
using System.Collections;
using UnityEngine;

public sealed class ConstructionSite : BuildableObject, IWorkableFacility
{
    private const float WorkerStandOffsetY = 0.15f;
    private Func<bool> completeConstruction;
    private Action removeSite;
    private CharacterActor worker;
    private string workOrderId = string.Empty;
    private ConstructionSafetyResult lastSafetyResult = ConstructionSafetyResult.Safe();
    private IWorkOrderRuntime workOrderRuntime;

    public string WorkOrderId => workOrderId;
    public BuildingSO TargetBuilding => BuildingData;
    public Vector2Int GridPosition => centerPos;
    public ConstructionSafetyResult LastSafetyResult => lastSafetyResult;
    public CharacterActor ActiveWorker => worker;

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

        if (worker != null)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Occupied,
                "이미 시공 중");
        }

        return FacilityAssignmentStatus.Allowed();
    }

    public FacilityAssignmentStatus GetWorkerAssignmentStatus(CharacterActor actor)
    {
        FacilityAssignmentStatus status = GetConstructionWorkStatus();
        if (!status.IsAllowed)
        {
            return status;
        }

        if (worker != null && worker != actor)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Occupied,
                "이미 시공 중");
        }

        if (HasWorkerReservationForOther(actor))
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Reserved,
                "이미 작업 예약됨");
        }

        return FacilityAssignmentStatus.Allowed();
    }

    public bool CanAssignWorker(CharacterActor actor, out string failureReason)
    {
        FacilityAssignmentStatus status = GetWorkerAssignmentStatus(actor);
        failureReason = status.Reason;
        return status.IsAllowed;
    }

    public ConstructionSafetyResult GetConstructionSafetyState(
        CharacterActor actor,
        bool forced = false)
    {
        lastSafetyResult = ConstructionSafetyPlanner.Evaluate(this, actor, forced);
        return lastSafetyResult;
    }

    public IEnumerator AllocateWorker(CharacterActor actor)
    {
        if (!CanAssignWorker(actor, out _))
        {
            yield break;
        }

        worker = actor;
        ReleaseWorkerReservation(actor);
        AbilityMove moveable = actor != null ? actor.GetAbility<AbilityMove>() : null;
        if (moveable == null)
        {
            yield break;
        }

        AIAction currentAction = actor.Brain != null ? actor.Brain.bestAction : null;
        Vector3 workPosition = GetMovementWorldPosition(centerPos);
        actor.Brain?.SetActionPhase("공사 현장 접근", this);
        yield return moveable.Move2PosBySpeed(workPosition, 1f, currentAction);
        actor.ChangeLayer("DungeonMiddleObject");
        yield return moveable.Move2PosBySpeed(workPosition + new Vector3(0f, WorkerStandOffsetY), 3f, currentAction);
        actor.Brain?.SetActionPhase("공사 중", this);
        actor.Flip(CharacterFacing.RIGHT);
    }

    public void DeallocateWorker(CharacterActor actor)
    {
        if (actor == null || worker != actor)
        {
            return;
        }

        worker = null;
        actor.Brain?.SetActionPhase("공사 현장 이탈", this);
        actor.transform.position -= new Vector3(0f, WorkerStandOffsetY);
        actor.ChangeLayer("Default");
        MarkFacilityDynamicStateDirty();
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
        removeSite?.Invoke();
    }

    private void EnsureSiteVisual()
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = CreateSiteSprite();
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
