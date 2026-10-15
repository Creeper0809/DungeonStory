public static class CharacterSkillActivationFactMapping
{
    public static CharacterSkillActivationOutcomeReceipt Capture(
        CharacterSkillActivationFact fact) => new(fact);

    public static GameplayDomainFactObserverBinding<
        CharacterSkillActivationFact,
        CharacterSkillActivationOutcomeReceipt> CreateBinding(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder,
        GameplayDomainFactCommitGroup commits) => new(
            "log:character.skill-activated",
            Capture,
            registry,
            ledger,
            recorder,
            commits);
}
