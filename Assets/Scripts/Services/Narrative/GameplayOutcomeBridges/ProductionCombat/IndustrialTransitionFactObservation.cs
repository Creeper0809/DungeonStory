using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using VContainer;

public static class IndustrialTransitionFactObservation
{
    public static InfrastructureCommandOutcomeReceipt CaptureCommand(
        IndustrialTransitionFact fact)
    {
        IndustrialTransitionFactData data = Snapshot(fact);
        if (IndustrialTransitionFact.IsAutonomous(data.kind))
            throw new ArgumentOutOfRangeException(nameof(fact));
        return new InfrastructureCommandOutcomeReceipt(
            CaptureSource(data),
            FacilityName(data),
            data.ownerRevision,
            data.absoluteDay);
    }

    public static InfrastructureAutonomousOutcomeReceipt CaptureAutonomous(
        IndustrialTransitionFact fact)
    {
        IndustrialTransitionFactData data = Snapshot(fact);
        if (!IndustrialTransitionFact.IsAutonomous(data.kind))
            throw new ArgumentOutOfRangeException(nameof(fact));
        return new InfrastructureAutonomousOutcomeReceipt(
            CaptureSource(data),
            FacilityName(data),
            data.ownerRevision,
            data.absoluteDay);
    }

    public static InfrastructureCommandOutcomeKind MapKind(
        IndustrialTransitionKind kind) => kind switch
    {
        IndustrialTransitionKind.PowerConnectionChanged =>
            InfrastructureCommandOutcomeKind.PowerConnectionChanged,
        IndustrialTransitionKind.PowerPriorityChanged =>
            InfrastructureCommandOutcomeKind.PowerPriorityChanged,
        IndustrialTransitionKind.PowerBreakerReset =>
            InfrastructureCommandOutcomeKind.PowerBreakerReset,
        IndustrialTransitionKind.FluidBlockageCleared =>
            InfrastructureCommandOutcomeKind.FluidBlockageCleared,
        IndustrialTransitionKind.WaterTransferModeChanged =>
            InfrastructureCommandOutcomeKind.WaterTransferModeChanged,
        IndustrialTransitionKind.FluidLeakRepaired =>
            InfrastructureCommandOutcomeKind.FluidLeakRepaired,
        IndustrialTransitionKind.ConveyorNodeEnabledChanged =>
            InfrastructureCommandOutcomeKind.ConveyorNodeEnabledChanged,
        IndustrialTransitionKind.ConveyorDestinationChanged =>
            InfrastructureCommandOutcomeKind.ConveyorDestinationChanged,
        IndustrialTransitionKind.ConveyorOverflowPolicyChanged =>
            InfrastructureCommandOutcomeKind.ConveyorOverflowPolicyChanged,
        IndustrialTransitionKind.ConveyorFilterChanged =>
            InfrastructureCommandOutcomeKind.ConveyorFilterChanged,
        IndustrialTransitionKind.ConveyorOverflowApproved =>
            InfrastructureCommandOutcomeKind.ConveyorOverflowApproved,
        IndustrialTransitionKind.AutomationModeChanged =>
            InfrastructureCommandOutcomeKind.AutomationModeChanged,
        IndustrialTransitionKind.AutomationMaintained =>
            InfrastructureCommandOutcomeKind.AutomationMaintained,
        IndustrialTransitionKind.AutomationMaintenanceRequiredEntered =>
            InfrastructureCommandOutcomeKind.AutomationMaintenanceRequiredEntered,
        IndustrialTransitionKind.PowerFuelConsumed =>
            InfrastructureCommandOutcomeKind.PowerFuelConsumed,
        IndustrialTransitionKind.PowerBreakerTripped =>
            InfrastructureCommandOutcomeKind.PowerBreakerTripped,
        IndustrialTransitionKind.WastewaterBatchCompleted =>
            InfrastructureCommandOutcomeKind.WastewaterBatchCompleted,
        IndustrialTransitionKind.FluidLeakEpisode =>
            InfrastructureCommandOutcomeKind.FluidLeakEpisode,
        IndustrialTransitionKind.WastewaterBackflow =>
            InfrastructureCommandOutcomeKind.WastewaterBackflow,
        IndustrialTransitionKind.PipeFreezeLatched =>
            InfrastructureCommandOutcomeKind.PipeFreezeLatched,
        IndustrialTransitionKind.PipeFreezeRecovered =>
            InfrastructureCommandOutcomeKind.PipeFreezeRecovered,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    public static InfrastructureCommandOutcomeSource CaptureSource(
        IndustrialTransitionFact fact) => CaptureSource(Snapshot(fact));

    public static void RegisterIndustrialTransitionFactObservation(
        this IContainerBuilder builder)
    {
        builder.Register(resolver => new IndustrialTransitionFactObserverBinding(
                resolver.Resolve<IGameplayOutcomeRegistry>(),
                resolver.Resolve<GameplayOutcomeLedger>(),
                resolver.Resolve<IGameplayOutcomeRecorder>(),
                resolver.Resolve<GameplayDomainFactCommitGroup>()),
                Lifetime.Singleton)
            .As<IDomainFactObserverBinding>();
    }

    public static void ValidateSaveJoin(
        DungeonInfrastructureObservationSaveData owner,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonGameRestoreReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        try
        {
            if (owner?.pending == null || owner.nextOwnerRevision < 1
                || ledger?.outbox == null || ledger.exactOutcomes == null
                || ledger.tombstones == null)
            {
                throw new InvalidOperationException(
                    "industrial-observation-save-state-missing");
            }

            List<GameplayOutcomeTombstoneSnapshot> known =
                NativeTombstones(ledger);
            if (known.Any(row => row.ownerRevision < 1
                    || row.ownerRevision >= owner.nextOwnerRevision
                    || !MatchesNativeResultKey(row))
                || known.GroupBy(row => row.ownerRevision)
                    .Any(group => group.Count() != 1))
            {
                throw new InvalidOperationException(
                    "industrial-observation-native-sequence-conflict");
            }

            long previous = 0;
            foreach (InfrastructureObservationSaveData pending in owner.pending)
            {
                if (pending?.fact == null
                    || pending.fact.ownerRevision <= previous)
                {
                    throw new InvalidOperationException(
                        "industrial-observation-pending-order-invalid");
                }
                pending.Validate(owner.nextOwnerRevision);
                previous = pending.fact.ownerRevision;

                var fact = new IndustrialTransitionFact(pending.fact);
                bool autonomous = IndustrialTransitionFact.IsAutonomous(
                    fact.Kind);
                string expectedKey = autonomous
                    ? CaptureAutonomous(fact).ResultKey.ToString()
                    : CaptureCommand(fact).ResultKey.ToString();
                List<GameplayOutcomeTombstoneSnapshot> found = known
                    .Where(row => row.ownerRevision == fact.OwnerRevision)
                    .ToList();
                if (!string.Equals(
                        pending.publicationKey,
                        expectedKey,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "industrial-observation-publication-key-conflict");
                }
                if (pending.publicationId.Length > 0
                    && (found.Count != 1
                        || !string.Equals(
                            pending.publicationId,
                            OutcomeId(found[0]),
                            StringComparison.Ordinal)
                        || !string.Equals(
                            pending.publicationFingerprint,
                            found[0].canonicalHash,
                            StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        "industrial-observation-publication-proof-conflict");
                }

                if (autonomous)
                {
                    ValidateCanonical(
                        CaptureAutonomous(fact), found, ledger);
                }
                else
                {
                    ValidateCanonical(CaptureCommand(fact), found, ledger);
                }
            }
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException or OverflowException)
        {
            report.AddError("industrial-observation-save-join:"
                + error.Message);
        }
    }

    public static void ValidatePowerFuelJoin(
        PowerNodeSaveData node,
        PowerFuelCommitSaveData pending,
        DungeonInfrastructureObservationSaveData owner,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonGameRestoreReport report)
    {
        if (report == null) throw new ArgumentNullException(nameof(report));
        try
        {
            if (node == null || pending == null || owner?.pending == null
                || ledger?.outbox == null || ledger.exactOutcomes == null
                || owner.nextOwnerRevision < 1
                || pending.outcomeOwnerRevision < 1
                || pending.outcomeOwnerRevision >= owner.nextOwnerRevision
                || pending.sourceStackIds == null)
            {
                throw new InvalidOperationException(
                    "power-fuel-observation-save-state-missing");
            }

            long revision = pending.outcomeOwnerRevision;
            List<InfrastructureObservationSaveData> ownerRows = owner.pending
                .Where(row => row?.fact?.ownerRevision == revision)
                .ToList();
            List<GameplayOutcomeSnapshot> ledgerRows = ledger.outbox
                .Concat(ledger.exactOutcomes)
                .Where(row => row != null
                    && row.ownerRevision == revision
                    && string.Equals(
                        row.producerId,
                        InfrastructureAutonomousOutcomeIds.ProducerId,
                        StringComparison.Ordinal))
                .ToList();

            if (ownerRows.Count > 1 || ledgerRows.Count > 1
                || ownerRows.Count == 0 && ledgerRows.Count == 0)
            {
                throw new InvalidOperationException(
                    "power-fuel-observation-owner-conflict");
            }
            if (ownerRows.Count == 1)
            {
                InfrastructureObservationSaveData observation = ownerRows[0];
                observation.Validate(owner.nextOwnerRevision);
                if (!MatchesPowerFuelFact(
                        observation.fact, node, pending))
                {
                    throw new InvalidOperationException(
                        "power-fuel-observation-fact-conflict");
                }
                var fact = new IndustrialTransitionFact(observation.fact);
                InfrastructureAutonomousOutcomeReceipt receipt =
                    CaptureAutonomous(fact);
                if (!string.Equals(
                        observation.publicationKey,
                        receipt.ResultKey.ToString(),
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "power-fuel-observation-key-conflict");
                }
            }
            if (ledgerRows.Count == 1
                && !MatchesPowerFuelOutcome(
                    ledgerRows[0], node, pending))
            {
                throw new InvalidOperationException(
                    "power-fuel-native-outcome-conflict");
            }

            if (ownerRows.Count == 1 && ledgerRows.Count == 1)
            {
                InfrastructureObservationSaveData observation = ownerRows[0];
                GameplayOutcomeSnapshot outcome = ledgerRows[0];
                if (observation.publicationId.Length > 0
                    && (!string.Equals(
                            observation.publicationId,
                            OutcomeId(outcome),
                            StringComparison.Ordinal)
                        || !string.Equals(
                            observation.publicationFingerprint,
                            outcome.immutablePayloadHash,
                            StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        "power-fuel-observation-proof-conflict");
                }
            }
        }
        catch (Exception error) when (error is ArgumentException
            or InvalidOperationException or OverflowException)
        {
            report.AddError("power-fuel-observation-save-join:"
                + error.Message);
        }
    }

    private static List<GameplayOutcomeTombstoneSnapshot> NativeTombstones(
        GameplayOutcomeLedgerSaveData ledger)
    {
        var result = ledger.tombstones
            .Where(row => row != null && IsNativeProducer(row.producerId))
            .ToList();
        foreach (GameplayOutcomeSnapshot row in ledger.outbox
                     .Concat(ledger.exactOutcomes)
                     .Where(row => row != null
                         && IsNativeProducer(row.producerId)))
        {
            result.Add(ToTombstone(row));
        }
        return result;
    }

    private static bool IsNativeProducer(string producerId) =>
        string.Equals(
            producerId,
            InfrastructureCommandOutcomeIds.ProducerId,
            StringComparison.Ordinal)
        || string.Equals(
            producerId,
            InfrastructureAutonomousOutcomeIds.ProducerId,
            StringComparison.Ordinal);

    private static bool MatchesNativeResultKey(
        GameplayOutcomeTombstoneSnapshot row)
    {
        GameplayResultKey expected = string.Equals(
            row.producerId,
            InfrastructureAutonomousOutcomeIds.ProducerId,
            StringComparison.Ordinal)
            ? InfrastructureAutonomousOutcomeIds.ResultKey(row.ownerRevision)
            : InfrastructureCommandOutcomeIds.ResultKey(row.ownerRevision);
        return new GameplayResultKey(
                row.producerId,
                new GameplayOperationId(row.operationId),
                row.commitRevision,
                row.localResultIndex)
            == expected;
    }

    private static GameplayOutcomeTombstoneSnapshot ToTombstone(
        GameplayOutcomeSnapshot row) => new()
    {
        runId = row.runId,
        sequence = row.sequence,
        producerId = row.producerId,
        operationId = row.operationId,
        commitRevision = row.commitRevision,
        localResultIndex = row.localResultIndex,
        ownerRevision = row.ownerRevision,
        temporalMetadataVersion = GameplayOutcomeDeliveryTime.CurrentVersion,
        absoluteDay = row.absoluteDay,
        parentRunId = row.parentRunId,
        parentSequence = row.parentSequence,
        deliveryMetadataVersion = row.deliveryMetadataVersion,
        deliveryClockId = row.deliveryClockId,
        recordedAbsoluteHour = row.recordedAbsoluteHour,
        publishedRevision = row.publishedRevision,
        deliveryFaultCount = row.deliveryFaultCount,
        deliveryDelayReason = row.deliveryDelayReason,
        canonicalHash = row.immutablePayloadHash,
        terminalTier = NarrativeMemoryTier.Forgotten
    };

    private static void ValidateCanonical<TReceipt>(
        TReceipt receipt,
        List<GameplayOutcomeTombstoneSnapshot> found,
        GameplayOutcomeLedgerSaveData incoming)
    {
        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new InfrastructureCommandOutcomeDescriptor(
                    new KoreanJosaFormatter()),
                new InfrastructureAutonomousOutcomeDescriptor(
                    new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new InfrastructureCommandOutcomeAdapter(),
                new InfrastructureAutonomousOutcomeAdapter()
            });
        var comparison = new GameplayOutcomeLedger(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 2,
                largePageCount: 2,
                knownResultKeyCapacity: 4,
                retainedSmallPageCount: 2,
                retainedLargePageCount: 2));
        GameplayOutcomeLedgerRestoreCandidate candidate = comparison
            .PrepareGameplayOutcomeRestore(new GameplayOutcomeLedgerSaveData
            {
                runId = incoming.runId,
                worldEpoch = incoming.worldEpoch,
                revision = incoming.revision,
                nextSequence = incoming.nextSequence,
                knownResultKeyHighWater = found.Count,
                tombstones = found
            });
        comparison.BeginRestoreCandidate();
        comparison.PublishGameplayOutcomeRestore(candidate);
        comparison.PublishRestoreCandidate();
        comparison.CompleteRestoreCandidate();
        var recorder = new GameplayOutcomeRecorder(
            comparison,
            registry,
            new GameEventBus());
        OutcomePrepareResult prepared = recorder.TryPrepare(
            receipt,
            out var token);
        recorder.CancelPrepared(token);
        if (found.Count == 0
                ? !prepared.Success
                : prepared.Code != OutcomePrepareCode.AlreadyTerminal)
        {
            throw new InvalidOperationException(
                "industrial-observation-canonical-fact-conflict:"
                + prepared.Code + ":" + prepared.DetailCode);
        }
    }

    private static bool MatchesPowerFuelFact(
        IndustrialTransitionFactData fact,
        PowerNodeSaveData node,
        PowerFuelCommitSaveData pending) =>
        fact != null
        && fact.kind == IndustrialTransitionKind.PowerFuelConsumed
        && string.Equals(
            fact.targetId, node.buildingInstanceId, StringComparison.Ordinal)
        && string.Equals(
            fact.facilityId, node.buildingInstanceId, StringComparison.Ordinal)
        && string.Equals(
            fact.details?.operationId,
            pending.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            fact.details.itemId,
            pending.itemId,
            StringComparison.Ordinal)
        && fact.details.quantity == pending.quantity
        && fact.details.massGrams == pending.inputMassGrams
        && fact.details.sourceStackIds.SequenceEqual(
            pending.sourceStackIds,
            StringComparer.Ordinal)
        && string.Equals(
            fact.details.physicalCommitId,
            pending.commitId,
            StringComparison.Ordinal)
        && fact.before.fuelSeconds == pending.fuelSecondsBefore
        && fact.after.fuelSeconds == pending.fuelSecondsAfter;

    private static bool MatchesPowerFuelOutcome(
        GameplayOutcomeSnapshot outcome,
        PowerNodeSaveData node,
        PowerFuelCommitSaveData pending)
    {
        if (outcome.commitRevision != pending.outcomeOwnerRevision
            || outcome.localResultIndex != 0
            || !string.Equals(
                outcome.operationId,
                "infrastructure-autonomous:"
                    + pending.outcomeOwnerRevision,
                StringComparison.Ordinal)
            || !string.Equals(
                outcome.outcomeTypeId,
                InfrastructureAutonomousOutcomeIds.Transitioned.Value,
                StringComparison.Ordinal))
        {
            return false;
        }
        return HasExactFact(
                outcome,
                InfrastructureAutonomousOutcomeIds.TransitionKindFact.Value,
                "power-fuel-consumed")
            && HasExactFact(
                outcome,
                InfrastructureAutonomousOutcomeIds.TargetIdFact.Value,
                node.buildingInstanceId)
            && HasExactFact(
                outcome,
                InfrastructureAutonomousOutcomeIds.BeforeValueFact.Value,
                "fuelSeconds=" + Float(pending.fuelSecondsBefore)
                    + ";operation=" + pending.operationId)
            && HasExactFact(
                outcome,
                InfrastructureAutonomousOutcomeIds.AfterValueFact.Value,
                "fuelSeconds=" + Float(pending.fuelSecondsAfter)
                    + ";item=" + pending.itemId
                    + ";quantity=" + pending.quantity
                    + ";massGrams=" + pending.inputMassGrams
                    + ";sourceStacks=" + string.Join(",",
                        pending.sourceStackIds.OrderBy(
                            value => value,
                            StringComparer.Ordinal))
                    + ";physicalCommit=" + pending.commitId);
    }

    private static bool HasExactFact(
        GameplayOutcomeSnapshot outcome,
        string factId,
        string expected) => outcome.facts != null
        && outcome.facts.Count(row => row != null
            && string.Equals(row.factId, factId, StringComparison.Ordinal)
            && string.Equals(row.value, expected, StringComparison.Ordinal)) == 1;

    private static string OutcomeId(
        GameplayOutcomeTombstoneSnapshot row) => new GameplayOutcomeId(
        new GameplayOutcomeRunId(row.runId), row.sequence).ToString();

    private static string OutcomeId(GameplayOutcomeSnapshot row) =>
        new GameplayOutcomeId(
            new GameplayOutcomeRunId(row.runId), row.sequence).ToString();

    private static IndustrialTransitionFactData Snapshot(
        IndustrialTransitionFact fact) =>
        (fact ?? throw new ArgumentNullException(nameof(fact))).Snapshot();

    private static InfrastructureCommandOutcomeSource CaptureSource(
        IndustrialTransitionFactData data)
    {
        string beforeValue = DescribeBefore(data);
        string afterValue = DescribeAfter(data);
        if (data.kind is IndustrialTransitionKind.WastewaterBatchCompleted
            or IndustrialTransitionKind.FluidLeakEpisode
            or IndustrialTransitionKind.WastewaterBackflow
            or IndustrialTransitionKind.PipeFreezeLatched
            or IndustrialTransitionKind.PipeFreezeRecovered)
        {
            string operation = "operation=" + data.details.operationId + ";";
            beforeValue = operation + beforeValue;
            afterValue = operation + afterValue;
        }
        return new InfrastructureCommandOutcomeSource(
            MapKind(data.kind),
            data.targetId,
            data.facilityId,
            data.facilityX,
            data.facilityY,
            beforeValue,
            afterValue);
    }

    private static string DescribeBefore(IndustrialTransitionFactData data) =>
        data.kind switch
        {
            IndustrialTransitionKind.PowerConnectionChanged =>
                Bool(data.before.connected),
            IndustrialTransitionKind.PowerPriorityChanged =>
                data.before.powerPriority.ToString(),
            IndustrialTransitionKind.PowerBreakerReset =>
                BreakerReset(data.before),
            IndustrialTransitionKind.FluidBlockageCleared =>
                Float(data.before.blockage),
            IndustrialTransitionKind.WaterTransferModeChanged =>
                WaterTransfer(data.before),
            IndustrialTransitionKind.FluidLeakRepaired =>
                Float(data.before.leak),
            IndustrialTransitionKind.ConveyorNodeEnabledChanged =>
                Bool(data.before.enabled),
            IndustrialTransitionKind.ConveyorDestinationChanged =>
                Optional(data.before.destinationId),
            IndustrialTransitionKind.ConveyorOverflowPolicyChanged =>
                OverflowPolicy(data.before),
            IndustrialTransitionKind.ConveyorFilterChanged =>
                Filter(data.before.filter),
            IndustrialTransitionKind.ConveyorOverflowApproved =>
                Bool(data.before.overflowApproved),
            IndustrialTransitionKind.AutomationModeChanged =>
                data.before.automationMode.ToString(),
            IndustrialTransitionKind.AutomationMaintained =>
                AutomationCondition(data.before, false),
            IndustrialTransitionKind.AutomationMaintenanceRequiredEntered =>
                MaintenanceRequired(data.before, data.details),
            IndustrialTransitionKind.PowerFuelConsumed =>
                "fuelSeconds=" + Float(data.before.fuelSeconds)
                + ";operation=" + data.details.operationId,
            IndustrialTransitionKind.PowerBreakerTripped =>
                "breaker=" + Bool(data.before.breakerTripped)
                + ";powered=" + Bool(data.before.powered)
                + ";heat=" + Float(data.before.heat)
                + ";fault=" + Float(data.before.fault)
                + ";loadRatio=" + Float(data.details.loadRatio),
            IndustrialTransitionKind.WastewaterBatchCompleted =>
                "wasteNetwork=" + data.details.wasteNetworkId
                + ";waterNetwork=" + data.details.waterNetworkId
                + ";wastewater=" + Float(data.before.wastewater)
                + ";outputQuality=" + data.details.outputQuality
                + ";outputWater=" + Float(data.before.outputWater)
                + ";processorWork=" + Float(data.before.work),
            IndustrialTransitionKind.FluidLeakEpisode =>
                "cleanWater=" + Float(data.before.cleanWater)
                + ";unsafeWater=" + Float(data.before.unsafeWater)
                + ";foulWater=" + Float(data.before.foulWater)
                + ";leakRate=" + Float(data.before.leak),
            IndustrialTransitionKind.WastewaterBackflow =>
                "wasteNetwork=" + Optional(data.details.wasteNetworkId)
                + ";blockage=" + Float(data.before.blockage)
                + ";sewageFilth=" + Optional(data.before.filthId)
                + ";filthAmount=" + Float(data.before.filthAmount),
            IndustrialTransitionKind.PipeFreezeLatched or
            IndustrialTransitionKind.PipeFreezeRecovered =>
                FreezeBefore(data.before),
            _ => throw new ArgumentOutOfRangeException(
                nameof(data.kind), data.kind, null)
        };

    private static string DescribeAfter(IndustrialTransitionFactData data) =>
        data.kind switch
        {
            IndustrialTransitionKind.PowerConnectionChanged =>
                Bool(data.after.connected),
            IndustrialTransitionKind.PowerPriorityChanged =>
                data.after.powerPriority.ToString(),
            IndustrialTransitionKind.PowerBreakerReset =>
                BreakerReset(data.after),
            IndustrialTransitionKind.FluidBlockageCleared =>
                Float(data.after.blockage),
            IndustrialTransitionKind.WaterTransferModeChanged =>
                WaterTransfer(data.after),
            IndustrialTransitionKind.FluidLeakRepaired =>
                Float(data.after.leak),
            IndustrialTransitionKind.ConveyorNodeEnabledChanged =>
                Bool(data.after.enabled),
            IndustrialTransitionKind.ConveyorDestinationChanged =>
                Optional(data.after.destinationId),
            IndustrialTransitionKind.ConveyorOverflowPolicyChanged =>
                OverflowPolicy(data.after),
            IndustrialTransitionKind.ConveyorFilterChanged =>
                Filter(data.after.filter),
            IndustrialTransitionKind.ConveyorOverflowApproved =>
                Bool(data.after.overflowApproved),
            IndustrialTransitionKind.AutomationModeChanged =>
                data.after.automationMode.ToString(),
            IndustrialTransitionKind.AutomationMaintained =>
                AutomationCondition(data.after, false),
            IndustrialTransitionKind.AutomationMaintenanceRequiredEntered =>
                MaintenanceRequired(data.after, data.details),
            IndustrialTransitionKind.PowerFuelConsumed =>
                "fuelSeconds=" + Float(data.after.fuelSeconds)
                + ";item=" + data.details.itemId
                + ";quantity=" + data.details.quantity
                + ";massGrams=" + data.details.massGrams
                + ";sourceStacks=" + string.Join(",",
                    data.details.sourceStackIds.OrderBy(
                        value => value, StringComparer.Ordinal))
                + ";physicalCommit=" + data.details.physicalCommitId,
            IndustrialTransitionKind.PowerBreakerTripped =>
                "breaker=" + Bool(data.after.breakerTripped)
                + ";powered=" + Bool(data.after.powered)
                + ";heat=" + Float(data.after.heat)
                + ";fault=" + Float(data.after.fault)
                + ";tripHeat=" + Float(data.details.tripHeat)
                + ";overloadTolerance="
                + Float(data.details.overloadTolerance),
            IndustrialTransitionKind.WastewaterBatchCompleted =>
                "wasteNetwork=" + data.details.wasteNetworkId
                + ";waterNetwork=" + data.details.waterNetworkId
                + ";wastewater=" + Float(data.after.wastewater)
                + ";inputUsed=" + Float(data.details.inputUsed)
                + ";outputQuality=" + data.details.outputQuality
                + ";outputWater=" + Float(data.after.outputWater)
                + ";waterProduced=" + Float(data.details.waterProduced)
                + ";sludgeItem=" + Optional(data.details.itemId)
                + ";sludgeQuantity=" + data.details.quantity
                + ";sludgeStacks=" + ItemDeltas(data.details.itemDeltas)
                + ";processorWork=" + Float(data.after.work),
            IndustrialTransitionKind.FluidLeakEpisode =>
                "cleanWater=" + Float(data.after.cleanWater)
                + ";unsafeWater=" + Float(data.after.unsafeWater)
                + ";foulWater=" + Float(data.after.foulWater)
                + ";cleanLoss=" + Float(data.details.cleanLoss)
                + ";unsafeLoss=" + Float(data.details.unsafeLoss)
                + ";foulLoss=" + Float(data.details.foulLoss)
                + ";totalLoss=" + Float(data.details.totalLoss)
                + ";sewageFilthAdded=" + Float(data.details.filthAdded)
                + ";filthIds=" + Ids(data.details.filthIds),
            IndustrialTransitionKind.WastewaterBackflow =>
                "wasteNetwork=" + Optional(data.details.wasteNetworkId)
                + ";overflow=" + Float(data.details.overflow)
                + ";blockage=" + Float(data.after.blockage)
                + ";sewageFilth=" + data.after.filthId
                + ";filthAmount=" + Float(data.after.filthAmount)
                + ";filthAdded=" + Float(data.details.filthAdded),
            IndustrialTransitionKind.PipeFreezeLatched or
            IndustrialTransitionKind.PipeFreezeRecovered =>
                FreezeAfter(data.after, data.details),
            _ => throw new ArgumentOutOfRangeException(
                nameof(data.kind), data.kind, null)
        };

    private static string BreakerReset(IndustrialStateData state) =>
        "breaker=" + Bool(state.breakerTripped)
        + ";fault=" + Float(state.fault);

    private static string WaterTransfer(IndustrialStateData state) =>
        "mode=" + state.transferMode
        + ";work=" + Float(state.work)
        + ";status=" + state.status
        + ";status-parameters=["
        + string.Join(",", state.statusParameters)
        + "]";

    private static string OverflowPolicy(IndustrialStateData state) =>
        "policy=" + state.overflowPolicy
        + ";warehouse=" + Optional(state.warehouseId);

    private static string Filter(ConveyorFilterCriteria filter) =>
        "items=" + string.Join(",", filter.itemIds.OrderBy(
            value => value, StringComparer.Ordinal))
        + ";categories=" + string.Join(",", filter.stockCategories
            .Select(value => ((int)value).ToString(
                CultureInfo.InvariantCulture))
            .OrderBy(value => value, StringComparer.Ordinal))
        + ";materials=" + string.Join(",", filter.materialIds.OrderBy(
            value => value, StringComparer.Ordinal))
        + ";forbidden=" + Bool(filter.allowForbidden)
        + ";quality=" + Bool(filter.filterQuality)
        + ":" + filter.minimumQuality
        + ":" + filter.maximumQuality
        + ";freshness=" + Bool(filter.filterFreshness)
        + ":" + Float(filter.minimumFreshness01)
        + ":" + Float(filter.maximumFreshness01)
        + ";contaminated=" + Bool(filter.allowContaminated);

    private static string AutomationCondition(
        IndustrialStateData state,
        bool continuousWear) =>
        "maintenance=" + Float(state.maintenance)
        + ";fault=" + Float(state.fault)
        + ";status=" + state.status
        + (continuousWear ? ";cause=continuous-wear" : string.Empty);

    private static string MaintenanceRequired(
        IndustrialStateData state,
        IndustrialTransitionDetails details)
    {
        if (!details.continuousWear)
            throw new InvalidOperationException(
                "industrial-transition-continuous-wear-required");
        return AutomationCondition(state, true);
    }

    private static string FreezeBefore(IndustrialStateData state) =>
        "latched=" + Bool(state.freezeLatched)
        + ";occurrence=" + Optional(state.occurrenceId)
        + ";temperatureC=" + Temperature(state);

    private static string FreezeAfter(
        IndustrialStateData state,
        IndustrialTransitionDetails details) =>
        "latched=" + Bool(state.freezeLatched)
        + ";occurrence=" + Optional(state.occurrenceId)
        + ";definition=" + Optional(details.definitionId)
        + ";temperatureC=" + Temperature(state)
        + ";freezeThresholdC=" + Float(details.freezeThresholdC)
        + ";recoveryThresholdC=" + Float(details.recoveryThresholdC)
        + ";throughputMultiplier=" + Float(details.throughputMultiplier)
        + ";reason=" + details.reason;

    private static string Temperature(IndustrialStateData state) =>
        state.hasTemperature ? Float(state.temperatureC) : "none";

    private static string ItemDeltas(
        IEnumerable<IndustrialItemDeltaData> values)
    {
        string result = string.Join(",", values
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .Select(value => value.stackId + ":" + value.quantity));
        return result.Length == 0 ? "none" : result;
    }

    private static string Ids(IEnumerable<string> values)
    {
        string result = string.Join(",", values.OrderBy(
            value => value, StringComparer.Ordinal));
        return result.Length == 0 ? "none" : result;
    }

    private static KoreanNameSnapshot FacilityName(
        IndustrialTransitionFactData data) => new(
        data.facilityDisplayName,
        "runtime-facility-current-v1:" + data.facilityId,
        KoreanPronunciationHint.AutoHangulDisplay(
            "runtime-facility-pronunciation-v1:" + data.facilityId),
        "ko-KR");

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Float(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string Optional(string value) =>
        string.IsNullOrWhiteSpace(value) ? "none" : value;
}

public sealed class IndustrialTransitionDomainFactObserver :
    IDurableDomainFactObserver<IndustrialTransitionFact>,
    IDurableDomainFactStagingObserver<IndustrialTransitionFact>,
    IDurableDomainFactRecoveryObserver<IndustrialTransitionFact>
{
    private readonly GameplayDomainFactObserver<
        IndustrialTransitionFact,
        InfrastructureCommandOutcomeReceipt> command;
    private readonly GameplayDomainFactObserver<
        IndustrialTransitionFact,
        InfrastructureAutonomousOutcomeReceipt> autonomous;

    public IndustrialTransitionDomainFactObserver(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits)
    {
        command = new GameplayDomainFactObserver<
            IndustrialTransitionFact,
            InfrastructureCommandOutcomeReceipt>(
                IndustrialTransitionFactObservation.CaptureCommand,
                registry,
                ledger,
                recorder,
                commits,
                matchesProposal: MatchesReservedIdentity);
        autonomous = new GameplayDomainFactObserver<
            IndustrialTransitionFact,
            InfrastructureAutonomousOutcomeReceipt>(
                IndustrialTransitionFactObservation.CaptureAutonomous,
                registry,
                ledger,
                recorder,
                commits,
                matchesProposal: MatchesReservedIdentity);
    }

    public bool TryReserve(
        in IndustrialTransitionFact proposed,
        out object reservation,
        out string failure)
    {
        reservation = null;
        if (proposed == null)
        {
            failure = "industrial-transition-fact-null";
            return false;
        }
        bool isAutonomous = IndustrialTransitionFact.IsAutonomous(proposed.Kind);
        object inner;
        bool accepted = isAutonomous
            ? autonomous.TryReserve(proposed, out inner, out failure)
            : command.TryReserve(proposed, out inner, out failure);
        if (!accepted) return false;
        reservation = new RoutedReservation(
            isAutonomous,
            proposed.Kind,
            inner);
        return true;
    }

    public bool TryPrepare(
        in IndustrialTransitionFact actual,
        object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (!TryRoute(actual, reservation, out RoutedReservation routed,
                out failure))
        {
            return false;
        }
        return routed.Autonomous
            ? autonomous.TryPrepare(
                actual, routed.Inner, out prepared, out failure)
            : command.TryPrepare(
                actual, routed.Inner, out prepared, out failure);
    }

    public bool TryStage(
        in IndustrialTransitionFact fact,
        out object reservation,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        reservation = null;
        prepared = null;
        if (fact == null)
        {
            failure = "industrial-transition-fact-null";
            return false;
        }
        bool isAutonomous = IndustrialTransitionFact.IsAutonomous(fact.Kind);
        object inner;
        bool accepted = isAutonomous
            ? autonomous.TryStage(
                fact, out inner, out prepared, out failure)
            : command.TryStage(
                fact, out inner, out prepared, out failure);
        if (!accepted) return false;
        reservation = new RoutedReservation(isAutonomous, fact.Kind, inner);
        return true;
    }

    public DomainFactDelivery Replay(in IndustrialTransitionFact fact)
    {
        if (fact == null)
            return DomainFactDelivery.Reject("industrial-transition-fact-null");
        return IndustrialTransitionFact.IsAutonomous(fact.Kind)
            ? autonomous.Replay(fact)
            : command.Replay(fact);
    }

    public bool TryPrepareReplay(
        string publicationKey,
        in IndustrialTransitionFact fact,
        out IDomainFactPreparedPublication prepared,
        out string failure)
    {
        prepared = null;
        if (fact == null)
        {
            failure = "industrial-transition-fact-null";
            return false;
        }
        return IndustrialTransitionFact.IsAutonomous(fact.Kind)
            ? autonomous.TryPrepareReplay(
                publicationKey, fact, out prepared, out failure)
            : command.TryPrepareReplay(
                publicationKey, fact, out prepared, out failure);
    }

    public void Cancel(object reservation)
    {
        if (reservation is not RoutedReservation routed || routed.Cancelled)
            return;
        routed.Cancelled = true;
        if (routed.Autonomous) autonomous.Cancel(routed.Inner);
        else command.Cancel(routed.Inner);
    }

    private static bool TryRoute(
        IndustrialTransitionFact fact,
        object reservation,
        out RoutedReservation routed,
        out string failure)
    {
        routed = reservation as RoutedReservation;
        if (fact == null || routed == null || routed.Cancelled)
        {
            failure = "industrial-transition-reservation-invalid";
            return false;
        }
        bool isAutonomous = IndustrialTransitionFact.IsAutonomous(fact.Kind);
        if (routed.Kind != fact.Kind || routed.Autonomous != isAutonomous)
        {
            failure = "industrial-transition-reservation-route-drift";
            return false;
        }
        failure = string.Empty;
        return true;
    }

    private static bool MatchesReservedIdentity(
        IndustrialTransitionFact actual,
        IndustrialTransitionFact proposed)
    {
        if (actual == null || proposed == null) return false;
        IndustrialTransitionFactData left = actual.Snapshot();
        IndustrialTransitionFactData right = proposed.Snapshot();
        InfrastructureCommandOutcomeSource leftSource =
            IndustrialTransitionFactObservation.CaptureSource(actual);
        InfrastructureCommandOutcomeSource rightSource =
            IndustrialTransitionFactObservation.CaptureSource(proposed);
        return left.ownerRevision == right.ownerRevision
            && left.kind == right.kind
            && leftSource.Kind == rightSource.Kind
            && string.Equals(leftSource.TargetId, rightSource.TargetId,
                StringComparison.Ordinal)
            && string.Equals(leftSource.FacilityId, rightSource.FacilityId,
                StringComparison.Ordinal)
            && leftSource.FacilityX == rightSource.FacilityX
            && leftSource.FacilityY == rightSource.FacilityY
            && string.Equals(leftSource.BeforeValue, rightSource.BeforeValue,
                StringComparison.Ordinal)
            && string.Equals(leftSource.AfterValue, rightSource.AfterValue,
                StringComparison.Ordinal)
            && string.Equals(left.facilityDisplayName,
                right.facilityDisplayName, StringComparison.Ordinal)
            && left.absoluteDay == right.absoluteDay;
    }

    private sealed class RoutedReservation : IDomainFactReservedIdentity
    {
        public RoutedReservation(
            bool autonomous,
            IndustrialTransitionKind kind,
            object inner)
        {
            Autonomous = autonomous;
            Kind = kind;
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public bool Autonomous { get; }
        public IndustrialTransitionKind Kind { get; }
        public object Inner { get; }
        public bool Cancelled { get; set; }
        public string PublicationKey =>
            (Inner as IDomainFactReservedIdentity)?.PublicationKey
            ?? string.Empty;
    }
}

public sealed class IndustrialTransitionFactObserverBinding :
    IDomainFactObserverBinding
{
    private const string ObserverId = "infrastructure.transition-observer";
    private readonly IndustrialTransitionDomainFactObserver observer;
    public IReadOnlyCollection<Type> FactTypes { get; } =
        Array.AsReadOnly(new[] { typeof(IndustrialTransitionFact) });

    public IndustrialTransitionFactObserverBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => observer = new(
        registry,
        ledger,
        recorder,
        commits);

    public IDisposable Connect(DomainFactDispatcher dispatcher) =>
        dispatcher.Subscribe(ObserverId, observer);
}
