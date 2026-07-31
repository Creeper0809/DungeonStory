using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DefenseFeatureSurfaceModel
{
    public string ThreatSummary { get; set; } = string.Empty;
    public string ThreatFactors { get; set; } = string.Empty;
    public string CampaignSummary { get; set; } = string.Empty;
    public string ReinforcementSummary { get; set; } = string.Empty;
    public string OwnerEvacuationSummary { get; set; } = string.Empty;
    public IReadOnlyList<DefenseFeatureIntruderRow> Intruders { get; set; }
        = Array.Empty<DefenseFeatureIntruderRow>();
    public IReadOnlyList<DefenseFeaturePolicyRow> Policies { get; set; }
        = Array.Empty<DefenseFeaturePolicyRow>();
    public DefenseFeaturePolicyRow SelectedPolicy { get; set; }
    public IReadOnlyList<DefenseFeatureGuardRow> Guards { get; set; }
        = Array.Empty<DefenseFeatureGuardRow>();
    public IReadOnlyList<DefenseFeatureFacilityRow> Facilities { get; set; }
        = Array.Empty<DefenseFeatureFacilityRow>();
    public IReadOnlyList<DefenseFeatureReportRow> Reports { get; set; }
        = Array.Empty<DefenseFeatureReportRow>();
}

