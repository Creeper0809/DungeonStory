using System;
using System.Collections.Generic;
using System.Linq;

public enum DefenseArmingPolicy
{
    Manual = 0,
    Safe = 1,
    Alert = 2,
    Aggressive = 3
}

public enum DefenseFacilityOperationalState
{
    Disarmed = 0,
    Preparing = 1,
    Ready = 2,
    Detecting = 3,
    Triggered = 4,
    Cooldown = 5,
    Reloading = 6,
    Empty = 7,
    Unpowered = 8,
    Faulted = 9,
    Jammed = 10,
    Damaged = 11,
    Destroyed = 12
}

[Flags]
public enum DefenseTriggerTiming
{
    None = 0,
    OnEnter = 1 << 0,
    Periodic = 1 << 1,
    Cooldown = 1 << 2,
    GuardResponse = 1 << 3
}

public enum DefenseAttackConcept
{
    None,
    Physical,
    Poison,
    Fire,
    Lightning,
    Ice,
    Guard
}

public enum DefenseTargetRule
{
    EnteringIntruder,
    IntrudersInRoom,
    AllIntrudersInRoom,
    GuardTarget
}

public enum DefenseStatusKind
{
    Corrosion,
    Burn,
    Charge,
    Slow
}

public readonly struct DefenseStatusSnapshot
{
    public DefenseStatusSnapshot(
        DefenseStatusKind kind,
        float value,
        float remainingSeconds,
        int stacks)
    {
        Kind = kind;
        Value = value;
        RemainingSeconds = Math.Max(0f, remainingSeconds);
        Stacks = Math.Max(0, stacks);
    }

    public DefenseStatusKind Kind { get; }
    public float Value { get; }
    public float RemainingSeconds { get; }
    public int Stacks { get; }
}

public enum DefenseSupplyKind
{
    None = 0,
    MetalParts = 1,
    Toxin = 2,
    Fuel = 3,
    Ammunition = 4,
    ElectricalCharge = 5,
    Treasury = 6
}

public static class DefenseFacilityFamilyIds
{
    public const string General = "defense:general";
    public const string Detection = "defense:detection";
    public const string Control = "defense:control";
    public const string Supply = "defense:supply";
    public const string Maintenance = "defense:maintenance";
    public const string Barrier = "defense:barrier";
    public const string Launcher = "defense:launcher";
    public const string ScatterTrap = "defense:scatter-trap";
    public const string BlastTrap = "defense:blast-trap";
    public const string MechanicalTrap = "defense:mechanical-trap";
    public const string Toxin = "defense:toxin";
    public const string Elemental = "defense:elemental";
    public const string GuardPost = "defense:guard-post";

    private static readonly IReadOnlyList<string> KnownValues =
        Array.AsReadOnly(new[]
        {
            General,
            Detection,
            Control,
            Supply,
            Maintenance,
            Barrier,
            Launcher,
            ScatterTrap,
            BlastTrap,
            MechanicalTrap,
            Toxin,
            Elemental,
            GuardPost
        });

    public static IReadOnlyList<string> Values => KnownValues;

    public static bool IsKnown(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && KnownValues.Contains(value, StringComparer.Ordinal);
}

public sealed class DefenseFacilityGrowthState
{
    public int capacityLevel;
    public int resetSpeedLevel;
    public int effectStrengthLevel;
    public int detectionRangeLevel;
    public int identificationLevel;
    public int outageResistanceLevel;

    public DefenseFacilityGrowthState DeepClone() => new()
    {
        capacityLevel = capacityLevel,
        resetSpeedLevel = resetSpeedLevel,
        effectStrengthLevel = effectStrengthLevel,
        detectionRangeLevel = detectionRangeLevel,
        identificationLevel = identificationLevel,
        outageResistanceLevel = outageResistanceLevel
    };
}

public enum DefenseFacilityPhysicalCommitPhase
{
    None = 0,
    IntentRecorded = 1,
    OutcomePublished = 2
}

public enum DefenseFacilityPhysicalCommitKind
{
    None = 0,
    MaintenanceSink = 1,
    SupplyTransfer = 2
}

[Serializable]
public sealed class DefenseFacilityPhysicalInputSaveData
{
    public string itemId = string.Empty;
    public string sourceStackId = string.Empty;
    public int quantity;

