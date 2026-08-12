using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ExpeditionFeatureSurfaceModel
{
    public bool IsAvailable { get; set; }
    public bool TruthRevealed { get; set; }
    public bool CanLaunchExpedition { get; set; }
    public string ExpeditionBlocker { get; set; } = string.Empty;
    public string CampaignSummary { get; set; } = string.Empty;
    public string CampaignGuidance { get; set; } = string.Empty;
    public string SelectedTargetId { get; set; } = string.Empty;
    public int AvailableMemberCount { get; set; }
    public IReadOnlyList<ExpeditionFeatureTargetRow> Targets { get; set; }
        = Array.Empty<ExpeditionFeatureTargetRow>();
    public string RewardSummary { get; set; } = string.Empty;
    public IReadOnlyList<ExpeditionFeatureResultRow> Results { get; set; }
        = Array.Empty<ExpeditionFeatureResultRow>();
}

public sealed class ExpeditionFeatureTargetRow
{
    public int Index { get; set; }
    public string TargetId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public int RequiredMembers { get; set; }
    public bool IsSelected { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsCompleted { get; set; }
}

public sealed class ExpeditionFeatureResultRow
{
    public int Index { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}

public readonly struct ExpeditionFeatureCommandResult
{
    public ExpeditionFeatureCommandResult(bool succeeded, string message)
    {
        Succeeded = succeeded;
        Message = message ?? string.Empty;
    }

    public bool Succeeded { get; }
    public string Message { get; }
}

public interface IExpeditionFeatureQueryService
{
    ExpeditionFeatureSurfaceModel Capture();
}

public interface IExpeditionFeatureCommandService
{
    ExpeditionFeatureCommandResult OpenWorldMap();
    ExpeditionFeatureCommandResult OpenExpedition();
    ExpeditionFeatureCommandResult UpgradeRecon();
    ExpeditionFeatureCommandResult QueueSelectedRegionRecon();
    ExpeditionFeatureCommandResult SelectTarget(string targetId);
    ExpeditionFeatureCommandResult StartSelectedTarget();
}

public sealed class ExpeditionFeatureQueryService : IExpeditionFeatureQueryService
{
    private const int MaxVisibleCards = 6;

    private readonly IOffenseQuery offense;
    private readonly RegularCustomerRuntime regularCustomers;
    private readonly ICaptivityRuntime captivityRuntime;
    private readonly IOffenseReturnArrivalRuntime returnArrivalRuntime;
    private readonly IOffenseRegionRuntime regionRuntime;

    public ExpeditionFeatureQueryService(
        IOffenseQuery offense,
        RegularCustomerRuntime regularCustomers,
        ICaptivityRuntime captivityRuntime,
        IOffenseReturnArrivalRuntime returnArrivalRuntime,
        IOffenseRegionRuntime regionRuntime)
    {
        this.offense = offense ?? throw new ArgumentNullException(nameof(offense));
        this.regularCustomers = regularCustomers
            ?? throw new ArgumentNullException(nameof(regularCustomers));
        this.captivityRuntime = captivityRuntime
            ?? throw new ArgumentNullException(nameof(captivityRuntime));
        this.returnArrivalRuntime = returnArrivalRuntime
            ?? throw new ArgumentNullException(nameof(returnArrivalRuntime));
        this.regionRuntime = regionRuntime
            ?? throw new ArgumentNullException(nameof(regionRuntime));
    }

