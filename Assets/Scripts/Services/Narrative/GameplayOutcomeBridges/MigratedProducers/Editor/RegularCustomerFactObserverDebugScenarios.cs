#if UNITY_EDITOR
using System;
using DungeonStory.Foundation.Facts;

internal static class RegularCustomerFactObserverDebugScenarios
{
    internal static void RunOrThrow()
    {
        var outcomes = new MigratedProducerOutcomeEditorFixture(
            "qa-regular-customer-fact-observer");
        var probe = new MigratedProducerOutcomeProbeTransaction(
            outcomes.Transaction,
            outcomes.Recorder);

        RegularCustomerFactData visitSource = VisitData(
            "character:regular-observer");
        var visit = new RegularCustomerFact(visitSource);
        visitSource.customerId = "character:mutated-source";
        visitSource.displayName = "Mutated Source";
        visitSource.visitCount = 99;
        RegularCustomerFactData visitSnapshot = visit.Snapshot();
        visitSnapshot.customerId = "character:mutated-snapshot";
        visitSnapshot.displayName = "Mutated Snapshot";
        visitSnapshot.speciesTag = "Mutated Species";
        visitSnapshot.visitCount = 88;

        RequireMapping(
            visit,
            MigratedProducerOutcomeKind.RegularCustomerVisitResult,
            "regular-customer-visit:customer=character:regular-observer:visit=2",
            "character:regular-observer",
            MigratedProducerOutcomeIds.CustomerRole,
            "관찰 손님의 방문 2회차가 기록되었습니다. 상태=RecruitCandidate.");
        Require(
            visit.Snapshot().speciesTag == "Slime"
            && visit.Snapshot().visitCount == 2,
            "Mutable source or snapshot data changed the frozen visit fact.");

        var committed = new RegularCustomerFact(RecruitData(
            RegularCustomerFactStage.RecruitmentCommitted));
        RequireMapping(
            committed,
            MigratedProducerOutcomeKind.RegularCustomerRecruitResult,
            "regular-customer-recruit:customer=character:regular-observer:day=6:kind=Staff",
            "character:regular-observer",
            MigratedProducerOutcomeIds.CustomerRole,
            "관찰 손님 영입이 확정되었습니다. 배치=Staff.");

        var delivered = new RegularCustomerFact(RecruitData(
            RegularCustomerFactStage.RecruitmentDelivered));
        RequireMapping(
            delivered,
            MigratedProducerOutcomeKind.RegularCustomerRecruitDeliveryResult,
            "regular-customer-recruit-delivery:customer=character:regular-observer:day=6:kind=Staff",
            "character:regular-observer",
            MigratedProducerOutcomeIds.ActorRole,
            "customer=character:regular-observer;actor=character:regular-observer;delivery=Staff;recruitedDay=6");

        using (var missing = new DomainFactDispatcher())
        {
            Require(
                !missing.TryReserve(
                    visit,
                    out DomainFactLease missingLease,
                    out string missingFailure)
                && !missingLease.IsValid
                && missingFailure.Contains(
                    "domain-fact-observer-missing",
                    StringComparison.Ordinal),
                "A regular-customer fact reserved without an observer.");
        }

        var fixture = new RegularCustomerFactEditorFixture(outcomes, probe);
        IDomainFactPublisher disposedPublisher = fixture.Publisher;
        try
        {
            RequireDuplicateSubscriptionRejected(fixture, outcomes, probe);
            Publish(fixture.Publisher, outcomes, visit);
            Publish(fixture.Publisher, outcomes, committed);
            Publish(fixture.Publisher, outcomes, delivered);
            Require(
                probe.CommitCount(
                    MigratedProducerOutcomeKind.RegularCustomerVisitResult) == 1
                && probe.CommitCount(
                    MigratedProducerOutcomeKind.RegularCustomerRecruitResult) == 1
                && probe.CommitCount(
                    MigratedProducerOutcomeKind
                        .RegularCustomerRecruitDeliveryResult) == 1,
                "The regular-customer observer did not commit all three mapped stages exactly once.");
        }
        finally
        {
            fixture.Dispose();
        }

        bool disposedRejected = false;
        try
        {
            disposedRejected = !disposedPublisher.TryReserve(
                visit,
                out DomainFactLease disposedLease,
                out _);
            if (disposedLease.IsValid)
            {
                disposedPublisher.Cancel(disposedLease);
            }
        }
        catch (ObjectDisposedException)
        {
            disposedRejected = true;
        }
        Require(
            disposedRejected,
            "A disposed regular-customer observer still reserved a fact.");

        var reconnectProbe = new MigratedProducerOutcomeProbeTransaction(
            outcomes.Transaction,
            outcomes.Recorder)
        {
            TargetKind = MigratedProducerOutcomeKind.RegularCustomerVisitResult
        };
        using var reconnected = new RegularCustomerFactEditorFixture(
            outcomes,
            reconnectProbe);
        var reconnect = new RegularCustomerFact(VisitData(
            "character:regular-reconnected",
            visitCount: 1,
            status: RegularCustomerStatus.Visitor));
        Publish(reconnected.Publisher, outcomes, reconnect);
        Require(
            reconnectProbe.CommitCount(
                MigratedProducerOutcomeKind.RegularCustomerVisitResult) == 1,
            "A fresh regular-customer observer did not reconnect exactly once.");
    }

