using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Infrastructure;
using DungeonStory.Narrative.Korean;
using DungeonStory.Operation;
using Newtonsoft.Json;

/// <summary>
/// Pure managed host probe for the combat-action fact observer. The host runner
/// supplies the compiled project assemblies; this file deliberately uses no
/// Unity runtime API or replacement gameplay implementation.
/// </summary>
public static class CombatActionObserverHostProbe
{
    private const string HostSerializer =
        "Newtonsoft.Json host round-trip (not Unity JsonUtility)";
    private const string OffenseTruthTargetId =
        "offense-target:host-truth";

    private static int authoritySequence;

    public static string Run()
    {
        var checks = new List<string>(47);

        VerifyObserverConnectionBoundary();
        checks.Add("observer-connect-disconnect-duplicate");

        SavedAuthority normal = VerifyNormalDamageAndSameFactReplay();
        checks.Add("normal-damage-and-same-fact-replay");

        VerifyFatalSocialParentCohort();
        checks.Add("fatal-social-parent-cohort");

        VerifyJsonRestoreExactReplayAndTamper(normal);
        checks.Add("host-json-fresh-authority-exact-and-tamper");

        VerifyKnownReservationAndPrepareRejections();
        checks.Add("known-reservation-and-prepare-rejections");

        VerifyAtomicCommitAmbiguityBoundaries();
        checks.Add("atomic-commit-before-after-ambiguity");

        VerifyCharacterSkillActivationFactObserver();
        checks.Add("character-skill-frozen-parity-atomic-replay");

        VerifyCharacterSkillActivationPublicationRuntime();
        checks.Add("character-skill-publication-journal-recovery");

        VerifyDiseaseAnatomyBurdenFactObserver();
        checks.Add("disease-anatomy-burden-frozen-exact-replay");

        VerifyCharacterLifeTransitionFactObservation();
        checks.Add("character-life-parent-child-frozen-exact-replay");

        VerifyCharacterRestFactObservation();
        checks.Add("character-rest-frozen-observer-replay-and-projection");

        VerifyEmergencyWorkSuspensionOwnerFactRecovery();
        checks.Add("emergency-work-suspension-owner-fact-recovery");

        VerifySpeciesActionFactObservation();
        checks.Add("species-action-frozen-mapping-and-recovery");

        VerifyWorkAccidentFactObservation();
        checks.Add("work-accident-frozen-observer-recovery");

        VerifyRepairWorkCompletedFactObservation();
        checks.Add("repair-work-completed-frozen-observer-recovery");

        VerifyConstructionWorkEndedFactObservation();
        checks.Add("construction-work-ended-frozen-observer-recovery");

        VerifyPrimitiveSurvivalCompletedFactObservation();
        checks.Add("primitive-survival-completed-frozen-observer-recovery");

        VerifyApparelWorkCompletedFactObservation();
        checks.Add("apparel-work-completed-frozen-observer-recovery");

        VerifyPhysicalCarcassFactCohort();
        checks.Add("physical-carcass-frozen-atomic-observer-recovery");

        VerifySurvivalMealMissedFactCohort();
        checks.Add("survival-meal-missed-frozen-atomic-observer-recovery");

        VerifyWorkExecutionActivityObserver();
        checks.Add("work-execution-activity-projection-lifecycle");

        VerifyWorkControlActivityObserver();
        checks.Add("work-control-30-transition-projection-lifecycle");

        VerifyCharacterVitalsActivityObserver();
        checks.Add("character-vitals-activity-projection-lifecycle");

        VerifyOwnerCommandActivityObserver();
        checks.Add("owner-command-rejection-projection-lifecycle");

        VerifyDefenseEngagementActivityObserver();
        checks.Add("defense-engagement-projection-lifecycle");

        VerifyWildlifeHuntActivityObserver();
        checks.Add("wildlife-hunt-projection-lifecycle");

        VerifyWildlifeFactObservation();
        checks.Add("wildlife-frozen-mapping-reserve-replay-tamper");

        VerifyWildlifeFoodRaidFactObservation();
        checks.Add("wildlife-food-raid-frozen-child-causation-replay");

        VerifyHeritableTraitsAnalyzedFactObservation();
        checks.Add("heritable-traits-analyzed-frozen-observer-recovery");

        VerifyFacilityEvolutionCompletedFactObservation();
        checks.Add("facility-evolution-completed-frozen-observer-recovery");

        VerifySurgeryResolvedFactObservation();
        checks.Add("surgery-resolved-frozen-observer-restore-join");

        VerifyOffenseBattleCommandPublicationRuntime();
        checks.Add("offense-battle-command-journal-cohort-recovery");

        VerifyCraftCompletionFactFreezeAndValidation();
        checks.Add("combat-craft-frozen-dto-and-output-totals");

        VerifyCraftCompletionStagingAndRestoreReplay();
        checks.Add("combat-craft-stage-json-replay-and-tamper");

        VerifyCombatEquipmentQualityObserver();
        checks.Add("combat-equipment-quality-stage-json-replay-and-tamper");

        VerifyIdleSocialInteractionObserver();
        checks.Add("idle-social-frozen-pending-publish-and-recovery");

        VerifyCaptivePerformerMilestoneRuntime();
        checks.Add("captive-performer-runtime-rollback-pending-and-recovery");

        VerifyCaptiveEscapePublicationRuntime();
        checks.Add("captive-escape-four-kinds-pending-recovery-and-tamper");

        VerifyCaptiveRansomPublicationRuntime();
        checks.Add("captive-ransom-pending-finalization-and-tamper");

        VerifyCaptivityInteractionPublicationRuntime();
        checks.Add("captivity-interaction-frozen-pending-recovery-and-tamper");

        VerifyCaptivityTransitionPublicationRecovery();
        checks.Add("captivity-transition-frozen-pending-recovery-and-tamper");

        VerifyStaffDiscontentPublicationRecovery();
        checks.Add("staff-discontent-frozen-pending-recovery-and-tamper");

        VerifyOffenseTruthPublicationRuntime();
        checks.Add("offense-truth-owner-deferral-recovery-and-tamper");

        VerifyOffenseExpeditionResultPublicationRuntime();
        checks.Add("offense-expedition-result-owner-recovery-and-tamper");

        VerifyOffenseArrivalSettlementRuntime();
        checks.Add("offense-arrival-settlement-owner-recovery-and-projection");

        VerifyOffenseNodePublicationRuntime();
        checks.Add("offense-node-owner-sequence-recovery-and-notification");

        VerifyOffenseBattleExperienceAndActivityFacts();
        checks.Add("offense-battle-experience-context-and-seven-activity-alerts");

        return JsonConvert.SerializeObject(new
        {
            result = "PASS",
            probe = nameof(CombatActionObserverHostProbe),
            serializer = HostSerializer,
            checkCount = checks.Count,
            checks,
            scope = "combat-action, character-skill activation fact observation and standalone publication journal, disease anatomy-burden, character-life parent/child, character-rest frozen observation/save join/pure narrative projection, emergency-work suspension owner fact observation, species-action frozen observation, wildlife command/action and food-raid physical-child-causal frozen observation, heritable-traits-analyzed and facility-evolution-completed frozen observation/save joins, surgery-resolved and surgery-completion-identity frozen observation/restore joins, work-accident, repair-work-completed, construction-work-ended, primitive-survival-completed, apparel-work-completed, physical-item/carcass, and survival-meal-missed frozen atomic observation, work-execution, work-control, character-vitals, owner-command rejection, defense-engagement, and wildlife-hunt activity projection, offense battle-command journal/cohort observation, combat-craft, equipment-quality, idle-social, captive-performer, captive-escape, captive-ransom, captivity-interaction, captivity-transition, staff-discontent, offense-truth, offense-expedition-result, offense-arrival-settlement, offense-node-publication, offense battle-experience/activity presentation, and ransom money-credit authority with real roots, managed host only",
            excluded = new[]
            {
                "Unity runtime and native APIs",
                "body, applier, wildlife, and gameplay execution",
                "actual physical output service and crafting gameplay execution",
                "idle AI selection and relationship/mood state mutation",
                "captivity scheduling and whole-runtime orchestration",
                "escape completion/defection physical actor and standing transitions",
                "ransom owner orchestration, physical release, actor, and standing execution",
                "captivity-interaction body effects, real item output, owner rollback, and orchestration",
                "actual CaptivityRuntime capture/recovery, transition effect application, food and physical ownership, release actor/exit, owner rollback, and whole-runtime execution",
                "StaffDiscontentRuntime MonoBehaviour, mood/lifecycle effect execution, and gameplay scheduling",
                "OffenseWorldMapRuntime MonoBehaviour, expedition finalizer orchestration, and Unity presentation",
                "offense expedition gameplay effects, rewards, finalizer orchestration, presentation, and whole-save coordination",
                "offense arrival actor materialization, physical containment/escape execution, and Unity scheduling",
                "offense node supply, health, loot, experience mutation, finalizer orchestration, and Unity scheduling",
                "offense battle actors, experience application, damage, RecordNarrative, and Unity gameplay execution",
                "character-skill effects, actor use-limit persistence/rollback, owner/DI migration, and live battle runtime gameplay",
                "actual CharacterStats rest mutation/rollback, actor lookup, narrative ledger mutation, and Unity gameplay execution",
                "actual emergency work execution, task interruption, and Unity gameplay orchestration",
                "actual work execution, AI scheduling, and actor activity storage",
                "actual work-control command, duty, selection, repair mutation, real actor recording, and native gameplay execution",
                "actual WorkOrderRuntime repair mutation, material consumption, worker completion effects, save join, and Unity gameplay execution",
                "actual WorkOrderRuntime construction placement, physical material/refund execution, and Unity gameplay execution",
                "actual primitive-survival owner effects, actor/stat/filth/water mutation, and Unity gameplay execution",
                "actual apparel work mutation, material consumption, physical item state, owner orchestration, and Unity gameplay execution",
                "actual physical transformation, carcass owner/body effects, inventory mutation, and Unity gameplay execution",
                "actual daily survival, health, water, meal consumption, and Unity gameplay execution",
                "actual character vital mutation, actor activity storage, mood, lifecycle, death runtime, and Unity gameplay execution",
                "actual owner command execution, defense engagement, wildlife hunting effects, actor storage, and Unity gameplay execution",
                "actual species effects, actors, physical movement, incident execution, and Unity gameplay orchestration",
                "actual trait-analysis aggregate, physical kit consumption, trait reveal effects, and Unity gameplay execution",
                "actual facility-evolution aggregate, building mutation, material consumption, and Unity gameplay execution",
                "actual SurgeryRuntime effects, material or body mutation, worker identity effects, execution, and Unity gameplay orchestration",
                "CombatActionPublicationRuntime and whole-world save orchestration"
            }
        });
    }

    private static void VerifySurgeryResolvedFactObservation()
    {
        SurgeryResolvedFactData source = CreateSurgeryResolvedFactData();
        string expectedContext =
            SurgeryResolvedFactMapping.ComputeContextSha256(source);
        var actual = new SurgeryResolvedFact(source);

        source.subjectName = "호출자 변조";
        source.materials[0].quantity = 99;
        source.before.currentHealth = 1f;
        SurgeryResolvedFactData detached = actual.Snapshot();
        detached.after.currentHealth = 2f;
        detached.materials.Clear();
        SurgeryResolvedFactData frozen = actual.Snapshot();
        Require(frozen.subjectName == "호스트 수술 환자"
            && frozen.materials.Count == 1
            && frozen.materials[0].quantity == 2
            && Math.Abs(frozen.before.currentHealth - 70f) < 0.0001f
            && Math.Abs(frozen.after.currentHealth - 82f) < 0.0001f
            && SurgeryResolvedFactMapping.ComputeContextSha256(frozen)
                == expectedContext,
            "Surgery resolved fact retained caller or snapshot aliases.");

        SurgeryOutcomeReceipt receipt =
            SurgeryResolvedFactMapping.Capture(actual);
        string expectedKey = SurgeryResolvedFactMapping.ResultKey(frozen)
            .ToString();
        Require(receipt.ResultKey.ToString() == expectedKey
            && receipt.AbsoluteDay == frozen.absoluteDay
            && receipt.OrderId == frozen.orderId
            && receipt.SubjectId == frozen.subjectId
            && receipt.SubjectName == frozen.subjectName
            && receipt.SubjectKind == frozen.subjectKind
            && receipt.ProcedureId == frozen.procedureId
            && receipt.ProcedureKind == frozen.procedureKind
            && receipt.DoctorId == frozen.doctorId
            && receipt.TargetNodeId == frozen.targetNodeId
            && receipt.ResultId == frozen.resultId
            && receipt.Severity == frozen.severity
            && receipt.Succeeded == frozen.succeeded
            && receipt.Materials.Contains("item:medical:gauze:2:required",
                StringComparison.Ordinal)
            && receipt.AnatomyBefore.Contains("health=70/100",
                StringComparison.Ordinal)
            && receipt.AnatomyAfter.Contains("health=82/100",
                StringComparison.Ordinal)
            && receipt.ChildReferences.Contains(
                "material-sink=physical-commit:surgery-host",
                StringComparison.Ordinal)
            && receipt.ContextSha256 == expectedContext
            && expectedContext.Length == 71
            && expectedContext.StartsWith("sha256:",
                StringComparison.Ordinal)
            && expectedContext.Substring(7).All(Uri.IsHexDigit),
            "Surgery resolved mapping lost frozen clinical or material context.");

        SurgeryResolvedFactData proposalSource = frozen.Clone();
        proposalSource.resultId = string.Empty;
        proposalSource.severity = SurgeryFailureSeverity.None;
        proposalSource.succeeded = false;
        proposalSource.after = new SurgeryAnatomyFactData();
        var proposal = new SurgeryResolvedFact(
            proposalSource,
            proposal: true);

        SavedSurgeryResolvedAuthority saved;
        using (var authority = new SurgeryFactAuthority())
        {
            Require(!authority.Dispatcher.TryReserve(
                    proposal,
                    out _,
                    out string missingFailure)
                && missingFailure.StartsWith(
                    "domain-fact-observer-missing:",
                    StringComparison.Ordinal),
                "Surgery proposal reserved without its required observer.");

            using IDisposable connection = authority.Connect();
            Require(authority.Dispatcher.TryReserve(
                    proposal,
                    out DomainFactLease lease,
                    out string failure),
                "Surgery proposal reservation failed: " + failure);
            string publicationKey = lease.PublicationKey;
            DomainFactDelivery delivery = authority.Dispatcher.Publish(
                lease,
                actual);
            GameplayOutcomeLedgerSaveData ledger =
                authority.Ledger.CaptureGameplayOutcomes();
            GameplayOutcomeSnapshot row = ledger.outbox
                .Concat(ledger.exactOutcomes)
                .Single();
            Require(delivery.Completed
                && publicationKey == expectedKey
                && delivery.OperationKey == publicationKey
                && !string.IsNullOrWhiteSpace(delivery.PublicationId)
                && delivery.Fingerprint?.Length == 64
                && OutcomeRowCount(ledger) == 1
                && row.facts.Count == 9
                && row.facts.Single(value => value.factId
                        == SurgeryOutcomeIds.ContextSha256.Value).value
                    == expectedContext,
                "Surgery actual fact did not publish exactly once: "
                    + delivery.Phase + ":" + delivery.Detail);

            SurgeryOrder owner = CreateSurgeryResolvedOwner(
                frozen,
                delivery);
            SurgerySaveValidation.ValidateObservationState(
                owner,
                SurgicalProcedureKind.Suture);
            SurgeryResolvedFactRestoreJoin.Validate(
                new[] { owner },
                ledger);
            saved = JsonConvert.DeserializeObject<
                SavedSurgeryResolvedAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedSurgeryResolvedAuthority
                        {
                            serializer = HostSerializer,
                            owner = SurgeryStateCloner.CloneOrder(owner),
                            ledger = ledger
                        }))
                ?? throw new InvalidOperationException(
                    "Surgery resolved host JSON returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.owner.resolutionPublicationKey == expectedKey
            && saved.owner.resolutionFactAccepted
            && saved.owner.resolutionFactDelivered
            && saved.owner.resolutionPublicationId.Length > 0
            && saved.owner.resolutionFingerprint.Length == 64,
            "Surgery host JSON lost its frozen owner proof.");

        VerifySurgeryCompletionObservation(saved);

        using (var restored = new SurgeryFactAuthority())
        {
            restored.RestoreLedger(saved.ledger);
            using IDisposable connection = restored.Connect();
            GameplayOutcomeLedgerSaveData freshLedger =
                restored.Ledger.CaptureGameplayOutcomes();
            SurgeryResolvedFactRestoreJoin.Validate(
                new[] { saved.owner },
                freshLedger);
            IReadOnlyDictionary<string, string> canonical =
                CanonicalHashes(freshLedger);
            var replayed = new DomainFactDelivery[1];
            Require(restored.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(
                            saved.owner.resolutionPublicationKey,
                            new SurgeryResolvedFact(
                                saved.owner.resolvedFact))
                    },
                    replayed,
                    out string replayFailure)
                && replayed[0].Completed
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1,
                "Surgery exact fresh-ledger replay was not idempotent: "
                    + replayFailure);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(
                    restored.Ledger.CaptureGameplayOutcomes()),
                "Surgery exact replay changed canonical evidence.");
        }

        RequireSurgeryJoinRejected(
            saved.owner,
            saved.ledger,
            value => value.resolvedFact.after.currentHealth += 1f,
            "Surgery join accepted changed anatomy.");
        RequireSurgeryJoinRejected(
            saved.owner,
            saved.ledger,
            value =>
            {
                value.completionAbsoluteDay++;
                value.resolvedFact.absoluteDay++;
            },
            "Surgery join accepted changed completion day.");
        RequireSurgeryJoinRejected(
            saved.owner,
            saved.ledger,
            value =>
            {
                value.facilityId = "building:surgery-host:other";
                value.resolvedFact.facilityId = value.facilityId;
            },
            "Surgery join accepted changed facility.");
        RequireSurgeryJoinRejected(
            saved.owner,
            saved.ledger,
            value =>
            {
                value.materials[0].quantity++;
                value.resolvedFact.materials[0].quantity++;
            },
            "Surgery join accepted changed material quantity.");
        RequireSurgeryJoinRejected(
            saved.owner,
            saved.ledger,
            value =>
            {
                value.resultOutcomeId = "miracle";
                value.resolvedFact.resultId = "miracle";
            },
            "Surgery join accepted changed result.");
        RequireSurgeryJoinRejected(
            saved.owner,
            saved.ledger,
            value => value.resolutionPublicationKey += ":wrong",
            "Surgery join accepted a changed opaque publication key.");
        RequireSurgeryJoinRejected(
            saved.owner,
            saved.ledger,
            value => value.resolutionPublicationId = "run:wrong/999",
            "Surgery join accepted a changed publication ID.");
        RequireSurgeryJoinRejected(
            saved.owner,
            saved.ledger,
            value => value.resolutionFingerprint = new string('0', 64),
            "Surgery join accepted a changed canonical fingerprint.");

        GameplayOutcomeLedgerSaveData retired =
            RetireSurgeryOutcome(saved.ledger);
        SurgeryResolvedFactRestoreJoin.Validate(
            new[] { saved.owner },
            retired);
        GameplayOutcomeLedgerSaveData changedTombstone =
            JsonConvert.DeserializeObject<GameplayOutcomeLedgerSaveData>(
                JsonConvert.SerializeObject(retired))
            ?? throw new InvalidOperationException(
                "Surgery tombstone host JSON returned null.");
        changedTombstone.tombstones.Single().canonicalHash =
            new string('f', 64);
        RequireSurgeryJoinRejected(
            saved.owner,
            changedTombstone,
            _ => { },
            "Surgery join accepted a changed tombstone proof.");
    }

    private static void VerifyHeritableTraitsAnalyzedFactObservation()
    {
        HeritableTraitsAnalyzedFactData source =
            CreateHeritableTraitsAnalyzedFactData();
        var fact = new HeritableTraitsAnalyzedFact(source);
        source.characterDisplayName = "변조된 분석 대상";
        source.sourceStackIds[0] = "stack:trait-analysis:changed";
        source.latentTraitIds[0] = "trait:latent:changed";
        HeritableTraitsAnalyzedFactData detached = fact.Snapshot();
        detached.sourceStackIds[1] =
            "stack:trait-analysis:detached";
        detached.latentTraitIds[1] =
            "trait:latent:detached";
        HeritableTraitsAnalyzedFactData frozen = fact.Snapshot();
        HeritableTraitAnalysisOutcomeReceipt receipt =
            HeritableTraitsAnalyzedFactMapping.Capture(fact);
        GameplayResultKey expectedKey =
            HeritableTraitsAnalyzedFactMapping.ResultKey(frozen);
        Require(frozen.characterDisplayName == "호스트 분석 대상"
            && frozen.sourceStackIds.SequenceEqual(new[]
            {
                "stack:trait-analysis:a",
                "stack:trait-analysis:b"
            }, StringComparer.Ordinal)
            && frozen.latentTraitIds.SequenceEqual(new[]
            {
                "trait:latent:arcane",
                "trait:latent:resilient"
            }, StringComparer.Ordinal)
            && receipt.ResultKey == expectedKey
            && receipt.OwnerRevision
                == checked((long)frozen.operationSequence + 1L)
            && receipt.CharacterId == frozen.characterId
            && receipt.CharacterDisplayName.DisplayText
                == frozen.characterDisplayName
            && receipt.FacilityInstanceId == frozen.facilityInstanceId
            && receipt.FacilityDefinitionId
                == frozen.facilityDefinitionId
            && receipt.KitItemId == frozen.kitItemId
            && receipt.PhysicalCommitId == frozen.physicalCommitId
            && receipt.SourceStackIds.SequenceEqual(
                frozen.sourceStackIds,
                StringComparer.Ordinal)
            && receipt.LatentTraitIds.SequenceEqual(
                frozen.latentTraitIds,
                StringComparer.Ordinal)
            && receipt.PhysicalInputMassGrams == frozen.inputMassGrams
            && receipt.AbsoluteDay == frozen.absoluteDay,
            "Heritable-trait analysis fact retained aliases or changed its native identity/count data.");

        SavedHeritableTraitAnalysisAuthority saved;
        using (var authority = new HeritableTraitAnalysisFactAuthority())
        {
            IDisposable connection = authority.Connect();
            RequireThrows<InvalidOperationException>(
                () => authority.Connect(),
                "Heritable-trait analysis observer allowed duplicate registration.");
            Require(authority.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Heritable-trait analysis fact did not reserve: "
                    + failure);
            string publicationKey = lease.PublicationKey;
            DomainFactDelivery delivery = authority.Dispatcher.Publish(
                lease,
                fact);
            GameplayOutcomeLedgerSaveData ledger =
                authority.Ledger.CaptureGameplayOutcomes();
            GameplayOutcomeSnapshot row = ledger.outbox
                .Concat(ledger.exactOutcomes)
                .Single();
            Require(delivery.Completed
                && publicationKey == expectedKey.ToString()
                && delivery.OperationKey == publicationKey
                && !string.IsNullOrWhiteSpace(delivery.PublicationId)
                && delivery.Fingerprint?.Length == 64
                && OutcomeRowCount(ledger) == 1
                && row.participants.Count == 1
                && row.subjects.Count == 1
                && row.metrics.Count == 4
                && row.tags.Count == 1
                && row.anchors.Count == 1
                && row.facts.Count == 6
                && row.metrics.Single(value => value.metricId
                        == HeritableTraitAnalysisOutcomeIds
                            .ConsumedKitCount.Value).value == 1d
                && row.metrics.Single(value => value.metricId
                        == HeritableTraitAnalysisOutcomeIds
                            .PhysicalInputMass.Value).value
                    == frozen.inputMassGrams
                && row.facts.Single(value => value.factId
                        == HeritableTraitAnalysisOutcomeIds
                            .SourceStackIds.Value).value
                    == string.Join(",", frozen.sourceStackIds)
                && row.facts.Single(value => value.factId
                        == HeritableTraitAnalysisOutcomeIds
                            .LatentTraitIds.Value).value
                    == string.Join(",", frozen.latentTraitIds),
                "Heritable-trait analysis observer changed the native shape, counts, or frozen evidence.");

            connection.Dispose();
            using IDisposable reconnected = authority.Connect();
            var duplicate = new DomainFactDelivery[1];
            Require(authority.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(publicationKey, fact)
                    },
                    duplicate,
                    out string duplicateFailure)
                && duplicate[0].Completed
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Heritable-trait analysis reconnect replay duplicated or rejected its row: "
                    + duplicateFailure);

            TraitAnalysisCommitSaveData pending =
                CreateTraitAnalysisPending(frozen);
            pending.phase = (int)TraitAnalysisCommitPhase.OutcomePublished;
            pending.publicationKey = publicationKey;
            pending.publicationId = delivery.PublicationId;
            pending.publicationFingerprint = delivery.Fingerprint;
            pending.ValidateObservationState();
            Require(pending.CreateCompletedFact().Snapshot()
                    .sourceStackIds.SequenceEqual(
                        frozen.sourceStackIds,
                        StringComparer.Ordinal),
                "Trait-analysis pending DTO did not reconstruct its frozen fact.");
            TraitAnalysisFactPreflight.ValidateObservation(pending, ledger);
            saved = JsonConvert.DeserializeObject<
                SavedHeritableTraitAnalysisAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedHeritableTraitAnalysisAuthority
                        {
                            serializer = HostSerializer,
                            pending = pending.Clone(),
                            ledger = ledger
                        }))
                ?? throw new InvalidOperationException(
                    "Heritable-trait analysis host JSON returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.pending.phase
                == (int)TraitAnalysisCommitPhase.OutcomePublished
            && saved.pending.publicationKey
                == HeritableTraitsAnalyzedFactMapping.ResultKey(
                    saved.pending.CreateCompletedFact().Snapshot())
                    .ToString()
            && saved.pending.publicationId.Length > 0
            && saved.pending.publicationFingerprint.Length == 64,
            "Heritable-trait analysis pending proof did not survive host JSON.");

        TraitAnalysisCommitSaveData unknownAck = saved.pending.Clone();
        unknownAck.phase = (int)TraitAnalysisCommitPhase.RevealPublished;
        unknownAck.publicationId = string.Empty;
        unknownAck.publicationFingerprint = string.Empty;
        unknownAck.ValidateObservationState();
        TraitAnalysisFactPreflight.ValidateObservation(
            unknownAck,
            saved.ledger);

        using (var restored = new HeritableTraitAnalysisFactAuthority())
        {
            restored.RestoreLedger(saved.ledger);
            using IDisposable connection = restored.Connect();
            TraitAnalysisFactPreflight.ValidateObservation(
                saved.pending,
                restored.Ledger.CaptureGameplayOutcomes());
            IReadOnlyDictionary<string, string> canonical =
                CanonicalHashes(
                    restored.Ledger.CaptureGameplayOutcomes());
            var replies = new DomainFactDelivery[1];
            Require(restored.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(
                            saved.pending.publicationKey,
                            saved.pending.CreateCompletedFact())
                    },
                    replies,
                    out string replayFailure)
                && replies[0].Completed
                && replies[0].OperationKey
                    == saved.pending.publicationKey
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1,
                "Heritable-trait analysis fresh-ledger replay did not reconcile exactly: "
                    + replayFailure);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(
                    restored.Ledger.CaptureGameplayOutcomes()),
                "Heritable-trait analysis replay changed canonical evidence.");
        }

        RequireTraitAnalysisPreflightRejected(
            saved,
            value => value.publicationKey += ":wrong",
            "Trait-analysis preflight accepted a changed opaque key.");
        RequireTraitAnalysisPreflightRejected(
            saved,
            value => value.publicationId = "run:wrong/999",
            "Trait-analysis preflight accepted a changed publication ID.");
        RequireTraitAnalysisPreflightRejected(
            saved,
            value => value.publicationFingerprint = new string('0', 64),
            "Trait-analysis preflight accepted a changed proof fingerprint.");
        RequireTraitAnalysisPreflightRejected(
            saved,
            value => value.absoluteDay++,
            "Trait-analysis preflight accepted a changed completion day.");
        RequireTraitAnalysisPreflightRejected(
            saved,
            value => value.characterDisplayName = "변조된 분석 대상",
            "Trait-analysis preflight accepted a changed character name.");
    }

    private static HeritableTraitsAnalyzedFactData
        CreateHeritableTraitsAnalyzedFactData() => new()
        {
            operationId = "trait-analysis:host:00000031",
            operationSequence = 31,
            characterId = "character:trait-analysis:host",
            characterDisplayName = "호스트 분석 대상",
            facilityInstanceId = "building:trait-analysis:host",
            facilityDefinitionId =
                "facility-definition:trait-analysis:host",
            kitItemId = "item:trait-analysis-kit:host",
            physicalCommitId = "physical-commit:trait-analysis:host",
            sourceStackIds = new[]
            {
                "stack:trait-analysis:a",
                "stack:trait-analysis:b"
            },
            inputMassGrams = 250L,
            latentTraitIds = new[]
            {
                "trait:latent:arcane",
                "trait:latent:resilient"
            },
            absoluteDay = 91
        };

    private static TraitAnalysisCommitSaveData CreateTraitAnalysisPending(
        HeritableTraitsAnalyzedFactData source) => new()
        {
            operationSequence = source.operationSequence,
            operationId = source.operationId,
            characterId = source.characterId,
            characterDisplayName = source.characterDisplayName,
            facilityInstanceId = source.facilityInstanceId,
            facilityDefinitionId = source.facilityDefinitionId,
            facilityDisplayName = "호스트 분석 시설",
            itemId = source.kitItemId,
            sourceStackId = source.sourceStackIds[0],
            absoluteDay = source.absoluteDay,
            latentTraitIds = source.latentTraitIds.ToList(),
            physicalCommitId = source.physicalCommitId,
            physicalSourceStackIds = source.sourceStackIds.ToList(),
            physicalInputMassGrams = source.inputMassGrams
        };

    private static void RequireTraitAnalysisPreflightRejected(
        SavedHeritableTraitAnalysisAuthority source,
        Action<TraitAnalysisCommitSaveData> mutate,
        string message)
    {
        TraitAnalysisCommitSaveData pending = source.pending.Clone();
        mutate(pending);
        RequireThrows<InvalidOperationException>(
            () => TraitAnalysisFactPreflight.ValidateObservation(
                pending,
                source.ledger),
            message);
    }

    private static void VerifyFacilityEvolutionCompletedFactObservation()
    {
        FacilityEvolutionCompletedFactData noMaterialData =
            CreateFacilityEvolutionFactData(
                "no-material",
                21,
                material: false);
        FacilityEvolutionCompletedFactData materialData =
            CreateFacilityEvolutionFactData(
                "material",
                22,
                material: true);
        var noMaterial = new FacilityEvolutionCompletedFact(
            noMaterialData);
        var material = new FacilityEvolutionCompletedFact(materialData);

        materialData.facilityDisplayName = "변조된 시설";
        materialData.sourceStackIds[0] =
            "stack:facility-evolution:changed";
        FacilityEvolutionCompletedFactData detached = material.Snapshot();
        detached.sourceStackIds[1] =
            "stack:facility-evolution:detached";
        FacilityEvolutionCompletedFactData frozen = material.Snapshot();
        FacilityEvolutionOutcomeReceipt noMaterialReceipt =
            FacilityEvolutionCompletedFactMapping.Capture(noMaterial);
        FacilityEvolutionOutcomeReceipt materialReceipt =
            FacilityEvolutionCompletedFactMapping.Capture(material);
        Require(frozen.facilityDisplayName == "호스트 진화 시설"
            && frozen.sourceStackIds.SequenceEqual(new[]
            {
                "stack:facility-evolution:a",
                "stack:facility-evolution:b"
            }, StringComparer.Ordinal)
            && !noMaterialReceipt.HasMaterialCommit
            && noMaterialReceipt.SourceStackIds.Count == 0
            && noMaterialReceipt.MaterialQuantity == 0
            && noMaterialReceipt.MaterialInputMassGrams == 0L
            && materialReceipt.HasMaterialCommit
            && materialReceipt.MaterialCommitId
                == "physical-commit:facility-evolution:material"
            && materialReceipt.SourceStackIds.SequenceEqual(
                frozen.sourceStackIds,
                StringComparer.Ordinal)
            && materialReceipt.MaterialQuantity == 3
            && materialReceipt.MaterialInputMassGrams == 1800L
            && materialReceipt.FacilityDisplayName.DisplayText
                == "호스트 진화 시설"
            && materialReceipt.ResultKey.ToString()
                == FacilityEvolutionCompletedFactMapping.ResultKey(frozen)
                    .ToString(),
            "Facility-evolution fact retained source aliases or blurred explicit material provenance.");

        SavedFacilityEvolutionAuthority saved;
        using (var authority = new FacilityEvolutionFactAuthority())
        {
            IDisposable connection = authority.Connect();
            RequireThrows<InvalidOperationException>(
                () => authority.Connect(),
                "Facility-evolution observer allowed duplicate registration.");
            Require(authority.Dispatcher.TryReserve(
                    noMaterial,
                    out DomainFactLease noMaterialLease,
                    out string noMaterialFailure),
                "Facility-evolution no-material fact did not reserve: "
                    + noMaterialFailure);
            string noMaterialKey = noMaterialLease.PublicationKey;
            DomainFactDelivery noMaterialDelivery =
                authority.Dispatcher.Publish(
                    noMaterialLease,
                    noMaterial);
            Require(authority.Dispatcher.TryReserve(
                    material,
                    out DomainFactLease materialLease,
                    out string materialFailure),
                "Facility-evolution material fact did not reserve: "
                    + materialFailure);
            string materialKey = materialLease.PublicationKey;
            DomainFactDelivery materialDelivery =
                authority.Dispatcher.Publish(materialLease, material);
            GameplayOutcomeLedgerSaveData ledger =
                authority.Ledger.CaptureGameplayOutcomes();
            GameplayOutcomeSnapshot noMaterialRow = ledger.outbox
                .Concat(ledger.exactOutcomes)
                .Single(value => value.operationId
                    == noMaterialReceipt.OperationId.Value);
            GameplayOutcomeSnapshot materialRow = ledger.outbox
                .Concat(ledger.exactOutcomes)
                .Single(value => value.operationId
                    == materialReceipt.OperationId.Value);
            Require(noMaterialDelivery.Completed
                && materialDelivery.Completed
                && noMaterialKey
                    == noMaterialReceipt.ResultKey.ToString()
                && materialKey == materialReceipt.ResultKey.ToString()
                && OutcomeRowCount(ledger) == 2
                && noMaterialRow.anchors.Count == 0
                && materialRow.anchors.Count == 2
                && noMaterialRow.metrics.Single(value => value.metricId
                        == EvolutionOutcomeIds.MaterialQuantityMetric.Value)
                    .value == 0d
                && materialRow.metrics.Single(value => value.metricId
                        == EvolutionOutcomeIds.MaterialQuantityMetric.Value)
                    .value == 3d
                && materialRow.metrics.Single(value => value.metricId
                        == EvolutionOutcomeIds.MaterialMassMetric.Value)
                    .value == 1800d
                && materialRow.facts.Any(value => value.factId
                        == EvolutionOutcomeIds.FacilityRecipeFact.Value
                    && value.value == frozen.recipeId)
                && materialRow.facts.Any(value => value.factId
                        == EvolutionOutcomeIds
                            .FacilityResultDefinitionFact.Value
                    && value.value
                        == frozen.resultFacilityDefinitionId),
                "Facility-evolution observer changed explicit material/no-material rows.");

            connection.Dispose();
            using IDisposable reconnected = authority.Connect();
            var duplicate = new DomainFactDelivery[1];
            Require(authority.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(
                            materialKey,
                            material)
                    },
                    duplicate,
                    out string duplicateFailure)
                && duplicate[0].Completed
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 2,
                "Facility-evolution reconnect replay duplicated or rejected the canonical row: "
                    + duplicateFailure);

            var pending = new FacilityEvolutionPendingMaterialCommitSnapshot
            {
                operationId = frozen.operationId,
                reasonCode = "facility-evolution:host",
                commitId = frozen.materialCommitId,
                sourceStackIds = frozen.sourceStackIds.ToArray(),
                quantity = frozen.materialQuantity,
                inputMassGrams = frozen.materialInputMassGrams,
                recipeId = frozen.recipeId,
                sourceFacilityPersistentId = frozen.facilityPersistentId,
                sourceFacilityDefinitionId =
                    "facility-definition:host:source",
                resultFacilityDefinitionId =
                    frozen.resultFacilityDefinitionId,
                historySequence = frozen.historySequence,
                phase = FacilityEvolutionMaterialCommitPhase.DomainApplied,
                completedFact = frozen.Clone(),
                publicationKey = materialKey,
                publicationId = materialDelivery.PublicationId,
                publicationFingerprint = materialDelivery.Fingerprint,
                observationAccepted = materialDelivery.MayBeCommitted,
                observationDelivered = materialDelivery.Completed
            };
            FacilityEvolutionFactPreflight.ValidateObservation(
                pending,
                authority.Ledger.CaptureGameplayOutcomes());
            saved = JsonConvert.DeserializeObject<
                SavedFacilityEvolutionAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedFacilityEvolutionAuthority
                        {
                            serializer = HostSerializer,
                            pending = pending.Clone(),
                            ledger = authority.Ledger
                                .CaptureGameplayOutcomes()
                        }))
                ?? throw new InvalidOperationException(
                    "Facility-evolution host JSON returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.pending.completedFact.facilityDisplayName
                == "호스트 진화 시설"
            && saved.pending.completedFact.sourceStackIds.Length == 2
            && saved.pending.publicationKey
                == FacilityEvolutionCompletedFactMapping.ResultKey(
                    saved.pending.completedFact).ToString()
            && saved.pending.observationAccepted
            && saved.pending.observationDelivered,
            "Facility-evolution pending observation lost frozen proof across host JSON.");

        FacilityEvolutionPendingMaterialCommitSnapshot unknownAck =
            saved.pending.Clone();
        unknownAck.publicationId = string.Empty;
        unknownAck.publicationFingerprint = string.Empty;
        unknownAck.observationAccepted = false;
        unknownAck.observationDelivered = false;
        FacilityEvolutionFactPreflight.ValidateObservation(
            unknownAck,
            saved.ledger);

        using (var restored = new FacilityEvolutionFactAuthority())
        {
            restored.RestoreLedger(saved.ledger);
            using IDisposable connection = restored.Connect();
            FacilityEvolutionFactPreflight.ValidateObservation(
                saved.pending,
                restored.Ledger.CaptureGameplayOutcomes());
            IReadOnlyDictionary<string, string> canonical =
                CanonicalHashes(
                    restored.Ledger.CaptureGameplayOutcomes());
            var replies = new DomainFactDelivery[1];
            Require(restored.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(
                            saved.pending.publicationKey,
                            new FacilityEvolutionCompletedFact(
                                saved.pending.completedFact))
                    },
                    replies,
                    out string replayFailure)
                && replies[0].Completed
                && replies[0].OperationKey
                    == saved.pending.publicationKey
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 2,
                "Facility-evolution fresh-ledger replay did not reconcile exactly: "
                    + replayFailure);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(
                    restored.Ledger.CaptureGameplayOutcomes()),
                "Facility-evolution fresh replay changed canonical evidence.");
        }

        RequireFacilityEvolutionPreflightRejected(
            saved,
            value => value.publicationKey += ":wrong",
            "Facility-evolution preflight accepted a changed opaque key.");
        RequireFacilityEvolutionPreflightRejected(
            saved,
            value => value.publicationId = "run:wrong/999",
            "Facility-evolution preflight accepted a changed publication ID.");
        RequireFacilityEvolutionPreflightRejected(
            saved,
            value => value.publicationFingerprint = new string('0', 64),
            "Facility-evolution preflight accepted a changed proof fingerprint.");
        RequireFacilityEvolutionPreflightRejected(
            saved,
            value => value.completedFact.absoluteDay++,
            "Facility-evolution preflight accepted a changed completion date.");
        RequireFacilityEvolutionPreflightRejected(
            saved,
            value => value.completedFact.facilityDisplayName =
                "변조된 진화 시설",
            "Facility-evolution preflight accepted a changed facility name.");
    }

    private static FacilityEvolutionCompletedFactData
        CreateFacilityEvolutionFactData(
            string suffix,
            int historySequence,
            bool material) => new()
        {
            operationId = "facility-evolution:host:" + suffix,
            historySequence = historySequence,
            facilityPersistentId =
                "building:facility-evolution:host:" + suffix,
            facilityDisplayName = "호스트 진화 시설",
            recipeId = "facility-evolution-recipe:host:" + suffix,
            resultFacilityDefinitionId =
                "facility-definition:host:evolved-" + suffix,
            resultStarGrade = material ? 4 : 3,
            materialCommitId = material
                ? "physical-commit:facility-evolution:" + suffix
                : string.Empty,
            sourceStackIds = material
                ? new[]
                {
                    "stack:facility-evolution:a",
                    "stack:facility-evolution:b"
                }
                : Array.Empty<string>(),
            materialQuantity = material ? 3 : 0,
            materialInputMassGrams = material ? 1800L : 0L,
            absoluteDay = 83
        };

    private static void RequireFacilityEvolutionPreflightRejected(
        SavedFacilityEvolutionAuthority source,
        Action<FacilityEvolutionPendingMaterialCommitSnapshot> mutate,
        string message)
    {
        FacilityEvolutionPendingMaterialCommitSnapshot pending =
            source.pending.Clone();
        mutate(pending);
        RequireThrows<InvalidOperationException>(
            () => FacilityEvolutionFactPreflight.ValidateObservation(
                pending,
                source.ledger),
            message);
    }

    private static void VerifySurgeryCompletionObservation(
        SavedSurgeryResolvedAuthority clinical)
    {
        SurgeryCompletionIdentityRestoreJoin.Validate(
            new[] { clinical.owner },
            Array.Empty<WorkCompletionIdentityDeliveryCursorSaveData>(),
            clinical.ledger);

        SurgeryResolvedFactData mutable =
            clinical.owner.resolvedFact.Clone();
        var fact = new SurgeryWorkCompletedFact(mutable);
        WorkCompletionIdentityDeliveryRequest request =
            SurgeryWorkCompletionFactMapping.Capture(fact);
        GameplayResultKey clinicalKey = SurgeryOutcomeIds.ResultKey(
            clinical.owner.orderId);
        GameplayResultKey completionKey =
            WorkCompletionIdentityOutcomeIds.ResultKey(request);
        mutable.doctorId = "character:surgery-host:changed-doctor";
        mutable.completedWork += 10f;
        Require(fact.OrderId == clinical.owner.orderId
            && fact.ProcedureId == clinical.owner.procedureId
            && fact.DoctorId == clinical.owner.completionDoctorId
            && fact.FacilityId == clinical.owner.facilityId
            && fact.ResultId == clinical.owner.resultOutcomeId
            && fact.Succeeded == clinical.owner.resultSucceeded
            && fact.Origin == clinical.owner.completionOrigin
            && fact.AbsoluteDay == clinical.owner.completionAbsoluteDay
            && fact.CompletedWork == clinical.owner.completedWork
            && request.Character.Value == clinical.owner.completionDoctorId
            && request.WorkId
                == "surgery:" + clinical.owner.procedureId
            && request.ProductId == clinical.owner.resultOutcomeId
            && request.OperationId == clinicalKey.OperationId.Value
            && request.TerminalOwnerId == clinicalKey.ProducerId
            && request.ChildResultKey == clinicalKey.ToString()
            && request.TargetEntityId == clinical.owner.facilityId
            && request.ContributionWorkUnits
                == clinical.owner.completedWork
            && request.AbsoluteDay
                == clinical.owner.completionAbsoluteDay,
            "Surgery completion fact or mapping retained mutable source state or changed its clinical child identity.");

        SavedSurgeryCompletionAuthority saved;
        using (var authority = new SurgeryFactAuthority())
        {
            authority.RestoreLedger(clinical.ledger);
            using IDisposable connection = authority.ConnectCompletion();
            Require(authority.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Surgery completion fact did not reserve: " + failure);
            string publicationKey = lease.PublicationKey;
            DomainFactDelivery delivery = authority.Dispatcher.Publish(
                lease,
                fact);
            GameplayOutcomeLedgerSaveData ledger =
                authority.Ledger.CaptureGameplayOutcomes();
            IReadOnlyList<WorkCompletionIdentityDeliveryCursorSaveData>
                deliveries = authority.CompletionDeliveries.Capture();
            GameplayOutcomeSnapshot identity = ledger.outbox
                .Concat(ledger.exactOutcomes)
                .Single(value => value.producerId
                    == WorkCompletionIdentityOutcomeIds.ProducerId);
            WorkCompletionIdentityDeliveryCursorSaveData cursor =
                deliveries.Single();
            Require(delivery.Completed
                && publicationKey == completionKey.ToString()
                && delivery.OperationKey == publicationKey
                && delivery.PublicationId == request.DeliveryId
                && delivery.Fingerprint == request.PayloadFingerprint
                && OutcomeRowCount(ledger) == 2
                && cursor.producerStreamId == request.ProducerStreamId
                && cursor.operationSequence == request.OperationSequence
                && cursor.deliveryId == request.DeliveryId
                && cursor.payloadFingerprint == request.PayloadFingerprint
                && cursor.disposition
                    == WorkCompletionIdentityDeliveryDisposition
                        .EffectsAppliedWithOutcome
                && cursor.notificationAttempted
                && identity.operationId == request.DeliveryId
                && identity.absoluteDay == request.AbsoluteDay
                && identity.facts.Any(value => value.factId
                        == WorkCompletionIdentityOutcomeIds
                            .ChildResultKeyFact.Value
                    && value.value == clinicalKey.ToString())
                && identity.provenance.Any(value => value.kindId
                        == "identity-payload-fingerprint"
                    && value.value == request.PayloadFingerprint),
                "Surgery completion observer lost its exact key, proof, cursor, or native identity row: "
                    + delivery.Phase + ":" + delivery.Detail);

            SurgeryOrder owner = SurgeryStateCloner.CloneOrder(
                clinical.owner);
            owner.completionPublicationKey = delivery.OperationKey;
            owner.completionPublicationId = delivery.PublicationId;
            owner.completionFingerprint = delivery.Fingerprint;
            owner.completionFactDelivered = delivery.Completed;
            SurgerySaveValidation.ValidateObservationState(
                owner,
                SurgicalProcedureKind.Suture);
            SurgeryCompletionIdentityRestoreJoin.Validate(
                new[] { owner },
                deliveries,
                ledger);
            saved = JsonConvert.DeserializeObject<
                SavedSurgeryCompletionAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedSurgeryCompletionAuthority
                        {
                            serializer = HostSerializer,
                            owner = SurgeryStateCloner.CloneOrder(owner),
                            deliveries = deliveries
                                .Select(value => value.Clone())
                                .ToList(),
                            ledger = ledger
                        }))
                ?? throw new InvalidOperationException(
                    "Surgery completion host JSON returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.owner.completionPublicationKey
                == completionKey.ToString()
            && saved.owner.completionPublicationId == request.DeliveryId
            && saved.owner.completionFingerprint
                == request.PayloadFingerprint
            && saved.owner.completionFactDelivered
            && saved.deliveries.Count == 1
            && saved.deliveries[0].notificationAttempted,
            "Surgery completion host JSON lost its owner proof or terminal cursor.");

        using (var restored = new SurgeryFactAuthority())
        {
            restored.RestoreLedger(saved.ledger);
            restored.RestoreCompletionDeliveries(saved.deliveries);
            using IDisposable connection = restored.ConnectCompletion();
            SurgeryCompletionIdentityRestoreJoin.Validate(
                new[] { saved.owner },
                restored.CompletionDeliveries.Capture(),
                restored.Ledger.CaptureGameplayOutcomes());
            IReadOnlyDictionary<string, string> canonical =
                CanonicalHashes(
                    restored.Ledger.CaptureGameplayOutcomes());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var replies = new DomainFactDelivery[1];
                Require(restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.owner.completionPublicationKey,
                                new SurgeryWorkCompletedFact(
                                    saved.owner.resolvedFact))
                        },
                        replies,
                        out string replayFailure)
                    && replies[0].Completed
                    && replies[0].OperationKey
                        == saved.owner.completionPublicationKey
                    && replies[0].PublicationId
                        == saved.owner.completionPublicationId
                    && replies[0].Fingerprint
                        == saved.owner.completionFingerprint
                    && OutcomeRowCount(
                        restored.Ledger.CaptureGameplayOutcomes()) == 2,
                    "Surgery completion exact replay attempt " + attempt
                        + " did not reconcile once: " + replayFailure);
            }
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(
                    restored.Ledger.CaptureGameplayOutcomes()),
                "Surgery completion exact replay changed canonical evidence.");
        }

        RequireSurgeryCompletionJoinRejected(
            saved,
            owner => owner.completionPublicationKey += ":wrong",
            _ => { },
            "Surgery completion join accepted a changed opaque key.");
        RequireSurgeryCompletionJoinRejected(
            saved,
            owner => owner.completionPublicationId =
                "work-identity-child:wrong",
            _ => { },
            "Surgery completion join accepted a changed publication ID.");
        RequireSurgeryCompletionJoinRejected(
            saved,
            owner => owner.completionFingerprint = new string('0', 64),
            _ => { },
            "Surgery completion join accepted a changed owner fingerprint.");
        RequireSurgeryCompletionJoinRejected(
            saved,
            _ => { },
            cursors => cursors[0].notificationAttempted = false,
            "Surgery completion join accepted an unattempted delivered notification.");
        RequireSurgeryCompletionJoinRejected(
            saved,
            _ => { },
            cursors => cursors[0].payloadFingerprint =
                new string('0', 64),
            "Surgery completion join accepted a changed cursor fingerprint.");
        RequireSurgeryCompletionJoinRejected(
            saved,
            owner =>
            {
                owner.completionPublicationKey = string.Empty;
                owner.completionPublicationId = string.Empty;
                owner.completionFingerprint = string.Empty;
                owner.completionFactDelivered = false;
            },
            _ => { },
            "Surgery completion join accepted a cursor without an owner key.");

        RequireSurgeryCompletionJoinRejected(
            saved,
            _ => { },
            cursors => cursors.Clear(),
            "Surgery completion join accepted delivered proof and identity outcome without its cursor.");

        SurgeryOrder pending = SurgeryStateCloner.CloneOrder(saved.owner);
        pending.completionPublicationId = string.Empty;
        pending.completionFingerprint = string.Empty;
        pending.completionFactDelivered = false;
        SurgerySaveValidation.ValidateObservationState(
            pending,
            SurgicalProcedureKind.Suture);
        SurgeryCompletionIdentityRestoreJoin.Validate(
            new[] { pending },
            saved.deliveries,
            saved.ledger);
    }

    private static void RequireSurgeryCompletionJoinRejected(
        SavedSurgeryCompletionAuthority source,
        Action<SurgeryOrder> mutateOwner,
        Action<List<WorkCompletionIdentityDeliveryCursorSaveData>>
            mutateDeliveries,
        string message)
    {
        SurgeryOrder owner = SurgeryStateCloner.CloneOrder(source.owner);
        var deliveries = source.deliveries
            .Select(value => value.Clone())
            .ToList();
        mutateOwner(owner);
        mutateDeliveries(deliveries);
        try
        {
            SurgeryCompletionIdentityRestoreJoin.Validate(
                new[] { owner },
                deliveries,
                source.ledger);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static SurgeryResolvedFactData CreateSurgeryResolvedFactData() =>
        new()
        {
            orderId = "surgery:7001",
            subjectId = "character:surgery-host:patient",
            subjectName = "호스트 수술 환자",
            subjectKind = SurgicalSubjectKind.Character,
            procedureId = "procedure:emergency-suture",
            procedureKind = SurgicalProcedureKind.Suture,
            doctorId = "character:surgery-host:doctor",
            facilityId = "building:surgery-host:operating-room",
            targetNodeId = "anatomy-node:torso",
            origin = CharacterCommandOrigin.DirectPlayerOrder,
            absoluteDay = 71,
            completedWork = 12.5f,
            resultId = "success",
            severity = SurgeryFailureSeverity.None,
            succeeded = true,
            materialsConsumed = true,
            processFluidConsumed = true,
            anesthesiaConsumed = true,
            materialSinkInputMassGrams = 250L,
            materials = new List<SurgicalMaterialRequirement>
            {
                new()
                {
                    itemId = "item:medical:gauze",
                    quantity = 2,
                    optional = false
                }
            },
            materialSinkOperationId =
                "surgery-material-sink:surgery:7001",
            materialSinkCommitId =
                "physical-commit:surgery-host",
            materialTerminalParentOperationId =
                "surgery-material-terminal:surgery:7001",
            materialTerminalCommitId =
                "physical-commit:surgery-host:terminal",
            before = CreateSurgeryAnatomyFact(70f, 0.12f, 8f),
            after = CreateSurgeryAnatomyFact(82f, 0.02f, 3f)
        };

    private static SurgeryAnatomyFactData CreateSurgeryAnatomyFact(
        float health,
        float bleeding,
        float infection) => new()
    {
        availability = SurgeryAnatomyAvailability.Available,
        profileId = "anatomy-profile:human",
        nodeId = "anatomy-node:torso",
        alive = true,
        currentHealth = health,
        maxHealth = 100f,
        bleeding = bleeding,
        infection = infection,
        installedPartId = string.Empty,
        installedPartKind = SurgicalPartKind.NaturalOrgan
    };

    private static SurgeryOrder CreateSurgeryResolvedOwner(
        SurgeryResolvedFactData frozen,
        DomainFactDelivery delivery) => new()
    {
        orderId = frozen.orderId,
        procedureId = frozen.procedureId,
        subject = new SurgicalSubjectRef
        {
            kind = frozen.subjectKind,
            subjectId = frozen.subjectId,
            displayName = frozen.subjectName
        },
        targetNodeId = frozen.targetNodeId,
        facilityId = frozen.facilityId,
        state = SurgeryOrderState.Recovering,
        requiredWork = frozen.completedWork,
        completedWork = frozen.completedWork,
        materialsConsumed = frozen.materialsConsumed,
        processFluidConsumed = frozen.processFluidConsumed,
        anesthesiaConsumed = frozen.anesthesiaConsumed,
        resultRolled = true,
        resultSucceeded = frozen.succeeded,
        resultOutcomeId = frozen.resultId,
        outcomeAnatomyBeforeCaptured = true,
        outcomeAnatomyBefore = frozen.before.Clone(),
        resolvedFact = frozen.Clone(),
        resolutionPublicationKey = delivery.OperationKey,
        resolutionPublicationId = delivery.PublicationId,
        resolutionFingerprint = delivery.Fingerprint,
        resolutionFactAccepted = delivery.MayBeCommitted,
        resolutionFactDelivered = delivery.Completed,
        completionOrigin = frozen.origin,
        completionDoctorId = frozen.doctorId,
        completionAbsoluteDay = frozen.absoluteDay,
        resolvedEffectCount = 0,
        failureSeverity = frozen.severity,
        materialSinkOperationId = frozen.materialSinkOperationId,
        materialSinkCommitId = frozen.materialSinkCommitId,
        materialSinkInputMassGrams = frozen.materialSinkInputMassGrams,
        materialTerminalParentOperationId =
            frozen.materialTerminalParentOperationId,
        materialTerminalCommitId = frozen.materialTerminalCommitId,
        replacementOperationId = frozen.replacementOperationId,
        replacementBatchCommitId = frozen.replacementBatchCommitId,
        replacementOutputStackId = frozen.replacementOutputStackId,
        replacementOutputItemInstanceId =
            frozen.replacementOutputItemInstanceId,
        materials = SurgeryResolvedFactData.CopyMaterials(
            frozen.materials)
    };

    private static void RequireSurgeryJoinRejected(
        SurgeryOrder source,
        GameplayOutcomeLedgerSaveData ledger,
        Action<SurgeryOrder> mutate,
        string message)
    {
        SurgeryOrder candidate = SurgeryStateCloner.CloneOrder(source);
        mutate(candidate);
        try
        {
            SurgeryResolvedFactRestoreJoin.Validate(
                new[] { candidate },
                ledger);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static GameplayOutcomeLedgerSaveData RetireSurgeryOutcome(
        GameplayOutcomeLedgerSaveData source)
    {
        GameplayOutcomeLedgerSaveData retired =
            JsonConvert.DeserializeObject<GameplayOutcomeLedgerSaveData>(
                JsonConvert.SerializeObject(source))
            ?? throw new InvalidOperationException(
                "Surgery ledger host JSON returned null.");
        GameplayOutcomeSnapshot row = retired.outbox
            .Concat(retired.exactOutcomes)
            .Single();
        retired.outbox.Clear();
        retired.exactOutcomes.Clear();
        retired.tombstones.Add(new GameplayOutcomeTombstoneSnapshot
        {
            runId = row.runId,
            sequence = row.sequence,
            producerId = row.producerId,
            operationId = row.operationId,
            commitRevision = row.commitRevision,
            localResultIndex = row.localResultIndex,
            ownerRevision = row.ownerRevision,
            temporalMetadataVersion =
                GameplayOutcomeDeliveryTime.CurrentVersion,
            absoluteDay = row.absoluteDay,
            parentRunId = row.parentRunId,
            parentSequence = row.parentSequence,
            deliveryMetadataVersion = row.deliveryMetadataVersion,
            deliveryClockId = row.deliveryClockId,
            recordedAbsoluteHour = row.recordedAbsoluteHour,
            publishedRevision = row.publishedRevision,
            deliveryFaultCount = row.deliveryFaultCount,
            deliveryDelayReason = row.deliveryDelayReason,
            terminalTier = NarrativeMemoryTier.Forgotten,
            canonicalHash = row.immutablePayloadHash
        });
        return retired;
    }

    private sealed class SavedSurgeryResolvedAuthority
    {
        public string serializer = string.Empty;
        public SurgeryOrder owner = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedFacilityEvolutionAuthority
    {
        public string serializer = string.Empty;
        public FacilityEvolutionPendingMaterialCommitSnapshot pending =
            new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedHeritableTraitAnalysisAuthority
    {
        public string serializer = string.Empty;
        public TraitAnalysisCommitSaveData pending = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class HeritableTraitAnalysisFactAuthority : IDisposable
    {
        public HeritableTraitAnalysisFactAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new HeritableTraitAnalysisOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new HeritableTraitAnalysisOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(
                    "run:trait-analysis-fact-host:"
                    + (++authoritySequence).ToString(
                        CultureInfo.InvariantCulture)),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            Binding = HeritableTraitsAnalyzedFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public DomainFactDispatcher Dispatcher { get; }
        public GameplayDomainFactObserverBinding<
            HeritableTraitsAnalyzedFact,
            HeritableTraitAnalysisOutcomeReceipt> Binding { get; }

        public IDisposable Connect() => Binding.Connect(Dispatcher);

        public void RestoreLedger(GameplayOutcomeLedgerSaveData source)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate =
                Ledger.PrepareGameplayOutcomeRestore(source);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose() => Dispatcher.Dispose();
    }

    private sealed class FacilityEvolutionFactAuthority : IDisposable
    {
        public FacilityEvolutionFactAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new FacilityEvolutionOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new FacilityEvolutionOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(
                    "run:facility-evolution-fact-host:"
                    + (++authoritySequence).ToString(
                        CultureInfo.InvariantCulture)),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            Binding = FacilityEvolutionCompletedFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public DomainFactDispatcher Dispatcher { get; }
        public GameplayDomainFactObserverBinding<
            FacilityEvolutionCompletedFact,
            FacilityEvolutionOutcomeReceipt> Binding { get; }

        public IDisposable Connect() => Binding.Connect(Dispatcher);

        public void RestoreLedger(GameplayOutcomeLedgerSaveData source)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate =
                Ledger.PrepareGameplayOutcomeRestore(source);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose() => Dispatcher.Dispose();
    }

    private sealed class SavedSurgeryCompletionAuthority
    {
        public string serializer = string.Empty;
        public SurgeryOrder owner = new();
        public List<WorkCompletionIdentityDeliveryCursorSaveData>
            deliveries = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SurgeryFactAuthority : IDisposable
    {
        public SurgeryFactAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new SurgeryOutcomeDescriptor(
                        new KoreanJosaFormatter()),
                    new WorkCompletionIdentityOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new SurgeryOutcomeAdapter(),
                    new WorkCompletionIdentityOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(
                    "run:surgery-fact-host:"
                    + (++authoritySequence).ToString(
                        CultureInfo.InvariantCulture)),
                1L);
            Events = new GameEventBus();
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                Events);
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            Binding = SurgeryResolvedFactMapping.CreateBinding(
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            CompletionDeliveries =
                new WorkCompletionIdentityDeliveryLedger();
            CompletionBridge =
                new WorkCompletionIdentityGameplayOutcomeBridge(
                    Recorder,
                    Ledger,
                    new SurgeryCompletionDisplayNames(),
                    Ledger);
            CompletionCommand = new SurgeryCompletionHostCommand(
                CompletionBridge,
                CompletionDeliveries);
            CompletionBinding =
                SurgeryWorkCompletionFactMapping.CreateBinding(
                    CompletionCommand,
                    CompletionDeliveries,
                    new CharacterIdentityEventPublisher(Events));
        }

        public GameEventBus Events { get; }
        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public DomainFactDispatcher Dispatcher { get; }
        public GameplayDomainFactObserverBinding<
            SurgeryResolvedFact,
            SurgeryOutcomeReceipt> Binding { get; }
        public WorkCompletionIdentityDeliveryLedger
            CompletionDeliveries { get; }
        public WorkCompletionIdentityGameplayOutcomeBridge
            CompletionBridge { get; }
        public IWorkCompletionIdentityDeliveryCommand CompletionCommand
            { get; }
        public WorkCompletionDomainFactBinding<SurgeryWorkCompletedFact>
            CompletionBinding { get; }

        public IDisposable Connect() => Binding.Connect(Dispatcher);
        public IDisposable ConnectCompletion() =>
            CompletionBinding.Connect(Dispatcher);

        public void RestoreCompletionDeliveries(
            IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData>
                source) => CompletionDeliveries.Restore(source);

        public void RestoreLedger(GameplayOutcomeLedgerSaveData source)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate =
                Ledger.PrepareGameplayOutcomeRestore(source);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose() => Dispatcher.Dispose();
    }

    private sealed class SurgeryCompletionHostCommand :
        IWorkCompletionIdentityDeliveryCommand
    {
        private readonly WorkCompletionIdentityGameplayOutcomeBridge bridge;
        private readonly WorkCompletionIdentityDeliveryLedger deliveries;

        public SurgeryCompletionHostCommand(
            WorkCompletionIdentityGameplayOutcomeBridge bridge,
            WorkCompletionIdentityDeliveryLedger deliveries)
        {
            this.bridge = bridge
                ?? throw new ArgumentNullException(nameof(bridge));
            this.deliveries = deliveries
                ?? throw new ArgumentNullException(nameof(deliveries));
        }

        public WorkCompletionIdentityDeliveryResult EnsureApplied(
            WorkCompletionIdentityDeliveryRequest request)
        {
            WorkCompletionIdentityDeliveryStatus status =
                deliveries.Inspect(request, out string failure);
            if (status == WorkCompletionIdentityDeliveryStatus.Conflict)
                return new(status, failure);
            if (status
                == WorkCompletionIdentityDeliveryStatus.AlreadyApplied)
            {
                OwnerOutcomeCommitResult existing =
                    bridge.Reconcile(request);
                return existing.DurablyCommitted
                    ? new WorkCompletionIdentityDeliveryResult(
                        WorkCompletionIdentityDeliveryStatus
                            .AlreadyApplied)
                    : new WorkCompletionIdentityDeliveryResult(
                        WorkCompletionIdentityDeliveryStatus.Conflict,
                        existing.DetailCode);
            }

            deliveries.BeginApply(request);
            PreparedWorkCompletionIdentityOutcome prepared = default;
            bool preparedValid = false;
            try
            {
                if (!bridge.TryPrepare(
                        request,
                        out prepared,
                        out bool capacityDeferred,
                        out failure))
                {
                    return new WorkCompletionIdentityDeliveryResult(
                        capacityDeferred
                            ? WorkCompletionIdentityDeliveryStatus
                                .Deferred
                            : WorkCompletionIdentityDeliveryStatus
                                .Conflict,
                        failure);
                }
                preparedValid = true;
                OwnerOutcomeCommitResult committed =
                    bridge.Commit(prepared);
                if (!committed.DurablyCommitted)
                {
                    bridge.Cancel(prepared);
                    preparedValid = false;
                    return new WorkCompletionIdentityDeliveryResult(
                        WorkCompletionIdentityDeliveryStatus.Conflict,
                        committed.DetailCode);
                }
                preparedValid = false;
                WorkCompletionIdentityDeliveryStatus committedCursor =
                    deliveries.Commit(
                        request,
                        out failure,
                        WorkCompletionIdentityDeliveryDisposition
                            .EffectsAppliedWithOutcome);
                return committedCursor
                    == WorkCompletionIdentityDeliveryStatus.Applied
                    ? new WorkCompletionIdentityDeliveryResult(
                        committedCursor)
                    : new WorkCompletionIdentityDeliveryResult(
                        WorkCompletionIdentityDeliveryStatus.Conflict,
                        failure);
            }
            catch
            {
                if (preparedValid)
                    bridge.Cancel(prepared);
                throw;
            }
            finally
            {
                deliveries.EndApply(request);
            }
        }

        public bool RetireProducerStream(string producerStreamId) =>
            deliveries.RetireProducerStream(producerStreamId);
    }

    private sealed class SurgeryCompletionDisplayNames :
        IGameplayOutcomeDisplayNameQuery
    {
        public bool TryGetCurrentName(
            GameplayEntityId entityId,
            out KoreanNameSnapshot name)
        {
            if (entityId.Kind.Equals(
                    WorkCompletionIdentityOutcomeIds.CharacterKind)
                && string.Equals(
                    entityId.Value,
                    "character:surgery-host:doctor",
                    StringComparison.Ordinal))
            {
                name = new KoreanNameSnapshot(
                    "호스트 수술의",
                    "surgery-completion-host-doctor-v1",
                    KoreanPronunciationHint.AutoHangulDisplay(
                        "surgery-completion-host-doctor-pronunciation-v1"),
                    "ko-KR");
                return true;
            }
            name = default;
            return false;
        }
    }

    private static void VerifyWorkExecutionActivityObserver()
    {
        var covered = new HashSet<WorkExecutionTransition>();
        var character = new CharacterId("character:work-activity-host");
        var workType = new WorkTypeId("work:host-observer");

        Verify(WorkExecutionTransition.MovementUnavailable,
            CharacterActivityOutcomes.Failed,
            "작업 실패: 이동 정보 없음", "missing-movement", bubble: true);
        Verify(WorkExecutionTransition.ApproachProtectionUnavailable,
            CharacterActivityOutcomes.Blocked,
            "이동 전 환경 보호 의복을 준비할 수 없어 작업을 시작하지 못했습니다.",
            "environment-apparel-approach-blocked", bubble: true);
        Verify(WorkExecutionTransition.ApproachedOrderReplaced,
            CharacterActivityOutcomes.Failed,
            "작업 실패: 이동 중 작업 주문이 취소되거나 교체되었습니다.",
            "work-order-invalidated-during-approach", bubble: true);
        Verify(WorkExecutionTransition.WorkProtectionUnavailable,
            CharacterActivityOutcomes.Blocked,
            "환경 보호 의복을 준비할 수 없어 작업을 시작하지 못했습니다.",
            "environment-apparel-work-start-blocked", bubble: true);
        Verify(WorkExecutionTransition.PaidFacilityRejected,
            CharacterActivityOutcomes.Blocked,
            "호스트 작업 중단: 호스트 사유", "paid-facility-order",
            bubble: true);
        Verify(WorkExecutionTransition.RoutineNeedSuspended,
            CharacterActivityOutcomes.Changed,
            "생리 욕구 해결 후 재개하도록 작업 진행 상태 보존",
            "routine-need-suspended");
        Verify(WorkExecutionTransition.TargetUnreachable,
            CharacterActivityOutcomes.Failed,
            "작업 실패: 작업 도달 실패", "target-unreachable", bubble: true);
        Verify(WorkExecutionTransition.RestockTargetUnsupported,
            CharacterActivityOutcomes.Failed,
            "보충 실패: 재고를 받을 수 없는 시설", "target-not-restockable",
            bubble: true);
        Verify(WorkExecutionTransition.RestockPlanRejected,
            CharacterActivityOutcomes.Failed,
            "보충 실패: 호스트 사유", "호스트 사유", bubble: true);
        Verify(WorkExecutionTransition.RestockSourceApproach,
            CharacterActivityOutcomes.Progress,
            "보충 이동: 호스트 창고 -> 호스트 상점", "moving-to-stock");
        Verify(WorkExecutionTransition.RestockLoadProgress,
            CharacterActivityOutcomes.Progress,
            "보충 적재: 호스트 자재 3/5", "loading-stock", quantity: 3);
        Verify(WorkExecutionTransition.RestockStockUnavailable,
            CharacterActivityOutcomes.Failed,
            "보충 실패: 창고 재고 부족", "warehouse-stock-shortage",
            bubble: true);
        Verify(WorkExecutionTransition.RestockDeliveryPathUnavailable,
            CharacterActivityOutcomes.Blocked,
            "보충 실패: 상점 경로 없음", "shop-path-missing", bubble: true);
        Verify(WorkExecutionTransition.RestockFinished,
            CharacterActivityOutcomes.Completed,
            "보충 완료: 호스트 상점 호스트 사유", "호스트 사유",
            quantity: 3);
        Verify(WorkExecutionTransition.ConstructionWorkforceRejected,
            CharacterActivityOutcomes.Blocked,
            "호스트 작업 중단: 호스트 사유", "construction-workforce-blocked",
            bubble: true);
        Verify(WorkExecutionTransition.MaterialsPending,
            CharacterActivityOutcomes.Blocked,
            "호스트 작업 대기: 재료가 아직 도착하지 않음",
            "waiting-for-materials", value: 0.375f);
        Verify(WorkExecutionTransition.WorkOrderRejected,
            CharacterActivityOutcomes.Blocked,
            "호스트 작업 중단: 호스트 사유", "work-order-blocked",
            bubble: true);
        Verify(WorkExecutionTransition.ProgressAdvanced,
            CharacterActivityOutcomes.Progress,
            "호스트 작업 진행 38%", "work-progress", value: 0.375f);
        Verify(WorkExecutionTransition.WorkOrderCompleted,
            CharacterActivityOutcomes.Completed,
            "호스트 작업 완료", "work-order-completed", value: 1f);
        Verify(WorkExecutionTransition.PersistentProgressAdvanced,
            CharacterActivityOutcomes.Progress,
            "호스트 작업 진행 38%", "persistent-work-progress",
            value: 0.375f);
        Verify(WorkExecutionTransition.RoutineNeedInterrupted,
            CharacterActivityOutcomes.Changed,
            "생리 욕구로 작업 중단: 호스트 사유", "routine-need-interrupt",
            hasTarget: false, kind: CharacterActivityKinds.Duty);
        Verify(WorkExecutionTransition.EnvironmentEvacuation,
            CharacterActivityOutcomes.Blocked,
            "호스트 사유", "environment-evacuation", bubble: true);
        Verify(WorkExecutionTransition.EnvironmentReassignment,
            CharacterActivityOutcomes.Blocked,
            "호스트 사유", "environment-reassignment", bubble: true);
        Verify(WorkExecutionTransition.EmergencySuspended,
            CharacterActivityOutcomes.Changed,
            "비상 대응을 위해 진행 상태를 보존하고 작업을 일시중단했습니다.",
            "suspended-for-emergency");
        Verify(WorkExecutionTransition.WorkplaceUnavailable,
            CharacterActivityOutcomes.Failed,
            "작업 실패: 작업장 없음", "no-workplace", bubble: true);
        Verify(WorkExecutionTransition.PriorityChanged,
            CharacterActivityOutcomes.Changed,
            "호스트 작업 우선순위: 2", "priority:2", value: 2f,
            hasTarget: false);
        Verify(WorkExecutionTransition.Cancelled,
            CharacterActivityOutcomes.Cancelled,
            "작업 종료: 호스트 사유", "호스트 사유");

        Require(covered.SetEquals(
                Enum.GetValues(typeof(WorkExecutionTransition))
                    .Cast<WorkExecutionTransition>()),
            "Work-execution projection did not cover every transition.");

        WorkExecutionChangedEvent failedRestock = Create(
            WorkExecutionTransition.RestockFinished,
            quantity: 0);
        CharacterActivityEvent failedProjection =
            WorkExecutionActivityObserver.Project(failedRestock);
        Require(failedProjection.OutcomeId == CharacterActivityOutcomes.Failed
            && failedProjection.FactText == "보충 실패: 호스트 사유"
            && failedProjection.ReasonCode == "호스트 사유"
            && failedProjection.Quantity == 0
            && failedProjection.BubbleEligible
            && failedProjection.Sentiment == -0.7f,
            "Work-execution failed restock projection changed legacy fields.");

        var events = new GameEventBus();
        var world = new EmptyCharacterAiWorldRegistry();
        var observer = new WorkExecutionActivityObserver(events, world);
        observer.Initialize();
        observer.Initialize();
        string missingActorFailure = string.Empty;
        try
        {
            events.Publish(Create(WorkExecutionTransition.ProgressAdvanced));
        }
        catch (InvalidOperationException exception)
        {
            missingActorFailure = exception.Message;
        }
        Require(missingActorFailure
                == "work-execution-activity-character-missing:"
                    + character.Value,
            "Work-execution observer hid a missing registered actor.");
        Require(world.LifetimeCharacterReadCount == 1,
            "Work-execution observer initialized more than one subscription.");
        observer.Dispose();
        observer.Dispose();
        events.Publish(Create(WorkExecutionTransition.ProgressAdvanced));
        Require(world.LifetimeCharacterReadCount == 1,
            "Disposed work-execution observer remained subscribed.");

        void Verify(
            WorkExecutionTransition transition,
            string outcome,
            string text,
            string reason,
            bool bubble = false,
            float value = 0f,
            int quantity = 0,
            bool hasTarget = true,
            string kind = CharacterActivityKinds.Work)
        {
            Require(covered.Add(transition),
                "Work-execution transition was asserted twice: " + transition);
            CharacterActivityEvent activity =
                WorkExecutionActivityObserver.Project(Create(transition));
            float sentiment = outcome == CharacterActivityOutcomes.Completed
                ? 0.45f
                : outcome == CharacterActivityOutcomes.Failed
                    || outcome == CharacterActivityOutcomes.Blocked
                    ? -0.7f
                    : 0f;
            bool isWork = kind == CharacterActivityKinds.Work;
            Require(activity.KindId == kind
                && activity.OutcomeId == outcome
                && activity.FactText == text
                && activity.ReasonCode == reason
                && activity.ActionId == (isWork ? workType.Value : string.Empty)
                && activity.TargetId == (hasTarget ? "building:work-activity-host" : string.Empty)
                && activity.TargetName == (hasTarget ? "호스트 대상" : string.Empty)
                && activity.FacilityId == (hasTarget ? 77 : -1)
                && activity.Value == value
                && activity.Quantity == quantity
                && activity.Sentiment == sentiment
                && activity.BubbleEligible == bubble
                && activity.NarrativeEligible
                && activity.VisibleToPlayer,
                "Work-execution projection changed legacy fields: " + transition);
        }

        WorkExecutionChangedEvent Create(
            WorkExecutionTransition transition,
            int quantity = 3) => new(
                transition,
                character,
                workType,
                targetId: "building:work-activity-host",
                targetName: "호스트 대상",
                targetDefinitionId: 77,
                workLabel: "호스트 작업",
                detail: "호스트 사유",
                sourceName: "호스트 창고",
                itemName: "호스트 자재",
                destinationName: "호스트 상점",
                progress: 0.375f,
                quantity: quantity,
                requestedQuantity: 5,
                priority: WorkPriorityLevel.Priority2,
                absoluteDay: 17,
                origin: CharacterCommandOrigin.DirectPlayerOrder);
    }

    private static void VerifyWorkControlActivityObserver()
    {
        const string targetId = "target:work-control-host";
        const string targetName = "호스트 표시 대상";
        const string targetRuntimeName = "HostRuntimeTarget";
        const string workLabel = "호스트 제작";
        const int targetDefinitionId = 81;
        var character = new CharacterId("character:work-control-host");
        var workType = new WorkTypeId("work:host-control");
        var covered = new HashSet<WorkControlTransition>();

        Verify(Create(WorkControlTransition.PriorityWorkAssigned,
                workType, target: true, label: workLabel),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Changed,
            "우선 작업 지정: HostRuntimeTarget - 호스트 제작",
            workType.Value, "priority-command", target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId);
        Verify(Create(WorkControlTransition.PrioritySuppressionAssigned,
                target: true),
            CharacterActivityKinds.Command, CharacterActivityOutcomes.Changed,
            "우선 제압 지정: HostRuntimeTarget", "command:suppress",
            target: true, projectedTargetName: targetRuntimeName);
        Verify(Create(WorkControlTransition.SuppressionRejected,
                target: true, detail: "호스트 거절"),
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Failed,
            "제압 실패: 호스트 거절", "combat:suppress", "호스트 거절",
            target: true, projectedTargetName: targetRuntimeName,
            sentiment: -0.7f, bubble: true);
        Verify(Create(WorkControlTransition.SuppressionMovementUnavailable),
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Failed,
            "제압 실패: 이동 정보 없음", "combat:suppress", "missing-movement",
            sentiment: -0.7f, bubble: true);
        Verify(Create(WorkControlTransition.InterceptionRejected,
                target: true, detail: null),
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Failed,
            "저지 실패: 방어 지휘 체계 없음", "combat:suppress",
            target: true, projectedTargetName: targetName,
            sentiment: -0.7f, bubble: true);
        Verify(Create(WorkControlTransition.InterceptionRejected,
                target: true, detail: "호스트 방어 거절"),
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Failed,
            "저지 실패: 호스트 방어 거절", "combat:suppress", "호스트 방어 거절",
            target: true, projectedTargetName: targetName,
            sentiment: -0.7f, bubble: true, firstVariant: false);
        Verify(Create(WorkControlTransition.InterceptionStarted, target: true),
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Started,
            "호스트 표시 대상 저지하러 이동", "defense:manual-intercept",
            target: true, projectedTargetName: targetName);
        Verify(Create(WorkControlTransition.SuppressionStarted, target: true),
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Started,
            "제압 시작: HostRuntimeTarget", "combat:suppress",
            target: true, projectedTargetName: targetRuntimeName);
        Verify(Create(WorkControlTransition.SuppressionTargetUnreachable,
                target: true),
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Blocked,
            "제압 실패: 도달할 수 없는 대상", "combat:suppress",
            "target-unreachable", target: true,
            projectedTargetName: targetRuntimeName,
            sentiment: -0.7f, bubble: true);
        Verify(Create(WorkControlTransition.SuppressionApplied,
                target: true, amount: 4.5f, state: false),
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Progress,
            "저항을 누르는 중: HostRuntimeTarget", "combat:suppress",
            target: true, projectedTargetName: targetRuntimeName,
            value: 4.5f, sentiment: -0.3f);
        Verify(Create(WorkControlTransition.SuppressionApplied,
                target: true, amount: 4.5f, state: true),
            CharacterActivityKinds.Combat, CharacterActivityOutcomes.Completed,
            "제압 완료: HostRuntimeTarget", "combat:suppress",
            target: true, projectedTargetName: targetRuntimeName,
            value: 4.5f, sentiment: 0.2f, firstVariant: false);
        Verify(Create(WorkControlTransition.DutySuppressionResumed),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Responded,
            "비번 중 제압 명령 대응", "command:suppress");
        Verify(Create(WorkControlTransition.DutyPriorityWorkResumed),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Responded,
            "비번 중 우선 작업 대응", "command:priority-work");
        Verify(Create(WorkControlTransition.DutyConditionRecovered),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Returned,
            "근무 복귀", reason: "condition-recovered", sentiment: 0.3f);
        Verify(Create(WorkControlTransition.DutyOffDutyStarted, detail: null),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Departed,
            "비번 시작", reason: string.Empty, sentiment: -0.2f);
        Verify(Create(WorkControlTransition.DutyOffDutyStarted,
                detail: "호스트 피로"),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Departed,
            "비번 시작: 호스트 피로", reason: "호스트 피로",
            sentiment: -0.2f, firstVariant: false);
        Verify(Create(WorkControlTransition.DutyExpeditionPrepared),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Changed,
            "원정 준비: 작업 해제", reason: "expedition-preparation");
        Verify(Create(WorkControlTransition.DutyShiftEnded,
                detail: "호스트 교대 종료"),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Changed,
            "근무 교대: 호스트 교대 종료", reason: "호스트 교대 종료");
        Verify(Create(WorkControlTransition.DutyRoutineNeedResumed,
                workType, target: true),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Returned,
            "생리 욕구를 해결해 중단했던 작업으로 복귀 준비",
            workType.Value, "routine-need-resume", target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId,
            sentiment: 0.45f);
        Verify(Create(WorkControlTransition.SelectionDutyBlocked, state: false),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Blocked,
            "작업 보류: 피로/기분 보호", reason: "wellbeing-protection",
            sentiment: -0.2f, bubble: true);
        Verify(Create(WorkControlTransition.SelectionDutyBlocked, state: true),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Blocked,
            "작업 보류: 비번", reason: "off-duty",
            sentiment: -0.2f, bubble: true, firstVariant: false);
        Verify(Create(WorkControlTransition.SelectionUrgentDutyResumed),
            CharacterActivityKinds.Duty, CharacterActivityOutcomes.Responded,
            "비번 중 긴급 작업 합류", reason: "urgent-work");
        Verify(Create(WorkControlTransition.SelectionPriorityTargetCancelled,
                workType, target: true),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Cancelled,
            "우선 작업 취소: 대상 사용 불가", workType.Value,
            "target-unavailable", target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId,
            bubble: true);
        VerifyRepair(WorkControlTransition.RepairCompleted,
            CharacterActivityOutcomes.Completed,
            "수리 완료: HostRuntimeTarget");
        VerifyRepair(WorkControlTransition.StructuralRepairCompleted,
            CharacterActivityOutcomes.Completed,
            "구조 수리 완료: HostRuntimeTarget",
            "structural-repair-completed", sentiment: 0.45f, bubble: true);
        VerifyRepair(WorkControlTransition.EquipmentRepairRejected,
            CharacterActivityOutcomes.Blocked,
            "장비 수리 중단: 호스트 장비 거절",
            "equipment-repair-blocked", detail: "호스트 장비 거절",
            sentiment: -0.7f, bubble: true);
        VerifyRepair(WorkControlTransition.EquipmentRepairCompleted,
            CharacterActivityOutcomes.Completed,
            "호스트 장비 수리 완료", "equipment-repair-completed",
            detail: "호스트 장비 수리 완료", sentiment: 0.45f,
            bubble: true);
        VerifyRepair(WorkControlTransition.AutomationMaintenanceRejected,
            CharacterActivityOutcomes.Blocked,
            "자동화 정비 중단: automation-fault",
            "automation-maintenance-blocked", detail: "automation-fault",
            sentiment: -0.7f, bubble: true);
        VerifyRepair(WorkControlTransition.AutomationMaintenanceCompleted,
            CharacterActivityOutcomes.Completed,
            "자동화 정비 완료: HostRuntimeTarget",
            "automation-maintenance-completed", sentiment: 0.45f,
            bubble: true);
        Verify(Create(WorkControlTransition.AssignedWorkStarted,
                workType, target: true, label: workLabel),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Started,
            "작업 시작 · 호스트 제작 · 호스트 표시 대상",
            workType.Value, target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId);
        Verify(Create(WorkControlTransition.AssignedWorkStopped,
                workType, target: true, label: workLabel, detail: null),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Completed,
            "작업 종료 · 호스트 제작 · 호스트 표시 대상",
            workType.Value, target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId,
            sentiment: 0.45f);
        Verify(Create(WorkControlTransition.AssignedWorkStopped,
                workType, target: true, label: workLabel, detail: string.Empty),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Completed,
            "작업 종료 · 호스트 제작 · 호스트 표시 대상",
            workType.Value, target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId,
            sentiment: 0.45f, firstVariant: false);
        Verify(Create(WorkControlTransition.AssignedWorkStopped,
                workType, target: true, label: workLabel, detail: "   "),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Completed,
            "작업 종료 · 호스트 제작 · 호스트 표시 대상",
            workType.Value, target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId,
            sentiment: 0.45f, firstVariant: false);
        Verify(Create(WorkControlTransition.AssignedWorkStopped,
                workType, target: true, label: workLabel,
                detail: "호스트 작업 종료"),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Completed,
            "작업 종료 · 호스트 제작 · 호스트 표시 대상 · 호스트 작업 종료",
            workType.Value, "호스트 작업 종료", target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId,
            sentiment: 0.45f, firstVariant: false);
        Verify(Create(WorkControlTransition.WorkAmountFinished,
                workType, target: true, label: workLabel),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Completed,
            "작업 종료 · 호스트 제작 · 호스트 표시 대상 · 작업량 완료",
            workType.Value, "작업량 완료", target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId,
            sentiment: 0.45f);
        Verify(Create(WorkControlTransition.RestockRunFinished,
                workType, target: true, label: workLabel),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Completed,
            "작업 종료 · 호스트 제작 · 호스트 표시 대상 · 보충 완료",
            workType.Value, "보충 완료", target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId,
            sentiment: 0.45f);
        Verify(Create(WorkControlTransition.RoutineShiftFinished,
                workType, target: true, label: workLabel,
                detail: "호스트 교대 종료"),
            CharacterActivityKinds.Work, CharacterActivityOutcomes.Completed,
            "작업 종료 · 호스트 제작 · 호스트 표시 대상 · 근무 교대 · 호스트 교대 종료",
            workType.Value, "근무 교대 · 호스트 교대 종료", target: true,
            projectedTargetName: targetName, facilityId: targetDefinitionId,
            sentiment: 0.45f);

        Require(covered.SetEquals(
                Enum.GetValues(typeof(WorkControlTransition))
                    .Cast<WorkControlTransition>()),
            "Work-control projection did not cover every transition.");
        RequireThrows<ArgumentException>(
            () => new WorkControlChangedEvent(
                WorkControlTransition.DutyExpeditionPrepared,
                default),
            "Work-control event accepted an invalid character.");
        RequireThrows<ArgumentException>(
            () => new WorkControlChangedEvent(
                (WorkControlTransition)int.MaxValue,
                character),
            "Work-control event accepted an undefined transition.");
        RequireThrows<ArgumentException>(
            () => new WorkControlChangedEvent(
                WorkControlTransition.SuppressionApplied,
                character,
                amount: float.NaN),
            "Work-control event accepted a non-finite amount.");
        RequireThrows<ArgumentException>(
            () => new WorkControlChangedEvent(
                WorkControlTransition.SuppressionApplied,
                character,
                amount: -0.01f),
            "Work-control event accepted a negative amount.");

        var events = new GameEventBus();
        var world = new EmptyCharacterAiWorldRegistry();
        var observer = new WorkControlActivityObserver(events, world);
        WorkControlChangedEvent published = Create(
            WorkControlTransition.DutyExpeditionPrepared);
        observer.Initialize();
        observer.Initialize();
        Require(PublishMissingActor()
                == "work-control-activity-character-missing:" + character.Value
            && world.LifetimeCharacterReadCount == 1,
            "Work-control observer hid a missing actor or subscribed twice.");
        observer.Dispose();
        observer.Dispose();
        events.Publish(published);
        Require(world.LifetimeCharacterReadCount == 1,
            "Disposed work-control observer remained subscribed.");
        observer.Initialize();
        observer.Initialize();
        Require(PublishMissingActor()
                == "work-control-activity-character-missing:" + character.Value
            && world.LifetimeCharacterReadCount == 2,
            "Work-control observer did not reinitialize exactly once.");
        observer.Dispose();

        WorkControlChangedEvent Create(
            WorkControlTransition transition,
            WorkTypeId selectedWorkType = default,
            bool target = false,
            string label = "",
            string detail = null,
            float amount = 0f,
            bool state = false) => new(
                transition,
                character,
                selectedWorkType,
                target ? targetId : string.Empty,
                target ? targetName : string.Empty,
                target && selectedWorkType.IsValid
                    ? targetDefinitionId
                    : -1,
                target ? targetRuntimeName : string.Empty,
                label,
                detail,
                amount,
                state);

        void VerifyRepair(
            WorkControlTransition transition,
            string outcome,
            string text,
            string reason = "",
            string detail = null,
            float sentiment = 0.45f,
            bool bubble = false) => Verify(
                Create(
                    transition,
                    BuiltInWorkTypeIds.Repair,
                    target: true,
                    detail: detail),
                CharacterActivityKinds.Work,
                outcome,
                text,
                BuiltInWorkTypeIds.Repair.Value,
                reason,
                target: true,
                projectedTargetName: targetName,
                facilityId: targetDefinitionId,
                sentiment: sentiment,
                bubble: bubble);

        void Verify(
            WorkControlChangedEvent changed,
            string kind,
            string outcome,
            string text,
            string action = "",
            string reason = "",
            bool target = false,
            string projectedTargetName = "",
            float value = 0f,
            int facilityId = -1,
            float sentiment = 0f,
            bool bubble = false,
            bool firstVariant = true)
        {
            Require(firstVariant
                    ? covered.Add(changed.Transition)
                    : covered.Contains(changed.Transition),
                "Work-control transition variant ordering was invalid: "
                    + changed.Transition);
            CharacterActivityEvent activity =
                WorkControlActivityObserver.Project(changed);
            Require(activity.KindId == kind
                && activity.OutcomeId == outcome
                && activity.FactText == text
                && activity.ActionId == action
                && activity.ReasonCode == reason
                && string.IsNullOrEmpty(activity.ActorId)
                && string.IsNullOrEmpty(activity.ActorName)
                && activity.TargetId
                    == (target ? targetId : string.Empty)
                && activity.TargetName == projectedTargetName
                && activity.PlaceId.Length == 0
                && activity.PlaceName.Length == 0
                && activity.Value == value
                && activity.Quantity == 0
                && activity.FacilityId == facilityId
                && activity.Sentiment == sentiment
                && activity.NarrativeEligible
                && activity.BubbleEligible == bubble
                && activity.VisibleToPlayer,
                "Work-control projection changed legacy fields: "
                    + changed.Transition);
        }

        string PublishMissingActor()
        {
            try
            {
                events.Publish(published);
                return string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }
    }

    private static void VerifyCharacterVitalsActivityObserver()
    {
        var character = new CharacterId("character:vitals-activity-host");
        RequireThrows<ArgumentException>(() => new CharacterVitalsChangedEvent(
            default, CharacterVitalTransition.Healed, 1f),
            "Vitals event accepted a missing character.");
        RequireThrows<ArgumentException>(() => new CharacterVitalsChangedEvent(
            character, CharacterVitalTransition.Healed, float.NaN),
            "Vitals event accepted a nonfinite change.");
        RequireThrows<ArgumentException>(() => new CharacterVitalsChangedEvent(
            character, CharacterVitalTransition.InjurySeverityChanged, 1.01f),
            "Vitals event accepted an out-of-range injury projection.");
        Verify(
            new CharacterVitalsChangedEvent(
                character,
                CharacterVitalTransition.Damaged,
                2.26f,
                reason: null),
            CharacterActivityOutcomes.Damaged,
            "피해 2.3",
            "health:damage",
            string.Empty,
            value: 2.26f,
            sentiment: -0.8f,
            bubble: true);
        Verify(
            new CharacterVitalsChangedEvent(
                character,
                CharacterVitalTransition.Damaged,
                3.14f,
                reason: "호스트 절상"),
            CharacterActivityOutcomes.Damaged,
            "피해 3.1: 호스트 절상",
            "health:damage",
            "호스트 절상",
            value: 3.14f,
            sentiment: -0.8f,
            bubble: true);
        Verify(
            new CharacterVitalsChangedEvent(
                character,
                CharacterVitalTransition.Healed,
                4.26f),
            CharacterActivityOutcomes.Completed,
            "회복 4.3",
            "health:heal",
            string.Empty,
            value: 4.26f,
            sentiment: 0.55f);
        Verify(
            new CharacterVitalsChangedEvent(
                character,
                CharacterVitalTransition.InjurySeverityChanged,
                0.126f),
            CharacterActivityOutcomes.Changed,
            "부상도 변경: 13%",
            "health:injury-severity",
            string.Empty,
            value: 0.126f,
            sentiment: -0.126f);
        Verify(
            new CharacterVitalsChangedEvent(
                character, CharacterVitalTransition.InjurySeverityChanged, 0.125f),
            CharacterActivityOutcomes.Changed,
            "부상도 변경: 12%",
            "health:injury-severity",
            string.Empty,
            value: 0.125f,
            sentiment: -0.125f);
        Verify(
            new CharacterVitalsChangedEvent(
                character,
                CharacterVitalTransition.Died,
                0f,
                wasFatal: true,
                deathCause: CharacterDeathCauseCode.Combat),
            CharacterActivityOutcomes.Defeated,
            "사망",
            "health:death",
            string.Empty,
            value: 1f,
            sentiment: -1f,
            bubble: true);
        Verify(
            new CharacterVitalsChangedEvent(
                character,
                CharacterVitalTransition.Died,
                7f,
                reason: "호스트 전투",
                wasFatal: true,
                deathCause: CharacterDeathCauseCode.Combat),
            CharacterActivityOutcomes.Defeated,
            "사망: 호스트 전투",
            "health:death",
            "호스트 전투",
            value: 1f,
            sentiment: -1f,
            bubble: true);

        var events = new GameEventBus();
        var world = new EmptyCharacterAiWorldRegistry();
        var observer = new CharacterVitalsActivityObserver(events, world);
        var published = new CharacterVitalsChangedEvent(
            character,
            CharacterVitalTransition.Damaged,
            1f,
            "호스트 수명주기");
        observer.Initialize();
        observer.Initialize();
        Require(PublishMissingActor()
                == "character-vitals-activity-character-missing:"
                    + character.Value
            && world.LifetimeCharacterReadCount == 1,
            "Character-vitals observer hid a missing actor or subscribed twice.");
        observer.Dispose();
        observer.Dispose();
        events.Publish(published);
        Require(world.LifetimeCharacterReadCount == 1,
            "Disposed character-vitals observer remained subscribed.");
        observer.Initialize();
        Require(PublishMissingActor()
                == "character-vitals-activity-character-missing:"
                    + character.Value
            && world.LifetimeCharacterReadCount == 2,
            "Character-vitals observer did not reinitialize exactly once.");
        observer.Dispose();

        string PublishMissingActor()
        {
            try
            {
                events.Publish(published);
                return string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }

        static void Verify(
            CharacterVitalsChangedEvent changed,
            string outcome,
            string text,
            string action,
            string reason,
            float value,
            float sentiment,
            bool bubble = false)
        {
            CharacterActivityEvent activity =
                CharacterVitalsActivityObserver.Project(changed);
            Require(activity.KindId == CharacterActivityKinds.Health
                && activity.OutcomeId == outcome
                && activity.FactText == text
                && activity.ActionId == action
                && activity.ReasonCode == reason
                && string.IsNullOrEmpty(activity.ActorId)
                && string.IsNullOrEmpty(activity.ActorName)
                && activity.TargetId.Length == 0
                && activity.TargetName.Length == 0
                && activity.PlaceId.Length == 0
                && activity.PlaceName.Length == 0
                && activity.Value == value
                && activity.Quantity == 0
                && activity.FacilityId == -1
                && activity.Sentiment == sentiment
                && activity.NarrativeEligible
                && activity.BubbleEligible == bubble
                && activity.VisibleToPlayer,
                "Character-vitals projection changed legacy fields: "
                    + changed.Transition);
        }
    }

    private static void VerifyOwnerCommandActivityObserver()
    {
        var character = new CharacterId("character:owner-command-host");
        var priority = new OwnerCommandRejectedEvent(
            OwnerCommandRejectedKind.PriorityWork,
            character,
            "building:owner-command-host",
            "호스트 작업장",
            44,
            "host-priority-rejected",
            "우선 작업 불가");
        var suppress = new OwnerCommandRejectedEvent(
            OwnerCommandRejectedKind.Suppress,
            character,
            "character:owner-command-target",
            "호스트 제압 대상",
            -1,
            "host-suppress-rejected",
            "제압 불가");
        Verify(
            OwnerCommandActivityObserver.Project(priority),
            "command:priority-work",
            priority.TargetId,
            priority.TargetName,
            priority.ReasonCode,
            "우선 지정 실패: 우선 작업 불가",
            facilityId: 44);
        Verify(
            OwnerCommandActivityObserver.Project(suppress),
            "command:priority-suppress",
            suppress.TargetId,
            suppress.TargetName,
            suppress.ReasonCode,
            "우선 지정 실패: 제압 불가",
            facilityId: -1);

        var nullDetail = new OwnerCommandRejectedEvent(
            OwnerCommandRejectedKind.Suppress,
            character,
            "character:owner-command-null-detail",
            "호스트 빈 사유 대상",
            -1,
            "host-empty-detail",
            detail: null);
        Require(nullDetail.Detail.Length == 0
            && OwnerCommandActivityObserver.Project(nullDetail).FactText
                == "우선 지정 실패:",
            "Owner-command rejection did not preserve its nullable detail boundary.");
        RequireThrows<ArgumentException>(
            () => new OwnerCommandRejectedEvent(
                (OwnerCommandRejectedKind)99,
                character,
                "building:owner-command-host",
                "호스트 작업장",
                44,
                "host-invalid",
                "invalid"),
            "Owner-command event accepted an undefined rejection kind.");
        RequireThrows<ArgumentException>(
            () => new OwnerCommandRejectedEvent(
                OwnerCommandRejectedKind.PriorityWork,
                default,
                "building:owner-command-host",
                "호스트 작업장",
                44,
                "host-invalid",
                "invalid"),
            "Owner-command event accepted a missing character.");
        RequireThrows<ArgumentException>(
            () => new OwnerCommandRejectedEvent(
                OwnerCommandRejectedKind.Suppress,
                character,
                "character:owner-command-target",
                "호스트 제압 대상",
                44,
                "host-invalid",
                "invalid"),
            "Owner-command suppress event accepted a facility definition id.");

        var events = new GameEventBus();
        var world = new EmptyCharacterAiWorldRegistry();
        var observer = new OwnerCommandActivityObserver(events, world);
        observer.Initialize();
        observer.Initialize();
        Require(PublishMissingActor()
                == "owner-command-activity-character-missing:"
                    + character.Value
            && world.LifetimeCharacterReadCount == 1,
            "Owner-command observer hid a missing actor or subscribed twice.");
        observer.Dispose();
        observer.Dispose();
        events.Publish(priority);
        Require(world.LifetimeCharacterReadCount == 1,
            "Disposed owner-command observer remained subscribed.");
        observer.Initialize();
        Require(PublishMissingActor()
                == "owner-command-activity-character-missing:"
                    + character.Value
            && world.LifetimeCharacterReadCount == 2,
            "Owner-command observer did not reinitialize exactly once.");
        observer.Dispose();

        string PublishMissingActor()
        {
            try
            {
                events.Publish(priority);
                return string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }

        static void Verify(
            CharacterActivityEvent activity,
            string action,
            string targetId,
            string targetName,
            string reason,
            string text,
            int facilityId)
        {
            Require(activity.KindId == CharacterActivityKinds.Command
                && activity.OutcomeId == CharacterActivityOutcomes.Failed
                && activity.FactText == text
                && activity.ActionId == action
                && activity.TargetId == targetId
                && activity.TargetName == targetName
                && activity.ReasonCode == reason
                && activity.ActorId == null
                && activity.ActorName == null
                && activity.PlaceId.Length == 0
                && activity.PlaceName.Length == 0
                && activity.Value == 0f
                && activity.Quantity == 0
                && activity.FacilityId == facilityId
                && activity.Sentiment == -0.7f
                && activity.NarrativeEligible
                && activity.BubbleEligible
                && activity.VisibleToPlayer,
                "Owner-command rejection projection changed legacy fields: "
                    + action);
        }
    }

    private static void VerifyDefenseEngagementActivityObserver()
    {
        var subject = new CharacterId("character:defense-engagement-host");
        var dispatched = new DefenseEngagementChangedEvent(
            DefenseEngagementTransition.Dispatched,
            subject,
            dispatchDetail: "호스트 방어 배치");
        var started = new DefenseEngagementChangedEvent(
            DefenseEngagementTransition.Started,
            subject,
            targetId: "intruder:defense-host",
            targetName: "호스트 침입자 대상",
            intruderDisplayLabel: "호스트 침입자");
        var completed = new DefenseEngagementChangedEvent(
            DefenseEngagementTransition.Completed,
            subject,
            targetId: "intruder:defense-host",
            targetName: "호스트 침입자 대상",
            intruderDisplayLabel: "호스트 침입자",
            exchangeCount: 6);
        Verify(
            DefenseEngagementActivityObserver.Project(dispatched),
            CharacterActivityOutcomes.Started,
            "호스트 방어 배치",
            "defense:dispatch",
            string.Empty,
            string.Empty,
            value: 0f,
            sentiment: -0.05f,
            bubble: false);
        Verify(
            DefenseEngagementActivityObserver.Project(started),
            CharacterActivityOutcomes.Started,
            "Defend against 호스트 침입자",
            "defense:engagement",
            started.TargetId,
            started.TargetName,
            value: 0f,
            sentiment: -0.1f,
            bubble: true);
        Verify(
            DefenseEngagementActivityObserver.Project(completed),
            CharacterActivityOutcomes.Completed,
            "Intruder defense completed",
            "defense:engagement",
            string.Empty,
            completed.TargetName,
            value: 6f,
            sentiment: 0.7f,
            bubble: true);

        RequireThrows<ArgumentException>(
            () => new DefenseEngagementChangedEvent(
                (DefenseEngagementTransition)99,
                subject,
                dispatchDetail: "invalid"),
            "Defense engagement event accepted an undefined transition.");
        RequireThrows<ArgumentException>(
            () => new DefenseEngagementChangedEvent(
                DefenseEngagementTransition.Dispatched,
                default,
                dispatchDetail: "invalid"),
            "Defense engagement event accepted a missing subject.");
        RequireThrows<ArgumentException>(
            () => new DefenseEngagementChangedEvent(
                DefenseEngagementTransition.Started,
                subject,
                targetName: "호스트 침입자 대상"),
            "Defense engagement event accepted a missing intruder label.");
        RequireThrows<ArgumentException>(
            () => new DefenseEngagementChangedEvent(
                DefenseEngagementTransition.Completed,
                subject,
                targetName: "호스트 침입자 대상",
                intruderDisplayLabel: "호스트 침입자",
                exchangeCount: -1),
            "Defense engagement event accepted a negative exchange count.");

        var events = new GameEventBus();
        var world = new EmptyCharacterAiWorldRegistry();
        var observer = new DefenseEngagementActivityObserver(events, world);
        observer.Initialize();
        observer.Initialize();
        Require(PublishMissingActor()
                == "defense-engagement-activity-character-missing:"
                    + subject.Value
            && world.LifetimeCharacterReadCount == 1,
            "Defense engagement observer hid a missing actor or subscribed twice.");
        observer.Dispose();
        observer.Dispose();
        events.Publish(started);
        Require(world.LifetimeCharacterReadCount == 1,
            "Disposed defense engagement observer remained subscribed.");
        observer.Initialize();
        Require(PublishMissingActor()
                == "defense-engagement-activity-character-missing:"
                    + subject.Value
            && world.LifetimeCharacterReadCount == 2,
            "Defense engagement observer did not reinitialize exactly once.");
        observer.Dispose();

        string PublishMissingActor()
        {
            try
            {
                events.Publish(started);
                return string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }

        static void Verify(
            CharacterActivityEvent activity,
            string outcome,
            string text,
            string action,
            string targetId,
            string targetName,
            float value,
            float sentiment,
            bool bubble)
        {
            Require(activity.KindId == CharacterActivityKinds.Combat
                && activity.OutcomeId == outcome
                && activity.FactText == text
                && activity.ActionId == action
                && activity.TargetId == targetId
                && activity.TargetName == targetName
                && activity.ActorId == null
                && activity.ActorName == null
                && activity.PlaceId.Length == 0
                && activity.PlaceName.Length == 0
                && activity.ReasonCode.Length == 0
                && activity.Value == value
                && activity.Quantity == 0
                && activity.FacilityId == -1
                && activity.Sentiment == sentiment
                && activity.NarrativeEligible
                && activity.BubbleEligible == bubble
                && activity.VisibleToPlayer,
                "Defense engagement projection changed legacy fields: "
                    + action + "/" + outcome);
        }
    }

    private static void VerifyWildlifeHuntActivityObserver()
    {
        var hunter = new CharacterId("character:wildlife-hunt-host");
        const string hunterName = "호스트 사냥꾼";
        const string wildlifeId = "wildlife-hunt-host";
        const string wildlifeName = "호스트 멧돼지";
        var limbLabels = new[]
        {
            (CombatBodyPart.Head, "머리"),
            (CombatBodyPart.Torso, "몸통"),
            (CombatBodyPart.LeftArm, "왼앞다리"),
            (CombatBodyPart.RightArm, "오른앞다리"),
            (CombatBodyPart.LeftLeg, "왼뒷다리"),
            (CombatBodyPart.RightLeg, "오른뒷다리"),
            ((CombatBodyPart)999, "몸")
        };
        foreach ((CombatBodyPart part, string label) in limbLabels)
        {
            var hit = new WildlifeHuntAttackResolvedEvent(
                hunter,
                hunterName,
                wildlifeId,
                wildlifeName,
                part,
                appliedDamage: 7,
                hit: true,
                coverBlocked: false,
                killed: false);
            VerifyAttack(
                WildlifeHuntActivityObserver.Project(hit),
                CharacterActivityOutcomes.Progress,
                $"{hunterName}이(가) {wildlifeName}의 {label}에 7 피해를 입혔다.",
                value: 7f,
                sentiment: 0.1f);
        }

        var killed = new WildlifeHuntAttackResolvedEvent(
            hunter,
            hunterName,
            wildlifeId,
            wildlifeName,
            CombatBodyPart.Torso,
            appliedDamage: 12,
            hit: true,
            coverBlocked: false,
            killed: true);
        VerifyAttack(
            WildlifeHuntActivityObserver.Project(killed),
            CharacterActivityOutcomes.Completed,
            $"{hunterName}이(가) {wildlifeName} 사냥을 끝냈다.",
            value: 12f,
            sentiment: 0.45f);
        var covered = new WildlifeHuntAttackResolvedEvent(
            hunter,
            hunterName,
            wildlifeId,
            wildlifeName,
            CombatBodyPart.Head,
            appliedDamage: 0,
            hit: false,
            coverBlocked: true,
            killed: false);
        VerifyAttack(
            WildlifeHuntActivityObserver.Project(covered),
            CharacterActivityOutcomes.Progress,
            $"{hunterName}의 공격이 엄폐물에 막혔다.",
            value: 0f,
            sentiment: -0.1f);
        var dodged = new WildlifeHuntAttackResolvedEvent(
            hunter,
            hunterName,
            wildlifeId,
            wildlifeName,
            CombatBodyPart.Head,
            appliedDamage: 0,
            hit: false,
            coverBlocked: false,
            killed: false);
        VerifyAttack(
            WildlifeHuntActivityObserver.Project(dodged),
            CharacterActivityOutcomes.Progress,
            $"{hunterName}의 공격을 {wildlifeName}이(가) 피했다.",
            value: 0f,
            sentiment: -0.1f);

        var completion = new WildlifeHuntCompletedEvent(
            hunter,
            "boar-host",
            dangerous: true,
            maximumHealth: 37.5f,
            absoluteDay: 19);
        CharacterStateNarrativeProjection projection =
            WildlifeHuntActivityObserver.Project(completion);
        Require(projection.Character.Equals(hunter)
            && projection.Domain == CharacterNarrativeDomain.Survival
            && projection.FactId == "survival/hunt"
            && projection.SubjectId == "wildlife:boar-host"
            && projection.Outcome == "dangerous-hunt"
            && projection.Value == 37.5f
            && projection.Day == 19
            && projection.TriggerPassives
            && projection.Context == null,
            "Wildlife hunt completion projection changed legacy fields.");
        var ordinary = new WildlifeHuntCompletedEvent(
            hunter,
            "rabbit-host",
            dangerous: false,
            maximumHealth: 5f,
            absoluteDay: 20);
        Require(WildlifeHuntActivityObserver.Project(ordinary).Outcome == "hunt",
            "Wildlife hunt completion lost its ordinary-hunt outcome.");

        RequireThrows<ArgumentException>(
            () => new WildlifeHuntAttackResolvedEvent(
                default,
                hunterName,
                wildlifeId,
                wildlifeName,
                CombatBodyPart.Head,
                1,
                true,
                false,
                false),
            "Wildlife hunt attack accepted a missing hunter.");
        RequireThrows<ArgumentException>(
            () => new WildlifeHuntAttackResolvedEvent(
                hunter,
                null,
                wildlifeId,
                wildlifeName,
                CombatBodyPart.Head,
                1,
                true,
                false,
                false),
            "Wildlife hunt attack accepted a null hunter name.");
        RequireThrows<ArgumentException>(
            () => new WildlifeHuntAttackResolvedEvent(
                hunter,
                hunterName,
                wildlifeId,
                wildlifeName,
                CombatBodyPart.Head,
                -1,
                true,
                false,
                false),
            "Wildlife hunt attack accepted negative damage.");
        RequireThrows<ArgumentException>(
            () => new WildlifeHuntCompletedEvent(
                hunter,
                "boar-host",
                dangerous: false,
                maximumHealth: float.NaN,
                absoluteDay: 19),
            "Wildlife hunt completion accepted nonfinite maximum health.");

        var events = new GameEventBus();
        var world = new EmptyCharacterAiWorldRegistry();
        var observer = new WildlifeHuntActivityObserver(events, world);
        observer.Initialize();
        observer.Initialize();
        Require(PublishAttackMissingActor()
                == "wildlife-hunt-observer-character-missing:" + hunter.Value
            && PublishCompletionMissingActor()
                == "wildlife-hunt-observer-character-missing:" + hunter.Value
            && world.LifetimeCharacterReadCount == 2,
            "Wildlife hunt observer hid a missing actor or subscribed twice.");
        observer.Dispose();
        observer.Dispose();
        events.Publish(killed);
        events.Publish(completion);
        Require(world.LifetimeCharacterReadCount == 2,
            "Disposed wildlife hunt observer retained one of its subscriptions.");
        observer.Initialize();
        Require(PublishAttackMissingActor()
                == "wildlife-hunt-observer-character-missing:" + hunter.Value
            && PublishCompletionMissingActor()
                == "wildlife-hunt-observer-character-missing:" + hunter.Value
            && world.LifetimeCharacterReadCount == 4,
            "Wildlife hunt observer did not reinitialize both subscriptions once.");
        observer.Dispose();

        string PublishAttackMissingActor()
        {
            try
            {
                events.Publish(killed);
                return string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }

        string PublishCompletionMissingActor()
        {
            try
            {
                events.Publish(completion);
                return string.Empty;
            }
            catch (InvalidOperationException exception)
            {
                return exception.Message;
            }
        }

        void VerifyAttack(
            CharacterActivityEvent activity,
            string outcome,
            string text,
            float value,
            float sentiment)
        {
            Require(activity.KindId == CharacterActivityKinds.Combat
                && activity.OutcomeId == outcome
                && activity.FactText == text
                && activity.ActionId == "survival/hunt"
                && activity.TargetId == "wildlife:" + wildlifeId
                && activity.TargetName == wildlifeName
                && activity.ActorId == null
                && activity.ActorName == null
                && activity.PlaceId.Length == 0
                && activity.PlaceName.Length == 0
                && activity.ReasonCode.Length == 0
                && activity.Value == value
                && activity.Quantity == 0
                && activity.FacilityId == -1
                && activity.Sentiment == sentiment
                && activity.NarrativeEligible
                && activity.BubbleEligible
                && activity.VisibleToPlayer,
                "Wildlife hunt attack projection changed legacy fields: "
                    + text);
        }
    }

    private static void VerifyWildlifeFactObservation()
    {
        WildlifeGameplayOutcomeSaveData commandSource = CreateCommand(
            applied: true);
        WildlifeGameplayOutcomeSaveData actionSource = CreateAction(
            WildlifeAttackProjection.Hit);
        var command = new WildlifeCommandFact(commandSource);
        var action = new WildlifeActionFact(actionSource);

        commandSource.wildlifeSpeciesId = "species:mutated-source";
        actionSource.actorDisplayName = "변조된 사냥꾼";
        WildlifeGameplayOutcomeSaveData detachedCommand = command.Snapshot();
        WildlifeGameplayOutcomeSaveData detachedAction = action.Snapshot();
        detachedCommand.facilityDisplayName = "변조된 우리";
        detachedAction.coverDamage = 999;
        Require(command.Snapshot().wildlifeSpeciesId == "species:host-boar"
            && command.Snapshot().facilityDisplayName == "호스트 야생동물 우리"
            && action.Snapshot().actorDisplayName == "호스트 사냥꾼"
            && action.Snapshot().coverDamage == 2,
            "Wildlife facts retained caller-owned source or snapshot state.");

        WildlifeCommandOutcomeReceipt nativeCommand =
            EnvironmentOutcomeReceiptFactory.CreateWildlifeCommand(
                command.Snapshot());
        WildlifeCommandOutcomeReceipt mappedCommand =
            WildlifeFactMapping.Capture(command);
        WildlifeActionOutcomeReceipt nativeAction =
            EnvironmentOutcomeReceiptFactory.CreateWildlifeAction(
                action.Snapshot());
        WildlifeActionOutcomeReceipt mappedAction =
            WildlifeFactMapping.Capture(action);
        string commandKey = new GameplayResultKey(
            EnvironmentOutcomeIds.WildlifeCommandProducer,
            new GameplayOperationId(
                "wildlife-command:hunt-designation:wild:test:1"),
            1L,
            0).ToString();
        string actionKey = new GameplayResultKey(
            EnvironmentOutcomeIds.WildlifeActionProducer,
            new GameplayOperationId(
                "wildlife-action:hunt-attack:wild:test:1"),
            1L,
            0).ToString();
        Require(mappedCommand.Payload.ResultKey.ToString() == commandKey
            && mappedAction.Payload.ResultKey.ToString() == actionKey,
            "Wildlife mapping changed native command or action keys.");
        RequireWildlifePayloadExtension(
            nativeCommand.Payload,
            mappedCommand.Payload,
            WildlifeFactMapping.ComputeContextSha256(command.Snapshot()),
            "command");
        RequireWildlifePayloadExtension(
            nativeAction.Payload,
            mappedAction.Payload,
            WildlifeFactMapping.ComputeContextSha256(action.Snapshot()),
            "hit action");
        VerifyAttackProjection(
            mappedAction.Payload,
            GameplayOutcomeStatus.Succeeded,
            healthAfter: 6,
            damage: 4,
            hit: true,
            killed: false,
            "호스트 사냥꾼이 호스트 멧돼지에게 4 피해를 입혔다.");

        WildlifeGameplayOutcomeSaveData missData = CreateAction(
            WildlifeAttackProjection.Miss);
        WildlifeActionOutcomeReceipt miss = WildlifeFactMapping.Capture(
            new WildlifeActionFact(missData));
        RequireWildlifePayloadExtension(
            EnvironmentOutcomeReceiptFactory.CreateWildlifeAction(missData)
                .Payload,
            miss.Payload,
            WildlifeFactMapping.ComputeContextSha256(missData),
            "miss action");
        VerifyAttackProjection(
            miss.Payload,
            GameplayOutcomeStatus.Failed,
            healthAfter: 10,
            damage: 0,
            hit: false,
            killed: false,
            "호스트 사냥꾼의 공격이 호스트 멧돼지에게 적중하지 않았다.");

        WildlifeGameplayOutcomeSaveData killedData = CreateAction(
            WildlifeAttackProjection.Killed);
        WildlifeActionOutcomeReceipt killed = WildlifeFactMapping.Capture(
            new WildlifeActionFact(killedData));
        RequireWildlifePayloadExtension(
            EnvironmentOutcomeReceiptFactory.CreateWildlifeAction(killedData)
                .Payload,
            killed.Payload,
            WildlifeFactMapping.ComputeContextSha256(killedData),
            "killed action");
        VerifyAttackProjection(
            killed.Payload,
            GameplayOutcomeStatus.Succeeded,
            healthAfter: 0,
            damage: 4,
            hit: true,
            killed: true,
            "호스트 사냥꾼이 호스트 멧돼지을 처치했다.");

        WildlifeGameplayOutcomeSaveData proposedCommandData = command.Snapshot();
        proposedCommandData.stateAfter = proposedCommandData.stateBefore;
        proposedCommandData.flagAfter = proposedCommandData.flagBefore;
        proposedCommandData.priorityAfter = proposedCommandData.priorityBefore;
        var proposedCommand = new WildlifeCommandFact(
            proposedCommandData,
            proposed: true);
        WildlifeGameplayOutcomeSaveData proposedActionData = action.Snapshot();
        proposedActionData.stateAfter = proposedActionData.stateBefore;
        proposedActionData.healthAfter = proposedActionData.healthBefore;
        proposedActionData.damage = 0;
        proposedActionData.hit = false;
        proposedActionData.killed = false;
        proposedActionData.ammunitionConsumed = 0;
        proposedActionData.ammunitionAfter = proposedActionData.ammunitionBefore;
        proposedActionData.coverDamage = 0;
        var proposedAction = new WildlifeActionFact(
            proposedActionData,
            proposed: true);

        using (var missing = new DomainFactDispatcher())
        {
            Require(!missing.TryReserve(
                    proposedCommand,
                    out _,
                    out string commandFailure)
                && commandFailure.Contains(
                    "domain-fact-observer-missing",
                    StringComparison.Ordinal)
                && !missing.TryReserve(
                    proposedAction,
                    out _,
                    out string actionFailure)
                && actionFailure.Contains(
                    "domain-fact-observer-missing",
                    StringComparison.Ordinal),
                "Wildlife facts reserved without their explicit observers.");
        }

        using var authority = new WildlifeFactAuthority();
        var commandBinding = WildlifeFactMapping.CreateCommandBinding(
            authority.Registry,
            authority.Ledger,
            authority.Recorder,
            authority.CommitGroup);
        var actionBinding = WildlifeFactMapping.CreateActionBinding(
            authority.Registry,
            authority.Ledger,
            authority.Recorder,
            authority.CommitGroup);
        IDisposable commandConnection = commandBinding.Connect(
            authority.Dispatcher);
        IDisposable actionConnection = actionBinding.Connect(
            authority.Dispatcher);
        try
        {
            Require(authority.Dispatcher.SubscribedFactTypes.SequenceEqual(
                    new[]
                    {
                        typeof(WildlifeActionFact),
                        typeof(WildlifeCommandFact)
                    }.OrderBy(value => value.FullName, StringComparer.Ordinal)),
                "Wildlife observer bindings did not subscribe explicitly.");
            RequireThrows<InvalidOperationException>(
                () => commandBinding.Connect(authority.Dispatcher),
                "Wildlife command binding allowed a duplicate subscription.");
            commandConnection.Dispose();
            actionConnection.Dispose();
            Require(!authority.Dispatcher.TryReserve(
                    proposedCommand,
                    out _,
                    out string disposedCommandFailure)
                && disposedCommandFailure.Contains(
                    "domain-fact-observer-missing",
                    StringComparison.Ordinal)
                && !authority.Dispatcher.TryReserve(
                    proposedAction,
                    out _,
                    out string disposedActionFailure)
                && disposedActionFailure.Contains(
                    "domain-fact-observer-missing",
                    StringComparison.Ordinal),
                "Disposed wildlife bindings remained subscribed.");
            commandConnection = commandBinding.Connect(authority.Dispatcher);
            actionConnection = actionBinding.Connect(authority.Dispatcher);

            GameplayOutcomeLedgerSaveData emptyLedger = authority.Ledger
                .CaptureGameplayOutcomes();
            WildlifeGameplayOutcomeSaveData pendingCommand =
                command.Snapshot();
            pendingCommand.publicationKey = commandKey;
            WildlifeFactPreflight.Validate(
                WildlifeOwner(pendingCommand),
                new CircusSaveData(),
                emptyLedger);
            RequireThrows<InvalidOperationException>(
                () => WildlifeFactPreflight.Validate(
                    WildlifeOwner(pendingCommand),
                    CircusOwner(pendingCommand),
                    emptyLedger),
                "Wildlife preflight accepted one pending fact in both owners.");

            Require(authority.Dispatcher.TryReserve(
                    proposedCommand,
                    out DomainFactLease commandLease,
                    out string reserveFailure)
                && commandLease.PublicationKey == commandKey,
                "Wildlife command proposal did not reserve its native key: "
                    + reserveFailure);
            DomainFactDelivery commandDelivery = authority.Dispatcher.Publish(
                commandLease,
                command);
            Require(commandDelivery.Completed
                && commandDelivery.OperationKey == commandKey,
                "Wildlife command result did not publish after state changed: "
                    + commandDelivery.Detail);

            Require(WildlifeFactMapping.Reserve(proposedAction, 1L).Status
                    == GameplayOutcomeStatus.Failed
                && mappedAction.Payload.Status
                    == GameplayOutcomeStatus.Succeeded,
                "Wildlife action preview did not preserve its unknown result status.");
            bool actionReserved = authority.Dispatcher.TryReserve(
                proposedAction,
                out DomainFactLease actionLease,
                out reserveFailure);
            Require(actionReserved && actionLease.PublicationKey == actionKey,
                "Wildlife action proposal did not reserve before its result changed: "
                    + reserveFailure);
            DomainFactDelivery actionDelivery = authority.Dispatcher.Publish(
                actionLease,
                action);
            Require(actionDelivery.Completed
                && actionDelivery.OperationKey == actionKey
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 2,
                "Wildlife hit result did not replace its failed preview exactly: "
                    + actionDelivery.Detail);

            WildlifeGameplayOutcomeSaveData committedCommand =
                command.Snapshot();
            committedCommand.publicationKey = commandKey;
            WildlifeGameplayOutcomeSaveData committedAction =
                action.Snapshot();
            committedAction.publicationKey = actionKey;
            GameplayOutcomeLedgerSaveData committedLedger = authority.Ledger
                .CaptureGameplayOutcomes();
            WildlifeFactPreflight.Validate(
                WildlifeOwner(committedCommand),
                CircusOwner(committedAction),
                committedLedger);

            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                committedLedger);
            var exactResults = new DomainFactDelivery[2];
            Require(authority.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(commandKey, command),
                        DomainFactReplay.Create(actionKey, action)
                    },
                    exactResults,
                    out string replayFailure)
                && exactResults.All(value => value.Completed)
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 2,
                "Wildlife exact replay duplicated or lost native outcomes: "
                    + replayFailure);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(authority.Ledger.CaptureGameplayOutcomes()),
                "Wildlife exact replay changed canonical native outcomes.");

            RequireActionTamperRejected(
                value => value.wildlifeSpeciesId = "species:host-wolf",
                "species");
            RequireActionTamperRejected(
                value => value.wildlifeDisplayName = "호스트 회색 늑대",
                "display name");
            RequireActionTamperRejected(
                value => value.facilityDisplayName = "변조된 사냥 초소",
                "facility name");
            RequireActionTamperRejected(
                value => value.coverDamage = 3,
                "cover damage");

            WildlifeGameplayOutcomeSaveData changedContext =
                committedAction.Clone();
            changedContext.facilityDisplayName = "변조된 사냥 초소";
            RequireThrows<InvalidOperationException>(
                () => WildlifeFactPreflight.Validate(
                    WildlifeOwner(committedCommand),
                    CircusOwner(changedContext),
                    committedLedger),
                "Wildlife preflight accepted mutated committed context.");
            WildlifeGameplayOutcomeSaveData changedKey =
                committedCommand.Clone();
            changedKey.publicationKey = actionKey;
            RequireThrows<InvalidOperationException>(
                () => WildlifeFactPreflight.Validate(
                    WildlifeOwner(changedKey),
                    new CircusSaveData(),
                    committedLedger),
                "Wildlife preflight accepted a mismatched opaque key.");

            void RequireActionTamperRejected(
                Action<WildlifeGameplayOutcomeSaveData> change,
                string label)
            {
                WildlifeGameplayOutcomeSaveData altered = action.Snapshot();
                change(altered);
                Require(WildlifeFactMapping.ComputeContextSha256(altered)
                        != WildlifeFactMapping.ComputeContextSha256(
                            action.Snapshot()),
                    "Wildlife " + label
                        + " change did not alter full-domain provenance.");
                var result = new DomainFactDelivery[1];
                Require(!authority.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                actionKey,
                                new WildlifeActionFact(altered))
                        },
                        result,
                        out string changedFailure)
                    && result[0].Phase == DomainFactDeliveryPhase.Rejected
                    && !result[0].MayBeCommitted
                    && !string.IsNullOrWhiteSpace(changedFailure),
                    "Wildlife replay accepted altered " + label + ".");
                RequireCanonicalEqual(
                    canonical,
                    CanonicalHashes(
                        authority.Ledger.CaptureGameplayOutcomes()),
                    "Rejected wildlife " + label
                        + " replay changed canonical outcomes.");
            }
        }
        finally
        {
            actionConnection.Dispose();
            commandConnection.Dispose();
        }

        static WildlifeGameplayOutcomeSaveData CreateCommand(bool applied) =>
            new()
            {
                profile = WildlifeGameplayOutcomeProfile.Command,
                operationId =
                    "wildlife-command:hunt-designation:wild:test:1",
                eventKind = "hunt-designation",
                wildlifeId = "wild:test",
                wildlifeSpeciesId = "species:host-boar",
                wildlifeDisplayName = "호스트 멧돼지",
                facilityId = "building:wildlife-host:pen",
                facilityDisplayName = "호스트 야생동물 우리",
                x = 3,
                y = 5,
                stateBefore = 1,
                stateAfter = applied ? 2 : 1,
                flagBefore = false,
                flagAfter = applied,
                priorityBefore = false,
                priorityAfter = applied,
                reasonCode = "hunt-designation",
                absoluteDay = 0,
                ownerRevision = 1L
            };

        static WildlifeGameplayOutcomeSaveData CreateAction(
            WildlifeAttackProjection projection)
        {
            bool hit = projection != WildlifeAttackProjection.Miss;
            bool killed = projection == WildlifeAttackProjection.Killed;
            int healthBefore = killed ? 4 : 10;
            int healthAfter = hit ? healthBefore - 4 : healthBefore;
            return new WildlifeGameplayOutcomeSaveData
            {
                profile = WildlifeGameplayOutcomeProfile.Action,
                operationId = "wildlife-action:hunt-attack:wild:test:1",
                eventKind = "hunt-attack",
                wildlifeId = "wild:test",
                wildlifeSpeciesId = "species:host-boar",
                wildlifeDisplayName = "호스트 멧돼지",
                actorId = "character:wildlife-host:hunter",
                actorDisplayName = "호스트 사냥꾼",
                facilityId = "building:wildlife-host:hunt-post",
                facilityDisplayName = "호스트 사냥 초소",
                x = 7,
                y = 9,
                stateBefore = 1,
                stateAfter = killed ? 2 : 1,
                healthBefore = healthBefore,
                healthAfter = healthAfter,
                damage = healthBefore - healthAfter,
                hit = hit,
                killed = killed,
                weaponDefinitionId = "weapon:wildlife-host:bow",
                weaponInstanceId = "weapon-instance:wildlife-host:bow",
                ammunitionItemId = "item:wildlife-host:arrow",
                ammunitionBefore = 3,
                ammunitionConsumed = 1,
                ammunitionAfter = 2,
                bodyPart = "torso",
                coverSourceId = "building:wildlife-host:cover",
                coverDamage = 2,
                reasonCode = "hunt-attack",
                absoluteDay = 0,
                ownerRevision = 1L
            };
        }

        static DungeonWildlifeSaveData WildlifeOwner(
            WildlifeGameplayOutcomeSaveData value) => new()
        {
            nextGameplayOutcomeSequence = 1,
            gameplayOutcomes = new List<WildlifeGameplayOutcomeSaveData>
            {
                value.Clone()
            }
        };

        static CircusSaveData CircusOwner(
            WildlifeGameplayOutcomeSaveData value) => new()
        {
            nextWildlifeGameplayOutcomeSequence = 1,
            wildlifeGameplayOutcomes = new List<
                WildlifeGameplayOutcomeSaveData>
            {
                value.Clone()
            }
        };

        static void VerifyAttackProjection(
            in EnvironmentOutcomePayload payload,
            GameplayOutcomeStatus status,
            int healthAfter,
            int damage,
            bool hit,
            bool killed,
            string summary)
        {
            Require(payload.Status == status
                && WildlifePayloadMetric(
                    payload,
                    EnvironmentOutcomeIds.HealthAfterMetric) == healthAfter
                && WildlifePayloadMetric(
                    payload,
                    EnvironmentOutcomeIds.DamageMetric) == damage
                && WildlifePayloadMetric(
                    payload,
                    EnvironmentOutcomeIds.HitMetric) == (hit ? 1d : 0d)
                && WildlifePayloadMetric(
                    payload,
                    EnvironmentOutcomeIds.KilledMetric)
                    == (killed ? 1d : 0d)
                && SpeciesPayloadFact(
                    payload,
                    EnvironmentOutcomeIds.SummaryFact) == summary,
                "Wildlife attack projection changed hit/miss/killed evidence: "
                    + summary);
        }
    }

    private enum WildlifeAttackProjection
    {
        Hit,
        Miss,
        Killed
    }

    private static void VerifyWildlifeFoodRaidFactObservation()
    {
        WildlifeFoodRaidFactData source = CreateFoodRaidData();
        var fact = new WildlifeFoodRaidFact(source);
        source.wildlifeDisplayName = "변조된 늑대";
        source.dispositionSourceStackIds[0] = "stack:mutated-source";
        WildlifeFoodRaidFactData detached = fact.Snapshot();
        detached.targetItemDisplayName = "변조된 식량";
        detached.dispositionSourceStackIds[0] = "stack:mutated-snapshot";
        Require(fact.Snapshot().wildlifeDisplayName == "호스트 늑대"
            && fact.Snapshot().targetItemDisplayName == "호스트 곡물"
            && fact.Snapshot().dispositionSourceStackIds.SequenceEqual(
                new[] { "stack:food-raid:test" },
                StringComparer.Ordinal),
            "Wildlife food-raid fact retained caller-owned source or snapshot state.");

        using (var missing = new DomainFactDispatcher())
        {
            Require(!missing.TryStage(
                    fact,
                    out _,
                    out string missingFailure)
                && missingFailure.Contains(
                    "domain-fact-staging-observer-missing",
                    StringComparison.Ordinal),
                "Wildlife food-raid fact staged without its explicit observer.");
        }

        using var authority = new WildlifeFoodRaidFactAuthority();
        var binding = WildlifeFoodRaidFactMapping.CreateBinding(
            authority.Registry,
            authority.Ledger,
            authority.Recorder,
            authority.CommitGroup,
            authority.Ledger);
        IDisposable connection = binding.Connect(authority.Dispatcher);
        try
        {
            RequireThrows<InvalidOperationException>(
                () => binding.Connect(authority.Dispatcher),
                "Wildlife food-raid binding allowed a duplicate subscription.");
            connection.Dispose();
            Require(!authority.Dispatcher.TryStage(
                    fact,
                    out _,
                    out string disposedFailure)
                && disposedFailure.Contains(
                    "domain-fact-staging-observer-missing",
                    StringComparison.Ordinal),
                "Disposed wildlife food-raid binding remained subscribed.");
            connection = binding.Connect(authority.Dispatcher);

            PhysicalItemBatchDispositionReceipt physicalReceipt =
                CreateFoodRaidPhysicalReceipt(fact.Snapshot());
            var physicalFact = new PhysicalItemDisposedFact(
                physicalReceipt,
                fact.Snapshot().completedAbsoluteDay);
            Require(authority.Dispatcher.TryStage(
                    physicalFact,
                    out IDomainFactStage physicalStage,
                    out string physicalFailure),
                "Physical food-raid child did not stage: " + physicalFailure);
            string childKey = WildlifeFoodRaidFactMapping.PhysicalChildKey(
                fact.Snapshot()).ToString();
            Require(physicalStage.OperationKey == childKey,
                "Physical food-raid child staged under the wrong result key.");
            DomainFactDelivery physicalDelivery = physicalStage.Commit();
            Require(physicalDelivery.Completed
                && physicalDelivery.OperationKey == childKey,
                "Physical food-raid child did not commit exactly: "
                    + physicalDelivery.Detail);
            Require(authority.Ledger.TryGetResultIdentity(
                    WildlifeFoodRaidFactMapping.PhysicalChildKey(
                        fact.Snapshot()),
                    out GameplayOutcomeReplayIdentity childIdentity)
                && childIdentity.OutcomeId.IsValid
                && childIdentity.HasCanonicalPayloadHash,
                "Physical food-raid child identity was unavailable after commit.");

            WildlifeFoodRaidOutcomeReceipt native =
                EnvironmentOutcomeReceiptFactory.CreateWildlifeFoodRaid(
                    fact.Snapshot(),
                    childKey,
                    childIdentity.OutcomeId);
            WildlifeFoodRaidOutcomeReceipt staticMapped =
                WildlifeFoodRaidFactMapping.CaptureWithChild(
                    fact,
                    childIdentity.OutcomeId);
            WildlifeFoodRaidOutcomeReceipt observedMapped =
                new WildlifeFoodRaidFactMapping(authority.Ledger).Capture(fact);
            string contextHash = WildlifeFoodRaidFactMapping
                .ComputeContextSha256(fact.Snapshot());
            RequireWildlifePayloadExtension(
                native.Payload,
                staticMapped.Payload,
                contextHash,
                "food-raid static child",
                "wildlife-food-raid-context-sha256");
            RequireWildlifePayloadExtension(
                native.Payload,
                observedMapped.Payload,
                contextHash,
                "food-raid observed child",
                "wildlife-food-raid-context-sha256");
            Require(observedMapped.Payload.Causation.ParentOutcomeId
                    == childIdentity.OutcomeId
                && observedMapped.Payload.Causation.RootOperationId.Value
                    == fact.Snapshot().dispositionOperationId
                && observedMapped.Payload.Causation.RelationId
                    == "physical-theft-consumption",
                "Wildlife food-raid mapping changed physical child causation.");

            GameplayOutcomeLedgerSaveData childOnly = authority.Ledger
                .CaptureGameplayOutcomes();
            WildlifeFoodRaidOrderSaveData pendingOrder = CreateFoodRaidOrder(
                fact.Snapshot(),
                WildlifeFoodRaidOrderState.WaitingForDispositionFinalization,
                WildlifeFoodRaidCommitPhase.ItemCommitted);
            PhysicalItemBatchDispositionSaveData pendingPhysical =
                CreateFoodRaidPhysicalPending(fact.Snapshot(), childKey);
            WildlifeFoodRaidFactPreflight.Validate(
                WildlifeFoodRaidOwner(pendingOrder),
                PhysicalFoodRaidOwner(pendingPhysical),
                childOnly);
            RequireThrows<InvalidOperationException>(
                () => WildlifeFoodRaidFactPreflight.Validate(
                    WildlifeFoodRaidOwner(pendingOrder),
                    new DungeonPhysicalItemSaveData(),
                    childOnly),
                "Wildlife food-raid preflight accepted a missing physical pending child.");
            RequireThrows<InvalidOperationException>(
                () => WildlifeFoodRaidFactPreflight.Validate(
                    new DungeonWildlifeSaveData(),
                    PhysicalFoodRaidOwner(pendingPhysical),
                    childOnly),
                "Wildlife food-raid preflight accepted an orphan physical pending child.");

            Require(authority.Dispatcher.TryStage(
                    fact,
                    out IDomainFactStage parentStage,
                    out string parentFailure),
                "Wildlife food-raid parent did not stage after its child: "
                    + parentFailure);
            string parentKey = observedMapped.Payload.ResultKey.ToString();
            string expectedParentKey = new GameplayResultKey(
                EnvironmentOutcomeIds.WildlifeFoodRaidProducer,
                new GameplayOperationId(
                    fact.Snapshot().dispositionOperationId),
                fact.Snapshot().dispositionOwnerRevision,
                0).ToString();
            Require(parentKey == expectedParentKey
                && parentStage.OperationKey == parentKey,
                "Wildlife food-raid parent staged under the wrong result key.");
            DomainFactDelivery parentDelivery = parentStage.Commit();
            Require(parentDelivery.Completed
                && parentDelivery.OperationKey == parentKey,
                "Wildlife food-raid parent did not commit exactly: "
                    + parentDelivery.Detail);

            GameplayOutcomeLedgerSaveData committed = authority.Ledger
                .CaptureGameplayOutcomes();
            Require(OutcomeRowCount(committed) == 2,
                "Wildlife food-raid child and parent did not produce exactly two rows.");
            GameplayOutcomeSnapshot childRow = FindLiveOutcome(
                committed,
                childKey);
            GameplayOutcomeSnapshot parentRow = FindLiveOutcome(
                committed,
                parentKey);
            Require(parentRow.parentRunId == childRow.runId
                && parentRow.parentSequence == childRow.sequence
                && parentRow.sequence > childRow.sequence
                && parentRow.rootOperationId
                    == fact.Snapshot().dispositionOperationId
                && parentRow.causationRelationId
                    == "physical-theft-consumption",
                "Wildlife food-raid ledger lost canonical child-before-parent causation.");

            WildlifeFoodRaidOrderSaveData acknowledged = CreateFoodRaidOrder(
                fact.Snapshot(),
                WildlifeFoodRaidOrderState.Leaving,
                WildlifeFoodRaidCommitPhase.Acknowledged,
                parentKey,
                parentDelivery.PublicationId,
                parentDelivery.Fingerprint);
            WildlifeFoodRaidFactPreflight.Validate(
                WildlifeFoodRaidOwner(acknowledged),
                new DungeonPhysicalItemSaveData(),
                committed);

            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                committed);
            var exactReplay = new DomainFactDelivery[1];
            Require(authority.Dispatcher.ReplayBatch(
                    new[] { DomainFactReplay.Create(parentKey, fact) },
                    exactReplay,
                    out string replayFailure)
                && exactReplay[0].Completed
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 2,
                "Wildlife food-raid exact parent replay duplicated its cohort: "
                    + replayFailure);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(authority.Ledger.CaptureGameplayOutcomes()),
                "Wildlife food-raid exact replay changed canonical rows.");

            RequireFoodRaidTamperRejected(
                value =>
                {
                    value.targetStackId = "stack:food-raid:changed";
                    value.dispositionSourceStackIds[0] =
                        value.targetStackId;
                },
                "source stack");
            RequireFoodRaidTamperRejected(
                value => value.wildlifeDisplayName = "호스트 회색 늑대",
                "wildlife display name");
            RequireFoodRaidTamperRejected(
                value => value.targetItemDisplayName = "변조된 곡물",
                "item display name");

            WildlifeFoodRaidFactData changedContext = fact.Snapshot();
            changedContext.wildlifeDisplayName = "저장 변조 늑대";
            WildlifeFoodRaidOrderSaveData changedOrder = CreateFoodRaidOrder(
                changedContext,
                WildlifeFoodRaidOrderState.Leaving,
                WildlifeFoodRaidCommitPhase.Acknowledged,
                parentKey,
                parentDelivery.PublicationId,
                parentDelivery.Fingerprint);
            RequireThrows<InvalidOperationException>(
                () => WildlifeFoodRaidFactPreflight.Validate(
                    WildlifeFoodRaidOwner(changedOrder),
                    new DungeonPhysicalItemSaveData(),
                    committed),
                "Wildlife food-raid preflight accepted a changed frozen name.");
            WildlifeFoodRaidFactData changedSavedSource = fact.Snapshot();
            changedSavedSource.targetStackId = "stack:food-raid:saved-tamper";
            changedSavedSource.dispositionSourceStackIds[0] =
                changedSavedSource.targetStackId;
            WildlifeFoodRaidOrderSaveData changedSourceOrder =
                CreateFoodRaidOrder(
                    changedSavedSource,
                    WildlifeFoodRaidOrderState.Leaving,
                    WildlifeFoodRaidCommitPhase.Acknowledged,
                    parentKey,
                    parentDelivery.PublicationId,
                    parentDelivery.Fingerprint);
            RequireThrows<InvalidOperationException>(
                () => WildlifeFoodRaidFactPreflight.Validate(
                    WildlifeFoodRaidOwner(changedSourceOrder),
                    new DungeonPhysicalItemSaveData(),
                    committed),
                "Wildlife food-raid preflight accepted a changed source stack.");
            WildlifeFoodRaidOrderSaveData changedFingerprint =
                CreateFoodRaidOrder(
                    fact.Snapshot(),
                    WildlifeFoodRaidOrderState.Leaving,
                    WildlifeFoodRaidCommitPhase.Acknowledged,
                    parentKey,
                    parentDelivery.PublicationId,
                    new string('0', 64));
            RequireThrows<InvalidOperationException>(
                () => WildlifeFoodRaidFactPreflight.Validate(
                    WildlifeFoodRaidOwner(changedFingerprint),
                    new DungeonPhysicalItemSaveData(),
                    committed),
                "Wildlife food-raid preflight accepted a changed canonical hash.");
            WildlifeFoodRaidOrderSaveData changedKey = CreateFoodRaidOrder(
                fact.Snapshot(),
                WildlifeFoodRaidOrderState.Leaving,
                WildlifeFoodRaidCommitPhase.Acknowledged,
                childKey,
                parentDelivery.PublicationId,
                parentDelivery.Fingerprint);
            RequireThrows<InvalidOperationException>(
                () => WildlifeFoodRaidFactPreflight.Validate(
                    WildlifeFoodRaidOwner(changedKey),
                    new DungeonPhysicalItemSaveData(),
                    committed),
                "Wildlife food-raid preflight accepted a mismatched parent key.");

            void RequireFoodRaidTamperRejected(
                Action<WildlifeFoodRaidFactData> change,
                string label)
            {
                WildlifeFoodRaidFactData altered = fact.Snapshot();
                change(altered);
                Require(WildlifeFoodRaidFactMapping.ComputeContextSha256(
                            altered)
                        != contextHash,
                    "Wildlife food-raid " + label
                        + " change did not alter full-domain provenance.");
                var result = new DomainFactDelivery[1];
                Require(!authority.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                parentKey,
                                new WildlifeFoodRaidFact(altered))
                        },
                        result,
                        out string changedFailure)
                    && result[0].Phase == DomainFactDeliveryPhase.Rejected
                    && !result[0].MayBeCommitted
                    && !string.IsNullOrWhiteSpace(changedFailure),
                    "Wildlife food-raid replay accepted altered " + label + ".");
                RequireCanonicalEqual(
                    canonical,
                    CanonicalHashes(
                        authority.Ledger.CaptureGameplayOutcomes()),
                    "Rejected wildlife food-raid " + label
                        + " replay changed canonical rows.");
            }
        }
        finally
        {
            connection.Dispose();
        }

        static WildlifeFoodRaidFactData CreateFoodRaidData() => new()
        {
            raidId = "raid:test",
            wildlifeId = "wild:test",
            wildlifeSpeciesId = "species:host-wolf",
            wildlifeDisplayName = "호스트 늑대",
            targetStackId = "stack:food-raid:test",
            targetItemId = "food:grain",
            targetItemDisplayName = "호스트 곡물",
            requestedQuantity = 1,
            startX = 2,
            startY = 3,
            startedGameTime = 10f,
            startedAbsoluteDay = 1,
            endX = 4,
            endY = 5,
            completedGameTime = 12f,
            completedAbsoluteDay = 1,
            stolenQuantity = 1,
            dispositionOperationId =
                "wildlife-food-raid:raid:test:wild:test",
            dispositionReasonCode = "wildlife-food-raid-consumed",
            dispositionCommitId =
                "physical-batch-disposition:3:"
                + "wildlife-food-raid:raid:test:wild:test:1:250",
            dispositionSourceStackIds = new List<string>
            {
                "stack:food-raid:test"
            },
            dispositionQuantity = 1,
            dispositionInputMassGrams = 250L,
            dispositionOwnerRevision = 1L,
            dispositionItemId = "food:grain",
            outcomeReason = "식량 약탈 완료"
        };

        static WildlifeFoodRaidOrderSaveData CreateFoodRaidOrder(
            WildlifeFoodRaidFactData value,
            WildlifeFoodRaidOrderState state,
            WildlifeFoodRaidCommitPhase phase,
            string publicationKey = "",
            string publicationId = "",
            string publicationFingerprint = "") => new()
        {
            outcomeSchemaVersion =
                WildlifeFoodRaidOrderSaveData.CurrentOutcomeSchemaVersion,
            raidId = value.raidId,
            wildlifeId = value.wildlifeId,
            wildlifeSpeciesId = value.wildlifeSpeciesId,
            wildlifeDisplayName = value.wildlifeDisplayName,
            targetStackId = value.targetStackId,
            targetItemId = value.targetItemId,
            targetItemDisplayName = value.targetItemDisplayName,
            requestedQuantity = value.requestedQuantity,
            startX = value.startX,
            startY = value.startY,
            startedGameTime = value.startedGameTime,
            startedAbsoluteDay = value.startedAbsoluteDay,
            endX = value.endX,
            endY = value.endY,
            completedGameTime = value.completedGameTime,
            completedAbsoluteDay = value.completedAbsoluteDay,
            state = state,
            stolenQuantity = value.stolenQuantity,
            outcomeReason = value.outcomeReason,
            commitPhase = phase,
            dispositionOperationId = value.dispositionOperationId,
            dispositionReasonCode = value.dispositionReasonCode,
            dispositionCommitId = value.dispositionCommitId,
            dispositionSourceStackIds = value.dispositionSourceStackIds
                .ToList(),
            dispositionQuantity = value.dispositionQuantity,
            dispositionInputMassGrams = value.dispositionInputMassGrams,
            dispositionOwnerRevision = value.dispositionOwnerRevision,
            dispositionItemId = value.dispositionItemId,
            fact = value.Clone(),
            publicationKey = publicationKey,
            publicationId = publicationId,
            publicationFingerprint = publicationFingerprint
        };

        static DungeonWildlifeSaveData WildlifeFoodRaidOwner(
            WildlifeFoodRaidOrderSaveData order) => new()
        {
            foodRaidOrders = new List<WildlifeFoodRaidOrderSaveData>
            {
                order
            }
        };

        static DungeonPhysicalItemSaveData PhysicalFoodRaidOwner(
            PhysicalItemBatchDispositionSaveData pending) => new()
        {
            pendingBatchDispositions =
                new List<PhysicalItemBatchDispositionSaveData>
                {
                    pending
                }
        };

        static PhysicalItemBatchDispositionSaveData
            CreateFoodRaidPhysicalPending(
                WildlifeFoodRaidFactData value,
                string publicationKey) => new()
        {
            kind = (int)PhysicalItemDispositionKind.Sink,
            operationId = value.dispositionOperationId,
            reasonCode = value.dispositionReasonCode,
            requestFingerprint =
                $"{(int)PhysicalItemDispositionKind.Sink}:"
                + value.dispositionReasonCode + ":"
                + value.targetStackId + "=1",
            sourceStackIds = value.dispositionSourceStackIds.ToList(),
            quantity = value.dispositionQuantity,
            inputMassGrams = value.dispositionInputMassGrams,
            commitId = value.dispositionCommitId,
            ownerRevision = value.dispositionOwnerRevision,
            publicationExpected = true,
            publicationKey = publicationKey,
            defaultObservationDay = value.completedAbsoluteDay
        };

        static PhysicalItemBatchDispositionReceipt
            CreateFoodRaidPhysicalReceipt(WildlifeFoodRaidFactData value)
        {
            var sourceFacts = new[]
            {
                new PhysicalItemDispositionSourceFact(
                    value.targetStackId,
                    value.targetItemId,
                    string.Empty,
                    value.dispositionQuantity,
                    value.dispositionInputMassGrams,
                    new UnityEngine.Vector2Int(value.endX, value.endY),
                    value.targetItemDisplayName)
            };
            ConstructorInfo constructor = typeof(
                    PhysicalItemBatchDispositionReceipt)
                .GetConstructors(
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(candidate => candidate.GetParameters().Length == 9);
            return (PhysicalItemBatchDispositionReceipt)constructor.Invoke(
                new object[]
                {
                    PhysicalItemDispositionKind.Sink,
                    value.dispositionOperationId,
                    value.dispositionReasonCode,
                    $"{(int)PhysicalItemDispositionKind.Sink}:"
                        + value.dispositionReasonCode + ":"
                        + value.targetStackId + "=1",
                    value.dispositionSourceStackIds.ToArray(),
                    value.dispositionQuantity,
                    value.dispositionInputMassGrams,
                    value.dispositionOwnerRevision,
                    sourceFacts
                });
        }

        static GameplayOutcomeSnapshot FindLiveOutcome(
            GameplayOutcomeLedgerSaveData ledger,
            string key) => ledger.outbox.Concat(ledger.exactOutcomes)
            .Single(value => ResultKey(value) == key);
    }

    private static void RequireWildlifePayloadExtension(
        in EnvironmentOutcomePayload native,
        in EnvironmentOutcomePayload mapped,
        string expectedContextHash,
        string label,
        string contextFactId = "wildlife-context-sha256")
    {
        Require(native.ResultKey == mapped.ResultKey
            && native.OutcomeTypeId.Equals(mapped.OutcomeTypeId)
            && native.AbsoluteDay == mapped.AbsoluteDay
            && native.Status == mapped.Status
            && native.OwnerRevision == mapped.OwnerRevision
            && native.Participants.Count == mapped.Participants.Count
            && native.Metrics.Count == mapped.Metrics.Count
            && native.Subjects.Count == mapped.Subjects.Count
            && native.Tags.Count == mapped.Tags.Count
            && native.Provenance.Count == mapped.Provenance.Count
            && mapped.Facts.Count == native.Facts.Count + 1
            && native.Location.Equals(mapped.Location)
            && native.Causation.Equals(mapped.Causation),
            "Wildlife " + label
                + " mapping changed native payload shape or identity.");
        for (int index = 0; index < native.Participants.Count; index++)
            Require(native.Participants.Get(index).Equals(
                    mapped.Participants.Get(index)),
                "Wildlife " + label + " changed a native participant.");
        for (int index = 0; index < native.Metrics.Count; index++)
            Require(native.Metrics.Get(index).Equals(mapped.Metrics.Get(index)),
                "Wildlife " + label + " changed a native metric.");
        for (int index = 0; index < native.Subjects.Count; index++)
            Require(native.Subjects.Get(index).Equals(mapped.Subjects.Get(index)),
                "Wildlife " + label + " changed a native subject.");
        for (int index = 0; index < native.Tags.Count; index++)
            Require(native.Tags.Get(index).Equals(mapped.Tags.Get(index)),
                "Wildlife " + label + " changed a native tag.");
        for (int index = 0; index < native.Provenance.Count; index++)
            Require(native.Provenance.Get(index).Equals(
                    mapped.Provenance.Get(index)),
                "Wildlife " + label + " changed native provenance.");
        for (int index = 0; index < native.Facts.Count; index++)
            Require(native.Facts.Get(index).Equals(mapped.Facts.Get(index)),
                "Wildlife " + label + " changed a native fact.");
        GameplayOutcomeFact context = mapped.Facts.Get(
            mapped.Facts.Count - 1);
        Require(context.FactId.Value == contextFactId
            && context.Value == expectedContextHash
            && context.Value?.Length == 71
            && context.Value.StartsWith("sha256:", StringComparison.Ordinal),
            "Wildlife " + label
                + " mapping omitted its full-domain context hash.");
    }

    private static double WildlifePayloadMetric(
        in EnvironmentOutcomePayload payload,
        GameplayMetricId metricId)
    {
        bool found = false;
        double result = 0d;
        for (int index = 0; index < payload.Metrics.Count; index++)
        {
            GameplayOutcomeMetric value = payload.Metrics.Get(index);
            if (!value.MetricId.Equals(metricId))
                continue;
            Require(!found,
                "Wildlife payload contained a duplicate metric: "
                    + metricId.Value);
            found = true;
            result = value.Value;
        }
        Require(found,
            "Wildlife payload metric was missing: " + metricId.Value);
        return result;
    }

    private static void VerifyOffenseBattleExperienceAndActivityFacts()
    {
        var exactText = new OffenseBattleExperienceFact(
            "battle:host-exact-text",
            "expedition:host-exact-text",
            "character:host-exact-text",
            "  호스트 생존자  ",
            "offense-target:host-exact-text",
            "  호스트 목표  ",
            "  호스트 권역  ",
            "faction:host-exact-text",
            "node:host-exact-text",
            "  호스트 교전 노드  ",
            victory: true,
            damageTaken: 12.5f);
        var exactActivity = new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.PartyStranded,
            "expedition:host-activity-text",
            targetTitle: "  호스트 목표  ",
            nodeTitle: "  호스트 노드  ",
            message: "  동결된 메시지  ",
            count: 3);
        Require(exactText.CharacterName == "  호스트 생존자  "
            && exactText.TargetTitle == "  호스트 목표  "
            && exactText.RegionName == "  호스트 권역  "
            && exactText.NodeTitle == "  호스트 교전 노드  "
            && exactActivity.TargetTitle == "  호스트 목표  "
            && exactActivity.NodeTitle == "  호스트 노드  "
            && exactActivity.Message == "  동결된 메시지  ",
            "Offense battle/activity facts normalized frozen text.");

        RequireThrows<ArgumentException>(
            () => CreateOffenseBattleExperience(
                victory: true,
                damageTaken: -0.01f),
            "Offense battle experience accepted negative damage.");
        RequireThrows<ArgumentException>(
            () => CreateOffenseBattleExperience(
                victory: true,
                damageTaken: float.NaN),
            "Offense battle experience accepted NaN damage.");
        RequireThrows<ArgumentException>(
            () => new OffenseBattleExperienceFact(
                "battle id invalid",
                "expedition:host-battle",
                "character:host-battle-member",
                "호스트 생존자",
                "offense-target:host-battle",
                "호스트 전투 목표",
                "호스트 전투 권역",
                "faction:host-battle",
                "node:host-battle",
                "호스트 전투 노드",
                victory: true,
                damageTaken: 1f),
            "Offense battle experience accepted an invalid identity.");
        RequireThrows<ArgumentException>(
            () => new OffenseExpeditionActivityFact(
                (OffenseExpeditionActivity)999,
                "expedition:host-activity"),
            "Offense activity accepted an invalid kind.");
        RequireThrows<ArgumentException>(
            () => new OffenseExpeditionActivityFact(
                OffenseExpeditionActivity.PartyStranded,
                "expedition id invalid"),
            "Offense activity accepted an invalid identity.");
        RequireThrows<ArgumentException>(
            () => new OffenseExpeditionActivityFact(
                OffenseExpeditionActivity.ObjectiveDestroyed,
                "expedition:host-activity",
                count: -1),
            "Offense activity accepted a negative count.");

        GameplayNarrativeEventContext victoryContext =
            CreateOffenseBattleContext(CreateOffenseBattleExperience(
                victory: true,
                damageTaken: 12.5f));
        GameplayNarrativeEventContext survivedContext =
            CreateOffenseBattleContext(CreateOffenseBattleExperience(
                victory: false,
                damageTaken: 7.25f,
                regionName: string.Empty));
        Require(victoryContext.eventInstanceId
                == "battle:host-battle:result:character:host-battle-member"
            && victoryContext.chainId == "expedition:host-battle"
            && victoryContext.locationId
                == "offense-target:host-battle"
            && victoryContext.locationDisplayName == "호스트 전투 권역"
            && victoryContext.actorId
                == "character:host-battle-member"
            && victoryContext.actorDisplayName == "호스트 생존자"
            && victoryContext.counterpartyId
                == "faction:host-battle"
            && victoryContext.counterpartyDisplayName
                == "호스트 전투 목표"
            && victoryContext.usedObjectId == "node:host-battle"
            && victoryContext.usedObjectDisplayName == "호스트 전투 노드"
            && victoryContext.resultDetail == "교전 승리"
            && survivedContext.locationDisplayName == "호스트 전투 목표"
            && survivedContext.resultDetail == "교전 생존",
            "Offense battle experience context changed its frozen victory/survival provenance.");

        var events = new GameEventBus();
        var alerts = new List<EventAlertRequestedEvent>();
        using IDisposable alertSubscription = events.Subscribe<
            EventAlertRequestedEvent>(alerts.Add);
        var observer = new OffenseExpeditionActivityObserver(events);
        observer.Initialize();
        observer.Initialize();
        foreach (OffenseExpeditionActivityFact fact in
                 CreateOffenseActivityFacts())
        {
            events.Publish(fact);
        }

        var expected = new[]
        {
            new ActivityAlertExpectation(
                "기억 소거 인장 획득",
                "이 권역의 첫 보스를 격파해 기억 소거 인장 1개가 전리품 투하 지점에 생성되었습니다.",
                EventAlertImportance.High,
                "오펜스"),
            new ActivityAlertExpectation(
                "원정대 조난",
                "거친 지형 때문에 원정대가 조난되었습니다.",
                EventAlertImportance.High,
                "오펜스"),
            new ActivityAlertExpectation(
                "목표 파괴",
                "호스트 요새 격파. 안전 이동 3칸이 지급되었습니다. 다음 목적지를 선택하세요.",
                EventAlertImportance.High,
                "오펜스"),
            new ActivityAlertExpectation(
                "원정 교전 승리",
                "호스트 관문을 돌파했습니다. 다음 경로를 선택하세요.",
                EventAlertImportance.Low,
                "오펜스"),
            new ActivityAlertExpectation(
                "출정 복원 실패",
                "출정 집결 서비스를 찾을 수 없어 원정대가 대기합니다.",
                EventAlertImportance.High,
                "expedition"),
            new ActivityAlertExpectation(
                "출정 보급 오류",
                "저장된 보급품을 다시 소비하지 못했습니다.",
                EventAlertImportance.High,
                "expedition"),
            new ActivityAlertExpectation(
                "출정 복원 실패",
                "저장된 출정 절차를 재개하지 못했습니다.",
                EventAlertImportance.High,
                "expedition")
        };
        Require(alerts.Count == expected.Length,
            "Duplicate activity observer initialization changed alert count.");
        for (int index = 0; index < expected.Length; index++)
        {
            EventAlertRequest actual = alerts[index].request;
            ActivityAlertExpectation wanted = expected[index];
            Require(actual != null
                && actual.Title == wanted.Title
                && actual.Detail == wanted.Detail
                && actual.Importance == wanted.Importance
                && actual.Category == wanted.Category,
                "Offense activity alert mapping changed at index "
                    + index.ToString(CultureInfo.InvariantCulture) + ".");
        }

        observer.Dispose();
        events.Publish(new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.PartyStranded,
            "expedition:host-activity-disposed",
            message: "해제 뒤에는 보이면 안 됩니다."));
        Require(alerts.Count == expected.Length,
            "Disposed offense activity observer still emitted an alert.");
        observer.Initialize();
        events.Publish(new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.PartyStranded,
            "expedition:host-activity-reinitialized",
            message: "재연결 뒤 한 번만 발행합니다."));
        Require(alerts.Count == expected.Length + 1
            && alerts[^1].request.Title == "원정대 조난"
            && alerts[^1].request.Detail == "재연결 뒤 한 번만 발행합니다."
            && alerts[^1].request.Importance == EventAlertImportance.High
            && alerts[^1].request.Category == "오펜스",
            "Reinitialized offense activity observer did not emit exactly once.");
        observer.Dispose();
    }

    private static void VerifyOffenseNodePublicationRuntime()
    {
        OffenseNodeResolutionFactData caller = CreateOffenseNodeData(
            "expedition:host-node-frozen",
            "node:host-frozen",
            sequence: 1L);
        var frozenProposal = new OffenseNodeResolutionFact(caller);
        caller.targetTitle = "변조된 원정 목표";
        caller.participants[0].displayName = "변조된 원정대원";
        caller.participants[0].experienceEligible = false;
        caller.participants[0].experienceDisplayName = "변조된 경험 이름";
        caller.experienceReward = 999;
        caller.participants.Clear();
        OffenseNodeResolutionFactData detached = frozenProposal.Snapshot();
        detached.nodeTitle = "분리 스냅샷 변조";
        detached.participants.Clear();
        OffenseNodeResolutionFactData exact = frozenProposal.Snapshot();
        Require(exact.targetTitle == "호스트 노드 목표"
            && exact.nodeTitle == "호스트 사건 노드"
            && exact.experienceReward == 5
            && exact.participants.Count == 1
            && exact.participants[0].displayName == "호스트 원정대원"
            && exact.participants[0].experienceEligible
            && exact.participants[0].experienceDisplayName
                == "호스트 원정대원",
            "Offense node proposal retained caller or snapshot mutations.");

        SavedOffenseNodeAuthority canonical;
        using (var authority = new OffenseNodeAuthority(
                   recorderFaults: false))
        {
            var events = new GameEventBus();
            int notifications = 0;
            using IDisposable notification = events.Subscribe<
                OffenseNodeExperienceObservedFact>(observed =>
            {
                notifications++;
                Require(observed.Experience.experienceAfter == 9,
                    "Offense node experience notification changed its frozen after value.");
                throw new InvalidOperationException(
                    "host-offense-node-notification-fault");
            });
            var faults = new OffenseNodeFaultPublisher(
                authority.Dispatcher);
            OffenseNodePublicationRuntime nodeRuntime = null;
            faults.DuringPublish = () => nodeRuntime.Capture();
            nodeRuntime = new OffenseNodePublicationRuntime(
                faults,
                authority.Dispatcher,
                events);
            Require(nodeRuntime.NextSequence("expedition:host-node") == 1L,
                "Offense node initial sequence was not one.");
            OffenseNodeResolutionFact firstProposal =
                CreateOffenseNodeProposal(
                    "expedition:host-node",
                    "node:host-event",
                    sequence: 1L);
            Require(nodeRuntime.TryBegin(
                    firstProposal,
                    out string firstBeginFailure),
                "Offense node first proposal was not reserved: "
                    + firstBeginFailure);
            RequireThrows<InvalidOperationException>(
                () => nodeRuntime.PublishApplied(firstProposal),
                "Unresolved offense node fact was accepted as applied.");
            OffenseNodeResolutionFact firstActual = firstProposal.Resolve(
                new OffenseExpeditionNodeResult(
                    "사건 노드를 해결했습니다.",
                    usedSupply: true,
                    gainedLoot: true));
            DomainFactDelivery firstDelivery =
                nodeRuntime.PublishApplied(firstActual);
            nodeRuntime.RecordExperience(CreateOffenseNodeExperience());
            nodeRuntime.End();
            Require(firstDelivery.Completed
                && faults.ActiveGuardRejected
                && nodeRuntime.NextSequence("expedition:host-node") == 2L
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1
                && authority.Migrated.Capture().owners.Count == 1,
                "Offense node first fact was not durably published: "
                    + firstDelivery.Detail);
            Require(nodeRuntime.RecoverPending(out string notifyFailure)
                && notifyFailure.Length == 0
                && notifications == 1
                && nodeRuntime.LastFailure.StartsWith(
                    "offense-node-followup:",
                    StringComparison.Ordinal)
                && !nodeRuntime.Capture().observations[0].notificationPending,
                "Offense node notification failure was not retained without replaying effects: "
                    + notifyFailure);
            Require(nodeRuntime.RecoverPending(out string repeatNotifyFailure)
                && repeatNotifyFailure.Length == 0
                && notifications == 1
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense node experience notification was duplicated: "
                    + repeatNotifyFailure);

            OffenseNodePublicationSaveData detachedCapture =
                nodeRuntime.Capture();
            detachedCapture.observations[0].fact.message = "변조된 결과";
            detachedCapture.observations[0].experience[0].experienceAfter = 900;
            OffenseNodePublicationSaveData unchanged = nodeRuntime.Capture();
            Require(unchanged.observations[0].fact.message
                    == "사건 노드를 해결했습니다."
                && unchanged.observations[0].experience[0].experienceAfter == 9,
                "Offense node capture exposed mutable owner state.");

            OffenseNodeResolutionFact duplicateNode =
                CreateOffenseNodeProposal(
                    "expedition:host-node",
                    "node:host-event",
                    sequence: 2L);
            Require(!nodeRuntime.TryBegin(
                    duplicateNode,
                    out string duplicateFailure)
                && duplicateFailure.Contains(
                    "offense-node-identity-or-pending-conflict",
                    StringComparison.Ordinal)
                && nodeRuntime.Capture().observations.Count == 1,
                "Offense node duplicate node guard changed owner state.");

            OffenseNodeResolutionFact secondProposal =
                CreateOffenseNodeProposal(
                    "expedition:host-node",
                    "node:host-cache",
                    sequence: 2L,
                    OffenseRouteNodeKind.Cache,
                    useSupply: false);
            Require(nodeRuntime.TryBegin(
                    secondProposal,
                    out string secondBeginFailure),
                "Offense node second proposal was not reserved: "
                    + secondBeginFailure);
            OffenseNodeResolutionFact secondActual = secondProposal.Resolve(
                new OffenseExpeditionNodeResult(
                    "보급 은닉처를 확보했습니다.",
                    usedSupply: false,
                    gainedLoot: true));
            DomainFactDelivery secondDelivery =
                nodeRuntime.PublishApplied(secondActual);
            nodeRuntime.End();
            Require(secondDelivery.Completed
                && nodeRuntime.RecoverPending(out string secondFailure)
                && secondFailure.Length == 0
                && notifications == 1
                && nodeRuntime.NextSequence("expedition:host-node") == 3L
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 2
                && authority.Migrated.Capture().owners.Count == 1
                && authority.Migrated.Capture().owners[0].revision == 2L,
                "Offense node second sequence was not published exactly once: "
                    + secondDelivery.Detail);

            GameplayResultKey firstKey = OffenseNodeFactObserver.Key(
                firstActual);
            GameplayResultKey secondKey = OffenseNodeFactObserver.Key(
                secondActual);
            Require(firstKey == MigratedProducerOutcomeCatalog.ResultKey(
                    MigratedProducerOutcomeKind.OffenseExpeditionNodeResult,
                    "offense-expedition-node:expedition:host-node", 1L)
                && secondKey == MigratedProducerOutcomeCatalog.ResultKey(
                    MigratedProducerOutcomeKind.OffenseExpeditionNodeResult,
                    "offense-expedition-node:expedition:host-node", 2L)
                && secondKey.OperationId != firstKey.OperationId
                && firstKey.CommitRevision == 1L
                && secondKey.CommitRevision == 2L
                && nodeRuntime.Capture().observations[0].publicationKey
                    == firstKey.ToString()
                && nodeRuntime.Capture().observations[1].publicationKey
                    == secondKey.ToString(),
                "Offense node canonical keys did not preserve the existing owner and revision identity contract.");

            OffenseNodeResolutionFact activeProposal =
                CreateOffenseNodeProposal(
                    "expedition:host-node",
                    "node:host-active",
                    sequence: 3L);
            Require(nodeRuntime.TryBegin(
                    activeProposal,
                    out string activeFailure),
                "Offense node active-capture proposal failed: "
                    + activeFailure);
            RequireThrows<InvalidOperationException>(
                () => nodeRuntime.Capture(),
                "Offense node capture accepted an active command.");
            nodeRuntime.End();

            canonical = JsonConvert.DeserializeObject<
                SavedOffenseNodeAuthority>(JsonConvert.SerializeObject(
                new SavedOffenseNodeAuthority
                {
                    serializer = HostSerializer,
                    publications = nodeRuntime.Capture(),
                    ledger = authority.Ledger.CaptureGameplayOutcomes(),
                    migrated = authority.Migrated.Capture()
                })) ?? throw new InvalidOperationException(
                    "Offense node canonical host JSON returned null.");
        }

        Require(canonical.serializer == HostSerializer
            && canonical.publications.observations.Count == 2
            && canonical.publications.observations[0].experience.Count == 1
            && canonical.publications.observations[0].experience[0]
                .experienceAfter == 9,
            "Offense node host JSON lost frozen facts or experience.");
        DungeonGameRestoreReport canonicalJoin = ValidateOffenseNodeJoin(
            canonical.publications,
            canonical.ledger,
            canonical.migrated);
        Require(canonicalJoin.Success,
            "Offense node canonical save join was rejected: "
                + string.Join(" | ", canonicalJoin.Errors));
        foreach (long badRevision in new[] { 1L, 3L })
        {
            var wrongCounter = JsonConvert.DeserializeObject<DungeonMigratedProducerOutcomeSaveData>(
                JsonConvert.SerializeObject(canonical.migrated));
            wrongCounter.owners[0].revision = badRevision;
            Require(!ValidateOffenseNodeJoin(canonical.publications, canonical.ledger, wrongCounter).Success,
                "Offense node save accepted a missing or ahead canonical counter.");
        }
        var earlierPending = JsonConvert.DeserializeObject<OffenseNodePublicationSaveData>(
            JsonConvert.SerializeObject(canonical.publications));
        earlierPending.observations[0].pending = true;
        earlierPending.observations[0].notificationPending = true;
        Require(!ValidateOffenseNodeJoin(earlierPending, canonical.ledger, canonical.migrated).Success,
            "Offense node save accepted a later node behind an unresolved earlier node.");
        OffenseNodePublicationSaveData tamperedJoin = JsonConvert
            .DeserializeObject<OffenseNodePublicationSaveData>(
                JsonConvert.SerializeObject(canonical.publications))
            ?? throw new InvalidOperationException(
                "Offense node tampered join clone returned null.");
        tamperedJoin.observations[0].fact.nodeDescription += " · 변조";
        DungeonGameRestoreReport rejectedJoin = ValidateOffenseNodeJoin(
            tamperedJoin,
            canonical.ledger,
            canonical.migrated);
        Require(!rejectedJoin.Success,
            "Offense node tampered save join was accepted.");
        OffenseNodePublicationSaveData experienceTamper = JsonConvert
            .DeserializeObject<OffenseNodePublicationSaveData>(
                JsonConvert.SerializeObject(canonical.publications))
            ?? throw new InvalidOperationException(
                "Offense node experience tamper clone returned null.");
        experienceTamper.observations[0].experience[0].requestedAmount--;
        DungeonGameRestoreReport rejectedExperience =
            ValidateOffenseNodeJoin(
                experienceTamper,
                canonical.ledger,
                canonical.migrated);
        Require(!rejectedExperience.Success,
            "Offense node auxiliary experience importance tamper was accepted.");
        VerifyOffenseNodeCanonicalTamper(canonical);
        VerifyOffenseNodeKnownRejections();
        VerifyOffenseNodeAmbiguousRecovery(faultAfterCommit: false);
        VerifyOffenseNodeAmbiguousRecovery(faultAfterCommit: true);
    }

    private static void VerifyOffenseNodeCanonicalTamper(
        SavedOffenseNodeAuthority canonical)
    {
        using var authority = new OffenseNodeAuthority(
            recorderFaults: false);
        authority.Restore(canonical.ledger, canonical.migrated);
        OffenseNodeObservationData row = canonical.publications
            .observations[0];
        IReadOnlyDictionary<string, string> expected = CanonicalHashes(
            canonical.ledger);
        foreach (Action<OffenseNodeResolutionFactData> mutate in new Action<
                     OffenseNodeResolutionFactData>[]
                 {
                     value => value.message += " · 변조",
                     value => value.nodeTitle += " · 변조",
                     value => value.participants[0].displayName += " · 변조"
                 })
        {
            OffenseNodeResolutionFactData changed = row.fact.Clone();
            mutate(changed);
            RequireOffenseNodeReplayRejected(
                authority,
                row.publicationKey,
                new OffenseNodeResolutionFact(changed),
                "Offense node same-key actual/context tamper was accepted.");
        }
        RequireCanonicalEqual(
            expected,
            CanonicalHashes(authority.Ledger.CaptureGameplayOutcomes()),
            "Offense node tamper attempts changed canonical rows.");
    }

    private static void VerifyOffenseNodeKnownRejections()
    {
        using var authority = new OffenseNodeAuthority(
            recorderFaults: false);
        var faults = new OffenseNodeFaultPublisher(authority.Dispatcher)
        {
            RejectReserve = true
        };
        var nodeRuntime = new OffenseNodePublicationRuntime(
            faults,
            authority.Dispatcher,
            new GameEventBus());
        OffenseNodeResolutionFact reserveProposal =
            CreateOffenseNodeProposal(
                "expedition:host-node-reserve",
                "node:host-reserve",
                sequence: 1L);
        Require(!nodeRuntime.TryBegin(
                reserveProposal,
                out string reserveFailure)
            && reserveFailure.Contains(
                "host-offense-node-reserve-rejected",
                StringComparison.Ordinal)
            && nodeRuntime.Capture().observations.Count == 0
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 0
            && authority.Migrated.Capture().owners.Count == 0,
            "Offense node reserve rejection retained saved facts.");

        faults.RejectReserve = false;
        faults.RejectPrepare = true;
        OffenseNodeResolutionFact prepareProposal =
            CreateOffenseNodeProposal(
                "expedition:host-node-prepare",
                "node:host-prepare",
                sequence: 1L);
        Require(nodeRuntime.TryBegin(
                prepareProposal,
                out string prepareFailure),
            "Offense node prepare-reject proposal did not reserve: "
                + prepareFailure);
        DomainFactDelivery rejected = nodeRuntime.PublishApplied(
            prepareProposal.Resolve(new OffenseExpeditionNodeResult(
                "준비 거절 노드",
                usedSupply: true,
                gainedLoot: false)));
        nodeRuntime.End();
        Require(!rejected.MayBeCommitted
            && nodeRuntime.Capture().observations.Count == 0
            && nodeRuntime.NextSequence(
                "expedition:host-node-prepare") == 1L
            && authority.Dispatcher.ActiveLeaseCount == 0
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 0
            && authority.Migrated.Capture().owners.Count == 0,
            "Offense node prepare rejection retained saved facts or counters.");
    }

    private static void VerifyOffenseNodeAmbiguousRecovery(
        bool faultAfterCommit)
    {
        string edge = faultAfterCommit ? "after" : "before";
        SavedOffenseNodeAuthority saved;
        using (var authority = new OffenseNodeAuthority(
                   recorderFaults: !faultAfterCommit))
        {
            IDomainFactPublisher publisher = authority.Dispatcher;
            OffenseNodeFaultPublisher replyLoss = null;
            if (faultAfterCommit)
            {
                replyLoss = new OffenseNodeFaultPublisher(
                    authority.Dispatcher)
                {
                    HideCompletedReply = true
                };
                publisher = replyLoss;
            }
            else
            {
                authority.Faults.ThrowBeforeNextSingleCommit = true;
            }
            var events = new GameEventBus();
            int notifications = 0;
            using IDisposable notification = events.Subscribe<
                OffenseNodeExperienceObservedFact>(_ => notifications++);
            var nodeRuntime = new OffenseNodePublicationRuntime(
                publisher,
                authority.Dispatcher,
                events);
            string expeditionId = "expedition:host-node-" + edge;
            OffenseNodeResolutionFact proposal = CreateOffenseNodeProposal(
                expeditionId,
                "node:host-" + edge,
                sequence: 1L);
            Require(nodeRuntime.TryBegin(
                    proposal,
                    out string beginFailure),
                "Offense node " + edge
                    + "-commit proposal failed: " + beginFailure);
            DomainFactDelivery ambiguous = nodeRuntime.PublishApplied(
                proposal.Resolve(new OffenseExpeditionNodeResult(
                    "불명 응답 노드",
                    usedSupply: true,
                    gainedLoot: false)));
            nodeRuntime.RecordExperience(CreateOffenseNodeExperience());
            nodeRuntime.End();
            Require(ambiguous.MayBeCommitted
                && !ambiguous.Completed
                && notifications == 0
                && nodeRuntime.Capture().observations.Count == 1
                && nodeRuntime.Capture().observations[0].pending
                && nodeRuntime.Capture().observations[0]
                    .notificationPending
                && (faultAfterCommit
                    ? replyLoss.HiddenReplyCount == 1
                        && OutcomeRowCount(
                            authority.Ledger.CaptureGameplayOutcomes()) == 1
                    : authority.Faults.BeforeCommitFaultCount == 1
                        && OutcomeRowCount(
                            authority.Ledger.CaptureGameplayOutcomes()) == 0),
                "Offense node " + edge
                    + "-commit ambiguity did not retain its applied fact: "
                    + ambiguous.Detail);
            saved = JsonConvert.DeserializeObject<SavedOffenseNodeAuthority>(
                JsonConvert.SerializeObject(new SavedOffenseNodeAuthority
                {
                    serializer = HostSerializer,
                    publications = nodeRuntime.Capture(),
                    ledger = authority.Ledger.CaptureGameplayOutcomes(),
                    migrated = authority.Migrated.Capture()
                })) ?? throw new InvalidOperationException(
                    "Offense node " + edge
                        + "-commit host JSON returned null.");
        }

        DungeonGameRestoreReport pendingJoin = ValidateOffenseNodeJoin(
            saved.publications,
            saved.ledger,
            saved.migrated);
        Require(pendingJoin.Success,
            "Offense node " + edge
                + "-commit pending save join was rejected: "
                + string.Join(" | ", pendingJoin.Errors));

        using var restored = new OffenseNodeAuthority(
            recorderFaults: false);
        restored.Restore(saved.ledger, saved.migrated);
        var restoredEvents = new GameEventBus();
        int restoredNotifications = 0;
        using IDisposable restoredSubscription = restoredEvents.Subscribe<
            OffenseNodeExperienceObservedFact>(observed =>
        {
            restoredNotifications++;
            Require(observed.Experience.experienceBefore == 4
                && observed.Experience.experienceAfter == 9,
                "Recovered offense node experience changed its frozen values.");
        });
        OffenseNodePublicationRuntime restoredRuntime = null;
        var recovery = new CaptureDuringOffenseNodeRecovery(
            restored.Dispatcher,
            () => restoredRuntime.Capture());
        restoredRuntime = new OffenseNodePublicationRuntime(
            restored.Dispatcher,
            recovery,
            restoredEvents);
        restoredRuntime.Restore(saved.publications);
        Require(restoredRuntime.RecoverPending(out string recoveryFailure)
            && recoveryFailure.Length == 0
            && recovery.CaptureRejected
            && restoredNotifications == 1
            && restoredRuntime.Capture().observations.Count == 1
            && !restoredRuntime.Capture().observations[0].pending
            && !restoredRuntime.Capture().observations[0]
                .notificationPending
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1
            && restored.Migrated.Capture().owners.Count == 1
            && restored.Migrated.Capture().owners[0].revision == 1L,
            "Offense node " + edge
                + "-commit recovery was not exact: " + recoveryFailure);
        Require(restoredRuntime.RecoverPending(out string repeatedFailure)
            && repeatedFailure.Length == 0
            && restoredNotifications == 1
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1
            && restored.Migrated.Capture().owners.Count == 1,
            "Offense node " + edge
                + "-commit recovery duplicated publication or notification: "
                + repeatedFailure);
    }

    private static void VerifyOffenseArrivalSettlementRuntime()
    {
        OffenseArrivalSettlementFactData caller =
            CreateOffenseArrivalSettlementFactData("return:90");
        var frozenFact = new OffenseArrivalSettledFact(caller);
        caller.absoluteDay = 999;
        caller.materializedIds[0] = "wild:999";
        caller.escapedIds.Clear();
        OffenseArrivalSettlementFactData detached = frozenFact.Snapshot();
        detached.materializedIds.Clear();
        detached.escapedIds[0] = "wild:999";
        OffenseArrivalSettlementFactData exact = frozenFact.Snapshot();
        Require(exact.absoluteDay == 103
            && exact.materializedIds.SequenceEqual(
                new[] { "wild:1" },
                StringComparer.Ordinal)
            && exact.escapedIds.SequenceEqual(
                new[] { "wild:1" },
                StringComparer.Ordinal),
            "Offense arrival-settlement fact retained caller or snapshot mutations.");

        SavedOffenseArrivalSettlementAuthority canonical;
        using (var authority = new OffenseArrivalSettlementAuthority(
                   recorderFaults: false))
        {
            var publisher = new OffenseArrivalSettlementFaultPublisher(
                authority.Dispatcher);
            OffenseArrivalSettlementRuntime owner = null;
            publisher.DuringPublish = () => owner.RequireIdle();
            owner = new OffenseArrivalSettlementRuntime(
                publisher,
                authority.Dispatcher);
            OffenseReturnArrivalState arrival =
                CreateOffenseArrivalState("return:1");
            DomainFactDelivery delivered = owner.Settle(
                arrival,
                secured: 0,
                escaped: 1,
                day: 103);
            Require(delivered.Completed
                && publisher.BusyGuardRejected
                && arrival.stage == OffenseReturnArrivalStage.Escaped
                && arrival.settledMaterializedAmount == 1
                && arrival.settledSecuredAmount == 0
                && arrival.settledEscapedAmount == 1
                && arrival.escapeRisk == 0f
                && arrival.settlement.hasFact
                && !arrival.settlement.pending
                && arrival.settlement.projectionPending
                && arrival.settlement.publicationKey
                    == OffenseArrivalSettlementFactObserver.Key(
                        new OffenseArrivalSettledFact(
                            arrival.settlement.fact)).ToString()
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1
                && authority.Migrated.Capture().owners.Count == 1
                && authority.Migrated.Capture().owners[0].revision == 1L,
                "Offense arrival settlement did not publish fixed revision one: "
                    + delivered.Detail);
            OffenseArrivalSettlementRuntime.ValidateState(arrival);

            OffenseArrivalSettlementObservationData detachedObservation =
                arrival.settlement.Clone();
            detachedObservation.fact.absoluteDay = 700;
            detachedObservation.fact.materializedIds.Clear();
            Require(arrival.settlement.fact.absoluteDay == 103
                && arrival.settlement.fact.materializedIds.Count == 1,
                "Offense arrival settlement clone exposed owner state.");

            string beforeDuplicate = JsonConvert.SerializeObject(arrival);
            DomainFactDelivery duplicate = owner.Settle(
                arrival,
                secured: 0,
                escaped: 1,
                day: 103);
            Require(!duplicate.MayBeCommitted
                && JsonConvert.SerializeObject(arrival) == beforeDuplicate
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1
                && authority.Migrated.Capture().owners.Count == 1,
                "Offense arrival duplicate settlement mutated durable state.");

            int projectionAttempts = 0;
            Require(!owner.DeliverProjection(arrival, () =>
                {
                    projectionAttempts++;
                    throw new InvalidOperationException(
                        "host-offense-arrival-projection-fault");
                })
                && arrival.settlement.projectionPending
                && owner.LastFailure.StartsWith(
                    "offense-arrival-projection:",
                    StringComparison.Ordinal),
                "Offense arrival projection failure was not retained for retry.");
            Require(owner.DeliverProjection(arrival, () =>
                    projectionAttempts++)
                && !arrival.settlement.projectionPending
                && owner.DeliverProjection(arrival, () =>
                    projectionAttempts++)
                && projectionAttempts == 2,
                "Offense arrival projection retry was not idempotent.");

            var expeditionSave = new DungeonOffenseSaveData
            {
                resultHistory = new List<
                    DungeonOffenseExpeditionResultSaveData>
                {
                    new()
                    {
                        expeditionId = arrival.expeditionId,
                        targetId = arrival.targetId,
                        arrivalReceipts = new List<
                            DungeonOffenseArrivalReceiptSaveData>
                        {
                            new()
                            {
                                arrivalId = arrival.arrivalId,
                                kind = arrival.kind.ToString(),
                                requestedAmount = arrival.requestedAmount,
                                resolution =
                                    OffenseExpeditionArrivalResolution.Pending
                            }
                        }
                    }
                }
            };
            var arrivalSave = new DungeonOffenseReturnArrivalSaveData
            {
                nextArrivalSequence = 2,
                arrivals = new List<OffenseReturnArrivalState> { arrival }
            };
            OffenseArrivalSettlementRuntime.ProjectTerminalReceipts(
                expeditionSave,
                arrivalSave);
            DungeonOffenseArrivalReceiptSaveData projected = expeditionSave
                .resultHistory.Single().arrivalReceipts.Single();
            Require(projected.arrivalId == arrival.arrivalId
                && projected.materializedAmount == 1
                && projected.securedAmount == 0
                && projected.escapedAmount == 1
                && projected.resolution
                    == OffenseExpeditionArrivalResolution.Escaped,
                "Offense arrival terminal receipt projection was not exact.");

            canonical = JsonConvert.DeserializeObject<
                SavedOffenseArrivalSettlementAuthority>(
                JsonConvert.SerializeObject(
                    new SavedOffenseArrivalSettlementAuthority
                    {
                        serializer = HostSerializer,
                        arrival = arrival,
                        ledger = authority.Ledger.CaptureGameplayOutcomes(),
                        migrated = authority.Migrated.Capture()
                    })) ?? throw new InvalidOperationException(
                        "Offense arrival canonical host JSON returned null.");
        }

        Require(canonical.serializer == HostSerializer
            && canonical.arrival.settlement.hasFact
            && !canonical.arrival.settlement.pending
            && !canonical.arrival.settlement.projectionPending
            && canonical.arrival.settlement.fact.arrivalId == "return:1",
            "Offense arrival host JSON lost its terminal observation.");
        DungeonGameRestoreReport canonicalJoin = ValidateOffenseArrivalJoin(
            canonical.arrival,
            canonical.ledger,
            canonical.migrated);
        Require(canonicalJoin.Success,
            "Offense arrival canonical save join was rejected: "
                + string.Join(" | ", canonicalJoin.Errors));
        OffenseReturnArrivalState tamperedJoin = JsonConvert
            .DeserializeObject<OffenseReturnArrivalState>(
                JsonConvert.SerializeObject(canonical.arrival))
            ?? throw new InvalidOperationException(
                "Offense arrival tampered join clone returned null.");
        tamperedJoin.settlement.fact.absoluteDay++;
        DungeonGameRestoreReport rejectedJoin = ValidateOffenseArrivalJoin(
            tamperedJoin,
            canonical.ledger,
            canonical.migrated);
        Require(!rejectedJoin.Success,
            "Offense arrival tampered save join was accepted.");

        using (var restored = new OffenseArrivalSettlementAuthority(
                   recorderFaults: false))
        {
            restored.Restore(canonical.ledger, canonical.migrated);
            OffenseArrivalSettlementObservationData row =
                canonical.arrival.settlement;
            IReadOnlyDictionary<string, string> expectedCanonical =
                CanonicalHashes(canonical.ledger);
            OffenseArrivalSettlementFactData changed = row.fact.Clone();
            changed.absoluteDay++;
            RequireOffenseArrivalReplayRejected(
                restored,
                row.publicationKey,
                new OffenseArrivalSettledFact(changed),
                "Offense arrival same-key payload tamper was accepted.");
            RequireCanonicalEqual(
                expectedCanonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Offense arrival payload tamper changed canonical rows.");

            var freshOwner = new OffenseArrivalSettlementRuntime(
                restored.Dispatcher,
                restored.Dispatcher);
            OffenseReturnArrivalState unapplied =
                CreateOffenseArrivalState("return:1");
            DomainFactDelivery replayBeforeOwner = freshOwner.Settle(
                unapplied,
                secured: 0,
                escaped: 1,
                day: 103);
            Require(!replayBeforeOwner.MayBeCommitted
                && IsOriginalAwaitingArrival(unapplied)
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1
                && restored.Migrated.Capture().owners.Count == 1,
                "Fresh arrival owner reused an existing canonical identity: "
                    + replayBeforeOwner.Detail);
        }

        VerifyOffenseArrivalKnownRejections();
        VerifyOffenseArrivalAmbiguousRecovery(faultAfterCommit: false);
        VerifyOffenseArrivalAmbiguousRecovery(faultAfterCommit: true);
    }

    private static void VerifyOffenseArrivalKnownRejections()
    {
        using var authority = new OffenseArrivalSettlementAuthority(
            recorderFaults: false);
        var faults = new OffenseArrivalSettlementFaultPublisher(
            authority.Dispatcher);
        var owner = new OffenseArrivalSettlementRuntime(
            faults,
            authority.Dispatcher);

        faults.RejectReserve = true;
        OffenseReturnArrivalState reserve =
            CreateOffenseArrivalState("return:2");
        DomainFactDelivery reserveRejected = owner.Settle(
            reserve,
            secured: 0,
            escaped: 1,
            day: 104);
        Require(!reserveRejected.MayBeCommitted
            && IsOriginalAwaitingArrival(reserve)
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 0
            && authority.Migrated.Capture().owners.Count == 0,
            "Offense arrival reserve rejection retained owner/canonical state.");

        faults.RejectReserve = false;
        faults.RejectPrepare = true;
        OffenseReturnArrivalState prepare =
            CreateOffenseArrivalState("return:3");
        DomainFactDelivery prepareRejected = owner.Settle(
            prepare,
            secured: 0,
            escaped: 1,
            day: 105);
        Require(!prepareRejected.MayBeCommitted
            && IsOriginalAwaitingArrival(prepare)
            && authority.Dispatcher.ActiveLeaseCount == 0
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 0
            && authority.Migrated.Capture().owners.Count == 0,
            "Offense arrival prepare rejection retained owner/canonical state.");
    }

    private static void VerifyOffenseArrivalAmbiguousRecovery(
        bool faultAfterCommit)
    {
        string edge = faultAfterCommit ? "after" : "before";
        SavedOffenseArrivalSettlementAuthority saved;
        using (var authority = new OffenseArrivalSettlementAuthority(
                   recorderFaults: !faultAfterCommit))
        {
            IDomainFactPublisher publisher = authority.Dispatcher;
            OffenseArrivalSettlementFaultPublisher replyLoss = null;
            if (faultAfterCommit)
            {
                replyLoss = new OffenseArrivalSettlementFaultPublisher(
                    authority.Dispatcher)
                {
                    HideCompletedReply = true
                };
                publisher = replyLoss;
            }
            else
            {
                authority.Faults.ThrowBeforeNextSingleCommit = true;
            }

            var owner = new OffenseArrivalSettlementRuntime(
                publisher,
                authority.Dispatcher);
            OffenseReturnArrivalState arrival = CreateOffenseArrivalState(
                faultAfterCommit ? "return:5" : "return:4");
            DomainFactDelivery ambiguous = owner.Settle(
                arrival,
                secured: 0,
                escaped: 1,
                day: faultAfterCommit ? 107 : 106);
            Require(ambiguous.MayBeCommitted
                && !ambiguous.Completed
                && arrival.stage == OffenseReturnArrivalStage.Escaped
                && arrival.settledMaterializedAmount == 1
                && arrival.settledSecuredAmount == 0
                && arrival.settledEscapedAmount == 1
                && arrival.materializedIds.SequenceEqual(
                    new[] { "wild:1" },
                    StringComparer.Ordinal)
                && arrival.escapedIds.SequenceEqual(
                    new[] { "wild:1" },
                    StringComparer.Ordinal)
                && arrival.settlement.hasFact
                && arrival.settlement.pending
                && arrival.settlement.projectionPending
                && (faultAfterCommit
                    ? replyLoss.HiddenReplyCount == 1
                        && OutcomeRowCount(
                            authority.Ledger.CaptureGameplayOutcomes()) == 1
                    : authority.Faults.BeforeCommitFaultCount == 1
                        && OutcomeRowCount(
                            authority.Ledger.CaptureGameplayOutcomes()) == 0),
                "Offense arrival " + edge
                    + "-commit ambiguity did not retain terminal state: "
                    + ambiguous.Detail);
            OffenseArrivalSettlementRuntime.ValidateState(arrival);
            saved = JsonConvert.DeserializeObject<
                SavedOffenseArrivalSettlementAuthority>(
                JsonConvert.SerializeObject(
                    new SavedOffenseArrivalSettlementAuthority
                    {
                        serializer = HostSerializer,
                        arrival = arrival,
                        ledger = authority.Ledger.CaptureGameplayOutcomes(),
                        migrated = authority.Migrated.Capture()
                    })) ?? throw new InvalidOperationException(
                        "Offense arrival " + edge
                            + "-commit host JSON returned null.");
        }

        DungeonGameRestoreReport pendingJoin = ValidateOffenseArrivalJoin(
            saved.arrival,
            saved.ledger,
            saved.migrated);
        Require(pendingJoin.Success,
            "Offense arrival " + edge
                + "-commit pending save join was rejected: "
                + string.Join(" | ", pendingJoin.Errors));

        using var restored = new OffenseArrivalSettlementAuthority(
            recorderFaults: false);
        restored.Restore(saved.ledger, saved.migrated);
        OffenseArrivalSettlementRuntime restoredOwner = null;
        var recovery = new RequireIdleDuringOffenseArrivalRecovery(
            restored.Dispatcher,
            () => restoredOwner.RequireIdle());
        restoredOwner = new OffenseArrivalSettlementRuntime(
            restored.Dispatcher,
            recovery);
        Require(restoredOwner.Recover(saved.arrival, out string recoveryFailure)
            && recoveryFailure.Length == 0
            && recovery.BusyGuardRejected
            && !saved.arrival.settlement.pending
            && saved.arrival.stage == OffenseReturnArrivalStage.Escaped
            && saved.arrival.settledMaterializedAmount == 1
            && saved.arrival.settledSecuredAmount == 0
            && saved.arrival.settledEscapedAmount == 1
            && saved.arrival.materializedIds.SequenceEqual(
                new[] { "wild:1" },
                StringComparer.Ordinal)
            && saved.arrival.escapedIds.SequenceEqual(
                new[] { "wild:1" },
                StringComparer.Ordinal)
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1
            && restored.Migrated.Capture().owners.Count == 1
            && restored.Migrated.Capture().owners[0].revision == 1L,
            "Offense arrival " + edge
                + "-commit recovery was not exact: " + recoveryFailure);
        Require(restoredOwner.Recover(saved.arrival, out string repeatFailure)
            && repeatFailure.Length == 0
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1
            && restored.Migrated.Capture().owners.Count == 1,
            "Offense arrival " + edge
                + "-commit recovery was not idempotent: " + repeatFailure);
        OffenseArrivalSettlementRuntime.ValidateState(saved.arrival);
    }

    private static void VerifyOffenseExpeditionResultPublicationRuntime()
    {
        OffenseExpeditionResultFactData caller =
            CreateOffenseExpeditionResultData(
                "expedition:host-result-frozen");
        var frozenFact = new OffenseExpeditionResolvedFact(caller);
        caller.absoluteDay = 999;
        caller.result.targetTitle = "변조된 원정 목표";
        caller.result.totalPower = 999f;
        caller.participants[0].displayName = "변조된 원정대원";
        OffenseExpeditionResultFactData detached = frozenFact.Snapshot();
        detached.result.targetTitle = "분리 스냅샷 변조";
        detached.participants.Clear();
        OffenseExpeditionResultFactData exact = frozenFact.Snapshot();
        Require(exact.absoluteDay == 102
            && exact.result.targetTitle == "호스트 원정 목표"
            && Math.Abs(exact.result.totalPower - 12f) < 0.0001f
            && exact.participants.Count == 1
            && exact.participants[0].displayName == "호스트 원정대원",
            "Offense expedition-result fact retained caller or snapshot mutations.");

        SavedOffenseExpeditionResultAuthority canonical;
        using (var authority = new OffenseExpeditionResultAuthority(
                   recorderFaults: false))
        {
            var events = new GameEventBus();
            int notifications = 0;
            using IDisposable notification = events.Subscribe<
                OffenseExpeditionResolvedFact>(_ =>
            {
                notifications++;
                throw new InvalidOperationException(
                    "host-offense-result-notification-fault");
            });
            var owner = new OffenseExpeditionResultPublicationRuntime(
                authority.Dispatcher,
                authority.Dispatcher,
                events);
            OffenseExpeditionResolvedFact fact =
                CreateOffenseExpeditionResultFact(
                    "expedition:host-result-normal");
            owner.Begin(fact.Result.expeditionId);
            DomainFactDelivery delivered = owner.PublishApplied(fact);
            owner.End();
            bool found = owner.TryGetApplied(
                fact.Result.expeditionId,
                out OffenseExpeditionResolvedFact queried);
            Require(delivered.Completed
                && found
                && queried.Snapshot().result.targetTitle
                    == "호스트 원정 목표"
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1
                && authority.Migrated.Capture().owners.Count == 1,
                "Offense expedition result did not publish exactly once: "
                    + delivered.Detail);

            OffenseExpeditionResultFactData queriedCopy = queried.Snapshot();
            queriedCopy.result.targetTitle = "조회 복사본 변조";
            OffenseExpeditionResultPublicationSaveData captured =
                owner.Capture();
            captured.observations[0].fact.result.totalPower = 700f;
            Require(owner.TryGetApplied(
                    fact.Result.expeditionId,
                    out OffenseExpeditionResolvedFact queriedAgain)
                && queriedAgain.Snapshot().result.targetTitle
                    == "호스트 원정 목표"
                && Math.Abs(owner.Capture().observations[0]
                    .fact.result.totalPower - 12f) < 0.0001f,
                "Offense expedition result query/capture exposed mutable owner state.");

            Require(owner.RecoverPending(out string notifyFailure)
                && notifyFailure.Length == 0
                && notifications == 1
                && owner.LastFailure.StartsWith(
                    "offense-result-notification:",
                    StringComparison.Ordinal)
                && !owner.Capture().observations[0].notificationPending
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense expedition notification fault changed durable state: "
                    + notifyFailure);
            Require(owner.RecoverPending(out string repeatNotifyFailure)
                && repeatNotifyFailure.Length == 0
                && notifications == 1
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense expedition notification was retried or duplicated: "
                    + repeatNotifyFailure);

            RequireThrows<InvalidOperationException>(
                () => owner.Begin(fact.Result.expeditionId),
                "Offense expedition duplicate result identity was accepted.");
            Require(owner.TryGetApplied(
                    fact.Result.expeditionId,
                    out OffenseExpeditionResolvedFact original)
                && original.Snapshot().result.targetTitle
                    == "호스트 원정 목표",
                "Offense expedition duplicate identity hid the original result.");

            owner.Begin("expedition:host-result-active-capture");
            RequireThrows<InvalidOperationException>(
                () => owner.Capture(),
                "Offense expedition capture accepted an active command.");
            owner.End();

            canonical = JsonConvert.DeserializeObject<
                SavedOffenseExpeditionResultAuthority>(
                JsonConvert.SerializeObject(
                    new SavedOffenseExpeditionResultAuthority
                    {
                        serializer = HostSerializer,
                        publications = owner.Capture(),
                        ledger = authority.Ledger.CaptureGameplayOutcomes(),
                        migrated = authority.Migrated.Capture()
                    })) ?? throw new InvalidOperationException(
                        "Offense expedition canonical host JSON returned null.");
        }

        Require(canonical.serializer == HostSerializer
            && canonical.publications.version
                == OffenseExpeditionResultPublicationSaveData.CurrentVersion
            && canonical.publications.observations.Count == 1
            && canonical.publications.observations[0].fact.result.targetTitle
                == "호스트 원정 목표",
            "Offense expedition host JSON lost the frozen result owner.");
        DungeonGameRestoreReport canonicalJoin =
            ValidateOffenseExpeditionResultJoin(
                canonical.publications,
                canonical.ledger,
                canonical.migrated);
        Require(canonicalJoin.Success,
            "Offense expedition canonical save join was rejected: "
                + string.Join(" | ", canonicalJoin.Errors));

        using (var restored = new OffenseExpeditionResultAuthority(
                   recorderFaults: false))
        {
            restored.Restore(canonical.ledger, canonical.migrated);
            OffenseExpeditionResultObservationData row =
                canonical.publications.observations[0];
            var original = new OffenseExpeditionResolvedFact(row.fact);
            IReadOnlyDictionary<string, string> expectedCanonical =
                CanonicalHashes(canonical.ledger);
            Require(row.publicationKey
                    == OffenseExpeditionResultFactObserver.Key(original)
                        .ToString(),
                "Offense expedition owner did not persist the fixed revision-1 key.");

            OffenseExpeditionResultFactData textTamper = row.fact.Clone();
            textTamper.result.targetTitle += " · 변조";
            RequireOffenseExpeditionReplayRejected(
                restored,
                row.publicationKey,
                new OffenseExpeditionResolvedFact(textTamper),
                "Offense expedition same-key text tamper was accepted.");

            OffenseExpeditionResultFactData numericTamper = row.fact.Clone();
            numericTamper.result.totalPower += 1f;
            RequireOffenseExpeditionReplayRejected(
                restored,
                row.publicationKey,
                new OffenseExpeditionResolvedFact(numericTamper),
                "Offense expedition same-key numeric tamper was accepted.");
            RequireCanonicalEqual(
                expectedCanonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Offense expedition tamper attempts changed canonical rows.");

            var unapplied = new OffenseExpeditionResultPublicationRuntime(
                restored.Dispatcher,
                restored.Dispatcher,
                new GameEventBus());
            unapplied.Begin(original.Result.expeditionId);
            DomainFactDelivery replayBeforeOwner =
                unapplied.PublishApplied(original);
            unapplied.End();
            Require(!replayBeforeOwner.MayBeCommitted
                && unapplied.Capture().observations.Count == 0
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1
                && restored.Migrated.Capture().owners.Count == 1,
                "Fresh unapplied expedition owner reused canonical identity: "
                    + replayBeforeOwner.Detail);
        }

        VerifyOffenseExpeditionKnownRejections();
        VerifyOffenseExpeditionAmbiguousRecovery(faultAfterCommit: false);
        VerifyOffenseExpeditionAmbiguousRecovery(faultAfterCommit: true);
    }

    private static void VerifyOffenseExpeditionKnownRejections()
    {
        using var authority = new OffenseExpeditionResultAuthority(
            recorderFaults: false);
        var faults = new OffenseExpeditionResultFaultPublisher(
            authority.Dispatcher);
        var owner = new OffenseExpeditionResultPublicationRuntime(
            faults,
            authority.Dispatcher,
            new GameEventBus());

        faults.RejectReserve = true;
        OffenseExpeditionResolvedFact reserveFact =
            CreateOffenseExpeditionResultFact(
                "expedition:host-result-reserve-reject");
        owner.Begin(reserveFact.Result.expeditionId);
        DomainFactDelivery reserveRejected = owner.PublishApplied(reserveFact);
        owner.End();
        Require(!reserveRejected.MayBeCommitted
            && owner.Capture().observations.Count == 0
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 0
            && authority.Migrated.Capture().owners.Count == 0,
            "Offense expedition reserve rejection retained owner/canonical state.");

        faults.RejectReserve = false;
        faults.RejectPrepare = true;
        OffenseExpeditionResolvedFact prepareFact =
            CreateOffenseExpeditionResultFact(
                "expedition:host-result-prepare-reject");
        owner.Begin(prepareFact.Result.expeditionId);
        DomainFactDelivery prepareRejected = owner.PublishApplied(prepareFact);
        owner.End();
        Require(!prepareRejected.MayBeCommitted
            && owner.Capture().observations.Count == 0
            && authority.Dispatcher.ActiveLeaseCount == 0
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 0
            && authority.Migrated.Capture().owners.Count == 0,
            "Offense expedition prepare rejection retained owner/canonical state.");
    }

    private static void VerifyOffenseExpeditionAmbiguousRecovery(
        bool faultAfterCommit)
    {
        string edge = faultAfterCommit ? "after" : "before";
        SavedOffenseExpeditionResultAuthority saved;
        using (var authority = new OffenseExpeditionResultAuthority(
                   recorderFaults: true))
        {
            authority.Faults.ThrowBeforeNextSingleCommit = !faultAfterCommit;
            var publicationFaults = new OffenseExpeditionResultFaultPublisher(authority.Dispatcher)
            { LoseNextPublishReply = faultAfterCommit };
            var owner = new OffenseExpeditionResultPublicationRuntime(
                publicationFaults,
                authority.Dispatcher,
                new GameEventBus());
            OffenseExpeditionResolvedFact fact =
                CreateOffenseExpeditionResultFact(
                    "expedition:host-result-" + edge + "-commit");
            owner.Begin(fact.Result.expeditionId);
            DomainFactDelivery ambiguous = owner.PublishApplied(fact);
            owner.End();
            Require(ambiguous.MayBeCommitted
                && !ambiguous.Completed
                && owner.Capture().observations.Count == 1
                && owner.Capture().observations[0].pending
                && (faultAfterCommit
                    ? publicationFaults.DroppedReplyCount == 1
                        && OutcomeRowCount(
                            authority.Ledger.CaptureGameplayOutcomes()) == 1
                    : authority.Faults.BeforeCommitFaultCount == 1
                        && OutcomeRowCount(
                            authority.Ledger.CaptureGameplayOutcomes()) == 0),
                "Offense expedition " + edge
                    + "-commit ambiguity did not retain pending owner state: "
                    + ambiguous.Detail);
            saved = JsonConvert.DeserializeObject<
                SavedOffenseExpeditionResultAuthority>(
                JsonConvert.SerializeObject(
                    new SavedOffenseExpeditionResultAuthority
                    {
                        serializer = HostSerializer,
                        publications = owner.Capture(),
                        ledger = authority.Ledger.CaptureGameplayOutcomes(),
                        migrated = authority.Migrated.Capture()
                    })) ?? throw new InvalidOperationException(
                        "Offense expedition " + edge
                            + "-commit host JSON returned null.");
        }

        DungeonGameRestoreReport pendingJoin =
            ValidateOffenseExpeditionResultJoin(
                saved.publications,
                saved.ledger,
                saved.migrated);
        Require(pendingJoin.Success,
            "Offense expedition " + edge
                + "-commit pending save join was rejected: "
                + string.Join(" | ", pendingJoin.Errors));

        using var restored = new OffenseExpeditionResultAuthority(
            recorderFaults: false);
        restored.Restore(saved.ledger, saved.migrated);
        OffenseExpeditionResultPublicationRuntime restoredOwner = null;
        var recovery = new CaptureDuringOffenseExpeditionRecovery(
            restored.Dispatcher,
            () => restoredOwner.Capture());
        restoredOwner = new OffenseExpeditionResultPublicationRuntime(
            restored.Dispatcher,
            recovery,
            new GameEventBus());
        restoredOwner.Restore(saved.publications);
        Require(restoredOwner.RecoverPending(out string recoveryFailure)
            && recoveryFailure.Length == 0
            && recovery.CaptureRejected
            && restoredOwner.Capture().observations.Count == 1
            && !restoredOwner.Capture().observations[0].pending
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1
            && restored.Migrated.Capture().owners.Count == 1,
            "Offense expedition " + edge
                + "-commit recovery was not exact: " + recoveryFailure);
        Require(restoredOwner.RecoverPending(out string repeatFailure)
            && repeatFailure.Length == 0
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1
            && restored.Migrated.Capture().owners.Count == 1,
            "Offense expedition " + edge
                + "-commit recovery was not idempotent: " + repeatFailure);

        DungeonGameRestoreReport recoveredJoin =
            ValidateOffenseExpeditionResultJoin(
                restoredOwner.Capture(),
                restored.Ledger.CaptureGameplayOutcomes(),
                restored.Migrated.Capture());
        Require(recoveredJoin.Success,
            "Offense expedition " + edge
                + "-commit recovered save join was rejected: "
                + string.Join(" | ", recoveredJoin.Errors));
    }

    private static void VerifyOffenseTruthPublicationRuntime()
    {
        OffenseTruthFactData caller = CreateOffenseTruthData();
        var frozenFact = new OffenseTruthRevealedFact(caller);
        caller.targetDisplayName = "변조된 목표 이름";
        caller.truthTitle = "변조된 진실 제목";
        caller.truthText = "변조된 진실 본문";
        caller.absoluteDay = 999;
        OffenseTruthFactData detached = frozenFact.Snapshot();
        detached.truthText = "분리 스냅샷 변조";
        OffenseTruthFactData exact = frozenFact.Snapshot();
        Require(exact.targetDisplayName == "호스트 진실 목표"
            && exact.truthTitle == "호스트 진실"
            && exact.truthText == "동결된 호스트 진실 본문"
            && exact.absoluteDay == 101
            && frozenFact.OwnerRevision == 1L,
            "Offense truth fact retained caller or snapshot mutations.");

        using (var authority = new OffenseTruthAuthority())
        {
            OffenseCampaignRuntime owner = CreateOffenseCampaign();
            var fact = new OffenseTruthRevealedFact(
                CreateOffenseTruthData());
            Require(owner.TryRevealTruth(
                    fact,
                    addKnownTarget: true,
                    authority.Dispatcher,
                    out string failure)
                && failure.Length == 0
                && owner.State.KnowTarget(OffenseTruthTargetId)
                && owner.State.IsTargetCompleted(OffenseTruthTargetId)
                && owner.State.RevealedTruthTargetId
                    == OffenseTruthTargetId
                && owner.State.CompletedTargetCount == 1
                && owner.TruthObservation.hasFact
                && !owner.TruthObservation.pending
                && owner.TruthObservation.notificationPending
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense truth normal reveal did not apply and publish once: "
                    + failure);
            Require(owner.TryRevealTruth(
                    fact,
                    addKnownTarget: true,
                    authority.Dispatcher,
                    out string duplicateFailure)
                && duplicateFailure.Length == 0
                && owner.State.CompletedTargetCount == 1
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense truth duplicate reveal changed owner or canonical state: "
                    + duplicateFailure);
        }

        using (var authority = new OffenseTruthAuthority())
        {
            OffenseCampaignRuntime owner = CreateOffenseCampaign();
            var rejecting = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.RejectCommit);
            Require(!owner.TryRevealTruth(
                    new OffenseTruthRevealedFact(
                        CreateOffenseTruthData()),
                    addKnownTarget: true,
                    rejecting,
                    out string failure)
                && failure.StartsWith(
                    "offense-truth-publication-rejected:",
                    StringComparison.Ordinal)
                && !owner.State.KnowTarget(OffenseTruthTargetId)
                && !owner.State.IsTargetCompleted(OffenseTruthTargetId)
                && !owner.State.TruthRevealed
                && !owner.TruthObservation.hasFact
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 0,
                "Offense truth known rejection did not roll back its owner: "
                    + failure);
        }

        SavedOffenseTruthAuthority saved;
        string publicationKey;
        using (var authority = new OffenseTruthAuthority())
        {
            OffenseCampaignRuntime owner = CreateOffenseCampaign();
            var uncertain = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.CommitThenUncertain);
            Require(owner.TryRevealTruth(
                    new OffenseTruthRevealedFact(
                        CreateOffenseTruthData()),
                    addKnownTarget: true,
                    uncertain,
                    out string failure)
                && failure.StartsWith(
                    "offense-truth-publication-pending:",
                    StringComparison.Ordinal)
                && owner.State.KnowTarget(OffenseTruthTargetId)
                && owner.State.IsTargetCompleted(OffenseTruthTargetId)
                && owner.State.TruthRevealed
                && owner.TruthObservation.pending
                && owner.TruthObservation.notificationPending
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense truth uncertain commit did not retain saved-forward state: "
                    + failure);
            publicationKey = owner.TruthObservation.publicationKey;
            saved = JsonConvert.DeserializeObject<SavedOffenseTruthAuthority>(
                JsonConvert.SerializeObject(new SavedOffenseTruthAuthority
                {
                    serializer = HostSerializer,
                    campaign = owner.Capture(),
                    ledger = authority.Ledger.CaptureGameplayOutcomes()
                })) ?? throw new InvalidOperationException(
                    "Offense truth host JSON round-trip returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.campaign.version
                == DungeonOffenseCampaignSaveData.CurrentVersion
            && saved.campaign.truthObservation.pending
            && saved.campaign.truthObservation.fact.targetDisplayName
                == "호스트 진실 목표"
            && saved.campaign.truthObservation.fact.truthTitle
                == "호스트 진실"
            && saved.campaign.truthObservation.fact.truthText
                == "동결된 호스트 진실 본문"
            && saved.campaign.truthObservation.fact.absoluteDay == 101,
            "Offense truth V2 host JSON lost its frozen pending fact.");
        DungeonGameRestoreReport pendingJoin = ValidateOffenseTruthJoin(
            saved.campaign,
            saved.ledger);
        Require(pendingJoin.Success,
            "Offense truth pending save join was rejected: "
                + string.Join(" | ", pendingJoin.Errors));

        DungeonOffenseCampaignSaveData tamperedSave =
            JsonConvert.DeserializeObject<DungeonOffenseCampaignSaveData>(
                JsonConvert.SerializeObject(saved.campaign))
            ?? throw new InvalidOperationException(
                "Offense truth tamper clone returned null.");
        tamperedSave.truthObservation.fact.truthText += " · 변조";
        DungeonGameRestoreReport tamperedJoin = ValidateOffenseTruthJoin(
            tamperedSave,
            saved.ledger);
        Require(!tamperedJoin.Success,
            "Offense truth preflight accepted changed text under the same key.");

        using (var restored = new OffenseTruthAuthority())
        {
            restored.Restore(saved.ledger);
            OffenseCampaignRuntime owner = CreateOffenseCampaign();
            owner.PublishRestoreCandidate(
                owner.BuildRestoreCandidate(saved.campaign));
            int completedBefore = owner.State.CompletedTargetCount;
            Require(owner.TryRecoverTruth(
                    restored.Dispatcher,
                    out string recoveryFailure)
                && recoveryFailure.Length == 0
                && !owner.TruthObservation.pending
                && owner.TruthObservation.notificationPending
                && owner.State.KnowTarget(OffenseTruthTargetId)
                && owner.State.IsTargetCompleted(OffenseTruthTargetId)
                && owner.State.RevealedTruthTargetId
                    == OffenseTruthTargetId
                && owner.State.CompletedTargetCount == completedBefore,
                "Offense truth restore recovery repeated owner effects: "
                    + recoveryFailure);
            Require(owner.TryRecoverTruth(
                    restored.Dispatcher,
                    out string repeatFailure)
                && repeatFailure.Length == 0
                && owner.State.CompletedTargetCount == completedBefore
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense truth repeated recovery duplicated canonical state: "
                    + repeatFailure);
            DungeonGameRestoreReport recoveredJoin =
                ValidateOffenseTruthJoin(
                    owner.Capture(),
                    restored.Ledger.CaptureGameplayOutcomes());
            Require(recoveredJoin.Success,
                "Offense truth recovered save join was rejected: "
                    + string.Join(" | ", recoveredJoin.Errors));

            OffenseTruthFactData changed =
                owner.TruthObservation.fact.Clone();
            changed.truthText += " · replay 변조";
            var tamperResults = new DomainFactDelivery[1];
            Require(!restored.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(
                            publicationKey,
                            new OffenseTruthRevealedFact(changed))
                    },
                    tamperResults,
                    out string tamperFailure)
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense truth same-key text tamper was accepted: "
                    + tamperFailure);

            OffenseCampaignRuntime unapplied = CreateOffenseCampaign();
            Require(!unapplied.TryRevealTruth(
                    new OffenseTruthRevealedFact(
                        CreateOffenseTruthData()),
                    addKnownTarget: true,
                    restored.Dispatcher,
                    out string replayFailure)
                && replayFailure
                    == "offense-truth-unapplied-publication-conflict"
                && !unapplied.State.KnowTarget(OffenseTruthTargetId)
                && !unapplied.State.IsTargetCompleted(
                    OffenseTruthTargetId)
                && !unapplied.State.TruthRevealed
                && !unapplied.TruthObservation.hasFact,
                "Offense truth canonical replay was accepted before owner apply: "
                    + replayFailure);
        }

        using (var authority = new OffenseTruthAuthority())
        {
            OffenseCampaignRuntime owner = CreateOffenseCampaign();
            DungeonOffenseCampaignSaveData before = owner.Capture();
            owner.BeginTruthDeferral();
            Require(owner.TryRevealTruth(
                    new OffenseTruthRevealedFact(
                        CreateOffenseTruthData()),
                    addKnownTarget: true,
                    authority.Dispatcher,
                    out string failure)
                && failure.Length == 0
                && owner.State.TruthRevealed
                && owner.TruthObservation.pending
                && authority.Ledger.TryGetResultIdentity(
                    OffenseTruthRevealFactMapping.Capture(
                        new OffenseTruthRevealedFact(CreateOffenseTruthData())).ResultKey,
                    out GameplayOutcomeReplayIdentity deferredIdentity)
                && deferredIdentity.State == GameplayOutcomeReplayState.Prepared
                && authority.Ledger.GetOutboxSnapshot().Count == 0,
                "Offense truth deferral did not retain an uncommitted child: "
                    + failure);
            RequireThrows<InvalidOperationException>(
                () => authority.Ledger.CaptureGameplayOutcomes(),
                "Offense truth ledger capture accepted an active reservation.");
            RequireThrows<InvalidOperationException>(
                () => owner.Capture(),
                "Offense truth capture accepted an active deferral.");
            RequireThrows<InvalidOperationException>(
                () => owner.BuildRestoreCandidate(before),
                "Offense truth restore build accepted an active deferral.");
            owner.CancelTruthDeferral();
            Require(!owner.State.KnowTarget(OffenseTruthTargetId)
                && !owner.State.IsTargetCompleted(OffenseTruthTargetId)
                && !owner.State.TruthRevealed
                && !owner.TruthObservation.hasFact
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 0,
                "Offense truth deferral cancel did not restore prior owner state.");
        }

        using (var authority = new OffenseTruthAuthority())
        {
            OffenseCampaignRuntime owner = CreateOffenseCampaign();
            owner.BeginTruthDeferral();
            Require(owner.TryRevealTruth(
                    new OffenseTruthRevealedFact(
                        CreateOffenseTruthData()),
                    addKnownTarget: true,
                    authority.Dispatcher,
                    out string failure)
                && failure.Length == 0,
                "Offense truth complete-deferral staging failed: "
                    + failure);
            owner.CompleteTruthDeferral();
            Require(owner.State.TruthRevealed
                && owner.State.CompletedTargetCount == 1
                && owner.TruthObservation.hasFact
                && !owner.TruthObservation.pending
                && owner.TruthObservation.notificationPending
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense truth complete deferral did not commit its durable child once.");
        }

        using (var authority = new OffenseTruthAuthority())
        {
            OffenseCampaignRuntime owner = CreateOffenseCampaign();
            var rejecting = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.RejectCommit);
            owner.BeginTruthDeferral();
            Require(owner.TryRevealTruth(
                    new OffenseTruthRevealedFact(
                        CreateOffenseTruthData()),
                    addKnownTarget: true,
                    rejecting,
                    out string failure)
                && failure.Length == 0,
                "Offense truth rejected-child deferral staging failed: "
                    + failure);
            owner.CompleteTruthDeferral();
            Require(owner.State.TruthRevealed
                && owner.State.CompletedTargetCount == 1
                && owner.TruthObservation.pending
                && owner.TruthObservation.notificationPending
                && owner.LastTruthPublicationFailure.Length > 0
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 0,
                "Offense truth rejected child rolled back an already-durable owner.");
            Require(owner.TryRecoverTruth(
                    authority.Dispatcher,
                    out string recoveryFailure)
                && recoveryFailure.Length == 0
                && !owner.TruthObservation.pending
                && owner.LastTruthPublicationFailure.Length == 0
                && owner.State.CompletedTargetCount == 1
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense truth rejected child did not recover exactly once: "
                    + recoveryFailure);
            Require(owner.TryRecoverTruth(
                    authority.Dispatcher,
                    out string repeatFailure)
                && repeatFailure.Length == 0
                && owner.State.CompletedTargetCount == 1
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Offense truth rejected-child recovery was not idempotent: "
                    + repeatFailure);
        }
    }

    private static void VerifyStaffDiscontentPublicationRecovery()
    {
        StaffDiscontentFactData transitionCaller =
            CreateStaffTransitionFact();
        var frozenTransitionFact =
            new StaffDiscontentTransitionedFact(transitionCaller);
        string transitionDigest =
            frozenTransitionFact.ComputeSemanticDigest();
        transitionCaller.before.localRebellionDays = 99;
        transitionCaller.after.displayName = "변조된 직원 이름";
        StaffDiscontentFactData detachedTransition =
            frozenTransitionFact.Snapshot();
        detachedTransition.after.mood = 1f;
        StaffDiscontentFactData exactTransition =
            frozenTransitionFact.Snapshot();
        Require(exactTransition.before.localRebellionDays == 1
            && exactTransition.after.localRebellionDays == 2
            && exactTransition.after.displayName == "호스트 불만 직원"
            && exactTransition.after.mood == 40f
            && frozenTransitionFact.ComputeSemanticDigest()
                == transitionDigest,
            "Staff discontent transition fact retained caller or snapshot mutations.");

        StaffDiscontentFactData responseCaller = CreateStaffResponseFact();
        var frozenResponseFact =
            new StaffRebellionResponseResolvedFact(responseCaller);
        string responseDigest = frozenResponseFact.ComputeSemanticDigest();
        responseCaller.before.localRebellionDays = 99;
        responseCaller.responderDisplayName = "변조된 응답자";
        StaffDiscontentFactData detachedResponse =
            frozenResponseFact.Snapshot();
        detachedResponse.after.lowMoodDays = 99;
        StaffDiscontentFactData exactResponse = frozenResponseFact.Snapshot();
        Require(exactResponse.before.localRebellionDays == 1
            && exactResponse.after.localRebellionDays == 2
            && exactResponse.responderDisplayName == "호스트 관리자"
            && exactResponse.after.lowMoodDays == 1
            && frozenResponseFact.ComputeSemanticDigest() == responseDigest,
            "Staff discontent response fact retained caller or snapshot mutations.");

        var calm = CreateStaffResponseFact();
        calm.responseType = StaffRebellionResponseType.Calm;
        calm.moodApplied = true;
        calm.resolvedMoodDelta = 25f;
        var calmFact = new StaffRebellionResponseResolvedFact(calm);
        string calmDigest = calmFact.ComputeSemanticDigest();
        calm.resolvedMoodDelta = 99f;
        Require(calmFact.Snapshot().resolvedMoodDelta == 25f
            && StaffDiscontentFactMapping.Capture(calmFact).FactDigest == calmDigest
            && new StaffRebellionResponseResolvedFact(calm).ComputeSemanticDigest() != calmDigest,
            "Staff calm deferred mood publication was not frozen in its canonical digest.");

        SavedStaffDiscontentAuthority saved;
        string transitionKey;
        string responseKey;
        using (var authority = new StaffDiscontentAuthority())
        {
            var state = CreateStaffDiscontentState();
            var publications = new StaffDiscontentPublicationRuntime(
                authority.Dispatcher,
                authority.Dispatcher);
            StaffDiscontentFactData transition =
                CreateStaffTransitionFact();
            Require(publications.TryStage(
                    transition,
                    out IDomainFactStage transitionStage,
                    out string transitionStageFailure),
                "Staff discontent transition staging failed: "
                    + transitionStageFailure);
            transitionKey = transitionStage.OperationKey;
            DomainFactDelivery transitionDelivery;
            try
            {
                transitionDelivery = publications.CommitApplied(
                    state,
                    transition,
                    transitionStage);
            }
            finally
            {
                transitionStage.Cancel();
            }
            Require(transitionDelivery.Completed
                && transitionDelivery.OperationKey == transitionKey
                && state.TransitionRevision == 1L
                && !state.TransitionObservation.pending
                && state.TransitionObservation.notificationPending
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Staff discontent transition did not commit once.");

            var uncertain = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.CommitThenUncertain);
            var uncertainPublications =
                new StaffDiscontentPublicationRuntime(
                    uncertain,
                    authority.Dispatcher);
            StaffDiscontentFactData response = CreateStaffResponseFact();
            Require(uncertainPublications.TryStage(
                    response,
                    out IDomainFactStage responseStage,
                    out string responseStageFailure),
                "Staff discontent response staging failed: "
                    + responseStageFailure);
            responseKey = responseStage.OperationKey;
            DomainFactDelivery responseDelivery;
            try
            {
                responseDelivery = uncertainPublications.CommitApplied(
                    state,
                    response,
                    responseStage);
            }
            finally
            {
                responseStage.Cancel();
            }
            Require(responseDelivery.Phase == DomainFactDeliveryPhase.InDoubt
                && state.OutcomeRevision == 1L
                && state.ResponseObservation.pending
                && state.ResponseObservation.notificationPending
                && state.HasPendingPublication
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 2,
                "Staff discontent uncertain response did not retain its pending owner observation.");

            saved = JsonConvert.DeserializeObject<
                SavedStaffDiscontentAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedStaffDiscontentAuthority
                        {
                            serializer = HostSerializer,
                            staff = CaptureStaffDiscontent(state),
                            ledger = authority.Ledger
                                .CaptureGameplayOutcomes()
                        }))
                ?? throw new InvalidOperationException(
                    "Staff discontent host JSON round-trip returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.staff.version
                == DungeonStaffDiscontentSaveData.CurrentVersion
            && saved.staff.records.Count == 1
            && saved.staff.records[0].localRebellionDays == 2,
            "Staff discontent V3 JSON did not preserve its declared host snapshot.");
        DungeonGameRestoreReport exactJoin = ValidateStaffDiscontentJoin(
            saved.staff,
            saved.ledger);
        Require(exactJoin.Success,
            "Staff discontent pending save join was rejected: "
                + string.Join(" | ", exactJoin.Errors));

        DungeonStaffDiscontentSaveData tamperedSave =
            JsonConvert.DeserializeObject<DungeonStaffDiscontentSaveData>(
                JsonConvert.SerializeObject(saved.staff))
            ?? throw new InvalidOperationException(
                "Staff discontent tamper clone returned null.");
        tamperedSave.responseObservation.fact.before
            .localRebellionDays++;
        DungeonGameRestoreReport tamperedJoin =
            ValidateStaffDiscontentJoin(tamperedSave, saved.ledger);
        Require(!tamperedJoin.Success,
            "Staff discontent preflight accepted a semantic-digest-only tamper.");

        using (var restored = new StaffDiscontentAuthority())
        {
            restored.Restore(saved.ledger);
            var state = new StaffDiscontentState();
            state.Restore(
                saved.staff.records.Select(
                    StaffDiscontentFactValues.Restore),
                saved.staff.outcomeRevision,
                saved.staff.transitionRevision,
                saved.staff.responseObservation,
                saved.staff.transitionObservation);
            StaffDiscontentSnapshot beforeRecovery =
                state.CaptureSnapshots().Single();
            var publications = new StaffDiscontentPublicationRuntime(
                restored.Dispatcher,
                restored.Dispatcher);
            Require(publications.TryRecover(
                    state,
                    out string recoveryFailure)
                && recoveryFailure.Length == 0
                && !state.HasPendingPublication
                && state.ResponseObservation.notificationPending
                && state.TransitionObservation.notificationPending
                && state.OutcomeRevision == 1L
                && state.TransitionRevision == 1L,
                "Staff discontent pending publication did not recover: "
                    + recoveryFailure);
            StaffDiscontentSnapshot afterRecovery =
                state.CaptureSnapshots().Single();
            Require(StaffDiscontentFactValues.SameState(
                    StaffDiscontentFactValues.Capture(beforeRecovery),
                    StaffDiscontentFactValues.Capture(afterRecovery))
                && afterRecovery.localRebellionDays == 2,
                "Staff discontent publication recovery repeated owner effects or lost rebellion days.");
            Require(publications.TryRecover(
                    state,
                    out string repeatRecoveryFailure)
                && repeatRecoveryFailure.Length == 0
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 2,
                "Staff discontent repeated recovery duplicated a canonical row: "
                    + repeatRecoveryFailure);
            DungeonGameRestoreReport recoveredJoin =
                ValidateStaffDiscontentJoin(
                    CaptureStaffDiscontent(state),
                    restored.Ledger.CaptureGameplayOutcomes());
            Require(recoveredJoin.Success,
                "Staff discontent completed recovery save join was rejected: "
                    + string.Join(" | ", recoveredJoin.Errors));

            StaffDiscontentObservationData transition =
                state.TransitionObservation;
            StaffDiscontentObservationData response =
                state.ResponseObservation;
            var originals = new[]
            {
                DomainFactReplay.Create(
                    transition.publicationKey,
                    new StaffDiscontentTransitionedFact(transition.fact)),
                DomainFactReplay.Create(
                    response.publicationKey,
                    new StaffRebellionResponseResolvedFact(response.fact))
            };
            var first = new DomainFactDelivery[2];
            Require(restored.Dispatcher.ReplayBatch(
                    originals,
                    first,
                    out string firstReplayFailure)
                && first[0].Completed
                && first[0].OperationKey == transitionKey
                && first[0].PublicationId.Length > 0
                && first[0].Fingerprint.Length == 64
                && first[1].Completed
                && first[1].OperationKey == responseKey
                && first[1].PublicationId.Length > 0
                && first[1].Fingerprint.Length == 64
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 2,
                "Staff discontent canonical replay identities were incomplete: "
                    + firstReplayFailure);
            var repeated = new DomainFactDelivery[2];
            Require(restored.Dispatcher.ReplayBatch(
                    originals,
                    repeated,
                    out string repeatedReplayFailure)
                && repeated[0].Completed
                && repeated[0].OperationKey == transitionKey
                && repeated[0].PublicationId == first[0].PublicationId
                && repeated[0].Fingerprint == first[0].Fingerprint
                && repeated[1].Completed
                && repeated[1].OperationKey == responseKey
                && repeated[1].PublicationId == first[1].PublicationId
                && repeated[1].Fingerprint == first[1].Fingerprint
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 2,
                "Staff discontent repeated replay changed canonical identity: "
                    + repeatedReplayFailure);

            StaffDiscontentFactData tampered = response.fact.Clone();
            tampered.before.localRebellionDays++;
            var tamperedResults = new DomainFactDelivery[1];
            Require(!restored.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(
                            response.publicationKey,
                            new StaffRebellionResponseResolvedFact(tampered))
                    },
                    tamperedResults,
                    out string tamperFailure)
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 2,
                "Staff discontent same-key semantic tamper was accepted: "
                    + tamperFailure);

            var unapplied = CreateStaffDiscontentState();
            Require(!publications.TryStage(
                    CreateStaffResponseFact(),
                    out IDomainFactStage replayStage,
                    out string replayStageFailure)
                && replayStage == null
                && replayStageFailure
                    == "staff-discontent-unapplied-publication-conflict"
                && unapplied.OutcomeRevision == 0L
                && unapplied.ResponseObservation.fact == null
                && unapplied.CaptureSnapshots().Single()
                    .localRebellionDays == 2,
                "Staff discontent unapplied canonical fact was accepted for effect replay: "
                    + replayStageFailure);
        }
    }

    private static void VerifyCaptivityTransitionPublicationRecovery()
    {
        CaptivityRehabilitationTerminalState rehabilitationCaller =
            CreateRehabilitationTerminal();
        var rehabilitationFact = new CaptivityRehabilitationProgressedFact(
            "character:host-captivity-rehabilitation",
            rehabilitationCaller);
        rehabilitationCaller.captiveDisplayName = "변조된 재사회화 이름";
        rehabilitationCaller.wardenDisplayName = "변조된 감시자 이름";
        rehabilitationCaller.absoluteDay = 999;
        rehabilitationCaller.trustBefore = 1f;
        CaptivityRehabilitationTerminalState frozenRehabilitation =
            rehabilitationFact.Snapshot();
        frozenRehabilitation.facilityDisplayName = "분리 스냅샷 변조";
        CaptivityRehabilitationTerminalState exactRehabilitation =
            rehabilitationFact.Snapshot();
        Require(exactRehabilitation.captiveDisplayName
                == "호스트 재사회화 대상"
            && exactRehabilitation.wardenDisplayName == "호스트 감시자"
            && exactRehabilitation.facilityDisplayName == "호스트 재사회화실"
            && exactRehabilitation.absoluteDay == 81
            && exactRehabilitation.workBefore == 17.5f
            && exactRehabilitation.workAfter == 0f
            && exactRehabilitation.trustBefore == 50f
            && exactRehabilitation.trustAfter == 55f,
            "Captivity rehabilitation fact retained caller or snapshot mutations.");

        CaptivityReleaseTerminalState releaseCaller =
            CreateReleaseTerminal();
        var releaseFact = new CaptivityReleasedFact(
            "character:host-captivity-release",
            releaseCaller);
        releaseCaller.captiveDisplayName = "변조된 석방 이름";
        releaseCaller.deciderDisplayName = "변조된 결정자 이름";
        releaseCaller.absoluteDay = 999;
        releaseCaller.actorTypeBefore = "Mutated";
        CaptivityReleaseTerminalState frozenRelease = releaseFact.Snapshot();
        frozenRelease.facilityDisplayName = "분리 스냅샷 변조";
        CaptivityReleaseTerminalState exactRelease = releaseFact.Snapshot();
        Require(exactRelease.captiveDisplayName == "호스트 석방 대상"
            && exactRelease.deciderDisplayName == "호스트 석방 결정자"
            && exactRelease.facilityDisplayName == "호스트 감방"
            && exactRelease.absoluteDay == 82
            && exactRelease.actorTypeBefore == "NPC"
            && exactRelease.lifecycleBefore == "Active"
            && !exactRelease.effectsApplied
            && !exactRelease.outcomePending
            && !exactRelease.observersPending
            && !exactRelease.decisionPublished,
            "Captivity release fact retained caller/marker or snapshot mutations.");

        SavedTransitionAuthority saved;
        string rehabilitationKey;
        string releaseKey;
        using (var authority = new TransitionAuthority())
        {
            var uncertain = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.CommitThenUncertain);
            CaptiveState rehabilitation = CreateRehabilitationState();
            var rehabilitationPendingFact =
                new CaptivityRehabilitationProgressedFact(
                    rehabilitation.captiveId,
                    rehabilitation.rehabilitationTerminal);
            Require(uncertain.TryStage(
                    rehabilitationPendingFact,
                    out IDomainFactStage rehabilitationStage,
                    out string rehabilitationStageFailure),
                "Captivity rehabilitation staging failed: "
                    + rehabilitationStageFailure);
            rehabilitationKey = rehabilitationStage.OperationKey;
            rehabilitation.rehabilitationTerminal.publicationKey =
                rehabilitationKey;
            rehabilitation.rehabilitationTerminal.outcomePending = true;
            DomainFactDelivery rehabilitationUncertain;
            try
            {
                rehabilitationUncertain = rehabilitationStage.Commit();
            }
            finally
            {
                rehabilitationStage.Cancel();
            }
            Require(rehabilitationUncertain.Phase
                    == DomainFactDeliveryPhase.InDoubt
                && rehabilitation.rehabilitationOutcomeRevision == 0L
                && rehabilitation.rehabilitationTerminal.outcomePending,
                "Commit-uncertain rehabilitation did not retain saved-forward state.");

            CaptiveState release = CreateReleaseState();
            var releasePendingFact = new CaptivityReleasedFact(
                release.captiveId,
                release.releaseTerminal);
            Require(uncertain.TryStage(
                    releasePendingFact,
                    out IDomainFactStage releaseStage,
                    out string releaseStageFailure),
                "Captivity release staging failed: " + releaseStageFailure);
            releaseKey = releaseStage.OperationKey;
            release.releaseTerminal.publicationKey = releaseKey;
            release.releaseTerminal.effectsApplied = true;
            release.releaseTerminal.outcomePending = true;
            release.releaseTerminal.observersPending = true;
            DomainFactDelivery releaseUncertain;
            try
            {
                releaseUncertain = releaseStage.Commit();
            }
            finally
            {
                releaseStage.Cancel();
            }
            Require(releaseUncertain.Phase == DomainFactDeliveryPhase.InDoubt
                && release.releaseOutcomeRevision == 0L
                && release.releaseTerminal.outcomePending
                && release.releaseTerminal.effectsApplied
                && release.releaseTerminal.observersPending
                && !release.releaseTerminal.decisionPublished
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 2,
                "Commit-uncertain release did not retain saved-forward observer state.");

            saved = JsonConvert.DeserializeObject<SavedTransitionAuthority>(
                JsonConvert.SerializeObject(new SavedTransitionAuthority
                {
                    serializer = HostSerializer,
                    captivity = CreateCaptivitySave(
                        rehabilitation,
                        release),
                    ledger = authority.Ledger.CaptureGameplayOutcomes()
                })) ?? throw new InvalidOperationException(
                    "Captivity transition host JSON round-trip returned null.");
        }

        Require(saved.serializer == HostSerializer,
            "Captivity transition save did not use the declared host serializer.");
        var shape = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(saved.captivity, shape);
        Require(shape.Success,
            "Captivity transition V10 pending save shape was rejected: "
                + string.Join(" | ", shape.Errors));
        DungeonGameRestoreReport exactJoin = ValidateTransitionJoin(
            saved.captivity,
            saved.ledger);
        Require(exactJoin.Success,
            "Captivity transition pending save join was rejected: "
                + string.Join(" | ", exactJoin.Errors));

        CaptivitySaveData tamperedSave =
            JsonConvert.DeserializeObject<CaptivitySaveData>(
                JsonConvert.SerializeObject(saved.captivity))
            ?? throw new InvalidOperationException(
                "Captivity transition tamper clone returned null.");
        CaptivityRehabilitationTerminalState tamperedRehabilitation =
            tamperedSave.captives.Single(value =>
                value.captiveId == "character:host-captivity-rehabilitation")
            .rehabilitationTerminal;
        tamperedRehabilitation.trustBefore += 5f;
        tamperedRehabilitation.trustAfter += 5f;
        var tamperedShape = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(tamperedSave, tamperedShape);
        Require(tamperedShape.Success,
            "Same-delta transition tamper unexpectedly broke V10 shape: "
                + string.Join(" | ", tamperedShape.Errors));
        DungeonGameRestoreReport tamperedJoin = ValidateTransitionJoin(
            tamperedSave,
            saved.ledger);
        Require(!tamperedJoin.Success,
            "Captivity transition preflight accepted changed absolute metrics under the same key.");

        using (var restored = new TransitionAuthority())
        {
            restored.Restore(saved.ledger);
            CaptiveState rehabilitation = saved.captivity.captives.Single(
                value => value.captiveId
                    == "character:host-captivity-rehabilitation");
            CaptiveState release = saved.captivity.captives.Single(
                value => value.captiveId
                    == "character:host-captivity-release");
            var originalFacts = new[]
            {
                DomainFactReplay.Create(
                    rehabilitationKey,
                    new CaptivityRehabilitationProgressedFact(
                        rehabilitation.captiveId,
                        rehabilitation.rehabilitationTerminal)),
                DomainFactReplay.Create(
                    releaseKey,
                    new CaptivityReleasedFact(
                        release.captiveId,
                        release.releaseTerminal))
            };
            var recovered = new DomainFactDelivery[2];
            Require(restored.Dispatcher.ReplayBatch(
                    originalFacts,
                    recovered,
                    out string recoveryFailure)
                && recovered[0].Completed
                && recovered[0].OperationKey == rehabilitationKey
                && recovered[0].PublicationId.Length > 0
                && recovered[0].Fingerprint.Length == 64
                && recovered[1].Completed
                && recovered[1].OperationKey == releaseKey
                && recovered[1].PublicationId.Length > 0
                && recovered[1].Fingerprint.Length == 64
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 2,
                "Captivity transition exact replay did not recover both canonical identities: "
                    + recoveryFailure);
            Require(rehabilitation.rehabilitationOutcomeRevision == 0L
                && rehabilitation.rehabilitationTerminal.outcomePending
                && release.releaseOutcomeRevision == 0L
                && release.releaseTerminal.outcomePending
                && release.releaseTerminal.effectsApplied
                && release.releaseTerminal.observersPending
                && !release.releaseTerminal.decisionPublished,
                "Observer replay mutated the saved transition owner snapshots.");

            var repeated = new DomainFactDelivery[2];
            Require(restored.Dispatcher.ReplayBatch(
                    originalFacts,
                    repeated,
                    out string repeatFailure)
                && repeated[0].Completed
                && repeated[0].OperationKey == recovered[0].OperationKey
                && repeated[0].PublicationId == recovered[0].PublicationId
                && repeated[0].Fingerprint == recovered[0].Fingerprint
                && repeated[1].Completed
                && repeated[1].OperationKey == recovered[1].OperationKey
                && repeated[1].PublicationId == recovered[1].PublicationId
                && repeated[1].Fingerprint == recovered[1].Fingerprint
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 2,
                "Captivity transition repeated replay changed canonical identities: "
                    + repeatFailure);

            CaptivityRehabilitationTerminalState changed =
                rehabilitation.rehabilitationTerminal.Clone();
            changed.trustBefore += 5f;
            changed.trustAfter += 5f;
            var results = new DomainFactDelivery[1];
            bool accepted = restored.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        rehabilitationKey,
                        new CaptivityRehabilitationProgressedFact(
                            rehabilitation.captiveId,
                            changed))
                },
                results,
                out string tamperFailure);
            Require(!accepted
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 2,
                "Captivity rehabilitation same-key metric tamper was accepted: "
                    + tamperFailure);
        }
    }

    private static void VerifyCaptivityInteractionPublicationRuntime()
    {
        CaptiveState callerState = CreateInteractionState("fact-freeze");
        CaptivityInteractionTerminalState callerOwned =
            callerState.interactionTerminal;
        var frozenFact = new CaptivityInteractionResolvedFact(
            callerState.captiveId,
            callerOwned,
            1L);
        string frozenDigest = frozenFact.ComputeSemanticDigest();
        callerOwned.captiveDisplayName = "변조된 상호작용 포로";
        callerOwned.absoluteDay = 999;
        callerOwned.willBefore = 1f;
        callerOwned.willAfter = 2f;
        callerOwned.fearBefore = 3f;
        callerOwned.fearAfter = 4f;
        callerOwned.trustBefore = 5f;
        callerOwned.trustAfter = 6f;
        callerOwned.grudgeBefore = 7f;
        callerOwned.grudgeAfter = 8f;
        callerOwned.corruptionBefore = 9f;
        callerOwned.corruptionAfter = 10f;
        callerOwned.bodyHealthBefore = 20f;
        callerOwned.bodyHealthAfter = 8f;
        callerOwned.bodyMaximumHealth = 25f;
        CaptivityInteractionTerminalState detached = frozenFact.Snapshot();
        detached.captiveDisplayName = "분리 스냅샷 변조";
        CaptivityInteractionTerminalState frozen = frozenFact.Snapshot();
        Require(frozen.captiveDisplayName == "호스트 상호작용 포로"
            && frozen.absoluteDay == 71
            && frozen.willBefore == 70f
            && frozen.willAfter == 67f
            && frozen.fearBefore == 20f
            && frozen.fearAfter == 22f
            && frozen.trustBefore == 30f
            && frozen.trustAfter == 35f
            && frozen.grudgeBefore == 40f
            && frozen.grudgeAfter == 38f
            && frozen.corruptionBefore == 10f
            && frozen.corruptionAfter == 11f
            && frozen.bodyHealthBefore == 80f
            && frozen.bodyHealthAfter == 68f
            && frozen.bodyMaximumHealth == 100f
            && frozenFact.ComputeSemanticDigest() == frozenDigest,
            "Captivity interaction fact retained caller or snapshot mutations.");

        CaptivityInteractionTerminalState shifted = frozen.Clone();
        shifted.willBefore += 5f;
        shifted.willAfter += 5f;
        shifted.fearBefore += 5f;
        shifted.fearAfter += 5f;
        shifted.trustBefore += 5f;
        shifted.trustAfter += 5f;
        shifted.grudgeBefore += 5f;
        shifted.grudgeAfter += 5f;
        shifted.corruptionBefore += 5f;
        shifted.corruptionAfter += 5f;
        shifted.bodyHealthBefore += 10f;
        shifted.bodyHealthAfter += 10f;
        shifted.bodyMaximumHealth += 20f;
        var shiftedFact = new CaptivityInteractionResolvedFact(
            callerState.captiveId,
            shifted,
            1L);
        CaptivityInteractionTerminalState identityShifted = frozen.Clone();
        identityShifted.captiveDisplayName = "다른 고정 이름";
        identityShifted.absoluteDay++;
        var identityShiftedFact = new CaptivityInteractionResolvedFact(
            callerState.captiveId,
            identityShifted,
            1L);
        CaptivityInteractionTerminalState maximumShifted = frozen.Clone();
        maximumShifted.bodyMaximumHealth++;
        var maximumShiftedFact = new CaptivityInteractionResolvedFact(
            callerState.captiveId,
            maximumShifted,
            1L);
        Require(shifted.willAfter - shifted.willBefore
                == frozen.willAfter - frozen.willBefore
            && shifted.fearAfter - shifted.fearBefore
                == frozen.fearAfter - frozen.fearBefore
            && shifted.trustAfter - shifted.trustBefore
                == frozen.trustAfter - frozen.trustBefore
            && shifted.grudgeAfter - shifted.grudgeBefore
                == frozen.grudgeAfter - frozen.grudgeBefore
            && shifted.corruptionAfter - shifted.corruptionBefore
                == frozen.corruptionAfter - frozen.corruptionBefore
            && shifted.bodyHealthBefore - shifted.bodyHealthAfter
                == frozen.bodyHealthBefore - frozen.bodyHealthAfter
            && shiftedFact.ComputeSemanticDigest() != frozenDigest
            && identityShiftedFact.ComputeSemanticDigest() != frozenDigest
            && maximumShiftedFact.ComputeSemanticDigest() != frozenDigest,
            "Captivity interaction semantic digest ignored frozen identity, day, absolute metrics, or body maximum.");

        using (var rejectedAuthority = new InteractionAuthority())
        {
            var physical = new RecordingPhysicalSource();
            var publisher = new MilestoneStagePublisher(
                rejectedAuthority.Dispatcher,
                MilestoneStageBehavior.RejectCommit);
            var runtime = new InteractionRuntimeReflection(
                publisher,
                rejectedAuthority.Dispatcher,
                physical);
            CaptiveState rejectedState = CreateInteractionState("rejected");
            Require(runtime.TryPrepare(
                    rejectedState,
                    out IDisposable prepared,
                    out long revision,
                    out string failure)
                && revision == 1L
                && prepared != null,
                "Captivity interaction rejection prepare failed: " + failure);
            using (prepared)
            {
                RequireThrows<InvalidOperationException>(
                    runtime.ValidateBeforeCapture,
                    "Captivity interaction capture guard accepted an active publication.");
                ApplyInteractionState(rejectedState, revision);
                DomainFactDelivery rejected = runtime.Commit(prepared);
                Require(!rejected.MayBeCommitted
                    && rejectedState.interactionTerminal.publicationKey.Length == 0
                    && !rejectedState.interactionTerminal.deliveryPending
                    && OutcomeRowCount(
                        rejectedAuthority.Ledger.CaptureGameplayOutcomes()) == 0,
                    "Known interaction publication rejection retained canonical or pending state.");
            }
            runtime.ValidateBeforeCapture();
            Require(physical.CallCount == 0,
                "Interaction publication rejection unexpectedly invoked physical output.");
        }

        SavedInteractionAuthority saved;
        string pendingPublicationKey;
        string pendingDigest;
        using (var authority = new InteractionAuthority())
        {
            var physical = new RecordingPhysicalSource();
            var publisher = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.CommitThenUncertain);
            var runtime = new InteractionRuntimeReflection(
                publisher,
                authority.Dispatcher,
                physical);
            CaptiveState state = CreateInteractionState("uncertain");
            Require(runtime.TryPrepare(
                    state,
                    out IDisposable prepared,
                    out long revision,
                    out string failure)
                && revision == 1L
                && prepared != null,
                "Captivity interaction uncertain prepare failed: " + failure);
            using (prepared)
            {
                ApplyInteractionState(state, revision);
                DomainFactDelivery uncertain = runtime.Commit(prepared);
                Require(uncertain.Phase == DomainFactDeliveryPhase.InDoubt
                    && state.interactionTerminal.deliveryPending
                    && !string.IsNullOrWhiteSpace(
                        state.interactionTerminal.publicationKey)
                    && OutcomeRowCount(
                        authority.Ledger.CaptureGameplayOutcomes()) == 1,
                    "Commit-then-uncertain interaction did not retain one pending canonical publication.");
                Require(!runtime.TryEnsureOutput(
                        state,
                        out string outputFailure)
                    && outputFailure.Length > 0
                    && physical.CallCount == 0,
                    "Pending interaction publication did not block physical output.");
            }

            pendingPublicationKey = state.interactionTerminal.publicationKey;
            pendingDigest = new CaptivityInteractionResolvedFact(
                state.captiveId,
                state.interactionTerminal,
                state.interactionTerminal.outcomeRevision)
                .ComputeSemanticDigest();
            state.displayName = "현재 상호작용 포로 이름";
            state.capturedAbsoluteDay = 404;
            state.will = 44f;
            state.fear = 55f;
            state.trust = 66f;
            state.grudge = 33f;
            state.corruption = 22f;
            var captivity = new CaptivitySaveData
            {
                captives = new List<CaptiveState> { state.Clone() },
                policies = new List<CaptivePolicyData>
                {
                    new()
                    {
                        policyId = CaptivityPolicyIds.Standard,
                        displayName = "표준 수용"
                    }
                }
            };
            saved = JsonConvert.DeserializeObject<SavedInteractionAuthority>(
                JsonConvert.SerializeObject(new SavedInteractionAuthority
                {
                    serializer = HostSerializer,
                    captivity = captivity,
                    ledger = authority.Ledger.CaptureGameplayOutcomes()
                })) ?? throw new InvalidOperationException(
                    "Captivity interaction host JSON round-trip returned null.");
        }

        Require(saved.serializer == HostSerializer,
            "Captivity interaction save did not use the declared host serializer.");
        var shape = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(saved.captivity, shape);
        Require(shape.Success,
            "Captivity interaction V9 save shape was rejected: "
                + string.Join(" | ", shape.Errors));
        DungeonGameRestoreReport exactJoin = ValidateInteractionJoin(
            saved.captivity,
            saved.ledger);
        Require(exactJoin.Success,
            "Captivity interaction exact save join was rejected: "
                + string.Join(" | ", exactJoin.Errors));

        CaptivitySaveData tamperedSave =
            JsonConvert.DeserializeObject<CaptivitySaveData>(
                JsonConvert.SerializeObject(saved.captivity))
            ?? throw new InvalidOperationException(
                "Captivity interaction tamper clone returned null.");
        CaptivityInteractionTerminalState tamperedTerminal =
            tamperedSave.captives.Single().interactionTerminal;
        tamperedTerminal.willBefore += 5f;
        tamperedTerminal.willAfter += 5f;
        var tamperedShape = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(tamperedSave, tamperedShape);
        Require(tamperedShape.Success,
            "Same-delta interaction tamper unexpectedly broke the neutral V9 shape: "
                + string.Join(" | ", tamperedShape.Errors));
        DungeonGameRestoreReport tamperedJoin = ValidateInteractionJoin(
            tamperedSave,
            saved.ledger);
        Require(!tamperedJoin.Success,
            "Captivity interaction preflight accepted same-delta absolute metric tampering.");

        using (var restored = new InteractionAuthority())
        {
            restored.Restore(saved.ledger);
            CaptiveState state = saved.captivity.captives.Single();
            var physical = new RecordingPhysicalSource();
            var runtime = new InteractionRuntimeReflection(
                restored.Dispatcher,
                restored.Dispatcher,
                physical);
            CaptiveState unappliedReplay =
                CreateInteractionState("uncertain");
            Require(!runtime.TryPrepare(
                    unappliedReplay,
                    out IDisposable replayPrepared,
                    out long replayRevision,
                    out string replayFailure)
                && replayPrepared == null
                && replayRevision == 1L
                && replayFailure
                    == "captivity-interaction-unapplied-replay-conflict"
                && unappliedReplay.interactionOutcomeRevision == 0L
                && unappliedReplay.interactionTerminal.outcomeRevision == 0L
                && unappliedReplay.will == 70f
                && unappliedReplay.fear == 20f
                && unappliedReplay.trust == 30f
                && unappliedReplay.grudge == 40f
                && unappliedReplay.corruption == 10f
                && physical.CallCount == 0,
                "Canonical interaction replay authorized unapplied owner effects: "
                    + replayFailure);
            float[] currentStats =
            {
                state.will,
                state.fear,
                state.trust,
                state.grudge,
                state.corruption
            };
            bool recovered = runtime.TryEnsureDelivered(
                state,
                out string recoveryFailure);
            string duplicateFailure = string.Empty;
            bool duplicateRecovered = recovered
                && runtime.TryEnsureDelivered(
                    state,
                    out duplicateFailure);
            Require(recovered
                && !state.interactionTerminal.deliveryPending
                && duplicateRecovered
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1,
                "Captivity interaction healthy recovery failed or duplicated: "
                    + recoveryFailure + " | " + duplicateFailure);
            Require(state.will == currentStats[0]
                && state.fear == currentStats[1]
                && state.trust == currentStats[2]
                && state.grudge == currentStats[3]
                && state.corruption == currentStats[4]
                && state.displayName == "현재 상호작용 포로 이름"
                && state.capturedAbsoluteDay == 404,
                "Captivity interaction publication recovery repeated or rewrote owner effects.");
            CaptivityInteractionTerminalState restoredTerminal =
                state.interactionTerminal;
            Require(restoredTerminal.captiveDisplayName
                    == "호스트 상호작용 포로"
                && restoredTerminal.absoluteDay == 71
                && restoredTerminal.willBefore == 70f
                && restoredTerminal.willAfter == 67f
                && restoredTerminal.fearBefore == 20f
                && restoredTerminal.fearAfter == 22f
                && restoredTerminal.trustBefore == 30f
                && restoredTerminal.trustAfter == 35f
                && restoredTerminal.grudgeBefore == 40f
                && restoredTerminal.grudgeAfter == 38f
                && restoredTerminal.corruptionBefore == 10f
                && restoredTerminal.corruptionAfter == 11f
                && restoredTerminal.bodyHealthBefore == 80f
                && restoredTerminal.bodyHealthAfter == 68f
                && restoredTerminal.bodyMaximumHealth == 100f
                && new CaptivityInteractionResolvedFact(
                    state.captiveId,
                    restoredTerminal,
                    restoredTerminal.outcomeRevision)
                    .ComputeSemanticDigest() == pendingDigest,
                "Captivity interaction fresh-authority recovery lost frozen result fields.");
            Require(runtime.TryEnsureOutput(
                    state,
                    out string outputFailure)
                && outputFailure.Length == 0
                && physical.CallCount == 0,
                "Delivered no-output interaction invoked the physical output port.");

            GameplayOutcomeSnapshot exact = restored.Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(4),
                    OutcomeFilter.All)
                .Items.Single().Exact;
            Require(exact.provenance.Count == 2
                && exact.provenance[1].kindId == "domain-fact-sha256"
                && exact.provenance[1].value == pendingDigest,
                "Captivity interaction canonical row lost semantic-digest provenance.");

            CaptivityInteractionTerminalState changed =
                restoredTerminal.Clone();
            changed.willBefore += 5f;
            changed.willAfter += 5f;
            var results = new DomainFactDelivery[1];
            bool accepted = restored.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        pendingPublicationKey,
                        new CaptivityInteractionResolvedFact(
                            state.captiveId,
                            changed,
                            changed.outcomeRevision))
                },
                results,
                out string tamperFailure);
            Require(!accepted
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1,
                "Captivity interaction same-key absolute metric tamper was accepted: "
                    + tamperFailure);
        }

        using (var outputAuthority = new InteractionAuthority())
        {
            var physical = new RecordingPhysicalSource(commitOutputs: true);
            var runtime = new InteractionRuntimeReflection(
                outputAuthority.Dispatcher,
                outputAuthority.Dispatcher,
                physical);
            CaptiveState state = CreateInteractionState("output");
            state.interactionTerminal.outputItemId =
                CaptivityItemDefinitions.ExtractedBloodItemId;
            state.interactionTerminal.outputAmount = 2;
            state.interactionTerminal.outputOperationId =
                CaptivityInteractionAttemptIdentity.FormatOutputOperationId(
                    state.captiveId,
                    state.interactionTerminal.attemptId);
            Require(runtime.TryPrepare(
                    state,
                    out IDisposable prepared,
                    out long revision,
                    out string prepareFailure),
                "Captivity interaction output prepare failed: "
                    + prepareFailure);
            using (prepared)
            {
                ApplyInteractionState(state, revision);
                Require(runtime.Commit(prepared).Completed,
                    "Captivity interaction output fact was not delivered.");
            }
            bool firstOutput = runtime.TryEnsureOutput(
                state,
                out string firstOutputFailure);
            string repeatedOutputFailure = string.Empty;
            bool repeatedOutput = firstOutput
                && runtime.TryEnsureOutput(
                    state,
                    out repeatedOutputFailure);
            Require(firstOutput
                && repeatedOutput
                && firstOutputFailure.Length == 0
                && repeatedOutputFailure.Length == 0
                && physical.CallCount == 1
                && state.interactionTerminal.outputPublished
                && !string.IsNullOrWhiteSpace(
                    state.interactionTerminal.outputCommitId),
                "Captivity interaction output receipt was not retained exactly once: "
                    + firstOutputFailure + " | " + repeatedOutputFailure);
        }
    }

    private static void VerifyCaptiveRansomPublicationRuntime()
    {
        CaptiveRansomAcceptedFactData callerOwned = CreateRansomFactData();
        var frozenFact = new CaptiveRansomAcceptedFact(callerOwned);
        callerOwned.displayName = "변조된 몸값 포로";
        callerOwned.amount = 999;
        callerOwned.retaliationPressure = 99f;
        CaptiveRansomAcceptedFactData detached = frozenFact.Snapshot();
        detached.amount = 1;
        Require(frozenFact.Snapshot().displayName == "호스트 몸값 포로"
            && frozenFact.Snapshot().amount == 240
            && frozenFact.Snapshot().retaliationPressure == 48f,
            "Captive ransom fact retained caller or snapshot DTO mutations.");
        CaptiveRansomAcceptedFactData invalid = CreateRansomFactData();
        invalid.amount = 0;
        RequireThrows<ArgumentException>(
            () => new CaptiveRansomAcceptedFact(invalid),
            "Captive ransom fact accepted a zero payment.");

        using (var authority = new RansomAuthority())
        {
            var events = new GameEventBus();
            var publisher = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.RejectCommit);
            var runtime = new RansomRuntimeReflection(
                publisher,
                authority.Dispatcher,
                events,
                new EmptyCharacterAiWorldRegistry());
            CaptiveState state = CreateRansomState("rejected");
            Require(runtime.TryPrepare(
                    state,
                    CaptivityStatus.Confined,
                    180,
                    35f,
                    70,
                    out IDisposable prepared,
                    out long revision,
                    out string prepareFailure),
                "Rejected ransom prepare failed: " + prepareFailure);
            using (prepared)
            {
                state.status = CaptivityStatus.Ransom;
                state.ransomOutcomeRevision = revision;
                state.ransomAcceptedAmount = 180;
                state.retaliationPressure = 35f;
                DomainFactDelivery rejected = runtime.Commit(prepared);
                Require(!rejected.MayBeCommitted
                    && rejected.Detail.Contains(
                        "host-captive-definite-rejection",
                        StringComparison.Ordinal)
                    && state.status == CaptivityStatus.Ransom
                    && state.ransomOutcomeRevision == 1L
                    && state.ransomAcceptedAmount == 180
                    && state.retaliationPressure == 35f
                    && !state.ransomObservation.HasFact
                    && !state.ransomObserversPending
                    && OutcomeRowCount(
                        authority.Ledger.CaptureGameplayOutcomes()) == 0,
                    "Known ransom rejection did not cancel its real stage, or the host incorrectly claimed owner rollback.");
            }
        }

        CaptivitySaveData pendingCaptivity;
        GameplayOutcomeLedgerSaveData pendingLedger;
        string pendingPublicationKey;
        using (var authority = new RansomAuthority())
        {
            var events = new GameEventBus();
            int ransomEvents = 0;
            int decisionEvents = 0;
            using IDisposable ransomSubscription = events.Subscribe<
                CaptiveRansomedEvent>(_ => ransomEvents++);
            using IDisposable decisionSubscription = events.Subscribe<
                PrisonerDecisionEvent>(_ => decisionEvents++);
            var publisher = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.CommitThenUncertain);
            var runtime = new RansomRuntimeReflection(
                publisher,
                authority.Dispatcher,
                events,
                new EmptyCharacterAiWorldRegistry());
            CaptiveState state = CreateRansomState("pending");
            Require(runtime.TryPrepare(
                    state,
                    CaptivityStatus.Confined,
                    240,
                    48f,
                    73,
                    out IDisposable prepared,
                    out long revision,
                    out string prepareFailure),
                "Pending ransom prepare failed: " + prepareFailure);
            using (prepared)
            {
                Require(revision == 1L
                    && !runtime.IsReplay(prepared),
                    "Ransom prepare did not reserve first revision exactly.");
                RequireThrows<InvalidOperationException>(
                    runtime.ValidateBeforeCapture,
                    "Active ransom publication did not guard save capture.");
                state.status = CaptivityStatus.Ransom;
                state.ransomOutcomeRevision = revision;
                state.ransomAcceptedAmount = 240;
                state.retaliationPressure = 48f;
                DomainFactDelivery pending = runtime.Commit(prepared);
                Require(pending.Phase == DomainFactDeliveryPhase.InDoubt
                    && pending.MayBeCommitted
                    && state.ransomObservation.HasFact
                    && state.ransomObservation.pending
                    && state.ransomObserversPending
                    && ransomEvents == 0
                    && decisionEvents == 0
                    && OutcomeRowCount(
                        authority.Ledger.CaptureGameplayOutcomes()) == 1,
                    "Commit-then-uncertain ransom did not retain one pending publication without early notification.");
            }
            runtime.ValidateBeforeCapture();

            CaptiveState detachedState = state.Clone();
            state.ransomObservation.fact.amount = 999;
            state.ransomObservation = new CaptiveRansomObservationData();
            Require(detachedState.ransomObservation.HasFact
                && detachedState.ransomObservation.fact.amount == 240
                && detachedState.ransomObservation.pending,
                "Captive state clone did not deeply copy its ransom observation.");
            state = detachedState;
            pendingPublicationKey = state.ransomObservation.publicationKey;
            Require(!string.IsNullOrWhiteSpace(pendingPublicationKey)
                && state.ransomObservation.fact.displayName
                    == "호스트 몸값 포로"
                && state.ransomObservation.fact.decisionMakerId.Length == 0
                && state.ransomObservation.fact.amount == 240
                && state.ransomObservation.fact.retaliationPressure == 48f
                && state.ransomObservation.fact.absoluteDay == 73,
                "Pending ransom did not freeze the original terms and optional empty decision maker.");
            state.displayName = "현재 몸값 포로 이름";
            state.retaliationPressure = 7f;
            state.capturedAbsoluteDay = 101;
            pendingCaptivity = new CaptivitySaveData
            {
                captives = new List<CaptiveState> { state.Clone() },
                policies = new List<CaptivePolicyData>
                {
                    new()
                    {
                        policyId = CaptivityPolicyIds.Standard,
                        displayName = "표준 수용"
                    }
                }
            };
            pendingLedger = authority.Ledger.CaptureGameplayOutcomes();
        }

        var saved = new SavedRansomAuthority
        {
            serializer = HostSerializer,
            captivity = pendingCaptivity,
            ledger = pendingLedger
        };
        string json = JsonConvert.SerializeObject(saved);
        SavedRansomAuthority exact = JsonConvert
            .DeserializeObject<SavedRansomAuthority>(json)
            ?? throw new InvalidOperationException(
                "Host JSON captive ransom state/ledger round-trip returned null.");
        var shapeReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(exact.captivity, shapeReport);
        Require(shapeReport.Success,
            "Host JSON captive ransom state failed V8 validation: "
                + string.Join(" | ", shapeReport.Errors));
        DungeonGameRestoreReport joinReport = ValidateRansomJoin(
            exact.captivity,
            exact.ledger);
        Require(joinReport.Success,
            "Canonical captive ransom save join failed: "
                + string.Join(" | ", joinReport.Errors));
        CaptiveState restoredState = exact.captivity.captives.Single();
        Require(restoredState.displayName == "현재 몸값 포로 이름"
            && restoredState.retaliationPressure == 7f
            && restoredState.capturedAbsoluteDay == 101
            && restoredState.ransomObservation.publicationKey
                == pendingPublicationKey
            && restoredState.ransomObservation.pending
            && restoredState.ransomObserversPending
            && restoredState.ransomObservation.fact.displayName
                == "호스트 몸값 포로"
            && restoredState.ransomObservation.fact.amount == 240
            && restoredState.ransomObservation.fact.retaliationPressure == 48f
            && restoredState.ransomObservation.fact.absoluteDay == 73,
            "Host JSON ransom round-trip rewrote frozen terms from current state.");

        IReadOnlyDictionary<string, string> canonical =
            CanonicalHashes(exact.ledger);
        using (var authority = new RansomAuthority())
        {
            authority.Restore(exact.ledger);
            var events = new GameEventBus();
            var ransomedEvents = new List<CaptiveRansomedEvent>();
            int decisionEvents = 0;
            using IDisposable ransomSubscription = events.Subscribe<
                CaptiveRansomedEvent>(ransomedEvents.Add);
            using IDisposable decisionSubscription = events.Subscribe<
                PrisonerDecisionEvent>(_ => decisionEvents++);
            var runtime = new RansomRuntimeReflection(
                authority.Dispatcher,
                authority.Dispatcher,
                events,
                new EmptyCharacterAiWorldRegistry());
            Require(runtime.Recover(
                    restoredState,
                    out string recoveryFailure)
                && recoveryFailure.Length == 0
                && !restoredState.ransomObservation.pending
                && restoredState.ransomObserversPending
                && ransomedEvents.Count == 0
                && decisionEvents == 0,
                "Fresh ransom authority did not recover publication only: "
                    + recoveryFailure);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(authority.Ledger.CaptureGameplayOutcomes()),
                "Ransom exact recovery duplicated or rewrote its canonical row.");

            CaptiveRansomAcceptedFactData changed =
                restoredState.ransomObservation.fact.Clone();
            changed.decisionMakerId = "character:captive-host:other-owner";
            var tamperResults = new DomainFactDelivery[1];
            bool tamperAccepted = authority.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        restoredState.ransomObservation.publicationKey,
                        new CaptiveRansomAcceptedFact(changed))
                },
                tamperResults,
                out string tamperFailure);
            Require(!tamperAccepted
                && tamperResults[0].Phase == DomainFactDeliveryPhase.Rejected
                && !tamperResults[0].MayBeCommitted
                && !string.IsNullOrWhiteSpace(tamperFailure),
                "Same-key decision-maker-only ransom tamper was not rejected.");

            Require(runtime.TryBeginFinalization(
                    restoredState,
                    out IDisposable finalization,
                    out string finalizationFailure),
                "Ransom finalization lease failed: " + finalizationFailure);
            using (finalization)
            {
                RequireThrows<InvalidOperationException>(
                    runtime.ValidateBeforeCapture,
                    "Active ransom finalization did not guard save capture.");
                VerifyRansomMoneyAtomicity(runtime);
                restoredState.status = CaptivityStatus.Released;
                restoredState.ransomIncomeCredited = true;
                runtime.NotifyCommitted(restoredState);
                runtime.NotifyCommitted(restoredState);
                Require(!restoredState.ransomObserversPending
                    && ransomedEvents.Count == 1
                    && ransomedEvents[0].CaptiveId == restoredState.captiveId
                    && ransomedEvents[0].Amount == 240
                    && ransomedEvents[0].RetaliationPressure == 48f
                    && decisionEvents == 0,
                    "Manual ransom finalization did not publish frozen notification exactly once.");
            }
            runtime.ValidateBeforeCapture();
            runtime.NotifyCommitted(restoredState);
            GameplayOutcomeSnapshot row = authority.Ledger
                .CaptureGameplayOutcomes().exactOutcomes.Single();
            Require(row.absoluteDay == 73
                && row.participants.Single().displayText
                    == "호스트 몸값 포로"
                && row.metrics.Single(value => value.metricId
                    == CaptiveRansomOutcomeIds.AmountMetric.Value).value == 240d
                && row.metrics.Single(value => value.metricId
                    == CaptiveRansomOutcomeIds.RetaliationPressureMetric.Value).value
                    == 48d
                && row.facts.Single(value => value.factId
                    == CaptiveRansomOutcomeIds.DecisionMakerFact.Value).value == "none"
                && ransomedEvents.Count == 1
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Ransom finalization used current name/pressure/day or duplicated canonical/event output.");
        }
    }

    private static void VerifyRansomMoneyAtomicity(
        RansomRuntimeReflection activeFinalization)
    {
        var session = new GameSessionState(startingMoney: 100);
        var ledger = new EconomyTransactionLedgerRuntime(
            new FixedHostGameClock(),
            new TreasuryEconomyAggregateStateStore(
                new DungeonRuntimeAggregateRootStore()));
        var account = new GameMoneyAccount(
            new HostSessionProvider(session),
            ledger);
        var context = new EconomyTransactionContext(
            EconomyTransactionKind.RansomIncome,
            "character:captive-host:ransom-money",
            "character:captive-host:ransom-money",
            "host ransom credit");
        Action<int> captureGuard = value =>
        {
            if (value > 100)
                activeFinalization.ValidateBeforeCapture();
        };
        session.holdingMoney.OnValueChange += captureGuard;
        try
        {
            RequireThrows<InvalidOperationException>(
                () => account.TryCreditOnce(
                    120,
                    context,
                    out _),
                "Ransom money credit did not surface the active capture guard.");
            Require(account.Balance == 100
                && ledger.Records.Count == 0,
                "Capture-guard credit failure changed balance or transaction rows.");
        }
        finally
        {
            session.holdingMoney.OnValueChange -= captureGuard;
        }

        Require(account.TryCreditOnce(
                120,
                context,
                out string firstFailure)
            && firstFailure.Length == 0
            && account.TryCreditOnce(
                120,
                context,
                out string replayFailure)
            && replayFailure.Length == 0
            && account.Balance == 220
            && ledger.Records.Count == 1
            && ledger.Records[0].amount == 120
            && ledger.Records[0].balanceBefore == 100
            && ledger.Records[0].balanceAfter == 220,
            "Actual ransom money credit was not atomic and idempotent.");
    }

    private static void VerifyCaptiveEscapePublicationRuntime()
    {
        CaptiveEscapedFactData callerOwned = CreateEscapeFactData(
            "freeze",
            CaptiveEscapeKind.PhysicalEscape,
            CaptivityStatus.EscapeAttempt,
            "외벽 통과",
            42f,
            31);
        var frozenFact = new CaptiveEscapedFact(callerOwned);
        callerOwned.displayName = "변조된 이탈자";
        callerOwned.trigger = "변조된 원인";
        callerOwned.retaliationPressure = 99f;
        CaptiveEscapedFactData detachedFact = frozenFact.Snapshot();
        detachedFact.trigger = "분리본 변조";
        Require(frozenFact.Snapshot().displayName == "호스트 이탈자"
            && frozenFact.Snapshot().trigger == "외벽 통과"
            && frozenFact.Snapshot().retaliationPressure == 42f,
            "Captive escape fact retained caller or snapshot DTO mutations.");
        CaptiveEscapedFactData invalidFact = CreateEscapeFactData(
            "invalid",
            CaptiveEscapeKind.PhysicalEscape,
            CaptivityStatus.EscapeAttempt,
            "외벽 통과",
            42f,
            31);
        invalidFact.retaliationPressure = float.NaN;
        RequireThrows<ArgumentException>(
            () => new CaptiveEscapedFact(invalidFact),
            "Captive escape fact accepted a non-finite pressure.");

        var cases = new[]
        {
            new EscapeHostCase(
                "physical",
                CaptiveEscapeKind.PhysicalEscape,
                CaptivityStatus.EscapeAttempt,
                "감방 외벽 통과",
                42f),
            new EscapeHostCase(
                "arrival",
                CaptiveEscapeKind.ArrivalCustodyLoss,
                CaptivityStatus.AwaitingCapture,
                "귀환 수용 전 이탈",
                37f),
            new EscapeHostCase(
                "betrayal",
                CaptiveEscapeKind.FalseComplianceBetrayal,
                CaptivityStatus.Labor,
                "침공의 혼란",
                66f),
            new EscapeHostCase(
                "minion",
                CaptiveEscapeKind.MinionControlBreak,
                CaptivityStatus.Minion,
                "정착지 통제 거부",
                58f)
        };

        using (var authority = new EscapeAuthority())
        {
            var events = new GameEventBus();
            var observed = new List<CaptiveEscapedEvent>();
            using IDisposable subscription = events.Subscribe<
                CaptiveEscapedEvent>(observed.Add);
            var runtime = new EscapeRuntimeReflection(
                authority.Dispatcher,
                authority.Dispatcher,
                events);
            var states = new List<CaptiveState>();
            for (int index = 0; index < cases.Length; index++)
            {
                EscapeHostCase escapeCase = cases[index];
                CaptiveState state = CreateEscapeState(
                    escapeCase.Suffix,
                    escapeCase.PreviousStatus);
                Require(runtime.TryPrepare(
                        state,
                        escapeCase.Kind,
                        "  " + escapeCase.Trigger + "  ",
                        escapeCase.PreviousStatus,
                        escapeCase.Pressure,
                        40 + index,
                        out IDisposable prepared,
                        out long revision,
                        out string normalizedTrigger,
                        out string prepareFailure),
                    escapeCase.Suffix + " escape prepare failed: "
                        + prepareFailure);
                using (prepared)
                {
                    Require(revision == 1L
                        && normalizedTrigger == escapeCase.Trigger
                        && !runtime.IsReplay(prepared),
                        escapeCase.Suffix
                            + " escape prepare did not freeze canonical inputs.");
                    if (index == 0)
                    {
                        RequireThrows<InvalidOperationException>(
                            runtime.ValidateBeforeCapture,
                            "Active escape publication did not guard save capture.");
                    }
                    state.status = CaptivityStatus.Escaped;
                    state.escapeOutcomeRevision = revision;
                    state.retaliationPressure = escapeCase.Pressure;
                    DomainFactDelivery committed = runtime.Commit(prepared);
                    Require(committed.Completed,
                        escapeCase.Suffix + " escape did not commit: "
                            + committed.Detail);
                    runtime.NotifyCommitted(prepared);
                }
                runtime.ValidateBeforeCapture();
                Require(state.escapeObservation.HasFact
                    && !state.escapeObservation.pending
                    && !state.escapeObservation.observersPending
                    && state.escapeObservation.fact.kind == escapeCase.Kind
                    && state.escapeObservation.fact.trigger == escapeCase.Trigger
                    && state.escapeObservation.fact.previousStatus
                        == escapeCase.PreviousStatus
                    && state.escapeObservation.fact.retaliationPressure
                        == escapeCase.Pressure,
                    escapeCase.Suffix
                        + " escape observation lost its frozen committed shape.");
                states.Add(state);
            }

            GameplayOutcomeLedgerSaveData canonical = authority.Ledger
                .CaptureGameplayOutcomes();
            Require(OutcomeRowCount(canonical) == 4
                && canonical.exactOutcomes.Count == 4
                && observed.Count == 4,
                "Four escape kinds did not publish exactly four canonical rows and events.");
            foreach (EscapeHostCase escapeCase in cases)
            {
                CaptiveState state = states.Single(value => value.captiveId
                    == EscapeId(escapeCase.Suffix));
                GameplayOutcomeSnapshot row = canonical.exactOutcomes.Single(
                    value => value.participants.Single().entityId
                        == state.captiveId);
                Require(row.ownerRevision == 1L
                    && row.participants.Single().displayText == "호스트 이탈자"
                    && row.metrics.Single(value => value.metricId
                        == CaptiveEscapeOutcomeIds.RetaliationPressureMetric.Value).value
                        == escapeCase.Pressure
                    && row.facts.Single(value => value.factId
                        == CaptiveEscapeOutcomeIds.KindFact.Value).value
                        == CaptiveEscapeOutcomeIds.KindId(escapeCase.Kind)
                    && row.facts.Single(value => value.factId
                        == CaptiveEscapeOutcomeIds.TriggerFact.Value).value
                        == escapeCase.Trigger
                    && row.tags.Count
                        == (CaptiveEscapedFact.IsBetrayal(escapeCase.Kind) ? 2 : 1),
                    escapeCase.Suffix
                        + " canonical escape row lost its expected shape.");
            }

            CaptiveState first = states[0];
            CaptiveEscapedFactData changed =
                first.escapeObservation.fact.Clone();
            changed.retaliationPressure += 1f;
            var tamperResults = new DomainFactDelivery[1];
            bool tamperAccepted = authority.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        first.escapeObservation.publicationKey,
                        new CaptiveEscapedFact(changed))
                },
                tamperResults,
                out string tamperFailure);
            Require(!tamperAccepted
                && tamperResults[0].Phase == DomainFactDeliveryPhase.Rejected
                && !tamperResults[0].MayBeCommitted
                && !string.IsNullOrWhiteSpace(tamperFailure)
                && OutcomeRowCount(authority.Ledger.CaptureGameplayOutcomes()) == 4,
                "Same-key valid escape pressure tamper was not canonically rejected.");

            CaptiveState detached = first.Clone();
            first.escapeObservation.fact.trigger = "변조된 저장 원인";
            first.escapeObservation = new CaptiveEscapeObservationData();
            Require(detached.escapeObservation.HasFact
                && detached.escapeObservation.fact.trigger == cases[0].Trigger
                && !detached.escapeObservation.pending,
                "Captive state clone did not deeply copy its escape observation.");
        }

        using (var authority = new EscapeAuthority())
        {
            var events = new GameEventBus();
            int eventCount = 0;
            using IDisposable subscription = events.Subscribe<
                CaptiveEscapedEvent>(_ => eventCount++);
            var publisher = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.RejectCommit);
            var runtime = new EscapeRuntimeReflection(
                publisher,
                authority.Dispatcher,
                events);
            CaptiveState state = CreateEscapeState(
                "rejected",
                CaptivityStatus.Labor);
            Require(runtime.TryPrepare(
                    state,
                    CaptiveEscapeKind.FalseComplianceBetrayal,
                    "배신 기회",
                    CaptivityStatus.Labor,
                    55f,
                    50,
                    out IDisposable prepared,
                    out long revision,
                    out _,
                    out string prepareFailure),
                "Rejected escape prepare failed: " + prepareFailure);
            using (prepared)
            {
                state.status = CaptivityStatus.Escaped;
                state.escapeOutcomeRevision = revision;
                state.retaliationPressure = 55f;
                DomainFactDelivery rejected = runtime.Commit(prepared);
                Require(!rejected.MayBeCommitted
                    && rejected.Detail.Contains(
                        "host-captive-definite-rejection",
                        StringComparison.Ordinal)
                    && state.status == CaptivityStatus.Escaped
                    && state.escapeOutcomeRevision == 1L
                    && state.retaliationPressure == 55f
                    && !state.escapeObservation.HasFact
                    && eventCount == 0
                    && OutcomeRowCount(
                        authority.Ledger.CaptureGameplayOutcomes()) == 0,
                    "Known escape rejection did not cancel its real stage, or the host incorrectly claimed owner rollback.");
            }
        }

        CaptivitySaveData pendingCaptivity;
        GameplayOutcomeLedgerSaveData pendingLedger;
        string pendingPublicationKey;
        using (var authority = new EscapeAuthority())
        {
            var events = new GameEventBus();
            int earlyEvents = 0;
            using IDisposable subscription = events.Subscribe<
                CaptiveEscapedEvent>(_ => earlyEvents++);
            var publisher = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.CommitThenUncertain);
            var runtime = new EscapeRuntimeReflection(
                publisher,
                authority.Dispatcher,
                events);
            CaptiveState state = CreateEscapeState(
                "pending",
                CaptivityStatus.Minion);
            Require(runtime.TryPrepare(
                    state,
                    CaptiveEscapeKind.MinionControlBreak,
                    "  통제 거부  ",
                    CaptivityStatus.Minion,
                    65f,
                    61,
                    out IDisposable prepared,
                    out long revision,
                    out string normalizedTrigger,
                    out string prepareFailure),
                "Pending escape prepare failed: " + prepareFailure);
            using (prepared)
            {
                state.status = CaptivityStatus.Escaped;
                state.escapeOutcomeRevision = revision;
                state.retaliationPressure = 65f;
                DomainFactDelivery pending = runtime.Commit(prepared);
                runtime.NotifyCommitted(prepared);
                Require(pending.Phase == DomainFactDeliveryPhase.InDoubt
                    && pending.MayBeCommitted
                    && normalizedTrigger == "통제 거부"
                    && state.escapeObservation.HasFact
                    && state.escapeObservation.pending
                    && state.escapeObservation.observersPending
                    && earlyEvents == 0
                    && OutcomeRowCount(
                        authority.Ledger.CaptureGameplayOutcomes()) == 1,
                    "Commit-then-uncertain escape did not retain one frozen pending publication.");
            }

            pendingPublicationKey = state.escapeObservation.publicationKey;
            Require(!string.IsNullOrWhiteSpace(pendingPublicationKey)
                && state.escapeObservation.fact.displayName == "호스트 이탈자"
                && state.escapeObservation.fact.retaliationPressure == 65f
                && state.escapeObservation.fact.absoluteDay == 61,
                "Pending escape did not freeze its original name, pressure, and day.");
            state.displayName = "현재 이탈자 이름";
            state.retaliationPressure = 7f;
            state.capturedAbsoluteDay = 99;
            pendingCaptivity = new CaptivitySaveData
            {
                captives = new List<CaptiveState> { state.Clone() },
                policies = new List<CaptivePolicyData>
                {
                    new()
                    {
                        policyId = CaptivityPolicyIds.Standard,
                        displayName = "표준 수용"
                    }
                }
            };
            pendingLedger = authority.Ledger.CaptureGameplayOutcomes();
        }

        var saved = new SavedEscapeAuthority
        {
            serializer = HostSerializer,
            captivity = pendingCaptivity,
            ledger = pendingLedger
        };
        string json = JsonConvert.SerializeObject(saved);
        SavedEscapeAuthority exact = JsonConvert
            .DeserializeObject<SavedEscapeAuthority>(json)
            ?? throw new InvalidOperationException(
                "Host JSON captive escape state/ledger round-trip returned null.");
        var shapeReport = new DungeonGameRestoreReport();
        CaptivitySaveValidation.Validate(exact.captivity, shapeReport);
        Require(shapeReport.Success,
            "Host JSON captive escape state failed V7 validation: "
                + string.Join(" | ", shapeReport.Errors));
        DungeonGameRestoreReport joinReport = ValidateEscapeJoin(
            exact.captivity,
            exact.ledger);
        Require(joinReport.Success,
            "Canonical captive escape save join failed: "
                + string.Join(" | ", joinReport.Errors));
        CaptiveState exactState = exact.captivity.captives.Single();
        Require(exactState.displayName == "현재 이탈자 이름"
            && exactState.retaliationPressure == 7f
            && exactState.capturedAbsoluteDay == 99
            && exactState.escapeObservation.publicationKey
                == pendingPublicationKey
            && exactState.escapeObservation.pending
            && exactState.escapeObservation.observersPending
            && exactState.escapeObservation.fact.displayName == "호스트 이탈자"
            && exactState.escapeObservation.fact.retaliationPressure == 65f
            && exactState.escapeObservation.fact.absoluteDay == 61,
            "Host JSON escape round-trip rewrote the pending fact from current state.");

        CaptivitySaveData tamperedSave = JsonConvert.DeserializeObject<
            CaptivitySaveData>(JsonConvert.SerializeObject(exact.captivity))
            ?? throw new InvalidOperationException(
                "Host JSON escape tamper copy returned null.");
        tamperedSave.captives.Single().escapeObservation.fact
            .retaliationPressure += 1f;
        DungeonGameRestoreReport tamperReport = ValidateEscapeJoin(
            tamperedSave,
            exact.ledger);
        Require(!tamperReport.Success,
            "Escape preflight accepted a same-key valid pressure tamper.");

        IReadOnlyDictionary<string, string> canonicalHashes =
            CanonicalHashes(exact.ledger);
        using (var restored = new EscapeAuthority())
        {
            restored.Restore(exact.ledger);
            var events = new GameEventBus();
            var observed = new List<CaptiveEscapedEvent>();
            using IDisposable subscription = events.Subscribe<
                CaptiveEscapedEvent>(observed.Add);
            var runtime = new EscapeRuntimeReflection(
                restored.Dispatcher,
                restored.Dispatcher,
                events);
            CaptiveState state = exactState.Clone();
            Require(runtime.Recover(state, out string recoveryFailure)
                && recoveryFailure.Length == 0
                && !state.escapeObservation.pending
                && !state.escapeObservation.observersPending
                && observed.Count == 1
                && observed[0].Trigger == "통제 거부"
                && observed[0].Betrayal,
                "Fresh canonical escape authority did not recover and notify exactly once: "
                    + recoveryFailure);
            RequireCanonicalEqual(
                canonicalHashes,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Canonical escape recovery duplicated or rewrote its row.");
            GameplayOutcomeSnapshot row = restored.Ledger
                .CaptureGameplayOutcomes().exactOutcomes.Single();
            Require(row.absoluteDay == 61
                && row.participants.Single().displayText == "호스트 이탈자"
                && row.metrics.Single(value => value.metricId
                    == CaptiveEscapeOutcomeIds.RetaliationPressureMetric.Value).value
                    == 65d
                && runtime.Recover(state, out string repeatedFailure)
                && repeatedFailure.Length == 0
                && observed.Count == 1
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1,
                "Escape recovery used live name/pressure/day or notified twice.");
        }

        using (var uncommitted = new EscapeAuthority())
        {
            GameplayOutcomeLedgerSaveData emptyLedger = uncommitted.Ledger
                .CaptureGameplayOutcomes();
            DungeonGameRestoreReport detachedJoin = ValidateEscapeJoin(
                exact.captivity,
                emptyLedger);
            Require(detachedJoin.Success,
                "Pending escape without canonical row was rejected: "
                    + string.Join(" | ", detachedJoin.Errors));
            var events = new GameEventBus();
            int eventCount = 0;
            using IDisposable subscription = events.Subscribe<
                CaptiveEscapedEvent>(_ => eventCount++);
            var runtime = new EscapeRuntimeReflection(
                uncommitted.Dispatcher,
                uncommitted.Dispatcher,
                events);
            CaptiveState state = exactState.Clone();
            Require(runtime.Recover(state, out string recoveryFailure)
                && recoveryFailure.Length == 0
                && !state.escapeObservation.pending
                && !state.escapeObservation.observersPending
                && eventCount == 1
                && OutcomeRowCount(
                    uncommitted.Ledger.CaptureGameplayOutcomes()) == 1
                && runtime.Recover(state, out string repeatedFailure)
                && repeatedFailure.Length == 0
                && eventCount == 1
                && OutcomeRowCount(
                    uncommitted.Ledger.CaptureGameplayOutcomes()) == 1,
                "Precommit escape recovery did not create one canonical row and one notification.");
        }
    }

    private static void VerifyCaptivePerformerMilestoneRuntime()
    {
        using (var authority = new MilestoneAuthority())
        {
            var port = new RecordingMilestonePort();
            var runtime = new CaptivePerformerMilestoneOutcomeRuntime(
                authority.Dispatcher,
                authority.Dispatcher,
                port);
            CaptiveState state = CreatePerformerState();
            Require(runtime.TryCommitEligibleMilestones(
                    state,
                    17,
                    out string failure)
                && failure.Length == 0,
                "Captive performer milestone cohort failed: " + failure);
            Require(state.carePriorityUnlocked
                && state.staffContractUnlocked
                && state.finalContractPending
                && state.performerMilestoneOutcomeRevision == 3L
                && state.carePriorityOutcomeRevision == 1L
                && state.staffContractOutcomeRevision == 2L
                && state.finalContractOutcomeRevision == 3L
                && state.performerMilestones.Count == 3
                && state.performerMilestones.All(value =>
                    !value.pending && !value.observersPending)
                && state.performerMilestones.Select(value =>
                    value.fact.threshold).SequenceEqual(new[] { 50, 75, 100 })
                && port.Thresholds.SequenceEqual(new[] { 50, 75, 100 })
                && port.AlertCount == 3
                && OutcomeRowCount(authority.Ledger.CaptureGameplayOutcomes()) == 3,
                "Captive fame 100 did not commit the ordered 50/75/100 cohort once.");

            CaptivityStateTransitionRules.ClearCaptiveOnlyState(state);
            Require(!state.carePriorityUnlocked
                && state.carePriorityOutcomeRevision == 1L,
                "Released captive cleanup did not preserve the milestone revision high-water.");
            Require(runtime.TryCommitEligibleMilestones(
                    state,
                    18,
                    out string duplicateFailure)
                && duplicateFailure.Length == 0
                && !state.carePriorityUnlocked
                && state.carePriorityOutcomeRevision == 1L
                && state.performerMilestones.Count == 3
                && port.Thresholds.Count == 3
                && OutcomeRowCount(authority.Ledger.CaptureGameplayOutcomes()) == 3,
                "Released captive cleanup caused a milestone regrant or duplicate history.");

            CaptiveState detached = state.Clone();
            state.performerMilestones[0].fact.displayName = "변조된 공연자";
            state.performerMilestones[0].fact.fame = 0f;
            state.performerMilestones.Clear();
            Require(detached.performerMilestones.Count == 3
                && detached.performerMilestones[0].fact.displayName == "호스트 공연자"
                && detached.performerMilestones[0].fact.fame == 100f
                && detached.performerMilestones[0].fact.absoluteDay == 17,
                "Captive state clone did not deeply freeze milestone observations.");
        }

        using (var authority = new MilestoneAuthority())
        {
            var port = new RecordingMilestonePort();
            var publisher = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.RejectCommit);
            var runtime = new CaptivePerformerMilestoneOutcomeRuntime(
                publisher,
                authority.Dispatcher,
                port);
            CaptiveState rejected = CreatePerformerState(50f);
            rejected.lastResult = "공연 완료";
            rejected.performerSkill = 41f;
            Require(!runtime.TryCommitEligibleMilestones(
                    rejected,
                    19,
                    out string rejectFailure)
                && rejectFailure.Contains(
                    "host-captive-definite-rejection",
                    StringComparison.Ordinal)
                && rejected.performerFame == 50f
                && rejected.performerSkill == 41f
                && rejected.lastResult == "공연 완료"
                && !rejected.carePriorityUnlocked
                && rejected.performerMilestoneOutcomeRevision == 0L
                && rejected.carePriorityOutcomeRevision == 0L
                && rejected.performerMilestones.Count == 0
                && port.Thresholds.Count == 0
                && port.AlertCount == 0
                && OutcomeRowCount(authority.Ledger.CaptureGameplayOutcomes()) == 0,
                "Known milestone rejection did not cancel the real stage and roll back only milestone state.");
        }

        using (var authority = new MilestoneAuthority())
        {
            var port = new RecordingMilestonePort();
            var publisher = new MilestoneStagePublisher(
                authority.Dispatcher,
                MilestoneStageBehavior.CommitThenUncertain);
            var runtime = new CaptivePerformerMilestoneOutcomeRuntime(
                publisher,
                authority.Dispatcher,
                port);
            CaptiveState pending = CreatePerformerState();
            Require(runtime.TryCommitEligibleMilestones(
                    pending,
                    23,
                    out string pendingFailure)
                && pendingFailure.Contains(
                    "captive-milestone-delivery-pending",
                    StringComparison.Ordinal)
                && pending.carePriorityUnlocked
                && pending.performerMilestoneOutcomeRevision == 1L
                && pending.carePriorityOutcomeRevision == 1L
                && pending.performerMilestones.Count == 1
                && pending.performerMilestones[0].pending
                && pending.performerMilestones[0].observersPending
                && port.Thresholds.Count == 0
                && port.AlertCount == 0
                && OutcomeRowCount(authority.Ledger.CaptureGameplayOutcomes()) == 1,
                "Commit-then-uncertain did not retain one durable pending milestone.");
            CaptivePerformerMilestoneFactData frozen =
                pending.performerMilestones[0].fact;
            string pendingPublicationKey =
                pending.performerMilestones[0].publicationKey;
            Require(!string.IsNullOrWhiteSpace(pendingPublicationKey)
                && frozen.displayName == "호스트 공연자"
                && frozen.fame == 100f
                && frozen.absoluteDay == 23
                && frozen.threshold == 50
                && frozen.revision == 1L,
                "Pending milestone did not freeze the original identity, fame, day, and revision.");

            pending.displayName = "현재 이름 변경";
            pending.performerFame = 0f;
            var saved = new SavedMilestoneAuthority
            {
                serializer = HostSerializer,
                captivity = new CaptivitySaveData
                {
                    captives = new List<CaptiveState> { pending.Clone() },
                    policies = new List<CaptivePolicyData>
                    {
                        new()
                        {
                            policyId = CaptivityPolicyIds.Standard,
                            displayName = "표준 수용"
                        }
                    }
                },
                ledger = authority.Ledger.CaptureGameplayOutcomes()
            };
            IReadOnlyDictionary<string, string> canonical =
                CanonicalHashes(saved.ledger);
            string json = JsonConvert.SerializeObject(saved);
            SavedMilestoneAuthority exact = JsonConvert
                .DeserializeObject<SavedMilestoneAuthority>(json)
                ?? throw new InvalidOperationException(
                    "Host JSON captive milestone state/ledger round-trip returned null.");
            var shapeReport = new DungeonGameRestoreReport();
            CaptivitySaveValidation.Validate(exact.captivity, shapeReport);
            Require(shapeReport.Success,
                "Host JSON captive milestone state failed current-version validation: "
                    + string.Join(" | ", shapeReport.Errors));
            Require(ValidateMilestoneJoin(exact.captivity, exact.ledger).Success,
                "Frozen milestone pending fact failed actual canonical save preflight.");
            var tampered = JsonConvert.DeserializeObject<CaptivitySaveData>(JsonConvert.SerializeObject(exact.captivity));
            tampered.captives[0].performerMilestones[0].fact.fame = 99f;
            Require(!ValidateMilestoneJoin(tampered, exact.ledger).Success,
                "Milestone save preflight accepted a valid-shaped but altered historical fame.");
            CaptiveState restoredState = exact.captivity.captives.Single();
            using (var notCommitted = new MilestoneAuthority())
            {
                var precommitState = restoredState.Clone();
                var beforeCommitSave = new CaptivitySaveData
                {
                    captives = new List<CaptiveState> { precommitState },
                    policies = exact.captivity.policies
                };
                Require(ValidateMilestoneJoin(beforeCommitSave, notCommitted.Ledger.CaptureGameplayOutcomes()).Success,
                    "Milestone preflight rejected an uncertain fact with no prior canonical row.");
                var precommitRuntime = new CaptivePerformerMilestoneOutcomeRuntime(
                    notCommitted.Dispatcher, notCommitted.Dispatcher, new RecordingMilestonePort());
                Require(precommitRuntime.TryCommitNextPendingMilestone(new[] { precommitState }, 98, out bool retry, out _)
                    && retry && !precommitState.performerMilestones[0].pending
                    && OutcomeRowCount(notCommitted.Ledger.CaptureGameplayOutcomes()) == 1,
                    "Milestone recovery could not publish an original fact whose first delivery never committed.");
                Require(!ValidateMilestoneJoin(beforeCommitSave, new GameplayOutcomeLedgerSaveData()).Success,
                    "Milestone preflight accepted completed state without its canonical row.");
            }
            Require(restoredState.displayName == "현재 이름 변경"
                && restoredState.performerFame == 0f
                && restoredState.performerMilestones.Count == 1
                && restoredState.performerMilestones[0].publicationKey
                    == pendingPublicationKey
                && restoredState.performerMilestones[0].pending
                && restoredState.performerMilestones[0].observersPending
                && restoredState.performerMilestones[0].fact.displayName == "호스트 공연자"
                && restoredState.performerMilestones[0].fact.fame == 100f
                && restoredState.performerMilestones[0].fact.absoluteDay == 23,
                "Host JSON round-trip rewrote the pending milestone from current captive state.");

            using var restored = new MilestoneAuthority();
            restored.Restore(exact.ledger);
            var restoredPort = new RecordingMilestonePort();
            var restoredRuntime = new CaptivePerformerMilestoneOutcomeRuntime(
                restored.Dispatcher,
                restored.Dispatcher,
                restoredPort);
            Require(restoredRuntime.TryCommitNextPendingMilestone(
                    new[] { restoredState },
                    99,
                    out bool attempted,
                    out string recoveryFailure)
                && attempted
                && recoveryFailure.Length == 0
                && restoredState.carePriorityUnlocked
                && restoredState.performerMilestoneOutcomeRevision == 1L
                && restoredState.carePriorityOutcomeRevision == 1L
                && restoredState.performerMilestones.Count == 1
                && !restoredState.performerMilestones[0].pending
                && !restoredState.performerMilestones[0].observersPending
                && restoredPort.Thresholds.SequenceEqual(new[] { 50 })
                && restoredPort.AlertCount == 1,
                "Fresh authority did not complete pending milestone recovery exactly once: "
                    + recoveryFailure);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Pending milestone recovery duplicated or rewrote its canonical row.");
            GameplayOutcomeSnapshot row = restored.Ledger
                .CaptureGameplayOutcomes().exactOutcomes.Single();
            Require(row.absoluteDay == 23
                && row.participants.Single().displayText == "호스트 공연자"
                && row.metrics.Single(value => value.metricId
                    == CaptivePerformerMilestoneOutcomeIds.FameMetric.Value).value == 100d,
                "Recovered milestone used current captive name, fame, or recovery day.");
            Require(restoredRuntime.TryCommitNextPendingMilestone(
                    new[] { restoredState },
                    100,
                    out bool repeated,
                    out string repeatedFailure)
                && !repeated
                && repeatedFailure.Length == 0
                && restoredPort.Thresholds.Count == 1
                && OutcomeRowCount(restored.Ledger.CaptureGameplayOutcomes()) == 1,
                "Repeated milestone recovery reapplied state, notification, or canonical output.");
        }
    }

    private static DungeonGameRestoreReport ValidateMilestoneJoin(CaptivitySaveData captivity, GameplayOutcomeLedgerSaveData ledger)
    {
        var report = new DungeonGameRestoreReport();
        var method = typeof(CaptivePerformerMilestoneFactPreflight).GetMethod("Validate",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null,
            new[] { typeof(CaptivitySaveData), typeof(GameplayOutcomeLedgerSaveData), typeof(DungeonGameRestoreReport) }, null)
            ?? throw new InvalidOperationException("Actual milestone save preflight entry is missing.");
        method.Invoke(null, new object[] { captivity, ledger, report });
        return report;
    }

    private static void VerifyIdleSocialInteractionObserver()
    {
        IdleSocialInteractionFactData source = CreateIdleSocialData();
        IdleSocialInteractionFactData proposedData = CreateIdleSocialData();
        proposedData.actorRelationshipAfter =
            proposedData.actorRelationshipBefore;
        proposedData.targetRelationshipAfter =
            proposedData.targetRelationshipBefore;
        proposedData.moodAfter = proposedData.moodBefore;
        var proposal = new IdleSocialInteractionResolvedFact(
            proposedData,
            isProposal: true);
        var fact = new IdleSocialInteractionResolvedFact(source);
        Require(fact.MatchesProposal(proposal),
            "Idle social actual fact did not match its exact proposal.");

        source.actorName = "변조된 행위자";
        source.actorRelationshipAfter = 999f;
        IdleSocialInteractionFactData frozen = fact.Snapshot();
        Require(frozen.actorName == "호스트 대화자"
            && frozen.actorRelationshipAfter == 0.4f,
            "Idle social fact retained caller-owned DTO state.");
        frozen.targetName = "변조된 대상";
        frozen.moodAfter = -999f;
        Require(fact.Snapshot().targetName == "호스트 대화 상대"
            && fact.Snapshot().moodAfter == 61f,
            "Idle social fact snapshot was not defensively cloned.");

        string expectedKey = new GameplayResultKey(
            IdleSocialInteractionOutcomeIds.ProducerId,
            new GameplayOperationId(fact.Snapshot().operationId),
            0L,
            0).ToString();
        var pending = new IdleSocialInteractionPendingState();
        pending.Add(new IdleSocialInteractionPendingData
        {
            publicationKey = expectedKey,
            fact = fact.Snapshot()
        });
        Require(pending.Count == 1
            && pending.ContainsParticipant("character:idle-host:actor")
            && pending.ContainsParticipant("character:idle-host:target"),
            "Idle social pending state lost participant ownership.");
        RequireThrows<InvalidOperationException>(
            () => pending.Add(new IdleSocialInteractionPendingData
            {
                publicationKey = expectedKey,
                fact = fact.Snapshot()
            }),
            "Idle social pending state accepted a duplicate fact/key.");

        using var authority = new IdleAuthority();
        Require(authority.Dispatcher.TryReserve(
                proposal,
                out DomainFactLease lease,
                out string reserveFailure),
            "Idle social proposal reservation failed: " + reserveFailure);
        Require(lease.PublicationKey == expectedKey,
            "Idle social proposal reserved an unexpected publication key.");
        DomainFactDelivery published = authority.Dispatcher.Publish(
            lease,
            fact);
        Require(published.Completed
            && published.OperationKey == expectedKey,
            "Idle social actual publication did not complete: "
                + published.Detail);
        Require(OutcomeRowCount(authority.Ledger.CaptureGameplayOutcomes()) == 1,
            "Idle social publication did not create exactly one native outcome.");

        var saved = new SavedIdleAuthority
        {
            serializer = HostSerializer,
            pending = pending.Capture(),
            ledger = authority.Ledger.CaptureGameplayOutcomes()
        };
        string json = JsonConvert.SerializeObject(saved);
        SavedIdleAuthority exact = JsonConvert
            .DeserializeObject<SavedIdleAuthority>(json)
            ?? throw new InvalidOperationException(
                "Host JSON idle social save round-trip returned null.");
        IdleSocialInteractionPendingState restoredPending =
            IdleSocialInteractionPendingState.Restore(exact.pending);
        Require(restoredPending.Count == 1
            && restoredPending.ContainsParticipant(
                "character:idle-host:actor")
            && restoredPending.ContainsParticipant(
                "character:idle-host:target"),
            "Idle social pending JSON round-trip lost participants.");
        IdleSocialInteractionPendingData savedItem = exact.pending.pending[0];
        IReadOnlyDictionary<string, string> canonical =
            CanonicalHashes(exact.ledger);

        using (var restored = new IdleAuthority())
        {
            restored.Restore(exact.ledger);
            var savedFact = new IdleSocialInteractionResolvedFact(
                savedItem.fact);
            DomainFactDelivery exactReplay = ReplayIdle(
                restored,
                savedItem.publicationKey,
                savedFact);
            Require(exactReplay.Completed
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1,
                "Idle social exact recovery duplicated or lost its outcome.");
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Idle social exact recovery changed canonical rows.");

            var runtime = new IdleSocialInteractionCommandRuntime(restored.Dispatcher, restored.Dispatcher,
                new DungeonRuntimeAggregateRootStore(), new ForbiddenRecoveryCalendar());
            runtime.PublishRestore(runtime.PrepareRestore(exact.pending));
            Require(runtime.Capture().pending.Count == 1 && runtime.Recover() && runtime.Capture().pending.Count == 0,
                "Actual idle command runtime did not recover and clear its restored pending fact.");
            RequireCanonicalEqual(canonical, CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Actual idle runtime recovery duplicated an already committed fact.");

            IdleSocialInteractionFactData changed = savedFact.Snapshot();
            changed.moodAfter += 0.1f;
            var replies = new DomainFactDelivery[1];
            bool accepted = restored.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        savedItem.publicationKey,
                        new IdleSocialInteractionResolvedFact(changed))
                },
                replies,
                out string failure);
            Require(!accepted
                && replies[0].Phase == DomainFactDeliveryPhase.Rejected
                && !replies[0].MayBeCommitted
                && !string.IsNullOrWhiteSpace(failure),
                "Idle social same-key metric tamper was not rejected.");
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Idle social metric tamper mutated canonical rows.");
        }

        restoredPending.Remove(savedItem.publicationKey);
        Require(restoredPending.Count == 0,
            "Idle social pending removal did not clear the saved key.");

        using (var uncommitted = new IdleAuthority())
        {
            var runtime = new IdleSocialInteractionCommandRuntime(uncommitted.Dispatcher, uncommitted.Dispatcher,
                new DungeonRuntimeAggregateRootStore(), new ForbiddenRecoveryCalendar());
            runtime.PublishRestore(runtime.PrepareRestore(exact.pending));
            Require(runtime.Recover() && runtime.Capture().pending.Count == 0
                && OutcomeRowCount(uncommitted.Ledger.CaptureGameplayOutcomes()) == 1,
                "Actual idle runtime failed recovery when the original observation had not committed.");
            Require(runtime.Recover() && OutcomeRowCount(uncommitted.Ledger.CaptureGameplayOutcomes()) == 1,
                "Repeated idle runtime recovery created another observation.");
        }
    }

    // Recovery must use the historical fact, not today's clock or live actors.
    private sealed class ForbiddenRecoveryCalendar : IGameCalendar
    {
        private static T Read<T>() => throw new InvalidOperationException("Idle recovery read the current calendar.");
        public int Day => Read<int>();
        public int Hour => Read<int>();
        public int Year => Read<int>();
        public int DayOfYear => Read<int>();
        public Season Season => Read<Season>();
        public int DayOfSeason => Read<int>();
        public long AbsoluteHour => Read<long>();
        public float ElapsedSeconds => Read<float>();
        public TimeOfDay TimeOfDay => Read<TimeOfDay>();
        public bool IsRunning => Read<bool>();
        public CalendarDateTime Current => Read<CalendarDateTime>();
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) => Read<CalendarDateTime>();
        public void Start() => Read<object>();
        public void SetDateTime(int day, int hour) => Read<object>();
    }

    private static void VerifyCombatEquipmentQualityObserver()
    {
        var fact = new CombatEquipmentQualityResolvedFact(
            "combat-quality:host-probe",
            attemptIndex: 0,
            makerCharacterId: "character:combat-host:quality-maker",
            makerDisplayName: "호스트 품질 제작자",
            definitionId: "combat-equipment:host-quality-blade",
            quality: CraftsmanshipQualityTier.Good,
            absoluteDay: 44,
            rejectedBelowMinimum: false);
        using var authority = new QualityAuthority();
        Require(authority.Dispatcher.SubscribedFactTypes.SequenceEqual(
                new[] { typeof(CombatEquipmentQualityResolvedFact) }),
            "Equipment quality observer binding was not explicit.");
        Require(authority.Dispatcher.TryStage(
                fact,
                out IDomainFactStage stage,
                out string stageFailure),
            "Equipment quality fact staging failed: " + stageFailure);
        string publicationKey = stage.OperationKey;
        string expectedKey = new GameplayResultKey(
            EvolutionOutcomeIds.ProductQualityProducerId,
            new GameplayOperationId(fact.OperationId),
            0L,
            0).ToString();
        Require(stage.IsActive
            && !stage.IsReplay
            && stage.OwnerRevision == 0L
            && publicationKey == expectedKey,
            "Equipment quality stage did not preserve first-attempt owner revision 0.");

        DomainFactDelivery committed = stage.Commit();
        Require(committed.Completed
            && committed.OperationKey == publicationKey,
            "Equipment quality stage did not commit exactly: "
                + committed.Detail);
        GameplayOutcomeLedgerSaveData first = authority.Ledger
            .CaptureGameplayOutcomes();
        Require(OutcomeRowCount(first) == 1
            && first.exactOutcomes.Count == 1
            && first.exactOutcomes[0].commitRevision == 0L
            && ResultKey(first.exactOutcomes[0]) == publicationKey,
            "Equipment quality stage did not create one revision-0 native outcome.");

        DomainFactDelivery sameReplay = ReplayQuality(
            authority,
            publicationKey,
            fact);
        Require(sameReplay.Completed
            && OutcomeRowCount(authority.Ledger.CaptureGameplayOutcomes()) == 1,
            "Equipment quality same-fact replay duplicated or lost its outcome.");

        var saved = new SavedQualityAuthority
        {
            serializer = HostSerializer,
            ledger = authority.Ledger.CaptureGameplayOutcomes()
        };
        string json = JsonConvert.SerializeObject(saved);
        SavedQualityAuthority exact = JsonConvert
            .DeserializeObject<SavedQualityAuthority>(json)
            ?? throw new InvalidOperationException(
                "Host JSON equipment quality ledger round-trip returned null.");
        IReadOnlyDictionary<string, string> canonical =
            CanonicalHashes(exact.ledger);

        using (var restored = new QualityAuthority())
        {
            restored.Restore(exact.ledger);
            DomainFactDelivery exactReplay = ReplayQuality(
                restored,
                publicationKey,
                fact);
            Require(exactReplay.Completed,
                "Fresh quality authority rejected exact fact replay: "
                    + exactReplay.Detail);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Fresh equipment quality replay changed canonical rows.");

            var changedQuality = new CombatEquipmentQualityResolvedFact(
                fact.OperationId,
                fact.AttemptIndex,
                fact.MakerCharacterId,
                fact.MakerDisplayName,
                fact.DefinitionId,
                CraftsmanshipQualityTier.Excellent,
                fact.AbsoluteDay,
                fact.RejectedBelowMinimum);
            var replies = new DomainFactDelivery[1];
            bool accepted = restored.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        publicationKey,
                        changedQuality)
                },
                replies,
                out string failure);
            Require(!accepted
                && replies[0].Phase == DomainFactDeliveryPhase.Rejected
                && !replies[0].MayBeCommitted
                && !string.IsNullOrWhiteSpace(failure),
                "Changed quality with the same key was not canonically rejected.");
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Changed equipment quality replay mutated canonical rows.");
        }
    }

    private static void VerifyCraftCompletionFactFreezeAndValidation()
    {
        CombatEquipmentCraftCompletionFactData source = CreateCraftCompletionData();
        var fact = new CombatEquipmentCraftCompletedFact(source);
        string frozenFingerprint = fact.ContentFingerprint();

        source.materialTransferInputs[0].itemId = "item:mutated-input";
        source.contributions[0].contributedWork = 999f;
        source.commandReceipts[0].beforeValue = "mutated";
        source.outputs[0].displayText = "변조된 산출물";
        source.outputs.Add(new ProductionDomainPublishedStackSaveData
        {
            outputLineId = "line:mutated",
            itemId = "item:mutated",
            stackId = "stack:mutated",
            quantity = 1,
            massGrams = 1,
            displayText = "변조"
        });
        Require(fact.ContentFingerprint() == frozenFingerprint,
            "Combat craft fact retained caller-owned nested DTO references.");

        CombatEquipmentCraftCompletionFactData detached = fact.Snapshot();
        detached.outputs[0].quantity = 99;
        detached.materialTransferInputs.Clear();
        Require(fact.ContentFingerprint() == frozenFingerprint
            && fact.Snapshot().outputs.Count == 2
            && fact.Snapshot().materialTransferInputs.Count == 1,
            "Combat craft fact snapshot was not defensively cloned.");

        CombatEquipmentCraftCompletionFactData invalidQuantity =
            CreateCraftCompletionData();
        invalidQuantity.outputQuantity++;
        RequireThrows<ArgumentException>(
            () => new CombatEquipmentCraftCompletedFact(invalidQuantity),
            "Combat craft fact accepted an output quantity total mismatch.");

        CombatEquipmentCraftCompletionFactData invalidMass =
            CreateCraftCompletionData();
        invalidMass.outputMassGrams++;
        RequireThrows<ArgumentException>(
            () => new CombatEquipmentCraftCompletedFact(invalidMass),
            "Combat craft fact accepted an output mass total mismatch.");

        // A legal policy change for the next attempt must not rewrite this
        // completed output's frozen history or prevent its pending delivery.
        CombatEquipmentCraftCompletionFactData frozen = CreateCraftCompletionData();
        var order = new CombatEquipmentCraftOrderSaveData
        {
            orderId = frozen.orderId, qualityAttemptIndex = frozen.qualityAttemptIndex,
            facilityPersistentId = frozen.facilityPersistentId, definitionId = frozen.definitionId,
            materialId = frozen.materialId, completedWork = frozen.completedWork,
            resolvedQuality = frozen.resolvedQuality, minimumQuality = frozen.minimumQuality,
            rejectedDisposition = frozen.rejectedDisposition, resolvedMakerCharacterId = frozen.resolvedMakerCharacterId,
            resolvedMakerDisplayName = frozen.resolvedMakerDisplayName, resolvedAbsoluteDay = frozen.resolvedAbsoluteDay,
            materialTransferCommitId = frozen.materialTransferCommitId, materialTransferMassGrams = frozen.materialTransferMassGrams,
            materialTransferInputs = frozen.materialTransferInputs, contributions = frozen.contributions,
            commandReceipts = frozen.commandReceipts, commandRevision = 1L,
            qualityOutcomeResultKey = frozen.qualityOutcomeResultKey, outputQuantity = frozen.outputQuantity,
            outputItemId = frozen.outputs[0].itemId, attemptOutcomeResolved = true, qualityOutcomeCommitted = true
        };
        frozen.outputCommitId = CombatEquipmentCraftOutputTransaction.BatchCommitId(order);
        frozen.outcomeFingerprint = CombatEquipmentCraftOutputTransaction.CaptureOutcomeFingerprint(order);
        order.completionFact = frozen.Clone();
        order.outputPublication = new ProductionDomainOutputPublicationSaveData
        {
            observationExpected = true, outputPublished = true, admissionCommitted = true,
            ownerFacilityId = frozen.facilityPersistentId, batchCommitId = frozen.outputCommitId,
            outcomeFingerprint = frozen.outcomeFingerprint, outputMassGrams = frozen.outputMassGrams,
            observationOwnerRevision = frozen.ownerRevision, stacks = frozen.outputs
        };
        string originalHistory = CombatEquipmentCraftCompletedFact.Restore(order).ContentFingerprint();
        order.commandReceipts.Add(new CombatCraftCommandReceiptSaveData
        {
            commandKind = "worker-policy", revision = ++order.commandRevision,
            beforeValue = "anyone", afterValue = "best-worker", absoluteDay = 34
        });
        Require(CombatEquipmentCraftCompletedFact.Restore(order).ContentFingerprint() == originalHistory,
            "A later worker-policy command changed the pending completion fact.");
        order.commandReceipts[0].afterValue = "tampered-old-command";
        RequireThrows<InvalidOperationException>(() => CombatEquipmentCraftCompletedFact.Restore(order),
            "Changing a command inside the frozen completion prefix was accepted.");
    }

    private static void VerifyCraftCompletionStagingAndRestoreReplay()
    {
        var fact = new CombatEquipmentCraftCompletedFact(
            CreateCraftCompletionData());
        using var authority = new Authority(connect: true, recorderFaults: false);
        using IDisposable craftBinding = ConnectCraftObserver(authority);

        Require(authority.Dispatcher.TryStage(
                fact,
                out IDomainFactStage stage,
                out string stageFailure),
            "Combat craft fact staging failed: " + stageFailure);
        string publicationKey = stage.OperationKey;
        GameplayResultKey expectedKey = MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.CombatEquipmentCraftCompletionResult,
            fact.OwnerIdentity,
            1L);
        Require(stage.IsActive
            && !stage.IsReplay
            && stage.OwnerRevision == 37L
            && publicationKey == expectedKey.ToString(),
            "Combat craft stage did not preserve physical revision 37 and natural narrative revision 1.");

        DomainFactDelivery committed = stage.Commit();
        Require(committed.Completed
            && committed.OperationKey == publicationKey,
            "Combat craft staged publication did not commit exactly: "
                + committed.Detail);
        GameplayOutcomeLedgerSaveData first = authority.Ledger
            .CaptureGameplayOutcomes();
        Require(OutcomeRowCount(first) == 1
            && first.exactOutcomes.Count == 1
            && first.exactOutcomes[0].commitRevision == 1L
            && ResultKey(first.exactOutcomes[0]) == publicationKey
            && authority.Migrated.Capture().owners.Count == 1,
            "Combat craft stage did not create one natural revision-1 migrated outcome.");

        DomainFactDelivery replay = ReplayCraft(
            authority,
            publicationKey,
            fact);
        Require(replay.Completed,
            "Combat craft same-key replay did not complete: " + replay.Detail);
        Require(OutcomeRowCount(authority.Ledger.CaptureGameplayOutcomes()) == 1
            && authority.Migrated.Capture().owners.Count == 1,
            "Combat craft same-key replay allocated a duplicate.");

        var saved = new SavedCraftAuthority
        {
            serializer = HostSerializer,
            publicationKey = publicationKey,
            fact = fact.Snapshot(),
            ledger = authority.Ledger.CaptureGameplayOutcomes(),
            migrated = authority.Migrated.Capture()
        };
        string json = JsonConvert.SerializeObject(saved);
        SavedCraftAuthority exact = JsonConvert
            .DeserializeObject<SavedCraftAuthority>(json)
            ?? throw new InvalidOperationException(
                "Host JSON combat craft save round-trip returned null.");
        IReadOnlyDictionary<string, string> canonical =
            CanonicalHashes(exact.ledger);

        using (var restored = new Authority(connect: true, recorderFaults: false))
        {
            restored.Restore(exact.ledger, exact.migrated);
            using IDisposable restoredBinding = ConnectCraftObserver(restored);
            DomainFactDelivery exactReplay = ReplayCraft(
                restored,
                exact.publicationKey,
                new CombatEquipmentCraftCompletedFact(exact.fact));
            Require(exactReplay.Completed,
                "Fresh authority rejected exact combat craft replay: "
                    + exactReplay.Detail);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Fresh combat craft replay changed canonical identity/hash rows.");
            Require(restored.Migrated.Capture().owners.Count == 1,
                "Fresh combat craft replay changed migrated owner counters.");
        }

        SavedCraftAuthority tampered = JsonConvert
            .DeserializeObject<SavedCraftAuthority>(json)
            ?? throw new InvalidOperationException(
                "Host JSON combat craft tamper round-trip returned null.");
        tampered.fact.ownerRevision = 38L;
        using (var restored = new Authority(connect: true, recorderFaults: false))
        {
            restored.Restore(tampered.ledger, tampered.migrated);
            using IDisposable restoredBinding = ConnectCraftObserver(restored);
            var replies = new DomainFactDelivery[1];
            bool accepted = restored.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        tampered.publicationKey,
                        new CombatEquipmentCraftCompletedFact(tampered.fact))
                },
                replies,
                out string failure);
            Require(!accepted
                && replies[0].Phase == DomainFactDeliveryPhase.Rejected
                && !replies[0].MayBeCommitted
                && !string.IsNullOrWhiteSpace(failure),
                "Tampered combat craft physical owner revision was not canonically rejected.");
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Tampered combat craft replay mutated the canonical ledger.");
        }
    }

    private static void VerifyObserverConnectionBoundary()
    {
        using var authority = new Authority(connect: false, recorderFaults: false);
        CombatActionFact fact = CreateAction(
            "combat-host-connect",
            revision: 1L,
            day: 4,
            healthBefore: 100f,
            healthAfter: 95f,
            fatal: false,
            socialParent: false);
        CombatActionFact proposal = ProposalFor(fact);

        IDisposable first = authority.Observer.Connect(authority.Dispatcher);
        try
        {
            Require(
                authority.Dispatcher.SubscribedFactTypes.SequenceEqual(
                    new[] { typeof(CombatActionFact) }),
                "Combat observer subscription was not visible.");
            RequireThrows<InvalidOperationException>(
                () => authority.Observer.Connect(authority.Dispatcher),
                "A duplicate combat observer connection was accepted.");
        }
        finally
        {
            first.Dispose();
        }

        Require(
            !authority.Dispatcher.TryReserve(
                proposal,
                out _,
                out string missingFailure)
            && missingFailure.StartsWith(
                "domain-fact-observer-missing:",
                StringComparison.Ordinal),
            "Disconnected combat observer still accepted a reservation.");

        using IDisposable reconnected = authority.Observer.Connect(
            authority.Dispatcher);
        Require(
            authority.Dispatcher.TryReserve(
                proposal,
                out DomainFactLease lease,
                out string reconnectFailure),
            "Reconnected combat observer rejected a valid proposal: "
                + reconnectFailure);
        authority.Dispatcher.Cancel(lease);
        Require(!lease.IsValid && authority.Dispatcher.ActiveLeaseCount == 0,
            "Cancelled reconnect reservation remained active.");
    }

    private static SavedAuthority VerifyNormalDamageAndSameFactReplay()
    {
        using var authority = new Authority(connect: true, recorderFaults: false);
        CombatActionFact fact = CreateAction(
            "combat-host-normal",
            revision: 7L,
            day: 12,
            healthBefore: 100f,
            healthAfter: 90f,
            fatal: false,
            socialParent: false);
        CommitResult committed = Commit(authority, fact);
        Require(committed.Delivery.Completed,
            "Normal combat cohort did not reach delivered state: "
                + committed.Delivery.Detail);

        GameplayOutcomeLedgerSaveData before = authority.Ledger
            .CaptureGameplayOutcomes();
        Require(OutcomeRowCount(before) == 2,
            "Normal damage cohort did not create exactly attack and damage outcomes.");
        Require(before.exactOutcomes.Any(value =>
                value.producerId == ProductionCombatOutcomeIds.CombatProducerId)
            && before.exactOutcomes.Any(value =>
                value.producerId == MigratedProducerOutcomeCatalog
                    .Get(MigratedProducerOutcomeKind.CombatAttackResult)
                    .ProducerId),
            "Normal damage cohort identities were incomplete.");
        Require(authority.Migrated.Capture().owners.Count == 1,
            "Normal damage cohort did not freeze exactly one migrated owner revision.");

        IReadOnlyDictionary<string, string> canonicalBefore = CanonicalHashes(before);
        DomainFactDelivery replay = Replay(authority, committed.PublicationKey, fact);
        Require(replay.Completed,
            "Same-fact replay did not reconcile as delivered: " + replay.Detail);
        GameplayOutcomeLedgerSaveData after = authority.Ledger
            .CaptureGameplayOutcomes();
        RequireCanonicalEqual(
            canonicalBefore,
            CanonicalHashes(after),
            "Same-fact replay changed canonical outcomes.");
        Require(OutcomeRowCount(after) == 2
            && authority.Migrated.Capture().owners.Count == 1,
            "Same-fact replay allocated a duplicate outcome or owner revision.");

        var pending = new CombatActionPendingState();
        pending.Add(new CombatActionPendingData
        {
            publicationKey = committed.PublicationKey,
            fact = fact.Snapshot()
        });
        return new SavedAuthority
        {
            serializer = HostSerializer,
            pending = pending.Capture(),
            ledger = after,
            migrated = authority.Migrated.Capture()
        };
    }

    private static void VerifyFatalSocialParentCohort()
    {
        using var authority = new Authority(connect: true, recorderFaults: false);
        CombatActionFact fact = CreateAction(
            "combat-host-fatal-social",
            revision: 3L,
            day: 21,
            healthBefore: 10f,
            healthAfter: 0f,
            fatal: true,
            socialParent: true);
        CommitResult committed = Commit(authority, fact);
        Require(committed.Delivery.Completed,
            "Fatal social-parent cohort did not reach delivered state: "
                + committed.Delivery.Detail);

        GameplayOutcomeLedgerSaveData saved = authority.Ledger
            .CaptureGameplayOutcomes();
        Require(OutcomeRowCount(saved) == 6,
            "Fatal social-parent cohort must contain parent, damage, attack, downed, death, and killed outcomes.");
        GameplayOutcomeSnapshot parent = saved.exactOutcomes.SingleOrDefault(
            value => value.outcomeTypeId
                == SocialLifeOutcomeIds.ConflictResolved.Value);
        Require(parent != null,
            "Fatal social-parent cohort did not contain its social parent.");

        GameplayOutcomeSnapshot damage = saved.exactOutcomes.Single(value =>
            value.producerId == ProductionCombatOutcomeIds.CombatProducerId);
        GameplayOutcomeSnapshot attack = saved.exactOutcomes.Single(value =>
            value.producerId == MigratedProducerOutcomeCatalog
                .Get(MigratedProducerOutcomeKind.CombatAttackResult).ProducerId);
        GameplayOutcomeSnapshot[] transitions = saved.exactOutcomes
            .Where(value => !ReferenceEquals(value, parent)
                && !ReferenceEquals(value, damage) && !ReferenceEquals(value, attack)).ToArray();
        Require(new[] { damage, attack }.All(value =>
                value.parentRunId == parent.runId && value.parentSequence == parent.sequence)
            && transitions.Length == 3 && transitions.All(value =>
                value.parentRunId == damage.runId && value.parentSequence == damage.sequence),
            "Fatal combat child outcomes did not retain the exact social/damage causation chain.");
        Require(authority.Migrated.Capture().owners.Count == 4,
            "Fatal combat cohort did not retain attack/downed/death/killed owner revisions.");
    }

    private static void VerifyJsonRestoreExactReplayAndTamper(
        SavedAuthority source)
    {
        string json = JsonConvert.SerializeObject(source);
        Require(json.Contains(HostSerializer, StringComparison.Ordinal),
            "Host serializer label was not persisted.");

        SavedAuthority exact = JsonConvert.DeserializeObject<SavedAuthority>(json)
            ?? throw new InvalidOperationException(
                "Host JSON exact save round-trip returned null.");
        CombatActionPendingSaveData pending = CombatActionPendingState
            .Restore(exact.pending)
            .Capture();
        Require(pending.pending.Count == 1,
            "Host JSON did not preserve the pending combat fact.");

        IReadOnlyDictionary<string, string> expectedCanonical =
            CanonicalHashes(exact.ledger);
        using (var restored = new Authority(connect: true, recorderFaults: false))
        {
            restored.Restore(exact.ledger, exact.migrated);
            CombatActionPendingData item = pending.pending[0];
            DomainFactDelivery replay = Replay(
                restored,
                item.publicationKey,
                new CombatActionFact(item.fact));
            Require(replay.Completed,
                "Fresh authority did not reconcile the exact saved fact: "
                    + replay.Detail);
            RequireCanonicalEqual(
                expectedCanonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Fresh exact replay changed canonical identity/hash rows.");
            Require(restored.Migrated.Capture().owners.Count
                    == exact.migrated.owners.Count,
                "Fresh exact replay changed migrated owner counters.");
        }

        SavedAuthority tampered = JsonConvert.DeserializeObject<SavedAuthority>(json)
            ?? throw new InvalidOperationException(
                "Host JSON tamper save round-trip returned null.");
        tampered.pending.pending[0].fact.evidence.weaponDefinitionId = "weapon:tampered";
        using (var restored = new Authority(connect: true, recorderFaults: false))
        {
            restored.Restore(tampered.ledger, tampered.migrated);
            CombatActionPendingData item = tampered.pending.pending[0];
            var replies = new DomainFactDelivery[1];
            bool accepted = restored.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        item.publicationKey,
                        new CombatActionFact(item.fact))
                },
                replies,
                out string failure);
            Require(!accepted
                && replies[0].Phase == DomainFactDeliveryPhase.Rejected
                && !replies[0].MayBeCommitted
                && !string.IsNullOrWhiteSpace(failure),
                "Tampered saved combat fact was not rejected canonically.");
            RequireCanonicalEqual(
                expectedCanonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Tampered replay mutated the restored canonical ledger.");
        }
    }

    private static void VerifyKnownReservationAndPrepareRejections()
    {
        using var authority = new Authority(connect: true, recorderFaults: false);
        CombatActionFact actual = CreateAction(
            "combat-host-reject",
            revision: 5L,
            day: 8,
            healthBefore: 80f,
            healthAfter: 75f,
            fatal: false,
            socialParent: false);
        Require(!authority.Dispatcher.TryReserve(
                actual,
                out _,
                out string proposalFailure)
            && proposalFailure == "combat-observer-proposal-required",
            "Actual fact was accepted as a reservation proposal.");

        CombatActionFact proposal = ProposalFor(actual);
        Require(authority.Dispatcher.TryReserve(
                proposal,
                out DomainFactLease lease,
                out string reserveFailure),
            "Valid rejection fixture proposal failed to reserve: "
                + reserveFailure);
        CombatActionFact drifted = CreateAction(
            actual.OperationId,
            actual.AttackRevision,
            day: 9,
            healthBefore: 80f,
            healthAfter: 75f,
            fatal: false,
            socialParent: false);
        DomainFactDelivery rejected = authority.Dispatcher.Publish(lease, drifted);
        GameplayOutcomeLedgerDiagnostics diagnostics = authority.Ledger
            .GetDiagnostics();
        Require(rejected.Phase == DomainFactDeliveryPhase.Rejected
            && !lease.IsValid
            && authority.Dispatcher.ActiveLeaseCount == 0
            && diagnostics.ReservedCount == 0
            && diagnostics.PendingDeliveryCount == 0
            && diagnostics.PublishedCount == 0
            && diagnostics.KnownResultKeyCount == 0
            && authority.Migrated.Capture().owners.Count == 0,
            "Known prepare rejection left committed or reserved combat state.");
    }

    private static void VerifyAtomicCommitAmbiguityBoundaries()
    {
        using (var before = new Authority(connect: true, recorderFaults: true))
        {
            before.Faults.ThrowBeforeNextBatchCommit = true;
            CombatActionFact fact = CreateAction(
                "combat-host-before-commit",
                revision: 1L,
                day: 15,
                healthBefore: 60f,
                healthAfter: 55f,
                fatal: false,
                socialParent: false);
            CommitResult attempt = Commit(before, fact, requireCommitted: false);
            Require(attempt.Delivery.Phase == DomainFactDeliveryPhase.InDoubt
                && attempt.Delivery.MayBeCommitted
                && before.Faults.BeforeCommitFaultCount == 1
                && before.Ledger.GetOutboxSnapshot().Count == 0
                && before.Ledger.GetDiagnostics().PublishedCount == 0,
                "Pre-commit recorder exception was not exposed as uncommitted ambiguity.");
            Require(Replay(before, attempt.PublicationKey, fact).Completed
                && OutcomeRowCount(before.Ledger.CaptureGameplayOutcomes()) == 2,
                "Pre-commit ambiguity could not replay the frozen fact exactly once.");
        }

        using (var after = new Authority(connect: true, recorderFaults: true))
        {
            after.Faults.ThrowAfterNextBatchCommit = true;
            CombatActionFact fact = CreateAction(
                "combat-host-after-commit",
                revision: 2L,
                day: 16,
                healthBefore: 60f,
                healthAfter: 50f,
                fatal: false,
                socialParent: false);
            CommitResult attempt = Commit(after, fact);
            Require(attempt.Delivery.MayBeCommitted
                && after.Faults.AfterCommitFaultCount == 1,
                "Post-commit recorder exception was reported as a rejection.");
            DomainFactDelivery recovered = Replay(
                after,
                attempt.PublicationKey,
                fact);
            Require(recovered.Completed,
                "Post-commit ambiguity did not recover through canonical replay: "
                    + recovered.Detail);
            Require(OutcomeRowCount(after.Ledger.CaptureGameplayOutcomes()) == 2,
                "Post-commit recovery duplicated or lost the combat cohort.");
        }
    }

    private static void VerifyOffenseBattleCommandPublicationRuntime()
    {
        VerifyBattleCommandNoSkillJournalAndFollowups();
        VerifyBattleCommandPostCommandEquipmentEvidence();
        VerifyBattleCommandJointSkillCohortAndTamper();
        VerifyBattleCommandExplicitRejection();
        VerifyBattleCommandAmbiguousRestoreReplay(afterCommit: false);
        VerifyBattleCommandAmbiguousRestoreReplay(afterCommit: true);
    }

    private static void VerifyBattleCommandNoSkillJournalAndFollowups()
    {
        using var authority = new BattleCommandAuthority(recorderFaults: false);
        OffenseBattleCommandFact proposal = CreateBattleCommandProposal(
            "no-skill",
            commandId: 1L,
            actionType: OffenseBattleActionType.Guard);
        Require(authority.Runtime.TryBegin(proposal, out string failure),
            "Battle command proposal was not reserved: " + failure);
        string publicationKey = authority.Runtime.ReservedPublicationKey;
        Require(!string.IsNullOrWhiteSpace(publicationKey),
            "Battle command proposal did not expose its reserved publication key.");
        RequireThrows<InvalidOperationException>(
            () => authority.Runtime.PublishApplied(proposal, null, default),
            "Battle command runtime published an unresolved proposal.");

        OffenseBattleCommandFact applied = ResolveBattleCommand(proposal);
        DomainFactDelivery delivered = authority.Runtime.PublishApplied(
            applied,
            null,
            default);
        authority.Runtime.End();
        Require(delivered.Completed,
            "Resolved battle command did not publish exactly once: "
                + delivered.Detail);
        OffenseBattleCommandPublicationSaveData saved = authority.Runtime.Capture();
        Require(saved.observations.Count == 1
            && !saved.observations[0].pending
            && !saved.observations[0].hasSkill
            && saved.observations[0].publicationKey == publicationKey
            && saved.observations[0].followupStage == 0
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 1,
            "No-skill battle command journal was not canonical and singular.");

        saved.observations[0].fact.actorDisplayName = "detached mutation";
        saved.observations[0].skill.recipients.Add(
            new CharacterSkillActivationRecipientFactData
            {
                characterId = "character:detached",
                displayName = "detached",
                beforeState = "before",
                afterState = "after"
            });
        OffenseBattleCommandPublicationSaveData detached = authority.Runtime.Capture();
        Require(detached.observations[0].fact.actorDisplayName
                != "detached mutation"
            && detached.observations[0].skill.recipients.Count == 0,
            "Battle command capture retained aliases to journal state.");
        OffenseBattleCommandPublicationSaveData validNoSkill =
            OffenseBattleCommandPublicationRuntime.ValidateAndClone(detached);
        Require(validNoSkill.observations.Count == 1
            && !validNoSkill.observations[0].hasSkill
            && validNoSkill.observations[0].skill.triggerSerial == -1,
            "Default no-skill battle command row was not accepted exactly.");

        OffenseBattleCommandPublicationSaveData invalidNoSkill =
            JsonConvert.DeserializeObject<OffenseBattleCommandPublicationSaveData>(
                JsonConvert.SerializeObject(detached));
        invalidNoSkill.observations[0].skill.triggerSerial = 0;
        RequireThrows<ArgumentException>(
            () => OffenseBattleCommandPublicationRuntime.ValidateAndClone(
                invalidNoSkill),
            "No-skill battle command row accepted non-default skill evidence.");

        RequireThrows<InvalidOperationException>(
            () => authority.Runtime.AdvanceFollowup(
                detached.observations[0].fact.battleId,
                1L,
                1),
            "Battle command followups accepted an out-of-order stage.");
        for (int stage = 0;
             stage < OffenseBattleCommandPublicationRuntime.FollowupsComplete;
             stage++)
        {
            authority.Runtime.AdvanceFollowup(
                detached.observations[0].fact.battleId,
                1L,
                stage);
            Require(authority.Runtime.Capture().observations[0].followupStage
                    == stage + 1,
                "Battle command followup stage did not advance in order: "
                    + stage + ".");
        }
        RequireThrows<InvalidOperationException>(
            () => authority.Runtime.AdvanceFollowup(
                detached.observations[0].fact.battleId,
                1L,
                OffenseBattleCommandPublicationRuntime.FollowupsComplete),
            "Completed battle command accepted an extra followup stage.");

        OffenseBattleCommandFact next = CreateBattleCommandProposal(
            "no-skill",
            commandId: 2L,
            actionType: OffenseBattleActionType.Guard);
        Require(authority.Runtime.TryBegin(next, out failure),
            "Completed battle command did not allow its next native revision: "
                + failure);
        string expected = MigratedProducerOutcomeCatalog.ResultKey(
            MigratedProducerOutcomeKind.OffenseBattleCommandResult,
            "offense-battle:" + next.Snapshot().battleId,
            2L).ToString();
        Require(authority.Runtime.ReservedPublicationKey == expected,
            "Next battle command did not reserve native revision two.");
        authority.Runtime.End();
    }

    private static void VerifyBattleCommandJointSkillCohortAndTamper()
    {
        using var authority = new BattleCommandAuthority(recorderFaults: false);
        OffenseBattleCommandFact proposal = CreateBattleCommandProposal(
            "joint",
            commandId: 1L,
            actionType: OffenseBattleActionType.Ability);
        Require(authority.Runtime.TryBegin(proposal, out string failure),
            "Joint battle command proposal was not reserved: " + failure);
        string commandKey = authority.Runtime.ReservedPublicationKey;
        CharacterSkillActivationFact skill = CreateBattleCommandSkill(
            proposal,
            commandKey,
            ownerRevision: 1L);
        Require(authority.Dispatcher.TryReserve(
                skill,
                out DomainFactLease skillLease,
                out failure),
            "Joint battle skill was not reserved: " + failure);
        string skillKey = skillLease.PublicationKey;
        DomainFactDelivery delivered = authority.Runtime.PublishApplied(
            ResolveBattleCommand(proposal),
            skill,
            skillLease);
        authority.Runtime.End();
        Require(delivered.Completed,
            "Joint command/skill cohort did not publish atomically: "
                + delivered.Detail);

        OffenseBattleCommandPublicationSaveData journal = authority.Runtime.Capture();
        OffenseBattleCommandObservationData row = journal.observations.Single();
        Require(!row.pending && row.hasSkill
            && row.publicationKey == commandKey
            && row.skillPublicationKey == skillKey
            && row.fact.targetIsDead
            && row.skill.childPublicationKeys.Count == 1
            && row.skill.childPublicationKeys[0] == commandKey
            && row.skill.triggerSerial
                == CharacterGrowthRules.StableNonNegativeHash(
                    row.fact.battleId)
            && row.skill.absoluteHour == row.fact.absoluteHour
            && !string.IsNullOrWhiteSpace(row.skillPublicationId)
            && row.skillFingerprint?.Length == 64,
            "Joint command/skill journal lost its shared cohort identity.");
        GameplayOutcomeLedgerSaveData ledger =
            authority.Ledger.CaptureGameplayOutcomes();
        DungeonMigratedProducerOutcomeSaveData migrated =
            authority.Migrated.Capture();
        RequireBattleCommandCanonicalPair(ledger,
            "Joint command/skill cohort did not write one canonical row each.");
        Require(ValidateBattleCommandPreflight(journal, ledger, migrated).Success,
            "Canonical joint command/skill preflight was rejected.");

        IReadOnlyDictionary<string, string> canonical = CanonicalHashes(ledger);
        OffenseBattleCommandFactData alteredData = row.fact.Clone();
        alteredData.actorDisplayName += " 변조";
        var replay = new DomainFactDelivery[1];
        Require(!authority.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        row.publicationKey,
                        new OffenseBattleCommandFact(alteredData))
                },
                replay,
                out failure)
            && replay[0].Phase == DomainFactDeliveryPhase.Rejected
            && !string.IsNullOrWhiteSpace(failure),
            "Battle command replay accepted a tampered frozen fact.");
        RequireCanonicalEqual(
            canonical,
            CanonicalHashes(authority.Ledger.CaptureGameplayOutcomes()),
            "Rejected battle command replay changed canonical outcomes.");

        OffenseBattleCommandPublicationSaveData tamperedJournal =
            JsonConvert.DeserializeObject<OffenseBattleCommandPublicationSaveData>(
                JsonConvert.SerializeObject(journal));
        tamperedJournal.observations[0].fact.actorDisplayName += " 저장 변조";
        Require(!ValidateBattleCommandPreflight(
                tamperedJournal,
                ledger,
                migrated).Success,
            "Battle command preflight accepted a tampered journal fact.");

        GameplayOutcomeLedgerSaveData partial =
            JsonConvert.DeserializeObject<GameplayOutcomeLedgerSaveData>(
                JsonConvert.SerializeObject(ledger));
        RemoveCanonicalProducer(
            partial,
            CharacterSkillActivationPersistenceContract.ProducerId);
        Require(!ValidateBattleCommandPreflight(
                journal,
                partial,
                migrated).Success,
            "Battle command preflight accepted a partial command/skill cohort.");
    }

    private static void VerifyBattleCommandPostCommandEquipmentEvidence()
    {
        const string oldWeaponInstance =
            "weapon-instance:command-host:switch-old";
        const string oldWeaponDefinition =
            "weapon:command-host:switch-old";
        const string newWeaponInstance =
            "weapon-instance:command-host:switch-new";
        const string newWeaponDefinition =
            "weapon:command-host:switch-new";
        using var authority = new BattleCommandAuthority(recorderFaults: false);
        OffenseBattleCommandFact proposal = CreateBattleCommandProposal(
            "switch-weapon",
            commandId: 1L,
            actionType: OffenseBattleActionType.SwitchWeapon);
        OffenseBattleCommandFactData proposalData = proposal.Snapshot();
        proposalData.weaponInstanceId = oldWeaponInstance;
        proposalData.weaponDefinitionId = oldWeaponDefinition;
        proposal = new OffenseBattleCommandFact(proposalData);
        Require(authority.Runtime.TryBegin(proposal, out string failure),
            "Switch-weapon battle command proposal was not reserved: "
                + failure);
        string publicationKey = authority.Runtime.ReservedPublicationKey;

        OffenseBattleCommandFactData wrongActor =
            ResolveBattleCommand(proposal).Snapshot();
        wrongActor.actorId = "character:command-host:switch-wrong-actor";
        RequireThrows<InvalidOperationException>(
            () => authority.Runtime.PublishApplied(
                new OffenseBattleCommandFact(wrongActor),
                null,
                default),
            "Switch-weapon command accepted a changed proposal actor.");

        OffenseBattleCommandFactData appliedData =
            ResolveBattleCommand(proposal).Snapshot();
        appliedData.weaponInstanceId = newWeaponInstance;
        appliedData.weaponDefinitionId = newWeaponDefinition;
        var applied = new OffenseBattleCommandFact(appliedData);
        DomainFactDelivery delivered = authority.Runtime.PublishApplied(
            applied,
            null,
            default);
        authority.Runtime.End();
        Require(delivered.Completed,
            "Switch-weapon command did not accept post-command equipment: "
                + delivered.Detail);

        OffenseBattleCommandObservationData row =
            authority.Runtime.Capture().observations.Single();
        GameplayOutcomeLedgerSaveData ledger =
            authority.Ledger.CaptureGameplayOutcomes();
        string commandProducer = MigratedProducerOutcomeCatalog.Get(
            MigratedProducerOutcomeKind.OffenseBattleCommandResult).ProducerId;
        GameplayOutcomeSnapshot canonical = ledger.outbox
            .Concat(ledger.exactOutcomes)
            .Single(value => value.producerId == commandProducer);
        Require(row.publicationKey == publicationKey
            && row.fact.weaponInstanceId == newWeaponInstance
            && row.fact.weaponDefinitionId == newWeaponDefinition
            && canonical.subjects.Any(value => value != null
                && value.entityKindId
                    == MigratedProducerOutcomeIds.ItemKind.Value
                && value.entityId == newWeaponInstance)
            && !canonical.subjects.Any(value => value != null
                && value.entityId == oldWeaponInstance),
            "Switch-weapon canonical evidence did not reflect the applied weapon.");

        IReadOnlyDictionary<string, string> canonicalHashes =
            CanonicalHashes(ledger);
        OffenseBattleCommandFactData alteredWeapon = applied.Snapshot();
        alteredWeapon.weaponInstanceId =
            "weapon-instance:command-host:switch-altered";
        alteredWeapon.weaponDefinitionId =
            "weapon:command-host:switch-altered";
        var replay = new DomainFactDelivery[1];
        Require(!authority.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        publicationKey,
                        new OffenseBattleCommandFact(alteredWeapon))
                },
                replay,
                out failure)
            && replay[0].Phase == DomainFactDeliveryPhase.Rejected
            && !string.IsNullOrWhiteSpace(failure),
            "Switch-weapon replay accepted altered applied equipment.");
        RequireCanonicalEqual(
            canonicalHashes,
            CanonicalHashes(authority.Ledger.CaptureGameplayOutcomes()),
            "Rejected switch-weapon replay changed canonical evidence.");
    }

    private static void VerifyBattleCommandExplicitRejection()
    {
        using var authority = new BattleCommandAuthority(recorderFaults: false);
        OffenseBattleCommandFact proposal = CreateBattleCommandProposal(
            "rejected",
            commandId: 1L,
            actionType: OffenseBattleActionType.Guard);
        Require(authority.Runtime.TryBegin(proposal, out string failure),
            "Rejected battle command fixture did not reserve: " + failure);
        authority.Port.RejectNextBatch = true;
        DomainFactDelivery rejected = authority.Runtime.PublishApplied(
            ResolveBattleCommand(proposal),
            null,
            default);
        authority.Runtime.End();
        Require(rejected.Phase == DomainFactDeliveryPhase.Rejected
            && authority.Runtime.Capture().observations.Count == 0
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 0
            && authority.Migrated.Capture().owners.Count == 0,
            "Explicitly rejected battle command retained a journal or authority row.");
    }

    private static void VerifyBattleCommandAmbiguousRestoreReplay(
        bool afterCommit)
    {
        string edge = afterCommit ? "after" : "before";
        SavedBattleCommandAuthority saved;
        using (var authority = new BattleCommandAuthority(recorderFaults: true))
        {
            OffenseBattleCommandFact proposal = CreateBattleCommandProposal(
                "ambiguous-" + edge,
                commandId: 1L,
                actionType: OffenseBattleActionType.Ability);
            Require(authority.Runtime.TryBegin(proposal, out string failure),
                "Ambiguous battle command fixture did not reserve: " + failure);
            string commandKey = authority.Runtime.ReservedPublicationKey;
            CharacterSkillActivationFact skill = CreateBattleCommandSkill(
                proposal,
                commandKey,
                ownerRevision: 1L);
            Require(authority.Dispatcher.TryReserve(
                    skill,
                    out DomainFactLease skillLease,
                    out failure),
                "Ambiguous battle skill fixture did not reserve: " + failure);
            if (afterCommit)
            {
                authority.Faults.ThrowAfterNextBatchCommit = true;
                authority.Port.HideCompletedBatchReplies = true;
            }
            else
            {
                authority.Faults.ThrowBeforeNextBatchCommit = true;
            }

            DomainFactDelivery attempt = authority.Runtime.PublishApplied(
                ResolveBattleCommand(proposal),
                skill,
                skillLease);
            authority.Runtime.End();
            Require(attempt.MayBeCommitted && !attempt.Completed
                && (afterCommit
                    ? authority.Faults.AfterCommitFaultCount == 1
                        && authority.Port.HiddenBatchReplyCount == 1
                    : authority.Faults.BeforeCommitFaultCount == 1),
                "Battle command " + edge
                    + "-commit ambiguity did not remain pending.");
            Require(authority.Runtime.Capture().observations.Single().pending,
                "Battle command " + edge
                    + "-commit ambiguity cleared its frozen journal row.");
            Require(!authority.Runtime.TryBegin(
                    CreateBattleCommandProposal(
                        "blocked-" + edge,
                        commandId: 2L,
                        actionType: OffenseBattleActionType.Guard),
                    out _),
                "Pending battle command allowed another reservation.");

            saved = JsonConvert.DeserializeObject<SavedBattleCommandAuthority>(
                JsonConvert.SerializeObject(new SavedBattleCommandAuthority
                {
                    serializer = HostSerializer,
                    publications = authority.Runtime.Capture(),
                    ledger = authority.Ledger.CaptureGameplayOutcomes(),
                    migrated = authority.Migrated.Capture()
                }));
        }

        Require(saved != null && saved.serializer == HostSerializer,
            "Battle command ambiguity did not survive the host JSON round-trip.");
        using var restored = new BattleCommandAuthority(recorderFaults: false);
        restored.Restore(saved.ledger, saved.migrated, saved.publications);
        Require(restored.Runtime.RecoverPending(out string recoveryFailure),
            "Battle command " + edge
                + "-commit ReplayBatch recovery failed: " + recoveryFailure);
        OffenseBattleCommandPublicationSaveData recovered =
            restored.Runtime.Capture();
        Require(recovered.observations.Count == 1
            && !recovered.observations[0].pending,
            "Battle command " + edge
                + "-commit recovery did not settle exactly one journal row.");
        GameplayOutcomeLedgerSaveData ledger =
            restored.Ledger.CaptureGameplayOutcomes();
        RequireBattleCommandCanonicalPair(
            ledger,
            "Battle command " + edge
                + "-commit recovery duplicated or lost its canonical cohort.");
        Require(ValidateBattleCommandPreflight(
                recovered,
                ledger,
                restored.Migrated.Capture()).Success,
            "Recovered battle command " + edge
                + "-commit cohort failed canonical preflight.");
    }

    private static OffenseBattleCommandFact CreateBattleCommandProposal(
        string suffix,
        long commandId,
        OffenseBattleActionType actionType)
    {
        bool ability = actionType == OffenseBattleActionType.Ability;
        return new OffenseBattleCommandFact(new OffenseBattleCommandFactData
        {
            battleId = "battle:command-host:" + suffix,
            expeditionId = "expedition:command-host:" + suffix,
            actorId = "character:command-host:" + suffix,
            actorDisplayName = "  호스트 전투원  ",
            targetId = "enemy:command-host:" + suffix,
            targetDisplayName = "  호스트 표적  ",
            abilityId = ability ? "skill:command-host:" + suffix : string.Empty,
            weaponInstanceId = "weapon-instance:command-host:" + suffix,
            weaponDefinitionId = "weapon:command-host:" + suffix,
            commandId = commandId,
            absoluteDay = 17,
            absoluteHour = 408L,
            actionType = actionType,
            resolved = false,
            targetWasDead = false,
            targetIsDead = false,
            hit = false,
            shieldBlocked = false,
            coverBlocked = false,
            targetHealthBefore = 100f,
            targetHealthAfter = 100f,
            amount = 0f
        });
    }

    private static OffenseBattleCommandFact ResolveBattleCommand(
        OffenseBattleCommandFact proposal)
    {
        OffenseBattleCommandFactData data = proposal.Snapshot();
        data.resolved = true;
        if (data.actionType == OffenseBattleActionType.Ability)
        {
            data.targetHealthAfter = 0f;
            data.amount = 100f;
            data.targetIsDead = true;
            data.hit = true;
        }
        return new OffenseBattleCommandFact(data);
    }

    private static CharacterSkillActivationFact CreateBattleCommandSkill(
        OffenseBattleCommandFact command,
        string commandPublicationKey,
        long ownerRevision)
    {
        OffenseBattleCommandFactData data = command.Snapshot();
        return new CharacterSkillActivationFact(
            new CharacterSkillActivationFactData
            {
                sourceCharacterId = data.actorId,
                sourceDisplayName = data.actorDisplayName,
                ownerRevision = ownerRevision,
                activationKind =
                    CharacterSkillActivationKind.ManualCombatUltimate,
                skillId = data.abilityId,
                skillDisplayName = "  호스트 전투 궁극기  ",
                trigger = CharacterSkillTrigger.ManualCombat,
                target = CharacterSkillTarget.Enemy,
                targetingMode = CharacterSkillTargetingMode.PlayerSelected,
                effectArea = CharacterSkillEffectArea.Single,
                areaSize = 1,
                durationHours = 0,
                cooldownDays = 0,
                absoluteDay = data.absoluteDay,
                absoluteHour = data.absoluteHour,
                expiresAbsoluteHour = data.absoluteHour,
                useCountBefore = 0,
                useCountAfter = 1,
                readyAbsoluteHourBefore = 0L,
                readyAbsoluteHourAfter = 0L,
                mechanicsSha256 = new string('a', 64),
                recipients = new List<CharacterSkillActivationRecipientFactData>
                {
                    new()
                    {
                        characterId = data.targetId,
                        displayName = data.targetDisplayName,
                        beforeState = "health=100;dead=false",
                        afterState = "health=0;dead=true"
                    }
                },
                childPublicationKeys = new List<string>
                {
                    commandPublicationKey
                },
                triggerEventId = "battle:" + data.battleId
                    + ":command:" + data.commandId + ":offense-ultimate",
                triggerEventKeySha256 = new string('b', 64),
                ultimateDomain = CharacterUltimateDomain.Offense,
                triggerSerial = CharacterGrowthRules.StableNonNegativeHash(
                    data.battleId)
            });
    }

    private static DungeonGameRestoreReport ValidateBattleCommandPreflight(
        OffenseBattleCommandPublicationSaveData publications,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonMigratedProducerOutcomeSaveData migrated)
    {
        MethodInfo method = typeof(OffenseBattleCommandFactPreflight)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(value => value.Name == "Validate"
                && value.GetParameters().Length == 4
                && value.GetParameters()[0].ParameterType
                    == typeof(OffenseBattleCommandPublicationSaveData));
        var report = new DungeonGameRestoreReport();
        method.Invoke(null, new object[]
        {
            publications,
            ledger,
            migrated,
            report
        });
        return report;
    }

    private static DungeonGameRestoreReport ValidateSkillPublicationCanonical(
        CharacterSkillActivationPublicationSaveData publications,
        GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo method = typeof(CharacterSkillActivationFactPreflight)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(value => value.Name == "ValidateCanonical"
                && value.GetParameters().Length == 4
                && value.GetParameters()[0].ParameterType
                    == typeof(CharacterSkillActivationPublicationSaveData)
                && value.GetParameters()[1].ParameterType
                    == typeof(GameplayOutcomeLedgerSaveData)
                && value.GetParameters()[2].ParameterType
                    == typeof(OffenseBattleCommandPublicationSaveData)
                && value.GetParameters()[3].ParameterType
                    == typeof(DungeonGameRestoreReport));
        var report = new DungeonGameRestoreReport();
        method.Invoke(null, new object[]
        {
            publications,
            ledger,
            new OffenseBattleCommandPublicationSaveData(),
            report
        });
        return report;
    }

    private static DungeonGameRestoreReport
        ValidateDiseaseAnatomyBurdenCanonical(
            PopulationHealthWorldSaveData population,
            GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo method = typeof(DiseaseAnatomyBurdenFactPreflight)
            .GetMethod(
                "Validate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(PopulationHealthWorldSaveData),
                    typeof(GameplayOutcomeLedgerSaveData),
                    typeof(DungeonGameRestoreReport)
                },
                modifiers: null)
            ?? throw new MissingMethodException(
                typeof(DiseaseAnatomyBurdenFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        method.Invoke(null, new object[] { population, ledger, report });
        return report;
    }

    private static DungeonGameRestoreReport ValidateCharacterLifeCanonical(
        CharacterLifeWorldSaveData life,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonMigratedProducerOutcomeSaveData migrated)
    {
        MethodInfo method = typeof(CharacterLifeFactPreflight).GetMethod(
            "Validate",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(CharacterLifeWorldSaveData),
                typeof(GameplayOutcomeLedgerSaveData),
                typeof(DungeonMigratedProducerOutcomeSaveData),
                typeof(DungeonGameRestoreReport)
            },
            modifiers: null)
            ?? throw new MissingMethodException(
                typeof(CharacterLifeFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        method.Invoke(null, new object[]
        {
            life,
            ledger,
            migrated,
            report
        });
        return report;
    }

    private static DungeonGameRestoreReport ValidateCharacterRestCanonical(
        DungeonCharacterWorldSaveData characters,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonMigratedProducerOutcomeSaveData migrated)
    {
        MethodInfo method = typeof(CharacterRestFactPreflight).GetMethod(
            "Validate",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(DungeonCharacterWorldSaveData),
                typeof(GameplayOutcomeLedgerSaveData),
                typeof(DungeonMigratedProducerOutcomeSaveData),
                typeof(DungeonGameRestoreReport)
            },
            modifiers: null)
            ?? throw new MissingMethodException(
                typeof(CharacterRestFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        method.Invoke(null, new object[]
        {
            characters,
            ledger,
            migrated,
            report
        });
        return report;
    }

    private static DungeonCharacterWorldSaveData CloneCharacterRestWorld(
        DungeonCharacterWorldSaveData source) =>
        JsonConvert.DeserializeObject<DungeonCharacterWorldSaveData>(
            JsonConvert.SerializeObject(source))
        ?? throw new InvalidOperationException(
            "Character rest host JSON clone returned null.");

    private static CharacterLifeWorldSaveData CloneCharacterLifeWorld(
        CharacterLifeWorldSaveData source) =>
        JsonConvert.DeserializeObject<CharacterLifeWorldSaveData>(
            JsonConvert.SerializeObject(source))
        ?? throw new InvalidOperationException(
            "Character life host JSON clone returned null.");

    private static void RequireBattleCommandCanonicalPair(
        GameplayOutcomeLedgerSaveData ledger,
        string message)
    {
        string commandProducer = MigratedProducerOutcomeCatalog.Get(
            MigratedProducerOutcomeKind.OffenseBattleCommandResult).ProducerId;
        Require(OutcomeRowCount(ledger) == 2
            && CanonicalProducerCount(ledger, commandProducer) == 1
            && CanonicalProducerCount(
                ledger,
                CharacterSkillActivationPersistenceContract.ProducerId) == 1,
            message);
    }

    private static int CanonicalProducerCount(
        GameplayOutcomeLedgerSaveData ledger,
        string producerId) =>
        ledger.outbox.Count(value => value.producerId == producerId)
        + ledger.exactOutcomes.Count(value => value.producerId == producerId)
        + ledger.tombstones.Count(value => value.producerId == producerId);

    private static void RemoveCanonicalProducer(
        GameplayOutcomeLedgerSaveData ledger,
        string producerId)
    {
        ledger.outbox.RemoveAll(value => value.producerId == producerId);
        ledger.exactOutcomes.RemoveAll(value => value.producerId == producerId);
        ledger.tombstones.RemoveAll(value => value.producerId == producerId);
    }

    private static void VerifyDiseaseAnatomyBurdenFactObserver()
    {
        DiseaseAnatomyBurdenProjectionSaveData source =
            CreateDiseaseAnatomyBurdenProjection("frozen");
        var fact = new DiseaseAnatomyBurdenAppliedFact(source);
        DiseaseAnatomyBurdenOutcomeReceipt receipt =
            DiseaseAnatomyBurdenFactMapping.Capture(fact);
        source.operationId = "disease-body-burden:mutated";
        source.characterDisplayName = "변조된 환자";
        source.infectionAfter = 99f;
        source.publicationKey = "mutated-publication-key";
        Require(fact.OperationId == "disease-body-burden:host:frozen"
            && fact.CharacterDisplayName == "호스트 질병 환자"
            && fact.InfectionAfter == 16.25f
            && receipt.OperationId.Value == fact.OperationId
            && receipt.CharacterId == fact.CharacterId
            && receipt.CharacterDisplayName.DisplayText
                == fact.CharacterDisplayName
            && receipt.DiseaseId == fact.DiseaseId
            && receipt.TargetSystem == fact.TargetSystem
            && receipt.NodeId == fact.NodeId
            && receipt.Severity == fact.Severity
            && receipt.RequestedBurden == fact.RequestedBurden
            && receipt.InfectionBefore == fact.InfectionBefore
            && receipt.InfectionAfter == fact.InfectionAfter
            && receipt.AbsoluteDay == fact.AbsoluteDay,
            "Disease anatomy burden fact or receipt retained mutable projection state.");

        DiseaseAnatomyBurdenProjectionSaveData invalid =
            CreateDiseaseAnatomyBurdenProjection("invalid");
        invalid.requestedInfectionBurden = 0f;
        RequireThrows<ArgumentException>(
            () => new DiseaseAnatomyBurdenAppliedFact(invalid),
            "Disease anatomy burden fact accepted an invalid applied value.");

        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new DiseaseAnatomyBurdenOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new DiseaseAnatomyBurdenOutcomeAdapter()
            });
        var ledger = new GameplayOutcomeLedger(
            registry,
            GameplayOutcomeBufferLimits.Default,
            new GameplayOutcomeRunId(
                "run:disease-anatomy-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture)),
            1L);
        var recorder = new GameplayOutcomeRecorder(
            ledger,
            registry,
            new GameEventBus());
        var commitGroup = new GameplayDomainFactCommitGroup(
            recorder,
            ledger);
        using var dispatcher = new DomainFactDispatcher();
        GameplayDomainFactObserverBinding<
            DiseaseAnatomyBurdenAppliedFact,
            DiseaseAnatomyBurdenOutcomeReceipt> binding =
            DiseaseAnatomyBurdenFactMapping.CreateBinding(
                registry,
                ledger,
                recorder,
                commitGroup);
        Require(!dispatcher.TryReserve(
                fact,
                out _,
                out string missingFailure)
            && missingFailure.StartsWith(
                "domain-fact-observer-missing:",
                StringComparison.Ordinal),
            "Disease anatomy burden fact published without its binding.");

        IDisposable connection = binding.Connect(dispatcher);
        RequireThrows<InvalidOperationException>(
            () => binding.Connect(dispatcher),
            "Disease anatomy burden binding accepted a duplicate observer.");
        Require(dispatcher.TryReserve(
                fact,
                out DomainFactLease lease,
                out string failure),
            "Disease anatomy burden fact did not reserve: " + failure);
        string publicationKey = lease.PublicationKey;
        DomainFactDelivery delivered = dispatcher.Publish(lease, fact);
        Require(delivered.Completed
            && publicationKey == receipt.ResultKey.ToString()
            && OutcomeRowCount(ledger.CaptureGameplayOutcomes()) == 1,
            "Disease anatomy burden fact did not publish exactly once.");

        IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
            ledger.CaptureGameplayOutcomes());
        var observedProjection =
            CreateDiseaseAnatomyBurdenProjection("frozen");
        observedProjection.publicationKey = receipt.ResultKey.ToString();
        var population = new PopulationHealthWorldSaveData();
        population.pendingAnatomyBurdenProjections.Add(observedProjection);
        GameplayOutcomeLedgerSaveData canonicalLedger =
            ledger.CaptureGameplayOutcomes();
        Require(ValidateDiseaseAnatomyBurdenCanonical(
                population,
                canonicalLedger).Success,
            "Disease anatomy burden preflight rejected its matching pending observation.");

        var renamedProjection =
            CreateDiseaseAnatomyBurdenProjection("frozen");
        renamedProjection.publicationKey = receipt.ResultKey.ToString();
        renamedProjection.characterDisplayName += " 변조";
        var renamedPopulation = new PopulationHealthWorldSaveData();
        renamedPopulation.pendingAnatomyBurdenProjections.Add(
            renamedProjection);
        Require(!ValidateDiseaseAnatomyBurdenCanonical(
                renamedPopulation,
                canonicalLedger).Success,
            "Disease anatomy burden preflight accepted a changed frozen name.");

        var resetProjection =
            CreateDiseaseAnatomyBurdenProjection("frozen");
        resetProjection.phase =
            (int)DiseaseAnatomyBurdenProjectionPhase.Pending;
        resetProjection.publicationKey = string.Empty;
        resetProjection.characterDisplayName = string.Empty;
        var resetPopulation = new PopulationHealthWorldSaveData();
        resetPopulation.pendingAnatomyBurdenProjections.Add(resetProjection);
        Require(!ValidateDiseaseAnatomyBurdenCanonical(
                resetPopulation,
                canonicalLedger).Success,
            "Disease anatomy burden preflight accepted a pending phase with canonical evidence.");

        var replayed = new DomainFactDelivery[1];
        Require(dispatcher.ReplayBatch(
                new[] { DomainFactReplay.Create(publicationKey, fact) },
                replayed,
                out failure)
            && replayed[0].Completed
            && OutcomeRowCount(ledger.CaptureGameplayOutcomes()) == 1,
            "Disease anatomy burden exact replay duplicated or lost evidence: "
                + failure);
        RequireCanonicalEqual(
            canonical,
            CanonicalHashes(ledger.CaptureGameplayOutcomes()),
            "Disease anatomy burden exact replay changed canonical evidence.");

        DiseaseAnatomyBurdenProjectionSaveData tamperedSource =
            CreateDiseaseAnatomyBurdenProjection("frozen");
        tamperedSource.infectionAfter += 1f;
        var tampered = new DiseaseAnatomyBurdenAppliedFact(tamperedSource);
        var rejected = new DomainFactDelivery[1];
        Require(!dispatcher.ReplayBatch(
                new[] { DomainFactReplay.Create(publicationKey, tampered) },
                rejected,
                out failure)
            && rejected[0].Phase == DomainFactDeliveryPhase.Rejected
            && !string.IsNullOrWhiteSpace(failure),
            "Disease anatomy burden replay accepted a tampered applied value.");
        RequireCanonicalEqual(
            canonical,
            CanonicalHashes(ledger.CaptureGameplayOutcomes()),
            "Rejected disease anatomy burden replay changed canonical evidence.");

        connection.Dispose();
        var disconnected = new DiseaseAnatomyBurdenAppliedFact(
            CreateDiseaseAnatomyBurdenProjection("disconnected"));
        Require(!dispatcher.TryReserve(
                disconnected,
                out _,
                out string disconnectedFailure)
            && disconnectedFailure.StartsWith(
                "domain-fact-observer-missing:",
                StringComparison.Ordinal),
            "Disposed disease anatomy burden binding remained connected.");
    }

    private static DiseaseAnatomyBurdenProjectionSaveData
        CreateDiseaseAnatomyBurdenProjection(string suffix) => new()
        {
            phase = (int)DiseaseAnatomyBurdenProjectionPhase.AnatomyApplied,
            operationId = "disease-body-burden:host:" + suffix,
            absoluteDay = 23,
            characterId = "character:disease-host:" + suffix,
            diseaseId = "disease:host:" + suffix,
            targetSystem = (int)DiseaseTargetSystem.Breathing,
            severity = 3.5f,
            requestedInfectionBurden = 4.25f,
            nodeId = "anatomy-node:host:" + suffix,
            infectionBefore = 12f,
            infectionAfter = 16.25f,
            attemptCount = 1,
            lastFailureCode = string.Empty,
            characterDisplayName = "호스트 질병 환자",
            publicationKey = string.Empty
        };

    private static void VerifyCharacterRestFactObservation()
    {
        const string characterId = "character:rest-host";
        const string displayName = "호스트 휴식자";
        CharacterEventContext expectedContext = CreateCharacterRestContext();
        var source = new CharacterRestAppliedFactData
        {
            characterId = characterId,
            displayName = displayName,
            before = 17.25f,
            after = 86.5f,
            absoluteDay = 12,
            activeConditions = new List<string>
            {
                "condition:zeta",
                "condition:alpha",
                "condition:alpha"
            },
            context = expectedContext.Clone()
        };
        var fact = new CharacterRestAppliedFact(source);
        source.displayName = "호출자 변조";
        source.before = 99f;
        source.activeConditions.Clear();
        source.context.resultDetail = "호출자 문맥 변조";
        CharacterRestAppliedFactData detached = fact.Snapshot();
        detached.after = 1f;
        detached.activeConditions.Add("condition:detached");
        detached.context.resultDetail = "분리 스냅샷 문맥 변조";
        CharacterRestAppliedFactData frozen = fact.Snapshot();
        Require(frozen.characterId == characterId
            && frozen.displayName == displayName
            && Math.Abs(frozen.before - 17.25f) < 0.0001f
            && Math.Abs(frozen.after - 86.5f) < 0.0001f
            && frozen.activeConditions.Count == 3
            && frozen.context.resultDetail == expectedContext.resultDetail,
            "Character rest fact retained caller or snapshot aliases.");

        MigratedDomainFactCapture capture = CharacterRestFactMapping.Capture(
            fact);
        MigratedProducerOutcomeSubject subject =
            capture.Details.Subjects.Single();
        GameplayOutcomeMetric beforeMetric = capture.Details.Metrics.Single(
            value => value.MetricId.Equals(
                MigratedProducerOutcomeIds.SleepBeforeMetric));
        GameplayOutcomeMetric afterMetric = capture.Details.Metrics.Single(
            value => value.MetricId.Equals(
                MigratedProducerOutcomeIds.SleepAfterMetric));
        GameplayOutcomeFact conditionFact = capture.Details.Facts.Single(
            value => value.FactId.Equals(
                MigratedProducerOutcomeIds.ActiveConditionsFact));
        GameplayOutcomeFact contextFact = capture.Details.Facts.Single(
            value => value.FactId.Value
                == "character-event-context-sha256");
        Require(capture.Kind
                == MigratedProducerOutcomeKind.RestOutcomeIdentityEvent
            && capture.OwnerIdentity == "rest-outcome:" + characterId
            && capture.AbsoluteDay == 12
            && capture.Status == GameplayOutcomeStatus.Succeeded
            && subject.EntityId.Value == characterId
            && subject.DisplayName.DisplayText == displayName
            && capture.Details.Metrics.Length == 2
            && Math.Abs(beforeMetric.Value - 17.25d) < 0.0001d
            && Math.Abs(afterMetric.Value - 86.5d) < 0.0001d
            && beforeMetric.UnitId.Equals(MigratedProducerOutcomeIds.PointUnit)
            && afterMetric.UnitId.Equals(MigratedProducerOutcomeIds.PointUnit)
            && conditionFact.Value == "condition:alpha,condition:zeta"
            && contextFact.Value
                == CharacterRestFactMapping.ComputeContextSha256(
                    expectedContext),
            "Character rest mapping lost frozen metrics, conditions, or context.");

        SavedCharacterRestAuthority saved;
        using (var authority = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            var binding = new MigratedDomainFactObserverBinding<
                CharacterRestAppliedFact>(
                "log:character-rest-applied",
                CharacterRestFactMapping.Capture,
                authority.Migrated,
                new GameplayDomainFactCommitGroup(
                    authority.Recorder,
                    authority.Ledger));
            using IDisposable connection = binding.Connect(
                authority.Dispatcher);
            RequireThrows<InvalidOperationException>(
                () => binding.Connect(authority.Dispatcher),
                "Character rest observer accepted a duplicate binding.");
            Require(authority.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Character rest fact did not reserve: " + failure);
            string publicationKey = lease.PublicationKey;
            DomainFactDelivery delivery = authority.Dispatcher.Publish(
                lease,
                fact);
            Require(delivery.Completed
                && publicationKey.Length > 0
                && delivery.OperationKey == publicationKey
                && OutcomeRowCount(
                    authority.Ledger.CaptureGameplayOutcomes()) == 1
                && authority.Migrated.Capture().owners.Count == 1,
                "Character rest fact did not publish exactly once: "
                    + delivery.Phase + ":" + delivery.Detail);

            var row = new CharacterRestObservationSaveData
            {
                fact = fact.Snapshot(),
                publicationKey = delivery.OperationKey,
                observationCommitted = true,
                deliveryCompleted = true,
                notificationsAttempted = true,
                rollbackBlocked = false,
                rollbackFailure = string.Empty
            };
            row.Validate(characterId);
            var characters = new DungeonCharacterWorldSaveData
            {
                actors = new List<DungeonCharacterSaveData>
                {
                    new()
                    {
                        persistentId = characterId,
                        displayName = displayName,
                        restObservation = row
                    }
                }
            };
            saved = JsonConvert.DeserializeObject<SavedCharacterRestAuthority>(
                JsonConvert.SerializeObject(new SavedCharacterRestAuthority
                {
                    serializer = HostSerializer,
                    characters = characters,
                    ledger = authority.Ledger.CaptureGameplayOutcomes(),
                    migrated = authority.Migrated.Capture()
                })) ?? throw new InvalidOperationException(
                "Character rest host JSON returned null.");
        }

        CharacterRestObservationSaveData savedRow =
            saved.characters.actors.Single().restObservation;
        Require(saved.serializer == HostSerializer
            && savedRow.fact.characterId == characterId
            && savedRow.publicationKey.Length > 0
            && savedRow.observationCommitted
            && savedRow.deliveryCompleted
            && savedRow.notificationsAttempted
            && !savedRow.rollbackBlocked
            && savedRow.rollbackFailure.Length == 0,
            "Character rest JSON lost its committed owner row.");
        DungeonGameRestoreReport canonicalJoin =
            ValidateCharacterRestCanonical(
                saved.characters,
                saved.ledger,
                saved.migrated);
        Require(canonicalJoin.Success,
            "Character rest canonical save join was rejected: "
                + string.Join(" | ", canonicalJoin.Errors));
        DungeonCharacterWorldSaveData contextTamper =
            CloneCharacterRestWorld(saved.characters);
        contextTamper.actors[0].restObservation.fact.context.sequence++;
        Require(!ValidateCharacterRestCanonical(
                contextTamper,
                saved.ledger,
                saved.migrated).Success,
            "Character rest preflight accepted a same-key context change.");
        DungeonCharacterWorldSaveData wrongOwner =
            CloneCharacterRestWorld(saved.characters);
        wrongOwner.actors[0].persistentId = "character:rest-host:wrong";
        Require(!ValidateCharacterRestCanonical(
                wrongOwner,
                saved.ledger,
                saved.migrated).Success,
            "Character rest preflight accepted a mismatched actor owner.");

        using (var restored = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            restored.Restore(saved.ledger, saved.migrated);
            var binding = new MigratedDomainFactObserverBinding<
                CharacterRestAppliedFact>(
                "log:character-rest-applied",
                CharacterRestFactMapping.Capture,
                restored.Migrated,
                new GameplayDomainFactCommitGroup(
                    restored.Recorder,
                    restored.Ledger));
            using IDisposable connection = binding.Connect(
                restored.Dispatcher);
            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                saved.ledger);
            var replayed = new DomainFactDelivery[1];
            var restoredFact = new CharacterRestAppliedFact(savedRow.fact);
            Require(restored.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(
                            savedRow.publicationKey,
                            restoredFact)
                    },
                    replayed,
                    out string replayFailure)
                && replayed[0].Completed
                && restored.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(
                            savedRow.publicationKey,
                            restoredFact)
                    },
                    replayed,
                    out replayFailure)
                && replayed[0].Completed
                && OutcomeRowCount(
                    restored.Ledger.CaptureGameplayOutcomes()) == 1
                && restored.Migrated.Capture().owners.Count == 1,
                "Character rest exact replay was not idempotent: "
                    + replayFailure);
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Character rest exact replay changed canonical evidence.");

            CharacterRestAppliedFactData changedBefore = savedRow.fact.Clone();
            changedBefore.before += 1f;
            RequireCharacterRestReplayRejected(
                restored,
                savedRow.publicationKey,
                changedBefore,
                canonical,
                "Character rest replay accepted a changed before value.");
            CharacterRestAppliedFactData changedAfter = savedRow.fact.Clone();
            changedAfter.after -= 1f;
            RequireCharacterRestReplayRejected(
                restored,
                savedRow.publicationKey,
                changedAfter,
                canonical,
                "Character rest replay accepted a changed after value.");
            CharacterRestAppliedFactData changedName = savedRow.fact.Clone();
            changedName.displayName += " 변조";
            RequireCharacterRestReplayRejected(
                restored,
                savedRow.publicationKey,
                changedName,
                canonical,
                "Character rest replay accepted a changed display name.");
            CharacterRestAppliedFactData changedContext = savedRow.fact.Clone();
            changedContext.context.resultDetail += " 변조";
            RequireCharacterRestReplayRejected(
                restored,
                savedRow.publicationKey,
                changedContext,
                canonical,
                "Character rest replay accepted a changed context.");
        }

        VerifyCharacterStateNarrativeProjection();
    }

    private static void RequireCharacterRestReplayRejected(
        Authority authority,
        string publicationKey,
        CharacterRestAppliedFactData changed,
        IReadOnlyDictionary<string, string> canonical,
        string message)
    {
        var deliveries = new DomainFactDelivery[1];
        Require(!authority.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        publicationKey,
                        new CharacterRestAppliedFact(changed))
                },
                deliveries,
                out string failure)
            && deliveries[0].Phase == DomainFactDeliveryPhase.Rejected
            && !string.IsNullOrWhiteSpace(failure),
            message);
        RequireCanonicalEqual(
            canonical,
            CanonicalHashes(authority.Ledger.CaptureGameplayOutcomes()),
            message + " Rejection changed canonical evidence.");
    }

    private static void VerifyCharacterStateNarrativeProjection()
    {
        var character = new CharacterId("character:rest-host");
        CharacterEventContext context = CreateCharacterRestContext();
        var crossing = new CharacterConditionChangedEvent(
            character,
            CharacterCondition.SLEEP,
            before: 20f,
            after: 19.999f,
            day: 12,
            context: context);
        context.resultDetail = "호출자 투영 문맥 변조";
        Require(CharacterStateNarrativeObserver.TryProject(
                crossing,
                out CharacterStateNarrativeProjection critical)
            && critical.Character.Equals(character)
            && critical.Domain == CharacterNarrativeDomain.Need
            && critical.FactId == "need:sleep"
            && critical.SubjectId.Length == 0
            && critical.Outcome == "critical"
            && Math.Abs(critical.Value - 19.999f) < 0.0001f
            && critical.Day == 12
            && critical.TriggerPassives
            && critical.Context.resultDetail == "충분히 쉬었다.",
            "Character condition critical-threshold projection drifted.");
        GameplayNarrativeEventContext detached = critical.Context;
        detached.resultDetail = "분리 투영 문맥 변조";
        Require(critical.Context.resultDetail == "충분히 쉬었다.",
            "Character state projection exposed its context alias.");
        Require(!CharacterStateNarrativeObserver.TryProject(
                new CharacterConditionChangedEvent(
                    character,
                    CharacterCondition.SLEEP,
                    20f,
                    20f),
                out _)
            && !CharacterStateNarrativeObserver.TryProject(
                new CharacterConditionChangedEvent(
                    character,
                    CharacterCondition.SLEEP,
                    80f,
                    80f),
                out _),
            "Character condition exact thresholds emitted a projection.");
        Require(CharacterStateNarrativeObserver.TryProject(
                new CharacterConditionChangedEvent(
                    character,
                    CharacterCondition.SLEEP,
                    80f,
                    80.001f,
                    day: 13),
                out CharacterStateNarrativeProjection satisfied)
            && satisfied.Outcome == "satisfied"
            && satisfied.Context == null,
            "Character condition satisfied/null-context projection drifted.");
        Require(!CharacterStateNarrativeObserver.TryProject(
                new CharacterMoodFactorAppliedEvent(
                    character,
                    "skill:rest-host",
                    3f,
                    day: 13,
                    context: CreateCharacterRestContext()),
                out _)
            && CharacterStateNarrativeObserver.TryProject(
                new CharacterMoodFactorAppliedEvent(
                    character,
                    "mood:rested",
                    0f,
                    day: 13),
                out CharacterStateNarrativeProjection mood)
            && mood.Domain == CharacterNarrativeDomain.Mood
            && mood.FactId == "mood:rested"
            && mood.Outcome == "positive"
            && mood.TriggerPassives
            && mood.Context == null,
            "Character mood projection did not suppress skill factors or preserve zero semantics.");
    }

    private static CharacterEventContext CreateCharacterRestContext() => new()
    {
        eventInstanceId = "event:rest-host:1",
        chainId = "chain:rest-host:1",
        locationId = "location:rest-host:bedroom",
        locationDisplayName = "호스트 침실",
        actorId = "character:rest-host",
        actorDisplayName = "호스트 휴식자",
        counterpartyId = "character:rest-host:helper",
        counterpartyDisplayName = "호스트 도우미",
        usedObjectId = "facility:rest-host:bed",
        usedObjectDisplayName = "호스트 침대",
        resultDetail = "충분히 쉬었다.",
        occurredDay = 12,
        sequence = 4L
    };

    private static void VerifyCharacterLifeTransitionFactObservation()
    {
        var source = new CharacterLifeTransitionSaveData
        {
            kind = (int)CharacterLifeTransitionKind.AgeCondition,
            absoluteDay = 31,
            characterId = "character:life-host",
            conditionId = "condition:host-life",
            previous = (int)AgeConditionSeverity.Mild,
            current = (int)AgeConditionSeverity.Moderate
        };
        var parentFact = new CharacterLifeTransitionFact(source);
        string domainId = parentFact.DomainId;
        source.characterId = "character:life-host-mutated";
        source.conditionId = "condition:mutated";
        source.current = (int)AgeConditionSeverity.Severe;
        CharacterLifeTransitionSaveData detached = parentFact.Snapshot();
        detached.conditionId = "condition:detached-mutation";
        Require(parentFact.CharacterId == "character:life-host"
            && parentFact.AgeChange.ConditionId == "condition:host-life"
            && parentFact.AgeChange.Current == AgeConditionSeverity.Moderate
            && parentFact.DomainId == domainId
            && domainId.StartsWith(
                "life-transition:",
                StringComparison.Ordinal)
            && domainId.Length == "life-transition:".Length + 64
            && new CharacterLifeTransitionFact(parentFact.Snapshot()).DomainId
                == domainId,
            "Character life fact retained DTO aliases or changed its stable domain identity.");

        MigratedDomainFactCapture parentCapture =
            CharacterLifeTransitionFactMapping.Capture(parentFact);
        MigratedProducerOutcomeSubject parentSubject =
            parentCapture.Details.Subjects.Single();
        Require(parentCapture.Kind
                == MigratedProducerOutcomeKind.CharacterAgeConditionChangedEvent
            && parentCapture.OwnerIdentity
                == "character-age-condition-change@1|31|character:life-host|condition:host-life|0|1|0|0"
            && parentCapture.AbsoluteDay == 31
            && parentCapture.Status == GameplayOutcomeStatus.Succeeded
            && parentCapture.Details.Metrics.Length == 0
            && parentCapture.Details.Facts.Length == 0
            && parentCapture.Details.Summary
                == "age-condition-change@1|character:life-host|condition:host-life|0|1|progressed|active"
            && parentSubject.EntityId.Kind.Equals(
                MigratedProducerOutcomeIds.CharacterKind)
            && parentSubject.EntityId.Value == parentFact.CharacterId
            && parentSubject.DisplayName.DisplayText == parentFact.CharacterId
            && parentSubject.Role.Equals(
                MigratedProducerOutcomeIds.ActorRole),
            "Character age-condition mapping drifted from its legacy single-subject payload.");

        CharacterLifeTransitionFact stageFact =
            CharacterLifeTransitionFact.FromLifeStage(
                new CharacterLifeStageTransition(
                    new CharacterId("character:life-stage-host"),
                    CharacterLifeStage.Adult,
                    CharacterLifeStage.Elder),
                absoluteDay: 31);
        MigratedDomainFactCapture stageCapture =
            CharacterLifeTransitionFactMapping.Capture(stageFact);
        Require(stageCapture.Kind
                == MigratedProducerOutcomeKind.CharacterLifeStageChangedEvent
            && stageCapture.OwnerIdentity
                == "character-life-stage-change@1|31|character:life-stage-host|3|4"
            && stageCapture.Details.Subjects.Single().EntityId.Value
                == "character:life-stage-host"
            && stageCapture.Details.Summary
                == "life-stage-change@1|character:life-stage-host|3|4",
            "Character life-stage mapping drifted from its legacy single-subject payload.");

        var registry = new GameplayOutcomeRegistry(
            new IGameplayOutcomeDescriptor[]
            {
                new AgeConditionAnatomyOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new AgeConditionAnatomyOutcomeAdapter(),
                new MigratedProducerOutcomeAdapter()
            },
            new IGameplayOutcomeDescriptorCatalog[]
            {
                new MigratedProducerOutcomeDescriptorCatalog(
                    new KoreanJosaFormatter())
            });
        var limits = new GameplayOutcomeBufferLimits(
            smallPageCount: 32,
            largePageCount: 4,
            knownResultKeyCapacity: 128,
            retainedSmallPageCount: 16,
            retainedLargePageCount: 4);
        var ledger = new GameplayOutcomeLedger(
            registry,
            limits,
            new GameplayOutcomeRunId(
                "run:character-life-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture)),
            1L);
        var recorder = new GameplayOutcomeRecorder(
            ledger,
            registry,
            new GameEventBus());
        var roots = new DungeonRuntimeAggregateRootStore();
        var migrated = new MigratedProducerOutcomeRuntime(
            roots,
            ledger,
            recorder,
            ledger);
        var commits = new GameplayDomainFactCommitGroup(
            recorder,
            ledger);
        using var dispatcher = new DomainFactDispatcher();
        MigratedDomainFactObserverBinding<CharacterLifeTransitionFact>
            parentBinding = CharacterLifeTransitionFactMapping.CreateBinding(
                migrated,
                commits);
        GameplayDomainFactObserverBinding<
            AgeConditionAnatomyAppliedFact,
            AgeConditionAnatomyOutcomeReceipt> childBinding =
            AgeConditionAnatomyFactMapping.CreateBinding(
                registry,
                ledger,
                recorder,
                commits);
        using IDisposable parentConnection = parentBinding.Connect(dispatcher);
        using IDisposable childConnection = childBinding.Connect(dispatcher);
        RequireThrows<InvalidOperationException>(
            () => parentBinding.Connect(dispatcher),
            "Character life observer accepted a duplicate binding.");

        Require(dispatcher.TryReserve(
                parentFact,
                out DomainFactLease parentLease,
                out string failure),
            "Character life parent fact did not reserve: " + failure);
        string parentKey = parentLease.PublicationKey;
        DomainFactDelivery parentDelivery = dispatcher.Publish(
            parentLease,
            parentFact);
        Require(parentDelivery.Completed
            && parentDelivery.OperationKey == parentKey
            && !string.IsNullOrWhiteSpace(parentDelivery.PublicationId),
            "Character life parent fact did not publish: "
                + parentDelivery.Detail);

        AgeConditionAnatomyProjectionSaveData projection =
            CharacterLifeObservationState.Projection(
                parentFact,
                parentDelivery);
        projection.phase =
            (int)AgeConditionAnatomyProjectionPhase.AnatomyApplied;
        projection.characterDisplayName = "호스트 생애 환자";
        projection.attemptCount = 1;
        projection.aggregateMaximumHealth = 200f;
        projection.aggregateHealthBefore = 130f;
        projection.aggregateHealthAfter = 117f;
        projection.nodeDeltas = new List<
            AgeConditionAnatomyNodeDeltaSaveData>
        {
            new()
            {
                nodeId = "anatomy-node:life-host-core",
                maximumHealth = 100f,
                healthBefore = 80f,
                requestedDamage = 8f,
                healthAfter = 72f,
                bleedingBefore = 0f,
                bleedingAfter = 0f
            },
            new()
            {
                nodeId = "anatomy-node:life-host-breathing",
                maximumHealth = 100f,
                healthBefore = 50f,
                requestedDamage = 5f,
                healthAfter = 45f,
                bleedingBefore = 0f,
                bleedingAfter = 0f
            }
        };
        var childFact = new AgeConditionAnatomyAppliedFact(projection);
        AgeConditionAnatomyOutcomeReceipt childReceipt =
            AgeConditionAnatomyFactMapping.Capture(childFact);
        Require(childReceipt.ParentOutcomeId.ToString()
                == parentDelivery.PublicationId
            && childReceipt.ParentResultKey.ToString() == parentKey
            && childReceipt.OperationId.Value == parentFact.AnatomyOperationId,
            "Age-condition anatomy mapping lost its exact parent binding.");

        Require(dispatcher.TryReserve(
                childFact,
                out DomainFactLease childLease,
                out failure),
            "Age-condition anatomy child fact did not reserve: " + failure);
        string childKey = childLease.PublicationKey;
        DomainFactDelivery childDelivery = dispatcher.Publish(
            childLease,
            childFact);
        Require(childDelivery.Completed
            && childDelivery.OperationKey == childKey
            && childKey == childReceipt.ResultKey.ToString(),
            "Age-condition anatomy child fact did not publish: "
                + childDelivery.Detail);

        GameplayOutcomeLedgerSaveData published =
            ledger.CaptureGameplayOutcomes();
        GameplayOutcomeSnapshot parentRow = published.outbox
            .Concat(published.exactOutcomes)
            .Single(value => ResultKey(value) == parentKey);
        GameplayOutcomeSnapshot childRow = published.outbox
            .Concat(published.exactOutcomes)
            .Single(value => ResultKey(value) == childKey);
        Require(OutcomeRowCount(published) == 2
            && childRow.parentRunId == parentRow.runId
            && childRow.parentSequence == parentRow.sequence
            && childRow.causationRelationId == "age-condition-effect",
            "Age-condition anatomy child lost its canonical parent causation.");

        projection.publicationKey = childKey;
        AgeConditionAnatomyProjectionSaveData restoredProjection =
            JsonConvert.DeserializeObject<
                AgeConditionAnatomyProjectionSaveData>(
                JsonConvert.SerializeObject(projection))
            ?? throw new InvalidOperationException(
                "Age-condition anatomy DTO host JSON returned null.");
        var restoredChild = new AgeConditionAnatomyAppliedFact(
            restoredProjection);
        AgeConditionAnatomyProjectionSaveData restoredSnapshot =
            restoredChild.Snapshot();
        Require(restoredSnapshot.operationId == parentFact.AnatomyOperationId
            && restoredSnapshot.parentPublicationId
                == parentDelivery.PublicationId
            && restoredSnapshot.parentPublicationKey == parentKey
            && restoredSnapshot.publicationKey == childKey,
            "Age-condition anatomy DTO restore lost publication references.");
        restoredProjection.parentPublicationId = "run:mutated/999";
        restoredProjection.parentPublicationKey = "mutated-key";
        Require(restoredChild.Snapshot().parentPublicationId
                == parentDelivery.PublicationId
            && restoredChild.Snapshot().parentPublicationKey == parentKey,
            "Restored age-condition anatomy fact retained DTO aliases.");

        var lifeSource = new CharacterLifeWorldSaveData
        {
            version = CharacterLifeWorldSaveData.CurrentVersion,
            lastAdvancedAbsoluteDay = parentFact.AbsoluteDay,
            pendingDailyObservation =
                new CharacterLifeDailyObservationSaveData(),
            pendingAgeConditionAnatomyProjections = new List<
                AgeConditionAnatomyProjectionSaveData>
            {
                restoredChild.Snapshot()
            }
        };
        DungeonMigratedProducerOutcomeSaveData counters =
            migrated.Capture();
        Require(ValidateCharacterLifeCanonical(
                lifeSource,
                published,
                counters).Success,
            "Character life preflight rejected its exact parent/child canonical join.");

        CharacterLifeWorldSaveData renamedLife =
            CloneCharacterLifeWorld(lifeSource);
        renamedLife.pendingAgeConditionAnatomyProjections[0]
            .characterDisplayName += " 변조";
        Require(!ValidateCharacterLifeCanonical(
                renamedLife,
                published,
                counters).Success,
            "Character life preflight accepted a changed frozen child name.");

        CharacterLifeWorldSaveData resetLife =
            CloneCharacterLifeWorld(lifeSource);
        AgeConditionAnatomyProjectionSaveData resetChild =
            resetLife.pendingAgeConditionAnatomyProjections[0];
        resetChild.phase = (int)AgeConditionAnatomyProjectionPhase.Pending;
        resetChild.characterDisplayName = string.Empty;
        resetChild.publicationKey = string.Empty;
        Require(!ValidateCharacterLifeCanonical(
                resetLife,
                published,
                counters).Success,
            "Character life preflight accepted a pending child with canonical evidence.");

        CharacterLifeWorldSaveData wrongParentLife =
            CloneCharacterLifeWorld(lifeSource);
        wrongParentLife.pendingAgeConditionAnatomyProjections[0]
            .parentPublicationId = new GameplayOutcomeId(
                new GameplayOutcomeRunId(parentRow.runId),
                parentRow.sequence + 1000L).ToString();
        Require(!ValidateCharacterLifeCanonical(
                wrongParentLife,
                published,
                counters).Success,
            "Character life preflight accepted the wrong parent sequence.");

        IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
            published);
        var replayed = new DomainFactDelivery[2];
        Require(dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(parentKey, parentFact),
                    DomainFactReplay.Create(childKey, childFact)
                },
                replayed,
                out failure)
            && replayed.All(value => value.Completed)
            && OutcomeRowCount(ledger.CaptureGameplayOutcomes()) == 2
            && migrated.Capture().owners.Count == 1,
            "Character life parent/child exact replay duplicated evidence: "
                + failure);
        RequireCanonicalEqual(
            canonical,
            CanonicalHashes(ledger.CaptureGameplayOutcomes()),
            "Character life parent/child exact replay changed canonical evidence.");

        AgeConditionAnatomyProjectionSaveData altered =
            childFact.Snapshot();
        altered.nodeDeltas[0].healthAfter -= 1f;
        altered.aggregateHealthAfter -= 1f;
        var alteredChild = new AgeConditionAnatomyAppliedFact(altered);
        var rejected = new DomainFactDelivery[1];
        Require(!dispatcher.ReplayBatch(
                new[] { DomainFactReplay.Create(childKey, alteredChild) },
                rejected,
                out failure)
            && rejected[0].Phase == DomainFactDeliveryPhase.Rejected
            && !string.IsNullOrWhiteSpace(failure),
            "Age-condition anatomy replay accepted an altered frozen payload.");
        RequireCanonicalEqual(
            canonical,
            CanonicalHashes(ledger.CaptureGameplayOutcomes()),
            "Rejected age-condition anatomy replay changed canonical evidence.");
    }

    private static void VerifyEmergencyWorkSuspensionOwnerFactRecovery()
    {
        const string characterId = "character:emergency-host:worker";
        const string buildingId = "building:emergency-host:forge";
        const string workerName = "호스트 긴급 작업자";
        const string targetName = "호스트 긴급 대장간";
        Require(WorkTypeCatalog.TryGet(
                BuiltInWorkTypeIds.Construct,
                out WorkTypeDefinition workType),
            "Emergency suspension host work type was unavailable.");

        var source = new EmergencyWorkSuspensionFactSaveData
        {
            characterId = characterId,
            workerDisplayName = workerName,
            workTypeId = workType.Id,
            workDisplayName = workType.DisplayName,
            targetBuildingId = buildingId,
            targetDisplayName = targetName,
            alertEpochId = 1L,
            suspendedAtAbsoluteHour = 984L,
            ownerRevision = 1L,
            absoluteDay = 42,
            inlineCompletedWork = 2f,
            inlineRequiredWork = 10f
        };
        var fact = new EmergencyWorkSuspensionFact(source);
        EmergencyWorkSuspensionOutcomeReceipt mapped =
            EmergencyWorkSuspensionFactMapping.Capture(fact);
        var expected = new EmergencyWorkSuspensionOutcomeReceipt(
            characterId,
            new KoreanNameSnapshot(
                workerName,
                "runtime-character-current-v1:" + characterId,
                KoreanPronunciationHint.AutoHangulDisplay(
                    "runtime-character-pronunciation-v1:" + characterId),
                "ko-KR"),
            BuiltInWorkTypeIds.Construct,
            workType.DisplayName,
            buildingId,
            new KoreanNameSnapshot(
                targetName,
                "runtime-facility-current-v1:" + buildingId,
                KoreanPronunciationHint.AutoHangulDisplay(
                    "runtime-facility-pronunciation-v1:" + buildingId),
                "ko-KR"),
            alertEpochId: 1L,
            suspendedAtAbsoluteHour: 984L,
            progressExternallyPersisted: false,
            inlineCompletedWork: 2f,
            inlineRequiredWork: 10f,
            ownerRevision: 1L,
            absoluteDay: 42);
        source.workerDisplayName = "변조된 작업자";
        source.inlineCompletedWork = 9f;
        EmergencyWorkSuspensionFactSaveData detached = fact.Snapshot();
        detached.targetDisplayName = "변조된 작업장";
        detached.ownerRevision = 9L;
        EmergencyWorkSuspensionFactSaveData frozen = fact.Snapshot();
        Require(frozen.workerDisplayName == workerName
            && frozen.targetDisplayName == targetName
            && frozen.inlineCompletedWork == 2f
            && frozen.ownerRevision == 1L
            && fact.DomainId
                == "emergency-work-suspension:" + characterId + ":1:1"
            && mapped.CharacterId == expected.CharacterId
            && mapped.WorkerDisplayName.DisplayText
                == expected.WorkerDisplayName.DisplayText
            && mapped.WorkerDisplayName.DisplaySnapshotRevision
                == expected.WorkerDisplayName.DisplaySnapshotRevision
            && mapped.WorkerDisplayName.PronunciationHint.Revision
                == expected.WorkerDisplayName.PronunciationHint.Revision
            && mapped.WorkerDisplayName.PronunciationHint.Mode
                == expected.WorkerDisplayName.PronunciationHint.Mode
            && mapped.WorkerDisplayName.PronunciationHint.Value
                == expected.WorkerDisplayName.PronunciationHint.Value
            && mapped.WorkerDisplayName.PronunciationHint
                    .ExplicitFinalConsonant
                == expected.WorkerDisplayName.PronunciationHint
                    .ExplicitFinalConsonant
            && mapped.WorkerDisplayName.Locale == "ko-KR"
            && mapped.WorkTypeId == expected.WorkTypeId
            && mapped.WorkDisplayName == expected.WorkDisplayName
            && mapped.TargetBuildingId == expected.TargetBuildingId
            && mapped.TargetDisplayName.DisplayText
                == expected.TargetDisplayName.DisplayText
            && mapped.TargetDisplayName.DisplaySnapshotRevision
                == expected.TargetDisplayName.DisplaySnapshotRevision
            && mapped.TargetDisplayName.PronunciationHint.Revision
                == expected.TargetDisplayName.PronunciationHint.Revision
            && mapped.TargetDisplayName.PronunciationHint.Mode
                == expected.TargetDisplayName.PronunciationHint.Mode
            && mapped.TargetDisplayName.PronunciationHint.Value
                == expected.TargetDisplayName.PronunciationHint.Value
            && mapped.TargetDisplayName.PronunciationHint
                    .ExplicitFinalConsonant
                == expected.TargetDisplayName.PronunciationHint
                    .ExplicitFinalConsonant
            && mapped.TargetDisplayName.Locale == "ko-KR"
            && mapped.AlertEpochId == expected.AlertEpochId
            && mapped.SuspendedAtAbsoluteHour
                == expected.SuspendedAtAbsoluteHour
            && mapped.ProgressExternallyPersisted
                == expected.ProgressExternallyPersisted
            && mapped.InlineCompletedWork == expected.InlineCompletedWork
            && mapped.InlineRequiredWork == expected.InlineRequiredWork
            && mapped.OwnerRevision == expected.OwnerRevision
            && mapped.AbsoluteDay == expected.AbsoluteDay
            && mapped.ResultKey.ToString() == expected.ResultKey.ToString(),
            "Emergency suspension fact was mutable or drifted from its legacy receipt mapping.");

        using (var normal = new EmergencySuspensionAuthority())
        {
            SettlementAlertRuntime runtime = normal.CreateRuntime();
            StartEmergencySuspensionAlert(runtime, "normal");
            SettlementSuspendedWorkSnapshot snapshot =
                normal.CreateSnapshot(runtime, ownerRevision: 1L);
            EmergencyAccountingResult recorded =
                runtime.RecordSuspendedWork(snapshot);
            SettlementThreatAlertSaveData saved =
                runtime.CaptureAlertSaveData();
            Require(recorded.Success
                && saved.suspensionObservations.Count == 1
                && !saved.suspensionObservations[0].pending
                && runtime.TryGetSuspendedWork(characterId, out _)
                && OutcomeRowCount(
                    normal.Ledger.CaptureGameplayOutcomes()) == 1,
                "Emergency suspension real staged publication did not complete exactly once.");
        }

        using (var rejected = new EmergencySuspensionAuthority())
        {
            var rejecting = new EmergencySuspensionStagePublisher(
                rejected.Dispatcher,
                EmergencySuspensionStageBehavior.Reject);
            SettlementAlertRuntime runtime = rejected.CreateRuntime(rejecting);
            StartEmergencySuspensionAlert(runtime, "reject");
            EmergencyAccountingResult result = runtime.RecordSuspendedWork(
                rejected.CreateSnapshot(runtime, ownerRevision: 1L));
            Require(!result.Success
                && !runtime.TryGetSuspendedWork(characterId, out _)
                && runtime.CaptureAlertSaveData()
                    .suspensionObservations.Count == 0
                && OutcomeRowCount(
                    rejected.Ledger.CaptureGameplayOutcomes()) == 0,
                "Known emergency suspension publication rejection retained owner or ledger state.");
        }

        SettlementThreatAlertSaveData pendingSave;
        SettlementThreatAlertSaveData orphanedPendingSave;
        GameplayOutcomeLedgerSaveData committedLedger;
        IReadOnlyDictionary<string, string> canonical;
        long frozenEpoch;
        using (var uncertain = new EmergencySuspensionAuthority())
        {
            var inDoubt = new EmergencySuspensionStagePublisher(
                uncertain.Dispatcher,
                EmergencySuspensionStageBehavior.CommitThenUncertain);
            SettlementAlertRuntime runtime = uncertain.CreateRuntime(inDoubt);
            StartEmergencySuspensionAlert(runtime, "uncertain");
            SettlementSuspendedWorkSnapshot snapshot =
                uncertain.CreateSnapshot(runtime, ownerRevision: 1L);
            frozenEpoch = snapshot.AlertEpochId;
            EmergencyAccountingResult result =
                runtime.RecordSuspendedWork(snapshot);
            Require(result.Success
                && result.Code.StartsWith(
                    "suspended-work-recorded-observation-pending",
                    StringComparison.Ordinal)
                && runtime.TryGetSuspendedWork(characterId, out _),
                "Commit-in-doubt emergency suspension did not preserve applied owner state.");

            EmergencyAccountingResult escalated = runtime.PublishIncidentSignal(
                new SettlementIncidentSignal(
                    "incident:emergency-suspension-host:red",
                    SettlementThreatAlertLevel.Red,
                    1L,
                    "host",
                    "escalate with pending frozen suspension"));
            pendingSave = runtime.CaptureAlertSaveData();
            EmergencyWorkSuspensionObservationSaveData observation =
                pendingSave.suspensionObservations.Single();
            SettlementSuspendedWorkSaveData active =
                pendingSave.suspendedWork.Single();
            Require(escalated.Success
                && observation.pending
                && observation.fact.alertEpochId == frozenEpoch
                && observation.fact.workerDisplayName == workerName
                && observation.fact.targetDisplayName == targetName
                && active.alertEpochId > frozenEpoch,
                "Alert escalation changed frozen suspension evidence instead of only rebinding active ownership.");
            committedLedger = uncertain.Ledger.CaptureGameplayOutcomes();
            canonical = CanonicalHashes(committedLedger);
            Require(OutcomeRowCount(committedLedger) == 1,
                "Commit-in-doubt suspension did not cross exactly one canonical boundary.");

            EmergencyAccountingResult resumed =
                runtime.MarkSuspendedWorkResumed(
                    characterId,
                    active.alertEpochId);
            orphanedPendingSave = runtime.CaptureAlertSaveData();
            Require(resumed.Success
                && !runtime.TryGetSuspendedWork(characterId, out _)
                && orphanedPendingSave.suspendedWork.Count == 0
                && orphanedPendingSave.suspensionObservations.Count == 1
                && orphanedPendingSave.suspensionObservations[0].pending,
                "Resuming work discarded its still-pending suspension evidence.");
        }

        SettlementThreatAlertSaveData pendingRoundTrip =
            JsonConvert.DeserializeObject<SettlementThreatAlertSaveData>(
                JsonConvert.SerializeObject(pendingSave))
            ?? throw new InvalidOperationException(
                "Emergency suspension host JSON round-trip returned null.");
        GameplayOutcomeLedgerSaveData ledgerRoundTrip =
            JsonConvert.DeserializeObject<GameplayOutcomeLedgerSaveData>(
                JsonConvert.SerializeObject(committedLedger))
            ?? throw new InvalidOperationException(
                "Emergency suspension ledger host JSON round-trip returned null.");

        using var empty = new EmergencySuspensionAuthority();
        GameplayOutcomeLedgerSaveData emptyLedger =
            empty.Ledger.CaptureGameplayOutcomes();
        Require(ValidateEmergencyWorkSuspensionCanonical(
                pendingRoundTrip,
                ledgerRoundTrip).Success
            && ValidateEmergencyWorkSuspensionCanonical(
                pendingRoundTrip,
                emptyLedger).Success,
            "Pending emergency suspension was not accepted with optional canonical evidence.");

        using var restored = new EmergencySuspensionAuthority();
        restored.RestoreLedger(ledgerRoundTrip);
        var recovery = new EmergencySuspensionRecoveryPublisher(
            restored.Dispatcher)
        {
            RejectNextReplay = true
        };
        SettlementAlertRuntime restoredRuntime = restored.CreateRuntime(
            recovery: recovery);
        restoredRuntime.RestoreAlertSaveData(pendingRoundTrip);
        bool loggingEnabled = UnityEngine.Debug.unityLogger.logEnabled;
        try
        {
            UnityEngine.Debug.unityLogger.logEnabled = false;
            restoredRuntime.OnRestoreCompleted();
        }
        finally
        {
            UnityEngine.Debug.unityLogger.logEnabled = loggingEnabled;
        }
        SettlementThreatAlertSaveData failedRecovery =
            restoredRuntime.CaptureAlertSaveData();
        EmergencyWorkSuspensionObservationSaveData failedObservation =
            failedRecovery.suspensionObservations.Single();
        EmergencyWorkSuspensionObservationSaveData pendingObservation =
            pendingRoundTrip.suspensionObservations.Single();
        Require(recovery.RejectedReplayCount == 1
            && failedObservation.pending
            && failedObservation.publicationKey
                == pendingObservation.publicationKey
            && failedObservation.fact.workerDisplayName
                == pendingObservation.fact.workerDisplayName
            && failedObservation.fact.targetDisplayName
                == pendingObservation.fact.targetDisplayName
            && failedObservation.fact.ownerRevision
                == pendingObservation.fact.ownerRevision
            && restoredRuntime.TryGetSuspendedWork(
                characterId,
                out SettlementSuspendedWorkSnapshot pendingOwner)
            && pendingOwner.AlertEpochId > frozenEpoch
            && pendingOwner.OutcomeOwnerRevision == 1L
            && pendingOwner.InlineCompletedWork == 2f
            && pendingOwner.InlineRequiredWork == 10f
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1,
            "Restore hook failure escaped or changed pending suspension ownership.");
        restoredRuntime.OnRestoreCompleted();
        restoredRuntime.OnRestoreCompleted();
        SettlementThreatAlertSaveData completed =
            restoredRuntime.CaptureAlertSaveData();
        EmergencyWorkSuspensionObservationSaveData completedObservation =
            completed.suspensionObservations.Single();
        Require(!completedObservation.pending
            && completedObservation.fact.alertEpochId == frozenEpoch
            && completedObservation.fact.workerDisplayName == workerName
            && completedObservation.fact.targetDisplayName == targetName
            && restoredRuntime.TryGetSuspendedWork(
                characterId,
                out SettlementSuspendedWorkSnapshot restoredOwner)
            && restoredOwner.AlertEpochId > frozenEpoch
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1,
            "Emergency suspension restore recovery duplicated evidence or lost owner state.");
        RequireCanonicalEqual(
            canonical,
            CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
            "Emergency suspension restore replay changed canonical evidence.");
        Require(ValidateEmergencyWorkSuspensionCanonical(
                completed,
                restored.Ledger.CaptureGameplayOutcomes()).Success
            && !ValidateEmergencyWorkSuspensionCanonical(
                completed,
                emptyLedger).Success,
            "Completed emergency suspension did not require its canonical outcome.");

        SettlementThreatAlertSaveData tampered =
            CloneEmergencyWorkSuspensionSave(completed);
        tampered.suspensionObservations[0].fact.workerDisplayName += " 변조";
        Require(!ValidateEmergencyWorkSuspensionCanonical(
                tampered,
                restored.Ledger.CaptureGameplayOutcomes()).Success,
            "Emergency suspension preflight accepted a changed frozen fact.");

        SettlementThreatAlertSaveData orphanedRoundTrip =
            CloneEmergencyWorkSuspensionSave(orphanedPendingSave);
        Require(ValidateEmergencyWorkSuspensionCanonical(
                orphanedRoundTrip,
                ledgerRoundTrip).Success,
            "Pending orphaned suspension evidence failed its canonical preflight.");
        using var orphaned = new EmergencySuspensionAuthority();
        orphaned.RestoreLedger(ledgerRoundTrip);
        SettlementAlertRuntime orphanedRuntime = orphaned.CreateRuntime();
        orphanedRuntime.RestoreAlertSaveData(orphanedRoundTrip);
        orphanedRuntime.OnRestoreCompleted();
        SettlementThreatAlertSaveData orphanedRecovered =
            orphanedRuntime.CaptureAlertSaveData();
        Require(orphanedRecovered.suspendedWork.Count == 0
            && orphanedRecovered.suspensionObservations.Count == 0
            && !orphanedRuntime.TryGetSuspendedWork(characterId, out _)
            && OutcomeRowCount(
                orphaned.Ledger.CaptureGameplayOutcomes()) == 1,
            "Pending resumed-work evidence did not clear after exact replay.");
        RequireCanonicalEqual(
            canonical,
            CanonicalHashes(orphaned.Ledger.CaptureGameplayOutcomes()),
            "Resumed-work pending replay duplicated canonical evidence.");
    }

    private static void VerifySpeciesActionFactObservation()
    {
        GolemUpkeepFactSaveData golemSource = CreateGolemUpkeepFactData(
            "frozen",
            applied: true);
        var golem = new GolemUpkeepFact(golemSource);
        SpeciesIncidentFactSaveData incidentSource =
            CreateSpeciesIncidentFactData("frozen", applied: true);
        var incident = new SpeciesIncidentFact(incidentSource);
        HarpyGaleFactSaveData harpySource = CreateHarpyGaleFactData("frozen");
        var harpy = new HarpyGaleFact(harpySource);

        golemSource.characterDisplayName = "변조된 골렘";
        golemSource.wornNodes.Add("node:species-host:mutated-source");
        incidentSource.summary = "변조된 사건";
        harpySource.itemDisplayName = "변조된 물건";
        GolemUpkeepFactSaveData detachedGolem = golem.Snapshot();
        SpeciesIncidentFactSaveData detachedIncident = incident.Snapshot();
        HarpyGaleFactSaveData detachedHarpy = harpy.Snapshot();
        detachedGolem.physicalEvidence = "변조된 증거";
        detachedGolem.wornNodes.Add("node:species-host:mutated-snapshot");
        detachedIncident.characterDisplayName = "변조된 주민";
        detachedHarpy.destinationStackId = "stack:species-host:altered";
        Require(golem.Snapshot().characterDisplayName == "호스트 골렘"
            && golem.Snapshot().physicalEvidence.StartsWith(
                "owner=golem-recharge:host:frozen|",
                StringComparison.Ordinal)
            && golem.Snapshot().wornNodes.Count == 0
            && incident.Snapshot().characterDisplayName == "호스트 주민"
            && incident.Snapshot().summary == "호스트 종족 사건"
            && harpy.Snapshot().itemDisplayName == "호스트 깃털 꾸러미"
            && harpy.Snapshot().destinationStackId
                == "stack:species-host:frozen:destination",
            "Species action facts retained mutable DTO state.");

        var roundedDischarge = new GolemUpkeepFact(
            new GolemUpkeepFactSaveData
            {
                operationId = "golem-discharge:host:rounding",
                characterId = "character:species-host:golem",
                characterDisplayName = "호스트 골렘",
                speciesId = "Golem",
                speciesDisplayName = "호스트 골렘 종족",
                revision = 8L,
                absoluteDay = 42,
                kind = "discharge",
                chargeBefore = 1f,
                chargeAfter = 0f,
                integrityBefore = 90f,
                integrityAfter = 90f,
                healthBefore = 0.3f,
                healthAfter = 0.2f,
                requestedDamage = 0.1f,
                appliedDamage = 0.3f - 0.2f
            });
        Require(roundedDischarge.Snapshot().appliedDamage
                > roundedDischarge.Snapshot().requestedDamage,
            "Golem damage validation did not exercise float subtraction rounding.");

        GolemUpkeepOutcomeReceipt golemReceipt =
            SpeciesActionFactMapping.Capture(golem);
        SpeciesIncidentOutcomeReceipt incidentReceipt =
            SpeciesActionFactMapping.Capture(incident);
        HarpyGaleRelocationOutcomeReceipt harpyReceipt =
            SpeciesActionFactMapping.Capture(harpy);
        PreparedPhysicalItemRelocationPreview harpyPreview =
            harpy.ToPhysicalPreview();
        Require(golemReceipt.Payload.ResultKey.ToString()
                == new GameplayResultKey(
                    EnvironmentOutcomeIds.GolemUpkeepProducer,
                    new GameplayOperationId(
                        "golem-recharge:host:frozen"),
                    7L,
                    0).ToString()
            && golemReceipt.Payload.Participants.Count == 2
            && golemReceipt.Payload.Participants.Get(0)
                .DisplayName.DisplayText == "호스트 골렘"
            && golemReceipt.Payload.Participants.Get(1)
                .DisplayName.DisplayText == "호스트 골렘 종족"
            && SpeciesPayloadFact(
                    golemReceipt.Payload,
                    EnvironmentOutcomeIds.StateBeforeFact)
                == "charge=20|integrity=90|health=80"
            && SpeciesPayloadFact(
                    golemReceipt.Payload,
                    EnvironmentOutcomeIds.StateAfterFact)
                == "charge=55|integrity=90|health=80"
            && SpeciesPayloadFact(
                    golemReceipt.Payload,
                    EnvironmentOutcomeIds.WorkChildFact)
                == "worker=character:species-host:golem|work=3"
            && incidentReceipt.Payload.ResultKey.ToString()
                == new GameplayResultKey(
                    EnvironmentOutcomeIds.SpeciesIncidentProducer,
                    new GameplayOperationId(
                        "species-incident:host:frozen"),
                    9L,
                    0).ToString()
            && incidentReceipt.Payload.Participants.Get(0)
                .DisplayName.DisplayText == "호스트 주민"
            && incidentReceipt.Payload.Participants.Get(1)
                .DisplayName.DisplayText == "호스트 하피 종족"
            && SpeciesPayloadFact(
                    incidentReceipt.Payload,
                    EnvironmentOutcomeIds.IncidentFact)
                == "species-incident:host-gale"
            && SpeciesPayloadFact(
                    incidentReceipt.Payload,
                    EnvironmentOutcomeIds.SummaryFact)
                == "호스트 종족 사건"
            && harpyReceipt.Payload.ResultKey.ToString()
                == new GameplayResultKey(
                    EnvironmentOutcomeIds.SpeciesIncidentProducer,
                    new GameplayOperationId(
                        "species-harpy-gale:host:frozen"),
                    11L,
                    0).ToString()
            && harpyReceipt.Payload.Participants.Get(0)
                .DisplayName.DisplayText == "호스트 하피"
            && harpyReceipt.Payload.Participants.Get(1)
                .DisplayName.DisplayText == "호스트 하피 종족"
            && harpyReceipt.Payload.Participants.Get(2)
                .DisplayName.DisplayText == "호스트 깃털 꾸러미"
            && harpyPreview.Receipt.OperationId
                == "species-harpy-gale:host:frozen"
            && harpyPreview.Receipt.DestinationStackId
                == "stack:species-host:frozen:destination"
            && harpyPreview.OwnerRevision == 13L,
            "Species action mapping changed native keys, names, or payload values.");

        using (var missing = new DomainFactDispatcher())
        {
            var missingProposal = new GolemUpkeepFact(
                CreateGolemUpkeepFactData("missing", applied: false),
                proposed: true);
            Require(!missing.TryReserve(
                    missingProposal,
                    out _,
                    out string missingFailure)
                && missingFailure.Contains(
                    "domain-fact-observer-missing",
                    StringComparison.Ordinal),
                "Species action reserve accepted a missing durable observer.");
        }

        using (var exact = new SpeciesActionAuthority())
        {
            var actualGolem = new GolemUpkeepFact(
                CreateGolemUpkeepFactData("exact", applied: true));
            var proposedGolem = new GolemUpkeepFact(
                CreateGolemUpkeepFactData("exact", applied: false),
                proposed: true);
            Require(exact.Dispatcher.TryReserve(
                    proposedGolem,
                    out DomainFactLease golemLease,
                    out string failure)
                && exact.Dispatcher.Publish(golemLease, actualGolem).Completed,
                "Golem action actual publication failed: " + failure);

            var actualIncident = new SpeciesIncidentFact(
                CreateSpeciesIncidentFactData("exact", applied: true));
            var proposedIncident = new SpeciesIncidentFact(
                CreateSpeciesIncidentFactData("exact", applied: false),
                proposed: true);
            Require(exact.Dispatcher.TryReserve(
                    proposedIncident,
                    out DomainFactLease incidentLease,
                    out failure)
                && exact.Dispatcher.Publish(
                    incidentLease,
                    actualIncident).Completed,
                "Species incident actual publication failed: " + failure);

            var actualHarpy = new HarpyGaleFact(
                CreateHarpyGaleFactData("exact"));
            HarpyGaleFactSaveData harpySnapshot = actualHarpy.Snapshot();
            string expectedHarpyKey = SpeciesActionFactMapping
                .Capture(actualHarpy).Payload.ResultKey.ToString();
            GameplayOutcomeLedgerSaveData harpyReserveLedger =
                exact.Ledger.CaptureGameplayOutcomes();
            bool harpyReserved = exact.Dispatcher.TryReserve(
                actualHarpy,
                out DomainFactLease harpyLease,
                out failure);
            Require(harpyReserved,
                "Harpy action reservation failed: failure=" + failure
                    + ";expectedKey=" + expectedHarpyKey
                    + ";rowCount=" + OutcomeRowCount(harpyReserveLedger)
                    + ";snapshotKeys=["
                    + OutcomeKeysForDiagnostics(harpyReserveLedger) + "]"
                    + ";operationId=" + harpySnapshot.operationId
                    + ";sourceStackId=" + harpySnapshot.sourceStackId
                    + ";destinationStackId="
                    + harpySnapshot.destinationStackId
                    + ";itemId=" + harpySnapshot.itemId
                    + ";itemInstanceId=" + harpySnapshot.itemInstanceId);
            string reservedHarpyKey = harpyLease.PublicationKey;
            Require(reservedHarpyKey == expectedHarpyKey,
                "Harpy action reservation key changed: reservedKey="
                    + reservedHarpyKey + ";expectedKey=" + expectedHarpyKey);
            DomainFactDelivery harpyDelivery = exact.Dispatcher.Publish(
                harpyLease,
                actualHarpy);
            GameplayOutcomeLedgerSaveData harpyPublishedLedger =
                exact.Ledger.CaptureGameplayOutcomes();
            string harpyPublicationDiagnostics =
                "phase=" + harpyDelivery.Phase
                + ";detail=" + harpyDelivery.Detail
                + ";operationKey=" + harpyDelivery.OperationKey
                + ";publicationId=" + harpyDelivery.PublicationId
                + ";fingerprint=" + harpyDelivery.Fingerprint
                + ";reservedKey=" + reservedHarpyKey
                + ";expectedKey=" + expectedHarpyKey
                + ";rowCount=" + OutcomeRowCount(harpyPublishedLedger)
                + ";snapshotKeys=["
                + OutcomeKeysForDiagnostics(harpyPublishedLedger) + "]"
                + ";operationId=" + harpySnapshot.operationId
                + ";sourceStackId=" + harpySnapshot.sourceStackId
                + ";destinationStackId=" + harpySnapshot.destinationStackId
                + ";itemId=" + harpySnapshot.itemId
                + ";itemInstanceId=" + harpySnapshot.itemInstanceId;
            Require(harpyDelivery.Completed,
                "Harpy action publication was not delivered: "
                    + harpyPublicationDiagnostics);
            Require(OutcomeRowCount(harpyPublishedLedger) == 3,
                "Harpy action publication ledger row count changed: "
                    + harpyPublicationDiagnostics);
        }

        SavedSpeciesActionAuthority saved;
        using (var ambiguous = new SpeciesActionAuthority())
        {
            var port = new SpeciesActionCommitThenInDoubtPublisher(
                ambiguous.Dispatcher);
            var proposed = new GolemUpkeepFact(
                CreateGolemUpkeepFactData("ambiguous", applied: false),
                proposed: true);
            var actual = new GolemUpkeepFact(
                CreateGolemUpkeepFactData("ambiguous", applied: true));
            Require(port.TryReserve(
                    proposed,
                    out DomainFactLease lease,
                    out string failure),
                "Ambiguous species action did not reserve: " + failure);
            string publicationKey = lease.PublicationKey;
            port.HideNextCompletedReply = true;
            DomainFactDelivery delivered = port.Publish(lease, actual);
            Require(delivered.Phase == DomainFactDeliveryPhase.InDoubt
                && OutcomeRowCount(
                    ambiguous.Ledger.CaptureGameplayOutcomes()) == 1,
                "Species action commit-then-in-doubt was not retained canonically.");
            saved = JsonConvert.DeserializeObject<SavedSpeciesActionAuthority>(
                JsonConvert.SerializeObject(new SavedSpeciesActionAuthority
                {
                    serializer = HostSerializer,
                    species = new CharacterSpeciesRuntimeSaveData
                    {
                        version = CharacterSpeciesRuntimeSaveData.CurrentVersion,
                        characters = new List<
                            CharacterSpeciesRuntimeRecordSaveData>
                        {
                            CreateGolemSpeciesOwner(actual.Snapshot())
                        },
                        pendingObservations = new List<
                            SpeciesActionObservationSaveData>
                        {
                            new()
                            {
                                actionType = 0,
                                publicationKey = publicationKey,
                                golem = actual.Snapshot()
                            }
                        }
                    },
                    ledger = ambiguous.Ledger.CaptureGameplayOutcomes()
                }));
        }

        Require(saved != null
            && saved.serializer == HostSerializer
            && saved.species.pendingObservations.Count == 1,
            "Species action pending DTO did not survive host JSON restore.");
        saved.species.pendingObservations[0].Validate();
        Require(ValidateSpeciesActionCanonical(
                saved.species,
                saved.ledger).Success,
            "Species action delivered pending row failed canonical preflight.");
        SavedSpeciesActionAuthority renamed =
            CloneSpeciesActionAuthority(saved);
        renamed.species.pendingObservations[0]
            .golem.characterDisplayName += " 변조";
        Require(!ValidateSpeciesActionCanonical(
                renamed.species,
                renamed.ledger).Success,
            "Species action preflight accepted a changed frozen name.");
        SavedSpeciesActionAuthority hashChanged =
            CloneSpeciesActionAuthority(saved);
        GameplayOutcomeSnapshot hashRow = hashChanged.ledger.outbox
            .Concat(hashChanged.ledger.exactOutcomes)
            .Single();
        hashRow.immutablePayloadHash = hashRow.immutablePayloadHash[0] == '0'
            ? "1" + hashRow.immutablePayloadHash.Substring(1)
            : "0" + hashRow.immutablePayloadHash.Substring(1);
        Require(!ValidateSpeciesActionCanonical(
                hashChanged.species,
                hashChanged.ledger).Success,
            "Species action preflight accepted a changed canonical hash.");
        using var restored = new SpeciesActionAuthority();
        restored.RestoreLedger(saved.ledger);
        IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
            restored.Ledger.CaptureGameplayOutcomes());
        SpeciesActionObservationSaveData pending =
            saved.species.pendingObservations.Single();
        var replies = new DomainFactDelivery[1];
        Require(restored.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        pending.publicationKey,
                        new GolemUpkeepFact(pending.golem))
                },
                replies,
                out string replayFailure)
            && replies[0].Completed
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1,
            "Species action exact recovery did not reconcile once: "
                + replayFailure);
        saved.species.pendingObservations.Clear();

        GolemUpkeepFactSaveData altered = pending.golem.Clone();
        altered.chargeAfter += 1f;
        var rejected = new DomainFactDelivery[1];
        Require(!restored.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        pending.publicationKey,
                        new GolemUpkeepFact(altered))
                },
                rejected,
                out replayFailure)
            && rejected[0].Phase == DomainFactDeliveryPhase.Rejected
            && !string.IsNullOrWhiteSpace(replayFailure),
            "Species action replay accepted altered frozen evidence.");
        RequireCanonicalEqual(
            canonical,
            CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
            "Rejected species action replay changed canonical hashes.");
    }

    private static GolemUpkeepFactSaveData CreateGolemUpkeepFactData(
        string suffix,
        bool applied) => new()
    {
        operationId = "golem-recharge:host:" + suffix,
        characterId = "character:species-host:golem",
        characterDisplayName = "호스트 골렘",
        speciesId = "Golem",
        speciesDisplayName = "호스트 골렘 종족",
        revision = 7L,
        absoluteDay = 42,
        x = 3,
        y = 5,
        kind = "recharge",
        physicalEvidence = applied
            ? "owner=golem-recharge:host:" + suffix
                + "|stack=stack:species-host:charge"
                + "|item=item:golem-charge|requested=1|before=1|after=0"
            : string.Empty,
        chargeBefore = 20f,
        chargeAfter = applied ? 55f : 20f,
        integrityBefore = 90f,
        integrityAfter = 90f,
        healthBefore = 80f,
        healthAfter = 80f,
        completedWork = 3f
    };

    private static SpeciesIncidentFactSaveData CreateSpeciesIncidentFactData(
        string suffix,
        bool applied) => new()
    {
        operationId = "species-incident:host:" + suffix,
        characterId = "character:species-host:resident",
        characterDisplayName = "호스트 주민",
        speciesId = "Harpy",
        speciesDisplayName = "호스트 하피 종족",
        revision = 9L,
        absoluteDay = 42,
        x = 7,
        y = 11,
        incidentId = "species-incident:host-gale",
        summary = applied ? "호스트 종족 사건" : string.Empty
    };

    private static HarpyGaleFactSaveData CreateHarpyGaleFactData(
        string suffix) => new()
    {
        operationId = "species-harpy-gale:host:" + suffix,
        characterId = "character:species-host:harpy",
        characterDisplayName = "호스트 하피",
        speciesId = "Harpy",
        speciesDisplayName = "호스트 하피 종족",
        revision = 11L,
        absoluteDay = 42,
        x = 13,
        y = 17,
        sourceStackId = "stack:species-host:" + suffix + ":source",
        destinationStackId = "stack:species-host:" + suffix + ":destination",
        itemId = "item:species-host:feather-bundle",
        itemInstanceId = string.Empty,
        itemDisplayName = "호스트 깃털 꾸러미",
        reasonCode = "species-harpy-gale-relocation",
        quantity = 1,
        sourceX = 12,
        sourceY = 17,
        destinationX = 13,
        destinationY = 17,
        massGrams = 250L,
        physicalRevision = 13L
    };

    private static void VerifyWorkAccidentFactObservation()
    {
        WorkAccidentFactSaveData source = CreateWorkAccidentFactData(1);
        var fact = new WorkAccidentFact(source);
        source.workerDisplayName = "변조된 작업자";
        source.appliedDamage = 99f;
        WorkAccidentFactSaveData detached = fact.Snapshot();
        detached.facilityDisplayName = "변조된 작업장";
        detached.fireTargetDisplayName = "변조된 화재 대상";
        Require(fact.Snapshot().workerDisplayName == "호스트 작업자"
            && fact.Snapshot().facilityDisplayName == "호스트 제련소"
            && fact.Snapshot().appliedDamage == 3.5f
            && fact.Snapshot().fireTargetDisplayName == "호스트 제련소",
            "Work-accident fact retained mutable DTO state.");

        ProcessAccidentOutcomeReceipt receipt =
            WorkAccidentFactMapping.Capture(fact);
        string expectedKey = new GameplayResultKey(
            EnvironmentOutcomeIds.ProcessAccidentProducer,
            new GameplayOperationId("work-accident-operation:00000001"),
            0L,
            0).ToString();
        GameplayOutcomeMetric damage = receipt.Payload.Metrics.Get(0);
        Require(receipt.Payload.ResultKey.ToString() == expectedKey
            && receipt.Payload.Participants.Count == 2
            && receipt.Payload.Participants.Get(0)
                .DisplayName.DisplayText == "호스트 작업자"
            && receipt.Payload.Participants.Get(1)
                .DisplayName.DisplayText == "호스트 제련소"
            && receipt.Payload.Metrics.Count == 1
            && damage.MetricId.Equals(EnvironmentOutcomeIds.DamageMetric)
            && damage.Value == 3.5d
            && SpeciesPayloadFact(
                    receipt.Payload,
                    EnvironmentOutcomeIds.WorkTypeFact)
                == "work-type:host:smithing"
            && SpeciesPayloadFact(
                    receipt.Payload,
                    EnvironmentOutcomeIds.AnatomyNodeFact)
                == "anatomy-node:host:right-arm"
            && SpeciesPayloadFact(
                    receipt.Payload,
                    EnvironmentOutcomeIds.ReasonFact)
                == "호스트 작업 위험 관측"
            && SpeciesPayloadFact(
                    receipt.Payload,
                    EnvironmentOutcomeIds.ProcessRiskMultiplierFact)
                == "0.75"
            && SpeciesPayloadFact(
                    receipt.Payload,
                    EnvironmentOutcomeIds.ProcessCommandOriginFact)
                == ((int)CharacterCommandOrigin.DirectPlayerOrder)
                    .ToString(CultureInfo.InvariantCulture)
            && SpeciesPayloadFact(
                    receipt.Payload,
                    EnvironmentOutcomeIds.ProcessFireEligibleFact)
                == "true"
            && SpeciesPayloadFact(
                    receipt.Payload,
                    EnvironmentOutcomeIds.ProcessFireIntensityFact)
                == "0.35"
            && SpeciesPayloadFact(
                    receipt.Payload,
                    EnvironmentOutcomeIds.ProcessFireTargetNameFact)
                == "호스트 제련소",
            "Work-accident mapping changed the native key, names, or damage.");

        using (var missing = new DomainFactDispatcher())
        {
            Require(!missing.TryReserve(
                    fact,
                    out _,
                    out string missingFailure)
                && missingFailure.Contains(
                    "domain-fact-observer-missing",
                    StringComparison.Ordinal),
                "Work-accident reserve accepted a missing durable observer.");
        }

        SavedWorkAccidentAuthority exactSaved;
        using (var exact = new WorkAccidentAuthority())
        {
            Require(exact.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Work-accident exact fact did not reserve: " + failure);
            Require(lease.PublicationKey == expectedKey,
                "Work-accident reservation changed the native result key.");
            string reservedKey = lease.PublicationKey;
            DomainFactDelivery delivered = exact.Dispatcher.Publish(
                lease,
                fact);
            Require(delivered.Completed
                && delivered.OperationKey == expectedKey
                && OutcomeRowCount(
                    exact.Ledger.CaptureGameplayOutcomes()) == 1,
                "Work-accident exact fact did not publish once: "
                    + delivered.Phase + ":" + delivered.Detail);
            exactSaved = JsonConvert.DeserializeObject<
                SavedWorkAccidentAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedWorkAccidentAuthority
                        {
                            serializer = HostSerializer,
                            fact = fact.Snapshot(),
                            publicationKey = reservedKey,
                            ledger = exact.Ledger.CaptureGameplayOutcomes()
                        }))
                ?? throw new InvalidOperationException(
                    "Work-accident exact host JSON restore returned null.");
        }

        Require(exactSaved.serializer == HostSerializer
            && exactSaved.publicationKey == expectedKey
            && exactSaved.fact.workerDisplayName == "호스트 작업자",
            "Work-accident frozen DTO did not survive host JSON restore.");
        var workOrders = new DungeonWorkOrderSaveData
        {
            version = DungeonWorkOrderSaveData.CurrentVersion,
            nextOrderSequence = 2,
            pendingAccidents = new List<WorkAccidentObservationSaveData>
            {
                new()
                {
                    fact = exactSaved.fact.Clone(),
                    publicationKey = exactSaved.publicationKey,
                    observationCompleted = true
                }
            }
        };
        WorkAccidentObservationSaveData.ValidateAll(workOrders);
        Require(ValidateWorkAccidentCanonical(
                workOrders,
                exactSaved.ledger).Success,
            "Work-accident delivered pending row failed canonical preflight.");
        DungeonWorkOrderSaveData changedFirePlan =
            JsonConvert.DeserializeObject<DungeonWorkOrderSaveData>(
                JsonConvert.SerializeObject(workOrders))
            ?? throw new InvalidOperationException(
                "Work-accident preflight clone returned null.");
        changedFirePlan.pendingAccidents[0].fact.fireIntensity = 0.5f;
        Require(!ValidateWorkAccidentCanonical(
                changedFirePlan,
                exactSaved.ledger).Success,
            "Work-accident preflight accepted changed fire intensity.");
        string strictPayload = JsonConvert.SerializeObject(workOrders);
        ValidateWorkAccidentRaw(strictPayload);
        var strictRoot = Newtonsoft.Json.Linq.JObject.Parse(strictPayload);
        var strictRows = (Newtonsoft.Json.Linq.JArray)
            strictRoot["pendingAccidents"];
        var strictRow = (Newtonsoft.Json.Linq.JObject)strictRows[0];
        var strictFact = (Newtonsoft.Json.Linq.JObject)strictRow["fact"];
        strictFact.Property("accidentMultiplier")?.Remove();
        RequireThrows<InvalidOperationException>(
            () => ValidateWorkAccidentRaw(
                strictRoot.ToString(Formatting.None)),
            "Work-accident strict parser accepted a missing risk field.");
        using (var restored = new WorkAccidentAuthority())
        {
            restored.RestoreLedger(exactSaved.ledger);
            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                restored.Ledger.CaptureGameplayOutcomes());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var replies = new DomainFactDelivery[1];
                Require(restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                exactSaved.publicationKey,
                                new WorkAccidentFact(exactSaved.fact))
                        },
                        replies,
                        out string exactReplayFailure)
                    && replies[0].Completed
                    && OutcomeRowCount(
                        restored.Ledger.CaptureGameplayOutcomes()) == 1,
                    "Work-accident exact replay attempt " + attempt
                        + " did not reconcile once: " + exactReplayFailure);
            }
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Duplicate work-accident replay changed canonical evidence.");

            WorkAccidentFactSaveData renamed = exactSaved.fact.Clone();
            renamed.workerDisplayName = "변조된 작업자";
            RequireTamperRejected(renamed, "changed frozen name");
            WorkAccidentFactSaveData firePlanChanged =
                exactSaved.fact.Clone();
            firePlanChanged.fireIntensity = 0.5f;
            RequireTamperRejected(
                firePlanChanged,
                "changed frozen fire plan");
            WorkAccidentFactSaveData multiplierChanged =
                exactSaved.fact.Clone();
            multiplierChanged.accidentMultiplier = 0.9f;
            RequireTamperRejected(
                multiplierChanged,
                "changed frozen accident multiplier");

            void RequireTamperRejected(
                WorkAccidentFactSaveData changed,
                string label)
            {
                var rejected = new DomainFactDelivery[1];
                Require(!restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                exactSaved.publicationKey,
                                new WorkAccidentFact(changed))
                        },
                        rejected,
                        out string changedReplayFailure)
                    && rejected[0].Phase
                        == DomainFactDeliveryPhase.Rejected
                    && !string.IsNullOrWhiteSpace(changedReplayFailure),
                    "Work-accident replay accepted a " + label + ".");
                RequireCanonicalEqual(
                    canonical,
                    CanonicalHashes(
                        restored.Ledger.CaptureGameplayOutcomes()),
                    "Rejected work-accident " + label
                        + " replay changed canonical evidence.");
            }
        }

        SavedWorkAccidentAuthority uncertainSaved;
        using (var uncertain = new WorkAccidentAuthority())
        {
            var publisher = new SpeciesActionCommitThenInDoubtPublisher(
                uncertain.Dispatcher);
            var uncertainFact = new WorkAccidentFact(
                CreateWorkAccidentFactData(2));
            Require(publisher.TryReserve(
                    uncertainFact,
                    out DomainFactLease lease,
                    out string failure),
                "Work-accident uncertain fact did not reserve: " + failure);
            string reservedKey = lease.PublicationKey;
            publisher.HideNextCompletedReply = true;
            DomainFactDelivery delivery = publisher.Publish(
                lease,
                uncertainFact);
            Require(delivery.Phase == DomainFactDeliveryPhase.InDoubt
                && OutcomeRowCount(
                    uncertain.Ledger.CaptureGameplayOutcomes()) == 1,
                "Work-accident hidden commit response lost canonical evidence.");
            uncertainSaved = JsonConvert.DeserializeObject<
                SavedWorkAccidentAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedWorkAccidentAuthority
                        {
                            serializer = HostSerializer,
                            fact = uncertainFact.Snapshot(),
                            publicationKey = reservedKey,
                            ledger = uncertain.Ledger.CaptureGameplayOutcomes()
                        }))
                ?? throw new InvalidOperationException(
                    "Work-accident uncertain host JSON restore returned null.");
        }

        using var recovered = new WorkAccidentAuthority();
        recovered.RestoreLedger(uncertainSaved.ledger);
        IReadOnlyDictionary<string, string> uncertainCanonical =
            CanonicalHashes(recovered.Ledger.CaptureGameplayOutcomes());
        var recoveredReply = new DomainFactDelivery[1];
        Require(recovered.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        uncertainSaved.publicationKey,
                        new WorkAccidentFact(uncertainSaved.fact))
                },
                recoveredReply,
                out string recoveryFailure)
            && recoveredReply[0].Completed
            && OutcomeRowCount(
                recovered.Ledger.CaptureGameplayOutcomes()) == 1,
            "Work-accident in-doubt recovery did not reconcile once: "
                + recoveryFailure);
        RequireCanonicalEqual(
            uncertainCanonical,
            CanonicalHashes(recovered.Ledger.CaptureGameplayOutcomes()),
            "Work-accident in-doubt recovery duplicated canonical evidence.");
    }

    private static WorkAccidentFactSaveData CreateWorkAccidentFactData(
        int sequence) => new()
    {
        operationId = "work-accident-operation:"
            + sequence.ToString("D8", CultureInfo.InvariantCulture),
        workerId = "character:work-accident-host:worker",
        workerDisplayName = "호스트 작업자",
        facilityId = "building:work-accident-host:forge",
        facilityDisplayName = "호스트 제련소",
        workTypeId = "work-type:host:smithing",
        anatomyNodeId = "anatomy-node:host:right-arm",
        observedCause = "호스트 작업 위험 관측",
        appliedDamage = 3.5f,
        accidentMultiplier = 0.75f,
        absoluteDay = 42,
        x = 7,
        y = 9,
        commandOrigin = (int)CharacterCommandOrigin.DirectPlayerOrder,
        fireEligible = true,
        fireIntensity = 0.35f,
        fireTargetDisplayName = "호스트 제련소"
    };

    private static void VerifyRepairWorkCompletedFactObservation()
    {
        var covered = new HashSet<RepairWorkOutcomeKind>();
        foreach (RepairWorkOutcomeKind kind in Enum.GetValues(
                     typeof(RepairWorkOutcomeKind)))
        {
            RepairWorkOutcomeSaveData owner = CreateOwner(
                kind,
                (int)kind + 1);
            var fact = new RepairWorkCompletedFact(owner);
            MigratedDomainFactCapture capture =
                RepairWorkCompletedFactMapping.Capture(fact);
            MigratedProducerOutcomeDetails details = capture.Details;
            GameplayMetricUnitId primaryUnit = kind switch
            {
                RepairWorkOutcomeKind.GeneralDamage =>
                    MigratedProducerOutcomeIds.EnumUnit,
                RepairWorkOutcomeKind.EquipmentDurability =>
                    MigratedProducerOutcomeIds.RatioUnit,
                _ => MigratedProducerOutcomeIds.PointUnit
            };
            GameplayEntityKindId targetKind = kind
                    == RepairWorkOutcomeKind.EquipmentDurability
                ? MigratedProducerOutcomeIds.ItemKind
                : MigratedProducerOutcomeIds.FacilityKind;
            int materialQuantity = kind
                    == RepairWorkOutcomeKind.EquipmentDurability
                ? 5
                : 0;
            long materialMass = kind
                    == RepairWorkOutcomeKind.EquipmentDurability
                ? 2400L
                : 0L;
            string stableKind = StableKind(kind);
            Dictionary<string, string> facts = details.Facts.ToDictionary(
                value => value.FactId.Value,
                value => value.Value,
                StringComparer.Ordinal);

            Require(covered.Add(kind)
                && capture.Kind == MigratedProducerOutcomeKind.RepairWorkResult
                && capture.OwnerIdentity == owner.operationId
                && capture.AbsoluteDay == owner.completedAbsoluteDay
                && capture.Status == GameplayOutcomeStatus.Succeeded
                && !capture.ReservationOnly
                && details.Subjects.Length == 3
                && details.Subjects[0].EntityId.Kind.Equals(
                    MigratedProducerOutcomeIds.FacilityKind)
                && details.Subjects[0].EntityId.Value == owner.facilityId
                && details.Subjects[0].Role.Equals(
                    MigratedProducerOutcomeIds.FacilityRole)
                && details.Subjects[1].EntityId.Kind.Equals(targetKind)
                && details.Subjects[1].EntityId.Value == owner.targetEntityId
                && details.Subjects[1].Role.Equals(
                    MigratedProducerOutcomeIds.TargetRole)
                && details.Subjects[2].EntityId.Kind.Equals(
                    MigratedProducerOutcomeIds.CharacterKind)
                && details.Subjects[2].EntityId.Value
                    == "character:repair-host:worker-b"
                && details.Subjects[2].Role.Equals(
                    MigratedProducerOutcomeIds.ActorRole)
                && details.Metrics.Length == 6
                && details.Facts.Length == 14
                && details.Provenance.Length == 1
                && details.Provenance[0].KindId
                    == "repair-context-sha256"
                && details.Provenance[0].Value
                    == RepairWorkCompletedFactMapping.ComputeContextSha256(
                        owner)
                && details.Provenance[0].Value.Length == 71
                && details.Provenance[0].Value.StartsWith("sha256:", StringComparison.Ordinal)
                && details.Provenance[0].Value.Skip(7).All(Uri.IsHexDigit)
                && details.Summary == stableKind
                    + " 수리가 실제 상태 변경과 함께 완료되었다.",
                "Repair-work mapping changed its exact subjects, identity, or context: "
                    + kind);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.PreviousMetric,
                owner.primaryBefore,
                primaryUnit,
                targetKind,
                owner.targetEntityId);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.CurrentMetric,
                owner.primaryAfter,
                primaryUnit,
                targetKind,
                owner.targetEntityId);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.DeltaMetric,
                owner.primaryAfter - owner.primaryBefore,
                primaryUnit,
                targetKind,
                owner.targetEntityId);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.CompletedWorkMetric,
                7000d,
                MigratedProducerOutcomeIds.MilliWorkUnit,
                targetKind,
                owner.targetEntityId);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.InputQuantityMetric,
                materialQuantity,
                MigratedProducerOutcomeIds.CountUnit,
                targetKind,
                owner.targetEntityId);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.InputMassMetric,
                materialMass,
                MigratedProducerOutcomeIds.GramUnit,
                targetKind,
                owner.targetEntityId);
            Require(facts.Count == 14
                && facts[MigratedProducerOutcomeIds.OperationFact.Value]
                    == owner.operationId
                && facts[MigratedProducerOutcomeIds.FacilityFact.Value]
                    == owner.facilityId
                && facts["repair-kind"] == stableKind
                && facts["repair-target-id"] == owner.targetEntityId
                && facts["repair-target-definition-id"]
                    == (owner.targetDefinitionId.Length == 0
                        ? "none"
                        : owner.targetDefinitionId)
                && facts[MigratedProducerOutcomeIds.BeforeFact.Value]
                    == CanonicalState(owner, before: true)
                && facts[MigratedProducerOutcomeIds.AfterFact.Value]
                    == CanonicalState(owner, before: false)
                && facts[MigratedProducerOutcomeIds.ContributorsFact.Value]
                    == "character:repair-host:worker-a=3,character:repair-host:worker-b=4"
                && facts["repair-completion-worker"]
                    == owner.completionWorkerId
                && facts["repair-completion-origin"]
                    == WorkCompletionIdentityOutcomeIds.StableOrigin(
                        owner.completionOrigin)
                && facts["repair-completion-work"] == "2.5"
                && facts[MigratedProducerOutcomeIds.MaterialInputOperationFact.Value]
                    == (materialQuantity == 0
                        ? "none"
                        : owner.materialOperationId)
                && facts[MigratedProducerOutcomeIds.BatchFact.Value]
                    == (materialQuantity == 0
                        ? "none"
                        : owner.materialCommitId)
                && facts["repair-material-sources"]
                    == (materialQuantity == 0
                        ? "none"
                        : "stack:repair-host:a=item:repair-host:part-a=2,stack:repair-host:b=item:repair-host:part-b=3"),
                "Repair-work mapping changed its legacy facts: " + kind);
        }
        Require(covered.SetEquals(
                Enum.GetValues(typeof(RepairWorkOutcomeKind))
                    .Cast<RepairWorkOutcomeKind>()),
            "Repair-work mapping did not cover every outcome kind.");

        RepairWorkOutcomeSaveData aliased = CreateOwner(
            RepairWorkOutcomeKind.EquipmentDurability,
            6);
        aliased.publicationKey = "repair-publication:host:alias";
        aliased.publicationConfirmed = true;
        aliased.completionPublicationKey = "repair-completion:host:alias";
        string frozenHash =
            RepairWorkCompletedFactMapping.ComputeContextSha256(aliased);
        var frozenFact = new RepairWorkCompletedFact(aliased);
        aliased.primaryBefore = 0.99f;
        aliased.contributions[0].relevantSkill = 99f;
        aliased.materialSources[0].quantity = 99;
        RepairWorkOutcomeSaveData detached = frozenFact.Snapshot();
        detached.primaryAfter = 0.01f;
        detached.contributions[1].contributedWork = 99f;
        detached.materialSources[1].itemId = "item:repair-host:mutated";
        RepairWorkOutcomeSaveData stillFrozen = frozenFact.Snapshot();
        Require(stillFrozen.primaryBefore == 0.25f
            && stillFrozen.primaryAfter == 0.9f
            && stillFrozen.contributions[0].relevantSkill == 2f
            && stillFrozen.contributions[1].contributedWork == 4f
            && stillFrozen.materialSources[0].quantity == 2
            && stillFrozen.materialSources[1].itemId
                == "item:repair-host:part-b"
            && stillFrozen.publicationKey.Length == 0
            && !stillFrozen.publicationConfirmed
            && stillFrozen.completionPublicationKey.Length == 0
            && RepairWorkCompletedFactMapping.Capture(frozenFact)
                .Details.Provenance.Single().Value == frozenHash,
            "Repair-work fact retained source/snapshot aliases or journal metadata.");

        RepairWorkOutcomeSaveData confirmed = CreateOwner(
            RepairWorkOutcomeKind.StructuralIntegrity,
            7);
        string parentKey = RepairWorkCompletionObservation.ResultKey(
            confirmed.operationId).ToString();
        confirmed.publicationKey = parentKey;
        confirmed.publicationConfirmed = true;
        var completionFact = new RepairWorkCompletionFact(confirmed);
        WorkCompletionIdentityDeliveryRequest completionRequest =
            RepairWorkCompletionObservation.CreateCompletionRequest(
                completionFact);
        Require(completionFact.ParentPublicationKey == parentKey
            && completionFact.Snapshot().publicationKey.Length == 0
            && !completionFact.Snapshot().publicationConfirmed
            && completionRequest.Character.Value == confirmed.completionWorkerId
            && completionRequest.WorkId == BuiltInWorkTypeIds.Repair.Value
            && completionRequest.OperationId == confirmed.operationId
            && completionRequest.TerminalOwnerId
                == RepairWorkCompletionFact.TerminalOwnerId
            && completionRequest.ChildResultKey == parentKey
            && completionRequest.TargetEntityId == confirmed.targetEntityId
            && completionRequest.ContributionWorkUnits
                == confirmed.completionContributionWork
            && RepairWorkCompletionObservation.RequireCompletionChild(
                    completionRequest).ToString() == parentKey,
            "Repair completion fact lost its confirmed parent or request identity.");
        RequireThrows<ArgumentException>(
            () => new RepairWorkCompletionFact(CreateOwner(
                RepairWorkOutcomeKind.StructuralIntegrity,
                8)),
            "Repair completion fact accepted an unconfirmed child publication.");

        SavedRepairWorkFactAuthority saved;
        using (var uncertain = new Authority(
                   connect: false,
                   recorderFaults: true))
        {
            var binding = new MigratedDomainFactObserverBinding<
                RepairWorkCompletedFact>(
                "log:repair-work-completed",
                RepairWorkCompletedFactMapping.Capture,
                uncertain.Migrated,
                new GameplayDomainFactCommitGroup(
                    uncertain.Recorder,
                    uncertain.Ledger));
            using IDisposable connection = binding.Connect(
                uncertain.Dispatcher);
            RepairWorkOutcomeSaveData owner = CreateOwner(
                RepairWorkOutcomeKind.EquipmentDurability,
                9);
            var fact = new RepairWorkCompletedFact(owner);
            string contextHash =
                RepairWorkCompletedFactMapping.ComputeContextSha256(owner);
            Require(uncertain.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Repair-work fact did not reserve: " + failure);
            string publicationKey = lease.PublicationKey;
            owner.publicationKey = publicationKey;
            uncertain.Faults.ThrowAfterNextSingleCommit = true;
            DomainFactDelivery attempt = uncertain.Dispatcher.Publish(
                lease,
                fact);
            Require(attempt.MayBeCommitted
                && uncertain.Faults.AfterCommitFaultCount == 1
                && publicationKey == RepairWorkCompletionObservation.ResultKey(
                    owner.operationId).ToString()
                && OutcomeRowCount(
                    uncertain.Ledger.CaptureGameplayOutcomes()) == 1
                && uncertain.Migrated.Capture().owners.Count == 1,
                "Repair-work post-commit unknown acknowledgement lost canonical evidence.");
            saved = JsonConvert.DeserializeObject<
                SavedRepairWorkFactAuthority>(
                JsonConvert.SerializeObject(
                    new SavedRepairWorkFactAuthority
                    {
                        serializer = HostSerializer,
                        owner = owner,
                        publicationKey = publicationKey,
                        contextHash = contextHash,
                        ledger = uncertain.Ledger.CaptureGameplayOutcomes(),
                        migrated = uncertain.Migrated.Capture()
                    }))
                ?? throw new InvalidOperationException(
                    "Repair-work host JSON round-trip returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.owner.publicationKey == saved.publicationKey
            && !saved.owner.publicationConfirmed
            && saved.owner.completionPublicationKey.Length == 0
            && RepairWorkCompletedFactMapping.ComputeContextSha256(
                saved.owner) == saved.contextHash,
            "Repair-work unknown acknowledgement did not retain its exact owner journal.");
        var rawWorkOrders = new DungeonWorkOrderSaveData
        {
            version = DungeonWorkOrderSaveData.CurrentVersion,
            nextOrderSequence = 1,
            repairOutcomes = new List<RepairWorkOutcomeSaveData>
            {
                saved.owner.Clone()
            }
        };
        string rawPayload = JsonConvert.SerializeObject(rawWorkOrders);
        ValidateWorkAccidentRaw(rawPayload);
        var rawRoot = Newtonsoft.Json.Linq.JObject.Parse(rawPayload);
        var rawRepairs = (Newtonsoft.Json.Linq.JArray)
            rawRoot["repairOutcomes"];
        var rawRepair = (Newtonsoft.Json.Linq.JObject)rawRepairs[0];
        rawRepair.Property("completionPublicationKey")?.Remove();
        RequireThrows<InvalidOperationException>(
            () => ValidateWorkAccidentRaw(
                rawRoot.ToString(Formatting.None)),
            "Repair-work strict parser accepted missing completion publication metadata.");
        RepairWorkCompletionObservation.ValidateCompletionRestoreJoin(
            new[] { saved.owner },
            Array.Empty<WorkCompletionIdentityDeliveryCursorSaveData>(),
            saved.ledger,
            saved.migrated);
        RepairWorkOutcomeSaveData changedJoinSkill = saved.owner.Clone();
        changedJoinSkill.contributions[0].relevantSkill += 1f;
        RequireThrows<InvalidOperationException>(
            () => RepairWorkCompletionObservation
                .ValidateCompletionRestoreJoin(
                    new[] { changedJoinSkill },
                    Array.Empty<
                        WorkCompletionIdentityDeliveryCursorSaveData>(),
                    saved.ledger,
                    saved.migrated),
            "Repair completion restore join accepted a changed relevant skill.");
        RepairWorkOutcomeSaveData changedJoinInitiator = saved.owner.Clone();
        changedJoinInitiator.initiatorCharacterId =
            "character:repair-host:initiator-changed";
        RequireThrows<InvalidOperationException>(
            () => RepairWorkCompletionObservation
                .ValidateCompletionRestoreJoin(
                    new[] { changedJoinInitiator },
                    Array.Empty<
                        WorkCompletionIdentityDeliveryCursorSaveData>(),
                    saved.ledger,
                    saved.migrated),
            "Repair completion restore join accepted a changed initiator.");
        RepairWorkOutcomeSaveData journalChanged = saved.owner.Clone();
        journalChanged.publicationConfirmed = true;
        journalChanged.completionPublicationKey =
            "repair-completion:host:journal-only";
        Require(RepairWorkCompletedFactMapping.ComputeContextSha256(
                journalChanged) == saved.contextHash,
            "Repair-work context hash included retry-only publication metadata.");

        using (var restored = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            restored.Restore(saved.ledger, saved.migrated);
            var binding = new MigratedDomainFactObserverBinding<
                RepairWorkCompletedFact>(
                "log:repair-work-completed",
                RepairWorkCompletedFactMapping.Capture,
                restored.Migrated,
                new GameplayDomainFactCommitGroup(
                    restored.Recorder,
                    restored.Ledger));
            using IDisposable connection = binding.Connect(
                restored.Dispatcher);
            var replayFact = new RepairWorkCompletedFact(saved.owner);
            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                restored.Ledger.CaptureGameplayOutcomes());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var replies = new DomainFactDelivery[1];
                Require(restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.publicationKey,
                                replayFact)
                        },
                        replies,
                        out string failure)
                    && replies[0].Completed
                    && replies[0].OperationKey == saved.publicationKey
                    && RepairWorkCompletedFactMapping.Capture(replayFact)
                        .Details.Provenance.Single().Value
                        == saved.contextHash
                    && OutcomeRowCount(
                        restored.Ledger.CaptureGameplayOutcomes()) == 1
                    && restored.Migrated.Capture().owners.Count == 1,
                    "Repair-work exact recovery attempt " + attempt
                        + " did not reconcile once: " + failure);
            }
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Repair-work exact replay changed canonical evidence.");

            RequireTamperRejected(
                value => value.completionWorkerId =
                    "character:repair-host:worker-b",
                "completion worker");
            RequireTamperRejected(
                value => value.completionOrigin =
                    CharacterCommandOrigin.ScriptedForced,
                "completion origin");
            RequireTamperRejected(
                value => value.completionContributionWork = 1.5f,
                "completion contribution");
            RequireTamperRejected(
                value => value.materialSources[0].quantity++,
                "material source");
            RequireTamperRejected(
                value => value.initiatorCharacterId =
                    "character:repair-host:initiator-changed",
                "initiator");
            RequireTamperRejected(
                value => value.startedAbsoluteDay--,
                "started day");
            RequireTamperRejected(
                value => value.contributions[0].relevantSkill += 1f,
                "relevant skill");

            void RequireTamperRejected(
                Action<RepairWorkOutcomeSaveData> change,
                string label)
            {
                RepairWorkOutcomeSaveData changed = saved.owner.Clone();
                change(changed);
                Require(RepairWorkCompletedFactMapping.ComputeContextSha256(
                        changed) != saved.contextHash,
                    "Repair-work " + label
                        + " change did not alter its context hash.");
                var replies = new DomainFactDelivery[1];
                Require(!restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.publicationKey,
                                new RepairWorkCompletedFact(changed))
                        },
                        replies,
                        out string failure)
                    && replies[0].Phase == DomainFactDeliveryPhase.Rejected
                    && !string.IsNullOrWhiteSpace(failure),
                    "Repair-work replay accepted a changed " + label + ".");
                RequireCanonicalEqual(
                    canonical,
                    CanonicalHashes(
                        restored.Ledger.CaptureGameplayOutcomes()),
                    "Rejected repair-work " + label
                        + " replay changed canonical evidence.");
            }
        }

        static RepairWorkOutcomeSaveData CreateOwner(
            RepairWorkOutcomeKind kind,
            int sequence)
        {
            bool equipment = kind
                == RepairWorkOutcomeKind.EquipmentDurability;
            (float before, float after, RepairWorkPrimaryUnit unit) = kind switch
            {
                RepairWorkOutcomeKind.GeneralDamage =>
                    (1f, 0f, RepairWorkPrimaryUnit.Flag),
                RepairWorkOutcomeKind.StructuralIntegrity =>
                    (20f, 35f, RepairWorkPrimaryUnit.Point),
                RepairWorkOutcomeKind.DefenseFacility =>
                    (30f, 75f, RepairWorkPrimaryUnit.Percent),
                RepairWorkOutcomeKind.EquipmentDurability =>
                    (0.25f, 0.9f, RepairWorkPrimaryUnit.Ratio),
                RepairWorkOutcomeKind.AutomationMaintenance =>
                    (40f, 85f, RepairWorkPrimaryUnit.Percent),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    null)
            };
            string suffix = sequence.ToString("D8", CultureInfo.InvariantCulture);
            return new RepairWorkOutcomeSaveData
            {
                operationId = "repair-work:" + suffix,
                kind = kind,
                facilityId = "building:repair-host:forge-" + suffix,
                targetEntityId = (equipment
                    ? "item-instance:repair-host:equipment-"
                    : "building:repair-host:target-") + suffix,
                targetDefinitionId = equipment
                    ? string.Empty
                    : "building-definition:repair-host:" + suffix,
                initiatorCharacterId = "character:repair-host:initiator",
                startedAbsoluteDay = 6,
                completedAbsoluteDay = 7,
                boundsKnown = true,
                primaryUnit = unit,
                primaryBefore = before,
                primaryAfter = after,
                secondaryBefore = 5f,
                secondaryAfter = 0f,
                terminal = true,
                completionWorkerId = "character:repair-host:worker-a",
                completionOrigin = CharacterCommandOrigin.DirectPlayerOrder,
                completionContributionWork = 2.5f,
                contributions = new List<CraftContributionSaveData>
                {
                    new()
                    {
                        characterId = "character:repair-host:worker-a",
                        contributedWork = 3f,
                        relevantSkill = 2f
                    },
                    new()
                    {
                        characterId = "character:repair-host:worker-b",
                        contributedWork = 4f,
                        relevantSkill = 5f
                    }
                },
                materialOperationId = equipment
                    ? "repair-material:host:" + suffix
                    : string.Empty,
                materialCommitId = equipment
                    ? "material-commit:host:" + suffix
                    : string.Empty,
                materialMassGrams = equipment ? 2400L : 0L,
                materialSources = equipment
                    ? new List<RepairWorkMaterialSourceSaveData>
                    {
                        new()
                        {
                            stackId = "stack:repair-host:a",
                            itemId = "item:repair-host:part-a",
                            quantity = 2
                        },
                        new()
                        {
                            stackId = "stack:repair-host:b",
                            itemId = "item:repair-host:part-b",
                            quantity = 3
                        }
                    }
                    : new List<RepairWorkMaterialSourceSaveData>()
            };
        }

        static void RequireMetric(
            MigratedProducerOutcomeDetails details,
            GameplayMetricId metricId,
            double expectedValue,
            GameplayMetricUnitId expectedUnit,
            GameplayEntityKindId expectedTargetKind,
            string expectedTargetId)
        {
            GameplayOutcomeMetric metric = details.Metrics.Single(
                value => value.MetricId.Equals(metricId));
            Require(metric.Value == expectedValue
                && metric.UnitId.Equals(expectedUnit)
                && metric.DefinitionOrInstanceId.Kind.Equals(
                    expectedTargetKind)
                && metric.DefinitionOrInstanceId.Value == expectedTargetId,
                "Repair-work metric changed value, unit, or target: "
                    + metricId.Value);
        }

        static string CanonicalState(
            RepairWorkOutcomeSaveData owner,
            bool before) => "primary="
            + (before ? owner.primaryBefore : owner.primaryAfter).ToString(
                "R",
                CultureInfo.InvariantCulture)
            + ";secondary="
            + (before ? owner.secondaryBefore : owner.secondaryAfter).ToString(
                "R",
                CultureInfo.InvariantCulture);

        static string StableKind(RepairWorkOutcomeKind kind) => kind switch
        {
            RepairWorkOutcomeKind.GeneralDamage => "general-damage",
            RepairWorkOutcomeKind.StructuralIntegrity =>
                "structural-integrity",
            RepairWorkOutcomeKind.DefenseFacility => "defense-facility",
            RepairWorkOutcomeKind.EquipmentDurability =>
                "equipment-durability",
            RepairWorkOutcomeKind.AutomationMaintenance =>
                "automation-maintenance",
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, null)
        };
    }

    private static void VerifyConstructionWorkEndedFactObservation()
    {
        foreach (WorkOrderTerminalKind kind in new[]
                 {
                     WorkOrderTerminalKind.Completed,
                     WorkOrderTerminalKind.Cancelled
                 })
        {
            ConstructionWorkEndedData data = CreateData(
                kind,
                kind == WorkOrderTerminalKind.Completed ? 1 : 2);
            MigratedDomainFactCapture capture =
                ConstructionWorkEndedFactMapping.Capture(
                    new ConstructionWorkEndedFact(data));
            MigratedProducerOutcomeDetails details = capture.Details;
            string terminal = kind == WorkOrderTerminalKind.Completed
                ? "completed"
                : "cancelled";
            string subjectId = kind == WorkOrderTerminalKind.Completed
                ? data.finalFacilityId
                : data.constructionSitePersistentId;
            var facts = details.Facts.ToDictionary(
                value => value.FactId.Value,
                value => value.Value,
                StringComparer.Ordinal);
            Require(capture.Kind
                    == MigratedProducerOutcomeKind.WorkOrderTerminalResult
                && capture.OwnerIdentity
                    == "work-order-terminal:" + data.workOrderId
                && capture.AbsoluteDay == data.absoluteDay
                && capture.Status == GameplayOutcomeStatus.Succeeded
                && !capture.ReservationOnly
                && details.Subjects.Length == 1
                && details.Subjects[0].EntityId.Kind.Equals(
                    MigratedProducerOutcomeIds.FacilityKind)
                && details.Subjects[0].EntityId.Value == subjectId
                && details.Subjects[0].DisplayName.DisplayText
                    == data.buildingDisplayName
                && details.Subjects[0].Role.Equals(
                    MigratedProducerOutcomeIds.FacilityRole)
                && details.Metrics.Length == 2
                && details.Facts.Length == 14
                && details.Provenance.Length == 1
                && details.Provenance[0].KindId
                    == "construction-context-sha256"
                && details.Provenance[0].Value
                    == ConstructionWorkEndedFactMapping
                        .ComputeContextSha256(data)
                && details.Provenance[0].Value.Length == 71
                && details.Provenance[0].Value.StartsWith("sha256:", StringComparison.Ordinal)
                && details.Provenance[0].Value.Substring(7).All(Uri.IsHexDigit)
                && details.Summary == data.buildingDisplayName
                    + (kind == WorkOrderTerminalKind.Completed
                        ? " 공사가 완료되었다."
                        : " 공사가 취소되었다."),
                "Construction terminal mapping changed its exact identity, subject, or digest: "
                    + kind);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.RequiredWorkMetric,
                10000d,
                subjectId);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.CompletedWorkMetric,
                kind == WorkOrderTerminalKind.Completed ? 10000d : 4000d,
                subjectId);
            Require(facts.Count == 14
                && facts[MigratedProducerOutcomeIds.WorkOrderFact.Value]
                    == data.workOrderId
                && facts[MigratedProducerOutcomeIds.TerminalKindFact.Value]
                    == terminal
                && facts[MigratedProducerOutcomeIds.ConstructionSiteFact.Value]
                    == data.constructionSitePersistentId
                && facts[MigratedProducerOutcomeIds.BuildingDefinitionFact.Value]
                    == data.buildingDefinitionId
                && facts[MigratedProducerOutcomeIds.FinalFacilityFact.Value]
                    == (kind == WorkOrderTerminalKind.Completed
                        ? data.finalFacilityId
                        : "none")
                && facts[MigratedProducerOutcomeIds.ReplacedFacilityFact.Value]
                    == (kind == WorkOrderTerminalKind.Completed
                        ? data.replacedFacilityId
                        : "none")
                && facts[MigratedProducerOutcomeIds.SupportFacilitiesFact.Value]
                    == string.Join(",", data.supportBuildingIds)
                && facts[MigratedProducerOutcomeIds.ContributorsFact.Value]
                    == "character:construction-host:worker-a=3000,character:construction-host:worker-b=7000"
                && facts[MigratedProducerOutcomeIds.RequiredMaterialsFact.Value]
                    == "item:construction-host:iron=2,item:construction-host:wood=3"
                && facts[MigratedProducerOutcomeIds.MaterialInputOperationFact.Value]
                    == data.materialInputOperationId
                && facts[MigratedProducerOutcomeIds.RestitutionOperationFact.Value]
                    == (kind == WorkOrderTerminalKind.Completed
                        ? "none"
                        : data.restitutionOperationId)
                && facts[MigratedProducerOutcomeIds.PlacementOperationFact.Value]
                    == data.placementOperationId
                && facts[MigratedProducerOutcomeIds.QualityFact.Value]
                    == (kind == WorkOrderTerminalKind.Completed
                        ? "good"
                        : "not-resolved")
                && facts[MigratedProducerOutcomeIds.DetailFact.Value]
                    == "12,14",
                "Construction terminal mapping changed its 14 legacy facts: "
                    + kind);
        }

        ConstructionWorkEndedData aliasSource = CreateData(
            WorkOrderTerminalKind.Completed,
            3);
        string frozenJson = JsonConvert.SerializeObject(
            aliasSource.Clone());
        var frozenFact = new ConstructionWorkEndedFact(aliasSource);
        aliasSource.supportBuildingIds[0] =
            "building:construction-host:mutated-support";
        aliasSource.contributions[0].relevantSkill = 99f;
        aliasSource.itemMaterials[0].delivered = 99;
        ConstructionWorkEndedData detached = frozenFact.Snapshot();
        detached.finalFacilityId = "building:construction-host:mutated-final";
        detached.contributions[1].contributedWork = 99f;
        detached.itemMaterials[1].required = 99;
        Require(JsonConvert.SerializeObject(frozenFact.Snapshot())
                == frozenJson,
            "Construction terminal fact retained source or snapshot aliases.");

        SavedConstructionWorkEndedAuthority saved;
        using (var uncertain = new Authority(
                   connect: false,
                   recorderFaults: true))
        {
            var binding = new MigratedDomainFactObserverBinding<
                ConstructionWorkEndedFact>(
                "log:construction-work-ended",
                ConstructionWorkEndedFactMapping.Capture,
                uncertain.Migrated,
                new GameplayDomainFactCommitGroup(
                    uncertain.Recorder,
                    uncertain.Ledger));
            using IDisposable connection = binding.Connect(
                uncertain.Dispatcher);
            ConstructionWorkEndedData data = CreateData(
                WorkOrderTerminalKind.Completed,
                4);
            var fact = new ConstructionWorkEndedFact(data);
            string contextHash = ConstructionWorkEndedFactMapping
                .ComputeContextSha256(data);
            Require(uncertain.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Construction terminal fact did not reserve: " + failure);
            string publicationKey = lease.PublicationKey;
            string expectedKey = MigratedProducerOutcomeCatalog.ResultKey(
                    MigratedProducerOutcomeKind.WorkOrderTerminalResult,
                    "work-order-terminal:" + data.workOrderId,
                    1L)
                .ToString();
            uncertain.Faults.ThrowAfterNextSingleCommit = true;
            DomainFactDelivery attempted = uncertain.Dispatcher.Publish(
                lease,
                fact);
            Require(attempted.MayBeCommitted
                && publicationKey == expectedKey
                && attempted.OperationKey == expectedKey
                && uncertain.Faults.AfterCommitFaultCount == 1
                && OutcomeRowCount(
                    uncertain.Ledger.CaptureGameplayOutcomes()) == 1
                && uncertain.Migrated.Capture().owners.Count == 1,
                "Construction terminal post-commit acknowledgement loss did not retain one exact outcome.");
            saved = JsonConvert.DeserializeObject<
                SavedConstructionWorkEndedAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedConstructionWorkEndedAuthority
                        {
                            serializer = HostSerializer,
                            fact = fact.Snapshot(),
                            publicationKey = publicationKey,
                            contextHash = contextHash,
                            ledger = uncertain.Ledger
                                .CaptureGameplayOutcomes(),
                            migrated = uncertain.Migrated.Capture()
                        }))
                ?? throw new InvalidOperationException(
                    "Construction terminal host JSON round-trip returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.publicationKey.Length > 0
            && ConstructionWorkEndedFactMapping.ComputeContextSha256(
                saved.fact) == saved.contextHash,
            "Construction terminal frozen fact did not survive host JSON restore.");
        ConstructionWorkEndedFactPreflight.Validate(new[] { ToOwner(saved.fact, saved.publicationKey) }, saved.ledger, saved.migrated);
        RequireThrows<InvalidOperationException>(() => ConstructionWorkEndedFactPreflight.Validate(
                new[] { ToOwner(saved.fact, string.Empty) }, saved.ledger, saved.migrated),
            "Construction canonical join accepted a lost publication key.");
        using (var restored = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            restored.Restore(saved.ledger, saved.migrated);
            var binding = new MigratedDomainFactObserverBinding<
                ConstructionWorkEndedFact>(
                "log:construction-work-ended",
                ConstructionWorkEndedFactMapping.Capture,
                restored.Migrated,
                new GameplayDomainFactCommitGroup(
                    restored.Recorder,
                    restored.Ledger));
            using IDisposable connection = binding.Connect(
                restored.Dispatcher);
            var exactFact = new ConstructionWorkEndedFact(saved.fact);
            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                restored.Ledger.CaptureGameplayOutcomes());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var replies = new DomainFactDelivery[1];
                Require(restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.publicationKey,
                                exactFact)
                        },
                        replies,
                        out string failure)
                    && replies[0].Completed
                    && replies[0].OperationKey == saved.publicationKey
                    && OutcomeRowCount(
                        restored.Ledger.CaptureGameplayOutcomes()) == 1
                    && restored.Migrated.Capture().owners.Count == 1,
                    "Construction terminal exact replay " + attempt
                        + " did not reconcile once: " + failure);
            }
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Construction terminal exact replay changed canonical evidence.");

            RequireTamperRejected(
                value => value.absoluteDay++,
                "occurrence day");
            RequireTamperRejected(
                value => value.qualityTier =
                    CraftsmanshipQualityTier.Excellent,
                "quality");
            RequireTamperRejected(
                value => value.supportBuildingIds[1] =
                    "building:construction-host:support-z",
                "support facility");
            RequireTamperRejected(
                value => value.finalFacilityId =
                    "building:construction-host:final-changed",
                "final facility");
            RequireTamperRejected(
                value => value.replacedFacilityId =
                    "building:construction-host:replaced-changed",
                "replaced facility");
            RequireTamperRejected(
                value => value.contributions[0].relevantSkill += 1f,
                "contribution skill");
            RequireTamperRejected(
                value => value.itemMaterials[0].delivered--,
                "delivered material");

            void RequireTamperRejected(
                Action<ConstructionWorkEndedData> change,
                string label)
            {
                ConstructionWorkEndedData changed = saved.fact.Clone();
                change(changed);
                RequireThrows<InvalidOperationException>(() => ConstructionWorkEndedFactPreflight.Validate(
                        new[] { ToOwner(changed, saved.publicationKey) }, saved.ledger, saved.migrated),
                    "Construction canonical join accepted changed " + label + ".");
                Require(ConstructionWorkEndedFactMapping
                        .ComputeContextSha256(changed) != saved.contextHash,
                    "Construction terminal " + label
                        + " change did not alter its context hash.");
                var replies = new DomainFactDelivery[1];
                Require(!restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.publicationKey,
                                new ConstructionWorkEndedFact(changed))
                        },
                        replies,
                        out string failure)
                    && replies[0].Phase
                        == DomainFactDeliveryPhase.Rejected
                    && !string.IsNullOrWhiteSpace(failure),
                    "Construction terminal replay accepted changed "
                        + label + ".");
                RequireCanonicalEqual(
                    canonical,
                    CanonicalHashes(
                        restored.Ledger.CaptureGameplayOutcomes()),
                    "Rejected construction terminal " + label
                        + " replay changed canonical evidence.");
            }
        }

        static WorkOrderSaveData ToOwner(ConstructionWorkEndedData data, string key) => new()
        {
            workOrderId = data.workOrderId, workTypeId = BuiltInWorkTypeIds.Construct.Value,
            terminalKind = data.terminalKind, terminalPhase = WorkOrderTerminalPhase.EffectsAppliedPendingOutcome,
            terminalFactCaptured = true, terminalFact = data.Clone(), terminalPublicationKey = key,
            placementOperationId = data.placementOperationId, constructionSitePersistentId = data.constructionSitePersistentId,
            targetBuildingId = data.targetBuildingId, gridX = data.gridX, gridY = data.gridY,
            requiredWork = data.requiredWork, completedWork = data.completedWork,
            finalFacilityId = data.finalFacilityId, finalFacilityDefinitionId = data.finalFacilityDefinitionId,
            replacedFacilityId = data.replacedFacilityId, replacedFacilityDefinitionId = data.replacedFacilityDefinitionId,
            supportBuildingIds = data.supportBuildingIds.ToList(), contributions = data.Clone().contributions,
            itemMaterials = data.Clone().itemMaterials,
            materialTransfer = new WorkOrderMaterialTransferSaveData
            { operationId = data.materialInputOperationId, restitutionOperationId = data.restitutionOperationId }
        };

        static ConstructionWorkEndedData CreateData(
            WorkOrderTerminalKind kind,
            int sequence)
        {
            bool completed = kind == WorkOrderTerminalKind.Completed;
            string suffix = sequence.ToString(
                "D8",
                CultureInfo.InvariantCulture);
            string definition =
                "building-definition:construction-host:" + suffix;
            return new ConstructionWorkEndedData
            {
                workOrderId = "work-order:construction-host:" + suffix,
                terminalKind = kind,
                placementOperationId =
                    "construction-placement:construction-host:" + suffix,
                constructionSitePersistentId =
                    "building:construction-host:site-" + suffix,
                targetBuildingId = sequence,
                buildingDefinitionId = definition,
                buildingDisplayName = "호스트 건설 시설 " + suffix,
                finalFacilityId = completed
                    ? "building:construction-host:final-" + suffix
                    : string.Empty,
                finalFacilityDefinitionId = completed
                    ? definition
                    : string.Empty,
                replacedFacilityId = completed
                    ? "building:construction-host:replaced-" + suffix
                    : string.Empty,
                replacedFacilityDefinitionId = completed
                    ? "building-definition:construction-host:replaced-"
                        + suffix
                    : string.Empty,
                absoluteDay = 11,
                gridX = 12,
                gridY = 14,
                requiredWork = 10f,
                completedWork = completed ? 10f : 4f,
                qualityTier = CraftsmanshipQualityTier.Good,
                supportBuildingIds = new List<string>
                {
                    "building:construction-host:support-a",
                    "building:construction-host:support-b"
                },
                contributions = new List<CraftContributionSaveData>
                {
                    new()
                    {
                        characterId =
                            "character:construction-host:worker-a",
                        contributedWork = 3f,
                        relevantSkill = 2f
                    },
                    new()
                    {
                        characterId =
                            "character:construction-host:worker-b",
                        contributedWork = 7f,
                        relevantSkill = 5f
                    }
                },
                itemMaterials = new List<WorkOrderItemMaterialSaveData>
                {
                    new()
                    {
                        itemId = "item:construction-host:iron",
                        required = 2,
                        delivered = 2
                    },
                    new()
                    {
                        itemId = "item:construction-host:wood",
                        required = 3,
                        delivered = 3
                    }
                },
                materialInputOperationId =
                    "material-input:construction-host:" + suffix,
                restitutionOperationId = completed
                    ? string.Empty
                    : "material-restitution:construction-host:" + suffix
            };
        }

        static void RequireMetric(
            MigratedProducerOutcomeDetails details,
            GameplayMetricId metricId,
            double expected,
            string subjectId)
        {
            GameplayOutcomeMetric metric = details.Metrics.Single(
                value => value.MetricId.Equals(metricId));
            Require(metric.Value == expected
                && metric.UnitId.Equals(
                    MigratedProducerOutcomeIds.MilliWorkUnit)
                && metric.DefinitionOrInstanceId.Kind.Equals(
                    MigratedProducerOutcomeIds.FacilityKind)
                && metric.DefinitionOrInstanceId.Value == subjectId,
                "Construction terminal metric changed: "
                    + metricId.Value);
        }
    }

    private static void VerifyPrimitiveSurvivalCompletedFactObservation()
    {
        string[] actions =
        {
            "survival:floor-rest",
            "survival:primitive-latrine",
            "survival:bucket-wash"
        };
        for (int index = 0; index < actions.Length; index++)
        {
            PrimitiveSurvivalPendingSaveData data = CreatePending(
                actions[index],
                index + 1);
            MigratedDomainFactCapture capture =
                PrimitiveSurvivalCompletedFactMapping.Capture(
                    new PrimitiveSurvivalCompletedFact(data));
            MigratedProducerOutcomeDetails details = capture.Details;
            string expectedSummary = "action=" + data.actionId
                + "; recovery="
                + data.recovery.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture)
                + "; physicalItemCount="
                + data.physicalItemCount.ToString(
                    CultureInfo.InvariantCulture);
            Require(capture.Kind == MigratedProducerOutcomeKind
                    .CharacterPrimitiveSurvivalCompletedEvent
                && capture.OwnerIdentity
                    == "primitive-survival:" + data.characterId
                && capture.AbsoluteDay == data.absoluteDay
                && capture.Status == GameplayOutcomeStatus.Succeeded
                && !capture.ReservationOnly
                && details.Subjects.Length == 1
                && details.Subjects[0].EntityId.Kind.Equals(
                    MigratedProducerOutcomeIds.CharacterKind)
                && details.Subjects[0].EntityId.Value == data.characterId
                && details.Subjects[0].DisplayName.DisplayText
                    == data.characterName
                && details.Subjects[0].Role.Equals(
                    MigratedProducerOutcomeIds.ActorRole)
                && details.Metrics.Length == 0
                && details.Facts.Length == 0
                && details.Summary == expectedSummary
                && details.Provenance.Length == 2
                && details.Provenance[1].KindId == "domain-commit"
                && details.Provenance[1].Value == data.operationId
                && details.Provenance[0].KindId
                    == "primitive-survival-context-sha256"
                && details.Provenance[0].Value
                    == PrimitiveSurvivalCompletedFactMapping
                        .ComputeContextSha256(data)
                && details.Provenance[0].Value.StartsWith("sha256:", StringComparison.Ordinal)
                && details.Provenance[0].Value.Length == 71
                && details.Provenance[0].Value.Substring(7).All(Uri.IsHexDigit),
                "Primitive-survival mapping changed its exact legacy shape: "
                    + data.actionId);
            Require(data.recovery == (data.actionId
                        == "survival:floor-rest"
                    ? 55f
                    : data.actionId == "survival:primitive-latrine"
                        ? 85f
                        : 50f)
                && data.physicalItemCount == (data.actionId
                        == "survival:bucket-wash"
                    ? 1
                    : 0)
                && (data.actionId == "survival:bucket-wash")
                    == (data.waterStackId.Length > 0)
                && (data.actionId == "survival:floor-rest")
                    == (data.restPublicationKey.Length > 0)
                && (data.actionId == "survival:floor-rest")
                    == (data.GetRestObservation() != null)
                && (data.GetRestObservation() == null
                    || data.GetRestObservation().publicationKey
                        == data.restPublicationKey
                    && data.GetRestObservation().fact.context.eventInstanceId
                        == data.operationId
                    && data.GetRestObservation().fact.after == data.sleepAfter)
                && (data.actionId == "survival:primitive-latrine")
                    == (data.wasteFilthId.Length > 0
                        && data.stainFilthId.Length > 0
                        && data.wasteFilthId != data.stainFilthId
                        && data.wasteAmountAfter > 0f
                        && data.stainAmountAfter > 0f),
                "Primitive-survival action boundary changed: "
                    + data.actionId);
        }

        PrimitiveSurvivalPendingSaveData aliasSource = CreatePending(
            "survival:floor-rest",
            4);
        string frozenJson = JsonConvert.SerializeObject(
            aliasSource.Clone());
        var frozenFact = new PrimitiveSurvivalCompletedFact(aliasSource);
        aliasSource.operationId = "primitive:survival-host:mutated";
        aliasSource.characterName = "변경된 생존자";
        aliasSource.GetRestObservation().fact.after = 99f;
        PrimitiveSurvivalPendingSaveData detached = frozenFact.Snapshot();
        detached.actionId = "survival:bucket-wash";
        detached.sleepAfter = 0f;
        detached.GetRestObservation().fact.context.resultDetail =
            "분리된 휴식 관측 변조";
        Require(JsonConvert.SerializeObject(frozenFact.Snapshot())
                == frozenJson,
            "Primitive-survival fact retained source or snapshot aliases.");

        SavedPrimitiveSurvivalAuthority saved;
        using (var uncertain = new Authority(
                   connect: false,
                   recorderFaults: true))
        {
            var binding = new MigratedDomainFactObserverBinding<
                PrimitiveSurvivalCompletedFact>(
                "log:primitive-survival-completed",
                PrimitiveSurvivalCompletedFactMapping.Capture,
                uncertain.Migrated,
                new GameplayDomainFactCommitGroup(
                    uncertain.Recorder,
                    uncertain.Ledger));
            using IDisposable connection = binding.Connect(
                uncertain.Dispatcher);

            var proposal = new PrimitiveSurvivalCompletedFact(
                CreatePending(
                    "survival:floor-rest",
                    5,
                    reservationOnly: true),
                reservationOnly: true);
            bool proposalReserved = uncertain.Dispatcher.TryReserve(
                    proposal,
                    out DomainFactLease proposalLease,
                    out string proposalFailure);
            Require(PrimitiveSurvivalCompletedFactMapping.Capture(proposal)
                    .ReservationOnly && proposalReserved,
                "Primitive-survival proposal did not reserve: "
                    + proposalFailure);
            string proposalKey = proposalLease.PublicationKey;
            DomainFactDelivery proposalDelivery = uncertain.Dispatcher
                .Publish(proposalLease, proposal);
            Require(!proposalDelivery.MayBeCommitted
                && proposalDelivery.Detail
                    == "fact-observer-proposal-cannot-be-published"
                && uncertain.Dispatcher.ActiveLeaseCount == 0
                && OutcomeRowCount(
                    uncertain.Ledger.CaptureGameplayOutcomes()) == 0,
                "Primitive-survival proposal was published as an actual result.");
            var proposalReplies = new DomainFactDelivery[1];
            Require(!uncertain.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(proposalKey, proposal)
                    },
                    proposalReplies,
                    out string proposalReplayFailure)
                && proposalReplies[0].Phase
                    == DomainFactDeliveryPhase.Rejected
                && proposalReplayFailure
                    == "fact-observer-proposal-cannot-be-replayed",
                "Primitive-survival proposal was accepted by recovery replay.");

            PrimitiveSurvivalPendingSaveData pending = CreatePending(
                "survival:bucket-wash",
                6);
            var proposedActual = new PrimitiveSurvivalCompletedFact(
                pending,
                reservationOnly: true);
            Require(uncertain.Dispatcher.TryReserve(
                    proposedActual,
                    out DomainFactLease lease,
                    out string failure),
                "Primitive-survival actual fact did not reserve: "
                    + failure);
            pending.publicationKey = lease.PublicationKey;
            var actual = new PrimitiveSurvivalCompletedFact(pending);
            string contextHash = PrimitiveSurvivalCompletedFactMapping
                .ComputeContextSha256(pending);
            string expectedKey = MigratedProducerOutcomeCatalog.ResultKey(
                    MigratedProducerOutcomeKind
                        .CharacterPrimitiveSurvivalCompletedEvent,
                    "primitive-survival:" + pending.characterId,
                    1L)
                .ToString();
            uncertain.Faults.ThrowAfterNextSingleCommit = true;
            DomainFactDelivery attempted = uncertain.Dispatcher.Publish(
                lease,
                actual);
            Require(attempted.MayBeCommitted
                && pending.publicationKey == expectedKey
                && attempted.OperationKey == expectedKey
                && uncertain.Faults.AfterCommitFaultCount == 1
                && OutcomeRowCount(
                    uncertain.Ledger.CaptureGameplayOutcomes()) == 1
                && uncertain.Migrated.Capture().owners.Count == 1,
                "Primitive-survival post-commit acknowledgement loss did not retain one exact outcome.");
            saved = JsonConvert.DeserializeObject<
                SavedPrimitiveSurvivalAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedPrimitiveSurvivalAuthority
                        {
                            serializer = HostSerializer,
                            pending = actual.Snapshot(),
                            publicationKey = pending.publicationKey,
                            contextHash = contextHash,
                            ledger = uncertain.Ledger
                                .CaptureGameplayOutcomes(),
                            migrated = uncertain.Migrated.Capture()
                        }))
                ?? throw new InvalidOperationException(
                    "Primitive-survival host JSON round-trip returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.pending.publicationKey == saved.publicationKey
            && PrimitiveSurvivalCompletedFactMapping.ComputeContextSha256(
                saved.pending) == saved.contextHash,
            "Primitive-survival frozen pending owner did not survive host JSON restore.");
        PrimitiveSurvivalFactPreflight.Validate(
            CreateSurvival(saved.pending),
            saved.ledger,
            saved.migrated);
        DungeonDarkSurvivalSaveData orphanSurvival =
            JsonConvert.DeserializeObject<DungeonDarkSurvivalSaveData>(
                JsonConvert.SerializeObject(
                    new DungeonDarkSurvivalSaveData
                    {
                        pendingPrimitivePublications =
                            new List<PrimitiveSurvivalPendingSaveData>
                            {
                                saved.pending.Clone()
                            }
                    }))
            ?? throw new InvalidOperationException(
                "Primitive-survival orphan host JSON round-trip returned null.");
        PrimitiveSurvivalPendingSaveData orphanPending =
            orphanSurvival.pendingPrimitivePublications.Single();
        Require(orphanSurvival.characters.Count == 0
            && orphanPending.actionId == "survival:bucket-wash"
            && orphanPending.characterId == saved.pending.characterId
            && orphanPending.publicationKey == saved.publicationKey,
            "Primitive-survival orphan JSON lost its frozen wash owner.");
        PrimitiveSurvivalFactPreflight.Validate(
            orphanSurvival,
            saved.ledger,
            saved.migrated);
        DungeonDarkSurvivalSaveData duplicatedOwner =
            CreateSurvival(saved.pending);
        duplicatedOwner.pendingPrimitivePublications.Add(
            saved.pending.Clone());
        RequireThrows<InvalidOperationException>(
            () => PrimitiveSurvivalFactPreflight.Validate(
                duplicatedOwner,
                saved.ledger,
                saved.migrated),
            "Primitive-survival preflight accepted an active/orphan duplicate key and operation.");

        PrimitiveSurvivalPendingSaveData metadataOnly =
            saved.pending.Clone();
        metadataOnly.version = 99;
        metadataOnly.publicationKey = "metadata-only-publication";
        metadataOnly.rollbackBlocked = true;
        metadataOnly.rollbackFailure = "metadata-only-rollback";
        metadataOnly.restObservations = CreatePending(
                "survival:floor-rest",
                7)
            .restObservations;
        Require(PrimitiveSurvivalCompletedFactMapping.ComputeContextSha256(
                metadataOnly) == saved.contextHash,
            "Primitive-survival context hash included version/publication/rollback/rest-observation metadata.");
        PrimitiveSurvivalPendingSaveData restKeyChanged =
            saved.pending.Clone();
        restKeyChanged.restPublicationKey = CreatePending(
                "survival:floor-rest",
                8)
            .restPublicationKey;
        Require(PrimitiveSurvivalCompletedFactMapping.ComputeContextSha256(
                restKeyChanged) != saved.contextHash,
            "Primitive-survival context hash omitted the rest child key.");

        RequirePreflightRejected(
            value => value.characterName += " 변경",
            saved.migrated,
            "changed character name");
        RequirePreflightRejected(
            value => value.absoluteDay++,
            saved.migrated,
            "changed occurrence day");
        RequirePreflightRejected(
            value => value.recovery = 49f,
            saved.migrated,
            "changed recovery");
        RequirePreflightRejected(
            value =>
            {
                value.targetGridX = 4;
                value.targetGridY = 6;
                value.wasteFilthId = "filth:survival-host:unexpected-waste";
                value.wasteAmountAfter = 1f;
                value.stainFilthId = "filth:survival-host:unexpected-stain";
                value.stainAmountAfter = 0.5f;
            },
            saved.migrated,
            "unexpected bucket-wash filth");
        RequirePreflightRejected(
            value => value.publicationKey = string.Empty,
            saved.migrated,
            "missing publication key");
        RequirePreflightRejected(
            value =>
            {
                value.rollbackBlocked = true;
                value.rollbackFailure = "primitive-survival-host-blocked";
            },
            saved.migrated,
            "rollback-blocked committed owner");
        RequireThrows<InvalidOperationException>(
            () => PrimitiveSurvivalFactPreflight.Validate(
                CreateSurvival(saved.pending),
                saved.ledger,
                new DungeonMigratedProducerOutcomeSaveData()),
            "Primitive-survival preflight accepted a committed row without its observer counter.");

        using (var restored = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            restored.Restore(saved.ledger, saved.migrated);
            var binding = new MigratedDomainFactObserverBinding<
                PrimitiveSurvivalCompletedFact>(
                "log:primitive-survival-completed",
                PrimitiveSurvivalCompletedFactMapping.Capture,
                restored.Migrated,
                new GameplayDomainFactCommitGroup(
                    restored.Recorder,
                    restored.Ledger));
            using IDisposable connection = binding.Connect(
                restored.Dispatcher);
            var exact = new PrimitiveSurvivalCompletedFact(orphanPending);
            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                restored.Ledger.CaptureGameplayOutcomes());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var replies = new DomainFactDelivery[1];
                Require(restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.publicationKey,
                                exact)
                        },
                        replies,
                        out string failure)
                    && replies[0].Completed
                    && replies[0].OperationKey == saved.publicationKey
                    && OutcomeRowCount(
                        restored.Ledger.CaptureGameplayOutcomes()) == 1
                    && restored.Migrated.Capture().owners.Count == 1,
                    "Primitive-survival exact replay " + attempt
                        + " did not reconcile once: " + failure);
            }
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Primitive-survival exact replay changed canonical evidence.");

            RequireTamperRejected(
                value => value.characterName += " 변경",
                "character name");
            RequireTamperRejected(
                value => value.absoluteDay++,
                "occurrence day");
            RequireTamperRejected(
                value => value.sleepAfter += 1f,
                "sleep after");
            RequireTamperRejected(
                value => value.hygieneAfter += 1f,
                "hygiene after");
            RequireTamperRejected(
                value => value.excretionAfter += 1f,
                "excretion after");
            RequireTamperRejected(
                value => value.waterStackId =
                    "stack:survival-host:water-changed",
                "water stack");
            RequireTamperRejected(
                value => value.operationId =
                    "primitive:survival-host:operation-changed",
                "business operation");

            void RequireTamperRejected(
                Action<PrimitiveSurvivalPendingSaveData> change,
                string label)
            {
                PrimitiveSurvivalPendingSaveData changed =
                    orphanPending.Clone();
                change(changed);
                Require(PrimitiveSurvivalCompletedFactMapping
                        .ComputeContextSha256(changed) != saved.contextHash,
                    "Primitive-survival " + label
                        + " change did not alter its context hash.");
                var replies = new DomainFactDelivery[1];
                Require(!restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.publicationKey,
                                new PrimitiveSurvivalCompletedFact(changed))
                        },
                        replies,
                        out string failure)
                    && replies[0].Phase
                        == DomainFactDeliveryPhase.Rejected
                    && !string.IsNullOrWhiteSpace(failure),
                    "Primitive-survival replay accepted changed "
                        + label + ".");
                RequireCanonicalEqual(
                    canonical,
                    CanonicalHashes(
                        restored.Ledger.CaptureGameplayOutcomes()),
                    "Rejected primitive-survival " + label
                        + " replay changed canonical evidence.");
            }
        }

        void RequirePreflightRejected(
            Action<PrimitiveSurvivalPendingSaveData> change,
            DungeonMigratedProducerOutcomeSaveData counters,
            string label)
        {
            PrimitiveSurvivalPendingSaveData changed =
                saved.pending.Clone();
            change(changed);
            RequireThrows<InvalidOperationException>(
                () => PrimitiveSurvivalFactPreflight.Validate(
                    CreateSurvival(changed),
                    saved.ledger,
                    counters),
                "Primitive-survival preflight accepted " + label + ".");
        }

        static DungeonDarkSurvivalSaveData CreateSurvival(
            PrimitiveSurvivalPendingSaveData pending) => new()
        {
            characters = new List<CharacterDeprivationState>
            {
                new()
                {
                    characterId = pending.characterId,
                    primitiveSurvivalPending = pending.Clone()
                }
            }
        };

        static PrimitiveSurvivalPendingSaveData CreatePending(
            string actionId,
            int sequence,
            bool reservationOnly = false)
        {
            bool floor = actionId == "survival:floor-rest";
            bool latrine = actionId == "survival:primitive-latrine";
            bool wash = actionId == "survival:bucket-wash";
            string suffix = sequence.ToString(
                "D8",
                CultureInfo.InvariantCulture);
            string operationId = "primitive:survival-host:" + suffix;
            string characterId = "character:survival-host:" + suffix;
            string characterName = "호스트 생존자 " + suffix;
            string restPublicationKey = floor && !reservationOnly
                ? MigratedProducerOutcomeCatalog.ResultKey(
                        MigratedProducerOutcomeKind.RestOutcomeIdentityEvent,
                        "rest-outcome:" + characterId,
                        1L)
                    .ToString()
                : string.Empty;
            var result = new PrimitiveSurvivalPendingSaveData
            {
                actionId = actionId,
                operationId = operationId,
                characterId = characterId,
                characterName = characterName,
                waterStackId = wash
                    ? "stack:survival-host:water-" + suffix
                    : string.Empty,
                restPublicationKey = restPublicationKey,
                absoluteDay = 17,
                recovery = actionId == "survival:floor-rest"
                    ? 55f
                    : latrine ? 85f : 50f,
                physicalItemCount = wash ? 1 : 0,
                sleepAfter = 61f,
                hygieneAfter = 52f,
                excretionAfter = 43f,
                targetGridX = latrine ? 7 : 0,
                targetGridY = latrine ? 9 : 0,
                wasteFilthId = latrine
                    ? "filth:survival-host:waste-" + suffix
                    : string.Empty,
                wasteAmountAfter = latrine ? 1.25f : 0f,
                stainFilthId = latrine
                    ? "filth:survival-host:stain-" + suffix
                    : string.Empty,
                stainAmountAfter = latrine ? 0.5f : 0f
            };
            if (floor && !reservationOnly)
            {
                result.restObservations.Add(new CharacterRestObservationSaveData
                {
                    fact = new CharacterRestAppliedFactData
                    {
                        characterId = characterId,
                        displayName = characterName,
                        before = 6f,
                        after = result.sleepAfter,
                        absoluteDay = result.absoluteDay,
                        activeConditions = new List<string>(),
                        context = new CharacterEventContext
                        {
                            eventInstanceId = operationId,
                            actorId = characterId,
                            actorDisplayName = characterName,
                            resultDetail = "바닥 휴식",
                            occurredDay = result.absoluteDay
                        }
                    },
                    publicationKey = restPublicationKey
                });
            }
            return result;
        }
    }

    private static void VerifyApparelWorkCompletedFactObservation()
    {
        ApparelWorkOrderKind[] supportedKinds =
        {
            ApparelWorkOrderKind.Laundry,
            ApparelWorkOrderKind.Drying,
            ApparelWorkOrderKind.Repair,
            ApparelWorkOrderKind.Alteration
        };
        var covered = new HashSet<ApparelWorkOrderKind>();
        for (int index = 0; index < supportedKinds.Length; index++)
        {
            ApparelWorkOrderKind kind = supportedKinds[index];
            ApparelWorkCompletedData source = CreateData(kind, index + 1);
            var fact = new ApparelWorkCompletedFact(source);
            MigratedDomainFactCapture capture =
                ApparelWorkCompletedFactMapping.Capture(fact);
            MigratedProducerOutcomeDetails details = capture.Details;
            Dictionary<string, string> facts = details.Facts.ToDictionary(
                value => value.FactId.Value,
                value => value.Value,
                StringComparer.Ordinal);
            ApparelWorkOrderOutcomeItemStateSaveData[] states = source
                .itemStates
                .OrderBy(value => value.itemInstanceId, StringComparer.Ordinal)
                .ToArray();
            string label = kind switch
            {
                ApparelWorkOrderKind.Laundry => "세탁",
                ApparelWorkOrderKind.Drying => "건조",
                ApparelWorkOrderKind.Repair => "수리",
                ApparelWorkOrderKind.Alteration => "수선",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };

            Require(covered.Add(kind)
                && capture.Kind == MigratedProducerOutcomeKind
                    .ApparelWorkOrderTerminalResult
                && capture.OwnerIdentity
                    == "apparel-work-order:order=" + source.orderId
                && capture.AbsoluteDay == source.absoluteDay
                && capture.Status == GameplayOutcomeStatus.Succeeded
                && !capture.ReservationOnly
                && details.Subjects.Length == 3
                && details.Subjects[0].EntityId.Kind.Equals(
                    MigratedProducerOutcomeIds.FacilityKind)
                && details.Subjects[0].EntityId.Value
                    == source.facilityInstanceId
                && details.Subjects[0].DisplayName.DisplayText
                    == "의복 작업 시설"
                && details.Subjects[0].Role.Equals(
                    MigratedProducerOutcomeIds.FacilityRole)
                && details.Subjects[1].EntityId.Kind.Equals(
                    MigratedProducerOutcomeIds.ItemKind)
                && details.Subjects[1].EntityId.Value
                    == states[0].itemInstanceId
                && details.Subjects[1].DisplayName.DisplayText
                    == states[0].apparelDisplayName
                && details.Subjects[2].EntityId.Value
                    == states[1].itemInstanceId
                && details.Subjects[2].EntityId.Kind.Equals(
                    MigratedProducerOutcomeIds.ItemKind)
                && details.Subjects[2].DisplayName.DisplayText
                    == states[1].apparelDisplayName
                && details.Subjects[1].Role.Equals(
                    MigratedProducerOutcomeIds.ItemRole)
                && details.Subjects[2].Role.Equals(
                    MigratedProducerOutcomeIds.ItemRole)
                && details.Metrics.Length == 4
                && details.Facts.Length == 8
                && details.Provenance.Length == 1
                && details.Provenance[0].KindId
                    == "apparel-work-context-sha256"
                && details.Provenance[0].Value
                    == ApparelWorkCompletedFactMapping.ComputeContextSha256(
                        source)
                && details.Provenance[0].Value.Length == 71
                && details.Provenance[0].Value.StartsWith(
                    "sha256:",
                    StringComparison.Ordinal)
                && details.Provenance[0].Value.Skip(7).All(Uri.IsHexDigit)
                && details.Summary == label + " 작업이 완료되어 2벌의 의복 상태가 바뀌었다.",
                "Apparel-work mapping changed identity, subjects, or provenance: "
                    + kind);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.CountMetric,
                2d,
                MigratedProducerOutcomeIds.CountUnit,
                MigratedProducerOutcomeIds.ItemKind,
                states[0].itemInstanceId);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.CompletedWorkMetric,
                4000d,
                MigratedProducerOutcomeIds.MilliWorkUnit,
                MigratedProducerOutcomeIds.FacilityKind,
                source.facilityInstanceId);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.QuantityMetric,
                kind == ApparelWorkOrderKind.Repair ? 3d : 0d,
                MigratedProducerOutcomeIds.CountUnit,
                MigratedProducerOutcomeIds.FacilityKind,
                source.facilityInstanceId);
            RequireMetric(
                details,
                MigratedProducerOutcomeIds.MassMetric,
                kind == ApparelWorkOrderKind.Repair ? 1500d : 0d,
                MigratedProducerOutcomeIds.GramUnit,
                MigratedProducerOutcomeIds.FacilityKind,
                source.facilityInstanceId);
            Require(facts.Count == 8
                && facts[MigratedProducerOutcomeIds.WorkOrderFact.Value]
                    == source.orderId
                && facts[MigratedProducerOutcomeIds.FacilityFact.Value]
                    == source.facilityInstanceId
                && facts["apparel-order-kind"] == kind.ToString()
                && facts[MigratedProducerOutcomeIds.BatchFact.Value]
                    == (kind == ApparelWorkOrderKind.Repair
                        ? source.materialCommitId
                        : "none")
                && facts["material-source-stack-ids"]
                    == (kind == ApparelWorkOrderKind.Repair
                        ? string.Join(",", source.materialSourceStackIds)
                        : "none")
                && facts[MigratedProducerOutcomeIds.ContributorsFact.Value]
                    == ContributorFact(source.contributions)
                && facts["apparel-state-before-1"]
                    == StateFact(states, value => value.beforeStatePayload)
                && facts["apparel-state-after-1"]
                    == StateFact(states, value => value.afterStatePayload),
                "Apparel-work mapping changed its legacy facts: " + kind);
        }
        Require(covered.SetEquals(supportedKinds),
            "Apparel-work mapping did not cover all four non-craft kinds.");

        ApparelWorkCompletedData aliasSource = CreateData(
            ApparelWorkOrderKind.Repair,
            9);
        ApparelWorkCompletedData original = aliasSource.Clone();
        string frozenHash = ApparelWorkCompletedFactMapping
            .ComputeContextSha256(aliasSource);
        var frozenFact = new ApparelWorkCompletedFact(aliasSource);
        aliasSource.facilityInstanceId = "building:apparel-host:mutated";
        aliasSource.materialSourceStackIds[0]
            = "stack:apparel-host:mutated";
        aliasSource.contributions[0].relevantSkill = 99f;
        aliasSource.itemStates[0].apparelDisplayName = "변경된 의복";
        ApparelWorkCompletedData detached = frozenFact.Snapshot();
        detached.materialSourceStackIds[1]
            = "stack:apparel-host:detached";
        detached.contributions[1].contributedWork = 99f;
        detached.itemStates[1].afterStatePayload = "detached-after";
        Require(JsonConvert.SerializeObject(frozenFact.Snapshot())
                == JsonConvert.SerializeObject(original)
            && ApparelWorkCompletedFactMapping.Capture(frozenFact)
                .Details.Provenance.Single().Value == frozenHash,
            "Apparel-work fact retained source or snapshot aliases.");

        RequireThrows<InvalidOperationException>(
            () => new ApparelWorkCompletedFact(CreateData(
                ApparelWorkOrderKind.Craft,
                10)),
            "Apparel-work fact accepted the craft terminal kind.");
        ApparelWorkCompletedData nonRepairMaterial = CreateData(
            ApparelWorkOrderKind.Laundry,
            11);
        nonRepairMaterial.materialOperationId
            = "material-input:apparel-host:unexpected";
        RequireThrows<InvalidOperationException>(
            () => new ApparelWorkCompletedFact(nonRepairMaterial),
            "Apparel-work fact accepted material evidence for a non-repair kind.");
        ApparelWorkCompletedData partialMaterial = CreateData(
            ApparelWorkOrderKind.Repair,
            12);
        partialMaterial.materialCommitId = string.Empty;
        RequireThrows<InvalidOperationException>(
            () => new ApparelWorkCompletedFact(partialMaterial),
            "Apparel-work fact accepted partial repair material evidence.");

        using (var normal = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            var binding = new MigratedDomainFactObserverBinding<
                ApparelWorkCompletedFact>(
                "log:apparel-work-completed-normal",
                ApparelWorkCompletedFactMapping.Capture,
                normal.Migrated,
                new GameplayDomainFactCommitGroup(
                    normal.Recorder,
                    normal.Ledger));
            using IDisposable connection = binding.Connect(
                normal.Dispatcher);
            var fact = new ApparelWorkCompletedFact(CreateData(
                ApparelWorkOrderKind.Alteration,
                13));
            Require(normal.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Apparel-work normal fact did not reserve: " + failure);
            string publicationKey = lease.PublicationKey;
            DomainFactDelivery delivery = normal.Dispatcher.Publish(
                lease,
                fact);
            Require(delivery.Completed
                && delivery.OperationKey == publicationKey
                && !string.IsNullOrWhiteSpace(delivery.PublicationId)
                && OutcomeRowCount(
                    normal.Ledger.CaptureGameplayOutcomes()) == 1
                && normal.Migrated.Capture().owners.Count == 1,
                "Apparel-work normal publication did not commit once.");
        }

        SavedApparelWorkCompletedAuthority saved;
        using (var uncertain = new Authority(
                   connect: false,
                   recorderFaults: true))
        {
            var binding = new MigratedDomainFactObserverBinding<
                ApparelWorkCompletedFact>(
                "log:apparel-work-completed-uncertain",
                ApparelWorkCompletedFactMapping.Capture,
                uncertain.Migrated,
                new GameplayDomainFactCommitGroup(
                    uncertain.Recorder,
                    uncertain.Ledger));
            using IDisposable connection = binding.Connect(
                uncertain.Dispatcher);
            ApparelWorkCompletedData data = CreateData(
                ApparelWorkOrderKind.Repair,
                14);
            var fact = new ApparelWorkCompletedFact(data);
            Require(uncertain.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Apparel-work uncertain fact did not reserve: " + failure);
            string publicationKey = lease.PublicationKey;
            ApparelWorkOrderSaveData owner = CreateOwner(
                data,
                publicationKey);
            uncertain.Faults.ThrowAfterNextSingleCommit = true;
            DomainFactDelivery attempted = uncertain.Dispatcher.Publish(
                lease,
                fact);
            Require(attempted.MayBeCommitted
                && attempted.OperationKey == publicationKey
                && uncertain.Faults.AfterCommitFaultCount == 1
                && OutcomeRowCount(
                    uncertain.Ledger.CaptureGameplayOutcomes()) == 1
                && uncertain.Migrated.Capture().owners.Count == 1,
                "Apparel-work post-commit acknowledgement loss did not retain one outcome.");
            saved = JsonConvert.DeserializeObject<
                SavedApparelWorkCompletedAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedApparelWorkCompletedAuthority
                        {
                            serializer = HostSerializer,
                            owner = owner,
                            publicationKey = publicationKey,
                            contextHash = ApparelWorkCompletedFactMapping
                                .ComputeContextSha256(data),
                            ledger = uncertain.Ledger
                                .CaptureGameplayOutcomes(),
                            migrated = uncertain.Migrated.Capture()
                        }))
                ?? throw new InvalidOperationException(
                    "Apparel-work host JSON round-trip returned null.");
        }

        Require(saved.serializer == HostSerializer
            && saved.publicationKey.Length > 0
            && saved.owner.terminalOutcomeReady
            && saved.owner.terminalPublicationKey == saved.publicationKey
            && !saved.owner.terminalPublicationConfirmed
            && saved.owner.completedWork == saved.owner.requiredWork
            && ApparelWorkCompletedFactMapping.ComputeContextSha256(
                ApparelWorkCompletedData.FromOwner(saved.owner))
                == saved.contextHash,
            "Apparel-work host JSON restore changed its frozen owner journal.");
        ApparelWorkCompletedFactPreflight.Validate(
            new[] { saved.owner },
            saved.ledger,
            saved.migrated);
        ApparelWorkOrderSaveData confirmedOwner = CloneOwner(saved.owner);
        confirmedOwner.terminalPublicationConfirmed = true;
        ApparelWorkCompletedFactPreflight.Validate(
            new[] { confirmedOwner },
            saved.ledger,
            saved.migrated);

        using (var restored = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            restored.Restore(saved.ledger, saved.migrated);
            var binding = new MigratedDomainFactObserverBinding<
                ApparelWorkCompletedFact>(
                "log:apparel-work-completed-restored",
                ApparelWorkCompletedFactMapping.Capture,
                restored.Migrated,
                new GameplayDomainFactCommitGroup(
                    restored.Recorder,
                    restored.Ledger));
            using IDisposable connection = binding.Connect(
                restored.Dispatcher);
            var exact = new ApparelWorkCompletedFact(
                ApparelWorkCompletedData.FromOwner(saved.owner));
            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                restored.Ledger.CaptureGameplayOutcomes());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var replies = new DomainFactDelivery[1];
                Require(restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.publicationKey,
                                exact)
                        },
                        replies,
                        out string failure)
                    && replies[0].Completed
                    && replies[0].OperationKey == saved.publicationKey
                    && OutcomeRowCount(
                        restored.Ledger.CaptureGameplayOutcomes()) == 1
                    && restored.Migrated.Capture().owners.Count == 1,
                    "Apparel-work exact replay " + attempt
                        + " did not reconcile once: " + failure);
            }
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Apparel-work exact replay changed canonical evidence.");

            RequireTamperRejected(
                value => value.terminalAbsoluteDay++,
                "occurrence day");
            RequireTamperRejected(
                value => value.terminalOutcomeItemStates[0]
                    .afterStatePayload += "-changed",
                "item after-state payload");
            RequireTamperRejected(
                value => value.terminalMaterialSourceStackIds[0]
                    = "stack:apparel-host:14:aa",
                "material source stack");
            RequireTamperRejected(
                value => value.contributions[0].relevantSkill += 1f,
                "contribution skill");

            void RequireTamperRejected(
                Action<ApparelWorkOrderSaveData> change,
                string label)
            {
                ApparelWorkOrderSaveData changed = CloneOwner(saved.owner);
                change(changed);
                Require(ApparelWorkCompletedFactMapping.ComputeContextSha256(
                        ApparelWorkCompletedData.FromOwner(changed))
                        != saved.contextHash,
                    "Apparel-work " + label
                        + " change did not alter its context hash.");
                RequireThrows<InvalidOperationException>(
                    () => ApparelWorkCompletedFactPreflight.Validate(
                        new[] { changed },
                        saved.ledger,
                        saved.migrated),
                    "Apparel-work preflight accepted changed " + label + ".");
                var replies = new DomainFactDelivery[1];
                Require(!restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.publicationKey,
                                new ApparelWorkCompletedFact(
                                    ApparelWorkCompletedData.FromOwner(
                                        changed)))
                        },
                        replies,
                        out string failure)
                    && replies[0].Phase
                        == DomainFactDeliveryPhase.Rejected
                    && !string.IsNullOrWhiteSpace(failure),
                    "Apparel-work replay accepted changed " + label + ".");
                RequireCanonicalEqual(
                    canonical,
                    CanonicalHashes(
                        restored.Ledger.CaptureGameplayOutcomes()),
                    "Rejected apparel-work " + label
                        + " replay changed canonical evidence.");
            }
        }

        static ApparelWorkCompletedData CreateData(
            ApparelWorkOrderKind kind,
            int sequence)
        {
            string suffix = sequence.ToString(
                "D2",
                CultureInfo.InvariantCulture);
            bool repair = kind == ApparelWorkOrderKind.Repair;
            return new ApparelWorkCompletedData
            {
                orderId = "apparel-order:host:" + suffix,
                kind = kind,
                facilityInstanceId = "building:apparel-host:" + suffix,
                absoluteDay = 20 + sequence,
                completedWork = 4f,
                materialOperationId = repair
                    ? "material-input:apparel-host:" + suffix
                    : string.Empty,
                materialCommitId = repair
                    ? "material-commit:apparel-host:" + suffix
                    : string.Empty,
                materialSourceStackIds = repair
                    ? new List<string>
                    {
                        "stack:apparel-host:" + suffix + ":a",
                        "stack:apparel-host:" + suffix + ":b"
                    }
                    : new List<string>(),
                materialQuantity = repair ? 3 : 0,
                materialMassGrams = repair ? 1500L : 0L,
                contributions = new List<CraftContributionSaveData>
                {
                    new()
                    {
                        characterId = "character:apparel-host:" + suffix
                            + ":a",
                        contributedWork = 1.25f,
                        relevantSkill = 3f
                    },
                    new()
                    {
                        characterId = "character:apparel-host:" + suffix
                            + ":b",
                        contributedWork = 2.75f,
                        relevantSkill = 4f
                    }
                },
                itemStates = new List<
                    ApparelWorkOrderOutcomeItemStateSaveData>
                {
                    new()
                    {
                        itemInstanceId = "item-instance:apparel-host:"
                            + suffix + ":a",
                        stackId = "stack:apparel-host:item:" + suffix + ":a",
                        apparelDisplayName = "호스트 의복 A " + suffix,
                        beforeStatePayload = "before-a-" + suffix,
                        afterStatePayload = "after-a-" + suffix
                    },
                    new()
                    {
                        itemInstanceId = "item-instance:apparel-host:"
                            + suffix + ":b",
                        stackId = "stack:apparel-host:item:" + suffix + ":b",
                        apparelDisplayName = "호스트 의복 B " + suffix,
                        beforeStatePayload = "before-b-" + suffix,
                        afterStatePayload = "after-b-" + suffix
                    }
                }
            };
        }

        static ApparelWorkOrderSaveData CreateOwner(
            ApparelWorkCompletedData source,
            string publicationKey) => new()
        {
            orderId = source.orderId,
            kind = source.kind,
            state = ApparelWorkOrderState.Completed,
            facilityInstanceId = source.facilityInstanceId,
            contributions = source.contributions.Select(value => new
                CraftContributionSaveData
                {
                    characterId = value.characterId,
                    contributedWork = value.contributedWork,
                    relevantSkill = value.relevantSkill
                }).ToList(),
            requiredWork = source.completedWork,
            completedWork = source.completedWork,
            terminalOutcomeReady = true,
            terminalAbsoluteDay = source.absoluteDay,
            terminalPublicationKey = publicationKey,
            terminalPublicationConfirmed = false,
            terminalMaterialOperationId = source.materialOperationId,
            terminalMaterialCommitId = source.materialCommitId,
            terminalMaterialSourceStackIds = source.materialSourceStackIds
                .ToList(),
            terminalMaterialQuantity = source.materialQuantity,
            terminalMaterialMassGrams = source.materialMassGrams,
            terminalOutcomeItemStates = source.itemStates.Select(
                ApparelWorkCompletedData.CloneItem).ToList()
        };

        static ApparelWorkOrderSaveData CloneOwner(
            ApparelWorkOrderSaveData source) =>
            JsonConvert.DeserializeObject<ApparelWorkOrderSaveData>(
                JsonConvert.SerializeObject(source))
            ?? throw new InvalidOperationException(
                "Apparel-work owner JSON clone returned null.");

        static string ContributorFact(
            IEnumerable<CraftContributionSaveData> values) => string.Join(
            ",",
            values.OrderBy(
                    value => value.characterId,
                    StringComparer.Ordinal)
                .Select(value => value.characterId + "="
                    + value.contributedWork.ToString(
                        "R",
                        CultureInfo.InvariantCulture)));

        static string StateFact(
            IEnumerable<ApparelWorkOrderOutcomeItemStateSaveData> values,
            Func<ApparelWorkOrderOutcomeItemStateSaveData, string> payload) =>
            string.Concat(values
                .OrderBy(value => value.itemInstanceId, StringComparer.Ordinal)
                .Select(value =>
                {
                    string item = value.itemInstanceId + "=" + payload(value);
                    return item.Length.ToString(
                            CultureInfo.InvariantCulture)
                        + ":"
                        + item;
                }));

        static void RequireMetric(
            MigratedProducerOutcomeDetails details,
            GameplayMetricId metricId,
            double expectedValue,
            GameplayMetricUnitId expectedUnit,
            GameplayEntityKindId expectedTargetKind,
            string expectedTargetId)
        {
            GameplayOutcomeMetric metric = details.Metrics.Single(
                value => value.MetricId.Equals(metricId));
            Require(metric.Value == expectedValue
                && metric.UnitId.Equals(expectedUnit)
                && metric.DefinitionOrInstanceId.Kind.Equals(
                    expectedTargetKind)
                && metric.DefinitionOrInstanceId.Value == expectedTargetId,
                "Apparel-work metric changed value, unit, or target: "
                    + metricId.Value);
        }
    }

    private static void VerifyPhysicalCarcassFactCohort()
    {
        PhysicalItemTransformationData physical = CreatePhysical(1);
        var physicalFact = new PhysicalItemTransformedFact(physical);
        MigratedDomainFactCapture physicalCapture =
            PhysicalItemTransformedFactMapping.Capture(physicalFact);
        MigratedProducerOutcomeDetails physicalDetails =
            physicalCapture.Details;
        string physicalHash = PhysicalItemTransformedFactMapping
            .ComputeContextSha256(physical);
        Require(physicalCapture.Kind == MigratedProducerOutcomeKind
                    .PhysicalItemTransformReceipt
                && physicalCapture.OwnerIdentity == physical.operationId
                && physicalCapture.AbsoluteDay == 137
                && physicalCapture.Status == GameplayOutcomeStatus.Succeeded
                && !physicalCapture.ReservationOnly
                && physicalDetails.Subjects.Length == 1
                && physicalDetails.Subjects[0].EntityId.Kind.Equals(
                    MigratedProducerOutcomeIds.OperationKind)
                && physicalDetails.Subjects[0].EntityId.Value
                    == physical.operationId
                && physicalDetails.Subjects[0].DisplayName.DisplayText
                    == physical.operationId
                && physicalDetails.Subjects[0].Role.Equals(
                    MigratedProducerOutcomeIds.OperationRole)
                && physicalDetails.Metrics.Length == 0
                && physicalDetails.Facts.Length == 0
                && physicalDetails.Summary
                    == physical.reasonCode + " 변환으로 입력 2개 (3000g)가 출력 3개 (1800g)로 확정됐다."
                && physicalDetails.Provenance.Length == 1
                && physicalDetails.Provenance[0].KindId
                    == "physical-transform-context-sha256"
                && physicalDetails.Provenance[0].Value == physicalHash
                && IsSha256Reference(physicalHash),
            "Physical transformation mapping changed its legacy identity or summary.");

        WildlifeCarcassProcessingData actorCarcass = CreateCarcass(
            1,
            physical.operationId,
            withActor: true,
            taboo: false);
        RequireWildlifeMapping(
            actorCarcass,
            MigratedProducerOutcomeKind.AnimalHusbandryWorkResult,
            MigratedProducerOutcomeIds.CharacterKind,
            actorCarcass.actorId,
            actorCarcass.actorDisplayName,
            MigratedProducerOutcomeIds.ActorRole,
            StandardCarcassSummary(actorCarcass));
        WildlifeCarcassProcessingData operationCarcass = CreateCarcass(
            2,
            CreatePhysical(2).operationId,
            withActor: false,
            taboo: false);
        RequireWildlifeMapping(
            operationCarcass,
            MigratedProducerOutcomeKind.AnimalHusbandryWorkResult,
            MigratedProducerOutcomeIds.OperationKind,
            operationCarcass.operationId,
            operationCarcass.speciesDisplayName + " 사체 도축",
            MigratedProducerOutcomeIds.OperationRole,
            StandardCarcassSummary(operationCarcass));
        WildlifeCarcassProcessingData tabooCarcass = CreateCarcass(
            3,
            CreatePhysical(3).operationId,
            withActor: true,
            taboo: true);
        RequireWildlifeMapping(
            tabooCarcass,
            MigratedProducerOutcomeKind.CharacterTabooIncidentEvent,
            MigratedProducerOutcomeIds.CharacterKind,
            tabooCarcass.actorId,
            tabooCarcass.actorDisplayName,
            MigratedProducerOutcomeIds.ActorRole,
            "sourceCharacter=" + tabooCarcass.sourceCharacterId
                + "; sourceName=" + tabooCarcass.sourceDisplayName
                + "; sameSpecies=True; produced=3");

        PhysicalItemTransformationData frozenPhysicalSource =
            CreatePhysical(4);
        PhysicalItemTransformationData originalPhysical =
            frozenPhysicalSource.Clone();
        var frozenPhysical = new PhysicalItemTransformedFact(
            frozenPhysicalSource);
        string frozenPhysicalHash = PhysicalItemTransformedFactMapping
            .ComputeContextSha256(frozenPhysicalSource);
        frozenPhysicalSource.inputs[0].quantity = 99;
        frozenPhysicalSource.outputs[0].destinationId
            = "building:warehouse:mutated";
        PhysicalItemTransformationData detachedPhysical =
            frozenPhysical.Snapshot();
        detachedPhysical.outputs[1].quantity = 99;
        Require(JsonConvert.SerializeObject(frozenPhysical.Snapshot())
                == JsonConvert.SerializeObject(originalPhysical)
            && PhysicalItemTransformedFactMapping.Capture(frozenPhysical)
                .Details.Provenance.Single().Value == frozenPhysicalHash,
            "Physical transformation fact retained source or snapshot aliases.");

        WildlifeCarcassProcessingData frozenCarcassSource = CreateCarcass(
            4,
            originalPhysical.operationId,
            withActor: true,
            taboo: false);
        WildlifeCarcassProcessingData originalCarcass =
            frozenCarcassSource.Clone();
        var frozenCarcass = new WildlifeCarcassProcessedFact(
            frozenCarcassSource);
        string frozenCarcassHash = WildlifeCarcassProcessedFactMapping
            .ComputeContextSha256(frozenCarcassSource);
        frozenCarcassSource.actorDisplayName = "변경된 작업자";
        frozenCarcassSource.outputQuantity = 99;
        WildlifeCarcassProcessingData detachedCarcass =
            frozenCarcass.Snapshot();
        detachedCarcass.speciesDisplayName = "변경된 종";
        Require(JsonConvert.SerializeObject(frozenCarcass.Snapshot())
                == JsonConvert.SerializeObject(originalCarcass)
            && WildlifeCarcassProcessedFactMapping.Capture(frozenCarcass)
                .Details.Provenance[0].Value == frozenCarcassHash,
            "Carcass processing fact retained source or snapshot aliases.");

        using (var rejected = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            var commits = new GameplayDomainFactCommitGroup(
                rejected.Recorder,
                rejected.Ledger);
            var physicalBinding = new MigratedDomainFactObserverBinding<
                PhysicalItemTransformedFact>(
                "log:physical-transform-rejected",
                PhysicalItemTransformedFactMapping.Capture,
                rejected.Migrated,
                commits);
            var carcassBinding = new MigratedDomainFactObserverBinding<
                WildlifeCarcassProcessedFact>(
                "log:carcass-processing-rejected",
                WildlifeCarcassProcessedFactMapping.Capture,
                rejected.Migrated,
                commits);
            using IDisposable physicalConnection = physicalBinding.Connect(
                rejected.Dispatcher);
            using IDisposable carcassConnection = carcassBinding.Connect(
                rejected.Dispatcher);
            PhysicalItemTransformationData physicalData = CreatePhysical(5);
            WildlifeCarcassProcessingData carcassData = CreateCarcass(
                5,
                physicalData.operationId,
                withActor: true,
                taboo: false);
            var first = new PhysicalItemTransformedFact(physicalData);
            var second = new WildlifeCarcassProcessedFact(carcassData);
            Require(rejected.Dispatcher.TryReserve(
                    first,
                    out DomainFactLease physicalLease,
                    out string failure),
                "Physical rejection fixture did not reserve: " + failure);
            Require(rejected.Dispatcher.TryReserve(
                    second,
                    out DomainFactLease carcassLease,
                    out failure),
                "Carcass rejection fixture did not reserve: " + failure);
            WildlifeCarcassProcessingData identityDrift = carcassData.Clone();
            identityDrift.operationId
                = "wildlife-carcass:host:preparation-rejected";
            var rejectedResults = new DomainFactDelivery[2];
            bool accepted = rejected.Dispatcher.PublishBatch(
                new[]
                {
                    DomainFactEmission.Create(physicalLease, first),
                    DomainFactEmission.Create(
                        carcassLease,
                        new WildlifeCarcassProcessedFact(identityDrift))
                },
                rejectedResults,
                out string rejectedFailure);
            Require(!accepted
                && rejectedResults.All(value => value.Phase
                    == DomainFactDeliveryPhase.Rejected
                    && !value.MayBeCommitted)
                && rejectedFailure.Contains(
                    "reserved-identity-changed",
                    StringComparison.Ordinal)
                && rejected.Dispatcher.ActiveLeaseCount == 0
                && OutcomeRowCount(
                    rejected.Ledger.CaptureGameplayOutcomes()) == 0
                && rejected.Migrated.Capture().owners.Count == 0,
                "Second-fact preparation rejection retained a partial cohort.");
        }

        using (var normal = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            var commits = new GameplayDomainFactCommitGroup(
                normal.Recorder,
                normal.Ledger);
            var physicalBinding = new MigratedDomainFactObserverBinding<
                PhysicalItemTransformedFact>(
                "log:physical-transform-normal",
                PhysicalItemTransformedFactMapping.Capture,
                normal.Migrated,
                commits);
            var carcassBinding = new MigratedDomainFactObserverBinding<
                WildlifeCarcassProcessedFact>(
                "log:carcass-processing-normal",
                WildlifeCarcassProcessedFactMapping.Capture,
                normal.Migrated,
                commits);
            using IDisposable physicalConnection = physicalBinding.Connect(
                normal.Dispatcher);
            using IDisposable carcassConnection = carcassBinding.Connect(
                normal.Dispatcher);
            PhysicalItemTransformationData physicalData = CreatePhysical(6);
            WildlifeCarcassProcessingData carcassData = CreateCarcass(
                6,
                physicalData.operationId,
                withActor: false,
                taboo: false);
            var first = new PhysicalItemTransformedFact(physicalData);
            var second = new WildlifeCarcassProcessedFact(carcassData);
            Require(normal.Dispatcher.TryReserve(
                    first,
                    out DomainFactLease physicalLease,
                    out string failure),
                "Physical normal fixture did not reserve: " + failure);
            Require(normal.Dispatcher.TryReserve(
                    second,
                    out DomainFactLease carcassLease,
                    out failure),
                "Carcass normal fixture did not reserve: " + failure);
            var results = new DomainFactDelivery[2];
            Require(normal.Dispatcher.PublishBatch(
                    new[]
                    {
                        DomainFactEmission.Create(physicalLease, first),
                        DomainFactEmission.Create(carcassLease, second)
                    },
                    results,
                    out failure)
                && results.All(value => value.Completed)
                && OutcomeRowCount(
                    normal.Ledger.CaptureGameplayOutcomes()) == 2
                && normal.Migrated.Capture().owners.Count == 2,
                "Physical/carcass normal cohort did not commit atomically: "
                    + failure);
        }

        SavedPhysicalCarcassCohortAuthority saved;
        using (var uncertain = new Authority(
                   connect: false,
                   recorderFaults: true))
        {
            var commits = new GameplayDomainFactCommitGroup(
                uncertain.Recorder,
                uncertain.Ledger);
            var physicalBinding = new MigratedDomainFactObserverBinding<
                PhysicalItemTransformedFact>(
                "log:physical-transform-uncertain",
                PhysicalItemTransformedFactMapping.Capture,
                uncertain.Migrated,
                commits);
            var carcassBinding = new MigratedDomainFactObserverBinding<
                WildlifeCarcassProcessedFact>(
                "log:carcass-processing-uncertain",
                WildlifeCarcassProcessedFactMapping.Capture,
                uncertain.Migrated,
                commits);
            using IDisposable physicalConnection = physicalBinding.Connect(
                uncertain.Dispatcher);
            using IDisposable carcassConnection = carcassBinding.Connect(
                uncertain.Dispatcher);
            var responseLoss = new BatchResponseLossFactPublisher(
                uncertain.Dispatcher);
            PhysicalItemTransformationData physicalData = CreatePhysical(7);
            WildlifeCarcassProcessingData carcassData = CreateCarcass(
                7,
                physicalData.operationId,
                withActor: true,
                taboo: false);
            var first = new PhysicalItemTransformedFact(physicalData);
            var second = new WildlifeCarcassProcessedFact(carcassData);
            Require(responseLoss.TryReserve(
                    first,
                    out DomainFactLease physicalLease,
                    out string failure),
                "Physical uncertain fixture did not reserve: " + failure);
            Require(responseLoss.TryReserve(
                    second,
                    out DomainFactLease carcassLease,
                    out failure),
                "Carcass uncertain fixture did not reserve: " + failure);
            string physicalKey = physicalLease.PublicationKey;
            string carcassKey = carcassLease.PublicationKey;
            uncertain.Faults.ThrowAfterNextBatchCommit = true;
            responseLoss.HideNextCompletedBatchResponse(
                physicalKey,
                carcassKey);
            var results = new DomainFactDelivery[2];
            Require(responseLoss.PublishBatch(
                    new[]
                    {
                        DomainFactEmission.Create(physicalLease, first),
                        DomainFactEmission.Create(carcassLease, second)
                    },
                    results,
                    out failure)
                && results.All(value => value.MayBeCommitted
                    && value.Phase == DomainFactDeliveryPhase.InDoubt)
                && results[0].OperationKey == physicalKey
                && results[1].OperationKey == carcassKey
                && uncertain.Faults.AfterCommitFaultCount == 1
                && responseLoss.HiddenCompletedBatchResponseCount == 1
                && OutcomeRowCount(
                    uncertain.Ledger.CaptureGameplayOutcomes()) == 2
                && uncertain.Migrated.Capture().owners.Count == 2,
                "Physical/carcass post-commit fault split or lost its cohort: "
                    + failure);
            saved = JsonConvert.DeserializeObject<
                SavedPhysicalCarcassCohortAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedPhysicalCarcassCohortAuthority
                        {
                            serializer = HostSerializer,
                            physical = new PhysicalItemTransformationSaveData
                            {
                                fact = first.Snapshot(),
                                publicationKey = physicalKey,
                                companionPublicationKey = carcassKey
                            },
                            carcass = new WildlifeCarcassProcessingSaveData
                            {
                                fact = second.Snapshot(),
                                publicationKey = carcassKey
                            },
                            physicalContextHash =
                                PhysicalItemTransformedFactMapping
                                    .ComputeContextSha256(physicalData),
                            carcassContextHash =
                                WildlifeCarcassProcessedFactMapping
                                    .ComputeContextSha256(carcassData),
                            ledger = uncertain.Ledger
                                .CaptureGameplayOutcomes(),
                            migrated = uncertain.Migrated.Capture()
                        }))
                ?? throw new InvalidOperationException(
                    "Physical/carcass host JSON round-trip returned null.");
        }

        saved.physical.Validate();
        saved.carcass.Validate();
        Require(saved.serializer == HostSerializer
            && saved.physical.publicationKey.Length > 0
            && saved.carcass.publicationKey.Length > 0
            && !saved.physical.publicationConfirmed
            && !saved.physical.rollbackBlocked
            && saved.physical.publicationKey
                != saved.carcass.publicationKey
            && saved.physical.companionPublicationKey
                == saved.carcass.publicationKey
            && PhysicalItemTransformedFactMapping.ComputeContextSha256(
                saved.physical.fact) == saved.physicalContextHash
            && WildlifeCarcassProcessedFactMapping.ComputeContextSha256(
                saved.carcass.fact) == saved.carcassContextHash,
            "Physical/carcass JSON restore changed frozen facts or opaque keys.");

        using (var restored = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            restored.Restore(saved.ledger, saved.migrated);
            var commits = new GameplayDomainFactCommitGroup(
                restored.Recorder,
                restored.Ledger);
            var physicalBinding = new MigratedDomainFactObserverBinding<
                PhysicalItemTransformedFact>(
                "log:physical-transform-restored",
                PhysicalItemTransformedFactMapping.Capture,
                restored.Migrated,
                commits);
            var carcassBinding = new MigratedDomainFactObserverBinding<
                WildlifeCarcassProcessedFact>(
                "log:carcass-processing-restored",
                WildlifeCarcassProcessedFactMapping.Capture,
                restored.Migrated,
                commits);
            using IDisposable physicalConnection = physicalBinding.Connect(
                restored.Dispatcher);
            using IDisposable carcassConnection = carcassBinding.Connect(
                restored.Dispatcher);
            var exactPhysical = new PhysicalItemTransformedFact(
                saved.physical.fact);
            var exactCarcass = new WildlifeCarcassProcessedFact(
                saved.carcass.fact);
            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                restored.Ledger.CaptureGameplayOutcomes());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var results = new DomainFactDelivery[2];
                Require(restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.physical.publicationKey,
                                exactPhysical),
                            DomainFactReplay.Create(
                                saved.carcass.publicationKey,
                                exactCarcass)
                        },
                        results,
                        out string failure)
                    && results.All(value => value.Completed)
                    && OutcomeRowCount(
                        restored.Ledger.CaptureGameplayOutcomes()) == 2
                    && restored.Migrated.Capture().owners.Count == 2,
                    "Physical/carcass exact replay " + attempt
                        + " did not reconcile once: " + failure);
            }
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Physical/carcass exact replay changed canonical evidence.");

            RequireTamperRejected(
                value =>
                {
                    value.inputQuantity = 3;
                    value.inputs[0].quantity = 3;
                    value.inputMassGrams = 3400L;
                    value.outputQuantity = 4;
                    value.outputs[1].quantity = 3;
                    value.outputMassGrams = 1900L;
                },
                null,
                "physical quantities and masses");
            RequireTamperRejected(
                null,
                value => value.transformOperationId
                    = "physical-transform:host:parent-changed",
                "carcass parent operation");

            void RequireTamperRejected(
                Action<PhysicalItemTransformationData> physicalChange,
                Action<WildlifeCarcassProcessingData> carcassChange,
                string label)
            {
                PhysicalItemTransformationData changedPhysical =
                    saved.physical.fact.Clone();
                WildlifeCarcassProcessingData changedCarcass =
                    saved.carcass.fact.Clone();
                physicalChange?.Invoke(changedPhysical);
                carcassChange?.Invoke(changedCarcass);
                Require(PhysicalItemTransformedFactMapping
                            .ComputeContextSha256(changedPhysical)
                        != saved.physicalContextHash
                    || WildlifeCarcassProcessedFactMapping
                            .ComputeContextSha256(changedCarcass)
                        != saved.carcassContextHash,
                    "Physical/carcass " + label
                        + " change did not alter context provenance.");
                var results = new DomainFactDelivery[2];
                Require(!restored.Dispatcher.ReplayBatch(
                        new[]
                        {
                            DomainFactReplay.Create(
                                saved.physical.publicationKey,
                                new PhysicalItemTransformedFact(
                                    changedPhysical)),
                            DomainFactReplay.Create(
                                saved.carcass.publicationKey,
                                new WildlifeCarcassProcessedFact(
                                    changedCarcass))
                        },
                        results,
                        out string failure)
                    && results.All(value => value.Phase
                        == DomainFactDeliveryPhase.Rejected
                        && !value.MayBeCommitted)
                    && !string.IsNullOrWhiteSpace(failure),
                    "Physical/carcass replay accepted changed " + label + ".");
                RequireCanonicalEqual(
                    canonical,
                    CanonicalHashes(
                        restored.Ledger.CaptureGameplayOutcomes()),
                    "Rejected physical/carcass " + label
                        + " replay changed canonical evidence.");
            }
        }

        static PhysicalItemTransformationData CreatePhysical(int sequence)
        {
            string suffix = sequence.ToString(
                "D2",
                CultureInfo.InvariantCulture);
            return new PhysicalItemTransformationData
            {
                operationId = "physical-transform:host:" + suffix,
                reasonCode = "reason:carcass-processing",
                absoluteDay = 137,
                wholeStack = true,
                inputQuantity = 2,
                inputMassGrams = 3000L,
                outputMassGrams = 1800L,
                outputQuantity = 3,
                inputs = new List<PhysicalItemTransformationInputData>
                {
                    new()
                    {
                        stackId = "stack:carcass-host:" + suffix,
                        itemId = "item:carcass-host:whole",
                        itemInstanceId = "item-instance:carcass-host:"
                            + suffix,
                        quantity = 2
                    }
                },
                outputs = new List<PhysicalItemTransformationOutputData>
                {
                    new()
                    {
                        itemId = "item:carcass-host:bone",
                        quantity = 1,
                        x = 7,
                        y = 9,
                        state = (int)WorldItemStackState.Stored,
                        destinationId = "building:warehouse:host"
                    },
                    new()
                    {
                        itemId = "item:carcass-host:meat",
                        quantity = 2,
                        x = 7,
                        y = 9,
                        state = (int)WorldItemStackState.Stored,
                        destinationId = "building:warehouse:host"
                    }
                }
            };
        }

        static WildlifeCarcassProcessingData CreateCarcass(
            int sequence,
            string transformOperationId,
            bool withActor,
            bool taboo)
        {
            string suffix = sequence.ToString(
                "D2",
                CultureInfo.InvariantCulture);
            return new WildlifeCarcassProcessingData
            {
                operationId = "wildlife-carcass:host:" + suffix
                    + (taboo ? ":taboo" : string.Empty),
                transformOperationId = transformOperationId,
                absoluteDay = 137,
                taboo = taboo,
                actorId = withActor
                    ? "character:carcass-host:" + suffix
                    : string.Empty,
                actorDisplayName = withActor
                    ? "호스트 도축 작업자 " + suffix
                    : string.Empty,
                speciesId = taboo
                    ? string.Empty
                    : "species:carcass-host:boar",
                speciesDisplayName = taboo ? string.Empty : "호스트 멧돼지",
                carcassStackId = "stack:carcass-host:" + suffix,
                facilityId = "building:butcher-host:" + suffix,
                inputQuantity = 2,
                outputQuantity = 3,
                sourceCharacterId = taboo
                    ? "character:carcass-host:source-" + suffix
                    : string.Empty,
                sourceDisplayName = taboo
                    ? "호스트 사체 원천 " + suffix
                    : string.Empty,
                sameSpecies = taboo
            };
        }

        static void RequireWildlifeMapping(
            WildlifeCarcassProcessingData data,
            MigratedProducerOutcomeKind expectedKind,
            GameplayEntityKindId expectedSubjectKind,
            string expectedSubjectId,
            string expectedDisplayName,
            GameplayRoleId expectedRole,
            string expectedSummary)
        {
            MigratedDomainFactCapture capture =
                WildlifeCarcassProcessedFactMapping.Capture(
                    new WildlifeCarcassProcessedFact(data));
            MigratedProducerOutcomeDetails details = capture.Details;
            string contextHash = WildlifeCarcassProcessedFactMapping
                .ComputeContextSha256(data);
            Require(capture.Kind == expectedKind
                && capture.OwnerIdentity == data.operationId
                && capture.AbsoluteDay == 137
                && capture.Status == GameplayOutcomeStatus.Succeeded
                && !capture.ReservationOnly
                && details.Subjects.Length == 1
                && details.Subjects[0].EntityId.Kind.Equals(
                    expectedSubjectKind)
                && details.Subjects[0].EntityId.Value == expectedSubjectId
                && details.Subjects[0].DisplayName.DisplayText
                    == expectedDisplayName
                && details.Subjects[0].Role.Equals(expectedRole)
                && details.Metrics.Length == 0
                && details.Facts.Length == 0
                && details.Summary == expectedSummary
                && details.Provenance.Length == 2
                && details.Provenance[0].KindId
                    == "carcass-processing-context-sha256"
                && details.Provenance[0].Value == contextHash
                && IsSha256Reference(contextHash)
                && details.Provenance[1].KindId == "domain-commit"
                && details.Provenance[1].Value == data.operationId,
                "Carcass mapping changed its kind, subject, summary, or provenance: "
                    + data.operationId);
        }

        static string StandardCarcassSummary(
            WildlifeCarcassProcessingData data) =>
            "event=carcass-butchered;speciesId=" + data.speciesId
            + ";speciesName=" + data.speciesDisplayName
            + ";carcassStackId=" + data.carcassStackId
            + ";facilityId=" + (data.facilityId.Length > 0
                ? data.facilityId
                : "none")
            + ";inputQuantity=" + data.inputQuantity.ToString(
                CultureInfo.InvariantCulture)
            + ";outputQuantity=" + data.outputQuantity.ToString(
                CultureInfo.InvariantCulture);

        static bool IsSha256Reference(string value) => value?.Length == 71
            && value.StartsWith("sha256:", StringComparison.Ordinal)
            && value.Skip(7).All(Uri.IsHexDigit);
    }

    private static void VerifySurvivalMealMissedFactCohort()
    {
        SurvivalMealMissedData source = CreateData(
            "character:meal-missed-host:single",
            absoluteDay: 41,
            consecutiveMisses: 3,
            cohortIndex: 0,
            cohortSize: 1);
        var fact = new SurvivalMealMissedFact(source);
        MigratedDomainFactCapture capture =
            SurvivalMealMissedFactMapping.Capture(fact);
        MigratedProducerOutcomeDetails details = capture.Details;
        string contextHash = SurvivalMealMissedFactMapping
            .ComputeContextSha256(source);
        Require(capture.Kind == MigratedProducerOutcomeKind.MealMissedEvent
                && capture.OwnerIdentity
                    == "meal-missed@1|41|40|character:meal-missed-host:single|3"
                && capture.AbsoluteDay == 41
                && capture.Status == GameplayOutcomeStatus.Succeeded
                && !capture.ReservationOnly
                && details.Subjects.Length == 1
                && details.Subjects[0].EntityId.Kind.Equals(
                    MigratedProducerOutcomeIds.CharacterKind)
                && details.Subjects[0].EntityId.Value
                    == source.characterId
                && details.Subjects[0].DisplayName.DisplayText
                    == source.characterId
                && details.Subjects[0].Role.Equals(
                    MigratedProducerOutcomeIds.ActorRole)
                && details.Metrics.Length == 0
                && details.Facts.Length == 0
                && details.Summary
                    == "meal-missed@1|character:meal-missed-host:single|40|3"
                && details.Provenance.Length == 1
                && details.Provenance[0].KindId
                    == "meal-missed-context-sha256"
                && details.Provenance[0].Value == contextHash
                && contextHash.Length == 71
                && contextHash.StartsWith("sha256:", StringComparison.Ordinal)
                && contextHash.Skip(7).All(Uri.IsHexDigit),
            "Meal-missed mapping changed its legacy identity, subject, summary, or context.");

        SurvivalMealMissedData original = source.Clone();
        source.characterId = "character:meal-missed-host:mutated";
        source.consecutiveMisses = 99;
        SurvivalMealMissedData detached = fact.Snapshot();
        detached.completedDay = 1;
        detached.cohortSize = 99;
        Require(JsonConvert.SerializeObject(fact.Snapshot())
                == JsonConvert.SerializeObject(original)
            && SurvivalMealMissedFactMapping.Capture(fact)
                .Details.Provenance.Single().Value == contextHash,
            "Meal-missed fact retained source or snapshot aliases.");
        SurvivalMealMissedData invalidDay = original.Clone();
        invalidDay.absoluteDay = 1;
        invalidDay.completedDay = 0;
        RequireThrows<InvalidOperationException>(
            () => new SurvivalMealMissedFact(invalidDay),
            "Meal-missed fact accepted an invalid occurrence day.");
        SurvivalMealMissedData mismatchedDay = original.Clone();
        mismatchedDay.completedDay = mismatchedDay.absoluteDay;
        RequireThrows<InvalidOperationException>(
            () => new SurvivalMealMissedFact(mismatchedDay),
            "Meal-missed fact accepted a mismatched completed day.");
        SurvivalMealMissedData invalidIndex = original.Clone();
        invalidIndex.cohortIndex = invalidIndex.cohortSize;
        RequireThrows<InvalidOperationException>(
            () => new SurvivalMealMissedFact(invalidIndex),
            "Meal-missed fact accepted an out-of-range cohort index.");

        using (var rejected = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            using IDisposable connection = Connect(rejected);
            SurvivalMealMissedData[] cohort = CreateCohort(51);
            var first = new SurvivalMealMissedFact(cohort[0]);
            var second = new SurvivalMealMissedFact(cohort[1]);
            Require(rejected.Dispatcher.TryReserve(
                    first,
                    out DomainFactLease firstLease,
                    out string failure),
                "First rejected meal-missed fact did not reserve: "
                    + failure);
            Require(rejected.Dispatcher.TryReserve(
                    second,
                    out DomainFactLease secondLease,
                    out failure),
                "Second rejected meal-missed fact did not reserve: "
                    + failure);
            SurvivalMealMissedData identityDrift = cohort[1].Clone();
            identityDrift.absoluteDay++;
            identityDrift.completedDay++;
            var results = new DomainFactDelivery[2];
            Require(!rejected.Dispatcher.PublishBatch(
                    new[]
                    {
                        DomainFactEmission.Create(firstLease, first),
                        DomainFactEmission.Create(
                            secondLease,
                            new SurvivalMealMissedFact(identityDrift))
                    },
                    results,
                    out failure)
                && results.All(value => value.Phase
                    == DomainFactDeliveryPhase.Rejected
                    && !value.MayBeCommitted)
                && failure.Contains(
                    "reserved-identity-changed",
                    StringComparison.Ordinal)
                && rejected.Dispatcher.ActiveLeaseCount == 0
                && OutcomeRowCount(
                    rejected.Ledger.CaptureGameplayOutcomes()) == 0
                && rejected.Migrated.Capture().owners.Count == 0,
                "Meal-missed second preparation rejection retained a partial cohort.");
        }

        using (var normal = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            using IDisposable connection = Connect(normal);
            SurvivalMealMissedData[] cohort = CreateCohort(52);
            var first = new SurvivalMealMissedFact(cohort[0]);
            var second = new SurvivalMealMissedFact(cohort[1]);
            Require(normal.Dispatcher.TryReserve(
                    first,
                    out DomainFactLease firstLease,
                    out string failure),
                "First normal meal-missed fact did not reserve: " + failure);
            Require(normal.Dispatcher.TryReserve(
                    second,
                    out DomainFactLease secondLease,
                    out failure),
                "Second normal meal-missed fact did not reserve: " + failure);
            var results = new DomainFactDelivery[2];
            Require(normal.Dispatcher.PublishBatch(
                    new[]
                    {
                        DomainFactEmission.Create(firstLease, first),
                        DomainFactEmission.Create(secondLease, second)
                    },
                    results,
                    out failure)
                && results.All(value => value.Completed)
                && OutcomeRowCount(
                    normal.Ledger.CaptureGameplayOutcomes()) == 2
                && normal.Migrated.Capture().owners.Count == 2,
                "Meal-missed normal cohort did not commit atomically: "
                    + failure);
        }

        SavedSurvivalMealMissedCohortAuthority saved;
        using (var uncertain = new Authority(
                   connect: false,
                   recorderFaults: true))
        {
            using IDisposable connection = Connect(uncertain);
            var responseLoss = new BatchResponseLossFactPublisher(
                uncertain.Dispatcher);
            SurvivalMealMissedData[] cohort = CreateCohort(53);
            var first = new SurvivalMealMissedFact(cohort[0]);
            var second = new SurvivalMealMissedFact(cohort[1]);
            Require(responseLoss.TryReserve(
                    first,
                    out DomainFactLease firstLease,
                    out string failure),
                "First uncertain meal-missed fact did not reserve: "
                    + failure);
            Require(responseLoss.TryReserve(
                    second,
                    out DomainFactLease secondLease,
                    out failure),
                "Second uncertain meal-missed fact did not reserve: "
                    + failure);
            string firstKey = firstLease.PublicationKey;
            string secondKey = secondLease.PublicationKey;
            uncertain.Faults.ThrowAfterNextBatchCommit = true;
            responseLoss.HideNextCompletedBatchResponse(firstKey, secondKey);
            var results = new DomainFactDelivery[2];
            Require(responseLoss.PublishBatch(
                    new[]
                    {
                        DomainFactEmission.Create(firstLease, first),
                        DomainFactEmission.Create(secondLease, second)
                    },
                    results,
                    out failure)
                && results.All(value => value.Phase
                    == DomainFactDeliveryPhase.InDoubt
                    && value.MayBeCommitted)
                && results[0].OperationKey == firstKey
                && results[1].OperationKey == secondKey
                && uncertain.Faults.AfterCommitFaultCount == 1
                && responseLoss.HiddenCompletedBatchResponseCount == 1
                && OutcomeRowCount(
                    uncertain.Ledger.CaptureGameplayOutcomes()) == 2
                && uncertain.Migrated.Capture().owners.Count == 2,
                "Meal-missed explicit response loss split or lost its cohort: "
                    + failure);
            saved = JsonConvert.DeserializeObject<
                SavedSurvivalMealMissedCohortAuthority>(
                    JsonConvert.SerializeObject(
                        new SavedSurvivalMealMissedCohortAuthority
                        {
                            serializer = HostSerializer,
                            cohort = new SurvivalMealMissedCohortSaveData
                            {
                                absoluteDay = 53,
                                publicationConfirmed = false,
                                members = new List<
                                    SurvivalMealMissedPublicationSaveData>
                                {
                                    new()
                                    {
                                        fact = first.Snapshot(),
                                        publicationKey = firstKey
                                    },
                                    new()
                                    {
                                        fact = second.Snapshot(),
                                        publicationKey = secondKey
                                    }
                                }
                            },
                            firstContextHash =
                                SurvivalMealMissedFactMapping
                                    .ComputeContextSha256(cohort[0]),
                            secondContextHash =
                                SurvivalMealMissedFactMapping
                                    .ComputeContextSha256(cohort[1]),
                            ledger = uncertain.Ledger
                                .CaptureGameplayOutcomes(),
                            migrated = uncertain.Migrated.Capture()
                        }))
                ?? throw new InvalidOperationException(
                    "Meal-missed host JSON round-trip returned null.");
        }

        saved.cohort.Validate();
        Require(saved.serializer == HostSerializer
            && !saved.cohort.publicationConfirmed
            && saved.cohort.absoluteDay == 53
            && saved.cohort.members.Count == 2
            && saved.cohort.members[0].fact.characterId
                == "character:meal-missed-host:a"
            && saved.cohort.members[1].fact.characterId
                == "character:meal-missed-host:b"
            && saved.cohort.members[0].fact.cohortIndex == 0
            && saved.cohort.members[1].fact.cohortIndex == 1
            && saved.cohort.members.All(value =>
                value.fact.cohortSize == 2)
            && SurvivalMealMissedFactMapping.ComputeContextSha256(
                saved.cohort.members[0].fact) == saved.firstContextHash
            && SurvivalMealMissedFactMapping.ComputeContextSha256(
                saved.cohort.members[1].fact) == saved.secondContextHash,
            "Meal-missed JSON restore changed its sorted frozen cohort or keys.");

        using (var restored = new Authority(
                   connect: false,
                   recorderFaults: false))
        {
            restored.Restore(saved.ledger, saved.migrated);
            using IDisposable connection = Connect(restored);
            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                restored.Ledger.CaptureGameplayOutcomes());
            for (int attempt = 0; attempt < 2; attempt++)
            {
                var results = new DomainFactDelivery[2];
                Require(restored.Dispatcher.ReplayBatch(
                        Replays(
                            saved.cohort.members[0].fact,
                            saved.cohort.members[1].fact),
                        results,
                        out string failure)
                    && results.All(value => value.Completed)
                    && OutcomeRowCount(
                        restored.Ledger.CaptureGameplayOutcomes()) == 2
                    && restored.Migrated.Capture().owners.Count == 2,
                    "Meal-missed exact replay " + attempt
                        + " did not reconcile once: " + failure);
            }
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(restored.Ledger.CaptureGameplayOutcomes()),
                "Meal-missed exact replay changed canonical evidence.");

            RequireTamperRejected(
                value => value.cohortIndex = 1,
                "cohort index");
            RequireTamperRejected(
                value => value.cohortSize = 3,
                "cohort size");
            RequireTamperRejected(
                value =>
                {
                    value.absoluteDay++;
                    value.completedDay++;
                },
                "day");

            void RequireTamperRejected(
                Action<SurvivalMealMissedData> change,
                string label)
            {
                SurvivalMealMissedData first = saved.cohort.members[0]
                    .fact.Clone();
                SurvivalMealMissedData second = saved.cohort.members[1]
                    .fact.Clone();
                change(first);
                Require(SurvivalMealMissedFactMapping.ComputeContextSha256(
                        first) != saved.firstContextHash,
                    "Meal-missed " + label
                        + " change did not alter context provenance.");
                var results = new DomainFactDelivery[2];
                Require(!restored.Dispatcher.ReplayBatch(
                        Replays(first, second),
                        results,
                        out string failure)
                    && results.All(value => value.Phase
                        == DomainFactDeliveryPhase.Rejected
                        && !value.MayBeCommitted)
                    && !string.IsNullOrWhiteSpace(failure),
                    "Meal-missed replay accepted changed " + label + ".");
                RequireCanonicalEqual(
                    canonical,
                    CanonicalHashes(
                        restored.Ledger.CaptureGameplayOutcomes()),
                    "Rejected meal-missed " + label
                        + " replay changed canonical evidence.");
            }

            DomainFactReplay[] Replays(
                SurvivalMealMissedData first,
                SurvivalMealMissedData second) => new[]
            {
                DomainFactReplay.Create(
                    saved.cohort.members[0].publicationKey,
                    new SurvivalMealMissedFact(first)),
                DomainFactReplay.Create(
                    saved.cohort.members[1].publicationKey,
                    new SurvivalMealMissedFact(second))
            };
        }

        static SurvivalMealMissedData CreateData(
            string characterId,
            int absoluteDay,
            int consecutiveMisses,
            int cohortIndex,
            int cohortSize) => new()
        {
            characterId = characterId,
            absoluteDay = absoluteDay,
            completedDay = absoluteDay - 1,
            consecutiveMisses = consecutiveMisses,
            cohortIndex = cohortIndex,
            cohortSize = cohortSize
        };

        static SurvivalMealMissedData[] CreateCohort(int absoluteDay) =>
            new[]
            {
                CreateData(
                    "character:meal-missed-host:a",
                    absoluteDay,
                    consecutiveMisses: 2,
                    cohortIndex: 0,
                    cohortSize: 2),
                CreateData(
                    "character:meal-missed-host:b",
                    absoluteDay,
                    consecutiveMisses: 4,
                    cohortIndex: 1,
                    cohortSize: 2)
            };

        static IDisposable Connect(Authority authority)
        {
            var binding = new MigratedDomainFactObserverBinding<
                SurvivalMealMissedFact>(
                "log:survival-meal-missed",
                SurvivalMealMissedFactMapping.Capture,
                authority.Migrated,
                new GameplayDomainFactCommitGroup(
                    authority.Recorder,
                    authority.Ledger));
            return binding.Connect(authority.Dispatcher);
        }
    }

    private static string SpeciesPayloadFact(
        in EnvironmentOutcomePayload payload,
        GameplayOutcomeFactId factId)
    {
        string result = null;
        for (int index = 0; index < payload.Facts.Count; index++)
        {
            GameplayOutcomeFact value = payload.Facts.Get(index);
            if (value.FactId.Value != factId.Value)
                continue;
            Require(result == null,
                "Species payload contained a duplicate fact: "
                    + factId.Value);
            result = value.Value;
        }
        return result
            ?? throw new InvalidOperationException(
                "Species payload fact was missing: " + factId.Value);
    }

    private static CharacterSpeciesRuntimeRecordSaveData
        CreateGolemSpeciesOwner(GolemUpkeepFactSaveData fact) => new()
    {
        characterInstanceId = fact.characterId,
        speciesDefinitionId = fact.speciesId,
        charge = fact.chargeAfter,
        integrity = fact.integrityAfter,
        rechargeWorkerId = fact.characterId,
        rechargeFacilityId = "building:species-host:charger",
        rechargeMaterialStackId = "stack:species-host:charge",
        rechargeProgressWork = 100f,
        rechargeOperationId = fact.operationId,
        rechargeEpisodeRevision = fact.revision,
        rechargeMaterialCommitted = true,
        rechargeChargeApplied = true,
        rechargeObservationCompleted = true,
        rechargePhysicalEvidence = fact.physicalEvidence,
        rechargeChargeBefore = fact.chargeBefore,
        rechargeRequestedCharge = fact.chargeAfter - fact.chargeBefore,
        golemEpisodeRevision = fact.revision
    };

    private static SavedSpeciesActionAuthority CloneSpeciesActionAuthority(
        SavedSpeciesActionAuthority source) =>
        JsonConvert.DeserializeObject<SavedSpeciesActionAuthority>(
            JsonConvert.SerializeObject(source))
        ?? throw new InvalidOperationException(
            "Species action host JSON clone returned null.");

    private static DungeonGameRestoreReport ValidateSpeciesActionCanonical(
        CharacterSpeciesRuntimeSaveData source,
        GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo method = typeof(SpeciesActionFactPreflight).GetMethod(
            "Validate",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(CharacterSpeciesRuntimeSaveData),
                typeof(GameplayOutcomeLedgerSaveData),
                typeof(DungeonGameRestoreReport)
            },
            modifiers: null)
            ?? throw new MissingMethodException(
                typeof(SpeciesActionFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        method.Invoke(null, new object[] { source, ledger, report });
        return report;
    }

    private static DungeonGameRestoreReport ValidateWorkAccidentCanonical(
        DungeonWorkOrderSaveData source,
        GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo method = typeof(WorkAccidentFactPreflight).GetMethod(
            "Validate",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(DungeonWorkOrderSaveData),
                typeof(GameplayOutcomeLedgerSaveData),
                typeof(DungeonGameRestoreReport)
            },
            modifiers: null)
            ?? throw new MissingMethodException(
                typeof(WorkAccidentFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        method.Invoke(null, new object[] { source, ledger, report });
        return report;
    }

    private static void ValidateWorkAccidentRaw(string json)
    {
        MethodInfo method = typeof(WorkOrdersSaveSection).GetMethod(
            "ValidateAccidentFields",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null)
            ?? throw new MissingMethodException(
                typeof(WorkOrdersSaveSection).FullName,
                "ValidateAccidentFields");
        try
        {
            method.Invoke(null, new object[] { json });
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is InvalidOperationException inner)
        {
            throw new InvalidOperationException(inner.Message, inner);
        }
    }

    private static void StartEmergencySuspensionAlert(
        SettlementAlertRuntime runtime,
        string suffix)
    {
        EmergencyAccountingResult started = runtime.PublishIncidentSignal(
            new SettlementIncidentSignal(
                "incident:emergency-suspension-host:" + suffix,
                SettlementThreatAlertLevel.Amber,
                1L,
                "host",
                "managed emergency suspension observer test"));
        Require(started.Success && runtime.Capture().AlertEpochId > 0L,
            "Emergency suspension host alert did not start: "
                + started.Code + ":" + started.Message);
    }

    private static SettlementThreatAlertSaveData
        CloneEmergencyWorkSuspensionSave(SettlementThreatAlertSaveData source) =>
        JsonConvert.DeserializeObject<SettlementThreatAlertSaveData>(
            JsonConvert.SerializeObject(source))
        ?? throw new InvalidOperationException(
            "Emergency suspension save host JSON clone returned null.");

    private static DungeonGameRestoreReport
        ValidateEmergencyWorkSuspensionCanonical(
            SettlementThreatAlertSaveData source,
            GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo method = typeof(EmergencyWorkSuspensionFactPreflight)
            .GetMethod(
                "Validate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(SettlementThreatAlertSaveData),
                    typeof(GameplayOutcomeLedgerSaveData),
                    typeof(DungeonGameRestoreReport)
                },
                modifiers: null)
            ?? throw new MissingMethodException(
                typeof(EmergencyWorkSuspensionFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        method.Invoke(null, new object[] { source, ledger, report });
        return report;
    }

    private static void VerifyCharacterSkillActivationFactObserver()
    {
        VerifyCharacterSkillActivationFactFreeze();
        VerifyCharacterSkillActivationReceiptParity();
        VerifyCharacterSkillActivationBindingAndValidation();
        VerifyCharacterSkillActivationAtomicReplay();
    }

    private static void VerifyCharacterSkillActivationPublicationRuntime()
    {
        VerifyStandaloneSkillPublicationAndRestore();
        VerifyStandaloneSkillPublicationRejection();
        VerifyStandaloneSkillPublicationAmbiguity(afterCommit: false);
        VerifyStandaloneSkillPublicationAmbiguity(afterCommit: true);
    }

    private static void VerifyStandaloneSkillPublicationAndRestore()
    {
        SavedSkillPublicationAuthority saved;
        using (var authority = new SkillAuthority(
                   connect: true,
                   recorderFaults: false))
        {
            var fact = new CharacterSkillActivationFact(
                CreateSkillActivationData(
                    CharacterSkillActivationKind.ManualDungeon,
                    "publication-normal",
                    revision: 1L));
            Require(authority.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Standalone skill publication did not reserve: " + failure);
            string publicationKey = lease.PublicationKey;
            Require(authority.Publications.TryBegin(
                    fact.SourceCharacterId,
                    out failure),
                "Standalone skill publication did not begin: " + failure);
            DomainFactDelivery delivered = authority.Publications.PublishApplied(
                fact,
                lease);
            authority.Publications.End(fact.SourceCharacterId);
            CharacterSkillActivationPublicationData pendingOwner =
                authority.Publications.Find(
                    fact.SourceCharacterId,
                    fact.OwnerRevision);
            Require(delivered.Completed
                && pendingOwner != null
                && !pendingOwner.pending
                && !pendingOwner.ownerFinalized
                && pendingOwner.publicationKey == publicationKey
                && authority.Publications.PendingOwners().Count == 1,
                "Delivered standalone skill was not retained for owner finalization.");

            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                authority.Ledger.CaptureGameplayOutcomes());
            CharacterSkillActivationFactData alteredData = fact.Snapshot();
            alteredData.skillDisplayName += " 변조";
            var replies = new DomainFactDelivery[1];
            Require(!authority.Dispatcher.ReplayBatch(
                    new[]
                    {
                        DomainFactReplay.Create(
                            publicationKey,
                            new CharacterSkillActivationFact(alteredData))
                    },
                    replies,
                    out failure)
                && replies[0].Phase == DomainFactDeliveryPhase.Rejected
                && !string.IsNullOrWhiteSpace(failure),
                "Standalone skill replay accepted a tampered delivered fact.");
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(authority.Ledger.CaptureGameplayOutcomes()),
                "Rejected standalone skill replay changed canonical evidence.");

            authority.Publications.CompleteOwner(
                fact.SourceCharacterId,
                fact.OwnerRevision);
            Require(authority.Publications.PendingOwners().Count == 0
                && authority.Publications.PendingForSource(
                    fact.SourceCharacterId) == null,
                "Standalone skill owner finalization did not clear pending ownership.");
            CharacterSkillActivationPublicationSaveData detached =
                authority.Publications.Capture();
            saved = JsonConvert.DeserializeObject<SavedSkillPublicationAuthority>(
                JsonConvert.SerializeObject(new SavedSkillPublicationAuthority
                {
                    serializer = HostSerializer,
                    publications = detached,
                    ledger = authority.Ledger.CaptureGameplayOutcomes()
                })) ?? throw new InvalidOperationException(
                    "Standalone skill publication host JSON returned null.");
            detached.observations[0].fact.skillDisplayName =
                "detached mutation";
            Require(authority.Publications.Capture().observations[0]
                    .fact.skillDisplayName != "detached mutation",
                "Standalone skill publication capture retained a journal alias.");
        }

        using var restored = new SkillAuthority(
            connect: true,
            recorderFaults: false);
        restored.Restore(saved.ledger, saved.publications);
        CharacterSkillActivationPublicationSaveData restoredJournal =
            restored.Publications.Capture();
        GameplayOutcomeLedgerSaveData restoredLedger =
            restored.Ledger.CaptureGameplayOutcomes();
        CharacterSkillActivationPublicationData restoredRow =
            restoredJournal.observations.Single();
        Require(saved.serializer == HostSerializer
            && restoredRow.ownerFinalized
            && !restoredRow.pending
            && restoredRow.fact.skillDisplayName != "detached mutation"
            && OutcomeRowCount(restoredLedger) == 1,
            "Standalone skill publication did not restore its detached checkpoint.");
        restoredRow.fact.recipients[0].afterState = "restored mutation";
        Require(restored.Publications.Find(
                restoredRow.fact.sourceCharacterId,
                restoredRow.fact.ownerRevision).fact.recipients[0].afterState
                != "restored mutation",
            "Standalone skill publication lookup retained a journal alias.");

        restoredJournal = restored.Publications.Capture();
        Require(ValidateSkillPublicationCanonical(
                restoredJournal,
                restoredLedger).Success,
            "Restored standalone skill canonical join was rejected.");
        CharacterSkillActivationPublicationSaveData tampered =
            JsonConvert.DeserializeObject<
                CharacterSkillActivationPublicationSaveData>(
                JsonConvert.SerializeObject(restoredJournal));
        tampered.observations[0].fact.skillDisplayName += " preflight 변조";
        Require(!ValidateSkillPublicationCanonical(
                tampered,
                restoredLedger).Success,
            "Standalone skill preflight accepted a tampered frozen fact.");
        Require(!ValidateSkillPublicationCanonical(
                new CharacterSkillActivationPublicationSaveData(),
                restoredLedger).Success,
            "Standalone skill preflight accepted canonical history without its journal row.");
    }

    private static void VerifyStandaloneSkillPublicationRejection()
    {
        using var authority = new SkillAuthority(
            connect: true,
            recorderFaults: false);
        var fact = new CharacterSkillActivationFact(
            CreateSkillActivationData(
                CharacterSkillActivationKind.ManualExpedition,
                "publication-rejected",
                revision: 1L));
        Require(authority.Dispatcher.TryReserve(
                fact,
                out DomainFactLease lease,
                out string failure),
            "Rejected standalone skill fixture did not reserve: " + failure);
        Require(authority.Publications.TryBegin(
                fact.SourceCharacterId,
                out failure),
            "Rejected standalone skill fixture did not begin: " + failure);
        authority.PublicationPort.RejectNextPublish = true;
        DomainFactDelivery rejected = authority.Publications.PublishApplied(
            fact,
            lease);
        authority.Publications.End(fact.SourceCharacterId);
        Require(rejected.Phase == DomainFactDeliveryPhase.Rejected
            && authority.Publications.Capture().observations.Count == 0
            && authority.Dispatcher.ActiveLeaseCount == 0
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 0,
            "Rejected standalone skill publication retained a journal or ledger row.");
    }

    private static void VerifyStandaloneSkillPublicationAmbiguity(
        bool afterCommit)
    {
        string edge = afterCommit ? "after" : "before";
        SavedSkillPublicationAuthority saved;
        using (var authority = new SkillAuthority(
                   connect: true,
                   recorderFaults: true))
        {
            var fact = new CharacterSkillActivationFact(
                CreateSkillActivationData(
                    CharacterSkillActivationKind.ManualDungeon,
                    "publication-" + edge,
                    revision: 1L));
            Require(authority.Dispatcher.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure),
                "Ambiguous standalone skill did not reserve: " + failure);
            Require(authority.Publications.TryBegin(
                    fact.SourceCharacterId,
                    out failure),
                "Ambiguous standalone skill did not begin: " + failure);
            if (afterCommit)
                authority.PublicationPort.ThrowAfterNextPublish = true;
            else
                authority.Faults.ThrowBeforeNextSingleCommit = true;
            DomainFactDelivery ambiguous = authority.Publications.PublishApplied(
                fact,
                lease);
            authority.Publications.End(fact.SourceCharacterId);
            CharacterSkillActivationPublicationData pending =
                authority.Publications.Capture().observations.Single();
            Require(ambiguous.MayBeCommitted
                && !ambiguous.Completed
                && pending.pending
                && !pending.ownerFinalized
                && (afterCommit
                    ? authority.PublicationPort.AfterPublishFaultCount == 1
                        && OutcomeRowCount(
                            authority.Ledger.CaptureGameplayOutcomes()) == 1
                    : authority.Faults.BeforeCommitFaultCount == 1
                        && OutcomeRowCount(
                            authority.Ledger.CaptureGameplayOutcomes()) == 0),
                "Standalone skill " + edge
                    + "-commit ambiguity did not retain its frozen journal row.");
            saved = JsonConvert.DeserializeObject<SavedSkillPublicationAuthority>(
                JsonConvert.SerializeObject(new SavedSkillPublicationAuthority
                {
                    serializer = HostSerializer,
                    publications = authority.Publications.Capture(),
                    ledger = authority.Ledger.CaptureGameplayOutcomes()
                })) ?? throw new InvalidOperationException(
                    "Standalone skill " + edge
                        + "-commit host JSON returned null.");
        }

        using var restored = new SkillAuthority(
            connect: true,
            recorderFaults: false);
        restored.Restore(saved.ledger, saved.publications);
        Require(restored.Publications.RecoverPending(
                out string recoveryFailure),
            "Standalone skill " + edge
                + "-commit replay recovery failed: " + recoveryFailure);
        CharacterSkillActivationPublicationData recovered =
            restored.Publications.Capture().observations.Single();
        Require(!recovered.pending
            && !recovered.ownerFinalized
            && OutcomeRowCount(
                restored.Ledger.CaptureGameplayOutcomes()) == 1,
            "Standalone skill " + edge
                + "-commit recovery duplicated or lost canonical evidence.");
        restored.Publications.CompleteOwner(
            recovered.fact.sourceCharacterId,
            recovered.fact.ownerRevision);
        Require(restored.Publications.PendingOwners().Count == 0
            && restored.Publications.Capture().observations.Count == 1,
            "Recovered standalone skill owner was not finalized exactly once.");
    }

    private static void VerifyCharacterSkillActivationFactFreeze()
    {
        CharacterSkillActivationFactData source =
            CreateSkillActivationData(
                CharacterSkillActivationKind.ManualDungeon,
                "freeze",
                revision: 3L);
        var fact = new CharacterSkillActivationFact(source);
        string expectedSourceId = fact.SourceCharacterId;
        string expectedBefore = fact.Recipients[0].BeforeState;
        string expectedChild = fact.ChildPublicationKeys[0];

        source.sourceCharacterId = "character:skill-host:mutated";
        source.recipients[0].beforeState = "mutated-before";
        source.recipients.Add(new CharacterSkillActivationRecipientFactData
        {
            characterId = "character:skill-host:late",
            displayName = "late",
            beforeState = "late-before",
            afterState = "late-after"
        });
        source.childPublicationKeys[0] = SkillChildKey("mutated");
        Require(fact.SourceCharacterId == expectedSourceId
            && fact.Recipients.Count == 2
            && fact.Recipients[0].BeforeState == expectedBefore
            && fact.ChildPublicationKeys[0] == expectedChild,
            "Character skill fact retained aliases to its source DTO.");

        CharacterSkillActivationFactData snapshot = fact.Snapshot();
        Require(!ReferenceEquals(snapshot, source)
            && !ReferenceEquals(snapshot.recipients, source.recipients)
            && !ReferenceEquals(
                snapshot.recipients[0],
                source.recipients[0])
            && !ReferenceEquals(
                snapshot.childPublicationKeys,
                source.childPublicationKeys),
            "Character skill fact snapshot did not deep-clone mutable collections.");
        snapshot.sourceCharacterId = "character:skill-host:snapshot-mutated";
        snapshot.recipients[0].beforeState = "snapshot-mutated-before";
        snapshot.childPublicationKeys[0] = SkillChildKey("snapshot-mutated");
        Require(fact.SourceCharacterId == expectedSourceId
            && fact.Recipients[0].BeforeState == expectedBefore
            && fact.ChildPublicationKeys[0] == expectedChild,
            "Character skill fact retained aliases to its snapshot DTO.");
    }

    private static void VerifyCharacterSkillActivationReceiptParity()
    {
        CharacterSkillActivationKind[] kinds =
        {
            CharacterSkillActivationKind.ManualDungeon,
            CharacterSkillActivationKind.ManualExpedition,
            CharacterSkillActivationKind.AutomaticPassive,
            CharacterSkillActivationKind.AutomaticUltimate,
            CharacterSkillActivationKind.ManualCombatUltimate
        };
        for (int index = 0; index < kinds.Length; index++)
        {
            CharacterSkillActivationFactData data =
                CreateSkillActivationData(
                    kinds[index],
                    "parity-" + index.ToString(CultureInfo.InvariantCulture),
                    revision: index + 1L);
            var fact = new CharacterSkillActivationFact(data);
            CharacterSkillActivationOutcomeReceipt mapped =
                CharacterSkillActivationFactMapping.Capture(fact);
            CharacterSkillActivationOutcomeReceipt native =
                CreateNativeCharacterSkillActivationReceipt(data);
            Require(mapped.ResultKey == native.ResultKey
                && mapped.OperationId == native.OperationId
                && fact.RecipientStateSha256
                    == mapped.RecipientStateSha256Value
                && JsonConvert.SerializeObject(mapped)
                    == JsonConvert.SerializeObject(native),
                "Character skill fact mapping drifted from the native receipt for "
                    + kinds[index] + ".");
        }
    }

    private static void VerifyCharacterSkillActivationBindingAndValidation()
    {
        CharacterSkillActivationFactData invalidIdentity =
            CreateSkillActivationData(
                CharacterSkillActivationKind.ManualDungeon,
                "invalid-identity",
                revision: 1L);
        invalidIdentity.sourceCharacterId = "character skill invalid";
        RequireThrows<ArgumentException>(
            () => new CharacterSkillActivationFact(invalidIdentity),
            "Character skill fact accepted an invalid source identity.");

        CharacterSkillActivationFactData invalidCount =
            CreateSkillActivationData(
                CharacterSkillActivationKind.ManualExpedition,
                "invalid-count",
                revision: 2L);
        invalidCount.useCountAfter = invalidCount.useCountBefore;
        RequireThrows<ArgumentException>(
            () => new CharacterSkillActivationFact(invalidCount),
            "Character skill fact accepted a non-incrementing use count.");

        CharacterSkillActivationFactData invalidEvent =
            CreateSkillActivationData(
                CharacterSkillActivationKind.AutomaticUltimate,
                "invalid-event",
                revision: 3L);
        invalidEvent.triggerEventId = string.Empty;
        RequireThrows<ArgumentException>(
            () => new CharacterSkillActivationFact(invalidEvent),
            "Character skill fact accepted an event-bound activation without an event.");

        using var authority = new SkillAuthority(
            connect: false,
            recorderFaults: false);
        var valid = new CharacterSkillActivationFact(
            CreateSkillActivationData(
                CharacterSkillActivationKind.ManualDungeon,
                "binding",
                revision: 4L));
        Require(!authority.Dispatcher.TryReserve(
                valid,
                out _,
                out string missingFailure)
            && missingFailure.StartsWith(
                "domain-fact-observer-missing:",
                StringComparison.Ordinal),
            "Character skill fact unexpectedly published without its explicit binding.");

        GameplayDomainFactObserverBinding<
            CharacterSkillActivationFact,
            CharacterSkillActivationOutcomeReceipt> binding =
            CharacterSkillActivationFactMapping.CreateBinding(
                authority.Registry,
                authority.Ledger,
                authority.Recorder,
                authority.CommitGroup);
        using IDisposable connection = binding.Connect(authority.Dispatcher);
        RequireThrows<InvalidOperationException>(
            () => binding.Connect(authority.Dispatcher),
            "Character skill binding accepted a duplicate durable observer.");

        CharacterSkillActivationFactData opaqueInvalidChild =
            CreateSkillActivationData(
                CharacterSkillActivationKind.ManualDungeon,
                "invalid-child",
                revision: 5L,
                childPublicationKeys:
                    new[] { "opaque-but-not-a-gameplay-result-key" });
        var invalidChild = new CharacterSkillActivationFact(
            opaqueInvalidChild);
        RequireThrows<ArgumentException>(
            () => authority.Dispatcher.TryReserve(
                invalidChild,
                out _,
                out _),
            "Character skill observer accepted an opaque invalid child publication key.");
        Require(authority.Dispatcher.ActiveLeaseCount == 0
            && OutcomeRowCount(
                authority.Ledger.CaptureGameplayOutcomes()) == 0,
            "Rejected character skill evidence left a lease or ledger row.");

        connection.Dispose();
        Require(!authority.Dispatcher.TryReserve(
                valid,
                out _,
                out string disconnectedFailure)
            && disconnectedFailure.StartsWith(
                "domain-fact-observer-missing:",
                StringComparison.Ordinal),
            "Disposed character skill binding remained connected.");
    }

    private static void VerifyCharacterSkillActivationAtomicReplay()
    {
        using (var before = new SkillAuthority(
                   connect: true,
                   recorderFaults: true))
        {
            CharacterSkillActivationFact[] facts =
                CreateSkillActivationBatch("before");
            before.Faults.ThrowBeforeNextBatchCommit = true;
            Require(PublishSkillActivationBatch(
                    before,
                    facts,
                    out string[] keys,
                    out DomainFactDelivery[] attempt,
                    out _)
                && attempt.All(value =>
                    value.Phase == DomainFactDeliveryPhase.InDoubt)
                && before.Faults.BeforeCommitFaultCount == 1
                && OutcomeRowCount(
                    before.Ledger.CaptureGameplayOutcomes()) == 0,
                "Character skill pre-commit fault was not an atomic ambiguity.");
            Require(ReplaySkillActivationBatch(
                    before,
                    keys,
                    facts,
                    out DomainFactDelivery[] replayed,
                    out _)
                && replayed.All(value => value.Completed)
                && OutcomeRowCount(
                    before.Ledger.CaptureGameplayOutcomes()) == 2,
                "Character skill pre-commit ambiguity did not replay exactly once.");
        }

        using (var after = new SkillAuthority(
                   connect: true,
                   recorderFaults: true))
        {
            CharacterSkillActivationFact[] facts =
                CreateSkillActivationBatch("after");
            after.Faults.ThrowAfterNextBatchCommit = true;
            Require(PublishSkillActivationBatch(
                    after,
                    facts,
                    out string[] keys,
                    out DomainFactDelivery[] attempt,
                    out _)
                && attempt.All(value => value.MayBeCommitted)
                && after.Faults.AfterCommitFaultCount == 1
                && OutcomeRowCount(
                    after.Ledger.CaptureGameplayOutcomes()) == 2,
                "Character skill post-commit fault lost atomic ledger state.");
            Require(ReplaySkillActivationBatch(
                    after,
                    keys,
                    facts,
                    out DomainFactDelivery[] replayed,
                    out _)
                && replayed.All(value => value.Completed),
                "Character skill post-commit ambiguity did not reconcile exact replay.");

            IReadOnlyDictionary<string, string> canonical = CanonicalHashes(
                after.Ledger.CaptureGameplayOutcomes());
            CharacterSkillActivationFactData alteredData = facts[1].Snapshot();
            alteredData.skillDisplayName += " 변조";
            var altered = new[]
            {
                facts[0],
                new CharacterSkillActivationFact(alteredData)
            };
            Require(!ReplaySkillActivationBatch(
                    after,
                    keys,
                    altered,
                    out DomainFactDelivery[] rejected,
                    out string failure)
                && rejected.All(value =>
                    value.Phase == DomainFactDeliveryPhase.Rejected
                    && !value.MayBeCommitted)
                && !string.IsNullOrWhiteSpace(failure),
                "Character skill replay accepted an altered canonical payload.");
            RequireCanonicalEqual(
                canonical,
                CanonicalHashes(after.Ledger.CaptureGameplayOutcomes()),
                "Altered character skill replay mutated the canonical ledger.");
        }
    }

    private static CharacterSkillActivationFact[] CreateSkillActivationBatch(
        string suffix)
    {
        string secondSource = "character:skill-host:" + suffix + "-second";
        const long secondRevision = 12L;
        string secondKey = CharacterSkillActivationPersistenceContract.ResultKey(
            secondSource,
            secondRevision);
        CharacterSkillActivationFactData first = CreateSkillActivationData(
            CharacterSkillActivationKind.ManualDungeon,
            suffix + "-first",
            revision: 11L,
            childPublicationKeys: new[] { secondKey });
        CharacterSkillActivationFactData second = CreateSkillActivationData(
            CharacterSkillActivationKind.ManualExpedition,
            suffix + "-second",
            revision: secondRevision,
            childPublicationKeys: Array.Empty<string>());
        Require(second.sourceCharacterId == secondSource,
            "Character skill batch fixture identity drifted.");
        return new[]
        {
            new CharacterSkillActivationFact(first),
            new CharacterSkillActivationFact(second)
        };
    }

    private static bool PublishSkillActivationBatch(
        SkillAuthority authority,
        IReadOnlyList<CharacterSkillActivationFact> facts,
        out string[] publicationKeys,
        out DomainFactDelivery[] results,
        out string failure)
    {
        var leases = new DomainFactLease[facts.Count];
        publicationKeys = new string[facts.Count];
        for (int index = 0; index < facts.Count; index++)
        {
            Require(authority.Dispatcher.TryReserve(
                    facts[index],
                    out leases[index],
                    out string reserveFailure),
                "Character skill batch reservation failed: " + reserveFailure);
            publicationKeys[index] = leases[index].PublicationKey;
            Require(!string.IsNullOrWhiteSpace(publicationKeys[index]),
                "Character skill reservation returned no publication key.");
        }
        DomainFactEmission[] emissions = facts.Select((fact, index) =>
                DomainFactEmission.Create(leases[index], fact))
            .ToArray();
        results = new DomainFactDelivery[facts.Count];
        return authority.Dispatcher.PublishBatch(
            emissions,
            results,
            out failure);
    }

    private static bool ReplaySkillActivationBatch(
        SkillAuthority authority,
        IReadOnlyList<string> publicationKeys,
        IReadOnlyList<CharacterSkillActivationFact> facts,
        out DomainFactDelivery[] results,
        out string failure)
    {
        DomainFactReplay[] replays = facts.Select((fact, index) =>
                DomainFactReplay.Create(publicationKeys[index], fact))
            .ToArray();
        results = new DomainFactDelivery[facts.Count];
        return authority.Dispatcher.ReplayBatch(
            replays,
            results,
            out failure);
    }

    private static CharacterSkillActivationFactData CreateSkillActivationData(
        CharacterSkillActivationKind kind,
        string suffix,
        long revision,
        IEnumerable<string> childPublicationKeys = null)
    {
        bool eventBound = kind is
            CharacterSkillActivationKind.AutomaticPassive
            or CharacterSkillActivationKind.AutomaticUltimate
            or CharacterSkillActivationKind.ManualCombatUltimate;
        bool ultimate = kind is
            CharacterSkillActivationKind.AutomaticUltimate
            or CharacterSkillActivationKind.ManualCombatUltimate;
        string sourceId = "character:skill-host:" + suffix;
        return new CharacterSkillActivationFactData
        {
            sourceCharacterId = sourceId,
            sourceDisplayName = "  호스트 기술 사용자  ",
            ownerRevision = revision,
            activationKind = kind,
            skillId = "skill:host:" + suffix,
            skillDisplayName = "  호스트 기술  ",
            trigger = CharacterSkillTrigger.ManualCombat,
            target = CharacterSkillTarget.Ally,
            targetingMode = CharacterSkillTargetingMode.PlayerSelected,
            effectArea = CharacterSkillEffectArea.Single,
            areaSize = 1,
            durationHours = 24,
            cooldownDays = 2,
            absoluteDay = 17,
            absoluteHour = 408L,
            expiresAbsoluteHour = 432L,
            useCountBefore = 2,
            useCountAfter = 3,
            readyAbsoluteHourBefore = eventBound ? 0L : 390L,
            readyAbsoluteHourAfter = eventBound ? 0L : 456L,
            mechanicsSha256 = new string('A', 64),
            recipients = new List<CharacterSkillActivationRecipientFactData>
            {
                new()
                {
                    characterId = "character:skill-host:" + suffix + "-ally",
                    displayName = "  호스트 대상  ",
                    beforeState = "mood=40|health=80",
                    afterState = "mood=45|health=80"
                },
                new()
                {
                    characterId = sourceId,
                    displayName = "  호스트 기술 사용자  ",
                    beforeState = "mana=30|uses=2",
                    afterState = "mana=20|uses=3"
                }
            },
            childPublicationKeys = (childPublicationKeys
                ?? new[] { SkillChildKey(suffix) }).ToList(),
            triggerEventId = eventBound
                ? "event:skill-host:" + suffix
                : string.Empty,
            triggerEventKeySha256 = eventBound
                ? new string('B', 64)
                : string.Empty,
            ultimateDomain = ultimate
                ? CharacterUltimateDomain.Offense
                : CharacterUltimateDomain.None,
            triggerSerial = ultimate ? 7 : -1
        };
    }

    private static CharacterSkillActivationOutcomeReceipt
        CreateNativeCharacterSkillActivationReceipt(
            CharacterSkillActivationFactData data)
    {
        var skill = new CharacterSkillInstance
        {
            id = data.skillId,
            displayName = data.skillDisplayName,
            trigger = data.trigger,
            target = data.target,
            targetingMode = data.targetingMode,
            effectArea = data.effectArea,
            areaSize = data.areaSize,
            manualDurationHours = data.durationHours,
            manualCooldownDays = data.cooldownDays
        };
        CharacterSkillActivationRecipientReceipt[] recipients = data.recipients
            .Select(value => new CharacterSkillActivationRecipientReceipt(
                value.characterId,
                value.displayName,
                value.beforeState,
                value.afterState))
            .ToArray();
        GameplayResultKey[] children = data.childPublicationKeys
            .Select(ReadSkillResultKey)
            .ToArray();
        return new CharacterSkillActivationOutcomeReceipt(
            data.sourceCharacterId,
            data.sourceDisplayName,
            data.ownerRevision,
            data.activationKind,
            skill,
            data.absoluteDay,
            data.absoluteHour,
            data.expiresAbsoluteHour,
            data.useCountBefore,
            data.useCountAfter,
            data.readyAbsoluteHourBefore,
            data.readyAbsoluteHourAfter,
            data.mechanicsSha256,
            recipients,
            children,
            data.triggerEventId,
            data.triggerEventKeySha256,
            data.ultimateDomain,
            data.triggerSerial);
    }

    private static GameplayResultKey ReadSkillResultKey(string value)
    {
        string[] parts = value.Split('|');
        return new GameplayResultKey(
            parts[0],
            new GameplayOperationId(parts[1]),
            long.Parse(
                parts[2],
                NumberStyles.None,
                CultureInfo.InvariantCulture),
            int.Parse(
                parts[3],
                NumberStyles.None,
                CultureInfo.InvariantCulture));
    }

    private static string SkillChildKey(string suffix) =>
        new GameplayResultKey(
            "skill-host-child",
            new GameplayOperationId("skill-child:" + suffix),
            1L,
            0).ToString();

    private static IDisposable ConnectCraftObserver(Authority authority) =>
        new CombatEquipmentCraftCompletionFactObserver(
            authority.Migrated,
            new GameplayDomainFactCommitGroup(
                authority.Recorder,
                authority.Ledger))
        .Connect(authority.Dispatcher);

    private static DomainFactDelivery ReplayCraft(
        Authority authority,
        string publicationKey,
        CombatEquipmentCraftCompletedFact fact)
    {
        var replies = new DomainFactDelivery[1];
        bool accepted = authority.Dispatcher.ReplayBatch(
            new[] { DomainFactReplay.Create(publicationKey, fact) },
            replies,
            out string failure);
        Require(accepted && replies[0].MayBeCommitted,
            "Combat craft replay was rejected: " + failure + ":"
                + replies[0].Detail);
        return replies[0];
    }

    private static DomainFactDelivery ReplayQuality(
        QualityAuthority authority,
        string publicationKey,
        CombatEquipmentQualityResolvedFact fact)
    {
        var replies = new DomainFactDelivery[1];
        bool accepted = authority.Dispatcher.ReplayBatch(
            new[] { DomainFactReplay.Create(publicationKey, fact) },
            replies,
            out string failure);
        Require(accepted && replies[0].MayBeCommitted,
            "Equipment quality replay was rejected: " + failure + ":"
                + replies[0].Detail);
        return replies[0];
    }

    private static DomainFactDelivery ReplayIdle(
        IdleAuthority authority,
        string publicationKey,
        IdleSocialInteractionResolvedFact fact)
    {
        var replies = new DomainFactDelivery[1];
        bool accepted = authority.Dispatcher.ReplayBatch(
            new[] { DomainFactReplay.Create(publicationKey, fact) },
            replies,
            out string failure);
        Require(accepted && replies[0].MayBeCommitted,
            "Idle social replay was rejected: " + failure + ":"
                + replies[0].Detail);
        return replies[0];
    }

    private static IdleSocialInteractionFactData CreateIdleSocialData() =>
        new()
        {
            operationId = "idle-social:host-probe",
            actorId = "character:idle-host:actor",
            actorName = "호스트 대화자",
            targetId = "character:idle-host:target",
            targetName = "호스트 대화 상대",
            behaviorId = "social:encourage",
            reasonId = "low-mood",
            absoluteDay = 52,
            actorRelationshipBefore = 0.4f,
            actorRelationshipAfter = 0.4f,
            targetRelationshipBefore = 0.35f,
            targetRelationshipAfter = 0.35f,
            moodBefore = 58f,
            moodAfter = 61f
        };

    private static CombatEquipmentCraftCompletionFactData
        CreateCraftCompletionData() => new()
    {
        orderId = "combat-order:host-probe",
        qualityAttemptIndex = 2,
        facilityPersistentId = "facility:combat-host:forge",
        definitionId = "combat-equipment:host-blade",
        materialId = "item:host-steel",
        completedWork = 125.5f,
        resolvedQuality = CombatEquipmentQuality.Good,
        minimumQuality = CraftsmanshipQualityTier.Normal,
        rejectedDisposition = RejectedOutputDisposition.KeepInStorage,
        resolvedMakerCharacterId = "character:combat-host:smith",
        resolvedMakerDisplayName = "호스트 대장장이",
        resolvedAbsoluteDay = 33,
        materialTransferCommitId = "material-transfer:combat-host",
        materialTransferMassGrams = 4200L,
        materialTransferInputs = new List<CombatEquipmentCraftMaterialTransferInput>
        {
            new()
            {
                itemId = "item:host-steel",
                sourceStackId = "stack:host-steel:source",
                quantity = 3
            }
        },
        contributions = new List<CraftContributionSaveData>
        {
            new()
            {
                characterId = "character:combat-host:smith",
                contributedWork = 125.5f,
                relevantSkill = 14f
            }
        },
        commandReceipts = new List<CombatCraftCommandReceiptSaveData>
        {
            new()
            {
                commandKind = "quality-policy",
                revision = 1L,
                beforeValue = "Normal",
                afterValue = "Good",
                absoluteDay = 32
            }
        },
        qualityOutcomeResultKey = "quality-publication:combat-host:2",
        outputCommitId = "output-commit:combat-host:2",
        outputQuantity = 3,
        outputMassGrams = 4100L,
        ownerRevision = 37L,
        outcomeFingerprint =
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
        outputs = new List<ProductionDomainPublishedStackSaveData>
        {
            new()
            {
                outputLineId = "line:host-blade:primary",
                itemId = "item:host-blade",
                itemInstanceId = "item-instance:host-blade:1",
                stackId = "stack:host-blade:1",
                quantity = 1,
                massGrams = 1500L,
                displayText = "호스트 검"
            },
            new()
            {
                outputLineId = "line:host-blade:secondary",
                itemId = "item:host-blade",
                itemInstanceId = "item-instance:host-blade:2",
                stackId = "stack:host-blade:2",
                quantity = 2,
                massGrams = 2600L,
                displayText = "호스트 검 묶음"
            }
        }
    };

    private static CommitResult Commit(
        Authority authority,
        CombatActionFact fact,
        bool requireCommitted = true)
    {
        CombatActionFact proposal = ProposalFor(fact);
        Require(authority.Dispatcher.TryReserve(
                proposal,
                out DomainFactLease lease,
                out string failure),
            "Combat proposal reservation failed: " + failure);
        string key = lease.PublicationKey;
        Require(!string.IsNullOrWhiteSpace(key),
            "Combat reservation returned no publication key.");
        DomainFactDelivery delivery = authority.Dispatcher.Publish(lease, fact);
        if (requireCommitted)
        {
            Require(delivery.MayBeCommitted,
                "Combat publication was rejected: " + delivery.Detail);
        }
        return new CommitResult(key, delivery);
    }

    private static DomainFactDelivery Replay(
        Authority authority,
        string publicationKey,
        CombatActionFact fact)
    {
        var replies = new DomainFactDelivery[1];
        bool accepted = authority.Dispatcher.ReplayBatch(
            new[] { DomainFactReplay.Create(publicationKey, fact) },
            replies,
            out string failure);
        Require(accepted && replies[0].MayBeCommitted,
            "Combat replay was rejected: " + failure + ":" + replies[0].Detail);
        return replies[0];
    }

    private static CombatActionFact ProposalFor(CombatActionFact fact)
    {
        CombatActionFactData data = fact.Snapshot();
        return CombatActionFact.Proposal(
            data.operationId,
            data.attackRevision,
            data.absoluteDay,
            data.target,
            data.hasConflict ? data.conflict : null);
    }

    private static CombatActionFact CreateAction(
        string operationId,
        long revision,
        int day,
        float healthBefore,
        float healthAfter,
        bool fatal,
        bool socialParent)
    {
        var attacker = new CombatFactParticipantData
        {
            kind = CombatFactParticipantKind.Character,
            id = "character:combat-host:attacker",
            name = "호스트 공격자"
        };
        var target = new CombatFactParticipantData
        {
            kind = CombatFactParticipantKind.Character,
            id = "character:combat-host:target",
            name = "호스트 대상"
        };
        CombatConflictFactData conflict = socialParent
            ? new CombatConflictFactData
            {
                operationId = operationId,
                instigator = attacker.Clone(),
                target = target.Clone(),
                conflictId = "conflict:combat-host:fatal",
                severity = 0.75f,
                committedMoodDelta = -4f,
                origin = CharacterCommandOrigin.Autonomous,
                absoluteDay = day,
                alreadyPublished = false
            }
            : new CombatConflictFactData();
        float damage = healthBefore - healthAfter;
        return new CombatActionFact(new CombatActionFactData
        {
            operationId = operationId,
            attackRevision = revision,
            absoluteDay = day,
            attacker = attacker,
            target = target,
            targetX = 9,
            targetY = -2,
            evidence = new CombatActionEvidenceData
            {
                requestKnown = true,
                weaponDefinitionId = "weapon:combat-host:sword",
                weaponInstanceId = "weapon-instance:combat-host:sword",
                ammunitionItemId = "ammo:combat-host:test",
                coverSourceId = string.Empty,
                damageType = CombatDamageType.Slash,
                fireMode = CombatFireMode.Aimed,
                ammunitionBefore = 3,
                ammunitionConsumed = 1,
                ammunitionAfter = 2,
                executed = true,
                hit = true,
                coverBlocked = false,
                shieldBlocked = false,
                evaded = false,
                nonlethal = false,
                specialEffects = CombatSpecialEffectFlags.None,
                statusPotency = 0f,
                coverDamage = 0f,
                statusTurns = 0,
                bodyPart = CombatBodyPart.Torso,
                appliedDamage = damage,
                suppression = 0f,
                armorHits = new List<CombatArmorHitFactData>
                {
                    new()
                    {
                        instanceId = "armor-instance:combat-host:vest",
                        damage = Math.Min(1f, damage)
                    }
                }
            },
            healthBefore = healthBefore,
            maxHealthBefore = 100f,
            healthAfter = healthAfter,
            maxHealthAfter = 100f,
            downedBefore = false,
            downedAfter = fatal,
            damageSource = "host-managed-combat-probe",
            origin = CharacterCommandOrigin.Autonomous,
            hasConflict = socialParent,
            conflict = conflict
        });
    }

    private static OffenseArrivalSettlementFactData
        CreateOffenseArrivalSettlementFactData(string arrivalId) => new()
    {
        arrivalId = arrivalId,
        expeditionId = "expedition:host-arrival",
        targetId = "offense-target:host-arrival",
        kind = OffenseReturnArrivalKind.SpecialWildlife,
        absoluteDay = 103,
        requestedAmount = 1,
        materializedAmount = 1,
        securedAmount = 0,
        escapedAmount = 1,
        materializedIds = new List<string> { "wild:1" },
        escapedIds = new List<string> { "wild:1" }
    };

    private static OffenseNodeResolutionFact CreateOffenseNodeProposal(
        string expeditionId,
        string nodeId,
        long sequence,
        OffenseRouteNodeKind kind = OffenseRouteNodeKind.Event,
        bool useSupply = true) =>
        new(CreateOffenseNodeData(
            expeditionId,
            nodeId,
            sequence,
            kind,
            useSupply));

    private static OffenseBattleExperienceFact
        CreateOffenseBattleExperience(
            bool victory,
            float damageTaken,
            string regionName = "호스트 전투 권역") => new(
        "battle:host-battle",
        "expedition:host-battle",
        "character:host-battle-member",
        "호스트 생존자",
        "offense-target:host-battle",
        "호스트 전투 목표",
        regionName,
        "faction:host-battle",
        "node:host-battle",
        "호스트 전투 노드",
        victory,
        damageTaken);

    private static GameplayNarrativeEventContext
        CreateOffenseBattleContext(OffenseBattleExperienceFact fact)
    {
        MethodInfo create = typeof(OffenseBattleExperienceObserver)
            .GetMethod(
                "CreateContext",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(OffenseBattleExperienceFact) },
                modifiers: null)
            ?? throw new MissingMethodException(
                typeof(OffenseBattleExperienceObserver).FullName,
                "CreateContext");
        return (GameplayNarrativeEventContext)create.Invoke(
            null,
            new object[] { fact });
    }

    private static IReadOnlyList<OffenseExpeditionActivityFact>
        CreateOffenseActivityFacts() => new[]
    {
        new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.RegionBossSealAwarded,
            "expedition:host-activity-boss"),
        new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.PartyStranded,
            "expedition:host-activity-stranded",
            message: "거친 지형 때문에 원정대가 조난되었습니다."),
        new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.ObjectiveDestroyed,
            "expedition:host-activity-objective",
            targetTitle: "호스트 요새",
            count: 3),
        new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.RouteBattleCompleted,
            "expedition:host-activity-route",
            targetTitle: "호스트 목표",
            nodeTitle: "호스트 관문"),
        new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.DepartureRestorationFailed,
            "expedition:host-activity-departure-restore"),
        new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.PackingResumptionFailed,
            "expedition:host-activity-packing",
            message: "저장된 보급품을 다시 소비하지 못했습니다."),
        new OffenseExpeditionActivityFact(
            OffenseExpeditionActivity.DepartureResumptionFailed,
            "expedition:host-activity-departure-resume",
            message: "저장된 출정 절차를 재개하지 못했습니다.")
    };

    private static OffenseNodeResolutionFactData CreateOffenseNodeData(
        string expeditionId,
        string nodeId,
        long sequence,
        OffenseRouteNodeKind kind = OffenseRouteNodeKind.Event,
        bool useSupply = true) => new()
    {
        expeditionId = expeditionId,
        targetId = "offense-target:host-node",
        targetTitle = "호스트 노드 목표",
        regionId = "offense-region:host-node",
        regionDisplayName = "호스트 노드 지역",
        factionId = "faction:host-node",
        nodeId = nodeId,
        nodeTitle = "호스트 사건 노드",
        nodeDescription = "호스트에서 동결한 원정 노드입니다.",
        message = string.Empty,
        nodeKind = kind,
        absoluteDay = 109,
        experienceReward = 5,
        sequence = sequence,
        resolved = false,
        useSupply = useSupply,
        usedSupply = false,
        gainedLoot = false,
        participants = new List<OffenseNodeParticipantData>
        {
            new()
            {
                characterId = "character:host-node-member",
                displayName = "호스트 원정대원",
                experienceEligible = true,
                experienceDisplayName = "호스트 원정대원"
            }
        }
    };

    private static OffenseNodeExperienceData
        CreateOffenseNodeExperience() => new()
    {
        characterId = "character:host-node-member",
        displayName = "호스트 원정대원",
        requestedAmount = 5,
        levelBefore = 2,
        levelAfter = 2,
        experienceBefore = 4,
        experienceAfter = 9
    };

    private static void RequireOffenseNodeReplayRejected(
        OffenseNodeAuthority authority,
        string publicationKey,
        OffenseNodeResolutionFact fact,
        string message)
    {
        var replies = new DomainFactDelivery[1];
        bool accepted = authority.Dispatcher.ReplayBatch(
            new[] { DomainFactReplay.Create(publicationKey, fact) },
            replies,
            out string failure);
        Require(!accepted
            && replies[0].Phase == DomainFactDeliveryPhase.Rejected
            && !replies[0].MayBeCommitted
            && !string.IsNullOrWhiteSpace(failure),
            message + " " + failure + ":" + replies[0].Detail);
    }

    private static DungeonGameRestoreReport ValidateOffenseNodeJoin(
        OffenseNodePublicationSaveData publications,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonMigratedProducerOutcomeSaveData migrated)
    {
        MethodInfo validate = typeof(OffenseNodeFactPreflight).GetMethod(
            "Validate",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(OffenseNodePublicationSaveData),
                typeof(GameplayOutcomeLedgerSaveData),
                typeof(DungeonMigratedProducerOutcomeSaveData),
                typeof(DungeonGameRestoreReport)
            },
            modifiers: null)
            ?? throw new MissingMethodException(
                typeof(OffenseNodeFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        validate.Invoke(
            null,
            new object[] { publications, ledger, migrated, report });
        return report;
    }

    private static OffenseReturnArrivalState CreateOffenseArrivalState(
        string arrivalId) => new()
    {
        arrivalId = arrivalId,
        expeditionId = "expedition:host-arrival",
        targetId = "offense-target:host-arrival",
        kind = OffenseReturnArrivalKind.SpecialWildlife,
        requestedAmount = 1,
        returnSealed = true,
        returningMembers = 0,
        stage = OffenseReturnArrivalStage.AwaitingContainment,
        escapeRisk = 37.5f,
        materializedIds = new List<string> { "wild:1" },
        escapedIds = new List<string> { "wild:1" },
        lastStatus = "host-awaiting-containment",
        settledMaterializedAmount = 0,
        settledSecuredAmount = 0,
        settledEscapedAmount = 0,
        settlement = new OffenseArrivalSettlementObservationData()
    };

    private static bool IsOriginalAwaitingArrival(
        OffenseReturnArrivalState value) =>
        value.stage == OffenseReturnArrivalStage.AwaitingContainment
        && value.settledMaterializedAmount == 0
        && value.settledSecuredAmount == 0
        && value.settledEscapedAmount == 0
        && Math.Abs(value.escapeRisk - 37.5f) < 0.0001f
        && value.lastStatus == "host-awaiting-containment"
        && value.settlement != null
        && !value.settlement.hasFact
        && !value.settlement.pending
        && !value.settlement.projectionPending
        && value.settlement.publicationKey == string.Empty
        && value.materializedIds.SequenceEqual(
            new[] { "wild:1" },
            StringComparer.Ordinal)
        && value.escapedIds.SequenceEqual(
            new[] { "wild:1" },
            StringComparer.Ordinal);

    private static void RequireOffenseArrivalReplayRejected(
        OffenseArrivalSettlementAuthority authority,
        string publicationKey,
        OffenseArrivalSettledFact fact,
        string message)
    {
        var replies = new DomainFactDelivery[1];
        bool accepted = authority.Dispatcher.ReplayBatch(
            new[] { DomainFactReplay.Create(publicationKey, fact) },
            replies,
            out string failure);
        Require(!accepted
            && replies[0].Phase == DomainFactDeliveryPhase.Rejected
            && !replies[0].MayBeCommitted
            && !string.IsNullOrWhiteSpace(failure),
            message + " " + failure + ":" + replies[0].Detail);
    }

    private static DungeonGameRestoreReport ValidateOffenseArrivalJoin(
        OffenseReturnArrivalState arrival,
        GameplayOutcomeLedgerSaveData ledger,
        DungeonMigratedProducerOutcomeSaveData migrated)
    {
        MethodInfo validate = typeof(OffenseArrivalSettlementFactPreflight)
            .GetMethod(
                "Validate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(DungeonOffenseReturnArrivalSaveData),
                    typeof(GameplayOutcomeLedgerSaveData),
                    typeof(DungeonMigratedProducerOutcomeSaveData),
                    typeof(DungeonGameRestoreReport)
                },
                modifiers: null)
            ?? throw new MissingMethodException(
                typeof(OffenseArrivalSettlementFactPreflight).FullName,
                "Validate");
        var arrivals = new DungeonOffenseReturnArrivalSaveData
        {
            nextArrivalSequence = 100,
            arrivals = new List<OffenseReturnArrivalState> { arrival }
        };
        var report = new DungeonGameRestoreReport();
        validate.Invoke(
            null,
            new object[] { arrivals, ledger, migrated, report });
        return report;
    }

    private static OffenseExpeditionResolvedFact
        CreateOffenseExpeditionResultFact(string expeditionId) =>
        new(CreateOffenseExpeditionResultData(expeditionId));

    private static OffenseExpeditionResultFactData
        CreateOffenseExpeditionResultData(string expeditionId) => new()
    {
        absoluteDay = 102,
        memberCount = 1,
        campaignAdvanced = true,
        worldSiteResolved = true,
        regionId = string.Empty,
        regionDisplayName = string.Empty,
        factionId = string.Empty,
        participants = new List<OffenseExpeditionParticipantFactData>
        {
            new()
            {
                characterId = "character:host-result-member",
                displayName = "호스트 원정대원",
                identityParticipant = true
            }
        },
        result = new DungeonOffenseExpeditionResultSaveData
        {
            expeditionId = expeditionId,
            targetId = "offense-target:host-result",
            targetTitle = "호스트 원정 목표",
            success = true,
            totalPower = 12f,
            requiredPower = 10f,
            danger = 2f,
            elapsedSeconds = 5f
        }
    };

    private static void RequireOffenseExpeditionReplayRejected(
        OffenseExpeditionResultAuthority authority,
        string publicationKey,
        OffenseExpeditionResolvedFact fact,
        string message)
    {
        var replies = new DomainFactDelivery[1];
        bool accepted = authority.Dispatcher.ReplayBatch(
            new[] { DomainFactReplay.Create(publicationKey, fact) },
            replies,
            out string failure);
        Require(!accepted
            && replies[0].Phase == DomainFactDeliveryPhase.Rejected
            && !replies[0].MayBeCommitted
            && !string.IsNullOrWhiteSpace(failure),
            message + " " + failure + ":" + replies[0].Detail);
    }

    private static DungeonGameRestoreReport
        ValidateOffenseExpeditionResultJoin(
            OffenseExpeditionResultPublicationSaveData publications,
            GameplayOutcomeLedgerSaveData ledger,
            DungeonMigratedProducerOutcomeSaveData migrated)
    {
        MethodInfo validate = typeof(OffenseExpeditionResultFactPreflight)
            .GetMethod(
                "Validate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(DungeonOffenseAggregateSaveData),
                    typeof(GameplayOutcomeLedgerSaveData),
                    typeof(DungeonMigratedProducerOutcomeSaveData),
                    typeof(DungeonGameRestoreReport)
                },
                modifiers: null)
            ?? throw new MissingMethodException(
                typeof(OffenseExpeditionResultFactPreflight).FullName,
                "Validate");
        var aggregate = new DungeonOffenseAggregateSaveData
        {
            resultPublications = publications
        };
        aggregate.expedition.resultHistory = publications.observations
            .Select(value => value.fact.Clone().result)
            .ToList();
        var report = new DungeonGameRestoreReport();
        validate.Invoke(
            null,
            new object[] { aggregate, ledger, migrated, report });
        return report;
    }

    private static OffenseTargetDefinition CreateOffenseTruthTarget() =>
        new()
        {
            id = OffenseTruthTargetId,
            title = "호스트 진실 목표",
            description = "호스트 관리형 진실 목표",
            regionId = string.Empty,
            regionDisplayName = string.Empty,
            factionId = string.Empty,
            prerequisiteTargetId = string.Empty,
            revealsTruth = true,
            truthText = "동결된 호스트 진실 본문",
            distance = 1f,
            danger = 1f,
            durationSeconds = 1f,
            requiredMembers = 1,
            requiredPower = 0f,
            seasonalOccurrenceInstanceId = string.Empty,
            authoredEncounterId = string.Empty,
            encounterPreviewText = string.Empty,
            encounterRewardPreviewText = string.Empty,
            rewards = Array.Empty<OffenseRewardPreview>()
        };

    private static OffenseTruthFactData CreateOffenseTruthData() => new()
    {
        targetId = OffenseTruthTargetId,
        ownerRevision = 1L,
        targetDisplayName = "호스트 진실 목표",
        truthTitle = "호스트 진실",
        truthText = "동결된 호스트 진실 본문",
        absoluteDay = 101
    };

    private static OffenseCampaignRuntime CreateOffenseCampaign()
    {
        var owner = new OffenseCampaignRuntime();
        owner.ConfigureTargets(new[] { CreateOffenseTruthTarget() });
        owner.Reset(0);
        return owner;
    }

    private static DungeonGameRestoreReport ValidateOffenseTruthJoin(
        DungeonOffenseCampaignSaveData campaign,
        GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo validate = typeof(OffenseTruthFactPreflight)
            .GetMethod(
                "Validate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(DungeonOffenseCampaignSaveData),
                    typeof(GameplayOutcomeLedgerSaveData),
                    typeof(DungeonGameRestoreReport)
                },
                modifiers: null)
            ?? throw new MissingMethodException(
                typeof(OffenseTruthFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        validate.Invoke(null, new object[] { campaign, ledger, report });
        return report;
    }

    private static StaffDiscontentFactData CreateStaffTransitionFact() =>
        new()
        {
            kind = StaffDiscontentFactKind.DailyTransition,
            revision = 1L,
            absoluteDay = 91,
            transition = StaffDiscontentOutcome.Warning,
            responseType = default,
            responderId = string.Empty,
            responderDisplayName = string.Empty,
            before = CreateStaffRecord(
                StaffDiscontentStage.Stable,
                StaffDiscontentOutcome.None,
                55f,
                0,
                1,
                90),
            after = CreateStaffRecord(
                StaffDiscontentStage.LowSatisfaction,
                StaffDiscontentOutcome.Warning,
                40f,
                1,
                2,
                91)
        };

    private static StaffDiscontentFactData CreateStaffResponseFact() =>
        new()
        {
            kind = StaffDiscontentFactKind.Response,
            revision = 1L,
            absoluteDay = 92,
            transition = StaffDiscontentOutcome.None,
            responseType = StaffRebellionResponseType.Calm,
            responderId = "character:host-staff-manager",
            responderDisplayName = "호스트 관리자",
            before = CreateStaffRecord(
                StaffDiscontentStage.EfficiencyDrop,
                StaffDiscontentOutcome.None,
                30f,
                2,
                1,
                91),
            after = CreateStaffRecord(
                StaffDiscontentStage.LowSatisfaction,
                StaffDiscontentOutcome.None,
                40f,
                1,
                2,
                91)
        };

    private static DungeonStaffDiscontentRecordSaveData CreateStaffRecord(
        StaffDiscontentStage stage,
        StaffDiscontentOutcome outcome,
        float mood,
        int lowMoodDays,
        int localRebellionDays,
        int lastProcessedAbsoluteDay) => new()
    {
        staffId = "character:host-staff-discontent",
        displayName = "호스트 불만 직원",
        stage = stage,
        outcome = outcome,
        mood = mood,
        lowMoodDays = lowMoodDays,
        localRebellionDays = localRebellionDays,
        lastProcessedAbsoluteDay = lastProcessedAbsoluteDay,
        permanentLoss = false,
        departed = false,
        localRebellion = false,
        ownerThreat = false,
        isolated = false,
        suppressed = false
    };

    private static StaffDiscontentState CreateStaffDiscontentState()
    {
        var state = new StaffDiscontentState();
        state.Restore(new[]
        {
            StaffDiscontentFactValues.Restore(
                CreateStaffResponseFact().after)
        });
        return state;
    }

    private static DungeonStaffDiscontentSaveData CaptureStaffDiscontent(
        StaffDiscontentState state) => new()
    {
        outcomeRevision = state.OutcomeRevision,
        transitionRevision = state.TransitionRevision,
        responseObservation = state.ResponseObservation,
        transitionObservation = state.TransitionObservation,
        records = state.CaptureSnapshots()
            .OrderBy(value => value.staffId, StringComparer.Ordinal)
            .Select(StaffDiscontentFactValues.Capture)
            .ToList()
    };

    private static DungeonGameRestoreReport ValidateStaffDiscontentJoin(
        DungeonStaffDiscontentSaveData staff,
        GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo validate = typeof(StaffDiscontentFactPreflight)
            .GetMethod(
                "Validate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(DungeonStaffDiscontentSaveData),
                    typeof(GameplayOutcomeLedgerSaveData),
                    typeof(DungeonGameRestoreReport)
                },
                modifiers: null)
            ?? throw new MissingMethodException(
                typeof(StaffDiscontentFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        validate.Invoke(null, new object[] { staff, ledger, report });
        return report;
    }

    private static CaptivityRehabilitationTerminalState
        CreateRehabilitationTerminal() => new()
    {
        outcomeRevision = 1L,
        captiveDisplayName = "호스트 재사회화 대상",
        wardenId = "character:host-captivity-rehabilitation-warden",
        wardenDisplayName = "호스트 감시자",
        facilityId = "building:host-captivity-rehabilitation",
        facilityDisplayName = "호스트 재사회화실",
        absoluteDay = 81,
        completedDay = true,
        approvedWork = 0.5f,
        workBefore = 17.5f,
        workAfter = 0f,
        rehabilitationDaysBefore = 2,
        rehabilitationDaysAfter = 3,
        trustBefore = 50f,
        trustAfter = 55f,
        grudgeBefore = 30f,
        grudgeAfter = 25f,
        corruptionBefore = 50f,
        corruptionAfter = 45f,
        foodConsumed = CaptivityStateTransitionRules.RehabilitationFoodCost
    };

    private static CaptivityReleaseTerminalState CreateReleaseTerminal() =>
        new()
        {
            outcomeRevision = 1L,
            captiveDisplayName = "호스트 석방 대상",
            deciderId = "character:host-captivity-release-owner",
            deciderDisplayName = "호스트 석방 결정자",
            facilityId = "building:host-captivity-release",
            facilityDisplayName = "호스트 감방",
            absoluteDay = 82,
            statusBefore = CaptivityStatus.Confined,
            statusAfter = CaptivityStatus.Released,
            inCustodyBefore = true,
            inCustodyAfter = false,
            restrainedBefore = true,
            restrainedAfter = false,
            actorTypeBefore = "NPC",
            actorTypeAfter = "Intruder",
            lifecycleBefore = "Active",
            lifecycleAfter = "Active",
            aiPausedBefore = false,
            aiPausedAfter = false,
            doorCaptiveBefore = true,
            doorCaptiveAfter = false,
            physicalInputsReturned = true
        };

    private static CaptiveState CreateRehabilitationState() => new()
    {
        captiveId = "character:host-captivity-rehabilitation",
        displayName = "호스트 재사회화 대상",
        speciesTag = "human",
        status = CaptivityStatus.Minion,
        policyId = CaptivityPolicyIds.Standard,
        trust = 55f,
        grudge = 25f,
        corruption = 45f,
        completedRehabilitationWork = 0f,
        rehabilitationDays = 3,
        rehabilitationInProgress = false,
        lastRehabilitationAbsoluteDay = 81,
        rehabilitationOutcomeRevision = 0L,
        rehabilitationTerminal = CreateRehabilitationTerminal(),
        lastResult = "재사회화 3일 완료 · 결과 전달 대기"
    };

    private static CaptiveState CreateReleaseState() => new()
    {
        captiveId = "character:host-captivity-release",
        displayName = "호스트 석방 대상",
        speciesTag = "human",
        status = CaptivityStatus.Released,
        policyId = CaptivityPolicyIds.Standard,
        housingBuildingId = "building:host-captivity-release",
        restrained = false,
        releaseOutcomeRevision = 0L,
        releaseTerminal = CreateReleaseTerminal(),
        lastResult = "석방됨 · 결과 전달 대기"
    };

    private static CaptivitySaveData CreateCaptivitySave(
        params CaptiveState[] captives) => new()
    {
        captives = (captives ?? Array.Empty<CaptiveState>())
            .Select(value => value.Clone())
            .ToList(),
        policies = new List<CaptivePolicyData>
        {
            new()
            {
                policyId = CaptivityPolicyIds.Standard,
                displayName = "표준 수용"
            }
        }
    };

    private static DungeonGameRestoreReport ValidateTransitionJoin(
        CaptivitySaveData captivity,
        GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo validate = typeof(CaptivityTransitionFactPreflight)
            .GetMethod(
                "Validate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(CaptivitySaveData),
                    typeof(GameplayOutcomeLedgerSaveData),
                    typeof(DungeonGameRestoreReport)
                },
                modifiers: null)
            ?? throw new MissingMethodException(
                typeof(CaptivityTransitionFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        validate.Invoke(null, new object[] { captivity, ledger, report });
        return report;
    }

    private static CaptiveState CreateInteractionState(string suffix)
    {
        string captiveId = "character:host-captivity-interaction-" + suffix;
        string wardenId = "character:host-captivity-warden-" + suffix;
        string facilityId = "building:host-captivity-interaction-" + suffix;
        var state = new CaptiveState
        {
            captiveId = captiveId,
            displayName = "호스트 상호작용 포로",
            status = CaptivityStatus.Interaction,
            policyId = CaptivityPolicyIds.Standard,
            reservedWardenId = wardenId,
            housingBuildingId = facilityId,
            restrained = true,
            currentInteractionId = "captivity:persuasion",
            interactionMaterialDestinationId =
                "captivity-interaction-material:host-" + suffix,
            interactionMaterialsConsumed = true,
            completedInteractionWork = 1f,
            requiredInteractionWork = 1f,
            interactionAttemptSequence = 1,
            currentInteractionAttemptId = 1,
            will = 70f,
            fear = 20f,
            trust = 30f,
            grudge = 40f,
            corruption = 10f
        };
        state.interactionTerminal = new CaptivityInteractionTerminalState
        {
            captiveDisplayName = state.displayName,
            absoluteDay = 71,
            attemptId = 1,
            interactionId = state.currentInteractionId,
            interactionKind = CaptiveInteractionKind.Persuasion,
            interactionDisplayName = "회유",
            wardenId = wardenId,
            wardenDisplayName = "호스트 감시자",
            facilityId = facilityId,
            facilityDisplayName = "호스트 감방",
            resultGridX = 7,
            resultGridY = 9,
            success = true,
            message = "회유 결과",
            willBefore = 70f,
            willAfter = 67f,
            fearBefore = 20f,
            fearAfter = 22f,
            trustBefore = 30f,
            trustAfter = 35f,
            grudgeBefore = 40f,
            grudgeAfter = 38f,
            corruptionBefore = 10f,
            corruptionAfter = 11f,
            bodyDamageAmount = 12f,
            bodyHealthBefore = 80f,
            bodyHealthAfter = 68f,
            bodyMaximumHealth = 100f
        };
        return state;
    }

    private static void ApplyInteractionState(
        CaptiveState state,
        long revision)
    {
        CaptivityInteractionTerminalState terminal = state.interactionTerminal;
        state.will = terminal.willAfter;
        state.fear = terminal.fearAfter;
        state.trust = terminal.trustAfter;
        state.grudge = terminal.grudgeAfter;
        state.corruption = terminal.corruptionAfter;
        state.interactionOutcomeRevision = revision;
        terminal.outcomeRevision = revision;
    }

    private static DungeonGameRestoreReport ValidateInteractionJoin(
        CaptivitySaveData captivity,
        GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo validate = typeof(CaptivityInteractionFactPreflight)
            .GetMethod(
                "Validate",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(CaptivitySaveData),
                    typeof(GameplayOutcomeLedgerSaveData),
                    typeof(DungeonGameRestoreReport)
                },
                modifiers: null)
            ?? throw new MissingMethodException(
                typeof(CaptivityInteractionFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        validate.Invoke(null, new object[] { captivity, ledger, report });
        return report;
    }

    private static CaptiveRansomAcceptedFactData CreateRansomFactData() => new()
    {
        captiveId = RansomId("fact"),
        displayName = "호스트 몸값 포로",
        decisionMakerId = string.Empty,
        previousStatus = CaptivityStatus.Confined,
        amount = 240,
        retaliationPressure = 48f,
        revision = 1L,
        absoluteDay = 73
    };

    private static CaptiveState CreateRansomState(string suffix) => new()
    {
        captiveId = RansomId(suffix),
        displayName = "호스트 몸값 포로",
        status = CaptivityStatus.Confined,
        policyId = CaptivityPolicyIds.Standard
    };

    private static string RansomId(string suffix) =>
        "character:captive-host:ransom-" + suffix;

    private static DungeonGameRestoreReport ValidateRansomJoin(
        CaptivitySaveData captivity,
        GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo validate = typeof(CaptiveRansomFactPreflight).GetMethod(
            "Validate",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(CaptivitySaveData),
                typeof(GameplayOutcomeLedgerSaveData),
                typeof(DungeonGameRestoreReport)
            },
            modifiers: null)
            ?? throw new MissingMethodException(
                typeof(CaptiveRansomFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        validate.Invoke(null, new object[] { captivity, ledger, report });
        return report;
    }

    private static CaptiveEscapedFactData CreateEscapeFactData(
        string suffix,
        CaptiveEscapeKind kind,
        CaptivityStatus previousStatus,
        string trigger,
        float pressure,
        int absoluteDay) => new()
    {
        captiveId = EscapeId(suffix),
        displayName = "호스트 이탈자",
        kind = kind,
        trigger = trigger,
        previousStatus = previousStatus,
        retaliationPressure = pressure,
        revision = 1L,
        absoluteDay = absoluteDay
    };

    private static CaptiveState CreateEscapeState(
        string suffix,
        CaptivityStatus previousStatus) => new()
    {
        captiveId = EscapeId(suffix),
        displayName = "호스트 이탈자",
        status = previousStatus,
        policyId = CaptivityPolicyIds.Standard
    };

    private static string EscapeId(string suffix) =>
        "character:captive-host:escape-" + suffix;

    private static DungeonGameRestoreReport ValidateEscapeJoin(
        CaptivitySaveData captivity,
        GameplayOutcomeLedgerSaveData ledger)
    {
        MethodInfo validate = typeof(CaptiveEscapeFactPreflight).GetMethod(
            "Validate",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: new[]
            {
                typeof(CaptivitySaveData),
                typeof(GameplayOutcomeLedgerSaveData),
                typeof(DungeonGameRestoreReport)
            },
            modifiers: null)
            ?? throw new MissingMethodException(
                typeof(CaptiveEscapeFactPreflight).FullName,
                "Validate");
        var report = new DungeonGameRestoreReport();
        validate.Invoke(null, new object[] { captivity, ledger, report });
        return report;
    }

    private static CaptiveState CreatePerformerState(float fame = 100f) => new()
    {
        captiveId = "character:captive-host:performer",
        displayName = "호스트 공연자",
        status = CaptivityStatus.Released,
        policyId = CaptivityPolicyIds.Standard,
        performerFame = fame,
        performerSkill = 40f,
        privilegeTier = fame >= 75f ? 2 : fame >= 50f ? 1 : 0
    };

    private static int OutcomeRowCount(GameplayOutcomeLedgerSaveData value) =>
        value.outbox.Count + value.exactOutcomes.Count + value.tombstones.Count;

    private static string ResultKey(GameplayOutcomeSnapshot row) =>
        new GameplayResultKey(
            row.producerId,
            new GameplayOperationId(row.operationId),
            row.commitRevision,
            row.localResultIndex).ToString();

    private static string ResultKey(GameplayOutcomeTombstoneSnapshot row) =>
        new GameplayResultKey(
            row.producerId,
            new GameplayOperationId(row.operationId),
            row.commitRevision,
            row.localResultIndex).ToString();

    private static string OutcomeKeysForDiagnostics(
        GameplayOutcomeLedgerSaveData value) => string.Join(
        ",",
        value.outbox.Select(ResultKey)
            .Concat(value.exactOutcomes.Select(ResultKey))
            .Concat(value.tombstones.Select(ResultKey))
            .OrderBy(key => key, StringComparer.Ordinal));

    private static IReadOnlyDictionary<string, string> CanonicalHashes(
        GameplayOutcomeLedgerSaveData value)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (GameplayOutcomeSnapshot row in value.outbox.Concat(
                     value.exactOutcomes))
        {
            string key = new GameplayResultKey(
                row.producerId,
                new GameplayOperationId(row.operationId),
                row.commitRevision,
                row.localResultIndex).ToString();
            Require(row.immutablePayloadHash?.Length == 64,
                "Canonical outcome hash was missing for " + key + ".");
            Require(result.TryAdd(key, row.immutablePayloadHash),
                "Duplicate canonical outcome key: " + key + ".");
        }
        foreach (GameplayOutcomeTombstoneSnapshot row in value.tombstones)
        {
            string key = new GameplayResultKey(
                row.producerId,
                new GameplayOperationId(row.operationId),
                row.commitRevision,
                row.localResultIndex).ToString();
            Require(row.canonicalHash?.Length == 64,
                "Canonical tombstone hash was missing for " + key + ".");
            Require(result.TryAdd(key, row.canonicalHash),
                "Duplicate canonical tombstone key: " + key + ".");
        }
        return result;
    }

    private static void RequireCanonicalEqual(
        IReadOnlyDictionary<string, string> expected,
        IReadOnlyDictionary<string, string> actual,
        string message)
    {
        Require(expected.Count == actual.Count
            && expected.All(pair => actual.TryGetValue(pair.Key, out string hash)
                && hash == pair.Value), message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private readonly struct CommitResult
    {
        public CommitResult(string publicationKey, DomainFactDelivery delivery)
        {
            PublicationKey = publicationKey;
            Delivery = delivery;
        }

        public string PublicationKey { get; }
        public DomainFactDelivery Delivery { get; }
    }

    private readonly struct ActivityAlertExpectation
    {
        public ActivityAlertExpectation(
            string title,
            string detail,
            EventAlertImportance importance,
            string category)
        {
            Title = title;
            Detail = detail;
            Importance = importance;
            Category = category;
        }

        public string Title { get; }
        public string Detail { get; }
        public EventAlertImportance Importance { get; }
        public string Category { get; }
    }

    private readonly struct EscapeHostCase
    {
        public EscapeHostCase(
            string suffix,
            CaptiveEscapeKind kind,
            CaptivityStatus previousStatus,
            string trigger,
            float pressure)
        {
            Suffix = suffix;
            Kind = kind;
            PreviousStatus = previousStatus;
            Trigger = trigger;
            Pressure = pressure;
        }

        public string Suffix { get; }
        public CaptiveEscapeKind Kind { get; }
        public CaptivityStatus PreviousStatus { get; }
        public string Trigger { get; }
        public float Pressure { get; }
    }

    private sealed class SavedSkillPublicationAuthority
    {
        public SavedSkillPublicationAuthority()
        {
        }

        public string serializer = string.Empty;
        public CharacterSkillActivationPublicationSaveData publications = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedBattleCommandAuthority
    {
        public SavedBattleCommandAuthority()
        {
        }

        public string serializer = string.Empty;
        public OffenseBattleCommandPublicationSaveData publications = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedSpeciesActionAuthority
    {
        public SavedSpeciesActionAuthority()
        {
        }

        public string serializer = string.Empty;
        public CharacterSpeciesRuntimeSaveData species = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedWorkAccidentAuthority
    {
        public string serializer = string.Empty;
        public WorkAccidentFactSaveData fact = new();
        public string publicationKey = string.Empty;
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedRepairWorkFactAuthority
    {
        public string serializer = string.Empty;
        public RepairWorkOutcomeSaveData owner = new();
        public string publicationKey = string.Empty;
        public string contextHash = string.Empty;
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedConstructionWorkEndedAuthority
    {
        public string serializer = string.Empty;
        public ConstructionWorkEndedData fact = new();
        public string publicationKey = string.Empty;
        public string contextHash = string.Empty;
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedPrimitiveSurvivalAuthority
    {
        public string serializer = string.Empty;
        public PrimitiveSurvivalPendingSaveData pending = new();
        public string publicationKey = string.Empty;
        public string contextHash = string.Empty;
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedApparelWorkCompletedAuthority
    {
        public string serializer = string.Empty;
        public ApparelWorkOrderSaveData owner = new();
        public string publicationKey = string.Empty;
        public string contextHash = string.Empty;
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedPhysicalCarcassCohortAuthority
    {
        public string serializer = string.Empty;
        public PhysicalItemTransformationSaveData physical = new();
        public WildlifeCarcassProcessingSaveData carcass = new();
        public string physicalContextHash = string.Empty;
        public string carcassContextHash = string.Empty;
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedSurvivalMealMissedCohortAuthority
    {
        public string serializer = string.Empty;
        public SurvivalMealMissedCohortSaveData cohort = new();
        public string firstContextHash = string.Empty;
        public string secondContextHash = string.Empty;
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedAuthority
    {
        public SavedAuthority()
        {
        }

        public string serializer = string.Empty;
        public CombatActionPendingSaveData pending = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedCraftAuthority
    {
        public SavedCraftAuthority()
        {
        }

        public string serializer = string.Empty;
        public string publicationKey = string.Empty;
        public CombatEquipmentCraftCompletionFactData fact = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedQualityAuthority
    {
        public SavedQualityAuthority()
        {
        }

        public string serializer = string.Empty;
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedIdleAuthority
    {
        public SavedIdleAuthority()
        {
        }

        public string serializer = string.Empty;
        public IdleSocialInteractionPendingSaveData pending = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedMilestoneAuthority
    {
        public SavedMilestoneAuthority()
        {
        }

        public string serializer = string.Empty;
        public CaptivitySaveData captivity = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedEscapeAuthority
    {
        public SavedEscapeAuthority()
        {
        }

        public string serializer = string.Empty;
        public CaptivitySaveData captivity = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedRansomAuthority
    {
        public SavedRansomAuthority()
        {
        }

        public string serializer = string.Empty;
        public CaptivitySaveData captivity = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedInteractionAuthority
    {
        public SavedInteractionAuthority()
        {
        }

        public string serializer = string.Empty;
        public CaptivitySaveData captivity = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedTransitionAuthority
    {
        public SavedTransitionAuthority()
        {
        }

        public string serializer = string.Empty;
        public CaptivitySaveData captivity = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedCharacterRestAuthority
    {
        public SavedCharacterRestAuthority()
        {
        }

        public string serializer = string.Empty;
        public DungeonCharacterWorldSaveData characters = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class SavedStaffDiscontentAuthority
    {
        public SavedStaffDiscontentAuthority()
        {
        }

        public string serializer = string.Empty;
        public DungeonStaffDiscontentSaveData staff = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class SavedOffenseNodeAuthority
    {
        public SavedOffenseNodeAuthority()
        {
        }

        public string serializer = string.Empty;
        public OffenseNodePublicationSaveData publications = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class OffenseNodeAuthority : IDisposable
    {
        private readonly Authority inner;
        private readonly IDisposable binding;

        public OffenseNodeAuthority(bool recorderFaults)
        {
            inner = new Authority(
                connect: false,
                recorderFaults: recorderFaults);
            Observer = new OffenseNodeFactObserver(
                inner.Migrated,
                new GameplayDomainFactCommitGroup(
                    inner.Recorder,
                    inner.Ledger));
            binding = Observer.Connect(inner.Dispatcher);
        }

        public GameplayOutcomeLedger Ledger => inner.Ledger;
        public FaultingRecorder Faults => inner.Faults;
        public MigratedProducerOutcomeRuntime Migrated => inner.Migrated;
        public DomainFactDispatcher Dispatcher => inner.Dispatcher;
        public OffenseNodeFactObserver Observer { get; }

        public void Restore(
            GameplayOutcomeLedgerSaveData ledger,
            DungeonMigratedProducerOutcomeSaveData migrated) =>
            inner.Restore(ledger, migrated);

        public void Dispose()
        {
            binding.Dispose();
            inner.Dispose();
        }
    }

    private sealed class OffenseNodeFaultPublisher : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;

        public OffenseNodeFaultPublisher(IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        public bool RejectReserve { get; set; }
        public bool RejectPrepare { get; set; }
        public bool HideCompletedReply { get; set; }
        public Action DuringPublish { get; set; }
        public bool ActiveGuardRejected { get; private set; }
        public int HiddenReplyCount { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure)
        {
            if (RejectReserve)
            {
                lease = default;
                failure = "host-offense-node-reserve-rejected";
                return false;
            }
            return inner.TryReserve(proposed, out lease, out failure);
        }

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (DuringPublish != null)
            {
                try
                {
                    DuringPublish();
                }
                catch (InvalidOperationException)
                {
                    ActiveGuardRejected = true;
                }
            }
            if (RejectPrepare && actual is OffenseNodeResolutionFact fact)
            {
                RejectPrepare = false;
                OffenseNodeResolutionFactData changed = fact.Snapshot();
                changed.nodeTitle += " · 준비 변조";
                return inner.Publish(
                    lease,
                    new OffenseNodeResolutionFact(changed));
            }
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (HideCompletedReply && delivered.Completed)
            {
                HideCompletedReply = false;
                HiddenReplyCount++;
                return DomainFactDelivery.Uncertain(
                    "host-offense-node-completed-reply-hidden");
            }
            return delivered;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            inner.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class CaptureDuringOffenseNodeRecovery :
        IDomainFactRecoveryPublisher
    {
        private readonly IDomainFactRecoveryPublisher inner;
        private readonly Action capture;

        public CaptureDuringOffenseNodeRecovery(
            IDomainFactRecoveryPublisher inner,
            Action capture)
        {
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));
            this.capture = capture
                ?? throw new ArgumentNullException(nameof(capture));
        }

        public bool CaptureRejected { get; private set; }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            try
            {
                capture();
            }
            catch (InvalidOperationException)
            {
                CaptureRejected = true;
            }
            return inner.ReplayBatch(facts, results, out failure);
        }
    }

    private sealed class SavedOffenseArrivalSettlementAuthority
    {
        public SavedOffenseArrivalSettlementAuthority()
        {
        }

        public string serializer = string.Empty;
        public OffenseReturnArrivalState arrival = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class OffenseArrivalSettlementAuthority : IDisposable
    {
        private readonly Authority inner;
        private readonly IDisposable binding;

        public OffenseArrivalSettlementAuthority(bool recorderFaults)
        {
            inner = new Authority(
                connect: false,
                recorderFaults: recorderFaults);
            Observer = new OffenseArrivalSettlementFactObserver(
                inner.Migrated,
                new GameplayDomainFactCommitGroup(
                    inner.Recorder,
                    inner.Ledger));
            binding = Observer.Connect(inner.Dispatcher);
        }

        public GameplayOutcomeLedger Ledger => inner.Ledger;
        public FaultingRecorder Faults => inner.Faults;
        public MigratedProducerOutcomeRuntime Migrated => inner.Migrated;
        public DomainFactDispatcher Dispatcher => inner.Dispatcher;
        public OffenseArrivalSettlementFactObserver Observer { get; }

        public void Restore(
            GameplayOutcomeLedgerSaveData ledger,
            DungeonMigratedProducerOutcomeSaveData migrated) =>
            inner.Restore(ledger, migrated);

        public void Dispose()
        {
            binding.Dispose();
            inner.Dispose();
        }
    }

    private sealed class OffenseArrivalSettlementFaultPublisher :
        IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;

        public OffenseArrivalSettlementFaultPublisher(
            IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        public bool RejectReserve { get; set; }
        public bool RejectPrepare { get; set; }
        public bool HideCompletedReply { get; set; }
        public Action DuringPublish { get; set; }
        public bool BusyGuardRejected { get; private set; }
        public int HiddenReplyCount { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure)
        {
            if (RejectReserve)
            {
                lease = default;
                failure = "host-offense-arrival-reserve-rejected";
                return false;
            }
            return inner.TryReserve(proposed, out lease, out failure);
        }

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (DuringPublish != null)
            {
                try
                {
                    DuringPublish();
                }
                catch (InvalidOperationException)
                {
                    BusyGuardRejected = true;
                }
            }
            if (RejectPrepare && actual is OffenseArrivalSettledFact fact)
            {
                RejectPrepare = false;
                OffenseArrivalSettlementFactData changed = fact.Snapshot();
                changed.absoluteDay++;
                return inner.Publish(
                    lease,
                    new OffenseArrivalSettledFact(changed));
            }
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (HideCompletedReply && delivered.Completed)
            {
                HideCompletedReply = false;
                HiddenReplyCount++;
                return DomainFactDelivery.Uncertain(
                    "host-offense-arrival-completed-reply-hidden");
            }
            return delivered;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            inner.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class RequireIdleDuringOffenseArrivalRecovery :
        IDomainFactRecoveryPublisher
    {
        private readonly IDomainFactRecoveryPublisher inner;
        private readonly Action requireIdle;

        public RequireIdleDuringOffenseArrivalRecovery(
            IDomainFactRecoveryPublisher inner,
            Action requireIdle)
        {
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));
            this.requireIdle = requireIdle
                ?? throw new ArgumentNullException(nameof(requireIdle));
        }

        public bool BusyGuardRejected { get; private set; }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            try
            {
                requireIdle();
            }
            catch (InvalidOperationException)
            {
                BusyGuardRejected = true;
            }
            return inner.ReplayBatch(facts, results, out failure);
        }
    }

    private sealed class SavedOffenseExpeditionResultAuthority
    {
        public SavedOffenseExpeditionResultAuthority()
        {
        }

        public string serializer = string.Empty;
        public OffenseExpeditionResultPublicationSaveData publications = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
        public DungeonMigratedProducerOutcomeSaveData migrated = new();
    }

    private sealed class OffenseExpeditionResultAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public OffenseExpeditionResultAuthority(bool recorderFaults)
        {
            var josa = new KoreanJosaFormatter();
            Registry = new GameplayOutcomeRegistry(
                MigratedProducerOutcomeCatalog.Definitions.Select(value =>
                    (IGameplayOutcomeDescriptor)
                    new MigratedProducerOutcomeDescriptor(value, josa)),
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new MigratedProducerOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 32,
                largePageCount: 2,
                knownResultKeyCapacity: 64,
                retainedSmallPageCount: 16,
                retainedLargePageCount: 2);
            string runId = "run:offense-result-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            RealRecorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            Faults = recorderFaults
                ? new FaultingRecorder(RealRecorder)
                : null;
            Recorder = Faults ?? (IGameplayOutcomeRecorder)RealRecorder;
            Roots = new DungeonRuntimeAggregateRootStore();
            Migrated = new MigratedProducerOutcomeRuntime(
                Roots,
                Ledger,
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            Observer = new OffenseExpeditionResultFactObserver(
                Migrated,
                new GameplayDomainFactCommitGroup(Recorder, Ledger));
            binding = Observer.Connect(Dispatcher);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder RealRecorder { get; }
        public FaultingRecorder Faults { get; }
        public IGameplayOutcomeRecorder Recorder { get; }
        public DungeonRuntimeAggregateRootStore Roots { get; }
        public MigratedProducerOutcomeRuntime Migrated { get; }
        public DomainFactDispatcher Dispatcher { get; }
        public OffenseExpeditionResultFactObserver Observer { get; }

        public void Restore(
            GameplayOutcomeLedgerSaveData ledger,
            DungeonMigratedProducerOutcomeSaveData migrated)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
            Migrated.Restore(Migrated.PrepareRestore(migrated));
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class OffenseExpeditionResultFaultPublisher :
        IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;

        public OffenseExpeditionResultFaultPublisher(
            IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        public bool RejectReserve { get; set; }
        public bool RejectPrepare { get; set; }
        public bool LoseNextPublishReply { get; set; }
        public int DroppedReplyCount { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure)
        {
            if (RejectReserve)
            {
                lease = default;
                failure = "host-offense-result-reserve-rejected";
                return false;
            }
            return inner.TryReserve(proposed, out lease, out failure);
        }

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (RejectPrepare
                && actual is OffenseExpeditionResolvedFact fact)
            {
                RejectPrepare = false;
                OffenseExpeditionResultFactData changed = fact.Snapshot();
                changed.result.totalPower += 1f;
                return inner.Publish(
                    lease,
                    new OffenseExpeditionResolvedFact(changed));
            }
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (LoseNextPublishReply && delivered.MayBeCommitted)
            {
                LoseNextPublishReply = false;
                DroppedReplyCount++;
                return DomainFactDelivery.Uncertain("host-offense-result-publish-reply-lost");
            }
            return delivered;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            inner.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) =>
            inner.Cancel(lease);
    }

    private sealed class CaptureDuringOffenseExpeditionRecovery :
        IDomainFactRecoveryPublisher
    {
        private readonly IDomainFactRecoveryPublisher inner;
        private readonly Action capture;

        public CaptureDuringOffenseExpeditionRecovery(
            IDomainFactRecoveryPublisher inner,
            Action capture)
        {
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));
            this.capture = capture
                ?? throw new ArgumentNullException(nameof(capture));
        }

        public bool CaptureRejected { get; private set; }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            try
            {
                capture();
            }
            catch (InvalidOperationException)
            {
                CaptureRejected = true;
            }
            return inner.ReplayBatch(facts, results, out failure);
        }
    }

    private sealed class SavedOffenseTruthAuthority
    {
        public SavedOffenseTruthAuthority()
        {
        }

        public string serializer = string.Empty;
        public DungeonOffenseCampaignSaveData campaign = new();
        public GameplayOutcomeLedgerSaveData ledger = new();
    }

    private sealed class OffenseTruthAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public OffenseTruthAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new OffenseTruthRevealOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new OffenseTruthRevealOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 16,
                retainedSmallPageCount: 4,
                retainedLargePageCount: 0);
            string runId = "run:offense-truth-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Observer = new GameplayDomainFactObserver<
                OffenseTruthRevealedFact,
                OffenseTruthRevealOutcomeReceipt>(
                OffenseTruthRevealFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            Dispatcher = new DomainFactDispatcher();
            binding = Dispatcher.Subscribe(
                "log:offense.truth-reveal",
                Observer);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public GameplayDomainFactObserver<
            OffenseTruthRevealedFact,
            OffenseTruthRevealOutcomeReceipt> Observer { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Restore(GameplayOutcomeLedgerSaveData ledger)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class StaffDiscontentAuthority : IDisposable
    {
        private readonly IDisposable transitionBinding;
        private readonly IDisposable responseBinding;

        public StaffDiscontentAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new StaffDiscontentTransitionOutcomeDescriptor(
                        new KoreanJosaFormatter()),
                    new StaffDiscontentOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new StaffDiscontentTransitionOutcomeAdapter(),
                    new StaffDiscontentOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 16,
                largePageCount: 0,
                knownResultKeyCapacity: 32,
                retainedSmallPageCount: 8,
                retainedLargePageCount: 0);
            string runId = "run:staff-discontent-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            TransitionObserver = new GameplayDomainFactObserver<
                StaffDiscontentTransitionedFact,
                StaffDiscontentTransitionOutcomeReceipt>(
                StaffDiscontentFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            ResponseObserver = new GameplayDomainFactObserver<
                StaffRebellionResponseResolvedFact,
                StaffDiscontentOutcomeReceipt>(
                StaffDiscontentFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            Dispatcher = new DomainFactDispatcher();
            transitionBinding = Dispatcher.Subscribe(
                "log:staff-discontent.transition",
                TransitionObserver);
            responseBinding = Dispatcher.Subscribe(
                "log:staff-discontent.response",
                ResponseObserver);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public GameplayDomainFactObserver<
            StaffDiscontentTransitionedFact,
            StaffDiscontentTransitionOutcomeReceipt> TransitionObserver
            { get; }
        public GameplayDomainFactObserver<
            StaffRebellionResponseResolvedFact,
            StaffDiscontentOutcomeReceipt> ResponseObserver { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Restore(GameplayOutcomeLedgerSaveData ledger)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            responseBinding.Dispose();
            transitionBinding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class TransitionAuthority : IDisposable
    {
        private readonly IDisposable rehabilitationBinding;
        private readonly IDisposable releaseBinding;

        public TransitionAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptivityRehabilitationOutcomeDescriptor(),
                    new CaptivityReleaseOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptivityRehabilitationOutcomeAdapter(),
                    new CaptivityReleaseOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 16,
                largePageCount: 0,
                knownResultKeyCapacity: 32,
                retainedSmallPageCount: 8,
                retainedLargePageCount: 0);
            string runId = "run:captivity-transition-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            RehabilitationObserver = new GameplayDomainFactObserver<
                CaptivityRehabilitationProgressedFact,
                CaptivityRehabilitationOutcomeReceipt>(
                CaptivityTransitionFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            ReleaseObserver = new GameplayDomainFactObserver<
                CaptivityReleasedFact,
                CaptivityReleaseOutcomeReceipt>(
                CaptivityTransitionFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            Dispatcher = new DomainFactDispatcher();
            rehabilitationBinding = Dispatcher.Subscribe(
                "log:captivity.rehabilitation",
                RehabilitationObserver);
            releaseBinding = Dispatcher.Subscribe(
                "log:captivity.release",
                ReleaseObserver);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public GameplayDomainFactObserver<
            CaptivityRehabilitationProgressedFact,
            CaptivityRehabilitationOutcomeReceipt> RehabilitationObserver
            { get; }
        public GameplayDomainFactObserver<
            CaptivityReleasedFact,
            CaptivityReleaseOutcomeReceipt> ReleaseObserver { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Restore(GameplayOutcomeLedgerSaveData ledger)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            releaseBinding.Dispose();
            rehabilitationBinding.Dispose();
            Dispatcher.Dispose();
        }
    }

    /// <summary>
    /// The production interaction publication owner is intentionally internal.
    /// This adapter invokes only its managed instance contract and does not
    /// widen the gameplay API for the host probe.
    /// </summary>
    private sealed class InteractionRuntimeReflection
    {
        private readonly object runtime;
        private readonly MethodInfo tryPrepare;
        private readonly MethodInfo commit;
        private readonly MethodInfo tryEnsureDelivered;
        private readonly MethodInfo tryEnsureOutput;
        private readonly MethodInfo validateBeforeCapture;

        public InteractionRuntimeReflection(
            IDomainFactStagingPublisher publisher,
            IDomainFactRecoveryPublisher recoveryPublisher,
            IPhysicalItemSourcePublicationService physicalSources)
        {
            Type runtimeType = typeof(CaptivePerformerMilestoneOutcomeRuntime)
                .Assembly.GetType(
                    "CaptivityInteractionOutcomeRuntime",
                    throwOnError: true)
                ?? throw new TypeLoadException(
                    "CaptivityInteractionOutcomeRuntime was not found.");
            ConstructorInfo constructor = runtimeType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(IDomainFactStagingPublisher),
                    typeof(IDomainFactRecoveryPublisher),
                    typeof(IPhysicalItemSourcePublicationService)
                },
                modifiers: null)
                ?? throw new MissingMethodException(
                    runtimeType.FullName,
                    ".ctor");
            runtime = constructor.Invoke(new object[]
            {
                publisher,
                recoveryPublisher,
                physicalSources
            });
            tryPrepare = RequiredMethod(runtimeType, "TryPrepare");
            commit = RequiredMethod(runtimeType, "Commit");
            tryEnsureDelivered = RequiredMethod(
                runtimeType,
                "TryEnsureDelivered");
            tryEnsureOutput = RequiredMethod(runtimeType, "TryEnsureOutput");
            validateBeforeCapture = RequiredMethod(
                runtimeType,
                "ValidateBeforeCapture");
        }

        public bool TryPrepare(
            CaptiveState state,
            out IDisposable prepared,
            out long revision,
            out string failureReason)
        {
            object[] arguments = { state, null, 0L, null };
            bool success = (bool)Invoke(tryPrepare, arguments);
            prepared = arguments[1] as IDisposable;
            revision = (long)arguments[2];
            failureReason = (string)arguments[3] ?? string.Empty;
            return success;
        }

        public DomainFactDelivery Commit(IDisposable prepared) =>
            (DomainFactDelivery)Invoke(commit, new object[] { prepared });

        public bool TryEnsureDelivered(
            CaptiveState state,
            out string failureReason)
        {
            object[] arguments = { state, null };
            bool success = (bool)Invoke(tryEnsureDelivered, arguments);
            failureReason = (string)arguments[1] ?? string.Empty;
            return success;
        }

        public bool TryEnsureOutput(
            CaptiveState state,
            out string failureReason)
        {
            object[] arguments = { state, null };
            bool success = (bool)Invoke(tryEnsureOutput, arguments);
            failureReason = (string)arguments[1] ?? string.Empty;
            return success;
        }

        public void ValidateBeforeCapture() =>
            Invoke(validateBeforeCapture, Array.Empty<object>());

        private object Invoke(MethodInfo method, object[] arguments)
        {
            try
            {
                return method.Invoke(runtime, arguments);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static MethodInfo RequiredMethod(Type type, string name) =>
            type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, name);
    }

    private sealed class InteractionAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public InteractionAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptivityInteractionOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptivityInteractionOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 16,
                retainedSmallPageCount: 4,
                retainedLargePageCount: 0);
            string runId = "run:captivity-interaction-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Observer = new GameplayDomainFactObserver<
                CaptivityInteractionResolvedFact,
                CaptivityInteractionOutcomeReceipt>(
                CaptivityInteractionFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            Dispatcher = new DomainFactDispatcher();
            binding = Dispatcher.Subscribe(
                "log:captivity.interaction-result",
                Observer);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public GameplayDomainFactObserver<
            CaptivityInteractionResolvedFact,
            CaptivityInteractionOutcomeReceipt> Observer { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Restore(GameplayOutcomeLedgerSaveData ledger)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class RecordingPhysicalSource :
        IPhysicalItemSourcePublicationService
    {
        private readonly bool commitOutputs;

        public RecordingPhysicalSource(bool commitOutputs = false) =>
            this.commitOutputs = commitOutputs;

        public int CallCount { get; private set; }

        public bool TryEnsureLooseOutputs(
            IReadOnlyDictionary<string, int> outputs,
            UnityEngine.Vector2Int outputPosition,
            string operationId,
            string reasonCode,
            out PhysicalItemSourcePublicationReceipt receipt,
            out string failureReason)
        {
            CallCount++;
            if (commitOutputs)
            {
                int quantity = (outputs
                        ?? new Dictionary<string, int>())
                    .Sum(value => Math.Max(0, value.Value));
                receipt = new PhysicalItemSourcePublicationReceipt(
                    operationId,
                    reasonCode,
                    new[] { "physical-output:host-captivity-interaction" },
                    quantity,
                    Math.Max(1, quantity) * 100L);
                failureReason = string.Empty;
                return true;
            }
            receipt = default;
            failureReason = "host-physical-output-not-run";
            return false;
        }
    }

    private sealed class RansomRuntimeReflection
    {
        private readonly object runtime;
        private readonly MethodInfo tryPrepare;
        private readonly MethodInfo commit;
        private readonly MethodInfo recover;
        private readonly MethodInfo tryBeginFinalization;
        private readonly MethodInfo notifyCommitted;
        private readonly MethodInfo validateBeforeCapture;

        public RansomRuntimeReflection(
            IDomainFactStagingPublisher publisher,
            IDomainFactRecoveryPublisher recoveryPublisher,
            IGameEventBus events,
            ICharacterAiWorldRegistry worldRegistry)
        {
            Type runtimeType = typeof(CaptivePerformerMilestoneOutcomeRuntime)
                .Assembly.GetType(
                    "CaptiveRansomOutcomeRuntime",
                    throwOnError: true)
                ?? throw new TypeLoadException(
                    "CaptiveRansomOutcomeRuntime was not found.");
            ConstructorInfo constructor = runtimeType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(IDomainFactStagingPublisher),
                    typeof(IDomainFactRecoveryPublisher),
                    typeof(IGameEventBus),
                    typeof(ICharacterAiWorldRegistry)
                },
                modifiers: null)
                ?? throw new MissingMethodException(
                    runtimeType.FullName,
                    ".ctor");
            runtime = constructor.Invoke(new object[]
            {
                publisher,
                recoveryPublisher,
                events,
                worldRegistry
            });
            tryPrepare = RequiredMethod(runtimeType, "TryPrepare");
            commit = RequiredMethod(runtimeType, "Commit");
            recover = RequiredMethod(runtimeType, "Recover");
            tryBeginFinalization = RequiredMethod(
                runtimeType,
                "TryBeginFinalization");
            notifyCommitted = RequiredMethod(runtimeType, "NotifyCommitted");
            validateBeforeCapture = RequiredMethod(
                runtimeType,
                "ValidateBeforeCapture");
        }

        public bool TryPrepare(
            CaptiveState state,
            CaptivityStatus previousStatus,
            int amount,
            float pressure,
            int absoluteDay,
            out IDisposable prepared,
            out long revision,
            out string failureReason)
        {
            object[] arguments =
            {
                state,
                previousStatus,
                amount,
                pressure,
                absoluteDay,
                null,
                0L,
                null
            };
            bool success = (bool)Invoke(tryPrepare, arguments);
            prepared = arguments[5] as IDisposable;
            revision = (long)arguments[6];
            failureReason = (string)arguments[7] ?? string.Empty;
            return success;
        }

        public DomainFactDelivery Commit(IDisposable prepared) =>
            (DomainFactDelivery)Invoke(commit, new object[] { prepared });

        public bool Recover(CaptiveState state, out string failureReason)
        {
            object[] arguments = { state, null };
            bool success = (bool)Invoke(recover, arguments);
            failureReason = (string)arguments[1] ?? string.Empty;
            return success;
        }

        public bool TryBeginFinalization(
            CaptiveState state,
            out IDisposable lease,
            out string failureReason)
        {
            object[] arguments = { state, null, null };
            bool success = (bool)Invoke(tryBeginFinalization, arguments);
            lease = arguments[1] as IDisposable;
            failureReason = (string)arguments[2] ?? string.Empty;
            return success;
        }

        public void NotifyCommitted(CaptiveState state) =>
            Invoke(notifyCommitted, new object[] { state });

        public bool IsReplay(IDisposable prepared)
        {
            PropertyInfo property = prepared?.GetType().GetProperty(
                "IsReplay",
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(
                    prepared?.GetType().FullName,
                    "IsReplay");
            return (bool)property.GetValue(prepared);
        }

        public void ValidateBeforeCapture() =>
            Invoke(validateBeforeCapture, Array.Empty<object>());

        private object Invoke(MethodInfo method, object[] arguments)
        {
            try
            {
                return method.Invoke(runtime, arguments);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static MethodInfo RequiredMethod(Type type, string name) =>
            type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, name);
    }

    private sealed class RansomAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public RansomAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptiveRansomOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptiveRansomOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 16,
                retainedSmallPageCount: 4,
                retainedLargePageCount: 0);
            string runId = "run:captive-ransom-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Observer = new GameplayDomainFactObserver<
                CaptiveRansomAcceptedFact,
                CaptiveRansomOutcomeReceipt>(
                CaptiveRansomFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            Dispatcher = new DomainFactDispatcher();
            binding = Dispatcher.Subscribe(
                "log:captivity.captive-ransom",
                Observer);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public GameplayDomainFactObserver<
            CaptiveRansomAcceptedFact,
            CaptiveRansomOutcomeReceipt> Observer { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Restore(GameplayOutcomeLedgerSaveData ledger)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class HostSessionProvider : IGameSessionStateProvider
    {
        private readonly GameSessionState state;

        public HostSessionProvider(GameSessionState state) =>
            this.state = state ?? throw new ArgumentNullException(nameof(state));

        public bool TryGetSessionState(out GameSessionState gameData)
        {
            gameData = state;
            return true;
        }
    }

    private sealed class FixedHostGameClock : IGameClock
    {
        public float DeltaTime => 0f;
        public float Time => 123f;
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private sealed class EmptyCharacterAiWorldRegistry :
        ICharacterAiWorldRegistry
    {
        public int CharacterVersion => 0;
        public IReadOnlyList<CharacterActor> Characters =>
            Array.Empty<CharacterActor>();
        public int LifetimeCharacterReadCount { get; private set; }
        public int LifetimeCharacterVersion => 0;
        public IReadOnlyList<CharacterActor> AllCharacters
        {
            get
            {
                LifetimeCharacterReadCount++;
                return Array.Empty<CharacterActor>();
            }
        }
        public int WildlifeVersion => 0;
        public IReadOnlyList<WildlifeActor> Wildlife =>
            Array.Empty<WildlifeActor>();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses =>
            Array.Empty<IWarehouseFacility>();
        public int RetailVersion => 0;
        public IReadOnlyList<IRetailFacility> RetailFacilities =>
            Array.Empty<IRetailFacility>();
        public int Version => 0;

        public void RegisterCharacter(CharacterActor actor)
        {
        }

        public void UnregisterCharacter(CharacterActor actor)
        {
        }

        public void RegisterCharacterLifetime(CharacterActor actor)
        {
        }

        public void UnregisterCharacterLifetime(CharacterActor actor)
        {
        }

        public void RegisterWildlife(WildlifeActor actor)
        {
        }

        public void UnregisterWildlife(WildlifeActor actor)
        {
        }

        public void RegisterBuilding(BuildableObject building)
        {
        }

        public void UnregisterBuilding(BuildableObject building)
        {
        }

        public int ReleaseTransientBuildingOwnership(
            IBuildingVisitorPort visitor,
            string reason) => 0;

        public int GetTransientBuildingOwnershipCount(
            CharacterId characterId) => 0;

        public void RegisterWarehouse(IWarehouseFacility warehouse)
        {
        }

        public void UnregisterWarehouse(IWarehouseFacility warehouse)
        {
        }

        public void SetGrid(Grid grid)
        {
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }

        public bool TryGetSessionState(out GameSessionState data)
        {
            data = null;
            return false;
        }

        public void Clear()
        {
        }
    }

    /// <summary>
    /// The production runtime is intentionally internal. This adapter exercises
    /// its public instance contract without widening production visibility.
    /// </summary>
    private sealed class EscapeRuntimeReflection
    {
        private readonly object runtime;
        private readonly MethodInfo tryPrepare;
        private readonly MethodInfo commit;
        private readonly MethodInfo notifyCommitted;
        private readonly MethodInfo recover;
        private readonly MethodInfo validateBeforeCapture;

        public EscapeRuntimeReflection(
            IDomainFactStagingPublisher publisher,
            IDomainFactRecoveryPublisher recoveryPublisher,
            IGameEventBus events)
        {
            Type runtimeType = typeof(CaptivePerformerMilestoneOutcomeRuntime)
                .Assembly.GetType(
                    "CaptiveEscapeOutcomeRuntime",
                    throwOnError: true)
                ?? throw new TypeLoadException(
                    "CaptiveEscapeOutcomeRuntime was not found.");
            ConstructorInfo constructor = runtimeType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic,
                binder: null,
                types: new[]
                {
                    typeof(IDomainFactStagingPublisher),
                    typeof(IDomainFactRecoveryPublisher),
                    typeof(IGameEventBus)
                },
                modifiers: null)
                ?? throw new MissingMethodException(
                    runtimeType.FullName,
                    ".ctor");
            runtime = constructor.Invoke(new object[]
            {
                publisher,
                recoveryPublisher,
                events
            });
            tryPrepare = RequiredMethod(runtimeType, "TryPrepare");
            commit = RequiredMethod(runtimeType, "Commit");
            notifyCommitted = RequiredMethod(runtimeType, "NotifyCommitted");
            recover = RequiredMethod(runtimeType, "Recover");
            validateBeforeCapture = RequiredMethod(
                runtimeType,
                "ValidateBeforeCapture");
        }

        public bool TryPrepare(
            CaptiveState state,
            CaptiveEscapeKind kind,
            string trigger,
            CaptivityStatus previousStatus,
            float pressure,
            int absoluteDay,
            out IDisposable prepared,
            out long revision,
            out string normalizedTrigger,
            out string failureReason)
        {
            object[] arguments =
            {
                state,
                kind,
                trigger,
                previousStatus,
                pressure,
                absoluteDay,
                null,
                0L,
                null,
                null
            };
            bool success = (bool)Invoke(tryPrepare, arguments);
            prepared = arguments[6] as IDisposable;
            revision = (long)arguments[7];
            normalizedTrigger = (string)arguments[8] ?? string.Empty;
            failureReason = (string)arguments[9] ?? string.Empty;
            return success;
        }

        public DomainFactDelivery Commit(IDisposable prepared) =>
            (DomainFactDelivery)Invoke(commit, new object[] { prepared });

        public void NotifyCommitted(IDisposable prepared) =>
            Invoke(notifyCommitted, new object[] { prepared });

        public bool Recover(CaptiveState state, out string failureReason)
        {
            object[] arguments = { state, null };
            bool success = (bool)Invoke(recover, arguments);
            failureReason = (string)arguments[1] ?? string.Empty;
            return success;
        }

        public bool IsReplay(IDisposable prepared)
        {
            PropertyInfo property = prepared?.GetType().GetProperty(
                "IsReplay",
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(
                    prepared?.GetType().FullName,
                    "IsReplay");
            return (bool)property.GetValue(prepared);
        }

        public void ValidateBeforeCapture() =>
            Invoke(validateBeforeCapture, Array.Empty<object>());

        private object Invoke(MethodInfo method, object[] arguments)
        {
            try
            {
                return method.Invoke(runtime, arguments);
            }
            catch (TargetInvocationException exception)
                when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        private static MethodInfo RequiredMethod(Type type, string name) =>
            type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(type.FullName, name);
    }

    private sealed class EscapeAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public EscapeAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptiveEscapeOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptiveEscapeOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 16,
                retainedSmallPageCount: 4,
                retainedLargePageCount: 0);
            string runId = "run:captive-escape-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Observer = new GameplayDomainFactObserver<
                CaptiveEscapedFact,
                CaptiveEscapeOutcomeReceipt>(
                CaptiveEscapeFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            Dispatcher = new DomainFactDispatcher();
            binding = Dispatcher.Subscribe(
                "log:captivity.captive-escape",
                Observer);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public GameplayDomainFactObserver<
            CaptiveEscapedFact,
            CaptiveEscapeOutcomeReceipt> Observer { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Restore(GameplayOutcomeLedgerSaveData ledger)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private enum MilestoneStageBehavior
    {
        RejectCommit,
        CommitThenUncertain
    }

    /// <summary>
    /// Boundary-only fault injection around a real staged observer. Rejection
    /// cancels the real reservation; uncertainty commits it before losing the
    /// reply, so neither path substitutes a fake ledger implementation.
    /// </summary>
    private sealed class MilestoneStagePublisher : IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly MilestoneStageBehavior behavior;

        public MilestoneStagePublisher(
            IDomainFactStagingPublisher inner,
            MilestoneStageBehavior behavior)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.behavior = behavior;
        }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            if (!inner.TryStage(fact, out stage, out failureReason))
                return false;
            stage = new MilestoneStage(stage, behavior);
            return true;
        }
    }

    private sealed class MilestoneStage : IDomainFactStage
    {
        private readonly IDomainFactStage inner;
        private readonly MilestoneStageBehavior behavior;

        public MilestoneStage(
            IDomainFactStage inner,
            MilestoneStageBehavior behavior)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.behavior = behavior;
        }

        public string OperationKey => inner.OperationKey;
        public long OwnerRevision => inner.OwnerRevision;
        public bool IsReplay => inner.IsReplay;
        public bool IsActive => inner.IsActive;

        public DomainFactDelivery Commit()
        {
            if (behavior == MilestoneStageBehavior.RejectCommit)
            {
                inner.Cancel();
                return DomainFactDelivery.Reject(
                    "host-captive-definite-rejection");
            }

            DomainFactDelivery committed = inner.Commit();
            return committed.MayBeCommitted
                ? DomainFactDelivery.Uncertain(
                    "host-captive-commit-reply-lost")
                : committed;
        }

        public void Cancel() => inner.Cancel();
    }

    private sealed class RecordingMilestonePort : ICaptivityPerformerPort
    {
        public List<int> Thresholds { get; } = new();
        public int AlertCount { get; private set; }

        public bool IsActorAvailable(string captiveId) => true;

        public void ApplyAssignmentState(string captiveId, bool assigned)
        {
        }

        public void Publish(CaptivePerformerMilestoneEvent gameEvent) =>
            Thresholds.Add(gameEvent.FameThreshold);

        public void RaiseAlert(
            string title,
            string message,
            CaptivityMilestoneImportance importance,
            string category) => AlertCount++;
    }

    private sealed class MilestoneAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public MilestoneAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CaptivePerformerMilestoneOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CaptivePerformerMilestoneOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 16,
                retainedSmallPageCount: 4,
                retainedLargePageCount: 0);
            string runId = "run:captive-performer-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Observer = new GameplayDomainFactObserver<
                CaptivePerformerMilestoneUnlockedFact,
                CaptivePerformerMilestoneOutcomeReceipt>(
                CaptivePerformerMilestoneFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            Dispatcher = new DomainFactDispatcher();
            binding = Dispatcher.Subscribe(
                "log:captivity.performer-milestone",
                Observer);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public GameplayDomainFactObserver<
            CaptivePerformerMilestoneUnlockedFact,
            CaptivePerformerMilestoneOutcomeReceipt> Observer { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Restore(GameplayOutcomeLedgerSaveData ledger)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class IdleAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public IdleAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new IdleSocialInteractionOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new IdleSocialInteractionOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 16,
                retainedSmallPageCount: 4,
                retainedLargePageCount: 0);
            string runId = "run:idle-social-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Observer = new GameplayDomainFactObserver<
                IdleSocialInteractionResolvedFact,
                IdleSocialInteractionOutcomeReceipt>(
                IdleSocialInteractionFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup,
                IdleSocialInteractionFactMapping.ReserveShape);
            Dispatcher = new DomainFactDispatcher();
            binding = Dispatcher.Subscribe(
                "log:social.idle-interaction",
                Observer);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public GameplayDomainFactObserver<
            IdleSocialInteractionResolvedFact,
            IdleSocialInteractionOutcomeReceipt> Observer { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Restore(GameplayOutcomeLedgerSaveData ledger)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class BattleCommandAuthority : IDisposable
    {
        private readonly IDisposable commandBinding;
        private readonly IDisposable skillBinding;

        public BattleCommandAuthority(bool recorderFaults)
        {
            var josa = new KoreanJosaFormatter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CharacterSkillActivationOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CharacterSkillActivationOutcomeAdapter(),
                    new MigratedProducerOutcomeAdapter()
                },
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    new MigratedProducerOutcomeDescriptorCatalog(josa)
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 32,
                largePageCount: 2,
                knownResultKeyCapacity: 128,
                retainedSmallPageCount: 16,
                retainedLargePageCount: 2);
            string runId = "run:battle-command-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            RealRecorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            Faults = recorderFaults
                ? new FaultingRecorder(RealRecorder)
                : null;
            Recorder = Faults ?? (IGameplayOutcomeRecorder)RealRecorder;
            Roots = new DungeonRuntimeAggregateRootStore();
            Migrated = new MigratedProducerOutcomeRuntime(
                Roots,
                Ledger,
                Recorder,
                Ledger);
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            commandBinding = new OffenseBattleCommandFactObserver(
                    Migrated,
                    CommitGroup)
                .Connect(Dispatcher);
            skillBinding = CharacterSkillActivationFactMapping.CreateBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    CommitGroup)
                .Connect(Dispatcher);
            Port = new BattleCommandPublisherPort(Dispatcher);
            Runtime = new OffenseBattleCommandPublicationRuntime(Port, Port);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder RealRecorder { get; }
        public FaultingRecorder Faults { get; }
        public IGameplayOutcomeRecorder Recorder { get; }
        public DungeonRuntimeAggregateRootStore Roots { get; }
        public MigratedProducerOutcomeRuntime Migrated { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public DomainFactDispatcher Dispatcher { get; }
        public BattleCommandPublisherPort Port { get; }
        public OffenseBattleCommandPublicationRuntime Runtime { get; }

        public void Restore(
            GameplayOutcomeLedgerSaveData ledger,
            DungeonMigratedProducerOutcomeSaveData migrated,
            OffenseBattleCommandPublicationSaveData publications)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
            Migrated.Restore(Migrated.PrepareRestore(migrated));
            Runtime.Restore(publications);
        }

        public void Dispose()
        {
            skillBinding.Dispose();
            commandBinding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class BattleCommandPublisherPort :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        public BattleCommandPublisherPort(DomainFactDispatcher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        public bool RejectNextBatch { get; set; }
        public bool HideCompletedBatchReplies { get; set; }
        public int HiddenBatchReplyCount { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual) => inner.Publish(lease, actual);

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (RejectNextBatch)
            {
                RejectNextBatch = false;
                foreach (DomainFactEmission fact in facts)
                    inner.Cancel(fact.Lease);
                failure = "host-battle-command-explicit-rejection";
                for (int index = 0; index < results.Length; index++)
                    results[index] = DomainFactDelivery.Reject(failure);
                return false;
            }

            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (HideCompletedBatchReplies)
            {
                HideCompletedBatchReplies = false;
                HiddenBatchReplyCount++;
                failure = "host-battle-command-completed-reply-hidden";
                for (int index = 0; index < results.Length; index++)
                    results[index] = DomainFactDelivery.Uncertain(failure);
            }
            return accepted;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            inner.ReplayBatch(facts, results, out failure);
    }

    private sealed class SkillPublicationPublisherPort :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;

        public SkillPublicationPublisherPort(DomainFactDispatcher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        public bool RejectNextPublish { get; set; }
        public bool ThrowAfterNextPublish { get; set; }
        public int AfterPublishFaultCount { get; private set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            if (RejectNextPublish)
            {
                RejectNextPublish = false;
                inner.Cancel(lease);
                return DomainFactDelivery.Reject(
                    "host-skill-publication-explicit-rejection");
            }
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (ThrowAfterNextPublish && delivered.MayBeCommitted)
            {
                ThrowAfterNextPublish = false;
                AfterPublishFaultCount++;
                throw new InvalidOperationException(
                    "host-skill-publication-after-commit");
            }
            return delivered;
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            inner.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            inner.ReplayBatch(facts, results, out failure);
    }

    private sealed class SpeciesActionCommitThenInDoubtPublisher :
        IDomainFactPublisher
    {
        private readonly DomainFactDispatcher inner;

        public SpeciesActionCommitThenInDoubtPublisher(
            DomainFactDispatcher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        public bool HideNextCompletedReply { get; set; }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (!HideNextCompletedReply || !delivered.Completed)
                return delivered;
            HideNextCompletedReply = false;
            return new DomainFactDelivery(
                DomainFactDeliveryPhase.InDoubt,
                delivered.OperationKey,
                delivered.PublicationId,
                delivered.Fingerprint,
                "host-species-action-commit-reply-hidden");
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            inner.PublishBatch(facts, results, out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    private sealed class SpeciesActionAuthority : IDisposable
    {
        private readonly IDisposable golemBinding;
        private readonly IDisposable incidentBinding;
        private readonly IDisposable harpyBinding;

        public SpeciesActionAuthority()
        {
            var josa = new KoreanJosaFormatter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new GolemUpkeepOutcomeDescriptor(josa),
                    new SpeciesIncidentOutcomeDescriptor(josa),
                    new HarpyGaleRelocationOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new GolemUpkeepOutcomeAdapter(),
                    new SpeciesIncidentOutcomeAdapter(),
                    new HarpyGaleRelocationOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(
                    "run:species-action-host:"
                    + (++authoritySequence).ToString(
                        CultureInfo.InvariantCulture)),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            golemBinding = SpeciesActionFactMapping.CreateGolemBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    CommitGroup)
                .Connect(Dispatcher);
            incidentBinding = SpeciesActionFactMapping
                .CreateSpeciesIncidentBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    CommitGroup)
                .Connect(Dispatcher);
            harpyBinding = SpeciesActionFactMapping.CreateHarpyGaleBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    CommitGroup)
                .Connect(Dispatcher);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void RestoreLedger(GameplayOutcomeLedgerSaveData source)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate =
                Ledger.PrepareGameplayOutcomeRestore(source);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            harpyBinding.Dispose();
            incidentBinding.Dispose();
            golemBinding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class WildlifeFoodRaidFactAuthority : IDisposable
    {
        private readonly IDisposable physicalConnection;

        public WildlifeFoodRaidFactAuthority()
        {
            var josa = new KoreanJosaFormatter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new TradeInventoryOutcomeDescriptor(josa),
                    new WildlifeFoodRaidOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new TradeInventoryOutcomeAdapter(),
                    new WildlifeFoodRaidOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(
                    "run:wildlife-food-raid-host:"
                    + (++authoritySequence).ToString(
                        CultureInfo.InvariantCulture)),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            physicalConnection = Dispatcher.Subscribe(
                "physical-disposition",
                new GameplayDomainFactObserver<
                    PhysicalItemDisposedFact,
                    TradeInventoryOutcomeReceipt>(
                    PhysicalItemDispositionFactObservation.Capture,
                    Registry,
                    Ledger,
                    Recorder,
                    CommitGroup));
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Dispose()
        {
            physicalConnection.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class WildlifeFactAuthority : IDisposable
    {
        public WildlifeFactAuthority()
        {
            var josa = new KoreanJosaFormatter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new WildlifeCommandOutcomeDescriptor(josa),
                    new WildlifeActionOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new WildlifeCommandOutcomeAdapter(),
                    new WildlifeActionOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(
                    "run:wildlife-fact-host:"
                    + (++authoritySequence).ToString(
                        CultureInfo.InvariantCulture)),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Dispose() => Dispatcher.Dispose();
    }

    private sealed class WorkAccidentAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public WorkAccidentAuthority()
        {
            var josa = new KoreanJosaFormatter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new ProcessAccidentOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new ProcessAccidentOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(
                    "run:work-accident-host:"
                    + (++authoritySequence).ToString(
                        CultureInfo.InvariantCulture)),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            binding = WorkAccidentFactMapping.CreateBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    CommitGroup)
                .Connect(Dispatcher);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void RestoreLedger(GameplayOutcomeLedgerSaveData source)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate =
                Ledger.PrepareGameplayOutcomeRestore(source);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private enum EmergencySuspensionStageBehavior
    {
        Reject,
        CommitThenUncertain
    }

    private sealed class EmergencySuspensionStagePublisher :
        IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;
        private readonly EmergencySuspensionStageBehavior behavior;

        public EmergencySuspensionStagePublisher(
            IDomainFactStagingPublisher inner,
            EmergencySuspensionStageBehavior behavior)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.behavior = behavior;
        }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            if (!inner.TryStage(fact, out stage, out failure))
                return false;
            stage = new EmergencySuspensionStage(stage, behavior);
            return true;
        }
    }

    private sealed class EmergencySuspensionStage : IDomainFactStage
    {
        private readonly IDomainFactStage inner;
        private readonly EmergencySuspensionStageBehavior behavior;

        public EmergencySuspensionStage(
            IDomainFactStage inner,
            EmergencySuspensionStageBehavior behavior)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.behavior = behavior;
        }

        public string OperationKey => inner.OperationKey;
        public long OwnerRevision => inner.OwnerRevision;
        public bool IsReplay => inner.IsReplay;
        public bool IsActive => inner.IsActive;

        public DomainFactDelivery Commit()
        {
            if (behavior == EmergencySuspensionStageBehavior.Reject)
            {
                inner.Cancel();
                return DomainFactDelivery.Reject(
                    "host-emergency-suspension-known-rejection");
            }

            DomainFactDelivery committed = inner.Commit();
            return committed.MayBeCommitted
                ? DomainFactDelivery.Uncertain(
                    "host-emergency-suspension-commit-reply-lost")
                : committed;
        }

        public void Cancel() => inner.Cancel();
    }

    private sealed class EmergencySuspensionRecoveryPublisher :
        IDomainFactRecoveryPublisher
    {
        private readonly IDomainFactRecoveryPublisher inner;

        public EmergencySuspensionRecoveryPublisher(
            IDomainFactRecoveryPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        public bool RejectNextReplay { get; set; }
        public int RejectedReplayCount { get; private set; }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            if (!RejectNextReplay)
                return inner.ReplayBatch(facts, results, out failure);
            RejectNextReplay = false;
            RejectedReplayCount++;
            failure = "host-emergency-suspension-recovery-unavailable";
            for (int index = 0; index < results.Length; index++)
                results[index] = DomainFactDelivery.Reject(failure);
            return false;
        }
    }

    private sealed class EmergencySuspensionCalendar : IGameCalendar
    {
        private int day = 42;
        private int hour;

        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);

        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);

        public void Start() => IsRunning = true;

        public void SetDateTime(int nextDay, int nextHour)
        {
            day = Math.Max(1, nextDay);
            hour = Math.Clamp(nextHour, 0, 23);
        }
    }

    private sealed class EmergencySuspensionAuthority : IDisposable
    {
        private const string CharacterId =
            "character:emergency-host:worker";
        private const string BuildingId =
            "building:emergency-host:forge";
        private readonly IDisposable binding;

        public EmergencySuspensionAuthority()
        {
            Calendar = new EmergencySuspensionCalendar();
            Events = new GameEventBus();
            Accounting = new EmergencyWorkAccountingRuntime(Events);
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new EmergencyWorkSuspensionOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new EmergencyWorkSuspensionOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(
                    "run:emergency-suspension-host:"
                    + (++authoritySequence).ToString(
                        CultureInfo.InvariantCulture)),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                Events);
            Dispatcher = new DomainFactDispatcher();
            binding = EmergencyWorkSuspensionFactMapping.CreateBinding(
                    Registry,
                    Ledger,
                    Recorder,
                    new GameplayDomainFactCommitGroup(Recorder, Ledger))
                .Connect(Dispatcher);
        }

        public EmergencySuspensionCalendar Calendar { get; }
        public GameEventBus Events { get; }
        public EmergencyWorkAccountingRuntime Accounting { get; }
        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public SettlementAlertRuntime CreateRuntime(
            IDomainFactStagingPublisher staging = null,
            IDomainFactRecoveryPublisher recovery = null) => new(
            Calendar,
            Events,
            Accounting,
            Accounting,
            staging ?? Dispatcher,
            recovery ?? Dispatcher);

        public SettlementSuspendedWorkSnapshot CreateSnapshot(
            SettlementAlertRuntime runtime,
            long ownerRevision) => new(
            CharacterId,
            BuiltInWorkTypeIds.Construct,
            BuildingId,
            runtime.Capture().AlertEpochId,
            Calendar.AbsoluteHour,
            progressExternallyPersisted: false,
            inlineCompletedWork: 2f,
            inlineRequiredWork: 10f,
            outcomeOwnerRevision: ownerRevision,
            workerDisplayName: "호스트 긴급 작업자",
            targetDisplayName: "호스트 긴급 대장간");

        public void RestoreLedger(GameplayOutcomeLedgerSaveData source)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate =
                Ledger.PrepareGameplayOutcomeRestore(source);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
            Accounting.Dispose();
        }
    }

    private sealed class SkillAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public SkillAuthority(bool connect, bool recorderFaults)
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CharacterSkillActivationOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CharacterSkillActivationOutcomeAdapter()
                });
            string runId = "run:character-skill-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                GameplayOutcomeBufferLimits.Default,
                new GameplayOutcomeRunId(runId),
                1L);
            RealRecorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            Faults = recorderFaults
                ? new FaultingRecorder(RealRecorder)
                : null;
            Recorder = Faults ?? (IGameplayOutcomeRecorder)RealRecorder;
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            if (connect)
            {
                binding = CharacterSkillActivationFactMapping.CreateBinding(
                        Registry,
                        Ledger,
                        Recorder,
                        CommitGroup)
                    .Connect(Dispatcher);
            }
            PublicationPort = new SkillPublicationPublisherPort(Dispatcher);
            Publications = new CharacterSkillActivationPublicationRuntime(
                PublicationPort,
                PublicationPort);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder RealRecorder { get; }
        public FaultingRecorder Faults { get; }
        public IGameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public DomainFactDispatcher Dispatcher { get; }
        public SkillPublicationPublisherPort PublicationPort { get; }
        public CharacterSkillActivationPublicationRuntime Publications { get; }

        public void Restore(
            GameplayOutcomeLedgerSaveData ledger,
            CharacterSkillActivationPublicationSaveData publications)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
            Publications.Restore(publications);
        }

        public void Dispose()
        {
            binding?.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class QualityAuthority : IDisposable
    {
        private readonly IDisposable binding;

        public QualityAuthority()
        {
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new ProductQualityOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new ProductQualityOutcomeAdapter()
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 16,
                retainedSmallPageCount: 4,
                retainedLargePageCount: 0);
            string runId = "run:combat-quality-host:"
                + (++authoritySequence).ToString(
                    CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            CommitGroup = new GameplayDomainFactCommitGroup(
                Recorder,
                Ledger);
            Observer = new GameplayDomainFactObserver<
                CombatEquipmentQualityResolvedFact,
                ProductQualityOutcomeReceipt>(
                CombatEquipmentQualityFactMapping.Capture,
                Registry,
                Ledger,
                Recorder,
                CommitGroup);
            Dispatcher = new DomainFactDispatcher();
            binding = Dispatcher.Subscribe(
                "log:combat.equipment-quality",
                Observer);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder Recorder { get; }
        public GameplayDomainFactCommitGroup CommitGroup { get; }
        public GameplayDomainFactObserver<
            CombatEquipmentQualityResolvedFact,
            ProductQualityOutcomeReceipt> Observer { get; }
        public DomainFactDispatcher Dispatcher { get; }

        public void Restore(GameplayOutcomeLedgerSaveData ledger)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
        }

        public void Dispose()
        {
            binding.Dispose();
            Dispatcher.Dispose();
        }
    }

    private sealed class Authority : IDisposable
    {
        private IDisposable binding;

        public Authority(bool connect, bool recorderFaults)
        {
            var josa = new KoreanJosaFormatter();
            Registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new CombatDamageOutcomeDescriptor(),
                    new SocialConflictOutcomeDescriptor(josa)
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new CombatDamageOutcomeAdapter(),
                    new SocialConflictOutcomeAdapter(),
                    new MigratedProducerOutcomeAdapter()
                },
                new IGameplayOutcomeDescriptorCatalog[]
                {
                    new MigratedProducerOutcomeDescriptorCatalog(josa)
                });
            var limits = new GameplayOutcomeBufferLimits(
                smallPageCount: 32,
                largePageCount: 2,
                knownResultKeyCapacity: 128,
                retainedSmallPageCount: 16,
                retainedLargePageCount: 2);
            string runId = "run:combat-action-host:"
                + (++authoritySequence).ToString(CultureInfo.InvariantCulture);
            Ledger = new GameplayOutcomeLedger(
                Registry,
                limits,
                new GameplayOutcomeRunId(runId),
                1L);
            RealRecorder = new GameplayOutcomeRecorder(
                Ledger,
                Registry,
                new GameEventBus());
            Faults = recorderFaults ? new FaultingRecorder(RealRecorder) : null;
            Recorder = Faults ?? (IGameplayOutcomeRecorder)RealRecorder;
            Roots = new DungeonRuntimeAggregateRootStore();
            Migrated = new MigratedProducerOutcomeRuntime(
                Roots,
                Ledger,
                Recorder,
                Ledger);
            Dispatcher = new DomainFactDispatcher();
            Observer = new CombatActionFactObserver(
                Ledger,
                Recorder,
                Migrated,
                new GameplayDomainFactCommitGroup(Recorder, Ledger));
            if (connect)
                binding = Observer.Connect(Dispatcher);
        }

        public GameplayOutcomeRegistry Registry { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public GameplayOutcomeRecorder RealRecorder { get; }
        public FaultingRecorder Faults { get; }
        public IGameplayOutcomeRecorder Recorder { get; }
        public DungeonRuntimeAggregateRootStore Roots { get; }
        public MigratedProducerOutcomeRuntime Migrated { get; }
        public DomainFactDispatcher Dispatcher { get; }
        public CombatActionFactObserver Observer { get; }

        public void Restore(
            GameplayOutcomeLedgerSaveData ledger,
            DungeonMigratedProducerOutcomeSaveData migrated)
        {
            GameplayOutcomeLedgerRestoreCandidate candidate = Ledger
                .PrepareGameplayOutcomeRestore(ledger);
            Ledger.BeginRestoreCandidate();
            try
            {
                Ledger.PublishGameplayOutcomeRestore(candidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }
            catch
            {
                candidate.Discard();
                Ledger.DiscardRestoreCandidate();
                throw;
            }
            Migrated.Restore(Migrated.PrepareRestore(migrated));
        }

        public void Dispose()
        {
            binding?.Dispose();
            binding = null;
            Dispatcher.Dispose();
        }
    }

    /// <summary>
    /// Caller-boundary fault injection: the real publisher completes and
    /// reconciles the batch, then this decorator hides that one response.
    /// </summary>
    private sealed class BatchResponseLossFactPublisher : IDomainFactPublisher
    {
        private readonly IDomainFactPublisher inner;
        private string[] hiddenKeys;

        public BatchResponseLossFactPublisher(IDomainFactPublisher inner) =>
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));

        public int HiddenCompletedBatchResponseCount { get; private set; }

        public void HideNextCompletedBatchResponse(params string[] keys)
        {
            if (hiddenKeys != null || keys == null || keys.Length == 0
                || keys.Any(string.IsNullOrWhiteSpace)
                || keys.Distinct(StringComparer.Ordinal).Count()
                    != keys.Length)
                throw new ArgumentException(
                    "Invalid completed batch response-loss fixture keys.",
                    nameof(keys));
            hiddenKeys = keys.ToArray();
        }

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) =>
            inner.TryReserve(proposed, out lease, out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual) => inner.Publish(lease, actual);

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure)
        {
            bool accepted = inner.PublishBatch(facts, results, out failure);
            if (hiddenKeys == null)
                return accepted;

            string[] expected = hiddenKeys;
            hiddenKeys = null;
            if (!accepted || results == null
                || results.Length != expected.Length
                || results.Any(value => (value.Phase != DomainFactDeliveryPhase.Delivered
                        && value.Phase != DomainFactDeliveryPhase.AcceptedPendingDelivery)
                    || string.IsNullOrEmpty(value.PublicationId)
                    || value.Fingerprint?.Length != 64))
                throw new InvalidOperationException(
                    "Response-loss fixture requires positive committed identities: "
                    + string.Join(",", results.Select(value => value.Phase + ":" + value.PublicationId)));

            failure = "host-physical-carcass-completed-batch-response-hidden";
            for (int index = 0; index < results.Length; index++)
            {
                string operationKey = string.IsNullOrWhiteSpace(
                        results[index].OperationKey)
                    ? expected[index]
                    : results[index].OperationKey;
                if (operationKey != expected[index])
                    throw new InvalidOperationException(
                        "Response-loss fixture observed an unexpected operation key.");
                results[index] = new DomainFactDelivery(
                    DomainFactDeliveryPhase.InDoubt,
                    operationKey,
                    null,
                    null,
                    failure);
            }
            HiddenCompletedBatchResponseCount++;
            return true;
        }

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);
    }

    /// <summary>
    /// Failure injector around the real recorder. Every operation delegates to
    /// the production recorder except the selected one-shot commit edge.
    /// </summary>
    private sealed class FaultingRecorder : IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;

        public FaultingRecorder(IGameplayOutcomeRecorder inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool ThrowBeforeNextBatchCommit { get; set; }
        public bool ThrowAfterNextBatchCommit { get; set; }
        public bool ThrowBeforeNextSingleCommit { get; set; }
        public bool ThrowAfterNextSingleCommit { get; set; }
        public int BeforeCommitFaultCount { get; private set; }
        public int AfterCommitFaultCount { get; private set; }

        public OutcomePrepareResult TryPrepare<TReceipt>(
            in TReceipt receipt,
            out PreparedOutcomeToken prepared) =>
            inner.TryPrepare(receipt, out prepared);

        public OutcomePrepareResult TryReserve(
            in OutcomeWriteRequirements requirements,
            out PreparedOutcomeReservation reservation) =>
            inner.TryReserve(requirements, out reservation);

        public OutcomePrepareResult TryWriteReserved<TReceipt>(
            in TReceipt receipt,
            in PreparedOutcomeReservation reservation,
            out PreparedOutcomeToken prepared) =>
            inner.TryWriteReserved(receipt, reservation, out prepared);

        public void CancelReservation(
            in PreparedOutcomeReservation reservation) =>
            inner.CancelReservation(reservation);

        public void CancelPrepared(in PreparedOutcomeToken prepared) =>
            inner.CancelPrepared(prepared);

        public OutcomeCommitResult CommitPrepared(
            in PreparedOutcomeToken prepared,
            long expectedOwnerRevision,
            out CommittedOutcomeToken committed)
        {
            if (ThrowBeforeNextSingleCommit)
            {
                ThrowBeforeNextSingleCommit = false;
                BeforeCommitFaultCount++;
                throw new InvalidOperationException("offense-result-host-before-single-commit");
            }
            OutcomeCommitResult result = inner.CommitPrepared(
                prepared,
                expectedOwnerRevision,
                out committed);
            if (ThrowAfterNextSingleCommit)
            {
                ThrowAfterNextSingleCommit = false;
                AfterCommitFaultCount++;
                throw new InvalidOperationException(
                    "combat-host-after-single-commit");
            }
            return result;
        }

        public OutcomeCommitResult CommitPreparedBatch(
            PreparedOutcomeToken[] prepared,
            long[] expectedOwnerRevisions,
            CommittedOutcomeToken[] committed)
        {
            if (ThrowBeforeNextBatchCommit)
            {
                ThrowBeforeNextBatchCommit = false;
                BeforeCommitFaultCount++;
                throw new InvalidOperationException(
                    "combat-host-before-atomic-commit");
            }

            OutcomeCommitResult result = inner.CommitPreparedBatch(
                prepared,
                expectedOwnerRevisions,
                committed);
            if (ThrowAfterNextBatchCommit)
            {
                ThrowAfterNextBatchCommit = false;
                AfterCommitFaultCount++;
                throw new InvalidOperationException(
                    "combat-host-after-atomic-commit");
            }
            return result;
        }

        public OutcomeDeliveryResult TryDeliver(
            in CommittedOutcomeToken committed) =>
            inner.TryDeliver(committed);

        public OutcomeAcknowledgeResult Acknowledge(
            in CommittedOutcomeToken committed) =>
            inner.Acknowledge(committed);

        public int RetryPendingDeliveries(int maximumCount) =>
            inner.RetryPendingDeliveries(maximumCount);
    }
}
