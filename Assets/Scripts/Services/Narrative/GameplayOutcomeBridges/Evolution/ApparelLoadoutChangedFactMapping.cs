using System;
using System.Globalization;

public static class ApparelLoadoutChangedFactMapping
{
    public const string ObserverId = "log:equipment.apparel-loadout-changed";

    public static GameplayResultKey ResultKey(ApparelLoadoutChangedFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            EvolutionOutcomeIds.ApparelProducerId,
            new GameplayOperationId(CreateOperationId(data)),
            data.transitionRevision,
            data.changeIndex);
    }

    public static ApparelChangeOutcomeReceipt Capture(
        ApparelLoadoutChangedFact fact)
    {
        ApparelLoadoutChangedFactData data = fact.Snapshot();
        data.Validate();
        return new ApparelChangeOutcomeReceipt(
            CreateOperationId(data),
            data.transitionRevision,
            data.changeIndex,
            new CharacterId(data.characterId),
            data.characterDisplayName,
            data.apparelId,
            data.equipped,
            (CharacterCommandOrigin)data.origin,
            data.absoluteDay);
    }

    public static GameplayDomainFactObserverBinding<
        ApparelLoadoutChangedFact,
        ApparelChangeOutcomeReceipt> CreateBinding(
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

    private static string CreateOperationId(ApparelLoadoutChangedFactData data) =>
        "apparel-change:" + data.characterId + ":revision:"
        + data.transitionRevision.ToString("D8", CultureInfo.InvariantCulture);
}
