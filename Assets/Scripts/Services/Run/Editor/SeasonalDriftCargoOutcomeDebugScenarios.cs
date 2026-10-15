#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class SeasonalDriftCargoOutcomeDebugScenarios
{
    private const string EventId = "seasonal:spring-drift-cargo";
    private const string ItemId = "material:lumber";
    private const int StartDay = 120;
    private const long UnitMassGrams = 1500L;

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds
        { get; } = new[]
        {
            MigratedProducerOutcomeKind.SeasonalDriftCargoResult
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifySavedRetry(
                MigratedProducerOutcomeFaultMode.RejectReservation,
                partialExpiration: false);
            VerifySavedRetry(
                MigratedProducerOutcomeFaultMode.RejectCommit,
                partialExpiration: true);
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static void RunBatch()
    {
        if (!RunAll(false))
            throw new InvalidOperationException(
                "Seasonal drift-cargo terminal outcome verification failed.");
        Debug.Log("SEASONAL_DRIFT_CARGO_OUTCOME=PASS");
    }

    private static void VerifySavedRetry(
        MigratedProducerOutcomeFaultMode fault,
        bool partialExpiration)
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.SeasonalDriftCargoResult;
        var catalog = new V20StoryContentCatalog(
            new ResourceGameContentCatalog(
                new UnityGameContentRootLoader()));
        var outcomes = new MigratedProducerOutcomeEditorFixture(
            "run:seasonal-drift-cargo-" + fault.ToString().ToLowerInvariant());
        var probe = new MigratedProducerOutcomeProbeTransaction(
            outcomes.Transaction,
            outcomes.Recorder)
        {
            TargetKind = Kind,
            FaultMode = fault
        };
        using DomainFactDispatcher terminalFacts =
            outcomes.CreateSeasonalTerminalFacts(probe);
        V20CampaignRuntime campaign = StartOnly(
            catalog,
            outcomes.AggregateRootStore,
            StartDay,
            110123 + (int)fault,
            terminalFacts);
        V20ActiveEventSaveData occurrence = campaign.ActiveSeasonalEvents.Single();
        string stackId = "stack:seasonal-drift:" + (int)fault;
        Require(campaign.TryRecordSeasonalDriftCargoPlan(
                occurrence.instanceId,
                51,
                9,
                out string failure)
            && campaign.TryMarkSeasonalDriftCargoSpawned(
                occurrence.instanceId,
                new[] { stackId },
                out failure),
            failure);

        if (partialExpiration)
        {
            SeasonalEventWorldSaveData partial = campaign.CaptureSeasonal();
            SeasonalDriftCargoSaveData cargo = partial.activeEvents.Single()
                .seasonalDriftCargo;
            cargo.securedQuantity = 2;
            cargo.expirationCompleted = true;
            cargo.dispositionOperationId =
                SeasonalDriftCargoRules.ExpirationOperationId(
                    occurrence.instanceId);
            cargo.dispositionCommitId = "physical-drift-expiration:commit:1";
            cargo.dispositionSourceStackIds = new List<string> { stackId };
            cargo.expiredQuantity = 4;
            cargo.expiredMassGrams = 4L * UnitMassGrams;
            campaign.PublishSeasonal(campaign.PrepareSeasonal(partial));
        }
        else
        {
            Require(campaign.TryRecordSeasonalDriftCargoSecured(
                    occurrence.instanceId,
                    occurrence.seasonalDriftCargo.exactQuantity,
                    out failure)
                && campaign.TryExpireSeasonalDriftCargo(
                    occurrence.instanceId,
                    Array.Empty<PhysicalItemTransformInput>(),
                    out failure),
                failure);
        }

        occurrence = campaign.ActiveSeasonalEvents.Single();
        var item = new DungeonItemDefinition(
            ItemId,
            "목재",
            "표류한 목재",
            StockCategory.General,
            1,
            null,
            UnitMassGrams / 1000f,
            75);
        string frozen = CargoIdentity(occurrence);
        int observationDay = occurrence.deadlineAbsoluteDay + 1;
        Require(!campaign.TryObserveSeasonalDriftCargo(
                occurrence.instanceId,
                item.DisplayName,
                UnitMassGrams,
                observationDay,
                out string publishFailure)
            && !string.IsNullOrWhiteSpace(publishFailure)
            && string.IsNullOrEmpty(occurrence.seasonalDriftCargo.outcomeResultKey)
            && string.Equals(frozen, CargoIdentity(occurrence),
                StringComparison.Ordinal),
            "Injected drift-cargo outcome failure changed or hid the physical terminal receipt.");

        SeasonalEventWorldSaveData saved = campaign.CaptureSeasonal();
        var restored = new V20CampaignRuntime(
            new DungeonRuntimeAggregateRootStore(),
            catalog,
            null,
            null,
            null,
            terminalFacts,
            terminalFacts);
        restored.PublishSeasonalRestore(restored.PrepareSeasonalRestore(saved));
        int expiredDay = occurrence.deadlineAbsoluteDay + 1;
        _ = restored.EvaluateDaily(new V20DailyEventContext
        {
            AbsoluteDay = expiredDay,
            RunSeed = 111123 + (int)fault,
            Season = Season.Spring
        });
        Require(restored.ActiveSeasonalEvents.Count == 1
                && string.Equals(
                    frozen,
                    CargoIdentity(restored.ActiveSeasonalEvents.Single()),
                    StringComparison.Ordinal),
            "Expired drift cargo was deleted before its terminal parent outcome became durable.");

        probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
        string before = "cargo=" + frozen + "|outcome=pending";
        occurrence = restored.ActiveSeasonalEvents.Single();
        Require(restored.TryObserveSeasonalDriftCargo(
                occurrence.instanceId,
                item.DisplayName + " 변경",
                UnitMassGrams + 1L,
                expiredDay + 1,
                out failure),
            failure);
        string resultKey = occurrence.seasonalDriftCargo.outcomeResultKey;
        GameplayResultKey expectedResultKey =
            MigratedProducerOutcomeCatalog.ResultKey(
                Kind,
                "seasonal-drift-cargo:occurrence="
                + occurrence.instanceId,
                1L);
        Require(resultKey == expectedResultKey.ToString()
                && outcomes.Ledger.TryGetResultIdentity(
                    expectedResultKey,
                    out GameplayOutcomeReplayIdentity expectedIdentity)
                && expectedIdentity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && expectedIdentity.HasCanonicalPayloadHash
                && string.Equals(frozen, CargoIdentity(occurrence),
                    StringComparison.Ordinal),
            "Drift-cargo outcome retry did not retain its exact physical terminal receipt.");
        GameplayOutcomeReplayIdentity identity =
            MigratedProducerOutcomeE2EAssertions.RequireExact(
                outcomes,
                Kind,
                probe,
                before,
                "cargo=" + frozen + "|outcome=" + resultKey);
        Require(outcomes.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact)
            && exact.absoluteDay == observationDay
            && exact.participants.Any(value =>
                value.entityKindId == MigratedProducerOutcomeIds.ItemKind.Value
                && value.entityId == stackId)
            && exact.participants.All(value => string.Equals(
                value.displayText,
                item.DisplayName,
                StringComparison.Ordinal))
            && Metric(exact, "quantity") == 6d
            && Metric(exact, MigratedProducerOutcomeIds.SecuredCountMetric.Value)
                == occurrence.seasonalDriftCargo.securedQuantity
            && Metric(exact, MigratedProducerOutcomeIds.ExpiredCountMetric.Value)
                == occurrence.seasonalDriftCargo.expiredQuantity
            && Metric(exact, "mass")
                == occurrence.seasonalDriftCargo.expiredMassGrams,
            "Drift-cargo parent outcome omitted exact lot or settlement metrics.");

        int reservations = probe.ReservationCount(Kind);
        int commits = probe.CommitCount(Kind);
        Require(restored.TryObserveSeasonalDriftCargo(
                occurrence.instanceId,
                item.DisplayName + " 두 번째 변경",
                UnitMassGrams + 2L,
                expiredDay + 2,
                out failure)
            && probe.ReservationCount(Kind) == reservations
            && probe.CommitCount(Kind) == commits,
            "Terminal drift-cargo replay created a duplicate result.");

        _ = restored.EvaluateDaily(new V20DailyEventContext
        {
            AbsoluteDay = expiredDay + 1,
            RunSeed = 112123 + (int)fault,
            Season = Season.Spring
        });
        Require(restored.ActiveSeasonalEvents.Count == 0,
            "Drift-cargo occurrence did not resolve after its terminal parent became durable.");
    }

    private static V20CampaignRuntime StartOnly(
        V20StoryContentCatalog catalog,
        DungeonRuntimeAggregateRootStore store,
        int absoluteDay,
        int runSeed,
        DomainFactDispatcher terminalFacts)
    {
        var campaign = new V20CampaignRuntime(
            store,
            catalog,
            null,
            null,
            null,
            terminalFacts,
            terminalFacts);
        SeasonalEventWorldSaveData state = campaign.CaptureSeasonal();
        state.cycle = absoluteDay / GameCalendarRules.DaysPerYear;
        state.completedEventIds.AddRange(catalog.SeasonalEvents
            .Where(value => !string.Equals(
                value.StableId,
                EventId,
                StringComparison.Ordinal))
            .Select(value => value.StableId));
        campaign.PublishSeasonal(campaign.PrepareSeasonal(state));
        IReadOnlyList<V20ResolvedEventResult> results = campaign.EvaluateDaily(
            new V20DailyEventContext
            {
                AbsoluteDay = absoluteDay,
                RunSeed = runSeed,
                Season = Season.Spring
            });
        Require(results.Count(value =>
                value.DefinitionId == EventId
                && value.ResolutionId == "started") == 1,
            "The isolated seasonal drift-cargo occurrence did not start exactly once.");
        return campaign;
    }

    private static string CargoIdentity(V20ActiveEventSaveData occurrence)
    {
        SeasonalDriftCargoSaveData cargo = occurrence.seasonalDriftCargo;
        return string.Join(",", cargo.sourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal))
            + "|secured=" + cargo.securedQuantity
            + "|expired=" + cargo.expiredQuantity
            + "|mass=" + cargo.expiredMassGrams
            + "|physical=" + (cargo.dispositionCommitId.Length == 0
                ? "none"
                : cargo.dispositionCommitId);
    }

    private static double Metric(GameplayOutcomeSnapshot exact, string id) =>
        exact.metrics.Single(value => value.metricId == id).value;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
