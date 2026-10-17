using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer;

public interface IEnvironmentalFireTargetQuery
{
    bool TryGetTarget(
        EnvironmentalFireTargetRef target,
        out EnvironmentalFireTargetSnapshot snapshot);

    // The adapter owns wall, door, floor, and direct-neighbour legality. This
    // runtime never scans the map or invents connectivity.
    IReadOnlyList<EnvironmentalFireTargetSnapshot> GetAdjacentTargets(
        EnvironmentalFireTargetRef target);
}

public interface IEnvironmentalFireDamageCommand
{
    // operationId is the idempotency authority across retry and restore.
    bool TryApply(
        EnvironmentalFireDamageCommand command,
        out EnvironmentalFireDamageResult result);
}

public interface IEnvironmentalFireExposureTargetQuery
{
    // Returns only live characters physically occupying the source fire cell,
    // identified by their persistent CharacterId.
    IReadOnlyList<EnvironmentalFireTargetRef> GetExposedCharacters(
        EnvironmentalFireTargetRef source,
        Vector2Int position);
}

public interface IEnvironmentalFireFuelLossSink
{
    // Commits the exact selected physical lot as a durable pending Sink. The
    // caller publishes the owning fire state before acknowledging the receipt.
    bool TryCommitPending(
        EnvironmentalFireFuelLossRequest request,
        string operationId,
        out EnvironmentalFireFuelLossReceipt receipt,
        out string failureReason);

    bool TryAcknowledge(string commitId, out string failureReason);
}

public interface IEnvironmentalFireSuppressionAccessQuery
{
    // Implementations must require a live worker on a safe, reachable cell
    // directly adjacent to the fire target.
    bool CanSuppress(
        string workerId,
        Vector2Int standPosition,
        EnvironmentalFireTargetRef target,
        out string failureReason);
}

public interface IEnvironmentalFireElectricalSafetyQuery
{
    // Implementations must resolve connection, breaker, live generation, and
    // stored-power state from the authoritative power runtime.
    bool TryGetSafety(
        EnvironmentalFireTargetRef target,
        out EnvironmentalFireElectricalSafetySnapshot snapshot);
}

public interface IEnvironmentalFireWaterSink
{
    // Implementations adapt the physical-item reserved sink/outbox. A true
    // result means the exact quantity is physically committed and pending ack.
    bool TryCommitReservedWaterPending(
        string leaseId,
        int quantity,
        string operationId,
        string workerId,
        string fireId,
        Vector2Int standPosition,
        Vector2Int targetPosition,
        out EnvironmentalFireWaterReceipt receipt,
        out string failureReason);

    bool TryGetPending(
        string operationId,
        string leaseId,
        out EnvironmentalFireWaterReceipt receipt);

    bool TryAcknowledge(string commitId, out string failureReason);
}

public interface IEnvironmentalFireQuery
{
    int Version { get; }
    IReadOnlyList<EnvironmentalFireSnapshot> ActiveFires { get; }
    IReadOnlyList<EnvironmentalFireHistorySnapshot> History { get; }
    bool TryGet(string fireId, out EnvironmentalFireSnapshot snapshot);
    IReadOnlyList<Vector2Int> GetActiveFireCells();
}

public interface IEnvironmentalFireCommand
{
    EnvironmentalFireIgnitionResult TryIgnite(
        EnvironmentalFireIgnitionRequest request);

    EnvironmentalFireSuppressionResult TryApplySuppression(
        EnvironmentalFireSuppressionCommand command);

    EnvironmentalFireSuppressionResult TryCancelSuppression(
        string operationId);

    IReadOnlyList<EnvironmentalFireSuppressionResult>
        ResumePendingSuppressions();
}

public interface IEnvironmentalFirePersistence
{
    DungeonEnvironmentalFireSaveData Capture();
    EnvironmentalFireRestoreCandidate PrepareRestore(
        DungeonEnvironmentalFireSaveData data);
    void Restore(EnvironmentalFireRestoreCandidate candidate);
}

public sealed class EnvironmentalFireRuntimeSettings
{
    public EnvironmentalFireRuntimeSettings(
        float tickInterval,
        float maximumInitialAttackIntensity,
        float initialAttackSuppressionPerWork)
    {
        RequireFinitePositive(tickInterval, nameof(tickInterval));
        RequireRange(
            maximumInitialAttackIntensity,
            0f,
            1f,
            nameof(maximumInitialAttackIntensity));
        RequireFinitePositive(
            initialAttackSuppressionPerWork,
            nameof(initialAttackSuppressionPerWork));

        TickInterval = tickInterval;
        MaximumInitialAttackIntensity = maximumInitialAttackIntensity;
        InitialAttackSuppressionPerWork = initialAttackSuppressionPerWork;
    }

    public float TickInterval { get; }
    public float MaximumInitialAttackIntensity { get; }
    public float InitialAttackSuppressionPerWork { get; }

