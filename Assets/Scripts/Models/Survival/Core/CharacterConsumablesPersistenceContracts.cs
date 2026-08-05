using System;
using System.Collections.Generic;
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
    public bool IsValid => PersistentEntityId.IsKind(Value, "consumable-operation");
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
    public bool IsValid => PersistentEntityId.IsKind(Value, "consumable-delivery");
    public bool Equals(ConsumableDeliveryId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is ConsumableDeliveryId other && Equals(other);
    public override int GetHashCode() => PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator ConsumableDeliveryId(string value) => new(value);
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
    public float completedAt;

    public ConsumableOperationId OperationId =>
        (ConsumableOperationId)operationId;
    public CharacterId CharacterId => (CharacterId)characterId;
    public ConsumableItemDefinitionId ItemDefinitionId =>
        (ConsumableItemDefinitionId)itemDefinitionId;
    public ItemStackId ItemStackId => (ItemStackId)itemStackId;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonCharacterConsumablesSaveData
{
    public const int CurrentVersion = 3;

    public int version = CurrentVersion;
    public long nextOperationSequence = 1;
    public long nextDeliverySequence = 1;
    public List<CharacterDietPolicyState> dietPolicies = new();
    public List<CharacterSubstancePolicyState> substancePolicies = new();
    public List<CharacterSubstanceState> substanceStates = new();
    public List<CharacterMealDeliveryState> pendingMealDeliveries = new();
    public List<CharacterConsumableOperationState> completedOperations = new();
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
    DungeonCharacterConsumablesSaveData Capture();
    CharacterConsumablesRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterConsumablesSaveData saveData);
    void PublishRestoreCandidate(
        CharacterConsumablesRestoreCandidate candidate);
}