public sealed class DefenseFeatureIntruderRow
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public sealed class DefenseFeaturePolicyRow
{
    public int Index { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
    public bool IsCustom { get; set; }
    public bool AutoRespond { get; set; }
    public float MinimumDispatchHealthRatio { get; set; }
    public float RetreatHealthRatio { get; set; }
    public bool HoldWithoutReplacement { get; set; }
    public float RejoinHealthRatio { get; set; }
}

public sealed class DefenseFeatureGuardRow
{
    public int Index { get; set; }
    public int ActorRuntimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool UsesSelectedPolicy { get; set; }
}

public sealed class DefenseFeatureFacilityRow
{
    public int Index { get; set; }
    public int RuntimeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DefenseArmingPolicy ArmingPolicy { get; set; }
    public DefenseFacilityOperationalState OperationalState { get; set; }
}

public sealed class DefenseFeatureReportRow
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public readonly struct DefenseFeatureCommandResult
{
    public DefenseFeatureCommandResult(bool succeeded, string message, string entityId = "")
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
        EntityId = entityId ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string Message { get; }
    public string EntityId { get; }
}

public interface IDefenseFeatureQueryService
{
    DefenseFeatureSurfaceModel Capture(string selectedPolicyId);
}

public interface IDefenseFeatureCommandService
{
    DefenseFeatureCommandResult ToggleAutoResponse(string policyId);
    DefenseFeatureCommandResult StepMinimumDispatchHealth(string policyId);
    DefenseFeatureCommandResult StepRetreatHealth(string policyId);
    DefenseFeatureCommandResult ToggleHoldWithoutReplacement(string policyId);
    DefenseFeatureCommandResult StepRejoinHealth(string policyId);
    DefenseFeatureCommandResult CreatePolicy();
    DefenseFeatureCommandResult DuplicatePolicy(string policyId);
    DefenseFeatureCommandResult DeletePolicy(string policyId);
    DefenseFeatureCommandResult AssignPolicy(int actorRuntimeId, string policyId);
    DefenseFeatureCommandResult CycleFacilityArmingPolicy(int facilityRuntimeId);
    DefenseFeatureCommandResult RequestFacilityService(int facilityRuntimeId);
}

public sealed class DefenseFeatureQueryService : IDefenseFeatureQueryService
{
    private const int MaxVisibleCards = 8;

    private readonly IInvasionThreatRuntimeProvider threatProvider;
    private readonly IInvasionDirectorRuntimeProvider directorProvider;
    private readonly IInvasionCombatReportRuntimeProvider reportProvider;
    private readonly IDefenseEngagementRuntime engagementRuntime;
    private readonly IInvasionOwnerEvacuationService ownerEvacuation;
    private readonly IDefenseResponsePolicyRuntime policyRuntime;
    private readonly IStaffWorkforceQueryService workforceQuery;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IDefenseFacilityRuntime defenseFacilities;
    private readonly IInvasionCampaignRuntime campaign;
    private readonly IFactionRuntime factions;

    public DefenseFeatureQueryService(
        IInvasionThreatRuntimeProvider threatProvider,
        IInvasionDirectorRuntimeProvider directorProvider,
        IInvasionCombatReportRuntimeProvider reportProvider,
        IDefenseEngagementRuntime engagementRuntime,
        IInvasionOwnerEvacuationService ownerEvacuation,
        IDefenseResponsePolicyRuntime policyRuntime,
        IStaffWorkforceQueryService workforceQuery,
        IBuildingWorldQuery buildingWorld,
        IDefenseFacilityRuntime defenseFacilities,
        IInvasionCampaignRuntime campaign,
        IFactionRuntime factions)
    {
        this.threatProvider = threatProvider
            ?? throw new ArgumentNullException(nameof(threatProvider));
        this.directorProvider = directorProvider
            ?? throw new ArgumentNullException(nameof(directorProvider));
        this.reportProvider = reportProvider
            ?? throw new ArgumentNullException(nameof(reportProvider));
        this.engagementRuntime = engagementRuntime
            ?? throw new ArgumentNullException(nameof(engagementRuntime));
        this.ownerEvacuation = ownerEvacuation
            ?? throw new ArgumentNullException(nameof(ownerEvacuation));
        this.policyRuntime = policyRuntime
            ?? throw new ArgumentNullException(nameof(policyRuntime));
        this.workforceQuery = workforceQuery
            ?? throw new ArgumentNullException(nameof(workforceQuery));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.defenseFacilities = defenseFacilities
            ?? throw new ArgumentNullException(nameof(defenseFacilities));
        this.campaign = campaign
            ?? throw new ArgumentNullException(nameof(campaign));
        this.factions = factions
            ?? throw new ArgumentNullException(nameof(factions));
    }

    public DefenseFeatureSurfaceModel Capture(string selectedPolicyId)
    {
        threatProvider.TryGetRuntime(out InvasionThreatRuntime threat);
        directorProvider.TryGetRuntime(out InvasionDirectorRuntime director);
        reportProvider.TryGetRuntime(out InvasionCombatReportRuntime reports);

        IReadOnlyList<InvasionIntruderRuntime> intruders = director != null
            ? director.ActiveIntruders
            : Array.Empty<InvasionIntruderRuntime>();
        DefenseResponsePolicyData selected = ResolveSelectedPolicy(selectedPolicyId);
        return new DefenseFeatureSurfaceModel
        {
            ThreatSummary = CreateThreatSummary(threat),
            ThreatFactors = threat != null
                ? $"현재 요인: {threat.LatestSnapshot.factors}"
                : "침공 위협 시스템을 불러오지 못했습니다.",
            CampaignSummary = CreateCampaignSummary(),
            ReinforcementSummary = CreateReinforcementSummary(),
            OwnerEvacuationSummary = CreateOwnerEvacuationSummary(),
            Intruders = intruders
                .Take(MaxVisibleCards)
                .Select(CreateIntruderRow)
                .ToArray(),
            Policies = policyRuntime.Policies
                .Select((policy, index) => CreatePolicyRow(policy, index, selected?.id))
                .ToArray(),
            SelectedPolicy = selected != null
                ? CreatePolicyRow(selected, 0, selected.id)
                : null,
            Guards = CreateGuardRows(selected),
            Facilities = buildingWorld.Buildings
                .OfType<DefenseFacility>()
                .Where(facility =>
                    facility != null && !facility.isDestroy && facility.Defense != null)
                .Take(MaxVisibleCards)
                .Select((facility, index) => CreateFacilityRow(facility, index))
                .ToArray(),
            Reports = reports != null
                ? reports.ReportHistory
                    .Take(MaxVisibleCards)
                    .Select((report, index) => CreateReportRow(report, index))
                    .ToArray()
                : Array.Empty<DefenseFeatureReportRow>()
        };
    }

    private DefenseResponsePolicyData ResolveSelectedPolicy(string selectedPolicyId)
    {
        return policyRuntime.Policies.FirstOrDefault(policy =>
                   policy != null
                   && string.Equals(policy.id, selectedPolicyId, StringComparison.Ordinal))
            ?? policyRuntime.Policies.FirstOrDefault();
    }

    private DefenseFeatureIntruderRow CreateIntruderRow(
        InvasionIntruderRuntime intruder,
        int index)
    {
        CharacterActor actor = intruder != null ? intruder.IntruderActor : null;
        InvasionIntruderPatternDefinition pattern = intruder?.Pattern
            ?? InvasionIntruderPatternCatalog.Default;
        DefenseEngagement engagement = null;
        bool hasEngagement = intruder != null
            && engagementRuntime.TryGetEngagement(intruder, out engagement);
        string front = hasEngagement
            ? $"전선 {FormatEngagementState(engagement.State)}"
                + $" / 선두 {GetCharacterName(engagement.LeadGuard)}"
                + $" / 예비 {GetCharacterName(engagement.ReserveGuard)}"
                + $" / 공방 {engagement.ExchangeCount}회"
            : CreateIntruderAdvanceSummary(intruder);
        string target = intruder?.CurrentPriorityTarget != null
            ? GetBuildingName(intruder.CurrentPriorityTarget)
            : "사장 또는 주요 시설";
        return new DefenseFeatureIntruderRow
        {
            Index = index,
            Title = $"{pattern.title} / {(actor != null ? actor.name : "침입자")}",
            Detail = $"상태 {FormatIntruderState(intruder?.State ?? InvasionIntruderState.Finished)}"
                + $" / 집중 {intruder?.Focus ?? 0f:0.#}"
                + $" / 목표 {target}\n{front}"
        };
    }

    private IReadOnlyList<DefenseFeatureGuardRow> CreateGuardRows(
        DefenseResponsePolicyData selected)
    {
        if (selected == null)
        {
            return Array.Empty<DefenseFeatureGuardRow>();
        }

        return workforceQuery.FindActiveWorkers()
            .Where(actor => actor != null && !actor.IsOwner)
            .Select((guard, index) =>
            {
                DefenseResponsePolicyData assigned = policyRuntime.GetPolicy(guard);
                CharacterWorkRoleUtility.TryGetWork(guard, out AbilityWork work);
                string duty = work != null && work.IsOffDuty ? "비번" : "당직";
                string priority = work != null
                    ? work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard).ToString()
                    : "없음";
                return new DefenseFeatureGuardRow
                {
                    Index = index,
                    ActorRuntimeId = guard.GetInstanceID(),
                    Name = GetCharacterName(guard),
                    Detail = $"현재 {assigned?.displayName ?? "표준"}"
                        + $" / {duty} / 경비 우선순위 {priority}",
                    UsesSelectedPolicy = string.Equals(
                        assigned?.id,
                        selected.id,
                        StringComparison.Ordinal)
                };
            })
            .ToArray();
    }

    private static DefenseFeaturePolicyRow CreatePolicyRow(
        DefenseResponsePolicyData policy,
        int index,
        string selectedPolicyId)
    {
        return new DefenseFeaturePolicyRow
        {
            Index = index,
            PolicyId = policy.id,
            DisplayName = policy.displayName,
            Detail = $"출동 체력 {policy.minimumDispatchHealthRatio:P0}"
                + $" / 후퇴 {FormatRetreat(policy.retreatHealthRatio)}"
                + $" / 재참전 {policy.rejoinHealthRatio:P0}"
                + $" / 대체자 없음 {(policy.holdWithoutReplacement ? "사수" : "후퇴")}",
            IsSelected = string.Equals(policy.id, selectedPolicyId, StringComparison.Ordinal),
            IsCustom = policy.kind == DefenseResponsePolicyKind.Custom,
            AutoRespond = policy.autoRespond,
            MinimumDispatchHealthRatio = policy.minimumDispatchHealthRatio,
            RetreatHealthRatio = policy.retreatHealthRatio,
            HoldWithoutReplacement = policy.holdWithoutReplacement,
            RejoinHealthRatio = policy.rejoinHealthRatio
        };
    }

    private DefenseFeatureFacilityRow CreateFacilityRow(
        DefenseFacility facility,
        int index)
    {
        DefenseFacilitySnapshot snapshot =
            defenseFacilities.GetSnapshot(facility);
        int capacity = Mathf.Max(
            0,
            facility.Defense.supplyCapacity
                + (facility.Defense.growth?.capacityLevel ?? 0));
        string supply = facility.Defense.UsesPhysicalSupply
            ? $"{snapshot.Supply}/{capacity}"
            : facility.Defense.requiresPower
                ? (snapshot.Powered ? "전력 정상" : "정전")
                : "보급 불필요";
        return new DefenseFeatureFacilityRow
        {
            Index = index,
            RuntimeId = facility.GetInstanceID(),
            Name = GetBuildingName(facility),
            ArmingPolicy = snapshot.ArmingPolicy,
            OperationalState = snapshot.OperationalState,
            Detail = $"{facility.Defense.concept} / {snapshot.ArmingPolicy}"
                + $" / {snapshot.OperationalState} / 보급 {supply}"
                + $" / 건전도 {snapshot.Condition:0}%"
                + (string.IsNullOrWhiteSpace(snapshot.BlockedReason)
                    ? string.Empty
                    : $"\n차단: {snapshot.BlockedReason}")
        };
    }

    private string CreateCampaignSummary()
    {
        ScheduledInvasionOperationState operation =
            campaign.Operations.LastOrDefault();
        string operationText = operation != null
            ? $"{operation.kind} / 목표 {operation.objectiveId}"
                + $" / 정보 신뢰도 {operation.intelligenceConfidence:P0}"
            : "예정 작전 없음";
        string weakest = campaign.Branches
            .OrderBy(branch => branch.strength)
            .Select(branch =>
                $"{branch.displayName} {branch.strength:0}"
                + (string.IsNullOrWhiteSpace(branch.recoveryReason)
                    ? string.Empty
                    : $" ({branch.recoveryReason})"))
            .FirstOrDefault() ?? "지부 정보 없음";
        return $"{operationText}\n최약 지부: {weakest}";
    }

    private string CreateReinforcementSummary()
    {
        FactionRouteState[] active = factions.Routes
            .Where(route => route != null
                && route.kind == FactionRouteKind.Reinforcement
                && route.status is FactionRouteStatus.Traveling
                    or FactionRouteStatus.Delayed
                    or FactionRouteStatus.Arrived)
            .ToArray();
        if (active.Length == 0)
        {
            return "이동 중이거나 도착한 동맹 지원군이 없습니다.";
        }

        return string.Join(
            "\n",
            active.Select(route =>
                $"{route.factionId} / {route.status}"
                + $" / ETA Day {route.estimatedArrivalDay}"
                + $" / 전력 {route.strength}"));
    }

    private static DefenseFeatureReportRow CreateReportRow(
        InvasionCombatReportSnapshot report,
        int index)
    {
        string outcome = report.Defended ? "방어 성공" : "방어 실패";
        return new DefenseFeatureReportRow
        {
            Index = index,
            Title = $"{outcome} / 위협 {report.ThreatSnapshot.threat:0.#}",
            Summary = $"잔여 위험 {report.ResidualRisk:0.#}"
                + $" / 방어 기여 {report.DefenseContributions.Count}개"
                + $" / 손상 {report.DamagedFacilities.Count}개",
            Detail = report.ToDetailText()
        };
    }

    private static string CreateThreatSummary(InvasionThreatRuntime threat)
    {
        return threat != null
            ? $"위협 {threat.CurrentThreat:0.#}"
                + $" / 단계 {threat.CurrentStage}"
                + $" / 안전 {threat.SafetyRemaining:0.#}초"
                + $" / 예보 {(threat.IsCandidatePending ? "대기" : "없음")}"
            : "위협 정보 없음";
    }

    private string CreateOwnerEvacuationSummary()
    {
        if (!ownerEvacuation.IsEvacuating)
        {
            return "침공이 시작되면 사장이 안전한 내부 칸으로 대피합니다.";
        }

        return $"{ownerEvacuation.StatusText} / 목표 {ownerEvacuation.TargetCell}"
            + (ownerEvacuation.HasReachedTarget ? " / 대피 완료" : string.Empty);
    }

    private static string CreateIntruderAdvanceSummary(InvasionIntruderRuntime intruder)
    {
        if (intruder == null)
        {
            return "상태 없음";
        }

        if (intruder.State == InvasionIntruderState.Rallying)
        {
            return $"외부 집결 {Mathf.CeilToInt(intruder.RallySecondsRemaining)}초 / 경비 대기";
        }

        return intruder.HasBreachedDungeonInterior
            ? "내부 진격 중 / 전선 미형성"
            : "입구 접근 중 / 경비 대기";
    }

    private static string FormatIntruderState(InvasionIntruderState state)
    {
        return state switch
        {
            InvasionIntruderState.Rallying => "외부 집결",
            InvasionIntruderState.Entering => "진입",
            InvasionIntruderState.Searching => "탐색",
            InvasionIntruderState.MovingToOwner => "사장 추적",
            InvasionIntruderState.MovingToFacility => "시설 추적",
            InvasionIntruderState.DamagingFacility => "시설 파괴",
            InvasionIntruderState.InterceptPlanned => "저지 예정",
            InvasionIntruderState.Engaged => "교전",
            InvasionIntruderState.FrontBroken => "전선 돌파",
            InvasionIntruderState.FinalCombat => "최종 교전",
            InvasionIntruderState.Finished => "종료",
            _ => "대기"
        };
    }

    private static string FormatEngagementState(DefenseEngagementState state)
    {
        return state switch
        {
            DefenseEngagementState.Dispatching => "출동",
            DefenseEngagementState.InterceptPlanned => "저지 예정",
            DefenseEngagementState.Engaged => "교전",
            DefenseEngagementState.ReserveWaiting => "교대 대기",
            DefenseEngagementState.Switching => "교대",
            DefenseEngagementState.Retreating => "후퇴",
            DefenseEngagementState.FrontCollapsed => "붕괴",
            _ => "종료"
        };
    }

    private static string FormatRetreat(float ratio)
    {
        return ratio > 0f ? ratio.ToString("P0") : "없음";
    }

    private static string GetCharacterName(CharacterActor actor)
    {
        return actor != null
            ? actor.Identity?.DisplayName ?? actor.name
            : "없음";
    }

    private static string GetBuildingName(BuildableObject building)
    {
        return building != null
            ? building.BuildingData?.objectName ?? building.name
            : "시설";
    }
}

