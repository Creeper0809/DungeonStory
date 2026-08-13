using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BuildingOccupancy
{
    private readonly struct VisitReservation
    {
        public VisitReservation(float expiresAt, long sequence)
        {
            ExpiresAt = expiresAt;
            Sequence = sequence;
        }

        public float ExpiresAt { get; }
        public long Sequence { get; }
    }

    private readonly BuildableObject owner;
    private readonly Dictionary<CharacterId, VisitReservation> visitReservations =
        new Dictionary<CharacterId, VisitReservation>();
    private readonly List<CharacterId> expiredVisitReservations =
        new List<CharacterId>();
    private readonly HashSet<CharacterId> activeUsers =
        new HashSet<CharacterId>();
    private float nextVisitReservationExpiry = float.PositiveInfinity;
    private long nextVisitReservationSequence = 1L;

    public BuildingOccupancy(BuildableObject owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public int CurrentUserCount => activeUsers.Count;

    public int ActiveVisitReservationCount
    {
        get
        {
            PruneExpiredVisitReservations();
            return visitReservations.Count;
        }
    }

    public int WaitingVisitReservationCount
    {
        get
        {
            PruneExpiredVisitReservations();
            int immediatelyAdmissible = Mathf.Max(
                0,
                owner.EffectiveCapacity - activeUsers.Count);
            return Mathf.Max(0, visitReservations.Count - immediatelyAdmissible);
        }
    }

    public void Reset()
    {
        activeUsers.Clear();
        visitReservations.Clear();
        expiredVisitReservations.Clear();
        nextVisitReservationExpiry = float.PositiveInfinity;
        nextVisitReservationSequence = 1L;
    }

    public bool CanVisit(IBuildingCharacterPort visitor, out string failureReason)
    {
        return CanVisitCore(
            visitor,
            includeCapacity: true,
            out failureReason);
    }

    public bool CanQueueVisit(
        IBuildingCharacterPort visitor,
        out string failureReason)
    {
        return CanVisitCore(
            visitor,
            includeCapacity: false,
            out failureReason);
    }

    private bool CanVisitCore(
        IBuildingCharacterPort visitor,
        bool includeCapacity,
        out string failureReason)
    {
        PruneExpiredVisitReservations();
        failureReason = string.Empty;
        if (owner.IsGridDestroyed)
        {
            failureReason = "\uC2DC\uC124 \uD3D0\uC1C4";
            return false;
        }

        if (FacilityEvolutionWorkUtility.IsRelocating(owner))
        {
            failureReason = "\uC774\uC804 \uC791\uC5C5 \uC911";
            return false;
        }

        FacilityData facilityData = owner.Facility;
        if (facilityData == null || !facilityData.IsVisitorFacility)
        {
            failureReason = "\uBC29\uBB38\uC6A9 \uC2DC\uC124 \uC544\uB2D8";
            return false;
        }

        if (!owner.ResolveRoomFacilityPolicy().IsFacilityRoleAvailable(
                owner,
                facilityData.roles,
                out failureReason))
        {
            return false;
        }

        if (owner.IsDamaged && facilityData.disabledWhenDamaged)
        {
            failureReason = "\uC2DC\uC124 \uD30C\uC190";
            return false;
        }

        CharacterId visitorId = GetVisitorId(visitor);
        if (includeCapacity
            && !IsAdmissionReady(visitorId, out failureReason))
        {
            return false;
        }

        if (owner.BuildingData.RequiresStockForUse()
            && owner is IStockedFacility stockedFacility
            && !stockedFacility.HasAvailableStock)
        {
            failureReason = "\uC7AC\uACE0 \uC5C6\uC74C";
            return false;
        }

        if (owner.PaidFacilityContracts != null
            && !owner.PaidFacilityContracts.CanBeginUse(owner, out failureReason))
        {
            return false;
        }

        return true;
    }

    public bool TryBeginUse(IBuildingCharacterPort visitor, out string failureReason)
    {
        CharacterId visitorId = GetVisitorId(visitor);
        if (!visitorId.IsValid)
        {
            failureReason = "방문자 영구 ID 없음";
            return false;
        }

        if (!CanQueueVisit(visitor, out failureReason)
            || !IsAdmissionReady(visitorId, out failureReason))
        {
            return false;
        }

        if (owner.PaidFacilityContracts != null
            && !owner.PaidFacilityContracts.TryChargeUse(owner, out failureReason))
        {
            return false;
        }

        ReleaseVisitReservation(visitor);
        if (!activeUsers.Add(visitorId))
        {
            failureReason = "이미 시설 이용 중";
            return false;
        }
        owner.NotifyOccupancyOrAssignmentChanged();
        return true;
    }

    public bool CompleteUse(IBuildingCharacterPort visitor)
    {
        CharacterId visitorId = GetVisitorId(visitor);
        if (!visitorId.IsValid || !activeUsers.Remove(visitorId))
        {
            return false;
        }

        owner.RecordFacilityUse(visitor);
        return true;
    }

    public void EndUse(IBuildingCharacterPort visitor)
    {
        CharacterId visitorId = GetVisitorId(visitor);
        if (!visitorId.IsValid || !activeUsers.Remove(visitorId))
        {
            return;
        }

        owner.NotifyOccupancyOrAssignmentChanged();
    }

    public bool TryReserveVisit(
        IBuildingCharacterPort visitor,
        out string failureReason,
        float seconds)
    {
        failureReason = string.Empty;
        if (visitor == null || !visitor.IsBuildingInteractionAvailable)
        {
            failureReason = "\uBC29\uBB38 \uC608\uC57D \uB300\uC0C1 \uC5C6\uC74C";
            return false;
        }

        CharacterId visitorId = GetVisitorId(visitor);
        if (!visitorId.IsValid)
        {
            failureReason = "방문자 영구 ID 없음";
            return false;
        }

        if (!CanQueueVisit(visitor, out failureReason))
        {
            return false;
        }

        float expiry = Now + Mathf.Max(0.1f, seconds);
        if (visitReservations.TryGetValue(
                visitorId,
                out VisitReservation previous))
        {
            if (previous.ExpiresAt <= nextVisitReservationExpiry
                && expiry > previous.ExpiresAt)
            {
                nextVisitReservationExpiry = 0f;
            }

            visitReservations[visitorId] = new VisitReservation(
                expiry,
                previous.Sequence);
        }
        else
        {
            visitReservations[visitorId] = new VisitReservation(
                expiry,
                nextVisitReservationSequence++);
        }
        nextVisitReservationExpiry = Mathf.Min(nextVisitReservationExpiry, expiry);
        owner.NotifyOccupancyOrAssignmentChanged();
        return true;
    }

    public void RefreshVisitReservation(IBuildingCharacterPort visitor, float seconds)
    {
        CharacterId visitorId = GetVisitorId(visitor);
        if (!visitorId.IsValid || !visitReservations.ContainsKey(visitorId))
        {
            return;
        }

        VisitReservation previous = visitReservations[visitorId];
        float expiry = Now + Mathf.Max(0.1f, seconds);
        visitReservations[visitorId] = new VisitReservation(
            expiry,
            previous.Sequence);
        if (previous.ExpiresAt <= nextVisitReservationExpiry
            && expiry > previous.ExpiresAt)
        {
            nextVisitReservationExpiry = 0f;
        }
        else
        {
            nextVisitReservationExpiry = Mathf.Min(nextVisitReservationExpiry, expiry);
        }
    }

    public void ReleaseVisitReservation(IBuildingCharacterPort visitor)
    {
        CharacterId visitorId = GetVisitorId(visitor);
        if (!visitorId.IsValid)
        {
            return;
        }

        if (!visitReservations.TryGetValue(
                visitorId,
                out VisitReservation reservation)
            || !visitReservations.Remove(visitorId))
        {
            return;
        }

        if (reservation.ExpiresAt <= nextVisitReservationExpiry)
        {
            nextVisitReservationExpiry = 0f;
        }

        owner.NotifyOccupancyOrAssignmentChanged();
    }

    public int GetVisitQueuePosition(IBuildingCharacterPort visitor)
    {
        PruneExpiredVisitReservations();
        CharacterId visitorId = GetVisitorId(visitor);
        if (!visitorId.IsValid
            || !visitReservations.TryGetValue(
                visitorId,
                out VisitReservation own))
        {
            return 0;
        }

        int ahead = 0;
        foreach (KeyValuePair<CharacterId, VisitReservation> pair in visitReservations)
        {
            if (!pair.Key.Equals(visitorId) && pair.Value.Sequence < own.Sequence)
            {
                ahead++;
            }
        }

        return ahead + 1;
    }

    private bool IsAdmissionReady(
        CharacterId visitorId,
        out string failureReason)
    {
        PruneExpiredVisitReservations();
        failureReason = string.Empty;
        int availableSlots = Mathf.Max(
            0,
            owner.EffectiveCapacity - activeUsers.Count);
        if (availableSlots <= 0)
        {
            failureReason = visitReservations.ContainsKey(visitorId)
                ? $"시설 대기열 {GetVisitQueuePositionById(visitorId)}번째"
                : "수용 인원 초과";
            return false;
        }

        if (!visitorId.IsValid
            || !visitReservations.TryGetValue(
                visitorId,
                out VisitReservation own))
        {
            if (visitReservations.Count == 0)
            {
                return true;
            }

            failureReason = "기존 시설 대기열 우선";
            return false;
        }

        int ahead = 0;
        foreach (KeyValuePair<CharacterId, VisitReservation> pair in visitReservations)
        {
            if (!pair.Key.Equals(visitorId) && pair.Value.Sequence < own.Sequence)
            {
                ahead++;
            }
        }

        if (ahead < availableSlots)
        {
            return true;
        }

        failureReason = $"시설 대기열 {ahead + 1}번째";
        return false;
    }

    private int GetVisitQueuePositionById(CharacterId visitorId)
    {
        if (!visitorId.IsValid
            || !visitReservations.TryGetValue(
                visitorId,
                out VisitReservation own))
        {
            return 0;
        }

        int ahead = 0;
        foreach (KeyValuePair<CharacterId, VisitReservation> pair in visitReservations)
        {
            if (!pair.Key.Equals(visitorId) && pair.Value.Sequence < own.Sequence)
            {
                ahead++;
            }
        }

        return ahead + 1;
    }

    private void PruneExpiredVisitReservations()
    {
        if (visitReservations.Count == 0)
        {
            nextVisitReservationExpiry = float.PositiveInfinity;
            return;
        }

        float now = Now;
        if (now < nextVisitReservationExpiry)
        {
            return;
        }

        bool changed = false;
        float nextExpiry = float.PositiveInfinity;
        expiredVisitReservations.Clear();
        foreach (KeyValuePair<CharacterId, VisitReservation> pair in visitReservations)
        {
            if (pair.Key.IsValid && now < pair.Value.ExpiresAt)
            {
                nextExpiry = Mathf.Min(nextExpiry, pair.Value.ExpiresAt);
                continue;
            }

            expiredVisitReservations.Add(pair.Key);
        }

        foreach (CharacterId visitorId in expiredVisitReservations)
        {
            visitReservations.Remove(visitorId);
            changed = true;
        }

        expiredVisitReservations.Clear();
        nextVisitReservationExpiry = nextExpiry;
        if (changed)
        {
            owner.NotifyOccupancyOrAssignmentChanged();
        }
    }

    private static CharacterId GetVisitorId(IBuildingCharacterPort visitor) =>
        visitor?.BuildingCharacterId ?? default;

    private float Now => owner.OccupancyAndAssignmentTime;
}

public sealed class BuildingAssignment
{
    private const float CleaningWorkThreshold = 75f;

    private readonly BuildableObject owner;
    private IBuildingCharacterPort workerReservation;
    private float workerReservationUntil;

    public BuildingAssignment(BuildableObject owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public IBuildingCharacterPort WorkerReservation
    {
        get
        {
            PruneExpiredWorkerReservation();
            return workerReservation;
        }
    }

    public void Reset()
    {
        workerReservation = null;
        workerReservationUntil = 0f;
    }

    public bool TryReserveWorker(
        IBuildingCharacterPort worker,
        out FacilityAssignmentStatus status,
        float seconds)
    {
        if (worker == null || !worker.IsBuildingInteractionAvailable)
        {
            status = FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.MissingWorker,
                "\uC791\uC5C5 \uC608\uC57D \uB300\uC0C1 \uC5C6\uC74C");
            return false;
        }

        PruneExpiredWorkerReservation();
        if (HasWorkerReservationForOther(worker))
        {
            status = FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Reserved,
                "\uC774\uBBF8 \uC791\uC5C5 \uC608\uC57D\uB428");
            return false;
        }

        workerReservation = worker;
        workerReservationUntil = Now + Mathf.Max(0.1f, seconds);
        owner.NotifyOccupancyOrAssignmentChanged();
        status = FacilityAssignmentStatus.Allowed();
        return true;
    }

    public void RefreshWorkerReservation(IBuildingCharacterPort worker, float seconds)
    {
        PruneExpiredWorkerReservation();
        if (worker == null || workerReservation != worker)
        {
            return;
        }

        workerReservationUntil = Now + Mathf.Max(0.1f, seconds);
    }

    public bool HasWorkerReservationForOther(IBuildingCharacterPort worker)
    {
        PruneExpiredWorkerReservation();
        return workerReservation != null && workerReservation != worker;
    }

    public void ReleaseWorkerReservation(IBuildingCharacterPort worker)
    {
        if (worker == null || workerReservation != worker)
        {
            return;
        }

        workerReservation = null;
        workerReservationUntil = 0f;
        owner.NotifyOccupancyOrAssignmentChanged();
    }

    public FacilityAssignmentStatus GetWorkAssignmentStatus(WorkTypeId workTypeId)
    {
        if (!workTypeId.IsValid
            || !WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition))
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.UnsupportedWork,
                "\uC54C \uC218 \uC5C6\uB294 \uC791\uC5C5");
        }

        return GetWorkAssignmentStatus(FacilityWorkTypeMap.GetRequired(definition));
    }

    public FacilityAssignmentStatus GetWorkAssignmentStatus(FacilityWorkType workType)
    {
        PruneExpiredWorkerReservation();
        if (owner.IsGridDestroyed)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Destroyed,
                "\uC2DC\uC124 \uD3D0\uC1C4");
        }

        FacilityData facilityData = owner.Facility;
        bool supportsButcherFallback = workType == FacilityWorkType.Butcher
            && WildlifeButcherFacilityUtility.IsButcherFacility(owner);
        bool supportsSurvivalFallback = SurvivalFacilityUtility.IsSurvivalWork(workType)
            && (SurvivalFacilityUtility.AddFallbackWorkTypes(
                    owner,
                    FacilityWorkType.None)
                & workType) != 0;
        bool supportsEquipmentMaintenance = workType == FacilityWorkType.Repair
            && CombatEquipmentMaintenanceFacilityUtility.IsMaintenanceFacility(owner);
        bool supportsFacilityEvolution = workType == FacilityWorkType.Craft
            && FacilityEvolutionWorkUtility.HasPendingWork(owner);
        bool supportsRuntimeCapability =
            FacilityWorkTypeMap.TryGet(workType, out WorkTypeDefinition runtimeWork)
            && RuntimeWorkCapabilityUtility.Supports(owner, runtimeWork.WorkTypeId);
        if (facilityData == null
            || (!facilityData.SupportsWork(workType)
                && !supportsButcherFallback
                && !supportsSurvivalFallback
                && !supportsEquipmentMaintenance
                && !supportsFacilityEvolution
                && !supportsRuntimeCapability))
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.UnsupportedWork,
                "\uC9C0\uC6D0\uD558\uC9C0 \uC54A\uB294 \uC791\uC5C5");
        }

        if (workType == FacilityWorkType.Clean
            && owner.FacilityState.cleanliness >= CleaningWorkThreshold)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.WorkNotNeeded,
                "\uCCAD\uC18C\uD560 \uD544\uC694\uAC00 \uC5C6\uC74C");
        }

        if (owner.IsDamaged
            && facilityData.disabledWhenDamaged
            && workType != FacilityWorkType.Repair)
        {
            return FacilityAssignmentStatus.Rejected(
                FacilityAssignmentFailureKind.Damaged,
                "\uC2DC\uC124 \uD30C\uC190");
        }

        return FacilityAssignmentStatus.Allowed();
    }

    public float GetLegacyWorkUrgency(FacilityWorkType workType)
    {
        float urgency = 0f;
        FacilityData facilityData = owner.Facility;
        if (facilityData == null)
        {
            return urgency;
        }

        if (owner.IsDamaged && workType == FacilityWorkType.Repair)
        {
            urgency += 80f;
        }

        int internalStockCapacity = owner.BuildingData.GetInternalStockCapacity();
        if (workType == FacilityWorkType.Restock
            && internalStockCapacity > 0
            && owner is IStockedFacility stockedFacility)
        {
            float stockRatio = Mathf.Clamp01(
                (float)stockedFacility.CurrentStock / internalStockCapacity);
            urgency += Mathf.Lerp(70f, 0f, stockRatio);
            if (stockedFacility.CurrentStock
                <= owner.BuildingData.GetRestockRequestThreshold())
            {
                urgency += 20f;
            }
        }

        if (workType == FacilityWorkType.Research
            && owner.ResolveBlueprintResearchWorkService().HasResearchWorkFor(owner))
        {
            urgency += 45f;
        }

        if (workType == FacilityWorkType.Craft
            && owner.HasPendingEquipmentCraftWork())
        {
            urgency += 55f;
        }

        if (workType == FacilityWorkType.Craft
            && FacilityEvolutionWorkUtility.HasPendingWork(owner))
        {
            urgency += 85f;
        }

        if (workType == FacilityWorkType.Clean
            && owner.FacilityState.cleanliness < CleaningWorkThreshold)
        {
            urgency += Mathf.Lerp(
                15f,
                70f,
                1f - (owner.FacilityState.cleanliness / CleaningWorkThreshold));
        }

        return urgency;
    }

    private void PruneExpiredWorkerReservation()
    {
        if (workerReservation == null
            || (workerReservation.IsBuildingInteractionAvailable
                && Now < workerReservationUntil))
        {
            return;
        }

        workerReservation = null;
        workerReservationUntil = 0f;
        owner.NotifyOccupancyOrAssignmentChanged();
    }

    private float Now => owner.OccupancyAndAssignmentTime;
}
