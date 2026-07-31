using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface ICharacterSurgeryWindowService
{
    void Open(CharacterActor actor, Transform uiHost);
    string BuildHealthSummary(CharacterActor actor);
    bool IsAutomaticEmergencyEnabled(CharacterActor actor);
    void ToggleAutomaticEmergency(CharacterActor actor);
}

public interface ISurgeryPlanningWindowService
{
    void Open(WildlifeActor actor, Transform uiHost);
    void Open(WorldItemStackSnapshot corpseStack, Transform uiHost);
}

internal sealed class SurgeryPlanningSubject
{
    public SurgicalSubjectRef Subject { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<AnatomyNodeDefinition> Nodes { get; set; } =
        Array.Empty<AnatomyNodeDefinition>();
    public float Instability { get; set; }
    public float CorpseFreshnessSeconds { get; set; }
    public bool IsCorpse => Subject?.kind is SurgicalSubjectKind.HumanoidCorpse
        or SurgicalSubjectKind.WildlifeCorpse;
}

public sealed class CharacterSurgeryWindowService :
    ICharacterSurgeryWindowService,
    ISurgeryPlanningWindowService
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
    private readonly ISurgeryRuntime surgery;
    private readonly ISurgeryPolicyRuntime policies;
    private readonly ICharacterWorldQuery characters;
    private readonly ICharacterBodyHealthRuntime bodyHealth;
    private readonly ICaptivityRuntime captivity;
    private readonly IWildlifeCaptureRuntime wildlifeCapture;
    private readonly ISurgicalCorpseFreshnessRuntime corpseFreshness;
    private readonly ITmpKoreanFontService fonts;
    private readonly ISurgeryEnvironmentRiskEvaluator environmentRisk;
    private GameObject currentWindow;

    public CharacterSurgeryWindowService(
        IAnatomyHealthRuntime anatomy,
        IWildlifeAnatomyHealthRuntime wildlifeAnatomy,
        IAnatomyProfileCatalog profiles,
        ISurgicalProcedureCatalog procedures,
        ISurgicalPartRuntime parts,
        ISurgicalAugmentationQuery augmentations,
        ISurgicalFacilityQuery facilities,
        ISurgeryRiskEvaluator risk,
        ISurgeryCommandService commands,
        ISurgeryRuntime surgery,
        ISurgeryPolicyRuntime policies,
        ICharacterWorldQuery characters,
        ICharacterBodyHealthRuntime bodyHealth,
        ICaptivityRuntime captivity,
        IWildlifeCaptureRuntime wildlifeCapture,
        ISurgicalCorpseFreshnessRuntime corpseFreshness,
        ITmpKoreanFontService fonts,
        ISurgeryEnvironmentRiskEvaluator environmentRisk = null)
    {
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.wildlifeAnatomy = wildlifeAnatomy
            ?? throw new ArgumentNullException(nameof(wildlifeAnatomy));
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.procedures = procedures ?? throw new ArgumentNullException(nameof(procedures));
        this.parts = parts ?? throw new ArgumentNullException(nameof(parts));
        this.augmentations = augmentations
            ?? throw new ArgumentNullException(nameof(augmentations));
        this.facilities = facilities ?? throw new ArgumentNullException(nameof(facilities));
        this.risk = risk ?? throw new ArgumentNullException(nameof(risk));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.surgery = surgery ?? throw new ArgumentNullException(nameof(surgery));
        this.policies = policies ?? throw new ArgumentNullException(nameof(policies));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.wildlifeCapture = wildlifeCapture
            ?? throw new ArgumentNullException(nameof(wildlifeCapture));
        this.corpseFreshness = corpseFreshness
            ?? throw new ArgumentNullException(nameof(corpseFreshness));
        this.fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        this.environmentRisk = environmentRisk;
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
        currentWindow = new GameObject(
            "CharacterSurgeryWindow",
            typeof(RectTransform),
            typeof(Image),
            typeof(CharacterSurgeryWindowView));
        currentWindow.transform.SetParent(parent, false);
        CharacterSurgeryWindowView view =
            currentWindow.GetComponent<CharacterSurgeryWindowView>();
        view.Configure(this, subject, fonts, () => currentWindow = null);
    }

