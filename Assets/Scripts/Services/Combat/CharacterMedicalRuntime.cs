using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public sealed partial class CharacterMedicalRuntime :
    ICharacterMedicalQuery,
    ICharacterMedicalCommand,
    ICharacterMedicalPersistence,
    IDungeonRestoreTransactionParticipant,
    IInitializable,
    ITickable,
    IDisposable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CharacterMedicalRuntime.Tick");

    private readonly ICharacterBodyHealthQuery bodyHealthQuery;
    private readonly ICharacterBodyHealthMutationTransaction bodyHealthMutation;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher factRecovery;
    private float nextFactRecoveryTime;
    private string lastFactFailure = string.Empty;
    private readonly IGameCalendar calendar;
    private readonly CharacterMedicalWorldServices world;
    private readonly IGameEventBus gameEventBus;
    private readonly ICharacterCarePriorityQuery carePriorityQuery;
    private readonly IResourceEconomyContentCatalog resourceCatalog;
    private readonly CharacterMedicalSupplyCoordinator supplyCoordinator;
    private readonly ICharacterMedicalSupplyDestinationRuntime supplyDestinations;
    private readonly ICharacterMedicalSupplyDestinationDrainRuntime
        supplyDestinationDrains;
    private readonly IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery
        supplyDestinationDrainRestoreCandidates;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly ICharacterPerformanceQuery performance;
    private readonly ISettlementLaborAccountingService laborAccounting;
    private readonly CharacterMedicalRestoreCoordinator restoreCoordinator;
    private readonly Dictionary<string, CharacterMedicalDownedRegistration>
        downedOccupants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Transform> carriedPatientParents =
        new Dictionary<string, Transform>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> treatmentFacilityReservations =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private IReadOnlyList<CharacterMedicalOrder> ordersView;
    private List<CharacterMedicalOrder> ordersViewSource;
    private IDisposable downedSubscription;
    private IDisposable recoveredSubscription;
    private IDisposable deathSubscription;
    private CharacterMedicalAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(
            () => new CharacterMedicalAggregateState());
    private CharacterMedicalAggregateState writableAggregateState =>
        aggregateRootStore.GetOrCreateWritable(
            () => new CharacterMedicalAggregateState(),
            state => state.Clone());
    private List<CharacterMedicalOrder> orders =>
        writableAggregateState.Orders;
    private int orderSequence
    {
        get => aggregateState.OrderSequence;
        set => writableAggregateState.OrderSequence = value;
    }

    public CharacterMedicalRuntime(
        ICharacterBodyHealthQuery bodyHealthQuery,
        CharacterMedicalWorldServices world,
        IGameEventBus gameEventBus,
        ICharacterCarePriorityQuery carePriorityQuery,
        IResourceEconomyContentCatalog resourceCatalog,
        IItemDefinitionCatalog itemDefinitions,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICharacterPerformanceQuery performance,
        IPhysicalFacilityItemSinkGateway physicalSinks,
        IPackagedLotTareDispositionService packagedTare,
        ICharacterMedicalSupplyDestinationRuntime supplyDestinations,
        ICharacterMedicalSupplyDestinationDrainRuntime supplyDestinationDrains,
        IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery
            supplyDestinationDrainRestoreCandidates,
        ICharacterBodyHealthMutationTransaction bodyHealthMutation,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher factRecovery,
        IGameCalendar calendar,
        ISettlementLaborAccountingService laborAccounting = null)
    {
        this.bodyHealthQuery = bodyHealthQuery
            ?? throw new ArgumentNullException(nameof(bodyHealthQuery));
        this.bodyHealthMutation = bodyHealthMutation
            ?? throw new ArgumentNullException(nameof(bodyHealthMutation));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.factRecovery = factRecovery ?? throw new ArgumentNullException(nameof(factRecovery));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.carePriorityQuery = carePriorityQuery
            ?? throw new ArgumentNullException(nameof(carePriorityQuery));
        this.resourceCatalog = resourceCatalog
            ?? throw new ArgumentNullException(nameof(resourceCatalog));
        this.supplyDestinations = supplyDestinations
            ?? throw new ArgumentNullException(nameof(supplyDestinations));
        this.supplyDestinationDrains = supplyDestinationDrains
            ?? throw new ArgumentNullException(nameof(supplyDestinationDrains));
        this.supplyDestinationDrainRestoreCandidates =
            supplyDestinationDrainRestoreCandidates
            ?? throw new ArgumentNullException(
                nameof(supplyDestinationDrainRestoreCandidates));
        supplyCoordinator = new CharacterMedicalSupplyCoordinator(
            new CharacterMedicalSupplyStockPort(this.world.ItemStacks),
            this.resourceCatalog,
            physicalSinks,
            packagedTare,
            this.supplyDestinations);
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.laborAccounting = laborAccounting;
        CharacterMedicalRestoreServices restoreServices = new(
            this.bodyHealthQuery,
            this.world.WorldRegistry,
            this.resourceCatalog,
            itemDefinitions ?? throw new ArgumentNullException(nameof(itemDefinitions)),
            this.aggregateRootStore);
        CharacterMedicalProjectionContext projectionContext = new(
            downedOccupants,
            carriedPatientParents,
            treatmentFacilityReservations,
            () => { ordersView = null; ordersViewSource = null; },
            () => new CharacterMedicalOrderViewSnapshot(
                ordersView,
                ordersViewSource),
            snapshot =>
            {
                ordersView = snapshot.View;
                ordersViewSource = snapshot.Source;
            });
        restoreCoordinator = new CharacterMedicalRestoreCoordinator(
            restoreServices,
            projectionContext);
    }

    public string ParticipantId => restoreCoordinator.ParticipantId;
    public void ValidatePayload(DungeonCharacterMedicalSaveData saveData) =>
        restoreCoordinator.ValidatePayload(saveData);

    public CharacterMedicalRestoreCandidate PrepareRestore(
        DungeonCharacterMedicalSaveData saveData)
    {
        CharacterMedicalRestoreCandidate candidate =
            restoreCoordinator.PrepareRestore(saveData);
        if (!supplyDestinationDrainRestoreCandidates.IsCandidateAvailable)
        {
            throw new InvalidOperationException(
                "character-medical-supply-drain-restore-candidate-missing");
        }
        CharacterMedicalSupplyDestinationDrainCrossAggregateJoin.Validate(
            candidate.State.Orders,
            supplyDestinationDrainRestoreCandidates.Drains);
        return candidate;
    }
    public void PublishRestore(CharacterMedicalRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        Dictionary<string, Vector2Int> facilityPositions = world.WorldRegistry
            .Buildings
            .Where(building => building != null
                && !building.isDestroy
                && building.PersistentInstanceId.IsValid)
            .ToDictionary(
                building => building.PersistentInstanceId.Value,
                building => building.centerPos,
                StringComparer.Ordinal);
        if (!supplyDestinations.TryReplace(
                candidate.State.Orders,
                facilityPositions,
                out string replacementFailure))
        {
            throw new InvalidOperationException(
                "character-medical-supply-authority-restore-failed:"
                + replacementFailure);
        }
        RecoverCandidateMedicalSuppliesOrThrow(candidate.State.Orders);
        RecoverCandidateMedicalDestinationDrainsOrThrow(
            candidate.State.Orders);
        restoreCoordinator.RefreshCandidateProjection(candidate);
        restoreCoordinator.PublishRestore(candidate);
    }
    public void BeginRestoreCandidate() => restoreCoordinator.BeginRestoreCandidate();
    public void PublishRestoreCandidate() => restoreCoordinator.PublishRestoreCandidate();
    public void RollbackPublishedRestoreCandidate() =>
        restoreCoordinator.RollbackPublishedRestoreCandidate();
    public void CompleteRestoreCandidate()
    {
        restoreCoordinator.CompleteRestoreCandidate();
        nextFactRecoveryTime = 0f;
    }
    public void DiscardRestoreCandidate() => restoreCoordinator.DiscardRestoreCandidate();

    public IReadOnlyList<CharacterMedicalOrder> ActiveOrders
    {
        get
        {
            List<CharacterMedicalOrder> current = aggregateState.Orders;
            if (!ReferenceEquals(ordersViewSource, current))
            {
                ordersViewSource = current;
                ordersView = ReadOnlyView.List(current);
            }

            return ordersView;
        }
    }

    public void Initialize()
    {
        if (downedSubscription != null) return;
        RecoverPendingMedicalSuppliesOrThrow();
        RecoverPendingMedicalDestinationDrainsOrThrow();
        downedSubscription = gameEventBus.Subscribe<CharacterBodyHealthDownedEvent>(
            gameEvent => NotifyCharacterDowned(gameEvent.Actor));
        recoveredSubscription = gameEventBus.Subscribe<CharacterBodyHealthRecoveredEvent>(
            gameEvent => NotifyCharacterRecovered(gameEvent.Actor));
        deathSubscription = gameEventBus.Subscribe<CharacterDeathEvent>(OnCharacterDeath);
        RecoverPendingProcedureFacts();
    }

    private void RecoverPendingMedicalSuppliesOrThrow()
    {
        RecoverCandidateMedicalSuppliesOrThrow(orders);
    }

    private void RecoverCandidateMedicalSuppliesOrThrow(
        IEnumerable<CharacterMedicalOrder> source)
    {
        foreach (CharacterMedicalOrder order in (source
                     ?? Enumerable.Empty<CharacterMedicalOrder>())
                     .Where(value => value != null)
                     .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            if (!supplyCoordinator.TryRecoverPendingSupply(
                    order,
                    out string recoveryFailure))
            {
                throw new InvalidOperationException(
                    $"Medical supply recovery failed for '{order.orderId}': "
                    + recoveryFailure);
            }
        }
    }

    private void RecoverCandidateMedicalDestinationDrainsOrThrow(
        IEnumerable<CharacterMedicalOrder> source)
    {
        foreach (CharacterMedicalOrder order in (source
                     ?? Enumerable.Empty<CharacterMedicalOrder>())
                     .Where(value => value != null
                         && value.state == CharacterMedicalOrderState
                             .MaterialDestinationDraining)
                     .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            CharacterMedicalSupplyDestinationDrainAdvanceResult resumed =
                supplyDestinationDrains.TryResume(order);
            if (resumed.Status !=
                CharacterMedicalSupplyDestinationDrainAdvanceStatus.Closed)
            {
                throw new InvalidOperationException(
                    "character-medical-supply-drain-restore-recovery-failed:"
                    + order.orderId + ":" + resumed.Status + ":"
                    + resumed.FailureReason);
            }
        }
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
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        if (Time.realtimeSinceStartup >= nextFactRecoveryTime)
        {
            nextFactRecoveryTime = Time.realtimeSinceStartup + 0.5f;
            RecoverPendingProcedureFacts();
        }
        for (int index = 0; index < orders.Count; index++)
        {
            CharacterMedicalOrder order = orders[index];
            if (order == null || !order.IsActive)
            {
                continue;
            }

            if (order.state ==
                CharacterMedicalOrderState.MaterialDestinationDraining)
            {
                if (!supplyCoordinator.TryRecoverPendingSupply(
                        order,
                        out string supplyRecoveryFailure))
                {
                    throw new InvalidOperationException(
                        "character-medical-supply-drain-sink-recovery-failed:"
                        + order.orderId + ":" + supplyRecoveryFailure);
                }
                CharacterMedicalSupplyDestinationDrainAdvanceResult resumed =
                    supplyDestinationDrains.TryResume(order);
                if (resumed.Status ==
                    CharacterMedicalSupplyDestinationDrainAdvanceStatus
                        .Conflict)
                {
                    throw new InvalidOperationException(
                        "character-medical-supply-drain-resume-conflict:"
                        + resumed.FailureReason);
                }
                if (resumed.IsClosed)
                {
                    FinalizeClosedDestinationDrain(
                        order,
                        resumed.ClosedFacilityId);
                }
                continue;
            }

            if (!TryGetPatient(order, out CharacterActor patient) || patient.IsDead)
            {
                CancelOrder(order, CharacterMedicalStatusCode.PatientMissing);
                continue;
            }

            if (!bodyHealthQuery.GetSnapshot(patient).Downed)
            {
                CompleteRecoveredOrder(order, patient);
                continue;
            }

            if (!order.carried)
            {
                continue;
            }

            CharacterActor rescuer = FindCharacter(order.rescuerId);
            if (rescuer == null || rescuer.IsDead)
            {
                DropPatientAtCurrentPosition(
                    order,
                    patient,
                    CharacterMedicalStatusCode.RescuerMissing);
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
        string id = eventType.CharacterId.Value;
        CharacterActor actor = FindCharacter(id);
        if (actor == null)
        {
            return;
        }

        foreach (CharacterMedicalOrder order in orders.Where(item => item.IsActive).ToArray())
        {
            if (string.Equals(order.patientId, id, StringComparison.Ordinal))
            {
                CancelOrder(order, CharacterMedicalStatusCode.PatientDied);
            }
            else if (string.Equals(order.rescuerId, id, StringComparison.Ordinal))
            {
                TryReleaseReservation(
                    order.orderId,
                    actor,
                    CharacterMedicalStatusCode.RescuerDied,
                    out _);
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
        out DomainFailure failure)
    {
        order = null;
        failure = DomainFailure.None;
        if (rescuer == null
            || rescuer.IsDead
            || rescuer.CurrentLifecycleState != CharacterLifecycleState.Active)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalRescuerUnavailable);
            return false;
        }

        RefreshTreatmentFacilities();
        order = orders
            .Where(candidate => IsOrderAvailableTo(candidate, rescuer))
            .OrderByDescending(candidate =>
                carePriorityQuery.GetCarePriority(candidate.patientId))
            .ThenBy(candidate => candidate.stabilized ? 1 : 0)
            .ThenBy(candidate => Manhattan(rescuer.GetNowXY(), candidate.PatientPosition))
            .FirstOrDefault();
        if (order == null)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }
        order.rescuerId = GetId(rescuer);
        BeginMedicalLaborReservation(order);
        order.state = order.stabilized
            ? CharacterMedicalOrderState.AwaitingRescue
            : CharacterMedicalOrderState.AwaitingStabilization;
        order.SetStatus(order.stabilized
            ? CharacterMedicalStatusCode.PreparingTransfer
            : CharacterMedicalStatusCode.PreparingStabilization);
        TryAssignTreatmentFacility(order);
        return true;
    }

    public bool TryReserveOrderForPatient(
        CharacterActor rescuer,
        CharacterActor patient,
        out CharacterMedicalOrder order,
        out DomainFailure failure)
    {
        order = null;
        failure = DomainFailure.None;
        if (rescuer == null
            || patient == null
            || rescuer.IsDead
            || patient.IsDead
            || rescuer.CurrentLifecycleState != CharacterLifecycleState.Active
            || patient.CurrentLifecycleState != CharacterLifecycleState.Downed)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalParticipantsInvalid);
            return false;
        }

        string patientId = GetId(patient);
        order = orders.FirstOrDefault(candidate =>
            string.Equals(candidate.patientId, patientId, StringComparison.Ordinal)
            && IsOrderAvailableTo(candidate, rescuer));
        if (order == null)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalOrderUnavailable,
                patientId);
            return false;
        }
        if (order.state ==
            CharacterMedicalOrderState.MaterialDestinationDraining)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalDestinationUnavailable,
                patientId);
            return false;
        }

        order.rescuerId = GetId(rescuer);
        BeginMedicalLaborReservation(order);
        order.state = order.stabilized
            ? CharacterMedicalOrderState.AwaitingRescue
            : CharacterMedicalOrderState.AwaitingStabilization;
        order.SetStatus(order.stabilized
            ? CharacterMedicalStatusCode.PreparingTransfer
            : CharacterMedicalStatusCode.PreparingStabilization);
        TryAssignTreatmentFacility(order);
        return true;
    }

    public bool TryRequestTreatment(
        CharacterActor patient,
        out CharacterMedicalOrder order,
        out DomainFailure failure)
    {
        order = null;
        failure = DomainFailure.None;
        if (patient == null || patient.IsDead)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalPatientUnavailable);
            return false;
        }

        CharacterBodyHealthSnapshot health = bodyHealthQuery.GetSnapshot(patient);
        bool injured = health.Downed
            || health.BloodLoss > 0.01f
            || health.Parts.Any(part =>
                part != null && part.currentHealth + 0.01f < part.maxHealth);
        if (!injured)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalNoTreatableInjury,
                GetId(patient));
            return false;
        }

        if (!health.Downed)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalAmbulatoryTreatmentUnsupported,
                GetId(patient));
            return false;
        }

        NotifyCharacterDowned(patient);
        string patientId = GetId(patient);
        order = orders.FirstOrDefault(candidate =>
            candidate.IsActive
            && string.Equals(candidate.patientId, patientId, StringComparison.Ordinal));
        if (order == null)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalOrderCreationFailed,
                patientId);
            return false;
        }
        if (order.state ==
            CharacterMedicalOrderState.MaterialDestinationDraining)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalDestinationUnavailable,
                patientId);
            return false;
        }

        order.SetStatus(CharacterMedicalStatusCode.TreatmentRequested);
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

    public bool TryAssignSpecificTreatmentFacility(
        string orderId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        CharacterMedicalOrder order = orders.FirstOrDefault(candidate =>
            candidate != null
            && candidate.IsActive
            && string.Equals(candidate.orderId, orderId, StringComparison.Ordinal));
        if (order == null)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalOrderMissing,
                orderId ?? string.Empty);
            return false;
        }
        if (order.state ==
            CharacterMedicalOrderState.MaterialDestinationDraining)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalDestinationUnavailable,
                orderId ?? string.Empty);
            return false;
        }

        bool isSurgicalFacility = facility?.BuildingData?.Abilities?
            .OfType<ISurgicalFacilityAbility>()
            .Any(ability => ability.IsPrimaryOperatingFacility) == true;
        if (facility == null
            || facility.isDestroy
            || facility.IsDamaged
            || facility.BuildingData?.GetAbility<BuildingMedicalAbility>() == null
                && !isSurgicalFacility)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalFacilityUnavailable,
                facility != null ? GetFacilityId(facility) : string.Empty);
            return false;
        }

        string facilityId = GetFacilityId(facility);
        if (treatmentFacilityReservations.TryGetValue(
                facilityId,
                out string reservedPatient)
            && !string.Equals(
                reservedPatient,
                order.patientId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalFacilityReserved,
                facilityId,
                reservedPatient);
            return false;
        }

        if (!string.IsNullOrEmpty(order.treatmentFacilityId)
            && string.Equals(
                order.treatmentFacilityId,
                facilityId,
                StringComparison.Ordinal))
        {
            order.BedPosition = facility.centerPos;
            treatmentFacilityReservations[facilityId] = order.patientId;
            return true;
        }

        if (!string.IsNullOrEmpty(order.treatmentMaterialDestinationId))
        {
            CharacterMedicalOrderState target = order.stabilized
                ? CharacterMedicalOrderState.AwaitingBed
                : CharacterMedicalOrderState.AwaitingStabilization;
            CharacterMedicalStatusCode targetStatus = order.stabilized
                ? CharacterMedicalStatusCode.AwaitingBed
                : CharacterMedicalStatusCode.AwaitingStabilization;
            CharacterMedicalSupplyDestinationDrainAdvanceResult drained =
                TryDrainTreatmentDestination(
                order,
                target,
                targetStatus,
                Array.Empty<string>());
            if (!drained.IsClosed)
            {
                failure = new DomainFailure(
                    FailureCode.CharacterMedicalDestinationUnavailable,
                    orderId ?? string.Empty);
                return false;
            }
        }

        ReleaseFacilityReservation(order);
        order.treatmentFacilityId = facilityId;
        order.BedPosition = facility.centerPos;
        treatmentFacilityReservations[facilityId] = order.patientId;
        return true;
    }

    public float AdvanceStabilization(string orderId, CharacterActor rescuer, float work)
    {
        if (!TryGetReservedOrder(orderId, rescuer, out CharacterMedicalOrder order)
            || !TryGetPatient(order, out CharacterActor patient))
        {
            return 0f;
        }
        if (order.stabilized)
            return 1f;

        float completed = Mathf.Min(
            order.requiredStabilizationWork,
            order.completedStabilizationWork + Mathf.Max(0f, work));
        if (completed + 0.001f < order.requiredStabilizationWork)
        {
            RecordMedicalLabor(
                order,
                rescuer,
                completed - order.completedStabilizationWork,
                stabilization: true);
            order.state = CharacterMedicalOrderState.Stabilizing;
            order.SetStatus(CharacterMedicalStatusCode.Stabilizing);
            order.completedStabilizationWork = completed;
            return completed / Mathf.Max(0.01f, order.requiredStabilizationWork);
        }

        if (!TryReserveMedicalFact(order, rescuer, patient,
                CharacterMedicalProcedureKind.Stabilization, completed, order.requiredStabilizationWork,
                out DomainFactLease reservation, out CharacterMedicalProcedureFactData draft))
            return order.completedStabilizationWork
                / Mathf.Max(0.01f, order.requiredStabilizationWork);
        CharacterBodyHealthMutationSnapshot before = default;
        try
        {
            before = bodyHealthMutation.CaptureCombatMutation(patient);
            bodyHealthMutation.ApplyPreparedMedicalStabilization(patient);
            if (!TryCommitMedicalFact(order, patient, reservation, draft, before))
                return order.completedStabilizationWork
                    / Mathf.Max(0.01f, order.requiredStabilizationWork);
        }
        catch
        {
            if (before.IsValid)
                bodyHealthMutation.RestoreCombatMutation(
                    patient, before, "medical-stabilization-aborted");
            facts.Cancel(reservation);
            throw;
        }

        RecordMedicalLabor(
            order,
            rescuer,
            completed - order.completedStabilizationWork,
            stabilization: true);
        order.currentReservationStabilizationWork = 0f;
        order.completedStabilizationWork = completed;
        order.stabilized = true;
        order.state = CharacterMedicalOrderState.AwaitingRescue;
        order.SetStatus(CharacterMedicalStatusCode.StabilizedWithInfectionRisk);
        bodyHealthMutation.CompletePreparedMedicalMutation(patient, before);
        gameEventBus.Publish(new CharacterInfectionBurdenRequestedEvent(patient, 8f));
        TryAssignTreatmentFacility(order);
        return 1f;
    }

    public bool TryBeginCarrying(
        string orderId,
        CharacterActor rescuer,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGetReservedOrder(orderId, rescuer, out CharacterMedicalOrder order)
            || !TryGetPatient(order, out CharacterActor patient))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalOrderUnavailable,
                orderId ?? string.Empty);
            return false;
        }

        if (!order.stabilized)
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalStabilizationRequired,
                orderId ?? string.Empty);
            return false;
        }

        if (!TryAssignTreatmentFacility(order))
        {
            if (order.state !=
                CharacterMedicalOrderState.MaterialDestinationDraining)
            {
                order.state = CharacterMedicalOrderState.AwaitingBed;
                order.SetStatus(CharacterMedicalStatusCode.AwaitingBed);
            }
            failure = new DomainFailure(
                FailureCode.CharacterMedicalBedUnavailable,
                orderId ?? string.Empty);
            return false;
        }

        RemoveDownedOccupant(order.patientId);
        carriedPatientParents[order.patientId] = patient.transform.parent;
        patient.transform.SetParent(rescuer.transform, worldPositionStays: false);
        patient.transform.localPosition = new Vector3(-0.28f, 0.16f, 0f);
        order.carried = true;
        order.state = CharacterMedicalOrderState.Carrying;
        order.SetStatus(CharacterMedicalStatusCode.Carrying);
        DefenseCombatPresentation.Ensure(patient)?.SetStatus(
            CharacterMedicalStatusCode.Carrying.ToString(),
            combatActive: false);
        return true;
    }

    public bool TryPlaceAtTreatmentDestination(
        string orderId,
        CharacterActor rescuer,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        CharacterActor patient = null;
        if (!TryGetReservedOrder(orderId, rescuer, out CharacterMedicalOrder order)
            || !TryGetPatient(order, out patient)
            || !TryGetTreatmentFacility(order, out BuildableObject facility)
            || !world.GridProvider.TryGetGrid(out Grid grid))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalDestinationUnavailable,
                orderId ?? string.Empty);
            if (order != null && patient != null)
            {
                PreparePatientForUnassignedState(order, patient);
                TryDrainTreatmentDestination(
                    order,
                    order.stabilized
                        ? CharacterMedicalOrderState.AwaitingRescue
                        : CharacterMedicalOrderState.AwaitingStabilization,
                    order.stabilized
                        ? CharacterMedicalStatusCode.AwaitingBed
                        : CharacterMedicalStatusCode.AwaitingStabilization,
                    Array.Empty<string>());
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
        order.SetStatus(CharacterMedicalStatusCode.Treating);
        DefenseCombatPresentation.Ensure(patient)?.SetStatus(
            CharacterMedicalStatusCode.Treating.ToString(),
            combatActive: false);
        return true;
    }

    public float AdvanceTreatment(string orderId, CharacterActor rescuer, float work)
    {
        if (!TryGetReservedOrder(orderId, rescuer, out CharacterMedicalOrder order)
            || !TryGetPatient(order, out CharacterActor patient))
        {
            return 0f;
        }

        if (!TryGetTreatmentFacility(order, out BuildableObject facility))
        {
            order.rescuerId = string.Empty;
            RegisterDownedOccupant(patient);
            TryDrainTreatmentDestination(
                order,
                CharacterMedicalOrderState.AwaitingBed,
                CharacterMedicalStatusCode.AwaitingBed,
                Array.Empty<string>());
            return 0f;
        }

        BuildingMedicalAbility medical = facility.BuildingData?.GetAbility<BuildingMedicalAbility>();
        float completed = Mathf.Min(
            order.requiredTreatmentWork,
            order.completedTreatmentWork + Mathf.Max(0f, work));
        bool terminal = completed + 0.001f >= order.requiredTreatmentWork;
        DomainFactLease reservation = default;
        CharacterMedicalProcedureFactData draft = null;
        if (terminal && !TryReserveMedicalFact(order, rescuer, patient,
                CharacterMedicalProcedureKind.Treatment, completed, order.requiredTreatmentWork,
                out reservation, out draft))
            return order.completedTreatmentWork
                / Mathf.Max(0.01f, order.requiredTreatmentWork);
        bool supplyReady = true;
        try
        {
            if (medical?.requiresMedicine == true
                && order.completedTreatmentWork <= 0.001f)
                supplyReady = supplyCoordinator.EnsureTreatmentSupplyReady(
                    order, facility);
        }
        catch
        {
            if (terminal) facts.Cancel(reservation);
            throw;
        }
        if (!supplyReady)
        {
            if (terminal) facts.Cancel(reservation);
            return 0f;
        }

        if (!terminal)
        {
            RecordMedicalLabor(
                order,
                rescuer,
                completed - order.completedTreatmentWork,
                stabilization: false);
            order.state = CharacterMedicalOrderState.Treating;
            order.SetStatus(
                order.treatmentSupply == CharacterMedicalSupplyKind.ExtractedBlood
                    ? CharacterMedicalStatusCode.TreatingWithExtractedBlood
                    : CharacterMedicalStatusCode.Treating);
            order.completedTreatmentWork = completed;
            return completed / Mathf.Max(0.01f, order.requiredTreatmentWork);
        }

        float severityReduction = medical != null
            ? Mathf.Max(0.05f, medical.severityReduction)
            : 0.18f;
        bool usedExtractedBlood =
            order.treatmentSupply == CharacterMedicalSupplyKind.ExtractedBlood;
        float treatmentEfficiency = usedExtractedBlood
            ? 0.55f
            : Mathf.Max(0.1f, order.treatmentPotency);
        if (rescuer != null)
        {
            CharacterPerformanceSnapshot treatment;
            try
            {
                treatment = performance.Evaluate(
                    rescuer,
                    "performance:medical:treatment-efficiency");
                if (treatment == null)
                    throw new InvalidOperationException(
                        "Medical treatment performance result is missing.");
            }
            catch
            {
                facts.Cancel(reservation);
                throw;
            }
            treatmentEfficiency *= treatment.IsApplicable ? treatment.Value : 0f;
        }
        CharacterBodyHealthMutationSnapshot before = default;
        try
        {
            before = bodyHealthMutation.CaptureCombatMutation(patient);
            bodyHealthMutation.ApplyPreparedMedicalTreatment(
                patient,
                severityReduction * 40f * treatmentEfficiency,
                usedExtractedBlood ? 14f : 25f);
            if (!TryCommitMedicalFact(order, patient, reservation, draft, before))
                return order.completedTreatmentWork
                    / Mathf.Max(0.01f, order.requiredTreatmentWork);
        }
        catch
        {
            if (before.IsValid)
                bodyHealthMutation.RestoreCombatMutation(
                    patient, before, "medical-treatment-aborted");
            facts.Cancel(reservation);
            throw;
        }
        order.state = CharacterMedicalOrderState.Treating;
        RecordMedicalLabor(
            order,
            rescuer,
            completed - order.completedTreatmentWork,
            stabilization: false);
        order.currentReservationTreatmentWork = 0f;
        order.completedTreatmentWork = completed;
        bodyHealthMutation.CompletePreparedMedicalMutation(patient, before);
        if (usedExtractedBlood)
        {
            ApplyExtractedBloodConsequences(patient, infection: 4f, instability: 6f);
        }
        else if (order.treatmentInfectionReduction > 0f)
        {
            gameEventBus.Publish(
                new CharacterInfectionBurdenReductionRequestedEvent(
                    patient,
                    order.treatmentInfectionReduction));
        }

        if (!usedExtractedBlood
            && order.treatmentPainReduction > 0f
            && !string.IsNullOrWhiteSpace(order.treatmentItemId))
        {
            patient.ApplyMoodFactor(
                $"medical:relief:{order.treatmentItemId}",
                "MedicalTreatmentPainRelief",
                Mathf.Clamp(order.treatmentPainReduction * 0.15f, 1f, 6f),
                180f,
                1);
        }

        gameEventBus.Publish(new CharacterMedicalBloodContactEvent(
            CharacterPersistentIdentity.Require(patient),
            CharacterPersistentIdentity.Require(rescuer),
            usedExtractedBlood));

        order.treatmentSupply = CharacterMedicalSupplyKind.None;
        order.treatmentSupplyConsumed = false;
        order.treatmentSupplyDeliveryRequested = false;
        order.treatmentItemId = string.Empty;
        order.treatmentPotency = 1f;
        order.treatmentInfectionReduction = 0f;
        order.treatmentPainReduction = 0f;
        CharacterBodyHealthSnapshot snapshot = bodyHealthQuery.GetSnapshot(patient);
        if (!snapshot.Downed)
        {
            return 1f;
        }

        order.completedTreatmentWork = 0f;
        order.requiredTreatmentWork = CalculateTreatmentWork(patient);
        order.SetStatus(CharacterMedicalStatusCode.AdditionalTreatmentRequired);
        return 1f;
    }

    private bool TryReserveMedicalFact(CharacterMedicalOrder order, CharacterActor rescuer,
        CharacterActor patient, CharacterMedicalProcedureKind procedure, float completedWork, float requiredWork,
        out DomainFactLease lease, out CharacterMedicalProcedureFactData draft)
    {
        lease = default;
        draft = null;
        if (order.nextTerminalOutcomeSequence <= 0 || order.nextTerminalOutcomeSequence == int.MaxValue)
            throw new InvalidOperationException("medical-procedure-sequence-exhausted");
        if (aggregateState.PendingProcedureFacts.Count >= CharacterMedicalSaveValidation.MaximumPendingProcedureFacts)
        {
            ReportFactFailure("medical-procedure-pending-capacity");
            return false;
        }
        string patientId = CharacterPersistentIdentity.Require(patient).Value;
        string clinicianId = CharacterPersistentIdentity.Require(rescuer).Value;
        draft = new CharacterMedicalProcedureFactData
        {
            orderId = order.orderId, sequence = order.nextTerminalOutcomeSequence,
            absoluteDay = calendar.Current.AbsoluteDay,
            patientId = patientId, patientName = string.IsNullOrWhiteSpace(patient.name) ? patientId : patient.name,
            clinicianId = clinicianId, clinicianName = string.IsNullOrWhiteSpace(rescuer.name) ? clinicianId : rescuer.name,
            facilityId = order.treatmentFacilityId, procedure = procedure,
            completedWork = completedWork, requiredWork = requiredWork
        };
        var proposal = new CharacterMedicalProcedureFact(draft, proposalOnly: true);
        if (!facts.TryReserve(proposal, out lease, out string failure))
        {
            ReportFactFailure("medical-procedure-reserve:" + failure);
            return false;
        }
        if (!lease.IsValid || string.IsNullOrWhiteSpace(lease.PublicationKey))
        {
            facts.Cancel(lease);
            throw new InvalidOperationException("medical-procedure-reservation-identity-missing");
        }
        return true;
    }

    private bool TryCommitMedicalFact(CharacterMedicalOrder order, CharacterActor patient,
        in DomainFactLease lease, CharacterMedicalProcedureFactData draft,
        in CharacterBodyHealthMutationSnapshot before)
    {
        var after = bodyHealthMutation.CaptureCombatMutation(patient);
        draft.before = CharacterMedicalProcedureFact.CaptureVitals(before.State);
        draft.after = CharacterMedicalProcedureFact.CaptureVitals(after.State);
        if (draft.procedure == CharacterMedicalProcedureKind.Treatment)
        {
            draft.materialKind = order.treatmentSupply;
            draft.materialItemId = order.treatmentItemId;
            draft.materialQuantity = order.treatmentSupplyConsumed ? 1 : 0;
        }
        var fact = new CharacterMedicalProcedureFact(draft);
        var pending = new CharacterMedicalPendingFactData
        { publicationKey = lease.PublicationKey, fact = fact.Snapshot() };
        var pendingFacts = writableAggregateState.PendingProcedureFacts;
        pendingFacts.Add(pending);
        DomainFactDelivery result;
        try { result = facts.Publish(lease, fact); }
        catch (Exception exception)
        {
            // The reply can be lost after the durable boundary. Preserve the
            // applied body and exact fact; replay never performs medical effects.
            result = DomainFactDelivery.Uncertain("medical-procedure-publish:" + exception.GetType().Name);
        }
        if (!result.MayBeCommitted)
        {
            bodyHealthMutation.RestoreCombatMutation(patient, before, "medical-procedure-publication-rejected");
            pendingFacts.Remove(pending);
            ReportFactFailure(result.Detail);
            return false;
        }
        order.nextTerminalOutcomeSequence++;
        if (result.Completed && result.OperationKey == pending.publicationKey)
        {
            pendingFacts.Remove(pending);
            lastFactFailure = string.Empty;
        }
        else ReportFactFailure("medical-procedure-pending:" + result.Detail);
        return true;
    }

    internal void RecoverPendingProcedureFacts()
    {
        var pendingFacts = writableAggregateState.PendingProcedureFacts;
        foreach (var pending in pendingFacts.Take(8).ToArray())
        {
            var fact = new CharacterMedicalProcedureFact(pending.fact);
            var replies = new DomainFactDelivery[1];
            bool accepted;
            string failure;
            try
            {
                accepted = factRecovery.ReplayBatch(
                    new[] { DomainFactReplay.Create(pending.publicationKey, fact) }, replies, out failure);
            }
            catch (Exception exception)
            {
                ReportFactFailure("medical-procedure-recovery:" + exception.GetType().Name);
                continue;
            }
            if (accepted && replies[0].Completed && replies[0].OperationKey == pending.publicationKey)
            {
                pendingFacts.Remove(pending);
                lastFactFailure = string.Empty;
            }
            else ReportFactFailure("medical-procedure-recovery-pending:" + failure + ":" + replies[0].Detail);
        }
    }

    private void ReportFactFailure(string failure)
    {
        if (failure == lastFactFailure) return;
        lastFactFailure = failure;
        Debug.LogWarning("Character medical observation: " + failure);
    }

    private static void BeginMedicalLaborReservation(
        CharacterMedicalOrder order)
    {
        if (order == null)
        {
            throw new ArgumentNullException(nameof(order));
        }

        order.currentReservationStabilizationWork = 0f;
        order.currentReservationTreatmentWork = 0f;
        if (order.nextLaborCheckpointSequence <= 0)
        {
            // Additive compatibility for version-8 payloads written before
            // the checkpoint sequence was introduced.
            order.nextLaborCheckpointSequence = 1;
        }
    }

    private void RecordMedicalLabor(
        CharacterMedicalOrder order,
        CharacterActor rescuer,
        float acceptedWork,
        bool stabilization)
    {
        if (order == null
            || rescuer == null
            || acceptedWork <= 0f
            || !float.IsFinite(acceptedWork))
        {
            return;
        }

        float previous = stabilization
            ? order.currentReservationStabilizationWork
            : order.currentReservationTreatmentWork;
        float current = checked(previous + acceptedWork);
        long deltaMilliWu = checked(
            EmergencyWuUnits.FromWu(current)
            - EmergencyWuUnits.FromWu(previous));
        int nextSequence = checked(
            (stabilization
                ? order.stabilizationLaborSequence
                : order.treatmentLaborSequence) + 1);
        if (laborAccounting != null)
        {
            string procedure = stabilization
                ? "stabilization"
                : "treatment";
            EmergencyAccountingResult recorded = laborAccounting.Record(
                new SettlementLaborContribution(
                    $"medical-work:{order.orderId}:{procedure}",
                    nextSequence,
                    SettlementLaborContributionChannel.ActualLabor,
                    deltaMilliWu,
                    BuiltInWorkTypeIds.Treat.Value));
            if (!recorded.Success)
            {
                throw new InvalidOperationException(
                    $"{recorded.Code}: {recorded.Message}");
            }
        }

        if (stabilization)
        {
            order.stabilizationLaborSequence = nextSequence;
            order.currentReservationStabilizationWork = current;
        }
        else
        {
            order.treatmentLaborSequence = nextSequence;
            order.currentReservationTreatmentWork = current;
        }
    }

    private void PublishMedicalWorkCheckpoint(
        CharacterMedicalOrder order,
        CharacterActor rescuer,
        CharacterMedicalStatusCode releaseStatus)
    {
        if (order == null)
        {
            return;
        }

        float contribution = checked(
            order.currentReservationStabilizationWork
            + order.currentReservationTreatmentWork);
        if (contribution <= 0f)
        {
            return;
        }

        string rescuerId = rescuer != null
            ? CharacterPersistentIdentity.Require(rescuer).Value
            : order.rescuerId;
        if (!DomainFactValueSyntax.IsIdentity(rescuerId)
            || !DomainFactValueSyntax.IsIdentity(order.patientId))
        {
            throw new InvalidOperationException(
                "Medical work checkpoint requires canonical rescuer and patient IDs.");
        }

        int sequence = Math.Max(1, order.nextLaborCheckpointSequence);
        gameEventBus.Publish(new WorkCompletedIdentityEvent(
            new CharacterId(rescuerId),
            BuiltInWorkTypeIds.Treat.Value,
            "checkpoint:medical-work-suspended:"
                + releaseStatus.ToString().ToLowerInvariant(),
            CharacterCommandOrigin.Autonomous,
            Math.Max(0, calendar.Day),
            $"medical-work-checkpoint:{order.orderId}:{sequence}",
            WorkCompletionIdentityReferenceDefaults.CheckpointTerminalOwner,
            WorkCompletionIdentityReferenceDefaults.None,
            order.patientId,
            contribution));
        order.nextLaborCheckpointSequence = checked(sequence + 1);
        order.currentReservationStabilizationWork = 0f;
        order.currentReservationTreatmentWork = 0f;
    }

    private void ApplyExtractedBloodConsequences(
        CharacterActor patient,
        float infection,
        float instability)
    {
        if (patient == null)
        {
            return;
        }

        gameEventBus.Publish(new CharacterInfectionBurdenRequestedEvent(
            patient,
            infection));
        gameEventBus.Publish(
            new CharacterMentalInstabilityBurdenRequestedEvent(
                patient,
                instability));
        patient.ApplyMoodFactor(
            "medical:extracted-blood",
            "MedicalExtractedBloodDiscomfort",
            -4f,
            240f,
            1);
    }

    public bool TryReleaseReservation(
        string orderId,
        CharacterActor rescuer,
        CharacterMedicalStatusCode releaseStatus,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGetOrder(orderId, out CharacterMedicalOrder order))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalOrderMissing,
                orderId ?? string.Empty);
            return false;
        }

        if (rescuer != null
            && !string.Equals(order.rescuerId, GetId(rescuer), StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.CharacterMedicalReservationMismatch,
                orderId ?? string.Empty,
                GetId(rescuer));
            return false;
        }

        PublishMedicalWorkCheckpoint(order, rescuer, releaseStatus);

        if (order.carried && TryGetPatient(order, out CharacterActor patient))
        {
            if (order.state ==
                CharacterMedicalOrderState.MaterialDestinationDraining)
            {
                PreparePatientForUnassignedState(order, patient);
            }
            else
            {
                DropPatientAtCurrentPosition(order, patient, releaseStatus);
            }
        }

        order.rescuerId = string.Empty;
        if (order.IsActive
            && order.state !=
                CharacterMedicalOrderState.MaterialDestinationDraining)
        {
            order.state = order.stabilized
                ? CharacterMedicalOrderState.AwaitingRescue
                : CharacterMedicalOrderState.AwaitingStabilization;
            order.SetStatus(
                releaseStatus == CharacterMedicalStatusCode.Unknown
                    ? CharacterMedicalStatusCode.ReservationReleased
                    : releaseStatus);
        }

        return true;
    }

    public void NotifyCharacterDowned(CharacterActor actor)
    {
        string actorId = actor != null ? GetId(actor) : string.Empty;
        if (actor == null
            || actor.IsDead
            || (actor.characterType == CharacterType.Intruder
                && !carePriorityQuery.IsCareSubject(actorId)))
        {
            return;
        }

        if (!bodyHealthQuery.GetSnapshot(actor).Downed)
        {
            return;
        }

        string patientId = actorId;
        CharacterMedicalOrder existingOrder = aggregateState.Orders
            .FirstOrDefault(item =>
                item.IsActive
                && string.Equals(
                    item.patientId,
                    patientId,
                    StringComparison.Ordinal));
        int nextSequence = existingOrder == null
            ? TakeNextOrderSequencePreview()
            : 0;

        CancelCharacterActions(actor);
        actor.SetLifecycleState(CharacterLifecycleState.Downed);
        RegisterDownedOccupant(actor);

        CharacterMedicalOrder order = orders.FirstOrDefault(item =>
            item.IsActive
            && string.Equals(item.patientId, patientId, StringComparison.Ordinal));
        if (order == null)
        {
            orderSequence = nextSequence;
            order = new CharacterMedicalOrder
            {
                orderId = $"medical:{nextSequence}",
                patientId = patientId,
                state = CharacterMedicalOrderState.AwaitingStabilization,
                statusCode = CharacterMedicalStatusCode.AwaitingStabilization
            };
            orders.Add(order);
        }

        order.PatientPosition = actor.GetNowXY();
        order.requiredStabilizationWork = Mathf.Min(
            30f,
            8f + bodyHealthQuery.GetTotalBleeding(actor) * 40f);
        order.requiredTreatmentWork = CalculateTreatmentWork(actor);
        order.stabilized = bodyHealthQuery.GetTotalBleeding(actor) <= 0.001f;
        if (order.state ==
            CharacterMedicalOrderState.MaterialDestinationDraining)
        {
            TryDrainTreatmentDestination(
                order,
                order.stabilized
                    ? CharacterMedicalOrderState.AwaitingRescue
                    : CharacterMedicalOrderState.AwaitingStabilization,
                order.stabilized
                    ? CharacterMedicalStatusCode.AwaitingRescue
                    : CharacterMedicalStatusCode.AwaitingStabilization,
                Array.Empty<string>());
            RequestRescueReplans(actor);
            return;
        }
        order.state = order.stabilized
            ? CharacterMedicalOrderState.AwaitingRescue
            : CharacterMedicalOrderState.AwaitingStabilization;
        order.SetStatus(order.stabilized
            ? CharacterMedicalStatusCode.AwaitingRescue
            : CharacterMedicalStatusCode.AwaitingStabilization);
        RequestRescueReplans(actor);
    }

    private int TakeNextOrderSequencePreview()
    {
        if (orderSequence == int.MaxValue)
        {
            throw new InvalidOperationException(
                "Character-medical order sequence is exhausted.");
        }

        return checked(orderSequence + 1);
    }

    public void NotifyCharacterRecovered(CharacterActor actor)
    {
        if (actor == null || actor.IsDead)
        {
            return;
        }

        if (bodyHealthQuery.GetSnapshot(actor).Downed)
        {
            return;
        }

        string patientId = GetId(actor);
        foreach (CharacterMedicalOrder order in orders.Where(item =>
            item.IsActive
            && string.Equals(item.patientId, patientId, StringComparison.Ordinal)))
        {
            CompleteRecoveredOrder(order, actor);
        }

        RemoveDownedOccupant(patientId);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
    }

    public DungeonCharacterMedicalSaveData Capture()
    {
        return new DungeonCharacterMedicalSaveData
        {
            version = DungeonCharacterMedicalSaveData.CurrentVersion,
            orderSequence = orderSequence,
            orders = orders.Select(CharacterMedicalOrderPersistence.Clone).ToList(),
            pendingProcedureFacts = aggregateState.PendingProcedureFacts.Select(value => value.Clone()).ToList()
        };
    }

    private bool IsOrderAvailableTo(CharacterMedicalOrder order, CharacterActor rescuer)
    {
        if (order == null
            || !order.IsActive
            || order.carried
            || order.state ==
                CharacterMedicalOrderState.MaterialDestinationDraining)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(order.rescuerId)
            && !string.Equals(order.rescuerId, GetId(rescuer), StringComparison.Ordinal))
        {
            return false;
        }

        CharacterActor patient = FindCharacter(order.patientId);
        if (patient == null
            || patient.IsDead
            || !bodyHealthQuery.GetSnapshot(patient).Downed)
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
            && order.state !=
                CharacterMedicalOrderState.MaterialDestinationDraining
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
        if (order.state ==
            CharacterMedicalOrderState.MaterialDestinationDraining)
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

        if (!string.IsNullOrEmpty(order.treatmentMaterialDestinationId))
        {
            TryDrainTreatmentDestination(
                order,
                order.stabilized
                    ? CharacterMedicalOrderState.AwaitingBed
                    : CharacterMedicalOrderState.AwaitingStabilization,
                order.stabilized
                    ? CharacterMedicalStatusCode.AwaitingBed
                    : CharacterMedicalStatusCode.AwaitingStabilization,
                Array.Empty<string>());
            return false;
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
        return world.WorldRegistry.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && (building.BuildingData?.GetAbility<BuildingMedicalAbility>() != null
                    || building.BuildingData?.Abilities?
                        .OfType<ISurgicalFacilityAbility>()
                        .Any(ability => ability.IsPrimaryOperatingFacility) == true
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
        CharacterMedicalStatusCode releaseStatus)
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
        order.SetStatus(
            releaseStatus == CharacterMedicalStatusCode.Unknown
                ? CharacterMedicalStatusCode.AwaitingRescue
                : releaseStatus);
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

    private void CancelOrder(
        CharacterMedicalOrder order,
        CharacterMedicalStatusCode statusCode)
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
        order.rescuerId = string.Empty;
        TryDrainTreatmentDestination(
            order,
            CharacterMedicalOrderState.Cancelled,
            statusCode == CharacterMedicalStatusCode.Unknown
                ? CharacterMedicalStatusCode.Cancelled
                : statusCode,
            Array.Empty<string>());
        RemoveDownedOccupant(order.patientId);
    }

    private void RegisterDownedOccupant(CharacterActor actor)
    {
        if (actor == null
            || !world.GridProvider.TryGetGrid(out Grid grid))
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
            downedOccupants[id] =
                new CharacterMedicalDownedRegistration(
                    grid,
                    position,
                    occupant);
        }
    }

    private void RemoveDownedOccupant(string patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId)
            || !downedOccupants.TryGetValue(
                patientId,
                out CharacterMedicalDownedRegistration registration))
        {
            return;
        }

        downedOccupants.Remove(patientId);
        registration.Grid.RemoveOccupant(
            GridLayer.DownedCharacter,
            new[] { registration.Position },
            disconnectPositions: false);
    }

    private void CancelCharacterActions(CharacterActor actor)
    {
        actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
        string reasonCode = CharacterMedicalStatusCode.AwaitingStabilization.ToString();
        actor.GetAbility<AbilityWork>()?.StopAssignedWork(reasonCode);
        actor.GetComponent<AbilityHaul>()?.StopHauling(reasonCode);
        actor.GetComponent<AbilityHunt>()?.StopHunting(reasonCode);
        actor.Brain?.RequestImmediateReplan(clearFailures: true);
    }

    private void RequestRescueReplans(CharacterActor patient)
    {
        foreach (CharacterActor candidate in world.WorldRegistry.Characters)
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
        CharacterBodyHealthSnapshot snapshot = bodyHealthQuery.GetSnapshot(actor);
        return 20f
            + bodyHealthQuery.GetMissingPartHealth(actor) * 0.8f
            + snapshot.BloodLoss * 0.4f;
    }

    private CharacterActor FindCharacter(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return world.WorldRegistry.Characters.FirstOrDefault(actor =>
            actor != null && string.Equals(GetId(actor), id, StringComparison.Ordinal));
    }

    private static string GetId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor).Value
            : string.Empty;
    }

    private static string GetFacilityId(BuildableObject facility)
    {
        return facility == null
            ? string.Empty
            : facility.RequirePersistentInstanceId().Value;
    }

    private static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private void RecoverPendingMedicalDestinationDrainsOrThrow()
    {
        foreach (CharacterMedicalOrder order in orders
                     .Where(value => value != null
                         && value.state == CharacterMedicalOrderState
                             .MaterialDestinationDraining)
                     .OrderBy(value => value.orderId, StringComparer.Ordinal))
        {
            if (!supplyCoordinator.TryRecoverPendingSupply(
                    order,
                    out string supplyRecoveryFailure))
            {
                throw new InvalidOperationException(
                    "character-medical-supply-drain-sink-recovery-failed:"
                    + order.orderId + ":" + supplyRecoveryFailure);
            }
            CharacterMedicalSupplyDestinationDrainAdvanceResult resumed =
                supplyDestinationDrains.TryResume(order);
            if (resumed.Status ==
                CharacterMedicalSupplyDestinationDrainAdvanceStatus.Conflict)
            {
                throw new InvalidOperationException(
                    "character-medical-supply-drain-recovery-conflict:"
                    + order.orderId + ":" + resumed.FailureReason);
            }
            if (resumed.IsClosed)
            {
                FinalizeClosedDestinationDrain(
                    order,
                    resumed.ClosedFacilityId);
            }
        }
    }

    private CharacterMedicalSupplyDestinationDrainAdvanceResult
        TryDrainTreatmentDestination(
            CharacterMedicalOrder order,
            CharacterMedicalOrderState targetState,
            CharacterMedicalStatusCode targetStatus,
            IReadOnlyList<string> targetStatusParameters)
    {
        if (!supplyCoordinator.TryRecoverPendingSupply(
                order,
                out string supplyRecoveryFailure))
        {
            throw new InvalidOperationException(
                "character-medical-supply-drain-sink-recovery-failed:"
                + (order?.orderId ?? string.Empty) + ":"
                + supplyRecoveryFailure);
        }
        CharacterMedicalSupplyDestinationDrainAdvanceResult result =
            supplyDestinationDrains.TryBeginOrResume(
                order,
                targetState,
                targetStatus,
                targetStatusParameters);
        if (result.Status ==
            CharacterMedicalSupplyDestinationDrainAdvanceStatus.Conflict)
        {
            throw new InvalidOperationException(
                "character-medical-supply-destination-drain-conflict:"
                + (order?.orderId ?? string.Empty) + ":"
                + result.FailureReason);
        }
        if (result.IsClosed)
        {
            FinalizeClosedDestinationDrain(order, result.ClosedFacilityId);
        }
        return result;
    }

    private void FinalizeClosedDestinationDrain(
        CharacterMedicalOrder order,
        string closedFacilityId)
    {
        ReleaseFacilityReservationById(order, closedFacilityId);
        if (order == null)
        {
            return;
        }
        if (order.state is CharacterMedicalOrderState.Completed
            or CharacterMedicalOrderState.Cancelled)
        {
            RemoveDownedOccupant(order.patientId);
            return;
        }
        if (TryGetPatient(order, out CharacterActor patient))
        {
            RequestRescueReplans(patient);
        }
    }

    private void CompleteRecoveredOrder(
        CharacterMedicalOrder order,
        CharacterActor actor)
    {
        if (order == null || actor == null)
        {
            return;
        }
        RestorePatientParent(order.patientId, actor);
        order.carried = false;
        order.rescuerId = string.Empty;
        RemoveDownedOccupant(order.patientId);
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        TryDrainTreatmentDestination(
            order,
            CharacterMedicalOrderState.Completed,
            CharacterMedicalStatusCode.TreatmentCompleted,
            Array.Empty<string>());
    }

    private void PreparePatientForUnassignedState(
        CharacterMedicalOrder order,
        CharacterActor patient)
    {
        if (order == null || patient == null)
        {
            return;
        }
        order.carried = false;
        CharacterActor rescuer = FindCharacter(order.rescuerId);
        RestorePatientParent(order.patientId, patient);
        if (rescuer != null)
        {
            patient.transform.position = rescuer.transform.position;
            order.PatientPosition = rescuer.GetNowXY();
        }
        order.rescuerId = string.Empty;
        RegisterDownedOccupant(patient);
    }

    private void ReleaseFacilityReservationById(
        CharacterMedicalOrder order,
        string facilityId)
    {
        if (order == null || string.IsNullOrWhiteSpace(facilityId))
        {
            return;
        }
        if (treatmentFacilityReservations.TryGetValue(
                facilityId,
                out string patientId)
            && string.Equals(
                patientId,
                order.patientId,
                StringComparison.Ordinal))
        {
            treatmentFacilityReservations.Remove(facilityId);
        }
    }
}
