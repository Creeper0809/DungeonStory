using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ExpeditionReturnStage
{
    Pending = 1,
    ToDoor = 2,
    ToInterior = 3,
    Arrived = 4,
    RequiresRescue = 5,
    Dead = 6
}

public sealed class ExpeditionReturnProgress
{
    public ExpeditionReturnProgress(string characterId, ExpeditionReturnStage stage = ExpeditionReturnStage.Pending)
    {
        if (string.IsNullOrWhiteSpace(characterId) || characterId != characterId.Trim()
            || !Enum.IsDefined(typeof(ExpeditionReturnStage), stage))
            throw new ArgumentException("Invalid expedition return progress.");
        CharacterId = characterId; Stage = stage;
    }
    public string CharacterId { get; }
    public ExpeditionReturnStage Stage { get; internal set; }
    public bool IsTerminal => Stage >= ExpeditionReturnStage.Arrived;
    public string LastFailure { get; internal set; } = string.Empty;
    internal bool InFlight { get; set; }
}

public sealed class OffenseExpeditionEquipmentBaseline
{
    public OffenseExpeditionEquipmentBaseline(
        string instanceId,
        string definitionId,
        float durabilityRatio)
    {
        InstanceId = instanceId?.Trim() ?? string.Empty;
        DefinitionId = definitionId?.Trim() ?? string.Empty;
        DurabilityRatio = Mathf.Clamp01(durabilityRatio);
    }

    public string InstanceId { get; }
    public string DefinitionId { get; }
    public float DurabilityRatio { get; }
}

public sealed class OffenseExpeditionAmmunitionConsumption
{
    public OffenseExpeditionAmmunitionConsumption(
        string instanceId,
        string itemId,
        int quantity)
    {
        InstanceId = instanceId?.Trim() ?? string.Empty;
        ItemId = itemId?.Trim() ?? string.Empty;
        Quantity = Mathf.Max(0, quantity);
    }

    public string InstanceId { get; }
    public string ItemId { get; }
    public int Quantity { get; }
}

public sealed class OffenseExpeditionRun
{
    private readonly List<CharacterActor> members;
    private readonly List<CharacterActor> protectedRescueMembers =
        new List<CharacterActor>();
    private readonly IReadOnlyList<CharacterActor> membersView;
    private readonly IReadOnlyList<CharacterActor> protectedRescueMembersView;
    private readonly List<OffenseExpeditionMemberState> memberStates;
    private readonly IReadOnlyList<OffenseExpeditionMemberState> memberStatesView;
    private readonly HashSet<string> completedNodeIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<StockCategory, int> carriedStock = new Dictionary<StockCategory, int>();
    private readonly IReadOnlyDictionary<StockCategory, int> carriedStockView;
    private readonly HashSet<string> recoveredEquipmentInstanceIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<OffenseSupplyType, int> consumedSupplies = new();
    private readonly List<OffenseExpeditionTreatmentReceipt> treatmentReceipts = new();
    private readonly List<OffenseExpeditionEquipmentBaseline> equipmentBaselines = new();
    private readonly List<OffenseExpeditionAmmunitionConsumption>
        ammunitionConsumptions = new();
    private readonly List<OffenseExpeditionItemReceipt>
        returnItemReceipts = new();
    private readonly List<OffenseExpeditionCurrencyReceipt>
        returnCurrencyReceipts = new();
    private readonly List<ExpeditionReturnProgress> returnProgress = new();
    private IReadOnlyList<ExpeditionReturnProgress> returnProgressView;
    public IReadOnlyList<ExpeditionReturnProgress> ReturnProgress => returnProgressView ??= returnProgress.AsReadOnly();
    public bool ReturnPending { get; private set; }
    public bool ReturnSuccess { get; private set; }
    public string ReturnMessage { get; private set; } = string.Empty;
    public bool ReturnFinalized { get; internal set; }
    public bool ReturnResourcesCommitted { get; private set; }
    public string ReturnResourceFailure { get; private set; } = string.Empty;
    internal bool ReturnBarrierInitialized { get; set; }
    internal bool ReturnFinalizing { get; set; }

    public void BeginPhysicalReturn(bool success, string message)
    {
        if (ReturnPending || ReturnFinalized) return;
        ReturnPending = true;
        ReturnSuccess = success;
        ReturnMessage = message ?? string.Empty;
        foreach (var actor in MemberActors.Concat(ProtectedRescueActors))
            returnProgress.Add(new ExpeditionReturnProgress(CharacterPersistentIdentity.Require(actor).Value));
    }

    public void RestorePhysicalReturn(bool pending, bool success, string message,
        IEnumerable<ExpeditionReturnProgress> progress)
    {
        var restored = progress?.ToList() ?? throw new ArgumentNullException(nameof(progress));
        var expected = MemberActors.Concat(ProtectedRescueActors)
            .Select(actor => CharacterPersistentIdentity.Require(actor).Value).OrderBy(id => id, StringComparer.Ordinal);
        if (pending ? !restored.Select(value => value.CharacterId).OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(expected)
                    : restored.Count != 0 || success || !string.IsNullOrEmpty(message))
            throw new InvalidOperationException("Invalid expedition return membership/state.");
        ReturnPending = pending; ReturnSuccess = success; ReturnMessage = message ?? string.Empty;
        returnProgress.Clear(); returnProgress.AddRange(restored);
        ReturnBarrierInitialized = false; ReturnFinalized = false; ReturnFinalizing = false;
    }

    public OffenseExpeditionRun(
        string expeditionId,
        OffenseTargetDefinition target,
        IEnumerable<CharacterActor> members,
        float totalPower)
        : this(
            expeditionId,
            target,
            members,
            totalPower,
            target != null ? Mathf.Max(1f, target.durationSeconds) : 1f,
            null,
            null,
            null)
    {
    }