    private static void RequireFinitePositive(float value, string parameter)
    {
        if (!IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(parameter);
    }

    private static void RequireRange(
        float value,
        float minimum,
        float maximum,
        string parameter)
    {
        if (!IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameter);
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}

public sealed class EnvironmentalFireRuntime :
    IEnvironmentalFireQuery,
    IEnvironmentalFireCommand,
    IEnvironmentalFirePersistence,
    IDungeonRestoreTransactionParticipant
{
    private sealed class TickCounters
    {
        public int Steps;
        public int DamageCommits;
        public int NewFires;
        public int EndedFires;
        public readonly List<string> Failures = new();

        public EnvironmentalFireTickReport ToReport() => new(
            Steps,
            DamageCommits,
            NewFires,
            EndedFires,
            Failures);
    }

    public const string SpreadRandomStreamId = "environment:fire:spread";
    private const float Epsilon = 0.0001f;

    private readonly IEnvironmentalFireTargetQuery targets;
    private readonly IEnvironmentalFireDamageCommand damage;
    private readonly IEnvironmentalFireSuppressionAccessQuery access;
    private readonly IEnvironmentalFireElectricalSafetyQuery electricalSafety;
    private readonly IEnvironmentalFireWaterSink water;
    private readonly IEnvironmentalFireExposureTargetQuery exposures;
    private readonly IEnvironmentalFireFuelLossSink fuelLoss;
    private readonly IDomainFactStagingPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private readonly IDomainFactPublisher completionFacts;
    private readonly IGameCalendar calendar;
    private readonly EnvironmentalFireRuntimeSettings settings;
    private readonly IRandomStream spreadRandom;
    public string LastCompletionIdentityFailureForDebug { get; private set; } = string.Empty;
    private sealed class RuntimeState
    {
        public readonly Dictionary<string, FireState> Active = new(StringComparer.Ordinal);
        public readonly Dictionary<EnvironmentalFireTargetRef, string> ByTarget = new();
        public readonly Dictionary<string, IgnitionRecord> Causes = new(StringComparer.Ordinal);
        public readonly Dictionary<string, SuppressionOperation> Suppressions = new(StringComparer.Ordinal);
        public readonly List<EnvironmentalFireHistorySnapshot> History = new();
        public long NextFire = 1, NextOutcome = 1;
        public float Accumulator;
    }

    private RuntimeState state = new();
    private RuntimeState stagedRestoreState, previousRestoreState;
    private bool restoreTransactionActive;
    private int previousVersion;
    private Dictionary<string, FireState> activeById => state.Active;
    private Dictionary<EnvironmentalFireTargetRef, string> fireByTarget => state.ByTarget;
    private Dictionary<string, IgnitionRecord> causes => state.Causes;
    private Dictionary<string, SuppressionOperation> suppressions => state.Suppressions;
    private List<EnvironmentalFireHistorySnapshot> history => state.History;
    private long nextFireSequence { get => state.NextFire; set => state.NextFire = value; }
    private long nextOutcomeSequence { get => state.NextOutcome; set => state.NextOutcome = value; }
    private float accumulator { get => state.Accumulator; set => state.Accumulator = value; }
    private int version = 1;

    [Inject]
    public EnvironmentalFireRuntime(
        IEnvironmentalFireTargetQuery targets,
        IEnvironmentalFireDamageCommand damage,
        IEnvironmentalFireSuppressionAccessQuery access,
        IEnvironmentalFireElectricalSafetyQuery electricalSafety,
        IEnvironmentalFireWaterSink water,
        EnvironmentalFireRuntimeSettings settings,
        IRandomStreamProvider randomStreams,
        IDomainFactStagingPublisher facts,
        IDomainFactRecoveryPublisher recovery,
        IDomainFactPublisher completionFacts,
        IGameCalendar calendar,
        IEnvironmentalFireExposureTargetQuery exposures = null,
        IEnvironmentalFireFuelLossSink fuelLoss = null)
    {
        this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        this.damage = damage ?? throw new ArgumentNullException(nameof(damage));
        this.access = access ?? throw new ArgumentNullException(nameof(access));
        this.electricalSafety = electricalSafety
            ?? throw new ArgumentNullException(nameof(electricalSafety));
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.exposures = exposures;
        this.fuelLoss = fuelLoss;
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
        this.completionFacts = completionFacts ?? throw new ArgumentNullException(nameof(completionFacts));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        spreadRandom = (randomStreams
            ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get(SpreadRandomStreamId);
    }

#if UNITY_EDITOR
    public EnvironmentalFireRuntime(
        IEnvironmentalFireTargetQuery targets,
        IEnvironmentalFireDamageCommand damage,
        IEnvironmentalFireSuppressionAccessQuery access,
        IEnvironmentalFireElectricalSafetyQuery electricalSafety,
        IEnvironmentalFireWaterSink water,
        EnvironmentalFireRuntimeSettings settings,
        IRandomStreamProvider randomStreams,
        IEnvironmentalFireExposureTargetQuery exposures = null,
        IEnvironmentalFireFuelLossSink fuelLoss = null)
        => throw new InvalidOperationException(
            "Environmental fire fixtures require the mandatory domain fact publishers and game calendar.");
#endif

    public int Version => version;

    public IReadOnlyList<EnvironmentalFireSnapshot> ActiveFires => activeById
        .Values
        .OrderBy(value => value.FireId, StringComparer.Ordinal)
        .Select(CloneSnapshot)
        .ToArray();

    public IReadOnlyList<EnvironmentalFireHistorySnapshot> History => history
        .Select(CloneHistory)
        .ToArray();

    public bool TryGet(
        string fireId,
        out EnvironmentalFireSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(fireId)
            && activeById.TryGetValue(fireId.Trim(), out FireState state))
        {
            snapshot = CloneSnapshot(state);
            return true;
        }

        snapshot = null;
        return false;
    }

    public IReadOnlyList<Vector2Int> GetActiveFireCells() => activeById
        .Values
        .Select(value => value.Position)
        .Distinct()
        .OrderBy(value => value.x)
        .ThenBy(value => value.y)
        .ToArray();

    public EnvironmentalFireTickReport Advance(float elapsedGameTime)
    {
        if (restoreTransactionActive) throw new InvalidOperationException("Fire simulation is unavailable during restore.");
        if (!IsFinite(elapsedGameTime) || elapsedGameTime < 0f)
            throw new ArgumentOutOfRangeException(nameof(elapsedGameTime));

        ResumePendingOutcomeCommits();

        var report = new TickCounters();
        accumulator += elapsedGameTime;
        while (accumulator >= settings.TickInterval)
        {
            accumulator -= settings.TickInterval;
            if (accumulator < 0f)
                accumulator = 0f;
            Step(report);
            report.Steps++;
        }

        return report.ToReport();
    }

    [GameplayEntryPoint(
        "Typed environmental-fire producers submit stable cause and target evidence here.")]
    public EnvironmentalFireIgnitionResult TryIgnite(
        EnvironmentalFireIgnitionRequest request)
    {
        if (restoreTransactionActive || request?.IsValid != true || request.Kind == EnvironmentalFireIgnitionKind.Spread
            || request.Target.Kind != EnvironmentalFireTargetKind.Building)
            return new EnvironmentalFireIgnitionResult(EnvironmentalFireIgnitionDisposition.InvalidRequest, string.Empty, "A canonical supported ignition event is required outside restore.");
        if (causes.TryGetValue(request.CauseId, out var known))
        {
            if (known.Fingerprint != request.Fingerprint)
                return new EnvironmentalFireIgnitionResult(EnvironmentalFireIgnitionDisposition.CauseConflict, known.FireId, "The cause ID was already used with different evidence.");
            ResumePendingIgnitionOutcome(request, known);
            return new EnvironmentalFireIgnitionResult(EnvironmentalFireIgnitionDisposition.PreviouslyProcessed, known.FireId, known.Reason);
        }
        if (!targets.TryGetTarget(request.Target, out var target) || !target.Exists)
            return CommitIgnitionOutcome(request, EnvironmentalFireIgnitionDisposition.TargetMissing, string.Empty, "The authored target no longer exists.", default, false);
        if (!target.CanBurn || !target.Accepts(request.Kind))
            return CommitIgnitionOutcome(request, EnvironmentalFireIgnitionDisposition.TargetNotCombustible, string.Empty, "The authored target is not eligible for this ignition source.", target.Position, true);
        if (fireByTarget.TryGetValue(request.Target, out string existing))
            return CommitIgnitionOutcome(request, EnvironmentalFireIgnitionDisposition.AlreadyBurning, existing, "The target already has an active environmental fire.", target.Position, true);
        return IgnitePrepared(request, target, "Ignition accepted.");
    }

    [GameplayEntryPoint(
        "Approved worker suppression publishes labor and optional exact physical-water evidence here.")]
    public EnvironmentalFireSuppressionResult TryApplySuppression(
        EnvironmentalFireSuppressionCommand command)
    {
        if (restoreTransactionActive || command == null || !command.IsValid)
            return SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.InvalidRequest,
                "A canonical suppression operation is required.");

        if (suppressions.TryGetValue(
                command.OperationId,
                out SuppressionOperation known))
        {
            if (!string.Equals(
                    known.Fingerprint,
                    known.LegacyPreOutcome
                        ? command.LegacyFingerprint
                        : command.Fingerprint,
                    StringComparison.Ordinal))
            {
                return SuppressionFailure(
                    EnvironmentalFireSuppressionDisposition.OperationConflict,
                    "The operation ID was already used by another command.");
            }

            if (known.OutcomePending)
                ResumePendingSuppressionOutcome(known);

            EnsureCompletionIdentity(known);
            return ResultForKnownOperation(known);
        }

        if (!TryValidateSuppression(
                command,
                out FireState fire,
                out EnvironmentalFireTargetSnapshot target,
                out EnvironmentalFireSuppressionResult failure))
        {
            return failure;
        }

        if (command.Mode == EnvironmentalFireSuppressionMode.InitialAttack)
        {
            if (fire.Intensity - Epsilon
                > settings.MaximumInitialAttackIntensity)
            {
                return SuppressionFailure(
                    EnvironmentalFireSuppressionDisposition.RequiresWater,
                    "This fire is above the authored initial-attack threshold.",
                    fire.Intensity);
            }

            return CommitSuppressionOutcome(
                command,
                fire,
                target,
                command.ApprovedWork
                    * settings.InitialAttackSuppressionPerWork,
                commitWater: false);
        }

        return CommitSuppressionOutcome(
            command,
            fire,
            target,
            command.WaterQuantity * target.Profile.WaterSuppressionPerUnit,
            commitWater: true);
    }

    [GameplayEntryPoint(
        "Cancels only suppression work that has not committed labor or physical water.")]
    public EnvironmentalFireSuppressionResult TryCancelSuppression(
        string operationId)
    {
        if (restoreTransactionActive)
            return SuppressionFailure(EnvironmentalFireSuppressionDisposition.InvalidRequest, "Fire cancellation is unavailable during restore.");
        string canonicalId = operationId?.Trim() ?? string.Empty;
        if (canonicalId.Length == 0)
        {
            return SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.InvalidRequest,
                "A canonical operation ID is required.");
        }

        if (!suppressions.TryGetValue(canonicalId, out SuppressionOperation operation))
        {
            return SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.OperationMissing,
                "The suppression operation does not exist.");
        }

