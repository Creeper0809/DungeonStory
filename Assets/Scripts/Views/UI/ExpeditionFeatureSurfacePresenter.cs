using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class ExpeditionFeatureSurfaceModel
{
    public bool IsAvailable { get; set; }
    public bool TruthRevealed { get; set; }
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

    private readonly IOffenseWorldMapRuntimeProvider worldMapProvider;
    private readonly IOffenseExpeditionRuntimeProvider expeditionProvider;
    private readonly IOffenseRewardRuntimeProvider rewardProvider;
    private readonly IRegularCustomerRuntimeProvider regularCustomerProvider;
    private readonly ICaptivityRuntime captivityRuntime;
    private readonly IOffenseReturnArrivalRuntime returnArrivalRuntime;
    private readonly IOffenseRegionRuntime regionRuntime;

    public ExpeditionFeatureQueryService(
        IOffenseWorldMapRuntimeProvider worldMapProvider,
        IOffenseExpeditionRuntimeProvider expeditionProvider,
        IOffenseRewardRuntimeProvider rewardProvider,
        IRegularCustomerRuntimeProvider regularCustomerProvider,
        ICaptivityRuntime captivityRuntime,
        IOffenseReturnArrivalRuntime returnArrivalRuntime,
        IOffenseRegionRuntime regionRuntime)
    {
        this.worldMapProvider = worldMapProvider
            ?? throw new ArgumentNullException(nameof(worldMapProvider));
        this.expeditionProvider = expeditionProvider
            ?? throw new ArgumentNullException(nameof(expeditionProvider));
        this.rewardProvider = rewardProvider
            ?? throw new ArgumentNullException(nameof(rewardProvider));
        this.regularCustomerProvider = regularCustomerProvider
            ?? throw new ArgumentNullException(nameof(regularCustomerProvider));
        this.captivityRuntime = captivityRuntime
            ?? throw new ArgumentNullException(nameof(captivityRuntime));
        this.returnArrivalRuntime = returnArrivalRuntime
            ?? throw new ArgumentNullException(nameof(returnArrivalRuntime));
        this.regionRuntime = regionRuntime
            ?? throw new ArgumentNullException(nameof(regionRuntime));
    }

    public ExpeditionFeatureSurfaceModel Capture()
    {
        if (!worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap)
            || !expeditionProvider.TryGetRuntime(out OffenseExpeditionRuntime expeditions))
        {
            return new ExpeditionFeatureSurfaceModel
            {
                IsAvailable = false,
                CampaignSummary = "원정 시스템을 불러오지 못했습니다."
            };
        }

        IOffenseWorldMapStateView state = worldMap.State;
        IReadOnlyList<OffenseTargetSnapshot> targets = worldMap.VisibleTargets;
        return new ExpeditionFeatureSurfaceModel
        {
            IsAvailable = true,
            TruthRevealed = state.TruthRevealed,
            CampaignSummary = state.TruthRevealed
                ? "진실이 밝혀졌습니다."
                : $"진실 추적 {state.CompletedTargetCount}/{worldMap.CampaignTargetCount}"
                    + $" / 정찰 Lv.{state.ReconLevel}"
                    + $" / 출정 중 {expeditions.ActiveExpeditions.Count}",
            CampaignGuidance = state.TruthRevealed
                ? OffenseWorldMapService.TruthRevealText
                : "목표를 순서대로 완료하고 마지막 원정에서 던전의 진실을 밝혀내세요.",
            SelectedTargetId = state.SelectedTargetId,
            AvailableMemberCount = expeditions.GetAvailableMemberActors().Count,
            Targets = CreateTargetRows(
                worldMap,
                targets,
                state.SelectedTargetId),
            RewardSummary = CreateRewardSummary(),
            Results = expeditions.ResultHistory
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
        OffenseWorldMapRuntime worldMap,
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
                        worldMap != null
                        && worldMap.TryGetTargetDefinition(
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

    private string CreateRewardSummary()
    {
        if (!rewardProvider.TryGetRuntime(out OffenseRewardRuntime rewards))
        {
            return "보상 기록을 불러오지 못했습니다.";
        }

        IOffenseRewardStateView state = rewards.State;
        regularCustomerProvider.TryGetRuntime(out RegularCustomerRuntime regularCustomers);
        int recruitCandidates = regularCustomers?.State.Records.Count(record =>
            record != null && record.IsRecruitCandidate && !record.IsRecruited) ?? 0;
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
        return $"회수 전리품 추정가 {state.RecoveredLootValue}"
            + $" / 영입 후보 {recruitCandidates}"
            + $" / 수용 포로 {prisoners}"
            + $" / 귀환 동물 {arrivingAnimals}";
    }
}

public sealed class ExpeditionFeatureCommandService : IExpeditionFeatureCommandService
{
    private readonly IOffenseWorldMapRuntimeProvider worldMapProvider;
    private readonly IOffenseExpeditionRuntimeProvider expeditionProvider;
    private readonly IKnowledgeResidueProcessingRuntime knowledgeProcessing;

    public ExpeditionFeatureCommandService(
        IOffenseWorldMapRuntimeProvider worldMapProvider,
        IOffenseExpeditionRuntimeProvider expeditionProvider,
        IKnowledgeResidueProcessingRuntime knowledgeProcessing)
    {
        this.worldMapProvider = worldMapProvider
            ?? throw new ArgumentNullException(nameof(worldMapProvider));
        this.expeditionProvider = expeditionProvider
            ?? throw new ArgumentNullException(nameof(expeditionProvider));
        this.knowledgeProcessing = knowledgeProcessing
            ?? throw new ArgumentNullException(nameof(knowledgeProcessing));
    }

    public ExpeditionFeatureCommandResult OpenWorldMap()
    {
        if (!worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap))
        {
            return MissingRuntime("원정 지도");
        }

        return new ExpeditionFeatureCommandResult(
            worldMap.ShowWorldMap() != null,
            "원정 지도를 열었습니다.");
    }

    public ExpeditionFeatureCommandResult OpenExpedition()
    {
        if (!expeditionProvider.TryGetRuntime(out OffenseExpeditionRuntime expeditions))
        {
            return MissingRuntime("원정 편성");
        }

        return new ExpeditionFeatureCommandResult(
            expeditions.ShowExpeditionPanel() != null,
            "원정 편성 화면을 열었습니다.");
    }

    public ExpeditionFeatureCommandResult UpgradeRecon()
    {
        if (!worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap))
        {
            return MissingRuntime("정찰");
        }

        bool succeeded = worldMap.TryUpgradeRecon(out string message);
        return new ExpeditionFeatureCommandResult(succeeded, message);
    }

    public ExpeditionFeatureCommandResult QueueSelectedRegionRecon()
    {
        if (!worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap)
            || string.IsNullOrWhiteSpace(worldMap.State.SelectedTargetId)
            || !worldMap.TryGetTargetDefinition(
                worldMap.State.SelectedTargetId,
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
        if (!worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap))
        {
            return MissingRuntime("원정 목표");
        }

        bool succeeded = worldMap.TrySelectTarget(targetId, out _, out string message);
        return new ExpeditionFeatureCommandResult(succeeded, message);
    }

    public ExpeditionFeatureCommandResult StartSelectedTarget()
    {
        if (!worldMapProvider.TryGetRuntime(out OffenseWorldMapRuntime worldMap)
            || !expeditionProvider.TryGetRuntime(out OffenseExpeditionRuntime expeditions))
        {
            return MissingRuntime("원정");
        }

        OffenseTargetSnapshot selected = worldMap.VisibleTargets.FirstOrDefault(target =>
            string.Equals(target.id, worldMap.State.SelectedTargetId, StringComparison.Ordinal));
        if (selected == null || !selected.isAvailable)
        {
            return new ExpeditionFeatureCommandResult(false, "출정 가능한 목표를 먼저 선택하세요.");
        }

        CharacterActor[] party = expeditions.GetAvailableMemberActors()
            .Take(selected.requiredMembers)
            .ToArray();
        bool succeeded = expeditions.TryStartExpedition(
            selected.id,
            party,
            out _,
            out string message);
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
            $"참가 가능한 직원 {model.AvailableMemberCount}명",
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
                $"{selected.Title} / 필요 인원 {selected.RequiredMembers}명",
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
