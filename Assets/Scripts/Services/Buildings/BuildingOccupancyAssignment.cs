using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BuildingOccupancy
{
    private readonly BuildableObject owner;
    private readonly Dictionary<IBuildingCharacterPort, float> visitReservations =
        new Dictionary<IBuildingCharacterPort, float>();
    private readonly List<IBuildingCharacterPort> expiredVisitReservations =
        new List<IBuildingCharacterPort>();
    private int currentUserCount;
    private float nextVisitReservationExpiry = float.PositiveInfinity;

    public BuildingOccupancy(BuildableObject owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public int CurrentUserCount => currentUserCount;

    public int ActiveVisitReservationCount
    {
        get
        {
            PruneExpiredVisitReservations();
            return visitReservations.Count;
        }
    }

    public void Reset()
    {
        currentUserCount = 0;
        visitReservations.Clear();
        expiredVisitReservations.Clear();
        nextVisitReservationExpiry = float.PositiveInfinity;
    }

    public bool CanVisit(IBuildingCharacterPort visitor, out string failureReason)
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

        int effectiveCapacity = owner.EffectiveCapacity;
        if (effectiveCapacity > 0
            && currentUserCount + GetActiveVisitReservationCountExcept(visitor)
                >= effectiveCapacity)
        {
            failureReason = "\uC218\uC6A9 \uC778\uC6D0 \uCD08\uACFC";
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
        if (!CanVisit(visitor, out failureReason))
        {
            return false;
        }

        if (owner.PaidFacilityContracts != null
            && !owner.PaidFacilityContracts.TryChargeUse(owner, out failureReason))
        {
            return false;
        }

        ReleaseVisitReservation(visitor);
        currentUserCount++;
        owner.RecordFacilityUse(visitor);
        return true;
    }

    public void EndUse(IBuildingCharacterPort visitor)
    {
        if (currentUserCount <= 0)
        {
            return;
        }

        currentUserCount--;
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

        if (!CanVisit(visitor, out failureReason))
        {
            return false;
        }

        float expiry = Now + Mathf.Max(0.1f, seconds);
        if (visitReservations.TryGetValue(visitor, out float previousExpiry)
            && previousExpiry <= nextVisitReservationExpiry
            && expiry > previousExpiry)
        {
            nextVisitReservationExpiry = 0f;
        }

        visitReservations[visitor] = expiry;
        nextVisitReservationExpiry = Mathf.Min(nextVisitReservationExpiry, expiry);
        owner.NotifyOccupancyOrAssignmentChanged();
        return true;
    }

    public void RefreshVisitReservation(IBuildingCharacterPort visitor, float seconds)
    {
        if (visitor == null || !visitReservations.ContainsKey(visitor))
        {
            return;
        }

        float previousExpiry = visitReservations[visitor];
        float expiry = Now + Mathf.Max(0.1f, seconds);
        visitReservations[visitor] = expiry;
        if (previousExpiry <= nextVisitReservationExpiry
            && expiry > previousExpiry)
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
        if (visitor == null)
        {
            return;
        }

        if (!visitReservations.TryGetValue(visitor, out float expiry)
            || !visitReservations.Remove(visitor))
        {
            return;
        }

        if (expiry <= nextVisitReservationExpiry)
        {
            nextVisitReservationExpiry = 0f;
        }

        owner.NotifyOccupancyOrAssignmentChanged();
    }

    private int GetActiveVisitReservationCountExcept(IBuildingCharacterPort visitor)
    {
        PruneExpiredVisitReservations();
        return Mathf.Max(
            0,
            visitReservations.Count
            - (visitor != null && visitReservations.ContainsKey(visitor) ? 1 : 0));
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
        foreach (KeyValuePair<IBuildingCharacterPort, float> pair in visitReservations)
        {
            if (pair.Key != null
                && pair.Key.IsBuildingInteractionAvailable
                && now < pair.Value)
            {
                nextExpiry = Mathf.Min(nextExpiry, pair.Value);
                continue;
            }

            expiredVisitReservations.Add(pair.Key);
        }

        foreach (IBuildingCharacterPort visitor in expiredVisitReservations)
        {
            visitReservations.Remove(visitor);
            changed = true;
        }

        expiredVisitReservations.Clear();
        nextVisitReservationExpiry = nextExpiry;
        if (changed)
        {
            owner.NotifyOccupancyOrAssignmentChanged();
        }
    }

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
