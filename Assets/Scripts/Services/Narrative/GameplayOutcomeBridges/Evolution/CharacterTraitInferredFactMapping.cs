using System;
using System.Globalization;

public static class CharacterTraitInferredFactMapping
{
    public const string ObserverId = "log:character.trait-inference";

    public static GameplayResultKey ResultKey(
        CharacterTraitInferredFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            EvolutionOutcomeIds.TraitInferenceProducerId,
            new GameplayOperationId(CreateOperationId(data)),
            data.afterRevision,
            0);
    }

    public static AcquiredTraitInferenceOutcomeReceipt Capture(
        CharacterTraitInferredFact fact)
    {
        CharacterTraitInferredFactData data = fact.Snapshot();
        data.Validate();
        NarrativeInferenceTimestamp timestamp =
            NarrativeInferenceTimestamp.FromGameTick(data.gameTick);
        var audit = new CharacterAcquiredTraitInferenceAuditRecord(
            data.auditId,
            CharacterAcquiredTraitInferenceCommandKind.Completion,
            CharacterAcquiredTraitInferenceIssueCode.None,
            data.requestKey,
            data.candidatePacketHash,
            data.selectedCombinationId,
            data.characterId,
            data.beforeRevision,
            data.registeredRevision,
            data.afterRevision,
            timestamp,
            string.Empty);
        var result = new CharacterAcquiredTraitInferenceCommandResult(
            audit,
            packet: null);
        return new AcquiredTraitInferenceOutcomeReceipt(
            CreateOperationId(data),
            result,
            data.trait,
            data.characterDisplayName,
            ResolveAbsoluteDay(data.gameTick));
    }

    public static GameplayDomainFactObserverBinding<
        CharacterTraitInferredFact,
        AcquiredTraitInferenceOutcomeReceipt> CreateBinding(
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
        CharacterTraitInferredFactData data) =>
        "trait-inference:" + data.characterId + ":revision:"
        + data.afterRevision.ToString("D8", CultureInfo.InvariantCulture);

    private static int ResolveAbsoluteDay(long gameTick)
    {
        if (gameTick < 0L)
            throw new ArgumentOutOfRangeException(nameof(gameTick));
        long absoluteDay = checked(
            gameTick / GameCalendarRules.HoursPerDay + 1L);
        return checked((int)absoluteDay);
    }
}
