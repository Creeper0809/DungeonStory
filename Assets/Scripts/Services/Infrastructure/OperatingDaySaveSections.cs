using System;
using System.Collections.Generic;
using DungeonStory.Operation;
using UnityEngine;

namespace DungeonStory.Infrastructure
{
public sealed class OperatingDaySettlementSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonOperatingDaySettlementSaveData,
        OperatingDaySettlementRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "operation.settlement";

    private readonly IOperatingDaySettlementSaveService saveService;

    public OperatingDaySettlementSaveSection(
        IOperatingDaySettlementSaveService saveService)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
    }

    public override string SectionId => Id;
    public override int SectionVersion => 1;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;

    protected override DungeonOperatingDaySettlementSaveData CapturePayload()
    {
        return saveService.Capture();
    }

    protected override OperatingDaySettlementRestoreCandidate
        BuildRestoreCandidate(
            DungeonOperatingDaySettlementSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        OperatingDaySettlementSaveValidation.Validate(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Operating-day settlement restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }

        return saveService.PrepareRestore(payload);
    }

    protected override void PublishRestoreCandidate(
        OperatingDaySettlementRestoreCandidate candidate)
    {
        saveService.PublishRestore(candidate);
    }
}

public sealed class EventAlertSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonEventAlertSaveData,
        EventAlertRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "operation.event-alerts";
    public const int CurrentVersion = 1;

    private readonly IEventAlertSaveService saveService;

    public EventAlertSaveSection(IEventAlertSaveService saveService)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.Presentation;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        OperatingDaySettlementSaveSection.Id,
        InvasionSaveSection.Id,
        OffenseAggregateSaveSection.Id
    };

    protected override DungeonEventAlertSaveData CapturePayload()
    {
        return saveService.Capture();
    }

    protected override EventAlertRestoreCandidate BuildRestoreCandidate(
        DungeonEventAlertSaveData payload)
    {
        IReadOnlyList<string> errors = EventAlertPayloadValidation.Validate(payload);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Event-alert restore candidate is invalid: "
                + string.Join(" | ", errors));
        }
        return saveService.PrepareRestore(payload);
    }

    protected override void PublishRestoreCandidate(
        EventAlertRestoreCandidate candidate) =>
        saveService.PublishRestore(candidate);
}

}
