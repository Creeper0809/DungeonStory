using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using VContainer.Unity;

public interface ITemporalStasisMaintenanceService
{
    void RefreshDailyMaintenance();
}

public interface ITemporalStasisMaintenanceRecovery
{
    bool TryRecoverPending(out DomainFailure failure);
}

/// <summary>
/// Maintains already-assigned temporal-stasis care. Age-treatment activation
/// and regeneration are owned by the surgery workflow.
/// </summary>
public sealed class PhysicalAgeTreatmentRuntime :
    ITemporalStasisMaintenanceService,
    ITemporalStasisMaintenanceRecovery
{
    public const string RuneConductorItemId = "component:rune-conductor";
    public const string ManaCrystalItemId = "resource:mana-crystal";
    public const float RequiredRunePower = 10f;
    public const string DispositionReasonCode =
        "temporal-stasis-seasonal-maintenance";

    private readonly IPhysicalFacilityItemBatchSinkGateway physicalItems;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterLifeCommand lifeCommands;
    private readonly ICharacterLifePersistence persistence;
    private readonly IGameCalendar calendar;
    private readonly IBuildingWorldQuery buildingWorld;
    private readonly IPowerInfrastructureQuery power;

    public PhysicalAgeTreatmentRuntime(
        IPhysicalFacilityItemBatchSinkGateway physicalItems,
        ICharacterLifeQuery life,
        ICharacterLifeCommand lifeCommands,
        ICharacterLifePersistence persistence,
        IGameCalendar calendar,
        IBuildingWorldQuery buildingWorld,
        IPowerInfrastructureQuery power)
    {
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.lifeCommands = lifeCommands
            ?? throw new ArgumentNullException(nameof(lifeCommands));
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.calendar = calendar
            ?? throw new ArgumentNullException(nameof(calendar));
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
        this.power = power ?? throw new ArgumentNullException(nameof(power));
    }

    public void RefreshDailyMaintenance()
    {
        if (!TryRecoverPending(out DomainFailure recoveryFailure))
        {
            throw new InvalidOperationException(
                "Temporal-stasis maintenance recovery failed: "
                + recoveryFailure);
        }

        CharacterLifeRecord[] assignments = life.Records
            .Where(value => value.RequestedAgingCareMode
                == AgingCareMode.TemporalStasis)
            .OrderBy(value => value.CharacterId.Value, StringComparer.Ordinal)
            .ToArray();
        foreach (CharacterLifeRecord record in assignments)
        {
            string facilityId = record.TemporalStasisFacilityId;
            BuildableObject facility = FindFacility(facilityId);
            bool operational = facility != null && HasRequiredPower(facility);
            int nextMaintenance =
                record.TemporalStasisNextMaintenanceAbsoluteDay;
            if (operational && calendar.Day >= nextMaintenance)
            {
                if (TryPerformMaintenance(record, out DomainFailure failure))
                {
                    continue;
                }
                if (failure.Code != FailureCode.TemporalStasisMaintenanceUnavailable)
                {
                    throw new InvalidOperationException(
                        "Temporal-stasis maintenance failed: " + failure);
                }
                operational = false;
            }

            lifeCommands.ConfigureTemporalStasis(
                record.CharacterId,
                facilityId,
                operational,
                nextMaintenance);
        }
    }

    public bool TryRecoverPending(out DomainFailure failure) =>
        TryRecoverPendingCore(out _, out failure);

    public static string FormatOperationId(
        CharacterId characterId,
        int sequence) =>
        $"temporal-stasis-maintenance:{characterId.Value}:{sequence:D8}";

    private bool TryPerformMaintenance(
        CharacterLifeRecord record,
        out DomainFailure failure)
    {
        if (!TryRecoverPendingCore(out _, out failure))
        {
            return false;
        }

        CharacterLifeWorldSaveData intent = persistence.Capture();
        int sequence = intent.nextTemporalStasisMaintenanceOperationSequence;
        string operationId = FormatOperationId(record.CharacterId, sequence);
        int afterDay = checked(calendar.Day + GameCalendarRules.DaysPerSeason);
        intent.pendingTemporalStasisMaintenance =
            new TemporalStasisMaintenanceCommitSaveData
            {
                phase = (int)TemporalStasisMaintenanceCommitPhase.IntentRecorded,
                operationSequence = sequence,
                operationId = operationId,
                reasonCode = DispositionReasonCode,
                characterId = record.CharacterId.Value,
                facilityInstanceId = record.TemporalStasisFacilityId,
                runeConductorItemId = RuneConductorItemId,
                runeConductorQuantity = 1,
                manaCrystalItemId = ManaCrystalItemId,
                manaCrystalQuantity = 1,
                nextMaintenanceBeforeAbsoluteDay =
                    record.TemporalStasisNextMaintenanceAbsoluteDay,
                nextMaintenanceAfterAbsoluteDay = afterDay
            };
        try
        {
            persistence.PublishRestore(persistence.PrepareRestore(intent));
        }
        catch (Exception exception)
        {
            failure = new DomainFailure(
                FailureCode.TemporalStasisMaintenanceUnavailable,
                operationId,
                "maintenance-intent-rejected",
                exception.GetType().Name);
            return false;
        }

        if (!physicalItems.TryCommitSinkPending(
                record.TemporalStasisFacilityId,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [RuneConductorItemId] = 1,
                    [ManaCrystalItemId] = 1
                },
                operationId,
                DispositionReasonCode,
                out _,
                out string consumeFailure))
        {
            TryClearUncommittedIntent(operationId);
            failure = new DomainFailure(
                FailureCode.TemporalStasisMaintenanceUnavailable,
                record.TemporalStasisFacilityId,
                RuneConductorItemId,
                ManaCrystalItemId,
                consumeFailure ?? string.Empty);
            return false;
        }

        if (!TryRecoverPendingCore(out bool completed, out failure)
            || !completed)
        {
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    private bool TryRecoverPendingCore(
        out bool completedOperation,
        out DomainFailure failure)
    {
        completedOperation = false;
        CharacterLifeWorldSaveData captured = persistence.Capture();
        TemporalStasisMaintenanceCommitSaveData pending =
            captured.pendingTemporalStasisMaintenance
            ?? new TemporalStasisMaintenanceCommitSaveData();
        TemporalStasisMaintenanceCommitPhase phase =
            (TemporalStasisMaintenanceCommitPhase)pending.phase;
        if (phase == TemporalStasisMaintenanceCommitPhase.None)
        {
            failure = DomainFailure.None;
            return true;
        }

        if (!MatchesAuthoredContract(pending))
        {
            failure = new DomainFailure(
                FailureCode.TemporalStasisMaintenanceUnavailable,
                pending.operationId,
                "maintenance-contract-mismatch");
            return false;
        }

        bool hasReceipt = physicalItems.TryGetPending(
            pending.operationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (hasReceipt && !ReceiptMatches(pending, receipt))
        {
            failure = new DomainFailure(
                FailureCode.TemporalStasisMaintenanceUnavailable,
                pending.operationId,
                "maintenance-receipt-mismatch");
            return false;
        }

        if (phase == TemporalStasisMaintenanceCommitPhase.IntentRecorded)
        {
            if (!hasReceipt)
            {
                ClearPending(captured, advanceSequence: false);
                persistence.PublishRestore(persistence.PrepareRestore(captured));
                failure = DomainFailure.None;
                return true;
            }

            CharacterLifeRecordSaveData record = captured.characters
                .Single(value => string.Equals(
                    value.characterId,
                    pending.characterId,
                    StringComparison.Ordinal));
            record.effectiveAgingCareMode = AgingCareMode.TemporalStasis;
            record.temporalStasisNextMaintenanceAbsoluteDay =
                pending.nextMaintenanceAfterAbsoluteDay;
            pending.phase =
                (int)TemporalStasisMaintenanceCommitPhase.OutcomePublished;
            pending.sourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            pending.inputQuantity = receipt.Quantity;
            pending.inputMassGrams = receipt.InputMassGrams;
            pending.commitId = receipt.CommitId;
            try
            {
                persistence.PublishRestore(persistence.PrepareRestore(captured));
                completedOperation = true;
            }
            catch (Exception exception)
            {
                failure = new DomainFailure(
                    FailureCode.TemporalStasisMaintenanceUnavailable,
                    pending.operationId,
                    "maintenance-outcome-publication-failed",
                    exception.GetType().Name);
                return false;
            }
        }
        else
        {
            completedOperation = true;
        }

        if (hasReceipt
            && !physicalItems.Acknowledge(
                receipt.CommitId,
                out string acknowledgeFailure))
        {
            failure = new DomainFailure(
                FailureCode.TemporalStasisMaintenanceUnavailable,
                pending.operationId,
                "maintenance-acknowledge-failed",
                acknowledgeFailure ?? string.Empty);
            return false;
        }

        CharacterLifeWorldSaveData terminal = persistence.Capture();
        ClearPending(terminal, advanceSequence: true);
        persistence.PublishRestore(persistence.PrepareRestore(terminal));
        failure = DomainFailure.None;
        return true;
    }

    private void TryClearUncommittedIntent(string operationId)
    {
        if (physicalItems.TryGetPending(operationId, out _))
        {
            return;
        }
        CharacterLifeWorldSaveData captured = persistence.Capture();
        TemporalStasisMaintenanceCommitSaveData pending =
            captured.pendingTemporalStasisMaintenance;
        if ((TemporalStasisMaintenanceCommitPhase)pending.phase
                == TemporalStasisMaintenanceCommitPhase.IntentRecorded
            && string.Equals(
                pending.operationId,
                operationId,
                StringComparison.Ordinal))
        {
            ClearPending(captured, advanceSequence: false);
            persistence.PublishRestore(persistence.PrepareRestore(captured));
        }
    }

    private static bool MatchesAuthoredContract(
        TemporalStasisMaintenanceCommitSaveData pending) =>
        pending.runeConductorQuantity == 1
        && pending.manaCrystalQuantity == 1
        && string.Equals(
            pending.runeConductorItemId,
            RuneConductorItemId,
            StringComparison.Ordinal)
        && string.Equals(
            pending.manaCrystalItemId,
            ManaCrystalItemId,
            StringComparison.Ordinal)
        && string.Equals(
            pending.reasonCode,
            DispositionReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            pending.operationId,
            FormatOperationId(
                new CharacterId(pending.characterId),
                pending.operationSequence),
            StringComparison.Ordinal);

    private static bool ReceiptMatches(
        TemporalStasisMaintenanceCommitSaveData pending,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            pending.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            pending.reasonCode,
            StringComparison.Ordinal)
        && receipt.Quantity
            == pending.runeConductorQuantity + pending.manaCrystalQuantity;

    private static void ClearPending(
        CharacterLifeWorldSaveData data,
        bool advanceSequence)
    {
        if (advanceSequence)
        {
            data.nextTemporalStasisMaintenanceOperationSequence = checked(
                data.nextTemporalStasisMaintenanceOperationSequence + 1);
        }
        data.pendingTemporalStasisMaintenance =
            new TemporalStasisMaintenanceCommitSaveData();
    }

    private BuildableObject FindFacility(string facilityId)
    {
        return buildingWorld.Buildings.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                facilityId,
                StringComparison.Ordinal));
    }

    private bool HasRequiredPower(BuildableObject facility)
    {
        return facility != null
            && power.TryGetNode(facility, out PowerNodeSnapshot node)
            && node.Powered
            && node.DemandPerSecond >= RequiredRunePower
            && node.SuppliedFraction >= 0.999f;
    }
}