    public OffenseExpeditionRun(
        string expeditionId,
        OffenseTargetDefinition target,
        IEnumerable<CharacterActor> members,
        float totalPower,
        float remainingSeconds)
        : this(
            expeditionId,
            target,
            members,
            totalPower,
            remainingSeconds,
            null,
            null,
            null)
    {
    }

    public OffenseExpeditionRun(
        string expeditionId,
        OffenseTargetDefinition target,
        IEnumerable<CharacterActor> members,
        float totalPower,
        float remainingSeconds,
        OffenseRouteGraph route,
        OffenseSupplyLoadout supplies,
        OffenseExpeditionPreparation preparation)
    {
        ExpeditionId = string.IsNullOrWhiteSpace(expeditionId)
            ? Guid.NewGuid().ToString("N")
            : expeditionId;
        Target = target;
        this.members = members?.Where((member) => member != null).Distinct().ToList()
            ?? new List<CharacterActor>();
        membersView = this.members.AsReadOnly();
        protectedRescueMembersView = protectedRescueMembers.AsReadOnly();
        memberStates = this.members
            .Take(5)
            .Select((member, index) => new OffenseExpeditionMemberState(
                member,
                (OffenseFormationSlot)Mathf.Clamp(index / 2, 0, 2)))
            .ToList();
        memberStatesView = memberStates.AsReadOnly();
        TotalPower = Mathf.Max(0f, totalPower);
        TotalDurationSeconds = target != null ? Mathf.Max(1f, target.durationSeconds) : 1f;
        RemainingSeconds = Mathf.Clamp(remainingSeconds, 0f, TotalDurationSeconds);
        Route = route ?? OffenseRouteGenerator.Create(target);
        Supplies = supplies ?? new OffenseSupplyLoadout();
        Preparation = preparation ?? new OffenseExpeditionPreparation();
        FieldFunds = Preparation.FieldFunds;
        Light = Preparation.StartingLight;
        CurrentNodeId = Route.EntranceNodeId;
        completedNodeIds.Add(Route.EntranceNodeId);
        carriedStockView = carriedStock;
        Phase = OffenseExpeditionPhase.ChoosingRoute;
    }

    public string ExpeditionId { get; }
    public OffenseTargetDefinition Target { get; private set; }
    public IReadOnlyList<CharacterActor> MemberActors => membersView;
    public IReadOnlyList<CharacterActor> ProtectedRescueActors =>
        protectedRescueMembersView;
    public IReadOnlyList<OffenseExpeditionMemberState> MemberStates => memberStatesView;
    public float TotalPower { get; }
    public float RemainingSeconds { get; private set; }
    public float TotalDurationSeconds { get; }
    public OffenseRouteGraph Route { get; }
    public OffenseSupplyLoadout Supplies { get; }
    public OffenseExpeditionPreparation Preparation { get; }
    public OffenseExpeditionPhase Phase { get; private set; }
    public string CurrentNodeId { get; private set; }
    public float Light { get; private set; }
    public IReadOnlyCollection<string> CompletedNodeIds => completedNodeIds;
    public IReadOnlyDictionary<StockCategory, int> CarriedStock => carriedStockView;
    public IReadOnlyCollection<string> RecoveredEquipmentInstanceIds =>
        recoveredEquipmentInstanceIds;
    public IReadOnlyDictionary<OffenseSupplyType, int> ConsumedSupplies =>
        consumedSupplies;
    public IReadOnlyList<OffenseExpeditionTreatmentReceipt> TreatmentReceipts =>
        treatmentReceipts;
    public IReadOnlyList<OffenseExpeditionEquipmentBaseline> EquipmentBaselines =>
        equipmentBaselines;
    public IReadOnlyList<OffenseExpeditionAmmunitionConsumption>
        AmmunitionConsumptions => ammunitionConsumptions;
    public IReadOnlyList<OffenseExpeditionItemReceipt> ReturnItemReceipts =>
        returnItemReceipts;
    public IReadOnlyList<OffenseExpeditionCurrencyReceipt>
        ReturnCurrencyReceipts => returnCurrencyReceipts;
    public bool IsComplete => Phase is OffenseExpeditionPhase.Completed
        or OffenseExpeditionPhase.Retreated
        or OffenseExpeditionPhase.Defeated;
    public bool UsesWorldTravel => !string.IsNullOrWhiteSpace(WorldSiteId);
    public string WorldSiteId { get; private set; } = string.Empty;
    public bool WorldObjectiveCompleted { get; private set; }
    public bool WorldObjectiveBattleActive { get; private set; }
    public bool DepartureCompleted { get; private set; }
    public int FieldFunds { get; private set; }
    public bool FieldFundsReturned { get; private set; }
    public OffenseRouteNode CurrentNode => Route.TryGetNode(CurrentNodeId, out OffenseRouteNode node)
        ? node
        : null;

    public bool TryConsumeSupply(OffenseSupplyType type, int amount)
    {
        int requested = Mathf.Max(0, amount);
        if (requested <= 0 || !Supplies.TryConsume(type, requested))
        {
            return requested == 0;
        }

        consumedSupplies[type] = checked(
            (consumedSupplies.TryGetValue(type, out int current) ? current : 0)
            + requested);
        return true;
    }

    public void RecordHealing(CharacterActor actor, float beforeHealth)
    {
        if (actor == null) return;
        float healed = Mathf.Max(0f, actor.CurrentHealth - beforeHealth);
        string characterId = actor.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (healed <= 0f || characterId.Length == 0) return;
        RecordHealing(characterId, healed);
    }

