using System;
using System.Globalization;

public static class CharacterSkillAcquiredFactMapping
{
    public const string ObserverId = "log:character.skill-acquisition";

    public static GameplayResultKey ResultKey(
        CharacterSkillAcquiredFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            CharacterSkillAcquiredOutcomeIds.ProducerId,
            new GameplayOperationId(CreateOperationId(data)),
            data.afterRevision,
            0);
    }

    public static CharacterSkillAcquiredOutcomeReceipt Capture(
        CharacterSkillAcquiredFact fact)
    {
        CharacterSkillAcquiredFactData data = fact.Snapshot();
        data.Validate();
        return new CharacterSkillAcquiredOutcomeReceipt(
            CreateOperationId(data),
            data.characterId,
            data.characterDisplayName,
            data.beforeRevision,
            data.afterRevision,
            data.unlockLevel,
            data.skill,
            data.absoluteDay);
    }

    public static GameplayDomainFactObserverBinding<
        CharacterSkillAcquiredFact,
        CharacterSkillAcquiredOutcomeReceipt> CreateBinding(
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

    private static string CreateOperationId(CharacterSkillAcquiredFactData data) =>
        "character-skill:" + data.characterId + ":"
        + data.skill.kind.ToString().ToLowerInvariant() + ":"
        + data.unlockLevel.ToString(CultureInfo.InvariantCulture) + ":"
        + data.afterRevision.ToString(CultureInfo.InvariantCulture);
}
