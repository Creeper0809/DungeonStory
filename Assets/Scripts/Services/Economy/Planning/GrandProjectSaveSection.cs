using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class GrandProjectSaveSection : IDungeonSaveSection
{
    public const string Id = "economy.grand-projects";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        ProductionBillsSaveSection.Id
    };

    private readonly IGrandProjectRuntime runtime;

    public GrandProjectSaveSection(IGrandProjectRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonGrandProjectSaveData.CurrentVersion;
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
                $"{SectionId}: 지원하지 않는 섹션 버전 {sectionVersion}입니다.");
            return;
        }

        runtime.Restore(
            string.IsNullOrWhiteSpace(payloadJson)
                ? new DungeonGrandProjectSaveData()
                : JsonUtility.FromJson<DungeonGrandProjectSaveData>(
                    payloadJson)
                    ?? new DungeonGrandProjectSaveData());
    }
}
