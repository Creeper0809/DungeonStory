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
    private readonly ICharacterConsumablesInputOwnerDescriptorSource
        inputOwnerSource;
    private readonly ICharacterConsumablesInputOwnerRuntime inputOwners;
    private readonly ICharacterConsumablesPersistentActorQuery persistentCharacters;

    public CharacterConsumablesSaveSection(
        ICharacterConsumablesPersistence persistence,
        ICharacterConsumablesInputOwnerDescriptorSource inputOwnerSource,
        ICharacterConsumablesInputOwnerRuntime inputOwners,
        ICharacterConsumablesPersistentActorQuery persistentCharacters)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
        this.inputOwnerSource = inputOwnerSource
            ?? throw new ArgumentNullException(nameof(inputOwnerSource));
        this.inputOwners = inputOwners
            ?? throw new ArgumentNullException(nameof(inputOwners));
        this.persistentCharacters = persistentCharacters
            ?? throw new ArgumentNullException(nameof(persistentCharacters));
    }

    public override string SectionId => Id;
    public override int SectionVersion =>
        DungeonCharacterConsumablesSaveData.CurrentVersion;
    public override DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public override IReadOnlyList<string> DependsOn => Dependencies;

    protected override DungeonCharacterConsumablesSaveData CapturePayload()
    {
        persistence.ReconcilePersistentActorReferences(
            persistentCharacters.GetPersistentActorIds()
            ?? Array.Empty<CharacterId>());
        if (!inputOwners.TryReconcileLive(
                inputOwnerSource.BuildLiveInputOwnerDescriptors(),
                CharacterConsumablesInputDestinationIdentity
                    .CapabilityRemovedReleaseReasonCode,
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Character-consumables input owner capture join failed: "
                + failureReason);
        }
        return persistence.Capture();
    }

    protected override void ValidateParsedPayload(
        DungeonCharacterConsumablesSaveData payload) =>
        persistence.ValidateRestorePayload(
            payload,
            requireWorldReferences: false);

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
        NormalizeCharacterIds(payload.activeMealPlans, value => value.characterId,
            (value, id) => value.characterId = id, report, "activeMealPlans");
        NormalizeCharacterIds(payload.activeSubstanceUsePlans, value => value.characterId,
            (value, id) => value.characterId = id, report, "activeSubstanceUsePlans");
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
        DungeonCharacterConsumablesSaveData payload)
    {
        CharacterConsumablesRestoreCandidate candidate =
            persistence.BuildRestoreCandidate(payload);
        if (!inputOwners.TryReplaceForRestore(
                inputOwnerSource.BuildRestoreInputOwnerDescriptors(),
                out string failureReason))
        {
            throw new InvalidOperationException(
                "Character-consumables input owner restore join failed: "
                + failureReason);
        }
        return candidate;
    }

    protected override void PublishRestoreCandidate(
        CharacterConsumablesRestoreCandidate candidate) =>
        persistence.PublishRestoreCandidate(candidate);
}