    public void RecordHealing(string characterId, float healedAmount)
    {
        string normalized = characterId?.Trim() ?? string.Empty;
        float healed = Mathf.Max(0f, healedAmount);
        if (normalized.Length == 0 || healed <= 0f) return;
        treatmentReceipts.Add(new OffenseExpeditionTreatmentReceipt(
            OffenseExpeditionTreatmentKind.Healing,
            normalized,
            string.Empty,
            healed));
    }

    public void RecordAmmunitionConsumption(
        string instanceId,
        string itemId,
        int quantity)
    {
        string normalizedInstance = instanceId?.Trim() ?? string.Empty;
        string normalizedItem = itemId?.Trim() ?? string.Empty;
        int committed = Mathf.Max(0, quantity);
        if (normalizedInstance.Length == 0
            || normalizedItem.Length == 0
            || committed <= 0)
        {
            throw new InvalidOperationException(
                "Committed expedition ammunition requires an instance, item and positive quantity.");
        }

        OffenseExpeditionAmmunitionConsumption existing =
            ammunitionConsumptions.FirstOrDefault(value =>
                string.Equals(value.InstanceId, normalizedInstance,
                    StringComparison.Ordinal)
                && string.Equals(value.ItemId, normalizedItem,
                    StringComparison.Ordinal));
        if (existing == null)
        {
            ammunitionConsumptions.Add(
                new OffenseExpeditionAmmunitionConsumption(
                    normalizedInstance,
                    normalizedItem,
                    committed));
            return;
        }

        int index = ammunitionConsumptions.IndexOf(existing);
        ammunitionConsumptions[index] =
            new OffenseExpeditionAmmunitionConsumption(
                normalizedInstance,
                normalizedItem,
                checked(existing.Quantity + committed));
    }

    public void RecordStabilization(
        CharacterActor actor,
        string anatomyNodeId)
    {
        string characterId = actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        if (characterId.Length == 0 || string.IsNullOrWhiteSpace(anatomyNodeId))
        {
            return;
        }

        treatmentReceipts.Add(new OffenseExpeditionTreatmentReceipt(
            OffenseExpeditionTreatmentKind.Stabilization,
            characterId,
            anatomyNodeId,
            0f));
    }

    public void CaptureEquipmentBaseline(ICombatEquipmentRuntime equipment)
    {
        if (equipment == null || equipmentBaselines.Count > 0) return;
        HashSet<string> instanceIds = new(StringComparer.Ordinal);
        foreach (CharacterActor actor in members.Where(value => value != null))
        {
            string characterId = actor.Identity?.PersistentId?.Trim() ?? string.Empty;
            if (characterId.Length == 0) continue;
            CharacterCombatLoadoutProfile profile =
                equipment.GetActiveProfileSnapshot(characterId);
            if (profile == null) continue;
            foreach (string id in profile.weaponInstanceIds
                         ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(id)) instanceIds.Add(id);
            foreach (string id in profile.armorInstanceIds
                         ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(id)) instanceIds.Add(id);
            if (!string.IsNullOrWhiteSpace(profile.shieldInstanceId))
                instanceIds.Add(profile.shieldInstanceId);
        }

        foreach (string instanceId in instanceIds.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            if (!equipment.TryGetInstance(instanceId, out CombatEquipmentInstance instance))
            {
                throw new InvalidOperationException(
                    $"Expedition equipment instance '{instanceId}' is unavailable while capturing its settlement baseline.");
            }
            equipmentBaselines.Add(new OffenseExpeditionEquipmentBaseline(
                instance.instanceId,
                instance.definitionId,
                instance.durabilityRatio));
        }
    }

    public IReadOnlyList<OffenseExpeditionItemReceipt> FreezeItemReceipts(
        ICombatEquipmentRuntime equipment,
        IV27EmbeddedWorkValueProjectionQuery values)
    {
        List<OffenseExpeditionItemReceipt> receipts = new();
        foreach (KeyValuePair<OffenseSupplyType, int> consumed in
                 consumedSupplies.OrderBy(pair => pair.Key))
        {
            string itemId = OffenseSupplyCatalog.GetPhysicalItemId(consumed.Key);
            receipts.Add(new OffenseExpeditionItemReceipt(
                OffenseExpeditionItemReceiptKind.SupplyConsumed,
                itemId,
                consumed.Value,
                string.Empty,
                0f,
                CreateValuation(values, itemId, consumed.Value)));
        }

        foreach (OffenseExpeditionAmmunitionConsumption consumed in
                 ammunitionConsumptions
                     .OrderBy(value => value.InstanceId, StringComparer.Ordinal)
                     .ThenBy(value => value.ItemId, StringComparer.Ordinal))
        {
            receipts.Add(new OffenseExpeditionItemReceipt(
                OffenseExpeditionItemReceiptKind.AmmunitionConsumed,
                consumed.ItemId,
                consumed.Quantity,
                consumed.InstanceId,
                0f,
                CreateValuation(values, consumed.ItemId, consumed.Quantity)));
        }

        if (equipment == null && equipmentBaselines.Count > 0)
        {
            throw new InvalidOperationException(
                "Expedition equipment receipts require the combat equipment authority.");
        }
        foreach (OffenseExpeditionEquipmentBaseline baseline in
                 equipmentBaselines.OrderBy(value => value.InstanceId, StringComparer.Ordinal))
        {
            CombatEquipmentInstance current = null;
            if (!equipment.TryGetInstance(baseline.InstanceId, out current))
            {
                throw new InvalidOperationException(
                    $"Expedition equipment instance '{baseline.InstanceId}' is unavailable at settlement.");
            }
            if (!equipment.TryGetDefinition(
                    baseline.DefinitionId,
                    out CombatEquipmentDefinitionSO definition))
            {
                throw new InvalidOperationException(
                    $"Expedition equipment definition '{baseline.DefinitionId}' is unavailable at settlement.");
            }
            string itemId = definition.ItemId;
            bool lost = current.worldState == CombatEquipmentWorldState.Lost;
            if (lost)
            {
                receipts.Add(new OffenseExpeditionItemReceipt(
                    OffenseExpeditionItemReceiptKind.EquipmentLost,
                    itemId,
                    1,
                    baseline.InstanceId,
                    baseline.DurabilityRatio,
                    CreateValuation(values, itemId, 1)));
                continue;
            }

            float wear = Mathf.Max(0f, baseline.DurabilityRatio - current.durabilityRatio);
            if (wear > 0f)
            {
                receipts.Add(new OffenseExpeditionItemReceipt(
                    OffenseExpeditionItemReceiptKind.EquipmentWorn,
                    itemId,
                    1,
                    baseline.InstanceId,
                    wear,
                    CreateValuation(values, itemId, 1)));
            }

        }

        return receipts;
    }

