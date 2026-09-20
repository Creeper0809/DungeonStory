using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace DungeonStory.Environment
{
    public enum EnvironmentalFireIgnitionKind
    {
        ElectricalFault = 0,
        ActiveHeatSource = 1,
        ProcessAccident = 2,
        AuthoredFlameImpact = 3,
        FeedSelfHeating = 4,
        Spread = 5
    }

    [Flags]
    public enum EnvironmentalFireIgnitionSources
    {
        None = 0,
        ElectricalFault = 1 << 0,
        ActiveHeatSource = 1 << 1,
        ProcessAccident = 1 << 2,
        AuthoredFlameImpact = 1 << 3,
        FeedSelfHeating = 1 << 4,
        Spread = 1 << 5,
        All = ElectricalFault
            | ActiveHeatSource
            | ProcessAccident
            | AuthoredFlameImpact
            | FeedSelfHeating
            | Spread
    }

    public enum EnvironmentalFireTargetKind
    {
        Building = 0,
        Character = 1,
        ItemStack = 2
    }

    public enum EnvironmentalFireIgnitionDisposition
    {
        Ignited = 0,
        PreviouslyProcessed = 1,
        AlreadyBurning = 2,
        TargetMissing = 3,
        TargetNotCombustible = 4,
        CauseConflict = 5,
        InvalidRequest = 6,
        FuelUnavailable = 7
    }

    public enum EnvironmentalFireSuppressionMode
    {
        InitialAttack = 0,
        Water = 1
    }

    public enum EnvironmentalFireSuppressionDisposition
    {
        Applied = 0,
        Extinguished = 1,
        AppliedAwaitingWaterAcknowledgement = 2,
        PreviouslyApplied = 3,
        FireMissing = 4,
        AccessBlocked = 5,
        ElectricalIsolationRequired = 6,
        RequiresWater = 7,
        WaterUnavailable = 8,
        OperationConflict = 9,
        OperationInProgress = 10,
        InvalidRequest = 11,
        Cancelled = 12,
        CancellationRejectedAfterCommit = 13,
        OperationMissing = 14
    }

    public enum EnvironmentalFireSuppressionPhase
    {
        Prepared = 0,
        WaterCommitted = 1,
        Applied = 2,
        Cancelled = 3
    }

    public enum EnvironmentalFireEndReason
    {
        Suppressed = 0,
        FuelExhausted = 1,
        TargetLost = 2,
        TargetNoLongerCombustible = 3
    }

    public readonly struct EnvironmentalFireTargetRef :
        IEquatable<EnvironmentalFireTargetRef>
    {
        public EnvironmentalFireTargetRef(
            EnvironmentalFireTargetKind kind,
            string targetId)
        {
            Kind = kind;
            TargetId = targetId?.Trim() ?? string.Empty;
        }

        public EnvironmentalFireTargetKind Kind { get; }
        public string TargetId { get; }
        public bool IsValid => Enum.IsDefined(typeof(EnvironmentalFireTargetKind), Kind)
            && !string.IsNullOrWhiteSpace(TargetId);

        public bool Equals(EnvironmentalFireTargetRef other) =>
            Kind == other.Kind
            && string.Equals(TargetId, other.TargetId, StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is EnvironmentalFireTargetRef other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine((int)Kind, TargetId ?? string.Empty);

        public override string ToString() => $"{Kind}:{TargetId}";
    }

    public sealed class EnvironmentalFireProfile
    {
        public EnvironmentalFireProfile(
            float fuelCapacity,
            float maximumIntensity,
            float growthPerTick,
            float fuelConsumedPerTick,
            float damagePerTick,
            float minimumSpreadIntensity,
            float spreadChancePerTick,
            float spreadIgnitionMultiplier,
            float waterSuppressionPerUnit)
        {
            RequireFinitePositive(fuelCapacity, nameof(fuelCapacity));
            RequireRange(maximumIntensity, 0.01f, 1f, nameof(maximumIntensity));
            RequireRange(growthPerTick, 0f, 1f, nameof(growthPerTick));
            RequireFinitePositive(fuelConsumedPerTick, nameof(fuelConsumedPerTick));
            RequireFinitePositive(damagePerTick, nameof(damagePerTick));
            RequireRange(
                minimumSpreadIntensity,
                0f,
                maximumIntensity,
                nameof(minimumSpreadIntensity));
            RequireRange(spreadChancePerTick, 0f, 1f, nameof(spreadChancePerTick));
            RequireRange(spreadIgnitionMultiplier, 0f, 1f, nameof(spreadIgnitionMultiplier));
            RequireFinitePositive(
                waterSuppressionPerUnit,
                nameof(waterSuppressionPerUnit));

            FuelCapacity = fuelCapacity;
            MaximumIntensity = maximumIntensity;
            GrowthPerTick = growthPerTick;
            FuelConsumedPerTick = fuelConsumedPerTick;
            DamagePerTick = damagePerTick;
            MinimumSpreadIntensity = minimumSpreadIntensity;
            SpreadChancePerTick = spreadChancePerTick;
            SpreadIgnitionMultiplier = spreadIgnitionMultiplier;
            WaterSuppressionPerUnit = waterSuppressionPerUnit;
        }

        public float FuelCapacity { get; }
        public float MaximumIntensity { get; }
        public float GrowthPerTick { get; }
        public float FuelConsumedPerTick { get; }
        public float DamagePerTick { get; }
        public float MinimumSpreadIntensity { get; }
        public float SpreadChancePerTick { get; }
        public float SpreadIgnitionMultiplier { get; }
        public float WaterSuppressionPerUnit { get; }

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

    public readonly struct EnvironmentalFireTargetSnapshot
    {
        public EnvironmentalFireTargetSnapshot(
            EnvironmentalFireTargetRef target,
            Vector2Int position,
            bool exists,
            bool combustible,
            bool hasElectricalHazard,
            EnvironmentalFireProfile profile,
            EnvironmentalFireIgnitionSources acceptedSources =
                EnvironmentalFireIgnitionSources.All,
            string displayName = "")
        {
            Target = target;
            Position = position;
            Exists = exists;
            Combustible = combustible;
            HasElectricalHazard = hasElectricalHazard;
            Profile = profile;
            AcceptedSources = acceptedSources;
            DisplayName = displayName?.Trim() ?? string.Empty;
        }

        public EnvironmentalFireTargetRef Target { get; }
        public Vector2Int Position { get; }
        public bool Exists { get; }
        public bool Combustible { get; }
        public bool HasElectricalHazard { get; }
        public EnvironmentalFireProfile Profile { get; }
        public EnvironmentalFireIgnitionSources AcceptedSources { get; }
        public string DisplayName { get; }
        public bool CanBurn => Target.IsValid && Exists && Combustible && Profile != null;

        public bool Accepts(EnvironmentalFireIgnitionKind kind)
        {
            if (!Enum.IsDefined(typeof(EnvironmentalFireIgnitionKind), kind))
                return false;
            var source = (EnvironmentalFireIgnitionSources)(1 << (int)kind);
            return (AcceptedSources & source) != 0;
        }
    }

    public readonly struct EnvironmentalFireElectricalSafetySnapshot
    {
        public EnvironmentalFireElectricalSafetySnapshot(
            EnvironmentalFireTargetRef target,
            bool hasElectricalContact,
            bool connectionEnabled,
            bool breakerTripped,
            bool hasLiveGeneration,
            bool hasStoredPower)
        {
            Target = target;
            HasElectricalContact = hasElectricalContact;
            ConnectionEnabled = connectionEnabled;
            BreakerTripped = breakerTripped;
            HasLiveGeneration = hasLiveGeneration;
            HasStoredPower = hasStoredPower;
        }

        public EnvironmentalFireTargetRef Target { get; }
        public bool HasElectricalContact { get; }
        public bool ConnectionEnabled { get; }
        public bool BreakerTripped { get; }
        public bool HasLiveGeneration { get; }
        public bool HasStoredPower { get; }
        public bool IsEnergized => HasElectricalContact
            && (HasLiveGeneration || HasStoredPower);
        public bool IsolationConfirmed => !HasElectricalContact
            && (!ConnectionEnabled || BreakerTripped)
            && !HasLiveGeneration
            && !HasStoredPower;
    }

    public sealed class EnvironmentalFireIgnitionRequest
    {
        public EnvironmentalFireIgnitionRequest(
            string causeId,
            EnvironmentalFireIgnitionKind kind,
            string producerId,
            EnvironmentalFireTargetRef target,
            float ignitionIntensity,
            string evidenceId,
            EnvironmentalFireFuelLossRequest fuelLoss = null,
            string targetDisplayName = "")
        {
            CauseId = causeId?.Trim() ?? string.Empty;
            Kind = kind;
            ProducerId = producerId?.Trim() ?? string.Empty;
            Target = target;
            IgnitionIntensity = ignitionIntensity;
            EvidenceId = evidenceId?.Trim() ?? string.Empty;
            FuelLoss = fuelLoss;
            TargetDisplayName = targetDisplayName?.Trim() ?? string.Empty;
        }

        public string CauseId { get; }
        public EnvironmentalFireIgnitionKind Kind { get; }
        public string ProducerId { get; }
        public EnvironmentalFireTargetRef Target { get; }
        public float IgnitionIntensity { get; }
        public string EvidenceId { get; }
        public EnvironmentalFireFuelLossRequest FuelLoss { get; }
        public string TargetDisplayName { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(CauseId)
            && Kind is >= EnvironmentalFireIgnitionKind.ElectricalFault
                and <= EnvironmentalFireIgnitionKind.Spread
            && !string.IsNullOrWhiteSpace(ProducerId)
            && Target.IsValid
            && IsFinite(IgnitionIntensity)
            && IgnitionIntensity > 0f
            && IgnitionIntensity <= 1f
            && !string.IsNullOrWhiteSpace(EvidenceId)
            && !string.IsNullOrWhiteSpace(TargetDisplayName)
            && (Kind == EnvironmentalFireIgnitionKind.FeedSelfHeating
                ? FuelLoss?.IsValid == true
                : FuelLoss == null);

        public string Fingerprint => string.Join(
            "|",
            (int)Kind,
            ProducerId,
            (int)Target.Kind,
            Target.TargetId,
            IgnitionIntensity.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            EvidenceId,
            TargetDisplayName,
            FuelLoss?.Fingerprint ?? "no-fuel-loss");

        public string LegacyFingerprint => string.Join(
            "|",
            CauseId,
            (int)Kind,
            ProducerId,
            (int)Target.Kind,
            Target.TargetId,
            IgnitionIntensity.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            EvidenceId,
            FuelLoss?.Fingerprint ?? "no-fuel-loss");

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public sealed class EnvironmentalFireFuelLossRequest
    {
        public EnvironmentalFireFuelLossRequest(
            EnvironmentalFireTargetRef target,
            int quantity)
        {
            Target = target;
            Quantity = quantity;
        }

        public EnvironmentalFireTargetRef Target { get; }
        public int Quantity { get; }
        public bool IsValid => Target.IsValid
            && Target.Kind == EnvironmentalFireTargetKind.ItemStack
            && Quantity > 0;
        public string Fingerprint => string.Join(
            "|",
            (int)Target.Kind,
            Target.TargetId,
            Quantity);
    }

    public readonly struct EnvironmentalFireFuelLossReceipt
    {
        public EnvironmentalFireFuelLossReceipt(
            EnvironmentalFireTargetRef target,
            string operationId,
            int quantity,
            long inputMassGrams,
            string commitId)
        {
            Target = target;
            OperationId = operationId?.Trim() ?? string.Empty;
            Quantity = quantity;
            InputMassGrams = inputMassGrams;
            CommitId = commitId?.Trim() ?? string.Empty;
        }

        public EnvironmentalFireTargetRef Target { get; }
        public string OperationId { get; }
        public int Quantity { get; }
        public long InputMassGrams { get; }
        public string CommitId { get; }
        public bool IsCommitted => Target.IsValid
            && Target.Kind == EnvironmentalFireTargetKind.ItemStack
            && !string.IsNullOrWhiteSpace(OperationId)
            && Quantity > 0
            && InputMassGrams > 0L
            && !string.IsNullOrWhiteSpace(CommitId);
    }

    public readonly struct EnvironmentalFireIgnitionResult
    {
        public EnvironmentalFireIgnitionResult(
            EnvironmentalFireIgnitionDisposition disposition,
            string fireId,
            string reason)
        {
            Disposition = disposition;
            FireId = fireId?.Trim() ?? string.Empty;
            Reason = reason?.Trim() ?? string.Empty;
        }

        public EnvironmentalFireIgnitionDisposition Disposition { get; }
        public string FireId { get; }
        public string Reason { get; }
        public bool Created => Disposition == EnvironmentalFireIgnitionDisposition.Ignited;
    }

    public sealed class EnvironmentalFireSuppressionCommand
    {
        public EnvironmentalFireSuppressionCommand(
            string operationId,
            string fireId,
            string workerId,
            Vector2Int standPosition,
            EnvironmentalFireSuppressionMode mode,
            float approvedWork,
            string waterLeaseId = "",
            int waterQuantity = 0,
            string workerDisplayName = "")
        {
            OperationId = operationId?.Trim() ?? string.Empty;
            FireId = fireId?.Trim() ?? string.Empty;
            WorkerId = workerId?.Trim() ?? string.Empty;
            StandPosition = standPosition;
            Mode = mode;
            ApprovedWork = approvedWork;
            WaterLeaseId = waterLeaseId?.Trim() ?? string.Empty;
            WaterQuantity = waterQuantity;
            WorkerDisplayName = workerDisplayName?.Trim() ?? string.Empty;
        }

        public string OperationId { get; }
        public string FireId { get; }
        public string WorkerId { get; }
        public Vector2Int StandPosition { get; }
        public EnvironmentalFireSuppressionMode Mode { get; }
        public float ApprovedWork { get; }
        public string WaterLeaseId { get; }
        public int WaterQuantity { get; }
        public string WorkerDisplayName { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(OperationId)
            && !string.IsNullOrWhiteSpace(FireId)
            && !string.IsNullOrWhiteSpace(WorkerId)
            && Enum.IsDefined(typeof(EnvironmentalFireSuppressionMode), Mode)
            && IsFinite(ApprovedWork)
            && ApprovedWork > 0f
            && !string.IsNullOrWhiteSpace(WorkerDisplayName)
            && (Mode == EnvironmentalFireSuppressionMode.InitialAttack
                ? WaterQuantity == 0 && WaterLeaseId.Length == 0
                : WaterQuantity > 0 && WaterLeaseId.Length > 0);

        public string Fingerprint => string.Join(
            "|",
            FireId,
            WorkerId,
            StandPosition.x,
            StandPosition.y,
            (int)Mode,
            ApprovedWork.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            WorkerDisplayName,
            WaterLeaseId,
            WaterQuantity);

        public string LegacyFingerprint => string.Join(
            "|",
            OperationId,
            FireId,
            WorkerId,
            StandPosition.x,
            StandPosition.y,
            (int)Mode,
            ApprovedWork.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            WaterLeaseId,
            WaterQuantity);

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct EnvironmentalFireSuppressionResult
    {
        public EnvironmentalFireSuppressionResult(
            EnvironmentalFireSuppressionDisposition disposition,
            float intensityBefore,
            float intensityAfter,
            int waterConsumed,
            string waterCommitId,
            string reason)
        {
            Disposition = disposition;
            IntensityBefore = intensityBefore;
            IntensityAfter = intensityAfter;
            WaterConsumed = Math.Max(0, waterConsumed);
            WaterCommitId = waterCommitId?.Trim() ?? string.Empty;
            Reason = reason?.Trim() ?? string.Empty;
        }

        public EnvironmentalFireSuppressionDisposition Disposition { get; }
        public float IntensityBefore { get; }
        public float IntensityAfter { get; }
        public int WaterConsumed { get; }
        public string WaterCommitId { get; }
        public string Reason { get; }
        public bool Applied => Disposition is
            EnvironmentalFireSuppressionDisposition.Applied
            or EnvironmentalFireSuppressionDisposition.Extinguished
            or EnvironmentalFireSuppressionDisposition.AppliedAwaitingWaterAcknowledgement
            or EnvironmentalFireSuppressionDisposition.PreviouslyApplied;
    }

    public readonly struct EnvironmentalFireWaterReceipt
    {
        public EnvironmentalFireWaterReceipt(
            string operationId,
            string leaseId,
            int quantity,
            string commitId)
        {
            OperationId = operationId?.Trim() ?? string.Empty;
            LeaseId = leaseId?.Trim() ?? string.Empty;
            Quantity = Math.Max(0, quantity);
            CommitId = commitId?.Trim() ?? string.Empty;
        }

        public string OperationId { get; }
        public string LeaseId { get; }
        public int Quantity { get; }
        public string CommitId { get; }
        public bool IsCommitted => OperationId.Length > 0
            && LeaseId.Length > 0
            && Quantity > 0
            && CommitId.Length > 0;
    }

    public readonly struct EnvironmentalFireDamageCommand
    {
        public EnvironmentalFireDamageCommand(
            string operationId,
            string fireId,
            EnvironmentalFireTargetRef target,
            Vector2Int position,
            float intensity,
            float requestedDamage)
        {
            OperationId = operationId?.Trim() ?? string.Empty;
            FireId = fireId?.Trim() ?? string.Empty;
            Target = target;
            Position = position;
            Intensity = intensity;
            RequestedDamage = requestedDamage;
        }

        public string OperationId { get; }
        public string FireId { get; }
        public EnvironmentalFireTargetRef Target { get; }
        public Vector2Int Position { get; }
        public float Intensity { get; }
        public float RequestedDamage { get; }
    }

    public readonly struct EnvironmentalFireDamageResult
    {
        public EnvironmentalFireDamageResult(
            bool committed,
            float appliedDamage,
            bool targetRemainsCombustible,
            string failureReason = "")
        {
            Committed = committed;
            AppliedDamage = Math.Max(0f, appliedDamage);
            TargetRemainsCombustible = targetRemainsCombustible;
            FailureReason = failureReason?.Trim() ?? string.Empty;
        }

        public bool Committed { get; }
        public float AppliedDamage { get; }
        public bool TargetRemainsCombustible { get; }
        public string FailureReason { get; }
    }

    public sealed class EnvironmentalFireSnapshot
    {
        public string FireId { get; set; } = string.Empty;
        public string CauseId { get; set; } = string.Empty;
        public EnvironmentalFireIgnitionKind IgnitionKind { get; set; }
        public string ProducerId { get; set; } = string.Empty;
        public string EvidenceId { get; set; } = string.Empty;
        public EnvironmentalFireTargetRef Target { get; set; }
        public Vector2Int Position { get; set; }
        public float Intensity { get; set; }
        public float RemainingFuel { get; set; }
        public long StepIndex { get; set; }
        public float TotalDamage { get; set; }
        public float TotalSuppressionWork { get; set; }
        public int TotalWaterConsumed { get; set; }
        public bool RequiresElectricalIsolation { get; set; }
        public EnvironmentalFireTargetRef FuelLossTarget { get; set; }
        public string FuelLossOperationId { get; set; } = string.Empty;
        public int FuelLossQuantity { get; set; }
        public long FuelLossMassGrams { get; set; }
        public string FuelLossCommitId { get; set; } = string.Empty;
        public bool FuelLossAcknowledged { get; set; }
    }

    public sealed class EnvironmentalFireHistorySnapshot
    {
        public EnvironmentalFireSnapshot Fire { get; set; }
        public EnvironmentalFireEndReason EndReason { get; set; }
    }

    public sealed class EnvironmentalFireTickReport
    {
        public EnvironmentalFireTickReport(
            int steps,
            int damageCommits,
            int newFires,
            int endedFires,
            IReadOnlyList<string> failures)
        {
            if (steps < 0
                || damageCommits < 0
                || newFires < 0
                || endedFires < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(steps),
                    "Environmental-fire tick counts cannot be negative.");
            }

            Steps = steps;
            DamageCommits = damageCommits;
            NewFires = newFires;
            EndedFires = endedFires;
            Failures.AddRange(failures
                ?? throw new ArgumentNullException(nameof(failures)));
        }

        public int Steps { get; internal set; }
        public int DamageCommits { get; internal set; }
        public int NewFires { get; internal set; }
        public int EndedFires { get; internal set; }
        public List<string> Failures { get; } = new();
    }

    [Serializable]
    public sealed class DungeonEnvironmentalFireSaveData
    {
        public const int CurrentVersion = 4;
        public int version = CurrentVersion;
        public long nextFireSequence = 1;
        public long nextOutcomeSequence = 1;
        public float accumulator;
        public List<EnvironmentalFireSaveRecord> activeFires = new();
        public List<EnvironmentalFireHistorySaveRecord> history = new();
        public List<EnvironmentalFireCauseSaveRecord> processedCauses = new();
        public List<EnvironmentalFireSuppressionSaveRecord> suppressionOperations = new();
        public List<EnvironmentalFireDamageOutcomeSaveRecord> damageOperations = new();
    }

    public enum EnvironmentalFireDamageOutcomePhase
    {
        Prepared = 1,
        AwaitingWorldRemoval = 2,
        OutcomeCommitted = 3
    }

    [Serializable]
    public sealed class EnvironmentalFireDamageOutcomeSaveRecord
    {
        public string operationId = string.Empty;
        public string requestFingerprint = string.Empty;
        public string fireId = string.Empty;
        public int targetKind;
        public string targetId = string.Empty;
        public string targetDisplayName = string.Empty;
        public int positionX;
        public int positionY;
        public float intensity;
        public float requestedDamage;
        public float appliedDamage;
        public bool targetRemainsCombustible;
        public int absoluteDay;
        public long ownerRevision;
        public EnvironmentalFireDamageOutcomePhase phase;
        public string outcomeDigest = string.Empty;

        public EnvironmentalFireDamageOutcomeSaveRecord Clone() => new()
        {
            operationId = operationId,
            requestFingerprint = requestFingerprint,
            fireId = fireId,
            targetKind = targetKind,
            targetId = targetId,
            targetDisplayName = targetDisplayName,
            positionX = positionX,
            positionY = positionY,
            intensity = intensity,
            requestedDamage = requestedDamage,
            appliedDamage = appliedDamage,
            targetRemainsCombustible = targetRemainsCombustible,
            absoluteDay = absoluteDay,
            ownerRevision = ownerRevision,
            phase = phase,
            outcomeDigest = outcomeDigest
        };
    }

    public static class EnvironmentalFireDamageOutcomeIdentity
    {
        public static string BuildRequestFingerprint(
            in EnvironmentalFireDamageCommand command,
            string targetDisplayName,
            int absoluteDay)
        {
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append("environmental-fire-damage-request@1");
            digest.Append(command.OperationId);
            digest.Append(command.FireId);
            digest.Append((int)command.Target.Kind);
            digest.Append(command.Target.TargetId);
            digest.Append(command.Position.x);
            digest.Append(command.Position.y);
            digest.Append(command.Intensity.ToString("R", CultureInfo.InvariantCulture));
            digest.Append(command.RequestedDamage.ToString("R", CultureInfo.InvariantCulture));
            digest.Append(targetDisplayName?.Trim() ?? string.Empty);
            digest.Append(absoluteDay);
            return digest.ComputeSha256();
        }

        public static long BuildOwnerRevision(string operationId)
        {
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append("environmental-fire-damage-owner-revision@1");
            digest.Append(operationId?.Trim() ?? string.Empty);
            string value = digest.ComputeSha256();
            long revision = long.Parse(
                value.Substring(0, 15),
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            return Math.Max(1L, revision);
        }
    }

    [Serializable]
    public sealed class EnvironmentalFireSaveRecord
    {
        public string fireId = string.Empty;
        public string causeId = string.Empty;
        public int ignitionKind;
        public string producerId = string.Empty;
        public string evidenceId = string.Empty;
        public int targetKind;
        public string targetId = string.Empty;
        public int positionX;
        public int positionY;
        public float intensity;
        public float remainingFuel;
        public long stepIndex;
        public float totalDamage;
        public float totalSuppressionWork;
        public int totalWaterConsumed;
        public bool requiresElectricalIsolation;
        public int fuelLossTargetKind;
        public string fuelLossTargetId = string.Empty;
        public string fuelLossOperationId = string.Empty;
        public int fuelLossQuantity;
        public long fuelLossMassGrams;
        public string fuelLossCommitId = string.Empty;
        public bool fuelLossAcknowledged;
        public bool fuelLossPending;
    }

    [Serializable]
    public sealed class EnvironmentalFireHistorySaveRecord
    {
        public EnvironmentalFireSaveRecord fire = new();
        public int endReason;
    }

    [Serializable]
    public sealed class EnvironmentalFireCauseSaveRecord
    {
        public string causeId = string.Empty;
        public string fingerprint = string.Empty;
        public int disposition;
        public string fireId = string.Empty;
        public string reason = string.Empty;
        public long outcomeRevision;
        public int absoluteDay;
        public string targetDisplayName = string.Empty;
        public int positionX;
        public int positionY;
        public bool hasLocation;
        public float appliedIntensity;
        public bool legacyPreOutcome;
        public bool outcomePending;
    }

    [Serializable]
    public sealed class EnvironmentalFireSuppressionSaveRecord
    {
        public string operationId = string.Empty;
        public string fingerprint = string.Empty;
        public string fireId = string.Empty;
        public string workerId = string.Empty;
        public string workerDisplayName = string.Empty;
        public long outcomeRevision;
        public int absoluteDay;
        public bool legacyPreOutcome;
        public bool outcomePending;
        public int standX;
        public int standY;
        public int mode;
        public float approvedWork;
        public string waterLeaseId = string.Empty;
        public int waterQuantity;
        public int phase;
        public int resultDisposition;
        public float intensityBefore;
        public float intensityAfter;
        public string waterCommitId = string.Empty;
        public int waterCommittedQuantity;
        public bool waterAcknowledged;
        public string resultReason = string.Empty;
    }

    public sealed class EnvironmentalFireRestoreCandidate
    {
        internal EnvironmentalFireRestoreCandidate(
            DungeonEnvironmentalFireSaveData state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        private DungeonEnvironmentalFireSaveData State { get; }

        public DungeonEnvironmentalFireSaveData Capture() =>
            EnvironmentalFireRestoreRules.Clone(State);
    }

    public static class EnvironmentalFireRestoreRules
    {
        private const string FireIdPrefix = "environmental-fire:";

        public static EnvironmentalFireRestoreCandidate Prepare(
            DungeonEnvironmentalFireSaveData data,
            float tickInterval)
        {
            if (data == null)
                throw new InvalidOperationException(
                    "Environmental-fire payload is null.");
            if (!IsFinite(tickInterval) || tickInterval <= 0f)
                throw new ArgumentOutOfRangeException(nameof(tickInterval));
            if (data.version == 2)
                data = MigrateV2(data);
            if (data.version == 3)
                data = MigrateV3(data);
            if (data.version != DungeonEnvironmentalFireSaveData.CurrentVersion
                || data.activeFires == null
                || data.history == null
                || data.processedCauses == null
                || data.suppressionOperations == null
                || data.damageOperations == null)
            {
                throw new InvalidOperationException(
                    "Environmental-fire payload is incomplete or has an unsupported version.");
            }
            if (data.nextFireSequence <= 0
                || data.nextOutcomeSequence <= 0
                || !IsFinite(data.accumulator)
                || data.accumulator < 0f
                || data.accumulator >= tickInterval)
            {
                throw new InvalidOperationException(
                    "Environmental-fire clock or identity sequence is invalid.");
            }

            var fireIds = new HashSet<string>(StringComparer.Ordinal);
            var activeTargets = new HashSet<EnvironmentalFireTargetRef>();
            var fireCauseIds = new Dictionary<string, string>(StringComparer.Ordinal);
            long maximumSequence = 0;
            foreach (EnvironmentalFireSaveRecord fire in data.activeFires)
            {
                ValidateFire(fire, active: true);
                if (!fireIds.Add(fire.fireId)
                    || !activeTargets.Add(ToTarget(fire)))
                {
                    throw new InvalidOperationException(
                        "Environmental-fire payload has a duplicate active fire or target.");
                }

                fireCauseIds.Add(fire.fireId, fire.causeId);
                maximumSequence = Math.Max(maximumSequence, ParseSequence(fire.fireId));
            }

            foreach (EnvironmentalFireHistorySaveRecord entry in data.history)
            {
                if (entry == null || entry.fire == null
                    || !Enum.IsDefined(
                        typeof(EnvironmentalFireEndReason),
                        entry.endReason))
                {
                    throw new InvalidOperationException(
                        "Environmental-fire history contains an invalid entry.");
                }

                ValidateFire(entry.fire, active: false);
                if (!fireIds.Add(entry.fire.fireId))
                {
                    throw new InvalidOperationException(
                        "Environmental-fire history duplicates a fire identity.");
                }

                fireCauseIds.Add(entry.fire.fireId, entry.fire.causeId);
                maximumSequence = Math.Max(
                    maximumSequence,
                    ParseSequence(entry.fire.fireId));
            }

            if (data.nextFireSequence <= maximumSequence)
            {
                throw new InvalidOperationException(
                    "Environmental-fire identity sequence would reuse an existing ID.");
            }

            var causeIds = new HashSet<string>(StringComparer.Ordinal);
            var ignitedFireIds = new HashSet<string>(StringComparer.Ordinal);
            var outcomeRevisions = new HashSet<long>();
            long maximumOutcomeRevision = 0L;
            foreach (EnvironmentalFireCauseSaveRecord cause in data.processedCauses)
            {
                ValidateCause(cause, fireIds);
                if (!causeIds.Add(cause.causeId))
                {
                    throw new InvalidOperationException(
                        "Environmental-fire payload has a duplicate ignition cause.");
                }
                if (!cause.legacyPreOutcome)
                {
                    if (!outcomeRevisions.Add(cause.outcomeRevision))
                        throw new InvalidOperationException(
                            "Environmental-fire payload has a duplicate outcome revision.");
                    maximumOutcomeRevision = Math.Max(
                        maximumOutcomeRevision,
                        cause.outcomeRevision);
                }

                if ((EnvironmentalFireIgnitionDisposition)cause.disposition
                    == EnvironmentalFireIgnitionDisposition.Ignited)
                {
                    if (!ignitedFireIds.Add(cause.fireId)
                        || !fireCauseIds.TryGetValue(
                            cause.fireId,
                            out string fireCauseId)
                        || !string.Equals(
                            fireCauseId,
                            cause.causeId,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Environmental-fire ignition ownership is inconsistent.");
                    }
                }
            }

            if (ignitedFireIds.Count != fireIds.Count)
            {
                throw new InvalidOperationException(
                    "Every fire must retain exactly one accepted ignition cause.");
            }

            var operationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnvironmentalFireSuppressionSaveRecord operation
                     in data.suppressionOperations)
            {
                ValidateSuppression(operation, fireIds);
                if (!operationIds.Add(operation.operationId))
                {
                    throw new InvalidOperationException(
                        "Environmental-fire payload has a duplicate suppression operation.");
                }
                if (!operation.legacyPreOutcome)
                {
                    if (!outcomeRevisions.Add(operation.outcomeRevision))
                        throw new InvalidOperationException(
                            "Environmental-fire payload has a duplicate outcome revision.");
                    maximumOutcomeRevision = Math.Max(
                        maximumOutcomeRevision,
                        operation.outcomeRevision);
                }
            }

            foreach (EnvironmentalFireDamageOutcomeSaveRecord operation
                     in data.damageOperations)
            {
                ValidateDamage(operation, fireIds);
                if (!operationIds.Add(operation.operationId))
                {
                    throw new InvalidOperationException(
                        "Environmental-fire payload has a duplicate damage operation.");
                }
            }

            if (data.nextOutcomeSequence <= maximumOutcomeRevision)
                throw new InvalidOperationException(
                    "Environmental-fire outcome sequence would reuse an existing revision.");

            return new EnvironmentalFireRestoreCandidate(Clone(data));
        }

        private static DungeonEnvironmentalFireSaveData MigrateV2(
            DungeonEnvironmentalFireSaveData source)
        {
            if (source.activeFires == null
                || source.history == null
                || source.processedCauses == null
                || source.suppressionOperations == null)
                throw new InvalidOperationException(
                    "Environmental-fire V2 payload is incomplete.");
            DungeonEnvironmentalFireSaveData migrated = Clone(source);
            migrated.version = DungeonEnvironmentalFireSaveData.CurrentVersion;
            migrated.nextOutcomeSequence = 1L;
            foreach (EnvironmentalFireCauseSaveRecord cause in migrated.processedCauses)
            {
                cause.legacyPreOutcome = true;
                cause.outcomePending = false;
                cause.outcomeRevision = 0L;
                cause.absoluteDay = 0;
                cause.targetDisplayName = string.Empty;
                cause.positionX = 0;
                cause.positionY = 0;
                cause.hasLocation = false;
                cause.appliedIntensity = 0f;
            }
            foreach (EnvironmentalFireSuppressionSaveRecord operation
                     in migrated.suppressionOperations)
            {
                operation.legacyPreOutcome = true;
                operation.outcomePending = false;
                operation.outcomeRevision = 0L;
                operation.absoluteDay = 0;
                operation.workerDisplayName = string.Empty;
            }
            return migrated;
        }

        private static DungeonEnvironmentalFireSaveData MigrateV3(
            DungeonEnvironmentalFireSaveData source)
        {
            if (source.activeFires == null
                || source.history == null
                || source.processedCauses == null
                || source.suppressionOperations == null)
            {
                throw new InvalidOperationException(
                    "Environmental-fire V3 payload is incomplete.");
            }
            DungeonEnvironmentalFireSaveData migrated = Clone(source);
            migrated.version = DungeonEnvironmentalFireSaveData.CurrentVersion;
            migrated.damageOperations = new List<
                EnvironmentalFireDamageOutcomeSaveRecord>();
            return migrated;
        }

        internal static DungeonEnvironmentalFireSaveData Clone(
            DungeonEnvironmentalFireSaveData source)
        {
            var result = new DungeonEnvironmentalFireSaveData
            {
                version = source.version,
                nextFireSequence = source.nextFireSequence,
                nextOutcomeSequence = source.nextOutcomeSequence,
                accumulator = source.accumulator
            };
            foreach (EnvironmentalFireSaveRecord fire in source.activeFires)
                result.activeFires.Add(CloneFire(fire));
            foreach (EnvironmentalFireHistorySaveRecord entry in source.history)
            {
                result.history.Add(new EnvironmentalFireHistorySaveRecord
                {
                    fire = CloneFire(entry.fire),
                    endReason = entry.endReason
                });
            }
            foreach (EnvironmentalFireCauseSaveRecord cause
                     in source.processedCauses)
            {
                result.processedCauses.Add(new EnvironmentalFireCauseSaveRecord
                {
                    causeId = cause.causeId,
                    fingerprint = cause.fingerprint,
                    disposition = cause.disposition,
                    fireId = cause.fireId,
                    reason = cause.reason,
                    outcomeRevision = cause.outcomeRevision,
                    targetDisplayName = cause.targetDisplayName,
                    positionX = cause.positionX,
                    positionY = cause.positionY,
                    hasLocation = cause.hasLocation,
                    absoluteDay = cause.absoluteDay,
                    appliedIntensity = cause.appliedIntensity,
                    legacyPreOutcome = cause.legacyPreOutcome,
                    outcomePending = cause.outcomePending
                });
            }
            foreach (EnvironmentalFireSuppressionSaveRecord operation
                     in source.suppressionOperations)
            {
                result.suppressionOperations.Add(
                    CloneSuppression(operation));
            }
            foreach (EnvironmentalFireDamageOutcomeSaveRecord operation
                     in source.damageOperations
                        ?? new List<EnvironmentalFireDamageOutcomeSaveRecord>())
            {
                result.damageOperations.Add(operation?.Clone());
            }

            return result;
        }

        private static void ValidateFire(
            EnvironmentalFireSaveRecord fire,
            bool active)
        {
            bool hasFuelLoss = fire != null
                && (fire.fuelLossQuantity != 0
                    || fire.fuelLossMassGrams != 0L
                    || !string.IsNullOrEmpty(fire.fuelLossTargetId)
                    || !string.IsNullOrEmpty(fire.fuelLossOperationId)
                    || !string.IsNullOrEmpty(fire.fuelLossCommitId)
                    || fire.fuelLossAcknowledged
                    || fire.fuelLossPending);
            if (fire == null
                || !IsCanonical(fire.fireId)
                || !IsCanonical(fire.causeId)
                || !IsCanonical(fire.producerId)
                || !IsCanonical(fire.evidenceId)
                || !Enum.IsDefined(
                    typeof(EnvironmentalFireIgnitionKind),
                    fire.ignitionKind)
                || !ToTarget(fire).IsValid
                || ToTarget(fire).Kind
                    != EnvironmentalFireTargetKind.Building
                || !IsFinite(fire.intensity)
                || fire.intensity < 0f
                || fire.intensity > 1f
                || (active && fire.intensity <= 0f)
                || !IsFinite(fire.remainingFuel)
                || fire.remainingFuel < 0f
                || (active && fire.remainingFuel <= 0f)
                || fire.stepIndex < 0
                || !IsFiniteNonNegative(fire.totalDamage)
                || !IsFiniteNonNegative(fire.totalSuppressionWork)
                || fire.totalWaterConsumed < 0
                || ((EnvironmentalFireIgnitionKind)fire.ignitionKind
                        == EnvironmentalFireIgnitionKind.FeedSelfHeating)
                    != hasFuelLoss
                || (hasFuelLoss
                    && (fire.fuelLossTargetKind
                            != (int)EnvironmentalFireTargetKind.ItemStack
                        || !IsCanonical(fire.fuelLossTargetId)
                        || !IsCanonical(fire.fuelLossOperationId)
                        || fire.fuelLossQuantity <= 0
                        || (fire.fuelLossPending
                            ? fire.fuelLossMassGrams != 0L
                                || !string.IsNullOrEmpty(fire.fuelLossCommitId)
                                || fire.fuelLossAcknowledged
                            : fire.fuelLossMassGrams <= 0L
                                || !IsCanonical(fire.fuelLossCommitId)))))
            {
                throw new InvalidOperationException(
                    "Environmental-fire payload contains an invalid fire state.");
            }
        }

        private static void ValidateCause(
            EnvironmentalFireCauseSaveRecord cause,
            HashSet<string> fireIds)
        {
            bool legacy = cause?.legacyPreOutcome == true;
            if (cause == null
                || !IsCanonical(cause.causeId)
                || !IsCanonical(cause.fingerprint)
                || !IsCanonical(cause.reason)
                || !IsFinite(cause.appliedIntensity)
                || cause.appliedIntensity < 0f
                || cause.appliedIntensity > 1f
                || (legacy
                    ? cause.outcomeRevision != 0L
                        || cause.outcomePending
                        || cause.absoluteDay != 0
                        || !string.IsNullOrEmpty(cause.targetDisplayName)
                        || cause.hasLocation
                        || cause.appliedIntensity != 0f
                    : cause.outcomeRevision <= 0
                        || cause.absoluteDay < 0
                        || !IsCanonical(cause.targetDisplayName))
                || !Enum.IsDefined(
                    typeof(EnvironmentalFireIgnitionDisposition),
                    cause.disposition))
            {
                throw new InvalidOperationException(
                    "Environmental-fire payload contains an invalid ignition cause.");
            }

            var disposition =
                (EnvironmentalFireIgnitionDisposition)cause.disposition;
            bool requiresFire = disposition
                is EnvironmentalFireIgnitionDisposition.Ignited
                or EnvironmentalFireIgnitionDisposition.AlreadyBurning;
            bool noFire = disposition
                is EnvironmentalFireIgnitionDisposition.TargetMissing
                or EnvironmentalFireIgnitionDisposition.TargetNotCombustible
                or EnvironmentalFireIgnitionDisposition.FuelUnavailable;
            if ((!requiresFire && !noFire)
                || (requiresFire
                    && (!IsCanonical(cause.fireId)
                        || !fireIds.Contains(cause.fireId)))
                || (noFire && !string.IsNullOrEmpty(cause.fireId))
                || (cause.outcomePending
                    && disposition != EnvironmentalFireIgnitionDisposition.Ignited))
            {
                throw new InvalidOperationException(
                    "Environmental-fire ignition outcome is inconsistent.");
            }
        }

        private static void ValidateSuppression(
            EnvironmentalFireSuppressionSaveRecord operation,
            HashSet<string> fireIds)
        {
            bool legacy = operation?.legacyPreOutcome == true;
            if (operation == null
                || !IsCanonical(operation.operationId)
                || !IsCanonical(operation.fingerprint)
                || !IsCanonical(operation.fireId)
                || !fireIds.Contains(operation.fireId)
                || !IsCanonical(operation.workerId)
                || (legacy
                    ? !string.IsNullOrEmpty(operation.workerDisplayName)
                        || operation.outcomeRevision != 0L
                        || operation.absoluteDay != 0
                        || operation.outcomePending
                    : !IsCanonical(operation.workerDisplayName)
                        || operation.outcomeRevision <= 0
                        || operation.absoluteDay < 0)
                || !Enum.IsDefined(
                    typeof(EnvironmentalFireSuppressionMode),
                    operation.mode)
                || !Enum.IsDefined(
                    typeof(EnvironmentalFireSuppressionPhase),
                    operation.phase)
                || !Enum.IsDefined(
                    typeof(EnvironmentalFireSuppressionDisposition),
                    operation.resultDisposition)
                || !IsFinite(operation.approvedWork)
                || operation.approvedWork <= 0f
                || !IsFinite(operation.intensityBefore)
                || operation.intensityBefore <= 0f
                || operation.intensityBefore > 1f
                || !IsFinite(operation.intensityAfter)
                || operation.intensityAfter < 0f
                || operation.intensityAfter > operation.intensityBefore
                || !IsCanonical(operation.resultReason))
            {
                throw new InvalidOperationException(
                    "Environmental-fire payload contains an invalid suppression operation.");
            }

            var command = new EnvironmentalFireSuppressionCommand(
                operation.operationId,
                operation.fireId,
                operation.workerId,
                new Vector2Int(operation.standX, operation.standY),
                (EnvironmentalFireSuppressionMode)operation.mode,
                operation.approvedWork,
                operation.waterLeaseId,
                operation.waterQuantity,
                operation.workerDisplayName);
            if ((!legacy && !command.IsValid)
                || !string.Equals(
                    legacy ? command.LegacyFingerprint : command.Fingerprint,
                    operation.fingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Environmental-fire suppression fingerprint is inconsistent.");
            }

            var phase = (EnvironmentalFireSuppressionPhase)operation.phase;
            var result =
                (EnvironmentalFireSuppressionDisposition)operation.resultDisposition;
            bool hasCommittedWater = operation.waterCommittedQuantity > 0
                && IsCanonical(operation.waterCommitId)
                && operation.waterCommittedQuantity == operation.waterQuantity;
            if ((!legacy && operation.outcomePending
                    && phase != EnvironmentalFireSuppressionPhase.Prepared
                    && phase != EnvironmentalFireSuppressionPhase.Applied
                    && phase != EnvironmentalFireSuppressionPhase.WaterCommitted)
                || (phase == EnvironmentalFireSuppressionPhase.Prepared
                    && (result != EnvironmentalFireSuppressionDisposition.OperationInProgress
                        || hasCommittedWater
                        || operation.waterAcknowledged
                        || operation.intensityAfter != operation.intensityBefore))
                || (phase == EnvironmentalFireSuppressionPhase.WaterCommitted
                    && ((EnvironmentalFireSuppressionMode)operation.mode
                            != EnvironmentalFireSuppressionMode.Water
                        || result != EnvironmentalFireSuppressionDisposition.OperationInProgress
                        || !hasCommittedWater
                        || operation.waterAcknowledged
                        || operation.intensityAfter != operation.intensityBefore))
                || (phase == EnvironmentalFireSuppressionPhase.Applied
                    && (result
                            is not EnvironmentalFireSuppressionDisposition.Applied
                            and not EnvironmentalFireSuppressionDisposition.Extinguished
                            and not EnvironmentalFireSuppressionDisposition
                                .AppliedAwaitingWaterAcknowledgement
                        || ((EnvironmentalFireSuppressionMode)operation.mode
                                == EnvironmentalFireSuppressionMode.Water)
                            != hasCommittedWater
                        || (operation.waterAcknowledged
                            && result == EnvironmentalFireSuppressionDisposition
                                .AppliedAwaitingWaterAcknowledgement)))
                || (phase == EnvironmentalFireSuppressionPhase.Cancelled
                    && (result != EnvironmentalFireSuppressionDisposition.Cancelled
                        || hasCommittedWater
                        || operation.waterAcknowledged
                        || operation.intensityAfter != operation.intensityBefore)))
            {
                throw new InvalidOperationException(
                    "Environmental-fire suppression phase is inconsistent.");
            }
        }

        private static void ValidateDamage(
            EnvironmentalFireDamageOutcomeSaveRecord operation,
            HashSet<string> fireIds)
        {
            EnvironmentalFireDamageCommand command = operation == null
                ? default
                : new EnvironmentalFireDamageCommand(
                    operation.operationId,
                    operation.fireId,
                    new EnvironmentalFireTargetRef(
                        (EnvironmentalFireTargetKind)operation.targetKind,
                        operation.targetId),
                    new Vector2Int(operation.positionX, operation.positionY),
                    operation.intensity,
                    operation.requestedDamage);
            if (operation == null
                || !IsCanonical(operation.operationId)
                || !IsCanonical(operation.requestFingerprint)
                || operation.requestFingerprint.Length != 64
                || !IsCanonical(operation.fireId)
                || !fireIds.Contains(operation.fireId)
                || !Enum.IsDefined(
                    typeof(EnvironmentalFireTargetKind),
                    operation.targetKind)
                || (EnvironmentalFireTargetKind)operation.targetKind is not
                    EnvironmentalFireTargetKind.Building
                    and not EnvironmentalFireTargetKind.Character
                || !IsCanonical(operation.targetId)
                || !IsCanonical(operation.targetDisplayName)
                || !IsFinite(operation.intensity)
                || operation.intensity <= 0f
                || operation.intensity > 1f
                || !IsFinite(operation.requestedDamage)
                || operation.requestedDamage <= 0f
                || !IsFinite(operation.appliedDamage)
                || operation.appliedDamage < 0f
                || operation.appliedDamage
                    > operation.requestedDamage * 2f + 0.001f
                || operation.absoluteDay < 0
                || operation.ownerRevision !=
                    EnvironmentalFireDamageOutcomeIdentity.BuildOwnerRevision(
                        operation.operationId)
                || !string.Equals(
                    operation.requestFingerprint,
                    EnvironmentalFireDamageOutcomeIdentity
                        .BuildRequestFingerprint(
                            command,
                            operation.targetDisplayName,
                            operation.absoluteDay),
                    StringComparison.Ordinal)
                || !Enum.IsDefined(
                    typeof(EnvironmentalFireDamageOutcomePhase),
                    operation.phase)
                || (operation.phase ==
                        EnvironmentalFireDamageOutcomePhase.Prepared
                    && (operation.appliedDamage != 0f
                        || !string.IsNullOrEmpty(operation.outcomeDigest)))
                || (operation.phase !=
                        EnvironmentalFireDamageOutcomePhase.Prepared
                    && operation.appliedDamage <= 0f)
                || (operation.phase ==
                        EnvironmentalFireDamageOutcomePhase.AwaitingWorldRemoval
                    && (operation.targetKind !=
                            (int)EnvironmentalFireTargetKind.Building
                        || operation.targetRemainsCombustible))
                || (operation.phase ==
                        EnvironmentalFireDamageOutcomePhase.OutcomeCommitted
                    ? operation.outcomeDigest.Length != 0
                        && operation.outcomeDigest.Length != 64
                    : !string.IsNullOrEmpty(operation.outcomeDigest)))
            {
                throw new InvalidOperationException(
                    "Environmental-fire payload contains an invalid damage outcome owner.");
            }
        }

        private static EnvironmentalFireTargetRef ToTarget(
            EnvironmentalFireSaveRecord fire) =>
            new(
                (EnvironmentalFireTargetKind)fire.targetKind,
                fire.targetId);

        private static long ParseSequence(string fireId)
        {
            if (!fireId.StartsWith(FireIdPrefix, StringComparison.Ordinal)
                || !long.TryParse(
                    fireId.Substring(FireIdPrefix.Length),
                    out long sequence)
                || sequence <= 0)
            {
                throw new InvalidOperationException(
                    "Environmental-fire payload contains a noncanonical fire ID.");
            }

            return sequence;
        }

        private static EnvironmentalFireSaveRecord CloneFire(
            EnvironmentalFireSaveRecord fire) =>
            new()
            {
                fireId = fire.fireId,
                causeId = fire.causeId,
                ignitionKind = fire.ignitionKind,
                producerId = fire.producerId,
                evidenceId = fire.evidenceId,
                targetKind = fire.targetKind,
                targetId = fire.targetId,
                positionX = fire.positionX,
                positionY = fire.positionY,
                intensity = fire.intensity,
                remainingFuel = fire.remainingFuel,
                stepIndex = fire.stepIndex,
                totalDamage = fire.totalDamage,
                totalSuppressionWork = fire.totalSuppressionWork,
                totalWaterConsumed = fire.totalWaterConsumed,
                requiresElectricalIsolation = fire.requiresElectricalIsolation,
                fuelLossTargetKind = fire.fuelLossTargetKind,
                fuelLossTargetId = fire.fuelLossTargetId,
                fuelLossOperationId = fire.fuelLossOperationId,
                fuelLossQuantity = fire.fuelLossQuantity,
                fuelLossMassGrams = fire.fuelLossMassGrams,
                fuelLossCommitId = fire.fuelLossCommitId,
                fuelLossAcknowledged = fire.fuelLossAcknowledged,
                fuelLossPending = fire.fuelLossPending
            };

        private static EnvironmentalFireSuppressionSaveRecord CloneSuppression(
            EnvironmentalFireSuppressionSaveRecord operation) =>
            new()
            {
                operationId = operation.operationId,
                fingerprint = operation.fingerprint,
                fireId = operation.fireId,
                workerId = operation.workerId,
                workerDisplayName = operation.workerDisplayName,
                outcomeRevision = operation.outcomeRevision,
                absoluteDay = operation.absoluteDay,
                legacyPreOutcome = operation.legacyPreOutcome,
                outcomePending = operation.outcomePending,
                standX = operation.standX,
                standY = operation.standY,
                mode = operation.mode,
                approvedWork = operation.approvedWork,
                waterLeaseId = operation.waterLeaseId,
                waterQuantity = operation.waterQuantity,
                phase = operation.phase,
                resultDisposition = operation.resultDisposition,
                intensityBefore = operation.intensityBefore,
                intensityAfter = operation.intensityAfter,
                waterCommitId = operation.waterCommitId,
                waterCommittedQuantity = operation.waterCommittedQuantity,
                waterAcknowledged = operation.waterAcknowledged,
                resultReason = operation.resultReason
            };

        private static bool IsCanonical(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && value.IndexOf('\r') < 0
            && value.IndexOf('\n') < 0;

        private static bool IsFiniteNonNegative(float value) =>
            IsFinite(value) && value >= 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
