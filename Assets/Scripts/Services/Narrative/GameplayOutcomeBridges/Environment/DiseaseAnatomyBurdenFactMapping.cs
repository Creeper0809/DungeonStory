using System;

public static class DiseaseAnatomyBurdenFactMapping
{
    public static DiseaseAnatomyBurdenOutcomeReceipt Capture(
        DiseaseAnatomyBurdenAppliedFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));
        return new DiseaseAnatomyBurdenOutcomeReceipt(
            fact.OperationId,
            fact.CharacterId,
            fact.CharacterDisplayName,
            fact.DiseaseId,
            fact.TargetSystem,
            fact.NodeId,
            fact.Severity,
            fact.RequestedBurden,
            fact.InfectionBefore,
            fact.InfectionAfter,
            fact.AbsoluteDay);
    }

    public static GameplayDomainFactObserverBinding<
        DiseaseAnatomyBurdenAppliedFact,
        DiseaseAnatomyBurdenOutcomeReceipt> CreateBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commitGroup) => new(
            "log:character.disease-body-burden",
            Capture,
            registry,
            ledger,
            recorder,
            commitGroup);
}
