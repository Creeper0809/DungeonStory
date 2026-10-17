using System;
using System.Globalization;

public static class CharacterTraitReactionFactMapping
{
    public const string ObserverId = "log:character.trait-reaction";

    public static GameplayResultKey ResultKey(
        CharacterTraitReactionFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            EvolutionOutcomeIds.TraitReactionProducerId,
            new GameplayOperationId(CreateOperationId(data)),
            data.afterRevision,
            0);
    }

    public static AcquiredTraitReactionOutcomeReceipt Capture(
        CharacterTraitReactionFact fact)
    {
        CharacterTraitReactionFactData data = fact.Snapshot();
        data.Validate();
        return new AcquiredTraitReactionOutcomeReceipt(
            CreateOperationId(data),
            data.afterRevision,
            new CharacterId(data.characterId),
            data.characterDisplayName,
            data.traitInstanceId,
            data.reactionId,
            data.action,
            data.appliedValue,
            data.absoluteDay);
    }

    public static GameplayDomainFactObserverBinding<
        CharacterTraitReactionFact,
        AcquiredTraitReactionOutcomeReceipt> CreateBinding(
            IGameplayOutcomeRegistry registry,
            GameplayOutcomeLedger ledger,
            IGameplayOutcomeRecorder recorder,
            GameplayDomainFactCommitGroup commits) => new(
                ObserverId,
                Capture,
                registry,
                ledger,
                recorder,
                commits);

    private static string CreateOperationId(
        CharacterTraitReactionFactData data) =>
        "trait-reaction:" + data.characterId + ":revision:"
        + data.afterRevision.ToString("D8", CultureInfo.InvariantCulture);
}
