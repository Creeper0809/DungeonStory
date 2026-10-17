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
            VerifyProjectionDoesNotDuplicateLeadingSubject();
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

    private static void VerifyProjectionDoesNotDuplicateLeadingSubject()
    {
        var fixture = new MigratedProducerOutcomeEditorFixture(
            "run:migrated-producer-projection-subject");
        MigratedProducerOutcomeCommitResult duplicateCandidate =
            fixture.Transaction.CommitRequiredSingleSubject(
                MigratedProducerOutcomeKind.HealthThresholdCrossedEvent,
                "projection-subject",
                absoluteDay: 1,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    "character:yeonhwa",
                    "연화",
                    MigratedProducerOutcomeIds.ActorRole),
                "연화의 건강이 임계치를 넘었다.");
        MigratedProducerOutcomeCommitResult placeholderCandidate =
            fixture.Transaction.CommitRequiredSingleSubject(
                MigratedProducerOutcomeKind.CharacterBodyHealthDownedEvent,
                "projection-subject-placeholder",
                absoluteDay: 1,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    "character:yeonhwa",
                    "연화",
                    MigratedProducerOutcomeIds.ActorRole),
                "연화이(가) 전투 불능이 됐다.");
        MigratedProducerOutcomeCommitResult embeddedPlaceholderCandidate =
            fixture.Transaction.CommitRequiredSingleSubject(
                MigratedProducerOutcomeKind.CharacterKilledEvent,
                "projection-embedded-subject-placeholder",
                absoluteDay: 1,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    "character:yeonhwa",
                    "연화",
                    MigratedProducerOutcomeIds.ActorRole),
                "철갑 추격자의 공격으로 연화이(가) 쓰러졌다.");
        MigratedProducerOutcomeCommitResult consonantLeadingPlaceholderCandidate =
            fixture.Transaction.CommitRequiredSingleSubject(
                MigratedProducerOutcomeKind.CharacterBodyHealthRecoveredEvent,
                "projection-consonant-leading-subject-placeholder",
                absoluteDay: 1,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    "character:minseok",
                    "민석",
                    MigratedProducerOutcomeIds.ActorRole),
                "민석이(가) 전투 불능에서 회복됐다.");
        MigratedProducerOutcomeCommitResult consonantEmbeddedPlaceholderCandidate =
            fixture.Transaction.CommitRequiredSingleSubject(
                MigratedProducerOutcomeKind.CharacterDeathEvent,
                "projection-consonant-embedded-subject-placeholder",
                absoluteDay: 1,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    "character:minseok",
                    "민석",
                    MigratedProducerOutcomeIds.ActorRole),
                "철갑 추격자의 공격으로 민석이(가) 쓰러졌다.");
        MigratedProducerOutcomeCommitResult prefixPartialNameCandidate =
            fixture.Transaction.CommitRequiredSingleSubject(
                MigratedProducerOutcomeKind.CombatAttackResult,
                "projection-prefix-partial-name",
                absoluteDay: 1,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    "character:yeonhwa",
                    "연화",
                    MigratedProducerOutcomeIds.ActorRole),
                "연화가루가 흩어졌다.");
        MigratedProducerOutcomeCommitResult suffixPartialNameCandidate =
            fixture.Transaction.CommitRequiredSingleSubject(
                MigratedProducerOutcomeKind.DefenseFacilityStateChangedEvent,
                "projection-suffix-partial-name",
                absoluteDay: 1,
                new MigratedProducerOutcomeSubject(
                    MigratedProducerOutcomeIds.CharacterKind,
                    "character:yeonhwa",
                    "연화",
                    MigratedProducerOutcomeIds.ActorRole),
                "초연화이(가) 쓰러졌다.");
        Require(
            duplicateCandidate.DurablyCommitted
            && fixture.Ledger.TryProject(
                duplicateCandidate.OutcomeId,
                new NarrativePerspectiveContext(
                    default,
                    NarrativePerspectiveKind.Global,
                    "ko-KR"),
                out NarrativeView view)
            && string.Equals(
                view.Text,
                "연화의 건강이 임계치를 넘었다.",
                StringComparison.Ordinal),
            "A summary that already names its subject was prefixed twice.");
        Require(
            placeholderCandidate.DurablyCommitted
            && fixture.Ledger.TryProject(
                placeholderCandidate.OutcomeId,
                new NarrativePerspectiveContext(
                    default,
                    NarrativePerspectiveKind.Global,
                    "ko-KR"),
                out NarrativeView placeholderView)
            && string.Equals(
                placeholderView.Text,
                "연화가 전투 불능이 됐다.",
                StringComparison.Ordinal),
            "A leading Korean subject placeholder was not resolved.");
        Require(
            embeddedPlaceholderCandidate.DurablyCommitted
            && fixture.Ledger.TryProject(
                embeddedPlaceholderCandidate.OutcomeId,
                new NarrativePerspectiveContext(
                    default,
                    NarrativePerspectiveKind.Global,
                    "ko-KR"),
                out NarrativeView embeddedPlaceholderView)
            && string.Equals(
                embeddedPlaceholderView.Text,
                "철갑 추격자의 공격으로 연화가 쓰러졌다.",
                StringComparison.Ordinal),
            "An embedded Korean subject placeholder was not resolved.");
        Require(
            consonantLeadingPlaceholderCandidate.DurablyCommitted
            && fixture.Ledger.TryProject(
                consonantLeadingPlaceholderCandidate.OutcomeId,
                new NarrativePerspectiveContext(
                    default,
                    NarrativePerspectiveKind.Global,
                    "ko-KR"),
                out NarrativeView consonantLeadingPlaceholderView)
            && string.Equals(
                consonantLeadingPlaceholderView.Text,
                "민석이 전투 불능에서 회복됐다.",
                StringComparison.Ordinal),
            "A leading consonant-final Korean subject placeholder was not resolved.");
        Require(
            consonantEmbeddedPlaceholderCandidate.DurablyCommitted
            && fixture.Ledger.TryProject(
                consonantEmbeddedPlaceholderCandidate.OutcomeId,
                new NarrativePerspectiveContext(
                    default,
                    NarrativePerspectiveKind.Global,
                    "ko-KR"),
                out NarrativeView consonantEmbeddedPlaceholderView)
            && string.Equals(
                consonantEmbeddedPlaceholderView.Text,
                "철갑 추격자의 공격으로 민석이 쓰러졌다.",
                StringComparison.Ordinal),
            "An embedded consonant-final Korean subject placeholder was not resolved.");
        Require(
            prefixPartialNameCandidate.DurablyCommitted
            && fixture.Ledger.TryProject(
                prefixPartialNameCandidate.OutcomeId,
                new NarrativePerspectiveContext(
                    default,
                    NarrativePerspectiveKind.Global,
                    "ko-KR"),
                out NarrativeView prefixPartialNameView)
            && string.Equals(
                prefixPartialNameView.Text,
                "연화는 연화가루가 흩어졌다.",
                StringComparison.Ordinal),
            "A name-prefix partial match suppressed the subject frame.");
        Require(
            suffixPartialNameCandidate.DurablyCommitted
            && fixture.Ledger.TryProject(
                suffixPartialNameCandidate.OutcomeId,
                new NarrativePerspectiveContext(
                    default,
                    NarrativePerspectiveKind.Global,
                    "ko-KR"),
                out NarrativeView suffixPartialNameView)
            && string.Equals(
                suffixPartialNameView.Text,
                "연화는 초연화이(가) 쓰러졌다.",
                StringComparison.Ordinal),
            "A name-suffix partial match rewrote another name's placeholder.");
    }

    private static void VerifyEveryCatalogKindCommitsAndQueries()
    {
        Require(
            MigratedProducerOutcomeCatalog.Definitions.Count == 72,
            "The migrated producer catalog no longer contains exactly 72 kinds.");
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
            fixture.Transaction.Capture().owners.Count == 72,
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
