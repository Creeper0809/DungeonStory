using System;
using System.Collections.Generic;

public sealed class DurableFacilityEquipmentSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonDurableFacilityEquipmentSaveData,
        DurableFacilityEquipmentRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "items.durable-facility-equipment";

    private static readonly string[] Dependencies =
    {
        ModularFacilityWorldSaveSection.Id,
        PhysicalItemsSaveSection.Id
    };

    private readonly IDurableFacilityEquipmentSlotPersistence persistence;
    private readonly DurableFacilityEquipmentRestoreProjection projection;

    public DurableFacilityEquipmentSaveSection(
        IDurableFacilityEquipmentSlotPersistence persistence,
        DurableFacilityEquipmentRestoreProjection projection)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.projection = projection
            ?? throw new ArgumentNullException(nameof(projection));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonDurableFacilityEquipmentSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override void ValidateRawPayload(string payloadJson) =>
        RequireTopLevelArrayFields(payloadJson, "slots");

    protected override DungeonDurableFacilityEquipmentSaveData
        CapturePayload() => persistence.CaptureSaveData();

    protected override DurableFacilityEquipmentRestoreCandidate
        BuildRestoreCandidate(
            DungeonDurableFacilityEquipmentSaveData payload) =>
        projection.Prepare(payload);

    protected override void ValidateParsedPayload(
        DungeonDurableFacilityEquipmentSaveData payload) =>
        projection.ValidateLocal(payload);

    protected override void PublishRestoreCandidate(
        DurableFacilityEquipmentRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
