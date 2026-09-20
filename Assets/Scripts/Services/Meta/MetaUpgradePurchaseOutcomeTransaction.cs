using System;

public sealed class MetaUpgradePurchaseOutcomeTransaction :
    IMetaUpgradePurchaseOutcomeTransaction,
    IMetaRunResultOutcomeTransaction
{
    private readonly IMigratedProducerOutcomeTransaction outcomes;

    public MetaUpgradePurchaseOutcomeTransaction(
        IMigratedProducerOutcomeTransaction outcomes)
    {
        this.outcomes = outcomes
            ?? throw new ArgumentNullException(nameof(outcomes));
    }

    public bool TryReserve(
        string upgradeId,
        int absoluteDay,
        out IMetaUpgradePurchaseOutcomeReservation reservation,
        out string failureReason)
    {
        reservation = null;
        string canonicalId = upgradeId?.Trim() ?? string.Empty;
        if (canonicalId.Length == 0)
        {
            failureReason = "meta-upgrade-purchase-id-invalid";
            return false;
        }

        string identity = CreateIdentity(canonicalId);
        if (!outcomes.TryReserveSingleSubject(
                MigratedProducerOutcomeKind.MetaUpgradePurchasedEvent,
                identity,
                absoluteDay,
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out failureReason))
        {
            return false;
        }

        reservation = new Reservation(this, canonicalId, identity, prepared);
        return true;
    }

    public MetaUpgradePurchaseOutcomeCommitResult Commit(
        IMetaUpgradePurchaseOutcomeReservation reservation,
        string displayName,
        int previousLevel,
        int currentLevel,
        int cost)
    {
        if (reservation is not Reservation owned || owned.Owner != this)
        {
            return new MetaUpgradePurchaseOutcomeCommitResult(
                false,
                "meta-upgrade-purchase-reservation-invalid");
        }

        MigratedProducerOutcomeCommitResult committed =
            outcomes.CommitSingleSubject(
                owned.Prepared,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.OperationKind,
                    owned.Identity,
                    string.IsNullOrWhiteSpace(displayName)
                        ? owned.UpgradeId
                        : displayName,
                    MigratedProducerOutcomeIds.OperationRole),
                CreateSummary(
                    displayName,
                    owned.UpgradeId,
                    previousLevel,
                    currentLevel,
                    cost));
        return new MetaUpgradePurchaseOutcomeCommitResult(
            committed.DurablyCommitted,
            committed.DetailCode);
    }

    public void Cancel(IMetaUpgradePurchaseOutcomeReservation reservation)
    {
        if (reservation is Reservation owned && owned.Owner == this)
            outcomes.Cancel(owned.Prepared);
    }

    public bool TryReserve(
        int runSequence,
        int absoluteDay,
        out IMetaRunResultOutcomeReservation reservation,
        out string failureReason)
    {
        reservation = null;
        if (runSequence < 1)
        {
            failureReason = "meta-run-result-sequence-invalid";
            return false;
        }

        string identity = "meta-run-result:sequence:" + runSequence;
        if (!outcomes.TryReserveSingleSubject(
                MigratedProducerOutcomeKind.RunResultReadyEvent,
                identity,
                absoluteDay,
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome prepared,
                out failureReason))
        {
            return false;
        }

        reservation = new RunResultReservation(
            this,
            runSequence,
            identity,
            prepared);
        return true;
    }

    public MetaRunResultOutcomeCommitResult Commit(
        IMetaRunResultOutcomeReservation reservation,
        RunResultSnapshot result)
    {
        if (reservation is not RunResultReservation owned
            || owned.Owner != this
            || result == null)
        {
            return new MetaRunResultOutcomeCommitResult(
                false,
                "meta-run-result-reservation-invalid");
        }

        MigratedProducerOutcomeCommitResult committed =
            outcomes.CommitSingleSubject(
                owned.Prepared,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.OperationKind,
                    owned.Identity,
                    string.IsNullOrWhiteSpace(result.ownerName)
                        ? "Run " + owned.RunSequence
                        : result.ownerName,
                    MigratedProducerOutcomeIds.OperationRole),
                "런 결과 확정: sequence=" + owned.RunSequence
                + "; outcome=" + result.outcome
                + "; reason=" + result.endReason
                + "; days=" + result.survivedOperatingDays
                + "; survival-seconds=" + result.survivalSeconds
                + "; legacy-currency=" + result.legacyCurrency
                + "; offense-success=" + result.offenseSuccessCount
                + "; defended-invasions=" + result.defendedInvasionCount);
        return new MetaRunResultOutcomeCommitResult(
            committed.DurablyCommitted,
            committed.DetailCode);
    }

    public void Cancel(IMetaRunResultOutcomeReservation reservation)
    {
        if (reservation is RunResultReservation owned && owned.Owner == this)
            outcomes.Cancel(owned.Prepared);
    }

    private static string CreateIdentity(string upgradeId) =>
        "meta-upgrade-purchase:" + upgradeId;

    private static string CreateSummary(
        string displayName,
        string upgradeId,
        int previousLevel,
        int currentLevel,
        int cost)
    {
        string name = string.IsNullOrWhiteSpace(displayName)
            ? upgradeId
            : displayName.Trim();
        return $"{name} Lv.{previousLevel}에서 Lv.{currentLevel}로 구매했다. 비용: {Math.Max(0, cost)}.";
    }

    private sealed class Reservation :
        IMetaUpgradePurchaseOutcomeReservation
    {
        internal Reservation(
            MetaUpgradePurchaseOutcomeTransaction owner,
            string upgradeId,
            string identity,
            PreparedMigratedProducerOutcome prepared)
        {
            Owner = owner;
            UpgradeId = upgradeId;
            Identity = identity;
            Prepared = prepared;
        }

        internal MetaUpgradePurchaseOutcomeTransaction Owner { get; }
        internal string UpgradeId { get; }
        internal string Identity { get; }
        internal PreparedMigratedProducerOutcome Prepared { get; }
    }

    private sealed class RunResultReservation :
        IMetaRunResultOutcomeReservation
    {
        internal RunResultReservation(
            MetaUpgradePurchaseOutcomeTransaction owner,
            int runSequence,
            string identity,
            PreparedMigratedProducerOutcome prepared)
        {
            Owner = owner;
            RunSequence = runSequence;
            Identity = identity;
            Prepared = prepared;
        }

        internal MetaUpgradePurchaseOutcomeTransaction Owner { get; }
        internal int RunSequence { get; }
        internal string Identity { get; }
        internal PreparedMigratedProducerOutcome Prepared { get; }
    }
}
