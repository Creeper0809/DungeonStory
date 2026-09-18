using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using UnityEngine;

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
    private float accumulator;
    private int version = 1;

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
    {
        this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        this.damage = damage ?? throw new ArgumentNullException(nameof(damage));
        this.access = access ?? throw new ArgumentNullException(nameof(access));
        this.electricalSafety = electricalSafety
            ?? throw new ArgumentNullException(nameof(electricalSafety));
        this.water = water ?? throw new ArgumentNullException(nameof(water));
        this.exposures = exposures;
        this.fuelLoss = fuelLoss;
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        spreadRandom = (randomStreams
            ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get(SpreadRandomStreamId);
    }

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
                    request.Fingerprint,
                    StringComparison.Ordinal))
            {
                return new EnvironmentalFireIgnitionResult(
                    EnvironmentalFireIgnitionDisposition.CauseConflict,
                    known.FireId,
                    "The cause ID was already used with different evidence.");
            }

            return new EnvironmentalFireIgnitionResult(
                EnvironmentalFireIgnitionDisposition.PreviouslyProcessed,
                known.FireId,
                known.Reason);
        }

        if (!targets.TryGetTarget(request.Target, out var target)
            || !target.Exists)
        {
            return RecordIgnitionOutcome(
                request.CauseId,
                request.Fingerprint,
                EnvironmentalFireIgnitionDisposition.TargetMissing,
                string.Empty,
                "The authored target no longer exists.");
        }

        if (!target.CanBurn || !target.Accepts(request.Kind))
        {
            return RecordIgnitionOutcome(
                request.CauseId,
                request.Fingerprint,
                EnvironmentalFireIgnitionDisposition.TargetNotCombustible,
                string.Empty,
                "The authored target is not eligible for this ignition source.");
        }

        if (fireByTarget.TryGetValue(request.Target, out string existingFireId))
        {
            return RecordIgnitionOutcome(
                request.CauseId,
                request.Fingerprint,
                EnvironmentalFireIgnitionDisposition.AlreadyBurning,
                existingFireId,
                "The target already has an active environmental fire.");
        }
        if (nextFireSequence <= 0 || nextFireSequence == long.MaxValue)
        {
            return new EnvironmentalFireIgnitionResult(
                EnvironmentalFireIgnitionDisposition.InvalidRequest,
                string.Empty,
                "Environmental fire identity sequence is exhausted or invalid.");
        }

        EnvironmentalFireFuelLossReceipt fuelReceipt = default;
        if (request.FuelLoss != null)
        {
            if (fuelLoss == null)
            {
                return new EnvironmentalFireIgnitionResult(
                    EnvironmentalFireIgnitionDisposition.InvalidRequest,
                    string.Empty,
                    "The exact environmental-fire fuel-loss port is unavailable.");
            }

            string fuelOperationId = string.Concat(
                "environmental-fire-fuel-loss:",
                request.CauseId);
            if (!fuelLoss.TryCommitPending(
                    request.FuelLoss,
                    fuelOperationId,
                    out fuelReceipt,
                    out string fuelFailure)
                || !MatchesFuelLoss(
                    request.FuelLoss,
                    fuelOperationId,
                    fuelReceipt))
            {
                return new EnvironmentalFireIgnitionResult(
                    EnvironmentalFireIgnitionDisposition.FuelUnavailable,
                    string.Empty,
                    NormalizeReason(
                        fuelFailure,
                        "The exact physical fire fuel could not be sunk."));
            }
        }

        string fireId = NextFireId();
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
            FuelLossReceipt = fuelReceipt
        };
        activeById.Add(fireId, state);
        fireByTarget.Add(state.Target, fireId);
        causes.Add(
            request.CauseId,
            new IgnitionRecord(
                request.Fingerprint,
                EnvironmentalFireIgnitionDisposition.Ignited,
                fireId,
                "Ignition accepted."));
        BumpVersion();

        if (fuelReceipt.IsCommitted
            && fuelLoss.TryAcknowledge(
                fuelReceipt.CommitId,
                out _))
        {
            state.FuelLossAcknowledged = true;
            BumpVersion();
        }

        return new EnvironmentalFireIgnitionResult(
            EnvironmentalFireIgnitionDisposition.Ignited,
            fireId,
            "Ignition accepted.");
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
                    command.Fingerprint,
                    StringComparison.Ordinal))
            {
                return SuppressionFailure(
                    EnvironmentalFireSuppressionDisposition.OperationConflict,
                    "The operation ID was already used by another command.");
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

            var operation = SuppressionOperation.Create(command, fire.Intensity);
            suppressions.Add(command.OperationId, operation);
            return ApplySuppressionContribution(
                operation,
                fire,
                command.ApprovedWork
                * settings.InitialAttackSuppressionPerWork,
                0,
                string.Empty);
        }

        var waterOperation = SuppressionOperation.Create(command, fire.Intensity);
        suppressions.Add(command.OperationId, waterOperation);
        if (!water.TryCommitReservedWaterPending(
                command.WaterLeaseId,
                command.WaterQuantity,
                command.OperationId,
                command.WorkerId,
                command.FireId,
                command.StandPosition,
                target.Position,
                out EnvironmentalFireWaterReceipt receipt,
                out string waterFailure))
        {
            suppressions.Remove(command.OperationId);
            return SuppressionFailure(
                EnvironmentalFireSuppressionDisposition.WaterUnavailable,
                NormalizeReason(waterFailure, "Physical water was unavailable."),
                fire.Intensity);
        }

        RequireMatchingReceipt(command, receipt);

        waterOperation.SetWaterCommitted(receipt);
        return ApplySuppressionContribution(
            waterOperation,
            fire,
            receipt.Quantity * target.Profile.WaterSuppressionPerUnit,
            receipt.Quantity,
            receipt.CommitId);
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

        string fireId = NextFireId();
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
        activeById.Add(fireId, state);
        fireByTarget.Add(state.Target, fireId);
        causes.Add(
            causeId,
            new IgnitionRecord(
                fingerprint,
                EnvironmentalFireIgnitionDisposition.Ignited,
                fireId,
                "Deterministic direct-neighbour spread accepted."));
        BumpVersion();
        return true;
    }

    public DungeonEnvironmentalFireSaveData Capture()
    {
        var result = new DungeonEnvironmentalFireSaveData
        {
            nextFireSequence = nextFireSequence,
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
                reason = entry.Value.Reason
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
                    record.reason));
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

        fire.Position = target.Position;
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

        if (water.TryAcknowledge(waterCommitId, out string failureReason))
        {
            operation.MarkAcknowledged();
            BumpVersion();
            return new EnvironmentalFireSuppressionResult(
                disposition,
                before,
                operation.IntensityAfter,
                waterConsumed,
                waterCommitId,
                operation.ResultReason);
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

    private EnvironmentalFireIgnitionResult RecordIgnitionOutcome(
        string causeId,
        string fingerprint,
        EnvironmentalFireIgnitionDisposition disposition,
        string fireId,
        string reason)
    {
        causes.Add(
            causeId,
            new IgnitionRecord(fingerprint, disposition, fireId, reason));
        BumpVersion();
        return new EnvironmentalFireIgnitionResult(
            disposition,
            fireId,
            reason);
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
            FuelLossTarget = fire.FuelLossReceipt.Target,
            FuelLossOperationId = fire.FuelLossReceipt.OperationId,
            FuelLossQuantity = fire.FuelLossReceipt.Quantity,
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
            fuelLossTargetKind = (int)fire.FuelLossReceipt.Target.Kind,
            fuelLossTargetId = fire.FuelLossReceipt.Target.TargetId,
            fuelLossOperationId = fire.FuelLossReceipt.OperationId,
            fuelLossQuantity = fire.FuelLossReceipt.Quantity,
            fuelLossMassGrams = fire.FuelLossReceipt.InputMassGrams,
            fuelLossCommitId = fire.FuelLossReceipt.CommitId,
            fuelLossAcknowledged = fire.FuelLossAcknowledged
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
                ? new EnvironmentalFireFuelLossReceipt(
                    new EnvironmentalFireTargetRef(
                        (EnvironmentalFireTargetKind)record.fuelLossTargetKind,
                        record.fuelLossTargetId),
                    record.fuelLossOperationId,
                    record.fuelLossQuantity,
                    record.fuelLossMassGrams,
                    record.fuelLossCommitId)
                : default,
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
            resultReason = operation.ResultReason
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
        public bool FuelLossAcknowledged;
    }

    private sealed class IgnitionRecord
    {
        public IgnitionRecord(
            string fingerprint,
            EnvironmentalFireIgnitionDisposition disposition,
            string fireId,
            string reason)
        {
            Fingerprint = fingerprint;
            Disposition = disposition;
            FireId = fireId;
            Reason = reason;
        }

        public string Fingerprint { get; }
        public EnvironmentalFireIgnitionDisposition Disposition { get; }
        public string FireId { get; }
        public string Reason { get; }
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
            string resultReason)
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

        public static SuppressionOperation Create(
            EnvironmentalFireSuppressionCommand command,
            float intensityBefore) =>
            new(
                command,
                command.Fingerprint,
                EnvironmentalFireSuppressionPhase.Prepared,
                EnvironmentalFireSuppressionDisposition.OperationInProgress,
                intensityBefore,
                intensityBefore,
                default,
                false,
                "Suppression operation prepared.");

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
                record.waterQuantity);
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
                record.resultReason);
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
