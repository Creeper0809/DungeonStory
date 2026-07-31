using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DefenseFacilitySaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "defense.facilities";

    private readonly IDefenseFacilityRuntime runtime;

    public DefenseFacilitySaveSection(IDefenseFacilityRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DefenseFacilitySaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        PhysicalItemsSaveSection.Id,
        PowerInfrastructureSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };

    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            report?.AddError(
                $"Unsupported {Id} section version {sectionVersion}.");
            return;
        }

        runtime.Restore(
            JsonUtility.FromJson<DefenseFacilitySaveData>(
                payloadJson ?? string.Empty)
            ?? new DefenseFacilitySaveData());
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Restore(new DefenseFacilitySaveData());
        report?.AddWarning(
            "Defense facility state was absent; existing facilities start Safe, condition 100, with configured initial supply.");
    }
}
