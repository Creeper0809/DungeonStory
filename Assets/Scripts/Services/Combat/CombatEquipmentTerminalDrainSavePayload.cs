using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class DungeonCombatEquipmentTerminalDrainSaveData
{
    public const int CurrentVersion = 2;

    // Missing current-format version must remain zero and fail loudly.
    public int version;
    public List<CombatEquipmentTerminalDrainSaveData> entries = new();

    public DungeonCombatEquipmentTerminalDrainSaveData Clone() => new()
    {
        version = version,
        entries = (entries
                ?? new List<CombatEquipmentTerminalDrainSaveData>())
            .Select(value => value?.Clone())
            .ToList()
    };
}

public sealed class CombatEquipmentTerminalDrainRestoreCandidate
{
    public CombatEquipmentTerminalDrainRestoreCandidate(
        DungeonCombatEquipmentTerminalDrainSaveData payload)
    {
        Payload = (payload ?? throw new ArgumentNullException(nameof(payload)))
            .Clone();
    }

    public DungeonCombatEquipmentTerminalDrainSaveData Payload { get; }
}

public sealed class CombatEquipmentTerminalDrainSaveValidation
{
    public void ValidateOwnPayload(
        DungeonCombatEquipmentTerminalDrainSaveData payload)
    {
        if (payload == null
            || payload.version !=
                DungeonCombatEquipmentTerminalDrainSaveData.CurrentVersion
            || payload.entries == null
            || payload.entries.Count > 4096
            || payload.entries.Any(value =>
                !CombatEquipmentTerminalDrainCanonical.IsValidSave(value)))
        {
            throw new InvalidOperationException(
                "Combat equipment terminal-drain payload is not current format.");
        }

        RequireUnique(
            payload.entries.Select(value => value.stepOperationId),
            "step operation");
        RequireUnique(
            payload.entries.Select(value => value.source.ownerStableId),
            "owner");
        RequireUnique(
            payload.entries.Select(value => value.source.sourceId),
            "source");
    }

    private static void RequireUnique(
        IEnumerable<string> values,
        string kind)
    {
        string[] ordered = (values ?? throw new ArgumentNullException(
                nameof(values)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            if (!ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                    ordered[index])
                || index > 0 && string.Equals(
                    ordered[index - 1],
                    ordered[index],
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Combat equipment terminal-drain payload has an invalid or duplicate "
                    + kind + ".");
            }
        }
    }
}
