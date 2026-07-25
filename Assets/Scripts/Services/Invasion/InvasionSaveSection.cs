using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class InvasionSaveSection : IDungeonSaveSection
{
    public const string Id = "invasion.state";

    private readonly IInvasionSaveService saveService;

    public InvasionSaveSection(IInvasionSaveService saveService)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        CharacterBodyHealthSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        DefenseTacticalSaveSection.Id
    };

    public string Capture() => JsonUtility.ToJson(saveService.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        saveService.Restore(
            JsonUtility.FromJson<DungeonInvasionSaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonInvasionSaveData(),
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
