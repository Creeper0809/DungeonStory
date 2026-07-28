using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterConsumablesSaveSection : IDungeonSaveSection
{
    public const string Id = "survival.character-consumables";

    private static readonly string[] Dependencies =
    {
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        SurvivalResourcesSaveSection.Id
    };
    private readonly ICharacterConsumablesRuntime runtime;

    public CharacterConsumablesSaveSection(
        ICharacterConsumablesRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonCharacterConsumablesSaveData.CurrentVersion;
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
                $"Unsupported character consumables section version "
                + $"{sectionVersion}; expected {SectionVersion}.");
            return;
        }

        runtime.Restore(
            JsonUtility.FromJson<DungeonCharacterConsumablesSaveData>(
                payloadJson)
            ?? new DungeonCharacterConsumablesSaveData());
    }
}
