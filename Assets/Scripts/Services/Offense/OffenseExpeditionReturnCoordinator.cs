using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;

public sealed class OffenseExpeditionReturnReceipt
{
    public OffenseExpeditionReturnReceipt(
        IReadOnlyList<OffenseExpeditionItemReceipt> itemReceipts,
        IReadOnlyList<OffenseExpeditionCurrencyReceipt> currencyReceipts = null)
    {
        ItemReceipts = itemReceipts ?? Array.Empty<OffenseExpeditionItemReceipt>();
        CurrencyReceipts = currencyReceipts
            ?? Array.Empty<OffenseExpeditionCurrencyReceipt>();
    }

    public IReadOnlyList<OffenseExpeditionItemReceipt> ItemReceipts { get; }
    public IReadOnlyList<OffenseExpeditionCurrencyReceipt> CurrencyReceipts { get; }
}

public interface IOffenseExpeditionReturnPort
{
    void Begin(string expeditionId);
    void ReleaseResources(OffenseExpeditionRun expedition, bool hasSurvivor);
    bool TryBeginMemberReturn(
        string expeditionId,
        CharacterActor actor,
        Action<ExpeditionReturnOutcome> completed,
        ExpeditionReturnProgress progress = null);
    void EndMemberImmediately(CharacterActor actor, bool survived);
    void HandleMemberDeath(CharacterActor actor);
    void Seal(string expeditionId);
}

public interface IOffenseExpeditionReturnSettlementPort
{
    OffenseExpeditionReturnReceipt ReleaseResourcesWithReceipt(
        OffenseExpeditionRun expedition,
        bool hasSurvivor);
}

public interface IOffenseExpeditionReturnCoordinator
{
    void Complete(
        OffenseExpeditionRun expedition,
        bool success,
        string message,
        List<OffenseExpeditionResult> resultHistory,
        Action stateChanged);
}

