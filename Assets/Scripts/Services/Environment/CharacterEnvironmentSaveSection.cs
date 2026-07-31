using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterEnvironmentSaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "environment.exposure";

    private static readonly string[] Dependencies =
    {
        CharacterWorldSaveSection.Id,
        EnvironmentalFieldSaveSection.Id
    };

    private readonly ICharacterEnvironmentRuntime runtime;

    public CharacterEnvironmentSaveSection(
        ICharacterEnvironmentRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonCharacterEnvironmentSaveData.CurrentVersion;
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
        if (sectionVersion < 1 || sectionVersion > SectionVersion)
        {
            report.AddError(
                $"Unsupported character environment section version {sectionVersion}; expected 1-{SectionVersion}.");
            return;
        }

        DungeonCharacterEnvironmentSaveData saveData =
            JsonUtility.FromJson<DungeonCharacterEnvironmentSaveData>(
                payloadJson)
            ?? new DungeonCharacterEnvironmentSaveData();
        if (sectionVersion == 1)
        {
            saveData.version =
                DungeonCharacterEnvironmentSaveData.CurrentVersion;
            foreach (CharacterEnvironmentExposure exposure in
                     saveData.exposures
                     ?? new List<CharacterEnvironmentExposure>())
            {
                if (exposure != null)
                {
                    exposure.coldWorkCooldownActive =
                        exposure.coldExposure >= 15f;
                }
            }

            report?.AddWarning(
                "Character environment section V1 migrated to V2 with cold-work cooldown latches derived from current exposure.");
        }

        runtime.Restore(saveData, report);
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Reset();
        report?.AddWarning(
            "Character environment section was absent; exposure and workwear were reset.");
    }
}
