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
        blockedReason = blockedReason ?? string.Empty
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
}

[Serializable]
public sealed class DefenseFacilitySaveData
{
    public const int CurrentVersion = 1;
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
        if ((facility.allowedGroups & ~DefenseFacilityRules.AllAccessGroupsMask) != 0)
            errors.Add($"Defense facility '{id}' has unknown access-group flags.");
        ValidateAllowed(facility, id, errors);
        ValidateGrowth(facility.growth, id, errors);
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

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);
}
