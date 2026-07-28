using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class AnimalHusbandrySaveSection : IDungeonSaveSection
{
    public const string Id = "economy.animal-husbandry";

    private static readonly string[] Dependencies =
    {
        WildlifeSaveSection.Id,
        CircusSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };

    private readonly IAnimalHusbandryRuntime runtime;

    public AnimalHusbandrySaveSection(IAnimalHusbandryRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonAnimalHusbandrySaveData.CurrentVersion;
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
                $"지원하지 않는 축산 저장 버전입니다: {sectionVersion}");
            return;
        }

        runtime.Restore(
            JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(payloadJson)
            ?? new DungeonAnimalHusbandrySaveData());
    }
}
