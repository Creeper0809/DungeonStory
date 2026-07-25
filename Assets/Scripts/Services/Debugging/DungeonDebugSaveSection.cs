using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DungeonDebugSaveSection : IDungeonSaveSection
{
    public const string Id = "debug.run";

    private readonly IDungeonDebugModeService debugModeService;

    public DungeonDebugSaveSection(IDungeonDebugModeService debugModeService)
    {
        this.debugModeService = debugModeService
            ?? throw new ArgumentNullException(nameof(debugModeService));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Presentation;
    public IReadOnlyList<string> DependsOn => new[] { RunFlowSaveSection.Id };
    public string Capture() => JsonUtility.ToJson(debugModeService.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        debugModeService.Restore(
            JsonUtility.FromJson<DungeonDebugRunSaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonDebugRunSaveData());
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
