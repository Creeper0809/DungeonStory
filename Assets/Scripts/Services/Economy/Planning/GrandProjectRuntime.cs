using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class GrandProjectRuntime :
    IGrandProjectRuntime,
    IGrandProjectBenefitQuery,
    IInitializable,
    ITickable
{
    public const string DeepMiningNetworkId = "grand-project:deep-mining-network";
    public const string DefenseDistrictId = "grand-project:defense-district";
    public const string IndoorFarmNetworkId = "grand-project:indoor-farm-network";
    public const string AlchemyPipelineId = "grand-project:alchemy-pipeline";
    public const string RegionalTradePostId = "grand-project:regional-trade-post";
    public const string ExpeditionSupplyBaseId = "grand-project:expedition-supply-base";

    private const float EvaluationInterval = 1f;
    private const string OfficeTag = "grand-project-office";

    private static readonly GrandProjectDefinition[] BuiltInDefinitions =
    {
        Project(
            DeepMiningNetworkId,
            "심부 채굴망",
            "지하 채맥과 운반 갱도를 연결해 채석 산출을 늘립니다.",
            "research:mining:deep",
            520f,
            Cost("material:stone-block", 260),
            Cost("material:steel-ingot", 100),
            Cost("material:lumber", 140)),
        Project(
            DefenseDistrictId,
            "방어 구역 확장",
            "병참로와 방어 거점을 확장해 침공 준비 효율을 높입니다.",
            "research:defense:tactical-command",
            760f,
            Cost("material:stone-block", 420),
            Cost("material:steel-ingot", 180),
            Cost("material:treated-lumber", 140)),
        Project(
            IndoorFarmNetworkId,
            "실내 농장망",
            "관개와 영양 공급망을 묶어 실내 수확량을 높입니다.",
            "research:agriculture:subterranean",
            680f,
            Cost("material:treated-lumber", 220),
            Cost("material:iron-ingot", 120),
            Cost("material:compost", 300),
            Cost(DungeonItemCatalogSO.StockItemId(StockCategory.Water), 180)),
        Project(
            AlchemyPipelineId,
            "연금 배관망",
            "연금 시설의 용매와 마나 공급을 연결해 산출량을 높입니다.",
            "research:arcane:advanced",
            820f,
            Cost("material:iron-ingot", 160),
            Cost("material:blacksteel-ingot", 60),
            Cost("resource:mana-crystal", 180),
            Cost("material:alchemical-solvent", 120)),
        Project(
            RegionalTradePostId,
            "지역 교역소",
            "장기 공급 계약을 처리할 교역소와 검수 창고를 세웁니다.",
            "research:commerce:integration",
            900f,
            Cost("material:lumber", 320),
            Cost("material:stone-block", 320),
            Cost("material:cloth", 180),
            Cost("material:gold-ingot", 80)),
        Project(
            ExpeditionSupplyBaseId,
            "원정 보급 기지",
            "대규모 원정의 식량, 약품, 탄약을 준비할 보급 기지를 세웁니다.",
            "research:authority:office",
            1100f,
            Cost("material:steel-ingot", 260),
            Cost("food:preserved-ration", 240),
            Cost("medicine:standard", 120),
            Cost("ammo:arrow-steel", 320),
            Cost("ammo:bolt-steel", 320))
    };

    private readonly IProductionItemGateway items;
    private readonly IBuildingWorldQuery buildings;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IBlueprintResearchRuntimeProvider researchProvider;
    private readonly IGameClock gameClock;
    private readonly IWorkforceReplanService workforce;
    private readonly IFacilityCandidateCache facilityCandidates;
    private readonly GrandProjectRuntimeState state = new GrandProjectRuntimeState();
    private float nextEvaluationTime;

    public GrandProjectRuntime(
        IProductionItemGateway items,
        IBuildingWorldQuery buildings,
        IWorldDropZoneQuery dropZones,
        IGameClock gameClock,
        IBlueprintResearchRuntimeProvider researchProvider = null,
        IWorkforceReplanService workforce = null,
        IFacilityCandidateCache facilityCandidates = null)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
        this.dropZones = dropZones ?? throw new ArgumentNullException(nameof(dropZones));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.researchProvider = researchProvider;
        this.workforce = workforce;
        this.facilityCandidates = facilityCandidates;
    }

    public int Version { get; private set; }
    public IReadOnlyList<GrandProjectDefinition> Definitions => BuiltInDefinitions;
    public GrandProjectRuntimeState State => state;
    public float ContractRewardMultiplier =>
        IsCompleted(RegionalTradePostId) ? 1.25f : 1f;
    public float DefensePreparationMultiplier =>
        IsCompleted(DefenseDistrictId) ? 1.2f : 1f;
    public int ExpeditionSupplyCapacityBonus =>
        IsCompleted(ExpeditionSupplyBaseId) ? 12 : 0;

    public void Initialize()
    {
        NormalizeState();
    }

    public void Tick()
    {
        if (gameClock.IsPaused
            || gameClock.Time < nextEvaluationTime
            || string.IsNullOrWhiteSpace(state.activeProjectId))
        {
            return;
        }

        nextEvaluationTime = gameClock.Time + EvaluationInterval;
        GrandProjectDefinition definition = FindDefinition(state.activeProjectId);
        BuildableObject office = FindOffice();
        if (definition == null || office == null)
        {
            SetStatus("사업을 진행할 영주 집무실이 필요합니다.");
            return;
        }

        EnsureDestination(definition);
        RequestMissingMaterials(definition, office);
        if (HasAllDelivered(definition))
        {
            SetStatus("모든 자재가 도착했습니다. 사업 작업을 기다리는 중입니다.");
            workforce?.RequestOneWorkerToReplanFor(
                BuiltInWorkTypeIds.GrandProject,
                forceInterrupt: false);
        }
        else
        {
            SetStatus(BuildMaterialStatus(definition));
            workforce?.RequestOneHaulerToReplan(forceInterrupt: false);
        }
    }

    public GrandProjectStatus GetStatus(
        string projectId,
        out string reason)
    {
        GrandProjectDefinition definition = FindDefinition(projectId);
        if (definition == null)
        {
            reason = "알 수 없는 대형 사업입니다.";
            return GrandProjectStatus.Locked;
        }

        if (IsCompleted(definition.ProjectId))
        {
            reason = "완료됨";
            return GrandProjectStatus.Completed;
        }

        if (!IsResearchCompleted(definition.RequiredResearchId))
        {
            reason = $"연구 필요: {definition.RequiredResearchId}";
            return GrandProjectStatus.Locked;
        }

        if (!string.IsNullOrWhiteSpace(state.activeProjectId)
            && !string.Equals(
                state.activeProjectId,
                definition.ProjectId,
                StringComparison.Ordinal))
        {
            reason = "다른 대형 사업이 진행 중입니다.";
            return GrandProjectStatus.Locked;
        }

        if (!string.Equals(
                state.activeProjectId,
                definition.ProjectId,
                StringComparison.Ordinal))
        {
            bool hasOffice = FindOffice() != null;
            reason = hasOffice ? "시작 가능" : "영주 집무실 필요";
            return hasOffice
                ? GrandProjectStatus.Available
                : GrandProjectStatus.Locked;
        }

        reason = state.lastStatus;
        return HasAllDelivered(definition)
            ? GrandProjectStatus.InProgress
            : GrandProjectStatus.WaitingForMaterials;
    }

    public bool Start(string projectId, out string message)
    {
        GrandProjectDefinition definition = FindDefinition(projectId);
        GrandProjectStatus status = GetStatus(projectId, out string reason);
        if (definition == null || status != GrandProjectStatus.Available)
        {
            message = reason;
            return false;
        }

        BuildableObject office = FindOffice();
        if (office == null)
        {
            message = "대형 사업을 진행할 영주 집무실이 필요합니다.";
            return false;
        }

        state.activeProjectId = definition.ProjectId;
        state.completedWork = 0f;
        state.destinationId = $"grand-project:{definition.ProjectId}";
        RequestMissingMaterials(definition, office);
        SetStatus("사업 자재를 집무실로 운반하는 중입니다.");
        Touch();
        workforce?.RequestOneHaulerToReplan(forceInterrupt: false);
        message = $"{definition.DisplayName} 사업을 시작했습니다.";
        return true;
    }

    public bool CancelActive(out string message)
    {
        if (string.IsNullOrWhiteSpace(state.activeProjectId))
        {
            message = "진행 중인 대형 사업이 없습니다.";
            return false;
        }

        string cancelledId = state.activeProjectId;
        items.ReleaseDestination(
            state.destinationId,
            ResolveReleasePosition());
        state.activeProjectId = string.Empty;
        state.destinationId = string.Empty;
        state.completedWork = 0f;
        state.lastStatus = "사업이 취소되어 자재 예약을 해제했습니다.";
        Touch();
        message = $"{FindDefinition(cancelledId)?.DisplayName ?? cancelledId} 사업을 취소했습니다.";
        return true;
    }

    public bool TryGetWork(
        BuildableObject facility,
        CharacterActor worker,
        out GrandProjectWorkSnapshot work)
    {
        work = default;
        GrandProjectDefinition definition = FindDefinition(state.activeProjectId);
        if (definition == null)
        {
            return false;
        }

        if (!IsOffice(facility) || !ReferenceEquals(facility, FindOffice()))
        {
            work = new GrandProjectWorkSnapshot(
                false,
                definition.ProjectId,
                definition.DisplayName,
                definition.RequiredWork,
                state.completedWork,
                "다른 집무실이 이 사업을 담당하고 있습니다.");
            return false;
        }

        bool materialsReady = HasAllDelivered(definition);
        work = new GrandProjectWorkSnapshot(
            materialsReady,
            definition.ProjectId,
            definition.DisplayName,
            definition.RequiredWork,
            state.completedWork,
            materialsReady ? string.Empty : BuildMaterialStatus(definition));
        return materialsReady;
    }

    public bool ApplyWork(
        BuildableObject facility,
        CharacterActor worker,
        float amount,
        out bool completed)
    {
        completed = false;
        if (!TryGetWork(facility, worker, out GrandProjectWorkSnapshot work)
            || !work.Available
            || amount <= 0f)
        {
            return false;
        }

        GrandProjectDefinition definition = FindDefinition(work.ProjectId);
        state.completedWork = Mathf.Min(
            definition.RequiredWork,
            state.completedWork + amount);
        if (state.completedWork + 0.001f < definition.RequiredWork)
        {
            SetStatus(
                $"{definition.DisplayName} 작업 {Mathf.FloorToInt(state.completedWork / definition.RequiredWork * 100f)}%");
            Touch();
            return true;
        }

        Dictionary<string, int> costs = definition.Requirements
            .GroupBy(requirement => requirement.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(requirement => requirement.Amount),
                StringComparer.Ordinal);
        if (!items.ConsumeDelivered(
                state.destinationId,
                costs,
                out string failureReason))
        {
            state.completedWork = Mathf.Max(0f, definition.RequiredWork - 0.01f);
            SetStatus($"납품 자재 확인 실패: {failureReason}");
            Touch();
            return false;
        }

        if (!state.completedProjectIds.Contains(definition.ProjectId))
        {
            state.completedProjectIds.Add(definition.ProjectId);
        }

        state.activeProjectId = string.Empty;
        state.destinationId = string.Empty;
        state.completedWork = 0f;
        state.lastStatus = $"{definition.DisplayName} 사업이 완공되었습니다.";
        completed = true;
        Touch();
        return true;
    }

    public bool IsCompleted(string projectId)
    {
        return !string.IsNullOrWhiteSpace(projectId)
            && state.completedProjectIds.Contains(projectId);
    }

    public float GetProductionOutputMultiplier(string facilityTag)
    {
        string tag = facilityTag?.Trim() ?? string.Empty;
        float multiplier = 1f;
        if (IsCompleted(DeepMiningNetworkId)
            && string.Equals(tag, "quarry", StringComparison.Ordinal))
        {
            multiplier *= 1.25f;
        }

        if (IsCompleted(IndoorFarmNetworkId)
            && string.Equals(tag, "crop-indoor", StringComparison.Ordinal))
        {
            multiplier *= 1.2f;
        }

        if (IsCompleted(AlchemyPipelineId)
            && tag is "alchemy" or "apothecary" or "distillery")
        {
            multiplier *= 1.15f;
        }

        return multiplier;
    }

    public DungeonGrandProjectSaveData Capture()
    {
        return new DungeonGrandProjectSaveData
        {
            state = new GrandProjectRuntimeState
            {
                activeProjectId = state.activeProjectId,
                destinationId = state.destinationId,
                completedWork = state.completedWork,
                lastStatus = state.lastStatus,
                completedProjectIds = state.completedProjectIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
            }
        };
    }

    public void Restore(DungeonGrandProjectSaveData saveData)
    {
        GrandProjectRuntimeState restored =
            saveData?.state ?? new GrandProjectRuntimeState();
        state.activeProjectId = FindDefinition(restored.activeProjectId) != null
            ? restored.activeProjectId
            : string.Empty;
        state.destinationId = state.activeProjectId.Length > 0
            ? restored.destinationId?.Trim() ?? string.Empty
            : string.Empty;
        state.completedWork = Mathf.Max(0f, restored.completedWork);
        state.lastStatus = restored.lastStatus ?? string.Empty;
        state.completedProjectIds = (restored.completedProjectIds
                ?? new List<string>())
            .Where(id => FindDefinition(id) != null)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        NormalizeState();
        Touch();
    }

    private void NormalizeState()
    {
        GrandProjectDefinition active = FindDefinition(state.activeProjectId);
        if (active == null)
        {
            state.activeProjectId = string.Empty;
            state.destinationId = string.Empty;
            state.completedWork = 0f;
            return;
        }

        state.destinationId = string.IsNullOrWhiteSpace(state.destinationId)
            ? $"grand-project:{active.ProjectId}"
            : state.destinationId.Trim();
        state.completedWork = Mathf.Clamp(
            state.completedWork,
            0f,
            active.RequiredWork);
    }

    private void EnsureDestination(GrandProjectDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(state.destinationId))
        {
            state.destinationId = $"grand-project:{definition.ProjectId}";
        }
    }

    private void RequestMissingMaterials(
        GrandProjectDefinition definition,
        BuildableObject office)
    {
        foreach (ItemAmountDefinition requirement in definition.Requirements)
        {
            int pending = items.CountPending(
                requirement.ItemId,
                state.destinationId);
            int missing = Mathf.Max(0, requirement.Amount - pending);
            if (missing <= 0)
            {
                continue;
            }

            items.RequestDelivery(
                requirement.ItemId,
                missing,
                office.centerPos,
                state.destinationId,
                out int requested,
                out _);
            if (requested > 0)
            {
                workforce?.RequestOneHaulerToReplan(forceInterrupt: false);
            }
        }

        items.PrioritizeDestination(state.destinationId);
    }

    private bool HasAllDelivered(GrandProjectDefinition definition)
    {
        return definition != null
            && definition.Requirements.All(requirement =>
                items.CountDelivered(
                    requirement.ItemId,
                    state.destinationId) >= requirement.Amount);
    }

    private string BuildMaterialStatus(GrandProjectDefinition definition)
    {
        ItemAmountDefinition missing = definition?.Requirements
            .FirstOrDefault(requirement =>
                items.CountDelivered(
                    requirement.ItemId,
                    state.destinationId) < requirement.Amount);
        if (missing == null)
        {
            return "모든 자재가 도착했습니다.";
        }

        int delivered = items.CountDelivered(
            missing.ItemId,
            state.destinationId);
        int pending = items.CountPending(
            missing.ItemId,
            state.destinationId);
        return $"{missing.ItemId} 납품 {delivered}/{missing.Amount} · 운반 포함 {pending}/{missing.Amount}";
    }

    private BuildableObject FindOffice()
    {
        return buildings.Buildings
            .Where(IsOffice)
            .OrderBy(building => building.centerPos.y)
            .ThenBy(building => building.centerPos.x)
            .FirstOrDefault();
    }

    private static bool IsOffice(BuildableObject building)
    {
        return building != null
            && !building.isDestroy
            && building.SupportsWork(BuiltInWorkTypeIds.GrandProject)
            && building.HasSemanticTag(OfficeTag);
    }

    private bool IsResearchCompleted(string researchId)
    {
        return string.IsNullOrWhiteSpace(researchId)
            || (researchProvider != null
                && researchProvider.TryGetRuntime(
                    out BlueprintResearchRuntime runtime)
                && runtime.State.Projects.IsCompleted(
                    new ResearchProjectId(researchId)));
    }

    private Vector2Int ResolveReleasePosition()
    {
        BuildableObject office = FindOffice();
        if (office != null)
        {
            return office.centerPos;
        }

        return dropZones.TryGetDeliveryDropoff(out Vector2Int dropoff)
            ? dropoff
            : Vector2Int.zero;
    }

    private static GrandProjectDefinition FindDefinition(string projectId)
    {
        return BuiltInDefinitions.FirstOrDefault(definition =>
            string.Equals(
                definition.ProjectId,
                projectId?.Trim(),
                StringComparison.Ordinal));
    }

    private void SetStatus(string status)
    {
        string normalized = status ?? string.Empty;
        if (string.Equals(state.lastStatus, normalized, StringComparison.Ordinal))
        {
            return;
        }

        state.lastStatus = normalized;
        Touch();
    }

    private void Touch()
    {
        Version++;
        facilityCandidates?.MarkDynamicStateDirty();
    }

    private static GrandProjectDefinition Project(
        string id,
        string name,
        string description,
        string research,
        float work,
        params ItemAmountDefinition[] requirements)
    {
        return new GrandProjectDefinition(
            id,
            name,
            description,
            research,
            work,
            requirements);
    }

    private static ItemAmountDefinition Cost(string itemId, int amount)
    {
        return new ItemAmountDefinition(itemId, amount);
    }
}
