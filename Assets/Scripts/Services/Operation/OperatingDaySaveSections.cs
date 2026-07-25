using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OperatingDaySettlementSaveSection : IDungeonSaveSection
{
    public const string Id = "operation.settlement";

    private readonly IOperatingDaySettlementSaveService saveService;

    public OperatingDaySettlementSaveSection(
        IOperatingDaySettlementSaveService saveService)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Array.Empty<string>();
    public string Capture() => JsonUtility.ToJson(saveService.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        saveService.Restore(
            JsonUtility.FromJson<DungeonOperatingDaySettlementSaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonOperatingDaySettlementSaveData(),
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

public sealed class EventAlertSaveSection : IDungeonSaveSection
{
    public const string Id = "operation.event-alerts";

    private readonly IEventAlertSaveService saveService;

    public EventAlertSaveSection(IEventAlertSaveService saveService)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.Presentation;
    public IReadOnlyList<string> DependsOn => new[]
    {
        OperatingDaySettlementSaveSection.Id,
        InvasionSaveSection.Id,
        OffenseSaveSection.Id
    };

    public string Capture() => JsonUtility.ToJson(saveService.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        ValidateVersion(sectionVersion);
        saveService.Restore(
            JsonUtility.FromJson<DungeonEventAlertSaveData>(
                payloadJson ?? string.Empty)
            ?? new DungeonEventAlertSaveData(),
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
