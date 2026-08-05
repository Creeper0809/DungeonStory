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
    private readonly ISurgeryPolicyRuntime policies;
    private readonly ISurgicalCorpseFreshnessRuntime corpseFreshness;
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly ICaptivityRuntime captivity;
    private readonly IBuildingWorldQuery buildings;
    private readonly IWorldItemStackRuntime items;
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
    private readonly ICharacterSpeciesCatalog speciesCatalog;
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
        policies = requiredContent.Policies;
        anatomyProfiles = requiredContent.AnatomyProfiles;
        speciesCatalog = requiredContent.Species;
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
        anatomy = requiredResources.Anatomy;
        wildlifeAnatomy = requiredResources.WildlifeAnatomy;
        workforce = requiredResources.Workforce;
        processFluids = requiredResources.ProcessFluids;
        clock = requiredExecution.Clock;
        outcomeRandom = requiredExecution.OutcomeRandom;
        environmentRiskEvaluator = requiredExecution.EnvironmentRisk;
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

        foreach (SurgeryOrder order in orders
                     .Where(candidate => candidate != null && candidate.IsActive)
                     .ToArray())
        {
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

            if (!TryResolveFacility(order.facilityId, out BuildableObject facility)
                || !procedures.TryGet(order.procedureId, out SurgicalProcedureSO procedure))
            {
                CancelInternal(order);
                continue;
            }

            if (order.state == SurgeryOrderState.EnvironmentWaiting)
            {
                surgeryEnvironment.TickWaiting(order, facility);
                continue;
            }

            SurgicalFacilitySnapshot facilityState = facilities.Evaluate(
                facility,
                procedure.RequiredFacilityTags);
            if (!facilityState.IsAvailable)
            {
                order.statusData.Set(
                    SurgeryStatusCode.FacilityUnavailable,
                    order.facilityId);
                continue;
            }

            if (order.state == SurgeryOrderState.Recovering)
            {
                if (clock.Time >= order.recoveryUntil)
                {
                    order.state = SurgeryOrderState.Completed;
                    order.statusData.Set(SurgeryStatusCode.RecoveryCompleted);
                    ReleasePatient(order);
                }

                continue;
            }

            if (refreshMaterials)
            {
                surgeryLogistics.RequestMissingMaterials(order, facility);
            }

            bool patientReady = surgeryLogistics.EnsureAdmission(order, facility);
            bool materialsReady = surgeryLogistics.AreRequiredMaterialsReady(order);
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
                .FirstOrDefault(node => node.bleedingPerSecond >= 0.08f);
            string procedureId = "procedure:emergency-suture";

            if (target == null)
            {
                target = snapshot.Nodes
                    .Where(node => node != null && !node.missing)
                    .OrderByDescending(node => node.infection)
                    .FirstOrDefault(node => node.infection >= 35f);
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
                        node.infection >= 80f || node.HealthRatio <= 0.01f);
                procedureId = "procedure:amputation";
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

        SurgicalFacilitySnapshot snapshot = facilities.Evaluate(
            facility,
            procedure.RequiredFacilityTags);
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

        if (!order.processFluidConsumed
            && !processFluids.TryConsumeCycle(
                facility,
                BuiltInWorkTypeIds.Surgery,
                out _))
        {
            failure = new DomainFailure(FailureCode.SurgeryMaterialUnavailable);
            order.statusData.Set(SurgeryStatusCode.ProcessFluidUnavailable);
            return false;
        }

        order.processFluidConsumed = true;
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

        order.resultRolled = true;
        completed = ResolveOutcome(order, procedure, facility, out failure);
        order.doctorId = string.Empty;
        return completed || order.state == SurgeryOrderState.Failed;
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
            order.statusData.Set(
                SurgeryStatusCode.ProcedurePaused,
                order.orderId);
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
                procedure.RequiredFacilityTags);
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

        string id = $"surgery:{++orderSequence}";
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
        }

        order = new SurgeryOrder
        {
            orderId = id,
            procedureId = procedure.ProcedureId,
            subject = subject.Clone(),
            targetNodeId = string.IsNullOrWhiteSpace(targetNodeId)
                ? procedure.TargetNodeId
                : targetNodeId.Trim(),
            selectedPartInstanceId = selectedPartInstanceId?.Trim() ?? string.Empty,
            preferredDoctorId = normalizedDoctorId,
            facilityId = facilities.GetFacilityId(facility.PrimaryFacility),
            materialDestinationId = $"surgery-materials:{id}",
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
        orders.Add(order);
        surgeryLogistics.RequestMissingMaterials(order, facility.PrimaryFacility);
        surgeryLogistics.PrepareAdmission(order, facility.PrimaryFacility);
        workforce.RequestOneHaulerToReplan(forceInterrupt: true);
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

        CancelInternal(order);
        return true;
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
        if (surgeryEnvironment.RecordClinicalStage(
            order,
            SurgeryOrderState.Anesthetizing))
        {
            surgeryEnvironment.ApplyRisk(order, doctor, facility);
        }
        if (order.completedWork < anesthesiaEnd)
        {
            order.state = SurgeryOrderState.Anesthetizing;
            order.statusData.Set(
                procedure.RequiresAnesthesia
                    ? SurgeryStatusCode.AnesthesiaInProgress
                    : SurgeryStatusCode.PatientRestraintInProgress);
            return;
        }

        if (surgeryEnvironment.RecordClinicalStage(
                order,
                SurgeryOrderState.Incision))
        {
            surgeryEnvironment.ApplyRisk(order, doctor, facility);
        }
        order.incisionOpen = true;
        if (order.completedWork < incisionEnd)
        {
            order.state = SurgeryOrderState.Incision;
            order.statusData.Set(SurgeryStatusCode.IncisionInProgress);
            return;
        }

        if (surgeryEnvironment.RecordClinicalStage(
                order,
                SurgeryOrderState.Procedure))
        {
            surgeryEnvironment.ApplyRisk(order, doctor, facility);
        }
        if (order.completedWork < procedureEnd)
        {
            order.state = SurgeryOrderState.Procedure;
            order.statusData.Set(SurgeryStatusCode.ProcedureInProgress);
            return;
        }

        if (surgeryEnvironment.RecordClinicalStage(
                order,
                SurgeryOrderState.Suturing))
        {
            surgeryEnvironment.ApplyRisk(order, doctor, facility);
        }
        order.state = SurgeryOrderState.Suturing;
        order.statusData.Set(SurgeryStatusCode.SuturingInProgress);
    }

    private bool ResolveOutcome(
        SurgeryOrder order,
        SurgicalProcedureSO procedure,
        BuildableObject facility,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        bool success = outcomeRandom.NextFloat() <= order.risk.successChance;
        if (success)
        {
            foreach (SurgicalProcedureEffect effect in procedure.Effects)
            {
                if (effect == null
                    || !effectHandlers.TryGetValue(
                        effect.GetType(),
                        out ISurgicalProcedureEffectHandler handler))
                {
                    failure = new DomainFailure(
                        FailureCode.SurgeryEffectHandlerMissing,
                        effect?.GetType().Name ?? string.Empty);
                    order.state = SurgeryOrderState.Failed;
                    order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
                    return false;
                }

                if (!handler.Apply(
                        order,
                        effect,
                        facility,
                        out failure))
                {
                    order.state = SurgeryOrderState.Failed;
                    order.statusData.Set(SurgeryStatusCode.ProcedurePaused);
                    return false;
                }
            }

            order.failureSeverity = SurgeryFailureSeverity.None;
            order.incisionOpen = false;
            order.state = SurgeryOrderState.Recovering;
            order.recoveryUntil = clock.Time + RecoverySeconds;
            order.statusData.Set(SurgeryStatusCode.RecoveryObservation);
            return true;
        }

        float severityRoll = outcomeRandom.NextFloat();
        order.failureSeverity = severityRoll < 0.6f
            ? SurgeryFailureSeverity.Minor
            : severityRoll < 0.9f
                ? SurgeryFailureSeverity.Major
                : SurgeryFailureSeverity.Fatal;
        ApplyFailureConsequences(order);
        order.incisionOpen = false;
        order.state = SurgeryOrderState.Failed;
        order.statusData.Set(order.failureSeverity switch
        {
            SurgeryFailureSeverity.Minor =>
                SurgeryStatusCode.CompletedWithMinorFailure,
            SurgeryFailureSeverity.Major =>
                SurgeryStatusCode.CompletedWithMajorFailure,
            SurgeryFailureSeverity.Fatal => SurgeryStatusCode.FailedFatal,
            _ => SurgeryStatusCode.CompletedWithMajorFailure
        });
        ReleasePatient(order);
        failure = new DomainFailure(
            FailureCode.SurgeryOutcomeFailed,
            order.failureSeverity.ToString());
        return false;
    }

    private void ApplyFailureConsequences(SurgeryOrder order)
    {
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
                int damage = order.failureSeverity switch
                {
                    SurgeryFailureSeverity.Minor => Mathf.CeilToInt(animal.MaxHealth * 0.1f),
                    SurgeryFailureSeverity.Major => Mathf.CeilToInt(animal.MaxHealth * 0.35f),
                    SurgeryFailureSeverity.Fatal => animal.CurrentHealth,
                    _ => 0
                };
                animal.ApplyDamage(damage, null);
            }

            return;
        }

        switch (order.failureSeverity)
        {
            case SurgeryFailureSeverity.Minor:
                anatomy.TryDamageNode(
                    patient,
                    order.targetNodeId,
                    3f,
                    0.08f,
                    SurgeryStatusCode.CompletedWithMinorFailure.ToString());
                anatomy.TryAddNodeBurden(
                    patient,
                    order.targetNodeId,
                    0f,
                    0f,
                    order.risk.infectionChance * 20f,
                    out _);
                break;
            case SurgeryFailureSeverity.Major:
                anatomy.TryDamageNode(
                    patient,
                    order.targetNodeId,
                    10f,
                    0.25f,
                    SurgeryStatusCode.CompletedWithMajorFailure.ToString());
                anatomy.TryAddNodeBurden(
                    patient,
                    order.targetNodeId,
                    5f,
                    0f,
                    order.risk.infectionChance * 35f,
                    out _);
                break;
            case SurgeryFailureSeverity.Fatal:
                patient.Die(SurgeryStatusCode.FailedFatal.ToString());
                break;
        }
    }

    private void CancelInternal(SurgeryOrder order)
    {
        if (order == null)
        {
            return;
        }

        order.state = SurgeryOrderState.Cancelled;
        order.statusData.Set(SurgeryStatusCode.Cancelled);
        order.doctorId = string.Empty;
        if (!string.IsNullOrWhiteSpace(order.selectedPartInstanceId))
        {
            parts.ReleaseReservation(order.selectedPartInstanceId, order.orderId);
        }

        Vector2Int releasePosition = TryResolveFacility(
            order.facilityId,
            out BuildableObject facility)
            ? facility.centerPos
            : new Vector2Int(order.admissionX, order.admissionY);
        items.ReleaseStacksByDestination(
            order.materialDestinationId,
            releasePosition);
        ReleasePatient(order);
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
            && captive.IsActive
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
