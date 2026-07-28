using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ProductionBillsSaveSection : IDungeonSaveSection
{
    public const string Id = "economy.production-bills";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };

    private readonly IProductionBillRuntime runtime;

    public ProductionBillsSaveSection(IProductionBillRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonProductionBillSaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
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
                $"Unsupported production-bill section version {sectionVersion}; "
                + $"expected {SectionVersion}.");
            return;
        }

        DungeonProductionBillSaveData snapshot =
            string.IsNullOrWhiteSpace(payloadJson)
                ? new DungeonProductionBillSaveData()
                : JsonUtility.FromJson<DungeonProductionBillSaveData>(
                    payloadJson) ?? new DungeonProductionBillSaveData();
        runtime.Restore(snapshot);
    }
}
