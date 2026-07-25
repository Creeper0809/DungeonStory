using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ExteriorActivitySaveSection : IDungeonSaveSection
{
    public const string Id = "exterior.activities";

    private readonly IExteriorActivityRuntime runtime;

    public ExteriorActivitySaveSection(IExteriorActivityRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        PhysicalItemsSaveSection.Id,
        WorkOrdersSaveSection.Id
    };

    public string Capture()
    {
        return JsonUtility.ToJson(runtime.Capture());
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        runtime.Restore(
            JsonUtility.FromJson<DungeonExteriorActivitySaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonExteriorActivitySaveData(),
            report);
    }

    private void ValidateVersion(int version)
    {
        if (version != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {version}.");
        }
    }
}
