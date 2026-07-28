using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorldResourceSaveSection : IDungeonSaveSection
{
    public const string Id = "economy.world-resources";

    private static readonly string[] Dependencies =
    {
        WildlifeSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly IWorldResourceRuntime runtime;

    public WorldResourceSaveSection(IWorldResourceRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonWorldResourceSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;

    public string Capture()
    {
        return JsonUtility.ToJson(runtime.Capture());
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            report.AddError(
                $"Unsupported world-resource section version {sectionVersion}; "
                + $"expected {SectionVersion}.");
            return;
        }

        DungeonWorldResourceSaveData snapshot =
            string.IsNullOrWhiteSpace(payloadJson)
                ? new DungeonWorldResourceSaveData()
                : JsonUtility.FromJson<DungeonWorldResourceSaveData>(payloadJson)
                    ?? new DungeonWorldResourceSaveData();
        runtime.Restore(snapshot);
    }
}
