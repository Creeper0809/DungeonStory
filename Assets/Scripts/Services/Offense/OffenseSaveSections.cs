using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class ExpeditionEquipmentSaveSection : IDungeonSaveSection
{
    public const string Id = "offense.expedition-equipment";

    private readonly IExpeditionEquipmentRuntime runtime;

    public ExpeditionEquipmentSaveSection(IExpeditionEquipmentRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
    public IReadOnlyList<string> DependsOn => new[] { PhysicalItemsSaveSection.Id };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        runtime.Restore(
            JsonUtility.FromJson<ExpeditionEquipmentSaveData>(
                payloadJson ?? string.Empty)
            ?? new ExpeditionEquipmentSaveData());
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

public sealed class OffenseSaveSection : IDungeonSaveSection
{
    public const string Id = "offense.expeditions";

    private readonly IOffenseSaveService saveService;

    public OffenseSaveSection(IOffenseSaveService saveService)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        PhysicalItemsSaveSection.Id,
        ExpeditionEquipmentSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        CharacterBodyHealthSaveSection.Id
    };

    public string Capture() => JsonUtility.ToJson(saveService.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        saveService.Restore(
            JsonUtility.FromJson<DungeonOffenseSaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonOffenseSaveData(),
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