public sealed class DefenseFeatureCommandService : IDefenseFeatureCommandService
{
    private readonly IDefenseResponsePolicyRuntime policyRuntime;
    private readonly ICharacterWorldQuery characterWorld;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IDefenseFacilityRuntime defenseFacilities;

    public DefenseFeatureCommandService(
        IDefenseResponsePolicyRuntime policyRuntime,
        ICharacterWorldQuery characterWorld,
        IBuildingWorldQuery buildingWorld,
        IDefenseFacilityRuntime defenseFacilities)
    {
        this.policyRuntime = policyRuntime
            ?? throw new ArgumentNullException(nameof(policyRuntime));
        this.characterWorld = characterWorld
            ?? throw new ArgumentNullException(nameof(characterWorld));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.defenseFacilities = defenseFacilities
            ?? throw new ArgumentNullException(nameof(defenseFacilities));
    }

    public DefenseFeatureCommandResult ToggleAutoResponse(string policyId)
    {
        return Update(policyId, policy => policy.autoRespond = !policy.autoRespond);
    }

    public DefenseFeatureCommandResult StepMinimumDispatchHealth(string policyId)
    {
        return Update(
            policyId,
            policy => policy.minimumDispatchHealthRatio =
                StepRatio(policy.minimumDispatchHealthRatio));
    }

