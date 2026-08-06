using System;
using System.Collections.Generic;

public sealed class TreasuryEconomySaveSection :
    DungeonStrictJsonSaveSection<
        TreasuryEconomySaveData,
        TreasuryEconomyRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.treasury";

    private static readonly string[] Dependencies =
    {
        PhysicalItemsSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id
    };

    private readonly ITreasuryEconomyPersistence persistence;

    public TreasuryEconomySaveSection(
        ITreasuryEconomyPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => TreasuryEconomySaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override TreasuryEconomySaveData CapturePayload() =>
        persistence.Capture();

    protected override void NormalizeRestorePayload(
        TreasuryEconomySaveData payload,
        DungeonGameRestoreReport report) =>
        V18WorldEconomyCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override TreasuryEconomyRestoreCandidate BuildRestoreCandidate(
        TreasuryEconomySaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        TreasuryEconomyRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
