using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SurgeryRuntime :
    ISurgeryQuery,
    ISurgeryWorkCommand,
    ISurgeryPersistence,
    ISurgeryCommandService,
    ITickable
{
    private const float MaterialRefreshInterval = 0.75f;
    private const float AutomaticPolicyScanInterval = 1f;
    private const float RecoverySeconds = 10f;

    private readonly ISurgicalProcedureCatalog procedures;
    private readonly ISurgicalFacilityQuery facilities;
    private readonly ISurgeryRiskEvaluator riskEvaluator;
    private readonly ISurgicalPartRuntime parts;
    private readonly ISurgicalPartReplacementRuntime replacementParts;
    private readonly ISurgeryPolicyRuntime policies;
    private readonly ISurgicalCorpseFreshnessRuntime corpseFreshness;
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly ICaptivityRuntime captivity;
    private readonly IBuildingWorldQuery buildings;
    private readonly IWorldItemStackRuntime items;
    private readonly ISurgeryMaterialDestinationRuntime materialDestinations;
    private readonly ISurgeryMaterialTerminalRuntime materialTerminal;
    private readonly IFacilityBufferMassAdmissionService outputAdmission;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IWildlifeAnatomyHealthRuntime wildlifeAnatomy;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly ISurgicalPatientTransportRuntime patientTransport;
    private readonly IWorkforceReplanService workforce;
    private readonly IGameClock clock;
    private readonly IRandomStream outcomeRandom;
    private readonly IProcessFluidUseRuntime processFluids;
    private readonly ISurgeryEnvironmentRiskEvaluator
        environmentRiskEvaluator;
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly IRunSeedProvider runSeedProvider;
    private readonly CharacterIdentityEventPublisher identityEvents;
    private readonly ICharacterSpeciesCatalog speciesCatalog;
    private readonly ICharacterSpeciesQuery speciesRuntime;
    private readonly ICharacterPerformanceQuery performance;
    private readonly Dictionary<Type, ISurgicalProcedureEffectHandler> effectHandlers;
    private readonly SurgeryOrderPlanningService planning;
    private readonly SurgeryPersistence persistence;
    private readonly SurgeryEnvironmentRuntime surgeryEnvironment;
    private readonly SurgeryLogisticsRuntime surgeryLogistics;
    private readonly SurgeryAggregateStateStore stateStore;
    private float nextMaterialRefreshAt;
    private float nextAutomaticPolicyScanAt;

    private List<SurgeryOrder> orders => stateStore.State.Orders;
    private int orderSequence
    {
        get => stateStore.State.OrderSequence;
        set => stateStore.State.OrderSequence = value;
    }

    public SurgeryRuntime(
        SurgeryContentServices content,
        SurgeryWorldServices world,
        SurgeryResourceServices resources,
        SurgeryExecutionServices execution,
        SurgeryAggregateStateStore stateStore)
    {
        SurgeryContentServices requiredContent = content ?? throw new ArgumentNullException(nameof(content));
        SurgeryWorldServices requiredWorld = world ?? throw new ArgumentNullException(nameof(world));
        SurgeryResourceServices requiredResources = resources ?? throw new ArgumentNullException(nameof(resources));
        SurgeryExecutionServices requiredExecution = execution ?? throw new ArgumentNullException(nameof(execution));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
        procedures = requiredContent.Procedures;
        facilities = requiredContent.Facilities;
        riskEvaluator = requiredContent.Risk;
        parts = requiredContent.Parts;
        replacementParts = parts as ISurgicalPartReplacementRuntime
            ?? throw new ArgumentException(
                "Surgical part runtime lacks replacement authority.",
                nameof(content));
        policies = requiredContent.Policies;
        anatomyProfiles = requiredContent.AnatomyProfiles;
        speciesCatalog = requiredContent.Species;
        speciesRuntime = requiredContent.SpeciesRuntime;
        performance = requiredContent.Performance;
        effectHandlers = SurgeryRuntimeSupport.BuildEffectIndex(
            requiredContent.Effects);
        corpseFreshness = requiredWorld.CorpseFreshness;
        characters = requiredWorld.Characters;
        wildlife = requiredWorld.Wildlife;
        captivity = requiredWorld.Captivity;
        buildings = requiredWorld.Buildings;
        patientTransport = requiredWorld.PatientTransport;
        bodyHealth = requiredWorld.BodyHealthQuery;
        items = requiredResources.Items;
        materialDestinations = requiredResources.MaterialDestinations;
        materialTerminal = requiredResources.MaterialTerminal;
        outputAdmission = requiredResources.PlannedOutputAdmission;
        anatomy = requiredResources.Anatomy;
        wildlifeAnatomy = requiredResources.WildlifeAnatomy;
        workforce = requiredResources.Workforce;
        processFluids = requiredResources.ProcessFluids;
        clock = requiredExecution.Clock;
        outcomeRandom = requiredExecution.OutcomeRandom;
        environmentRiskEvaluator = requiredExecution.EnvironmentRisk;
        extremeTraits = requiredExecution.ExtremeTraits;
        runSeedProvider = requiredExecution.RunSeedProvider;
        identityEvents = requiredExecution.IdentityEvents;
        planning = new SurgeryOrderPlanningService(
            requiredContent,
            requiredWorld,
            requiredResources);
        persistence = new SurgeryPersistence(this.stateStore);
        surgeryEnvironment = new SurgeryEnvironmentRuntime(
            requiredContent,
            requiredWorld,
            requiredResources,
            requiredExecution);
        surgeryLogistics = new SurgeryLogisticsRuntime(
            requiredContent,
            requiredWorld,
            requiredResources,
            requiredExecution);
    }

    public IReadOnlyList<SurgeryOrder> ActiveOrders =>
        orders.Where(order => order != null && order.IsActive).ToArray();

    public void Tick()
    {
        if (clock.IsPaused || clock.DeltaTime <= 0f)
        {
            return;
        }

        bool refreshMaterials = clock.Time >= nextMaterialRefreshAt;
        if (refreshMaterials)
        {
            nextMaterialRefreshAt = clock.Time + MaterialRefreshInterval;
        }

        if (clock.Time >= nextAutomaticPolicyScanAt)
        {
            nextAutomaticPolicyScanAt = clock.Time + AutomaticPolicyScanInterval;
            TryScheduleAutomaticEmergencySurgery();
        }

        TryActivateQueuedOrders();

        foreach (SurgeryOrder order in orders
                     .Where(candidate => candidate != null && candidate.IsActive)
                     .ToArray())
        {
            if (!order.OwnsMaterialAuthority)
            {
                continue;
            }
            if (order.state == SurgeryOrderState.TerminalDraining)
            {
                DriveMaterialTerminal(
                    order,
                    order.materialTerminalTargetState);
                continue;
            }
            if (order.state == SurgeryOrderState.Recovering)
            {
                if (clock.Time >= order.recoveryUntil)
                {
                    DriveMaterialTerminal(
                        order,
                        SurgeryOrderState.Completed);
                }
                continue;
            }

            if (!string.IsNullOrWhiteSpace(order.doctorId))
            {
                CharacterActor assignedDoctor =
                    SurgicalSubjectResolver.FindCharacter(
                        characters,
                        order.doctorId);
                bool unavailable = assignedDoctor == null
                    || assignedDoctor.IsDead
                    || bodyHealth.GetSnapshot(assignedDoctor).Downed;
                if (unavailable)
                {
                    order.doctorId = string.Empty;
                    order.statusData.Set(
                        SurgeryStatusCode.DoctorReplacementRequested,
                        order.doctorId);
                    workforce.RequestOneWorkerToReplanFor(
                        BuiltInWorkTypeIds.Surgery,
                        forceInterrupt: true);
                }
            }

            bool hasFacility = TryResolveFacility(
                order.facilityId,
                out BuildableObject facility);
            bool hasProcedure = procedures.TryGet(
                order.procedureId,
                out SurgicalProcedureSO procedure);
            if (order.resultRolled && hasProcedure)
            {
                // Work, inputs, and RNG are already frozen. Only resume the
                // commit-forward effect/consequence boundary; assigning another
                // worker would charge labor for a result that already exists.
                ResolveOutcome(order, procedure, facility, out _);
                continue;
            }
            if (!hasFacility || !hasProcedure)
            {
                if (!order.resultRolled)
                {
                    TryBeginInternalCancellation(order);
                }
                else
                {
                    order.statusData.Set(
                        SurgeryStatusCode.ProcedurePaused,
                        "frozen-outcome-configuration-missing");
                }
                continue;
            }

            if (order.subject?.kind == SurgicalSubjectKind.Character)
            {
                CharacterActor patient =
                    SurgicalSubjectResolver.FindCharacter(
                        characters,
                        order.subject.subjectId);
                if (patient != null && patient.IsDead)
                {
                    TryBeginInternalCancellation(order);
                    continue;
                }
            }

            if (order.state == SurgeryOrderState.EnvironmentWaiting)
            {
                surgeryEnvironment.TickWaiting(order, facility);
                continue;
            }

            SurgicalFacilitySnapshot facilityState = facilities.Evaluate(
                facility,
                procedure);
            if (!facilityState.IsAvailable)
            {
                order.statusData.Set(
                    SurgeryStatusCode.FacilityUnavailable,
                    order.facilityId);
                continue;
            }

            if (refreshMaterials
                && IsClinicalStage(order.state)
                && !HasLiveDoctorWorkOwnership(order))
            {
                // A current-format restore intentionally does not serialize
                // transient AIWork/coroutine ownership. Keep the aggregate
                // doctor link, but re-admit the exact surgery through the
                // production workforce boundary once candidate publication is
                // ready. The same fence also repairs any other orphaned
                // clinical-stage action without duplicating a live owner.
                workforce.RequestOneWorkerToReplanFor(
                    BuiltInWorkTypeIds.Surgery,
                    clearFailures: true,
                    forceInterrupt: true);
            }

            if (refreshMaterials)
            {
                surgeryLogistics.RequestMissingMaterials(order, facility);
                bool processFluidReady = order.processFluidConsumed
                    || processFluids.EnsureCycleSupply(
                        facility,
                        BuiltInWorkTypeIds.Surgery,
                        out _);
                if (!processFluidReady)
                {
                    // EnsureCycleSupply is polled until the physical input arrives.
                    // Repeated destructive replans cancel the very AIHaul that owns
                    // the requested medicine/water lease, so subsequent polls are
                    // wake-up hints only. The request-creation boundary performs the
                    // one urgent interruption when new material is actually routed.
                    workforce.RequestOneHaulerToReplan(
                        clearFailures: true,
                        forceInterrupt: false);
                }
            }

            bool patientReady = surgeryLogistics.EnsureAdmission(order, facility);
            // Once the clinical action has consumed its physical materials,
            // the destination buffer is expected to be empty. Requiring the
            // already-consumed items to remain buffered would regress a
            // restored (or briefly interrupted) clinical stage back to
            // MaterialsWaiting and request the same inputs again.
            bool materialsReady = order.materialsConsumed
                || surgeryLogistics.AreRequiredMaterialsReady(order);
            if (!patientReady)
            {
                order.state = SurgeryOrderState.PatientWaiting;
                continue;
            }

            if (!materialsReady)
            {
                order.state = SurgeryOrderState.MaterialsWaiting;
                order.statusData.Set(
                    SurgeryStatusCode.MaterialsDeliveryPending);
                continue;
            }

            if (!order.processFluidConsumed
                && !processFluids.EnsureCycleSupply(
                    facility,
                    BuiltInWorkTypeIds.Surgery,
                    out _))
            {
                order.state = SurgeryOrderState.MaterialsWaiting;
                order.statusData.Set(
                    SurgeryStatusCode.ProcessFluidUnavailable);
                continue;
            }

            if (order.state is SurgeryOrderState.PatientWaiting
                or SurgeryOrderState.MaterialsWaiting)
            {
                order.state = SurgeryOrderState.Anesthetizing;
                order.statusData.Set(
                    procedure.RequiresAnesthesia
                        ? SurgeryStatusCode.AnesthesiaInProgress
                        : SurgeryStatusCode.PatientRestraintInProgress);
                workforce.RequestOneWorkerToReplanFor(
                    BuiltInWorkTypeIds.Surgery,
                    forceInterrupt: true);
            }
            else if (refreshMaterials
                && order.state == SurgeryOrderState.Anesthetizing
                && string.IsNullOrWhiteSpace(order.doctorId))
            {
                workforce.RequestOneWorkerToReplanFor(
                    BuiltInWorkTypeIds.Surgery,
                    forceInterrupt: true);
            }
        }
    }

    private bool HasLiveDoctorWorkOwnership(SurgeryOrder order)
    {
        if (order == null || string.IsNullOrWhiteSpace(order.doctorId))
        {
            return false;
        }

        CharacterActor assignedDoctor = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.doctorId);
        AbilityWork work = assignedDoctor?.GetComponent<AbilityWork>();
        return assignedDoctor?.Brain?.HasRunningWorkAction == true
            && work != null
            && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Surgery;
    }

    private static bool IsClinicalStage(SurgeryOrderState state) =>
        state is SurgeryOrderState.Anesthetizing
            or SurgeryOrderState.Incision
            or SurgeryOrderState.Procedure
            or SurgeryOrderState.Suturing;

    private void TryScheduleAutomaticEmergencySurgery()
    {
        foreach (CharacterActor actor in characters.Characters)
        {
            if (actor == null
                || actor.IsDead
                || actor.characterType != CharacterType.NPC)
            {
                continue;
            }

            SurgicalSubjectRef subject = SurgeryRuntimeSupport.CreateCharacterSubject(
                actor,
                automaticEmergencyDefault: true);
            if (!policies.IsAutomaticEmergencySurgeryEnabled(subject)
                || orders.Any(order => order != null
                    && order.IsActive
                    && string.Equals(
                        order.subject?.subjectId,
                        subject.subjectId,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(actor);
            if (!anatomyProfiles.TryGet(
                    snapshot.ProfileId,
                    out AnatomyProfileDefinition profile))
            {
                continue;
            }

            AnatomyNodeHealthState target = snapshot.Nodes
                .Where(node => node != null && !node.missing)
                .OrderByDescending(node => node.bleedingPerSecond)
                .FirstOrDefault(node => node.bleedingPerSecond >=
                    SurgeryEmergencyCauseRules.AcuteBleedingMinimum);
            string procedureId = "procedure:emergency-suture";

            if (target == null)
            {
                target = snapshot.Nodes
                    .Where(node => node != null && !node.missing)
                    .OrderByDescending(node => node.infection)
                    .FirstOrDefault(node => node.infection >=
                        SurgeryEmergencyCauseRules.AcuteInfectionMinimum);
                procedureId = "procedure:foreign-body-removal";
            }

            if (target == null)
            {
                target = snapshot.Nodes
                    .Where(node => node != null && !node.missing)
                    .Where(node => profile.TryGetNode(
                        node.nodeId,
                        out AnatomyNodeDefinition definition)
                        && definition.Removable
                        && !definition.Vital)
                    .OrderByDescending(node => node.infection)
                    .ThenBy(node => node.HealthRatio)
                    .FirstOrDefault(node =>
                        node.infection >=
                            SurgeryEmergencyCauseRules.CriticalInfectionMinimum
                        || node.HealthRatio <= SurgeryEmergencyCauseRules
                            .CriticalHealthRatioMaximum);
                procedureId = "procedure:amputation";
            }

            if (target == null
                && TryGetAutomaticMaintenanceSuggestion(
                    actor,
                    out string maintenanceProcedureId,
                    out string maintenanceTargetNodeId))
            {
                target = snapshot.Nodes
                    .FirstOrDefault(node => node != null
                        && string.Equals(
                            node.nodeId,
                            maintenanceTargetNodeId,
                            StringComparison.Ordinal));
                procedureId = maintenanceProcedureId;
            }

            if (target == null)
            {
                continue;
            }

            TrySchedule(
                subject,
                procedureId,
                target.nodeId,
                string.Empty,
                string.Empty,
                string.Empty,
                out _,
                out _);
        }
    }

    public bool TryGetAutomaticMaintenanceSuggestion(
        CharacterActor actor,
        out string procedureId,
        out string targetNodeId)
    {
        procedureId = string.Empty;
        targetNodeId = string.Empty;
        if (actor == null
            || actor.IsDead
            || !CharacterPersistentIdentity.TryGet(actor, out CharacterId characterId)
            || !speciesRuntime.TryGet(
                characterId,
                out CharacterSpeciesRuntimeState speciesState)
            || !speciesState.SpeciesId.Equals(new CharacterSpeciesId("Golem"))
            || speciesState.Integrity > 50f)
            return false;

        AnatomyNodeHealthState target = anatomy.GetAnatomySnapshot(actor).Nodes
            .Where(node => node != null && !node.missing)
            .OrderByDescending(node => node.rejectionBurden)
            .ThenBy(node => node.ConditionFactor)
            .ThenBy(node => node.nodeId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target == null)
            return false;
        procedureId = "procedure:golem-power-core";
        targetNodeId = target.nodeId;
        return true;
    }

    public bool TryGetOrder(string orderId, out SurgeryOrder order)
    {
        order = orders.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.orderId, orderId, StringComparison.Ordinal));
        return order != null;
    }

    public bool HasWorkFor(BuildableObject facility)
    {
        return TryGetWorkFor(facility, out _);
    }

    public bool TryGetWorkFor(
        BuildableObject facility,
        out SurgeryOrder order)
    {
        string facilityId = facilities.GetFacilityId(facility);
        order = orders
            .Where(candidate => candidate != null
                && candidate.IsActive
                && !candidate.resultRolled
                && candidate.state is SurgeryOrderState.Anesthetizing
                    or SurgeryOrderState.Incision
                    or SurgeryOrderState.Procedure
                    or SurgeryOrderState.Suturing
                && string.Equals(
                    candidate.facilityId,
                    facilityId,
                    StringComparison.Ordinal))
                .OrderByDescending(surgeryEnvironment.GetUrgency)
            .ThenBy(candidate => candidate.createdAt)
            .ThenBy(candidate => candidate.orderId, StringComparer.Ordinal)
            .FirstOrDefault();
        return order != null;
    }

    public bool CanOperate(
        SurgeryOrder order,
        CharacterActor doctor,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order == null || !order.IsActive)
        {
            failure = new DomainFailure(FailureCode.SurgeryOrderMissing);
            return false;
        }

        if (doctor == null || doctor.IsDead || !doctor.CanRunAi)
        {
            failure = new DomainFailure(FailureCode.SurgeryOperatorIneligible);
            return false;
        }

        if (!procedures.TryGet(order.procedureId, out SurgicalProcedureSO procedure))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryProcedureMissing,
                order.procedureId);
            return false;
        }

        return procedure.OperatorRequirement.IsQualified(
            doctor,
            procedure.Family,
            performance,
            out _,
            out failure);
    }

    public bool TryReserveWork(
        BuildableObject facility,
        CharacterActor doctor,
        out SurgeryOrder order,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (doctor == null || doctor.IsDead || !doctor.CanRunAi)
        {
            order = null;
            failure = new DomainFailure(FailureCode.SurgeryOperatorIneligible);
            return false;
        }

        string doctorId = doctor.Identity?.PersistentId ?? string.Empty;
        if (captivity.IsCaptive(doctorId)
            || doctor.characterType != CharacterType.NPC)
        {
            order = null;
            failure = new DomainFailure(FailureCode.SurgeryStaffOnly, doctorId);
            return false;
        }

        if (!TryGetWorkFor(facility, out order))
        {
            failure = new DomainFailure(FailureCode.SurgeryOrderMissing);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(order.preferredDoctorId)
            && !string.Equals(
                order.preferredDoctorId,
                doctor.Identity?.PersistentId,
                StringComparison.Ordinal))
        {
            order = null;
            failure = new DomainFailure(
                FailureCode.SurgeryPreferredDoctorOnly,
                order.preferredDoctorId);
            return false;
        }

        if (!string.IsNullOrWhiteSpace(order.doctorId)
            && !string.Equals(
                order.doctorId,
                doctor.Identity?.PersistentId,
                StringComparison.Ordinal))
        {
            order = null;
            failure = new DomainFailure(
                FailureCode.SurgeryDoctorAlreadyAssigned,
                order.doctorId);
            return false;
        }

        if (!procedures.TryGet(
                order.procedureId,
                out SurgicalProcedureSO procedure))
        {
            order = null;
            failure = new DomainFailure(
                FailureCode.SurgeryProcedureMissing,
                order.procedureId);
            return false;
        }

        if (!CanOperate(order, doctor, out failure))
        {
            order = null;
            return false;
        }

        if (!planning.ValidateSubject(
                order.subject,
                procedure,
                order.targetNodeId,
                out failure))
        {
            order = null;
            return false;
        }

        SurgicalFacilitySnapshot snapshot = facilities.Evaluate(
            facility,
            procedure);
        if (!snapshot.IsAvailable)
        {
            order = null;
            failure = new DomainFailure(
                FailureCode.SurgeryFacilityUnavailable,
                facilities.GetFacilityId(facility));
            return false;
        }

        order.doctorId = doctor.Identity?.PersistentId ?? string.Empty;
        order.risk = riskEvaluator.Evaluate(
            doctor,
            order.subject,
            procedure,
            snapshot,
            planning.ResolvePatientInstability(order.subject),
            planning.ResolveCompatibilityPenalty(order));
        SurgeryEnvironmentRiskSnapshot environmentRisk =
            environmentRiskEvaluator.Evaluate(
                facility.centerPos,
                doctor,
                order.subject);
        if (environmentRisk.Extreme
            && !surgeryEnvironment.IsEmergency(order))
        {
            surgeryEnvironment.EnterWait(
                order,
                SurgeryOrderState.Anesthetizing,
                environmentRisk);
            failure = new DomainFailure(
                FailureCode.SurgeryEnvironmentUnsafe,
                order.orderId);
            return false;
        }
        order.statusData.Set(SurgeryStatusCode.OperationStarted);
        return true;
    }

    public bool ApplyWork(
        string orderId,
        CharacterActor doctor,
        float work,
        out bool completed,
        out DomainFailure failure)
    {
        completed = false;
        failure = DomainFailure.None;
        if (!TryGetOrder(orderId, out SurgeryOrder order)
            || !order.IsActive)
        {
            failure = new DomainFailure(FailureCode.SurgeryOrderMissing, orderId);
            return false;
        }

        if (doctor == null
            || !string.Equals(
                order.doctorId,
                doctor.Identity?.PersistentId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryReservedDoctorMismatch,
                order.doctorId,
                doctor?.Identity?.PersistentId ?? string.Empty);
            return false;
        }

        if (!TryResolveFacility(order.facilityId, out BuildableObject facility)
            || !procedures.TryGet(order.procedureId, out SurgicalProcedureSO procedure))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryFacilityOrProcedureMissing,
                order.facilityId,
                order.procedureId);
            return false;
        }

        if (!ValidateCharacterNodeRemovalOwnership(
                order.subject,
                procedure,
                order.targetNodeId,
                out failure))
        {
            order.statusData.Set(
                SurgeryStatusCode.ProcedurePaused,
                failure.ToString());
            return false;
        }

        if (!order.processFluidConsumed
            && !processFluids.TryConsumeCycle(
                facility,
                BuiltInWorkTypeIds.Surgery,
                out DomainFailure processFluidFailure))
        {
            failure = processFluidFailure.IsFailure
                ? processFluidFailure
                : new DomainFailure(FailureCode.SurgeryMaterialUnavailable);
            order.statusData.Set(SurgeryStatusCode.ProcessFluidUnavailable);
            // Manual process-water fallback creates a physical delivery order.
            // Wake one hauler immediately; otherwise an urgent surgery can sit
            // paused indefinitely until an unrelated routine haul scan happens.
            workforce.RequestOneHaulerToReplan(
                clearFailures: true,
                forceInterrupt: false);
            return false;
        }

        order.processFluidConsumed = true;
        if (order.materialsConsumed
            && !surgeryLogistics.TryFinalizeConsumedMaterials(
                order,
                out failure))
        {
            return false;
        }
        if (!order.materialsConsumed
            && !surgeryLogistics.TryConsumeMaterials(order, out failure))
        {
            order.state = SurgeryOrderState.MaterialsWaiting;
            order.statusData.Set(SurgeryStatusCode.MaterialsDeliveryPending);
            return false;
        }

        if (order.state == SurgeryOrderState.EnvironmentWaiting)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryEnvironmentUnsafe,
                order.orderId);
            return false;
        }

        float stageBoundary = surgeryEnvironment.GetCurrentStageBoundary(order);
        SurgeryEnvironmentRiskSnapshot environmentRisk = default;
        bool waitAtBoundary = false;
        if (!surgeryEnvironment.IsEmergency(order))
        {
            environmentRisk = environmentRiskEvaluator.Evaluate(
                facility.centerPos,
                doctor,
                order.subject);
            waitAtBoundary = environmentRisk.Extreme;
            if (waitAtBoundary
                && order.completedWork + 0.001f >= stageBoundary)
            {
                surgeryEnvironment.ApplyCurrentStageRisk(
                    order,
                    doctor,
                    facility);
                surgeryEnvironment.EnterWait(
                    order,
                    surgeryEnvironment.GetNextClinicalStage(order.state),
                    environmentRisk);
                failure = new DomainFailure(
                    FailureCode.SurgeryEnvironmentUnsafe,
                    order.orderId);
                return false;
            }
        }

        float applied = Mathf.Max(0f, work);
        if (waitAtBoundary)
        {
            applied = Mathf.Min(
                applied,
                Mathf.Max(0f, stageBoundary - order.completedWork));
        }
        if (applied <= 0f)
        {
            return true;
        }

        order.completedWork = Mathf.Min(
            order.requiredWork,
            order.completedWork + applied);
        if (waitAtBoundary
            && order.completedWork + 0.001f >= stageBoundary
            && stageBoundary + 0.001f < order.requiredWork)
        {
            surgeryEnvironment.ApplyCurrentStageRisk(
                order,
                doctor,
                facility);
            surgeryEnvironment.EnterWait(
                order,
                surgeryEnvironment.GetNextClinicalStage(order.state),
                environmentRisk);
            failure = new DomainFailure(
                FailureCode.SurgeryEnvironmentUnsafe,
                order.orderId);
            return false;
        }

        UpdatePhase(order, procedure, doctor, facility);
        if (order.completedWork + 0.001f < order.requiredWork)
        {
            return true;
        }

        if (!planning.ValidateSubject(
                order.subject,
                procedure,
                order.targetNodeId,
                out failure))
        {
            order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
            return false;
        }

        if (!TryPrepareReplacementOutput(
                order,
                procedure,
                facility,
                out failure))
        {
            order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
            return false;
        }

        completed = ResolveOutcome(order, procedure, facility, out failure);
        order.doctorId = string.Empty;
        // The clinical outcome is now final even when the physical terminal
        // drain must continue on subsequent ticks. The doctor action must not
        // rerun the RNG/effect/consequence boundary while that drain recovers.
        return completed || order.resultRolled;
    }

    public void ReleaseDoctor(
        string orderId,
        CharacterActor doctor)
    {
        if (!TryGetOrder(orderId, out SurgeryOrder order)
            || doctor == null
            || !string.Equals(
                order.doctorId,
                doctor.Identity?.PersistentId,
                StringComparison.Ordinal))
        {
            return;
        }

        order.doctorId = string.Empty;
        if (order.incisionOpen && order.IsActive)
        {
            CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
                characters,
                order.subject?.subjectId);
            anatomy.TryDamageNode(
                patient,
                order.targetNodeId,
                1f,
                0.08f,
                SurgeryStatusCode.ProcedureInterruptedOpenWound.ToString());
            order.statusData.Set(
                SurgeryStatusCode.ProcedureInterruptedOpenWound,
                order.orderId);
        }
        else
        {
            // Preserve the actionable resource/environment reason produced by
            // the failed work tick. A generic pause label hid the missing-water
            // delivery and made both AI recovery and UI diagnosis opaque.
            if (order.statusData?.code is not SurgeryStatusCode.ProcessFluidUnavailable
                and not SurgeryStatusCode.MaterialsDeliveryPending
                and not SurgeryStatusCode.EnvironmentUnsafe)
            {
                order.statusData.Set(
                    SurgeryStatusCode.ProcedurePaused,
                    order.orderId);
            }
        }
    }

    public bool TrySchedule(
        SurgicalSubjectRef subject,
        string procedureId,
        string targetNodeId,
        string selectedPartInstanceId,
        string preferredDoctorId,
        string preferredFacilityId,
        out SurgeryOrder order,
        out DomainFailure failure)
    {
        order = null;
        failure = DomainFailure.None;
        if (subject == null || !subject.IsValid)
        {
            failure = new DomainFailure(FailureCode.SurgerySubjectInvalid);
            return false;
        }

        if (subject.kind == SurgicalSubjectKind.Character
            && !surgeryEnvironment.IsProcedureFamily(
                procedureId,
                MedicalProcedureFamily.Construct)
            && speciesCatalog.TryGet(
                new CharacterSpeciesId(subject.speciesId),
                out CharacterSpeciesSO subjectSpecies)
            && (subjectSpecies.needs?.UsesMaintenanceInsteadOfSurgery ?? false))
        {
            failure = new DomainFailure(
                FailureCode.SurgerySubjectMaintenanceOnly,
                subject.speciesId);
            return false;
        }

        if (!procedures.TryGet(procedureId, out SurgicalProcedureSO procedure))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryProcedureMissing,
                procedureId);
            return false;
        }

        string normalizedDoctorId = preferredDoctorId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(normalizedDoctorId))
        {
            CharacterActor preferredDoctor = characters.Characters.FirstOrDefault(
                candidate => candidate?.Identity != null
                    && string.Equals(
                        candidate.Identity.PersistentId,
                        normalizedDoctorId,
                        StringComparison.Ordinal));
            if (preferredDoctor != null
                && !procedure.OperatorRequirement.IsQualified(
                    preferredDoctor,
                    procedure.Family,
                    performance,
                    out _,
                    out failure))
            {
                return false;
            }
        }

        if (subject.kind == SurgicalSubjectKind.Character
            && !string.IsNullOrWhiteSpace(normalizedDoctorId)
            && string.Equals(
                subject.subjectId,
                normalizedDoctorId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgerySelfOperationForbidden);
            return false;
        }

        if (!planning.ValidateSubject(subject, procedure, targetNodeId, out failure)
            || !planning.ValidateResearch(procedure, out failure))
        {
            return false;
        }
        if (!ValidateCharacterNodeRemovalOwnership(
                subject,
                procedure,
                targetNodeId,
                out failure))
        {
            return false;
        }
        if (!ValidateReplacementScope(
                subject,
                procedure,
                targetNodeId,
                out failure))
        {
            return false;
        }

        if (orders.Any(candidate => candidate != null
            && candidate.IsActive
            && string.Equals(
                candidate.subject?.subjectId,
                subject.subjectId,
                StringComparison.Ordinal)))
        {
            failure = new DomainFailure(
                FailureCode.SurgerySubjectAlreadyScheduled,
                subject.subjectId);
            return false;
        }

        SurgicalFacilitySnapshot facility;
        if (!string.IsNullOrWhiteSpace(preferredFacilityId))
        {
            if (!TryResolveFacility(preferredFacilityId, out BuildableObject preferred))
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryFacilityMissing,
                    preferredFacilityId);
                return false;
            }

            facility = facilities.Evaluate(
                preferred,
                procedure);
            if (!facility.IsAvailable)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryFacilityUnavailable,
                    preferredFacilityId);
                return false;
            }
        }
        else if (!facilities.TryFindBestFacility(
                     subject,
                     procedure,
                     out facility,
                     out _))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryFacilityUnavailable);
            return false;
        }

        SurgeryAggregateState state = stateStore.State;
        if (!state.TryPrepareNextOrderIdentity(
                out int nextOrderSequence,
                out string id,
                out failure))
        {
            return false;
        }
        if (planning.RequiresInstalledPart(procedure))
        {
            if (!planning.ValidateSelectedPart(
                    subject,
                    procedure,
                    targetNodeId,
                    selectedPartInstanceId,
                    out failure)
                || !parts.TryReserveForOrder(
                    selectedPartInstanceId,
                    id,
                    out _))
            {
                if (!failure.IsFailure)
                {
                    failure = new DomainFailure(
                        FailureCode.SurgeryPartUnavailable,
                        selectedPartInstanceId);
                }
                return false;
            }
            if (subject.kind == SurgicalSubjectKind.Wildlife
                && parts.TryGet(
                    selectedPartInstanceId,
                    out SurgicalPartInstance recovered)
                && recovered.detachedDurabilityMaximum > 0f)
            {
                parts.ReleaseReservation(selectedPartInstanceId, id);
                failure = new DomainFailure(
                    FailureCode.SurgerySubjectKindUnsupported,
                    selectedPartInstanceId,
                    "recovered-part-reinstall-character-only");
                return false;
            }
        }

        bool facilityAlreadyOwned = orders.Any(candidate =>
            candidate?.IsActive == true
            && candidate.OwnsMaterialAuthority
            && string.Equals(
                candidate.facilityId,
                facilities.GetFacilityId(facility.PrimaryFacility),
                StringComparison.Ordinal));
        string normalizedTargetNodeId = string.IsNullOrWhiteSpace(targetNodeId)
            ? procedure.TargetNodeId
            : targetNodeId.Trim();
        order = new SurgeryOrder
        {
            orderId = id,
            procedureId = procedure.ProcedureId,
            emergencyCause = ClassifyEmergencyCause(
                subject,
                procedure,
                normalizedTargetNodeId),
            subject = subject.Clone(),
            targetNodeId = normalizedTargetNodeId,
            selectedPartInstanceId = selectedPartInstanceId?.Trim() ?? string.Empty,
            replacementExpectedOldPartId =
                CapturePlannedReplacementPartId(
                    subject,
                    procedure,
                    targetNodeId),
            preferredDoctorId = normalizedDoctorId,
            facilityId = facilities.GetFacilityId(facility.PrimaryFacility),
            materialDestinationId = facilityAlreadyOwned
                ? string.Empty
                : SurgeryMaterialDestinationAuthority.BuildDestinationId(id),
            state = SurgeryOrderState.PatientWaiting,
            requiredWork = procedure.RequiredWork,
            anesthesiaWork = procedure.RequiredWork * 0.15f,
            incisionWork = procedure.RequiredWork * 0.2f,
            procedureWork = procedure.RequiredWork * 0.5f,
            sutureWork = procedure.RequiredWork * 0.15f,
            materials = planning.BuildMaterials(subject, procedure, facility),
            statusData = new SurgeryStatusData
            {
                code = SurgeryStatusCode.PatientAdmissionWaiting
            },
            createdAt = clock.Time
        };
        if (!facilityAlreadyOwned
            && !materialDestinations.TryClaim(
                order,
                facility.PrimaryFacility,
                out string claimReason))
        {
            if (!string.IsNullOrWhiteSpace(order.selectedPartInstanceId))
            {
                parts.ReleaseReservation(
                    order.selectedPartInstanceId,
                    order.orderId);
            }

            failure = new DomainFailure(
                FailureCode.SurgeryMaterialUnavailable,
                "destination-authority:" + claimReason);
            order = null;
            return false;
        }
        orderSequence = nextOrderSequence;
        orders.Add(order);
        if (facilityAlreadyOwned)
        {
            return true;
        }
        surgeryLogistics.RequestMissingMaterials(order, facility.PrimaryFacility);
        surgeryLogistics.PrepareAdmission(order, facility.PrimaryFacility);
        // RequestMissingMaterials performs the single urgent handoff when it
        // creates a delivery. This second signal must not destroy that new haul
        // ownership in the same scheduling boundary.
        workforce.RequestOneHaulerToReplan(forceInterrupt: false);
        workforce.RequestOneWorkerToReplanFor(
            BuiltInWorkTypeIds.Surgery,
            forceInterrupt: true);
        return true;
    }

    public bool TryCancel(string orderId, out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (!TryGetOrder(orderId, out SurgeryOrder order) || !order.IsActive)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryOrderMissing,
                orderId);
            return false;
        }

        if (order.state == SurgeryOrderState.TerminalDraining)
        {
            if (order.materialTerminalTargetState !=
                SurgeryOrderState.Cancelled)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryOrderMissing,
                    orderId,
                    "terminal-drain-in-progress");
                return false;
            }
            DriveMaterialTerminal(order, SurgeryOrderState.Cancelled);
            return true;
        }

        if (order.resultRolled)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                order.orderId,
                "frozen-outcome-finalization-required");
            return false;
        }

        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReserved)
        {
            if (!TryAbortReplacementForCancellation(
                    order,
                    out string abortFailure))
            {
                failure = new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    order.orderId,
                    abortFailure);
                return false;
            }
        }
        else if (order.replacementPhase is
                 SurgicalPartReplacementPhase.BodyCommitted
                 or SurgicalPartReplacementPhase.OutputPublished
                 or SurgicalPartReplacementPhase.Completed)
        {
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                order.orderId,
                "replacement-commit-forward-required");
            return false;
        }

        if (!order.OwnsMaterialAuthority)
        {
            CancelQueuedOrder(order);
            return true;
        }

        BeginCancellation(order);
        return true;
    }

    private SurgeryEmergencyCause ClassifyEmergencyCause(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        string targetNodeId)
    {
        AnatomyHealthSnapshot snapshot;
        if (subject?.kind == SurgicalSubjectKind.Character)
        {
            CharacterActor actor = SurgicalSubjectResolver.FindCharacter(
                characters,
                subject.subjectId);
            if (actor == null)
            {
                return SurgeryEmergencyCause.None;
            }
            snapshot = anatomy.GetAnatomySnapshot(actor);
        }
        else if (subject?.kind == SurgicalSubjectKind.Wildlife)
        {
            WildlifeActor actor = SurgicalSubjectResolver.FindWildlife(
                wildlife,
                subject.subjectId);
            if (actor == null)
            {
                return SurgeryEmergencyCause.None;
            }
            snapshot = wildlifeAnatomy.GetAnatomySnapshot(actor);
        }
        else
        {
            return SurgeryEmergencyCause.None;
        }

        AnatomyNodeHealthState node = snapshot.Nodes.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.nodeId,
                targetNodeId,
                StringComparison.Ordinal));
        AnatomyProfileDefinition profile = anatomyProfiles.TryGet(
            snapshot.ProfileId,
            out AnatomyProfileDefinition runtimeProfile)
                ? runtimeProfile
                : anatomyProfiles.GetForSpecies(subject.speciesId);
        return node != null
            && profile != null
            && profile.TryGetNode(
                targetNodeId,
                out AnatomyNodeDefinition definition)
                    ? SurgeryEmergencyCauseRules.Classify(
                        procedure,
                        node,
                        definition)
                    : SurgeryEmergencyCause.None;
    }

    private bool ValidateCharacterNodeRemovalOwnership(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        string targetNodeId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (subject?.kind != SurgicalSubjectKind.Character
            || procedure?.Effects?.Any(effect =>
                effect is RemoveSurgicalNodeEffect) != true)
        {
            return true;
        }

        string nodeId = string.IsNullOrWhiteSpace(targetNodeId)
            ? procedure.TargetNodeId
            : targetNodeId.Trim();
        CharacterActor actor = SurgicalSubjectResolver.FindCharacter(
            characters,
            subject.subjectId);
        AnatomyNodeHealthState node = actor == null
            ? null
            : anatomy.GetAnatomySnapshot(actor).Nodes.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.nodeId,
                    nodeId,
                    StringComparison.Ordinal));
        if (node == null || string.IsNullOrWhiteSpace(node.installedPartId))
        {
            return true;
        }

        failure = new DomainFailure(
            FailureCode.SurgeryTargetNodeUnavailable,
            nodeId,
            "installed-part-removal-unsupported");
        return false;
    }

    private void TryActivateQueuedOrders()
    {
        SurgeryOrder[] queued = orders
            .Where(order => order?.IsActive == true
                && order.state == SurgeryOrderState.PatientWaiting
                && !order.HasAnyMaterialAuthority)
            .OrderByDescending(surgeryEnvironment.GetUrgency)
            .ThenBy(order => order.createdAt)
            .ThenBy(order => order.orderId, StringComparer.Ordinal)
            .ToArray();
        foreach (IGrouping<string, SurgeryOrder> facilityQueue in queued
                     .GroupBy(order => order.facilityId, StringComparer.Ordinal))
        {
            if (orders.Any(candidate => candidate?.IsActive == true
                    && candidate.OwnsMaterialAuthority
                    && string.Equals(
                        candidate.facilityId,
                        facilityQueue.Key,
                        StringComparison.Ordinal)))
            {
                continue;
            }

            SurgeryOrder order = facilityQueue.First();
            if (!TryResolveFacility(
                    order.facilityId,
                    out BuildableObject facility)
                || !procedures.TryGet(
                    order.procedureId,
                    out SurgicalProcedureSO procedure))
            {
                CancelQueuedOrder(order);
                continue;
            }
            SurgicalFacilitySnapshot facilityState = facilities.Evaluate(
                facility,
                procedure);
            if (!facilityState.IsAvailable)
            {
                order.statusData.Set(
                    SurgeryStatusCode.FacilityUnavailable,
                    order.facilityId);
                continue;
            }
            if (!string.IsNullOrWhiteSpace(order.selectedPartInstanceId)
                && !parts.TryValidateReservationForOrder(
                    order.selectedPartInstanceId,
                    order.orderId,
                    out DomainFailure reservationFailure))
            {
                order.statusData.Set(
                    SurgeryStatusCode.ProcedurePaused,
                    reservationFailure.ToString());
                CancelQueuedOrder(order);
                continue;
            }
            if (ProcedureInstallsPart(procedure)
                && order.subject?.kind == SurgicalSubjectKind.Character
                && !string.Equals(
                    CapturePlannedReplacementPartId(
                        order.subject,
                        procedure,
                        order.targetNodeId),
                    order.replacementExpectedOldPartId,
                    StringComparison.Ordinal))
            {
                order.statusData.Set(
                    SurgeryStatusCode.ProcedurePaused,
                    "replacement-expected-old-changed");
                CancelQueuedOrder(order);
                continue;
            }

            order.materialDestinationId =
                SurgeryMaterialDestinationAuthority.BuildDestinationId(
                    order.orderId);
            if (!materialDestinations.TryClaim(
                    order,
                    facility,
                    out string claimFailure))
            {
                ClearMaterialAuthority(order);
                order.statusData.Set(
                    SurgeryStatusCode.MaterialsDeliveryPending,
                    claimFailure);
                continue;
            }

            surgeryLogistics.RequestMissingMaterials(order, facility);
            surgeryLogistics.PrepareAdmission(order, facility);
            workforce.RequestOneHaulerToReplan(forceInterrupt: false);
            workforce.RequestOneWorkerToReplanFor(
                BuiltInWorkTypeIds.Surgery,
                forceInterrupt: true);
        }
    }

    private void CancelQueuedOrder(SurgeryOrder order)
    {
        if (order == null)
            return;
        if (!string.IsNullOrWhiteSpace(order.selectedPartInstanceId))
        {
            parts.ReleaseReservation(
                order.selectedPartInstanceId,
                order.orderId);
        }
        order.doctorId = string.Empty;
        order.state = SurgeryOrderState.Cancelled;
        order.statusData.Set(SurgeryStatusCode.Cancelled);
        order.replacementExpectedOldPartId = string.Empty;
        ClearMaterialAuthority(order);
    }

    private static void ClearMaterialAuthority(SurgeryOrder order)
    {
        order.materialDestinationId = string.Empty;
        order.materialBufferCapacityGrams = 0L;
        order.materialMassAuthorityRevision = 0L;
        order.materialCapacityFingerprint = string.Empty;
    }

    public DungeonSurgerySaveData Capture()
    {
        return persistence.Capture();
    }

    private void UpdatePhase(
        SurgeryOrder order,
        SurgicalProcedureSO procedure,
        CharacterActor doctor,
        BuildableObject facility)
    {
        float anesthesiaEnd = order.anesthesiaWork;
        float incisionEnd = anesthesiaEnd + order.incisionWork;
        float procedureEnd = incisionEnd + order.procedureWork;
        bool preserveEmergencyNotice = order.statusData?.code ==
            SurgeryStatusCode.EmergencyProcedureContinuing;
        if (surgeryEnvironment.RecordClinicalStage(
            order,
            SurgeryOrderState.Anesthetizing))
        {
            preserveEmergencyNotice = surgeryEnvironment.ApplyRisk(
                order,
                doctor,
                facility);
        }
        if (order.completedWork < anesthesiaEnd)
        {
            order.state = SurgeryOrderState.Anesthetizing;
            SetClinicalProgressStatus(
                order,
                procedure.RequiresAnesthesia
                    ? SurgeryStatusCode.AnesthesiaInProgress
                    : SurgeryStatusCode.PatientRestraintInProgress,
                preserveEmergencyNotice);
            return;
        }

        if (surgeryEnvironment.RecordClinicalStage(
                order,
                SurgeryOrderState.Incision))
        {
            preserveEmergencyNotice = surgeryEnvironment.ApplyRisk(
                order,
                doctor,
                facility);
        }
        order.incisionOpen = true;
        if (order.completedWork < incisionEnd)
        {
            order.state = SurgeryOrderState.Incision;
            SetClinicalProgressStatus(
                order,
                SurgeryStatusCode.IncisionInProgress,
                preserveEmergencyNotice);
            return;
        }

        if (surgeryEnvironment.RecordClinicalStage(
                order,
                SurgeryOrderState.Procedure))
        {
            preserveEmergencyNotice = surgeryEnvironment.ApplyRisk(
                order,
                doctor,
                facility);
        }
        if (order.completedWork < procedureEnd)
        {
            order.state = SurgeryOrderState.Procedure;
            SetClinicalProgressStatus(
                order,
                SurgeryStatusCode.ProcedureInProgress,
                preserveEmergencyNotice);
            return;
        }

        if (surgeryEnvironment.RecordClinicalStage(
                order,
                SurgeryOrderState.Suturing))
        {
            preserveEmergencyNotice = surgeryEnvironment.ApplyRisk(
                order,
                doctor,
                facility);
        }
        order.state = SurgeryOrderState.Suturing;
        SetClinicalProgressStatus(
            order,
            SurgeryStatusCode.SuturingInProgress,
            preserveEmergencyNotice);
    }

    private static void SetClinicalProgressStatus(
        SurgeryOrder order,
        SurgeryStatusCode status,
        bool preserveEmergencyNotice)
    {
        if (!preserveEmergencyNotice)
        {
            order.statusData.Set(status);
        }
    }

    private bool ValidateReplacementScope(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        string targetNodeId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (procedure == null
            || subject?.kind != SurgicalSubjectKind.Wildlife
            || !ProcedureInstallsPart(procedure))
        {
            return true;
        }
        string nodeId = string.IsNullOrWhiteSpace(targetNodeId)
            ? procedure.TargetNodeId
            : targetNodeId.Trim();
        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            wildlife,
            subject.subjectId);
        AnatomyNodeHealthState node = animal == null
            ? null
            : wildlifeAnatomy.GetAnatomySnapshot(animal)
                .Nodes.FirstOrDefault(value => value != null
                    && string.Equals(
                        value.nodeId,
                        nodeId,
                        StringComparison.Ordinal));
        if (node == null
            || node.missing
            || string.IsNullOrWhiteSpace(node.installedPartId))
            return true;
        failure = new DomainFailure(
            FailureCode.SurgerySubjectKindUnsupported,
            nodeId,
            "replacement-recovery-character-only");
        return false;
    }

    private static bool ProcedureInstallsPart(SurgicalProcedureSO procedure) =>
        (procedure?.Effects ?? Array.Empty<SurgicalProcedureEffect>())
        .OfType<InstallSurgicalPartEffect>()
        .Any();

    private string CapturePlannedReplacementPartId(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        string targetNodeId)
    {
        if (subject?.kind != SurgicalSubjectKind.Character
            || !ProcedureInstallsPart(procedure))
        {
            return string.Empty;
        }
        string nodeId = string.IsNullOrWhiteSpace(targetNodeId)
            ? procedure.TargetNodeId
            : targetNodeId.Trim();
        return anatomy.GetAnatomySnapshot(subject.subjectId).Nodes
            .FirstOrDefault(node => node != null
                && !node.missing
                && string.Equals(
                    node.nodeId,
                    nodeId,
                    StringComparison.Ordinal))
            ?.installedPartId?.Trim() ?? string.Empty;
    }

    private bool TryPrepareReplacementOutput(
        SurgeryOrder order,
        SurgicalProcedureSO procedure,
        BuildableObject facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (order.replacementPhase != SurgicalPartReplacementPhase.None)
            return true;
        if (!ProcedureInstallsPart(procedure))
            return true;
        if (!ValidateReplacementScope(
                order.subject,
                procedure,
                order.targetNodeId,
                out failure))
        {
            return false;
        }
        if (order.subject?.kind != SurgicalSubjectKind.Character)
        {
            return true;
        }

        CharacterActor character = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        AnatomyNodeHealthState current = anatomy
            .GetAnatomySnapshot(character)
            .Nodes.FirstOrDefault(node => node != null
                && string.Equals(
                    node.nodeId,
                    order.targetNodeId,
                    StringComparison.Ordinal));
        if (current == null)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeMissing,
                order.targetNodeId);
            return false;
        }
        if (current.missing
            || string.IsNullOrWhiteSpace(current.installedPartId))
        {
            return true;
        }
        if (!string.Equals(
                current.installedPartId,
                order.replacementExpectedOldPartId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                order.replacementExpectedOldPartId,
                current.installedPartId);
            return false;
        }
        return replacementParts.TryReserveReplacementOutput(
            order,
            current,
            facility.centerPos,
            outputAdmission,
            out failure);
    }

    private bool ResolveOutcome(
        SurgeryOrder order,
        SurgicalProcedureSO procedure,
        BuildableObject facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        CharacterActor doctor = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.doctorId);
        if (!order.resultRolled)
        {
            IReadOnlyList<SurgicalProcedureEffect> pendingEffects =
                procedure.Effects ?? Array.Empty<SurgicalProcedureEffect>();
            foreach (SurgicalProcedureEffect pendingEffect in pendingEffects)
            {
                if (pendingEffect == null
                    || !effectHandlers.TryGetValue(
                        pendingEffect.GetType(),
                        out ISurgicalProcedureEffectHandler pendingHandler))
                {
                    failure = new DomainFailure(
                        FailureCode.SurgeryEffectHandlerMissing,
                        pendingEffect?.GetType().Name ?? string.Empty);
                    order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
                    return false;
                }
                if (!pendingHandler.CanApply(
                        order,
                        pendingEffect,
                        out failure))
                {
                    order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
                    return false;
                }
            }

            bool critical = procedure.Urgency ==
                    MedicalProcedureUrgency.Emergency
                || order.risk.deathChance >= .15f
                || order.risk.successChance <= .50f;
            ExtremeRiskResolution extremeResolution = default;
            bool extremeResolved = extremeTraits != null
                && runSeedProvider != null
                && doctor != null
                && extremeTraits.TryResolveMiracleSurgery(
                    doctor,
                    order.orderId,
                    critical,
                    unchecked((ulong)(uint)runSeedProvider.RunSeed),
                    clock.Time,
                    out extremeResolution);
            bool forcedMiracle = extremeResolved
                && extremeResolution.Outcome == ExtremeRiskOutcome.Miracle;
            bool forcedComplication = extremeResolved
                && extremeResolution.Outcome ==
                    ExtremeRiskOutcome.Complication;
            order.resultSucceeded = forcedMiracle
                || (!forcedComplication
                    && outcomeRandom.NextFloat() <= order.risk.successChance);
            if (!order.resultSucceeded)
            {
                if (forcedComplication)
                {
                    order.failureSeverity = SurgeryFailureSeverity.Major;
                }
                else
                {
                    float severityRoll = outcomeRandom.NextFloat();
                    order.failureSeverity = severityRoll < 0.6f
                        ? SurgeryFailureSeverity.Minor
                        : severityRoll < 0.9f
                            ? SurgeryFailureSeverity.Major
                            : SurgeryFailureSeverity.Fatal;
                }
            }
            order.resultOutcomeId = forcedMiracle
                ? "miracle"
                : forcedComplication
                    ? "severe-complication"
                    : order.resultSucceeded ? "success" : "failure";
            order.resultRolled = true;
        }

        if (order.resultSucceeded)
        {
            IReadOnlyList<SurgicalProcedureEffect> effects =
                procedure.Effects ?? Array.Empty<SurgicalProcedureEffect>();
            if (order.resolvedEffectCount < 0
                || order.resolvedEffectCount > effects.Count)
            {
                failure = new DomainFailure(
                    FailureCode.SurgeryEffectFailed,
                    order.orderId,
                    "resolved-effect-count-invalid");
                return false;
            }
            for (int index = order.resolvedEffectCount;
                 index < effects.Count;
                 index++)
            {
                SurgicalProcedureEffect effect = effects[index];
                if (effect == null
                    || !effectHandlers.TryGetValue(
                        effect.GetType(),
                        out ISurgicalProcedureEffectHandler handler))
                {
                    failure = new DomainFailure(
                        FailureCode.SurgeryEffectHandlerMissing,
                        effect?.GetType().Name ?? string.Empty);
                    order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
                    return false;
                }

                if (!handler.Apply(
                        order,
                        effect,
                        facility,
                        out failure))
                {
                    order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
                    return false;
                }
                order.resolvedEffectCount = index + 1;
            }

            order.failureSeverity = SurgeryFailureSeverity.None;
            order.incisionOpen = false;
            order.state = SurgeryOrderState.Recovering;
            CharacterActor recoveringPatient =
                SurgicalSubjectResolver.FindCharacter(
                    characters,
                    order.subject?.subjectId);
            float recoveryDurationMultiplier = recoveringPatient?.Stats != null
                ? recoveringPatient.Stats.GetDetailedStatMultiplier(
                    "medical:aftermath-duration")
                : 1f;
            order.recoveryUntil = clock.Time
                + RecoverySeconds * Mathf.Max(0f, recoveryDurationMultiplier);
            order.statusData.Set(SurgeryStatusCode.RecoveryObservation);
            PublishSurgeryWorkCompleted(
                doctor,
                order,
                procedure,
                order.resultOutcomeId);
            return true;
        }

        if (!replacementParts.TryAbortReplacement(
                order,
                outputAdmission,
                out string abortFailure))
        {
            order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                order.orderId,
                abortFailure);
            return false;
        }
        if (!order.outcomeConsequencesApplied)
        {
            if (!TryApplyFailureConsequences(order, out failure))
            {
                order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
                return false;
            }
            order.outcomeConsequencesApplied = true;
        }
        order.incisionOpen = false;
        order.statusData.Set(order.failureSeverity switch
        {
            SurgeryFailureSeverity.Minor =>
                SurgeryStatusCode.CompletedWithMinorFailure,
            SurgeryFailureSeverity.Major =>
                SurgeryStatusCode.CompletedWithMajorFailure,
            SurgeryFailureSeverity.Fatal => SurgeryStatusCode.FailedFatal,
            _ => SurgeryStatusCode.CompletedWithMajorFailure
        });
        DriveMaterialTerminal(order, SurgeryOrderState.Failed);
        failure = new DomainFailure(
            FailureCode.SurgeryOutcomeFailed,
            order.failureSeverity.ToString());
        PublishSurgeryWorkCompleted(
            doctor,
            order,
            procedure,
            order.resultOutcomeId);
        return false;
    }

    private void PublishSurgeryWorkCompleted(
        CharacterActor doctor,
        SurgeryOrder order,
        SurgicalProcedureSO procedure,
        string outcomeId)
    {
        if (identityEvents == null
            || doctor == null
            || !CharacterPersistentIdentity.TryGet(doctor, out CharacterId id))
            return;
        identityEvents.Publish(new WorkCompletedIdentityEvent(
            id,
            $"surgery:{procedure?.ProcedureId ?? order.procedureId}",
            outcomeId,
            CharacterCommandOrigin.Autonomous,
            Mathf.Max(0, Mathf.FloorToInt(clock.Time / GameCalendarRules.SecondsPerDay))));
    }

    private bool TryApplyFailureConsequences(
        SurgeryOrder order,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject?.subjectId);
        if (patient == null)
        {
            WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
                wildlife,
                order.subject?.subjectId);
            if (animal != null && animal.IsAlive)
            {
                if (order.outcomeConsequenceStep < 2)
                {
                    int damage = order.failureSeverity switch
                    {
                        SurgeryFailureSeverity.Minor =>
                            Mathf.CeilToInt(animal.MaxHealth * 0.1f),
                        SurgeryFailureSeverity.Major =>
                            Mathf.CeilToInt(animal.MaxHealth * 0.35f),
                        SurgeryFailureSeverity.Fatal => animal.CurrentHealth,
                        _ => 0
                    };
                    animal.ApplyDamage(damage, null);
                    order.outcomeConsequenceStep = 2;
                }
                return true;
            }
            failure = new DomainFailure(
                FailureCode.SurgeryLivingSubjectUnavailable,
                order.subject?.subjectId ?? string.Empty,
                "failure-consequence-subject-missing");
            return false;
        }

        switch (order.failureSeverity)
        {
            case SurgeryFailureSeverity.Minor:
                if (order.outcomeConsequenceStep < 1)
                {
                    if (!anatomy.TryDamageNode(
                            patient,
                            order.targetNodeId,
                            3f,
                            0.08f,
                            SurgeryStatusCode.CompletedWithMinorFailure
                                .ToString()))
                    {
                        failure = new DomainFailure(
                            FailureCode.SurgeryEffectFailed,
                            order.targetNodeId,
                            "failure-damage-not-applied");
                        return false;
                    }
                    order.outcomeConsequenceStep = 1;
                }
                if (order.outcomeConsequenceStep < 2)
                {
                    if (!anatomy.TryAddNodeBurden(
                            patient,
                            order.targetNodeId,
                            0f,
                            0f,
                            order.risk.infectionChance * 20f,
                            out failure))
                    {
                        return false;
                    }
                    order.outcomeConsequenceStep = 2;
                }
                return true;
            case SurgeryFailureSeverity.Major:
                if (order.outcomeConsequenceStep < 1)
                {
                    if (!anatomy.TryDamageNode(
                            patient,
                            order.targetNodeId,
                            10f,
                            0.25f,
                            SurgeryStatusCode.CompletedWithMajorFailure
                                .ToString()))
                    {
                        failure = new DomainFailure(
                            FailureCode.SurgeryEffectFailed,
                            order.targetNodeId,
                            "failure-damage-not-applied");
                        return false;
                    }
                    order.outcomeConsequenceStep = 1;
                }
                if (order.outcomeConsequenceStep < 2)
                {
                    if (!anatomy.TryAddNodeBurden(
                            patient,
                            order.targetNodeId,
                            5f,
                            0f,
                            order.risk.infectionChance * 35f,
                            out failure))
                    {
                        return false;
                    }
                    order.outcomeConsequenceStep = 2;
                }
                return true;
            case SurgeryFailureSeverity.Fatal:
                if (order.outcomeConsequenceStep < 2)
                {
                    patient.Die(
                        CharacterDeathCauseCode.MedicalProcedureFailure,
                        "surgery:fatal-procedure-failure");
                    order.outcomeConsequenceStep = 2;
                }
                return true;
            default:
                failure = new DomainFailure(
                    FailureCode.SurgeryEffectFailed,
                    order.orderId,
                    "failure-severity-invalid");
                return false;
        }
    }

    private void BeginCancellation(SurgeryOrder order)
    {
        if (order == null)
        {
            return;
        }
        if (order.replacementPhase == SurgicalPartReplacementPhase.None
            && !TryAbortReplacementForCancellation(
                order,
                out string replacementFailure))
        {
            throw new InvalidOperationException(
                $"Could not clear surgery replacement intent for "
                + $"'{order.orderId}': {replacementFailure}");
        }

        order.statusData.Set(SurgeryStatusCode.Cancelled);
        order.doctorId = string.Empty;
        DriveMaterialTerminal(order, SurgeryOrderState.Cancelled);
    }

    private bool TryBeginInternalCancellation(SurgeryOrder order)
    {
        if (order == null)
            return false;
        if (order.resultRolled)
        {
            order.statusData.Set(
                SurgeryStatusCode.ProcedurePaused,
                "frozen-outcome-finalization-required");
            return false;
        }
        if (order.replacementPhase ==
            SurgicalPartReplacementPhase.OutputReserved)
        {
            if (!TryAbortReplacementForCancellation(
                    order,
                    out string abortFailure))
            {
                order.statusData.Set(
                    SurgeryStatusCode.ProcedurePaused,
                    abortFailure);
                return false;
            }
        }
        else if (order.replacementPhase is
                 SurgicalPartReplacementPhase.BodyCommitted
                 or SurgicalPartReplacementPhase.OutputPublished
                 or SurgicalPartReplacementPhase.Completed)
        {
            order.statusData.Set(
                SurgeryStatusCode.ProcedurePaused,
                "replacement-commit-forward-required");
            return false;
        }

        BeginCancellation(order);
        return true;
    }

    private bool TryAbortReplacementForCancellation(
        SurgeryOrder order,
        out string failureReason)
    {
        string expectedOldPartId = order.replacementExpectedOldPartId;
        if (!replacementParts.TryAbortReplacement(
                order,
                outputAdmission,
                out failureReason))
        {
            return false;
        }

        // Material authority was claimed with the planned old part in its
        // capacity fingerprint. Keep that projection stable until revoke.
        if (order.OwnsMaterialAuthority)
        {
            order.replacementExpectedOldPartId = expectedOldPartId;
        }
        return true;
    }

    private bool DriveMaterialTerminal(
        SurgeryOrder order,
        SurgeryOrderState terminalTarget)
    {
        if (order == null)
        {
            return false;
        }
        if (order?.materialsConsumed == true
            && !surgeryLogistics.TryFinalizeConsumedMaterials(
                order,
                out DomainFailure sinkFailure))
        {
            throw new InvalidOperationException(
                $"Could not finalize surgery material sink for "
                + $"'{order.orderId}': {sinkFailure}");
        }

        SurgeryMaterialTerminalAdvanceResult result =
            materialTerminal.TryBeginOrResume(order, terminalTarget);
        if (result.Status == SurgeryMaterialTerminalAdvanceStatus.Conflict)
        {
            throw new InvalidOperationException(
                $"Could not close surgery material destination "
                + $"'{order.materialDestinationId}': {result.FailureReason}");
        }
        if (!result.IsReadyForOwnerClosure)
        {
            return false;
        }

        FinalizeMaterialTerminalOwner(order, terminalTarget);
        return true;
    }

    private void FinalizeMaterialTerminalOwner(
        SurgeryOrder order,
        SurgeryOrderState terminalTarget)
    {
        if (!string.IsNullOrWhiteSpace(order.selectedPartInstanceId)
            && terminalTarget != SurgeryOrderState.Completed)
        {
            parts.ReleaseReservation(
                order.selectedPartInstanceId,
                order.orderId);
        }
        if (order.replacementPhase == SurgicalPartReplacementPhase.Completed
            && !string.IsNullOrWhiteSpace(
                order.replacementExpectedOldPartId))
        {
            parts.ReleaseReservation(
                order.replacementExpectedOldPartId,
                order.orderId);
        }

        order.doctorId = string.Empty;
        order.state = terminalTarget;
        if (order.replacementPhase == SurgicalPartReplacementPhase.None)
        {
            order.replacementExpectedOldPartId = string.Empty;
        }
        switch (terminalTarget)
        {
            case SurgeryOrderState.Completed:
                order.statusData.Set(SurgeryStatusCode.RecoveryCompleted);
                break;
            case SurgeryOrderState.Cancelled:
                order.statusData.Set(SurgeryStatusCode.Cancelled);
                break;
        }
        ReleasePatient(order);
        order.materialTerminalDrainPhase =
            SurgeryMaterialTerminalDrainPhase.ClosedAwaitingCheckpointGc;
    }

    private void ReleasePatient(SurgeryOrder order)
    {
        if (order?.subject?.kind == SurgicalSubjectKind.Wildlife)
        {
            patientTransport.RequestWildlifeReturn(order);
            return;
        }

        if (order?.subject?.kind != SurgicalSubjectKind.Character)
        {
            return;
        }

        CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        if (patient == null)
        {
            return;
        }

        if (!order.subject.willing
            && captivity.TryGetCaptive(
                order.subject.subjectId,
                out CaptiveState captive)
            && captive.IsInCustody
            && !patient.IsDead
            && !bodyHealth.GetSnapshot(patient).Downed
            && (order.patientAdmitted || order.admissionMoveRequested))
        {
            Vector2Int returnPosition = captive.housingPosition;
            if (returnPosition == default)
            {
                returnPosition = new Vector2Int(
                    order.patientOriginX,
                    order.patientOriginY);
            }

            AbilityMove move = patient.GetAbility<AbilityMove>();
            if (move != null
                && patient.GetNowXY() != returnPosition
                && move.TryStartSystemMove(
                    returnPosition,
                    DoorAccessOverrideKind.EscortPass,
                    out _))
            {
                patient.Brain?.SetActionPhase(
                    SurgeryStatusCode.PrisonReturnInProgress.ToString(),
                    null);
                if (!order.subjectAiWasPaused)
                {
                    patient.SetAiPaused(false);
                }

                order.statusData.Set(SurgeryStatusCode.PrisonReturnInProgress);
                order.patientAdmitted = false;
                return;
            }
        }

        if (!order.subjectAiWasPaused)
        {
            patient.SetAiPaused(false);
        }

        patient.Brain?.SetActionPhase(
            (order.state == SurgeryOrderState.Completed
                ? SurgeryStatusCode.RecoveryCompleted
                : SurgeryStatusCode.Completed).ToString(),
            null);
        order.patientAdmitted = false;
    }

    private bool TryResolveFacility(
        string facilityId,
        out BuildableObject facility)
    {
        facility = buildings.Buildings.FirstOrDefault(candidate =>
            candidate != null
            && !candidate.isDestroy
            && string.Equals(
                facilities.GetFacilityId(candidate),
                facilityId,
                StringComparison.Ordinal));
        return facility != null;
    }
}