    public DefenseFeatureCommandResult StepRetreatHealth(string policyId)
    {
        return Update(
            policyId,
            policy => policy.retreatHealthRatio = StepRatio(policy.retreatHealthRatio));
    }

    public DefenseFeatureCommandResult ToggleHoldWithoutReplacement(string policyId)
    {
        return Update(
            policyId,
            policy => policy.holdWithoutReplacement = !policy.holdWithoutReplacement);
    }

    public DefenseFeatureCommandResult StepRejoinHealth(string policyId)
    {
        return Update(
            policyId,
            policy => policy.rejoinHealthRatio = StepRatio(policy.rejoinHealthRatio));
    }

    public DefenseFeatureCommandResult CreatePolicy()
    {
        bool succeeded = policyRuntime.TryCreatePolicy(
            $"새 정책 {policyRuntime.Policies.Count + 1}",
            out DefenseResponsePolicyData created);
        return new DefenseFeatureCommandResult(
            succeeded,
            succeeded ? $"방어 정책 생성: {created.displayName}" : "방어 정책을 만들지 못했습니다.",
            created?.id);
    }

    public DefenseFeatureCommandResult DuplicatePolicy(string policyId)
    {
        DefenseResponsePolicyData source = FindPolicy(policyId);
        DefenseResponsePolicyData duplicate = null;
        bool succeeded = source != null
            && policyRuntime.TryDuplicatePolicy(
                source.id,
                $"{source.displayName} 사본",
                out duplicate);
        return new DefenseFeatureCommandResult(
            succeeded,
            succeeded ? $"방어 정책 복제: {duplicate.displayName}" : "방어 정책을 복제하지 못했습니다.",
            succeeded ? duplicate.id : string.Empty);
    }