    public DefenseFacilityPhysicalInputSaveData DeepClone() => new()
    {
        itemId = itemId ?? string.Empty,
        sourceStackId = sourceStackId ?? string.Empty,
        quantity = quantity
    };
}

[Serializable]
public sealed class DefenseFacilityPhysicalCommitSaveData
{
    public DefenseFacilityPhysicalCommitPhase phase;
    public DefenseFacilityPhysicalCommitKind kind;
    public int operationSequence;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string destinationId = string.Empty;
    public string itemId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public string commitId = string.Empty;
    public long outcomeOwnerRevision;
    public string requestFingerprint = string.Empty;
    public int supplyBefore;
    public int supplyAfter;
    public int supplyUnitsGranted;
    public List<DefenseFacilityPhysicalInputSaveData> inputs = new();

    public DefenseFacilityPhysicalCommitSaveData DeepClone() => new()
    {
        phase = phase,
        kind = kind,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        reasonCode = reasonCode ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        itemId = itemId ?? string.Empty,
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        commitId = commitId ?? string.Empty,
        outcomeOwnerRevision = outcomeOwnerRevision,
        requestFingerprint = requestFingerprint ?? string.Empty,
        supplyBefore = supplyBefore,
        supplyAfter = supplyAfter,
        supplyUnitsGranted = supplyUnitsGranted,
        inputs = (inputs ?? new List<DefenseFacilityPhysicalInputSaveData>())
            .ConvertAll(value => value?.DeepClone())
    };
}

public enum DefenseFacilityServicePendingPhase
{
    None = 0,
    Requested = 1,
    TerminalApplied = 2
}

public enum DefenseFacilityServiceOutcomePhase
{
    Requested = 1,
    Completed = 2,
    FailedWithImpact = 3
}

[Serializable]
public sealed class DefenseFacilityServicePendingSaveData
{
    public DefenseFacilityServicePendingPhase phase;
    public DefenseFacilityPhysicalCommitKind kind;
    public int operationSequence;
    public long requestRevision;
    public long terminalRevision;
    public int absoluteDay;
    public DefenseFacilityOperationalState beforeState;
    public DefenseFacilityOperationalState requestedState;
    public DefenseFacilityOperationalState terminalState;
    public int beforeSupply;
    public int requestedSupply;
    public int terminalSupply;
    public bool requestAcknowledged;
    public DefenseFacilityServiceOutcomePhase terminalPhase;
    public string physicalOperationId = string.Empty;
    public long physicalOwnerRevision;
    public string itemId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;

    public DefenseFacilityServicePendingSaveData DeepClone() =>
        (DefenseFacilityServicePendingSaveData)MemberwiseClone();
}

public sealed class DefenseFacilityState
{
    public string facilityPersistentId = string.Empty;
    public int buildingId;
    public int gridX;
    public int gridY;
    public DefenseArmingPolicy armingPolicy = DefenseArmingPolicy.Safe;
    public DefenseFacilityOperationalState operationalState =
        DefenseFacilityOperationalState.Ready;
    public float condition = 100f;
    public int supply;
    public int activationCount;
    public float cooldownUntil;
    public bool forcedDangerousOperation;
    public int allowedGroups = DefenseFacilityRules.DefaultAllowedGroups;
    public List<string> allowedPersistentIds = new();
    public DefenseFacilityGrowthState growth = new();
    public string blockedReason = string.Empty;
    public int nextMaintenanceOperationSequence;
    public DefenseFacilityPhysicalCommitSaveData pendingMaintenance = new();
    public int nextSupplyOperationSequence;
    public DefenseFacilityPhysicalCommitSaveData pendingSupply = new();
    public long serviceRevision;
    public int nextServiceOperationSequence;
    public DefenseFacilityServicePendingSaveData pendingService = new();

