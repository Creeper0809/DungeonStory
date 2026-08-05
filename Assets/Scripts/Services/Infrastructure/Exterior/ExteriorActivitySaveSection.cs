using System;
using System.Collections.Generic;

public sealed class ExteriorActivitySaveSection :
    DungeonStrictJsonSaveSection<
        DungeonExteriorActivitySaveData,
        ExteriorActivityWorldRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "exterior.activities";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        WorkOrdersSaveSection.Id,
        CharacterWorldSaveSection.Id,
        WildlifeSaveSection.Id
    };

    private readonly IExteriorActivityRuntime runtime;

    public ExteriorActivitySaveSection(IExteriorActivityRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonExteriorActivitySaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonExteriorActivitySaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override void ValidateParsedPayload(
        DungeonExteriorActivitySaveData payload) =>
        runtime.ValidateRestorePayload(payload);

    protected override ExteriorActivityWorldRestoreCandidate
        BuildRestoreCandidate(DungeonExteriorActivitySaveData payload) =>
        runtime.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        ExteriorActivityWorldRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}
