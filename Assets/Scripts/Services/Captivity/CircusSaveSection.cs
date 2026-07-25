using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CircusSaveSection : IDungeonSaveSection
{
    public const string Id = "circus";
    private static readonly string[] Dependencies =
    {
        CaptivitySaveSection.Id,
        WildlifeSaveSection.Id,
        CharacterWorldSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        CharacterBodyHealthSaveSection.Id
    };
    private readonly ICircusRuntime runtime;

    public CircusSaveSection(ICircusRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => CircusSaveData.CurrentVersion;
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
            report.AddError($"지원하지 않는 서커스 저장 버전입니다: {sectionVersion}");
            return;
        }

        List<string> warnings = new List<string>();
        runtime.Restore(
            JsonUtility.FromJson<CircusSaveData>(payloadJson)
                ?? new CircusSaveData(),
            warnings);
        foreach (string warning in warnings)
        {
            report.AddWarning(warning);
        }
    }
}