    public DefenseFacilityState DeepClone() => new()
    {
        facilityPersistentId = facilityPersistentId ?? string.Empty,
        buildingId = buildingId,
        gridX = gridX,
        gridY = gridY,
        armingPolicy = armingPolicy,
        operationalState = operationalState,
        condition = condition,
        supply = supply,
        activationCount = activationCount,
        cooldownUntil = cooldownUntil,
        forcedDangerousOperation = forcedDangerousOperation,
        allowedGroups = allowedGroups,
        allowedPersistentIds = new List<string>(
            allowedPersistentIds ?? new List<string>()),
        growth = growth?.DeepClone() ?? new DefenseFacilityGrowthState(),
        blockedReason = blockedReason ?? string.Empty,
        nextMaintenanceOperationSequence = nextMaintenanceOperationSequence,
        pendingMaintenance = pendingMaintenance?.DeepClone()
            ?? new DefenseFacilityPhysicalCommitSaveData(),
        nextSupplyOperationSequence = nextSupplyOperationSequence,
        pendingSupply = pendingSupply?.DeepClone()
            ?? new DefenseFacilityPhysicalCommitSaveData(),
        serviceRevision = serviceRevision,
        nextServiceOperationSequence = nextServiceOperationSequence,
        pendingService = pendingService?.DeepClone()
            ?? new DefenseFacilityServicePendingSaveData()
    };
}

public sealed class DefenseFacilityAggregateState
{
    private readonly Dictionary<string, DefenseFacilityState> states =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<DefenseFacilityState> States => states.Values;

    public bool TryGet(string id, out DefenseFacilityState state) =>
        states.TryGetValue(id ?? string.Empty, out state);

    public void Add(DefenseFacilityState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.facilityPersistentId))
        {
            throw new ArgumentException("Defense state requires a persistent facility ID.", nameof(state));
        }
        states.Add(state.facilityPersistentId, state);
    }

    public void Set(DefenseFacilityState state)
    {
        if (state == null || string.IsNullOrWhiteSpace(state.facilityPersistentId))
        {
            throw new ArgumentException(
                "Defense state requires a persistent facility ID.",
                nameof(state));
        }
        states[state.facilityPersistentId] = state;
    }

    public DefenseFacilityAggregateState DeepClone()
    {
        DefenseFacilityAggregateState clone = new();
        foreach (DefenseFacilityState state in states.Values)
        {
            clone.Add(state.DeepClone());
        }
        return clone;
    }
}

[Serializable]
public sealed class DefenseFacilityGrowthSaveData
{
    public int capacityLevel;
    public int resetSpeedLevel;
    public int effectStrengthLevel;
    public int detectionRangeLevel;
    public int identificationLevel;
    public int outageResistanceLevel;
}

[Serializable]
public sealed class DefenseFacilityRecordSaveData
{
    public string facilityPersistentId = string.Empty;
    public int buildingId;
    public int gridX;
    public int gridY;
    public DefenseArmingPolicy armingPolicy = DefenseArmingPolicy.Safe;
    public DefenseFacilityOperationalState operationalState =
        DefenseFacilityOperationalState.Ready;
    public float condition = 100f;
    public int supply;
    public int activationCount;
    public float cooldownUntil;
    public bool forcedDangerousOperation;
    public int allowedGroups = DefenseFacilityRules.DefaultAllowedGroups;
    public List<string> allowedPersistentIds = new();
    public DefenseFacilityGrowthSaveData growth = new();
    public string blockedReason = string.Empty;
    public int nextMaintenanceOperationSequence;
    public DefenseFacilityPhysicalCommitSaveData pendingMaintenance = new();
    public int nextSupplyOperationSequence;
    public DefenseFacilityPhysicalCommitSaveData pendingSupply = new();
    public long serviceRevision;
    public int nextServiceOperationSequence;
    public DefenseFacilityServicePendingSaveData pendingService = new();
}

[Serializable]
public sealed class DefenseFacilitySaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<DefenseFacilityRecordSaveData> facilities = new();
}

public interface IDefenseFacilityPersistence
{
    DefenseFacilitySaveData CaptureState();
    DefenseFacilityRestoreCandidate PrepareRestoreState(
        DefenseFacilitySaveData data);
    void PublishRestoreState(DefenseFacilityRestoreCandidate candidate);
}

