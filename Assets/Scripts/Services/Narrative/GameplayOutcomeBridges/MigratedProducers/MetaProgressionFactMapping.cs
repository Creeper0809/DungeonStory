using System;

public static class MetaProgressionFactMapping
{
    public static MigratedDomainFactCapture Capture(
        MetaUpgradePurchaseDomainFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        string identity = "meta-upgrade-purchase:" + fact.UpgradeId;
        string displayName = string.IsNullOrWhiteSpace(fact.DisplayName)
            ? fact.UpgradeId
            : fact.DisplayName;
        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.OperationKind,
            identity,
            displayName,
            MigratedProducerOutcomeIds.OperationRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            displayName + " Lv." + fact.PreviousLevel
            + "에서 Lv." + fact.CurrentLevel
            + "로 구매했다. 비용: " + Math.Max(0, fact.Cost) + ".");
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent,
            identity,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }

    public static MigratedDomainFactCapture Capture(MetaRunResultDomainFact fact)
    {
        if (fact == null)
            throw new ArgumentNullException(nameof(fact));

        RunResultSnapshot result = fact.Snapshot();
        string identity = "meta-run-result:sequence:" + fact.RunSequence;
        var subject = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.OperationKind,
            identity,
            string.IsNullOrWhiteSpace(result.ownerName)
                ? "Run " + fact.RunSequence
                : result.ownerName,
            MigratedProducerOutcomeIds.OperationRole);
        var details = new MigratedProducerOutcomeDetails(
            new[] { subject },
            Array.Empty<GameplayOutcomeMetric>(),
            Array.Empty<GameplayOutcomeFact>(),
            "런 결과 확정: sequence=" + fact.RunSequence
            + "; outcome=" + result.outcome
            + "; reason=" + result.endReason
            + "; days=" + result.survivedOperatingDays
            + "; survival-seconds=" + result.survivalSeconds
            + "; legacy-currency=" + result.legacyCurrency
            + "; offense-success=" + result.offenseSuccessCount
            + "; defended-invasions=" + result.defendedInvasionCount);
        return new MigratedDomainFactCapture(
            MigratedProducerOutcomeKind.RunResultReadyEvent,
            identity,
            fact.AbsoluteDay,
            GameplayOutcomeStatus.Succeeded,
            details);
    }
}
