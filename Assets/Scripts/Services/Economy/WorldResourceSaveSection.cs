using System;
using System.Collections.Generic;

public sealed class WorldResourceSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonWorldResourceSaveData,
        WorldResourceRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.world-resources";

    private static readonly string[] Dependencies =
    {
        WildlifeSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly IWorldResourcePersistence persistence;

    public WorldResourceSaveSection(IWorldResourcePersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonWorldResourceSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonWorldResourceSaveData CapturePayload() =>
        persistence.Capture();

    protected override WorldResourceRestoreCandidate BuildRestoreCandidate(
        DungeonWorldResourceSaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        WorldResourceRestoreCandidate candidate) =>
        persistence.Restore(candidate);
}
