#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class ApparelChangeProductionEntryDebugScenarios
{
    private const string ApparelId = "apparel:hauling-harness";
    private const string TextileId = "textile:common-wool";

    public static void RunApparelObserverFocusedForBatchMode()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        string qaRoot = Path.GetFullPath(Path.Combine(
            Application.dataPath,
            "..",
            "Artifacts",
            "QA")) + Path.DirectorySeparatorChar;
        if (!Application.isBatchMode
            || string.IsNullOrWhiteSpace(output)
            || !Path.IsPathRooted(output)
            || !Path.GetFullPath(output).StartsWith(
                qaRoot,
                StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(output))
        {
            throw new InvalidOperationException(
                "Apparel observer checks require new project-local batch evidence.");
        }

        string probe = File.ReadAllText(Path.Combine(
            Directory.GetParent(Path.GetFullPath(output)).FullName,
            "isolation-probe.txt"));
        Require(probe.Contains("PASS_NONINTERACTIVE_PROBE")
                && probe.Contains("visible=false")
                && probe.Contains("windowCreatedAndDestroyed=true")
                && probe.Contains("inJob=true"),
            "Apparel observer checks require the noninteractive desktop probe.");
        Require(RunAll(), "Apparel production-entry observer checks failed.");
        VerifyObserverRecoveryAndTransitionSequence();
        Require(EquipmentApparelCausalProductionEntryDebugScenarios.RunAll(),
            "Equipment-apparel causal checkpoint regression failed.");
        Debug.Log("B89_APPAREL_OBSERVER_FOCUSED=PASS");
    }

    public static bool RunAll(bool log = false)
    {
        using Fixture fixture = new();

        int versionBefore = fixture.Aggregate.Version;
        int itemVersionBefore = fixture.Items.ItemStackVersion;
        Require(
            fixture.Aggregate.GetEquipped(fixture.CharacterId).Count == 0,
            "The apparel aggregate did not start without equipped apparel.");
        Require(
            fixture.Aggregate.TryPlanChange(
                fixture.CharacterId,
                fixture.ItemInstanceId,
                out ApparelChangePlan plan,
                out DomainFailure planFailure),
            "CharacterApparelAggregate.TryPlanChange rejected the real unique apparel stack: "
            + Describe(planFailure));
        Require(
            plan != null
            && plan.CharacterId.Equals(fixture.CharacterId)
            && plan.ItemInstanceId.Equals(fixture.ItemInstanceId)
            && string.Equals(plan.StackId, fixture.StackId,
                StringComparison.Ordinal)
            && ReferenceEquals(plan.Definition, fixture.Definition)
            && plan.Instance != null
            && plan.Fit.AvailablePoints != AnatomyAttachmentPoint.None
            && fixture.Aggregate.Version == versionBefore
            && fixture.Items.ItemStackVersion == itemVersionBefore,
            "The apparel plan lost its exact character, stack, instance, authored definition, fit, or read-only state.");

        Require(
            fixture.Aggregate.TryCommitChange(
                plan,
                out DomainFailure commitFailure),
            "CharacterApparelAggregate.TryCommitChange rejected the planned production change: "
            + Describe(commitFailure));

        string equippedDestination =
            CharacterApparelAggregate.EquippedDestinationPrefix
            + fixture.CharacterId.Value;
        WorldItemStackSnapshot physical = fixture.Items.GetAllStacks()
            .SingleOrDefault(value => value != null
                && string.Equals(value.StackId, fixture.StackId,
                    StringComparison.Ordinal));
        EquippedApparelSnapshot equipped = fixture.Aggregate
            .GetEquipped(fixture.CharacterId)
            .SingleOrDefault();
        Require(
            fixture.Aggregate.Version == versionBefore + 1
            && fixture.Items.ItemStackVersion == itemVersionBefore + 1
            && physical != null
            && physical.Quantity == 1
            && physical.State == WorldItemStackState.Carried
            && string.Equals(physical.DestinationId, equippedDestination,
                StringComparison.Ordinal)
            && string.Equals(physical.ItemInstanceId,
                fixture.ItemInstanceId.Value,
                StringComparison.Ordinal)
            && ApparelItemStateCodec.TryRead(
                physical.Components,
                out ApparelInstanceState retainedState)
            && string.Equals(retainedState.apparelDefinitionId, ApparelId,
                StringComparison.Ordinal)
            && equipped.CharacterId.Equals(fixture.CharacterId)
            && equipped.ItemInstanceId.Equals(fixture.ItemInstanceId)
            && string.Equals(equipped.ApparelDefinitionId, ApparelId,
                StringComparison.Ordinal)
            && equipped.Layer == fixture.Definition.Layer
            && equipped.OccupiedPoints == fixture.Definition.OccupiedPoints
            && fixture.Aggregate.TryGetByItemInstance(
                fixture.ItemInstanceId,
                out EquippedApparelSnapshot byInstance)
            && byInstance.CharacterId.Equals(fixture.CharacterId),
            "The committed apparel change did not move the unique physical stack or advance the exact aggregate equipped state.");

        Require(
            fixture.PublishedIdentityEvents.Count == 1
            && fixture.PublishedIdentityEvents[0].Character.Equals(
                fixture.CharacterId)
            && string.Equals(
                fixture.PublishedIdentityEvents[0].ApparelId,
                ApparelId,
                StringComparison.Ordinal)
            && fixture.PublishedIdentityEvents[0].Equipped
            && fixture.PublishedIdentityEvents[0].Origin
                == CharacterCommandOrigin.DirectPlayerOrder,
            "The real CharacterIdentityEventPublisher did not publish the exact equipped apparel event.");

        long ownerRevision = fixture.ApparelChanges.Capture()
            .transitionRevision;
        string operationId = "apparel-change:"
            + fixture.CharacterId.Value
            + ":revision:"
            + ownerRevision.ToString("D8");
        GameplayResultKey key = new(
            EvolutionOutcomeIds.ApparelProducerId,
            new GameplayOperationId(operationId),
            ownerRevision,
            0);
        Require(
            fixture.Outcomes.Ledger.TryGetResultIdentity(
                key,
                out GameplayOutcomeReplayIdentity identity)
            && identity.ResultKey.Equals(key)
            && identity.State
                == GameplayOutcomeReplayState.PublishedAcknowledged
            && identity.HasCanonicalPayloadHash
            && fixture.Outcomes.Ledger.TryGetExact(
                identity.OutcomeId,
                out GameplayOutcomeSnapshot exact)
            && exact != null
            && string.Equals(
                exact.producerId,
                EvolutionOutcomeIds.ApparelProducerId,
                StringComparison.Ordinal)
            && string.Equals(
                exact.outcomeTypeId,
                EvolutionOutcomeIds.ApparelChanged.Value,
                StringComparison.Ordinal)
            && string.Equals(exact.operationId, operationId,
                StringComparison.Ordinal)
            && exact.commitRevision == ownerRevision
            && exact.localResultIndex == 0
            && exact.ownerRevision == ownerRevision
            && exact.status == GameplayOutcomeStatus.Succeeded
            && exact.lifecycle
                == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
            && exact.participants.Count == 1
            && string.Equals(
                exact.participants[0].entityKindId,
                EvolutionOutcomeIds.CharacterKind.Value,
                StringComparison.Ordinal)
            && string.Equals(
                exact.participants[0].entityId,
                fixture.CharacterId.Value,
                StringComparison.Ordinal)
            && string.Equals(
                exact.participants[0].roleId,
                EvolutionOutcomeIds.WearerRole.Value,
                StringComparison.Ordinal)
            && exact.participants[0].participationKind
                == GameplayParticipationKind.Direct
            && exact.participants[0].hasPerceptionEvidence
            && string.Equals(
                exact.participants[0].displayText,
                fixture.CharacterDisplayName,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(
                exact.participants[0].displayRevision)
            && string.Equals(
                exact.participants[0].locale,
                "ko-KR",
                StringComparison.Ordinal)
            && exact.subjects.Count == 1
            && string.Equals(
                exact.subjects[0].entityKindId,
                EvolutionOutcomeIds.CharacterKind.Value,
                StringComparison.Ordinal)
            && string.Equals(
                exact.subjects[0].entityId,
                fixture.CharacterId.Value,
                StringComparison.Ordinal)
            && Mathf.Approximately(exact.subjects[0].salience, 0.45f)
            && exact.subjects[0].tier == NarrativeMemoryTier.Recent
            && !exact.subjects[0].isPinned
            && !exact.subjects[0].isOptionalWitness
            && exact.metrics.Count == 2
            && exact.metrics.Any(value => value != null
                && string.Equals(
                    value.metricId,
                    EvolutionOutcomeIds.ApparelEquippedMetric.Value,
                    StringComparison.Ordinal)
                && Mathf.Approximately((float)value.value, 1f)
                && string.Equals(
                    value.unitId,
                    EvolutionOutcomeIds.BooleanUnit.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.referenceKindId,
                    EvolutionOutcomeIds.ApparelKind.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.referenceId,
                    ApparelId,
                    StringComparison.Ordinal))
            && exact.metrics.Any(value => value != null
                && string.Equals(
                    value.metricId,
                    EvolutionOutcomeIds.ApparelOriginMetric.Value,
                    StringComparison.Ordinal)
                && Mathf.Approximately(
                    (float)value.value,
                    (int)CharacterCommandOrigin.DirectPlayerOrder)
                && string.Equals(
                    value.unitId,
                    EvolutionOutcomeIds.EnumUnit.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.referenceKindId,
                    EvolutionOutcomeIds.ApparelKind.Value,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.referenceId,
                    ApparelId,
                    StringComparison.Ordinal))
            && exact.tags.Count == 1
            && string.Equals(
                exact.tags[0],
                EvolutionOutcomeIds.EquipmentTag.Value,
                StringComparison.Ordinal),
            "The exact equipment.apparel-change/equipment.apparel-changed result was not PublishedAcknowledged.");
        Require(
            fixture.Outcomes.Ledger.CaptureGameplayOutcomes()
                .exactOutcomes.Count == 1,
            "The production-entry ledger did not retain exactly one apparel-change outcome.");

        if (log)
        {
            Debug.Log(
                "Apparel change production-entry outcome PASS: "
                + EvolutionOutcomeIds.ApparelProducerId + "/"
                + EvolutionOutcomeIds.ApparelChanged.Value + "; destination="
                + equippedDestination);
        }
        return true;
    }

    private static void VerifyObserverRecoveryAndTransitionSequence()
    {
        using (var missing = new Fixture(
                   FixturePublicationMode.MissingObserver))
        {
            WorldItemStackSnapshot loose = missing.AddLooseGarment(
                0xA771C0E0UL,
                new Vector2Int(7, 5));
            var appliedPosition = new Vector2Int(11, 13);
            const string appliedDestination =
                "warehouse:apparel-route-roundtrip";
            bool routeApplied = missing.Items.TryRouteStackToDestination(
                loose.StackId,
                WorldItemStackState.Stored,
                appliedDestination,
                appliedPosition,
                out string routeFailure);
            string restoreRouteFailure = string.Empty;
            bool routeRestored = routeApplied
                && missing.Items.TryRestoreStackRoute(
                    loose,
                    WorldItemStackState.Stored,
                    appliedDestination,
                    out restoreRouteFailure);
            Require(string.IsNullOrEmpty(loose.DestinationId)
                    && !loose.HasDestinationPosition
                    && routeRestored,
                "Exact apparel route round-trip failed: "
                + routeFailure + "/" + restoreRouteFailure);
            WorldItemStackSnapshot restoredLoose = missing.Items.GetAllStacks()
                .Single(value => value.StackId == loose.StackId);
            Require(string.IsNullOrEmpty(restoredLoose.DestinationId)
                    && restoredLoose.SourceStorageDestinationId
                        == loose.SourceStorageDestinationId
                    && !restoredLoose.HasDestinationPosition
                    && restoredLoose.DestinationPosition
                        == loose.DestinationPosition,
                "Exact apparel route rollback invented a destination or destination position.");
            int apparelVersion = missing.Aggregate.Version;
            int itemVersion = missing.Items.ItemStackVersion;
            WorldItemStackSnapshot before = missing.Items.GetAllStacks()
                .Single(value => value.ItemInstanceId
                    == missing.ItemInstanceId.Value);
            Require(missing.Aggregate.TryPlanChange(
                    missing.CharacterId,
                    missing.ItemInstanceId,
                    out ApparelChangePlan plan,
                    out DomainFailure planFailure),
                "Missing-observer apparel fixture could not plan its real garment: "
                + Describe(planFailure));
            Require(!missing.Aggregate.TryCommitChange(
                        plan,
                        out DomainFailure missingFailure)
                    && missingFailure.IsFailure
                    && missing.Aggregate.Version == apparelVersion
                    && missing.Items.ItemStackVersion == itemVersion
                    && missing.PublishedIdentityEvents.Count == 0
                    && missing.ApparelChanges.Capture().observations.Count == 0,
                "A missing apparel observer did not reject before physical movement: "
                + Describe(missingFailure));
            WorldItemStackSnapshot after = missing.Items.GetAllStacks()
                .Single(value => value.ItemInstanceId
                    == missing.ItemInstanceId.Value);
            Require(after.State == before.State
                    && after.DestinationId == before.DestinationId
                    && after.Position == before.Position,
                "A missing apparel observer changed the physical garment route.");
        }

        using var source = new Fixture(FixturePublicationMode.Faulting);
        ItemInstanceId overrideItem = source.AddStoredGarment(0xA771C0DFUL);
        Require(source.Aggregate.TryPlanChange(
                source.CharacterId,
                source.ItemInstanceId,
                out ApparelChangePlan equipPlan,
                out DomainFailure equipPlanFailure),
            "ACK-loss apparel fixture could not plan its real garment: "
            + Describe(equipPlanFailure));
        source.FaultingFacts.HideNextCommittedAcknowledgement = true;
        Require(source.Aggregate.TryCommitChange(
                    equipPlan,
                    out DomainFailure equipFailure)
                && !equipFailure.IsFailure,
            "Committed apparel movement was rejected after its ACK was hidden: "
            + Describe(equipFailure));

        ApparelChangePublicationSaveData pending =
            source.ApparelChanges.Capture();
        Require(pending.transitionRevision == 1L
                && pending.observations.Count == 1
                && pending.observations[0].pending
                && pending.observations[0].fact.changeIndex == 0
                && pending.observations[0].fact.changeCount == 1
                && source.FaultingFacts.CommittedBatchCalls == 1
                && source.Aggregate.GetEquipped(source.CharacterId)
                    .Single().ItemInstanceId.Equals(source.ItemInstanceId)
                && source.PublishedIdentityEvents.Count == 1,
            "Apparel ACK loss did not retain one applied physical change and one frozen pending fact.");
        string publicationJson =
            new ApparelChangePublicationSaveSection(source.ApparelChanges)
                .Capture();
        ApparelChangePublicationSaveSection.ValidateJson(publicationJson);
        ApparelChangePublicationSaveData savedPublications =
            DungeonSaveJson.Deserialize<ApparelChangePublicationSaveData>(
                publicationJson);
        GameplayOutcomeLedgerSaveData savedLedger =
            source.Outcomes.Ledger.CaptureGameplayOutcomes();
        ApparelLoadoutChangedFactPreflight.ValidateObservation(
            savedPublications.observations.Single(),
            savedLedger);
        IReadOnlyList<EquippedApparelSaveData> savedApparel =
            source.Aggregate.CaptureApparel();
        IReadOnlyList<CharacterApparelPolicySaveData> savedPolicies =
            source.Aggregate.CaptureApparelPolicies();
        string physicalBeforeRecovery = JsonUtility.ToJson(
            source.Items.Capture());
        int eventsBeforeRecovery = source.PublishedIdentityEvents.Count;

        using var restoredOutcomes = new EvolutionGameplayOutcomeEditorFixture(
            "run:apparel-change-observer-restored",
            GameCalendarRules.SecondsPerDay * 2f);
        RestoreLedger(restoredOutcomes.Ledger, savedLedger);
        restoredOutcomes.ApparelChanges.Restore(savedPublications);
        CharacterApparelAggregate restoredOwner = source.CreateAggregate(
            restoredOutcomes.ApparelChanges,
            restoredOutcomes.Calendar);
        source.RestoreAggregate(restoredOwner, savedApparel, savedPolicies);
        Require(restoredOutcomes.ApparelChanges.RecoverPending(
                    out string recoveryFailure)
                && source.PublishedIdentityEvents.Count == eventsBeforeRecovery
                && physicalBeforeRecovery == JsonUtility.ToJson(
                    source.Items.Capture())
                && restoredOutcomes.Ledger.CaptureGameplayOutcomes()
                    .exactOutcomes.Count == 1,
            "Fresh apparel observation recovery repeated movement, events, or ledger rows: "
            + recoveryFailure);

        ApparelChangePublicationData recovered = restoredOutcomes
            .ApparelChanges.Capture().observations.Single();
        Require(!recovered.pending
                && !string.IsNullOrWhiteSpace(recovered.publicationId)
                && recovered.fingerprint?.Length == 64,
            "Fresh apparel journal did not acknowledge its exact frozen fact.");
        VerifyObserverLifecycle(
            recovered.fact,
            restoredOutcomes.Registry,
            restoredOutcomes.Ledger,
            restoredOutcomes.Recorder);

        source.RestoreAggregate(restoredOwner, savedApparel, savedPolicies);
        Require(restoredOwner.Version == 2
                && restoredOutcomes.ApparelChanges.Capture()
                    .transitionRevision == 1L,
            "A repeated owner snapshot restore advanced the durable apparel transition sequence.");
        Require(restoredOwner.TryUnequip(
                    source.CharacterId,
                    source.ItemInstanceId,
                    out DomainFailure unequipFailure)
                && !unequipFailure.IsFailure,
            "Real apparel unequip failed after observation recovery: "
            + Describe(unequipFailure));
        ApparelChangePublicationSaveData afterUnequip =
            restoredOutcomes.ApparelChanges.Capture();
        Require(afterUnequip.transitionRevision == 2L
                && afterUnequip.observations.Last().fact.transitionRevision == 2L
                && restoredOwner.Version != 2,
            "The next apparel fact reused cache version instead of its durable transition revision.");

        bool reEquipPlanned = restoredOwner.TryPlanChange(
            source.CharacterId,
            source.ItemInstanceId,
            out ApparelChangePlan reEquipPlan,
            out DomainFailure reEquipPlanFailure);
        DomainFailure reEquipFailure = DomainFailure.None;
        bool reEquipped = reEquipPlanned
            && restoredOwner.TryCommitChange(
                reEquipPlan,
                out reEquipFailure);
        Require(reEquipped,
            "Real apparel re-equip failed before temporary override: "
            + Describe(reEquipPlanFailure) + "/" + Describe(reEquipFailure));
        bool overrideStarted = restoredOwner.TryBeginTemporaryOverride(
            source.CharacterId,
            overrideItem,
            "qa:apparel-observer",
            out DomainFailure overrideFailure);
        DomainFailure restoreFailure = DomainFailure.None;
        bool overrideRestored = overrideStarted
            && restoredOwner.TryRestoreTemporaryOverride(
                source.CharacterId,
                out restoreFailure);
        Require(overrideRestored,
            "Real temporary apparel override did not round-trip its two-change cohorts: "
            + Describe(overrideFailure) + "/" + Describe(restoreFailure));

        ApparelChangePublicationSaveData completed =
            restoredOutcomes.ApparelChanges.Capture();
        Require(completed.transitionRevision == 5L
                && completed.observations.Count == 7
                && completed.observations.All(value => !value.pending)
                && completed.observations
                    .Where(value => value.fact.transitionRevision >= 4L)
                    .GroupBy(value => value.fact.transitionRevision)
                    .All(group => group.Count() == 2
                        && group.Select(value => value.fact.changeIndex)
                            .SequenceEqual(new[] { 0, 1 }))
                && restoredOutcomes.Ledger.CaptureGameplayOutcomes()
                    .exactOutcomes.Count == 7
                && source.PublishedIdentityEvents.Count == 7,
            "Apparel equip, unequip, override, or restore lost its exact transition cohort.");
        GameplayOutcomeLedgerSaveData completedLedger = restoredOutcomes.Ledger.CaptureGameplayOutcomes();
        ApparelLoadoutChangedFactPreflight.ValidateJournal(completed, completedLedger);
        var missingHistory = ApparelChangePublicationRuntime.ValidateAndClone(completed);
        missingHistory.observations.RemoveAll(value => value.fact.transitionRevision == 5L);
        missingHistory.transitionRevision = 4L;
        bool missingHistoryRejected = false;
        try { ApparelLoadoutChangedFactPreflight.ValidateJournal(missingHistory, completedLedger); }
        catch (InvalidOperationException error)
        { missingHistoryRejected = error.Message == "apparel-change-journal-history-missing"; }
        Require(missingHistoryRejected, "A journal could drop completed apparel history and restart its sequence.");
        Debug.Log("B89_APPAREL_OWNER_RECOVERY=PASS");
    }

    private static void RestoreLedger(
        GameplayOutcomeLedger ledger,
        GameplayOutcomeLedgerSaveData saved)
    {
        ledger.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            ledger.PrepareGameplayOutcomeRestore(saved);
        ledger.PublishGameplayOutcomeRestore(candidate);
        ledger.PublishRestoreCandidate();
        ledger.CompleteRestoreCandidate();
    }

    private static void VerifyObserverLifecycle(
        ApparelLoadoutChangedFactData data,
        GameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        GameplayOutcomeRecorder recorder)
    {
        var fact = new ApparelLoadoutChangedFact(data);
        using DomainFactDispatcher facts = new();
        Require(!facts.Replay(fact).MayBeCommitted,
            "Apparel fact replay succeeded without its observer.");
        var binding = ApparelLoadoutChangedFactMapping.CreateBinding(
            registry,
            ledger,
            recorder,
            new GameplayDomainFactCommitGroup(recorder, ledger));
        IDisposable connection = binding.Connect(facts);
        bool duplicateRejected = false;
        try
        {
            try
            {
                using IDisposable duplicate = binding.Connect(facts);
            }
            catch (InvalidOperationException error)
            {
                duplicateRejected = error.Message.Contains(
                    "Duplicate durable domain observer",
                    StringComparison.Ordinal);
            }
            Require(duplicateRejected,
                "Apparel observer accepted a duplicate binding.");
        }
        finally
        {
            connection.Dispose();
        }
        Require(!facts.Replay(fact).MayBeCommitted,
            "Disposed apparel observer still received replay.");
        using IDisposable rebound = binding.Connect(facts);
        Require(facts.Replay(fact).Completed,
            "Rebound apparel observer did not deduplicate exact replay.");
    }

    private static string Describe(in DomainFailure failure) =>
        failure.IsFailure
            ? failure.Code.ToString() + ":"
                + string.Join(",", failure.Parameters.ToArray())
            : FailureCode.None.ToString();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static T Proxy<T>() where T : class =>
        DispatchProxy.Create<T, DefaultInterfaceProxy>();

    public class DefaultInterfaceProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private enum FixturePublicationMode
    {
        Normal,
        MissingObserver,
        Faulting
    }

    private sealed class FaultingApparelFacts :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        internal FaultingApparelFacts(DomainFactDispatcher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool HideNextCommittedAcknowledgement { get; set; }
        internal int CommittedBatchCalls { get; private set; }
        internal int ReplayBatchCalls { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => inner.TryReserve(
                proposed,
                out lease,
                out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual) => inner.Publish(lease, actual);

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (!accepted)
                return false;
            CommittedBatchCalls++;
            if (!HideNextCommittedAcknowledgement)
                return true;
            HideNextCommittedAcknowledgement = false;
            failure = "editor-injected-apparel-ack-loss-after-real-commit";
            for (int index = 0; index < results.Length; index++)
                results[index] = DomainFactDelivery.Uncertain(failure);
            return true;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            ReplayBatchCalls++;
            return inner.ReplayBatch(facts, results, out failure);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameObject actorObject;
        private readonly CharacterSO actorData;
        private readonly CharacterAiWorldRegistry world;
        private readonly IDisposable apparelEventSubscription;
        private readonly CharacterIdentityEventPublisher identityPublisher;
        private readonly DomainFactDispatcher ownedFacts;

        internal Fixture(
            FixturePublicationMode publicationMode = FixturePublicationMode.Normal)
        {
            Outcomes = new EvolutionGameplayOutcomeEditorFixture(
                "run:apparel-change-production-entry",
                GameCalendarRules.SecondsPerDay * 2f);
            if (publicationMode == FixturePublicationMode.MissingObserver)
            {
                ownedFacts = new DomainFactDispatcher();
                ApparelChanges = new ApparelChangePublicationRuntime(
                    ownedFacts,
                    ownedFacts);
            }
            else if (publicationMode == FixturePublicationMode.Faulting)
            {
                FaultingFacts = new FaultingApparelFacts(
                    Outcomes.ApparelChangeFacts);
                ApparelChanges = new ApparelChangePublicationRuntime(
                    FaultingFacts,
                    FaultingFacts);
            }
            else
            {
                ApparelChanges = Outcomes.ApparelChanges;
            }
            Items = PhysicalItemDebugScenarios
                .CreateRuntimeForCrossDomainFixture(
                    out WorldItemRepository repository,
                    out _,
                    out _,
                    out _);
            Repository = repository;

            IGameContentDefinitionSource content =
                CharacterAiEditorTestDependencies.ContentDefinitions;
            ApparelCatalog = new ResourceApparelDefinitionCatalog(content);
            TextileCatalog = new ResourceTextileMaterialCatalog(content);
            Require(
                ApparelCatalog.TryGet(
                    ApparelId,
                    out ApparelDefinitionSO definition),
                "The authored hauling-harness apparel definition is missing.");
            Require(
                TextileCatalog.TryGet(
                    TextileId,
                    out TextileMaterialDefinitionSO textile),
                "The authored common-wool textile definition is missing.");
            Require(
                Items.CatalogProvider.TryGetDefinition(
                    definition.PhysicalItemId,
                    out _),
                "The authored apparel physical item is missing from the real item catalog.");
            Definition = definition;
            Textile = textile;

            world = new CharacterAiWorldRegistry(
                new SceneRuntimeRegistry<CharacterActor>(),
                new SceneRuntimeRegistry<WildlifeActor>(),
                new SceneRuntimeRegistry<BuildableObject>(),
                new SceneRuntimeRegistry<IWarehouseFacility>(),
                new SceneRuntimeRegistry<IRetailFacility>(),
                Proxy<IGameSessionStateProvider>(),
                Proxy<IRestoreWorldCandidateQuery>(),
                Proxy<ICharacterLifePublicationService>());
            actorObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "ApparelChangeProductionEntryWearer");
            actorData = CharacterAiPlanDebugFixtures.CreateCharacterData(
                CharacterType.NPC,
                "의류 교체 검증 주민",
                "Slime");
            Actor = actorObject.GetComponent<CharacterActor>();
            Actor.Initialize(actorData);
            world.RegisterCharacter(Actor);
            CharacterId = CharacterPersistentIdentity.Require(Actor);
            CharacterDisplayName = Actor.Identity?.DisplayName?.Trim()
                ?? string.Empty;
            Require(
                CharacterDisplayName.Length > 0,
                "The real apparel wearer has no immutable display name.");

            Anatomy = new AnatomyAttachmentQuery(
                world,
                CharacterAiEditorTestDependencies.BodyHealthRuntime);
            Availability = new ApparelAvailabilityIndex(
                Items,
                ApparelCatalog,
                TextileCatalog);

            ItemInstanceId = ((IItemInstanceRepository)Repository)
                .AllocateItemInstanceId();
            var apparelState = new ApparelInstanceState
            {
                apparelDefinitionId = Definition.ApparelId,
                primaryMaterialId = Textile.MaterialId,
                craftsmanshipQuality = CraftsmanshipQualityTier.Normal,
                sourceKind = TextileSourceKind.Animal,
                sourceDefinitionId = Textile.MaterialId,
                size = Anatomy.GetSize(CharacterId),
                modifications = ApparelModificationKind.None,
                closedOpenings = ApparelModificationKind.None,
                durability = 100f,
                moisture = 0f,
                contamination = 0f,
                craftedAbsoluteDay = 2,
                deterministicBatchHash = 0xA771C0DEUL
            };
            StackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                Definition.PhysicalItemId,
                1,
                WorldItemStackState.Stored,
                destinationId: "warehouse:apparel-change-production-entry",
                position: new Vector2Int(2, 2),
                itemInstanceId: ItemInstanceId.Value,
                components: new[]
                {
                    ApparelItemStateCodec.Create(apparelState)
                });
            Require(
                ItemInstanceId.IsValid
                && Items.GetAllStacks().Count(value => value != null
                    && string.Equals(
                        value.ItemInstanceId,
                        ItemInstanceId.Value,
                        StringComparison.Ordinal)) == 1,
                "The real repository did not retain one unique apparel instance stack.");

            ApparelCandidate[] candidates = new ApparelCandidate[8];
            int candidateCount = Availability.FindCandidates(
                new ApparelSelectionQuery(
                    Anatomy.GetAvailablePoints(CharacterId),
                    Anatomy.GetSize(CharacterId),
                    Definition.Layer,
                    ApparelUseTag.None,
                    TextileMaterialTag.None,
                    CraftsmanshipQualityTier.Awful),
                candidates);
            Require(
                candidates.Take(candidateCount).Any(value =>
                    value.ItemInstanceId.Equals(ItemInstanceId)
                    && string.Equals(value.StackId, StackId,
                        StringComparison.Ordinal)),
                "The real apparel availability index did not expose the unique authored garment.");

            GameEventBus identityBus = new();
            apparelEventSubscription = identityBus.Subscribe<ApparelChangedEvent>(
                value => PublishedIdentityEvents.Add(value));
            identityPublisher = new CharacterIdentityEventPublisher(identityBus);
            Aggregate = CreateAggregate(ApparelChanges);
        }

        internal CharacterApparelAggregate CreateAggregate(
            ApparelChangePublicationRuntime publications,
            IGameCalendar calendar = null) =>
            new CharacterApparelAggregate(
                new CharacterApparelAggregateStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                ApparelCatalog,
                Anatomy,
                Items,
                Items,
                world,
                Availability,
                identityPublisher,
                calendar ?? Outcomes.Calendar,
                publications);

        internal ItemInstanceId AddStoredGarment(ulong deterministicHash)
        {
            ItemInstanceId itemInstanceId =
                ((IItemInstanceRepository)Repository).AllocateItemInstanceId();
            var apparelState = new ApparelInstanceState
            {
                apparelDefinitionId = Definition.ApparelId,
                primaryMaterialId = Textile.MaterialId,
                craftsmanshipQuality = CraftsmanshipQualityTier.Normal,
                sourceKind = TextileSourceKind.Animal,
                sourceDefinitionId = Textile.MaterialId,
                size = Anatomy.GetSize(CharacterId),
                modifications = ApparelModificationKind.None,
                closedOpenings = ApparelModificationKind.None,
                durability = 100f,
                moisture = 0f,
                contamination = 0f,
                craftedAbsoluteDay = 2,
                deterministicBatchHash = deterministicHash
            };
            WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                Definition.PhysicalItemId,
                1,
                WorldItemStackState.Stored,
                destinationId: "warehouse:apparel-change-production-entry",
                position: new Vector2Int(2, 2),
                itemInstanceId: itemInstanceId.Value,
                components: new[] { ApparelItemStateCodec.Create(apparelState) });
            Require(Items.GetAllStacks().Count(value => value != null
                    && string.Equals(
                        value.ItemInstanceId,
                        itemInstanceId.Value,
                        StringComparison.Ordinal)) == 1,
                "The apparel observer fixture did not retain its second unique garment.");
            return itemInstanceId;
        }

        internal WorldItemStackSnapshot AddLooseGarment(
            ulong deterministicHash,
            Vector2Int position)
        {
            ItemInstanceId itemInstanceId =
                ((IItemInstanceRepository)Repository).AllocateItemInstanceId();
            var apparelState = new ApparelInstanceState
            {
                apparelDefinitionId = Definition.ApparelId,
                primaryMaterialId = Textile.MaterialId,
                craftsmanshipQuality = CraftsmanshipQualityTier.Normal,
                sourceKind = TextileSourceKind.Animal,
                sourceDefinitionId = Textile.MaterialId,
                size = Anatomy.GetSize(CharacterId),
                modifications = ApparelModificationKind.None,
                closedOpenings = ApparelModificationKind.None,
                durability = 100f,
                moisture = 0f,
                contamination = 0f,
                craftedAbsoluteDay = 2,
                deterministicBatchHash = deterministicHash
            };
            string stackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                Definition.PhysicalItemId,
                1,
                WorldItemStackState.Loose,
                destinationId: string.Empty,
                position: position,
                itemInstanceId: itemInstanceId.Value,
                components: new[] { ApparelItemStateCodec.Create(apparelState) });
            return Items.GetAllStacks().Single(value => value.StackId == stackId);
        }

        internal void RestoreAggregate(
            CharacterApparelAggregate target,
            IReadOnlyList<EquippedApparelSaveData> apparel,
            IReadOnlyList<CharacterApparelPolicySaveData> policies)
        {
            var report = new DungeonGameRestoreReport();
            CharacterApparelRestoreCandidate candidate = target.PrepareRestoreApparel(
                apparel,
                policies,
                report);
            Require(report.Success,
                "Fresh apparel owner rejected its saved physical join: "
                + string.Join(" | ", report.Errors));
            target.PublishRestoreApparel(candidate);
        }

        internal EvolutionGameplayOutcomeEditorFixture Outcomes { get; }
        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal ResourceApparelDefinitionCatalog ApparelCatalog { get; }
        internal ResourceTextileMaterialCatalog TextileCatalog { get; }
        internal ApparelDefinitionSO Definition { get; }
        internal TextileMaterialDefinitionSO Textile { get; }
        internal CharacterActor Actor { get; }
        internal CharacterId CharacterId { get; }
        internal string CharacterDisplayName { get; }
        internal AnatomyAttachmentQuery Anatomy { get; }
        internal ApparelAvailabilityIndex Availability { get; }
        internal ItemInstanceId ItemInstanceId { get; }
        internal string StackId { get; }
        internal CharacterApparelAggregate Aggregate { get; }
        internal ApparelChangePublicationRuntime ApparelChanges { get; }
        internal FaultingApparelFacts FaultingFacts { get; }
        internal List<ApparelChangedEvent> PublishedIdentityEvents { get; } =
            new();

        public void Dispose()
        {
            apparelEventSubscription?.Dispose();
            if (Actor != null)
                world.UnregisterCharacter(Actor);
            CharacterAiPlanDebugFixtures.DestroyProbeObject(actorObject);
            if (actorData != null)
                UnityEngine.Object.DestroyImmediate(actorData);
            Items?.Dispose();
            ownedFacts?.Dispose();
            Outcomes?.Dispose();
        }
    }
}
#endif
