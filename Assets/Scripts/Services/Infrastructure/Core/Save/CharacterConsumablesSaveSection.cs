using System;
using System.Collections.Generic;

/// <summary>
/// V3 character-consumables persistence adapter. The Survival domain owns the
/// payload and candidate; Infrastructure owns the strict save protocol edge.
/// </summary>
public sealed class CharacterConsumablesSaveSection :
    DungeonStrictJsonSaveSection<
        DungeonCharacterConsumablesSaveData,
        CharacterConsumablesRestoreCandidate>,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "survival.character-consumables";

    private static readonly string[] Dependencies =
    {
        "characters.world",
        "items.physical",
        "survival.resources"
    };

    private readonly ICharacterConsumablesPersistence persistence;

    public CharacterConsumablesSaveSection(
        ICharacterConsumablesPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonCharacterConsumablesSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonCharacterConsumablesSaveData CapturePayload() =>
        persistence.Capture();

    protected override CharacterConsumablesRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterConsumablesSaveData payload) =>
        persistence.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        CharacterConsumablesRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