    public DefenseFeatureCommandResult DeletePolicy(string policyId)
    {
        bool succeeded = policyRuntime.TryDeletePolicy(policyId, reassignToStandard: true);
        return new DefenseFeatureCommandResult(
            succeeded,
            succeeded ? "정책을 삭제하고 경비를 표준 정책으로 재배정했습니다." : "기본 정책은 삭제할 수 없습니다.",
            succeeded ? DefenseResponsePolicyRuntime.StandardPolicyId : policyId);
    }

    public DefenseFeatureCommandResult AssignPolicy(int actorRuntimeId, string policyId)
    {
        CharacterActor actor = characterWorld.Characters.FirstOrDefault(candidate =>
            candidate != null && candidate.GetInstanceID() == actorRuntimeId);
        if (actor == null)
        {
            return new DefenseFeatureCommandResult(false, "배정할 경비를 찾지 못했습니다.");
        }

        bool succeeded = policyRuntime.AssignPolicy(actor, policyId);
        return new DefenseFeatureCommandResult(
            succeeded,
            succeeded
                ? $"{actor.Identity?.DisplayName ?? actor.name}: 정책 배정 완료"
                : "정책을 배정하지 못했습니다.");
    }

    public DefenseFeatureCommandResult CycleFacilityArmingPolicy(
        int facilityRuntimeId)
    {
        DefenseFacility facility = FindFacility(facilityRuntimeId);
        if (facility == null)
        {
            return new DefenseFeatureCommandResult(
                false,
                "방어시설을 찾을 수 없습니다.");
        }

        DefenseArmingPolicy current =
            defenseFacilities.GetSnapshot(facility).ArmingPolicy;
        DefenseArmingPolicy next =
            (DefenseArmingPolicy)(((int)current + 1) % 4);
        bool succeeded =
            defenseFacilities.SetArmingPolicy(facility, next, out string warning);
        return new DefenseFeatureCommandResult(
            succeeded,
            succeeded
                ? $"무장 정책 {current} → {next}"
                    + (string.IsNullOrWhiteSpace(warning)
                        ? string.Empty
                        : $" / {warning}")
                : "무장 정책을 변경하지 못했습니다.");
    }

