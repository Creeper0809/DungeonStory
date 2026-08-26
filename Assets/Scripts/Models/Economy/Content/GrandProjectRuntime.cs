using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer.Unity;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
internal sealed class GrandProjectAggregateState
{
    internal GrandProjectRuntimeState RuntimeState { get; set; } = new();
    internal int Version { get; set; }
    internal float NextEvaluationTime { get; set; }
}

public sealed class GrandProjectRestoreCandidate
{
    internal GrandProjectRestoreCandidate(GrandProjectAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal GrandProjectAggregateState State { get; }
}

public sealed class GrandProjectOfficeSnapshot
{
    public GrandProjectOfficeSnapshot(
        BuildingInstanceId instanceId,
        Vector2Int position)
    {
        InstanceId = instanceId;
        Position = position;
    }

    public BuildingInstanceId InstanceId { get; }
    public Vector2Int Position { get; }
}

public interface IGrandProjectWorldPort
{
    GrandProjectOfficeSnapshot FindOffice();
    bool IsResearchCompleted(string researchId);
    Vector2Int ResolveReleasePosition();
}

public interface IGrandProjectOperationsPort
{
    int CountPending(string itemId, string destinationId);
    int CountDelivered(string itemId, string destinationId);
    bool RequestDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested);
    bool CommitDeliveredMaterialsPending(
        string destinationId,
        IReadOnlyDictionary<string, int> costs,
        string operationId,
        string reasonCode,
        out GrandProjectPhysicalInputReceipt receipt,
        out string failureReason);
    bool TryGetPendingMaterials(
        string operationId,
        out GrandProjectPhysicalInputReceipt receipt);
    bool AcknowledgeMaterials(
        string commitId,
        out string failureReason);
    int ReleaseDestination(
        string destinationId,
        Vector2Int releasePosition);
    void PrioritizeDestination(string destinationId);
    void RequestGrandProjectWorker();
    void RequestHauler();
    void MarkDynamicStateDirty();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
    public const string PhysicalOperationPrefix = "grand-project-materials:";
    public const string PhysicalReasonCode =
        "grand-project.infrastructure-embedded";

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
            Cost("resource:clean-water", 180)),
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

    private readonly IGrandProjectWorldPort world;
    private readonly IGrandProjectOperationsPort operations;
    private readonly IGameClock gameClock;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    private GrandProjectAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new GrandProjectAggregateState());
    private GrandProjectRuntimeState state
    {
        get => aggregateState.RuntimeState;
        set
        {
            GrandProjectAggregateState current = aggregateState;
            aggregateRootStore.Replace(new GrandProjectAggregateState
            {
                RuntimeState = value ?? new GrandProjectRuntimeState(),
                Version = current.Version,
                NextEvaluationTime = current.NextEvaluationTime
            });
        }
    }
    private float nextEvaluationTime
    {
        get => aggregateState.NextEvaluationTime;
        set => aggregateState.NextEvaluationTime = value;
    }

    public GrandProjectRuntime(
        IGrandProjectWorldPort world,
        IGrandProjectOperationsPort operations,
        IGameClock gameClock,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.operations = operations
            ?? throw new ArgumentNullException(nameof(operations));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
    }

    public int Version
    {
        get => aggregateState.Version;
        private set => aggregateState.Version = value;
    }
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
        if (HasPendingPhysicalCommit())
        {
            ResumePendingPhysicalCommit(out _);
        }
        if (gameClock.IsPaused
            || gameClock.Time < nextEvaluationTime
            || string.IsNullOrWhiteSpace(state.activeProjectId))
        {
            return;
        }

        nextEvaluationTime = gameClock.Time + EvaluationInterval;
        GrandProjectDefinition definition = FindDefinition(state.activeProjectId);
        GrandProjectOfficeSnapshot office = world.FindOffice();
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
            operations.RequestGrandProjectWorker();
        }
        else
        {
            SetStatus(BuildMaterialStatus(definition));
            operations.RequestHauler();
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
            bool hasOffice = world.FindOffice() != null;
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

        GrandProjectOfficeSnapshot office = world.FindOffice();
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
        operations.RequestHauler();
        message = $"{definition.DisplayName} 사업을 시작했습니다.";
        return true;
    }

    public bool CancelActive(out string message)
    {
        if (HasPendingPhysicalCommit())
        {
            message = "대형 사업 자재 커밋이 진행 중이라 취소할 수 없습니다.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(state.activeProjectId))
        {
            message = "진행 중인 대형 사업이 없습니다.";
            return false;
        }

        string cancelledId = state.activeProjectId;
        operations.ReleaseDestination(
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
        BuildingInstanceId facilityId,
        out GrandProjectWorkSnapshot work)
    {
        work = default;
        GrandProjectDefinition definition = FindDefinition(state.activeProjectId);
        if (definition == null)
        {
            return false;
        }

        GrandProjectOfficeSnapshot office = world.FindOffice();
        if (!facilityId.IsValid
            || office == null
            || !office.InstanceId.Equals(facilityId))
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
        BuildingInstanceId facilityId,
        float amount,
        out bool completed)
    {
        completed = false;
        if (HasPendingPhysicalCommit())
        {
            return ResumePendingPhysicalCommit(out completed);
        }
        if (!TryGetWork(facilityId, out GrandProjectWorkSnapshot work)
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
        string operationId = BuildPhysicalOperationId(definition.ProjectId);
        string beforeFingerprint = CreateStateFingerprint(state);
        if (!operations.CommitDeliveredMaterialsPending(
                state.destinationId,
                costs,
                operationId,
                PhysicalReasonCode,
                out GrandProjectPhysicalInputReceipt physicalReceipt,
                out string failureReason)
            || !physicalReceipt.IsCommitted)
        {
            state.completedWork = Mathf.Max(0f, definition.RequiredWork - 0.01f);
            SetStatus($"납품 자재 확인 실패: {failureReason}");
            Touch();
            return false;
        }
        state.pendingPhysicalCommit = CreatePhysicalOwner(
            definition.ProjectId,
            beforeFingerprint,
            physicalReceipt);
        Touch();
        return ResumePendingPhysicalCommit(out completed);
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
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToList(),
                pendingPhysicalCommit = state.pendingPhysicalCommit?.Clone()
                    ?? new GrandProjectPhysicalCommitSaveData()
            }
        };
    }

    public GrandProjectRestoreCandidate BuildRestore(
        DungeonGrandProjectSaveData saveData)
    {
        if (saveData?.state == null)
        {
            throw new InvalidOperationException(
                "Grand-project restore payload or state is missing.");
        }
        GrandProjectRuntimeState source = saveData.state;
        GrandProjectRuntimeState restored = new()
        {
            activeProjectId = source.activeProjectId,
            destinationId = source.destinationId,
            completedWork = source.completedWork,
            lastStatus = source.lastStatus,
            completedProjectIds = new List<string>(source.completedProjectIds),
            pendingPhysicalCommit = source.pendingPhysicalCommit?.Clone()
                ?? new GrandProjectPhysicalCommitSaveData()
        };
        return new GrandProjectRestoreCandidate(
            new GrandProjectAggregateState
            {
                RuntimeState = restored,
                Version = aggregateState.Version + 1,
                NextEvaluationTime = gameClock.Time + EvaluationInterval
            });
    }

    public void PublishRestoreCandidate(
        GrandProjectRestoreCandidate candidate)
    {
        aggregateRootStore.Replace(
            (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .State);
    }

    private void NormalizeState()
    {
        NormalizeState(state);
    }

    private void NormalizeState(GrandProjectRuntimeState target)
    {
        target.pendingPhysicalCommit ??= new GrandProjectPhysicalCommitSaveData();
        GrandProjectDefinition active = FindDefinition(target.activeProjectId);
        if (active == null)
        {
            target.activeProjectId = string.Empty;
            target.destinationId = string.Empty;
            target.completedWork = 0f;
            return;
        }

        target.destinationId = string.IsNullOrWhiteSpace(target.destinationId)
            ? $"grand-project:{active.ProjectId}"
            : target.destinationId.Trim();
        target.completedWork = Mathf.Clamp(
            target.completedWork,
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
        GrandProjectOfficeSnapshot office)
    {
        foreach (ItemAmountDefinition requirement in definition.Requirements)
        {
            int pending = operations.CountPending(
                requirement.ItemId,
                state.destinationId);
            int missing = Mathf.Max(0, requirement.Amount - pending);
            if (missing <= 0)
            {
                continue;
            }

            operations.RequestDelivery(
                requirement.ItemId,
                missing,
                office.Position,
                state.destinationId,
                out int requested);
            if (requested > 0)
            {
                operations.RequestHauler();
            }
        }

        operations.PrioritizeDestination(state.destinationId);
    }

    private bool HasAllDelivered(GrandProjectDefinition definition)
    {
        return definition != null
            && definition.Requirements.All(requirement =>
                operations.CountDelivered(
                    requirement.ItemId,
                    state.destinationId) >= requirement.Amount);
    }

    private string BuildMaterialStatus(GrandProjectDefinition definition)
    {
        ItemAmountDefinition missing = definition?.Requirements
            .FirstOrDefault(requirement =>
                operations.CountDelivered(
                    requirement.ItemId,
                    state.destinationId) < requirement.Amount);
        if (missing == null)
        {
            return "모든 자재가 도착했습니다.";
        }

        int delivered = operations.CountDelivered(
            missing.ItemId,
            state.destinationId);
        int pending = operations.CountPending(
            missing.ItemId,
            state.destinationId);
        return $"{missing.ItemId} 납품 {delivered}/{missing.Amount} · 운반 포함 {pending}/{missing.Amount}";
    }

    private bool IsResearchCompleted(string researchId)
    {
        return string.IsNullOrWhiteSpace(researchId)
            || world.IsResearchCompleted(researchId);
    }

    private Vector2Int ResolveReleasePosition()
    {
        GrandProjectOfficeSnapshot office = world.FindOffice();
        if (office != null)
        {
            return office.Position;
        }

        return world.ResolveReleasePosition();
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
        operations.MarkDynamicStateDirty();
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

    public static string BuildPhysicalOperationId(string projectId) =>
        PhysicalOperationPrefix + (projectId ?? string.Empty);

    public static string CreateStateFingerprint(GrandProjectRuntimeState value)
    {
        if (value == null)
            return string.Empty;
        return string.Join("|", new[]
        {
            value.activeProjectId ?? string.Empty,
            value.destinationId ?? string.Empty,
            value.completedWork.ToString("R", CultureInfo.InvariantCulture),
            string.Join(",", (value.completedProjectIds ?? new List<string>())
                .OrderBy(id => id, StringComparer.Ordinal))
        });
    }

    private bool HasPendingPhysicalCommit() =>
        state.pendingPhysicalCommit != null
        && state.pendingPhysicalCommit.phase != GrandProjectPhysicalCommitPhase.None;

    private static GrandProjectPhysicalCommitSaveData CreatePhysicalOwner(
        string projectId,
        string beforeFingerprint,
        GrandProjectPhysicalInputReceipt receipt) => new()
    {
        phase = GrandProjectPhysicalCommitPhase.InputCommitted,
        projectId = projectId,
        operationId = receipt.OperationId,
        reasonCode = receipt.ReasonCode,
        requestFingerprint = receipt.RequestFingerprint,
        commitId = receipt.CommitId,
        inputQuantity = receipt.InputQuantity,
        inputMassGrams = receipt.InputMassGrams,
        sourceStackIds = receipt.SourceStackIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList(),
        stateBeforeFingerprint = beforeFingerprint,
        stateAfterFingerprint = string.Empty
    };

    private bool ResumePendingPhysicalCommit(out bool completed)
    {
        completed = false;
        GrandProjectPhysicalCommitSaveData owner = state.pendingPhysicalCommit;
        if (owner == null || owner.phase == GrandProjectPhysicalCommitPhase.None)
            return false;
        if (!operations.TryGetPendingMaterials(
                owner.operationId,
                out GrandProjectPhysicalInputReceipt receipt)
            || !Matches(owner, receipt))
            throw new InvalidOperationException(
                "Grand-project physical owner has no exact pending receipt: "
                + owner.operationId);

        GrandProjectDefinition definition = FindDefinition(owner.projectId)
            ?? throw new InvalidOperationException(
                "Grand-project physical owner references an unknown project: "
                + owner.projectId);
        if (owner.phase == GrandProjectPhysicalCommitPhase.InputCommitted)
        {
            if (!string.Equals(
                    owner.stateBeforeFingerprint,
                    CreateStateFingerprint(state),
                    StringComparison.Ordinal)
                || !string.Equals(
                    state.activeProjectId,
                    owner.projectId,
                    StringComparison.Ordinal)
                || state.completedWork + 0.001f < definition.RequiredWork)
                throw new InvalidOperationException(
                    "Grand-project physical input owner does not match its before-state envelope.");

            if (!state.completedProjectIds.Contains(owner.projectId))
                state.completedProjectIds.Add(owner.projectId);
            state.completedProjectIds = state.completedProjectIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            state.activeProjectId = string.Empty;
            state.destinationId = string.Empty;
            state.completedWork = 0f;
            state.lastStatus = $"{definition.DisplayName} 사업이 완공되었습니다.";
            owner.phase = GrandProjectPhysicalCommitPhase.OutcomePublished;
            owner.stateAfterFingerprint = CreateStateFingerprint(state);
            completed = true;
            Touch();
        }
        else
        {
            if (!string.Equals(
                    owner.stateAfterFingerprint,
                    CreateStateFingerprint(state),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Grand-project physical input owner does not match its after-state envelope.");
            completed = true;
        }

        if (!operations.AcknowledgeMaterials(
                owner.commitId,
                out _))
            return true;
        state.pendingPhysicalCommit = new GrandProjectPhysicalCommitSaveData();
        Touch();
        return true;
    }

    private static bool Matches(
        GrandProjectPhysicalCommitSaveData owner,
        GrandProjectPhysicalInputReceipt receipt) =>
        receipt.IsCommitted
        && string.Equals(owner.operationId, receipt.OperationId, StringComparison.Ordinal)
        && string.Equals(owner.reasonCode, receipt.ReasonCode, StringComparison.Ordinal)
        && string.Equals(owner.requestFingerprint, receipt.RequestFingerprint, StringComparison.Ordinal)
        && string.Equals(owner.commitId, receipt.CommitId, StringComparison.Ordinal)
        && owner.inputQuantity == receipt.InputQuantity
        && owner.inputMassGrams == receipt.InputMassGrams
        && owner.sourceStackIds.SequenceEqual(
            receipt.SourceStackIds.OrderBy(id => id, StringComparer.Ordinal),
            StringComparer.Ordinal);
}