    public void RestoreSettlementReceipts(
        IReadOnlyDictionary<OffenseSupplyType, int> restoredConsumedSupplies,
        IEnumerable<OffenseExpeditionTreatmentReceipt> restoredTreatments,
        IEnumerable<OffenseExpeditionEquipmentBaseline> restoredEquipmentBaselines,
        IEnumerable<OffenseExpeditionAmmunitionConsumption>
            restoredAmmunitionConsumptions = null)
    {
        consumedSupplies.Clear();
        foreach (KeyValuePair<OffenseSupplyType, int> pair in
                 restoredConsumedSupplies ?? new Dictionary<OffenseSupplyType, int>())
            if (pair.Value > 0) consumedSupplies[pair.Key] = pair.Value;
        treatmentReceipts.Clear();
        treatmentReceipts.AddRange((restoredTreatments
            ?? Array.Empty<OffenseExpeditionTreatmentReceipt>())
            .Where(value => value != null));
        equipmentBaselines.Clear();
        equipmentBaselines.AddRange((restoredEquipmentBaselines
            ?? Array.Empty<OffenseExpeditionEquipmentBaseline>())
            .Where(value => value != null));
        ammunitionConsumptions.Clear();
        ammunitionConsumptions.AddRange((restoredAmmunitionConsumptions
            ?? Array.Empty<OffenseExpeditionAmmunitionConsumption>())
            .Where(value => value != null));
    }

    public void RestoreReturnResourceSettlement(
        bool committed,
        string failure,
        IEnumerable<OffenseExpeditionItemReceipt> restoredItems,
        IEnumerable<OffenseExpeditionCurrencyReceipt> restoredCurrencies)
    {
        returnItemReceipts.Clear();
        returnCurrencyReceipts.Clear();
        RecordReturnItemReceipts(restoredItems);
        RecordReturnCurrencyReceipts(restoredCurrencies);
        ReturnResourcesCommitted = committed;
        ReturnResourceFailure = committed
            ? string.Empty
            : failure ?? string.Empty;
    }

    internal void RecordReturnItemReceipts(
        IEnumerable<OffenseExpeditionItemReceipt> receipts)
    {
        foreach (OffenseExpeditionItemReceipt receipt in receipts
                     ?? Array.Empty<OffenseExpeditionItemReceipt>())
        {
            if (receipt == null)
            {
                throw new InvalidOperationException(
                    "A committed expedition return item receipt cannot be null.");
            }
            OffenseExpeditionItemReceipt existing = returnItemReceipts
                .FirstOrDefault(value => ReturnReceiptKey(value)
                    == ReturnReceiptKey(receipt));
            if (existing == null)
            {
                returnItemReceipts.Add(receipt);
                continue;
            }
            if (!SameReturnReceipt(existing, receipt))
            {
                throw new InvalidOperationException(
                    $"Expedition return receipt '{receipt.kind}/{receipt.itemId}/{receipt.instanceId}' changed after physical commit.");
            }
        }
    }

    internal void RecordReturnCurrencyReceipts(
        IEnumerable<OffenseExpeditionCurrencyReceipt> receipts)
    {
        foreach (OffenseExpeditionCurrencyReceipt receipt in receipts
                     ?? Array.Empty<OffenseExpeditionCurrencyReceipt>())
        {
            if (receipt == null)
            {
                throw new InvalidOperationException(
                    "A committed expedition return currency receipt cannot be null.");
            }
            OffenseExpeditionCurrencyReceipt existing = returnCurrencyReceipts
                .FirstOrDefault(value => string.Equals(
                    value.operationId,
                    receipt.operationId,
                    StringComparison.Ordinal));
            if (existing == null)
            {
                returnCurrencyReceipts.Add(receipt);
                continue;
            }
            if (!string.Equals(
                    existing.currencyId,
                    receipt.currencyId,
                    StringComparison.Ordinal)
                || existing.amount != receipt.amount)
            {
                throw new InvalidOperationException(
                    $"Expedition currency receipt '{receipt.operationId}' changed after commit.");
            }
        }
    }

    internal bool MarkReturnResourceFailure(string failure)
    {
        string next = string.IsNullOrWhiteSpace(failure)
            ? "expedition-return-resource-commit-failed"
            : failure.Trim();
        if (string.Equals(
                ReturnResourceFailure,
                next,
                StringComparison.Ordinal))
        {
            return false;
        }
        ReturnResourceFailure = next;
        return true;
    }

    internal void CompleteReturnResources()
    {
        ReturnResourcesCommitted = true;
        ReturnResourceFailure = string.Empty;
    }

