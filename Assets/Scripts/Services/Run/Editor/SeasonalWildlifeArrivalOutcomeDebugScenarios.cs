#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class SeasonalWildlifeArrivalOutcomeDebugScenarios
{
    private const string EventId = "seasonal:spring-migrant-herd";
    private const string SpeciesId = "deep_goat";
    private const int StartDay = 100;

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds
        { get; } = new[]
        {
            MigratedProducerOutcomeKind.SeasonalWildlifeArrivalResult
        };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyIncompleteSpawnSuppression(spawnedCount: 0);
            VerifyIncompleteSpawnSuppression(spawnedCount: 1);
            VerifySavedRetry(MigratedProducerOutcomeFaultMode.RejectReservation);
            VerifySavedRetry(MigratedProducerOutcomeFaultMode.RejectCommit);
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
                "Seasonal wildlife arrival terminal outcome verification failed.");
        Debug.Log("SEASONAL_WILDLIFE_ARRIVAL_OUTCOME=PASS");
    }

    public static void RunEec02FromBatch()
    {
        string configured = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_U0_EEC02_WILDLIFE_ARRIVAL_REPORT_PATH");
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "DUNGEONSTORY_U0_EEC02_WILDLIFE_ARRIVAL_REPORT_PATH is required.");
        }

        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Unity project root is unavailable.");
        string fullPath = Path.GetFullPath(Path.Combine(projectRoot, configured));
        string qaRoot = Path.GetFullPath(Path.Combine(projectRoot, "Artifacts/QA"));
        if (!fullPath.StartsWith(
                qaRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            || File.Exists(fullPath))
        {
            throw new InvalidOperationException(
                "EEC02 wildlife-arrival report must be a create-only path under Artifacts/QA.");
        }

        if (!RunAll(throwOnFailure: true))
            throw new InvalidOperationException(
                "EEC02 seasonal wildlife arrival verification failed.");

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Report directory is unavailable."));
        using FileStream report = new(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(report);
        writer.Write(
            "U0_EEC02_SEASONAL_WILDLIFE=PASS\n"
            + "runtimeEntry=V20CampaignRuntime,SeasonalWildlifeArrivalFact,SeasonalTerminalFactMapping,V20ContentResolutionService\n"
            + "spawnBranches=zero-suppressed,partial-suppressed,full-success\n"
            + "identity=exact-wildlife-ids,species-id,occurrence-id\n"
            + "genericJoin=shared-occurrence-id-on-v20-resolved-event\n"
            + "persistence=frozen-plan,json-round-trip,retry-before-expiry\n"
            + "exactlyOnce=arrival-terminal,generic-completed-event,replay-no-duplicate\n"
            + "fullAuditClosure=false\n"
            + "humanApprovalClaimed=false\n"
            + "trainingEligible=false\n");
        Debug.Log("U0_EEC02_SEASONAL_WILDLIFE=PASS; report=" + fullPath);
    }

    private static void VerifyIncompleteSpawnSuppression(int spawnedCount)
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.SeasonalWildlifeArrivalResult;
        var catalog = new V20StoryContentCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        var outcomes = new MigratedProducerOutcomeEditorFixture(
            "run:eec02-incomplete-" + spawnedCount);
        var probe = new MigratedProducerOutcomeProbeTransaction(
            outcomes.Transaction,
            outcomes.Recorder)
        {
            TargetKind = Kind
        };
        using DomainFactDispatcher terminalFacts =
            outcomes.CreateSeasonalTerminalFacts(probe);
        V20CampaignRuntime campaign = StartOnly(
            catalog,
            outcomes.AggregateRootStore,
            StartDay,
            97123 + spawnedCount,
            terminalFacts);
        V20ActiveEventSaveData occurrence = campaign.ActiveSeasonalEvents.Single();
        WildlifeSpeciesDefinition species = catalog.Wildlife.Single(value =>
                string.Equals(value.SpeciesId, SpeciesId, StringComparison.Ordinal))
            .ToDefinition();
        Require(spawnedCount >= 0
                && spawnedCount < occurrence.seasonalWildlifeArrival.exactCount,
            "Incomplete spawn fixture requires a strict subset of the authored count.");
        SeasonalWildlifeArrivalMemberSaveData[] plan = CreatePlan(
            occurrence,
            9600 + spawnedCount * 10);
        Require(campaign.TryRecordSeasonalWildlifeArrivalPlan(
                occurrence.instanceId,
                plan,
                out string failure),
            failure);
        for (int index = 0; index < spawnedCount; index++)
        {
            Require(campaign.TryMarkSeasonalWildlifeArrivalSpawned(
                    occurrence.instanceId,
                    plan[index].wildlifeId,
                    out failure),
                failure);
        }

        string before = PlanIdentity(campaign.ActiveSeasonalEvents.Single());
        Require(!campaign.TryObserveSeasonalWildlifeArrival(
                occurrence.instanceId,
                species.DisplayName,
                StartDay,
                out string publishFailure)
            && !string.IsNullOrWhiteSpace(publishFailure)
            && string.Equals(
                before,
                PlanIdentity(campaign.ActiveSeasonalEvents.Single()),
                StringComparison.Ordinal)
            && string.IsNullOrEmpty(campaign.ActiveSeasonalEvents.Single()
                .seasonalWildlifeArrival.outcomeResultKey)
            && probe.ReservationCount(Kind) == 0
            && probe.CommitCount(Kind) == 0
            && outcomes.Transaction.Capture().owners.Count == 0
            && outcomes.Ledger.CaptureGameplayOutcomes().outbox.Count == 0
            && outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0,
            "An incomplete seasonal spawn created or hid a terminal outcome.");
    }

    private static void VerifySavedRetry(MigratedProducerOutcomeFaultMode fault)
    {
        const MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.SeasonalWildlifeArrivalResult;
        var catalog = new V20StoryContentCatalog(
            new ResourceGameContentCatalog(
                new UnityGameContentRootLoader()));
        var outcomes = new MigratedProducerOutcomeEditorFixture(
            "run:seasonal-wildlife-arrival-" + fault.ToString().ToLowerInvariant());
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
            98123 + (int)fault,
            terminalFacts);
        V20ActiveEventSaveData occurrence = campaign.ActiveSeasonalEvents.Single();
        WildlifeSpeciesDefinition species = catalog.Wildlife.Single(value =>
                string.Equals(value.SpeciesId, SpeciesId, StringComparison.Ordinal))
            .ToDefinition();
        SeasonalWildlifeArrivalMemberSaveData[] plan = CreatePlan(
            occurrence,
            9700 + (int)fault * 10);
        Require(campaign.TryRecordSeasonalWildlifeArrivalPlan(
                occurrence.instanceId,
                plan,
                out string failure),
            failure);
        foreach (SeasonalWildlifeArrivalMemberSaveData member in plan)
        {
            Require(campaign.TryMarkSeasonalWildlifeArrivalSpawned(
                    occurrence.instanceId,
                    member.wildlifeId,
                    out failure),
                failure);
        }

        string frozenPlan = PlanIdentity(campaign.ActiveSeasonalEvents.Single());
        Require(!campaign.TryObserveSeasonalWildlifeArrival(
                occurrence.instanceId,
                species.DisplayName,
                StartDay,
                out string publishFailure)
            && !string.IsNullOrWhiteSpace(publishFailure)
            && string.IsNullOrEmpty(campaign.ActiveSeasonalEvents.Single()
                .seasonalWildlifeArrival.outcomeResultKey)
            && string.Equals(
                frozenPlan,
                PlanIdentity(campaign.ActiveSeasonalEvents.Single()),
                StringComparison.Ordinal),
            "Injected seasonal-arrival outcome failure changed or hid its frozen physical result.");

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
            RunSeed = 99123 + (int)fault,
            Season = Season.Spring
        });
        Require(restored.ActiveSeasonalEvents.Count == 1
                && string.Equals(
                    frozenPlan,
                    PlanIdentity(restored.ActiveSeasonalEvents.Single()),
                    StringComparison.Ordinal),
            "An expired seasonal occurrence was deleted before its spawned members received a durable parent outcome.");

        probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
        string before = "members=" + frozenPlan + "|outcome=pending";
        Require(restored.TryObserveSeasonalWildlifeArrival(
                occurrence.instanceId,
                species.DisplayName + " 변경",
                expiredDay,
                out failure),
            failure);
        V20ActiveEventSaveData committedOccurrence =
            restored.ActiveSeasonalEvents.Single();
        string resultKey = committedOccurrence.seasonalWildlifeArrival
            .outcomeResultKey;
        GameplayResultKey expectedResultKey =
            MigratedProducerOutcomeCatalog.ResultKey(
                Kind,
                "seasonal-wildlife-arrival:occurrence="
                + occurrence.instanceId + ":species=" + SpeciesId,
                1L);
        Require(resultKey == expectedResultKey.ToString()
                && outcomes.Ledger.TryGetResultIdentity(
                    expectedResultKey,
                    out GameplayOutcomeReplayIdentity expectedIdentity)
                && expectedIdentity.State
                    == GameplayOutcomeReplayState.PublishedAcknowledged
                && expectedIdentity.HasCanonicalPayloadHash
                && string.Equals(
                    frozenPlan,
                    PlanIdentity(committedOccurrence),
                    StringComparison.Ordinal),
            "The retried seasonal arrival did not preserve its exact member set and terminal key.");
        GameplayOutcomeReplayIdentity identity =
            MigratedProducerOutcomeE2EAssertions.RequireExact(
                outcomes,
                Kind,
                probe,
                before,
                "members=" + frozenPlan + "|outcome=" + resultKey);
        Require(outcomes.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact)
            && exact.absoluteDay == StartDay
            && exact.participants.Count == plan.Length
            && exact.subjects.Count == plan.Length
            && exact.participants.All(value => string.Equals(
                value.displayText,
                species.DisplayName,
                StringComparison.Ordinal))
            && plan.All(member => exact.participants.Any(value =>
                value.entityKindId == "wildlife"
                && string.Equals(
                    value.entityId,
                    member.wildlifeId,
                    StringComparison.Ordinal)))
            && exact.metrics.Any(value =>
                value.metricId == MigratedProducerOutcomeIds.CountMetric.Value
                && Math.Abs(value.value - plan.Length) < 0.0001d)
            && string.Equals(
                SnapshotFact(exact, MigratedProducerOutcomeIds.SpeciesFact),
                SpeciesId,
                StringComparison.Ordinal)
            && string.Equals(
                SnapshotFact(exact, MigratedProducerOutcomeIds.OccurrenceFact),
                occurrence.instanceId,
                StringComparison.Ordinal),
            "The seasonal-arrival parent outcome omitted exact wildlife identities or count.");

        int reservationCount = probe.ReservationCount(Kind);
        int commitCount = probe.CommitCount(Kind);
        Require(restored.TryObserveSeasonalWildlifeArrival(
                occurrence.instanceId,
                species.DisplayName + " 두 번째 변경",
                expiredDay + 1,
                out failure)
            && probe.ReservationCount(Kind) == reservationCount
            && probe.CommitCount(Kind) == commitCount,
            "Terminal seasonal-arrival replay created a duplicate outcome.");

        IReadOnlyList<V20ResolvedEventResult> completion = restored.EvaluateDaily(
            new V20DailyEventContext
        {
            AbsoluteDay = expiredDay + 1,
            RunSeed = 100123 + (int)fault,
            Season = Season.Spring
        });
        Require(restored.ActiveSeasonalEvents.Count == 0,
            "A terminal seasonal occurrence did not resolve after its parent outcome became durable.");
        V20ResolvedEventResult genericCompleted = completion.Single(value =>
            string.Equals(value.DefinitionId, EventId, StringComparison.Ordinal)
            && string.Equals(value.ResolutionId, "completed", StringComparison.Ordinal));
        Require(string.Equals(
                genericCompleted.OccurrenceInstanceId,
                occurrence.instanceId,
                StringComparison.Ordinal),
            "The generic completed event lost its source occurrence identity.");
        VerifyGenericEventJoin(genericCompleted, occurrence.instanceId, fault);
    }

    private static void VerifyGenericEventJoin(
        V20ResolvedEventResult completed,
        string occurrenceId,
        MigratedProducerOutcomeFaultMode sourceFault)
    {
        const string ActionId = "action:eec02-generic-event";
        var rootOperationId = new GameplayOperationId(
            "content-resolution:"
            + NarrativeInferenceHash.ComputeSha256Utf8(ActionId));
        var fact = new ContentResolutionFact(new ContentResolutionFactData
        {
            operationId = ActionId,
            absoluteDay = StartDay + 1,
            resolutionIndex = 0,
            resolution = CaptureResolution(completed),
            stateBeforeFingerprint = "eec02-before",
            stateAfterFingerprint = "eec02-after"
        });
        V20ResolvedEventResult frozen = ContentResolutionFact.ToResult(
            fact.Snapshot().resolution);
        Require(string.Equals(
                frozen.OccurrenceInstanceId,
                occurrenceId,
                StringComparison.Ordinal),
            "The frozen generic resolved-event fact omitted the source occurrence.");

        var outcomes = new MigratedProducerOutcomeEditorFixture(
            "run:eec02-generic-join-"
            + sourceFault.ToString().ToLowerInvariant());
        var probe = new MigratedProducerOutcomeProbeTransaction(
            outcomes.Transaction,
            outcomes.Recorder)
        {
            TargetKind = MigratedProducerOutcomeKind.V20ResolvedEventResult
        };
        using var facts = new SocialRitualFactEditorFixture(outcomes, probe);
        Require(facts.Journal.TryReserve(
                fact,
                out DomainFactLease lease,
                out string reserveFailure),
            reserveFailure);
        DomainFactDelivery delivery = facts.Journal.Publish(lease, fact);
        Require(delivery.Completed,
            "The generic completed-event fact did not publish durably: "
            + delivery.Detail);
        GameplayOutcomeReplayIdentity identity =
            MigratedProducerOutcomeE2EAssertions.RequireExact(
                outcomes,
                MigratedProducerOutcomeKind.V20ResolvedEventResult,
                probe);
        Require(outcomes.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact)
            && string.Equals(exact.rootOperationId, rootOperationId.Value,
                StringComparison.Ordinal)
            && string.Equals(
                SnapshotFact(exact, MigratedProducerOutcomeIds.OperationFact),
                ActionId,
                StringComparison.Ordinal)
            && string.Equals(
                SnapshotFact(exact, MigratedProducerOutcomeIds.OccurrenceFact),
                occurrenceId,
                StringComparison.Ordinal)
            && string.Equals(
                SnapshotFact(exact, MigratedProducerOutcomeIds.IdentityFact),
                EventId,
                StringComparison.Ordinal)
            && string.Equals(
                SnapshotFact(exact, MigratedProducerOutcomeIds.ActionFact),
                "completed",
                StringComparison.Ordinal),
            "The generic completed-event ledger row did not join the seasonal occurrence.");
    }

    private static V20ResolvedEventRecoverySaveData CaptureResolution(
        V20ResolvedEventResult value) => new()
    {
        definitionId = value.DefinitionId,
        resolutionId = value.ResolutionId,
        effects = value.Effects.Select(effect => new V20ContentEffect
        {
            kind = effect.kind,
            targetId = effect.targetId,
            amount = effect.amount,
            durationDays = effect.durationDays
        }).ToList(),
        participantCharacterIds = value.ParticipantCharacterIds.ToList(),
        contextFactionId = value.ContextFactionId,
        instanceId = value.InstanceId,
        displayName = value.DisplayName,
        riskTier = value.RiskTier,
        riskReason = value.RiskReason,
        terminalActionLabel = value.TerminalActionLabel,
        terminalOutcomeNarrative = value.TerminalOutcomeNarrative,
        terminalEffectKinds = value.TerminalEffectKinds.ToList(),
        observedCause = value.ObservedCause,
        occurrenceInstanceId = value.OccurrenceInstanceId,
        applications = value.Applications
            .Select(application => application.Clone())
            .ToList()
    };

    private static SeasonalWildlifeArrivalMemberSaveData[] CreatePlan(
        V20ActiveEventSaveData occurrence,
        int firstWildlifeNumber) => Enumerable
        .Range(0, occurrence.seasonalWildlifeArrival.exactCount)
        .Select(index => new SeasonalWildlifeArrivalMemberSaveData
        {
            wildlifeId = "wild:" + (firstWildlifeNumber + index),
            positionX = 40 + index,
            positionY = 7
        })
        .ToArray();

    private static string SnapshotFact(
        GameplayOutcomeSnapshot snapshot,
        GameplayOutcomeFactId factId) => snapshot.facts
        .Single(value => string.Equals(
            value.factId,
            factId.Value,
            StringComparison.Ordinal)).value;

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
            "The isolated seasonal wildlife occurrence did not start exactly once.");
        return campaign;
    }

    private static string PlanIdentity(V20ActiveEventSaveData occurrence) =>
        string.Join(",", occurrence.seasonalWildlifeArrival.members
            .OrderBy(value => value.wildlifeId, StringComparer.Ordinal)
            .Select(value => value.wildlifeId + "@" + value.positionX + ":"
                + value.positionY + ":" + value.spawned));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
