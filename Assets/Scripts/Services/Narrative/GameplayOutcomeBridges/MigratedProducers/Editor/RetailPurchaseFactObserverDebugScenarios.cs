#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;

internal static class RetailPurchaseFactObserverDebugScenarios
{
    internal static void RunOrThrow()
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.BuildingRetailPurchaseCommitResult;
        var outcomes = new MigratedProducerOutcomeEditorFixture(
            "qa-retail-purchase-fact-observer");
        var probe = new MigratedProducerOutcomeProbeTransaction(
            outcomes.Transaction,
            outcomes.Recorder)
        {
            TargetKind = Kind
        };
        RetailPurchaseRecoverySaveData source = CreateRaw("first");
        source.Validate();
        var fact = new RetailPurchaseFact(source);

        source.buyerId = "character:mutated-source";
        source.lot.itemDefinitionId = "item:mutated-source";
        source.lot.components[0].values[0].stringValue = "mutated-source";
        RetailPurchaseFactData detached = fact.Snapshot();
        detached.facilityId = "facility:mutated-snapshot";
        detached.lot.sourceStackId = "stack:mutated-snapshot";
        detached.lot.components[0].values[0].stringValue =
            "mutated-snapshot";

        var fixture = new RetailPurchaseFactEditorFixture(
            outcomes,
            probe);
        IDomainFactPublisher disposedPublisher = fixture.Publisher;
        try
        {
            RequireDuplicateSubscriptionRejected(
                fixture,
                outcomes,
                probe);
            Require(
                fixture.Publisher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string reserveFailure),
                "Retail purchase fact reservation failed: "
                + reserveFailure);

            RetailPurchaseFactData frozen = fact.Snapshot();
            string ownerIdentity =
                "retail-purchase:" + frozen.operationId
                + ":buyer=" + frozen.buyerId;
            GameplayResultKey expectedKey =
                MigratedProducerOutcomeCatalog.ResultKey(
                    Kind,
                    ownerIdentity,
                    1L);
            Require(
                GameplayDomainFactDeliveryQuery.TryReadKey(
                    lease.PublicationKey,
                    out GameplayResultKey reservedKey)
                && reservedKey == expectedKey,
                "Retail purchase reservation changed its native result key.");

            MigratedProducerOutcomeDetails mapped =
                RetailPurchaseFactMapping.Capture(fact)
                    .ResolveDetails(expectedKey);
            string expectedSummary =
                "시설 소매 구매 확정: item:retail-observer; quantity=1; "
                + "cost=25; buyerMoney=100->75";
            Require(
                mapped.Subjects.Length == 3
                && mapped.Metrics.Length == 6
                && mapped.Metrics.Select(value => value.Value)
                    .SequenceEqual(new double[]
                    {
                        1d,
                        250d,
                        25d,
                        100d,
                        75d,
                        -25d
                    })
                && mapped.Facts.Length == 13
                && mapped.SummaryFirst
                && mapped.Summary == expectedSummary,
                "Retail purchase mapping changed its 3/6/13 plus summary shape.");
            Require(
                mapped.Subjects[0].EntityId.Value
                    == "character:retail-observer"
                && mapped.Subjects[1].EntityId.Value
                    == "facility:retail-observer"
                && mapped.Subjects[2].EntityId.Value
                    == frozen.operationId
                && mapped.Facts.Any(value =>
                    value.FactId.Value == "retail-item-definition-id"
                    && value.Value == "item:retail-observer")
                && mapped.Facts.Any(value =>
                    value.FactId.Value == "retail-source-stack-id"
                    && value.Value == "stack:retail-observer")
                && mapped.Facts.Any(value =>
                    value.FactId.Value
                        == "retail-component-fingerprint"
                    && value.Value == new string('a', 64)),
                "Mutable source or snapshot data changed the frozen retail emission.");

