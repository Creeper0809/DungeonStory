using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivitySaveSection :
    DungeonStrictJsonSaveSection<
        CaptivitySaveData,
        CaptivityRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "captivity";

    private static readonly string[] Dependencies =
    {
        "world.facilities",
        "characters.world",
        "items.physical",
        "combat.equipment",
        "combat.body-health"
    };

    private readonly ICaptivityPersistence persistence;

    public CaptivitySaveSection(ICaptivityPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion => CaptivitySaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override CaptivitySaveData CapturePayload() =>
        persistence.Capture();

    protected override CaptivityRestoreCandidate BuildRestoreCandidate(
        CaptivitySaveData payload) =>
        persistence.BuildRestore(payload);

    protected override void PublishRestoreCandidate(
        CaptivityRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
