using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class DungeonProductionApparelOrderTerminalDrainSaveData
{
    public const int CurrentVersion = 3;

    // Missing current-format version must remain zero and fail loudly.
    public int version;
    public List<ProductionApparelOrderTerminalDrainSaveData> entries = new();

    public DungeonProductionApparelOrderTerminalDrainSaveData Clone() => new()
    {
        version = version,
        entries = (entries
                ?? new List<ProductionApparelOrderTerminalDrainSaveData>())
            .Select(value => value?.Clone())
            .ToList()
    };
}

public sealed class ProductionApparelOrderTerminalDrainRestoreCandidate
{
    public ProductionApparelOrderTerminalDrainRestoreCandidate(
        DungeonProductionApparelOrderTerminalDrainSaveData payload)
    {
        Payload = (payload ?? throw new ArgumentNullException(nameof(payload)))
            .Clone();
    }

    public DungeonProductionApparelOrderTerminalDrainSaveData Payload { get; }
}

public sealed class ProductionApparelOrderTerminalDrainSaveValidation
{
    public void ValidateOwnPayload(
        DungeonProductionApparelOrderTerminalDrainSaveData payload)
    {
        if (payload == null
            || payload.version !=
                DungeonProductionApparelOrderTerminalDrainSaveData
                    .CurrentVersion
            || payload.entries == null
            || payload.entries.Count > 4096
            || payload.entries.Any(value =>
                !ProductionApparelOrderTerminalDrainCanonical
                    .IsValidSave(value)))
        {
            throw new InvalidOperationException(
                "Apparel order terminal-drain payload is not current format.");
        }

        RequireUnique(
            payload.entries.Select(value => value.stepOperationId),
            "step operation");
        RequireUnique(
            payload.entries.Select(value => value.ownerStableId),
            "owner");
        RequireUnique(
            payload.entries.Select(value => value.orderId),
            "order");
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
                    "Apparel order terminal-drain payload has an invalid or duplicate "
                    + kind + ".");
            }
        }
    }
}
