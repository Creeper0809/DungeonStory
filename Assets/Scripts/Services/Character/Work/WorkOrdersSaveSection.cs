using System;
using System.Collections.Generic;

public sealed class WorkOrdersSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonWorkOrderSaveData,
        WorkOrderRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "work.orders";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };
    private readonly IWorkOrderRuntime runtime;

    public WorkOrdersSaveSection(IWorkOrderRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonWorkOrderSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonWorkOrderSaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override void ValidateParsedPayload(
        DungeonWorkOrderSaveData payload)
    {
        runtime.ValidateRestorePayload(payload);
    }

    protected override WorkOrderRestoreCandidate BuildRestoreCandidate(
        DungeonWorkOrderSaveData payload)
    {
        return runtime.PrepareRestoreCandidate(payload);
    }

    protected override void PublishRestoreCandidate(
        WorkOrderRestoreCandidate candidate)
    {
        runtime.PublishRestoreCandidate(candidate);
    }
}
