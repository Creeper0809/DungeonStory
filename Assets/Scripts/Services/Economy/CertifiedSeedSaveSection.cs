using System;
using System.Collections.Generic;

public sealed class CertifiedSeedSaveSection :
    DungeonStrictJsonSaveSection<
        CertifiedSeedWorldSaveData,
        CertifiedSeedRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.certified-seeds";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly ICertifiedSeedPersistence persistence;

    public CertifiedSeedSaveSection(ICertifiedSeedPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CertifiedSeedWorldSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.RuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override CertifiedSeedWorldSaveData CapturePayload() =>
        persistence.Capture();

    protected override CertifiedSeedRestoreCandidate BuildRestoreCandidate(
        CertifiedSeedWorldSaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        CertifiedSeedRestoreCandidate candidate) =>
        persistence.Restore(candidate);
}
