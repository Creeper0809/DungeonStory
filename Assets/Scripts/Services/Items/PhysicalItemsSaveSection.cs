using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class PhysicalItemsSaveSection : IDungeonSaveSection
{
    public const string Id = "items.physical";

    private readonly IWorldItemStackRuntime runtime;

    public PhysicalItemsSaveSection(IWorldItemStackRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonPhysicalItemSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Items;
    public IReadOnlyList<string> DependsOn => new[]
    {
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id
    };

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
                $"Unsupported physical item section version {sectionVersion}; expected {SectionVersion}.");
            return;
        }

        runtime.Restore(JsonUtility.FromJson<DungeonPhysicalItemSaveData>(payloadJson)
            ?? new DungeonPhysicalItemSaveData());
    }
}
