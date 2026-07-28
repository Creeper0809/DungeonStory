using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ResourceStockPolicySaveSection : IDungeonSaveSection
{
    public const string Id = "economy.stock-policies";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ProductionBillsSaveSection.Id
    };

    private readonly IResourceStockPolicyRuntime runtime;

    public ResourceStockPolicySaveSection(
        IResourceStockPolicyRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonResourceStockPolicySaveData.CurrentVersion;
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
                ? new DungeonResourceStockPolicySaveData()
                : JsonUtility.FromJson<DungeonResourceStockPolicySaveData>(
                    payloadJson)
                    ?? new DungeonResourceStockPolicySaveData());
    }
}