public sealed class TemporalStasisMaintenanceAdapter : IStartable, IDisposable
{
    private readonly ITemporalStasisMaintenanceService maintenance;
    private readonly ITemporalStasisMaintenanceRecovery recovery;
    private readonly ICharacterLifeQuery life;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly IGameEventBus events;
    private IDisposable dayStartedSubscription;
    private IDisposable dayEndedSubscription;

    public TemporalStasisMaintenanceAdapter(
        ITemporalStasisMaintenanceService maintenance,
        ITemporalStasisMaintenanceRecovery recovery,
        ICharacterLifeQuery life,
        IMilestoneGameplayModifierQuery milestoneModifiers,
        IGameEventBus events)
    {
        this.maintenance = maintenance
            ?? throw new ArgumentNullException(nameof(maintenance));
        this.recovery = recovery
            ?? throw new ArgumentNullException(nameof(recovery));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.milestoneModifiers = milestoneModifiers
            ?? throw new ArgumentNullException(nameof(milestoneModifiers));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Start()
    {
        if (!recovery.TryRecoverPending(out DomainFailure failure))
        {
            throw new InvalidOperationException(
                "Temporal-stasis maintenance startup recovery failed: "
                + failure);
        }
        dayStartedSubscription = events.Subscribe<OperatingDayStartedEvent>(
            OnDayStarted);
        dayEndedSubscription = events.Subscribe<OperatingDayEndedEvent>(
            _ => maintenance.RefreshDailyMaintenance());
    }

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        int warningDays = Math.Max(
            0,
            milestoneModifiers.TemporalStasisWarningDays);
        if (warningDays == 0)
        {
            return;
        }

        foreach (CharacterLifeRecord record in life.Records
                     .Where(value => value != null
                         && value.RequestedAgingCareMode
                             == AgingCareMode.TemporalStasis)
                     .OrderBy(value => value.CharacterId.Value,
                         StringComparer.Ordinal))
        {
            int remaining = record.TemporalStasisNextMaintenanceAbsoluteDay
                - started.day;
            if (remaining <= 0 || remaining > warningDays)
            {
                continue;
            }

            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "시간 고정 유지보수 예고",
                $"{record.CharacterId.Value}의 시간 고정 촉매 교체까지 {remaining}일 남았습니다. "
                    + $"{PhysicalAgeTreatmentRuntime.RuneConductorItemId}와 "
                    + $"{PhysicalAgeTreatmentRuntime.ManaCrystalItemId}을 시설 버퍼에 준비해야 합니다.",
                EventAlertImportance.High,
                "V21 시간 고정",
                sourceId: $"temporal-stasis-maintenance-warning:{record.CharacterId.Value}")));
        }
    }

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        dayStartedSubscription = null;
        dayEndedSubscription?.Dispose();
        dayEndedSubscription = null;
    }
}
