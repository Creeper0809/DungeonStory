using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CropPlotSaveSection : IDungeonSaveSection
{
    public const string Id = "economy.crop-plots";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly ICropPlotRuntime runtime;

    public CropPlotSaveSection(ICropPlotRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonCropPlotSaveData.CurrentVersion;
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
                $"Unsupported crop-plot section version {sectionVersion}; "
                + $"expected {SectionVersion}.");
            return;
        }

        DungeonCropPlotSaveData snapshot =
            string.IsNullOrWhiteSpace(payloadJson)
                ? new DungeonCropPlotSaveData()
                : JsonUtility.FromJson<DungeonCropPlotSaveData>(payloadJson)
                    ?? new DungeonCropPlotSaveData();
        runtime.Restore(snapshot);
    }
}
