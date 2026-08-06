using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CircusSaveSection :
    DungeonStrictJsonSaveSection<
        CircusSaveData,
        CircusRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "circus";

    private static readonly string[] Dependencies =
    {
        CaptivitySaveSection.Id,
        "wildlife.population",
        "characters.world",
        "world.facilities",
        "combat.body-health"
    };

    private readonly ICircusPersistence persistence;

    public CircusSaveSection(ICircusPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CircusSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override CircusSaveData CapturePayload() =>
        persistence.Capture();

    protected override void NormalizeRestorePayload(
        CircusSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload?.orders != null)
        {
            for (int orderIndex = 0; orderIndex < payload.orders.Count; orderIndex++)
            {
                CircusShowOrder order = payload.orders[orderIndex];
                NormalizeCharacterIds(
                    order?.performerIds,
                    report,
                    $"orders[{orderIndex}].performerIds");
                NormalizeCharacterIds(
                    order?.audienceIds,
                    report,
                    $"orders[{orderIndex}].audienceIds");
            }
        }

        if (payload?.capturedWildlife == null)
        {
            return;
        }

        for (int index = 0; index < payload.capturedWildlife.Count; index++)
        {
            CapturedWildlifeState animal = payload.capturedWildlife[index];
            if (animal != null)
            {
                animal.reservedCarrierId = NormalizeV18CharacterReference(
                    animal.reservedCarrierId,
                    report,
                    $"capturedWildlife[{index}].reservedCarrierId");
            }
        }
    }

    private void NormalizeCharacterIds(
        IList<string> values,
        DungeonGameRestoreReport report,
        string path)
    {
        if (values == null)
        {
            return;
        }

        for (int index = 0; index < values.Count; index++)
        {
            values[index] = NormalizeV18CharacterReference(
                values[index],
                report,
                $"{path}[{index}]");
        }
    }

    protected override CircusRestoreCandidate BuildRestoreCandidate(
        CircusSaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        CircusRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
