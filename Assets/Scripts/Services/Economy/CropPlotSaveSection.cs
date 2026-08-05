using System;
using System.Collections.Generic;

public sealed class CropPlotSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCropPlotSaveData,
        CropPlotRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.crop-plots";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly ICropPlotPersistence persistence;

    public CropPlotSaveSection(ICropPlotPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonCropPlotSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonCropPlotSaveData CapturePayload() =>
        persistence.Capture();

    protected override CropPlotRestoreCandidate BuildRestoreCandidate(
        DungeonCropPlotSaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        CropPlotRestoreCandidate candidate) =>
        persistence.Restore(candidate);
}
