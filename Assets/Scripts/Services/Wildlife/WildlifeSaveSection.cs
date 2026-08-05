using System;
using System.Collections.Generic;

public sealed class WildlifeSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonWildlifeSaveData,
        WildlifeRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "wildlife.population";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly IWildlifeRuntime runtime;

    public WildlifeSaveSection(IWildlifeRuntime runtime)
    {
        this.runtime = runtime
            ?? throw new ArgumentNullException(nameof(runtime));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonWildlifeSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonWildlifeSaveData CapturePayload()
    {
        return runtime.Capture();
    }

    protected override void ValidateParsedPayload(
        DungeonWildlifeSaveData payload) =>
        runtime.ValidateRestorePayload(payload);

    protected override WildlifeRestoreCandidate BuildRestoreCandidate(
        DungeonWildlifeSaveData payload) =>
        runtime.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        WildlifeRestoreCandidate candidate) =>
        runtime.PublishRestoreCandidate(candidate);
}
