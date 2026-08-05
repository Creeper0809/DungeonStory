using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public interface ICharacterSurgeryWindowService
{
    void Open(CharacterActor actor, Transform uiHost);
    CharacterSurgeryHealthProjection GetHealthSummary(CharacterActor actor);
    bool IsAutomaticEmergencyEnabled(CharacterActor actor);
    void ToggleAutomaticEmergency(CharacterActor actor);
}

public interface ISurgeryPlanningWindowService
{
    void Open(WildlifeActor actor, Transform uiHost);
    void Open(WorldItemStackSnapshot corpseStack, Transform uiHost);
}

public sealed class SurgeryClinicalContext
{
    public SurgeryClinicalContext(
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy,
        IAnatomyProfileCatalog profiles,
        ISurgicalProcedureCatalog procedures,
        ISurgicalPartRuntime parts,
        ISurgicalAugmentationQuery augmentations,
        ISurgicalFacilityQuery facilities,
        ISurgeryRiskEvaluator risk)
    {
        Anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        WildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
        Profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        Procedures = procedures
            ?? throw new ArgumentNullException(nameof(procedures));
        Parts = parts ?? throw new ArgumentNullException(nameof(parts));
        Augmentations = augmentations
            ?? throw new ArgumentNullException(nameof(augmentations));
        Facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
        Risk = risk ?? throw new ArgumentNullException(nameof(risk));
    }

    public IAnatomyHealthRuntime Anatomy { get; }
    public IWildlifeAnatomyHealthRuntime WildlifeAnatomy { get; }
    public IAnatomyProfileCatalog Profiles { get; }
    public ISurgicalProcedureCatalog Procedures { get; }
    public ISurgicalPartRuntime Parts { get; }
    public ISurgicalAugmentationQuery Augmentations { get; }
    public ISurgicalFacilityQuery Facilities { get; }
    public ISurgeryRiskEvaluator Risk { get; }
}

public sealed class SurgeryExecutionContext
{
    public SurgeryExecutionContext(
        ISurgeryCommandService commands,
        ISurgeryQuery surgery,
        ISurgeryPolicyRuntime policies,
        ISurgicalCorpseFreshnessRuntime corpseFreshness,
        ISurgeryEnvironmentRiskEvaluator environmentRisk)
    {
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        Surgery = surgery ?? throw new ArgumentNullException(nameof(surgery));
        Policies = policies ?? throw new ArgumentNullException(nameof(policies));
        CorpseFreshness = corpseFreshness
            ?? throw new ArgumentNullException(nameof(corpseFreshness));
        EnvironmentRisk = environmentRisk
            ?? throw new ArgumentNullException(nameof(environmentRisk));
    }

    public ISurgeryCommandService Commands { get; }
    public ISurgeryQuery Surgery { get; }
    public ISurgeryPolicyRuntime Policies { get; }
    public ISurgicalCorpseFreshnessRuntime CorpseFreshness { get; }
    public ISurgeryEnvironmentRiskEvaluator EnvironmentRisk { get; }
}

public sealed class SurgerySubjectWorldContext
{
    public SurgerySubjectWorldContext(
        ICharacterWorldQuery characters,
        ICharacterBodyHealthQuery bodyHealth,
        ICaptivityRuntime captivity,
        IWildlifeCaptureRuntime wildlifeCapture)
    {
        Characters = characters
            ?? throw new ArgumentNullException(nameof(characters));
        BodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        Captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        WildlifeCapture = wildlifeCapture
            ?? throw new ArgumentNullException(nameof(wildlifeCapture));
    }

    public ICharacterWorldQuery Characters { get; }
    public ICharacterBodyHealthQuery BodyHealth { get; }
    public ICaptivityRuntime Captivity { get; }
    public IWildlifeCaptureRuntime WildlifeCapture { get; }
}

public sealed class SurgeryWindowPresentationContext
{
    public SurgeryWindowPresentationContext(
        ITmpKoreanFontService fonts,
        ICharacterSurgeryWindowViewFactory viewFactory)
    {
        Fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        ViewFactory = viewFactory
            ?? throw new ArgumentNullException(nameof(viewFactory));
    }

    public ITmpKoreanFontService Fonts { get; }
    public ICharacterSurgeryWindowViewFactory ViewFactory { get; }
}

