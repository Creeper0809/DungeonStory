#if UNITY_EDITOR
using System;
using System.Linq;

public static class MigratedProducerOutcomeDebugScenarios
{
    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyEveryCatalogKindCommitsAndQueries();
            VerifyOwnerRevisionSaveRoundTripAndMalformedRejection();
            VerifyE2EFaultProbeAndDurableDeliveryRetry();
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    private static void VerifyEveryCatalogKindCommitsAndQueries()
    {
        Require(
            MigratedProducerOutcomeCatalog.Definitions.Count == 57,
            "The migrated producer catalog no longer contains exactly 57 kinds.");
        var fixture = new MigratedProducerOutcomeEditorFixture(
            "run:migrated-producer-all-kinds");

        foreach (MigratedProducerOutcomeDefinition definition in
                 MigratedProducerOutcomeCatalog.Definitions)
        {
            string identity = "all-kinds:" + (int)definition.Kind;
            MigratedProducerOutcomeCommitResult committed =
                fixture.Transaction.CommitRequiredSingleSubject(
                    definition.Kind,
                    identity,
                    absoluteDay: 17,
                    new MigratedProducerOutcomeSubject(
                        MigratedProducerOutcomeIds.OperationKind,
                        identity,
                        definition.KoreanLabel,
                        MigratedProducerOutcomeIds.OperationRole),
                    "kind=" + definition.Kind + "; source="
                        + definition.SourceTypeName);
            Require(
                fixture.Ledger.TryGetResultIdentity(
                    committed.ResultKey,
                    out GameplayOutcomeReplayIdentity replay)
                && replay.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && fixture.Ledger.TryGetExact(
                    replay.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && string.Equals(
                    exact.outcomeTypeId,
                    definition.OutcomeTypeId.Value,
                    StringComparison.Ordinal)
                && exact.absoluteDay == 17
                && exact.status == GameplayOutcomeStatus.Succeeded
                && exact.ownerRevision == 1L
                && exact.participants.Count == 1
                && exact.subjects.Count == 1,
                "Migrated producer outcome did not round-trip through the exact ledger: "
                + definition.Kind);
        }

        Require(
            fixture.Transaction.Capture().owners.Count == 57,
            "The all-kind commit did not persist one owner revision per catalog kind.");
    }

    private static void VerifyOwnerRevisionSaveRoundTripAndMalformedRejection()
    {
        var source = new MigratedProducerOutcomeEditorFixture(
            "run:migrated-producer-save-source");
        MigratedProducerOutcomeDefinition definition =
            MigratedProducerOutcomeCatalog.Definitions[0];
        _ = source.Transaction.CommitRequiredSingleSubject(
            definition.Kind,
            "save-round-trip",
            absoluteDay: 3,
            new MigratedProducerOutcomeSubject(
                MigratedProducerOutcomeIds.OperationKind,
                "save-round-trip",
                definition.KoreanLabel,
                MigratedProducerOutcomeIds.OperationRole),
            "save round trip");

        DungeonMigratedProducerOutcomeSaveData save =
            source.Transaction.Capture();
        var restored = new MigratedProducerOutcomeEditorFixture(
            "run:migrated-producer-save-target");
        restored.Transaction.Restore(
            restored.Transaction.PrepareRestore(save));
        DungeonMigratedProducerOutcomeSaveData roundTrip =
            restored.Transaction.Capture();
        Require(
            roundTrip.owners.Count == 1
            && roundTrip.owners[0].revision == 1L
            && string.Equals(
                roundTrip.owners[0].ownerKey,
                save.owners[0].ownerKey,
                StringComparison.Ordinal),
            "Migrated producer owner revision save round trip drifted.");

        DungeonMigratedProducerOutcomeSaveData malformed =
            source.Transaction.Capture();
        malformed.owners.Add(malformed.owners[0].Clone());
        bool rejected = false;
        try
        {
            _ = restored.Transaction.PrepareRestore(malformed);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected, "Duplicate migrated producer owner keys were accepted.");
    }

    private static void VerifyE2EFaultProbeAndDurableDeliveryRetry()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.ServiceModeChangeResult;
        var deliveryFault = new MigratedProducerOutcomeDeliveryFault
        {
            TargetKind = Kind,
            Enabled = true
        };
        var fixture = new MigratedProducerOutcomeEditorFixture(
            "run:migrated-producer-e2e-support",
            deliveryFault: deliveryFault);
        var probe = new MigratedProducerOutcomeProbeTransaction(
            fixture.Transaction,
            fixture.Recorder)
        {
            TargetKind = Kind,
            FaultMode = MigratedProducerOutcomeFaultMode.RejectReservation
        };
        bool reserved = probe.TryReserveSingleSubject(
            Kind,
            "e2e-support-reservation",
            1,
            GameplayOutcomeStatus.Succeeded,
            out _,
            out _);
        Require(!reserved, "The E2E reservation fault was not injected.");

        probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
        Require(
            probe.TryReserveSingleSubject(
                Kind,
                "e2e-support-commit",
                1,
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome rejected,
                out _),
            "The E2E commit-rejection reservation failed unexpectedly.");
        MigratedProducerOutcomeCommitResult rejectedResult =
            probe.CommitSingleSubject(
                rejected,
                Subject("e2e-support-commit"),
                "commit rejection");
        Require(
            !rejectedResult.DurablyCommitted
            && fixture.Transaction.Capture().owners.Count == 0,
            "The E2E commit fault did not reject before owner advancement.");

        probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
        Require(
            probe.TryReserveSingleSubject(
                Kind,
                "e2e-support-delivery",
                1,
                GameplayOutcomeStatus.Succeeded,
                out PreparedMigratedProducerOutcome pending,
                out _),
            "The E2E delivery-fault reservation failed unexpectedly.");
        MigratedProducerOutcomeCommitResult pendingResult =
            probe.CommitSingleSubject(
                pending,
                Subject("e2e-support-delivery"),
                "delivery retry");
        Require(
            pendingResult.DurablyCommitted,
            "Delivery failure was incorrectly reported as a definite rejection.");
        MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
            fixture,
            Kind,
            probe,
            deliveryFault);
    }

    private static MigratedProducerOutcomeSubject Subject(string identity) =>
        new(
            MigratedProducerOutcomeIds.OperationKind,
            identity,
            identity,
            MigratedProducerOutcomeIds.OperationRole);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