    public DefenseFeatureCommandResult RequestFacilityService(
        int facilityRuntimeId)
    {
        DefenseFacility facility = FindFacility(facilityRuntimeId);
        if (facility == null)
        {
            return new DefenseFeatureCommandResult(
                false,
                "방어시설을 찾을 수 없습니다.");
        }

        DefenseFacilitySnapshot snapshot =
            defenseFacilities.GetSnapshot(facility);
        bool succeeded;
        string message;
        if (snapshot.OperationalState == DefenseFacilityOperationalState.Jammed)
        {
            succeeded = defenseFacilities.TryClearJam(
                facility,
                out message);
        }
        else
        {
            succeeded = defenseFacilities.TryRequestReload(
                facility,
                out message);
        }

        return new DefenseFeatureCommandResult(
            succeeded,
            message);
    }

    private DefenseFacility FindFacility(int runtimeId)
    {
        return buildingWorld.Buildings
            .OfType<DefenseFacility>()
            .FirstOrDefault(facility =>
                facility != null
                && facility.GetInstanceID() == runtimeId);
    }

    private DefenseFeatureCommandResult Update(
        string policyId,
        Action<DefenseResponsePolicyData> mutate)
    {
        DefenseResponsePolicyData source = FindPolicy(policyId);
        if (source == null)
        {
            return new DefenseFeatureCommandResult(false, "방어 정책을 찾지 못했습니다.");
        }

        DefenseResponsePolicyData edited = source.Clone();
        mutate(edited);
        bool succeeded = policyRuntime.TryUpdatePolicy(edited);
        return new DefenseFeatureCommandResult(
            succeeded,
            succeeded ? $"방어 정책 갱신: {edited.displayName}" : "방어 정책을 갱신하지 못했습니다.");
    }

    private DefenseResponsePolicyData FindPolicy(string policyId)
    {
        return policyRuntime.Policies.FirstOrDefault(policy =>
            policy != null
            && string.Equals(policy.id, policyId, StringComparison.Ordinal));
    }

    private static float StepRatio(float current)
    {
        float next = Mathf.Round((Mathf.Clamp01(current) + 0.05f) * 20f) / 20f;
        return next > 1f ? 0f : next;
    }
}

public sealed class DefenseFeatureSurfacePresenter : IFeatureSurfaceTabPresenter
{
    private const float CompactCardHeight = 92f;
    private const float DetailCardHeight = 164f;

    private readonly IDefenseFeatureQueryService query;
    private readonly IDefenseFeatureCommandService commands;
    private string selectedPolicyId = DefenseResponsePolicyRuntime.StandardPolicyId;
    private string pendingDeletePolicyId = string.Empty;
    private int selectedReportIndex = -1;

    public DefenseFeatureSurfacePresenter(
        IDefenseFeatureQueryService query,
        IDefenseFeatureCommandService commands)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public TabId Id => TabId.Defense;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        DefenseFeatureSurfaceModel model = query.Capture(selectedPolicyId);
        if (model.SelectedPolicy != null)
        {
            selectedPolicyId = model.SelectedPolicy.PolicyId;
        }