    internal OffenseExpeditionReturnReceipt GetReturnResourceReceipt() =>
        new OffenseExpeditionReturnReceipt(
            returnItemReceipts,
            returnCurrencyReceipts);

    private static (
        OffenseExpeditionItemReceiptKind kind,
        string itemId,
        string instanceId) ReturnReceiptKey(
        OffenseExpeditionItemReceipt receipt)
    {
        string instance = receipt.kind is
                OffenseExpeditionItemReceiptKind.AmmunitionConsumed
                or OffenseExpeditionItemReceiptKind.EquipmentWorn
                or OffenseExpeditionItemReceiptKind.EquipmentLost
                or OffenseExpeditionItemReceiptKind.EquipmentRecovered
            ? receipt.instanceId
            : string.Empty;
        return (receipt.kind, receipt.itemId, instance);
    }

    private static bool SameReturnReceipt(
        OffenseExpeditionItemReceipt left,
        OffenseExpeditionItemReceipt right)
    {
        OffenseItemValuationSnapshot leftValue = left.valuation;
        OffenseItemValuationSnapshot rightValue = right.valuation;
        return left.kind == right.kind
            && string.Equals(left.itemId, right.itemId, StringComparison.Ordinal)
            && left.quantity == right.quantity
            && string.Equals(
                left.instanceId,
                right.instanceId,
                StringComparison.Ordinal)
            && Mathf.Approximately(left.durabilityLoss, right.durabilityLoss)
            && leftValue != null
            && rightValue != null
            && string.Equals(
                leftValue.itemId,
                rightValue.itemId,
                StringComparison.Ordinal)
            && leftValue.quantity == rightValue.quantity
            && leftValue.state == rightValue.state
            && leftValue.acquisitionMilliEwuPerUnit
                == rightValue.acquisitionMilliEwuPerUnit
            && leftValue.recoverableMilliEwuPerUnit
                == rightValue.recoverableMilliEwuPerUnit
            && string.Equals(
                leftValue.basisId,
                rightValue.basisId,
                StringComparison.Ordinal)
            && string.Equals(
                leftValue.selectedSourceId,
                rightValue.selectedSourceId,
                StringComparison.Ordinal);
    }

    public static OffenseItemValuationSnapshot CreateValuation(
        IV27EmbeddedWorkValueProjectionQuery values,
        string itemId,
        int quantity)
    {
        if (values == null || !values.AuthorityAvailable)
        {
            return new OffenseItemValuationSnapshot(
                itemId,
                quantity,
                OffenseSettlementValuationState.AuthorityUnavailable);
        }
        if (!values.TryGet(itemId, out V27EmbeddedWorkValueProjection value))
        {
            return new OffenseItemValuationSnapshot(
                itemId,
                quantity,
                OffenseSettlementValuationState.UnvaluedItem);
        }
        return new OffenseItemValuationSnapshot(
            itemId,
            quantity,
            OffenseSettlementValuationState.Valued,
            value.AcquisitionMilliEwu,
            value.RecoverableMilliEwu,
            value.BasisId,
            value.SelectedSourceId);
    }

    public void MergeProtectedRescueMembers(IEnumerable<CharacterActor> rescued)
    {
        foreach (CharacterActor actor in rescued ?? Array.Empty<CharacterActor>())
        {
            if (actor != null
                && !members.Contains(actor)
                && !protectedRescueMembers.Contains(actor))
            {
                protectedRescueMembers.Add(actor);
            }
        }
    }

    public void BeginRescueReturn()
    {
        WorldObjectiveCompleted = true;
        WorldObjectiveBattleActive = false;
        Phase = OffenseExpeditionPhase.Returning;
    }

    public IReadOnlyList<OffenseRouteNode> GetAvailableRouteNodes()
    {
        return Phase == OffenseExpeditionPhase.ChoosingRoute
            ? Route.GetNextNodes(CurrentNodeId)
            : Array.Empty<OffenseRouteNode>();
    }

