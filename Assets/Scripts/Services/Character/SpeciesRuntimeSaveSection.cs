using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SpeciesRuntimeSaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "character.species-runtime";

    private readonly ICharacterSpeciesRuntime runtime;

    public SpeciesRuntimeSaveSection(ICharacterSpeciesRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => CharacterSpeciesRuntimeSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn =>
        new[] { CharacterWorldSaveSection.Id };

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
            JsonUtility.FromJson<CharacterSpeciesRuntimeSaveData>(
                payloadJson ?? string.Empty)
            ?? new CharacterSpeciesRuntimeSaveData());
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Restore(new CharacterSpeciesRuntimeSaveData());
        report?.AddWarning(
            "Species runtime section was absent; stable incident state and construct charge start from defaults.");
    }
}
