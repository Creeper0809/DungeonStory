using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class DungeonProductionGenericBillTerminalDrainSaveData
{
    public const int CurrentVersion = 1;

    // Deliberately has no current-version initializer. JsonUtility must leave a
    // missing required version at zero so semantic validation fails loudly.
    public int version;
    public List<ProductionGenericBillTerminalDrainSaveData> entries = new();

    public DungeonProductionGenericBillTerminalDrainSaveData Clone() => new()
    {
        version = version,
        entries = (entries ?? new List<
                ProductionGenericBillTerminalDrainSaveData>())
            .Select(value => value?.Clone())
            .ToList()
    };
}

/// <summary>
/// Detached current-format candidate. It is not aggregate authority and may
/// only be published after the eight lifecycle sources and the Items-owned
/// child projection have been cross-validated during the staged commit.
/// </summary>
public sealed class ProductionGenericBillTerminalDrainRestoreCandidate
{
    public ProductionGenericBillTerminalDrainRestoreCandidate(
        DungeonProductionGenericBillTerminalDrainSaveData payload)
    {
        Payload = (payload ?? throw new ArgumentNullException(nameof(payload)))
            .Clone();
    }

    public DungeonProductionGenericBillTerminalDrainSaveData Payload { get; }
}
