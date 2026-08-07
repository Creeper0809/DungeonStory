using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Projects the V19 life, family, health, safety, reproduction, and career
/// authorities into the character summary without owning mutable state.
/// </summary>
public sealed class CharacterSummaryPopulationPresenter
{
    private readonly ICharacterLifeQuery life;
    private readonly IKinshipQuery kinship;
    private readonly IPopulationHealthQuery health;
    private readonly IDiseaseDefinitionCatalog diseases;
    private readonly ICareerService careers;
    private readonly IReproductionService reproduction;
    private readonly IChildSafetyPolicy childSafety;
    private readonly IWorldHazardZoneQuery hazards;
    private TMP_Text summaryText;
    private Button globalPolicyButton;
    private Button characterPermissionButton;

    public CharacterSummaryPopulationPresenter(
        ICharacterLifeQuery life,
        IKinshipQuery kinship,
        IPopulationHealthQuery health,
        IDiseaseDefinitionCatalog diseases,
        ICareerService careers,
        IReproductionService reproduction,
        IChildSafetyPolicy childSafety,
        IWorldHazardZoneQuery hazards)
    {
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.kinship = kinship ?? throw new ArgumentNullException(nameof(kinship));
        this.health = health ?? throw new ArgumentNullException(nameof(health));
        this.diseases = diseases
            ?? throw new ArgumentNullException(nameof(diseases));
        this.careers = careers
            ?? throw new ArgumentNullException(nameof(careers));
        this.reproduction = reproduction
            ?? throw new ArgumentNullException(nameof(reproduction));
        this.childSafety = childSafety
            ?? throw new ArgumentNullException(nameof(childSafety));
        this.hazards = hazards ?? throw new ArgumentNullException(nameof(hazards));
    }

    public void Bind(
        TMP_Text summary,
        Button globalPolicy,
        Button characterPermission)
    {
        summaryText = summary;
        globalPolicyButton = globalPolicy;
        characterPermissionButton = characterPermission;
    }

    public void Refresh(CharacterActor actor)
    {
        if (summaryText == null)
            return;
        if (!TryGetId(actor, out CharacterId id)
            || !life.TryGet(id, out CharacterLifeRecord record))
        {
            summaryText.text = "생애 정보가 아직 등록되지 않았습니다.";
            SetButtonState(globalPolicyButton, false, "감독 도제: 확인 불가");
            SetButtonState(
                characterPermissionButton,
                false,
                "개별 허용: 확인 불가");
            return;
        }

        StringBuilder text = new();
        AppendLife(text, record);
        AppendFamily(text, id);
        AppendReproduction(text, id);
        AppendDisease(text, id);
        AppendCareer(text, id);
        AppendSafety(text, actor, id, record.LifeStage);
        summaryText.text = text.ToString().TrimEnd();
        RefreshButtons(id, record.LifeStage);
    }

    public void ToggleGlobalPolicy(CharacterActor actor)
    {
        childSafety.SetSupervisedApprenticeship(
            !childSafety.SupervisedApprenticeshipEnabled);
        Refresh(actor);
    }

    public void ToggleCharacterPermission(CharacterActor actor)
    {
        if (!TryGetId(actor, out CharacterId id)
            || !life.TryGet(id, out CharacterLifeRecord record)
            || record.LifeStage != CharacterLifeStage.Adolescent)
        {
            return;
        }

        childSafety.SetCharacterApprenticeshipPermission(
            id,
            !childSafety.IsCharacterApprenticeshipPermitted(id));
        Refresh(actor);
    }

    private static void AppendLife(
        StringBuilder text,
        CharacterLifeRecord record)
    {
        text.AppendLine("[생애]");
        text.Append("생물학적 나이 ")
            .Append((record.BiologicalAgeDayUnits
                / GameCalendarRules.DaysPerYear).ToString("0.0"))
            .Append("세 · 실제 경과 ")
            .Append(record.ChronologicalAgeDays)
            .Append("일 · ")
            .AppendLine(StageLabel(record.LifeStage));
        text.Append("생일 연중 ")
            .Append(record.BirthdayDayOfYear)
            .Append("일 · 노화 관리 ")
            .AppendLine(AgingCareLabel(record.EffectiveAgingCareMode));
        if (record.AgeConditions.Count == 0)
        {
            text.AppendLine("노화 질환 없음");
        }
        else
        {
            text.Append("노화 질환 ")
                .AppendLine(string.Join(
                    ", ",
                    record.AgeConditions
                        .OrderBy(value => value.ConditionId, StringComparer.Ordinal)
                        .Select(value =>
                            $"{AgeConditionLabel(value.ConditionId)}({SeverityLabel(value.Severity)})")));
        }
    }

    private void AppendFamily(StringBuilder text, CharacterId id)
    {
        text.AppendLine().AppendLine("[가족·계보]");
        CharacterId partner = kinship.GetPartner(id);
        CharacterId guardian = kinship.GetGuardian(id);
        text.Append("세대 ").Append(kinship.GetGeneration(id))
            .Append(" · 동반자 ")
            .AppendLine(partner.IsValid ? partner.Value : "없음");
        text.Append("부모 ")
            .AppendLine(FormatIds(kinship.GetParents(id, includeAdoptive: true)));
        text.Append("자녀 ")
            .AppendLine(FormatIds(kinship.GetChildren(id, includeAdoptive: true)));
        text.Append("보호자 ")
            .AppendLine(guardian.IsValid ? guardian.Value : "없음");
    }

