using System;
using System.Collections.Generic;

public sealed class DefenseFacilitySaveSection :
    DungeonStrictJsonSaveSection<
        DefenseFacilitySaveData,
        DefenseFacilityRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "defense.facilities";
    private const string PhysicalItemsSectionId = "items.physical";
    private const string PowerInfrastructureSectionId = "infrastructure.power";
    private const string ModularFacilityWorldSectionId = "world.facilities";
    private readonly IDefenseFacilityPersistence persistence;

    public DefenseFacilitySaveSection(IDefenseFacilityPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => DefenseFacilitySaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => new[]
    {
        PhysicalItemsSectionId,
        PowerInfrastructureSectionId,
        ModularFacilityWorldSectionId
    };

    protected override DefenseFacilitySaveData CapturePayload() =>
        persistence.CaptureState();

    protected override DefenseFacilityRestoreCandidate BuildRestoreCandidate(
        DefenseFacilitySaveData payload)
    {
        IReadOnlyList<string> errors = DefenseFacilitySaveRules.Validate(payload);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Defense-facility restore candidate is invalid: "
                + string.Join(" | ", errors));
        }
        return persistence.PrepareRestoreState(payload);
    }

    protected override void PublishRestoreCandidate(
        DefenseFacilityRestoreCandidate candidate) =>
        persistence.PublishRestoreState(candidate);
}
