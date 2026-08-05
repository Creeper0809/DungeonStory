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

    protected override InvasionRestoreCandidate BuildRestoreCandidate(
        DungeonInvasionSaveData payload) =>
        saveService.PrepareRestore(payload);

    protected override void PublishRestoreCandidate(
        InvasionRestoreCandidate candidate) =>
        saveService.PublishRestore(candidate);
}
