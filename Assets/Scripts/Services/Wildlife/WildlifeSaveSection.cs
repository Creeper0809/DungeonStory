using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WildlifeSaveSection : IDungeonSaveSection
{
    public const string Id = "wildlife.population";

    private static readonly string[] Dependencies = { PhysicalItemsSaveSection.Id };
    private readonly IWildlifeRuntime runtime;

    public WildlifeSaveSection(IWildlifeRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonWildlifeSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
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
        if (sectionVersion < 2 || sectionVersion > SectionVersion)
        {
            report.AddError(
                $"Unsupported wildlife section version {sectionVersion}; expected 2-{SectionVersion}.");
            return;
        }

        DungeonWildlifeSaveData saveData =
            JsonUtility.FromJson<DungeonWildlifeSaveData>(payloadJson)
            ?? new DungeonWildlifeSaveData();
        if (sectionVersion == 2)
        {
            saveData.version = DungeonWildlifeSaveData.CurrentVersion;
            saveData.foodRaidOrders =
                new List<WildlifeFoodRaidOrderSaveData>();
            report?.AddWarning(
                "Wildlife section V2 migrated to V3 with no active food raid orders.");
        }

        runtime.Restore(saveData, report);
    }
}