public sealed class DefenseFacilityRestoreCandidate
{
    public DefenseFacilityRestoreCandidate(
        DefenseFacilityAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public DefenseFacilityAggregateState State { get; }
}

public readonly struct DefenseFacilitySnapshot
{
    public DefenseFacilitySnapshot(
        DefenseFacilityState state,
        float cooldownRemaining,
        bool powered,
        string destinationId)
    {
        PersistentId = state?.facilityPersistentId ?? string.Empty;
        ArmingPolicy = state?.armingPolicy ?? DefenseArmingPolicy.Safe;
        OperationalState = state?.operationalState
            ?? DefenseFacilityOperationalState.Ready;
        Condition = state?.condition ?? 100f;
        Supply = state?.supply ?? 0;
        ActivationCount = state?.activationCount ?? 0;
        CooldownRemaining = Math.Max(0f, cooldownRemaining);
        Powered = powered;
        BlockedReason = state?.blockedReason ?? string.Empty;
        SupplyDestinationId = destinationId ?? string.Empty;
        HasPendingMaintenance = state != null
            && ((state.pendingMaintenance?.phase
                    ?? DefenseFacilityPhysicalCommitPhase.None)
                != DefenseFacilityPhysicalCommitPhase.None
                || (state.pendingService?.kind
                    ?? DefenseFacilityPhysicalCommitKind.None)
                == DefenseFacilityPhysicalCommitKind.MaintenanceSink);
    }

    public string PersistentId { get; }
    public DefenseArmingPolicy ArmingPolicy { get; }
    public DefenseFacilityOperationalState OperationalState { get; }
    public float Condition { get; }
    public int Supply { get; }
    public int ActivationCount { get; }
    public float CooldownRemaining { get; }
    public bool Powered { get; }
    public string BlockedReason { get; }
    public string SupplyDestinationId { get; }
    public bool HasPendingMaintenance { get; }
}

public readonly struct DefenseActivationAuthorization
{
    public DefenseActivationAuthorization(
        bool allowed,
        bool jammed,
        bool misfired,
        float effectMultiplier)
    {
        Allowed = allowed;
        Jammed = jammed;
        Misfired = misfired;
        EffectMultiplier = Math.Max(0f, effectMultiplier);
    }

    public bool Allowed { get; }
    public bool Jammed { get; }
    public bool Misfired { get; }
    public float EffectMultiplier { get; }
    public static DefenseActivationAuthorization Granted =>
        new(true, false, false, 1f);
}

public readonly struct DefenseFacilityStateChangedEvent
{
    public DefenseFacilityStateChangedEvent(
        string facilityPersistentId,
        DefenseFacilityOperationalState state,
        string reason)
    {
        FacilityPersistentId = facilityPersistentId ?? string.Empty;
        State = state;
        Reason = reason ?? string.Empty;
    }

    public string FacilityPersistentId { get; }
    public DefenseFacilityOperationalState State { get; }
    public string Reason { get; }
}

public static class DefenseFacilityRules
{
    public const int AllAccessGroupsMask = 127;
    public const int DefaultAllowedGroups = 79;

    public static float ClampPercent(float value) =>
        Math.Max(0f, Math.Min(100f, value));

    public static float ResolveCooldown(
        float currentTime,
        float cooldownSeconds,
        int resetSpeedLevel) =>
        currentTime
        + Math.Max(0f, cooldownSeconds)
        / (1f + Math.Max(0, resetSpeedLevel) * 0.1f);

    public static bool Roll(
        string persistentId,
        int activationCount,
        string channel,
        float chance)
    {
        float normalizedChance = Math.Max(0f, Math.Min(1f, chance));
        if (normalizedChance <= 0f)
        {
            return false;
        }
        int hash = StableHash(
            (persistentId ?? string.Empty)
            + "|"
            + (channel ?? string.Empty)
            + "|"
            + activationCount);
        float sample = (hash & 0x7fffffff) / (float)int.MaxValue;
        return sample < normalizedChance;
    }

    public static bool IsCanonicalBuildingId(string value) =>
        IsCanonical(value) && value.StartsWith("building:", StringComparison.Ordinal)
        && value.Length > "building:".Length;

    public static bool IsCanonicalCharacterId(string value) => IsCanonical(value);

    public static bool IsCanonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char character in value ?? string.Empty)
            {
                hash = hash * 31 + character;
            }
            return hash;
        }
    }
}

