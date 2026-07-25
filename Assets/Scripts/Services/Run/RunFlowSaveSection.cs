using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class RunFlowSaveSection : IDungeonSaveSection
{
    public const string Id = "run.flow";

    private readonly IDungeonRunFlowRuntime runtime;

    public RunFlowSaveSection(IDungeonRunFlowRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        OffenseSaveSection.Id,
        InvasionSaveSection.Id
    };

    public string Capture()
    {
        return JsonUtility.ToJson(new DungeonRunFlowSaveData
        {
            phase = runtime.Phase,
            outcome = runtime.Outcome,
            currentDay = runtime.CurrentDay,
            bossArmed = runtime.IsBossArmed,
            bossActive = runtime.IsBossActive,
            finalInvasionDefended = runtime.IsFinalInvasionDefended,
            bossCycle = runtime.BossCycle
        });
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        DungeonRunFlowSaveData source =
            JsonUtility.FromJson<DungeonRunFlowSaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonRunFlowSaveData();
        runtime.RestoreState(
            source.phase,
            source.outcome,
            Mathf.Max(1, source.currentDay),
            source.bossArmed,
            source.bossActive,
            source.finalInvasionDefended,
            Mathf.Max(0, source.bossCycle));
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