    public bool TryEnterNode(string nodeId, out string message)
    {
        if (ReturnPending || Phase != OffenseExpeditionPhase.ChoosingRoute)
        {
            message = "현재는 다음 경로를 선택할 수 없습니다.";
            return false;
        }

        OffenseRouteNode node = Route.GetNextNodes(CurrentNodeId)
            .FirstOrDefault(candidate => string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        if (node == null)
        {
            message = "현재 위치에서 이어지지 않는 경로입니다.";
            return false;
        }

        CurrentNodeId = node.Id;
        if (!TryConsumeSupply(OffenseSupplyType.Rations, 1))
        {
            ApplyStressToSurvivors(6f);
        }

        Light = Mathf.Clamp(Light - (18f + node.DangerMultiplier * 5f), 0f, 100f);
        if (Light <= 20f)
        {
            ApplyStressToSurvivors(4f);
        }

        Phase = node.StartsBattle
            ? OffenseExpeditionPhase.InBattle
            : OffenseExpeditionPhase.ResolvingNode;
        message = $"{node.Title}에 진입했습니다.";
        return true;
    }

    public bool TryResolveCurrentNode(
        bool useSupply,
        out OffenseExpeditionNodeResult result,
        out string message)
    {
        result = null;
        OffenseRouteNode node = CurrentNode;
        if (Phase != OffenseExpeditionPhase.ResolvingNode || node == null)
        {
            message = "선택으로 해결할 원정 노드가 아닙니다.";
            return false;
        }

        bool usedSupply = false;
        bool gainedLoot = false;
        string resultMessage;
        switch (node.Kind)
        {
            case OffenseRouteNodeKind.Event:
                if (useSupply)
                {
                    if (!TryConsumeSupply(OffenseSupplyType.Tools, 1))
                    {
                        message = "사용할 원정 도구가 없습니다.";
                        return false;
                    }

                    usedSupply = true;
                    RecoverStressForSurvivors(5f);
                    AddCarriedStock(StockCategory.General, 3 + Mathf.Max(1, Target?.campaignOrder ?? 1));
                    gainedLoot = true;
                    resultMessage = "도구로 위험을 걷어내고 숨겨진 물자를 챙겼습니다.";
                }
                else
                {
                    ApplyStressToSurvivors(10f);
                    resultMessage = "위험을 감수해 통과했습니다. 원정대의 스트레스가 올랐습니다.";
                }
                break;
            case OffenseRouteNodeKind.Camp:
                if (useSupply)
                {
                    if (!TryConsumeSupply(OffenseSupplyType.Rations, 2))
                    {
                        message = "야영에는 식량 2개가 필요합니다.";
                        return false;
                    }

                    usedSupply = true;
                    foreach (OffenseExpeditionMemberState member in memberStates.Where(value => value.IsAlive))
                    {
                        float beforeHealth = member.Actor.CurrentHealth;
                        member.Actor.Heal(member.Actor.MaxHealth * Preparation.CampHealRatio);
                        RecordHealing(member.Actor, beforeHealth);
                        member.RecoverStress(Preparation.CampStressRecovery);
                    }
                    resultMessage = "야영을 마치고 체력과 스트레스를 회복했습니다.";
                }
                else
                {
                    ApplyStressToSurvivors(5f);
                    resultMessage = "쉬지 않고 전진했습니다.";
                }
                break;
            case OffenseRouteNodeKind.Cache:
                AddCarriedStock(ResolveCacheCategory(), 5 + Mathf.Max(1, Target?.campaignOrder ?? 1) * 2);
                gainedLoot = true;
                resultMessage = "보급고에서 운반할 전리품을 확보했습니다.";
                break;
            default:
                message = "이 노드는 별도 선택으로 해결할 수 없습니다.";
                return false;
        }

        CompleteCurrentNode();
        result = new OffenseExpeditionNodeResult(resultMessage, usedSupply, gainedLoot);
        message = resultMessage;
        return true;
    }

    public bool TryUseSupply(OffenseSupplyType type, int memberIndex, out string message)
    {
        if (Phase != OffenseExpeditionPhase.ChoosingRoute)
        {
            message = "경로를 선택하는 동안에만 보급품을 정비할 수 있습니다.";
            return false;
        }

        switch (type)
        {
            case OffenseSupplyType.Rations:
                if (!TryConsumeSupply(type, 1))
                {
                    message = "식량이 없습니다.";
                    return false;
                }
                RecoverStressForSurvivors(8f);
                message = "식량을 나눠 먹어 스트레스를 낮췄습니다.";
                return true;
            case OffenseSupplyType.Medicine:
                if (memberIndex < 0 || memberIndex >= memberStates.Count || !memberStates[memberIndex].IsAlive)
                {
                    message = "치료할 대원을 선택해야 합니다.";
                    return false;
                }
                if (!TryConsumeSupply(type, 1))
                {
                    message = "치료약이 없습니다.";
                    return false;
                }
                CharacterActor actor = memberStates[memberIndex].Actor;
                float beforeHealth = actor.CurrentHealth;
                actor.Heal(actor.MaxHealth * Preparation.MedicineHealRatio);
                RecordHealing(actor, beforeHealth);
                message = $"{GetMemberName(actor)}을 치료했습니다.";
                return true;
            case OffenseSupplyType.ManaLantern:
                if (!TryConsumeSupply(type, 1))
                {
                    message = "마력등이 없습니다.";
                    return false;
                }
                Light = Mathf.Clamp(Light + 35f, 0f, 100f);
                message = "마력등을 밝혀 시야를 회복했습니다.";
                return true;
            default:
                message = "원정 도구는 사건 현장에서 사용합니다.";
                return false;
        }
    }

    public bool TrySwapFormation(int firstIndex, int secondIndex, out string message)
    {
        if (Phase != OffenseExpeditionPhase.ChoosingRoute
            || firstIndex < 0 || firstIndex >= memberStates.Count
            || secondIndex < 0 || secondIndex >= memberStates.Count
            || firstIndex == secondIndex)
        {
            message = "지금은 해당 진형을 바꿀 수 없습니다.";
            return false;
        }

        OffenseFormationSlot first = memberStates[firstIndex].Formation;
        memberStates[firstIndex].Formation = memberStates[secondIndex].Formation;
        memberStates[secondIndex].Formation = first;
        memberStates.Sort((left, right) => left.Formation.CompareTo(right.Formation));
        members.Clear();
        members.AddRange(memberStates.Select(state => state.Actor));
        message = "원정대 진형을 변경했습니다.";
        return true;
    }

    public void RecordBattleMemberResult(CharacterActor actor, float damageTaken, bool survived)
    {
        OffenseExpeditionMemberState member = memberStates.FirstOrDefault(value => value.Actor == actor);
        if (member == null) return;
        member.RecordDamage(damageTaken);
        member.AddStress(survived ? 8f + damageTaken / Mathf.Max(1f, actor.MaxHealth) * 20f : 100f);
    }

    public void CompleteBattleNode(bool victory)
    {
        OffenseRouteNode node = CurrentNode;
        if (Phase != OffenseExpeditionPhase.InBattle || node == null) return;
        if (!victory)
        {
            Phase = OffenseExpeditionPhase.Defeated;
            return;
        }

        if (node.Kind == OffenseRouteNodeKind.Battle)
        {
            AddCarriedStock(StockCategory.Weapon, 2 + Mathf.Max(1, Target?.campaignOrder ?? 1));
        }

        completedNodeIds.Add(node.Id);
        Phase = node.IsBoss
            ? OffenseExpeditionPhase.Completed
            : OffenseExpeditionPhase.ChoosingRoute;
    }

    public void BeginWorldTravel(string siteId)
    {
        WorldSiteId = siteId ?? string.Empty;
        WorldObjectiveCompleted = false;
        WorldObjectiveBattleActive = false;
        Phase = OffenseExpeditionPhase.Traveling;
    }

    public void MarkDepartureCompleted()
    {
        DepartureCompleted = true;
    }

    public bool RetargetWorldObjective(OffenseTargetDefinition target)
    {
        if (target == null
            || !target.IsValid
            || !UsesWorldTravel
            || Phase is OffenseExpeditionPhase.InBattle
                or OffenseExpeditionPhase.AwaitingDecision
                or OffenseExpeditionPhase.Completed
                or OffenseExpeditionPhase.Defeated
                or OffenseExpeditionPhase.Retreated)
        {
            return false;
        }

        Target = target;
        WorldSiteId = target.id;
        WorldObjectiveCompleted = false;
        WorldObjectiveBattleActive = false;
        Phase = OffenseExpeditionPhase.Traveling;
        return true;
    }

    public void AdjustStress(float amount)
    {
        if (amount >= 0f)
        {
            ApplyStressToSurvivors(amount);
        }
        else
        {
            RecoverStressForSurvivors(-amount);
        }
    }

    public bool ApplyEventInjury(
        float maxHealthRatio,
        int deterministicRoll,
        bool nonLethal)
    {
        CharacterActor[] candidates = memberStates
            .Where(member => member.IsAlive && member.Actor != null)
            .Select(member => member.Actor)
            .OrderBy(actor => actor.Identity?.PersistentId ?? actor.name)
            .ToArray();
        if (candidates.Length == 0)
        {
            return false;
        }

        int index = (int)((uint)deterministicRoll % (uint)candidates.Length);
        CharacterActor victim = candidates[index];
        float damage = victim.MaxHealth * Mathf.Clamp(maxHealthRatio, 0f, 0.95f);
        if (nonLethal)
        {
            damage = Mathf.Min(damage, Mathf.Max(0f, victim.CurrentHealth - 1f));
        }

        if (damage <= 0f)
        {
            return false;
        }

        victim.ApplyDamage(damage, "원정 사건 부상");
        OffenseExpeditionMemberState member = memberStates.FirstOrDefault(
            state => state.Actor == victim);
        member?.RecordDamage(damage);
        return true;
    }

    public bool HealMostInjured(float maxHealthRatio)
    {
        CharacterActor actor = memberStates
            .Where(member => member.IsAlive && member.Actor != null)
            .Select(member => member.Actor)
            .OrderBy(candidate =>
                candidate.CurrentHealth / Mathf.Max(1f, candidate.MaxHealth))
            .ThenBy(candidate =>
                candidate.Identity?.PersistentId ?? candidate.name)
            .FirstOrDefault();
        if (actor == null)
        {
            return false;
        }

        float beforeHealth = actor.CurrentHealth;
        actor.Heal(actor.MaxHealth * Mathf.Clamp01(maxHealthRatio));
        RecordHealing(actor, beforeHealth);
        return true;
    }

    public int GetCarriedStock(StockCategory category)
    {
        return carriedStock.TryGetValue(category, out int amount)
            ? amount
            : 0;
    }

    public void AddCarriedLoot(StockCategory category, int amount)
    {
        AddCarriedStock(category, amount);
    }

    public void AddEncounterReward(string itemId, int amount = 1)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (!string.Equals(
                normalized,
                OffenseLootItemIds.UnappraisedLoot,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Encounter reward '{normalized}' has no physical return mapping.");
        }

        AddCarriedStock(StockCategory.General, Mathf.Max(0, amount));
    }

    public bool AddRecoveredEquipment(string instanceId)
    {
        string normalized = instanceId?.Trim() ?? string.Empty;
        return normalized.Length > 0 && recoveredEquipmentInstanceIds.Add(normalized);
    }

    public bool TryRemoveCarriedLoot(StockCategory category, int amount)
    {
        int removal = Mathf.Max(0, amount);
        if (removal == 0)
        {
            return true;
        }

        int current = GetCarriedStock(category);
        if (current < removal)
        {
            return false;
        }

        int remaining = current - removal;
        if (remaining > 0)
        {
            carriedStock[category] = remaining;
        }
        else
        {
            carriedStock.Remove(category);
        }

        return true;
    }

    public bool CanSpendFieldFunds(int amount)
    {
        return FieldFunds >= Mathf.Max(0, amount);
    }

    public bool TrySpendFieldFunds(int amount)
    {
        int cost = Mathf.Max(0, amount);
        if (FieldFunds < cost)
        {
            return false;
        }

        FieldFunds -= cost;
        return true;
    }

    public void AddFieldFunds(int amount)
    {
        FieldFunds += Mathf.Max(0, amount);
    }

    public int TakeReturningFieldFunds()
    {
        if (FieldFundsReturned)
        {
            return 0;
        }

        FieldFundsReturned = true;
        int returning = Mathf.Max(0, FieldFunds);
        FieldFunds = 0;
        return returning;
    }

    public int PeekReturningFieldFunds() => FieldFundsReturned
        ? 0
        : Mathf.Max(0, FieldFunds);

    public void CompleteReturningFieldFunds(int committedAmount)
    {
        int expected = PeekReturningFieldFunds();
        if (committedAmount <= 0 || committedAmount != expected)
        {
            throw new InvalidOperationException(
                "Expedition field-fund return receipt does not match its owned balance.");
        }
        FieldFundsReturned = true;
        FieldFunds = 0;
    }

    public void RestoreFieldFunds(int amount, bool returned)
    {
        FieldFunds = Mathf.Max(0, amount);
        FieldFundsReturned = returned;
    }

    public bool BeginWorldBattle(bool objectiveBattle = true)
    {
        if (!UsesWorldTravel
            || Phase is OffenseExpeditionPhase.Completed
                or OffenseExpeditionPhase.Defeated
                or OffenseExpeditionPhase.Retreated)
        {
            return false;
        }

        WorldObjectiveBattleActive = objectiveBattle;
        Phase = OffenseExpeditionPhase.InBattle;
        return true;
    }

    public void CompleteWorldBattle(bool victory)
    {
        if (!UsesWorldTravel || Phase != OffenseExpeditionPhase.InBattle)
        {
            return;
        }

        if (!victory)
        {
            WorldObjectiveBattleActive = false;
            Phase = OffenseExpeditionPhase.Defeated;
            return;
        }

        if (WorldObjectiveBattleActive)
        {
            WorldObjectiveCompleted = true;
            Phase = OffenseExpeditionPhase.Returning;
        }
        else
        {
            Phase = WorldObjectiveCompleted
                ? OffenseExpeditionPhase.Returning
                : OffenseExpeditionPhase.Traveling;
        }

        WorldObjectiveBattleActive = false;
    }

    public void SetDecisionPaused(bool paused)
    {
        if (!UsesWorldTravel || IsComplete || Phase == OffenseExpeditionPhase.InBattle)
        {
            return;
        }

        Phase = paused
            ? OffenseExpeditionPhase.AwaitingDecision
            : WorldObjectiveCompleted
                ? OffenseExpeditionPhase.Returning
                : OffenseExpeditionPhase.Traveling;
    }

    public bool Retreat(out string message)
    {
        if (IsComplete)
        {
            message = "이미 끝난 원정입니다.";
            return false;
        }

        Phase = OffenseExpeditionPhase.Retreated;
        message = "운반 중인 전리품을 지키며 던전으로 철수합니다.";
        return true;
    }

    internal void Tick(float deltaTime)
    {
        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Mathf.Max(0f, deltaTime));
    }

