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

    protected override void NormalizeRestorePayload(
        DungeonCharacterConsumablesSaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload == null)
        {
            return;
        }

        NormalizeCharacterIds(payload.dietPolicies, value => value.characterId,
            (value, id) => value.characterId = id, report, "dietPolicies");
        NormalizeCharacterIds(payload.substancePolicies, value => value.characterId,
            (value, id) => value.characterId = id, report, "substancePolicies");
        NormalizeCharacterIds(payload.substanceStates, value => value.characterId,
            (value, id) => value.characterId = id, report, "substanceStates");
        NormalizeCharacterIds(payload.pendingMealDeliveries, value => value.characterId,
            (value, id) => value.characterId = id, report, "pendingMealDeliveries");
        NormalizeCharacterIds(payload.completedOperations, value => value.characterId,
            (value, id) => value.characterId = id, report, "completedOperations");
    }

    private void NormalizeCharacterIds<T>(
        IList<T> values,
        Func<T, string> get,
        Action<T, string> set,
        DungeonGameRestoreReport report,
        string path)
        where T : class
    {
        if (values == null)
        {
            return;
        }

        bool changed = false;
        for (int index = 0; index < values.Count; index++)
        {
            T value = values[index];
            if (value != null)
            {
                string previous = get(value);
                string normalized = NormalizeV18CharacterReference(
                    previous,
                    report,
                    $"{path}[{index}].characterId");
                set(
                    value,
                    normalized);
                changed |= !string.Equals(
                    previous,
                    normalized,
                    StringComparison.Ordinal);
            }
        }
        if (changed && values is List<T> list)
        {
            list.Sort((left, right) => string.CompareOrdinal(
                left == null ? null : get(left),
                right == null ? null : get(right)));
        }
    }

    protected override CharacterConsumablesRestoreCandidate BuildRestoreCandidate(
        DungeonCharacterConsumablesSaveData payload) =>
        persistence.BuildRestoreCandidate(payload);

    protected override void PublishRestoreCandidate(
        CharacterConsumablesRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
