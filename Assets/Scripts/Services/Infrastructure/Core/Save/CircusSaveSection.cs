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

    protected override CircusRestoreCandidate BuildRestoreCandidate(
        CircusSaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        CircusRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
