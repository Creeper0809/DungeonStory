using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class FactionSaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "world.factions";

    private readonly IFactionRuntime runtime;

    public FactionSaveSection(IFactionRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonFactionSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        OffenseV17SaveSection.Id,
        PhysicalItemsSaveSection.Id
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
            JsonUtility.FromJson<DungeonFactionSaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonFactionSaveData());
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Reset();
        report?.AddWarning(
            "Faction section was absent; six neutral dungeon factions were generated deterministically.");
    }
}