    public string BuildHealthSummary(CharacterActor actor)
    {
        if (actor == null)
        {
            return "해부 정보가 없습니다.";
        }

        AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(actor);
        if (!profiles.TryGet(snapshot.ProfileId, out AnatomyProfileDefinition profile))
        {
            return "해부 프로필을 찾을 수 없습니다.";
        }

        StringBuilder builder = new StringBuilder(1024);
        builder.AppendLine();
        builder.AppendLine($"신체·장기 · {profile.DisplayName}");
        builder.AppendLine(
            $"의식 {snapshot.Consciousness * 100f:0}% · 시야 {snapshot.Sight * 100f:0}%"
            + $" · 호흡 {snapshot.Breathing * 100f:0}%");
        builder.AppendLine(
            $"소화 {snapshot.Digestion * 100f:0}% · 여과 {snapshot.Filtration * 100f:0}%"
            + $" · 조작 {snapshot.Manipulation * 100f:0}% · 이동 {snapshot.Mobility * 100f:0}%");
        foreach (AnatomyNodeDefinition definition in profile.Nodes)
        {
            AnatomyNodeHealthState state = snapshot.Nodes.FirstOrDefault(node =>
                node != null
                && string.Equals(
                    node.nodeId,
                    definition.NodeId,
                    StringComparison.Ordinal));
            if (state == null)
            {
                continue;
            }

            string condition = state.missing
                ? "결손"
                : state.installedPartKind != SurgicalPartKind.NaturalOrgan
                    || !string.IsNullOrWhiteSpace(state.installedPartId)
                        ? $"{FormatPartKind(state.installedPartKind)} {state.EffectiveEfficiency * 100f:0}%"
                        : $"{state.currentHealth:0.#}/{state.maxHealth:0.#}";
            builder.Append($"- {definition.DisplayName}: {condition}");
            if (state.bleedingPerSecond > 0.001f)
            {
                builder.Append($" · 출혈 {state.bleedingPerSecond:0.##}/초");
            }

            if (state.infection > 0.1f)
            {
                builder.Append($" · 감염 {state.infection:0.#}");
            }

            if (state.rejectionBurden > 0.1f)
            {
                builder.Append($" · 거부 {state.rejectionBurden:0.#}");
            }

            if (state.mutationBurden > 0.1f)
            {
                builder.Append($" · 변이 {state.mutationBurden:0.#}");
            }

            builder.AppendLine();
        }

        SurgeryOrder order = surgery.ActiveOrders.FirstOrDefault(candidate =>
            candidate?.subject != null
            && string.Equals(
                candidate.subject.subjectId,
                actor.Identity?.PersistentId,
                StringComparison.Ordinal));
        builder.AppendLine();
        if (order == null)
        {
            builder.AppendLine("수술 대기열 없음");
        }
        else
        {
            string procedureName = procedures.TryGet(
                order.procedureId,
                out SurgicalProcedureSO procedure)
                ? procedure.DisplayName
                : order.procedureId;
            builder.AppendLine(
                $"수술 대기 · {procedureName} · {order.status}"
                + $" · {order.Progress01 * 100f:0}%");
            if (!string.IsNullOrWhiteSpace(order.doctorId))
            {
                builder.AppendLine($"집도의 {order.doctorId}");
            }
        }

        return builder.ToString().TrimEnd();
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
        if (environmentRisk == null
            || facility.PrimaryFacility == null)
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
        if (environmentRisk == null
            || facility.PrimaryFacility == null)
        {
            return false;
        }

        snapshot = environmentRisk.Evaluate(
            facility.PrimaryFacility.centerPos,
            doctor,
            patient?.Subject);
        return true;
    }

