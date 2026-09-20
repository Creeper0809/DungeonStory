using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
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
    IEnvironmentalFirePersistence
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
    private readonly IEnvironmentGameplayOutcomeCommitter environmentOutcomes;
    private readonly IGameCalendar calendar;
    private readonly EnvironmentalFireRuntimeSettings settings;
    private readonly IRandomStream spreadRandom;
    private readonly Dictionary<string, FireState> activeById =
        new(StringComparer.Ordinal);
    private readonly Dictionary<EnvironmentalFireTargetRef, string> fireByTarget =
        new();
    private readonly Dictionary<string, IgnitionRecord> causes =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, SuppressionOperation> suppressions =
        new(StringComparer.Ordinal);
    private readonly List<EnvironmentalFireHistorySnapshot> history = new();

    private long nextFireSequence = 1;
    private long nextOutcomeSequence = 1;
    private float accumulator;
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
        IEnvironmentGameplayOutcomeCommitter environmentOutcomes,
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
        this.environmentOutcomes = environmentOutcomes
            ?? throw new ArgumentNullException(nameof(environmentOutcomes));
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
            "Environmental fire fixtures require the mandatory gameplay outcome committer and game calendar.");
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
        if (request == null
            || !request.IsValid
            || request.Kind == EnvironmentalFireIgnitionKind.Spread
            || request.Target.Kind != EnvironmentalFireTargetKind.Building)
        {
            return new EnvironmentalFireIgnitionResult(
                EnvironmentalFireIgnitionDisposition.InvalidRequest,
                string.Empty,
                "A canonical supported ignition event is required.");
        }

        if (causes.TryGetValue(request.CauseId, out IgnitionRecord known))
        {
            if (!string.Equals(
                    known.Fingerprint,
                    known.LegacyPreOutcome
                        ? request.LegacyFingerprint
                        : request.Fingerprint,
                    StringComparison.Ordinal))
            {
                return new EnvironmentalFireIgnitionResult(
                    EnvironmentalFireIgnitionDisposition.CauseConflict,
                    known.FireId,
                    "The cause ID was already used with different evidence.");
            }

            if (known.OutcomePending)
                ResumePendingIgnitionOutcome(request, known);

            var canonicalResult = new EnvironmentalFireIgnitionResult(
                known.Disposition,
                known.FireId,
                known.Reason);
            if (!known.LegacyPreOutcome)
            {
                EnvironmentalFireIgnitionOutcomeReceipt canonicalReceipt =
                    EnvironmentOutcomeReceiptFactory.CreateFireIgnition(
                        request,
                        canonicalResult,
                        known.TargetDisplayName,
                        new CoreGridCell(known.Position.x, known.Position.y),
                        known.AbsoluteDay,
                        known.OutcomeRevision,
                        known.AppliedIntensity,
                        known.HasLocation);
                if (!environmentOutcomes.IsCanonicalAcknowledgedReplay(
                        canonicalReceipt,
                        out string replayFailure))
                    throw new InvalidOperationException(
                        "Environmental fire ignition replay is not canonical: "
                        + replayFailure);
            }
            return new EnvironmentalFireIgnitionResult(
                EnvironmentalFireIgnitionDisposition.PreviouslyProcessed,
                known.FireId,
                known.Reason);
        }

        if (!targets.TryGetTarget(request.Target, out var target)
            || !target.Exists)
        {
            return CommitIgnitionOutcome(
                request,
                EnvironmentalFireIgnitionDisposition.TargetMissing,
                string.Empty,
                "The authored target no longer exists.",
                default,
                hasLocation: false);
        }

        if (!target.CanBurn || !target.Accepts(request.Kind))
        {
            return CommitIgnitionOutcome(
                request,
                EnvironmentalFireIgnitionDisposition.TargetNotCombustible,
                string.Empty,
                "The authored target is not eligible for this ignition source.",
                target.Position,
                hasLocation: true);
        }

        if (fireByTarget.TryGetValue(request.Target, out string existingFireId))
        {
            return CommitIgnitionOutcome(
                request,
                EnvironmentalFireIgnitionDisposition.AlreadyBurning,
                existingFireId,
                "The target already has an active environmental fire.",
                target.Position,
                hasLocation: true);
        }
        if (nextFireSequence <= 0 || nextFireSequence == long.MaxValue)
        {
            return new EnvironmentalFireIgnitionResult(
                EnvironmentalFireIgnitionDisposition.InvalidRequest,
                string.Empty,
                "Environmental fire identity sequence is exhausted or invalid.");
        }

        long fireSequenceBefore = nextFireSequence;
        string fireId = string.Concat("environmental-fire:", fireSequenceBefore);
        var ignitionResult = new EnvironmentalFireIgnitionResult(
            EnvironmentalFireIgnitionDisposition.Ignited,
            fireId,
            "Ignition accepted.");
        long outcomeRevision = RequireNextOutcomeRevision();
        EnvironmentalFireIgnitionOutcomeReceipt outcomeReceipt =
            EnvironmentOutcomeReceiptFactory.CreateFireIgnition(
                request,
                ignitionResult,
                request.TargetDisplayName,
                new CoreGridCell(target.Position.x, target.Position.y),
                calendar.Day,
                outcomeRevision,
                Math.Min(
                    request.IgnitionIntensity,
                    target.Profile.MaximumIntensity));
        if (!environmentOutcomes.TryPrepare(
                outcomeReceipt,
                out PreparedEnvironmentOutcome preparedOutcome,
                out string prepareFailure))
            throw new InvalidOperationException(
                "Environmental fire ignition outcome prepare failed: "
                + prepareFailure);

        var state = new FireState
        {
            FireId = fireId,
            CauseId = request.CauseId,
            IgnitionKind = request.Kind,
            ProducerId = request.ProducerId,
            EvidenceId = request.EvidenceId,
            Target = request.Target,
            Position = target.Position,
            Intensity = Math.Min(
                request.IgnitionIntensity,
                target.Profile.MaximumIntensity),
            RemainingFuel = target.Profile.FuelCapacity,
            RequiresElectricalIsolation = target.HasElectricalHazard
                || request.Kind == EnvironmentalFireIgnitionKind.ElectricalFault,
            FuelLossRequest = request.FuelLoss
        };
        int versionBefore = version;
        var ownerRecord = new IgnitionRecord(
            request.Fingerprint,
            EnvironmentalFireIgnitionDisposition.Ignited,
            fireId,
            "Ignition accepted.",
            outcomeRevision,
            request.TargetDisplayName,
            target.Position,
            true,
            calendar.Day,
            state.Intensity,
            outcomePending: true);
        activeById.Add(fireId, state);
        fireByTarget.Add(state.Target, fireId);
        causes.Add(request.CauseId, ownerRecord);
        nextFireSequence = checked(fireSequenceBefore + 1L);
        nextOutcomeSequence = checked(outcomeRevision + 1L);
        BumpVersion();

        EnvironmentalFireFuelLossReceipt fuelReceipt = default;
        bool fuelCommitAttempted = false;
        try
        {
            if (request.FuelLoss != null)
            {
                if (fuelLoss == null)
                    throw new InvalidOperationException(
                        "The exact environmental-fire fuel-loss port is unavailable.");
                string fuelOperationId = string.Concat(
                    "environmental-fire-fuel-loss:",
                    request.CauseId);
                fuelCommitAttempted = true;
                if (!fuelLoss.TryCommitPending(
                        request.FuelLoss,
                        fuelOperationId,
                        out fuelReceipt,
                        out string fuelFailure))
                {
                    activeById.Remove(fireId);
                    fireByTarget.Remove(state.Target);
                    causes.Remove(request.CauseId);
                    nextFireSequence = fireSequenceBefore;
                    nextOutcomeSequence = outcomeRevision;
                    version = versionBefore;
                    environmentOutcomes.Cancel(preparedOutcome);
                    return CommitIgnitionOutcome(
                        request,
                        EnvironmentalFireIgnitionDisposition.FuelUnavailable,
                        string.Empty,
                        NormalizeReason(
                            fuelFailure,
                            "The exact physical fire fuel could not be sunk."),
                        target.Position,
                        hasLocation: true);
                }
                if (!MatchesFuelLoss(
                        request.FuelLoss,
                        fuelOperationId,
                        fuelReceipt))
                    throw new InvalidOperationException(
                        "The physical fire-fuel receipt did not match its exact request.");
                state.FuelLossReceipt = fuelReceipt;
            }

            EnvironmentOutcomeCommitResult committed =
                environmentOutcomes.Commit(preparedOutcome, outcomeRevision);
            if (!committed.DurablyCommitted)
                throw new InvalidOperationException(
                    "Environmental fire ignition outcome commit failed: "
                    + committed.DetailCode);
            ownerRecord.MarkOutcomeCommitted();
        }
        catch
        {
            EnvironmentOutcomeCommitResult reconciled =
                environmentOutcomes.Reconcile(outcomeReceipt.Payload.ResultKey);
            if (!reconciled.DurablyCommitted)
            {
                if (fuelCommitAttempted)
                {
                    environmentOutcomes.Cancel(preparedOutcome);
                    throw new InvalidOperationException(
                        "Environmental fire ignition is durably pending physical-fuel and parent-outcome reconciliation.");
                }
                activeById.Remove(fireId);
                fireByTarget.Remove(state.Target);
                causes.Remove(request.CauseId);
                nextFireSequence = fireSequenceBefore;
                nextOutcomeSequence = outcomeRevision;
                version = versionBefore;
                environmentOutcomes.Cancel(preparedOutcome);
                throw;
            }
            ownerRecord.MarkOutcomeCommitted();
        }

        if (fuelReceipt.IsCommitted
            && fuelLoss.TryAcknowledge(
                fuelReceipt.CommitId,
                out _))
        {
            state.FuelLossAcknowledged = true;
            BumpVersion();
        }

        return ignitionResult;
    }

    [GameplayEntryPoint(
        "Approved worker suppression publishes labor and optional exact physical-water evidence here.")]
    public EnvironmentalFireSuppressionResult TryApplySuppression(
        EnvironmentalFireSuppressionCommand command)
    {
        if (command == null || !command.IsValid)
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

            if (!known.LegacyPreOutcome
                && known.Phase == EnvironmentalFireSuppressionPhase.Applied)
            {
                EnvironmentalFireSuppressionResult canonical =
                    CanonicalSuppressionResult(known);
                Vector2Int outcomePosition = FindFirePosition(
                    known.Command.FireId);
                EnvironmentalFireSuppressionOutcomeReceipt receipt =
                    EnvironmentOutcomeReceiptFactory.CreateFireSuppression(
                        known.Command,
                        canonical,
                        known.Command.WorkerDisplayName,
                        new CoreGridCell(outcomePosition.x, outcomePosition.y),
                        known.AbsoluteDay,
                        known.OutcomeRevision);
                if (!environmentOutcomes.IsCanonicalAcknowledgedReplay(
                        receipt,
                        out string replayFailure))
                    throw new InvalidOperationException(
                        "Environmental fire suppression replay is not canonical: "
                        + replayFailure);
            }
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
        ResumePendingOutcomeCommits();
        ResumePendingFuelLosses();
        var results = new List<EnvironmentalFireSuppressionResult>();
        foreach (SuppressionOperation operation in suppressions.Values
                     .OrderBy(value => value.Command.OperationId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (operation.Phase == EnvironmentalFireSuppressionPhase.Cancelled)
                continue;

            if (operation.Phase == EnvironmentalFireSuppressionPhase.Applied)
            {
                if (!operation.LegacyPreOutcome)
                {
                    EnvironmentalFireSuppressionResult canonical =
                        CanonicalSuppressionResult(operation);
                    Vector2Int outcomePosition = FindFirePosition(
                        operation.Command.FireId);
                    EnvironmentalFireSuppressionOutcomeReceipt outcomeReceipt =
                        EnvironmentOutcomeReceiptFactory.CreateFireSuppression(
                            operation.Command,
                            canonical,
                            operation.Command.WorkerDisplayName,
                            new CoreGridCell(
                                outcomePosition.x,
                                outcomePosition.y),
                            operation.AbsoluteDay,
                            operation.OutcomeRevision);
                    if (!environmentOutcomes.IsCanonicalAcknowledgedReplay(
                            outcomeReceipt,
                            out string replayFailure))
                        throw new InvalidOperationException(
                            "Environmental fire suppression resume is not canonical: "
                            + replayFailure);
                }
                if (operation.Command.Mode == EnvironmentalFireSuppressionMode.Water
                    && !operation.WaterAcknowledged
                    && operation.WaterReceipt.IsCommitted)
                {
                    bool acknowledged = water.TryAcknowledge(
                        operation.WaterReceipt.CommitId,
                        out string acknowledgementFailure);
                    if (acknowledged)
                    {
                        operation.MarkAcknowledged();
                        BumpVersion();
                    }
                    else
                    {
                        operation.SetResult(
                            EnvironmentalFireSuppressionDisposition
                                .AppliedAwaitingWaterAcknowledgement,
                            NormalizeReason(
                                acknowledgementFailure,
                                "Water contribution is applied; acknowledgement remains pending."));
                    }
                }

                results.Add(ResultForKnownOperation(operation));
                continue;
            }

            if (!TryValidateSuppression(
                    operation.Command,
                    out FireState fire,
                    out EnvironmentalFireTargetSnapshot target,
                    out EnvironmentalFireSuppressionResult failure))
            {
                results.Add(failure);
                continue;
            }

            EnvironmentalFireWaterReceipt receipt = operation.WaterReceipt;
            if (operation.Command.Mode == EnvironmentalFireSuppressionMode.Water
                && operation.Phase == EnvironmentalFireSuppressionPhase.Prepared)
            {
                if (water.TryGetPending(
                        operation.Command.OperationId,
                        operation.Command.WaterLeaseId,
                        out var pending))
                {
                    RequireMatchingReceipt(operation.Command, pending);
                    receipt = pending;
                }
                else if (!water.TryCommitReservedWaterPending(
                             operation.Command.WaterLeaseId,
                             operation.Command.WaterQuantity,
                             operation.Command.OperationId,
                             operation.Command.WorkerId,
                             operation.Command.FireId,
                             operation.Command.StandPosition,
                             target.Position,
                             out receipt,
                             out string waterFailure))
                {
                    results.Add(SuppressionFailure(
                        EnvironmentalFireSuppressionDisposition.WaterUnavailable,
                        NormalizeReason(
                            waterFailure,
                            "Physical water remains unavailable."),
                        operation.IntensityBefore));
                    continue;
                }

                RequireMatchingReceipt(operation.Command, receipt);
                operation.SetWaterCommitted(receipt);
                BumpVersion();
            }

            float reduction = operation.Command.Mode
                == EnvironmentalFireSuppressionMode.InitialAttack
                    ? operation.Command.ApprovedWork
                        * settings.InitialAttackSuppressionPerWork
                    : receipt.Quantity * target.Profile.WaterSuppressionPerUnit;
            results.Add(ApplySuppressionContribution(
                operation,
                fire,
                reduction,
                receipt.Quantity,
                receipt.CommitId));
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
        string fingerprint = string.Join(
            "|",
            (int)EnvironmentalFireIgnitionKind.Spread,
            source.FireId,
            (int)target.Target.Kind,
            target.Target.TargetId,
            requestedIntensity.ToString(
                "R",
                System.Globalization.CultureInfo.InvariantCulture),
            source.StepIndex);
        if (causes.ContainsKey(causeId) || fireByTarget.ContainsKey(target.Target))
            return false;

        var request = new EnvironmentalFireIgnitionRequest(
            causeId,
            EnvironmentalFireIgnitionKind.Spread,
            source.FireId,
            target.Target,
            requestedIntensity,
            string.Concat("spread-step:", source.StepIndex),
            targetDisplayName: target.DisplayName);
        if (!request.IsValid)
            throw new InvalidOperationException(
                "Environmental fire spread requires an immutable target display snapshot.");
        long fireSequenceBefore = nextFireSequence;
        string fireId = string.Concat("environmental-fire:", fireSequenceBefore);
        long outcomeRevision = RequireNextOutcomeRevision();
        var result = new EnvironmentalFireIgnitionResult(
            EnvironmentalFireIgnitionDisposition.Ignited,
            fireId,
            "Deterministic direct-neighbour spread accepted.");
        EnvironmentalFireIgnitionOutcomeReceipt receipt =
            EnvironmentOutcomeReceiptFactory.CreateFireIgnition(
                request,
                result,
                target.DisplayName,
                new CoreGridCell(target.Position.x, target.Position.y),
                calendar.Day,
                outcomeRevision,
                Math.Min(requestedIntensity, target.Profile.MaximumIntensity));
        if (!environmentOutcomes.TryPrepare(
                receipt,
                out PreparedEnvironmentOutcome prepared,
                out string prepareFailure))
            throw new InvalidOperationException(
                "Environmental fire spread outcome prepare failed: "
                + prepareFailure);
        var state = new FireState
        {
            FireId = fireId,
            CauseId = causeId,
            IgnitionKind = EnvironmentalFireIgnitionKind.Spread,
            ProducerId = source.FireId,
            EvidenceId = string.Concat("spread-step:", source.StepIndex),
            Target = target.Target,
            Position = target.Position,
            Intensity = Math.Min(
                requestedIntensity,
                target.Profile.MaximumIntensity),
            RemainingFuel = target.Profile.FuelCapacity,
            RequiresElectricalIsolation = target.HasElectricalHazard
        };
        int versionBefore = version;
        try
        {
            activeById.Add(fireId, state);
            fireByTarget.Add(state.Target, fireId);
            causes.Add(
                causeId,
                new IgnitionRecord(
                    fingerprint,
                    EnvironmentalFireIgnitionDisposition.Ignited,
                    fireId,
                    "Deterministic direct-neighbour spread accepted.",
                    outcomeRevision,
                    target.DisplayName,
                    target.Position,
                    true,
                    calendar.Day,
                    state.Intensity));
            nextFireSequence = checked(fireSequenceBefore + 1L);
            nextOutcomeSequence = checked(outcomeRevision + 1L);
            BumpVersion();
            EnvironmentOutcomeCommitResult committed =
                environmentOutcomes.Commit(prepared, outcomeRevision);
            if (!committed.DurablyCommitted)
                throw new InvalidOperationException(
                    "Environmental fire spread outcome commit failed: "
                    + committed.DetailCode);
        }
        catch
        {
            EnvironmentOutcomeCommitResult reconciled =
                environmentOutcomes.Reconcile(receipt.Payload.ResultKey);
            if (!reconciled.DurablyCommitted)
            {
                activeById.Remove(fireId);
                fireByTarget.Remove(state.Target);
                causes.Remove(causeId);
                nextFireSequence = fireSequenceBefore;
                nextOutcomeSequence = outcomeRevision;
                version = versionBefore;
                environmentOutcomes.Cancel(prepared);
                throw;
            }
        }
        return true;
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
            result.processedCauses.Add(new EnvironmentalFireCauseSaveRecord
            {
                causeId = entry.Key,
                fingerprint = entry.Value.Fingerprint,
                disposition = (int)entry.Value.Disposition,
                fireId = entry.Value.FireId,
                reason = entry.Value.Reason,
                outcomeRevision = entry.Value.OutcomeRevision,
                targetDisplayName = entry.Value.TargetDisplayName,
                positionX = entry.Value.Position.x,
                positionY = entry.Value.Position.y,
                hasLocation = entry.Value.HasLocation,
                absoluteDay = entry.Value.AbsoluteDay,
                appliedIntensity = entry.Value.AppliedIntensity,
                legacyPreOutcome = entry.Value.LegacyPreOutcome,
                outcomePending = entry.Value.OutcomePending
            });
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

        activeById.Clear();
        fireByTarget.Clear();
        causes.Clear();
        suppressions.Clear();
        history.Clear();

        nextFireSequence = data.nextFireSequence;
        nextOutcomeSequence = data.nextOutcomeSequence;
        accumulator = data.accumulator;
        foreach (EnvironmentalFireSaveRecord record in data.activeFires)
        {
            FireState state = FromSaveRecord(record);
            activeById.Add(state.FireId, state);
            fireByTarget.Add(state.Target, state.FireId);
        }

        foreach (EnvironmentalFireHistorySaveRecord record in data.history)
        {
            history.Add(new EnvironmentalFireHistorySnapshot
            {
                Fire = CloneSnapshot(FromSaveRecord(record.fire)),
                EndReason = (EnvironmentalFireEndReason)record.endReason
            });
        }

        foreach (EnvironmentalFireCauseSaveRecord record in data.processedCauses)
        {
            causes.Add(
                record.causeId,
                new IgnitionRecord(
                    record.fingerprint,
                    (EnvironmentalFireIgnitionDisposition)record.disposition,
                    record.fireId,
                    record.reason,
                    record.outcomeRevision,
                    record.targetDisplayName,
                    new Vector2Int(record.positionX, record.positionY),
                    record.hasLocation,
                    record.absoluteDay,
                    record.appliedIntensity,
                    record.legacyPreOutcome,
                    record.outcomePending));
        }

        foreach (EnvironmentalFireSuppressionSaveRecord record
                 in data.suppressionOperations)
        {
            SuppressionOperation operation =
                SuppressionOperation.FromSaveRecord(record);
            suppressions.Add(operation.Command.OperationId, operation);
        }

        BumpVersion();
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
        float intensityAfter = Math.Max(0f, fire.Intensity - reduction);
        EnvironmentalFireSuppressionDisposition disposition =
            intensityAfter <= Epsilon
                ? EnvironmentalFireSuppressionDisposition.Extinguished
                : EnvironmentalFireSuppressionDisposition.Applied;
        var predicted = new EnvironmentalFireSuppressionResult(
            disposition,
            fire.Intensity,
            intensityAfter,
            commitWater ? command.WaterQuantity : 0,
            string.Empty,
            disposition == EnvironmentalFireSuppressionDisposition.Extinguished
                ? "The fire was extinguished."
                : "Suppression contribution applied.");
        long outcomeRevision = RequireNextOutcomeRevision();
        EnvironmentalFireSuppressionOutcomeReceipt outcomeReceipt =
            EnvironmentOutcomeReceiptFactory.CreateFireSuppression(
                command,
                predicted,
                command.WorkerDisplayName,
                new CoreGridCell(target.Position.x, target.Position.y),
                calendar.Day,
                outcomeRevision);
        if (!environmentOutcomes.TryPrepare(
                outcomeReceipt,
                out PreparedEnvironmentOutcome preparedOutcome,
                out string prepareFailure))
            throw new InvalidOperationException(
                "Environmental fire suppression outcome prepare failed: "
                + prepareFailure);

        FireState before = CloneState(fire);
        int versionBefore = version;
        var operation = SuppressionOperation.Create(
            command,
            fire.Intensity,
            outcomeRevision,
            calendar.Day);
        suppressions.Add(command.OperationId, operation);
        nextOutcomeSequence = checked(outcomeRevision + 1L);
        BumpVersion();

        EnvironmentalFireWaterReceipt waterReceipt = default;
        string waterFailure = string.Empty;
        bool waterCommitted;
        try
        {
            waterCommitted = !commitWater
                || water.TryCommitReservedWaterPending(
                    command.WaterLeaseId,
                    command.WaterQuantity,
                    command.OperationId,
                    command.WorkerId,
                    command.FireId,
                    command.StandPosition,
                    target.Position,
                    out waterReceipt,
                    out waterFailure);
            if (commitWater && waterCommitted)
                RequireMatchingReceipt(command, waterReceipt);
        }
        catch
        {
            if (commitWater)
            {
                operation.MarkOutcomePending();
                environmentOutcomes.Cancel(preparedOutcome);
                throw new InvalidOperationException(
                    "Environmental fire suppression is durably pending physical-water reconciliation.");
            }
            suppressions.Remove(command.OperationId);
            nextOutcomeSequence = outcomeRevision;
            version = versionBefore;
            environmentOutcomes.Cancel(preparedOutcome);
            throw;
        }
        if (!waterCommitted)
        {
            suppressions.Remove(command.OperationId);
            nextOutcomeSequence = outcomeRevision;
            version = versionBefore;
            environmentOutcomes.Cancel(preparedOutcome);
            return SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.WaterUnavailable,
                NormalizeReason(waterFailure, "Physical water was unavailable."),
                fire.Intensity);
        }
        if (commitWater)
        {
            operation.SetWaterCommitted(waterReceipt);
            BumpVersion();
        }
        EnvironmentalFireSuppressionResult applied;
        try
        {
            fire.Position = target.Position;
            applied = ApplySuppressionContribution(
                operation,
                fire,
                reduction,
                waterReceipt.Quantity,
                waterReceipt.CommitId);
            EnvironmentOutcomeCommitResult committed =
                environmentOutcomes.Commit(preparedOutcome, outcomeRevision);
            if (!committed.DurablyCommitted)
                throw new InvalidOperationException(
                    "Environmental fire suppression outcome commit failed: "
                    + committed.DetailCode);
        }
        catch
        {
            EnvironmentOutcomeCommitResult reconciled =
                environmentOutcomes.Reconcile(outcomeReceipt.Payload.ResultKey);
            if (!reconciled.DurablyCommitted)
            {
                if (waterReceipt.IsCommitted)
                {
                    RestoreFireState(before);
                    operation.ResetForOutcomeRetry();
                    operation.MarkOutcomePending();
                    nextOutcomeSequence = checked(outcomeRevision + 1L);
                    version = versionBefore;
                    BumpVersion();
                    environmentOutcomes.Cancel(preparedOutcome);
                    throw new InvalidOperationException(
                        "Environmental fire suppression is durably pending its parent outcome commit.");
                }
                RestoreFireState(before);
                suppressions.Remove(command.OperationId);
                nextOutcomeSequence = outcomeRevision;
                version = versionBefore;
                environmentOutcomes.Cancel(preparedOutcome);
                throw;
            }
            nextOutcomeSequence = checked(outcomeRevision + 1L);
            applied = predicted;
        }

        if (!commitWater)
            return applied;
        if (water.TryAcknowledge(waterReceipt.CommitId, out string failureReason))
        {
            operation.MarkAcknowledged();
            BumpVersion();
            return applied;
        }
        operation.SetResult(
            EnvironmentalFireSuppressionDisposition
                .AppliedAwaitingWaterAcknowledgement,
            NormalizeReason(
                failureReason,
                "Water contribution is applied; acknowledgement remains pending."));
        BumpVersion();
        return ResultForKnownOperation(operation);
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
        var result = new EnvironmentalFireIgnitionResult(
            disposition,
            fireId,
            reason);
        long outcomeRevision = RequireNextOutcomeRevision();
        EnvironmentalFireIgnitionOutcomeReceipt receipt =
            EnvironmentOutcomeReceiptFactory.CreateFireIgnition(
                request,
                result,
                request.TargetDisplayName,
                new CoreGridCell(position.x, position.y),
                calendar.Day,
                outcomeRevision,
                0f,
                hasLocation);
        if (!environmentOutcomes.TryPrepare(
                receipt,
                out PreparedEnvironmentOutcome prepared,
                out string prepareFailure))
            throw new InvalidOperationException(
                "Environmental fire ignition outcome prepare failed: "
                + prepareFailure);
        int versionBefore = version;
        causes.Add(
            request.CauseId,
            new IgnitionRecord(
                request.Fingerprint,
                disposition,
                fireId,
                reason,
                outcomeRevision,
                request.TargetDisplayName,
                position,
                hasLocation,
                calendar.Day,
                0f));
        nextOutcomeSequence = checked(outcomeRevision + 1L);
        BumpVersion();
        try
        {
            EnvironmentOutcomeCommitResult committed =
                environmentOutcomes.Commit(prepared, outcomeRevision);
            if (!committed.DurablyCommitted)
                throw new InvalidOperationException(
                    "Environmental fire ignition outcome commit failed: "
                    + committed.DetailCode);
        }
        catch
        {
            EnvironmentOutcomeCommitResult reconciled =
                environmentOutcomes.Reconcile(receipt.Payload.ResultKey);
            if (!reconciled.DurablyCommitted)
            {
                causes.Remove(request.CauseId);
                nextOutcomeSequence = outcomeRevision;
                version = versionBefore;
                environmentOutcomes.Cancel(prepared);
                throw;
            }
        }
        return result;
    }

    private void ResumePendingOutcomeCommits()
    {
        foreach (KeyValuePair<string, IgnitionRecord> entry in causes
                     .Where(pair => pair.Value.OutcomePending)
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                     .ToArray())
        {
            if (!activeById.TryGetValue(entry.Value.FireId, out FireState fire))
                throw new InvalidOperationException(
                    "A pending ignition outcome lost its active fire owner.");
            EnvironmentalFireFuelLossRequest fuelRequest =
                fire.FuelLossReceipt.IsCommitted
                    ? new EnvironmentalFireFuelLossRequest(
                        fire.FuelLossReceipt.Target,
                        fire.FuelLossReceipt.Quantity)
                    : fire.FuelLossRequest;
            var request = new EnvironmentalFireIgnitionRequest(
                fire.CauseId,
                fire.IgnitionKind,
                fire.ProducerId,
                fire.Target,
                Math.Max(Epsilon, entry.Value.AppliedIntensity),
                fire.EvidenceId,
                fuelRequest,
                entry.Value.TargetDisplayName);
            ResumePendingIgnitionOutcome(request, entry.Value);
        }

        foreach (SuppressionOperation operation in suppressions.Values
                     .Where(value => value.OutcomePending)
                     .OrderBy(value => value.Command.OperationId, StringComparer.Ordinal)
                     .ToArray())
        {
            ResumePendingSuppressionOutcome(operation);
        }
        ResumePendingFuelLosses();
    }

    private void ResumePendingIgnitionOutcome(
        EnvironmentalFireIgnitionRequest request,
        IgnitionRecord pending)
    {
        if (pending.LegacyPreOutcome || !pending.OutcomePending)
            return;
        if (!activeById.TryGetValue(pending.FireId, out FireState owner))
            throw new InvalidOperationException(
                "A pending ignition outcome lost its active fire owner.");
        if (owner.FuelLossRequest != null
            && !owner.FuelLossReceipt.IsCommitted)
        {
            if (fuelLoss == null)
                throw new InvalidOperationException(
                    "The exact environmental-fire fuel-loss port is unavailable.");
            string operationId = string.Concat(
                "environmental-fire-fuel-loss:",
                owner.CauseId);
            if (!fuelLoss.TryCommitPending(
                    owner.FuelLossRequest,
                    operationId,
                    out EnvironmentalFireFuelLossReceipt resumedFuel,
                    out string fuelFailure))
                throw new InvalidOperationException(
                    "Pending environmental fire fuel commit failed: "
                    + NormalizeReason(
                        fuelFailure,
                        "The exact physical fire fuel remains unavailable."));
            if (!MatchesFuelLoss(
                    owner.FuelLossRequest,
                    operationId,
                    resumedFuel))
                throw new InvalidOperationException(
                    "The resumed physical fire-fuel receipt did not match its exact request.");
            owner.FuelLossReceipt = resumedFuel;
            BumpVersion();
        }
        var result = new EnvironmentalFireIgnitionResult(
            pending.Disposition,
            pending.FireId,
            pending.Reason);
        EnvironmentalFireIgnitionOutcomeReceipt receipt =
            EnvironmentOutcomeReceiptFactory.CreateFireIgnition(
                request,
                result,
                pending.TargetDisplayName,
                new CoreGridCell(pending.Position.x, pending.Position.y),
                pending.AbsoluteDay,
                pending.OutcomeRevision,
                pending.AppliedIntensity,
                pending.HasLocation);
        EnvironmentOutcomeCommitResult reconciled =
            environmentOutcomes.Reconcile(receipt.Payload.ResultKey);
        if (!reconciled.DurablyCommitted)
        {
            if (!environmentOutcomes.TryPrepare(
                    receipt,
                    out PreparedEnvironmentOutcome prepared,
                    out string prepareFailure))
                throw new InvalidOperationException(
                    "Pending environmental fire ignition outcome prepare failed: "
                    + prepareFailure);
            EnvironmentOutcomeCommitResult committed =
                environmentOutcomes.Commit(prepared, pending.OutcomeRevision);
            if (!committed.DurablyCommitted)
                throw new InvalidOperationException(
                    "Pending environmental fire ignition outcome commit failed: "
                    + committed.DetailCode);
        }
        pending.MarkOutcomeCommitted();
        BumpVersion();
    }

    private void ResumePendingSuppressionOutcome(SuppressionOperation pending)
    {
        if (pending.LegacyPreOutcome || !pending.OutcomePending)
            return;
        if (pending.Phase == EnvironmentalFireSuppressionPhase.Prepared
            || pending.Phase == EnvironmentalFireSuppressionPhase.WaterCommitted)
        {
            if (!TryValidateSuppression(
                    pending.Command,
                    out FireState fire,
                    out EnvironmentalFireTargetSnapshot target,
                    out EnvironmentalFireSuppressionResult failure))
                throw new InvalidOperationException(
                    "Pending environmental fire suppression owner cannot resume: "
                    + failure.Reason);
            if (pending.Command.Mode == EnvironmentalFireSuppressionMode.Water
                && pending.Phase == EnvironmentalFireSuppressionPhase.Prepared)
            {
                EnvironmentalFireWaterReceipt waterReceipt;
                if (!water.TryGetPending(
                        pending.Command.OperationId,
                        pending.Command.WaterLeaseId,
                        out waterReceipt)
                    && !water.TryCommitReservedWaterPending(
                        pending.Command.WaterLeaseId,
                        pending.Command.WaterQuantity,
                        pending.Command.OperationId,
                        pending.Command.WorkerId,
                        pending.Command.FireId,
                        pending.Command.StandPosition,
                        target.Position,
                        out waterReceipt,
                        out string waterFailure))
                    throw new InvalidOperationException(
                        "Pending environmental fire suppression water commit failed: "
                        + NormalizeReason(
                            waterFailure,
                            "Physical water remains unavailable."));
                RequireMatchingReceipt(pending.Command, waterReceipt);
                pending.SetWaterCommitted(waterReceipt);
                BumpVersion();
            }
            float reduction = pending.Command.Mode
                == EnvironmentalFireSuppressionMode.InitialAttack
                    ? pending.Command.ApprovedWork
                        * settings.InitialAttackSuppressionPerWork
                    : pending.WaterReceipt.Quantity
                        * target.Profile.WaterSuppressionPerUnit;
            float intensityAfter = Math.Max(0f, fire.Intensity - reduction);
            var predicted = new EnvironmentalFireSuppressionResult(
                intensityAfter <= Epsilon
                    ? EnvironmentalFireSuppressionDisposition.Extinguished
                    : EnvironmentalFireSuppressionDisposition.Applied,
                fire.Intensity,
                intensityAfter,
                pending.Command.Mode == EnvironmentalFireSuppressionMode.Water
                    ? pending.WaterReceipt.Quantity
                    : 0,
                pending.Command.Mode == EnvironmentalFireSuppressionMode.Water
                    ? pending.WaterReceipt.CommitId
                    : string.Empty,
                intensityAfter <= Epsilon
                    ? "The fire was extinguished."
                    : "Suppression contribution applied.");
            EnvironmentalFireSuppressionOutcomeReceipt pendingReceipt =
                EnvironmentOutcomeReceiptFactory.CreateFireSuppression(
                    pending.Command,
                    predicted,
                    pending.Command.WorkerDisplayName,
                    new CoreGridCell(target.Position.x, target.Position.y),
                    pending.AbsoluteDay,
                    pending.OutcomeRevision);
            EnvironmentOutcomeCommitResult existing =
                environmentOutcomes.Reconcile(pendingReceipt.Payload.ResultKey);
            if (existing.DurablyCommitted)
            {
                ApplySuppressionContribution(
                    pending,
                    fire,
                    reduction,
                    pending.Command.Mode == EnvironmentalFireSuppressionMode.Water
                        ? pending.WaterReceipt.Quantity
                        : 0,
                    pending.Command.Mode == EnvironmentalFireSuppressionMode.Water
                        ? pending.WaterReceipt.CommitId
                        : string.Empty);
                pending.MarkOutcomeCommitted();
                TryAcknowledgeSuppressionWater(pending);
                BumpVersion();
                return;
            }
            if (!environmentOutcomes.TryPrepare(
                    pendingReceipt,
                    out PreparedEnvironmentOutcome preparedOwner,
                    out string ownerPrepareFailure))
                throw new InvalidOperationException(
                    "Pending environmental fire suppression outcome prepare failed: "
                    + ownerPrepareFailure);

            FireState before = CloneState(fire);
            int versionBefore = version;
            try
            {
                ApplySuppressionContribution(
                    pending,
                    fire,
                    reduction,
                    pending.Command.Mode == EnvironmentalFireSuppressionMode.Water
                        ? pending.WaterReceipt.Quantity
                        : 0,
                    pending.Command.Mode == EnvironmentalFireSuppressionMode.Water
                        ? pending.WaterReceipt.CommitId
                        : string.Empty);
                EnvironmentOutcomeCommitResult committed =
                    environmentOutcomes.Commit(
                        preparedOwner,
                        pending.OutcomeRevision);
                if (!committed.DurablyCommitted)
                    throw new InvalidOperationException(
                        "Pending environmental fire suppression outcome commit failed: "
                        + committed.DetailCode);
            }
            catch
            {
                EnvironmentOutcomeCommitResult reconciledOwner =
                    environmentOutcomes.Reconcile(
                        pendingReceipt.Payload.ResultKey);
                if (!reconciledOwner.DurablyCommitted)
                {
                    RestoreFireState(before);
                    pending.ResetForOutcomeRetry();
                    pending.MarkOutcomePending();
                    version = versionBefore;
                    BumpVersion();
                    environmentOutcomes.Cancel(preparedOwner);
                    throw;
                }
            }
            pending.MarkOutcomeCommitted();
            TryAcknowledgeSuppressionWater(pending);
            BumpVersion();
            return;
        }
        if (pending.Phase != EnvironmentalFireSuppressionPhase.Applied)
            throw new InvalidOperationException(
                "A pending native suppression outcome has no completed owner mutation.");
        EnvironmentalFireSuppressionResult result =
            CanonicalSuppressionResult(pending);
        Vector2Int position = FindFirePosition(pending.Command.FireId);
        EnvironmentalFireSuppressionOutcomeReceipt receipt =
            EnvironmentOutcomeReceiptFactory.CreateFireSuppression(
                pending.Command,
                result,
                pending.Command.WorkerDisplayName,
                new CoreGridCell(position.x, position.y),
                pending.AbsoluteDay,
                pending.OutcomeRevision);
        EnvironmentOutcomeCommitResult reconciled =
            environmentOutcomes.Reconcile(receipt.Payload.ResultKey);
        if (!reconciled.DurablyCommitted)
        {
            if (!environmentOutcomes.TryPrepare(
                    receipt,
                    out PreparedEnvironmentOutcome prepared,
                    out string prepareFailure))
                throw new InvalidOperationException(
                    "Pending environmental fire suppression outcome prepare failed: "
                    + prepareFailure);
            EnvironmentOutcomeCommitResult committed =
                environmentOutcomes.Commit(prepared, pending.OutcomeRevision);
            if (!committed.DurablyCommitted)
                throw new InvalidOperationException(
                    "Pending environmental fire suppression outcome commit failed: "
                    + committed.DetailCode);
        }
        pending.MarkOutcomeCommitted();
        TryAcknowledgeSuppressionWater(pending);
        BumpVersion();
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
            operation.ResultReason);
    }

    private static EnvironmentalFireSuppressionResult
        CanonicalSuppressionResult(SuppressionOperation operation)
    {
        EnvironmentalFireSuppressionDisposition disposition =
            operation.IntensityAfter <= Epsilon
                ? EnvironmentalFireSuppressionDisposition.Extinguished
                : EnvironmentalFireSuppressionDisposition.Applied;
        return new EnvironmentalFireSuppressionResult(
            disposition,
            operation.IntensityBefore,
            operation.IntensityAfter,
            operation.WaterReceipt.Quantity,
            operation.WaterReceipt.CommitId,
            operation.ResultReason);
    }

    private Vector2Int FindFirePosition(string fireId)
    {
        if (activeById.TryGetValue(fireId, out FireState active))
            return active.Position;
        EnvironmentalFireHistorySnapshot ended = history.LastOrDefault(value =>
            value?.Fire != null
            && string.Equals(value.Fire.FireId, fireId, StringComparison.Ordinal));
        if (ended?.Fire != null)
            return ended.Fire.Position;
        throw new InvalidOperationException(
            "A suppression outcome lost its owning fire state.");
    }

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

    private string NextFireId()
    {
        if (nextFireSequence <= 0 || nextFireSequence == long.MaxValue)
            throw new InvalidOperationException(
                "Environmental fire identity sequence is exhausted or invalid.");
        return string.Concat("environmental-fire:", nextFireSequence++);
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
        public IgnitionRecord(
            string fingerprint,
            EnvironmentalFireIgnitionDisposition disposition,
            string fireId,
            string reason,
            long outcomeRevision,
            string targetDisplayName,
            Vector2Int position,
            bool hasLocation,
            int absoluteDay,
            float appliedIntensity,
            bool legacyPreOutcome = false,
            bool outcomePending = false)
        {
            Fingerprint = fingerprint;
            Disposition = disposition;
            FireId = fireId;
            Reason = reason;
            OutcomeRevision = outcomeRevision;
            TargetDisplayName = targetDisplayName;
            Position = position;
            HasLocation = hasLocation;
            AbsoluteDay = absoluteDay;
            AppliedIntensity = appliedIntensity;
            LegacyPreOutcome = legacyPreOutcome;
            OutcomePending = outcomePending;
        }

        public string Fingerprint { get; }
        public EnvironmentalFireIgnitionDisposition Disposition { get; }
        public string FireId { get; }
        public string Reason { get; }
        public long OutcomeRevision { get; }
        public string TargetDisplayName { get; }
        public Vector2Int Position { get; }
        public bool HasLocation { get; }
        public int AbsoluteDay { get; }
        public float AppliedIntensity { get; }
        public bool LegacyPreOutcome { get; }
        public bool OutcomePending { get; private set; }

        public void MarkOutcomePending() => OutcomePending = true;
        public void MarkOutcomeCommitted() => OutcomePending = false;
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
            bool outcomePending)
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
        }

        public EnvironmentalFireSuppressionCommand Command { get; }
        public string Fingerprint { get; }
        public EnvironmentalFireSuppressionPhase Phase { get; private set; }
        public EnvironmentalFireSuppressionDisposition ResultDisposition
            { get; private set; }
        public float IntensityBefore { get; }
        public float IntensityAfter { get; private set; }
        public EnvironmentalFireWaterReceipt WaterReceipt { get; private set; }
        public bool WaterAcknowledged { get; private set; }
        public string ResultReason { get; private set; }
        public long OutcomeRevision { get; }
        public int AbsoluteDay { get; }
        public bool LegacyPreOutcome { get; }
        public bool OutcomePending { get; private set; }

        public static SuppressionOperation Create(
            EnvironmentalFireSuppressionCommand command,
            float intensityBefore,
            long outcomeRevision,
            int absoluteDay) =>
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
                record.workerDisplayName);
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
                record.outcomePending);
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
            float intensityAfter,
            EnvironmentalFireSuppressionDisposition disposition,
            string reason)
        {
            Phase = EnvironmentalFireSuppressionPhase.Applied;
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