        if (operation.Phase == EnvironmentalFireSuppressionPhase.Cancelled)
        {
            return SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.Cancelled,
                operation.ResultReason,
                operation.IntensityBefore);
        }

        if (operation.OutcomePending)
            return SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.OperationInProgress,
                "Cancellation is unavailable while the joint physical-water and outcome commit is pending.",
                operation.IntensityBefore);

        if (operation.Phase != EnvironmentalFireSuppressionPhase.Prepared)
        {
            return ResultAfterCommittedCancellation(operation);
        }

        if (operation.Command.Mode == EnvironmentalFireSuppressionMode.Water
            && water.TryGetPending(
                operation.Command.OperationId,
                operation.Command.WaterLeaseId,
                out EnvironmentalFireWaterReceipt pending))
        {
            RequireMatchingReceipt(operation.Command, pending);
            operation.SetWaterCommitted(pending);
            BumpVersion();
            return ResultAfterCommittedCancellation(operation);
        }

        operation.Cancel("Cancelled before any physical water commit or work contribution.");
        BumpVersion();
        return SuppressionFailure(
            EnvironmentalFireSuppressionDisposition.Cancelled,
            operation.ResultReason,
            operation.IntensityBefore);
    }

    public IReadOnlyList<EnvironmentalFireSuppressionResult>
        ResumePendingSuppressions()
    {
        if (restoreTransactionActive) throw new InvalidOperationException("Fire recovery is unavailable during restore.");
        ResumePendingOutcomeCommits();
        var results = new List<EnvironmentalFireSuppressionResult>();
        foreach (var operation in suppressions.Values.OrderBy(value => value.Command.OperationId, StringComparer.Ordinal).ToArray())
        {
            if (operation.Phase == EnvironmentalFireSuppressionPhase.Cancelled) continue;
            if (operation.Phase != EnvironmentalFireSuppressionPhase.Applied)
                throw new InvalidOperationException("A suppression without a pending publication has not applied its contribution.");
            TryAcknowledgeSuppressionWater(operation); EnsureCompletionIdentity(operation); results.Add(ResultForKnownOperation(operation));
        }
        return results;
    }

    private void Step(TickCounters report)
    {
        FireState[] scheduled = activeById.Values
            .OrderBy(value => value.FireId, StringComparer.Ordinal)
            .ToArray();
        foreach (FireState fire in scheduled)
        {
            if (!activeById.ContainsKey(fire.FireId))
                continue;

            if (!targets.TryGetTarget(fire.Target, out var target)
                || !target.Exists)
            {
                EndFire(fire, EnvironmentalFireEndReason.TargetLost);
                report.EndedFires++;
                continue;
            }

            if (!target.CanBurn)
            {
                EndFire(
                    fire,
                    EnvironmentalFireEndReason.TargetNoLongerCombustible);
                report.EndedFires++;
                continue;
            }

            fire.Position = target.Position;
            EnvironmentalFireTargetRef[] exposedCharacters = exposures?
                .GetExposedCharacters(fire.Target, fire.Position)?
                .Where(value => value.IsValid
                    && value.Kind == EnvironmentalFireTargetKind.Character)
                .Distinct()
                .OrderBy(value => value.TargetId, StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<EnvironmentalFireTargetRef>();
            string operationId = string.Concat(
                "environmental-fire-damage:",
                fire.FireId,
                ":",
                fire.StepIndex);
            var command = new EnvironmentalFireDamageCommand(
                operationId,
                fire.FireId,
                fire.Target,
                fire.Position,
                fire.Intensity,
                target.Profile.DamagePerTick * fire.Intensity);
            if (!damage.TryApply(command, out EnvironmentalFireDamageResult applied)
                || !applied.Committed)
            {
                report.Failures.Add(string.Concat(
                    operationId,
                    ":",
                    NormalizeReason(
                        applied.FailureReason,
                        "The authoritative fire damage port rejected the tick.")));
                continue;
            }

            fire.TotalDamage += applied.AppliedDamage;
            foreach (EnvironmentalFireTargetRef character in exposedCharacters)
            {
                string exposureOperationId = string.Concat(
                    "environmental-fire-exposure-damage:",
                    fire.FireId,
                    ":",
                    fire.StepIndex,
                    ":",
                    character.TargetId);
                var exposureCommand = new EnvironmentalFireDamageCommand(
                    exposureOperationId,
                    fire.FireId,
                    character,
                    fire.Position,
                    fire.Intensity,
                    target.Profile.DamagePerTick * fire.Intensity);
                if (!damage.TryApply(
                        exposureCommand,
                        out EnvironmentalFireDamageResult exposureApplied)
                    || !exposureApplied.Committed)
                {
                    report.Failures.Add(string.Concat(
                        exposureOperationId,
                        ":",
                        NormalizeReason(
                            exposureApplied.FailureReason,
                            "The authoritative body-damage port rejected the fire exposure.")));
                    continue;
                }

                fire.TotalDamage += exposureApplied.AppliedDamage;
                report.DamageCommits++;
            }
            fire.StepIndex++;
            fire.RemainingFuel = Math.Max(
                0f,
                fire.RemainingFuel
                - target.Profile.FuelConsumedPerTick * fire.Intensity);
            fire.Intensity = Math.Min(
                target.Profile.MaximumIntensity,
                fire.Intensity + target.Profile.GrowthPerTick);
            report.DamageCommits++;
            BumpVersion();

            if (!applied.TargetRemainsCombustible)
            {
                EndFire(
                    fire,
                    EnvironmentalFireEndReason.TargetNoLongerCombustible);
                report.EndedFires++;
                continue;
            }

            if (fire.RemainingFuel <= Epsilon)
            {
                EndFire(fire, EnvironmentalFireEndReason.FuelExhausted);
                report.EndedFires++;
                continue;
            }

            report.NewFires += TrySpread(fire, target.Profile);
        }
    }

    private int TrySpread(FireState source, EnvironmentalFireProfile sourceProfile)
    {
        if (source.Intensity + Epsilon < sourceProfile.MinimumSpreadIntensity)
            return 0;

        IReadOnlyList<EnvironmentalFireTargetSnapshot> raw =
            targets.GetAdjacentTargets(source.Target)
            ?? throw new InvalidOperationException(
                "The environmental fire adjacency port returned null.");
        EnvironmentalFireTargetSnapshot[] adjacent = raw
            .Where(value => value.Target.IsValid)
            .GroupBy(value => value.Target)
            .Select(group => group.First())
            .OrderBy(value => (int)value.Target.Kind)
            .ThenBy(value => value.Target.TargetId, StringComparer.Ordinal)
            .ToArray();
        int created = 0;
        foreach (EnvironmentalFireTargetSnapshot candidate in adjacent)
        {
            if (candidate.Target.Kind != EnvironmentalFireTargetKind.Building
                || !candidate.CanBurn
                || !candidate.Accepts(EnvironmentalFireIgnitionKind.Spread)
                || fireByTarget.ContainsKey(candidate.Target))
                continue;
            if (!spreadRandom.Chance(sourceProfile.SpreadChancePerTick))
                continue;

            string causeId = string.Concat(
                "environmental-fire-spread:",
                source.FireId,
                ":",
                source.StepIndex,
                ":",
                (int)candidate.Target.Kind,
                ":",
                candidate.Target.TargetId);
            created += TryIgniteSpread(
                causeId,
                source,
                candidate,
                source.Intensity * sourceProfile.SpreadIgnitionMultiplier)
                ? 1
                : 0;
        }

        return created;
    }

    private bool TryIgniteSpread(
        string causeId,
        FireState source,
        EnvironmentalFireTargetSnapshot target,
        float requestedIntensity)
    {
        if (causes.ContainsKey(causeId) || fireByTarget.ContainsKey(target.Target)) return false;
        var request = new EnvironmentalFireIgnitionRequest(causeId, EnvironmentalFireIgnitionKind.Spread, source.FireId,
            target.Target, requestedIntensity, string.Concat("spread-step:", source.StepIndex), targetDisplayName: target.DisplayName);
        if (!request.IsValid) throw new InvalidOperationException("Fire spread requires an immutable target name.");
        return IgnitePrepared(request, target, "Deterministic direct-neighbour spread accepted.").Created;
    }

    public DungeonEnvironmentalFireSaveData Capture()
    {
        var result = new DungeonEnvironmentalFireSaveData
        {
            nextFireSequence = nextFireSequence,
            nextOutcomeSequence = nextOutcomeSequence,
            accumulator = accumulator
        };
        foreach (FireState fire in activeById.Values.OrderBy(
                     value => value.FireId,
                     StringComparer.Ordinal))
        {
            result.activeFires.Add(ToSaveRecord(fire));
        }

        foreach (EnvironmentalFireHistorySnapshot entry in history)
        {
            result.history.Add(new EnvironmentalFireHistorySaveRecord
            {
                fire = ToSaveRecord(entry.Fire),
                endReason = (int)entry.EndReason
            });
        }

        foreach (KeyValuePair<string, IgnitionRecord> entry in causes.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            result.processedCauses.Add(entry.Value.Capture());
        }

        foreach (SuppressionOperation operation in suppressions.Values.OrderBy(
                     value => value.Command.OperationId,
                     StringComparer.Ordinal))
        {
            result.suppressionOperations.Add(
                ToSaveRecord(operation));
        }

        return result;
    }

    public EnvironmentalFireRestoreCandidate PrepareRestore(
        DungeonEnvironmentalFireSaveData data) =>
        EnvironmentalFireRestoreRules.Prepare(data, settings.TickInterval);

    public void Restore(EnvironmentalFireRestoreCandidate candidate)
    {
        if (candidate == null) throw new ArgumentNullException(nameof(candidate));
        DungeonEnvironmentalFireSaveData data = candidate.Capture();
        var restored = new RuntimeState
        {
            NextFire = data.nextFireSequence,
            NextOutcome = data.nextOutcomeSequence,
            Accumulator = data.accumulator
        };
        foreach (EnvironmentalFireSaveRecord record in data.activeFires)
        {
            FireState state = FromSaveRecord(record);
            restored.Active.Add(state.FireId, state);
            restored.ByTarget.Add(state.Target, state.FireId);
        }

        foreach (EnvironmentalFireHistorySaveRecord record in data.history)
        {
            restored.History.Add(new EnvironmentalFireHistorySnapshot
            {
                Fire = CloneSnapshot(FromSaveRecord(record.fire)),
                EndReason = (EnvironmentalFireEndReason)record.endReason
            });
        }

        foreach (EnvironmentalFireCauseSaveRecord record in data.processedCauses)
        {
            restored.Causes.Add(
                record.causeId,
                new IgnitionRecord(record));
        }

        foreach (EnvironmentalFireSuppressionSaveRecord record
                 in data.suppressionOperations)
        {
            SuppressionOperation operation =
                SuppressionOperation.FromSaveRecord(record);
            restored.Suppressions.Add(operation.Command.OperationId, operation);
        }

        if (restoreTransactionActive)
        {
            stagedRestoreState = restored;
            return;
        }
        state = restored;
        BumpVersion();
    }

    public string ParticipantId => "environment.fire";

    public void BeginRestoreCandidate()
    {
        if (restoreTransactionActive)
            throw new InvalidOperationException("Environmental fire restore is already active.");
        restoreTransactionActive = true;
    }

    public void PublishRestoreCandidate()
    {
        if (!restoreTransactionActive || stagedRestoreState == null)
            throw new InvalidOperationException("Environmental fire restore candidate is missing.");
        previousRestoreState = state;
        previousVersion = version;
        state = stagedRestoreState;
        stagedRestoreState = null;
        BumpVersion();
    }

    public void RollbackPublishedRestoreCandidate() => DiscardRestoreCandidate();

    public void DiscardRestoreCandidate()
    {
        if (previousRestoreState != null)
        {
            state = previousRestoreState;
            version = previousVersion;
        }
        CompleteRestoreCandidate();
    }

    public void CompleteRestoreCandidate()
    {
        stagedRestoreState = previousRestoreState = null;
        restoreTransactionActive = false;
    }

    private bool TryValidateSuppression(
        EnvironmentalFireSuppressionCommand command,
        out FireState fire,
        out EnvironmentalFireTargetSnapshot target,
        out EnvironmentalFireSuppressionResult failure)
    {
        if (!activeById.TryGetValue(command.FireId, out fire))
        {
            target = default;
            failure = SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.FireMissing,
                "The fire is no longer active.");
            return false;
        }

        if (causes.TryGetValue(fire.CauseId, out var ignition) && ignition.OutcomePending)
        {
            target = default;
            failure = SuppressionFailure(EnvironmentalFireSuppressionDisposition.OperationInProgress, "The ignition is still reconciling its committed inputs.");
            return false;
        }

        if (!targets.TryGetTarget(fire.Target, out target) || !target.Exists)
        {
            EndFire(fire, EnvironmentalFireEndReason.TargetLost);
            failure = SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.FireMissing,
                "The fire target no longer exists.");
            return false;
        }

        if (!target.CanBurn)
        {
            EndFire(fire, EnvironmentalFireEndReason.TargetNoLongerCombustible);
            failure = SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.FireMissing,
                "The target is no longer combustible.");
            return false;
        }

        if (!access.CanSuppress(
                command.WorkerId,
                command.StandPosition,
                fire.Target,
                out string accessFailure))
        {
            failure = SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.AccessBlocked,
                NormalizeReason(
                    accessFailure,
                    "A safe directly-adjacent work position is required."),
                fire.Intensity);
            return false;
        }

        if (fire.RequiresElectricalIsolation)
        {
            if (!electricalSafety.TryGetSafety(fire.Target, out var safety)
                || !safety.Target.Equals(fire.Target)
                || !safety.IsolationConfirmed)
            {
                failure = SuppressionFailure(
                    EnvironmentalFireSuppressionDisposition
                        .ElectricalIsolationRequired,
                    "The authoritative power state has not confirmed breaker/disconnect isolation and discharged stored power.",
                    fire.Intensity);
                return false;
            }
        }

        failure = default;
        return true;
    }

    private EnvironmentalFireSuppressionResult CommitSuppressionOutcome(
        EnvironmentalFireSuppressionCommand command,
        FireState fire,
        EnvironmentalFireTargetSnapshot target,
        float reduction,
        bool commitWater)
    {
        var predicted = PredictSuppression(command, fire.Intensity, reduction, commitWater ? command.WaterQuantity : 0, string.Empty);
        long revision = RequireNextOutcomeRevision();
        var fact = EnvironmentalFireSuppressedFact.Create(command, predicted, target.Target.TargetId, target.Position, calendar.Day, revision);
        var prepared = PrepareFact(fact, revision);
        FireState before = CloneState(fire);
        int versionBefore = version;
        var operation = SuppressionOperation.Create(command, fire.Intensity, revision, calendar.Day, target.Target.TargetId);
        operation.Position = target.Position; operation.PublicationKey = prepared.OperationKey; operation.MarkOutcomePending();
        suppressions.Add(command.OperationId, operation); nextOutcomeSequence = checked(revision + 1); BumpVersion();
        EnvironmentalFireWaterReceipt receipt = default;
        if (commitWater)
        {
            bool consumed; string failure;
            try
            {
                consumed = water.TryCommitReservedWaterPending(command.WaterLeaseId, command.WaterQuantity, command.OperationId,
                    command.WorkerId, command.FireId, command.StandPosition, target.Position, out receipt, out failure);
                if (consumed) RequireMatchingReceipt(command, receipt);
            }
            catch { prepared.Cancel(); throw; } // Unknown water debit stays Prepared for owner reconciliation.
            if (!consumed)
            {
                prepared.Cancel(); suppressions.Remove(command.OperationId); nextOutcomeSequence = revision; version = versionBefore;
                return SuppressionFailure(EnvironmentalFireSuppressionDisposition.WaterUnavailable,
                    NormalizeReason(failure, "Physical water was unavailable."), fire.Intensity);
            }
            operation.SetWaterCommitted(receipt);
        }
        try { fire.Position = target.Position; ApplySuppressionContribution(operation, fire, reduction, receipt.Quantity, receipt.CommitId); }
        catch
        {
            prepared.Cancel(); RestoreFireState(before);
            if (receipt.IsCommitted) { operation.ResetForOutcomeRetry(); operation.MarkOutcomePending(); BumpVersion(); }
            else { suppressions.Remove(command.OperationId); nextOutcomeSequence = revision; version = versionBefore; }
            throw;
        }
        var delivery = CommitFact(prepared);
        if (!delivery.MayBeCommitted && !receipt.IsCommitted)
        {
            RestoreFireState(before); suppressions.Remove(command.OperationId); nextOutcomeSequence = revision; version = versionBefore;
            throw new InvalidOperationException("Fire suppression observation rejected: " + delivery.Detail);
        }
        // Irreversible water or ambiguous publication retains Applied; replay cannot apply it again.
        ConfirmSuppression(operation, delivery);
        TryAcknowledgeSuppressionWater(operation); EnsureCompletionIdentity(operation);
        return new EnvironmentalFireSuppressionResult(operation.ResultDisposition, operation.IntensityBefore,
            operation.IntensityAfter, operation.WaterReceipt.Quantity, operation.WaterReceipt.CommitId,
            operation.ResultReason, operation.PublicationKey);
    }

    private void RestoreFireState(FireState before)
    {
        history.RemoveAll(entry => entry?.Fire != null
            && string.Equals(
                entry.Fire.FireId,
                before.FireId,
                StringComparison.Ordinal));
        activeById[before.FireId] = before;
        fireByTarget[before.Target] = before.FireId;
    }

    private static FireState CloneState(FireState source) => new()
    {
        FireId = source.FireId,
        CauseId = source.CauseId,
        IgnitionKind = source.IgnitionKind,
        ProducerId = source.ProducerId,
        EvidenceId = source.EvidenceId,
        Target = source.Target,
        Position = source.Position,
        Intensity = source.Intensity,
        RemainingFuel = source.RemainingFuel,
        StepIndex = source.StepIndex,
        TotalDamage = source.TotalDamage,
        TotalSuppressionWork = source.TotalSuppressionWork,
        TotalWaterConsumed = source.TotalWaterConsumed,
        RequiresElectricalIsolation = source.RequiresElectricalIsolation,
        FuelLossReceipt = source.FuelLossReceipt,
        FuelLossRequest = source.FuelLossRequest,
        FuelLossAcknowledged = source.FuelLossAcknowledged
    };

    private EnvironmentalFireSuppressionResult ApplySuppressionContribution(
        SuppressionOperation operation,
        FireState fire,
        float reduction,
        int waterConsumed,
        string waterCommitId)
    {
        if (!IsFinite(reduction) || reduction <= 0f)
            throw new InvalidOperationException(
                "The authored fire suppression contribution must be positive.");

        float before = fire.Intensity;
        fire.Intensity = Math.Max(0f, fire.Intensity - reduction);
        fire.TotalSuppressionWork += operation.Command.ApprovedWork;
        fire.TotalWaterConsumed += Math.Max(0, waterConsumed);
        var disposition = fire.Intensity <= Epsilon
            ? EnvironmentalFireSuppressionDisposition.Extinguished
            : EnvironmentalFireSuppressionDisposition.Applied;
        operation.MarkApplied(
            before,
            fire.Intensity,
            disposition,
            disposition == EnvironmentalFireSuppressionDisposition.Extinguished
                ? "The fire was extinguished."
                : "Suppression contribution applied.");
        if (disposition == EnvironmentalFireSuppressionDisposition.Extinguished)
            EndFire(fire, EnvironmentalFireEndReason.Suppressed);
        else
            BumpVersion();

        if (operation.Command.Mode != EnvironmentalFireSuppressionMode.Water)
        {
            return new EnvironmentalFireSuppressionResult(
                disposition,
                before,
                operation.IntensityAfter,
                0,
                string.Empty,
                operation.ResultReason);
        }
        return new EnvironmentalFireSuppressionResult(
            disposition,
            before,
            operation.IntensityAfter,
            waterConsumed,
            waterCommitId,
            operation.ResultReason);
    }

    private EnvironmentalFireIgnitionResult CommitIgnitionOutcome(
        EnvironmentalFireIgnitionRequest request,
        EnvironmentalFireIgnitionDisposition disposition,
        string fireId,
        string reason,
        Vector2Int position,
        bool hasLocation)
    {
        var result = new EnvironmentalFireIgnitionResult(disposition, fireId, reason);
        long revision = RequireNextOutcomeRevision();
        var fact = EnvironmentalFireIgnitedFact.Create(request, result, position, calendar.Day, revision, 0, hasLocation);
        var prepared = PrepareFact(fact, revision);
        int versionBefore = version;
        var owner = new IgnitionRecord(fact, prepared.OperationKey);
        causes.Add(request.CauseId, owner); nextOutcomeSequence = checked(revision + 1); BumpVersion();
        var delivery = CommitFact(prepared);
        if (!delivery.MayBeCommitted)
        {
            causes.Remove(request.CauseId); nextOutcomeSequence = revision; version = versionBefore;
            throw new InvalidOperationException("Fire ignition decision publication rejected: " + delivery.Detail);
        }
        ConfirmIgnition(owner, delivery);
        return result;
    }

    private void ResumePendingOutcomeCommits()
    {
        foreach (var entry in causes.Values.Where(value => value.OutcomePending).ToArray())
            ResumePendingIgnitionOutcome(entry.Fact.ToRequest(), entry);
        foreach (var operation in suppressions.Values.Where(value => value.OutcomePending).ToArray())
            ResumePendingSuppressionOutcome(operation);
        ResumePendingFuelLosses();
    }

    private void ResumePendingIgnitionOutcome(
        EnvironmentalFireIgnitionRequest request,
        IgnitionRecord pending)
    {
        if (pending.Fingerprint != request.Fingerprint) throw new InvalidOperationException("Fire ignition frozen request changed.");
        if (!pending.OutcomePending) return;
        var frozen = pending.Fact.ToRequest();
        if (frozen.FuelLoss != null && pending.Disposition == EnvironmentalFireIgnitionDisposition.Ignited)
        {
            if (!activeById.TryGetValue(pending.FireId, out var owner)) throw new InvalidOperationException("A pending fuel ignition lost its active owner.");
            if (!owner.FuelLossReceipt.IsCommitted)
            {
                string operation = "environmental-fire-fuel-loss:" + frozen.CauseId;
                if (fuelLoss == null) throw new InvalidOperationException("Pending fire fuel port is unavailable.");
                if (!fuelLoss.TryCommitPending(frozen.FuelLoss, operation, out var receipt, out string failure))
                    throw new InvalidOperationException("Pending fire fuel is unavailable: " + failure);
                if (!MatchesFuelLoss(frozen.FuelLoss, operation, receipt)) throw new InvalidOperationException("Pending fire fuel receipt changed.");
                owner.FuelLossReceipt = receipt; BumpVersion();
            }
        }
        ConfirmIgnition(pending, ReplayFact(pending.PublicationKey, pending.Fact));
    }

    private void ResumePendingSuppressionOutcome(SuppressionOperation pending)
    {
        if (!pending.OutcomePending) return;
        if (pending.Phase == EnvironmentalFireSuppressionPhase.Prepared || pending.Phase == EnvironmentalFireSuppressionPhase.WaterCommitted)
        {
            if (!TryValidateSuppression(pending.Command, out var fire, out var target, out var failed))
                throw new InvalidOperationException("Pending suppression cannot resume: " + failed.Reason);
            if (pending.Command.Mode == EnvironmentalFireSuppressionMode.Water && pending.Phase == EnvironmentalFireSuppressionPhase.Prepared)
            {
                if (!water.TryGetPending(pending.Command.OperationId, pending.Command.WaterLeaseId, out var receipt))
                {
                    if (!water.TryCommitReservedWaterPending(pending.Command.WaterLeaseId, pending.Command.WaterQuantity,
                        pending.Command.OperationId, pending.Command.WorkerId, pending.Command.FireId, pending.Command.StandPosition,
                        target.Position, out receipt, out string failure))
                        throw new InvalidOperationException("Pending suppression water unavailable: " + failure);
                }
                RequireMatchingReceipt(pending.Command, receipt); pending.SetWaterCommitted(receipt); BumpVersion();
            }
            float reduction = pending.Command.Mode == EnvironmentalFireSuppressionMode.InitialAttack
                ? pending.Command.ApprovedWork * settings.InitialAttackSuppressionPerWork
                : pending.WaterReceipt.Quantity * target.Profile.WaterSuppressionPerUnit;
            var predicted = PredictSuppression(pending.Command, fire.Intensity, reduction, pending.WaterReceipt.Quantity, pending.WaterReceipt.CommitId);
            var fact = EnvironmentalFireSuppressedFact.Create(pending.Command, predicted, pending.TargetId, target.Position, pending.AbsoluteDay, pending.OutcomeRevision);
            var stage = PrepareFact(fact, pending.OutcomeRevision);
            if (stage.OperationKey != pending.PublicationKey)
            { stage.Cancel(); throw new InvalidOperationException("Pending suppression publication identity changed."); }
            FireState before = CloneState(fire);
            try
            {
                pending.Position = target.Position;
                ApplySuppressionContribution(pending, fire, reduction, pending.WaterReceipt.Quantity, pending.WaterReceipt.CommitId);
            }
            catch { stage.Cancel(); RestoreFireState(before); pending.ResetForOutcomeRetry(); pending.MarkOutcomePending(); throw; }
            ConfirmSuppression(pending, CommitFact(stage));
        }
        else if (pending.Phase == EnvironmentalFireSuppressionPhase.Applied)
            ConfirmSuppression(pending, ReplayFact(pending.PublicationKey, new EnvironmentalFireSuppressedFact(ToSaveRecord(pending))));
        else throw new InvalidOperationException("A pending suppression has no recoverable domain phase.");
        TryAcknowledgeSuppressionWater(pending);
    }

    private void TryAcknowledgeSuppressionWater(SuppressionOperation operation)
    {
        if (operation.Command.Mode != EnvironmentalFireSuppressionMode.Water
            || operation.WaterAcknowledged
            || !operation.WaterReceipt.IsCommitted)
            return;
        if (water.TryAcknowledge(
                operation.WaterReceipt.CommitId,
                out string failureReason))
        {
            operation.MarkAcknowledged();
            return;
        }
        operation.SetResult(
            EnvironmentalFireSuppressionDisposition
                .AppliedAwaitingWaterAcknowledgement,
            NormalizeReason(
                failureReason,
                "Water contribution is applied; acknowledgement remains pending."));
    }

    private EnvironmentalFireIgnitionResult IgnitePrepared(EnvironmentalFireIgnitionRequest request, EnvironmentalFireTargetSnapshot target, string reason)
    {
        if (nextFireSequence <= 0 || nextFireSequence == long.MaxValue)
            throw new InvalidOperationException("Fire identity sequence is exhausted.");
        long sequence = nextFireSequence, revision = RequireNextOutcomeRevision();
        string id = "environmental-fire:" + sequence;
        var result = new EnvironmentalFireIgnitionResult(EnvironmentalFireIgnitionDisposition.Ignited, id, reason);
        var fact = EnvironmentalFireIgnitedFact.Create(request, result, target.Position, calendar.Day, revision,
            Math.Min(request.IgnitionIntensity, target.Profile.MaximumIntensity));
        var prepared = PrepareFact(fact, revision);
        var fire = new FireState
        {
            FireId = id, CauseId = request.CauseId, IgnitionKind = request.Kind, ProducerId = request.ProducerId,
            EvidenceId = request.EvidenceId, Target = request.Target, Position = target.Position,
            Intensity = Math.Min(request.IgnitionIntensity, target.Profile.MaximumIntensity), RemainingFuel = target.Profile.FuelCapacity,
            RequiresElectricalIsolation = target.HasElectricalHazard || request.Kind == EnvironmentalFireIgnitionKind.ElectricalFault,
            FuelLossRequest = request.FuelLoss
        };
        int priorVersion = version;
        var owner = new IgnitionRecord(fact, prepared.OperationKey);
        activeById.Add(id, fire); fireByTarget.Add(fire.Target, id); causes.Add(request.CauseId, owner);
        nextFireSequence = checked(sequence + 1); nextOutcomeSequence = checked(revision + 1); BumpVersion();
        if (request.FuelLoss != null)
        {
            bool consumed; string failure;
            try
            {
                if (fuelLoss == null) throw new InvalidOperationException("Fire fuel port is unavailable.");
                consumed = fuelLoss.TryCommitPending(request.FuelLoss, "environmental-fire-fuel-loss:" + request.CauseId, out var receipt, out failure);
                if (consumed)
                {
                    if (!MatchesFuelLoss(request.FuelLoss, "environmental-fire-fuel-loss:" + request.CauseId, receipt))
                        throw new InvalidOperationException("Fire fuel receipt mismatch.");
                    fire.FuelLossReceipt = receipt;
                }
            }
            catch { prepared.Cancel(); throw; } // The fuel owner can have committed; preserve the pending fire.
            if (!consumed)
            {
                prepared.Cancel(); RollbackIgnition();
                return CommitIgnitionOutcome(request, EnvironmentalFireIgnitionDisposition.FuelUnavailable, string.Empty,
                    NormalizeReason(failure, "The exact physical fire fuel could not be sunk."), target.Position, true);
            }
        }
        var delivery = CommitFact(prepared);
        if (!delivery.MayBeCommitted && !fire.FuelLossReceipt.IsCommitted)
        {
            RollbackIgnition();
            throw new InvalidOperationException("Fire ignition observation rejected: " + delivery.Detail);
        }
        ConfirmIgnition(owner, delivery);
        if (fire.FuelLossReceipt.IsCommitted && fuelLoss.TryAcknowledge(fire.FuelLossReceipt.CommitId, out _))
        { fire.FuelLossAcknowledged = true; BumpVersion(); }
        return result;
        void RollbackIgnition()
        {
            activeById.Remove(id); fireByTarget.Remove(fire.Target); causes.Remove(request.CauseId);
            nextFireSequence = sequence; nextOutcomeSequence = revision; version = priorVersion;
        }
    }

    private IDomainFactStage PrepareFact<TFact>(TFact fact, long revision)
    {
        if (!facts.TryStage(fact, out var stage, out string failure)) throw new InvalidOperationException("Fire observation prepare failed: " + failure);
        if (stage == null || !stage.IsActive || stage.IsReplay || stage.OwnerRevision != revision
            || !DomainFactValueSyntax.IsText(stage.OperationKey, 2048, false))
        { stage?.Cancel(); throw new InvalidOperationException("Fire observation reservation is invalid."); }
        return stage;
    }
    private static DomainFactDelivery CommitFact(IDomainFactStage stage)
    {
        try { return stage.Commit(); }
        catch (Exception exception) when (IsRecoverableFactFailure(exception))
        { return DomainFactDelivery.Uncertain("fire-observation-response-unknown:" + exception.Message); }
        finally { stage.Cancel(); }
    }
    private DomainFactDelivery ReplayFact<TFact>(string key, TFact fact)
    {
        try
        {
            var results = new DomainFactDelivery[1];
            if (!recovery.ReplayBatch(new[] { DomainFactReplay.Create(key, fact) }, results, out string failure))
                return DomainFactDelivery.Uncertain("fire-observation-recovery-pending:" + failure);
            return results[0];
        }
        catch (Exception exception) when (IsRecoverableFactFailure(exception))
        { return DomainFactDelivery.Uncertain("fire-observation-recovery-failed:" + exception.Message); }
    }
    private static bool IsRecoverableFactFailure(Exception exception) => exception is not OutOfMemoryException and not StackOverflowException and not AccessViolationException;
    private static bool Delivered(DomainFactDelivery delivery, string key) => delivery.Completed && delivery.OperationKey == key
        && !string.IsNullOrEmpty(delivery.PublicationId) && !string.IsNullOrEmpty(delivery.Fingerprint);
    private void ConfirmIgnition(IgnitionRecord owner, DomainFactDelivery delivery)
    {
        if (!Delivered(delivery, owner.PublicationKey))
        { owner.MarkOutcomePending(); throw new InvalidOperationException("Fire ignition publication remains pending: " + delivery.Detail); }
        owner.MarkOutcomeCommitted(); BumpVersion();
    }
    private void ConfirmSuppression(SuppressionOperation owner, DomainFactDelivery delivery)
    {
        if (!Delivered(delivery, owner.PublicationKey))
        { owner.MarkOutcomePending(); throw new InvalidOperationException("Fire suppression publication remains pending: " + delivery.Detail); }
        owner.MarkOutcomeCommitted(); BumpVersion();
    }
    private static EnvironmentalFireSuppressionResult PredictSuppression(EnvironmentalFireSuppressionCommand command,
        float before, float reduction, int water, string waterCommit)
    {
        float after = Math.Max(0, before - reduction);
        return new EnvironmentalFireSuppressionResult(after <= Epsilon ? EnvironmentalFireSuppressionDisposition.Extinguished
            : EnvironmentalFireSuppressionDisposition.Applied, before, after, water, waterCommit,
            after <= Epsilon ? "The fire was extinguished." : "Suppression contribution applied.");
    }

    private long RequireNextOutcomeRevision()
    {
        if (nextOutcomeSequence <= 0L || nextOutcomeSequence == long.MaxValue)
            throw new InvalidOperationException(
                "Environmental fire outcome sequence is exhausted or invalid.");
        return nextOutcomeSequence;
    }

    private void EndFire(FireState fire, EnvironmentalFireEndReason reason)
    {
        if (!activeById.Remove(fire.FireId))
            return;
        fireByTarget.Remove(fire.Target);
        history.Add(new EnvironmentalFireHistorySnapshot
        {
            Fire = CloneSnapshot(fire),
            EndReason = reason
        });
        BumpVersion();
    }

    private void EnsureCompletionIdentity(SuppressionOperation operation)
    {
        var saved = ToSaveRecord(operation);
        if (!EnvironmentalFireSuppressionCompletedFact.RequiresCompletion(saved) || operation.OutcomePending || operation.CompletionIdentityAcknowledged) return;
        var fact = new EnvironmentalFireSuppressionCompletedFact(saved);
        DomainFactDelivery delivery;
        DomainFactLease lease = default;
        try
        {
            if (string.IsNullOrEmpty(operation.CompletionPublicationKey))
            {
                if (!completionFacts.TryReserve(fact, out lease, out string failure))
                { LastCompletionIdentityFailureForDebug = "fire-completion-publication-pending:" + failure; return; }
                operation.CompletionPublicationKey = lease.PublicationKey; BumpVersion();
            }
            delivery = lease.IsValid ? completionFacts.Publish(lease, fact) : ReplayFact(operation.CompletionPublicationKey, fact);
        }
        catch (Exception exception) when (IsRecoverableFactFailure(exception)) { delivery = DomainFactDelivery.Uncertain(exception.Message); }
        finally { if (lease.IsValid) completionFacts.Cancel(lease); }
        if (!Delivered(delivery, operation.CompletionPublicationKey))
        { LastCompletionIdentityFailureForDebug = "fire-completion-publication-pending:" + delivery.Detail; return; }
        operation.MarkCompletionIdentityAcknowledged(); LastCompletionIdentityFailureForDebug = string.Empty; BumpVersion();
    }

    private EnvironmentalFireSuppressionResult ResultForKnownOperation(
        SuppressionOperation operation)
    {
        if (operation.Phase == EnvironmentalFireSuppressionPhase.Prepared
            || operation.Phase == EnvironmentalFireSuppressionPhase.WaterCommitted)
        {
            return SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.OperationInProgress,
                "The prepared suppression operation must be resumed.",
                operation.IntensityBefore);
        }

        if (operation.Phase == EnvironmentalFireSuppressionPhase.Cancelled)
        {
            return SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.Cancelled,
                operation.ResultReason,
                operation.IntensityBefore);
        }

        EnvironmentalFireSuppressionDisposition disposition =
            operation.ResultDisposition;
        if (disposition == EnvironmentalFireSuppressionDisposition.Applied
            || disposition == EnvironmentalFireSuppressionDisposition.Extinguished)
        {
            disposition = EnvironmentalFireSuppressionDisposition.PreviouslyApplied;
        }

        return new EnvironmentalFireSuppressionResult(
            disposition,
            operation.IntensityBefore,
            operation.IntensityAfter,
            operation.WaterReceipt.Quantity,
            operation.WaterReceipt.CommitId,
            operation.ResultReason,
            SuppressionOutcomeResultKey(operation));
    }

    private static string SuppressionOutcomeResultKey(
        SuppressionOperation operation)
    { return operation?.PublicationKey ?? string.Empty; }

    private static EnvironmentalFireSuppressionResult
        ResultAfterCommittedCancellation(SuppressionOperation operation) =>
        new(
            EnvironmentalFireSuppressionDisposition
                .CancellationRejectedAfterCommit,
            operation.IntensityBefore,
            operation.IntensityAfter,
            operation.WaterReceipt.Quantity,
            operation.WaterReceipt.CommitId,
            "Cancellation cannot refund committed water or applied work.");

    private static EnvironmentalFireSuppressionResult SuppressionFailure(
        EnvironmentalFireSuppressionDisposition disposition,
        string reason,
        float intensity = 0f) =>
        new(
            disposition,
            intensity,
            intensity,
            0,
            string.Empty,
            reason);

    private static bool ValidateWaterReceipt(
        EnvironmentalFireSuppressionCommand command,
        EnvironmentalFireWaterReceipt receipt,
        out string failureReason)
    {
        if (!receipt.IsCommitted
            || !string.Equals(
                receipt.OperationId,
                command.OperationId,
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.LeaseId,
                command.WaterLeaseId,
                StringComparison.Ordinal)
            || receipt.Quantity != command.WaterQuantity)
        {
            failureReason =
                "The physical-water receipt does not match the exact operation, lease, and quantity.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    private static void RequireMatchingReceipt(
        EnvironmentalFireSuppressionCommand command,
        EnvironmentalFireWaterReceipt receipt)
    {
        if (!ValidateWaterReceipt(command, receipt, out string failureReason))
            throw new InvalidOperationException(failureReason);
    }

    private void BumpVersion() => version = unchecked(version + 1);

    private void ResumePendingFuelLosses()
    {
        if (fuelLoss == null)
            return;

        foreach (FireState fire in activeById.Values
                     .Where(value => value.FuelLossReceipt.IsCommitted
                         && !value.FuelLossAcknowledged)
                     .OrderBy(value => value.FireId, StringComparer.Ordinal))
        {
            if (fuelLoss.TryAcknowledge(
                    fire.FuelLossReceipt.CommitId,
                    out _))
            {
                fire.FuelLossAcknowledged = true;
                BumpVersion();
            }
        }

        foreach (EnvironmentalFireHistorySnapshot entry in history
                     .Where(value => value?.Fire != null
                         && value.Fire.FuelLossQuantity > 0
                         && !value.Fire.FuelLossAcknowledged)
                     .OrderBy(value => value.Fire.FireId, StringComparer.Ordinal))
        {
            if (fuelLoss.TryAcknowledge(
                    entry.Fire.FuelLossCommitId,
                    out _))
            {
                entry.Fire.FuelLossAcknowledged = true;
                BumpVersion();
            }
        }
    }

    private static bool MatchesFuelLoss(
        EnvironmentalFireFuelLossRequest request,
        string operationId,
        EnvironmentalFireFuelLossReceipt receipt) =>
        request?.IsValid == true
        && receipt.IsCommitted
        && receipt.Target.Equals(request.Target)
        && receipt.Quantity == request.Quantity
        && string.Equals(
            receipt.OperationId,
            operationId,
            StringComparison.Ordinal);

    private static string NormalizeReason(string reason, string fallback) =>
        string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static EnvironmentalFireSnapshot CloneSnapshot(FireState fire) =>
        new()
        {
            FireId = fire.FireId,
            CauseId = fire.CauseId,
            IgnitionKind = fire.IgnitionKind,
            ProducerId = fire.ProducerId,
            EvidenceId = fire.EvidenceId,
            Target = fire.Target,
            Position = fire.Position,
            Intensity = fire.Intensity,
            RemainingFuel = fire.RemainingFuel,
            StepIndex = fire.StepIndex,
            TotalDamage = fire.TotalDamage,
            TotalSuppressionWork = fire.TotalSuppressionWork,
            TotalWaterConsumed = fire.TotalWaterConsumed,
            RequiresElectricalIsolation = fire.RequiresElectricalIsolation,
            FuelLossTarget = fire.FuelLossReceipt.IsCommitted
                ? fire.FuelLossReceipt.Target
                : fire.FuelLossRequest?.Target ?? default,
            FuelLossOperationId = fire.FuelLossReceipt.IsCommitted
                ? fire.FuelLossReceipt.OperationId
                : fire.FuelLossRequest == null
                    ? string.Empty
                    : string.Concat(
                        "environmental-fire-fuel-loss:",
                        fire.CauseId),
            FuelLossQuantity = fire.FuelLossReceipt.IsCommitted
                ? fire.FuelLossReceipt.Quantity
                : fire.FuelLossRequest?.Quantity ?? 0,
            FuelLossMassGrams = fire.FuelLossReceipt.InputMassGrams,
            FuelLossCommitId = fire.FuelLossReceipt.CommitId,
            FuelLossAcknowledged = fire.FuelLossAcknowledged
        };

    private static EnvironmentalFireSnapshot CloneSnapshot(
        EnvironmentalFireSnapshot fire) =>
        new()
        {
            FireId = fire.FireId,
            CauseId = fire.CauseId,
            IgnitionKind = fire.IgnitionKind,
            ProducerId = fire.ProducerId,
            EvidenceId = fire.EvidenceId,
            Target = fire.Target,
            Position = fire.Position,
            Intensity = fire.Intensity,
            RemainingFuel = fire.RemainingFuel,
            StepIndex = fire.StepIndex,
            TotalDamage = fire.TotalDamage,
            TotalSuppressionWork = fire.TotalSuppressionWork,
            TotalWaterConsumed = fire.TotalWaterConsumed,
            RequiresElectricalIsolation = fire.RequiresElectricalIsolation,
            FuelLossTarget = fire.FuelLossTarget,
            FuelLossOperationId = fire.FuelLossOperationId,
            FuelLossQuantity = fire.FuelLossQuantity,
            FuelLossMassGrams = fire.FuelLossMassGrams,
            FuelLossCommitId = fire.FuelLossCommitId,
            FuelLossAcknowledged = fire.FuelLossAcknowledged
        };

    private static EnvironmentalFireHistorySnapshot CloneHistory(
        EnvironmentalFireHistorySnapshot entry) =>
        new()
        {
            Fire = CloneSnapshot(entry.Fire),
            EndReason = entry.EndReason
        };

    private static EnvironmentalFireSaveRecord ToSaveRecord(FireState fire) =>
        new()
        {
            fireId = fire.FireId,
            causeId = fire.CauseId,
            ignitionKind = (int)fire.IgnitionKind,
            producerId = fire.ProducerId,
            evidenceId = fire.EvidenceId,
            targetKind = (int)fire.Target.Kind,
            targetId = fire.Target.TargetId,
            positionX = fire.Position.x,
            positionY = fire.Position.y,
            intensity = fire.Intensity,
            remainingFuel = fire.RemainingFuel,
            stepIndex = fire.StepIndex,
            totalDamage = fire.TotalDamage,
            totalSuppressionWork = fire.TotalSuppressionWork,
            totalWaterConsumed = fire.TotalWaterConsumed,
            requiresElectricalIsolation = fire.RequiresElectricalIsolation,
            fuelLossTargetKind = (int)(fire.FuelLossReceipt.IsCommitted
                ? fire.FuelLossReceipt.Target.Kind
                : fire.FuelLossRequest?.Target.Kind ?? default),
            fuelLossTargetId = fire.FuelLossReceipt.IsCommitted
                ? fire.FuelLossReceipt.Target.TargetId
                : fire.FuelLossRequest?.Target.TargetId ?? string.Empty,
            fuelLossOperationId = fire.FuelLossReceipt.IsCommitted
                ? fire.FuelLossReceipt.OperationId
                : fire.FuelLossRequest == null
                    ? string.Empty
                    : string.Concat(
                        "environmental-fire-fuel-loss:",
                        fire.CauseId),
            fuelLossQuantity = fire.FuelLossReceipt.IsCommitted
                ? fire.FuelLossReceipt.Quantity
                : fire.FuelLossRequest?.Quantity ?? 0,
            fuelLossMassGrams = fire.FuelLossReceipt.InputMassGrams,
            fuelLossCommitId = fire.FuelLossReceipt.CommitId,
            fuelLossAcknowledged = fire.FuelLossAcknowledged,
            fuelLossPending = fire.FuelLossRequest != null
                && !fire.FuelLossReceipt.IsCommitted
        };

    private static EnvironmentalFireSaveRecord ToSaveRecord(
        EnvironmentalFireSnapshot fire) =>
        new()
        {
            fireId = fire.FireId,
            causeId = fire.CauseId,
            ignitionKind = (int)fire.IgnitionKind,
            producerId = fire.ProducerId,
            evidenceId = fire.EvidenceId,
            targetKind = (int)fire.Target.Kind,
            targetId = fire.Target.TargetId,
            positionX = fire.Position.x,
            positionY = fire.Position.y,
            intensity = fire.Intensity,
            remainingFuel = fire.RemainingFuel,
            stepIndex = fire.StepIndex,
            totalDamage = fire.TotalDamage,
            totalSuppressionWork = fire.TotalSuppressionWork,
            totalWaterConsumed = fire.TotalWaterConsumed,
            requiresElectricalIsolation = fire.RequiresElectricalIsolation,
            fuelLossTargetKind = (int)fire.FuelLossTarget.Kind,
            fuelLossTargetId = fire.FuelLossTarget.TargetId,
            fuelLossOperationId = fire.FuelLossOperationId,
            fuelLossQuantity = fire.FuelLossQuantity,
            fuelLossMassGrams = fire.FuelLossMassGrams,
            fuelLossCommitId = fire.FuelLossCommitId,
            fuelLossAcknowledged = fire.FuelLossAcknowledged
        };

    private static FireState FromSaveRecord(EnvironmentalFireSaveRecord record) =>
        new()
        {
            FireId = record.fireId,
            CauseId = record.causeId,
            IgnitionKind = (EnvironmentalFireIgnitionKind)record.ignitionKind,
            ProducerId = record.producerId,
            EvidenceId = record.evidenceId,
            Target = new EnvironmentalFireTargetRef(
                (EnvironmentalFireTargetKind)record.targetKind,
                record.targetId),
            Position = new Vector2Int(record.positionX, record.positionY),
            Intensity = record.intensity,
            RemainingFuel = record.remainingFuel,
            StepIndex = record.stepIndex,
            TotalDamage = record.totalDamage,
            TotalSuppressionWork = record.totalSuppressionWork,
            TotalWaterConsumed = record.totalWaterConsumed,
            RequiresElectricalIsolation = record.requiresElectricalIsolation,
            FuelLossReceipt = record.fuelLossQuantity > 0
                && !record.fuelLossPending
                ? new EnvironmentalFireFuelLossReceipt(
                    new EnvironmentalFireTargetRef(
                        (EnvironmentalFireTargetKind)record.fuelLossTargetKind,
                        record.fuelLossTargetId),
                    record.fuelLossOperationId,
                    record.fuelLossQuantity,
                    record.fuelLossMassGrams,
                    record.fuelLossCommitId)
                : default,
            FuelLossRequest = record.fuelLossPending
                ? new EnvironmentalFireFuelLossRequest(
                    new EnvironmentalFireTargetRef(
                        (EnvironmentalFireTargetKind)record.fuelLossTargetKind,
                        record.fuelLossTargetId),
                    record.fuelLossQuantity)
                : null,
            FuelLossAcknowledged = record.fuelLossAcknowledged
        };

    private static EnvironmentalFireSuppressionSaveRecord ToSaveRecord(
        SuppressionOperation operation) =>
        new()
        {
            operationId = operation.Command.OperationId,
            fingerprint = operation.Fingerprint,
            fireId = operation.Command.FireId,
            workerId = operation.Command.WorkerId,
            standX = operation.Command.StandPosition.x,
            standY = operation.Command.StandPosition.y,
            mode = (int)operation.Command.Mode,
            approvedWork = operation.Command.ApprovedWork,
            waterLeaseId = operation.Command.WaterLeaseId,
            waterQuantity = operation.Command.WaterQuantity,
            phase = (int)operation.Phase,
            resultDisposition = (int)operation.ResultDisposition,
            intensityBefore = operation.IntensityBefore,
            intensityAfter = operation.IntensityAfter,
            waterCommitId = operation.WaterReceipt.CommitId,
            waterCommittedQuantity = operation.WaterReceipt.Quantity,
            waterAcknowledged = operation.WaterAcknowledged,
            resultReason = operation.ResultReason,
            workerDisplayName = operation.Command.WorkerDisplayName,
            targetId = operation.TargetId,
            positionX = operation.Position.x, positionY = operation.Position.y,
            publicationKey = operation.PublicationKey, completionPublicationKey = operation.CompletionPublicationKey,
            commandOrigin = (int)operation.Command.CommandOrigin,
            completionIdentityAcknowledged = operation.CompletionIdentityAcknowledged,
            outcomeRevision = operation.OutcomeRevision,
            absoluteDay = operation.AbsoluteDay,
            legacyPreOutcome = operation.LegacyPreOutcome,
            outcomePending = operation.OutcomePending
        };

    private sealed class FireState
    {
        public string FireId;
        public string CauseId;
        public EnvironmentalFireIgnitionKind IgnitionKind;
        public string ProducerId;
        public string EvidenceId;
        public EnvironmentalFireTargetRef Target;
        public Vector2Int Position;
        public float Intensity;
        public float RemainingFuel;
        public long StepIndex;
        public float TotalDamage;
        public float TotalSuppressionWork;
        public int TotalWaterConsumed;
        public bool RequiresElectricalIsolation;
        public EnvironmentalFireFuelLossReceipt FuelLossReceipt;
        public EnvironmentalFireFuelLossRequest FuelLossRequest;
        public bool FuelLossAcknowledged;
    }

    private sealed class IgnitionRecord
    {
        private readonly EnvironmentalFireCauseSaveRecord saved;
        public IgnitionRecord(EnvironmentalFireIgnitedFact fact, string key)
        { saved = fact.Snapshot(); saved.publicationKey = key; saved.outcomePending = true; }
        public IgnitionRecord(EnvironmentalFireCauseSaveRecord record)
        { _ = new EnvironmentalFireIgnitedFact(record); saved = record.Clone(); }
        public EnvironmentalFireIgnitedFact Fact => new(saved);
        public EnvironmentalFireCauseSaveRecord Capture() => saved.Clone();
        public string Fingerprint => saved.fingerprint;
        public EnvironmentalFireIgnitionDisposition Disposition => (EnvironmentalFireIgnitionDisposition)saved.disposition;
        public string FireId => saved.fireId;
        public string Reason => saved.reason;
        public string PublicationKey => saved.publicationKey;
        public bool OutcomePending => saved.outcomePending;
        public void MarkOutcomePending() => saved.outcomePending = true;
        public void MarkOutcomeCommitted() => saved.outcomePending = false;
    }

    private sealed class SuppressionOperation
    {
        private SuppressionOperation(
            EnvironmentalFireSuppressionCommand command,
            string fingerprint,
            EnvironmentalFireSuppressionPhase phase,
            EnvironmentalFireSuppressionDisposition resultDisposition,
            float intensityBefore,
            float intensityAfter,
            EnvironmentalFireWaterReceipt waterReceipt,
            bool waterAcknowledged,
            string resultReason,
            long outcomeRevision,
            int absoluteDay,
            bool legacyPreOutcome,
            bool outcomePending,
            string targetId,
            bool completionIdentityAcknowledged)
        {
            Command = command;
            Fingerprint = fingerprint;
            Phase = phase;
            ResultDisposition = resultDisposition;
            IntensityBefore = intensityBefore;
            IntensityAfter = intensityAfter;
            WaterReceipt = waterReceipt;
            WaterAcknowledged = waterAcknowledged;
            ResultReason = resultReason;
            OutcomeRevision = outcomeRevision;
            AbsoluteDay = absoluteDay;
            LegacyPreOutcome = legacyPreOutcome;
            OutcomePending = outcomePending;
            TargetId = targetId;
            CompletionIdentityAcknowledged = completionIdentityAcknowledged;
        }

        public EnvironmentalFireSuppressionCommand Command { get; }
        public string Fingerprint { get; }
        public EnvironmentalFireSuppressionPhase Phase { get; private set; }
        public EnvironmentalFireSuppressionDisposition ResultDisposition
            { get; private set; }
        public float IntensityBefore { get; private set; }
        public float IntensityAfter { get; private set; }
        public EnvironmentalFireWaterReceipt WaterReceipt { get; private set; }
        public bool WaterAcknowledged { get; private set; }
        public string ResultReason { get; private set; }
        public long OutcomeRevision { get; }
        public int AbsoluteDay { get; }
        public bool LegacyPreOutcome { get; }
        public bool OutcomePending { get; private set; }
        public string TargetId { get; }
        public Vector2Int Position { get; set; }
        public string PublicationKey { get; set; } = string.Empty;
        public string CompletionPublicationKey { get; set; } = string.Empty;
        public bool CompletionIdentityAcknowledged { get; private set; }
        public void MarkCompletionIdentityAcknowledged() => CompletionIdentityAcknowledged = true;

        public static SuppressionOperation Create(
            EnvironmentalFireSuppressionCommand command,
            float intensityBefore,
            long outcomeRevision,
            int absoluteDay,
            string targetId) =>
            new(
                command,
                command.Fingerprint,
                EnvironmentalFireSuppressionPhase.Prepared,
                EnvironmentalFireSuppressionDisposition.OperationInProgress,
                intensityBefore,
                intensityBefore,
                default,
                false,
                "Suppression operation prepared.",
                outcomeRevision,
                absoluteDay,
                false,
                false,
                targetId,
                false);

        public static SuppressionOperation FromSaveRecord(
            EnvironmentalFireSuppressionSaveRecord record)
        {
            var command = new EnvironmentalFireSuppressionCommand(
                record.operationId,
                record.fireId,
                record.workerId,
                new Vector2Int(record.standX, record.standY),
                (EnvironmentalFireSuppressionMode)record.mode,
                record.approvedWork,
                record.waterLeaseId,
                record.waterQuantity,
                record.workerDisplayName,
                (CharacterCommandOrigin)record.commandOrigin);
            var receipt = record.waterCommittedQuantity > 0
                ? new EnvironmentalFireWaterReceipt(
                    record.operationId,
                    record.waterLeaseId,
                    record.waterCommittedQuantity,
                    record.waterCommitId)
                : default;
            return new SuppressionOperation(
                command,
                record.fingerprint,
                (EnvironmentalFireSuppressionPhase)record.phase,
                (EnvironmentalFireSuppressionDisposition)record.resultDisposition,
                record.intensityBefore,
                record.intensityAfter,
                receipt,
                record.waterAcknowledged,
                record.resultReason,
                record.outcomeRevision,
                record.absoluteDay,
                record.legacyPreOutcome,
                record.outcomePending,
                record.targetId,
                record.completionIdentityAcknowledged)
            {
                Position = new Vector2Int(record.positionX, record.positionY), PublicationKey = record.publicationKey,
                CompletionPublicationKey = record.completionPublicationKey
            };
        }

        public void SetWaterCommitted(EnvironmentalFireWaterReceipt receipt)
        {
            WaterReceipt = receipt;
            Phase = EnvironmentalFireSuppressionPhase.WaterCommitted;
            ResultDisposition =
                EnvironmentalFireSuppressionDisposition.OperationInProgress;
            ResultReason = "Physical water is committed pending contribution.";
        }

        public void MarkApplied(
            float intensityBefore,
            float intensityAfter,
            EnvironmentalFireSuppressionDisposition disposition,
            string reason)
        {
            Phase = EnvironmentalFireSuppressionPhase.Applied;
            IntensityBefore = intensityBefore;
            IntensityAfter = intensityAfter;
            ResultDisposition = disposition;
            ResultReason = reason;
        }

        public void MarkAcknowledged()
        {
            WaterAcknowledged = true;
            ResultDisposition = IntensityAfter <= Epsilon
                ? EnvironmentalFireSuppressionDisposition.Extinguished
                : EnvironmentalFireSuppressionDisposition.Applied;
            ResultReason = IntensityAfter <= Epsilon
                ? "The fire was extinguished."
                : "Suppression contribution applied.";
        }

        public void MarkOutcomePending() => OutcomePending = true;
        public void MarkOutcomeCommitted() => OutcomePending = false;

        public void ResetForOutcomeRetry()
        {
            Phase = WaterReceipt.IsCommitted
                ? EnvironmentalFireSuppressionPhase.WaterCommitted
                : EnvironmentalFireSuppressionPhase.Prepared;
            IntensityAfter = IntensityBefore;
            ResultDisposition =
                EnvironmentalFireSuppressionDisposition.OperationInProgress;
            ResultReason = WaterReceipt.IsCommitted
                ? "Physical water is committed pending contribution."
                : "Suppression operation prepared.";
        }

        public void SetResult(
            EnvironmentalFireSuppressionDisposition disposition,
            string reason)
        {
            ResultDisposition = disposition;
            ResultReason = reason;
        }

        public void Cancel(string reason)
        {
            Phase = EnvironmentalFireSuppressionPhase.Cancelled;
            ResultDisposition = EnvironmentalFireSuppressionDisposition.Cancelled;
            ResultReason = reason;
        }
    }
}
