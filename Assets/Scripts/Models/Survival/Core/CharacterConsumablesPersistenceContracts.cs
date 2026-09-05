using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ConsumableOperationId : IEquatable<ConsumableOperationId>
{
    private readonly string value;

    public ConsumableOperationId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => CharacterConsumableIdContract.IsValidOperation(Value);
    public bool Equals(ConsumableOperationId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is ConsumableOperationId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator ConsumableOperationId(string value) => new(value);
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ConsumableDeliveryId : IEquatable<ConsumableDeliveryId>
{
    private readonly string value;

    public ConsumableDeliveryId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => CharacterConsumableIdContract.IsValidDelivery(Value);
    public bool Equals(ConsumableDeliveryId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is ConsumableDeliveryId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator ConsumableDeliveryId(string value) => new(value);
}

internal enum ConsumableGeneratedIdKind
{
    External,
    Generated,
    Malformed
}

internal static class CharacterConsumableIdContract
{
    internal const string OperationPrefix = "consumable-operation:";
    internal const string DeliveryPrefix = "consumable-delivery:";
    internal const string AutomaticNamespace = "auto:";
    internal const string AutomaticV1Namespace = "auto:v1:";

    internal static bool IsValidOperation(string id) =>
        PersistentEntityId.IsKind(id, "consumable-operation")
        && ClassifyOperation(id, out _) != ConsumableGeneratedIdKind.Malformed;

    internal static bool IsValidDelivery(string id) =>
        PersistentEntityId.IsKind(id, "consumable-delivery")
        && ClassifyDelivery(id, out _) != ConsumableGeneratedIdKind.Malformed;

    internal static bool IsExternalOperation(string id) =>
        ClassifyOperation(id, out _) == ConsumableGeneratedIdKind.External;

    internal static bool IsCurrentAutomaticOperation(string id) =>
        id != null
        && id.StartsWith(
            OperationPrefix + AutomaticV1Namespace,
            StringComparison.Ordinal)
        && ClassifyOperation(id, out _) == ConsumableGeneratedIdKind.Generated;

    internal static ConsumableOperationId CreateAutomaticOperation(long sequence) =>
        new(CreateAutomaticId(OperationPrefix, sequence));

    internal static ConsumableDeliveryId CreateAutomaticDelivery(long sequence) =>
        new(CreateAutomaticId(DeliveryPrefix, sequence));

    internal static ConsumableGeneratedIdKind ClassifyOperation(
        string id,
        out long sequence) =>
        Classify(id, OperationPrefix, out sequence);

    internal static ConsumableGeneratedIdKind ClassifyDelivery(
        string id,
        out long sequence) =>
        Classify(id, DeliveryPrefix, out sequence);

    private static string CreateAutomaticId(string prefix, long sequence)
    {
        if (sequence < 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence));
        }
        return prefix + AutomaticV1Namespace
            + sequence.ToString("D16", CultureInfo.InvariantCulture);
    }

    private static ConsumableGeneratedIdKind Classify(
        string id,
        string prefix,
        out long sequence)
    {
        sequence = 0L;
        if (string.IsNullOrEmpty(id)
            || !id.StartsWith(prefix, StringComparison.Ordinal))
        {
            return ConsumableGeneratedIdKind.External;
        }

        string suffix = id.Substring(prefix.Length);
        if (suffix.StartsWith(AutomaticNamespace, StringComparison.Ordinal))
        {
            if (!suffix.StartsWith(AutomaticV1Namespace, StringComparison.Ordinal))
            {
                return ConsumableGeneratedIdKind.Malformed;
            }
            string automaticSequence = suffix.Substring(AutomaticV1Namespace.Length);
            return TryParseCanonicalSequence(automaticSequence, out sequence)
                ? ConsumableGeneratedIdKind.Generated
                : ConsumableGeneratedIdKind.Malformed;
        }

        // V18 originally emitted an un-namespaced D16 suffix. Exact legacy values
        // remain reserved so their sequences continue to protect the watermark.
        return TryParseCanonicalSequence(suffix, out sequence)
            ? ConsumableGeneratedIdKind.Generated
            : ConsumableGeneratedIdKind.External;
    }

    private static bool TryParseCanonicalSequence(string value, out long sequence)
    {
        if (long.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out sequence)
            && sequence >= 1L
            && string.Equals(
                value,
                sequence.ToString("D16", CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            return true;
        }
        sequence = 0L;
        return false;
    }
}

/// <summary>
/// Survival-owned definition identifier used by persistence snapshots. The
/// outer item adapter resolves this value against authored item content.
/// </summary>
[Serializable]
public readonly struct ConsumableItemDefinitionId : IEquatable<ConsumableItemDefinitionId>
{
    private readonly string value;

    public ConsumableItemDefinitionId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    public bool Equals(ConsumableItemDefinitionId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is ConsumableItemDefinitionId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator ConsumableItemDefinitionId(string value) => new(value);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterDietPolicyKind
{
    Free = 0,
    Vegan = 1,
    Vegetarian = 2,
    CarnivorePreferred = 3,
    StrictTaboo = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum SubstancePolicyMode
{
    Forbidden = 0,
    MedicalOnly = 1,
    CombatOnly = 2,
    MoodThreshold = 3,
    Scheduled = 4
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterDietPolicyState
{
    public string characterId = string.Empty;
    public CharacterDietPolicyKind policy = CharacterDietPolicyKind.Free;

    public CharacterId CharacterId => (CharacterId)characterId;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSubstancePolicyState
{
    public string characterId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public SubstancePolicyMode mode = SubstancePolicyMode.Forbidden;
    [Range(0f, 100f)] public float moodThreshold = 30f;
    public int scheduledHour = 20;

    public CharacterId CharacterId => (CharacterId)characterId;
    public ConsumableItemDefinitionId ItemDefinitionId =>
        (ConsumableItemDefinitionId)itemDefinitionId;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterSubstanceState
{
    public string characterId = string.Empty;
    public string itemDefinitionId = string.Empty;
    [Range(0f, 100f)] public float tolerance;
    [Range(0f, 100f)] public float addiction;
    [Range(0f, 100f)] public float withdrawal;
    public float activeSeconds;
    public float secondsSinceLastDose;
    public float scheduledCooldownSeconds;
    public bool addicted;
    public bool overdosed;

    public CharacterId CharacterId => (CharacterId)characterId;
    public ConsumableItemDefinitionId ItemDefinitionId =>
        (ConsumableItemDefinitionId)itemDefinitionId;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterMealDeliveryState
{
    public string deliveryId = string.Empty;
    public string characterId = string.Empty;
    public string buildingInstanceId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public float requestedAt;
    public float retryAfter;

    public ConsumableDeliveryId DeliveryId =>
        (ConsumableDeliveryId)deliveryId;
    public CharacterId CharacterId => (CharacterId)characterId;
    public BuildingInstanceId BuildingInstanceId =>
        (BuildingInstanceId)buildingInstanceId;
    public ConsumableItemDefinitionId ItemDefinitionId =>
        (ConsumableItemDefinitionId)itemDefinitionId;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterConsumableOperationState
{
    public string operationId = string.Empty;
    public string characterId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public string itemStackId = string.Empty;
    public bool meal;
    public bool policyViolation;
    public bool contaminated;
    public float completedAt;

    public ConsumableOperationId OperationId =>
        (ConsumableOperationId)operationId;
    public CharacterId CharacterId => (CharacterId)characterId;
    public ConsumableItemDefinitionId ItemDefinitionId =>
        (ConsumableItemDefinitionId)itemDefinitionId;
    public ItemStackId ItemStackId => (ItemStackId)itemStackId;
}

[Serializable]
public sealed class CharacterMealPlanSaveData
{
    public string planId = string.Empty;
    public string characterId = string.Empty;
    public string facilityInstanceId = string.Empty;
    public string sourceStackId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public CharacterMealPlanPhase phase = CharacterMealPlanPhase.Eating;
    public double createdAt;
    public double leaseExpiresAt;
    public float expectedCompletionEta;
    public bool automaticOperation;
    public float beginContamination;
    public string physicalCommitOperationId = string.Empty;
    public string physicalCommitReasonCode = string.Empty;
    public string physicalCommitId = string.Empty;
    public List<string> physicalCommitSourceStackIds = new();
    public int physicalCommitQuantity;
    public long physicalCommitInputMassGrams;
    public bool committedPolicyViolation;
    public bool committedContaminated;

    public ConsumableOperationId OperationId =>
        (ConsumableOperationId)planId;
    public CharacterId CharacterId => (CharacterId)characterId;
    public BuildingInstanceId FacilityId =>
        (BuildingInstanceId)facilityInstanceId;
    public ItemStackId SourceStackId => (ItemStackId)sourceStackId;
    public ConsumableItemDefinitionId ItemDefinitionId =>
        (ConsumableItemDefinitionId)itemDefinitionId;
}

[Serializable]
public sealed class CharacterSubstanceUsePlanSaveData
{
    public string operationId = string.Empty;
    public string characterId = string.Empty;
    public string itemDefinitionId = string.Empty;
    public string sourceStackId = string.Empty;
    public CharacterSubstanceUsePlanPhase phase =
        CharacterSubstanceUsePlanPhase.ItemCommitted;
    public bool automaticOperation;
    public string physicalCommitOperationId = string.Empty;
    public string physicalCommitReasonCode = string.Empty;
    public string physicalCommitId = string.Empty;
    public List<string> physicalCommitSourceStackIds = new();
    public int physicalCommitQuantity;
    public long physicalCommitInputMassGrams;
    public float resolvedTolerance;
    public float resolvedAddiction;
    public float resolvedWithdrawal;
    public float resolvedActiveSeconds;
    public float resolvedSecondsSinceLastDose;
    public float resolvedScheduledCooldownSeconds;
    public float effectToleranceRatio;
    public bool resolvedAddicted;
    public bool resolvedOverdosed;
    public bool becameAddicted;

    public ConsumableOperationId OperationId =>
        (ConsumableOperationId)operationId;
    public CharacterId CharacterId => (CharacterId)characterId;
    public ConsumableItemDefinitionId ItemDefinitionId =>
        (ConsumableItemDefinitionId)itemDefinitionId;
    public ItemStackId SourceStackId => (ItemStackId)sourceStackId;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonCharacterConsumablesSaveData
{
    public const int CurrentVersion = 8;

    public int version = CurrentVersion;
    public long nextOperationSequence = 1;
    public long nextDeliverySequence = 1;
    public List<CharacterDietPolicyState> dietPolicies = new();
    public List<CharacterSubstancePolicyState> substancePolicies = new();
    public List<CharacterSubstanceState> substanceStates = new();
    public List<CharacterMealDeliveryState> pendingMealDeliveries = new();
    public List<CharacterConsumableOperationState> completedOperations = new();
    public List<CharacterMealFollowupCooldownSaveData> mealFollowupCooldowns = new();
    public List<CharacterMealQualityPolicyState> mealQualityPolicies = new();
    public List<CharacterMealPlanSaveData> activeMealPlans = new();
    public List<CharacterSubstanceUsePlanSaveData> activeSubstanceUsePlans = new();
}

[Serializable]
public sealed class CharacterMealQualityPolicyState
{
    public string characterId = string.Empty;
    public CharacterMealQualityLimit maximumQuality = CharacterMealQualityLimit.Inherit;
    public CharacterId CharacterId => (CharacterId)characterId;
}

[Serializable]
public sealed class CharacterMealFollowupCooldownSaveData
{
    public string characterId = string.Empty;
    public float untilGameSeconds;
    public CharacterId CharacterId => (CharacterId)characterId;
}

public sealed class CharacterConsumablesRestoreCandidate
{
    internal CharacterConsumablesRestoreCandidate(
        CharacterConsumablesAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CharacterConsumablesAggregateState State { get; }
}

public interface ICharacterConsumablesPersistence
{
    void ReconcilePersistentActorReferences(
        IReadOnlyCollection<CharacterId> persistentActorIds);
    DungeonCharacterConsumablesSaveData Capture();
    void ValidateRestorePayload(
        DungeonCharacterConsumablesSaveData saveData,
        bool requireWorldReferences);
    CharacterConsumablesRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterConsumablesSaveData saveData);
    void PublishRestoreCandidate(
        CharacterConsumablesRestoreCandidate candidate);
}

public interface ICharacterConsumablesPersistentActorQuery
{
    IReadOnlyCollection<CharacterId> GetPersistentActorIds();
}