    internal SurgeryOrder GetActiveOrder(SurgeryPlanningSubject patient)
    {
        return surgery.ActiveOrders.FirstOrDefault(candidate =>
            candidate?.subject != null
            && string.Equals(
                candidate.subject.subjectId,
                patient?.Subject?.subjectId,
                StringComparison.Ordinal));
    }

    internal bool TrySchedule(
        SurgeryPlanningSubject patient,
        SurgicalProcedureSO procedure,
        AnatomyNodeDefinition node,
        SurgicalPartInstance part,
        CharacterActor doctor,
        SurgicalFacilitySnapshot facility,
        out string message)
    {
        if (patient?.Subject?.IsValid != true || procedure == null || node == null)
        {
            message = "수술 대상, 절차와 부위를 모두 선택해야 합니다.";
            return false;
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
            out _,
            out message);
        return scheduled;
    }

    internal bool TryCancel(SurgeryPlanningSubject patient, out string message)
    {
        SurgeryOrder order = surgery.ActiveOrders.FirstOrDefault(candidate =>
            candidate?.subject != null
            && string.Equals(
                candidate.subject.subjectId,
                patient?.Subject?.subjectId,
                StringComparison.Ordinal));
        if (order == null)
        {
            message = "취소할 수술 주문이 없습니다.";
            return false;
        }

        return commands.TryCancel(order.orderId, out message);
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
        bool wildlife = WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
            stack.ItemId,
            out string wildlifeSpeciesId);
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

    private static string FormatPartKind(SurgicalPartKind kind)
    {
        return kind switch
        {
            SurgicalPartKind.Prosthetic => "보철",
            SurgicalPartKind.Implant => "임플란트",
            SurgicalPartKind.ArcaneGraft => "이형 이식",
            _ => "이식 장기"
        };
    }
}

public sealed class CharacterSurgeryWindowView : MonoBehaviour
{
    private CharacterSurgeryWindowService service;
    private SurgeryPlanningSubject patient;
    private ITmpKoreanFontService fonts;
    private Action onClosed;
    private TMP_Text procedureValue;
    private TMP_Text nodeValue;
    private TMP_Text partValue;
    private TMP_Text doctorValue;
    private TMP_Text facilityValue;
    private TMP_Text details;
    private RectTransform panel;
    private IReadOnlyList<SurgicalProcedureSO> procedureOptions =
        Array.Empty<SurgicalProcedureSO>();
    private IReadOnlyList<AnatomyNodeDefinition> nodeOptions =
        Array.Empty<AnatomyNodeDefinition>();
    private IReadOnlyList<SurgicalPartInstance> partOptions =
        Array.Empty<SurgicalPartInstance>();
    private IReadOnlyList<CharacterActor> doctorOptions =
        Array.Empty<CharacterActor>();
    private IReadOnlyList<SurgicalFacilitySnapshot> facilityOptions =
        Array.Empty<SurgicalFacilitySnapshot>();
    private int procedureIndex;
    private int nodeIndex;
    private int partIndex;
    private int doctorIndex;
    private int facilityIndex;

