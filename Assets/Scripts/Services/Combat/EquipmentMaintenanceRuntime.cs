using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

public enum CombatEquipmentRepairOrderState
{
    PendingCombatEnd = 0,
    WaitingForDelivery = 1,
    Ready = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5
}

[Serializable]
public sealed class EquipmentMaintenancePolicyData
{
    public string id = string.Empty;
    public string displayName = string.Empty;
    public bool automaticRepair = true;
    [Range(0f, 1f)] public float sendAtDurability = 0.35f;
    [Range(0f, 1f)] public float returnAtDurability = 0.9f;
    public bool allowUnequipDuringInvasion;
    public bool preferReplacement = true;

    public EquipmentMaintenancePolicyData Clone()
    {
        return (EquipmentMaintenancePolicyData)MemberwiseClone();
    }

    public void Normalize()
    {
        id = id?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        sendAtDurability = Mathf.Clamp01(sendAtDurability);
        returnAtDurability = Mathf.Clamp(returnAtDurability, sendAtDurability, 1f);
    }
}

[Serializable]
public sealed class EquipmentRepairMaterialTransferInput
{
    public string itemId = string.Empty;
    public string sourceStackId = string.Empty;
    public int quantity;

    public EquipmentRepairMaterialTransferInput Clone() => new()
    {
        itemId = itemId ?? string.Empty,
        sourceStackId = sourceStackId ?? string.Empty,
        quantity = Mathf.Max(0, quantity)
    };
}

public enum CombatEquipmentRepairTerminalEffectPhase
{
    WipPreparedAwaitingOwnerDispositionAcknowledgement = 0,
    OwnerDispositionAcknowledgedAwaitingDestinationClose = 1,
    DestinationClosedAwaitingSourceRemoval = 2,
    SourceRemoved = 3
}

[Serializable]
public sealed class CombatEquipmentRepairTerminalEffectSaveData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string ownerStableId = string.Empty;
    public string sourceId = string.Empty;
    public string facilityId = string.Empty;
    public string frozenSourcePayload = string.Empty;
    public string sourceFingerprint = string.Empty;
    public string inputDispositionStepOperationId = string.Empty;
    public string inputDispositionRequestFingerprint = string.Empty;
    public string inputDispositionCommitId = string.Empty;
    public string inputDispositionReceiptFingerprint = string.Empty;
    public int releasedInputQuantity;
    public long releasedInputMassGrams;
    public string wipLossCommitId = string.Empty;
    public string wipLossReceiptFingerprint = string.Empty;
    public int wipInputQuantity;
    public long wipInputMassGrams;
    public long committedOutputMassGrams;
    public long declaredLossMassGrams;
    public int terminalReason;
    public int lossKind;
    public string sourceRemovalCommitId = string.Empty;
    public string sourceRemovalReceiptFingerprint = string.Empty;
    public CombatEquipmentRepairTerminalEffectPhase phase;

    public CombatEquipmentRepairTerminalEffectSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        ownerStableId = ownerStableId ?? string.Empty,
        sourceId = sourceId ?? string.Empty,
        facilityId = facilityId ?? string.Empty,
        frozenSourcePayload = frozenSourcePayload ?? string.Empty,
        sourceFingerprint = sourceFingerprint ?? string.Empty,
        inputDispositionStepOperationId =
            inputDispositionStepOperationId ?? string.Empty,
        inputDispositionRequestFingerprint =
            inputDispositionRequestFingerprint ?? string.Empty,
        inputDispositionCommitId = inputDispositionCommitId ?? string.Empty,
        inputDispositionReceiptFingerprint =
            inputDispositionReceiptFingerprint ?? string.Empty,
        releasedInputQuantity = releasedInputQuantity,
        releasedInputMassGrams = releasedInputMassGrams,
        wipLossCommitId = wipLossCommitId ?? string.Empty,
        wipLossReceiptFingerprint = wipLossReceiptFingerprint ?? string.Empty,
        wipInputQuantity = wipInputQuantity,
        wipInputMassGrams = wipInputMassGrams,
        committedOutputMassGrams = committedOutputMassGrams,
        declaredLossMassGrams = declaredLossMassGrams,
        terminalReason = terminalReason,
        lossKind = lossKind,
        sourceRemovalCommitId = sourceRemovalCommitId ?? string.Empty,
        sourceRemovalReceiptFingerprint =
            sourceRemovalReceiptFingerprint ?? string.Empty,
        phase = phase
    };
}

[Serializable]
public sealed class CombatEquipmentRepairOrder
{
    public string orderId = string.Empty;
    public string equipmentInstanceId = string.Empty;
    public string originalOwnerCharacterId = string.Empty;
    public string facilityBuildingId = string.Empty;
    public string materialItemId = string.Empty;
    public int requiredMaterialAmount;
    public float requiredWork;
    public float completedWork;
    public float targetDurability = 0.9f;
    public string reservedWorkerId = string.Empty;
    public CombatEquipmentRepairOrderState state;
    public bool manuallyRequested;
    public bool equipmentDeliveryRequested;
    public bool materialDeliveryRequested;
    public bool materialsConsumed;
    public string materialTransferOperationId = string.Empty;
    public string materialTransferCommitId = string.Empty;
    public string materialTransferRequestFingerprint = string.Empty;
    public long materialTransferMassGrams;
    public List<EquipmentRepairMaterialTransferInput> materialTransferInputs =
        new List<EquipmentRepairMaterialTransferInput>();
    public string repairEquipmentSourceStackId = string.Empty;
    public float repairDurabilityBefore;
    public float repairDurabilityAfter;
    public bool repairOutcomePublished;
    public bool materialTransferAcknowledged;
    public bool repairOutputReleased;

    public string FacilityDestinationId =>
        $"equipment-repair:{equipmentInstanceId}";

    public float ProgressRatio => requiredWork <= 0f
        ? 1f
        : Mathf.Clamp01(completedWork / requiredWork);

    public CombatEquipmentRepairOrder Clone()
    {
        CombatEquipmentRepairOrder clone =
            (CombatEquipmentRepairOrder)MemberwiseClone();
        clone.materialTransferInputs = materialTransferInputs?
            .Where(input => input != null)
            .Select(input => input.Clone())
            .ToList() ?? new List<EquipmentRepairMaterialTransferInput>();
        return clone;
    }
}

[Serializable]
public sealed class EquipmentMaintenanceAssignmentSaveData
{
    public string characterId = string.Empty;
    public string policyId = string.Empty;
}

[Serializable]
public sealed class CombatEquipmentMaintenanceSaveData
{
    public List<EquipmentMaintenancePolicyData> policies =
        new List<EquipmentMaintenancePolicyData>();
    public List<EquipmentMaintenanceAssignmentSaveData> assignments =
        new List<EquipmentMaintenanceAssignmentSaveData>();
    public List<CombatEquipmentRepairOrder> orders =
        new List<CombatEquipmentRepairOrder>();
    public List<CombatEquipmentRepairTerminalEffectSaveData>
        repairTerminalEffects =
            new List<CombatEquipmentRepairTerminalEffectSaveData>();
    public int policySequence;
    public int orderSequence;
}