    private void AppendReproduction(StringBuilder text, CharacterId id)
    {
        ReproductionProcess[] active = reproduction.Processes
            .Where(value => value != null
                && (value.FirstParentId.Equals(id)
                    || value.SecondParentId.Equals(id)
                    || value.CarrierId.Equals(id)))
            .OrderBy(value => value.ProcessId, StringComparer.Ordinal)
            .ToArray();
        text.Append("가족계획 ")
            .Append(FamilyPlanningLabel(reproduction.FamilyPlanningPolicy))
            .Append(" · 진행 ");
        if (active.Length == 0)
        {
            text.AppendLine("없음");
            return;
        }

        text.AppendLine(string.Join(
            ", ",
            active.Select(value =>
                $"{ReproductionModeLabel(value.Mode)} "
                + $"{ReproductionStatusLabel(value.Status)} "
                + $"{value.ProgressRatio:P0}")));
    }

    private void AppendDisease(StringBuilder text, CharacterId id)
    {
        text.AppendLine().AppendLine("[질병·면역]");
        if (!health.TryGetCharacterSnapshot(id, out PopulationCharacterHealthSnapshot state)
            || state.ActiveDiseases.Count == 0)
        {
            text.AppendLine("활성 질병 없음");
        }
        else
        {
            foreach (ActiveDiseaseSnapshot active in state.ActiveDiseases)
            {
                DiseaseDefinition definition = diseases.Require(active.DiseaseId);
                text.Append(definition.DisplayName)
                    .Append(" · 중증도 ")
                    .Append(active.Severity.ToString("0"))
                    .Append(" · ")
                    .AppendLine(active.Diagnosed ? "확진" : "미확진");
            }
        }

        string[] immunity = diseases.Definitions
            .Select(value => new
            {
                value.DisplayName,
                Value = health.GetImmunity(id, value.Id)
            })
            .Where(value => value.Value > 0.01f)
            .OrderByDescending(value => value.Value)
            .Select(value => $"{value.DisplayName} {value.Value:0}")
            .ToArray();
        text.Append("면역 ")
            .AppendLine(immunity.Length > 0
                ? string.Join(", ", immunity)
                : "없음");
    }

    private void AppendCareer(StringBuilder text, CharacterId id)
    {
        text.AppendLine().AppendLine("[경력]");
        if (!careers.TryGet(id, out CharacterCareerSnapshot career))
        {
            text.AppendLine("경력 기록 없음");
            return;
        }

        text.Append(career.Retired ? "은퇴" : "재직")
            .Append(" · 직위 ")
            .Append(career.Position == CareerPositionKind.None
                ? "없음"
                : CareerPositionLabel(career.Position));
        if (!string.IsNullOrWhiteSpace(career.PositionScopeId))
            text.Append(" (").Append(career.PositionScopeId).Append(')');
        CareerMentorshipSnapshot mentorship = careers.Mentorships
            .FirstOrDefault(value => value.StudentCharacterId.Equals(id));
        text.Append(" · 멘토 ")
            .AppendLine(mentorship.MentorCharacterId.IsValid
                ? mentorship.MentorCharacterId.Value
                : "없음");
    }

    private void AppendSafety(
        StringBuilder text,
        CharacterActor actor,
        CharacterId id,
        CharacterLifeStage stage)
    {
        WorldHazardSnapshot hazard = hazards.GetHazard(id, actor.GetNowXY());
        text.AppendLine().AppendLine("[아동 안전]");
        text.Append("현재 구역 ")
            .Append(HazardLevelLabel(hazard.Level))
            .Append(" · 위험 ")
            .AppendLine(hazard.Flags == WorldHazardFlags.None
                ? "없음"
                : HazardFlagsLabel(hazard.Flags));
        text.Append("감독 도제 정책 ")
            .Append(childSafety.SupervisedApprenticeshipEnabled ? "활성" : "비활성")
            .Append(" · 개별 허용 ")
            .AppendLine(stage == CharacterLifeStage.Adolescent
                ? childSafety.IsCharacterApprenticeshipPermitted(id)
                    ? "허용"
                    : "금지"
                : "청소년만 설정 가능");
    }

    private void RefreshButtons(CharacterId id, CharacterLifeStage stage)
    {
        SetButtonState(
            globalPolicyButton,
            true,
            childSafety.SupervisedApprenticeshipEnabled
                ? "감독 도제: 켬"
                : "감독 도제: 끔");
        bool adolescent = stage == CharacterLifeStage.Adolescent;
        SetButtonState(
            characterPermissionButton,
            adolescent,
            adolescent
                ? childSafety.IsCharacterApprenticeshipPermitted(id)
                    ? "이 캐릭터: 허용"
                    : "이 캐릭터: 금지"
                : "청소년만 개별 허용");
    }