            DomainFactDelivery delivery = fixture.Publisher.Publish(
                lease,
                fact);
            Require(
                delivery.Completed
                && delivery.OperationKey == expectedKey.ToString()
                && delivery.PublicationId.Length > 0
                && delivery.Fingerprint.Length == 64,
                "Retail purchase delivery lost its native key, ID, or hash.");
            Require(
                outcomes.Ledger.TryGetResultIdentity(
                    expectedKey,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.ResultKey == expectedKey
                && identity.OutcomeId.IsValid
                && identity.OutcomeId.ToString() == delivery.PublicationId
                && identity.CanonicalPayloadHash == delivery.Fingerprint
                && identity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && outcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact.immutablePayloadHash == delivery.Fingerprint
                && exact.subjects.Count == 3
                && exact.metrics.Count == 6
                && exact.facts.Count == 15
                && exact.facts[1].factId
                    == MigratedProducerOutcomeIds.SummaryFact.Value
                && exact.facts[1].value == expectedSummary,
                "Retail purchase canonical ledger payload changed from its native delivery.");
            Require(
                probe.ReservationCount(Kind) == 1
                && probe.CommitAttemptCount(Kind) == 1
                && probe.CommitCount(Kind) == 1
                && probe.LatestCommittedKey(Kind) == expectedKey,
                "Retail purchase observer did not reserve and commit exactly once.");
        }
        finally
        {
            fixture.Dispose();
        }

        bool disposedRejected = false;
        try
        {
            disposedRejected = !disposedPublisher.TryReserve(
                fact,
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
            "A disposed retail purchase fact fixture still reserved facts.");

        var reconnectProbe = new MigratedProducerOutcomeProbeTransaction(
            outcomes.Transaction,
            outcomes.Recorder)
        {
            TargetKind = Kind
        };
        using var reconnected = new RetailPurchaseFactEditorFixture(
            outcomes,
            reconnectProbe);
        var reconnectFact = new RetailPurchaseFact(CreateRaw("reconnected"));
        Require(
            reconnected.Publisher.TryReserve(
                reconnectFact,
                out DomainFactLease reconnectLease,
                out string reconnectFailure),
            "Reconnected retail observer reservation failed: "
            + reconnectFailure);
        DomainFactDelivery reconnectDelivery =
            reconnected.Publisher.Publish(reconnectLease, reconnectFact);
        Require(
            reconnectDelivery.Completed
            && reconnectProbe.ReservationCount(Kind) == 1
            && reconnectProbe.CommitCount(Kind) == 1,
            "A fresh retail purchase observer binding did not reconnect once.");
    }

    private static void RequireDuplicateSubscriptionRejected(
        RetailPurchaseFactEditorFixture fixture,
        MigratedProducerOutcomeEditorFixture outcomes,
        MigratedProducerOutcomeProbeTransaction probe)
    {
        IDisposable duplicate = null;
        bool rejected = false;
        try
        {
            duplicate = fixture.Dispatcher.Subscribe(
                "fixture:retail.purchase.fact.duplicate",
                new MigratedProducerDomainFactProbeObserver<
                    RetailPurchaseFact>(
                    RetailPurchaseFactMapping.Capture,
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
            "A duplicate retail purchase fact observer was accepted.");
    }

    private static RetailPurchaseRecoverySaveData CreateRaw(string suffix)
    {
        string sourceOperation = "retail-stock:observer:" + suffix;
        return new RetailPurchaseRecoverySaveData
        {
            shopUseOperationId = "shop-use:observer:" + suffix,
            shopUseCartIndex = 0,
            operationId = "retail-unit:" + sourceOperation + ":ordinal:1",
            buyerId = "character:retail-observer",
            facilityId = "facility:retail-observer",
            absoluteDay = 9,
            purchaseCost = 25,
            buyerMoneyBefore = 100,
            buyerMoneyAfter = 75,
            revenueWorkerId = "character:retail-worker",
            revenuePriceMultiplier = 1.1f,
            revenueSkillModifierProvenance = "skill:retail-premium",
            phase = RetailPurchaseRecoveryPhase.PhysicalCommitted,
            lot = new RetailStockLotSnapshot
            {
                saleItemId = 17,
                itemDefinitionId = "item:retail-observer",
                itemInstanceId = "item-instance:retail-observer",
                sourceStackId = "stack:retail-observer",
                quantity = 1,
                unitMassGrams = 250L,
                sourceOperationId = sourceOperation,
                componentFingerprint = new string('a', 64),
                components = new List<RetailStockComponentSnapshot>
                {
                    new()
                    {
                        componentTypeId = "component:retail-observer",
                        values = new List<RetailStockComponentValueSnapshot>
                        {
                            new()
                            {
                                key = "quality",
                                kind = 0,
                                stringValue = "fine"
                            }
                        }
                    }
                }
            }
        };
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }
}
#endif
