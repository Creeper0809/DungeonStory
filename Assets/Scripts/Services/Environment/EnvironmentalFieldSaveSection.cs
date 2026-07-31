using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class EnvironmentalFieldSaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "environment.field";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id
    };

    private readonly IEnvironmentalFieldRuntime runtime;

    public EnvironmentalFieldSaveSection(
        IEnvironmentalFieldRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonEnvironmentalFieldSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
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
                $"Unsupported environment field section version {sectionVersion}; expected {SectionVersion}.");
            return;
        }

        runtime.Restore(
            JsonUtility.FromJson<DungeonEnvironmentalFieldSaveData>(
                payloadJson)
            ?? new DungeonEnvironmentalFieldSaveData(),
            report);
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Reset();
        report?.AddWarning(
            "Environment field section was absent; the field will initialize from current weather.");
    }
}