    internal void Configure(
        CharacterSurgeryWindowService service,
        SurgeryPlanningSubject patient,
        ITmpKoreanFontService fonts,
        Action onClosed)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        this.patient = patient ?? throw new ArgumentNullException(nameof(patient));
        this.fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        this.onClosed = onClosed;
        Build();
        RefreshOptions(resetProcedureDependent: true);
    }

    private void Build()
    {
        RectTransform root = transform as RectTransform;
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;
        GetComponent<Image>().color = DungeonUiTheme.ModalScrim;

        panel = CreateRect("SurgeryPanel", transform);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        ApplyResponsivePanelSize();
        panel.gameObject.AddComponent<Image>().color = DungeonUiTheme.Panel;
        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 18, 18);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        TMP_Text title = CreateText(
            "Title",
            panel,
            $"{patient.DisplayName} 수술 계획",
            25f,
            FontStyles.Bold);
        title.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;

        procedureValue = CreateSelector(
            panel,
            "수술",
            delta =>
            {
                procedureIndex = Wrap(procedureIndex + delta, procedureOptions.Count);
                RefreshOptions(resetProcedureDependent: true);
            });
        nodeValue = CreateSelector(
            panel,
            "대상 부위",
            delta =>
            {
                nodeIndex = Wrap(nodeIndex + delta, nodeOptions.Count);
                RefreshDetails();
            });
        partValue = CreateSelector(
            panel,
            "장기·보철",
            delta =>
            {
                partIndex = Wrap(partIndex + delta, partOptions.Count);
                RefreshDetails();
            });
        doctorValue = CreateSelector(
            panel,
            "집도의",
            delta =>
            {
                doctorIndex = Wrap(doctorIndex + delta, doctorOptions.Count);
                RefreshDetails();
            });
        facilityValue = CreateSelector(
            panel,
            "집도 시설",
            delta =>
            {
                facilityIndex = Wrap(facilityIndex + delta, facilityOptions.Count);
                RefreshDetails();
            });

        details = CreateText(
            "RiskDetails",
            panel,
            string.Empty,
            15f,
            FontStyles.Normal);
        details.color = DungeonUiTheme.TextSecondary;
        details.textWrappingMode = TextWrappingModes.Normal;
        details.overflowMode = TextOverflowModes.Ellipsis;
        LayoutElement detailsLayout = details.gameObject.AddComponent<LayoutElement>();
        detailsLayout.flexibleHeight = 1f;
        detailsLayout.minHeight = 170f;

        RectTransform footer = CreateRect("Footer", panel);
        footer.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
        HorizontalLayoutGroup footerLayout =
            footer.gameObject.AddComponent<HorizontalLayoutGroup>();
        footerLayout.spacing = 8f;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = true;
        footerLayout.childForceExpandWidth = true;
        footerLayout.childForceExpandHeight = true;
        CreateButton("Schedule", footer, "수술 예약", Schedule);
        CreateButton("CancelOrder", footer, "예약 취소", CancelOrder, destructive: true);
        CreateButton("Close", footer, "닫기", Close);
        fonts.ApplyToChildren(transform);
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyResponsivePanelSize();
    }

    private void ApplyResponsivePanelSize()
    {
        if (panel == null || transform is not RectTransform root)
        {
            return;
        }

        const float edgeMargin = 48f;
        float availableWidth = Mathf.Max(0f, root.rect.width - edgeMargin);
        float availableHeight = Mathf.Max(0f, root.rect.height - edgeMargin);
        panel.sizeDelta = new Vector2(
            Mathf.Min(1180f, availableWidth),
            Mathf.Min(840f, availableHeight));
    }

    private TMP_Text CreateSelector(
        Transform parent,
        string label,
        Action<int> change)
    {
        RectTransform row = CreateRect(label + "Row", parent);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
        HorizontalLayoutGroup layout =
            row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childControlWidth = false;
        layout.childForceExpandHeight = true;
        layout.childForceExpandWidth = false;

        TMP_Text labelText = CreateText(
            "Label",
            row,
            label,
            15f,
            FontStyles.Bold);
        labelText.gameObject.AddComponent<LayoutElement>().preferredWidth = 112f;
        CreateButton("Previous", row, "<", () => change(-1))
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 46f;
        TMP_Text value = CreateText(
            "Value",
            row,
            "-",
            15f,
            FontStyles.Normal);
        LayoutElement valueLayout = value.gameObject.AddComponent<LayoutElement>();
        valueLayout.flexibleWidth = 1f;
        value.alignment = TextAlignmentOptions.MidlineLeft;
        CreateButton("Next", row, ">", () => change(1))
            .gameObject.AddComponent<LayoutElement>().preferredWidth = 46f;
        return value;
    }

    private void RefreshOptions(bool resetProcedureDependent)
    {
        procedureOptions = service.GetProcedures(patient);
        procedureIndex = Wrap(procedureIndex, procedureOptions.Count);
        nodeOptions = service.GetNodes(patient);
        nodeIndex = Wrap(nodeIndex, nodeOptions.Count);
        SurgicalProcedureSO procedure = Current(procedureOptions, procedureIndex);
        partOptions = service.GetParts(procedure);
        doctorOptions = service.GetDoctors(patient);
        facilityOptions = service.GetFacilities(procedure);
        if (resetProcedureDependent)
        {
            partIndex = 0;
            facilityIndex = 0;
        }

        partIndex = Wrap(partIndex, partOptions.Count);
        doctorIndex = Wrap(doctorIndex, doctorOptions.Count);
        facilityIndex = Wrap(facilityIndex, facilityOptions.Count);
        RefreshDetails();
    }

    private void RefreshDetails()
    {
        SurgicalProcedureSO procedure = Current(procedureOptions, procedureIndex);
        AnatomyNodeDefinition node = Current(nodeOptions, nodeIndex);
        SurgicalPartInstance part = Current(partOptions, partIndex);
        CharacterActor doctor = Current(doctorOptions, doctorIndex);
        SurgicalFacilitySnapshot facility = Current(facilityOptions, facilityIndex);

        procedureValue.text = procedure?.DisplayName ?? "사용 가능한 수술 없음";
        nodeValue.text = node?.DisplayName ?? "대상 부위 없음";
        partValue.text = part != null
            ? service.GetPartLabel(part)
            : procedure != null && RequiresPart(procedure.Kind)
                ? "사용 가능한 장기·보철 없음"
                : "필요 없음";
        doctorValue.text = doctor?.Identity?.DisplayName ?? "집도의 없음";
        facilityValue.text = facility.PrimaryFacility != null
            ? facility.PrimaryFacility.BuildingData?.objectName
                ?? facility.PrimaryFacility.name
            : "조건을 충족하는 시설 없음";

        if (procedure == null)
        {
            details.text = "현재 선택 가능한 수술 절차가 없습니다.";
            return;
        }

        SurgeryRiskBreakdown breakdown = service.EvaluateRisk(
            patient,
            procedure,
            part,
            doctor,
            facility);
        StringBuilder builder = new StringBuilder(640);
        builder.AppendLine(procedure.Description);
        if (patient.IsCorpse)
        {
            if (patient.CorpseFreshnessSeconds <= 0f)
            {
                builder.AppendLine("사체가 부패해 장기를 적출할 수 없습니다.");
            }
            else
            {
                builder.AppendLine(
                    $"사체 신선도 {patient.CorpseFreshnessSeconds / 180f:0.0}일 남음");
            }
        }

        builder.AppendLine();
        builder.AppendLine(
            $"작업량 {procedure.RequiredWork:0.#} · 필요 설비 "
            + $"{SurgicalFacilityQuery.FormatTags(procedure.RequiredFacilityTags)}");
        builder.AppendLine(
            $"성공 {breakdown.successChance * 100f:0.#}%"
            + $" · 감염 {breakdown.infectionChance * 100f:0.#}%"
            + $" · 출혈 {breakdown.bleedingChance * 100f:0.#}%");
        builder.AppendLine(
            $"장기 손상 {breakdown.organDamageChance * 100f:0.#}%"
            + $" · 사망 {breakdown.deathChance * 100f:0.#}%");
        builder.AppendLine();
        builder.AppendLine(breakdown.summary);
        if (facility.PrimaryFacility == null)
        {
            builder.AppendLine("차단: 요구 조건을 충족하는 수술 시설이 없습니다.");
        }
        else if (RequiresPart(procedure.Kind) && part == null)
        {
            builder.AppendLine("차단: 사용할 장기 또는 보철이 없습니다.");
        }

        SurgeryRiskBreakdown baseBreakdown = service.EvaluateBaseRisk(
            patient,
            procedure,
            part,
            doctor,
            facility);
        builder.AppendLine();
        builder.AppendLine("기본 위험");
        builder.AppendLine(
            $"성공 {baseBreakdown.successChance * 100f:0.#}%"
            + $" · 감염 {baseBreakdown.infectionChance * 100f:0.#}%"
            + $" · 출혈 {baseBreakdown.bleedingChance * 100f:0.#}%"
            + $" · 장기 손상 {baseBreakdown.organDamageChance * 100f:0.#}%"
            + $" · 사망 {baseBreakdown.deathChance * 100f:0.#}%");
        if (service.TryEvaluateEnvironmentRisk(
                patient,
                doctor,
                facility,
                out SurgeryEnvironmentRiskSnapshot environment))
        {
            builder.AppendLine("현재 환경·노출 보정");
            builder.AppendLine(
                $"온도 {environment.Environment.TemperatureC:0.#}°C"
                + $" · 공기 {environment.Environment.AirQuality:0}"
                + $" · 조명 {environment.Environment.LightLevel:0}");
            builder.AppendLine(environment.Summary);
            builder.AppendLine("위의 최종 확률은 환경이 유지될 경우의 값입니다.");
        }

        SurgeryOrder activeOrder = service.GetActiveOrder(patient);
        if (activeOrder?.state == SurgeryOrderState.EnvironmentWaiting)
        {
            builder.AppendLine();
            builder.AppendLine("환경 복구 대기");
            builder.AppendLine(activeOrder.environmentWaitReason);
            builder.AppendLine(
                $"정상 범위 유지 {activeOrder.environmentStableSeconds:0.0}/5.0초");
            if (!string.IsNullOrWhiteSpace(
                    activeOrder.environmentRecoveryWorkStatus))
            {
                builder.AppendLine(activeOrder.environmentRecoveryWorkStatus);
            }
        }

        details.text = builder.ToString().TrimEnd();
    }

    private void Schedule()
    {
        bool succeeded = service.TrySchedule(
            patient,
            Current(procedureOptions, procedureIndex),
            Current(nodeOptions, nodeIndex),
            Current(partOptions, partIndex),
            Current(doctorOptions, doctorIndex),
            Current(facilityOptions, facilityIndex),
            out string message);
        details.text = succeeded
            ? $"수술을 예약했습니다.\n{message}"
            : $"예약할 수 없습니다.\n{message}";
    }

    private void CancelOrder()
    {
        bool succeeded = service.TryCancel(patient, out string message);
        details.text = succeeded
            ? "수술 예약을 취소했습니다."
            : message;
    }

    private void Close()
    {
        onClosed?.Invoke();
        Destroy(gameObject);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private TMP_Text CreateText(
        string name,
        Transform parent,
        string value,
        float size,
        FontStyles style)
    {
        RectTransform rect = CreateRect(name, parent);
        TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.characterSpacing = 0f;
        fonts.Apply(text);
        return text;
    }

    private Button CreateButton(
        string name,
        Transform parent,
        string label,
        Action action,
        bool destructive = false)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(() => action?.Invoke());
        TMP_Text text = CreateText(
            "Label",
            rect,
            label,
            15f,
            FontStyles.Bold);
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(4f, 2f);
        textRect.offsetMax = new Vector2(-4f, -2f);
        text.alignment = TextAlignmentOptions.Center;
        DungeonUiTheme.StyleButton(button, destructive: destructive);
        return button;
    }

    private static T Current<T>(IReadOnlyList<T> options, int index)
    {
        return options != null && options.Count > 0
            ? options[Mathf.Clamp(index, 0, options.Count - 1)]
            : default;
    }

    private static int Wrap(int value, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        value %= count;
        return value < 0 ? value + count : value;
    }

    private static bool RequiresPart(SurgicalProcedureKind kind)
    {
        return kind is SurgicalProcedureKind.TransplantOrgan
            or SurgicalProcedureKind.InstallProsthetic
            or SurgicalProcedureKind.InstallImplant
            or SurgicalProcedureKind.ArcaneModification;
    }
}