    public void RestoreJourneyState(
        OffenseExpeditionPhase phase,
        string currentNodeId,
        float light,
        IEnumerable<string> completedNodes,
        IReadOnlyDictionary<StockCategory, int> restoredCarriedStock)
    {
        if (Route.TryGetNode(currentNodeId, out _))
        {
            CurrentNodeId = currentNodeId;
        }

        Phase = phase;
        Light = Mathf.Clamp(light, 0f, 100f);
        completedNodeIds.Clear();
        completedNodeIds.Add(Route.EntranceNodeId);
        foreach (string nodeId in completedNodes ?? Array.Empty<string>())
        {
            if (Route.TryGetNode(nodeId, out _)) completedNodeIds.Add(nodeId);
        }

        carriedStock.Clear();
        if (restoredCarriedStock == null) return;
        foreach (KeyValuePair<StockCategory, int> pair in restoredCarriedStock)
        {
            if (pair.Value > 0) carriedStock[pair.Key] = pair.Value;
        }
    }

    public void RestoreRecoveredEquipment(IEnumerable<string> instanceIds)
    {
        recoveredEquipmentInstanceIds.Clear();
        foreach (string instanceId in instanceIds ?? Array.Empty<string>())
        {
            AddRecoveredEquipment(instanceId);
        }
    }

