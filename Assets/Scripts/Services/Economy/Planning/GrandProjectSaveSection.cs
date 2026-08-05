using System;
using System.Collections.Generic;

// V18 required section: validation succeeds before the candidate Aggregate root is replaced.
public sealed class GrandProjectSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonGrandProjectSaveData,
        GrandProjectRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.grand-projects";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        ProductionBillsSaveSection.Id
    };

    private readonly IGrandProjectRuntime runtime;

    public GrandProjectSaveSection(IGrandProjectRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonGrandProjectSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonGrandProjectSaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override GrandProjectRestoreCandidate BuildRestoreCandidate(
        DungeonGrandProjectSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        GrandProjectSaveValidation.Validate(
            payload,
            runtime.Definitions,
            report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Grand-project restore candidate is invalid: "
                + string.Join(" | ", report.Errors));
        }
        return runtime.BuildRestore(payload);
    }

    protected override void PublishRestoreCandidate(
        GrandProjectRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}