public sealed class CharacterSurgeryWindowService :
    ICharacterSurgeryWindowService,
    ISurgeryPlanningWindowService,
    ICharacterSurgeryWindowQuery,
    ICharacterSurgeryWindowCommand
{
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IWildlifeAnatomyHealthRuntime wildlifeAnatomy;
    private readonly IAnatomyProfileCatalog profiles;
    private readonly ISurgicalProcedureCatalog procedures;
    private readonly ISurgicalPartRuntime parts;
    private readonly ISurgicalAugmentationQuery augmentations;
    private readonly ISurgicalFacilityQuery facilities;
    private readonly ISurgeryRiskEvaluator risk;
    private readonly ISurgeryCommandService commands;
    private readonly ISurgeryQuery surgery;
    private readonly ISurgeryPolicyRuntime policies;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly ICaptivityRuntime captivity;
    private readonly IWildlifeCaptureRuntime wildlifeCapture;
    private readonly ISurgicalCorpseFreshnessRuntime corpseFreshness;
    private readonly ITmpKoreanFontService fonts;
    private readonly ICharacterSurgeryWindowViewFactory viewFactory;
    private readonly ISurgeryEnvironmentRiskEvaluator environmentRisk;
    private GameObject currentWindow;

    public CharacterSurgeryWindowService(
        SurgeryClinicalContext clinical,
        SurgeryExecutionContext execution,
        SurgerySubjectWorldContext subjectWorld,
        SurgeryWindowPresentationContext presentation)
    {
        clinical = clinical ?? throw new ArgumentNullException(nameof(clinical));
        execution = execution ?? throw new ArgumentNullException(nameof(execution));
        subjectWorld = subjectWorld
            ?? throw new ArgumentNullException(nameof(subjectWorld));
        anatomy = clinical.Anatomy;
        wildlifeAnatomy = clinical.WildlifeAnatomy;
        profiles = clinical.Profiles;
        procedures = clinical.Procedures;
        parts = clinical.Parts;
        augmentations = clinical.Augmentations;
        facilities = clinical.Facilities;
        risk = clinical.Risk;
        commands = execution.Commands;
        surgery = execution.Surgery;
        policies = execution.Policies;
        corpseFreshness = execution.CorpseFreshness;
        environmentRisk = execution.EnvironmentRisk;
        characters = subjectWorld.Characters;
        bodyHealth = subjectWorld.BodyHealth;
        captivity = subjectWorld.Captivity;
        wildlifeCapture = subjectWorld.WildlifeCapture;
        presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        fonts = presentation.Fonts;
        viewFactory = presentation.ViewFactory;
    }

    public void Open(CharacterActor actor, Transform uiHost)
    {
        if (actor == null || uiHost == null)
        {
            return;
        }

        Open(CreatePlanningSubject(actor), uiHost);
    }

    public void Open(WildlifeActor actor, Transform uiHost)
    {
        if (actor == null || !actor.IsAlive || uiHost == null)
        {
            return;
        }

        Open(CreatePlanningSubject(actor), uiHost);
    }

    public void Open(WorldItemStackSnapshot corpseStack, Transform uiHost)
    {
        if (!TryCreateCorpsePlanningSubject(
                corpseStack,
                out SurgeryPlanningSubject subject)
            || uiHost == null)
        {
            return;
        }

        Open(subject, uiHost);
    }

    private void Open(SurgeryPlanningSubject subject, Transform uiHost)
    {
        if (subject?.Subject?.IsValid != true || uiHost == null)
        {
            return;
        }

        if (currentWindow != null)
        {
            UnityEngine.Object.Destroy(currentWindow);
        }

        Canvas canvas = uiHost.GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : uiHost;
        ICharacterSurgeryWindowView view = viewFactory.Create(parent);
        currentWindow = view.Root;
        view.Configure(
            this,
            this,
            subject,
            fonts,
            () => currentWindow = null);
    }

    public CharacterSurgeryHealthProjection GetHealthSummary(
        CharacterActor actor)
    {
        if (actor == null)
        {
            return new CharacterSurgeryHealthProjection
            {
                Failure = new DomainFailure(
                    FailureCode.SurgerySubjectInvalid)
            };
        }

        AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(actor);
        if (!profiles.TryGet(
                snapshot.ProfileId,
                out AnatomyProfileDefinition profile))
        {
            return new CharacterSurgeryHealthProjection
            {
                Failure = new DomainFailure(
                    FailureCode.SurgeryAnatomyFamilyUnsupported,
                    snapshot.ProfileId)
            };
        }

        IReadOnlyList<AnatomyNodeHealthState> snapshotNodes =
            snapshot.Nodes ?? Array.Empty<AnatomyNodeHealthState>();
        IReadOnlyList<CharacterSurgeryNodeProjection> nodes = profile.Nodes
            .Select(definition =>
            {
                AnatomyNodeHealthState state = snapshotNodes.FirstOrDefault(
                    candidate => candidate != null
                        && string.Equals(
                            candidate.nodeId,
                            definition.NodeId,
                            StringComparison.Ordinal));
                return state == null
                    ? null
                    : new CharacterSurgeryNodeProjection
                    {
                        DisplayName = definition.DisplayName,
                        Missing = state.missing,
                        InstalledPartKind = state.installedPartKind,
                        HasInstalledPart =
                            state.installedPartKind
                                != SurgicalPartKind.NaturalOrgan
                            || !string.IsNullOrWhiteSpace(
                                state.installedPartId),
                        EffectiveEfficiency = state.EffectiveEfficiency,
                        CurrentHealth = state.currentHealth,
                        MaxHealth = state.maxHealth,
                        BleedingPerSecond = state.bleedingPerSecond,
                        Infection = state.infection,
                        RejectionBurden = state.rejectionBurden,
                        MutationBurden = state.mutationBurden
                    };
            })
            .Where(node => node != null)
            .ToArray();
        SurgeryOrder order = surgery.ActiveOrders.FirstOrDefault(candidate =>
            candidate?.subject != null
            && string.Equals(
                candidate.subject.subjectId,
                actor.Identity?.PersistentId,
                StringComparison.Ordinal));
        return new CharacterSurgeryHealthProjection
        {
            ProfileDisplayName = profile.DisplayName,
            Consciousness = snapshot.Consciousness,
            Sight = snapshot.Sight,
            Breathing = snapshot.Breathing,
            Digestion = snapshot.Digestion,
            Filtration = snapshot.Filtration,
            Manipulation = snapshot.Manipulation,
            Mobility = snapshot.Mobility,
            Nodes = nodes,
            ActiveOrder = CreateOrderProjection(order)
        };
    }

    public bool IsAutomaticEmergencyEnabled(CharacterActor actor)
    {
        return actor != null
            && policies.IsAutomaticEmergencySurgeryEnabled(CreateSubject(actor));
    }

    public void ToggleAutomaticEmergency(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        SurgicalSubjectRef subject = CreateSubject(actor);
        policies.SetAutomaticEmergencySurgery(
            subject,
            !policies.IsAutomaticEmergencySurgeryEnabled(subject));
    }

    public SurgeryWindowOptionsProjection GetOptions(
        SurgeryPlanningSubject subject,
        string procedureId)
    {
        IReadOnlyList<SurgicalProcedureSO> availableProcedures =
            GetProcedures(subject);
        SurgicalProcedureSO selectedProcedure = availableProcedures
            .FirstOrDefault(procedure => string.Equals(
                procedure.ProcedureId,
                procedureId,
                StringComparison.Ordinal))
            ?? availableProcedures.FirstOrDefault();
        return new SurgeryWindowOptionsProjection
        {
            Procedures = availableProcedures
                .Select(procedure => new SurgeryWindowOption(
                    procedure.ProcedureId,
                    procedure.DisplayName))
                .ToArray(),
            Nodes = GetNodes(subject)
                .Select(node => new SurgeryWindowOption(
                    node.NodeId,
                    node.DisplayName))
                .ToArray(),
            Parts = GetParts(selectedProcedure)
                .Select(part => new SurgeryWindowOption(
                    part.partInstanceId,
                    GetPartLabel(part)))
                .ToArray(),
            Doctors = GetDoctors(subject)
                .Select(doctor => new SurgeryWindowOption(
                    doctor.Identity?.PersistentId,
                    doctor.Identity?.DisplayName))
                .ToArray(),
            Facilities = GetFacilities(selectedProcedure)
                .Where(facility => facility.PrimaryFacility != null)
                .Select(facility => new SurgeryWindowOption(
                    facilities.GetFacilityId(facility.PrimaryFacility),
                    facility.PrimaryFacility.BuildingData?.objectName
                        ?? facility.PrimaryFacility.name))
                .ToArray()
        };
    }

    public SurgeryWindowDetailsProjection GetDetails(
        SurgeryPlanningSubject subject,
        SurgeryWindowSelection selection)
    {
        ResolveSelection(
            subject,
            selection,
            out SurgicalProcedureSO procedure,
            out AnatomyNodeDefinition node,
            out SurgicalPartInstance part,
            out CharacterActor doctor,
            out SurgicalFacilitySnapshot facility);
        if (procedure == null)
        {
            return new SurgeryWindowDetailsProjection
            {
                ProcedureLabel = "Unavailable",
                NodeLabel = node?.DisplayName ?? "-",
                PartLabel = "-",
                DoctorLabel = doctor?.Identity?.DisplayName ?? "-",
                FacilityLabel = "-",
                BodyText = FailureCode.SurgerySubjectInvalid.ToString()
            };
        }

        SurgeryRiskBreakdown breakdown = EvaluateRisk(
            subject,
            procedure,
            part,
            doctor,
            facility);
        SurgeryRiskBreakdown baseBreakdown = EvaluateBaseRisk(
            subject,
            procedure,
            part,
            doctor,
            facility);
        StringBuilder builder = new StringBuilder(640);
        builder.AppendLine(procedure.Description);
        if (subject?.IsCorpse == true)
        {
            builder.AppendLine(subject.CorpseFreshnessSeconds <= 0f
                ? FailureCode.SurgerySubjectInvalid.ToString()
                : $"Corpse freshness: {subject.CorpseFreshnessSeconds / 180f:0.0}");
        }

        builder.AppendLine();
        builder.AppendLine(
            $"Work {procedure.RequiredWork:0.#} / "
            + CharacterSurgeryUiText.FormatFacilityTags(
                procedure.RequiredFacilityTags));
        builder.AppendLine(
            $"Success {breakdown.successChance * 100f:0.#}%"
            + $" / Infection {breakdown.infectionChance * 100f:0.#}%"
            + $" / Bleeding {breakdown.bleedingChance * 100f:0.#}%");
        builder.AppendLine(
            $"Organ damage {breakdown.organDamageChance * 100f:0.#}%"
            + $" / Death {breakdown.deathChance * 100f:0.#}%");
        builder.AppendLine(CharacterSurgeryUiText.LocalizeRisk(breakdown));
        if (facility.PrimaryFacility == null)
        {
            builder.AppendLine(FailureCode.SurgeryFacilityUnavailable.ToString());
        }
        else if (RequiresPart(procedure.Kind) && part == null)
        {
            builder.AppendLine(FailureCode.SurgeryPartUnavailable.ToString());
        }

        builder.AppendLine();
        builder.AppendLine(
            $"Base / Success {baseBreakdown.successChance * 100f:0.#}%"
            + $" / Infection {baseBreakdown.infectionChance * 100f:0.#}%"
            + $" / Bleeding {baseBreakdown.bleedingChance * 100f:0.#}%"
            + $" / Organ damage {baseBreakdown.organDamageChance * 100f:0.#}%"
            + $" / Death {baseBreakdown.deathChance * 100f:0.#}%");
        if (TryEvaluateEnvironmentRisk(
                subject,
                doctor,
                facility,
                out SurgeryEnvironmentRiskSnapshot environment))
        {
            builder.AppendLine(
                $"Environment / {environment.Environment.TemperatureC:0.#} C"
                + $" / Air {environment.Environment.AirQuality:0}"
                + $" / Light {environment.Environment.LightLevel:0}");
            string warning = CharacterSurgeryUiText.FormatEnvironmentRisk(
                environment);
            if (!string.IsNullOrWhiteSpace(warning))
            {
                builder.AppendLine(warning);
            }
        }

        SurgeryOrderUiProjection activeOrder =
            GetActiveOrderProjection(subject);
        if (activeOrder?.State == SurgeryOrderState.EnvironmentWaiting)
        {
            builder.AppendLine(
                CharacterSurgeryUiText.FormatEnvironmentWait(activeOrder));
        }

        return new SurgeryWindowDetailsProjection
        {
            ProcedureLabel = procedure.DisplayName,
            NodeLabel = node?.DisplayName ?? "-",
            PartLabel = part != null
                ? GetPartLabel(part)
                : RequiresPart(procedure.Kind)
                    ? FailureCode.SurgeryPartUnavailable.ToString()
                    : "-",
            DoctorLabel = doctor?.Identity?.DisplayName ?? "-",
            FacilityLabel = facility.PrimaryFacility != null
                ? facility.PrimaryFacility.BuildingData?.objectName
                    ?? facility.PrimaryFacility.name
                : "-",
            BodyText = builder.ToString().TrimEnd()
        };
    }

    public SurgeryUiCommandResult Schedule(
        SurgeryPlanningSubject subject,
        SurgeryWindowSelection selection)
    {
        ResolveSelection(
            subject,
            selection,
            out SurgicalProcedureSO procedure,
            out AnatomyNodeDefinition node,
            out SurgicalPartInstance part,
            out CharacterActor doctor,
            out SurgicalFacilitySnapshot facility);
        return TrySchedule(
            subject,
            procedure,
            node,
            part,
            doctor,
            facility);
    }

    public SurgeryUiCommandResult Cancel(SurgeryPlanningSubject subject) =>
        TryCancel(subject);

    private void ResolveSelection(
        SurgeryPlanningSubject subject,
        SurgeryWindowSelection selection,
        out SurgicalProcedureSO procedure,
        out AnatomyNodeDefinition node,
        out SurgicalPartInstance part,
        out CharacterActor doctor,
        out SurgicalFacilitySnapshot facility)
    {
        procedure = GetProcedures(subject).FirstOrDefault(candidate =>
            string.Equals(
                candidate.ProcedureId,
                selection.ProcedureId,
                StringComparison.Ordinal));
        node = GetNodes(subject).FirstOrDefault(candidate =>
            string.Equals(
                candidate.NodeId,
                selection.NodeId,
                StringComparison.Ordinal));
        part = GetParts(procedure).FirstOrDefault(candidate =>
            string.Equals(
                candidate.partInstanceId,
                selection.PartId,
                StringComparison.Ordinal));
        doctor = GetDoctors(subject).FirstOrDefault(candidate =>
            string.Equals(
                candidate.Identity?.PersistentId,
                selection.DoctorId,
                StringComparison.Ordinal));
        facility = GetFacilities(procedure).FirstOrDefault(candidate =>
            candidate.PrimaryFacility != null
            && string.Equals(
                facilities.GetFacilityId(candidate.PrimaryFacility),
                selection.FacilityId,
                StringComparison.Ordinal));
    }

    internal IReadOnlyList<SurgicalProcedureSO> GetProcedures(
        SurgeryPlanningSubject patient)
    {
        return procedures.Procedures
            .Where(procedure => procedure != null
                && (patient?.IsCorpse == true
                    ? procedure.AllowsCorpseSubject
                    : procedure.AllowsLivingSubject)
                && (patient?.Subject?.kind != SurgicalSubjectKind.Wildlife
                    && patient?.Subject?.kind != SurgicalSubjectKind.WildlifeCorpse
                    || procedure.AllowsWildlife))
            .OrderBy(procedure => procedure.RequiredWork)
            .ThenBy(procedure => procedure.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    internal IReadOnlyList<AnatomyNodeDefinition> GetNodes(
        SurgeryPlanningSubject patient)
    {
        return patient?.Nodes ?? Array.Empty<AnatomyNodeDefinition>();
    }

    internal IReadOnlyList<SurgicalPartInstance> GetParts(
        SurgicalProcedureSO procedure)
    {
        if (procedure == null || !RequiresPart(procedure.Kind))
        {
            return Array.Empty<SurgicalPartInstance>();
        }

        return parts.Parts
            .Where(part => part != null
                && !part.installed
                && string.IsNullOrWhiteSpace(part.reservedOrderId)
                && PartMatchesProcedure(part.kind, procedure.Kind))
            .OrderByDescending(part => part.quality)
            .ThenBy(part => part.displayName, StringComparer.Ordinal)
            .ToArray();
    }

    internal IReadOnlyList<CharacterActor> GetDoctors(
        SurgeryPlanningSubject patient)
    {
        string patientCharacterId =
            patient?.Subject?.kind == SurgicalSubjectKind.Character
                ? patient.Subject.subjectId
                : string.Empty;
        return characters.Characters
            .Where(actor => actor != null
                && !actor.IsDead
                && actor.characterType == CharacterType.NPC
                && !string.Equals(
                    actor.Identity?.PersistentId,
                    patientCharacterId,
                    StringComparison.Ordinal)
                && !captivity.IsCaptive(actor.Identity?.PersistentId))
            .OrderByDescending(GetMedicalScore)
            .ThenBy(actor => actor.Identity?.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    internal string GetPartLabel(SurgicalPartInstance part)
    {
        if (part == null)
        {
            return string.Empty;
        }

        string effect = augmentations.GetSpecialEffectLabel(part);
        return string.IsNullOrWhiteSpace(effect)
            ? $"{part.displayName} · 품질 {part.quality:0.00}"
            : $"{part.displayName} · 품질 {part.quality:0.00} · {effect}";
    }

    internal IReadOnlyList<SurgicalFacilitySnapshot> GetFacilities(
        SurgicalProcedureSO procedure)
    {
        return facilities.GetCandidateFacilities(procedure);
    }

    internal SurgeryRiskBreakdown EvaluateRisk(
        SurgeryPlanningSubject patient,
        SurgicalProcedureSO procedure,
        SurgicalPartInstance part,
        CharacterActor doctor,
        SurgicalFacilitySnapshot facility)
    {
        float compatibilityPenalty = part == null
            ? 0f
            : ResolveCompatibility(patient?.Subject, part);
        SurgeryRiskBreakdown baseline = risk.Evaluate(
            doctor,
            patient?.Subject,
            procedure,
            facility,
            patient?.Instability ?? 0f,
            compatibilityPenalty);
        if (facility.PrimaryFacility == null)
        {
            return baseline;
        }

        SurgeryEnvironmentRiskSnapshot snapshot =
            environmentRisk.Evaluate(
                facility.PrimaryFacility.centerPos,
                doctor,
                patient?.Subject);
        return environmentRisk.Apply(
            baseline,
            snapshot,
            stageWeight: 1f);
    }

    internal SurgeryRiskBreakdown EvaluateBaseRisk(
        SurgeryPlanningSubject patient,
        SurgicalProcedureSO procedure,
        SurgicalPartInstance part,
        CharacterActor doctor,
        SurgicalFacilitySnapshot facility)
    {
        float compatibilityPenalty = part == null
            ? 0f
            : ResolveCompatibility(patient?.Subject, part);
        return risk.Evaluate(
            doctor,
            patient?.Subject,
            procedure,
            facility,
            patient?.Instability ?? 0f,
            compatibilityPenalty);
    }

    internal bool TryEvaluateEnvironmentRisk(
        SurgeryPlanningSubject patient,
        CharacterActor doctor,
        SurgicalFacilitySnapshot facility,
        out SurgeryEnvironmentRiskSnapshot snapshot)
    {
        snapshot = default;
        if (facility.PrimaryFacility == null)
        {
            return false;
        }

        snapshot = environmentRisk.Evaluate(
            facility.PrimaryFacility.centerPos,
            doctor,
            patient?.Subject);
        return true;
    }

    internal SurgeryOrderUiProjection GetActiveOrderProjection(
        SurgeryPlanningSubject patient)
    {
        SurgeryOrder order = surgery.ActiveOrders.FirstOrDefault(candidate =>
            candidate?.subject != null
            && string.Equals(
                candidate.subject.subjectId,
                patient?.Subject?.subjectId,
                StringComparison.Ordinal));
        return CreateOrderProjection(order);
    }

    internal SurgeryUiCommandResult TrySchedule(
        SurgeryPlanningSubject patient,
        SurgicalProcedureSO procedure,
        AnatomyNodeDefinition node,
        SurgicalPartInstance part,
        CharacterActor doctor,
        SurgicalFacilitySnapshot facility)
    {
        if (patient?.Subject?.IsValid != true || procedure == null || node == null)
        {
            return SurgeryUiCommandResult.Rejected(
                FailureCode.SurgerySubjectInvalid);
        }

        bool scheduled = commands.TrySchedule(
            patient.Subject,
            procedure.ProcedureId,
            node.NodeId,
            part?.partInstanceId ?? string.Empty,
            doctor?.Identity?.PersistentId ?? string.Empty,
            facility.PrimaryFacility != null
                ? facilities.GetFacilityId(facility.PrimaryFacility)
                : string.Empty,
            out SurgeryOrder order,
            out DomainFailure failure);
        return scheduled
            ? SurgeryUiCommandResult.Success(order?.orderId)
            : SurgeryUiCommandResult.Rejected(failure);
    }

    internal SurgeryUiCommandResult TryCancel(
        SurgeryPlanningSubject patient)
    {
        SurgeryOrder order = surgery.ActiveOrders.FirstOrDefault(candidate =>
            candidate?.subject != null
            && string.Equals(
                candidate.subject.subjectId,
                patient?.Subject?.subjectId,
                StringComparison.Ordinal));
        if (order == null)
        {
            return SurgeryUiCommandResult.Rejected(
                FailureCode.SurgeryOrderMissing);
        }

        bool cancelled = commands.TryCancel(
            order.orderId,
            out DomainFailure failure);
        return cancelled
            ? SurgeryUiCommandResult.Success(order.orderId)
            : SurgeryUiCommandResult.Rejected(failure);
    }

    private SurgicalSubjectRef CreateSubject(CharacterActor actor)
    {
        AnatomyProfileDefinition profile = actor != null
            ? profiles.GetForSpecies(actor.Identity?.SpeciesTag)
            : null;
        return new SurgicalSubjectRef
        {
            kind = SurgicalSubjectKind.Character,
            subjectId = actor?.Identity?.PersistentId ?? string.Empty,
            displayName = actor?.Identity?.DisplayName ?? string.Empty,
            speciesId = actor?.Identity?.SpeciesTag ?? string.Empty,
            anatomyProfileId = profile?.ProfileId ?? string.Empty,
            willing = actor != null
                && actor.characterType == CharacterType.NPC
                && !captivity.IsCaptive(actor.Identity?.PersistentId),
            automaticEmergencyDefault =
                actor != null
                && actor.characterType == CharacterType.NPC
                && !captivity.IsCaptive(actor.Identity?.PersistentId)
        };
    }

    private SurgeryPlanningSubject CreatePlanningSubject(CharacterActor actor)
    {
        AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(actor);
        AnatomyProfileDefinition profile = profiles.TryGet(
            snapshot.ProfileId,
            out AnatomyProfileDefinition resolved)
                ? resolved
                : profiles.GetForSpecies(actor.Identity?.SpeciesTag);
        CharacterBodyHealthSnapshot body = bodyHealth.GetSnapshot(actor);
        CharacterStats patientStats = actor.GetComponent<CharacterStats>();
        float healthRatio = patientStats != null
            ? Mathf.Clamp01(
                patientStats.CurrentHealth
                / Mathf.Max(1f, patientStats.MaxHealth))
            : 1f;
        return new SurgeryPlanningSubject
        {
            Subject = CreateSubject(actor),
            DisplayName = actor.Identity?.DisplayName ?? actor.name,
            Nodes = profile?.Nodes ?? Array.Empty<AnatomyNodeDefinition>(),
            Instability = Mathf.Clamp01(
                body.BloodLoss / 100f * 0.7f
                + (body.Downed ? 0.2f : 0f)
                + (1f - healthRatio) * 0.25f)
        };
    }

    private SurgeryPlanningSubject CreatePlanningSubject(WildlifeActor actor)
    {
        AnatomyHealthSnapshot snapshot = wildlifeAnatomy.GetAnatomySnapshot(actor);
        AnatomyProfileDefinition profile = profiles.TryGet(
            snapshot.ProfileId,
            out AnatomyProfileDefinition resolved)
                ? resolved
                : profiles.GetForSpecies(actor.SpeciesId);
        float healthRatio = Mathf.Clamp01(
            actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth));
        bool captured = wildlifeCapture.IsCaptured(actor.WildlifeId);
        return new SurgeryPlanningSubject
        {
            Subject = new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.Wildlife,
                subjectId = actor.WildlifeId,
                displayName = actor.DisplayName,
                speciesId = actor.SpeciesId,
                anatomyProfileId = profile?.ProfileId ?? string.Empty,
                willing = false,
                automaticEmergencyDefault = false
            },
            DisplayName = actor.DisplayName,
            Nodes = profile?.Nodes ?? Array.Empty<AnatomyNodeDefinition>(),
            Instability = Mathf.Clamp01(
                (1f - healthRatio) * 0.55f
                + (captured ? 0f : 0.2f))
        };
    }

    private bool TryCreateCorpsePlanningSubject(
        WorldItemStackSnapshot stack,
        out SurgeryPlanningSubject subject)
    {
        subject = null;
        if (stack == null)
        {
            return false;
        }

        bool humanoid = string.Equals(
            stack.ItemId,
            DarkSurvivalItemDefinitions.HumanoidCorpseItemId,
            StringComparison.Ordinal);
        string wildlifeSpeciesId =
            WildlifeItemDefinitions.GetSpeciesIdFromCarcass(stack.ItemId);
        bool wildlife = !string.IsNullOrWhiteSpace(wildlifeSpeciesId);
        if (!humanoid && !wildlife)
        {
            return false;
        }

        string speciesId = humanoid
            ? stack.SourceSpeciesTag
            : wildlifeSpeciesId;
        corpseFreshness.TryGetFreshness(
            stack.StackId,
            out float remainingFreshness,
            out _);
        AnatomyProfileDefinition profile = profiles.GetForSpecies(speciesId);
        subject = new SurgeryPlanningSubject
        {
            Subject = new SurgicalSubjectRef
            {
                kind = humanoid
                    ? SurgicalSubjectKind.HumanoidCorpse
                    : SurgicalSubjectKind.WildlifeCorpse,
                subjectId = stack.StackId,
                displayName = string.IsNullOrWhiteSpace(stack.SourceDisplayName)
                    ? stack.DisplayName
                    : stack.SourceDisplayName,
                speciesId = speciesId ?? string.Empty,
                anatomyProfileId = profile?.ProfileId ?? string.Empty,
                willing = false,
                automaticEmergencyDefault = false
            },
            DisplayName = string.IsNullOrWhiteSpace(stack.SourceDisplayName)
                ? stack.DisplayName
                : stack.SourceDisplayName,
            Nodes = profile?.Nodes ?? Array.Empty<AnatomyNodeDefinition>(),
            Instability = Mathf.Clamp01(stack.Contamination / 100f * 0.6f),
            CorpseFreshnessSeconds = remainingFreshness
        };
        return true;
    }

    private float ResolveCompatibility(
        SurgicalSubjectRef recipient,
        SurgicalPartInstance part)
    {
        if (recipient == null || part == null)
        {
            return 0f;
        }

        AnatomyProfileDefinition recipientProfile =
            profiles.GetForSpecies(recipient.speciesId);
        float compatibility = string.Equals(
            recipient.speciesId,
            part.donorSpeciesId,
            StringComparison.OrdinalIgnoreCase)
            ? 1f
            : string.Equals(
                recipientProfile.AnatomyFamily,
                part.anatomyFamily,
                StringComparison.OrdinalIgnoreCase)
                ? 0.75f
                : part.kind == SurgicalPartKind.ArcaneGraft
                    ? 0.2f
                    : 0.45f;
        return (1f - compatibility) * 0.35f;
    }

    private SurgeryOrderUiProjection CreateOrderProjection(
        SurgeryOrder order)
    {
        if (order == null)
        {
            return null;
        }

        string procedureName = procedures.TryGet(
            order.procedureId,
            out SurgicalProcedureSO procedure)
                ? procedure.DisplayName
                : order.procedureId;
        return new SurgeryOrderUiProjection
        {
            OrderId = order.orderId,
            ProcedureName = procedureName,
            DoctorId = order.doctorId,
            State = order.state,
            EnvironmentResumeStage = order.environmentResumeStage,
            EnvironmentStableSeconds = order.environmentStableSeconds,
            Progress01 = order.Progress01,
            Status = order.statusData?.Clone() ?? new SurgeryStatusData(),
            EnvironmentWait = order.environmentWait?.Clone()
                ?? new SurgeryStatusData(),
            EnvironmentRecovery = order.environmentRecovery?.Clone()
                ?? new SurgeryStatusData()
        };
    }

    private static float GetMedicalScore(CharacterActor actor)
    {
        return actor == null
            ? 0f
            : actor.GetCharacterStat(CharacterStatType.Medical) * 0.65f
                + actor.GetCharacterStat(CharacterStatType.Dexterity) * 0.25f
                + actor.GetCharacterStat(CharacterStatType.Research) * 0.1f;
    }

    private static bool RequiresPart(SurgicalProcedureKind kind)
    {
        return kind is SurgicalProcedureKind.TransplantOrgan
            or SurgicalProcedureKind.InstallProsthetic
            or SurgicalProcedureKind.InstallImplant
            or SurgicalProcedureKind.ArcaneModification;
    }

    private static bool PartMatchesProcedure(
        SurgicalPartKind part,
        SurgicalProcedureKind procedure)
    {
        return procedure switch
        {
            SurgicalProcedureKind.TransplantOrgan =>
                part == SurgicalPartKind.NaturalOrgan,
            SurgicalProcedureKind.InstallProsthetic =>
                part == SurgicalPartKind.Prosthetic,
            SurgicalProcedureKind.InstallImplant =>
                part == SurgicalPartKind.Implant,
            SurgicalProcedureKind.ArcaneModification =>
                part is SurgicalPartKind.ArcaneGraft
                    or SurgicalPartKind.Implant,
            _ => true
        };
    }

}
