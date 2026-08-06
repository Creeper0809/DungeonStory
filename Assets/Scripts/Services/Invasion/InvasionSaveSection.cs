using System;
using System.Collections.Generic;
public sealed class InvasionSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonInvasionSaveData,
        InvasionRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "invasion.state";

    private static readonly string[] Dependencies =
    {
        CharacterWorldSaveSection.Id,
        ModularFacilityWorldSaveSection.Id,
        CharacterBodyHealthSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        DefenseTacticalSaveSection.Id
    };

    private readonly IInvasionSaveService saveService;

    public InvasionSaveSection(IInvasionSaveService saveService)
    {
        this.saveService = saveService
            ?? throw new ArgumentNullException(nameof(saveService));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DungeonInvasionSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonInvasionSaveData CapturePayload()
    {
        return saveService.Capture();
    }

    protected override void NormalizeRestorePayload(
        DungeonInvasionSaveData payload,
        DungeonGameRestoreReport report) =>
        V18CombatOffenseCharacterReferenceRestoreNormalizer.Normalize(
            payload,
            (value, path) => NormalizeV18CharacterReference(value, report, path));

    protected override InvasionRestoreCandidate BuildRestoreCandidate(
        DungeonInvasionSaveData payload) =>
        saveService.PrepareRestore(payload);

    protected override void ValidateParsedPayload(
        DungeonInvasionSaveData payload)
    {
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        saveService.ValidateRestorePayload(payload, report);
        if (!report.Success)
        {
            throw new InvalidOperationException(
                "Invasion restore payload is invalid: "
                + string.Join(" | ", report.Errors));
        }
    }

    protected override void PublishRestoreCandidate(
        InvasionRestoreCandidate candidate) =>
        saveService.PublishRestore(candidate);
}
