using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SurvivalResourcesSaveSection : IDungeonSaveSection
{
    public const string Id = "survival.resources";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        WildlifeSaveSection.Id
    };
    private readonly ISurvivalFoodRuntime runtime;

    public SurvivalResourcesSaveSection(ISurvivalFoodRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonSurvivalSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
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
                $"Unsupported survival section version {sectionVersion}; expected {SectionVersion}.");
            return;
        }

        runtime.Restore(JsonUtility.FromJson<DungeonSurvivalSaveData>(payloadJson)
            ?? new DungeonSurvivalSaveData());
    }
}

public sealed class DarkSurvivalSaveSection : IDungeonSaveSection
{
    public const string Id = "survival.deprivation";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        SurvivalResourcesSaveSection.Id
    };
    private readonly ICharacterDeprivationRuntime runtime;

    public DarkSurvivalSaveSection(ICharacterDeprivationRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonDarkSurvivalSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
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
                $"Unsupported deprivation section version {sectionVersion}; expected {SectionVersion}.");
            return;
        }

        runtime.Restore(JsonUtility.FromJson<DungeonDarkSurvivalSaveData>(payloadJson)
            ?? new DungeonDarkSurvivalSaveData());
    }
}