public interface ICombatEquipmentMaintenanceRuntime
{
    IReadOnlyList<EquipmentMaintenancePolicyData> Policies { get; }
    IReadOnlyList<CombatEquipmentRepairOrder> Orders { get; }
    EquipmentMaintenancePolicyData GetPolicy(CharacterActor actor);
    string GetAssignedPolicyId(CharacterActor actor);
    bool AssignPolicy(CharacterActor actor, string policyId);
    bool TryCreatePolicy(string displayName, out EquipmentMaintenancePolicyData policy);
    bool TryDuplicatePolicy(
        string sourcePolicyId,
        string displayName,
        out EquipmentMaintenancePolicyData policy);
    bool TryUpdatePolicy(EquipmentMaintenancePolicyData policy);
    bool TryDeletePolicy(string policyId, bool reassignToStandard);
    bool TryRequestManualRepair(string equipmentInstanceId, out string message);
    bool HasRepairWorkFor(BuildableObject building);
    float GetRepairUrgency(BuildableObject building);
    bool TryApplyRepairWork(
        CharacterActor worker,
        BuildableObject building,
        float workAmount,
        out bool completed,
        out string message);
    CombatEquipmentMaintenanceSaveData Capture();
    EquipmentMaintenanceRestoreCandidate PrepareRestore(
        CombatEquipmentMaintenanceSaveData saveData);
    void PublishRestore(EquipmentMaintenanceRestoreCandidate candidate);
}

public interface ICombatEquipmentMaintenanceOrderQuery
{
    IReadOnlyList<CombatEquipmentRepairOrder> Orders { get; }
}

public interface ICombatEquipmentRepairTerminalEffectQuery
{
    IReadOnlyList<CombatEquipmentRepairTerminalEffectSaveData> TerminalEffects
    {
        get;
    }
}

public static class CombatEquipmentMaintenanceFacilityUtility
{
    public static bool IsMaintenanceFacility(BuildableObject building)
    {
        return building?.BuildingData?.Facility != null
            && building.BuildingData
                .GetAbility<BuildingEquipmentMaintenanceAbility>() != null;
    }

    internal static FacilityWorkType AddFallbackWorkTypes(
        BuildableObject building,
        FacilityWorkType supported)
    {
        return IsMaintenanceFacility(building)
            ? supported | FacilityWorkType.Repair
            : supported;
    }
}

