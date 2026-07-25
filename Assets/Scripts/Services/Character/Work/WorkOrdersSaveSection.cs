using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class WorkOrdersSaveSection : IDungeonSaveSection
{
    public const string Id = "work.orders";

    private static readonly string[] Dependencies = { PhysicalItemsSaveSection.Id };
    private readonly IWorkOrderRuntime runtime;

    public WorkOrdersSaveSection(IWorkOrderRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonWorkOrderSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
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
                $"Unsupported work-order section version {sectionVersion}; expected {SectionVersion}.");
            return;
        }

        runtime.Restore(
            JsonUtility.FromJson<DungeonWorkOrderSaveData>(payloadJson)
                ?? new DungeonWorkOrderSaveData(),
            report);
    }
}
