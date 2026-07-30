using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class SurgeryRuntime :
    ISurgeryRuntime,
    ISurgeryCommandService,
    ITickable
{
    private const float MaterialRefreshInterval = 0.75f;
    private const float AdmissionRetryInterval = 1.5f;
    private const float AutomaticPolicyScanInterval = 1f;
    private const float RecoverySeconds = 10f;
    private static readonly Vector2Int[] CardinalDirections =
    {
        Vector2Int.left,
        Vector2Int.right,
        Vector2Int.up,
        Vector2Int.down
    };

    private readonly ISurgicalProcedureCatalog procedures;
    private readonly ISurgicalFacilityQuery facilities;
    private readonly ISurgeryRiskEvaluator riskEvaluator;
    private readonly ISurgicalPartRuntime parts;
    private readonly ISurgeryPolicyRuntime policies;
    private readonly ISurgeryExtractionLedger extractionLedger;
    private readonly ISurgicalCorpseFreshnessRuntime corpseFreshness;
    private readonly ICharacterWorldQuery characters;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly ICaptivityRuntime captivity;
    private readonly IBuildingWorldQuery buildings;
    private readonly IWorldItemStackRuntime items;
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IWildlifeAnatomyHealthRuntime wildlifeAnatomy;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly ISurgicalPatientTransportRuntime patientTransport;
    private readonly ICharacterMedicalRuntime medical;
    private readonly IBlueprintResearchStateService research;
    private readonly IWorkforceReplanService workforce;
    private readonly IGameClock clock;
    private readonly IRandomStream outcomeRandom;
    private readonly IProcessFluidUseRuntime processFluids;
    private readonly Dictionary<Type, ISurgicalProcedureEffectHandler> effectHandlers;
    private readonly List<SurgeryOrder> orders = new();
    private float nextMaterialRefreshAt;
    private float nextAutomaticPolicyScanAt;
    private int orderSequence;

    public SurgeryRuntime(
        ISurgicalProcedureCatalog procedures,
        ISurgicalFacilityQuery facilities,
        ISurgeryRiskEvaluator riskEvaluator,
        ISurgicalPartRuntime parts,
        ISurgeryPolicyRuntime policies,
        ISurgeryExtractionLedger extractionLedger,
        ISurgicalCorpseFreshnessRuntime corpseFreshness,
        ICharacterWorldQuery characters,
        IWildlifeWorldQuery wildlife,
        ICaptivityRuntime captivity,
        IBuildingWorldQuery buildings,
        IWorldItemStackRuntime items,
        ICharacterBodyHealthRuntime bodyHealth,
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy,
        IAnatomyProfileCatalog anatomyProfiles,
        ISurgicalPatientTransportRuntime patientTransport,
        ICharacterMedicalRuntime medical,
        IBlueprintResearchStateService research,
        IWorkforceReplanService workforce,
        IGameClock clock,
        IRandomStreamProvider randomStreams,
        IReadOnlyList<ISurgicalProcedureEffectHandler> registeredEffectHandlers,
        IProcessFluidUseRuntime processFluids = null)
    {
        this.procedures = procedures ?? throw new ArgumentNullException(nameof(procedures));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.riskEvaluator = riskEvaluator ?? throw new ArgumentNullException(nameof(riskEvaluator));
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        this.extractionLedger = extractionLedger
            ?? throw new ArgumentNullException(nameof(extractionLedger));
        this.corpseFreshness = corpseFreshness
            ?? throw new ArgumentNullException(nameof(corpseFreshness));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.bodyHealth = bodyHealth ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.wildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        this.patientTransport = patientTransport
            ?? throw new ArgumentNullException(nameof(patientTransport));
        this.medical = medical ?? throw new ArgumentNullException(nameof(medical));
        this.research = research ?? throw new ArgumentNullException(nameof(research));
        this.workforce = workforce ?? throw new ArgumentNullException(nameof(workforce));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.processFluids = processFluids;
        outcomeRandom = (randomStreams
            ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("medical:surgery-outcomes");
        effectHandlers = BuildEffectIndex(registeredEffectHandlers);
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
            if (!TryResolveFacility(order.facilityId, out BuildableObject facility)
                || !procedures.TryGet(order.procedureId, out SurgicalProcedureSO procedure))
            {
                CancelInternal(order, "수술 시설 또는 절차가 사라졌습니다.");
                continue;
            }

            SurgicalFacilitySnapshot facilityState = facilities.Evaluate(
                facility,
                procedure.RequiredFacilityTags);
            if (!facilityState.IsAvailable)
            {
                order.status = facilityState.BlockReason;
                continue;
            }

            if (order.state == SurgeryOrderState.Recovering)
            {
                if (clock.Time >= order.recoveryUntil)
                {
                    order.state = SurgeryOrderState.Completed;
                    order.status = "수술 후 회복 완료";
                    ReleasePatient(order);
                }

                continue;
            }

            if (refreshMaterials)
            {
                RequestMissingMaterials(order, facility, procedure);
            }

            bool patientReady = EnsurePatientAdmission(order, facility);
            bool materialsReady = AreRequiredMaterialsReady(order);
            if (!patientReady)
            {
                order.state = SurgeryOrderState.PatientWaiting;
                continue;
            }

            if (!materialsReady)
            {
                order.state = SurgeryOrderState.MaterialsWaiting;
                order.status = "수술 재료 운반 대기";
                continue;
            }

            if (order.state is SurgeryOrderState.PatientWaiting
                or SurgeryOrderState.MaterialsWaiting)
            {
                order.state = SurgeryOrderState.Anesthetizing;
                order.status = procedure.RequiresAnesthesia
                    ? "마취 준비 완료"
                    : "절개 준비 완료";
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

            SurgicalSubjectRef subject = CreateCharacterSubject(
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

    private static SurgicalSubjectRef CreateCharacterSubject(
        CharacterActor actor,
        bool automaticEmergencyDefault)
    {
        return new SurgicalSubjectRef
        {
            kind = SurgicalSubjectKind.Character,
            subjectId = actor?.Identity?.PersistentId ?? string.Empty,
            displayName = actor?.Identity?.DisplayName ?? string.Empty,
            speciesId = actor?.Identity?.SpeciesTag ?? string.Empty,
            willing = actor != null && actor.characterType == CharacterType.NPC,
            automaticEmergencyDefault = automaticEmergencyDefault
        };
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
            .OrderBy(candidate => candidate.createdAt)
            .ThenBy(candidate => candidate.orderId, StringComparer.Ordinal)
            .FirstOrDefault();
        return order != null;
    }

    public bool TryReserveWork(
        BuildableObject facility,
        CharacterActor doctor,
        out SurgeryOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (doctor == null || doctor.IsDead || !doctor.CanRunAi)
        {
            order = null;
            failureReason = "집도 가능한 의사가 아닙니다.";
            return false;
        }

        string doctorId = doctor.Identity?.PersistentId ?? string.Empty;
        if (captivity.IsCaptive(doctorId)
            || doctor.characterType != CharacterType.NPC)
        {
            order = null;
            failureReason = "사장 또는 직원만 수술을 집도할 수 있습니다.";
            return false;
        }

        if (!TryGetWorkFor(facility, out order))
        {
            failureReason = "이 시설에서 진행할 수술이 없습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(order.preferredDoctorId)
            && !string.Equals(
                order.preferredDoctorId,
                doctor.Identity?.PersistentId,
                StringComparison.Ordinal))
        {
            order = null;
            failureReason = "지정된 의사만 집도할 수 있습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(order.doctorId)
            && !string.Equals(
                order.doctorId,
                doctor.Identity?.PersistentId,
                StringComparison.Ordinal))
        {
            order = null;
            failureReason = "다른 의사가 집도 중입니다.";
            return false;
        }

        if (!procedures.TryGet(
                order.procedureId,
                out SurgicalProcedureSO procedure))
        {
            order = null;
            failureReason = "수술 절차를 찾을 수 없습니다.";
            return false;
        }

        SurgicalFacilitySnapshot snapshot = facilities.Evaluate(
            facility,
            procedure.RequiredFacilityTags);
        if (!snapshot.IsAvailable)
        {
            order = null;
            failureReason = snapshot.BlockReason;
            return false;
        }

        order.doctorId = doctor.Identity?.PersistentId ?? string.Empty;
        order.risk = riskEvaluator.Evaluate(
            doctor,
            order.subject,
            procedure,
            snapshot,
            ResolvePatientInstability(order.subject),
            ResolveCompatibilityPenalty(order));
        order.status = "집도 시작";
        return true;
    }

    public bool ApplyWork(
        string orderId,
        CharacterActor doctor,
        float work,
        out bool completed,
        out string failureReason)
    {
        completed = false;
        failureReason = string.Empty;
        if (!TryGetOrder(orderId, out SurgeryOrder order)
            || !order.IsActive)
        {
            failureReason = "수술 주문이 유효하지 않습니다.";
            return false;
        }

        if (doctor == null
            || !string.Equals(
                order.doctorId,
                doctor.Identity?.PersistentId,
                StringComparison.Ordinal))
        {
            failureReason = "예약된 의사가 아닙니다.";
            return false;
        }

        if (!TryResolveFacility(order.facilityId, out BuildableObject facility)
            || !procedures.TryGet(order.procedureId, out SurgicalProcedureSO procedure))
        {
            failureReason = "수술 시설 또는 절차가 사라졌습니다.";
            return false;
        }

        if (!order.processFluidConsumed
            && processFluids != null
            && !processFluids.TryConsumeCycle(
                facility,
                BuiltInWorkTypeIds.Surgery,
                out failureReason))
        {
            order.status = failureReason;
            return false;
        }

        order.processFluidConsumed = true;
        if (!order.materialsConsumed
            && !TryConsumeMaterials(order, out failureReason))
        {
            order.state = SurgeryOrderState.MaterialsWaiting;
            order.status = failureReason;
            return false;
        }

        float applied = Mathf.Max(0f, work);
        if (applied <= 0f)
        {
            return true;
        }

        order.completedWork = Mathf.Min(
            order.requiredWork,
            order.completedWork + applied);
        UpdatePhase(order, procedure);
        if (order.completedWork + 0.001f < order.requiredWork)
        {
            return true;
        }

        order.resultRolled = true;
        completed = ResolveOutcome(order, procedure, facility, out failureReason);
        order.doctorId = string.Empty;
        return completed || order.state == SurgeryOrderState.Failed;
    }

    public void ReleaseDoctor(
        string orderId,
        CharacterActor doctor,
        string reason)
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
                "중단된 수술의 열린 상처");
            order.status = $"수술 중단 · 열린 상처: {reason}";
        }
        else
        {
            order.status = $"수술 일시 중단: {reason}";
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
        out string failureReason)
    {
        order = null;
        failureReason = string.Empty;
        if (subject == null || !subject.IsValid)
        {
            failureReason = "수술 대상이 유효하지 않습니다.";
            return false;
        }

        if (!procedures.TryGet(procedureId, out SurgicalProcedureSO procedure))
        {
            failureReason = "수술 절차를 찾을 수 없습니다.";
            return false;
        }

        string normalizedDoctorId = preferredDoctorId?.Trim() ?? string.Empty;
        if (subject.kind == SurgicalSubjectKind.Character
            && !string.IsNullOrWhiteSpace(normalizedDoctorId)
            && string.Equals(
                subject.subjectId,
                normalizedDoctorId,
                StringComparison.Ordinal))
        {
            failureReason = "환자는 자신의 수술을 집도할 수 없습니다.";
            return false;
        }

        if (!ValidateSubject(subject, procedure, targetNodeId, out failureReason)
            || !ValidateResearch(procedure, out failureReason))
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
            failureReason = "대상에게 이미 진행 중인 수술 주문이 있습니다.";
            return false;
        }

        SurgicalFacilitySnapshot facility;
        if (!string.IsNullOrWhiteSpace(preferredFacilityId))
        {
            if (!TryResolveFacility(preferredFacilityId, out BuildableObject preferred))
            {
                failureReason = "지정한 수술 시설을 찾을 수 없습니다.";
                return false;
            }

            facility = facilities.Evaluate(
                preferred,
                procedure.RequiredFacilityTags);
            if (!facility.IsAvailable)
            {
                failureReason = facility.BlockReason;
                return false;
            }
        }
        else if (!facilities.TryFindBestFacility(
                     subject,
                     procedure,
                     out facility,
                     out failureReason))
        {
            return false;
        }

        string id = $"surgery:{++orderSequence}";
        if (RequiresInstalledPart(procedure))
        {
            if (!ValidateSelectedPart(
                    subject,
                    procedure,
                    targetNodeId,
                    selectedPartInstanceId,
                    out failureReason)
                || !parts.TryReserveForOrder(
                    selectedPartInstanceId,
                    id,
                    out failureReason))
            {
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
            materials = BuildMaterialRequirements(subject, procedure, facility),
            status = "환자 입실 대기",
            createdAt = clock.Time
        };
        orders.Add(order);
        RequestMissingMaterials(order, facility.PrimaryFacility, procedure);
        PrepareSubjectForAdmission(order, facility.PrimaryFacility);
        workforce.RequestOneHaulerToReplan(forceInterrupt: true);
        workforce.RequestOneWorkerToReplanFor(
            BuiltInWorkTypeIds.Surgery,
            forceInterrupt: true);
        return true;
    }

    private bool ValidateSelectedPart(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        string targetNodeId,
        string partInstanceId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!parts.TryGet(partInstanceId, out SurgicalPartInstance part)
            || part.installed)
        {
            failureReason = "사용 가능한 장기 또는 보철을 선택해야 합니다.";
            return false;
        }

        bool kindMatches = procedure.Kind switch
        {
            SurgicalProcedureKind.TransplantOrgan =>
                part.kind == SurgicalPartKind.NaturalOrgan,
            SurgicalProcedureKind.InstallProsthetic =>
                part.kind == SurgicalPartKind.Prosthetic,
            SurgicalProcedureKind.InstallImplant =>
                part.kind == SurgicalPartKind.Implant,
            SurgicalProcedureKind.ArcaneModification =>
                part.kind == SurgicalPartKind.ArcaneGraft
                || part.kind == SurgicalPartKind.Implant,
            _ => true
        };
        if (!kindMatches)
        {
            failureReason = "선택한 부품 종류가 수술 절차와 맞지 않습니다.";
            return false;
        }

        string target = string.IsNullOrWhiteSpace(targetNodeId)
            ? procedure.TargetNodeId
            : targetNodeId.Trim();
        if (string.Equals(part.nodeId, target, StringComparison.Ordinal))
        {
            return true;
        }

        AnatomyProfileDefinition recipient =
            anatomyProfiles.GetForSpecies(subject?.speciesId);
        if (recipient.TryGetNode(target, out AnatomyNodeDefinition targetNode)
            && recipient.TryGetNode(
                part.nodeId,
                out AnatomyNodeDefinition partNode)
            && !string.IsNullOrWhiteSpace(targetNode.PairedGroupId)
            && string.Equals(
                targetNode.PairedGroupId,
                partNode.PairedGroupId,
                StringComparison.Ordinal))
        {
            return true;
        }

        failureReason = "선택한 장기 또는 보철은 대상 부위와 맞지 않습니다.";
        return false;
    }

    public bool TryCancel(string orderId, out string failureReason)
    {
        failureReason = string.Empty;
        if (!TryGetOrder(orderId, out SurgeryOrder order) || !order.IsActive)
        {
            failureReason = "취소할 수술 주문이 없습니다.";
            return false;
        }

        CancelInternal(order, "플레이어가 수술을 취소했습니다.");
        return true;
    }

    public DungeonSurgerySaveData Capture()
    {
        SurgeryPolicyRuntime concretePolicies = policies as SurgeryPolicyRuntime;
        return new DungeonSurgerySaveData
        {
            orders = orders.Select(CloneOrder).ToList(),
            parts = parts.CaptureParts().ToList(),
            organStorageStates = parts.CaptureStorageStates().ToList(),
            corpseFreshness = corpseFreshness.Capture().ToList(),
            policies = concretePolicies?.Capture().ToList()
                ?? new List<SurgerySubjectPolicyState>(),
            corpseRecords = extractionLedger.Capture().ToList(),
            wildlifeAnatomy = wildlifeAnatomy.Capture().ToList(),
            orderSequence = orderSequence
        };
    }

    public void Restore(
        DungeonSurgerySaveData saveData,
        IList<string> warnings)
    {
        foreach (SurgeryOrder active in orders.Where(order =>
                     order != null && order.IsActive))
        {
            patientTransport.CancelTransport(
                active,
                "저장 상태 복원을 위해 운반 예약을 해제했습니다.");
            if (active.subject?.kind == SurgicalSubjectKind.Character)
            {
                ReleasePatient(active);
            }
        }

        orders.Clear();
        orderSequence = Mathf.Max(0, saveData?.orderSequence ?? 0);
        parts.RestoreParts(saveData?.parts, warnings);
        parts.RestoreStorageStates(saveData?.organStorageStates, warnings);
        corpseFreshness.Restore(saveData?.corpseFreshness, warnings);
        extractionLedger.Restore(saveData?.corpseRecords, warnings);
        wildlifeAnatomy.Restore(saveData?.wildlifeAnatomy, warnings);
        if (policies is SurgeryPolicyRuntime concretePolicies)
        {
            concretePolicies.Restore(saveData?.policies, warnings);
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (SurgeryOrder source in
                 saveData?.orders ?? new List<SurgeryOrder>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.orderId)
                || !ids.Add(source.orderId))
            {
                warnings?.Add("중복되거나 잘못된 수술 주문을 제외했습니다.");
                continue;
            }

            SurgeryOrder restored = CloneOrder(source);
            restored.doctorId = string.Empty;
            restored.admissionMoveRequested = false;
            restored.patientTransporterId = string.Empty;
            restored.patientTransportInProgress = false;
            restored.patientReturnRequested = false;
            if (!restored.IsActive
                || procedures.TryGet(restored.procedureId, out _)
                && TryResolveFacility(restored.facilityId, out _))
            {
                orders.Add(restored);
            }
            else
            {
                warnings?.Add($"{restored.orderId}: 대상 시설이나 절차가 없어 취소했습니다.");
                restored.state = SurgeryOrderState.Cancelled;
                restored.status = "복원 중 수술 대상 소실";
                orders.Add(restored);
            }
        }
    }

    private void PrepareSubjectForAdmission(
        SurgeryOrder order,
        BuildableObject facility)
    {
        if (order.subject.kind != SurgicalSubjectKind.Character)
        {
            return;
        }

        CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        if (patient == null || patient.IsDead)
        {
            return;
        }

        if (bodyHealth.GetSnapshot(patient).Downed)
        {
            if (medical.TryRequestTreatment(patient, out CharacterMedicalOrder medicalOrder, out _))
            {
                medical.TryAssignSpecificTreatmentFacility(
                    medicalOrder.orderId,
                    facility,
                    out _);
            }

            return;
        }

        order.subjectAiWasPaused = patient.IsAiPaused();
        patient.SetAiPaused(true);
        patient.Brain?.SetActionPhase("수술 입실 준비", facility);
    }

    private bool EnsurePatientAdmission(
        SurgeryOrder order,
        BuildableObject facility)
    {
        if (order.subject.kind is SurgicalSubjectKind.HumanoidCorpse
            or SurgicalSubjectKind.WildlifeCorpse)
        {
            WorldItemStackSnapshot corpse = items.GetAllStacks().FirstOrDefault(stack =>
                stack != null
                && string.Equals(
                    stack.StackId,
                    order.subject.subjectId,
                    StringComparison.Ordinal));
            bool ready = corpse != null
                && corpse.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    corpse.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal);
            order.status = ready ? "사체 해부 준비 완료" : "사체 운반 대기";
            return ready;
        }

        if (order.subject.kind == SurgicalSubjectKind.Wildlife)
        {
            WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
                wildlife,
                order.subject.subjectId);
            if (animal == null || !animal.IsAlive)
            {
                order.status = "살아 있는 동물 환자를 찾을 수 없습니다.";
                return false;
            }

            if (ManhattanToFacility(animal.GridPosition, facility) <= 1)
            {
                order.patientAdmitted = true;
                order.status = "동물 환자 준비 완료";
                return true;
            }

            if (!TryFindAdmissionCell(
                    facility,
                    animal.GridPosition,
                    out Vector2Int wildlifeAdmission))
            {
                order.status = "수술대에 접근할 수 있는 동물 환자 칸이 없습니다.";
                return false;
            }

            bool ready = patientTransport.EnsureWildlifeAdmission(
                order,
                animal,
                wildlifeAdmission,
                out string transportStatus);
            order.status = transportStatus;
            return ready;
        }

        CharacterActor patient = SurgicalSubjectResolver.FindCharacter(
            characters,
            order.subject.subjectId);
        if (patient == null || patient.IsDead)
        {
            order.status = "환자를 찾을 수 없습니다.";
            return false;
        }

        if (order.patientAdmitted)
        {
            return true;
        }

        if (ManhattanToFacility(patient.GetNowXY(), facility) <= 1
            || facility.ContainsGridPosition(patient.GetNowXY()))
        {
            order.patientAdmitted = true;
            patient.SetAiPaused(true);
            patient.Brain?.SetActionPhase("수술 대기", facility);
            order.status = "환자 입실 완료";
            return true;
        }

        if (bodyHealth.GetSnapshot(patient).Downed)
        {
            order.status = "구조자가 환자를 수술실로 이송 중";
            return false;
        }

        if (!order.subject.willing)
        {
            if (!captivity.TryGetCaptive(
                    order.subject.subjectId,
                    out CaptiveState captive)
                || !captive.restrained)
            {
                order.status = "비동의 환자는 먼저 구속해야 합니다.";
                return false;
            }
        }

        AbilityMove move = patient.GetAbility<AbilityMove>();
        Vector2Int requestedAdmission = new(
            order.admissionX,
            order.admissionY);
        if (order.admissionMoveRequested)
        {
            if (move != null
                && move.IsSystemMoveInProgressTo(requestedAdmission))
            {
                order.status = "환자가 수술실로 이동 중";
                return false;
            }

            order.admissionMoveRequested = false;
        }

        if (move != null && move.IsSystemMoveInProgress)
        {
            order.status = "환자의 현재 이동이 끝나기를 기다리는 중";
            return false;
        }

        if (clock.Time < order.nextAdmissionRetryAt)
        {
            order.status = "환자가 수술실로 이동 중";
            return false;
        }

        if (!TryFindAdmissionCell(facility, patient.GetNowXY(), out Vector2Int admission))
        {
            order.status = "수술대에 접근할 수 있는 환자 칸이 없습니다.";
            return false;
        }

        if (!order.admissionMoveRequested)
        {
            Vector2Int origin = patient.GetNowXY();
            order.patientOriginX = origin.x;
            order.patientOriginY = origin.y;
        }

        order.admissionX = admission.x;
        order.admissionY = admission.y;
        order.nextAdmissionRetryAt = clock.Time + AdmissionRetryInterval;
        string message = "환자를 수술대로 이동시킬 수 없습니다.";
        order.admissionMoveRequested = move != null
            && move.TryStartSystemMove(
                admission,
                order.subject.willing
                    ? DoorAccessOverrideKind.None
                    : DoorAccessOverrideKind.EscortPass,
                out message);
        order.status = order.admissionMoveRequested
            ? "환자가 수술실로 이동 중"
            : message;
        return false;
    }

    private void RequestMissingMaterials(
        SurgeryOrder order,
        BuildableObject facility,
        SurgicalProcedureSO procedure)
    {
        if (order == null || facility == null)
        {
            return;
        }

        bool deliveryCreated = false;
        foreach (SurgicalMaterialRequirement requirement in order.materials)
        {
            if (requirement == null || requirement.optional)
            {
                continue;
            }

            int routed = CountRoutedItem(order, requirement.itemId);
            int missing = Mathf.Max(0, requirement.quantity - routed);
            if (missing > 0)
            {
                bool created = items.TryRequestItemDelivery(
                    requirement.itemId,
                    missing,
                    facility.centerPos,
                    order.materialDestinationId,
                    out int requested,
                    out _);
                deliveryCreated |= created && requested > 0;
            }
        }

        if (order.subject.kind is SurgicalSubjectKind.HumanoidCorpse
            or SurgicalSubjectKind.WildlifeCorpse)
        {
            WorldItemStackSnapshot corpse = items.GetAllStacks().FirstOrDefault(stack =>
                stack != null
                && string.Equals(
                    stack.StackId,
                    order.subject.subjectId,
                    StringComparison.Ordinal));
            if (corpse != null
                && !string.Equals(
                    corpse.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal))
            {
                bool created = items.TryRequestStackDelivery(
                    corpse.StackId,
                    1,
                    facility.centerPos,
                    order.materialDestinationId,
                    out int requested,
                    out _);
                deliveryCreated |= created && requested > 0;
            }
        }

        if (!string.IsNullOrWhiteSpace(order.selectedPartInstanceId)
            && parts.TryGet(
                order.selectedPartInstanceId,
                out SurgicalPartInstance part)
            && !string.IsNullOrWhiteSpace(part.worldStackId))
        {
            bool created = items.TryRequestStackDelivery(
                part.worldStackId,
                1,
                facility.centerPos,
                order.materialDestinationId,
                out int requested,
                out _);
            deliveryCreated |= created && requested > 0;
        }

        if (deliveryCreated)
        {
            foreach (WorldItemStackSnapshot stack in items.GetAllStacks()
                         .Where(stack => stack != null
                             && string.Equals(
                                 stack.DestinationId,
                                 order.materialDestinationId,
                                 StringComparison.Ordinal)
                             && stack.State is WorldItemStackState.Loose
                                 or WorldItemStackState.Stored))
            {
                items.PrioritizeHaul(stack.StackId);
            }
        }

        if (deliveryCreated)
        {
            workforce.RequestOneHaulerToReplan(forceInterrupt: true);
        }

        order.materialsRequested = true;
    }

    private int CountRoutedItem(SurgeryOrder order, string itemId)
    {
        int worldQuantity = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        int carriedQuantity = characters.Characters
            .Where(actor => actor != null)
            .Select(actor => actor.GetComponent<AbilityHaul>())
            .Where(haul => haul != null)
            .Sum(haul => haul.GetInTransitQuantity(
                order.materialDestinationId,
                itemId));
        return worldQuantity + carriedQuantity;
    }

    private bool AreRequiredMaterialsReady(SurgeryOrder order)
    {
        foreach (SurgicalMaterialRequirement requirement in order.materials)
        {
            if (requirement == null || requirement.optional)
            {
                continue;
            }

            int buffered = items.GetAllStacks()
                .Where(stack => stack != null
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(
                        stack.DestinationId,
                        order.materialDestinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        requirement.itemId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            if (buffered < requirement.quantity)
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(order.selectedPartInstanceId)
            && parts.TryGet(
                order.selectedPartInstanceId,
                out SurgicalPartInstance selected))
        {
            WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(
                candidate => candidate != null
                    && string.Equals(
                        candidate.StackId,
                        selected.worldStackId,
                        StringComparison.Ordinal));
            if (stack == null
                || stack.State != WorldItemStackState.FacilityBuffer
                || !string.Equals(
                    stack.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryConsumeMaterials(
        SurgeryOrder order,
        out string failureReason)
    {
        Dictionary<string, int> costs = order.materials
            .Where(requirement => requirement != null && !requirement.optional)
            .GroupBy(requirement => requirement.itemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => Mathf.Max(1, item.quantity)),
                StringComparer.Ordinal);
        if (costs.Count > 0
            && !items.TryConsumeFacilityItemBuffer(
                order.materialDestinationId,
                costs,
                out failureReason))
        {
            return false;
        }

        order.materialsConsumed = true;
        order.anesthesiaConsumed = order.materials.Any(requirement =>
            requirement != null
            && string.Equals(
                requirement.itemId,
                SurgeryItemDefinitions.AnestheticId,
                StringComparison.Ordinal));
        failureReason = string.Empty;
        return true;
    }

    private void UpdatePhase(
        SurgeryOrder order,
        SurgicalProcedureSO procedure)
    {
        float anesthesiaEnd = order.anesthesiaWork;
        float incisionEnd = anesthesiaEnd + order.incisionWork;
        float procedureEnd = incisionEnd + order.procedureWork;
        RecordClinicalStage(order, SurgeryOrderState.Anesthetizing);
        if (order.completedWork < anesthesiaEnd)
        {
            order.state = SurgeryOrderState.Anesthetizing;
            order.status = procedure.RequiresAnesthesia ? "마취 중" : "환자 고정 중";
            return;
        }

        RecordClinicalStage(order, SurgeryOrderState.Incision);
        order.incisionOpen = true;
        if (order.completedWork < incisionEnd)
        {
            order.state = SurgeryOrderState.Incision;
            order.status = "절개 중";
            return;
        }

        RecordClinicalStage(order, SurgeryOrderState.Procedure);
        if (order.completedWork < procedureEnd)
        {
            order.state = SurgeryOrderState.Procedure;
            order.status = "수술 처치 중";
            return;
        }

        RecordClinicalStage(order, SurgeryOrderState.Suturing);
        order.state = SurgeryOrderState.Suturing;
        order.status = "봉합 중";
    }

    private static void RecordClinicalStage(
        SurgeryOrder order,
        SurgeryOrderState state)
    {
        order.reachedClinicalStages ??= new List<SurgeryOrderState>();
        if (!order.reachedClinicalStages.Contains(state))
        {
            order.reachedClinicalStages.Add(state);
        }
    }

    private bool ResolveOutcome(
        SurgeryOrder order,
        SurgicalProcedureSO procedure,
        BuildableObject facility,
        out string failureReason)
    {
        failureReason = string.Empty;
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
                    failureReason =
                        $"등록되지 않은 수술 효과입니다: {effect?.GetType().Name ?? "null"}";
                    order.state = SurgeryOrderState.Failed;
                    order.status = failureReason;
                    return false;
                }

                if (!handler.Apply(
                        order,
                        effect,
                        facility,
                        out failureReason))
                {
                    order.state = SurgeryOrderState.Failed;
                    order.status = failureReason;
                    return false;
                }
            }

            order.failureSeverity = SurgeryFailureSeverity.None;
            order.incisionOpen = false;
            order.state = SurgeryOrderState.Recovering;
            order.recoveryUntil = clock.Time + RecoverySeconds;
            order.status = "수술 완료 · 회복 관찰 중";
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
        order.status = order.failureSeverity switch
        {
            SurgeryFailureSeverity.Minor => "수술 실패 · 경미한 합병증",
            SurgeryFailureSeverity.Major => "수술 실패 · 장기 손상",
            SurgeryFailureSeverity.Fatal => "수술 실패 · 치명적 결과",
            _ => "수술 실패"
        };
        ReleasePatient(order);
        failureReason = order.status;
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
                    "수술 합병증");
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
                    "수술 중 장기 손상");
                anatomy.TryAddNodeBurden(
                    patient,
                    order.targetNodeId,
                    5f,
                    0f,
                    order.risk.infectionChance * 35f,
                    out _);
                break;
            case SurgeryFailureSeverity.Fatal:
                patient.Die("치명적인 수술 실패");
                break;
        }
    }

    private bool ValidateSubject(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        string targetNodeId,
        out string failureReason)
    {
        failureReason = string.Empty;
        bool corpse = subject.kind is SurgicalSubjectKind.HumanoidCorpse
            or SurgicalSubjectKind.WildlifeCorpse;
        if (corpse && !procedure.AllowsCorpseSubject
            || !corpse && !procedure.AllowsLivingSubject
            || subject.kind is SurgicalSubjectKind.Wildlife
                or SurgicalSubjectKind.WildlifeCorpse
                && !procedure.AllowsWildlife)
        {
            failureReason = "이 수술은 선택한 대상 유형에 사용할 수 없습니다.";
            return false;
        }

        string nodeId = string.IsNullOrWhiteSpace(targetNodeId)
            ? procedure.TargetNodeId
            : targetNodeId.Trim();
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            failureReason = "수술할 신체 부위를 선택해야 합니다.";
            return false;
        }

        if (corpse)
        {
            WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.StackId,
                    subject.subjectId,
                    StringComparison.Ordinal));
            if (stack == null)
            {
                failureReason = "신선한 사체 물리 스택을 찾을 수 없습니다.";
                return false;
            }

            if (!corpseFreshness.TryGetFreshness(
                    subject.subjectId,
                    out _,
                    out bool isFresh)
                || !isFresh)
            {
                failureReason = "신선하지 않은 사체에서는 장기를 적출할 수 없습니다.";
                return false;
            }

            if (extractionLedger.IsExtracted(subject.subjectId, nodeId))
            {
                failureReason = "이미 적출한 부위입니다.";
                return false;
            }
        }
        else if (subject.kind == SurgicalSubjectKind.Character)
        {
            CharacterActor actor = SurgicalSubjectResolver.FindCharacter(
                characters,
                subject.subjectId);
            if (actor == null || actor.IsDead)
            {
                failureReason = "살아 있는 수술 대상을 찾을 수 없습니다.";
                return false;
            }

            if (!anatomy.GetAnatomySnapshot(actor).Nodes.Any(node =>
                    node != null
                    && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal)))
            {
                failureReason = "대상의 해부 구조에 해당 부위가 없습니다.";
                return false;
            }
        }
        else
        {
            WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
                wildlife,
                subject.subjectId);
            if (animal == null || !animal.IsAlive)
            {
                failureReason = "살아 있는 동물 수술 대상을 찾을 수 없습니다.";
                return false;
            }

            if (!wildlifeAnatomy.GetAnatomySnapshot(animal).Nodes.Any(node =>
                    node != null
                    && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal)))
            {
                failureReason = "대상 동물의 해부 구조에 해당 부위가 없습니다.";
                return false;
            }
        }

        return true;
    }

    private bool ValidateResearch(
        SurgicalProcedureSO procedure,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (string.IsNullOrWhiteSpace(procedure.RequiredResearchId))
        {
            return true;
        }

        try
        {
            if (research.GetState().Projects.IsCompleted(
                    new ResearchProjectId(procedure.RequiredResearchId)))
            {
                return true;
            }
        }
        catch (InvalidOperationException)
        {
            failureReason = "연구 상태를 불러오지 못했습니다.";
            return false;
        }

        failureReason = "필요한 수술 연구가 완료되지 않았습니다.";
        return false;
    }

    private List<SurgicalMaterialRequirement> BuildMaterialRequirements(
        SurgicalSubjectRef subject,
        SurgicalProcedureSO procedure,
        SurgicalFacilitySnapshot facility)
    {
        Dictionary<string, SurgicalMaterialRequirement> merged =
            new Dictionary<string, SurgicalMaterialRequirement>(StringComparer.Ordinal);
        foreach (SurgicalMaterialRequirement requirement in procedure.Materials)
        {
            Add(requirement?.itemId, requirement?.quantity ?? 0, requirement?.optional ?? false);
        }

        if (procedure.RequiresAnesthesia
            || subject != null && !subject.willing)
        {
            Add(SurgeryItemDefinitions.AnestheticId, 1, false);
        }

        bool alreadyRestrained = subject?.kind == SurgicalSubjectKind.Character
            && captivity.TryGetCaptive(
                subject.subjectId,
                out CaptiveState captive)
            && captive.restrained;
        if (procedure.RequiresRestraintForUnwilling
            && subject != null
            && !subject.willing
            && !alreadyRestrained)
        {
            Add(CaptivityItemDefinitions.RestraintsItemId, 1, false);
        }

        foreach (BuildableObject support in facility.SupportFacilities
                     .Append(facility.PrimaryFacility)
                     .Where(building => building != null))
        {
            BuildingSterilizationAbility sterilization =
                support.BuildingData?.GetAbility<BuildingSterilizationAbility>();
            if (sterilization != null)
            {
                Add(
                    DungeonItemCatalogSO.StockItemId(StockCategory.Water),
                    sterilization.waterCost,
                    false);
                Add(
                    SurgeryItemDefinitions.DisinfectantId,
                    sterilization.disinfectantCost,
                    false);
            }

            BuildingTransplantSupportAbility transplant =
                support.BuildingData?.GetAbility<BuildingTransplantSupportAbility>();
            if (transplant != null
                && (procedure.RequiredFacilityTags & SurgeryFacilityTag.Transplant) != 0)
            {
                Add(SurgeryItemDefinitions.BloodPackId, transplant.bloodCost, false);
                Add(
                    SurgeryItemDefinitions.ImmunosuppressantId,
                    transplant.immunosuppressantCost,
                    false);
            }

            BuildingArcaneSurgeryAbility arcane =
                support.BuildingData?.GetAbility<BuildingArcaneSurgeryAbility>();
            if (arcane != null)
            {
                Add(
                    DungeonItemCatalogSO.StockItemId(StockCategory.Mana),
                    arcane.manaCrystalCost,
                    false);
            }
        }

        return merged.Values
            .Where(requirement => requirement.quantity > 0)
            .OrderBy(requirement => requirement.itemId, StringComparer.Ordinal)
            .ToList();

        void Add(string itemId, int quantity, bool optional)
        {
            if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
            {
                return;
            }

            if (!merged.TryGetValue(itemId.Trim(), out SurgicalMaterialRequirement entry))
            {
                entry = new SurgicalMaterialRequirement
                {
                    itemId = itemId.Trim(),
                    optional = optional
                };
                merged.Add(entry.itemId, entry);
            }

            entry.quantity += quantity;
            entry.optional &= optional;
        }
    }

    private float ResolvePatientInstability(SurgicalSubjectRef subject)
    {
        CharacterActor actor = SurgicalSubjectResolver.FindCharacter(
            characters,
            subject?.subjectId);
        if (actor != null)
        {
            CharacterBodyHealthSnapshot snapshot = bodyHealth.GetSnapshot(actor);
            return Mathf.Clamp01(
                Mathf.Max(
                    1f - snapshot.Consciousness,
                    snapshot.BloodLoss / 100f));
        }

        WildlifeActor animal = SurgicalSubjectResolver.FindWildlife(
            wildlife,
            subject?.subjectId);
        return animal != null
            ? 1f - animal.CurrentHealth / Mathf.Max(1f, animal.MaxHealth)
            : 0f;
    }

    private float ResolveCompatibilityPenalty(SurgeryOrder order)
    {
        if (order == null
            || string.IsNullOrWhiteSpace(order.selectedPartInstanceId)
            || !parts.TryGet(
                order.selectedPartInstanceId,
                out SurgicalPartInstance part))
        {
            return 0f;
        }

        if (string.Equals(
                part.donorSpeciesId,
                order.subject?.speciesId,
                StringComparison.OrdinalIgnoreCase))
        {
            return 0f;
        }

        AnatomyProfileDefinition recipient = anatomyProfiles.GetForSpecies(
            order.subject?.speciesId);
        float compatibility = string.Equals(
                part.anatomyFamily,
                recipient.AnatomyFamily,
                StringComparison.OrdinalIgnoreCase)
            ? 0.75f
            : string.Equals(
                recipient.AnatomyFamily,
                "slime",
                StringComparison.OrdinalIgnoreCase)
                ? 0.2f
                : 0.45f;
        return (1f - compatibility) * 0.35f;
    }

    private void CancelInternal(SurgeryOrder order, string reason)
    {
        if (order == null)
        {
            return;
        }

        order.state = SurgeryOrderState.Cancelled;
        order.status = reason ?? "수술 취소";
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
                patient.Brain?.SetActionPhase("수술 후 감방으로 복귀 중", null);
                if (!order.subjectAiWasPaused)
                {
                    patient.SetAiPaused(false);
                }

                order.status = "수술 완료 · 감방 복귀 중";
                order.patientAdmitted = false;
                return;
            }
        }

        if (!order.subjectAiWasPaused)
        {
            patient.SetAiPaused(false);
        }

        patient.Brain?.SetActionPhase(
            order.state == SurgeryOrderState.Completed
                ? "수술 회복 완료"
                : "수술 종료",
            null);
        order.patientAdmitted = false;
    }

    private bool TryFindAdmissionCell(
        BuildableObject facility,
        Vector2Int origin,
        out Vector2Int admission)
    {
        admission = default;
        if (facility?.Grid == null)
        {
            return false;
        }

        List<Vector2Int> candidates = new List<Vector2Int>();
        foreach (Vector2Int occupied in facility.buildPoses)
        {
            foreach (Vector2Int direction in CardinalDirections)
            {
                Vector2Int cell = occupied + direction;
                if (!facility.ContainsGridPosition(cell)
                    && facility.Grid.IsValidGridPos(cell)
                    && facility.Grid.IsWalkable(cell)
                    && !candidates.Contains(cell))
                {
                    candidates.Add(cell);
                }
            }
        }

        if (candidates.Count == 0)
        {
            return false;
        }

        admission = candidates
            .OrderBy(cell => Mathf.Abs(cell.x - origin.x) + Mathf.Abs(cell.y - origin.y))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .First();
        return true;
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

    private static int ManhattanToFacility(
        Vector2Int position,
        BuildableObject facility)
    {
        return facility?.buildPoses?
            .Select(cell => Mathf.Abs(cell.x - position.x) + Mathf.Abs(cell.y - position.y))
            .DefaultIfEmpty(int.MaxValue)
            .Min() ?? int.MaxValue;
    }

    private static bool RequiresInstalledPart(SurgicalProcedureSO procedure)
    {
        return procedure?.Kind is SurgicalProcedureKind.TransplantOrgan
            or SurgicalProcedureKind.InstallProsthetic
            or SurgicalProcedureKind.InstallImplant
            or SurgicalProcedureKind.ArcaneModification;
    }

    private static Dictionary<Type, ISurgicalProcedureEffectHandler> BuildEffectIndex(
        IReadOnlyList<ISurgicalProcedureEffectHandler> handlers)
    {
        Dictionary<Type, ISurgicalProcedureEffectHandler> index =
            new Dictionary<Type, ISurgicalProcedureEffectHandler>();
        foreach (ISurgicalProcedureEffectHandler handler in
                 handlers ?? Array.Empty<ISurgicalProcedureEffectHandler>())
        {
            if (handler == null || handler.EffectType == null)
            {
                continue;
            }

            if (!index.TryAdd(handler.EffectType, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate surgical effect handler: {handler.EffectType.Name}");
            }
        }

        return index;
    }

    private static SurgeryOrder CloneOrder(SurgeryOrder source)
    {
        return new SurgeryOrder
        {
            orderId = source.orderId ?? string.Empty,
            procedureId = source.procedureId ?? string.Empty,
            subject = source.subject?.Clone() ?? new SurgicalSubjectRef(),
            targetNodeId = source.targetNodeId ?? string.Empty,
            selectedPartInstanceId = source.selectedPartInstanceId ?? string.Empty,
            preferredDoctorId = source.preferredDoctorId ?? string.Empty,
            doctorId = source.doctorId ?? string.Empty,
            facilityId = source.facilityId ?? string.Empty,
            materialDestinationId = source.materialDestinationId ?? string.Empty,
            state = source.state,
            requiredWork = source.requiredWork,
            completedWork = source.completedWork,
            anesthesiaWork = source.anesthesiaWork,
            incisionWork = source.incisionWork,
            procedureWork = source.procedureWork,
            sutureWork = source.sutureWork,
            materialsRequested = source.materialsRequested,
            materialsConsumed = source.materialsConsumed,
            processFluidConsumed = source.processFluidConsumed,
            anesthesiaConsumed = source.anesthesiaConsumed,
            incisionOpen = source.incisionOpen,
            resultRolled = source.resultRolled,
            patientAdmitted = source.patientAdmitted,
            admissionMoveRequested = source.admissionMoveRequested,
            subjectAiWasPaused = source.subjectAiWasPaused,
            patientTransporterId = source.patientTransporterId ?? string.Empty,
            patientTransportInProgress = source.patientTransportInProgress,
            patientReturnRequested = source.patientReturnRequested,
            patientOriginX = source.patientOriginX,
            patientOriginY = source.patientOriginY,
            admissionX = source.admissionX,
            admissionY = source.admissionY,
            nextAdmissionRetryAt = source.nextAdmissionRetryAt,
            failureSeverity = source.failureSeverity,
            risk = source.risk?.Clone() ?? new SurgeryRiskBreakdown(),
            reachedClinicalStages = (source.reachedClinicalStages
                ?? new List<SurgeryOrderState>()).ToList(),
            materials = (source.materials ?? new List<SurgicalMaterialRequirement>())
                .Where(requirement => requirement != null)
                .Select(requirement => requirement.Clone())
                .ToList(),
            status = source.status ?? string.Empty,
            createdAt = source.createdAt,
            recoveryUntil = source.recoveryUntil
        };
    }
}