public sealed class OffenseExpeditionReturnPort :
    IOffenseExpeditionReturnPort,
    IOffenseExpeditionReturnSettlementPort
{
    private readonly IOffensePreparationService preparation;
    private readonly IExpeditionReturnService returnService;
    private readonly IOffenseReturnArrivalRuntime arrivals;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IGameMoneyAccount money;
    private readonly IGameEventBus events;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IV27EmbeddedWorkValueProjectionQuery workValues;

    public OffenseExpeditionReturnPort(
        IOffensePreparationService preparation,
        IExpeditionReturnService returnService,
        IOffenseReturnArrivalRuntime arrivals,
        ICombatEquipmentRuntime equipment,
        IGameMoneyAccount money,
        IGameEventBus events,
        IWorldDropZoneQuery dropZones,
        IV27EmbeddedWorkValueProjectionQuery workValues)
    {
        this.preparation = preparation
            ?? throw new ArgumentNullException(nameof(preparation));
        this.returnService = returnService
            ?? throw new ArgumentNullException(nameof(returnService));
        this.arrivals = arrivals
            ?? throw new ArgumentNullException(nameof(arrivals));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.money = money ?? throw new ArgumentNullException(nameof(money));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.dropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        this.workValues = workValues
            ?? throw new ArgumentNullException(nameof(workValues));
    }

    public void Begin(string expeditionId)
    {
        arrivals.BeginExpeditionReturn(expeditionId);
    }

    public void ReleaseResources(
        OffenseExpeditionRun expedition,
        bool hasSurvivor)
    {
        ReleaseResourcesWithReceipt(expedition, hasSurvivor);
    }

    public OffenseExpeditionReturnReceipt ReleaseResourcesWithReceipt(
        OffenseExpeditionRun expedition,
        bool hasSurvivor)
    {
        if (expedition == null)
        {
            throw new ArgumentNullException(nameof(expedition));
        }
        if (expedition.ReturnResourcesCommitted)
        {
            return expedition.GetReturnResourceReceipt();
        }
        if (!hasSurvivor)
        {
            preparation.AbandonPackedSupplies(expedition.ExpeditionId);
            expedition.CompleteReturnResources();
            return expedition.GetReturnResourceReceipt();
        }

        if (preparation is IOffensePreparationSettlementPort settlement)
        {
            IReadOnlyList<OffenseExpeditionItemReceipt> committedSupplies =
                GetCommittedStageReceipts(
                    expedition,
                    OffenseExpeditionItemReceiptKind.SupplyReturned);
            IReadOnlyDictionary<string, int> expectedSupplies =
                BuildExpectedSupplyReturns(expedition.Supplies);
            if (committedSupplies.Count > 0)
            {
                ValidateCommittedStageReceipts(
                    committedSupplies,
                    expectedSupplies,
                    "supply");
            }
            else
            {
                OffenseExpeditionItemReceipt[] publishedSupplies = settlement
                    .ReturnSuppliesWithReceipt(
                        expedition.Supplies,
                        expedition.ExpeditionId)
                    .Select(value => CreateItemReceipt(
                        OffenseExpeditionItemReceiptKind.SupplyReturned,
                        value))
                    .ToArray();
                ValidateCommittedStageReceipts(
                    publishedSupplies,
                    expectedSupplies,
                    "supply");
                expedition.RecordReturnItemReceipts(publishedSupplies);
            }

            IReadOnlyList<OffenseExpeditionItemReceipt> committedLoot =
                GetCommittedStageReceipts(
                    expedition,
                    OffenseExpeditionItemReceiptKind.LootRecovered);
            IReadOnlyDictionary<string, int> expectedLoot =
                BuildExpectedLootReturns(expedition.CarriedStock);
            if (committedLoot.Count > 0)
            {
                ValidateCommittedStageReceipts(
                    committedLoot,
                    expectedLoot,
                    "loot");
            }
            else
            {
                OffenseExpeditionItemReceipt[] publishedLoot = settlement
                    .DepositLootWithReceipt(
                        expedition.CarriedStock,
                        expedition.ExpeditionId)
                    .Select(value => CreateItemReceipt(
                        OffenseExpeditionItemReceiptKind.LootRecovered,
                        value))
                    .ToArray();
                ValidateCommittedStageReceipts(
                    publishedLoot,
                    expectedLoot,
                    "loot");
                expedition.RecordReturnItemReceipts(publishedLoot);
            }
        }
        else
        {
            preparation.ReturnSupplies(
                expedition.Supplies,
                expedition.ExpeditionId);
            preparation.DepositLoot(expedition.CarriedStock);
        }
        MaterializeRecoveredEquipment(expedition);
        int returningFunds = expedition.PeekReturningFieldFunds();
        if (returningFunds > 0)
        {
            EconomyTransactionContext transaction =
                new EconomyTransactionContext(
                    EconomyTransactionKind.ExpeditionFieldFundReturn,
                    expedition.ExpeditionId,
                    expedition.Target?.id,
                    "expedition-field-fund-return");
            string failure = money is IIdempotentGameMoneyAccount
                ? string.Empty
                : "idempotent money authority unavailable";
            if (money is not IIdempotentGameMoneyAccount idempotent
                || !idempotent.TryCreditOnce(
                    returningFunds,
                    transaction,
                    out failure))
            {
                throw new InvalidOperationException(
                    "Expedition field-fund return failed: " + failure);
            }
            expedition.CompleteReturningFieldFunds(returningFunds);
            expedition.RecordReturnCurrencyReceipts(new[]
            {
                new OffenseExpeditionCurrencyReceipt(
                    OffenseSettlementCurrencyIds.Gold,
                    returningFunds,
                    expedition.ExpeditionId)
            });
        }

        expedition.CompleteReturnResources();
        events.RaiseAlert(
            "expedition-cargo-arrived",
            "expedition-cargo-unloaded",
            EventAlertImportance.Low,
            "offense");
        return expedition.GetReturnResourceReceipt();
    }

    private static IReadOnlyList<OffenseExpeditionItemReceipt>
        GetCommittedStageReceipts(
            OffenseExpeditionRun expedition,
            OffenseExpeditionItemReceiptKind kind) =>
        expedition.ReturnItemReceipts
            .Where(value => value.kind == kind)
            .OrderBy(value => value.itemId, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyDictionary<string, int> BuildExpectedSupplyReturns(
        OffenseSupplyLoadout loadout)
    {
        Dictionary<string, int> expected = new(StringComparer.Ordinal);
        foreach (KeyValuePair<OffenseSupplyType, int> pair in
                 loadout?.Amounts
                 ?? new Dictionary<OffenseSupplyType, int>())
        {
            if (pair.Value <= 0) continue;
            string itemId = OffenseSupplyCatalog.GetPhysicalItemId(pair.Key);
            expected.TryGetValue(itemId, out int current);
            expected[itemId] = checked(current + pair.Value);
        }

        return expected;
    }

    private static IReadOnlyDictionary<string, int> BuildExpectedLootReturns(
        IReadOnlyDictionary<StockCategory, int> carriedStock)
    {
        int total = 0;
        foreach (int amount in carriedStock?.Values ?? Array.Empty<int>())
        {
            total = checked(total + Mathf.Max(0, amount));
        }

        return total == 0
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [OffenseLootItemIds.UnappraisedLoot] = total
            };
    }

    private static void ValidateCommittedStageReceipts(
        IReadOnlyList<OffenseExpeditionItemReceipt> receipts,
        IReadOnlyDictionary<string, int> expected,
        string stage)
    {
        Dictionary<string, int> actual = new(StringComparer.Ordinal);
        foreach (OffenseExpeditionItemReceipt receipt in receipts
                     ?? Array.Empty<OffenseExpeditionItemReceipt>())
        {
            if (receipt == null
                || string.IsNullOrEmpty(receipt.itemId)
                || !string.IsNullOrEmpty(receipt.instanceId)
                || receipt.quantity <= 0
                || actual.ContainsKey(receipt.itemId))
            {
                throw new InvalidOperationException(
                    $"Expedition return {stage} receipt shape changed after physical commit.");
            }

            actual.Add(receipt.itemId, receipt.quantity);
        }

        if (actual.Count != expected.Count
            || expected.Any(pair =>
                !actual.TryGetValue(pair.Key, out int quantity)
                || quantity != pair.Value))
        {
            throw new InvalidOperationException(
                $"Expedition return {stage} receipts conflict with the owned physical quantities.");
        }
    }

    private OffenseExpeditionItemReceipt CreateItemReceipt(
        OffenseExpeditionItemReceiptKind kind,
        OffensePhysicalItemCommitReceipt receipt) =>
        new OffenseExpeditionItemReceipt(
            kind,
            receipt.ItemId,
            receipt.Quantity,
            string.Empty,
            0f,
            OffenseExpeditionRun.CreateValuation(
                workValues,
                receipt.ItemId,
                receipt.Quantity));

    private void MaterializeRecoveredEquipment(
        OffenseExpeditionRun expedition)
    {
        if (expedition.RecoveredEquipmentInstanceIds.Count == 0)
        {
            return;
        }
        HashSet<string> alreadyCommitted = expedition.ReturnItemReceipts
            .Where(value => value.kind
                == OffenseExpeditionItemReceiptKind.EquipmentRecovered)
            .Select(value => value.instanceId)
            .ToHashSet(StringComparer.Ordinal);
        string[] pending = expedition.RecoveredEquipmentInstanceIds
            .Where(value => !alreadyCommitted.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (pending.Length == 0)
        {
            return;
        }
        if (!dropZones.TryGetExpeditionLootDropoff(out Vector2Int dropoff))
        {
            throw new InvalidOperationException(
                "전리품 하역 지점을 찾지 못해 회수 장비가 대기 중입니다.");
        }

        List<string> failures = new List<string>();
        foreach (string instanceId in pending)
        {
            if (!equipment.TryGetInstance(
                    instanceId,
                    out CombatEquipmentInstance recovered)
                || !equipment.TryGetDefinition(
                    recovered.definitionId,
                    out CombatEquipmentDefinitionSO definition))
            {
                failures.Add($"{instanceId}: 장비 정의를 찾지 못했습니다.");
                continue;
            }

            OffenseItemValuationSnapshot valuation = CreateValuation(
                definition.ItemId,
                1);
            if (!equipment.TryMaterializeRecoveredEquipment(
                    instanceId,
                    dropoff,
                    out string failure))
            {
                failures.Add($"{instanceId}: {failure}");
                continue;
            }
            expedition.RecordReturnItemReceipts(new[]
            {
                new OffenseExpeditionItemReceipt(
                    OffenseExpeditionItemReceiptKind.EquipmentRecovered,
                    definition.ItemId,
                    1,
                    instanceId,
                    0f,
                    valuation)
            });
        }
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "원정 장비 하역 실패: " + string.Join(" | ", failures));
        }
    }

    private OffenseItemValuationSnapshot CreateValuation(
        string itemId,
        int quantity)
    {
        if (!workValues.AuthorityAvailable)
            return new OffenseItemValuationSnapshot(
                itemId,
                quantity,
                OffenseSettlementValuationState.AuthorityUnavailable);
        if (!workValues.TryGet(itemId, out V27EmbeddedWorkValueProjection value))
            return new OffenseItemValuationSnapshot(
                itemId,
                quantity,
                OffenseSettlementValuationState.UnvaluedItem);
        return new OffenseItemValuationSnapshot(
            itemId,
            quantity,
            OffenseSettlementValuationState.Valued,
            value.AcquisitionMilliEwu,
            value.RecoverableMilliEwu,
            value.BasisId,
            value.SelectedSourceId);
    }

    public bool TryBeginMemberReturn(
        string expeditionId,
        CharacterActor actor,
        Action<ExpeditionReturnOutcome> completed,
        ExpeditionReturnProgress progress = null)
    {
        arrivals.RegisterReturningMember(expeditionId);
        bool settled = false;
        bool started = returnService.TryBeginReturn(
            actor,
            true,
            outcome =>
            {
                if (settled) return;
                settled = true;
                if (outcome == ExpeditionReturnOutcome.Dead) HandleMemberDeath(actor);
                arrivals.CompleteReturningMember(expeditionId);
                completed?.Invoke(outcome);
            },
            out _, progress);
        if (!started)
        {
            arrivals.CompleteReturningMember(expeditionId);
        }

        return started;
    }

    public void EndMemberImmediately(CharacterActor actor, bool survived)
    {
        actor?.EndExpedition(survived);
    }

    public void HandleMemberDeath(CharacterActor actor)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        actor.EnsureRuntimeState();
        equipment.HandleCharacterDeath(
            CharacterPersistentIdentity.Require(actor).Value);
    }

    public void Seal(string expeditionId)
    {
        arrivals.SealExpeditionReturn(expeditionId);
    }
}

