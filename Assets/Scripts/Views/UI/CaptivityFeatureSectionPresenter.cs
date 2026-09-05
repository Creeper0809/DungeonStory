using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class CaptivityFeatureSectionPresenter :
    ICaptivityFeatureSectionPresenter
{
    private const float CardHeight = 96f;

    private readonly ICaptivityRuntime captivity;
    private readonly ICaptivityCommandService commands;
    private readonly CaptivityInteractionRegistry interactions;
    private readonly ICharacterAiWorldRegistry world;
    private readonly ICharacterSettlementStandingQuery standings;
    private string selectedCaptiveId = string.Empty;
    private string selectedMinionId = string.Empty;
    private string selectedPolicyId = string.Empty;

    public CaptivityFeatureSectionPresenter(
        ICaptivityRuntime captivity,
        ICaptivityCommandService commands,
        CaptivityInteractionRegistry interactions,
        ICharacterAiWorldRegistry world,
        ICharacterSettlementStandingQuery standings)
    {
        this.captivity = captivity
            ?? throw new ArgumentNullException(nameof(captivity));
        this.commands = commands
            ?? throw new ArgumentNullException(nameof(commands));
        this.interactions = interactions
            ?? throw new ArgumentNullException(nameof(interactions));
        this.world = world
            ?? throw new ArgumentNullException(nameof(world));
        this.standings = standings
            ?? throw new ArgumentNullException(nameof(standings));
    }

    public void Present(IFeatureSurfaceView view)
    {
        CaptiveState[] active = captivity.Captives
            .Where(state => state.IsInCustody)
            .OrderByDescending(state => state.escapeRisk)
            .ThenBy(state => state.displayName, StringComparer.Ordinal)
            .ToArray();
        CaptiveState[] minions = captivity.Captives
            .Where(state => state.IsMinion)
            .OrderBy(state => state.displayName, StringComparer.Ordinal)
            .ToArray();
        if (active.Length == 0 && minions.Length == 0)
        {
            selectedCaptiveId = string.Empty;
            view.AddSection(
                "포로·노역",
                "수용 중인 포로가 없습니다. 쓰러진 침입자의 건강 탭에서 포획을 명령할 수 있습니다.");
            return;
        }

        AddMinionManagement(view, minions);
        if (active.Length == 0)
        {
            selectedCaptiveId = string.Empty;
            view.AddSection(
                "포로·노역",
                "수용 중인 포로가 없습니다. 하수인은 위 관리 구역에서 확인합니다.");
            return;
        }

        CaptiveState selected = active.FirstOrDefault(state =>
            string.Equals(
                state.captiveId,
                selectedCaptiveId,
                StringComparison.Ordinal));
        if (selected == null)
        {
            selected = active[0];
            selectedCaptiveId = selected.captiveId;
        }

        int falseComplianceCount = active.Count(state => state.falseCompliance);
        view.AddSection(
            "포로·노역",
            $"수용 {active.Length}명 · 노역 {active.Count(state => state.status == CaptivityStatus.Labor)}명"
            + $" · 탈출 고위험 {active.Count(state => state.escapeRisk >= 70f)}명"
            + (falseComplianceCount > 0
                ? $" · 복종 진위 불명 {falseComplianceCount}명"
                : string.Empty));

        for (int index = 0; index < active.Length; index++)
        {
            CaptiveState row = active[index];
            int capturedIndex = index;
            view.AddDataCard(
                $"Captivity_Select_{capturedIndex}",
                row.displayName,
                CreateSummary(row),
                string.Equals(row.captiveId, selectedCaptiveId, StringComparison.Ordinal)
                    ? "선택됨"
                    : "관리",
                () =>
                {
                    selectedCaptiveId = row.captiveId;
                    view.ShowFeedback($"{row.displayName} 관리 항목을 열었습니다.");
                    view.RequestRefresh();
                },
                CardHeight);
        }

        AddSelectedCaptiveControls(view, selected);
    }

    private void AddSelectedCaptiveControls(
        IFeatureSurfaceView view,
        CaptiveState selected)
    {
        view.AddSection(
            $"{selected.displayName} 처우",
            CreateDetailedSummary(selected));
        AddPolicyControls(view, selected);
        AddPerformerMilestoneControls(view, selected);

        if (selected.status == CaptivityStatus.Labor)
        {
            AddCommand(
                view,
                "Captivity_StopLabor",
                "노역 중지",
                "포로를 감방으로 돌려보내고 일반 AI를 다시 정지합니다.",
                () => commands.TrySetLaborPermissions(
                    selected.captiveId,
                    CaptiveLaborPermission.None,
                    out string reason)
                        ? Success("노역을 중지했습니다.")
                        : Failure(reason));
        }
        else
        {
            AddCommand(
                view,
                "Captivity_BasicLabor",
                "기본 노역",
                "청소와 운반만 허용합니다. 순응도 50, 건강 40 이상이 필요합니다.",
                () => commands.TrySetLaborPermissions(
                    selected.captiveId,
                    CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul,
                    out string reason)
                        ? Success("청소·운반 노역을 허용했습니다.")
                        : Failure(reason));
            AddCommand(
                view,
                "Captivity_AllLabor",
                "모든 노역 허용",
                "급수·연료·건설·수리·도축·제작 보조까지 허용합니다.",
                () => commands.TrySetLaborPermissions(
                    selected.captiveId,
                    CaptiveLaborPermission.All,
                    out string reason)
                        ? Success("모든 노역을 허용했습니다.")
                        : Failure(reason));
        }

        if (selected.status != CaptivityStatus.Interaction)
        {
            foreach (ICaptivityInteractionHandler handler in interactions.All
                         .OrderBy(item => item.Kind))
            {
                ICaptivityInteractionHandler captured = handler;
                AddCommand(
                    view,
                    $"Captivity_Interaction_{captured.InteractionId}",
                    captured.DisplayName,
                    $"필요 작업량 {captured.RequiredWork:0.#}"
                    + $" · 재료 {FormatMaterials(captured.MaterialRequirements)}"
                    + " · 담당자와 감방 시설이 필요합니다.",
                    () => TryStartInteraction(selected, captured));
            }
        }
        else
        {
            view.AddDataCard(
                "Captivity_InteractionProgress",
                "관리 작업 진행 중",
                $"{selected.currentInteractionId} · "
                + $"{selected.completedInteractionWork:0.#}/{selected.requiredInteractionWork:0.#}",
                "상태",
                () => view.ShowFeedback(selected.lastResult),
                CardHeight);
        }

        AddCommand(
            view,
            "Captivity_Recruit",
            "정식 영입",
            "포획 10일, 신뢰 70 이상, 원한 30 이하, 타락 60 미만일 때 정식 주민으로 영입합니다.",
            () => commands.TryRecruit(selected.captiveId, out string reason)
                ? Success("정식 주민으로 영입했습니다.")
                : Failure(reason));
        AddCommand(
            view,
            "Captivity_Minion",
            "하수인 전환",
            "포획 3일과 타락 80 이상을 충족한 포로를 하수인으로 전환합니다.",
            () => commands.TryConvertToMinion(selected.captiveId, out string reason)
                ? Success("하수인으로 전환했습니다.")
                : Failure(reason));
        AddCommand(
            view,
            "Captivity_Ransom",
            $"몸값 협상 · {selected.RansomValue:N0}",
            "몸값을 받고 석방합니다. 원한이 높으면 이후 보복 압력이 남습니다.",
            () => commands.TryRansom(
                    selected.captiveId,
                    out int amount,
                    out string reason)
                ? Success($"몸값 {amount:N0}을 받고 석방했습니다.")
                : Failure(reason));
        AddCommand(
            view,
            "Captivity_Release",
            "석방",
            "구속을 풀고 던전 밖으로 내보냅니다. 원한과 기억은 남습니다.",
            () => commands.TryRelease(selected.captiveId, out string reason)
                ? Success("포로를 석방했습니다.")
                : Failure(reason));
    }

    private void AddMinionManagement(
        IFeatureSurfaceView view,
        IReadOnlyList<CaptiveState> minions)
    {
        if (minions == null || minions.Count == 0)
        {
            selectedMinionId = string.Empty;
            return;
        }

        CaptiveState selected = minions.FirstOrDefault(state =>
            string.Equals(
                state.captiveId,
                selectedMinionId,
                StringComparison.Ordinal)) ?? minions[0];
        selectedMinionId = selected.captiveId;
        view.AddSection(
            "하수인 관리",
            $"하수인 {minions.Count}명 · 허용 업무 23/31 · 임금 0 · 생활 자원 100% · 업무 XP 50% · 경비 가능 · 원정 불가");
        for (int index = 0; index < minions.Count; index++)
        {
            CaptiveState row = minions[index];
            int capturedIndex = index;
            view.AddDataCard(
                $"Minion_Select_{capturedIndex}",
                row.displayName,
                $"통제 안정도 {MinionIntegrationRules.ResolveControlStability(row.corruption, row.trust, row.grudge):0.#}"
                + $" · 재사회화 {row.rehabilitationDays}/{MinionIntegrationRules.RequiredRehabilitationDays}일"
                + $" · {row.completedRehabilitationWork:0.#}/{MinionIntegrationRules.RehabilitationRequiredWork:0.#} WU",
                string.Equals(
                    row.captiveId,
                    selectedMinionId,
                    StringComparison.Ordinal)
                        ? "선택됨"
                        : "관리",
                () =>
                {
                    selectedMinionId = row.captiveId;
                    view.ShowFeedback($"{row.displayName} 하수인 관리 항목을 열었습니다.");
                    view.RequestRefresh();
                },
                CardHeight);
        }

        float breakChance = MinionIntegrationRules
            .ResolveControlBreakChancePercent(
                selected.corruption,
                selected.trust,
                selected.grudge,
                captivity.TryGetActor(selected.captiveId, out CharacterActor actor)
                    ? actor.Mood.Value
                    : 50f);
        view.AddSection(
            $"{selected.displayName} 하수인 상태",
            $"신분 하수인 · 허용 업무 23/31 · 숙련 성장률 50% · 임금 없음\n"
            + $"신뢰 {selected.trust:0} · 원한 {selected.grudge:0} · 타락 {selected.corruption:0}"
            + $" · 통제 이탈 {breakChance:0.#}%/일\n"
            + $"재사회화 {selected.rehabilitationDays}/{MinionIntegrationRules.RequiredRehabilitationDays}일"
            + $" · 오늘 작업 {selected.completedRehabilitationWork:0.#}/{MinionIntegrationRules.RehabilitationRequiredWork:0.#} WU"
            + " · 완료할 때 음식 1개"
            + (selected.rehabilitationInProgress
                ? " · 담당 주민이 작업 중"
                : string.Empty));
        if (!selected.rehabilitationInProgress)
        {
            AddCommand(
                view,
                "Minion_StartRehabilitation",
                "재사회화 시작",
                "정식 주민 한 명이 수용 시설에서 18 WU를 수행합니다. 완료할 때 음식 1개를 사용하며 하루 한 번만 진행합니다.",
                () => TryStartRehabilitation(selected));
        }
        AddCommand(
            view,
            "Minion_Recruit",
            "정식 영입",
            "재사회화 15일, 신뢰 70 이상, 원한 30 이하, 타락 30 이하를 모두 충족하면 정식 주민으로 전환합니다.",
            () => commands.TryRecruit(selected.captiveId, out string reason)
                ? Success("정식 주민으로 영입했습니다.")
                : Failure(reason));
    }

    private void AddPerformerMilestoneControls(
        IFeatureSurfaceView view,
        CaptiveState selected)
    {
        if (selected == null || selected.performerFame < 50f)
        {
            return;
        }

        string benefits = selected.performerFame >= 100f
            ? "우선 식량·치료 · 직원 계약 · 최종 계약 선택"
            : selected.performerFame >= 75f
                ? "우선 식량·치료 · 직원 계약"
                : "우선 식량·치료";
        view.AddSection(
            "공연자 이정표",
            $"명성 {selected.performerFame:0} · {benefits}"
            + (selected.exclusiveFighter ? " · 전속 투사" : string.Empty));

        if (selected.staffContractUnlocked && selected.CanRecruit)
        {
            AddCommand(
                view,
                "Captivity_PerformerStaffContract",
                "직원 계약 제안",
                "공연 명성으로 열린 계약입니다. 신뢰·원한·타락 조건을 확인합니다.",
                () => commands.TryResolvePerformerMilestone(
                        selected.captiveId,
                        CaptivePerformerMilestoneChoice.StaffContract,
                        out string reason)
                    ? Success("공연자와 직원 계약을 맺었습니다.")
                    : Failure(reason));
        }

        if (!selected.finalContractPending)
        {
            return;
        }

        AddCommand(
            view,
            "Captivity_PerformerReleaseNegotiation",
            "석방 협상",
            "명성을 대가로 자유를 보장하고 공연자를 석방합니다.",
            () => commands.TryResolvePerformerMilestone(
                    selected.captiveId,
                    CaptivePerformerMilestoneChoice.ReleaseNegotiation,
                    out string reason)
                ? Success("석방 협상을 마쳤습니다.")
                : Failure(reason));
        AddCommand(
            view,
            "Captivity_PerformerExclusiveContract",
            "전속 투사 계약",
            "포로 신분은 유지하되 전속 투사로 공연과 전투에 우선 배치합니다.",
            () => commands.TryResolvePerformerMilestone(
                    selected.captiveId,
                    CaptivePerformerMilestoneChoice.ExclusiveFighterContract,
                    out string reason)
                ? Success("전속 투사 계약을 맺었습니다.")
                : Failure(reason));
    }

    private void AddPolicyControls(
        IFeatureSurfaceView view,
        CaptiveState selected)
    {
        CaptivePolicyData[] policies = captivity.Policies
            .OrderBy(policy => policy.displayName, StringComparer.Ordinal)
            .ToArray();
        CaptivePolicyData selectedPolicy = policies.FirstOrDefault(policy =>
            string.Equals(
                policy.policyId,
                selectedPolicyId,
                StringComparison.Ordinal))
            ?? policies.FirstOrDefault(policy => string.Equals(
                policy.policyId,
                selected.policyId,
                StringComparison.Ordinal))
            ?? policies.FirstOrDefault();
        if (selectedPolicy == null)
        {
            return;
        }

        selectedPolicyId = selectedPolicy.policyId;
        view.AddSection(
            "수용 정책",
            $"{selectedPolicy.displayName} · 노동 {FormatLabor(selectedPolicy.allowedLabor)}"
            + $" · 몸값 {FormatAllowed(selectedPolicy.allowRansom)}"
            + $" · 영입 {FormatAllowed(selectedPolicy.allowRecruitment)}"
            + $" · 타락 {FormatAllowed(selectedPolicy.allowCorruption)}"
            + $" · 공연 {FormatAllowed(selectedPolicy.allowPerformance)}");

        foreach (CaptivePolicyData policy in policies)
        {
            CaptivePolicyData capturedPolicy = policy;
            AddCommand(
                view,
                $"Captivity_Policy_{policy.policyId}",
                policy.displayName,
                FormatPolicy(policy),
                () =>
                {
                    selectedPolicyId = capturedPolicy.policyId;
                    return commands.TrySetPolicy(
                            selected.captiveId,
                            capturedPolicy.policyId,
                            out string reason)
                        ? Success($"{selected.displayName}에게 {capturedPolicy.displayName} 정책을 배정했습니다.")
                        : Failure(reason);
                });
        }

        AddCommand(
            view,
            "Captivity_PolicyCreate",
            "새 정책",
            "표준 노동 범위로 새 수용 정책을 만듭니다.",
            () => commands.TryCreatePolicy(
                    string.Empty,
                    out string createdId,
                    out string reason)
                ? SelectPolicy(createdId, "새 수용 정책을 만들었습니다.")
                : Failure(reason));
        AddCommand(
            view,
            "Captivity_PolicyDuplicate",
            "정책 복제",
            $"{selectedPolicy.displayName}의 설정을 복제합니다.",
            () => commands.TryDuplicatePolicy(
                    selectedPolicy.policyId,
                    out string duplicateId,
                    out string reason)
                ? SelectPolicy(duplicateId, "수용 정책을 복제했습니다.")
                : Failure(reason));
        AddCommand(
            view,
            "Captivity_PolicyLabor",
            "정책 노동 범위 변경",
            $"현재 {FormatLabor(selectedPolicy.allowedLabor)} · 누르면 없음 → 기본 → 전체 순서로 변경합니다.",
            () => UpdatePolicy(
                selectedPolicy,
                policy => policy.allowedLabor = NextLabor(policy.allowedLabor),
                "정책 노동 범위를 변경했습니다."));
        AddCommand(
            view,
            "Captivity_PolicyRansom",
            "몸값 허용 전환",
            $"현재 {FormatAllowed(selectedPolicy.allowRansom)}",
            () => UpdatePolicy(
                selectedPolicy,
                policy => policy.allowRansom = !policy.allowRansom,
                "몸값 허용을 변경했습니다."));
        AddCommand(
            view,
            "Captivity_PolicyRecruitment",
            "영입 허용 전환",
            $"현재 {FormatAllowed(selectedPolicy.allowRecruitment)}",
            () => UpdatePolicy(
                selectedPolicy,
                policy => policy.allowRecruitment = !policy.allowRecruitment,
                "영입 허용을 변경했습니다."));
        AddCommand(
            view,
            "Captivity_PolicyCorruption",
            "타락 전환 허용",
            $"현재 {FormatAllowed(selectedPolicy.allowCorruption)}",
            () => UpdatePolicy(
                selectedPolicy,
                policy => policy.allowCorruption = !policy.allowCorruption,
                "타락 전환 허용을 변경했습니다."));
        AddCommand(
            view,
            "Captivity_PolicyPerformance",
            "공연 허용 전환",
            $"현재 {FormatAllowed(selectedPolicy.allowPerformance)}",
            () => UpdatePolicy(
                selectedPolicy,
                policy => policy.allowPerformance = !policy.allowPerformance,
                "공연 허용을 변경했습니다."));
        if (!string.Equals(
                selectedPolicy.policyId,
                "captivity:standard",
                StringComparison.Ordinal))
        {
            AddCommand(
                view,
                "Captivity_PolicyDelete",
                "정책 삭제",
                "이 정책의 포로는 표준 수용으로 재배정됩니다.",
                () => commands.TryDeletePolicy(
                        selectedPolicy.policyId,
                        out string reason)
                    ? SelectPolicy("captivity:standard", "수용 정책을 삭제했습니다.")
                    : Failure(reason));
        }

        OperationsFeatureCommandResult SelectPolicy(
            string policyId,
            string message)
        {
            selectedPolicyId = policyId;
            return Success(message);
        }
    }

    private OperationsFeatureCommandResult UpdatePolicy(
        CaptivePolicyData source,
        Action<CaptivePolicyData> mutate,
        string successMessage)
    {
        CaptivePolicyData updated = source.Clone();
        mutate(updated);
        return commands.TryUpdatePolicy(updated, out string reason)
            ? Success(successMessage)
            : Failure(reason);
    }

    private OperationsFeatureCommandResult TryStartInteraction(
        CaptiveState selected,
        ICaptivityInteractionHandler handler)
    {
        if (!captivity.TryGetActor(selected.captiveId, out CharacterActor subject))
        {
            return Failure("포로의 월드 개체를 찾지 못했습니다.");
        }

        CharacterActor warden = world.AllCharacters
            .Where(actor => IsAvailableWarden(actor, selected.captiveId))
            .OrderBy(actor => Manhattan(actor.GetNowXY(), subject.GetNowXY()))
            .FirstOrDefault();
        if (warden == null)
        {
            return Failure("관리 작업을 맡을 수 있는 직원이 없습니다.");
        }

        captivity.TryGetHousing(selected.captiveId, out BuildableObject facility);
        return commands.TryStartInteraction(
            selected.captiveId,
            handler.InteractionId,
            warden,
            facility,
            out string failureReason)
                ? Success($"{warden.Identity?.DisplayName ?? warden.name}에게 {handler.DisplayName} 작업을 배정했습니다.")
                : Failure(failureReason);
    }

    private OperationsFeatureCommandResult TryStartRehabilitation(
        CaptiveState selected)
    {
        if (!captivity.TryGetActor(selected.captiveId, out CharacterActor subject))
        {
            return Failure("하수인의 월드 개체를 찾지 못했습니다.");
        }

        CharacterActor warden = world.AllCharacters
            .Where(actor => IsAvailableWarden(actor, selected.captiveId))
            .OrderBy(actor => Manhattan(actor.GetNowXY(), subject.GetNowXY()))
            .FirstOrDefault();
        if (warden == null)
        {
            return Failure("재사회화를 맡을 수 있는 정식 주민이 없습니다.");
        }

        BuildableObject facility = world.Buildings
            .Where(building => building != null
                && !building.isDestroy
                && building.BuildingData.GetCaptiveHousingAbility()?.IsValid == true)
            .OrderBy(building => Manhattan(subject.GetNowXY(), building.centerPos))
            .ThenBy(
                building => building.RequirePersistentInstanceId().Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        if (facility == null)
        {
            return Failure("재사회화를 진행할 수용 시설이 없습니다.");
        }

        return commands.TryStartRehabilitation(
            selected.captiveId,
            warden,
            facility,
            out string failureReason)
                ? Success($"{warden.Identity?.DisplayName ?? warden.name}에게 재사회화 작업을 배정했습니다.")
                : Failure(failureReason);
    }

    private bool IsAvailableWarden(
        CharacterActor actor,
        string captiveId)
    {
        if (actor == null
            || actor.IsDead
            || actor.CurrentLifecycleState != CharacterLifecycleState.Active
            || actor.characterType != CharacterType.NPC)
        {
            return false;
        }

        string actorId = actor.Identity?.PersistentId ?? string.Empty;
        return !string.Equals(actorId, captiveId, StringComparison.Ordinal)
            && standings.IsFormalResident(actor)
            && actor.TryGetAbility(out AbilityWork _);
    }

    private static string CreateSummary(CaptiveState state)
    {
        return $"{FormatStatus(state.status)} · 건강 {state.health:0}"
            + $" · 순응 {state.compliance:0} · 탈출 {state.escapeRisk:0}"
            + (state.falseCompliance ? " · 복종 진위 불명" : string.Empty);
    }

    private static string CreateDetailedSummary(CaptiveState state)
    {
        return $"의지 {state.will:0} · 공포 {state.fear:0} · 신뢰 {state.trust:0}"
            + $" · 원한 {state.grudge:0} · 타락 {state.corruption:0}\n"
            + $"순응 {state.compliance:0} · 탈출 위험 {state.escapeRisk:0}"
            + $" · 보복 압력 {state.retaliationPressure:0}"
            + $" · 공연 명성 {state.performerFame:0} · {state.lastResult}";
    }

    private static CaptiveLaborPermission NextLabor(
        CaptiveLaborPermission current)
    {
        CaptiveLaborPermission normalized =
            current & CaptiveLaborPermission.All;
        CaptiveLaborPermission basic =
            CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul;
        return normalized == CaptiveLaborPermission.None
            ? basic
            : normalized == basic
                ? CaptiveLaborPermission.All
                : CaptiveLaborPermission.None;
    }

    private static string FormatPolicy(CaptivePolicyData policy)
    {
        return $"노동 {FormatLabor(policy.allowedLabor)}"
            + $" · 몸값 {FormatAllowed(policy.allowRansom)}"
            + $" · 영입 {FormatAllowed(policy.allowRecruitment)}"
            + $" · 타락 {FormatAllowed(policy.allowCorruption)}"
            + $" · 공연 {FormatAllowed(policy.allowPerformance)}";
    }

    private static string FormatLabor(CaptiveLaborPermission permissions)
    {
        CaptiveLaborPermission normalized =
            permissions & CaptiveLaborPermission.All;
        if (normalized == CaptiveLaborPermission.None)
        {
            return "없음";
        }

        if (normalized == CaptiveLaborPermission.All)
        {
            return "전체";
        }

        CaptiveLaborPermission basic =
            CaptiveLaborPermission.Clean | CaptiveLaborPermission.Haul;
        return normalized == basic ? "청소·운반" : normalized.ToString();
    }

    private static string FormatAllowed(bool allowed)
    {
        return allowed ? "허용" : "차단";
    }

    private static string FormatMaterials(
        IReadOnlyDictionary<StockCategory, int> materials)
    {
        return materials == null || materials.Count == 0
            ? "없음"
            : string.Join(
                ", ",
                materials.Select(item => $"{item.Key} {item.Value}"));
    }

    private static string FormatStatus(CaptivityStatus status)
    {
        return status switch
        {
            CaptivityStatus.AwaitingCapture => "포획 대기",
            CaptivityStatus.Stabilizing => "현장 안정화",
            CaptivityStatus.AwaitingEscort => "호송 대기",
            CaptivityStatus.Escorting => "호송 중",
            CaptivityStatus.Confined => "수용 중",
            CaptivityStatus.Labor => "노역 중",
            CaptivityStatus.Interaction => "관리 작업 중",
            CaptivityStatus.Performer => "공연 준비",
            CaptivityStatus.EscapeAttempt => "탈출 시도",
            CaptivityStatus.Ransom => "몸값 협상",
            _ => status.ToString()
        };
    }

    private static void AddCommand(
        IFeatureSurfaceView view,
        string actionName,
        string title,
        string detail,
        Func<OperationsFeatureCommandResult> execute)
    {
        view.AddDataCard(
            actionName,
            title,
            detail,
            "실행",
            () =>
            {
                OperationsFeatureCommandResult result = execute();
                view.ShowFeedback(result.Message);
                view.RequestRefresh();
            },
            CardHeight);
    }

    private static OperationsFeatureCommandResult Success(string message)
    {
        return new OperationsFeatureCommandResult(true, message);
    }

    private static OperationsFeatureCommandResult Failure(string message)
    {
        return new OperationsFeatureCommandResult(false, message);
    }

    private static int Manhattan(Vector2Int left, Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x) + Mathf.Abs(left.y - right.y);
    }
}
