using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class CharacterMedicalRuntime :
    ICharacterMedicalRuntime,
    IInitializable,
    ITickable,
    IDisposable
{
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly IGridSystemProvider gridProvider;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly ISurvivalFoodRuntime survivalRuntime;
    private readonly IGameEventBus gameEventBus;
    private readonly List<CharacterMedicalOrder> orders = new List<CharacterMedicalOrder>();
    private readonly Dictionary<string, DownedCharacterGridOccupant> downedOccupants =
        new Dictionary<string, DownedCharacterGridOccupant>(StringComparer.Ordinal);
    private readonly Dictionary<string, Transform> carriedPatientParents =
        new Dictionary<string, Transform>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> treatmentFacilityReservations =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private IReadOnlyList<CharacterMedicalOrder> ordersView;
    private IDisposable downedSubscription;
    private IDisposable recoveredSubscription;
    private IDisposable deathSubscription;
    private int orderSequence;

    public CharacterMedicalRuntime(
        ICharacterBodyHealthRuntime bodyHealth,
        IGridSystemProvider gridProvider,
        ICharacterAiWorldRegistry worldRegistry,
        ISurvivalFoodRuntime survivalRuntime,
        IGameEventBus gameEventBus)
    {
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.gridProvider = gridProvider ?? throw new ArgumentNullException(nameof(gridProvider));
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.survivalRuntime = survivalRuntime ?? throw new ArgumentNullException(nameof(survivalRuntime));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
    }

    public IReadOnlyList<CharacterMedicalOrder> ActiveOrders =>
        ordersView ??= ReadOnlyView.List(orders);

    public void Initialize()
    {
        downedSubscription = gameEventBus.Subscribe<CharacterBodyHealthRuntime.CharacterDownedEvent>(
            gameEvent => NotifyCharacterDowned(gameEvent.Actor));
        recoveredSubscription = gameEventBus.Subscribe<CharacterBodyHealthRuntime.CharacterRecoveredEvent>(
            gameEvent => NotifyCharacterRecovered(gameEvent.Actor));
        deathSubscription = gameEventBus.Subscribe<CharacterDeathEvent>(OnCharacterDeath);
    }

    public void Dispose()
    {
        downedSubscription?.Dispose();
        recoveredSubscription?.Dispose();
        deathSubscription?.Dispose();
        downedSubscription = null;
        recoveredSubscription = null;
        deathSubscription = null;
        foreach (CharacterMedicalOrder order in orders.ToArray())
        {
            RemoveDownedOccupant(order.patientId);
        }

        orders.Clear();
        treatmentFacilityReservations.Clear();
    }

    public void Tick()
    {
        foreach (CharacterMedicalOrder order in orders.Where(item => item.IsActive).ToArray())
        {
            if (!TryGetPatient(order, out CharacterActor patient) || patient.IsDead)
            {
                CancelOrder(order, "환자 소실");
                continue;
            }

            if (!order.carried)
            {
                continue;
            }

            CharacterActor rescuer = FindCharacter(order.rescuerId);
            if (rescuer == null || rescuer.IsDead)
            {
                DropPatientAtCurrentPosition(order, patient, "구조자 소실");
                continue;
            }

            Vector3 carryOffset = rescuer.VisualRenderer != null && rescuer.VisualRenderer.flipX
                ? new Vector3(0.28f, 0.04f, -0.01f)
                : new Vector3(-0.28f, 0.04f, -0.01f);
            patient.transform.position = rescuer.transform.position + carryOffset;
            order.PatientPosition = rescuer.GetNowXY();
        }
    }

    private void OnCharacterDeath(CharacterDeathEvent eventType)
    {
        CharacterActor actor = eventType.Actor;
        if (actor == null)
        {
            return;
        }

        string id = GetId(actor);
        foreach (CharacterMedicalOrder order in orders.Where(item => item.IsActive).ToArray())
        {
            if (string.Equals(order.patientId, id, StringComparison.Ordinal))
            {
                CancelOrder(order, "환자 사망");
            }
            else if (string.Equals(order.rescuerId, id, StringComparison.Ordinal))
            {
                ReleaseReservation(order.orderId, actor, "구조자 사망");
            }
        }
    }

    public bool HasAvailableRescueOrder(CharacterActor rescuer)
    {
        return rescuer != null
            && !rescuer.IsDead
            && rescuer.CurrentLifecycleState == CharacterLifecycleState.Active
            && orders.Any(order => IsOrderAvailableTo(order, rescuer));
    }

    public bool TryReserveBestOrder(
        CharacterActor rescuer,
        out CharacterMedicalOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        if (rescuer == null
            || rescuer.IsDead
            || rescuer.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            failureReason = "구조 가능한 캐릭터가 아닙니다.";
            return false;
        }

        RefreshTreatmentFacilities();
        order = orders
            .Where(candidate => IsOrderAvailableTo(candidate, rescuer))
            .OrderBy(candidate => candidate.stabilized ? 1 : 0)
            .ThenBy(candidate => Manhattan(rescuer.GetNowXY(), candidate.PatientPosition))
            .FirstOrDefault();
        if (order == null)
        {
            failureReason = "구조할 환자가 없습니다.";
            return false;
        }

        order.rescuerId = GetId(rescuer);
        order.state = order.stabilized
            ? CharacterMedicalOrderState.AwaitingRescue
            : CharacterMedicalOrderState.AwaitingStabilization;
        order.status = order.stabilized ? "병상으로 이송 준비" : "현장 안정화 준비";
        TryAssignTreatmentFacility(order);
        return true;
    }

    public bool TryReserveOrderForPatient(
        CharacterActor rescuer,
        CharacterActor patient,
        out CharacterMedicalOrder order,
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        if (rescuer == null
            || patient == null
            || rescuer.IsDead
            || patient.IsDead
            || rescuer.CurrentLifecycleState != CharacterLifecycleState.Active
            || patient.CurrentLifecycleState != CharacterLifecycleState.Downed)
        {
            failureReason = "구조자 또는 환자 상태가 유효하지 않습니다.";
            return false;
        }

        string patientId = GetId(patient);
        order = orders.FirstOrDefault(candidate =>
            string.Equals(candidate.patientId, patientId, StringComparison.Ordinal)
            && IsOrderAvailableTo(candidate, rescuer));
        if (order == null)
        {
            failureReason = "선택한 환자의 구조 주문을 예약할 수 없습니다.";
            return false;
        }

        order.rescuerId = GetId(rescuer);
        order.state = order.stabilized
            ? CharacterMedicalOrderState.AwaitingRescue
            : CharacterMedicalOrderState.AwaitingStabilization;
        order.status = order.stabilized ? "병상 이송 준비" : "현장 안정화 준비";
        TryAssignTreatmentFacility(order);
        return true;
    }

    public bool TryGetOrder(string orderId, out CharacterMedicalOrder order)
    {
        order = orders.FirstOrDefault(item => string.Equals(
            item.orderId,
            orderId,
            StringComparison.Ordinal));
        return order != null;
    }

    public bool TryGetPatient(CharacterMedicalOrder order, out CharacterActor patient)
    {
        patient = order != null ? FindCharacter(order.patientId) : null;
        return patient != null && !patient.IsDead;
    }

    public bool TryGetTreatmentFacility(
        CharacterMedicalOrder order,
        out BuildableObject facility)
    {
        facility = null;
        if (order == null || string.IsNullOrWhiteSpace(order.treatmentFacilityId))
        {
            return false;
        }

        facility = GetTreatmentFacilities().FirstOrDefault(candidate =>
            string.Equals(GetFacilityId(candidate), order.treatmentFacilityId, StringComparison.Ordinal));
        return facility != null && !facility.isDestroy;
    }

    public float AdvanceStabilization(string orderId, CharacterActor rescuer, float work)
    {
        if (!TryGetReservedOrder(orderId, rescuer, out CharacterMedicalOrder order)
            || !TryGetPatient(order, out CharacterActor patient))
        {
            return 0f;
        }

        order.state = CharacterMedicalOrderState.Stabilizing;
        order.status = "현장 안정화 중";
        order.completedStabilizationWork = Mathf.Min(
            order.requiredStabilizationWork,
            order.completedStabilizationWork + Mathf.Max(0f, work));
        if (order.completedStabilizationWork + 0.001f < order.requiredStabilizationWork)
        {
            return order.completedStabilizationWork / Mathf.Max(0.01f, order.requiredStabilizationWork);
        }

        bool hadMedicine = survivalRuntime.TryConsumeStoredStock(
            StockCategory.Medicine,
            1) > 0;
        bodyHealth.Stabilize(patient);
        if (!hadMedicine)
        {
            gameEventBus.Publish(new CharacterInfectionBurdenRequestedEvent(patient, 8f));
        }

        order.stabilized = true;
        order.state = CharacterMedicalOrderState.AwaitingRescue;
        order.status = hadMedicine
            ? "안정화 완료"
            : "응급 처치 완료 · 감염 위험";
        TryAssignTreatmentFacility(order);
        return 1f;
    }

    public bool TryBeginCarrying(
        string orderId,
        CharacterActor rescuer,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryGetReservedOrder(orderId, rescuer, out CharacterMedicalOrder order)
            || !TryGetPatient(order, out CharacterActor patient))
        {
            failureReason = "구조 주문이 유효하지 않습니다.";
            return false;
        }

        if (!order.stabilized)
        {
            failureReason = "먼저 현장 안정화가 필요합니다.";
            return false;
        }

        if (!TryAssignTreatmentFacility(order))
        {
            order.state = CharacterMedicalOrderState.AwaitingBed;
            order.status = "치료 침상 필요";
            failureReason = order.status;
            return false;
        }

        RemoveDownedOccupant(order.patientId);
        carriedPatientParents[order.patientId] = patient.transform.parent;
        patient.transform.SetParent(rescuer.transform, worldPositionStays: false);
        patient.transform.localPosition = new Vector3(-0.28f, 0.16f, 0f);
        order.carried = true;
        order.state = CharacterMedicalOrderState.Carrying;
        order.status = "병상으로 이송 중";
        DefenseCombatPresentation.Ensure(patient)?.SetStatus("구조 중", combatActive: false);
        return true;
    }

    public bool TryPlaceAtTreatmentDestination(
        string orderId,
        CharacterActor rescuer,
        out string failureReason)
    {
        failureReason = string.Empty;
        CharacterActor patient = null;
        if (!TryGetReservedOrder(orderId, rescuer, out CharacterMedicalOrder order)
            || !TryGetPatient(order, out patient)
            || !TryGetTreatmentFacility(order, out BuildableObject facility)
            || !gridProvider.TryGetGrid(out Grid grid))
        {
            failureReason = "치료 목적지가 사라졌습니다.";
            if (order != null && patient != null)
            {
                DropPatientAtCurrentPosition(order, patient, failureReason);
            }

            return false;
        }

        order.carried = false;
        order.PatientPosition = facility.centerPos;
        order.BedPosition = facility.centerPos;
        RestorePatientParent(order.patientId, patient);
        patient.transform.position = grid.GetWorldPos(facility.centerPos);
        RegisterDownedOccupant(patient);
        order.state = CharacterMedicalOrderState.Treating;
        order.status = "병상 치료 중";
        DefenseCombatPresentation.Ensure(patient)?.SetStatus("치료 중", combatActive: false);
        return true;
    }

    public float AdvanceTreatment(string orderId, CharacterActor rescuer, float work)
    {
        if (!TryGetReservedOrder(orderId, rescuer, out CharacterMedicalOrder order)
            || !TryGetPatient(order, out CharacterActor patient)
            || !TryGetTreatmentFacility(order, out BuildableObject facility))
        {
            return 0f;
        }

        BuildingMedicalAbility medical = facility.BuildingData?.GetAbility<BuildingMedicalAbility>();
        if (medical?.requiresMedicine == true
            && order.completedTreatmentWork <= 0.001f
            && survivalRuntime.TryConsumeStoredStock(StockCategory.Medicine, 1) <= 0)
        {
            order.status = "약품 운반 대기";
            return 0f;
        }

        order.state = CharacterMedicalOrderState.Treating;
        order.status = "병상 치료 중";
        order.completedTreatmentWork = Mathf.Min(
            order.requiredTreatmentWork,
            order.completedTreatmentWork + Mathf.Max(0f, work));
        if (order.completedTreatmentWork + 0.001f < order.requiredTreatmentWork)
        {
            return order.completedTreatmentWork / Mathf.Max(0.01f, order.requiredTreatmentWork);
        }

        float severityReduction = medical != null
            ? Mathf.Max(0.05f, medical.severityReduction)
            : 0.18f;
        bodyHealth.ApplyTreatment(patient, severityReduction * 40f, 25f);
        CharacterBodyHealthSnapshot snapshot = bodyHealth.GetSnapshot(patient);
        if (!snapshot.Downed)
        {
            NotifyCharacterRecovered(patient);
            return 1f;
        }

        order.completedTreatmentWork = 0f;
        order.requiredTreatmentWork = CalculateTreatmentWork(patient);
        order.status = "추가 치료 필요";
        return 1f;
    }

    public void ReleaseReservation(string orderId, CharacterActor rescuer, string reason)
    {
        if (!TryGetOrder(orderId, out CharacterMedicalOrder order))
        {
            return;
        }

        if (rescuer != null
            && !string.Equals(order.rescuerId, GetId(rescuer), StringComparison.Ordinal))
        {
            return;
        }

        if (order.carried && TryGetPatient(order, out CharacterActor patient))
        {
            DropPatientAtCurrentPosition(order, patient, reason);
        }

        order.rescuerId = string.Empty;
        if (order.IsActive)
        {
            order.state = order.stabilized
                ? CharacterMedicalOrderState.AwaitingRescue
                : CharacterMedicalOrderState.AwaitingStabilization;
            order.status = string.IsNullOrWhiteSpace(reason) ? "구조 대기" : reason;
        }
    }

    public void NotifyCharacterDowned(CharacterActor actor)
    {
        if (actor == null
            || actor.IsDead
            || actor.characterType == CharacterType.Intruder)
        {
            return;
        }

        CancelCharacterActions(actor);
        actor.SetLifecycleState(CharacterLifecycleState.Downed);
        RegisterDownedOccupant(actor);

        string patientId = GetId(actor);
        CharacterMedicalOrder order = orders.FirstOrDefault(item =>
            item.IsActive
            && string.Equals(item.patientId, patientId, StringComparison.Ordinal));
        if (order == null)
        {
            order = new CharacterMedicalOrder
            {
                orderId = $"medical:{++orderSequence}",
                patientId = patientId,
                state = CharacterMedicalOrderState.AwaitingStabilization,
                status = "현장 안정화 필요"
            };
            orders.Add(order);
        }

        order.PatientPosition = actor.GetNowXY();
        order.requiredStabilizationWork = Mathf.Min(
            30f,
            8f + bodyHealth.GetTotalBleeding(actor) * 40f);
        order.requiredTreatmentWork = CalculateTreatmentWork(actor);
        order.stabilized = bodyHealth.GetTotalBleeding(actor) <= 0.001f;
        order.state = order.stabilized
            ? CharacterMedicalOrderState.AwaitingRescue
            : CharacterMedicalOrderState.AwaitingStabilization;
        order.status = order.stabilized ? "구조 대기" : "현장 안정화 필요";
        RequestRescueReplans(actor);
    }

    public void NotifyCharacterRecovered(CharacterActor actor)
    {
        if (actor == null || actor.IsDead)
        {
            return;
        }

        string patientId = GetId(actor);
        foreach (CharacterMedicalOrder order in orders.Where(item =>
            item.IsActive
            && string.Equals(item.patientId, patientId, StringComparison.Ordinal)))
        {
            RestorePatientParent(order.patientId, actor);
            order.carried = false;
            order.state = CharacterMedicalOrderState.Completed;
            order.status = "치료 완료";
            ReleaseFacilityReservation(order);
        }

        RemoveDownedOccupant(patientId);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
    }

    public DungeonCharacterMedicalSaveData Capture()
    {
        return new DungeonCharacterMedicalSaveData
        {
            orderSequence = orderSequence,
            orders = orders.Select(CloneOrder).ToList()
        };
    }

    public void Restore(DungeonCharacterMedicalSaveData saveData, IList<string> warnings)
    {
        orders.Clear();
        treatmentFacilityReservations.Clear();
        foreach (string patientId in downedOccupants.Keys.ToArray())
        {
            RemoveDownedOccupant(patientId);
        }

        orderSequence = Mathf.Max(0, saveData?.orderSequence ?? 0);
        foreach (CharacterMedicalOrder source in saveData?.orders ?? new List<CharacterMedicalOrder>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.orderId)
                || string.IsNullOrWhiteSpace(source.patientId)
                || orders.Any(item => string.Equals(item.orderId, source.orderId, StringComparison.Ordinal)))
            {
                warnings?.Add("유효하지 않거나 중복된 의료 주문을 건너뛰었습니다.");
                continue;
            }

            CharacterMedicalOrder restored = CloneOrder(source);
            CharacterActor patient = FindCharacter(restored.patientId);
            if (patient == null || patient.IsDead)
            {
                restored.state = CharacterMedicalOrderState.Cancelled;
                restored.status = "복원 환자 없음";
                warnings?.Add($"의료 주문 {restored.orderId}: 환자를 찾을 수 없습니다.");
            }
            else if (bodyHealth.GetSnapshot(patient).Downed)
            {
                patient.SetLifecycleState(CharacterLifecycleState.Downed);
                RegisterDownedOccupant(patient);
                restored.carried = false;
                restored.rescuerId = string.Empty;
                restored.state = restored.stabilized
                    ? CharacterMedicalOrderState.AwaitingRescue
                    : CharacterMedicalOrderState.AwaitingStabilization;
            }

            orders.Add(restored);
        }
    }

    private bool IsOrderAvailableTo(CharacterMedicalOrder order, CharacterActor rescuer)
    {
        if (order == null || !order.IsActive || order.carried)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(order.rescuerId)
            && !string.Equals(order.rescuerId, GetId(rescuer), StringComparison.Ordinal))
        {
            return false;
        }

        CharacterActor patient = FindCharacter(order.patientId);
        if (patient == null || patient.IsDead || !bodyHealth.GetSnapshot(patient).Downed)
        {
            return false;
        }

        return !order.stabilized || TryAssignTreatmentFacility(order);
    }

    private bool TryGetReservedOrder(
        string orderId,
        CharacterActor rescuer,
        out CharacterMedicalOrder order)
    {
        return TryGetOrder(orderId, out order)
            && order.IsActive
            && rescuer != null
            && !rescuer.IsDead
            && string.Equals(order.rescuerId, GetId(rescuer), StringComparison.Ordinal);
    }

    private bool TryAssignTreatmentFacility(CharacterMedicalOrder order)
    {
        if (order == null)
        {
            return false;
        }

        if (TryGetTreatmentFacility(order, out BuildableObject current)
            && IsFacilityAvailable(current, order.patientId))
        {
            treatmentFacilityReservations[GetFacilityId(current)] = order.patientId;
            order.BedPosition = current.centerPos;
            return true;
        }

        ReleaseFacilityReservation(order);
        BuildableObject facility = GetTreatmentFacilities()
            .Where(candidate => IsFacilityAvailable(candidate, order.patientId))
            .OrderBy(candidate => Manhattan(candidate.centerPos, order.PatientPosition))
            .FirstOrDefault();
        if (facility == null)
        {
            order.treatmentFacilityId = string.Empty;
            return false;
        }

        string facilityId = GetFacilityId(facility);
        order.treatmentFacilityId = facilityId;
        order.BedPosition = facility.centerPos;
        treatmentFacilityReservations[facilityId] = order.patientId;
        return true;
    }

    private bool IsFacilityAvailable(BuildableObject facility, string patientId)
    {
        if (facility == null || facility.isDestroy)
        {
            return false;
        }

        string facilityId = GetFacilityId(facility);
        return !treatmentFacilityReservations.TryGetValue(facilityId, out string reservedPatient)
            || string.Equals(reservedPatient, patientId, StringComparison.Ordinal);
    }

    private IEnumerable<BuildableObject> GetTreatmentFacilities()
    {
        return worldRegistry.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && (building.BuildingData?.GetAbility<BuildingMedicalAbility>() != null
                    || building.SupportsFacilityRole(FacilityRole.Rest)));
    }

    private void RefreshTreatmentFacilities()
    {
        HashSet<string> existing = new HashSet<string>(
            GetTreatmentFacilities().Select(GetFacilityId),
            StringComparer.Ordinal);
        foreach (string missing in treatmentFacilityReservations.Keys
            .Where(key => !existing.Contains(key))
            .ToArray())
        {
            treatmentFacilityReservations.Remove(missing);
        }
    }

    private void ReleaseFacilityReservation(CharacterMedicalOrder order)
    {
        if (order == null || string.IsNullOrWhiteSpace(order.treatmentFacilityId))
        {
            return;
        }

        treatmentFacilityReservations.Remove(order.treatmentFacilityId);
        order.treatmentFacilityId = string.Empty;
    }

    private void DropPatientAtCurrentPosition(
        CharacterMedicalOrder order,
        CharacterActor patient,
        string reason)
    {
        order.carried = false;
        CharacterActor rescuer = FindCharacter(order.rescuerId);
        RestorePatientParent(order.patientId, patient);
        if (rescuer != null)
        {
            patient.transform.position = rescuer.transform.position;
            order.PatientPosition = rescuer.GetNowXY();
        }

        RegisterDownedOccupant(patient);
        order.rescuerId = string.Empty;
        order.state = order.stabilized
            ? CharacterMedicalOrderState.AwaitingRescue
            : CharacterMedicalOrderState.AwaitingStabilization;
        order.status = string.IsNullOrWhiteSpace(reason) ? "구조 대기" : reason;
    }

    private void RestorePatientParent(string patientId, CharacterActor patient)
    {
        if (patient == null)
        {
            return;
        }

        string id = patientId ?? string.Empty;
        carriedPatientParents.TryGetValue(id, out Transform originalParent);
        carriedPatientParents.Remove(id);
        patient.transform.SetParent(originalParent, worldPositionStays: true);
    }

    private void CancelOrder(CharacterMedicalOrder order, string reason)
    {
        if (order == null)
        {
            return;
        }

        if (TryGetPatient(order, out CharacterActor patient))
        {
            RestorePatientParent(order.patientId, patient);
        }

        order.carried = false;
        order.state = CharacterMedicalOrderState.Cancelled;
        order.status = reason ?? "취소";
        ReleaseFacilityReservation(order);
        RemoveDownedOccupant(order.patientId);
    }

    private void RegisterDownedOccupant(CharacterActor actor)
    {
        if (actor == null
            || !gridProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        string id = GetId(actor);
        RemoveDownedOccupant(id);
        Vector2Int position = grid.GetXY(actor.transform.position);
        if (!grid.IsValidGridPos(position))
        {
            return;
        }

        DownedCharacterGridOccupant occupant = new DownedCharacterGridOccupant(actor);
        if (grid.RegisterOccupant(
                occupant,
                GridLayer.DownedCharacter,
                new[] { position },
                connectPositions: false))
        {
            downedOccupants[id] = occupant;
        }
    }

    private void RemoveDownedOccupant(string patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId)
            || !downedOccupants.TryGetValue(patientId, out DownedCharacterGridOccupant occupant))
        {
            return;
        }

        downedOccupants.Remove(patientId);
        if (occupant?.Actor == null || !gridProvider.TryGetGrid(out Grid grid))
        {
            return;
        }

        Vector2Int position = grid.GetXY(occupant.Actor.transform.position);
        grid.RemoveOccupant(
            GridLayer.DownedCharacter,
            new[] { position },
            disconnectPositions: false);
    }

    private void CancelCharacterActions(CharacterActor actor)
    {
        actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
        actor.GetAbility<AbilityWork>()?.StopAssignedWork("쓰러짐");
        actor.GetComponent<AbilityHaul>()?.StopHauling("쓰러짐");
        actor.GetComponent<AbilityHunt>()?.StopHunting("쓰러짐");
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
    }

    private void RequestRescueReplans(CharacterActor patient)
    {
        foreach (CharacterActor candidate in worldRegistry.Characters)
        {
            if (candidate == null
                || candidate == patient
                || candidate.IsDead
                || candidate.CurrentLifecycleState != CharacterLifecycleState.Active
                || candidate.characterType is CharacterType.Customer or CharacterType.Intruder
                || !candidate.TryGetAbility(out AbilityWork work)
                || work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Rescue)
                    == WorkPriorityLevel.Off)
            {
                continue;
            }

            candidate.Brain?.RequestImmediateReplan(clearFailures: true);
        }
    }

    private float CalculateTreatmentWork(CharacterActor actor)
    {
        CharacterBodyHealthSnapshot snapshot = bodyHealth.GetSnapshot(actor);
        return 20f
            + bodyHealth.GetMissingPartHealth(actor) * 0.8f
            + snapshot.BloodLoss * 0.4f;
    }

    private CharacterActor FindCharacter(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null && string.Equals(GetId(actor), id, StringComparison.Ordinal));
    }

    private static string GetId(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId;
        return !string.IsNullOrWhiteSpace(id)
            ? id
            : $"scene-actor:{actor?.GetInstanceID() ?? 0}";
    }

    private static string GetFacilityId(BuildableObject facility)
    {
        return facility == null
            ? string.Empty
            : $"building:{facility.id}:{facility.centerPos.x}:{facility.centerPos.y}";
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static CharacterMedicalOrder CloneOrder(CharacterMedicalOrder source)
    {
        return new CharacterMedicalOrder
        {
            orderId = source.orderId ?? string.Empty,
            patientId = source.patientId ?? string.Empty,
            rescuerId = source.rescuerId ?? string.Empty,
            treatmentFacilityId = source.treatmentFacilityId ?? string.Empty,
            state = source.state,
            stabilized = source.stabilized,
            carried = source.carried,
            requiredStabilizationWork = source.requiredStabilizationWork,
            completedStabilizationWork = source.completedStabilizationWork,
            requiredTreatmentWork = source.requiredTreatmentWork,
            completedTreatmentWork = source.completedTreatmentWork,
            patientX = source.patientX,
            patientY = source.patientY,
            bedX = source.bedX,
            bedY = source.bedY,
            status = source.status ?? string.Empty
        };
    }
}
