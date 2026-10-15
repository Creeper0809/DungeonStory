using System;
using System.Globalization;
using System.Linq;

public static class AgeConditionAnatomyFactMapping
{
    public static AgeConditionAnatomyOutcomeReceipt Capture(AgeConditionAnatomyAppliedFact fact)
    {
        var value = fact.Snapshot();
        if (!GameplayDomainFactDeliveryQuery.TryReadKey(value.parentPublicationKey, out var key)
            || key.ProducerId != MigratedProducerOutcomeCatalog.Get(MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent).ProducerId)
            throw new InvalidOperationException("age-anatomy-parent-key-invalid");
        var parent = ReadParent(value.parentPublicationId);
        string detail = string.Join(";", value.nodeDeltas.Select(row => string.Join(",", new[]
        { row.nodeId, row.healthBefore.ToString("0.###", CultureInfo.InvariantCulture),
            row.healthAfter.ToString("0.###", CultureInfo.InvariantCulture), row.requestedDamage.ToString("0.###", CultureInfo.InvariantCulture),
            row.bleedingBefore.ToString("0.###", CultureInfo.InvariantCulture), row.bleedingAfter.ToString("0.###", CultureInfo.InvariantCulture) })));
        return new AgeConditionAnatomyOutcomeReceipt(value.operationId, value.characterId, value.characterDisplayName,
            value.conditionId, (AgeConditionSeverity)value.previousSeverity, (AgeConditionSeverity)value.currentSeverity,
            value.absoluteDay, value.nodeDeltas.Sum(row => (double)row.healthBefore),
            value.nodeDeltas.Sum(row => (double)row.healthAfter),
            value.nodeDeltas.Sum(row => Math.Max(0d, row.healthBefore - row.healthAfter)),
            value.nodeDeltas.Count, detail, parent, key);
    }
    internal static GameplayOutcomeId ReadParent(string value)
    {
        int separator = value?.LastIndexOf('/') ?? -1;
        if (separator <= 0 || !long.TryParse(value.Substring(separator + 1), NumberStyles.None,
                CultureInfo.InvariantCulture, out long sequence))
            throw new InvalidOperationException("age-anatomy-parent-id-invalid");
        var id = new GameplayOutcomeId(new GameplayOutcomeRunId(value.Substring(0, separator)), sequence);
        if (!id.IsValid || id.ToString() != value) throw new InvalidOperationException("age-anatomy-parent-id-noncanonical");
        return id;
    }
    public static GameplayDomainFactObserverBinding<AgeConditionAnatomyAppliedFact, AgeConditionAnatomyOutcomeReceipt>
        CreateBinding(IGameplayOutcomeRegistry registry, GameplayOutcomeLedger ledger, IGameplayOutcomeRecorder recorder,
            GameplayDomainFactCommitGroup commits) => new("log:character.age-condition-anatomy", Capture, registry, ledger, recorder, commits);
}