public sealed class EquipmentMaintenancePolicyRuntime :
    ICombatEquipmentMaintenanceRuntime,
    ICombatEquipmentMaintenanceOrderQuery,
    ICombatEquipmentRepairTerminalEffectQuery,
    ITickable
{
    private const string RepairDestinationOwnerDomain =
        "combat.equipment-maintenance";
    private const long RepairBufferCapacitySchemaRevision = 1L;
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("EquipmentMaintenancePolicyRuntime.Tick");

    public const string StandardPolicyId = EquipmentMaintenancePolicyIds.Standard;
    public const string PreventivePolicyId = EquipmentMaintenancePolicyIds.Preventive;
    public const string ManualPolicyId = EquipmentMaintenancePolicyIds.Manual;

    private readonly ICombatEquipmentRuntime equipment;
    private readonly ICombatEquipmentCatalog catalog;
    private readonly IResourceEconomyContentCatalog resourceCatalog;
    private readonly IWorldItemStackRuntime items;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly ICombatEquipmentPickupRuntime equipmentPickup;
    private readonly IFacilityBufferDestinationClaimQuery destinationClaims;
    private readonly IFacilityBufferDestinationLifecycleCommand destinationLifecycle;
    private readonly IFacilityBufferMassCapacityAuthorityQuery destinationCapacities;
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IDefenseEngagementStore defenseEngagements;
    private readonly IGameClock gameClock;
    private readonly IUiClock uiClock;
    private readonly EquipmentMaintenanceItemServices itemServices;
    private readonly EquipmentMaintenanceWorldServices worldServices;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private float nextScanAt;
    private EquipmentMaintenanceAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(EquipmentMaintenanceAggregateState.CreateDefault);
    private EquipmentMaintenanceAggregateState writableAggregateState =>
        aggregateRootStore.GetOrCreateWritable(
            EquipmentMaintenanceAggregateState.CreateDefault,
            state => state.Clone());
    private Dictionary<string, EquipmentMaintenancePolicyData> policies =>
        writableAggregateState.Policies;
    private Dictionary<string, string> assignments =>
        writableAggregateState.Assignments;
    private Dictionary<string, CombatEquipmentRepairOrder> orders =>
        writableAggregateState.Orders;
    private int policySequence
    {
        get => aggregateState.PolicySequence;
        set => writableAggregateState.PolicySequence = value;
    }
    private int orderSequence
    {
        get => aggregateState.OrderSequence;
        set => writableAggregateState.OrderSequence = value;
    }

    public EquipmentMaintenancePolicyRuntime(
        EquipmentMaintenanceItemServices itemServices,
        EquipmentMaintenanceWorldServices worldServices,
        EquipmentMaintenanceClockServices clocks,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.itemServices = itemServices
            ?? throw new ArgumentNullException(nameof(itemServices));
        this.worldServices = worldServices
            ?? throw new ArgumentNullException(nameof(worldServices));
        clocks = clocks ?? throw new ArgumentNullException(nameof(clocks));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        equipment = itemServices.Equipment;
        catalog = itemServices.EquipmentCatalog;
        resourceCatalog = itemServices.ResourceCatalog;
        items = itemServices.Items;
        batchDispositions = itemServices.BatchDispositions;
        equipmentPickup = itemServices.EquipmentPickup;
        destinationClaims = itemServices.DestinationClaims;
        destinationLifecycle = itemServices.DestinationLifecycle;
        destinationCapacities = itemServices.DestinationCapacities;
        worldRegistry = worldServices.WorldRegistry;
        defenseEngagements = worldServices.DefenseEngagements;
        gameClock = clocks.GameClock;
        uiClock = clocks.UiClock;
    }

    public IReadOnlyList<EquipmentMaintenancePolicyData> Policies =>
        policies.Values
            .OrderBy(item => item.displayName, StringComparer.Ordinal)
            .Select(item => item.Clone())
            .ToArray();
    public IReadOnlyList<CombatEquipmentRepairOrder> Orders =>
        orders.Values
            .Where(item => item.state is not CombatEquipmentRepairOrderState.Completed
                and not CombatEquipmentRepairOrderState.Cancelled)
            .OrderBy(item => item.orderId, StringComparer.Ordinal)
            .Select(item => item.Clone())
            .ToArray();

    public IReadOnlyList<CombatEquipmentRepairTerminalEffectSaveData>
        TerminalEffects => aggregateState.TerminalEffects.Values
            .OrderBy(item => item.sourceId, StringComparer.Ordinal)
            .Select(item => item.Clone())
            .ToArray();

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        float cadenceTime = uiClock.Time;
        if (gameClock.IsPaused || cadenceTime < nextScanAt)
        {
            return;
        }

        nextScanAt = cadenceTime + 1f;
        RefreshOrders();
        CreateAutomaticOrders();
    }

    public EquipmentMaintenancePolicyData GetPolicy(CharacterActor actor)
    {
        string policyId = GetAssignedPolicyId(actor);
        return policies.TryGetValue(policyId, out EquipmentMaintenancePolicyData policy)
            ? policy.Clone()
            : policies[StandardPolicyId].Clone();
    }

    public string GetAssignedPolicyId(CharacterActor actor)
    {
        string characterId = GetCharacterId(actor);
        return !string.IsNullOrWhiteSpace(characterId)
            && assignments.TryGetValue(characterId, out string policyId)
            && policies.ContainsKey(policyId)
                ? policyId
                : StandardPolicyId;
    }

    public bool AssignPolicy(CharacterActor actor, string policyId)
    {
        string characterId = GetCharacterId(actor);
        if (string.IsNullOrWhiteSpace(characterId) || !policies.ContainsKey(policyId ?? string.Empty))
        {
            return false;
        }

        assignments[characterId] = policyId;
        return true;
    }

    public bool TryCreatePolicy(
        string displayName,
        out EquipmentMaintenancePolicyData policy)
    {
        if (policySequence == int.MaxValue)
        {
            policy = null;
            return false;
        }

        int nextSequence = policySequence + 1;
        policySequence = nextSequence;
        policy = new EquipmentMaintenancePolicyData
        {
            id = $"equipment-maintenance:custom:{nextSequence}",
            displayName = string.IsNullOrWhiteSpace(displayName)
                ? $"장비 정책 {policySequence}"
                : displayName.Trim(),
            automaticRepair = true,
            sendAtDurability = 0.35f,
            returnAtDurability = 0.9f,
            preferReplacement = true
        };
        policies[policy.id] = policy;
        policy = policy.Clone();
        return true;
    }

    public bool TryDuplicatePolicy(
        string sourcePolicyId,
        string displayName,
        out EquipmentMaintenancePolicyData policy)
    {
        policy = null;
        if (!policies.TryGetValue(
                sourcePolicyId?.Trim() ?? string.Empty,
                out EquipmentMaintenancePolicyData source))
        {
            return false;
        }
        if (policySequence == int.MaxValue)
        {
            return false;
        }

        int nextSequence = policySequence + 1;
        policySequence = nextSequence;
        policy = source.Clone();
        policy.id = $"equipment-maintenance:custom:{nextSequence}";
        policy.displayName = string.IsNullOrWhiteSpace(displayName)
            ? $"{source.displayName} 복사본"
            : displayName.Trim();
        policies[policy.id] = policy;
        policy = policy.Clone();
        return true;
    }

    public bool TryUpdatePolicy(EquipmentMaintenancePolicyData source)
    {
        if (source == null
            || string.IsNullOrWhiteSpace(source.id)
            || !policies.ContainsKey(source.id))
        {
            return false;
        }

        EquipmentMaintenancePolicyData normalized = source.Clone();
        normalized.Normalize();
        if (string.IsNullOrWhiteSpace(normalized.displayName))
        {
            return false;
        }

        policies[normalized.id] = normalized;
        return true;
    }

    public bool TryDeletePolicy(string policyId, bool reassignToStandard)
    {
        if (string.IsNullOrWhiteSpace(policyId)
            || policyId is StandardPolicyId or PreventivePolicyId or ManualPolicyId
            || !policies.ContainsKey(policyId))
        {
            return false;
        }

        string[] affected = assignments
            .Where(pair => string.Equals(pair.Value, policyId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray();
        if (affected.Length > 0 && !reassignToStandard)
        {
            return false;
        }

        foreach (string characterId in affected)
        {
            assignments[characterId] = StandardPolicyId;
        }

        return policies.Remove(policyId);
    }

    public bool TryRequestManualRepair(string equipmentInstanceId, out string message)
    {
        return TryCreateOrder(equipmentInstanceId, manuallyRequested: true, out message);
    }

    public bool HasRepairWorkFor(BuildableObject building)
    {
        if (!CombatEquipmentMaintenanceFacilityUtility.IsMaintenanceFacility(building))
        {
            return false;
        }

        RefreshOrders();
        return orders.Values.Any(order =>
            order.state is CombatEquipmentRepairOrderState.Ready
                or CombatEquipmentRepairOrderState.InProgress
            && IsOrderForBuilding(order, building));
    }

    public float GetRepairUrgency(BuildableObject building)
    {
        if (building == null)
        {
            return 0f;
        }

        return orders.Values
            .Where(order => IsOrderForBuilding(order, building)
                && order.state is CombatEquipmentRepairOrderState.Ready
                    or CombatEquipmentRepairOrderState.InProgress)
            .Select(order => equipment.TryGetInstance(
                    order.equipmentInstanceId,
                    out CombatEquipmentInstance instance)
                ? Mathf.Lerp(35f, 95f, 1f - instance.durabilityRatio)
                : 0f)
            .DefaultIfEmpty(0f)
            .Max();
    }

    public bool TryApplyRepairWork(
        CharacterActor worker,
        BuildableObject building,
        float workAmount,
        out bool completed,
        out string message)
    {
        completed = false;
        message = string.Empty;
        CombatEquipmentRepairOrder order = orders.Values
            .Where(candidate => IsOrderForBuilding(candidate, building)
                && candidate.state is CombatEquipmentRepairOrderState.Ready
                    or CombatEquipmentRepairOrderState.InProgress)
            .OrderBy(candidate => candidate.orderId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (order == null)
        {
            message = "수리 대기 장비가 없습니다.";
            return false;
        }

        order.state = CombatEquipmentRepairOrderState.InProgress;
        order.reservedWorkerId = GetCharacterId(worker);
        float multiplier = building.BuildingData
            .GetAbility<BuildingEquipmentMaintenanceAbility>()?.workSpeedMultiplier ?? 1f;
        order.completedWork = Mathf.Min(
            order.requiredWork,
            order.completedWork + Mathf.Max(0f, workAmount) * Mathf.Max(0.1f, multiplier));
        if (order.completedWork + 0.001f < order.requiredWork)
        {
            message = $"장비 수리 {Mathf.RoundToInt(order.ProgressRatio * 100f)}%";
            return true;
        }

        completed = CompleteOrder(order, building, out message);
        return completed;
    }

    public CombatEquipmentMaintenanceSaveData Capture()
    {
        return new CombatEquipmentMaintenanceSaveData
        {
            policySequence = aggregateState.PolicySequence,
            orderSequence = aggregateState.OrderSequence,
            policies = aggregateState.Policies.Values
                .OrderBy(item => item.id, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToList(),
            assignments = aggregateState.Assignments
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair =>
                new EquipmentMaintenanceAssignmentSaveData
                {
                    characterId = pair.Key,
                    policyId = pair.Value
                }).ToList(),
            orders = aggregateState.Orders.Values
                .Where(item => item.state is not CombatEquipmentRepairOrderState.Completed
                    and not CombatEquipmentRepairOrderState.Cancelled)
                .OrderBy(item => item.orderId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToList(),
            repairTerminalEffects = aggregateState.TerminalEffects.Values
                .OrderBy(item => item.sourceId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToList()
        };
    }

    public EquipmentMaintenanceRestoreCandidate PrepareRestore(
        CombatEquipmentMaintenanceSaveData saveData)
    {
        DungeonGameRestoreReport report = new();
        EquipmentMaintenanceSaveValidation.Validate(
            saveData,
            report,
            itemServices,
            worldServices);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Equipment-maintenance restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        return new EquipmentMaintenanceRestoreCandidate(
            EquipmentMaintenanceSaveValidation.CreateState(saveData));
    }

    public void PublishRestore(EquipmentMaintenanceRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        if (!TryPublishRepairBufferAuthorities(
                candidate.State.Orders.Values,
                candidate.State.TerminalEffects,
                out string authorityFailure))
        {
            throw new InvalidOperationException(
                "Equipment repair destination restore failed: "
                + authorityFailure);
        }

        aggregateRootStore.Replace(candidate.State);
    }

    internal EquipmentMaintenanceAggregateState CaptureTerminalState() =>
        aggregateState.Clone();

    internal bool TryPublishTerminalState(
        EquipmentMaintenanceAggregateState candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (candidate == null)
        {
            failureReason = "equipment-repair-terminal-state-missing";
            return false;
        }
        if (!TryPublishRepairBufferAuthorities(
                candidate.Orders.Values,
                candidate.TerminalEffects,
                out failureReason))
        {
            return false;
        }
        aggregateRootStore.Replace(candidate);
        return true;
    }

    internal bool TryAcknowledgeTerminalMaterial(
        CombatEquipmentRepairOrder frozenOrder,
        out string failureReason) =>
        EquipmentRepairMaterialOutbox.TryAcknowledgeTerminalLoss(
            frozenOrder,
            batchDispositions,
            out failureReason);

    private void CreateAutomaticOrders()
    {
        foreach (CombatEquipmentInstance instance in equipment.Instances)
        {
            if (instance == null
                || orders.Values.Any(order =>
                    string.Equals(
                        order.equipmentInstanceId,
                        instance.instanceId,
                        StringComparison.Ordinal))
                || !catalog.TryGet(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition)
                || definition.Kind is not CombatEquipmentKind.Armor
                    and not CombatEquipmentKind.Shield)
            {
                continue;
            }

            EquipmentMaintenancePolicyData policy = GetPolicyByCharacterId(
                instance.ownerCharacterId);
            if (!policy.automaticRepair
                || instance.durabilityRatio > policy.sendAtDurability)
            {
                continue;
            }

            TryCreateOrder(instance.instanceId, manuallyRequested: false, out _);
        }
    }

    private bool TryCreateOrder(
        string instanceId,
        bool manuallyRequested,
        out string message)
    {
        message = string.Empty;
        if (!equipment.TryGetInstance(instanceId, out CombatEquipmentInstance instance)
            || !catalog.TryGet(instance.definitionId, out CombatEquipmentDefinitionSO definition)
            || definition.Kind is not CombatEquipmentKind.Armor
                and not CombatEquipmentKind.Shield)
        {
            message = "수리 가능한 방어구나 방패가 아닙니다.";
            return false;
        }

        if (orders.Values.Any(order =>
            string.Equals(order.equipmentInstanceId, instanceId, StringComparison.Ordinal)))
        {
            message = "이미 수리 대기 중인 장비입니다.";
            return false;
        }

        BuildableObject facility = FindMaintenanceFacility();
        if (facility == null)
        {
            message = "장비를 수리할 대장작업대가 없습니다.";
            return false;
        }

        EquipmentMaintenancePolicyData policy = GetPolicyByCharacterId(
            instance.ownerCharacterId);
        float lost = 1f - instance.durabilityRatio;
        BuildingEquipmentMaintenanceAbility maintenance = facility.BuildingData
            .GetAbility<BuildingEquipmentMaintenanceAbility>();
        string materialItemId = maintenance?.RepairSupplyItemId;
        if (string.IsNullOrWhiteSpace(materialItemId)
            && !TryResolveRepairMaterial(
                instance,
                definition,
                out materialItemId))
        {
            message = "장비의 원래 재질을 확인할 수 없습니다.";
            return false;
        }

        int requiredMaterialAmount =
            Mathf.Max(1, Mathf.CeilToInt(lost / 0.25f))
            * (maintenance?.RepairSupplyPerQuarterDurability ?? 1);
        if (orderSequence == int.MaxValue)
        {
            message = "Equipment repair order sequence is exhausted.";
            return false;
        }

        int nextSequence = orderSequence + 1;
        CombatEquipmentRepairOrder order = new CombatEquipmentRepairOrder
        {
            orderId = $"equipment-repair:{nextSequence:D6}",
            equipmentInstanceId = instance.instanceId,
            originalOwnerCharacterId = instance.ownerCharacterId,
            facilityBuildingId = facility.RequirePersistentInstanceId().Value,
            materialItemId = materialItemId,
            requiredMaterialAmount = requiredMaterialAmount,
            requiredWork = 12f + lost * 28f,
            completedWork = 0f,
            targetDurability = policy.returnAtDurability,
            state = IsDefenseActive() && !policy.allowUnequipDuringInvasion
                ? CombatEquipmentRepairOrderState.PendingCombatEnd
                : CombatEquipmentRepairOrderState.WaitingForDelivery,
            manuallyRequested = manuallyRequested
        };
        CombatEquipmentRepairOrder[] proposedOrders = orders.Values
            .Where(candidate => candidate != null)
            .Append(order)
            .OrderBy(candidate => candidate.orderId, StringComparer.Ordinal)
            .ToArray();
        if (!TryPublishRepairBufferAuthorities(proposedOrders, out message))
        {
            return false;
        }
        orderSequence = nextSequence;
        orders[order.orderId] = order;
        if (order.state == CombatEquipmentRepairOrderState.WaitingForDelivery)
        {
            PrepareDelivery(order);
        }

        message = order.state == CombatEquipmentRepairOrderState.PendingCombatEnd
            ? "침공 종료 후 장비 수리를 시작합니다."
            : "장비 수리 운반을 요청했습니다.";
        return true;
    }

    private void RefreshOrders()
    {
        foreach (string staleOrderId in orders
            .Where(pair => pair.Value == null
                || pair.Value.state is CombatEquipmentRepairOrderState.Completed
                    or CombatEquipmentRepairOrderState.Cancelled)
            .Select(pair => pair.Key)
            .ToArray())
        {
            orders.Remove(staleOrderId);
        }

        foreach (CombatEquipmentRepairOrder order in orders.Values.ToArray())
        {
            if (!equipment.TryGetInstance(order.equipmentInstanceId, out _)
                || !TryFindOrderFacility(order, out _))
            {
                CancelOrderAndReleaseDestination(order);
                continue;
            }

            if (order.state == CombatEquipmentRepairOrderState.PendingCombatEnd
                && !IsDefenseActive())
            {
                order.state = CombatEquipmentRepairOrderState.WaitingForDelivery;
                PrepareDelivery(order);
            }

            if (order.state == CombatEquipmentRepairOrderState.WaitingForDelivery)
            {
                PrepareDelivery(order);
                if (HasDeliveredEquipment(order) && HasDeliveredMaterials(order))
                {
                    order.state = CombatEquipmentRepairOrderState.Ready;
                }
            }
        }
    }

    private void PrepareDelivery(CombatEquipmentRepairOrder order)
    {
        if (order == null
            || !equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance instance)
            || !TryFindOrderFacility(order, out BuildableObject facility))
        {
            return;
        }
        RequireRepairBufferAuthority(order, facility);

        if (!order.equipmentDeliveryRequested
            && HasEquipmentEnRoute(order, instance))
        {
            order.equipmentDeliveryRequested = true;
        }

        if (!order.equipmentDeliveryRequested)
        {
            Vector2Int sourcePosition = ResolveEquipmentSourcePosition(instance);
            string previousStackId = instance.sourceStackId;
            bool reusedPhysicalStack =
                !string.IsNullOrWhiteSpace(previousStackId)
                && items.TryRouteStackToDestination(
                    previousStackId,
                    WorldItemStackState.Loose,
                    order.FacilityDestinationId,
                    facility.centerPos,
                    out _);
            string stackId = reusedPhysicalStack
                ? previousStackId
                : string.Empty;
            bool stackReady = reusedPhysicalStack
                || (equipment.TryDropExistingEquipmentToWorld(
                        instance.instanceId,
                        sourcePosition,
                        out stackId,
                        out _)
                    && items.TryRouteStackToDestination(
                        stackId,
                        WorldItemStackState.Loose,
                        order.FacilityDestinationId,
                        facility.centerPos,
                        out _));
            if (stackReady)
            {
                if (equipment.TryLinkToWorldStack(
                        instance.instanceId,
                        stackId,
                        CombatEquipmentWorldState.Loose))
                {
                    order.equipmentDeliveryRequested = true;
                    TryRequestReplacement(order, instance.definitionId);
                }
            }
        }

        string materialItemId = ResolveOrderMaterialItemId(order);
        if (string.IsNullOrWhiteSpace(materialItemId))
        {
            order.materialDeliveryRequested = false;
            return;
        }
        int pendingMaterial = items.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    order.FacilityDestinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    materialItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        int requiredAmount = ResolveOrderMaterialAmount(order);
        if (pendingMaterial >= requiredAmount)
        {
            order.materialDeliveryRequested = true;
        }

        int missing = Mathf.Max(0, requiredAmount - pendingMaterial);
        if (missing > 0 && !order.materialDeliveryRequested)
        {
            bool requestedDelivery = items.TryRequestItemDelivery(
                materialItemId,
                missing,
                facility.centerPos,
                order.FacilityDestinationId,
                out int requested,
                out _);
            order.materialDeliveryRequested = requestedDelivery && requested > 0;
        }
    }

    private bool CompleteOrder(
        CombatEquipmentRepairOrder order,
        BuildableObject building,
        out string message)
    {
        message = string.Empty;
        if (!order.repairOutputReleased && !HasDeliveredEquipment(order))
        {
            if (!order.materialsConsumed)
            {
                order.state =
                    CombatEquipmentRepairOrderState.WaitingForDelivery;
            }
            message = "수리 장비가 작업대에 없습니다.";
            return false;
        }
        if (!order.materialsConsumed && !HasDeliveredMaterials(order))
        {
            order.state = CombatEquipmentRepairOrderState.WaitingForDelivery;
            message = "수리 재료가 부족합니다.";
            return false;
        }

        string materialItemId = ResolveOrderMaterialItemId(order);
        if (string.IsNullOrWhiteSpace(materialItemId))
        {
            order.state = CombatEquipmentRepairOrderState.WaitingForDelivery;
            message = "equipment.repair.material_definition_missing";
            return false;
        }
        WorldItemStackSnapshot equipmentStack = order.repairOutputReleased
            ? null
            : FindDeliveredEquipmentStack(order);
        if (!order.repairOutputReleased && equipmentStack == null)
        {
            order.state = CombatEquipmentRepairOrderState.WaitingForDelivery;
            message = "equipment.repair.equipment_stack_missing";
            return false;
        }

        if (!equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance beforeRepair)
            || !catalog.TryGet(
                beforeRepair.definitionId,
                out CombatEquipmentDefinitionSO definition))
        {
            message = "수리할 장비 상태를 찾지 못했습니다.";
            return false;
        }

        string equipmentSourceStackId = order.materialsConsumed
            ? order.repairEquipmentSourceStackId
            : equipmentStack.StackId;
        float durabilityBefore = beforeRepair.durabilityRatio;
        float durabilityAfter = Mathf.Clamp01(Mathf.Max(
            durabilityBefore,
            order.targetDurability));
        if (!EquipmentRepairMaterialOutbox.TryCommitOrResume(
                order,
                items.GetAllStacks(),
                batchDispositions,
                equipmentSourceStackId,
                durabilityBefore,
                durabilityAfter,
                out message))
        {
            return false;
        }

        if (!TryPublishRepairOutcome(order, out message)
            || !EquipmentRepairMaterialOutbox.TryAcknowledgeOutcome(
                order,
                batchDispositions,
                out message))
        {
            return false;
        }

        if (!order.repairOutputReleased)
        {
            WorldItemStackSnapshot exactOutput = items.GetAllStacks()
                .FirstOrDefault(stack => stack != null
                    && string.Equals(
                        stack.StackId,
                        order.repairEquipmentSourceStackId,
                        StringComparison.Ordinal)
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(
                        stack.DestinationId,
                        order.FacilityDestinationId,
                        StringComparison.Ordinal));
            if (exactOutput == null
                || !equipment.TrySetWorldStateBySourceStack(
                    order.repairEquipmentSourceStackId,
                    CombatEquipmentWorldState.Loose))
            {
                message = "equipment.repair.output_state_failed";
                return false;
            }

            int released = items.ReleaseStacksByDestination(
                order.FacilityDestinationId,
                building.centerPos);
            WorldItemStackSnapshot releasedOutput = items.GetAllStacks()
                .FirstOrDefault(stack => stack != null
                    && string.Equals(
                        stack.StackId,
                        order.repairEquipmentSourceStackId,
                        StringComparison.Ordinal));
            if (released <= 0
                || releasedOutput == null
                || releasedOutput.State != WorldItemStackState.Loose
                || !string.IsNullOrEmpty(releasedOutput.DestinationId))
            {
                message = "equipment.repair.output_release_failed";
                return false;
            }
            order.repairOutputReleased = true;
        }
        CombatEquipmentRepairOrderState previousState = order.state;
        float previousCompletedWork = order.completedWork;
        order.state = CombatEquipmentRepairOrderState.Completed;
        order.completedWork = order.requiredWork;
        if (!TryPublishRepairBufferAuthorities(
                orders.Values,
                out string terminalFailure))
        {
            order.state = previousState;
            order.completedWork = previousCompletedWork;
            message = "equipment.repair.buffer_terminal_close_failed:"
                + terminalFailure;
            return false;
        }
        message = $"{definition.DisplayName} 수리 완료";
        return true;
    }

    private bool TryPublishRepairOutcome(
        CombatEquipmentRepairOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance instance))
        {
            failureReason = "equipment.repair.instance_missing";
            return false;
        }

        float current = instance.durabilityRatio;
        if (!order.repairOutcomePublished)
        {
            if (Approximately(
                    current,
                    order.repairDurabilityBefore))
            {
                if (!equipment.TryRestoreDurability(
                        order.equipmentInstanceId,
                        order.repairDurabilityAfter))
                {
                    failureReason =
                        "equipment.repair.durability_publication_failed";
                    return false;
                }
            }
            else if (!Approximately(
                         current,
                         order.repairDurabilityAfter))
            {
                failureReason =
                    "equipment.repair.durability_conflict";
                return false;
            }

            if (!equipment.TryGetInstance(
                    order.equipmentInstanceId,
                    out instance)
                || !Approximately(
                    instance.durabilityRatio,
                    order.repairDurabilityAfter))
            {
                failureReason =
                    "equipment.repair.durability_result_mismatch";
                return false;
            }
            order.repairOutcomePublished = true;
            return true;
        }

        if (!Approximately(
                current,
                order.repairDurabilityAfter))
        {
            failureReason = "equipment.repair.durability_replay_conflict";
            return false;
        }
        return true;
    }

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;

    private bool TryPublishRepairBufferAuthorities(
        IEnumerable<CombatEquipmentRepairOrder> sourceOrders,
        out string failureReason) => TryPublishRepairBufferAuthorities(
            sourceOrders,
            aggregateState.TerminalEffects,
            out failureReason);

    private bool TryPublishRepairBufferAuthorities(
        IEnumerable<CombatEquipmentRepairOrder> sourceOrders,
        IReadOnlyDictionary<string,
            CombatEquipmentRepairTerminalEffectSaveData> terminalEffects,
        out string failureReason)
    {
        failureReason = string.Empty;
        CombatEquipmentRepairOrder[] activeOrders =
            (sourceOrders ?? Array.Empty<CombatEquipmentRepairOrder>())
            .Where(order => order != null
                && order.state is not CombatEquipmentRepairOrderState.Completed
                    and not CombatEquipmentRepairOrderState.Cancelled
                && !IsRepairDestinationClosed(order, terminalEffects))
            .OrderBy(order => order.orderId, StringComparer.Ordinal)
            .ToArray();
        List<FacilityBufferDestinationClaim> claims = new(activeOrders.Length);
        List<FacilityBufferCapacityProfile> profiles = new(activeOrders.Length);
        FacilityBufferDestinationClaim[] previousClaims = destinationClaims
            .CaptureClaims()
            .Where(claim => claim != null
                && string.Equals(
                    claim.OwnerDomain,
                    RepairDestinationOwnerDomain,
                    StringComparison.Ordinal))
            .OrderBy(claim => claim.DestinationId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferCapacityProfile[] previousProfiles = destinationCapacities
            .CaptureAuthorityProfiles()
            .Where(profile => profile != null
                && string.Equals(
                    profile.OwnerDomain,
                    RepairDestinationOwnerDomain,
                    StringComparison.Ordinal))
            .OrderBy(profile => profile.DestinationId, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < activeOrders.Length; index++)
        {
            CombatEquipmentRepairOrder order = activeOrders[index];
            if (!TryFindOrderFacility(order, out BuildableObject facility))
            {
                failureReason =
                    $"equipment.repair.facility_missing:{order.orderId}";
                return false;
            }
            if (!equipment.TryGetInstance(
                    order.equipmentInstanceId,
                    out CombatEquipmentInstance instance))
            {
                failureReason =
                    $"equipment.repair.instance_missing:{order.orderId}";
                return false;
            }
            if (!TryCalculateRepairBufferCapacity(
                    order,
                    instance,
                    out PhysicalMassGrams capacity,
                    out string capacityFailure))
            {
                failureReason = capacityFailure;
                return false;
            }

            FacilityBufferDestinationClaim claim =
                CreateDestinationClaim(order, facility);
            claims.Add(claim);
            profiles.Add(new FacilityBufferCapacityProfile(
                claim.DestinationId,
                claim.DropPosition,
                claim.OwnerDomain,
                claim.OwnerOperationId,
                claim.OwnerFacilityId,
                capacity,
                RepairBufferCapacitySchemaRevision));
        }

        if (!destinationLifecycle.TryReplaceOwnedAuthorities(
                RepairDestinationOwnerDomain,
                claims,
                profiles,
                out failureReason))
        {
            failureReason =
                "equipment.repair.buffer_authority_replace_failed:"
                + failureReason;
            return false;
        }

        FacilityBufferCapacityProfile[] expectedProfiles = profiles
            .OrderBy(profile => profile.DestinationId, StringComparer.Ordinal)
            .ToArray();
        FacilityBufferCapacityProfile[] published = destinationCapacities
            .CaptureAuthorityProfiles()
            .Where(profile => profile != null
                && string.Equals(
                    profile.OwnerDomain,
                    RepairDestinationOwnerDomain,
                    StringComparison.Ordinal))
            .OrderBy(profile => profile.DestinationId, StringComparer.Ordinal)
            .ToArray();
        if (published.Length != expectedProfiles.Length)
        {
            RollBackRepairBufferAuthoritiesOrThrow(
                previousClaims,
                previousProfiles);
            failureReason =
                "equipment.repair.buffer_profile_publication_count_mismatch";
            return false;
        }
        for (int index = 0; index < published.Length; index++)
        {
            FacilityBufferCapacityProfile expected = expectedProfiles[index];
            FacilityBufferCapacityProfile actual = published[index];
            if (!AreEquivalent(expected, actual))
            {
                RollBackRepairBufferAuthoritiesOrThrow(
                    previousClaims,
                    previousProfiles);
                failureReason =
                    "equipment.repair.buffer_profile_publication_mismatch:"
                    + expected.DestinationId;
                return false;
            }
        }
        return true;
    }

    private static bool IsRepairDestinationClosed(
        CombatEquipmentRepairOrder order,
        IReadOnlyDictionary<string,
            CombatEquipmentRepairTerminalEffectSaveData> terminalEffects) =>
        order != null
        && terminalEffects != null
        && terminalEffects.TryGetValue(
            order.orderId,
            out CombatEquipmentRepairTerminalEffectSaveData effect)
        && effect != null
        && effect.phase is CombatEquipmentRepairTerminalEffectPhase
                .DestinationClosedAwaitingSourceRemoval
            or CombatEquipmentRepairTerminalEffectPhase.SourceRemoved;

    private void RollBackRepairBufferAuthoritiesOrThrow(
        IReadOnlyList<FacilityBufferDestinationClaim> claims,
        IReadOnlyList<FacilityBufferCapacityProfile> profiles)
    {
        if (!destinationLifecycle.TryReplaceOwnedAuthorities(
                RepairDestinationOwnerDomain,
                claims,
                profiles,
                out string rollbackFailure))
        {
            throw new InvalidOperationException(
                "Equipment repair destination authority rollback failed: "
                + rollbackFailure);
        }
    }

    private void RequireRepairBufferAuthority(
        CombatEquipmentRepairOrder order,
        BuildableObject facility)
    {
        FacilityBufferDestinationClaim expectedClaim =
            CreateDestinationClaim(order, facility);
        FacilityBufferDestinationClaim actualClaim = destinationClaims
            .CaptureClaims()
            .SingleOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.DestinationId,
                    expectedClaim.DestinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.OwnerDomain,
                    RepairDestinationOwnerDomain,
                    StringComparison.Ordinal));
        if (actualClaim == null
            || actualClaim.DropPosition != expectedClaim.DropPosition
            || !string.Equals(
                actualClaim.OwnerOperationId,
                expectedClaim.OwnerOperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                actualClaim.OwnerFacilityId,
                expectedClaim.OwnerFacilityId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Equipment repair order '{order.orderId}' has no exact destination claim authority.");
        }
        if (!equipment.TryGetInstance(
                order.equipmentInstanceId,
                out CombatEquipmentInstance instance))
        {
            throw new InvalidOperationException(
                $"Equipment repair order '{order.orderId}' has no valid equipment authority.");
        }
        if (!TryCalculateRepairBufferCapacity(
                order,
                instance,
                out PhysicalMassGrams expectedCapacity,
                out string capacityFailure))
        {
            throw new InvalidOperationException(
                $"Equipment repair order '{order.orderId}' has no valid capacity authority: {capacityFailure}");
        }

        FacilityBufferCapacityProfile actualProfile = destinationCapacities
            .CaptureAuthorityProfiles()
            .SingleOrDefault(profile => profile != null
                && string.Equals(
                    profile.DestinationId,
                    expectedClaim.DestinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    profile.OwnerDomain,
                    RepairDestinationOwnerDomain,
                    StringComparison.Ordinal));
        FacilityBufferCapacityProfile expectedProfile = new(
            expectedClaim.DestinationId,
            expectedClaim.DropPosition,
            expectedClaim.OwnerDomain,
            expectedClaim.OwnerOperationId,
            expectedClaim.OwnerFacilityId,
            expectedCapacity,
            RepairBufferCapacitySchemaRevision);
        if (actualProfile == null || !AreEquivalent(expectedProfile, actualProfile))
        {
            throw new InvalidOperationException(
                $"Equipment repair order '{order.orderId}' has no exact positive-gram capacity profile.");
        }
    }

    private bool TryCalculateRepairBufferCapacity(
        CombatEquipmentRepairOrder order,
        CombatEquipmentInstance instance,
        out PhysicalMassGrams capacity,
        out string failureReason)
    {
        capacity = default;
        failureReason = string.Empty;
        try
        {
            Dictionary<string, EquipmentModuleInstance> modulesById = equipment
                .ModuleInstances
                .Where(module => module != null
                    && !string.IsNullOrWhiteSpace(module.instanceId))
                .ToDictionary(
                    module => module.instanceId,
                    module => module,
                    StringComparer.Ordinal);
            List<EquipmentModuleInstance> attachedModules = new();
            HashSet<int> seenSlotIndexes = new();
            HashSet<string> seenModuleIds = new(StringComparer.Ordinal);
            foreach (EquipmentModuleSlotState slot in
                     (instance.moduleSlots ?? new List<EquipmentModuleSlotState>())
                     .Where(slot => slot != null
                         && !string.IsNullOrWhiteSpace(slot.moduleInstanceId))
                     .OrderBy(slot => slot.slotIndex)
                     .ThenBy(slot => slot.moduleInstanceId, StringComparer.Ordinal))
            {
                if (!seenSlotIndexes.Add(slot.slotIndex)
                    || !seenModuleIds.Add(slot.moduleInstanceId))
                {
                    failureReason =
                        $"equipment.repair.attached_module_duplicate:{order.orderId}:{slot.moduleInstanceId}";
                    return false;
                }
                if (!modulesById.TryGetValue(
                        slot.moduleInstanceId,
                        out EquipmentModuleInstance module)
                    || module.state != EquipmentModuleProcessState.Installed
                    || !string.Equals(
                        module.attachedEquipmentInstanceId,
                        instance.instanceId,
                        StringComparison.Ordinal))
                {
                    failureReason =
                        $"equipment.repair.attached_module_invalid:{order.orderId}:{slot.moduleInstanceId}";
                    return false;
                }
                attachedModules.Add(module);
            }

            ItemDefinitionId equipmentItemId = (ItemDefinitionId)
                PhysicalItemIds.ForEquipment(instance.definitionId);
            ItemInstanceComponentSaveData component =
                EquipmentItemStateCodec.Encode(instance, attachedModules);
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                items.MassQuery,
                equipmentItemId,
                instance.instanceId,
                new[] { component });
            long equipmentMass = items.MassQuery.GetQuantityMass(
                equipmentItemId,
                subject,
                1).Value;
            int materialQuantity = ResolveOrderMaterialAmount(order);
            long materialMass = items.MassQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)order.materialItemId)
                .Multiply(materialQuantity)
                .Value;
            long totalMass = checked(equipmentMass + materialMass);
            if (equipmentMass <= 0L || materialMass <= 0L || totalMass <= 0L)
            {
                failureReason =
                    $"equipment.repair.capacity_not_positive:{order.orderId}";
                return false;
            }
            capacity = new PhysicalMassGrams(totalMass);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidOperationException
                or OverflowException)
        {
            failureReason =
                $"equipment.repair.capacity_invalid:{order.orderId}:{exception.Message}";
            return false;
        }
    }

    private static bool AreEquivalent(
        FacilityBufferCapacityProfile left,
        FacilityBufferCapacityProfile right) =>
        left != null
        && right != null
        && string.Equals(left.DestinationId, right.DestinationId, StringComparison.Ordinal)
        && left.DropPosition == right.DropPosition
        && string.Equals(left.OwnerDomain, right.OwnerDomain, StringComparison.Ordinal)
        && string.Equals(left.OwnerOperationId, right.OwnerOperationId, StringComparison.Ordinal)
        && string.Equals(left.OwnerFacilityId, right.OwnerFacilityId, StringComparison.Ordinal)
        && left.MaxMassGrams == right.MaxMassGrams
        && left.CapacityRevision == right.CapacityRevision;

    private void CancelOrderAndReleaseDestination(CombatEquipmentRepairOrder order)
    {
        if (order == null)
            return;

        if (order.materialsConsumed
            || !string.IsNullOrEmpty(order.materialTransferOperationId))
        {
            throw new InvalidOperationException(
                $"Equipment repair order '{order.orderId}' cannot be cancelled after its material entered WIP.");
        }

        FacilityBufferDestinationClaim claim = destinationClaims.CaptureClaims()
            .SingleOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.DestinationId,
                    order.FacilityDestinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.OwnerDomain,
                    RepairDestinationOwnerDomain,
                    StringComparison.Ordinal)
                && string.Equals(
                    candidate.OwnerOperationId,
                    order.orderId,
                    StringComparison.Ordinal));
        if (claim == null)
        {
            throw new InvalidOperationException(
                $"Equipment repair order '{order.orderId}' lost its destination claim before cancellation.");
        }

        items.ReleaseStacksByDestination(
            order.FacilityDestinationId,
            claim.DropPosition);
        CombatEquipmentRepairOrderState previousState = order.state;
        order.state = CombatEquipmentRepairOrderState.Cancelled;
        if (!TryPublishRepairBufferAuthorities(
                orders.Values,
                out string terminalFailure))
        {
            order.state = previousState;
            throw new InvalidOperationException(
                "Equipment repair destination terminal close failed: "
                + terminalFailure);
        }
    }

    private static FacilityBufferDestinationClaim CreateDestinationClaim(
        CombatEquipmentRepairOrder order,
        BuildableObject facility)
    {
        if (order == null)
            throw new ArgumentNullException(nameof(order));
        if (facility == null)
            throw new ArgumentNullException(nameof(facility));
        return new FacilityBufferDestinationClaim(
            order.FacilityDestinationId,
            facility.centerPos,
            RepairDestinationOwnerDomain,
            order.orderId,
            order.facilityBuildingId,
            FacilityBufferDestinationAnchorKind.LiveFacility);
    }

    private bool HasEquipmentEnRoute(
        CombatEquipmentRepairOrder order,
        CombatEquipmentInstance instance)
    {
        if (instance != null
            && instance.worldState is CombatEquipmentWorldState.Carried
                or CombatEquipmentWorldState.MaintenanceBuffer)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(instance.sourceStackId)
            && items.GetAllStacks().Any(stack =>
                stack != null
                && string.Equals(
                    stack.StackId,
                    instance.sourceStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    order.FacilityDestinationId,
                    StringComparison.Ordinal));
    }

    private bool HasDeliveredEquipment(CombatEquipmentRepairOrder order)
    {
        return FindDeliveredEquipmentStack(order) != null;
    }

    private WorldItemStackSnapshot FindDeliveredEquipmentStack(
        CombatEquipmentRepairOrder order)
    {
        return items.GetAllStacks().FirstOrDefault(stack =>
            stack != null
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(
                stack.DestinationId,
                order.FacilityDestinationId,
                StringComparison.Ordinal)
            && equipment.TryGetInstanceBySourceStack(
                stack.StackId,
                out CombatEquipmentInstance linked)
            && string.Equals(
                linked.instanceId,
                order.equipmentInstanceId,
                StringComparison.Ordinal));
    }

    private bool HasDeliveredMaterials(CombatEquipmentRepairOrder order)
    {
        string materialItemId = ResolveOrderMaterialItemId(order);
        if (string.IsNullOrWhiteSpace(materialItemId))
        {
            return false;
        }
        return items.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    order.FacilityDestinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    materialItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity) >= ResolveOrderMaterialAmount(order);
    }

    private Vector2Int ResolveEquipmentSourcePosition(CombatEquipmentInstance instance)
    {
        CharacterActor owner = FindCharacter(instance.ownerCharacterId);
        if (owner != null)
        {
            return owner.GetNowXY();
        }

        WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.StackId,
                instance.sourceStackId,
                StringComparison.Ordinal));
        return stack?.Position ?? FindMaintenanceFacility()?.centerPos ?? Vector2Int.zero;
    }

    private BuildableObject FindMaintenanceFacility()
    {
        return worldRegistry.Buildings
            .Where(CombatEquipmentMaintenanceFacilityUtility.IsMaintenanceFacility)
            .OrderBy(building => building.IsDamaged ? 1 : 0)
            .ThenBy(building => building.centerPos.y)
            .ThenBy(building => building.centerPos.x)
            .FirstOrDefault();
    }

    private bool TryFindOrderFacility(
        CombatEquipmentRepairOrder order,
        out BuildableObject facility)
    {
        string facilityId = order?.facilityBuildingId ?? string.Empty;
        facility = worldRegistry.Buildings.FirstOrDefault(building =>
            CombatEquipmentMaintenanceFacilityUtility.IsMaintenanceFacility(building)
            && string.Equals(
                building.PersistentInstanceId.Value,
                facilityId,
                StringComparison.Ordinal));
        return facility != null;
    }

    private static bool IsOrderForBuilding(
        CombatEquipmentRepairOrder order,
        BuildableObject building)
    {
        return order != null
            && building != null
            && string.Equals(
                order.facilityBuildingId,
                building.PersistentInstanceId.Value,
                StringComparison.Ordinal);
    }

    private EquipmentMaintenancePolicyData GetPolicyByCharacterId(string characterId)
    {
        string policyId = !string.IsNullOrWhiteSpace(characterId)
            && assignments.TryGetValue(characterId, out string assigned)
                ? assigned
                : StandardPolicyId;
        return policies.TryGetValue(policyId, out EquipmentMaintenancePolicyData policy)
            ? policy
            : policies[StandardPolicyId];
    }

    private void TryRequestReplacement(
        CombatEquipmentRepairOrder order,
        string definitionId)
    {
        if (order == null
            || string.IsNullOrWhiteSpace(order.originalOwnerCharacterId)
            || !GetPolicyByCharacterId(order.originalOwnerCharacterId).preferReplacement)
        {
            return;
        }

        CharacterActor owner = FindCharacter(order.originalOwnerCharacterId);
        if (owner == null
            || owner.IsDead
            || owner.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            return;
        }

        bool hasStoredReplacement = equipment.Instances.Any(candidate =>
            candidate != null
            && !string.Equals(
                candidate.instanceId,
                order.equipmentInstanceId,
                StringComparison.Ordinal)
            && string.Equals(candidate.definitionId, definitionId, StringComparison.Ordinal)
            && candidate.worldState == CombatEquipmentWorldState.Stored
            && !string.IsNullOrWhiteSpace(candidate.sourceStackId)
            && items.GetAllStacks().Any(stack =>
                stack != null
                && string.Equals(
                    stack.StackId,
                    candidate.sourceStackId,
                    StringComparison.Ordinal)
                && stack.State == WorldItemStackState.Stored));
        if (hasStoredReplacement)
        {
            equipmentPickup.TryRequestEquipmentPickup(owner, definitionId, out _);
        }
    }

    private bool TryResolveRepairMaterial(
        CombatEquipmentInstance instance,
        CombatEquipmentDefinitionSO definition,
        out string materialItemId)
    {
        materialItemId = string.Empty;
        return definition != null
            && EquipmentMaintenanceSaveValidation.TryResolveRepairMaterial(
                instance,
                catalog,
                resourceCatalog,
                out materialItemId);
    }

    private string ResolveOrderMaterialItemId(
        CombatEquipmentRepairOrder order)
    {
        return order?.materialItemId ?? string.Empty;
    }

    private static int ResolveOrderMaterialAmount(
        CombatEquipmentRepairOrder order)
    {
        return order?.requiredMaterialAmount ?? 0;
    }

    private bool IsDefenseActive()
    {
        return defenseEngagements.Engagements.Any(item =>
            item != null && item.IsActive) == true;
    }

    private CharacterActor FindCharacter(string characterId)
    {
        return worldRegistry.Characters.FirstOrDefault(actor =>
            actor != null
            && string.Equals(
                GetCharacterId(actor),
                characterId,
                StringComparison.Ordinal));
    }

    private static string GetCharacterId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor).Value
            : string.Empty;
    }
}