    public ExpeditionFeatureSurfaceModel Capture()
    {
        OffenseCampaignSnapshot campaign = offense.Capture();
        if (!campaign.IsAvailable)
        {
            return new ExpeditionFeatureSurfaceModel
            {
                IsAvailable = false,
                CampaignSummary = "원정 시스템을 불러오지 못했습니다."
            };
        }

        IReadOnlyList<OffenseTargetSnapshot> targets = campaign.VisibleTargets;
        return new ExpeditionFeatureSurfaceModel
        {
            IsAvailable = true,
            TruthRevealed = campaign.TruthRevealed,
            CanLaunchExpedition = campaign.CanLaunchExpedition,
            ExpeditionBlocker = campaign.ExpeditionBlocker,
            CampaignSummary = campaign.TruthRevealed
                ? "진실이 밝혀졌습니다."
                : $"진실 추적 {campaign.CompletedTargetCount}/{campaign.CampaignTargetCount}"
                    + $" / 정찰 Lv.{campaign.ReconLevel}"
                    + $" / 출정 중 {campaign.ActiveExpeditions.Count}",
            CampaignGuidance = campaign.TruthRevealed
                ? OffenseWorldMapService.TruthRevealText
                : "목표를 순서대로 완료하고 마지막 원정에서 던전의 진실을 밝혀내세요.",
            SelectedTargetId = campaign.SelectedTargetId,
            AvailableMemberCount = campaign.AvailableMemberCount,
            Targets = CreateTargetRows(
                targets,
                campaign.SelectedTargetId),
            RewardSummary = CreateRewardSummary(campaign),
            Results = campaign.ResultHistory
                .Take(MaxVisibleCards)
                .Select((result, index) => new ExpeditionFeatureResultRow
                {
                    Index = index,
                    Title = $"{result.targetTitle} / {(result.success ? "성공" : "실패")}",
                    Detail = result.rewardSummaries.Count > 0
                        ? string.Join(", ", result.rewardSummaries)
                        : "즉시 지급된 보상이 없습니다."
                })
                .ToArray()
        };
    }

    private IReadOnlyList<ExpeditionFeatureTargetRow> CreateTargetRows(
        IReadOnlyList<OffenseTargetSnapshot> targets,
        string selectedTargetId)
    {
        IEnumerable<OffenseTargetSnapshot> displayTargets = targets
            .Where(target => !target.isCompleted)
            .OrderBy(target => target.campaignOrder)
            .Take(MaxVisibleCards);
        if (!displayTargets.Any())
        {
            displayTargets = targets
                .OrderByDescending(target => target.campaignOrder)
                .Take(MaxVisibleCards);
        }

        return displayTargets
            .Select((target, index) =>
            {
                OffenseStrategicPressureSnapshot pressure =
                    regionRuntime.GetPressureForTarget(
                        offense.TryGetTargetDefinition(
                            target.id,
                            out OffenseTargetDefinition definition)
                            ? definition
                            : null);
                string regionDetail = string.IsNullOrWhiteSpace(
                    target.regionDisplayName)
                    ? string.Empty
                    : $" / {target.regionDisplayName}"
                        + $" [물류 {pressure.Logistics:0}"
                        + $"·무장 {pressure.Armament:0}"
                        + $"·병력 {pressure.Manpower:0}"
                        + $"·정보 {pressure.Intelligence:0}]";
                return new ExpeditionFeatureTargetRow
                {
                    Index = index,
                    TargetId = target.id,
                    Title = $"{target.campaignOrder}. {target.title}"
                        + (target.revealsTruth ? " [최종]" : string.Empty),
                    Detail = $"{target.statusMessage} / 위험 {target.danger:0.#}"
                        + $" / 인원 {target.requiredMembers}"
                        + $" / 적 {OffenseEncounterCatalog.GetEnemySummary(target.campaignOrder)}"
                        + regionDetail,
                    RequiredMembers = target.requiredMembers,
                    IsSelected = string.Equals(
                        selectedTargetId,
                        target.id,
                        StringComparison.Ordinal),
                    IsAvailable = target.isAvailable,
                    IsCompleted = target.isCompleted
                };
            })
            .ToArray();
    }

