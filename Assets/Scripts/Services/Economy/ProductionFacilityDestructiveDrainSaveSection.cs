using System;
using System.Collections.Generic;

public sealed class ProductionFacilityDestructiveDrainSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonProductionFacilityDestructiveDrainSaveData,
        ProductionFacilityDestructiveDrainRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id =
        "economy.production-facility-destructive-drains";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        CharacterWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ProductionBillsSaveSection.Id,
        ProductionPreparedOutputRoutingSaveSection.Id,
        CombatEquipmentSaveSection.Id,
        EquipmentMaintenanceSaveSection.Id,
        CharacterEnvironmentSaveSection.Id,
        ProductionGenericBillTerminalDrainSaveSection.Id,
        CombatEquipmentTerminalDrainSaveSection.Id,
        ProductionApparelOrderTerminalDrainSaveSection.Id
    };

    private readonly IProductionFacilityDestructiveDrainPersistence persistence;
    private readonly IProductionOutputLifecycleRestoreCandidatePublisher
        lifecycleRestoreCandidates;

    public ProductionFacilityDestructiveDrainSaveSection(
        IProductionFacilityDestructiveDrainPersistence persistence,
        IProductionOutputLifecycleRestoreCandidatePublisher
            lifecycleRestoreCandidates)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.lifecycleRestoreCandidates = lifecycleRestoreCandidates
            ?? throw new ArgumentNullException(nameof(lifecycleRestoreCandidates));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonProductionFacilityDestructiveDrainSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonProductionFacilityDestructiveDrainSaveData
        CapturePayload() => persistence.Capture();

    protected override ProductionFacilityDestructiveDrainRestoreCandidate
        BuildRestoreCandidate(
            DungeonProductionFacilityDestructiveDrainSaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        ProductionFacilityDestructiveDrainRestoreCandidate candidate) =>
        persistence.Restore(candidate);

    protected override void PublishRestoreCandidateProjection(
        DungeonProductionFacilityDestructiveDrainSaveData payload,
        ProductionFacilityDestructiveDrainRestoreCandidate candidate) =>
        lifecycleRestoreCandidates.SetDrain(payload);

    protected override void ValidateRawPayload(string payloadJson)
    {
        RequireTopLevelArrayFields(payloadJson, "entries");
    }
}