    private static bool TryGetId(CharacterActor actor, out CharacterId id) =>
        actor != null && CharacterPersistentIdentity.TryGet(actor, out id);

    private static string FormatIds(
        System.Collections.Generic.IReadOnlyList<CharacterId> ids) =>
        ids != null && ids.Count > 0
            ? string.Join(", ", ids.Select(value => value.Value))
            : "없음";

    private static string StageLabel(CharacterLifeStage stage) => stage switch
    {
        CharacterLifeStage.Infant => "영아",
        CharacterLifeStage.Child => "아동",
        CharacterLifeStage.Adolescent => "청소년",
        CharacterLifeStage.Adult => "성년",
        CharacterLifeStage.Elder => "노년",
        _ => stage.ToString()
    };

    private static string SeverityLabel(AgeConditionSeverity severity) =>
        severity switch
        {
            AgeConditionSeverity.Mild => "경증",
            AgeConditionSeverity.Moderate => "중등도",
            AgeConditionSeverity.Severe => "중증",
            AgeConditionSeverity.Critical => "위중",
            AgeConditionSeverity.OrganFunctionLoss => "장기 기능 상실",
            _ => severity.ToString()
        };

    private static string AgingCareLabel(AgingCareMode mode) => mode switch
    {
        AgingCareMode.Normal => "일반",
        AgingCareMode.RuneHibernation => "룬 동면",
        AgingCareMode.TemporalStasis => "시간 고정",
        _ => mode.ToString()
    };

    private static string AgeConditionLabel(string conditionId) => conditionId switch
    {
        "condition:age-cardiac-degeneration" => "심장 기능 퇴행",
        "condition:age-neural-degeneration" => "신경 기능 퇴행",
        "condition:age-organ-fibrosis" => "장기 섬유화",
        "condition:core-corrosion" => "핵 부식",
        "condition:rune-circuit-wear" => "룬 회로 마모",
        "condition:frame-fatigue" => "골격 피로",
        _ => conditionId ?? string.Empty
    };

    private static string FamilyPlanningLabel(FamilyPlanningPolicy policy) =>
        policy switch
        {
            FamilyPlanningPolicy.Off => "중지",
            FamilyPlanningPolicy.Planned => "계획",
            FamilyPlanningPolicy.Allowed => "허용",
            _ => policy.ToString()
        };

    private static string ReproductionModeLabel(ReproductionMode mode) => mode switch
    {
        ReproductionMode.Pregnancy => "임신·출산",
        ReproductionMode.Egg => "산란·부화",
        ReproductionMode.Spore => "포자 배양",
        ReproductionMode.CoreDivision => "핵 분열",
        ReproductionMode.GolemAssembly => "골렘 조립",
        _ => mode.ToString()
    };

    private static string ReproductionStatusLabel(
        ReproductionProcessStatus status) => status switch
    {
        ReproductionProcessStatus.Planned => "계획됨",
        ReproductionProcessStatus.Active => "진행 중",
        ReproductionProcessStatus.WaitingForEnvironment => "환경 대기",
        ReproductionProcessStatus.WaitingForEmergencyExtraction => "응급 적출 대기",
        ReproductionProcessStatus.Completed => "완료",
        ReproductionProcessStatus.Failed => "실패",
        _ => status.ToString()
    };

    private static string CareerPositionLabel(CareerPositionKind position) =>
        position switch
        {
            CareerPositionKind.Steward => "관리인",
            CareerPositionKind.ChiefResearcher => "수석 연구원",
            CareerPositionKind.ChiefPhysician => "수석 의사",
            CareerPositionKind.GuardCaptain => "경비대장",
            CareerPositionKind.Foreman => "작업반장",
            CareerPositionKind.Mentor => "멘토",
            _ => position.ToString()
        };

    private static string HazardLevelLabel(WorldHazardLevel level) => level switch
    {
        WorldHazardLevel.Safe => "안전",
        WorldHazardLevel.Restricted => "제한",
        WorldHazardLevel.Forbidden => "금지",
        _ => level.ToString()
    };

    private static string HazardFlagsLabel(WorldHazardFlags flags)
    {
        (WorldHazardFlags Flag, string Label)[] labels =
        {
            (WorldHazardFlags.Combat, "전투"),
            (WorldHazardFlags.Fire, "화재"),
            (WorldHazardFlags.ToxicAir, "유독 공기"),
            (WorldHazardFlags.LethalTemperature, "치명 온도"),
            (WorldHazardFlags.SevereContamination, "중증 오염"),
            (WorldHazardFlags.Industrial, "산업 위험"),
            (WorldHazardFlags.UncomfortableTemperature, "불편 온도")
        };
        return string.Join(
            ", ",
            labels.Where(value => (flags & value.Flag) != 0)
                .Select(value => value.Label));
    }

    private static void SetButtonState(
        Button button,
        bool interactable,
        string label)
    {
        if (button == null)
            return;
        button.interactable = interactable;
        TMP_Text text = button.transform.Find("Label")?.GetComponent<TMP_Text>();
        if (text != null)
            text.text = label;
        DungeonUiTheme.StyleButton(button, selected: false);
    }
}