public static class DefenseFacilitySaveRules
{
    public static IReadOnlyList<string> Validate(DefenseFacilitySaveData data)
    {
        List<string> errors = new();
        if (data == null)
        {
            errors.Add("Defense facility payload is null.");
            return errors;
        }
        if (data.version != DefenseFacilitySaveData.CurrentVersion)
        {
            errors.Add($"Defense facility payload version {data.version} is unsupported.");
        }
        if (data.facilities == null)
        {
            errors.Add("Defense facility payload has no facility list.");
            return errors;
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        string previous = string.Empty;
        foreach (DefenseFacilityRecordSaveData facility in data.facilities)
        {
            string id = facility?.facilityPersistentId ?? string.Empty;
            if (facility == null || !DefenseFacilityRules.IsCanonicalBuildingId(id))
            {
                errors.Add("Defense facility payload contains a null or non-canonical facility ID.");
                continue;
            }
            if (!ids.Add(id))
            {
                errors.Add($"Defense facility payload contains duplicate facility '{id}'.");
            }
            if (previous.Length > 0 && string.CompareOrdinal(previous, id) >= 0)
            {
                errors.Add("Defense facility payload is not in canonical facility-ID order.");
            }
            previous = id;
            ValidateFacility(facility, id, errors);
        }
        return errors;
    }

    private static void ValidateFacility(
        DefenseFacilityRecordSaveData facility,
        string id,
        ICollection<string> errors)
    {
        if (facility.buildingId < 0)
            errors.Add($"Defense facility '{id}' has a negative building ID.");
        if (!Enum.IsDefined(typeof(DefenseArmingPolicy), facility.armingPolicy)
            || !Enum.IsDefined(typeof(DefenseFacilityOperationalState), facility.operationalState))
            errors.Add($"Defense facility '{id}' has an invalid enum value.");
        if (!IsFinite(facility.condition) || facility.condition < 0f || facility.condition > 100f
            || !IsFinite(facility.cooldownUntil) || facility.cooldownUntil < 0f)
            errors.Add($"Defense facility '{id}' has invalid condition or cooldown state.");
        if (facility.supply < 0 || facility.activationCount < 0)
            errors.Add($"Defense facility '{id}' has a negative supply or activation count.");
        if (facility.nextMaintenanceOperationSequence < 0
            || facility.nextSupplyOperationSequence < 0)
            errors.Add($"Defense facility '{id}' has a negative physical operation sequence.");
        if (facility.serviceRevision < 0L
            || facility.nextServiceOperationSequence < 0
            || facility.pendingService == null
                && (facility.serviceRevision != 0L
                    || facility.nextServiceOperationSequence != 0))
            errors.Add($"Defense facility '{id}' has invalid service revision or pending state.");
        else if (facility.pendingService != null)
            ValidateService(facility, id, errors);
        if ((facility.allowedGroups & ~DefenseFacilityRules.AllAccessGroupsMask) != 0)
            errors.Add($"Defense facility '{id}' has unknown access-group flags.");
        ValidateAllowed(facility, id, errors);
        ValidateGrowth(facility.growth, id, errors);
        ValidatePhysicalCommit(
            facility.pendingMaintenance,
            DefenseFacilityPhysicalCommitKind.MaintenanceSink,
            facility.nextMaintenanceOperationSequence,
            id,
            errors);
        ValidatePhysicalCommit(
            facility.pendingSupply,
            DefenseFacilityPhysicalCommitKind.SupplyTransfer,
            facility.nextSupplyOperationSequence,
            id,
            errors);
        if (facility.blockedReason == null
            || !string.Equals(facility.blockedReason, facility.blockedReason.Trim(), StringComparison.Ordinal))
            errors.Add($"Defense facility '{id}' has a non-canonical blocked reason.");
    }

    private static void ValidateAllowed(
        DefenseFacilityRecordSaveData facility,
        string id,
        ICollection<string> errors)
    {
        if (facility.allowedPersistentIds == null)
        {
            errors.Add($"Defense facility '{id}' has no allowed-character list.");
            return;
        }
        string previous = string.Empty;
        foreach (string characterId in facility.allowedPersistentIds)
        {
            if (!DefenseFacilityRules.IsCanonicalCharacterId(characterId)
                || previous.Length > 0 && string.CompareOrdinal(previous, characterId) >= 0)
                errors.Add($"Defense facility '{id}' has a null, duplicate, unordered, or non-canonical allowed character ID.");
            previous = characterId ?? string.Empty;
        }
    }

    private static void ValidateGrowth(
        DefenseFacilityGrowthSaveData growth,
        string id,
        ICollection<string> errors)
    {
        if (growth == null)
        {
            errors.Add($"Defense facility '{id}' has no growth state.");
            return;
        }
        if (growth.capacityLevel < 0 || growth.resetSpeedLevel < 0
            || growth.effectStrengthLevel < 0 || growth.detectionRangeLevel < 0
            || growth.identificationLevel < 0 || growth.outageResistanceLevel < 0)
            errors.Add($"Defense facility '{id}' has a negative growth level.");
    }

    private static void ValidatePhysicalCommit(
        DefenseFacilityPhysicalCommitSaveData pending,
        DefenseFacilityPhysicalCommitKind expectedKind,
        int expectedSequence,
        string facilityId,
        ICollection<string> errors)
    {
        if (pending == null)
        {
            errors.Add($"Defense facility '{facilityId}' has no physical commit state.");
            return;
        }

        if (pending.phase == DefenseFacilityPhysicalCommitPhase.None)
        {
            bool empty = pending.kind == DefenseFacilityPhysicalCommitKind.None
                && pending.operationSequence == 0
                && string.IsNullOrEmpty(pending.operationId)
                && string.IsNullOrEmpty(pending.reasonCode)
                && string.IsNullOrEmpty(pending.destinationId)
                && string.IsNullOrEmpty(pending.itemId)
                && pending.inputQuantity == 0
                && pending.inputMassGrams == 0L
                && string.IsNullOrEmpty(pending.commitId)
                && pending.outcomeOwnerRevision == 0L
                && string.IsNullOrEmpty(pending.requestFingerprint)
                && pending.supplyBefore == 0
                && pending.supplyAfter == 0
                && pending.supplyUnitsGranted == 0
                && pending.inputs != null
                && pending.inputs.Count == 0;
            if (!empty)
            {
                errors.Add($"Defense facility '{facilityId}' has partial empty physical commit state.");
            }
            return;
        }

        bool canonicalInputs = pending.inputs != null
            && pending.inputs.Count > 0;
        string previousSource = string.Empty;
        long totalQuantity = 0L;
        if (canonicalInputs)
        {
            foreach (DefenseFacilityPhysicalInputSaveData input in pending.inputs)
            {
                if (input == null
                    || !DefenseFacilityRules.IsCanonical(input.itemId)
                    || !DefenseFacilityRules.IsCanonical(input.sourceStackId)
                    || !string.Equals(input.itemId, pending.itemId, StringComparison.Ordinal)
                    || input.quantity <= 0
                    || previousSource.Length > 0
                        && string.CompareOrdinal(previousSource, input.sourceStackId) >= 0)
                {
                    canonicalInputs = false;
                    break;
                }
                previousSource = input.sourceStackId;
                totalQuantity += input.quantity;
            }
        }

        bool valid = Enum.IsDefined(
                typeof(DefenseFacilityPhysicalCommitPhase),
                pending.phase)
            && pending.phase != DefenseFacilityPhysicalCommitPhase.None
            && pending.kind == expectedKind
            && pending.operationSequence == expectedSequence
            && DefenseFacilityRules.IsCanonical(pending.operationId)
            && DefenseFacilityRules.IsCanonical(pending.reasonCode)
            && DefenseFacilityRules.IsCanonical(pending.destinationId)
            && DefenseFacilityRules.IsCanonical(pending.itemId)
            && pending.inputQuantity > 0
            && pending.inputMassGrams > 0L
            && DefenseFacilityRules.IsCanonical(pending.commitId)
            && pending.outcomeOwnerRevision >= 0L
            && DefenseFacilityRules.IsCanonical(pending.requestFingerprint)
            && pending.supplyBefore >= 0
            && pending.supplyAfter >= 0
            && canonicalInputs
            && totalQuantity == pending.inputQuantity;
        if (expectedKind == DefenseFacilityPhysicalCommitKind.MaintenanceSink)
        {
            valid &= pending.supplyUnitsGranted == 0
                && pending.supplyAfter == pending.supplyBefore;
        }
        else
        {
            long expectedSupplyAfter = (long)pending.supplyBefore
                + pending.supplyUnitsGranted;
            valid &= pending.supplyUnitsGranted > 0
                && expectedSupplyAfter <= int.MaxValue
                && pending.supplyAfter == expectedSupplyAfter;
        }
        if (!valid)
        {
            errors.Add($"Defense facility '{facilityId}' has an invalid {expectedKind} commit.");
        }
    }

    private static void ValidateService(
        DefenseFacilityRecordSaveData facility,
        string id,
        ICollection<string> errors)
    {
        DefenseFacilityServicePendingSaveData pending = facility.pendingService;
        if (pending.phase == DefenseFacilityServicePendingPhase.None)
        {
            if (pending.kind != DefenseFacilityPhysicalCommitKind.None
                || pending.operationSequence != 0
                || pending.requestRevision != 0L
                || pending.terminalRevision != 0L
                || pending.absoluteDay != 0
                || pending.beforeSupply != 0
                || pending.requestedSupply != 0
                || pending.terminalSupply != 0
                || pending.requestAcknowledged
                || pending.terminalPhase !=
                    (DefenseFacilityServiceOutcomePhase)0
                || !string.IsNullOrEmpty(pending.physicalOperationId)
                || pending.physicalOwnerRevision != 0L
                || !string.IsNullOrEmpty(pending.itemId)
                || pending.inputQuantity != 0
                || pending.inputMassGrams != 0L)
                errors.Add($"Defense facility '{id}' has partial empty service state.");
            return;
        }
        if (!Enum.IsDefined(typeof(DefenseFacilityServicePendingPhase), pending.phase)
            || pending.kind is not (DefenseFacilityPhysicalCommitKind.SupplyTransfer
                or DefenseFacilityPhysicalCommitKind.MaintenanceSink)
            || pending.operationSequence != facility.nextServiceOperationSequence
            || pending.absoluteDay < 0
            || pending.beforeSupply < 0 || pending.requestedSupply < 0
            || pending.terminalSupply < 0
            || pending.beforeSupply != pending.requestedSupply
            || pending.requestRevision <= 0L
            || pending.terminalRevision < 0L
            || pending.requestRevision > facility.serviceRevision
            || pending.terminalRevision > facility.serviceRevision
            || !Enum.IsDefined(typeof(DefenseFacilityOperationalState), pending.beforeState)
            || !Enum.IsDefined(typeof(DefenseFacilityOperationalState), pending.requestedState)
            || !Enum.IsDefined(typeof(DefenseFacilityOperationalState), pending.terminalState)
            || pending.phase == DefenseFacilityServicePendingPhase.Requested
                && (pending.terminalRevision != 0L
                    || facility.serviceRevision != pending.requestRevision
                    || pending.terminalPhase !=
                        (DefenseFacilityServiceOutcomePhase)0
                    || !string.IsNullOrEmpty(pending.physicalOperationId))
            || pending.phase == DefenseFacilityServicePendingPhase.TerminalApplied
                && (pending.terminalRevision != pending.requestRevision + 1L
                    || facility.serviceRevision != pending.terminalRevision
                    || pending.terminalPhase is not (
                        DefenseFacilityServiceOutcomePhase.Completed
                        or DefenseFacilityServiceOutcomePhase.FailedWithImpact)))
        {
            errors.Add($"Defense facility '{id}' has invalid service pending state.");
        }
        if (pending.phase == DefenseFacilityServicePendingPhase.TerminalApplied)
        {
            DefenseFacilityPhysicalCommitSaveData physical = pending.kind
                == DefenseFacilityPhysicalCommitKind.MaintenanceSink
                    ? facility.pendingMaintenance
                    : facility.pendingSupply;
            bool completed = pending.terminalPhase
                == DefenseFacilityServiceOutcomePhase.Completed;
            if (completed && (physical == null
                    || physical.phase != DefenseFacilityPhysicalCommitPhase.OutcomePublished
                    || !string.Equals(pending.physicalOperationId,
                        physical.operationId, StringComparison.Ordinal)
                    || pending.physicalOwnerRevision <= 0L
                    || pending.physicalOwnerRevision != physical.outcomeOwnerRevision
                    || !string.Equals(pending.itemId,
                        physical.itemId, StringComparison.Ordinal)
                    || pending.inputQuantity != physical.inputQuantity
                    || pending.inputMassGrams != physical.inputMassGrams)
                || !completed && (!string.IsNullOrEmpty(pending.physicalOperationId)
                    || pending.physicalOwnerRevision != 0L
                    || !string.IsNullOrEmpty(pending.itemId)
                    || pending.inputQuantity != 0
                    || pending.inputMassGrams != 0L))
                errors.Add($"Defense facility '{id}' has a mismatched service terminal child.");
        }
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