    public void RestoreStrategicJourneyState(
        string siteId,
        bool objectiveCompleted,
        bool objectiveBattleActive,
        OffenseExpeditionPhase phase)
    {
        if (string.IsNullOrWhiteSpace(siteId))
        {
            return;
        }

        WorldSiteId = siteId;
        WorldObjectiveCompleted = objectiveCompleted;
        WorldObjectiveBattleActive = objectiveBattleActive
            && phase == OffenseExpeditionPhase.InBattle;
        Phase = phase;
    }

    public void RestoreDepartureState(bool completed)
    {
        DepartureCompleted = completed;
    }

    private void CompleteCurrentNode()
    {
        completedNodeIds.Add(CurrentNodeId);
        Phase = OffenseExpeditionPhase.ChoosingRoute;
    }

    private void ApplyStressToSurvivors(float amount)
    {
        foreach (OffenseExpeditionMemberState member in memberStates.Where(value => value.IsAlive))
        {
            member.AddStress(amount);
        }
    }

    private void RecoverStressForSurvivors(float amount)
    {
        foreach (OffenseExpeditionMemberState member in memberStates.Where(value => value.IsAlive))
        {
            member.RecoverStress(amount);
        }
    }

    private void AddCarriedStock(StockCategory category, int amount)
    {
        if (amount <= 0) return;
        carriedStock[category] = carriedStock.TryGetValue(category, out int current)
            ? current + amount
            : amount;
    }

    private StockCategory ResolveCacheCategory()
    {
        int categoryIndex = Mathf.Max(1, Target?.campaignOrder ?? 1) % 3;
        return categoryIndex switch
        {
            0 => StockCategory.Mana,
            1 => StockCategory.Food,
            _ => StockCategory.General
        };
    }

    private static string GetMemberName(CharacterActor actor)
    {
        actor?.EnsureRuntimeState();
        return actor != null && actor.Identity != null
            ? actor.Identity.DisplayName
            : actor != null ? actor.name : "대원";
    }
}