        view.AddSection("침공 위협", model.ThreatSummary);
        view.AddLabel(model.ThreatFactors, 17f, 44f);
        view.AddSection("인간 연합 작전", model.CampaignSummary);
        view.AddSection("동맹 지원군", model.ReinforcementSummary);
        view.AddSection("침입자 추적", $"활성 침입자 {model.Intruders.Count}명");
        if (model.Intruders.Count == 0)
        {
            view.AddLabel("현재 던전은 안전합니다.", 18f, 44f);
        }

        foreach (DefenseFeatureIntruderRow row in model.Intruders)
        {
            DefenseFeatureIntruderRow captured = row;
            view.AddDataCard(
                $"P1Action_IntruderTrack_{captured.Index}",
                captured.Title,
                captured.Detail,
                "추적",
                () => view.ShowFeedback(captured.Detail.Replace("\n", " / ")),
                116f);
        }

        view.AddSection("사장 대피", model.OwnerEvacuationSummary);
        AddPolicies(view, model);
        AddFacilities(view, model);
        AddReports(view, model);
    }

    private void AddPolicies(IFeatureSurfaceView view, DefenseFeatureSurfaceModel model)
    {
        view.AddSection(
            "경비 대응 정책",
            model.SelectedPolicy != null
                ? $"정책 {model.Policies.Count}개 / 선택 {model.SelectedPolicy.DisplayName}"
                : "사용 가능한 정책이 없습니다.");
        foreach (DefenseFeaturePolicyRow row in model.Policies)
        {
            DefenseFeaturePolicyRow captured = row;
            view.AddDataCard(
                $"P1Action_DefensePolicySelect_{captured.Index}",
                captured.DisplayName,
                captured.Detail,
                captured.IsSelected ? "선택됨" : "선택",
                () =>
                {
                    selectedPolicyId = captured.PolicyId;
                    pendingDeletePolicyId = string.Empty;
                    view.ShowFeedback($"방어 정책 선택: {captured.DisplayName}");
                    view.RequestRefresh();
                },
                CompactCardHeight);
        }

        DefenseFeaturePolicyRow selected = model.SelectedPolicy;
        if (selected == null)
        {
            return;
        }

        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyAuto",
            selected.AutoRespond ? "자동 출동 끄기" : "자동 출동 켜기",
            "당직 중이고 경비 우선순위가 켜진 직원에게만 적용됩니다.",
            () => commands.ToggleAutoResponse(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyDispatchHealth",
            $"최소 출동 체력 {selected.MinimumDispatchHealthRatio:P0}",
            "누를 때마다 5%씩 조정합니다.",
            () => commands.StepMinimumDispatchHealth(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyRetreatHealth",
            $"후퇴 체력 {FormatRetreat(selected.RetreatHealthRatio)}",
            "0%는 자동 후퇴 없음입니다.",
            () => commands.StepRetreatHealth(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyHold",
            selected.HoldWithoutReplacement
                ? "대체자 없으면 사수"
                : "대체자 없어도 후퇴",
            "예비 경비가 없을 때 선두 경비의 행동을 결정합니다.",
            () => commands.ToggleHoldWithoutReplacement(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyRejoinHealth",
            $"치료 후 재참전 {selected.RejoinHealthRatio:P0}",
            "후퇴한 경비가 다시 출동할 최소 체력입니다.",
            () => commands.StepRejoinHealth(selected.PolicyId));
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyCreate",
            "새 정책",
            "현재 기본값으로 사용자 정책을 만듭니다.",
            commands.CreatePolicy,
            selectReturnedEntity: true);
        AddPolicyCommand(
            view,
            "P1Action_DefensePolicyDuplicate",
            "정책 복제",
            "현재 정책을 새 사용자 정책으로 복제합니다.",
            () => commands.DuplicatePolicy(selected.PolicyId),
            selectReturnedEntity: true);

        if (selected.IsCustom)
        {
            bool confirming = string.Equals(
                pendingDeletePolicyId,
                selected.PolicyId,
                StringComparison.Ordinal);
            view.AddDataCard(
                "P1Action_DefensePolicyDelete",
                confirming ? "삭제 확정" : "정책 삭제",
                confirming
                    ? "배정된 경비는 표준 정책으로 재배정됩니다."
                    : "한 번 더 눌러 삭제를 확정합니다.",
                "실행",
                () =>
                {
                    if (!confirming)
                    {
                        pendingDeletePolicyId = selected.PolicyId;
                        view.ShowFeedback("정책 삭제를 한 번 더 확인하세요.");
                    }
                    else
                    {
                        DefenseFeatureCommandResult result = commands.DeletePolicy(selected.PolicyId);
                        selectedPolicyId = result.Succeeded
                            ? DefenseResponsePolicyRuntime.StandardPolicyId
                            : selectedPolicyId;
                        pendingDeletePolicyId = string.Empty;
                        view.ShowFeedback(result.Message);
                    }

                    view.RequestRefresh();
                },
                CompactCardHeight);
        }

        view.AddSection(
            "경비별 정책 배정",
            $"직원 {model.Guards.Count}명 / 배정할 정책 {selected.DisplayName}");
        foreach (DefenseFeatureGuardRow guard in model.Guards)
        {
            DefenseFeatureGuardRow captured = guard;
            view.AddDataCard(
                $"P1Action_DefensePolicyAssign_{captured.Index}",
                captured.Name,
                captured.Detail,
                captured.UsesSelectedPolicy ? "배정됨" : "이 정책 배정",
                () =>
                {
                    DefenseFeatureCommandResult result = commands.AssignPolicy(
                        captured.ActorRuntimeId,
                        selected.PolicyId);
                    view.ShowFeedback(result.Message);
                    view.RequestRefresh();
                },
                CompactCardHeight);
        }
    }

    private void AddFacilities(
        IFeatureSurfaceView view,
        DefenseFeatureSurfaceModel model)
    {
        view.AddSection("방어 시설", $"가동 시설 {model.Facilities.Count}개");
        foreach (DefenseFeatureFacilityRow facility in model.Facilities)
        {
            DefenseFeatureFacilityRow captured = facility;
            view.AddDataCard(
                $"P1Action_DefenseFacilityPolicy_{captured.Index}",
                captured.Name,
                captured.Detail,
                "무장 전환",
                () =>
                {
                    DefenseFeatureCommandResult result =
                        commands.CycleFacilityArmingPolicy(
                            captured.RuntimeId);
                    view.ShowFeedback(result.Message);
                    view.RequestRefresh();
                },
                CompactCardHeight);
            if (captured.OperationalState is
                    DefenseFacilityOperationalState.Empty
                    or DefenseFacilityOperationalState.Reloading
                    or DefenseFacilityOperationalState.Jammed)
            {
                view.AddDataCard(
                    $"P1Action_DefenseFacilityService_{captured.Index}",
                    captured.OperationalState
                        == DefenseFacilityOperationalState.Jammed
                            ? "걸림 해제"
                            : "재장전 요청",
                    captured.Detail,
                    "실행",
                    () =>
                    {
                        DefenseFeatureCommandResult result =
                            commands.RequestFacilityService(
                                captured.RuntimeId);
                        view.ShowFeedback(result.Message);
                        view.RequestRefresh();
                    },
                    CompactCardHeight);
            }
        }
    }

    private void AddReports(IFeatureSurfaceView view, DefenseFeatureSurfaceModel model)
    {
        view.AddSection("침공 전투 보고", $"완료 기록 {model.Reports.Count}건");
        foreach (DefenseFeatureReportRow report in model.Reports)
        {
            DefenseFeatureReportRow captured = report;
            bool selected = selectedReportIndex == captured.Index;
            view.AddDataCard(
                $"P1Action_CombatReport_{captured.Index}",
                captured.Title,
                selected ? captured.Detail : captured.Summary,
                selected ? "선택됨" : "상세",
                () =>
                {
                    selectedReportIndex = captured.Index;
                    view.ShowFeedback(captured.Title);
                    view.RequestRefresh();
                },
                selected ? DetailCardHeight : CompactCardHeight);
        }
    }

    private void AddPolicyCommand(
        IFeatureSurfaceView view,
        string actionName,
        string title,
        string detail,
        Func<DefenseFeatureCommandResult> execute,
        bool selectReturnedEntity = false)
    {
        view.AddDataCard(
            actionName,
            title,
            detail,
            "실행",
            () =>
            {
                DefenseFeatureCommandResult result = execute();
                if (selectReturnedEntity
                    && result.Succeeded
                    && !string.IsNullOrWhiteSpace(result.EntityId))
                {
                    selectedPolicyId = result.EntityId;
                }

                view.ShowFeedback(result.Message);
                view.RequestRefresh();
            },
            CompactCardHeight);
    }

    private static string FormatRetreat(float ratio)
    {
        return ratio > 0f ? ratio.ToString("P0") : "없음";
    }
}