    private string CreateRewardSummary(OffenseCampaignSnapshot campaign)
    {
        int recruitCandidates = regularCustomers.State.Records.Count(record =>
            record != null && record.IsRecruitCandidate && !record.IsRecruited);
        int prisoners = captivityRuntime.Captives.Count(captive =>
            captive != null
            && captive.status is not CaptivityStatus.Released
            and not CaptivityStatus.Escaped
            and not CaptivityStatus.Dead
            and not CaptivityStatus.Recruited);
        int arrivingAnimals = returnArrivalRuntime.Arrivals
            .Where(arrival => arrival != null
                && arrival.kind == OffenseReturnArrivalKind.SpecialWildlife
                && arrival.stage is not OffenseReturnArrivalStage.Secured
                and not OffenseReturnArrivalStage.Escaped)
            .Sum(arrival => Mathf.Max(0, arrival.requestedAmount));
        return $"회수 전리품 추정가 {campaign.RecoveredLootValue}"
            + $" / 영입 후보 {recruitCandidates}"
            + $" / 수용 포로 {prisoners}"
            + $" / 귀환 동물 {arrivingAnimals}";
    }
}

public sealed class ExpeditionFeatureCommandService : IExpeditionFeatureCommandService
{
    private readonly IOffenseQuery offenseQuery;
    private readonly IOffenseApplication offenseApplication;
    private readonly IKnowledgeResidueProcessingRuntime knowledgeProcessing;

    public ExpeditionFeatureCommandService(
        IOffenseQuery offenseQuery,
        IOffenseApplication offenseApplication,
        IKnowledgeResidueProcessingRuntime knowledgeProcessing)
    {
        this.offenseQuery = offenseQuery
            ?? throw new ArgumentNullException(nameof(offenseQuery));
        this.offenseApplication = offenseApplication
            ?? throw new ArgumentNullException(nameof(offenseApplication));
        this.knowledgeProcessing = knowledgeProcessing
            ?? throw new ArgumentNullException(nameof(knowledgeProcessing));
    }

    public ExpeditionFeatureCommandResult OpenWorldMap()
    {
        bool succeeded = offenseApplication.TryOpenWorldMap(out string message);
        return new ExpeditionFeatureCommandResult(succeeded, message);
    }

    public ExpeditionFeatureCommandResult OpenExpedition()
    {
        bool succeeded = offenseApplication.TryOpenExpedition(out string message);
        return new ExpeditionFeatureCommandResult(succeeded, message);
    }

    public ExpeditionFeatureCommandResult UpgradeRecon()
    {
        bool succeeded = offenseApplication.TryUpgradeRecon(out string message);
        return new ExpeditionFeatureCommandResult(succeeded, message);
    }

    public ExpeditionFeatureCommandResult QueueSelectedRegionRecon()
    {
        OffenseCampaignSnapshot campaign = offenseQuery.Capture();
        if (!campaign.IsAvailable
            || string.IsNullOrWhiteSpace(campaign.SelectedTargetId)
            || !offenseQuery.TryGetTargetDefinition(
                campaign.SelectedTargetId,
                out OffenseTargetDefinition target))
        {
            return new ExpeditionFeatureCommandResult(
                false,
                "기억으로 정찰할 원정 목표를 먼저 선택하세요.");
        }

        bool succeeded = knowledgeProcessing.TryQueueRegionReconnaissance(
            target.regionId,
            out string message);
        return new ExpeditionFeatureCommandResult(succeeded, message);
    }

    public ExpeditionFeatureCommandResult SelectTarget(string targetId)
    {
        bool succeeded = offenseApplication.TrySelectTarget(targetId, out string message);
        return new ExpeditionFeatureCommandResult(succeeded, message);
    }

    public ExpeditionFeatureCommandResult StartSelectedTarget()
    {
        bool succeeded = offenseApplication.TryStartSelectedTarget(out string message);
        return new ExpeditionFeatureCommandResult(succeeded, message);
    }

    private static ExpeditionFeatureCommandResult MissingRuntime(string feature)
    {
        return new ExpeditionFeatureCommandResult(
            false,
            $"{feature} 시스템을 불러오지 못했습니다.");
    }
}

public sealed class ExpeditionFeatureSurfacePresenter : IFeatureSurfaceTabPresenter
{
    private const float CompactCardHeight = 92f;

    private readonly IExpeditionFeatureQueryService query;
    private readonly IExpeditionFeatureCommandService commands;
    private int selectedResultIndex = -1;

