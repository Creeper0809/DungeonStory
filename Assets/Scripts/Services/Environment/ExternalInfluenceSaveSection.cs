using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ExternalInfluenceSaveSection :
    IDungeonSaveSection,
    IOptionalDungeonSaveSection
{
    public const string Id = "external.influence";
    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        WildlifeSaveSection.Id
    };

    private readonly IExternalInfluenceRuntime runtime;

    public ExternalInfluenceSaveSection(
        IExternalInfluenceRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion =>
        DungeonExternalInfluenceSaveData.CurrentVersion;
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
                $"Unsupported external influence section version {sectionVersion}; expected 1-{SectionVersion}.");
            return;
        }

        DungeonExternalInfluenceSaveData saveData =
            JsonUtility.FromJson<DungeonExternalInfluenceSaveData>(
                payloadJson)
            ?? new DungeonExternalInfluenceSaveData();
        if (sectionVersion == 1)
        {
            saveData.version = DungeonExternalInfluenceSaveData.CurrentVersion;
            saveData.ecologyRaidScheduled = false;
            saveData.ecologyRaidInProgress = false;
            saveData.ecologyRaidRemainingSeconds = 0f;
            saveData.currentOperatingDay = -1;
            saveData.lastRumorMitigationDay = -1;
            report?.AddWarning(
                "External influence section V1 migrated to V2 with no pending ecology raid or rumor mitigation use.");
        }

        runtime.Restore(saveData, report);
    }

    public void RestoreMissing(DungeonGameRestoreReport report)
    {
        runtime.Reset();
        report?.AddWarning(
            "External influence section was absent; influence values were reset.");
    }
}
