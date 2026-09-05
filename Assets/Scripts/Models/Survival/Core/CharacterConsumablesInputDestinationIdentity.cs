using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public enum CharacterConsumablesInputKind
{
    Meal = 0,
    RecreationalSubstance = 1
}

public static class CharacterConsumablesInputDestinationIdentity
{
    private const string ExactFacilityInputPrefix = "facility-input:exact:";
    public const string OwnerDomain = "survival.character-consumables";
    public const string CapabilityRemovedReleaseReasonCode =
        "character-consumables-input-capability-removed";
    public const string FacilityLostReleaseReasonCode =
        "character-consumables-input-facility-lost";
    private const string DestinationVersion = "v1";

    public static string Build(
        CharacterConsumablesInputKind kind,
        BuildingInstanceId facilityId,
        ConsumableItemDefinitionId itemId)
    {
        if (!Enum.IsDefined(typeof(CharacterConsumablesInputKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (!facilityId.IsValid)
            throw new ArgumentException(
                "Character-consumables destination requires a facility ID.",
                nameof(facilityId));
        if (!itemId.IsValid)
            throw new ArgumentException(
                "Character-consumables destination requires an item ID.",
                nameof(itemId));
        return ExactFacilityInputPrefix + OwnerDomain + ":"
            + DestinationVersion + ":" + KindSegment(kind) + ":"
            + Uri.EscapeDataString(facilityId.Value) + ":"
            + Uri.EscapeDataString(itemId.Value);
    }

    public static string KindSegment(CharacterConsumablesInputKind kind) =>
        kind switch
        {
            CharacterConsumablesInputKind.Meal => "meal",
            CharacterConsumablesInputKind.RecreationalSubstance =>
                "recreation-substance",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public static bool IsDestinationForKind(
        string destinationId,
        CharacterConsumablesInputKind kind)
    {
        if (string.IsNullOrEmpty(destinationId)
            || !Enum.IsDefined(typeof(CharacterConsumablesInputKind), kind))
        {
            return false;
        }

        string prefix = ExactFacilityInputPrefix + OwnerDomain + ":"
            + DestinationVersion + ":" + KindSegment(kind) + ":";
        return destinationId.StartsWith(prefix, StringComparison.Ordinal);
    }
}

public sealed class CharacterConsumablesInputOwnerDescriptor
{
    public CharacterConsumablesInputOwnerDescriptor(
        CharacterConsumablesInputKind kind,
        string facilityPersistentId,
        Vector2Int position,
        string itemDefinitionId)
    {
        if (!Enum.IsDefined(typeof(CharacterConsumablesInputKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        RequireCanonical(facilityPersistentId, nameof(facilityPersistentId));
        RequireCanonical(itemDefinitionId, nameof(itemDefinitionId));
        Kind = kind;
        FacilityPersistentId = facilityPersistentId;
        Position = position;
        ItemDefinitionId = itemDefinitionId;
        DestinationId = CharacterConsumablesInputDestinationIdentity.Build(
            kind,
            new BuildingInstanceId(facilityPersistentId),
            new ConsumableItemDefinitionId(itemDefinitionId));
    }

    public CharacterConsumablesInputKind Kind { get; }
    public string FacilityPersistentId { get; }
    public Vector2Int Position { get; }
    public string ItemDefinitionId { get; }
    public string DestinationId { get; }

    private static void RequireCanonical(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Character-consumables input ownership requires canonical IDs.",
                name);
        }
    }
}

public interface ICharacterConsumablesInputOwnerDescriptorSource
{
    IReadOnlyList<CharacterConsumablesInputOwnerDescriptor>
        BuildLiveInputOwnerDescriptors();

    IReadOnlyList<CharacterConsumablesInputOwnerDescriptor>
        BuildRestoreInputOwnerDescriptors();
}

public interface ICharacterConsumablesInputOwnerRuntime
{
    bool TryReconcileLive(
        IReadOnlyList<CharacterConsumablesInputOwnerDescriptor> descriptors,
        string reasonCode,
        out string failureReason);

    bool TryReplaceForRestore(
        IReadOnlyList<CharacterConsumablesInputOwnerDescriptor> descriptors,
        out string failureReason);
}
