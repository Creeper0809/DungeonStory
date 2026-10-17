using System;

public static class CharacterAmbitionTransitionFactMapping
{
    public const string ObserverId = "log:character.ambition-transition";

    public static GameplayResultKey ResultKey(
        CharacterAmbitionTransitionFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            CharacterAmbitionTransitionOutcomeIds.ProducerId,
            new GameplayOperationId(CreateOperationId(data)),
            1L,
            0);
    }

    public static CharacterAmbitionTransitionOutcomeReceipt Capture(
        CharacterAmbitionTransitionFact fact)
    {
        CharacterAmbitionTransitionFactData data = fact.Snapshot();
        data.Validate();
        return new CharacterAmbitionTransitionOutcomeReceipt(
            CreateOperationId(data),
            data.characterId,
            data.characterDisplayName,
            data.ambitionId,
            data.ambitionDisplayName,
            data.definitionRevision,
            data.beforeStatus,
            data.afterStatus,
            data.transitionDay,
            data.startDayBefore,
            data.startDayAfter,
            data.dueDayBefore,
            data.dueDayAfter,
            data.progressBefore,
            data.progressAfter,
            data.nextAllowedDayBefore,
            data.nextAllowedDayAfter,
            data.reasonCode);
    }

    public static GameplayDomainFactObserverBinding<
        CharacterAmbitionTransitionFact,
        CharacterAmbitionTransitionOutcomeReceipt> CreateBinding(
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
        CharacterAmbitionTransitionFactData data) =>
        CharacterAmbitionTransitionOutcomeIds.CreateOperationId(
            data.characterId,
            data.ambitionId,
            data.transitionDay,
            data.afterStatus);
}
