using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CaptivitySaveSection : IDungeonSaveSection
{
    public const string Id = "captivity";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        CharacterBodyHealthSaveSection.Id
    };

    private readonly ICaptivityRuntime runtime;

    public CaptivitySaveSection(ICaptivityRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => CaptivitySaveData.CurrentVersion;
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
                $"지원하지 않는 포로 저장 버전입니다: {sectionVersion}");
            return;
        }

        List<string> warnings = new List<string>();
        runtime.Restore(
            JsonUtility.FromJson<CaptivitySaveData>(payloadJson)
                ?? new CaptivitySaveData(),
            warnings);
        foreach (string warning in warnings)
        {
            report.AddWarning(warning);
        }
    }
}