    public ExpeditionFeatureSurfacePresenter(
        IExpeditionFeatureQueryService query,
        IExpeditionFeatureCommandService commands)
    {
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    public TabId Id => TabId.Expedition;

    public void Present(IFeatureSurfaceView view)
    {
        if (view == null)
        {
            throw new ArgumentNullException(nameof(view));
        }

        ExpeditionFeatureSurfaceModel model = query.Capture();
        view.AddSection("원정", model.CampaignSummary);
        if (!model.IsAvailable)
        {
            return;
        }

        view.AddLabel(model.CampaignGuidance, 18f, model.TruthRevealed ? 86f : 52f);
        AddCommandCard(
            view,
            "P1Action_OffenseOpenMap",
            "원정 지도 열기",
            $"선택 목표: {FormatOptional(model.SelectedTargetId)}",
            commands.OpenWorldMap,
            refresh: false);
        AddCommandCard(
            view,
            "P1Action_OffenseOpenExpedition",
            "원정 편성 열기",
            model.CanLaunchExpedition
                ? $"참가 가능한 직원 {model.AvailableMemberCount}명"
                : model.ExpeditionBlocker,
            commands.OpenExpedition,
            refresh: false);
        AddCommandCard(
            view,
            "P1Action_OffenseRecon",
            "정찰 강화",
            "정찰 범위를 넓혀 새로운 원정 목표를 발견합니다.",
            commands.UpgradeRecon);

        foreach (ExpeditionFeatureTargetRow row in model.Targets)
        {
            ExpeditionFeatureTargetRow captured = row;
            view.AddDataCard(
                $"P1Action_OffenseTarget_{captured.Index}",
                captured.Title,
                captured.Detail,
                captured.IsCompleted
                    ? "완료"
                    : captured.IsSelected
                        ? "선택됨"
                        : captured.IsAvailable
                            ? "목표 선택"
                            : "잠김",
                () =>
                {
                    ExpeditionFeatureCommandResult result = commands.SelectTarget(captured.TargetId);
                    view.ShowFeedback(result.Message);
                    view.RequestRefresh();
                },
                CompactCardHeight);
        }

        ExpeditionFeatureTargetRow selected = model.Targets.FirstOrDefault(row =>
            row.IsSelected && row.IsAvailable && !row.IsCompleted);
        if (selected != null)
        {
            AddCommandCard(
                view,
                "P1Action_OffenseMemoryRecon",
                "선택 지역 기억 정찰",
                "기억 잔재 1개를 연구 시설로 운반하고 분석해 지역 정보망을 약화합니다.",
                commands.QueueSelectedRegionRecon);
            AddCommandCard(
                view,
                "P1Action_OffenseStart",
                "선택 목표 출정",
                model.CanLaunchExpedition
                    ? $"{selected.Title} / 필요 인원 {selected.RequiredMembers}명"
                    : model.ExpeditionBlocker,
                commands.StartSelectedTarget);
        }

        view.AddSection("원정 보상", model.RewardSummary);
        foreach (ExpeditionFeatureResultRow result in model.Results)
        {
            ExpeditionFeatureResultRow captured = result;
            bool expanded = selectedResultIndex == captured.Index;
            view.AddDataCard(
                $"P1Action_ExpeditionReward_{captured.Index}",
                captured.Title,
                captured.Detail,
                expanded ? "선택됨" : "결과 상세",
                () =>
                {
                    selectedResultIndex = captured.Index;
                    view.ShowFeedback($"{captured.Title}: {captured.Detail}");
                    view.RequestRefresh();
                },
                expanded ? 150f : CompactCardHeight);
        }
    }

    private static void AddCommandCard(
        IFeatureSurfaceView view,
        string actionName,
        string title,
        string detail,
        Func<ExpeditionFeatureCommandResult> execute,
        bool refresh = true)
    {
        view.AddDataCard(
            actionName,
            title,
            detail,
            "실행",
            () =>
            {
                ExpeditionFeatureCommandResult result = execute();
                view.ShowFeedback(result.Message);
                if (refresh)
                {
                    view.RequestRefresh();
                }
            },
            CompactCardHeight);
    }

    private static string FormatOptional(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "없음" : value;
    }
}