/// <summary>
/// Explicit capability for isolated aggregate tests that do not compose the
/// exterior return presentation. It never stores state and is safe to share.
/// </summary>
public sealed class NoOpOffenseExpeditionReturnPort :
    IOffenseExpeditionReturnPort
{
    public static readonly NoOpOffenseExpeditionReturnPort Instance = new();

    private NoOpOffenseExpeditionReturnPort()
    {
    }

    public void Begin(string expeditionId)
    {
    }

    public void ReleaseResources(OffenseExpeditionRun expedition, bool hasSurvivor)
    {
    }

    public bool TryBeginMemberReturn(
        string expeditionId,
        CharacterActor actor,
        Action<ExpeditionReturnOutcome> completed,
        ExpeditionReturnProgress progress = null)
    {
        if (actor == null) return false;
        actor.EndExpedition(!actor.IsDead);
        completed?.Invoke(actor.IsDead ? ExpeditionReturnOutcome.Dead : ExpeditionReturnOutcome.Arrived);
        return true;
    }

    public void EndMemberImmediately(CharacterActor actor, bool survived)
    {
        actor?.EndExpedition(survived);
    }

    public void HandleMemberDeath(CharacterActor actor)
    {
    }

    public void Seal(string expeditionId)
    {
    }
}

public sealed class OffenseExpeditionReturnCoordinator :
    IOffenseExpeditionReturnCoordinator
{
    private readonly IOffenseExpeditionReturnPort returnPort;
    private readonly IOffenseExpeditionResultFinalizer resultFinalizer;
    private readonly IGameEventBus events;
    private readonly ICharacterPerformanceQuery performance;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IV27EmbeddedWorkValueProjectionQuery workValues;

    [Inject]
    public OffenseExpeditionReturnCoordinator(
        IOffenseExpeditionReturnPort returnPort,
        IOffenseExpeditionResultFinalizer resultFinalizer,
        IGameEventBus events,
        ICharacterPerformanceQuery performance,
        ICombatEquipmentRuntime equipment,
        IV27EmbeddedWorkValueProjectionQuery workValues)
    {
        this.returnPort = returnPort
            ?? throw new ArgumentNullException(nameof(returnPort));
        this.resultFinalizer = resultFinalizer
            ?? throw new ArgumentNullException(nameof(resultFinalizer));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.workValues = workValues
            ?? throw new ArgumentNullException(nameof(workValues));
    }

    public OffenseExpeditionReturnCoordinator(
        IOffenseExpeditionReturnPort returnPort,
        IOffenseExpeditionResultFinalizer resultFinalizer,
        IGameEventBus events,
        ICharacterPerformanceQuery performance = null)
    {
        this.returnPort = returnPort
            ?? throw new ArgumentNullException(nameof(returnPort));
        this.resultFinalizer = resultFinalizer
            ?? throw new ArgumentNullException(nameof(resultFinalizer));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.performance = performance
            ?? throw new ArgumentNullException(nameof(performance));
    }

    public void Complete(
        OffenseExpeditionRun expedition,
        bool success,
        string message,
        List<OffenseExpeditionResult> resultHistory,
        Action stateChanged)
    {
        if (expedition == null)
        {
            return;
        }

        if (expedition.ReturnFinalized || expedition.ReturnFinalizing) return;
        if (expedition.ReturnPending && expedition.ReturnProgress.Any(value => value.InFlight)
            && expedition.ReturnProgress.All(value => value.InFlight || value.IsTerminal)) return;
        expedition.BeginPhysicalReturn(success, message);
        success = expedition.ReturnSuccess;
        message = expedition.ReturnMessage;
        if (!expedition.ReturnBarrierInitialized)
        {
            returnPort.Begin(expedition.ExpeditionId);
            expedition.ReturnBarrierInitialized = true;
        }
        var participants = expedition.MemberStates.Select(member =>
            (actor: member.Actor, stress: member.Stress, damage: member.TotalDamageTaken, awardExperience: true))
            .Concat(expedition.ProtectedRescueActors.Select(actor =>
                (actor, stress: 0f, damage: 0f, awardExperience: false))).ToArray();
        bool resourceAttemptFailed = false;

        void ResolveIfReady()
        {
            if (expedition.ReturnFinalized || expedition.ReturnFinalizing
                || resourceAttemptFailed
                || expedition.ReturnProgress.Any(progress => !progress.IsTerminal))
            {
                return;
            }

            bool hasSurvivor = participants.Any(participant =>
                participant.actor != null && !participant.actor.IsDead);
            OffenseExpeditionReturnReceipt release;
            IReadOnlyList<OffenseExpeditionItemReceipt> frozenItems;
            int itemReceiptCountBefore = expedition.ReturnItemReceipts.Count;
            int currencyReceiptCountBefore =
                expedition.ReturnCurrencyReceipts.Count;
            bool fieldFundsReturnedBefore = expedition.FieldFundsReturned;
            try
            {
                frozenItems = expedition.FreezeItemReceipts(
                    equipment,
                    workValues);
                if (expedition.ReturnResourcesCommitted)
                {
                    release = expedition.GetReturnResourceReceipt();
                }
                else if (returnPort is
                         IOffenseExpeditionReturnSettlementPort settlementPort)
                {
                    release = settlementPort.ReleaseResourcesWithReceipt(
                        expedition,
                        hasSurvivor);
                    expedition.RecordReturnItemReceipts(release?.ItemReceipts);
                    expedition.RecordReturnCurrencyReceipts(
                        release?.CurrencyReceipts);
                    expedition.CompleteReturnResources();
                    release = expedition.GetReturnResourceReceipt();
                }
                else
                {
                    returnPort.ReleaseResources(expedition, hasSurvivor);
                    expedition.CompleteReturnResources();
                    release = expedition.GetReturnResourceReceipt();
                }
            }
            catch (InvalidOperationException ex)
            {
                resourceAttemptFailed = true;
                bool failureChanged =
                    expedition.MarkReturnResourceFailure(ex.Message);
                if (failureChanged)
                {
                    events.RaiseAlert(
                        "원정 귀환 물자 정산 대기",
                        expedition.ReturnResourceFailure,
                        EventAlertImportance.High,
                        "offense");
                }
                if (failureChanged
                    || expedition.ReturnItemReceipts.Count
                        != itemReceiptCountBefore
                    || expedition.ReturnCurrencyReceipts.Count
                        != currencyReceiptCountBefore
                    || expedition.FieldFundsReturned
                        != fieldFundsReturnedBefore)
                {
                    stateChanged?.Invoke();
                }
                return;
            }

            expedition.ReturnFinalizing = true;
            List<OffenseExpeditionMemberSnapshot> members = new();
            foreach (var participant in participants)
            {
                var actor = participant.actor;
                bool survived = actor != null && !actor.IsDead;
                actor?.Lifecycle?.RecordExpeditionReturn(participant.stress, survived);
                if (success && survived && participant.awardExperience)
                    actor.Progression?.AddExperience(
                        OffenseExpeditionRuntime.CalculateSuccessfulReturnExperience(expedition));
                actor?.EnsureRuntimeState();
                members.Add(new OffenseExpeditionMemberSnapshot(
                    actor != null ? actor.Identity?.DisplayName ?? actor.name : string.Empty,
                    actor != null ? actor.Identity?.SpeciesTag ?? string.Empty : string.Empty,
                    actor != null ? OffenseExpeditionService.CalculateMemberPower(actor, performance) : 0f,
                    survived, participant.damage));
            }
            var pendingResult = new OffenseExpeditionResult(
                expedition.ExpeditionId, expedition.Target.id, expedition.Target.title, success,
                expedition.TotalPower, expedition.Target.requiredPower, expedition.Target.danger,
                expedition.TotalDurationSeconds - expedition.RemainingSeconds, members,
                success ? expedition.Target.rewards?.Where(reward => reward != null)
                    .Select(reward => reward.ToSummaryText()).ToArray() ?? Array.Empty<string>() : Array.Empty<string>());
            pendingResult = pendingResult.WithSettlement(
                frozenItems,
                expedition.TreatmentReceipts);
            pendingResult = pendingResult
                .WithAdditionalItemReceipts(release?.ItemReceipts)
                .WithAdditionalCurrencyReceipts(release?.CurrencyReceipts);
            pendingResult = resultFinalizer.Finalize(
                expedition,
                pendingResult,
                resultHistory);
            returnPort.Seal(expedition.ExpeditionId);
            expedition.ReturnFinalized = true;
            expedition.ReturnFinalizing = false;
            stateChanged?.Invoke();
            if (!string.IsNullOrWhiteSpace(message))
            {
                events.RaiseAlert(
                    success ? "expedition-returned" : "expedition-ended",
                    message,
                    success
                        ? EventAlertImportance.Medium
                        : EventAlertImportance.High,
                    "offense");
            }
        }

        foreach (var participant in participants)
        {
            var actor = participant.actor;
            if (actor == null) continue;
            var progress = expedition.ReturnProgress.Single(value =>
                value.CharacterId == CharacterPersistentIdentity.Require(actor).Value);
            if (progress.IsTerminal || progress.InFlight) continue;
            if (actor.Stats?.IsDead == true)
            {
                returnPort.EndMemberImmediately(actor, false);
                returnPort.HandleMemberDeath(actor);
                progress.Stage = ExpeditionReturnStage.Dead;
                continue;
            }
            progress.InFlight = true;
            bool started = returnPort.TryBeginMemberReturn(expedition.ExpeditionId, actor, outcome =>
            {
                progress.InFlight = false;
                progress.Stage = outcome switch
                {
                    ExpeditionReturnOutcome.Arrived => ExpeditionReturnStage.Arrived,
                    ExpeditionReturnOutcome.RequiresRescue => ExpeditionReturnStage.RequiresRescue,
                    ExpeditionReturnOutcome.Dead => ExpeditionReturnStage.Dead,
                    _ => progress.Stage
                };
                progress.LastFailure = outcome == ExpeditionReturnOutcome.Interrupted ? "return-lifecycle-interrupted" : string.Empty;
                ResolveIfReady();
                stateChanged?.Invoke();
            }, progress);
            if (!started)
            {
                progress.InFlight = false;
                if (progress.LastFailure != "return-start-unavailable")
                {
                    progress.LastFailure = "return-start-unavailable";
                    events.RaiseAlert("원정 귀환 대기",
                        "귀환 이동을 시작하지 못했습니다: " + actor.Identity?.PersistentId,
                        EventAlertImportance.High, "offense");
                    stateChanged?.Invoke();
                }
            }
            else if (progress.InFlight && progress.LastFailure.Length > 0)
            {
                progress.LastFailure = string.Empty;
                stateChanged?.Invoke();
            }
        }
        ResolveIfReady();
    }
}