    private static void RequireMapping(
        RegularCustomerFact fact,
        MigratedProducerOutcomeKind expectedKind,
        string expectedOwner,
        string expectedSubject,
        GameplayRoleId expectedRole,
        string expectedSummary)
    {
        MigratedDomainFactCapture capture =
            RegularCustomerFactMapping.Capture(fact);
        MigratedProducerOutcomeDetails details = capture.Details;
        Require(
            capture.Kind == expectedKind
            && capture.OwnerIdentity == expectedOwner
            && capture.AbsoluteDay == 6
            && capture.Status == GameplayOutcomeStatus.Succeeded
            && !capture.ReservationOnly
            && details.Subjects.Length == 1
            && details.Subjects[0].EntityId.Kind.Equals(
                MigratedProducerOutcomeIds.CharacterKind)
            && details.Subjects[0].EntityId.Value == expectedSubject
            && details.Subjects[0].DisplayName.DisplayText == "관찰 손님"
            && details.Subjects[0].Role.Equals(expectedRole)
            && details.Metrics.Length == 0
            && details.Facts.Length == 0
            && details.Summary == expectedSummary
            && !details.SummaryFirst,
            "Regular-customer fact mapping changed its exact native payload for "
            + expectedKind + ".");
    }

    private static void Publish(
        IDomainFactPublisher publisher,
        MigratedProducerOutcomeEditorFixture outcomes,
        RegularCustomerFact fact)
    {
        Require(
            publisher.TryReserve(
                fact,
                out DomainFactLease lease,
                out string reserveFailure),
            "Regular-customer fact reservation failed: " + reserveFailure);
        string reservedKey = lease.PublicationKey;
        DomainFactDelivery delivery = publisher.Publish(lease, fact);
        Require(
            delivery.Completed
            && delivery.OperationKey == reservedKey
            && delivery.PublicationId.Length > 0
            && delivery.Fingerprint.Length == 64
            && GameplayDomainFactDeliveryQuery.TryReadKey(
                delivery.OperationKey,
                out GameplayResultKey key)
            && outcomes.Ledger.TryGetResultIdentity(
                key,
                out GameplayOutcomeReplayIdentity identity)
            && identity.OutcomeId.ToString() == delivery.PublicationId
            && identity.CanonicalPayloadHash == delivery.Fingerprint,
            "Regular-customer fact delivery lost its canonical native proof.");
    }

    private static void RequireDuplicateSubscriptionRejected(
        RegularCustomerFactEditorFixture fixture,
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        IDisposable duplicate = null;
        bool rejected = false;
        try
        {
            duplicate = fixture.Dispatcher.Subscribe(
                RegularCustomerFactMapping.ObserverId,
                new MigratedProducerDomainFactProbeObserver<RegularCustomerFact>(
                    RegularCustomerFactMapping.Capture,
                    outcomes.Transaction,
                    new GameplayDomainFactCommitGroup(
                        outcomes.Recorder,
                        outcomes.Ledger),
                    probe));
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        finally
        {
            duplicate?.Dispose();
        }
        Require(
            rejected,
            "A duplicate regular-customer fact observer was accepted.");
    }

    private static RegularCustomerFactData VisitData(
        string customerId,
        int visitCount = 2,
        RegularCustomerStatus status = RegularCustomerStatus.RecruitCandidate) =>
        new()
        {
            stage = RegularCustomerFactStage.VisitRecorded,
            customerId = customerId,
            displayName = "관찰 손님",
            speciesTag = "Slime",
            absoluteDay = 6,
            visitCount = visitCount,
            averageSatisfaction = visitCount == 1 ? 45f : 87.5f,
            status = status,
            capabilities = RecruitCapability.All,
            becameRegular = status == RegularCustomerStatus.Regular
                || status == RegularCustomerStatus.RecruitCandidate,
            becameRecruitCandidate = status
                == RegularCustomerStatus.RecruitCandidate
        };

    private static RegularCustomerFactData RecruitData(
        RegularCustomerFactStage stage) => new()
        {
            stage = stage,
            customerId = "character:regular-observer",
            displayName = "관찰 손님",
            speciesTag = "Slime",
            absoluteDay = 6,
            visitCount = 2,
            averageSatisfaction = 87.5f,
            status = RegularCustomerStatus.Recruited,
            capabilities = RecruitCapability.All,
            recruitedAbsoluteDay = 6,
            deliveryKind = RegularCustomerRecruitDeliveryKind.Staff,
            actorId = stage == RegularCustomerFactStage.RecruitmentDelivered
                ? "character:regular-observer"
                : string.Empty
        };

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }
}
#endif
